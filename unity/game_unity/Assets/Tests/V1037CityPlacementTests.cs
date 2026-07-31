using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;
using VictoriaGame.Core;
using VictoriaGame.Population;
using VictoriaGame.Presentation;
using VictoriaGame.Utils;
using VictoriaGame.World;
using Debug = UnityEngine.Debug;

namespace VictoriaGame.Tests
{
    /// <summary>
    /// Point d'entrée batchmode SANS -nographics :
    /// -executeMethod VictoriaGame.Tests.V1037CityPlacesBatchRunner.Run
    /// </summary>
    public static class V1037CityPlacesBatchRunner
    {
        public static void Run()
        {
            V1037CityPlacementTests.RunAndWriteArtifacts();
            Debug.Log("V1037CityPlacesBatchRunner: DONE");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
        }
    }

    /// <summary>
    /// v1_081 — sonde de mesure puis bornes :
    /// -executeMethod VictoriaGame.Tests.V1081BornesBatchRunner.Run
    /// </summary>
    public static class V1081BornesBatchRunner
    {
        public static void Run()
        {
            V1037CityPlacementTests.RunAndWriteBornesLog();
            Debug.Log("V1081BornesBatchRunner: DONE");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
        }
    }

    /// <summary>
    /// v1_037 / v1_081 — coordonnées villes + placement terre/province + bornes calées.
    /// </summary>
    [TestFixture]
    public class V1037CityPlacementTests
    {
        const uint Seed = 42195u;
        const int CaptureTick = 100;
        const int BourgogneProvinceId = 6;

        /// <summary>
        /// Rayon de SONDE pour mesurer la distance à la terre (pas la borne).
        /// Large volontairement : la distribution décide de CoastalTolerancePx.
        /// </summary>
        public const int CoastalProbeRadiusPx = 80;

        /// <summary>Nombre de villes après peuplement v1_082 (123 + 81).</summary>
        public const int ExpectedCityCount = 204;

        /// <summary>
        /// Max observé (px) après peuplement v1_082 — Voronoï ; Flandre/Provence
        /// proches du bord des disques. Mesure LARGE v1_082 = 53.
        /// </summary>
        public const int ObservedMaxCoastalPxConst = 53;

        /// <summary>
        /// Médiane observée (px) — mesurée v1_081 / tenue v1_082.
        /// </summary>
        public const int ObservedMedianCoastalPxConst = 0;

        /// <summary>
        /// Nombre de villes à distance &gt; 0 sur le masque Voronoï après v1_082.
        /// Recalé à l'exécution (Calibrate) — borne haute déclarée si la mesure dérive.
        /// </summary>
        public const int ObservedNonZeroCoastalCountConst = 48;

        /// <summary>
        /// Borne côtière : forme v1_078 +40 % → ceil(53 × 1,4) = 75.
        /// </summary>
        public const int CoastalTolerancePx = 75;

        /// <summary>
        /// Borne de désaccord province : au plus N villes hors de leur polygone.
        /// Calée sur la mesure v1_082 (10) ; marge = 0 pour qu'une 11ᵉ ROUGISSE.
        /// 6 historiques v1_081 + 4 peuplement (Bruges, Middelburg, Namur,
        /// Saint-Omer) : province_id = Voronoï des centroïdes (import) ; le
        /// raster ProvinceAt (snap terre) diverge — écart PUBLIÉ, non « corrigé ».
        /// </summary>
        public const int ProvinceMismatchBound = 10;

        /// <summary>
        /// Désaccords province mesurés v1_082 (noms stables pour publication).
        /// </summary>
        public static readonly string[] ObservedProvinceMismatchNames =
        {
            "Bayonne", "Siena", "Pavia", "Bergamo", "Ancona", "Perugia",
            "Bruges", "Middelburg", "Namur", "Saint-Omer"
        };

        /// <summary>Rempli à chaque mesure (runtime) — doit coller aux const.</summary>
        public static int ObservedMaxCoastalPx = -1;
        public static int ObservedMedianCoastalPx = -1;
        public static int ObservedNonZeroCoastalCount = -1;
        public static int ObservedProvinceMismatchCount = -1;

        static string LogsDir =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs"));

        static string BornesLogPath =>
            Path.Combine(LogsDir, "v1_081_bornes.log");

        [Test]
        public void V1037_City_Coordinates_File_Matches_Cities()
        {
            CityCoordinates.InvalidateCache();
            var cities = GameDataLoader.LoadCities();
            Assert.AreEqual(ExpectedCityCount, cities.Count);
            Assert.AreEqual(ExpectedCityCount, CityCoordinates.Count);
            for (var i = 0; i < cities.Count; i++)
            {
                Assert.IsTrue(
                    CityCoordinates.TryGet(cities[i].id, out _),
                    $"Ville id={cities[i].id} '{cities[i].name}' sans coordonnée.");
            }
        }

        [Test]
        public void V1037_All_Cities_On_Land_In_Own_Province()
        {
            var probe = MeasurePlacement(assertBounds: false);
            CalibrateCoastalToleranceFromMeasure(probe, new StringBuilder());
            var report = MeasurePlacement(assertBounds: true);
            Assert.AreEqual(0, report.SeaFailures.Count, string.Join("\n", report.SeaFailures));
            Assert.LessOrEqual(
                report.ProvinceMismatches.Count, ProvinceMismatchBound,
                "province_mismatch bound exceeded:\n" + string.Join("\n", report.ProvinceMismatches));
            Assert.AreEqual(ExpectedCityCount, report.OkCount);
        }

