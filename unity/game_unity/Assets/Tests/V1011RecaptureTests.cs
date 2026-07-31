using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using VictoriaGame.Presentation;

namespace VictoriaGame.Tests
{
    /// <summary>
    /// Point d'entrée batchmode (SANS -nographics) :
    /// -executeMethod VictoriaGame.Tests.V1011RecaptureBatchRunner.Run
    /// </summary>
    public static class V1011RecaptureBatchRunner
    {
        public static void Run()
        {
            V1011RecaptureTests.RunAndWriteArtifacts();
            Debug.Log("V1011RecaptureBatchRunner: DONE");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
        }
    }

    /// <summary>
    /// v1_011 — re-capture post-v1_010 : 5 couches thématiques + chronique légère,
    /// contrôle non-vacuité / pixeldiff, verdict pipeline offscreen (bureau séparé).
    /// Tick de capture documenté : t1000 (aligné sur les ancrages de référence).
    /// </summary>
    [TestFixture]
    public class V1011RecaptureTests
    {
        const uint Seed = 42195u;

        /// <summary>
        /// Tick de capture des 5 couches. t1000 choisi pour cohérence avec les ancrages
        /// post-v1_010 (army 44804, maxProv 8, annexed@800=7, countriesWithLand=17).
        /// </summary>
        const int CaptureTick = 1000;

        const int ChronicleTickA = 200;
        const int ChronicleTickB = 500;
        const int ChronicleTickC = 1000;

        [Test]
        public void V1011_RecaptureLayersAndChronicle() => RunAndWriteArtifacts();

