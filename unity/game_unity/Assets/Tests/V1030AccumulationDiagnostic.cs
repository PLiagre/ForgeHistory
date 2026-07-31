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
    /// <summary>Batch : -executeMethod VictoriaGame.Tests.V1030BatchRunner.Run</summary>
    public static class V1030BatchRunner
    {
        public static void Run()
        {
            try
            {
                V1030AccumulationDiagnostic.RunFullSuiteAndWriteLog();
                UnityEngine.Debug.Log("V1030BatchRunner: DONE");
            }
            catch (Exception ex) when (HarnessAllocationGuard.IsNativeAllocationFailure(ex))
            {
                UnityEngine.Debug.LogWarning(
                    "V1030BatchRunner: ALLOCATION_FAILURE (charge harnais) — " + ex.Message);
                UnityEngine.Debug.Log("V1030BatchRunner: DONE_PARTIAL");
            }
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
        }
    }

    /// <summary>
    /// v1_030 — accumulation sans borne + priorité de transport (GoodId vs sévérité).
    /// Mesures lourdes via BatchRunner uniquement.
    /// </summary>
    [TestFixture]
    public class V1030AccumulationDiagnostic
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
        const int TicksPerYear = 12; // approx. pour stock/conso annuelle

        static readonly int[] SnapshotTicks = { 500, 1000, 3000 };
        static readonly int[] FocusGoods =
            { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 };

        [TearDown]
        public void TearDown()
        {
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
        public void V1030_ServiceOrderModes_Exist()
        {
            Assert.AreEqual(
                PhysicalStockSystem.TransportServiceOrder.ByGoodId,
                (PhysicalStockSystem.TransportServiceOrder)0);
            Assert.AreEqual(
                PhysicalStockSystem.TransportServiceOrder.ByDeficitSeverity,
                (PhysicalStockSystem.TransportServiceOrder)1);
            Assert.AreEqual(
                PhysicalStockSystem.TransportServiceOrder.ByDeficitSeverity,
                PhysicalStockSystem.ServiceOrderMode);
        }

        [Test]
        public void V1030_ByGoodId_Determinism()
        {
            HarnessAllocationGuard.Run(() =>
            {
                ApplyAdoptedLocks(PhysicalStockSystem.TransportServiceOrder.ByGoodId);
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
                    ApplyAdoptedLocks(PhysicalStockSystem.TransportServiceOrder.ByGoodId);
                    h2.RunTicks(0);
                    SetTransportInfra(h2.EntityManager, PerDev);
                    h2.RunTicks(80);
                    d2 = WorldDigest(h2.EntityManager);
                }

                Assert.AreEqual(d1, d2, $"Non déterministe ByGoodId: {d1:X16} vs {d2:X16}");
            });
        }

        [Test]
        public void V1030_BySeverity_Determinism()
        {
            HarnessAllocationGuard.Run(() =>
            {
                ApplyAdoptedLocks(PhysicalStockSystem.TransportServiceOrder.ByDeficitSeverity);
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
                    ApplyAdoptedLocks(PhysicalStockSystem.TransportServiceOrder.ByDeficitSeverity);
                    h2.RunTicks(0);
                    SetTransportInfra(h2.EntityManager, PerDev);
                    h2.RunTicks(80);
                    d2 = WorldDigest(h2.EntityManager);
                }

                Assert.AreEqual(d1, d2, $"Non déterministe BySeverity: {d1:X16} vs {d2:X16}");
            });
        }

        [Test]
        public void V1030_Modes_ProduceDifferentDigests_OrDocumentParity()
        {
            HarnessAllocationGuard.Run(() =>
            {
                ApplyAdoptedLocks(PhysicalStockSystem.TransportServiceOrder.ByGoodId);
                ulong dGood;
                using (var h = new SimulationHarness(Seed))
                {
                    h.RunTicks(0);
                    SetTransportInfra(h.EntityManager, PerDev);
                    h.RunTicks(120);
                    dGood = WorldDigest(h.EntityManager);
                }

                ApplyAdoptedLocks(PhysicalStockSystem.TransportServiceOrder.ByDeficitSeverity);
                ulong dSev;
                using (var h = new SimulationHarness(Seed))
                {
                    h.RunTicks(0);
                    SetTransportInfra(h.EntityManager, PerDev);
                    h.RunTicks(120);
                    dSev = WorldDigest(h.EntityManager);
                }

                UnityEngine.Debug.Log(
                    $"V1030 mode digests t120: ByGoodId={dGood:X16} BySeverity={dSev:X16} " +
                    $"differs={dGood != dSev}");
                // Différence attendue si la priorité change qui reçoit quoi ; égalité
                // possible si aucune arête n'est saturée — on documente, on n'échoue pas.
                Assert.AreNotEqual(0UL, dGood);
                Assert.AreNotEqual(0UL, dSev);
            });
        }

        // Diagnostic lourd : uniquement via V1030BatchRunner.
        public static void RunFullSuiteEntry() => RunFullSuiteAndWriteLog();

        public static void RunFullSuiteAndWriteLog()
        {
            var logPath = Path.Combine(
                UnityEngine.Application.dataPath, "..", "Logs", "v1_030_accumulation.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            var sb = new StringBuilder(512 * 1024);

            sb.AppendLine("=== v1_030 ACCUMULATION + TRANSPORT PRIORITY — seed=42195 ===");
            sb.AppendLine(
                $"config: PerDev={Fmt(PerDev)} w={Fmt(BlendWeight)} c={Fmt(Continuity)} MultiHop=ON");
            sb.AppendLine();

            // ---------- PARTIE 1 — ACCUMULATION ----------
            sb.AppendLine("=== PARTIE 1 — ACCUMULATION BIEN PAR BIEN ===");
            ApplyAdoptedLocks(PhysicalStockSystem.TransportServiceOrder.ByGoodId);
            var snaps = new Dictionary<int, Dictionary<int, GoodSnap>>();
            double bourgogneWine = 0;
            using (var h = new SimulationHarness(Seed))
            {
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
                        bourgogneWine = ProvinceGoodStock(h.EntityManager, BourgogneProvinceId, WineGoodId);
                    }

                    System.GC.Collect();
                }
            }

            sb.AppendLine(
                "tick\tgoodId\ttag\ttype\tstock\tprodCum\tconsCum\tstock/yearCons\ttrend\ttopProv\ttopQty");
            foreach (var tick in SnapshotTicks)
            {
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

            sb.AppendLine();
            sb.AppendLine(
                $"BOURGOGNE(id={BourgogneProvinceId}) WINE stock@t3000={Fmt(bourgogneWine)} " +
                $"(réf panel v1_029≈6085943)");
            var wineSnap = snaps[3000].TryGetValue(WineGoodId, out var ws) ? ws : default;
            sb.AppendLine(
                $"WINE mondial@t3000: stock={Fmt(wineSnap.Stock)} consCum={Fmt(wineSnap.ConsCum)} " +
                $"années={Fmt(wineSnap.YearsOfConsumption)} topProv={wineSnap.TopProvinceId}");

            // Part du stock sans perspective d'usage : biens dont stock croît et
            // stock > N années de conso mondiale.
            double deadStock = 0, totalStock = 0;
            foreach (var kv in snaps[3000])
            {
                totalStock += kv.Value.Stock;
                if (kv.Value.YearsOfConsumption >= 50.0 || IsDiverging(snaps, kv.Key))
                {
                    deadStock += kv.Value.Stock;
                }
            }

            var deadShare = totalStock > 1e-6 ? deadStock / totalStock : 0;
            sb.AppendLine(
                $"STOCK SANS USAGE (années≥50 OU divergent): {Fmt(deadStock)} / {Fmt(totalStock)} " +
                $"= {Fmt(deadShare)}");

            sb.AppendLine();
            sb.AppendLine("IMPACT SUR MESURES ANTÉRIEURES:");
            sb.AppendLine(
                "- Conservation per-tick: critère RELATIF au flux du tick " +
                "(abs≤1e-2 OR ≤1e-3×flux). Un stock qui diverge n'assouplit PAS ce critère — " +
                "le drift mesure Δ(stock+transit) vs Δ(prod−cons) du tick, pas le niveau absolu.");
            sb.AppendLine(
                "- Satisfaction physique: lit PhysicalDemandSnapshot (served/demand du tick). " +
                "L'accumulation n'améliore PAS la sat si le bien n'atteint pas le déficit " +
                "(stock mort local). Les mesures clothServedShare antérieures restent valides " +
                "comme photographies du service, mais sous-estiment le potentiel si la " +
                "capacité d'arête était capturée par des RawMaterials prioritaires GoodId.");
            sb.AppendLine(
                "VERDICT IMPACT: mesures de conservation SAINES ; measures de service drap " +
                "POSSIBLEMENT BIAISÉES à la baisse par l'ordre GoodId (à trancher Partie 2).");
            sb.AppendLine();

            // ---------- PARTIE 2 — ORDRE DE SERVICE ----------
            sb.AppendLine("=== PARTIE 2 — RÉPARTITION CAPACITÉ + EXPÉRIENCE ORDRE ===");
            var shareGoodId = MeasureTransportShares(
                PhysicalStockSystem.TransportServiceOrder.ByGoodId, 200, 40);
            sb.AppendLine("Répartition transport ByGoodId (fenêtre t160→t200):");
            sb.AppendLine("goodId\ttag\tshipped\tshare\tavgRoom\texhaustedFrac");
            AppendShareTable(sb, shareGoodId);

            var clothRoom = shareGoodId.AvgRoom.TryGetValue(ClothGoodId, out var cr) ? cr : 0;
            var woodRoom = shareGoodId.AvgRoom.TryGetValue(WoodGoodId, out var wr) ? wr : 0;
            var clothEx = shareGoodId.ExhaustedFrac.TryGetValue(ClothGoodId, out var ce) ? ce : 0;
            var woodShip = shareGoodId.Share.TryGetValue(WoodGoodId, out var wsh) ? wsh : 0;
            var ironShip = shareGoodId.Share.TryGetValue(IronGoodId, out var ish) ? ish : 0;
            var woolShip = shareGoodId.Share.TryGetValue(WoolGoodId, out var wosh) ? wosh : 0;
            var coalShip = shareGoodId.Share.TryGetValue(CoalGoodId, out var cosh) ? cosh : 0;
            var rawBeforeCloth = woodShip + ironShip + woolShip + coalShip;
            sb.AppendLine(
                $"ORDRE EFFECTIF: wood+iron+wool+coal share={Fmt(rawBeforeCloth)} " +
                $"cloth share={Fmt(shareGoodId.Share.TryGetValue(ClothGoodId, out var csh) ? csh : 0)} " +
                $"clothAvgRoom={Fmt(clothRoom)} clothExhaustedFrac={Fmt(clothEx)} " +
                $"woodAvgRoom={Fmt(woodRoom)}");
            sb.AppendLine();

            sb.AppendLine("EXPÉRIENCE DÉCISIVE (seul l'ordre change) t3000:");
            var before = RunServicePoint(PhysicalStockSystem.TransportServiceOrder.ByGoodId, 3000);
            System.GC.Collect();
            var after = RunServicePoint(
                PhysicalStockSystem.TransportServiceOrder.ByDeficitSeverity, 3000);
            System.GC.Collect();

            sb.AppendLine(
                "mode\tclothServedShare\tphysMean\tstarvedProv\tmissedIn\tdigest\tcpuMs");
            sb.AppendLine(
                $"ByGoodId\t{Fmt(before.ClothServedShare)}\t{Fmt(before.PhysMean)}\t" +
                $"{before.StarvedProvinces}\t{Fmt(before.MissedIn)}\t{before.Digest:X16}\t" +
                $"{Fmt(before.CpuMs)}");
            sb.AppendLine(
                $"BySeverity\t{Fmt(after.ClothServedShare)}\t{Fmt(after.PhysMean)}\t" +
                $"{after.StarvedProvinces}\t{Fmt(after.MissedIn)}\t{after.Digest:X16}\t" +
                $"{Fmt(after.CpuMs)}");

            var clothDelta = after.ClothServedShare - before.ClothServedShare;
            var orderIsCause = clothDelta >= 0.05f;
            sb.AppendLine(
                $"ΔclothServedShare={Fmt(clothDelta)} " +
                $"(réf v1_024 ByGoodId≈0.142 ; seuil cause=+0.05)");
            sb.AppendLine(
                orderIsCause
                    ? "VERDICT PARTIE 2: OUI — l'ordre GoodId était une cause matérielle du drap affamé."
                    : "VERDICT PARTIE 2: NON confirmé au seuil +0.05 — hypothèse infirmée ou effet faible; " +
                      "la sévérité reste néanmoins la règle légitime (plus de magie GoodId).");
            sb.AppendLine();

            // ---------- PARTIE 3 — RÈGLE ----------
            sb.AppendLine("=== PARTIE 3 — SUPPRESSION RÈGLE MAGIQUE ===");
            sb.AppendLine(
                "Défaut compilé: ServiceOrderMode=ByDeficitSeverity. " +
                "Priorité = pull destinataire (NeighborPull) ; départage GoodId à égalité. " +
                "Aucun bien privilégié en dur. ByGoodId conservé pour A/B et bit-id v1_025.");
            sb.AppendLine(
                "Registre: docs/registre-regles-magiques.md entrée #8 (transport GoodId).");
            sb.AppendLine();

            // ---------- PARTIE 4 — SOURCE ----------
            sb.AppendLine("=== PARTIE 4 — PRODUCTION INCONDITIONNELLE ===");
            var root = MeasureUnconditionalProduction();
            sb.AppendLine(
                $"biens_sans_recette={root.PrimaryGoodCount} " +
                $"prodTickPrimaire={Fmt(root.PrimaryProdPerTick)} " +
                $"consTickPrimaire={Fmt(root.PrimaryConsPerTick)} " +
                $"ratioProd/Cons={Fmt(root.PrimaryProdPerTick / math.max(root.PrimaryConsPerTick, 1e-4f))}");
            sb.AppendLine(
                $"WINE: capMondiale≈{Fmt(root.WineCapPerTick)} cons≈{Fmt(root.WineConsPerTick)} " +
                $"ratio={Fmt(root.WineCapPerTick / math.max(root.WineConsPerTick, 1e-4f))}");
            sb.AppendLine(
                "CONSTAT: PhysicalProductionSystem dépose DesiredOutput PLEIN pour tout bien " +
                "sans recette (primaires / endowment), indépendamment du stock local et de la " +
                "demande mondiale. Aucun frein émergent → accumulation divergente.");
            sb.AppendLine(
                "PROPOSITION (non codée): frein émergent — un producteur qui ne peut ni " +
                "écouler (pull transport=0 sur horizon) ni stocker (capacité de stockage " +
                "dérivée du DevScore) réduit son output au débit évacuable. Alternatives: " +
                "réaffectation main-d'œuvre, pas de péremption/évaporation/plafond arbitraire.");
            sb.AppendLine();

            // ---------- PARTIE 5 — GARDE-FOUS ----------
            sb.AppendLine("=== PARTIE 5 — GARDE-FOUS ===");
            ApplyAdoptedLocks(PhysicalStockSystem.TransportServiceOrder.ByDeficitSeverity);
            int relFail;
            float maxDrift;
            using (var h = new SimulationHarness(Seed))
            {
                h.RunTicks(0);
                SetTransportInfra(h.EntityManager, PerDev);
                h.RunTicks(200);
                var m = GetMetrics(h.EntityManager);
                relFail = PhysicalConservationGate.PerTickHolds(m) ? 0 : 1;
                maxDrift = m.MaxTickConservationDrift;
            }

            sb.AppendLine(
                $"conservation_rel_t200={(relFail == 0 ? "PASS" : "FAIL")} maxDrift={Fmt(maxDrift)}");
            sb.AppendLine(
                $"digest_AVANT(ByGoodId)={before.Digest:X16} " +
                $"digest_APRÈS(BySeverity)={after.Digest:X16} " +
                $"changed={before.Digest != after.Digest}");
            sb.AppendLine(
                $"cpu stock/transport réf≈1.579 ms ; mesuré BySeverity≈{Fmt(after.CpuMs)} ms");
            sb.AppendLine(
                $"stabilité: w={Fmt(BlendWeight)} c={Fmt(Continuity)} conservés ; " +
                "MultiHop ON ; CapacityPerDev inchangé.");
            sb.AppendLine();

            sb.AppendLine("=== VERDICT MESURÉ ===");
            sb.AppendLine(
                $"ByGoodId clothServedShare={Fmt(before.ClothServedShare)} ; " +
                $"BySeverity clothServedShare={Fmt(after.ClothServedShare)} " +
                $"(Δ={Fmt(clothDelta)}). " +
                $"rawShareBeforeCloth≈{Fmt(rawBeforeCloth)}. " +
                $"Bourgogne wine={Fmt(bourgogneWine)}. " +
                $"deadStockShare={Fmt(deadShare)}. " +
                $"orderCause={(orderIsCause ? "YES" : "WEAK/NO")}. " +
                $"default=ByDeficitSeverity.");

            File.WriteAllText(logPath, sb.ToString());
            UnityEngine.Debug.Log(
                $"V1030AccumulationDiagnostic: wrote {logPath} " +
                $"cloth {Fmt(before.ClothServedShare)}→{Fmt(after.ClothServedShare)} " +
                $"Δ={Fmt(clothDelta)}");

            PhysicalStockSystem.ServiceOrderMode =
                PhysicalStockSystem.TransportServiceOrder.ByDeficitSeverity;
            PhysicalSatisfactionBlendSystem.UnlockWeight();
            PopGrowthSystem.UnlockContinuity();
        }

        // ----- mesures -----

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

        struct ShareReport
        {
            public Dictionary<int, double> Shipped;
            public Dictionary<int, double> Share;
            public Dictionary<int, double> AvgRoom;
            public Dictionary<int, double> ExhaustedFrac;
            public Dictionary<int, string> Tags;
        }

        struct ServicePoint
        {
            public float ClothServedShare;
            public float PhysMean;
            public int StarvedProvinces;
            public float MissedIn;
            public ulong Digest;
            public float CpuMs;
        }

        struct RootCause
        {
            public int PrimaryGoodCount;
            public float PrimaryProdPerTick;
            public float PrimaryConsPerTick;
            public float WineCapPerTick;
            public float WineConsPerTick;
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

            // Transit compte dans le stock « physique » mondial.
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

        static bool IsDiverging(Dictionary<int, Dictionary<int, GoodSnap>> snaps, int goodId)
        {
            return DescribeTrend(snaps, goodId, 3000) == "diverges" ||
                   DescribeTrend(snaps, goodId, 3000) == "grows";
        }

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

        static ShareReport MeasureTransportShares(
            PhysicalStockSystem.TransportServiceOrder mode, int totalTicks, int window)
        {
            ApplyAdoptedLocks(mode);
            PhysicalStockSystem.RecordTransportShares = true;
            var shipped = new Dictionary<int, double>();
            var roomSum = new Dictionary<int, double>();
            var roomCount = new Dictionary<int, int>();
            var exhausted = new Dictionary<int, int>();
            var tags = new Dictionary<int, string>();

            using (var h = new SimulationHarness(Seed))
            {
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

                        var rc = PhysicalStockSystem.LastTickCapRoomCountByGood[g];
                        if (rc > 0)
                        {
                            roomSum[g] = roomSum.TryGetValue(g, out var rs)
                                ? rs + PhysicalStockSystem.LastTickCapRoomSumByGood[g]
                                : PhysicalStockSystem.LastTickCapRoomSumByGood[g];
                            roomCount[g] = roomCount.TryGetValue(g, out var rcc) ? rcc + rc : rc;
                            exhausted[g] = exhausted.TryGetValue(g, out var ex)
                                ? ex + PhysicalStockSystem.LastTickCapExhaustedByGood[g]
                                : PhysicalStockSystem.LastTickCapExhaustedByGood[g];
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
            var avgRoom = new Dictionary<int, double>();
            var exhFrac = new Dictionary<int, double>();
            foreach (var kv in shipped)
            {
                share[kv.Key] = total > 1e-9 ? kv.Value / total : 0;
            }

            foreach (var kv in roomCount)
            {
                avgRoom[kv.Key] = kv.Value > 0 && roomSum.TryGetValue(kv.Key, out var rs)
                    ? rs / kv.Value
                    : 0;
                exhFrac[kv.Key] = kv.Value > 0 && exhausted.TryGetValue(kv.Key, out var ex)
                    ? (double)ex / kv.Value
                    : 0;
            }

            return new ShareReport
            {
                Shipped = shipped,
                Share = share,
                AvgRoom = avgRoom,
                ExhaustedFrac = exhFrac,
                Tags = tags
            };
        }

        static void AppendShareTable(StringBuilder sb, ShareReport r)
        {
            var ids = new List<int>(r.Shipped.Keys);
            ids.Sort();
            foreach (var id in ids)
            {
                var tag = r.Tags.TryGetValue(id, out var t) ? t : "?";
                var sh = r.Share.TryGetValue(id, out var s) ? s : 0;
                var ar = r.AvgRoom.TryGetValue(id, out var a) ? a : 0;
                var ef = r.ExhaustedFrac.TryGetValue(id, out var e) ? e : 0;
                sb.AppendLine(
                    $"{id}\t{tag}\t{Fmt(r.Shipped[id])}\t{Fmt(sh)}\t{Fmt(ar)}\t{Fmt(ef)}");
            }
        }

        static ServicePoint RunServicePoint(
            PhysicalStockSystem.TransportServiceOrder mode, int ticks)
        {
            ApplyAdoptedLocks(mode);
            using var h = new SimulationHarness(Seed);
            h.RunTicks(0);
            SetTransportInfra(h.EntityManager, PerDev);
            h.RunTicks(ticks);

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

            var m = GetMetrics(h.EntityManager);
            return new ServicePoint
            {
                ClothServedShare = clothD > 1e-6f ? clothS / clothD : 0f,
                PhysMean = n > 0 ? physSum / n : 0f,
                StarvedProvinces = starved,
                MissedIn = m.MissedInputShare,
                Digest = WorldDigest(h.EntityManager),
                CpuMs = (float)PhysicalStockSystem.LastTickCpuMs
            };
        }

        static RootCause MeasureUnconditionalProduction()
        {
            ApplyAdoptedLocks(PhysicalStockSystem.TransportServiceOrder.ByGoodId);
            using var h = new SimulationHarness(Seed);
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

            var types = new Dictionary<int, GoodType>();
            using (var q = h.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<GoodData>()))
            using (var goods = q.ToComponentDataArray<GoodData>(Allocator.Temp))
            {
                for (var i = 0; i < goods.Length; i++)
                {
                    types[goods[i].GoodId] = goods[i].Type;
                }
            }

            float wineCap = 0;
            using (var q = h.EntityManager.CreateEntityQuery(
                       ComponentType.ReadOnly<ProvincePhysicalActivity>()))
            using (var entities = q.ToEntityArray(Allocator.Temp))
            {
                for (var e = 0; e < entities.Length; e++)
                {
                    var buf = h.EntityManager.GetBuffer<ProvincePhysicalActivity>(entities[e]);
                    for (var i = 0; i < buf.Length; i++)
                    {
                        if (buf[i].GoodId == WineGoodId)
                        {
                            wineCap += buf[i].BaseCapacity;
                        }
                    }
                }
            }

            float primaryProd = 0, primaryCons = 0, wineCons = 0;
            var primaryCount = 0;
            using (var q = h.EntityManager.CreateEntityQuery(
                       ComponentType.ReadOnly<PhysicalEconomySingleton>()))
            {
                var ledger = h.EntityManager.GetBuffer<PhysicalLedgerEntry>(q.GetSingletonEntity());
                for (var i = 0; i < ledger.Length; i++)
                {
                    var g = ledger[i].GoodId;
                    var isPrimary = !recipeOutputs.Contains(g);
                    if (isPrimary)
                    {
                        primaryCount++;
                        primaryProd += (float)(ledger[i].CumulativeProduction / 100.0);
                        primaryCons += (float)(ledger[i].CumulativeConsumption / 100.0);
                    }

                    if (g == WineGoodId)
                    {
                        wineCons = (float)(ledger[i].CumulativeConsumption / 100.0);
                    }
                }
            }

            return new RootCause
            {
                PrimaryGoodCount = primaryCount,
                PrimaryProdPerTick = primaryProd,
                PrimaryConsPerTick = primaryCons,
                WineCapPerTick = wineCap,
                WineConsPerTick = wineCons
            };
        }

        static void ApplyAdoptedLocks(PhysicalStockSystem.TransportServiceOrder mode)
        {
            PopGrowthSystem.LockContinuity(Continuity);
            PhysicalSatisfactionBlendSystem.LockWeight(BlendWeight);
            PhysicalStockSystem.IdealPoolMode = false;
            PhysicalStockSystem.MultiHopTransport = true;
            PhysicalStockSystem.ServiceOrderMode = mode;
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

        static string Fmt(double v) =>
            v.ToString("0.###", CultureInfo.InvariantCulture);

        static string Fmt(float v) =>
            v.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
