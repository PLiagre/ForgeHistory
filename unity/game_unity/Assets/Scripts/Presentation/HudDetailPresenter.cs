using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine.UIElements;

namespace VictoriaGame.Presentation
{
    /// <summary>
    /// Transforme un DetailBlock (sections --- NAME ---) en structure visuelle
    /// pays/province : titre naturel, sous-titre FR, indicateurs prioritaires
    /// décomposés, détail secondaire en ScrollView. Aucune donnée inventée.
    /// </summary>
    public static class HudDetailPresenter
    {
        public const int MaxPriorityRows = 8;
        public const int MinPriorityRows = 4;

        /// <summary>Tokens anglais / debug interdits dans le texte utilisateur des panneaux.</summary>
        public static readonly string[] ForbiddenUserTokens =
        {
            "CONTROL PLAYER", "CONTROL AI", "tax income last tick",
            "RATE", "EXP", "INDUS", "OWNER", "PEASANT", "ARTISAN", "NOBLE",
            "PHY", "LOD", "MIX", "ACT ", "STOCK ", "FARM id", "Tax-", "Tax+",
            "LOCKED", "not your country", "PLAYER —", "PLAYER -",
            "LAWMOD", "STAB", "LEG", "EFF"
        };

        const string ClassTitle = "panel__title";
        const string ClassSubtitle = "panel__subtitle";
        const string ClassSections = "panel__sections";
        const string ClassSection = "panel__section";
        const string ClassSectionTitle = "panel__section-title";
        const string ClassRow = "panel__row";
        const string ClassRowLabel = "panel__row-label";
        const string ClassRowValue = "panel__row-value";
        const string ClassAlerts = "panel__alerts";
        const string ClassAlert = "panel__alert";
        const string ClassPriority = "panel__priority";
        const string ClassScroll = "panel__scroll";

        static readonly string[] PrioritySectionOrder =
        {
            "TREASURY", "TAX", "MILITARY", "POPULATION", "STATUS",
            "PROD STOCKS", "WHY HUNGRY", "BUILDINGS", "SATISFACTION",
            "TRADE FLOWS", "PROVINCES PROD"
        };

        static readonly HashSet<string> MetricKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "GOLD", "DEBT", "RATE", "INC", "EXP", "LAST", "ARMY", "WARS",
            "PRESTIGE", "INDUS", "POP", "PROVINCES", "CAPITAL", "DEV",
            "PHY", "LOD", "MIX", "W", "ACT", "STOCK", "OK", "NEED",
            "STAB", "LEG", "LAWMOD", "EFF"
        };

        public static void Populate(VisualElement panel, string detail, string titleSuffix)
        {
            if (panel == null)
                return;

            var title = panel.Q<Label>(panel.name + "_Title");
            var subtitle = panel.Q<Label>(panel.name + "_Subtitle");
            var sectionsRoot = panel.Q<VisualElement>(panel.name + "_Sections");
            var alertsRoot = panel.Q<VisualElement>(panel.name + "_Alerts");

            if (title == null || subtitle == null || sectionsRoot == null || alertsRoot == null)
                EnsureSkeleton(panel, out title, out subtitle, out sectionsRoot, out alertsRoot);

            sectionsRoot.Clear();
            alertsRoot.Clear();
            title.text = "";
            subtitle.text = "";

            if (string.IsNullOrEmpty(detail))
                return;

            var sections = ParseSections(detail);
            if (sections.Count == 0)
            {
                title.text = string.IsNullOrEmpty(titleSuffix) ? "Détail" : titleSuffix;
                AddRow(sectionsRoot, "Info", HudValueFormatter.RestorePresentationName(detail.Replace('\n', ' ')));
                return;
            }

            ApplyIdentity(sections, title, subtitle, titleSuffix, alertsRoot);

            var priorityRows = new List<(string label, string value)>(MaxPriorityRows);
            var secondary = new List<(string section, string label, string value)>(48);

            CollectRows(sections, alertsRoot, priorityRows, secondary);

            var priorityBlock = new VisualElement { name = panel.name + "_Priority" };
            priorityBlock.AddToClassList(ClassPriority);
            var priorityTitle = new Label("Indicateurs");
            priorityTitle.AddToClassList(ClassSectionTitle);
            priorityBlock.Add(priorityTitle);
            var shown = Math.Min(Math.Max(priorityRows.Count, 0), MaxPriorityRows);
            for (var i = 0; i < shown; i++)
                AddRow(priorityBlock, priorityRows[i].label, priorityRows[i].value);
            sectionsRoot.Add(priorityBlock);

            if (secondary.Count == 0 && priorityRows.Count <= MaxPriorityRows)
                return;

            var scroll = new ScrollView { name = panel.name + "_Scroll" };
            scroll.AddToClassList(ClassScroll);
            scroll.mode = ScrollViewMode.Vertical;

            string currentSec = null;
            VisualElement block = null;
            for (var i = 0; i < secondary.Count; i++)
            {
                var row = secondary[i];
                if (block == null || !string.Equals(currentSec, row.section, StringComparison.Ordinal))
                {
                    currentSec = row.section;
                    block = new VisualElement();
                    block.AddToClassList(ClassSection);
                    var secTitle = new Label(HudValueFormatter.LocalizeSection(currentSec));
                    secTitle.AddToClassList(ClassSectionTitle);
                    block.Add(secTitle);
                    scroll.Add(block);
                }

                AddRow(block, row.label, row.value);
            }

            for (var i = MaxPriorityRows; i < priorityRows.Count; i++)
            {
                if (block == null)
                {
                    block = new VisualElement();
                    block.AddToClassList(ClassSection);
                    var secTitle = new Label("Autres");
                    secTitle.AddToClassList(ClassSectionTitle);
                    block.Add(secTitle);
                    scroll.Add(block);
                }

                AddRow(block, priorityRows[i].label, priorityRows[i].value);
            }

            if (scroll.childCount > 0)
                sectionsRoot.Add(scroll);
        }

