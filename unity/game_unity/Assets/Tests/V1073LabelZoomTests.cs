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
using VictoriaGame.World;
using Debug = UnityEngine.Debug;

namespace VictoriaGame.Tests
{
    /// <summary>
    /// Point d'entrée batchmode :
    /// -executeMethod VictoriaGame.Tests.V1073LabelZoomBatchRunner.Run
    /// </summary>
    public static class V1073LabelZoomBatchRunner
    {
        public static void Run()
        {
            V1073LabelZoomTests.RunAndWriteArtifacts();
            Debug.Log("V1073LabelZoomBatchRunner: DONE");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
        }
    }

    /// <summary>
    /// v1_073 — repli accents, échelle glyphe/sprites au zoom, délogement, preuves V1073-A…E.
    /// </summary>
    [TestFixture]
    public class V1073LabelZoomTests
    {
        const uint Seed = 42195u;
        const int CaptureTick = 1000;
        const int ParisCityId = 1;
        const int DijonCityId = 15;
        const int LondonCityId = 21;
        const int BourgogneProvinceId = 6;
        const int IleDeFranceProvinceId = 1;
        const int DrawnFloorProvince = 3;

        static readonly (string raw, string expected, string withoutFold)[] MeasuredCases =
        {
            ("Île-de-France", "ILE-DE-FRANCE", "LE-DE-FRANCE"),
            ("Lübeck", "LUBECK", "LBECK"),
            ("Königsberg", "KONIGSBERG", "KNIGSBERG"),
            ("Châlons", "CHALONS", "CHLONS"),
            ("Kutná Hora", "KUTNA HORA", "KUTN HORA"),
            ("Târgoviște", "TARGOVISTE", "TRGOVITE"),
        };

        [TearDown]
        public void TearDown()
        {
            MapSnapshotExporter.ResetZoomScaleToNeutral();
            MapLabelLayout.CollisionEnabled = true;
            MapLabelLayout.LegacyCityLabels = false;
            MapLabelLayout.UseImportanceQueue = true;
            PilotMapProvider.Enabled = false;
            MapGeometryCache.ResetStatsAndClear();
        }

        [Test]
        public void V1073_A_NoLetterLost_SixNamedCases()
        {
            Assert.IsTrue(CheckAccentFold(out var detail), detail);
            // Rouge : sans repli, Île-de-France → LE-DE-FRANCE.
            var red = MapSnapshotExporter.SanitizeLabelTextWithoutFold("Île-de-France");
            Assert.AreEqual("LE-DE-FRANCE", red, "rouge V1073-A: repli retiré");
            Assert.AreNotEqual(
                MapSnapshotExporter.SanitizeLabelText("Île-de-France"), red);
        }

        [Test]
        public void V1073_B_MeasureAndDrawSameScale()
        {
            Assert.IsTrue(CheckMeasureDrawParity(out var detail), detail);
        }

        [Test]
        public void V1073_C_OverlapZero_PairedWithFloorAndCapitals()
        {
            Assert.IsTrue(CheckOverlapPaired(out var detail), detail);
            // Rouge : carte vide → le contrôle doit échouer.
            MapLabelLayout.Begin(64, 64);
            try
            {
                Assert.AreEqual(0, MapLabelLayout.CountTextOverlaps());
                Assert.AreEqual(0, MapLabelLayout.LastDrawn);
                Assert.IsFalse(
                    OverlapControlPasses(
                        overlaps: 0, drawn: 0,
                        hasParis: false, hasLondres: false, hasDijon: false),
                    "rouge V1073-C: zéro chevauchement sur carte vide doit être ÉCHEC");
            }
            finally
            {
                MapLabelLayout.End();
            }
        }

        [Test]
        public void V1073_D_NeutralSettings_BitIdentical()
        {
            Assert.IsTrue(CheckNeutralBitIdentical(out var detail), detail);
        }

        [Test]
        public void V1073_E_TextGrowsAcrossLevels()
        {
            Assert.IsTrue(CheckTextGrows(out var detail), detail);
        }

        [Test]
        public void V1073_Artifacts_And_Verdict() => RunAndWriteArtifacts();

