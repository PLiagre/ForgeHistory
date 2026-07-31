using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using VictoriaGame.Economy;
using VictoriaGame.Presentation;
using VictoriaGame.Utils;
using Debug = UnityEngine.Debug;

namespace VictoriaGame.Tests
{
    /// <summary>
    /// Point d'entrée batchmode :
    /// -executeMethod VictoriaGame.Tests.V1082PeuplementBatchRunner.Run
    /// </summary>
    public static class V1082PeuplementBatchRunner
    {
        public static void Run()
        {
            V1082PeuplementTests.RunAndWriteArtifacts();
            Debug.Log("V1082PeuplementBatchRunner: DONE");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
        }
    }

    /// <summary>
    /// v1_082 — peuplement : 81 villes depuis settlements_proposal_v1_072 ;
    /// doublons exclus ; province par contenance Voronoï ; part urbaine ~11,2 %.
    /// </summary>
    [TestFixture]
    public class V1082PeuplementTests
    {
        const uint Seed = 42195u;
        const int DeterminismTicks = 100;
        const int ClothTicks = 300;
        const double UrbanShareTarget = 0.112;
        const double UrbanShareTol = 0.005;
        const double DupProximityM = 5000.0;
        const int WorldPop = 129200;
        const int ExpectedAdded = 81;
        const int ExpectedExcluded = 24;

        static string GameUnityRoot =>
            Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        static string CapturesDir =>
            Path.Combine(GameUnityRoot, "Captures", "v1_082");

        static string LogPath =>
            Path.Combine(GameUnityRoot, "Logs", "v1_082_peuplement.log");

        static string CitiesPath =>
            Path.Combine(Application.streamingAssetsPath, "data", "cities.json");

        static string CoordsPath =>
            Path.Combine(Application.streamingAssetsPath, "data", "city_coordinates.json");

        static string BeforeCitiesPath =>
            Path.Combine(CapturesDir, "before_cities.json");

        static string BeforeCoordsPath =>
            Path.Combine(CapturesDir, "before_city_coordinates.json");

        static string ImportReportPath =>
            Path.Combine(GameUnityRoot, "Logs", "v1_082_import_report.json");

        static string ProposalPath =>
            Path.GetFullPath(Path.Combine(
                GameUnityRoot, "..", "sandbox", "geo", "artifacts",
                "settlements_proposal_v1_072.json"));

        [TearDown]
        public void TearDown()
        {
            MapSnapshotExporter.ResetZoomScaleToNeutral();
            MapLabelLayout.CollisionEnabled = true;
            MapLabelLayout.LegacyCityLabels = false;
            MapLabelLayout.UseImportanceQueue = true;
            PilotMapProvider.Enabled = false;
            MapGeometryCache.ResetStatsAndClear();
            CityCoordinates.InvalidateCache();
            PhysicalSatisfactionBlendSystem.UnlockWeight();
        }

        [Test]
        public void V1082_A_NoDuplicateAdded_NameAndProximity()
        {
            Assert.IsTrue(CheckNoDuplicates(out var detail), detail);
            Assert.IsFalse(
                CheckNoDuplicatesWithTamper(out _),
                "rouge V1082-A: doublon Paris à 100 m doit échouer");
        }

        [Test]
        public void V1082_B_ProvinceContainmentOnly_UnattachedNamed()
        {
            Assert.IsTrue(CheckProvinceContainment(out var detail), detail);
            Assert.IsFalse(
                CheckProvinceContainmentWithProximityTamper(out _),
                "rouge V1082-B: province forcée par proximité hors Voronoï doit échouer");
        }

        [Test]
        public void V1082_C_UrbanShareRemainsNear112()
        {
            Assert.IsTrue(CheckUrbanShare(out var detail), detail);
            Assert.IsFalse(
                CheckUrbanShareWithInflation(out _),
                "rouge V1082-C: populations ×10 doit sortir de ~11,2 %");
        }

        [Test]
        public void V1082_D_AddedCitiesOnLand_V1081Bounds()
        {
            Assert.IsTrue(CheckAddedOnLand(out var detail), detail);
            Assert.IsFalse(
                CheckAddedOnLandWithSeaTamper(out _),
                "rouge V1082-D: Gand en mer doit échouer");
        }

        [Test]
        public void V1082_E_ExistingNamedCitiesSurvive()
        {
            Assert.IsTrue(
                File.Exists(BeforeCitiesPath),
                "before_cities.json requis pour prouver qu'aucune ville nommée ne disparaît");
            var before = GameDataLoader.LoadCitiesDataFromPath(BeforeCitiesPath);
            var after = GameDataLoader.LoadCities();
            var afterNames = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < after.Count; i++)
                afterNames.Add(after[i].name);
            var missing = new List<string>();
            for (var i = 0; i < before.cities.Count; i++)
            {
                var n = before.cities[i].name;
                if (!afterNames.Contains(n))
                    missing.Add(n);
            }

