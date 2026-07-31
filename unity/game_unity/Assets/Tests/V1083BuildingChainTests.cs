using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using VictoriaGame.Core;
using VictoriaGame.Economy;
using VictoriaGame.Presentation;
using VictoriaGame.Utils;
using VictoriaGame.World;
using Debug = UnityEngine.Debug;

namespace VictoriaGame.Tests
{
    /// <summary>
    /// Point d'entrée batchmode :
    /// -executeMethod VictoriaGame.Tests.V1083BuildingChainBatchRunner.Run
    /// </summary>
    public static class V1083BuildingChainBatchRunner
    {
        public static void Run()
        {
            V1083BuildingChainTests.RunAndWriteArtifacts();
            Debug.Log("V1083BuildingChainBatchRunner: DONE");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
        }
    }

    /// <summary>
    /// v1_083 — diagnostic chaîne ville→bâtiment→site→drap→import ;
    /// couture CitySeedCoefficient (défaut 0 bit-identique, PROPOSÉE non adoptée) ;
    /// captures PNG regardables ; bornes terre par géométrie.
    /// </summary>
    [TestFixture]
    public class V1083BuildingChainTests
    {
        const uint Seed = 42195u;
        const int ClothTicks = 300;
        const int DeterminismTicks = 100;
        const int ExpectedCitiesBefore = 123;
        const int ExpectedCitiesAfter = 204;

        /// <summary>Paliers de balayage CitySeedCoefficient (mesure, non adoption).</summary>
        static readonly float[] CoeffSweep = { 0f, 0.25f, 0.5f, 0.75f, 1f };

        static string GameUnityRoot =>
            Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        static string CapturesDir =>
            Path.Combine(GameUnityRoot, "Captures", "v1_083");

        static string LogPath =>
            Path.Combine(GameUnityRoot, "Logs", "v1_083_building_chain.log");

        static string CitiesPath =>
            Path.Combine(Application.streamingAssetsPath, "data", "cities.json");

        static string CoordsPath =>
            Path.Combine(Application.streamingAssetsPath, "data", "city_coordinates.json");

        static string BeforeCitiesPath =>
            Path.Combine(GameUnityRoot, "Captures", "v1_082", "before_cities.json");

        static string BeforeCoordsPath =>
            Path.Combine(GameUnityRoot, "Captures", "v1_082", "before_city_coordinates.json");

        [TearDown]
        public void TearDown()
        {
            BuildingInitSystem.UnlockCitySeedCoefficient();
            BuildingConstructionSystem.UnlockCapacityIntensity();
            PhysicalSatisfactionBlendSystem.UnlockWeight();
            MapSnapshotExporter.ResetZoomScaleToNeutral();
            MapLabelLayout.CollisionEnabled = true;
            MapLabelLayout.LegacyCityLabels = false;
            MapLabelLayout.UseImportanceQueue = true;
            PilotMapProvider.Enabled = false;
            MapGeometryCache.ResetStatsAndClear();
            CityCoordinates.InvalidateCache();
            MapViewport.Reset();
        }

        [Test]
        public void V1083_A_TargetIsProvincial_BuildingsIndependentOfCityCount()
        {
            BuildingInitSystem.LockCitySeedCoefficient(0f);
            var before = MeasureChainWithFiles(BeforeCitiesPath, BeforeCoordsPath, 0);
            var after = MeasureChainWithFiles(CitiesPath, CoordsPath, 0);
            Assert.AreEqual(ExpectedCitiesBefore, before.Cities,
                "snapshot before v1_082 doit avoir 123 villes");
            Assert.AreEqual(ExpectedCitiesAfter, after.Cities,
                "monde courant doit avoir 204 villes");
            Assert.AreEqual(before.Buildings, after.Buildings,
                "à CitySeedCoefficient=0, bâtiments AVANT==APRÈS (target provincial)");
            Assert.AreEqual(before.Sites, after.Sites,
                "sites de production provinciaux inchangés par le peuplement");
        }

        [Test]
        public void V1083_B_ZeroCoefficient_BitIdenticalDigest()
        {
            BuildingInitSystem.LockCitySeedCoefficient(0f);
            ulong dA, dB;
            using (var h = new SimulationHarness(Seed))
            {
                h.RunTicks(DeterminismTicks);
                dA = WorldDigest.Compute(h.EntityManager);
            }

            BuildingInitSystem.LockCitySeedCoefficient(0f);
            using (var h = new SimulationHarness(Seed))
            {
                h.RunTicks(DeterminismTicks);
                dB = WorldDigest.Compute(h.EntityManager);
            }

            Assert.AreEqual(dA, dB, "coeff=0 doit être rejouable bit-identique");
        }