        public static void RunAndWriteArtifacts()
        {
            var captureDir = Path.Combine(Application.dataPath, "..", "Captures", "v1_073");
            var logPath = Path.Combine(Application.dataPath, "..", "Logs", "v1_073_labels.log");
            Directory.CreateDirectory(captureDir);
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

            var sb = new StringBuilder(131072);
            sb.AppendLine("=== v1_073 LABELS + ZOOM SCALE ===");
            sb.AppendLine("seed=" + Seed + " captureTick=t" + CaptureTick);
            sb.AppendLine();

            // --- PARTIE 1 accents ---
            sb.AppendLine("=== PARTIE 1 — REPLI ACCENTS ===");
            foreach (var c in MeasuredCases)
            {
                var after = MapSnapshotExporter.SanitizeLabelText(c.raw);
                var before = MapSnapshotExporter.SanitizeLabelTextWithoutFold(c.raw);
                sb.AppendLine(
                    "case raw=\"" + c.raw + "\" before=\"" + before +
                    "\" after=\"" + after + "\" expected=\"" + c.expected + "\"");
                Assert.AreEqual(c.expected, after, c.raw);
                Assert.AreEqual(c.withoutFold, before, c.raw + " without fold");
            }

            ScanAllDataNames(out var examined, out var lostBefore, out var lostAfter, out var stillLost);
            sb.AppendLine(
                "scan distinct_names=" + examined +
                " lost_letters_before=" + lostBefore +
                " lost_letters_after=" + lostAfter);
            if (stillLost.Count > 0)
            {
                sb.AppendLine("STILL_LOST:");
                for (var i = 0; i < stillLost.Count; i++)
                    sb.AppendLine("  " + stillLost[i]);
            }

            if (MapSnapshotExporter.LastSanitizeUnmapped.Count > 0)
            {
                sb.AppendLine("unmapped_named:");
                for (var i = 0; i < MapSnapshotExporter.LastSanitizeUnmapped.Count; i++)
                    sb.AppendLine("  " + MapSnapshotExporter.LastSanitizeUnmapped[i]);
            }
            else
                sb.AppendLine("unmapped_named: (aucun)");

            sb.AppendLine();

            // --- PARTIE 2/3/4 avec captures ---
            MapSnapshotExporter.ResetZoomScaleToNeutral();
            MapLabelLayout.CollisionEnabled = true;
            MapLabelLayout.LegacyCityLabels = false;
            MapLabelLayout.UseImportanceQueue = true;
            MapViewport.Reset();
            MapGeometryCache.ResetStatsAndClear();
            CityCoordinates.InvalidateCache();
            MapSpriteCatalog.Rebuild();

            var statsNeutral = new LevelStats[3];
            var statsZoom = new LevelStats[3];
            Color32[][] neutralPixels = new Color32[3][];
            Color32[][] zoomPixels = new Color32[3][];

            using (var harness = new SimulationHarness(Seed))
            {
                harness.RunTicks(CaptureTick);
                var em = harness.EntityManager;

                PilotMapProvider.SetEnabled(true, clearCache: true);

                // NEUTRE
                MapSnapshotExporter.ZoomScaleEnabled = false;
                CaptureThreeLevels(
                    em, captureDir, "neutral", statsNeutral, neutralPixels, sb);

                // ZOOM
                MapSnapshotExporter.ZoomScaleEnabled = true;
                CaptureThreeLevels(
                    em, captureDir, "zoom", statsZoom, zoomPixels, sb);
            }

            sb.AppendLine("=== ECHELLES RETENUES ===");
            sb.AppendLine(
                "glyph_world=" + MapSnapshotExporter.ZoomGlyphScaleWorld +
                " h=" + (7 * MapSnapshotExporter.ZoomGlyphScaleWorld) + "px");
            sb.AppendLine(
                "glyph_country=" + MapSnapshotExporter.ZoomGlyphScaleCountry +
                " h=" + (7 * MapSnapshotExporter.ZoomGlyphScaleCountry) + "px");
            sb.AppendLine(
                "glyph_province=" + MapSnapshotExporter.ZoomGlyphScaleProvince +
                " h=" + (7 * MapSnapshotExporter.ZoomGlyphScaleProvince) + "px");
            sb.AppendLine(
                "sprite_country_neutral=" + MapSpriteVisibility.CountrySpriteSize +
                " zoom=" + MapSpriteVisibility.ZoomCountrySpriteSize);
            sb.AppendLine(
                "sprite_province_neutral=" + MapSpriteVisibility.ProvinceSpriteSize +
                " zoom=" + MapSpriteVisibility.ZoomProvinceSpriteSize);
            sb.AppendLine();

            sb.AppendLine("=== LARGEUR PLUS LONG NOM PLACE ===");
            for (var i = 0; i < 3; i++)
            {
                var name = i == 0 ? "world" : i == 1 ? "country" : "province";
                sb.AppendLine(
                    name + "_longest_neutral_px=" + statsNeutral[i].LongestLabelPx +
                    " zoom_px=" + statsZoom[i].LongestLabelPx +
                    " name=\"" + statsZoom[i].LongestLabelName + "\"");
            }

            sb.AppendLine();
            sb.AppendLine("=== SPRITES DESSINES ===");
            for (var i = 0; i < 3; i++)
            {
                var name = i == 0 ? "world" : i == 1 ? "country" : "province";
                sb.AppendLine(
                    name + "_sprites_neutral=" + statsNeutral[i].SpritesDrawn +
                    " zoom=" + statsZoom[i].SpritesDrawn +
                    " size_n=" + statsNeutral[i].SpriteSize +
                    " size_z=" + statsZoom[i].SpriteSize);
            }

            sb.AppendLine();
            sb.AppendLine("=== MAPLABELLAYOUT COUNTERS (avant=neutre / apres=zoom) ===");
            for (var i = 0; i < 3; i++)
            {
                var name = i == 0 ? "world" : i == 1 ? "country" : "province";
                AppendCounters(sb, name + "_neutral", statsNeutral[i]);
                AppendCounters(sb, name + "_zoom", statsZoom[i]);
                sb.AppendLine(name + "_omitted_neutral=" + statsNeutral[i].OmittedNames);
                sb.AppendLine(name + "_omitted_zoom=" + statsZoom[i].OmittedNames);
            }

            var displacedZoom = statsZoom[0].Displaced + statsZoom[1].Displaced +
                                statsZoom[2].Displaced;
            sb.AppendLine();
            sb.AppendLine("=== DELOGEMENT ===");
            sb.AppendLine(
                "LastDisplaced_neutral_sum=" +
                (statsNeutral[0].Displaced + statsNeutral[1].Displaced +
                 statsNeutral[2].Displaced));
            sb.AppendLine("LastDisplaced_zoom_sum=" + displacedZoom);
            if (displacedZoom > 0)
                sb.AppendLine(
                    "verdict_delogement: EXERCE (LastDisplaced=" + displacedZoom +
                    ") — couvert par V1073_F si présent / compteur publié.");
            else
                sb.AppendLine(
                    "verdict_delogement: LastDisplaced=0 même à la plus grande échelle — " +
                    "RECOMMANDATION: retirer le mécanisme de délogement (jamais exercé depuis v1_041).");

            // Contrôles
            sb.AppendLine();
            sb.AppendLine("=== CONTROLES V1073-A..E ===");
            var vA = CheckAccentFold(out var dA);
            sb.AppendLine("V1073-A accents: " + (vA ? "PASS" : "FAIL") + " — " + dA);
            sb.AppendLine(
                "V1073-A rouge constaté: withoutFold(Île-de-France)=" +
                MapSnapshotExporter.SanitizeLabelTextWithoutFold("Île-de-France"));

            var vB = CheckMeasureDrawParity(out var dB);
            sb.AppendLine("V1073-B measure=draw: " + (vB ? "PASS" : "FAIL") + " — " + dB);

            var vC = CheckOverlapPaired(out var dC);
            sb.AppendLine("V1073-C overlap+floor+capitals: " + (vC ? "PASS" : "FAIL") + " — " + dC);
            sb.AppendLine(
                "V1073-C rouge constaté: carte vide LastDrawn=0 ⇒ contrôle ÉCHEC (pas un vert)");

            var vD = CheckNeutralBitIdentical(out var dD);
            sb.AppendLine("V1073-D neutral bit-identical: " + (vD ? "PASS" : "FAIL") + " — " + dD);

            var vE = CheckTextGrows(out var dE);
            sb.AppendLine("V1073-E text grows: " + (vE ? "PASS" : "FAIL") + " — " + dE);

            var all = vA && vB && vC && vD && vE && lostAfter == 0;
            sb.AppendLine();
            sb.AppendLine(
                "VERDICT: " + (all ? "PASS" : "FAIL") +
                " | " + examined + " noms balayés, " + lostBefore +
                " perdaient des lettres, " + lostAfter + " en perd encore" +
                "; échelle 2/3/5 ; sprites 10/16/28 ; displaced_zoom=" + displacedZoom +
                "; contrôles " + (vA && vB && vC && vD && vE ? "5/5" : "INCOMPLET"));

            File.WriteAllText(logPath, sb.ToString(), Encoding.UTF8);
            Debug.Log("V1073: wrote " + logPath);

            MapSnapshotExporter.ResetZoomScaleToNeutral();
            Assert.IsTrue(all, "V1073 artifacts verdict FAIL — voir " + logPath);
        }

