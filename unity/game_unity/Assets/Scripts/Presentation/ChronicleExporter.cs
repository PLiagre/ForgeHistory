using Unity.Entities;
using Unity.Collections;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using VictoriaGame.Core;
using VictoriaGame.Economy;
using VictoriaGame.Military;
using VictoriaGame.Utils;
using VictoriaGame.World;

namespace VictoriaGame.Presentation
{
    /// <summary>
    /// Chronique visuelle + journal d'événements dérivé de l'état (lecture seule).
    /// Aucun ISystem — appelé depuis un harnais. Masque partagé via MapSnapshotExporter.MapGeometry.
    /// </summary>
    public static class ChronicleExporter
    {
        public const int ChronicleInterval = 25;
        public const int ChronicleWidth = 480;
        public const int ChronicleHeight = 360;
        public const int ContactCols = 7;
        public const int ContactRows = 6;
        public const int FinalTick = 1000;

        enum EventKind : int
        {
            Annex = 0,
            Bankrupt = 1,
            Death = 2,
            Resurrection = 3,
            WarStart = 4,
            WarEnd = 5
        }

        struct ChronicleEvent : IComparable<ChronicleEvent>
        {
            public int Tick;
            public EventKind Kind;
            public string Key;
            public string Line;

            public int CompareTo(ChronicleEvent other)
            {
                var c = Tick.CompareTo(other.Tick);
                if (c != 0) return c;
                c = ((int)Kind).CompareTo((int)other.Kind);
                if (c != 0) return c;
                return string.CompareOrdinal(Key, other.Key);
            }
        }

        struct ProvinceSnap
        {
            public int Id;
            public string Name;
            public string OwnerTag;
            public int OwnerChangedTick;
        }

        struct CountrySnap
        {
            public string Tag;
            public int ProvinceCount;
            public int BankruptcyTick;
            public float Debt;
        }

