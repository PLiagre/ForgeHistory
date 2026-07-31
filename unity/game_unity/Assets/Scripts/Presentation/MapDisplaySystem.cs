using Unity.Entities;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Unity.Mathematics;
using UnityEngine;
using VictoriaGame.Core;
using VictoriaGame.World;

namespace VictoriaGame.Presentation
{
    /// <summary>
    /// Premier SystemBase de la Phase V — OBLIGATOIREMENT PresentationSystemGroup.
    /// Texture2D + UI Toolkit = managé → SystemBase (pas Burst / pas ISystem).
    /// Géométrie monde construite UNE fois ; re-construite uniquement au changement
    /// de fenêtre (zoom). Recoloriage tous les <see cref="RefreshIntervalTicks"/> ticks
    /// ou dès qu'un OwnerChangedTick / couche / viewport a changé. Lecture seule ECS.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class MapDisplaySystem : SystemBase
    {
        /// <summary>Cadence de rafraîchissement texture : tous les N ticks de simulation.</summary>
        public const int RefreshIntervalTicks = 10;

        public enum DisplayLayer : byte
        {
            Political = 0,
            Satisfaction = 1,
            Population = 2,
            Army = 3,
            TradeNode = 4
        }

        MapSnapshotExporter.MapGeometry _worldGeometry;
        MapSnapshotExporter.MapGeometry _activeGeometry;
        int _geometryBuilds;
        int _lastRenderedTick = -1;
        int _lastOwnerChangeFingerprint = -1;
        int _lastViewportRevision = -1;
        DisplayLayer _layer = DisplayLayer.Political;
        DisplayLayer _lastPresentedLayer = DisplayLayer.Political;
        WorldMetrics.Snapshot _lastSnap;
        string _lastMetricsLine = "";
        string _lastProvinceDetail = "";
        string _lastCountryDetail = "";
        string _lastCityDetail = "";
        MapLayerRenderer.Palettes _palettes;
        MapLayerRenderer.FixedDomains _domains;
        CountryColors.Table _colors;
        bool _layerAssetsReady;
        Color32[] _lastPixels;
        int _lastPixelW;
        int _lastPixelH;
        MapWindow _lastPresentedWindow;
        int _lastHoverProvinceId = -1;
        double _lastPerceivedResponseMs;

        static DisplayLayer? _forcedLayer;
        static bool _forceRefresh;
        static MapDisplaySystem _instanceForGeo;

        public static int GeometryBuilds { get; private set; }
        public static double LastWindowRebuildMilliseconds { get; private set; }
        public static double LastPerceivedResponseMilliseconds { get; private set; }
        public static DisplayLayer CurrentLayer { get; private set; }
        public static string LastMetricsLine { get; private set; } = "";
        public static string LastProvinceDetail { get; private set; } = "";
        public static string LastCountryDetail { get; private set; } = "";
        public static string LastCityDetail { get; private set; } = "";
        public static WorldMetrics.Snapshot LastSnapshot { get; private set; }
        public static bool HasPresentedFrame { get; private set; }
        public static MapSnapshotExporter.MapGeometry ActiveGeometry { get; private set; }
        public static bool LastGeometryCacheHit { get; private set; }

        public static void ForceLayer(DisplayLayer layer)
        {
            _forcedLayer = layer;
            _forceRefresh = true;
        }

        public static void RequestRefresh() => _forceRefresh = true;

        protected override void OnCreate()
        {
            GeometryBuilds = 0;
            LastWindowRebuildMilliseconds = 0;
            LastPerceivedResponseMilliseconds = 0;
            HasPresentedFrame = false;
            CurrentLayer = DisplayLayer.Political;
            LastMetricsLine = "";
            LastProvinceDetail = "";
            LastCountryDetail = "";
            LastCityDetail = "";
            ActiveGeometry = null;
            LastGeometryCacheHit = false;
            _forcedLayer = null;
            _forceRefresh = false;
            MapViewport.Reset();
        }

        protected override void OnStartRunning()
        {
            _instanceForGeo = this;
        }

        protected override void OnStopRunning()
        {
            if (_instanceForGeo == this)
                _instanceForGeo = null;
        }

        protected override void OnUpdate()
        {
            var em = EntityManager;
            if (!SystemAPI.HasSingleton<WorldState>())
                return;

            _instanceForGeo = this;
            var worldState = SystemAPI.GetSingleton<WorldState>();
            var tick = worldState.CurrentTick;

            HandleLayerHotkeys();
            HandlePaceHotkeys();
            HandleViewportHotkeys();
            if (_forcedLayer.HasValue)
            {
                _layer = _forcedLayer.Value;
                _forcedLayer = null;
            }

            var layerChanged = _layer != _lastPresentedLayer;
            var ownerFp = ComputeOwnerChangeFingerprint(em);
            var dueByTick = _lastRenderedTick < 0
                || tick - _lastRenderedTick >= RefreshIntervalTicks
                || tick < _lastRenderedTick;
            var dueByOwner = ownerFp != _lastOwnerChangeFingerprint;
            var viewportChanged = MapViewport.Revision != _lastViewportRevision;
            var hoverChanged = MapViewport.HoverProvinceId != _lastHoverProvinceId;
            if (!dueByTick && !dueByOwner && !layerChanged && !viewportChanged &&
                !hoverChanged && !_forceRefresh && HasPresentedFrame)
            {
                var hudEarly = InGameHud.Instance;
                if (hudEarly != null && hudEarly.MapTexture != null)
                {
                    hudEarly.RefreshInfoBar(AppendHover(_lastMetricsLine));
                    hudEarly.RefreshProvincePanel(_lastProvinceDetail);
                    hudEarly.RefreshCountryPanel(_lastCountryDetail);
                    hudEarly.RefreshHoverLabel(MapViewport.HoverLabel);
                    return;
                }
            }

            // Raffinement progressif : image immédiate avant le rebuild net.
            //
            // v1_095 — DEUX QUALITÉS D'IMAGE IMMÉDIATE, dans cet ordre :
            //   1. le fond GPU, net et géographiquement juste (0,03 ms mesurées) ;
            //   2. à défaut, l'ancien recadrage étiré du dernier rendu CPU.
            // La 2 reste parce que le GPU n'est disponible qu'en mode pilote et sur
            // un matériel qui accepte le shader ; on ne remplace pas un chemin qui
            // marche partout par un chemin qui marche souvent.
            if (viewportChanged && HasPresentedFrame)
            {
                var gpuFrame = TryRenderGpuBackground(em);
                if (gpuFrame != null)
                {
                    var hudGpu = InGameHud.Instance;
                    if (hudGpu == null)
                    {
                        var go = new GameObject("InGameHud");
                        hudGpu = go.AddComponent<InGameHud>();
                    }

                    hudGpu.PresentRenderTexture(gpuFrame, AppendHover(_lastMetricsLine));
                }
            }

            if (viewportChanged && !GpuBackgroundUsedThisFrame &&
                _lastPixels != null && HasPresentedFrame)
            {
                var preview = CropScalePreview(
                    _lastPixels, _lastPixelW, _lastPixelH,
                    _lastPresentedWindow, MapViewport.State.Window,
                    MapSnapshotExporter.Width, MapSnapshotExporter.Height);
                if (preview != null)
                {
                    var hudPrev = InGameHud.Instance;
                    if (hudPrev == null)
                    {
                        var go = new GameObject("InGameHud");
                        hudPrev = go.AddComponent<InGameHud>();
                    }

                    hudPrev.PresentFrame(
                        preview, MapSnapshotExporter.Width, MapSnapshotExporter.Height,
                        AppendHover(_lastMetricsLine));
                }
            }

            if (_worldGeometry == null)
            {
                var swWorld = Stopwatch.StartNew();
                _worldGeometry = MapGeometryCache.GetOrBuild(
                    MapSnapshotExporter.Width, MapSnapshotExporter.Height, null, out var worldHit);
                swWorld.Stop();
                LastWindowRebuildMilliseconds = swWorld.Elapsed.TotalMilliseconds;
                LastPerceivedResponseMilliseconds = LastWindowRebuildMilliseconds;
                LastGeometryCacheHit = worldHit;
                _geometryBuilds++;
                GeometryBuilds = _geometryBuilds;
                if (_worldGeometry == null)
                {
                    UnityEngine.Debug.LogWarning("MapDisplaySystem: BuildMapGeometry a échoué.");
                    return;
                }

                MapViewport.EnsureWorldWindow(_worldGeometry);
                _activeGeometry = _worldGeometry;
                ActiveGeometry = _activeGeometry;
                UnityEngine.Debug.Log(
                    $"MapDisplaySystem: GEOMETRY_BUILDS={_geometryBuilds} " +
                    $"worldMs={LastWindowRebuildMilliseconds:0.0} cacheHit={worldHit} " +
                    $"(cadence refresh={RefreshIntervalTicks} ticks ou Owner/layer/viewport change)");
            }

            if (viewportChanged || _activeGeometry == null)
            {
                var perceivedSw = Stopwatch.StartNew();
                EnsureGeometryForViewport();
                perceivedSw.Stop();
                _lastPerceivedResponseMs = perceivedSw.Elapsed.TotalMilliseconds;
                LastPerceivedResponseMilliseconds = _lastPerceivedResponseMs;
                _lastViewportRevision = MapViewport.Revision;
            }

            EnsureLayerAssets();

            _lastSnap = WorldMetrics.Capture(em, tick);
            LastSnapshot = _lastSnap;
            _lastMetricsLine = FormatPanelLine(tick, worldState.Year, in _lastSnap, MapViewport.State);
            LastMetricsLine = _lastMetricsLine;
            _lastProvinceDetail = BuildProvinceDetailIfNeeded(em);
            LastProvinceDetail = _lastProvinceDetail;
            _lastCountryDetail = BuildCountryDetailIfNeeded(em);
            LastCountryDetail = _lastCountryDetail;
            _lastCityDetail = BuildCityDetailIfNeeded(em);
            LastCityDetail = _lastCityDetail;

            var labels = MapViewport.LabelDensityFor(MapViewport.State.Level);
            var selectedProv = MapViewport.State.TargetProvinceId;
            var hoverProv = MapViewport.HoverProvinceId;
            var geo = _activeGeometry;
            var level = MapViewport.State.Level;
            var filterProv = level == MapObservationLevel.Province
                ? MapViewport.State.TargetProvinceId
                : -1;
            var filterCountry = level == MapObservationLevel.Country
                ? MapViewport.State.TargetCountryId
                : -1;

            // ui_001 — texture interactive map-only : pas de panneau/dump bitmap.
            // Les exports diagnostiques (MapSnapshotExporter) gardent leurs overlays.
            Color32[] pixels;
            if (_layer == DisplayLayer.Political)
            {
                pixels = MapSnapshotExporter.RenderPoliticalPixels(
                    em, geo, labels, selectedProv,
                    overlay: p => ComposeInteractiveMapOnly(
                        p, geo, em, level, filterProv, filterCountry, hoverProv));
            }
            else
            {
                var frame = MapLayerRenderer.CaptureFrame(em, geo, _colors, tick);
                var kind = ToLayerKind(_layer);
                pixels = MapLayerRenderer.RenderLayerToPixels(
                    geo, frame, kind, _palettes, _domains, _colors,
                    extraOverlay: p => ComposeInteractiveMapOnly(
                        p, geo, em, level, filterProv, filterCountry, hoverProv,
                        thematicLayer: true));
            }

            if (pixels == null)
                return;

            var hud = InGameHud.Instance;
            if (hud == null)
            {
                var go = new GameObject("InGameHud");
                hud = go.AddComponent<InGameHud>();
            }

            hud.PresentFrame(pixels, geo.Width, geo.Height, AppendHover(_lastMetricsLine));
            hud.RefreshInfoBar(AppendHover(_lastMetricsLine));
            hud.RefreshProvincePanel(
                !string.IsNullOrEmpty(_lastCityDetail) ? _lastCityDetail : _lastProvinceDetail);
            hud.RefreshCountryPanel(_lastCountryDetail);
            hud.RefreshHoverLabel(MapViewport.HoverLabel);
            _lastPixels = pixels;
            _lastPixelW = geo.Width;
            _lastPixelH = geo.Height;
            _lastPresentedWindow = MapViewport.State.Window;
            _lastHoverProvinceId = hoverProv;
            _lastRenderedTick = tick;
            _lastOwnerChangeFingerprint = ownerFp;
            _lastPresentedLayer = _layer;
            _forceRefresh = false;
            HasPresentedFrame = true;
            CurrentLayer = _layer;
        }

        void EnsureGeometryForViewport()
        {
            var state = MapViewport.State;
            if (state.Level == MapObservationLevel.World &&
                ApproximatelyWorldWindow(state.Window))
            {
                // Monde plein : réutiliser / cacher la géométrie monde.
                var world = MapGeometryCache.GetOrBuild(
                    MapSnapshotExporter.Width, MapSnapshotExporter.Height, null, out var hit);
                LastGeometryCacheHit = hit;
                LastWindowRebuildMilliseconds = hit ? 0 : MapGeometryCache.LastBuildMilliseconds;
                if (world != null)
                    _worldGeometry = world;
                _activeGeometry = _worldGeometry;
                ActiveGeometry = _activeGeometry;
                return;
            }

            var sw = Stopwatch.StartNew();
            var zoomed = MapGeometryCache.GetOrBuild(
                MapSnapshotExporter.Width, MapSnapshotExporter.Height, state.Window, out var cacheHit);
            sw.Stop();
            LastGeometryCacheHit = cacheHit;
            LastWindowRebuildMilliseconds = cacheHit ? 0 : sw.Elapsed.TotalMilliseconds;
            if (zoomed == null)
            {
                _activeGeometry = _worldGeometry;
                ActiveGeometry = _activeGeometry;
                return;
            }

            _activeGeometry = zoomed;
            ActiveGeometry = _activeGeometry;
            if (!cacheHit)
            {
                _geometryBuilds++;
                GeometryBuilds = _geometryBuilds;
            }

            UnityEngine.Debug.Log(
                $"MapDisplaySystem: viewport rebuild level={state.Level} " +
                $"prov={state.TargetProvinceId} country={state.TargetCountryId} " +
                $"ms={LastWindowRebuildMilliseconds:0.0} cacheHit={cacheHit} " +
                $"GEOMETRY_BUILDS={_geometryBuilds}");
        }

        static bool ApproximatelyWorldWindow(MapWindow w)
        {
            var ww = MapViewport.WorldWindow;
            if (!MapViewport.IsInitialized)
                return true;
            return math.abs(w.MinX - ww.MinX) < 0.0001f &&
                   math.abs(w.MaxX - ww.MaxX) < 0.0001f &&
                   math.abs(w.MinY - ww.MinY) < 0.0001f &&
                   math.abs(w.MaxY - ww.MaxY) < 0.0001f;
        }

        string BuildCityDetailIfNeeded(EntityManager em)
        {
            var cityId = MapViewport.SelectedCityId;
            if (cityId < 0)
                return "";
            if (!CityObservation.TryCapture(em, cityId, out var snap))
                return "";
            return snap.DetailBlock;
        }

        string BuildProvinceDetailIfNeeded(EntityManager em)
        {
            var state = MapViewport.State;
            if (state.Level != MapObservationLevel.Province || state.TargetProvinceId < 0)
                return "";

            var name = ProvinceCoordinates.NameOf(state.TargetProvinceId);
            if (!ProvinceObservation.TryCapture(em, state.TargetProvinceId, name, out var snap))
                return "";
            return snap.DetailBlock;
        }

        string BuildCountryDetailIfNeeded(EntityManager em)
        {
            var state = MapViewport.State;
            if (state.Level != MapObservationLevel.Country || state.TargetCountryId < 0)
                return "";
            if (!CountryObservation.TryCapture(em, state.TargetCountryId, out var snap))
                return "";
            return snap.DetailBlock;
        }

        void EnsureLayerAssets()
        {
            if (_layerAssetsReady)
                return;
            _palettes = MapLayerRenderer.LoadPalettes();
            _domains = MapLayerRenderer.GetFixedDomains(_palettes);
            _colors = CountryColors.Load();
            _layerAssetsReady = true;
        }

        static MapLayerRenderer.LayerKind ToLayerKind(DisplayLayer layer)
        {
            switch (layer)
            {
                case DisplayLayer.Satisfaction: return MapLayerRenderer.LayerKind.Satisfaction;
                case DisplayLayer.Population: return MapLayerRenderer.LayerKind.Population;
                case DisplayLayer.Army: return MapLayerRenderer.LayerKind.Army;
                case DisplayLayer.TradeNode: return MapLayerRenderer.LayerKind.TradeNode;
                default: return MapLayerRenderer.LayerKind.Satisfaction;
            }
        }

        void HandleLayerHotkeys()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha1) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad1))
                _layer = DisplayLayer.Political;
            else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha2) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad2))
                _layer = DisplayLayer.Satisfaction;
            else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha3) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad3))
                _layer = DisplayLayer.Population;
            else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha4) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad4))
                _layer = DisplayLayer.Army;
            else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha5) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad5))
                _layer = DisplayLayer.TradeNode;
        }

        void HandleViewportHotkeys()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.Backspace) ||
                UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                TryZoomOut();
            }
        }

        public static bool TryZoomOut()
        {
            if (!MapViewport.IsInitialized || _instanceForGeo == null)
                return false;

            var em = _instanceForGeo.EntityManager;
            var state = MapViewport.State;
            MapWindow parent;
            if (state.Level == MapObservationLevel.Province &&
                state.TargetCountry != Entity.Null)
            {
                var ids = CollectProvinceIdsForCountry(em, state.TargetCountry);
                parent = MapViewport.BuildCountryWindow(_instanceForGeo._worldGeometry, ids);
            }
            else
            {
                parent = MapViewport.WorldWindow;
            }

            var ok = MapViewport.ZoomOut(parent);
            if (ok)
                RequestRefresh();
            return ok;
        }

        /// <summary>
        /// Clic sur la carte : priorité ville (Country/Province/World si marqueur),
        /// sinon Monde→Pays, Pays→Province (via ProvinceAt).
        /// </summary>
        public static bool TryClickAtTexturePixel(int px, int py)
        {
            if (_instanceForGeo == null)
                return false;
            var geo = ActiveGeometry ?? _instanceForGeo._activeGeometry;
            if (geo == null)
                return false;

            var em = _instanceForGeo.EntityManager;
            var level = MapViewport.State.Level;

            // Hit-test villes d'abord (marqueurs du dernier Compose).
            if ((level == MapObservationLevel.World ||
                 level == MapObservationLevel.Country ||
                 level == MapObservationLevel.Province) &&
                CityMarkerComposer.TryHit(px, py, out var cityId))
            {
                MapViewport.SelectCity(cityId);
                RequestRefresh();
                return true;
            }

            if (!MapClickPicker.TryPickProvinceId(geo, px, py, out var viewId))
                return false;

            // v1_094 — la géométrie rend des VUES (cell_id en pilote, ProvinceId en
            // hérité) ; l'ECS ne connaît que des ProvinceId. Traduire ICI, une fois,
            // plutôt que dans chaque requête : sans ça, un clic en mode pilote
            // cherchait la province 1164 et ne sélectionnait rien.
            var provinceId = ToSimulationProvinceId(viewId);
            if (provinceId <= 0)
                return false;

            if (level == MapObservationLevel.World)
                return TrySelectCountryOwningProvince(em, provinceId);
            if (level == MapObservationLevel.Country ||
                level == MapObservationLevel.Province)
                return TrySelectProvinceById(em, provinceId, viewId);
            return false;
        }

        /// <summary>
        /// v1_094 — identifiant de vue (cell_id ou ProvinceId) → ProvinceId simulé.
        /// Renvoie -1 si la vue n'est rattachée à aucune province.
        /// </summary>
        public static int ToSimulationProvinceId(int viewId)
            => PilotMapProvider.SimulationProvinceIdOfView(viewId);

        /// <summary>
        /// v1_095 — POINT D'ENTRÉE DE MESURE. Appelle exactement la fonction que la
        /// boucle d'affichage appelle : ce qui est mesuré ici est ce que le jeu paie.
        /// <paramref name="geometry"/> tient lieu de géométrie active hors boucle.
        /// </summary>
        public static RenderTexture RenderGpuBackgroundForMeasure(
            EntityManager em, MapSnapshotExporter.MapGeometry geometry)
        {
            ActiveGeometry = geometry;
            return TryRenderGpuBackground(em);
        }

        /// <summary>v1_095 — vrai si la dernière image immédiate est venue du GPU.</summary>
        public static bool GpuBackgroundUsedThisFrame { get; private set; }

        /// <summary>v1_095 — nombre de fonds GPU produits (diagnostic).</summary>
        public static int GpuBackgroundFrames { get; private set; }

        /// <summary>v1_095 — coût de la dernière image GPU, relecture comprise.</summary>
        public static double LastGpuBackgroundMilliseconds { get; private set; }

        /// <summary>Empreinte du monde ayant servi à bâtir la palette GPU courante.</summary>
        static int _gpuPaletteFingerprint = int.MinValue;

        /// <summary>
        /// v1_095 — fond de carte par le GPU pour la fenêtre courante.
        ///
        /// LA PALETTE N'EST RECONSTRUITE QUE SI LE MONDE A CHANGÉ. C'est tout
        /// l'intérêt de la bascule : déplacer la carte ne touche plus au monde,
        /// donc ne coûte qu'un Blit. Reconstruire la palette à chaque image
        /// rendrait le GPU aussi lent que le CPU et le ferait silencieusement.
        ///
        /// Renvoie la RenderTexture telle quelle — AUCUNE relecture CPU. La relecture
        /// coûtait 18 ms par image en 960×720 et faisait à elle seule sortir le
        /// rendu du budget de 60 images/s, alors que le Blit en coûte 0,03.
        /// Null si le GPU n'est pas utilisable ici (mode hérité, shader absent,
        /// matériel refusant le shader).
        /// </summary>
        static RenderTexture TryRenderGpuBackground(EntityManager em)
        {
            GpuBackgroundUsedThisFrame = false;

            // Le GPU lit les textures du pipeline pilote : hors mode pilote, il n'a
            // aucune géométrie à lire. Ce n'est pas une limite du shader, c'est que
            // la carte héritée n'a pas de texture d'identifiants.
            if (!PilotMapProvider.Enabled || !MapGpuRenderer.IsAvailable)
                return null;
            var geo = ActiveGeometry ?? _instanceForGeo?._activeGeometry;
            if (geo?.ViewsSkeleton == null)
                return null;

            var sw = Stopwatch.StartNew();
            var colors = CountryColors.Load();

            var fp = ComputeOwnerChangeFingerprint(em);
            if (fp != _gpuPaletteFingerprint || MapGpuRenderer.PaletteWidth == 0)
            {
                var views = MapSnapshotExporter.BuildViewsForRender(
                    em, geo.ViewsSkeleton, colors);
                if (!MapGpuRenderer.BuildPalette(views, out var err))
                {
                    UnityEngine.Debug.LogWarning("MapGpuRenderer: palette refusée — " + err);
                    return null;
                }

                _gpuPaletteFingerprint = fp;
            }

            var w = MapViewport.State.Window;
            var lod = PilotMapProvider.LodForObservation(MapViewport.State.Level);
            var hover = MapViewport.HoverProvinceId;
            var selected = MapViewport.State.Level == MapObservationLevel.Province
                ? MapViewport.State.TargetProvinceId
                : -1;

            // Survol et sélection sont exprimés en identifiants de VUE côté shader.
            // TargetProvinceId est un ProvinceId simulé depuis v1_094 : le traduire
            // ici, sinon la surbrillance porterait sur une cellule qui n'existe pas.
            var selectedView = -1;
            if (selected > 0 && PilotMapProvider.Enabled)
            {
                var cells = PilotMapProvider.CellsOfProvince(selected);
                if (cells.Count > 0)
                    selectedView = cells[0];
            }

            var rt = MapGpuRenderer.Render(
                MapSnapshotExporter.Width, MapSnapshotExporter.Height,
                w.MinX, w.MaxX, w.MinY, w.MaxY,
                lod, colors.Sea, hover, selectedView);
            sw.Stop();
            if (rt == null)
                return null;

            LastGpuBackgroundMilliseconds = sw.Elapsed.TotalMilliseconds;
            GpuBackgroundUsedThisFrame = true;
            GpuBackgroundFrames++;
            return rt;
        }

        /// <summary>Survol : met à jour le label de présentation (pas d'écriture ECS).</summary>
        public static void UpdateHoverAtTexturePixel(int px, int py)
        {
            var geo = ActiveGeometry ?? _instanceForGeo?._activeGeometry;
            if (geo == null)
            {
                MapViewport.ClearHover();
                return;
            }

            if (!MapClickPicker.TryPickProvinceName(geo, px, py, out var id, out var name))
            {
                MapViewport.ClearHover();
                return;
            }

            var label = string.IsNullOrEmpty(name)
                ? ("P" + id.ToString(CultureInfo.InvariantCulture))
                : name;
            if (MapViewport.State.Level == MapObservationLevel.World &&
                _instanceForGeo != null)
            {
                // id est un identifiant de VUE ; l'ECS veut un ProvinceId (v1_094).
                var tag = ResolveOwnerTag(
                    _instanceForGeo.EntityManager, ToSimulationProvinceId(id));
                if (!string.IsNullOrEmpty(tag))
                    label = tag + " / " + label;
            }

            MapViewport.SetHover(id, label);
        }

        public static bool TryPanByTextureDelta(float dpx, float dpy)
        {
            if (!MapViewport.IsInitialized)
                return false;
            var geo = ActiveGeometry;
            if (geo == null || geo.Width < 1 || geo.Height < 1)
                return false;
            var w = MapViewport.State.Window;
            var dx = -dpx / geo.Width * w.Width;
            var dy = -dpy / geo.Height * w.Height;
            // Texture y=0 bas ; delta UI y-down → inverser dy monde.
            dy = -dy;
            var ok = MapViewport.Pan(dx, dy);
            if (ok)
                RequestRefresh();
            return ok;
        }

        public static bool TryWheelZoomAtTexturePixel(int px, int py, float scrollY)
        {
            if (!MapViewport.IsInitialized || math.abs(scrollY) < 0.01f)
                return false;
            var geo = ActiveGeometry;
            if (geo == null)
                return false;
            MapViewportNavigation.TexturePixelToWorld(
                MapViewport.State.Window, geo.Width, geo.Height, px, py,
                out var wx, out var wy);
            // scrollY > 0 = zoom avant (réduire fenêtre)
            var factor = scrollY > 0f ? 0.85f : 1.18f;
            var ok = MapViewport.ZoomAt(wx, wy, factor);
            if (ok)
                RequestRefresh();
            return ok;
        }

        static bool TrySelectCountryOwningProvince(EntityManager em, int provinceId)
        {
            Entity owner = Entity.Null;
            var countryId = -1;
            string tag = null;
            using (var q = em.CreateEntityQuery(
                       ComponentType.ReadOnly<ProvinceData>(),
                       ComponentType.ReadOnly<ProvinceOwnership>()))
            using (var pdata = q.ToComponentDataArray<ProvinceData>(Unity.Collections.Allocator.Temp))
            using (var owns = q.ToComponentDataArray<ProvinceOwnership>(Unity.Collections.Allocator.Temp))
            {
                for (var i = 0; i < pdata.Length; i++)
                {
                    if (pdata[i].ProvinceId != provinceId)
                        continue;
                    owner = owns[i].Owner;
                    break;
                }
            }

            if (owner == Entity.Null)
                return false;
            if (em.HasComponent<CountryData>(owner))
            {
                var cd = em.GetComponentData<CountryData>(owner);
                countryId = cd.CountryId;
                tag = cd.Tag.ToString();
            }

            if (string.IsNullOrEmpty(tag))
                return false;
            return TrySelectCountryByTag(em, tag) ||
                   SelectCountryDirect(em, owner, countryId);
        }

        static bool SelectCountryDirect(EntityManager em, Entity country, int countryId)
        {
            MapSnapshotExporter.MapGeometry worldGeo;
            if (_instanceForGeo?._worldGeometry != null)
                worldGeo = _instanceForGeo._worldGeometry;
            else
            {
                worldGeo = MapGeometryCache.GetOrBuild(
                    MapSnapshotExporter.Width, MapSnapshotExporter.Height, null, out _);
                if (worldGeo == null)
                    return false;
            }

            var ids = CollectProvinceIdsForCountry(em, country);
            var window = MapViewport.BuildCountryWindow(worldGeo, ids);
            MapViewport.EnsureWorldWindow(worldGeo);
            var ok = MapViewport.SelectCountry(country, countryId, window);
            if (ok)
                RequestRefresh();
            return ok;
        }

        static string ResolveOwnerTag(EntityManager em, int provinceId)
        {
            using var q = em.CreateEntityQuery(
                ComponentType.ReadOnly<ProvinceData>(),
                ComponentType.ReadOnly<ProvinceOwnership>());
            using var pdata = q.ToComponentDataArray<ProvinceData>(Unity.Collections.Allocator.Temp);
            using var owns = q.ToComponentDataArray<ProvinceOwnership>(Unity.Collections.Allocator.Temp);
            for (var i = 0; i < pdata.Length; i++)
            {
                if (pdata[i].ProvinceId != provinceId)
                    continue;
                if (owns[i].Owner != Entity.Null && em.HasComponent<CountryData>(owns[i].Owner))
                    return em.GetComponentData<CountryData>(owns[i].Owner).Tag.ToString();
                return "";
            }

            return "";
        }

        /// <summary>
        /// Bandeau joueur : le survol est déjà présenté nommément via le Label
        /// dédié <see cref="InGameHud.HoverLabel"/> (RefreshHoverLabel) — sans le
        /// jeton technique "HOVER". Cet ajout au fil de métriques est un doublon
        /// diagnostique ; masqué hors <see cref="InGameHud.ShowDebugIds"/> (v1_004
        /// polish : fuite de debug fermée, brief 004-polish-visuel).
        /// </summary>
        static string AppendHover(string metricsLine)
        {
            if (!InGameHud.ShowDebugIds)
                return metricsLine ?? "";
            var hover = MapViewport.HoverLabel;
            if (string.IsNullOrEmpty(hover))
                return metricsLine ?? "";
            return (metricsLine ?? "") + "  HOVER " + hover;
        }

        static void ApplyHoverHighlight(
            Color32[] pixels, MapSnapshotExporter.MapGeometry geo, int hoverProvinceId)
        {
            if (pixels == null || geo?.ProvinceAt == null || geo.ViewsSkeleton == null)
                return;
            if (hoverProvinceId < 0)
                return;

            var viewIndex = -1;
            for (var i = 0; i < geo.ViewsSkeleton.Count; i++)
            {
                if (geo.ViewsSkeleton[i].Id != hoverProvinceId)
                    continue;
                viewIndex = i;
                break;
            }

            if (viewIndex < 0)
                return;

            var n = math.min(pixels.Length, geo.ProvinceAt.Length);
            for (var i = 0; i < n; i++)
            {
                if (geo.ProvinceAt[i] != viewIndex)
                    continue;
                var c = pixels[i];
                pixels[i] = new Color32(
                    (byte)math.min(255, c.r + 40),
                    (byte)math.min(255, c.g + 40),
                    (byte)math.min(255, c.b + 28),
                    c.a);
            }
        }

        /// <summary>
        /// Recadrage immédiat (progressive refinement) — pas le rendu final.
        /// </summary>
        static Color32[] CropScalePreview(
            Color32[] src, int srcW, int srcH,
            MapWindow srcWindow, MapWindow dstWindow,
            int dstW, int dstH)
        {
            if (src == null || srcW < 1 || srcH < 1 || dstW < 1 || dstH < 1)
                return null;
            if (srcWindow.Width < 0.0001f || srcWindow.Height < 0.0001f)
                return null;

            var dst = new Color32[dstW * dstH];
            for (var y = 0; y < dstH; y++)
            {
                var wy = dstWindow.MinY + (y + 0.5f) / dstH * dstWindow.Height;
                var v = (wy - srcWindow.MinY) / srcWindow.Height;
                var sy = (int)(v * srcH);
                if (sy < 0) sy = 0;
                if (sy >= srcH) sy = srcH - 1;
                for (var x = 0; x < dstW; x++)
                {
                    var wx = dstWindow.MinX + (x + 0.5f) / dstW * dstWindow.Width;
                    var u = (wx - srcWindow.MinX) / srcWindow.Width;
                    var sx = (int)(u * srcW);
                    if (sx < 0) sx = 0;
                    if (sx >= srcW) sx = srcW - 1;
                    dst[y * dstW + x] = src[sy * srcW + sx];
                }
            }

            return dst;
        }

        public static bool TrySelectCountryByTag(EntityManager em, string tag)
        {
            if (string.IsNullOrEmpty(tag) || _instanceForGeo?._worldGeometry == null)
            {
                // Tests / batch sans DisplaySystem : construire la géométrie monde à la demande.
                var worldGeo = MapSnapshotExporter.BuildMapGeometry(
                    MapSnapshotExporter.Width, MapSnapshotExporter.Height);
                if (worldGeo == null)
                    return false;
                return TrySelectCountryByTagWithGeo(em, tag, worldGeo);
            }

            return TrySelectCountryByTagWithGeo(em, tag, _instanceForGeo._worldGeometry);
        }

        static bool TrySelectCountryByTagWithGeo(
            EntityManager em, string tag, MapSnapshotExporter.MapGeometry worldGeo)
        {
            Entity country = Entity.Null;
            var countryId = -1;
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<CountryData>()))
            using (var entities = q.ToEntityArray(Unity.Collections.Allocator.Temp))
            using (var data = q.ToComponentDataArray<CountryData>(Unity.Collections.Allocator.Temp))
            {
                for (var i = 0; i < data.Length; i++)
                {
                    if (!string.Equals(
                            data[i].Tag.ToString(), tag, System.StringComparison.Ordinal))
                        continue;
                    country = entities[i];
                    countryId = data[i].CountryId;
                    break;
                }
            }

            if (country == Entity.Null || countryId < 0)
                return false;

            var ids = CollectProvinceIdsForCountry(em, country);
            var window = MapViewport.BuildCountryWindow(worldGeo, ids);
            MapViewport.EnsureWorldWindow(worldGeo);
            var ok = MapViewport.SelectCountry(country, countryId, window);
            if (ok)
                RequestRefresh();
            return ok;
        }

        public static bool TrySelectProvinceById(EntityManager em, int provinceId)
            => TrySelectProvinceById(em, provinceId, -1);

        /// <summary>
        /// v1_094 — <paramref name="viewId"/> est l'identifiant de la vue cliquée
        /// (cell_id en pilote). Il ne sert qu'à cadrer la fenêtre : la géométrie
        /// s'indexe par vue, l'ECS par province. Négatif ⇒ cadrer sur la première
        /// cellule rattachée, faute de clic.
        /// </summary>
        public static bool TrySelectProvinceById(EntityManager em, int provinceId, int viewId)
        {
            MapSnapshotExporter.MapGeometry worldGeo;
            if (_instanceForGeo?._worldGeometry != null)
                worldGeo = _instanceForGeo._worldGeometry;
            else
            {
                worldGeo = MapSnapshotExporter.BuildMapGeometry(
                    MapSnapshotExporter.Width, MapSnapshotExporter.Height);
                if (worldGeo == null)
                    return false;
            }

            Entity province = Entity.Null;
            Entity owner = Entity.Null;
            var countryId = -1;
            using (var q = em.CreateEntityQuery(
                       ComponentType.ReadOnly<ProvinceData>(),
                       ComponentType.ReadOnly<ProvinceOwnership>()))
            using (var entities = q.ToEntityArray(Unity.Collections.Allocator.Temp))
            using (var pdata = q.ToComponentDataArray<ProvinceData>(Unity.Collections.Allocator.Temp))
            using (var owns = q.ToComponentDataArray<ProvinceOwnership>(Unity.Collections.Allocator.Temp))
            {
                for (var i = 0; i < pdata.Length; i++)
                {
                    if (pdata[i].ProvinceId != provinceId)
                        continue;
                    province = entities[i];
                    owner = owns[i].Owner;
                    break;
                }
            }

            if (province == Entity.Null)
                return false;

            if (owner != Entity.Null && em.HasComponent<CountryData>(owner))
                countryId = em.GetComponentData<CountryData>(owner).CountryId;

            MapViewport.EnsureWorldWindow(worldGeo);
            var windowId = viewId;
            if (windowId <= 0)
            {
                windowId = provinceId;
                if (PilotMapProvider.Enabled)
                {
                    var cells = PilotMapProvider.CellsOfProvince(provinceId);
                    if (cells.Count > 0)
                        windowId = cells[0];
                }
            }

            var window = MapViewport.BuildProvinceWindow(worldGeo, windowId);
            var ok = MapViewport.SelectProvince(owner, countryId, province, provinceId, window);
            if (ok)
                RequestRefresh();
            return ok;
        }

        static HashSet<int> CollectProvinceIdsForCountry(EntityManager em, Entity country)
        {
            var set = new HashSet<int>();
            if (country == Entity.Null)
                return set;
            using var q = em.CreateEntityQuery(
                ComponentType.ReadOnly<ProvinceData>(),
                ComponentType.ReadOnly<ProvinceOwnership>());
            using var pdata = q.ToComponentDataArray<ProvinceData>(Unity.Collections.Allocator.Temp);
            using var owns = q.ToComponentDataArray<ProvinceOwnership>(Unity.Collections.Allocator.Temp);
            for (var i = 0; i < pdata.Length; i++)
            {
                if (owns[i].Owner != country)
                    continue;

                // v1_094 — le résultat sert à cadrer une fenêtre depuis la GÉOMÉTRIE,
                // qui s'indexe par vue. En pilote, une province vaut N cellules :
                // rendre le ProvinceId brut cadrerait sur un ensemble vide.
                if (PilotMapProvider.Enabled)
                {
                    var cells = PilotMapProvider.CellsOfProvince(pdata[i].ProvinceId);
                    for (var c = 0; c < cells.Count; c++)
                        set.Add(cells[c]);
                }
                else
                {
                    set.Add(pdata[i].ProvinceId);
                }
            }

            return set;
        }

        void HandlePaceHotkeys()
        {
            if (!SystemAPI.HasSingleton<WorldState>())
                return;

            var wsRw = SystemAPI.GetSingletonRW<WorldState>();
            ref var ws = ref wsRw.ValueRW;

            if (UnityEngine.Input.GetKeyDown(KeyCode.Space))
                ws.IsPaused = !ws.IsPaused;

            if (UnityEngine.Input.GetKeyDown(KeyCode.LeftBracket)
                || UnityEngine.Input.GetKeyDown(KeyCode.KeypadMinus)
                || UnityEngine.Input.GetKeyDown(KeyCode.Minus))
            {
                ws.SimulationSpeed = StepSpeed(ws.SimulationSpeed, -1);
            }
            else if (UnityEngine.Input.GetKeyDown(KeyCode.RightBracket)
                     || UnityEngine.Input.GetKeyDown(KeyCode.KeypadPlus)
                     || UnityEngine.Input.GetKeyDown(KeyCode.Equals))
            {
                ws.SimulationSpeed = StepSpeed(ws.SimulationSpeed, +1);
            }

            var hud = InGameHud.Instance;
            if (hud != null)
                hud.RefreshInfoBar(_lastMetricsLine);
        }

        public static readonly float[] SpeedSteps = { 0.5f, 1f, 2f, 4f, 8f };

        public static float StepSpeed(float current, int direction)
        {
            var idx = 1;
            var bestDist = float.MaxValue;
            for (var i = 0; i < SpeedSteps.Length; i++)
            {
                var d = math.abs(SpeedSteps[i] - current);
                if (d < bestDist)
                {
                    bestDist = d;
                    idx = i;
                }
            }

            idx = math.clamp(idx + direction, 0, SpeedSteps.Length - 1);
            return SpeedSteps[idx];
        }

        public static int NearestSpeedStepIndex(float current)
        {
            var idx = 1;
            var bestDist = float.MaxValue;
            for (var i = 0; i < SpeedSteps.Length; i++)
            {
                var d = math.abs(SpeedSteps[i] - current);
                if (d < bestDist)
                {
                    bestDist = d;
                    idx = i;
                }
            }
            return idx;
        }

        static int ComputeOwnerChangeFingerprint(EntityManager em)
        {
            var maxTick = 0;
            var count = 0;
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<ProvinceOwnership>());
            using var owns = q.ToComponentDataArray<ProvinceOwnership>(Unity.Collections.Allocator.Temp);
            for (var i = 0; i < owns.Length; i++)
            {
                count++;
                if (owns[i].OwnerChangedTick > maxTick)
                    maxTick = owns[i].OwnerChangedTick;
            }

            unchecked
            {
                return (maxTick * 397) ^ count;
            }
        }

        /// <summary>
        /// Overlay interactif map-only : sprites / villes / survol — aucun panneau bitmap.
        /// </summary>
        static void ComposeInteractiveMapOnly(
            Color32[] pixels,
            MapSnapshotExporter.MapGeometry geo,
            EntityManager em,
            MapObservationLevel level,
            int filterProv,
            int filterCountry,
            int hoverProv,
            bool thematicLayer = false)
        {
            MapSpriteComposer.Compose(pixels, geo, em, level, thematicLayer: thematicLayer);
            CityMarkerComposer.Compose(
                pixels, geo, em, level, filterProv, filterCountry);
            ApplyHoverHighlight(pixels, geo, hoverProv);
        }

        /// <summary>
        /// Bandeau joueur : date + métriques FR. TICK / C# / P# uniquement si
        /// <see cref="InGameHud.ShowDebugIds"/>.
        /// </summary>
        static string FormatPanelLine(
            int tick, int year, in WorldMetrics.Snapshot s, MapViewportState vp)
        {
            var sb = new StringBuilder(192);
            sb.Append("AN ").Append(year.ToString(CultureInfo.InvariantCulture));
            if (InGameHud.ShowDebugIds)
                sb.Append("  TICK ").Append(tick.ToString(CultureInfo.InvariantCulture));

            sb.Append("  Trésor ").Append(WorldMetrics.Fmt1(s.TotalTreasury));
            sb.Append("  Dette ").Append(WorldMetrics.Fmt1(s.TotalDebt));
            sb.Append("  Armée ").Append(WorldMetrics.Fmt0(s.WorldArmyStr));
            sb.Append("  Population ").Append(s.Population.ToString(CultureInfo.InvariantCulture));
            sb.Append("  Guerres ").Append(s.ActiveWars.ToString(CultureInfo.InvariantCulture));

            sb.Append("  ZOOM ").Append(FormatViewLevelFr(vp.Level));
            if (InGameHud.ShowDebugIds)
            {
                if (vp.TargetCountryId >= 0)
                    sb.Append(" C").Append(vp.TargetCountryId.ToString(CultureInfo.InvariantCulture));
                if (vp.TargetProvinceId >= 0)
                    sb.Append(" P").Append(vp.TargetProvinceId.ToString(CultureInfo.InvariantCulture));
            }

            return sb.ToString();
        }

        static string FormatViewLevelFr(MapObservationLevel level)
        {
            switch (level)
            {
                case MapObservationLevel.World: return "Monde";
                case MapObservationLevel.Country: return "Pays";
                case MapObservationLevel.Province: return "Province";
                case MapObservationLevel.City: return "Ville";
                default: return level.ToString();
            }
        }

        /// <summary>
        /// Panneau bitmap d'info (exports diagnostiques uniquement — plus sur le chemin interactif).
        /// </summary>
        public static void DrawDiagnosticInfoPanel(Color32[] pixels, int width, int height, string line)
        {
            if (pixels == null || string.IsNullOrEmpty(line))
                return;

            const int pad = 8;
            var panelH = MapSnapshotExporter.BitmapGlyphHeight + pad * 2;
            var bg = new Color32(0x12, 0x14, 0x18, 230);
            var fg = new Color32(0xf0, 0xf0, 0xf0, 255);
            var halo = new Color32(0x08, 0x08, 0x08, 255);

            var y0 = height - panelH;
            if (y0 < 0) y0 = 0;
            for (var y = y0; y < height; y++)
            {
                var row = y * width;
                for (var x = 0; x < width; x++)
                    pixels[row + x] = bg;
            }

            var textY = height - pad - MapSnapshotExporter.BitmapGlyphHeight;
            if (textY < 0) textY = 0;
            MapSnapshotExporter.DrawBitmapText(pixels, line, pad, textY, fg, halo);
        }

        protected override void OnDestroy()
        {
            if (_instanceForGeo == this)
                _instanceForGeo = null;
            _worldGeometry = null;
            _activeGeometry = null;
            ActiveGeometry = null;
        }
    }
}
