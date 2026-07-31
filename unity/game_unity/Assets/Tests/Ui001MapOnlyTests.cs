using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using VictoriaGame.Politics;
using VictoriaGame.Presentation;
using VictoriaGame.Tests;

namespace VictoriaGame.EditModeTests
{
    /// <summary>
    /// ui_001 — preuve EditMode : texture map-only vs export diagnostique à panneau.
    /// </summary>
    public class Ui001MapOnlyTests
    {
        const uint Seed = 42195u;

        [Test]
        public void Interactive_MapOnly_Has_No_Diagnostic_Panel_While_Export_Keeps_It()
        {
            var log = new StringBuilder(2048);
            log.AppendLine("=== ui_001 map-only EditMode ===");

            MapViewport.Reset();
            MapGeometryCache.ResetStatsAndClear();

            using (var harness = new SimulationHarness(Seed))
            {
                harness.RunTicks(10);
                var em = harness.EntityManager;

                var geo = MapGeometryCache.GetOrBuild(
                    MapSnapshotExporter.Width, MapSnapshotExporter.Height, null, out _);
                Assert.IsNotNull(geo);

                var detail =
                    "--- IDENTITY ---\nCOUNTRY 0 TEST\n--- TREASURY ---\nGOLD   12.3\nDEBT   1.0\n" +
                    "--- TAX ---\nRATE   0.00002\n--- MILITARY ---\nARMY   100\n";

                var mapOnly = MapSnapshotExporter.RenderPoliticalPixels(
                    em, geo, MapSnapshotExporter.LabelDensity.Countries, -1,
                    overlay: _ => { /* map-only : aucun panneau */ });
                Assert.IsNotNull(mapOnly);
                Assert.IsFalse(
                    GameViewCapture.PixelsHaveDiagnosticPanelBg(mapOnly, geo.Width, geo.Height),
                    "Rendu map-only ne doit pas avoir le fond panneau");

                var withPanel = MapSnapshotExporter.RenderPoliticalPixels(
                    em, geo, MapSnapshotExporter.LabelDensity.Countries, -1,
                    overlay: p =>
                    {
                        MapSnapshotExporter.DrawProvinceDetailPanel(p, geo.Width, geo.Height, detail);
                    });
                Assert.IsNotNull(withPanel);
                Assert.IsTrue(
                    GameViewCapture.PixelsHaveDiagnosticPanelBg(withPanel, geo.Width, geo.Height),
                    "Export diagnostique doit conserver le panneau bitmap");

                var tax = HudValueFormatter.FormatTaxPercent(TaxPolicyLimits.DefaultProductionTaxRate);
                Assert.IsFalse(HudValueFormatter.ContainsScientificNotation(tax), tax);
                Assert.IsTrue(tax.Contains("%"), tax);
                log.AppendLine($"tax_format='{tax}'");
                log.AppendLine("map_only=PASS diagnostic_panel=PASS");
            }

            var path = Path.Combine(Application.dataPath, "..", "Logs", "ui_001_map_only.log");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, log.ToString(), Encoding.UTF8);
        }
    }
}