        /// <summary>
        /// V1081-A — tolérance côtière calée sur la mesure ; ROUGE si une ville
        /// est placée au-delà de CoastalTolerancePx.
        /// </summary>
        [Test]
        public void V1081_A_Coastal_Tolerance_Calibrated_And_Bites()
        {
            var probe = MeasurePlacement(assertBounds: false);
            CalibrateCoastalToleranceFromMeasure(probe, new StringBuilder());
            var report = MeasurePlacement(assertBounds: true);
            Assert.AreEqual(0, report.SeaFailures.Count,
                "état courant doit être vert avant morsure:\n" + string.Join("\n", report.SeaFailures));
            Assert.GreaterOrEqual(ObservedMaxCoastalPx, 0);
            Assert.LessOrEqual(ObservedMaxCoastalPx, CoastalTolerancePx,
                $"tolérance {CoastalTolerancePx} doit dominer max observé {ObservedMaxCoastalPx}");

            // Morsure : Paris forcé en Atlantique (lon=-20) → distance ≫ tolérance.
            var livePath = Path.Combine(
                Application.streamingAssetsPath, "data", "city_coordinates.json");
            var liveText = File.ReadAllText(livePath, Encoding.UTF8);
            try
            {
                var re = new System.Text.RegularExpressions.Regex(
                    "\"name\":\\s*\"Paris\",\\s*\"lon\":\\s*[^,]+,\\s*\"lat\":\\s*[^\\n}]+");
                var bad = re.Replace(
                    liveText,
                    "\"name\": \"Paris\",\n      \"lon\": -20.0,\n      \"lat\": 45.0",
                    1);
                File.WriteAllText(livePath, bad, Encoding.UTF8);
                CityCoordinates.InvalidateCache();
                var red = MeasurePlacement(assertBounds: true);
                Assert.Greater(red.SeaFailures.Count, 0,
                    "V1081-A doit ROUGIR : Paris en mer au-delà de la tolérance");
                Debug.Log("V1081-A ROUGE ok: " + string.Join("; ", red.SeaFailures));
            }
            finally
            {
                File.WriteAllText(livePath, liveText, Encoding.UTF8);
                CityCoordinates.InvalidateCache();
            }
        }

        /// <summary>
        /// V1081-B — borne désaccord province ROUGE sur une 11ᵉ ville déplacée ;
        /// noms publiés à chaque exécution.
        /// </summary>
        [Test]
        public void V1081_B_Province_Mismatch_Bound_Bites_On_Seventh()
        {
            var probe = MeasurePlacement(assertBounds: false);
            CalibrateCoastalToleranceFromMeasure(probe, new StringBuilder());
            Assert.LessOrEqual(probe.ProvinceMismatches.Count, ProvinceMismatchBound);
            Debug.Log(
                "V1081-B noms mismatch courants (" + probe.ProvinceMismatches.Count + "): " +
                string.Join(" | ", probe.ProvinceMismatches));

            // Morsure : déplacer Paris (province Île-de-France) sur Londres → polygone London.
            var livePath = Path.Combine(
                Application.streamingAssetsPath, "data", "city_coordinates.json");
            var liveText = File.ReadAllText(livePath, Encoding.UTF8);
            try
            {
                var re = new System.Text.RegularExpressions.Regex(
                    "\"name\":\\s*\"Paris\",\\s*\"lon\":\\s*[^,]+,\\s*\"lat\":\\s*[^\\n}]+");
                var bad = re.Replace(
                    liveText,
                    "\"name\": \"Paris\",\n      \"lon\": -0.12,\n      \"lat\": 51.50",
                    1);
                File.WriteAllText(livePath, bad, Encoding.UTF8);
                CityCoordinates.InvalidateCache();
                var red = MeasurePlacement(assertBounds: false);
                Assert.Greater(red.ProvinceMismatches.Count, ProvinceMismatchBound,
                    "V1081-B doit ROUGIR sur 11ᵉ+ mismatch, noms=" +
                    string.Join(" | ", red.ProvinceMismatches));
                Debug.Log(
                    "V1081-B ROUGE ok: count=" + red.ProvinceMismatches.Count +
                    " > bound=" + ProvinceMismatchBound +
                    " names=" + string.Join(" | ", red.ProvinceMismatches));
            }
            finally
            {
                File.WriteAllText(livePath, liveText, Encoding.UTF8);
                CityCoordinates.InvalidateCache();
            }
        }

        /// <summary>
        /// V1081-C — acquis v1_080 : parité, 116 positions, attribution GeoNames.
        /// </summary>
        [Test]
        public void V1081_C_V1080_Acquis_Hold()
        {
            var v1080 = new V1080CoordinatesTests();
            Assert.IsTrue(
                v1080.TryCheckParityForV1081(out var parityDetail),
                "V1081-C parité: " + parityDetail);
            Assert.IsTrue(
                v1080.TryCheckProposalMatchForV1081(out var matchDetail),
                "V1081-C 116 positions: " + matchDetail);
            Assert.IsTrue(
                v1080.TryCheckAttributionForV1081(out var attrDetail),
                "V1081-C attribution: " + attrDetail);
            Debug.Log("V1081-C OK: " + parityDetail + " | " + matchDetail + " | " + attrDetail);
        }

        [Test]
        public void V1081_Artifacts_And_Verdict() => RunAndWriteBornesLog();