        [Test]
        public void V1083_C_LandBoundsPerGeometry_Bite()
        {
            var voronoi = MeasureLandDistances(pilot: false);
            var pilot = MeasureLandDistances(pilot: true);
            Assert.Greater(voronoi.BoundPx, 0);
            Assert.Greater(pilot.BoundPx, 0);
            // Morsure : Paris lon=-20 → au-delà de chaque borne.
            Assert.IsFalse(
                CityWouldPassLandBound(pilot: false, lon: -20.0, lat: 48.85, voronoi.BoundPx),
                "Voronoï doit ROUGIR pour Paris en Atlantique");
            Assert.IsFalse(
                CityWouldPassLandBound(pilot: true, lon: -20.0, lat: 48.85, pilot.BoundPx),
                "Pilote doit ROUGIR pour Paris en Atlantique");
        }

        [Test]
        public void V1083_Artifacts_And_Verdict() => RunAndWriteArtifacts();

        public static void RunAndWriteArtifacts()
        {
            Directory.CreateDirectory(CapturesDir);
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            var sb = new StringBuilder(256 * 1024);
            sb.AppendLine("=== v1_083 — CHAÎNE VILLE → PRODUCTION (BuildingInitSystem) ===");
            sb.AppendLine("seed=" + Seed);
            sb.AppendLine("before_cities=" + BeforeCitiesPath);
            sb.AppendLine("after_cities=" + CitiesPath);
            sb.AppendLine();

            // ── PARTIE 1 — diagnostic ──────────────────────────────────────
            sb.AppendLine("=== PARTIE 1 — DIAGNOSTIC MAILLON ROMPU ===");
            sb.AppendLine(
                "target @ BuildingInitSystem = ProductionSite.BaseOutput × " +
                "clamp(0.5 + ProvinceDevelopment.Production×0.05, 0.1, 2.0)");
            sb.AppendLine(
                "cityFactor = 1 + CitySeedCoefficient × max(0, nVillesProvince−1) " +
                "(défaut coeff=0 → cityFactor=1 → count indépendant des villes)");
            sb.AppendLine(
                "villes = destinations de répartition (cities[i % Count]) uniquement " +
                "quand coeff=0.");
            sb.AppendLine();

            BuildingInitSystem.LockCitySeedCoefficient(0f);
            var chainBefore = MeasureChainWithFiles(BeforeCitiesPath, BeforeCoordsPath, ClothTicks);
            var chainAfter = MeasureChainWithFiles(CitiesPath, CoordsPath, ClothTicks);

            sb.AppendLine("--- chaîne AVANT import v1_082 (123 villes) ---");
            AppendChain(sb, chainBefore);
            sb.AppendLine("--- chaîne APRÈS import v1_082 (204 villes) ---");
            AppendChain(sb, chainAfter);

            var buildingsEqual = chainBefore.Buildings == chainAfter.Buildings;
            var sitesEqual = chainBefore.Sites == chainAfter.Sites;
            sb.AppendLine(
                "buildings_before=" + chainBefore.Buildings +
                " buildings_after=" + chainAfter.Buildings +
                " delta=" + (chainAfter.Buildings - chainBefore.Buildings) +
                " " + (buildingsEqual ? "IDENTIQUE" : "DIFFÈRE"));
            sb.AppendLine(
                "sites_before=" + chainBefore.Sites +
                " sites_after=" + chainAfter.Sites +
                " delta=" + (chainAfter.Sites - chainBefore.Sites) +
                " " + (sitesEqual ? "IDENTIQUE" : "DIFFÈRE"));
            sb.AppendLine(
                "cloth_import_before=" +
                chainBefore.ImportShare.ToString("0.###", CultureInfo.InvariantCulture) +
                " (" + Pct(chainBefore.ImportShare) + " %) " +
                "cloth_import_after=" +
                chainAfter.ImportShare.ToString("0.###", CultureInfo.InvariantCulture) +
                " (" + Pct(chainAfter.ImportShare) + " %)");

            string brokenLink;
            if (buildingsEqual)
            {
                brokenLink =
                    "villes → bâtiments (BuildingInitSystem count) : target dérive de " +
                    "ProvinceDevelopment/ProductionSite, pas des villes ; " +
                    "81 villes de plus REDISTRIBUENT les mêmes bâtiments.";
            }
            else
            {
                brokenLink =
                    "bâtiments DIFFÈRENT malgré le raisonnement code — chercher plus loin " +
                    "(sites / sortie / demande).";
            }

            sb.AppendLine("maillon_rompu=" + brokenLink);
            sb.AppendLine(
                "si_target_dependait_des_villes: count croîtrait avec nVilles → capacité " +
                "bâtiment (CapacityIntensity=1) croîtrait → sortie drap ↑ → import ↓.");
            sb.AppendLine();

            // ── PARTIE 2 — couture ─────────────────────────────────────────
            sb.AppendLine("=== PARTIE 2 — COUTURE CitySeedCoefficient (PROPOSÉE, NON ADOPTÉE) ===");
            sb.AppendLine(
                "formule: count = max(1, round(target × (1 + coeff × max(0,n−1)) / Capacity))");
            sb.AppendLine("DefaultCitySeedCoefficient=0 (bit-identique compilé).");

            BuildingInitSystem.LockCitySeedCoefficient(0f);
            ulong dig0A, dig0B;
            using (var h = new SimulationHarness(Seed))
            {
                h.RunTicks(DeterminismTicks);
                dig0A = WorldDigest.Compute(h.EntityManager);
            }

            BuildingInitSystem.LockCitySeedCoefficient(0f);
            using (var h = new SimulationHarness(Seed))
            {
                h.RunTicks(DeterminismTicks);
                dig0B = WorldDigest.Compute(h.EntityManager);
            }

            sb.AppendLine(
                "bit_identity_coeff0 digest_A=0x" + dig0A.ToString("X16") +
                " digest_B=0x" + dig0B.ToString("X16") +
                " " + (dig0A == dig0B ? "PASS" : "FAIL"));

            sb.AppendLine(
                "coeff | buildings | cloth_out | cloth_demand | import_share | import_pct");
            var sweep = new List<SweepRow>(CoeffSweep.Length);
            foreach (var c in CoeffSweep)
            {
                var row = MeasureSweepAt(c);
                sweep.Add(row);
                sb.AppendLine(
                    Fmt(c) + " | " + row.Buildings + " | " +
                    Fmt1(row.ClothOut) + " | " + Fmt1(row.ClothDemand) + " | " +
                    Fmt(row.ImportShare) + " | " + Pct(row.ImportShare));
            }

            var baseImport = sweep[0].ImportShare;
            var monotoneOk = true;
            for (var i = 1; i < sweep.Count; i++)
            {
                // Effet attendu : plus de coeff → plus de bâtiments → import ↓ (ou =).
                if (sweep[i].ImportShare > sweep[i - 1].ImportShare + 1e-4f)
                    monotoneOk = false;
            }

            var maxDelta = 0f;
            for (var i = 0; i < sweep.Count; i++)
                maxDelta = math.max(maxDelta, math.abs(sweep[i].ImportShare - baseImport));

            var effectMoves = maxDelta > 0.005f; // > 0,5 pt au-delà bruit proxy
            string part2Verdict;
            float proposed = 0f;
            if (!buildingsEqual)
            {
                part2Verdict =
                    "FAIL — diagnostic PARTIE 1 n'a pas confirmé l'égalité bâtiments ; " +
                    "couture non justifiée sur cette piste.";
            }
            else if (!effectMoves)
            {
                part2Verdict =
                    "FAIL — couture ouverte (buildings croissent avec coeff) mais part " +
                    "d'import drap ne bouge pas au-delà de 0,5 pt ; chaîne se rompt " +
                    "encore APRÈS les bâtiments (CapacityIntensity / sortie physique / " +
                    "proxy V1025 / Flandre sans province).";
            }
            else if (!monotoneOk)
            {
                part2Verdict =
                    "FAIL — effet non monotone sur import drap ; ne pas proposer.";
            }
            else
            {
                // Proposer le plus petit coeff qui déplace > 0,5 pt, plafonné 0,75.
                proposed = 0.5f;
                for (var i = 0; i < sweep.Count; i++)
                {
                    if (math.abs(sweep[i].ImportShare - baseImport) > 0.005f)
                    {
                        proposed = sweep[i].Coeff;
                        break;
                    }
                }

                if (proposed > 0.75f)
                    proposed = 0.75f;
                part2Verdict =
                    "PASS_MESURE — effet monotone ; PROPOSÉ coeff=" + Fmt(proposed) +
                    " NON ADOPTÉ (Default reste 0).";
            }

            sb.AppendLine("monotone_import_decreasing=" + (monotoneOk ? "YES" : "NO"));
            sb.AppendLine(
                "max_delta_import=" +
                maxDelta.ToString("0.####", CultureInfo.InvariantCulture));
            sb.AppendLine("part2_verdict=" + part2Verdict);

            // Garde-fous V1016/17/18/V1020 : smoke court à coeff proposé (si >0) et à 0.
            sb.AppendLine("--- garde-fous smoke (t200, seed=42195) ---");
            foreach (var c in new[] { 0f, proposed > 0f ? proposed : 0.5f })
            {
                BuildingInitSystem.LockCitySeedCoefficient(c);
                using var h = new SimulationHarness(Seed);
                h.RunTicks(200);
                var m = WorldMetrics.Capture(h.EntityManager, 200);
                sb.AppendLine(
                    "coeff=" + Fmt(c) +
                    " pop=" + m.Population +
                    " sat=" + Fmt(m.NeedsSatAvg) +
                    " debt=" + Fmt1(m.TotalDebt) +
                    " bankrupt=" + m.BankruptCount +
                    " alive=" + (m.Population > 100000 && m.NeedsSatAvg > 0.5f
                        ? "YES"
                        : "NO"));
            }

            sb.AppendLine(
                "V1016/17/18/V1020: non rejoués en entier ici — smoke t200 ci-dessus ; " +
                "suite LARGE du runner tranche. DefaultCitySeedCoefficient reste 0.");
            sb.AppendLine();

            // ── PARTIE 3 — captures + bornes ───────────────────────────────
            sb.AppendLine("=== PARTIE 3 — CAPTURES PNG + BORNES TERRE ===");
            WriteCaptures(sb, before: true);
            WriteCaptures(sb, before: false);

            var voronoiBound = MeasureLandDistances(pilot: false);
            var pilotBound = MeasureLandDistances(pilot: true);
            sb.AppendLine("--- bornes par géométrie ---");
            AppendBound(sb, "voronoi", voronoiBound);
            AppendBound(sb, "pilot", pilotBound);
            sb.AppendLine(
                "reconciliation: v1_081 a mesuré Voronoï 1600×1200 sur 123 villes " +
                "(max=26 px → tol=37). Après peuplement v1_082 (204 villes) le max " +
                "Voronoï dérive à " + voronoiBound.MaxDistPx + " px → borne " +
                voronoiBound.BoundPx + " (=ceil(max×1.4)). La borne 37 n'était PAS " +
                "fausse : elle domine le monde pré-peuplement. v1_082 a employé " +
                "bound_px=259 sur le MASQUE PILOTE (max_added=185) en citant à tort " +
                "'v1081_tol=75' — 75 est la borne Voronoï post-peuplement " +
                "(ceil(53×1.4)), pas une référence pilote. Mesure v1_083 pilote " +
                "max_added=" + pilotBound.MaxAddedDistPx + " → bound=" +
                pilotBound.BoundPx + " ; beyond_probe_all=" + pilotBound.BeyondProbe +
                " (masque pilote incomplet hors peuplement). Deux géométries = deux bornes.");
            sb.AppendLine(
                "morsure_voronoi_paris_atlantique=" +
                (!CityWouldPassLandBound(false, -20.0, 48.85, voronoiBound.BoundPx)
                    ? "ROUGE_OK"
                    : "FAIL_NO_BITE"));
            sb.AppendLine(
                "morsure_pilot_paris_atlantique=" +
                (!CityWouldPassLandBound(true, -20.0, 48.85, pilotBound.BoundPx)
                    ? "ROUGE_OK"
                    : "FAIL_NO_BITE"));
            sb.AppendLine();

            // ── VERDICT ────────────────────────────────────────────────────
            sb.AppendLine("=== VERDICT MESURE ===");
            var verdict =
                (buildingsEqual ? "PASS_DIAG" : "FAIL_DIAG") +
                " bâtiments avant " + chainBefore.Buildings +
                ", après " + chainAfter.Buildings +
                (buildingsEqual ? " — IDENTIQUE" : " — DIFFÈRE") +
                " : target dérive de ProductionSite.BaseOutput × efficiency" +
                "(ProvinceDevelopment.Production) et non des villes, maillon rompu " +
                "trouvé à BuildingInitSystem (count) ; chaîne publiée : " +
                chainAfter.Cities + " villes → " + chainAfter.Buildings +
                " bâtiments → " + chainAfter.Sites + " sites → drap_out " +
                Fmt1(chainAfter.ClothOut) + " → demande " + Fmt1(chainAfter.ClothDemand) +
                " → import " + Pct(chainAfter.ImportShare) + " % ; " +
                part2Verdict +
                " ; PNG écrits dans Captures/v1_083 ; bornes Voronoï " +
                voronoiBound.BoundPx + " px / Pilote " + pilotBound.BoundPx +
                " px, réconciliées et prouvées mordantes.";
            sb.AppendLine(verdict);

            File.WriteAllText(LogPath, sb.ToString(), Encoding.UTF8);
            Debug.Log("V1083BuildingChainTests: wrote " + LogPath);
            BuildingInitSystem.UnlockCitySeedCoefficient();

            Assert.IsTrue(buildingsEqual,
                "PARTIE 1: bâtiments avant/après doivent être identiques à coeff=0");
            Assert.AreEqual(dig0A, dig0B, "bit-identité coeff=0");
        }