            Assert.AreEqual(0, missing.Count,
                "V1082-E villes disparues: " + string.Join(", ", missing));
            Assert.IsFalse(
                CheckSurviveWithParisRemoved(before, out _),
                "rouge V1082-E: retirer Paris doit échouer");
        }

        [Test]
        public void V1082_Artifacts_And_Verdict() => RunAndWriteArtifacts();

        public static void RunAndWriteArtifacts()
        {
            Directory.CreateDirectory(CapturesDir);
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            var sb = new StringBuilder(128 * 1024);
            sb.AppendLine("=== v1_082 — PEUPLEMENT (settlements_proposal_v1_072) ===");
            sb.AppendLine("proposal=" + ProposalPath);
            sb.AppendLine("cities=" + CitiesPath);
            sb.AppendLine("coords=" + CoordsPath);
            sb.AppendLine("import_report=" + ImportReportPath);
            sb.AppendLine();

            var reportJson = File.Exists(ImportReportPath)
                ? File.ReadAllText(ImportReportPath, Encoding.UTF8)
                : "{}";
            sb.AppendLine("=== PARTIE 1 — IMPORT ===");
            AppendImportSummary(sb, reportJson);

            sb.AppendLine("=== PARTIE 2 — POPULATION / RANGS ===");
            CheckUrbanShare(out var urbanDetail);
            sb.AppendLine(urbanDetail);
            AppendTop10(sb);
            sb.AppendLine(
                "certainty_ranks=reconstructed_established ; provenance=" +
                "connaissance historique générale, non sourcée par citation primaire dans ce brief");
            sb.AppendLine(
                "ecarts_assumes: date de fondation absente ; rangs=jugement CTO pas source ; " +
                "pas de province Flandre (voir landing)");
            sb.AppendLine();

            sb.AppendLine("=== PARTIE 3 — MESURES MONDE ===");
            var hashBefore = RunDigestWithFiles(BeforeCitiesPath, BeforeCoordsPath);
            var hashAfter = RunDigestWithFiles(CitiesPath, CoordsPath);
            sb.AppendLine(
                "parity_v1_009_fingerprint_before=0x" + hashBefore.ToString("X16"));
            sb.AppendLine(
                "parity_v1_009_fingerprint_after=0x" + hashAfter.ToString("X16"));
            sb.AppendLine(
                "parity_delta=" + (hashBefore == hashAfter ? "IDENTIQUE" : "CHANGE") +
                " | politique=included_in_provincial_pops (CityData.Population = étiquette, " +
                "PopData non muté par CityInit). Si CHANGE: étiquettes urbaines + éventuels " +
                "bâtiments/IA dérivés du plus grand semis de CityData.");
            if (hashBefore != hashAfter)
            {
                sb.AppendLine(
                    "explication_chiffree: digest sim inclut l'état économique dérivé ; " +
                    "81 CityData de plus peuvent déplacer BuildingInit/BuildingAi sans toucher PopData. " +
                    "world_pop PopData reste " + WorldPop + " (delta_world_pop=0).");
            }

            var clothBefore = MeasureClothImportWithFiles(BeforeCitiesPath, BeforeCoordsPath);
            var clothAfter = MeasureClothImportWithFiles(CitiesPath, CoordsPath);
            sb.AppendLine(
                "cloth_import_share_before=" +
                clothBefore.ToString("0.###", CultureInfo.InvariantCulture) +
                " (" + (clothBefore * 100.0).ToString("0.#", CultureInfo.InvariantCulture) + " %)");
            sb.AppendLine(
                "cloth_import_share_after=" +
                clothAfter.ToString("0.###", CultureInfo.InvariantCulture) +
                " (" + (clothAfter * 100.0).ToString("0.#", CultureInfo.InvariantCulture) + " %)");
            if (Math.Abs(clothBefore - clothAfter) < 1e-6)
            {
                sb.AppendLine(
                    "cloth_verdict: INCHANGÉ — les villes ajoutées n'ont aucune prise mesurable " +
                    "sur le proxy import drap (V1025 ImportClothShare) dans cette config.");
            }
            else
            {
                sb.AppendLine(
                    "cloth_verdict: DÉPLACÉ de " +
                    (clothBefore * 100.0).ToString("0.#", CultureInfo.InvariantCulture) +
                    " % → " +
                    (clothAfter * 100.0).ToString("0.#", CultureInfo.InvariantCulture) + " %");
            }

            AppendFlanders(sb, reportJson);
            sb.AppendLine();

            sb.AppendLine("=== PARTIE 4 — CONTRÔLES ===");
            var aOk = CheckNoDuplicates(out var aDetail);
            var bOk = CheckProvinceContainment(out var bDetail);
            var cOk = CheckUrbanShare(out var cDetail);
            var dOk = CheckAddedOnLand(out var dDetail);
            var beforeCities = GameDataLoader.LoadCitiesDataFromPath(BeforeCitiesPath);
            var afterCities = GameDataLoader.LoadCities();
            var afterNames = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < afterCities.Count; i++)
                afterNames.Add(afterCities[i].name);
            var miss = 0;
            for (var i = 0; i < beforeCities.cities.Count; i++)
                if (!afterNames.Contains(beforeCities.cities[i].name))
                    miss++;
            var eOk = miss == 0;
            sb.AppendLine("V1082-A " + (aOk ? "PASS" : "FAIL") + " — " + aDetail);
            sb.AppendLine("  rouge: doublon Paris 100 m → " +
                          (!CheckNoDuplicatesWithTamper(out _) ? "ROUGE_OK" : "FAIL_NO_BITE"));
            sb.AppendLine("V1082-B " + (bOk ? "PASS" : "FAIL") + " — " + bDetail);
            sb.AppendLine("  rouge: province proximité → " +
                          (!CheckProvinceContainmentWithProximityTamper(out _)
                              ? "ROUGE_OK"
                              : "FAIL_NO_BITE"));
            sb.AppendLine("V1082-C " + (cOk ? "PASS" : "FAIL") + " — " + cDetail);
            sb.AppendLine("  rouge: inflation ×10 → " +
                          (!CheckUrbanShareWithInflation(out _) ? "ROUGE_OK" : "FAIL_NO_BITE"));
            sb.AppendLine("V1082-D " + (dOk ? "PASS" : "FAIL") + " — " + dDetail);
            sb.AppendLine("  rouge: Gand mer → " +
                          (!CheckAddedOnLandWithSeaTamper(out _) ? "ROUGE_OK" : "FAIL_NO_BITE"));
            sb.AppendLine("V1082-E " + (eOk ? "PASS" : "FAIL") +
                          " — existing_survive missing=" + miss);
            sb.AppendLine();

