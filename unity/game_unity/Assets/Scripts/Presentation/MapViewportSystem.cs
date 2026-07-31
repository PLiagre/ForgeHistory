using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Unity.Mathematics;
using VictoriaGame.Core;
using VictoriaGame.Economy;
using VictoriaGame.Population;
using VictoriaGame.World;

namespace VictoriaGame.Presentation
{
    /// <summary>
    /// Niveaux d'observation (v1_029). City activable en présentation (v1_036) :
    /// les villes sont des entités semées ; District reste réservé.
    /// </summary>
    public enum MapObservationLevel : byte
    {
        World = 0,
        Country = 1,
        Province = 2,
        /// <summary>Fiche ville (v1_036) — entités CityData semées historiquement.</summary>
        City = 3,
        /// <summary>Réservé — pas de données de simulation (quartiers).</summary>
        District = 4
    }

    /// <summary>
    /// Région d'intérêt en coordonnées projetées. Zoomer = re-rendre cette fenêtre
    /// à pleine résolution, pas étirer des pixels.
    /// </summary>
    public struct MapWindow : IEquatable<MapWindow>
    {
        public float MinX;
        public float MaxX;
        public float MinY;
        public float MaxY;

        public float Width => MaxX - MinX;
        public float Height => MaxY - MinY;

        public bool Equals(MapWindow other) =>
            MinX.Equals(other.MinX) && MaxX.Equals(other.MaxX) &&
            MinY.Equals(other.MinY) && MaxY.Equals(other.MaxY);

        public override bool Equals(object obj) => obj is MapWindow w && Equals(w);

        public override int GetHashCode() =>
            HashCode.Combine(MinX, MaxX, MinY, MaxY);

        public static bool operator ==(MapWindow a, MapWindow b) => a.Equals(b);
        public static bool operator !=(MapWindow a, MapWindow b) => !a.Equals(b);
    }

    /// <summary>
    /// État de présentation (jamais écrit dans le monde simulé).
    /// TargetCountryId / TargetProvinceId permettent les tests sans Entity stable.
    /// </summary>
    public struct MapViewportState : IEquatable<MapViewportState>
    {
        public MapObservationLevel Level;
        public Entity TargetCountry;
        public Entity TargetProvince;
        public int TargetCountryId;
        public int TargetProvinceId;
        public MapWindow Window;

        public static MapViewportState World(MapWindow window) => new MapViewportState
        {
            Level = MapObservationLevel.World,
            TargetCountry = Entity.Null,
            TargetProvince = Entity.Null,
            TargetCountryId = -1,
            TargetProvinceId = -1,
            Window = window
        };

        public bool Equals(MapViewportState other) =>
            Level == other.Level &&
            TargetCountry == other.TargetCountry &&
            TargetProvince == other.TargetProvince &&
            TargetCountryId == other.TargetCountryId &&
            TargetProvinceId == other.TargetProvinceId &&
            Window == other.Window;

        public override bool Equals(object obj) => obj is MapViewportState s && Equals(s);

        public override int GetHashCode() =>
            HashCode.Combine((int)Level, TargetCountryId, TargetProvinceId, Window);
    }

    /// <summary>
    /// Machine d'états d'observation — déterministe, testable SANS rendu.
    /// Même état + même entrée → même transition. Zéro aléa, zéro horloge.
    /// </summary>
    public static class MapViewportNavigation
    {
        /// <summary>Niveaux réellement supportés (City/District exclus).</summary>
        public const int ImplementedLevelCount = 3;

        public static MapViewportState CreateWorld(MapWindow worldWindow) =>
            MapViewportState.World(worldWindow);

        /// <summary>Monde → Pays. Refuse si countryId &lt; 0.</summary>
        public static bool TrySelectCountry(
            in MapViewportState current,
            Entity country,
            int countryId,
            MapWindow countryWindow,
            out MapViewportState next)
        {
            next = current;
            if (countryId < 0)
                return false;
            if (current.Level != MapObservationLevel.World &&
                current.Level != MapObservationLevel.Country)
                return false;

            next = new MapViewportState
            {
                Level = MapObservationLevel.Country,
                TargetCountry = country,
                TargetProvince = Entity.Null,
                TargetCountryId = countryId,
                TargetProvinceId = -1,
                Window = countryWindow
            };
            return true;
        }

        /// <summary>Pays (ou Monde) → Province. Depuis Monde, countryId doit être fourni.</summary>
        public static bool TrySelectProvince(
            in MapViewportState current,
            Entity country,
            int countryId,
            Entity province,
            int provinceId,
            MapWindow provinceWindow,
            out MapViewportState next)
        {
            next = current;
            if (provinceId < 0)
                return false;
            if (current.Level == MapObservationLevel.City ||
                current.Level == MapObservationLevel.District)
                return false;

            var resolvedCountryId = countryId;
            var resolvedCountry = country;
            if (current.Level == MapObservationLevel.Country ||
                current.Level == MapObservationLevel.Province)
            {
                if (current.TargetCountryId >= 0)
                    resolvedCountryId = current.TargetCountryId;
                if (current.TargetCountry != Entity.Null)
                    resolvedCountry = current.TargetCountry;
            }

            next = new MapViewportState
            {
                Level = MapObservationLevel.Province,
                TargetCountry = resolvedCountry,
                TargetProvince = province,
                TargetCountryId = resolvedCountryId,
                TargetProvinceId = provinceId,
                Window = provinceWindow
            };
            return true;
        }

