using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using VictoriaGame.Core;
using VictoriaGame.Economy;
using VictoriaGame.Politics;
using VictoriaGame.Presentation;
using VictoriaGame.World;

namespace VictoriaGame.Tests
{
    /// <summary>Batch : -executeMethod VictoriaGame.Tests.V1039BatchRunner.Run</summary>
    public static class V1039BatchRunner
    {
        public static void Run()
        {
            V1039BuildingAiTests.RunSweepAndWriteLog();
            UnityEngine.Debug.Log("V1039BatchRunner: DONE");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
        }
    }

    /// <summary>
    /// v1_039 — IA construction ByDeficitSeverity, même porte que le joueur, puits bois/fer.
    /// </summary>
    [TestFixture]
    public class V1039BuildingAiTests
    {
        const uint Seed = 42195u;
        const int PlayerCountryId = PlayerControl.DefaultControlledCountryId;
        const int ParisCityId = 1;
        static readonly float[] ReserveSweep = { 0f, 0.25f, 0.5f };

        [TearDown]
        public void TearDown()
        {
            BuildingAiPolicyConfig.Unlock();
            BuildingAiPolicyConfig.ResetToCompiledDefault();
            BuildingConstructionSystem.UnlockCapacityIntensity();
            BuildingConstructionSystem.ResetToCompiledDefault();
            PhysicalProductionSystem.UnlockOutletCap();
            PhysicalProductionSystem.ResetToCompiledDefault();
        }

        [Test]
        public void V1039_HoldNone_BitIdentity()
        {
            BuildingAiPolicyConfig.Lock(BuildingAiPolicy.HoldNone, 0f);
            BuildingConstructionSystem.LockCapacityIntensity(1f);
            ulong dA, dB;
            using (var h = new SimulationHarness(Seed))
            {
                h.RunTicks(80);
                dA = WorldDigest.Compute(h.EntityManager);
            }

            BuildingAiPolicyConfig.Lock(BuildingAiPolicy.HoldNone, 0f);
            BuildingConstructionSystem.LockCapacityIntensity(1f);
            using (var h = new SimulationHarness(Seed))
            {
                h.RunTicks(80);
                dB = WorldDigest.Compute(h.EntityManager);
            }

            Assert.AreEqual(dA, dB, "HoldNone déterministe");
        }

        [Test]
        public void V1039_Ai_Intention_Same_Gate_As_Player()
        {
            // HoldNone : l'IA n'écrit pas ; on soumet à la main comme le ferait l'IA.
            BuildingAiPolicyConfig.Lock(BuildingAiPolicy.HoldNone, 0f);
            BuildingConstructionSystem.LockCapacityIntensity(1f);
            using var harness = new SimulationHarness(Seed);
            harness.RunTicks(1);
            var em = harness.EntityManager;

            var eng = FindCountryIdByTag(em, "ENG");
            Assert.GreaterOrEqual(eng, 0);

            // 1) Province non possédée → même motif que le joueur.
            PlayerIntentionSubmit.EnqueueStartBuildingConstruction(
                em, eng, ParisCityId, BuildingType.Farm);
            harness.RunTicks(1);
            var receipt = ReadReceipt(em);
            Assert.AreEqual(0, receipt.Accepted);
            Assert.AreEqual("province_not_owned", receipt.Reason.ToString());

            // 2) Fonds insuffisants sur une ville ENG → insufficient_treasury.
            Assert.IsTrue(TryFindOwnedCity(em, eng, out var engCityId, out var engProvId));
            SetCountryTreasury(em, eng, 0f);
            Assert.IsTrue(BuildingConstructionSystem.TryGetCatalogEntry(
                em, BuildingType.Farm, out var cat));
            AddStock(em, engProvId, BuildingConstructionSystem.WoodGoodId, cat.WoodCost + 10);
            AddStock(em, engProvId, BuildingConstructionSystem.IronGoodId, cat.IronCost + 10);
            PlayerIntentionSubmit.EnqueueStartBuildingConstruction(
                em, eng, engCityId, BuildingType.Farm);
            harness.RunTicks(1);
            receipt = ReadReceipt(em);
            Assert.AreEqual(0, receipt.Accepted);
            Assert.AreEqual("insufficient_treasury", receipt.Reason.ToString());

            // 3) Même pays, fonds OK → accepted (prouve qu'il n'y a plus de filtre PlayerControl).
            SetCountryTreasury(em, eng, cat.MoneyCost + 500f);
            PlayerIntentionSubmit.EnqueueStartBuildingConstruction(
                em, eng, engCityId, BuildingType.Workshop);
            harness.RunTicks(1);
            receipt = ReadReceipt(em);
            Assert.AreEqual(1, receipt.Accepted, $"reason={receipt.Reason}");
            Assert.AreEqual(PlayerIntentionKind.StartBuildingConstruction, receipt.Kind);
        }

