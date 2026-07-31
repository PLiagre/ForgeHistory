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
    /// <summary>Batch : -executeMethod VictoriaGame.Tests.V1026BatchRunner.Run</summary>
    public static class V1026BatchRunner
    {
        public static void Run()
        {
            try
            {
                V1026DemographicResponseDiagnostic.RunFullSuiteAndWriteLog();
                UnityEngine.Debug.Log("V1026BatchRunner: DONE");
            }
            catch (Exception ex) when (HarnessAllocationGuard.IsNativeAllocationFailure(ex))
            {
                UnityEngine.Debug.LogWarning(
                    "V1026BatchRunner: ALLOCATION_FAILURE (charge harnais) — " + ex.Message);
                UnityEngine.Debug.Log("V1026BatchRunner: DONE_PARTIAL");
            }
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
        }
    }

    /// <summary>
    /// v1_026 — diagnostic de la falaise démographique (escalier 0.2/0.5/0.8) et
    /// introduction d'une réponse continue réversible (ResponseContinuity).
    /// Mesures lourdes via BatchRunner uniquement (21 runs t3000 hors EditMode).
    /// </summary>
    [TestFixture]
    public class V1026DemographicResponseDiagnostic
    {
        const uint Seed = 42195u;
        const float PerDev = 2400.643f;
        const float CliffEps = 0.02f;

        static readonly float[] ContinuitySweep = { 0f, 0.5f, 1f };
        static readonly float[] WeightSweep =
            { 0f, 0.1f, 0.25f, 0.4f, 0.5f, 0.75f, 1.0f };
        // Falaise fine : points hors grille principale (0.25/0.4/0.5 déjà dans WeightSweep).
        static readonly float[] CliffWeightExtra = { 0.3f, 0.35f, 0.45f };

        // Couple adopté — mis à jour par RunFullSuiteAndWriteLog après mesure.
        public static float AdoptedContinuity = 0.5f;
        public static float AdoptedWeight = 0.25f;

        [TearDown]
        public void TearDown()
        {
            PopGrowthSystem.UnlockContinuity();
            PopGrowthSystem.ResetToCompiledDefault();
            PhysicalSatisfactionBlendSystem.UnlockWeight();
            PhysicalSatisfactionBlendSystem.ResetToCompiledDefault();
            PhysicalStockSystem.IdealPoolMode = false;
            PhysicalStockSystem.MultiHopTransport = true;
        }

        [Test]
        public void V1026_ContinuousRate_PassesThroughAnchors()
        {
            Assert.AreEqual(
                PopGrowthSystem.RateBelow02,
                PopGrowthSystem.ContinuousGrowthRate(0f),
                1e-7f);
            Assert.AreEqual(
                PopGrowthSystem.RateAt02,
                PopGrowthSystem.ContinuousGrowthRate(0.2f),
                1e-7f);
            Assert.AreEqual(
                PopGrowthSystem.RateAt05,
                PopGrowthSystem.ContinuousGrowthRate(0.5f),
                1e-7f);
            Assert.AreEqual(
                PopGrowthSystem.RateAt08,
                PopGrowthSystem.ContinuousGrowthRate(0.8f),
                1e-7f);
            Assert.AreEqual(
                PopGrowthSystem.RateAt08,
                PopGrowthSystem.ContinuousGrowthRate(1f),
                1e-7f);

            // Entre deux marches : strictement entre les ancrages (plus de palier plat).
            var mid = PopGrowthSystem.ContinuousGrowthRate(0.35f);
            Assert.Greater(mid, PopGrowthSystem.RateAt02);
            Assert.Less(mid, PopGrowthSystem.RateAt05);

            // Aux seuils, escalier == continu (blend trivial).
            Assert.AreEqual(
                PopGrowthSystem.StaircaseGrowthRate(0.2f),
                PopGrowthSystem.ContinuousGrowthRate(0.2f),
                1e-7f);
            Assert.AreEqual(
                PopGrowthSystem.StaircaseGrowthRate(0.5f),
                PopGrowthSystem.ContinuousGrowthRate(0.5f),
                1e-7f);
            Assert.AreEqual(
                PopGrowthSystem.StaircaseGrowthRate(0.8f),
                PopGrowthSystem.ContinuousGrowthRate(0.8f),
                1e-7f);
        }

        [Test]
        public void V1026_ContinuityZero_Determinism()
        {
            HarnessAllocationGuard.Run(() =>
            {
                PopGrowthSystem.LockContinuity(0f);
                PhysicalSatisfactionBlendSystem.LockWeight(0f);
                PhysicalStockSystem.MultiHopTransport = true;
                ulong d1, d2;
                using (var h1 = new SimulationHarness(Seed))
                {
                    h1.RunTicks(0);
                    SetTransportInfra(h1.EntityManager, PerDev);
                    h1.RunTicks(80);
                    d1 = WorldDigest(h1.EntityManager);
                }

                using (var h2 = new SimulationHarness(Seed))
                {
                    PopGrowthSystem.LockContinuity(0f);
                    PhysicalSatisfactionBlendSystem.LockWeight(0f);
                    h2.RunTicks(0);
                    SetTransportInfra(h2.EntityManager, PerDev);
                    h2.RunTicks(80);
                    d2 = WorldDigest(h2.EntityManager);
                }

                Assert.AreEqual(d1, d2, $"Non déterministe c=0 w=0: {d1:X16} vs {d2:X16}");
            });
        }

        [Test]
        public void V1026_ContinuityZero_WeightZero_NoOpParityDigest()
        {
            // À c=0 w=0 le monde reste bit-identique (chemin escalier original).
            HarnessAllocationGuard.Run(() =>
            {
                PopGrowthSystem.LockContinuity(0f);
                PhysicalSatisfactionBlendSystem.LockWeight(0f);
                PhysicalStockSystem.MultiHopTransport = true;
                ulong dA, dB;
                using (var h1 = new SimulationHarness(Seed))
                {
                    h1.RunTicks(0);
                    SetTransportInfra(h1.EntityManager, PerDev);
                    h1.RunTicks(100);
                    dA = WorldDigest(h1.EntityManager);
                }

                using (var h2 = new SimulationHarness(Seed))
                {
                    PopGrowthSystem.LockContinuity(0f);
                    PhysicalSatisfactionBlendSystem.LockWeight(0f);
                    h2.RunTicks(0);
                    SetTransportInfra(h2.EntityManager, PerDev);
                    h2.RunTicks(100);
                    dB = WorldDigest(h2.EntityManager);
                }

                Assert.AreEqual(dA, dB);
            });
        }

        [Test]
        public void V1026_Determinism_AdoptedConfig()
        {
            HarnessAllocationGuard.Run(() =>
            {
                var c = AdoptedContinuity;
                var w = AdoptedWeight;
                PopGrowthSystem.LockContinuity(c);
                PhysicalSatisfactionBlendSystem.LockWeight(w);
                PhysicalStockSystem.MultiHopTransport = true;
                ulong d1, d2;
                using (var h1 = new SimulationHarness(Seed))
                {
                    h1.RunTicks(0);
                    SetTransportInfra(h1.EntityManager, PerDev);
                    h1.RunTicks(120);
                    d1 = WorldDigest(h1.EntityManager);
                }

                using (var h2 = new SimulationHarness(Seed))
                {
                    PopGrowthSystem.LockContinuity(c);
                    PhysicalSatisfactionBlendSystem.LockWeight(w);
                    h2.RunTicks(0);
                    SetTransportInfra(h2.EntityManager, PerDev);
                    h2.RunTicks(120);
                    d2 = WorldDigest(h2.EntityManager);
                }

                Assert.AreEqual(d1, d2, $"Non déterministe adopté c={c} w={w}: {d1:X16} vs {d2:X16}");
            });
        }

        [Test]
        public void V1026_Conservation_PerTick_AdoptedConfig()
        {
            HarnessAllocationGuard.Run(() =>
            {
                PopGrowthSystem.LockContinuity(AdoptedContinuity);
                PhysicalSatisfactionBlendSystem.LockWeight(AdoptedWeight);
                PhysicalStockSystem.MultiHopTransport = true;
                using var harness = new SimulationHarness(Seed);
                harness.RunTicks(0);
                SetTransportInfra(harness.EntityManager, PerDev);
                harness.RunTicks(150);
                PhysicalConservationGate.AssertPerTickHolds(
                    GetMetrics(harness.EntityManager), "V1026 adopted");
            });
        }

        [Test]
        public void V1026_ContinuityPositive_ChangesWorld_WhenStarving()
        {
            // À w>0, c=1 doit diverger de c=0 (sinon la continuité est un no-op déguisé).
            HarnessAllocationGuard.Run(() =>
            {
                PhysicalStockSystem.MultiHopTransport = true;
                ulong stair, cont;
                using (var h1 = new SimulationHarness(Seed))
                {
                    PopGrowthSystem.LockContinuity(0f);
                    PhysicalSatisfactionBlendSystem.LockWeight(0.5f);
                    h1.RunTicks(0);
                    SetTransportInfra(h1.EntityManager, PerDev);
                    h1.RunTicks(200);
                    stair = WorldDigest(h1.EntityManager);
                }

                using (var h2 = new SimulationHarness(Seed))
                {
                    PopGrowthSystem.LockContinuity(1f);
                    PhysicalSatisfactionBlendSystem.LockWeight(0.5f);
                    h2.RunTicks(0);
                    SetTransportInfra(h2.EntityManager, PerDev);
                    h2.RunTicks(200);
                    cont = WorldDigest(h2.EntityManager);
                }

                Assert.AreNotEqual(stair, cont,
                    "c=1 w=0.5 devrait diverger de l'escalier (sinon continuité inerte)");
            });
        }

        // Diagnostic lourd : V1026BatchRunner uniquement (évite OOM EditMode).
        public static void RunFullSuiteAndWriteLog()
        {
            var logPath = Path.Combine(
                UnityEngine.Application.dataPath, "..", "Logs", "v1_026_demographic.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            var sb = new StringBuilder(512 * 1024);

            sb.AppendLine("=== v1_026 DEMOGRAPHIC RESPONSE DIAGNOSTIC — seed=42195 ===");
            sb.AppendLine(
                "Config: CapacityPerDev + MultiHop=ON + terrain_endowment (v1_025).");
            sb.AppendLine(
                "Réf v1_025: palier_poids=0.25 popRatio=0.892 (−10.8%) physMean=0.434 starved≈22.");
            sb.AppendLine();

            PopGrowthSystem.LockContinuity(0f);
            PhysicalSatisfactionBlendSystem.LockWeight(0f);
            PhysicalStockSystem.IdealPoolMode = false;
            PhysicalStockSystem.MultiHopTransport = true;

            // ========== PARTIE 1 — MESURER LA FALAISE ==========
            sb.AppendLine("=== PARTIE 1 — DISTRIBUTION SATISFACTION & EXPOSITION FALAISE ===");
            SatDistribution dist;
            using (var h = new SimulationHarness(Seed))
            {
                PopGrowthSystem.LockContinuity(0f);
                PhysicalSatisfactionBlendSystem.LockWeight(0.25f);
                h.RunTicks(0);
                SetTransportInfra(h.EntityManager, PerDev);
                h.RunTicks(500);
                dist = MeasureSatDistribution(h.EntityManager);
            }

            AppendDistribution(sb, dist, "w=0.25 c=0 t500");

            sb.AppendLine();
            sb.AppendLine(
                "--- Falaise: distribution ci-dessus ; courbe poids dérivée de la grille c=0 (PARTIE 3) ---");
            sb.AppendLine(
                "Extras falaise hors grille (0.3/0.35/0.45) à c=0 t3000:");
            sb.AppendLine(
                "weight\tpop\tsatAvg\tphysMean\tcliffShare\tstarved\tdebt\tarmy\tstatus");

            var allocFails = 0;
            TrajectoryReport localStair = default;
            foreach (var w in CliffWeightExtra)
            {
                if (!TryRunCrossPoint(0f, w, out var row, out var err))
                {
                    allocFails++;
                    sb.AppendLine($"{Fmt(w)}\tALLOC_FAIL\t{err}");
                    ForceGc();
                    continue;
                }

                sb.AppendLine(
                    $"{Fmt(w)}\t{row.Pop}\t{Fmt(row.SatAvg)}\t{Fmt(row.PhysMean)}\t" +
                    $"{Fmt(row.CliffShare)}\t{row.Starved}\t{Fmt(row.Debt)}\t{Fmt(row.Army)}\tOK");
                ForceGc();
            }

            sb.AppendLine();
            sb.AppendLine("--- Sensibilité locale : province physSat≈0, trajectoire t1500 (c=0 w=0.25) ---");
            if (TryMeasureZeroPhysTrajectory(0f, 0.25f, 1500, 50, out localStair, out var trajErr))
            {
                AppendTrajectory(sb, localStair, "escalier");
            }
            else
            {
                allocFails++;
                sb.AppendLine($"traj escalier ALLOC_FAIL: {trajErr}");
            }

            ForceGc();
            sb.AppendLine();

            // ========== PARTIE 2 — mécanisme (déjà dans PopGrowthSystem) ==========
            sb.AppendLine("=== PARTIE 2 — RÉPONSE CONTINUE RÉVERSIBLE ===");
            sb.AppendLine(
                "ResponseContinuity ∈ [0..1] : JSON demographic_response_continuity.json + static.");
            sb.AppendLine(
                "c=0 → chemin escalier ORIGINAL (math.select). c=1 → ContinuousGrowthRate.");
            sb.AppendLine(
                $"Ancrages inchangés: {PopGrowthSystem.RateBelow02}, {PopGrowthSystem.RateAt02}, " +
                $"{PopGrowthSystem.RateAt05}, {PopGrowthSystem.RateAt08} @ 0.2/0.5/0.8.");
            sb.AppendLine(
                $"Vérif ancrages: r(0.2)={Fmt(PopGrowthSystem.ContinuousGrowthRate(0.2f))} " +
                $"r(0.5)={Fmt(PopGrowthSystem.ContinuousGrowthRate(0.5f))} " +
                $"r(0.8)={Fmt(PopGrowthSystem.ContinuousGrowthRate(0.8f))}");
            sb.AppendLine();

            // ========== PARTIE 3 — BALAYAGE CROISÉ ==========
            sb.AppendLine("=== PARTIE 3 — BALAYAGE CONTINUITÉ × POIDS (t3000) ===");
            sb.AppendLine(
                "cont\tweight\tpop\tpopRatio\tsatAvg\tphysMean\tstarved\tdebt\tbankrupt\t" +
                "army\tcountries\twars\tmigrate\talive\tcpuMs\tcliffShare\tstatus");

            var grid = new List<CrossRow>();
            CrossRow? baseline = null;

            foreach (var c in ContinuitySweep)
            {
                foreach (var w in WeightSweep)
                {
                    // Si c=0 et déjà capturé en falaise, on rejoue quand même pour
                    // homogénéité migrate/cpu — mais on pourrait skip. On rejoue
                    // uniquement si pas déjà dans grid pour ce (c,w).
                    if (!TryRunCrossPoint(c, w, out var row, out var err))
                    {
                        allocFails++;
                        sb.AppendLine(
                            $"{Fmt(c)}\t{Fmt(w)}\tALLOC_FAIL\t{err}");
                        ForceGc();
                        continue;
                    }

                    grid.Add(row);
                    if (c <= 0f && w <= 0f)
                    {
                        baseline = row;
                    }

                    var popRatio = baseline.HasValue && baseline.Value.Pop > 0
                        ? (float)row.Pop / baseline.Value.Pop
                        : 1f;
                    var alive = IsAlive(row, baseline);
                    sb.AppendLine(
                        $"{Fmt(c)}\t{Fmt(w)}\t{row.Pop}\t{Fmt(popRatio)}\t{Fmt(row.SatAvg)}\t" +
                        $"{Fmt(row.PhysMean)}\t{row.Starved}\t{Fmt(row.Debt)}\t{row.Bankrupt}\t" +
                        $"{Fmt(row.Army)}\t{row.Countries}\t{row.Wars}\t{row.MigrateEst}\t" +
                        $"{(alive ? "Y" : "N")}\t{Fmt(row.CpuMs)}\t{Fmt(row.CliffShare)}\tOK");
                    ForceGc();
                }
            }

            sb.AppendLine($"allocation_failures_so_far={allocFails} grid_points={grid.Count}/21");
            // Snapshot disque après grille (évite perte totale si OOM ensuite).
            File.WriteAllText(logPath, sb.ToString());

            if (grid.Count == 0)
            {
                sb.AppendLine("=== VERDICT MESURÉ ===");
                sb.AppendLine(
                    "ÉCHEC CHARGE: aucun point de grille mesuré (ALLOCATION_FAILURE). " +
                    "PARTIE 1 distribution livrée ci-dessus ; mécanisme ResponseContinuity compilé.");
                File.WriteAllText(logPath, sb.ToString());
                UnityEngine.Debug.Log($"V1026DemographicResponseDiagnostic: wrote {logPath} (PARTIAL OOM)");
                throw new ArgumentException(
                    "Could not allocate native memory during V1026 grid sweep");
            }

            // Courbe falaise dérivée de la grille c=0
            sb.AppendLine("--- Courbe falaise (c=0, depuis grille) ---");
            sb.AppendLine("weight\tpop\tpopRatio\tsatAvg\tphysMean\tcliffShare\tstarved\talive");
            CrossRow? c0base = null;
            foreach (var w in WeightSweep)
            {
                var row = FindRow(grid, 0f, w);
                if (row.Pop == 0 && math.abs(row.Weight - w) > 1e-4f)
                {
                    continue;
                }

                if (!c0base.HasValue && w <= 0f)
                {
                    c0base = row;
                }

                var br = c0base ?? baseline;
                var popRatio = br.HasValue && br.Value.Pop > 0
                    ? (float)row.Pop / br.Value.Pop
                    : 1f;
                sb.AppendLine(
                    $"{Fmt(w)}\t{row.Pop}\t{Fmt(popRatio)}\t{Fmt(row.SatAvg)}\t{Fmt(row.PhysMean)}\t" +
                    $"{Fmt(row.CliffShare)}\t{row.Starved}\t{(IsAlive(row, baseline) ? "Y" : "N")}");
            }

            sb.AppendLine();

            // Choisir le meilleur compromis : vivant, popRatio max parmi c>0, ou c=0 si rien.
            var adopted = PickAdopted(grid, baseline);
            AdoptedContinuity = adopted.Continuity;
            AdoptedWeight = adopted.Weight;
            sb.AppendLine();
            sb.AppendLine(
                $"ADOPTÉ: continuité={Fmt(adopted.Continuity)} poids={Fmt(adopted.Weight)} " +
                $"pop={adopted.Pop} satAvg={Fmt(adopted.SatAvg)} starved={adopted.Starved} " +
                $"reason={adopted.PickReason}");

            // Question centrale
            var stair025 = FindRow(grid, 0f, 0.25f);
            var stair05 = FindRow(grid, 0f, 0.5f);
            var cont04 = FindRow(grid, 1f, 0.4f);
            var cont025 = FindRow(grid, 1f, 0.25f);
            var cont05 = FindRow(grid, 1f, 0.5f);
            var midViable = HasIntermediatePlateau(grid, baseline);
            sb.AppendLine(
                $"QUESTION PALIER: escalier w0.25 pop={stair025.Pop} / w0.5 pop={stair05.Pop} ; " +
                $"continu w0.25 pop={cont025.Pop} / w0.4 pop={cont04.Pop} / w0.5 pop={cont05.Pop}");
            sb.AppendLine(
                midViable
                    ? "VERDICT PALIER: OUI — la continuité ouvre un palier intermédiaire viable " +
                      "là où l'escalier n'offrait que 0.25 (peu sensible) ou 0.5 (effondrement)."
                    : "VERDICT PALIER: NON — la continuité n'ouvre pas de palier viable net entre " +
                      "0.25 et 0.5 ; la brutalité vient d'ailleurs que de l'escalier (ou le " +
                      "compromis reste à w≤0.25).");
            sb.AppendLine();

            // ========== PARTIE 4 — MONDE SENSIBLE ==========
            sb.AppendLine("=== PARTIE 4 — SENSIBILITÉ AU COUPLE ADOPTÉ ===");
            sb.AppendLine("--- Série temporelle (≥ tous les 50 ticks, t1500 pour charge) ---");
            SeriesReport series = default;
            if (TryMeasureTimeSeries(adopted.Continuity, adopted.Weight, 1500, 50, out series, out var serErr))
            {
                sb.AppendLine(
                    "tick\tpop\tsatAvg\tphysMean\tstarved\tdebt\tarmy\tmigrate\tcpuMs");
                foreach (var s in series.Points)
                {
                    sb.AppendLine(
                        $"{s.Tick}\t{s.Pop}\t{Fmt(s.SatAvg)}\t{Fmt(s.PhysMean)}\t{s.Starved}\t" +
                        $"{Fmt(s.Debt)}\t{Fmt(s.Army)}\t{s.Migrate}\t{Fmt(s.CpuMs)}");
                }
            }
            else
            {
                allocFails++;
                sb.AppendLine($"série ALLOC_FAIL: {serErr}");
            }

            ForceGc();
            sb.AppendLine();
            sb.AppendLine("--- Province physSat≈0 au couple adopté (t1500) ---");
            TrajectoryReport localAdopted = default;
            if (TryMeasureZeroPhysTrajectory(
                    adopted.Continuity, adopted.Weight, 1500, 50, out localAdopted, out var locErr))
            {
                AppendTrajectory(sb, localAdopted, "adopté");
                sb.AppendLine(
                    $"deltaPop phys=0: escalier={localStair.DeltaPop} adopté={localAdopted.DeltaPop} " +
                    $"(lisible si |delta|≫75)");
            }
            else
            {
                allocFails++;
                sb.AppendLine($"traj adopté ALLOC_FAIL: {locErr}");
            }

            ForceGc();
            sb.AppendLine();
            EmergentStory story = default;
            try
            {
                story = FindEmergentStory(adopted.Continuity, adopted.Weight);
            }
            catch (Exception ex) when (HarnessAllocationGuard.IsNativeAllocationFailure(ex))
            {
                allocFails++;
                sb.AppendLine($"récit ALLOC_FAIL: {ex.Message}");
            }

            if (story.Found)
            {
                sb.AppendLine(
                    $"récit émergent: province {story.ProvinceId} phys={Fmt(story.PhysSat)} " +
                    $"pop0={story.Pop0} pop1={story.Pop1} deltaPop={story.DeltaPop} " +
                    $"migrateIn={story.MigrateIn} note={story.Note}");
            }
            else
            {
                sb.AppendLine(
                    "récit émergent: ABSENCE — aucune province n'enchaîne famine→déclin→migration " +
                    "de façon isolable sur cette fenêtre.");
            }

            sb.AppendLine(
                $"écart-type trajectoires pop provinciales: {Fmt(series.PopDeltaStd)} " +
                $"(destins différenciés si std élevée ; fonte uniforme si ≈0)");
            sb.AppendLine($"allocation_failures_total={allocFails}");
            sb.AppendLine();

            // ========== PARTIE 5 — GARDE-FOUS ==========
            sb.AppendLine("=== PARTIE 5 — GARDE-FOUS ===");

            // Déterminisme c=0 w=0
            PopGrowthSystem.LockContinuity(0f);
            PhysicalSatisfactionBlendSystem.LockWeight(0f);
            ulong d0a, d0b;
            using (var h1 = new SimulationHarness(Seed))
            {
                h1.RunTicks(0);
                SetTransportInfra(h1.EntityManager, PerDev);
                h1.RunTicks(200);
                d0a = WorldDigest(h1.EntityManager);
            }

            using (var h2 = new SimulationHarness(Seed))
            {
                PopGrowthSystem.LockContinuity(0f);
                PhysicalSatisfactionBlendSystem.LockWeight(0f);
                h2.RunTicks(0);
                SetTransportInfra(h2.EntityManager, PerDev);
                h2.RunTicks(200);
                d0b = WorldDigest(h2.EntityManager);
            }

            var det00 = d0a == d0b;
            sb.AppendLine($"determinisme c=0 w=0 t200: {(det00 ? "PASS" : "FAIL")} ({d0a:X16})");

            // Déterminisme adopté
            PopGrowthSystem.LockContinuity(adopted.Continuity);
            PhysicalSatisfactionBlendSystem.LockWeight(adopted.Weight);
            ulong da, db;
            using (var h1 = new SimulationHarness(Seed))
            {
                h1.RunTicks(0);
                SetTransportInfra(h1.EntityManager, PerDev);
                h1.RunTicks(200);
                da = WorldDigest(h1.EntityManager);
            }

            using (var h2 = new SimulationHarness(Seed))
            {
                PopGrowthSystem.LockContinuity(adopted.Continuity);
                PhysicalSatisfactionBlendSystem.LockWeight(adopted.Weight);
                h2.RunTicks(0);
                SetTransportInfra(h2.EntityManager, PerDev);
                h2.RunTicks(200);
                db = WorldDigest(h2.EntityManager);
            }

            var detAd = da == db;
            sb.AppendLine(
                $"determinisme adopté c={Fmt(adopted.Continuity)} w={Fmt(adopted.Weight)} t200: " +
                $"{(detAd ? "PASS" : "FAIL")} ({da:X16})");

            // Conservation
            PopGrowthSystem.LockContinuity(adopted.Continuity);
            PhysicalSatisfactionBlendSystem.LockWeight(adopted.Weight);
            float maxDrift;
            using (var h = new SimulationHarness(Seed))
            {
                h.RunTicks(0);
                SetTransportInfra(h.EntityManager, PerDev);
                h.RunTicks(200);
                maxDrift = GetMetrics(h.EntityManager).MaxTickConservationDrift;
            }

            var consOk = maxDrift <= 0f;
            sb.AppendLine(
                $"conservation per-tick (relatif flux): {(consOk ? "PASS" : "FAIL")} " +
                $"maxDrift={Fmt(maxDrift)}");

            var cpu = adopted.CpuMs;
            sb.AppendLine($"cpuMs/tick (échantillon adopté t3000 last): {Fmt(cpu)}");
            sb.AppendLine(
                "Parité V1009 / V1016-18 : exécutés en EditMode (filtre XML) — voir v1_026_tests.xml.");
            sb.AppendLine(
                "Registre: escalier démographique = dette EN COURS DE CONVERSION (entrée #7).");
            sb.AppendLine();

            sb.AppendLine("=== VERDICT MESURÉ ===");
            sb.AppendLine(
                midViable
                    ? $"La continuité ouvre un palier viable à w={Fmt(adopted.Weight)} " +
                      $"(c={Fmt(adopted.Continuity)}, pop={adopted.Pop}, " +
                      $"popRatio vs w0=" +
                      $"{(baseline.HasValue && baseline.Value.Pop > 0 ? Fmt((float)adopted.Pop / baseline.Value.Pop) : "?")}) " +
                      "là où l'escalier n'offrait que 0.25 ou la mort."
                    : $"Continuité adoptée c={Fmt(adopted.Continuity)} w={Fmt(adopted.Weight)} " +
                      $"sans palier intermédiaire net ; pop={adopted.Pop}. " +
                      "Résultat important si la brutalité persiste hors escalier.");
            sb.AppendLine(
                $"determinism00={(det00 ? "PASS" : "FAIL")} determinismAdopted={(detAd ? "PASS" : "FAIL")} " +
                $"conservation={(consOk ? "PASS" : "FAIL")}");

            // Persister le JSON adopté (si c>0 ou w changé — on écrit la justification).
            WriteAdoptedJson(adopted, midViable, baseline);

            File.WriteAllText(logPath, sb.ToString());
            UnityEngine.Debug.Log(
                $"V1026DemographicResponseDiagnostic: wrote {logPath} " +
                $"adopted c={Fmt(adopted.Continuity)} w={Fmt(adopted.Weight)} " +
                $"midViable={(midViable ? "Y" : "N")}");

            PopGrowthSystem.UnlockContinuity();
            PhysicalSatisfactionBlendSystem.UnlockWeight();
            PhysicalStockSystem.MultiHopTransport = true;

            Assert.IsTrue(det00, "Déterminisme c=0 w=0 échoué");
            Assert.IsTrue(detAd, "Déterminisme adopté échoué");
            Assert.IsTrue(consOk, "Conservation per-tick échouée");
        }

        // ----- types -----

        struct SatDistribution
        {
            public float Min, P10, Median, P90, Max, Std;
            public int BandPops0, BandPops1, BandPops2, BandPops3;
            public float BandPopShare0, BandPopShare1, BandPopShare2, BandPopShare3;
            public float CliffShare;
            public int TotalPops;
            public long TotalPopSize;
        }

        struct CrossRow
        {
            public float Continuity, Weight;
            public int Pop, Starved, Bankrupt, Countries, Wars, MigrateEst;
            public float SatAvg, PhysMean, Debt, Army, CpuMs, CliffShare;
            public float BandPopShare0, BandPopShare1, BandPopShare2, BandPopShare3;
            public string PickReason;
        }

        struct TrajPoint
        {
            public int Tick, Pop, Starved, Migrate;
            public float SatAvg, PhysMean, Debt, Army, CpuMs;
        }

        struct TrajectoryReport
        {
            public int ProvinceId, Pop0, Pop1, DeltaPop;
            public float PhysSat0;
            public List<TrajPoint> Points;
        }

        struct SeriesReport
        {
            public List<TrajPoint> Points;
            public float PopDeltaStd;
        }

        struct EmergentStory
        {
            public bool Found;
            public int ProvinceId, Pop0, Pop1, DeltaPop, MigrateIn;
            public float PhysSat;
            public string Note;
        }

        // ----- mesures -----

        static SatDistribution MeasureSatDistribution(EntityManager em)
        {
            var sats = new List<float>();
            var sizes = new List<int>();
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<PopData>());
            using var pops = q.ToComponentDataArray<PopData>(Allocator.Temp);
            long totalSize = 0;
            for (var i = 0; i < pops.Length; i++)
            {
                sats.Add(pops[i].NeedsSatisfaction);
                sizes.Add(pops[i].Size);
                totalSize += pops[i].Size;
            }

            if (sats.Count == 0)
            {
                return default;
            }

            var indexed = new List<(float S, int Size)>();
            for (var i = 0; i < sats.Count; i++)
            {
                indexed.Add((sats[i], sizes[i]));
            }

            indexed.Sort((a, b) => a.S.CompareTo(b.S));

            float sum = 0f, sumSq = 0f;
            long cliffSize = 0;
            long b0 = 0, b1 = 0, b2 = 0, b3 = 0;
            var bp0 = 0;
            var bp1 = 0;
            var bp2 = 0;
            var bp3 = 0;
            for (var i = 0; i < indexed.Count; i++)
            {
                var s = indexed[i].S;
                var n = indexed[i].Size;
                sum += s;
                sumSq += s * s;
                if (s < 0.2f)
                {
                    b0 += n;
                    bp0++;
                }
                else if (s < 0.5f)
                {
                    b1 += n;
                    bp1++;
                }
                else if (s < 0.8f)
                {
                    b2 += n;
                    bp2++;
                }
                else
                {
                    b3 += n;
                    bp3++;
                }

                if (NearCliff(s))
                {
                    cliffSize += n;
                }
            }

            var mean = sum / indexed.Count;
            var variance = sumSq / indexed.Count - mean * mean;
            var denom = totalSize > 0 ? (float)totalSize : 1f;

            return new SatDistribution
            {
                Min = indexed[0].S,
                P10 = Percentile(indexed, 0.10f),
                Median = Percentile(indexed, 0.50f),
                P90 = Percentile(indexed, 0.90f),
                Max = indexed[indexed.Count - 1].S,
                Std = math.sqrt(math.max(0f, variance)),
                BandPops0 = bp0,
                BandPops1 = bp1,
                BandPops2 = bp2,
                BandPops3 = bp3,
                BandPopShare0 = b0 / denom,
                BandPopShare1 = b1 / denom,
                BandPopShare2 = b2 / denom,
                BandPopShare3 = b3 / denom,
                CliffShare = cliffSize / denom,
                TotalPops = indexed.Count,
                TotalPopSize = totalSize
            };
        }

        static bool NearCliff(float s)
        {
            return math.abs(s - 0.2f) < CliffEps
                   || math.abs(s - 0.5f) < CliffEps
                   || math.abs(s - 0.8f) < CliffEps;
        }

        static float Percentile(List<(float S, int Size)> sorted, float p)
        {
            if (sorted.Count == 0)
            {
                return 0f;
            }

            var idx = (int)math.clamp(math.round(p * (sorted.Count - 1)), 0, sorted.Count - 1);
            return sorted[idx].S;
        }

        static void AppendDistribution(StringBuilder sb, SatDistribution d, string label)
        {
            sb.AppendLine(
                $"[{label}] pops={d.TotalPops} popSize={d.TotalPopSize} " +
                $"min={Fmt(d.Min)} p10={Fmt(d.P10)} med={Fmt(d.Median)} p90={Fmt(d.P90)} " +
                $"max={Fmt(d.Max)} std={Fmt(d.Std)}");
            sb.AppendLine(
                $"bandes [0,0.2[ pops={d.BandPops0} share={Fmt(d.BandPopShare0)} ; " +
                $"[0.2,0.5[ pops={d.BandPops1} share={Fmt(d.BandPopShare1)} ; " +
                $"[0.5,0.8[ pops={d.BandPops2} share={Fmt(d.BandPopShare2)} ; " +
                $"[0.8,1] pops={d.BandPops3} share={Fmt(d.BandPopShare3)}");
            sb.AppendLine(
                $"EXPOSITION FALAISE (±{Fmt(CliffEps)} d'un seuil): share={Fmt(d.CliffShare)} " +
                "de la population mondiale");
        }

        static void ForceGc()
        {
            PopGrowthSystem.UnlockContinuity();
            PopGrowthSystem.ResetToCompiledDefault();
            PhysicalSatisfactionBlendSystem.UnlockWeight();
            PhysicalSatisfactionBlendSystem.ResetToCompiledDefault();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        static bool TryRunCrossPoint(
            float continuity, float weight, out CrossRow row, out string error)
        {
            row = default;
            error = null;
            try
            {
                row = RunCrossPoint(continuity, weight);
                return true;
            }
            catch (Exception ex) when (HarnessAllocationGuard.IsNativeAllocationFailure(ex))
            {
                error = ex.Message;
                ForceGc();
                return false;
            }
        }

        static bool TryMeasureZeroPhysTrajectory(
            float continuity, float weight, int ticks, int step,
            out TrajectoryReport report, out string error)
        {
            report = default;
            error = null;
            try
            {
                report = MeasureZeroPhysTrajectory(continuity, weight, ticks, step);
                return true;
            }
            catch (Exception ex) when (HarnessAllocationGuard.IsNativeAllocationFailure(ex))
            {
                error = ex.Message;
                ForceGc();
                return false;
            }
        }

        static bool TryMeasureTimeSeries(
            float continuity, float weight, int ticks, int step,
            out SeriesReport report, out string error)
        {
            report = default;
            error = null;
            try
            {
                report = MeasureTimeSeries(continuity, weight, ticks, step);
                return true;
            }
            catch (Exception ex) when (HarnessAllocationGuard.IsNativeAllocationFailure(ex))
            {
                error = ex.Message;
                ForceGc();
                return false;
            }
        }

        static CrossRow RunCrossPoint(float continuity, float weight)
        {
            PopGrowthSystem.LockContinuity(continuity);
            PhysicalSatisfactionBlendSystem.LockWeight(weight);
            PhysicalStockSystem.MultiHopTransport = true;
            PhysicalStockSystem.IdealPoolMode = false;

            using var harness = new SimulationHarness(Seed);
            harness.RunTicks(0);
            SetTransportInfra(harness.EntityManager, PerDev);

            Dictionary<int, int> prevPop = null;
            EstimateMigrations(harness.EntityManager, ref prevPop);
            harness.RunTicks(3000);
            var migrate = EstimateMigrations(harness.EntityManager, ref prevPop);

            var m = WorldMetrics.Capture(harness.EntityManager, 3000);
            var gap = ComputeGap(harness.EntityManager);
            var dist = MeasureSatDistribution(harness.EntityManager);
            var cpu = (float)(GetMetrics(harness.EntityManager).LastTickCpuMs
                              + PhysicalSatisfactionBlendSystem.LastTickCpuMs);

            PopGrowthSystem.UnlockContinuity();
            PhysicalSatisfactionBlendSystem.UnlockWeight();

            return new CrossRow
            {
                Continuity = continuity,
                Weight = weight,
                Pop = m.Population,
                SatAvg = m.NeedsSatAvg,
                PhysMean = gap.PhysMean,
                Starved = gap.Starved,
                Debt = m.TotalDebt,
                Bankrupt = m.BankruptCount,
                Army = m.WorldArmyStr,
                Countries = m.CountriesWithLand,
                Wars = m.ActiveWars,
                MigrateEst = migrate,
                CpuMs = cpu,
                CliffShare = dist.CliffShare,
                BandPopShare0 = dist.BandPopShare0,
                BandPopShare1 = dist.BandPopShare1,
                BandPopShare2 = dist.BandPopShare2,
                BandPopShare3 = dist.BandPopShare3,
                PickReason = ""
            };
        }

        static bool IsAlive(CrossRow row, CrossRow? baseline)
        {
            if (!baseline.HasValue)
            {
                return true;
            }

            var b = baseline.Value;
            if (b.Pop <= 0)
            {
                return row.Pop > 0;
            }

            return row.Pop >= b.Pop * 0.80f;
        }

        static CrossRow PickAdopted(List<CrossRow> grid, CrossRow? baseline)
        {
            // Critère: vivant (pop≥80% baseline), maximiser sensibilité utile =
            // plus grand poids avec pop viable, en préférant c>0 s'il élargit le palier.
            CrossRow best = default;
            var found = false;
            var bestScore = float.NegativeInfinity;

            foreach (var row in grid)
            {
                if (!IsAlive(row, baseline))
                {
                    continue;
                }

                // Score: poids (sensibilité) + 0.15*continuité (préfère continuité si égalité)
                // pénalité légère sur starved et dette.
                var score = row.Weight * 10f + row.Continuity * 1.5f
                            - row.Starved * 0.02f - row.Debt * 0.0001f;
                if (baseline.HasValue && baseline.Value.Pop > 0)
                {
                    score += 3f * ((float)row.Pop / baseline.Value.Pop);
                }

                if (!found || score > bestScore)
                {
                    found = true;
                    bestScore = score;
                    best = row;
                    best.PickReason =
                        $"max score={Fmt(score)} (vivant, poids↑, continuité↑)";
                }
            }

            if (!found && baseline.HasValue)
            {
                best = baseline.Value;
                best.PickReason = "fallback baseline c=0 w=0 (aucun point vivant)";
            }

            return best;
        }

        static CrossRow FindRow(List<CrossRow> grid, float c, float w)
        {
            foreach (var r in grid)
            {
                if (math.abs(r.Continuity - c) < 1e-4f && math.abs(r.Weight - w) < 1e-4f)
                {
                    return r;
                }
            }

            return default;
        }

        static bool HasIntermediatePlateau(List<CrossRow> grid, CrossRow? baseline)
        {
            // Un palier intermédiaire = à c>0, un poids ∈ (0.25, 0.5) vivant
            // alors qu'à c=0 le même poids est mort OU nettement pire.
            if (!baseline.HasValue)
            {
                return false;
            }

            var stair05 = FindRow(grid, 0f, 0.5f);
            var stair025Alive = IsAlive(FindRow(grid, 0f, 0.25f), baseline);
            var stair05Alive = IsAlive(stair05, baseline);

            foreach (var c in new[] { 0.5f, 1f })
            {
                foreach (var w in new[] { 0.4f, 0.5f })
                {
                    var row = FindRow(grid, c, w);
                    if (!IsAlive(row, baseline))
                    {
                        continue;
                    }

                    // Viable à un poids où l'escalier meurt, ou nettement plus peuplé à w=0.5.
                    if (w >= 0.4f && !stair05Alive)
                    {
                        return true;
                    }

                    if (w >= 0.5f && stair05.Pop > 0 && row.Pop > stair05.Pop * 1.15f)
                    {
                        return true;
                    }

                    if (w > 0.25f && w < 0.5f && stair025Alive && !stair05Alive)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        static TrajectoryReport MeasureZeroPhysTrajectory(
            float continuity, float weight, int ticks, int step)
        {
            PopGrowthSystem.LockContinuity(continuity);
            PhysicalSatisfactionBlendSystem.LockWeight(weight);
            PhysicalStockSystem.MultiHopTransport = true;

            using var h = new SimulationHarness(Seed);
            h.RunTicks(0);
            SetTransportInfra(h.EntityManager, PerDev);

            // Warmup pour stabiliser la sat physique, puis verrouiller la pire province.
            h.RunTicks(50);
            var target = FindLowestPhysProvince(h.EntityManager);
            var pop0 = PopInProvince(h.EntityManager, target.ProvinceId);
            var points = new List<TrajPoint>();
            Dictionary<int, int> prev = null;
            var done = 50;
            while (done <= ticks)
            {
                var m = WorldMetrics.Capture(h.EntityManager, done);
                var gap = ComputeGap(h.EntityManager);
                var migrate = EstimateMigrations(h.EntityManager, ref prev);
                var localPop = PopInProvince(h.EntityManager, target.ProvinceId);
                points.Add(new TrajPoint
                {
                    Tick = done,
                    Pop = localPop,
                    SatAvg = m.NeedsSatAvg,
                    PhysMean = gap.PhysMean,
                    Starved = gap.Starved,
                    Debt = m.TotalDebt,
                    Army = m.WorldArmyStr,
                    Migrate = migrate,
                    CpuMs = GetMetrics(h.EntityManager).LastTickCpuMs
                });
                if (done >= ticks)
                {
                    break;
                }

                var next = math.min(done + step, ticks);
                h.RunTicks(next - done);
                done = next;
            }

            var pop1 = PopInProvince(h.EntityManager, target.ProvinceId);
            PopGrowthSystem.UnlockContinuity();
            PhysicalSatisfactionBlendSystem.UnlockWeight();

            return new TrajectoryReport
            {
                ProvinceId = target.ProvinceId,
                Pop0 = pop0,
                Pop1 = pop1,
                DeltaPop = pop1 - pop0,
                PhysSat0 = target.PhysSat,
                Points = points
            };
        }

        static void AppendTrajectory(StringBuilder sb, TrajectoryReport t, string tag)
        {
            sb.AppendLine(
                $"[{tag}] province={t.ProvinceId} phys0={Fmt(t.PhysSat0)} " +
                $"pop0={t.Pop0} pop1={t.Pop1} deltaPop={t.DeltaPop}");
            sb.AppendLine("tick\tlocalPop\tsatAvg\tphysMean\tstarved\tmigrate");
            foreach (var p in t.Points)
            {
                sb.AppendLine(
                    $"{p.Tick}\t{p.Pop}\t{Fmt(p.SatAvg)}\t{Fmt(p.PhysMean)}\t{p.Starved}\t{p.Migrate}");
            }
        }

        static SeriesReport MeasureTimeSeries(
            float continuity, float weight, int ticks, int step)
        {
            PopGrowthSystem.LockContinuity(continuity);
            PhysicalSatisfactionBlendSystem.LockWeight(weight);
            PhysicalStockSystem.MultiHopTransport = true;

            using var h = new SimulationHarness(Seed);
            h.RunTicks(0);
            SetTransportInfra(h.EntityManager, PerDev);

            var points = new List<TrajPoint>();
            Dictionary<int, int> prev = null;
            var popByProv0 = new Dictionary<int, int>();
            CapturePopByProvince(h.EntityManager, popByProv0);

            var done = 0;
            while (done <= ticks)
            {
                var m = WorldMetrics.Capture(h.EntityManager, done);
                var gap = ComputeGap(h.EntityManager);
                var migrate = EstimateMigrations(h.EntityManager, ref prev);
                points.Add(new TrajPoint
                {
                    Tick = done,
                    Pop = m.Population,
                    SatAvg = m.NeedsSatAvg,
                    PhysMean = gap.PhysMean,
                    Starved = gap.Starved,
                    Debt = m.TotalDebt,
                    Army = m.WorldArmyStr,
                    Migrate = migrate,
                    CpuMs = GetMetrics(h.EntityManager).LastTickCpuMs
                });
                if (done >= ticks)
                {
                    break;
                }

                var next = math.min(done + step, ticks);
                h.RunTicks(next - done);
                done = next;
            }

            var popByProv1 = new Dictionary<int, int>();
            CapturePopByProvince(h.EntityManager, popByProv1);
            var deltas = new List<float>();
            foreach (var kv in popByProv0)
            {
                popByProv1.TryGetValue(kv.Key, out var p1);
                deltas.Add(p1 - kv.Value);
            }

            foreach (var kv in popByProv1)
            {
                if (!popByProv0.ContainsKey(kv.Key))
                {
                    deltas.Add(kv.Value);
                }
            }

            float mean = 0f;
            foreach (var d in deltas)
            {
                mean += d;
            }

            mean = deltas.Count > 0 ? mean / deltas.Count : 0f;
            float var = 0f;
            foreach (var d in deltas)
            {
                var += (d - mean) * (d - mean);
            }

            var std = deltas.Count > 0 ? math.sqrt(var / deltas.Count) : 0f;

            PopGrowthSystem.UnlockContinuity();
            PhysicalSatisfactionBlendSystem.UnlockWeight();

            return new SeriesReport { Points = points, PopDeltaStd = std };
        }

        static EmergentStory FindEmergentStory(float continuity, float weight)
        {
            PopGrowthSystem.LockContinuity(continuity);
            PhysicalSatisfactionBlendSystem.LockWeight(weight);
            PhysicalStockSystem.MultiHopTransport = true;

            using var h = new SimulationHarness(Seed);
            h.RunTicks(0);
            SetTransportInfra(h.EntityManager, PerDev);

            var pop0 = new Dictionary<int, int>();
            CapturePopByProvince(h.EntityManager, pop0);
            h.RunTicks(1500);

            EmergentStory best = default;
            using var q = h.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<ProvinceData>(),
                ComponentType.ReadOnly<PhysicalDemandSnapshot>());
            using var entities = q.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                var e = entities[i];
                var pid = h.EntityManager.GetComponentData<ProvinceData>(e).ProvinceId;
                var snap = h.EntityManager.GetComponentData<PhysicalDemandSnapshot>(e);
                pop0.TryGetValue(pid, out var p0);
                var p1 = PopInProvince(h.EntityManager, pid);
                var delta = p1 - p0;
                var score = (1f - snap.PhysicalSatisfaction) * math.max(0, -delta);
                if (!best.Found || score > (1f - best.PhysSat) * math.max(0, -best.DeltaPop))
                {
                    best = new EmergentStory
                    {
                        Found = score > 50f,
                        ProvinceId = pid,
                        PhysSat = snap.PhysicalSatisfaction,
                        Pop0 = p0,
                        Pop1 = p1,
                        DeltaPop = delta,
                        MigrateIn = 0,
                        Note = "famine locale → déclin pop (migration émergente via systèmes existants)"
                    };
                }
            }

            PopGrowthSystem.UnlockContinuity();
            PhysicalSatisfactionBlendSystem.UnlockWeight();
            return best;
        }

        static (int ProvinceId, float PhysSat) FindLowestPhysProvince(EntityManager em)
        {
            var bestId = -1;
            var bestPhys = float.MaxValue;
            using var q = em.CreateEntityQuery(
                ComponentType.ReadOnly<ProvinceData>(),
                ComponentType.ReadOnly<PhysicalDemandSnapshot>());
            using var entities = q.ToEntityArray(Allocator.Temp);
            var rows = new List<(int Id, float Phys)>();
            for (var i = 0; i < entities.Length; i++)
            {
                var pid = em.GetComponentData<ProvinceData>(entities[i]).ProvinceId;
                var phys = em.GetComponentData<PhysicalDemandSnapshot>(entities[i]).PhysicalSatisfaction;
                rows.Add((pid, phys));
            }

            rows.Sort((a, b) =>
            {
                var c = a.Phys.CompareTo(b.Phys);
                return c != 0 ? c : a.Id.CompareTo(b.Id);
            });

            if (rows.Count > 0)
            {
                bestId = rows[0].Id;
                bestPhys = rows[0].Phys;
            }

            return (bestId, bestPhys);
        }

        static (float PhysMean, int Starved) ComputeGap(EntityManager em)
        {
            var sum = 0f;
            var n = 0;
            var starved = 0;
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<PhysicalDemandSnapshot>());
            using var snaps = q.ToComponentDataArray<PhysicalDemandSnapshot>(Allocator.Temp);
            for (var i = 0; i < snaps.Length; i++)
            {
                sum += snaps[i].PhysicalSatisfaction;
                n++;
                if (snaps[i].FoodDemand > 1e-3f &&
                    snaps[i].FoodSatisfied / snaps[i].FoodDemand < 0.2f)
                {
                    starved++;
                }
            }

            return (n > 0 ? sum / n : 0f, starved);
        }

        static int EstimateMigrations(EntityManager em, ref Dictionary<int, int> prev)
        {
            var cur = new Dictionary<int, int>();
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<PopData>());
            using var pops = q.ToComponentDataArray<PopData>(Allocator.Temp);
            for (var i = 0; i < pops.Length; i++)
            {
                var e = pops[i].Province;
                if (e == Entity.Null || !em.HasComponent<ProvinceData>(e))
                {
                    continue;
                }

                var pid = em.GetComponentData<ProvinceData>(e).ProvinceId;
                cur[pid] = cur.TryGetValue(pid, out var c) ? c + pops[i].Size : pops[i].Size;
            }

            var moved = 0;
            if (prev != null)
            {
                foreach (var kv in cur)
                {
                    prev.TryGetValue(kv.Key, out var old);
                    var delta = kv.Value - old;
                    if (delta > 0)
                    {
                        moved += delta;
                    }
                }
            }

            prev = cur;
            return moved;
        }

        static void CapturePopByProvince(EntityManager em, Dictionary<int, int> map)
        {
            map.Clear();
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<PopData>());
            using var pops = q.ToComponentDataArray<PopData>(Allocator.Temp);
            for (var i = 0; i < pops.Length; i++)
            {
                if (pops[i].Province == Entity.Null ||
                    !em.HasComponent<ProvinceData>(pops[i].Province))
                {
                    continue;
                }

                var pid = em.GetComponentData<ProvinceData>(pops[i].Province).ProvinceId;
                map[pid] = map.TryGetValue(pid, out var cur) ? cur + pops[i].Size : pops[i].Size;
            }
        }

        static int PopInProvince(EntityManager em, int provinceId)
        {
            var sum = 0;
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<PopData>());
            using var pops = q.ToComponentDataArray<PopData>(Allocator.Temp);
            for (var i = 0; i < pops.Length; i++)
            {
                if (pops[i].Province == Entity.Null ||
                    !em.HasComponent<ProvinceData>(pops[i].Province))
                {
                    continue;
                }

                if (em.GetComponentData<ProvinceData>(pops[i].Province).ProvinceId == provinceId)
                {
                    sum += pops[i].Size;
                }
            }

            return sum;
        }

        static void WriteAdoptedJson(CrossRow adopted, bool midViable, CrossRow? baseline)
        {
            var path = Path.Combine(
                UnityEngine.Application.streamingAssetsPath,
                "data",
                "demographic_response_continuity.json");
            var popRatio = baseline.HasValue && baseline.Value.Pop > 0
                ? (float)adopted.Pop / baseline.Value.Pop
                : 1f;
            var justification =
                $"v1_026 balayage c×w t3000 seed=42195: adopté c={Fmt(adopted.Continuity)} " +
                $"w={Fmt(adopted.Weight)} pop={adopted.Pop} (ratio={Fmt(popRatio)}) " +
                $"sat={Fmt(adopted.SatAvg)} starved={adopted.Starved} " +
                $"palier_intermediaire={(midViable ? "OUI" : "NON")}. " +
                adopted.PickReason;
            var json =
                "{\n" +
                $"  \"response_continuity\": {Fmt(adopted.Continuity)},\n" +
                $"  \"continuity_justification\": \"{EscapeJson(justification)}\"\n" +
                "}\n";
            File.WriteAllText(path, json);

            // Align static for remainder of process.
            PopGrowthSystem.LockContinuity(adopted.Continuity);
            PopGrowthSystem.UnlockContinuity();
            // After unlock, JSON is re-read — good.
        }

        static string EscapeJson(string s) =>
            s.Replace("\\", "\\\\").Replace("\"", "\\\"");

        static ulong WorldDigest(EntityManager em)
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

        static void SetTransportInfra(EntityManager em, float capacityPerDev)
        {
            if (!TryGetSingletonEntity<PhysicalEconomySingleton>(em, out var e))
            {
                return;
            }

            var cfg = em.GetComponentData<PhysicalTransportConfig>(e);
            cfg.CapacityPerDevPoint = capacityPerDev;
            cfg.EdgeCapacityPerTick = 500f;
            cfg.TransitTicksPerEdge = 1;
            em.SetComponentData(e, cfg);
        }

        static PhysicalEconomyMetrics GetMetrics(EntityManager em)
        {
            if (TryGetSingletonEntity<PhysicalEconomySingleton>(em, out var e))
            {
                return em.GetComponentData<PhysicalEconomyMetrics>(e);
            }

            return default;
        }

        static bool TryGetSingletonEntity<T>(EntityManager em, out Entity entity)
            where T : unmanaged, IComponentData
        {
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<T>());
            if (q.IsEmptyIgnoreFilter)
            {
                entity = Entity.Null;
                return false;
            }

            entity = q.GetSingletonEntity();
            return true;
        }

        static string Fmt(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