        /// <summary>Texte plat visible d'un panneau (titre + lignes) pour assertions contractuelles.</summary>
        public static string CollectVisibleText(VisualElement panel)
        {
            if (panel == null)
                return "";
            var sb = new StringBuilder(1024);
            AppendLabels(panel, sb);
            return sb.ToString();
        }

        /// <summary>Vrai si le texte utilisateur contient un token anglais/debug interdit.</summary>
        public static bool ContainsForbiddenUserToken(string text, out string hit)
        {
            hit = null;
            if (string.IsNullOrEmpty(text))
                return false;
            var upper = text.ToUpperInvariant();
            for (var i = 0; i < ForbiddenUserTokens.Length; i++)
            {
                var tok = ForbiddenUserTokens[i];
                var tokU = tok.ToUpperInvariant();
                // Phrases multi-mots : sous-chaîne ; tokens techniques : mot entier
                // (évite INDUS ⊂ Industrie, EXP ⊂ Dépenses, etc.)
                if (tok.IndexOf(' ') >= 0)
                {
                    if (upper.IndexOf(tokU, StringComparison.Ordinal) >= 0)
                    {
                        hit = tok;
                        return true;
                    }
                }
                else if (ContainsWholeToken(upper, tokU))
                {
                    hit = tok;
                    return true;
                }
            }

            return false;
        }

        static bool ContainsWholeToken(string haystackUpper, string tokenUpper)
        {
            var idx = 0;
            while (idx < haystackUpper.Length)
            {
                var found = haystackUpper.IndexOf(tokenUpper, idx, StringComparison.Ordinal);
                if (found < 0)
                    return false;
                var beforeOk = found == 0 || !char.IsLetterOrDigit(haystackUpper[found - 1]);
                var after = found + tokenUpper.Length;
                var afterOk = after >= haystackUpper.Length || !char.IsLetterOrDigit(haystackUpper[after]);
                if (beforeOk && afterOk)
                    return true;
                idx = found + 1;
            }

            return false;
        }

        static void AppendLabels(VisualElement root, StringBuilder sb)
        {
            if (root is Label label && !string.IsNullOrEmpty(label.text))
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(label.text);
            }