        /// <summary>Remonte d'un niveau : Province→Pays→Monde. Refuse au Monde.</summary>
        public static bool TryZoomOut(
            in MapViewportState current,
            MapWindow parentWindow,
            out MapViewportState next)
        {
            next = current;
            switch (current.Level)
            {
                case MapObservationLevel.Province:
                    if (current.TargetCountryId < 0 && current.TargetCountry == Entity.Null)
                    {
                        next = MapViewportState.World(parentWindow);
                        return true;
                    }

                    next = new MapViewportState
                    {
                        Level = MapObservationLevel.Country,
                        TargetCountry = current.TargetCountry,
                        TargetProvince = Entity.Null,
                        TargetCountryId = current.TargetCountryId,
                        TargetProvinceId = -1,
                        Window = parentWindow
                    };
                    return true;

                case MapObservationLevel.Country:
                    next = MapViewportState.World(parentWindow);
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Fenêtre autour d'un ensemble de points, aspect forcé Width/Height (anti-distorsion).
        /// </summary>
        public static MapWindow ComputeWindowFromPoints(
            NativeArray<float> xs,
            NativeArray<float> ys,
            float marginFraction,
            float targetAspect)
        {
            if (xs.Length == 0 || ys.Length == 0 || xs.Length != ys.Length)
                return default;

            var minX = xs[0];
            var maxX = xs[0];
            var minY = ys[0];
            var maxY = ys[0];
            for (var i = 1; i < xs.Length; i++)
            {
                if (xs[i] < minX) minX = xs[i];
                if (xs[i] > maxX) maxX = xs[i];
                if (ys[i] < minY) minY = ys[i];
                if (ys[i] > maxY) maxY = ys[i];
            }

            return FitAspectWithMargin(minX, maxX, minY, maxY, marginFraction, targetAspect);
        }

        /// <summary>Fenêtre mono-point (province) — rayon minimal en unités projetées.</summary>
        public static MapWindow ComputePointWindow(
            float x, float y, float halfExtent, float targetAspect)
        {
            var minX = x - halfExtent;
            var maxX = x + halfExtent;
            var minY = y - halfExtent;
            var maxY = y + halfExtent;
            return FitAspectWithMargin(minX, maxX, minY, maxY, 0f, targetAspect);
        }

        public static MapWindow FitAspectWithMargin(
            float minX, float maxX, float minY, float maxY,
            float marginFraction, float targetAspect)
        {
            var dx = maxX - minX;
            var dy = maxY - minY;
            if (dx < 0.01f) dx = 0.01f;
            if (dy < 0.01f) dy = 0.01f;
            minX -= dx * marginFraction;
            maxX += dx * marginFraction;
            minY -= dy * marginFraction;
            maxY += dy * marginFraction;

            dx = maxX - minX;
            dy = maxY - minY;
            if (targetAspect > 0.01f)
            {
                var aspect = dx / dy;
                if (aspect < targetAspect)
                {
                    var need = dy * targetAspect;
                    var pad = (need - dx) * 0.5f;
                    minX -= pad;
                    maxX += pad;
                }
                else if (aspect > targetAspect)
                {
                    var need = dx / targetAspect;
                    var pad = (need - dy) * 0.5f;
                    minY -= pad;
                    maxY += pad;
                }
            }

            return new MapWindow
            {
                MinX = minX,
                MaxX = maxX,
                MinY = minY,
                MaxY = maxY
            };
        }

        /// <summary>
        /// Panne la fenêtre (unités projetées). Garde la taille ; clamp optionnel dans bounds.
        /// </summary>
        public static MapWindow PanWindow(MapWindow window, float dx, float dy, MapWindow? clampBounds)
        {
            var next = new MapWindow
            {
                MinX = window.MinX + dx,
                MaxX = window.MaxX + dx,
                MinY = window.MinY + dy,
                MaxY = window.MaxY + dy
            };
            return ClampWindow(next, clampBounds);
        }

        /// <summary>
        /// Zoom autour d'un point projeté. factor &lt; 1 = zoom avant, &gt; 1 = zoom arrière.
        /// </summary>
        public static MapWindow ZoomWindowAt(
            MapWindow window, float worldX, float worldY, float factor, MapWindow? clampBounds)
        {
            if (factor < 0.05f) factor = 0.05f;
            if (factor > 20f) factor = 20f;
            var minX = worldX + (window.MinX - worldX) * factor;
            var maxX = worldX + (window.MaxX - worldX) * factor;
            var minY = worldY + (window.MinY - worldY) * factor;
            var maxY = worldY + (window.MaxY - worldY) * factor;
            var next = new MapWindow
            {
                MinX = minX,
                MaxX = maxX,
                MinY = minY,
                MaxY = maxY
            };
            return ClampWindow(next, clampBounds);
        }

        public static MapWindow ClampWindow(MapWindow window, MapWindow? clampBounds)
        {
            if (!clampBounds.HasValue)
                return window;
            var b = clampBounds.Value;
            var w = window.Width;
            var h = window.Height;
            if (w >= b.Width)
            {
                window.MinX = b.MinX;
                window.MaxX = b.MaxX;
            }
            else
            {
                if (window.MinX < b.MinX)
                {
                    window.MinX = b.MinX;
                    window.MaxX = b.MinX + w;
                }
                if (window.MaxX > b.MaxX)
                {
                    window.MaxX = b.MaxX;
                    window.MinX = b.MaxX - w;
                }
            }

            if (h >= b.Height)
            {
                window.MinY = b.MinY;
                window.MaxY = b.MaxY;
            }
            else
            {
                if (window.MinY < b.MinY)
                {
                    window.MinY = b.MinY;
                    window.MaxY = b.MinY + h;
                }
                if (window.MaxY > b.MaxY)
                {
                    window.MaxY = b.MaxY;
                    window.MinY = b.MaxY - h;
                }
            }

            return window;
        }

        /// <summary>
        /// Texture (px,py) → coordonnées projetées. y texture = 0 en bas.
        /// </summary>
        public static void TexturePixelToWorld(
            in MapWindow window, int width, int height, int px, int py,
            out float worldX, out float worldY)
        {
            if (width < 1) width = 1;
            if (height < 1) height = 1;
            if (px < 0) px = 0;
            if (py < 0) py = 0;
            if (px >= width) px = width - 1;
            if (py >= height) py = height - 1;
            var rangeX = window.MaxX - window.MinX;
            var rangeY = window.MaxY - window.MinY;
            if (rangeX < 0.0001f) rangeX = 0.0001f;
            if (rangeY < 0.0001f) rangeY = 0.0001f;
            worldX = window.MinX + (px + 0.5f) / width * rangeX;
            worldY = window.MinY + (py + 0.5f) / height * rangeY;
        }
    }

    /// <summary>
    /// Conversion clic/survol → province via ProvinceAt (zéro recherche géométrique).
    /// Testable sans rendu.
    /// </summary>
    public static class MapClickPicker
    {
        /// <summary>
        /// UI locale (y bas = bas du widget) → pixel texture (y=0 en bas).
        /// Gère ScaleAndCrop : scale = max(ew/tw, eh/th), centrage.
        /// </summary>
        public static bool TryLocalToTexturePixel(
            float localX, float localY, float elementW, float elementH,
            int texW, int texH, bool uiYDown,
            out int px, out int py)
        {
            px = 0;
            py = 0;
            if (texW <= 0 || texH <= 0 || elementW <= 0.5f || elementH <= 0.5f)
                return false;

            var scale = math.max(elementW / texW, elementH / texH);
            var displayW = texW * scale;
            var displayH = texH * scale;
            var ox = (elementW - displayW) * 0.5f;
            var oy = (elementH - displayH) * 0.5f;
            var lx = localX - ox;
            var ly = localY - oy;
            if (lx < 0f || ly < 0f || lx >= displayW || ly >= displayH)
                return false;

            px = (int)(lx / scale);
            var uiPy = (int)(ly / scale);
            if (px < 0) px = 0;
            if (uiPy < 0) uiPy = 0;
            if (px >= texW) px = texW - 1;
            if (uiPy >= texH) uiPy = texH - 1;
            py = uiYDown ? (texH - 1 - uiPy) : uiPy;
            return true;
        }

        /// <summary>
        /// ProvinceAt[i] = index dans ViewsSkeleton, -1 = mer.
        /// Retourne l'Id province ou -1.
        /// Mode pilote : lit la cellule via ProvinceAt (index de vue), publie SelectedCellId,
        /// et renvoie le province_id comparable pour la navigation ECS si disponible.
        /// </summary>
        public static bool TryPickProvinceId(
            MapSnapshotExporter.MapGeometry geo, int px, int py, out int provinceId)
        {
            provinceId = -1;
            if (geo?.ProvinceAt == null || geo.ViewsSkeleton == null)
                return false;
            if (px < 0 || py < 0 || px >= geo.Width || py >= geo.Height)
                return false;
            if (geo.IsLand != null)
            {
                var landIdx = py * geo.Width + px;
                if (landIdx < 0 || landIdx >= geo.IsLand.Length || !geo.IsLand[landIdx])
                    return false;
            }

            var idx = py * geo.Width + px;
            if (idx < 0 || idx >= geo.ProvinceAt.Length)
                return false;
            var viewIndex = geo.ProvinceAt[idx];
            if (viewIndex < 0 || viewIndex >= geo.ViewsSkeleton.Count)
                return false;

            if (PilotMapProvider.Enabled)
            {
                if (!PilotMapProvider.TryPickCell(geo, px, py, out var cellId))
                    return false;
                if (PilotMapProvider.TryGetProvinceIdForNavigation(cellId, out provinceId))
                    return true;
                // Cellule sans propriétaire : sélection publiée, pas de navigation ECS.
                provinceId = -1;
                return false;
            }

            provinceId = geo.ViewsSkeleton[viewIndex].Id;
            return provinceId >= 0;
        }

        public static bool TryPickProvinceName(
            MapSnapshotExporter.MapGeometry geo, int px, int py,
            out int provinceId, out string provinceName)
        {
            provinceName = "";
            if (PilotMapProvider.Enabled)
            {
                if (!PilotMapProvider.TryPickCell(geo, px, py, out var cellId))
                {
                    provinceId = -1;
                    return false;
                }

                provinceId = cellId;
                provinceName = "cell " + cellId.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (PilotMapProvider.TryGetProvinceIdForNavigation(cellId, out var nav) && nav > 0)
                    provinceName = provinceName + " → P" + nav.ToString(System.Globalization.CultureInfo.InvariantCulture);
                return true;
            }

            if (!TryPickProvinceId(geo, px, py, out provinceId))
                return false;
            for (var i = 0; i < geo.ViewsSkeleton.Count; i++)
            {
                if (geo.ViewsSkeleton[i].Id != provinceId)
                    continue;
                provinceName = geo.ViewsSkeleton[i].ProvinceName ?? "";
                return true;
            }

            return true;
        }
    }

    /// <summary>
    /// Cache de géométries par fenêtre. Transparent : même BuildMapGeometry,
    /// cold et hot produisent des images identiques en octets.
    /// Borne mémoire (entrées + estimation octets).
    /// </summary>
    public static class MapGeometryCache
    {
        public const int MaxEntries = 12;
        /// <summary>~120 Mo — IsLand + ProvinceAt ≈ 5 octets/px × 1.92 Mpx ≈ 9.6 Mo/entrée.</summary>
        public const long MaxApproxBytes = 120L * 1024L * 1024L;

        struct CacheKey : IEquatable<CacheKey>
        {
            public int Width;
            public int Height;
            public bool IsWorld;
            public float MinX, MaxX, MinY, MaxY;

            public bool Equals(CacheKey other) =>
                Width == other.Width && Height == other.Height && IsWorld == other.IsWorld &&
                MinX.Equals(other.MinX) && MaxX.Equals(other.MaxX) &&
                MinY.Equals(other.MinY) && MaxY.Equals(other.MaxY);

            public override bool Equals(object obj) => obj is CacheKey k && Equals(k);

            public override int GetHashCode() =>
                HashCode.Combine(Width, Height, IsWorld, MinX, MaxX, MinY, MaxY);
        }

        sealed class Entry
        {
            public CacheKey Key;
            public MapSnapshotExporter.MapGeometry Geo;
            public long ApproxBytes;
        }

        static readonly LinkedList<Entry> Lru = new LinkedList<Entry>();
        static readonly Dictionary<CacheKey, LinkedListNode<Entry>> Map =
            new Dictionary<CacheKey, LinkedListNode<Entry>>();

        public static int Hits { get; private set; }
        public static int Misses { get; private set; }
        public static long ApproxBytesUsed { get; private set; }
        public static int EntryCount => Map.Count;

        public static float HitRate =>
            Hits + Misses == 0 ? 0f : (float)Hits / (Hits + Misses);

        public static void ResetStatsAndClear()
        {
            Lru.Clear();
            Map.Clear();
            Hits = 0;
            Misses = 0;
            ApproxBytesUsed = 0;
        }

        public static void Clear()
        {
            Lru.Clear();
            Map.Clear();
            ApproxBytesUsed = 0;
        }

        static CacheKey MakeKey(int width, int height, MapWindow? window)
        {
            if (!window.HasValue)
            {
                return new CacheKey
                {
                    Width = width,
                    Height = height,
                    IsWorld = true
                };
            }

            var w = window.Value;
            return new CacheKey
            {
                Width = width,
                Height = height,
                IsWorld = false,
                MinX = w.MinX,
                MaxX = w.MaxX,
                MinY = w.MinY,
                MaxY = w.MaxY
            };
        }

        static long EstimateBytes(MapSnapshotExporter.MapGeometry geo)
        {
            if (geo == null)
                return 0;
            var pixels = (long)geo.Width * geo.Height;
            // IsLand (1) + ProvinceAt (4) + slack
            return pixels * 5L + 256L * 1024L;
        }

        public static MapSnapshotExporter.MapGeometry GetOrBuild(
            int width, int height, MapWindow? window, out bool cacheHit)
        {
            var key = MakeKey(width, height, window);
            if (Map.TryGetValue(key, out var node))
            {
                Hits++;
                cacheHit = true;
                Lru.Remove(node);
                Lru.AddFirst(node);
                return node.Value.Geo;
            }

            Misses++;
            cacheHit = false;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            MapSnapshotExporter.MapGeometry geo;
            if (window.HasValue)
                geo = MapSnapshotExporter.BuildMapGeometry(width, height, window.Value);
            else
                geo = MapSnapshotExporter.BuildMapGeometry(width, height);
            sw.Stop();
            LastBuildMilliseconds = sw.Elapsed.TotalMilliseconds;

            if (geo == null)
                return null;

            Insert(key, geo);
            return geo;
        }

        public static double LastBuildMilliseconds { get; private set; }

        static void Insert(CacheKey key, MapSnapshotExporter.MapGeometry geo)
        {
            var bytes = EstimateBytes(geo);
            while ((Map.Count >= MaxEntries || ApproxBytesUsed + bytes > MaxApproxBytes) &&
                   Lru.Last != null)
                EvictLast();

            var entry = new Entry { Key = key, Geo = geo, ApproxBytes = bytes };
            var node = Lru.AddFirst(entry);
            Map[key] = node;
            ApproxBytesUsed += bytes;
        }

        static void EvictLast()
        {
            var last = Lru.Last;
            if (last == null)
                return;
            ApproxBytesUsed -= last.Value.ApproxBytes;
            if (ApproxBytesUsed < 0)
                ApproxBytesUsed = 0;
            Map.Remove(last.Value.Key);
            Lru.RemoveLast();
        }
    }

    /// <summary>
    /// Contrôleur de présentation (statique) — état de viewport hors ECS monde.
    /// MapDisplaySystem / tests / HUD lisent et écrivent ici uniquement.
    /// </summary>
    public static class MapViewport
    {
        public const float WorldMarginFraction = 0.05f;
        public const float CountryMarginFraction = 0.18f;
        /// <summary>Demi-étendue province ≈ 2.5 × CellRadius — voisins visibles, netteté locale.</summary>
        public const float ProvinceHalfExtent = MapSnapshotExporter.CellRadius * 2.5f;
        public static readonly float TextureAspect =
            (float)MapSnapshotExporter.Width / MapSnapshotExporter.Height;

        static MapViewportState _state;
        static MapWindow _worldWindow;
        static bool _initialized;
        static int _revision;
        static double _lastLevelChangeMs;
        static int _hoverProvinceId = -1;
        static string _hoverLabel = "";
        static int _selectedCityId = -1;

        public static MapViewportState State => _state;
        public static MapWindow WorldWindow => _worldWindow;
        public static int Revision => _revision;
        public static double LastLevelChangeMilliseconds => _lastLevelChangeMs;
        public static bool IsInitialized => _initialized;
        public static int HoverProvinceId => _hoverProvinceId;
        public static string HoverLabel => _hoverLabel;
        /// <summary>Ville sélectionnée (présentation seule, hors ECS). -1 = aucune.</summary>
        public static int SelectedCityId => _selectedCityId;

        public static void Reset()
        {
            _initialized = false;
            _revision = 0;
            _lastLevelChangeMs = 0;
            _state = default;
            _worldWindow = default;
            _hoverProvinceId = -1;
            _hoverLabel = "";
            _selectedCityId = -1;
            MapGeometryCache.ResetStatsAndClear();
        }

        public static void SelectCity(int cityId)
        {
            _selectedCityId = cityId;
            _revision++;
        }

        public static void ClearSelectedCity()
        {
            if (_selectedCityId < 0)
                return;
            _selectedCityId = -1;
            _revision++;
        }

        public static void SetHover(int provinceId, string label)
        {
            _hoverProvinceId = provinceId;
            _hoverLabel = label ?? "";
        }

        public static void ClearHover()
        {
            _hoverProvinceId = -1;
            _hoverLabel = "";
        }

        public static void EnsureWorldWindow(MapSnapshotExporter.MapGeometry worldGeo)
        {
            if (worldGeo == null)
                return;
            _worldWindow = new MapWindow
            {
                MinX = worldGeo.MinX,
                MaxX = worldGeo.MaxX,
                MinY = worldGeo.MinY,
                MaxY = worldGeo.MaxY
            };
            if (!_initialized)
            {
                _state = MapViewportNavigation.CreateWorld(_worldWindow);
                _initialized = true;
                _revision++;
            }
        }

        public static bool SelectCountry(Entity country, int countryId, MapWindow window)
        {
            if (!MapViewportNavigation.TrySelectCountry(
                    _state, country, countryId, window, out var next))
                return false;
            _selectedCityId = -1;
            Apply(next);
            return true;
        }

        public static bool SelectProvince(
            Entity country, int countryId,
            Entity province, int provinceId,
            MapWindow window)
        {
            if (!MapViewportNavigation.TrySelectProvince(
                    _state, country, countryId, province, provinceId, window, out var next))
                return false;
            _selectedCityId = -1;
            Apply(next);
            return true;
        }

        public static bool ZoomOut(MapWindow parentWindow)
        {
            if (!MapViewportNavigation.TryZoomOut(_state, parentWindow, out var next))
                return false;
            _selectedCityId = -1;
            Apply(next);
            return true;
        }

        public static void ForceState(MapViewportState state)
        {
            Apply(state);
            _initialized = true;
        }

        /// <summary>Déplace la fenêtre courante (pan) — présentation seule.</summary>
        public static bool Pan(float dx, float dy)
        {
            if (!_initialized)
                return false;
            var nextWin = MapViewportNavigation.PanWindow(
                _state.Window, dx, dy, _worldWindow);
            if (nextWin == _state.Window)
                return false;
            var next = _state;
            next.Window = nextWin;
            Apply(next);
            return true;
        }

        /// <summary>Zoom molette autour d'un point projeté — présentation seule.</summary>
        public static bool ZoomAt(float worldX, float worldY, float factor)
        {
            if (!_initialized)
                return false;
            var nextWin = MapViewportNavigation.ZoomWindowAt(
                _state.Window, worldX, worldY, factor, _worldWindow);
            if (nextWin == _state.Window)
                return false;
            // Zoom arrière extrême au niveau World → fenêtre monde.
            if (_state.Level == MapObservationLevel.World &&
                nextWin.Width >= _worldWindow.Width * 0.98f)
            {
                nextWin = _worldWindow;
            }

            var next = _state;
            next.Window = nextWin;
            Apply(next);
            return true;
        }

        static void Apply(MapViewportState next)
        {
            // Coût wall-clock hors déterminisme (mesure outillage uniquement).
            var t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            _state = next;
            _revision++;
            var t1 = System.Diagnostics.Stopwatch.GetTimestamp();
            _lastLevelChangeMs = (t1 - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        }

        /// <summary>Construit la fenêtre pays depuis les provinces d'un propriétaire (ids).</summary>
        public static MapWindow BuildCountryWindow(
            MapSnapshotExporter.MapGeometry worldGeo,
            HashSet<int> provinceIds)
        {
            if (worldGeo?.ViewsSkeleton == null || provinceIds == null || provinceIds.Count == 0)
                return _worldWindow;

            var xs = new List<float>(provinceIds.Count);
            var ys = new List<float>(provinceIds.Count);
            for (var i = 0; i < worldGeo.ViewsSkeleton.Count; i++)
            {
                var v = worldGeo.ViewsSkeleton[i];
                if (!provinceIds.Contains(v.Id))
                    continue;
                xs.Add(v.X);
                ys.Add(v.Y);
            }

            if (xs.Count == 0)
                return _worldWindow;

            var nxs = new NativeArray<float>(xs.Count, Allocator.Temp);
            var nys = new NativeArray<float>(ys.Count, Allocator.Temp);
            try
            {
                for (var i = 0; i < xs.Count; i++)
                {
                    nxs[i] = xs[i];
                    nys[i] = ys[i];
                }

                return MapViewportNavigation.ComputeWindowFromPoints(
                    nxs, nys, CountryMarginFraction, TextureAspect);
            }
            finally
            {
                if (nxs.IsCreated) nxs.Dispose();
                if (nys.IsCreated) nys.Dispose();
            }
        }

        public static MapWindow BuildProvinceWindow(
            MapSnapshotExporter.MapGeometry worldGeo, int provinceId)
        {
            if (worldGeo?.ViewsSkeleton == null)
                return _worldWindow;
            for (var i = 0; i < worldGeo.ViewsSkeleton.Count; i++)
            {
                var v = worldGeo.ViewsSkeleton[i];
                if (v.Id != provinceId)
                    continue;
                return MapViewportNavigation.ComputePointWindow(
                    v.X, v.Y, ProvinceHalfExtent, TextureAspect);
            }

            return _worldWindow;
        }

        public static MapSnapshotExporter.LabelDensity LabelDensityFor(MapObservationLevel level)
        {
            switch (level)
            {
                case MapObservationLevel.Country:
                    return MapSnapshotExporter.LabelDensity.Provinces;
                case MapObservationLevel.Province:
                    return MapSnapshotExporter.LabelDensity.SelectedProvince;
                default:
                    return MapSnapshotExporter.LabelDensity.Countries;
            }
        }
    }

    /// <summary>
    /// Lecture seule ECS → agrégats réels d'une province (stocks, flux, pops, dev…).
    /// Aucune invention de ville/quartier/maison. Priorité lisibilité d'état.
    /// </summary>
    public static class ProvinceObservation
    {
        public struct Snapshot
        {
            public int ProvinceId;
            public string ProvinceName;
            public string OwnerTag;
            public string OwnerName;
            public int Tax;
            public int Production;
            public int Manpower;
            public float PhysicalSatisfaction;
            public float LodSatisfaction;
            public float BlendedSatisfaction;
            public float BlendWeight;
            public List<StockLine> Stocks;
            public List<ActivityLine> Activities;
            public List<DeficitLine> Deficits;
            public List<CargoLine> CargoIn;
            public List<CargoLine> CargoOut;
            public List<PopLine> Pops;
            public List<BuildingLine> Buildings;
            public string SummaryLine;
            public string DetailBlock;
        }

        public struct BuildingLine
        {
            public int BuildingId;
            public BuildingType Type;
            public int CityId;
            public bool IsComplete;
            public float Capacity;
        }

        public struct StockLine
        {
            public int GoodId;
            public string Tag;
            public double Quantity;
        }

        public struct ActivityLine
        {
            public int GoodId;
            public string Tag;
            public float BaseCapacity;
            public float RelativeIntensity;
        }

        public struct DeficitLine
        {
            public int GoodId;
            public string Tag;
            public float Amount;
        }

        public struct CargoLine
        {
            public int OriginId;
            public int DestId;
            public int GoodId;
            public string Tag;
            public double Quantity;
            public int TicksRemaining;
        }

        public struct PopLine
        {
            public PopType Type;
            public int Size;
            public string Culture;
            public string Religion;
            public float LodSat;
        }

        public static bool TryCapture(
            EntityManager em,
            int provinceId,
            string provinceName,
            out Snapshot snap)
        {
            snap = default;
            Entity provinceEntity = Entity.Null;
            ProvinceOwnership ownership = default;
            ProvinceDevelopment dev = default;
            var found = false;

            using (var q = em.CreateEntityQuery(
                       ComponentType.ReadOnly<ProvinceData>(),
                       ComponentType.ReadOnly<ProvinceOwnership>(),
                       ComponentType.ReadOnly<ProvinceDevelopment>()))
            using (var entities = q.ToEntityArray(Allocator.Temp))
            using (var pdata = q.ToComponentDataArray<ProvinceData>(Allocator.Temp))
            using (var owns = q.ToComponentDataArray<ProvinceOwnership>(Allocator.Temp))
            using (var devs = q.ToComponentDataArray<ProvinceDevelopment>(Allocator.Temp))
            {
                for (var i = 0; i < pdata.Length; i++)
                {
                    if (pdata[i].ProvinceId != provinceId)
                        continue;
                    provinceEntity = entities[i];
                    ownership = owns[i];
                    dev = devs[i];
                    found = true;
                    break;
                }
            }

            if (!found || provinceEntity == Entity.Null)
                return false;

            var goodTags = LoadGoodTags(em);
            var ownerTag = "";
            var ownerName = "";
            if (ownership.Owner != Entity.Null && em.HasComponent<CountryData>(ownership.Owner))
            {
                var cd = em.GetComponentData<CountryData>(ownership.Owner);
                ownerTag = cd.Tag.ToString();
                ownerName = cd.Name.ToString();
            }

            float physSat = 0f;
            if (em.HasComponent<PhysicalDemandSnapshot>(provinceEntity))
                physSat = em.GetComponentData<PhysicalDemandSnapshot>(provinceEntity)
                    .PhysicalSatisfaction;

            var stocks = new List<StockLine>(16);
            if (em.HasBuffer<ProvinceStock>(provinceEntity))
            {
                var buf = em.GetBuffer<ProvinceStock>(provinceEntity);
                for (var i = 0; i < buf.Length; i++)
                {
                    if (buf[i].Quantity <= 0.0 && buf[i].GoodId == 0)
                        continue;
                    stocks.Add(new StockLine
                    {
                        GoodId = buf[i].GoodId,
                        Tag = TagOf(goodTags, buf[i].GoodId),
                        Quantity = buf[i].Quantity
                    });
                }
            }

            stocks.Sort((a, b) => b.Quantity.CompareTo(a.Quantity));

            var activities = new List<ActivityLine>(8);
            if (em.HasBuffer<ProvincePhysicalActivity>(provinceEntity))
            {
                var buf = em.GetBuffer<ProvincePhysicalActivity>(provinceEntity);
                for (var i = 0; i < buf.Length; i++)
                {
                    activities.Add(new ActivityLine
                    {
                        GoodId = buf[i].GoodId,
                        Tag = TagOf(goodTags, buf[i].GoodId),
                        BaseCapacity = buf[i].BaseCapacity,
                        RelativeIntensity = buf[i].RelativeIntensity
                    });
                }
            }

            var deficits = new List<DeficitLine>(8);
            if (em.HasBuffer<PhysicalInputDeficit>(provinceEntity))
            {
                var buf = em.GetBuffer<PhysicalInputDeficit>(provinceEntity);
                for (var i = 0; i < buf.Length; i++)
                {
                    if (buf[i].Amount <= 0f)
                        continue;
                    deficits.Add(new DeficitLine
                    {
                        GoodId = buf[i].GoodId,
                        Tag = TagOf(goodTags, buf[i].GoodId),
                        Amount = buf[i].Amount
                    });
                }
            }

            var cargoIn = new List<CargoLine>(16);
            var cargoOut = new List<CargoLine>(16);
            CollectCargo(em, provinceId, goodTags, cargoIn, cargoOut);

            var pops = new List<PopLine>(16);
            double lodSum = 0;
            double lodW = 0;
            using (var pq = em.CreateEntityQuery(ComponentType.ReadOnly<PopData>()))
            using (var popArr = pq.ToComponentDataArray<PopData>(Allocator.Temp))
            {
                for (var i = 0; i < popArr.Length; i++)
                {
                    if (popArr[i].Province != provinceEntity)
                        continue;
                    pops.Add(new PopLine
                    {
                        Type = popArr[i].Type,
                        Size = popArr[i].Size,
                        Culture = popArr[i].CultureTag.ToString(),
                        Religion = popArr[i].ReligionTag.ToString(),
                        LodSat = popArr[i].NeedsSatisfaction
                    });
                    lodSum += popArr[i].NeedsSatisfaction * popArr[i].Size;
                    lodW += popArr[i].Size;
                }
            }

            pops.Sort((a, b) => b.Size.CompareTo(a.Size));
            var lodSat = lodW > 0 ? (float)(lodSum / lodW) : 0f;
            var blendW = PhysicalSatisfactionBlendSystem.PhysicalBlendWeight;
            var blended = (1f - blendW) * lodSat + blendW * physSat;

            var buildings = new List<BuildingLine>(16);
            using (var bq = em.CreateEntityQuery(ComponentType.ReadOnly<BuildingData>()))
            using (var barr = bq.ToComponentDataArray<BuildingData>(Allocator.Temp))
            {
                for (var i = 0; i < barr.Length; i++)
                {
                    if (barr[i].ProvinceId != provinceId)
                        continue;
                    buildings.Add(new BuildingLine
                    {
                        BuildingId = barr[i].BuildingId,
                        Type = barr[i].Type,
                        CityId = barr[i].CityId,
                        IsComplete = barr[i].IsComplete != 0,
                        Capacity = barr[i].CapacityContribution
                    });
                }
            }

            buildings.Sort((a, b) => a.BuildingId.CompareTo(b.BuildingId));

            var sb = new StringBuilder(1024);
            sb.Append("PROV ").Append(provinceId);
            if (!string.IsNullOrEmpty(provinceName))
                sb.Append(' ').Append(Sanitize(provinceName));
            sb.Append("  OWN ").Append(string.IsNullOrEmpty(ownerTag) ? "?" : ownerTag);
            sb.Append("  DEV T").Append(dev.Tax)
                .Append("/P").Append(dev.Production)
                .Append("/M").Append(dev.Manpower);
            sb.Append("  SAT PHY=").Append(Fmt2(physSat))
                .Append(" LOD=").Append(Fmt2(lodSat))
                .Append(" BLEND=").Append(Fmt2(blended))
                .Append("(w=").Append(Fmt2(blendW)).Append(')');

            var detail = BuildDetailBlock(
                provinceId, provinceName, ownerTag, ownerName, dev,
                physSat, lodSat, blended, blendW,
                stocks, activities, deficits, cargoIn, cargoOut, pops, buildings);

            snap = new Snapshot
            {
                ProvinceId = provinceId,
                ProvinceName = provinceName ?? "",
                OwnerTag = ownerTag,
                OwnerName = ownerName,
                Tax = dev.Tax,
                Production = dev.Production,
                Manpower = dev.Manpower,
                PhysicalSatisfaction = physSat,
                LodSatisfaction = lodSat,
                BlendedSatisfaction = blended,
                BlendWeight = blendW,
                Stocks = stocks,
                Activities = activities,
                Deficits = deficits,
                CargoIn = cargoIn,
                CargoOut = cargoOut,
                Pops = pops,
                Buildings = buildings,
                SummaryLine = sb.ToString(),
                DetailBlock = detail
            };
            return true;
        }

        static void CollectCargo(
            EntityManager em,
            int provinceId,
            Dictionary<int, string> goodTags,
            List<CargoLine> cargoIn,
            List<CargoLine> cargoOut)
        {
            using var q = em.CreateEntityQuery(
                ComponentType.ReadOnly<ProvinceData>(),
                ComponentType.ReadOnly<CargoInTransit>());
            using var entities = q.ToEntityArray(Allocator.Temp);
            using var pdata = q.ToComponentDataArray<ProvinceData>(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                if (!em.HasBuffer<CargoInTransit>(entities[i]))
                    continue;
                var buf = em.GetBuffer<CargoInTransit>(entities[i]);
                for (var c = 0; c < buf.Length; c++)
                {
                    var cargo = buf[c];
                    if (cargo.Quantity <= 0.0)
                        continue;
                    var line = new CargoLine
                    {
                        OriginId = cargo.OriginProvinceId,
                        DestId = cargo.DestProvinceId,
                        GoodId = cargo.GoodId,
                        Tag = TagOf(goodTags, cargo.GoodId),
                        Quantity = cargo.Quantity,
                        TicksRemaining = cargo.TicksRemaining
                    };
                    if (cargo.DestProvinceId == provinceId)
                        cargoIn.Add(line);
                    if (cargo.OriginProvinceId == provinceId)
                        cargoOut.Add(line);
                }
            }
        }

        static string BuildDetailBlock(
            int provinceId,
            string provinceName,
            string ownerTag,
            string ownerName,
            ProvinceDevelopment dev,
            float physSat,
            float lodSat,
            float blended,
            float blendW,
            List<StockLine> stocks,
            List<ActivityLine> activities,
            List<DeficitLine> deficits,
            List<CargoLine> cargoIn,
            List<CargoLine> cargoOut,
            List<PopLine> pops,
            List<BuildingLine> buildings)
        {
            var sb = new StringBuilder(3072);

            // --- Identité ---
            sb.Append("--- IDENTITY ---\n");
            sb.Append("PROVINCE ").Append(provinceId);
            if (!string.IsNullOrEmpty(provinceName))
                sb.Append(' ').Append(Sanitize(provinceName));
            sb.Append('\n');
            sb.Append("OWNER  ").Append(string.IsNullOrEmpty(ownerTag) ? "?" : ownerTag);
            if (!string.IsNullOrEmpty(ownerName))
                sb.Append("  ").Append(Sanitize(ownerName));
            sb.Append('\n');
            sb.Append("DEV    TAX=").Append(dev.Tax)
                .Append("  PROD=").Append(dev.Production)
                .Append("  MAN=").Append(dev.Manpower).Append('\n');

            // --- Pourquoi la faim ? (diagnostic d'un coup d'œil) ---
            sb.Append("--- WHY HUNGRY ---\n");
            AppendHungerDiagnosis(sb, deficits, activities, cargoIn);

            // --- Population (libellé / taille / culture séparés) ---
            sb.Append("--- POPULATION ---\n");
            if (pops.Count == 0)
                sb.Append("(none)\n");
            else
            {
                for (var i = 0; i < pops.Count && i < 6; i++)
                {
                    sb.Append(PadRight(pops[i].Type.ToString().ToUpperInvariant(), 10))
                        .Append(' ')
                        .Append(PadLeft(pops[i].Size.ToString(CultureInfo.InvariantCulture), 6))
                        .Append(' ')
                        .Append(Sanitize(pops[i].Culture));
                    if (!string.IsNullOrEmpty(pops[i].Religion))
                        sb.Append(' ').Append(Sanitize(pops[i].Religion));
                    sb.Append('\n');
                }
            }

            // --- Production / stocks ---
            sb.Append("--- PROD STOCKS ---\n");
            sb.Append("ACT ");
            if (activities.Count == 0)
                sb.Append("(none)");
            else
            {
                for (var i = 0; i < activities.Count && i < 5; i++)
                {
                    if (i > 0) sb.Append(" |");
                    sb.Append(' ').Append(Sanitize(activities[i].Tag))
                        .Append(" cap=").Append(Fmt1(activities[i].BaseCapacity));
                }
            }

            sb.Append('\n');
            sb.Append("STOCK");
            var stockShown = 0;
            for (var i = 0; i < stocks.Count && stockShown < 8; i++)
            {
                if (stocks[i].Quantity <= 0.5)
                    continue;
                sb.Append(' ').Append(Sanitize(stocks[i].Tag)).Append('=')
                    .Append(Fmt0(stocks[i].Quantity));
                stockShown++;
            }

            if (stockShown == 0)
                sb.Append(" (empty)");
            sb.Append('\n');

            // --- Bâtiments (v1_039 — panneau province distinct de la fiche ville) ---
            sb.Append("--- BUILDINGS ---\n");
            if (buildings == null || buildings.Count == 0)
                sb.Append("(none)\n");
            else
            {
                for (var i = 0; i < buildings.Count && i < 12; i++)
                {
                    var b = buildings[i];
                    sb.Append(b.Type.ToString().ToUpperInvariant())
                        .Append(" id=").Append(b.BuildingId.ToString(CultureInfo.InvariantCulture))
                        .Append(" city=").Append(b.CityId.ToString(CultureInfo.InvariantCulture))
                        .Append(b.IsComplete ? " COMPLETE" : " SITE")
                        .Append(" cap=")
                        .Append(b.Capacity.ToString("0.#", CultureInfo.InvariantCulture))
                        .Append('\n');
                }
            }

            // --- Flux ---
            sb.Append("--- TRADE FLOWS ---\n");
            AppendFlowDetail(sb, "IN ", cargoIn, activities);
            AppendFlowDetail(sb, "OUT", cargoOut, activities);

            // --- Satisfaction ---
            sb.Append("--- SATISFACTION ---\n");
            sb.Append("PHY=").Append(Fmt2(physSat))
                .Append("  LOD=").Append(Fmt2(lodSat))
                .Append("  MIX=").Append(Fmt2(blended))
                .Append("  W=").Append(Fmt2(blendW));

            return sb.ToString();
        }

        static void AppendHungerDiagnosis(
            StringBuilder sb,
            List<DeficitLine> deficits,
            List<ActivityLine> activities,
            List<CargoLine> cargoIn)
        {
            if (deficits.Count == 0)
            {
                sb.Append("OK  no input deficit\n");
                return;
            }

            for (var i = 0; i < deficits.Count && i < 4; i++)
            {
                var d = deficits[i];
                var localCap = 0f;
                for (var a = 0; a < activities.Count; a++)
                {
                    if (activities[a].GoodId == d.GoodId)
                        localCap += activities[a].BaseCapacity;
                }

                double inbound = 0;
                for (var c = 0; c < cargoIn.Count; c++)
                {
                    if (cargoIn[c].GoodId == d.GoodId)
                        inbound += cargoIn[c].Quantity;
                }

                string cause;
                if (localCap <= 0.01f && inbound <= 0.01)
                    cause = "NO_LOCAL_NO_ROUTE";
                else if (localCap <= 0.01f && inbound > 0.01)
                    cause = "IMPORT_SHORT";
                else if (inbound <= 0.01)
                    cause = "LOCAL_LOW_NO_ROUTE";
                else
                    cause = "INPUT_SHORT";

                sb.Append("NEED ").Append(Sanitize(d.Tag))
                    .Append('=').Append(Fmt1(d.Amount))
                    .Append("  ").Append(cause)
                    .Append("  loc=").Append(Fmt1(localCap))
                    .Append("  in=").Append(Fmt0(inbound))
                    .Append('\n');
            }
        }

        static void AppendFlowDetail(
            StringBuilder sb, string label, List<CargoLine> cargo, List<ActivityLine> activities)
        {
            sb.Append(label);
            if (cargo.Count == 0)
            {
                sb.Append(" (none)\n");
                return;
            }

            var byGood = new Dictionary<int, (string tag, double qty, int ticks, int peer)>();
            for (var i = 0; i < cargo.Count; i++)
            {
                var c = cargo[i];
                if (!byGood.TryGetValue(c.GoodId, out var cur))
                    cur = (c.Tag, 0, int.MaxValue, label.StartsWith("IN") ? c.OriginId : c.DestId);
                cur.qty += c.Quantity;
                if (c.TicksRemaining < cur.ticks)
                    cur.ticks = c.TicksRemaining;
                byGood[c.GoodId] = cur;
            }

            var shown = 0;
            var keys = new List<int>(byGood.Keys);
            keys.Sort();
            for (var ki = 0; ki < keys.Count && shown < 4; ki++)
            {
                var kv = byGood[keys[ki]];
                var localCap = 0f;
                for (var a = 0; a < activities.Count; a++)
                {
                    if (activities[a].GoodId == keys[ki])
                        localCap += activities[a].BaseCapacity;
                }

                var importShare = kv.qty / math.max(1.0, kv.qty + localCap);
                if (shown > 0)
                    sb.Append(" |");
                sb.Append(' ').Append(Sanitize(kv.tag))
                    .Append('=').Append(Fmt0(kv.qty));
                if (label.StartsWith("IN"))
                    sb.Append(" from P").Append(kv.peer);
                else
                    sb.Append(" to P").Append(kv.peer);
                sb.Append(" shr=").Append(Fmt0(importShare * 100.0)).Append('%');
                shown++;
            }

            sb.Append('\n');
        }

        static string PadRight(string s, int width)
        {
            if (s == null) s = "";
            if (s.Length >= width) return s;
            return s + new string(' ', width - s.Length);
        }

        static string PadLeft(string s, int width)
        {
            if (s == null) s = "";
            if (s.Length >= width) return s;
            return new string(' ', width - s.Length) + s;
        }

        static Dictionary<int, string> LoadGoodTags(EntityManager em)
        {
            var map = new Dictionary<int, string>(16);
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<GoodData>());
            using var goods = q.ToComponentDataArray<GoodData>(Allocator.Temp);
            for (var i = 0; i < goods.Length; i++)
                map[goods[i].GoodId] = goods[i].Tag.ToString();
            return map;
        }

        static string TagOf(Dictionary<int, string> map, int goodId) =>
            map.TryGetValue(goodId, out var t) && !string.IsNullOrEmpty(t)
                ? t
                : goodId.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// Préservation Unicode pour le DetailBlock UI Toolkit (accents).
        /// Ne remplace plus Î → vide (ex. Île-de-France).
        /// </summary>
        static string Sanitize(string s)
        {
            if (string.IsNullOrEmpty(s))
                return "";
            var sb = new StringBuilder(s.Length);
            for (var i = 0; i < s.Length; i++)
            {
                var c = s[i];
                if (c == '\n' || c == '\r' || c == '\t')
                {
                    sb.Append(' ');
                    continue;
                }

                if (char.IsControl(c))
                    continue;
                sb.Append(c);
            }

            return sb.ToString().Trim();
        }

        static string Fmt0(double v) =>
            Math.Round(v).ToString(CultureInfo.InvariantCulture);

        static string Fmt1(float v) =>
            v.ToString("0.0", CultureInfo.InvariantCulture);

        static string Fmt2(float v) =>
            v.ToString("0.00", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Système de présentation : synchronise l'état de viewport (aucune écriture ECS monde).
    /// La logique de navigation est dans <see cref="MapViewportNavigation"/> (testable sans rendu).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct MapViewportSystem : ISystem
    {
        public void OnCreate(ref SystemState state) { }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // État de présentation hors ECS — rien à écrire dans le monde.
            // Les transitions sont appliquées via MapViewport (HUD / tests / hotkeys managés).
        }

        public void OnDestroy(ref SystemState state) { }
    }
}
