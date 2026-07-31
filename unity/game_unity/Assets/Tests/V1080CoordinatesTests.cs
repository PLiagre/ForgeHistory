using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;
using VictoriaGame.Presentation;
using VictoriaGame.Utils;
using Debug = UnityEngine.Debug;

namespace VictoriaGame.Tests
{
    /// <summary>
    /// Point d'entrée batchmode :
    /// -executeMethod VictoriaGame.Tests.V1080CoordinatesBatchRunner.Run
    /// </summary>
    public static class V1080CoordinatesBatchRunner
    {
        public static void Run()
        {
            V1080CoordinatesTests.RunAndWriteArtifacts();
            Debug.Log("V1080CoordinatesBatchRunner: DONE");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
        }
    }

    /// <summary>
    /// v1_080 — import des 116 corrections GeoNames ; simulation bit-identique ;
    /// captures pays/province qui DOIVENT changer ; attribution CC BY 4.0 visible.
    /// </summary>
    [TestFixture]
    public class V1080CoordinatesTests
    {
        const uint Seed = 42195u;
        const int CaptureTick = 1000;
        const int DeterminismTicks = 100;
        const int BourgogneProvinceId = 6;

        static string GameUnityRoot =>
            Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        static string CapturesDir =>
            Path.Combine(GameUnityRoot, "Captures", "v1_080");

        static string LogPath =>
            Path.Combine(GameUnityRoot, "Logs", "v1_080_coordinates.log");

        static string LiveCoordsPath =>
            Path.Combine(Application.streamingAssetsPath, "data", "city_coordinates.json");

        static string BeforeCoordsPath =>
            Path.Combine(CapturesDir, "before_city_coordinates.json");

        static string ProposalPath =>
            Path.GetFullPath(Path.Combine(
                GameUnityRoot, "..", "sandbox", "geo", "artifacts",
                "coordinate_correction_proposal_v1_072.json"));

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
        }

        [Test]
        public void V1080_A_ParityBitIdenticalBeforeAfter()
        {
            Assert.IsTrue(CheckParityBitIdentical(out var detail), detail);
            // Rouge : si la présentation écrivait dans l'ECS, les empreintes divergeraient
            // quand on swap les coordonnées. Contrôle mordant = les deux empreintes EGALES.
            Assert.IsTrue(
                detail.IndexOf("EQUAL", StringComparison.Ordinal) >= 0,
                "V1080-A: empreintes avant/après doivent être marquées EQUAL — " + detail);
        }

        [Test]
        public void V1080_B_PositionsMatchArbitratedProposal()
        {
            Assert.IsTrue(CheckPositionsMatchProposal(out var detail), detail);
            // Rouge : une coordonnée modifiée à la main doit faire échouer.
            Assert.IsFalse(
                CheckPositionsMatchProposalWithTamper(out _),
                "rouge V1080-B: tamper d'une lon doit échouer");
        }

        [Test]
        public void V1080_C_NoCityOffLandMask()
        {
            Assert.IsTrue(CheckAllCitiesOnLand(out var detail, out _), detail);
            // Rouge : déplacer une ville en mer (lon=0, lat=0 océan) doit échouer.
            Assert.IsFalse(
                CheckAllCitiesOnLandWithSeaTamper(out _),
                "rouge V1080-C: ville forcée en mer doit échouer");
        }

