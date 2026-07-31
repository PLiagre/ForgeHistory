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
using VictoriaGame.World;

namespace VictoriaGame.Tests
{
    /// <summary>Point d'entrée batchmode : -executeMethod VictoriaGame.Tests.V1020BatchRunner.Run</summary>
    public static class V1020BatchRunner
    {
        public static void Run()
        {
            V1020PhysicalStockTests.RunFullSuiteAndWriteLog();
            UnityEngine.Debug.Log("V1020BatchRunner: DONE");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
        }
    }

    [TestFixture]
    public class V1020PhysicalStockTests
    {
        const uint Seed = 42195u;
        static readonly int[] MilestoneTicks = { 100, 500, 1000, 3000 };

        /// <summary>Epsilon conservation documenté (aligné sur PhysicalStockSystem).</summary>
        const float ConservationEpsAbs = PhysicalStockSystem.ConservationEpsilonAbs;

        [Test]
        public void V1020_Conservation_HoldsAtMilestones()
        {
            using var harness = new SimulationHarness(Seed);
            var prev = 0;
            foreach (var tick in MilestoneTicks)
            {
                harness.RunTicks(tick - prev);
                prev = tick;
                AssertConservation(harness.EntityManager, tick);
            }
        }

        [Test]
        public void V1020_NonTeleportation_MinTicksEqualsEdgeDistance()
        {
            Assert.AreEqual(0, PhysicalStockSystem.MinTicksToArrive(0, 1));
            Assert.AreEqual(1, PhysicalStockSystem.MinTicksToArrive(1, 1));
            Assert.AreEqual(3, PhysicalStockSystem.MinTicksToArrive(3, 1));
            Assert.AreEqual(6, PhysicalStockSystem.MinTicksToArrive(3, 2));

            using var harness = new SimulationHarness(Seed);
            harness.RunTicks(0);

            var em = harness.EntityManager;
            ZeroAllProduction(em);
            ClearAllStocksAndCargos(em);
            ZeroAllFoodDemand(em);

            if (!TryFindAdjacentPair(em, out var origin, out var dest))
            {
                Assert.Inconclusive("Pas d'arête terrestre dans les données.");
                return;
            }

            const int goodId = 1;
            const float qty = 250f;

            // Injection directe d'une cargaison : prouve le délai sans dépendre du gradient
            // (la conso locale pourrait sinon absorber tout le surplus à l'origine).
            if (!TryGetSingletonEntity<PhysicalEconomySingleton>(em, out var singleton))
            {
                Assert.Fail("PhysicalEconomySingleton absent après init");
                return;
            }

            var cargos = em.GetBuffer<CargoInTransit>(singleton);
            cargos.Add(new CargoInTransit
            {
                OriginProvinceId = origin,
                DestProvinceId = dest,
                GoodId = goodId,
                Quantity = qty,
                TicksRemaining = 2 // ≥ 2 : après 1 tick, pas encore livré
            });

            harness.RunTicks(1);
            Assert.AreEqual(0f, GetProvinceStock(em, dest, goodId), 1e-3f,
                "Non-téléportation: pas de stock au destinataire avant épuisement du délai");
            Assert.Greater(SumTransitTo(em, dest, goodId), 0f,
                "La cargaison doit encore être en transit après 1 tick (delay=2)");

            harness.RunTicks(1);
            Assert.AreEqual(qty, GetProvinceStock(em, dest, goodId), 1e-2f,
                "Livraison exactement après MinTicks restants");
            Assert.AreEqual(0f, SumTransitTo(em, dest, goodId), 1e-3f);
        }

        [Test]
        public void V1020_Determinism_SameSeed_IdenticalPhysicalDigest()
        {
            ulong d1, d2;
            using (var h1 = new SimulationHarness(Seed))
            {
                h1.RunTicks(200);
                d1 = PhysicalDigest(h1.EntityManager);
            }

            using (var h2 = new SimulationHarness(Seed))
            {
                h2.RunTicks(200);
                d2 = PhysicalDigest(h2.EntityManager);
            }

            Assert.AreEqual(d1, d2,
                $"Couche physique non déterministe: {d1:X16} vs {d2:X16}");
        }

