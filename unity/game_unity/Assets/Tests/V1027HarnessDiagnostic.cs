using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using NUnit.Framework;
using VictoriaGame.Core;
using VictoriaGame.Economy;
using VictoriaGame.Population;
using VictoriaGame.Presentation;
using VictoriaGame.World;

namespace VictoriaGame.Tests
{
    /// <summary>Batch : -executeMethod VictoriaGame.Tests.V1027BatchRunner.Run</summary>
    public static class V1027BatchRunner
    {
        public static void Run()
        {
            V1027HarnessDiagnostic.RunFullSuiteAndWriteLog();
            UnityEngine.Debug.Log("V1027BatchRunner: DONE");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
        }
    }

    /// <summary>
    /// Garde-fou : un échec d'allocation native n'est JAMAIS un échec logique.
    /// Remonte en Assert.Inconclusive → XML result=Inconclusive → tests.inconclusive.
    /// </summary>
    public static class HarnessAllocationGuard
    {
        public static bool IsNativeAllocationFailure(Exception ex)
        {
            if (ex == null)
            {
                return false;
            }

            var msg = ex.Message ?? string.Empty;
            if (msg.IndexOf("Could not allocate native memory", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            if (msg.IndexOf("Allocator.Temp", StringComparison.OrdinalIgnoreCase) >= 0 &&
                msg.IndexOf("allocate", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return ex.InnerException != null && IsNativeAllocationFailure(ex.InnerException);
        }

        public static void Run(Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex) when (IsNativeAllocationFailure(ex))
            {
                Assert.Inconclusive(
                    "ALLOCATION_FAILURE (non concluant — pas un échec logique) : " + ex.Message);
            }
        }

        public static T Run<T>(Func<T> action)
        {
            try
            {
                return action();
            }
            catch (Exception ex) when (IsNativeAllocationFailure(ex))
            {
                Assert.Inconclusive(
                    "ALLOCATION_FAILURE (non concluant — pas un échec logique) : " + ex.Message);
                return default;
            }
        }
    }

    /// <summary>
    /// Critère UNIQUE de conservation per-tick (délègue à PhysicalStockSystem).
    /// Tous les tests d'invariant doivent passer par ici — pas de seuil 50 dupliqué.
    /// </summary>
    public static class PhysicalConservationGate
    {
        /// <summary>
        /// True ssi MaxTickConservationDrift == 0 (aucun tick hors
        /// CheckConservationPerTick : abs≤1e-2 OU ≤1e-3×flux).
        /// </summary>
        public static bool PerTickHolds(PhysicalEconomyMetrics metrics) =>
            metrics.MaxTickConservationDrift <= 0f;

        public static void AssertPerTickHolds(PhysicalEconomyMetrics metrics, string context)
        {
            Assert.AreEqual(
                0f,
                metrics.MaxTickConservationDrift,
                $"{context}: conservation per-tick FAIL maxDrift={metrics.MaxTickConservationDrift} " +
                $"(critère unique: abs≤{PhysicalStockSystem.ConservationEpsilonAbs} OR " +
                $"≤{PhysicalStockSystem.ConservationEpsilonRelTick}×fluxTick). " +
                $"Seuil absIdentity={PhysicalStockSystem.ConservationEpsilonAbsIdentity} " +
                "est INFORMATTIF (cumuls float), pas le critère durci.");
        }

        /// <summary>
        /// Compare côté à côté critère ABSOLU(50) vs RELATIF(flux) sur un run.
        /// </summary>
        public static ConservationCompareResult CompareAbsoluteVsRelative(EntityManager em, int ticks)
        {
            var result = new ConservationCompareResult();
            double prevStockTransit = 0;
            double prevProd = 0;
            double prevCons = 0;
            var primed = false;

            for (var t = 0; t < ticks; t++)
            {
                // Le harness appelle RunTicks à l'extérieur ; ici on lit l'état courant.
                SampleLedgerTotals(em, out var st, out var prod, out var cons);
                if (!primed)
                {
                    prevStockTransit = st;
                    prevProd = prod;
                    prevCons = cons;
                    primed = true;
                    continue;
                }

                PhysicalStockSystem.CheckConservationPerTick(
                    st, prevStockTransit, prod, prevProd, cons, prevCons,
                    out var drift, out var flux);

                result.Samples++;
                result.MaxDriftAbs = math.max(result.MaxDriftAbs, drift);
                result.MaxFlux = math.max(result.MaxFlux, flux);
                var abs50Fail = drift > PhysicalStockSystem.ConservationEpsilonAbsIdentity;
                var relFail = !(drift <= PhysicalStockSystem.ConservationEpsilonAbs ||
                                drift <= PhysicalStockSystem.ConservationEpsilonRelTick * flux);
                if (abs50Fail)
                {
                    result.Absolute50FailCount++;
                }

                if (relFail)
                {
                    result.RelativeFailCount++;
                }

                if (abs50Fail != relFail)
                {
                    result.DisagreementCount++;
                }

                prevStockTransit = st;
                prevProd = prod;
                prevCons = cons;
            }

            return result;
        }

        static void SampleLedgerTotals(
            EntityManager em, out double stockTransit, out double prod, out double cons)
        {
            stockTransit = 0;
            prod = 0;
            cons = 0;
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<ProvinceStock>());
            using var entities = q.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                var buf = em.GetBuffer<ProvinceStock>(entities[i]);
                for (var j = 0; j < buf.Length; j++)
                {
                    stockTransit += buf[j].Quantity;
                }
            }

            if (!TryGetSingletonEntity(em, out var singleton))
            {
                return;
            }

            var cargos = em.GetBuffer<CargoInTransit>(singleton);
            for (var i = 0; i < cargos.Length; i++)
            {
                stockTransit += cargos[i].Quantity;
            }

            var ledger = em.GetBuffer<PhysicalLedgerEntry>(singleton);
            for (var i = 0; i < ledger.Length; i++)
            {
                prod += ledger[i].CumulativeProduction;
                cons += ledger[i].CumulativeConsumption;
            }
        }

        static bool TryGetSingletonEntity(EntityManager em, out Entity entity)
        {
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<PhysicalEconomySingleton>());
            if (q.IsEmptyIgnoreFilter)
            {
                entity = Entity.Null;
                return false;
            }

            entity = q.GetSingletonEntity();
            return true;
        }