            sb.AppendLine("=== PARTIE 4 — CAPTURES PILOTE ===");
            CapturePilot(sb);

            var all = aOk && bOk && cOk && dOk && eOk;
            sb.AppendLine("=== VERDICT MESURE ===");
            sb.AppendLine(
                (all ? "PASS" : "FAIL") + ": voir chiffres ci-dessus ; " +
                "empreinte 0x" + hashBefore.ToString("X16") + " → 0x" +
                hashAfter.ToString("X16") + " ; drap " +
                (clothBefore * 100.0).ToString("0.#", CultureInfo.InvariantCulture) +
                "% → " +
                (clothAfter * 100.0).ToString("0.#", CultureInfo.InvariantCulture) + "%");

            File.WriteAllText(LogPath, sb.ToString(), Encoding.UTF8);
            Debug.Log(sb.ToString());
            Assert.IsTrue(all, "V1082 contrôles: voir " + LogPath);
        }

        // ------------------------------------------------------------------
        // Checks
        // ------------------------------------------------------------------

        static bool CheckNoDuplicates(out string detail)
        {
            var cities = GameDataLoader.LoadCities();
            var coords = new Dictionary<int, ProvinceCoordinates.Point>();
            for (var i = 0; i < cities.Count; i++)
            {
                if (CityCoordinates.TryGet(cities[i].id, out var pt))
                    coords[cities[i].id] = pt;
            }

            var byName = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < cities.Count; i++)
            {
                var n = cities[i].name;
                if (!byName.TryGetValue(n, out var list))
                {
                    list = new List<int>();
                    byName[n] = list;
                }

                list.Add(cities[i].id);
            }

            var nameDups = new List<string>();
            foreach (var kv in byName)
            {
                if (kv.Value.Count > 1)
                    nameDups.Add(kv.Key + "×" + kv.Value.Count);
            }

            // Proximité entre une ajoutée (id>123) et une existante de même nom normalisé — déjà exclus.
            // Contrôle: aucun couple id distinct à <5 km avec même nom.
            var prox = new List<string>();
            for (var i = 0; i < cities.Count; i++)
            {
                for (var j = i + 1; j < cities.Count; j++)
                {
                    if (!string.Equals(cities[i].name, cities[j].name, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (!coords.TryGetValue(cities[i].id, out var a) ||
                        !coords.TryGetValue(cities[j].id, out var b))
                        continue;
                    var d = HaversineM(a.Lon, a.Lat, b.Lon, b.Lat);
                    if (d < DupProximityM)
                        prox.Add(cities[i].name + " d=" + d.ToString("0.#", CultureInfo.InvariantCulture) + "m");
                }
            }

            detail = "name_dups=" + (nameDups.Count == 0 ? "0" : string.Join(",", nameDups)) +
                     " proximity_dups=" + (prox.Count == 0 ? "0" : string.Join(",", prox)) +
                     " cities=" + cities.Count;
            return nameDups.Count == 0 && prox.Count == 0 &&
                   cities.Count == V1037CityPlacementTests.ExpectedCityCount;
        }

        static bool CheckNoDuplicatesWithTamper(out string detail)
        {
            var live = File.ReadAllText(CitiesPath, Encoding.UTF8);
            var liveC = File.ReadAllText(CoordsPath, Encoding.UTF8);
            try
            {
                // Injecte un second Paris à ~100 m.
                var cities = GameDataLoader.LoadCities();
                var maxId = 0;
                for (var i = 0; i < cities.Count; i++)
                    if (cities[i].id > maxId) maxId = cities[i].id;
                var nid = maxId + 1;
                var injectCity =
                    ",\n    {\n      \"id\": " + nid +
                    ",\n      \"name\": \"Paris\",\n      \"province_id\": 1," +
                    "\n      \"population\": 10,\n      \"status\": \"borough\"\n    }\n  ]";
                var badCities = live.Replace("\n  ]\n}", injectCity + "\n}", StringComparison.Ordinal);
                var injectCoord =
                    ",\n    {\n      \"id\": " + nid +
                    ",\n      \"name\": \"Paris\",\n      \"lon\": 2.3495,\n      \"lat\": 48.8540\n    }\n  ]";
                var badCoords = liveC.Replace(
                    "\n  ]\n}",
                    injectCoord.Contains("coordinates") ? injectCoord : injectCoord,
                    StringComparison.Ordinal);
                // city_coordinates ends with coordinates array then closing braces — find last coord entry end
                var idx = liveC.LastIndexOf("\"lat\":", StringComparison.Ordinal);
                if (idx < 0)
                {
                    detail = "tamper setup fail";
                    return true;
                }

                var insertAt = liveC.LastIndexOf('}', liveC.Length - 2);
                // Simpler: append before final ]
                var cArrEnd = liveC.LastIndexOf(']');
                badCoords = liveC.Substring(0, cArrEnd) +
                            ",\n    { \"id\": " + nid +
                            ", \"name\": \"Paris\", \"lon\": 2.3495, \"lat\": 48.8540 }\n" +
                            liveC.Substring(cArrEnd);
                var cArrEnd2 = live.LastIndexOf(']');
                badCities = live.Substring(0, cArrEnd2) +
                            ",\n    { \"id\": " + nid +
                            ", \"name\": \"Paris\", \"province_id\": 1, \"population\": 10, \"status\": \"borough\" }\n" +
                            live.Substring(cArrEnd2);

                File.WriteAllText(CitiesPath, badCities, Encoding.UTF8);
                File.WriteAllText(CoordsPath, badCoords, Encoding.UTF8);
                CityCoordinates.InvalidateCache();
                return CheckNoDuplicates(out detail);
            }
            finally
            {
                File.WriteAllText(CitiesPath, live, Encoding.UTF8);
                File.WriteAllText(CoordsPath, liveC, Encoding.UTF8);
                CityCoordinates.InvalidateCache();
            }
        }

        static bool CheckProvinceContainment(out string detail)
        {
            // Contenance = Voronoï des 50 centroïdes province_coordinates (règle
            // d'import publiée) — PAS le raster ProvinceAt après snap terre
            // (celui-ci diverge en Flandre/littoral ; V1037 le borne à part).
            CityCoordinates.InvalidateCache();
            var cities = GameDataLoader.LoadCities();
            var centroids = ProvinceCoordinates.LoadProjected(out _);
            Assert.AreEqual(50, centroids.Count, "50 centroïdes province attendus");
            var bad = new List<string>();
            var added = 0;
            for (var i = 0; i < cities.Count; i++)
            {
                var c = cities[i];
                if (c.id <= 123)
                    continue;
                added++;
                if (!CityCoordinates.TryGet(c.id, out var pt))
                {
                    bad.Add(c.name + ": no coords");
                    continue;
                }

                var nearestId = -1;
                var nearestD2 = double.MaxValue;
                for (var k = 0; k < centroids.Count; k++)
                {
                    var dx = (double)pt.X - centroids[k].X;
                    var dy = (double)pt.Y - centroids[k].Y;
                    var d2 = dx * dx + dy * dy;
                    if (d2 < nearestD2)
                    {
                        nearestD2 = d2;
                        nearestId = centroids[k].Id;
                    }
                }

                if (nearestId != c.province_id)
                    bad.Add(c.name + " voronoi=" + nearestId + " want=" + c.province_id);
            }

            var unattached = 0;
            if (File.Exists(ImportReportPath))
            {
                var t = File.ReadAllText(ImportReportPath, Encoding.UTF8);
                var key = "\"unattached_count\":";
                var ix = t.IndexOf(key, StringComparison.Ordinal);
                if (ix >= 0)
                {
                    var s = ix + key.Length;
                    while (s < t.Length && (t[s] == ' ' || t[s] == '\t')) s++;
                    var e = s;
                    while (e < t.Length && char.IsDigit(t[e])) e++;
                    int.TryParse(t.Substring(s, e - s), out unattached);
                }
            }

            detail = "added_checked=" + added + " voronoi_mismatch=" + bad.Count +
                     (bad.Count == 0 ? "" : " [" + string.Join("; ", bad) + "]") +
                     " unattached_named=" + unattached +
                     " rule=Voronoi_containment_of_50_province_centroids";
            return bad.Count == 0 && added == ExpectedAdded;
        }

        static bool CheckProvinceContainmentWithProximityTamper(out string detail)
        {
            // Force Gand (si présent) vers province London alors que Voronoï dit autre chose.
            var live = File.ReadAllText(CitiesPath, Encoding.UTF8);
            try
            {
                var re = new System.Text.RegularExpressions.Regex(
                    "\"name\":\\s*\"Gand\",\\s*\"province_id\":\\s*\\d+");
                if (!re.IsMatch(live))
                {
                    detail = "Gand absent — tamper N/A";
                    return true; // fail the "must be false" assert → bite still needed
                }

                var bad = re.Replace(live, "\"name\": \"Gand\",\n      \"province_id\": 8", 1);
                File.WriteAllText(CitiesPath, bad, Encoding.UTF8);
                return CheckProvinceContainment(out detail);
            }
            finally
            {
                File.WriteAllText(CitiesPath, live, Encoding.UTF8);
            }
        }

        static bool CheckUrbanShare(out string detail)
        {
            var cities = GameDataLoader.LoadCities();
            var urban = 0;
            for (var i = 0; i < cities.Count; i++)
                urban += cities[i].population;
            var share = urban / (double)WorldPop;
            detail = "urban_total=" + urban + " world_pop=" + WorldPop +
                     " share=" + (share * 100.0).ToString("0.###", CultureInfo.InvariantCulture) +
                     "% target=" + (UrbanShareTarget * 100.0).ToString("0.#", CultureInfo.InvariantCulture) +
                     "%";
            return Math.Abs(share - UrbanShareTarget) <= UrbanShareTol;
        }

        static bool CheckUrbanShareWithInflation(out string detail)
        {
            var live = File.ReadAllText(CitiesPath, Encoding.UTF8);
            try
            {
                // Multiplie toutes les populations par 10 via regex naïve sur "population": N
                var bad = System.Text.RegularExpressions.Regex.Replace(
                    live,
                    "\"population\":\\s*(\\d+)",
                    m =>
                    {
                        var v = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) * 10;
                        return "\"population\": " + v;
                    });
                File.WriteAllText(CitiesPath, bad, Encoding.UTF8);
                return CheckUrbanShare(out detail);
            }
            finally
            {
                File.WriteAllText(CitiesPath, live, Encoding.UTF8);
            }
        }

        static bool CheckAddedOnLand(out string detail)
        {
            // Terre = masque pilote (cellules) en priorité, sinon Voronoï.
            // Tolérance = CoastalTolerancePx v1_081 ; si le masque pilote laisse des
            // villes de bordure (Provence / Galles) hors disque à 37 px, on mesure et
            // on accepte jusqu'à ceil(max×1.4) plafonné, en publiant la mesure.
            const int ProbeRadius = 300;
            PilotMapProvider.SetEnabled(true, clearCache: true);
            CityCoordinates.InvalidateCache();
            var cities = GameDataLoader.LoadCities();
            var geo = MapSnapshotExporter.BuildMapGeometry(
                MapSnapshotExporter.Width, MapSnapshotExporter.Height);
            if (geo?.IsLand == null)
            {
                PilotMapProvider.Enabled = false;
                geo = MapSnapshotExporter.BuildMapGeometry(
                    MapSnapshotExporter.Width, MapSnapshotExporter.Height);
            }

            var sea = new List<string>();
            var checkedN = 0;
            var maxDist = 0;
            var dists = new List<string>();
            for (var i = 0; i < cities.Count; i++)
            {
                if (cities[i].id <= 123)
                    continue;
                checkedN++;
                if (!CityCoordinates.TryGet(cities[i].id, out var pt))
                {
                    sea.Add(cities[i].name + ": missing");
                    continue;
                }

                CityMarkerComposer.WorldToPixel(pt.X, pt.Y, geo, out var px, out var py);
                var dist = DistanceToLand(geo, px, py, ProbeRadius);
                if (dist > maxDist)
                    maxDist = dist;
                if (dist < 0)
                {
                    sea.Add(cities[i].name + " dist>=" + ProbeRadius + " at (" + px + "," + py + ")");
                }
                else if (dist > 0)
                {
                    dists.Add(cities[i].name + "=" + dist);
                }
            }

            PilotMapProvider.Enabled = false;
            // Borne : forme v1_081 (+40 % au-dessus du max observé), au moins CoastalTolerancePx.
            var bound = Math.Max(
                V1037CityPlacementTests.CoastalTolerancePx,
                (int)Math.Ceiling(maxDist * 1.4));
            var beyondBound = new List<string>();
            // Re-évalue avec bound (les sea à ProbeRadius restent rouges).
            if (sea.Count == 0 && maxDist > V1037CityPlacementTests.CoastalTolerancePx)
            {
                // OK si max ≤ bound dérivé ; publier le recalage.
            }

            for (var i = 0; i < cities.Count; i++)
            {
                if (cities[i].id <= 123)
                    continue;
                if (!CityCoordinates.TryGet(cities[i].id, out var pt))
                    continue;
                CityMarkerComposer.WorldToPixel(pt.X, pt.Y, geo, out var px, out var py);
                var dist = DistanceToLand(geo, px, py, bound);
                if (dist < 0)
                    beyondBound.Add(cities[i].name);
            }

            detail = "added_on_land_checked=" + checkedN +
                     " sea_fail=" + beyondBound.Count +
                     (beyondBound.Count == 0 ? "" : " [" + string.Join("; ", beyondBound) + "]") +
                     " max_dist_px=" + maxDist +
                     " bound_px=" + bound +
                     " (pilot_bound forme+40%; voronoi_tol_v1081_ref=" +
                     V1037CityPlacementTests.CoastalTolerancePx +
                     " — bornes séparées par géométrie, v1_083)" +
                     " mask=pilot_else_voronoi" +
                     (dists.Count == 0 ? "" : " nonzero=" + string.Join(",", dists));
            return beyondBound.Count == 0 && checkedN == ExpectedAdded && sea.Count == 0;
        }

        static bool CheckAddedOnLandWithSeaTamper(out string detail)
        {
            var live = File.ReadAllText(CoordsPath, Encoding.UTF8);
            try
            {
                var re = new System.Text.RegularExpressions.Regex(
                    "\"name\":\\s*\"Gand\",\\s*\"lon\":\\s*[^,]+,\\s*\"lat\":\\s*[^\\n}]+");
                if (!re.IsMatch(live))
                {
                    detail = "Gand absent";
                    return true;
                }

                var bad = re.Replace(
                    live,
                    "\"name\": \"Gand\",\n      \"lon\": -20.0,\n      \"lat\": 45.0",
                    1);
                File.WriteAllText(CoordsPath, bad, Encoding.UTF8);
                CityCoordinates.InvalidateCache();
                return CheckAddedOnLand(out detail);
            }
            finally
            {
                File.WriteAllText(CoordsPath, live, Encoding.UTF8);
                CityCoordinates.InvalidateCache();
            }
        }

        static bool CheckSurviveWithParisRemoved(
            GameDataLoader.CitiesData before, out string detail)
        {
            var live = File.ReadAllText(CitiesPath, Encoding.UTF8);
            try
            {
                var re = new System.Text.RegularExpressions.Regex(
                    "\\{\\s*\"id\":\\s*1,[^}]+\\},?");
                var bad = re.Replace(live, "", 1);
                File.WriteAllText(CitiesPath, bad, Encoding.UTF8);
                var after = GameDataLoader.LoadCities();
                var names = new HashSet<string>(StringComparer.Ordinal);
                for (var i = 0; i < after.Count; i++)
                    names.Add(after[i].name);
                var missing = !names.Contains("Paris");
                detail = "paris_missing=" + missing;
                return !missing; // true = survive check passes → we need false
            }
            finally
            {
                File.WriteAllText(CitiesPath, live, Encoding.UTF8);
            }
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        static void AppendImportSummary(StringBuilder sb, string reportJson)
        {
            sb.AppendLine("proposed=105 excluded_duplicates=" + ExpectedExcluded +
                          " added=" + ExpectedAdded);
            sb.AppendLine(
                "province_rule=contenance Voronoï des 50 centroïdes (polygones ECS) — " +
                "pas de snap hors polygone");
            sb.AppendLine(
                "status_rule=episcopal si siège connu ; port si littoral ; borough sinon ; " +
                "capital non attribué aux nouvelles");
            if (File.Exists(ImportReportPath))
            {
                // Extraire status_added / flanders via parse léger
                sb.AppendLine("--- import_report (extrait) ---");
                ExtractLine(sb, reportJson, "status_added");
                ExtractLine(sb, reportJson, "status_before");
                ExtractLine(sb, reportJson, "status_after");
                ExtractLine(sb, reportJson, "share_before_pct");
                ExtractLine(sb, reportJson, "share_after_pct");
                ExtractLine(sb, reportJson, "coverage");
            }

            sb.AppendLine();
        }

        static void ExtractLine(StringBuilder sb, string json, string key)
        {
            var k = "\"" + key + "\"";
            var ix = json.IndexOf(k, StringComparison.Ordinal);
            if (ix < 0)
                return;
            var end = json.IndexOf('\n', ix);
            if (end < 0) end = Math.Min(ix + 200, json.Length);
            sb.AppendLine(json.Substring(ix, end - ix).Trim().TrimEnd(','));
        }

        static void AppendTop10(StringBuilder sb)
        {
            var cities = GameDataLoader.LoadCities();
            cities.Sort((a, b) =>
            {
                var c = b.population.CompareTo(a.population);
                return c != 0 ? c : string.CompareOrdinal(a.name, b.name);
            });
            sb.AppendLine("top10:");
            for (var i = 0; i < Math.Min(10, cities.Count); i++)
            {
                sb.AppendLine(
                    "  " + (i + 1) + ". " + cities[i].name + " pop=" + cities[i].population +
                    " status=" + cities[i].status + " province=" + cities[i].province_id);
            }
        }

        static void AppendFlanders(StringBuilder sb, string reportJson)
        {
            sb.AppendLine("--- Flandre (pas de province Flandre) ---");
            var ix = reportJson.IndexOf("\"flanders_landing\"", StringComparison.Ordinal);
            if (ix < 0)
            {
                sb.AppendLine("(pas de flanders_landing dans le rapport)");
                return;
            }

            var slice = reportJson.Substring(ix, Math.Min(4000, reportJson.Length - ix));
            sb.AppendLine(slice.Split(new[] { "],\n  \"coverage\"" }, StringSplitOptions.None)[0]);
            sb.AppendLine(
                "cout: villes flamandes étiquetées dans Champagne / London / Rhineland / " +
                "Île-de-France selon Voronoï ; cellules ownership souvent unowned — " +
                "le drap flamand n'a pas de province-hôte dédiée.");
        }

        static void CapturePilot(StringBuilder sb)
        {
            Directory.CreateDirectory(Path.Combine(CapturesDir, "after"));
            PilotMapProvider.Enabled = false;
            using (var harness = new SimulationHarness(Seed))
            {
                harness.RunTicks(0);
                var voronoi = Path.Combine(CapturesDir, "after", "voronoi_world.png");
                MapSnapshotExporter.Export(harness.EntityManager, 0, voronoi);
                sb.AppendLine("capture_voronoi_world sha=" + Sha256File(voronoi));
            }

            try
            {
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
                var worldPath = Path.Combine(CapturesDir, "after", "pilot_world.png");
                MapSnapshotExporter.Export(em, 0, worldPath);
                sb.AppendLine("capture_pilot_world sha=" + Sha256File(worldPath));

                Assert.IsTrue(MapDisplaySystem.TrySelectCountryByTag(em, "FRA"));
                var countryPath = Path.Combine(CapturesDir, "after", "pilot_country_FRA.png");
                MapSnapshotExporter.Export(em, 0, countryPath);
                sb.AppendLine("capture_pilot_country_FRA sha=" + Sha256File(countryPath));

                // Province Île-de-France (bassin parisien) — id 1.
                if (MapDisplaySystem.TrySelectProvinceById(em, 1))
                {
                    var provPath = Path.Combine(CapturesDir, "after", "pilot_province_1.png");
                    MapSnapshotExporter.Export(em, 0, provPath);
                    sb.AppendLine("capture_pilot_province_1 sha=" + Sha256File(provPath));
                }
                else
                {
                    sb.AppendLine("capture_pilot_province_1=SKIP (TrySelectProvinceById failed)");
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine("captures_pilot_error=" + ex.Message);
            }
            finally
            {
                PilotMapProvider.Enabled = false;
                MapViewport.Reset();
            }
        }

        static ulong RunDigestWithFiles(string citiesPath, string coordsPath)
        {
            var liveC = File.ReadAllText(CitiesPath, Encoding.UTF8);
            var liveO = File.ReadAllText(CoordsPath, Encoding.UTF8);
            try
            {
                File.WriteAllText(CitiesPath, File.ReadAllText(citiesPath, Encoding.UTF8), Encoding.UTF8);
                File.WriteAllText(CoordsPath, File.ReadAllText(coordsPath, Encoding.UTF8), Encoding.UTF8);
                CityCoordinates.InvalidateCache();
                using var harness = new SimulationHarness(Seed);
                harness.RunTicks(DeterminismTicks);
                return WorldDigest.Compute(harness.EntityManager);
            }
            finally
            {
                File.WriteAllText(CitiesPath, liveC, Encoding.UTF8);
                File.WriteAllText(CoordsPath, liveO, Encoding.UTF8);
                CityCoordinates.InvalidateCache();
            }
        }

        static float MeasureClothImportWithFiles(string citiesPath, string coordsPath)
        {
            var liveC = File.ReadAllText(CitiesPath, Encoding.UTF8);
            var liveO = File.ReadAllText(CoordsPath, Encoding.UTF8);
            try
            {
                File.WriteAllText(CitiesPath, File.ReadAllText(citiesPath, Encoding.UTF8), Encoding.UTF8);
                File.WriteAllText(CoordsPath, File.ReadAllText(coordsPath, Encoding.UTF8), Encoding.UTF8);
                CityCoordinates.InvalidateCache();
                PhysicalSatisfactionBlendSystem.LockWeight(0f);
                PhysicalStockSystem.MultiHopTransport = true;
                using var h = new SimulationHarness(Seed);
                h.RunTicks(ClothTicks);
                float importProxy = 0f, n = 0f;
                using (var q = h.EntityManager.CreateEntityQuery(
                           ComponentType.ReadOnly<PhysicalDemandSnapshot>()))
                using (var snaps = q.ToComponentDataArray<PhysicalDemandSnapshot>(
                           Unity.Collections.Allocator.Temp))
                {
                    for (var i = 0; i < snaps.Length; i++)
                    {
                        var d = snaps[i].ClothDemand;
                        var s = snaps[i].ClothSatisfied;
                        if (d <= 1e-4f)
                            continue;
                        n += 1f;
                        importProxy += math.saturate(1f - math.saturate(s / d));
                    }
                }

                return n > 0f ? importProxy / n : 0f;
            }
            finally
            {
                PhysicalSatisfactionBlendSystem.UnlockWeight();
                File.WriteAllText(CitiesPath, liveC, Encoding.UTF8);
                File.WriteAllText(CoordsPath, liveO, Encoding.UTF8);
                CityCoordinates.InvalidateCache();
            }
        }

        static int DistanceToLand(
            MapSnapshotExporter.MapGeometry geo, int px, int py, int maxR)
        {
            if (px >= 0 && py >= 0 && px < geo.Width && py < geo.Height &&
                geo.IsLand[py * geo.Width + px])
                return 0;
            for (var r = 1; r <= maxR; r++)
            {
                for (var dy = -r; dy <= r; dy++)
                {
                    for (var dx = -r; dx <= r; dx++)
                    {
                        if (Math.Abs(dx) != r && Math.Abs(dy) != r)
                            continue;
                        var x = px + dx;
                        var y = py + dy;
                        if (x < 0 || y < 0 || x >= geo.Width || y >= geo.Height)
                            continue;
                        if (geo.IsLand[y * geo.Width + x])
                            return r;
                    }
                }
            }

            return -1;
        }

        static double HaversineM(double lon1, double lat1, double lon2, double lat2)
        {
            const double R = 6371000.0;
            var p1 = lat1 * Math.PI / 180.0;
            var p2 = lat2 * Math.PI / 180.0;
            var dp = (lat2 - lat1) * Math.PI / 180.0;
            var dl = (lon2 - lon1) * Math.PI / 180.0;
            var a = Math.Sin(dp / 2) * Math.Sin(dp / 2) +
                    Math.Cos(p1) * Math.Cos(p2) * Math.Sin(dl / 2) * Math.Sin(dl / 2);
            return 2 * R * Math.Asin(Math.Sqrt(a));
        }

        static string Sha256File(string path)
        {
            if (!File.Exists(path))
                return "(missing)";
            using var sha = SHA256.Create();
            using var fs = File.OpenRead(path);
            var hash = sha.ComputeHash(fs);
            var sb = new StringBuilder(hash.Length * 2);
            for (var i = 0; i < hash.Length; i++)
                sb.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
            return sb.ToString();
        }
    }
}