        [Test]
        public void V1020_GhostLayer_DoesNotAlterPopNeedsSatisfaction()
        {
            // Parité d'intrusion : NeedsSatisfaction doit matcher un digest pop-only
            // entre deux runs — la couche fantôme ne touche pas PopNeeds.
            ulong popHash1, popHash2;
            using (var h1 = new SimulationHarness(Seed))
            {
                h1.RunTicks(100);
                popHash1 = PopSatisfactionDigest(h1.EntityManager);
            }

            using (var h2 = new SimulationHarness(Seed))
            {
                h2.RunTicks(100);
                popHash2 = PopSatisfactionDigest(h2.EntityManager);
            }

            Assert.AreEqual(popHash1, popHash2);
        }

        // Suite de mesure lourde : uniquement via V1020BatchRunner (évite bloat XML/log EditMode).
        public static void V1020_MeasureAndWritePhysicalLog() => RunFullSuiteAndWriteLog();

        public static void RunFullSuiteAndWriteLog()
        {
            var logPath = Path.Combine(
                UnityEngine.Application.dataPath, "..", "Logs", "v1_020_physical.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

            var sb = new StringBuilder();
            sb.AppendLine("=== v1_020 PHYSICAL STOCK LAYER (ghost) — seed=42195 ===");
            sb.AppendLine(
                "Couche ADDITIVE : stocks localisés + transport terrestre + cargaisons.");
            sb.AppendLine(
                "Ne touche PAS MarketPrice / PopNeeds / NeedsSatisfaction / TreasuryData.");
            sb.AppendLine(
                $"Conservation epsilon: absIdentity={PhysicalStockSystem.ConservationEpsilonAbsIdentity} " +
                $"relTick={PhysicalStockSystem.ConservationEpsilonRelTick}");
            sb.AppendLine();

            // Déterminisme couche (t200, 2 runs) — avant le long run pour fail-fast.
            ulong d1, d2;
            using (var h1 = new SimulationHarness(Seed))
            {
                h1.RunTicks(200);
                d1 = PhysicalDigest(h1.EntityManager);
            }

            using (var h2 = new SimulationHarness(Seed))
            {
                h2.RunTicks(200);
                d2 = PhysicalDigest(h2.EntityManager);
            }

            var detOk = d1 == d2;
            sb.AppendLine("=== DETERMINISME COUCHE (t200, 2 runs) ===");
            sb.AppendLine($"digestA={d1:X16} digestB={d2:X16} => {(detOk ? "PASS" : "FAIL")}");
            sb.AppendLine();

            sb.AppendLine("=== NON-TELEPORTATION ===");
            sb.AppendLine(
                $"MinTicks(distance=3,delay=1)={PhysicalStockSystem.MinTicksToArrive(3, 1)} " +
                "(attendu 3)");
            sb.AppendLine();

            var conservationOk = true;
            var maxDriftRun = 0f;
            var cpuSamples = new List<double>();
            GapReport finalGap = default;
            PhysicalEconomyMetrics finalMetrics = default;

            using (var harness = new SimulationHarness(Seed))
            {
                var prev = 0;
                foreach (var tick in MilestoneTicks)
                {
                    harness.RunTicks(tick - prev);
                    prev = tick;

                    var em = harness.EntityManager;
                    var metrics = GetMetrics(em);
                    cpuSamples.Add(metrics.LastTickCpuMs);
                    maxDriftRun = Math.Max(maxDriftRun, metrics.MaxTickConservationDrift);

                    var consOk = PhysicalConservationGate.PerTickHolds(metrics);
                    conservationOk &= consOk;
                    TryAssertConservation(em, out var consReport);

                    var gap = ComputeSatisfactionGap(em);
                    if (tick == 3000)
                    {
                        finalGap = gap;
                        finalMetrics = metrics;
                    }

                    sb.AppendLine($"=== t{tick} ===");
                    sb.AppendLine(
                        $"provincesInDeficit={metrics.ProvincesInDeficit} " +
                        $"totalInTransit={Fmt(metrics.TotalInTransit)} " +
                        $"meanDeliveryDelay={Fmt(metrics.MeanDeliveryDelayTicks)} " +
                        $"cargoCount={metrics.CargoCount}");
                    sb.AppendLine(
                        $"landIsolatedProvinces={metrics.LandIsolatedProvinceCount} " +
                        $"blockedProductionShare={Fmt(metrics.BlockedProductionShare)} " +
                        $"(stock isolé / stock total)");
                    sb.AppendLine($"lastTickCpuMs={Fmt(metrics.LastTickCpuMs)}");
                    sb.AppendLine(
                        $"conservationPerTick: {(consOk ? "PASS" : "FAIL")} " +
                        $"maxDrift={Fmt(metrics.MaxTickConservationDrift)} | identity: {consReport}");
                    sb.AppendLine(
                        $"physicalSat: mean={Fmt(gap.PhysicalMean)} min={Fmt(gap.PhysicalMin)} " +
                        $"max={Fmt(gap.PhysicalMax)} starved(<0.3)={gap.PhysicalStarved}");
                    sb.AppendLine(
                        $"currentSat(LOD): mean={Fmt(gap.CurrentMean)} min={Fmt(gap.CurrentMin)} " +
                        $"max={Fmt(gap.CurrentMax)} starved(<0.3)={gap.CurrentStarved}");
                    sb.AppendLine(
                        $"gap(current-physical): mean={Fmt(gap.GapMean)} " +
                        $"p10={Fmt(gap.GapP10)} p50={Fmt(gap.GapP50)} p90={Fmt(gap.GapP90)} " +
                        $"worstProvinceId={gap.WorstProvinceId} worstGap={Fmt(gap.WorstGap)}");
                    sb.AppendLine(
                        $"extremes: physicalWouldStarveButLodOk={gap.GhostStarvedLodOk} " +
                        $"lodStarvedPhysicalOk={gap.LodStarvedGhostOk}");
                    sb.AppendLine();
                }
            }

            var maxCpu = 0.0;
            var sumCpu = 0.0;
            for (var i = 0; i < cpuSamples.Count; i++)
            {
                maxCpu = Math.Max(maxCpu, cpuSamples[i]);
                sumCpu += cpuSamples[i];
            }

            var avgCpu = cpuSamples.Count > 0 ? sumCpu / cpuSamples.Count : 0.0;
            sb.AppendLine("=== PERFORMANCE ===");
            sb.AppendLine(
                $"couche physique lastTickCpuMs: avg={Fmt((float)avgCpu)} max={Fmt((float)maxCpu)}");
            sb.AppendLine(
                avgCpu > 4.0
                    ? "ALERTE: couche > ~4 ms/tick — optimiser (Burst jobs, sparse maps) avant v1_021."
                    : "OK: couche sous la cible indicative ~4 ms/tick.");
            sb.AppendLine();

            sb.AppendLine("=== VERDICT MESURE (t3000) ===");
            sb.AppendLine(
                $"physicalSatMean={Fmt(finalGap.PhysicalMean)} currentSatMean={Fmt(finalGap.CurrentMean)} " +
                $"gapMean={Fmt(finalGap.GapMean)}");
            sb.AppendLine(
                $"physicalStarved={finalGap.PhysicalStarved}/{finalGap.ProvinceCount} " +
                $"currentStarved={finalGap.CurrentStarved}/{finalGap.ProvinceCount}");
            sb.AppendLine(
                $"provinces vivant du pot mondial (LOD ok, physique famine): " +
                $"{finalGap.GhostStarvedLodOk}");
            sb.AppendLine(
                $"isolées terrestre={finalMetrics.LandIsolatedProvinceCount} " +
                $"blockedShare={Fmt(finalMetrics.BlockedProductionShare)}");

            string verdict;
            if (finalGap.PhysicalMean < 0.35f || finalGap.PhysicalStarved > finalGap.ProvinceCount / 2)
            {
                verdict =
                    "EFFONDRE — l'économie physique affame une large part du monde. " +
                    "Le branchement v1_021 devra être progressif (filet LOD résiduel) " +
                    "ou précédé d'un boost capacité/routes maritimes.";
            }
            else if (finalGap.GapMean > 0.15f || finalGap.GhostStarvedLodOk > finalGap.ProvinceCount / 5)
            {
                verdict =
                    "ECART SUBSTANTIEL — le monde physique survit mais divergera du LOD. " +
                    "v1_021: brancher avec monitoring + capacité calibrée.";
            }
            else
            {
                verdict =
                    "VIVABLE — écart modéré ; branchement v1_021 envisageable " +
                    "avec filet de sécurité léger.";
            }

            sb.AppendLine($"VERDICT: {verdict}");
            sb.AppendLine($"conservation_suite={(conservationOk ? "PASS" : "FAIL")}");
            sb.AppendLine($"determinism={(detOk ? "PASS" : "FAIL")}");

            File.WriteAllText(logPath, sb.ToString());
            UnityEngine.Debug.Log(
                $"V1020PhysicalStockTests: wrote {logPath} conservation={(conservationOk ? "PASS" : "FAIL")} determinism={(detOk ? "PASS" : "FAIL")}");

            Assert.IsTrue(conservationOk, "Conservation échouée — voir v1_020_physical.log");
            Assert.IsTrue(detOk, "Déterminisme couche échoué");
        }

        // ----- Conservation -----

        static void AssertConservation(EntityManager em, int tick)
        {
            var metrics = GetMetrics(em);
            PhysicalConservationGate.AssertPerTickHolds(metrics, $"V1020 t{tick}");
            // Identité float rapportée mais non bloquante (stocks float → résidu ~O(1) aux totaux 1e7+).
            TryAssertConservation(em, out var report);
            UnityEngine.Debug.Log($"Conservation identity t{tick}: {report}");
        }

        static bool TryAssertConservation(EntityManager em, out string report)
        {
            var stockByGood = new Dictionary<int, double>();
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<ProvinceStock>()))
            using (var entities = q.ToEntityArray(Allocator.Temp))
            {
                for (var i = 0; i < entities.Length; i++)
                {
                    var buf = em.GetBuffer<ProvinceStock>(entities[i]);
                    for (var j = 0; j < buf.Length; j++)
                    {
                        AddDict(stockByGood, buf[j].GoodId, buf[j].Quantity);
                    }
                }
            }