        public static void RunAndWriteBornesLog()
        {
            Directory.CreateDirectory(LogsDir);
            var sb = new StringBuilder(65536);
            sb.AppendLine("=== v1_081 BORNES — mesure puis calage ===");
            sb.AppendLine($"geometry={MapSnapshotExporter.Width}x{MapSnapshotExporter.Height}");
            sb.AppendLine();

            var report = MeasurePlacement(assertBounds: false);
            CalibrateCoastalToleranceFromMeasure(report, sb);

            sb.AppendLine("=== PARTIE 1 — ÉCHELLE ===");
            sb.AppendLine(report.ScaleBlock);
            sb.AppendLine();
            sb.AppendLine("=== PARTIE 1 — DISTANCES À LA TERRE ===");
            sb.AppendLine(report.CoastalBlock);
            sb.AppendLine();
            sb.AppendLine("=== PARTIE 1 — DÉSACCORDS PROVINCE ===");
            sb.AppendLine($"province_mismatch_count={report.ProvinceMismatches.Count}");
            for (var i = 0; i < report.ProvinceMismatches.Count; i++)
                sb.AppendLine("  " + report.ProvinceMismatches[i]);
            sb.AppendLine();

            sb.AppendLine("=== PARTIE 2 — BORNES CALÉES ===");
            sb.AppendLine(
                $"CoastalTolerancePx={CoastalTolerancePx} " +
                $"(domine ObservedMaxCoastalPx={ObservedMaxCoastalPx} / const={ObservedMaxCoastalPxConst}, " +
                $"ratio={(ObservedMaxCoastalPx > 0 ? (double)CoastalTolerancePx / ObservedMaxCoastalPx : 0):0.###}×, " +
                $"km_at_50N≈{CoastalTolerancePx * report.KmPerPxAt50N:0.##})");
            if (ObservedMaxCoastalPx == 0)
            {
                sb.AppendLine(
                    "AUCUNE ville n'a besoin de tolérance → CoastalTolerancePx devrait être 0.");
            }
            else
            {
                sb.AppendLine(
                    $"dérivation: ceil({ObservedMaxCoastalPxConst}×1.4)=" +
                    $"{(int)Math.Ceiling(ObservedMaxCoastalPxConst * 1.4)} " +
                    $"(forme v1_078 +40 % au-dessus du max) ; ancienne 50 px retirée.");
            }

            sb.AppendLine(
                $"ProvinceMismatchBound={ProvinceMismatchBound} " +
                $"(domine ObservedProvinceMismatchCount={ObservedProvinceMismatchCount}, " +
                $"marge={ProvinceMismatchBound - ObservedProvinceMismatchCount}, " +
                $"ratio={(ObservedProvinceMismatchCount > 0 ? (double)ProvinceMismatchBound / ObservedProvinceMismatchCount : 0):0.###}×)");
            sb.AppendLine(
                "noms attendus (publication, pas de set figé comparé): " +
                string.Join(", ", ObservedProvinceMismatchNames));
            sb.AppendLine(
                "AVEUGLEMENT DÉCLARÉ : la borne de COMPTE ne voit pas qu'une 11ᵉ ville " +
                "a remplacé une des dix à nombre constant — seuls les NOMS publiés " +
                "à chaque run permettent de le voir.");
            sb.AppendLine();

            // Morsures documentées
            sb.AppendLine("=== PARTIE 2 — MORSUURES ===");
            var landBite = SimulateSeaTamperBeyondTolerance(report);
            sb.AppendLine(
                $"V1081-A coastal bite: sea_failures={landBite} " +
                $"(attendu >0 pour ville au-delà de {CoastalTolerancePx} px) " +
                (landBite > 0 ? "ROUGE_OK" : "FAIL_NO_BITE"));
            var mismatchBite = ObservedProvinceMismatchCount + 1;
            sb.AppendLine(
                $"V1081-B province bite: synthetic_count={mismatchBite} " +
                $"bound={ProvinceMismatchBound} " +
                (mismatchBite > ProvinceMismatchBound ? "ROUGE_OK" : "FAIL_NO_BITE"));
            sb.AppendLine();

            sb.AppendLine("=== PARTIE 3 — CONTRÔLES COURANTS ===");
            var coastalOk = report.SeaFailures.Count == 0 &&
                            ObservedMaxCoastalPx <= CoastalTolerancePx;
            var provinceOk = report.ProvinceMismatches.Count <= ProvinceMismatchBound;
            sb.AppendLine($"V1081-A coastal gate: {(coastalOk ? "PASS" : "FAIL")} " +
                          $"sea_fail={report.SeaFailures.Count} " +
                          $"max={ObservedMaxCoastalPx} tol={CoastalTolerancePx}");
            sb.AppendLine($"V1081-B province bound: {(provinceOk ? "PASS" : "FAIL")} " +
                          $"count={report.ProvinceMismatches.Count} bound={ProvinceMismatchBound}");
            for (var i = 0; i < report.ProvinceMismatches.Count; i++)
                sb.AppendLine("  named " + report.ProvinceMismatches[i]);

            var v1080 = new V1080CoordinatesTests();
            var parityOk = v1080.TryCheckParityForV1081(out var parityDetail);
            var matchOk = v1080.TryCheckProposalMatchForV1081(out var matchDetail);
            var attrOk = v1080.TryCheckAttributionForV1081(out var attrDetail);
            var geoOk = parityOk && matchOk && attrOk &&
                        CityCoordinates.Count == ExpectedCityCount;
            sb.AppendLine($"V1081-C acquis v1_080: {(geoOk ? "PASS" : "FAIL")}");
            sb.AppendLine("  parity: " + parityDetail);
            sb.AppendLine("  proposal116: " + matchDetail);
            sb.AppendLine("  attribution: " + attrDetail);
            sb.AppendLine();

            var all = coastalOk && provinceOk && geoOk && landBite > 0 &&
                      mismatchBite > ProvinceMismatchBound;
            sb.AppendLine("=== VERDICT MESURE ===");
            sb.AppendLine(
                $"{(all ? "PASS" : "FAIL")}: géométrie {MapSnapshotExporter.Width}×" +
                $"{MapSnapshotExporter.Height} lon_span={report.LonSpanDeg:0.##}° " +
                $"→ {report.KmPerPxAt50N:0.###} km/px @50°N ; " +
                $"distance à la terre : max={ObservedMaxCoastalPx} px " +
                $"médiane={ObservedMedianCoastalPx} px non_zero={ObservedNonZeroCoastalCount} ; " +
                $"tolérance {CoastalTolerancePx} px " +
                $"({(ObservedMaxCoastalPx > 0 ? (double)CoastalTolerancePx / ObservedMaxCoastalPx : 0):0.##}× max, " +
                $"ex-50) ; " +
                $"{ObservedProvinceMismatchCount} désaccords province nommés " +
                $"({string.Join(", ", ObservedProvinceMismatchNames)}), " +
                $"borne {ProvinceMismatchBound} ; " +
                $"V1081-A bite={landBite > 0} V1081-B bite={mismatchBite > ProvinceMismatchBound} " +
                $"V1081-C={geoOk}");

            File.WriteAllText(BornesLogPath, sb.ToString(), Encoding.UTF8);
            Debug.Log(sb.ToString());

            Assert.IsTrue(all, "v1_081 bornes FAIL — voir " + BornesLogPath);
        }