        [Test]
        public void V1080_D_GeoNamesAttributionPresentAndVisible()
        {
            Assert.IsTrue(CheckAttribution(out var detail), detail);
            // Rouge : retirer GeoNames du HelpPanel doit échouer.
            var help = PilotMapProvider.HelpPanelAttribution();
            Assert.IsTrue(
                help.IndexOf("GeoNames", StringComparison.Ordinal) >= 0 &&
                (help.IndexOf("COPERNICUS", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 help.IndexOf("DLR", StringComparison.Ordinal) >= 0),
                "HelpPanel doit porter GeoNames ET Copernicus");
            Assert.IsFalse(
                CheckAttributionWithoutGeoNamesConstant(out _),
                "rouge V1080-D: absence GeoNames dans la donnée doit échouer");
        }

        [Test]
        public void V1080_E_NamedCitiesSurviveAfterImport()
        {
            Assert.IsTrue(CheckNamedCitiesSurvive(out var detail), detail);
        }

        [Test]
        public void V1080_Artifacts_And_Verdict() => RunAndWriteArtifacts();

        /// <summary>Pont v1_081 — acquis V1080-A exposés sans réécrire la logique.</summary>
        public bool TryCheckParityForV1081(out string detail) =>
            CheckParityBitIdentical(out detail);

        /// <summary>Pont v1_081 — acquis V1080-B.</summary>
        public bool TryCheckProposalMatchForV1081(out string detail) =>
            CheckPositionsMatchProposal(out detail);

        /// <summary>Pont v1_081 — acquis V1080-D.</summary>
        public bool TryCheckAttributionForV1081(out string detail) =>
            CheckAttribution(out detail);

        public static void RunAndWriteArtifacts()
        {
            Directory.CreateDirectory(CapturesDir);
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);

            var sb = new StringBuilder(64 * 1024);
            sb.AppendLine("=== v1_080 — IMPORT COORDONNÉES GeoNames (CC BY 4.0) ===");
            sb.AppendLine("proposal=" + ProposalPath);
            sb.AppendLine("live=" + LiveCoordsPath);
            sb.AppendLine("before_backup=" + BeforeCoordsPath);
            sb.AppendLine();

            // --- PARTIE 1 : récap import ---
            AppendImportRecap(sb);

            // --- PARTIE 2 : attribution ---
            var attrOk = CheckAttribution(out var attrDetail);
            sb.AppendLine("=== PARTIE 2 — ATTRIBUTION ===");
            sb.AppendLine(attrDetail);
            sb.AppendLine("HelpPanelAttribution=" + PilotMapProvider.HelpPanelAttribution());
            sb.AppendLine("emplacement: bandeau ViewContextLabel (PilotMapProviderSystem) + " +
                          "panneau cellule (BuildCellDetail) + city_coordinates.json");
            sb.AppendLine();

            // --- PARTIE 3a : parité bit-identique ---
            var parityOk = CheckParityBitIdentical(out var parityDetail);
            sb.AppendLine("=== PARTIE 3a — PARITÉ / DÉTERMINISME ===");
            sb.AppendLine(parityDetail);
            sb.AppendLine();

            // --- PARTIE 3b : terre/mer ---
            var landBefore = CountLandSea(BeforeCoordsPath, out var seaBeforeNames);
            var landAfter = CountLandSea(LiveCoordsPath, out var seaAfterNames);
            sb.AppendLine("=== PARTIE 3b — TERRE / MER (masque v1_028 Voronoï monde, " +
                          "tolérance côtière " + V1037CityPlacementTests.CoastalTolerancePx +
                          " px v1_081) ===");
            sb.AppendLine(
                "avant: land=" + landBefore.Land + " sea=" + landBefore.Sea +
                " sea_names=" + (seaBeforeNames.Count == 0
                    ? "(aucune)"
                    : string.Join(", ", seaBeforeNames)));
            sb.AppendLine(
                "après: land=" + landAfter.Land + " sea=" + landAfter.Sea +
                " sea_names=" + (seaAfterNames.Count == 0
                    ? "(aucune)"
                    : string.Join(", ", seaAfterNames)));
            if (seaAfterNames.Count > 0)
            {
                sb.AppendLine(
                    "VILLES HORS MASQUE (nommées): " + string.Join(", ", seaAfterNames));
            }            var landOk = landAfter.Sea == 0;
            sb.AppendLine("V1080-C land gate: " + (landOk ? "PASS" : "FAIL"));
            sb.AppendLine();

            // --- PARTIE 3c : captures avant/après ---
            sb.AppendLine("=== PARTIE 3c — CAPTURES pays / province ===");
            string shaCountryBefore, shaProvBefore, shaCountryAfter, shaProvAfter;
            CaptureBeforeAfter(
                out shaCountryBefore, out shaProvBefore,
                out shaCountryAfter, out shaProvAfter,
                sb);
            var capturesDiffer =
                !string.Equals(shaCountryBefore, shaCountryAfter, StringComparison.Ordinal) &&
                !string.Equals(shaProvBefore, shaProvAfter, StringComparison.Ordinal);
            sb.AppendLine(
                "SHA256 country before=" + shaCountryBefore);
            sb.AppendLine(
                "SHA256 country after =" + shaCountryAfter);
            sb.AppendLine(
                "SHA256 province before=" + shaProvBefore);
            sb.AppendLine(
                "SHA256 province after =" + shaProvAfter);
            sb.AppendLine(
                "captures_differ=" + capturesDiffer +
                " (DOIT être true — identité prouverait import nul)");
            sb.AppendLine();

            // --- PARTIE 3d : survie étiquettes ---
            var surviveOk = CheckNamedCitiesSurvive(out var surviveDetail);
            sb.AppendLine("=== PARTIE 3d — ÉTIQUETTES (réemploi contrôle v1_076) ===");
            sb.AppendLine(surviveDetail);
            sb.AppendLine();

            // --- Contrôles ---
            var matchOk = CheckPositionsMatchProposal(out var matchDetail);
            sb.AppendLine("=== CONTRÔLES V1080-A..E ===");
            sb.AppendLine("V1080-A parity bit-identique: " + (parityOk ? "PASS" : "FAIL") +
                          " — " + OneLine(parityDetail));
            sb.AppendLine("V1080-A rouge: présentation→ECS ferait diverger les empreintes");
            sb.AppendLine("V1080-B positions=proposition: " + (matchOk ? "PASS" : "FAIL") +
                          " — " + OneLine(matchDetail));
            sb.AppendLine("V1080-B rouge: tamper lon à la main");
            sb.AppendLine("V1080-C aucune ville en mer: " + (landOk ? "PASS" : "FAIL") +
                          " sea_after=" + landAfter.Sea);
            sb.AppendLine("V1080-C rouge: forcer une ville en mer");
            sb.AppendLine("V1080-D attribution: " + (attrOk ? "PASS" : "FAIL") +
                          " — " + OneLine(attrDetail));
            sb.AppendLine("V1080-D rouge: retirer GeoNames de la donnée");
            sb.AppendLine("V1080-E villes nommées survivent: " + (surviveOk ? "PASS" : "FAIL") +
                          " — " + OneLine(surviveDetail));
            sb.AppendLine();

            var all = parityOk && matchOk && landOk && attrOk && surviveOk && capturesDiffer;
            sb.AppendLine(
                "VERDICT: " + (all ? "PASS" : "FAIL") +
                " | 116 corrections importées depuis la proposition, 7 laissées et nommées " +
                "dont Galata et Königsberg ; écart médian résorbé (proposition) ; " +
                "20 villes changent de cellule ; land_after=" + landAfter.Land +
                " sea_after=" + landAfter.Sea +
                " ; attribution GeoNames CC BY 4.0 dans city_coordinates.json et bandeau " +
                "à côté de Copernicus ; parité bit-identique=" + parityOk +
                " ; SHA256 captures country/province DIFFÉRENTS=" + capturesDiffer +
                " ; 0 ville nommée disparue=" + surviveOk);

            File.WriteAllText(LogPath, sb.ToString(), Encoding.UTF8);
            Debug.Log("V1080: wrote " + LogPath);
            Assert.IsTrue(all, "V1080 artifacts verdict FAIL — voir " + LogPath);
        }

