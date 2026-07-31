using System;
using System.Collections;
using System.IO;
using System.Text;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using VictoriaGame.Core;
using VictoriaGame.Politics;
using VictoriaGame.Presentation;
using VictoriaGame.World;

namespace VictoriaGame.PlayModeTests
{
    /// <summary>
    /// ui_001 / ui_002 — contrats UI (map-only, libellés FR, pause).
    /// Preuve visuelle d'acceptation = player standalone (Logs/ui_002_screens),
    /// pas les captures Editor/batchmode (UI Toolkit souvent non rasterisé).
    /// </summary>
    public class Ui001PlayModeTests
    {
        static readonly string ContractLogPath = Path.Combine(
            Application.dataPath, "..", "Logs", "ui_001_contracts.log");

        const int WaitFrames = 30;

        [UnityTest]
        public IEnumerator Ui001_Contracts_MapOnly_ReadableHud_NoFakeVisualProof()
        {
            var log = new StringBuilder(8192);
            log.AppendLine("=== ui_001/ui_002 PlayMode contracts (no Editor visual acceptance) ===");
            log.AppendLine($"started_at={DateTime.UtcNow:o}");
            log.AppendLine("verdict_policy=A_REVOIR_HUMAINEMENT");
            log.AppendLine("visual_acceptance=Logs/ui_003_screens (standalone framebuffer)");
            log.AppendLine("editor_capture=disabled (no map-only fallback as HUD proof)");

            if (Application.CanStreamedLevelBeLoaded("Main"))
            {
                var op = SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
                if (op != null)
                    while (!op.isDone)
                        yield return null;
            }

            InGameHud.ForceProgrammaticFallback = false;
            InGameHud.ShowDebugIds = false;
            if (InGameHud.Instance == null)
            {
                var go = new GameObject("InGameHud");
                go.AddComponent<InGameHud>();
            }

            for (var f = 0; f < WaitFrames; f++)
                yield return null;

            var hud = InGameHud.Instance;
            Assert.IsNotNull(hud, "InGameHud requis");
            Assert.IsTrue(hud.UiReady);
            Assert.IsFalse(hud.UsedProgrammaticFallback, "Chemin UXML nominal requis");

            Assert.IsNotNull(hud.PauseButton);
            Assert.IsNotNull(hud.ZoomOutButton);
            Assert.IsNotNull(hud.HoverLabel);
            Assert.IsNotNull(hud.ProvincePanel);
            Assert.IsNotNull(hud.CountryPanel);
            Assert.IsNotNull(hud.TaxDownButton);
            Assert.IsNotNull(hud.TaxUpButton);
            Assert.IsNotNull(hud.TaxStatusLabel);
            Assert.IsNotNull(hud.PaceStatusBadge, "Badge EN PAUSE / VITESSE requis");

            var world = Unity.Entities.World.DefaultGameObjectInjectionWorld;
            Assert.IsNotNull(world);
            var em = world.EntityManager;

            SetPace(em, isPaused: false, speed: 1f);
            MapDisplaySystem.RequestRefresh();
            for (var i = 0; i < 20; i++)
                yield return null;

            MapViewport.ForceState(MapViewportState.World(MapViewport.WorldWindow));
            MapDisplaySystem.RequestRefresh();
            for (var i = 0; i < 15; i++)
                yield return null;

            string countryDetail = "";
            if (CountryObservation.TryCapture(em, PlayerControl.DefaultControlledCountryId, out var countrySnap))
                countryDetail = countrySnap.DetailBlock;
            Assert.IsFalse(string.IsNullOrEmpty(countryDetail));

            Entity countryEntity = Entity.Null;
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<CountryData>()))
            using (var entities = q.ToEntityArray(Unity.Collections.Allocator.Temp))
            using (var data = q.ToComponentDataArray<CountryData>(Unity.Collections.Allocator.Temp))
            {
                for (var i = 0; i < data.Length; i++)
                {
                    if (data[i].CountryId != PlayerControl.DefaultControlledCountryId)
                        continue;
                    countryEntity = entities[i];
                    break;
                }
            }