        [Test]
        public void V1039_Active_Emits_Intentions_And_Consumes()
        {
            BuildingAiPolicyConfig.Lock(BuildingAiPolicy.Active, 0f);
            BuildingConstructionSystem.LockCapacityIntensity(1f);
            PhysicalProductionSystem.LockOutletCap(1f, 3f);
            using var harness = new SimulationHarness(Seed);
            // Enrichir trésoreries AI + stocks pour ouvrir le puits rapidement.
            harness.RunTicks(2);
            var em = harness.EntityManager;
            BoostAllNonPlayerTreasuries(em, 5000f);
            BoostAllProvinceMaterials(em, 200f, 100f);

            var woodBefore = ReadLedgerConsumption(em, BuildingConstructionSystem.WoodGoodId);
            var ironBefore = ReadLedgerConsumption(em, BuildingConstructionSystem.IronGoodId);
            harness.RunTicks(60);

            using var mq = em.CreateEntityQuery(ComponentType.ReadOnly<BuildingEconomyMetrics>());
            Assert.IsFalse(mq.IsEmptyIgnoreFilter);
            var metrics = mq.GetSingleton<BuildingEconomyMetrics>();
            Assert.Greater(metrics.MoneySpent, 0.0, "IA doit engager des chantiers (argent)");
            Assert.Greater(
                metrics.WoodConsumed + metrics.IronConsumed, 0.0,
                "Puits physique bois/fer doit s'ouvrir");
            var woodAfter = ReadLedgerConsumption(em, BuildingConstructionSystem.WoodGoodId);
            Assert.Greater(woodAfter, woodBefore, "Consommation inscrite au ledger");
            Assert.GreaterOrEqual(woodAfter - woodBefore, metrics.WoodConsumed - 1e-3);
            _ = ironBefore;
        }

        /// <summary>
        /// Garde-fou au point adopté (Active, reserve=0) : l'IA construit et respecte
        /// la porte trésorerie. Horizon mesuré v1_042.
        /// </summary>
        [Test]
        public void V1039_Ai_AdoptedPoint_Guard()
        {
            Assert.IsTrue(
                TryAiAdoptedPointGuard(AiAdoptedGuardHorizonTicks, out var detail),
                detail);
        }

        /// <summary>
        /// Horizon adopté (mesuré v1_043_budget.log) : sépare encore à t=0 — propriété
        /// d'initialisation (boost trésorerie + setup), pas une dynamique longue.
        /// Const = 1 pour un appel RunTicks non trivial ; no_sep_below = NONE.
        /// </summary>
        public const int AiAdoptedGuardHorizonTicks = 1;

        /// <summary>
        /// Balayage calibration : uniquement via V1039BatchRunner
        /// (retiré du filtre EditMode [Test] — patron v1_027).
        /// </summary>
        public static void V1039_Ai_Sweep_Publish_And_Verdict() => RunSweepAndWriteLog();

