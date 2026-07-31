using System;
using System.Globalization;
using System.Text;

namespace VictoriaGame.Presentation
{
    /// <summary>
    /// Formatage joueur : pas de notation scientifique, décimale localisée FR,
    /// libellés techniques → français, restauration bornée des accents de présentation.
    /// </summary>
    public static class HudValueFormatter
    {
        /// <summary>Taux de production → pourcentage lisible (ex. 0.00002 → "0,002 %").</summary>
        public static string FormatTaxPercent(float rate)
        {
            var pct = rate * 100f;
            return FormatNumber(pct, "0.###") + " %";
        }

        public static string FormatMoney(float value) => FormatNumber(value, "0.#");

        public static string FormatQuantity(float value) => FormatNumber(value, "0.#");

        public static string FormatNumber(float value, string format)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return "—";
            var raw = value.ToString(format, CultureInfo.InvariantCulture);
            if (LooksScientific(raw))
                raw = value.ToString("0.######", CultureInfo.InvariantCulture);
            return raw.Replace('.', ',');
        }

        public static string FormatHumanValue(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return "";
            var t = raw.Trim();
            if (t.Equals("(none)", StringComparison.OrdinalIgnoreCase) ||
                t.Equals("none", StringComparison.OrdinalIgnoreCase))
                return "Aucune";
            if (t.Equals("(empty)", StringComparison.OrdinalIgnoreCase))
                return "Vides";
            if (TryParseScientificOrPlain(t, out var f))
                return FormatNumber(f, Math.Abs(f) < 0.01f && f != 0f ? "0.######" : "0.##");

            var parts = t.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var sb = new StringBuilder(t.Length + 8);
            for (var i = 0; i < parts.Length; i++)
            {
                if (i > 0) sb.Append(' ');
                var p = parts[i];
                if (p.StartsWith("[", StringComparison.Ordinal) && p.EndsWith("]", StringComparison.Ordinal))
                {
                    sb.Append(FormatBracketRange(p));
                    continue;
                }

                if (TryParseScientificOrPlain(p, out var n))
                    sb.Append(FormatNumber(n, Math.Abs(n) < 0.01f && n != 0f ? "0.######" : "0.##"));
                else
                    sb.Append(RestorePresentationName(p));
            }

            return sb.ToString();
        }

        static string FormatBracketRange(string bracket)
        {
            var inner = bracket.Substring(1, bracket.Length - 2);
            var dots = inner.IndexOf("..", StringComparison.Ordinal);
            if (dots < 0)
                return bracket;
            var a = inner.Substring(0, dots);
            var b = inner.Substring(dots + 2);
            var left = TryParseScientificOrPlain(a, out var fa) ? FormatTaxPercent(fa) : a;
            var right = TryParseScientificOrPlain(b, out var fb) ? FormatTaxPercent(fb) : b;
            return left + " – " + right;
        }

        public static bool LooksScientific(string s)
        {
            if (string.IsNullOrEmpty(s))
                return false;
            for (var i = 0; i < s.Length; i++)
            {
                var c = s[i];
                if (c != 'E' && c != 'e')
                    continue;
                if (i == 0 || i + 1 >= s.Length)
                    continue;
                if (!IsAsciiDigit(s[i - 1]) && s[i - 1] != '.')
                    continue;
                var j = i + 1;
                if (s[j] == '+' || s[j] == '-')
                    j++;
                if (j < s.Length && IsAsciiDigit(s[j]))
                    return true;
            }

            return false;
        }

        static bool IsAsciiDigit(char c) => c >= '0' && c <= '9';

        public static bool ContainsScientificNotation(string s) => LooksScientific(s);

        static bool TryParseScientificOrPlain(string token, out float value)
        {
            value = 0f;
            if (string.IsNullOrEmpty(token))
                return false;
            return float.TryParse(
                token,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value);
        }

        /// <summary>Libellés techniques → français joueur.</summary>
        public static string LocalizeLabel(string label)
        {
            if (string.IsNullOrEmpty(label))
                return "";
            switch (label.Trim().ToUpperInvariant())
            {
                case "GOLD": return "Trésor";
                case "DEBT": return "Dette";
                case "INC": return "Revenu";
                case "EXP": return "Dépenses";
                case "RATE": return "Taux";
                case "LAST": return "Revenu fiscal";
                case "ARMY": return "Armée";
                case "WARS": return "Guerres";
                case "POP": return "Population";
                case "PROVINCES": return "Provinces";
                case "CAPITAL": return "Capitale";
                case "PRESTIGE": return "Prestige";
                case "INDUS": return "Industrie";
                case "OWNER": return "Propriétaire";
                case "DEV": return "Développement";
                case "COUNTRY": return "Pays";
                case "PROVINCE": return "Province";
                case "CONTROL": return "Contrôle";
                case "ACT": return "Activité";
                case "STOCK": return "Stocks";
                case "OK": return "Approvisionnement";
                case "NEED": return "Besoin";
                case "IN": return "Entrées";
                case "OUT": return "Sorties";
                case "PEASANT": return "Paysans";
                case "ARTISAN": return "Artisans";
                case "NOBLE": return "Nobles";
                case "MERCHANT": return "Marchands";
                case "CLERGY": return "Clergé";
                case "WORKER": return "Ouvriers";
                case "CAPITALIST": return "Capitalistes";
                case "INTELLECTUAL": return "Intellectuels";
                case "PHY": return "Physique";
                case "LOD": return "Niveau de vie";
                case "MIX": return "Mixte";
                case "W": return "Pondération";
                default: return RestorePresentationName(label);
            }
        }

