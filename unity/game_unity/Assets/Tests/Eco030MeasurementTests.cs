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
    /// <summary>Point d'entrée batchmode : -executeMethod VictoriaGame.Tests.Eco030BatchRunner.Run</summary>
    public static class Eco030BatchRunner
    {
        public static void Run()
        {
            Eco030MeasurementTests.RunMeasurementsAndWriteLog();
        }
    }

    [TestFixture]
    public class Eco030MeasurementTests
    {
        const uint Seed = 42195u;
        static readonly int[] SnapshotTicks = { 50, 200, 500, 1000 };
        static readonly string[] FocusTags = { "VEN", "BYZ" };
        const int SampleGoodId = 1; // grain (Food) — produit dans plusieurs nœuds

        [Test]
        public void Eco030_MeasureRegionalProductionPricesAtKeyTicks() => RunMeasurementsAndWriteLog();

        public static void RunMeasurementsAndWriteLog()
        {
            var previousWeight = TaxSystem.LocalityWeight;
            var logPath = Path.Combine(
                UnityEngine.Application.dataPath, "..", "Logs", "eco_030_measurements.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine(
                    $"=== eco_030 seed={Seed} DefaultLocalityWeight={TaxSystem.DefaultLocalityWeight:F2} " +
                    $"FactorMin={TaxSystem.FactorMin:F2} FactorMax={TaxSystem.FactorMax:F2} " +
                    $"SupplyStride={TaxSystem.SupplyStride} DemandStride={TaxSystem.DemandStride} ===");
                sb.AppendLine(
                    "tried: LOCALITY_WEIGHT=0 (non-régression eco_029), 0.25 (retenu), 0.5 (essai — " +
                    "BYZ army~490, debt~751 hors bande douce), bornes facteur [0.5, 2.0]");
                sb.AppendLine(
                    "Note: MarketPrice reste GLOBAL ; seul le revenu de production (TaxSystem) " +
                    "voit priceEff régional. needsSatAvg/pop doivent rester isolés.");
                sb.AppendLine();

                AppendScenario(sb, "A — LOCALITY_WEIGHT=0 (mondial = eco_029)", 0f);
                AppendScenario(sb, "B — LOCALITY_WEIGHT=0.25 (retenu)", 0.25f);
                AppendScenario(sb, "C — LOCALITY_WEIGHT=0.5 (essai)", 0.5f);

                AppendVerdict(sb);

                File.WriteAllText(logPath, sb.ToString());
                UnityEngine.Debug.Log(sb.ToString());
            }
            finally
            {
                TaxSystem.LocalityWeight = previousWeight;
            }
        }

        static void AppendScenario(StringBuilder sb, string title, float localityWeight)
        {
            TaxSystem.LocalityWeight = localityWeight;
            sb.AppendLine(
                $"=== {title} LOCALITY_WEIGHT={localityWeight.ToString("F2", CultureInfo.InvariantCulture)} ===");

            foreach (var tick in SnapshotTicks)
            {
                using var harness = new SimulationHarness(Seed);
                harness.RunTicks(tick);
                var em = harness.EntityManager;

                var army = SumArmyStrengthByCountry(em);
                var regs = CountRegimentsByCountry(em);
                var snaps = CaptureCountrySnapshots(em, army, regs);
                var metrics = Aggregate(snaps);
                var (needsSatAvg, population) = CapturePopMetrics(em);
                var priceSamples = CapturePriceEffSamples(em, SampleGoodId);

                sb.AppendLine(
                    $"tick{tick}: totalIncome={metrics.TotalIncome:F1} totalDebt={metrics.TotalDebt:F1} " +
                    $"bankrupt={metrics.BankruptCountries} worldArmyStr={metrics.WorldArmyStr:F0} " +
                    $"needsSatAvg={needsSatAvg:F3} population={population}");

                foreach (var tag in FocusTags)
                {
                    foreach (var snap in snaps)
                    {
                        if (snap.Tag.ToString() != tag)
                            continue;

                        sb.AppendLine(
                            $"  {tag}: Balance={snap.Balance:F1} Debt={snap.Debt:F1} " +
                            $"Income={snap.Income:F1} Expenses={snap.Expenses:F1} " +
                            $"armyStr={snap.ArmyStrength:F0} regiments={snap.RegimentCount}");
                    }
                }

                // Top/bottom revenus pour hétérogénéité géographique.
                snaps.Sort((a, b) => b.Income.CompareTo(a.Income));
                if (snaps.Count > 0)
                {
                    var top = snaps[0];
                    var bot = snaps[snaps.Count - 1];
                    sb.AppendLine(
                        $"  income extremes: {top.Tag}={top.Income:F1} … {bot.Tag}={bot.Income:F1}");
                }

                if (priceSamples.Count > 0)
                {
                    sb.Append($"  priceEff goodId={SampleGoodId} (grain):");
                    foreach (var s in priceSamples)
                    {
                        sb.Append(
                            $" node{s.NodeId}: global={s.GlobalPrice:F3} factor={s.Factor:F3} " +
                            $"eff={s.EffectivePrice:F3};");
                    }
                    sb.AppendLine();
                }

                army.Dispose();
                regs.Dispose();
            }

            sb.AppendLine();
        }

        static void AppendVerdict(StringBuilder sb)
        {
            var w0 = CaptureAt(1000, 0f);
            var wRetained = CaptureAt(1000, TaxSystem.DefaultLocalityWeight);
            var w05 = CaptureAt(1000, 0.5f);

            sb.AppendLine("=== VERDICT eco_030 (t1000, seed 42195) ===");
            sb.AppendLine(
                $"w=0: income={w0.TotalIncome:F1} debt={w0.TotalDebt:F1} bankrupt={w0.BankruptCountries} " +
                $"army={w0.WorldArmyStr:F0} sat={w0.NeedsSatAvg:F3} pop={w0.Population} " +
                $"VEN.inc={w0.Ven.Income:F1} BYZ.inc={w0.Byz.Income:F1} " +
                $"VEN.regs={w0.Ven.RegimentCount} BYZ.army={w0.Byz.ArmyStrength:F0}");
            sb.AppendLine(
                $"w={TaxSystem.DefaultLocalityWeight.ToString("F2", CultureInfo.InvariantCulture)}: " +
                $"income={wRetained.TotalIncome:F1} debt={wRetained.TotalDebt:F1} " +
                $"bankrupt={wRetained.BankruptCountries} army={wRetained.WorldArmyStr:F0} " +
                $"sat={wRetained.NeedsSatAvg:F3} pop={wRetained.Population} " +
                $"VEN.inc={wRetained.Ven.Income:F1} BYZ.inc={wRetained.Byz.Income:F1} " +
                $"VEN.regs={wRetained.Ven.RegimentCount} BYZ.army={wRetained.Byz.ArmyStrength:F0}");
            sb.AppendLine(
                $"w=0.5: income={w05.TotalIncome:F1} debt={w05.TotalDebt:F1} bankrupt={w05.BankruptCountries} " +
                $"army={w05.WorldArmyStr:F0} BYZ.army={w05.Byz.ArmyStrength:F0} VEN.regs={w05.Ven.RegimentCount}");

            // Non-régression needs/pop (isolation).
            if (System.Math.Abs(w0.NeedsSatAvg - wRetained.NeedsSatAvg) < 1e-4 &&
                w0.Population == wRetained.Population)
            {
                sb.AppendLine(
                    $"OK isolation needs/pop: sat={wRetained.NeedsSatAvg:F3} pop={wRetained.Population} " +
                    "(identique w=0 vs w retenu).");
            }
            else
            {
                sb.AppendLine(
                    $"ALERT isolation: w0 sat={w0.NeedsSatAvg:F3} pop={w0.Population} vs " +
                    $"wRet sat={wRetained.NeedsSatAvg:F3} pop={wRetained.Population}");
            }

            // Hétérogénéité géographique du revenu.
            var incomeDiffVen = System.Math.Abs(wRetained.Ven.Income - w0.Ven.Income);
            var incomeDiffByz = System.Math.Abs(wRetained.Byz.Income - w0.Byz.Income);
            var worldIncomeDelta = System.Math.Abs(wRetained.TotalIncome - w0.TotalIncome);
            var worldIncomeRel = w0.TotalIncome > 0f
                ? worldIncomeDelta / w0.TotalIncome
                : 0f;

            if (incomeDiffVen > 0.01f || incomeDiffByz > 0.01f || worldIncomeDelta > 0.01f)
            {
                sb.AppendLine(
                    $"OK hétérogénéité: ΔVEN.inc={incomeDiffVen:F2} ΔBYZ.inc={incomeDiffByz:F2} " +
                    $"ΔworldIncome={worldIncomeDelta:F2} (rel={worldIncomeRel.ToString("P1", CultureInfo.InvariantCulture)}).");
            }
            else
            {
                sb.AppendLine("ALERT hétérogénéité: revenus inchangés à w retenu (facteur inerte?).");
            }

            if (worldIncomeRel < 0.25f)
            {
                sb.AppendLine(
                    $"OK redistribution (pas destruction): |Δincome|/income_w0=" +
                    $"{worldIncomeRel.ToString("P1", CultureInfo.InvariantCulture)} < 25%.");
            }
            else
            {
                sb.AppendLine(
                    $"ALERT destruction?: |Δincome|/income_w0=" +
                    $"{worldIncomeRel.ToString("P1", CultureInfo.InvariantCulture)} ≥ 25%.");
            }

            if (wRetained.TotalDebt <= 1100f)
                sb.AppendLine($"OK DETTE BORNÉE: totalDebt={wRetained.TotalDebt:F1} ≤ ~1000 (+marge).");
            else
                sb.AppendLine($"ALERT SPIRALE: totalDebt={wRetained.TotalDebt:F1} > 1100.");

            if (wRetained.Ven.RegimentCount >= 2 && wRetained.Ven.RegimentCount <= 5 &&
                wRetained.Byz.ArmyStrength >= 450f &&
                wRetained.WorldArmyStr >= 20000f && wRetained.WorldArmyStr <= 60000f)
            {
                sb.AppendLine(
                    $"OK acquis eco_026/027/029: VEN~{wRetained.Ven.RegimentCount} rég, " +
                    $"BYZ armyStr={wRetained.Byz.ArmyStrength:F0}, worldArmy={wRetained.WorldArmyStr:F0}.");
            }
            else
            {
                sb.AppendLine(
                    $"ALERT acquis: VEN regs={wRetained.Ven.RegimentCount} " +
                    $"BYZ army={wRetained.Byz.ArmyStrength:F0} worldArmy={wRetained.WorldArmyStr:F0}.");
            }

            if (System.Math.Abs(wRetained.NeedsSatAvg - 0.70) < 0.15 &&
                System.Math.Abs(wRetained.Population - 140000) < 40000)
            {
                sb.AppendLine(
                    $"OK needs/pop ordre eco_029: sat={wRetained.NeedsSatAvg:F3} pop={wRetained.Population}.");
            }
            else
            {
                sb.AppendLine(
                    $"ALERT needs/pop: sat={wRetained.NeedsSatAvg:F3} pop={wRetained.Population}.");
            }

            // Exemple priceEff sur deux nœuds.
            if (wRetained.PriceSamples.Count >= 2)
            {
                var a = wRetained.PriceSamples[0];
                var b = wRetained.PriceSamples[1];
                sb.AppendLine(
                    $"priceEff grain: node{a.NodeId} factor={a.Factor:F3} eff={a.EffectivePrice:F3} vs " +
                    $"node{b.NodeId} factor={b.Factor:F3} eff={b.EffectivePrice:F3} " +
                    $"(global={a.GlobalPrice:F3}).");
                if (System.Math.Abs(a.Factor - b.Factor) > 1e-4f)
                    sb.AppendLine("OK priceEff régional divergent entre nœuds.");
                else
                    sb.AppendLine("NOTE: facteurs identiques sur les 2 premiers nœuds échantillonnés.");
            }

            // Gagnants / perdants géographiques.
            sb.AppendLine(
                $"VEN w0→wRet Income {w0.Ven.Income:F1}→{wRetained.Ven.Income:F1} " +
                $"({(wRetained.Ven.Income >= w0.Ven.Income ? "gagnant/neutre" : "perdant")})");
            sb.AppendLine(
                $"BYZ w0→wRet Income {w0.Byz.Income:F1}→{wRetained.Byz.Income:F1} " +
                $"({(wRetained.Byz.Income >= w0.Byz.Income ? "gagnant/neutre" : "perdant")})");

            sb.AppendLine(
                $"LOCALITY_WEIGHT retenu={TaxSystem.DefaultLocalityWeight.ToString("F2", CultureInfo.InvariantCulture)} " +
                $"FACTOR=[{TaxSystem.FactorMin.ToString("F1", CultureInfo.InvariantCulture)}," +
                $"{TaxSystem.FactorMax.ToString("F1", CultureInfo.InvariantCulture)}] " +
                $"(w=0 = eco_029 ; w=0.5 essayé debt={w05.TotalDebt:F0}/BYZ={w05.Byz.ArmyStrength:F0}).");
        }

        struct CountrySnapshot
        {
            public FixedString32Bytes Tag;
            public float Balance;
            public float Debt;
            public float Income;
            public float Expenses;
            public float ArmyStrength;
            public int RegimentCount;
        }

        struct AggregateMetrics
        {
            public float TotalIncome;
            public float TotalDebt;
            public int BankruptCountries;
            public float WorldArmyStr;
        }

        struct PriceEffSample
        {
            public int NodeId;
            public float GlobalPrice;
            public float Factor;
            public float EffectivePrice;
        }

        struct WorldSnap
        {
            public float TotalIncome;
            public float TotalDebt;
            public int BankruptCountries;
            public float WorldArmyStr;
            public float NeedsSatAvg;
            public int Population;
            public CountrySnapshot Ven;
            public CountrySnapshot Byz;
            public List<PriceEffSample> PriceSamples;
        }

        static WorldSnap CaptureAt(int ticks, float localityWeight)
        {
            TaxSystem.LocalityWeight = localityWeight;
            using var harness = new SimulationHarness(Seed);
            harness.RunTicks(ticks);
            var em = harness.EntityManager;
            var army = SumArmyStrengthByCountry(em);
            var regs = CountRegimentsByCountry(em);
            var snaps = CaptureCountrySnapshots(em, army, regs);
            var agg = Aggregate(snaps);
            var (sat, pop) = CapturePopMetrics(em);
            var ven = FindTag(snaps, "VEN");
            var byz = FindTag(snaps, "BYZ");
            var samples = CapturePriceEffSamples(em, SampleGoodId);
            army.Dispose();
            regs.Dispose();
            return new WorldSnap
            {
                TotalIncome = agg.TotalIncome,
                TotalDebt = agg.TotalDebt,
                BankruptCountries = agg.BankruptCountries,
                WorldArmyStr = agg.WorldArmyStr,
                NeedsSatAvg = sat,
                Population = pop,
                Ven = ven,
                Byz = byz,
                PriceSamples = samples
            };
        }

        static CountrySnapshot FindTag(List<CountrySnapshot> snaps, string tag)
        {
            foreach (var s in snaps)
            {
                if (s.Tag.ToString() == tag)
                    return s;
            }

            return default;
        }

        static AggregateMetrics Aggregate(List<CountrySnapshot> snapshots)
        {
            float totalIncome = 0f;
            float totalDebt = 0f;
            var bankrupt = 0;
            float worldArmy = 0f;

            foreach (var snap in snapshots)
            {
                totalIncome += snap.Income;
                totalDebt += snap.Debt;
                if (snap.Debt > 0.01f && snap.Balance <= 0f)
                    bankrupt++;
                worldArmy += snap.ArmyStrength;
            }

            return new AggregateMetrics
            {
                TotalIncome = totalIncome,
                TotalDebt = totalDebt,
                BankruptCountries = bankrupt,
                WorldArmyStr = worldArmy
            };
        }

        /// <summary>
        /// Calcule priceEff pour un bien dans chaque nœud qui le produit (miroir TaxSystem).
        /// </summary>
        static List<PriceEffSample> CapturePriceEffSamples(EntityManager em, int goodId)
        {
            var results = new List<PriceEffSample>();
            float globalPrice = 0f;
            float globalSupply = 0f;
            float globalDemand = 0f;
            GoodType goodType = GoodType.Food;
            var found = false;

            using (var priceQuery = em.CreateEntityQuery(
                       ComponentType.ReadOnly<MarketPrice>(),
                       ComponentType.ReadOnly<GoodData>()))
            {
                using var prices = priceQuery.ToComponentDataArray<MarketPrice>(Allocator.Temp);
                using var goods = priceQuery.ToComponentDataArray<GoodData>(Allocator.Temp);
                for (var i = 0; i < goods.Length; i++)
                {
                    if (goods[i].GoodId != goodId)
                        continue;
                    globalPrice = prices[i].CurrentPrice;
                    globalSupply = prices[i].Supply;
                    globalDemand = prices[i].Demand;
                    goodType = goods[i].Type;
                    found = true;
                    break;
                }
            }

            if (!found)
                return results;

            var regionalSupply = new NativeHashMap<int, float>(64, Allocator.Temp);
            var regionalDemand = new NativeHashMap<int, float>(64, Allocator.Temp);
            var nodeSet = new NativeHashMap<int, byte>(16, Allocator.Temp);

            using (var siteQuery = em.CreateEntityQuery(
                       ComponentType.ReadOnly<ProductionSite>(),
                       ComponentType.ReadOnly<ProvinceData>()))
            {
                using var sites = siteQuery.ToComponentDataArray<ProductionSite>(Allocator.Temp);
                using var provs = siteQuery.ToComponentDataArray<ProvinceData>(Allocator.Temp);
                for (var i = 0; i < sites.Length; i++)
                {
                    if (sites[i].GoodId != goodId)
                        continue;
                    int key = TaxSystem.SupplyKey(provs[i].TradeNodeId, goodId);
                    regionalSupply.TryGetValue(key, out float cur);
                    regionalSupply[key] = cur + sites[i].LastOutput;
                    nodeSet.TryAdd(provs[i].TradeNodeId, 0);
                }
            }

            using (var popQuery = em.CreateEntityQuery(
                       ComponentType.ReadOnly<PopNeeds>(),
                       ComponentType.ReadOnly<PopData>()))
            {
                using var needs = popQuery.ToComponentDataArray<PopNeeds>(Allocator.Temp);
                using var pops = popQuery.ToComponentDataArray<PopData>(Allocator.Temp);
                for (var i = 0; i < pops.Length; i++)
                {
                    int nodeId = 0;
                    if (em.HasComponent<ProvinceData>(pops[i].Province))
                        nodeId = em.GetComponentData<ProvinceData>(pops[i].Province).TradeNodeId;

                    float scale = pops[i].Size;
                    AddDemand(regionalDemand, nodeId, GoodType.Food, needs[i].FoodNeed * scale);
                    AddDemand(regionalDemand, nodeId, GoodType.Manufactured, needs[i].ClothNeed * scale);
                    AddDemand(regionalDemand, nodeId, GoodType.Luxury, needs[i].LuxuryNeed * scale);
                }
            }

            var nodes = new List<int>();
            foreach (var kv in nodeSet)
                nodes.Add(kv.Key);
            nodes.Sort();

            float w = TaxSystem.LocalityWeight;
            foreach (var nodeId in nodes)
            {
                float regSupply = regionalSupply.TryGetValue(TaxSystem.SupplyKey(nodeId, goodId), out float rs)
                    ? rs
                    : 0f;
                float regDemand = regionalDemand.TryGetValue(TaxSystem.DemandKey(nodeId, goodType), out float rd)
                    ? rd
                    : 0f;
                float factor = TaxSystem.ComputeRegionalFactor(
                    regDemand, regSupply, globalDemand, globalSupply,
                    TaxSystem.ScarcityEpsilon, TaxSystem.FactorMin, TaxSystem.FactorMax);
                float eff = TaxSystem.ComputeEffectivePrice(globalPrice, factor, w);
                results.Add(new PriceEffSample
                {
                    NodeId = nodeId,
                    GlobalPrice = globalPrice,
                    Factor = factor,
                    EffectivePrice = eff
                });
            }

            regionalSupply.Dispose();
            regionalDemand.Dispose();
            nodeSet.Dispose();
            return results;
        }

        static void AddDemand(NativeHashMap<int, float> map, int nodeId, GoodType type, float amount)
        {
            int key = TaxSystem.DemandKey(nodeId, type);
            map.TryGetValue(key, out float cur);
            map[key] = cur + amount;
        }

        static NativeHashMap<Entity, float> SumArmyStrengthByCountry(EntityManager em)
        {
            var map = new NativeHashMap<Entity, float>(32, Allocator.Temp);
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<ArmyData>());
            using var armies = query.ToComponentDataArray<ArmyData>(Allocator.Temp);

            for (var i = 0; i < armies.Length; i++)
            {
                var country = armies[i].Country;
                if (country == Entity.Null)
                    continue;

                map.TryGetValue(country, out var current);
                map[country] = current + armies[i].Strength;
            }

            return map;
        }

        static NativeHashMap<Entity, int> CountRegimentsByCountry(EntityManager em)
        {
            var map = new NativeHashMap<Entity, int>(32, Allocator.Temp);
            using var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<ArmyData>(),
                ComponentType.ReadOnly<RegimentSlot>());
            using var entities = query.ToEntityArray(Allocator.Temp);
            using var armies = query.ToComponentDataArray<ArmyData>(Allocator.Temp);

            for (var i = 0; i < entities.Length; i++)
            {
                var country = armies[i].Country;
                if (country == Entity.Null)
                    continue;

                var slots = em.GetBuffer<RegimentSlot>(entities[i]);
                map.TryGetValue(country, out var current);
                map[country] = current + slots.Length;
            }

            return map;
        }

        static List<CountrySnapshot> CaptureCountrySnapshots(
            EntityManager em,
            NativeHashMap<Entity, float> armyStrengthByCountry,
            NativeHashMap<Entity, int> regimentCounts)
        {
            var results = new List<CountrySnapshot>();
            using var countryQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<CountryData>(),
                ComponentType.ReadOnly<TreasuryData>());
            using var countries = countryQuery.ToEntityArray(Allocator.Temp);
            using var countryData = countryQuery.ToComponentDataArray<CountryData>(Allocator.Temp);
            using var treasuries = countryQuery.ToComponentDataArray<TreasuryData>(Allocator.Temp);

            for (var i = 0; i < countries.Length; i++)
            {
                var entity = countries[i];
                var treasury = treasuries[i];
                var armyStr = armyStrengthByCountry.TryGetValue(entity, out var a) ? a : 0f;
                var regs = regimentCounts.TryGetValue(entity, out var r) ? r : 0;

                results.Add(new CountrySnapshot
                {
                    Tag = countryData[i].Tag,
                    Balance = treasury.Balance,
                    Debt = treasury.Debt,
                    Income = treasury.Income,
                    Expenses = treasury.Expenses,
                    ArmyStrength = armyStr,
                    RegimentCount = regs
                });
            }

            results.Sort((a, b) => a.Tag.CompareTo(b.Tag));
            return results;
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
    }
}