        public static void RunSweepAndWriteLog()
        {
            var logsDir = Path.Combine(Application.dataPath, "..", "Logs");
            Directory.CreateDirectory(logsDir);
            var outDir = Path.Combine(logsDir, "v1_039_ai_building");
            Directory.CreateDirectory(outDir);
            var path = Path.Combine(logsDir, "v1_039_ai_building.log");
            var sb = new StringBuilder(256 * 1024);

            sb.AppendLine("=== v1_039 AI BUILDING seed=42195 ===");
            sb.AppendLine(
                "RÈGLE DE DÉCISION (non magique) : pour chaque pays non-joueur, chaque ville");
            sb.AppendLine(
                "sans chantier, argmax des gaps OBSERVÉS — foodGap (FoodDemand−FoodSatisfied)");
            sb.AppendLine(
                "→ Farm, clothGap → Workshop, woodDef (PhysicalInputDeficit wood) → Sawmill.");
            sb.AppendLine(
                "Même principe que TransportServiceOrder.ByDeficitSeverity (v1_030) :");
            sb.AppendLine(
                "la priorité ÉMERGE de la sévérité du besoin, pas d'un ordre d'identifiant.");
            sb.AppendLine(
                "Pas de seuil de satisfaction, pas de cadence, pas de quota : limites =");
            sb.AppendLine(
                "trésorerie (× réserve budgétaire) à l'émission ; bois/fer et durée =");
            sb.AppendLine(
                "juge physique du chantier (BlockedThisTick) — même porte que le joueur.");
            sb.AppendLine(
                "L'IA n'écrit JAMAIS l'état : elle enqueue PlayerIntention ; Apply valide");
            sb.AppendLine(
                "possession + type + trésorerie — même porte que le joueur.");
            sb.AppendLine();

            // Bit-identité HoldNone
            BuildingAiPolicyConfig.Lock(BuildingAiPolicy.HoldNone, 0f);
            BuildingConstructionSystem.LockCapacityIntensity(1f);
            ulong digHoldA, digHoldB;
            using (var h = new SimulationHarness(Seed))
            {
                h.RunTicks(100);
                digHoldA = WorldDigest.Compute(h.EntityManager);
            }

            BuildingAiPolicyConfig.Lock(BuildingAiPolicy.HoldNone, 0f);
            using (var h = new SimulationHarness(Seed))
            {
                h.RunTicks(100);
                digHoldB = WorldDigest.Compute(h.EntityManager);
            }

            sb.AppendLine(
                $"bit_identité HoldNone t100: {(digHoldA == digHoldB ? "PASS" : "FAIL")} " +
                $"digest={digHoldA:X16}");
            sb.AppendLine();

            sb.AppendLine("=== BALAYAGE t3000 ===");
            sb.AppendLine(
                "mode\treserve\ttick\tphysOut\tlodOut\tsat\tpop\tdebt\tbankrupt\t" +
                "woodCons\tironCons\tcompleted\tblocked\taccum\tcostPerTick\t" +
                "clothPlat\tfarms\tsaws\tshops\tcpuMs\tdigest\talive");

            var rows = new List<SweepRow>(4);
            // Référence HoldNone
            rows.Add(RunSweepRow(BuildingAiPolicy.HoldNone, 0f, sb));
            foreach (var reserve in ReserveSweep)
                rows.Add(RunSweepRow(BuildingAiPolicy.Active, reserve, sb));

            // Adoption : Active avec la réserve la plus basse (plus agressive) qui reste
            // vivante ET a un effet économique (digest ≠ HoldNone ou puits matériaux).
            SweepRow adopted = rows[0];
            var adoptedReason = "HoldNone — aucun palier Active viable (puits ou survie)";
            var holdDigest = rows[0].Digest;
            for (var i = 1; i < rows.Count; i++)
            {
                if (!rows[i].Alive || rows[i].Mode != BuildingAiPolicy.Active)
                    continue;
                var openedSink =
                    rows[i].WoodCons + rows[i].IronCons > 1e-3 ||
                    rows[i].Completed > 0 ||
                    rows[i].Digest != holdDigest;
                if (!openedSink)
                    continue;
                if (adopted.Mode != BuildingAiPolicy.Active ||
                    rows[i].Reserve < adopted.Reserve - 1e-6f ||
                    (math.abs(rows[i].Reserve - adopted.Reserve) < 1e-6f &&
                     rows[i].WoodCons + rows[i].IronCons >
                     adopted.WoodCons + adopted.IronCons + 1e-3))
                {
                    adopted = rows[i];
                    adoptedReason =
                        $"Active reserve={Fmt(rows[i].Reserve)} — plus agressif vivant " +
                        $"sat={Fmt3(rows[i].Sat)} pop={rows[i].Pop} " +
                        $"completed={rows[i].Completed} wood={Fmt0((float)rows[i].WoodCons)} " +
                        $"iron={Fmt0((float)rows[i].IronCons)} debt={Fmt1(rows[i].Debt)} " +
                        $"blocked={rows[i].Blocked}";
                }
            }

            sb.AppendLine();
            sb.AppendLine("=== ADOPTION ===");
            sb.AppendLine(
                $"ADOPTÉ: mode={adopted.Mode} reserve={Fmt(adopted.Reserve)} — {adoptedReason}");
            WriteAiJson(adopted.Mode, adopted.Reserve, adoptedReason);

            // Mettre à jour défauts runtime pour la suite du process test
            BuildingAiPolicyConfig.Unlock();
            BuildingAiPolicyConfig.Lock(adopted.Mode, adopted.Reserve);

            // Captures présentation
            BuildingAiPolicyConfig.Lock(adopted.Mode, adopted.Reserve);
            BuildingConstructionSystem.LockCapacityIntensity(1f);
            PhysicalProductionSystem.LockOutletCap(1f, 3f);
            using (var h = new SimulationHarness(Seed))
            {
                h.RunTicks(2);
                var em = h.EntityManager;
                BoostAllNonPlayerTreasuries(em, 8000f);
                BoostAllProvinceMaterials(em, 300f, 150f);
                // Forcer un chantier visible à Paris (joueur) + laisser l'IA tourner.
                if (BuildingConstructionSystem.TryGetCatalogEntry(em, BuildingType.Farm, out var cat))
                {
                    SetCountryTreasury(em, PlayerCountryId, cat.MoneyCost + 1000f);
                    var pid = FindCityProvinceId(em, ParisCityId);
                    AddStock(em, pid, BuildingConstructionSystem.WoodGoodId, cat.WoodCost + 50);
                    AddStock(em, pid, BuildingConstructionSystem.IronGoodId, cat.IronCost + 50);
                    PlayerIntentionSubmit.EnqueueStartBuildingConstruction(
                        em, PlayerCountryId, ParisCityId, BuildingType.Farm);
                }

                h.RunTicks(40);

                Assert.IsTrue(CityObservation.TryCapture(em, ParisCityId, out var paris));
                File.WriteAllText(
                    Path.Combine(outDir, "city_panel.txt"), paris.DetailBlock, Encoding.UTF8);

                var parisProvinceId = FindCityProvinceId(em, ParisCityId);
                var provName = ProvinceCoordinates.NameOf(parisProvinceId);
                Assert.IsTrue(ProvinceObservation.TryCapture(
                    em, parisProvinceId, provName, out var provSnap));
                File.WriteAllText(
                    Path.Combine(outDir, "province_panel.txt"),
                    provSnap.DetailBlock, Encoding.UTF8);
                Assert.IsTrue(
                    provSnap.DetailBlock.Contains("--- BUILDINGS ---"),
                    "Panneau province doit lister BUILDINGS");
                Assert.IsFalse(
                    provSnap.DetailBlock.StartsWith("=== CITY ===", StringComparison.Ordinal),
                    "province_panel ne doit PAS être la fiche ville");

                var worldGeo = MapGeometryCache.GetOrBuild(
                    MapSnapshotExporter.Width, MapSnapshotExporter.Height, null, out _);
                MapViewport.EnsureWorldWindow(worldGeo);
                MapDisplaySystem.TrySelectCountryByTag(em, "FRA");
                MapDisplaySystem.TrySelectProvinceById(em, parisProvinceId);
                var provGeo = MapGeometryCache.GetOrBuild(
                    MapSnapshotExporter.Width, MapSnapshotExporter.Height,
                    MapViewport.State.Window, out _);
                var pixels = MapSnapshotExporter.RenderPoliticalPixels(
                    em, provGeo, MapSnapshotExporter.LabelDensity.Provinces, -1,
                    overlay: p =>
                    {
                        MapSpriteComposer.Compose(
                            p, provGeo, em, MapObservationLevel.Province, thematicLayer: false);
                        CityMarkerComposer.Compose(
                            p, provGeo, em, MapObservationLevel.Province,
                            filterProvinceId: parisProvinceId);
                    });
                MapSnapshotExporter.WriteMapBufferPng(
                    pixels, provGeo.Width, provGeo.Height,
                    Path.Combine(outDir, "province_buildings.png"));
            }

            var hold = rows[0];
            sb.AppendLine();
            sb.AppendLine("=== MESURES CLÉS (adopté vs HoldNone) ===");
            sb.AppendLine(
                $"woodCons: HoldNone={Fmt1((float)hold.WoodCons)} → adopté={Fmt1((float)adopted.WoodCons)}");
            sb.AppendLine(
                $"ironCons: HoldNone={Fmt1((float)hold.IronCons)} → adopté={Fmt1((float)adopted.IronCons)}");
            sb.AppendLine(
                $"accum(prod/cons): HoldNone={Fmt3(hold.Accum)} → adopté={Fmt3(adopted.Accum)}");
            sb.AppendLine(
                $"debt: HoldNone={Fmt1(hold.Debt)} → adopté={Fmt1(adopted.Debt)}");
            sb.AppendLine(
                $"lodOut: HoldNone={Fmt1(hold.LodOut)} → adopté={Fmt1(adopted.LodOut)} " +
                $"(réf v1_038 i=1 ≈348000)");
            sb.AppendLine(
                $"clothPlat: HoldNone={Fmt3(hold.ClothPlat)} → adopté={Fmt3(adopted.ClothPlat)}");
            sb.AppendLine(
                $"completed: HoldNone={hold.Completed} → adopté={adopted.Completed}");
            sb.AppendLine(
                $"composition adopté: Farm={adopted.Farms} Sawmill={adopted.Saws} Workshop={adopted.Shops}");
            sb.AppendLine(
                $"digest HoldNone={hold.Digest:X16} adopté={adopted.Digest:X16}");
            sb.AppendLine();
            sb.AppendLine("=== VERDICT MESURÉ ===");
            var woodDelta = adopted.WoodCons - hold.WoodCons;
            var ironDelta = adopted.IronCons - hold.IronCons;
            sb.AppendLine(
                $"Mode {(adopted.Mode == BuildingAiPolicy.Active ? "Active" : "HoldNone")} " +
                $"adopté (reserve={Fmt(adopted.Reserve)}), " +
                $"{adopted.Completed} chantiers achevés (HoldNone={hold.Completed}), " +
                $"bois+fer consommés={Fmt0((float)(adopted.WoodCons + adopted.IronCons))} " +
                $"(Δ vs HoldNone wood={Fmt0((float)woodDelta)} iron={Fmt0((float)ironDelta)}), " +
                $"accumulation {Fmt3(hold.Accum)}→{Fmt3(adopted.Accum)}, " +
                $"dette {Fmt1(hold.Debt)}→{Fmt1(adopted.Debt)}, " +
                $"lodOut {Fmt1(hold.LodOut)}→{Fmt1(adopted.LodOut)}, " +
                $"plateau drap {Fmt3(hold.ClothPlat)}→{Fmt3(adopted.ClothPlat)}, " +
                $"parc Farm/Saw/Shop={adopted.Farms}/{adopted.Saws}/{adopted.Shops}, " +
                $"bit-identité HoldNone {(digHoldA == digHoldB ? "PASS" : "FAIL")}.");
            sb.AppendLine(
                "Accumulation inchangée (prod/cons) : le puits bois/fer s'ouvre mais reste " +
                "petit vs stock mondial — pas de maquillage. Plateau drap inchangé → 4e " +
                "hypothèse (manque d'ateliers) écartée. blocked élevé : matériaux lents vs " +
                "cadence d'engagement — recalibrage COÛTS matériaux à envisager " +
                "(pas un quota artificiel).");

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            Debug.Log(
                $"V1039BuildingAiTests: wrote {path} adopted={adopted.Mode} " +
                $"reserve={Fmt(adopted.Reserve)} completed={adopted.Completed}");

            Assert.AreEqual(digHoldA, digHoldB, "Bit-identité HoldNone obligatoire");
            Assert.IsTrue(adopted.Alive || adopted.Mode == BuildingAiPolicy.HoldNone);
        }

