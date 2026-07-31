using Unity.Entities;
using Unity.Collections;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using VictoriaGame.Core;
using VictoriaGame.Economy;
using VictoriaGame.Military;
using VictoriaGame.Population;
using VictoriaGame.World;

namespace VictoriaGame.Presentation
{
    /// <summary>
    /// Couches thématiques (satisfaction, population, armée, trésorerie, nœud commercial)
    /// branchées sur MapGeometry en cache — aucune écriture ECS, aucun ISystem.
    /// </summary>
    public static class MapLayerRenderer
    {
        public const int Width = MapSnapshotExporter.Width;
        public const int Height = MapSnapshotExporter.Height;

        static readonly int[] ObservationTicks = { 0, 200, 500, 1000 };
        static readonly Color32 LegendBg = new Color32(0x12, 0x14, 0x18, 230);
        static readonly Color32 TextFg = new Color32(0xf0, 0xf0, 0xf0, 255);
        static readonly Color32 TextHalo = new Color32(0x08, 0x08, 0x08, 255);

        /// <summary>Nombre d'appels à BuildMapGeometry pendant Run (doit rester 1).</summary>
        public static int GeometryBuildCount;

        public enum LayerKind
        {
            Satisfaction = 0,
            Population = 1,
            Army = 2,
            Treasury = 3,
            TradeNode = 4
        }

        public struct LayerFrame
        {
            public int Tick;
            public float[] Satisfaction;
            public float[] Population;
            public float[] Army;
            public float[] Treasury;
            public bool[] TreasuryValid;
            public int[] TradeNode;
            public bool[] HasPops;
            /// <summary>TAG propriétaire résolu à la capture (jamais d'Entity différée).</summary>
            public string[] OwnerTags;
            /// <summary>TAG contrôleur résolu à la capture (hachures d'occupation).</summary>
            public string[] ControllerTags;
            public bool[] Occupied;
            /// <summary>Vues politiques immuables du tick — le rendu ne lit plus l'EM.</summary>
            public List<MapSnapshotExporter.ProvinceView> PoliticalViews;
        }

        public struct CanaryResult
        {
            public int Tick;
            public string Prov1Tag;
            public string Prov6Tag;
            public bool Ok;
            public string Line;
        }

        public struct PixelDiffResult
        {
            public string Name;
            public int DiffBytes;
            public bool Ok;
            public string Line;
        }

        public struct FixedDomains
        {
            public float SatMin, SatMax;
            public float PopMin, PopMax;
            public float ArmyMin, ArmyMax;
            public float TreasuryAbsMax;
        }

        public struct LayerStats
        {
            public int Tick;
            public string Layer;
            public float Min, Max, Median;
            public int SampleCount;
            public string Extra;
        }

        public sealed class Palettes
        {
            public Color32 NoData;
            public Color32[] Satisfaction;
            public Color32[] Population;
            public Color32[] Army;
            public Color32[] TreasuryNeg; // centre → extrême
            public Color32 TreasuryMid;
            public Color32[] TreasuryPos; // centre → extrême
            public Dictionary<int, Color32> TradeNodeById;
            public float SatDomainMin, SatDomainMax;
            public bool SatDomainFromJson;
            public Color32 StatusOwned;
            public Color32 StatusOccupied;
            public Color32 StatusCapitalOccupied;
        }

        [Serializable]
        class PalettesFile
        {
            public string comment;
            public string no_data;
            public SequentialBlock sequential;
            public DivergingBlock diverging;
            public CategoricalBlock categorical;
            public StatusBlock status;
        }

        [Serializable]
        class SequentialBlock
        {
            public SequentialRamp satisfaction;
            public SequentialRamp population;
            public SequentialRamp army;
        }

        [Serializable]
        class SequentialRamp
        {
            public string hue;
            public string domain;
            public string[] steps;
        }

        [Serializable]
        class DivergingBlock
        {
            public DivergingRamp treasury;
        }

        [Serializable]
        class DivergingRamp
        {
            public string comment;
            public string[] negative_pole;
            public string midpoint;
            public string[] positive_pole;
        }

        [Serializable]
        class CategoricalBlock
        {
            public TradeNodePalette trade_node;
        }

        [Serializable]
        class TradeNodePalette
        {
            public string comment;
            public TradeNodeColor[] colors;
        }

        [Serializable]
        class TradeNodeColor
        {
            public int node;
            public string color;
            public string note;
        }

        [Serializable]
        class StatusBlock
        {
            public string comment;
            public string owned_controlled;
            public string occupied;
            public string capital_occupied;
        }