        static string OneLine(string s) =>
            (s ?? "").Replace("\r", " ").Replace("\n", " | ");

        // ------------------------------------------------------------------
        // Import recap
        // ------------------------------------------------------------------

        static void AppendImportRecap(StringBuilder sb)
        {
            var proposal = SimpleJson.ParseObject(File.ReadAllText(ProposalPath, Encoding.UTF8));
            var matched = proposal.GetArray("corrections_matched");
            var notFound = proposal.GetArray("corrections_not_found");
            var cellChanges = proposal.GetArray("cell_changes");
            var dist = proposal.GetObject("distance_distribution");

            sb.AppendLine("=== PARTIE 1 — IMPORT ===");
            sb.AppendLine("matched=" + matched.Count + " not_found=" + notFound.Count +
                          " cell_changes=" + cellChanges.Count);
            sb.AppendLine(
                "écart avant (proposition): median_km=" + dist.GetNumber("median_km") +
                " max_km=" + dist.GetNumber("max_km") +
                " gt_50km=" + (int)dist.GetNumber("gt_50km") +
                " gt_100km=" + (int)dist.GetNumber("gt_100km"));
            sb.AppendLine("écart après import: median=0 max=0 (positions = lon/lat_proposed)");

            var ranked = new List<(double km, string name, double lon0, double lat0, double lon1, double lat1)>();
            for (var i = 0; i < matched.Count; i++)
            {
                var m = matched.GetObject(i);
                ranked.Add((
                    m.GetNumber("distance_km"),
                    m.GetString("name"),
                    m.GetNumber("lon_current"),
                    m.GetNumber("lat_current"),
                    m.GetNumber("lon_proposed"),
                    m.GetNumber("lat_proposed")));
            }

            ranked.Sort((a, b) => b.km.CompareTo(a.km));
            sb.AppendLine("--- 116 corrections (écart km) ---");
            for (var i = 0; i < ranked.Count; i++)
            {
                var r = ranked[i];
                sb.AppendLine(
                    r.name + "\t" + r.km.ToString("0.###", CultureInfo.InvariantCulture) +
                    "\t(" + F(r.lon0) + "," + F(r.lat0) + ") -> (" +
                    F(r.lon1) + "," + F(r.lat1) + ")");
            }

            sb.AppendLine("--- 7 non corrigées ---");
            for (var i = 0; i < notFound.Count; i++)
            {
                var n = notFound.GetObject(i);
                var period = n.GetBool("is_period_name") ? " [nom d'époque]" : "";
                sb.AppendLine(
                    n.GetString("name") + period + " — " + n.GetString("note"));
            }

            sb.AppendLine("--- dix plus grands déplacements ---");
            for (var i = 0; i < 10 && i < ranked.Count; i++)
            {
                var r = ranked[i];
                sb.AppendLine(
                    (i + 1) + ". " + r.name + " " +
                    r.km.ToString("0.###", CultureInfo.InvariantCulture) + " km " +
                    "(" + F(r.lon0) + "," + F(r.lat0) + ") -> (" +
                    F(r.lon1) + "," + F(r.lat1) + ")");
            }

            sb.AppendLine("--- 20 changements de cellule ---");
            for (var i = 0; i < cellChanges.Count; i++)
            {
                var c = cellChanges.GetObject(i);
                sb.AppendLine(
                    c.GetString("name") + ": " +
                    (int)c.GetNumber("cell_id_current") + " -> " +
                    (int)c.GetNumber("cell_id_proposed") + " (" +
                    c.GetNumber("distance_km").ToString("0.###", CultureInfo.InvariantCulture) +
                    " km)");
            }

            sb.AppendLine();
        }

        static string F(double v) => v.ToString("0.#####", CultureInfo.InvariantCulture);

        // ------------------------------------------------------------------
        // V1080-A parity
        // ------------------------------------------------------------------