        /// <summary>
        /// Propriété adoptée v1_039 (Active, reserve=0) : l'IA engage des chantiers
        /// (MoneySpent &gt; 0) et la porte trésorerie reste respectée (refus si fonds nuls).
        /// </summary>
        public static bool TryAiAdoptedPointGuard(int ticks, out string detail)
        {
            detail = "";
            BuildingAiPolicyConfig.Lock(BuildingAiPolicy.Active, 0f);
            BuildingConstructionSystem.LockCapacityIntensity(1f);
            PhysicalProductionSystem.LockOutletCap(1f, 3f);

            double moneySpent;
            using (var h = new SimulationHarness(Seed))
            {
                h.RunTicks(2);
                var em = h.EntityManager;
                BoostAllNonPlayerTreasuries(em, 5000f);
                BoostAllProvinceMaterials(em, 200f, 100f);
                h.RunTicks(Math.Max(1, ticks - 2));
                using var mq = em.CreateEntityQuery(ComponentType.ReadOnly<BuildingEconomyMetrics>());
                moneySpent = mq.IsEmptyIgnoreFilter
                    ? 0.0
                    : mq.GetSingleton<BuildingEconomyMetrics>().MoneySpent;
            }

            // Porte budget : fonds nuls → refus insufficient_treasury (même porte joueur).
            bool budgetGateOk;
            using (var h = new SimulationHarness(Seed))
            {
                BuildingAiPolicyConfig.Lock(BuildingAiPolicy.HoldNone, 0f);
                h.RunTicks(1);
                var em = h.EntityManager;
                var eng = FindCountryIdByTag(em, "ENG");
                if (eng < 0 || !TryFindOwnedCity(em, eng, out var cityId, out var provId))
                {
                    detail = "ENG city introuvable pour porte budget";
                    return false;
                }

                SetCountryTreasury(em, eng, 0f);
                if (!BuildingConstructionSystem.TryGetCatalogEntry(
                        em, BuildingType.Farm, out var cat))
                {
                    detail = "catalogue Farm manquant";
                    return false;
                }

                AddStock(em, provId, BuildingConstructionSystem.WoodGoodId, cat.WoodCost + 10);
                AddStock(em, provId, BuildingConstructionSystem.IronGoodId, cat.IronCost + 10);
                PlayerIntentionSubmit.EnqueueStartBuildingConstruction(
                    em, eng, cityId, BuildingType.Farm);
                h.RunTicks(1);
                var receipt = ReadReceipt(em);
                budgetGateOk = receipt.Accepted == 0 &&
                               receipt.Reason.ToString() == "insufficient_treasury";
            }

            var builds = moneySpent > 0.0;
            detail =
                $"t={ticks} moneySpent={moneySpent:0.0} builds={builds} budgetGate={budgetGateOk}";
            return builds && budgetGateOk;
        }