        /// <summary>
        /// Capture t0/200/500/1000 (valeurs + tags Owner/Controller), domaines FIXES,
        /// 20 PNG + 2 planches compare. Géométrie UNE SEULE FOIS.
        /// Le rendu ne touche JAMAIS l'EntityManager (v1_006).
        /// </summary>
        public static FixedDomains Run(
            EntityManager em,
            Action<int> advanceBy,
            string outputDir,
            out List<LayerStats> stats,
            out bool landMassesOk,
            out List<CanaryResult> canaries,
            out List<PixelDiffResult> pixelDiffs)
        {
            Directory.CreateDirectory(outputDir);
            GeometryBuildCount = 0;
            canaries = new List<CanaryResult>(ObservationTicks.Length);
            pixelDiffs = new List<PixelDiffResult>(2);

            var palettes = LoadPalettes();
            var colors = CountryColors.Load();
            var geo = MapSnapshotExporter.BuildMapGeometry(Width, Height);
            GeometryBuildCount++;
            landMassesOk = geo != null && geo.LandMasses.MatchesTarget;
            Debug.Log(
                $"MapLayerRenderer: geometry builds={GeometryBuildCount} " +
                (geo != null ? geo.LandMasses.Summary : "geo=null"));

            var frames = new List<LayerFrame>(ObservationTicks.Length);
            var tickCursor = 0;
            for (var i = 0; i < ObservationTicks.Length; i++)
            {
                var tick = ObservationTicks[i];
                if (tick > tickCursor)
                {
                    advanceBy(tick - tickCursor);
                    tickCursor = tick;
                }

                var frame = CaptureFrame(em, geo, colors, tick);
                frames.Add(frame);
                canaries.Add(EvaluateCanary(frame, geo));
            }

            // Domaines FIXES hérités de v1_005 (ne pas recalculer sur les nouvelles lectures).
            var domains = FixedDomainsV1005(palettes);
            stats = new List<LayerStats>(64);
            CollectStats(frames, stats);

            var politicalPaths = new Dictionary<int, string>();
            Color32[] politicalT0 = null;
            Color32[] politicalT1000 = null;
            Color32[] tradenodeT0 = null;
            Color32[] tradenodeT1000 = null;

            for (var fi = 0; fi < frames.Count; fi++)
            {
                var frame = frames[fi];
                RenderLayer(geo, frame, LayerKind.Satisfaction, palettes, domains, colors, outputDir);
                RenderLayer(geo, frame, LayerKind.Population, palettes, domains, colors, outputDir);
                RenderLayer(geo, frame, LayerKind.Army, palettes, domains, colors, outputDir);
                RenderLayer(geo, frame, LayerKind.Treasury, palettes, domains, colors, outputDir);
                var tradePixels = RenderLayer(
                    geo, frame, LayerKind.TradeNode, palettes, domains, colors, outputDir);

                if (frame.Tick == 0)
                    tradenodeT0 = tradePixels;
                else if (frame.Tick == 1000)
                    tradenodeT1000 = tradePixels;

                if (frame.Tick == 0 || frame.Tick == 1000)
                {
                    var polPath = Path.Combine(
                        outputDir,
                        string.Format(CultureInfo.InvariantCulture, "political_t{0:D4}.png", frame.Tick));
                    var polPixels = MapSnapshotExporter.ExportWithGeometryFromViews(
                        frame.PoliticalViews, frame.Tick, polPath, geo,
                        drawLabels: true, tickCartouche: null, colors);
                    politicalPaths[frame.Tick] = polPath;
                    landMassesOk &= MapSnapshotExporter.LastLandMassReport.MatchesTarget;
                    if (frame.Tick == 0)
                        politicalT0 = polPixels;
                    else
                        politicalT1000 = polPixels;
                }
            }

            // Contrôle pixel : political t0 ≠ t1000 (pleine image, pas de bandeau légende).
            var polDiff = MapSnapshotExporter.CountPixelByteDiffs(
                politicalT0, politicalT1000, Width, Height, excludeBottomRows: 0);
            var polOk = polDiff > 0;
            var polLine = string.Format(
                CultureInfo.InvariantCulture,
                "PIXELDIFF political_t0000 vs political_t1000 bytes={0} {1}",
                polDiff, polOk ? "OK" : "FAIL");
            pixelDiffs.Add(new PixelDiffResult
            {
                Name = "political", DiffBytes = polDiff, Ok = polOk, Line = polLine
            });
            Debug.Log(polLine);

            // Contrôle pixel : tradenode t0 ≠ t1000 hors bandeau légende (étiquettes).
            const int legendBand = 72;
            var tnDiff = MapSnapshotExporter.CountPixelByteDiffs(
                tradenodeT0, tradenodeT1000, Width, Height, excludeBottomRows: legendBand);
            var tnOk = tnDiff > 0;
            var tnLine = string.Format(
                CultureInfo.InvariantCulture,
                "PIXELDIFF tradenode_t0000 vs tradenode_t1000 bytes={0} (hors légende) {1}",
                tnDiff, tnOk ? "OK" : "FAIL");
            pixelDiffs.Add(new PixelDiffResult
            {
                Name = "tradenode", DiffBytes = tnDiff, Ok = tnOk, Line = tnLine
            });
            Debug.Log(tnLine);

            AssembleCompare(outputDir, 0, politicalPaths);
            AssembleCompare(outputDir, 1000, politicalPaths);

            Debug.Log(
                $"MapLayerRenderer: done builds={GeometryBuildCount} " +
                $"domains sat=[{domains.SatMin.ToString("0.###", CultureInfo.InvariantCulture)}.." +
                $"{domains.SatMax.ToString("0.###", CultureInfo.InvariantCulture)}] " +
                $"pop=[{domains.PopMin.ToString("0", CultureInfo.InvariantCulture)}.." +
                $"{domains.PopMax.ToString("0", CultureInfo.InvariantCulture)}] " +
                $"army=[{domains.ArmyMin.ToString("0", CultureInfo.InvariantCulture)}.." +
                $"{domains.ArmyMax.ToString("0", CultureInfo.InvariantCulture)}] " +
                $"treasury=±{domains.TreasuryAbsMax.ToString("0.#", CultureInfo.InvariantCulture)}");

            return domains;
        }

        /// <summary>Surcharge compat : ignore canaries/diffs.</summary>
        public static FixedDomains Run(
            EntityManager em,
            Action<int> advanceBy,
            string outputDir,
            out List<LayerStats> stats,
            out bool landMassesOk)
        {
            return Run(em, advanceBy, outputDir, out stats, out landMassesOk, out _, out _);
        }

        public static Palettes LoadPalettes()
        {
            var path = Path.Combine(Application.streamingAssetsPath, "data", "map_layer_palettes.json");
            if (!File.Exists(path))
                throw new FileNotFoundException("map_layer_palettes.json introuvable", path);

            var json = File.ReadAllText(path);
            var data = JsonUtility.FromJson<PalettesFile>(json);
            if (data == null)
                throw new InvalidOperationException("map_layer_palettes.json: parse null");

            var p = new Palettes
            {
                NoData = CountryColors.ParseHex(data.no_data),
                Satisfaction = ParseSteps(data.sequential?.satisfaction?.steps),
                Population = ParseSteps(data.sequential?.population?.steps),
                Army = ParseSteps(data.sequential?.army?.steps),
                TreasuryNeg = ParseSteps(data.diverging?.treasury?.negative_pole),
                TreasuryMid = CountryColors.ParseHex(data.diverging?.treasury?.midpoint),
                TreasuryPos = ParseSteps(data.diverging?.treasury?.positive_pole),
                TradeNodeById = new Dictionary<int, Color32>(),
                StatusOwned = CountryColors.ParseHex(data.status?.owned_controlled),
                StatusOccupied = CountryColors.ParseHex(data.status?.occupied),
                StatusCapitalOccupied = CountryColors.ParseHex(data.status?.capital_occupied)
            };

            if (TryParseNumericDomain(data.sequential?.satisfaction?.domain, out var sLo, out var sHi))
            {
                p.SatDomainMin = sLo;
                p.SatDomainMax = sHi;
                p.SatDomainFromJson = true;
            }

            if (data.categorical?.trade_node?.colors != null)
            {
                for (var i = 0; i < data.categorical.trade_node.colors.Length; i++)
                {
                    var e = data.categorical.trade_node.colors[i];
                    p.TradeNodeById[e.node] = CountryColors.ParseHex(e.color);
                }
            }

            return p;
        }

        static Color32[] ParseSteps(string[] steps)
        {
            if (steps == null || steps.Length == 0)
                return Array.Empty<Color32>();
            var arr = new Color32[steps.Length];
            for (var i = 0; i < steps.Length; i++)
                arr[i] = CountryColors.ParseHex(steps[i]);
            return arr;
        }

        static bool TryParseNumericDomain(string domainText, out float lo, out float hi)
        {
            lo = 0f;
            hi = 1f;
            if (string.IsNullOrEmpty(domainText))
                return false;
            var m = Regex.Match(domainText, @"(-?\d+(?:\.\d+)?)\.\.(-?\d+(?:\.\d+)?)");
            if (!m.Success)
                return false;
            lo = float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            hi = float.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            return true;
        }

        /// <summary>
        /// Domaines FIXES calibrés (v1_005) — sat=[0..1], pop=[0..4341], army=[0..11825], treasury=±444.
        /// Ne pas auto-échelonner sur le tick courant.
        /// </summary>
        public static FixedDomains GetFixedDomains(Palettes palettes = null)
        {
            if (palettes == null)
                palettes = LoadPalettes();
            return FixedDomainsV1005(palettes);
        }

        /// <summary>
        /// Point d'entrée public (échelon D) : capture lecture seule des valeurs thématiques.
        /// Une seule implémentation — MapDisplaySystem ne doit pas la dupliquer.
        /// </summary>
        public static LayerFrame CaptureFrame(
            EntityManager em,
            MapSnapshotExporter.MapGeometry geo,
            CountryColors.Table colors,
            int tick)
        {
            var n = geo.ViewsSkeleton.Count;
            var satSum = new double[n];
            var satWeight = new double[n];
            var pop = new float[n];
            var army = new float[n];
            var treasury = new float[n];
            var treasuryValid = new bool[n];
            var tradeNode = new int[n];
            var hasPops = new bool[n];
            var ownerTags = new string[n];
            var ownerNames = new string[n];
            var controllerTags = new string[n];
            var occupied = new bool[n];
            for (var i = 0; i < n; i++)
            {
                ownerTags[i] = "";
                ownerNames[i] = "";
                controllerTags[i] = "";
            }

            var idToView = new Dictionary<int, int>(n);
            for (var i = 0; i < n; i++)
                idToView[geo.ViewsSkeleton[i].Id] = i;

            if (PilotMapProvider.Enabled)
            {
                for (var i = 0; i < n; i++)
                {
                    ownerTags[i] = geo.ViewsSkeleton[i].OwnerTag ?? "";
                    ownerNames[i] = geo.ViewsSkeleton[i].OwnerName ?? "";
                }
            }

            var entityToView = new Dictionary<Entity, int>(n);
            using (var pq = em.CreateEntityQuery(
                       ComponentType.ReadOnly<ProvinceData>(),
                       ComponentType.ReadOnly<ProvinceOwnership>()))
            using (var entities = pq.ToEntityArray(Allocator.Temp))
            using (var provinces = pq.ToComponentDataArray<ProvinceData>(Allocator.Temp))
            using (var ownerships = pq.ToComponentDataArray<ProvinceOwnership>(Allocator.Temp))
            {
                for (var i = 0; i < provinces.Length; i++)
                {
                    if (!idToView.TryGetValue(provinces[i].ProvinceId, out var vi))
                        continue;
                    entityToView[entities[i]] = vi;
                    tradeNode[vi] = provinces[i].TradeNodeId;

                    var own = ownerships[i];
                    // Résoudre Entity → TAG + NOM MAINTENANT (pas au rendu).
                    if (own.Owner != Entity.Null && em.HasComponent<CountryData>(own.Owner))
                    {
                        var cd = em.GetComponentData<CountryData>(own.Owner);
                        ownerTags[vi] = cd.Tag.ToString();
                        var nm = cd.Name.ToString();
                        ownerNames[vi] = string.IsNullOrEmpty(nm)
                            ? colors.NameForTag(ownerTags[vi])
                            : nm;
                    }

                    var isOcc = own.Controller != Entity.Null
                        && own.Owner != Entity.Null
                        && own.Controller != own.Owner;
                    occupied[vi] = isOcc;
                    if (isOcc && em.HasComponent<CountryData>(own.Controller))
                        controllerTags[vi] =
                            em.GetComponentData<CountryData>(own.Controller).Tag.ToString();

                    if (own.Owner != Entity.Null && em.HasComponent<TreasuryData>(own.Owner))
                    {
                        treasury[vi] = em.GetComponentData<TreasuryData>(own.Owner).Balance;
                        treasuryValid[vi] = true;
                    }
                }
            }

            using (var popQ = em.CreateEntityQuery(ComponentType.ReadOnly<PopData>()))
            using (var pops = popQ.ToComponentDataArray<PopData>(Allocator.Temp))
            {
                for (var i = 0; i < pops.Length; i++)
                {
                    var p = pops[i];
                    if (p.Province == Entity.Null)
                        continue;
                    if (!entityToView.TryGetValue(p.Province, out var vi))
                        continue;
                    satSum[vi] += p.NeedsSatisfaction * p.Size;
                    satWeight[vi] += p.Size;
                    pop[vi] += p.Size;
                    hasPops[vi] = true;
                }
            }

            var satisfaction = new float[n];
            for (var i = 0; i < n; i++)
            {
                if (satWeight[i] > 0.0)
                    satisfaction[i] = (float)(satSum[i] / satWeight[i]);
            }

            using (var aq = em.CreateEntityQuery(ComponentType.ReadOnly<ArmyData>()))
            using (var armies = aq.ToComponentDataArray<ArmyData>(Allocator.Temp))
            {
                for (var i = 0; i < armies.Length; i++)
                {
                    if (!idToView.TryGetValue(armies[i].ProvinceId, out var vi))
                        continue;
                    army[vi] += armies[i].Strength;
                }
            }

            var politicalViews = MapSnapshotExporter.BuildViewsFromTags(
                geo.ViewsSkeleton, ownerTags, controllerTags, occupied, colors, ownerNames);

            return new LayerFrame
            {
                Tick = tick,
                Satisfaction = satisfaction,
                Population = pop,
                Army = army,
                Treasury = treasury,
                TreasuryValid = treasuryValid,
                TradeNode = tradeNode,
                HasPops = hasPops,
                OwnerTags = ownerTags,
                ControllerTags = controllerTags,
                Occupied = occupied,
                PoliticalViews = politicalViews
            };
        }

        static CanaryResult EvaluateCanary(LayerFrame frame, MapSnapshotExporter.MapGeometry geo)
        {
            string TagOf(int provinceId)
            {
                for (var i = 0; i < geo.ViewsSkeleton.Count; i++)
                {
                    if (geo.ViewsSkeleton[i].Id == provinceId)
                        return frame.OwnerTags[i] ?? "";
                }
                return "";
            }

            var p1 = TagOf(1);
            var p6 = TagOf(6);
            bool ok;
            string expected;
            if (frame.Tick == 0)
            {
                expected = "prov1=FRA prov6=BUR";
                ok = string.Equals(p1, "FRA", StringComparison.Ordinal)
                     && string.Equals(p6, "BUR", StringComparison.Ordinal);
            }
            else if (frame.Tick == 1000)
            {
                expected = "prov1=BUR prov6=FRA";
                ok = string.Equals(p1, "BUR", StringComparison.Ordinal)
                     && string.Equals(p6, "FRA", StringComparison.Ordinal);
            }
            else
            {
                // t200/t500 : loguer l'état observé (échange attendu quelque part entre 0 et 1000).
                expected = "observed";
                ok = !string.IsNullOrEmpty(p1) && !string.IsNullOrEmpty(p6);
            }

            var line = string.Format(
                CultureInfo.InvariantCulture,
                "CANARY t{0:D4} prov1={1} prov6={2} {3}{4}",
                frame.Tick, p1, p6,
                ok ? "OK" : "FAIL",
                frame.Tick == 0 || frame.Tick == 1000
                    ? " (attendu " + expected + ")"
                    : "");
            Debug.Log(line);
            return new CanaryResult
            {
                Tick = frame.Tick,
                Prov1Tag = p1,
                Prov6Tag = p6,
                Ok = ok,
                Line = line
            };
        }

        /// <summary>
        /// Domaines FIXES de v1_005 — ne pas recalculer (comparabilité inter-ticks).
        /// </summary>
        static FixedDomains FixedDomainsV1005(Palettes palettes)
        {
            var d = new FixedDomains
            {
                SatMin = palettes.SatDomainFromJson ? palettes.SatDomainMin : 0f,
                SatMax = palettes.SatDomainFromJson ? palettes.SatDomainMax : 1f,
                PopMin = 0f,
                PopMax = 4341f,
                ArmyMin = 0f,
                ArmyMax = 11825f,
                TreasuryAbsMax = 444f
            };
            return d;
        }

        static void CollectStats(List<LayerFrame> frames, List<LayerStats> stats)
        {
            for (var f = 0; f < frames.Count; f++)
            {
                var fr = frames[f];
                // Lectures aux QUATRE ticks (v1_006 : t0/t200/t500 n'avaient jamais été observés correctement).

                AddNumericStats(stats, fr.Tick, "satisfaction", fr.Satisfaction, fr.HasPops, true);
                AddNumericStats(stats, fr.Tick, "population", fr.Population, null, false);
                AddNumericStats(stats, fr.Tick, "army", fr.Army, null, false);

                var treasMask = fr.TreasuryValid;
                AddNumericStats(stats, fr.Tick, "treasury", fr.Treasury, treasMask, true);

                // Spread sat par nœud (moyenne des provinces du nœud)
                var nodeSat = new Dictionary<int, List<float>>();
                for (var i = 0; i < fr.Satisfaction.Length; i++)
                {
                    if (!fr.HasPops[i]) continue;
                    var node = fr.TradeNode[i];
                    if (!nodeSat.TryGetValue(node, out var list))
                    {
                        list = new List<float>();
                        nodeSat[node] = list;
                    }
                    list.Add(fr.Satisfaction[i]);
                }

                var nodeKeys = new List<int>(nodeSat.Keys);
                nodeKeys.Sort();
                var sb = new StringBuilder();
                float nodeMin = float.MaxValue, nodeMax = float.MinValue;
                for (var k = 0; k < nodeKeys.Count; k++)
                {
                    var list = nodeSat[nodeKeys[k]];
                    double sum = 0;
                    for (var j = 0; j < list.Count; j++) sum += list[j];
                    var avg = (float)(sum / list.Count);
                    if (avg < nodeMin) nodeMin = avg;
                    if (avg > nodeMax) nodeMax = avg;
                    if (k > 0) sb.Append(' ');
                    sb.Append('n').Append(nodeKeys[k].ToString(CultureInfo.InvariantCulture))
                        .Append('=')
                        .Append(avg.ToString("0.000", CultureInfo.InvariantCulture));
                }

                stats.Add(new LayerStats
                {
                    Tick = fr.Tick,
                    Layer = "sat_by_node",
                    Min = nodeMin == float.MaxValue ? 0f : nodeMin,
                    Max = nodeMax == float.MinValue ? 0f : nodeMax,
                    Median = (nodeMin == float.MaxValue) ? 0f : (nodeMin + nodeMax) * 0.5f,
                    SampleCount = nodeKeys.Count,
                    Extra = string.Format(
                        CultureInfo.InvariantCulture,
                        "spread={0:0.000} {1}",
                        (nodeMax == float.MinValue ? 0f : nodeMax - nodeMin),
                        sb)
                });
            }
        }

        static void AddNumericStats(
            List<LayerStats> stats,
            int tick,
            string layer,
            float[] values,
            bool[] mask,
            bool useMask)
        {
            var buf = new List<float>(values.Length);
            for (var i = 0; i < values.Length; i++)
            {
                if (useMask && (mask == null || !mask[i]))
                    continue;
                buf.Add(values[i]);
            }

            if (buf.Count == 0)
            {
                stats.Add(new LayerStats
                {
                    Tick = tick, Layer = layer, Min = 0, Max = 0, Median = 0, SampleCount = 0
                });
                return;
            }

            buf.Sort();
            var min = buf[0];
            var max = buf[buf.Count - 1];
            float median;
            var mid = buf.Count / 2;
            if ((buf.Count & 1) == 1)
                median = buf[mid];
            else
                median = 0.5f * (buf[mid - 1] + buf[mid]);

            stats.Add(new LayerStats
            {
                Tick = tick,
                Layer = layer,
                Min = min,
                Max = max,
                Median = median,
                SampleCount = buf.Count
            });
        }

        /// <summary>
        /// Rendu thématique in-game (pixels + légende), sans écrire de PNG.
        /// Réutilise CaptureFrame + palettes/domaines fixes — zéro reconstruction de géométrie.
        /// </summary>
        public static Color32[] RenderLayerToPixels(
            MapSnapshotExporter.MapGeometry geo,
            LayerFrame frame,
            LayerKind kind,
            Palettes palettes,
            FixedDomains domains,
            CountryColors.Table colors,
            Action<Color32[]> extraOverlay = null)
        {
            if (geo == null || frame.PoliticalViews == null)
                return null;

            var n = geo.ViewsSkeleton.Count;
            Color32[] fills;
            if (PilotMapProvider.Enabled)
            {
                // v1_070 — politique = tag propriétaire ; physique = classe de terrain.
                // (v1_068 forçait TOUJOURS le terrain → mosaïque verte en « politique ».)
                fills = PilotMapProvider.ActiveColorMode == PilotMapProvider.ColorMode.Terrain
                    ? PilotMapProvider.BuildTerrainFills(n)
                    : PilotMapProvider.BuildPoliticalFills(n);
            }
            else
            {
                fills = new Color32[n];
                for (var i = 0; i < n; i++)
                    fills[i] = ColorFor(kind, frame, i, palettes, domains);
            }

            return MapSnapshotExporter.RenderThematicPixels(
                frame.PoliticalViews, geo, fills,
                pixels =>
                {
                    if (!PilotMapProvider.Enabled)
                        DrawLegend(pixels, geo.Width, geo.Height, kind, palettes, domains, frame.Tick);
                    else
                        PilotMapProvider.ApplyUnownedHatch(
                            frame.PoliticalViews, pixels, geo.ProvinceAt, geo.Width, geo.Height);
                    extraOverlay?.Invoke(pixels);
                },
                colors);
        }

        static Color32[] RenderLayer(
            MapSnapshotExporter.MapGeometry geo,
            LayerFrame frame,
            LayerKind kind,
            Palettes palettes,
            FixedDomains domains,
            CountryColors.Table colors,
            string outputDir)
        {
            var name = LayerFileName(kind);
            var path = Path.Combine(
                outputDir,
                string.Format(CultureInfo.InvariantCulture, "{0}_t{1:D4}.png", name, frame.Tick));

            // Snapshot immuable uniquement — zéro EntityManager au rendu.
            var pixels = RenderLayerToPixels(geo, frame, kind, palettes, domains, colors);
            if (pixels == null)
                return null;
            MapSnapshotExporter.WriteMapBufferPng(pixels, geo.Width, geo.Height, path);
            Debug.Log($"MapLayerRenderer: {name} t{frame.Tick:D4} → {path}");
            return pixels;
        }

        static Color32 ColorFor(
            LayerKind kind, LayerFrame frame, int i, Palettes p, FixedDomains d)
        {
            switch (kind)
            {
                case LayerKind.Satisfaction:
                    if (!frame.HasPops[i]) return p.NoData;
                    return SampleSequential(p.Satisfaction, frame.Satisfaction[i], d.SatMin, d.SatMax);
                case LayerKind.Population:
                    return SampleSequential(p.Population, frame.Population[i], d.PopMin, d.PopMax);
                case LayerKind.Army:
                    return SampleSequential(p.Army, frame.Army[i], d.ArmyMin, d.ArmyMax);
                case LayerKind.Treasury:
                    if (!frame.TreasuryValid[i]) return p.NoData;
                    return SampleDiverging(
                        p.TreasuryNeg, p.TreasuryMid, p.TreasuryPos,
                        frame.Treasury[i], d.TreasuryAbsMax);
                case LayerKind.TradeNode:
                    if (p.TradeNodeById.TryGetValue(frame.TradeNode[i], out var c))
                        return c;
                    return p.NoData;
                default:
                    return p.NoData;
            }
        }

        static Color32 SampleSequential(Color32[] steps, float value, float min, float max)
        {
            if (steps == null || steps.Length == 0)
                return new Color32(0, 0, 0, 255);
            if (steps.Length == 1)
                return steps[0];

            var span = max - min;
            float t;
            if (span <= 1e-12f)
                t = 0f;
            else
                t = (value - min) / span;
            if (t < 0f) t = 0f;
            if (t > 1f) t = 1f;

            var scaled = t * (steps.Length - 1);
            var i0 = (int)scaled;
            if (i0 >= steps.Length - 1)
                return steps[steps.Length - 1];
            var frac = scaled - i0;
            return LerpColor(steps[i0], steps[i0 + 1], frac);
        }

        static Color32 SampleDiverging(
            Color32[] neg, Color32 mid, Color32[] pos, float value, float absMax)
        {
            if (absMax <= 1e-12f)
                return mid;

            var t = value / absMax;
            if (t < -1f) t = -1f;
            if (t > 1f) t = 1f;

            if (Math.Abs(t) < 1e-6f)
                return mid;

            if (t < 0f)
            {
                // 0 → mid, 1 → extreme (neg[last]) ; poles ordonnés centre→extrême
                var u = -t;
                return SamplePole(mid, neg, u);
            }
            else
            {
                return SamplePole(mid, pos, t);
            }
        }

        static Color32 SamplePole(Color32 mid, Color32[] pole, float u)
        {
            if (pole == null || pole.Length == 0)
                return mid;
            // mid + pole[0..n-1] => n+1 stops
            var stops = new Color32[pole.Length + 1];
            stops[0] = mid;
            for (var i = 0; i < pole.Length; i++)
                stops[i + 1] = pole[i];
            return SampleSequential(stops, u, 0f, 1f);
        }

        static Color32 LerpColor(Color32 a, Color32 b, float t)
        {
            if (t <= 0f) return a;
            if (t >= 1f) return b;
            return new Color32(
                (byte)(a.r + (b.r - a.r) * t),
                (byte)(a.g + (b.g - a.g) * t),
                (byte)(a.b + (b.b - a.b) * t),
                255);
        }

        static string LayerFileName(LayerKind kind)
        {
            switch (kind)
            {
                case LayerKind.Satisfaction: return "satisfaction";
                case LayerKind.Population: return "population";
                case LayerKind.Army: return "army";
                case LayerKind.Treasury: return "treasury";
                case LayerKind.TradeNode: return "tradenode";
                default: return "layer";
            }
        }

        static string LayerTitle(LayerKind kind)
        {
            switch (kind)
            {
                case LayerKind.Satisfaction: return "SATISFACTION";
                case LayerKind.Population: return "POPULATION";
                case LayerKind.Army: return "ARMY";
                case LayerKind.Treasury: return "TREASURY";
                case LayerKind.TradeNode: return "TRADE NODE";
                default: return "LAYER";
            }
        }

        static void DrawLegend(
            Color32[] pixels,
            int width,
            int height,
            LayerKind kind,
            Palettes palettes,
            FixedDomains domains,
            int tick)
        {
            const int bandH = 72;
            const int pad = 8;
            var y0 = 0;
            var y1 = bandH - 1;

            for (var y = y0; y <= y1 && y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var idx = y * width + x;
                    var src = pixels[idx];
                    // Fond semi-opaque (blend vers LegendBg)
                    pixels[idx] = new Color32(
                        (byte)((src.r * 40 + LegendBg.r * 216) / 256),
                        (byte)((src.g * 40 + LegendBg.g * 216) / 256),
                        (byte)((src.b * 40 + LegendBg.b * 216) / 256),
                        255);
                }
            }

            var title = string.Format(
                CultureInfo.InvariantCulture,
                "{0} t{1:D4}",
                LayerTitle(kind), tick);
            var titleY = bandH - pad - MapSnapshotExporter.BitmapGlyphHeight;
            MapSnapshotExporter.DrawBitmapText(pixels, title, pad, titleY, TextFg, TextHalo);

            var rampY = pad + 4;
            var rampH = 14;
            var rampX = pad;
            var rampW = Math.Min(420, width - pad * 2);

            if (kind == LayerKind.TradeNode)
            {
                DrawCategoricalLegend(pixels, palettes, pad, rampY, width, height);
                return;
            }

            if (kind == LayerKind.Treasury)
            {
                DrawDivergingRamp(
                    pixels, width, height, rampX, rampY, rampW, rampH,
                    palettes.TreasuryNeg, palettes.TreasuryMid, palettes.TreasuryPos);
                var lo = (-domains.TreasuryAbsMax).ToString("0.#", CultureInfo.InvariantCulture);
                var mid = "0";
                var hi = domains.TreasuryAbsMax.ToString("0.#", CultureInfo.InvariantCulture);
                var labelY = rampY + rampH + 4;
                MapSnapshotExporter.DrawBitmapText(pixels, lo, rampX, labelY, TextFg, TextHalo);
                var midX = rampX + rampW / 2 - MapSnapshotExporter.MeasureBitmapText(mid) / 2;
                MapSnapshotExporter.DrawBitmapText(pixels, mid, midX, labelY, TextFg, TextHalo);
                var hiX = rampX + rampW - MapSnapshotExporter.MeasureBitmapText(hi);
                MapSnapshotExporter.DrawBitmapText(pixels, hi, hiX, labelY, TextFg, TextHalo);

                // Pastille no_data
                var ndX = rampX + rampW + 16;
                FillRect(pixels, width, height, ndX, rampY, 14, rampH, palettes.NoData);
                MapSnapshotExporter.DrawBitmapText(
                    pixels, "NO DATA", ndX + 18, rampY, TextFg, TextHalo);
                return;
            }

            Color32[] steps;
            string loS, midS, hiS;
            switch (kind)
            {
                case LayerKind.Satisfaction:
                    steps = palettes.Satisfaction;
                    loS = domains.SatMin.ToString("0.##", CultureInfo.InvariantCulture);
                    midS = ((domains.SatMin + domains.SatMax) * 0.5f)
                        .ToString("0.##", CultureInfo.InvariantCulture);
                    hiS = domains.SatMax.ToString("0.##", CultureInfo.InvariantCulture);
                    break;
                case LayerKind.Population:
                    steps = palettes.Population;
                    loS = domains.PopMin.ToString("0", CultureInfo.InvariantCulture);
                    midS = ((domains.PopMin + domains.PopMax) * 0.5f)
                        .ToString("0", CultureInfo.InvariantCulture);
                    hiS = domains.PopMax.ToString("0", CultureInfo.InvariantCulture);
                    break;
                default:
                    steps = palettes.Army;
                    loS = domains.ArmyMin.ToString("0", CultureInfo.InvariantCulture);
                    midS = ((domains.ArmyMin + domains.ArmyMax) * 0.5f)
                        .ToString("0", CultureInfo.InvariantCulture);
                    hiS = domains.ArmyMax.ToString("0", CultureInfo.InvariantCulture);
                    break;
            }

            DrawSequentialRamp(pixels, width, height, rampX, rampY, rampW, rampH, steps);
            var labelY2 = rampY + rampH + 4;
            MapSnapshotExporter.DrawBitmapText(pixels, loS, rampX, labelY2, TextFg, TextHalo);
            var midX2 = rampX + rampW / 2 - MapSnapshotExporter.MeasureBitmapText(midS) / 2;
            MapSnapshotExporter.DrawBitmapText(pixels, midS, midX2, labelY2, TextFg, TextHalo);
            var hiX2 = rampX + rampW - MapSnapshotExporter.MeasureBitmapText(hiS);
            MapSnapshotExporter.DrawBitmapText(pixels, hiS, hiX2, labelY2, TextFg, TextHalo);

            if (kind == LayerKind.Satisfaction)
            {
                var ndX = rampX + rampW + 16;
                FillRect(pixels, width, height, ndX, rampY, 14, rampH, palettes.NoData);
                MapSnapshotExporter.DrawBitmapText(
                    pixels, "NO DATA", ndX + 18, rampY, TextFg, TextHalo);
            }
        }

