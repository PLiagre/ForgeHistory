using Unity.Entities;
using System;
using System.Collections.Generic;
using System.Text;
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;
using VictoriaGame.World;
// Presentation : List/string OK pour raster Color32 (comme CityMarkerComposer).

namespace VictoriaGame.Presentation
{
    /// <summary>
    /// Emprise rectangulaire d'une étiquette ou d'une icône sur le raster carte.
    /// </summary>
    public struct MapLabelRect
    {
        public int X0;
        public int Y0;
        public int X1;
        public int Y1;
    }

    public enum MapLabelKind : byte
    {
        Province = 0,
        Country = 1,
        City = 2,
        Marker = 3,
        Building = 4,
    }

    /// <summary>
    /// Ordre fixe de placement autour d'un marqueur (documenté, déterministe) :
    /// Below → Above → Right → Left → 4 diagonales → anneau r1 (8 dir) → anneau r2 (8 dir).
    /// </summary>
    public enum MapLabelSlot : byte
    {
        Below = 0,
        Above = 1,
        Right = 2,
        Left = 3,
        BelowRight = 4,
        BelowLeft = 5,
        AboveRight = 6,
        AboveLeft = 7,
        RingR1_N = 8,
        RingR1_NE = 9,
        RingR1_E = 10,
        RingR1_SE = 11,
        RingR1_S = 12,
        RingR1_SW = 13,
        RingR1_W = 14,
        RingR1_NW = 15,
        RingR2_N = 16,
        RingR2_NE = 17,
        RingR2_E = 18,
        RingR2_SE = 19,
        RingR2_S = 20,
        RingR2_SW = 21,
        RingR2_W = 22,
        RingR2_NW = 23,
        /// <summary>Placement par offsets (noms de province/pays).</summary>
        Offset = 24,
    }

    public struct MapPlacedLabel
    {
        public MapLabelKind Kind;
        public MapLabelRect Rect;
        public MapLabelSlot Slot;
        public int Id;
        public bool Moved;
        public int Rank;
    }

    /// <summary>
    /// Échelle d'importance unique (toutes catégories mélangées) — v1_041.
    /// Rang plus petit = plus prioritaire au Flush.
    /// </summary>
    public static class MapLabelImportance
    {
        public const int CountryName = 1;
        public const int NationalCapital = 2;
        public const int ProvinceName = 3;
        /// <summary>Capitale de province OU ville pop ≥ CountryMinLabelPopulation.</summary>
        public const int MajorCity = 4;
        public const int OtherCity = 5;
        /// <summary>
        /// Étiquette synthétique « CELL nnnn » (pilote) — sous les villes nommées (v1_076).
        /// Empêche ROUEN de tomber pendant que CELL 1264 reste.
        /// </summary>
        public const int SyntheticCellLabel = 6;

        public static bool IsProtectedRank(int rank, bool isCapitalCity) =>
            rank == CountryName ||
            rank == NationalCapital ||
            rank == ProvinceName ||
            (rank == MajorCity && isCapitalCity);

        public static int RankForCity(bool isNationalCapital, bool isProvinceCapital, int population)
        {
            if (isNationalCapital)
                return NationalCapital;
            if (isProvinceCapital || population >= MapLabelVisibility.CountryMinLabelPopulation)
                return MajorCity;
            return OtherCity;
        }

        /// <summary>true si le texte sanitizé est une étiquette cellule pilote.</summary>
        public static bool IsSyntheticCellLabel(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Length < 6)
                return false;
            if (!text.StartsWith("CELL ", StringComparison.Ordinal))
                return false;
            for (var i = 5; i < text.Length; i++)
            {
                if (text[i] < '0' || text[i] > '9')
                    return false;
            }

            return true;
        }

        public static int RankForProvinceLabel(string sanitizedText) =>
            IsSyntheticCellLabel(sanitizedText) ? SyntheticCellLabel : ProvinceName;

