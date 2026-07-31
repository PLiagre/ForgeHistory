using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Collections;
using Unity.Entities;
using NUnit.Framework;
using VictoriaGame.Core;
using VictoriaGame.Economy;
using VictoriaGame.Military;
using VictoriaGame.Population;
using VictoriaGame.World;

namespace VictoriaGame.Tests
{
    /// <summary>Point d'entrée batchmode : -executeMethod VictoriaGame.Tests.Eco028BatchRunner.Run</summary>
    public static class Eco028BatchRunner
    {
        public static void Run()
        {
            Eco028MeasurementTests.RunMeasurementsAndWriteLog();
        }
    }

    [TestFixture]
    public class Eco028MeasurementTests
    {
        const uint Seed = 42195u;
        static readonly int[] SnapshotTicks = { 50, 200, 500, 1000 };

        static readonly (int Id, string Label)[] NodeLabels =
        {
            (1, "complet Food+Manu+Lux"),
            (2, "SANS Manufactured"),
            (3, "Alexandria mono-Luxury SANS Food+Manu"),
            (4, "SANS Manufactured"),
            (5, "complet Food+Manu+Lux"),
            (6, "SANS Luxury"),
            (7, "SANS Luxury"),
            (8, "SANS Luxury"),
        };

        [Test]
        public void Eco028_MeasureRegionalMarketsAtKeyTicks() => RunMeasurementsAndWriteLog();

        public static void RunMeasurementsAndWriteLog()
        {
            var previousWeight = PopConsumptionSystem.LocalityWeight;
            var logPath = Path.Combine(
                UnityEngine.Application.dataPath, "..", "Logs", "eco_028_measurements.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine(
                    $"=== eco_028 seed={Seed} DefaultLocalityWeight={PopConsumptionSystem.DefaultLocalityWeight:F2} " +
                    $"GoodTypeStride={PopConsumptionSystem.GoodTypeStride} " +
                    $"GateMode={ArmyDisbandmentSystem.GateMode} ===");
                sb.AppendLine(
                    "tried: LOCALITY_WEIGHT=0 (non-régression eco_027), 0.5 (retenu), " +
                    "autarcie pure w=1 déconseillée (effondre node3)");
                sb.AppendLine(
                    "Note isolation: MarketPrice reste GLOBAL ; à w>0 la démographie régionale " +
                    "peut faire bouger dette/armée par effet INDIRECT (via pop.Size → labor → offre).");
                sb.AppendLine();

                AppendScenario(sb, "A — LOCALITY_WEIGHT=0 (mondial, = eco_027)", 0f);
                AppendScenario(sb, "B — LOCALITY_WEIGHT=0.5 (retenu)", 0.5f);

                AppendVerdict(sb);

                File.WriteAllText(logPath, sb.ToString());
                UnityEngine.Debug.Log(sb.ToString());
            }
            finally
            {
                PopConsumptionSystem.LocalityWeight = previousWeight;
            }
        }

        static void AppendScenario(StringBuilder sb, string title, float localityWeight)
        {
            PopConsumptionSystem.LocalityWeight = localityWeight;
            sb.AppendLine($"=== {title} LOCALITY_WEIGHT={localityWeight.ToString("F2", CultureInfo.InvariantCulture)} ===");

            foreach (var tick in SnapshotTicks)
            {
                using var harness = new SimulationHarness(Seed);
                harness.RunTicks(tick);
                var em = harness.EntityManager;

                var (needsSatAvg, population) = CapturePopMetrics(em);
                var (totalDebt, bankrupt, worldArmyStr) = CaptureMonetary(em);
                var perNode = CapturePerNode(em);

                sb.AppendLine(
                    $"tick{tick}: needsSatAvg={needsSatAvg:F4} population={population} " +
                    $"totalDebt={totalDebt:F1} bankrupt={bankrupt} worldArmyStr={worldArmyStr:F0}");

                float satMin = float.MaxValue, satMax = float.MinValue;
                foreach (var n in perNode)
                {
                    if (n.Population <= 0)
                        continue;
                    if (n.SatAvg < satMin) satMin = n.SatAvg;
                    if (n.SatAvg > satMax) satMax = n.SatAvg;
                }

                var spread = satMax - satMin;
                sb.AppendLine(
                    $"  nodes: satMin={satMin:F4} satMax={satMax:F4} spread={spread:F4}");

                foreach (var label in NodeLabels)
                {
                    NodeSnap snap = default;
                    var found = false;
                    foreach (var n in perNode)
                    {
                        if (n.NodeId != label.Id)
                            continue;
                        snap = n;
                        found = true;
                        break;
                    }

                    if (!found)
                    {
                        sb.AppendLine($"  node{label.Id} ({label.Label}): (absent)");
                        continue;
                    }

                    sb.AppendLine(
                        $"  node{label.Id} ({label.Label}): sat={snap.SatAvg:F4} pop={snap.Population}");
                }
            }

            sb.AppendLine();
        }

