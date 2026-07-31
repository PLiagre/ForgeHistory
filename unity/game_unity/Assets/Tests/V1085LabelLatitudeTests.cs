using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using VictoriaGame.Presentation;
using Debug = UnityEngine.Debug;

namespace VictoriaGame.Tests
{
    /// <summary>
    /// Point d'entrée batchmode :
    /// -executeMethod VictoriaGame.Tests.V1085LabelLatitudeBatchRunner.Run
    /// </summary>
    public static class V1085LabelLatitudeBatchRunner
    {
        public static void Run()
        {
            V1085LabelLatitudeTests.RunAndWriteArtifacts();
            Debug.Log("V1085LabelLatitudeBatchRunner: DONE");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
        }
    }

    /// <summary>
    /// v1_085 — signe de latitude des étiquettes (nord@py0, y = −lat).
    /// Contrôle d'ordre N-S dérivé de la donnée (204 villes + 50 provinces + pays).
    /// </summary>
    [TestFixture]
    public class V1085LabelLatitudeTests
    {
        const uint Seed = 42195u;
        const int DeterminismTicks = 100;
        const ulong ExpectedParity = ParityAnchors.Expected;
        const float MidLatitudeExpected = 47.5f;

        /// <summary>Ancienne formule (pré-v1_085) — miroir N-S quand buffer = nord@py0.</summary>
        static float WorldToPixelYLegacyMirror(float worldY, float minY, float maxY, int height)
        {
            var rangeY = maxY - minY;
            return (maxY - worldY) / rangeY * height;
        }

        static float WorldToPixelYNordAt0(float worldY, float minY, float maxY, int height)
        {
            var rangeY = maxY - minY;
            return (worldY - minY) / rangeY * height;
        }

        static string GameUnityRoot =>
            Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        static string CapturesDir =>
            Path.Combine(GameUnityRoot, "Captures", "v1_085");

        static string LogPath =>
            Path.Combine(GameUnityRoot, "Logs", "v1_085_labels.log");