        /// <summary>
        /// Exécute la chronique complète : une seule avance continue par pas de 25 ticks.
        /// onTick(tick) = lecture seule (métriques) après chaque observation.
        /// </summary>
        public static string Run(
            EntityManager em,
            Action<int> advanceBy,
            Action<int> onTick,
            string chronicleDir,
            string mapsDir,
            string journalPath,
            out string journalHashHex,
            out bool journalReproOk,
            out bool landMassesOk)
        {
            Directory.CreateDirectory(chronicleDir);
            Directory.CreateDirectory(mapsDir);

            // Géométrie UNE SEULE FOIS par résolution.
            // Le critère {39,4,2,2,1,1,1} est calibré en pleine résolution (Bosphore) :
            // on l'exige sur fullGeo ; la chronique 480×360 réutilise le même algo sans ce garde-fou.
            var chronicleGeo = MapSnapshotExporter.BuildMapGeometry(ChronicleWidth, ChronicleHeight);
            var fullGeo = MapSnapshotExporter.BuildMapGeometry(
                MapSnapshotExporter.Width, MapSnapshotExporter.Height);
            landMassesOk = fullGeo != null && fullGeo.LandMasses.MatchesTarget;
            if (chronicleGeo != null)
            {
                Debug.Log(
                    $"ChronicleExporter: chronicle geo {ChronicleWidth}x{ChronicleHeight} " +
                    chronicleGeo.LandMasses.Summary);
            }

            if (fullGeo != null)
            {
                Debug.Log(
                    $"ChronicleExporter: full geo {MapSnapshotExporter.Width}x{MapSnapshotExporter.Height} " +
                    fullGeo.LandMasses.Summary);
            }

            var provinceNames = LoadProvinceNames();
            var events = new List<ChronicleEvent>(256);
            var landCurve = new List<(int Tick, int Countries)>(41);
            var annexCountByTag = new Dictionary<string, int>(StringComparer.Ordinal);
            var deaths = new List<(string Tag, int Tick, int LastProvinceId)>();

            ProvinceSnap[] prevProvinces = null;
            Dictionary<string, CountrySnap> prevCountries = null;

            var framePaths = new string[FinalTick / ChronicleInterval + 1];
            var frameIdx = 0;

            for (var tick = 0; tick <= FinalTick; tick += ChronicleInterval)
            {
                if (tick > 0)
                    advanceBy(ChronicleInterval);

                var provinces = CaptureProvinces(em, provinceNames);
                var countries = CaptureCountries(em, provinces);

                var countriesWithLand = 0;
                foreach (var kv in countries)
                {
                    if (kv.Value.ProvinceCount > 0)
                        countriesWithLand++;
                }

                landCurve.Add((tick, countriesWithLand));

                if (prevProvinces != null)
                    DiffSnapshots(
                        prevProvinces, provinces, prevCountries, countries,
                        events, annexCountByTag, deaths);

                prevProvinces = provinces;
                prevCountries = countries;

                var cartouche = string.Format(CultureInfo.InvariantCulture, "t{0:D4}", tick);
                var framePath = Path.Combine(
                    chronicleDir,
                    string.Format(CultureInfo.InvariantCulture, "frame_t{0:D4}.png", tick));
                MapSnapshotExporter.ExportWithGeometry(
                    em, tick, framePath, chronicleGeo, drawLabels: false, tickCartouche: cartouche);
                framePaths[frameIdx++] = framePath;

                if (tick == 0 || tick == 200 || tick == 500 || tick == 1000)
                {
                    var mapPath = Path.Combine(mapsDir, $"map_t{tick}.png");
                    MapSnapshotExporter.ExportWithGeometry(
                        em, tick, mapPath, fullGeo, drawLabels: true, tickCartouche: null);
                    landMassesOk &= MapSnapshotExporter.LastLandMassReport.MatchesTarget;
                }

                onTick?.Invoke(tick);
            }

            MapSnapshotExporter.ExportAdjacencyGraph(Path.Combine(mapsDir, "graph_adjacency.png"));

            CollectWars(em, events);

            events.Sort();
            var journal = FormatJournal(events, annexCountByTag, deaths, landCurve);

            // Preuve de reproductibilité : double FormatJournal + hash SHA-256.
            var journal2 = FormatJournal(events, annexCountByTag, deaths, landCurve);
            journalHashHex = Sha256Hex(journal);
            var hash2 = Sha256Hex(journal2);
            journalReproOk = string.Equals(journalHashHex, hash2, StringComparison.Ordinal)
                && string.Equals(journal, journal2, StringComparison.Ordinal);

            File.WriteAllText(journalPath, journal, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            AssembleContactSheet(framePaths, Path.Combine(chronicleDir, "contact_sheet.png"));

            return journal;
        }

        static Dictionary<int, string> LoadProvinceNames()
        {
            var map = new Dictionary<int, string>();
            var defs = GameDataLoader.LoadProvinces();
            for (var i = 0; i < defs.Count; i++)
            {
                var d = defs[i];
                map[d.id] = string.IsNullOrEmpty(d.name) ? $"P{d.id}" : d.name;
            }

            return map;
        }

        static ProvinceSnap[] CaptureProvinces(EntityManager em, Dictionary<int, string> names)
        {
            using var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<ProvinceData>(),
                ComponentType.ReadOnly<ProvinceOwnership>());
            using var provinces = query.ToComponentDataArray<ProvinceData>(Allocator.Temp);
            using var ownerships = query.ToComponentDataArray<ProvinceOwnership>(Allocator.Temp);

            var list = new List<ProvinceSnap>(provinces.Length);
            for (var i = 0; i < provinces.Length; i++)
            {
                var id = provinces[i].ProvinceId;
                var own = ownerships[i];
                var tag = "";
                if (own.Owner != Entity.Null && em.HasComponent<CountryData>(own.Owner))
                    tag = em.GetComponentData<CountryData>(own.Owner).Tag.ToString();

                names.TryGetValue(id, out var name);
                if (string.IsNullOrEmpty(name))
                    name = $"P{id}";

                list.Add(new ProvinceSnap
                {
                    Id = id,
                    Name = name,
                    OwnerTag = tag,
                    OwnerChangedTick = own.OwnerChangedTick
                });
            }

            list.Sort((a, b) => a.Id.CompareTo(b.Id));
            return list.ToArray();
        }