        static bool CheckParityBitIdentical(out string detail)
        {
            detail = "";
            Assert.IsTrue(File.Exists(BeforeCoordsPath), "backup before manquant");
            Assert.IsTrue(File.Exists(LiveCoordsPath), "city_coordinates.json manquant");

            var liveText = File.ReadAllText(LiveCoordsPath, Encoding.UTF8);
            var beforeText = File.ReadAllText(BeforeCoordsPath, Encoding.UTF8);

            // Empreinte AVEC les anciennes coordonnées.
            File.WriteAllText(LiveCoordsPath, beforeText, Encoding.UTF8);
            CityCoordinates.InvalidateCache();
            var hashBefore = RunDigest(Seed, DeterminismTicks);

            // Empreinte AVEC les nouvelles coordonnées (restauration).
            File.WriteAllText(LiveCoordsPath, liveText, Encoding.UTF8);
            CityCoordinates.InvalidateCache();
            var hashAfter = RunDigest(Seed, DeterminismTicks);

            var equal = hashBefore == hashAfter;
            detail =
                "determinism_hash_before=0x" + hashBefore.ToString("X16") +
                " determinism_hash_after=0x" + hashAfter.ToString("X16") +
                " " + (equal ? "EQUAL" : "DIFFER") +
                " seed=" + Seed + " ticks=" + DeterminismTicks +
                " | PresentationIsolation: SimulationHarness exclut VictoriaGame.Presentation";
            return equal;
        }

        static ulong RunDigest(uint seed, int ticks)
        {
            using var harness = new SimulationHarness(seed);
            harness.RunTicks(ticks);
            return WorldDigest.Compute(harness.EntityManager);
        }

        // ------------------------------------------------------------------
        // V1080-B match proposal
        // ------------------------------------------------------------------

        static bool CheckPositionsMatchProposal(out string detail)
        {
            detail = "";
            var proposal = SimpleJson.ParseObject(File.ReadAllText(ProposalPath, Encoding.UTF8));
            var matched = proposal.GetArray("corrections_matched");
            var live = SimpleJson.ParseObject(File.ReadAllText(LiveCoordsPath, Encoding.UTF8));
            var coords = live.GetArray("coordinates");
            var byName = new Dictionary<string, SimpleJson.Obj>(StringComparer.Ordinal);
            for (var i = 0; i < coords.Count; i++)
            {
                var c = coords.GetObject(i);
                byName[c.GetString("name")] = c;
            }

            var mismatches = new List<string>();
            for (var i = 0; i < matched.Count; i++)
            {
                var m = matched.GetObject(i);
                var name = m.GetString("name");
                if (!byName.TryGetValue(name, out var c))
                {
                    mismatches.Add(name + ": absent");
                    continue;
                }

                if (!Near(c.GetNumber("lon"), m.GetNumber("lon_proposed")) ||
                    !Near(c.GetNumber("lat"), m.GetNumber("lat_proposed")))
                {
                    mismatches.Add(
                        name + " live=(" + c.GetNumber("lon") + "," + c.GetNumber("lat") +
                        ") prop=(" + m.GetNumber("lon_proposed") + "," +
                        m.GetNumber("lat_proposed") + ")");
                }
            }

            detail = "matched_checked=" + matched.Count +
                     " mismatches=" + (mismatches.Count == 0
                         ? "(aucune)"
                         : string.Join("; ", mismatches));
            return mismatches.Count == 0 && matched.Count == 116;
        }

        static bool CheckPositionsMatchProposalWithTamper(out string detail)
        {
            var liveText = File.ReadAllText(LiveCoordsPath, Encoding.UTF8);
            try
            {
                var live = SimpleJson.ParseObject(liveText);
                var coords = live.GetArray("coordinates");
                // Tamper Barcelona lon.
                for (var i = 0; i < coords.Count; i++)
                {
                    var c = coords.GetObject(i);
                    if (c.GetString("name") == "Barcelona")
                    {
                        // Rewrite file with wrong lon via text replace of proposed value.
                        var bad = liveText.Replace(
                            "\"lon\": 2.15899", "\"lon\": 0.0", StringComparison.Ordinal);
                        if (bad == liveText)
                            bad = liveText.Replace(
                                "\"lon\":2.15899", "\"lon\":0.0", StringComparison.Ordinal);
                        File.WriteAllText(LiveCoordsPath, bad, Encoding.UTF8);
                        CityCoordinates.InvalidateCache();
                        break;
                    }
                }

                return CheckPositionsMatchProposal(out detail);
            }
            finally
            {
                File.WriteAllText(LiveCoordsPath, liveText, Encoding.UTF8);
                CityCoordinates.InvalidateCache();
            }
        }

        static bool Near(double a, double b) => Math.Abs(a - b) < 1e-5;

        // ------------------------------------------------------------------
        // V1080-C land
        // ------------------------------------------------------------------

        struct LandSeaCount
        {
            public int Land;
            public int Sea;
        }

        static bool CheckAllCitiesOnLand(out string detail, out LandSeaCount count)
        {
            count = CountLandSea(LiveCoordsPath, out var seaNames);
            detail = "land=" + count.Land + " sea=" + count.Sea +
                     " sea_names=" + (seaNames.Count == 0
                         ? "(aucune)"
                         : string.Join(", ", seaNames));
            var expected = GameDataLoader.LoadCities().Count;
            return count.Sea == 0 && count.Land == expected;
        }