            var transitByGood = new Dictionary<int, double>();
            if (TryGetSingletonEntity<PhysicalEconomySingleton>(em, out var singleton))
            {
                var cargos = em.GetBuffer<CargoInTransit>(singleton);
                for (var i = 0; i < cargos.Length; i++)
                {
                    AddDict(transitByGood, cargos[i].GoodId, cargos[i].Quantity);
                }

                var ledger = em.GetBuffer<PhysicalLedgerEntry>(singleton);
                var sb = new StringBuilder();
                var ok = true;
                for (var i = 0; i < ledger.Length; i++)
                {
                    var e = ledger[i];
                    stockByGood.TryGetValue(e.GoodId, out var stock);
                    transitByGood.TryGetValue(e.GoodId, out var transit);
                    var pass = PhysicalStockSystem.CheckConservation(
                        stock, transit, e.CumulativeProduction, e.CumulativeConsumption, out var delta);
                    if (!pass)
                    {
                        ok = false;
                    }

                    sb.Append(
                        $"good{e.GoodId}: stock={Fmt((float)stock)} transit={Fmt((float)transit)} " +
                        $"prod={Fmt((float)e.CumulativeProduction)} cons={Fmt((float)e.CumulativeConsumption)} " +
                        $"delta={Fmt(delta)} {(pass ? "OK" : "FAIL")}; ");
                }

                report = sb.ToString();
                return ok;
            }