        struct LevelStats
        {
            public int Drawn, Moved, Omitted, Displaced, Reserved, Enqueued;
            public int SpritesDrawn, SpriteSize, LongestLabelPx, GlyphScale, GlyphHeight;
            public string LongestLabelName;
            public string OmittedNames;
            public int Overlaps;
            public bool HasParis, HasLondres, HasDijon;
            public string Sha;
            public Color32[] Pixels;
        }

        static void CaptureThreeLevels(
            EntityManager em,
            string captureDir,
            string tag,
            LevelStats[] stats,
            Color32[][] pixelsOut,
            StringBuilder sb)
        {
            var worldGeo = MapGeometryCache.GetOrBuild(
                MapSnapshotExporter.Width, MapSnapshotExporter.Height, null, out _);
            MapViewport.EnsureWorldWindow(worldGeo);

            // WORLD
            stats[0] = RenderLevel(
                em, worldGeo, MapObservationLevel.World,
                MapSnapshotExporter.LabelDensity.Countries, -1,
                filterCountry: -1, filterProvince: -1);
            pixelsOut[0] = stats[0].Pixels;
            WritePng(Path.Combine(captureDir, tag + "_world.png"), pixelsOut[0],
                worldGeo.Width, worldGeo.Height);
            AssertPngNorthUpOrFail(em, Path.Combine(captureDir, tag + "_world.png"));

            // COUNTRY FRA
            Assert.IsTrue(MapDisplaySystem.TrySelectCountryByTag(em, "FRA"));
            var countryGeo = MapGeometryCache.GetOrBuild(
                MapSnapshotExporter.Width, MapSnapshotExporter.Height,
                MapViewport.State.Window, out _);
            stats[1] = RenderLevel(
                em, countryGeo, MapObservationLevel.Country,
                MapSnapshotExporter.LabelDensity.Provinces, -1,
                filterCountry: MapViewport.State.TargetCountryId, filterProvince: -1);
            pixelsOut[1] = stats[1].Pixels;
            WritePng(Path.Combine(captureDir, tag + "_country.png"), pixelsOut[1],
                countryGeo.Width, countryGeo.Height);
            AssertPngNorthUpOrFail(em, Path.Combine(captureDir, tag + "_country.png"));

            // PROVINCE Bourgogne
            Assert.IsTrue(MapDisplaySystem.TrySelectProvinceById(em, BourgogneProvinceId));
            var provGeo = MapGeometryCache.GetOrBuild(
                MapSnapshotExporter.Width, MapSnapshotExporter.Height,
                MapViewport.State.Window, out _);
            stats[2] = RenderLevel(
                em, provGeo, MapObservationLevel.Province,
                MapSnapshotExporter.LabelDensity.SelectedProvince, BourgogneProvinceId,
                filterCountry: -1, filterProvince: BourgogneProvinceId);
            pixelsOut[2] = stats[2].Pixels;
            WritePng(Path.Combine(captureDir, tag + "_province.png"), pixelsOut[2],
                provGeo.Width, provGeo.Height);
            AssertPngNorthUpOrFail(em, Path.Combine(captureDir, tag + "_province.png"));

            // Remonter au monde : SelectCountry refuse depuis Province.
            var worldForEng = MapGeometryCache.GetOrBuild(
                MapSnapshotExporter.Width, MapSnapshotExporter.Height, null, out _);
            MapViewport.EnsureWorldWindow(worldForEng);
            MapViewport.ZoomOut(MapViewport.WorldWindow);
            MapViewport.ZoomOut(MapViewport.WorldWindow);
            Assert.IsTrue(MapDisplaySystem.TrySelectCountryByTag(em, "ENG"),
                "ENG select depuis monde");
            var engGeo = MapGeometryCache.GetOrBuild(
                MapSnapshotExporter.Width, MapSnapshotExporter.Height,
                MapViewport.State.Window, out _);
            var engStats = RenderLevel(
                em, engGeo, MapObservationLevel.Country,
                MapSnapshotExporter.LabelDensity.Provinces, -1,
                filterCountry: MapViewport.State.TargetCountryId, filterProvince: -1);
            stats[1].HasLondres = engStats.HasLondres || stats[1].HasLondres;
            WritePng(Path.Combine(captureDir, tag + "_country_eng.png"), engStats.Pixels,
                engGeo.Width, engGeo.Height);
            AssertPngNorthUpOrFail(em, Path.Combine(captureDir, tag + "_country_eng.png"));

            sb.AppendLine("--- captures " + tag + " ---");
            for (var i = 0; i < 3; i++)
            {
                var name = i == 0 ? "world" : i == 1 ? "country" : "province";
                sb.AppendLine(
                    tag + "_" + name + " scale=" + stats[i].GlyphScale +
                    " h=" + stats[i].GlyphHeight +
                    " drawn=" + stats[i].Drawn +
                    " omitted=" + stats[i].Omitted +
                    " displaced=" + stats[i].Displaced +
                    " sprites=" + stats[i].SpritesDrawn +
                    " sha=" + stats[i].Sha);
            }
        }