        public static bool IsPopTypeToken(string token)
        {
            if (string.IsNullOrEmpty(token))
                return false;
            switch (token.Trim().ToUpperInvariant())
            {
                case "PEASANT":
                case "ARTISAN":
                case "NOBLE":
                case "MERCHANT":
                case "CLERGY":
                case "WORKER":
                case "CAPITALIST":
                case "INTELLECTUAL":
                    return true;
                default:
                    return false;
            }
        }

        public static string LocalizeBuildingType(string typeKey)
        {
            if (string.IsNullOrEmpty(typeKey))
                return "Bâtiment";
            switch (typeKey.Trim().ToUpperInvariant())
            {
                case "FARM": return "Ferme";
                case "MINE": return "Mine";
                case "WORKSHOP": return "Atelier";
                case "PORT": return "Port";
                case "FORT": return "Fort";
                case "BARRACKS": return "Caserne";
                default: return typeKey.Substring(0, 1) + typeKey.Substring(1).ToLowerInvariant();
            }
        }

        public static string LocalizeGoodTag(string tag)
        {
            if (string.IsNullOrEmpty(tag))
                return "";
            switch (tag.Trim().ToUpperInvariant())
            {
                case "GRAIN": return "grain";
                case "FISH": return "poisson";
                case "WINE": return "vin";
                case "WOOD": return "bois";
                case "IRON": return "fer";
                case "CLOTH": return "tissu";
                case "WOOL": return "laine";
                case "LIVESTOCK": return "bétail";
                default: return tag.ToLowerInvariant();
            }
        }

        public static string LocalizeCultureReligion(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return "";
            var parts = raw.Split(new[] { ' ', '\t', '_' }, StringSplitOptions.RemoveEmptyEntries);
            var sb = new StringBuilder(raw.Length);
            for (var i = 0; i < parts.Length; i++)
            {
                if (i > 0) sb.Append(' ');
                switch (parts[i].ToUpperInvariant())
                {
                    case "FRENCH": sb.Append("français"); break;
                    case "CATHOLIC": sb.Append("catholique"); break;
                    case "ENGLISH": sb.Append("anglais"); break;
                    case "GERMAN": sb.Append("allemand"); break;
                    case "ITALIAN": sb.Append("italien"); break;
                    case "ORTHODOX": sb.Append("orthodoxe"); break;
                    case "PROTESTANT": sb.Append("protestant"); break;
                    default: sb.Append(parts[i].ToLowerInvariant()); break;
                }
            }

            return sb.ToString();
        }

        public static string LocalizeAlert(string line)
        {
            if (string.IsNullOrEmpty(line))
                return "";
            var u = line.ToUpperInvariant();
            if (u.Contains("NOT YOUR COUNTRY") || u.StartsWith("LOCKED", StringComparison.Ordinal))
                return "Verrouillé — ce n'est pas votre pays";
            if (u.StartsWith("OK", StringComparison.Ordinal))
                return "Approvisionnement : aucun déficit d'intrants";
            if (u.StartsWith("NEED", StringComparison.Ordinal))
            {
                // NEED GRAIN=1.2  INPUT_SHORT ...
                var rest = line.Length > 4 ? line.Substring(4).Trim() : "";
                return "Besoin : " + FormatHumanValue(rest.Replace('_', ' '));
            }

            return RestorePresentationName(line);
        }

        public static string LocalizeSection(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "";
            switch (name.Trim().ToUpperInvariant())
            {
                case "IDENTITY": return "Identité";
                case "TREASURY": return "Trésor";
                case "TAX": return "Impôt";
                case "MILITARY": return "Armée";
                case "STATUS": return "État";
                case "PROVINCES PROD": return "Production";
                case "POPULATION": return "Population";
                case "PROD STOCKS": return "Stocks";
                case "TRADE FLOWS": return "Commerce";
                case "SATISFACTION": return "Satisfaction";
                case "WHY HUNGRY": return "Ravitaillement";
                case "BUILDINGS": return "Bâtiments";
                default: return name.Replace('_', ' ').Trim();
            }
        }

        /// <summary>
        /// Répare les corruptions d'accent dues au Sanitize ASCII (ex. LE-DE-FRANCE).
        /// Borné aux toponymes / libellés connus — n'invente pas de données sim.
        /// </summary>
        public static string RestorePresentationName(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return "";

            var key = raw.Trim().Replace('_', '-').ToUpperInvariant();
            switch (key)
            {
                case "LE-DE-FRANCE":
                case "ILE-DE-FRANCE":
                case "ILEDEFRANCE":
                    return "Île-de-France";
                case "BOURGOGNE":
                    return "Bourgogne";
                case "PROVENCE":
                    return "Provence";
                case "BRETAGNE":
                    return "Bretagne";
                case "NORMANDIE":
                    return "Normandie";
                case "GASCOGNE":
                    return "Gascogne";
                case "LANGUEDOC":
                    return "Languedoc";
                case "CHAMPAGNE":
                    return "Champagne";
                default:
                    break;
            }

            // Truncations fréquentes (PadRight 12) : "Ile-de-Franc" → Île-de-France
            if (key.StartsWith("ILE-DE-FRANC", StringComparison.Ordinal) ||
                key.StartsWith("LE-DE-FRANC", StringComparison.Ordinal))
                return "Île-de-France";

            return raw.Trim().Replace('_', ' ');
        }
    }
}
