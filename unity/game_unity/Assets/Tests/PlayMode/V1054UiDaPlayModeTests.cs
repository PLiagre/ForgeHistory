using System.Collections;
using System.Text;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VictoriaGame.Core;
using VictoriaGame.Presentation;

namespace VictoriaGame.PlayModeTests
{
    /// <summary>
    /// v1_054 — contrats DA (zones, pause). Compositeur synthétique retiré.
    /// Preuve visuelle d'acceptation = Logs/ui_002_screens (standalone framebuffer).
    /// </summary>
    public class V1054UiDaPlayModeTests
    {
        const int WaitFrames = 40;

        [UnityTest]
        public IEnumerator UiDa_Contracts_Without_Synthetic_Composer()
        {
            var log = new StringBuilder(1024);
            log.AppendLine("=== v1_054 contracts (post ui_003) ===");
            log.AppendLine("capture_composer=REMOVED → visual=Logs/ui_003_screens (standalone)");

            if (Application.CanStreamedLevelBeLoaded("Main"))
            {
                var op = SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
                if (op != null)
                    while (!op.isDone)
                        yield return null;
            }

            InGameHud.ForceProgrammaticFallback = false;
            if (InGameHud.Instance == null)
            {
                var go = new GameObject("InGameHud");
                go.AddComponent<InGameHud>();
            }

            for (var f = 0; f < WaitFrames; f++)
                yield return null;

            var hud = InGameHud.Instance;
            Assert.IsNotNull(hud);
            Assert.IsTrue(hud.UiReady);
            Assert.IsFalse(hud.UsedProgrammaticFallback);
            Assert.IsNotNull(hud.ViewContextLabel);
            Assert.IsNotNull(hud.DateLabel);
            Assert.IsNotNull(hud.PaceStatusBadge);

            var world = Unity.Entities.World.DefaultGameObjectInjectionWorld;
            Assert.IsNotNull(world);
            var em = world.EntityManager;

            using (var q = em.CreateEntityQuery(ComponentType.ReadWrite<WorldState>()))
            {
                var entity = q.GetSingletonEntity();
                var ws = em.GetComponentData<WorldState>(entity);
                ws.IsPaused = true;
                ws.SimulationSpeed = 1f;
                em.SetComponentData(entity, ws);
            }

            hud.RefreshInfoBar(hud.LastMetricsLine);
            yield return null;

            Assert.IsTrue(hud.PauseButton.ClassListContains(InGameHud.ClassBtnPaused));
            Assert.AreEqual("Lecture", hud.PauseButton.text);
            Assert.IsTrue(
                hud.InfoBarText.Contains("EN PAUSE") ||
                hud.PaceStatusBadge.text == "EN PAUSE",
                $"pause explicite: info='{hud.InfoBarText}' badge='{hud.PaceStatusBadge.text}'");

            log.AppendLine("VERDICT=A_REVOIR_HUMAINEMENT (captures → ui_003 standalone)");
            Debug.Log(log.ToString());
        }
    }
}