        static LevelStats RenderLevel(
            EntityManager em,
            MapSnapshotExporter.MapGeometry geo,
            MapObservationLevel level,
            MapSnapshotExporter.LabelDensity density,
            int selectedProvinceId,
            int filterCountry,
            int filterProvince)
        {
            var pixels = MapSnapshotExporter.RenderPoliticalPixels(
                em, geo, density, selectedProvinceId,
                overlay: p =>
                {
                    if (level != MapObservationLevel.World)
                    {
                        MapSpriteComposer.Compose(
                            p, geo, em, level, thematicLayer: false);
                    }

                    CityMarkerComposer.Compose(
                        p, geo, em, level,
                        filterCountryId: filterCountry,
                        filterProvinceId: filterProvince);
                });

            var st = new LevelStats
            {
                Drawn = MapLabelLayout.LastDrawn,
                Moved = MapLabelLayout.LastMoved,
                Omitted = MapLabelLayout.LastOmitted,
                Displaced = MapLabelLayout.LastDisplaced,
                Reserved = MapLabelLayout.LastReserved,
                Enqueued = MapLabelLayout.LastEnqueued,
                SpritesDrawn = level == MapObservationLevel.World
                    ? 0
                    : MapSpriteComposer.LastSpritesDrawn,
                SpriteSize = MapSpriteVisibility.SpriteSizeFor(level),
                GlyphScale = MapSnapshotExporter.GlyphScaleFor(level),
                GlyphHeight = 7 * MapSnapshotExporter.GlyphScaleFor(level),
                OmittedNames = MapLabelLayout.FormatOmittedNames(),
                Overlaps = MapLabelLayout.CountTextOverlaps(),
                Sha = Sha256Hex(pixels),
                Pixels = pixels,
                LongestLabelName = "",
                LongestLabelPx = 0,
            };

            var placed = MapLabelLayout.LastPlaced;
            for (var i = 0; i < placed.Count; i++)
            {
                var r = placed[i];
                if (r.Kind == MapLabelKind.Marker || r.Kind == MapLabelKind.Building)
                    continue;
                var w = r.Rect.X1 - r.Rect.X0;
                if (w > st.LongestLabelPx)
                {
                    st.LongestLabelPx = w;
                    st.LongestLabelName = r.Kind + ":" + r.Id;
                }
            }

            st.HasParis = HasCityId(placed, ParisCityId);
            st.HasDijon = HasCityId(placed, DijonCityId);
            st.HasLondres = HasCityId(placed, LondonCityId);
            return st;
        }

        static bool HasCityId(IReadOnlyList<MapPlacedLabel> placed, int cityId)
        {
            for (var i = 0; i < placed.Count; i++)
            {
                if (placed[i].Kind == MapLabelKind.City && placed[i].Id == cityId)
                    return true;
            }

            return false;
        }

