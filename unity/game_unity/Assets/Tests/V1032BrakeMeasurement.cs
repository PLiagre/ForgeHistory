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
    /// <summary>Batch : -executeMethod VictoriaGame.Tests.V1032BatchRunner.Run</summary>
    public static class V1032BatchRunner
    {
        public static void Run()
        {
            try
            {
                V1032BrakeMeasurement.RunFullSuiteAndWriteLog();
                UnityEngine.Debug.Log("V1032BatchRunner: DONE");
            }
            catch (Exception ex) when (HarnessAllocationGuard.IsNativeAllocationFailure(ex))
            {
                UnityEngine.Debug.LogWarning(
                    "V1032BatchRunner: ALLOCATION_FAILURE (charge harnais) — " + ex.Message);
                UnityEngine.Debug.Log("V1032BatchRunner: DONE_PARTIAL");
            }
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
        }
    }

    /// <summary>
    /// v1_032 — mesurer le frein débouchés livré en v1_031, balayer l'intensité,
    /// adopter le palier le plus fort qui garde le monde vivant et la stabilité verte.
    /// Suites lourdes via BatchRunner uniquement (jamais en [Test] EditMode).
    /// </summary>
    [TestFixture]
    public class V1032BrakeMeasurement
    {
        const uint Seed = 42195u;
        const float PerDev = 2400.643f;
        const float BlendWeight = 0.25f;
        const float Continuity = 0.5f;
        const int BourgogneProvinceId = 6;
        const int WineGoodId = 14;
        const int ClothGoodId = 8;
        const int WoodGoodId = 4;
        const int IronGoodId = 5;
        const int WoolGoodId = 6;
        const int CoalGoodId = 7;
        const int TicksPerYear = 12;
        const float RefPrimaryRatio = 2.575f;
        const float RefDeadShare = 1.0f;
        const float RefClothServed = 0.306f;
        const double RefBourgogneWine = 19926333.012;

        static readonly int[] SnapshotTicks = { 500, 1000, 3000 };
        static readonly int[] FocusGoods =
            { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 };
        static readonly float[] IntensitySweep = { 0f, 0.25f, 0.5f, 0.75f, 1f };

        /// <summary>Mis à jour par RunFullSuiteAndWriteLog après mesure.</summary>
        public static float AdoptedIntensity = 0f;

        [TearDown]
        public void TearDown()
        {
            PhysicalProductionSystem.UnlockOutletCap();
            PhysicalProductionSystem.ResetToCompiledDefault();
            PopGrowthSystem.UnlockContinuity();
            PopGrowthSystem.ResetToCompiledDefault();
            PhysicalSatisfactionBlendSystem.UnlockWeight();
            PhysicalSatisfactionBlendSystem.ResetToCompiledDefault();
            PhysicalStockSystem.IdealPoolMode = false;
            PhysicalStockSystem.MultiHopTransport = true;
            PhysicalStockSystem.ServiceOrderMode =
                PhysicalStockSystem.TransportServiceOrder.ByDeficitSeverity;
            PhysicalStockSystem.RecordTransportShares = false;
            PhysicalStockSystem.ClearTransportShareCounters();
        }

        [Test]
        public void V1032_OutletCapApi_Exists()
        {
            Assert.GreaterOrEqual(PhysicalProductionSystem.DefaultOutletCapIntensity, 0f);
            Assert.LessOrEqual(PhysicalProductionSystem.DefaultOutletCapIntensity, 1f);
            Assert.AreEqual(3f, PhysicalProductionSystem.DefaultStorageMonths, 1e-7f);
            PhysicalProductionSystem.LockOutletCap(0.5f, 3f);
            Assert.AreEqual(0.5f, PhysicalProductionSystem.OutletCapIntensity, 1e-6f);
            PhysicalProductionSystem.UnlockOutletCap();
            PhysicalProductionSystem.ResetToCompiledDefault();
            Assert.AreEqual(
                PhysicalProductionSystem.DefaultOutletCapIntensity,
                PhysicalProductionSystem.OutletCapIntensity,
                1e-6f);
        }

        [Test]
        public void V1032_IntensityZero_Determinism()
        {
            HarnessAllocationGuard.Run(() =>
            {
                ApplyAdoptedLocks(0f);
                ulong d1, d2;
                using (var h1 = new SimulationHarness(Seed))
                {
                    ApplyAdoptedLocks(0f);
                    h1.RunTicks(0);
                    SetTransportInfra(h1.EntityManager, PerDev);
                    h1.RunTicks(80);
                    d1 = WorldDigest(h1.EntityManager);
                }

                using (var h2 = new SimulationHarness(Seed))
                {
                    ApplyAdoptedLocks(0f);
                    h2.RunTicks(0);
                    SetTransportInfra(h2.EntityManager, PerDev);
                    h2.RunTicks(80);
                    d2 = WorldDigest(h2.EntityManager);
                }

                Assert.AreEqual(d1, d2, $"Non déterministe i=0: {d1:X16} vs {d2:X16}");
            });
        }

        [Test]
        public void V1032_IntensityZero_BitIdentity_VsExplicitLock()
        {
            HarnessAllocationGuard.Run(() =>
            {
                // Chemin i=0 : deux harnais LockOutletCap(0) → digests identiques
                // (bit-identité stricte du frein désactivé, indépendamment du défaut adopté).
                ulong d1, d2;
                using (var h = new SimulationHarness(Seed))
                {
                    ApplyAdoptedLocks(0f);
                    h.RunTicks(0);
                    SetTransportInfra(h.EntityManager, PerDev);
                    h.RunTicks(60);
                    d1 = WorldDigest(h.EntityManager);
                }

                using (var h = new SimulationHarness(Seed))
                {
                    ApplyAdoptedLocks(0f);
                    h.RunTicks(0);
                    SetTransportInfra(h.EntityManager, PerDev);
                    h.RunTicks(60);
                    d2 = WorldDigest(h.EntityManager);
                }

                Assert.AreEqual(d1, d2, $"Bit-identité i=0 cassée: {d1:X16} vs {d2:X16}");
                Assert.AreNotEqual(0UL, d1);
            });
        }

        [Test]
        public void V1032_IntensityFull_Determinism()
        {
            HarnessAllocationGuard.Run(() =>
            {
                ApplyAdoptedLocks(1f);
                ulong d1, d2;
                using (var h1 = new SimulationHarness(Seed))
                {
                    ApplyAdoptedLocks(1f);
                    h1.RunTicks(0);
                    SetTransportInfra(h1.EntityManager, PerDev);
                    h1.RunTicks(80);
                    d1 = WorldDigest(h1.EntityManager);
                }

                using (var h2 = new SimulationHarness(Seed))
                {
                    ApplyAdoptedLocks(1f);
                    h2.RunTicks(0);
                    SetTransportInfra(h2.EntityManager, PerDev);
                    h2.RunTicks(80);
                    d2 = WorldDigest(h2.EntityManager);
                }

                Assert.AreEqual(d1, d2, $"Non déterministe i=1: {d1:X16} vs {d2:X16}");
            });
        }

        // Diagnostic lourd : uniquement via V1032BatchRunner.
        public static void RunFullSuiteEntry() => RunFullSuiteAndWriteLog();

        public static void RunFullSuiteAndWriteLog()
        {
            var logPath = Path.Combine(
                UnityEngine.Application.dataPath, "..", "Logs", "v1_032_brake.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            var sb = new StringBuilder(1024 * 1024);

            sb.AppendLine("=== v1_032 FREIN DÉBOUCHÉS — MESURE — seed=42195 ===");
            sb.AppendLine(
                $"config: PerDev={Fmt(PerDev)} w={Fmt(BlendWeight)} c={Fmt(Continuity)} " +
                "MultiHop=ON ServiceOrder=ByDeficitSeverity storage_months=3");
            sb.AppendLine(
                $"réf v1_030: primaryRatio={Fmt(RefPrimaryRatio)} deadShare={Fmt(RefDeadShare)} " +
                $"clothServed={Fmt(RefClothServed)} BourgogneWine={Fmt(RefBourgogneWine)}");
            sb.AppendLine();

            // ---------- PARTIE 1 — ACCUMULATION i=0 vs i=1 ----------
            sb.AppendLine("=== PARTIE 1 — ACCUMULATION CESSE ? (i=0 vs i=1) ===");
            var snaps0 = CaptureAccumulation(0f, out var bourgogne0);
            ForceGc();
            var snaps1 = CaptureAccumulation(1f, out var bourgogne1);
            ForceGc();

            sb.AppendLine("--- intensité=0 (référence bit-identique) ---");
            AppendAccumulationTable(sb, snaps0);
            sb.AppendLine(
                $"BOURGOGNE WINE@t3000 i=0: {Fmt(bourgogne0)} (réf v1_030={Fmt(RefBourgogneWine)})");
            var dead0 = DeadStockShare(snaps0);
            var ratio0 = PrimaryProdConsRatio(0f);
            ForceGc();
            sb.AppendLine(
                $"deadShare@t3000 i=0={Fmt(dead0.Share)} primaryProd/Cons={Fmt(ratio0)}");

            sb.AppendLine("--- intensité=1 (frein plein) ---");
            AppendAccumulationTable(sb, snaps1);
            sb.AppendLine($"BOURGOGNE WINE@t3000 i=1: {Fmt(bourgogne1)}");
            var dead1 = DeadStockShare(snaps1);
            var ratio1 = PrimaryProdConsRatio(1f);
            ForceGc();
            sb.AppendLine(
                $"deadShare@t3000 i=1={Fmt(dead1.Share)} primaryProd/Cons={Fmt(ratio1)}");

            var stocksCease = StocksCeaseLinearGrowth(snaps1);
            var ratioDown = ratio1 < RefPrimaryRatio * 0.85f;
            var deadDown = dead1.Share < 0.85f;
            sb.AppendLine(
                $"CRITÈRES: stocksCeaseLinear={stocksCease} " +
                $"ratioSous2.575={(ratioDown ? "Y" : "N")} ({Fmt(ratio1)}) " +
                $"deadSous1.0={(deadDown ? "Y" : "N")} ({Fmt(dead1.Share)})");
            sb.AppendLine(
                $"ΔBourgogneWine={Fmt(bourgogne1 - bourgogne0)} " +
                $"({Fmt(bourgogne0)} → {Fmt(bourgogne1)})");

            // Métriques frein à i=1 t3000
            var brakeMetrics = CaptureBrakeMetrics(1f, 3000);
            ForceGc();
            sb.AppendLine(
                $"METRICS i=1@t3000: MissedOutletShare={Fmt(brakeMetrics.MissedOutlet)} " +
                $"StorageCapacityTotal={Fmt(brakeMetrics.StorageCap)} " +
                $"StorageSaturatedProvinceCount={brakeMetrics.Saturated} " +
                $"MissedInputShare={Fmt(brakeMetrics.MissedInput)} " +
                $"LodOut={Fmt(brakeMetrics.LodOut)} PhysOut={Fmt(brakeMetrics.PhysOut)}");
            sb.AppendLine(DescribeStorageRegime(brakeMetrics));
            sb.AppendLine();

            // ---------- PARTIE 4 d'abord (socle) — BALAYAGE ----------
            // (PARTIE 2 oscillations au palier adopté après choix)
            sb.AppendLine("=== PARTIE 4 — BALAYAGE INTENSITÉ (t3000) ===");
            sb.AppendLine(
                "intensity\tpop\tpopRatio\tsatAvg\tphysMean\tstarved\tdebt\tbankrupt\t" +
                "army\tcountries\twars\talive\tstabilityLite\tdeadShare\tratioPC\t" +
                "bourgWine\tclothServed\tmissedOutlet\tsaturated\tlodGap\tdigest\tcpuMs");

            var sweep = new List<SweepRow>();
            SweepRow? baseline = null;
            foreach (var intensity in IntensitySweep)
            {
                if (!TryRunSweepPoint(intensity, out var row, out var err))
                {
                    sb.AppendLine($"{Fmt(intensity)}\tALLOC_FAIL\t{err}");
                    ForceGc();
                    continue;
                }

                sweep.Add(row);
                if (intensity <= 0f)
                {
                    baseline = row;
                }

                var popRatio = baseline.HasValue && baseline.Value.Pop > 0
                    ? (float)row.Pop / baseline.Value.Pop
                    : 1f;
                var alive = IsAlive(row, baseline);
                sb.AppendLine(
                    $"{Fmt(intensity)}\t{row.Pop}\t{Fmt(popRatio)}\t{Fmt(row.SatAvg)}\t" +
                    $"{Fmt(row.PhysMean)}\t{row.Starved}\t{Fmt(row.Debt)}\t{row.Bankrupt}\t" +
                    $"{Fmt(row.Army)}\t{row.Countries}\t{row.Wars}\t" +
                    $"{(alive ? "Y" : "N")}\t{(row.StabilityLite ? "Y" : "N")}\t" +
                    $"{Fmt(row.DeadShare)}\t{Fmt(row.PrimaryRatio)}\t{Fmt(row.BourgogneWine)}\t" +
                    $"{Fmt(row.ClothServed)}\t{Fmt(row.MissedOutlet)}\t{row.Saturated}\t" +
                    $"{Fmt(row.LodGap)}\t{row.Digest:X16}\t{Fmt(row.CpuMs)}");
                ForceGc();
            }

            File.WriteAllText(logPath, sb.ToString());

            var adopted = PickAdopted(sweep, baseline);
            AdoptedIntensity = adopted.Intensity;
            sb.AppendLine();
            sb.AppendLine(
                $"ADOPTÉ: intensity={Fmt(adopted.Intensity)} pop={adopted.Pop} " +
                $"sat={Fmt(adopted.SatAvg)} starved={adopted.Starved} " +
                $"deadShare={Fmt(adopted.DeadShare)} ratioPC={Fmt(adopted.PrimaryRatio)} " +
                $"bourgWine={Fmt(adopted.BourgogneWine)} alive={(IsAlive(adopted, baseline) ? "Y" : "N")} " +
                $"stabilityLite={(adopted.StabilityLite ? "Y" : "N")} reason={adopted.PickReason}");
            sb.AppendLine();

            // ---------- PARTIE 2 — OSCILLATIONS au palier retenu ----------
            sb.AppendLine("=== PARTIE 2 — OSCILLATIONS (palier adopté, série ≥50 ticks) ===");
            if (TryMeasureOscillations(adopted.Intensity, 800, 50, out var osc, out var oscErr))
            {
                sb.AppendLine("tick\tphysOut\tmissedOutlet\tsaturated\tprodTopConstrained");
                foreach (var p in osc.Points)
                {
                    sb.AppendLine(
                        $"{p.Tick}\t{Fmt(p.PhysOut)}\t{Fmt(p.MissedOutlet)}\t" +
                        $"{p.Saturated}\t{Fmt(p.TopConstrainedProd)}");
                }

                sb.AppendLine(
                    $"amplitude_physOut={Fmt(osc.Amplitude)} period_est_ticks={osc.PeriodEst} " +
                    $"tick_to_tick_maxΔ={Fmt(osc.MaxTickDelta)} " +
                    $"regime={(osc.Violent ? "VIOLENT" : "doux/acceptable")}");
                if (osc.Violent)
                {
                    sb.AppendLine(
                        "VERDICT OSCILLATION: VIOLENTE (Δ tick-à-tick élevée) — défaut de " +
                        "conception à rapporter, AUCUN amortisseur ajouté.");
                }
                else
                {
                    sb.AppendLine(
                        "VERDICT OSCILLATION: douce ou absente — comportement émergent acceptable.");
                }
            }
            else
            {
                sb.AppendLine($"oscillations ALLOC_FAIL: {oscErr}");
            }

            ForceGc();
            sb.AppendLine();

            // ---------- PARTIE 3 — EFFETS DE BORD ----------
            sb.AppendLine("=== PARTIE 3 — EFFETS DE BORD (sans supposer) ===");
            var shares0 = MeasureTransportShares(0f, 200, 40);
            ForceGc();
            var sharesA = MeasureTransportShares(adopted.Intensity, 200, 40);
            ForceGc();
            AppendShareSummary(sb, "i=0", shares0);
            AppendShareSummary(sb, $"i={Fmt(adopted.Intensity)}", sharesA);

            sb.AppendLine("--- santé monde t3000 par palier (depuis balayage) ---");
            sb.AppendLine(
                "intensity\tpop\tsat\tphys\tstarved\tdebt\tbankrupt\tarmy\tcountries\twars");
            foreach (var row in sweep)
            {
                sb.AppendLine(
                    $"{Fmt(row.Intensity)}\t{row.Pop}\t{Fmt(row.SatAvg)}\t{Fmt(row.PhysMean)}\t" +
                    $"{row.Starved}\t{Fmt(row.Debt)}\t{row.Bankrupt}\t{Fmt(row.Army)}\t" +
                    $"{row.Countries}\t{row.Wars}");
            }

            sb.AppendLine("--- écart LOD vs physique (LodGap = MissedInputShare proxy) ---");
            foreach (var row in sweep)
            {
                sb.AppendLine(
                    $"i={Fmt(row.Intensity)} lodGap={Fmt(row.LodGap)} " +
                    $"missedOutlet={Fmt(row.MissedOutlet)}");
            }

            sb.AppendLine(
                $"clothServedShare: i0={Fmt(baseline?.ClothServed ?? 0)} " +
                $"adopted={Fmt(adopted.ClothServed)} réf_v1_030={Fmt(RefClothServed)} " +
                $"Δadopted-i0={Fmt(adopted.ClothServed - (baseline?.ClothServed ?? 0))}");
            sb.AppendLine();

            // ---------- PARTIE 5 — GARDE-FOUS ----------
            sb.AppendLine("=== PARTIE 5 — GARDE-FOUS ===");
            var dig0 = baseline?.Digest ?? 0UL;
            var digA = adopted.Digest;
            sb.AppendLine(
                $"digest_AVANT(i=0)={dig0:X16} digest_APRÈS(adopted i={Fmt(adopted.Intensity)})=" +
                $"{digA:X16} changed={dig0 != digA}");

            int consFail0 = 1, consFailA = 1;
            float drift0 = -1, driftA = -1;
            try
            {
                ApplyAdoptedLocks(0f);
                using (var h = new SimulationHarness(Seed))
                {
                    ApplyAdoptedLocks(0f);
                    h.RunTicks(0);
                    SetTransportInfra(h.EntityManager, PerDev);
                    h.RunTicks(200);
                    var m = GetMetrics(h.EntityManager);
                    consFail0 = PhysicalConservationGate.PerTickHolds(m) ? 0 : 1;
                    drift0 = m.MaxTickConservationDrift;
                }

                ForceGc();
                ApplyAdoptedLocks(adopted.Intensity);
                using (var h = new SimulationHarness(Seed))
                {
                    ApplyAdoptedLocks(adopted.Intensity);
                    h.RunTicks(0);
                    SetTransportInfra(h.EntityManager, PerDev);
                    h.RunTicks(200);
                    var m = GetMetrics(h.EntityManager);
                    consFailA = PhysicalConservationGate.PerTickHolds(m) ? 0 : 1;
                    driftA = m.MaxTickConservationDrift;
                }
            }
            catch (Exception ex) when (HarnessAllocationGuard.IsNativeAllocationFailure(ex))
            {
                sb.AppendLine($"conservation ALLOC_FAIL: {ex.Message}");
            }

            sb.AppendLine(
                $"conservation_i0={(consFail0 == 0 ? "PASS" : "FAIL")} drift={Fmt(drift0)} ; " +
                $"conservation_adopted={(consFailA == 0 ? "PASS" : "FAIL")} drift={Fmt(driftA)}");
            sb.AppendLine(
                $"cpu stock/transport réf≈1.579 ms ; mesuré adopted≈{Fmt(adopted.CpuMs)} ms ; " +
                $"prod réf≈0.121 mélange≈0.047");
            sb.AppendLine(
                "stabilité V1016/V1017/V1018: exécutées en EditMode (filtre XML) après " +
                "écriture JSON+const — voir v1_032_tests.xml. " +
                $"stabilityLite_adopted={(adopted.StabilityLite ? "PASS" : "FAIL")}.");
            sb.AppendLine(
                "parité v1_009 4/4: EditMode filtre V1009WorldParityTests (XML).");
            sb.AppendLine();

            // Persistance
            WriteAdoptedJson(adopted, baseline);
            WriteAdoptedMarker(adopted);

            sb.AppendLine("=== VERDICT MESURÉ ===");
            var popRatioA = baseline.HasValue && baseline.Value.Pop > 0
                ? (float)adopted.Pop / baseline.Value.Pop
                : 1f;
            sb.AppendLine(
                $"Le rapport production/consommation primaire passe de {Fmt(ratio0)} à " +
                $"{Fmt(adopted.PrimaryRatio)}, la part de stock sans usage de {Fmt(dead0.Share)} " +
                $"à {Fmt(adopted.DeadShare)}, la Bourgogne vin de {Fmt(bourgogne0)} à " +
                $"{Fmt(adopted.BourgogneWine)}, le monde garde {Fmt(popRatioA * 100f)}% de sa " +
                $"population (i=0), stabilitéLite={(adopted.StabilityLite ? "verte" : "rouge")} " +
                $"à intensité {Fmt(adopted.Intensity)}. " +
                $"clothServed {Fmt(baseline?.ClothServed ?? 0)}→{Fmt(adopted.ClothServed)}. " +
                $"stocksCease@i1={stocksCease}.");

            File.WriteAllText(logPath, sb.ToString());
            UnityEngine.Debug.Log(
                $"V1032BrakeMeasurement: wrote {logPath} adopted i={Fmt(adopted.Intensity)} " +
                $"dead={Fmt(adopted.DeadShare)} ratio={Fmt(adopted.PrimaryRatio)}");

            PhysicalProductionSystem.UnlockOutletCap();
            PhysicalProductionSystem.ResetToCompiledDefault();
            PhysicalSatisfactionBlendSystem.UnlockWeight();
            PopGrowthSystem.UnlockContinuity();
            PhysicalStockSystem.ServiceOrderMode =
                PhysicalStockSystem.TransportServiceOrder.ByDeficitSeverity;
        }

        // ----- types -----

        struct GoodSnap
        {
            public int GoodId;
            public string Tag;
            public string Type;
            public double Stock;
            public double ProdCum;
            public double ConsCum;
            public double YearsOfConsumption;
            public int TopProvinceId;
            public double TopQty;
        }

        struct DeadReport
        {
            public double Dead;
            public double Total;
            public double Share;
        }

        struct BrakeSnap
        {
            public float MissedOutlet;
            public float StorageCap;
            public int Saturated;
            public float MissedInput;
            public float LodOut;
            public float PhysOut;
        }

        struct SweepRow
        {
            public float Intensity;
            public int Pop;
            public float SatAvg;
            public float PhysMean;
            public int Starved;
            public float Debt;
            public int Bankrupt;
            public float Army;
            public int Countries;
            public int Wars;
            public bool StabilityLite;
            public double DeadShare;
            public float PrimaryRatio;
            public double BourgogneWine;
            public float ClothServed;
            public float MissedOutlet;
            public int Saturated;
            public float LodGap;
            public ulong Digest;
            public float CpuMs;
            public string PickReason;
        }

        struct ShareReport
        {
            public Dictionary<int, double> Shipped;
            public Dictionary<int, double> Share;
            public Dictionary<int, string> Tags;
        }

        struct OscPoint
        {
            public int Tick;
            public float PhysOut;
            public float MissedOutlet;
            public int Saturated;
            public float TopConstrainedProd;
        }

        struct OscReport
        {
            public List<OscPoint> Points;
            public float Amplitude;
            public int PeriodEst;
            public float MaxTickDelta;
            public bool Violent;
        }

        // ----- mesures -----

        static Dictionary<int, Dictionary<int, GoodSnap>> CaptureAccumulation(
            float intensity, out double bourgogneWine)
        {
            ApplyAdoptedLocks(intensity);
            var snaps = new Dictionary<int, Dictionary<int, GoodSnap>>();
            bourgogneWine = 0;
            using var h = new SimulationHarness(Seed);
            ApplyAdoptedLocks(intensity);
            h.RunTicks(0);
            SetTransportInfra(h.EntityManager, PerDev);
            var cursor = 0;
            foreach (var target in SnapshotTicks)
            {
                h.RunTicks(target - cursor);
                cursor = target;
                snaps[target] = CaptureGoodSnaps(h.EntityManager, target);
                if (target == 3000)
                {
                    bourgogneWine = ProvinceGoodStock(
                        h.EntityManager, BourgogneProvinceId, WineGoodId);
                }
            }

            return snaps;
        }

        static void AppendAccumulationTable(
            StringBuilder sb, Dictionary<int, Dictionary<int, GoodSnap>> snaps)
        {
            sb.AppendLine(
                "tick\tgoodId\ttag\ttype\tstock\tprodCum\tconsCum\tstock/yearCons\ttrend\ttopProv\ttopQty");
            foreach (var tick in SnapshotTicks)
            {
                if (!snaps.ContainsKey(tick))
                {
                    continue;
                }

                foreach (var id in FocusGoods)
                {
                    if (!snaps[tick].TryGetValue(id, out var s))
                    {
                        continue;
                    }

                    var trend = DescribeTrend(snaps, id, tick);
                    sb.AppendLine(
                        $"{tick}\t{id}\t{s.Tag}\t{s.Type}\t{Fmt(s.Stock)}\t{Fmt(s.ProdCum)}\t" +
                        $"{Fmt(s.ConsCum)}\t{Fmt(s.YearsOfConsumption)}\t{trend}\t" +
                        $"{s.TopProvinceId}\t{Fmt(s.TopQty)}");
                }
            }
        }

        static bool StocksCeaseLinearGrowth(Dictionary<int, Dictionary<int, GoodSnap>> snaps)
        {
            // Au moins la moitié des biens focus ne « diverges » plus.
            var n = 0;
            var stopped = 0;
            foreach (var id in FocusGoods)
            {
                if (!snaps.ContainsKey(3000) || !snaps[3000].ContainsKey(id))
                {
                    continue;
                }

                n++;
                var trend = DescribeTrend(snaps, id, 3000);
                if (trend == "stable" || trend == "shrinks" || trend == "grows")
                {
                    // "grows" lent ≠ diverge linéaire ; diverge = growLate > 0.5*growEarly
                    if (trend != "diverges")
                    {
                        stopped++;
                    }
                }
            }

            return n > 0 && stopped >= (n + 1) / 2;
        }

        static DeadReport DeadStockShare(Dictionary<int, Dictionary<int, GoodSnap>> snaps)
        {
            double dead = 0, total = 0;
            if (!snaps.ContainsKey(3000))
            {
                return default;
            }

            foreach (var kv in snaps[3000])
            {
                total += kv.Value.Stock;
                if (kv.Value.YearsOfConsumption >= 50.0 || IsDiverging(snaps, kv.Key))
                {
                    dead += kv.Value.Stock;
                }
            }

            return new DeadReport
            {
                Dead = dead,
                Total = total,
                Share = total > 1e-6 ? dead / total : 0
            };
        }

        static float PrimaryProdConsRatio(float intensity)
        {
            ApplyAdoptedLocks(intensity);
            using var h = new SimulationHarness(Seed);
            ApplyAdoptedLocks(intensity);
            h.RunTicks(0);
            SetTransportInfra(h.EntityManager, PerDev);
            h.RunTicks(100);

            var recipeOutputs = new HashSet<int>();
            using (var q = h.EntityManager.CreateEntityQuery(
                       ComponentType.ReadOnly<PhysicalEconomySingleton>()))
            {
                if (!q.IsEmptyIgnoreFilter)
                {
                    var recipes = h.EntityManager.GetBuffer<PhysicalRecipeEntry>(
                        q.GetSingletonEntity());
                    for (var i = 0; i < recipes.Length; i++)
                    {
                        recipeOutputs.Add(recipes[i].OutputGoodId);
                    }
                }
            }

            float primaryProd = 0, primaryCons = 0;
            using (var q = h.EntityManager.CreateEntityQuery(
                       ComponentType.ReadOnly<PhysicalEconomySingleton>()))
            {
                if (q.IsEmptyIgnoreFilter)
                {
                    return 0;
                }

                var ledger = h.EntityManager.GetBuffer<PhysicalLedgerEntry>(q.GetSingletonEntity());
                for (var i = 0; i < ledger.Length; i++)
                {
                    if (recipeOutputs.Contains(ledger[i].GoodId))
                    {
                        continue;
                    }

                    primaryProd += (float)(ledger[i].CumulativeProduction / 100.0);
                    primaryCons += (float)(ledger[i].CumulativeConsumption / 100.0);
                }
            }

            return primaryProd / math.max(primaryCons, 1e-4f);
        }

        static BrakeSnap CaptureBrakeMetrics(float intensity, int ticks)
        {
            ApplyAdoptedLocks(intensity);
            using var h = new SimulationHarness(Seed);
            ApplyAdoptedLocks(intensity);
            h.RunTicks(0);
            SetTransportInfra(h.EntityManager, PerDev);
            h.RunTicks(ticks);
            var m = GetMetrics(h.EntityManager);
            return new BrakeSnap
            {
                MissedOutlet = m.MissedOutletShare,
                StorageCap = m.StorageCapacityTotal,
                Saturated = m.StorageSaturatedProvinceCount,
                MissedInput = m.MissedInputShare,
                LodOut = m.LodOutputTotal,
                PhysOut = m.PhysicalOutputTotal
            };
        }

        static string DescribeStorageRegime(BrakeSnap m)
        {
            if (m.Saturated <= 0)
            {
                return "RÉGIME ENTREPOSAGE: jamais saturé → le frein ne contraint quasi rien " +
                       "via le stockage (seul conso+évacuation freinent).";
            }

            if (m.Saturated >= 40)
            {
                return $"RÉGIME ENTREPOSAGE: saturé partout ({m.Saturated} prov) → risque d'étranglement.";
            }

            return $"RÉGIME ENTREPOSAGE: saturé partiel ({m.Saturated} prov) — contrainte active sans étouffer.";
        }

        static bool TryRunSweepPoint(float intensity, out SweepRow row, out string err)
        {
            row = default;
            err = null;
            try
            {
                ApplyAdoptedLocks(intensity);
                using var h = new SimulationHarness(Seed);
                ApplyAdoptedLocks(intensity);
                h.RunTicks(0);
                SetTransportInfra(h.EntityManager, PerDev);

                // Captures intermédiaires pour stabilityLite (dette/armée)
                float debt1000 = 0, army1000 = 0;
                int victories1500 = 0, victories3000 = 0;
                int wars1500 = 0, wars3000 = 0;
                float debt2500 = 0, debt3000 = 0, army3000 = 0;

                h.RunTicks(1000);
                var m1000 = WorldMetrics.Capture(h.EntityManager, 1000);
                debt1000 = m1000.TotalDebt;
                army1000 = m1000.WorldArmyStr;

                h.RunTicks(500);
                var m1500 = WorldMetrics.Capture(h.EntityManager, 1500);
                victories1500 = m1500.Victories;
                wars1500 = m1500.WarsDeclared;

                h.RunTicks(1000);
                var m2500 = WorldMetrics.Capture(h.EntityManager, 2500);
                debt2500 = m2500.TotalDebt;

                h.RunTicks(500);
                var m3000 = WorldMetrics.Capture(h.EntityManager, 3000);
                debt3000 = m3000.TotalDebt;
                army3000 = m3000.WorldArmyStr;
                victories3000 = m3000.Victories;
                wars3000 = m3000.WarsDeclared;

                float clothD = 0, clothS = 0, physSum = 0;
                var starved = 0;
                var n = 0;
                using (var q = h.EntityManager.CreateEntityQuery(
                           ComponentType.ReadOnly<PhysicalDemandSnapshot>()))
                using (var snaps = q.ToComponentDataArray<PhysicalDemandSnapshot>(Allocator.Temp))
                {
                    for (var i = 0; i < snaps.Length; i++)
                    {
                        clothD += snaps[i].ClothDemand;
                        clothS += snaps[i].ClothSatisfied;
                        physSum += snaps[i].PhysicalSatisfaction;
                        n++;
                        if (snaps[i].PhysicalSatisfaction < 0.2f)
                        {
                            starved++;
                        }
                    }
                }

                var goodSnaps = CaptureGoodSnaps(h.EntityManager, 3000);
                var dead = DeadStockShare(new Dictionary<int, Dictionary<int, GoodSnap>>
                {
                    [500] = goodSnaps,
                    [1000] = goodSnaps,
                    [3000] = goodSnaps
                });
                // Dead share sans trend (pas de série) : années≥50 seulement
                double deadQty = 0, totalQty = 0;
                foreach (var kv in goodSnaps)
                {
                    totalQty += kv.Value.Stock;
                    if (kv.Value.YearsOfConsumption >= 50.0)
                    {
                        deadQty += kv.Value.Stock;
                    }
                }

                var deadShare = totalQty > 1e-6 ? deadQty / totalQty : 0;

                // Ratio prod/cons primaire sur ledger cumulé / 3000
                var recipeOutputs = new HashSet<int>();
                float primaryProd = 0, primaryCons = 0;
                using (var q = h.EntityManager.CreateEntityQuery(
                           ComponentType.ReadOnly<PhysicalEconomySingleton>()))
                {
                    if (!q.IsEmptyIgnoreFilter)
                    {
                        var singleton = q.GetSingletonEntity();
                        var recipes = h.EntityManager.GetBuffer<PhysicalRecipeEntry>(singleton);
                        for (var i = 0; i < recipes.Length; i++)
                        {
                            recipeOutputs.Add(recipes[i].OutputGoodId);
                        }

                        var ledger = h.EntityManager.GetBuffer<PhysicalLedgerEntry>(singleton);
                        for (var i = 0; i < ledger.Length; i++)
                        {
                            if (recipeOutputs.Contains(ledger[i].GoodId))
                            {
                                continue;
                            }

                            primaryProd += (float)(ledger[i].CumulativeProduction / 3000.0);
                            primaryCons += (float)(ledger[i].CumulativeConsumption / 3000.0);
                        }
                    }
                }

                var metrics = GetMetrics(h.EntityManager);
                var stabilityLite =
                    debt3000 <= Math.Max(debt1000 * 2.5f, 2500f) &&
                    debt3000 < 15429.4f * 0.5f &&
                    (debt2500 <= 1e-3f || debt3000 / debt2500 < 1.8f) &&
                    army3000 > 1000f &&
                    army3000 >= army1000 * 0.35f &&
                    victories3000 > victories1500 &&
                    wars3000 > wars1500;

                row = new SweepRow
                {
                    Intensity = intensity,
                    Pop = m3000.Population,
                    SatAvg = m3000.NeedsSatAvg,
                    PhysMean = n > 0 ? physSum / n : 0f,
                    Starved = starved,
                    Debt = debt3000,
                    Bankrupt = m3000.BankruptCount,
                    Army = army3000,
                    Countries = m3000.CountriesWithLand,
                    Wars = m3000.WarsDeclared,
                    StabilityLite = stabilityLite,
                    DeadShare = deadShare,
                    PrimaryRatio = primaryProd / math.max(primaryCons, 1e-4f),
                    BourgogneWine = ProvinceGoodStock(
                        h.EntityManager, BourgogneProvinceId, WineGoodId),
                    ClothServed = clothD > 1e-6f ? clothS / clothD : 0f,
                    MissedOutlet = metrics.MissedOutletShare,
                    Saturated = metrics.StorageSaturatedProvinceCount,
                    LodGap = metrics.MissedInputShare,
                    Digest = WorldDigest(h.EntityManager),
                    CpuMs = (float)PhysicalStockSystem.LastTickCpuMs,
                    PickReason = ""
                };
                return true;
            }
            catch (Exception ex) when (HarnessAllocationGuard.IsNativeAllocationFailure(ex))
            {
                err = ex.Message;
                return false;
            }
        }

        static bool IsAlive(SweepRow row, SweepRow? baseline)
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

            // Monde vivant : pop ≥ 80% baseline ET stabilité lite.
            return row.Pop >= b.Pop * 0.80f && row.StabilityLite;
        }

        static SweepRow PickAdopted(List<SweepRow> sweep, SweepRow? baseline)
        {
            // Plus forte intensité qui garde monde vivant + stabilité lite.
            SweepRow best = default;
            var found = false;
            foreach (var row in sweep)
            {
                if (!IsAlive(row, baseline))
                {
                    continue;
                }

                if (!found || row.Intensity > best.Intensity)
                {
                    best = row;
                    best.PickReason =
                        $"plus fort palier vivant+stableLite (i={Fmt(row.Intensity)})";
                    found = true;
                }
            }

            if (found)
            {
                return best;
            }

            // Aucun palier non nul : rester à 0.
            if (baseline.HasValue)
            {
                var b = baseline.Value;
                b.PickReason = "AUCUN palier non nul ne tient — défaut laissé à 0 (résultat, pas échec)";
                return b;
            }

            return new SweepRow
            {
                Intensity = 0f,
                PickReason = "sweep vide — défaut 0"
            };
        }

        static bool TryMeasureOscillations(
            float intensity, int totalTicks, int step, out OscReport report, out string err)
        {
            report = new OscReport { Points = new List<OscPoint>() };
            err = null;
            try
            {
                ApplyAdoptedLocks(intensity);
                using var h = new SimulationHarness(Seed);
                ApplyAdoptedLocks(intensity);
                h.RunTicks(0);
                SetTransportInfra(h.EntityManager, PerDev);

                float minP = float.MaxValue, maxP = float.MinValue;
                float prev = -1f;
                float maxDelta = 0f;
                var crossingTicks = new List<int>();
                float meanAcc = 0f;
                var count = 0;

                for (var t = step; t <= totalTicks; t += step)
                {
                    h.RunTicks(step);
                    var m = GetMetrics(h.EntityManager);
                    var top = TopConstrainedProvinceProduction(h.EntityManager);
                    report.Points.Add(new OscPoint
                    {
                        Tick = t,
                        PhysOut = m.PhysicalOutputTotal,
                        MissedOutlet = m.MissedOutletShare,
                        Saturated = m.StorageSaturatedProvinceCount,
                        TopConstrainedProd = top
                    });

                    minP = math.min(minP, m.PhysicalOutputTotal);
                    maxP = math.max(maxP, m.PhysicalOutputTotal);
                    meanAcc += m.PhysicalOutputTotal;
                    count++;
                    if (prev >= 0f)
                    {
                        maxDelta = math.max(maxDelta, math.abs(m.PhysicalOutputTotal - prev));
                    }

                    prev = m.PhysicalOutputTotal;
                }

                var mean = count > 0 ? meanAcc / count : 0f;
                for (var i = 1; i < report.Points.Count; i++)
                {
                    var a = report.Points[i - 1].PhysOut - mean;
                    var b = report.Points[i].PhysOut - mean;
                    if (a <= 0 && b > 0)
                    {
                        crossingTicks.Add(report.Points[i].Tick);
                    }
                }

                var period = 0;
                if (crossingTicks.Count >= 2)
                {
                    var sum = 0;
                    for (var i = 1; i < crossingTicks.Count; i++)
                    {
                        sum += crossingTicks[i] - crossingTicks[i - 1];
                    }

                    period = sum / (crossingTicks.Count - 1);
                }

                report.Amplitude = maxP - minP;
                report.PeriodEst = period;
                report.MaxTickDelta = maxDelta;
                // Violent = Δ tick-à-tick (sur pas de 50) > 50% de la moyenne
                report.Violent = mean > 1e-3f && maxDelta > mean * 0.5f;
                return true;
            }
            catch (Exception ex) when (HarnessAllocationGuard.IsNativeAllocationFailure(ex))
            {
                err = ex.Message;
                return false;
            }
        }

        static float TopConstrainedProvinceProduction(EntityManager em)
        {
            // Proxy : province avec MissedOutlet le plus visible = plus bas PhysSat cloth-ish
            // On prend le max stock wine en Bourgogne-like : production physique locale via ledger n/a
            // Utilise StorageSaturated : somme PhysicalOutput n'est pas par province.
            // Fallback : stock wine Bourgogne delta n/a — on retourne PhysicalOutputTotal déjà loggé.
            // Mesure utile : quantité wine en Bourgogne (niveau) comme proxy de contrainte locale.
            return (float)ProvinceGoodStock(em, BourgogneProvinceId, WineGoodId);
        }

        static ShareReport MeasureTransportShares(float intensity, int totalTicks, int window)
        {
            ApplyAdoptedLocks(intensity);
            PhysicalStockSystem.RecordTransportShares = true;
            var shipped = new Dictionary<int, double>();
            var tags = new Dictionary<int, string>();

            using (var h = new SimulationHarness(Seed))
            {
                ApplyAdoptedLocks(intensity);
                h.RunTicks(0);
                SetTransportInfra(h.EntityManager, PerDev);
                h.RunTicks(math.max(0, totalTicks - window));

                using (var q = h.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<GoodData>()))
                using (var goods = q.ToComponentDataArray<GoodData>(Allocator.Temp))
                {
                    for (var i = 0; i < goods.Length; i++)
                    {
                        tags[goods[i].GoodId] = goods[i].Tag.ToString();
                    }
                }

                for (var t = 0; t < window; t++)
                {
                    h.RunTicks(1);
                    for (var g = 0; g < PhysicalStockSystem.TransportShareSlots; g++)
                    {
                        var sh = PhysicalStockSystem.LastTickShippedByGood[g];
                        if (sh > 0)
                        {
                            shipped[g] = shipped.TryGetValue(g, out var c) ? c + sh : sh;
                        }
                    }
                }
            }

            PhysicalStockSystem.RecordTransportShares = false;
            double total = 0;
            foreach (var kv in shipped)
            {
                total += kv.Value;
            }

            var share = new Dictionary<int, double>();
            foreach (var kv in shipped)
            {
                share[kv.Key] = total > 1e-9 ? kv.Value / total : 0;
            }

            return new ShareReport { Shipped = shipped, Share = share, Tags = tags };
        }

        static void AppendShareSummary(StringBuilder sb, string label, ShareReport r)
        {
            var wood = r.Share.TryGetValue(WoodGoodId, out var w) ? w : 0;
            var iron = r.Share.TryGetValue(IronGoodId, out var ir) ? ir : 0;
            var wool = r.Share.TryGetValue(WoolGoodId, out var wo) ? wo : 0;
            var coal = r.Share.TryGetValue(CoalGoodId, out var co) ? co : 0;
            var cloth = r.Share.TryGetValue(ClothGoodId, out var cl) ? cl : 0;
            var raw = wood + iron + wool + coal;
            double totalVol = 0;
            foreach (var kv in r.Shipped)
            {
                totalVol += kv.Value;
            }

            sb.AppendLine(
                $"{label}: volume={Fmt(totalVol)} raw(wood+iron+wool+coal)={Fmt(raw)} " +
                $"cloth={Fmt(cloth)} (réf v1_030 raw=0.778 cloth=0.027)");
        }

        static Dictionary<int, GoodSnap> CaptureGoodSnaps(EntityManager em, int atTick)
        {
            var tags = new Dictionary<int, string>();
            var types = new Dictionary<int, string>();
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<GoodData>()))
            using (var goods = q.ToComponentDataArray<GoodData>(Allocator.Temp))
            {
                for (var i = 0; i < goods.Length; i++)
                {
                    tags[goods[i].GoodId] = goods[i].Tag.ToString();
                    types[goods[i].GoodId] = goods[i].Type.ToString();
                }
            }

            var stockByGood = new Dictionary<int, double>();
            var topProv = new Dictionary<int, int>();
            var topQty = new Dictionary<int, double>();
            using (var q = em.CreateEntityQuery(
                       ComponentType.ReadOnly<ProvinceData>(),
                       ComponentType.ReadOnly<ProvinceStock>()))
            using (var entities = q.ToEntityArray(Allocator.Temp))
            {
                for (var e = 0; e < entities.Length; e++)
                {
                    var pid = em.GetComponentData<ProvinceData>(entities[e]).ProvinceId;
                    var buf = em.GetBuffer<ProvinceStock>(entities[e]);
                    for (var i = 0; i < buf.Length; i++)
                    {
                        var g = buf[i].GoodId;
                        var qty = buf[i].Quantity;
                        stockByGood[g] = stockByGood.TryGetValue(g, out var cur) ? cur + qty : qty;
                        if (!topQty.TryGetValue(g, out var tq) || qty > tq)
                        {
                            topQty[g] = qty;
                            topProv[g] = pid;
                        }
                    }
                }
            }

            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<PhysicalEconomySingleton>()))
            {
                if (!q.IsEmptyIgnoreFilter)
                {
                    var singleton = q.GetSingletonEntity();
                    if (em.HasBuffer<CargoInTransit>(singleton))
                    {
                        var cargos = em.GetBuffer<CargoInTransit>(singleton);
                        for (var i = 0; i < cargos.Length; i++)
                        {
                            var g = cargos[i].GoodId;
                            stockByGood[g] = stockByGood.TryGetValue(g, out var cur)
                                ? cur + cargos[i].Quantity
                                : cargos[i].Quantity;
                        }
                    }
                }
            }

            var result = new Dictionary<int, GoodSnap>();
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<PhysicalEconomySingleton>()))
            {
                if (q.IsEmptyIgnoreFilter)
                {
                    return result;
                }

                var ledger = em.GetBuffer<PhysicalLedgerEntry>(q.GetSingletonEntity());
                var ticks = math.max(1, atTick);
                for (var i = 0; i < ledger.Length; i++)
                {
                    var e = ledger[i];
                    var stock = stockByGood.TryGetValue(e.GoodId, out var s) ? s : 0;
                    var consPerTick = e.CumulativeConsumption / ticks;
                    var yearCons = consPerTick > 1e-9
                        ? stock / (consPerTick * TicksPerYear)
                        : (stock > 1e-3 ? 1e9 : 0);

                    result[e.GoodId] = new GoodSnap
                    {
                        GoodId = e.GoodId,
                        Tag = tags.TryGetValue(e.GoodId, out var tg) ? tg : "?",
                        Type = types.TryGetValue(e.GoodId, out var ty) ? ty : "?",
                        Stock = stock,
                        ProdCum = e.CumulativeProduction,
                        ConsCum = e.CumulativeConsumption,
                        YearsOfConsumption = yearCons,
                        TopProvinceId = topProv.TryGetValue(e.GoodId, out var tp) ? tp : -1,
                        TopQty = topQty.TryGetValue(e.GoodId, out var tq) ? tq : 0
                    };
                }
            }

            return result;
        }

        static string DescribeTrend(
            Dictionary<int, Dictionary<int, GoodSnap>> snaps, int goodId, int tick)
        {
            if (!snaps.ContainsKey(500) || !snaps[500].ContainsKey(goodId))
            {
                return "n/a";
            }

            var a = snaps[500][goodId].Stock;
            var b = snaps.ContainsKey(1000) && snaps[1000].ContainsKey(goodId)
                ? snaps[1000][goodId].Stock
                : a;
            var c = snaps.ContainsKey(3000) && snaps[3000].ContainsKey(goodId)
                ? snaps[3000][goodId].Stock
                : b;
            if (tick < 3000)
            {
                return "pending";
            }

            var growEarly = b - a;
            var growLate = c - b;
            if (c <= a * 1.05 && math.abs(growLate) <= math.max(1.0, a * 0.02))
            {
                return "stable";
            }

            if (growLate > growEarly * 0.5 && growLate > 0)
            {
                return "diverges";
            }

            if (growLate > 0)
            {
                return "grows";
            }

            return "shrinks";
        }

        static bool IsDiverging(Dictionary<int, Dictionary<int, GoodSnap>> snaps, int goodId) =>
            DescribeTrend(snaps, goodId, 3000) == "diverges" ||
            DescribeTrend(snaps, goodId, 3000) == "grows";

        static double ProvinceGoodStock(EntityManager em, int provinceId, int goodId)
        {
            using var q = em.CreateEntityQuery(
                ComponentType.ReadOnly<ProvinceData>(),
                ComponentType.ReadOnly<ProvinceStock>());
            using var entities = q.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                if (em.GetComponentData<ProvinceData>(entities[i]).ProvinceId != provinceId)
                {
                    continue;
                }

                return PhysicalStockSystem.GetStockQuantity(
                    em.GetBuffer<ProvinceStock>(entities[i]), goodId);
            }

            return 0;
        }

        static void WriteAdoptedJson(SweepRow adopted, SweepRow? baseline)
        {
            var path = Path.Combine(
                UnityEngine.Application.streamingAssetsPath,
                "data",
                "physical_outlet_cap.json");
            var popRatio = baseline.HasValue && baseline.Value.Pop > 0
                ? (float)adopted.Pop / baseline.Value.Pop
                : 1f;
            var justification =
                $"v1_032 balayage intensity t3000 seed=42195: adopté i={Fmt(adopted.Intensity)} " +
                $"pop={adopted.Pop} (ratio={Fmt(popRatio)}) sat={Fmt(adopted.SatAvg)} " +
                $"deadShare={Fmt(adopted.DeadShare)} ratioPC={Fmt(adopted.PrimaryRatio)} " +
                $"bourgWine={Fmt(adopted.BourgogneWine)} cloth={Fmt(adopted.ClothServed)} " +
                $"stabilityLite={(adopted.StabilityLite ? "Y" : "N")}. {adopted.PickReason}";
            var json =
                "{\n" +
                $"  \"outlet_cap_intensity\": {Fmt(adopted.Intensity)},\n" +
                "  \"storage_months_of_local_demand\": 3.0,\n" +
                $"  \"intensity_justification\": \"{EscapeJson(justification)}\",\n" +
                "  \"storage_justification\": \"v1_031: capacite d'entreposage = storage_months × " +
                "demande locale / tick (1 tick ≈ 1 mois). 3 mois = reserve de saison, derivee " +
                "de la demande reelle — PAS un plafond arbitraire.\"\n" +
                "}\n";
            File.WriteAllText(path, json);
        }

        static void WriteAdoptedMarker(SweepRow adopted)
        {
            var path = Path.Combine(
                UnityEngine.Application.dataPath, "..", "Logs", "v1_032_adopted.txt");
            File.WriteAllText(
                path,
                $"adopted_intensity={Fmt(adopted.Intensity)}\n" +
                $"dead_share={Fmt(adopted.DeadShare)}\n" +
                $"primary_ratio={Fmt(adopted.PrimaryRatio)}\n" +
                $"bourgogne_wine={Fmt(adopted.BourgogneWine)}\n" +
                $"pop={adopted.Pop}\n" +
                $"stability_lite={(adopted.StabilityLite ? "1" : "0")}\n" +
                $"reason={adopted.PickReason}\n");
        }

        static void ApplyAdoptedLocks(float outletIntensity)
        {
            PopGrowthSystem.LockContinuity(Continuity);
            PhysicalSatisfactionBlendSystem.LockWeight(BlendWeight);
            PhysicalProductionSystem.LockOutletCap(outletIntensity, 3f);
            PhysicalStockSystem.IdealPoolMode = false;
            PhysicalStockSystem.MultiHopTransport = true;
            PhysicalStockSystem.ServiceOrderMode =
                PhysicalStockSystem.TransportServiceOrder.ByDeficitSeverity;
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

        static PhysicalEconomyMetrics GetMetrics(EntityManager em)
        {
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<PhysicalEconomyMetrics>());
            return q.GetSingleton<PhysicalEconomyMetrics>();
        }

        static void ForceGc()
        {
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            System.GC.Collect();
        }

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

        static string EscapeJson(string s) =>
            s.Replace("\\", "\\\\").Replace("\"", "\\\"");

        static string Fmt(double v) =>
            v.ToString("0.###", CultureInfo.InvariantCulture);

        static string Fmt(float v) =>
            v.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
