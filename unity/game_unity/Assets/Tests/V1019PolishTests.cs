using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using VictoriaGame.Presentation;

namespace VictoriaGame.Tests
{
    /// <summary>
    /// Point d'entrée batchmode SANS -nographics :
    /// -executeMethod VictoriaGame.Tests.V1019PolishBatchRunner.Run
    /// </summary>
    public static class V1019PolishBatchRunner
    {
        public static void Run()
        {
            V1019PolishTests.RunAndWriteArtifacts();
            Debug.Log("V1019PolishBatchRunner: DONE");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
        }
    }

    /// <summary>
    /// v1_019 — preuve visuelle : carte polie (mer, côtes, frontières, labels pays, palette).
    /// 6 PNG dans Logs/v1_019_polish/ + métriques + reproductibilité octet-à-octet.
    /// Monde post-v1_018 (constantes actuelles), seed 42195, capture t1000.
    /// </summary>
    [TestFixture]
    public class V1019PolishTests
    {
        const uint Seed = 42195u;
        const int CaptureTick = 1000;

        [Test]
        public void V1019_PolishMapSnapshots() => RunAndWriteArtifacts();

        public static void RunAndWriteArtifacts()
        {
            var polishDir = Path.Combine(
                Application.dataPath, "..", "Logs", "v1_019_polish");
            var polishLogPath = Path.Combine(
                Application.dataPath, "..", "Logs", "v1_019_polish.log");

            Directory.CreateDirectory(polishDir);
            Directory.CreateDirectory(Path.GetDirectoryName(polishLogPath)!);

            var sb = new StringBuilder(8192);
            sb.AppendLine($"=== v1_019 MAP POLISH seed={Seed} captureTick=t{CaptureTick} ===");
            sb.AppendLine(MapSnapshotExporter.FormatConstantsLine());
            sb.AppendLine(
                "CHANGEMENTS: mer ardoise lisible ; côtes + léger relief ; " +
                "frontières pays épaisses (r=2) vs internes discrètes ; " +
                "UN nom de pays / centroïde (collision déterministe) ; " +
                "palette country_colors.json retouchée ; détroits GraphStraitEdge conservés.");
            sb.AppendLine("Géométrie INCHANGÉE (CellRadius/corridors/straits/BuildMapGeometry).");
            sb.AppendLine();

            var palettes = MapLayerRenderer.LoadPalettes();
            var domains = MapLayerRenderer.GetFixedDomains(palettes);
            var colors = CountryColors.Load();
            var sea = colors.Sea;

            sb.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "SEA hex={0} unowned={1}",
                CountryColors.ToHex(sea), CountryColors.ToHex(colors.Unowned)));
            sb.AppendLine(MapLayerRenderer.FormatDomainsLine(domains));
            sb.AppendLine();

            Color32[] politicalPixels = null;
            Color32[] politicalPixelsB = null;
            Color32[] satisfactionPixels = null;
            Color32[] populationPixels = null;
            Color32[] armyPixels = null;
            Color32[] tradenodePixels = null;

