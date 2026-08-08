using Unity.Entities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using VictoriaGame.Core;
using VictoriaGame.Politics;
using VictoriaGame.World;

namespace VictoriaGame.Presentation
{
    /// <summary>
    /// Harness opt-in :
    /// - --ui-capture-dir &lt;chemin&gt; : séquence éditoriale ui_003 (1920×1080)
    /// - --ui-responsive-dir &lt;chemin&gt; : matrice v1_055 (4 rés × pays/province = 8 PNG)
    /// N'altère rien si aucun argument n'est présent.
    /// </summary>
    public sealed class UiStandaloneCaptureHarness : MonoBehaviour
    {
        public const string ArgCaptureDir = "--ui-capture-dir";
        public const string ArgResponsiveDir = "--ui-responsive-dir";
        public const string ArgResponsiveRes = "--ui-responsive-res";
        /// <summary>
        /// Mode debug explicite (brief 004-polish-visuel) : présence seule suffit
        /// à activer <see cref="InGameHud.ShowDebugIds"/> pour cette capture —
        /// jamais activé par défaut (voir <see cref="HasFlag"/>).
        /// </summary>
        public const string ArgDebugIds = "--debug-ids";
        const int WarmupFrames = 90;
        const int SettleFrames = 20;

        static readonly string[] CaptureFiles =
        {
            "01_world_neutral.png",
            "01_world_neutral_b.png",
            "02_country_selected.png",
            "03_province_selected.png",
            "04_pause_active.png",
            "05_tax_min.png",
            "06_tax_max.png",
            // brief 004-polish-visuel : preuve du bandeau en survol (jeton HOVER).
            "07_hover_debug_leak.png"
        };

        static readonly Vector2Int[] ResponsiveResolutions =
        {
            new Vector2Int(1280, 720),
            new Vector2Int(1920, 1080),
            new Vector2Int(2560, 1440),
            new Vector2Int(3440, 1440)
        };

        string _captureDir;
        string _visualLogPath;
        Vector2Int? _responsiveResFilter;

        /// <summary>
        /// brief 005-refonte-visuelle-carte : séquence zoom (Success Condition 3),
        /// traits de bordure à 3 niveaux (Success Condition 4), tentative chemin GPU
        /// (Success Condition 1).
        /// </summary>
        public const string ArgV005Dir = "--v005-dir";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void BootIfRequested()
        {
            var responsive = TryParseArg(ArgResponsiveDir, out var responsiveDir);
            var editorial = TryParseArg(ArgCaptureDir, out var captureDir);
            var v005 = TryParseArg(ArgV005Dir, out var v005Dir);
            if (!responsive && !editorial && !v005)
                return;

            var existing = UnityEngine.Object.FindFirstObjectByType<UiStandaloneCaptureHarness>();
            if (existing != null)
                return;

            var go = new GameObject(nameof(UiStandaloneCaptureHarness));
            DontDestroyOnLoad(go);
            var harness = go.AddComponent<UiStandaloneCaptureHarness>();
            if (responsive)
            {
                harness._captureDir = responsiveDir;
                if (TryParseResolutionArg(out var resFilter))
                    harness._responsiveResFilter = resFilter;
                var captureFull = Path.GetFullPath(responsiveDir);
                harness._visualLogPath = Path.GetFullPath(
                    Path.Combine(captureFull, "..", "v1_055_ui_responsive.log"));
                // Append si plusieurs lancements (une résolution par process).
                harness.StartCoroutine(harness.RunResponsiveSequence());
            }
            else if (editorial)
            {
                harness._captureDir = captureDir;
                var captureFull = Path.GetFullPath(captureDir);
                harness._visualLogPath = Path.GetFullPath(
                    Path.Combine(captureFull, "..", "ui_003_visual.log"));
                harness.StartCoroutine(harness.RunCaptureSequence());
            }
            else
            {
                harness._captureDir = v005Dir;
                var captureFull = Path.GetFullPath(v005Dir);
                harness._visualLogPath = Path.GetFullPath(
                    Path.Combine(captureFull, "..", "v005_zoom_gpu.log"));
                harness.StartCoroutine(harness.RunV005ZoomAndGpuSequence());
            }
        }

        public static bool TryParseResolutionArg(out Vector2Int res)
        {
            res = default;
            if (!TryParseArg(ArgResponsiveRes, out var raw) || string.IsNullOrWhiteSpace(raw))
                return false;
            var parts = raw.ToLowerInvariant().Split('x');
            if (parts.Length != 2)
                return false;
            if (!int.TryParse(parts[0], out var w) || !int.TryParse(parts[1], out var h))
                return false;
            if (w <= 0 || h <= 0)
                return false;
            res = new Vector2Int(w, h);
            return true;
        }

        public static bool TryParseCaptureDir(out string dir) =>
            TryParseArg(ArgCaptureDir, out dir);

        public static bool TryParseArg(string flag, out string dir)
        {
            dir = null;
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (!string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
                    continue;
                dir = args[i + 1];
                return !string.IsNullOrWhiteSpace(dir);
            }

            return false;
        }

