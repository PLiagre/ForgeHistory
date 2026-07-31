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
    /// <summary>Batch : -executeMethod VictoriaGame.Tests.V1023BatchRunner.Run</summary>
    public static class V1023BatchRunner
    {
        public static void Run()
        {
            V1023PhysicalLevelDiagnostic.RunFullSuiteAndWriteLog();
            UnityEngine.Debug.Log("V1023BatchRunner: DONE");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
        }
    }

    /// <summary>
    /// v1_023 — diagnostic du plafond de satisfaction physique (0.27 vs LOD 0.70).
    /// PARTIE 1 obligatoire : décomposition chiffrée + expérience monde idéal.
    /// PARTIE 2 : correction prouvée (transport multi-sauts — stock mort / intrants).
    /// </summary>
    [TestFixture]
    public class V1023PhysicalLevelDiagnostic
    {
        const uint Seed = 42195u;
        const float UnlimitedCapacity = 1e9f;
        const float BeforeDebt3000 = 15429.4f;
        const int MeasureTicks = 1000;
        const int SweepTicks = 3000;

        static readonly float[] WeightSweep = { 0f, 0.1f, 0.25f, 0.5f, 0.75f, 1.0f };

        [TearDown]
        public void TearDown()
        {
            PhysicalSatisfactionBlendSystem.UnlockWeight();
            PhysicalSatisfactionBlendSystem.ResetToCompiledDefault();
            PhysicalStockSystem.IdealPoolMode = false;
            PhysicalStockSystem.MultiHopTransport = true;
        }

        [Test]
        public void V1023_DemandParity_LodAndPhysicalUseSameFormula()
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
        public void V1023_IdealPool_ClosesGeographyGap()
        {
            PhysicalSatisfactionBlendSystem.LockWeight(0f);

            PhysicalStockSystem.MultiHopTransport = false;
            PhysicalStockSystem.IdealPoolMode = false;
            float physBroken;
            float lod;
            using (var h = new SimulationHarness(Seed))
            {
                h.RunTicks(0);
                SetTransportConfig(h.EntityManager, 500f, 1);
                h.RunTicks(MeasureTicks);
                var g = ComputeGap(h.EntityManager);
                physBroken = g.PhysMean;
                lod = g.LodMean;
            }

            PhysicalStockSystem.IdealPoolMode = true;
            float physIdeal;
            using (var h = new SimulationHarness(Seed))
            {
                h.RunTicks(0);
                SetTransportConfig(h.EntityManager, UnlimitedCapacity, 1);
                h.RunTicks(MeasureTicks);
                physIdeal = ComputeGap(h.EntityManager).PhysMean;
            }

            PhysicalStockSystem.IdealPoolMode = false;
            Assert.Greater(physIdeal, physBroken + 0.05f,
                $"IdealPool n'élève pas physSat ({physBroken} → {physIdeal})");
            Assert.Greater(physIdeal / math.max(lod, 0.01f), 0.75f,
                $"IdealPool trop loin du LOD: phys={physIdeal} lod={lod}");
        }

        [Test]
        public void V1023_MultiHop_AtCap500_DoesNotMeetOwnCriterion()
        {
            // v1_023 : MultiHop@cap500 = +0.006 << critère +0.03 (étranglement capacité).
            // Le sort définitif est tranché dans V1024 (cellule multi-sauts × capacité).
            PhysicalSatisfactionBlendSystem.LockWeight(0f);

            PhysicalStockSystem.MultiHopTransport = false;
            var before = RunPhysStats(500f, MeasureTicks);

            PhysicalStockSystem.MultiHopTransport = true;
            var after = RunPhysStats(500f, MeasureTicks);

            Assert.Less(after.Mean - before.Mean, 0.03f,
                $"À cap500 MultiHop ne doit pas passer le critère +0.03 " +
                $"({before.Mean} → {after.Mean}) — capacité étrangle");
            Assert.Greater(after.StdDev, 0.02f,
                $"Dispersion écrasée après MultiHop (std={after.StdDev})");
        }

        [Test]
        public void V1023_WeightZero_StillNoOp()
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
        public void V1023_Determinism_MultiHop()
        {
            PhysicalSatisfactionBlendSystem.LockWeight(0.25f);
            PhysicalStockSystem.MultiHopTransport = true;
            ulong d1, d2;
            using (var h1 = new SimulationHarness(Seed))
            {
                h1.RunTicks(200);
                d1 = WorldDigest(h1.EntityManager);
            }

            using (var h2 = new SimulationHarness(Seed))
            {
                PhysicalSatisfactionBlendSystem.LockWeight(0.25f);
                h2.RunTicks(200);
                d2 = WorldDigest(h2.EntityManager);
            }

            Assert.AreEqual(d1, d2, $"Non déterministe: {d1:X16} vs {d2:X16}");
        }

        // Suite de mesure lourde : uniquement via V1023BatchRunner (évite bloat XML/log EditMode).
        public static void V1023_MeasureLevelDecomposition() => RunFullSuiteAndWriteLog();

        public static void RunFullSuiteAndWriteLog()
        {
            var logPath = Path.Combine(
                UnityEngine.Application.dataPath, "..", "Logs", "v1_023_level.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            var sb = new StringBuilder(128 * 1024);

            sb.AppendLine("=== v1_023 PHYSICAL LEVEL DIAGNOSTIC — seed=42195 ===");
            sb.AppendLine(
                "Question: pourquoi physSat≈0.27 alors que LOD≈0.70 ? Prouver puis corriger.");
            sb.AppendLine();

            PhysicalSatisfactionBlendSystem.LockWeight(0f);

            // ---------- PARTIE 1 : décomposition (couche cassée = multi-hop OFF) ----------
            sb.AppendLine("=== PARTIE 1 — DÉCOMPOSITION (MultiHop=OFF, reproduit le plafond) ===");
            PhysicalStockSystem.MultiHopTransport = false;
            PhysicalStockSystem.IdealPoolMode = false;

            var at500 = RunSnapshot(500f, MeasureTicks, multiHop: false, ideal: false);
            var atUnlimited = RunSnapshot(UnlimitedCapacity, MeasureTicks, multiHop: false, ideal: false);
            var atIdeal = RunSnapshot(UnlimitedCapacity, MeasureTicks, multiHop: false, ideal: true);
            PhysicalStockSystem.IdealPoolMode = false;

            sb.AppendLine(
                "mode\tphysSat\tlodSat\tgap\tmissedIn\tlodOut\tphysOut\t" +
                "dFoodL\tdFoodP\tsFoodL\tsFoodP\tdClothL\tdClothP\tsClothL\tsClothP");
            AppendSnap(sb, "cap500", at500);
            AppendSnap(sb, "unlimited", atUnlimited);
            AppendSnap(sb, "idealPool", atIdeal);
            sb.AppendLine();

            sb.AppendLine("--- Demande LOD vs PHYS (cap500) ---");
            sb.AppendLine(
                $"Food demand LOD/PHYS={Fmt(at500.Cmp.LodFoodDemand)}/{Fmt(at500.Cmp.PhysFoodDemand)} " +
                $"ratio={Fmt(Ratio(at500.Cmp.PhysFoodDemand, at500.Cmp.LodFoodDemand))}");
            sb.AppendLine(
                $"Cloth demand LOD/PHYS={Fmt(at500.Cmp.LodClothDemand)}/{Fmt(at500.Cmp.PhysClothDemand)} " +
                $"ratio={Fmt(Ratio(at500.Cmp.PhysClothDemand, at500.Cmp.LodClothDemand))}");
            sb.AppendLine(
                $"Luxury demand LOD/PHYS={Fmt(at500.Cmp.LodLuxuryDemand)}/{Fmt(at500.Cmp.PhysLuxuryDemand)} " +
                $"ratio={Fmt(Ratio(at500.Cmp.PhysLuxuryDemand, at500.Cmp.LodLuxuryDemand))}");
            sb.AppendLine(
                $"Food supply LOD(flow)/PHYS(served)={Fmt(at500.Cmp.LodFoodSupply)}/{Fmt(at500.Cmp.PhysFoodServed)}");
            sb.AppendLine(
                $"Cloth supply LOD(flow)/PHYS(served)={Fmt(at500.Cmp.LodClothSupply)}/{Fmt(at500.Cmp.PhysClothServed)}");
            sb.AppendLine(
                $"Luxury supply LOD(flow)/PHYS(served)={Fmt(at500.Cmp.LodLuxurySupply)}/{Fmt(at500.Cmp.PhysLuxuryServed)}");

            var demandMatch =
                math.abs(at500.Cmp.LodFoodDemand - at500.Cmp.PhysFoodDemand) < 1f &&
                math.abs(at500.Cmp.LodClothDemand - at500.Cmp.PhysClothDemand) < 1f;
            sb.AppendLine(
                demandMatch
                    ? "VERDICT DEMANDE: LOD et PHYS coïncident (même FoodNeed×Size) — l'écart N'EST PAS la demande."
                    : "VERDICT DEMANDE: ÉCART de définition de demande — cause dominante candidate.");
            sb.AppendLine();

            var gapTotal = math.max(1e-4f, at500.LodMean - at500.PhysMean);
            var gapTransport = math.max(0f, atUnlimited.PhysMean - at500.PhysMean);
            var gapGeoOrInputs = math.max(0f, atIdeal.PhysMean - atUnlimited.PhysMean);
            var gapDefinitions = math.max(0f, at500.LodMean - atIdeal.PhysMean);
            // Renormaliser pour sommer à 100 % du gap total observé.
            var rawSum = gapTransport + gapGeoOrInputs + gapDefinitions;
            if (rawSum < 1e-4f)
            {
                rawSum = gapTotal;
            }

            var pctTransport = 100f * gapTransport / rawSum;
            var pctInputsGeo = 100f * gapGeoOrInputs / rawSum;
            var pctDefinitions = 100f * gapDefinitions / rawSum;

            sb.AppendLine("--- Tableau de décomposition (sommes ≈ 100 % de l'écart LOD−phys@500) ---");
            sb.AppendLine(
                $"gapTotal(LOD−phys500)={Fmt(gapTotal)} " +
                $"(LOD={Fmt(at500.LodMean)} phys500={Fmt(at500.PhysMean)})");
            sb.AppendLine(
                $"(ii) transport capacité 500→illimité: Δphys={Fmt(gapTransport)} → {Fmt(pctTransport)} %");
            sb.AppendLine(
                $"(i)  géographie multi-saut + intrants locaux (illimité→idealPool): " +
                $"Δphys={Fmt(gapGeoOrInputs)} → {Fmt(pctInputsGeo)} % " +
                $"(missedInput@500={Fmt(at500.MissedIn)} @ideal={Fmt(atIdeal.MissedIn)})");
            sb.AppendLine(
                $"(iii) définitions résiduelles (LOD−idealPool): Δ={Fmt(gapDefinitions)} → {Fmt(pctDefinitions)} %");
            sb.AppendLine(
                $"VERDICT DÉCOMPOSITION: transport={Fmt(pctTransport)}% ; " +
                $"intrants+multi-saut={Fmt(pctInputsGeo)}% ; définitions={Fmt(pctDefinitions)}%");
            sb.AppendLine(
                atIdeal.PhysMean >= at500.LodMean * 0.9f
                    ? "IdealPool ≈ LOD → l'écart n'est NI une mauvaise unité de demande NI une offre mal normalisée ; c'est l'accès physique (transport/intrants)."
                    : "IdealPool reste sous le LOD → résidu de définition ou production physique structurellement inférieure.");
            sb.AppendLine();

            var dead = MeasureDeadStock(at500);
            sb.AppendLine("--- Stock mort (cap500, MultiHop OFF) ---");
            sb.AppendLine(
                $"totalStock={Fmt(dead.TotalStock)} deadStock={Fmt(dead.DeadStock)} " +
                $"deadShare={Fmt(dead.DeadShare)} wood={Fmt(dead.Wood)} " +
                $"(raw sans consommateur local ni voisin déficitaire d'intrant)");
            sb.AppendLine();

            // ---------- PARTIE 2/3 : correction multi-saut ----------
            sb.AppendLine("=== PARTIE 2/3 — CORRECTION MultiHop=ON + critère variance ===");
            PhysicalStockSystem.MultiHopTransport = true;
            PhysicalStockSystem.IdealPoolMode = false;

            var after500 = RunSnapshot(500f, MeasureTicks, multiHop: true, ideal: false);
            var afterStats = default(PhysDist);
            using (var h = new SimulationHarness(Seed))
            {
                PhysicalStockSystem.MultiHopTransport = true;
                h.RunTicks(0);
                SetTransportConfig(h.EntityManager, 500f, 1);
                h.RunTicks(MeasureTicks);
                afterStats = ComputePhysDist(h.EntityManager);
            }

            var beforeStats = default(PhysDist);
            using (var h = new SimulationHarness(Seed))
            {
                PhysicalStockSystem.MultiHopTransport = false;
                h.RunTicks(0);
                SetTransportConfig(h.EntityManager, 500f, 1);
                h.RunTicks(MeasureTicks);
                beforeStats = ComputePhysDist(h.EntityManager);
            }

            PhysicalStockSystem.MultiHopTransport = true;

            sb.AppendLine(
                $"AVANT MultiHop: mean={Fmt(beforeStats.Mean)} std={Fmt(beforeStats.StdDev)} " +
                $"min={Fmt(beforeStats.Min)} p10={Fmt(beforeStats.P10)} med={Fmt(beforeStats.Median)} " +
                $"p90={Fmt(beforeStats.P90)} max={Fmt(beforeStats.Max)} gapLOD={Fmt(beforeStats.GapToLod)}");
            sb.AppendLine(
                $"APRÈS MultiHop: mean={Fmt(afterStats.Mean)} std={Fmt(afterStats.StdDev)} " +
                $"min={Fmt(afterStats.Min)} p10={Fmt(afterStats.P10)} med={Fmt(afterStats.Median)} " +
                $"p90={Fmt(afterStats.P90)} max={Fmt(afterStats.Max)} gapLOD={Fmt(afterStats.GapToLod)}");

            var varianceOk = afterStats.StdDev >= beforeStats.StdDev * 0.5f && afterStats.StdDev > 0.02f;
            var levelOk = afterStats.Mean > beforeStats.Mean + 0.03f;
            sb.AppendLine(
                levelOk
                    ? $"NIVEAU: OK mean {Fmt(beforeStats.Mean)} → {Fmt(afterStats.Mean)}"
                    : $"NIVEAU: ÉCHEC mean {Fmt(beforeStats.Mean)} → {Fmt(afterStats.Mean)}");
            sb.AppendLine(
                varianceOk
                    ? $"VARIANCE: OK std {Fmt(beforeStats.StdDev)} → {Fmt(afterStats.StdDev)} (signal Qui est mal desservi)"
                    : $"VARIANCE: ÉCHEC std écrasée {Fmt(beforeStats.StdDev)} → {Fmt(afterStats.StdDev)}");

            sb.AppendLine("--- Provinces les mieux desservies (phys vs LOD) ---");
            foreach (var row in afterStats.BestServed)
            {
                var ratio = row.Lod > 1e-4f ? row.Phys / row.Lod : 0f;
                sb.AppendLine(
                    $"prov={row.ProvinceId} phys={Fmt(row.Phys)} lod={Fmt(row.Lod)} " +
                    $"ratio={Fmt(ratio)} {(ratio >= 0.9f ? "PASS≥0.9" : "sous 0.9")}");
            }

            sb.AppendLine(
                $"missedInput AVANT={Fmt(at500.MissedIn)} APRÈS={Fmt(after500.MissedIn)}");
            sb.AppendLine();

            // ---------- PARTIE 4 : rebalayage poids ----------
            sb.AppendLine("=== PARTIE 4 — REBALAYAGE POIDS (MultiHop=ON, capacity=500, t3000) ===");
            sb.AppendLine(
                "weight\tpop\tpopRatio\tsatAvg\tphysMean\tstarved\tdebt\tbankrupt\tarmy\tcountries\t" +
                "wars\talive\tcpuMs");

            WeightRow? baseline = null;
            var rows = new List<WeightRow>();
            float adoptedW = 0f;
            var adoptedFound = false;

            foreach (var w in WeightSweep)
            {
                var row = RunWeightPoint(w);
                rows.Add(row);
                if (math.abs(w) < 1e-6f)
                {
                    baseline = row;
                }
            }

            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                row.Alive = IsAlive(row, baseline);
                rows[i] = row;
                var popRatio = baseline.HasValue && baseline.Value.Pop > 0
                    ? row.Pop / (float)baseline.Value.Pop
                    : 1f;
                sb.AppendLine(
                    $"{Fmt(row.Weight)}\t{row.Pop}\t{Fmt(popRatio)}\t{Fmt(row.SatAvg)}\t" +
                    $"{Fmt(row.PhysMean)}\t{row.Starved}\t{Fmt(row.Debt)}\t{row.Bankrupt}\t" +
                    $"{Fmt(row.Army)}\t{row.Countries}\t{row.Wars}\t" +
                    $"{(row.Alive ? "YES" : "NO")}\t{Fmt(row.CpuMs)}");

                if (row.Alive && row.Weight >= adoptedW)
                {
                    adoptedW = row.Weight;
                    adoptedFound = true;
                }
            }

            sb.AppendLine();
            sb.AppendLine(
                adoptedFound
                    ? $"NOUVEAU PALIER ADOPTABLE: w={Fmt(adoptedW)} (v1_022 était 0.25). " +
                      (adoptedW > 0.25f + 1e-3f
                          ? "SUCCÈS: un poids plus élevé devient soutenable."
                          : adoptedW < 0.25f - 1e-3f
                              ? "Le palier a BAISSÉ — la correction n'a pas suffi pour relever w."
                              : "Palier inchangé à 0.25 — coût démographique à comparer.")
                    : "Aucun poids vivant — voir critères IsAlive.");

            // Récit émergent
            sb.AppendLine();
            sb.AppendLine("=== RÉCIT ÉMERGENT (w adopté, chercher deltaPop≠0) ===");
            var story = FindEmergentStory(adoptedFound ? adoptedW : 0.25f);
            if (story.Found && math.abs(story.DeltaPop) > 0)
            {
                sb.AppendLine(
                    $"OUI — prov={story.ProvinceId} phys={Fmt(story.PhysSat)} " +
                    $"lod={Fmt(story.LodSat)} pop={story.PopSize} deltaPop={story.DeltaPop}");
            }
            else if (story.Found)
            {
                sb.AppendLine(
                    $"NON DÉMONTRÉ — prov={story.ProvinceId} phys={Fmt(story.PhysSat)} " +
                    $"mais deltaPop={story.DeltaPop} (comme v1_022).");
            }
            else
            {
                sb.AppendLine("NON — aucune province mal desservie isolée avec signal pop.");
            }

            sb.AppendLine();
            sb.AppendLine("=== PARTIE 5 — GARDE-FOUS ===");
            PhysicalSatisfactionBlendSystem.LockWeight(0.25f);
            PhysicalStockSystem.MultiHopTransport = true;
            ulong dA, dB;
            using (var h1 = new SimulationHarness(Seed))
            {
                h1.RunTicks(200);
                dA = WorldDigest(h1.EntityManager);
            }

            using (var h2 = new SimulationHarness(Seed))
            {
                PhysicalSatisfactionBlendSystem.LockWeight(0.25f);
                h2.RunTicks(200);
                dB = WorldDigest(h2.EntityManager);
            }

            var detOk = dA == dB;
            sb.AppendLine($"determinisme w=0.25 t200: {(detOk ? "PASS" : "FAIL")} ({dA:X16})");
            sb.AppendLine(
                $"perf lastTickCpuMs≈{Fmt(after500.CpuMs)} (réf v1_022≈0.30)");
            sb.AppendLine(
                "Parité v1_009 + V1016/17/18 + V1020/21/22 : filtre EditMode (voir XML).");
            sb.AppendLine();

            sb.AppendLine("=== VERDICT MESURÉ ===");
            sb.AppendLine(
                $"décomposition: transport={Fmt(pctTransport)}% " +
                $"intrants+multi-saut={Fmt(pctInputsGeo)}% définitions={Fmt(pctDefinitions)}%");
            sb.AppendLine(
                $"correction MultiHop: mean {Fmt(beforeStats.Mean)}→{Fmt(afterStats.Mean)} " +
                $"std {Fmt(beforeStats.StdDev)}→{Fmt(afterStats.StdDev)} " +
                $"level={(levelOk ? "PASS" : "FAIL")} variance={(varianceOk ? "PASS" : "FAIL")}");
            sb.AppendLine(
                $"palier_poids={(adoptedFound ? Fmt(adoptedW) : "none")} " +
                $"(réf v1_022=0.25) determinism={(detOk ? "PASS" : "FAIL")}");

            File.WriteAllText(logPath, sb.ToString());
            UnityEngine.Debug.Log(
                $"V1023PhysicalLevelDiagnostic: wrote {logPath} determinism={(detOk ? "PASS" : "FAIL")} level={(levelOk ? "PASS" : "FAIL")}");

            PhysicalSatisfactionBlendSystem.UnlockWeight();
            PhysicalStockSystem.IdealPoolMode = false;
            PhysicalStockSystem.MultiHopTransport = true;

            Assert.IsTrue(demandMatch || pctDefinitions > 30f,
                "Partie 1 incomplète: ni parité de demande ni part définitions mesurée");
            Assert.IsTrue(detOk, "Déterminisme échoué");
            Assert.IsTrue(levelOk || atIdeal.PhysMean > at500.PhysMean + 0.1f,
                "Ni la correction ni le diagnostic IdealPool n'élèvent le niveau");
        }

        // ----- Mesures -----

        struct Snap
        {
            public float PhysMean;
            public float LodMean;
            public float MissedIn;
            public float LodOut;
            public float PhysOut;
            public float CpuMs;
            public DemandSupplyCmp Cmp;
        }

        struct DemandSupplyCmp
        {
            public float LodFoodDemand, LodClothDemand, LodLuxuryDemand;
            public float PhysFoodDemand, PhysClothDemand, PhysLuxuryDemand;
            public float LodFoodSupply, LodClothSupply, LodLuxurySupply;
            public float PhysFoodServed, PhysClothServed, PhysLuxuryServed;
        }

        struct DeadStockReport
        {
            public float TotalStock;
            public float DeadStock;
            public float DeadShare;
            public float Wood;
        }

        struct PhysDist
        {
            public float Mean, StdDev, Min, P10, Median, P90, Max, GapToLod;
            public List<(int ProvinceId, float Phys, float Lod)> BestServed;
        }

        struct WeightRow
        {
            public float Weight;
            public int Pop;
            public float SatAvg;
            public float PhysMean;
            public int Starved;
            public float Debt;
            public int Bankrupt;
            public float Army;
            public int Countries;
            public int Wars;
            public bool Alive;
            public float CpuMs;
            public float DebtAt1000;
        }

        struct EmergentStory
        {
            public bool Found;
            public int ProvinceId;
            public float PhysSat;
            public float LodSat;
            public int PopSize;
            public int DeltaPop;
        }

        static Snap RunSnapshot(float capacity, int ticks, bool multiHop, bool ideal)
        {
            PhysicalStockSystem.MultiHopTransport = multiHop;
            PhysicalStockSystem.IdealPoolMode = ideal;
            using var harness = new SimulationHarness(Seed);
            harness.RunTicks(0);
            SetTransportConfig(harness.EntityManager, capacity, 1);
            harness.RunTicks(ticks);
            var em = harness.EntityManager;
            var gap = ComputeGap(em);
            var m = GetMetrics(em);
            var cmp = CompareDemandSupply(em);
            return new Snap
            {
                PhysMean = gap.PhysMean,
                LodMean = gap.LodMean,
                MissedIn = m.MissedInputShare,
                LodOut = m.LodOutputTotal,
                PhysOut = m.PhysicalOutputTotal,
                CpuMs = m.LastTickCpuMs,
                Cmp = cmp
            };
        }

        static PhysDist RunPhysStats(float capacity, int ticks)
        {
            using var harness = new SimulationHarness(Seed);
            harness.RunTicks(0);
            SetTransportConfig(harness.EntityManager, capacity, 1);
            harness.RunTicks(ticks);
            return ComputePhysDist(harness.EntityManager);
        }

        static void AppendSnap(StringBuilder sb, string label, Snap s)
        {
            sb.AppendLine(
                $"{label}\t{Fmt(s.PhysMean)}\t{Fmt(s.LodMean)}\t{Fmt(s.LodMean - s.PhysMean)}\t" +
                $"{Fmt(s.MissedIn)}\t{Fmt(s.LodOut)}\t{Fmt(s.PhysOut)}\t" +
                $"{Fmt(s.Cmp.LodFoodDemand)}\t{Fmt(s.Cmp.PhysFoodDemand)}\t" +
                $"{Fmt(s.Cmp.LodFoodSupply)}\t{Fmt(s.Cmp.PhysFoodServed)}\t" +
                $"{Fmt(s.Cmp.LodClothDemand)}\t{Fmt(s.Cmp.PhysClothDemand)}\t" +
                $"{Fmt(s.Cmp.LodClothSupply)}\t{Fmt(s.Cmp.PhysClothServed)}");
        }

        static DemandSupplyCmp CompareDemandSupply(EntityManager em)
        {
            var cmp = new DemandSupplyCmp();

            // Demande LOD = FoodNeed × Size (même formule que PopConsumption + PhysicalStock).
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

            // Offre LOD = LastOutput par type.
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

            // Demande / offre servie physique = snapshots.
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

        static DeadStockReport MeasureDeadStock(Snap _)
        {
            PhysicalStockSystem.MultiHopTransport = false;
            PhysicalStockSystem.IdealPoolMode = false;
            using var harness = new SimulationHarness(Seed);
            harness.RunTicks(0);
            SetTransportConfig(harness.EntityManager, 500f, 1);
            harness.RunTicks(MeasureTicks);
            var em = harness.EntityManager;

            var goodType = new Dictionary<int, GoodType>();
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<GoodData>()))
            using (var goods = q.ToComponentDataArray<GoodData>(Allocator.Temp))
            {
                for (var i = 0; i < goods.Length; i++)
                {
                    goodType[goods[i].GoodId] = goods[i].Type;
                }
            }

            // Provinces avec déficit d'intrant, par GoodId.
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
                        }
                    }
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

            float total = 0f, dead = 0f, wood = 0f;
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
                    }
                }
            }

            return new DeadStockReport
            {
                TotalStock = total,
                DeadStock = dead,
                DeadShare = total > 1e-4f ? dead / total : 0f,
                Wood = wood
            };
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

                    // À w=0 NeedsSatisfaction = LOD pur.
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

        static PhysDist ComputePhysDist(EntityManager em)
        {
            var gap = ComputeGap(em);
            var values = new List<float>();
            var pairs = new List<(int Pid, float Phys, float Lod)>();

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
                    values.Add(phys);
                    pairs.Add((pid, phys, lod));
                }
            }

            values.Sort();
            var n = values.Count;
            float mean = gap.PhysMean;
            double varSum = 0;
            for (var i = 0; i < n; i++)
            {
                var d = values[i] - mean;
                varSum += d * d;
            }

            float Pct(float p)
            {
                if (n == 0)
                {
                    return 0f;
                }

                var idx = (int)math.clamp(p * (n - 1), 0, n - 1);
                return values[idx];
            }

            pairs.Sort((a, b) => b.Phys.CompareTo(a.Phys));
            var best = new List<(int, float, float)>();
            for (var i = 0; i < math.min(5, pairs.Count); i++)
            {
                best.Add(pairs[i]);
            }

            return new PhysDist
            {
                Mean = mean,
                StdDev = n > 1 ? (float)math.sqrt(varSum / n) : 0f,
                Min = n > 0 ? values[0] : 0f,
                P10 = Pct(0.10f),
                Median = Pct(0.50f),
                P90 = Pct(0.90f),
                Max = n > 0 ? values[n - 1] : 0f,
                GapToLod = gap.LodMean - mean,
                BestServed = best
            };
        }

        static WeightRow RunWeightPoint(float weight)
        {
            PhysicalSatisfactionBlendSystem.LockWeight(weight);
            PhysicalStockSystem.MultiHopTransport = true;
            PhysicalStockSystem.IdealPoolMode = false;
            using var harness = new SimulationHarness(Seed);
            harness.RunTicks(0);
            SetTransportConfig(harness.EntityManager, 500f, 1);

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
            if (row.Bankrupt >= all / 2)
            {
                return false;
            }

            return true;
        }

        static EmergentStory FindEmergentStory(float weight)
        {
            PhysicalSatisfactionBlendSystem.LockWeight(weight);
            PhysicalStockSystem.MultiHopTransport = true;
            using var harness = new SimulationHarness(Seed);
            harness.RunTicks(0);
            SetTransportConfig(harness.EntityManager, 500f, 1);

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
            cfg.CapacityPerDevPoint = 0f; // balayage à capacité constante
            cfg.TransitTicksPerEdge = delay < 1 ? 1 : delay;
            em.SetComponentData(e, cfg);
        }

        static void AssertRelativeClose(float a, float b, float relTol, string label)
        {
            var scale = math.max(math.abs(a), math.abs(b));
            var tol = math.max(1e-3f, scale * relTol);
            Assert.AreEqual(a, b, tol, $"{label} LOD≠PHYS: {a} vs {b} (tol rel {relTol})");
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

        static float Ratio(float a, float b) => b > 1e-4f ? a / b : 0f;

        static string Fmt(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