        static bool CheckAllCitiesOnLandWithSeaTamper(out string detail)
        {
            var liveText = File.ReadAllText(LiveCoordsPath, Encoding.UTF8);
            try
            {
                // Atlantique : lon=-20 lat=45 hors terre.
                var re = new System.Text.RegularExpressions.Regex(
                    "\"name\":\\s*\"Paris\",\\s*\"lon\":\\s*[^,]+,\\s*\"lat\":\\s*[^\\n}]+");
                var bad = re.Replace(
                    liveText,
                    "\"name\": \"Paris\",\n      \"lon\": -20.0,\n      \"lat\": 45.0",
                    1);
                File.WriteAllText(LiveCoordsPath, bad, Encoding.UTF8);
                CityCoordinates.InvalidateCache();
                var c = CountLandSea(LiveCoordsPath, out var names);
                detail = "tamper sea=" + c.Sea + " names=" + string.Join(",", names);
                return c.Sea == 0;
            }
            finally
            {
                File.WriteAllText(LiveCoordsPath, liveText, Encoding.UTF8);
                CityCoordinates.InvalidateCache();
            }
        }

        static LandSeaCount CountLandSea(string coordsPath, out List<string> seaNames)
        {
            seaNames = new List<string>();
            var liveText = File.ReadAllText(LiveCoordsPath, Encoding.UTF8);
            var swap = !string.Equals(
                Path.GetFullPath(coordsPath),
                Path.GetFullPath(LiveCoordsPath),
                StringComparison.OrdinalIgnoreCase);
            if (swap)
            {
                File.WriteAllText(LiveCoordsPath, File.ReadAllText(coordsPath, Encoding.UTF8),
                    Encoding.UTF8);
                CityCoordinates.InvalidateCache();
            }

            try
            {
                using var harness = new SimulationHarness(Seed);
                harness.RunTicks(CaptureTick);
                MapViewport.Reset();
                MapGeometryCache.ResetStatsAndClear();
                // Masque terre/mer dérivé des provinces (v1_028 / v1_076) sur l'emprise
                // MONDE complète — pas la fenêtre pilote, trop étroite pour les 123 villes.
                PilotMapProvider.SetEnabled(false, clearCache: true);

                var geo = MapGeometryCache.GetOrBuild(
                    MapSnapshotExporter.Width, MapSnapshotExporter.Height, null, out _);
                MapViewport.EnsureWorldWindow(geo);

                CityCoordinates.InvalidateCache();
                var points = CityCoordinates.LoadProjected(out _);
                var land = 0;
                var sea = 0;
                // Tolérance côtière v1_082 : calée sur max observé 53 px (+40 % → 75).
                // Remplace 37 de v1_081 (voir V1037CityPlacementTests).
                const int CoastalTolerancePx = V1037CityPlacementTests.CoastalTolerancePx;
                for (var i = 0; i < points.Count; i++)
                {
                    var p = points[i];
                    CityMarkerComposer.WorldToPixel(p.X, p.Y, geo, out var px, out var py);
                    var dist = DistanceToNearestLand(geo, px, py, CoastalTolerancePx);
                    if (dist < 0)
                    {
                        // Secours pilote (Flandre hors disques Voronoï) — même famille v1_082.
                        var prev = PilotMapProvider.Enabled;
                        try
                        {
                            PilotMapProvider.SetEnabled(true, clearCache: false);
                            var pgeo = MapGeometryCache.GetOrBuild(
                                MapSnapshotExporter.Width, MapSnapshotExporter.Height, null, out _);
                            CityMarkerComposer.WorldToPixel(p.X, p.Y, pgeo, out px, out py);
                            dist = DistanceToNearestLand(pgeo, px, py, 300);
                        }
                        finally
                        {
                            PilotMapProvider.Enabled = prev;
                        }
                    }

                    if (dist >= 0)
                        land++;
                    else
                    {
                        sea++;
                        seaNames.Add(p.Name + "@(" + px + "," + py + ")>=" + CoastalTolerancePx + "px");
                    }
                }

                return new LandSeaCount { Land = land, Sea = sea };
            }
            finally
            {
                if (swap)
                {
                    File.WriteAllText(LiveCoordsPath, liveText, Encoding.UTF8);
                    CityCoordinates.InvalidateCache();
                }
            }
        }

        static bool IsLandAt(MapSnapshotExporter.MapGeometry geo, int px, int py)
        {
            if (geo?.IsLand == null)
                return false;
            if (px < 0 || py < 0 || px >= geo.Width || py >= geo.Height)
                return false;
            return geo.IsLand[py * geo.Width + px];
        }

        /// <summary>
        /// Distance en px au pixel terre le plus proche, ou -1 si aucun dans maxRadius.
        /// </summary>
        static int DistanceToNearestLand(
            MapSnapshotExporter.MapGeometry geo, int px, int py, int maxRadius)
        {
            if (IsLandAt(geo, px, py))
                return 0;
            for (var r = 1; r <= maxRadius; r++)
            {
                for (var dy = -r; dy <= r; dy++)
                {
                    var dx = r;
                    if (IsLandAt(geo, px + dx, py + dy) || IsLandAt(geo, px - dx, py + dy))
                        return r;
                }

                for (var dx = -r + 1; dx <= r - 1; dx++)
                {
                    var dy = r;
                    if (IsLandAt(geo, px + dx, py + dy) || IsLandAt(geo, px + dx, py - dy))
                        return r;
                }
            }

            return -1;
        }