        static void AppendVerdict(StringBuilder sb)
        {
            PopConsumptionSystem.LocalityWeight = 0f;
            var w0 = CaptureAt(1000);
            PopConsumptionSystem.LocalityWeight = 0.5f;
            var w05 = CaptureAt(1000);

            sb.AppendLine("=== VERDICT eco_028 (t1000, seed 42195) ===");
            sb.AppendLine(
                $"w=0: needsSatAvg={w0.NeedsSatAvg:F4} pop={w0.Population} " +
                $"debt={w0.TotalDebt:F1} bankrupt={w0.Bankrupt} army={w0.WorldArmyStr:F0} spread={w0.Spread:F4}");
            sb.AppendLine(
                $"w=0.5: needsSatAvg={w05.NeedsSatAvg:F4} pop={w05.Population} " +
                $"debt={w05.TotalDebt:F1} bankrupt={w05.Bankrupt} army={w05.WorldArmyStr:F0} spread={w05.Spread:F4}");

            if (w0.Spread < 1e-5f)
                sb.AppendLine("OK w=0: satisfaction homogène (spread~0) — non-régression structurelle.");
            else
                sb.AppendLine($"ALERT w=0: spread={w0.Spread:F4} attendu ~0.");

            if (w05.Spread > 0.01f)
                sb.AppendLine($"OK w=0.5: hétérogénéité visible (spread={w05.Spread:F4}).");
            else
                sb.AppendLine($"ALERT w=0.5: spread trop faible ({w05.Spread:F4}) — carte sans sens.");

            var node3 = FindNode(w05.Nodes, 3);
            if (node3.Population > 0 && node3.SatAvg > 0.15f)
                sb.AppendLine(
                    $"OK node3/Alexandria: sat={node3.SatAvg:F4} pop={node3.Population} " +
                    "(planché par terme global, pas d'effondrement).");
            else
                sb.AppendLine(
                    $"ALERT node3/Alexandria: sat={node3.SatAvg:F4} pop={node3.Population} — " +
                    "risque d'effondrement mono-bien.");

            var node1 = FindNode(w05.Nodes, 1);
            var node5 = FindNode(w05.Nodes, 5);
            sb.AppendLine(
                $"nœuds complets: node1 sat={node1.SatAvg:F4} pop={node1.Population} ; " +
                $"node5 sat={node5.SatAvg:F4} pop={node5.Population}");

            // Divergence démographique : comparer pop node3 vs node1 à t50 et t1000
            PopConsumptionSystem.LocalityWeight = 0.5f;
            var early = CaptureAt(50);
            var late = CaptureAt(1000);
            var n1e = FindNode(early.Nodes, 1);
            var n3e = FindNode(early.Nodes, 3);
            var n1l = FindNode(late.Nodes, 1);
            var n3l = FindNode(late.Nodes, 3);
            sb.AppendLine(
                $"démographie: node1 pop t50={n1e.Population}→t1000={n1l.Population} ; " +
                $"node3 t50={n3e.Population}→t1000={n3l.Population}");

            if (System.Math.Abs(w05.NeedsSatAvg - w0.NeedsSatAvg) < 0.15f &&
                System.Math.Abs(w05.Population - w0.Population) < 40000)
            {
                sb.AppendLine(
                    "OK: à w=0.5, moyenne mondiale et population dans l'ordre de grandeur de w=0 " +
                    "(redistribution, pas destruction).");
            }
            else
            {
                sb.AppendLine(
                    "ALERT: écart mondial w=0 vs w=0.5 trop large — calibrage LOCALITY_WEIGHT à revoir.");
            }

            sb.AppendLine(
                $"LOCALITY_WEIGHT retenu={PopConsumptionSystem.DefaultLocalityWeight:F2} " +
                "(défaut de production).");
        }

        struct WorldSnap
        {
            public float NeedsSatAvg;
            public int Population;
            public float TotalDebt;
            public int Bankrupt;
            public float WorldArmyStr;
            public float Spread;
            public List<NodeSnap> Nodes;
        }