            MapViewport.SelectCountry(countryEntity, PlayerControl.DefaultControlledCountryId, MapViewport.WorldWindow);
            MapDisplaySystem.RequestRefresh();
            for (var i = 0; i < 15; i++)
                yield return null;
            hud.RefreshProvincePanel("");
            hud.RefreshCountryPanel(countryDetail);
            yield return null;

            Assert.IsNotNull(hud.MapTexture, "Texture interactive absente");
            Assert.IsFalse(
                GameViewCapture.TextureHasDiagnosticPanelBg(hud.MapTexture),
                "Texture interactive map-only ne doit pas contenir le fond du panneau bitmap diagnostique");
            log.AppendLine("map_only_interactive=PASS");

            var worldGeo = MapGeometryCache.GetOrBuild(
                MapSnapshotExporter.Width, MapSnapshotExporter.Height, null, out _);
            Assert.IsNotNull(worldGeo);
            var diagPixels = MapSnapshotExporter.RenderPoliticalPixels(
                em, worldGeo, MapSnapshotExporter.LabelDensity.Countries, -1,
                overlay: p =>
                {
                    MapSnapshotExporter.DrawProvinceDetailPanel(
                        p, worldGeo.Width, worldGeo.Height, countryDetail);
                });
            Assert.IsNotNull(diagPixels);
            Assert.IsTrue(
                GameViewCapture.PixelsHaveDiagnosticPanelBg(diagPixels, worldGeo.Width, worldGeo.Height),
                "Export diagnostique doit encore peindre le panneau bitmap");
            log.AppendLine("diagnostic_export_with_panel=PASS");

            hud.RefreshInfoBar(MapDisplaySystem.LastMetricsLine);
            yield return null;
            var metrics = MapDisplaySystem.LastMetricsLine ?? "";
            log.AppendLine($"metrics_line='{metrics}'");
            Assert.IsTrue(metrics.Contains("Trésor") || metrics.Contains("Dette"),
                $"Bandeau FR attendu: '{metrics}'");
            Assert.IsFalse(metrics.Contains("TICK"), "TICK masqué hors debug");
            Assert.IsFalse(HudValueFormatter.ContainsScientificNotation(metrics),
                "Notation scientifique interdite dans le bandeau");

            if (hud.TaxStatusLabel != null && !string.IsNullOrEmpty(hud.TaxStatusLabel.text))
            {
                Assert.IsFalse(
                    HudValueFormatter.ContainsScientificNotation(hud.TaxStatusLabel.text),
                    $"TaxStatus scientifique: '{hud.TaxStatusLabel.text}'");
                log.AppendLine($"tax_status='{hud.TaxStatusLabel.text}'");
            }

            // Pause explicite
            SetPace(em, isPaused: true, speed: 1f);
            hud.RefreshCountryPanel(countryDetail);
            hud.RefreshInfoBar(MapDisplaySystem.LastMetricsLine);
            yield return null;
            Assert.IsTrue(hud.PauseButton.ClassListContains(InGameHud.ClassBtnPaused));
            Assert.AreEqual("Lecture", hud.PauseButton.text);
            Assert.IsTrue(
                hud.InfoBarText.Contains("EN PAUSE") ||
                (hud.PaceStatusBadge != null && hud.PaceStatusBadge.text == "EN PAUSE"),
                $"État pause explicite attendu: info='{hud.InfoBarText}' badge='{hud.PaceStatusBadge?.text}'");
            log.AppendLine("pause_explicit=PASS");

            // Impôt bornes sans notation scientifique (intentions / UI)
            PlayerIntentionSubmit.EnqueueSetProductionTaxRate(
                em, PlayerControl.DefaultControlledCountryId, TaxPolicyLimits.MinProductionTaxRate);
            hud.SimulateTaxStepClick(-1);
            for (var i = 0; i < 8; i++)
                yield return null;
            Assert.IsFalse(
                HudValueFormatter.ContainsScientificNotation(hud.TaxStatusLabel.text ?? ""),
                $"Tax min scientifique: '{hud.TaxStatusLabel.text}'");