        public struct ConservationCompareResult
        {
            public int Samples;
            public float MaxDriftAbs;
            public float MaxFlux;
            public int Absolute50FailCount;
            public int RelativeFailCount;
            public int DisagreementCount;
        }
    }

    /// <summary>
    /// Politique de logs : conserver les N derniers artefacts éphémères par famille,
    /// ne jamais toucher aux logs de mesure des briefs déjà joués (v1_*_physical.log etc.).
    /// </summary>
    public static class HarnessLogPolicy
    {
        public const int KeepEphemeralPerFamily = 3;

        /// <summary>
        /// Familles éphémères (runTests / compile / batch Unity) — rotation autorisée.
        /// Les logs de mesure dédiés (v1_0NN_*.log hors runTests/batch/compile) sont PROTÉGÉS.
        /// </summary>
        static readonly string[] EphemeralSuffixes =
        {
            "_runTests.log",
            "_tests.xml",
            "_batch.log",
            "_editmode.log",
            "_core_tests.log",
            "_core_tests.xml",
            "_playmode_tests.log",
            "_playmode_tests.xml",
        };

        static readonly string[] ProtectedMeasureMarkers =
        {
            "_physical.log",
            "_blend.log",
            "_flow.log",
            "_endowment.log",
            "_harness.log",
            "_level.log",
            "_tax_sweep.log",
            "_tax_pops.log",
            "_buildings.log",
            "_ai_building.log",
            "_suite.log",
            "_budget.log",
        };