        struct NodeSnap
        {
            public int NodeId;
            public float SatAvg;
            public int Population;
        }

        static WorldSnap CaptureAt(int ticks)
        {
            using var harness = new SimulationHarness(Seed);
            harness.RunTicks(ticks);
            var em = harness.EntityManager;
            var (needsSatAvg, population) = CapturePopMetrics(em);
            var (totalDebt, bankrupt, worldArmyStr) = CaptureMonetary(em);
            var nodes = CapturePerNode(em);

            float satMin = float.MaxValue, satMax = float.MinValue;
            foreach (var n in nodes)
            {
                if (n.Population <= 0)
                    continue;
                if (n.SatAvg < satMin) satMin = n.SatAvg;
                if (n.SatAvg > satMax) satMax = n.SatAvg;
            }

            var spread = satMax >= satMin ? satMax - satMin : 0f;
            return new WorldSnap
            {
                NeedsSatAvg = needsSatAvg,
                Population = population,
                TotalDebt = totalDebt,
                Bankrupt = bankrupt,
                WorldArmyStr = worldArmyStr,
                Spread = spread,
                Nodes = nodes
            };
        }

        static NodeSnap FindNode(List<NodeSnap> nodes, int id)
        {
            foreach (var n in nodes)
            {
                if (n.NodeId == id)
                    return n;
            }

            return new NodeSnap { NodeId = id };
        }

        static (float needsSatAvg, int population) CapturePopMetrics(EntityManager em)
        {
            double weightedSat = 0.0;
            var totalPop = 0;

            using var popQuery = em.CreateEntityQuery(typeof(PopData));
            using var pops = popQuery.ToComponentDataArray<PopData>(Allocator.Temp);
            foreach (var pop in pops)
            {
                totalPop += pop.Size;
                weightedSat += pop.NeedsSatisfaction * pop.Size;
            }

            var satAvg = totalPop > 0 ? (float)(weightedSat / totalPop) : 0f;
            return (satAvg, totalPop);
        }

        static (float totalDebt, int bankrupt, float worldArmyStr) CaptureMonetary(EntityManager em)
        {
            float totalDebt = 0f;
            var bankrupt = 0;
            using var treasuryQuery = em.CreateEntityQuery(ComponentType.ReadOnly<TreasuryData>());
            using var treasuries = treasuryQuery.ToComponentDataArray<TreasuryData>(Allocator.Temp);
            for (var i = 0; i < treasuries.Length; i++)
            {
                totalDebt += treasuries[i].Debt;
                if (treasuries[i].BankruptcyTick > 0)
                    bankrupt++;
            }

            float worldArmyStr = 0f;
            using var armyQuery = em.CreateEntityQuery(ComponentType.ReadOnly<ArmyData>());
            using var armies = armyQuery.ToComponentDataArray<ArmyData>(Allocator.Temp);
            for (var i = 0; i < armies.Length; i++)
                worldArmyStr += armies[i].Strength;

            return (totalDebt, bankrupt, worldArmyStr);
        }

        static List<NodeSnap> CapturePerNode(EntityManager em)
        {
            // Accumulators indexés 0..8 (TradeNodeId 1..8) ; index 0 = orphelin.
            var popSum = new int[9];
            var satWeighted = new double[9];

            using var popQuery = em.CreateEntityQuery(typeof(PopData));
            using var pops = popQuery.ToComponentDataArray<PopData>(Allocator.Temp);
            for (var i = 0; i < pops.Length; i++)
            {
                var pop = pops[i];
                var nodeId = 0;
                if (pop.Province != Entity.Null && em.HasComponent<ProvinceData>(pop.Province))
                    nodeId = em.GetComponentData<ProvinceData>(pop.Province).TradeNodeId;

                if (nodeId < 0 || nodeId > 8)
                    nodeId = 0;

                popSum[nodeId] += pop.Size;
                satWeighted[nodeId] += pop.NeedsSatisfaction * pop.Size;
            }

            var results = new List<NodeSnap>(8);
            for (var id = 1; id <= 8; id++)
            {
                var pop = popSum[id];
                var sat = pop > 0 ? (float)(satWeighted[id] / pop) : 0f;
                results.Add(new NodeSnap
                {
                    NodeId = id,
                    SatAvg = sat,
                    Population = pop
                });
            }

            return results;
        }
    }
}