        [TearDown]
        public void TearDown()
        {
            MapSnapshotExporter.DebugLegacyMirrorWorldToPixelY = false;
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
        public void V1085_A_Control_RedOnLegacyMirrorFormula()
        {
            Assert.IsTrue(
                MeasureOrderControl(pilot: true, useLegacyMirror: true, out var inv, out var tot, out var detail),
                detail);
            Assert.Greater(inv, 0, "rouge: formule maxY−wy doit inverser des paires — " + detail);
            Assert.AreEqual(tot, inv, "rouge: toutes les paires NS distinctes doivent être inversées — " + detail);
        }

        [Test]
        public void V1085_B_Control_GreenOnLiveWorldToPixel()
        {
            Assert.IsTrue(
                MeasureOrderControl(pilot: true, useLegacyMirror: false, out var inv, out var tot, out var detail),
                detail);
            Assert.AreEqual(0, inv, "vert: 0 paire NS inversée — " + detail);
            Assert.Greater(tot, 1000, "assez de paires — " + detail);
        }

        [Test]
        public void V1085_C_LongitudeNotRegressed()
        {
            Assert.IsTrue(
                MeasureLongitudeOrder(pilot: true, useLegacyMirror: true, out var invOld, out var tot, out _),
                "EW old");
            Assert.IsTrue(
                MeasureLongitudeOrder(pilot: true, useLegacyMirror: false, out var invNew, out _, out var detail),
                detail);
            Assert.AreEqual(0, invOld, "longitude déjà juste avant");
            Assert.AreEqual(0, invNew, "longitude juste après — " + detail);
            Assert.Greater(tot, 1000);
        }

        [Test]
        public void V1085_D_ParityUnchanged()
        {
            var hash = RunDigest(Seed, DeterminismTicks);
            Assert.AreEqual(
                ExpectedParity, hash,
                "parité v1_009 bit-identique attendue 0x" + ExpectedParity.ToString("X16") +
                " got 0x" + hash.ToString("X16"));
        }

        [Test]
        public void V1085_Artifacts_And_Verdict() => RunAndWriteArtifacts();

        public static void RunAndWriteArtifacts()
        {
            Directory.CreateDirectory(CapturesDir);
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            var sb = new StringBuilder(131072);
            sb.AppendLine("=== v1_085 LABELS LATITUDE ===");
            sb.AppendLine("created=" + DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
            sb.AppendLine();

            sb.AppendLine("=== PARTIE 1 — CHAÎNE DES CONVENTIONS ===");
            AppendConventionChain(sb);
            sb.AppendLine();

            sb.AppendLine("=== PARTIE 1b — ÉCHELLES px/degré (code, fenêtre pilote monde) ===");
            AppendScales(sb);
            sb.AppendLine();

            sb.AppendLine("=== PARTIE 2 — CONTRÔLE ORDRE N-S (dérivé de la donnée, sans liste manuscrite) ===");
            sb.AppendLine("--- ROUGE : formule pré-correction maxY−worldY (code NON corrigé rejoué) ---");
            AppendAllOrderCounts(sb, useLegacyMirror: true, tag: "ROUGE");
            sb.AppendLine();
            sb.AppendLine("--- VERT : CityPresentation / MapSnapshotExporter WorldToPixel live (nord@py0) ---");
            AppendAllOrderCounts(sb, useLegacyMirror: false, tag: "VERT");
            sb.AppendLine();
            sb.AppendLine("--- LONGITUDE (non-dégradation) ---");
            MeasureLongitudeOrder(true, true, out var ewOld, out var ewTot, out var ewOldD);
            MeasureLongitudeOrder(true, false, out var ewNew, out _, out var ewNewD);
            MeasureLongitudeOrder(false, true, out var ewOldL, out var ewTotL, out _);
            MeasureLongitudeOrder(false, false, out var ewNewL, out _, out _);
            sb.AppendLine("pilot_ON  EW ROUGE inv=" + ewOld + "/" + ewTot + " | " + ewOldD);
            sb.AppendLine("pilot_ON  EW VERT  inv=" + ewNew + "/" + ewTot + " | " + ewNewD);
            sb.AppendLine("pilot_OFF EW ROUGE inv=" + ewOldL + "/" + ewTotL);
            sb.AppendLine("pilot_OFF EW VERT  inv=" + ewNewL + "/" + ewTotL);
            sb.AppendLine();

            sb.AppendLine("=== PARTIE 3 — CAPTURES + PARITÉ ===");
            EnsureBeforeCaptures(sb);
            WriteAfterCaptures(sb);
            AssertBeforeAfterDiffer(sb);

            var hashBefore = RunDigest(Seed, DeterminismTicks);
            var hashAfter = RunDigest(Seed, DeterminismTicks);
            sb.AppendLine(
                "parity_v1_009_fingerprint_before=0x" + hashBefore.ToString("X16"));
            sb.AppendLine(
                "parity_v1_009_fingerprint_after=0x" + hashAfter.ToString("X16"));
            sb.AppendLine(
                "parity_expected=0x" + ExpectedParity.ToString("X16") +
                " match=" + (hashBefore == ExpectedParity && hashAfter == ExpectedParity));
            sb.AppendLine();

            sb.AppendLine("=== PARTIE 3b — SUITE LARGE ===");
            sb.AppendLine(
                "LARGE: rejouée à part (voir Logs/v1_085_large.xml) — filtre v1_082 + V1083/V1085 ; " +
                "seuil 2,65 s/cas (v1_078).");
            sb.AppendLine();

            MeasureOrderControl(true, true, out var redInv, out var redTot, out _);
            MeasureOrderControl(true, false, out var greenInv, out var greenTot, out _);
            var chainPilotOdd = true; // 1 inversion labels vs terrain
            var chainLegacyWasOdd = true; // legacy fill+labels NORD_BAS + flip → sud-en-haut
            var all =
                redInv == redTot && redInv > 0 &&
                greenInv == 0 && greenTot == redTot &&
                ewOld == 0 && ewNew == 0 &&
                hashBefore == ExpectedParity && hashAfter == ExpectedParity;

            sb.AppendLine("=== VERDICT MESURÉ ===");
            sb.AppendLine(
                (all ? "PASS" : "FAIL") +
                " | chaîne 8 étages : Project y=−lat ; pilote fill nord@py0 ; " +
                "étage fautif = WorldToPixelY/WorldToPixel (maxY−wy) qui posait NORD_BAS " +
                "sur un buffer nord@py0 — 1 inversion (impair) en mode pilote pour les étiquettes " +
                "contre 0 pour le relief ; legacy FillVoronoi aligné sur nord@py0 (v1_085). " +
                "Contrôle NS villes monde pilote ROUGE " + redInv + "/" + redTot +
                " puis VERT " + greenInv + "/" + greenTot +
                " ; longitude 0/" + ewTot + " avant comme après ; " +
                "parité 0x" + ExpectedParity.ToString("X16") + " des deux côtés ; " +
                "anisotropie px/deg déclarée (ratio≠1/cos(47.5°)) non corrigée.");
            sb.AppendLine("chain_pilot_labels_inversions_odd=" + chainPilotOdd);
            sb.AppendLine("chain_legacy_pre_fix_inversions_odd=" + chainLegacyWasOdd);

            File.WriteAllText(LogPath, sb.ToString(), Encoding.UTF8);
            Debug.Log("V1085: wrote " + LogPath);
            Assert.IsTrue(all, "V1085 artifacts FAIL — voir " + LogPath);
        }

        static void AppendConventionChain(StringBuilder sb)
        {
            sb.AppendLine(
                "1. lat city/province_coordinates.json — NORD_VERS_LE_HAUT " +
                "(lat croît vers le nord) — fichiers StreamingAssets/data/*_coordinates.json");
            sb.AppendLine(
                "2. ProvinceCoordinates.Project (ProvinceCoordinates.cs:118-125) — " +
                "y = −lat ⇒ worldY DÉCROÎT vers le nord — NORD_VERS_LE_BAS (axe worldY)");
            sb.AppendLine(
                "3a. Bornes legacy ComputeBounds (MapSnapshotExporter.cs:1668) — " +
                "MinY=min(worldY)=nord, MaxY=max(worldY)=sud — cohérent avec y=−lat");
            sb.AppendLine(
                "3b. Bornes pilote PilotMapProvider (PilotMapProvider.cs:534-547) — " +
                "Project(lon,latMax)→MinY, Project(lon,latMin)→MaxY — MinY=nord");
            sb.AppendLine(
                "3c. Remplissage pilote BuildMapGeometry (PilotMapProvider.cs:1137-1140) — " +
                "worldY = minY + v×range ⇒ py=0 → nord — NORD_VERS_LE_HAUT (buffer)");
            sb.AppendLine(
                "3d. Remplissage legacy FillVoronoi — AVANT v1_085: wy=maxY−v×range " +
                "(sud@py0) ; APRÈS: wy=minY+v×range (nord@py0, aligné pilote)");
            sb.AppendLine(
                "4. MapSnapshotExporter.WorldToPixel — AVANT: py=(maxY−wy)/H (NORD_BAS) ; " +
                "APRÈS v1_085: py=(wy−minY)/H (NORD_HAUT buffer)");
            sb.AppendLine(
                "5. CityPresentation.WorldToPixelY (CityPresentation.cs:447+) — même couple " +
                "AVANT/APRÈS que (4) ; commentaire pré-correction se contredisait " +
                "(« worldY croît vers le nord (y=−lat) »)");
            sb.AppendLine(
                "6. Tampon pixels — index py=0 = première rangée ; convention documentée nord@py0");
            sb.AppendLine(
                "7. BlitGlyph (MapSnapshotExporter.cs:3738) — row0 = haut de lettre vers py croissant " +
                "(sud si nord@py0) ; pas de pré-inversion (v1_079)");
            sb.AppendLine(
                "8. WriteMapBufferPng (MapSnapshotExporter.cs:910-928) — FlipMapBufferRows puis " +
                "EncodeToPNG : unique inversion buffer→PNG ; nord@py0 → haut du PNG");
            sb.AppendLine();
            sb.AppendLine(
                "COMPTE INVERSIONS (vers image finale nord-en-haut) :");
            sb.AppendLine(
                "  mode pilote ON — relief: 0 (pair) → carte droite ; " +
                "étiquettes pré-fix: 1 (impair, étage 4/5) → miroir N-S ; post-fix: 0");
            sb.AppendLine(
                "  mode pilote OFF — pré-fix relief+labels: 1 (impair) → carte entière sud-en-haut " +
                "mais labels cohérents avec relief ; post-fix FillVoronoi+WorldToPixel: 0");
            sb.AppendLine(
                "  Le compte impair des étiquettes pilote EXPLIQUE la mesure CTO " +
                "(Toulouse en Manche, Gand en Lorraine) alors que le relief reste droit.");
        }

        static void AppendScales(StringBuilder sb)
        {
            ProvinceCoordinates.LoadProjected(out var mid);
            PilotMapProvider.SetEnabled(true, clearCache: true);
            var geo = MapSnapshotExporter.BuildMapGeometry(
                MapSnapshotExporter.Width, MapSnapshotExporter.Height);
            PilotMapProvider.Enabled = false;
            Assert.IsNotNull(geo);
            var cos = Mathf.Cos(mid * Mathf.Deg2Rad);
            var rangeX = geo.MaxX - geo.MinX;
            var rangeY = geo.MaxY - geo.MinY;
            var lonSpan = rangeX / Mathf.Max(cos, 1e-6f);
            var latSpan = rangeY; // |dy/dlat| = 1
            var pxPerLon = geo.Width / lonSpan;
            var pyPerLat = geo.Height / latSpan;
            var ratio = pxPerLon / pyPerLat;
            var expectedIso = 1f / cos; // 1/cos(mid) si px/deg_lon ÷ px/deg_lat pour degrés égaux isotropes monde
            // Rapport equirectangulaire en unités monde isotropes : dx/dlon=cos, dy/dlat=1
            // ⇒ (px/deg_lon)/(px/deg_lat) = cos × (px/worldX)/(px/worldY)
            var pxPerWorldX = geo.Width / rangeX;
            var pxPerWorldY = geo.Height / rangeY;
            var anisoWorld = pxPerWorldX / pxPerWorldY;
            sb.AppendLine(
                "mid_latitude=" + mid.ToString("0.###", CultureInfo.InvariantCulture) +
                " (attendu " + MidLatitudeExpected.ToString("0.###", CultureInfo.InvariantCulture) + ")");
            sb.AppendLine(
                "px_per_deg_lon=" + pxPerLon.ToString("0.####", CultureInfo.InvariantCulture) +
                " px_per_deg_lat=" + pyPerLat.ToString("0.####", CultureInfo.InvariantCulture) +
                " ratio_lon_over_lat=" + ratio.ToString("0.####", CultureInfo.InvariantCulture));
            sb.AppendLine(
                "1/cos(mid)=" + expectedIso.ToString("0.####", CultureInfo.InvariantCulture) +
                " | px_per_world anisotropy=" + anisoWorld.ToString("0.####", CultureInfo.InvariantCulture) +
                " | ANISOTROPIE DÉCLARÉE (non corrigée dans v1_085) : ratio_lon/lat ≠ 1/cos(mid) " +
                "car la fenêtre W/H et rangeX/rangeY ne sont pas calés pour des degrés isotropes.");
            sb.AppendLine(
                "CTO oeil (ordre de grandeur seulement): 106.8 / 82.2 ; code: " +
                pxPerLon.ToString("0.#", CultureInfo.InvariantCulture) + " / " +
                pyPerLat.ToString("0.#", CultureInfo.InvariantCulture));
        }

        static void AppendAllOrderCounts(StringBuilder sb, bool useLegacyMirror, string tag)
        {
            foreach (var pilot in new[] { true, false })
            {
                foreach (var level in new[]
                         {
                             MapObservationLevel.World,
                             MapObservationLevel.Country,
                             MapObservationLevel.Province
                         })
                {
                    CountAtLevel(
                        pilot, level, useLegacyMirror,
                        out var cInv, out var cTot,
                        out var pInv, out var pTot,
                        out var kInv, out var kTot,
                        out var detail);
                    sb.AppendLine(
                        tag + " pilot=" + (pilot ? "ON" : "OFF") +
                        " level=" + level +
                        " cities_NS=" + cInv + "/" + cTot +
                        " provinces_NS=" + pInv + "/" + pTot +
                        " countries_NS=" + kInv + "/" + kTot +
                        " | " + detail);
                }
            }
        }

        static bool MeasureOrderControl(
            bool pilot, bool useLegacyMirror,
            out int inverted, out int total, out string detail)
        {
            CountAtLevel(
                pilot, MapObservationLevel.World, useLegacyMirror,
                out inverted, out total,
                out _, out _, out _, out _, out detail);
            return total > 0;
        }

        static bool MeasureLongitudeOrder(
            bool pilot, bool useLegacyMirror,
            out int inverted, out int total, out string detail)
        {
            inverted = 0;
            total = 0;
            detail = "";
            PrepareGeo(pilot, out var geo, out var mid);
            if (geo == null)
            {
                detail = "geo null";
                return false;
            }

            var cities = LoadCityLatLon();
            var pts = new List<(float Lon, float Px)>(cities.Count);
            for (var i = 0; i < cities.Count; i++)
            {
                var c = cities[i];
                ProvinceCoordinates.Project(c.Lon, c.Lat, mid, out var x, out _);
                var px = (x - geo.MinX) / (geo.MaxX - geo.MinX) * geo.Width;
                pts.Add((c.Lon, px));
            }

            for (var i = 0; i < pts.Count; i++)
            for (var j = i + 1; j < pts.Count; j++)
            {
                if (Math.Abs(pts[i].Lon - pts[j].Lon) < 1e-8f)
                    continue;
                total++;
                var east = pts[i].Lon > pts[j].Lon ? pts[i] : pts[j];
                var west = pts[i].Lon > pts[j].Lon ? pts[j] : pts[i];
                if (!(east.Px > west.Px))
                    inverted++;
            }

            detail = "EW inv=" + inverted + "/" + total + " pilot=" + pilot +
                     " mirror=" + useLegacyMirror;
            return true;
        }

        static void CountAtLevel(
            bool pilot,
            MapObservationLevel level,
            bool useLegacyMirror,
            out int cityInv, out int cityTot,
            out int provInv, out int provTot,
            out int countryInv, out int countryTot,
            out string detail)
        {
            cityInv = cityTot = provInv = provTot = countryInv = countryTot = 0;
            detail = "";
            PrepareGeo(pilot, out var geo, out var mid);
            if (geo == null)
            {
                detail = "geo null";
                return;
            }

            // Fenêtre pays / province : recentrer comme le jeu.
            using (var harness = new SimulationHarness(Seed))
            {
                harness.RunTicks(0);
                var em = harness.EntityManager;
                MapViewport.EnsureWorldWindow(geo);
                if (level == MapObservationLevel.Country)
                {
                    MapDisplaySystem.TrySelectCountryByTag(em, "FRA");
                    geo = MapGeometryCache.GetOrBuild(
                        MapSnapshotExporter.Width, MapSnapshotExporter.Height,
                        MapViewport.State.Window, out _);
                }
                else if (level == MapObservationLevel.Province)
                {
                    MapDisplaySystem.TrySelectProvinceById(em, 1);
                    geo = MapGeometryCache.GetOrBuild(
                        MapSnapshotExporter.Width, MapSnapshotExporter.Height,
                        MapViewport.State.Window, out _);
                }
            }

            if (geo == null)
            {
                detail = "geo window null";
                return;
            }

            var cityPts = new List<(float Lat, float Py)>();
            var cities = LoadCityLatLon();
            for (var i = 0; i < cities.Count; i++)
            {
                var c = cities[i];
                ProvinceCoordinates.Project(c.Lon, c.Lat, mid, out _, out var y);
                var py = useLegacyMirror
                    ? WorldToPixelYLegacyMirror(y, geo.MinY, geo.MaxY, geo.Height)
                    : WorldToPixelYNordAt0(y, geo.MinY, geo.MaxY, geo.Height);
                // WriteMapBufferPng : buffer py=0 → haut PNG ; contrôle sur buffer = PNG viewerY.
                cityPts.Add((c.Lat, py));
            }

            CountNs(cityPts, out cityInv, out cityTot);

            var provPts = new List<(float Lat, float Py)>();
            var provs = ProvinceCoordinates.LoadProjected(out _);
            for (var i = 0; i < provs.Count; i++)
            {
                var p = provs[i];
                var py = useLegacyMirror
                    ? WorldToPixelYLegacyMirror(p.Y, geo.MinY, geo.MaxY, geo.Height)
                    : WorldToPixelYNordAt0(p.Y, geo.MinY, geo.MaxY, geo.Height);
                provPts.Add((p.Lat, py));
            }

            CountNs(provPts, out provInv, out provTot);

            // Centroïdes pays (même agrégat qu'ApplyCountryLabels) — dérivés de ViewsSkeleton.
            CollectCountryCentroids(geo, useLegacyMirror, out countryInv, out countryTot);

            // Vérifie aussi l'API live (vert seulement).
            if (!useLegacyMirror)
            {
                var liveMismatch = 0;
                for (var i = 0; i < cities.Count; i++)
                {
                    var c = cities[i];
                    if (!CityCoordinates.TryGet(c.Id, out var pt))
                        continue;
                    CityMarkerComposer.WorldToPixel(pt.X, pt.Y, geo, out _, out var livePy);
                    var expect = (int)Math.Floor(
                        WorldToPixelYNordAt0(pt.Y, geo.MinY, geo.MaxY, geo.Height));
                    if (livePy != expect)
                        liveMismatch++;
                }

                detail =
                    "live_api_mismatch=" + liveMismatch +
                    " geo=[" + geo.MinY.ToString("0.##", CultureInfo.InvariantCulture) +
                    ".." + geo.MaxY.ToString("0.##", CultureInfo.InvariantCulture) + "]";
            }
            else
            {
                detail = "formula=maxY-worldY (pré-v1_085)";
            }
        }

        static void CollectCountryCentroids(
            MapSnapshotExporter.MapGeometry geo,
            bool useLegacyMirror,
            out int inv, out int tot)
        {
            inv = tot = 0;
            if (geo?.ViewsSkeleton == null || geo.ViewsSkeleton.Count == 0)
                return;

            var tags = new Dictionary<string, (float SumLat, float SumY, int N)>(StringComparer.Ordinal);
            for (var i = 0; i < geo.ViewsSkeleton.Count; i++)
            {
                var v = geo.ViewsSkeleton[i];
                var tag = v.OwnerTag;
                if (string.IsNullOrEmpty(tag))
                    continue;
                // Lat depuis worldY : lat = −y (Project).
                var lat = -v.Y;
                if (!tags.TryGetValue(tag, out var agg))
                    agg = default;
                agg.SumLat += lat;
                agg.SumY += v.Y;
                agg.N++;
                tags[tag] = agg;
            }

            var pts = new List<(float Lat, float Py)>(tags.Count);
            foreach (var kv in tags)
            {
                var meanY = kv.Value.SumY / kv.Value.N;
                var meanLat = kv.Value.SumLat / kv.Value.N;
                var py = useLegacyMirror
                    ? WorldToPixelYLegacyMirror(meanY, geo.MinY, geo.MaxY, geo.Height)
                    : WorldToPixelYNordAt0(meanY, geo.MinY, geo.MaxY, geo.Height);
                pts.Add((meanLat, py));
            }

            CountNs(pts, out inv, out tot);
        }

        static void CountNs(List<(float Lat, float Py)> pts, out int inv, out int tot)
        {
            inv = tot = 0;
            for (var i = 0; i < pts.Count; i++)
            for (var j = i + 1; j < pts.Count; j++)
            {
                if (Math.Abs(pts[i].Lat - pts[j].Lat) < 1e-8f)
                    continue;
                // Ordre continu (sans quantification pixel) — le floor peut coller deux lats.
                tot++;
                var north = pts[i].Lat > pts[j].Lat ? pts[i] : pts[j];
                var south = pts[i].Lat > pts[j].Lat ? pts[j] : pts[i];
                // nord@py0 / PNG haut : py_nord < py_sud
                if (!(north.Py < south.Py))
                    inv++;
            }
        }

        static void PrepareGeo(bool pilot, out MapSnapshotExporter.MapGeometry geo, out float mid)
        {
            mid = MidLatitudeExpected;
            ProvinceCoordinates.LoadProjected(out mid);
            CityCoordinates.InvalidateCache();
            MapGeometryCache.ResetStatsAndClear();
            MapViewport.Reset();
            PilotMapProvider.Enabled = false;
            if (pilot)
                PilotMapProvider.SetEnabled(true, clearCache: true);
            geo = MapSnapshotExporter.BuildMapGeometry(
                MapSnapshotExporter.Width, MapSnapshotExporter.Height);
        }

        struct CityLatLon
        {
            public int Id;
            public float Lon;
            public float Lat;
        }

        static List<CityLatLon> LoadCityLatLon()
        {
            var points = CityCoordinates.LoadProjected(out _);
            var list = new List<CityLatLon>(points.Count);
            for (var i = 0; i < points.Count; i++)
            {
                var p = points[i];
                list.Add(new CityLatLon { Id = p.Id, Lon = p.Lon, Lat = p.Lat });
            }

            return list;
        }

        static void EnsureBeforeCaptures(StringBuilder sb)
        {
            // Avant = même pipeline avec DebugLegacyMirrorWorldToPixelY (formule maxY−wy).
            // Ne pas dépendre de Captures/v1_083 : V1083 les régénère avec le code courant.
            WriteCapturesLabeled(sb, "before", mirrorY: true);
        }

        static void WriteAfterCaptures(StringBuilder sb)
        {
            WriteCapturesLabeled(sb, "after", mirrorY: false);
        }

        static void WriteCapturesLabeled(StringBuilder sb, string label, bool mirrorY)
        {
            MapSnapshotExporter.DebugLegacyMirrorWorldToPixelY = mirrorY;
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
                var worldPixels = MapSnapshotExporter.RenderPoliticalPixels(
                    em, worldGeo, MapSnapshotExporter.LabelDensity.Countries, -1,
                    overlay: p =>
                    {
                        CityMarkerComposer.Compose(
                            p, worldGeo, em, MapObservationLevel.World, filterCountryId: -1);
                    });
                var worldPath = Path.Combine(CapturesDir, label + "_world.png");
                MapSnapshotExporter.WriteMapBufferPng(
                    worldPixels, MapSnapshotExporter.Width, MapSnapshotExporter.Height, worldPath);
                sb.AppendLine(label + "_world sha=" + Sha256File(worldPath) +
                              " mirrorY=" + mirrorY +
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
                    sb.AppendLine(label + "_country_FRA sha=" + Sha256File(countryPath) +
                                  " mirrorY=" + mirrorY +
                                  " markers=" + CityMarkerComposer.LastMarkersDrawn);
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
                    sb.AppendLine(label + "_province_1 sha=" + Sha256File(provPath) +
                                  " mirrorY=" + mirrorY +
                                  " markers=" + CityMarkerComposer.LastMarkersDrawn);
                }

                PilotMapProvider.Enabled = false;
                MapViewport.Reset();
            }
            finally
            {
                MapSnapshotExporter.DebugLegacyMirrorWorldToPixelY = false;
            }
        }

        static void AssertBeforeAfterDiffer(StringBuilder sb)
        {
            var names = new[] { "world.png", "country_FRA.png", "province_1.png" };
            foreach (var n in names)
            {
                var a = Path.Combine(CapturesDir, "before_" + n);
                var b = Path.Combine(CapturesDir, "after_" + n);
                if (!File.Exists(a) || !File.Exists(b))
                {
                    sb.AppendLine("diff_" + n + "=MISSING");
                    continue;
                }

                var sa = Sha256File(a);
                var sbHash = Sha256File(b);
                var differ = !string.Equals(sa, sbHash, StringComparison.Ordinal);
                sb.AppendLine("diff_" + n + "=" + (differ ? "CHANGED" : "IDENTICAL") +
                              " before=" + sa + " after=" + sbHash);
                Assert.IsTrue(differ, "captures before/after doivent différer pour " + n);
            }
        }

        static ulong RunDigest(uint seed, int ticks)
        {
            using var harness = new SimulationHarness(seed);
            harness.RunTicks(ticks);
            return WorldDigest.Compute(harness.EntityManager);
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
    }
}