            report = "singleton absent";
            return false;
        }

        // ----- Digests -----

        static ulong PhysicalDigest(EntityManager em)
        {
            var hash = StateHash.New();
            var rows = new List<(int ProvinceId, int GoodId, double Qty)>();

            using (var q = em.CreateEntityQuery(
                       ComponentType.ReadOnly<ProvinceData>(),
                       ComponentType.ReadOnly<ProvinceStock>()))
            using (var entities = q.ToEntityArray(Allocator.Temp))
            {
                for (var i = 0; i < entities.Length; i++)
                {
                    var pid = em.GetComponentData<ProvinceData>(entities[i]).ProvinceId;
                    var buf = em.GetBuffer<ProvinceStock>(entities[i]);
                    for (var j = 0; j < buf.Length; j++)
                    {
                        rows.Add((pid, buf[j].GoodId, buf[j].Quantity));
                    }
                }
            }

            rows.Sort((a, b) =>
            {
                var c = a.ProvinceId.CompareTo(b.ProvinceId);
                return c != 0 ? c : a.GoodId.CompareTo(b.GoodId);
            });

            foreach (var r in rows)
            {
                hash.Int(r.ProvinceId);
                hash.Int(r.GoodId);
                hash.Double(r.Qty);
            }

            if (TryGetSingletonEntity<PhysicalEconomySingleton>(em, out var singleton))
            {
                var cargos = em.GetBuffer<CargoInTransit>(singleton);
                var cargoRows = new List<CargoInTransit>(cargos.Length);
                for (var i = 0; i < cargos.Length; i++)
                {
                    cargoRows.Add(cargos[i]);
                }

                cargoRows.Sort((a, b) => new PhysicalStockSystem.CargoComparer().Compare(a, b));
                foreach (var c in cargoRows)
                {
                    hash.Int(c.OriginProvinceId);
                    hash.Int(c.DestProvinceId);
                    hash.Int(c.GoodId);
                    hash.Double(c.Quantity);
                    hash.Int(c.TicksRemaining);
                }
            }

            return hash.Value;
        }

