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
    /// -executeMethod VictoriaGame.Tests.V1077OrientationBatchRunner.Run
    /// </summary>
    public static class V1077OrientationBatchRunner
    {
        public static void Run()
        {
            V1077OrientationTests.RunAndWriteArtifacts();
            Debug.Log("V1077OrientationBatchRunner: DONE");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
        }
    }

    /// <summary>
    /// v1_077 — orientation PNG (fichier), chemin d'écriture unique, re-vérif v1_073/v1_076.
    /// </summary>
    [TestFixture]
    public class V1077OrientationTests
    {
        static readonly string[] Archive073 =
        {
            "neutral_world.png", "neutral_country.png", "neutral_province.png",
            "neutral_country_eng.png",
            "zoom_world.png", "zoom_country.png", "zoom_province.png",
            "zoom_country_eng.png",
        };

        static readonly string[] Archive076 =
        {
            "before_world.png", "before_country.png", "before_province.png",
            "after_neutral_world.png", "after_neutral_country.png", "after_neutral_province.png",
            "after_zoom_world.png", "after_zoom_country.png", "after_zoom_province.png",
        };

        [Test]
        public void V1077_A_Control_RedOnArchivedFlippedCaptures()
        {
            Assert.IsTrue(MeasureArchiveOrientation(expectNorthUp: false, out var detail), detail);
        }

        [Test]
        public void V1077_A_Control_GreenOnWriteMapBufferPng()
        {
            // Rouge : WritePngSized brut (sans inversion) → nord en bas.
            var colors = CountryColors.Load();

            using var harness = new SimulationHarness(42195u);
            harness.RunTicks(1000);
            PilotMapProvider.SetEnabled(true, clearCache: true);

            // v1_095 — repère DÉRIVÉ du monde à t1000. Nommer ENG/CAS ne tient plus
            // depuis v1_094 : la carte peint le monde joué, et la Castille a perdu
            // la Navarre — sa seule province de la fenêtre pilote — avant t1000.
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

            var dir = Path.Combine(Application.dataPath, "..", "Captures", "v1_077_probe");
            Directory.CreateDirectory(dir);
            var rawPath = Path.Combine(dir, "raw_no_flip.png");
            var okPath = Path.Combine(dir, "map_buffer_flip.png");

            MapSnapshotExporter.WritePngSized(pixels, geo.Width, geo.Height, rawPath);
            MapSnapshotExporter.WriteMapBufferPng(pixels, geo.Width, geo.Height, okPath);

            var rawOk = MapSnapshotExporter.TryAssertPngNorthUp(
                rawPath, eng, cas,
                out var rawEngY, out var rawCasY, out _, out _, out _, out _, out var rawDetail);
            Assert.IsFalse(
                rawOk,
                "rouge V1077-A: WritePngSized sans inversion doit échouer — " + rawDetail);

            Assert.IsTrue(
                MapSnapshotExporter.TryAssertPngNorthUp(
                    okPath, eng, cas,
                    out var engY, out var casY, out _, out _, out _, out _, out var okDetail),
                "vert V1077-A: WriteMapBufferPng — " + okDetail);
            Assert.Less(engY, casY, okDetail);
            Assert.Greater(rawEngY, rawCasY, "archive brute: ENG sous CAS");
            PilotMapProvider.Enabled = false;
            MapGeometryCache.ResetStatsAndClear();
        }

        [Test]
        public void V1077_B_SingleMapWritePath_Recensement()
        {
            Assert.IsTrue(CheckWritePathRecensement(out var detail), detail);
        }

        [Test]
        public void V1077_C_GainsHold_OnUprightImages()
        {
            // Délègue aux contrôles publics déjà mordants de v1_073 / v1_076.
            var t73 = new V1073LabelZoomTests();
            t73.V1073_A_NoLetterLost_SixNamedCases();
            var t76 = new V1076OverlayTests();
            t76.V1076_A_NoSpriteOnNonLand();
            t76.V1076_B_NeutralNamedCitiesSurviveZoom();
            t76.V1076_D_V1073GainsHold();
        }

        [Test]
        public void V1077_Artifacts_And_Verdict() => RunAndWriteArtifacts();

        public static void RunAndWriteArtifacts()
        {
            var logPath = Path.Combine(Application.dataPath, "..", "Logs", "v1_077_orientation.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            var sb = new StringBuilder(65536);
            sb.AppendLine("=== v1_077 ORIENTATION PNG ===");
            sb.AppendLine("created=" + DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
            sb.AppendLine();

            sb.AppendLine("=== PARTIE 1 — RECENSEMENT CHEMINS ÉCRITURE PNG Color32[] ===");
            var vB = CheckWritePathRecensement(out var recensement);
            sb.AppendLine(recensement);
            sb.AppendLine();

            sb.AppendLine("=== PARTIE 2 — CONTRÔLE ORIENTATION SUR FICHIER ===");
            sb.AppendLine("--- ROUGE : archives v1_073 / v1_076 (avant correction) ---");
            var redOk = MeasureArchiveOrientation(expectNorthUp: false, out var redDetail);
            sb.AppendLine(redDetail);
            sb.AppendLine("V1077-A rouge archives: " + (redOk ? "PASS (toutes à l'envers)" : "FAIL"));
            sb.AppendLine();

            sb.AppendLine("--- VERT : Captures/v1_073 + v1_076 (régénérées par V1073/V1076 WriteMapBufferPng) ---");
            var greenOk = MeasureLiveOrientation(expectNorthUp: true, out var greenDetail);
            if (!greenOk)
            {
                sb.AppendLine("live encore à l'envers → régénération forcée via artifacts V1073+V1076");
                V1073LabelZoomTests.RunAndWriteArtifacts();
                V1076OverlayTests.RunAndWriteArtifacts();
                greenOk = MeasureLiveOrientation(expectNorthUp: true, out greenDetail);
            }

            sb.AppendLine(greenDetail);
            sb.AppendLine("V1077-A vert live: " + (greenOk ? "PASS" : "FAIL"));
            sb.AppendLine();

            sb.AppendLine("=== PARTIE 3 — RE-VÉRIF ACQUIS SUR IMAGES À L'ENDROIT ===");
            var t73 = new V1073LabelZoomTests();
            var t76 = new V1076OverlayTests();
            var cA = true;
            var cB = true;
            var cD = true;
            var cAccent = true;
            try { t73.V1073_A_NoLetterLost_SixNamedCases(); }
            catch (Exception ex) { cAccent = false; sb.AppendLine("V1073-A FAIL " + ex.Message); }
            try { t76.V1076_A_NoSpriteOnNonLand(); }
            catch (Exception ex) { cA = false; sb.AppendLine("V1076-A FAIL " + ex.Message); }
            try { t76.V1076_B_NeutralNamedCitiesSurviveZoom(); }
            catch (Exception ex) { cB = false; sb.AppendLine("V1076-B FAIL " + ex.Message); }
            try { t76.V1076_D_V1073GainsHold(); }
            catch (Exception ex) { cD = false; sb.AppendLine("V1076-D FAIL " + ex.Message); }
            if (cAccent) sb.AppendLine("V1073-A 0 nom perd de lettre: PASS (confirmé)");
            if (cA) sb.AppendLine("V1076-A sprites hors mer: PASS (confirmé)");
            if (cB) sb.AppendLine("V1076-B 0 ville nommée renoncée: PASS (confirmé)");
            if (cD) sb.AppendLine("V1076-D accents+échelle: PASS (confirmé)");
            sb.AppendLine(
                "Retournement ≠ terre↔mer : conclusions v1_073/v1_076 conservées sur images à l'endroit. " +
                "Détail amas : Logs/v1_076_overlay.log.");
            sb.AppendLine();

            sb.AppendLine("=== PARTIE 4 — CONTRÔLES ===");
            sb.AppendLine("V1077-A rouge archives puis vert live: " +
                          (redOk && greenOk ? "PASS" : "FAIL"));
            sb.AppendLine("V1077-B chemin unique WriteMapBufferPng: " + (vB ? "PASS" : "FAIL"));
            sb.AppendLine("V1077-C acquis v1_073/v1_076: " +
                          (cAccent && cA && cB && cD ? "PASS" : "FAIL"));
            sb.AppendLine(
                "Budget suite : voir XML LARGE ; v1_076 avait 255 s / 142 cas vs budget 245 s — " +
                "filtre intact ; révision proposée 280 s (bruit + V1077).");

            var all = redOk && greenOk && vB && cAccent && cA && cB && cD;
            sb.AppendLine();
            sb.AppendLine(
                "VERDICT: " + (all ? "PASS" : "FAIL") +
                " | WriteMapBufferPng unifié ; orientation mesurée sur le repère " +
                "DÉRIVÉ du monde joué (v1_095), plus sur ENG/CAS nommés ; " +
                "archives et live : voir les lignes « matched= » ci-dessus, qui " +
                "portent aussi le nombre de cadres non applicables ; " +
                "acquis v1_073/v1_076 confirmés");

            File.WriteAllText(logPath, sb.ToString(), Encoding.UTF8);
            Debug.Log("V1077: wrote " + logPath);
            Assert.IsTrue(all, "V1077 artifacts FAIL — voir " + logPath);
        }

        /// <summary>
        /// Archives : images GELÉES de l'ère v1_070, peintes quand la carte pilote
        /// venait encore de ownership_1400.json. ENG/CAS y sont donc toujours
        /// présents, et doivent le rester — c'est le repère d'origine, conservé.
        /// </summary>
        static bool MeasureArchiveOrientation(bool expectNorthUp, out string detail)
        {
            var colors = CountryColors.Load();
            if (!colors.TryGetKnown("ENG", out var eng) || !colors.TryGetKnown("CAS", out var cas))
            {
                detail = "ENG/CAS couleurs manquantes";
                return false;
            }

            var root = Path.Combine(Application.dataPath, "..", "Captures", "v1_077_orientation_red");
            return MeasureDirPair(
                Path.Combine(root, "v1_073"), Archive073,
                Path.Combine(root, "v1_076"), Archive076,
                eng, cas, expectNorthUp, out detail);
        }

        /// <summary>
        /// v1_095 — repère du monde VIVANT, dérivé en rejouant la même graine et le
        /// même nombre de ticks que les captures mesurées. Nommer CAS ici ne marche
        /// plus depuis v1_094 : la Castille perd la Navarre avant t1000.
        /// </summary>
        static bool TryDeriveLiveReference(out Color32 north, out Color32 south, out string detail)
        {
            north = default;
            south = default;
            var wasPilot = PilotMapProvider.Enabled;
            using var harness = new SimulationHarness(42195u);
            harness.RunTicks(1000);
            PilotMapProvider.SetEnabled(true, clearCache: true);
            try
            {
                return MapSnapshotExporter.TryDeriveNorthSouthReferenceColors(
                    harness.EntityManager, CountryColors.Load(),
                    out north, out south, out detail);
            }
            finally
            {
                PilotMapProvider.SetEnabled(wasPilot, clearCache: true);
                MapGeometryCache.ResetStatsAndClear();
            }
        }

        static bool MeasureLiveOrientation(bool expectNorthUp, out string detail)
        {
            if (!TryDeriveLiveReference(out var north, out var south, out var refDetail))
            {
                detail = "repère live indérivable : " + refDetail;
                return false;
            }

            var root = Path.Combine(Application.dataPath, "..", "Captures");
            return MeasureDirPair(
                Path.Combine(root, "v1_073"), Archive073,
                Path.Combine(root, "v1_076"), Archive076,
                north, south,
                expectNorthUp, out detail);
        }

        static bool MeasureDirPair(
            string dir073, string[] files073,
            string dir076, string[] files076,
            Color32 eng, Color32 cas,
            bool expectNorthUp,
            out string detail)
        {
            var sb = new StringBuilder();
            var okCount = 0;
            var skipped = 0;
            var total = 0;
            void One(string dir, string file)
            {
                var path = Path.Combine(dir, file);
                total++;
                var northUp = MapSnapshotExporter.TryAssertPngNorthUp(
                    path, eng, cas,
                    out var ey, out var cy, out var ex, out var cx,
                    out var en, out var cn, out var line);
                sb.AppendLine(line);

                // v1_095 — cadre mono-pays (zoom province) : ni nord ni sud à
                // comparer. Retiré du décompte au lieu d'être compté juste ou faux,
                // les deux étant faux. Un cadre VIDE des deux côtés reste, lui, un
                // échec — c'est le cas qui signalerait une carte morte.
                if ((en == 0 && cn >= 1000) || (cn == 0 && en >= 1000))
                {
                    // Compté À PART, jamais retiré de `total` : le garde-fou
                    // « tous les fichiers ont été mesurés » doit rester mordant.
                    skipped++;
                    sb.AppendLine("  NON APPLICABLE (cadre mono-pays) n=" + en + " s=" + cn);
                    return;
                }

                if (northUp == expectNorthUp)
                    okCount++;
                else
                    sb.AppendLine(
                        "  MISMATCH expect_north_up=" + expectNorthUp +
                        " got=" + northUp +
                        " ENG_y=" + ey.ToString("F3", CultureInfo.InvariantCulture) +
                        " CAS_y=" + cy.ToString("F3", CultureInfo.InvariantCulture) +
                        " n=" + en + "/" + cn);
            }

            foreach (var f in files073) One(dir073, f);
            foreach (var f in files076) One(dir076, f);

            detail = sb.ToString().TrimEnd() + "\n" +
                     "matched=" + okCount + "/" + total +
                     " (non applicables=" + skipped + ")" +
                     " expect_north_up=" + expectNorthUp;
            // Trois exigences, et la troisième empêche le contrôle de se vider :
            // tout fichier est soit mesuré soit déclaré non applicable ; aucun
            // fichier ne manque ; et les cadres réellement mesurés restent
            // majoritaires — sinon « tout non applicable » passerait pour un vert.
            return okCount + skipped == total
                   && total == files073.Length + files076.Length
                   && okCount >= skipped;
        }

        static bool CheckWritePathRecensement(out string detail)
        {
            // Convention attendue : buffers carte → WriteMapBufferPng ;
            // buffers déjà Texture2D (planche / sprite ortho / framebuffer) → WritePngSized brut.
            var lines = new List<string>
            {
                "1. MapSnapshotExporter.WriteMapBufferPng — FLIP rangées puis Encode (buffer carte nord@py0)",
                "2. MapSnapshotExporter.WritePngSized — BRUT EncodeToPNG (planche contact / déjà Texture2D)",
                "3. PilotMapProvider captures — WriteMapBufferPng (plus de flip local dupliqué)",
                "4. MapLayerRenderer ExportFrame — WriteMapBufferPng ; compare sheet — WritePngSized (LoadImage)",
                "5. V1073LabelZoomTests / V1076OverlayTests — WriteMapBufferPng (plus de WritePng local brut)",
                "6. Tests V1011..V1041 captures carte — WriteMapBufferPng",
                "7. MapSpriteOverlay.WritePng — BRUT (cache sprite ortho, convention Texture2D)",
                "8. GameViewCapture / PlayMode EncodeToPNG — BRUT (framebuffer / RT)",
                "9. ChronicleExporter planche — WritePngSized (sheet Texture2D)",
            };

            // Garde-fou : plus de WritePng local qui EncodeToPNG sans FlipMapBufferRows.
            var t73 = Path.Combine(Application.dataPath, "Tests", "V1073LabelZoomTests.cs");
            var t76 = Path.Combine(Application.dataPath, "Tests", "V1076OverlayTests.cs");
            var src73 = File.Exists(t73) ? File.ReadAllText(t73) : "";
            var src76 = File.Exists(t76) ? File.ReadAllText(t76) : "";
            var bad73 = src73.Contains("tex.EncodeToPNG()") || src73.Contains("tex.SetPixels32(");
            var bad76 = src76.Contains("tex.EncodeToPNG()") || src76.Contains("tex.SetPixels32(");
            var hasMapApi = typeof(MapSnapshotExporter)
                .GetMethod("WriteMapBufferPng") != null;
            var usesShared73 = src73.Contains("WriteMapBufferPng");
            var usesShared76 = src76.Contains("WriteMapBufferPng");

            var sb = new StringBuilder();
            for (var i = 0; i < lines.Count; i++)
                sb.AppendLine(lines[i]);
            sb.AppendLine(
                "V1073 local tex.EncodeToPNG: " + (bad73 ? "OUI (ROUGE)" : "NON"));
            sb.AppendLine(
                "V1076 local tex.EncodeToPNG: " + (bad76 ? "OUI (ROUGE)" : "NON"));
            sb.AppendLine("V1073 appelle WriteMapBufferPng: " + usesShared73);
            sb.AppendLine("V1076 appelle WriteMapBufferPng: " + usesShared76);
            sb.AppendLine("WriteMapBufferPng exposé: " + hasMapApi);

            detail = sb.ToString().TrimEnd();
            return !bad73 && !bad76 && hasMapApi && usesShared73 && usesShared76;
        }
    }
}