        /// <summary>Drapeau sans valeur (présence seule) — utilisé par <see cref="ArgDebugIds"/>.</summary>
        public static bool HasFlag(string flag)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        IEnumerator RunResponsiveSequence()
        {
            var log = new StringBuilder(32768);
            var appendExisting = File.Exists(_visualLogPath);
            if (appendExisting)
                log.AppendLine($"=== v1_055 responsive append at={DateTime.UtcNow:o} ===");
            else
            {
                log.AppendLine("=== v1_055 standalone responsive proof ===");
                log.AppendLine($"started_at={DateTime.UtcNow:o}");
                log.AppendLine("verdict_policy=A_REVOIR_HUMAINEMENT");
                log.AppendLine($"source={GameViewCapture.SourceStandaloneFramebuffer}");
                log.AppendLine("composer=NONE");
                log.AppendLine($"capture_dir={_captureDir}");
                log.AppendLine("matrix=1280x720,1920x1080,2560x1440,3440x1440 × country,province");
                log.AppendLine($"debug_ids={HasFlag(ArgDebugIds)}");
            }

            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            Directory.CreateDirectory(_captureDir);

            for (var i = 0; i < 10; i++)
                yield return null;

            InGameHud.ForceProgrammaticFallback = false;
            // Mode debug explicite (brief 004-polish-visuel) : OFF par défaut,
            // activable uniquement via --debug-ids en ligne de commande.
            InGameHud.ShowDebugIds = HasFlag(ArgDebugIds);
            if (InGameHud.Instance == null)
            {
                var go = new GameObject("InGameHud");
                go.AddComponent<InGameHud>();
            }

            for (var f = 0; f < WarmupFrames; f++)
                yield return null;

            var hud = InGameHud.Instance;
            if (hud == null || !hud.UiReady)
            {
                log.AppendLine("FAIL InGameHud non prêt");
                FailAndQuit(log, 3, append: appendExisting);
                yield break;
            }

            var dotsWorld = Unity.Entities.World.DefaultGameObjectInjectionWorld;
            if (dotsWorld == null || !dotsWorld.IsCreated)
            {
                log.AppendLine("FAIL World DOTS absent");
                FailAndQuit(log, 4, append: appendExisting);
                yield break;
            }

            var em = dotsWorld.EntityManager;
            var waitPresent = 0;
            while (!MapDisplaySystem.HasPresentedFrame && waitPresent < 300)
            {
                waitPresent++;
                yield return null;
            }

            Entity countryEntity = Entity.Null;
            string countryDetail = "";
            string provinceDetail = "";
            var provinceId = -1;
            ResolveCountryAndProvince(em, out countryEntity, out countryDetail, out provinceId, out provinceDetail);
            log.AppendLine($"fixtures country_len={countryDetail.Length} province_id={provinceId} province_len={provinceDetail.Length}");

            var results = new List<GameViewCapture.CaptureResult>(8);
            var allOk = true;
            var expectedCaptures = 0;

            for (var r = 0; r < ResponsiveResolutions.Length; r++)
            {
                var res = ResponsiveResolutions[r];
                if (_responsiveResFilter.HasValue &&
                    (_responsiveResFilter.Value.x != res.x || _responsiveResFilter.Value.y != res.y))
                    continue;

                expectedCaptures += 2;
                yield return ApplyResolution(res.x, res.y, log);
                var screenExact = Screen.width == res.x && Screen.height == res.y;
                GameViewCapture.SetExpectedResolution(res.x, res.y);
                if (!screenExact)
                {
                    log.AppendLine(
                        $"WARN display_limited screen={Screen.width}x{Screen.height} " +
                        $"requested={res.x}x{res.y} — fallback standalone panel RT");
                    // Forcer la géométrie logique pour mesures + paint RT.
                    hud.ForceLayoutSizeForTests(res.x, res.y);
                    for (var i = 0; i < 8; i++)
                        yield return null;
                }

                // --- pays ---
                SetPace(em, isPaused: false, speed: 1f);
                MapViewport.ForceState(new MapViewportState
                {
                    Level = MapObservationLevel.Country,
                    TargetCountry = countryEntity,
                    TargetProvince = Entity.Null,
                    TargetCountryId = PlayerControl.DefaultControlledCountryId,
                    TargetProvinceId = -1,
                    Window = MapViewport.WorldWindow
                });
                MapDisplaySystem.RequestRefresh();
                for (var i = 0; i < SettleFrames; i++)
                    yield return null;
                hud.RefreshProvincePanel("");
                hud.RefreshCountryPanel(countryDetail);
                hud.RefreshInfoBar(MapDisplaySystem.LastMetricsLine);
                yield return WaitStableLayout(hud);
                hud.RefreshProvincePanel("");
                hud.RefreshCountryPanel(countryDetail);
                yield return null;
                var countryTag = $"{res.x}x{res.y}_country";
                var countryMetrics = HudLayoutProbe.Measure(hud);
                LogLayout(log, countryTag, countryMetrics, hud);
                if (!HudLayoutProbe.PassesResponsiveGates(countryMetrics, res.x))
                {
                    log.AppendLine($"FAIL layout_gates tag={countryTag} anomalies={countryMetrics.AnomalySummary}");
                    allOk = false;
                }

                yield return CaptureOne(
                    Path.Combine(_captureDir, countryTag + ".png"),
                    results, log, hud, countryTag,
                    preferPanelRt: !screenExact);

                // --- province ---
                MapViewport.ForceState(new MapViewportState
                {
                    Level = MapObservationLevel.Province,
                    TargetCountry = countryEntity,
                    TargetProvince = Entity.Null,
                    TargetCountryId = PlayerControl.DefaultControlledCountryId,
                    TargetProvinceId = provinceId,
                    Window = MapViewport.WorldWindow
                });
                MapDisplaySystem.RequestRefresh();
                for (var i = 0; i < SettleFrames; i++)
                    yield return null;
                hud.RefreshCountryPanel("");
                hud.RefreshProvincePanel(provinceDetail);
                hud.RefreshInfoBar(MapDisplaySystem.LastMetricsLine);
                if (!screenExact)
                {
                    hud.ForceLayoutSizeForTests(res.x, res.y);
                    for (var i = 0; i < 5; i++)
                        yield return null;
                }

                yield return WaitStableLayout(hud);
                hud.RefreshCountryPanel("");
                hud.RefreshProvincePanel(provinceDetail);
                yield return null;
                var provinceTag = $"{res.x}x{res.y}_province";
                var provinceMetrics = HudLayoutProbe.Measure(hud);
                LogLayout(log, provinceTag, provinceMetrics, hud);
                if (!HudLayoutProbe.PassesResponsiveGates(provinceMetrics, res.x))
                {
                    log.AppendLine($"FAIL layout_gates tag={provinceTag} anomalies={provinceMetrics.AnomalySummary}");
                    allOk = false;
                }

                yield return CaptureOne(
                    Path.Combine(_captureDir, provinceTag + ".png"),
                    results, log, hud, provinceTag,
                    preferPanelRt: !screenExact);
            }

            GameViewCapture.ResetExpectedResolution();

            if (expectedCaptures == 0)
            {
                log.AppendLine("FAIL aucune résolution sélectionnée");
                allOk = false;
            }

            var captureOk = results.Count == expectedCaptures;
            foreach (var cap in results)
            {
                if (!cap.Success || !cap.HasHudChrome || !cap.HasMapContent)
                    captureOk = false;
            }

            allOk = allOk && captureOk;
            log.AppendLine("=== SHA256 captures (batch) ===");
            for (var i = 0; i < results.Count; i++)
            {
                var cap = results[i];
                log.AppendLine(
                    $"cap[{i}] sha={cap.Sha256} dim={cap.Width}x{cap.Height} bytes={cap.ByteLength} " +
                    $"source={cap.Source} hud={cap.HasHudChrome} map={cap.HasMapContent} ok={cap.Success} err={cap.Error}");
            }

            var exitCode = allOk ? 0 : 5;
            if (!_responsiveResFilter.HasValue)
            {
                log.AppendLine("VERDICT=A_REVOIR_HUMAINEMENT");
                log.AppendLine(allOk
                    ? "status=PASS_TECHNIQUE_EN_ATTENTE_REVUE_ARTISTIQUE"
                    : "status=FAIL_CAPTURE_OU_LAYOUT");
            }
            else
            {
                log.AppendLine(allOk
                    ? $"status=PASS_RES_{_responsiveResFilter.Value.x}x{_responsiveResFilter.Value.y}"
                    : $"status=FAIL_RES_{_responsiveResFilter.Value.x}x{_responsiveResFilter.Value.y}");
            }

            WriteLog(log, append: appendExisting);
            Debug.Log($"UiStandaloneCaptureHarness responsive exit={exitCode} log={_visualLogPath}");
            Application.Quit(exitCode);
        }