        /// <summary>Politique de rangs publiée (patron DocumentedPolicy).</summary>
        public static string DocumentedRankScale() =>
            "Rangs (↑ = prioritaire): " +
            "1=nom de pays ; 2=capitale NATIONALE (Capital de CapitalProvinceId) ; " +
            "3=nom de province ; 4=capitale de province + villes pop>=" +
            MapLabelVisibility.CountryMinLabelPopulation +
            " ; 5=autres villes ; 6=étiquette CELL nnnn (pilote, v1_076). " +
            "Tri Flush: rang↑, population↓, statut↓, clé domaine↑ (CityId/ProvinceId/CountryId). " +
            "Délogement v1_041 RETIRÉ (v1_076) — jamais exercé (LastDisplaced=0).";
    }

    /// <summary>
    /// Visibilité des NOMS de villes selon le niveau d'observation (v1_040).
    /// Distinct de <see cref="CityMarkerVisibility.IncludeCity"/> (marqueurs).
    /// Politique publiée via <see cref="DocumentedPolicy"/>.
    /// </summary>
    public static class MapLabelVisibility
    {
        /// <summary>
        /// Au niveau PAYS : capitales toujours nommées ; autres villes si
        /// population ≥ médiane urbaine (~89) arrondie à 100 — même logique
        /// graduée que WorldMinPopulation pour les marqueurs monde.
        /// </summary>
        public const int CountryMinLabelPopulation = 100;

        public static bool IncludeCityLabel(MapObservationLevel level, in CityData city)
        {
            if (!CityMarkerVisibility.ShowLabels(level))
                return false;
            // Captures AVANT : toutes les villes nommées, sans filtre d'importance.
            if (MapLabelLayout.LegacyCityLabels || !MapLabelLayout.CollisionEnabled)
                return true;
            if (level == MapObservationLevel.Province || level == MapObservationLevel.City)
                return true;
            if (level == MapObservationLevel.Country)
            {
                if (city.Status == CityStatus.Capital)
                    return true;
                return city.Population >= CountryMinLabelPopulation;
            }

            return false;
        }

        public static int StatusRank(CityStatus status) => status switch
        {
            CityStatus.Capital => 3,
            CityStatus.Port => 2,
            CityStatus.Episcopal => 1,
            _ => 0,
        };

        /// <summary>
        /// Comparateur déterministe : population↓, statut↓, CityId↑.
        /// Jamais Entity.Index.
        /// </summary>
        public static int CompareCityImportance(in CityData a, in CityData b)
        {
            var cmp = b.Population.CompareTo(a.Population);
            if (cmp != 0) return cmp;
            cmp = StatusRank(b.Status).CompareTo(StatusRank(a.Status));
            if (cmp != 0) return cmp;
            return a.CityId.CompareTo(b.CityId);
        }

        public static string DocumentedPolicy() =>
            "WORLD: pas de noms de villes. " +
            "COUNTRY: capitales + villes pop>=" + CountryMinLabelPopulation +
            " (médiane urbaine ~89 arrondie ; marqueurs restent toutes villes). " +
            "PROVINCE/CITY: toutes les villes. " +
            "Priorité conflit: file unique triée par MapLabelImportance (plus de " +
            "paquet provinces-avant-villes). " +
            "Slots: Below→Above→Right→Left→diag→anneau r16/r28. " +
            "Tri étiquettes: rang↑ pop↓ statut↓ clé↑.";
    }

    /// <summary>
    /// Réservation d'espace PARTAGÉE — noms de provinces ET de villes (v1_040/v1_041).
    /// Session : Begin → (Enqueue labels + Reserve immédiats) → Flush → End.
    /// Lecture seule ECS.
    /// </summary>
    public static class MapLabelLayout
    {
        public const int RingRadius1 = 16;
        public const int RingRadius2 = 28;
        public const int RingRadius3 = 44;
        public const int RingRadius4 = 64;

        static readonly int[] DefaultOffsetsX =
        {
            0, 0, 0, 14, -14, 14, -14, 28, -28, 0, 0,
            40, -40, 40, -40, 0, 0, 56, -56, 42, -42, 42, -42,
            70, -70, 0, 0, 84, -84, 60, -60, 60, -60,
        };
        static readonly int[] DefaultOffsetsY =
        {
            0, 16, -16, 0, 0, 12, -12, 0, 0, 28, -28,
            0, 0, 24, -24, 40, -40, 0, 0, 32, 32, -32, -32,
            0, 0, 56, -56, 0, 0, 48, 48, -48, -48,
        };

        // Directions anneau : N, NE, E, SE, S, SW, W, NW (déterministe).
        static readonly int[] RingDx = { 0, 1, 1, 1, 0, -1, -1, -1 };
        static readonly int[] RingDy = { -1, -1, 0, 1, 1, 1, 0, -1 };

        struct PendingLabel
        {
            public string Text;
            public int AnchorX;
            public int AnchorY;
            public int MarkerSize;
            public bool UseAnchorSlots;
            public Color32 Fg;
            public Color32 Halo;
            public MapLabelKind Kind;
            public int Id;
            public int Rank;
            public int Population;
            public int StatusRank;
            public int DomainKey;
            public bool IsProtected;
        }

        struct CommittedLabel
        {
            public PendingLabel Source;
            public MapLabelRect Rect;
            public MapLabelSlot Slot;
            public int Ox;
            public int Oy;
            public bool Moved;
            public int OccupiedIndex;
        }

        static readonly List<MapLabelRect> Occupied = new List<MapLabelRect>(256);
        static readonly List<MapPlacedLabel> Placed = new List<MapPlacedLabel>(128);
        static readonly List<PendingLabel> Pending = new List<PendingLabel>(128);
        static readonly List<CommittedLabel> Committed = new List<CommittedLabel>(128);
        static readonly List<string> OmittedNames = new List<string>(32);
        static readonly List<string> DrawnNames = new List<string>(128);
        static readonly List<int> OmittedRanks = new List<int>(32);
        static int _width;
        static int _height;
        static bool _active;

        /// <summary>
        /// false = comportement legacy (dessine sans test) pour captures AVANT.
        /// </summary>
        public static bool CollisionEnabled { get; set; } = true;

        /// <summary>
        /// true = noms de villes centrés sous le marqueur sans réservation (AVANT v1_040).
        /// Les noms de provinces gardent leur collision interne via la session.
        /// </summary>
        public static bool LegacyCityLabels { get; set; } = false;

        /// <summary>
        /// true (défaut) = file unique triée par importance (v1_041).
        /// false = placement immédiat à l'appel (comportement v1_040, captures AVANT).
        /// </summary>
        public static bool UseImportanceQueue { get; set; } = true;

        public static bool IsActive => _active;
        public static int LastDrawn { get; private set; }
        public static int LastMoved { get; private set; }
        public static int LastOmitted { get; private set; }
        /// <summary>Toujours 0 — délogement v1_041 retiré en v1_076.</summary>
        public static int LastDisplaced => 0;
        public static int LastReserved { get; private set; }
        public static int LastEnqueued { get; private set; }
        public static IReadOnlyList<MapPlacedLabel> LastPlaced => Placed;
        public static IReadOnlyList<string> LastOmittedNames => OmittedNames;
        public static IReadOnlyList<string> LastDrawnNames => DrawnNames;
        public static IReadOnlyList<int> LastOmittedRanks => OmittedRanks;

        public static string FormatOmittedNames()
        {
            if (OmittedNames.Count == 0)
                return "(aucune)";
            var sb = new StringBuilder(OmittedNames.Count * 12);
            for (var i = 0; i < OmittedNames.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(OmittedNames[i]);
                if (i < OmittedRanks.Count)
                {
                    sb.Append("[r");
                    sb.Append(OmittedRanks[i]);
                    sb.Append(']');
                }
            }

            return sb.ToString();
        }

        public static string FormatDrawnNames()
        {
            if (DrawnNames.Count == 0)
                return "(aucune)";
            var sb = new StringBuilder(DrawnNames.Count * 12);
            for (var i = 0; i < DrawnNames.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(DrawnNames[i]);
            }

            return sb.ToString();
        }

        public static string DocumentedPlacementOrder() =>
            "Slots ancre: Below→Above→Right→Left→BelowRight→BelowLeft→AboveRight→AboveLeft ; " +
            "anneau r=" + RingRadius1 + "/" + RingRadius2 + "/" + RingRadius3 + "/" + RingRadius4 +
            " (N→NE→E→SE→S→SW→W→NW). " +
            "Offsets province: centroïde puis couronne élargie (sauté si occupé).";

        public static void Begin(int width, int height)
        {
            Occupied.Clear();
            Placed.Clear();
            Pending.Clear();
            Committed.Clear();
            OmittedNames.Clear();
            DrawnNames.Clear();
            OmittedRanks.Clear();
            _width = width;
            _height = height;
            _active = true;
            LastDrawn = 0;
            LastMoved = 0;
            LastOmitted = 0;
            LastReserved = 0;
            LastEnqueued = 0;
        }

        public static void End()
        {
            _active = false;
        }

        public static void ResetStatsKeepOccupied()
        {
            LastDrawn = 0;
            LastMoved = 0;
            LastOmitted = 0;
            DrawnNames.Clear();
            OmittedNames.Clear();
            OmittedRanks.Clear();
        }

        /// <summary>Inscrit un rectangle déjà occupé (marqueur, bâtiment, …).</summary>
        public static void Reserve(int x0, int y0, int x1, int y1)
        {
            if (!_active)
                return;
            if (x1 <= x0 || y1 <= y0)
                return;
            Occupied.Add(new MapLabelRect { X0 = x0, Y0 = y0, X1 = x1, Y1 = y1 });
            LastReserved++;
        }

        public static void ReserveCentered(int cx, int cy, int size)
        {
            var half = math.max(1, size / 2);
            Reserve(cx - half, cy - half, cx + half + 1, cy + half + 1);
        }

        /// <summary>
        /// Inscrit une étiquette candidate (pas de dessin). Flush trie et place.
        /// </summary>
        public static void Enqueue(
            string text,
            int anchorX,
            int anchorY,
            int markerSize,
            Color32 fg,
            Color32 halo,
            MapLabelKind kind,
            int id,
            int rank,
            int population,
            int statusRank,
            int domainKey,
            bool useAnchorSlots,
            bool isProtected)
        {
            if (!_active || string.IsNullOrEmpty(text))
                return;
            if (!UseImportanceQueue)
                return;

            Pending.Add(new PendingLabel
            {
                Text = text,
                AnchorX = anchorX,
                AnchorY = anchorY,
                MarkerSize = markerSize,
                UseAnchorSlots = useAnchorSlots,
                Fg = fg,
                Halo = halo,
                Kind = kind,
                Id = id,
                Rank = rank,
                Population = population,
                StatusRank = statusRank,
                DomainKey = domainKey,
                IsProtected = isProtected,
            });
            LastEnqueued++;
        }

        /// <summary>
        /// Trie la file par importance, place, dessine.
        /// À appeler APRÈS overlay (marqueurs réservés), AVANT End.
        /// Délogement v1_041 retiré (v1_076) — jamais exercé.
        /// </summary>
        public static void Flush(Color32[] pixels)
        {
            if (!_active || !UseImportanceQueue)
                return;
            if (pixels == null)
            {
                Pending.Clear();
                return;
            }

            Pending.Sort(ComparePending);

            for (var i = 0; i < Pending.Count; i++)
                PlaceOne(pixels, Pending[i]);

            Pending.Clear();

            // Dessin différé : toutes les décisions sont finales.
            for (var i = 0; i < Committed.Count; i++)
            {
                var c = Committed[i];
                MapSnapshotExporter.DrawBitmapText(
                    pixels, c.Source.Text, c.Ox, c.Oy, c.Source.Fg, c.Source.Halo);
            }
        }

        static int ComparePending(PendingLabel a, PendingLabel b)
        {
            var cmp = a.Rank.CompareTo(b.Rank);
            if (cmp != 0) return cmp;
            cmp = b.Population.CompareTo(a.Population);
            if (cmp != 0) return cmp;
            cmp = b.StatusRank.CompareTo(a.StatusRank);
            if (cmp != 0) return cmp;
            return a.DomainKey.CompareTo(b.DomainKey);
        }

        static void PlaceOne(Color32[] pixels, PendingLabel pending)
        {
            if (TryFindFreeSlot(pending, out var slot, out var box, out var ox, out var oy, out var moved))
            {
                CommitDeferred(pending, box, slot, ox, oy, moved);
                return;
            }

            LastOmitted++;
            OmittedNames.Add(pending.Text);
            OmittedRanks.Add(pending.Rank);
        }

        static void CommitDeferred(
            PendingLabel pending,
            MapLabelRect box,
            MapLabelSlot slot,
            int ox,
            int oy,
            bool moved)
        {
            var occIdx = Occupied.Count;
            Occupied.Add(box);
            Committed.Add(new CommittedLabel
            {
                Source = pending,
                Rect = box,
                Slot = slot,
                Ox = ox,
                Oy = oy,
                Moved = moved,
                OccupiedIndex = occIdx,
            });
            Placed.Add(new MapPlacedLabel
            {
                Kind = pending.Kind,
                Rect = box,
                Slot = slot,
                Id = pending.Id,
                Moved = moved,
                Rank = pending.Rank,
            });
            DrawnNames.Add(pending.Text ?? "");
            LastDrawn++;
            if (moved)
                LastMoved++;
        }

        struct SlotCandidate
        {
            public MapLabelSlot Slot;
            public MapLabelRect Box;
            public int Ox;
            public int Oy;
            public bool Moved;
        }

        static List<SlotCandidate> EnumerateSlots(PendingLabel pending)
        {
            var list = new List<SlotCandidate>(32);
            var textW = MapSnapshotExporter.MeasureBitmapText(pending.Text);
            var textH = MapSnapshotExporter.BitmapGlyphHeight;

            if (pending.UseAnchorSlots)
            {
                // 8 fentes cardinales + diagonales.
                for (var s = 0; s < 8; s++)
                {
                    var slot = (MapLabelSlot)s;
                    ComputeAnchorOrigin(
                        pending.AnchorX, pending.AnchorY, pending.MarkerSize,
                        textW, textH, slot, out var ox, out var oy);
                    list.Add(new SlotCandidate
                    {
                        Slot = slot,
                        Box = MakeBox(ox, oy, textW, textH),
                        Ox = ox,
                        Oy = oy,
                        Moved = s > 0,
                    });
                }

                // Anneaux r1, r2.
                AppendRing(list, pending.AnchorX, pending.AnchorY, textW, textH,
                    RingRadius1, MapLabelSlot.RingR1_N);
                AppendRing(list, pending.AnchorX, pending.AnchorY, textW, textH,
                    RingRadius2, MapLabelSlot.RingR2_N);
                // Rayons supplémentaires (protégés / densités provinciales).
                AppendRing(list, pending.AnchorX, pending.AnchorY, textW, textH,
                    RingRadius3, MapLabelSlot.RingR2_N);
                AppendRing(list, pending.AnchorX, pending.AnchorY, textW, textH,
                    RingRadius4, MapLabelSlot.RingR2_N);
            }
            else
            {
                var n = math.min(DefaultOffsetsX.Length, DefaultOffsetsY.Length);
                for (var oi = 0; oi < n; oi++)
                {
                    var ox = pending.AnchorX - textW / 2 + DefaultOffsetsX[oi];
                    var oy = pending.AnchorY - textH / 2 + DefaultOffsetsY[oi];
                    list.Add(new SlotCandidate
                    {
                        Slot = MapLabelSlot.Offset,
                        Box = MakeBox(ox, oy, textW, textH),
                        Ox = ox,
                        Oy = oy,
                        Moved = oi > 0,
                    });
                }
            }

            return list;
        }

        static void AppendRing(
            List<SlotCandidate> list,
            int ax, int ay, int textW, int textH,
            int radius, MapLabelSlot baseSlot)
        {
            for (var d = 0; d < 8; d++)
            {
                var ox = ax + RingDx[d] * radius - textW / 2;
                var oy = ay + RingDy[d] * radius - textH / 2;
                list.Add(new SlotCandidate
                {
                    Slot = (MapLabelSlot)((byte)baseSlot + d),
                    Box = MakeBox(ox, oy, textW, textH),
                    Ox = ox,
                    Oy = oy,
                    Moved = true,
                });
            }
        }

        static bool TryFindFreeSlot(
            PendingLabel pending,
            out MapLabelSlot slot,
            out MapLabelRect box,
            out int ox,
            out int oy,
            out bool moved)
        {
            slot = MapLabelSlot.Below;
            box = default;
            ox = oy = 0;
            moved = false;
            var candidates = EnumerateSlots(pending);
            for (var i = 0; i < candidates.Count; i++)
            {
                var c = candidates[i];
                if (!IsOnCanvas(c.Box))
                    continue;
                if (CollisionEnabled && Collides(c.Box))
                    continue;
                slot = c.Slot;
                box = c.Box;
                ox = c.Ox;
                oy = c.Oy;
                moved = c.Moved;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Tente de placer une étiquette autour d'un ancre (marqueur ville) — chemin immédiat (v1_040).
        /// Slots élargis v1_041 : Below→Above→Right→Left→diag→anneaux.
        /// </summary>
        public static bool TryPlaceAroundAnchor(
            Color32[] pixels,
            string text,
            int anchorX,
            int anchorY,
            int markerSize,
            Color32 fg,
            Color32 halo,
            MapLabelKind kind,
            int id,
            out MapLabelSlot slotUsed,
            out MapLabelRect box)
        {
            slotUsed = MapLabelSlot.Below;
            box = default;
            if (string.IsNullOrEmpty(text) || pixels == null)
            {
                LastOmitted++;
                OmittedNames.Add(text ?? "");
                OmittedRanks.Add(MapLabelImportance.OtherCity);
                return false;
            }

            var pending = new PendingLabel
            {
                Text = text,
                AnchorX = anchorX,
                AnchorY = anchorY,
                MarkerSize = markerSize,
                UseAnchorSlots = true,
                Fg = fg,
                Halo = halo,
                Kind = kind,
                Id = id,
                Rank = MapLabelImportance.OtherCity,
            };

            // Chemin immédiat v1_040 : 4 fentes seulement (pas d'anneau).
            if (!TryFindFreeSlotLegacy4(pending, out slotUsed, out box, out var ox, out var oy, out var moved))
            {
                LastOmitted++;
                OmittedNames.Add(text);
                OmittedRanks.Add(pending.Rank);
                return false;
            }

            MapSnapshotExporter.DrawBitmapText(pixels, text, ox, oy, fg, halo);
            CommitImmediate(box, kind, id, slotUsed, moved, pending.Rank, text);
            return true;
        }

        static bool TryFindFreeSlotLegacy4(
            PendingLabel pending,
            out MapLabelSlot slot,
            out MapLabelRect box,
            out int ox,
            out int oy,
            out bool moved)
        {
            slot = MapLabelSlot.Below;
            box = default;
            ox = oy = 0;
            moved = false;
            var textW = MapSnapshotExporter.MeasureBitmapText(pending.Text);
            var textH = MapSnapshotExporter.BitmapGlyphHeight;
            for (var s = 0; s < 4; s++)
            {
                var trySlot = (MapLabelSlot)s;
                ComputeAnchorOrigin(
                    pending.AnchorX, pending.AnchorY, pending.MarkerSize,
                    textW, textH, trySlot, out var tox, out var toy);
                var candidate = MakeBox(tox, toy, textW, textH);
                if (!IsOnCanvas(candidate))
                    continue;
                if (CollisionEnabled && Collides(candidate))
                    continue;
                slot = trySlot;
                box = candidate;
                ox = tox;
                oy = toy;
                moved = s > 0;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Placement province/pays immédiat (v1_040) : offsets pixel fixes.
        /// </summary>
        public static bool TryPlaceWithOffsets(
            Color32[] pixels,
            string text,
            int centerX,
            int centerY,
            int[] offsetsX,
            int[] offsetsY,
            Color32 fg,
            Color32 halo,
            MapLabelKind kind,
            int id,
            out MapLabelRect box)
        {
            box = default;
            if (string.IsNullOrEmpty(text) || pixels == null ||
                offsetsX == null || offsetsY == null)
            {
                LastOmitted++;
                OmittedNames.Add(text ?? "");
                OmittedRanks.Add(MapLabelImportance.ProvinceName);
                return false;
            }

            var textW = MapSnapshotExporter.MeasureBitmapText(text);
            var textH = MapSnapshotExporter.BitmapGlyphHeight;
            var n = math.min(offsetsX.Length, offsetsY.Length);
            var rank = kind == MapLabelKind.Country
                ? MapLabelImportance.CountryName
                : MapLabelImportance.RankForProvinceLabel(text);

            for (var oi = 0; oi < n; oi++)
            {
                var ox = centerX - textW / 2 + offsetsX[oi];
                var oy = centerY - textH / 2 + offsetsY[oi];
                var candidate = MakeBox(ox, oy, textW, textH);
                if (!IsOnCanvas(candidate))
                    continue;
                if (CollisionEnabled && Collides(candidate))
                    continue;

                MapSnapshotExporter.DrawBitmapText(pixels, text, ox, oy, fg, halo);
                CommitImmediate(candidate, kind, id, MapLabelSlot.Offset, oi > 0, rank, text);
                box = candidate;
                return true;
            }

            LastOmitted++;
            OmittedNames.Add(text);
            OmittedRanks.Add(rank);
            return false;
        }

        /// <summary>Zéro chevauchement entre étiquettes textuelles placées (garde-fou test).</summary>
        public static int CountTextOverlaps()
        {
            var overlaps = 0;
            for (var i = 0; i < Placed.Count; i++)
            {
                if (Placed[i].Kind == MapLabelKind.Marker ||
                    Placed[i].Kind == MapLabelKind.Building)
                    continue;
                for (var j = i + 1; j < Placed.Count; j++)
                {
                    if (Placed[j].Kind == MapLabelKind.Marker ||
                        Placed[j].Kind == MapLabelKind.Building)
                        continue;
                    if (MapLabelLayoutBurst.Overlaps(Placed[i].Rect, Placed[j].Rect))
                        overlaps++;
                }
            }

            return overlaps;
        }

        /// <summary>
        /// Chevauchements recalculés à l'échelle de DESSIN (ActiveGlyphScale), depuis
        /// les origines commités. Si la mesure ment (override ≠ dessin), ce compteur monte
        /// alors que <see cref="CountTextOverlaps"/> peut rester à 0 (V1073-B).
        /// </summary>
        public static int CountTextOverlapsAtDrawScale()
        {
            if (Committed.Count == 0)
                return CountTextOverlaps();

            var boxes = new List<MapLabelRect>(Committed.Count);
            var prevOverride = MapSnapshotExporter.DebugMeasureScaleOverride;
            MapSnapshotExporter.DebugMeasureScaleOverride = null;
            try
            {
                for (var i = 0; i < Committed.Count; i++)
                {
                    var c = Committed[i];
                    if (c.Source.Kind == MapLabelKind.Marker ||
                        c.Source.Kind == MapLabelKind.Building)
                        continue;
                    var tw = MapSnapshotExporter.MeasureBitmapText(c.Source.Text);
                    var th = MapSnapshotExporter.BitmapGlyphHeight;
                    boxes.Add(MakeBox(c.Ox, c.Oy, tw, th));
                }
            }
            finally
            {
                MapSnapshotExporter.DebugMeasureScaleOverride = prevOverride;
            }

            var overlaps = 0;
            for (var i = 0; i < boxes.Count; i++)
            {
                for (var j = i + 1; j < boxes.Count; j++)
                {
                    if (MapLabelLayoutBurst.Overlaps(boxes[i], boxes[j]))
                        overlaps++;
                }
            }

            return overlaps;
        }

        static void CommitImmediate(
            MapLabelRect box, MapLabelKind kind, int id, MapLabelSlot slot, bool moved, int rank,
            string text)
        {
            Occupied.Add(box);
            Placed.Add(new MapPlacedLabel
            {
                Kind = kind,
                Rect = box,
                Slot = slot,
                Id = id,
                Moved = moved,
                Rank = rank,
            });
            DrawnNames.Add(text ?? "");
            LastDrawn++;
            if (moved)
                LastMoved++;
        }

        static MapLabelRect MakeBox(int ox, int oy, int textW, int textH) =>
            new MapLabelRect
            {
                X0 = ox - 1,
                Y0 = oy - 1,
                X1 = ox + textW + 1,
                Y1 = oy + textH + 1,
            };

        static bool IsOnCanvas(in MapLabelRect box) =>
            box.X1 >= 0 && box.Y1 >= 0 && box.X0 < _width && box.Y0 < _height;

        static bool Collides(in MapLabelRect box)
        {
            for (var i = 0; i < Occupied.Count; i++)
            {
                if (MapLabelLayoutBurst.Overlaps(box, Occupied[i]))
                    return true;
            }

            return false;
        }

        public static void ComputeAnchorOrigin(
            int ax, int ay, int markerSize, int textW, int textH, MapLabelSlot slot,
            out int ox, out int oy)
        {
            var half = markerSize / 2;
            switch (slot)
            {
                case MapLabelSlot.Above:
                    ox = ax - textW / 2;
                    oy = ay - half - textH - 2;
                    break;
                case MapLabelSlot.Right:
                    ox = ax + half + 2;
                    oy = ay - textH / 2;
                    break;
                case MapLabelSlot.Left:
                    ox = ax - half - textW - 2;
                    oy = ay - textH / 2;
                    break;
                case MapLabelSlot.BelowRight:
                    ox = ax + half + 2;
                    oy = ay + half + 2;
                    break;
                case MapLabelSlot.BelowLeft:
                    ox = ax - half - textW - 2;
                    oy = ay + half + 2;
                    break;
                case MapLabelSlot.AboveRight:
                    ox = ax + half + 2;
                    oy = ay - half - textH - 2;
                    break;
                case MapLabelSlot.AboveLeft:
                    ox = ax - half - textW - 2;
                    oy = ay - half - textH - 2;
                    break;
                default: // Below
                    ox = ax - textW / 2;
                    oy = ay + half + 2;
                    break;
            }
        }
    }

    /// <summary>Noyau Burst : test de chevauchement AABB (déterministe).</summary>
    [BurstCompile]
    public static class MapLabelLayoutBurst
    {
        [BurstCompile]
        public static bool Overlaps(in MapLabelRect a, in MapLabelRect b) =>
            a.X0 < b.X1 && a.X1 > b.X0 && a.Y0 < b.Y1 && a.Y1 > b.Y0;
    }

    /// <summary>
    /// Système DOTS de la couche layout (v1_040/v1_041). Le placement réel tourne pendant
    /// la composition raster (Color32 managé) ; ce système ancre la couche dans
    /// PresentationSystemGroup sans écrire l'ECS.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(MapDisplaySystem))]
    public partial struct MapLabelLayoutSystem : ISystem
    {
        public void OnCreate(ref SystemState state) { }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // no-op : réservation invoquée depuis MapSnapshotExporter / CityMarkerComposer.
        }

        public void OnDestroy(ref SystemState state) { }
    }
}
