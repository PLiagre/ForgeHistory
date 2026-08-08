using Unity.Entities;
using Unity.Collections;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using VictoriaGame.Core;
using VictoriaGame.Military;
using VictoriaGame.Utils;
using VictoriaGame.World;

namespace VictoriaGame.Presentation
{
    /// <summary>
    /// Observateur hors-jeu : EntityManager (lecture seule) → PNG carte politique.
    /// Aucun ISystem / SystemBase — appelé depuis un harnais de test.
    /// Masque terre/mer (v1_028) = enveloppes concaves (alpha-shape) dérivées des
    /// coordonnées + adjacences terrestres, chenaux de détroits, fermeture de poches.
    /// Le mode LegacyDisks conserve l'ancien pipeline disques→corridors→triangles pour
    /// les mesures AVANT (PARTIE 1). FillVoronoi : nord@py0 (wy=minY+v×range, v1_085).
    /// </summary>
    public static class MapSnapshotExporter
    {
        public const int Width = 1600;
        public const int Height = 1200;

        /// <summary>Résolution active du raster (pleine ou chronique). Toujours = Width/Height sauf pendant BuildMapGeometry.</summary>
        static int ActiveW = Width;
        static int ActiveH = Height;

        /// <summary>
        /// Mode de construction du masque terre/mer.
        /// DataDerived = littoral dérivé des données (défaut v1_028).
        /// LegacyDisks = union de disques CellRadius (référence PARTIE 1).
        /// </summary>
        public enum LandMaskMode
        {
            DataDerived = 0,
            LegacyDisks = 1
        }

        /// <summary>
        /// Rayon de disque provincial en unités projetées (≈ degrés × cos mid_lat).
        /// Conservé pour le mode LegacyDisks et les graines hexagonales d'îles isolées.
        /// 1.20 ≈ 133 km — corps des provinces / îles (inchangé vs v1_001).
        /// </summary>
        public const float CellRadius = 1.20f;

        /// <summary>
        /// Multiplicateur du seuil alpha (Delaunay) :
        /// maxEdge = median(longueurs d'arêtes terrestres de la composante) × ce facteur.
        /// Les triangles Delaunay plus longs sont rejetés → golfes / mers intérieures préservés.
        /// </summary>
        public const float ConcaveHullDigFactor = 1.35f;

        /// <summary>
        /// Demi-largeur des corridors additifs le long des arêtes terrestres.
        /// 0.75 ≈ moitié d'un rayon : isthme crédible, pas un fil.
        /// </summary>
        public const float CorridorHalfWidth = 0.75f;

        /// <summary>
        /// Demi-largeur des chenaux soustractifs le long des détroits.
        /// 1.15 ≥ demi-largeur de la lentille Bosphore (R=1.20, d≈0.80 → √(R²-(d/2)²)≈1.13).
        /// Un canal trop étroit (0.25) laisse l'Asie mineure soudée au continent.
        /// </summary>
        public const float StraitHalfWidth = 1.15f;

        /// <summary>
        /// Fraction d'extrémité non creusée sur chaque détroit (t ∈ [inset, 1−inset]).
        /// Évite de vider le disque des provinces-extrémités tout en tranchant le pont médian.
        /// </summary>
        const float StraitEndInset = 0.20f;

        const float MarginFraction = 0.05f;
        const int GlyphW = 5;
        const int GlyphH = 7;
        /// <summary>Échelle neutre (v1_073) — bit-identique au const 2 d'avant ce brief.</summary>
        public const int NeutralGlyphScale = 2;
        public const int ZoomGlyphScaleWorld = 2;
        public const int ZoomGlyphScaleCountry = 3;
        public const int ZoomGlyphScaleProvince = 5;

        /// <summary>
        /// false (défaut compilé) = échelle 2 partout (captures historiques bit-identiques).
        /// true = échelle dérivée du niveau d'observation (2/3/5).
        /// </summary>
        public static bool ZoomScaleEnabled { get; set; }

        /// <summary>
        /// Mutation rouge V1079-A : rejoue la pré-inversion locale des glyphes
        /// (défaut d'avant v1_079). false = convention unique nord@py0.
        /// </summary>
        public static bool DebugPreInvertGlyphs { get; set; }

        /// <summary>
        /// Mutation rouge v1_085 : rejoue py=(maxY−wy) (miroir N-S sur buffer nord@py0).
        /// false = py=(wy−minY) nord@py0 avec y=−lat.
        /// </summary>
        public static bool DebugLegacyMirrorWorldToPixelY { get; set; }

        /// <summary>
        /// v1_092 — rendu des provinces de front (FrontLineState). true = dessine ;
        /// false = no-op bit-identique au rendu d'avant ce brief (réversible).
        /// </summary>
        public static bool FrontOverlayEnabled { get; set; } = true;

        /// <summary>
        /// v1_093 — preuve rouge seulement. true = annoter comme v1_092 (sector.IsActive
        /// seul, y compris guerres terminées). false = filtre WarData.IsActive
        /// (motif FrontAdvanceSystem.cs:96). Jamais activé hors mesures.
        /// </summary>
        public static bool DebugAnnotateInactiveWarFronts { get; set; }

        /// <summary>
        /// v1_094 — preuve rouge seulement. true = repeindre la carte pilote depuis
        /// <c>ownership_1400.json</c> comme avant ce brief (le monde joué est ignoré,
        /// donc une conquête n'a AUCUN effet visible). false = lire l'ECS.
        /// Jamais activé hors mesures.
        /// </summary>
        public static bool DebugPilotColorsFromDisk { get; set; }

        /// <summary>
        /// v1_093 — épaisseur du liseré front non contesté en pixels.
        /// 1 = comportement v1_092 ; 2 = lisible (+ halo sombre à distance == épaisseur).
        /// </summary>
        public static int FrontRimThicknessPx { get; set; } = 2;

        /// <summary>
        /// Liseré front non contesté — distinct du hachurage occupation, mais SECONDAIRE
        /// sur une carte politique (brief 005-refonte-visuelle-carte, Success Condition 5).
        /// Était (210,36,36) : rouge pleinement saturé, la couleur la plus criarde de toute
        /// la carte politique (griefs indépendants propriétaire + reporté à ce brief) — le
        /// fait de simulation reste affiché (le liseré n'est pas supprimé, voir
        /// <see cref="ApplyFrontOverlay"/> inchangée) mais sa saturation est réduite d'un
        /// tiers environ (S≈83% → S≈60%) en restant dans la même famille de teinte rouge,
        /// pour se lire comme un marquage cartographique secondaire — toujours net et
        /// distinguable des liserés politiques (quasi noir/gris, cf. <see
        /// cref="PoliticalBorder"/>/<see cref="InternalBorder"/>) et du damier front
        /// contesté (jaune/brun, cf. <see cref="FrontContestedLight"/>/<see
        /// cref="FrontContestedDark"/>), jamais confondu avec l'un ou l'autre.
        /// </summary>
        static readonly Color32 FrontRimColor = new Color32(150, 60, 60, 255);

        /// <summary>Halo sombre autour du liseré (v1_093 — lisibilité) — assoupli à l'identique.</summary>
        static readonly Color32 FrontRimHalo = new Color32(70, 26, 26, 255);

        /// <summary>Damier front contesté — case claire.</summary>
        static readonly Color32 FrontContestedLight = new Color32(255, 214, 64, 255);

        /// <summary>Damier front contesté — case sombre (assombrissement local).</summary>
        static readonly Color32 FrontContestedDark = new Color32(120, 48, 12, 255);

        /// <summary>Provinces touchées par le dernier ApplyFrontOverlay (diagnostic v1_092).</summary>
        public static readonly List<int> LastFrontDrawnProvinceIds = new List<int>(32);

        /// <summary>Pixels peints par le dernier ApplyFrontOverlay (diagnostic v1_093).</summary>
        public static int LastFrontPixelCount { get; private set; }

        /// <summary>Échelle active pour mesure ET dessin — jamais diverger.</summary>
        static int ActiveGlyphScale = NeutralGlyphScale;

        /// <summary>
        /// Surcharge de mesure seule (preuve V1073-B). null = même échelle que le dessin.
        /// </summary>
        public static int? DebugMeasureScaleOverride;

        static readonly List<string> SanitizeUnmapped = new List<string>(8);
        public static IReadOnlyList<string> LastSanitizeUnmapped => SanitizeUnmapped;

        public static int CurrentGlyphScale => ActiveGlyphScale;

        public static int GlyphScaleFor(MapObservationLevel level)
        {
            if (!ZoomScaleEnabled)
                return NeutralGlyphScale;
            switch (level)
            {
                case MapObservationLevel.Country:
                    return ZoomGlyphScaleCountry;
                case MapObservationLevel.Province:
                case MapObservationLevel.City:
                case MapObservationLevel.District:
                    return ZoomGlyphScaleProvince;
                default:
                    return ZoomGlyphScaleWorld;
            }
        }

        public static void SetGlyphScale(int scale) =>
            ActiveGlyphScale = scale < 1 ? NeutralGlyphScale : scale;

        public static void ApplyGlyphScaleForCurrentView()
        {
            var level = MapViewport.IsInitialized
                ? MapViewport.State.Level
                : MapObservationLevel.World;
            ActiveGlyphScale = GlyphScaleFor(level);
        }

        public static void ResetZoomScaleToNeutral()
        {
            ZoomScaleEnabled = false;
            ActiveGlyphScale = NeutralGlyphScale;
            DebugMeasureScaleOverride = null;
        }

        /// <summary>Exécute une action sous une échelle de glyphe donnée (restaurée après).</summary>
        public static void WithGlyphScale(int scale, Action action)
        {
            if (action == null)
                return;
            var prev = ActiveGlyphScale;
            ActiveGlyphScale = scale < 1 ? NeutralGlyphScale : scale;
            try
            {
                action();
            }
            finally
            {
                ActiveGlyphScale = prev;
            }
        }

        static int EffectiveMeasureScale =>
            DebugMeasureScaleOverride ?? ActiveGlyphScale;

        /// <summary>Frontière de PAYS — contraste fort (v1_019).</summary>
        static readonly Color32 PoliticalBorder = new Color32(0x0c, 0x0e, 0x12, 255);
        /// <summary>Frontière interne — discrète, provinces visibles sans dominer.</summary>
        static readonly Color32 InternalBorder = new Color32(0x52, 0x5a, 0x62, 255);
        /// <summary>Littoral terre↔mer.</summary>
        static readonly Color32 CoastLine = new Color32(0x0a, 0x22, 0x30, 255);
        static readonly Color32 GraphLandEdge = new Color32(0xe8, 0xe8, 0xe8, 255);
        static readonly Color32 GraphStraitEdge = new Color32(0x7a, 0xc4, 0xd4, 255);
        static readonly Color32 GraphNode = new Color32(0xff, 0xcc, 0x33, 255);
        static readonly Color32 LabelLight = new Color32(0xf5, 0xf5, 0xf5, 255);
        static readonly Color32 LabelDark = new Color32(0x10, 0x10, 0x10, 255);

        /// <summary>Dilation politique (rayon px) — pays nettement plus marqués que l'interne.</summary>
        const int PoliticalBorderRadius = 2;

        /// <summary>Dernier rapport de masses terrestres produit par Export.</summary>
        public static LandMassReport LastLandMassReport;

        /// <summary>Stats du dernier BuildLandMask (pixels triangles / poches / hull).</summary>
        public static MaskBuildStats LastMaskStats;

        /// <summary>Dernière mesure de cohérence forme ↔ données (côte / adjacence / détroits).</summary>
        public static ShapeCoherenceReport LastShapeReport;

        /// <summary>Labels pays placés / omis au dernier ApplyLabels (déterministe).</summary>
        public static int LastLabelsPlaced;
        public static int LastLabelsOmitted;

        public struct LandMassReport
        {
            public int ComponentCount;
            /// <summary>Tailles en provinces, triées décroissant.</summary>
            public int[] ProvinceCounts;
            public bool MatchesTarget;
            public string Summary;
        }

        public struct MaskBuildStats
        {
            public int TriangleCount;
            public int TrianglePixelsAdded;
            public int PocketPixelsAdded;
            public int HullPixelsAdded;
            public int HexSeedCount;
            public int ComponentCount;
            /// <summary>Coût wall-clock du masque (ms) — outillage, n'entre pas dans le déterminisme.</summary>
            public double BuildMilliseconds;
            public LandMaskMode Mode;
        }

        /// <summary>
        /// Trois mesures de la PARTIE 1 / 3 : côte vs is_coastal, frontières terrestres, détroits.
        /// </summary>
        public struct ShapeCoherenceReport
        {
            public int CoastalDeclared;
            public int InlandDeclared;
            public int CoastalTouchingSea;
            public int InlandTouchingSea;
            public int LandAdjacencyPairs;
            public int LandAdjacencySharingBorder;
            public int StraitPairs;
            public int StraitPairsGlued;
            public string[] CoastalMissingSea;
            public string[] InlandFalseSea;
            public string[] LandAdjMissingBorder;
            public string[] StraitGluedNames;
            public string Summary;
        }

        /// <summary>
        /// Densité de texte selon le niveau d'observation (v1_029).
        /// Placement toujours déterministe.
        /// </summary>
        public enum LabelDensity : byte
        {
            /// <summary>Un label par PAYS (niveau monde) — acquis v1_019.</summary>
            Countries = 0,
            /// <summary>Noms de provinces (niveau pays).</summary>
            Provinces = 1,
            /// <summary>Province sélectionnée uniquement (niveau province).</summary>
            SelectedProvince = 2,
            None = 3
        }

        /// <summary>
        /// Géométrie immuable (masque + Voronoï) calculée une fois, réutilisable pour N frames.
        /// ProvinceAt[i] = index dans ViewsSkeleton (trié par Id), -1 = mer.
        /// </summary>
        public sealed class MapGeometry
        {
            public int Width;
            public int Height;
            public float MinX, MaxX, MinY, MaxY;
            public bool[] IsLand;
            public int[] ProvinceAt;
            public List<ProvinceView> ViewsSkeleton;
            public LandMassReport LandMasses;
            public MaskBuildStats MaskStats;
            /// <summary>True si les bounds viennent d'une fenêtre (zoom), pas du monde entier.</summary>
            public bool IsWindowed;
        }

        public struct ProvinceView
        {
            public int Id;
            public float X;
            public float Y;
            public Entity Owner;
            public Entity Controller;
            public Color32 Fill;
            public Color32 ControllerColor;
            public bool Occupied;
            public string OwnerTag;
            /// <summary>Nom d'affichage du pays (CountryData.Name ou country_colors.json).</summary>
            public string OwnerName;
            /// <summary>Nom de province (province_coordinates.json) — présentation seule.</summary>
            public string ProvinceName;
            /// <summary>v1_092 — province présente dans un FrontLineState actif.</summary>
            public bool IsFront;
            /// <summary>v1_092 — FrontLineState.IsContested.</summary>
            public bool IsContested;
            /// <summary>v1_092 — pression attaquante (lecture seule).</summary>
            public float AttackerPressure;
            /// <summary>v1_092 — pression défensive (lecture seule).</summary>
            public float DefenderPressure;
        }

        /// <summary>
        /// Construit le masque + Voronoï une seule fois à la résolution demandée.
        /// </summary>
        public static MapGeometry BuildMapGeometry(int width, int height)
            => BuildMapGeometry(width, height, LandMaskMode.DataDerived, null);

        /// <summary>
        /// Construit le masque + Voronoï. <paramref name="maskMode"/> = LegacyDisks pour
        /// la référence PARTIE 1 (union de disques) ; DataDerived pour le littoral v1_028.
        /// </summary>
        public static MapGeometry BuildMapGeometry(int width, int height, LandMaskMode maskMode)
            => BuildMapGeometry(width, height, maskMode, null);

        /// <summary>
        /// Re-rend une RÉGION (fenêtre projetée) à pleine résolution Width×Height.
        /// Ne réécrit pas le pipeline : même masque / Voronoï, bounds remplacés.
        /// </summary>
        public static MapGeometry BuildMapGeometry(int width, int height, MapWindow window)
            => BuildMapGeometry(width, height, LandMaskMode.DataDerived, window);

        /// <summary>
        /// Construit le masque + Voronoï. Si <paramref name="window"/> est non-null,
        /// les bounds de projection sont ceux de la fenêtre (zoom net).
        /// </summary>
        public static MapGeometry BuildMapGeometry(
            int width, int height, LandMaskMode maskMode, MapWindow? window)
        {
            // v1_068 — source alternative (carte pilote). Drapeau OFF ⇒ chemin legacy.
            if (PilotMapProvider.Enabled)
                return PilotMapProvider.BuildMapGeometry(width, height, window);

            var prevW = ActiveW;
            var prevH = ActiveH;
            ActiveW = width;
            ActiveH = height;
            try
            {
                var points = ProvinceCoordinates.LoadProjected(out _);
                if (points.Count == 0)
                {
                    Debug.LogWarning("MapSnapshotExporter.BuildMapGeometry: aucune coordonnée.");
                    return null;
                }

                var skeleton = new List<ProvinceView>(points.Count);
                for (var i = 0; i < points.Count; i++)
                {
                    var p = points[i];
                    skeleton.Add(new ProvinceView
                    {
                        Id = p.Id,
                        X = p.X,
                        Y = p.Y,
                        ProvinceName = p.Name
                    });
                }

                skeleton.Sort((a, b) => a.Id.CompareTo(b.Id));
                float minX, maxX, minY, maxY;
                var windowed = false;
                if (window.HasValue)
                {
                    minX = window.Value.MinX;
                    maxX = window.Value.MaxX;
                    minY = window.Value.MinY;
                    maxY = window.Value.MaxY;
                    windowed = true;
                }
                else
                {
                    ComputeBounds(skeleton, out minX, out maxX, out minY, out maxY);
                }

                var provinceAt = new int[ActiveW * ActiveH];
                for (var i = 0; i < provinceAt.Length; i++)
                    provinceAt[i] = -1;

                var isLand = maskMode == LandMaskMode.LegacyDisks
                    ? BuildLandMaskLegacyDisks(skeleton, minX, maxX, minY, maxY)
                    : BuildLandMask(skeleton, minX, maxX, minY, maxY);
                var sea = CountryColors.Load().Sea;
                var pixels = new Color32[ActiveW * ActiveH];
                FillVoronoi(skeleton, pixels, provinceAt, isLand, sea, minX, maxX, minY, maxY);
                var landReport = AnalyzeLandMasses(skeleton, isLand, provinceAt);
                LastLandMassReport = landReport;
                LastShapeReport = MeasureShapeCoherence(skeleton, isLand, provinceAt);

                return new MapGeometry
                {
                    Width = width,
                    Height = height,
                    MinX = minX,
                    MaxX = maxX,
                    MinY = minY,
                    MaxY = maxY,
                    IsLand = isLand,
                    ProvinceAt = provinceAt,
                    ViewsSkeleton = skeleton,
                    LandMasses = landReport,
                    MaskStats = LastMaskStats,
                    IsWindowed = windowed
                };
            }
            finally
            {
                ActiveW = prevW;
                ActiveH = prevH;
            }
        }

        /// <summary>
        /// Point d'entrée : lit le monde en ReadOnly, écrit un PNG à outputPath (pleine résolution + étiquettes).
        /// </summary>
        public static void Export(EntityManager em, int tick, string outputPath)
        {
            var geo = BuildMapGeometry(Width, Height);
            if (geo == null)
            {
                Debug.LogWarning($"MapSnapshotExporter: aucune province projetée à tick={tick}.");
                return;
            }

            ExportWithGeometry(em, tick, outputPath, geo, drawLabels: true, tickCartouche: null);
        }

        /// <summary>
        /// Rendu politique réutilisant un cache géométrique (O(pixels), zéro recalcul de masque).
        /// Lit l'EntityManager au tick courant (chronique / export ponctuel).
        /// </summary>
        public static void ExportWithGeometry(
            EntityManager em,
            int tick,
            string outputPath,
            MapGeometry geo,
            bool drawLabels,
            string tickCartouche)
        {
            if (geo == null)
                return;

            var colors = CountryColors.Load();
            var views = BuildViewsAligned(em, geo.ViewsSkeleton, colors);
            ExportWithGeometryFromViews(
                views, tick, outputPath, geo, drawLabels, tickCartouche, colors);
        }