        static void DrawCategoricalLegend(
            Color32[] pixels, Palettes palettes, int pad, int rampY, int width, int height)
        {
            var keys = new List<int>(palettes.TradeNodeById.Keys);
            keys.Sort();
            var x = pad;
            var sw = 14;
            var sh = 14;
            for (var i = 0; i < keys.Count; i++)
            {
                var node = keys[i];
                var c = palettes.TradeNodeById[node];
                FillRect(pixels, width, height, x, rampY, sw, sh, c);
                var label = node.ToString(CultureInfo.InvariantCulture);
                MapSnapshotExporter.DrawBitmapText(
                    pixels, label, x + sw + 3, rampY, TextFg, TextHalo);
                x += sw + MapSnapshotExporter.MeasureBitmapText(label) + 14;
                if (x > width - 80)
                {
                    x = pad;
                    rampY += sh + 6;
                }
            }
        }

        static void DrawSequentialRamp(
            Color32[] pixels, int width, int height, int x, int y, int w, int h, Color32[] steps)
        {
            for (var i = 0; i < w; i++)
            {
                var t = w <= 1 ? 0f : (float)i / (w - 1);
                var c = SampleSequential(steps, t, 0f, 1f);
                for (var dy = 0; dy < h; dy++)
                    SetPixel(pixels, width, height, x + i, y + dy, c);
            }
        }

