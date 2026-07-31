using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using VictoriaGame.Presentation;
using Debug = UnityEngine.Debug;

namespace VictoriaGame.Tests
{
    /// <summary>
    /// Point d'entrée batchmode :
    /// -executeMethod VictoriaGame.Tests.V1079TextOrientationBatchRunner.Run
    /// </summary>
    public static class V1079TextOrientationBatchRunner
    {
        public static void Run()
        {
            V1079TextOrientationTests.RunAndWriteArtifacts();
            Debug.Log("V1079TextOrientationBatchRunner: DONE");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
        }
    }

    /// <summary>
    /// v1_079 — orientation TEXTE sur fichier PNG (motif glyphe asymétrique),
    /// convention unique nord@py0, re-vérif carte V1077-A.
    /// </summary>
    [TestFixture]
    public class V1079TextOrientationTests
    {
        const uint Seed = 42195u;
        const int WitnessOx = 8;
        const int WitnessOy = 8;
        const int WitnessScale = 2;

        static readonly string[] Archive076Text =
        {
            "after_neutral_world.png", "after_neutral_country.png", "after_neutral_province.png",
            "after_zoom_world.png", "after_zoom_country.png", "after_zoom_province.png",
        };

        [TearDown]
        public void TearDown()
        {
            MapSnapshotExporter.DebugPreInvertGlyphs = false;
            MapSnapshotExporter.ResetZoomScaleToNeutral();
            PilotMapProvider.Enabled = false;
            MapGeometryCache.ResetStatsAndClear();
        }

        [Test]
        public void V1079_A_Control_RedOnArchivedFlippedText()
        {
            Assert.IsTrue(
                MeasureArchiveTextFlipped(out var detail),
                "rouge V1079-A: archives texte miroité doivent faire échouer le contrôle — " +
                detail);
        }

        [Test]
        public void V1079_A_Control_RedOnLocalPreInversionProbe()
        {
            // Rouge : DebugPreInvertGlyphs + WriteMapBufferPng = double inversion.
            var dir = Path.Combine(Application.dataPath, "..", "Captures", "v1_079_probe");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "witness_pre_invert_red.png");
            MapSnapshotExporter.DebugPreInvertGlyphs = true;
            try
            {
                WriteWitnessProbe(path);
            }
            finally
            {
                MapSnapshotExporter.DebugPreInvertGlyphs = false;
            }

            Assert.IsFalse(
                MapSnapshotExporter.TryAssertPngWitnessTextUpright(
                    path, WitnessOx, WitnessOy,
                    MapSnapshotExporter.Width, MapSnapshotExporter.Height, WitnessScale,
                    out var up, out var flip, out var detail),
                "rouge V1079-A: témoin pré-inversé doit échouer — " + detail);
            Assert.Greater(flip, up, detail);
        }