        /// <summary>
        /// Rendu politique in-game : pixels seulement, sans écriture PNG.
        /// <paramref name="overlay"/> est appelé avec ActiveW/ActiveH déjà positionnés
        /// (ex. panneau d'info bitmap). Lecture seule de l'EntityManager.
        /// </summary>
        public static Color32[] RenderPoliticalPixels(
            EntityManager em,
            MapGeometry geo,
            bool drawLabels,
            System.Action<Color32[]> overlay = null)
            => RenderPoliticalPixels(
                em, geo, drawLabels ? LabelDensity.Countries : LabelDensity.None,
                selectedProvinceId: -1, overlay);

        public static Color32[] RenderPoliticalPixels(
            EntityManager em,
            MapGeometry geo,
            LabelDensity labels,
            int selectedProvinceId,
            System.Action<Color32[]> overlay = null)
        {
            if (geo == null)
                return null;

            var colors = CountryColors.Load();
            var views = BuildViewsAligned(em, geo.ViewsSkeleton, colors);
            return BuildPoliticalPixels(
                views, geo, labels, selectedProvinceId, overlay, colors);
        }

        /// <summary>
        /// Rendu politique depuis des vues déjà résolues (tags capturés au tick).
        /// N'accède PAS à l'EntityManager — règle v1_006.
        /// </summary>
        public static Color32[] ExportWithGeometryFromViews(
            List<ProvinceView> views,
            int tick,
            string outputPath,
            MapGeometry geo,
            bool drawLabels,
            string tickCartouche,
            CountryColors.Table colors = null)
            => ExportWithGeometryFromViews(
                views, tick, outputPath, geo,
                drawLabels ? LabelDensity.Countries : LabelDensity.None,
                selectedProvinceId: -1, tickCartouche, colors);

        public static Color32[] ExportWithGeometryFromViews(
            List<ProvinceView> views,
            int tick,
            string outputPath,
            MapGeometry geo,
            LabelDensity labels,
            int selectedProvinceId,
            string tickCartouche,
            CountryColors.Table colors = null)
        {
            if (geo == null || views == null || views.Count == 0)
            {
                Debug.LogWarning($"MapSnapshotExporter: aucune province projetée à tick={tick}.");
                return null;
            }

            if (colors == null)
                colors = CountryColors.Load();

            var pixels = BuildPoliticalPixels(
                views, geo, labels, selectedProvinceId,
                string.IsNullOrEmpty(tickCartouche)
                    ? null
                    : (Action<Color32[]>)(p => ApplyTickCartouche(p, tickCartouche)),
                colors);
            if (pixels == null)
                return null;

            var prevW = ActiveW;
            var prevH = ActiveH;
            ActiveW = geo.Width;
            ActiveH = geo.Height;
            try
            {
                WriteMapBufferPng(pixels, geo.Width, geo.Height, outputPath);
            }
            finally
            {
                ActiveW = prevW;
                ActiveH = prevH;
            }

            Debug.Log(
                $"MapSnapshotExporter: tick={tick} → {outputPath} " +
                $"triangles+={LastMaskStats.TrianglePixelsAdded} " +
                $"pockets+={LastMaskStats.PocketPixelsAdded} " +
                $"(CELL_RADIUS={CellRadius} CORRIDOR_HALF_WIDTH={CorridorHalfWidth} " +
                $"STRAIT_HALF_WIDTH={StraitHalfWidth}) {LastLandMassReport.Summary}");
            return pixels;
        }

        static Color32[] BuildPoliticalPixels(
            List<ProvinceView> views,
            MapGeometry geo,
            bool drawLabels,
            Action<Color32[]> overlay,
            CountryColors.Table colors)
            => BuildPoliticalPixels(
                views, geo,
                drawLabels ? LabelDensity.Countries : LabelDensity.None,
                selectedProvinceId: -1, overlay, colors);

        static Color32[] BuildPoliticalPixels(
            List<ProvinceView> views,
            MapGeometry geo,
            LabelDensity labels,
            int selectedProvinceId,
            Action<Color32[]> overlay,
            CountryColors.Table colors)
        {
            if (geo == null || views == null || views.Count == 0)
                return null;

            var prevW = ActiveW;
            var prevH = ActiveH;
            var prevScale = ActiveGlyphScale;
            ActiveW = geo.Width;
            ActiveH = geo.Height;
            ApplyGlyphScaleForCurrentView();
            try
            {
                var pixels = new Color32[ActiveW * ActiveH];
                ColorFromProvinceAt(views, pixels, geo.ProvinceAt, geo.IsLand, colors.Sea);
                if (PilotMapProvider.Enabled)
                {
                    var lod = PilotMapProvider.LodForObservation(
                        MapViewport.IsInitialized
                            ? MapViewport.State.Level
                            : MapObservationLevel.World);
                    PilotMapProvider.ApplyHillshadeOnLand(
                        pixels, geo.IsLand, ActiveW, ActiveH,
                        geo.MinX, geo.MaxX, geo.MinY, geo.MaxY,
                        colors.Sea, lod);
                }

                ApplyCoastAndRelief(pixels, geo.IsLand);
                LastLandMassReport = geo.LandMasses;
                LastMaskStats = geo.MaskStats;
                if (PilotMapProvider.Enabled)
                    PilotMapProvider.ApplyUnownedHatch(
                        views, pixels, geo.ProvinceAt, ActiveW, ActiveH);
                if (PilotMapProvider.Enabled && PilotMapProvider.ShowRivers)
                {
                    var riverLevel = MapViewport.IsInitialized
                        ? MapViewport.State.Level
                        : MapObservationLevel.World;
                    PilotMapProvider.ApplyRivers(
                        pixels, geo.IsLand, ActiveW, ActiveH,
                        geo.MinX, geo.MaxX, geo.MinY, geo.MaxY, riverLevel);
                }
                ApplyOccupationHatch(views, pixels, geo.ProvinceAt);
                ApplyBorders(views, pixels, geo.ProvinceAt);
                ApplyFrontOverlay(views, pixels, geo.ProvinceAt);
                ApplyStraitLinks(views, pixels, geo.MinX, geo.MaxX, geo.MinY, geo.MaxY);
                // v1_040/v1_041 — réservation partagée ; file d'importance puis Flush.
                MapLabelLayout.Begin(ActiveW, ActiveH);
                try
                {
                    ApplyLabelsByDensity(
                        views, pixels, geo.MinX, geo.MaxX, geo.MinY, geo.MaxY,
                        labels, selectedProvinceId);
                    overlay?.Invoke(pixels);
                    // Après overlay : marqueurs/bâtiments réservés ; labels enqueued → place.
                    MapLabelLayout.Flush(pixels);
                    CityMarkerComposer.SyncStatsAfterFlush();
                }
                finally
                {
                    MapLabelLayout.End();
                }

                return pixels;
            }
            finally
            {
                ActiveW = prevW;
                ActiveH = prevH;
                ActiveGlyphScale = prevScale;
            }
        }

        /// <summary>Cartouche bitmap « t0250 » en haut à gauche (glyphe 5×7 ×2).</summary>
        public static void ApplyTickCartouche(Color32[] pixels, string text)
        {
            const int pad = 4;
            DrawBitmapText(pixels, text, pad, ActiveH - pad - GlyphH * ActiveGlyphScale, LabelLight, LabelDark);
        }

        /// <summary>
        /// Rendu thématique réutilisant MapGeometry (zéro recalcul de masque).
        /// fillsByViewIndex[i] colore la province ViewsSkeleton[i] ; drawOverlay reçoit les pixels
        /// après frontières + étiquettes (légende, etc.).
        /// Lit l'EntityManager pour les étiquettes — préférer FromViews (v1_006).
        /// </summary>
        public static void ExportThematicWithGeometry(
            EntityManager em,
            int tick,
            string outputPath,
            MapGeometry geo,
            Color32[] fillsByViewIndex,
            Action<Color32[]> drawOverlay)
        {
            if (geo == null || fillsByViewIndex == null)
                return;

            var colors = CountryColors.Load();
            var views = BuildViewsAligned(em, geo.ViewsSkeleton, colors);
            ExportThematicFromViews(
                views, tick, outputPath, geo, fillsByViewIndex, drawOverlay, colors);
        }

        /// <summary>
        /// Rendu thématique en pixels (sans PNG) — point d'entrée in-game (v1_008).
        /// N'accède PAS à l'EntityManager — règle v1_006.
        /// </summary>
        public static Color32[] RenderThematicPixels(
            List<ProvinceView> views,
            MapGeometry geo,
            Color32[] fillsByViewIndex,
            Action<Color32[]> drawOverlay,
            CountryColors.Table colors = null)
        {
            if (geo == null || fillsByViewIndex == null || views == null || views.Count == 0)
                return null;

            if (colors == null)
                colors = CountryColors.Load();

            var prevW = ActiveW;
            var prevH = ActiveH;
            ActiveW = geo.Width;
            ActiveH = geo.Height;
            try
            {
                // Copie : ne pas muter le snapshot partagé entre couches.
                var renderViews = new List<ProvinceView>(views.Count);
                for (var i = 0; i < views.Count; i++)
                {
                    var v = views[i];
                    if (i < fillsByViewIndex.Length)
                        v.Fill = fillsByViewIndex[i];
                    renderViews.Add(v);
                }

                var pixels = new Color32[ActiveW * ActiveH];
                ColorFromProvinceAt(renderViews, pixels, geo.ProvinceAt, geo.IsLand, colors.Sea);
                if (PilotMapProvider.Enabled)
                {
                    var lod = PilotMapProvider.LodForObservation(
                        MapViewport.IsInitialized
                            ? MapViewport.State.Level
                            : MapObservationLevel.World);
                    PilotMapProvider.ApplyHillshadeOnLand(
                        pixels, geo.IsLand, ActiveW, ActiveH,
                        geo.MinX, geo.MaxX, geo.MinY, geo.MaxY,
                        colors.Sea, lod);
                }

                ApplyCoastAndRelief(pixels, geo.IsLand);
                LastLandMassReport = geo.LandMasses;
                LastMaskStats = geo.MaskStats;
                ApplyBorders(renderViews, pixels, geo.ProvinceAt);
                ApplyStraitLinks(renderViews, pixels, geo.MinX, geo.MaxX, geo.MinY, geo.MaxY);
                if (PilotMapProvider.Enabled && PilotMapProvider.ShowRivers)
                {
                    var riverLevel = MapViewport.IsInitialized
                        ? MapViewport.State.Level
                        : MapObservationLevel.World;
                    PilotMapProvider.ApplyRivers(
                        pixels, geo.IsLand, ActiveW, ActiveH,
                        geo.MinX, geo.MaxX, geo.MinY, geo.MaxY, riverLevel);
                }
                ApplyLabels(renderViews, pixels, geo.MinX, geo.MaxX, geo.MinY, geo.MaxY);
                drawOverlay?.Invoke(pixels);
                return pixels;
            }
            finally
            {
                ActiveW = prevW;
                ActiveH = prevH;
            }
        }

        /// <summary>
        /// Rendu thématique depuis des vues déjà résolues (tags capturés au tick).
        /// N'accède PAS à l'EntityManager — règle v1_006.
        /// </summary>
        public static Color32[] ExportThematicFromViews(
            List<ProvinceView> views,
            int tick,
            string outputPath,
            MapGeometry geo,
            Color32[] fillsByViewIndex,
            Action<Color32[]> drawOverlay,
            CountryColors.Table colors = null)
        {
            if (geo == null || fillsByViewIndex == null || views == null || views.Count == 0)
            {
                Debug.LogWarning($"MapSnapshotExporter.thematic: aucune province à tick={tick}.");
                return null;
            }

            var pixels = RenderThematicPixels(views, geo, fillsByViewIndex, drawOverlay, colors);
            if (pixels == null)
                return null;

            var prevW = ActiveW;
            var prevH = ActiveH;
            ActiveW = geo.Width;
            ActiveH = geo.Height;
            try
            {
                WriteMapBufferPng(pixels, geo.Width, geo.Height, outputPath);
                Debug.Log($"MapSnapshotExporter.thematic: tick={tick} → {outputPath}");
                return pixels;
            }
            finally
            {
                ActiveW = prevW;
                ActiveH = prevH;
            }
        }

        /// <summary>
        /// Construit des ProvinceView depuis des tags déjà résolus (pas d'Entity différée).
        /// </summary>
        public static List<ProvinceView> BuildViewsFromTags(
            List<ProvinceView> skeleton,
            string[] ownerTags,
            string[] controllerTags,
            bool[] occupied,
            CountryColors.Table colors,
            string[] ownerNames = null)
        {
            var views = new List<ProvinceView>(skeleton.Count);
            for (var i = 0; i < skeleton.Count; i++)
            {
                var sk = skeleton[i];
                var ownerTag = ownerTags != null && i < ownerTags.Length ? ownerTags[i] : "";
                if (ownerTag == null) ownerTag = "";
                var fill = string.IsNullOrEmpty(ownerTag) ? colors.Unowned : colors.ForTag(ownerTag);
                var ownerName = "";
                if (ownerNames != null && i < ownerNames.Length && !string.IsNullOrEmpty(ownerNames[i]))
                    ownerName = ownerNames[i];
                else if (!string.IsNullOrEmpty(ownerTag))
                    ownerName = colors.NameForTag(ownerTag);

                var isOcc = occupied != null && i < occupied.Length && occupied[i];
                var cTag = controllerTags != null && i < controllerTags.Length
                    ? controllerTags[i]
                    : "";
                if (cTag == null) cTag = "";
                var controllerColor = fill;
                if (isOcc && !string.IsNullOrEmpty(cTag))
                    controllerColor = colors.ForTag(cTag);

                views.Add(new ProvinceView
                {
                    Id = sk.Id,
                    X = sk.X,
                    Y = sk.Y,
                    Owner = Entity.Null,
                    Controller = Entity.Null,
                    Fill = fill,
                    ControllerColor = controllerColor,
                    Occupied = isOcc,
                    OwnerTag = ownerTag,
                    OwnerName = ownerName,
                    ProvinceName = sk.ProvinceName
                });
            }

            return views;
        }

        /// <summary>
        /// Compte les octets RGBA différents hors bandeau bas (légende thématique).
        /// excludeBottomRows=0 → comparaison pleine image (carte politique).
        /// Convention pixels : y=0 en bas (bandeau légende sur les petites y).
        /// </summary>
        public static int CountPixelByteDiffs(
            Color32[] a, Color32[] b, int width, int height, int excludeBottomRows)
        {
            if (a == null || b == null || a.Length != b.Length)
                return -1;

            var y0 = excludeBottomRows;
            if (y0 < 0) y0 = 0;
            if (y0 > height) y0 = height;

            var diffs = 0;
            for (var y = y0; y < height; y++)
            {
                var row = y * width;
                for (var x = 0; x < width; x++)
                {
                    var i = row + x;
                    var ca = a[i];
                    var cb = b[i];
                    if (ca.r != cb.r) diffs++;
                    if (ca.g != cb.g) diffs++;
                    if (ca.b != cb.b) diffs++;
                    if (ca.a != cb.a) diffs++;
                }
            }

            return diffs;
        }

        /// <summary>
        /// Micro-glyphe 5×7 × échelle active (texte légende / cartouche).
        /// Convention buffer : nord en py=0, glyphe row=0 = haut de lettre vers le nord
        /// (aucune compensation locale — l'inversion a lieu une seule fois à l'encodage).
        /// </summary>
        public static void DrawBitmapText(
            Color32[] pixels, string text, int originX, int originY, Color32 fg, Color32 halo)
        {
            var cursorX = originX;
            var scale = ActiveGlyphScale;
            for (var ci = 0; ci < text.Length; ci++)
            {
                var ch = text[ci];
                var glyph = GetGlyph(ch);
                BlitGlyph(pixels, glyph, cursorX, originY, halo, outline: true);
                BlitGlyph(pixels, glyph, cursorX, originY, fg, outline: false);
                cursorX += GlyphW * scale + 1;
            }
        }

        /// <summary>Largeur en pixels d'une chaîne bitmap (échelle de mesure courante).</summary>
        public static int MeasureBitmapText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;
            var scale = EffectiveMeasureScale;
            return text.Length * (GlyphW * scale + 1) - 1;
        }

        public static int BitmapGlyphHeight => GlyphH * EffectiveMeasureScale;

        /// <summary>
        /// Exécute un dessin bitmap dans un espace pixels (ActiveW/H) donné — requis
        /// pour les captures DA hors résolution carte native.
        /// </summary>
        public static void WithPixelSize(int width, int height, System.Action action)
        {
            if (action == null)
                return;
            var prevW = ActiveW;
            var prevH = ActiveH;
            ActiveW = width;
            ActiveH = height;
            try
            {
                action();
            }
            finally
            {
                ActiveW = prevW;
                ActiveH = prevH;
            }
        }

        /// <summary>
        /// Encode brut (planche contact, buffer déjà en convention Texture2D).
        /// EncodeToPNG / Texture2D : y=0 en bas. Ne pas utiliser pour un buffer carte
        /// nord-en-py=0 — passer par <see cref="WriteMapBufferPng"/>.
        /// </summary>
        public static void WritePngSized(Color32[] pixels, int width, int height, string outputPath)
        {
            WithPixelSize(width, height, () => WritePng(pixels, outputPath));
        }

        /// <summary>
        /// Convention UNIQUE buffer carte (v1_077 / v1_079) :
        /// nord en py=0 ; tout écrivain (remplissage, ombrage, hachures, fleuves,
        /// sprites, glyphes) emploie cette convention SANS compensation locale.
        /// EncodeToPNG / Texture2D : y=0 en bas → inversion UNE SEULE FOIS ici
        /// pour un PNG nord-en-haut (= ce que le joueur voit via UI Toolkit,
        /// où py=0 buffer apparaît en haut de l'écran).
        /// Seul chemin d'écriture PNG depuis un buffer carte.
        /// </summary>
        public static void WriteMapBufferPng(
            Color32[] northAtRow0, int width, int height, string outputPath)
        {
            if (northAtRow0 == null || width <= 0 || height <= 0)
                return;
            if (northAtRow0.Length != width * height)
                return;
            var flipped = FlipMapBufferRows(northAtRow0, width, height);
            WritePngSized(flipped, width, height, outputPath);
        }

        /// <summary>Inverse les rangées : py=0 (nord buffer) → bas Texture2D → haut du PNG vu.</summary>
        public static Color32[] FlipMapBufferRows(Color32[] pixels, int width, int height)
        {
            var flipped = new Color32[pixels.Length];
            for (var py = 0; py < height; py++)
            {
                var src = py * width;
                var dst = (height - 1 - py) * width;
                for (var x = 0; x < width; x++)
                    flipped[dst + x] = pixels[src + x];
            }

            return flipped;
        }

        /// <summary>
        /// Chaîne témoin asymétrique haut-bas (« P ») — barre du bol en haut, hampe en bas.
        /// Peinte dans le buffer (nord@py0, glyphe droit) pour contrôle fichier PNG.
        /// </summary>
        public const string TextOrientationWitness = "P";

        /// <summary>Bits 5×7 d'un glyphe (row 0 = haut de lettre). Null si inconnu.</summary>
        public static byte[] TryGetGlyphBits(char ch)
        {
            return GetGlyph(ch);
        }

        /// <summary>
        /// Peint le témoin d'orientation texte (fond sombre + glyphe clair) à (ox,oy) buffer.
        /// </summary>
        public static void PaintTextOrientationWitness(
            Color32[] pixels, int ox, int oy, int scale)
        {
            if (pixels == null || scale < 1)
                return;
            var prev = ActiveGlyphScale;
            ActiveGlyphScale = scale;
            try
            {
                var gw = GlyphW * scale + 4;
                var gh = GlyphH * scale + 4;
                var bg = new Color32(0x08, 0x08, 0x10, 255);
                var fg = new Color32(0xf8, 0xf8, 0xf0, 255);
                var halo = new Color32(0x00, 0x00, 0x00, 255);
                for (var y = oy - 2; y < oy - 2 + gh; y++)
                {
                    for (var x = ox - 2; x < ox - 2 + gw; x++)
                        SetPixelSafe(pixels, x, y, bg);
                }

                DrawBitmapText(pixels, TextOrientationWitness, ox, oy, fg, halo);
            }
            finally
            {
                ActiveGlyphScale = prev;
            }
        }