        static void ResolveCountryAndProvince(
            EntityManager em,
            out Entity countryEntity,
            out string countryDetail,
            out int provinceId,
            out string provinceDetail)
        {
            countryEntity = Entity.Null;
            countryDetail = "";
            provinceId = -1;
            provinceDetail = "";

            if (CountryObservation.TryCapture(em, PlayerControl.DefaultControlledCountryId, out var countrySnap))
                countryDetail = countrySnap.DetailBlock;

            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<CountryData>()))
            using (var entities = q.ToEntityArray(Unity.Collections.Allocator.Temp))
            using (var data = q.ToComponentDataArray<CountryData>(Unity.Collections.Allocator.Temp))
            {
                for (var i = 0; i < data.Length; i++)
                {
                    if (data[i].CountryId != PlayerControl.DefaultControlledCountryId)
                        continue;
                    countryEntity = entities[i];
                    if (data[i].CapitalProvinceId >= 0)
                        provinceId = data[i].CapitalProvinceId;
                    break;
                }
            }

            if (provinceId >= 0 &&
                ProvinceObservation.TryCapture(em, provinceId, ProvinceCoordinates.NameOf(provinceId), out var provSnap))
            {
                provinceDetail = provSnap.DetailBlock;
                return;
            }

            using var pq = em.CreateEntityQuery(ComponentType.ReadOnly<ProvinceData>());
            using var pdata = pq.ToComponentDataArray<ProvinceData>(Unity.Collections.Allocator.Temp);
            for (var i = 0; i < pdata.Length; i++)
            {
                var id = pdata[i].ProvinceId;
                if (!ProvinceObservation.TryCapture(em, id, ProvinceCoordinates.NameOf(id), out var snap))
                    continue;
                provinceId = id;
                provinceDetail = snap.DetailBlock;
                break;
            }
        }

        static IEnumerator ApplyResolution(int width, int height, StringBuilder log)
        {
            // Si déjà à la bonne taille (lancement -screen-width/height), ne pas reforcer.
            if (Screen.width == width && Screen.height == height)
            {
                log.AppendLine($"resolution_ok already={width}x{height}");
                yield break;
            }

            Screen.SetResolution(width, height, FullScreenMode.ExclusiveFullScreen);
            for (var i = 0; i < 45; i++)
                yield return null;
            if (Screen.width == width && Screen.height == height)
            {
                log.AppendLine($"resolution_set mode=ExclusiveFullScreen screen={Screen.width}x{Screen.height}");
                yield break;
            }

            Screen.SetResolution(width, height, FullScreenMode.FullScreenWindow);
            for (var i = 0; i < 45; i++)
                yield return null;
            if (Screen.width == width && Screen.height == height)
            {
                log.AppendLine($"resolution_set mode=FullScreenWindow screen={Screen.width}x{Screen.height}");
                yield break;
            }

            Screen.SetResolution(width, height, FullScreenMode.Windowed);
            for (var i = 0; i < 45; i++)
                yield return null;
            log.AppendLine(
                $"resolution_set request={width}x{height} screen={Screen.width}x{Screen.height} mode=Windowed");
        }

        static IEnumerator WaitStableLayout(InGameHud hud)
        {
            var version = hud != null ? hud.LayoutVersion : -1;
            for (var i = 0; i < 12; i++)
                yield return null;
            // Attendre au moins un GeometryChanged après SetResolution / Populate.
            for (var i = 0; i < 30; i++)
            {
                if (hud != null && hud.LayoutVersion != version &&
                    hud.LastLayoutWidth > 1f && hud.LastLayoutHeight > 1f)
                    break;
                yield return null;
            }

            for (var i = 0; i < 5; i++)
                yield return null;
        }

        static void LogLayout(StringBuilder log, string tag, HudLayoutProbe.Metrics m, InGameHud hud)
        {
            log.AppendLine(
                $"layout tag={tag} root={m.RootWidth:0.#}x{m.RootHeight:0.#} " +
                $"panel={m.PanelWidth:0.#}x{m.PanelHeight:0.#} topbar_h={m.TopBarHeight:0.#} " +
                $"map_ratio={m.MapWidthRatio:0.###} compact={m.Compact} narrow={m.Narrow} ultrawide={m.Ultrawide} " +
                $"overlap={m.CriticalOverlap} offscreen={m.EssentialActionOffscreen} " +
                $"hit_small={m.HitTargetsTooSmall} trunc={m.TruncationSuspected} " +
                $"anomalies={m.AnomalySummary} layout_ver={hud?.LayoutVersion}");
            LogHudState(hud, log, tag);
        }

        IEnumerator RunCaptureSequence()
        {
            var log = new StringBuilder(16384);
            log.AppendLine("=== ui_003 standalone visual proof (editorial FR) ===");
            log.AppendLine($"started_at={DateTime.UtcNow:o}");
            log.AppendLine("verdict_policy=A_REVOIR_HUMAINEMENT");
            log.AppendLine($"source={GameViewCapture.SourceStandaloneFramebuffer}");
            log.AppendLine($"capture_target={GameViewCapture.Width}x{GameViewCapture.Height}");
            log.AppendLine("composer=NONE");
            log.AppendLine($"capture_dir={_captureDir}");
            log.AppendLine($"debug_ids={HasFlag(ArgDebugIds)}");

            GameViewCapture.ResetExpectedResolution();
            Screen.SetResolution(
                GameViewCapture.Width, GameViewCapture.Height, FullScreenMode.Windowed);
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;

            for (var i = 0; i < 10; i++)
                yield return null;

            if (Screen.width != GameViewCapture.Width || Screen.height != GameViewCapture.Height)
            {
                log.AppendLine(
                    $"FAIL resolution screen={Screen.width}x{Screen.height} " +
                    $"(attendu {GameViewCapture.Width}x{GameViewCapture.Height})");
                FailAndQuit(log, 2);
                yield break;
            }

            InGameHud.ForceProgrammaticFallback = false;
            // Mode debug explicite (brief 004-polish-visuel) : OFF par défaut,
            // activable uniquement via --debug-ids en ligne de commande.
            InGameHud.ShowDebugIds = HasFlag(ArgDebugIds);
            if (InGameHud.Instance == null)
            {
                var go = new GameObject("InGameHud");
                go.AddComponent<InGameHud>();
            }

            for (var f = 0; f < WarmupFrames; f++)
                yield return null;

            var hud = InGameHud.Instance;
            if (hud == null || !hud.UiReady)
            {
                log.AppendLine("FAIL InGameHud non prêt");
                FailAndQuit(log, 3);
                yield break;
            }

            var dotsWorld = Unity.Entities.World.DefaultGameObjectInjectionWorld;
            if (dotsWorld == null || !dotsWorld.IsCreated)
            {
                log.AppendLine("FAIL World DOTS absent");
                FailAndQuit(log, 4);
                yield break;
            }

            var em = dotsWorld.EntityManager;
            Directory.CreateDirectory(_captureDir);

            var waitPresent = 0;
            while (!MapDisplaySystem.HasPresentedFrame && waitPresent < 300)
            {
                waitPresent++;
                yield return null;
            }

            if (!MapDisplaySystem.HasPresentedFrame)
                log.AppendLine("WARN HasPresentedFrame encore false — poursuite");

            // brief 005-refonte-visuelle-carte, Success Condition 2 : capture de la fenêtre
            // caméra AU DÉMARRAGE, avant tout ForceState/pan/zoom — la seule fenêtre qui
            // compte pour ce Success Condition. Le ForceState(World(WorldWindow)) juste
            // après existe pour stabiliser LE RESTE de cette séquence (01_world_neutral et
            // tout ce qui suit sont un scénario délibérément recadré plein-monde, pas la
            // preuve du cadrage initial) — donc capturé AVANT, jamais réutilisé pour ce qui
            // suit.
            // Culture-invariante EXPLICITE + champs séparés (jamais "[a,b]" avec un ToString
            // par défaut qui, en build FR, rend la virgule ambiguë entre séparateur décimal
            // et séparateur de liste — c'est exactement ce qui a rendu ce log illisible lors
            // de la première passe de ce brief).
            var initialWindow = MapViewport.State.Window;
            var ic = System.Globalization.CultureInfo.InvariantCulture;
            log.AppendLine(
                "initial_window_at_boot" +
                " INIT_MINX=" + initialWindow.MinX.ToString("0.###", ic) +
                " INIT_MAXX=" + initialWindow.MaxX.ToString("0.###", ic) +
                " INIT_MINY=" + initialWindow.MinY.ToString("0.###", ic) +
                " INIT_MAXY=" + initialWindow.MaxY.ToString("0.###", ic) +
                " playable_provinces_count=" + MapDisplaySystem.LastPlayableProvinceCount +
                " WORLD_MINX=" + MapViewport.WorldWindow.MinX.ToString("0.###", ic) +
                " WORLD_MAXX=" + MapViewport.WorldWindow.MaxX.ToString("0.###", ic) +
                " WORLD_MINY=" + MapViewport.WorldWindow.MinY.ToString("0.###", ic) +
                " WORLD_MAXY=" + MapViewport.WorldWindow.MaxY.ToString("0.###", ic));
            var initialResults = new List<GameViewCapture.CaptureResult>(1);
            yield return CaptureOne(
                Path.Combine(_captureDir, "00_initial_framing.png"), initialResults, log, hud,
                "00_initial_framing");

            SetPace(em, isPaused: false, speed: 1f);
            MapViewport.ForceState(MapViewportState.World(MapViewport.WorldWindow));
            MapDisplaySystem.RequestRefresh();
            for (var i = 0; i < SettleFrames * 2; i++)
                yield return null;
            hud.RefreshProvincePanel("");
            hud.RefreshCountryPanel("");
            hud.RefreshInfoBar(MapDisplaySystem.LastMetricsLine);
            for (var i = 0; i < 10; i++)
                yield return null;

            var results = new List<GameViewCapture.CaptureResult>(CaptureFiles.Length);
            var allOk = true;

            LogPanelOverlap(log, hud, "01_world_neutral");
            yield return CaptureOne(Path.Combine(_captureDir, CaptureFiles[0]), results, log, hud, "01_world_neutral");
            yield return CaptureOne(Path.Combine(_captureDir, CaptureFiles[1]), results, log, hud, "01_world_neutral_b");

            if (results.Count >= 2)
            {
                if (string.Equals(results[0].Sha256, results[1].Sha256, StringComparison.Ordinal))
                    log.AppendLine($"determinism_match sha={results[0].Sha256}");
                else
                    log.AppendLine(
                        $"determinism_diff sha_a={results[0].Sha256} sha_b={results[1].Sha256} " +
                        "cause=possible GPU/UI Toolkit frame variance — NOT forged");
            }

            Entity countryEntity = Entity.Null;
            string countryDetail = "";
            if (CountryObservation.TryCapture(em, PlayerControl.DefaultControlledCountryId, out var countrySnap))
                countryDetail = countrySnap.DetailBlock;

            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<CountryData>()))
            using (var entities = q.ToEntityArray(Unity.Collections.Allocator.Temp))
            using (var data = q.ToComponentDataArray<CountryData>(Unity.Collections.Allocator.Temp))
            {
                for (var i = 0; i < data.Length; i++)
                {
                    if (data[i].CountryId != PlayerControl.DefaultControlledCountryId)
                        continue;
                    countryEntity = entities[i];
                    break;
                }
            }

            MapViewport.SelectCountry(countryEntity, PlayerControl.DefaultControlledCountryId, MapViewport.WorldWindow);
            MapDisplaySystem.RequestRefresh();
            for (var i = 0; i < SettleFrames; i++)
                yield return null;
            hud.RefreshProvincePanel("");
            hud.RefreshCountryPanel(countryDetail);
            hud.RefreshInfoBar(MapDisplaySystem.LastMetricsLine);
            yield return null;
            if (!AssertEditorial(hud, log, "02_country_selected", expectCountry: true, expectProvince: false))
                allOk = false;
            LogPanelOverlap(log, hud, "02_country_selected");
            yield return CaptureOne(Path.Combine(_captureDir, CaptureFiles[2]), results, log, hud, "02_country_selected");

            var provinceId = -1;
            var provinceDetail = "";
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<CountryData>()))
            using (var data = q.ToComponentDataArray<CountryData>(Unity.Collections.Allocator.Temp))
            {
                for (var i = 0; i < data.Length; i++)
                {
                    if (data[i].CountryId != PlayerControl.DefaultControlledCountryId)
                        continue;
                    if (data[i].CapitalProvinceId >= 0)
                        provinceId = data[i].CapitalProvinceId;
                    break;
                }
            }

            if (provinceId >= 0 &&
                ProvinceObservation.TryCapture(em, provinceId, ProvinceCoordinates.NameOf(provinceId), out var provSnap))
            {
                provinceDetail = provSnap.DetailBlock;
            }
            else
            {
                using var pq = em.CreateEntityQuery(ComponentType.ReadOnly<ProvinceData>());
                using var pdata = pq.ToComponentDataArray<ProvinceData>(Unity.Collections.Allocator.Temp);
                for (var i = 0; i < pdata.Length; i++)
                {
                    var id = pdata[i].ProvinceId;
                    if (!ProvinceObservation.TryCapture(em, id, ProvinceCoordinates.NameOf(id), out var snap))
                        continue;
                    provinceId = id;
                    provinceDetail = snap.DetailBlock;
                    break;
                }
            }

            // --- 07 : survol d'une province (brief 004-polish-visuel) ---
            // Scénario reproductible et fixe pour prouver/démentir la fuite de debug
            // "HOVER <nom>" dans le bandeau — reste au niveau Pays (comme 02), simule
            // MapViewport.SetHover directement (même état que UpdateHoverAtTexturePixel
            // écrirait, sans dépendre d'un pointeur réel).
            if (provinceId >= 0)
            {
                var hoverName = ProvinceCoordinates.NameOf(provinceId);
                MapViewport.SetHover(
                    provinceId, string.IsNullOrEmpty(hoverName) ? ("P" + provinceId) : hoverName);
                MapDisplaySystem.RequestRefresh();
                for (var i = 0; i < SettleFrames; i++)
                    yield return null;
                hud.RefreshInfoBar(MapDisplaySystem.LastMetricsLine);
                yield return null;
                yield return CaptureOne(
                    Path.Combine(_captureDir, CaptureFiles[7]), results, log, hud, "07_hover_debug_leak");
                MapViewport.ClearHover();
                MapDisplaySystem.RequestRefresh();
                for (var i = 0; i < 5; i++)
                    yield return null;
            }
            else
            {
                log.AppendLine("WARN 07_hover_debug_leak skipped — provinceId invalide");
            }

            MapViewport.SelectProvince(
                countryEntity, PlayerControl.DefaultControlledCountryId,
                Entity.Null, provinceId, MapViewport.WorldWindow);
            MapDisplaySystem.RequestRefresh();
            for (var i = 0; i < SettleFrames; i++)
                yield return null;
            hud.RefreshCountryPanel("");
            hud.RefreshProvincePanel(provinceDetail);
            hud.RefreshInfoBar(MapDisplaySystem.LastMetricsLine);
            yield return null;
            if (!AssertEditorial(hud, log, "03_province_selected", expectCountry: false, expectProvince: true))
                allOk = false;
            LogPanelOverlap(log, hud, "03_province_selected");
            log.AppendLine(
                "investir_status_default tag=03_province_selected text='" +
                (hud.InvestStatusLabel?.text ?? "(null)") + "'");
            yield return CaptureOne(Path.Combine(_captureDir, CaptureFiles[3]), results, log, hud, "03_province_selected");

            // brief 005-refonte-visuelle-carte, Success Condition 6b : même scénario
            // province, bascule --debug-ids EN COURS DE PROCESS (InGameHud.ShowDebugIds
            // est un champ static mutable — pas besoin d'un second lancement du player) —
            // preuve d'une vraie PORTE (jetons bruts réservés au debug), pas d'une simple
            // suppression.
            InGameHud.ShowDebugIds = true;
            hud.RefreshProvincePanel(provinceDetail);
            yield return null;
            log.AppendLine(
                "investir_status_debug tag=03_province_selected_debug text='" +
                (hud.InvestStatusLabel?.text ?? "(null)") + "'");
            var debugCaptureResults = new List<GameViewCapture.CaptureResult>(1);
            yield return CaptureOne(
                Path.Combine(_captureDir, "03_province_selected_debug.png"), debugCaptureResults, log, hud,
                "03_province_selected_debug");
            InGameHud.ShowDebugIds = HasFlag(ArgDebugIds);
            hud.RefreshProvincePanel(provinceDetail);
            yield return null;

            // brief 005-refonte-visuelle-carte, itération de correction du feedback
            // (Issue 5, SC6b) : le dénominateur investir_raw_token_* de l'itération
            // précédente ne portait que sur UNE province (Île-de-France, la capitale) --
            // la rubrique exige >= 2 provinces/scénarios distincts. Seconde sélection
            // réelle (n'importe quelle autre province que la capitale), rejouée pour les
            // deux builds défaut/debug -- le mécanisme de porte (InGameHud.ShowDebugIds)
            // est déjà prouvé, rien à réinventer, seulement un second scénario à ajouter.
            var secondProvinceId = -1;
            var secondProvinceDetail = "";
            using (var q2 = em.CreateEntityQuery(ComponentType.ReadOnly<ProvinceData>()))
            using (var pdata2 = q2.ToComponentDataArray<ProvinceData>(Unity.Collections.Allocator.Temp))
            {
                for (var i = 0; i < pdata2.Length; i++)
                {
                    var id = pdata2[i].ProvinceId;
                    if (id == provinceId)
                        continue;
                    if (!ProvinceObservation.TryCapture(em, id, ProvinceCoordinates.NameOf(id), out var snap2))
                        continue;
                    secondProvinceId = id;
                    secondProvinceDetail = snap2.DetailBlock;
                    break;
                }
            }

            if (secondProvinceId < 0)
            {
                log.AppendLine("WARN 03b_province_selected_second skipped -- aucune seconde province trouvée");
            }
            else
            {
                MapViewport.SelectProvince(
                    countryEntity, PlayerControl.DefaultControlledCountryId,
                    Entity.Null, secondProvinceId, MapViewport.WorldWindow);
                MapDisplaySystem.RequestRefresh();
                for (var i = 0; i < SettleFrames; i++)
                    yield return null;
                hud.RefreshCountryPanel("");
                hud.RefreshProvincePanel(secondProvinceDetail);
                hud.RefreshInfoBar(MapDisplaySystem.LastMetricsLine);
                yield return null;
                log.AppendLine(
                    "investir_status_default tag=03b_province_selected_second province_id=" + secondProvinceId +
                    " text='" + (hud.InvestStatusLabel?.text ?? "(null)") + "'");
                var secondDefaultResults = new List<GameViewCapture.CaptureResult>(1);
                yield return CaptureOne(
                    Path.Combine(_captureDir, "03b_province_selected_second.png"), secondDefaultResults, log, hud,
                    "03b_province_selected_second");

                InGameHud.ShowDebugIds = true;
                hud.RefreshProvincePanel(secondProvinceDetail);
                yield return null;
                log.AppendLine(
                    "investir_status_debug tag=03b_province_selected_second_debug province_id=" + secondProvinceId +
                    " text='" + (hud.InvestStatusLabel?.text ?? "(null)") + "'");
                var secondDebugResults = new List<GameViewCapture.CaptureResult>(1);
                yield return CaptureOne(
                    Path.Combine(_captureDir, "03b_province_selected_second_debug.png"), secondDebugResults, log, hud,
                    "03b_province_selected_second_debug");
                InGameHud.ShowDebugIds = HasFlag(ArgDebugIds);
                hud.RefreshProvincePanel(provinceDetail);
                yield return null;
            }

            MapViewport.ForceState(new MapViewportState
            {
                Level = MapObservationLevel.Country,
                TargetCountry = countryEntity,
                TargetProvince = Entity.Null,
                TargetCountryId = PlayerControl.DefaultControlledCountryId,
                TargetProvinceId = -1,
                Window = MapViewport.WorldWindow
            });
            SetPace(em, isPaused: true, speed: 1f);
            MapDisplaySystem.RequestRefresh();
            for (var i = 0; i < SettleFrames; i++)
                yield return null;
            hud.RefreshProvincePanel("");
            hud.RefreshCountryPanel(countryDetail);
            hud.RefreshInfoBar(MapDisplaySystem.LastMetricsLine);
            yield return null;
            yield return CaptureOne(Path.Combine(_captureDir, CaptureFiles[4]), results, log, hud, "04_pause_active");

            SetPace(em, isPaused: false, speed: 1f);
            MapViewport.SelectCountry(countryEntity, PlayerControl.DefaultControlledCountryId, MapViewport.WorldWindow);
            MapDisplaySystem.RequestRefresh();
            for (var i = 0; i < SettleFrames; i++)
                yield return null;

            PlayerIntentionSubmit.EnqueueSetProductionTaxRate(
                em, PlayerControl.DefaultControlledCountryId, TaxPolicyLimits.MinProductionTaxRate);
            for (var i = 0; i < 40; i++)
                yield return null;
            hud.SimulateTaxStepClick(-1);
            for (var i = 0; i < 40; i++)
                yield return null;
            hud.RefreshCountryPanel(countryDetail);
            hud.RefreshInfoBar(MapDisplaySystem.LastMetricsLine);
            yield return null;
            if (!AssertEditorial(hud, log, "05_tax_min", expectCountry: true, expectProvince: false))
                allOk = false;
            LogPanelOverlap(log, hud, "05_tax_min");
            yield return CaptureOne(Path.Combine(_captureDir, CaptureFiles[5]), results, log, hud, "05_tax_min");

            PlayerIntentionSubmit.EnqueueSetProductionTaxRate(
                em, PlayerControl.DefaultControlledCountryId, TaxPolicyLimits.MaxProductionTaxRate);
            for (var i = 0; i < 40; i++)
                yield return null;
            hud.SimulateTaxStepClick(+1);
            for (var i = 0; i < 40; i++)
                yield return null;
            hud.RefreshCountryPanel(countryDetail);
            hud.RefreshInfoBar(MapDisplaySystem.LastMetricsLine);
            yield return null;
            if (!AssertEditorial(hud, log, "06_tax_max", expectCountry: true, expectProvince: false))
                allOk = false;
            yield return CaptureOne(Path.Combine(_captureDir, CaptureFiles[6]), results, log, hud, "06_tax_max");

            var captureOk = results.Count == CaptureFiles.Length;
            var shaSet = new HashSet<string>(StringComparer.Ordinal);
            foreach (var r in results)
            {
                if (!r.Success || !r.HasHudChrome || !r.HasMapContent)
                    captureOk = false;
                if (!string.IsNullOrEmpty(r.Sha256))
                    shaSet.Add(r.Sha256);
            }

            if (shaSet.Count < 2)
            {
                log.AppendLine("FAIL toutes les captures partagent le même SHA256");
                captureOk = false;
            }

            allOk = allOk && captureOk;

            log.AppendLine("=== SHA256 captures ===");
            for (var i = 0; i < results.Count; i++)
            {
                var r = results[i];
                var name = i < CaptureFiles.Length ? CaptureFiles[i] : $"cap_{i}";
                log.AppendLine(
                    $"{name} sha={r.Sha256} dim={r.Width}x{r.Height} bytes={r.ByteLength} " +
                    $"source={r.Source} hud={r.HasHudChrome} map={r.HasMapContent} ok={r.Success}");
            }

            var exitCode = allOk ? 0 : 5;
            log.AppendLine("VERDICT=A_REVOIR_HUMAINEMENT");
            log.AppendLine(allOk
                ? "status=PASS_TECHNIQUE_EN_ATTENTE_REVUE_ARTISTIQUE"
                : "status=FAIL_CAPTURE_OU_HUD_ABSENT");

            WriteLog(log);
            Debug.Log($"UiStandaloneCaptureHarness exit={exitCode} log={_visualLogPath}");
            Application.Quit(exitCode);
        }

        /// <summary>
        /// brief 005-refonte-visuelle-carte : séquence >= 5 transitions de zoom
        /// (Success Condition 3), capture aux 3 niveaux monde/pays/province pour la
        /// mesure d'épaisseur de trait en pixels écran (Success Condition 4), et
        /// tentative réelle du chemin GPU (Success Condition 1 / Acceptable Waivers 1).
        /// </summary>
        IEnumerator RunV005ZoomAndGpuSequence()
        {
            var log = new StringBuilder(16384);
            log.AppendLine("=== v005 zoom + GPU-path attempt ===");
            log.AppendLine("started_at=" + DateTime.UtcNow.ToString("o"));
            log.AppendLine("source=" + GameViewCapture.SourceStandaloneFramebuffer);
            log.AppendLine("capture_target=" + GameViewCapture.Width + "x" + GameViewCapture.Height);

            GameViewCapture.ResetExpectedResolution();
            Screen.SetResolution(GameViewCapture.Width, GameViewCapture.Height, FullScreenMode.Windowed);
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;

            for (var i = 0; i < 10; i++)
                yield return null;

            InGameHud.ForceProgrammaticFallback = false;
            InGameHud.ShowDebugIds = false;
            if (InGameHud.Instance == null)
            {
                var go = new GameObject("InGameHud");
                go.AddComponent<InGameHud>();
            }

            for (var f = 0; f < WarmupFrames; f++)
                yield return null;

            var hud = InGameHud.Instance;
            if (hud == null || !hud.UiReady)
            {
                log.AppendLine("FAIL InGameHud non prêt");
                FailAndQuit(log, 3);
                yield break;
            }

            var dotsWorld = Unity.Entities.World.DefaultGameObjectInjectionWorld;
            if (dotsWorld == null || !dotsWorld.IsCreated)
            {
                log.AppendLine("FAIL World DOTS absent");
                FailAndQuit(log, 4);
                yield break;
            }

            var em = dotsWorld.EntityManager;
            Directory.CreateDirectory(_captureDir);

            var waitPresent = 0;
            while (!MapDisplaySystem.HasPresentedFrame && waitPresent < 300)
            {
                waitPresent++;
                yield return null;
            }

            SetPace(em, isPaused: false, speed: 1f);

            Entity countryEntity = Entity.Null;
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<CountryData>()))
            using (var entities = q.ToEntityArray(Unity.Collections.Allocator.Temp))
            using (var data = q.ToComponentDataArray<CountryData>(Unity.Collections.Allocator.Temp))
            {
                for (var i = 0; i < data.Length; i++)
                {
                    if (data[i].CountryId != PlayerControl.DefaultControlledCountryId)
                        continue;
                    countryEntity = entities[i];
                    break;
                }
            }

            var provinceId = -1;
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<CountryData>()))
            using (var data = q.ToComponentDataArray<CountryData>(Unity.Collections.Allocator.Temp))
            {
                for (var i = 0; i < data.Length; i++)
                {
                    if (data[i].CountryId != PlayerControl.DefaultControlledCountryId)
                        continue;
                    if (data[i].CapitalProvinceId >= 0)
                        provinceId = data[i].CapitalProvinceId;
                    break;
                }
            }

            var results = new List<GameViewCapture.CaptureResult>(8);
            var transitionIndex = 0;

            IEnumerator DoTransition(string tag, string zoomLevel, Action apply)
            {
                transitionIndex++;
                apply();
                MapDisplaySystem.RequestRefresh();
                for (var i = 0; i < SettleFrames; i++)
                    yield return null;
                hud.RefreshInfoBar(MapDisplaySystem.LastMetricsLine);
                yield return null;
                log.AppendLine(
                    "zoom_transition index=" + transitionIndex + " tag=" + tag + " zoom_level=" + zoomLevel +
                    " geometryRebuildMs=" + MapDisplaySystem.LastWindowRebuildMilliseconds.ToString("0.###") +
                    " cpuRasterMs=" + MapDisplaySystem.LastCpuRasterMilliseconds.ToString("0.###") +
                    " presentFrameMs=" + MapDisplaySystem.LastPresentFrameMilliseconds.ToString("0.###") +
                    " fullRedrawMs=" + MapDisplaySystem.LastFullRedrawMilliseconds.ToString("0.###") +
                    " gpuUsed=" + MapDisplaySystem.GpuBackgroundUsedThisFrame +
                    " gpuMs=" + MapDisplaySystem.LastGpuBackgroundMilliseconds.ToString("0.###") +
                    " geometryBuilds=" + MapDisplaySystem.GeometryBuilds);
                yield return CaptureOne(Path.Combine(_captureDir, tag + ".png"), results, log, hud, tag);
            }

            // 5 transitions : Monde -> Pays -> Province -> Monde -> Pays (mêmes niveaux
            // que Success Condition 4 réutilise pour min/mid/max).
            yield return DoTransition(
                "zoom_01_world", "min",
                () => MapViewport.ForceState(MapViewportState.World(MapViewport.WorldWindow)));
            yield return DoTransition(
                "zoom_02_country", "mid",
                () => MapViewport.SelectCountry(
                    countryEntity, PlayerControl.DefaultControlledCountryId, MapViewport.WorldWindow));
            yield return DoTransition(
                "zoom_03_province", "max",
                () => MapViewport.SelectProvince(
                    countryEntity, PlayerControl.DefaultControlledCountryId,
                    Entity.Null, provinceId, MapViewport.WorldWindow));
            yield return DoTransition(
                "zoom_04_world_back", "min",
                () => MapViewport.ForceState(MapViewportState.World(MapViewport.WorldWindow)));
            yield return DoTransition(
                "zoom_05_country_back", "mid",
                () => MapViewport.SelectCountry(
                    countryEntity, PlayerControl.DefaultControlledCountryId, MapViewport.WorldWindow));

            // --- tentative réelle du chemin GPU (Success Condition 1 / Acceptable Waivers row 1) ---
            log.AppendLine("=== GPU path attempt ===");
            var prevPilot = PilotMapProvider.Enabled;
            PilotMapProvider.SetEnabled(true, clearCache: true);
            MapViewport.SelectProvince(
                countryEntity, PlayerControl.DefaultControlledCountryId,
                Entity.Null, provinceId, MapViewport.WorldWindow);
            MapDisplaySystem.RequestRefresh();
            yield return null; // frame où PresentRenderTexture est censé s'exécuter
            log.AppendLine(
                "gpu_attempt_frame0 gpuUsed=" + MapDisplaySystem.GpuBackgroundUsedThisFrame +
                " gpuFrames=" + MapDisplaySystem.GpuBackgroundFrames +
                " gpuMs=" + MapDisplaySystem.LastGpuBackgroundMilliseconds.ToString("0.###") +
                " pilotEnabled=" + PilotMapProvider.Enabled +
                " gpuRendererAvailable=" + MapGpuRenderer.IsAvailable);
            yield return CaptureOne(
                Path.Combine(_captureDir, "gpu_attempt_frame0.png"), results, log, hud, "gpu_attempt_frame0");
            for (var i = 0; i < 3; i++)
                yield return null;
            log.AppendLine(
                "gpu_attempt_settled gpuUsed=" + MapDisplaySystem.GpuBackgroundUsedThisFrame +
                " gpuFrames=" + MapDisplaySystem.GpuBackgroundFrames +
                " — brief 005-refonte-visuelle-carte, Success Condition 3, itération de " +
                "correction du feedback : CETTE PHRASE DÉCRIVAIT LE DÉFAUT AVANT CORRECTIF, " +
                "PAS LE COMPORTEMENT ACTUEL. Depuis le court-circuit ajouté dans " +
                "MapDisplaySystem.OnUpdate, quand TryRenderGpuBackground réussit sur une frame " +
                "de changement de viewport, PresentFrame (CPU) et la rastérisation CPU pleine " +
                "sont sautées CETTE frame-là — la RenderTexture GPU présentée via " +
                "PresentRenderTexture est alors bien ce que l'écran affiche à la fin de cette " +
                "frame, tant qu'un autre déclencheur indépendant du viewport (dueByOwner — " +
                "changement de possession de province par la guerre en cours, dueByTick, " +
                "hoverChanged) ne force pas un redessin CPU complet séparé avant que ce log ne " +
                "soit lu (gpuUsed/gpuFrames ci-dessus restent la seule preuve directement " +
                "vérifiable dans CE log ; voir generator-log.md Success Condition 3 pour la " +
                "mesure complète des 5 transitions, avant/après ce correctif).");
            yield return CaptureOne(
                Path.Combine(_captureDir, "gpu_attempt_settled.png"), results, log, hud, "gpu_attempt_settled");
            PilotMapProvider.SetEnabled(prevPilot, clearCache: true);
            MapDisplaySystem.RequestRefresh();
            for (var i = 0; i < SettleFrames; i++)
                yield return null;

            var exitCode = 0;
            log.AppendLine("VERDICT=A_REVOIR_HUMAINEMENT");
            log.AppendLine("status=PASS_TECHNIQUE_EN_ATTENTE_REVUE_ARTISTIQUE");
            WriteLog(log);
            Debug.Log($"UiStandaloneCaptureHarness v005 exit={exitCode} log={_visualLogPath}");
            Application.Quit(exitCode);
        }

        IEnumerator CaptureOne(
            string path, List<GameViewCapture.CaptureResult> results, StringBuilder log,
            InGameHud hud, string tag, bool preferPanelRt = false)
        {
            GameViewCapture.CaptureResult cap = default;
            const int maxAttempts = 8;
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                if (preferPanelRt)
                    yield return GameViewCapture.CaptureStandalonePanelRtPngCoroutine(path, r => cap = r);
                else
                    yield return GameViewCapture.CaptureFramebufferPngCoroutine(path, r => cap = r);
                if (cap.Success)
                    break;
                log.AppendLine($"capture retry tag={tag} attempt={attempt} error={cap.Error}");
                for (var i = 0; i < 5; i++)
                    yield return null;
            }

            results.Add(cap);
            LogHudState(hud, log, tag);
            if (!cap.Success)
            {
                log.AppendLine($"capture FAIL file={tag} error={cap.Error}");
            }
            else
            {
                log.AppendLine(
                    $"capture file={Path.GetFileName(path)} bytes={cap.ByteLength} sha256={cap.Sha256} " +
                    $"dim={cap.Width}x{cap.Height} screen={cap.ScreenWidth}x{cap.ScreenHeight} " +
                    $"source={cap.Source} hud_chrome={cap.HasHudChrome} map={cap.HasMapContent}");
            }
        }

        /// <summary>
        /// brief 005-refonte-visuelle-carte, Success Condition 6a : chevauchement mesuré
        /// entre TOUTES les paires de panneaux HUD visibles (pas seulement Lois/Impôt) —
        /// voir HudLayoutProbe.MeasureAllPanelOverlaps.
        /// </summary>
        static void LogPanelOverlap(StringBuilder log, InGameHud hud, string tag)
        {
            var overlaps = HudLayoutProbe.MeasureAllPanelOverlaps(hud, out var pairsChecked);
            log.AppendLine(
                "panel_overlap tag=" + tag + " pairs_checked=" + pairsChecked +
                " overlaps=" + overlaps.Count);
            foreach (var (a, b) in overlaps)
                log.AppendLine("  overlap tag=" + tag + " pair=" + a + "+" + b);
        }

        static void LogHudState(InGameHud hud, StringBuilder log, string tag)
        {
            if (hud == null)
            {
                log.AppendLine($"hud_state tag={tag} missing");
                return;
            }

            log.AppendLine(
                $"hud_state tag={tag} " +
                $"view='{hud.ViewContextLabel?.text}' " +
                $"date='{hud.DateLabel?.text}' " +
                $"info='{hud.InfoBarText}' " +
                $"badge='{hud.PaceStatusBadge?.text}' " +
                $"pauseBtn='{hud.PauseButton?.text}' " +
                $"tax='{hud.TaxStatusLabel?.text?.Replace("\n", " | ")}' " +
                $"taxBtnDown='{hud.TaxDownButton?.text}' " +
                $"taxBtnUp='{hud.TaxUpButton?.text}' " +
                $"prov={(string.IsNullOrEmpty(hud.LastProvinceDetail) ? "hidden" : "visible")} " +
                $"country={(string.IsNullOrEmpty(hud.LastCountryDetail) ? "hidden" : "visible")}");
        }

        static bool AssertEditorial(
            InGameHud hud, StringBuilder log, string tag, bool expectCountry, bool expectProvince)
        {
            var ok = true;
            var bundle = new StringBuilder(2048);
            // Scope actually collected below, named explicitly in the log line (feedback-002.md
            // Issue 2): CountryPanel/ProvincePanel + TaxStatus/TaxButtons only. The "Lois"
            // (_lawBar) and "Investir" (_investBar) blocks are siblings of CountryPanel/
            // ProvincePanel in the visual tree, not descendants, and are NOT collected here —
            // editorial_forbidden=PASS below covers exactly the named scope, never the whole
            // screen.
            var scopeParts = new List<string>(4);
            if (expectCountry && hud.CountryPanel != null)
            {
                bundle.AppendLine(HudDetailPresenter.CollectVisibleText(hud.CountryPanel));
                scopeParts.Add("CountryPanel");
            }
            if (expectProvince && hud.ProvincePanel != null)
            {
                bundle.AppendLine(HudDetailPresenter.CollectVisibleText(hud.ProvincePanel));
                scopeParts.Add("ProvincePanel");
            }
            if (hud.TaxStatusLabel != null && !string.IsNullOrEmpty(hud.TaxStatusLabel.text))
            {
                bundle.AppendLine(hud.TaxStatusLabel.text);
                scopeParts.Add("TaxStatus");
            }
            if (hud.TaxDownButton != null)
                bundle.AppendLine(hud.TaxDownButton.text);
            if (hud.TaxUpButton != null)
                bundle.AppendLine(hud.TaxUpButton.text);
            if (hud.TaxDownButton != null || hud.TaxUpButton != null)
                scopeParts.Add("TaxButtons");
            var scope = scopeParts.Count > 0 ? string.Join("+", scopeParts) : "none";

            var text = bundle.ToString();
            log.AppendLine($"editorial_probe tag={tag} chars={text.Length} scope={scope}");
            log.AppendLine("--- editorial_text_begin ---");
            log.AppendLine(text.Trim());
            log.AppendLine("--- editorial_text_end ---");

            if (HudDetailPresenter.ContainsForbiddenUserToken(text, out var hit))
            {
                log.AppendLine($"FAIL editorial forbidden token='{hit}' tag={tag} scope={scope}");
                ok = false;
            }
            else
                log.AppendLine($"editorial_forbidden=PASS tag={tag} scope={scope}");

            if (expectCountry)
            {
                if (text.IndexOf("Pays contrôlé", StringComparison.OrdinalIgnoreCase) < 0 &&
                    text.IndexOf("Pays géré", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    log.AppendLine($"FAIL editorial missing FR subtitle pays tag={tag}");
                    ok = false;
                }

                if (text.IndexOf("Trésor", StringComparison.OrdinalIgnoreCase) < 0 &&
                    text.IndexOf("Dette", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    log.AppendLine($"FAIL editorial missing FR indicateurs pays tag={tag}");
                    ok = false;
                }
            }

            if (expectProvince)
            {
                if (text.IndexOf("Île-de-France", StringComparison.OrdinalIgnoreCase) < 0 &&
                    text.IndexOf("Propriétaire", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    log.AppendLine($"FAIL editorial missing accent/propriétaire province tag={tag}");
                    ok = false;
                }

                if (text.IndexOf("Paysans", StringComparison.OrdinalIgnoreCase) < 0 &&
                    text.IndexOf("Artisans", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    log.AppendLine($"FAIL editorial missing pop FR tag={tag}");
                    ok = false;
                }
            }

            if (tag.StartsWith("05_tax", StringComparison.Ordinal) ||
                tag.StartsWith("06_tax", StringComparison.Ordinal))
            {
                var down = hud.TaxDownButton?.text ?? "";
                var up = hud.TaxUpButton?.text ?? "";
                if (down.IndexOf("Impôt", StringComparison.OrdinalIgnoreCase) < 0 ||
                    up.IndexOf("Impôt", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    log.AppendLine($"FAIL editorial tax buttons FR down='{down}' up='{up}'");
                    ok = false;
                }

                var tax = hud.TaxStatusLabel?.text ?? "";
                if (tax.IndexOf("Taux", StringComparison.OrdinalIgnoreCase) < 0 ||
                    tax.IndexOf("plage", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    log.AppendLine($"FAIL editorial tax grid FR tax='{tax}'");
                    ok = false;
                }
            }

            return ok;
        }

        static void SetPace(EntityManager em, bool isPaused, float speed)
        {
            using var q = em.CreateEntityQuery(ComponentType.ReadWrite<WorldState>());
            if (q.IsEmptyIgnoreFilter)
                return;
            var entity = q.GetSingletonEntity();
            var ws = em.GetComponentData<WorldState>(entity);
            ws.IsPaused = isPaused;
            ws.SimulationSpeed = speed;
            em.SetComponentData(entity, ws);
            if (InGameHud.Instance != null)
                InGameHud.Instance.RefreshInfoBar(InGameHud.Instance.LastMetricsLine);
        }

        void FailAndQuit(StringBuilder log, int code, bool append = false)
        {
            log.AppendLine("VERDICT=A_REVOIR_HUMAINEMENT");
            log.AppendLine("status=FAIL");
            WriteLog(log, append: append);
            Application.Quit(code);
        }

        void WriteLog(StringBuilder log, bool append = false)
        {
            try
            {
                var dir = Path.GetDirectoryName(_visualLogPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                if (append && File.Exists(_visualLogPath))
                    File.AppendAllText(_visualLogPath, log.ToString(), Encoding.UTF8);
                else
                    File.WriteAllText(_visualLogPath, log.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Debug.LogError("UiStandaloneCaptureHarness log write failed: " + ex.Message);
            }
        }
    }
}