        [Test]
        public void V1079_A_Control_GreenOnWriteMapBufferPng()
        {
            var dir = Path.Combine(Application.dataPath, "..", "Captures", "v1_079_probe");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "witness_upright_green.png");
            WriteWitnessProbe(path);
            Assert.IsTrue(
                MapSnapshotExporter.TryAssertPngWitnessTextUpright(
                    path, WitnessOx, WitnessOy,
                    MapSnapshotExporter.Width, MapSnapshotExporter.Height, WitnessScale,
                    out var up, out var flip, out var detail),
                "vert V1079-A: témoin sans pré-inversion — " + detail);
            Assert.Greater(up, flip, detail);
        }

        [Test]
        public void V1079_B_MapNorthUp_StillHolds()
        {
            // V1077-A reste mordant : WritePngSized brut rouge, WriteMapBufferPng vert.
            var colors = CountryColors.Load();

            using var harness = new SimulationHarness(Seed);
            harness.RunTicks(1000);
            PilotMapProvider.SetEnabled(true, clearCache: true);

            // v1_095 — repère DÉRIVÉ : depuis v1_094 la carte peint le monde joué,
            // et la Castille a perdu la Navarre avant t1000 (0 pixel CAS).
            Assert.IsTrue(
                MapSnapshotExporter.TryDeriveNorthSouthReferenceColors(
                    harness.EntityManager, colors, out var eng, out var cas,
                    out var refDetail),
                "repère d'orientation indérivable : " + refDetail);

            var geo = MapGeometryCache.GetOrBuild(
                MapSnapshotExporter.Width, MapSnapshotExporter.Height, null, out _);
            MapViewport.EnsureWorldWindow(geo);
            var pixels = MapSnapshotExporter.RenderPoliticalPixels(
                harness.EntityManager, geo, MapSnapshotExporter.LabelDensity.Countries, -1);

            var dir = Path.Combine(Application.dataPath, "..", "Captures", "v1_079_probe");
            Directory.CreateDirectory(dir);
            var rawPath = Path.Combine(dir, "raw_no_flip_map.png");
            var okPath = Path.Combine(dir, "map_buffer_flip_map.png");
            MapSnapshotExporter.WritePngSized(pixels, geo.Width, geo.Height, rawPath);
            MapSnapshotExporter.WriteMapBufferPng(pixels, geo.Width, geo.Height, okPath);

            Assert.IsFalse(
                MapSnapshotExporter.TryAssertPngNorthUp(
                    rawPath, eng, cas,
                    out _, out _, out _, out _, out _, out _, out var rawDetail),
                "rouge V1079-B: sans inversion carte — " + rawDetail);
            Assert.IsTrue(
                MapSnapshotExporter.TryAssertPngNorthUp(
                    okPath, eng, cas,
                    out var engY, out var casY, out _, out _, out _, out _, out var okDetail),
                "vert V1079-B: WriteMapBufferPng — " + okDetail);
            Assert.Less(engY, casY, okDetail);
        }

        [Test]
        public void V1079_C_ThreePaths_SameOrientation()
        {
            Assert.IsTrue(MeasureThreePaths(out var detail), detail);
        }

        [Test]
        public void V1079_Artifacts_And_Verdict() => RunAndWriteArtifacts();

        public static void RunAndWriteArtifacts()
        {
            var logPath = Path.Combine(Application.dataPath, "..", "Logs", "v1_079_orientation_texte.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            var sb = new StringBuilder(65536);
            sb.AppendLine("=== v1_079 ORIENTATION TEXTE PNG ===");
            sb.AppendLine("created=" + DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
            sb.AppendLine();

            sb.AppendLine("=== PARTIE 1 — TABLEAU DES TROIS CHEMINS (MESURE) ===");
            var threeOk = MeasureThreePaths(out var threeDetail);
            sb.AppendLine(threeDetail);
            sb.AppendLine("V1079-C trois chemins: " + (threeOk ? "PASS" : "FAIL"));
            sb.AppendLine();

            sb.AppendLine("=== PARTIE 1b — RECENSEMENT ÉCRIVAINS BUFFER ===");
            sb.AppendLine(RecensementEcrivains());
            sb.AppendLine();

            sb.AppendLine("=== PARTIE 2 — CONVENTION UNIQUE ===");
            sb.AppendLine(
                "Convention: buffer nord@py0 ; écrivains sans compensation locale ; " +
                "inversion UNE FOIS à WriteMapBufferPng (captures) / UI Toolkit (écran, py0→haut).");
            sb.AppendLine(
                "Correction: BlitGlyph gy = oy + row*scale (pré-inversion GlyphH-1-row RETIRÉE).");
            sb.AppendLine();

            sb.AppendLine("=== PARTIE 3 — CONTRÔLE TEXTE (V1079-A) ===");
            sb.AppendLine("--- ROUGE : archives Captures/v1_079_text_red (copie v1_076 after_*) ---");
            var redArchive = MeasureArchiveTextFlipped(out var redArchDetail);
            sb.AppendLine(redArchDetail);
            sb.AppendLine("V1079-A rouge archives: " + (redArchive ? "PASS (texte miroité)" : "FAIL"));

            var probeDir = Path.Combine(Application.dataPath, "..", "Captures", "v1_079_probe");
            Directory.CreateDirectory(probeDir);
            var redPath = Path.Combine(probeDir, "witness_pre_invert_red.png");
            var greenPath = Path.Combine(probeDir, "witness_upright_green.png");
            MapSnapshotExporter.DebugPreInvertGlyphs = true;
            try { WriteWitnessProbe(redPath); }
            finally { MapSnapshotExporter.DebugPreInvertGlyphs = false; }
            WriteWitnessProbe(greenPath);

            var redProbe = !MapSnapshotExporter.TryAssertPngWitnessTextUpright(
                redPath, WitnessOx, WitnessOy,
                MapSnapshotExporter.Width, MapSnapshotExporter.Height, WitnessScale,
                out var ru, out var rf, out var rd);
            sb.AppendLine("rouge témoin pré-inversé: " + rd + " → " + (redProbe ? "PASS" : "FAIL"));
            var greenProbe = MapSnapshotExporter.TryAssertPngWitnessTextUpright(
                greenPath, WitnessOx, WitnessOy,
                MapSnapshotExporter.Width, MapSnapshotExporter.Height, WitnessScale,
                out var gu, out var gf, out var gd);
            sb.AppendLine("vert témoin corrigé: " + gd + " → " + (greenProbe ? "PASS" : "FAIL"));
            sb.AppendLine();

            sb.AppendLine("=== PARTIE 3b — RE-VÉRIF CARTE NORD EN HAUT (V1079-B) ===");
            var mapOk = false;
            var mapDetail = "";
            try
            {
                var t = new V1079TextOrientationTests();
                t.V1079_B_MapNorthUp_StillHolds();
                mapOk = true;
                mapDetail = "WritePngSized rouge + WriteMapBufferPng vert (ENG_y<CAS_y)";
            }
            catch (Exception ex)
            {
                mapDetail = ex.Message;
            }

            sb.AppendLine(mapDetail);
            sb.AppendLine("V1079-B carte nord en haut: " + (mapOk ? "PASS" : "FAIL"));
            sb.AppendLine();

            sb.AppendLine("=== PARTIE 4 — RÉGÉNÉRATION CAPTURES À TEXTE + CONTRÔLE VERT ===");
            var liveOk = RegenerateAndCheckLiveText(sb, out var liveDetail);
            sb.AppendLine(liveDetail);
            sb.AppendLine("live texte upright: " + (liveOk ? "PASS" : "FAIL"));
            sb.AppendLine();

            var vA = redArchive && redProbe && greenProbe;
            var vB = mapOk;
            var vC = threeOk && liveOk;
            sb.AppendLine("=== CONTRÔLES ===");
            sb.AppendLine("V1079-A rouge archives+témoin puis vert témoin: " + (vA ? "PASS" : "FAIL"));
            sb.AppendLine("V1079-B carte toujours nord en haut: " + (vB ? "PASS" : "FAIL"));
            sb.AppendLine("V1079-C trois chemins même orientation: " + (vC ? "PASS" : "FAIL"));

            var all = vA && vB && vC;
            sb.AppendLine();
            sb.AppendLine(
                "VERDICT: " + (all ? "PASS" : "FAIL") +
                " | pré-inversion glyphes retirée ; témoin P asymétrique ; " +
                "archives texte rouge puis live vert ; carte V1077 intacte");

            File.WriteAllText(logPath, sb.ToString(), Encoding.UTF8);
            Debug.Log("V1079: wrote " + logPath);
            Assert.IsTrue(all, "V1079 artifacts FAIL — voir " + logPath);
        }

        static string RecensementEcrivains()
        {
            var lines = new List<string>
            {
                "1. Remplissage politique/terrain (ProvinceAt → Fill) — nord@py0, aucune compensation",
                "2. ApplyUnownedHatch — nord@py0",
                "3. ApplyHillshadeOnLand — nord@py0",
                "4. ApplyRivers — nord@py0",
                "5. MapSpriteOverlay.BlitSprite — nord@py0 (WorldToPixelY maxY−wy)",
                "6. BlitGlyph / DrawBitmapText — nord@py0, row0=haut lettre (pré-inversion RETIRÉE v1_079)",
                "7. Encodage captures : WriteMapBufferPng — UNIQUE inversion rangées",
                "8. Encodage écran : InGameHud.SetPixels32(buffer) — UI Toolkit affiche py0 en haut",
                "   (équivalent visuel de WriteMapBufferPng ; pas de 2e compensation glyphe)",
            };
            return string.Join("\n", lines);
        }

        static bool MeasureArchiveTextFlipped(out string detail)
        {
            var dir = Path.Combine(Application.dataPath, "..", "Captures", "v1_079_text_red");
            Directory.CreateDirectory(dir);
            var sb = new StringBuilder();

            // Régénère les 6 after_* avec DebugPreInvertGlyphs (défaut v1_077 démasqué).
            EnsureRedTextArchives(dir, sb);

            var witnessRed = Path.Combine(dir, "witness_pre_invert_red.png");
            MapSnapshotExporter.DebugPreInvertGlyphs = true;
            try { WriteWitnessProbe(witnessRed); }
            finally { MapSnapshotExporter.DebugPreInvertGlyphs = false; }

            var witnessUpright = MapSnapshotExporter.TryAssertPngWitnessTextUpright(
                witnessRed, WitnessOx, WitnessOy,
                MapSnapshotExporter.Width, MapSnapshotExporter.Height, WitnessScale,
                out var wu, out var wf, out var wl);
            sb.AppendLine("archive witness: " + wl);

            var failCount = 0;
            var total = 0;
            foreach (var file in Archive076Text)
            {
                var path = Path.Combine(dir, file);
                total++;
                // Témoin peint dans le coin de chaque archive rouge.
                var upright = MapSnapshotExporter.TryAssertPngWitnessTextUpright(
                    path, WitnessOx, WitnessOy,
                    MapSnapshotExporter.Width, MapSnapshotExporter.Height, WitnessScale,
                    out var up, out var flip, out var line);
                sb.AppendLine(line);
                if (!upright && flip > up)
                    failCount++;
                else
                    sb.AppendLine("  UNEXPECTED upright/weak on " + file);
            }

            var province = Path.Combine(dir, "after_neutral_province.png");
            var provFail = !MapSnapshotExporter.TryAssertPngWitnessTextUpright(
                province, WitnessOx, WitnessOy,
                MapSnapshotExporter.Width, MapSnapshotExporter.Height, WitnessScale,
                out var pu, out var pf, out var pl);
            sb.AppendLine("focus province: " + pl);

            detail = sb.ToString().TrimEnd() + "\n" +
                     "witness_red_fails=" + (!witnessUpright && wf > wu) +
                     " captures_flipped=" + failCount + "/" + total +
                     " province_flipped=" + (provFail && pf > pu);
            return !witnessUpright && wf > wu && provFail && pf > pu && failCount >= 5;
        }

        /// <summary>
        /// Produit les archives rouges : carte nord en haut + texte miroité
        /// (DebugPreInvertGlyphs + WriteMapBufferPng) + témoin P dans le coin.
        /// </summary>
        static void EnsureRedTextArchives(string dir, StringBuilder sb)
        {
            using var harness = new SimulationHarness(Seed);
            harness.RunTicks(1000);
            PilotMapProvider.SetEnabled(true, clearCache: true);
            var geo = MapGeometryCache.GetOrBuild(
                MapSnapshotExporter.Width, MapSnapshotExporter.Height, null, out _);
            MapViewport.EnsureWorldWindow(geo);

            MapSnapshotExporter.DebugPreInvertGlyphs = true;
            try
            {
                var densities = new[]
                {
                    (MapSnapshotExporter.LabelDensity.Countries, "after_neutral_world.png"),
                    (MapSnapshotExporter.LabelDensity.Provinces, "after_neutral_country.png"),
                    (MapSnapshotExporter.LabelDensity.SelectedProvince, "after_neutral_province.png"),
                    (MapSnapshotExporter.LabelDensity.Countries, "after_zoom_world.png"),
                    (MapSnapshotExporter.LabelDensity.Provinces, "after_zoom_country.png"),
                    (MapSnapshotExporter.LabelDensity.SelectedProvince, "after_zoom_province.png"),
                };
                foreach (var (density, file) in densities)
                {
                    var pixels = MapSnapshotExporter.RenderPoliticalPixels(
                        harness.EntityManager, geo, density, -1);
                    MapSnapshotExporter.PaintTextOrientationWitness(
                        pixels, WitnessOx, WitnessOy, WitnessScale);
                    var path = Path.Combine(dir, file);
                    MapSnapshotExporter.WriteMapBufferPng(pixels, geo.Width, geo.Height, path);
                    sb.AppendLine("wrote red archive " + file);
                }
            }
            finally
            {
                MapSnapshotExporter.DebugPreInvertGlyphs = false;
                PilotMapProvider.Enabled = false;
                MapGeometryCache.ResetStatsAndClear();
            }
        }

        static bool MeasureThreePaths(out string detail)
        {
            var sb = new StringBuilder();
            sb.AppendLine(
                "chemin                         | carte          | texte");
            sb.AppendLine(
                "-------------------------------|----------------|----------------");

            using var harness = new SimulationHarness(Seed);
            harness.RunTicks(1000);
            PilotMapProvider.SetEnabled(true, clearCache: true);
            var geo = MapGeometryCache.GetOrBuild(
                MapSnapshotExporter.Width, MapSnapshotExporter.Height, null, out _);
            MapViewport.EnsureWorldWindow(geo);
            var pixels = MapSnapshotExporter.RenderPoliticalPixels(
                harness.EntityManager, geo, MapSnapshotExporter.LabelDensity.Countries, -1);
            MapSnapshotExporter.PaintTextOrientationWitness(
                pixels, WitnessOx, WitnessOy, WitnessScale);

            // v1_095 — repère nord/sud dérivé du monde à t1000 (voir V1079_B).
            if (!MapSnapshotExporter.TryDeriveNorthSouthReferenceColors(
                    harness.EntityManager, CountryColors.Load(),
                    out var refNorth, out var refSouth, out var refDetail))
            {
                detail = "repère d'orientation indérivable : " + refDetail;
                return false;
            }

            var dir = Path.Combine(Application.dataPath, "..", "Captures", "v1_079_probe");
            Directory.CreateDirectory(dir);

            // 1) Capture 1600×1200 — WriteMapBufferPng
            var p1600 = Path.Combine(dir, "path_1600_WriteMapBufferPng.png");
            MapSnapshotExporter.WriteMapBufferPng(pixels, geo.Width, geo.Height, p1600);
            var c1600 = ClassifyPng(p1600, geo.Width, geo.Height, refNorth, refSouth, out var d1600);
            sb.AppendLine("1600×1200 WriteMapBufferPng   | " + c1600.map + " | " + c1600.text);
            sb.AppendLine("  " + d1600);

            // 2) Capture 960×720 — même API (chemin PilotMapProvider)
            var small = DownscaleNearest(pixels, geo.Width, geo.Height, 960, 720);
            // Témoin re-peint à l'échelle 960 (sinon perdu au downscale).
            MapSnapshotExporter.WithPixelSize(960, 720, () =>
            {
                MapSnapshotExporter.PaintTextOrientationWitness(small, WitnessOx, WitnessOy, WitnessScale);
            });
            var p960 = Path.Combine(dir, "path_960_WriteMapBufferPng.png");
            MapSnapshotExporter.WriteMapBufferPng(small, 960, 720, p960);
            var c960 = ClassifyPng(p960, 960, 720, refNorth, refSouth, out var d960);
            sb.AppendLine("960×720 WriteMapBufferPng     | " + c960.map + " | " + c960.text);
            sb.AppendLine("  " + d960);

            // 3) Écran InGameHud.PresentFrame = SetPixels32(buffer) sans flip local.
            //    UI Toolkit affiche py=0 en haut (= joueur nord en haut, v1_077).
            //    Mesure écran = WriteMapBufferPng du même buffer (équivalent visuel).
            //    EncodeToPNG(MapTexture) seul : y=0 en bas → carte NORD_BAS (attendu).
            var engCol = refNorth;
            var casCol = refSouth;
            var texPath = Path.Combine(dir, "path_screen_MapTexture_encode.png");
            MapSnapshotExporter.WritePngSized(pixels, geo.Width, geo.Height, texPath);
            var texNorth = MapSnapshotExporter.TryAssertPngNorthUp(
                texPath, engCol, casCol,
                out _, out _, out _, out _, out _, out _, out _);

            var screenEq = Path.Combine(dir, "path_screen_equivalent_WriteMapBufferPng.png");
            MapSnapshotExporter.WriteMapBufferPng(pixels, geo.Width, geo.Height, screenEq);
            var cScreen = ClassifyPng(screenEq, geo.Width, geo.Height, refNorth, refSouth, out var dScreen);
            sb.AppendLine(
                "écran PresentFrame≡WriteMapBuf | " + cScreen.map + " | " + cScreen.text);
            sb.AppendLine(
                "  MapTexture EncodeToPNG north_up=" + texNorth +
                " (y0 bas : attendu false ; UI Toolkit affiche nord en haut)");
            sb.AppendLine("  " + dScreen);

            var allSame =
                c1600.map == "NORD_HAUT" && c1600.text == "UPRIGHT" &&
                c960.map == "NORD_HAUT" && c960.text == "UPRIGHT" &&
                ClassifyPng(
                    Path.Combine(dir, "path_screen_equivalent_WriteMapBufferPng.png"),
                    geo.Width, geo.Height, refNorth, refSouth, out _).map == "NORD_HAUT";

            // Re-check screen text
            var screenTextOk = MapSnapshotExporter.TryAssertPngWitnessTextUpright(
                Path.Combine(dir, "path_screen_equivalent_WriteMapBufferPng.png"),
                WitnessOx, WitnessOy, geo.Width, geo.Height, WitnessScale,
                out _, out _, out _);

            detail = sb.ToString().TrimEnd() +
                     "\nall_paths_north_up_text_upright=" + (allSame && screenTextOk);
            PilotMapProvider.Enabled = false;
            MapGeometryCache.ResetStatsAndClear();
            return allSame && screenTextOk;
        }

        static (string map, string text) ClassifyPng(
            string path, int w, int h, Color32 eng, Color32 cas, out string detail)
        {
            var north = MapSnapshotExporter.TryAssertPngNorthUp(
                path, eng, cas,
                out var ey, out var cy, out _, out _, out _, out _, out var mapLine);
            var textOk = MapSnapshotExporter.TryAssertPngWitnessTextUpright(
                path, WitnessOx, WitnessOy, w, h, WitnessScale,
                out var up, out var flip, out var textLine);
            detail = mapLine + " | " + textLine;
            return (
                north ? "NORD_HAUT" : "NORD_BAS",
                textOk ? "UPRIGHT" : (flip > up ? "MIROITE" : "FAIBLE"));
        }

        static bool RegenerateAndCheckLiveText(StringBuilder sb, out string detail)
        {
            // Régénère v1_076 after_neutral_province avec le BlitGlyph corrigé + témoin.
            using var harness = new SimulationHarness(Seed);
            harness.RunTicks(1000);
            PilotMapProvider.SetEnabled(true, clearCache: true);
            var geo = MapGeometryCache.GetOrBuild(
                MapSnapshotExporter.Width, MapSnapshotExporter.Height, null, out _);
            MapViewport.EnsureWorldWindow(geo);
            MapViewport.ForceState(MapViewportState.World(MapViewport.WorldWindow));
            // Niveau province pour étiquettes villes.
            var pixels = MapSnapshotExporter.RenderPoliticalPixels(
                harness.EntityManager, geo, MapSnapshotExporter.LabelDensity.Provinces, -1);
            MapSnapshotExporter.PaintTextOrientationWitness(
                pixels, WitnessOx, WitnessOy, WitnessScale);

            var liveDir = Path.Combine(Application.dataPath, "..", "Captures", "v1_076");
            Directory.CreateDirectory(liveDir);
            var livePath = Path.Combine(liveDir, "after_neutral_province.png");
            MapSnapshotExporter.WriteMapBufferPng(pixels, geo.Width, geo.Height, livePath);

            var witnessOk = MapSnapshotExporter.TryAssertPngWitnessTextUpright(
                livePath, WitnessOx, WitnessOy, geo.Width, geo.Height, WitnessScale,
                out _, out _, out var wLine);
            // « A » des labels carte (scan) — doit maintenant être upright.
            var scanOk = MapSnapshotExporter.TryAssertPngTextUpright(
                livePath, out var up, out var flip, out var sLine, probeChar: 'A');
            if (!MapSnapshotExporter.TryDeriveNorthSouthReferenceColors(
                    harness.EntityManager, CountryColors.Load(),
                    out var eng, out var cas, out var refDetail))
            {
                detail = "repère d'orientation indérivable : " + refDetail;
                return false;
            }

            var northOk = MapSnapshotExporter.TryAssertPngNorthUp(
                livePath, eng, cas,
                out var ey, out var cy, out _, out _, out _, out _, out var nLine);

            sb.AppendLine(wLine);
            sb.AppendLine(sLine);
            sb.AppendLine(nLine);
            detail =
                "witness=" + witnessOk + " scanA=" + scanOk +
                " north=" + northOk +
                " ENG_y=" + ey.ToString("F3", CultureInfo.InvariantCulture) +
                " CAS_y=" + cy.ToString("F3", CultureInfo.InvariantCulture) +
                " up/flip=" + up + "/" + flip;
            PilotMapProvider.Enabled = false;
            MapGeometryCache.ResetStatsAndClear();
            return witnessOk && northOk && ey < cy;
        }

        static void WriteWitnessProbe(string path)
        {
            var w = MapSnapshotExporter.Width;
            var h = MapSnapshotExporter.Height;
            var pixels = new Color32[w * h];
            var sea = new Color32(0x1a, 0x3a, 0x4a, 255);
            for (var i = 0; i < pixels.Length; i++)
                pixels[i] = sea;

            MapSnapshotExporter.WithPixelSize(w, h, () =>
            {
                MapSnapshotExporter.PaintTextOrientationWitness(
                    pixels, WitnessOx, WitnessOy, WitnessScale);
            });
            MapSnapshotExporter.WriteMapBufferPng(pixels, w, h, path);
        }

        static Color32[] DownscaleNearest(
            Color32[] src, int sw, int sh, int dw, int dh)
        {
            var dst = new Color32[dw * dh];
            for (var y = 0; y < dh; y++)
            {
                var sy = y * sh / dh;
                for (var x = 0; x < dw; x++)
                {
                    var sx = x * sw / dw;
                    dst[y * dw + x] = src[sy * sw + sx];
                }
            }

            return dst;
        }
    }
}