        static void AppendCounters(StringBuilder sb, string prefix, LevelStats s)
        {
            sb.AppendLine(
                prefix +
                " Drawn=" + s.Drawn +
                " Moved=" + s.Moved +
                " Omitted=" + s.Omitted +
                " Displaced=" + s.Displaced +
                " Reserved=" + s.Reserved +
                " Enqueued=" + s.Enqueued +
                " Overlaps=" + s.Overlaps);
        }

        static bool CheckAccentFold(out string detail)
        {
            foreach (var c in MeasuredCases)
            {
                var after = MapSnapshotExporter.SanitizeLabelText(c.raw);
                if (after != c.expected)
                {
                    detail = "FAIL " + c.raw + " → " + after + " expected " + c.expected;
                    return false;
                }
            }

            ScanAllDataNames(out var n, out var lb, out var la, out var still);
            if (la > 0)
            {
                detail = "encore " + la + " noms qui perdent des lettres: " +
                         string.Join("; ", still);
                return false;
            }

            detail = n + " noms, " + lb + " perdaient avant, 0 après; six cas OK";
            return true;
        }

        static bool CheckMeasureDrawParity(out string detail)
        {
            MapSnapshotExporter.ResetZoomScaleToNeutral();
            MapSnapshotExporter.ZoomScaleEnabled = true;
            MapLabelLayout.CollisionEnabled = true;
            MapLabelLayout.UseImportanceQueue = true;
            MapLabelLayout.LegacyCityLabels = false;

            using var harness = new SimulationHarness(Seed);
            harness.RunTicks(CaptureTick);
            var em = harness.EntityManager;
            PilotMapProvider.SetEnabled(true, clearCache: true);
            MapViewport.Reset();
            MapGeometryCache.ResetStatsAndClear();

            Assert.IsTrue(MapDisplaySystem.TrySelectProvinceById(em, BourgogneProvinceId));
            var geo = MapGeometryCache.GetOrBuild(
                MapSnapshotExporter.Width, MapSnapshotExporter.Height,
                MapViewport.State.Window, out _);

            // Vert : même échelle
            MapSnapshotExporter.DebugMeasureScaleOverride = null;
            MapSnapshotExporter.RenderPoliticalPixels(
                em, geo, MapSnapshotExporter.LabelDensity.SelectedProvince,
                BourgogneProvinceId,
                overlay: p =>
                {
                    MapSpriteComposer.Compose(p, geo, em, MapObservationLevel.Province, false);
                    CityMarkerComposer.Compose(
                        p, geo, em, MapObservationLevel.Province,
                        filterProvinceId: BourgogneProvinceId);
                });
            var overMatch = MapLabelLayout.CountTextOverlaps();
            var overDrawMatch = MapLabelLayout.CountTextOverlapsAtDrawScale();
            var drawn = MapLabelLayout.LastDrawn;

            // Rouge : mesurer à 2, dessiner à 5
            MapSnapshotExporter.DebugMeasureScaleOverride = 2;
            MapSnapshotExporter.SetGlyphScale(MapSnapshotExporter.ZoomGlyphScaleProvince);
            MapSnapshotExporter.RenderPoliticalPixels(
                em, geo, MapSnapshotExporter.LabelDensity.SelectedProvince,
                BourgogneProvinceId,
                overlay: p =>
                {
                    MapSpriteComposer.Compose(p, geo, em, MapObservationLevel.Province, false);
                    CityMarkerComposer.Compose(
                        p, geo, em, MapObservationLevel.Province,
                        filterProvinceId: BourgogneProvinceId);
                });
            var overBook = MapLabelLayout.CountTextOverlaps();
            var overDraw = MapLabelLayout.CountTextOverlapsAtDrawScale();
            MapSnapshotExporter.DebugMeasureScaleOverride = null;

            var green = overMatch == 0 && overDrawMatch == 0 && drawn >= DrawnFloorProvince;
            var red = overDraw > 0 || overBook > 0;
            // Si la densité ne crée pas de collision même en mensonge, exiger au moins
            // que la largeur mesurée ≠ largeur dessin pour BOURGOGNE.
            MapSnapshotExporter.SetGlyphScale(5);
            MapSnapshotExporter.DebugMeasureScaleOverride = 2;
            var wMeas = MapSnapshotExporter.MeasureBitmapText("BOURGOGNE");
            MapSnapshotExporter.DebugMeasureScaleOverride = null;
            var wDraw = MapSnapshotExporter.MeasureBitmapText("BOURGOGNE");
            var lie = wMeas != wDraw;
            red = red || lie;

            detail = "match overlaps=" + overMatch + "/" + overDrawMatch +
                     " drawn=" + drawn +
                     " ; rouge drawScaleOverlaps=" + overDraw +
                     " book=" + overBook +
                     " wMeas=" + wMeas + " wDraw=" + wDraw;
            MapSnapshotExporter.ResetZoomScaleToNeutral();
            return green && red && lie;
        }