        static Dictionary<string, CountrySnap> CaptureCountries(
            EntityManager em, ProvinceSnap[] provinces)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < provinces.Length; i++)
            {
                var tag = provinces[i].OwnerTag;
                if (string.IsNullOrEmpty(tag))
                    continue;
                counts.TryGetValue(tag, out var n);
                counts[tag] = n + 1;
            }

            var result = new Dictionary<string, CountrySnap>(StringComparer.Ordinal);
            using var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<CountryData>(),
                ComponentType.ReadOnly<TreasuryData>());
            using var countries = query.ToComponentDataArray<CountryData>(Allocator.Temp);
            using var treasuries = query.ToComponentDataArray<TreasuryData>(Allocator.Temp);

            var rows = new List<(string Tag, CountrySnap Snap)>(countries.Length);
            for (var i = 0; i < countries.Length; i++)
            {
                var tag = countries[i].Tag.ToString();
                counts.TryGetValue(tag, out var pc);
                rows.Add((tag, new CountrySnap
                {
                    Tag = tag,
                    ProvinceCount = pc,
                    BankruptcyTick = treasuries[i].BankruptcyTick,
                    Debt = treasuries[i].Debt
                }));
            }

            rows.Sort((a, b) => string.CompareOrdinal(a.Tag, b.Tag));
            for (var i = 0; i < rows.Count; i++)
                result[rows[i].Tag] = rows[i].Snap;

            return result;
        }

        static void DiffSnapshots(
            ProvinceSnap[] prevProv,
            ProvinceSnap[] curProv,
            Dictionary<string, CountrySnap> prevCountries,
            Dictionary<string, CountrySnap> curCountries,
            List<ChronicleEvent> events,
            Dictionary<string, int> annexCountByTag,
            List<(string Tag, int Tick, int LastProvinceId)> deaths)
        {
            var prevById = new Dictionary<int, ProvinceSnap>(prevProv.Length);
            for (var i = 0; i < prevProv.Length; i++)
                prevById[prevProv[i].Id] = prevProv[i];

            for (var i = 0; i < curProv.Length; i++)
            {
                var cur = curProv[i];
                if (!prevById.TryGetValue(cur.Id, out var prev))
                    continue;
                if (string.Equals(prev.OwnerTag, cur.OwnerTag, StringComparison.Ordinal))
                    continue;

                var before = string.IsNullOrEmpty(prev.OwnerTag) ? "---" : prev.OwnerTag;
                var after = string.IsNullOrEmpty(cur.OwnerTag) ? "---" : cur.OwnerTag;
                var tick = cur.OwnerChangedTick > 0 ? cur.OwnerChangedTick : 0;
                var line = string.Format(
                    CultureInfo.InvariantCulture,
                    "t{0:D4} ANNEX province={1} {2} : {3} → {4}",
                    tick, cur.Id, cur.Name, before, after);
                events.Add(new ChronicleEvent
                {
                    Tick = tick,
                    Kind = EventKind.Annex,
                    Key = cur.Id.ToString(CultureInfo.InvariantCulture),
                    Line = line
                });

                if (!string.IsNullOrEmpty(after) && after != "---")
                {
                    annexCountByTag.TryGetValue(after, out var n);
                    annexCountByTag[after] = n + 1;
                }
            }

            var allTags = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var t in prevCountries.Keys) allTags.Add(t);
            foreach (var t in curCountries.Keys) allTags.Add(t);

            foreach (var tag in allTags)
            {
                prevCountries.TryGetValue(tag, out var prevC);
                curCountries.TryGetValue(tag, out var curC);

                // BANKRUPT
                if (curC.BankruptcyTick > prevC.BankruptcyTick)
                {
                    var tick = curC.BankruptcyTick;
                    var line = string.Format(
                        CultureInfo.InvariantCulture,
                        "t{0:D4} BANKRUPT {1} (dette={2})",
                        tick, tag, curC.Debt.ToString("0.###", CultureInfo.InvariantCulture));
                    events.Add(new ChronicleEvent
                    {
                        Tick = tick,
                        Kind = EventKind.Bankrupt,
                        Key = tag,
                        Line = line
                    });
                }

                // DEATH
                if (prevC.ProvinceCount >= 1 && curC.ProvinceCount == 0)
                {
                    var lastId = FindLastLostProvince(prevProv, curProv, tag);
                    var tick = FindDeathTick(prevProv, curProv, tag);
                    var line = string.Format(
                        CultureInfo.InvariantCulture,
                        "t{0:D4} DEATH {1} (dernière province perdue : {2})",
                        tick, tag, lastId);
                    events.Add(new ChronicleEvent
                    {
                        Tick = tick,
                        Kind = EventKind.Death,
                        Key = tag,
                        Line = line
                    });
                    deaths.Add((tag, tick, lastId));
                }

                // RÉSURRECTION
                if (prevC.ProvinceCount == 0 && curC.ProvinceCount >= 1)
                {
                    var tick = FindResurrectionTick(prevProv, curProv, tag);
                    var line = string.Format(
                        CultureInfo.InvariantCulture,
                        "t{0:D4} RESURRECTION {1} (provinces={2})",
                        tick, tag, curC.ProvinceCount);
                    events.Add(new ChronicleEvent
                    {
                        Tick = tick,
                        Kind = EventKind.Resurrection,
                        Key = tag,
                        Line = line
                    });
                }
            }
        }

        static int FindLastLostProvince(ProvinceSnap[] prev, ProvinceSnap[] cur, string tag)
        {
            var curById = new Dictionary<int, ProvinceSnap>(cur.Length);
            for (var i = 0; i < cur.Length; i++)
                curById[cur[i].Id] = cur[i];

            var bestId = -1;
            var bestTick = -1;
            for (var i = 0; i < prev.Length; i++)
            {
                if (!string.Equals(prev[i].OwnerTag, tag, StringComparison.Ordinal))
                    continue;
                if (!curById.TryGetValue(prev[i].Id, out var c))
                    continue;
                if (string.Equals(c.OwnerTag, tag, StringComparison.Ordinal))
                    continue;
                if (c.OwnerChangedTick >= bestTick)
                {
                    bestTick = c.OwnerChangedTick;
                    bestId = prev[i].Id;
                }
            }

            return bestId;
        }

        static int FindDeathTick(ProvinceSnap[] prev, ProvinceSnap[] cur, string tag)
        {
            var curById = new Dictionary<int, ProvinceSnap>(cur.Length);
            for (var i = 0; i < cur.Length; i++)
                curById[cur[i].Id] = cur[i];

            var bestTick = 0;
            for (var i = 0; i < prev.Length; i++)
            {
                if (!string.Equals(prev[i].OwnerTag, tag, StringComparison.Ordinal))
                    continue;
                if (!curById.TryGetValue(prev[i].Id, out var c))
                    continue;
                if (string.Equals(c.OwnerTag, tag, StringComparison.Ordinal))
                    continue;
                if (c.OwnerChangedTick > bestTick)
                    bestTick = c.OwnerChangedTick;
            }

            return bestTick;
        }

        static int FindResurrectionTick(ProvinceSnap[] prev, ProvinceSnap[] cur, string tag)
        {
            var prevById = new Dictionary<int, ProvinceSnap>(prev.Length);
            for (var i = 0; i < prev.Length; i++)
                prevById[prev[i].Id] = prev[i];

            var bestTick = 0;
            for (var i = 0; i < cur.Length; i++)
            {
                if (!string.Equals(cur[i].OwnerTag, tag, StringComparison.Ordinal))
                    continue;
                if (!prevById.TryGetValue(cur[i].Id, out var p))
                    continue;
                if (string.Equals(p.OwnerTag, tag, StringComparison.Ordinal))
                    continue;
                if (cur[i].OwnerChangedTick > bestTick)
                    bestTick = cur[i].OwnerChangedTick;
            }

            return bestTick;
        }

        static void CollectWars(EntityManager em, List<ChronicleEvent> events)
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<WarData>());
            using var wars = query.ToComponentDataArray<WarData>(Allocator.Temp);

            var rows = new List<(string Atk, string Def, int Start, WarData War)>(wars.Length);
            for (var i = 0; i < wars.Length; i++)
            {
                var w = wars[i];
                var atk = ResolveTag(em, w.Attacker);
                var def = ResolveTag(em, w.Defender);
                rows.Add((atk, def, w.StartTick, w));
            }

            rows.Sort((a, b) =>
            {
                var c = string.CompareOrdinal(a.Atk, b.Atk);
                if (c != 0) return c;
                c = string.CompareOrdinal(a.Def, b.Def);
                if (c != 0) return c;
                return a.Start.CompareTo(b.Start);
            });

            for (var i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                var w = r.War;
                var startKey = string.Format(
                    CultureInfo.InvariantCulture, "{0}|{1}|{2}|S", r.Atk, r.Def, r.Start);
                var startLine = string.Format(
                    CultureInfo.InvariantCulture,
                    "t{0:D4} WAR_START {1} → {2} cb={3}",
                    w.StartTick, r.Atk, r.Def, w.CasusBelli);
                events.Add(new ChronicleEvent
                {
                    Tick = w.StartTick,
                    Kind = EventKind.WarStart,
                    Key = startKey,
                    Line = startLine
                });

                if (w.EndTick > 0)
                {
                    var issue = ResolveWarIssue(w.WarScore);
                    var endKey = string.Format(
                        CultureInfo.InvariantCulture, "{0}|{1}|{2}|E", r.Atk, r.Def, r.Start);
                    var endLine = string.Format(
                        CultureInfo.InvariantCulture,
                        "t{0:D4} WAR_END {1} vs {2} score={3} issue={4}",
                        w.EndTick, r.Atk, r.Def,
                        w.WarScore.ToString("0.###", CultureInfo.InvariantCulture),
                        issue);
                    events.Add(new ChronicleEvent
                    {
                        Tick = w.EndTick,
                        Kind = EventKind.WarEnd,
                        Key = endKey,
                        Line = endLine
                    });
                }
            }
        }

        /// <summary>Même règle que WorldMetrics : |WarScore|≥60 → victoire (signe = camp), sinon paix blanche.</summary>
        public static string ResolveWarIssue(float warScore)
        {
            if (Math.Abs(warScore) >= 60f)
                return warScore > 0f ? "VICTOIRE_ATTAQUANT" : "VICTOIRE_DÉFENSEUR";
            return "PAIX_BLANCHE";
        }

        static string ResolveTag(EntityManager em, Entity e)
        {
            if (e == Entity.Null || !em.HasComponent<CountryData>(e))
                return "???";
            return em.GetComponentData<CountryData>(e).Tag.ToString();
        }

        static string FormatJournal(
            List<ChronicleEvent> events,
            Dictionary<string, int> annexCountByTag,
            List<(string Tag, int Tick, int LastProvinceId)> deaths,
            List<(int Tick, int Countries)> landCurve)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== v1_004 CHRONICLE JOURNAL (dérivé, lecture seule) ===");
            sb.AppendLine("Events sorted by (tick, kind, key). No wall-clock timestamps.");
            sb.AppendLine();

            for (var i = 0; i < events.Count; i++)
                sb.AppendLine(events[i].Line);

            sb.AppendLine();
            sb.AppendLine("=== SYNTHÈSE ===");

            var counts = new int[6];
            for (var i = 0; i < events.Count; i++)
                counts[(int)events[i].Kind]++;

            sb.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "counts ANNEX={0} BANKRUPT={1} DEATH={2} RESURRECTION={3} WAR_START={4} WAR_END={5} TOTAL={6}",
                counts[0], counts[1], counts[2], counts[3], counts[4], counts[5], events.Count));

            if (counts[3] == 0)
                sb.AppendLine("NOTE RESURRECTION: aucun événement (pays 0→≥1 province) observé.");

            var annexTags = new List<string>(annexCountByTag.Keys);
            annexTags.Sort((a, b) =>
            {
                var c = annexCountByTag[b].CompareTo(annexCountByTag[a]);
                if (c != 0) return c;
                return string.CompareOrdinal(a, b);
            });
            sb.Append("top5_annexers:");
            var shown = 0;
            for (var i = 0; i < annexTags.Count && shown < 5; i++)
            {
                sb.Append(' ').Append(annexTags[i]).Append('=')
                    .Append(annexCountByTag[annexTags[i]].ToString(CultureInfo.InvariantCulture));
                shown++;
            }

            if (shown == 0)
                sb.Append(" (none)");
            sb.AppendLine();

            deaths.Sort((a, b) =>
            {
                var c = a.Tick.CompareTo(b.Tick);
                if (c != 0) return c;
                return string.CompareOrdinal(a.Tag, b.Tag);
            });
            sb.Append("deaths:");
            if (deaths.Count == 0)
                sb.Append(" (none)");
            else
            {
                for (var i = 0; i < deaths.Count; i++)
                {
                    sb.Append(' ').Append(deaths[i].Tag).Append('@')
                        .Append(deaths[i].Tick.ToString(CultureInfo.InvariantCulture));
                }
            }

            sb.AppendLine();
            sb.Append("land_countries_curve:");
            for (var i = 0; i < landCurve.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append('t').Append(landCurve[i].Tick.ToString("D4", CultureInfo.InvariantCulture))
                    .Append('=').Append(landCurve[i].Countries.ToString(CultureInfo.InvariantCulture));
            }

            sb.AppendLine();
            return sb.ToString();
        }

        static string Sha256Hex(string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(bytes);
            var sb = new StringBuilder(hash.Length * 2);
            for (var i = 0; i < hash.Length; i++)
                sb.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        static void AssembleContactSheet(string[] framePaths, string outputPath)
        {
            var sheetW = ContactCols * ChronicleWidth;
            var sheetH = ContactRows * ChronicleHeight;
            var sheet = new Color32[sheetW * sheetH];
            var sea = CountryColors.Load().Sea;
            for (var i = 0; i < sheet.Length; i++)
                sheet[i] = sea;

            for (var i = 0; i < framePaths.Length; i++)
            {
                if (string.IsNullOrEmpty(framePaths[i]) || !File.Exists(framePaths[i]))
                    continue;

                var col = i % ContactCols;
                var row = i / ContactCols;
                if (row >= ContactRows)
                    break;

                var bytes = File.ReadAllBytes(framePaths[i]);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!ImageConversion.LoadImage(tex, bytes, markNonReadable: false))
                {
                    UnityEngine.Object.DestroyImmediate(tex);
                    continue;
                }

                var frame = tex.GetPixels32();
                var fw = tex.width;
                var fh = tex.height;
                UnityEngine.Object.DestroyImmediate(tex);

                var ox = col * ChronicleWidth;
                var oy = (ContactRows - 1 - row) * ChronicleHeight; // y=0 bas
                for (var fy = 0; fy < fh && fy < ChronicleHeight; fy++)
                {
                    for (var fx = 0; fx < fw && fx < ChronicleWidth; fx++)
                    {
                        var src = fy * fw + fx;
                        var dst = (oy + fy) * sheetW + (ox + fx);
                        sheet[dst] = frame[src];
                    }
                }
            }

            MapSnapshotExporter.WritePngSized(sheet, sheetW, sheetH, outputPath);
            Debug.Log($"ChronicleExporter: contact_sheet → {outputPath} ({sheetW}x{sheetH})");
        }
    }
}
