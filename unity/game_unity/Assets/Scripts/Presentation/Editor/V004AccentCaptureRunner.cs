using System;
using System.IO;
using System.Linq;
using System.Text;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

namespace VictoriaGame.Presentation.Editor
{
    /// <summary>
    /// brief 004-polish-visuel — Success Condition 1 (accents) + Success Condition 5
    /// (galerie MapSnapshotExporter EditMode, même mécanisme que brief 003).
    ///
    /// Ce chemin (BuildMapGeometry / RenderPoliticalPixels / WriteMapBufferPng) est le
    /// chemin EditMode "une seule inversion documentée" (voir WriteMapBufferPng) — DIFFÉRENT
    /// du chemin interactif live (InGameHud.PresentFrame/PresentRenderTexture via Texture2D),
    /// qui affiche les étiquettes de la carte À L'ENVERS dans ce build standalone (constaté
    /// captures/v004_before, v004_after_default, v004_after_debug — TOUTES les captures
    /// standalone montrent ce défaut, y compris sur des noms SANS accent comme BOURGOGNE,
    /// ce qui prouve qu'il ne s'agit PAS d'un défaut de repli d'accent : défaut d'orientation
    /// générique du chemin interactif, hors périmètre des Success Conditions 1-4 de ce brief,
    /// consigné comme piste pour un brief futur dans generator-log.md).
    ///
    /// Batch : -executeMethod VictoriaGame.Presentation.Editor.V004AccentCaptureRunner.Run
    /// </summary>
    public static class V004AccentCaptureRunner
    {
        const int W = 960;
        const int H = 720;
        const string ProjectAssemblyName = "VictoriaGame";

        public static void Run()
        {
            var code = 1;
            try
            {
                code = RunInternal();
            }
            catch (Exception ex)
            {
                Debug.LogError("V004AccentCaptureRunner FAILED: " + ex);
                code = 1;
            }

            EditorApplication.Exit(code);
        }

        static int RunInternal()
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var capturesDir = Path.Combine(projectRoot, "Captures", "v004_accent");
            Directory.CreateDirectory(capturesDir);
            var logsDir = Path.Combine(projectRoot, "Logs");
            Directory.CreateDirectory(logsDir);
            var logPath = Path.Combine(logsDir, "v004_accent_capture.log");
            var log = new StringBuilder(4096);
            log.AppendLine("=== brief 004-polish-visuel — accent fold + gallery capture ===");
            log.AppendLine("started_at=" + DateTime.UtcNow.ToString("o"));

            global::Unity.Entities.World world = null;
            try
            {
                world = new global::Unity.Entities.World("V004AccentCaptureWorld");
                var projectSystems = DefaultWorldInitialization
                    .GetAllSystems(WorldSystemFilterFlags.Default)
                    .Where(t => t.Assembly.GetName().Name == ProjectAssemblyName)
                    .ToList();
                DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, projectSystems);
                world.GetExistingSystemManaged<InitializationSystemGroup>().Update();
                var simGroup = world.GetExistingSystemManaged<SimulationSystemGroup>();
                simGroup.Update();

                PilotMapProvider.SetEnabled(true, clearCache: true);
                MapGeometryCache.ResetStatsAndClear();
                MapViewport.Reset();

                // --- Preuve mécanique directe (indépendante du rendu) : le repli
                // d'accent réellement utilisé par le pipeline de labels de carte. ---
                var sanitizedIleDeFrance = MapSnapshotExporter.SanitizeLabelText("Île-de-France");
                log.AppendLine("sanitize_check name=Île-de-France output=" + sanitizedIleDeFrance +
                                " unmapped_count=" + MapSnapshotExporter.LastSanitizeUnmapped.Count);

                var battery = new[]
                {
                    "Île-de-France", "Châlons", "Kutná Hora", "Königsberg", "Lübeck",
                    "Târgoviște", "Besançon", "Liège", "Nimègue", "Nîmes", "Orléans"
                };
                var totalUnmapped = 0;
                foreach (var name in battery)
                {
                    var folded = MapSnapshotExporter.SanitizeLabelText(name);
                    var unmapped = MapSnapshotExporter.LastSanitizeUnmapped.Count;
                    totalUnmapped += unmapped;
                    log.AppendLine("sanitize_battery name=" + name + " output=" + folded +
                                    " unmapped_count=" + unmapped);
                }

                log.AppendLine("sanitize_battery_total_unmapped=" + totalUnmapped +
                                " sample=" + battery.Length);

                var geo = MapSnapshotExporter.BuildMapGeometry(W, H);
                if (geo == null)
                {
                    log.AppendLine("FAIL geometry null");
                    File.WriteAllText(logPath, log.ToString(), Encoding.UTF8);
                    return 2;
                }

                log.AppendLine("geometry views=" + geo.ViewsSkeleton.Count);

                var pixelsCountries = MapSnapshotExporter.RenderPoliticalPixels(
                    world.EntityManager, geo, MapSnapshotExporter.LabelDensity.Countries, -1);
                var pngCountries = Path.Combine(capturesDir, "01_world_country_labels.png");
                MapSnapshotExporter.WriteMapBufferPng(pixelsCountries, geo.Width, geo.Height, pngCountries);
                log.AppendLine("capture file=" + pngCountries);

                var pixelsProvinces = MapSnapshotExporter.RenderPoliticalPixels(
                    world.EntityManager, geo, MapSnapshotExporter.LabelDensity.Provinces, -1);
                var pngProvinces = Path.Combine(capturesDir, "02_world_province_labels.png");
                MapSnapshotExporter.WriteMapBufferPng(pixelsProvinces, geo.Width, geo.Height, pngProvinces);
                log.AppendLine("capture file=" + pngProvinces);

                File.WriteAllText(logPath, log.ToString(), Encoding.UTF8);
                Debug.Log("V004AccentCaptureRunner: DONE");
                return 0;
            }
            finally
            {
                PilotMapProvider.SetEnabled(false, clearCache: true);
                world?.Dispose();
            }
        }
    }
}
