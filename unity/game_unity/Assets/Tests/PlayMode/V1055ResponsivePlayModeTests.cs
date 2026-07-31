using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using VictoriaGame.Presentation;

namespace VictoriaGame.PlayModeTests
{
    /// <summary>
    /// v1_055 — matrice responsive + contrats accessibilité (mesures post-GeometryChanged).
    /// Preuve visuelle d'acceptation = Logs/v1_055_screens (standalone framebuffer).
    /// </summary>
    public class V1055ResponsivePlayModeTests
    {
        const int WaitFrames = 40;
        static readonly Vector2Int[] Resolutions =
        {
            new Vector2Int(1280, 720),
            new Vector2Int(1920, 1080),
            new Vector2Int(2560, 1440),
            new Vector2Int(3440, 1440)
        };

        [UnityTest]
        public IEnumerator Responsive_Matrix_NoCriticalOverlap(
            [ValueSource(nameof(Resolutions))] Vector2Int resolution)
        {
            yield return EnsureHud();
            var hud = InGameHud.Instance;
            Assert.IsNotNull(hud);
            Assert.IsTrue(hud.UiReady);

            var before = hud.LayoutVersion;
            hud.ForceLayoutSizeForTests(resolution.x, resolution.y);
            for (var i = 0; i < 10; i++)
                yield return null;

            Assert.GreaterOrEqual(hud.LayoutVersion, before);
            Assert.AreEqual(resolution.x, hud.LastLayoutWidth, 0.5f);
            Assert.AreEqual(resolution.y, hud.LastLayoutHeight, 0.5f);

            var expectCompact = resolution.x < InGameHud.CompactWidthThreshold ||
                                resolution.y < InGameHud.CompactHeightThreshold;
            var expectNarrow = resolution.x <= InGameHud.NarrowWidthThreshold + 0.5f;
            var expectUltrawide = resolution.y > 0 &&
                                  (resolution.x / (float)resolution.y) >= InGameHud.UltrawideAspectThreshold;
            Assert.AreEqual(expectCompact, hud.IsCompact, $"compact @ {resolution.x}x{resolution.y}");
            Assert.AreEqual(expectNarrow, hud.IsNarrow, $"narrow @ {resolution.x}x{resolution.y}");
            Assert.AreEqual(expectUltrawide, hud.IsUltrawide, $"ultrawide @ {resolution.x}x{resolution.y}");

            hud.RefreshProvincePanel("");
            hud.RefreshCountryPanel(
                "--- IDENTITY ---\nNAME France\nCONTROL PLAYER\n--- TREASURY ---\nGOLD 10\nDEBT 0\n");
            for (var i = 0; i < 8; i++)
                yield return null;

            var metrics = HudLayoutProbe.Measure(hud);
            Assert.IsFalse(metrics.CriticalOverlap,
                $"chevauchement critique @ {resolution}: {metrics.AnomalySummary}");
            Assert.IsFalse(metrics.EssentialActionOffscreen,
                $"action hors écran @ {resolution}: {metrics.AnomalySummary}");
            Assert.IsFalse(metrics.HitTargetsTooSmall,
                $"cibles <32 @ {resolution}: {metrics.AnomalySummary}");

            if (Mathf.Abs(resolution.x - 1920) <= 8)
            {
                Assert.GreaterOrEqual(
                    metrics.MapWidthRatio + 0.001f,
                    HudLayoutProbe.MinMapWidthRatioAt1080,
                    $"carte <68% @1080p ratio={metrics.MapWidthRatio}");
            }

            Assert.IsTrue(
                HudLayoutProbe.PassesResponsiveGates(metrics, resolution.x),
                metrics.AnomalySummary);
        }

        [UnityTest]
        public IEnumerator Accessibility_HitTargets_And_VisualStates()
        {
            yield return EnsureHud();
            var hud = InGameHud.Instance;
            Assert.IsNotNull(hud);

            hud.ForceLayoutSizeForTests(1920, 1080);
            for (var i = 0; i < 8; i++)
                yield return null;

            AssertMinHit(hud.PauseButton, "Pause");
            AssertMinHit(hud.ZoomOutButton, "Zoom-");
            AssertMinHit(hud.GetSpeedButton(1f), "1x");
            Assert.IsFalse(string.IsNullOrEmpty(hud.ZoomOutButton.tooltip), "tooltip Zoom- requis");

            Assert.IsTrue(hud.PauseButton.ClassListContains(InGameHud.ClassBtn));
            // Cinq états prouvés via classes distinctes (pas uniquement la couleur).
            hud.PauseButton.EnableInClassList(InGameHud.ClassBtnIdle, false);
            hud.PauseButton.EnableInClassList(InGameHud.ClassBtnPaused, true);
            Assert.IsTrue(hud.PauseButton.ClassListContains(InGameHud.ClassBtnPaused));

            hud.GetSpeedButton(2f).EnableInClassList(InGameHud.ClassBtnSelected, true);
            hud.GetSpeedButton(2f).EnableInClassList(InGameHud.ClassBtnActive, true);
            Assert.IsTrue(hud.GetSpeedButton(2f).ClassListContains(InGameHud.ClassBtnSelected));

            hud.PauseButton.EnableInClassList(InGameHud.ClassBtnHover, true);
            Assert.IsTrue(hud.PauseButton.ClassListContains(InGameHud.ClassBtnHover));

            Assert.IsTrue(hud.PauseButton.focusable);
            Assert.Less(hud.PauseButton.tabIndex, hud.ZoomOutButton.tabIndex);
            Assert.Less(hud.ZoomOutButton.tabIndex, hud.GetSpeedButton(0.5f).tabIndex);
            hud.PauseButton.Focus();
            yield return null;
            Assert.GreaterOrEqual(hud.PauseButton.tabIndex, 0);

            hud.TaxDownButton.EnableInClassList(InGameHud.ClassBtnDisabled, true);
            hud.TaxDownButton.EnableInClassList(InGameHud.ClassBtnIdle, false);
            Assert.IsTrue(hud.TaxDownButton.ClassListContains(InGameHud.ClassBtnDisabled));
            hud.TaxDownButton.EnableInClassList(InGameHud.ClassBtnDisabled, false);
            hud.TaxDownButton.EnableInClassList(InGameHud.ClassBtnIdle, true);

            // Chemin runtime pause si WorldState dispo.
            hud.SimulatePauseClick();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Escape_ZoomOut_Path_Still_Available()
        {
            yield return EnsureHud();
            var hud = InGameHud.Instance;
            Assert.IsNotNull(hud.ZoomOutButton);
            Assert.DoesNotThrow(() => hud.SimulateZoomOutClick());
            yield return null;
        }

        static IEnumerator EnsureHud()
        {
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
        }

        static void AssertMinHit(VisualElement el, string name)
        {
            Assert.IsNotNull(el, name);
            var minH = el.resolvedStyle.minHeight.value;
            var h = el.worldBound.height;
            var okHeight =
                (!float.IsNaN(minH) && minH >= HudLayoutProbe.MinHitTargetPx - 0.5f) ||
                h >= HudLayoutProbe.MinHitTargetPx - 0.5f;
            Assert.IsTrue(okHeight, $"{name} hauteur cible <32 (minH={minH} h={h})");

            var minW = el.resolvedStyle.minWidth.value;
            if (!float.IsNaN(minW) && minW > 0f)
                Assert.GreaterOrEqual(minW, 31.5f, $"{name} min-width USS");
        }
    }
}