        public static void RunAndWriteArtifacts()
        {
            var layersDir = Path.Combine(
                Application.dataPath, "..", "Logs", "v1_011_layers");
            var chronicleDir = Path.Combine(
                Application.dataPath, "..", "Logs", "v1_011_chronicle");
            var captureLogPath = Path.Combine(
                Application.dataPath, "..", "Logs", "v1_011_capture.log");

            Directory.CreateDirectory(layersDir);
            Directory.CreateDirectory(chronicleDir);
            Directory.CreateDirectory(Path.GetDirectoryName(captureLogPath)!);

            var sb = new StringBuilder(4096);
            sb.AppendLine($"=== v1_011 RECAPTURE seed={Seed} captureTick=t{CaptureTick} ===");
            sb.AppendLine(MapSnapshotExporter.FormatConstantsLine());
            sb.AppendLine(
                "Tick de capture = t1000 (cohérence ancrages post-v1_010). " +
                "Domaines FIXES v1_005 — pas d'auto-échelonnage. " +
                "Pipeline : CaptureFrame / RenderLayerToPixels / ExportWithGeometry.");
            sb.AppendLine();

            var palettes = MapLayerRenderer.LoadPalettes();
            var domains = MapLayerRenderer.GetFixedDomains(palettes);
            var colors = CountryColors.Load();
            var sea = colors.Sea;

            sb.AppendLine(MapLayerRenderer.FormatDomainsLine(domains));
            sb.AppendLine();

            Color32[] politicalPixels = null;
            Color32[] satisfactionPixels = null;
            Color32[] populationPixels = null;
            Color32[] armyPixels = null;
            Color32[] tradenodePixels = null;
            Color32[] chronicleA = null;
            Color32[] chronicleB = null;
            Color32[] chronicleC = null;

            ImageReport[] layerReports;
            ImageReport[] chronicleReports;

            using (var harness = new SimulationHarness(Seed))
            {
                harness.RunTicks(0);
                var geo = MapSnapshotExporter.BuildMapGeometry(
                    MapSnapshotExporter.Width, MapSnapshotExporter.Height);
                Assert.IsNotNull(geo, "MapGeometry null.");
                sb.AppendLine($"GEOMETRY {geo.Width}x{geo.Height} {geo.LandMasses.Summary}");
                sb.AppendLine(MapSnapshotExporter.FormatMaskStatsLine());
                sb.AppendLine();

                // --- PARTIE 2 (chemin unique 0→1000) : snapshots politiques espacés ---
                harness.RunTicks(ChronicleTickA);
                chronicleA = ExportPolitical(
                    harness, geo, colors, ChronicleTickA,
                    Path.Combine(chronicleDir, $"political_t{ChronicleTickA:D4}.png"));

                harness.RunTicks(ChronicleTickB - ChronicleTickA);
                chronicleB = ExportPolitical(
                    harness, geo, colors, ChronicleTickB,
                    Path.Combine(chronicleDir, $"political_t{ChronicleTickB:D4}.png"));

                harness.RunTicks(ChronicleTickC - ChronicleTickB);
                chronicleC = ExportPolitical(
                    harness, geo, colors, ChronicleTickC,
                    Path.Combine(chronicleDir, $"political_t{ChronicleTickC:D4}.png"));

                // --- PARTIE 1 : 5 couches thématiques au tick documenté ---
                Assert.AreEqual(CaptureTick, ChronicleTickC);
                var frame = MapLayerRenderer.CaptureFrame(
                    harness.EntityManager, geo, colors, CaptureTick);

                politicalPixels = MapSnapshotExporter.ExportWithGeometryFromViews(
                    frame.PoliticalViews, CaptureTick,
                    Path.Combine(layersDir, "political.png"),
                    geo, drawLabels: true, tickCartouche: null, colors);

                satisfactionPixels = RenderAndWrite(
                    geo, frame, MapLayerRenderer.LayerKind.Satisfaction,
                    palettes, domains, colors, Path.Combine(layersDir, "satisfaction.png"));
                populationPixels = RenderAndWrite(
                    geo, frame, MapLayerRenderer.LayerKind.Population,
                    palettes, domains, colors, Path.Combine(layersDir, "population.png"));
                armyPixels = RenderAndWrite(
                    geo, frame, MapLayerRenderer.LayerKind.Army,
                    palettes, domains, colors, Path.Combine(layersDir, "army.png"));
                tradenodePixels = RenderAndWrite(
                    geo, frame, MapLayerRenderer.LayerKind.TradeNode,
                    palettes, domains, colors, Path.Combine(layersDir, "tradenode.png"));
            }

            layerReports = new[]
            {
                Describe("political.png", Path.Combine(layersDir, "political.png"),
                    politicalPixels, sea, MapSnapshotExporter.Width, MapSnapshotExporter.Height),
                Describe("satisfaction.png", Path.Combine(layersDir, "satisfaction.png"),
                    satisfactionPixels, sea, MapSnapshotExporter.Width, MapSnapshotExporter.Height),
                Describe("population.png", Path.Combine(layersDir, "population.png"),
                    populationPixels, sea, MapSnapshotExporter.Width, MapSnapshotExporter.Height),
                Describe("army.png", Path.Combine(layersDir, "army.png"),
                    armyPixels, sea, MapSnapshotExporter.Width, MapSnapshotExporter.Height),
                Describe("tradenode.png", Path.Combine(layersDir, "tradenode.png"),
                    tradenodePixels, sea, MapSnapshotExporter.Width, MapSnapshotExporter.Height),
            };

            chronicleReports = new[]
            {
                Describe($"political_t{ChronicleTickA:D4}.png",
                    Path.Combine(chronicleDir, $"political_t{ChronicleTickA:D4}.png"),
                    chronicleA, sea, MapSnapshotExporter.Width, MapSnapshotExporter.Height),
                Describe($"political_t{ChronicleTickB:D4}.png",
                    Path.Combine(chronicleDir, $"political_t{ChronicleTickB:D4}.png"),
                    chronicleB, sea, MapSnapshotExporter.Width, MapSnapshotExporter.Height),
                Describe($"political_t{ChronicleTickC:D4}.png",
                    Path.Combine(chronicleDir, $"political_t{ChronicleTickC:D4}.png"),
                    chronicleC, sea, MapSnapshotExporter.Width, MapSnapshotExporter.Height),
            };

            // Pixeldiff couches (comme v1_006 / v1_008) — hors bandeau légende pour thématiques.
            const int legendBand = 72;
            var diffPolArmy = MapSnapshotExporter.CountPixelByteDiffs(
                politicalPixels, armyPixels,
                MapSnapshotExporter.Width, MapSnapshotExporter.Height, excludeBottomRows: 0);
            var diffSatPop = MapSnapshotExporter.CountPixelByteDiffs(
                satisfactionPixels, populationPixels,
                MapSnapshotExporter.Width, MapSnapshotExporter.Height, excludeBottomRows: legendBand);
            var diffChron = MapSnapshotExporter.CountPixelByteDiffs(
                chronicleA, chronicleC,
                MapSnapshotExporter.Width, MapSnapshotExporter.Height, excludeBottomRows: 0);

            var layersNonEmpty = true;
            for (var i = 0; i < layerReports.Length; i++)
            {
                if (layerReports[i].NonBgPixels <= 0 || layerReports[i].FileBytes <= 0)
                    layersNonEmpty = false;
            }

            var chronNonEmpty = true;
            for (var i = 0; i < chronicleReports.Length; i++)
            {
                if (chronicleReports[i].NonBgPixels <= 0 || chronicleReports[i].FileBytes <= 0)
                    chronNonEmpty = false;
            }

            var layersDistinct = diffPolArmy > 0 && diffSatPop > 0;
            var chronMoved = diffChron > 0;
            var pipelineOk = layersNonEmpty && layersDistinct;

            // VERDICT en tête — recopié ensuite en bas pour lecture chronologique.
            var verdictHead = new StringBuilder();
            verdictHead.AppendLine("=== VERDICT PIPELINE CAPTURE (bureau séparé / offscreen) ===");
            if (pipelineOk)
            {
                verdictHead.AppendLine(
                    "VERDICT: OUI — le pipeline de capture Unity produit des images PLEINES et DISTINCTES.");
            }
            else
            {
                verdictHead.AppendLine(
                    "VERDICT: NON — captures vides, uniformes ou couches identiques. " +
                    "Symptôme pour VICTORIA_NO_DESKTOP=1.");
                if (!layersNonEmpty)
                    verdictHead.AppendLine("SYMPTÔME: au moins une couche vide (0 pixel non-fond ou 0 octet).");
                if (!layersDistinct)
                    verdictHead.AppendLine(
                        "SYMPTÔME: pixeldiff nul entre couches (political/army ou sat/pop).");
            }

            verdictHead.AppendLine(
                $"LAYERS_NONEMPTY={(layersNonEmpty ? "OK" : "FAIL")} " +
                $"LAYERS_DISTINCT={(layersDistinct ? "OK" : "FAIL")} " +
                $"CHRONICLE_NONEMPTY={(chronNonEmpty ? "OK" : "FAIL")} " +
                $"CHRONICLE_MOVED={(chronMoved ? "OK" : "FAIL")}");
            verdictHead.AppendLine();

            var body = new StringBuilder();
            body.AppendLine("=== PARTIE 1 — couches thématiques (Logs/v1_011_layers/) ===");
            for (var i = 0; i < layerReports.Length; i++)
                body.AppendLine(FormatReport(layerReports[i]));
            body.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "PIXELDIFF political vs army bytes={0} {1}",
                diffPolArmy, diffPolArmy > 0 ? "OK" : "FAIL"));
            body.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "PIXELDIFF satisfaction vs population bytes={0} (hors légende) {1}",
                diffSatPop, diffSatPop > 0 ? "OK" : "FAIL"));
            body.AppendLine();

            body.AppendLine("=== PARTIE 2 — chronique politique (Logs/v1_011_chronicle/) ===");
            for (var i = 0; i < chronicleReports.Length; i++)
                body.AppendLine(FormatReport(chronicleReports[i]));
            body.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "PIXELDIFF political_t{0:D4} vs political_t{1:D4} bytes={2} {3}",
                ChronicleTickA, ChronicleTickC, diffChron, chronMoved ? "OK" : "FAIL"));
            body.AppendLine();

            body.Append(verdictHead);

            var fullLog = verdictHead.ToString() + sb + body;
            File.WriteAllText(captureLogPath, fullLog);
            Debug.Log(fullLog);

            // Assertions : rapportent l'échec de capture sans le maquiller.
            Assert.IsTrue(pipelineOk,
                "Pipeline capture KO — voir Logs/v1_011_capture.log (verdict NON).");
            Assert.IsTrue(chronNonEmpty, "Chronique : image vide.");
            Assert.IsTrue(chronMoved, "Chronique : political t200 identique à t1000.");
            Assert.IsTrue(File.Exists(Path.Combine(layersDir, "political.png")));
            Assert.IsTrue(File.Exists(Path.Combine(layersDir, "army.png")));
            Assert.IsTrue(File.Exists(Path.Combine(chronicleDir, "political_t0200.png")));
            Assert.IsTrue(File.Exists(Path.Combine(chronicleDir, "political_t1000.png")));
            Assert.IsTrue(File.Exists(captureLogPath));
        }

        static Color32[] ExportPolitical(
            SimulationHarness harness,
            MapSnapshotExporter.MapGeometry geo,
            CountryColors.Table colors,
            int tick,
            string path)
        {
            // CaptureFrame public (v1_008) → vues politiques figées au tick, sans BuildViewsAligned privé.
            var frame = MapLayerRenderer.CaptureFrame(
                harness.EntityManager, geo, colors, tick);
            return MapSnapshotExporter.ExportWithGeometryFromViews(
                frame.PoliticalViews, tick, path, geo,
                drawLabels: true, tickCartouche: null, colors);
        }

        static Color32[] RenderAndWrite(
            MapSnapshotExporter.MapGeometry geo,
            MapLayerRenderer.LayerFrame frame,
            MapLayerRenderer.LayerKind kind,
            MapLayerRenderer.Palettes palettes,
            MapLayerRenderer.FixedDomains domains,
            CountryColors.Table colors,
            string path)
        {
            var pixels = MapLayerRenderer.RenderLayerToPixels(
                geo, frame, kind, palettes, domains, colors);
            if (pixels == null)
                return null;
            MapSnapshotExporter.WriteMapBufferPng(pixels, geo.Width, geo.Height, path);
            Debug.Log($"V1011: {kind} → {path}");
            return pixels;
        }

        struct ImageReport
        {
            public string Name;
            public string Path;
            public int FileBytes;
            public int Width;
            public int Height;
            public int NonBgPixels;
            public int DistinctColors;
        }

        static ImageReport Describe(
            string name, string path, Color32[] pixels, Color32 sea, int w, int h)
        {
            var bytes = File.Exists(path) ? (int)new FileInfo(path).Length : 0;
            var nonBg = 0;
            var distinct = 0;
            if (pixels != null)
            {
                nonBg = CountNonBackground(pixels, sea);
                distinct = CountDistinctColors(pixels);
            }

            return new ImageReport
            {
                Name = name,
                Path = path,
                FileBytes = bytes,
                Width = w,
                Height = h,
                NonBgPixels = nonBg,
                DistinctColors = distinct
            };
        }

        static string FormatReport(in ImageReport r)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "IMAGE {0} path={1} file_bytes={2} dim={3}x{4} non_bg_pixels={5} distinct_colors={6} {7}",
                r.Name, r.Path, r.FileBytes, r.Width, r.Height, r.NonBgPixels, r.DistinctColors,
                r.NonBgPixels > 0 && r.FileBytes > 0 ? "OK" : "EMPTY");
        }

        static int CountNonBackground(Color32[] pixels, Color32 sea)
        {
            var n = 0;
            for (var i = 0; i < pixels.Length; i++)
            {
                var c = pixels[i];
                if (c.r != sea.r || c.g != sea.g || c.b != sea.b)
                    n++;
            }

            return n;
        }

        static int CountDistinctColors(Color32[] pixels)
        {
            // Hashset managé OK en test (hors chemin Burst / simulation).
            var set = new System.Collections.Generic.HashSet<int>();
            for (var i = 0; i < pixels.Length; i++)
            {
                var c = pixels[i];
                set.Add((c.r << 16) | (c.g << 8) | c.b);
            }

            return set.Count;
        }
    }
}