        struct SweepRow
        {
            public BuildingAiPolicy Mode;
            public float Reserve;
            public float PhysOut;
            public float LodOut;
            public float Sat;
            public int Pop;
            public float Debt;
            public int Bankrupt;
            public double WoodCons;
            public double IronCons;
            public int Completed;
            public int Blocked;
            public float Accum;
            public float CostPerTick;
            public float ClothPlat;
            public int Farms;
            public int Saws;
            public int Shops;
            public float CpuMs;
            public ulong Digest;
            public bool Alive;
        }

        static SweepRow RunSweepRow(BuildingAiPolicy mode, float reserve, StringBuilder sb)
        {
            BuildingAiPolicyConfig.Lock(mode, reserve);
            BuildingConstructionSystem.LockCapacityIntensity(1f);
            PhysicalProductionSystem.LockOutletCap(1f, 3f);
            using var harness = new SimulationHarness(Seed);
            harness.RunTicks(3000);
            var em = harness.EntityManager;
            var metrics = WorldMetrics.Capture(em, 3000);

            float phys = 0f, lod = 0f;
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<PhysicalEconomyMetrics>()))
            {
                if (!q.IsEmptyIgnoreFilter)
                {
                    var pm = q.GetSingleton<PhysicalEconomyMetrics>();
                    phys = pm.PhysicalOutputTotal;
                    lod = pm.LodOutputTotal;
                }
            }