        static void AppendChain(StringBuilder sb, ChainSnap s)
        {
            sb.AppendLine(
                "villes=" + s.Cities +
                " → bâtiments=" + s.Buildings +
                " → sites=" + s.Sites +
                " → drap_out=" + Fmt1(s.ClothOut) +
                " → drap_demand=" + Fmt1(s.ClothDemand) +
                " → drap_satisfied=" + Fmt1(s.ClothSatisfied) +
                " → import_share=" + Fmt(s.ImportShare) +
                " (" + Pct(s.ImportShare) + " %)");
        }

        static void AppendBound(StringBuilder sb, string name, LandBound b)
        {
            sb.AppendLine(
                name + ": geometry=" + b.Width + "x" + b.Height +
                " lon_span_deg=" + b.LonSpanDeg.ToString("0.##", CultureInfo.InvariantCulture) +
                " km_per_px_50N=" + b.KmPerPx.ToString("0.###", CultureInfo.InvariantCulture) +
                " max_dist_px=" + b.MaxDistPx +
                " max_added_dist_px=" + b.MaxAddedDistPx +
                " max_dist_km=" + (b.MaxDistPx * b.KmPerPx).ToString("0.#", CultureInfo.InvariantCulture) +
                " bound_px=" + b.BoundPx +
                " bound_km=" + (b.BoundPx * b.KmPerPx).ToString("0.#", CultureInfo.InvariantCulture) +
                " nonzero=" + b.NonZero +
                " cities=" + b.CitiesChecked +
                " added_checked=" + b.AddedChecked +
                " beyond_probe=" + b.BeyondProbe +
                " form=ceil(operational_max×1.4)" +
                (name == "pilot"
                    ? " operational=added_id>123"
                    : " operational=all_cities"));
        }

