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
    /// -executeMethod VictoriaGame.Tests.V1028ShapeBatchRunner.Run
    /// </summary>
    public static class V1028ShapeBatchRunner
    {
        public static void Run()
        {
            V1028ShapeTests.RunAndWriteArtifacts();
            Debug.Log("V1028ShapeBatchRunner: DONE");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
        }
    }

    /// <summary>
    /// v1_028 — formes provinciales : mesures avant/après, masque dérivé des données,
    /// captures Logs/v1_028_shapes/, reproductibilité octet-à-octet.
    /// </summary>
    [TestFixture]
    public class V1028ShapeTests
    {
        const uint Seed = 42195u;
        const int CaptureTick = 1000;

        [Test]
        public void V1028_ProvinceShapes() => RunAndWriteArtifacts();

        public static void RunAndWriteArtifacts()
        {
            var shapesDir = Path.Combine(
                Application.dataPath, "..", "Logs", "v1_028_shapes");
            var shapesLogPath = Path.Combine(
                Application.dataPath, "..", "Logs", "v1_028_shapes.log");

            Directory.CreateDirectory(shapesDir);
            Directory.CreateDirectory(Path.GetDirectoryName(shapesLogPath)!);

            var sb = new StringBuilder(16384);
            sb.AppendLine($"=== v1_028 PROVINCE SHAPES seed={Seed} captureTick=t{CaptureTick} ===");
            sb.AppendLine(MapSnapshotExporter.FormatConstantsLine());
            sb.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "CONCAVE_HULL_DIG_FACTOR={0}",
                MapSnapshotExporter.ConcaveHullDigFactor));
            sb.AppendLine(
                "MASQUE: LegacyDisks (AVANT) = disques CellRadius ; " +
                "DataDerived (APRÈS) = alpha-shape Delaunay + faces d'adjacence + hex + corridors + détroits.");
            sb.AppendLine();

            var palettes = MapLayerRenderer.LoadPalettes();
            var domains = MapLayerRenderer.GetFixedDomains(palettes);
            var colors = CountryColors.Load();
            var sea = colors.Sea;

            sb.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "SEA hex={0}", CountryColors.ToHex(sea)));
            sb.AppendLine(MapLayerRenderer.FormatDomainsLine(domains));
            sb.AppendLine();

            MapSnapshotExporter.ShapeCoherenceReport before;
            MapSnapshotExporter.ShapeCoherenceReport after;
            MapSnapshotExporter.MaskBuildStats beforeStats;
            MapSnapshotExporter.MaskBuildStats afterStats;
            MapSnapshotExporter.LandMassReport beforeMasses;
            MapSnapshotExporter.LandMassReport afterMasses;

            Color32[] politicalBefore = null;
            Color32[] politicalAfter = null;
            Color32[] politicalRepro = null;
            Color32[] satisfactionPixels = null;
            Color32[] populationPixels = null;
            Color32[] armyPixels = null;
            Color32[] treasuryPixels = null;
            Color32[] tradenodePixels = null;
            int labelsPlaced = 0;
            int labelsOmitted = 0;

            using (var harness = new SimulationHarness(Seed))
            {
                harness.RunTicks(CaptureTick);

                // --- PARTIE 1 : géométrie LEGACY (disques) ---
                var geoBefore = MapSnapshotExporter.BuildMapGeometry(
                    MapSnapshotExporter.Width, MapSnapshotExporter.Height,
                    MapSnapshotExporter.LandMaskMode.LegacyDisks);
                Assert.IsNotNull(geoBefore, "MapGeometry legacy null.");
                before = MapSnapshotExporter.LastShapeReport;
                beforeStats = geoBefore.MaskStats;
                beforeMasses = geoBefore.LandMasses;

                sb.AppendLine("=== PARTIE 1 — MESURES AVANT (LegacyDisks) ===");
                sb.AppendLine(before.Summary);
                sb.AppendLine(MapSnapshotExporter.FormatMaskStatsLine());
                sb.AppendLine(beforeMasses.Summary);
                AppendNamedGaps(sb, "AVANT", before);
                sb.AppendLine();

                var frameBefore = MapLayerRenderer.CaptureFrame(
                    harness.EntityManager, geoBefore, colors, CaptureTick);
                politicalBefore = MapSnapshotExporter.ExportWithGeometryFromViews(
                    frameBefore.PoliticalViews, CaptureTick,
                    Path.Combine(shapesDir, "political_before.png"),
                    geoBefore, drawLabels: true, tickCartouche: null, colors);

                // --- PARTIE 2/3 : géométrie DataDerived ---
                var geoAfter = MapSnapshotExporter.BuildMapGeometry(
                    MapSnapshotExporter.Width, MapSnapshotExporter.Height,
                    MapSnapshotExporter.LandMaskMode.DataDerived);
                Assert.IsNotNull(geoAfter, "MapGeometry data-derived null.");
                after = MapSnapshotExporter.LastShapeReport;
                afterStats = geoAfter.MaskStats;
                afterMasses = geoAfter.LandMasses;

                sb.AppendLine("=== PARTIE 3 — MESURES APRÈS (DataDerived) ===");
                sb.AppendLine(after.Summary);
                sb.AppendLine(MapSnapshotExporter.FormatMaskStatsLine());
                sb.AppendLine(afterMasses.Summary);
                AppendNamedGaps(sb, "APRÈS", after);
                sb.AppendLine();

                var frame = MapLayerRenderer.CaptureFrame(
                    harness.EntityManager, geoAfter, colors, CaptureTick);

                politicalAfter = MapSnapshotExporter.ExportWithGeometryFromViews(
                    frame.PoliticalViews, CaptureTick,
                    Path.Combine(shapesDir, "political.png"),
                    geoAfter, drawLabels: true, tickCartouche: null, colors);
                labelsPlaced = MapSnapshotExporter.LastLabelsPlaced;
                labelsOmitted = MapSnapshotExporter.LastLabelsOmitted;

                politicalRepro = MapSnapshotExporter.ExportWithGeometryFromViews(
                    frame.PoliticalViews, CaptureTick,
                    Path.Combine(shapesDir, "political_repro.png"),
                    geoAfter, drawLabels: true, tickCartouche: null, colors);

                satisfactionPixels = RenderAndWrite(
                    geoAfter, frame, MapLayerRenderer.LayerKind.Satisfaction,
                    palettes, domains, colors, Path.Combine(shapesDir, "satisfaction.png"));
                populationPixels = RenderAndWrite(
                    geoAfter, frame, MapLayerRenderer.LayerKind.Population,
                    palettes, domains, colors, Path.Combine(shapesDir, "population.png"));
                armyPixels = RenderAndWrite(
                    geoAfter, frame, MapLayerRenderer.LayerKind.Army,
                    palettes, domains, colors, Path.Combine(shapesDir, "army.png"));
                treasuryPixels = RenderAndWrite(
                    geoAfter, frame, MapLayerRenderer.LayerKind.Treasury,
                    palettes, domains, colors, Path.Combine(shapesDir, "treasury.png"));
                tradenodePixels = RenderAndWrite(
                    geoAfter, frame, MapLayerRenderer.LayerKind.TradeNode,
                    palettes, domains, colors, Path.Combine(shapesDir, "tradenode.png"));
            }

            var diffBeforeAfter = MapSnapshotExporter.CountPixelByteDiffs(
                politicalBefore, politicalAfter,
                MapSnapshotExporter.Width, MapSnapshotExporter.Height, excludeBottomRows: 0);
            var diffRepro = MapSnapshotExporter.CountPixelByteDiffs(
                politicalAfter, politicalRepro,
                MapSnapshotExporter.Width, MapSnapshotExporter.Height, excludeBottomRows: 0);
            const int legendBand = 72;
            var diffPolArmy = MapSnapshotExporter.CountPixelByteDiffs(
                politicalAfter, armyPixels,
                MapSnapshotExporter.Width, MapSnapshotExporter.Height, excludeBottomRows: 0);
            var diffSatPop = MapSnapshotExporter.CountPixelByteDiffs(
                satisfactionPixels, populationPixels,
                MapSnapshotExporter.Width, MapSnapshotExporter.Height, excludeBottomRows: legendBand);

            var hashA = Sha256File(Path.Combine(shapesDir, "political.png"));
            var hashB = Sha256File(Path.Combine(shapesDir, "political_repro.png"));
            var byteIdentical = hashA != null && hashA == hashB;

            var layers = new[]
            {
                ("political.png", politicalAfter),
                ("satisfaction.png", satisfactionPixels),
                ("population.png", populationPixels),
                ("army.png", armyPixels),
                ("treasury.png", treasuryPixels),
                ("tradenode.png", tradenodePixels),
            };
            var layersNonEmpty = true;
            for (var i = 0; i < layers.Length; i++)
            {
                var path = Path.Combine(shapesDir, layers[i].Item1);
                if (!File.Exists(path) || new FileInfo(path).Length <= 0 ||
                    layers[i].Item2 == null || layers[i].Item2.Length == 0)
                    layersNonEmpty = false;
            }

            var layersDistinct = diffPolArmy > 0 && diffSatPop > 0;
            var reproOk = diffRepro == 0 && byteIdentical;
            var geometryChanged = diffBeforeAfter > 0;
            var pipelineOk = layersNonEmpty && layersDistinct && reproOk && geometryChanged;

            var verdict = new StringBuilder();
            verdict.AppendLine("=== VERDICT MESURÉ v1_028 ===");
            verdict.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "AVANT  coastal_touch={0}/{1} inland_false_sea={2}/{3} land_border={4}/{5} strait_glued={6}/{7}",
                before.CoastalTouchingSea, before.CoastalDeclared,
                before.InlandTouchingSea, before.InlandDeclared,
                before.LandAdjacencySharingBorder, before.LandAdjacencyPairs,
                before.StraitPairsGlued, before.StraitPairs));
            verdict.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "APRÈS  coastal_touch={0}/{1} inland_false_sea={2}/{3} land_border={4}/{5} strait_glued={6}/{7}",
                after.CoastalTouchingSea, after.CoastalDeclared,
                after.InlandTouchingSea, after.InlandDeclared,
                after.LandAdjacencySharingBorder, after.LandAdjacencyPairs,
                after.StraitPairsGlued, after.StraitPairs));
            verdict.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "MASK_COST legacyMs={0:F1} derivedMs={1:F1}",
                beforeStats.BuildMilliseconds, afterStats.BuildMilliseconds));
            verdict.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "LABELS placed={0} omitted={1}", labelsPlaced, labelsOmitted));
            verdict.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "PIXELDIFF before→after political bytes={0} REPRO bytes={1} sha_match={2}",
                diffBeforeAfter, diffRepro, byteIdentical));
            verdict.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "LAYERS_NONEMPTY={0} LAYERS_DISTINCT={1} REPRO={2} GEOMETRY_CHANGED={3}",
                layersNonEmpty ? "OK" : "FAIL",
                layersDistinct ? "OK" : "FAIL",
                reproOk ? "OK" : "FAIL",
                geometryChanged ? "OK" : "FAIL"));

            if (pipelineOk)
            {
                verdict.AppendLine(
                    "VERDICT: OUI — masque DataDerived livré, mesures avant/après publiées, " +
                    "écarts résiduels nommés, 6+ PNG, repro OK.");
            }
            else
            {
                verdict.AppendLine("VERDICT: NON — voir métriques / écarts nommés.");
            }

            sb.AppendLine("=== COÛT MASQUE ===");
            sb.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "legacy buildMs={0:F1} derived buildMs={1:F1} (GEOMETRY_BUILDS=1 inchangé côté affichage)",
                beforeStats.BuildMilliseconds, afterStats.BuildMilliseconds));
            sb.AppendLine();
            sb.AppendLine("=== IMAGES Logs/v1_028_shapes/ ===");
            sb.AppendLine("political_before.png political.png political_repro.png");
            sb.AppendLine("satisfaction.png population.png army.png treasury.png tradenode.png");
            sb.AppendLine($"SHA256 political.png={hashA}");
            sb.AppendLine($"SHA256 political_repro.png={hashB}");
            sb.AppendLine();
            sb.Append(verdict);

            var fullLog = verdict.ToString() + "\n" + sb;
            File.WriteAllText(shapesLogPath, fullLog);
            Debug.Log(fullLog);

            Assert.IsTrue(File.Exists(Path.Combine(shapesDir, "political_before.png")));
            Assert.IsTrue(File.Exists(Path.Combine(shapesDir, "political.png")));
            Assert.IsTrue(File.Exists(Path.Combine(shapesDir, "satisfaction.png")));
            Assert.IsTrue(File.Exists(Path.Combine(shapesDir, "population.png")));
            Assert.IsTrue(File.Exists(Path.Combine(shapesDir, "army.png")));
            Assert.IsTrue(File.Exists(Path.Combine(shapesDir, "treasury.png")));
            Assert.IsTrue(File.Exists(Path.Combine(shapesDir, "tradenode.png")));
            Assert.IsTrue(File.Exists(shapesLogPath));
            Assert.IsTrue(reproOk, "Deux captures political non identiques — voir v1_028_shapes.log");
            Assert.IsTrue(layersNonEmpty, "Couche vide — voir v1_028_shapes.log");
            Assert.IsTrue(layersDistinct, "Pixeldiff nul entre couches — voir v1_028_shapes.log");
            Assert.IsTrue(geometryChanged, "political_before == political — masque non changé");
        }

        static void AppendNamedGaps(
            StringBuilder sb, string tag, MapSnapshotExporter.ShapeCoherenceReport r)
        {
            sb.AppendLine($"--- ÉCARTS NOMMÉS ({tag}) ---");
            AppendList(sb, "coastal_missing_sea", r.CoastalMissingSea);
            AppendList(sb, "inland_false_sea", r.InlandFalseSea);
            AppendList(sb, "land_adj_missing_border", r.LandAdjMissingBorder);
            AppendList(sb, "strait_glued", r.StraitGluedNames);
        }

        static void AppendList(StringBuilder sb, string title, string[] items)
        {
            if (items == null || items.Length == 0)
            {
                sb.AppendLine($"{title}: (aucun)");
                return;
            }

            sb.AppendLine($"{title}: count={items.Length}");
            for (var i = 0; i < items.Length; i++)
                sb.AppendLine("  - " + items[i]);
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
            Debug.Log($"V1028: {kind} → {path}");
            return pixels;
        }

        static string Sha256File(string path)
        {
            if (!File.Exists(path))
                return null;
            using var fs = File.OpenRead(path);
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(fs);
            var sb = new StringBuilder(hash.Length * 2);
            for (var i = 0; i < hash.Length; i++)
                sb.Append(hash[i].ToString("x2"));
            return sb.ToString();
        }
    }
}
