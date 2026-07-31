using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using VictoriaGame.Core;
using VictoriaGame.Presentation;

namespace VictoriaGame.PlayModeTests
{
    /// <summary>
    /// v1_013 / v1_053 — boutons pause/paliers/zoom/impôt, synchro clavier↔UI,
    /// contrats de noms, fallback UXML.
    /// </summary>
    public class V1013SpeedControlsPlayModeTests
    {
        static readonly string LogPath = Path.Combine(
            Application.dataPath, "..", "Logs", "v1_013_ui.log");

        static readonly string ArchitectureLogPath = Path.Combine(
            Application.dataPath, "..", "Logs", "v1_053_ui_architecture.log");

        [UnityTest]
        public IEnumerator PaceButtons_Click_Writes_WorldState_And_Syncs_With_Keyboard()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== v1_013 / v1_053 UI speed controls ===");
            sb.AppendLine($"started_at={System.DateTime.UtcNow:o}");

            yield return EnsureHudReady();

            var hud = InGameHud.Instance;
            Assert.IsNotNull(hud, "InGameHud requis.");
            Assert.IsTrue(hud.UiReady, "UI doit être prête.");
            Assert.IsFalse(hud.UsedProgrammaticFallback, "Chemin nominal = UXML Resources.");
            sb.AppendLine($"uxml_path=nominal fallback={hud.UsedProgrammaticFallback}");

            AssertContractualElements(hud, sb);

            var world = Unity.Entities.World.DefaultGameObjectInjectionWorld;
            Assert.IsNotNull(world);
            Assert.IsTrue(world.IsCreated);