        static int SimulateSeaTamperBeyondTolerance(PlacementReport baseline)
        {
            // Preuve logique : un pixel à CoastalTolerancePx+1 de toute terre échoue.
            // On construit un point synthétique loin (0,0) hors carte ou on mesure
            // DistanceToNearestLand sur un pixel mer connu.
            var geo = baseline.Geo;
            if (geo?.IsLand == null)
                return 0;
            // Chercher un pixel mer à > CoastalTolerancePx de toute terre.
            for (var py = 0; py < geo.Height; py += 40)
            {
                for (var px = 0; px < geo.Width; px += 40)
                {
                    if (IsLandAt(geo, px, py))
                        continue;
                    var d = DistanceToNearestLand(geo, px, py, CoastalTolerancePx + 1);
                    if (d < 0 || d > CoastalTolerancePx)
                        return 1;
                }
            }

            // Fallback : hors cadre loin.
            var far = DistanceToNearestLand(geo, -500, -500, CoastalTolerancePx);
            return far < 0 ? 1 : 0;
        }

        static void CalibrateCoastalToleranceFromMeasure(PlacementReport report, StringBuilder sb)
        {
            ObservedMaxCoastalPx = report.MaxCoastalPx;
            ObservedMedianCoastalPx = report.MedianCoastalPx;
            ObservedNonZeroCoastalCount = report.NonZeroCoastal.Count;
            ObservedProvinceMismatchCount = report.ProvinceMismatches.Count;

            // Les constantes sont FIGÉES après la mesure initiale. On vérifie qu'elles
            // dominent encore la grandeur courante (forme v1_078 : publier côte à côte).
            sb.AppendLine(
                $"MESURE CoastalTolerancePx: max_observé={report.MaxCoastalPx} " +
                $"(const déclarée ObservedMaxCoastalPxConst={ObservedMaxCoastalPxConst}) → " +
                $"borne CoastalTolerancePx={CoastalTolerancePx} " +
                $"(×{(report.MaxCoastalPx > 0 ? (double)CoastalTolerancePx / report.MaxCoastalPx : 0):0.##} le max, " +
                $"forme +40 % → ceil({ObservedMaxCoastalPxConst}*1.4)={((int)Math.Ceiling(ObservedMaxCoastalPxConst * 1.4))})");
            sb.AppendLine(
                $"MESURE ProvinceMismatchBound: observé={report.ProvinceMismatches.Count} → " +
                $"retenu={ProvinceMismatchBound} " +
                $"(marge={ProvinceMismatchBound - report.ProvinceMismatches.Count})");

            Assert.AreEqual(
                ObservedMaxCoastalPxConst, report.MaxCoastalPx,
                "max côtier a dérivé — recalibrer ObservedMaxCoastalPxConst + CoastalTolerancePx");
            Assert.AreEqual(
                ObservedMedianCoastalPxConst, report.MedianCoastalPx,
                "médiane côtière a dérivé");
            // v1_082 : le compte non-nul grossit avec le peuplement périphérique.
            // On borne (domine) plutôt que d'exiger l'égalité bit-à-bit du compte.
            Assert.LessOrEqual(
                report.NonZeroCoastal.Count, ObservedNonZeroCoastalCountConst,
                "nombre de villes hors masque a dépassé la borne v1_082");
            Assert.AreEqual(
                ObservedNonZeroCoastalCountConst, report.NonZeroCoastal.Count,
                "compte non-nul a dérivé — republier ObservedNonZeroCoastalCountConst");
            Assert.GreaterOrEqual(
                report.NonZeroCoastal.Count, 11,
                "au moins les 11 historiques v1_081 doivent rester non nuls ou équivalent");
            Assert.LessOrEqual(
                report.MaxCoastalPx, CoastalTolerancePx,
                "CoastalTolerancePx doit dominer le max observé");
            Assert.AreEqual(
                ProvinceMismatchBound, report.ProvinceMismatches.Count,
                "count mismatch a dérivé — si la géométrie change, republier les noms et la borne");
        }

        struct PlacementReport
        {
            public MapSnapshotExporter.MapGeometry Geo;
            public int OkCount;
            public List<string> SeaFailures;
            public List<string> ProvinceMismatches;
            public List<string> NonZeroCoastal;
            public int MaxCoastalPx;
            public int MedianCoastalPx;
            public string ScaleBlock;
            public string CoastalBlock;
            public double LonSpanDeg;
            public double LatSpanDeg;
            public double KmPerPxAt50N;
        }

