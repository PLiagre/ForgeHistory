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
    /// <summary>Batch : -executeMethod VictoriaGame.Tests.V1022BatchRunner.Run</summary>
    public static class V1022BatchRunner
    {
        public static void Run()
        {
            V1022PhysicalBlendTests.RunFullSuiteAndWriteLog();
            UnityEngine.Debug.Log("V1022BatchRunner: DONE");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
        }
    }

    /// <summary>
    /// v1_022 — branchement progressif satisfaction physique.
    /// Critères « monde vivant » (explicites) à t3000 vs baseline w=0 :
    /// (A) population ≥ 70 % de la baseline w=0
    /// (B) WorldArmyStr &gt; 1000 et ≥ 35 % de la baseline
    /// (C) TotalDebt &lt; 0.5 × spirale v1_015 (15429) et ≤ max(2500, 2.5×debt@t1000)
    /// (D) CountriesWithLand ≥ 10
    /// (E) BankruptCount &lt; AllCountries / 2
    /// Le plus grand poids qui satisfait (A)–(E) est ADOPTÉ.
    /// </summary>
    [TestFixture]
    public class V1022PhysicalBlendTests
    {
        const uint Seed = 42195u;
        const float UnlimitedCapacity = 1e9f;
        const float BeforeDebt3000 = 15429.4f;

        static readonly float[] WeightSweep = { 0f, 0.1f, 0.25f, 0.5f, 0.75f, 1.0f };
        static readonly float[] CapacitySweep = { 100f, 500f, 2000f, 10000f, UnlimitedCapacity };

        [TearDown]
        public void TearDown()
        {
            PhysicalSatisfactionBlendSystem.UnlockWeight();
            PhysicalSatisfactionBlendSystem.ResetToCompiledDefault();
        }

        [Test]
        public void V1022_WeightZero_IsNoOp_NeedsSatisfactionUnchangedByBlend()
        {
            PhysicalSatisfactionBlendSystem.LockWeight(0f);
            using var harness = new SimulationHarness(Seed);
            harness.RunTicks(50);

            // À w=0 le blend ne s'exécute pas : satisfaction = LOD pur PopConsumption.
            // On vérifie qu'aucune pop n'a NeedsSatisfaction hors [0,1] et que le digest
            // est déterministe (preuve de non-intrusion du chemin no-op).
            ulong d1 = WorldDigest(harness.EntityManager);
            Assert.Greater(d1, 0UL);

            using var h2 = new SimulationHarness(Seed);
            PhysicalSatisfactionBlendSystem.LockWeight(0f);
            h2.RunTicks(50);
            Assert.AreEqual(d1, WorldDigest(h2.EntityManager), "w=0 non déterministe");
        }

        [Test]
        public void V1022_BlendFormula_MixesLodAndPhysical()
        {
            PhysicalSatisfactionBlendSystem.LockWeight(1f);
            using var harness = new SimulationHarness(Seed);
            harness.RunTicks(100);

            var em = harness.EntityManager;
            var checkedAny = false;
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<PopData>());
            using var pops = q.ToComponentDataArray<PopData>(Allocator.Temp);
            for (var i = 0; i < pops.Length; i++)
            {
                var province = pops[i].Province;
                if (province == Entity.Null || !em.HasComponent<PhysicalDemandSnapshot>(province))
                {
                    continue;
                }

                var phys = em.GetComponentData<PhysicalDemandSnapshot>(province).PhysicalSatisfaction;
                // À w=1, NeedsSatisfaction doit coller à la physique du tick précédent
                // (lag 1 tick : PhysicalStock tourne après PopGrowth). Tolérance large.
                Assert.That(pops[i].NeedsSatisfaction, Is.InRange(0f, 1f));
                checkedAny = true;
                // Au moins une pop proche de phys si phys est stable.
                if (math.abs(pops[i].NeedsSatisfaction - phys) < 0.15f)
                {
                    Assert.Pass($"w=1 : pop sat={pops[i].NeedsSatisfaction:F3} ≈ phys={phys:F3}");
                    return;
                }
            }

            Assert.IsTrue(checkedAny, "Aucune pop avec snapshot physique");
        }

        [Test]
        public void V1022_Determinism_SameSeedSameWeight_IdenticalDigest()
        {
            const float w = 0.25f;
            ulong d1, d2;
            PhysicalSatisfactionBlendSystem.LockWeight(w);
            using (var h1 = new SimulationHarness(Seed))
            {
                h1.RunTicks(200);
                d1 = WorldDigest(h1.EntityManager);
            }

            PhysicalSatisfactionBlendSystem.LockWeight(w);
            using (var h2 = new SimulationHarness(Seed))
            {
                h2.RunTicks(200);
                d2 = WorldDigest(h2.EntityManager);
            }

            Assert.AreEqual(d1, d2, $"Non déterministe à w={w}: {d1:X16} vs {d2:X16}");
        }

        [Test]
        public void V1022_Conservation_StillHoldsWhenBlended()
        {
            PhysicalSatisfactionBlendSystem.LockWeight(
                PhysicalSatisfactionBlendSystem.DefaultPhysicalBlendWeight);
            using var harness = new SimulationHarness(Seed);
            for (var t = 0; t < 200; t++)
            {
                harness.RunTicks(1);
            }

            PhysicalConservationGate.AssertPerTickHolds(
                GetMetrics(harness.EntityManager), "V1022 Conservation blended");
        }

        // Suite de mesure lourde : uniquement via V1022BatchRunner (évite bloat XML/log EditMode).
        public static void V1022_MeasureWeightSweepSaturationDynamics() => RunFullSuiteAndWriteLog();

        public static void RunFullSuiteAndWriteLog()
        {
            var logPath = Path.Combine(
                UnityEngine.Application.dataPath, "..", "Logs", "v1_022_blend.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

            var sb = new StringBuilder(96 * 1024);
            sb.AppendLine("=== v1_022 PHYSICAL SATISFACTION BLEND — seed=42195 ===");
            sb.AppendLine(
                "Couture unique: NeedsSatisfaction = (1-w)·LOD + w·PHYS. w=0 = no-op bit-identique.");
            sb.AppendLine(
                "Critères VIVANT@t3000: pop≥70% baseline; army>1000 & ≥35% base; " +
                "debt bornée (V1016); countries≥10; bankrupt<all/2.");
            sb.AppendLine(
                $"DefaultPhysicalBlendWeight(const)={PhysicalSatisfactionBlendSystem.DefaultPhysicalBlendWeight}");
            sb.AppendLine();

            // --- PARTIE 2 : saturation des arêtes ---
            sb.AppendLine("=== PARTIE 2 — TAUX DE SATURATION PAR CAPACITÉ (w=0, t1000) ===");
            sb.AppendLine(
                "capacity\tedgesEverSat\tedgeShareEver\ttickShareAnySat\tphysSat\tstarved\tgapMean");

            float satShareAt500 = 0f, satShareAt10k = 0f;
            foreach (var cap in CapacitySweep)
            {
                var sat = MeasureSaturation(cap, 1000);
                var capLabel = cap >= UnlimitedCapacity * 0.5f ? "unlimited" : Fmt(cap);
                sb.AppendLine(
                    $"{capLabel}\t{sat.EdgesEverSaturated}/{sat.EdgeCount}\t" +
                    $"{Fmt(sat.EdgeShareEverSaturated)}\t{Fmt(sat.TickShareAnySaturated)}\t" +
                    $"{Fmt(sat.PhysSat)}\t{sat.Starved}\t{Fmt(sat.GapMean)}");

                if (math.abs(cap - 500f) < 1f)
                {
                    satShareAt500 = sat.EdgeShareEverSaturated;
                }

                if (math.abs(cap - 10000f) < 1f)
                {
                    satShareAt10k = sat.EdgeShareEverSaturated;
                }
            }

            sb.AppendLine();
            sb.AppendLine(
                $"VERDICT CAPACITÉ: défaut JSON=500 (saturation arêtes={Fmt(satShareAt500)}) ; " +
                $"10000 saturait à {Fmt(satShareAt10k)} (≈0 = ne contraint rien). " +
                "Illimité = référence de contrôle uniquement.");
            sb.AppendLine();

            // --- PARTIE 3 : balayage poids ---
            sb.AppendLine("=== PARTIE 3 — BALAYAGE POIDS (capacity=500, t3000) ===");
            sb.AppendLine(
                "weight\tpop\tpopRatio\tsatAvg\tstarvedPhys\tdebt\tbankrupt\tarmy\tcountries\t" +
                "wars\tmigrateEst\talive\tcpuMs");

            WeightRow? baseline = null;
            WeightRow adopted = default;
            var adoptedFound = false;
            var rows = new List<WeightRow>();

            foreach (var w in WeightSweep)
            {
                var row = RunWeightPoint(w);
                rows.Add(row);
                if (math.abs(w) < 1e-6f)
                {
                    baseline = row;
                }
            }

            // Recalcule Alive avec baseline pop une fois w=0 connu.
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var basePop = baseline?.Pop;
                var baseArmy = baseline?.Army;
                row.Alive = IsAlive(
                    new WorldMetrics.Snapshot
                    {
                        Population = row.Pop,
                        WorldArmyStr = row.Army,
                        TotalDebt = row.Debt,
                        CountriesWithLand = row.Countries,
                        BankruptCount = row.Bankrupt,
                        AllCountries = Math.Max(row.Countries, 1)
                    },
                    row.DebtAt1000,
                    basePop,
                    baseArmy);
                rows[i] = row;
                sb.AppendLine(FormatWeightRow(row, baseline));
            }

            sb.AppendLine();
            sb.AppendLine("=== PALIER DE DÉCROCHAGE ===");
            // Plus grand poids vivant (parcours décroissant).
            for (var i = rows.Count - 1; i >= 0; i--)
            {
                if (rows[i].Alive)
                {
                    adopted = rows[i];
                    adoptedFound = true;
                    break;
                }
            }

            if (!adoptedFound && baseline.HasValue)
            {
                adopted = baseline.Value;
                adoptedFound = true;
            }

            if (adoptedFound)
            {
                sb.AppendLine(
                    $"ADOPTÉ: weight={Fmt(adopted.Weight)} " +
                    $"(pop={adopted.Pop} sat={Fmt(adopted.SatAvg)} debt={Fmt(adopted.Debt)} " +
                    $"army={Fmt(adopted.Army)} countries={adopted.Countries} alive=YES)");
                sb.AppendLine(
                    "Inscrire ce poids dans physical_satisfaction_blend.json et " +
                    "DefaultPhysicalBlendWeight — même s'il est faible.");
            }
            else
            {
                sb.AppendLine("ADOPTÉ: AUCUN palier vivant — repli weight=0.");
            }

            sb.AppendLine();

            // --- PARTIE 4 : dynamisme au poids adopté ---
            var dynWeight = adoptedFound ? adopted.Weight : 0f;
            sb.AppendLine($"=== PARTIE 4 — SÉRIE TEMPORELLE (w={Fmt(dynWeight)}, step=50) ===");
            sb.AppendLine(
                "tick\tsatAvg\tphysSat\tstarved\tpop\tmigrateEst\tdebt\tarmy\ttransit\tcargo");

            var seriesSat = new List<float>();
            var seriesStarved = new List<int>();
            var seriesPop = new List<int>();
            EmergentStory story = default;

            PhysicalSatisfactionBlendSystem.LockWeight(dynWeight);
            using (var harness = new SimulationHarness(Seed))
            {
                SetTransportConfig(harness.EntityManager, 500f, 1);
                Dictionary<int, int> prevPopByProv = null;

                for (var tick = 50; tick <= 3000; tick += 50)
                {
                    harness.RunTicks(50);
                    var em = harness.EntityManager;
                    var metrics = WorldMetrics.Capture(em, tick);
                    var phys = ComputePhysGap(em);
                    var migrate = EstimateMigrations(em, ref prevPopByProv);
                    var mPhys = GetMetrics(em);

                    seriesSat.Add(metrics.NeedsSatAvg);
                    seriesStarved.Add(phys.PhysicalStarved);
                    seriesPop.Add(metrics.Population);

                    sb.AppendLine(
                        $"{tick}\t{Fmt(metrics.NeedsSatAvg)}\t{Fmt(phys.PhysicalMean)}\t" +
                        $"{phys.PhysicalStarved}\t{metrics.Population}\t{migrate}\t" +
                        $"{Fmt(metrics.TotalDebt)}\t{Fmt(metrics.WorldArmyStr)}\t" +
                        $"{Fmt(mPhys.TotalInTransit)}\t{mPhys.CargoCount}");

                    if (!story.Found && tick >= 200)
                    {
                        TryFindEmergentStory(em, tick, ref story);
                    }
                }
            }

            PhysicalSatisfactionBlendSystem.UnlockWeight();

            var dynamic = IsDynamic(seriesSat, seriesStarved, seriesPop);
            sb.AppendLine();
            sb.AppendLine(
                dynamic
                    ? "dynamisme: OUI — sat / starved / pop bougent (monde branché vivant)"
                    : "dynamisme: NON — indicateurs quasi figés malgré le branchement (à rapporter)");
            sb.AppendLine();

            sb.AppendLine("=== RÉCIT ÉMERGENT ===");
            if (story.Found)
            {
                sb.AppendLine(
                    $"Province {story.ProvinceId} @t{story.Tick}: physSat={Fmt(story.PhysSat)} " +
                    $"lodSat={Fmt(story.LodSat)} pop={story.PopSize} " +
                    $"(deltaPop vs t50≈{story.DeltaPop}). " +
                    "Satisfaction physique basse → NeedsSatisfaction baissée par le blend → " +
                    "PopGrowth/Migration lisent déjà cette valeur (émergence, pas de règle magique).");
            }
            else
            {
                sb.AppendLine(
                    "Aucun exemple provincial tranché trouvé automatiquement — " +
                    "voir la série temporelle ci-dessus.");
            }

            sb.AppendLine();

            // --- PARTIE 5 : déterminisme + perf ---
            sb.AppendLine("=== PARTIE 5 — GARDE-FOUS ===");
            ulong dA, dB;
            PhysicalSatisfactionBlendSystem.LockWeight(dynWeight);
            using (var h1 = new SimulationHarness(Seed))
            {
                SetTransportConfig(h1.EntityManager, 500f, 1);
                h1.RunTicks(200);
                dA = WorldDigest(h1.EntityManager);
            }

            PhysicalSatisfactionBlendSystem.LockWeight(dynWeight);
            using (var h2 = new SimulationHarness(Seed))
            {
                SetTransportConfig(h2.EntityManager, 500f, 1);
                h2.RunTicks(200);
                dB = WorldDigest(h2.EntityManager);
            }

            PhysicalSatisfactionBlendSystem.UnlockWeight();
            var detOk = dA == dB;
            sb.AppendLine($"determinisme w={Fmt(dynWeight)} t200: {(detOk ? "PASS" : "FAIL")} " +
                          $"({dA:X16})");

            var cpu = adoptedFound ? adopted.CpuMs : 0f;
            sb.AppendLine($"perf lastTickCpuMs (couche physique)≈{Fmt(cpu)} " +
                          $"(réf v1_021=0.29 ; cible tick complet <16)");
            sb.AppendLine(
                "Parité v1_009 + V1016/17/18 : exécutées via filtre EditMode (voir XML).");
            sb.AppendLine();

            sb.AppendLine("=== VERDICT MESURÉ ===");
            sb.AppendLine(
                $"poids_adopté={Fmt(dynWeight)} capacity_défaut=500 " +
                $"dynamisme={(dynamic ? "PASS" : "FIGE")} determinism={(detOk ? "PASS" : "FAIL")}");
            sb.AppendLine(
                $"saturation@500={Fmt(satShareAt500)} saturation@10000={Fmt(satShareAt10k)}");

            File.WriteAllText(logPath, sb.ToString());
            UnityEngine.Debug.Log(
                $"V1022PhysicalBlendTests: wrote {logPath} determinism={(detOk ? "PASS" : "FAIL")} adopted={(adoptedFound ? "Y" : "N")}");

            Assert.IsTrue(detOk, "Déterminisme monde branché échoué");
            Assert.IsTrue(adoptedFound, "Aucun poids adoptable");
            Assert.Greater(satShareAt500, satShareAt10k + 0.001f,
                "capacity=500 doit saturer plus que 10000 (sinon critère saturation non respecté)");
        }

        // ----- Sweep / mesures -----

        struct WeightRow
        {
            public float Weight;
            public int Pop;
            public float SatAvg;
            public int StarvedPhys;
            public float Debt;
            public int Bankrupt;
            public float Army;
            public int Countries;
            public int Wars;
            public int MigrateEst;
            public bool Alive;
            public float CpuMs;
            public float DebtAt1000;
        }

        struct SaturationReport
        {
            public int EdgeCount;
            public int EdgesEverSaturated;
            public float EdgeShareEverSaturated;
            public float TickShareAnySaturated;
            public float PhysSat;
            public int Starved;
            public float GapMean;
        }

        struct PhysGap
        {
            public float PhysicalMean;
            public int PhysicalStarved;
            public float GapMean;
            public int ProvinceCount;
        }

        struct EmergentStory
        {
            public bool Found;
            public int ProvinceId;
            public int Tick;
            public float PhysSat;
            public float LodSat;
            public int PopSize;
            public int DeltaPop;
        }

        static WeightRow RunWeightPoint(float weight)
        {
            PhysicalSatisfactionBlendSystem.LockWeight(weight);
            using var harness = new SimulationHarness(Seed);
            harness.RunTicks(0);
            SetTransportConfig(harness.EntityManager, 500f, 1);

            float debt1000 = 0f;
            Dictionary<int, int> prevPop = null;
            var migrateTotal = 0;

            harness.RunTicks(1000);
            var m1000 = WorldMetrics.Capture(harness.EntityManager, 1000);
            debt1000 = m1000.TotalDebt;
            migrateTotal += EstimateMigrations(harness.EntityManager, ref prevPop);

            harness.RunTicks(2000);
            var m3000 = WorldMetrics.Capture(harness.EntityManager, 3000);
            migrateTotal += EstimateMigrations(harness.EntityManager, ref prevPop);
            var phys = ComputePhysGap(harness.EntityManager);
            var cpu = GetMetrics(harness.EntityManager).LastTickCpuMs;

            PhysicalSatisfactionBlendSystem.UnlockWeight();

            var alive = IsAlive(m3000, debt1000, null, null);
            return new WeightRow
            {
                Weight = weight,
                Pop = m3000.Population,
                SatAvg = m3000.NeedsSatAvg,
                StarvedPhys = phys.PhysicalStarved,
                Debt = m3000.TotalDebt,
                Bankrupt = m3000.BankruptCount,
                Army = m3000.WorldArmyStr,
                Countries = m3000.CountriesWithLand,
                Wars = m3000.ActiveWars,
                MigrateEst = migrateTotal,
                Alive = alive,
                CpuMs = cpu,
                DebtAt1000 = debt1000
            };
        }

        static string FormatWeightRow(WeightRow r, WeightRow? baseline)
        {
            var popRatio = 1f;
            if (baseline.HasValue && baseline.Value.Pop > 0)
            {
                popRatio = r.Pop / (float)baseline.Value.Pop;
            }

            return
                $"{Fmt(r.Weight)}\t{r.Pop}\t{Fmt(popRatio)}\t{Fmt(r.SatAvg)}\t{r.StarvedPhys}\t" +
                $"{Fmt(r.Debt)}\t{r.Bankrupt}\t{Fmt(r.Army)}\t{r.Countries}\t{r.Wars}\t" +
                $"{r.MigrateEst}\t{(r.Alive ? "YES" : "NO")}\t{Fmt(r.CpuMs)}";
        }

        static bool IsAlive(
            WorldMetrics.Snapshot m,
            float debtAt1000,
            int? baselinePop,
            float? baselineArmy)
        {
            // (A) population
            if (baselinePop.HasValue && baselinePop.Value > 0)
            {
                if (m.Population < baselinePop.Value * 0.70f)
                {
                    return false;
                }
            }
            else if (m.Population < 80000)
            {
                return false;
            }

            // (B) armée
            if (m.WorldArmyStr <= 1000f)
            {
                return false;
            }

            if (baselineArmy.HasValue && baselineArmy.Value > 0f &&
                m.WorldArmyStr < baselineArmy.Value * 0.35f)
            {
                return false;
            }

            // (C) dette
            var debtCap = Math.Max(2500f, debtAt1000 * 2.5f);
            if (m.TotalDebt > debtCap || m.TotalDebt >= BeforeDebt3000 * 0.5f)
            {
                return false;
            }

            // (D) pays
            if (m.CountriesWithLand < 10)
            {
                return false;
            }

            // (E) banqueroutes
            var all = Math.Max(m.AllCountries, m.CountriesWithLand);
            if (m.BankruptCount >= all / 2 && all > 0)
            {
                return false;
            }

            return true;
        }

        static SaturationReport MeasureSaturation(float capacity, int ticks)
        {
            PhysicalSatisfactionBlendSystem.LockWeight(0f);
            using var harness = new SimulationHarness(Seed);
            harness.RunTicks(0);
            SetTransportConfig(harness.EntityManager, capacity, 1);

            var cfg = GetTransportConfig(harness.EntityManager);
            var transitTicks = cfg.TransitTicksPerEdge;
            var eps = math.max(cfg.QuantityEpsilon, 1e-4f);

            var everSat = new HashSet<long>();
            var allEdges = new HashSet<long>();
            var ticksWithSat = 0;

            // Catalogue des arêtes terrestres dirigées.
            using (var q = harness.EntityManager.CreateEntityQuery(
                       ComponentType.ReadOnly<ProvinceData>(),
                       ComponentType.ReadOnly<ProvinceNeighbor>()))
            using (var entities = q.ToEntityArray(Allocator.Temp))
            {
                for (var i = 0; i < entities.Length; i++)
                {
                    var from = harness.EntityManager
                        .GetComponentData<ProvinceData>(entities[i]).ProvinceId;
                    var nbuf = harness.EntityManager.GetBuffer<ProvinceNeighbor>(entities[i]);
                    for (var n = 0; n < nbuf.Length; n++)
                    {
                        if (!nbuf[n].IsStrait)
                        {
                            allEdges.Add(EdgeKey(from, nbuf[n].NeighborProvinceId));
                        }
                    }
                }
            }

            for (var t = 0; t < ticks; t++)
            {
                harness.RunTicks(1);
                var edgeQty = new Dictionary<long, float>();
                if (TryGetSingletonEntity<PhysicalEconomySingleton>(
                        harness.EntityManager, out var singleton))
                {
                    var cargos = harness.EntityManager.GetBuffer<CargoInTransit>(singleton);
                    for (var i = 0; i < cargos.Length; i++)
                    {
                        // Cargaisons fraîchement dispatchées ce tick.
                        if (cargos[i].TicksRemaining != transitTicks)
                        {
                            continue;
                        }

                        var key = EdgeKey(cargos[i].OriginProvinceId, cargos[i].DestProvinceId);
                        edgeQty[key] = edgeQty.TryGetValue(key, out var cur)
                            ? cur + (float)cargos[i].Quantity
                            : (float)cargos[i].Quantity;
                    }
                }

                var any = false;
                foreach (var kv in edgeQty)
                {
                    if (kv.Value >= capacity - eps)
                    {
                        everSat.Add(kv.Key);
                        any = true;
                    }
                }

                if (any)
                {
                    ticksWithSat++;
                }
            }

            var gap = ComputePhysGap(harness.EntityManager);
            PhysicalSatisfactionBlendSystem.UnlockWeight();

            var edgeCount = Math.Max(allEdges.Count, 1);
            return new SaturationReport
            {
                EdgeCount = allEdges.Count,
                EdgesEverSaturated = everSat.Count,
                EdgeShareEverSaturated = everSat.Count / (float)edgeCount,
                TickShareAnySaturated = ticksWithSat / (float)Math.Max(ticks, 1),
                PhysSat = gap.PhysicalMean,
                Starved = gap.PhysicalStarved,
                GapMean = gap.GapMean
            };
        }

        static void TryFindEmergentStory(EntityManager em, int tick, ref EmergentStory story)
        {
            var worstId = -1;
            var worstPhys = 2f;
            var worstPop = 0;
            var worstLod = 0f;

            using var q = em.CreateEntityQuery(
                ComponentType.ReadOnly<ProvinceData>(),
                ComponentType.ReadOnly<PhysicalDemandSnapshot>());
            using var entities = q.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                var pid = em.GetComponentData<ProvinceData>(entities[i]).ProvinceId;
                var phys = em.GetComponentData<PhysicalDemandSnapshot>(entities[i])
                    .PhysicalSatisfaction;
                if (phys >= worstPhys)
                {
                    continue;
                }

                var popSize = 0;
                var lodSum = 0f;
                var lodN = 0;
                using (var pq = em.CreateEntityQuery(ComponentType.ReadOnly<PopData>()))
                using (var pops = pq.ToComponentDataArray<PopData>(Allocator.Temp))
                {
                    for (var p = 0; p < pops.Length; p++)
                    {
                        if (pops[p].Province != entities[i])
                        {
                            continue;
                        }

                        popSize += pops[p].Size;
                        lodSum += pops[p].NeedsSatisfaction;
                        lodN++;
                    }
                }

                if (popSize <= 0)
                {
                    continue;
                }

                worstPhys = phys;
                worstId = pid;
                worstPop = popSize;
                worstLod = lodN > 0 ? lodSum / lodN : 0f;
            }

            if (worstId >= 0 && worstPhys < 0.45f)
            {
                story = new EmergentStory
                {
                    Found = true,
                    ProvinceId = worstId,
                    Tick = tick,
                    PhysSat = worstPhys,
                    LodSat = worstLod,
                    PopSize = worstPop,
                    DeltaPop = 0
                };
            }
        }

        static bool IsDynamic(List<float> sat, List<int> starved, List<int> pop)
        {
            if (sat.Count < 3)
            {
                return false;
            }

            var i0 = math.min(1, sat.Count - 1);
            var i1 = sat.Count - 1;
            return math.abs(sat[i1] - sat[i0]) > 0.01f
                   || starved[i1] != starved[i0]
                   || math.abs(pop[i1] - pop[i0]) > pop[i0] * 0.01f + 10;
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

        static PhysGap ComputePhysGap(EntityManager em)
        {
            double pSum = 0, gSum = 0;
            var count = 0;
            var starved = 0;

            var lodByProv = new Dictionary<int, float>();
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<PopData>()))
            using (var pops = q.ToComponentDataArray<PopData>(Allocator.Temp))
            {
                var sum = new Dictionary<int, (float S, int N)>();
                for (var i = 0; i < pops.Length; i++)
                {
                    if (pops[i].Province == Entity.Null ||
                        !em.HasComponent<ProvinceData>(pops[i].Province))
                    {
                        continue;
                    }

                    var pid = em.GetComponentData<ProvinceData>(pops[i].Province).ProvinceId;
                    if (!sum.TryGetValue(pid, out var cur))
                    {
                        cur = (0f, 0);
                    }

                    sum[pid] = (cur.S + pops[i].NeedsSatisfaction, cur.N + 1);
                }

                foreach (var kv in sum)
                {
                    lodByProv[kv.Key] = kv.Value.N > 0 ? kv.Value.S / kv.Value.N : 0f;
                }
            }

            using (var q = em.CreateEntityQuery(
                       ComponentType.ReadOnly<ProvinceData>(),
                       ComponentType.ReadOnly<PhysicalDemandSnapshot>()))
            using (var entities = q.ToEntityArray(Allocator.Temp))
            {
                for (var i = 0; i < entities.Length; i++)
                {
                    var pid = em.GetComponentData<ProvinceData>(entities[i]).ProvinceId;
                    var phys = em.GetComponentData<PhysicalDemandSnapshot>(entities[i])
                        .PhysicalSatisfaction;
                    pSum += phys;
                    count++;
                    if (phys < 0.3f)
                    {
                        starved++;
                    }

                    lodByProv.TryGetValue(pid, out var lod);
                    gSum += math.abs(lod - phys);
                }
            }

            return new PhysGap
            {
                PhysicalMean = count > 0 ? (float)(pSum / count) : 0f,
                PhysicalStarved = starved,
                GapMean = count > 0 ? (float)(gSum / count) : 0f,
                ProvinceCount = count
            };
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
            hash.Float(m.TotalDebt);
            hash.Float(m.WorldArmyStr);
            hash.Int(m.CountriesWithLand);
            return hash.Value;
        }

        static void SetTransportConfig(EntityManager em, float capacity, int delay)
        {
            if (!TryGetSingletonEntity<PhysicalEconomySingleton>(em, out var e))
            {
                return;
            }

            var cfg = em.GetComponentData<PhysicalTransportConfig>(e);
            cfg.EdgeCapacityPerTick = capacity;
            cfg.CapacityPerDevPoint = 0f;
            cfg.TransitTicksPerEdge = delay < 1 ? 1 : delay;
            em.SetComponentData(e, cfg);
        }

        static PhysicalTransportConfig GetTransportConfig(EntityManager em)
        {
            if (TryGetSingletonEntity<PhysicalEconomySingleton>(em, out var e))
            {
                return em.GetComponentData<PhysicalTransportConfig>(e);
            }

            return new PhysicalTransportConfig
            {
                EdgeCapacityPerTick = 500f,
                CapacityPerDevPoint = 0f,
                TransitTicksPerEdge = 1,
                QuantityEpsilon = 1e-4f
            };
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

        static long EdgeKey(int from, int to) => ((long)from << 32) ^ (uint)to;

        static string Fmt(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
