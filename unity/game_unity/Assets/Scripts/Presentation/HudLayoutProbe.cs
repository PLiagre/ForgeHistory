using UnityEngine;
using UnityEngine.UIElements;

namespace VictoriaGame.Presentation
{
    /// <summary>
    /// Mesures de layout HUD après GeometryChangedEvent (v1_055).
    /// Aucune logique métier — bornes et chevauchements uniquement.
    /// </summary>
    public static class HudLayoutProbe
    {
        public const float MinMapWidthRatioAt1080 = 0.68f;
        public const float MinHitTargetPx = 32f;
        public const float OverlapEpsilon = 1f;

        public struct Metrics
        {
            public float RootWidth;
            public float RootHeight;
            public float PanelWidth;
            public float PanelHeight;
            public float TopBarHeight;
            public float MapWidthRatio;
            public bool Compact;
            public bool Narrow;
            public bool Ultrawide;
            public bool CriticalOverlap;
            public bool EssentialActionOffscreen;
            public bool HitTargetsTooSmall;
            public bool TruncationSuspected;
            public string AnomalySummary;
        }

        public static Metrics Measure(InGameHud hud)
        {
            var m = new Metrics
            {
                AnomalySummary = ""
            };
            if (hud == null || !hud.UiReady)
            {
                m.AnomalySummary = "hud_absent";
                m.CriticalOverlap = true;
                m.EssentialActionOffscreen = true;
                return m;
            }

            var doc = hud.GetComponent<UIDocument>();
            var root = doc != null ? doc.rootVisualElement : null;
            if (root == null)
            {
                m.AnomalySummary = "root_absent";
                return m;
            }

            var rootRect = root.worldBound;
            m.RootWidth = rootRect.width;
            m.RootHeight = rootRect.height;
            m.Compact = root.ClassListContains(InGameHud.ClassCompact);
            m.Narrow = root.ClassListContains(InGameHud.ClassNarrow);
            m.Ultrawide = root.ClassListContains(InGameHud.ClassUltrawide);

            VisualElement visiblePanel = null;
            if (hud.ProvincePanel != null && !hud.ProvincePanel.ClassListContains(InGameHud.ClassHidden))
                visiblePanel = hud.ProvincePanel;
            else if (hud.CountryPanel != null && !hud.CountryPanel.ClassListContains(InGameHud.ClassHidden))
                visiblePanel = hud.CountryPanel;

            if (visiblePanel != null)
            {
                var pr = visiblePanel.worldBound;
                m.PanelWidth = pr.width;
                m.PanelHeight = pr.height;
            }

            var topBar = root.Q<VisualElement>("TopBar");
            if (topBar != null)
                m.TopBarHeight = topBar.worldBound.height;

            if (m.RootWidth > 1f)
                m.MapWidthRatio = 1f - (m.PanelWidth / m.RootWidth);
            else
                m.MapWidthRatio = 1f;

            var anomalies = new System.Text.StringBuilder(256);

            if (topBar != null && visiblePanel != null &&
                RectsOverlap(topBar.worldBound, visiblePanel.worldBound))
            {
                m.CriticalOverlap = true;
                anomalies.Append("topbar_panel_overlap;");
            }

            if (hud.TaxBar != null &&
                !hud.TaxBar.ClassListContains(InGameHud.ClassHidden) &&
                visiblePanel != null &&
                hud.CountryPanel == visiblePanel)
            {
                // Tax empilé au-dessus du panneau pays : chevauchement volontaire exclu.
                var tax = hud.TaxBar.worldBound;
                var panel = visiblePanel.worldBound;
                if (tax.yMax > panel.yMax + OverlapEpsilon &&
                    tax.yMin < panel.yMin - OverlapEpsilon &&
                    RectsOverlap(tax, panel))
                {
                    m.CriticalOverlap = true;
                    anomalies.Append("tax_panel_full_overlap;");
                }
            }

            m.EssentialActionOffscreen =
                IsOffscreen(hud.PauseButton, rootRect) ||
                IsOffscreen(hud.ZoomOutButton, rootRect);
            if (hud.TaxBar != null && !hud.TaxBar.ClassListContains(InGameHud.ClassHidden))
            {
                m.EssentialActionOffscreen =
                    m.EssentialActionOffscreen ||
                    IsOffscreen(hud.TaxDownButton, rootRect) ||
                    IsOffscreen(hud.TaxUpButton, rootRect);
            }

            if (m.EssentialActionOffscreen)
                anomalies.Append("action_offscreen;");

            m.HitTargetsTooSmall = AnyHitTargetTooSmall(hud);
            if (m.HitTargetsTooSmall)
                anomalies.Append("hit_lt_32;");

            m.TruncationSuspected = SuspectTruncation(hud);
            if (m.TruncationSuspected)
                anomalies.Append("text_truncation;");

            // À 1080p nominal (±8 %), la carte libre doit rester >= 68 %.
            if (Mathf.Abs(m.RootWidth - 1920f) <= 160f &&
                m.MapWidthRatio + 0.001f < MinMapWidthRatioAt1080)
            {
                anomalies.Append("map_ratio_lt_68_at_1080;");
            }

            m.AnomalySummary = anomalies.Length == 0 ? "none" : anomalies.ToString();
            return m;
        }