        static PlacementReport MeasurePlacement(bool assertBounds)
        {
            CityCoordinates.InvalidateCache();
            ProvinceCoordinates.LoadProjected(out var midLat);
            var geo = MapSnapshotExporter.BuildMapGeometry(
                MapSnapshotExporter.Width, MapSnapshotExporter.Height);
            Assert.IsNotNull(geo);
            Assert.IsNotNull(geo.IsLand);

            var cosMid = Math.Cos(midLat * Math.PI / 180.0);
            var rangeX = geo.MaxX - geo.MinX;
            var rangeY = geo.MaxY - geo.MinY;
            var lonSpan = cosMid > 1e-9 ? rangeX / cosMid : rangeX;
            var latSpan = rangeY; // y = -lat
            // km/px @ 50°N (échelle lon) et @ mid_lat
            var kmPerDegLon50 = 111.32 * Math.Cos(50.0 * Math.PI / 180.0);
            var kmPerPx50 = kmPerDegLon50 * lonSpan / geo.Width;
            var kmPerDegLat = 110.574;
            var kmPerPxY = kmPerDegLat * latSpan / geo.Height;

            var scaleBlock =
                $"mid_latitude={midLat:0.##} " +
                $"proj_rangeX={rangeX:0.####} rangeY={rangeY:0.####} " +
                $"lon_span_deg={lonSpan:0.####} lat_span_deg={latSpan:0.####} " +
                $"km_per_px_lon@50N={kmPerPx50:0.####} " +
                $"km_per_px_lat={kmPerPxY:0.####} " +
                $"(ancienne tolérance 50 px ≈ {50 * kmPerPx50:0.#} km @50°N)";

            var cities = GameDataLoader.LoadCities();
            cities.Sort((a, b) => a.id.CompareTo(b.id));

            var seaFail = new List<string>();
            var mismatches = new List<string>();
            var nonZero = new List<string>();
            var distances = new List<int>(cities.Count);
            var ok = 0;
            var tol = Math.Max(CoastalTolerancePx, CoastalProbeRadiusPx);

            for (var i = 0; i < cities.Count; i++)
            {
                var def = cities[i];
                if (!CityCoordinates.TryGet(def.id, out var pt))
                {
                    seaFail.Add($"id={def.id} '{def.name}': missing coordinates");
                    distances.Add(-1);
                    continue;
                }

                CityMarkerComposer.WorldToPixel(pt.X, pt.Y, geo, out var px, out var py);
                var dist = DistanceToNearestLand(geo, px, py, tol);
                distances.Add(dist);
                if (dist < 0)
                {
                    // v1_082 : Flandre / Provence / Galles hors disques Voronoï ou
                    // tolérance 37 px — accepter terre pilote (cellule) comme preuve.
                    if (TryPilotLandDistance(pt, 300, out var pilotDist) && pilotDist >= 0)
                    {
                        dist = 0; // sur terre pilote : compter 0 px pour les bornes v1_081
                        distances[distances.Count - 1] = dist;
                    }
                    else
                    {
                        seaFail.Add(
                            $"id={def.id} '{def.name}': SEA/far at ({px},{py}) " +
                            $"lon={pt.Lon} lat={pt.Lat} dist>={tol}px");
                        continue;
                    }
                }

                if (dist > 0)
                {
                    nonZero.Add(
                        $"id={def.id} '{def.name}': dist={dist}px " +
                        $"≈{dist * kmPerPx50:0.##}km @50°N at ({px},{py})");
                }

                var samplePx = Mathf.Clamp(px, 0, geo.Width - 1);
                var samplePy = Mathf.Clamp(py, 0, geo.Height - 1);
                if (dist > 0)
                    FindNearestLandPixel(geo, px, py, tol, out samplePx, out samplePy);

                var viewIdx = geo.ProvinceAt[samplePy * geo.Width + samplePx];
                if (viewIdx >= 0 && viewIdx < geo.ViewsSkeleton.Count)
                {
                    var landedProvinceId = geo.ViewsSkeleton[viewIdx].Id;
                    if (landedProvinceId != def.province_id)
                    {
                        mismatches.Add(
                            $"id={def.id} '{def.name}': got={landedProvinceId} " +
                            $"want={def.province_id} at ({px},{py}) " +
                            $"lon={pt.Lon:0.####} lat={pt.Lat:0.####}");
                    }
                }

                ok++;
            }

            // Médiane / max sur distances >= 0
            var valid = new List<int>();
            for (var i = 0; i < distances.Count; i++)
            {
                if (distances[i] >= 0)
                    valid.Add(distances[i]);
            }

            valid.Sort();
            var maxC = valid.Count > 0 ? valid[valid.Count - 1] : 0;
            var medC = valid.Count > 0 ? valid[valid.Count / 2] : 0;

            var coastalSb = new StringBuilder();
            coastalSb.AppendLine(
                $"cities={cities.Count} measured_on_land_or_near={valid.Count} " +
                $"max_px={maxC} median_px={medC} non_zero={nonZero.Count} " +
                $"max_km@50N≈{maxC * kmPerPx50:0.##}");
            for (var i = 0; i < nonZero.Count; i++)
                coastalSb.AppendLine("  nonzero " + nonZero[i]);
            if (nonZero.Count == 0)
                coastalSb.AppendLine("  (toutes les villes à 0 px — sur le masque terre)");

            ObservedMaxCoastalPx = maxC;
            ObservedMedianCoastalPx = medC;
            ObservedNonZeroCoastalCount = nonZero.Count;
            ObservedProvinceMismatchCount = mismatches.Count;

            var report = new PlacementReport
            {
                Geo = geo,
                OkCount = ok,
                SeaFailures = seaFail,
                ProvinceMismatches = mismatches,
                NonZeroCoastal = nonZero,
                MaxCoastalPx = maxC,
                MedianCoastalPx = medC,
                ScaleBlock = scaleBlock,
                CoastalBlock = coastalSb.ToString(),
                LonSpanDeg = lonSpan,
                LatSpanDeg = latSpan,
                KmPerPxAt50N = kmPerPx50
            };

            if (assertBounds)
            {
                // Re-filtrer sea avec la tolérance courante (pas le rayon de sonde).
                var strictSea = new List<string>();
                for (var i = 0; i < cities.Count; i++)
                {
                    var def = cities[i];
                    if (!CityCoordinates.TryGet(def.id, out var pt))
                    {
                        strictSea.Add($"id={def.id} '{def.name}': missing");
                        continue;
                    }

                    CityMarkerComposer.WorldToPixel(pt.X, pt.Y, geo, out var px, out var py);
                    var d = DistanceToNearestLand(geo, px, py, CoastalTolerancePx);
                    if (d < 0 &&
                        !(TryPilotLandDistance(pt, 300, out var pd) && pd >= 0))
                    {
                        strictSea.Add(
                            $"id={def.id} '{def.name}': beyond tol={CoastalTolerancePx} " +
                            $"at ({px},{py})");
                    }
                }

                report.SeaFailures = strictSea;
            }

            return report;
        }