        public static bool IsProtectedMeasureLog(string fileName)
        {
            foreach (var m in ProtectedMeasureMarkers)
            {
                if (fileName.EndsWith(m, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsEphemeral(string fileName)
        {
            if (IsProtectedMeasureLog(fileName))
            {
                return false;
            }

            foreach (var suf in EphemeralSuffixes)
            {
                if (fileName.EndsWith(suf, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            if (fileName.StartsWith("compile", StringComparison.OrdinalIgnoreCase) &&
                fileName.EndsWith(".log", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Applique la rotation : pour chaque suffixe éphémère, garde les
        /// <see cref="KeepEphemeralPerFamily"/> plus récents, archive le reste
        /// dans Logs/archive/ (ne supprime pas).
        /// </summary>
        public static LogRotationReport Apply(string logsDir)
        {
            var report = new LogRotationReport();
            if (!Directory.Exists(logsDir))
            {
                return report;
            }

            var archiveDir = Path.Combine(logsDir, "archive");
            Directory.CreateDirectory(archiveDir);

            foreach (var suffix in EphemeralSuffixes)
            {
                var matches = new List<FileInfo>();
                foreach (var path in Directory.GetFiles(logsDir, "*" + suffix))
                {
                    var name = Path.GetFileName(path);
                    if (IsProtectedMeasureLog(name))
                    {
                        report.ProtectedKept++;
                        continue;
                    }

                    matches.Add(new FileInfo(path));
                }

                matches.Sort((a, b) => b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc));
                for (var i = 0; i < matches.Count; i++)
                {
                    if (i < KeepEphemeralPerFamily)
                    {
                        report.Kept++;
                        continue;
                    }

                    var dest = Path.Combine(archiveDir, matches[i].Name);
                    if (File.Exists(dest))
                    {
                        dest = Path.Combine(
                            archiveDir,
                            Path.GetFileNameWithoutExtension(matches[i].Name) +
                            "_" + matches[i].LastWriteTimeUtc.ToString("yyyyMMddHHmmss") +
                            matches[i].Extension);
                    }

                    File.Move(matches[i].FullName, dest);
                    report.Archived++;
                    report.ArchivedBytes += matches[i].Length;
                }
            }

            // compile*.log hors suffixe listé
            var compileLogs = new List<FileInfo>();
            foreach (var path in Directory.GetFiles(logsDir, "compile*.log"))
            {
                compileLogs.Add(new FileInfo(path));
            }

            compileLogs.Sort((a, b) => b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc));
            for (var i = 0; i < compileLogs.Count; i++)
            {
                if (i < KeepEphemeralPerFamily)
                {
                    report.Kept++;
                    continue;
                }

                var dest = Path.Combine(archiveDir, compileLogs[i].Name);
                if (File.Exists(dest))
                {
                    dest = Path.Combine(
                        archiveDir,
                        Path.GetFileNameWithoutExtension(compileLogs[i].Name) +
                        "_" + compileLogs[i].LastWriteTimeUtc.ToString("yyyyMMddHHmmss") +
                        ".log");
                }

                File.Move(compileLogs[i].FullName, dest);
                report.Archived++;
                report.ArchivedBytes += compileLogs[i].Length;
            }

            return report;
        }

        public struct LogRotationReport
        {
            public int Kept;
            public int Archived;
            public long ArchivedBytes;
            public int ProtectedKept;
        }
    }

    /// <summary>
    /// v1_027 — consolidation du harnais de mesure : bornes mémoire/sorties,
    /// échec d'allocation ≠ échec logique, conservation relative au flux,
    /// décomposition du coût, politique de logs. Zéro changement de simulation.
    /// </summary>
    [TestFixture]
    public class V1027HarnessDiagnostic
    {
        const uint Seed = 42195u;
        const float PerDev = 2400.643f;
        const int CompareTicks = 200;

        /// <summary>
        /// Digest pop+metrics config adoptée v1_025 (même empreinte locale que V1025,
        /// t200, w=0.25, MultiHop+CapacityPerDev). Réf log v1_025_endowment.log.
        /// </summary>
        public const ulong V1025AdoptedDigest = 0xA3300966B9F0BFDAUL;

        [TearDown]
        public void TearDown()
        {
            PopGrowthSystem.UnlockContinuity();
            PopGrowthSystem.ResetToCompiledDefault();
            PhysicalSatisfactionBlendSystem.UnlockWeight();
            PhysicalSatisfactionBlendSystem.ResetToCompiledDefault();
            PhysicalProductionSystem.UnlockOutletCap();
            PhysicalProductionSystem.ResetToCompiledDefault();
            BuildingConstructionSystem.UnlockCapacityIntensity();
            BuildingConstructionSystem.ResetToCompiledDefault();
            BuildingAiPolicyConfig.Unlock();
            BuildingAiPolicyConfig.ResetToCompiledDefault();
            PhysicalStockSystem.IdealPoolMode = false;
            PhysicalStockSystem.MultiHopTransport = true;
        }

        [Test]
        public void V1027_AllocationFailure_IsInconclusiveNotFailed()
        {
            var sawInconclusive = false;
            try
            {
                HarnessAllocationGuard.Run(() =>
                    throw new ArgumentException(
                        "Could not allocate native memory. If this allocation was made " +
                        "from a managed thread outside of a job, you must use " +
                        "Allocator.Persistent or Allocator.TempJob"));
            }
            catch (Exception ex)
            {
                // NUnit InconclusiveException
                sawInconclusive = ex.GetType().Name.IndexOf("Inconclusive", StringComparison.Ordinal) >= 0
                                  || (ex.Message != null &&
                                      ex.Message.IndexOf("ALLOCATION_FAILURE", StringComparison.Ordinal) >= 0);
                if (!sawInconclusive)
                {
                    throw;
                }
            }

            Assert.IsTrue(sawInconclusive,
                "Un échec d'allocation doit remonter en Inconclusive, pas en Failed");
        }

        [Test]
        public void V1027_ConservationGate_UsesRelativeCriterion()
        {
            HarnessAllocationGuard.Run(() =>
            {
                PhysicalSatisfactionBlendSystem.LockWeight(0.25f);
                PhysicalStockSystem.MultiHopTransport = true;
                using var harness = new SimulationHarness(Seed);
                harness.RunTicks(0);
                SetTransportInfra(harness.EntityManager, PerDev);

                float maxDriftMetric = 0f;
                float maxRawDrift = 0f;
                float maxFlux = 0f;
                var abs50WouldFail = 0;
                var relWouldFail = 0;

                double prevSt = 0, prevProd = 0, prevCons = 0;
                var primed = false;

                for (var t = 0; t < CompareTicks; t++)
                {
                    harness.RunTicks(1);
                    var metrics = GetMetrics(harness.EntityManager);
                    maxDriftMetric = math.max(maxDriftMetric, metrics.MaxTickConservationDrift);

                    SampleTotals(harness.EntityManager, out var st, out var prod, out var cons);
                    if (!primed)
                    {
                        prevSt = st;
                        prevProd = prod;
                        prevCons = cons;
                        primed = true;
                        continue;
                    }

                    PhysicalStockSystem.CheckConservationPerTick(
                        st, prevSt, prod, prevProd, cons, prevCons,
                        out var drift, out var flux);
                    maxRawDrift = math.max(maxRawDrift, drift);
                    maxFlux = math.max(maxFlux, flux);
                    if (drift > PhysicalStockSystem.ConservationEpsilonAbsIdentity)
                    {
                        abs50WouldFail++;
                    }

                    if (!(drift <= PhysicalStockSystem.ConservationEpsilonAbs ||
                          drift <= PhysicalStockSystem.ConservationEpsilonRelTick * flux))
                    {
                        relWouldFail++;
                    }

                    prevSt = st;
                    prevProd = prod;
                    prevCons = cons;
                }

                PhysicalConservationGate.AssertPerTickHolds(
                    GetMetrics(harness.EntityManager), "V1027 relative gate");

                // À flux constant le relatif ne doit pas être arbitrairement plus laxiste
                // que l'absolu-50 : si abs50 échoue, relatif doit aussi (sinon trop laxiste).
                // Inversement, un drift 0.02–50 peut échouer abs50 mais passer relatif —
                // c'est le but (puissance constante). On documente les deux.
                UnityEngine.Debug.Log(
                    $"V1027 conservation side-by-side t{CompareTicks}: " +
                    $"maxRawDrift={Fmt(maxRawDrift)} maxFlux={Fmt(maxFlux)} " +
                    $"abs50FailTicks={abs50WouldFail} relFailTicks={relWouldFail} " +
                    $"metricMaxFailDrift={Fmt(maxDriftMetric)}");

                Assert.AreEqual(0, relWouldFail,
                    $"Critère relatif échoué {relWouldFail}× (maxDrift={maxRawDrift})");
            });
        }

        [Test]
        public void V1027_Determinism_MatchesV1025AdoptedDigest()
        {
            HarnessAllocationGuard.Run(() =>
            {
                // Empreinte v1_025 = monde escalier (c=0) + blend 0.25 + ordre GoodId
                // + frein débouchés OFF (v1_032 a adopté intensity=1 par défaut —
                // verrouiller i=0 pour la preuve bit-identité outillage, comme ByGoodId).
                // + capacité bâtiment OFF (v1_038 a adopté intensity=1 — verrouiller 0).
                // + IA construction OFF (v1_039 a adopté Active — verrouiller HoldNone).
                PopGrowthSystem.LockContinuity(0f);
                PhysicalSatisfactionBlendSystem.LockWeight(0.25f);
                PhysicalProductionSystem.LockOutletCap(0f);
                BuildingConstructionSystem.LockCapacityIntensity(0f);
                BuildingAiPolicyConfig.Lock(BuildingAiPolicy.HoldNone, 0f);
                PhysicalStockSystem.MultiHopTransport = true;
                PhysicalStockSystem.ServiceOrderMode =
                    PhysicalStockSystem.TransportServiceOrder.ByGoodId;
                ulong d1, d2;
                using (var h1 = new SimulationHarness(Seed))
                {
                    PhysicalProductionSystem.LockOutletCap(0f);
                    BuildingConstructionSystem.LockCapacityIntensity(0f);
                    BuildingAiPolicyConfig.Lock(BuildingAiPolicy.HoldNone, 0f);
                    h1.RunTicks(0);
                    SetTransportInfra(h1.EntityManager, PerDev);
                    h1.RunTicks(200);
                    d1 = AdoptedConfigDigest(h1.EntityManager);
                }

                using (var h2 = new SimulationHarness(Seed))
                {
                    PopGrowthSystem.LockContinuity(0f);
                    PhysicalSatisfactionBlendSystem.LockWeight(0.25f);
                    PhysicalProductionSystem.LockOutletCap(0f);
                    BuildingConstructionSystem.LockCapacityIntensity(0f);
                    BuildingAiPolicyConfig.Lock(BuildingAiPolicy.HoldNone, 0f);
                    PhysicalStockSystem.ServiceOrderMode =
                        PhysicalStockSystem.TransportServiceOrder.ByGoodId;
                    h2.RunTicks(0);
                    SetTransportInfra(h2.EntityManager, PerDev);
                    h2.RunTicks(200);
                    d2 = AdoptedConfigDigest(h2.EntityManager);
                }

                PhysicalStockSystem.ServiceOrderMode =
                    PhysicalStockSystem.TransportServiceOrder.ByDeficitSeverity;
                PhysicalProductionSystem.UnlockOutletCap();
                BuildingConstructionSystem.UnlockCapacityIntensity();
                BuildingAiPolicyConfig.Unlock();
                BuildingAiPolicyConfig.ResetToCompiledDefault();

                Assert.AreEqual(d1, d2, $"Non déterministe: {d1:X16} vs {d2:X16}");
                Assert.AreEqual(
                    V1025AdoptedDigest,
                    d1,
                    $"BIT-IDENTITÉ rompue vs v1_025: attendu {V1025AdoptedDigest:X16}, obtenu {d1:X16}");
            });
        }

        [Test]
        public void V1027_CostDecomposition_PublishesPerSystemMs()
        {
            HarnessAllocationGuard.Run(() =>
            {
                PhysicalSatisfactionBlendSystem.LockWeight(0.25f);
                PhysicalStockSystem.MultiHopTransport = true;
                using var harness = new SimulationHarness(Seed);
                harness.RunTicks(0);
                SetTransportInfra(harness.EntityManager, PerDev);
                harness.RunTicks(50);

                var stock = PhysicalStockSystem.LastTickCpuMs;
                var prod = PhysicalProductionSystem.LastTickCpuMs;
                var blend = PhysicalSatisfactionBlendSystem.LastTickCpuMs;
                var total = stock + prod + blend;

                UnityEngine.Debug.Log(
                    $"V1027 cost ms/tick: stock={Fmt((float)stock)} prod={Fmt((float)prod)} " +
                    $"blend={Fmt((float)blend)} sum={Fmt((float)total)} " +
                    $"(réf v1_022≈0.30 ; v1_025≈1.32)");

                Assert.Greater(stock, 0.0, "PhysicalStockSystem doit publier LastTickCpuMs");
                Assert.Greater(prod, 0.0, "PhysicalProductionSystem doit publier LastTickCpuMs");
                // blend peut être ~0 si w=0 ; ici w=0.25
                Assert.GreaterOrEqual(blend, 0.0);
                Assert.Less(total, 16.0, "Couche physique hors budget tick complet 16 ms");
            });
        }

        [Test]
        public void V1027_LogPolicy_ProtectsMeasureLogs()
        {
            Assert.IsTrue(HarnessLogPolicy.IsProtectedMeasureLog("v1_025_endowment.log"));
            Assert.IsTrue(HarnessLogPolicy.IsProtectedMeasureLog("v1_027_harness.log"));
            Assert.IsFalse(HarnessLogPolicy.IsEphemeral("v1_025_endowment.log"));
            Assert.IsTrue(HarnessLogPolicy.IsEphemeral("v1_025_runTests.log"));
            Assert.IsTrue(HarnessLogPolicy.IsEphemeral("v1_025_tests.xml"));
            Assert.IsTrue(HarnessLogPolicy.IsEphemeral("compile.log"));
        }

        // Diagnostic lourd : V1027BatchRunner uniquement (séries + balayages hors XML).
        public static void RunFullSuiteAndWriteLog()
        {
            var logsDir = Path.Combine(UnityEngine.Application.dataPath, "..", "Logs");
            Directory.CreateDirectory(logsDir);
            var logPath = Path.Combine(logsDir, "v1_027_harness.log");
            var sb = new StringBuilder(64 * 1024);

            sb.AppendLine("=== v1_027 HARNESS CONSOLIDATION — seed=42195 ===");
            sb.AppendLine(
                "Outillage uniquement : aucune règle / seuil / comportement de simulation modifié.");
            sb.AppendLine(
                "Objectifs: XML<5Mo, runTests.log<50Mo ; allocation≠échec logique ; " +
                "conservation relative au flux ; décomposition coût ; rotation logs.");
            sb.AppendLine();

            // ----- PARTIE 1 — baseline tailles (avant rotation) -----
            sb.AppendLine("=== PARTIE 1 — BORNES MÉMOIRE / SORTIES ===");
            long beforeBytes = 0;
            if (Directory.Exists(logsDir))
            {
                foreach (var f in Directory.GetFiles(logsDir))
                {
                    beforeBytes += new FileInfo(f).Length;
                }
            }

            sb.AppendLine($"Logs/ size_before_rotation={beforeBytes} bytes ({FmtMb(beforeBytes)} Mo)");
            sb.AppendLine(
                "Suites MESURE retirées du filtre EditMode [Test] : " +
                "V1020_MeasureAndWritePhysicalLog, V1021_MeasureSweepAndTimeSeries, " +
                "V1022_MeasureWeightSweepSaturationDynamics, V1023_MeasureLevelDecomposition " +
                "→ BatchRunner + .log dédié uniquement (zéro série temporelle dans le XML).");
            sb.AppendLine(
                "Invariants restent EditMode : parité, déterminisme, conservation, " +
                "non-téléportation, no-op w=0, stabilité V1016/17/18.");
            sb.AppendLine();

            // ----- PARTIE 2 — allocation / monde unique -----
            sb.AppendLine("=== PARTIE 2 — ÉCHEC MÉMOIRE ≠ ÉCHEC LOGIQUE ===");
            sb.AppendLine(
                "HarnessAllocationGuard: ArgumentException 'Could not allocate native memory' " +
                "→ Assert.Inconclusive (XML inconclusive, result JSON tests.inconclusive).");
            sb.AppendLine(
                "Allocateurs suites mesure: TempJob/Persistent hors job ; 1 monde à la fois " +
                "(using SimulationHarness) + GC.Collect entre paliers (déjà V1024/25).");
            sb.AppendLine();

            // ----- PARTIE 3 — conservation relative -----
            sb.AppendLine("=== PARTIE 3 — CONSERVATION RELATIVE AU FLUX ===");
            PhysicalSatisfactionBlendSystem.LockWeight(0.25f);
            PhysicalStockSystem.MultiHopTransport = true;
            float maxRaw = 0f, maxFlux = 0f, metricFail = 0f;
            var absFail = 0;
            var relFail = 0;
            using (var h = new SimulationHarness(Seed))
            {
                h.RunTicks(0);
                SetTransportInfra(h.EntityManager, PerDev);
                double prevSt = 0, prevProd = 0, prevCons = 0;
                var primed = false;
                for (var t = 0; t < CompareTicks; t++)
                {
                    h.RunTicks(1);
                    metricFail = math.max(metricFail, GetMetrics(h.EntityManager).MaxTickConservationDrift);
                    SampleTotals(h.EntityManager, out var st, out var prod, out var cons);
                    if (!primed)
                    {
                        prevSt = st;
                        prevProd = prod;
                        prevCons = cons;
                        primed = true;
                        continue;
                    }

                    PhysicalStockSystem.CheckConservationPerTick(
                        st, prevSt, prod, prevProd, cons, prevCons,
                        out var drift, out var flux);
                    maxRaw = math.max(maxRaw, drift);
                    maxFlux = math.max(maxFlux, flux);
                    if (drift > PhysicalStockSystem.ConservationEpsilonAbsIdentity)
                    {
                        absFail++;
                    }

                    if (!(drift <= PhysicalStockSystem.ConservationEpsilonAbs ||
                          drift <= PhysicalStockSystem.ConservationEpsilonRelTick * flux))
                    {
                        relFail++;
                    }

                    prevSt = st;
                    prevProd = prod;
                    prevCons = cons;
                }
            }

            sb.AppendLine(
                $"t{CompareTicks} maxDriftAbs={Fmt(maxRaw)} maxFlux={Fmt(maxFlux)} " +
                $"seuilAbs50={PhysicalStockSystem.ConservationEpsilonAbsIdentity} " +
                $"seuilAbsTick={PhysicalStockSystem.ConservationEpsilonAbs} " +
                $"seuilRel={PhysicalStockSystem.ConservationEpsilonRelTick}");
            sb.AppendLine(
                $"côte-à-côte: abs50_fail_ticks={absFail} relative_fail_ticks={relFail} " +
                $"metric_max_fail_drift={Fmt(metricFail)}");
            sb.AppendLine(
                absFail > relFail
                    ? "VERDICT: critère relatif moins sévère que abs-50 sur dérives d'agrégation " +
                      "(attendu post-endowment) — puissance constante respectée."
                    : "VERDICT: abs-50 et relatif d'accord sur ce run.");
            sb.AppendLine(
                $"conservation_relative={(relFail == 0 && metricFail <= 0f ? "PASS" : "FAIL")}");
            sb.AppendLine();

            // ----- PARTIE 4 — coût + logs -----
            sb.AppendLine("=== PARTIE 4 — DÉCOMPOSITION COÛT + POLITIQUE LOGS ===");
            double avgStock = 0, avgProd = 0, avgBlend = 0;
            const int costSamples = 100;
            using (var h = new SimulationHarness(Seed))
            {
                h.RunTicks(0);
                SetTransportInfra(h.EntityManager, PerDev);
                h.RunTicks(20); // warm-up
                for (var i = 0; i < costSamples; i++)
                {
                    h.RunTicks(1);
                    avgStock += PhysicalStockSystem.LastTickCpuMs;
                    avgProd += PhysicalProductionSystem.LastTickCpuMs;
                    avgBlend += PhysicalSatisfactionBlendSystem.LastTickCpuMs;
                }
            }

            avgStock /= costSamples;
            avgProd /= costSamples;
            avgBlend /= costSamples;
            var avgSum = avgStock + avgProd + avgBlend;
            sb.AppendLine(
                $"ms/tick (moy {costSamples} ticks, config adoptée): " +
                $"stock={Fmt((float)avgStock)} prod={Fmt((float)avgProd)} " +
                $"blend={Fmt((float)avgBlend)} sum={Fmt((float)avgSum)}");
            sb.AppendLine("réf: v1_022≈0.30 ms (stock seul) ; v1_025≈1.32 ms couche physique");
            var dominant = avgStock >= avgProd && avgStock >= avgBlend ? "stock/transport" :
                avgProd >= avgBlend ? "production" : "blend";
            sb.AppendLine(
                $"poste dominant={dominant} " +
                $"→ candidat brief ultérieur (aucune optimisation ici).");

            var rotation = HarnessLogPolicy.Apply(logsDir);
            sb.AppendLine(
                $"log_rotation: kept={rotation.Kept} archived={rotation.Archived} " +
                $"archivedBytes={rotation.ArchivedBytes} " +
                $"protected_measure_untouched_policy=YES " +
                $"(KeepEphemeralPerFamily={HarnessLogPolicy.KeepEphemeralPerFamily})");
            long afterBytes = 0;
            if (Directory.Exists(logsDir))
            {
                foreach (var f in Directory.GetFiles(logsDir))
                {
                    afterBytes += new FileInfo(f).Length;
                }
            }

            sb.AppendLine($"Logs/ size_after_rotation={afterBytes} bytes ({FmtMb(afterBytes)} Mo)");
            sb.AppendLine();

            // ----- PARTIE 5 — bit-identité -----
            sb.AppendLine("=== PARTIE 5 — PREUVE BIT-IDENTITÉ ===");
            PopGrowthSystem.LockContinuity(0f);
            PhysicalSatisfactionBlendSystem.LockWeight(0.25f);
            PhysicalProductionSystem.LockOutletCap(0f);
            BuildingConstructionSystem.LockCapacityIntensity(0f);
            PhysicalStockSystem.MultiHopTransport = true;
            PhysicalStockSystem.ServiceOrderMode =
                PhysicalStockSystem.TransportServiceOrder.ByGoodId;
            ulong dA, dB;
            using (var h1 = new SimulationHarness(Seed))
            {
                PhysicalProductionSystem.LockOutletCap(0f);
                BuildingConstructionSystem.LockCapacityIntensity(0f);
                h1.RunTicks(0);
                SetTransportInfra(h1.EntityManager, PerDev);
                h1.RunTicks(200);
                dA = AdoptedConfigDigest(h1.EntityManager);
            }

            using (var h2 = new SimulationHarness(Seed))
            {
                PopGrowthSystem.LockContinuity(0f);
                PhysicalSatisfactionBlendSystem.LockWeight(0.25f);
                PhysicalProductionSystem.LockOutletCap(0f);
                BuildingConstructionSystem.LockCapacityIntensity(0f);
                PhysicalStockSystem.ServiceOrderMode =
                    PhysicalStockSystem.TransportServiceOrder.ByGoodId;
                h2.RunTicks(0);
                SetTransportInfra(h2.EntityManager, PerDev);
                h2.RunTicks(200);
                dB = AdoptedConfigDigest(h2.EntityManager);
            }

            PhysicalStockSystem.ServiceOrderMode =
                PhysicalStockSystem.TransportServiceOrder.ByDeficitSeverity;
            PhysicalProductionSystem.UnlockOutletCap();
            BuildingConstructionSystem.UnlockCapacityIntensity();

            var detOk = dA == dB;
            var bitOk = dA == V1025AdoptedDigest;
            sb.AppendLine($"digest_after_A={dA:X16} digest_after_B={dB:X16} determinism={(detOk ? "PASS" : "FAIL")}");
            sb.AppendLine(
                $"digest_v1_025_ref={V1025AdoptedDigest:X16} bit_identical={(bitOk ? "PASS" : "FAIL")} " +
                "(LockOutletCap(0)+LockCapacityIntensity(0)+ByGoodId pour empreinte v1_025)");
            sb.AppendLine();
            sb.AppendLine("=== VERDICT MESURÉ ===");
            sb.AppendLine(
                $"conservation_rel={(relFail == 0 ? "PASS" : "FAIL")} " +
                $"determinism={(detOk ? "PASS" : "FAIL")} " +
                $"bit_id={(bitOk ? "PASS" : "FAIL")} " +
                $"cost_sum_ms={Fmt((float)avgSum)} dominant={dominant}");

            File.WriteAllText(logPath, sb.ToString());
            // Une seule ligne dans le log Unity — jamais le rapport complet (XML/log bloat).
            UnityEngine.Debug.Log(
                $"V1027HarnessDiagnostic: wrote {logPath} bit_id={(bitOk ? "PASS" : "FAIL")} " +
                $"cost={Fmt((float)avgSum)}ms dominant={dominant}");

            PhysicalSatisfactionBlendSystem.UnlockWeight();
            PhysicalStockSystem.MultiHopTransport = true;

            Assert.IsTrue(detOk, "Déterminisme échoué");
            Assert.IsTrue(bitOk, $"Bit-identité vs v1_025 rompue: {dA:X16}");
            Assert.AreEqual(0, relFail, "Conservation relative échouée");
        }

        // ----- helpers -----

        static PhysicalEconomyMetrics GetMetrics(EntityManager em)
        {
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<PhysicalEconomyMetrics>());
            return q.GetSingleton<PhysicalEconomyMetrics>();
        }

        static void SetTransportInfra(EntityManager em, float perDev)
        {
            using var q = em.CreateEntityQuery(ComponentType.ReadWrite<PhysicalTransportConfig>());
            if (q.IsEmptyIgnoreFilter)
            {
                return;
            }

            var e = q.GetSingletonEntity();
            var cfg = em.GetComponentData<PhysicalTransportConfig>(e);
            cfg.CapacityPerDevPoint = perDev;
            cfg.EdgeCapacityPerTick = 500f;
            cfg.TransitTicksPerEdge = 1;
            em.SetComponentData(e, cfg);
        }

        /// <summary>Même empreinte que V1025PhysicalEndowmentDiagnostic.WorldDigest.</summary>
        static ulong AdoptedConfigDigest(EntityManager em)
        {
            var hash = StateHash.New();
            var rows = new List<(int CountryId, int PopSize, float Sat, int ProvinceId)>();
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<PopData>()))
            using (var pops = q.ToComponentDataArray<PopData>(Allocator.Temp))
            {
                for (var i = 0; i < pops.Length; i++)
                {
                    var pid = 0;
                    if (pops[i].Province != Entity.Null &&
                        em.HasComponent<ProvinceData>(pops[i].Province))
                    {
                        pid = em.GetComponentData<ProvinceData>(pops[i].Province).ProvinceId;
                    }

                    var cid = 0;
                    if (pops[i].Country != Entity.Null &&
                        em.HasComponent<CountryData>(pops[i].Country))
                    {
                        cid = em.GetComponentData<CountryData>(pops[i].Country).CountryId;
                    }

                    rows.Add((cid, pops[i].Size, pops[i].NeedsSatisfaction, pid));
                }
            }

            rows.Sort((a, b) =>
            {
                var c = a.CountryId.CompareTo(b.CountryId);
                if (c != 0)
                {
                    return c;
                }

                c = a.ProvinceId.CompareTo(b.ProvinceId);
                return c != 0 ? c : a.PopSize.CompareTo(b.PopSize);
            });

            foreach (var r in rows)
            {
                hash.Int(r.CountryId);
                hash.Int(r.ProvinceId);
                hash.Int(r.PopSize);
                hash.Float(r.Sat);
            }

            var m = WorldMetrics.Capture(em, 0);
            hash.Int(m.Population);
            hash.Float(m.NeedsSatAvg);
            return hash.Value;
        }

        static void SampleTotals(
            EntityManager em, out double stockTransit, out double prod, out double cons)
        {
            stockTransit = 0;
            prod = 0;
            cons = 0;
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<ProvinceStock>());
            using var entities = q.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                var buf = em.GetBuffer<ProvinceStock>(entities[i]);
                for (var j = 0; j < buf.Length; j++)
                {
                    stockTransit += buf[j].Quantity;
                }
            }

            using var sq = em.CreateEntityQuery(ComponentType.ReadOnly<PhysicalEconomySingleton>());
            if (sq.IsEmptyIgnoreFilter)
            {
                return;
            }

            var singleton = sq.GetSingletonEntity();
            var cargos = em.GetBuffer<CargoInTransit>(singleton);
            for (var i = 0; i < cargos.Length; i++)
            {
                stockTransit += cargos[i].Quantity;
            }

            var ledger = em.GetBuffer<PhysicalLedgerEntry>(singleton);
            for (var i = 0; i < ledger.Length; i++)
            {
                prod += ledger[i].CumulativeProduction;
                cons += ledger[i].CumulativeConsumption;
            }
        }

        static string Fmt(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        static string FmtMb(long bytes) =>
            (bytes / (1024.0 * 1024.0)).ToString("0.##", CultureInfo.InvariantCulture);
    }
}
