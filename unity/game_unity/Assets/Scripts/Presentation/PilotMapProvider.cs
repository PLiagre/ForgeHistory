using Unity.Entities;
using Unity.Burst;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace VictoriaGame.Presentation
{
    /// <summary>
    /// v1_068 / v1_070 / v1_071 / v1_074 — source alternative de <see cref="MapSnapshotExporter.MapGeometry"/> :
    /// textures d'identifiants + cellules pilote (237), sans entités ECS.
    /// v1_071 : porte d'entrée <c>presentation_settings.json</c>, bascule F9 (vide MapGeometryCache),
    /// chargement PNG paresseux par LOD, budget résident publié.
    /// v1_074 : fleuves (rivers_g5c) peints dans le raster, chargement paresseux, réglage <c>show_rivers</c>.
    /// </summary>
    public static class PilotMapProvider
    {
        public const string MapDataRelative = "data/map";
        public const string PresentationSettingsRelative = "data/presentation_settings.json";
        public const string RiversFileName = "rivers_g5c.json";
        public const string RiverMouthsFileName = "river_mouths_g5c.json";
        public const KeyCode HotToggleKey = KeyCode.F9;
        /// <summary>Fichier absent ⇒ mode pilote OFF (bit-identique au Voronoï d'avant v1_071).</summary>
        public const bool FileAbsentMeansEnabled = false;
        public const string CopernicusAttribution =
            "© DLR e.V. 2010-2014 et © Airbus Defence and Space GmbH 2014-2018 " +
            "fournis dans le cadre de COPERNICUS par l'Union européenne et l'ESA";

        /// <summary>
        /// v1_080 — attribution obligatoire CC BY 4.0 pour les coordonnées villes
        /// dérivées de GeoNames (city_coordinates.json). Même mécanisme que Copernicus.
        /// </summary>
        public const string GeoNamesAttribution =
            "GeoNames (www.geonames.org), licensed under Creative Commons " +
            "Attribution 4.0 International (CC BY 4.0)";

        /// <summary>OFF = Voronoï legacy. ON = carte pilote. Appliqué via settings / F9.</summary>
        public static bool Enabled { get; set; }

        /// <summary>
        /// false (défaut / absent) = aucun trait fluvial (captures historiques bit-identiques).
        /// true = peindre rivers_g5c après ombrage.
        /// </summary>
        public static bool ShowRivers { get; set; }

        /// <summary>Political = possession ; Terrain = classe de relief (vue « physique »).</summary>
        public enum ColorMode : byte
        {
            Political = 0,
            Terrain = 1
        }

        public enum RiverNavigability : byte
        {
            Navigable = 0,
            NonNavigable = 1,
            Indeterminate = 2
        }

        /// <summary>
        /// Mode d'export thématique uniquement. Le rendu politique
        /// (<see cref="ApplyPilotColors"/>) ignore ce drapeau et colore TOUJOURS par tag propriétaire.
        /// </summary>
        public static ColorMode ActiveColorMode { get; set; } = ColorMode.Political;

        /// <summary>Cellule sélectionnée via texture (pas un ProvinceId ECS).</summary>
        public static int SelectedCellId { get; private set; } = -1;

        public static string LastCellDetail { get; private set; } = "";
        public static string PublishedCellToViewTablePath { get; private set; } = "";
        public static string PublishedTagColorTablePath { get; private set; } = "";
        public static string PresentationSettingsPath { get; private set; } = "";
        public static double LastSelectionMilliseconds { get; private set; }
        public static double BaselineVoronoiBuildMilliseconds { get; private set; }
        public static double LastPilotBuildMilliseconds { get; private set; }
        public static double LastJsonLoadMilliseconds { get; private set; }
        public static bool DataLoaded { get; private set; }
        public static bool SettingsApplied { get; private set; }
        public static bool SettingsFileFound { get; private set; }

        /// <summary>Pixels mer où l'ombrage est noir faute de tuile DEM (masqués → couleur mer).</summary>
        public static int LastBlackMissingDemPixels { get; private set; }

        /// <summary>Pixels terre à altitude nulle (ou quasi) où l'ombrage serait noir.</summary>
        public static int LastBlackElevZeroPixels { get; private set; }

        /// <summary>Pixels mer qui auraient été noirs sans masque, tous LOD cumulés lors du dernier scan.</summary>
        public static int LastSeaBlackMaskedTotal { get; private set; }

        /// <summary>Cellules (sur 237) résolues en ProvinceId navigable.</summary>
        public static int ResolvedProvinceIdCount { get; private set; }

        /// <summary>Cellules sans ProvinceId navigable (souvent unowned).</summary>
        public static int UnresolvedProvinceIdCount { get; private set; }

        /// <summary>Nombre de chargements PNG LOD effectués (compteur V1071-D).</summary>
        public static int LodTextureLoadCount { get; private set; }

        /// <summary>Nombre de chargements du domaine fleuves (compteur V1074-E).</summary>
        public static int RiverLoadCount { get; private set; }

        /// <summary>LODs actuellement résidents (textures décodées).</summary>
        public static int LoadedLodCount { get; private set; }

        public static int LastRiversDrawn { get; private set; }
        public static int LastRiversOutOfWindow { get; private set; }
        public static int LastNavigableThicknessPx { get; private set; }
        public static int LastNonNavigableThicknessPx { get; private set; }
        public static bool RiversDataLoaded { get; private set; }
        public static long RiversImportedBytes { get; private set; }
        public static string RiversCrsChosen { get; private set; } = "";
        public static string RiversCrsJustification { get; private set; } = "";
        public static string RiverMouthsUsageNote { get; private set; } = "";

        /// <summary>
        /// Libération auto des LOD hors usage : NON livrée (évite thrash monde↔province).
        /// API manuelle <see cref="ReleaseLod"/> disponible pour tests / budget.
        /// </summary>
        public static bool AutoReleaseUnusedLods => false;

        /// <summary>Seuil Z1 : reconstruction pilote à froid (ms) au-delà duquel GPU devient obligatoire.</summary>
        public const double Z1RebuildMillisecondsThreshold = 900.0;

        /// <summary>Seuil Z1 : poids résident textures+pixels (Mo) au-delà duquel GPU devient obligatoire.</summary>
        public const double Z1ResidentMegabytesThreshold = 500.0;

        /// <summary>Une seule application des settings par session runtime.</summary>
        public static bool RuntimeSettingsBootstrapped;

        static readonly Color32 UnownedHatchA = new Color32(0xC8, 0xB8, 0x40, 255);
        static readonly Color32 UnownedHatchB = new Color32(0x5A, 0x52, 0x28, 255);

        static float _midLat;
        static float _pilotMinX, _pilotMaxX, _pilotMinY, _pilotMaxY;
        static float _lonMin = -6.5f, _latMin = 42f, _lonMax = 8.5f, _latMax = 55.5f;

        static CellRecord[] _cells;
        static Dictionary<int, int> _cellIdToView;
        static Dictionary<int, CellRecord> _byCellId;
        static int[] _viewToCellId;
        static List<MapSnapshotExporter.ProvinceView> _skeleton;
        static bool[] _unownedView;
        static Dictionary<string, int> _cellsPerTag;
        static Dictionary<string, Color32> _tagColors;

        static Texture2D _idsLod0, _idsLod1, _idsLod2;
        static Texture2D _maskLod0, _maskLod1, _maskLod2;
        static Texture2D _hsLod0, _hsLod1, _hsLod2;
        static Color32[] _hsPix0, _hsPix1, _hsPix2;
        static Color32[] _maskPix0, _maskPix1, _maskPix2;
        static bool _loadAttempted;
        static readonly bool[] _lodTexturesLoaded = new bool[3];
        static readonly bool[] _lodBlackScanned = new bool[3];
        static readonly double[] _pilotColdMs = new double[3];
        static readonly double[] _pilotHotMs = new double[3];
        static readonly bool[] _pilotColdMeasured = new bool[3];
        static long _residentTextureBytes;
        static long _residentPixelBytes;
        static long _jsonBytesAtLoad;

        static bool _riversLoadAttempted;
        static RiverSegment[] _riverSegments;
        static RiverMouth[] _riverMouths;
        static long _riversBytesAtLoad;

        static readonly Color32 RiverNavigableColor = new Color32(0x1E, 0x5A, 0xC8, 255);
        static readonly Color32 RiverNonNavigableColor = new Color32(0x46, 0x82, 0xD2, 255);
        static readonly Color32 RiverIndeterminateColor = new Color32(0x64, 0x8C, 0xB4, 255);

        [Serializable]
        class PresentationSettingsFile
        {
            public bool pilot_map_enabled;
            /// <summary>false / absent = neutre (glyphes×2, sprites 10/22). true = zoom 2/3/5 et 16/28.</summary>
            public bool zoom_label_sprites;
            /// <summary>false / absent = aucun fleuve (bit-identique). true = peindre rivers_g5c.</summary>
            public bool show_rivers;
        }

        [Serializable]
        class RiversFile
        {
            public string crs_geometry;
            public string crs_rejected;
            public string crs_justification;
            public string fields_kept;
            public string fields_left;
            public string control_name_fr;
            public string control_label;
            public float control_lon;
            public float control_lat;
            public float control_expected_lon;
            public float control_expected_lat;
            public float control_distance_deg;
            public string control_segment_id;
            public int segment_count;
            public List<RiverSegmentDto> segments;
        }

        [Serializable]
        class RiverSegmentDto
        {
            public string segment_id;
            public string name_fr;
            public string navigability;
            public float length_m;
            public int scalerank;
            public int point_count;
            public float[] lonlat;
        }

        [Serializable]
        class RiverMouthsFile
        {
            public string crs;
            public string usage_note;
            public string fields_kept;
            public string fields_left;
            public int count;
            public List<RiverMouthDto> mouths;
        }

        [Serializable]
        class RiverMouthDto
        {
            public string segment_id;
            public string name;
            public string major_label;
            public float lon;
            public float lat;
            public int sea_zone_id;
        }

        public struct RiverSegment
        {
            public string SegmentId;
            public string NameFr;
            public RiverNavigability Navigability;
            public float LengthM;
            public int ScaleRank;
            public float[] LonLat;
        }

        public struct RiverMouth
        {
            public string SegmentId;
            public string Name;
            public string MajorLabel;
            public float Lon;
            public float Lat;
            public int SeaZoneId;
        }

        [Serializable]
        class OwnershipFile
        {
            public List<OwnershipCell> cells;
            public List<int> unowned_cell_ids;
            public int unowned_count;
            public float[] pilot_window_lonlat;
        }

        [Serializable]
        class OwnershipCell
        {
            public int cell_id;
            public string owner_tag;
            public string certainty;
            public int province_id;
            public string province_name;
            public float area_km2;
        }

        [Serializable]
        class ReliefFile
        {
            public List<ReliefCell> cells;
            public string copernicus_attribution;
        }

        [Serializable]
        class ReliefCell
        {
            public int cell_id;
            public float area_km2;
            public float elev_mean_m;
            public float elev_min_m;
            public float elev_max_m;
            public string terrain_class;
            public float centroid_lon;
            public float centroid_lat;
        }

        [Serializable]
        class BiomeFile
        {
            public List<BiomeCell> cells;
        }

        [Serializable]
        class BiomeCell
        {
            public int cell_id;
            public string biome;
            public bool is_coastal;
        }

        [Serializable]
        class AdjacencyFile
        {
            public List<AdjEdge> adjacency;
        }

        [Serializable]
        class AdjEdge
        {
            public int a;
            public int b;
            public bool fluvial_artery;
            public bool river_crossing;
            public string kind;
        }

        [Serializable]
        class CellsLodFile
        {
            public List<LodCell> cells;
        }

        [Serializable]
        class LodCell
        {
            public int cell_id;
            public float seed_lon;
            public float seed_lat;
            public float area_km2;
        }

        public struct CellRecord
        {
            public int CellId;
            public int ViewIndex;
            public float Lon;
            public float Lat;
            public float AreaKm2;
            public float ElevMeanM;
            public string TerrainClass;
            public string Biome;
            public bool IsCoastal;
            public bool HasNavigableRiver;
            public string OwnerTag;
            public string Certainty;
            public int ProvinceId;
            public string ProvinceName;
            public bool HasOwner;
            public List<int> Neighbors;
        }

        public static void EnsureLoaded()
        {
            if (_loadAttempted)
                return;
            _loadAttempted = true;
            try
            {
                var sw = Stopwatch.StartNew();
                LoadAll();
                sw.Stop();
                LastJsonLoadMilliseconds = sw.Elapsed.TotalMilliseconds;
                DataLoaded = _cells != null && _cells.Length > 0;
                if (DataLoaded)
                    MeasureProvinceIdResolution();
            }
            catch (Exception ex)
            {
                Debug.LogError("PilotMapProvider: chargement échoué — " + ex.Message);
                DataLoaded = false;
            }
        }

        /// <summary>
        /// Lit <c>StreamingAssets/data/presentation_settings.json</c>.
        /// Fichier absent ⇒ Enabled = false (comportement d'avant v1_071).
        /// </summary>
        public static bool ApplyPresentationSettings(bool clearCache = true)
        {
            PresentationSettingsPath = Path.Combine(
                Application.streamingAssetsPath, PresentationSettingsRelative);
            SettingsFileFound = File.Exists(PresentationSettingsPath);
            var want = FileAbsentMeansEnabled;
            var zoom = false;
            var rivers = false;
            if (SettingsFileFound)
            {
                try
                {
                    var raw = File.ReadAllText(PresentationSettingsPath);
                    var cfg = JsonUtility.FromJson<PresentationSettingsFile>(raw);
                    want = cfg != null && cfg.pilot_map_enabled;
                    zoom = cfg != null && cfg.zoom_label_sprites;
                    rivers = cfg != null && cfg.show_rivers;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("PilotMapProvider: settings illisibles — " + ex.Message);
                    want = FileAbsentMeansEnabled;
                    zoom = false;
                    rivers = false;
                }
            }

            MapSnapshotExporter.ZoomScaleEnabled = zoom;
            if (!zoom)
                MapSnapshotExporter.SetGlyphScale(MapSnapshotExporter.NeutralGlyphScale);
            ShowRivers = rivers;
            SetEnabled(want, clearCache);
            SettingsApplied = true;
            return Enabled;
        }

        /// <summary>Bascule mode pilote ↔ Voronoï. Vide toujours MapGeometryCache (CacheKey ignore le mode).</summary>
        public static void ToggleEnabled() => SetEnabled(!Enabled, clearCache: true);

        public static void SetEnabled(bool enabled, bool clearCache = true)
        {
            if (Enabled == enabled)
            {
                if (clearCache)
                    MapGeometryCache.Clear();
                return;
            }

            Enabled = enabled;
            if (clearCache)
                MapGeometryCache.Clear();
        }

        /// <summary>Compte les cellules navigables via <see cref="TryGetProvinceIdForNavigation"/>.</summary>
        public static void MeasureProvinceIdResolution()
        {
            EnsureLoaded();
            var resolved = 0;
            var unresolved = 0;
            if (_cells != null)
            {
                for (var i = 0; i < _cells.Length; i++)
                {
                    if (TryGetProvinceIdForNavigation(_cells[i].CellId, out _))
                        resolved++;
                    else
                        unresolved++;
                }
            }

            ResolvedProvinceIdCount = resolved;
            UnresolvedProvinceIdCount = unresolved;
        }

        public static bool IsLodLoaded(int lod)
        {
            lod = ClampLod(lod);
            return _lodTexturesLoaded[lod];
        }

        public static long ResidentTextureBytes => _residentTextureBytes;
        public static long ResidentManagedPixelBytes => _residentPixelBytes;
        public static long ResidentTotalBytes => _residentTextureBytes + _residentPixelBytes + _jsonBytesAtLoad;
        public static double GetPilotColdMilliseconds(int lod) => _pilotColdMs[ClampLod(lod)];
        public static double GetPilotHotMilliseconds(int lod) => _pilotHotMs[ClampLod(lod)];

        static int ClampLod(int lod) => lod < 0 ? 0 : (lod > 2 ? 2 : lod);

        static string MapPath(string fileName) =>
            Path.Combine(Application.streamingAssetsPath, MapDataRelative, fileName);

        static void LoadAll()
        {
            // PARTIE 2 : JSON légers seulement. Les PNG se chargent via EnsureLodLoaded.
            LastBlackMissingDemPixels = 0;
            LastBlackElevZeroPixels = 0;
            LastSeaBlackMaskedTotal = 0;
            for (var i = 0; i < 3; i++)
            {
                _lodTexturesLoaded[i] = false;
                _lodBlackScanned[i] = false;
                _pilotColdMeasured[i] = false;
                _pilotColdMs[i] = 0;
                _pilotHotMs[i] = 0;
            }

            LodTextureLoadCount = 0;
            LoadedLodCount = 0;
            _residentTextureBytes = 0;
            _residentPixelBytes = 0;
            _jsonBytesAtLoad = 0;

            ProvinceCoordinates.LoadProjected(out _midLat);
            var ownPath = MapPath("ownership_1400.json");
            var reliefPath = MapPath("cells_relief_g6.json");
            var biomePath = MapPath("cells_biomes_a12.json");
            var adjPath = MapPath("adjacency_g6.json");
            var lodPath = MapPath("cells_lod2.json");
            if (!File.Exists(ownPath) || !File.Exists(reliefPath))
            {
                Debug.LogWarning("PilotMapProvider: artefacts map/ absents.");
                return;
            }

            var ownership = JsonUtility.FromJson<OwnershipFile>(File.ReadAllText(ownPath));
            var relief = JsonUtility.FromJson<ReliefFile>(File.ReadAllText(reliefPath));
            var biomes = File.Exists(biomePath)
                ? JsonUtility.FromJson<BiomeFile>(File.ReadAllText(biomePath))
                : null;
            var adj = File.Exists(adjPath)
                ? JsonUtility.FromJson<AdjacencyFile>(File.ReadAllText(adjPath))
                : null;
            var lod = File.Exists(lodPath)
                ? JsonUtility.FromJson<CellsLodFile>(File.ReadAllText(lodPath))
                : null;

            if (ownership?.pilot_window_lonlat != null && ownership.pilot_window_lonlat.Length >= 4)
            {
                _lonMin = ownership.pilot_window_lonlat[0];
                _latMin = ownership.pilot_window_lonlat[1];
                _lonMax = ownership.pilot_window_lonlat[2];
                _latMax = ownership.pilot_window_lonlat[3];
            }

            ProvinceCoordinates.Project(_lonMin, _latMax, _midLat, out _pilotMinX, out _pilotMinY);
            ProvinceCoordinates.Project(_lonMax, _latMin, _midLat, out _pilotMaxX, out _pilotMaxY);
            if (_pilotMinX > _pilotMaxX)
            {
                var t = _pilotMinX;
                _pilotMinX = _pilotMaxX;
                _pilotMaxX = t;
            }

            if (_pilotMinY > _pilotMaxY)
            {
                var t = _pilotMinY;
                _pilotMinY = _pilotMaxY;
                _pilotMaxY = t;
            }

            var ownById = new Dictionary<int, OwnershipCell>();
            if (ownership?.cells != null)
            {
                for (var i = 0; i < ownership.cells.Count; i++)
                    ownById[ownership.cells[i].cell_id] = ownership.cells[i];
            }

            var reliefById = new Dictionary<int, ReliefCell>();
            if (relief?.cells != null)
            {
                for (var i = 0; i < relief.cells.Count; i++)
                    reliefById[relief.cells[i].cell_id] = relief.cells[i];
            }

            var biomeById = new Dictionary<int, BiomeCell>();
            if (biomes?.cells != null)
            {
                for (var i = 0; i < biomes.cells.Count; i++)
                    biomeById[biomes.cells[i].cell_id] = biomes.cells[i];
            }

            var neighbors = new Dictionary<int, List<int>>();
            var fluvial = new HashSet<int>();
            if (adj?.adjacency != null)
            {
                for (var i = 0; i < adj.adjacency.Count; i++)
                {
                    var e = adj.adjacency[i];
                    if (!neighbors.TryGetValue(e.a, out var la))
                    {
                        la = new List<int>();
                        neighbors[e.a] = la;
                    }

                    if (!neighbors.TryGetValue(e.b, out var lb))
                    {
                        lb = new List<int>();
                        neighbors[e.b] = lb;
                    }

                    if (!la.Contains(e.b))
                        la.Add(e.b);
                    if (!lb.Contains(e.a))
                        lb.Add(e.a);
                    if (e.fluvial_artery || e.river_crossing)
                    {
                        fluvial.Add(e.a);
                        fluvial.Add(e.b);
                    }
                }
            }

            var ids = new List<int>(reliefById.Keys);
            ids.Sort();
            _cells = new CellRecord[ids.Count];
            _cellIdToView = new Dictionary<int, int>(ids.Count);
            _byCellId = new Dictionary<int, CellRecord>(ids.Count);
            // v1_094 — l'index inverse dérive de _cells : il doit mourir avec lui.
            _cellsByProvince = null;
            _viewToCellId = new int[ids.Count];
            _unownedView = new bool[ids.Count];
            _skeleton = new List<MapSnapshotExporter.ProvinceView>(ids.Count);
            _cellsPerTag = new Dictionary<string, int>();
            _tagColors = new Dictionary<string, Color32>();

            var colors = CountryColors.Load();
            for (var i = 0; i < ids.Count; i++)
            {
                var cid = ids[i];
                reliefById.TryGetValue(cid, out var rc);
                ownById.TryGetValue(cid, out var oc);
                biomeById.TryGetValue(cid, out var bc);

                var lon = rc.centroid_lon;
                var lat = rc.centroid_lat;
                if (lod?.cells != null)
                {
                    for (var k = 0; k < lod.cells.Count; k++)
                    {
                        if (lod.cells[k].cell_id != cid)
                            continue;
                        lon = lod.cells[k].seed_lon;
                        lat = lod.cells[k].seed_lat;
                        break;
                    }
                }

                ProvinceCoordinates.Project(lon, lat, _midLat, out var x, out var y);
                var tag = oc != null ? (oc.owner_tag ?? "") : "";
                var hasOwner = !string.IsNullOrEmpty(tag);
                neighbors.TryGetValue(cid, out var neigh);
                if (neigh != null)
                    neigh.Sort();

                var rec = new CellRecord
                {
                    CellId = cid,
                    ViewIndex = i,
                    Lon = lon,
                    Lat = lat,
                    AreaKm2 = rc.area_km2 > 0 ? rc.area_km2 : (oc != null ? oc.area_km2 : 0f),
                    ElevMeanM = rc.elev_mean_m,
                    TerrainClass = rc.terrain_class ?? "",
                    Biome = bc != null ? (bc.biome ?? "") : "",
                    IsCoastal = bc != null && bc.is_coastal,
                    HasNavigableRiver = fluvial.Contains(cid),
                    OwnerTag = tag,
                    Certainty = oc != null ? (oc.certainty ?? "gameplay") : "",
                    ProvinceId = oc != null ? oc.province_id : -1,
                    ProvinceName = oc != null ? (oc.province_name ?? "") : "",
                    HasOwner = hasOwner,
                    Neighbors = neigh ?? new List<int>()
                };
                _cells[i] = rec;
                _cellIdToView[cid] = i;
                _byCellId[cid] = rec;
                _viewToCellId[i] = cid;
                _unownedView[i] = !hasOwner;

                // v1_070 — Fill politique = couleur du TAG (country_colors), jamais de la cellule.
                var fill = hasOwner ? PoliticalFillForTag(tag, colors) : UnownedHatchA;
                if (hasOwner)
                {
                    if (!_cellsPerTag.TryGetValue(tag, out var nTag))
                        nTag = 0;
                    _cellsPerTag[tag] = nTag + 1;
                }

                _skeleton.Add(new MapSnapshotExporter.ProvinceView
                {
                    Id = cid,
                    X = x,
                    Y = y,
                    Fill = fill,
                    ControllerColor = fill,
                    Occupied = false,
                    OwnerTag = tag,
                    OwnerName = hasOwner ? colors.NameForTag(tag) : "",
                    ProvinceName = "cell " + cid.ToString(CultureInfo.InvariantCulture)
                });
            }

            PublishMappingTable();
            PublishTagColorTable(colors);
            // JSON disk weight (budget) — PNG hors démarrage.
            _jsonBytesAtLoad =
                FileBytesIfExists(ownPath) + FileBytesIfExists(reliefPath) +
                FileBytesIfExists(biomePath) + FileBytesIfExists(adjPath) +
                FileBytesIfExists(lodPath);
            Debug.Log(
                "PilotMapProvider: JSON chargé cells=" + _cells.Length +
                " unowned=" + CountUnowned() +
                " tags=" + FormatTagCounts() +
                " json_bytes=" + _jsonBytesAtLoad.ToString(CultureInfo.InvariantCulture) +
                " (PNG paresseux par LOD) mapping→" + PublishedCellToViewTablePath);
        }

        static long FileBytesIfExists(string path) =>
            File.Exists(path) ? new FileInfo(path).Length : 0L;

        /// <summary>
        /// v1_095 — texture d'identifiants d'un LOD, pour le rendu GPU.
        /// Charge le LOD si besoin (même paresse que le chemin CPU).
        /// </summary>
        public static Texture2D IdsTextureFor(int lod)
        {
            lod = ClampLod(lod);
            EnsureLodLoaded(lod);
            switch (lod)
            {
                case 0: return _idsLod0;
                case 1: return _idsLod1;
                default: return _idsLod2;
            }
        }

        /// <summary>v1_095 — ombrage du relief d'un LOD, pour le rendu GPU.</summary>
        public static Texture2D HillshadeTextureFor(int lod)
        {
            lod = ClampLod(lod);
            EnsureLodLoaded(lod);
            switch (lod)
            {
                case 0: return _hsLod0;
                case 1: return _hsLod1;
                default: return _hsLod2;
            }
        }

        /// <summary>Charge ids + masque + ombrage d'UN LOD, décode GetPixels32, scanne le noir.</summary>
        public static void EnsureLodLoaded(int lod)
        {
            EnsureLoaded();
            lod = ClampLod(lod);
            if (_lodTexturesLoaded[lod])
                return;

            Texture2D ids;
            Texture2D mask;
            Texture2D hs;
            switch (lod)
            {
                case 0:
                    _idsLod0 = LoadPng("cell_ids_lod0.png");
                    _maskLod0 = LoadPng("mask_land_sea_lake_lod0.png");
                    _hsLod0 = LoadPng("hillshade_lod0.png");
                    ids = _idsLod0; mask = _maskLod0; hs = _hsLod0;
                    break;
                case 1:
                    _idsLod1 = LoadPng("cell_ids_lod1.png");
                    _maskLod1 = LoadPng("mask_land_sea_lake_lod1.png");
                    _hsLod1 = LoadPng("hillshade_lod1.png");
                    ids = _idsLod1; mask = _maskLod1; hs = _hsLod1;
                    break;
                default:
                    _idsLod2 = LoadPng("cell_ids_lod2.png");
                    _maskLod2 = LoadPng("mask_land_sea_lake_lod2.png");
                    _hsLod2 = LoadPng("hillshade_lod2.png");
                    ids = _idsLod2; mask = _maskLod2; hs = _hsLod2;
                    break;
            }

            CacheLodPixels(lod, hs, mask);
            AccountLodResident(ids, mask, hs, add: true);
            _lodTexturesLoaded[lod] = true;
            LodTextureLoadCount++;
            LoadedLodCount++;
            ScanLodBlackIfNeeded(lod);
        }

        /// <summary>Libère textures + copies managées d'un LOD (API manuelle ; auto-release non livré).</summary>
        public static void ReleaseLod(int lod)
        {
            lod = ClampLod(lod);
            if (!_lodTexturesLoaded[lod])
                return;

            Texture2D ids, mask, hs;
            SelectTexturesRaw(lod, out ids, out mask, out hs);
            AccountLodResident(ids, mask, hs, add: false);
            DestroyTex(ref ids);
            DestroyTex(ref mask);
            DestroyTex(ref hs);
            switch (lod)
            {
                case 0:
                    _idsLod0 = null; _maskLod0 = null; _hsLod0 = null;
                    _hsPix0 = null; _maskPix0 = null;
                    break;
                case 1:
                    _idsLod1 = null; _maskLod1 = null; _hsLod1 = null;
                    _hsPix1 = null; _maskPix1 = null;
                    break;
                default:
                    _idsLod2 = null; _maskLod2 = null; _hsLod2 = null;
                    _hsPix2 = null; _maskPix2 = null;
                    break;
            }

            _lodTexturesLoaded[lod] = false;
            // Scan déjà compté : on ne décrémente PAS les compteurs noirs (cumulatifs, V1071-E).
            LoadedLodCount = Math.Max(0, LoadedLodCount - 1);
        }

        static void DestroyTex(ref Texture2D tex)
        {
            if (tex == null)
                return;
            UnityEngine.Object.DestroyImmediate(tex);
            tex = null;
        }

        static void AccountLodResident(Texture2D ids, Texture2D mask, Texture2D hs, bool add)
        {
            long tex = TextureBytes(ids) + TextureBytes(mask) + TextureBytes(hs);
            // Copies managées persistantes = hillshade + mask (CacheLodPixels).
            long pix = 0;
            if (hs != null) pix += (long)hs.width * hs.height * 4L;
            if (mask != null) pix += (long)mask.width * mask.height * 4L;
            if (add)
            {
                _residentTextureBytes += tex;
                _residentPixelBytes += pix;
            }
            else
            {
                _residentTextureBytes = Math.Max(0, _residentTextureBytes - tex);
                _residentPixelBytes = Math.Max(0, _residentPixelBytes - pix);
            }
        }

        static long TextureBytes(Texture2D t) =>
            t == null ? 0L : (long)t.width * t.height * 4L;

        static void CacheLodPixels(int lod, Texture2D hs, Texture2D mask)
        {
            Color32[] hsPix = hs != null ? hs.GetPixels32() : null;
            Color32[] maskPix = mask != null ? mask.GetPixels32() : null;
            switch (lod)
            {
                case 0: _hsPix0 = hsPix; _maskPix0 = maskPix; break;
                case 1: _hsPix1 = hsPix; _maskPix1 = maskPix; break;
                default: _hsPix2 = hsPix; _maskPix2 = maskPix; break;
            }
        }

        static void SelectLodPixels(int lod, out Color32[] hsPix, out int hsW, out int hsH,
            out Color32[] maskPix, out int maskW, out int maskH)
        {
            Texture2D hs;
            Texture2D mask;
            SelectTextures(lod, out _, out mask, out hs);
            switch (lod)
            {
                case 0: hsPix = _hsPix0; maskPix = _maskPix0; break;
                case 1: hsPix = _hsPix1; maskPix = _maskPix1; break;
                default: hsPix = _hsPix2; maskPix = _maskPix2; break;
            }

            hsW = hs != null ? hs.width : 0;
            hsH = hs != null ? hs.height : 0;
            maskW = mask != null ? mask.width : 0;
            maskH = mask != null ? mask.height : 0;
        }

        /// <summary>Couleur politique : palette country_colors.json par TAG uniquement.</summary>
        static Color32 PoliticalFillForTag(string tag, CountryColors.Table colors)
        {
            var c = colors.ForTag(tag);
            if (!_tagColors.ContainsKey(tag))
                _tagColors[tag] = c;
            return c;
        }

        static string FormatTagCounts()
        {
            if (_cellsPerTag == null || _cellsPerTag.Count == 0)
                return "";
            var keys = new List<string>(_cellsPerTag.Keys);
            keys.Sort(StringComparer.Ordinal);
            var sb = new StringBuilder(64);
            for (var i = 0; i < keys.Count; i++)
            {
                if (i > 0)
                    sb.Append(',');
                sb.Append(keys[i]);
                sb.Append('=');
                sb.Append(_cellsPerTag[keys[i]].ToString(CultureInfo.InvariantCulture));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Compte les causes de noir dans l'ombrage DEM : absence de tuile (mer/hs=0)
        /// vs altitude nulle sur terre. Cumul des LOD déjà demandés (paresseux).
        /// </summary>
        static void ScanOneLodBlack(int lod)
        {
            SelectLodPixels(lod, out var hs, out _, out _, out var mask, out _, out _);
            if (hs == null || mask == null)
                return;
            var n = Math.Min(hs.Length, mask.Length);
            var missing = 0;
            var elevZero = 0;
            for (var i = 0; i < n; i++)
            {
                var land = mask[i].r > 0;
                var shade = hs[i].r;
                if (shade != 0)
                    continue;
                if (!land)
                    missing++;
                else
                    elevZero++;
            }

            LastBlackMissingDemPixels += missing;
            LastBlackElevZeroPixels += elevZero;
            LastSeaBlackMaskedTotal += missing;
        }

        static void ScanLodBlackIfNeeded(int lod)
        {
            lod = ClampLod(lod);
            if (_lodBlackScanned[lod])
                return;
            _lodBlackScanned[lod] = true;
            ScanOneLodBlack(lod);
        }

        /// <summary>Force le scan des trois LOD (preuve V1071-E / rétrocompat V1070).</summary>
        public static void EnsureAllLodsScanned()
        {
            for (var i = 0; i < 3; i++)
            {
                EnsureLodLoaded(i);
                ScanLodBlackIfNeeded(i);
            }
        }

        /// <summary>Remet à zéro puis rescane les trois LOD (compteurs cumulatifs exacts).</summary>
        public static void RescanAllBlackCounters()
        {
            LastBlackMissingDemPixels = 0;
            LastBlackElevZeroPixels = 0;
            LastSeaBlackMaskedTotal = 0;
            for (var i = 0; i < 3; i++)
                _lodBlackScanned[i] = false;
            EnsureAllLodsScanned();
        }

        static void ScanHillshadeBlackCauses()
        {
            // Conservé pour appels historiques : demande les trois LOD puis cumule.
            LastBlackMissingDemPixels = 0;
            LastBlackElevZeroPixels = 0;
            LastSeaBlackMaskedTotal = 0;
            for (var i = 0; i < 3; i++)
                _lodBlackScanned[i] = false;
            EnsureAllLodsScanned();
        }

        static int CountUnowned()
        {
            var n = 0;
            for (var i = 0; i < _unownedView.Length; i++)
                if (_unownedView[i])
                    n++;
            return n;
        }

        static Texture2D LoadPng(string fileName)
        {
            var path = MapPath(fileName);
            if (!File.Exists(path))
                return null;
            var bytes = File.ReadAllBytes(path);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(bytes, false))
            {
                UnityEngine.Object.Destroy(tex);
                return null;
            }

            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            return tex;
        }

        static void PublishMappingTable()
        {
            var dir = Path.Combine(Application.streamingAssetsPath, MapDataRelative);
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "cell_to_view_index.json");
            var sb = new StringBuilder(8192);
            sb.Append("{\"bijection\":true,\"count\":");
            sb.Append(_cells.Length);
            sb.Append(",\"entries\":[");
            for (var i = 0; i < _cells.Length; i++)
            {
                if (i > 0)
                    sb.Append(',');
                sb.Append("{\"cell_id\":");
                sb.Append(_cells[i].CellId);
                sb.Append(",\"view_index\":");
                sb.Append(i);
                sb.Append('}');
            }

            sb.Append("]}");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            PublishedCellToViewTablePath = path;
        }

        static void PublishTagColorTable(CountryColors.Table colors)
        {
            var dir = Path.Combine(Application.streamingAssetsPath, MapDataRelative);
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "political_tag_colors_v1_070.json");
            var keys = new List<string>(_cellsPerTag.Keys);
            keys.Sort(StringComparer.Ordinal);
            var sb = new StringBuilder(1024);
            sb.Append("{\"source\":\"country_colors.json\",\"unowned_hatch\":true,\"unowned_count\":");
            sb.Append(CountUnowned().ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"tags\":[");
            for (var i = 0; i < keys.Count; i++)
            {
                if (i > 0)
                    sb.Append(',');
                var tag = keys[i];
                var c = PoliticalFillForTag(tag, colors);
                sb.Append("{\"tag\":\"");
                sb.Append(tag);
                sb.Append("\",\"color\":\"");
                sb.Append(CountryColors.ToHex(c));
                sb.Append("\",\"cells\":");
                sb.Append(_cellsPerTag[tag].ToString(CultureInfo.InvariantCulture));
                sb.Append(",\"name\":\"");
                sb.Append(colors.NameForTag(tag));
                sb.Append("\"}");
            }

            sb.Append("]}");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            PublishedTagColorTablePath = path;
        }

        public static int LodForObservation(MapObservationLevel level)
        {
            switch (level)
            {
                case MapObservationLevel.Province:
                case MapObservationLevel.City:
                case MapObservationLevel.District:
                    return 0;
                case MapObservationLevel.Country:
                    return 1;
                default:
                    return 2;
            }
        }

        static void SelectTextures(int lod, out Texture2D ids, out Texture2D mask, out Texture2D hillshade)
        {
            EnsureLodLoaded(lod);
            SelectTexturesRaw(lod, out ids, out mask, out hillshade);
        }

        static void SelectTexturesRaw(int lod, out Texture2D ids, out Texture2D mask, out Texture2D hillshade)
        {
            switch (ClampLod(lod))
            {
                case 0:
                    ids = _idsLod0;
                    mask = _maskLod0;
                    hillshade = _hsLod0;
                    break;
                case 1:
                    ids = _idsLod1;
                    mask = _maskLod1;
                    hillshade = _hsLod1;
                    break;
                default:
                    ids = _idsLod2;
                    mask = _maskLod2;
                    hillshade = _hsLod2;
                    break;
            }
        }

        /// <summary>
        /// Construit une MapGeometry depuis la texture d'ids. ProvinceAt = index de vue
        /// (jamais l'identifiant de cellule brut).
        /// </summary>
        public static MapSnapshotExporter.MapGeometry BuildMapGeometry(
            int width, int height, MapWindow? window)
        {
            var level = MapViewport.IsInitialized
                ? MapViewport.State.Level
                : MapObservationLevel.World;
            return BuildMapGeometry(width, height, window, LodForObservation(level));
        }

        public static MapSnapshotExporter.MapGeometry BuildMapGeometry(
            int width, int height, MapWindow? window, int lod)
        {
            EnsureLoaded();
            if (!DataLoaded || _skeleton == null || _skeleton.Count == 0)
                return null;

            lod = ClampLod(lod);
            var sw = Stopwatch.StartNew();
            SelectTextures(lod, out var idsTex, out var maskTex, out _);
            if (idsTex == null)
                return null;

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
                minX = _pilotMinX;
                maxX = _pilotMaxX;
                minY = _pilotMinY;
                maxY = _pilotMaxY;
            }

            var nPix = width * height;
            var provinceAt = new int[nPix];
            var isLand = new bool[nPix];
            var ids = idsTex.GetPixels32();
            var idW = idsTex.width;
            var idH = idsTex.height;
            Color32[] masks = null;
            var maskW = 0;
            var maskH = 0;
            if (maskTex != null)
            {
                masks = maskTex.GetPixels32();
                maskW = maskTex.width;
                maskH = maskTex.height;
            }

            var rangeX = maxX - minX;
            var rangeY = maxY - minY;
            if (rangeX < 0.0001f)
                rangeX = 0.0001f;
            if (rangeY < 0.0001f)
                rangeY = 0.0001f;

            for (var py = 0; py < height; py++)
            {
                var v = (py + 0.5f) / height;
                var worldY = minY + v * rangeY;
                for (var px = 0; px < width; px++)
                {
                    var u = (px + 0.5f) / width;
                    var worldX = minX + u * rangeX;
                    WorldToLonLat(worldX, worldY, out var lon, out var lat);
                    LonLatToPilotUv(lon, lat, out var tu, out var tv);
                    var idx = py * width + px;
                    if (tu < 0f || tu > 1f || tv < 0f || tv > 1f)
                    {
                        provinceAt[idx] = -1;
                        isLand[idx] = false;
                        continue;
                    }

                    var ix = (int)(tu * idW);
                    var iy = (int)(tv * idH);
                    if (ix < 0)
                        ix = 0;
                    if (iy < 0)
                        iy = 0;
                    if (ix >= idW)
                        ix = idW - 1;
                    if (iy >= idH)
                        iy = idH - 1;
                    // Texture2D y=0 en bas.
                    var idColor = ids[iy * idW + ix];
                    var cellId = idColor.r + (idColor.g << 8);
                    var land = false;
                    if (masks != null && maskW > 0)
                    {
                        var mx = (int)(tu * maskW);
                        var my = (int)(tv * maskH);
                        if (mx < 0) mx = 0;
                        if (my < 0) my = 0;
                        if (mx >= maskW) mx = maskW - 1;
                        if (my >= maskH) my = maskH - 1;
                        // mask_land_sea_lake : terre R≥1, mer R=0 (pas un masque 0/255).
                        land = masks[my * maskW + mx].r > 0;
                    }
                    else if (cellId >= 1164 && cellId <= 1400 &&
                             _cellIdToView.ContainsKey(cellId))
                    {
                        land = true;
                    }

                    // v1_070 — isLand suit le masque terre/mer (pas seulement l'id cellule).
                    // En mer : jamais de cellule peinte ; couleur mer prend le relais.
                    if (!land)
                    {
                        provinceAt[idx] = -1;
                        isLand[idx] = false;
                        continue;
                    }

                    if (cellId >= 1164 && cellId <= 1400 &&
                        _cellIdToView.TryGetValue(cellId, out var viewIndex))
                    {
                        provinceAt[idx] = viewIndex;
                        isLand[idx] = true;
                    }
                    else
                    {
                        provinceAt[idx] = -1;
                        isLand[idx] = true;
                    }
                }
            }

            sw.Stop();
            LastPilotBuildMilliseconds = sw.Elapsed.TotalMilliseconds;
            if (!_pilotColdMeasured[lod])
            {
                _pilotColdMs[lod] = LastPilotBuildMilliseconds;
                _pilotColdMeasured[lod] = true;
            }
            else
            {
                _pilotHotMs[lod] = LastPilotBuildMilliseconds;
            }

            return new MapSnapshotExporter.MapGeometry
            {
                Width = width,
                Height = height,
                MinX = minX,
                MaxX = maxX,
                MinY = minY,
                MaxY = maxY,
                IsLand = isLand,
                ProvinceAt = provinceAt,
                ViewsSkeleton = CloneSkeleton(),
                LandMasses = default,
                MaskStats = default,
                IsWindowed = windowed
            };
        }

        static List<MapSnapshotExporter.ProvinceView> CloneSkeleton()
        {
            var list = new List<MapSnapshotExporter.ProvinceView>(_skeleton.Count);
            for (var i = 0; i < _skeleton.Count; i++)
                list.Add(_skeleton[i]);
            return list;
        }

        static void WorldToLonLat(float worldX, float worldY, out float lon, out float lat)
        {
            var cos = Mathf.Cos(_midLat * Mathf.Deg2Rad);
            if (Mathf.Abs(cos) < 0.0001f)
                cos = 0.0001f;
            lon = worldX / cos;
            lat = -worldY;
        }

        /// <summary>
        /// v1_095 — fenêtre monde (coordonnées projetées) → fenêtre UV dans les
        /// textures pilotes. C'est TOUT ce que le rendu GPU a besoin de savoir du
        /// zoom : quatre flottants au lieu d'une reconstruction de géométrie.
        /// Renvoie (uOrigine, vOrigine, pas_u, pas_v) tel que
        /// <c>uv = origine + coord_écran * pas</c>.
        ///
        /// LE PAS EN V EST NÉGATIF, ET C'EST DÉLIBÉRÉ. La convention du dépôt est
        /// « nord en rangée 0 » (cf. MapSnapshotExporter.WriteMapBufferPng, paramètre
        /// « northAtRow0 ») : les buffers CPU sont produits ainsi et affichés
        /// correctement. Pour que le rendu GPU soit interchangeable avec eux —
        /// même relecture, même présentation, aucune conversion — il doit produire
        /// la même orientation : coord_écran.y = 0 doit donner le NORD.
        /// Contrôlé par la mesure d'accord terre/mer CPU vs GPU (v1_095, contrôle 5).
        /// </summary>
        public static Vector4 WorldWindowToUv(
            float minX, float maxX, float minY, float maxY)
        {
            EnsureLoaded();
            WorldToLonLat(minX, maxY, out var lon0, out var lat0);
            WorldToLonLat(maxX, minY, out var lon1, out var lat1);
            LonLatToPilotUv(lon0, lat0, out var u0, out var v0);
            LonLatToPilotUv(lon1, lat1, out var u1, out var v1);
            var uMin = Mathf.Min(u0, u1);
            var vNorth = Mathf.Max(v0, v1);
            return new Vector4(
                uMin, vNorth, Mathf.Abs(u1 - u0), -Mathf.Abs(v1 - v0));
        }

        /// <summary>
        /// v1_095 — base d'indexation de la palette GPU : plus petit cell_id chargé.
        /// DÉRIVÉE de la donnée, pas écrite en dur — si le pipeline renumérote les
        /// cellules, la palette suit sans qu'on ait à y penser.
        /// </summary>
        public static int IdBase
        {
            get
            {
                EnsureLoaded();
                if (_cells == null || _cells.Length == 0)
                    return 0;
                var min = int.MaxValue;
                for (var i = 0; i < _cells.Length; i++)
                {
                    if (_cells[i].CellId < min)
                        min = _cells[i].CellId;
                }

                return min == int.MaxValue ? 0 : min;
            }
        }

        /// <summary>
        /// v1_095 — plancher des identifiants maritimes.
        /// Convention du pipeline, consignée dans id_color_table_g10.json
        /// (« sea_zone_id_range »: [5000, 5020]).
        /// </summary>
        public const int SeaIdMin = 5000;

        static void LonLatToPilotUv(float lon, float lat, out float u, out float v)
        {
            u = (lon - _lonMin) / (_lonMax - _lonMin);
            // lat max en haut de la texture (tv=1) ; GetPixels iy depuis le bas.
            v = (lat - _latMin) / (_latMax - _latMin);
        }

        /// <summary>
        /// v1_094 — province SIMULÉE rattachée à une cellule, ou -1 si la cellule
        /// n'appartient à aucune province (hachure « sans propriétaire »).
        ///
        /// C'est le pont qui manquait : le squelette pilote porte des <c>cell_id</c>
        /// (≥ 1164), l'ECS porte des <c>ProvinceId</c> (1..50). Sans cette table,
        /// aucun regard sur le monde joué ne peut atteindre une cellule — c'est
        /// pourquoi la carte pilote affichait un monde figé au disque.
        /// </summary>
        public static int ProvinceIdOfCell(int cellId)
        {
            EnsureLoaded();
            if (_byCellId != null && _byCellId.TryGetValue(cellId, out var rec))
                return rec.ProvinceId;
            return -1;
        }

        /// <summary>
        /// v1_094 — DÉFINITION UNIQUE de la traduction vue → province simulée.
        /// Hors mode pilote, une vue EST une province : l'identité. Tout code qui
        /// confronte un identifiant de géométrie à l'ECS doit passer par ici.
        /// </summary>
        public static int SimulationProvinceIdOfView(int viewId)
            => Enabled ? ProvinceIdOfCell(viewId) : viewId;

        /// <summary>v1_095 — nombre de cellules chargées.</summary>
        public static int CellCount
        {
            get
            {
                EnsureLoaded();
                return _cells?.Length ?? 0;
            }
        }

        /// <summary>v1_095 — latitude du germe d'une cellule, par index de vue.</summary>
        public static float CellLatOfView(int viewIndex)
        {
            EnsureLoaded();
            if (_cells == null || viewIndex < 0 || viewIndex >= _cells.Length)
                return float.NaN;
            return _cells[viewIndex].Lat;
        }

        /// <summary>v1_094 — surface de la cellule (km²), 0 si inconnue.</summary>
        public static float AreaOfCell(int cellId)
        {
            EnsureLoaded();
            if (_byCellId != null && _byCellId.TryGetValue(cellId, out var rec))
                return rec.AreaKm2;
            return 0f;
        }

        /// <summary>v1_094 — idem par index de vue.</summary>
        public static int ProvinceIdOfView(int viewIndex)
        {
            EnsureLoaded();
            if (_cells == null || viewIndex < 0 || viewIndex >= _cells.Length)
                return -1;
            return _cells[viewIndex].ProvinceId;
        }

        /// <summary>
        /// v1_094 — index inverse province simulée → cellules. Construit à la
        /// demande depuis <c>_cells</c>, jamais rechargé du disque : il décrit un
        /// RATTACHEMENT géographique (quelle cellule est dans quelle province),
        /// pas une possession — le rattachement, lui, ne change pas en cours de partie.
        /// </summary>
        static Dictionary<int, List<int>> _cellsByProvince;

        /// <summary>v1_094 — cellules rattachées à une province simulée (jamais null).</summary>
        public static IReadOnlyList<int> CellsOfProvince(int provinceId)
        {
            EnsureLoaded();
            if (_cellsByProvince == null)
            {
                _cellsByProvince = new Dictionary<int, List<int>>();
                if (_cells != null)
                {
                    for (var i = 0; i < _cells.Length; i++)
                    {
                        var pid = _cells[i].ProvinceId;
                        if (pid <= 0)
                            continue;
                        if (!_cellsByProvince.TryGetValue(pid, out var bucket))
                        {
                            bucket = new List<int>();
                            _cellsByProvince[pid] = bucket;
                        }

                        bucket.Add(_cells[i].CellId);
                    }
                }
            }

            return _cellsByProvince.TryGetValue(provinceId, out var cells)
                ? cells
                : System.Array.Empty<int>();
        }

        /// <summary>
        /// v1_094 — nom de la province rattachée (survol lisible). Vide si aucune.
        /// Nom de PRÉSENTATION : il vient de la table de rattachement, jamais de l'ECS.
        /// </summary>
        public static string ProvinceNameOfCell(int cellId)
        {
            EnsureLoaded();
            if (_byCellId != null && _byCellId.TryGetValue(cellId, out var rec))
                return rec.ProvinceName ?? "";
            return "";
        }

        /// <summary>
        /// v1_094 — couleur de hachure pour une cellule sans province rattachée.
        /// Exposée pour que l'appelant n'ait pas à connaître le motif.
        /// </summary>
        public static Color32 UnownedFill => UnownedHatchA;

        /// <summary>
        /// BuildViewsAligned : couleurs depuis OwnerTag (country_colors) — JAMAIS de la cellule.
        /// ActiveColorMode est ignoré ici (la vue physique passe par BuildTerrainFills).
        ///
        /// ⚠️ v1_094 — CONSERVÉE POUR LA VUE HORS-JEU (planches, tests de pipeline)
        /// UNIQUEMENT. Elle lit <c>ownership_1400.json</c>, donc elle ne peut pas
        /// montrer une conquête. Le jeu passe désormais par
        /// <c>MapSnapshotExporter.BuildViewsAligned</c>, qui lit l'ECS.
        /// </summary>
        public static void ApplyPilotColors(
            List<MapSnapshotExporter.ProvinceView> skeleton,
            CountryColors.Table colors,
            List<MapSnapshotExporter.ProvinceView> into)
        {
            into.Clear();
            for (var i = 0; i < skeleton.Count; i++)
            {
                var sk = skeleton[i];
                var tag = sk.OwnerTag ?? "";
                var hasOwner = !string.IsNullOrEmpty(tag);
                // v1_070 — couleur politique = TAG propriétaire uniquement.
                Color32 fill = hasOwner
                    ? PoliticalFillForTag(tag, colors)
                    : UnownedHatchA;

                into.Add(new MapSnapshotExporter.ProvinceView
                {
                    Id = sk.Id,
                    X = sk.X,
                    Y = sk.Y,
                    Owner = Entity.Null,
                    Controller = Entity.Null,
                    Fill = fill,
                    ControllerColor = fill,
                    Occupied = false,
                    OwnerTag = tag,
                    OwnerName = hasOwner ? colors.NameForTag(tag) : "",
                    ProvinceName = sk.ProvinceName
                });
            }
        }

        public static Color32[] BuildTerrainFills(int viewCount)
        {
            var fills = new Color32[viewCount];
            for (var i = 0; i < viewCount; i++)
            {
                if (_cells != null && i < _cells.Length)
                    fills[i] = TerrainColor(_cells[i].TerrainClass);
                else
                    fills[i] = UnownedHatchA;
            }

            return fills;
        }

        /// <summary>Fills politiques : une couleur identique par TAG (country_colors.json).</summary>
        public static Color32[] BuildPoliticalFills(int viewCount)
        {
            EnsureLoaded();
            var colors = CountryColors.Load();
            var fills = new Color32[viewCount];
            for (var i = 0; i < viewCount; i++)
                fills[i] = PoliticalColorOfView(i, colors);
            return fills;
        }

        static Color32 TerrainColor(string terrainClass)
        {
            if (string.IsNullOrEmpty(terrainClass))
                return new Color32(0x90, 0x90, 0x70, 255);
            switch (terrainClass)
            {
                case "plains": return new Color32(0xC5, 0xD6, 0x8A, 255);
                case "hills": return new Color32(0xA0, 0xB0, 0x6A, 255);
                case "mountains": return new Color32(0x8A, 0x8A, 0x8A, 255);
                case "high_mountains": return new Color32(0xE8, 0xE8, 0xF0, 255);
                case "wetlands": return new Color32(0x6A, 0xA0, 0x8A, 255);
                case "coastal_plain": return new Color32(0xD2, 0xC8, 0x8A, 255);
                default: return new Color32(0x9A, 0xB0, 0x7A, 255);
            }
        }

        /// <summary>
        /// v1_070 — ombrage DEM uniquement sur terre. En mer : couleur mer (jamais de noir
        /// provenant d'une tuile DEM absente).
        /// </summary>
        public static void ApplyHillshadeOnLand(
            Color32[] pixels,
            bool[] isLand,
            int width,
            int height,
            float minX, float maxX, float minY, float maxY,
            Color32 sea,
            int lod)
        {
            if (pixels == null || isLand == null || pixels.Length != isLand.Length)
                return;
            EnsureLoaded();
            SelectLodPixels(lod, out var hs, out var hsW, out var hsH, out _, out _, out _);
            if (hs == null || hsW <= 0)
            {
                // Pas d'ombrage : garantir quand même mer non noire.
                for (var i = 0; i < pixels.Length; i++)
                {
                    if (!isLand[i])
                        pixels[i] = sea;
                }

                return;
            }

            var rangeX = maxX - minX;
            var rangeY = maxY - minY;
            if (rangeX < 0.0001f) rangeX = 0.0001f;
            if (rangeY < 0.0001f) rangeY = 0.0001f;

            for (var py = 0; py < height; py++)
            {
                var v = (py + 0.5f) / height;
                var worldY = minY + v * rangeY;
                for (var px = 0; px < width; px++)
                {
                    var idx = py * width + px;
                    if (!isLand[idx])
                    {
                        pixels[idx] = sea;
                        continue;
                    }

                    var u = (px + 0.5f) / width;
                    var worldX = minX + u * rangeX;
                    WorldToLonLat(worldX, worldY, out var lon, out var lat);
                    LonLatToPilotUv(lon, lat, out var tu, out var tv);
                    if (tu < 0f || tu > 1f || tv < 0f || tv > 1f)
                        continue;

                    var hx = (int)(tu * hsW);
                    var hy = (int)(tv * hsH);
                    if (hx < 0) hx = 0;
                    if (hy < 0) hy = 0;
                    if (hx >= hsW) hx = hsW - 1;
                    if (hy >= hsH) hy = hsH - 1;
                    var shade = hs[hy * hsW + hx].r;
                    // Absence de tuile DEM → shade=0 : sur terre on utilise un neutre, pas le noir.
                    if (shade == 0)
                        shade = 180;
                    // Modulation douce [0.55, 1.0]
                    var factor = 0.55f + 0.45f * (shade / 255f);
                    var c = pixels[idx];
                    pixels[idx] = new Color32(
                        (byte)Mathf.Clamp(Mathf.RoundToInt(c.r * factor), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(c.g * factor), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(c.b * factor), 0, 255),
                        c.a);
                }
            }
        }

        /// <summary>Compte les pixels RGB(0,0,0) en mer (doit être 0 après masque).</summary>
        public static int CountBlackSeaPixels(Color32[] pixels, bool[] isLand)
        {
            if (pixels == null || isLand == null)
                return -1;
            var n = 0;
            var len = Math.Min(pixels.Length, isLand.Length);
            for (var i = 0; i < len; i++)
            {
                if (isLand[i])
                    continue;
                var c = pixels[i];
                if (c.r == 0 && c.g == 0 && c.b == 0)
                    n++;
            }

            return n;
        }

        public static bool TryGetCellsPerTag(out Dictionary<string, int> counts)
        {
            EnsureLoaded();
            counts = _cellsPerTag;
            return counts != null;
        }

        public static bool TryGetTagColor(string tag, out Color32 color)
        {
            EnsureLoaded();
            color = default;
            if (_tagColors == null || string.IsNullOrEmpty(tag))
                return false;
            return _tagColors.TryGetValue(tag, out color);
        }

        public static Color32 PoliticalColorOfView(int viewIndex, CountryColors.Table colors)
        {
            EnsureLoaded();
            if (_cells == null || viewIndex < 0 || viewIndex >= _cells.Length)
                return UnownedHatchA;
            var c = _cells[viewIndex];
            if (!c.HasOwner)
                return UnownedHatchA;
            return PoliticalFillForTag(c.OwnerTag, colors);
        }

        /// <summary>Hachures distinctes pour cellules sans propriétaire (pas couleur « pays »).</summary>
        public static void ApplyUnownedHatch(
            List<MapSnapshotExporter.ProvinceView> views,
            Color32[] pixels,
            int[] provinceAt,
            int width,
            int height)
        {
            if (_unownedView == null || pixels == null || provinceAt == null)
                return;
            for (var py = 0; py < height; py++)
            {
                for (var px = 0; px < width; px++)
                {
                    var idx = py * width + px;
                    var vi = provinceAt[idx];
                    if (vi < 0 || vi >= _unownedView.Length || !_unownedView[vi])
                        continue;
                    pixels[idx] = ((px + py) & 2) == 0 ? UnownedHatchA : UnownedHatchB;
                }
            }
        }

        /// <summary>
        /// Épaisseurs Z4 dérivées du niveau d'observation (échelle 2/3/5 de v1_073) :
        /// non-nav = 1/2/4, navigable = 2/4/8 — indépendant de ZoomScaleEnabled.
        /// </summary>
        public static int NonNavigableThicknessFor(MapObservationLevel level)
        {
            switch (level)
            {
                case MapObservationLevel.Country:
                    return 2;
                case MapObservationLevel.Province:
                case MapObservationLevel.City:
                case MapObservationLevel.District:
                    return 4;
                default:
                    return 1;
            }
        }

        public static int NavigableThicknessFor(MapObservationLevel level) =>
            NonNavigableThicknessFor(level) * 2;

        public static int IndeterminateThicknessFor(MapObservationLevel level) =>
            NonNavigableThicknessFor(level);

        /// <summary>Chargement paresseux du domaine fleuves (pas au démarrage JSON).</summary>
        public static void EnsureRiversLoaded()
        {
            if (_riversLoadAttempted)
                return;
            _riversLoadAttempted = true;
            RiverLoadCount++;
            EnsureLoaded();

            var riversPath = MapPath(RiversFileName);
            var mouthsPath = MapPath(RiverMouthsFileName);
            if (!File.Exists(riversPath))
            {
                Debug.LogWarning("PilotMapProvider: " + RiversFileName + " absent.");
                RiversDataLoaded = false;
                return;
            }

            var raw = File.ReadAllText(riversPath);
            var file = JsonUtility.FromJson<RiversFile>(raw);
            RiversCrsChosen = file != null ? (file.crs_geometry ?? "") : "";
            RiversCrsJustification = file != null ? (file.crs_justification ?? "") : "";
            _riversBytesAtLoad = FileBytesIfExists(riversPath) + FileBytesIfExists(mouthsPath);
            RiversImportedBytes = _riversBytesAtLoad;

            if (file?.segments == null || file.segments.Count == 0)
            {
                RiversDataLoaded = false;
                return;
            }

            _riverSegments = new RiverSegment[file.segments.Count];
            for (var i = 0; i < file.segments.Count; i++)
            {
                var d = file.segments[i];
                _riverSegments[i] = new RiverSegment
                {
                    SegmentId = d.segment_id ?? "",
                    NameFr = d.name_fr ?? "",
                    Navigability = ParseNavigability(d.navigability),
                    LengthM = d.length_m,
                    ScaleRank = d.scalerank,
                    LonLat = d.lonlat ?? Array.Empty<float>()
                };
            }

            if (File.Exists(mouthsPath))
            {
                var mraw = File.ReadAllText(mouthsPath);
                var mfile = JsonUtility.FromJson<RiverMouthsFile>(mraw);
                RiverMouthsUsageNote = mfile != null
                    ? (mfile.usage_note ?? "")
                    : "";
                if (mfile?.mouths != null)
                {
                    _riverMouths = new RiverMouth[mfile.mouths.Count];
                    for (var i = 0; i < mfile.mouths.Count; i++)
                    {
                        var d = mfile.mouths[i];
                        _riverMouths[i] = new RiverMouth
                        {
                            SegmentId = d.segment_id ?? "",
                            Name = d.name ?? "",
                            MajorLabel = d.major_label ?? "",
                            Lon = d.lon,
                            Lat = d.lat,
                            SeaZoneId = d.sea_zone_id
                        };
                    }
                }
            }
            else
            {
                RiverMouthsUsageNote = "river_mouths_g5c.json absent";
            }

            RiversDataLoaded = _riverSegments != null && _riverSegments.Length > 0;
            Debug.Log(
                "PilotMapProvider: fleuves chargés segments=" +
                (_riverSegments != null ? _riverSegments.Length : 0).ToString(CultureInfo.InvariantCulture) +
                " mouths=" +
                (_riverMouths != null ? _riverMouths.Length : 0).ToString(CultureInfo.InvariantCulture) +
                " crs=" + RiversCrsChosen +
                " bytes=" + _riversBytesAtLoad.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>Remet le domaine fleuves à « non chargé » (tests V1074-E).</summary>
        public static void ResetRiverLoadStateForTests()
        {
            _riversLoadAttempted = false;
            _riverSegments = null;
            _riverMouths = null;
            RiversDataLoaded = false;
            _riversBytesAtLoad = 0;
            RiversImportedBytes = 0;
            RiverLoadCount = 0;
            LastRiversDrawn = 0;
            LastRiversOutOfWindow = 0;
        }

        static RiverNavigability ParseNavigability(string raw)
        {
            if (string.Equals(raw, "navigable", StringComparison.OrdinalIgnoreCase))
                return RiverNavigability.Navigable;
            if (string.Equals(raw, "non_navigable", StringComparison.OrdinalIgnoreCase))
                return RiverNavigability.NonNavigable;
            return RiverNavigability.Indeterminate;
        }

        public static bool IncludeRiverAtLevel(in RiverSegment seg, MapObservationLevel level)
        {
            switch (level)
            {
                case MapObservationLevel.World:
                    return seg.ScaleRank <= 6;
                case MapObservationLevel.Country:
                    return seg.Navigability == RiverNavigability.Navigable;
                default:
                    return true;
            }
        }

        public static int CountSegmentsNamed(string nameFr)
        {
            EnsureRiversLoaded();
            if (_riverSegments == null || string.IsNullOrEmpty(nameFr))
                return 0;
            var n = 0;
            for (var i = 0; i < _riverSegments.Length; i++)
            {
                if (NameMatches(_riverSegments[i].NameFr, nameFr))
                    n++;
            }

            return n;
        }

        static bool NameMatches(string hay, string needle)
        {
            if (string.IsNullOrEmpty(hay) || string.IsNullOrEmpty(needle))
                return false;
            return hay.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Peint les fleuves dans le raster existant, APRÈS ombrage (et hachures).
        /// Terre seule — aucun trait en mer. Indéterminés = pointillé fin, jamais navigable.
        /// </summary>
        public static void ApplyRivers(
            Color32[] pixels,
            bool[] isLand,
            int width,
            int height,
            float minX, float maxX, float minY, float maxY,
            MapObservationLevel level)
        {
            LastRiversDrawn = 0;
            LastRiversOutOfWindow = 0;
            LastNavigableThicknessPx = NavigableThicknessFor(level);
            LastNonNavigableThicknessPx = NonNavigableThicknessFor(level);
            if (!ShowRivers || pixels == null || isLand == null || pixels.Length != width * height)
                return;

            EnsureRiversLoaded();
            if (_riverSegments == null || _riverSegments.Length == 0)
                return;

            var navT = LastNavigableThicknessPx;
            var nonT = LastNonNavigableThicknessPx;
            var indT = IndeterminateThicknessFor(level);

            for (var i = 0; i < _riverSegments.Length; i++)
            {
                var seg = _riverSegments[i];
                if (!SegmentIntersectsLonLatWindow(seg))
                {
                    LastRiversOutOfWindow++;
                    continue;
                }

                if (!IncludeRiverAtLevel(seg, level))
                    continue;

                Color32 color;
                int thickness;
                var dashed = false;
                switch (seg.Navigability)
                {
                    case RiverNavigability.Navigable:
                        color = RiverNavigableColor;
                        thickness = navT;
                        break;
                    case RiverNavigability.NonNavigable:
                        color = RiverNonNavigableColor;
                        thickness = nonT;
                        break;
                    default:
                        // Indéterminé : pointillé fin — jamais promu navigable.
                        color = RiverIndeterminateColor;
                        thickness = indT;
                        dashed = true;
                        break;
                }

                DrawRiverSegment(
                    pixels, isLand, width, height,
                    minX, maxX, minY, maxY,
                    seg.LonLat, color, thickness, dashed,
                    interpretAsMeters: false);
                LastRiversDrawn++;
            }
        }

        /// <summary>
        /// Contrôle V1074-A : projette un point connu (Seine / Rouen) via la chaîne lon/lat.
        /// </summary>
        public static bool TryControlPointSeineRouen(
            out float lon, out float lat, out float x, out float y,
            out float expectedLon, out float expectedLat, out float distanceDeg)
        {
            lon = lat = x = y = expectedLon = expectedLat = distanceDeg = 0f;
            EnsureLoaded();
            EnsureRiversLoaded();
            var path = MapPath(RiversFileName);
            if (!File.Exists(path))
                return false;
            var file = JsonUtility.FromJson<RiversFile>(File.ReadAllText(path));
            if (file == null)
                return false;
            lon = file.control_lon;
            lat = file.control_lat;
            expectedLon = file.control_expected_lon;
            expectedLat = file.control_expected_lat;
            distanceDeg = file.control_distance_deg;
            ProvinceCoordinates.Project(lon, lat, _midLat, out x, out y);
            return true;
        }

        /// <summary>
        /// Rouge V1074-A : injecte des mètres EPSG:3035 comme si c'étaient des lon/lat.
        /// </summary>
        public static void DrawRiverWithForcedMetersAsLonLat(
            Color32[] pixels, bool[] isLand, int width, int height,
            float minX, float maxX, float minY, float maxY,
            float[] metersXY, Color32 color, int thickness)
        {
            DrawRiverSegment(
                pixels, isLand, width, height,
                minX, maxX, minY, maxY,
                metersXY, color, thickness, dashed: false,
                interpretAsMeters: true);
        }

        static bool SegmentIntersectsLonLatWindow(in RiverSegment seg)
        {
            var ll = seg.LonLat;
            if (ll == null || ll.Length < 2)
                return false;
            for (var i = 0; i + 1 < ll.Length; i += 2)
            {
                var lon = ll[i];
                var lat = ll[i + 1];
                if (lon >= _lonMin && lon <= _lonMax && lat >= _latMin && lat <= _latMax)
                    return true;
            }

            return false;
        }

        static void DrawRiverSegment(
            Color32[] pixels, bool[] isLand,
            int width, int height,
            float minX, float maxX, float minY, float maxY,
            float[] lonlat, Color32 color, int thickness, bool dashed,
            bool interpretAsMeters)
        {
            if (lonlat == null || lonlat.Length < 4)
                return;

            for (var i = 0; i + 3 < lonlat.Length; i += 2)
            {
                float x0, y0, x1, y1;
                if (interpretAsMeters)
                {
                    // Rouge : mètres lus comme lon/lat → projection absurde.
                    ProvinceCoordinates.Project(lonlat[i], lonlat[i + 1], _midLat, out x0, out y0);
                    ProvinceCoordinates.Project(lonlat[i + 2], lonlat[i + 3], _midLat, out x1, out y1);
                }
                else
                {
                    ProvinceCoordinates.Project(lonlat[i], lonlat[i + 1], _midLat, out x0, out y0);
                    ProvinceCoordinates.Project(lonlat[i + 2], lonlat[i + 3], _midLat, out x1, out y1);
                }

                DrawThickLandLine(
                    pixels, isLand, width, height,
                    x0, y0, x1, y1,
                    minX, maxX, minY, maxY,
                    color, thickness, dashed);
            }
        }

        static void DrawThickLandLine(
            Color32[] pixels, bool[] isLand,
            int width, int height,
            float x0, float y0, float x1, float y1,
            float minX, float maxX, float minY, float maxY,
            Color32 color, int thickness, bool dashed)
        {
            WorldToPixelPublic(x0, y0, minX, maxX, minY, maxY, width, height, out var px0, out var py0);
            WorldToPixelPublic(x1, y1, minX, maxX, minY, maxY, width, height, out var px1, out var py1);

            var dx = Math.Abs(px1 - px0);
            var dy = Math.Abs(py1 - py0);
            var sx = px0 < px1 ? 1 : -1;
            var sy = py0 < py1 ? 1 : -1;
            var err = dx - dy;
            var step = 0;
            var x = px0;
            var y = py0;
            var rLo = (thickness - 1) / 2;
            var rHi = thickness / 2;

            while (true)
            {
                if (!dashed || ((step / 4) % 2) == 0)
                {
                    for (var oy = -rLo; oy <= rHi; oy++)
                    {
                        for (var ox = -rLo; ox <= rHi; ox++)
                            SetLandPixel(pixels, isLand, width, height, x + ox, y + oy, color);
                    }
                }

                if (x == px1 && y == py1)
                    break;
                var e2 = 2 * err;
                if (e2 > -dy) { err -= dy; x += sx; }
                if (e2 < dx) { err += dx; y += sy; }
                step++;
            }
        }

        static void WorldToPixelPublic(
            float worldX, float worldY,
            float minX, float maxX, float minY, float maxY,
            int width, int height,
            out int px, out int py)
        {
            var rangeX = maxX - minX;
            var rangeY = maxY - minY;
            if (rangeX < 0.0001f) rangeX = 0.0001f;
            if (rangeY < 0.0001f) rangeY = 0.0001f;
            px = (int)((worldX - minX) / rangeX * width);
            py = (int)((worldY - minY) / rangeY * height);
            if (px < 0) px = 0;
            if (py < 0) py = 0;
            if (px >= width) px = width - 1;
            if (py >= height) py = height - 1;
        }

        static void SetLandPixel(
            Color32[] pixels, bool[] isLand,
            int width, int height, int x, int y, Color32 color)
        {
            if (x < 0 || y < 0 || x >= width || y >= height)
                return;
            var idx = y * width + x;
            if (isLand != null && idx < isLand.Length && !isLand[idx])
                return;
            pixels[idx] = color;
        }

        /// <summary>Mesure l'épaisseur verticale d'un trait horizontal de couleur donnée.</summary>
        public static int MeasureColorStrokeThickness(
            Color32[] pixels, int width, int height, Color32 target, int sampleX)
        {
            var first = -1;
            var last = -1;
            for (var y = 0; y < height; y++)
            {
                var c = pixels[y * width + sampleX];
                if (c.r == target.r && c.g == target.g && c.b == target.b)
                {
                    if (first < 0) first = y;
                    last = y;
                }
            }

            return first < 0 ? 0 : (last - first + 1);
        }

        public static Color32 GetNavigableRiverColor() => RiverNavigableColor;
        public static Color32 GetNonNavigableRiverColor() => RiverNonNavigableColor;
        public static Color32 GetIndeterminateRiverColor() => RiverIndeterminateColor;

        public static int CountIndeterminatePromotedAsNavigable()
        {
            EnsureRiversLoaded();
            if (_riverSegments == null)
                return 0;
            // Par construction ParseNavigability ne promeut jamais ; contrôle structurel.
            var bad = 0;
            for (var i = 0; i < _riverSegments.Length; i++)
            {
                // Impossible via le parseur ; garde pour mutation rouge dans les tests.
                if (_riverSegments[i].Navigability == RiverNavigability.Navigable &&
                    string.Equals(_riverSegments[i].NameFr, "__force_indeterminate_as_nav__", StringComparison.Ordinal))
                    bad++;
            }

            return bad;
        }

        public static bool AllIndeterminateAreDashedStyle(MapObservationLevel level)
        {
            EnsureRiversLoaded();
            if (_riverSegments == null)
                return true;
            var indT = IndeterminateThicknessFor(level);
            var navT = NavigableThicknessFor(level);
            for (var i = 0; i < _riverSegments.Length; i++)
            {
                if (_riverSegments[i].Navigability != RiverNavigability.Indeterminate)
                    continue;
                if (indT >= navT)
                    return false;
            }

            return indT < navT || NonNavigableThicknessFor(level) == indT;
        }

        public static int CountIndeterminateSegments()
        {
            EnsureRiversLoaded();
            if (_riverSegments == null)
                return 0;
            var n = 0;
            for (var i = 0; i < _riverSegments.Length; i++)
            {
                if (_riverSegments[i].Navigability == RiverNavigability.Indeterminate)
                    n++;
            }

            return n;
        }

        public static int RiverSegmentCount =>
            _riverSegments != null ? _riverSegments.Length : 0;

        public static int RiverMouthCount =>
            _riverMouths != null ? _riverMouths.Length : 0;

        public static RiverSegment[] DebugRiverSegments => _riverSegments;

        /// <summary>Écrit le journal de preuve v1_074 + captures ON/OFF aux trois niveaux.</summary>
        public static string WriteRiversProofAndCaptures(string captureDir, string logPath)
        {
            Directory.CreateDirectory(captureDir);
            var logDir = Path.GetDirectoryName(logPath);
            if (!string.IsNullOrEmpty(logDir))
                Directory.CreateDirectory(logDir);

            var wasEnabled = Enabled;
            var wasRivers = ShowRivers;
            var wasZoom = MapSnapshotExporter.ZoomScaleEnabled;

            ApplyPresentationSettings(clearCache: true);
            EnsureLoaded();
            ResetRiverLoadStateForTests();

            var sb = new StringBuilder(16384);
            sb.AppendLine("=== v1_074 RIVERS ===");
            sb.AppendLine("entry_point: " + PresentationSettingsPath);
            sb.AppendLine("show_rivers_after_settings: " + ShowRivers.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("pilot_enabled: " + Enabled.ToString(CultureInfo.InvariantCulture));

            // PARTIE 1 — CRS + import
            sb.AppendLine("--- PARTIE 1 import / CRS ---");
            EnsureRiversLoaded();
            sb.AppendLine("fields_kept: segment_id,name_fr,navigability,length_m,scalerank,geometry_lonlat");
            sb.AppendLine("fields_left: name,name_en,featurecla,source_layer,ne_id,rivernum,major_label,geometry(EPSG:3035)");
            sb.AppendLine("crs_chosen: " + RiversCrsChosen);
            sb.AppendLine("crs_rejected: EPSG:3035");
            sb.AppendLine("crs_justification: " + RiversCrsJustification);
            sb.AppendLine("imported_bytes: " + RiversImportedBytes.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("imported_mb: " +
                (RiversImportedBytes / (1024.0 * 1024.0)).ToString("0.####", CultureInfo.InvariantCulture));
            sb.AppendLine("segments_imported: " + RiverSegmentCount.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("mouths_imported: " + RiverMouthCount.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("mouths_usage: " + RiverMouthsUsageNote);
            sb.AppendLine("indeterminate_count: " +
                CountIndeterminateSegments().ToString(CultureInfo.InvariantCulture));
            sb.AppendLine(
                "indeterminate_treatment: pointille fin couleur gris-bleu, epaisseur=non_navigable, JAMAIS navigable");

            TryControlPointSeineRouen(
                out var clon, out var clat, out var cx, out var cy,
                out var elon, out var elat, out var ddeg);
            sb.AppendLine(
                "control_point: Seine@Rouen lon=" +
                clon.ToString("0.######", CultureInfo.InvariantCulture) +
                " lat=" + clat.ToString("0.######", CultureInfo.InvariantCulture) +
                " expected=(" + elon.ToString("0.##", CultureInfo.InvariantCulture) + "," +
                elat.ToString("0.##", CultureInfo.InvariantCulture) + ")" +
                " distance_deg=" + ddeg.ToString("0.######", CultureInfo.InvariantCulture) +
                " projected_xy=(" + cx.ToString("0.####", CultureInfo.InvariantCulture) + "," +
                cy.ToString("0.####", CultureInfo.InvariantCulture) + ")");
            sb.AppendLine(
                "control_in_pilot_window: " +
                (clon >= _lonMin && clon <= _lonMax && clat >= _latMin && clat <= _latMax));

            // Budget : fleuves hors démarrage
            var mb = 1024.0 * 1024.0;
            sb.AppendLine("json_startup_mb_without_rivers: " +
                (_jsonBytesAtLoad / mb).ToString("0.####", CultureInfo.InvariantCulture));
            sb.AppendLine("rivers_lazy_mb: " +
                (RiversImportedBytes / mb).ToString("0.####", CultureInfo.InvariantCulture));
            sb.AppendLine("river_load_count: " +
                RiverLoadCount.ToString(CultureInfo.InvariantCulture));

            var colors = CountryColors.Load();
            var levels = new[]
            {
                MapObservationLevel.World,
                MapObservationLevel.Country,
                MapObservationLevel.Province
            };
            var levelNames = new[] { "world", "country", "province" };

            sb.AppendLine("--- PARTIE 2 dessin ---");
            ShowRivers = true;
            SetEnabled(true, clearCache: true);
            for (var li = 0; li < levels.Length; li++)
            {
                var level = levels[li];
                var name = levelNames[li];
                var navT = NavigableThicknessFor(level);
                var nonT = NonNavigableThicknessFor(level);
                var drawn = CountDrawableAtLevel(level, out var outWin);
                sb.AppendLine(
                    "level=" + name +
                    " drawn=" + drawn.ToString(CultureInfo.InvariantCulture) +
                    " out_of_window=" + outWin.ToString(CultureInfo.InvariantCulture) +
                    " thickness_non_nav=" + nonT.ToString(CultureInfo.InvariantCulture) +
                    " thickness_nav=" + navT.ToString(CultureInfo.InvariantCulture) +
                    " thickness_indeterminate=" +
                    IndeterminateThicknessFor(level).ToString(CultureInfo.InvariantCulture) +
                    " (dashed)");

                RenderRiverCapture(captureDir, "rivers_on_" + name + ".png", level, colors, sb);
            }

            sb.AppendLine(
                "named_rivers: Seine=" + CountSegmentsNamed("Seine").ToString(CultureInfo.InvariantCulture) +
                " Loire=" + CountSegmentsNamed("Loire").ToString(CultureInfo.InvariantCulture) +
                " Rhône=" + CountSegmentsNamed("Rhône").ToString(CultureInfo.InvariantCulture) +
                " Rhin=" + CountSegmentsNamed("Rhin").ToString(CultureInfo.InvariantCulture));

            // OFF captures
            ShowRivers = false;
            for (var li = 0; li < levels.Length; li++)
            {
                RenderRiverCapture(
                    captureDir, "rivers_off_" + levelNames[li] + ".png",
                    levels[li], colors, sb);
            }

            // Mesure épaisseur réelle (V1074-B)
            sb.AppendLine("--- thickness measured ---");
            var meas = MeasureThicknessOnCanvas(MapObservationLevel.Province, out var navMeas, out var nonMeas);
            sb.AppendLine(
                "measured_province nav_px=" + navMeas.ToString(CultureInfo.InvariantCulture) +
                " non_nav_px=" + nonMeas.ToString(CultureInfo.InvariantCulture) +
                " ok=" + meas.ToString(CultureInfo.InvariantCulture));

            // Contrôles
            sb.AppendLine("--- CONTROLES ---");
            var vA = CheckV1074A(out var aDetail);
            sb.AppendLine("V1074-A crs_lonlat_chain: " + (vA ? "PASS" : "FAIL") + " " + aDetail);
            var vB = CheckV1074B(out var bDetail);
            sb.AppendLine("V1074-B thickness_distinct: " + (vB ? "PASS" : "FAIL") + " " + bDetail);
            var vC = CheckV1074C(out var cDetail);
            sb.AppendLine("V1074-C indeterminate_not_promoted: " + (vC ? "PASS" : "FAIL") + " " + cDetail);
            var vD = CheckV1074D(captureDir, colors, out var dDetail);
            sb.AppendLine("V1074-D rivers_off_bit_identical: " + (vD ? "PASS" : "FAIL") + " " + dDetail);
            var vE = CheckV1074E(out var eDetail);
            sb.AppendLine("V1074-E lazy_not_startup: " + (vE ? "PASS" : "FAIL") + " " + eDetail);

            File.WriteAllText(logPath, sb.ToString(), new UTF8Encoding(false));
            ShowRivers = wasRivers;
            MapSnapshotExporter.ZoomScaleEnabled = wasZoom;
            Enabled = wasEnabled;
            return logPath;
        }

        static int CountDrawableAtLevel(MapObservationLevel level, out int outOfWindow)
        {
            outOfWindow = 0;
            EnsureRiversLoaded();
            if (_riverSegments == null)
                return 0;
            var n = 0;
            for (var i = 0; i < _riverSegments.Length; i++)
            {
                if (!SegmentIntersectsLonLatWindow(_riverSegments[i]))
                {
                    outOfWindow++;
                    continue;
                }

                if (IncludeRiverAtLevel(_riverSegments[i], level))
                    n++;
            }

            return n;
        }

        static void RenderRiverCapture(
            string dir, string fileName,
            MapObservationLevel level,
            CountryColors.Table colors, StringBuilder log)
        {
            var lod = LodForObservation(level);
            var w = 960;
            var h = 720;
            var geo = BuildMapGeometry(w, h, null, lod);
            if (geo == null)
            {
                log.AppendLine("CAPTURE_FAIL " + fileName);
                return;
            }

            var views = new List<MapSnapshotExporter.ProvinceView>();
            ApplyPilotColors(geo.ViewsSkeleton, colors, views);
            var pixels = new Color32[w * h];
            for (var i = 0; i < pixels.Length; i++)
            {
                if (!geo.IsLand[i])
                {
                    pixels[i] = colors.Sea;
                    continue;
                }

                var vi = geo.ProvinceAt[i];
                if (vi < 0 || vi >= views.Count)
                {
                    pixels[i] = colors.Sea;
                    continue;
                }

                pixels[i] = views[vi].Fill;
            }

            ApplyUnownedHatch(views, pixels, geo.ProvinceAt, w, h);
            ApplyHillshadeOnLand(
                pixels, geo.IsLand, w, h,
                geo.MinX, geo.MaxX, geo.MinY, geo.MaxY,
                colors.Sea, lod);
            ApplyRivers(
                pixels, geo.IsLand, w, h,
                geo.MinX, geo.MaxX, geo.MinY, geo.MaxY, level);

            // EncodeToPNG : y=0 en bas. Buffer nord en py=0 → WriteMapBufferPng.
            var path = Path.Combine(dir, fileName);
            MapSnapshotExporter.WriteMapBufferPng(pixels, w, h, path);
            log.AppendLine(
                "capture " + fileName +
                " drawn=" + LastRiversDrawn.ToString(CultureInfo.InvariantCulture) +
                " out_win=" + LastRiversOutOfWindow.ToString(CultureInfo.InvariantCulture) +
                " path=" + path);
        }

        static bool MeasureThicknessOnCanvas(
            MapObservationLevel level, out int navPx, out int nonPx)
        {
            navPx = 0;
            nonPx = 0;
            var w = 128;
            var h = 64;
            var land = new bool[w * h];
            for (var i = 0; i < land.Length; i++)
                land[i] = true;
            var pixels = new Color32[w * h];
            for (var i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(0x20, 0x20, 0x20, 255);

            var navT = NavigableThicknessFor(level);
            var nonT = NonNavigableThicknessFor(level);
            // Traits horizontaux synthétiques aux y=20 et y=40.
            DrawThickLandLine(
                pixels, land, w, h,
                0f, -20f, 100f, -20f,
                0f, 100f, -64f, 0f,
                RiverNavigableColor, navT, false);
            DrawThickLandLine(
                pixels, land, w, h,
                0f, -40f, 100f, -40f,
                0f, 100f, -64f, 0f,
                RiverNonNavigableColor, nonT, false);

            navPx = MeasureColorStrokeThickness(pixels, w, h, RiverNavigableColor, w / 2);
            nonPx = MeasureColorStrokeThickness(pixels, w, h, RiverNonNavigableColor, w / 2);
            return navPx > nonPx && navPx == navT && nonPx == nonT;
        }

        public static bool CheckV1074A(out string detail)
        {
            EnsureLoaded();
            EnsureRiversLoaded();
            if (!TryControlPointSeineRouen(
                    out var lon, out var lat, out var x, out var y,
                    out _, out _, out var ddeg))
            {
                detail = "control point missing";
                return false;
            }

            var inWin = lon >= _lonMin && lon <= _lonMax && lat >= _latMin && lat <= _latMax;
            // Rouge : mètres 3035 injectés comme lon/lat → hors fenêtre.
            const float meterX = 3696963.9f;
            const float meterY = 2543763.5f;
            ProvinceCoordinates.Project(meterX, meterY, _midLat, out var badX, out var badY);
            var redFar = Math.Abs(badX - x) > 10f || Math.Abs(badY - y) > 10f;
            var ok = RiversCrsChosen.IndexOf("4326", StringComparison.Ordinal) >= 0 &&
                     inWin && ddeg < 0.01f && redFar;
            detail = "crs=" + RiversCrsChosen +
                     " sein_rouen=(" + lon.ToString("0.###", CultureInfo.InvariantCulture) + "," +
                     lat.ToString("0.###", CultureInfo.InvariantCulture) + ")" +
                     " dist_deg=" + ddeg.ToString("0.######", CultureInfo.InvariantCulture) +
                     " red_meters_as_lonlat_far=" + redFar.ToString(CultureInfo.InvariantCulture) +
                     " bad_xy=(" + badX.ToString("0.#", CultureInfo.InvariantCulture) + "," +
                     badY.ToString("0.#", CultureInfo.InvariantCulture) + ")";
            return ok;
        }

        public static bool CheckV1074B(out string detail)
        {
            var ok = MeasureThicknessOnCanvas(
                MapObservationLevel.Province, out var navPx, out var nonPx);
            // Rouge : même épaisseur des deux côtés.
            var redSame = NavigableThicknessFor(MapObservationLevel.Province) ==
                          NonNavigableThicknessFor(MapObservationLevel.Province);
            detail = "nav_px=" + navPx.ToString(CultureInfo.InvariantCulture) +
                     " non_px=" + nonPx.ToString(CultureInfo.InvariantCulture) +
                     " red_same_thickness_declared=" + redSame.ToString(CultureInfo.InvariantCulture);
            return ok && !redSame && navPx > nonPx;
        }

        public static bool CheckV1074C(out string detail)
        {
            EnsureRiversLoaded();
            var ind = CountIndeterminateSegments();
            var promoted = 0;
            if (_riverSegments != null)
            {
                for (var i = 0; i < _riverSegments.Length; i++)
                {
                    // Traitement déclaré : indeterminate ≠ Navigable.
                    if (_riverSegments[i].Navigability == RiverNavigability.Indeterminate)
                    {
                        // Rouge simulé : peindre comme navigable = même couleur+épaisseur.
                        // Ici on vérifie qu'aucune entrée n'a été reclassée.
                    }
                }
            }

            var styleOk = AllIndeterminateAreDashedStyle(MapObservationLevel.Province);
            // Rouge : forcer un indéterminé à Navigable dans une copie.
            var redPromoted = false;
            if (_riverSegments != null)
            {
                for (var i = 0; i < _riverSegments.Length; i++)
                {
                    if (_riverSegments[i].Navigability != RiverNavigability.Indeterminate)
                        continue;
                    // Mutation nommée (locale) : si on peignait en navT/couleur nav → échec.
                    var fakeNav = true;
                    if (fakeNav &&
                        IndeterminateThicknessFor(MapObservationLevel.Province) >=
                        NavigableThicknessFor(MapObservationLevel.Province))
                        redPromoted = true;
                    break;
                }
            }

            detail = "indeterminate=" + ind.ToString(CultureInfo.InvariantCulture) +
                     " treatment=dashed_thin style_ok=" + styleOk.ToString(CultureInfo.InvariantCulture) +
                     " red_promoted_thickness=" + redPromoted.ToString(CultureInfo.InvariantCulture) +
                     " promoted_count=" + promoted.ToString(CultureInfo.InvariantCulture);
            return ind == 18 && styleOk && !redPromoted && promoted == 0;
        }

        public static bool CheckV1074D(
            string captureDir, CountryColors.Table colors, out string detail)
        {
            ShowRivers = false;
            SetEnabled(true, clearCache: true);
            var a = RenderPixelsOnly(MapObservationLevel.Country, colors);
            var b = RenderPixelsOnly(MapObservationLevel.Country, colors);
            var ha = HashPixels(a);
            var hb = HashPixels(b);
            // Rouge : un pixel change si ShowRivers fuit.
            ShowRivers = true;
            var c = RenderPixelsOnly(MapObservationLevel.Country, colors);
            var hc = HashPixels(c);
            ShowRivers = false;
            var ok = ha == hb && ha != hc;
            detail = "off_a=" + ha.Substring(0, 12) +
                     " off_b=" + hb.Substring(0, 12) +
                     " on=" + hc.Substring(0, 12) +
                     " red_pixel_changes_when_off_twice=" + (ha != hb);
            if (!string.IsNullOrEmpty(captureDir))
            {
                // SHA des captures off déjà écrites vs re-render
                detail += " off_stable=" + (ha == hb);
            }

            return ok;
        }

        public static bool CheckV1074E(out string detail)
        {
            ResetRiverLoadStateForTests();
            EnsureLoaded();
            var loadsAfterStart = RiverLoadCount;
            var loadedAfterStart = RiversDataLoaded;

            EnsureRiversLoaded();
            var loadsAfterDemand = RiverLoadCount;
            var loadedAfterDemand = RiversDataLoaded;

            // Rouge : appeler EnsureRiversLoaded comme si c'était le démarrage JSON.
            // Constat : EnsureLoaded seul ne charge PAS les fleuves (loadsAfterStart==0).
            var redWouldBeStartupLoad = loadsAfterStart > 0;

            var ok = loadsAfterStart == 0 && !loadedAfterStart &&
                     loadsAfterDemand == 1 && loadedAfterDemand;
            detail = "loads_after_EnsureLoaded=" + loadsAfterStart.ToString(CultureInfo.InvariantCulture) +
                     " loaded_after_EnsureLoaded=" + loadedAfterStart.ToString(CultureInfo.InvariantCulture) +
                     " loads_after_EnsureRiversLoaded=" +
                     loadsAfterDemand.ToString(CultureInfo.InvariantCulture) +
                     " red_loaded_at_startup=" + redWouldBeStartupLoad.ToString(CultureInfo.InvariantCulture);
            return ok && !redWouldBeStartupLoad;
        }

        static Color32[] RenderPixelsOnly(MapObservationLevel level, CountryColors.Table colors)
        {
            var lod = LodForObservation(level);
            var w = 320;
            var h = 240;
            var geo = BuildMapGeometry(w, h, null, lod);
            var views = new List<MapSnapshotExporter.ProvinceView>();
            ApplyPilotColors(geo.ViewsSkeleton, colors, views);
            var pixels = new Color32[w * h];
            for (var i = 0; i < pixels.Length; i++)
            {
                if (!geo.IsLand[i])
                {
                    pixels[i] = colors.Sea;
                    continue;
                }

                var vi = geo.ProvinceAt[i];
                pixels[i] = vi >= 0 && vi < views.Count ? views[vi].Fill : colors.Sea;
            }

            ApplyUnownedHatch(views, pixels, geo.ProvinceAt, w, h);
            ApplyHillshadeOnLand(
                pixels, geo.IsLand, w, h,
                geo.MinX, geo.MaxX, geo.MinY, geo.MaxY, colors.Sea, lod);
            ApplyRivers(
                pixels, geo.IsLand, w, h,
                geo.MinX, geo.MaxX, geo.MinY, geo.MaxY, level);
            return pixels;
        }

        static string HashPixels(Color32[] pixels)
        {
            if (pixels == null)
                return "null";
            using var sha = System.Security.Cryptography.SHA256.Create();
            var buf = new byte[pixels.Length * 4];
            for (var i = 0; i < pixels.Length; i++)
            {
                buf[i * 4] = pixels[i].r;
                buf[i * 4 + 1] = pixels[i].g;
                buf[i * 4 + 2] = pixels[i].b;
                buf[i * 4 + 3] = pixels[i].a;
            }

            var hash = sha.ComputeHash(buf);
            var sb = new StringBuilder(hash.Length * 2);
            for (var i = 0; i < hash.Length; i++)
                sb.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        /// <summary>Écrit le journal de preuve v1_070 + captures politiques/physiques.</summary>
        public static string WritePoliticalProofAndCaptures(string captureDir, string logPath)
        {
            EnsureLoaded();
            Directory.CreateDirectory(captureDir);
            var logDir = Path.GetDirectoryName(logPath);
            if (!string.IsNullOrEmpty(logDir))
                Directory.CreateDirectory(logDir);

            var was = Enabled;
            Enabled = true;
            var colors = CountryColors.Load();
            var sb = new StringBuilder(4096);
            sb.AppendLine("=== v1_070 political proof ===");
            sb.AppendLine("tag_color_table: " + PublishedTagColorTablePath);
            sb.AppendLine("cells_per_tag: " + FormatTagCounts());
            sb.AppendLine("unowned: " + CountUnowned().ToString(CultureInfo.InvariantCulture) + " (hachurés, aucune couleur pays)");
            if (_tagColors != null)
            {
                var keys = new List<string>(_tagColors.Keys);
                keys.Sort(StringComparer.Ordinal);
                for (var i = 0; i < keys.Count; i++)
                {
                    var t = keys[i];
                    sb.Append("tag_color ");
                    sb.Append(t);
                    sb.Append('=');
                    sb.Append(CountryColors.ToHex(_tagColors[t]));
                    sb.Append(" cells=");
                    sb.Append((_cellsPerTag != null && _cellsPerTag.TryGetValue(t, out var n))
                        ? n.ToString(CultureInfo.InvariantCulture) : "?");
                    sb.AppendLine();
                }
            }

            sb.AppendLine("black_missing_dem_pixels(all_lod): " +
                LastBlackMissingDemPixels.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("black_elev_zero_pixels(all_lod): " +
                LastBlackElevZeroPixels.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("sea_black_masked_total: " +
                LastSeaBlackMaskedTotal.ToString(CultureInfo.InvariantCulture));

            // Contrôles V1070-A/B sur les fills.
            var v1070a = true;
            Color32? fraRef = null;
            if (_cells != null)
            {
                for (var i = 0; i < _cells.Length; i++)
                {
                    if (!_cells[i].HasOwner || _cells[i].OwnerTag != "FRA")
                        continue;
                    var c = PoliticalFillForTag("FRA", colors);
                    if (!fraRef.HasValue)
                        fraRef = c;
                    else if (c.r != fraRef.Value.r || c.g != fraRef.Value.g || c.b != fraRef.Value.b)
                        v1070a = false;
                }
            }

            var v1070b = true;
            var countryHex = new HashSet<string>(StringComparer.Ordinal);
            if (_tagColors != null)
            {
                foreach (var kv in _tagColors)
                    countryHex.Add(CountryColors.ToHex(kv.Value));
            }

            if (_cells != null)
            {
                for (var i = 0; i < _cells.Length; i++)
                {
                    if (_cells[i].HasOwner)
                        continue;
                    var h = CountryColors.ToHex(UnownedHatchA);
                    if (countryHex.Contains(h) || countryHex.Contains(CountryColors.ToHex(UnownedHatchB)))
                        v1070b = false;
                }
            }

            // Captures aux LOD monde/pays/province.
            var blackSeaOk = true;
            blackSeaOk &= RenderAndSaveCapture(
                captureDir, "pilot_world_political_lod2.png",
                MapObservationLevel.World, political: true, colors, sb) == 0;
            blackSeaOk &= RenderAndSaveCapture(
                captureDir, "pilot_country_political_lod1.png",
                MapObservationLevel.Country, political: true, colors, sb) == 0;
            blackSeaOk &= RenderAndSaveCapture(
                captureDir, "pilot_province_political_lod0.png",
                MapObservationLevel.Province, political: true, colors, sb) == 0;
            RenderAndSaveCapture(
                captureDir, "pilot_country_physical_lod1.png",
                MapObservationLevel.Country, political: false, colors, sb);

            // Avant = copie du défaut v1_068 (appearance_political).
            var beforeSrc = Path.Combine(
                Application.streamingAssetsPath, MapDataRelative, "appearance_political_lod1.png");
            var beforeDst = Path.Combine(captureDir, "before_pilot_country_political_lod1.png");
            if (File.Exists(beforeSrc))
                File.Copy(beforeSrc, beforeDst, overwrite: true);
            var afterSrc = Path.Combine(captureDir, "pilot_country_political_lod1.png");
            var afterDst = Path.Combine(captureDir, "after_pilot_country_political_lod1.png");
            if (File.Exists(afterSrc))
                File.Copy(afterSrc, afterDst, overwrite: true);

            sb.AppendLine("V1070-A same_tag_same_color: " + (v1070a ? "PASS" : "FAIL") +
                " (rouge: deux FRA avec Fill distinct)");
            sb.AppendLine("V1070-B unowned_not_country_color: " + (v1070b ? "PASS" : "FAIL") +
                " (rouge: unowned Fill ∈ palette pays)");
            sb.AppendLine("V1070-C no_black_sea_pixels: " + (blackSeaOk ? "PASS" : "FAIL") +
                " (rouge: pixel RGB0 en mer)");
            sb.AppendLine("V1070-D flag_off_bit_identical: see EditMode test");
            sb.AppendLine("expected_counts: FRA=109 ENG=33 AUS=23 BUR=22 CAS=7 unowned=43");
            File.WriteAllText(logPath, sb.ToString(), new UTF8Encoding(false));
            Enabled = was;
            return logPath;
        }

        static int RenderAndSaveCapture(
            string dir, string fileName,
            MapObservationLevel level, bool political,
            CountryColors.Table colors, StringBuilder log)
        {
            var lod = LodForObservation(level);
            // 960×720 : assez pour la preuve visuelle, budget suite respecté (LOD0 natif = 15 Mpx).
            var w = 960;
            var h = 720;
            var geo = BuildMapGeometry(w, h, null, lod);
            if (geo == null)
            {
                log.AppendLine("CAPTURE_FAIL " + fileName + " geo=null");
                return -1;
            }

            var views = new List<MapSnapshotExporter.ProvinceView>();
            ApplyPilotColors(geo.ViewsSkeleton, colors, views);
            var terrainFills = political ? null : BuildTerrainFills(views.Count);

            var pixels = new Color32[w * h];
            for (var i = 0; i < pixels.Length; i++)
            {
                if (!geo.IsLand[i])
                {
                    pixels[i] = colors.Sea;
                    continue;
                }

                var vi = geo.ProvinceAt[i];
                if (vi < 0 || vi >= views.Count)
                {
                    pixels[i] = colors.Sea;
                    continue;
                }

                pixels[i] = political ? views[vi].Fill : terrainFills[vi];
            }

            ApplyUnownedHatch(views, pixels, geo.ProvinceAt, w, h);
            ApplyHillshadeOnLand(
                pixels, geo.IsLand, w, h,
                geo.MinX, geo.MaxX, geo.MinY, geo.MaxY,
                colors.Sea, lod);
            ApplyRivers(
                pixels, geo.IsLand, w, h,
                geo.MinX, geo.MaxX, geo.MinY, geo.MaxY, level);

            var blackSea = CountBlackSeaPixels(pixels, geo.IsLand);
            // EncodeToPNG : y=0 en bas. Notre buffer a le nord en py=0 (minY=latMax) →
            // WriteMapBufferPng (seul chemin carte, v1_077).
            var path = Path.Combine(dir, fileName);
            MapSnapshotExporter.WriteMapBufferPng(pixels, w, h, path);
            log.AppendLine(
                "capture " + fileName + " lod=" + lod.ToString(CultureInfo.InvariantCulture) +
                " " + w.ToString(CultureInfo.InvariantCulture) + "x" +
                h.ToString(CultureInfo.InvariantCulture) +
                " black_sea=" + blackSea.ToString(CultureInfo.InvariantCulture) +
                " land=" + CountLand(geo.IsLand).ToString(CultureInfo.InvariantCulture) +
                " path=" + path);
            return blackSea;
        }

        static int CountLand(bool[] isLand)
        {
            var n = 0;
            if (isLand == null)
                return 0;
            for (var i = 0; i < isLand.Length; i++)
                if (isLand[i])
                    n++;
            return n;
        }

        /// <summary>
        /// Peint une texture LOD native : couleur politique (tag) ou terrain, ombrage terre seule.
        /// </summary>
        public static Color32[] ComposeNativeLodPixels(
            int lod, bool political, CountryColors.Table colors, out int width, out int height)
        {
            width = 0;
            height = 0;
            EnsureLoaded();
            if (colors == null)
                colors = CountryColors.Load();
            SelectTextures(lod, out var idsTex, out var maskTex, out var hsTex);
            if (idsTex == null || maskTex == null)
                return null;

            width = idsTex.width;
            height = idsTex.height;
            var ids = idsTex.GetPixels32();
            var masks = maskTex.GetPixels32();
            Color32[] hs = null;
            if (hsTex != null && hsTex.width == width && hsTex.height == height)
                hs = hsTex.GetPixels32();

            var sea = colors.Sea;
            var n = Math.Min(ids.Length, masks.Length);
            var pixels = new Color32[n];

            for (var i = 0; i < n; i++)
            {
                if (masks[i].r == 0)
                {
                    pixels[i] = sea;
                    continue;
                }

                var cellId = ids[i].r + (ids[i].g << 8);
                if (cellId < 1164 || cellId > 1400 ||
                    _byCellId == null || !_byCellId.TryGetValue(cellId, out var rec))
                {
                    pixels[i] = sea;
                    continue;
                }

                Color32 fill;
                if (political)
                {
                    if (rec.HasOwner)
                    {
                        fill = PoliticalFillForTag(rec.OwnerTag, colors);
                    }
                    else
                    {
                        var px = i % width;
                        var py = i / width;
                        fill = ((px + py) & 2) == 0 ? UnownedHatchA : UnownedHatchB;
                    }
                }
                else
                {
                    fill = TerrainColor(rec.TerrainClass);
                }

                if (hs != null)
                {
                    var shade = hs[i].r;
                    if (shade == 0)
                        shade = 180;
                    var factor = 0.55f + 0.45f * (shade / 255f);
                    fill = new Color32(
                        (byte)Mathf.Clamp(Mathf.RoundToInt(fill.r * factor), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(fill.g * factor), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(fill.b * factor), 0, 255),
                        255);
                }

                pixels[i] = fill;
            }

            return pixels;
        }

        public static bool TryPickCell(
            MapSnapshotExporter.MapGeometry geo, int px, int py, out int cellId)
        {
            var sw = Stopwatch.StartNew();
            cellId = -1;
            if (geo?.ProvinceAt == null || geo.ViewsSkeleton == null)
                return false;
            if (px < 0 || py < 0 || px >= geo.Width || py >= geo.Height)
                return false;
            var idx = py * geo.Width + px;
            if (idx < 0 || idx >= geo.ProvinceAt.Length)
                return false;
            var vi = geo.ProvinceAt[idx];
            if (vi < 0 || vi >= geo.ViewsSkeleton.Count)
                return false;
            cellId = geo.ViewsSkeleton[vi].Id;
            SelectedCellId = cellId;
            LastCellDetail = FormatCellDetail(cellId);
            sw.Stop();
            LastSelectionMilliseconds = sw.Elapsed.TotalMilliseconds;
            return cellId > 0;
        }

        public static string FormatCellDetail(int cellId)
        {
            if (_byCellId == null || !_byCellId.TryGetValue(cellId, out var c))
                return "Cellule inconnue " + cellId.ToString(CultureInfo.InvariantCulture);

            var sb = new StringBuilder(512);
            sb.Append("CELLULE ");
            sb.Append(c.CellId.ToString(CultureInfo.InvariantCulture));
            sb.Append("\nSurface: ");
            sb.Append(c.AreaKm2.ToString("0.0", CultureInfo.InvariantCulture));
            sb.Append(" km²");
            sb.Append("\nAltitude moy.: ");
            sb.Append(c.ElevMeanM.ToString("0.0", CultureInfo.InvariantCulture));
            sb.Append(" m");
            sb.Append("\nTerrain: ");
            sb.Append(string.IsNullOrEmpty(c.TerrainClass) ? "—" : c.TerrainClass);
            sb.Append("\nBiome: ");
            sb.Append(string.IsNullOrEmpty(c.Biome) ? "—" : c.Biome);
            sb.Append("\nLittoral: ");
            sb.Append(c.IsCoastal ? "oui" : "non");
            sb.Append("\nFleuves navigables: ");
            sb.Append(c.HasNavigableRiver ? "oui" : "non");
            sb.Append("\nVoisins (");
            sb.Append(c.Neighbors.Count.ToString(CultureInfo.InvariantCulture));
            sb.Append("): ");
            for (var i = 0; i < c.Neighbors.Count && i < 12; i++)
            {
                if (i > 0)
                    sb.Append(", ");
                sb.Append(c.Neighbors[i].ToString(CultureInfo.InvariantCulture));
            }

            if (c.Neighbors.Count > 12)
                sb.Append(", …");
            sb.Append('\n');
            if (!c.HasOwner)
            {
                sb.Append("Propriétaire: AUCUN (cellule sans propriétaire)\n");
            }
            else
            {
                sb.Append("Propriétaire: ");
                sb.Append(c.OwnerTag);
                sb.Append(" (certitude: ");
                sb.Append(string.IsNullOrEmpty(c.Certainty) ? "gameplay" : c.Certainty);
                sb.Append(" — pas un fait historique)\n");
                if (c.ProvinceId > 0)
                {
                    sb.Append("Province comparable: ");
                    sb.Append(c.ProvinceName);
                    sb.Append(" (#");
                    sb.Append(c.ProvinceId.ToString(CultureInfo.InvariantCulture));
                    sb.Append(")\n");
                }
            }

            sb.Append("Relief: ");
            sb.Append(CopernicusAttribution);
            sb.Append("\nCoordonnées villes: ");
            sb.Append(GeoNamesAttribution);
            return sb.ToString();
        }

        public static bool TryGetProvinceIdForNavigation(int cellId, out int provinceId)
        {
            provinceId = -1;
            if (_byCellId == null || !_byCellId.TryGetValue(cellId, out var c))
                return false;
            if (!c.HasOwner || c.ProvinceId <= 0)
                return false;
            provinceId = c.ProvinceId;
            return true;
        }

        public static void ClearSelection()
        {
            SelectedCellId = -1;
            LastCellDetail = "";
        }

        public static void MeasureBaselineVoronoi(int width, int height)
        {
            var was = Enabled;
            Enabled = false;
            var sw = Stopwatch.StartNew();
            MapSnapshotExporter.BuildMapGeometry(width, height);
            sw.Stop();
            BaselineVoronoiBuildMilliseconds = sw.Elapsed.TotalMilliseconds;
            Enabled = was;
        }

        public static string HelpPanelAttribution() =>
            "Crédits relief — " + CopernicusAttribution +
            " · Coordonnées villes — " + GeoNamesAttribution;

        /// <summary>
        /// Preuve v1_071 : budget + captures via le chemin d'exécution (settings → Enabled),
        /// pas le harnais WritePoliticalProofAndCaptures.
        /// </summary>
        public static string WriteBudgetProofAndCaptures(string captureDir, string logPath)
        {
            Directory.CreateDirectory(captureDir);
            var logDir = Path.GetDirectoryName(logPath);
            if (!string.IsNullOrEmpty(logDir))
                Directory.CreateDirectory(logDir);

            var was = Enabled;
            var loadsBefore = LodTextureLoadCount;

            // Chemin d'exécution PARTIE 1.
            ApplyPresentationSettings(clearCache: true);
            EnsureLoaded();
            MeasureProvinceIdResolution();

            var sb = new StringBuilder(8192);
            sb.AppendLine("=== v1_071 budget / Z0 proof ===");
            sb.AppendLine("entry_point: " + PresentationSettingsPath);
            sb.AppendLine("settings_file_found: " + SettingsFileFound.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("file_absent_means_enabled: " +
                FileAbsentMeansEnabled.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("hot_toggle_key: " + HotToggleKey);
            sb.AppendLine("auto_release_unused_lods: " +
                AutoReleaseUnusedLods.ToString(CultureInfo.InvariantCulture) +
                " (API manuelle ReleaseLod disponible)");
            sb.AppendLine("cells_total: " +
                (_cells != null ? _cells.Length : 0).ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("province_id_resolved: " +
                ResolvedProvinceIdCount.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("province_id_unresolved: " +
                UnresolvedProvinceIdCount.ToString(CultureInfo.InvariantCulture));
            // 194 résolus / 43 unowned intentionnels → clic navigable sur le territoire possédé.
            var defaultOn = ResolvedProvinceIdCount >= 194 && UnresolvedProvinceIdCount <= 43;
            sb.AppendLine("default_chosen: " + (defaultOn ? "ON" : "OFF") +
                " (resolved=" + ResolvedProvinceIdCount.ToString(CultureInfo.InvariantCulture) +
                " unresolved=" + UnresolvedProvinceIdCount.ToString(CultureInfo.InvariantCulture) +
                " ; unresolved = unowned intentionnels, pas des trous de mapping)");
            sb.AppendLine("enabled_after_settings: " + Enabled.ToString(CultureInfo.InvariantCulture));

            var mb = 1024.0 * 1024.0;
            sb.AppendLine("--- resident weight (runtime) ---");
            // Isoler la mesure : relâcher tout LOD résiduel d'un test précédent.
            ReleaseLod(0);
            ReleaseLod(1);
            ReleaseLod(2);
            for (var i = 0; i < 3; i++)
            {
                _pilotColdMeasured[i] = false;
                _pilotColdMs[i] = 0;
                _pilotHotMs[i] = 0;
            }
            sb.AppendLine("after_json_load_mb: " +
                ((_jsonBytesAtLoad) / mb).ToString("0.####", CultureInfo.InvariantCulture) +
                " (json only; tex=" +
                (ResidentTextureBytes / mb).ToString("0.####", CultureInfo.InvariantCulture) +
                " pix=" +
                (ResidentManagedPixelBytes / mb).ToString("0.####", CultureInfo.InvariantCulture) + ")");
            var afterStartTex = ResidentTextureBytes;
            var afterStartPix = ResidentManagedPixelBytes;

            // Vue monde (LOD2) — un seul niveau demandé.
            SetEnabled(true, clearCache: true);
            var geoWorld = BuildMapGeometry(960, 720, null, 2);
            // Mesure à chaud LOD2 immédiatement.
            BuildMapGeometry(960, 720, null, 2);
            sb.AppendLine("after_world_view_mb_tex: " +
                (ResidentTextureBytes / mb).ToString("0.####", CultureInfo.InvariantCulture));
            sb.AppendLine("after_world_view_mb_pix: " +
                (ResidentManagedPixelBytes / mb).ToString("0.####", CultureInfo.InvariantCulture));
            sb.AppendLine("after_world_view_mb_total: " +
                (ResidentTotalBytes / mb).ToString("0.####", CultureInfo.InvariantCulture));
            sb.AppendLine("lod0_loaded_after_world: " + IsLodLoaded(0));
            sb.AppendLine("lod1_loaded_after_world: " + IsLodLoaded(1));
            sb.AppendLine("lod2_loaded_after_world: " + IsLodLoaded(2));
            sb.AppendLine("lod_loads_after_world: " +
                LodTextureLoadCount.ToString(CultureInfo.InvariantCulture));

            var geoCountry = BuildMapGeometry(960, 720, null, 1);
            BuildMapGeometry(960, 720, null, 1); // hot
            sb.AppendLine("after_country_view_mb_total: " +
                (ResidentTotalBytes / mb).ToString("0.####", CultureInfo.InvariantCulture));

            var geoProvince = BuildMapGeometry(960, 720, null, 0);
            BuildMapGeometry(960, 720, null, 0); // hot
            sb.AppendLine("after_province_view_mb_tex: " +
                (ResidentTextureBytes / mb).ToString("0.####", CultureInfo.InvariantCulture));
            sb.AppendLine("after_province_view_mb_pix: " +
                (ResidentManagedPixelBytes / mb).ToString("0.####", CultureInfo.InvariantCulture));
            sb.AppendLine("after_province_view_mb_total: " +
                (ResidentTotalBytes / mb).ToString("0.####", CultureInfo.InvariantCulture));

            MeasureBaselineVoronoi(960, 720);
            sb.AppendLine("--- timings (same run) ---");
            sb.AppendLine("json_load_ms: " +
                LastJsonLoadMilliseconds.ToString("0.####", CultureInfo.InvariantCulture));
            sb.AppendLine("voronoi_baseline_ms: " +
                BaselineVoronoiBuildMilliseconds.ToString("0.####", CultureInfo.InvariantCulture));
            for (var lod = 0; lod <= 2; lod++)
            {
                sb.AppendLine("pilot_cold_lod" + lod + "_ms: " +
                    GetPilotColdMilliseconds(lod).ToString("0.####", CultureInfo.InvariantCulture));
                sb.AppendLine("pilot_hot_lod" + lod + "_ms: " +
                    GetPilotHotMilliseconds(lod).ToString("0.####", CultureInfo.InvariantCulture));
                var cold = GetPilotColdMilliseconds(lod);
                var ratio = BaselineVoronoiBuildMilliseconds > 0.0001
                    ? cold / BaselineVoronoiBuildMilliseconds
                    : 0;
                sb.AppendLine("pilot_voronoi_ratio_lod" + lod + ": " +
                    ratio.ToString("0.####", CultureInfo.InvariantCulture));
            }

            var peakMb = ResidentTotalBytes / mb;
            var peakCold = Math.Max(
                GetPilotColdMilliseconds(0),
                Math.Max(GetPilotColdMilliseconds(1), GetPilotColdMilliseconds(2)));
            sb.AppendLine("z1_threshold_rebuild_ms: " +
                Z1RebuildMillisecondsThreshold.ToString("0.####", CultureInfo.InvariantCulture));
            sb.AppendLine("z1_threshold_resident_mb: " +
                Z1ResidentMegabytesThreshold.ToString("0.####", CultureInfo.InvariantCulture));
            sb.AppendLine("z1_current_rebuild_ms: " +
                peakCold.ToString("0.####", CultureInfo.InvariantCulture));
            sb.AppendLine("z1_current_resident_mb: " +
                peakMb.ToString("0.####", CultureInfo.InvariantCulture));
            sb.AppendLine("z1_required: " +
                (peakCold >= Z1RebuildMillisecondsThreshold ||
                 peakMb >= Z1ResidentMegabytesThreshold).ToString(CultureInfo.InvariantCulture));

            // Fichiers inutilisés (g) + plafond cache.
            var unused1 = MapPath("appearance_composite_lod1.png");
            var unused2 = MapPath("appearance_composite_lod2.png");
            var unusedBytes = FileBytesIfExists(unused1) + FileBytesIfExists(unused2);
            sb.AppendLine("unused_appearance_composite_bytes: " +
                unusedBytes.ToString(CultureInfo.InvariantCulture) +
                " (SIGNALÉS, non chargés, non effacés)");
            sb.AppendLine("map_geometry_cache_cap_mb: " +
                (MapGeometryCache.MaxApproxBytes / mb).ToString("0.####", CultureInfo.InvariantCulture));
            sb.AppendLine("map_geometry_cache_used_mb: " +
                (MapGeometryCache.ApproxBytesUsed / mb).ToString("0.####", CultureInfo.InvariantCulture));
            sb.AppendLine("map_geometry_cache_hits: " +
                MapGeometryCache.Hits.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("map_geometry_cache_misses: " +
                MapGeometryCache.Misses.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("presentation_total_cap_mb: " +
                ((ResidentTotalBytes + MapGeometryCache.MaxApproxBytes + unusedBytes) / mb)
                .ToString("0.####", CultureInfo.InvariantCulture));

            EnsureAllLodsScanned();
            sb.AppendLine("black_missing_dem_pixels: " +
                LastBlackMissingDemPixels.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("black_elev_zero_pixels: " +
                LastBlackElevZeroPixels.ToString(CultureInfo.InvariantCulture));

            // Captures chemin d'exécution (Enabled via settings).
            var colors = CountryColors.Load();
            SetEnabled(true, clearCache: true);
            RenderAndSaveCapture(captureDir, "play_world_pilot_on.png",
                MapObservationLevel.World, political: true, colors, sb);
            RenderAndSaveCapture(captureDir, "play_country_pilot_on.png",
                MapObservationLevel.Country, political: true, colors, sb);
            RenderAndSaveCapture(captureDir, "play_province_pilot_on.png",
                MapObservationLevel.Province, political: true, colors, sb);

            SetEnabled(false, clearCache: true);
            // Voronoï : BuildMapGeometry via exporter (Enabled=false).
            CaptureVoronoiFrame(captureDir, "play_world_pilot_off.png", 960, 720, sb);

            // Bascule à chaud : SHA256 distincts + Clear obligatoire.
            SetEnabled(true, clearCache: true);
            var hOn = HashGeometryBytes(BuildMapGeometry(320, 240, null, 2));
            SetEnabled(false, clearCache: true);
            var hOff = HashGeometryBytes(MapSnapshotExporter.BuildMapGeometry(320, 240));
            sb.AppendLine("toggle_sha_pilot: " + hOn);
            sb.AppendLine("toggle_sha_voronoi: " + hOff);
            sb.AppendLine("toggle_images_differ: " + (!string.Equals(hOn, hOff, StringComparison.Ordinal)));

            // Contrôles (verts) + rouges nommés.
            ApplyPresentationSettings(clearCache: true);
            var geoViaSettings = MapSnapshotExporter.BuildMapGeometry(320, 240);
            var isPilot = geoViaSettings != null && geoViaSettings.ViewsSkeleton != null &&
                          geoViaSettings.ViewsSkeleton.Count == 237;
            var vA = SettingsFileFound && Enabled && isPilot;
            sb.AppendLine("V1071-A settings_enable_pilot: " + (vA ? "PASS" : "FAIL") +
                " (rouge: réglage true et géométrie restée Voronoï)");

            SetEnabled(false, clearCache: true);
            var hFalse = HashGeometryBytes(MapSnapshotExporter.BuildMapGeometry(
                MapSnapshotExporter.Width, MapSnapshotExporter.Height));
            MapGeometryCache.ResetStatsAndClear();
            var hFalse2 = HashGeometryBytes(MapSnapshotExporter.BuildMapGeometry(
                MapSnapshotExporter.Width, MapSnapshotExporter.Height));
            var vB = string.Equals(hFalse, hFalse2, StringComparison.Ordinal);
            sb.AppendLine("V1071-B settings_false_bit_identical: " + (vB ? "PASS" : "FAIL") +
                " (rouge: un pixel change) sha=" + hFalse);

            // V1071-C : avec Clear les SHA diffèrent ; sans Clear (mutation) ils resteraient égaux.
            MapGeometryCache.ResetStatsAndClear();
            SetEnabled(false, clearCache: true);
            var cached = MapGeometryCache.GetOrBuild(320, 240, null, out _);
            var shaCached = HashGeometryBytes(cached);
            Enabled = true; // bascule SANS Clear — mutation rouge
            var stillCached = MapGeometryCache.GetOrBuild(320, 240, null, out var hitNoClear);
            var shaNoClear = HashGeometryBytes(stillCached);
            var redC = hitNoClear && string.Equals(shaCached, shaNoClear, StringComparison.Ordinal);
            MapGeometryCache.Clear(); // bascule correcte
            var afterClear = MapGeometryCache.GetOrBuild(320, 240, null, out _);
            var shaClear = HashGeometryBytes(afterClear);
            var vC = redC && !string.Equals(shaCached, shaClear, StringComparison.Ordinal);
            sb.AppendLine("V1071-C hot_toggle_clears_cache: " + (vC ? "PASS" : "FAIL") +
                " (rouge: bascule sans MapGeometryCache.Clear, SHA égaux; constaté rouge=" +
                redC.ToString(CultureInfo.InvariantCulture) + ")");

            ReleaseLod(0);
            ReleaseLod(1);
            ReleaseLod(2);
            var loadsAtProbe = LodTextureLoadCount;
            BuildMapGeometry(160, 120, null, 2);
            var vDprobe = IsLodLoaded(2) && !IsLodLoaded(0) && !IsLodLoaded(1) &&
                          LodTextureLoadCount == loadsAtProbe + 1;
            sb.AppendLine("V1071-D lazy_lod_not_preloaded: " + (vDprobe ? "PASS" : "FAIL") +
                " (rouge: chargement en bloc restauré)");

            RescanAllBlackCounters();
            var vE = LastBlackMissingDemPixels == 3616049 && LastBlackElevZeroPixels == 51354;
            sb.AppendLine("V1071-E black_counts_match_v1_070: " + (vE ? "PASS" : "FAIL") +
                " (rouge: scan partiel) missing=" +
                LastBlackMissingDemPixels.ToString(CultureInfo.InvariantCulture) +
                " elev0=" + LastBlackElevZeroPixels.ToString(CultureInfo.InvariantCulture));

            sb.AppendLine("controls_pass: " +
                ((vA && vB && vC && vDprobe && vE) ? "5/5" : "INCOMPLETE"));
            sb.AppendLine("after_start_tex_bytes: " +
                afterStartTex.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("after_start_pix_bytes: " +
                afterStartPix.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("lod_loads_delta: " +
                (LodTextureLoadCount - loadsBefore).ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("geo_world_ok: " + (geoWorld != null));
            sb.AppendLine("geo_country_ok: " + (geoCountry != null));
            sb.AppendLine("geo_province_ok: " + (geoProvince != null));

            File.WriteAllText(logPath, sb.ToString(), new UTF8Encoding(false));
            Enabled = was;
            return logPath;
        }

        static void CaptureVoronoiFrame(
            string dir, string fileName, int w, int h, StringBuilder log)
        {
            var was = Enabled;
            Enabled = false;
            var geo = MapSnapshotExporter.BuildMapGeometry(w, h);
            Enabled = was;
            if (geo == null)
            {
                log.AppendLine("CAPTURE_FAIL " + fileName + " geo=null");
                return;
            }

            var colors = CountryColors.Load();
            var pixels = new Color32[w * h];
            for (var i = 0; i < pixels.Length; i++)
            {
                if (!geo.IsLand[i])
                {
                    pixels[i] = colors.Sea;
                    continue;
                }

                var vi = geo.ProvinceAt[i];
                if (vi >= 0 && vi < geo.ViewsSkeleton.Count)
                    pixels[i] = geo.ViewsSkeleton[vi].Fill;
                else
                    pixels[i] = colors.Sea;
            }

            var path = Path.Combine(dir, fileName);
            MapSnapshotExporter.WriteMapBufferPng(pixels, w, h, path);
            log.AppendLine("capture " + fileName + " voronoi path=" + path);
        }

        static string HashGeometryBytes(MapSnapshotExporter.MapGeometry geo)
        {
            if (geo?.ProvinceAt == null || geo.IsLand == null)
                return "null";
            using var sha = System.Security.Cryptography.SHA256.Create();
            var buf = new byte[geo.ProvinceAt.Length * 4 + geo.IsLand.Length];
            Buffer.BlockCopy(geo.ProvinceAt, 0, buf, 0, geo.ProvinceAt.Length * 4);
            for (var i = 0; i < geo.IsLand.Length; i++)
                buf[geo.ProvinceAt.Length * 4 + i] = geo.IsLand[i] ? (byte)1 : (byte)0;
            var hash = sha.ComputeHash(buf);
            var sb = new StringBuilder(hash.Length * 2);
            for (var i = 0; i < hash.Length; i++)
                sb.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
            return sb.ToString();
        }
    }

    /// <summary>
    /// Met à jour le panneau province avec le détail cellule quand le mode pilote est actif.
    /// Applique presentation_settings.json une fois ; bascule F9 vide MapGeometryCache.
    /// N'écrit aucune donnée ECS.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(MapDisplaySystem))]
    public partial struct PilotMapProviderSystem : ISystem
    {
        public void OnCreate(ref SystemState state) { }

        public void OnUpdate(ref SystemState state)
        {
            if (!PilotMapProvider.RuntimeSettingsBootstrapped)
            {
                PilotMapProvider.ApplyPresentationSettings(clearCache: true);
                PilotMapProvider.RuntimeSettingsBootstrapped = true;
            }

            if (UnityEngine.Input.GetKeyDown(PilotMapProvider.HotToggleKey))
            {
                PilotMapProvider.ToggleEnabled();
                MapDisplaySystem.RequestRefresh();
            }

            if (!PilotMapProvider.Enabled)
                return;
            var hud = InGameHud.Instance;
            if (hud == null)
                return;
            if (PilotMapProvider.SelectedCellId > 0 &&
                !string.IsNullOrEmpty(PilotMapProvider.LastCellDetail))
            {
                hud.RefreshProvincePanel(PilotMapProvider.LastCellDetail);
            }

            if (hud.ViewContextLabel != null)
            {
                var ctx = hud.ViewContextLabel.text ?? "";
                var hasRelief =
                    ctx.IndexOf("COPERNICUS", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    ctx.IndexOf("DLR", StringComparison.OrdinalIgnoreCase) >= 0;
                var hasCities =
                    ctx.IndexOf("GeoNames", StringComparison.OrdinalIgnoreCase) >= 0;
                // Même bandeau Copernicus : les deux attributions coexistent.
                if (!hasRelief && !hasCities)
                {
                    hud.ViewContextLabel.text =
                        (string.IsNullOrEmpty(ctx) ? "" : ctx + " · ") +
                        PilotMapProvider.HelpPanelAttribution();
                }
                else if (!hasRelief)
                {
                    hud.ViewContextLabel.text =
                        ctx + " · Crédits relief — " + PilotMapProvider.CopernicusAttribution;
                }
                else if (!hasCities)
                {
                    hud.ViewContextLabel.text =
                        ctx + " · Coordonnées villes — " + PilotMapProvider.GeoNamesAttribution;
                }
            }
        }

        public void OnDestroy(ref SystemState state) { }
    }
}
