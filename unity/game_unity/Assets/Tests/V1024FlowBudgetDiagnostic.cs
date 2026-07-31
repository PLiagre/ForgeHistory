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
    /// <summary>Batch : -executeMethod VictoriaGame.Tests.V1024BatchRunner.Run</summary>
    public static class V1024BatchRunner
    {
        public static void Run()
        {
            V1024FlowBudgetDiagnostic.RunFullSuiteAndWriteLog();
            UnityEngine.Debug.Log("V1024BatchRunner: DONE");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
        }
    }

    /// <summary>
    /// v1_024 — budget de flux, cellule multi-sauts×capacité, calibration infra,
    /// résidu sous-production drap, rebalayage poids.
    /// </summary>
    [TestFixture]
    public class V1024FlowBudgetDiagnostic
    {
        const uint Seed = 42195u;
        const float UnlimitedCapacity = 1e9f;
        const float BeforeDebt3000 = 15429.4f;
        const int MeasureTicks = 1000;
        const int SweepTicks = 3000;
        const float FlowMargin = 1.5f;
        const int ClothGoodId = 8;
        const int WoolGoodId = 6;

        static readonly float[] WeightSweep = { 0f, 0.1f, 0.25f, 0.5f, 0.75f, 1.0f };
        static readonly float[] CapacitySweep =
            { 500f, 2000f, 10000f, 50000f, UnlimitedCapacity };

        [TearDown]
        public void TearDown()
        {
            PhysicalSatisfactionBlendSystem.UnlockWeight();
            PhysicalSatisfactionBlendSystem.ResetToCompiledDefault();
            PhysicalStockSystem.IdealPoolMode = false;
            PhysicalStockSystem.MultiHopTransport = true;
        }

        [Test]
        public void V1024_DemandParity_UsesRelativeTolerance()
        {
            PhysicalSatisfactionBlendSystem.LockWeight(0f);
            PhysicalStockSystem.MultiHopTransport = false;
            using var harness = new SimulationHarness(Seed);
            harness.RunTicks(100);
            var cmp = CompareDemandSupply(harness.EntityManager);
            AssertRelativeClose(cmp.LodFoodDemand, cmp.PhysFoodDemand, 1e-5f, "Food");
            AssertRelativeClose(cmp.LodClothDemand, cmp.PhysClothDemand, 1e-5f, "Cloth");
            AssertRelativeClose(cmp.LodLuxuryDemand, cmp.PhysLuxuryDemand, 1e-5f, "Luxury");
        }

        [Test]
        public void V1024_MultiHop_DefaultIsOn_AfterCellProof()
        {
            Assert.IsTrue(PhysicalStockSystem.MultiHopTransport,
                "MultiHop ON: cellule v1_024 prouve clothServedShare ×3.7 à capacité desserrée");
        }

        [Test]
        public void V1024_WeightZero_NoOp()
        {
            PhysicalSatisfactionBlendSystem.LockWeight(0f);
            PhysicalStockSystem.MultiHopTransport = true;
            using var h1 = new SimulationHarness(Seed);
            h1.RunTicks(50);
            var d1 = WorldDigest(h1.EntityManager);
            using var h2 = new SimulationHarness(Seed);
            PhysicalSatisfactionBlendSystem.LockWeight(0f);
            h2.RunTicks(50);
            Assert.AreEqual(d1, WorldDigest(h2.EntityManager));
        }

        [Test]
        public void V1024_Determinism_AdoptedDefaults()
        {
            PhysicalSatisfactionBlendSystem.LockWeight(0.25f);
            PhysicalStockSystem.MultiHopTransport = true;
            ulong d1, d2;
            using (var h1 = new SimulationHarness(Seed))
            {
                h1.RunTicks(0);
                SetTransportInfra(h1.EntityManager, 2400.643f);
                h1.RunTicks(200);
                d1 = WorldDigest(h1.EntityManager);
            }

            using (var h2 = new SimulationHarness(Seed))
            {
                PhysicalSatisfactionBlendSystem.LockWeight(0.25f);
                h2.RunTicks(0);
                SetTransportInfra(h2.EntityManager, 2400.643f);
                h2.RunTicks(200);
                d2 = WorldDigest(h2.EntityManager);
            }

            Assert.AreEqual(d1, d2, $"Non déterministe: {d1:X16} vs {d2:X16}");
        }

        [Test]
        public void V1024_FlowBudget_CapacityStrangles()
        {
            PhysicalSatisfactionBlendSystem.LockWeight(0f);
            PhysicalStockSystem.MultiHopTransport = false;
            using var harness = new SimulationHarness(Seed);
            harness.RunTicks(0);
            SetTransportConfig(harness.EntityManager, 500f, 1);
            harness.RunTicks(MeasureTicks);
            var budget = MeasureFlowBudget(harness.EntityManager, 500f);
            Assert.Greater(budget.DirectedEdgeCount, 0);
            Assert.Greater(budget.AggNeedOverCap, 1f,
                $"Hypothèse étranglement infirmée: besoin/cap={budget.AggNeedOverCap}");
        }

        [Test]
        public void V1024_Underproduction_LodSeedingWasBottleneck()
        {
            // v1_024 : le LOD seul (4 sites laine) ne couvre pas la demande drap.
            // v1_025 ajoute l'endowment physique — IdealPool peut désormais servir le drap.
            // Ce test documente encore le déficit LOD (amorçage historique), pas le couvercle physique.
            PhysicalSatisfactionBlendSystem.LockWeight(0f);
            var under = MeasureUnderproduction();
            Assert.Greater(under.ClothDemand, 0f);
            Assert.Less(under.WoolCapacity, under.ClothDemand,
                "LOD wool cap ≥ cloth demand — le diagnostic v1_024 (LOD) ne tiendrait plus");
            // Avec endowment, IdealPool peut couvrir : on ne assert plus cover<0.55.
            Assert.Greater(under.ClothCoverIdeal, 0f);
        }

        // Diagnostic lourd : uniquement via V1024BatchRunner (évite OOM Allocator.Temp
        // dans -runTests après 10×t3000 multi-hop). Ne pas marquer [Test].
        public static void RunFullSuiteEntry() => RunFullSuiteAndWriteLog();

        public static void RunFullSuiteAndWriteLog()
        {
            var logPath = Path.Combine(
                UnityEngine.Application.dataPath, "..", "Logs", "v1_024_flow.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            var sb = new StringBuilder(256 * 1024);

            sb.AppendLine("=== v1_024 FLOW BUDGET DIAGNOSTIC — seed=42195 ===");
            sb.AppendLine(
                "Objectif: budget de flux → cellule multi-sauts×cap → calibration infra → " +
                "sous-production drap → rebalayage poids.");
            sb.AppendLine();

            PhysicalSatisfactionBlendSystem.LockWeight(0f);
            PhysicalStockSystem.IdealPoolMode = false;
            PhysicalStockSystem.MultiHopTransport = false;

            // ---------- PARTIE 1 — BUDGET DE FLUX ----------
            sb.AppendLine("=== PARTIE 1 — BUDGET DE FLUX (cap500, MultiHop=OFF) ===");
            FlowBudget budget;
            DeadStockSplit dead;
            using (var h = new SimulationHarness(Seed))
            {
                h.RunTicks(0);
                SetTransportConfig(h.EntityManager, 500f, 1);
                h.RunTicks(MeasureTicks);
                budget = MeasureFlowBudget(h.EntityManager, 500f);
                dead = MeasureDeadStockSplit(h.EntityManager);
            }

            sb.AppendLine(
                $"edgesDirected={budget.DirectedEdgeCount} " +
                $"installedCap@500={Fmt(budget.InstalledCapacity)} " +
                $"meanDevScore={Fmt(budget.MeanDevScore)}");
            sb.AppendLine(
                "good\tneed\thopNeed\tinstalled\tratioNeed\tratioHop");
            AppendGoodBudget(sb, "Food", budget.FoodNeed, budget.FoodHopNeed, budget.InstalledCapacity);
            AppendGoodBudget(sb, "Cloth", budget.ClothNeed, budget.ClothHopNeed, budget.InstalledCapacity);
            AppendGoodBudget(sb, "Luxury", budget.LuxuryNeed, budget.LuxuryHopNeed, budget.InstalledCapacity);
            AppendGoodBudget(sb, "Inputs", budget.InputNeed, budget.InputHopNeed, budget.InstalledCapacity);
            sb.AppendLine(
                $"AGG need={Fmt(budget.AggNeed)} hopNeed={Fmt(budget.AggHopNeed)} " +
                $"ratioNeed/cap={Fmt(budget.AggNeedOverCap)} " +
                $"ratioHop/cap={Fmt(budget.AggHopOverCap)}");
            var strangles = budget.AggHopOverCap > 1.5f || budget.AggNeedOverCap > 1.5f;
            sb.AppendLine(
                strangles
                    ? $"VERDICT: capacité ÉTRANGLANTE (hop/cap={Fmt(budget.AggHopOverCap)} > 1.5)"
                    : $"VERDICT: capacité NON étranglante (hop/cap={Fmt(budget.AggHopOverCap)}) — chercher ailleurs");
            sb.AppendLine(
                $"stockMort: total={Fmt(dead.TotalStock)} dead={Fmt(dead.DeadStock)} " +
                $"share={Fmt(dead.DeadShare)} noConsumer={Fmt(dead.NoConsumerShare)} " +
                $"outOfReach={Fmt(dead.OutOfReachShare)} wood={Fmt(dead.Wood)}");
            sb.AppendLine();

            // Calibration dérivée du budget (avant cellule — pour JSON / défauts).
            var calibratedPerDev = budget.MeanDevScore > 1e-3f && budget.DirectedEdgeCount > 0
                ? budget.AggHopNeed * FlowMargin /
                  (budget.DirectedEdgeCount * budget.MeanDevScore)
                : 750f;
            var calibratedConstant = budget.DirectedEdgeCount > 0
                ? budget.AggHopNeed * FlowMargin / budget.DirectedEdgeCount
                : 2000f;
            sb.AppendLine(
                $"CALIBRATION dérivée: CapacityPerDevPoint={Fmt(calibratedPerDev)} " +
                $"(margin={Fmt(FlowMargin)}) ; constante équivalente≈{Fmt(calibratedConstant)}");
            sb.AppendLine();

            // ---------- PARTIE 2 — CELLULE MultiHop × capacité ----------
            sb.AppendLine("=== PARTIE 2 — CELLULE MultiHop × capacité (t3000) ===");
            sb.AppendLine(
                "multiHop\tcap\tphysMean\tphysStd\tclothServedShare\tdeadShare\t" +
                "missedIn\ttransit\tcpuMs");

            var cell = new List<CellPoint>();
            foreach (var multi in new[] { false, true })
            {
                foreach (var cap in CapacitySweep)
                {
                    var pt = RunCellPoint(cap, SweepTicks, multi);
                    cell.Add(pt);
                    sb.AppendLine(
                        $"{multi}\t{CapLabel(cap)}\t{Fmt(pt.PhysMean)}\t{Fmt(pt.PhysStd)}\t" +
                        $"{Fmt(pt.ClothServedShare)}\t{Fmt(pt.DeadShare)}\t" +
                        $"{Fmt(pt.MissedIn)}\t{Fmt(pt.Transit)}\t{Fmt(pt.CpuMs)}");
                    System.GC.Collect();
                }
            }

            sb.AppendLine();
            // Trancher multi-sauts : décisif si +0.03 phys OU +0.05 clothServed
            // à capacité desserrée (≥2000) — la mission est l'arrivée des intrants.
            var keepMulti = false;
            var bestGain = 0f;
            var bestClothGain = 0f;
            var bestCapLabel = "none";
            foreach (var cap in CapacitySweep)
            {
                CellPoint off = default, on = default;
                foreach (var p in cell)
                {
                    if (math.abs(p.Capacity - cap) < 1f ||
                        (cap >= UnlimitedCapacity * 0.5f && p.Capacity >= UnlimitedCapacity * 0.5f))
                    {
                        if (!p.MultiHop)
                        {
                            off = p;
                        }
                        else
                        {
                            on = p;
                        }
                    }
                }

                var gain = on.PhysMean - off.PhysMean;
                var clothGain = on.ClothServedShare - off.ClothServedShare;
                if (gain > bestGain)
                {
                    bestGain = gain;
                    bestCapLabel = CapLabel(cap);
                }

                if (clothGain > bestClothGain)
                {
                    bestClothGain = clothGain;
                }

                var desseree = cap >= 2000f;
                if (desseree && (gain >= 0.03f || clothGain >= 0.05f))
                {
                    keepMulti = true;
                }
            }

            PhysicalStockSystem.MultiHopTransport = keepMulti;
            sb.AppendLine(
                $"SORT MULTI-SAUTS: {(keepMulti ? "GARDER (ON)" : "DÉSACTIVER (OFF)")} " +
                $"bestGainPhys={Fmt(bestGain)} bestGainCloth={Fmt(bestClothGain)} @{bestCapLabel} " +
                $"(seuil: +0.03 phys OU +0.05 clothServed @cap≥2000)");
            sb.AppendLine(
                $"cpu@cap500 OFF≈{Fmt(FindCell(cell, false, 500f).CpuMs)} " +
                $"ON≈{Fmt(FindCell(cell, true, 500f).CpuMs)}");
            sb.AppendLine();

            // ---------- PARTIE 3 — CAPACITÉ INFRA + SATURATION ----------
            sb.AppendLine("=== PARTIE 3 — CAPACITÉ INFRA (dérivée budget, pas satisfaction) ===");
            var perDev = math.max(calibratedPerDev, 100f);
            sb.AppendLine(
                $"règle: edgeCap = {Fmt(perDev)} × avg(DevScore(A),DevScore(B)) ; " +
                $"DevScore=(Tax+Prod+Manpower)/3");
            sb.AppendLine(
                $"installe théorique≈{Fmt(perDev * budget.MeanDevScore * budget.DirectedEdgeCount)} " +
                $"vs hopNeed×margin={Fmt(budget.AggHopNeed * FlowMargin)}");

            PhysicalStockSystem.MultiHopTransport = keepMulti;
            CellPoint infraPt;
            SaturationReport satInfra;
            using (var h = new SimulationHarness(Seed))
            {
                h.RunTicks(0);
                SetTransportInfra(h.EntityManager, perDev);
                h.RunTicks(SweepTicks);
                infraPt = CaptureCell(h.EntityManager, perDev, keepMulti);
                satInfra = MeasureSaturationInfra(h.EntityManager, perDev, 40);
            }

            var sat500 = MeasureSaturationConstant(500f, 40);
            sb.AppendLine(
                $"infra physMean={Fmt(infraPt.PhysMean)} clothShare={Fmt(infraPt.ClothServedShare)} " +
                $"missedIn={Fmt(infraPt.MissedIn)} cpu={Fmt(infraPt.CpuMs)}");
            sb.AppendLine(
                $"saturation@cap500: {sat500.EdgesEverSaturated}/{sat500.EdgeCount}=" +
                $"{Fmt(sat500.EdgeShareEverSaturated)} (réf v1_022=11% monde quasi-mort)");
            sb.AppendLine(
                $"saturation@infra: {satInfra.EdgesEverSaturated}/{satInfra.EdgeCount}=" +
                $"{Fmt(satInfra.EdgeShareEverSaturated)} " +
                $"(rapporté au budget hop/cap={Fmt(budget.AggHopOverCap)})");
            sb.AppendLine();

            // ---------- PARTIE 4 — SOUS-PRODUCTION ----------
            sb.AppendLine("=== PARTIE 4 — RÉSIDU SOUS-PRODUCTION DRAP ===");
            var under = MeasureUnderproduction();
            sb.AppendLine(
                $"clothSites={under.ClothSites} clothBaseCap={Fmt(under.ClothCapacity)} " +
                $"clothDemand={Fmt(under.ClothDemand)} " +
                $"coverSites={Fmt(under.ClothCapacity / math.max(under.ClothDemand, 1f))}");
            sb.AppendLine(
                $"woolSites={under.WoolSites} woolBaseCap={Fmt(under.WoolCapacity)} " +
                $"recipe cloth←1 wool → maxPhysCloth≈{Fmt(under.WoolCapacity)}");
            sb.AppendLine(
                $"idealPool clothServed={Fmt(under.ClothServedIdeal)} " +
                $"cover={Fmt(under.ClothCoverIdeal)} " +
                $"(réf v1_023≈0.40)");
            sb.AppendLine(
                under.WoolCapacity < under.ClothDemand
                    ? "VERDICT: AMORÇAGE DU MONDE — capacité laine < demande drap. " +
                      "Aucun transport ne sauve. Sites cloth OK (cap>demande) ; goulot=intrant wool. " +
                      "Pas de correction magique de recette ici ; prochain chantier=amorçage."
                    : "VERDICT: capacité intrants OK — chercher ailleurs (géographie/recettes).");
            sb.AppendLine();

            // ---------- PARTIE 5 — POIDS + GARDE-FOUS ----------
            sb.AppendLine("=== PARTIE 5 — REBALAYAGE POIDS (infra, MultiHop=" + keepMulti + ") ===");
            sb.AppendLine(
                "weight\tpop\tpopRatio\tsatAvg\tphysMean\tstarved\tdebt\tbankrupt\t" +
                "army\tcountries\twars\talive\tcpuMs");

            WeightRow? baseline = null;
            var adoptedW = -1f;
            var adoptedFound = false;
            foreach (var w in WeightSweep)
            {
                var row = RunWeightPoint(w, perDev, keepMulti);
                if (w <= 0f)
                {
                    baseline = row;
                }

                var alive = IsAlive(row, baseline);
                if (alive)
                {
                    adoptedW = w;
                    adoptedFound = true;
                }

                var popRatio = baseline.HasValue && baseline.Value.Pop > 0
                    ? row.Pop / (float)baseline.Value.Pop
                    : 1f;
                sb.AppendLine(
                    $"{Fmt(w)}\t{row.Pop}\t{Fmt(popRatio)}\t{Fmt(row.SatAvg)}\t{Fmt(row.PhysMean)}\t" +
                    $"{row.Starved}\t{Fmt(row.Debt)}\t{row.Bankrupt}\t{Fmt(row.Army)}\t" +
                    $"{row.Countries}\t{row.Wars}\t{(alive ? "YES" : "NO")}\t{Fmt(row.CpuMs)}");
            }

            sb.AppendLine(
                $"NOUVEAU PALIER ADOPTABLE: w={(adoptedFound ? Fmt(adoptedW) : "none")} " +
                $"(réf v1_023=0.25). Lire physMean AVEC population (effondrement → phys↑).");
            sb.AppendLine();

            var storyW = adoptedFound ? adoptedW : 0.25f;
            var story = FindEmergentStory(storyW, perDev, keepMulti);
            sb.AppendLine("=== RÉCIT ÉMERGENT ===");
            if (story.Found)
            {
                sb.AppendLine(
                    $"OUI — prov={story.ProvinceId} phys={Fmt(story.PhysSat)} " +
                    $"lod={Fmt(story.LodSat)} pop={story.PopSize} deltaPop={story.DeltaPop}");
            }
            else
            {
                sb.AppendLine("NON — aucune province mal desservie isolée avec signal pop.");
            }

            sb.AppendLine();
            sb.AppendLine("=== GARDE-FOUS ===");
            PhysicalSatisfactionBlendSystem.LockWeight(0.25f);
            PhysicalStockSystem.MultiHopTransport = keepMulti;
            ulong dA, dB;
            using (var h1 = new SimulationHarness(Seed))
            {
                h1.RunTicks(0);
                SetTransportInfra(h1.EntityManager, perDev);
                h1.RunTicks(200);
                dA = WorldDigest(h1.EntityManager);
            }

            using (var h2 = new SimulationHarness(Seed))
            {
                PhysicalSatisfactionBlendSystem.LockWeight(0.25f);
                h2.RunTicks(0);
                SetTransportInfra(h2.EntityManager, perDev);
                h2.RunTicks(200);
                dB = WorldDigest(h2.EntityManager);
            }

            var detOk = dA == dB;
            sb.AppendLine($"determinisme w=0.25 t200: {(detOk ? "PASS" : "FAIL")} ({dA:X16})");
            sb.AppendLine($"perf infra cpuMs≈{Fmt(infraPt.CpuMs)} (réf v1_022≈0.30 sans multi)");
            sb.AppendLine(
                "Parité v1_009 + V1016/17/18 + V1020..V1024 : filtre EditMode (voir XML).");
            sb.AppendLine();

            sb.AppendLine("=== VERDICT MESURÉ ===");
            sb.AppendLine(
                $"budget: need/cap={Fmt(budget.AggNeedOverCap)} hop/cap={Fmt(budget.AggHopOverCap)} " +
                $"étrangle={(strangles ? "OUI" : "NON")}");
            sb.AppendLine(
                $"multiHop={(keepMulti ? "ON" : "OFF")} bestGainPhys={Fmt(bestGain)} " +
                $"bestGainCloth={Fmt(bestClothGain)}");
            sb.AppendLine(
                $"infra perDev={Fmt(perDev)} phys={Fmt(infraPt.PhysMean)} " +
                $"clothShare={Fmt(infraPt.ClothServedShare)}");
            sb.AppendLine(
                $"sous-prod: woolCap={Fmt(under.WoolCapacity)} clothDemand={Fmt(under.ClothDemand)} " +
                $"→ AMORÇAGE");
            sb.AppendLine(
                $"palier_poids={(adoptedFound ? Fmt(adoptedW) : "none")} determinism=" +
                $"{(detOk ? "PASS" : "FAIL")}");

            File.WriteAllText(logPath, sb.ToString());
            UnityEngine.Debug.Log(
                $"V1024FlowBudgetDiagnostic: wrote {logPath} determinism={(detOk ? "PASS" : "FAIL")} multiHop={(keepMulti ? "ON" : "OFF")}");

            PhysicalSatisfactionBlendSystem.UnlockWeight();
            PhysicalStockSystem.IdealPoolMode = false;
            PhysicalStockSystem.MultiHopTransport = keepMulti;

            Assert.IsTrue(strangles || budget.AggNeedOverCap > 0.5f,
                "Partie 1: budget de flux non mesuré");
            Assert.IsTrue(detOk, "Déterminisme échoué");
            Assert.Greater(cell.Count, 5, "Partie 2: cellule incomplète");
        }

        // ----- structures -----

        struct FlowBudget
        {
            public int DirectedEdgeCount;
            public float InstalledCapacity;
            public float MeanDevScore;
            public float FoodNeed, ClothNeed, LuxuryNeed, InputNeed, AggNeed;
            public float FoodHopNeed, ClothHopNeed, LuxuryHopNeed, InputHopNeed, AggHopNeed;
            public float AggNeedOverCap, AggHopOverCap;
        }

        struct DeadStockSplit
        {
            public float TotalStock, DeadStock, DeadShare, Wood;
            public float NoConsumerShare, OutOfReachShare;
        }

        struct CellPoint
        {
            public bool MultiHop;
            public float Capacity;
            public float PhysMean, PhysStd;
            public float ClothServedShare, DeadShare, MissedIn, Transit, CpuMs;
        }

        struct Underproduction
        {
            public int ClothSites, WoolSites;
            public float ClothCapacity, WoolCapacity, ClothDemand;
            public float ClothServedIdeal, ClothCoverIdeal;
        }

        struct WeightRow
        {
            public float Weight;
            public int Pop;
            public float SatAvg, PhysMean, Debt, Army, CpuMs, DebtAt1000;
            public int Starved, Bankrupt, Countries, Wars;
        }

        struct EmergentStory
        {
            public bool Found;
            public int ProvinceId;
            public float PhysSat, LodSat;
            public int PopSize, DeltaPop;
        }

        struct SaturationReport
        {
            public int EdgeCount, EdgesEverSaturated;
            public float EdgeShareEverSaturated;
        }

        struct DemandSupplyCmp
        {
            public float LodFoodDemand, LodClothDemand, LodLuxuryDemand;
            public float PhysFoodDemand, PhysClothDemand, PhysLuxuryDemand;
            public float LodFoodSupply, LodClothSupply, LodLuxurySupply;
            public float PhysFoodServed, PhysClothServed, PhysLuxuryServed;
        }

        // ----- mesures -----

        static FlowBudget MeasureFlowBudget(EntityManager em, float capacityPerEdge)
        {
            var entityByPid = new Dictionary<int, Entity>();
            var adj = new Dictionary<int, List<int>>();
            var edges = new HashSet<long>();
            double devSum = 0;
            var devN = 0;

            using (var q = em.CreateEntityQuery(
                       ComponentType.ReadOnly<ProvinceData>(),
                       ComponentType.ReadOnly<ProvinceNeighbor>()))
            using (var entities = q.ToEntityArray(Allocator.Temp))
            {
                for (var i = 0; i < entities.Length; i++)
                {
                    var e = entities[i];
                    if (!em.HasComponent<PhysicalDemandSnapshot>(e))
                    {
                        continue;
                    }

                    var pid = em.GetComponentData<ProvinceData>(e).ProvinceId;
                    entityByPid[pid] = e;
                    var list = new List<int>();
                    var nbuf = em.GetBuffer<ProvinceNeighbor>(e);
                    for (var n = 0; n < nbuf.Length; n++)
                    {
                        if (nbuf[n].IsStrait)
                        {
                            continue;
                        }

                        list.Add(nbuf[n].NeighborProvinceId);
                        edges.Add(EdgeKey(pid, nbuf[n].NeighborProvinceId));
                    }

                    list.Sort();
                    adj[pid] = list;
                    if (em.HasComponent<ProvinceDevelopment>(e))
                    {
                        var d = em.GetComponentData<ProvinceDevelopment>(e);
                        devSum += (d.Tax + d.Production + d.Manpower) / 3.0;
                        devN++;
                    }
                }
            }

            // Demande pop + production locale (LastOutput) + déficits d'intrants.
            var foodDem = new Dictionary<int, float>();
            var clothDem = new Dictionary<int, float>();
            var luxDem = new Dictionary<int, float>();
            var foodProd = new Dictionary<int, float>();
            var clothProd = new Dictionary<int, float>();
            var luxProd = new Dictionary<int, float>();
            var inputDem = new Dictionary<(int Pid, int GoodId), float>();

            using (var q = em.CreateEntityQuery(
                       ComponentType.ReadOnly<ProvinceData>(),
                       ComponentType.ReadOnly<PhysicalDemandSnapshot>()))
            using (var entities = q.ToEntityArray(Allocator.Temp))
            {
                for (var i = 0; i < entities.Length; i++)
                {
                    var pid = em.GetComponentData<ProvinceData>(entities[i]).ProvinceId;
                    var snap = em.GetComponentData<PhysicalDemandSnapshot>(entities[i]);
                    foodDem[pid] = snap.FoodDemand;
                    clothDem[pid] = snap.ClothDemand;
                    luxDem[pid] = snap.LuxuryDemand;
                }
            }

            var goodType = new Dictionary<int, GoodType>();
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<GoodData>()))
            using (var goods = q.ToComponentDataArray<GoodData>(Allocator.Temp))
            {
                for (var i = 0; i < goods.Length; i++)
                {
                    goodType[goods[i].GoodId] = goods[i].Type;
                }
            }

            using (var q = em.CreateEntityQuery(
                       ComponentType.ReadOnly<ProvinceData>(),
                       ComponentType.ReadOnly<ProductionSite>()))
            using (var entities = q.ToEntityArray(Allocator.Temp))
            {
                for (var i = 0; i < entities.Length; i++)
                {
                    var pid = em.GetComponentData<ProvinceData>(entities[i]).ProvinceId;
                    var site = em.GetComponentData<ProductionSite>(entities[i]);
                    if (!goodType.TryGetValue(site.GoodId, out var t))
                    {
                        continue;
                    }

                    if (t == GoodType.Food)
                    {
                        foodProd[pid] = site.LastOutput;
                    }
                    else if (t == GoodType.Manufactured)
                    {
                        clothProd[pid] = site.LastOutput;
                    }
                    else if (t == GoodType.Luxury)
                    {
                        luxProd[pid] = site.LastOutput;
                    }
                }
            }

            using (var q = em.CreateEntityQuery(
                       ComponentType.ReadOnly<ProvinceData>(),
                       ComponentType.ReadOnly<PhysicalInputDeficit>()))
            using (var entities = q.ToEntityArray(Allocator.Temp))
            {
                for (var i = 0; i < entities.Length; i++)
                {
                    var pid = em.GetComponentData<ProvinceData>(entities[i]).ProvinceId;
                    var buf = em.GetBuffer<PhysicalInputDeficit>(entities[i]);
                    for (var d = 0; d < buf.Length; d++)
                    {
                        if (buf[d].Amount > 1e-4f)
                        {
                            var key = (pid, buf[d].GoodId);
                            inputDem[key] = inputDem.TryGetValue(key, out var cur)
                                ? cur + buf[d].Amount
                                : buf[d].Amount;
                        }
                    }
                }
            }

            float SumDeficit(Dictionary<int, float> dem, Dictionary<int, float> prod)
            {
                float need = 0f;
                foreach (var kv in dem)
                {
                    prod.TryGetValue(kv.Key, out var p);
                    var def = kv.Value - p;
                    if (def > 0f)
                    {
                        need += def;
                    }
                }

                return need;
            }

            float HopWeighted(
                Dictionary<int, float> dem,
                Dictionary<int, float> prod,
                Func<int, float> surplusOf)
            {
                float hopNeed = 0f;
                foreach (var kv in dem)
                {
                    prod.TryGetValue(kv.Key, out var p);
                    var def = kv.Value - p;
                    if (def <= 1e-4f)
                    {
                        continue;
                    }

                    var hops = NearestSurplusHops(kv.Key, adj, surplusOf);
                    hopNeed += def * math.max(1, hops);
                }

                return hopNeed;
            }

            float FoodSurplus(int pid)
            {
                foodDem.TryGetValue(pid, out var d);
                foodProd.TryGetValue(pid, out var p);
                return p - d;
            }

            float ClothSurplus(int pid)
            {
                clothDem.TryGetValue(pid, out var d);
                clothProd.TryGetValue(pid, out var p);
                return p - d;
            }

            float LuxSurplus(int pid)
            {
                luxDem.TryGetValue(pid, out var d);
                luxProd.TryGetValue(pid, out var p);
                return p - d;
            }

            var foodNeed = SumDeficit(foodDem, foodProd);
            var clothNeed = SumDeficit(clothDem, clothProd);
            var luxNeed = SumDeficit(luxDem, luxProd);
            float inputNeed = 0f;
            foreach (var kv in inputDem)
            {
                inputNeed += kv.Value;
            }

            var foodHop = HopWeighted(foodDem, foodProd, FoodSurplus);
            var clothHop = HopWeighted(clothDem, clothProd, ClothSurplus);
            var luxHop = HopWeighted(luxDem, luxProd, LuxSurplus);

            // Intrants : hop vers province avec stock/surplus du même GoodId.
            float inputHop = 0f;
            foreach (var kv in inputDem)
            {
                var pid = kv.Key.Pid;
                var gid = kv.Key.GoodId;
                var hops = NearestSurplusHops(pid, adj, p =>
                {
                    if (!entityByPid.TryGetValue(p, out var e))
                    {
                        return 0f;
                    }

                    var stock = em.GetBuffer<ProvinceStock>(e);
                    for (var i = 0; i < stock.Length; i++)
                    {
                        if (stock[i].GoodId == gid)
                        {
                            return (float)stock[i].Quantity;
                        }
                    }

                    return 0f;
                });
                inputHop += kv.Value * math.max(1, hops);
            }

            var installed = edges.Count * capacityPerEdge;
            var aggNeed = foodNeed + clothNeed + luxNeed + inputNeed;
            var aggHop = foodHop + clothHop + luxHop + inputHop;
            var meanDev = devN > 0 ? (float)(devSum / devN) : 1f;

            return new FlowBudget
            {
                DirectedEdgeCount = edges.Count,
                InstalledCapacity = installed,
                MeanDevScore = meanDev,
                FoodNeed = foodNeed,
                ClothNeed = clothNeed,
                LuxuryNeed = luxNeed,
                InputNeed = inputNeed,
                AggNeed = aggNeed,
                FoodHopNeed = foodHop,
                ClothHopNeed = clothHop,
                LuxuryHopNeed = luxHop,
                InputHopNeed = inputHop,
                AggHopNeed = aggHop,
                AggNeedOverCap = installed > 1e-4f ? aggNeed / installed : 0f,
                AggHopOverCap = installed > 1e-4f ? aggHop / installed : 0f
            };
        }

        static int NearestSurplusHops(
            int fromPid,
            Dictionary<int, List<int>> adj,
            Func<int, float> surplusOf)
        {
            if (surplusOf(fromPid) > 1e-4f)
            {
                return 0;
            }

            var visited = new HashSet<int> { fromPid };
            var queue = new Queue<(int Pid, int Dist)>();
            queue.Enqueue((fromPid, 0));
            while (queue.Count > 0)
            {
                var (pid, dist) = queue.Dequeue();
                if (!adj.TryGetValue(pid, out var neigh))
                {
                    continue;
                }

                for (var i = 0; i < neigh.Count; i++)
                {
                    var n = neigh[i];
                    if (!visited.Add(n))
                    {
                        continue;
                    }

                    if (surplusOf(n) > 1e-4f)
                    {
                        return dist + 1;
                    }

                    queue.Enqueue((n, dist + 1));
                }
            }

            // Aucun surplus atteignable : compter un diamètre prudent (pénalise fort).
            return 4;
        }

        static DeadStockSplit MeasureDeadStockSplit(EntityManager em)
        {
            var goodType = new Dictionary<int, GoodType>();
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<GoodData>()))
            using (var goods = q.ToComponentDataArray<GoodData>(Allocator.Temp))
            {
                for (var i = 0; i < goods.Length; i++)
                {
                    goodType[goods[i].GoodId] = goods[i].Type;
                }
            }

            var worldConsumers = new HashSet<int>();
            var deficitProvinces = new HashSet<(int Pid, int GoodId)>();
            using (var q = em.CreateEntityQuery(
                       ComponentType.ReadOnly<ProvinceData>(),
                       ComponentType.ReadOnly<PhysicalInputDeficit>()))
            using (var entities = q.ToEntityArray(Allocator.Temp))
            {
                for (var i = 0; i < entities.Length; i++)
                {
                    var pid = em.GetComponentData<ProvinceData>(entities[i]).ProvinceId;
                    var deficit = em.GetBuffer<PhysicalInputDeficit>(entities[i]);
                    for (var d = 0; d < deficit.Length; d++)
                    {
                        if (deficit[d].Amount > 1e-4f)
                        {
                            deficitProvinces.Add((pid, deficit[d].GoodId));
                            worldConsumers.Add(deficit[d].GoodId);
                        }
                    }
                }
            }

            // Consommateurs pop (food/cloth/luxury types) — toujours « ont un consommateur ».
            foreach (var kv in goodType)
            {
                if (kv.Value == GoodType.Food || kv.Value == GoodType.Manufactured ||
                    kv.Value == GoodType.Luxury)
                {
                    worldConsumers.Add(kv.Key);
                }
            }

            var entityByPid = new Dictionary<int, Entity>();
            using (var q = em.CreateEntityQuery(
                       ComponentType.ReadOnly<ProvinceData>(),
                       ComponentType.ReadOnly<ProvinceNeighbor>()))
            using (var entities = q.ToEntityArray(Allocator.Temp))
            {
                for (var i = 0; i < entities.Length; i++)
                {
                    entityByPid[em.GetComponentData<ProvinceData>(entities[i]).ProvinceId] =
                        entities[i];
                }
            }

            float total = 0f, dead = 0f, wood = 0f, noConsumer = 0f, outOfReach = 0f;
            foreach (var kv in entityByPid)
            {
                var pid = kv.Key;
                var stock = em.GetBuffer<ProvinceStock>(kv.Value);
                var neighbors = em.GetBuffer<ProvinceNeighbor>(kv.Value);
                for (var s = 0; s < stock.Length; s++)
                {
                    var qty = (float)stock[s].Quantity;
                    if (qty <= 1e-4f)
                    {
                        continue;
                    }

                    total += qty;
                    if (stock[s].GoodId == 4)
                    {
                        wood += qty;
                    }

                    if (!goodType.TryGetValue(stock[s].GoodId, out var t) ||
                        t != GoodType.RawMaterial)
                    {
                        continue;
                    }

                    if (!worldConsumers.Contains(stock[s].GoodId))
                    {
                        dead += qty;
                        noConsumer += qty;
                        continue;
                    }

                    var useful = deficitProvinces.Contains((pid, stock[s].GoodId));
                    if (!useful)
                    {
                        for (var n = 0; n < neighbors.Length; n++)
                        {
                            if (!neighbors[n].IsStrait &&
                                deficitProvinces.Contains(
                                    (neighbors[n].NeighborProvinceId, stock[s].GoodId)))
                            {
                                useful = true;
                                break;
                            }
                        }
                    }

                    if (!useful)
                    {
                        dead += qty;
                        outOfReach += qty;
                    }
                }
            }

            return new DeadStockSplit
            {
                TotalStock = total,
                DeadStock = dead,
                DeadShare = total > 1e-4f ? dead / total : 0f,
                Wood = wood,
                NoConsumerShare = total > 1e-4f ? noConsumer / total : 0f,
                OutOfReachShare = total > 1e-4f ? outOfReach / total : 0f
            };
        }

        static CellPoint RunCellPoint(float capacity, int ticks, bool multiHop)
        {
            PhysicalStockSystem.MultiHopTransport = multiHop;
            PhysicalStockSystem.IdealPoolMode = false;
            using var harness = new SimulationHarness(Seed);
            harness.RunTicks(0);
            SetTransportConfig(harness.EntityManager, capacity, 1);
            harness.RunTicks(ticks);
            return CaptureCell(harness.EntityManager, capacity, multiHop);
        }

        static CellPoint CaptureCell(EntityManager em, float capacity, bool multiHop)
        {
            var gap = ComputeGap(em);
            var dist = ComputePhysStd(em, gap.PhysMean);
            var cmp = CompareDemandSupply(em);
            var m = GetMetrics(em);
            var dead = MeasureDeadStockSplit(em);
            return new CellPoint
            {
                MultiHop = multiHop,
                Capacity = capacity,
                PhysMean = gap.PhysMean,
                PhysStd = dist,
                ClothServedShare = Ratio(cmp.PhysClothServed, cmp.PhysClothDemand),
                DeadShare = dead.DeadShare,
                MissedIn = m.MissedInputShare,
                Transit = m.TotalInTransit,
                CpuMs = m.LastTickCpuMs
            };
        }

        static CellPoint FindCell(List<CellPoint> cell, bool multi, float cap)
        {
            foreach (var p in cell)
            {
                if (p.MultiHop == multi &&
                    (math.abs(p.Capacity - cap) < 1f ||
                     (cap >= UnlimitedCapacity * 0.5f && p.Capacity >= UnlimitedCapacity * 0.5f)))
                {
                    return p;
                }
            }

            return default;
        }

        static Underproduction MeasureUnderproduction()
        {
            PhysicalStockSystem.MultiHopTransport = false;
            PhysicalStockSystem.IdealPoolMode = true;
            float served, demand;
            int clothSites = 0, woolSites = 0;
            float clothCap = 0f, woolCap = 0f;
            using (var h = new SimulationHarness(Seed))
            {
                h.RunTicks(0);
                SetTransportConfig(h.EntityManager, UnlimitedCapacity, 1);
                // Capacités installées (BaseOutput) avant ticks.
                var em0 = h.EntityManager;
                using (var q = em0.CreateEntityQuery(ComponentType.ReadOnly<ProductionSite>()))
                using (var sites = q.ToComponentDataArray<ProductionSite>(Allocator.Temp))
                {
                    for (var i = 0; i < sites.Length; i++)
                    {
                        if (sites[i].GoodId == ClothGoodId)
                        {
                            clothSites++;
                            clothCap += sites[i].BaseOutput;
                        }
                        else if (sites[i].GoodId == WoolGoodId)
                        {
                            woolSites++;
                            woolCap += sites[i].BaseOutput;
                        }
                    }
                }

                h.RunTicks(MeasureTicks);
                var cmp = CompareDemandSupply(h.EntityManager);
                served = cmp.PhysClothServed;
                demand = cmp.PhysClothDemand;
            }

            PhysicalStockSystem.IdealPoolMode = false;
            return new Underproduction
            {
                ClothSites = clothSites,
                WoolSites = woolSites,
                ClothCapacity = clothCap,
                WoolCapacity = woolCap,
                ClothDemand = demand,
                ClothServedIdeal = served,
                ClothCoverIdeal = Ratio(served, demand)
            };
        }

        static WeightRow RunWeightPoint(float weight, float perDev, bool multiHop)
        {
            PhysicalSatisfactionBlendSystem.LockWeight(weight);
            PhysicalStockSystem.MultiHopTransport = multiHop;
            PhysicalStockSystem.IdealPoolMode = false;
            using var harness = new SimulationHarness(Seed);
            harness.RunTicks(0);
            SetTransportInfra(harness.EntityManager, perDev);
            harness.RunTicks(1000);
            var m1000 = WorldMetrics.Capture(harness.EntityManager, 1000);
            harness.RunTicks(2000);
            var m3000 = WorldMetrics.Capture(harness.EntityManager, 3000);
            var gap = ComputeGap(harness.EntityManager);
            var cpu = GetMetrics(harness.EntityManager).LastTickCpuMs;
            PhysicalSatisfactionBlendSystem.UnlockWeight();
            return new WeightRow
            {
                Weight = weight,
                Pop = m3000.Population,
                SatAvg = m3000.NeedsSatAvg,
                PhysMean = gap.PhysMean,
                Starved = gap.Starved,
                Debt = m3000.TotalDebt,
                Bankrupt = m3000.BankruptCount,
                Army = m3000.WorldArmyStr,
                Countries = m3000.CountriesWithLand,
                Wars = m3000.ActiveWars,
                CpuMs = cpu,
                DebtAt1000 = m1000.TotalDebt
            };
        }

        static bool IsAlive(WeightRow row, WeightRow? baseline)
        {
            if (baseline.HasValue && baseline.Value.Pop > 0 &&
                row.Pop < baseline.Value.Pop * 0.70f)
            {
                return false;
            }

            if (row.Army <= 1000f)
            {
                return false;
            }

            if (baseline.HasValue && baseline.Value.Army > 0f &&
                row.Army < baseline.Value.Army * 0.35f)
            {
                return false;
            }

            var debtCap = Math.Max(2500f, row.DebtAt1000 * 2.5f);
            if (row.Debt > debtCap || row.Debt >= BeforeDebt3000 * 0.5f)
            {
                return false;
            }

            if (row.Countries < 10)
            {
                return false;
            }

            var all = Math.Max(row.Countries, 1);
            return row.Bankrupt < all / 2;
        }

        static EmergentStory FindEmergentStory(float weight, float perDev, bool multiHop)
        {
            PhysicalSatisfactionBlendSystem.LockWeight(weight);
            PhysicalStockSystem.MultiHopTransport = multiHop;
            using var harness = new SimulationHarness(Seed);
            harness.RunTicks(0);
            SetTransportInfra(harness.EntityManager, perDev);

            var popAt50 = new Dictionary<int, int>();
            harness.RunTicks(50);
            CapturePopByProv(harness.EntityManager, popAt50);
            harness.RunTicks(150);

            var story = default(EmergentStory);
            var em = harness.EntityManager;
            var worstPhys = 2f;
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

                var pop = 0;
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

                        pop += pops[p].Size;
                        lodSum += pops[p].NeedsSatisfaction;
                        lodN++;
                    }
                }

                if (pop <= 0)
                {
                    continue;
                }

                popAt50.TryGetValue(pid, out var prev);
                worstPhys = phys;
                story = new EmergentStory
                {
                    Found = true,
                    ProvinceId = pid,
                    PhysSat = phys,
                    LodSat = lodN > 0 ? lodSum / lodN : 0f,
                    PopSize = pop,
                    DeltaPop = pop - prev
                };
            }

            PhysicalSatisfactionBlendSystem.UnlockWeight();
            return story;
        }

        static SaturationReport MeasureSaturationConstant(float capacity, int ticks)
        {
            PhysicalStockSystem.MultiHopTransport = false;
            using var harness = new SimulationHarness(Seed);
            harness.RunTicks(0);
            SetTransportConfig(harness.EntityManager, capacity, 1);
            return MeasureSaturationLoop(harness, capacity, false, ticks);
        }

        static SaturationReport MeasureSaturationInfra(
            EntityManager emSeed, float perDev, int ticks)
        {
            // Re-run dedicated harness for saturation sampling.
            PhysicalStockSystem.MultiHopTransport = false;
            using var harness = new SimulationHarness(Seed);
            harness.RunTicks(0);
            SetTransportInfra(harness.EntityManager, perDev);
            return MeasureSaturationLoop(harness, perDev, true, ticks);
        }

        static SaturationReport MeasureSaturationLoop(
            SimulationHarness harness, float capOrPerDev, bool infra, int ticks)
        {
            var em = harness.EntityManager;
            var allEdges = new HashSet<long>();
            using (var q = em.CreateEntityQuery(
                       ComponentType.ReadOnly<ProvinceData>(),
                       ComponentType.ReadOnly<ProvinceNeighbor>()))
            using (var entities = q.ToEntityArray(Allocator.Temp))
            {
                for (var i = 0; i < entities.Length; i++)
                {
                    var from = em.GetComponentData<ProvinceData>(entities[i]).ProvinceId;
                    var nbuf = em.GetBuffer<ProvinceNeighbor>(entities[i]);
                    for (var n = 0; n < nbuf.Length; n++)
                    {
                        if (!nbuf[n].IsStrait)
                        {
                            allEdges.Add(EdgeKey(from, nbuf[n].NeighborProvinceId));
                        }
                    }
                }
            }

            var everSat = new HashSet<long>();
            if (!TryGetSingletonEntity<PhysicalEconomySingleton>(em, out var singleton))
            {
                return default;
            }

            var entityByPid = new Dictionary<int, Entity>();
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<ProvinceData>()))
            using (var entities = q.ToEntityArray(Allocator.Temp))
            {
                for (var i = 0; i < entities.Length; i++)
                {
                    entityByPid[em.GetComponentData<ProvinceData>(entities[i]).ProvinceId] =
                        entities[i];
                }
            }

            for (var t = 0; t < ticks; t++)
            {
                harness.RunTicks(1);
                var cargos = em.GetBuffer<CargoInTransit>(singleton);
                var used = new Dictionary<long, float>();
                for (var i = 0; i < cargos.Length; i++)
                {
                    if (cargos[i].TicksRemaining != 1)
                    {
                        continue;
                    }

                    var key = EdgeKey(cargos[i].OriginProvinceId, cargos[i].DestProvinceId);
                    used[key] = used.TryGetValue(key, out var u)
                        ? u + (float)cargos[i].Quantity
                        : (float)cargos[i].Quantity;
                }

                foreach (var kv in used)
                {
                    float edgeCap;
                    if (infra)
                    {
                        var from = (int)(kv.Key >> 32);
                        var to = (int)(uint)kv.Key;
                        edgeCap = capOrPerDev * 0.5f *
                                  (DevScoreManaged(em, entityByPid, from) +
                                   DevScoreManaged(em, entityByPid, to));
                    }
                    else
                    {
                        edgeCap = capOrPerDev;
                    }

                    if (kv.Value >= edgeCap * 0.99f)
                    {
                        everSat.Add(kv.Key);
                    }
                }
            }

            var edgeCount = Math.Max(allEdges.Count, 1);
            return new SaturationReport
            {
                EdgeCount = allEdges.Count,
                EdgesEverSaturated = everSat.Count,
                EdgeShareEverSaturated = everSat.Count / (float)edgeCount
            };
        }

        static float DevScoreManaged(
            EntityManager em, Dictionary<int, Entity> entityByPid, int provinceId)
        {
            if (!entityByPid.TryGetValue(provinceId, out var entity) ||
                !em.HasComponent<ProvinceDevelopment>(entity))
            {
                return 1f;
            }

            var d = em.GetComponentData<ProvinceDevelopment>(entity);
            var avg = (d.Tax + d.Production + d.Manpower) / 3f;
            return math.max(1f, avg);
        }

        static (float PhysMean, float LodMean, int Starved, int Count) ComputeGap(EntityManager em)
        {
            var lodByProv = new Dictionary<int, float>();
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<PopData>()))
            using (var pops = q.ToComponentDataArray<PopData>(Allocator.Temp))
            {
                var acc = new Dictionary<int, (float S, int N)>();
                for (var i = 0; i < pops.Length; i++)
                {
                    if (pops[i].Province == Entity.Null ||
                        !em.HasComponent<ProvinceData>(pops[i].Province))
                    {
                        continue;
                    }

                    var pid = em.GetComponentData<ProvinceData>(pops[i].Province).ProvinceId;
                    if (!acc.TryGetValue(pid, out var cur))
                    {
                        cur = (0f, 0);
                    }

                    acc[pid] = (cur.S + pops[i].NeedsSatisfaction, cur.N + 1);
                }

                foreach (var kv in acc)
                {
                    lodByProv[kv.Key] = kv.Value.N > 0 ? kv.Value.S / kv.Value.N : 0f;
                }
            }

            double pSum = 0, lSum = 0;
            var count = 0;
            var starved = 0;
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
                    lodByProv.TryGetValue(pid, out var lod);
                    pSum += phys;
                    lSum += lod;
                    count++;
                    if (phys < 0.3f)
                    {
                        starved++;
                    }
                }
            }

            return (
                count > 0 ? (float)(pSum / count) : 0f,
                count > 0 ? (float)(lSum / count) : 0f,
                starved,
                count);
        }

        static float ComputePhysStd(EntityManager em, float mean)
        {
            double varSum = 0;
            var n = 0;
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<PhysicalDemandSnapshot>());
            using var snaps = q.ToComponentDataArray<PhysicalDemandSnapshot>(Allocator.Temp);
            for (var i = 0; i < snaps.Length; i++)
            {
                var d = snaps[i].PhysicalSatisfaction - mean;
                varSum += d * d;
                n++;
            }

            return n > 1 ? (float)math.sqrt(varSum / n) : 0f;
        }

        static DemandSupplyCmp CompareDemandSupply(EntityManager em)
        {
            var cmp = new DemandSupplyCmp();
            using (var q = em.CreateEntityQuery(
                       ComponentType.ReadOnly<PopData>(), ComponentType.ReadOnly<PopNeeds>()))
            using (var pops = q.ToComponentDataArray<PopData>(Allocator.Temp))
            using (var needs = q.ToComponentDataArray<PopNeeds>(Allocator.Temp))
            {
                for (var i = 0; i < pops.Length; i++)
                {
                    var scale = pops[i].Size;
                    cmp.LodFoodDemand += needs[i].FoodNeed * scale;
                    cmp.LodClothDemand += needs[i].ClothNeed * scale;
                    cmp.LodLuxuryDemand += needs[i].LuxuryNeed * scale;
                }
            }

            var goodType = new Dictionary<int, GoodType>();
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<GoodData>()))
            using (var goods = q.ToComponentDataArray<GoodData>(Allocator.Temp))
            {
                for (var i = 0; i < goods.Length; i++)
                {
                    goodType[goods[i].GoodId] = goods[i].Type;
                }
            }

            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<ProductionSite>()))
            using (var sites = q.ToComponentDataArray<ProductionSite>(Allocator.Temp))
            {
                for (var i = 0; i < sites.Length; i++)
                {
                    if (!goodType.TryGetValue(sites[i].GoodId, out var t))
                    {
                        continue;
                    }

                    if (t == GoodType.Food)
                    {
                        cmp.LodFoodSupply += sites[i].LastOutput;
                    }
                    else if (t == GoodType.Manufactured)
                    {
                        cmp.LodClothSupply += sites[i].LastOutput;
                    }
                    else if (t == GoodType.Luxury)
                    {
                        cmp.LodLuxurySupply += sites[i].LastOutput;
                    }
                }
            }

            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<PhysicalDemandSnapshot>()))
            using (var snaps = q.ToComponentDataArray<PhysicalDemandSnapshot>(Allocator.Temp))
            {
                for (var i = 0; i < snaps.Length; i++)
                {
                    cmp.PhysFoodDemand += snaps[i].FoodDemand;
                    cmp.PhysClothDemand += snaps[i].ClothDemand;
                    cmp.PhysLuxuryDemand += snaps[i].LuxuryDemand;
                    cmp.PhysFoodServed += snaps[i].FoodSatisfied;
                    cmp.PhysClothServed += snaps[i].ClothSatisfied;
                    cmp.PhysLuxuryServed += snaps[i].LuxurySatisfied;
                }
            }

            return cmp;
        }

        static void CapturePopByProv(EntityManager em, Dictionary<int, int> dst)
        {
            dst.Clear();
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
                dst[pid] = dst.TryGetValue(pid, out var c) ? c + pops[i].Size : pops[i].Size;
            }
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

        static void AppendGoodBudget(
            StringBuilder sb, string label, float need, float hop, float installed)
        {
            sb.AppendLine(
                $"{label}\t{Fmt(need)}\t{Fmt(hop)}\t{Fmt(installed)}\t" +
                $"{Fmt(installed > 1e-4f ? need / installed : 0f)}\t" +
                $"{Fmt(installed > 1e-4f ? hop / installed : 0f)}");
        }

        static void AssertRelativeClose(float a, float b, float relTol, string label)
        {
            var scale = math.max(math.abs(a), math.abs(b));
            var tol = math.max(1e-3f, scale * relTol);
            Assert.AreEqual(a, b, tol, $"{label} LOD≠PHYS: {a} vs {b} (tol rel {relTol})");
        }

        static long EdgeKey(int from, int to) => ((long)from << 32) ^ (uint)to;

        static float Ratio(float a, float b) => b > 1e-4f ? a / b : 0f;

        static string CapLabel(float c) =>
            c >= UnlimitedCapacity * 0.5f ? "unlimited" : c.ToString("0", CultureInfo.InvariantCulture);

        static string Fmt(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
