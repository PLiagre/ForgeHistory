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
    /// <summary>Batch : -executeMethod VictoriaGame.Tests.V1038BatchRunner.Run</summary>
    public static class V1038BatchRunner
    {
        public static void Run()
        {
            V1038BuildingTests.RunSweepAndWriteLog();
            UnityEngine.Debug.Log("V1038BatchRunner: DONE");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
        }
    }

    /// <summary>
    /// v1_038 — bâtiments physiques, chantiers, intensité réversible de capacité.
    /// </summary>
    [TestFixture]
    public class V1038BuildingTests
    {
        const uint Seed = 42195u;
        const int PlayerCountryId = PlayerControl.DefaultControlledCountryId;
        const int ParisCityId = 1;
        static readonly float[] IntensitySweep = { 0f, 0.25f, 0.5f, 0.75f, 1f };

        [TearDown]
        public void TearDown()
        {
            BuildingConstructionSystem.UnlockCapacityIntensity();
            BuildingConstructionSystem.ResetToCompiledDefault();
            BuildingAiPolicyConfig.Unlock();
            BuildingAiPolicyConfig.ResetToCompiledDefault();
            PhysicalProductionSystem.UnlockOutletCap();
            PhysicalProductionSystem.ResetToCompiledDefault();
        }

        [Test]
        public void V1038_Catalog_And_Seed_Present()
        {
            BuildingConstructionSystem.LockCapacityIntensity(0f);
            using var harness = new SimulationHarness(Seed);
            harness.RunTicks(0);

            var em = harness.EntityManager;
            Assert.IsTrue(BuildingConstructionSystem.TryGetCatalogEntry(em, BuildingType.Farm, out var farm));
            Assert.Greater(farm.MoneyCost, 0f);
            Assert.Greater(farm.WoodCost, 0f);
            Assert.Greater(farm.Capacity, 0f);

            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<BuildingData>());
            using var buildings = q.ToComponentDataArray<BuildingData>(Allocator.Temp);
            Assert.Greater(buildings.Length, 0, "Parc initial semé attendu");
            var complete = 0;
            for (var i = 0; i < buildings.Length; i++)
            {
                if (buildings[i].IsComplete != 0) complete++;
                Assert.Greater(buildings[i].BuildingId, 0);
            }

            Assert.AreEqual(buildings.Length, complete, "Semis = bâtiments achevés");
            BuildingAiPolicyConfig.Lock(BuildingAiPolicy.HoldNone, 0f);
            Assert.AreEqual(BuildingAiPolicy.HoldNone, BuildingAiPolicyConfig.Mode);
            Assert.AreEqual(0f, BuildingConstructionSystem.CapacityIntensity, 1e-6f);
        }

        [Test]
        public void V1038_Intention_Build_Cycle_And_Blocked()
        {
            BuildingConstructionSystem.LockCapacityIntensity(0f);
            BuildingAiPolicyConfig.Lock(BuildingAiPolicy.HoldNone, 0f);
            using var harness = new SimulationHarness(Seed);
            harness.RunTicks(1);

            var em = harness.EntityManager;
            Assert.IsTrue(BuildingConstructionSystem.TryGetCatalogEntry(em, BuildingType.Farm, out var cat));

            // Enrichir trésorerie + stocks pour le cycle complet.
            SetPlayerTreasury(em, cat.MoneyCost + 500f);
            var provinceId = FindCityProvinceId(em, ParisCityId);
            Assert.GreaterOrEqual(provinceId, 0);
            AddStock(em, provinceId, BuildingConstructionSystem.WoodGoodId, cat.WoodCost + 5000);
            AddStock(em, provinceId, BuildingConstructionSystem.IronGoodId, cat.IronCost + 5000);

            var balanceBefore = ReadTreasury(em, PlayerCountryId);
            var woodBefore = ReadStock(em, provinceId, BuildingConstructionSystem.WoodGoodId);
            var ledgerWoodBefore = ReadLedgerConsumption(em, BuildingConstructionSystem.WoodGoodId);

            Assert.IsTrue(PlayerIntentionSubmit.EnqueueStartBuildingConstruction(
                em, PlayerCountryId, ParisCityId, BuildingType.Farm));
            // UI n'écrit pas l'état — seul le buffer a grossi.
            Assert.AreEqual(balanceBefore, ReadTreasury(em, PlayerCountryId), 1e-4f);

            harness.RunTicks(1);
            var receipt = ReadReceipt(em);
            Assert.AreEqual(1, receipt.Accepted, $"reason={receipt.Reason} kind={receipt.Kind}");
            Assert.AreEqual(PlayerIntentionKind.StartBuildingConstruction, receipt.Kind);
            // Le tick applique aussi upkeep/taxe — vérifier le débit chantier via métriques.
            using (var mq = em.CreateEntityQuery(ComponentType.ReadOnly<BuildingEconomyMetrics>()))
            {
                var spent = mq.GetSingleton<BuildingEconomyMetrics>().MoneySpent;
                Assert.AreEqual(cat.MoneyCost, (float)spent, 1e-2f);
            }
            Assert.Less(ReadTreasury(em, PlayerCountryId), balanceBefore - cat.MoneyCost + 1f);

            // Chantier actif
            Assert.IsTrue(BuildingConstructionSystem.CityHasActiveConstruction(em, ParisCityId));

            // Refus slot occupé
            PlayerIntentionSubmit.EnqueueStartBuildingConstruction(
                em, PlayerCountryId, ParisCityId, BuildingType.Workshop);
            harness.RunTicks(1);
            Assert.AreEqual(0, ReadReceipt(em).Accepted);
            Assert.AreEqual("slot_occupied", ReadReceipt(em).Reason.ToString());

            // Avancer jusqu'à complétion (réapprovisionner : la prod physique
            // consomme aussi bois/fer via recettes — le chantier ne doit pas être affamé).
            var ticks = cat.DurationTicks + 2;
            for (var t = 0; t < ticks; t++)
            {
                AddStock(em, provinceId, BuildingConstructionSystem.WoodGoodId, cat.WoodCost);
                AddStock(em, provinceId, BuildingConstructionSystem.IronGoodId, cat.IronCost);
                harness.RunTicks(1);
            }

            Assert.IsFalse(
                BuildingConstructionSystem.CityHasActiveConstruction(em, ParisCityId),
                "Chantier Farm doit être achevé après durée+matériaux");
            var foundComplete = false;
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<BuildingData>()))
            using (var arr = q.ToComponentDataArray<BuildingData>(Allocator.Temp))
            {
                for (var i = 0; i < arr.Length; i++)
                {
                    if (arr[i].CityId == ParisCityId &&
                        arr[i].Type == BuildingType.Farm &&
                        arr[i].IsComplete != 0 &&
                        arr[i].BuildingId > 0)
                    {
                        // Nouveau bâtiment (id > semis) ou au moins un Farm complete dans Paris
                        foundComplete = true;
                    }
                }
            }

            Assert.IsTrue(foundComplete);
            var woodAfter = ReadStock(em, provinceId, BuildingConstructionSystem.WoodGoodId);
            Assert.Less(woodAfter, woodBefore - 1.0, "Bois consommé par le chantier");
            var ledgerWoodAfter = ReadLedgerConsumption(em, BuildingConstructionSystem.WoodGoodId);
            Assert.Greater(ledgerWoodAfter, ledgerWoodBefore + 1.0, "Consommation inscrite au ledger");

            // Cas bloqué : nouveau chantier sans matériaux
            SetPlayerTreasury(em, cat.MoneyCost + 100f);
            // Vider bois
            SetStock(em, provinceId, BuildingConstructionSystem.WoodGoodId, 0);
            SetStock(em, provinceId, BuildingConstructionSystem.IronGoodId, cat.IronCost + 10);
            Assert.IsTrue(PlayerIntentionSubmit.EnqueueStartBuildingConstruction(
                em, PlayerCountryId, ParisCityId, BuildingType.Sawmill));
            harness.RunTicks(1);
            Assert.AreEqual(1, ReadReceipt(em).Accepted);
            harness.RunTicks(5);
            Assert.IsTrue(BuildingConstructionSystem.CityHasActiveConstruction(em, ParisCityId));
            // Toujours chantier, pas de capacité ajoutée pour Sawmill incomplete
            using (var q = em.CreateEntityQuery(
                       ComponentType.ReadOnly<BuildingData>(),
                       ComponentType.ReadOnly<BuildingConstruction>()))
            using (var arr = q.ToComponentDataArray<BuildingConstruction>(Allocator.Temp))
            {
                Assert.Greater(arr.Length, 0);
                var anyBlocked = false;
                for (var i = 0; i < arr.Length; i++)
                {
                    if (arr[i].BlockedThisTick != 0 || arr[i].ProgressTicks == 0)
                        anyBlocked = true;
                }

                Assert.IsTrue(anyBlocked, "Chantier bloqué faute de bois");
            }
        }

        [Test]
        public void V1038_Intention_Rejects_Unknown_Type_And_Uncontrolled()
        {
            BuildingConstructionSystem.LockCapacityIntensity(0f);
            BuildingAiPolicyConfig.Lock(BuildingAiPolicy.HoldNone, 0f);
            using var harness = new SimulationHarness(Seed);
            harness.RunTicks(0);
            var em = harness.EntityManager;
            SetPlayerTreasury(em, 10000f);

            // Type hors périmètre (Factory)
            PlayerIntentionSubmit.EnqueueStartBuildingConstruction(
                em, PlayerCountryId, ParisCityId, BuildingType.Factory);
            harness.RunTicks(1);
            Assert.AreEqual(0, ReadReceipt(em).Accepted);
            Assert.AreEqual("type_unknown", ReadReceipt(em).Reason.ToString());

            // Ville inconnue
            PlayerIntentionSubmit.EnqueueStartBuildingConstruction(
                em, PlayerCountryId, 99999, BuildingType.Farm);
            harness.RunTicks(1);
            Assert.AreEqual(0, ReadReceipt(em).Accepted);
            Assert.AreEqual("target_unknown", ReadReceipt(em).Reason.ToString());

            // Province non possédée par ENG (Paris = FRA) → même motif que le joueur
            // (v1_039 : porte = possession, pas PlayerControl).
            var eng = FindCountryIdByTag(em, "ENG");
            Assert.GreaterOrEqual(eng, 0);
            PlayerIntentionSubmit.EnqueueStartBuildingConstruction(
                em, eng, ParisCityId, BuildingType.Farm);
            harness.RunTicks(1);
            Assert.AreEqual(0, ReadReceipt(em).Accepted);
            Assert.AreEqual("province_not_owned", ReadReceipt(em).Reason.ToString());
        }

        [Test]
        public void V1038_IntensityZero_BitIdentity()
        {
            ulong dA, dB;
            BuildingConstructionSystem.LockCapacityIntensity(0f);
            BuildingAiPolicyConfig.Lock(BuildingAiPolicy.HoldNone, 0f);
            using (var h = new SimulationHarness(Seed))
            {
                h.RunTicks(50);
                dA = WorldDigest.Compute(h.EntityManager);
            }

            BuildingConstructionSystem.LockCapacityIntensity(0f);
            BuildingAiPolicyConfig.Lock(BuildingAiPolicy.HoldNone, 0f);
            using (var h = new SimulationHarness(Seed))
            {
                h.RunTicks(50);
                dB = WorldDigest.Compute(h.EntityManager);
            }

            Assert.AreEqual(dA, dB, "Intensité 0 déterministe");
        }

        /// <summary>
        /// Garde-fou au point adopté (intensity=1) : capacité dérivée des bâtiments,
        /// distincte du scalaire LOD (i=0). Horizon mesuré v1_042.
        /// </summary>
        [Test]
        public void V1038_Capacity_AdoptedPoint_Guard()
        {
            Assert.IsTrue(
                TryCapacityAdoptedPointGuard(CapacityAdoptedGuardHorizonTicks, out var detail),
                detail);
        }

        /// <summary>
        /// Horizon le plus court qui sépare encore (mesuré v1_043_budget.log) :
        /// t=1 NO_SEP, t=5 SEPARATES.
        /// </summary>
        public const int CapacityAdoptedGuardHorizonTicks = 5;

        /// <summary>
        /// Balayage calibration : uniquement via V1038BatchRunner
        /// (retiré du filtre EditMode [Test] — patron v1_027).
        /// </summary>
        public static void V1038_Capacity_Sweep_Publish_And_Verdict() => RunSweepAndWriteLog();

        public static void RunSweepAndWriteLog()
        {
            var logsDir = Path.Combine(Application.dataPath, "..", "Logs");
            Directory.CreateDirectory(logsDir);
            var outDir = Path.Combine(logsDir, "v1_038_buildings");
            Directory.CreateDirectory(outDir);
            var path = Path.Combine(logsDir, "v1_038_buildings.log");
            var sb = new StringBuilder(128 * 1024);

            // Isoler le balayage capacité de l'IA Active (défaut v1_039).
            BuildingAiPolicyConfig.Lock(BuildingAiPolicy.HoldNone, 0f);

            sb.AppendLine("=== v1_038 BUILDINGS seed=42195 ===");
            sb.AppendLine(
                "Règle magique inscrite (registre #9) : capacité/efficacité dérivées de " +
                "ProvinceDevelopment.Production — omission de revue, pas dette nouvelle.");
            sb.AppendLine(
                $"AI construction: {BuildingAiPolicyConfig.Mode} (EXPLICITE HoldNone — " +
                "aucun pays non-joueur ne construit ; IA Active = v1_039).");
            sb.AppendLine(
                "Mécanismes différés (une phrase) : Routes=capacité d'arête investie; " +
                "destruction=Capacity→0; obsolescence=tech gate; Factory+méthodes=ProductionMethod; " +
                "8 autres types=catalogue; IA=BuildingAiPolicy.Active.");
            sb.AppendLine();

            // Parc initial
            int seeded = 0;
            var byType = new SortedDictionary<string, int>();
            var byCountry = new SortedDictionary<int, int>();
            BuildingConstructionSystem.LockCapacityIntensity(0f);
            using (var h = new SimulationHarness(Seed))
            {
                h.RunTicks(0);
                var em = h.EntityManager;
                using var q = em.CreateEntityQuery(ComponentType.ReadOnly<BuildingData>());
                using var arr = q.ToComponentDataArray<BuildingData>(Allocator.Temp);
                seeded = arr.Length;
                for (var i = 0; i < arr.Length; i++)
                {
                    var t = arr[i].Type.ToString();
                    byType[t] = byType.TryGetValue(t, out var c) ? c + 1 : 1;
                    byCountry[arr[i].CountryId] =
                        byCountry.TryGetValue(arr[i].CountryId, out var cc) ? cc + 1 : 1;
                }
            }

            sb.AppendLine($"PARC INITIAL semé: total={seeded}");
            foreach (var kv in byType)
                sb.AppendLine($"  type {kv.Key}={kv.Value}");
            foreach (var kv in byCountry)
                sb.AppendLine($"  countryId {kv.Key}={kv.Value}");
            sb.AppendLine();

            // Digests intensité 0
            BuildingConstructionSystem.LockCapacityIntensity(0f);
            ulong dig0A, dig0B;
            using (var h = new SimulationHarness(Seed))
            {
                h.RunTicks(100);
                dig0A = WorldDigest.Compute(h.EntityManager);
            }

            BuildingConstructionSystem.LockCapacityIntensity(0f);
            using (var h = new SimulationHarness(Seed))
            {
                h.RunTicks(100);
                dig0B = WorldDigest.Compute(h.EntityManager);
            }

            sb.AppendLine(
                $"digest_AVANT(i=0)={dig0A:X16} digest_replay(i=0)={dig0B:X16} " +
                $"bit_identity={(dig0A == dig0B ? "PASS" : "FAIL")}");
            sb.AppendLine();

            sb.AppendLine(
                "intensity | tick | physOut | lodOut | sat | pop | debt | bankrupt | " +
                "woodCons | ironCons | completed | blocked | cpuMs | digest | alive");

            var rows = new List<SweepRow>(IntensitySweep.Length);
            foreach (var intensity in IntensitySweep)
            {
                rows.Add(RunSweepAtIntensity(intensity, sb));
            }

            // Adoption : palier le plus haut vivant (pop>100k, sat>0.55, bankrupt<10)
            SweepRow adopted = rows[0];
            var adoptedReason = "défaut i=0 (bit-identique) — aucun palier non-nul viable";
            for (var i = rows.Count - 1; i >= 0; i--)
            {
                var r = rows[i];
                if (r.Alive)
                {
                    adopted = r;
                    adoptedReason =
                        $"plus fort palier vivant (i={Fmt(r.Intensity)}) pop={r.Pop} sat={Fmt3(r.Sat)} " +
                        $"debt={Fmt1(r.Debt)} bankrupt={r.Bankrupt}";
                    break;
                }
            }

            // Si un palier >0 est vivant, l'adopter ; sinon rester à 0
            sb.AppendLine();
            sb.AppendLine("=== ADOPTION ===");
            sb.AppendLine($"ADOPTÉ: intensity={Fmt(adopted.Intensity)} — {adoptedReason}");

            // Écrire JSON adoptée (si > défaut compilé on met à jour le fichier)
            WriteCapacityJson(adopted.Intensity, adoptedReason);

            // Cycle construction chiffré + captures
            int completedSites = 0, blockedSites = 0;
            double woodCons = 0, ironCons = 0, moneySpent = 0;
            BuildingConstructionSystem.LockCapacityIntensity(adopted.Intensity);
            using (var h = new SimulationHarness(Seed))
            {
                h.RunTicks(1);
                var em = h.EntityManager;
                if (BuildingConstructionSystem.TryGetCatalogEntry(em, BuildingType.Farm, out var cat))
                {
                    SetPlayerTreasury(em, cat.MoneyCost + 1000f);
                    var pid = FindCityProvinceId(em, ParisCityId);
                    PlayerIntentionSubmit.EnqueueStartBuildingConstruction(
                        em, PlayerCountryId, ParisCityId, BuildingType.Farm);
                    h.RunTicks(1);
                    for (var t = 0; t < cat.DurationTicks + 2; t++)
                    {
                        AddStock(em, pid, BuildingConstructionSystem.WoodGoodId, cat.WoodCost);
                        AddStock(em, pid, BuildingConstructionSystem.IronGoodId, cat.IronCost);
                        h.RunTicks(1);
                    }
                }

                // Second chantier bloqué
                if (BuildingConstructionSystem.TryGetCatalogEntry(em, BuildingType.Workshop, out var cat2))
                {
                    SetPlayerTreasury(em, cat2.MoneyCost + 100f);
                    var pid = FindCityProvinceId(em, ParisCityId);
                    SetStock(em, pid, BuildingConstructionSystem.WoodGoodId, 0);
                    SetStock(em, pid, BuildingConstructionSystem.IronGoodId, cat2.IronCost);
                    PlayerIntentionSubmit.EnqueueStartBuildingConstruction(
                        em, PlayerCountryId, ParisCityId, BuildingType.Workshop);
                    h.RunTicks(4);
                }

                using var mq = em.CreateEntityQuery(ComponentType.ReadOnly<BuildingEconomyMetrics>());
                if (!mq.IsEmptyIgnoreFilter)
                {
                    var m = mq.GetSingleton<BuildingEconomyMetrics>();
                    completedSites = m.CompletedThisRun;
                    blockedSites = m.BlockedTicks;
                    woodCons = m.WoodConsumed;
                    ironCons = m.IronConsumed;
                    moneySpent = m.MoneySpent;
                }

                // Captures
                Assert.IsTrue(CityObservation.TryCapture(em, ParisCityId, out var paris));
                File.WriteAllText(Path.Combine(outDir, "city_panel.txt"), paris.DetailBlock, Encoding.UTF8);

                var worldGeo = MapGeometryCache.GetOrBuild(
                    MapSnapshotExporter.Width, MapSnapshotExporter.Height, null, out _);
                MapViewport.EnsureWorldWindow(worldGeo);
                MapDisplaySystem.TrySelectCountryByTag(em, "FRA");
                var countryGeo = MapGeometryCache.GetOrBuild(
                    MapSnapshotExporter.Width, MapSnapshotExporter.Height,
                    MapViewport.State.Window, out _);
                // Zoom province Paris
                var parisProvinceId = FindCityProvinceId(em, ParisCityId);
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

                // Panneau province RÉEL (v1_039 — plus la fiche ville).
                var provName = ProvinceCoordinates.NameOf(parisProvinceId);
                Assert.IsTrue(ProvinceObservation.TryCapture(
                    em, parisProvinceId, provName, out var provSnap));
                File.WriteAllText(
                    Path.Combine(outDir, "province_panel.txt"),
                    provSnap.DetailBlock, Encoding.UTF8);
            }

            sb.AppendLine();
            sb.AppendLine("=== CHANTIERS (run preuve) ===");
            sb.AppendLine($"completed={completedSites} blocked_ticks={blockedSites}");
            sb.AppendLine(
                $"wood_consumed={Fmt1((float)woodCons)} iron_consumed={Fmt1((float)ironCons)} " +
                $"money_spent={Fmt1((float)moneySpent)}");
            sb.AppendLine($"cpu_last_tick_ms={Fmt3((float)BuildingConstructionSystem.LastTickCpuMs)}");

            ulong digAdopted;
            BuildingConstructionSystem.LockCapacityIntensity(adopted.Intensity);
            using (var h = new SimulationHarness(Seed))
            {
                h.RunTicks(100);
                digAdopted = WorldDigest.Compute(h.EntityManager);
            }

            sb.AppendLine(
                $"digest_APRÈS(adopted i={Fmt(adopted.Intensity)})={digAdopted:X16}");
            sb.AppendLine();
            sb.AppendLine("=== VERDICT MESURÉ ===");
            sb.AppendLine(
                $"Parc initial de {seeded} bâtiments semé, intensité adoptée {Fmt(adopted.Intensity)} " +
                $"(sat={Fmt3(adopted.Sat)}, pop={adopted.Pop}, bankrupt={adopted.Bankrupt}; " +
                $"{adoptedReason}), " +
                $"{completedSites} chantiers achevés et {blockedSites} ticks bloqués faute d'intrants, " +
                $"{Fmt0((float)(woodCons + ironCons))} unités bois+fer consommées et inscrites au registre, " +
                $"conservation via ledger, parité/stabilité déléguées au filtre EditMode large.");
            sb.AppendLine(
                dig0A == dig0B
                    ? "PASS bit-identité intensité 0."
                    : "FAIL bit-identité intensité 0.");

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            Debug.Log($"V1038BuildingTests: wrote {path} adopted i={Fmt(adopted.Intensity)} seeded={seeded}");
        }

        /// <summary>
        /// Propriété adoptée v1_038 (intensity=1) : capacité agrégée des bâtiments &gt; 0
        /// et production physique distincte du scalaire LOD (intensity=0).
        /// </summary>
        public static bool TryCapacityAdoptedPointGuard(int ticks, out string detail)
        {
            detail = "";
            BuildingAiPolicyConfig.Lock(BuildingAiPolicy.HoldNone, 0f);
            PhysicalProductionSystem.LockOutletCap(1f, 3f);

            float capSum = 0f;
            float physAdopted, physLod;
            BuildingConstructionSystem.LockCapacityIntensity(
                BuildingConstructionSystem.DefaultCapacityIntensity);
            using (var h = new SimulationHarness(Seed))
            {
                h.RunTicks(ticks);
                var em = h.EntityManager;
                using var map = new NativeHashMap<long, float>(128, Allocator.Temp);
                BuildingConstructionSystem.AggregateCompletedCapacity(em, map);
                using var vals = map.GetValueArray(Allocator.Temp);
                for (var i = 0; i < vals.Length; i++)
                    capSum += vals[i];
                physAdopted = ReadPhysOut(em);
            }

            BuildingConstructionSystem.LockCapacityIntensity(0f);
            using (var h = new SimulationHarness(Seed))
            {
                h.RunTicks(ticks);
                physLod = ReadPhysOut(h.EntityManager);
            }

            var intensityOk =
                Math.Abs(
                    BuildingConstructionSystem.DefaultCapacityIntensity - 1f) < 1e-6f;
            var derivedFromBuildings = capSum > 1f;
            var distinctFromLod = Math.Abs(physAdopted - physLod) > 1f;
            detail =
                $"t={ticks} capSum={capSum:0.0} phys_i1={physAdopted:0.0} phys_i0={physLod:0.0} " +
                $"intensityOk={intensityOk} derived={derivedFromBuildings} distinct={distinctFromLod}";
            return intensityOk && derivedFromBuildings && distinctFromLod;
        }

        static float ReadPhysOut(EntityManager em)
        {
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<PhysicalEconomyMetrics>());
            if (q.IsEmptyIgnoreFilter)
                return 0f;
            return q.GetSingleton<PhysicalEconomyMetrics>().PhysicalOutputTotal;
        }

        struct SweepRow
        {
            public float Intensity;
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
            public float CpuMs;
            public ulong Digest;
            public bool Alive;
        }

        static SweepRow RunSweepAtIntensity(float intensity, StringBuilder sb)
        {
            BuildingConstructionSystem.LockCapacityIntensity(intensity);
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

            double wood = 0, iron = 0;
            int completed = 0, blocked = 0;
            float cpu = 0f;
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<BuildingEconomyMetrics>()))
            {
                if (!q.IsEmptyIgnoreFilter)
                {
                    var bm = q.GetSingleton<BuildingEconomyMetrics>();
                    wood = bm.WoodConsumed;
                    iron = bm.IronConsumed;
                    completed = bm.CompletedThisRun;
                    blocked = bm.BlockedTicks;
                    cpu = bm.LastTickCpuMs;
                }
            }

            var digest = WorldDigest.Compute(em);
            var alive = metrics.Population > 100000 &&
                        metrics.NeedsSatAvg > 0.55f &&
                        metrics.BankruptCount < 10;
            var row = new SweepRow
            {
                Intensity = intensity,
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
                CpuMs = cpu,
                Digest = digest,
                Alive = alive
            };
            sb.AppendLine(
                $"{Fmt(intensity)}\t3000\t{Fmt1(phys)}\t{Fmt1(lod)}\t{Fmt3(metrics.NeedsSatAvg)}\t" +
                $"{metrics.Population}\t{Fmt1(metrics.TotalDebt)}\t{metrics.BankruptCount}\t" +
                $"{Fmt1((float)wood)}\t{Fmt1((float)iron)}\t{completed}\t{blocked}\t" +
                $"{Fmt3(cpu)}\t{digest:X16}\t{(alive ? "Y" : "N")}");
            return row;
        }

        static void WriteCapacityJson(float intensity, string justification)
        {
            var path = Path.Combine(
                Application.streamingAssetsPath, "data", "building_capacity.json");
            var json =
                "{\n" +
                $"  \"building_capacity_intensity\": {intensity.ToString("0.###", CultureInfo.InvariantCulture)},\n" +
                $"  \"intensity_justification\": \"{EscapeJson(justification)}\"\n" +
                "}\n";
            File.WriteAllText(path, json, Encoding.UTF8);
        }

        static string EscapeJson(string s) =>
            (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");

        static void SetPlayerTreasury(EntityManager em, float balance)
        {
            using var q = em.CreateEntityQuery(
                ComponentType.ReadOnly<CountryData>(),
                ComponentType.ReadWrite<TreasuryData>());
            using var countries = q.ToComponentDataArray<CountryData>(Allocator.Temp);
            using var ents = q.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < countries.Length; i++)
            {
                if (countries[i].CountryId != PlayerCountryId)
                    continue;
                var t = em.GetComponentData<TreasuryData>(ents[i]);
                t.Balance = balance;
                em.SetComponentData(ents[i], t);
                return;
            }
        }

        static float ReadTreasury(EntityManager em, int countryId)
        {
            using var q = em.CreateEntityQuery(
                ComponentType.ReadOnly<CountryData>(),
                ComponentType.ReadOnly<TreasuryData>());
            using var countries = q.ToComponentDataArray<CountryData>(Allocator.Temp);
            using var treasuries = q.ToComponentDataArray<TreasuryData>(Allocator.Temp);
            for (var i = 0; i < countries.Length; i++)
            {
                if (countries[i].CountryId == countryId)
                    return treasuries[i].Balance;
            }

            return 0f;
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
                var stock = em.GetBuffer<ProvinceStock>(ents[i]);
                PhysicalStockSystem.AddToStock(stock, goodId, qty);
                return;
            }
        }

        static void SetStock(EntityManager em, int provinceId, int goodId, double qty)
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
                var stock = em.GetBuffer<ProvinceStock>(ents[i]);
                var idx = PhysicalStockSystem.FindStockIndex(stock, goodId);
                if (idx >= 0)
                {
                    var e = stock[idx];
                    e.Quantity = qty;
                    stock[idx] = e;
                }
                else
                {
                    stock.Add(new ProvinceStock { GoodId = goodId, Quantity = qty });
                }

                return;
            }
        }

        static double ReadStock(EntityManager em, int provinceId, int goodId)
        {
            using var q = em.CreateEntityQuery(
                ComponentType.ReadOnly<ProvinceData>(),
                ComponentType.ReadOnly<ProvinceStock>());
            using var provs = q.ToComponentDataArray<ProvinceData>(Allocator.Temp);
            using var ents = q.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < provs.Length; i++)
            {
                if (provs[i].ProvinceId != provinceId)
                    continue;
                return PhysicalStockSystem.GetStockQuantity(em.GetBuffer<ProvinceStock>(ents[i]), goodId);
            }

            return 0.0;
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