            using (var harness = new SimulationHarness(Seed))
            {
                harness.RunTicks(CaptureTick);
                var geo = MapSnapshotExporter.BuildMapGeometry(
                    MapSnapshotExporter.Width, MapSnapshotExporter.Height);
                Assert.IsNotNull(geo, "MapGeometry null.");
                sb.AppendLine($"GEOMETRY {geo.Width}x{geo.Height} {geo.LandMasses.Summary}");
                sb.AppendLine(MapSnapshotExporter.FormatMaskStatsLine());
                sb.AppendLine();

                var frame = MapLayerRenderer.CaptureFrame(
                    harness.EntityManager, geo, colors, CaptureTick);

                politicalPixels = MapSnapshotExporter.ExportWithGeometryFromViews(
                    frame.PoliticalViews, CaptureTick,
                    Path.Combine(polishDir, "political.png"),
                    geo, drawLabels: true, tickCartouche: null, colors);

                var labelsPlaced = MapSnapshotExporter.LastLabelsPlaced;
                var labelsOmitted = MapSnapshotExporter.LastLabelsOmitted;

                // Reproductibilité : 2e rendu identique octet-à-octet (labels inclus).
                politicalPixelsB = MapSnapshotExporter.ExportWithGeometryFromViews(
                    frame.PoliticalViews, CaptureTick,
                    Path.Combine(polishDir, "political_repro.png"),
                    geo, drawLabels: true, tickCartouche: null, colors);

                satisfactionPixels = RenderAndWrite(
                    geo, frame, MapLayerRenderer.LayerKind.Satisfaction,
                    palettes, domains, colors, Path.Combine(polishDir, "satisfaction.png"));
                populationPixels = RenderAndWrite(
                    geo, frame, MapLayerRenderer.LayerKind.Population,
                    palettes, domains, colors, Path.Combine(polishDir, "population.png"));
                armyPixels = RenderAndWrite(
                    geo, frame, MapLayerRenderer.LayerKind.Army,
                    palettes, domains, colors, Path.Combine(polishDir, "army.png"));
                tradenodePixels = RenderAndWrite(
                    geo, frame, MapLayerRenderer.LayerKind.TradeNode,
                    palettes, domains, colors, Path.Combine(polishDir, "tradenode.png"));

                sb.AppendLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "LABELS placed={0} omitted={1} (omission déterministe si collision irréductible)",
                    labelsPlaced, labelsOmitted));
                sb.AppendLine();
            }

            var reports = new[]
            {
                Describe("political.png", Path.Combine(polishDir, "political.png"),
                    politicalPixels, sea),
                Describe("satisfaction.png", Path.Combine(polishDir, "satisfaction.png"),
                    satisfactionPixels, sea),
                Describe("population.png", Path.Combine(polishDir, "population.png"),
                    populationPixels, sea),
                Describe("army.png", Path.Combine(polishDir, "army.png"),
                    armyPixels, sea),
                Describe("tradenode.png", Path.Combine(polishDir, "tradenode.png"),
                    tradenodePixels, sea),
                Describe("political_repro.png", Path.Combine(polishDir, "political_repro.png"),
                    politicalPixelsB, sea),
            };

            const int legendBand = 72;
            var diffPolArmy = MapSnapshotExporter.CountPixelByteDiffs(
                politicalPixels, armyPixels,
                MapSnapshotExporter.Width, MapSnapshotExporter.Height, excludeBottomRows: 0);
            var diffSatPop = MapSnapshotExporter.CountPixelByteDiffs(
                satisfactionPixels, populationPixels,
                MapSnapshotExporter.Width, MapSnapshotExporter.Height, excludeBottomRows: legendBand);
            var diffRepro = MapSnapshotExporter.CountPixelByteDiffs(
                politicalPixels, politicalPixelsB,
                MapSnapshotExporter.Width, MapSnapshotExporter.Height, excludeBottomRows: 0);

            var hashA = Sha256File(Path.Combine(polishDir, "political.png"));
            var hashB = Sha256File(Path.Combine(polishDir, "political_repro.png"));
            var byteIdentical = hashA != null && hashA == hashB;

            var layersNonEmpty = true;
            for (var i = 0; i < 5; i++)
            {
                if (reports[i].NonBgPixels <= 0 || reports[i].FileBytes <= 0)
                    layersNonEmpty = false;
            }

            var layersDistinct = diffPolArmy > 0 && diffSatPop > 0;
            var reproOk = diffRepro == 0 && byteIdentical;
            var pipelineOk = layersNonEmpty && layersDistinct && reproOk;

            var verdict = new StringBuilder();
            verdict.AppendLine("=== VERDICT v1_019 POLISH ===");
            if (pipelineOk)
            {
                verdict.AppendLine(
                    "VERDICT: OUI — 6 PNG non vides, couches distinctes, labels déterministes (repro OK).");
            }
            else
            {
                verdict.AppendLine("VERDICT: NON — voir métriques ci-dessous.");
                if (!layersNonEmpty)
                    verdict.AppendLine("SYMPTÔME: couche vide.");
                if (!layersDistinct)
                    verdict.AppendLine("SYMPTÔME: pixeldiff nul entre couches.");
                if (!reproOk)
                    verdict.AppendLine("SYMPTÔME: deux captures political non identiques.");
            }

            verdict.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "LAYERS_NONEMPTY={0} LAYERS_DISTINCT={1} REPRO={2}",
                layersNonEmpty ? "OK" : "FAIL",
                layersDistinct ? "OK" : "FAIL",
                reproOk ? "OK" : "FAIL"));
            verdict.AppendLine();

            var body = new StringBuilder();
            body.AppendLine("=== IMAGES Logs/v1_019_polish/ ===");
            for (var i = 0; i < reports.Length; i++)
                body.AppendLine(FormatReport(reports[i]));
            body.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "PIXELDIFF political vs army bytes={0} {1}",
                diffPolArmy, diffPolArmy > 0 ? "OK" : "FAIL"));
            body.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "PIXELDIFF satisfaction vs population bytes={0} (hors légende) {1}",
                diffSatPop, diffSatPop > 0 ? "OK" : "FAIL"));
            body.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "PIXELDIFF political vs political_repro bytes={0} sha256_match={1} {2}",
                diffRepro, byteIdentical, reproOk ? "OK" : "FAIL"));
            body.AppendLine($"SHA256 political.png={hashA}");
            body.AppendLine($"SHA256 political_repro.png={hashB}");
            body.AppendLine();
            body.Append(verdict);

            var fullLog = verdict.ToString() + sb + body;
            File.WriteAllText(polishLogPath, fullLog);
            Debug.Log(fullLog);

            Assert.IsTrue(pipelineOk, "v1_019 polish KO — voir Logs/v1_019_polish.log");
            Assert.IsTrue(File.Exists(Path.Combine(polishDir, "political.png")));
            Assert.IsTrue(File.Exists(Path.Combine(polishDir, "satisfaction.png")));
            Assert.IsTrue(File.Exists(Path.Combine(polishDir, "population.png")));
            Assert.IsTrue(File.Exists(Path.Combine(polishDir, "army.png")));
            Assert.IsTrue(File.Exists(Path.Combine(polishDir, "tradenode.png")));
            Assert.IsTrue(File.Exists(polishLogPath));
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
            Debug.Log($"V1019: {kind} → {path}");
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

        static ImageReport Describe(string name, string path, Color32[] pixels, Color32 sea)
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
                Width = MapSnapshotExporter.Width,
                Height = MapSnapshotExporter.Height,
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
            var set = new System.Collections.Generic.HashSet<int>();
            for (var i = 0; i < pixels.Length; i++)
            {
                var c = pixels[i];
                set.Add((c.r << 16) | (c.g << 8) | c.b);
            }

            return set.Count;
        }

        static string Sha256File(string path)
        {
            if (!File.Exists(path))
                return null;
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
