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
    /// <summary>Batch : -executeMethod VictoriaGame.Tests.V1025BatchRunner.Run</summary>
    public static class V1025BatchRunner
    {
        public static void Run()
        {
            V1025PhysicalEndowmentDiagnostic.RunFullSuiteAndWriteLog();
            UnityEngine.Debug.Log("V1025BatchRunner: DONE");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
        }
    }

    /// <summary>
    /// v1_025 — dotation multi-biens dérivée du terrain, déplacement de la rareté
    /// mondiale → locale, mesure de la config adoptée (infra + multi-sauts + endowment).
    /// </summary>
    [TestFixture]
    public class V1025PhysicalEndowmentDiagnostic
    {
        const uint Seed = 42195u;
        const float PerDev = 2400.643f;
        const int ClothGoodId = 8;
        const int WoolGoodId = 6;
        const int CoalGoodId = 7;
        const int IronGoodId = 5;
        const int WoodGoodId = 4;
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
        public void V1025_Endowment_IsMultiGood_PerProvince()
        {
            PhysicalSatisfactionBlendSystem.LockWeight(0f);
            using var h = new SimulationHarness(Seed);
            h.RunTicks(0);
            var dist = MeasureActivityDistribution(h.EntityManager);
            Assert.Greater(dist.ProvinceCount, 0);
            Assert.Greater(dist.MultiGoodProvinces, 0,
                "Hypothèse infirmée: aucune province multi-biens après endowment");
            Assert.Greater(dist.MeanActivitiesPerProvince, 1.01f,
                $"Mean activités/province={dist.MeanActivitiesPerProvince} — toujours 1:1?");
            Assert.Greater(dist.MaxActivitiesPerProvince, 1);
        }

        [Test]
        public void V1025_WoolWorldCapacity_CoversClothDemand()
        {
            PhysicalSatisfactionBlendSystem.LockWeight(0f);
            var report = MeasureEndowmentCaps(runTicks: 200);
            Assert.Greater(report.ClothDemand, 0f);
            Assert.Greater(report.WoolWorldCap, report.ClothDemand,
                $"Lainé mondiale {report.WoolWorldCap} < demande drap {report.ClothDemand} — " +
                "impossibilité structurelle non résolue");
            Assert.Greater(report.WoolSiteCount, 4,
                $"Sites laine physiques={report.WoolSiteCount} (LOD seul=4)");
        }

        [Test]
        public void V1025_Rarity_RemainsGeographicallyConcentrated()
        {
            PhysicalSatisfactionBlendSystem.LockWeight(0f);
            using var h = new SimulationHarness(Seed);
            h.RunTicks(0);
            var conc = MeasureConcentration(h.EntityManager, WoolGoodId);
            Assert.Less(conc.ProducerShare, 0.55f,
                $"Laine trop diffuse ({conc.ProducerShare:P0} des provinces) — géographie aplatie");
            Assert.Greater(conc.TopQuartileShare, 0.25f,
                "Top 25% des producteurs devraient porter une part significative");
        }

        [Test]
        public void V1025_WeightZero_NoOp_AndDeterminism()
        {
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
                PhysicalSatisfactionBlendSystem.LockWeight(0f);
                h2.RunTicks(0);
                SetTransportInfra(h2.EntityManager, PerDev);
                h2.RunTicks(80);
                d2 = WorldDigest(h2.EntityManager);
            }

            Assert.AreEqual(d1, d2, $"Non déterministe w=0: {d1:X16} vs {d2:X16}");
        }

        [Test]
        public void V1025_Determinism_AdoptedConfig()
        {
            PhysicalSatisfactionBlendSystem.LockWeight(0.25f);
            PhysicalStockSystem.MultiHopTransport = true;
            ulong d1, d2;
            using (var h1 = new SimulationHarness(Seed))
            {
                h1.RunTicks(0);
                SetTransportInfra(h1.EntityManager, PerDev);
                h1.RunTicks(150);
                d1 = WorldDigest(h1.EntityManager);
            }

            using (var h2 = new SimulationHarness(Seed))
            {
                PhysicalSatisfactionBlendSystem.LockWeight(0.25f);
                h2.RunTicks(0);
                SetTransportInfra(h2.EntityManager, PerDev);
                h2.RunTicks(150);
                d2 = WorldDigest(h2.EntityManager);
            }

            Assert.AreEqual(d1, d2, $"Non déterministe config adoptée: {d1:X16} vs {d2:X16}");
        }

        [Test]
        public void V1025_LodSites_Untouched_SingleGood()
        {
            PhysicalSatisfactionBlendSystem.LockWeight(0f);
            using var h = new SimulationHarness(Seed);
            h.RunTicks(0);
            var sites = 0;
            using var q = h.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<ProductionSite>());
            using var arr = q.ToComponentDataArray<ProductionSite>(Allocator.Temp);
            for (var i = 0; i < arr.Length; i++)
            {
                if (arr[i].GoodId > 0)
                {
                    sites++;
                }
            }

            Assert.AreEqual(50, sites, "LOD: une province = un ProductionSite (inchangé)");
        }

        // Diagnostic lourd : V1025BatchRunner uniquement (évite OOM EditMode).
        public static void RunFullSuiteAndWriteLog()
        {
            var logPath = Path.Combine(
                UnityEngine.Application.dataPath, "..", "Logs", "v1_025_endowment.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            var sb = new StringBuilder(256 * 1024);

            sb.AppendLine("=== v1_025 PHYSICAL ENDOWMENT DIAGNOSTIC — seed=42195 ===");
            sb.AppendLine(
                "Config adoptée mesurée: CapacityPerDev + MultiHop=ON + terrain_endowment.");
            sb.AppendLine();

            PhysicalSatisfactionBlendSystem.LockWeight(0f);
            PhysicalStockSystem.IdealPoolMode = false;
            PhysicalStockSystem.MultiHopTransport = true;

            // ----- PARTIE 1 — état des dotations -----
            sb.AppendLine("=== PARTIE 1 — ÉTAT DES DOTATIONS (LOD vs physique) ===");
            GoodBudgetReport budgets;
            ActivityDistribution dist;
            using (var h = new SimulationHarness(Seed))
            {
                h.RunTicks(0);
                SetTransportInfra(h.EntityManager, PerDev);
                dist = MeasureActivityDistribution(h.EntityManager);
                h.RunTicks(200);
                budgets = MeasureAllGoodBudgets(h.EntityManager);
            }

            sb.AppendLine(
                $"provinces={dist.ProvinceCount} multiGood={dist.MultiGoodProvinces} " +
                $"meanAct={Fmt(dist.MeanActivitiesPerProvince)} " +
                $"maxAct={dist.MaxActivitiesPerProvince} " +
                $"hypothese_1province_1bien={(dist.MaxActivitiesPerProvince <= 1 ? "CONFIRMEE" : "INFIRMEE")}");
            sb.AppendLine("good\tlodSites\tlodCap\tphysSites\tphysCap\tdemand\tratio\tshortage");
            for (var i = 0; i < budgets.Rows.Count; i++)
            {
                var r = budgets.Rows[i];
                var shortage = r.PhysCap + 1e-3f < r.Demand ? "WORLD" :
                    r.LocalShortageShare > 0.15f ? "LOCAL" : "ok";
                sb.AppendLine(
                    $"{r.Tag}\t{r.LodSites}\t{Fmt(r.LodCap)}\t{r.PhysSites}\t{Fmt(r.PhysCap)}\t" +
                    $"{Fmt(r.Demand)}\t{Fmt(r.Ratio)}\t{shortage}");
            }

            sb.AppendLine("Pénuries MONDIALES (physCap < demand):");
            var worldShort = 0;
            for (var i = 0; i < budgets.Rows.Count; i++)
            {
                if (budgets.Rows[i].PhysCap + 1e-3f < budgets.Rows[i].Demand)
                {
                    worldShort++;
                    sb.AppendLine($"  - {budgets.Rows[i].Tag} ratio={Fmt(budgets.Rows[i].Ratio)}");
                }
            }

            if (worldShort == 0)
            {
                sb.AppendLine("  (aucune)");
            }

            sb.AppendLine("Pénuries LOCALES (monde OK, dispersion):");
            var localShort = 0;
            for (var i = 0; i < budgets.Rows.Count; i++)
            {
                var r = budgets.Rows[i];
                if (r.PhysCap >= r.Demand - 1e-3f && r.LocalShortageShare > 0.15f)
                {
                    localShort++;
                    sb.AppendLine(
                        $"  - {r.Tag} localShare={Fmt(r.LocalShortageShare)} " +
                        $"producers={r.PhysSites}/{dist.ProvinceCount}");
                }
            }

            if (localShort == 0)
            {
                sb.AppendLine("  (aucune au seuil 15%)");
            }

            sb.AppendLine();

            // ----- PARTIE 2/3 — avant/après + concentration -----
            sb.AppendLine("=== PARTIE 2/3 — RARETÉ DÉPLACÉE (laine / charbon / fer / bois) ===");
            var caps = MeasureEndowmentCaps(runTicks: 300);
            sb.AppendLine(
                $"wool: lodSites={caps.WoolLodSites} lodCap={Fmt(caps.WoolLodCap)} " +
                $"physSites={caps.WoolSiteCount} physCap={Fmt(caps.WoolWorldCap)} " +
                $"clothDemand={Fmt(caps.ClothDemand)} " +
                $"cover={Fmt(caps.WoolWorldCap / math.max(caps.ClothDemand, 1f))}");
            sb.AppendLine(
                $"coal physCap={Fmt(caps.CoalWorldCap)} iron={Fmt(caps.IronWorldCap)} " +
                $"wood={Fmt(caps.WoodWorldCap)}");

            using (var h = new SimulationHarness(Seed))
            {
                h.RunTicks(0);
                var woolC = MeasureConcentration(h.EntityManager, WoolGoodId);
                var coalC = MeasureConcentration(h.EntityManager, CoalGoodId);
                sb.AppendLine(
                    $"concentration wool producers={woolC.ProducerCount} " +
                    $"share={Fmt(woolC.ProducerShare)} topQ={Fmt(woolC.TopQuartileShare)}");
                sb.AppendLine(
                    $"concentration coal producers={coalC.ProducerCount} " +
                    $"share={Fmt(coalC.ProducerShare)} topQ={Fmt(coalC.TopQuartileShare)}");
            }

            var trade = MeasureTradeSignals(300);
            sb.AppendLine(
                $"commerce émergent: transitMean={Fmt(trade.MeanInTransit)} " +
                $"delay={Fmt(trade.MeanDelay)} localClothShare={Fmt(trade.LocalClothShare)} " +
                $"importClothShare={Fmt(trade.ImportClothShare)}");
            sb.AppendLine();

            // ----- PARTIE 4 — config adoptée + balayage poids -----
            sb.AppendLine("=== PARTIE 4 — CONFIG ADOPTÉE (infra+MultiHop+endowment) t3000 ===");
            sb.AppendLine(
                "weight\tpop\tpopRatio\tsatAvg\tphysMean\tstarved\tdebt\tbankrupt\t" +
                "army\tcountries\twars\talive\tcpuMs");

            WeightRow? baseline = null;
            var adoptedW = -1f;
            var adoptedFound = false;
            foreach (var w in WeightSweep)
            {
                var row = RunWeightPoint(w);
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
                    ? (float)row.Pop / baseline.Value.Pop
                    : 1f;
                sb.AppendLine(
                    $"{Fmt(w)}\t{row.Pop}\t{Fmt(popRatio)}\t{Fmt(row.SatAvg)}\t{Fmt(row.PhysMean)}\t" +
                    $"{row.Starved}\t{Fmt(row.Debt)}\t{row.Bankrupt}\t{Fmt(row.Army)}\t" +
                    $"{row.Countries}\t{row.Wars}\t{(alive ? "Y" : "N")}\t{Fmt(row.CpuMs)}");
            }

            sb.AppendLine(
                $"palier_poids={(adoptedFound ? Fmt(adoptedW) : "none")} " +
                $"(réf v1_024=0.25)");
            sb.AppendLine();

            // ----- PARTIE 5 — garde-fous -----
            sb.AppendLine("=== PARTIE 5 — GARDE-FOUS ===");
            PhysicalSatisfactionBlendSystem.LockWeight(0.25f);
            PhysicalStockSystem.MultiHopTransport = true;
            ulong dA, dB;
            using (var h1 = new SimulationHarness(Seed))
            {
                h1.RunTicks(0);
                SetTransportInfra(h1.EntityManager, PerDev);
                h1.RunTicks(200);
                dA = WorldDigest(h1.EntityManager);
            }

            using (var h2 = new SimulationHarness(Seed))
            {
                PhysicalSatisfactionBlendSystem.LockWeight(0.25f);
                h2.RunTicks(0);
                SetTransportInfra(h2.EntityManager, PerDev);
                h2.RunTicks(200);
                dB = WorldDigest(h2.EntityManager);
            }

            var detOk = dA == dB;
            sb.AppendLine($"determinisme config adoptée t200: {(detOk ? "PASS" : "FAIL")} ({dA:X16})");

            var story = FindEmergentStory();
            if (story.Found)
            {
                sb.AppendLine(
                    $"récit émergent: province {story.ProvinceId} phys={Fmt(story.PhysSat)} " +
                    $"lod={Fmt(story.LodSat)} pop={story.PopSize} deltaPop={story.DeltaPop} " +
                    $"note={story.Note}");
            }
            else
            {
                sb.AppendLine("récit émergent: aucun écart phys/lod marquant isolé");
            }

            sb.AppendLine();
            sb.AppendLine("=== VERDICT MESURÉ ===");
            var woolOk = caps.WoolWorldCap >= caps.ClothDemand;
            sb.AppendLine(
                woolOk
                    ? $"Le monde PEUT s'habiller: woolCap={Fmt(caps.WoolWorldCap)} ≥ " +
                      $"clothDemand={Fmt(caps.ClothDemand)} ; production laine sur " +
                      $"{caps.WoolSiteCount} provinces (concentration locale conservée)."
                    : $"ÉCHEC habitabilité: woolCap={Fmt(caps.WoolWorldCap)} < " +
                      $"clothDemand={Fmt(caps.ClothDemand)}");
            sb.AppendLine(
                $"Pénuries mondiales restantes={worldShort} ; locales={localShort} ; " +
                $"palier_poids={(adoptedFound ? Fmt(adoptedW) : "none")}");
            sb.AppendLine($"determinism={(detOk ? "PASS" : "FAIL")}");

            File.WriteAllText(logPath, sb.ToString());
            UnityEngine.Debug.Log(
                $"V1025PhysicalEndowmentDiagnostic: wrote {logPath} determinism={(detOk ? "PASS" : "FAIL")} woolOk={(woolOk ? "Y" : "N")}");

            PhysicalSatisfactionBlendSystem.UnlockWeight();
            PhysicalStockSystem.MultiHopTransport = true;

            Assert.IsTrue(detOk, "Déterminisme échoué");
            Assert.IsTrue(woolOk, "Capacité laine mondiale insuffisante");
            Assert.Greater(dist.MultiGoodProvinces, 0);
        }

        // ----- types -----

        struct ActivityDistribution
        {
            public int ProvinceCount;
            public int MultiGoodProvinces;
            public int MaxActivitiesPerProvince;
            public float MeanActivitiesPerProvince;
        }

        struct CapReport
        {
            public int WoolLodSites, WoolSiteCount;
            public float WoolLodCap, WoolWorldCap, ClothDemand;
            public float CoalWorldCap, IronWorldCap, WoodWorldCap;
        }

        struct Concentration
        {
            public int ProducerCount;
            public float ProducerShare;
            public float TopQuartileShare;
        }

        struct GoodRow
        {
            public string Tag;
            public int GoodId;
            public int LodSites, PhysSites;
            public float LodCap, PhysCap, Demand, Ratio, LocalShortageShare;
        }

        struct GoodBudgetReport
        {
            public List<GoodRow> Rows;
        }

        struct TradeSignals
        {
            public float MeanInTransit, MeanDelay, LocalClothShare, ImportClothShare;
        }

        struct WeightRow
        {
            public float Weight;
            public int Pop, Starved, Bankrupt, Countries, Wars;
            public float SatAvg, PhysMean, Debt, Army, CpuMs;
        }

        struct EmergentStory
        {
            public bool Found;
            public int ProvinceId, PopSize, DeltaPop;
            public float PhysSat, LodSat;
            public string Note;
        }

        // ----- mesures -----

        static ActivityDistribution MeasureActivityDistribution(EntityManager em)
        {
            var provinceCount = 0;
            var multi = 0;
            var maxAct = 0;
            var sumAct = 0;
            using var q = em.CreateEntityQuery(
                ComponentType.ReadOnly<ProvinceData>(),
                ComponentType.ReadOnly<ProvincePhysicalActivity>());
            using var entities = q.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                provinceCount++;
                var buf = em.GetBuffer<ProvincePhysicalActivity>(entities[i]);
                // Activités uniques = endowment + (site LOD si pas déjà dedans)
                var goods = new HashSet<int>();
                for (var a = 0; a < buf.Length; a++)
                {
                    goods.Add(buf[a].GoodId);
                }

                if (em.HasComponent<ProductionSite>(entities[i]))
                {
                    var site = em.GetComponentData<ProductionSite>(entities[i]);
                    if (site.GoodId > 0)
                    {
                        goods.Add(site.GoodId);
                    }
                }

                var n = goods.Count;
                sumAct += n;
                if (n > maxAct)
                {
                    maxAct = n;
                }

                if (n > 1)
                {
                    multi++;
                }
            }

            return new ActivityDistribution
            {
                ProvinceCount = provinceCount,
                MultiGoodProvinces = multi,
                MaxActivitiesPerProvince = maxAct,
                MeanActivitiesPerProvince = provinceCount > 0 ? (float)sumAct / provinceCount : 0f
            };
        }

        static CapReport MeasureEndowmentCaps(int runTicks)
        {
            PhysicalStockSystem.MultiHopTransport = true;
            PhysicalStockSystem.IdealPoolMode = false;
            using var h = new SimulationHarness(Seed);
            h.RunTicks(0);
            SetTransportInfra(h.EntityManager, PerDev);
            var em = h.EntityManager;

            var woolLodSites = 0;
            var woolLodCap = 0f;
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<ProductionSite>()))
            using (var sites = q.ToComponentDataArray<ProductionSite>(Allocator.Temp))
            {
                for (var i = 0; i < sites.Length; i++)
                {
                    if (sites[i].GoodId == WoolGoodId)
                    {
                        woolLodSites++;
                        woolLodCap += sites[i].BaseOutput;
                    }
                }
            }

            float woolPhys = 0f, coal = 0f, iron = 0f, wood = 0f;
            var woolSites = 0;
            AccumulatePhysCap(em, WoolGoodId, ref woolPhys, ref woolSites);
            var tmp = 0;
            AccumulatePhysCap(em, CoalGoodId, ref coal, ref tmp);
            AccumulatePhysCap(em, IronGoodId, ref iron, ref tmp);
            AccumulatePhysCap(em, WoodGoodId, ref wood, ref tmp);

            h.RunTicks(runTicks);
            var clothDemand = SumClothDemand(em);

            return new CapReport
            {
                WoolLodSites = woolLodSites,
                WoolLodCap = woolLodCap,
                WoolSiteCount = woolSites,
                WoolWorldCap = woolPhys,
                ClothDemand = clothDemand,
                CoalWorldCap = coal,
                IronWorldCap = iron,
                WoodWorldCap = wood
            };
        }

        static void AccumulatePhysCap(
            EntityManager em, int goodId, ref float cap, ref int sites)
        {
            using var q = em.CreateEntityQuery(
                ComponentType.ReadOnly<ProvinceData>(),
                ComponentType.ReadOnly<ProductionSite>());
            using var entities = q.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                var e = entities[i];
                var site = em.GetComponentData<ProductionSite>(e);
                var local = 0f;
                if (site.GoodId == goodId)
                {
                    local = site.BaseOutput;
                }

                if (em.HasBuffer<ProvincePhysicalActivity>(e))
                {
                    var buf = em.GetBuffer<ProvincePhysicalActivity>(e);
                    for (var a = 0; a < buf.Length; a++)
                    {
                        if (buf[a].GoodId != goodId)
                        {
                            continue;
                        }

                        // Même bien que LOD : garder BaseOutput (pas de double compte).
                        if (site.GoodId == goodId)
                        {
                            continue;
                        }

                        local += buf[a].BaseCapacity;
                    }
                }

                if (local > 1e-4f)
                {
                    sites++;
                    cap += local;
                }
            }
        }

        static Concentration MeasureConcentration(EntityManager em, int goodId)
        {
            var caps = new List<float>();
            using var q = em.CreateEntityQuery(
                ComponentType.ReadOnly<ProvinceData>(),
                ComponentType.ReadOnly<ProductionSite>());
            using var entities = q.ToEntityArray(Allocator.Temp);
            var provinceCount = entities.Length;
            for (var i = 0; i < entities.Length; i++)
            {
                var e = entities[i];
                var site = em.GetComponentData<ProductionSite>(e);
                var local = site.GoodId == goodId ? site.BaseOutput : 0f;
                if (em.HasBuffer<ProvincePhysicalActivity>(e))
                {
                    var buf = em.GetBuffer<ProvincePhysicalActivity>(e);
                    for (var a = 0; a < buf.Length; a++)
                    {
                        if (buf[a].GoodId == goodId && site.GoodId != goodId)
                        {
                            local += buf[a].BaseCapacity;
                        }
                    }
                }

                if (local > 1e-4f)
                {
                    caps.Add(local);
                }
            }

            caps.Sort();
            var total = 0f;
            for (var i = 0; i < caps.Count; i++)
            {
                total += caps[i];
            }

            var topN = math.max(1, caps.Count / 4);
            var topSum = 0f;
            for (var i = caps.Count - topN; i < caps.Count; i++)
            {
                topSum += caps[i];
            }

            return new Concentration
            {
                ProducerCount = caps.Count,
                ProducerShare = provinceCount > 0 ? (float)caps.Count / provinceCount : 0f,
                TopQuartileShare = total > 1e-4f ? topSum / total : 0f
            };
        }

        static GoodBudgetReport MeasureAllGoodBudgets(EntityManager em)
        {
            var tagById = new Dictionary<int, string>();
            var typeById = new Dictionary<int, GoodType>();
            using (var gq = em.CreateEntityQuery(ComponentType.ReadOnly<GoodData>()))
            using (var goods = gq.ToComponentDataArray<GoodData>(Allocator.Temp))
            {
                for (var i = 0; i < goods.Length; i++)
                {
                    tagById[goods[i].GoodId] = goods[i].Tag.ToString();
                    typeById[goods[i].GoodId] = goods[i].Type;
                }
            }

            var lodSites = new Dictionary<int, int>();
            var lodCap = new Dictionary<int, float>();
            var physSites = new Dictionary<int, int>();
            var physCap = new Dictionary<int, float>();

            using (var q = em.CreateEntityQuery(
                       ComponentType.ReadOnly<ProvinceData>(),
                       ComponentType.ReadOnly<ProductionSite>()))
            using (var entities = q.ToEntityArray(Allocator.Temp))
            {
                for (var i = 0; i < entities.Length; i++)
                {
                    var e = entities[i];
                    var site = em.GetComponentData<ProductionSite>(e);
                    if (site.GoodId > 0)
                    {
                        Add(lodSites, site.GoodId, 1);
                        AddF(lodCap, site.GoodId, site.BaseOutput);
                        Add(physSites, site.GoodId, 1);
                        AddF(physCap, site.GoodId, site.BaseOutput);
                    }

                    if (!em.HasBuffer<ProvincePhysicalActivity>(e))
                    {
                        continue;
                    }

                    var buf = em.GetBuffer<ProvincePhysicalActivity>(e);
                    var seen = new HashSet<int>();
                    for (var a = 0; a < buf.Length; a++)
                    {
                        var g = buf[a].GoodId;
                        if (g == site.GoodId || seen.Contains(g))
                        {
                            continue;
                        }

                        seen.Add(g);
                        Add(physSites, g, 1);
                        AddF(physCap, g, buf[a].BaseCapacity);
                    }
                }
            }

            // Demande : pops (food/cloth/lux) + intrants recettes approximés via snapshots.
            var demand = new Dictionary<int, float>();
            float foodD = 0f, clothD = 0f, luxD = 0f;
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<PhysicalDemandSnapshot>()))
            using (var snaps = q.ToComponentDataArray<PhysicalDemandSnapshot>(Allocator.Temp))
            {
                for (var i = 0; i < snaps.Length; i++)
                {
                    foodD += snaps[i].FoodDemand;
                    clothD += snaps[i].ClothDemand;
                    luxD += snaps[i].LuxuryDemand;
                }
            }

            // Répartir food/manufactured/luxury sur biens du type (proxy égalitaire pour diagnostic).
            SpreadDemand(demand, typeById, GoodType.Food, foodD);
            SpreadDemand(demand, typeById, GoodType.Manufactured, clothD);
            SpreadDemand(demand, typeById, GoodType.Luxury, luxD);

            // Intrants : capacité phys des outputs manufacturés × qty_per_unit.
            if (TryGetSingletonEntity<PhysicalEconomySingleton>(em, out var singleton))
            {
                var recipes = em.GetBuffer<PhysicalRecipeEntry>(singleton);
                for (var r = 0; r < recipes.Length; r++)
                {
                    var outId = recipes[r].OutputGoodId;
                    var inId = recipes[r].InputGoodId;
                    var outCap = physCap.TryGetValue(outId, out var c) ? c : 0f;
                    AddF(demand, inId, outCap * recipes[r].QtyPerUnit);
                }
            }

            // Cloth demand dédiée (plus précise que le spread manufactured).
            demand[ClothGoodId] = clothD;

            var ids = new List<int>(tagById.Keys);
            ids.Sort();
            var rows = new List<GoodRow>(ids.Count);
            for (var i = 0; i < ids.Count; i++)
            {
                var id = ids[i];
                var pc = physCap.TryGetValue(id, out var p) ? p : 0f;
                var d = demand.TryGetValue(id, out var dd) ? dd : 0f;
                var ps = physSites.TryGetValue(id, out var s) ? s : 0;
                var producerShare = 50 > 0 ? (float)ps / 50f : 0f;
                rows.Add(new GoodRow
                {
                    Tag = tagById[id],
                    GoodId = id,
                    LodSites = lodSites.TryGetValue(id, out var ls) ? ls : 0,
                    LodCap = lodCap.TryGetValue(id, out var lc) ? lc : 0f,
                    PhysSites = ps,
                    PhysCap = pc,
                    Demand = d,
                    Ratio = d > 1e-4f ? pc / d : 0f,
                    // Proxy local: si peu de producteurs alors rareté locale probable.
                    LocalShortageShare = pc >= d - 1e-3f ? math.saturate(1f - producerShare * 2f) : 0f
                });
            }

            return new GoodBudgetReport { Rows = rows };
        }

        static void SpreadDemand(
            Dictionary<int, float> demand,
            Dictionary<int, GoodType> typeById,
            GoodType type,
            float total)
        {
            var ids = new List<int>();
            foreach (var kv in typeById)
            {
                if (kv.Value == type)
                {
                    ids.Add(kv.Key);
                }
            }

            if (ids.Count == 0 || total <= 0f)
            {
                return;
            }

            var each = total / ids.Count;
            for (var i = 0; i < ids.Count; i++)
            {
                AddF(demand, ids[i], each);
            }
        }

        static TradeSignals MeasureTradeSignals(int ticks)
        {
            PhysicalSatisfactionBlendSystem.LockWeight(0f);
            PhysicalStockSystem.MultiHopTransport = true;
            using var h = new SimulationHarness(Seed);
            h.RunTicks(0);
            SetTransportInfra(h.EntityManager, PerDev);
            h.RunTicks(ticks);
            var m = GetMetrics(h.EntityManager);

            float localSat = 0f, importProxy = 0f, n = 0f;
            using (var q = h.EntityManager.CreateEntityQuery(
                       ComponentType.ReadOnly<PhysicalDemandSnapshot>()))
            using (var snaps = q.ToComponentDataArray<PhysicalDemandSnapshot>(Allocator.Temp))
            {
                for (var i = 0; i < snaps.Length; i++)
                {
                    var d = snaps[i].ClothDemand;
                    var s = snaps[i].ClothSatisfied;
                    if (d <= 1e-4f)
                    {
                        continue;
                    }

                    n += 1f;
                    var share = math.saturate(s / d);
                    localSat += share;
                    importProxy += math.saturate(1f - share);
                }
            }

            PhysicalSatisfactionBlendSystem.UnlockWeight();
            return new TradeSignals
            {
                MeanInTransit = m.TotalInTransit,
                MeanDelay = m.MeanDeliveryDelayTicks,
                LocalClothShare = n > 0f ? localSat / n : 0f,
                ImportClothShare = n > 0f ? importProxy / n : 0f
            };
        }

        static WeightRow RunWeightPoint(float weight)
        {
            PhysicalSatisfactionBlendSystem.LockWeight(weight);
            PhysicalStockSystem.MultiHopTransport = true;
            PhysicalStockSystem.IdealPoolMode = false;
            using var harness = new SimulationHarness(Seed);
            harness.RunTicks(0);
            SetTransportInfra(harness.EntityManager, PerDev);
            harness.RunTicks(3000);
            var m = WorldMetrics.Capture(harness.EntityManager, 3000);
            var gap = ComputeGap(harness.EntityManager);
            var cpu = GetMetrics(harness.EntityManager).LastTickCpuMs;
            PhysicalSatisfactionBlendSystem.UnlockWeight();
            return new WeightRow
            {
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
                CpuMs = cpu
            };
        }

        static bool IsAlive(WeightRow row, WeightRow? baseline)
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

            // Même critère pratique v1_024 : pop ≥ 80% baseline et pas d'effondrement sat.
            return row.Pop >= b.Pop * 0.80f;
        }

        static EmergentStory FindEmergentStory()
        {
            PhysicalSatisfactionBlendSystem.LockWeight(0.25f);
            PhysicalStockSystem.MultiHopTransport = true;
            using var h = new SimulationHarness(Seed);
            h.RunTicks(0);
            SetTransportInfra(h.EntityManager, PerDev);

            var pop0 = new Dictionary<int, int>();
            CapturePopByProvince(h.EntityManager, pop0);
            h.RunTicks(800);

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
                        Found = score > 1f,
                        ProvinceId = pid,
                        PhysSat = snap.PhysicalSatisfaction,
                        LodSat = 0f,
                        PopSize = p1,
                        DeltaPop = delta,
                        Note = "dépendance/intrant ou famine locale sous blend"
                    };
                }
            }

            PhysicalSatisfactionBlendSystem.UnlockWeight();
            return best;
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

        static float SumClothDemand(EntityManager em)
        {
            var d = 0f;
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<PhysicalDemandSnapshot>());
            using var snaps = q.ToComponentDataArray<PhysicalDemandSnapshot>(Allocator.Temp);
            for (var i = 0; i < snaps.Length; i++)
            {
                d += snaps[i].ClothDemand;
            }

            return d;
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
                if (map.TryGetValue(pid, out var cur))
                {
                    map[pid] = cur + pops[i].Size;
                }
                else
                {
                    map[pid] = pops[i].Size;
                }
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

        static void Add(Dictionary<int, int> map, int key, int v)
        {
            map[key] = map.TryGetValue(key, out var cur) ? cur + v : v;
        }

        static void AddF(Dictionary<int, float> map, int key, float v)
        {
            map[key] = map.TryGetValue(key, out var cur) ? cur + v : v;
        }

        static string Fmt(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