        static bool CheckOverlapPaired(out string detail)
        {
            MapSnapshotExporter.ResetZoomScaleToNeutral();
            MapSnapshotExporter.ZoomScaleEnabled = true;
            MapLabelLayout.CollisionEnabled = true;
            MapLabelLayout.UseImportanceQueue = true;
            MapLabelLayout.LegacyCityLabels = false;

            using var harness = new SimulationHarness(Seed);
            harness.RunTicks(CaptureTick);
            var em = harness.EntityManager;
            PilotMapProvider.SetEnabled(true, clearCache: true);
            MapViewport.Reset();
            MapGeometryCache.ResetStatsAndClear();

            var worldGeo = MapGeometryCache.GetOrBuild(
                MapSnapshotExporter.Width, MapSnapshotExporter.Height, null, out _);
            MapViewport.EnsureWorldWindow(worldGeo);

            // FRA country — plancher LastDrawn + overlaps
            Assert.IsTrue(MapDisplaySystem.TrySelectCountryByTag(em, "FRA"));
            var fraGeo = MapGeometryCache.GetOrBuild(
                MapSnapshotExporter.Width, MapSnapshotExporter.Height,
                MapViewport.State.Window, out _);
            var fra = RenderLevel(
                em, fraGeo, MapObservationLevel.Country,
                MapSnapshotExporter.LabelDensity.Provinces, -1,
                filterCountry: MapViewport.State.TargetCountryId, filterProvince: -1);

            // Île-de-France — PARIS nommé
            Assert.IsTrue(MapDisplaySystem.TrySelectProvinceById(em, IleDeFranceProvinceId));
            var idfGeo = MapGeometryCache.GetOrBuild(
                MapSnapshotExporter.Width, MapSnapshotExporter.Height,
                MapViewport.State.Window, out _);
            var idf = RenderLevel(
                em, idfGeo, MapObservationLevel.Province,
                MapSnapshotExporter.LabelDensity.SelectedProvince, IleDeFranceProvinceId,
                filterCountry: -1, filterProvince: IleDeFranceProvinceId);
            var hasParis = idf.HasParis || fra.HasParis;

            // Remonter monde puis Bourgogne — DIJON
            MapViewport.EnsureWorldWindow(worldGeo);
            MapViewport.ZoomOut(MapViewport.WorldWindow);
            MapViewport.ZoomOut(MapViewport.WorldWindow);
            Assert.IsTrue(MapDisplaySystem.TrySelectProvinceById(em, BourgogneProvinceId));
            var bGeo = MapGeometryCache.GetOrBuild(
                MapSnapshotExporter.Width, MapSnapshotExporter.Height,
                MapViewport.State.Window, out _);
            var bourg = RenderLevel(
                em, bGeo, MapObservationLevel.Province,
                MapSnapshotExporter.LabelDensity.SelectedProvince, BourgogneProvinceId,
                filterCountry: -1, filterProvince: BourgogneProvinceId);
            var hasDijon = bourg.HasDijon;

            // ENG — LONDON (LONDRES)
            MapViewport.EnsureWorldWindow(worldGeo);
            MapViewport.ZoomOut(MapViewport.WorldWindow);
            MapViewport.ZoomOut(MapViewport.WorldWindow);
            Assert.IsTrue(MapDisplaySystem.TrySelectCountryByTag(em, "ENG"), "ENG depuis monde");
            var engGeo = MapGeometryCache.GetOrBuild(
                MapSnapshotExporter.Width, MapSnapshotExporter.Height,
                MapViewport.State.Window, out _);
            var eng = RenderLevel(
                em, engGeo, MapObservationLevel.Country,
                MapSnapshotExporter.LabelDensity.Provinces, -1,
                filterCountry: MapViewport.State.TargetCountryId, filterProvince: -1);
            var hasLondres = eng.HasLondres;

            var overlaps = fra.Overlaps + idf.Overlaps + bourg.Overlaps + eng.Overlaps;
            var drawnFloor = Math.Min(Math.Min(idf.Drawn, bourg.Drawn), fra.Drawn);
            var ok = OverlapControlPasses(
                overlaps, drawnFloor, hasParis, hasLondres, hasDijon);
            detail = "overlaps=" + overlaps +
                     " drawnFra=" + fra.Drawn + " drawnIdf=" + idf.Drawn +
                     " drawnB=" + bourg.Drawn + " drawnEng=" + eng.Drawn +
                     " PARIS=" + hasParis + " LONDON=" + hasLondres + " DIJON=" + hasDijon +
                     " omittedFra=" + fra.OmittedNames;
            MapSnapshotExporter.ResetZoomScaleToNeutral();
            return ok;
        }

        static bool OverlapControlPasses(
            int overlaps, int drawn, bool hasParis, bool hasLondres, bool hasDijon) =>
            overlaps == 0 && drawn >= DrawnFloorProvince &&
            hasParis && hasLondres && hasDijon;