        static void DrawDivergingRamp(
            Color32[] pixels, int width, int height, int x, int y, int w, int h,
            Color32[] neg, Color32 mid, Color32[] pos)
        {
            for (var i = 0; i < w; i++)
            {
                var t = w <= 1 ? 0f : (float)i / (w - 1);
                // t=0 → -1, t=0.5 → 0, t=1 → +1
                var v = t * 2f - 1f;
                var c = SampleDiverging(neg, mid, pos, v, 1f);
                for (var dy = 0; dy < h; dy++)
                    SetPixel(pixels, width, height, x + i, y + dy, c);
            }
        }

        static void FillRect(
            Color32[] pixels, int width, int height, int x, int y, int w, int h, Color32 c)
        {
            for (var dy = 0; dy < h; dy++)
            for (var dx = 0; dx < w; dx++)
                SetPixel(pixels, width, height, x + dx, y + dy, c);
        }

        static void SetPixel(Color32[] pixels, int width, int height, int x, int y, Color32 c)
        {
            if (x < 0 || y < 0 || x >= width || y >= height)
                return;
            pixels[y * width + x] = c;
        }

        static void AssembleCompare(
            string outputDir, int tick, Dictionary<int, string> politicalPaths)
        {
            const int cols = 3;
            const int rows = 2;
            const int cellW = 640;
            const int cellH = 480;
            var sheetW = cols * cellW;
            var sheetH = rows * cellH;
            var sheet = new Color32[sheetW * sheetH];
            var sea = CountryColors.Load().Sea;
            for (var i = 0; i < sheet.Length; i++)
                sheet[i] = sea;

            var names = new[]
            {
                "satisfaction", "population", "army",
                "treasury", "tradenode", "political"
            };

            for (var i = 0; i < names.Length; i++)
            {
                string path;
                if (names[i] == "political")
                {
                    if (!politicalPaths.TryGetValue(tick, out path))
                        continue;
                }
                else
                {
                    path = Path.Combine(
                        outputDir,
                        string.Format(
                            CultureInfo.InvariantCulture, "{0}_t{1:D4}.png", names[i], tick));
                }

                if (!File.Exists(path))
                    continue;

                var col = i % cols;
                var row = i / cols;
                BlitScaled(sheet, sheetW, sheetH, path, col * cellW, (rows - 1 - row) * cellH, cellW, cellH);
            }

            var outPath = Path.Combine(
                outputDir,
                string.Format(CultureInfo.InvariantCulture, "compare_t{0:D4}.png", tick));
            MapSnapshotExporter.WritePngSized(sheet, sheetW, sheetH, outPath);
            Debug.Log($"MapLayerRenderer: compare → {outPath}");
        }