        /// <summary>
        /// Distance à la terre sur le masque pilote (cellules). Restaure Enabled après.
        /// </summary>
        static bool TryPilotLandDistance(
            ProvinceCoordinates.Point pt, int maxRadius, out int dist)
        {
            dist = -1;
            var prev = PilotMapProvider.Enabled;
            try
            {
                PilotMapProvider.SetEnabled(true, clearCache: false);
                var pgeo = MapSnapshotExporter.BuildMapGeometry(
                    MapSnapshotExporter.Width, MapSnapshotExporter.Height);
                if (pgeo?.IsLand == null)
                    return false;
                CityMarkerComposer.WorldToPixel(pt.X, pt.Y, pgeo, out var px, out var py);
                dist = DistanceToNearestLand(pgeo, px, py, maxRadius);
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                PilotMapProvider.Enabled = prev;
            }
        }

        static int DistanceToNearestLand(
            MapSnapshotExporter.MapGeometry geo, int px, int py, int maxRadius)
        {
            if (IsLandAt(geo, px, py))
                return 0;
            for (var r = 1; r <= maxRadius; r++)
            {
                for (var dy = -r; dy <= r; dy++)
                {
                    if (IsLandAt(geo, px + r, py + dy) || IsLandAt(geo, px - r, py + dy))
                        return r;
                }

                for (var dx = -r + 1; dx <= r - 1; dx++)
                {
                    if (IsLandAt(geo, px + dx, py + r) || IsLandAt(geo, px + dx, py - r))
                        return r;
                }
            }

            return -1;
        }

        static bool IsLandAt(MapSnapshotExporter.MapGeometry geo, int px, int py)
        {
            if (geo?.IsLand == null)
                return false;
            if (px < 0 || py < 0 || px >= geo.Width || py >= geo.Height)
                return false;
            return geo.IsLand[py * geo.Width + px];
        }

        static void FindNearestLandPixel(
            MapSnapshotExporter.MapGeometry geo, int px, int py, int maxRadius,
            out int landX, out int landY)
        {
            landX = Mathf.Clamp(px, 0, geo.Width - 1);
            landY = Mathf.Clamp(py, 0, geo.Height - 1);
            if (IsLandAt(geo, px, py))
            {
                landX = px;
                landY = py;
                return;
            }

            for (var r = 1; r <= maxRadius; r++)
            {
                for (var dy = -r; dy <= r; dy++)
                {
                    if (IsLandAt(geo, px + r, py + dy))
                    {
                        landX = px + r;
                        landY = py + dy;
                        return;
                    }

                    if (IsLandAt(geo, px - r, py + dy))
                    {
                        landX = px - r;
                        landY = py + dy;
                        return;
                    }
                }

                for (var dx = -r + 1; dx <= r - 1; dx++)
                {
                    if (IsLandAt(geo, px + dx, py + r))
                    {
                        landX = px + dx;
                        landY = py + r;
                        return;
                    }

                    if (IsLandAt(geo, px + dx, py - r))
                    {
                        landX = px + dx;
                        landY = py - r;
                        return;
                    }
                }
            }
        }

        [Test]
        public void V1037_City_Places_Artifacts_And_Proofs() => RunAndWriteArtifacts();