        public static bool PassesResponsiveGates(in Metrics m, float nominalWidth)
        {
            if (m.CriticalOverlap || m.EssentialActionOffscreen || m.HitTargetsTooSmall)
                return false;
            if (Mathf.Abs(nominalWidth - 1920f) <= 160f &&
                m.MapWidthRatio + 0.001f < MinMapWidthRatioAt1080)
                return false;
            return true;
        }

        static bool AnyHitTargetTooSmall(InGameHud hud)
        {
            if (HitTooSmall(hud.PauseButton) || HitTooSmall(hud.ZoomOutButton))
                return true;
            var taxVisible = hud.TaxBar != null &&
                             !hud.TaxBar.ClassListContains(InGameHud.ClassHidden);
            if (taxVisible)
            {
                if (HitTooSmall(hud.TaxDownButton) || HitTooSmall(hud.TaxUpButton))
                    return true;
            }

            var speed = hud.GetSpeedButton(1f);
            return HitTooSmall(speed);
        }

        static bool HitTooSmall(VisualElement el)
        {
            if (el == null || IsEffectivelyHidden(el))
                return false;

            var minH = el.resolvedStyle.minHeight.value;
            var minW = el.resolvedStyle.minWidth.value;
            // USS min-height/min-width ≥ 32 : contrat style même avant Yoga layout.
            if (minH >= MinHitTargetPx - 0.5f && minW >= MinHitTargetPx - 0.5f)
                return false;

            var r = el.worldBound;
            if (r.width < 1f && r.height < 1f)
                return false; // pas encore layouté — ne pas faux-positif
            return r.width + 0.5f < MinHitTargetPx || r.height + 0.5f < MinHitTargetPx;
        }

        static bool IsEffectivelyHidden(VisualElement el)
        {
            for (var cur = el; cur != null; cur = cur.parent)
            {
                if (cur.ClassListContains(InGameHud.ClassHidden))
                    return true;
                if (cur.resolvedStyle.display == DisplayStyle.None)
                    return true;
            }

            return false;
        }

        static bool IsOffscreen(VisualElement el, Rect root)
        {
            if (el == null || el.ClassListContains(InGameHud.ClassHidden))
                return false;
            var r = el.worldBound;
            if (r.width < 1f || r.height < 1f)
                return true;
            return r.xMax < root.xMin + OverlapEpsilon ||
                   r.xMin > root.xMax - OverlapEpsilon ||
                   r.yMax < root.yMin + OverlapEpsilon ||
                   r.yMin > root.yMax - OverlapEpsilon;
        }

        static bool RectsOverlap(Rect a, Rect b)
        {
            return a.xMin < b.xMax - OverlapEpsilon &&
                   a.xMax > b.xMin + OverlapEpsilon &&
                   a.yMin < b.yMax - OverlapEpsilon &&
                   a.yMax > b.yMin + OverlapEpsilon;
        }

        static bool SuspectTruncation(InGameHud hud)
        {
            if (hud.ViewContextLabel != null &&
                !string.IsNullOrEmpty(hud.ViewContextLabel.text) &&
                hud.ViewContextLabel.worldBound.width < 8f)
                return true;
            if (hud.DateLabel != null &&
                !string.IsNullOrEmpty(hud.DateLabel.text) &&
                hud.DateLabel.worldBound.width < 8f)
                return true;
            return false;
        }
    }
}