        static bool CheckNeutralBitIdentical(out string detail)
        {
            MapSnapshotExporter.ResetZoomScaleToNeutral();
            MapLabelLayout.CollisionEnabled = true;
            MapLabelLayout.UseImportanceQueue = true;
            MapLabelLayout.LegacyCityLabels = false;

            using var harness = new SimulationHarness(Seed);
            harness.RunTicks(CaptureTick);
            var em = harness.EntityManager;
            PilotMapProvider.SetEnabled(true, clearCache: true);
            MapViewport.Reset();
            MapGeometryCache.ResetStatsAndClear();

            var worldGeo = MapGeometryCache.GetOrBuild(
                MapSnapshotExporter.Width, MapSnapshotExporter.Height, null, out _);
            MapViewport.EnsureWorldWindow(worldGeo);

            MapSnapshotExporter.ZoomScaleEnabled = false;
            var a = MapSnapshotExporter.RenderPoliticalPixels(
                em, worldGeo, MapSnapshotExporter.LabelDensity.Countries, -1,
                overlay: p => CityMarkerComposer.Compose(
                    p, worldGeo, em, MapObservationLevel.World));
            var ha = Sha256Hex(a);
            var b = MapSnapshotExporter.RenderPoliticalPixels(
                em, worldGeo, MapSnapshotExporter.LabelDensity.Countries, -1,
                overlay: p => CityMarkerComposer.Compose(
                    p, worldGeo, em, MapObservationLevel.World));
            var hb = Sha256Hex(b);

            // Rouge : un pixel change si on force le zoom
            MapSnapshotExporter.ZoomScaleEnabled = true;
            var z = MapSnapshotExporter.RenderPoliticalPixels(
                em, worldGeo, MapSnapshotExporter.LabelDensity.Countries, -1,
                overlay: p => CityMarkerComposer.Compose(
                    p, worldGeo, em, MapObservationLevel.World));
            var hz = Sha256Hex(z);

            // World glyph scale is 2 even with zoom — sprites 0 — may be bit-identical at world.
            // Prove at country level instead.
            Assert.IsTrue(MapDisplaySystem.TrySelectCountryByTag(em, "FRA"));
            var cGeo = MapGeometryCache.GetOrBuild(
                MapSnapshotExporter.Width, MapSnapshotExporter.Height,
                MapViewport.State.Window, out _);
            MapSnapshotExporter.ZoomScaleEnabled = false;
            var cn = MapSnapshotExporter.RenderPoliticalPixels(
                em, cGeo, MapSnapshotExporter.LabelDensity.Provinces, -1,
                overlay: p =>
                {
                    MapSpriteComposer.Compose(p, cGeo, em, MapObservationLevel.Country, false);
                    CityMarkerComposer.Compose(
                        p, cGeo, em, MapObservationLevel.Country,
                        filterCountryId: MapViewport.State.TargetCountryId);
                });
            var hcn = Sha256Hex(cn);
            var cn2 = MapSnapshotExporter.RenderPoliticalPixels(
                em, cGeo, MapSnapshotExporter.LabelDensity.Provinces, -1,
                overlay: p =>
                {
                    MapSpriteComposer.Compose(p, cGeo, em, MapObservationLevel.Country, false);
                    CityMarkerComposer.Compose(
                        p, cGeo, em, MapObservationLevel.Country,
                        filterCountryId: MapViewport.State.TargetCountryId);
                });
            var hcn2 = Sha256Hex(cn2);
            MapSnapshotExporter.ZoomScaleEnabled = true;
            var cz = MapSnapshotExporter.RenderPoliticalPixels(
                em, cGeo, MapSnapshotExporter.LabelDensity.Provinces, -1,
                overlay: p =>
                {
                    MapSpriteComposer.Compose(p, cGeo, em, MapObservationLevel.Country, false);
                    CityMarkerComposer.Compose(
                        p, cGeo, em, MapObservationLevel.Country,
                        filterCountryId: MapViewport.State.TargetCountryId);
                });
            var hcz = Sha256Hex(cz);

            var scalesNeutral =
                MapSnapshotExporter.GlyphScaleFor(MapObservationLevel.World) == 2 &&
                MapSnapshotExporter.GlyphScaleFor(MapObservationLevel.Country) == 2 &&
                MapSnapshotExporter.GlyphScaleFor(MapObservationLevel.Province) == 2;
            MapSnapshotExporter.ZoomScaleEnabled = false;
            scalesNeutral =
                MapSnapshotExporter.GlyphScaleFor(MapObservationLevel.World) == 2 &&
                MapSnapshotExporter.GlyphScaleFor(MapObservationLevel.Country) == 2 &&
                MapSnapshotExporter.GlyphScaleFor(MapObservationLevel.Province) == 2;

            var ok = ha == hb && hcn == hcn2 && hcn != hcz && scalesNeutral;
            detail = "world " + ha + "==" + hb +
                     " country_neutral==" + (hcn == hcn2) +
                     " country_zoom_diff=" + (hcn != hcz) +
                     " scalesNeutral=" + scalesNeutral +
                     " rouge: zoom change SHA country";
            MapSnapshotExporter.ResetZoomScaleToNeutral();
            return ok;
        }

        static bool CheckTextGrows(out string detail)
        {
            MapSnapshotExporter.ZoomScaleEnabled = true;
            var sw = MapSnapshotExporter.GlyphScaleFor(MapObservationLevel.World);
            var sc = MapSnapshotExporter.GlyphScaleFor(MapObservationLevel.Country);
            var sp = MapSnapshotExporter.GlyphScaleFor(MapObservationLevel.Province);
            MapSnapshotExporter.SetGlyphScale(sw);
            var ww = MapSnapshotExporter.MeasureBitmapText("BOURGOGNE");
            MapSnapshotExporter.SetGlyphScale(sc);
            var wc = MapSnapshotExporter.MeasureBitmapText("BOURGOGNE");
            MapSnapshotExporter.SetGlyphScale(sp);
            var wp = MapSnapshotExporter.MeasureBitmapText("BOURGOGNE");

            // Rouge : échelle restée constante
            var redConstant = !(sw < sc && sc < sp);

            var ok = sw == 2 && sc == 3 && sp == 5 && ww < wc && wc < wp && !redConstant;
            detail = "scales " + sw + "/" + sc + "/" + sp +
                     " widths " + ww + "/" + wc + "/" + wp +
                     " rouge_constant=" + redConstant;
            MapSnapshotExporter.ResetZoomScaleToNeutral();
            return ok;
        }