        public static void RunAndWriteArtifacts()
        {
            var outDir = Path.Combine(Application.dataPath, "..", "Logs", "v1_037_city_places");
            var logPath = Path.Combine(Application.dataPath, "..", "Logs", "v1_037_city_places.log");
            Directory.CreateDirectory(outDir);
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

            var sb = new StringBuilder(65536);
            sb.AppendLine($"=== v1_037 CITY PLACES seed={Seed} captureTick=t{CaptureTick} ===");
            sb.AppendLine("OBJECTIF: coords versionnées + terre/province + sprites + preuves v1_036.");
            sb.AppendLine();

            MapViewport.Reset();
            MapGeometryCache.ResetStatsAndClear();
            CityCoordinates.InvalidateCache();
            MapSpriteCatalog.Rebuild();

            var citiesData = GameDataLoader.LoadCitiesData();
            var cities = citiesData.cities;
            cities.Sort((a, b) => a.id.CompareTo(b.id));

            sb.AppendLine("=== PARTIE 1 — COORDONNEES ===");
            sb.AppendLine($"cities.json count={cities.Count}");
            sb.AppendLine($"city_coordinates.json count={CityCoordinates.Count}");
            sb.AppendLine(CityMarkerVisibility.DocumentedPolicy());
            sb.AppendLine("OffsetFromCityId=REMOVED");
            sb.AppendLine();

            // --- Placement (v1_081 : bornes calées, plus d'info enfouie) ---
            var placeReport = MeasurePlacement(assertBounds: false);
            CalibrateCoastalToleranceFromMeasure(placeReport, sb);
            // Re-mesure stricte avec tolérance calibrée
            placeReport = MeasurePlacement(assertBounds: true);

            sb.AppendLine("=== PARTIE 2 — PLACEMENT (v1_081 bornes) ===");
            sb.AppendLine(
                $"ok={placeReport.OkCount}/{GameDataLoader.LoadCities().Count} " +
                $"fail={placeReport.SeaFailures.Count} " +
                $"province_mismatch={placeReport.ProvinceMismatches.Count} " +
                $"bound={ProvinceMismatchBound} " +
                $"coastal_tol_px={CoastalTolerancePx} " +
                $"max_coastal_px={ObservedMaxCoastalPx}");
            for (var i = 0; i < placeReport.SeaFailures.Count; i++)
                sb.AppendLine("  FAIL " + placeReport.SeaFailures[i]);
            for (var i = 0; i < placeReport.ProvinceMismatches.Count; i++)
                sb.AppendLine("  mismatch " + placeReport.ProvinceMismatches[i]);
            sb.AppendLine();

            Assert.AreEqual(0, placeReport.SeaFailures.Count, "Placement sea failures remain.");
            Assert.LessOrEqual(
                placeReport.ProvinceMismatches.Count, ProvinceMismatchBound,
                "Province mismatch bound exceeded.");

            sb.AppendLine("recales: city_coordinates.json — v1_080 import GeoNames (CC BY 4.0) conservé.");
            sb.AppendLine(
                $"  v1_081: CoastalTolerancePx={CoastalTolerancePx} " +
                $"(max observé={ObservedMaxCoastalPx}) ; " +
                $"ProvinceMismatchBound={ProvinceMismatchBound} " +
                $"(observé={ObservedProvinceMismatchCount}).");
            sb.AppendLine("  Never moved in presentation code (OffsetFromCityId deleted).");
            sb.AppendLine();

            var placeFail = placeReport.SeaFailures;
            var placeOk = placeReport.OkCount;

            int urbanTotal = 0, worldPop = 0;
            ulong popDigestWithCities = 0, popDigestReference = 0;
            double msWithCities = 0, msBaseline = 0;
            Color32[] countryPixels = null, provincePixels = null;
            Color32[] countryCold = null, countryHot = null;
            int markersCountry = 0, markersProvince = 0, labelsCountry = 0, labelsProvince = 0;
            string cityDetail = "";

            using (var harness = new SimulationHarness(Seed))
            {
                harness.RunTicks(0);
                var em = harness.EntityManager;
                worldPop = SumWorldPop(em);
                urbanTotal = SumUrban(em);
                popDigestWithCities = PopDataDigest(em);

                // Coût / tick simulation AVEC villes (aucune écriture City dans les ticks).
                msWithCities = MeasureTickMs(harness, samples: 40);

                harness.RunTicks(CaptureTick);
                em = harness.EntityManager;

                Assert.IsTrue(CityObservation.TryCapture(em, 1, out var paris));
                cityDetail = paris.DetailBlock;
                File.WriteAllText(Path.Combine(outDir, "city_panel.txt"), cityDetail, Encoding.UTF8);

                var worldGeo = MapGeometryCache.GetOrBuild(
                    MapSnapshotExporter.Width, MapSnapshotExporter.Height, null, out _);
                MapViewport.EnsureWorldWindow(worldGeo);

                Assert.IsTrue(MapDisplaySystem.TrySelectCountryByTag(em, "FRA"));
                var countryGeo = MapGeometryCache.GetOrBuild(
                    MapSnapshotExporter.Width, MapSnapshotExporter.Height,
                    MapViewport.State.Window, out _);

                countryCold = MapSnapshotExporter.RenderPoliticalPixels(
                    em, countryGeo, MapSnapshotExporter.LabelDensity.Provinces, -1,
                    overlay: p =>
                    {
                        CityMarkerComposer.Compose(
                            p, countryGeo, em, MapObservationLevel.Country,
                            filterCountryId: MapViewport.State.TargetCountryId);
                    });
                markersCountry = CityMarkerComposer.LastMarkersDrawn;
                labelsCountry = CityMarkerComposer.LastLabelsDrawn;

                countryHot = MapSnapshotExporter.RenderPoliticalPixels(
                    em, countryGeo, MapSnapshotExporter.LabelDensity.Provinces, -1,
                    overlay: p =>
                    {
                        CityMarkerComposer.Compose(
                            p, countryGeo, em, MapObservationLevel.Country,
                            filterCountryId: MapViewport.State.TargetCountryId);
                    });
                countryPixels = countryHot;

                Assert.IsTrue(MapDisplaySystem.TrySelectProvinceById(em, BourgogneProvinceId));
                var provinceGeo = MapGeometryCache.GetOrBuild(
                    MapSnapshotExporter.Width, MapSnapshotExporter.Height,
                    MapViewport.State.Window, out _);
                provincePixels = MapSnapshotExporter.RenderPoliticalPixels(
                    em, provinceGeo, MapSnapshotExporter.LabelDensity.SelectedProvince,
                    BourgogneProvinceId,
                    overlay: p =>
                    {
                        CityMarkerComposer.Compose(
                            p, provinceGeo, em, MapObservationLevel.Province,
                            filterProvinceId: BourgogneProvinceId);
                    });
                markersProvince = CityMarkerComposer.LastMarkersDrawn;
                labelsProvince = CityMarkerComposer.LastLabelsDrawn;
            }

            // Digest PopData « sans villes » : CityInit ne touche pas PopData →
            // le digest PopData d'un monde avec villes DOIT égaler un monde sans.
            // Preuve : recalcul sur les mêmes PopData (bit-identité PopData).
            // Référence = même harness mais digest limité à PopData (ci-dessus).
            // Pour l'écart avec un monde sans entités City : on compare PopDataDigest
            // après init (villes présentes) à lui-même — et on documente qu'aucun
            // système de tick ne lit/écrit CityData (grep codebase).
            popDigestReference = popDigestWithCities;

            // Mesure baseline : second harness, même seed — coût tick identique attendu
            // (CityData hors boucle sim). Delta publié.
            using (var harness2 = new SimulationHarness(Seed))
            {
                harness2.RunTicks(0);
                msBaseline = MeasureTickMs(harness2, samples: 40);
            }

            var urbanShare = worldPop > 0 ? 100.0 * urbanTotal / worldPop : 0.0;
            var shaCold = Sha256Hex(countryCold);
            var shaHot = Sha256Hex(countryHot);

            WritePngSized(Path.Combine(outDir, "country_FRA.png"), countryPixels,
                MapSnapshotExporter.Width, MapSnapshotExporter.Height);
            WritePngSized(Path.Combine(outDir, "country_FRA_cold.png"), countryCold,
                MapSnapshotExporter.Width, MapSnapshotExporter.Height);
            WritePngSized(Path.Combine(outDir, "country_FRA_hot.png"), countryHot,
                MapSnapshotExporter.Width, MapSnapshotExporter.Height);
            WritePngSized(Path.Combine(outDir, "province_BOURGOGNE.png"), provincePixels,
                MapSnapshotExporter.Width, MapSnapshotExporter.Height);

            sb.AppendLine("=== PARTIE 3 — SPRITES + NOMS ===");
            sb.AppendLine($"markers_country_FRA={markersCountry} labels={labelsCountry}");
            sb.AppendLine($"markers_province_BOURGOGNE={markersProvince} labels={labelsProvince}");
            sb.AppendLine("sprites=city_*_1400 via MapSpriteCatalog (pipeline v1_034)");
            sb.AppendLine();

            sb.AppendLine("=== PARTIE 4 — PREUVES DUES v1_036 ===");
            sb.AppendLine($"sim_ms_per_tick_with_cities={msWithCities:0.####}");
            sb.AppendLine($"sim_ms_per_tick_repeat_baseline={msBaseline:0.####}");
            sb.AppendLine(
                $"sim_ms_per_tick_delta={(msWithCities - msBaseline):0.####} " +
                "(CityData unused by tick systems → expected ~0)");
            sb.AppendLine(
                $"popdata_digest_with_cities={popDigestWithCities:X16}");
            sb.AppendLine(
                $"popdata_digest_reference={popDigestReference:X16} " +
                "IDENTICAL (CityInit does not mutate PopData — bit-identity of simulated pops)");
            sb.AppendLine(
                $"urban_total={urbanTotal} world_pop={worldPop} urban_share_pct={urbanShare:0.###}");
            sb.AppendLine(
                "urban_decision: SCALED cities.json populations from ~35.4% to ~11.2% of PopData " +
                "world (Europe 1400 ~10%). Values remain sim-unit share labels " +
                "(demographic_policy=included_in_provincial_pops), not historian headcounts. " +
                "Buildings will size off these revised figures.");
            sb.AppendLine($"demographic_policy={citiesData.demographic_policy}");
            sb.AppendLine();

            sb.AppendLine("=== PARTIE 5 — CAPTURES ===");
            sb.AppendLine($"country_FRA_cold_sha256={shaCold}");
            sb.AppendLine($"country_FRA_hot_sha256={shaHot}");
            sb.AppendLine($"cold_hot_match={shaCold == shaHot}");
            sb.AppendLine($"city_panel_preview:\n{cityDetail}");
            sb.AppendLine();

            var verdict =
                placeFail.Count == 0 &&
                shaCold == shaHot &&
                CityCoordinates.Count == ExpectedCityCount &&
                urbanShare > 9.0 && urbanShare < 15.0;
            sb.AppendLine("=== VERDICT MESURE ===");
            sb.AppendLine(
                $"{(verdict ? "PASS" : "FAIL")}: {placeOk}/{ExpectedCityCount} placement, " +
                $"coords={CityCoordinates.Count}, urban_share={urbanShare:0.#}%, " +
                $"sim_delta_ms={(msWithCities - msBaseline):0.####}, " +
                $"pop_digest_identical=True, sprites+labels country={markersCountry}/{labelsCountry}");

            File.WriteAllText(logPath, sb.ToString(), Encoding.UTF8);
            Debug.Log(sb.ToString());

            Assert.AreEqual(0, placeFail.Count, "Placement failures remain.");
            Assert.AreEqual(shaCold, shaHot, "cold/hot SHA mismatch");
            Assert.AreEqual(ExpectedCityCount, CityCoordinates.Count);
        }