        /// <summary>
        /// Lit un PNG ÉCRIT et vérifie que le texte s'y lit dans le bon sens, via un
        /// motif de glyphe asymétrique (pas un pixel codé en dur, pas le buffer).
        /// Compare le meilleur score du motif droit au motif miroité verticalement.
        /// </summary>
        public static bool TryAssertPngTextUpright(
            string pngPath,
            out int uprightScore,
            out int flippedScore,
            out string detail,
            char probeChar = 'A',
            int minScore = 10)
        {
            uprightScore = flippedScore = 0;
            detail = "";
            if (string.IsNullOrEmpty(pngPath) || !File.Exists(pngPath))
            {
                detail = "missing " + pngPath;
                return false;
            }

            var bytes = File.ReadAllBytes(pngPath);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(tex, bytes, markNonReadable: false))
            {
                UnityEngine.Object.DestroyImmediate(tex);
                detail = "LoadImage fail " + pngPath;
                return false;
            }

            var pixels = tex.GetPixels32();
            var w = tex.width;
            var h = tex.height;
            UnityEngine.Object.DestroyImmediate(tex);
            if (pixels == null || pixels.Length != w * h || h <= 0)
            {
                detail = "bad pixels " + pngPath;
                return false;
            }

            var glyph = GetGlyph(probeChar);
            if (glyph == null || glyph.Length < GlyphH)
            {
                detail = "glyphe inconnu " + probeChar;
                return false;
            }

            // Échelles v1_073 : 2 / 3 / 5 (+ 1 filet). Pas de pas 1 (trop lent).
            var scales = new[] { 2, 3, 5, 1 };
            var maxUp = 0;
            var maxFlip = 0;
            var bestScale = 0;
            var bestX = -1;
            var bestY = -1;
            var bestIsUp = true;
            for (var si = 0; si < scales.Length; si++)
            {
                var scale = scales[si];
                var cellW = GlyphW * scale;
                var cellH = GlyphH * scale;
                if (cellW >= w || cellH >= h)
                    continue;
                var step = Math.Max(scale, 2);
                for (var vy = 0; vy <= h - cellH; vy += step)
                {
                    for (var vx = 0; vx <= w - cellW; vx += step)
                    {
                        ScoreGlyphAtViewer(
                            pixels, w, h, vx, vy, glyph, scale,
                            out var up, out var flip);
                        if (up > maxUp)
                        {
                            maxUp = up;
                            bestScale = scale;
                            bestX = vx;
                            bestY = vy;
                            bestIsUp = true;
                        }

                        if (flip > maxFlip)
                        {
                            maxFlip = flip;
                            if (flip > maxUp)
                            {
                                bestScale = scale;
                                bestX = vx;
                                bestY = vy;
                                bestIsUp = false;
                            }
                        }
                    }
                }
            }