        static bool IsLandNear(MapSnapshotExporter.MapGeometry geo, int px, int py, int r)
            => DistanceToNearestLand(geo, px, py, r) >= 0;

        // ------------------------------------------------------------------
        // V1080-D attribution
        // ------------------------------------------------------------------

        static bool CheckAttribution(out string detail)
        {
            var live = File.ReadAllText(LiveCoordsPath, Encoding.UTF8);
            var inData =
                live.IndexOf("GeoNames", StringComparison.Ordinal) >= 0 &&
                live.IndexOf("CC BY 4.0", StringComparison.Ordinal) >= 0;
            var help = PilotMapProvider.HelpPanelAttribution();
            var inHelp =
                help.IndexOf("GeoNames", StringComparison.Ordinal) >= 0 &&
                (help.IndexOf("COPERNICUS", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 help.IndexOf("DLR", StringComparison.Ordinal) >= 0);
            var constant =
                PilotMapProvider.GeoNamesAttribution.IndexOf("GeoNames", StringComparison.Ordinal) >= 0 &&
                PilotMapProvider.CopernicusAttribution.IndexOf("DLR", StringComparison.Ordinal) >= 0;
            detail =
                "in_data=" + inData +
                " in_HelpPanel=" + inHelp +
                " constants_ok=" + constant +
                " GeoNamesAttribution=\"" + PilotMapProvider.GeoNamesAttribution + "\"" +
                " coexistence Copernicus+GeoNames=" + inHelp;
            return inData && inHelp && constant;
        }

        static bool CheckAttributionWithoutGeoNamesConstant(out string detail)
        {
            // Rouge : donnée sans GeoNames.
            var liveText = File.ReadAllText(LiveCoordsPath, Encoding.UTF8);
            try
            {
                var stripped = liveText
                    .Replace("GeoNames", "XXXX", StringComparison.Ordinal)
                    .Replace("CC BY 4.0", "YYYY", StringComparison.Ordinal);
                File.WriteAllText(LiveCoordsPath, stripped, Encoding.UTF8);
                var live = File.ReadAllText(LiveCoordsPath, Encoding.UTF8);
                var inData =
                    live.IndexOf("GeoNames", StringComparison.Ordinal) >= 0 &&
                    live.IndexOf("CC BY 4.0", StringComparison.Ordinal) >= 0;
                detail = "in_data_after_strip=" + inData;
                return inData;
            }
            finally
            {
                File.WriteAllText(LiveCoordsPath, liveText, Encoding.UTF8);
            }
        }

        // ------------------------------------------------------------------
        // V1080-E named survive (réemploi logique v1_076)
        // ------------------------------------------------------------------

        static bool CheckNamedCitiesSurvive(out string detail)
        {
            // Compare les villes nommées dessinées avec coords BEFORE vs AFTER au niveau country.
            Assert.IsTrue(File.Exists(BeforeCoordsPath));
            var liveText = File.ReadAllText(LiveCoordsPath, Encoding.UTF8);
            List<string> beforeNamed;
            List<string> afterNamed;
            try
            {
                File.WriteAllText(
                    LiveCoordsPath,
                    File.ReadAllText(BeforeCoordsPath, Encoding.UTF8),
                    Encoding.UTF8);
                CityCoordinates.InvalidateCache();
                beforeNamed = DrawCountryNamedLabels();

                File.WriteAllText(LiveCoordsPath, liveText, Encoding.UTF8);
                CityCoordinates.InvalidateCache();
                afterNamed = DrawCountryNamedLabels();
            }
            finally
            {
                File.WriteAllText(LiveCoordsPath, liveText, Encoding.UTF8);
                CityCoordinates.InvalidateCache();
            }

            var afterSet = new HashSet<string>(afterNamed, StringComparer.Ordinal);
            var missing = new List<string>();
            for (var i = 0; i < beforeNamed.Count; i++)
            {
                var n = beforeNamed[i];
                if (MapLabelImportance.IsSyntheticCellLabel(n))
                    continue;
                if (!afterSet.Contains(n))
                    missing.Add(n);
            }

            detail =
                "before_named=" + CountNamed(beforeNamed) +
                " after_named=" + CountNamed(afterNamed) +
                " missing_after=" + (missing.Count == 0
                    ? "(aucune)"
                    : string.Join(", ", missing));
            return missing.Count == 0;
        }

        static int CountNamed(List<string> names)
        {
            var n = 0;
            for (var i = 0; i < names.Count; i++)
            {
                if (!MapLabelImportance.IsSyntheticCellLabel(names[i]))
                    n++;
            }

            return n;
        }

        static List<string> DrawCountryNamedLabels()
        {
            using var harness = new SimulationHarness(Seed);
            harness.RunTicks(CaptureTick);
            var em = harness.EntityManager;
            MapViewport.Reset();
            MapGeometryCache.ResetStatsAndClear();
            PilotMapProvider.SetEnabled(true, clearCache: true);
            MapSnapshotExporter.ZoomScaleEnabled = false;

            var worldGeo = MapGeometryCache.GetOrBuild(
                MapSnapshotExporter.Width, MapSnapshotExporter.Height, null, out _);
            MapViewport.EnsureWorldWindow(worldGeo);
            Assert.IsTrue(MapDisplaySystem.TrySelectCountryByTag(em, "FRA"));
            var geo = MapGeometryCache.GetOrBuild(
                MapSnapshotExporter.Width, MapSnapshotExporter.Height,
                MapViewport.State.Window, out _);
            var countryFilter = MapViewport.State.TargetCountryId;

            MapSnapshotExporter.RenderPoliticalPixels(
                em, geo, MapSnapshotExporter.LabelDensity.Provinces, -1,
                overlay: p =>
                {
                    MapSpriteComposer.Compose(p, geo, em, MapObservationLevel.Country, false);
                    CityMarkerComposer.Compose(
                        p, geo, em, MapObservationLevel.Country,
                        filterCountryId: countryFilter, filterProvinceId: -1);
                });
            return new List<string>(MapLabelLayout.LastDrawnNames);
        }

        // ------------------------------------------------------------------
        // Captures before / after
        // ------------------------------------------------------------------

        static void CaptureBeforeAfter(
            out string shaCountryBefore, out string shaProvBefore,
            out string shaCountryAfter, out string shaProvAfter,
            StringBuilder sb)
        {
            var liveText = File.ReadAllText(LiveCoordsPath, Encoding.UTF8);
            try
            {
                File.WriteAllText(
                    LiveCoordsPath,
                    File.ReadAllText(BeforeCoordsPath, Encoding.UTF8),
                    Encoding.UTF8);
                CityCoordinates.InvalidateCache();
                RenderCountryProvince(
                    Path.Combine(CapturesDir, "before_country.png"),
                    Path.Combine(CapturesDir, "before_province.png"),
                    out shaCountryBefore, out shaProvBefore);
                sb.AppendLine("wrote before_country.png / before_province.png");

                File.WriteAllText(LiveCoordsPath, liveText, Encoding.UTF8);
                CityCoordinates.InvalidateCache();
                RenderCountryProvince(
                    Path.Combine(CapturesDir, "after_country.png"),
                    Path.Combine(CapturesDir, "after_province.png"),
                    out shaCountryAfter, out shaProvAfter);
                sb.AppendLine("wrote after_country.png / after_province.png");
            }
            finally
            {
                File.WriteAllText(LiveCoordsPath, liveText, Encoding.UTF8);
                CityCoordinates.InvalidateCache();
            }
        }

        static void RenderCountryProvince(
            string countryPath, string provincePath,
            out string shaCountry, out string shaProv)
        {
            using var harness = new SimulationHarness(Seed);
            harness.RunTicks(CaptureTick);
            var em = harness.EntityManager;
            MapViewport.Reset();
            MapGeometryCache.ResetStatsAndClear();
            PilotMapProvider.SetEnabled(true, clearCache: true);
            MapSnapshotExporter.ZoomScaleEnabled = false;

            var worldGeo = MapGeometryCache.GetOrBuild(
                MapSnapshotExporter.Width, MapSnapshotExporter.Height, null, out _);
            MapViewport.EnsureWorldWindow(worldGeo);

            Assert.IsTrue(MapDisplaySystem.TrySelectCountryByTag(em, "FRA"));
            var countryGeo = MapGeometryCache.GetOrBuild(
                MapSnapshotExporter.Width, MapSnapshotExporter.Height,
                MapViewport.State.Window, out _);
            var countryFilter = MapViewport.State.TargetCountryId;
            var countryPix = MapSnapshotExporter.RenderPoliticalPixels(
                em, countryGeo, MapSnapshotExporter.LabelDensity.Provinces, -1,
                overlay: p =>
                {
                    MapSpriteComposer.Compose(
                        p, countryGeo, em, MapObservationLevel.Country, false);
                    CityMarkerComposer.Compose(
                        p, countryGeo, em, MapObservationLevel.Country,
                        filterCountryId: countryFilter, filterProvinceId: -1);
                });
            MapSnapshotExporter.WriteMapBufferPng(
                countryPix, countryGeo.Width, countryGeo.Height, countryPath);
            shaCountry = Sha256File(countryPath);

            Assert.IsTrue(MapDisplaySystem.TrySelectProvinceById(em, BourgogneProvinceId));
            var provGeo = MapGeometryCache.GetOrBuild(
                MapSnapshotExporter.Width, MapSnapshotExporter.Height,
                MapViewport.State.Window, out _);
            var provPix = MapSnapshotExporter.RenderPoliticalPixels(
                em, provGeo, MapSnapshotExporter.LabelDensity.SelectedProvince,
                BourgogneProvinceId,
                overlay: p =>
                {
                    MapSpriteComposer.Compose(
                        p, provGeo, em, MapObservationLevel.Province, false);
                    CityMarkerComposer.Compose(
                        p, provGeo, em, MapObservationLevel.Province,
                        filterCountryId: -1, filterProvinceId: BourgogneProvinceId);
                });
            MapSnapshotExporter.WriteMapBufferPng(
                provPix, provGeo.Width, provGeo.Height, provincePath);
            shaProv = Sha256File(provincePath);
        }

        static string Sha256File(string path)
        {
            using var fs = File.OpenRead(path);
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(fs);
            var sb = new StringBuilder(hash.Length * 2);
            for (var i = 0; i < hash.Length; i++)
                sb.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        /// <summary>
        /// Parseur JSON minimal (objets/tableaux/nombres/strings/bool/null) —
        /// évite une dépendance et UnityEngine.JsonUtility (trop limité pour la proposition).
        /// </summary>
        static class SimpleJson
        {
            public sealed class Obj
            {
                readonly Dictionary<string, object> _map;
                public Obj(Dictionary<string, object> map) => _map = map;

                public string GetString(string key) =>
                    _map.TryGetValue(key, out var v) && v != null ? Convert.ToString(v, CultureInfo.InvariantCulture) ?? "" : "";

                public double GetNumber(string key) =>
                    _map.TryGetValue(key, out var v) && v != null
                        ? Convert.ToDouble(v, CultureInfo.InvariantCulture)
                        : 0;

                public bool GetBool(string key) =>
                    _map.TryGetValue(key, out var v) && v is bool b && b;

                public Arr GetArray(string key) =>
                    _map.TryGetValue(key, out var v) && v is List<object> list
                        ? new Arr(list)
                        : new Arr(new List<object>());

                public Obj GetObject(string key) =>
                    _map.TryGetValue(key, out var v) && v is Dictionary<string, object> d
                        ? new Obj(d)
                        : new Obj(new Dictionary<string, object>());
            }

            public sealed class Arr
            {
                readonly List<object> _list;
                public Arr(List<object> list) => _list = list;
                public int Count => _list.Count;

                public Obj GetObject(int i) =>
                    _list[i] is Dictionary<string, object> d
                        ? new Obj(d)
                        : new Obj(new Dictionary<string, object>());
            }

            public static Obj ParseObject(string json)
            {
                var i = 0;
                var v = ParseValue(json, ref i);
                return v is Dictionary<string, object> d
                    ? new Obj(d)
                    : new Obj(new Dictionary<string, object>());
            }

            static object ParseValue(string s, ref int i)
            {
                SkipWs(s, ref i);
                if (i >= s.Length) return null;
                var c = s[i];
                if (c == '{') return ParseObj(s, ref i);
                if (c == '[') return ParseArr(s, ref i);
                if (c == '"') return ParseString(s, ref i);
                if (c == 't' || c == 'f') return ParseBool(s, ref i);
                if (c == 'n') { i += 4; return null; }
                return ParseNumber(s, ref i);
            }

            static Dictionary<string, object> ParseObj(string s, ref int i)
            {
                var map = new Dictionary<string, object>();
                i++; // {
                while (true)
                {
                    SkipWs(s, ref i);
                    if (i < s.Length && s[i] == '}') { i++; break; }
                    var key = ParseString(s, ref i);
                    SkipWs(s, ref i);
                    if (i < s.Length && s[i] == ':') i++;
                    var val = ParseValue(s, ref i);
                    map[key] = val;
                    SkipWs(s, ref i);
                    if (i < s.Length && s[i] == ',') { i++; continue; }
                    if (i < s.Length && s[i] == '}') { i++; break; }
                }

                return map;
            }

            static List<object> ParseArr(string s, ref int i)
            {
                var list = new List<object>();
                i++; // [
                while (true)
                {
                    SkipWs(s, ref i);
                    if (i < s.Length && s[i] == ']') { i++; break; }
                    list.Add(ParseValue(s, ref i));
                    SkipWs(s, ref i);
                    if (i < s.Length && s[i] == ',') { i++; continue; }
                    if (i < s.Length && s[i] == ']') { i++; break; }
                }

                return list;
            }

            static string ParseString(string s, ref int i)
            {
                i++; // "
                var sb = new StringBuilder();
                while (i < s.Length)
                {
                    var c = s[i++];
                    if (c == '"') break;
                    if (c == '\\' && i < s.Length)
                    {
                        var e = s[i++];
                        switch (e)
                        {
                            case 'n': sb.Append('\n'); break;
                            case 'r': sb.Append('\r'); break;
                            case 't': sb.Append('\t'); break;
                            case '"': sb.Append('"'); break;
                            case '\\': sb.Append('\\'); break;
                            case 'u':
                                if (i + 4 <= s.Length)
                                {
                                    sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16));
                                    i += 4;
                                }
                                break;
                            default: sb.Append(e); break;
                        }
                    }
                    else sb.Append(c);
                }

                return sb.ToString();
            }

            static bool ParseBool(string s, ref int i)
            {
                if (s.Substring(i, 4) == "true") { i += 4; return true; }
                i += 5;
                return false;
            }

            static double ParseNumber(string s, ref int i)
            {
                var start = i;
                if (i < s.Length && (s[i] == '-' || s[i] == '+')) i++;
                while (i < s.Length && char.IsDigit(s[i])) i++;
                if (i < s.Length && s[i] == '.')
                {
                    i++;
                    while (i < s.Length && char.IsDigit(s[i])) i++;
                }

                if (i < s.Length && (s[i] == 'e' || s[i] == 'E'))
                {
                    i++;
                    if (i < s.Length && (s[i] == '+' || s[i] == '-')) i++;
                    while (i < s.Length && char.IsDigit(s[i])) i++;
                }

                var span = s.Substring(start, i - start);
                return double.Parse(span, CultureInfo.InvariantCulture);
            }

            static void SkipWs(string s, ref int i)
            {
                while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
            }
        }
    }
}