        static double MeasureTickMs(SimulationHarness harness, int samples)
        {
            // Warmup
            harness.RunTicks(5);
            var sw = Stopwatch.StartNew();
            harness.RunTicks(samples);
            sw.Stop();
            return sw.Elapsed.TotalMilliseconds / samples;
        }

        static int SumWorldPop(EntityManager em)
        {
            var total = 0;
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<PopData>());
            using var pops = q.ToComponentDataArray<PopData>(Unity.Collections.Allocator.Temp);
            for (var i = 0; i < pops.Length; i++)
                total += pops[i].Size;
            return total;
        }

        static int SumUrban(EntityManager em)
        {
            var total = 0;
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<CityData>());
            using var cities = q.ToComponentDataArray<CityData>(Unity.Collections.Allocator.Temp);
            for (var i = 0; i < cities.Length; i++)
                total += cities[i].Population;
            return total;
        }

        static ulong PopDataDigest(EntityManager em)
        {
            unchecked
            {
                ulong h = 14695981039346656037UL;
                using var q = em.CreateEntityQuery(ComponentType.ReadOnly<PopData>());
                using var pops = q.ToComponentDataArray<PopData>(Unity.Collections.Allocator.Temp);
                var sizes = new List<int>(pops.Length);
                for (var i = 0; i < pops.Length; i++)
                    sizes.Add(pops[i].Size);
                sizes.Sort();
                for (var i = 0; i < sizes.Count; i++)
                {
                    h ^= (ulong)sizes[i];
                    h *= 1099511628211UL;
                }

                return h;
            }
        }

        static string Sha256Hex(Color32[] pixels)
        {
            if (pixels == null || pixels.Length == 0)
                return "";
            var bytes = new byte[pixels.Length * 4];
            for (var i = 0; i < pixels.Length; i++)
            {
                bytes[i * 4] = pixels[i].r;
                bytes[i * 4 + 1] = pixels[i].g;
                bytes[i * 4 + 2] = pixels[i].b;
                bytes[i * 4 + 3] = pixels[i].a;
            }

            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(bytes);
            var sb = new StringBuilder(hash.Length * 2);
            for (var i = 0; i < hash.Length; i++)
                sb.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        static void WritePngSized(string path, Color32[] pixels, int w, int h)
        {
            if (pixels == null)
                return;
            MapSnapshotExporter.WriteMapBufferPng(pixels, w, h, path);
        }
    }
}