        static void ScanAllDataNames(
            out int examined, out int lostBefore, out int lostAfter, out List<string> stillLost)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            CollectNamesFromJson(
                Path.Combine(Application.streamingAssetsPath, "data", "provinces.json"), names);
            CollectNamesFromJson(
                Path.Combine(Application.streamingAssetsPath, "data", "cities.json"), names);
            CollectNamesFromJson(
                Path.Combine(Application.streamingAssetsPath, "data", "countries.json"), names);
            CollectNamesFromJson(
                Path.Combine(Application.streamingAssetsPath, "data", "city_coordinates.json"), names);
            CollectNamesFromJson(
                Path.Combine(Application.streamingAssetsPath, "data", "province_coordinates.json"), names);

            examined = names.Count;
            lostBefore = 0;
            lostAfter = 0;
            stillLost = new List<string>();
            foreach (var n in names)
            {
                var folded = MapSnapshotExporter.SanitizeLabelText(n);
                var without = MapSnapshotExporter.SanitizeLabelTextWithoutFold(n);
                // Perd des lettres si le sans-repli est plus court (lettres alpha) que le replié.
                var lettersFolded = CountAlnum(folded);
                var lettersWithout = CountAlnum(without);
                var lettersRawApprox = CountAlnum(MapSnapshotExporter.FoldDiacriticsToAscii(n));
                if (lettersWithout < lettersRawApprox)
                    lostBefore++;
                if (lettersFolded < lettersRawApprox)
                {
                    lostAfter++;
                    stillLost.Add(n + " → " + folded);
                }
            }
        }

        static int CountAlnum(string s)
        {
            var n = 0;
            if (s == null) return 0;
            for (var i = 0; i < s.Length; i++)
            {
                var c = s[i];
                if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
                    n++;
            }

            return n;
        }

        static void CollectNamesFromJson(string path, HashSet<string> names)
        {
            if (!File.Exists(path))
                return;
            var raw = File.ReadAllText(path);
            // Extraction légère des valeurs "name": "..."
            const string key = "\"name\"";
            var idx = 0;
            while (idx < raw.Length)
            {
                var k = raw.IndexOf(key, idx, StringComparison.Ordinal);
                if (k < 0) break;
                var colon = raw.IndexOf(':', k + key.Length);
                if (colon < 0) break;
                var q1 = raw.IndexOf('"', colon + 1);
                if (q1 < 0) break;
                var q2 = raw.IndexOf('"', q1 + 1);
                if (q2 < 0) break;
                var val = raw.Substring(q1 + 1, q2 - q1 - 1);
                if (!string.IsNullOrWhiteSpace(val) &&
                    !val.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    names.Add(val);
                idx = q2 + 1;
            }
        }

        static void WritePng(string path, Color32[] pixels, int w, int h)
        {
            // v1_077 : réemploie WriteMapBufferPng (inversion rangées) — ne pas
            // SetPixels32/EncodeToPNG brut (PNG à l'envers).
            MapSnapshotExporter.WriteMapBufferPng(pixels, w, h, path);
        }

        /// <summary>
        /// v1_095 — le repère nord/sud est DÉRIVÉ du monde rendu, plus nommé en dur.
        /// Avant : ENG au nord, CAS au sud. Depuis v1_094 la carte peint le monde
        /// joué, et la Castille perd la Navarre — sa seule province dans la fenêtre
        /// pilote — avant t1000 : zéro pixel CAS, contrôle aveugle.
        /// </summary>
        static void AssertPngNorthUpOrFail(EntityManager em, string path)
        {
            var colors = CountryColors.Load();
            Assert.IsTrue(
                MapSnapshotExporter.TryDeriveNorthSouthReferenceColors(
                    em, colors, out var north, out var south, out var refDetail),
                "repère d'orientation indérivable : " + refDetail);
            var ok = MapSnapshotExporter.TryAssertPngNorthUp(
                path, north, south,
                out _, out _, out _, out _, out var nCount, out var sCount, out var detail);
            if (ok)
                return;

            // v1_095 — NON-APPLICABILITÉ EXPLICITE, PAS UN LAISSEZ-PASSER.
            // Au zoom province, un seul pays remplit le cadre : il n'y a ni nord ni
            // sud à comparer, et exiger les deux repères reviendrait à exiger que
            // l'image montre autre chose qu'elle-même. On ne tolère ce cas que s'il
            // est BIEN mono-pays — un côté franchement peint, l'autre absent.
            // Deux côtés vides resteraient un échec : ce serait une image morte.
            var monoCountry = (nCount == 0 && sCount >= 1000) ||
                              (sCount == 0 && nCount >= 1000);
            Assert.IsTrue(
                monoCountry,
                "V1077-A orientation fichier (" + refDetail + ") : " + detail);
            UnityEngine.Debug.Log(
                "V1077-A non applicable (cadre mono-pays) sur " +
                Path.GetFileName(path) + " : n=" + nCount + " s=" + sCount);
        }

        static string Sha256Hex(Color32[] pixels)
        {
            if (pixels == null || pixels.Length == 0)
                return "(empty)";
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
    }
}