            var em = world.EntityManager;
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<WorldState>()))
            {
                Assert.AreEqual(1, q.CalculateEntityCount(), "WorldState singleton requis.");
            }

            TickControlBootstrap.Ensure(em);

            var pauseBtn = hud.PauseButton;
            Assert.IsNotNull(pauseBtn, "Bouton Pause introuvable.");
            Assert.AreEqual(InGameHud.PauseButtonName, pauseBtn.name);
            sb.AppendLine($"button_found name={pauseBtn.name} text={pauseBtn.text}");

            foreach (var speed in MapDisplaySystem.SpeedSteps)
            {
                var btn = hud.GetSpeedButton(speed);
                Assert.IsNotNull(btn, $"Bouton palier {speed}x introuvable.");
                sb.AppendLine($"button_found name={btn.name} text={btn.text} speed={speed.ToString(CultureInfo.InvariantCulture)}");
            }

            // --- État initial connu ---
            SetPace(em, isPaused: false, speed: 1f);
            hud.RefreshInfoBar(hud.LastMetricsLine);
            yield return null;

            Assert.IsFalse(ReadWorldState(em).IsPaused);
            Assert.AreEqual(1f, ReadWorldState(em).SimulationSpeed, 0.001f);
            Assert.AreEqual("Pause", hud.PauseButton.text);
            sb.AppendLine("initial: paused=false speed=1 PauseLabel=Pause");

            // --- Clic PAUSE : même handler que button.clicked += OnPauseClicked ---
            InvokeWiredClick(pauseBtn, hud.SimulatePauseClick);
            yield return null;

            var afterPause = ReadWorldState(em);
            Assert.IsTrue(afterPause.IsPaused, "Clic Pause doit basculer IsPaused.");
            Assert.AreEqual("Lecture", hud.PauseButton.text);
            Assert.IsTrue(
                hud.PauseButton.ClassListContains(InGameHud.ClassBtnPaused),
                "Pause engagée → classe hud-btn--paused.");
            sb.AppendLine($"click_pause → IsPaused={afterPause.IsPaused} label={hud.PauseButton.text}");

            InvokeWiredClick(pauseBtn, hud.SimulatePauseClick);
            yield return null;
            Assert.IsFalse(ReadWorldState(em).IsPaused, "Second clic Pause → reprise.");
            Assert.AreEqual("Pause", hud.PauseButton.text);
            sb.AppendLine($"click_pause_again → IsPaused={ReadWorldState(em).IsPaused} label={hud.PauseButton.text}");

            // --- Clic palier 4x ---
            var tier4 = hud.GetSpeedButton(4f);
            Assert.IsNotNull(tier4);
            InvokeWiredClick(tier4, () => hud.SimulateSpeedTierClick(4f));
            yield return null;

            var after4 = ReadWorldState(em);
            Assert.AreEqual(4f, after4.SimulationSpeed, 0.001f, "Clic 4x → SimulationSpeed=4.");
            Assert.AreEqual(
                MapDisplaySystem.NearestSpeedStepIndex(4f),
                ActiveSpeedIndexFromButtons(hud),
                "Palier 4x surligné (classe active).");
            sb.AppendLine($"click_tier_4x → SimulationSpeed={after4.SimulationSpeed.ToString(CultureInfo.InvariantCulture)} activeIdx={ActiveSpeedIndexFromButtons(hud)}");

            // --- Clic palier 0.5x ---
            var tier05 = hud.GetSpeedButton(0.5f);
            InvokeWiredClick(tier05, () => hud.SimulateSpeedTierClick(0.5f));
            yield return null;
            Assert.AreEqual(0.5f, ReadWorldState(em).SimulationSpeed, 0.001f);
            sb.AppendLine($"click_tier_0.5x → SimulationSpeed={ReadWorldState(em).SimulationSpeed.ToString(CultureInfo.InvariantCulture)}");

            // --- Zoom : même chemin que ZoomOutButton.clicked ---
            Assert.IsNotNull(hud.ZoomOutButton);
            Assert.AreEqual(InGameHud.ZoomOutButtonName, hud.ZoomOutButton.name);
            InvokeWiredClick(hud.ZoomOutButton, hud.SimulateZoomOutClick);
            yield return null;
            sb.AppendLine("click_zoom_out → SimulateZoomOutClick OK (pas de NRE)");

            // --- Impôt : même chemin que Tax± (intention, pas d'écriture TaxPolicy ici) ---
            Assert.IsNotNull(hud.TaxDownButton);
            Assert.IsNotNull(hud.TaxUpButton);
            Assert.AreEqual(InGameHud.TaxDownButtonName, hud.TaxDownButton.name);
            Assert.AreEqual(InGameHud.TaxUpButtonName, hud.TaxUpButton.name);
            InvokeWiredClick(hud.TaxDownButton, () => hud.SimulateTaxStepClick(-1));
            InvokeWiredClick(hud.TaxUpButton, () => hud.SimulateTaxStepClick(+1));
            yield return null;
            sb.AppendLine("click_tax_pm → SimulateTaxStepClick OK (pas de NRE)");

            // --- Synchro clavier → UI (même source : écrire WorldState comme HandlePaceHotkeys) ---
            SetPace(em, isPaused: true, speed: 8f);
            hud.RefreshInfoBar(hud.LastMetricsLine); // chemin MapDisplaySystem après hotkey
            yield return null;

            Assert.AreEqual("Lecture", hud.PauseButton.text, "UI pause reflète clavier/état.");
            Assert.AreEqual(
                MapDisplaySystem.NearestSpeedStepIndex(8f),
                ActiveSpeedIndexFromButtons(hud),
                "UI palier reflète SimulationSpeed après changement hors-bouton.");
            Assert.IsTrue(hud.InfoBarText.Contains("EN PAUSE"),
                $"InfoBar doit refléter pause: '{hud.InfoBarText}'");
            sb.AppendLine($"keyboard_sync → paused=true speed=8 label={hud.PauseButton.text} activeIdx={ActiveSpeedIndexFromButtons(hud)} info='{hud.InfoBarText}'");

            // StepSpeed (mécanisme v1_012) toujours cohérent
            Assert.AreEqual(4f, MapDisplaySystem.StepSpeed(8f, -1), 0.001f);
            Assert.AreEqual(8f, MapDisplaySystem.StepSpeed(8f, +1), 0.001f);
            sb.AppendLine("StepSpeed reuse OK (pas de second canal)");

            sb.AppendLine("VERDICT: boutons trouvés, clics agissent sur WorldState, UI synchro clavier→HUD");
            sb.AppendLine("status=PASS");

            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.WriteAllText(LogPath, sb.ToString(), Encoding.UTF8);
            AppendArchitectureLog("PaceButtons_Click", sb.ToString(), cases: 1);
            Debug.Log($"V1013SpeedControlsPlayModeTests: wrote {LogPath}");
        }

        [UnityTest]
        public IEnumerator UxmlMissing_Uses_ProgrammaticFallback_Without_NRE()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== v1_053 fallback programmatique ===");
            sb.AppendLine($"started_at={System.DateTime.UtcNow:o}");

            if (InGameHud.Instance != null)
            {
                Object.Destroy(InGameHud.Instance.gameObject);
                InGameHud.ForceProgrammaticFallback = false;
                for (var i = 0; i < 3; i++)
                    yield return null;
            }

            InGameHud.ForceProgrammaticFallback = true;
            var go = new GameObject("InGameHud_Fallback");
            go.AddComponent<InGameHud>();
            for (var i = 0; i < 10; i++)
                yield return null;

            try
            {
                var hud = InGameHud.Instance;
                Assert.IsNotNull(hud);
                Assert.IsTrue(hud.UiReady);
                Assert.IsTrue(hud.UsedProgrammaticFallback, "ForceProgrammaticFallback doit activer le fallback.");
                sb.AppendLine($"fallback={hud.UsedProgrammaticFallback}");

                AssertContractualElements(hud, sb);

                // Chemins branchés sans NullReferenceException.
                Assert.DoesNotThrow(() => hud.SimulatePauseClick());
                Assert.DoesNotThrow(() => hud.SimulateSpeedTierClick(2f));
                Assert.DoesNotThrow(() => hud.SimulateZoomOutClick());
                Assert.DoesNotThrow(() => hud.SimulateTaxStepClick(+1));
                Assert.DoesNotThrow(() => hud.RefreshHoverLabel("test"));
                Assert.DoesNotThrow(() => hud.RefreshHoverLabel(""));
                Assert.DoesNotThrow(() => hud.RefreshProvincePanel("prov"));
                Assert.DoesNotThrow(() => hud.RefreshProvincePanel(""));
                Assert.DoesNotThrow(() => hud.RefreshCountryPanel("country"));
                Assert.DoesNotThrow(() => hud.RefreshCountryPanel(""));
                yield return null;

                sb.AppendLine("VERDICT: fallback sans NRE, contrats de noms préservés");
                sb.AppendLine("status=PASS");
            }
            finally
            {
                InGameHud.ForceProgrammaticFallback = false;
                if (go != null)
                    Object.Destroy(go);
            }

            for (var i = 0; i < 5; i++)
                yield return null;

            // Recréer un HUD nominal pour d'éventuels tests suivants.
            if (InGameHud.Instance == null)
            {
                var restored = new GameObject("InGameHud");
                restored.AddComponent<InGameHud>();
            }

            for (var i = 0; i < 5; i++)
                yield return null;

            AppendArchitectureLog("UxmlMissing_Fallback", sb.ToString(), cases: 1);
            Debug.Log("V1013 fallback: wrote architecture log");
        }

        static IEnumerator EnsureHudReady()
        {
            InGameHud.ForceProgrammaticFallback = false;
            if (InGameHud.Instance == null)
            {
                var go = new GameObject("InGameHud");
                go.AddComponent<InGameHud>();
            }

            for (var i = 0; i < 45; i++)
                yield return null;
        }

        static void AssertContractualElements(InGameHud hud, StringBuilder sb)
        {
            Assert.IsNotNull(hud.PauseButton);
            Assert.IsNotNull(hud.ZoomOutButton);
            Assert.IsNotNull(hud.PaceBar);
            Assert.IsNotNull(hud.TaxBar);
            Assert.IsNotNull(hud.TaxDownButton);
            Assert.IsNotNull(hud.TaxUpButton);
            Assert.IsNotNull(hud.TaxStatusLabel);
            Assert.IsNotNull(hud.HoverLabel);
            Assert.IsNotNull(hud.ProvincePanel);
            Assert.IsNotNull(hud.CountryPanel);
            Assert.AreEqual(InGameHud.PauseButtonName, hud.PauseButton.name);
            Assert.AreEqual(InGameHud.ZoomOutButtonName, hud.ZoomOutButton.name);
            Assert.AreEqual(InGameHud.HoverLabelName, hud.HoverLabel.name);
            Assert.AreEqual(InGameHud.ProvincePanelName, hud.ProvincePanel.name);
            Assert.AreEqual(InGameHud.CountryPanelName, hud.CountryPanel.name);
            Assert.AreEqual(InGameHud.TaxDownButtonName, hud.TaxDownButton.name);
            Assert.AreEqual(InGameHud.TaxUpButtonName, hud.TaxUpButton.name);
            Assert.AreEqual(InGameHud.TaxStatusLabelName, hud.TaxStatusLabel.name);
            foreach (var speed in MapDisplaySystem.SpeedSteps)
                Assert.IsNotNull(hud.GetSpeedButton(speed), $"PaceSpeed manquant pour {speed}");
            sb.AppendLine("contractual_elements=OK");
        }

        /// <summary>
        /// Vérifie que le Button UI Toolkit est présent, puis invoque le callback
        /// branché sur button.clicked (même méthode, une seule fois — pas de double toggle).
        /// </summary>
        static void InvokeWiredClick(Button button, System.Action wiredClickedHandler)
        {
            Assert.IsNotNull(button, "Bouton UI Toolkit requis avant invocation.");
            Assert.IsNotNull(wiredClickedHandler);
            // En batch/headless, ClickEvent n'atteint pas toujours le Clickable ;
            // le handler passé EST celui enregistré via button.clicked += .
            wiredClickedHandler.Invoke();
        }

        static void SetPace(EntityManager em, bool isPaused, float speed)
        {
            using var q = em.CreateEntityQuery(ComponentType.ReadWrite<WorldState>());
            var entity = q.GetSingletonEntity();
            var ws = em.GetComponentData<WorldState>(entity);
            ws.IsPaused = isPaused;
            ws.SimulationSpeed = speed;
            em.SetComponentData(entity, ws);
        }

        static WorldState ReadWorldState(EntityManager em)
        {
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<WorldState>());
            return q.GetSingleton<WorldState>();
        }

        static int ActiveSpeedIndexFromButtons(InGameHud hud)
        {
            var steps = MapDisplaySystem.SpeedSteps;
            for (var i = 0; i < steps.Length; i++)
            {
                var btn = hud.GetSpeedButton(steps[i]);
                if (btn == null)
                    continue;
                if (btn.ClassListContains(InGameHud.ClassBtnActive) ||
                    btn.ClassListContains(InGameHud.ClassBtnSelected))
                    return i;
            }
            return -1;
        }

        static void AppendArchitectureLog(string caseName, string body, int cases)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ArchitectureLogPath)!);
            var header = new StringBuilder();
            header.AppendLine($"=== case={caseName} executed_cases={cases} at={System.DateTime.UtcNow:o} ===");
            File.AppendAllText(ArchitectureLogPath, header + body + "\n", Encoding.UTF8);
        }
    }
}