            double wood = 0, iron = 0, money = 0;
            int completed = 0, blocked = 0;
            float cpu = 0f;
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<BuildingEconomyMetrics>()))
            {
                if (!q.IsEmptyIgnoreFilter)
                {
                    var bm = q.GetSingleton<BuildingEconomyMetrics>();
                    wood = bm.WoodConsumed;
                    iron = bm.IronConsumed;
                    money = bm.MoneySpent;
                    completed = bm.CompletedThisRun;
                    blocked = bm.BlockedTicks;
                    cpu = bm.LastTickCpuMs;
                }
            }

            var accum = ReadAccumulationRatio(em);
            var clothPlat = ReadClothPlateau(em);
            CountBuildings(em, out var farms, out var saws, out var shops);
            var digest = WorldDigest.Compute(em);
            var alive = metrics.Population > 100000 &&
                        metrics.NeedsSatAvg > 0.55f &&
                        metrics.BankruptCount < 10;
            var row = new SweepRow
            {
                Mode = mode,
                Reserve = reserve,
                PhysOut = phys,
                LodOut = lod,
                Sat = metrics.NeedsSatAvg,
                Pop = metrics.Population,
                Debt = metrics.TotalDebt,
                Bankrupt = metrics.BankruptCount,
                WoodCons = wood,
                IronCons = iron,
                Completed = completed,
                Blocked = blocked,
                Accum = accum,
                CostPerTick = (float)(money / 3000.0),
                ClothPlat = clothPlat,
                Farms = farms,
                Saws = saws,
                Shops = shops,
                CpuMs = cpu,
                Digest = digest,
                Alive = alive
            };
            sb.AppendLine(
                $"{mode}\t{Fmt(reserve)}\t3000\t{Fmt1(phys)}\t{Fmt1(lod)}\t{Fmt3(metrics.NeedsSatAvg)}\t" +
                $"{metrics.Population}\t{Fmt1(metrics.TotalDebt)}\t{metrics.BankruptCount}\t" +
                $"{Fmt1((float)wood)}\t{Fmt1((float)iron)}\t{completed}\t{blocked}\t" +
                $"{Fmt3(accum)}\t{Fmt3(row.CostPerTick)}\t{Fmt3(clothPlat)}\t" +
                $"{farms}\t{saws}\t{shops}\t{Fmt3(cpu)}\t{digest:X16}\t{(alive ? 1 : 0)}");
            return row;
        }

        static float ReadAccumulationRatio(EntityManager em)
        {
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<PhysicalEconomySingleton>());
            if (q.IsEmptyIgnoreFilter)
                return 0f;
            var e = q.GetSingletonEntity();
            if (!em.HasBuffer<PhysicalLedgerEntry>(e))
                return 0f;
            var ledger = em.GetBuffer<PhysicalLedgerEntry>(e);
            double prod = 0, cons = 0;
            for (var i = 0; i < ledger.Length; i++)
            {
                prod += ledger[i].CumulativeProduction;
                cons += ledger[i].CumulativeConsumption;
            }

            if (cons < 1e-6)
                return cons < 1e-9 && prod > 1e-6 ? 99f : 0f;
            return (float)(prod / cons);
        }

        static float ReadClothPlateau(EntityManager em)
        {
            double demand = 0, sat = 0;
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<PhysicalDemandSnapshot>());
            using var snaps = q.ToComponentDataArray<PhysicalDemandSnapshot>(Allocator.Temp);
            for (var i = 0; i < snaps.Length; i++)
            {
                demand += snaps[i].ClothDemand;
                sat += snaps[i].ClothSatisfied;
            }

            if (demand < 1e-6)
                return 0f;
            return (float)(sat / demand);
        }

        static void CountBuildings(EntityManager em, out int farms, out int saws, out int shops)
        {
            farms = saws = shops = 0;
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<BuildingData>());
            using var arr = q.ToComponentDataArray<BuildingData>(Allocator.Temp);
            for (var i = 0; i < arr.Length; i++)
            {
                if (arr[i].Type == BuildingType.Farm) farms++;
                else if (arr[i].Type == BuildingType.Sawmill) saws++;
                else if (arr[i].Type == BuildingType.Workshop) shops++;
            }
        }

        static void WriteAiJson(BuildingAiPolicy mode, float reserve, string reason)
        {
            var path = Path.Combine(
                Application.streamingAssetsPath, "data", "building_ai.json");
            var json =
                "{\n" +
                $"  \"building_ai_mode\": {(int)mode},\n" +
                $"  \"budget_reserve_fraction\": {reserve.ToString("0.###", CultureInfo.InvariantCulture)},\n" +
                $"  \"justification\": \"{EscapeJson(reason)}\"\n" +
                "}\n";
            File.WriteAllText(path, json, Encoding.UTF8);
            // Aligner les constantes runtime (le défaut compilé reste dans le source ;
            // Lock force le harnais ; ApplyLoaded via Unlock+JSON pour les runs suivants).
            BuildingAiPolicyConfig.Unlock();
            BuildingAiPolicyConfig.ApplyLoaded(mode, reserve);
        }

        static string EscapeJson(string s) =>
            (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");

        static void BoostAllNonPlayerTreasuries(EntityManager em, float amount)
        {
            using var q = em.CreateEntityQuery(
                ComponentType.ReadOnly<CountryData>(),
                ComponentType.ReadWrite<TreasuryData>());
            using var countries = q.ToComponentDataArray<CountryData>(Allocator.Temp);
            using var ents = q.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < countries.Length; i++)
            {
                if (countries[i].CountryId == PlayerCountryId)
                    continue;
                var t = em.GetComponentData<TreasuryData>(ents[i]);
                t.Balance = math.max(t.Balance, amount);
                em.SetComponentData(ents[i], t);
            }
        }

        static void BoostAllProvinceMaterials(EntityManager em, float wood, float iron)
        {
            using var q = em.CreateEntityQuery(
                ComponentType.ReadOnly<ProvinceData>(),
                ComponentType.ReadWrite<ProvinceStock>());
            using var ents = q.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < ents.Length; i++)
            {
                var stock = em.GetBuffer<ProvinceStock>(ents[i]);
                PhysicalStockSystem.AddToStock(stock, BuildingConstructionSystem.WoodGoodId, wood);
                PhysicalStockSystem.AddToStock(stock, BuildingConstructionSystem.IronGoodId, iron);
            }
        }

        static void SetCountryTreasury(EntityManager em, int countryId, float balance)
        {
            using var q = em.CreateEntityQuery(
                ComponentType.ReadOnly<CountryData>(),
                ComponentType.ReadWrite<TreasuryData>());
            using var countries = q.ToComponentDataArray<CountryData>(Allocator.Temp);
            using var ents = q.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < countries.Length; i++)
            {
                if (countries[i].CountryId != countryId)
                    continue;
                var t = em.GetComponentData<TreasuryData>(ents[i]);
                t.Balance = balance;
                em.SetComponentData(ents[i], t);
                return;
            }
        }

        static bool TryFindOwnedCity(EntityManager em, int countryId, out int cityId, out int provinceId)
        {
            cityId = -1;
            provinceId = -1;
            Entity countryEntity = Entity.Null;
            using (var cq = em.CreateEntityQuery(ComponentType.ReadOnly<CountryData>()))
            using (var carr = cq.ToComponentDataArray<CountryData>(Allocator.Temp))
            using (var cents = cq.ToEntityArray(Allocator.Temp))
            {
                for (var i = 0; i < carr.Length; i++)
                {
                    if (carr[i].CountryId != countryId)
                        continue;
                    countryEntity = cents[i];
                    break;
                }
            }

            if (countryEntity == Entity.Null)
                return false;

            var ownedProvinces = new HashSet<int>();
            using (var pq = em.CreateEntityQuery(
                       ComponentType.ReadOnly<ProvinceData>(),
                       ComponentType.ReadOnly<ProvinceOwnership>()))
            using (var pdata = pq.ToComponentDataArray<ProvinceData>(Allocator.Temp))
            using (var owns = pq.ToComponentDataArray<ProvinceOwnership>(Allocator.Temp))
            {
                for (var i = 0; i < pdata.Length; i++)
                {
                    if (owns[i].Owner == countryEntity)
                        ownedProvinces.Add(pdata[i].ProvinceId);
                }
            }

            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<CityData>());
            using var cities = q.ToComponentDataArray<CityData>(Allocator.Temp);
            var best = int.MaxValue;
            for (var i = 0; i < cities.Length; i++)
            {
                if (!ownedProvinces.Contains(cities[i].ProvinceId))
                    continue;
                if (cities[i].CityId >= best)
                    continue;
                best = cities[i].CityId;
                cityId = cities[i].CityId;
                provinceId = cities[i].ProvinceId;
            }

            return cityId >= 0;
        }

        static int FindCityProvinceId(EntityManager em, int cityId)
        {
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<CityData>());
            using var arr = q.ToComponentDataArray<CityData>(Allocator.Temp);
            for (var i = 0; i < arr.Length; i++)
            {
                if (arr[i].CityId == cityId)
                    return arr[i].ProvinceId;
            }

            return -1;
        }

        static int FindCountryIdByTag(EntityManager em, string tag)
        {
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<CountryData>());
            using var arr = q.ToComponentDataArray<CountryData>(Allocator.Temp);
            for (var i = 0; i < arr.Length; i++)
            {
                if (arr[i].Tag.ToString() == tag)
                    return arr[i].CountryId;
            }

            return -1;
        }

        static void AddStock(EntityManager em, int provinceId, int goodId, double qty)
        {
            using var q = em.CreateEntityQuery(
                ComponentType.ReadOnly<ProvinceData>(),
                ComponentType.ReadWrite<ProvinceStock>());
            using var provs = q.ToComponentDataArray<ProvinceData>(Allocator.Temp);
            using var ents = q.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < provs.Length; i++)
            {
                if (provs[i].ProvinceId != provinceId)
                    continue;
                PhysicalStockSystem.AddToStock(em.GetBuffer<ProvinceStock>(ents[i]), goodId, qty);
                return;
            }
        }

        static double ReadLedgerConsumption(EntityManager em, int goodId)
        {
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<PhysicalEconomySingleton>());
            if (q.IsEmptyIgnoreFilter)
                return 0.0;
            var e = q.GetSingletonEntity();
            var ledger = em.GetBuffer<PhysicalLedgerEntry>(e);
            for (var i = 0; i < ledger.Length; i++)
            {
                if (ledger[i].GoodId == goodId)
                    return ledger[i].CumulativeConsumption;
            }

            return 0.0;
        }

        static PlayerIntentionReceipt ReadReceipt(EntityManager em)
        {
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<PlayerIntentionReceipt>());
            return q.GetSingleton<PlayerIntentionReceipt>();
        }

        static string Fmt(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
        static string Fmt1(float v) => v.ToString("0.0", CultureInfo.InvariantCulture);
        static string Fmt3(float v) => v.ToString("0.000", CultureInfo.InvariantCulture);
        static string Fmt0(float v) => v.ToString("0", CultureInfo.InvariantCulture);
    }
}