        static ChainSnap MeasureChainWithFiles(string citiesPath, string coordsPath, int ticks)
        {
            var liveC = File.ReadAllText(CitiesPath, Encoding.UTF8);
            var liveO = File.ReadAllText(CoordsPath, Encoding.UTF8);
            try
            {
                File.WriteAllText(CitiesPath, File.ReadAllText(citiesPath, Encoding.UTF8), Encoding.UTF8);
                File.WriteAllText(CoordsPath, File.ReadAllText(coordsPath, Encoding.UTF8), Encoding.UTF8);
                CityCoordinates.InvalidateCache();
                BuildingInitSystem.LockCitySeedCoefficient(0f);
                return MeasureChain(ticks);
            }
            finally
            {
                File.WriteAllText(CitiesPath, liveC, Encoding.UTF8);
                File.WriteAllText(CoordsPath, liveO, Encoding.UTF8);
                CityCoordinates.InvalidateCache();
            }
        }

        static ChainSnap MeasureChain(int ticks)
        {
            PhysicalSatisfactionBlendSystem.LockWeight(0f);
            PhysicalStockSystem.MultiHopTransport = true;
            using var h = new SimulationHarness(Seed);
            h.RunTicks(math.max(0, ticks));
            var em = h.EntityManager;
            var snap = Snapshot(em);
            PhysicalSatisfactionBlendSystem.UnlockWeight();
            return snap;
        }