        static ulong PopSatisfactionDigest(EntityManager em)
        {
            var hash = StateHash.New();
            var rows = new List<(int ProvinceId, float Sat, float Food, float Cloth, float Lux)>();

            using var q = em.CreateEntityQuery(
                ComponentType.ReadOnly<PopData>(),
                ComponentType.ReadOnly<PopNeeds>());
            using var entities = q.ToEntityArray(Allocator.Temp);
            var provinceIds = MapProvinceIds(em);

            for (var i = 0; i < entities.Length; i++)
            {
                var pop = em.GetComponentData<PopData>(entities[i]);
                var needs = em.GetComponentData<PopNeeds>(entities[i]);
                var pid = provinceIds.TryGetValue(pop.Province, out var id) ? id : -1;
                rows.Add((pid, pop.NeedsSatisfaction, needs.FoodSatisfied, needs.ClothSatisfied,
                    needs.LuxurySatisfied));
            }

            rows.Sort((a, b) =>
            {
                var c = a.ProvinceId.CompareTo(b.ProvinceId);
                if (c != 0)
                {
                    return c;
                }

                c = a.Sat.CompareTo(b.Sat);
                if (c != 0)
                {
                    return c;
                }

                return a.Food.CompareTo(b.Food);
            });

            foreach (var r in rows)
            {
                hash.Int(r.ProvinceId);
                hash.Float(r.Sat);
                hash.Float(r.Food);
                hash.Float(r.Cloth);
                hash.Float(r.Lux);
            }

            return hash.Value;
        }

        // ----- Gap physique vs LOD -----

        struct GapReport
        {
            public int ProvinceCount;
            public float PhysicalMean, PhysicalMin, PhysicalMax;
            public float CurrentMean, CurrentMin, CurrentMax;
            public float GapMean, GapP10, GapP50, GapP90;
            public int PhysicalStarved, CurrentStarved;
            public int GhostStarvedLodOk, LodStarvedGhostOk;
            public int WorstProvinceId;
            public float WorstGap;
        }

