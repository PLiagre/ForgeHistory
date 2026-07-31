using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace VictoriaGame.Presentation
{
    /// <summary>
    /// Palette de présentation : lit country_colors.json à chaque appel.
    /// Tag absent → couleur de repli déterministe dérivée du tag (jamais d'aléa).
    /// </summary>
    public static class CountryColors
    {
        [Serializable]
        class ColorEntry
        {
            public string tag;
            public string name;
            public string color;
        }

        [Serializable]
        class ColorsFile
        {
            public string comment;
            public string unowned;
            public string sea;
            public List<ColorEntry> colors;
        }

        public sealed class Table
        {
            public Color32 Unowned;
            public Color32 Sea;
            readonly Dictionary<string, Color32> _byTag;
            readonly Dictionary<string, string> _nameByTag;

            public Table(
                Color32 unowned,
                Color32 sea,
                Dictionary<string, Color32> byTag,
                Dictionary<string, string> nameByTag = null)
            {
                Unowned = unowned;
                Sea = sea;
                _byTag = byTag;
                _nameByTag = nameByTag ?? new Dictionary<string, string>();
            }

            public Color32 ForTag(string tag)
            {
                if (string.IsNullOrEmpty(tag))
                    return Unowned;
                if (_byTag.TryGetValue(tag, out var c))
                    return c;
                return HashColorFromTag(tag);
            }

            /// <summary>Nom d'affichage (country_colors.json). Tag inconnu → tag lui-même.</summary>
            public string NameForTag(string tag)
            {
                if (string.IsNullOrEmpty(tag))
                    return "";
                if (_nameByTag.TryGetValue(tag, out var n) && !string.IsNullOrEmpty(n))
                    return n;
                return tag;
            }

            public bool TryGetKnown(string tag, out Color32 color) =>
                _byTag.TryGetValue(tag, out color);

            public IReadOnlyDictionary<string, Color32> Known => _byTag;
        }

        public static Table Load()
        {
            var path = Path.Combine(Application.streamingAssetsPath, "data", "country_colors.json");
            if (!File.Exists(path))
            {
                Debug.LogWarning($"CountryColors: fichier introuvable: {path}");
                return new Table(
                    HashColorFromTag("unowned"),
                    HashColorFromTag("sea"),
                    new Dictionary<string, Color32>());
            }

            var json = File.ReadAllText(path);
            var data = JsonUtility.FromJson<ColorsFile>(json);
            var byTag = new Dictionary<string, Color32>();
            var nameByTag = new Dictionary<string, string>();
            if (data?.colors != null)
            {
                for (var i = 0; i < data.colors.Count; i++)
                {
                    var e = data.colors[i];
                    if (string.IsNullOrEmpty(e.tag) || string.IsNullOrEmpty(e.color))
                        continue;
                    byTag[e.tag] = ParseHex(e.color);
                    if (!string.IsNullOrEmpty(e.name))
                        nameByTag[e.tag] = e.name;
                }
            }

            var unowned = string.IsNullOrEmpty(data?.unowned)
                ? HashColorFromTag("unowned")
                : ParseHex(data.unowned);
            var sea = string.IsNullOrEmpty(data?.sea)
                ? HashColorFromTag("sea")
                : ParseHex(data.sea);
            return new Table(unowned, sea, byTag, nameByTag);
        }

        public static Color32 ParseHex(string hex)
        {
            if (string.IsNullOrEmpty(hex))
                return HashColorFromTag("invalid");

            var s = hex[0] == '#' ? hex.Substring(1) : hex;
            if (s.Length < 6)
                return HashColorFromTag(hex);

            byte r = Convert.ToByte(s.Substring(0, 2), 16);
            byte g = Convert.ToByte(s.Substring(2, 2), 16);
            byte b = Convert.ToByte(s.Substring(4, 2), 16);
            return new Color32(r, g, b, 255);
        }

        public static string ToHex(Color32 c) =>
            $"#{c.r:x2}{c.g:x2}{c.b:x2}";

        /// <summary>FNV-1a 32-bit → RGB saturé. Identique d'une exécution à l'autre pour un même tag.</summary>
        public static Color32 HashColorFromTag(string tag)
        {
            unchecked
            {
                uint h = 2166136261u;
                for (var i = 0; i < tag.Length; i++)
                    h = (h ^ tag[i]) * 16777619u;

                var r = (byte)(64 + (h & 0x7Fu));
                var g = (byte)(64 + ((h >> 8) & 0x7Fu));
                var b = (byte)(64 + ((h >> 16) & 0x7Fu));
                return new Color32(r, g, b, 255);
            }
        }
    }
}