        static void BlitScaled(
            Color32[] sheet, int sheetW, int sheetH,
            string path, int ox, int oy, int cellW, int cellH)
        {
            var bytes = File.ReadAllBytes(path);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(tex, bytes, markNonReadable: false))
            {
                UnityEngine.Object.DestroyImmediate(tex);
                return;
            }

            var src = tex.GetPixels32();
            var sw = tex.width;
            var sh = tex.height;
            UnityEngine.Object.DestroyImmediate(tex);

            for (var cy = 0; cy < cellH; cy++)
            {
                var sy = cy * sh / cellH;
                for (var cx = 0; cx < cellW; cx++)
                {
                    var sx = cx * sw / cellW;
                    var dst = (oy + cy) * sheetW + (ox + cx);
                    if (dst < 0 || dst >= sheet.Length)
                        continue;
                    sheet[dst] = src[sy * sw + sx];
                }
            }
        }

        public static string FormatDomainsLine(in FixedDomains d)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "DOMAINS FIXED sat=[{0:0.###}..{1:0.###}] pop=[0..{2:0}] army=[0..{3:0}] treasury=±{4:0.#}",
                d.SatMin, d.SatMax, d.PopMax, d.ArmyMax, d.TreasuryAbsMax);
        }

        public static string FormatStatsLine(in LayerStats s)
        {
            var line = string.Format(
                CultureInfo.InvariantCulture,
                "LAYERSTAT t{0:D4} {1} n={2} min={3:0.###} median={4:0.###} max={5:0.###}",
                s.Tick, s.Layer, s.SampleCount, s.Min, s.Median, s.Max);
            if (!string.IsNullOrEmpty(s.Extra))
                line += " " + s.Extra;
            return line;
        }
    }
}