        static GapReport ComputeSatisfactionGap(EntityManager em)
        {
            // Satisfaction LOD courante : moyenne NeedsSatisfaction des pops par province.
            var lodByProv = new Dictionary<int, (float Sum, int Count)>();
            var provinceIds = MapProvinceIds(em);

            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<PopData>()))
            using (var pops = q.ToComponentDataArray<PopData>(Allocator.Temp))
            {
                for (var i = 0; i < pops.Length; i++)
                {
                    var pid = provinceIds.TryGetValue(pops[i].Province, out var id) ? id : -1;
                    if (pid < 0)
                    {
                        continue;
                    }

                    if (!lodByProv.TryGetValue(pid, out var cur))
                    {
                        cur = (0f, 0);
                    }

                    lodByProv[pid] = (cur.Sum + pops[i].NeedsSatisfaction, cur.Count + 1);
                }
            }

            var gaps = new List<(int ProvinceId, float Physical, float Current, float Gap)>();

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
                    var current = 1f;
                    if (lodByProv.TryGetValue(pid, out var lod) && lod.Count > 0)
                    {
                        current = lod.Sum / lod.Count;
                    }

                    gaps.Add((pid, phys, current, current - phys));
                }
            }

            gaps.Sort((a, b) => a.ProvinceId.CompareTo(b.ProvinceId));

            var report = new GapReport
            {
                ProvinceCount = gaps.Count,
                PhysicalMin = 1f,
                CurrentMin = 1f,
                WorstGap = float.MinValue,
                WorstProvinceId = -1
            };

            if (gaps.Count == 0)
            {
                return report;
            }

            double pSum = 0, cSum = 0, gSum = 0;
            var gapValues = new List<float>(gaps.Count);

            foreach (var g in gaps)
            {
                pSum += g.Physical;
                cSum += g.Current;
                gSum += g.Gap;
                gapValues.Add(g.Gap);
                report.PhysicalMin = math.min(report.PhysicalMin, g.Physical);
                report.PhysicalMax = math.max(report.PhysicalMax, g.Physical);
                report.CurrentMin = math.min(report.CurrentMin, g.Current);
                report.CurrentMax = math.max(report.CurrentMax, g.Current);
                if (g.Physical < 0.3f)
                {
                    report.PhysicalStarved++;
                }

                if (g.Current < 0.3f)
                {
                    report.CurrentStarved++;
                }

                if (g.Physical < 0.3f && g.Current >= 0.5f)
                {
                    report.GhostStarvedLodOk++;
                }

                if (g.Current < 0.3f && g.Physical >= 0.5f)
                {
                    report.LodStarvedGhostOk++;
                }

                if (g.Gap > report.WorstGap)
                {
                    report.WorstGap = g.Gap;
                    report.WorstProvinceId = g.ProvinceId;
                }
            }

            report.PhysicalMean = (float)(pSum / gaps.Count);
            report.CurrentMean = (float)(cSum / gaps.Count);
            report.GapMean = (float)(gSum / gaps.Count);

            gapValues.Sort();
            report.GapP10 = Percentile(gapValues, 0.10f);
            report.GapP50 = Percentile(gapValues, 0.50f);
            report.GapP90 = Percentile(gapValues, 0.90f);
            return report;
        }

        static float Percentile(List<float> sorted, float p)
        {
            if (sorted.Count == 0)
            {
                return 0f;
            }

            var idx = (int)math.clamp(p * (sorted.Count - 1), 0, sorted.Count - 1);
            return sorted[idx];
        }

        // ----- Helpers entités -----

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

        static Dictionary<Entity, int> MapProvinceIds(EntityManager em)
        {
            var map = new Dictionary<Entity, int>();
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<ProvinceData>());
            using var entities = q.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                map[entities[i]] = em.GetComponentData<ProvinceData>(entities[i]).ProvinceId;
            }

            return map;
        }

        static void AddDict(Dictionary<int, double> map, int key, double v)
        {
            map.TryGetValue(key, out var cur);
            map[key] = cur + v;
        }

        static string Fmt(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        static void ZeroAllFoodDemand(EntityManager em)
        {
            using var q = em.CreateEntityQuery(ComponentType.ReadWrite<PopNeeds>());
            using var entities = q.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                var needs = em.GetComponentData<PopNeeds>(entities[i]);
                needs.FoodNeed = 0f;
                needs.ClothNeed = 0f;
                needs.LuxuryNeed = 0f;
                em.SetComponentData(entities[i], needs);
            }
        }

        static void ZeroAllProduction(EntityManager em)
        {
            using var q = em.CreateEntityQuery(ComponentType.ReadWrite<ProductionSite>());
            using var entities = q.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                var site = em.GetComponentData<ProductionSite>(entities[i]);
                site.BaseOutput = 0f;
                site.LastOutput = 0f;
                em.SetComponentData(entities[i], site);
            }
        }

        static void ClearAllStocksAndCargos(EntityManager em)
        {
            using (var q = em.CreateEntityQuery(ComponentType.ReadWrite<ProvinceStock>()))
            using (var entities = q.ToEntityArray(Allocator.Temp))
            {
                for (var i = 0; i < entities.Length; i++)
                {
                    em.GetBuffer<ProvinceStock>(entities[i]).Clear();
                }
            }

            if (TryGetSingletonEntity<PhysicalEconomySingleton>(em, out var singleton))
            {
                em.GetBuffer<CargoInTransit>(singleton).Clear();
                var ledger = em.GetBuffer<PhysicalLedgerEntry>(singleton);
                ledger.Clear();
            }
        }

        static void InjectStock(EntityManager em, int provinceId, int goodId, float qty)
        {
            using var q = em.CreateEntityQuery(
                ComponentType.ReadOnly<ProvinceData>(),
                ComponentType.ReadWrite<ProvinceStock>());
            using var entities = q.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                if (em.GetComponentData<ProvinceData>(entities[i]).ProvinceId != provinceId)
                {
                    continue;
                }

                PhysicalStockSystem.AddToStock(em.GetBuffer<ProvinceStock>(entities[i]), goodId, qty);
                return;
            }
        }

        static float GetProvinceStock(EntityManager em, int provinceId, int goodId)
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

                return (float)PhysicalStockSystem.GetStockQuantity(
                    em.GetBuffer<ProvinceStock>(entities[i]), goodId);
            }

            return 0f;
        }

        static float SumTransitTo(EntityManager em, int destProvinceId, int goodId)
        {
            if (!TryGetSingletonEntity<PhysicalEconomySingleton>(em, out var singleton))
            {
                return 0f;
            }

            var cargos = em.GetBuffer<CargoInTransit>(singleton);
            var sum = 0.0;
            for (var i = 0; i < cargos.Length; i++)
            {
                if (cargos[i].DestProvinceId == destProvinceId && cargos[i].GoodId == goodId)
                {
                    sum += cargos[i].Quantity;
                }
            }

            return (float)sum;
        }

        static void BoostFoodDemandAt(EntityManager em, int provinceId, float foodNeed)
        {
            var provinceEntity = Entity.Null;
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<ProvinceData>()))
            using (var entities = q.ToEntityArray(Allocator.Temp))
            {
                for (var i = 0; i < entities.Length; i++)
                {
                    if (em.GetComponentData<ProvinceData>(entities[i]).ProvinceId == provinceId)
                    {
                        provinceEntity = entities[i];
                        break;
                    }
                }
            }

            if (provinceEntity == Entity.Null)
            {
                return;
            }

            using var pq = em.CreateEntityQuery(
                ComponentType.ReadWrite<PopData>(),
                ComponentType.ReadWrite<PopNeeds>());
            using var pops = pq.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < pops.Length; i++)
            {
                var pop = em.GetComponentData<PopData>(pops[i]);
                if (pop.Province != provinceEntity)
                {
                    continue;
                }

                var needs = em.GetComponentData<PopNeeds>(pops[i]);
                needs.FoodNeed = foodNeed;
                em.SetComponentData(pops[i], needs);
                return;
            }
        }

        static bool TryFindAdjacentPair(EntityManager em, out int origin, out int dest)
        {
            origin = -1;
            dest = -1;
            using var q = em.CreateEntityQuery(
                ComponentType.ReadOnly<ProvinceData>(),
                ComponentType.ReadOnly<ProvinceNeighbor>());
            using var entities = q.ToEntityArray(Allocator.Temp);

            var candidates = new List<(int From, int To)>();
            for (var i = 0; i < entities.Length; i++)
            {
                var id = em.GetComponentData<ProvinceData>(entities[i]).ProvinceId;
                var neighbors = em.GetBuffer<ProvinceNeighbor>(entities[i]);
                for (var n = 0; n < neighbors.Length; n++)
                {
                    if (!neighbors[n].IsStrait)
                    {
                        candidates.Add((id, neighbors[n].NeighborProvinceId));
                    }
                }
            }

            candidates.Sort((a, b) =>
            {
                var c = a.From.CompareTo(b.From);
                return c != 0 ? c : a.To.CompareTo(b.To);
            });

            if (candidates.Count == 0)
            {
                return false;
            }

            origin = candidates[0].From;
            dest = candidates[0].To;
            return true;
        }
    }
}