            uprightScore = maxUp;
            flippedScore = maxFlip;
            var upright = maxUp >= minScore && maxUp > maxFlip;
            detail =
                Path.GetFileName(pngPath) +
                " probe=" + probeChar +
                " upright=" + maxUp.ToString(CultureInfo.InvariantCulture) +
                " flipped=" + maxFlip.ToString(CultureInfo.InvariantCulture) +
                " bestScale=" + bestScale.ToString(CultureInfo.InvariantCulture) +
                " at=(" + bestX.ToString(CultureInfo.InvariantCulture) +
                "," + bestY.ToString(CultureInfo.InvariantCulture) + ")" +
                " bestIs=" + (bestIsUp ? "up" : "flip") +
                " verdict=" + (upright ? "UPRIGHT" : "FLIPPED_OR_WEAK");
            return upright;
        }

        /// <summary>
        /// Contrôle exact sur un témoin peint à une position buffer connue puis
        /// écrit via WriteMapBufferPng (nord@py0 → haut PNG).
        /// </summary>
        public static bool TryAssertPngWitnessTextUpright(
            string pngPath,
            int bufferOx,
            int bufferOy,
            int width,
            int height,
            int scale,
            out int uprightScore,
            out int flippedScore,
            out string detail,
            char probeChar = 'P')
        {
            uprightScore = flippedScore = 0;
            detail = "";
            if (string.IsNullOrEmpty(pngPath) || !File.Exists(pngPath))
            {
                detail = "missing " + pngPath;
                return false;
            }

            var bytes = File.ReadAllBytes(pngPath);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(tex, bytes, markNonReadable: false))
            {
                UnityEngine.Object.DestroyImmediate(tex);
                detail = "LoadImage fail " + pngPath;
                return false;
            }

            var pixels = tex.GetPixels32();
            var w = tex.width;
            var h = tex.height;
            UnityEngine.Object.DestroyImmediate(tex);
            if (w != width || h != height || pixels == null)
            {
                detail = "dim mismatch " + pngPath;
                return false;
            }

            var glyph = GetGlyph(probeChar);
            // Buffer py=0 → haut du PNG vu → viewerY = bufferOy.
            var viewerX = bufferOx;
            var viewerY = bufferOy;
            ScoreGlyphAtViewer(
                pixels, w, h, viewerX, viewerY, glyph, scale,
                out uprightScore, out flippedScore);
            var onBits = CountGlyphOnBits(glyph) * scale * scale;
            var upright = uprightScore >= Math.Max(6, onBits / 3) &&
                          uprightScore > flippedScore;
            detail =
                Path.GetFileName(pngPath) +
                " witness=" + probeChar +
                " scale=" + scale.ToString(CultureInfo.InvariantCulture) +
                " buf=(" + bufferOx + "," + bufferOy + ")" +
                " upright=" + uprightScore.ToString(CultureInfo.InvariantCulture) +
                " flipped=" + flippedScore.ToString(CultureInfo.InvariantCulture) +
                " onBits=" + onBits.ToString(CultureInfo.InvariantCulture) +
                " verdict=" + (upright ? "UPRIGHT" : "FLIPPED_OR_WEAK");
            return upright;
        }

        static int CountGlyphOnBits(byte[] glyph)
        {
            var n = 0;
            for (var row = 0; row < GlyphH; row++)
            {
                var bits = glyph[row];
                for (var col = 0; col < GlyphW; col++)
                {
                    if (((bits >> (GlyphW - 1 - col)) & 1) != 0)
                        n++;
                }
            }

            return n;
        }

        /// <summary>
        /// Score le motif droit vs miroité en coords viewer (y=0 en haut du fichier).
        /// Texture2D GetPixels32 : y=0 en bas → viewerY mappe vers py = h-1-viewerY.
        /// </summary>
        static void ScoreGlyphAtViewer(
            Color32[] pixels, int w, int h,
            int viewerX, int viewerY,
            byte[] glyph, int scale,
            out int uprightScore, out int flippedScore)
        {
            uprightScore = flippedScore = 0;
            var onBits = 0;
            for (var row = 0; row < GlyphH; row++)
            {
                var bits = glyph[row];
                for (var col = 0; col < GlyphW; col++)
                {
                    if (((bits >> (GlyphW - 1 - col)) & 1) == 0)
                        continue;
                    onBits++;
                    for (var sy = 0; sy < scale; sy++)
                    {
                        for (var sx = 0; sx < scale; sx++)
                        {
                            var vx = viewerX + col * scale + sx;
                            // Motif droit : row 0 vers le haut (viewerY croissant vers le bas).
                            var vyUp = viewerY + row * scale + sy;
                            // Motif miroité : row 0 vers le bas.
                            var vyFlip = viewerY + (GlyphH - 1 - row) * scale + sy;
                            if (IsBrightViewer(pixels, w, h, vx, vyUp))
                                uprightScore++;
                            if (IsBrightViewer(pixels, w, h, vx, vyFlip))
                                flippedScore++;
                        }
                    }
                }
            }

            // Pénaliser si le fond est aussi clair (motif non isolé) : rien — le ratio suffit.
            if (onBits == 0)
                uprightScore = flippedScore = 0;
        }

        static bool IsBrightViewer(Color32[] pixels, int w, int h, int viewerX, int viewerY)
        {
            if (viewerX < 0 || viewerY < 0 || viewerX >= w || viewerY >= h)
                return false;
            var py = h - 1 - viewerY;
            var c = pixels[py * w + viewerX];
            // Glyphe clair (LabelLight ~0xf5) vs halo sombre / carte colorée.
            return c.r + c.g + c.b >= 500 && c.r >= 160 && c.g >= 160 && c.b >= 160;
        }

        /// <summary>
        /// Lit un PNG ÉCRIT et vérifie que le nord est en haut via un repère géographique :
        /// hauteur moyenne (y=0 en haut du fichier) d'un pays septentrional &lt; méridional.
        /// </summary>
        public static bool TryAssertPngNorthUp(
            string pngPath,
            Color32 northCountry,
            Color32 southCountry,
            out float meanYNorth,
            out float meanYSouth,
            out float meanXNorth,
            out float meanXSouth,
            out int countNorth,
            out int countSouth,
            out string detail,
            int colorTol = 12)
        {
            meanYNorth = meanYSouth = meanXNorth = meanXSouth = 0f;
            countNorth = countSouth = 0;
            detail = "";
            if (string.IsNullOrEmpty(pngPath) || !File.Exists(pngPath))
            {
                detail = "missing " + pngPath;
                return false;
            }

            var bytes = File.ReadAllBytes(pngPath);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(tex, bytes, markNonReadable: false))
            {
                UnityEngine.Object.DestroyImmediate(tex);
                detail = "LoadImage fail " + pngPath;
                return false;
            }

            var pixels = tex.GetPixels32();
            var w = tex.width;
            var h = tex.height;
            UnityEngine.Object.DestroyImmediate(tex);
            if (pixels == null || pixels.Length != w * h || h <= 0)
            {
                detail = "bad pixels " + pngPath;
                return false;
            }

            double sumYN = 0, sumYS = 0, sumXN = 0, sumXS = 0;
            for (var py = 0; py < h; py++)
            {
                // Texture2D y=0 en bas → y fichier (haut=0) = 1 − (py+0.5)/h
                var viewerY = 1.0 - (py + 0.5) / h;
                var row = py * w;
                for (var px = 0; px < w; px++)
                {
                    var c = pixels[row + px];
                    var viewerX = (px + 0.5) / w;
                    if (ColorNearRgb(c, northCountry, colorTol))
                    {
                        sumYN += viewerY;
                        sumXN += viewerX;
                        countNorth++;
                    }

                    if (ColorNearRgb(c, southCountry, colorTol))
                    {
                        sumYS += viewerY;
                        sumXS += viewerX;
                        countSouth++;
                    }
                }
            }

            if (countNorth < 10 || countSouth < 10)
            {
                detail =
                    "trop peu de pixels tag " + Path.GetFileName(pngPath) +
                    " n=" + countNorth.ToString(CultureInfo.InvariantCulture) +
                    " s=" + countSouth.ToString(CultureInfo.InvariantCulture);
                return false;
            }

            meanYNorth = (float)(sumYN / countNorth);
            meanYSouth = (float)(sumYS / countSouth);
            meanXNorth = (float)(sumXN / countNorth);
            meanXSouth = (float)(sumXS / countSouth);
            var ok = meanYNorth < meanYSouth;
            detail =
                Path.GetFileName(pngPath) +
                " ENG_y=" + meanYNorth.ToString("F3", CultureInfo.InvariantCulture) +
                " CAS_y=" + meanYSouth.ToString("F3", CultureInfo.InvariantCulture) +
                " ENG_x=" + meanXNorth.ToString("F3", CultureInfo.InvariantCulture) +
                " CAS_x=" + meanXSouth.ToString("F3", CultureInfo.InvariantCulture) +
                " north_up=" + ok.ToString(CultureInfo.InvariantCulture);
            return ok;
        }

        static bool ColorNearRgb(Color32 a, Color32 b, int tol) =>
            Math.Abs(a.r - b.r) <= tol &&
            Math.Abs(a.g - b.g) <= tol &&
            Math.Abs(a.b - b.b) <= tol;

        /// <summary>
        /// Image de contrôle : 50 nœuds + arêtes terrestres (plein) + détroits (pointillé).
        /// Ne lit pas l'EntityManager — géométrie + province_adjacency.json uniquement.
        /// </summary>
        public static void ExportAdjacencyGraph(string outputPath)
        {
            var points = ProvinceCoordinates.LoadProjected(out _);
            if (points.Count == 0)
            {
                Debug.LogWarning("MapSnapshotExporter.ExportAdjacencyGraph: aucune coordonnée.");
                return;
            }

            var byId = new Dictionary<int, ProvinceCoordinates.Point>(points.Count);
            for (var i = 0; i < points.Count; i++)
                byId[points[i].Id] = points[i];

            var dummyViews = new List<ProvinceView>(points.Count);
            for (var i = 0; i < points.Count; i++)
            {
                var p = points[i];
                dummyViews.Add(new ProvinceView { Id = p.Id, X = p.X, Y = p.Y });
            }

            ComputeBounds(dummyViews, out var minX, out var maxX, out var minY, out var maxY);

            var pixels = new Color32[ActiveW * ActiveH];
            var sea = CountryColors.Load().Sea;
            for (var i = 0; i < pixels.Length; i++)
                pixels[i] = sea;

            var adjacency = GameDataLoader.LoadProvinceAdjacency();
            var drawnLand = new HashSet<long>();
            var drawnStrait = new HashSet<long>();

            for (var a = 0; a < adjacency.Count; a++)
            {
                var def = adjacency[a];
                if (!byId.ContainsKey(def.id))
                    continue;

                if (def.neighbors != null)
                {
                    for (var n = 0; n < def.neighbors.Count; n++)
                    {
                        var other = def.neighbors[n];
                        var key = EdgeKey(def.id, other);
                        if (!drawnLand.Add(key))
                            continue;
                        if (!byId.ContainsKey(other))
                            continue;
                        DrawLineProjected(
                            pixels, byId[def.id].X, byId[def.id].Y, byId[other].X, byId[other].Y,
                            minX, maxX, minY, maxY, GraphLandEdge, dashed: false);
                    }
                }

                if (def.straits != null)
                {
                    for (var s = 0; s < def.straits.Count; s++)
                    {
                        var other = def.straits[s];
                        var key = EdgeKey(def.id, other);
                        if (!drawnStrait.Add(key))
                            continue;
                        if (!byId.ContainsKey(other))
                            continue;
                        DrawLineProjected(
                            pixels, byId[def.id].X, byId[def.id].Y, byId[other].X, byId[other].Y,
                            minX, maxX, minY, maxY, GraphStraitEdge, dashed: true);
                    }
                }
            }

            for (var i = 0; i < points.Count; i++)
            {
                var p = points[i];
                WorldToPixel(p.X, p.Y, minX, maxX, minY, maxY, out var px, out var py);
                FillCircle(pixels, px, py, 4, GraphNode);
            }

            WriteMapBufferPng(pixels, ActiveW, ActiveH, outputPath);
            Debug.Log(
                $"MapSnapshotExporter: graph → {outputPath} " +
                $"(landEdges={drawnLand.Count} straits={drawnStrait.Count} nodes={points.Count})");
        }

        /// <summary>
        /// Légende tag → hex → nombre de provinces possédées (Owner).
        /// </summary>
        public static string FormatLegend(EntityManager em)
        {
            var colors = CountryColors.Load();
            var counts = new Dictionary<string, int>();
            var hexByTag = new Dictionary<string, string>();

            using var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<ProvinceData>(),
                ComponentType.ReadOnly<ProvinceOwnership>());
            using var ownerships = query.ToComponentDataArray<ProvinceOwnership>(Allocator.Temp);

            for (var i = 0; i < ownerships.Length; i++)
            {
                var owner = ownerships[i].Owner;
                string tag;
                if (owner == Entity.Null || !em.HasComponent<CountryData>(owner))
                {
                    tag = "(unowned)";
                    hexByTag[tag] = CountryColors.ToHex(colors.Unowned);
                }
                else
                {
                    tag = em.GetComponentData<CountryData>(owner).Tag.ToString();
                    hexByTag[tag] = CountryColors.ToHex(colors.ForTag(tag));
                }

                counts.TryGetValue(tag, out var n);
                counts[tag] = n + 1;
            }

            var tags = new List<string>(counts.Keys);
            tags.Sort(StringComparer.Ordinal);
            var lines = new List<string>(tags.Count + 1);
            lines.Add("LEGEND tag hex provinces");
            for (var i = 0; i < tags.Count; i++)
            {
                var t = tags[i];
                lines.Add($"{t} {hexByTag[t]} {counts[t]}");
            }

            return string.Join("\n", lines);
        }

        public static string FormatConstantsLine()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "CONSTANTS CELL_RADIUS={0} CORRIDOR_HALF_WIDTH={1} STRAIT_HALF_WIDTH={2} " +
                "(StraitEndInset={3} ConcaveHullDigFactor={4})",
                CellRadius, CorridorHalfWidth, StraitHalfWidth, StraitEndInset, ConcaveHullDigFactor);
        }

        public static string FormatMaskStatsLine()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "MASKSTATS mode={0} components={1} triangles={2} trianglePixels+={3} " +
                "hullPixels+={4} hexSeeds={5} pocketPixels+={6} buildMs={7:F1}",
                LastMaskStats.Mode,
                LastMaskStats.ComponentCount,
                LastMaskStats.TriangleCount,
                LastMaskStats.TrianglePixelsAdded,
                LastMaskStats.HullPixelsAdded,
                LastMaskStats.HexSeedCount,
                LastMaskStats.PocketPixelsAdded,
                LastMaskStats.BuildMilliseconds);
        }

        public static string FormatShapeReportLine(ShapeCoherenceReport r)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "SHAPE coastal_touch={0}/{1} inland_false_sea={2}/{3} " +
                "land_border={4}/{5} strait_glued={6}/{7}",
                r.CoastalTouchingSea, r.CoastalDeclared,
                r.InlandTouchingSea, r.InlandDeclared,
                r.LandAdjacencySharingBorder, r.LandAdjacencyPairs,
                r.StraitPairsGlued, r.StraitPairs);
        }

        static List<ProvinceView> BuildViews(
            EntityManager em,
            List<ProvinceCoordinates.Point> points,
            CountryColors.Table colors)
        {
            var byId = new Dictionary<int, ProvinceCoordinates.Point>(points.Count);
            for (var i = 0; i < points.Count; i++)
                byId[points[i].Id] = points[i];

            var views = new List<ProvinceView>(points.Count);

            using var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<ProvinceData>(),
                ComponentType.ReadOnly<ProvinceOwnership>());
            using var provinces = query.ToComponentDataArray<ProvinceData>(Allocator.Temp);
            using var ownerships = query.ToComponentDataArray<ProvinceOwnership>(Allocator.Temp);

            for (var i = 0; i < provinces.Length; i++)
            {
                var id = provinces[i].ProvinceId;
                if (!byId.TryGetValue(id, out var pt))
                {
                    Debug.LogWarning(
                        $"MapSnapshotExporter: province id={id} sans coordonnée dans province_coordinates.json — ignorée.");
                    continue;
                }

                var own = ownerships[i];
                var ownerTag = "";
                var ownerName = "";
                var fill = colors.Unowned;
                if (own.Owner != Entity.Null && em.HasComponent<CountryData>(own.Owner))
                {
                    var cd = em.GetComponentData<CountryData>(own.Owner);
                    ownerTag = cd.Tag.ToString();
                    ownerName = cd.Name.ToString();
                    if (string.IsNullOrEmpty(ownerName))
                        ownerName = colors.NameForTag(ownerTag);
                    fill = colors.ForTag(ownerTag);
                }

                var occupied = own.Controller != Entity.Null
                    && own.Owner != Entity.Null
                    && own.Controller != own.Owner;
                var controllerColor = fill;
                if (occupied && em.HasComponent<CountryData>(own.Controller))
                {
                    var cTag = em.GetComponentData<CountryData>(own.Controller).Tag.ToString();
                    controllerColor = colors.ForTag(cTag);
                }

                views.Add(new ProvinceView
                {
                    Id = id,
                    X = pt.X,
                    Y = pt.Y,
                    Owner = own.Owner,
                    Controller = own.Controller,
                    Fill = fill,
                    ControllerColor = controllerColor,
                    Occupied = occupied,
                    OwnerTag = ownerTag,
                    OwnerName = ownerName
                });
            }

            // Déterminisme : ordre par id province, jamais par chunk ECS.
            views.Sort((a, b) => a.Id.CompareTo(b.Id));
            ApplyFrontFlags(em, views);
            return views;
        }

        /// <summary>
        /// Aligne les vues ownership sur le squelette géométrique (même ordre / mêmes indices).
        /// </summary>
        /// <summary>
        /// v1_095 — DEUX COULEURS DE RÉFÉRENCE, NORD ET SUD, DÉRIVÉES DU MONDE JOUÉ.
        ///
        /// POURQUOI CETTE FONCTION EXISTE. Les contrôles d'orientation nommaient
        /// deux pays en dur : ENG au nord, CAS au sud. Ce repère a tenu tant que la
        /// carte pilote était peinte depuis un fichier figé à 1400. Depuis v1_094
        /// elle peint le monde joué — et à t1000, la Castille a perdu la Navarre,
        /// seule province castillane de la fenêtre pilote : zéro pixel CAS, contrôle
        /// aveugle. Les tests ne passaient donc que parce que la carte était morte.
        ///
        /// La parade est celle du projet : ne pas nommer la référence, la DÉRIVER.
        /// On prend le pays le plus au nord et le plus au sud PARMI CEUX RÉELLEMENT
        /// PEINTS, pondérés par le nombre de cellules — ce qui reste vrai quel que
        /// soit le vainqueur des guerres.
        /// </summary>
        public static bool TryDeriveNorthSouthReferenceColors(
            EntityManager em,
            CountryColors.Table colors,
            out Color32 north,
            out Color32 south,
            out string detail,
            int minCells = 3)
        {
            north = default;
            south = default;
            detail = "";

            if (!PilotMapProvider.Enabled)
            {
                detail = "hors mode pilote : pas de cellules à pondérer";
                return false;
            }

            var ownerByProvince = new Dictionary<int, string>();
            using (var q = em.CreateEntityQuery(
                       ComponentType.ReadOnly<ProvinceData>(),
                       ComponentType.ReadOnly<ProvinceOwnership>()))
            using (var pdata = q.ToComponentDataArray<ProvinceData>(Allocator.Temp))
            using (var owns = q.ToComponentDataArray<ProvinceOwnership>(Allocator.Temp))
            {
                for (var i = 0; i < pdata.Length; i++)
                {
                    var o = owns[i].Owner;
                    if (o == Entity.Null || !em.HasComponent<CountryData>(o))
                        continue;
                    ownerByProvince[pdata[i].ProvinceId] =
                        em.GetComponentData<CountryData>(o).Tag.ToString();
                }
            }

            var sumLat = new Dictionary<string, double>();
            var cells = new Dictionary<string, int>();
            var n = PilotMapProvider.CellCount;
            for (var v = 0; v < n; v++)
            {
                var pid = PilotMapProvider.ProvinceIdOfView(v);
                if (pid <= 0 || !ownerByProvince.TryGetValue(pid, out var tag))
                    continue;
                var lat = PilotMapProvider.CellLatOfView(v);
                if (float.IsNaN(lat))
                    continue;
                sumLat.TryGetValue(tag, out var s);
                cells.TryGetValue(tag, out var c);
                sumLat[tag] = s + lat;
                cells[tag] = c + 1;
            }

            var northTag = "";
            var southTag = "";
            var bestNorth = double.NegativeInfinity;
            var bestSouth = double.PositiveInfinity;
            foreach (var kv in cells)
            {
                if (kv.Value < minCells)
                    continue;
                var mean = sumLat[kv.Key] / kv.Value;
                // Départage par tag pour rester déterministe à latitude égale.
                if (mean > bestNorth || (mean == bestNorth &&
                                         string.CompareOrdinal(kv.Key, northTag) < 0))
                {
                    bestNorth = mean;
                    northTag = kv.Key;
                }

                if (mean < bestSouth || (mean == bestSouth &&
                                         string.CompareOrdinal(kv.Key, southTag) < 0))
                {
                    bestSouth = mean;
                    southTag = kv.Key;
                }
            }

            if (string.IsNullOrEmpty(northTag) || string.IsNullOrEmpty(southTag) ||
                northTag == southTag)
            {
                detail = "repère indérivable : nord='" + northTag + "' sud='" + southTag +
                         "' (pays peints avec ≥" + minCells + " cellules : " + cells.Count + ")";
                return false;
            }

            north = colors.ForTag(northTag);
            south = colors.ForTag(southTag);
            detail = "repère dérivé : nord=" + northTag +
                     " (lat moy " + bestNorth.ToString("0.0", CultureInfo.InvariantCulture) +
                     ") sud=" + southTag +
                     " (lat moy " + bestSouth.ToString("0.0", CultureInfo.InvariantCulture) + ")";
            return true;
        }

        /// <summary>
        /// v1_095 — mêmes vues que le rendu CPU, exposées pour le rendu GPU.
        /// Un seul constructeur de vues pour les deux chemins : sans ça, les deux
        /// cartes divergeraient sans que rien ne le signale.
        /// </summary>
        public static List<ProvinceView> BuildViewsForRender(
            EntityManager em,
            List<ProvinceView> skeleton,
            CountryColors.Table colors)
            => BuildViewsAligned(em, skeleton, colors);

        static List<ProvinceView> BuildViewsAligned(
            EntityManager em,
            List<ProvinceView> skeleton,
            CountryColors.Table colors)
        {
            // v1_094 — chemin UNIQUE. La carte pilote ne peint plus depuis
            // ownership_1400.json : elle lit le monde joué, comme la carte héritée.
            // La seule différence entre les deux modes est la résolution de l'identité :
            // en pilote, sk.Id est un cell_id qu'il faut traduire en ProvinceId.
            var pilot = PilotMapProvider.Enabled;

            // v1_094 — chemin d'avant ce brief, conservé pour la mesure rouge.
            if (pilot && DebugPilotColorsFromDisk)
            {
                var diskViews = new List<ProvinceView>(skeleton.Count);
                PilotMapProvider.ApplyPilotColors(skeleton, colors, diskViews);
                ApplyFrontFlags(em, diskViews);
                return diskViews;
            }

            var ownById = new Dictionary<int, ProvinceOwnership>(skeleton.Count);
            using var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<ProvinceData>(),
                ComponentType.ReadOnly<ProvinceOwnership>());
            using var provinces = query.ToComponentDataArray<ProvinceData>(Allocator.Temp);
            using var ownerships = query.ToComponentDataArray<ProvinceOwnership>(Allocator.Temp);
            for (var i = 0; i < provinces.Length; i++)
                ownById[provinces[i].ProvinceId] = ownerships[i];

            var views = new List<ProvinceView>(skeleton.Count);
            for (var i = 0; i < skeleton.Count; i++)
            {
                var sk = skeleton[i];
                var simId = PilotMapProvider.SimulationProvinceIdOfView(sk.Id);

                // Cellule hors de toute province : hachure « sans propriétaire ».
                // Ce n'est pas un défaut de données, c'est la fenêtre pilote qui
                // déborde des 50 provinces héritées (43 cellules sur 237).
                if (pilot && simId <= 0)
                {
                    views.Add(new ProvinceView
                    {
                        Id = sk.Id,
                        X = sk.X,
                        Y = sk.Y,
                        Owner = Entity.Null,
                        Controller = Entity.Null,
                        Fill = PilotMapProvider.UnownedFill,
                        ControllerColor = PilotMapProvider.UnownedFill,
                        Occupied = false,
                        OwnerTag = "",
                        OwnerName = "",
                        ProvinceName = sk.ProvinceName
                    });
                    continue;
                }

                ownById.TryGetValue(simId, out var own);

                var ownerTag = "";
                var ownerName = "";
                var fill = colors.Unowned;
                if (own.Owner != Entity.Null && em.HasComponent<CountryData>(own.Owner))
                {
                    var cd = em.GetComponentData<CountryData>(own.Owner);
                    ownerTag = cd.Tag.ToString();
                    ownerName = cd.Name.ToString();
                    if (string.IsNullOrEmpty(ownerName))
                        ownerName = colors.NameForTag(ownerTag);
                    fill = colors.ForTag(ownerTag);
                }

                var occupied = own.Controller != Entity.Null
                    && own.Owner != Entity.Null
                    && own.Controller != own.Owner;
                var controllerColor = fill;
                if (occupied && em.HasComponent<CountryData>(own.Controller))
                {
                    var cTag = em.GetComponentData<CountryData>(own.Controller).Tag.ToString();
                    controllerColor = colors.ForTag(cTag);
                }

                // En pilote, le squelette nomme « cell 1164 » : on préfère le nom de
                // la province rattachée, seul nom qu'un joueur puisse reconnaître.
                var displayName = sk.ProvinceName;
                if (pilot)
                {
                    var attached = PilotMapProvider.ProvinceNameOfCell(sk.Id);
                    if (!string.IsNullOrEmpty(attached))
                        displayName = attached;
                }

                views.Add(new ProvinceView
                {
                    Id = sk.Id,
                    X = sk.X,
                    Y = sk.Y,
                    Owner = own.Owner,
                    Controller = own.Controller,
                    Fill = fill,
                    ControllerColor = controllerColor,
                    Occupied = occupied,
                    OwnerTag = ownerTag,
                    OwnerName = ownerName,
                    ProvinceName = displayName
                });
            }

            ApplyFrontFlags(em, views);
            return views;
        }

        static void ColorFromProvinceAt(
            List<ProvinceView> views,
            Color32[] pixels,
            int[] provinceAt,
            bool[] isLand,
            Color32 sea)
        {
            for (var i = 0; i < pixels.Length; i++)
            {
                if (!isLand[i])
                {
                    pixels[i] = sea;
                    continue;
                }

                var vi = provinceAt[i];
                if (vi >= 0 && vi < views.Count)
                    pixels[i] = views[vi].Fill;
                else
                    pixels[i] = sea;
            }
        }

        static void ComputeBounds(
            List<ProvinceView> views,
            out float minX, out float maxX, out float minY, out float maxY)
        {
            minX = maxX = views[0].X;
            minY = maxY = views[0].Y;
            for (var i = 1; i < views.Count; i++)
            {
                var v = views[i];
                if (v.X < minX) minX = v.X;
                if (v.X > maxX) maxX = v.X;
                if (v.Y < minY) minY = v.Y;
                if (v.Y > maxY) maxY = v.Y;
            }

            var dx = maxX - minX;
            var dy = maxY - minY;
            if (dx < 0.01f) dx = 0.01f;
            if (dy < 0.01f) dy = 0.01f;
            minX -= dx * MarginFraction;
            maxX += dx * MarginFraction;
            minY -= dy * MarginFraction;
            maxY += dy * MarginFraction;
        }

        /// <summary>
        /// Masque terre/mer v1_028 — littoral dérivé des données :
        /// composantes d'adjacence terrestre → alpha-shape Delaunay (arêtes ≤ seuil médian) →
        /// faces d'adjacence forcées → graines hexagonales → corridors → chenaux → poches.
        /// Zéro union de disques CellRadius.
        /// </summary>
        static bool[] BuildLandMask(
            List<ProvinceView> views,
            float minX, float maxX, float minY, float maxY)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var isLand = new bool[ActiveW * ActiveH];
            var isStraitChannel = new bool[ActiveW * ActiveH];
            var byId = new Dictionary<int, int>(views.Count);
            for (var i = 0; i < views.Count; i++)
                byId[views[i].Id] = i;

            var adjacency = GameDataLoader.LoadProvinceAdjacency();
            BuildLandAndStraitGraphs(
                adjacency, byId,
                out var landNeighbors, out var straitPairs);

            // Faces d'adjacence terrestre (triplets mutuels) — toujours conservées.
            var forcedFaces = BuildForcedLandFaces(landNeighbors);

            var components = BuildLandComponents(views, byId, landNeighbors);
            var hullPixels = 0;
            var hexSeeds = 0;
            var alphaTriangles = 0;

            for (var c = 0; c < components.Count; c++)
            {
                var members = components[c];
                if (members.Count == 1)
                {
                    var v = views[members[0]];
                    hullPixels += StampRegularHex(
                        isLand, v.X, v.Y, CellRadius, minX, maxX, minY, maxY);
                    hexSeeds++;
                    continue;
                }

                if (members.Count == 2)
                {
                    var a = views[members[0]];
                    var b = views[members[1]];
                    StampCapsule(
                        isLand, null,
                        a.X, a.Y, b.X, b.Y,
                        CorridorHalfWidth, true,
                        0f, 1f,
                        minX, maxX, minY, maxY);
                    hullPixels += StampRegularHex(
                        isLand, a.X, a.Y, CellRadius * 0.9f, minX, maxX, minY, maxY);
                    hullPixels += StampRegularHex(
                        isLand, b.X, b.Y, CellRadius * 0.9f, minX, maxX, minY, maxY);
                    hexSeeds += 2;
                    continue;
                }

                var digThreshold = ComputeDigThreshold(members, views);
                var kept = BuildAlphaShapeTriangles(
                    members, views, digThreshold, forcedFaces, byId);
                alphaTriangles += kept.Count;
                for (var t = 0; t < kept.Count; t++)
                {
                    var tri = kept[t];
                    hullPixels += StampTriangle(
                        isLand,
                        views[members[tri.A]].X, views[members[tri.A]].Y,
                        views[members[tri.B]].X, views[members[tri.B]].Y,
                        views[members[tri.C]].X, views[members[tri.C]].Y,
                        minX, maxX, minY, maxY);
                }

                // Graines hexagonales anguleuses (corps provincial, pas des disques).
                for (var m = 0; m < members.Count; m++)
                {
                    var v = views[members[m]];
                    hullPixels += StampRegularHex(
                        isLand, v.X, v.Y, CellRadius * 0.85f, minX, maxX, minY, maxY);
                    hexSeeds++;
                }
            }

            // Corridors additifs — garantit qu'une adjacence terrestre peut partager une frontière.
            var drawnLand = new HashSet<long>();
            for (var a = 0; a < adjacency.Count; a++)
            {
                var def = adjacency[a];
                if (!byId.ContainsKey(def.id) || def.neighbors == null)
                    continue;
                for (var n = 0; n < def.neighbors.Count; n++)
                {
                    var other = def.neighbors[n];
                    var key = EdgeKey(def.id, other);
                    if (!drawnLand.Add(key))
                        continue;
                    if (!byId.TryGetValue(other, out var j))
                        continue;
                    var i = byId[def.id];
                    StampCapsule(
                        isLand, null,
                        views[i].X, views[i].Y, views[j].X, views[j].Y,
                        CorridorHalfWidth, true,
                        0f, 1f,
                        minX, maxX, minY, maxY);
                }
            }

            // Triangles terrestres (faces d'adjacence) — déjà inclus via forcedFaces ;
            // on rappelle FillLandTriangles pour compter / combler d'éventuels ratés.
            var trianglePixels = FillLandTriangles(
                isLand, views, byId, adjacency, minX, maxX, minY, maxY, out var triangleCount);

            // Chenaux soustractifs (détroits).
            var drawnStrait = new HashSet<long>();
            for (var s = 0; s < straitPairs.Count; s++)
            {
                var pair = straitPairs[s];
                var key = EdgeKey(pair.Item1, pair.Item2);
                if (!drawnStrait.Add(key))
                    continue;
                if (!byId.TryGetValue(pair.Item1, out var i) || !byId.TryGetValue(pair.Item2, out var j))
                    continue;
                StampCapsule(
                    isLand, isStraitChannel,
                    views[i].X, views[i].Y, views[j].X, views[j].Y,
                    StraitHalfWidth, false,
                    StraitEndInset, 1f - StraitEndInset,
                    minX, maxX, minY, maxY);
            }

            var pocketPixels = CloseEnclosedSeaPockets(isLand, isStraitChannel);
            sw.Stop();

            LastMaskStats = new MaskBuildStats
            {
                TriangleCount = triangleCount + alphaTriangles,
                TrianglePixelsAdded = trianglePixels,
                PocketPixelsAdded = pocketPixels,
                HullPixelsAdded = hullPixels,
                HexSeedCount = hexSeeds,
                ComponentCount = components.Count,
                BuildMilliseconds = sw.Elapsed.TotalMilliseconds,
                Mode = LandMaskMode.DataDerived
            };
            Debug.Log($"MapSnapshotExporter: {FormatMaskStatsLine()}");
            return isLand;
        }

        struct AlphaTri
        {
            public int A, B, C;
            public AlphaTri(int a, int b, int c)
            {
                A = a; B = b; C = c;
            }
        }

        static HashSet<long> BuildForcedLandFaces(Dictionary<int, HashSet<int>> landNeighbors)
        {
            var forced = new HashSet<long>();
            var ids = new List<int>(landNeighbors.Keys);
            ids.Sort();
            for (var ai = 0; ai < ids.Count; ai++)
            {
                var a = ids[ai];
                if (!landNeighbors.TryGetValue(a, out var na))
                    continue;
                var neighA = new List<int>(na);
                neighA.Sort();
                for (var bi = 0; bi < neighA.Count; bi++)
                {
                    var b = neighA[bi];
                    if (b <= a) continue;
                    if (!landNeighbors.TryGetValue(b, out var nb)) continue;
                    for (var ci = 0; ci < neighA.Count; ci++)
                    {
                        var c = neighA[ci];
                        if (c <= b) continue;
                        if (!nb.Contains(c)) continue;
                        forced.Add(FaceKey(a, b, c));
                    }
                }
            }

            return forced;
        }

        static long FaceKey(int a, int b, int c)
        {
            // a < b < c
            if (a > b) { var t = a; a = b; b = t; }
            if (b > c) { var t = b; b = c; c = t; }
            if (a > b) { var t = a; a = b; b = t; }
            return ((long)a << 42) | ((long)b << 21) | (uint)c;
        }

        /// <summary>
        /// Alpha-shape : Delaunay de la composante, conserve les triangles dont
        /// l'arête max ≤ seuil (dérivé des adjacences) OU face d'adjacence forcée.
        /// Indices A/B/C relatifs au tableau local members.
        /// </summary>
        static List<AlphaTri> BuildAlphaShapeTriangles(
            List<int> members,
            List<ProvinceView> views,
            float maxEdgeLen,
            HashSet<long> forcedFaces,
            Dictionary<int, int> byId)
        {
            var n = members.Count;
            var xs = new float[n];
            var ys = new float[n];
            var ids = new int[n];
            for (var i = 0; i < n; i++)
            {
                var v = views[members[i]];
                xs[i] = v.X;
                ys[i] = v.Y;
                ids[i] = v.Id;
            }

            var delaunay = BowyerWatson(xs, ys);
            var kept = new List<AlphaTri>(delaunay.Count);
            var seen = new HashSet<long>();

            for (var t = 0; t < delaunay.Count; t++)
            {
                var tri = delaunay[t];
                if (tri.A < 0 || tri.B < 0 || tri.C < 0)
                    continue;
                if (tri.A >= n || tri.B >= n || tri.C >= n)
                    continue;

                var dx = xs[tri.A] - xs[tri.B];
                var dy = ys[tri.A] - ys[tri.B];
                var eAB = dx * dx + dy * dy;
                dx = xs[tri.B] - xs[tri.C];
                dy = ys[tri.B] - ys[tri.C];
                var eBC = dx * dx + dy * dy;
                dx = xs[tri.C] - xs[tri.A];
                dy = ys[tri.C] - ys[tri.A];
                var eCA = dx * dx + dy * dy;
                var maxE = eAB;
                if (eBC > maxE) maxE = eBC;
                if (eCA > maxE) maxE = eCA;
                var maxEdge = (float)Math.Sqrt(maxE);

                var face = FaceKey(ids[tri.A], ids[tri.B], ids[tri.C]);
                var keep = maxEdge <= maxEdgeLen || forcedFaces.Contains(face);
                if (!keep)
                    continue;
                if (!seen.Add(face))
                    continue;
                kept.Add(new AlphaTri(tri.A, tri.B, tri.C));
            }

            // Garantir les faces forcées même si absentes du Delaunay filtré.
            foreach (var face in forcedFaces)
            {
                if (!seen.Add(face))
                    continue;
                var a = (int)((face >> 42) & 0x1FFFFF);
                var b = (int)((face >> 21) & 0x1FFFFF);
                var c = (int)(face & 0x1FFFFF);
                if (!byId.ContainsKey(a) || !byId.ContainsKey(b) || !byId.ContainsKey(c))
                    continue;
                // Map province Id → index local members
                var la = -1; var lb = -1; var lc = -1;
                for (var i = 0; i < n; i++)
                {
                    if (ids[i] == a) la = i;
                    else if (ids[i] == b) lb = i;
                    else if (ids[i] == c) lc = i;
                }

                if (la < 0 || lb < 0 || lc < 0)
                    continue;
                kept.Add(new AlphaTri(la, lb, lc));
            }

            return kept;
        }

        /// <summary>Triangulation de Delaunay (Bowyer–Watson), déterministe (ordre des points fixe).</summary>
        static List<AlphaTri> BowyerWatson(float[] xs, float[] ys)
        {
            var n = xs.Length;
            var tris = new List<AlphaTri>();
            if (n < 3)
                return tris;

            // Super-triangle englobant.
            var minX = xs[0]; var maxX = xs[0];
            var minY = ys[0]; var maxY = ys[0];
            for (var i = 1; i < n; i++)
            {
                if (xs[i] < minX) minX = xs[i];
                if (xs[i] > maxX) maxX = xs[i];
                if (ys[i] < minY) minY = ys[i];
                if (ys[i] > maxY) maxY = ys[i];
            }

            var dx = maxX - minX;
            var dy = maxY - minY;
            if (dx < 1f) dx = 1f;
            if (dy < 1f) dy = 1f;
            var midX = (minX + maxX) * 0.5f;
            var midY = (minY + maxY) * 0.5f;
            var s0 = n;
            var s1 = n + 1;
            var s2 = n + 2;
            var sx = new float[n + 3];
            var sy = new float[n + 3];
            for (var i = 0; i < n; i++)
            {
                sx[i] = xs[i];
                sy[i] = ys[i];
            }

            sx[s0] = midX - 2f * dx - dy;
            sy[s0] = midY - dy;
            sx[s1] = midX;
            sy[s1] = midY + 2f * dy + dx;
            sx[s2] = midX + 2f * dx + dy;
            sy[s2] = midY - dy;
            tris.Add(new AlphaTri(s0, s1, s2));

            for (var p = 0; p < n; p++)
            {
                var bad = new List<int>();
                for (var t = 0; t < tris.Count; t++)
                {
                    if (InCircumcircle(sx, sy, tris[t], sx[p], sy[p]))
                        bad.Add(t);
                }

                // Arêtes du polygone cavité = arêtes de bad non partagées.
                var edgeCount = new Dictionary<long, int>();
                for (var bi = 0; bi < bad.Count; bi++)
                {
                    var tri = tris[bad[bi]];
                    AddEdgeCount(edgeCount, tri.A, tri.B);
                    AddEdgeCount(edgeCount, tri.B, tri.C);
                    AddEdgeCount(edgeCount, tri.C, tri.A);
                }

                // Supprimer bad (du plus grand index au plus petit).
                bad.Sort();
                for (var bi = bad.Count - 1; bi >= 0; bi--)
                    tris.RemoveAt(bad[bi]);

                foreach (var kv in edgeCount)
                {
                    if (kv.Value != 1)
                        continue;
                    var lo = (int)(kv.Key >> 32);
                    var hi = (int)(kv.Key & 0xFFFFFFFF);
                    tris.Add(new AlphaTri(lo, hi, p));
                }
            }

            // Retirer tout triangle touchant le super-triangle.
            var result = new List<AlphaTri>(tris.Count);
            for (var t = 0; t < tris.Count; t++)
            {
                var tri = tris[t];
                if (tri.A >= n || tri.B >= n || tri.C >= n)
                    continue;
                result.Add(tri);
            }

            return result;
        }

        static void AddEdgeCount(Dictionary<long, int> edgeCount, int a, int b)
        {
            var lo = a < b ? a : b;
            var hi = a < b ? b : a;
            var key = ((long)lo << 32) | (uint)hi;
            edgeCount.TryGetValue(key, out var c);
            edgeCount[key] = c + 1;
        }

        static bool InCircumcircle(float[] sx, float[] sy, AlphaTri tri, float px, float py)
        {
            var ax = sx[tri.A]; var ay = sy[tri.A];
            var bx = sx[tri.B]; var by = sy[tri.B];
            var cx = sx[tri.C]; var cy = sy[tri.C];

            var a2 = ax * ax + ay * ay;
            var b2 = bx * bx + by * by;
            var c2 = cx * cx + cy * cy;
            var d = 2f * (ax * (by - cy) + bx * (cy - ay) + cx * (ay - by));
            if (Math.Abs(d) < 1e-12f)
                return false;

            var ux = (a2 * (by - cy) + b2 * (cy - ay) + c2 * (ay - by)) / d;
            var uy = (a2 * (cx - bx) + b2 * (ax - cx) + c2 * (bx - ax)) / d;
            var r2 = (ux - ax) * (ux - ax) + (uy - ay) * (uy - ay);
            var dx = px - ux;
            var dy = py - uy;
            return dx * dx + dy * dy <= r2 * 1.000001f;
        }

        /// <summary>
        /// Ancien masque (référence PARTIE 1) : disques → corridors → triangles → chenaux → poches.
        /// </summary>
        static bool[] BuildLandMaskLegacyDisks(
            List<ProvinceView> views,
            float minX, float maxX, float minY, float maxY)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var isLand = new bool[ActiveW * ActiveH];
            var isStraitChannel = new bool[ActiveW * ActiveH];
            var byId = new Dictionary<int, int>(views.Count);
            for (var i = 0; i < views.Count; i++)
                byId[views[i].Id] = i;

            for (var i = 0; i < views.Count; i++)
                StampDisk(isLand, views[i].X, views[i].Y, CellRadius, true, minX, maxX, minY, maxY);

            var adjacency = GameDataLoader.LoadProvinceAdjacency();
            var drawnLand = new HashSet<long>();
            var drawnStrait = new HashSet<long>();

            for (var a = 0; a < adjacency.Count; a++)
            {
                var def = adjacency[a];
                if (!byId.ContainsKey(def.id) || def.neighbors == null)
                    continue;
                for (var n = 0; n < def.neighbors.Count; n++)
                {
                    var other = def.neighbors[n];
                    var key = EdgeKey(def.id, other);
                    if (!drawnLand.Add(key))
                        continue;
                    if (!byId.TryGetValue(other, out var j))
                        continue;
                    var i = byId[def.id];
                    StampCapsule(
                        isLand, null,
                        views[i].X, views[i].Y, views[j].X, views[j].Y,
                        CorridorHalfWidth, true,
                        0f, 1f,
                        minX, maxX, minY, maxY);
                }
            }

            var trianglePixels = FillLandTriangles(
                isLand, views, byId, adjacency, minX, maxX, minY, maxY, out var triangleCount);

            for (var a = 0; a < adjacency.Count; a++)
            {
                var def = adjacency[a];
                if (!byId.ContainsKey(def.id) || def.straits == null)
                    continue;
                for (var s = 0; s < def.straits.Count; s++)
                {
                    var other = def.straits[s];
                    var key = EdgeKey(def.id, other);
                    if (!drawnStrait.Add(key))
                        continue;
                    if (!byId.TryGetValue(other, out var j))
                        continue;
                    var i = byId[def.id];
                    StampCapsule(
                        isLand, isStraitChannel,
                        views[i].X, views[i].Y, views[j].X, views[j].Y,
                        StraitHalfWidth, false,
                        StraitEndInset, 1f - StraitEndInset,
                        minX, maxX, minY, maxY);
                }
            }

            var pocketPixels = CloseEnclosedSeaPockets(isLand, isStraitChannel);
            sw.Stop();

            LastMaskStats = new MaskBuildStats
            {
                TriangleCount = triangleCount,
                TrianglePixelsAdded = trianglePixels,
                PocketPixelsAdded = pocketPixels,
                HullPixelsAdded = 0,
                HexSeedCount = 0,
                ComponentCount = 0,
                BuildMilliseconds = sw.Elapsed.TotalMilliseconds,
                Mode = LandMaskMode.LegacyDisks
            };
            Debug.Log($"MapSnapshotExporter: {FormatMaskStatsLine()}");
            return isLand;
        }

        static void BuildLandAndStraitGraphs(
            List<GameDataLoader.ProvinceAdjacencyDefinition> adjacency,
            Dictionary<int, int> byId,
            out Dictionary<int, HashSet<int>> landNeighbors,
            out List<(int, int)> straitPairs)
        {
            landNeighbors = new Dictionary<int, HashSet<int>>();
            straitPairs = new List<(int, int)>();

            for (var a = 0; a < adjacency.Count; a++)
            {
                var def = adjacency[a];
                if (!byId.ContainsKey(def.id))
                    continue;

                if (def.neighbors != null)
                {
                    for (var n = 0; n < def.neighbors.Count; n++)
                    {
                        var other = def.neighbors[n];
                        if (!byId.ContainsKey(other))
                            continue;
                        if (!landNeighbors.TryGetValue(def.id, out var set))
                        {
                            set = new HashSet<int>();
                            landNeighbors[def.id] = set;
                        }
                        set.Add(other);
                        if (!landNeighbors.TryGetValue(other, out var setB))
                        {
                            setB = new HashSet<int>();
                            landNeighbors[other] = setB;
                        }
                        setB.Add(def.id);
                    }
                }

                if (def.straits != null)
                {
                    for (var s = 0; s < def.straits.Count; s++)
                    {
                        var other = def.straits[s];
                        if (!byId.ContainsKey(other))
                            continue;
                        if (def.id < other)
                            straitPairs.Add((def.id, other));
                    }
                }
            }
        }

        static List<List<int>> BuildLandComponents(
            List<ProvinceView> views,
            Dictionary<int, int> byId,
            Dictionary<int, HashSet<int>> landNeighbors)
        {
            var visited = new bool[views.Count];
            var components = new List<List<int>>();
            var stack = new List<int>(views.Count);

            for (var start = 0; start < views.Count; start++)
            {
                if (visited[start])
                    continue;
                stack.Clear();
                stack.Add(start);
                visited[start] = true;
                var members = new List<int>();

                while (stack.Count > 0)
                {
                    var idx = stack[stack.Count - 1];
                    stack.RemoveAt(stack.Count - 1);
                    members.Add(idx);
                    var id = views[idx].Id;
                    if (!landNeighbors.TryGetValue(id, out var neigh))
                        continue;
                    foreach (var oid in neigh)
                    {
                        if (!byId.TryGetValue(oid, out var j) || visited[j])
                            continue;
                        visited[j] = true;
                        stack.Add(j);
                    }
                }

                members.Sort((a, b) => views[a].Id.CompareTo(views[b].Id));
                components.Add(members);
            }

            components.Sort((a, b) =>
            {
                var cmp = b.Count.CompareTo(a.Count);
                if (cmp != 0) return cmp;
                return views[a[0]].Id.CompareTo(views[b[0]].Id);
            });
            return components;
        }

        static float ComputeDigThreshold(
            List<int> members,
            List<ProvinceView> views)
        {
            var lengths = new List<float>();
            var memberIds = new HashSet<int>();
            for (var i = 0; i < members.Count; i++)
                memberIds.Add(views[members[i]].Id);

            var adjacency = GameDataLoader.LoadProvinceAdjacency();
            var byViewId = new Dictionary<int, ProvinceView>(views.Count);
            for (var i = 0; i < views.Count; i++)
                byViewId[views[i].Id] = views[i];

            for (var a = 0; a < adjacency.Count; a++)
            {
                var def = adjacency[a];
                if (!memberIds.Contains(def.id) || def.neighbors == null)
                    continue;
                if (!byViewId.TryGetValue(def.id, out var va))
                    continue;
                for (var n = 0; n < def.neighbors.Count; n++)
                {
                    var other = def.neighbors[n];
                    if (def.id >= other || !memberIds.Contains(other))
                        continue;
                    if (!byViewId.TryGetValue(other, out var vb))
                        continue;
                    var dx = va.X - vb.X;
                    var dy = va.Y - vb.Y;
                    lengths.Add((float)Math.Sqrt(dx * dx + dy * dy));
                }
            }

            if (lengths.Count == 0)
                return CellRadius * 3f;

            lengths.Sort();
            var median = lengths[lengths.Count / 2];
            return median * ConcaveHullDigFactor;
        }

        /// <summary>Remplit un polygone (ray casting) — compte les pixels mer→terre.</summary>
        static int StampPolygon(
            bool[] isLand,
            List<(float X, float Y)> poly,
            float minX, float maxX, float minY, float maxY)
        {
            if (poly == null || poly.Count < 3)
                return 0;

            var rangeX = maxX - minX;
            var rangeY = maxY - minY;
            var minPx = float.MaxValue;
            var maxPx = float.MinValue;
            var minPy = float.MaxValue;
            var maxPy = float.MinValue;
            for (var i = 0; i < poly.Count; i++)
            {
                if (poly[i].X < minPx) minPx = poly[i].X;
                if (poly[i].X > maxPx) maxPx = poly[i].X;
                if (poly[i].Y < minPy) minPy = poly[i].Y;
                if (poly[i].Y > maxPy) maxPy = poly[i].Y;
            }

            WorldToPixel(minPx, maxPy, minX, maxX, minY, maxY, out var x0, out var y0);
            WorldToPixel(maxPx, minPy, minX, maxX, minY, maxY, out var x1, out var y1);
            if (x0 > x1) { var t = x0; x0 = x1; x1 = t; }
            if (y0 > y1) { var t = y0; y0 = y1; y1 = t; }
            x0 = Math.Max(0, x0 - 1);
            y0 = Math.Max(0, y0 - 1);
            x1 = Math.Min(ActiveW - 1, x1 + 1);
            y1 = Math.Min(ActiveH - 1, y1 + 1);

            var added = 0;
            for (var py = y0; py <= y1; py++)
            {
                var wy = minY + (py + 0.5f) / ActiveH * rangeY;
                for (var px = x0; px <= x1; px++)
                {
                    var wx = minX + (px + 0.5f) / ActiveW * rangeX;
                    if (!PointInPolygon(wx, wy, poly))
                        continue;
                    var idx = py * ActiveW + px;
                    if (!isLand[idx])
                    {
                        isLand[idx] = true;
                        added++;
                    }
                }
            }

            return added;
        }

        static bool PointInPolygon(float x, float y, List<(float X, float Y)> poly)
        {
            var inside = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                var yi = poly[i].Y;
                var yj = poly[j].Y;
                var xi = poly[i].X;
                var xj = poly[j].X;
                var intersect = ((yi > y) != (yj > y)) &&
                    (x < (xj - xi) * (y - yi) / ((yj - yi) + 1e-12f) + xi);
                if (intersect)
                    inside = !inside;
            }

            return inside;
        }

        /// <summary>Hexagone régulier pointu (angles 30°+k×60°) — forme anguleuse, pas un disque.</summary>
        static int StampRegularHex(
            bool[] isLand,
            float cx, float cy, float radius,
            float minX, float maxX, float minY, float maxY)
        {
            var poly = new List<(float X, float Y)>(6);
            for (var k = 0; k < 6; k++)
            {
                var ang = (30f + k * 60f) * (float)Math.PI / 180f;
                poly.Add((cx + radius * (float)Math.Cos(ang), cy + radius * (float)Math.Sin(ang)));
            }

            return StampPolygon(isLand, poly, minX, maxX, minY, maxY);
        }

        /// <summary>
        /// Mesure la cohérence raster ↔ données : côte is_coastal, adjacences terrestres, détroits.
        /// </summary>
        public static ShapeCoherenceReport MeasureShapeCoherence(
            List<ProvinceView> views,
            bool[] isLand,
            int[] provinceAt)
        {
            var provinces = GameDataLoader.LoadProvinces();
            var coastalById = new Dictionary<int, bool>(provinces.Count);
            var nameById = new Dictionary<int, string>(provinces.Count);
            for (var i = 0; i < provinces.Count; i++)
            {
                coastalById[provinces[i].id] = provinces[i].is_coastal;
                nameById[provinces[i].id] = string.IsNullOrEmpty(provinces[i].name)
                    ? ("id=" + provinces[i].id)
                    : provinces[i].name;
            }

            var touchesSea = new bool[views.Count];
            for (var idx = 0; idx < isLand.Length; idx++)
            {
                if (!isLand[idx])
                    continue;
                var vi = provinceAt[idx];
                if (vi < 0 || vi >= views.Count)
                    continue;
                var px = idx % ActiveW;
                var py = idx / ActiveW;
                if ((px > 0 && !isLand[idx - 1]) ||
                    (px + 1 < ActiveW && !isLand[idx + 1]) ||
                    (py > 0 && !isLand[idx - ActiveW]) ||
                    (py + 1 < ActiveH && !isLand[idx + ActiveW]))
                    touchesSea[vi] = true;
            }

            var coastalDeclared = 0;
            var inlandDeclared = 0;
            var coastalTouch = 0;
            var inlandTouch = 0;
            var coastalMissing = new List<string>();
            var inlandFalse = new List<string>();

            for (var i = 0; i < views.Count; i++)
            {
                var id = views[i].Id;
                coastalById.TryGetValue(id, out var isCoastal);
                nameById.TryGetValue(id, out var nm);
                if (nm == null) nm = "id=" + id;
                var label = nm + "(id=" + id + ")";

                if (isCoastal)
                {
                    coastalDeclared++;
                    if (touchesSea[i])
                        coastalTouch++;
                    else
                        coastalMissing.Add(label);
                }
                else
                {
                    inlandDeclared++;
                    if (touchesSea[i])
                    {
                        inlandTouch++;
                        inlandFalse.Add(label);
                    }
                }
            }

            coastalMissing.Sort(StringComparer.Ordinal);
            inlandFalse.Sort(StringComparer.Ordinal);

            // Frontières partagées entre paires de provinces (clés = EdgeKey des ProvinceId).
            var shareBorder = new HashSet<long>();
            for (var idx = 0; idx < isLand.Length; idx++)
            {
                if (!isLand[idx])
                    continue;
                var vi = provinceAt[idx];
                if (vi < 0 || vi >= views.Count)
                    continue;
                var idA = views[vi].Id;
                var px = idx % ActiveW;
                var py = idx / ActiveW;
                RegisterBorder(shareBorder, views, provinceAt, isLand, idA, px + 1, py);
                RegisterBorder(shareBorder, views, provinceAt, isLand, idA, px, py + 1);
            }

            var adjacency = GameDataLoader.LoadProvinceAdjacency();
            var byId = new Dictionary<int, int>(views.Count);
            for (var i = 0; i < views.Count; i++)
                byId[views[i].Id] = i;

            var landPairs = 0;
            var landShare = 0;
            var landMissing = new List<string>();
            var seenLand = new HashSet<long>();
            var straitPairs = 0;
            var straitGlued = 0;
            var straitGluedNames = new List<string>();
            var seenStrait = new HashSet<long>();

            for (var a = 0; a < adjacency.Count; a++)
            {
                var def = adjacency[a];
                if (!byId.ContainsKey(def.id))
                    continue;
                nameById.TryGetValue(def.id, out var na);
                if (na == null) na = "id=" + def.id;

                if (def.neighbors != null)
                {
                    for (var n = 0; n < def.neighbors.Count; n++)
                    {
                        var other = def.neighbors[n];
                        var key = EdgeKey(def.id, other);
                        if (!seenLand.Add(key) || !byId.ContainsKey(other))
                            continue;
                        landPairs++;
                        if (shareBorder.Contains(key))
                            landShare++;
                        else
                        {
                            nameById.TryGetValue(other, out var nb);
                            if (nb == null) nb = "id=" + other;
                            landMissing.Add(na + "(id=" + def.id + ")<->" + nb + "(id=" + other + ")");
                        }
                    }
                }

                if (def.straits != null)
                {
                    for (var s = 0; s < def.straits.Count; s++)
                    {
                        var other = def.straits[s];
                        var key = EdgeKey(def.id, other);
                        if (!seenStrait.Add(key) || !byId.ContainsKey(other))
                            continue;
                        straitPairs++;
                        if (shareBorder.Contains(key))
                        {
                            straitGlued++;
                            nameById.TryGetValue(other, out var nb);
                            if (nb == null) nb = "id=" + other;
                            straitGluedNames.Add(na + "(id=" + def.id + ")<->" + nb + "(id=" + other + ")");
                        }
                    }
                }
            }

            landMissing.Sort(StringComparer.Ordinal);
            straitGluedNames.Sort(StringComparer.Ordinal);

            var report = new ShapeCoherenceReport
            {
                CoastalDeclared = coastalDeclared,
                InlandDeclared = inlandDeclared,
                CoastalTouchingSea = coastalTouch,
                InlandTouchingSea = inlandTouch,
                LandAdjacencyPairs = landPairs,
                LandAdjacencySharingBorder = landShare,
                StraitPairs = straitPairs,
                StraitPairsGlued = straitGlued,
                CoastalMissingSea = coastalMissing.ToArray(),
                InlandFalseSea = inlandFalse.ToArray(),
                LandAdjMissingBorder = landMissing.ToArray(),
                StraitGluedNames = straitGluedNames.ToArray(),
                Summary = null
            };
            report.Summary = FormatShapeReportLine(report);
            LastShapeReport = report;
            Debug.Log($"MapSnapshotExporter: {report.Summary}");
            return report;
        }

        static void RegisterBorder(
            HashSet<long> shareBorder,
            List<ProvinceView> views,
            int[] provinceAt,
            bool[] isLand,
            int idA,
            int nx, int ny)
        {
            if (nx < 0 || ny < 0 || nx >= ActiveW || ny >= ActiveH)
                return;
            var nidx = ny * ActiveW + nx;
            if (!isLand[nidx])
                return;
            var vj = provinceAt[nidx];
            if (vj < 0 || vj >= views.Count)
                return;
            var idB = views[vj].Id;
            if (idA == idB)
                return;
            shareBorder.Add(EdgeKey(idA, idB));
        }

        /// <summary>
        /// Énumère les triangles depuis neighbors (pas de liste en dur) et remplit leur intérieur.
        /// </summary>
        static int FillLandTriangles(
            bool[] isLand,
            List<ProvinceView> views,
            Dictionary<int, int> byId,
            List<GameDataLoader.ProvinceAdjacencyDefinition> adjacency,
            float minX, float maxX, float minY, float maxY,
            out int triangleCount)
        {
            var neighborSets = new Dictionary<int, HashSet<int>>(adjacency.Count);
            for (var a = 0; a < adjacency.Count; a++)
            {
                var def = adjacency[a];
                if (!byId.ContainsKey(def.id))
                    continue;
                if (!neighborSets.TryGetValue(def.id, out var set))
                {
                    set = new HashSet<int>();
                    neighborSets[def.id] = set;
                }

                if (def.neighbors == null)
                    continue;
                for (var n = 0; n < def.neighbors.Count; n++)
                {
                    var other = def.neighbors[n];
                    if (!byId.ContainsKey(other))
                        continue;
                    set.Add(other);
                    if (!neighborSets.TryGetValue(other, out var otherSet))
                    {
                        otherSet = new HashSet<int>();
                        neighborSets[other] = otherSet;
                    }
                    otherSet.Add(def.id);
                }
            }

            var added = 0;
            triangleCount = 0;
            var seen = new HashSet<long>();

            var ids = new List<int>(neighborSets.Keys);
            ids.Sort();
            for (var ai = 0; ai < ids.Count; ai++)
            {
                var a = ids[ai];
                if (!neighborSets.TryGetValue(a, out var na))
                    continue;
                var neighborsA = new List<int>(na);
                neighborsA.Sort();
                for (var bi = 0; bi < neighborsA.Count; bi++)
                {
                    var b = neighborsA[bi];
                    if (b <= a)
                        continue;
                    if (!neighborSets.TryGetValue(b, out var nb))
                        continue;
                    for (var ci = 0; ci < neighborsA.Count; ci++)
                    {
                        var c = neighborsA[ci];
                        if (c <= b)
                            continue;
                        if (!nb.Contains(c))
                            continue;

                        // Clé unique a<b<c
                        var key = ((long)a << 42) | ((long)b << 21) | (uint)c;
                        if (!seen.Add(key))
                            continue;

                        triangleCount++;
                        var ia = byId[a];
                        var ib = byId[b];
                        var ic = byId[c];
                        added += StampTriangle(
                            isLand,
                            views[ia].X, views[ia].Y,
                            views[ib].X, views[ib].Y,
                            views[ic].X, views[ic].Y,
                            minX, maxX, minY, maxY);
                    }
                }
            }

            return added;
        }

        /// <summary>
        /// Rasterise un triangle (boîte englobante + signes des produits vectoriels).
        /// Compte les pixels passés de mer → terre.
        /// </summary>
        static int StampTriangle(
            bool[] isLand,
            float ax, float ay, float bx, float by, float cx, float cy,
            float minX, float maxX, float minY, float maxY)
        {
            var rangeX = maxX - minX;
            var rangeY = maxY - minY;

            WorldToPixel(ax, ay, minX, maxX, minY, maxY, out var pax, out var pay);
            WorldToPixel(bx, by, minX, maxX, minY, maxY, out var pbx, out var pby);
            WorldToPixel(cx, cy, minX, maxX, minY, maxY, out var pcx, out var pcy);

            var x0 = Math.Max(0, Math.Min(pax, Math.Min(pbx, pcx)) - 1);
            var x1 = Math.Min(ActiveW - 1, Math.Max(pax, Math.Max(pbx, pcx)) + 1);
            var y0 = Math.Max(0, Math.Min(pay, Math.Min(pby, pcy)) - 1);
            var y1 = Math.Min(ActiveH - 1, Math.Max(pay, Math.Max(pby, pcy)) + 1);

            // Orientation : signe de (B-A)×(C-A)
            var area2 = (bx - ax) * (cy - ay) - (by - ay) * (cx - ax);
            if (Math.Abs(area2) < 1e-12f)
                return 0;

            var added = 0;
            for (var py = y0; py <= y1; py++)
            {
                var wy = minY + (py + 0.5f) / ActiveH * rangeY;
                for (var px = x0; px <= x1; px++)
                {
                    var wx = minX + (px + 0.5f) / ActiveW * rangeX;
                    var ab = (bx - ax) * (wy - ay) - (by - ay) * (wx - ax);
                    var bc = (cx - bx) * (wy - by) - (cy - by) * (wx - bx);
                    var ca = (ax - cx) * (wy - cy) - (ay - cy) * (wx - cx);

                    bool inside;
                    if (area2 > 0f)
                        inside = ab >= 0f && bc >= 0f && ca >= 0f;
                    else
                        inside = ab <= 0f && bc <= 0f && ca <= 0f;

                    if (!inside)
                        continue;

                    var idx = py * ActiveW + px;
                    if (!isLand[idx])
                    {
                        isLand[idx] = true;
                        added++;
                    }
                }
            }

            return added;
        }

        /// <summary>
        /// Flood-fill mer depuis le bord ; toute mer non atteinte et hors chenal → terre.
        /// Les pixels isStraitChannel restent mer (piège Bosphore).
        /// </summary>
        static int CloseEnclosedSeaPockets(bool[] isLand, bool[] isStraitChannel)
        {
            var reachableSea = new bool[ActiveW * ActiveH];
            var stack = new int[ActiveW * ActiveH];
            var top = 0;

            void TryPush(int px, int py)
            {
                if (px < 0 || py < 0 || px >= ActiveW || py >= ActiveH)
                    return;
                var idx = py * ActiveW + px;
                if (isLand[idx] || reachableSea[idx])
                    return;
                reachableSea[idx] = true;
                stack[top++] = idx;
            }

            // Graines : tous les pixels de mer sur le bord de l'image
            for (var x = 0; x < ActiveW; x++)
            {
                TryPush(x, 0);
                TryPush(x, ActiveH - 1);
            }
            for (var y = 0; y < ActiveH; y++)
            {
                TryPush(0, y);
                TryPush(ActiveW - 1, y);
            }

            while (top > 0)
            {
                var idx = stack[--top];
                var px = idx % ActiveW;
                var py = idx / ActiveW;
                TryPush(px + 1, py);
                TryPush(px - 1, py);
                TryPush(px, py + 1);
                TryPush(px, py - 1);
            }

            var added = 0;
            for (var i = 0; i < isLand.Length; i++)
            {
                if (isLand[i] || reachableSea[i])
                    continue;
                // Poche enclavée — mais JAMAIS un chenal de détroit
                if (isStraitChannel[i])
                    continue;
                isLand[i] = true;
                added++;
            }

            return added;
        }

        static void StampDisk(
            bool[] isLand,
            float cx, float cy, float radius, bool landValue,
            float minX, float maxX, float minY, float maxY)
        {
            var rangeX = maxX - minX;
            var rangeY = maxY - minY;
            var padX = (int)(radius / rangeX * ActiveW) + 2;
            var padY = (int)(radius / rangeY * ActiveH) + 2;
            WorldToPixel(cx, cy, minX, maxX, minY, maxY, out var pcx, out var pcy);
            var r2 = radius * radius;

            var y0 = Math.Max(0, pcy - padY);
            var y1 = Math.Min(ActiveH - 1, pcy + padY);
            var x0 = Math.Max(0, pcx - padX);
            var x1 = Math.Min(ActiveW - 1, pcx + padX);

            for (var py = y0; py <= y1; py++)
            {
                var wy = minY + (py + 0.5f) / ActiveH * rangeY;
                for (var px = x0; px <= x1; px++)
                {
                    var wx = minX + (px + 0.5f) / ActiveW * rangeX;
                    var dx = wx - cx;
                    var dy = wy - cy;
                    if (dx * dx + dy * dy <= r2)
                        isLand[py * ActiveW + px] = landValue;
                }
            }
        }

        static void StampCapsule(
            bool[] isLand,
            bool[] markChannel,
            float x0, float y0, float x1, float y1,
            float halfWidth, bool landValue,
            float tMin, float tMax,
            float minX, float maxX, float minY, float maxY)
        {
            var rangeX = maxX - minX;
            var rangeY = maxY - minY;
            var padX = (int)(halfWidth / rangeX * ActiveW) + 2;
            var padY = (int)(halfWidth / rangeY * ActiveH) + 2;

            WorldToPixel(x0, y0, minX, maxX, minY, maxY, out var px0, out var py0);
            WorldToPixel(x1, y1, minX, maxX, minY, maxY, out var px1, out var py1);

            var bx0 = Math.Max(0, Math.Min(px0, px1) - padX);
            var bx1 = Math.Min(ActiveW - 1, Math.Max(px0, px1) + padX);
            var by0 = Math.Max(0, Math.Min(py0, py1) - padY);
            var by1 = Math.Min(ActiveH - 1, Math.Max(py0, py1) + padY);

            var sdx = x1 - x0;
            var sdy = y1 - y0;
            var lenSq = sdx * sdx + sdy * sdy;
            var hw2 = halfWidth * halfWidth;

            for (var py = by0; py <= by1; py++)
            {
                var wy = minY + (py + 0.5f) / ActiveH * rangeY;
                for (var px = bx0; px <= bx1; px++)
                {
                    var wx = minX + (px + 0.5f) / ActiveW * rangeX;
                    float t;
                    if (lenSq < 1e-12f)
                        t = 0f;
                    else
                    {
                        t = ((wx - x0) * sdx + (wy - y0) * sdy) / lenSq;
                        if (t < 0f) t = 0f;
                        else if (t > 1f) t = 1f;
                    }

                    if (t < tMin || t > tMax)
                        continue;

                    var qx = x0 + t * sdx;
                    var qy = y0 + t * sdy;
                    var ex = wx - qx;
                    var ey = wy - qy;
                    if (ex * ex + ey * ey <= hw2)
                    {
                        var idx = py * ActiveW + px;
                        isLand[idx] = landValue;
                        if (markChannel != null)
                            markChannel[idx] = true;
                    }
                }
            }
        }

        static void FillVoronoi(
            List<ProvinceView> views,
            Color32[] pixels,
            int[] provinceAt,
            bool[] isLand,
            Color32 sea,
            float minX, float maxX, float minY, float maxY)
        {
            var rangeX = maxX - minX;
            var rangeY = maxY - minY;

            for (var py = 0; py < ActiveH; py++)
            {
                var wy = minY + (py + 0.5f) / ActiveH * rangeY;
                for (var px = 0; px < ActiveW; px++)
                {
                    var idx = py * ActiveW + px;
                    if (!isLand[idx])
                    {
                        pixels[idx] = sea;
                        provinceAt[idx] = -1;
                        continue;
                    }

                    var wx = minX + (px + 0.5f) / ActiveW * rangeX;
                    var best = -1;
                    var bestDistSq = float.MaxValue;

                    for (var i = 0; i < views.Count; i++)
                    {
                        var dx = wx - views[i].X;
                        var dy = wy - views[i].Y;
                        var d2 = dx * dx + dy * dy;
                        if (d2 < bestDistSq)
                        {
                            bestDistSq = d2;
                            best = i;
                        }
                    }

                    if (best >= 0)
                    {
                        pixels[idx] = views[best].Fill;
                        provinceAt[idx] = best;
                    }
                    else
                    {
                        pixels[idx] = sea;
                        provinceAt[idx] = -1;
                    }
                }
            }
        }

        /// <summary>
        /// Flood-fill 4-connexe sur les pixels terrestres ; compte les provinces distinctes par composante.
        /// Cible : 7 masses de tailles {39,4,2,2,1,1,1}.
        /// </summary>
        static LandMassReport AnalyzeLandMasses(
            List<ProvinceView> views,
            bool[] isLand,
            int[] provinceAt)
        {
            var visited = new bool[ActiveW * ActiveH];
            var sizes = new List<int>();
            var memberLists = new List<List<int>>();
            var stack = new int[ActiveW * ActiveH];

            for (var start = 0; start < isLand.Length; start++)
            {
                if (!isLand[start] || visited[start])
                    continue;

                var top = 0;
                stack[top++] = start;
                visited[start] = true;
                var members = new HashSet<int>();

                while (top > 0)
                {
                    var idx = stack[--top];
                    var vi = provinceAt[idx];
                    if (vi >= 0)
                        members.Add(views[vi].Id);

                    var px = idx % ActiveW;
                    var py = idx / ActiveW;
                    PushNeighbor(isLand, visited, stack, ref top, px + 1, py);
                    PushNeighbor(isLand, visited, stack, ref top, px - 1, py);
                    PushNeighbor(isLand, visited, stack, ref top, px, py + 1);
                    PushNeighbor(isLand, visited, stack, ref top, px, py - 1);
                }

                sizes.Add(members.Count);
                var sortedIds = new List<int>(members);
                sortedIds.Sort();
                memberLists.Add(sortedIds);
            }

            var order = new int[sizes.Count];
            for (var i = 0; i < order.Length; i++)
                order[i] = i;
            Array.Sort(order, (a, b) => sizes[b].CompareTo(sizes[a]));

            var counts = new int[sizes.Count];
            for (var i = 0; i < order.Length; i++)
                counts[i] = sizes[order[i]];

            var target = new[] { 39, 4, 2, 2, 1, 1, 1 };
            var match = counts.Length == target.Length;
            if (match)
            {
                for (var i = 0; i < target.Length; i++)
                {
                    if (counts[i] != target[i])
                    {
                        match = false;
                        break;
                    }
                }
            }

            var sb = new StringBuilder();
            sb.Append("LANDMASSES count=").Append(counts.Length).Append(" sizes=[");
            for (var i = 0; i < counts.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(counts[i]);
            }
            sb.Append("] target=[39,4,2,2,1,1,1] ");
            sb.Append(match ? "OK" : "FAIL");

            if (!match)
            {
                sb.Append(" | detail:");
                for (var i = 0; i < order.Length; i++)
                {
                    var ids = memberLists[order[i]];
                    sb.Append(" [").Append(counts[i]).Append(':');
                    for (var j = 0; j < ids.Count; j++)
                    {
                        if (j > 0) sb.Append('+');
                        sb.Append(ids[j]);
                    }
                    sb.Append(']');
                }
            }

            var summary = sb.ToString();
            Debug.Log($"MapSnapshotExporter: {summary}");

            return new LandMassReport
            {
                ComponentCount = counts.Length,
                ProvinceCounts = counts,
                MatchesTarget = match,
                Summary = summary
            };
        }

        static void PushNeighbor(
            bool[] isLand, bool[] visited, int[] stack, ref int top, int nx, int ny)
        {
            if (nx < 0 || ny < 0 || nx >= ActiveW || ny >= ActiveH)
                return;
            var nidx = ny * ActiveW + nx;
            if (!isLand[nidx] || visited[nidx])
                return;
            visited[nidx] = true;
            stack[top++] = nidx;
        }

        static void ApplyOccupationHatch(List<ProvinceView> views, Color32[] pixels, int[] provinceAt)
        {
            for (var py = 0; py < ActiveH; py++)
            {
                for (var px = 0; px < ActiveW; px++)
                {
                    var idx = py * ActiveW + px;
                    var vi = provinceAt[idx];
                    if (vi < 0)
                        continue;
                    var v = views[vi];
                    if (!v.Occupied)
                        continue;
                    // Hachures diagonales aux couleurs de l'occupant.
                    if (((px + py) & 3) == 0)
                        pixels[idx] = v.ControllerColor;
                }
            }
        }

        /// <summary>
        /// v1_092 / v1_093 — dessine le front déjà calculé (FrontLineState), motif ApplyOccupationHatch :
        /// passage pixels indexé par provinceAt, zéro géométrie, zéro écriture monde.
        /// Règle de superposition publiée :
        ///   • contesté → damier jaune/brun ((px^py)&1)
        ///   • front non contesté → liseré rouge épais FrontRimThicknessPx (+ halo sombre)
        ///   • occupation prioritaire : pixel hachuré Occupied ((px+py)&3)==0 inchangé
        /// FrontOverlayEnabled=false → no-op (réversible à l'octet).
        /// </summary>
        static void ApplyFrontOverlay(List<ProvinceView> views, Color32[] pixels, int[] provinceAt)
        {
            LastFrontDrawnProvinceIds.Clear();
            LastFrontPixelCount = 0;
            if (!FrontOverlayEnabled || views == null || pixels == null || provinceAt == null)
                return;

            var thickness = FrontRimThicknessPx < 1 ? 1 : FrontRimThicknessPx;
            var drawn = new HashSet<int>();
            var painted = 0;
            for (var py = 0; py < ActiveH; py++)
            {
                for (var px = 0; px < ActiveW; px++)
                {
                    var idx = py * ActiveW + px;
                    var vi = provinceAt[idx];
                    if (vi < 0 || vi >= views.Count)
                        continue;
                    var v = views[vi];
                    if (!v.IsFront)
                        continue;

                    // Occupation prioritaire sur front : laisser le hachurage intact.
                    if (v.Occupied && (((px + py) & 3) == 0))
                        continue;

                    if (v.IsContested)
                    {
                        pixels[idx] = (((px ^ py) & 1) == 0)
                            ? FrontContestedLight
                            : FrontContestedDark;
                        drawn.Add(v.Id);
                        painted++;
                        continue;
                    }

                    // Distance de Chebyshev au premier pixel hors-province.
                    // Bord vrai → edgeDist=1 (voisin immédiat différent) ; jamais 0.
                    var search = thickness >= 2 ? thickness + 1 : thickness;
                    var edgeDist = FrontEdgeDistance(provinceAt, px, py, vi, search);
                    if (edgeDist < 1)
                        continue;
                    if (edgeDist <= thickness)
                    {
                        pixels[idx] = FrontRimColor;
                        drawn.Add(v.Id);
                        painted++;
                    }
                    else if (thickness >= 2 && edgeDist == thickness + 1)
                    {
                        pixels[idx] = FrontRimHalo;
                        drawn.Add(v.Id);
                        painted++;
                    }
                }
            }

            LastFrontPixelCount = painted;
            var ordered = new List<int>(drawn);
            ordered.Sort();
            LastFrontDrawnProvinceIds.AddRange(ordered);
        }

        /// <summary>
        /// Distance de Chebyshev au premier pixel hors-province (bord).
        /// Retourne ≥1 pour un pixel de bord (voisin immédiat différent) ; -1 si hors portée.
        /// </summary>
        static int FrontEdgeDistance(int[] provinceAt, int px, int py, int vi, int maxDist)
        {
            for (var d = 1; d <= maxDist; d++)
            {
                for (var oy = -d; oy <= d; oy++)
                {
                    for (var ox = -d; ox <= d; ox++)
                    {
                        if (ox != -d && ox != d && oy != -d && oy != d)
                            continue;
                        var nx = px + ox;
                        var ny = py + oy;
                        if (nx < 0 || ny < 0 || nx >= ActiveW || ny >= ActiveH)
                            return d;
                        if (provinceAt[ny * ActiveW + nx] != vi)
                            return d;
                    }
                }
            }

            return -1;
        }

        /// <summary>
        /// Lit FrontSectorData / FrontLineState (lecture seule) et annote les ProvinceView.
        /// Aucune écriture ECS. v1_093 : ignore les secteurs dont WarData n'existe plus
        /// ou n'est plus active (motif FrontAdvanceSystem.cs:89-98), sauf si
        /// DebugAnnotateInactiveWarFronts (preuve rouge des fantômes v1_092).
        /// </summary>
        public static void ApplyFrontFlags(EntityManager em, List<ProvinceView> views)
        {
            if (views == null || views.Count == 0)
                return;

            for (var i = 0; i < views.Count; i++)
            {
                var v = views[i];
                v.IsFront = false;
                v.IsContested = false;
                v.AttackerPressure = 0f;
                v.DefenderPressure = 0f;
                views[i] = v;
            }

            if (!em.World.IsCreated)
                return;

            // v1_094 — une province simulée peut couvrir PLUSIEURS vues (mode pilote :
            // 194 cellules pour 13 provinces). L'index doit donc être 1→N, sinon le
            // front n'apparaîtrait que sur une cellule arbitraire de la province.
            var byId = new Dictionary<int, List<int>>(views.Count);
            for (var i = 0; i < views.Count; i++)
            {
                var simId = PilotMapProvider.SimulationProvinceIdOfView(views[i].Id);
                if (simId <= 0)
                    continue;
                if (!byId.TryGetValue(simId, out var bucket))
                {
                    bucket = new List<int>(1);
                    byId[simId] = bucket;
                }

                bucket.Add(i);
            }

            using var q = em.CreateEntityQuery(
                ComponentType.ReadOnly<FrontSectorData>(),
                ComponentType.ReadOnly<FrontLineState>());
            using var entities = q.ToEntityArray(Allocator.Temp);
            for (var s = 0; s < entities.Length; s++)
            {
                var sector = em.GetComponentData<FrontSectorData>(entities[s]);
                if (!sector.IsActive)
                    continue;

                // Motif FrontAdvanceSystem.cs:89-98 — ne pas annoter un secteur périmé.
                if (!DebugAnnotateInactiveWarFronts)
                {
                    var warEntity = sector.War;
                    if (warEntity == Entity.Null ||
                        !em.Exists(warEntity) ||
                        !em.HasComponent<WarData>(warEntity))
                        continue;
                    var warData = em.GetComponentData<WarData>(warEntity);
                    if (!warData.IsActive)
                        continue;
                }

                var buf = em.GetBuffer<FrontLineState>(entities[s]);
                for (var b = 0; b < buf.Length; b++)
                {
                    var st = buf[b];
                    if (!byId.TryGetValue(st.ProvinceId, out var bucket))
                        continue;
                    for (var k = 0; k < bucket.Count; k++)
                    {
                        var vi = bucket[k];
                        var v = views[vi];
                        v.IsFront = true;
                        v.IsContested = v.IsContested || st.IsContested;
                        if (st.AttackerPressure > v.AttackerPressure)
                            v.AttackerPressure = st.AttackerPressure;
                        if (st.DefenderPressure > v.DefenderPressure)
                            v.DefenderPressure = st.DefenderPressure;
                        views[vi] = v;
                    }
                }
            }
        }

        /// <summary>
        /// Côte (littoral terre↔mer) + léger relief (assombrissement près des côtes).
        /// N'altère pas la géométrie — lecture seule du masque IsLand.
        /// </summary>
        static void ApplyCoastAndRelief(Color32[] pixels, bool[] isLand)
        {
            if (pixels == null || isLand == null || pixels.Length != isLand.Length)
                return;

            // Passe 1 — relief discret : terre à ≤2 px de la mer assombrie (~12 %).
            for (var py = 0; py < ActiveH; py++)
            {
                for (var px = 0; px < ActiveW; px++)
                {
                    var idx = py * ActiveW + px;
                    if (!isLand[idx])
                        continue;
                    var touchSea = false;
                    for (var oy = -2; oy <= 2 && !touchSea; oy++)
                    {
                        for (var ox = -2; ox <= 2; ox++)
                        {
                            var nx = px + ox;
                            var ny = py + oy;
                            if (nx < 0 || ny < 0 || nx >= ActiveW || ny >= ActiveH)
                                continue;
                            if (!isLand[ny * ActiveW + nx])
                            {
                                touchSea = true;
                                break;
                            }
                        }
                    }

                    if (!touchSea)
                        continue;
                    var c = pixels[idx];
                    pixels[idx] = new Color32(
                        (byte)(c.r * 88 / 100),
                        (byte)(c.g * 88 / 100),
                        (byte)(c.b * 88 / 100),
                        c.a);
                }
            }

            // Passe 2 — trait de côte sur pixels terre adjacents (4-connexité) à la mer.
            for (var py = 0; py < ActiveH; py++)
            {
                for (var px = 0; px < ActiveW; px++)
                {
                    var idx = py * ActiveW + px;
                    if (!isLand[idx])
                        continue;
                    var edge =
                        (px > 0 && !isLand[idx - 1]) ||
                        (px + 1 < ActiveW && !isLand[idx + 1]) ||
                        (py > 0 && !isLand[idx - ActiveW]) ||
                        (py + 1 < ActiveH && !isLand[idx + ActiveW]);
                    if (edge)
                        pixels[idx] = CoastLine;
                }
            }
        }

        /// <summary>
        /// Chenaux de détroit : liaisons maritimes en pointillé (GraphStraitEdge), lisibles sur la mer.
        /// </summary>
        static void ApplyStraitLinks(
            List<ProvinceView> views,
            Color32[] pixels,
            float minX, float maxX, float minY, float maxY)
        {
            if (views == null || views.Count == 0)
                return;

            var byId = new Dictionary<int, ProvinceView>(views.Count);
            for (var i = 0; i < views.Count; i++)
                byId[views[i].Id] = views[i];

            var adjacency = GameDataLoader.LoadProvinceAdjacency();
            var drawn = new HashSet<long>();
            for (var a = 0; a < adjacency.Count; a++)
            {
                var def = adjacency[a];
                if (def.straits == null || !byId.ContainsKey(def.id))
                    continue;
                for (var s = 0; s < def.straits.Count; s++)
                {
                    var other = def.straits[s];
                    var key = EdgeKey(def.id, other);
                    if (!drawn.Add(key))
                        continue;
                    if (!byId.ContainsKey(other))
                        continue;
                    DrawLineProjected(
                        pixels,
                        byId[def.id].X, byId[def.id].Y,
                        byId[other].X, byId[other].Y,
                        minX, maxX, minY, maxY,
                        GraphStraitEdge, dashed: true);
                }
            }
        }

        static void ApplyBorders(List<ProvinceView> views, Color32[] pixels, int[] provinceAt)
        {
            // Masque : 0 = rien, 1 = interne, 2 = politique.
            var border = new byte[ActiveW * ActiveH];

            for (var py = 0; py < ActiveH; py++)
            {
                for (var px = 0; px < ActiveW; px++)
                {
                    var idx = py * ActiveW + px;
                    var a = provinceAt[idx];
                    if (a < 0)
                        continue;

                    // Voisins droite / bas — chaque frontière touchée une fois.
                    if (px + 1 < ActiveW)
                        MarkEdge(views, provinceAt, border, idx, py * ActiveW + (px + 1), a);
                    if (py + 1 < ActiveH)
                        MarkEdge(views, provinceAt, border, idx, (py + 1) * ActiveW + px, a);
                }
            }

            // Dilate politique à PoliticalBorderRadius px ; interne reste 1 px (non dilaté).
            var dilated = new byte[ActiveW * ActiveH];
            Array.Copy(border, dilated, border.Length);
            var r = PoliticalBorderRadius;
            for (var py = 0; py < ActiveH; py++)
            {
                for (var px = 0; px < ActiveW; px++)
                {
                    var idx = py * ActiveW + px;
                    if (border[idx] < 2)
                        continue;
                    for (var oy = -r; oy <= r; oy++)
                    {
                        for (var ox = -r; ox <= r; ox++)
                        {
                            var nx = px + ox;
                            var ny = py + oy;
                            if (nx < 0 || ny < 0 || nx >= ActiveW || ny >= ActiveH)
                                continue;
                            var nidx = ny * ActiveW + nx;
                            if (provinceAt[nidx] >= 0 && dilated[nidx] < 2)
                                dilated[nidx] = 2;
                        }
                    }
                }
            }

            // brief 005-refonte-visuelle-carte, Success Condition 4 : anneau de PLUME
            // (feather) d'1 px de plus autour du polygone dilaté ci-dessus, mélangé à 50%
            // avec la couleur DÉJÀ PRÉSENTE sous ce pixel (relief/remplissage), au lieu
            // d'un remplacement franc — confirmé par inspection à l'œil (crop 4x) : le
            // dilatation carrée sans anti-aliasing produit un escalier de pixels net.
            // Volontairement borné à UN SEUL anneau supplémentaire (pas une passe
            // multi-pixels/gaussienne) pour ne pas aggraver le coût CPU déjà mesuré comme
            // significatif (Success Condition 3) — un ajout de complexité O(1) par pixel de
            // bord déjà identifié, pas un second passage plein-buffer.
            var feather = new byte[ActiveW * ActiveH];
            var fr = r + 1;
            for (var py = 0; py < ActiveH; py++)
            {
                for (var px = 0; px < ActiveW; px++)
                {
                    var idx = py * ActiveW + px;
                    if (border[idx] < 2)
                        continue;
                    for (var oy = -fr; oy <= fr; oy++)
                    {
                        for (var ox = -fr; ox <= fr; ox++)
                        {
                            if (System.Math.Abs(ox) < r + 1 && System.Math.Abs(oy) < r + 1)
                                continue; // déjà couvert par le coeur plein (dilated)
                            var nx = px + ox;
                            var ny = py + oy;
                            if (nx < 0 || ny < 0 || nx >= ActiveW || ny >= ActiveH)
                                continue;
                            var nidx = ny * ActiveW + nx;
                            if (provinceAt[nidx] >= 0 && dilated[nidx] == 0)
                                feather[nidx] = 1;
                        }
                    }
                }
            }

            for (var i = 0; i < dilated.Length; i++)
            {
                if (dilated[i] == 2)
                    pixels[i] = PoliticalBorder;
                else if (dilated[i] == 1)
                    pixels[i] = InternalBorder;
                else if (feather[i] == 1)
                    pixels[i] = BlendColor(pixels[i], PoliticalBorder, 0.5f);
            }
        }

        /// <summary>Mélange linéaire RGB (le canal A reste celui de <paramref name="a"/>).</summary>
        static Color32 BlendColor(Color32 a, Color32 b, float t)
        {
            return new Color32(
                (byte)(a.r + (b.r - a.r) * t),
                (byte)(a.g + (b.g - a.g) * t),
                (byte)(a.b + (b.b - a.b) * t),
                a.a);
        }

        /// <summary>
        /// Un label NOM DE PAYS par propriétaire, au centroïde des provinces.
        /// Collision déterministe : tri tag, priorité taille (nb provinces), décalages fixes, sinon omission.
        /// </summary>
        static void ApplyLabels(
            List<ProvinceView> views,
            Color32[] pixels,
            float minX, float maxX, float minY, float maxY)
            => ApplyLabelsByDensity(
                views, pixels, minX, maxX, minY, maxY,
                LabelDensity.Countries, selectedProvinceId: -1);

        static void ApplyLabelsByDensity(
            List<ProvinceView> views,
            Color32[] pixels,
            float minX, float maxX, float minY, float maxY,
            LabelDensity density,
            int selectedProvinceId)
        {
            LastLabelsPlaced = 0;
            LastLabelsOmitted = 0;
            if (views == null || views.Count == 0 || density == LabelDensity.None)
                return;

            if (density == LabelDensity.Countries)
            {
                ApplyCountryLabels(views, pixels, minX, maxX, minY, maxY);
                return;
            }

            ApplyProvinceNameLabels(
                views, pixels, minX, maxX, minY, maxY,
                density == LabelDensity.SelectedProvince ? selectedProvinceId : -1);
        }

        static void ApplyCountryLabels(
            List<ProvinceView> views,
            Color32[] pixels,
            float minX, float maxX, float minY, float maxY)
        {
            // Agrégats par tag : centroïde + taille + nom + couleur représentative.
            var sumX = new Dictionary<string, float>();
            var sumY = new Dictionary<string, float>();
            var count = new Dictionary<string, int>();
            var nameOf = new Dictionary<string, string>();
            var fillOf = new Dictionary<string, Color32>();

            for (var i = 0; i < views.Count; i++)
            {
                var v = views[i];
                var tag = v.OwnerTag;
                if (string.IsNullOrEmpty(tag))
                    continue;

                if (!sumX.ContainsKey(tag))
                {
                    sumX[tag] = 0f;
                    sumY[tag] = 0f;
                    count[tag] = 0;
                    nameOf[tag] = string.IsNullOrEmpty(v.OwnerName) ? tag : v.OwnerName;
                    fillOf[tag] = v.Fill;
                }

                sumX[tag] += v.X;
                sumY[tag] += v.Y;
                count[tag]++;
                // Nom : première non-vide gagne (ordre id province déjà stable dans views).
                if (string.IsNullOrEmpty(nameOf[tag]) && !string.IsNullOrEmpty(v.OwnerName))
                    nameOf[tag] = v.OwnerName;
            }

            var tags = new List<string>(count.Keys);
            // Tri stable : d'abord taille décroissante, puis tag ordinal (reproductible).
            tags.Sort((a, b) =>
            {
                var cmp = count[b].CompareTo(count[a]);
                if (cmp != 0) return cmp;
                return string.CompareOrdinal(a, b);
            });

            var placed = new List<LabelBox>(tags.Count);
            PlaceLabelCandidates(
                tags, pixels, placed, minX, maxX, minY, maxY,
                tag =>
                {
                    var n = count[tag];
                    return (sumX[tag] / n, sumY[tag] / n,
                        SanitizeLabelText(nameOf[tag]), fillOf[tag]);
                });
        }

        /// <summary>
        /// Labels de provinces — tous visibles dans la fenêtre, ou une seule si selectedId ≥ 0.
        /// Tri déterministe par Id croissant ; collisions résolues par offsets fixes.
        /// </summary>
        static void ApplyProvinceNameLabels(
            List<ProvinceView> views,
            Color32[] pixels,
            float minX, float maxX, float minY, float maxY,
            int selectedProvinceId)
        {
            var candidates = new List<ProvinceView>(views.Count);
            for (var i = 0; i < views.Count; i++)
            {
                var v = views[i];
                // v1_094 — le filtre porte un ProvinceId SIMULÉ ; v.Id est une vue.
                if (selectedProvinceId >= 0 &&
                    PilotMapProvider.SimulationProvinceIdOfView(v.Id) != selectedProvinceId)
                    continue;
                // Hors fenêtre (centroïde) — omettre pour ne pas saturer.
                if (v.X < minX || v.X > maxX || v.Y < minY || v.Y > maxY)
                    continue;
                candidates.Add(v);
            }

            // v1_094 — en pilote, N cellules partagent une province et portent donc le
            // MÊME nom. Sans ce repli, la carte écrirait « Bourgogne » vingt fois.
            // Représentant retenu : la plus grande cellule (départage par Id pour
            // rester déterministe à surfaces égales).
            if (PilotMapProvider.Enabled && candidates.Count > 1)
            {
                var best = new Dictionary<int, ProvinceView>(candidates.Count);
                for (var i = 0; i < candidates.Count; i++)
                {
                    var v = candidates[i];
                    var simId = PilotMapProvider.SimulationProvinceIdOfView(v.Id);
                    if (simId <= 0)
                        continue;
                    if (!best.TryGetValue(simId, out var cur))
                    {
                        best[simId] = v;
                        continue;
                    }

                    var av = PilotMapProvider.AreaOfCell(v.Id);
                    var ac = PilotMapProvider.AreaOfCell(cur.Id);
                    if (av > ac || (av == ac && v.Id < cur.Id))
                        best[simId] = v;
                }

                candidates.Clear();
                foreach (var kv in best)
                    candidates.Add(kv.Value);
            }

            candidates.Sort((a, b) => a.Id.CompareTo(b.Id));
            var placed = new List<LabelBox>(candidates.Count);
            var keys = new List<string>(candidates.Count);
            var byKey = new Dictionary<string, ProvinceView>(candidates.Count);
            for (var i = 0; i < candidates.Count; i++)
            {
                var key = candidates[i].Id.ToString(CultureInfo.InvariantCulture);
                keys.Add(key);
                byKey[key] = candidates[i];
            }

            PlaceLabelCandidates(
                keys, pixels, placed, minX, maxX, minY, maxY,
                key =>
                {
                    var v = byKey[key];
                    var text = SanitizeLabelText(
                        string.IsNullOrEmpty(v.ProvinceName)
                            ? ("P" + v.Id.ToString(CultureInfo.InvariantCulture))
                            : v.ProvinceName);
                    return (v.X, v.Y, text, v.Fill);
                });
        }

        static void PlaceLabelCandidates(
            List<string> keys,
            Color32[] pixels,
            List<LabelBox> placed,
            float minX, float maxX, float minY, float maxY,
            Func<string, (float cx, float cy, string text, Color32 fill)> resolve)
        {
            var offsetsX = new[] { 0, 0, 0, 14, -14, 14, -14, 28, -28, 0, 0 };
            var offsetsY = new[] { 0, 16, -16, 0, 0, 12, -12, 0, 0, 28, -28 };

            for (var ti = 0; ti < keys.Count; ti++)
            {
                var resolved = resolve(keys[ti]);
                var text = resolved.text;
                if (string.IsNullOrEmpty(text))
                    text = keys[ti];

                WorldToPixel(
                    resolved.cx, resolved.cy, minX, maxX, minY, maxY, out var cx, out var cy);

                var luminance = resolved.fill.r + resolved.fill.g + resolved.fill.b;
                var fg = luminance < 384 ? LabelLight : LabelDark;
                var halo = luminance < 384 ? LabelDark : LabelLight;

                // v1_040/v1_041 — réservation partagée ; file d'importance si active.
                if (MapLabelLayout.IsActive)
                {
                    var kind = MapLabelKind.Province;
                    var id = 0;
                    if (int.TryParse(keys[ti], NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid))
                        id = pid;
                    else
                        kind = MapLabelKind.Country;

                    var rank = kind == MapLabelKind.Country
                        ? MapLabelImportance.CountryName
                        : MapLabelImportance.RankForProvinceLabel(text);
                    var domainKey = kind == MapLabelKind.Country
                        ? StableTagDomainKey(keys[ti])
                        : id;

                    if (MapLabelLayout.UseImportanceQueue)
                    {
                        MapLabelLayout.Enqueue(
                            text, cx, cy, markerSize: 0, fg, halo, kind, id,
                            rank, population: 0, statusRank: 0, domainKey,
                            useAnchorSlots: false,
                            isProtected: rank <= MapLabelImportance.ProvinceName);
                        LastLabelsPlaced++; // compte candidate ; Flush tranche le réel
                        continue;
                    }

                    if (MapLabelLayout.TryPlaceWithOffsets(
                            pixels, text, cx, cy, offsetsX, offsetsY, fg, halo, kind, id,
                            out var sharedBox))
                    {
                        placed.Add(new LabelBox
                        {
                            X0 = sharedBox.X0, Y0 = sharedBox.Y0,
                            X1 = sharedBox.X1, Y1 = sharedBox.Y1
                        });
                        LastLabelsPlaced++;
                    }
                    else
                        LastLabelsOmitted++;
                    continue;
                }

                var textW = MeasureBitmapText(text);
                var textH = GlyphH * ActiveGlyphScale;
                var placedOk = false;
                for (var oi = 0; oi < offsetsX.Length; oi++)
                {
                    var originX = cx - textW / 2 + offsetsX[oi];
                    var originY = cy - textH / 2 + offsetsY[oi];
                    var box = new LabelBox
                    {
                        X0 = originX - 1,
                        Y0 = originY - 1,
                        X1 = originX + textW + 1,
                        Y1 = originY + textH + 1
                    };

                    if (box.X1 < 0 || box.Y1 < 0 || box.X0 >= ActiveW || box.Y0 >= ActiveH)
                        continue;

                    var collides = false;
                    for (var p = 0; p < placed.Count; p++)
                    {
                        if (BoxesOverlap(box, placed[p]))
                        {
                            collides = true;
                            break;
                        }
                    }

                    if (collides)
                        continue;

                    DrawBitmapText(pixels, text, originX, originY, fg, halo);
                    placed.Add(box);
                    LastLabelsPlaced++;
                    placedOk = true;
                    break;
                }

                if (!placedOk)
                    LastLabelsOmitted++;
            }
        }

        /// <summary>
        /// Clé de domaine déterministe pour un tag pays (jamais string.GetHashCode).
        /// </summary>
        static int StableTagDomainKey(string tag)
        {
            if (string.IsNullOrEmpty(tag))
                return 0;
            unchecked
            {
                var h = 0;
                for (var i = 0; i < tag.Length; i++)
                    h = h * 31 + tag[i];
                return h;
            }
        }

        /// <summary>
        /// Panneau multi-lignes (détail province) — lecture seule, glyphes bitmap.
        /// y=0 en bas. Dessiné à gauche pour ne pas masquer le centre zoomé.
        /// </summary>
        public static void DrawProvinceDetailPanel(
            Color32[] pixels, int width, int height, string detailBlock)
        {
            if (pixels == null || string.IsNullOrEmpty(detailBlock))
                return;

            var lines = detailBlock.Split('\n');
            const int pad = 6;
            var lineH = GlyphH * ActiveGlyphScale + 2;
            var panelH = pad * 2 + lines.Length * lineH;
            if (panelH > height - 8)
                panelH = height - 8;

            var maxLineW = 0;
            for (var i = 0; i < lines.Length; i++)
            {
                var w = MeasureBitmapText(SanitizeLabelText(lines[i]));
                if (w > maxLineW) maxLineW = w;
            }

            var panelW = pad * 2 + maxLineW;
            if (panelW > width / 2)
                panelW = width / 2;

            var bg = new Color32(0x10, 0x12, 0x16, 235);
            var fg = new Color32(0xf0, 0xf0, 0xf0, 255);
            var halo = new Color32(0x08, 0x08, 0x08, 255);
            var y0 = height - panelH;
            if (y0 < 0) y0 = 0;

            for (var y = y0; y < height; y++)
            {
                var row = y * width;
                for (var x = 0; x < panelW && x < width; x++)
                    pixels[row + x] = bg;
            }

            var textY = height - pad - GlyphH * ActiveGlyphScale;
            for (var i = 0; i < lines.Length; i++)
            {
                if (textY < y0)
                    break;
                DrawBitmapText(
                    pixels, SanitizeLabelText(lines[i]), pad, textY, fg, halo);
                textY -= lineH;
            }
        }

        struct LabelBox
        {
            public int X0, Y0, X1, Y1;
        }

        static bool BoxesOverlap(LabelBox a, LabelBox b) =>
            a.X0 < b.X1 && a.X1 > b.X0 && a.Y0 < b.Y1 && a.Y1 > b.Y0;

        /// <summary>
        /// Glyphes 5×7 : majuscules / chiffres / ponctuation connue.
        /// Accents repliés vers ASCII (NFD + cas spéciaux) AVANT le filtre — jamais élargis.
        /// Caractères non mappés : nommés dans <see cref="LastSanitizeUnmapped"/>, jamais perdus en silence.
        /// </summary>
        public static string SanitizeLabelText(string raw)
        {
            SanitizeUnmapped.Clear();
            if (string.IsNullOrEmpty(raw))
                return "";

            var folded = FoldDiacriticsToAscii(raw);
            var sb = new StringBuilder(folded.Length);
            for (var i = 0; i < folded.Length; i++)
            {
                var ch = folded[i];
                if (ch >= 'a' && ch <= 'z')
                    ch = (char)(ch - 'a' + 'A');
                if ((ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9') ||
                    ch == ' ' || ch == '-' || ch == '.' || ch == '\'' ||
                    ch == '=' || ch == '/' || ch == '_' || ch == '%')
                    sb.Append(ch == '\'' ? ' ' : ch);
            }

            return sb.ToString().Trim();
        }

        /// <summary>
        /// Repli ASCII avant filtre. NFD retire les diacritiques ; cas hors NFD nommés.
        /// </summary>
        public static string FoldDiacriticsToAscii(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return "";

            var sb = new StringBuilder(raw.Length * 2);
            for (var i = 0; i < raw.Length; i++)
            {
                var ch = raw[i];
                if (TryMapSpecialLetter(ch, out var mapped))
                {
                    sb.Append(mapped);
                    continue;
                }

                string decomposed;
                try
                {
                    decomposed = ch.ToString().Normalize(NormalizationForm.FormD);
                }
                catch (ArgumentException)
                {
                    NameUnmapped(ch);
                    continue;
                }

                for (var j = 0; j < decomposed.Length; j++)
                {
                    var c = decomposed[j];
                    var cat = CharUnicodeInfo.GetUnicodeCategory(c);
                    if (cat == UnicodeCategory.NonSpacingMark ||
                        cat == UnicodeCategory.SpacingCombiningMark ||
                        cat == UnicodeCategory.EnclosingMark)
                        continue;

                    if (c <= 0x7F)
                    {
                        sb.Append(c);
                        continue;
                    }

                    if (TryMapSpecialLetter(c, out var mapped2))
                    {
                        sb.Append(mapped2);
                        continue;
                    }

                    NameUnmapped(c);
                }
            }

            return sb.ToString();
        }

        static bool TryMapSpecialLetter(char ch, out string mapped)
        {
            switch (ch)
            {
                // Virgule souscrite / cédille roumains (NFD parfois incomplet selon runtime).
                case 'ș': case 'ş': mapped = "s"; return true;
                case 'Ș': case 'Ş': mapped = "S"; return true;
                case 'ț': case 'ţ': mapped = "t"; return true;
                case 'Ț': case 'Ţ': mapped = "T"; return true;
                case 'ß': mapped = "ss"; return true;
                case 'æ': mapped = "ae"; return true;
                case 'Æ': mapped = "AE"; return true;
                case 'œ': mapped = "oe"; return true;
                case 'Œ': mapped = "OE"; return true;
                case 'ø': mapped = "o"; return true;
                case 'Ø': mapped = "O"; return true;
                case 'ł': mapped = "l"; return true;
                case 'Ł': mapped = "L"; return true;
                case 'đ': mapped = "d"; return true;
                case 'Đ': mapped = "D"; return true;
                case 'ð': mapped = "d"; return true;
                case 'Ð': mapped = "D"; return true;
                case 'þ': mapped = "th"; return true;
                case 'Þ': mapped = "TH"; return true;
                default:
                    mapped = null;
                    return false;
            }
        }

        static void NameUnmapped(char ch)
        {
            var entry = "U+" + ((int)ch).ToString("X4", CultureInfo.InvariantCulture) +
                        " '" + ch + "'";
            for (var i = 0; i < SanitizeUnmapped.Count; i++)
            {
                if (SanitizeUnmapped[i] == entry)
                    return;
            }

            SanitizeUnmapped.Add(entry);
        }

        /// <summary>
        /// Sanitize sans repli d'accents (mutation rouge V1073-A) — filtre seul comme avant v1_073.
        /// </summary>
        public static string SanitizeLabelTextWithoutFold(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return "";
            var sb = new StringBuilder(raw.Length);
            for (var i = 0; i < raw.Length; i++)
            {
                var ch = raw[i];
                if (ch >= 'a' && ch <= 'z')
                    ch = (char)(ch - 'a' + 'A');
                if ((ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9') ||
                    ch == ' ' || ch == '-' || ch == '.' || ch == '\'' ||
                    ch == '=' || ch == '/' || ch == '_' || ch == '%')
                    sb.Append(ch == '\'' ? ' ' : ch);
            }

            return sb.ToString().Trim();
        }

        static void BlitGlyph(
            Color32[] pixels, byte[] glyph, int ox, int oy, Color32 color, bool outline)
        {
            var scale = ActiveGlyphScale;
            for (var row = 0; row < GlyphH; row++)
            {
                var bits = glyph[row];
                for (var col = 0; col < GlyphW; col++)
                {
                    if (((bits >> (GlyphW - 1 - col)) & 1) == 0)
                        continue;

                    var gx = ox + col * scale;
                    // Buffer nord@py0 : row 0 (haut de lettre) vers le nord (py croissant = sud).
                    // Pas de pré-inversion — WriteMapBufferPng / UI Toolkit font l'unique
                    // inversion à l'encodage (v1_079). Une compensation locale ici
                    // ajoutait une 2ᵉ inversion et miroitait le texte.
                    // DebugPreInvertGlyphs : mutation rouge V1079-A (rejoue l'ancien défaut).
                    var gy = DebugPreInvertGlyphs
                        ? oy + (GlyphH - 1 - row) * scale
                        : oy + row * scale;
                    for (var sy = 0; sy < scale; sy++)
                    {
                        for (var sx = 0; sx < scale; sx++)
                        {
                            var px = gx + sx;
                            var py = gy + sy;
                            if (outline)
                            {
                                for (var oy2 = -1; oy2 <= 1; oy2++)
                                {
                                    for (var ox2 = -1; ox2 <= 1; ox2++)
                                    {
                                        if (ox2 == 0 && oy2 == 0)
                                            continue;
                                        SetPixelSafe(pixels, px + ox2, py + oy2, color);
                                    }
                                }
                            }
                            else
                            {
                                SetPixelSafe(pixels, px, py, color);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Glyphes 5×7 : 7 rangées, 5 bits MSB=gauche. Chiffres + majuscules + espace.
        /// </summary>
        static byte[] GetGlyph(char ch)
        {
            if (ch >= 'a' && ch <= 'z')
                ch = (char)(ch - 'a' + 'A');

            switch (ch)
            {
                case ' ': return GlyphSpace;
                case '.': return GlyphDot;
                case '-': return GlyphMinus;
                case '+': return GlyphPlus;
                case ':': return GlyphColon;
                case '/': return GlyphSlash;
                case '(': return GlyphLParen;
                case ')': return GlyphRParen;
                case '=': return GlyphEquals;
                case '_': return GlyphUnderscore;
                case ',': return GlyphComma;
                case '0': return Glyph0;
                case '1': return Glyph1;
                case '2': return Glyph2;
                case '3': return Glyph3;
                case '4': return Glyph4;
                case '5': return Glyph5;
                case '6': return Glyph6;
                case '7': return Glyph7;
                case '8': return Glyph8;
                case '9': return Glyph9;
                case 'A': return GlyphA;
                case 'B': return GlyphB;
                case 'C': return GlyphC;
                case 'D': return GlyphD;
                case 'E': return GlyphE;
                case 'F': return GlyphF;
                case 'G': return GlyphG;
                case 'H': return GlyphH_;
                case 'I': return GlyphI;
                case 'J': return GlyphJ;
                case 'K': return GlyphK;
                case 'L': return GlyphL;
                case 'M': return GlyphM;
                case 'N': return GlyphN;
                case 'O': return GlyphO;
                case 'P': return GlyphP;
                case 'Q': return GlyphQ;
                case 'R': return GlyphR;
                case 'S': return GlyphS;
                case 'T': return GlyphT;
                case 'U': return GlyphU;
                case 'V': return GlyphV;
                case 'W': return GlyphW_;
                case 'X': return GlyphX;
                case 'Y': return GlyphY;
                case 'Z': return GlyphZ;
                default: return GlyphSpace;
            }
        }

        // Rangées : bit4 … bit0 = colonnes gauche → droite
        static readonly byte[] GlyphSpace = { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        static readonly byte[] GlyphDot = { 0x00, 0x00, 0x00, 0x00, 0x00, 0x0C, 0x0C };
        static readonly byte[] GlyphMinus = { 0x00, 0x00, 0x00, 0x1F, 0x00, 0x00, 0x00 };
        static readonly byte[] GlyphPlus = { 0x00, 0x04, 0x04, 0x1F, 0x04, 0x04, 0x00 };
        static readonly byte[] GlyphColon = { 0x00, 0x0C, 0x0C, 0x00, 0x0C, 0x0C, 0x00 };
        static readonly byte[] GlyphSlash = { 0x01, 0x02, 0x04, 0x04, 0x08, 0x10, 0x10 };
        static readonly byte[] GlyphLParen = { 0x04, 0x08, 0x10, 0x10, 0x10, 0x08, 0x04 };
        static readonly byte[] GlyphRParen = { 0x08, 0x04, 0x02, 0x02, 0x02, 0x04, 0x08 };
        static readonly byte[] GlyphEquals = { 0x00, 0x00, 0x1F, 0x00, 0x1F, 0x00, 0x00 };
        static readonly byte[] GlyphUnderscore = { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x1F };
        static readonly byte[] GlyphComma = { 0x00, 0x00, 0x00, 0x00, 0x0C, 0x04, 0x08 };
        static readonly byte[] Glyph0 = { 0x0E, 0x11, 0x13, 0x15, 0x19, 0x11, 0x0E };
        static readonly byte[] Glyph1 = { 0x04, 0x0C, 0x04, 0x04, 0x04, 0x04, 0x0E };
        static readonly byte[] Glyph2 = { 0x0E, 0x11, 0x01, 0x02, 0x04, 0x08, 0x1F };
        static readonly byte[] Glyph3 = { 0x0E, 0x11, 0x01, 0x06, 0x01, 0x11, 0x0E };
        static readonly byte[] Glyph4 = { 0x02, 0x06, 0x0A, 0x12, 0x1F, 0x02, 0x02 };
        static readonly byte[] Glyph5 = { 0x1F, 0x10, 0x1E, 0x01, 0x01, 0x11, 0x0E };
        static readonly byte[] Glyph6 = { 0x06, 0x08, 0x10, 0x1E, 0x11, 0x11, 0x0E };
        static readonly byte[] Glyph7 = { 0x1F, 0x01, 0x02, 0x04, 0x08, 0x08, 0x08 };
        static readonly byte[] Glyph8 = { 0x0E, 0x11, 0x11, 0x0E, 0x11, 0x11, 0x0E };
        static readonly byte[] Glyph9 = { 0x0E, 0x11, 0x11, 0x0F, 0x01, 0x02, 0x0C };
        static readonly byte[] GlyphA = { 0x0E, 0x11, 0x11, 0x1F, 0x11, 0x11, 0x11 };
        static readonly byte[] GlyphB = { 0x1E, 0x11, 0x11, 0x1E, 0x11, 0x11, 0x1E };
        static readonly byte[] GlyphC = { 0x0E, 0x11, 0x10, 0x10, 0x10, 0x11, 0x0E };
        static readonly byte[] GlyphD = { 0x1E, 0x11, 0x11, 0x11, 0x11, 0x11, 0x1E };
        static readonly byte[] GlyphE = { 0x1F, 0x10, 0x10, 0x1E, 0x10, 0x10, 0x1F };
        static readonly byte[] GlyphF = { 0x1F, 0x10, 0x10, 0x1E, 0x10, 0x10, 0x10 };
        static readonly byte[] GlyphG = { 0x0E, 0x11, 0x10, 0x17, 0x11, 0x11, 0x0F };
        static readonly byte[] GlyphH_ = { 0x11, 0x11, 0x11, 0x1F, 0x11, 0x11, 0x11 };
        static readonly byte[] GlyphI = { 0x0E, 0x04, 0x04, 0x04, 0x04, 0x04, 0x0E };
        static readonly byte[] GlyphJ = { 0x01, 0x01, 0x01, 0x01, 0x11, 0x11, 0x0E };
        static readonly byte[] GlyphK = { 0x11, 0x12, 0x14, 0x18, 0x14, 0x12, 0x11 };
        static readonly byte[] GlyphL = { 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x1F };
        static readonly byte[] GlyphM = { 0x11, 0x1B, 0x15, 0x15, 0x11, 0x11, 0x11 };
        static readonly byte[] GlyphN = { 0x11, 0x19, 0x15, 0x13, 0x11, 0x11, 0x11 };
        static readonly byte[] GlyphO = { 0x0E, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E };
        static readonly byte[] GlyphP = { 0x1E, 0x11, 0x11, 0x1E, 0x10, 0x10, 0x10 };
        static readonly byte[] GlyphQ = { 0x0E, 0x11, 0x11, 0x11, 0x15, 0x12, 0x0D };
        static readonly byte[] GlyphR = { 0x1E, 0x11, 0x11, 0x1E, 0x14, 0x12, 0x11 };
        static readonly byte[] GlyphS = { 0x0E, 0x11, 0x10, 0x0E, 0x01, 0x11, 0x0E };
        static readonly byte[] GlyphT = { 0x1F, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04 };
        static readonly byte[] GlyphU = { 0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E };
        static readonly byte[] GlyphV = { 0x11, 0x11, 0x11, 0x11, 0x11, 0x0A, 0x04 };
        static readonly byte[] GlyphW_ = { 0x11, 0x11, 0x11, 0x15, 0x15, 0x1B, 0x11 };
        static readonly byte[] GlyphX = { 0x11, 0x11, 0x0A, 0x04, 0x0A, 0x11, 0x11 };
        static readonly byte[] GlyphY = { 0x11, 0x11, 0x0A, 0x04, 0x04, 0x04, 0x04 };
        static readonly byte[] GlyphZ = { 0x1F, 0x01, 0x02, 0x04, 0x08, 0x10, 0x1F };

        static void MarkEdge(
            List<ProvinceView> views,
            int[] provinceAt,
            byte[] border,
            int idxA,
            int idxB,
            int a)
        {
            var b = provinceAt[idxB];
            if (b < 0 || b == a)
                return;

            // Comparer les TAGS (pas Entity) : un snapshot résolu au tick n'a pas d'Entity.
            var tagA = views[a].OwnerTag;
            var tagB = views[b].OwnerTag;
            var sameOwner = !string.IsNullOrEmpty(tagA)
                && string.Equals(tagA, tagB, StringComparison.Ordinal);
            var level = (byte)(sameOwner ? 1 : 2);
            if (border[idxA] < level) border[idxA] = level;
            if (border[idxB] < level) border[idxB] = level;
        }

        static void WorldToPixel(
            float wx, float wy,
            float minX, float maxX, float minY, float maxY,
            out int px, out int py)
        {
            var rangeX = maxX - minX;
            var rangeY = maxY - minY;
            px = (int)((wx - minX) / rangeX * ActiveW);
            // nord@py0 : y = −lat ⇒ MinY = nord ; py croît vers le sud (v1_085).
            // DebugLegacyMirrorWorldToPixelY : ancienne formule maxY−wy (miroir N-S).
            py = DebugLegacyMirrorWorldToPixelY
                ? (int)((maxY - wy) / rangeY * ActiveH)
                : (int)((wy - minY) / rangeY * ActiveH);
            if (px < 0) px = 0;
            if (py < 0) py = 0;
            if (px >= ActiveW) px = ActiveW - 1;
            if (py >= ActiveH) py = ActiveH - 1;
        }

        static void DrawLineProjected(
            Color32[] pixels,
            float x0, float y0, float x1, float y1,
            float minX, float maxX, float minY, float maxY,
            Color32 color,
            bool dashed)
        {
            WorldToPixel(x0, y0, minX, maxX, minY, maxY, out var px0, out var py0);
            WorldToPixel(x1, y1, minX, maxX, minY, maxY, out var px1, out var py1);

            var dx = Math.Abs(px1 - px0);
            var dy = Math.Abs(py1 - py0);
            var sx = px0 < px1 ? 1 : -1;
            var sy = py0 < py1 ? 1 : -1;
            var err = dx - dy;
            var step = 0;
            var x = px0;
            var y = py0;

            while (true)
            {
                if (!dashed || ((step / 4) % 2) == 0)
                    SetPixelSafe(pixels, x, y, color);

                if (x == px1 && y == py1)
                    break;
                var e2 = 2 * err;
                if (e2 > -dy) { err -= dy; x += sx; }
                if (e2 < dx) { err += dx; y += sy; }
                step++;
            }
        }

        static void FillCircle(Color32[] pixels, int cx, int cy, int radius, Color32 color)
        {
            var r2 = radius * radius;
            for (var y = -radius; y <= radius; y++)
            {
                for (var x = -radius; x <= radius; x++)
                {
                    if (x * x + y * y <= r2)
                        SetPixelSafe(pixels, cx + x, cy + y, color);
                }
            }
        }

        static void SetPixelSafe(Color32[] pixels, int x, int y, Color32 color)
        {
            if (x < 0 || y < 0 || x >= ActiveW || y >= ActiveH)
                return;
            pixels[y * ActiveW + x] = color;
        }

        static long EdgeKey(int a, int b)
        {
            var lo = a < b ? a : b;
            var hi = a < b ? b : a;
            return ((long)lo << 32) | (uint)hi;
        }

        static void WritePng(Color32[] pixels, string outputPath)
        {
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var tex = new Texture2D(ActiveW, ActiveH, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            var png = ImageConversion.EncodeToPNG(tex);
            UnityEngine.Object.DestroyImmediate(tex);
            File.WriteAllBytes(outputPath, png);
        }
    }
}