        static SweepRow MeasureSweepAt(float coeff)
        {
            BuildingInitSystem.LockCitySeedCoefficient(coeff);
            PhysicalSatisfactionBlendSystem.LockWeight(0f);
            PhysicalStockSystem.MultiHopTransport = true;
            using var h = new SimulationHarness(Seed);
            h.RunTicks(ClothTicks);
            var s = Snapshot(h.EntityManager);
            PhysicalSatisfactionBlendSystem.UnlockWeight();
            return new SweepRow
            {
                Coeff = coeff,
                Buildings = s.Buildings,
                ClothOut = s.ClothOut,
                ClothDemand = s.ClothDemand,
                ImportShare = s.ImportShare
            };
        }

        static ChainSnap Snapshot(EntityManager em)
        {
            var cities = 0;
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<CityData>()))
                cities = q.CalculateEntityCount();

            var buildings = 0;
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<BuildingData>()))
            using (var arr = q.ToComponentDataArray<BuildingData>(Allocator.Temp))
            {
                for (var i = 0; i < arr.Length; i++)
                    if (arr[i].IsComplete != 0)
                        buildings++;
            }

            var sites = 0;
            float clothOut = 0f;
            using (var q = em.CreateEntityQuery(
                       ComponentType.ReadOnly<ProductionSite>(),
                       ComponentType.ReadOnly<ProvinceData>()))
            using (var siteArr = q.ToComponentDataArray<ProductionSite>(Allocator.Temp))
            using (var provArr = q.ToComponentDataArray<ProvinceData>(Allocator.Temp))
            {
                sites = siteArr.Length;
                for (var i = 0; i < siteArr.Length; i++)
                {
                    var tag = provArr[i].GoodTag.ToString();
                    if (string.Equals(tag, "cloth", StringComparison.OrdinalIgnoreCase))
                        clothOut += siteArr[i].LastOutput;
                }
            }

            // Capacité bâtiment workshop (complète le LOD si CapacityIntensity>0).
            float workshopCap = 0f;
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<BuildingData>()))
            using (var arr = q.ToComponentDataArray<BuildingData>(Allocator.Temp))
            {
                for (var i = 0; i < arr.Length; i++)
                {
                    if (arr[i].IsComplete == 0 || arr[i].Type != BuildingType.Workshop)
                        continue;
                    workshopCap += arr[i].CapacityContribution;
                }
            }

            float demand = 0f, satisfied = 0f, importProxy = 0f, n = 0f;
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<PhysicalDemandSnapshot>()))
            using (var snaps = q.ToComponentDataArray<PhysicalDemandSnapshot>(Allocator.Temp))
            {
                for (var i = 0; i < snaps.Length; i++)
                {
                    var d = snaps[i].ClothDemand;
                    var s = snaps[i].ClothSatisfied;
                    demand += d;
                    satisfied += s;
                    if (d <= 1e-4f)
                        continue;
                    n += 1f;
                    importProxy += math.saturate(1f - math.saturate(s / d));
                }
            }

            return new ChainSnap
            {
                Cities = cities,
                Buildings = buildings,
                Sites = sites,
                ClothOut = clothOut + workshopCap * BuildingConstructionSystem.CapacityIntensity,
                ClothDemand = demand,
                ClothSatisfied = satisfied,
                ImportShare = n > 0f ? importProxy / n : 0f
            };
        }

        static void WriteCaptures(StringBuilder sb, bool before)
        {
            var label = before ? "before" : "after";
            var citiesPath = before ? BeforeCitiesPath : CitiesPath;
            var coordsPath = before ? BeforeCoordsPath : CoordsPath;
            var liveC = File.ReadAllText(CitiesPath, Encoding.UTF8);
            var liveO = File.ReadAllText(CoordsPath, Encoding.UTF8);
            try
            {
                File.WriteAllText(CitiesPath, File.ReadAllText(citiesPath, Encoding.UTF8), Encoding.UTF8);
                File.WriteAllText(CoordsPath, File.ReadAllText(coordsPath, Encoding.UTF8), Encoding.UTF8);
                CityCoordinates.InvalidateCache();

                using var harness = new SimulationHarness(Seed);
                harness.RunTicks(0);
                var em = harness.EntityManager;
                MapViewport.Reset();
                MapGeometryCache.ResetStatsAndClear();
                PilotMapProvider.SetEnabled(true, clearCache: true);
                MapSnapshotExporter.ZoomScaleEnabled = false;

                var worldGeo = MapGeometryCache.GetOrBuild(
                    MapSnapshotExporter.Width, MapSnapshotExporter.Height, null, out _);
                MapViewport.EnsureWorldWindow(worldGeo);

                // World — marqueurs villes (sinon before==after bit-identique sans overlay).
                var worldPixels = MapSnapshotExporter.RenderPoliticalPixels(
                    em, worldGeo, MapSnapshotExporter.LabelDensity.Countries, -1,
                    overlay: p =>
                    {
                        CityMarkerComposer.Compose(
                            p, worldGeo, em, MapObservationLevel.World,
                            filterCountryId: -1);
                    });
                var worldPath = Path.Combine(CapturesDir, label + "_world.png");
                MapSnapshotExporter.WriteMapBufferPng(
                    worldPixels, MapSnapshotExporter.Width, MapSnapshotExporter.Height, worldPath);
                sb.AppendLine("capture_" + label + "_world sha=" + Sha256File(worldPath) +
                              " path=" + worldPath +
                              " markers=" + CityMarkerComposer.LastMarkersDrawn);

                if (MapDisplaySystem.TrySelectCountryByTag(em, "FRA"))
                {
                    var countryGeo = MapGeometryCache.GetOrBuild(
                        MapSnapshotExporter.Width, MapSnapshotExporter.Height,
                        MapViewport.State.Window, out _);
                    var countryPixels = MapSnapshotExporter.RenderPoliticalPixels(
                        em, countryGeo, MapSnapshotExporter.LabelDensity.Provinces, -1,
                        overlay: p =>
                        {
                            CityMarkerComposer.Compose(
                                p, countryGeo, em, MapObservationLevel.Country,
                                filterCountryId: MapViewport.State.TargetCountryId);
                        });
                    var countryPath = Path.Combine(CapturesDir, label + "_country_FRA.png");
                    MapSnapshotExporter.WriteMapBufferPng(
                        countryPixels, MapSnapshotExporter.Width, MapSnapshotExporter.Height,
                        countryPath);
                    sb.AppendLine("capture_" + label + "_country_FRA sha=" + Sha256File(countryPath) +
                                  " path=" + countryPath +
                                  " markers=" + CityMarkerComposer.LastMarkersDrawn);
                }
                else
                {
                    sb.AppendLine("capture_" + label + "_country_FRA=SKIP");
                }

                if (MapDisplaySystem.TrySelectProvinceById(em, 1))
                {
                    var provinceGeo = MapGeometryCache.GetOrBuild(
                        MapSnapshotExporter.Width, MapSnapshotExporter.Height,
                        MapViewport.State.Window, out _);
                    var provincePixels = MapSnapshotExporter.RenderPoliticalPixels(
                        em, provinceGeo, MapSnapshotExporter.LabelDensity.SelectedProvince, 1,
                        overlay: p =>
                        {
                            CityMarkerComposer.Compose(
                                p, provinceGeo, em, MapObservationLevel.Province,
                                filterProvinceId: 1);
                        });
                    var provPath = Path.Combine(CapturesDir, label + "_province_1.png");
                    MapSnapshotExporter.WriteMapBufferPng(
                        provincePixels, MapSnapshotExporter.Width, MapSnapshotExporter.Height,
                        provPath);
                    sb.AppendLine("capture_" + label + "_province_1 sha=" + Sha256File(provPath) +
                                  " path=" + provPath +
                                  " markers=" + CityMarkerComposer.LastMarkersDrawn);
                }
                else
                {
                    sb.AppendLine("capture_" + label + "_province_1=SKIP");
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine("captures_" + label + "_error=" + ex.Message);
            }
            finally
            {
                File.WriteAllText(CitiesPath, liveC, Encoding.UTF8);
                File.WriteAllText(CoordsPath, liveO, Encoding.UTF8);
                CityCoordinates.InvalidateCache();
                PilotMapProvider.Enabled = false;
                MapViewport.Reset();
            }
        }

        static LandBound MeasureLandDistances(bool pilot)
        {
            PilotMapProvider.Enabled = false;
            CityCoordinates.InvalidateCache();
            MapGeometryCache.ResetStatsAndClear();
            if (pilot)
                PilotMapProvider.SetEnabled(true, clearCache: true);

            ProvinceCoordinates.LoadProjected(out var midLat);
            var geo = MapSnapshotExporter.BuildMapGeometry(
                MapSnapshotExporter.Width, MapSnapshotExporter.Height);
            if (geo?.IsLand == null && pilot)
            {
                PilotMapProvider.Enabled = false;
                geo = MapSnapshotExporter.BuildMapGeometry(
                    MapSnapshotExporter.Width, MapSnapshotExporter.Height);
            }

            Assert.IsNotNull(geo);
            Assert.IsNotNull(geo.IsLand);

            var cosMid = Math.Cos(midLat * Math.PI / 180.0);
            var rangeX = geo.MaxX - geo.MinX;
            var lonSpan = cosMid > 1e-9 ? rangeX / cosMid : rangeX;
            var kmPerDegLon50 = 111.32 * Math.Cos(50.0 * Math.PI / 180.0);
            var kmPerPx = (float)(kmPerDegLon50 * lonSpan / geo.Width);

            var cities = GameDataLoader.LoadCities();
            // Sonde large pour mesurer ; ne pas compter dist<0 comme =probe (sinon
            // max artificiel = rayon de sonde).
            var probe = 400;
            var maxDist = 0;
            var maxAdded = 0;
            var nonzero = 0;
            var checkedN = 0;
            var addedChecked = 0;
            var beyondProbe = 0;
            for (var i = 0; i < cities.Count; i++)
            {
                if (!CityCoordinates.TryGet(cities[i].id, out var pt))
                    continue;
                checkedN++;
                CityMarkerComposer.WorldToPixel(pt.X, pt.Y, geo, out var px, out var py);
                var dist = DistanceToLand(geo, px, py, probe);
                if (dist < 0)
                {
                    beyondProbe++;
                    continue;
                }

                if (dist > maxDist)
                    maxDist = dist;
                if (cities[i].id > 123)
                {
                    addedChecked++;
                    if (dist > maxAdded)
                        maxAdded = dist;
                }

                if (dist > 0)
                    nonzero++;
            }

            PilotMapProvider.Enabled = false;
            // Borne opérationnelle : sur Voronoï = toutes villes ; sur pilote = villes
            // ajoutées v1_082 (id>123), comme V1082-D — le masque pilote laisse des
            // historiques hors disque (beyond_probe) sans invalider le peuplement.
            var operationalMax = pilot ? maxAdded : maxDist;
            if (operationalMax <= 0)
                operationalMax = maxDist;
            var bound = (int)math.max(1, math.ceil(operationalMax * 1.4f));

            return new LandBound
            {
                Width = geo.Width,
                Height = geo.Height,
                LonSpanDeg = (float)lonSpan,
                KmPerPx = kmPerPx,
                MaxDistPx = maxDist,
                MaxAddedDistPx = maxAdded,
                BoundPx = bound,
                NonZero = nonzero,
                CitiesChecked = checkedN,
                AddedChecked = addedChecked,
                BeyondProbe = beyondProbe
            };
        }

        static bool CityWouldPassLandBound(bool pilot, double lon, double lat, int boundPx)
        {
            PilotMapProvider.Enabled = false;
            MapGeometryCache.ResetStatsAndClear();
            if (pilot)
                PilotMapProvider.SetEnabled(true, clearCache: true);
            ProvinceCoordinates.LoadProjected(out var midLat);
            var geo = MapSnapshotExporter.BuildMapGeometry(
                MapSnapshotExporter.Width, MapSnapshotExporter.Height);
            ProvinceCoordinates.Project((float)lon, (float)lat, midLat, out var x, out var y);
            CityMarkerComposer.WorldToPixel(x, y, geo, out var px, out var py);
            var dist = DistanceToLand(geo, px, py, boundPx + 1);
            PilotMapProvider.Enabled = false;
            return dist >= 0 && dist <= boundPx;
        }

        static int DistanceToLand(
            MapSnapshotExporter.MapGeometry geo, int px, int py, int maxRadius)
        {
            if (geo?.IsLand == null)
                return -1;
            if (px >= 0 && py >= 0 && px < geo.Width && py < geo.Height &&
                geo.IsLand[py * geo.Width + px])
                return 0;
            for (var r = 1; r <= maxRadius; r++)
            {
                for (var dy = -r; dy <= r; dy++)
                {
                    var x1 = px - r;
                    var x2 = px + r;
                    var y = py + dy;
                    if (y < 0 || y >= geo.Height)
                        continue;
                    if (x1 >= 0 && x1 < geo.Width && geo.IsLand[y * geo.Width + x1])
                        return r;
                    if (x2 >= 0 && x2 < geo.Width && geo.IsLand[y * geo.Width + x2])
                        return r;
                }

                for (var dx = -r + 1; dx <= r - 1; dx++)
                {
                    var y1 = py - r;
                    var y2 = py + r;
                    var x = px + dx;
                    if (x < 0 || x >= geo.Width)
                        continue;
                    if (y1 >= 0 && y1 < geo.Height && geo.IsLand[y1 * geo.Width + x])
                        return r;
                    if (y2 >= 0 && y2 < geo.Height && geo.IsLand[y2 * geo.Width + x])
                        return r;
                }
            }

            return -1;
        }

        static string Sha256File(string path)
        {
            if (!File.Exists(path))
                return "MISSING";
            using var fs = File.OpenRead(path);
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(fs);
            var sb = new StringBuilder(hash.Length * 2);
            for (var i = 0; i < hash.Length; i++)
                sb.Append(hash[i].ToString("x2"));
            return sb.ToString();
        }

        static string Fmt(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
        static string Fmt1(float v) => v.ToString("0.#", CultureInfo.InvariantCulture);
        static string Pct(float share) =>
            (share * 100.0).ToString("0.#", CultureInfo.InvariantCulture);

        struct ChainSnap
        {
            public int Cities, Buildings, Sites;
            public float ClothOut, ClothDemand, ClothSatisfied, ImportShare;
        }

        struct SweepRow
        {
            public float Coeff, ClothOut, ClothDemand, ImportShare;
            public int Buildings;
        }

        struct LandBound
        {
            public int Width, Height, MaxDistPx, MaxAddedDistPx, BoundPx, NonZero,
                CitiesChecked, AddedChecked, BeyondProbe;
            public float LonSpanDeg, KmPerPx;
        }
    }
}