            var count = root.childCount;
            for (var i = 0; i < count; i++)
                AppendLabels(root[i], sb);
        }

        static void ApplyIdentity(
            List<Section> sections,
            Label title,
            Label subtitle,
            string titleSuffix,
            VisualElement alertsRoot)
        {
            var identity = FindSection(sections, "IDENTITY");
            if (identity == null || identity.Lines.Count == 0)
            {
                title.text = string.IsNullOrEmpty(titleSuffix)
                    ? HudValueFormatter.LocalizeSection(sections[0].Name)
                    : titleSuffix;
                return;
            }

            var isProvince = false;
            for (var i = 0; i < identity.Lines.Count; i++)
            {
                var line = identity.Lines[i];
                if (line.StartsWith("PROVINCE", StringComparison.OrdinalIgnoreCase))
                {
                    isProvince = true;
                    title.text = HumanizeProvinceTitle(line);
                }
                else if (line.StartsWith("COUNTRY", StringComparison.OrdinalIgnoreCase))
                {
                    title.text = HumanizeCountryTitle(line);
                }
                else if (line.StartsWith("CONTROL", StringComparison.OrdinalIgnoreCase))
                {
                    subtitle.text = HumanizeControlLine(line);
                }
                else if (line.StartsWith("OWNER", StringComparison.OrdinalIgnoreCase))
                {
                    subtitle.text = HumanizeOwnerLine(line);
                }
                else if (line.StartsWith("CAPITAL", StringComparison.OrdinalIgnoreCase) ||
                         line.StartsWith("PROVINCES", StringComparison.OrdinalIgnoreCase) ||
                         line.StartsWith("DEV", StringComparison.OrdinalIgnoreCase))
                {
                    // Indicateurs secondaires — traités dans CollectRows via IDENTITY skip;
                    // CAPITAL / PROVINCES / DEV restent hors titre.
                }
                else if (IsAlertLine(line))
                {
                    AddAlert(alertsRoot, HudValueFormatter.LocalizeAlert(line));
                }
            }

            if (string.IsNullOrEmpty(title.text))
            {
                title.text = string.IsNullOrEmpty(titleSuffix)
                    ? (isProvince ? "Province" : "Pays")
                    : titleSuffix;
            }
        }

        static void CollectRows(
            List<Section> sections,
            VisualElement alertsRoot,
            List<(string label, string value)> priorityRows,
            List<(string section, string label, string value)> secondary)
        {
            var ordered = new List<Section>(sections.Count);
            for (var p = 0; p < PrioritySectionOrder.Length; p++)
            {
                var found = FindSection(sections, PrioritySectionOrder[p]);
                if (found != null)
                    ordered.Add(found);
            }

            for (var s = 0; s < sections.Count; s++)
            {
                if (string.Equals(sections[s].Name, "IDENTITY", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (ordered.Contains(sections[s]))
                    continue;
                ordered.Add(sections[s]);
            }

            for (var s = 0; s < ordered.Count; s++)
            {
                var sec = ordered[s];
                var secName = sec.Name ?? "";
                var preferPriority =
                    string.Equals(secName, "TREASURY", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(secName, "TAX", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(secName, "MILITARY", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(secName, "POPULATION", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(secName, "STATUS", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(secName, "PROD STOCKS", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(secName, "WHY HUNGRY", StringComparison.OrdinalIgnoreCase);

                if (string.Equals(secName, "BUILDINGS", StringComparison.OrdinalIgnoreCase))
                {
                    EmitBuildings(sec, secondary);
                    continue;
                }

                if (string.Equals(secName, "SATISFACTION", StringComparison.OrdinalIgnoreCase))
                {
                    // PHY/LOD/MIX/W : diagnostic — hors UI joueur (debug seulement).
                    if (InGameHud.ShowDebugIds)
                    {
                        for (var i = 0; i < sec.Lines.Count; i++)
                            EmitExpandedLine(sec.Lines[i], secName, false, alertsRoot, priorityRows, secondary);
                    }

                    continue;
                }

                for (var i = 0; i < sec.Lines.Count; i++)
                {
                    var line = sec.Lines[i];
                    // LAWMOD/EFF : modificateur de loi brut — identifiant technique, hors UI joueur
                    // par défaut (REVUE-v1_054.md P1 « dump technique »). Reste lisible en mode
                    // debug explicite (--debug-ids), même gate que HOVER (MapDisplaySystem.AppendHover).
                    if (!InGameHud.ShowDebugIds &&
                        line.TrimStart().StartsWith("LAWMOD", StringComparison.OrdinalIgnoreCase))
                        continue;
                    EmitExpandedLine(line, secName, preferPriority, alertsRoot, priorityRows, secondary);
                }
            }

            if (priorityRows.Count < MinPriorityRows && secondary.Count > 0)
            {
                while (priorityRows.Count < MinPriorityRows && secondary.Count > 0)
                {
                    priorityRows.Add((secondary[0].label, secondary[0].value));
                    secondary.RemoveAt(0);
                }
            }
        }

        static void EmitBuildings(
            Section sec,
            List<(string section, string label, string value)> secondary)
        {
            // Agrège "FARM id=1 city=2 COMPLETE cap=2000" → "Ferme ×N — achevé, capacité 2000"
            var groups = new Dictionary<string, (string label, string value, int count)>(8);
            var order = new List<string>(8);
            for (var i = 0; i < sec.Lines.Count; i++)
            {
                var line = sec.Lines[i];
                if (IsNoiseLine(line))
                    continue;
                if (!TryParseBuildingLine(line, out var typeKey, out var label, out var value))
                {
                    secondary.Add((sec.Name, HudValueFormatter.LocalizeLabel(line), ""));
                    continue;
                }

                var key = typeKey + "|" + value;
                if (groups.TryGetValue(key, out var g))
                    groups[key] = (g.label, g.value, g.count + 1);
                else
                {
                    groups[key] = (label, value, 1);
                    order.Add(key);
                }
            }

            for (var i = 0; i < order.Count; i++)
            {
                var g = groups[order[i]];
                var displayLabel = g.count > 1 ? g.label + " ×" + g.count.ToString(CultureInfo.InvariantCulture) : g.label;
                secondary.Add((sec.Name, displayLabel, g.value));
            }
        }

        static bool TryParseBuildingLine(string line, out string typeKey, out string label, out string value)
        {
            typeKey = "";
            label = "";
            value = "";
            if (string.IsNullOrEmpty(line))
                return false;

            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return false;

            typeKey = parts[0].ToUpperInvariant();
            label = HudValueFormatter.LocalizeBuildingType(typeKey);

            var complete = false;
            var site = false;
            string cap = null;
            for (var i = 1; i < parts.Length; i++)
            {
                var p = parts[i];
                if (p.StartsWith("id=", StringComparison.OrdinalIgnoreCase))
                {
                    if (InGameHud.ShowDebugIds)
                    {
                        if (value.Length > 0) value += ", ";
                        value += "id " + p.Substring(3);
                    }

                    continue;
                }

                if (p.StartsWith("city=", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (p.Equals("COMPLETE", StringComparison.OrdinalIgnoreCase))
                {
                    complete = true;
                    continue;
                }

                if (p.Equals("SITE", StringComparison.OrdinalIgnoreCase))
                {
                    site = true;
                    continue;
                }

                if (p.StartsWith("cap=", StringComparison.OrdinalIgnoreCase))
                    cap = p.Substring(4);
            }

            var sb = new StringBuilder(48);
            if (complete)
                sb.Append("achevé");
            else if (site)
                sb.Append("en construction");
            if (!string.IsNullOrEmpty(cap))
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append("capacité ").Append(HudValueFormatter.FormatHumanValue(cap));
            }

            value = sb.Length > 0 ? sb.ToString() : "—";
            return true;
        }

        static void EmitExpandedLine(
            string line,
            string sectionName,
            bool preferPriority,
            VisualElement alertsRoot,
            List<(string label, string value)> priorityRows,
            List<(string section, string label, string value)> secondary)
        {
            if (IsNoiseLine(line))
                return;

            if (IsAlertLine(line))
            {
                AddAlert(alertsRoot, HudValueFormatter.LocalizeAlert(line));
                return;
            }

            var rows = ExpandMetricLine(line, sectionName);
            for (var r = 0; r < rows.Count; r++)
            {
                var label = rows[r].label;
                var value = rows[r].value;
                if (string.IsNullOrEmpty(label) && string.IsNullOrEmpty(value))
                    continue;
                if (IsHiddenDiagnosticLabel(label))
                    continue;

                if (preferPriority && priorityRows.Count < MaxPriorityRows)
                    priorityRows.Add((label, value));
                else
                    secondary.Add((sectionName, label, value));
            }
        }

        static bool IsHiddenDiagnosticLabel(string label)
        {
            if (string.IsNullOrEmpty(label))
                return true;
            var u = label.Trim().ToUpperInvariant();
            return u == "PHY" || u == "LOD" || u == "MIX" || u == "W" ||
                   u == "PHYSIQUE" || u == "BLEND" ||
                   u.StartsWith("DEV", StringComparison.Ordinal);
        }

        /// <summary>Décompose « DEBT x RATE y » / « ACT WOOD cap=… » en lignes à un sens.</summary>
        static List<(string label, string value)> ExpandMetricLine(string line, string sectionName)
        {
            var result = new List<(string label, string value)>(4);
            if (string.IsNullOrEmpty(line))
                return result;

            var trimmed = line.Trim();

            // "OK  no input deficit"
            if (trimmed.StartsWith("OK", StringComparison.OrdinalIgnoreCase))
            {
                result.Add(("Approvisionnement", "Aucun déficit d'intrants"));
                return result;
            }

            // "ACT WOOD cap=400 | GRAIN cap=…"
            if (trimmed.StartsWith("ACT", StringComparison.OrdinalIgnoreCase))
            {
                ExpandActLine(trimmed, result);
                return result;
            }

            // "STOCK GRAIN=42597 WINE=16" ou "STOCK (empty)"
            if (trimmed.StartsWith("STOCK", StringComparison.OrdinalIgnoreCase))
            {
                ExpandStockLine(trimmed, result);
                return result;
            }

            // "LAST   7.4  (tax income last tick)"
            if (trimmed.StartsWith("LAST", StringComparison.OrdinalIgnoreCase))
            {
                SplitLabelValue(trimmed, out _, out var rest);
                var num = rest;
                var paren = rest.IndexOf('(');
                if (paren >= 0)
                    num = rest.Substring(0, paren).Trim();
                result.Add(("Revenu fiscal", HudValueFormatter.FormatHumanValue(num)));
                return result;
            }

            // Ligne type "PRESTIGE 50 INDUS 0.0" ou "GOLD 38.2" ou "DEBT 0 RATE 0.002"
            var tokens = trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length >= 2 && MetricKeys.Contains(tokens[0]))
            {
                var i = 0;
                while (i < tokens.Length)
                {
                    var key = tokens[i];
                    if (!MetricKeys.Contains(key))
                        break;

                    i++;
                    if (i >= tokens.Length)
                        break;

                    var sbVal = new StringBuilder();
                    sbVal.Append(tokens[i]);
                    i++;
                    // Inclure % et plages [a..b] dans la même valeur
                    while (i < tokens.Length && !MetricKeys.Contains(tokens[i]))
                    {
                        sbVal.Append(' ').Append(tokens[i]);
                        var tok = tokens[i];
                        i++;
                        if (tok.IndexOf(']') >= 0)
                            break;
                    }

                    var label = HudValueFormatter.LocalizeLabel(key);
                    if (key.Equals("RATE", StringComparison.OrdinalIgnoreCase) &&
                        sectionName.Equals("TREASURY", StringComparison.OrdinalIgnoreCase))
                        label = "Taux d'intérêt";
                    else if (key.Equals("RATE", StringComparison.OrdinalIgnoreCase))
                        label = "Taux";
                    else if (key.Equals("LAST", StringComparison.OrdinalIgnoreCase))
                        label = "Revenu fiscal";
                    else if (key.Equals("INC", StringComparison.OrdinalIgnoreCase))
                        label = "Revenu";

                    var rawVal = StripEnglishGloss(sbVal.ToString());
                    rawVal = HudValueFormatter.FormatHumanValue(rawVal);
                    // Plage déjà en % : normaliser crochets « [0 %..0,02 %] » → « 0 % – 0,02 % »
                    rawVal = NormalizePercentRangeDisplay(rawVal);
                    result.Add((label, rawVal));
                }

                if (result.Count > 0)
                    return result;
            }

            // Population : "PEASANT  2513 FRENCH CATHOLIC"
            if (tokens.Length >= 2 && HudValueFormatter.IsPopTypeToken(tokens[0]))
            {
                var popLabel = HudValueFormatter.LocalizeLabel(tokens[0]);
                var popVal = tokens[1];
                if (tokens.Length > 2)
                {
                    var culture = string.Join(" ", tokens, 2, tokens.Length - 2);
                    popVal = tokens[1] + " · " + HudValueFormatter.LocalizeCultureReligion(culture);
                }

                result.Add((popLabel, popVal));
                return result;
            }

            // Production pays : "1 Ile-de-Franc grain" → nom restauré + bien
            if (sectionName.Equals("PROVINCES PROD", StringComparison.OrdinalIgnoreCase))
            {
                result.Add(FormatProvinceProdLine(trimmed));
                return result;
            }

            SplitLabelValue(trimmed, out var lab, out var val);
            lab = HudValueFormatter.LocalizeLabel(lab);
            val = HudValueFormatter.FormatHumanValue(StripEnglishGloss(val));
            if (string.IsNullOrEmpty(val) && LooksLikeRawDump(lab))
                return result;
            result.Add((lab, val));
            return result;
        }

        static void ExpandActLine(string trimmed, List<(string label, string value)> result)
        {
            var body = trimmed.Substring(3).Trim();
            if (string.IsNullOrEmpty(body) || body.Equals("(none)", StringComparison.OrdinalIgnoreCase))
            {
                result.Add(("Production", "Aucune"));
                return;
            }

            var chunks = body.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
            var parts = new List<string>(chunks.Length);
            for (var i = 0; i < chunks.Length; i++)
            {
                var c = chunks[i].Trim();
                if (c.Length == 0) continue;
                var capIdx = c.IndexOf("cap=", StringComparison.OrdinalIgnoreCase);
                if (capIdx >= 0)
                {
                    var good = c.Substring(0, capIdx).Trim();
                    var cap = c.Substring(capIdx + 4).Trim();
                    parts.Add(
                        HudValueFormatter.LocalizeGoodTag(good) + " (" +
                        HudValueFormatter.FormatHumanValue(cap) + ")");
                }
                else
                    parts.Add(HudValueFormatter.LocalizeGoodTag(c));
            }

            result.Add(("Activité", string.Join(" · ", parts)));
        }

        static void ExpandStockLine(string trimmed, List<(string label, string value)> result)
        {
            var body = trimmed.Substring(5).Trim();
            if (string.IsNullOrEmpty(body) ||
                body.Equals("(empty)", StringComparison.OrdinalIgnoreCase) ||
                body.Equals("(none)", StringComparison.OrdinalIgnoreCase))
            {
                result.Add(("Stocks", "Vides"));
                return;
            }

            var tokens = body.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            var parts = new List<string>(tokens.Length);
            for (var i = 0; i < tokens.Length; i++)
            {
                var t = tokens[i];
                var eq = t.IndexOf('=');
                if (eq > 0)
                {
                    var good = t.Substring(0, eq);
                    var qty = t.Substring(eq + 1);
                    parts.Add(
                        HudValueFormatter.LocalizeGoodTag(good) + " " +
                        HudValueFormatter.FormatHumanValue(qty));
                }
                else
                    parts.Add(HudValueFormatter.LocalizeGoodTag(t));
            }

            result.Add(("Stocks", string.Join(" · ", parts)));
        }

        static (string label, string value) FormatProvinceProdLine(string line)
        {
            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3 && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid))
            {
                var fromSource = ProvinceCoordinates.NameOf(pid);
                var name = !string.IsNullOrEmpty(fromSource)
                    ? fromSource
                    : HudValueFormatter.RestorePresentationName(parts[1]);
                var good = HudValueFormatter.LocalizeGoodTag(parts[parts.Length - 1]);
                // Si parts[1] était tronqué, NameOf couvre ; sinon joindre le milieu
                if (string.IsNullOrEmpty(fromSource) && parts.Length > 3)
                {
                    var mid = string.Join(" ", parts, 1, parts.Length - 2);
                    name = HudValueFormatter.RestorePresentationName(mid);
                }

                return (name, good);
            }

            return ("Production", HudValueFormatter.RestorePresentationName(line));
        }

        static string StripEnglishGloss(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";
            var v = value;
            var markers = new[]
            {
                "(tax income last tick)",
                "tax income last tick",
                "(tax locked)",
                "tax locked"
            };
            for (var i = 0; i < markers.Length; i++)
            {
                var idx = v.IndexOf(markers[i], StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                    v = (v.Substring(0, idx) + v.Substring(idx + markers[i].Length)).Trim();
            }

            return v.Trim();
        }

        static string NormalizePercentRangeDisplay(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";
            var v = value;
            var open = v.IndexOf('[');
            var close = v.IndexOf(']');
            if (open >= 0 && close > open)
            {
                var inner = v.Substring(open + 1, close - open - 1);
                var dots = inner.IndexOf("..", StringComparison.Ordinal);
                if (dots >= 0)
                {
                    var left = inner.Substring(0, dots).Trim();
                    var right = inner.Substring(dots + 2).Trim();
                    var head = v.Substring(0, open).Trim();
                    var tail = close + 1 < v.Length ? v.Substring(close + 1).Trim() : "";
                    var range = left + " – " + right;
                    v = string.IsNullOrEmpty(head)
                        ? range
                        : head + " · plage " + range;
                    if (!string.IsNullOrEmpty(tail))
                        v = v + " " + tail;
                }
            }

            return v.Trim();
        }

        static string HumanizeCountryTitle(string line)
        {
            // "COUNTRY 0 FRA  France" → "France" (tag discret si utile)
            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3)
                return HudValueFormatter.RestorePresentationName(line);

            var tag = parts[2];
            var nameSb = new StringBuilder();
            for (var i = 3; i < parts.Length; i++)
            {
                if (nameSb.Length > 0) nameSb.Append(' ');
                nameSb.Append(parts[i]);
            }

            var name = HudValueFormatter.RestorePresentationName(nameSb.ToString());
            if (string.IsNullOrEmpty(name))
                return tag;
            // Tag ISO discret seulement s'il apporte une valeur (différent du nom)
            if (!string.IsNullOrEmpty(tag) &&
                !name.StartsWith(tag, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(name, tag, StringComparison.OrdinalIgnoreCase))
            {
                // Nom seul en titre — le tag n'est plus préfixé ("FRA France")
                return name;
            }

            return name;
        }

        static string HumanizeProvinceTitle(string line)
        {
            // "PROVINCE 1 LE-DE-FRANCE" → source JSON "Île-de-France"
            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 &&
                int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid))
            {
                var fromSource = ProvinceCoordinates.NameOf(pid);
                if (!string.IsNullOrEmpty(fromSource))
                    return fromSource;
            }

            if (parts.Length >= 3)
            {
                var sb = new StringBuilder();
                for (var i = 2; i < parts.Length; i++)
                {
                    if (sb.Length > 0) sb.Append(' ');
                    sb.Append(parts[i]);
                }

                return HudValueFormatter.RestorePresentationName(sb.ToString());
            }

            return HudValueFormatter.RestorePresentationName(line);
        }

        static string HumanizeControlLine(string line)
        {
            var u = line.ToUpperInvariant();
            if (u.Contains("PLAYER"))
                return "Pays contrôlé";
            if (u.Contains("AI") || u.Contains("LOCKED"))
                return "Pays géré par l'IA";
            return "Contrôle";
        }

        static string HumanizeOwnerLine(string line)
        {
            // "OWNER  FRA  France" → "Propriétaire : France"
            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3)
            {
                var nameSb = new StringBuilder();
                for (var i = 2; i < parts.Length; i++)
                {
                    // Sauter le tag ISO s'il précède le nom
                    if (i == 2 && parts[i].Length <= 4 && IsAllLetters(parts[i]) && parts.Length > 3)
                        continue;
                    if (nameSb.Length > 0) nameSb.Append(' ');
                    nameSb.Append(parts[i]);
                }

                var name = HudValueFormatter.RestorePresentationName(nameSb.ToString());
                if (string.IsNullOrEmpty(name) && parts.Length >= 2)
                    name = parts[1];
                return "Propriétaire : " + name;
            }

            if (parts.Length == 2)
                return "Propriétaire : " + parts[1];
            return "Propriétaire";
        }

        static bool IsAllLetters(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            for (var i = 0; i < s.Length; i++)
            {
                if (!char.IsLetter(s[i]))
                    return false;
            }

            return true;
        }

        static bool IsNoiseLine(string line)
        {
            if (string.IsNullOrEmpty(line))
                return true;
            var t = line.Trim();
            if (t.StartsWith("---", StringComparison.Ordinal))
                return true;
            if (t.Equals("(none)", StringComparison.OrdinalIgnoreCase))
                return true;
            if (t.StartsWith("PLAYER —", StringComparison.OrdinalIgnoreCase) ||
                t.StartsWith("PLAYER -", StringComparison.OrdinalIgnoreCase))
                return true;
            if (t.StartsWith("CONTROL", StringComparison.OrdinalIgnoreCase))
                return true;
            return false;
        }

        static bool LooksLikeRawDump(string label)
        {
            if (string.IsNullOrEmpty(label))
                return true;
            return label.IndexOf('=') >= 0 && label.Length > 40;
        }

        static void EnsureSkeleton(
            VisualElement panel,
            out Label title,
            out Label subtitle,
            out VisualElement sectionsRoot,
            out VisualElement alertsRoot)
        {
            title = panel.Q<Label>(panel.name + "_Title");
            if (title == null)
            {
                title = new Label { name = panel.name + "_Title" };
                title.AddToClassList(ClassTitle);
                panel.Add(title);
            }

            subtitle = panel.Q<Label>(panel.name + "_Subtitle");
            if (subtitle == null)
            {
                subtitle = new Label { name = panel.name + "_Subtitle" };
                subtitle.AddToClassList(ClassSubtitle);
                panel.Add(subtitle);
            }

            sectionsRoot = panel.Q<VisualElement>(panel.name + "_Sections");
            if (sectionsRoot == null)
            {
                sectionsRoot = new VisualElement { name = panel.name + "_Sections" };
                sectionsRoot.AddToClassList(ClassSections);
                panel.Add(sectionsRoot);
            }

            alertsRoot = panel.Q<VisualElement>(panel.name + "_Alerts");
            if (alertsRoot == null)
            {
                alertsRoot = new VisualElement { name = panel.name + "_Alerts" };
                alertsRoot.AddToClassList(ClassAlerts);
                panel.Add(alertsRoot);
            }
        }

        static void AddRow(VisualElement parent, string label, string value)
        {
            var row = new VisualElement();
            row.AddToClassList(ClassRow);
            var l = new Label(label ?? "");
            l.AddToClassList(ClassRowLabel);
            row.Add(l);
            if (!string.IsNullOrEmpty(value))
            {
                var v = new Label(value);
                v.AddToClassList(ClassRowValue);
                row.Add(v);
            }

            parent.Add(row);
        }

        static void AddAlert(VisualElement alertsRoot, string line)
        {
            if (alertsRoot == null || string.IsNullOrEmpty(line))
                return;
            var alert = new Label(line);
            alert.AddToClassList(ClassAlert);
            alertsRoot.Add(alert);
        }

        static bool IsAlertLine(string line)
        {
            if (string.IsNullOrEmpty(line))
                return false;
            var u = line.ToUpperInvariant();
            return u.StartsWith("LOCKED", StringComparison.Ordinal) ||
                   u.StartsWith("REFUS", StringComparison.Ordinal) ||
                   u.Contains("NOT YOUR COUNTRY", StringComparison.Ordinal) ||
                   u.StartsWith("WARN", StringComparison.Ordinal) ||
                   u.StartsWith("DEFICIT", StringComparison.Ordinal) ||
                   u.StartsWith("NEED", StringComparison.Ordinal) ||
                   u.Contains("HUNGRY", StringComparison.Ordinal);
        }

        static void SplitLabelValue(string line, out string label, out string value)
        {
            label = line ?? "";
            value = "";
            if (string.IsNullOrEmpty(line))
                return;

            var eq = line.IndexOf('=');
            if (eq > 0 && eq < line.Length - 1 && eq < 24)
            {
                label = line.Substring(0, eq).Trim();
                value = line.Substring(eq + 1).Trim();
                return;
            }

            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                label = parts[0];
                value = string.Join(" ", parts, 1, parts.Length - 1);
            }
        }

        static Section FindSection(List<Section> sections, string name)
        {
            for (var i = 0; i < sections.Count; i++)
            {
                if (string.Equals(sections[i].Name, name, StringComparison.OrdinalIgnoreCase))
                    return sections[i];
            }

            return null;
        }

        static List<Section> ParseSections(string detail)
        {
            var list = new List<Section>(8);
            Section current = null;
            var lines = detail.Replace("\r\n", "\n").Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].TrimEnd();
                if (line.Length == 0)
                    continue;

                if (line.StartsWith("---", StringComparison.Ordinal) &&
                    line.EndsWith("---", StringComparison.Ordinal) &&
                    line.Length > 6)
                {
                    var name = line.Trim('-', ' ', '\t');
                    current = new Section { Name = name, Lines = new List<string>(8) };
                    list.Add(current);
                    continue;
                }

                if (current == null)
                {
                    current = new Section { Name = "DETAIL", Lines = new List<string>(8) };
                    list.Add(current);
                }

                current.Lines.Add(line.Trim());
            }

            return list;
        }

        sealed class Section
        {
            public string Name;
            public List<string> Lines;
        }

        /// <summary>Découpe la ligne métriques en zones bandeau (sans inventer).</summary>
        public static void SplitMetricsLine(
            string metricsLine,
            out string viewContext,
            out string metricsCore,
            out string dateLabel)
        {
            viewContext = "";
            metricsCore = metricsLine ?? "";
            dateLabel = "";

            if (string.IsNullOrEmpty(metricsLine))
                return;

            var tokens = metricsLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var sbMetrics = new StringBuilder(metricsLine.Length);
            var sbZoom = new StringBuilder(32);
            var capturingZoom = false;

            for (var i = 0; i < tokens.Length; i++)
            {
                var t = tokens[i];
                if (string.Equals(t, "AN", StringComparison.OrdinalIgnoreCase) &&
                    i + 1 < tokens.Length &&
                    int.TryParse(tokens[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                {
                    dateLabel = "An " + tokens[i + 1];
                    i++;
                    continue;
                }

                if (string.Equals(t, "TICK", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 < tokens.Length)
                    {
                        if (sbMetrics.Length > 0) sbMetrics.Append("  ");
                        sbMetrics.Append("Tick ").Append(tokens[i + 1]);
                        i++;
                    }

                    continue;
                }

                if (string.Equals(t, "ZOOM", StringComparison.OrdinalIgnoreCase))
                {
                    capturingZoom = true;
                    continue;
                }

                if (capturingZoom)
                {
                    if (string.Equals(t, "HOVER", StringComparison.OrdinalIgnoreCase) ||
                        t.StartsWith("[", StringComparison.Ordinal))
                    {
                        capturingZoom = false;
                        i--;
                        continue;
                    }

                    if (sbZoom.Length > 0)
                        sbZoom.Append(' ');
                    sbZoom.Append(t);
                    continue;
                }

                if (sbMetrics.Length > 0)
                    sbMetrics.Append("  ");
                sbMetrics.Append(t);
            }

            viewContext = sbZoom.Length > 0 ? sbZoom.ToString() : "";
            metricsCore = sbMetrics.ToString();
        }
    }
}