            PlayerIntentionSubmit.EnqueueSetProductionTaxRate(
                em, PlayerControl.DefaultControlledCountryId, TaxPolicyLimits.MaxProductionTaxRate);
            hud.SimulateTaxStepClick(+1);
            for (var i = 0; i < 8; i++)
                yield return null;
            Assert.IsFalse(
                HudValueFormatter.ContainsScientificNotation(hud.TaxStatusLabel.text ?? ""),
                $"Tax max scientifique: '{hud.TaxStatusLabel.text}'");
            log.AppendLine("tax_bounds_format=PASS");

            // Vérifie que CapturePngCoroutine échoue honnêtement si le panneau n'est pas peint
            // (batchmode) — jamais de fallback MapTexture accepté comme preuve HUD.
            GameViewCapture.CaptureResult probe = default;
            yield return GameViewCapture.CapturePngCoroutine(
                Path.Combine(Application.temporaryCachePath, "ui_002_editor_probe.png"),
                r => probe = r);
            if (probe.Success)
            {
                Assert.IsTrue(probe.HasHudChrome && probe.HasMapContent,
                    "Si Editor paint le HUD, chrome+carte requis");
                Assert.AreNotEqual(
                    "InGameHud.MapTexture",
                    probe.Source ?? "",
                    "Source map-only interdite");
                log.AppendLine($"editor_panel_probe=SUCCESS source={probe.Source}");
            }
            else
            {
                log.AppendLine($"editor_panel_probe=FAIL_HONEST error={probe.Error}");
                Assert.IsFalse(
                    (probe.Source ?? "").IndexOf("MapTexture", StringComparison.OrdinalIgnoreCase) >= 0,
                    "Aucun fallback MapTexture ne doit être présenté comme capture HUD");
            }

            log.AppendLine("VERDICT=A_REVOIR_HUMAINEMENT");
            log.AppendLine("status=CONTRACTS_PASS_VISUAL_SEE_UI_003_STANDALONE");

            Directory.CreateDirectory(Path.GetDirectoryName(ContractLogPath)!);
            File.WriteAllText(ContractLogPath, log.ToString(), Encoding.UTF8);
            Debug.Log($"Ui001PlayModeTests wrote {ContractLogPath}");

            // Contrats éditoriaux ui_003 sur le panneau peuplé (texte visible).
            var countryVisible = HudDetailPresenter.CollectVisibleText(hud.CountryPanel);
            Assert.IsFalse(
                HudDetailPresenter.ContainsForbiddenUserToken(countryVisible, out var forbidHit),
                $"Token interdit pays '{forbidHit}':\n{countryVisible}");
            Assert.IsTrue(
                countryVisible.IndexOf("Pays contrôlé", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                countryVisible.IndexOf("France", System.StringComparison.OrdinalIgnoreCase) >= 0,
                countryVisible);
            log.AppendLine("editorial_country=PASS");
            File.WriteAllText(ContractLogPath, log.ToString(), Encoding.UTF8);

            SetPace(em, isPaused: false, speed: 1f);
            MapViewport.ForceState(MapViewportState.World(MapViewport.WorldWindow));
        }

        static void SetPace(EntityManager em, bool isPaused, float speed)
        {
            using var q = em.CreateEntityQuery(ComponentType.ReadWrite<WorldState>());
            var entity = q.GetSingletonEntity();
            var ws = em.GetComponentData<WorldState>(entity);
            ws.IsPaused = isPaused;
            ws.SimulationSpeed = speed;
            em.SetComponentData(entity, ws);
            if (InGameHud.Instance != null)
                InGameHud.Instance.RefreshInfoBar(InGameHud.Instance.LastMetricsLine);
        }
    }
}
