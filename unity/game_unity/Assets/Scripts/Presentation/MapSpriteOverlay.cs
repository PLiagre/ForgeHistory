using Unity.Entities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Mathematics;
using UnityEngine;
using VictoriaGame.Core;
using VictoriaGame.Economy;
using VictoriaGame.Military;
using VictoriaGame.Politics;
using VictoriaGame.World;

namespace VictoriaGame.Presentation
{
    /// <summary>
    /// Overlay économique de carte (v1_034) — lecture seule ECS.
    /// Texture2D / Color32 = managé → <see cref="SystemBase"/> (pas Burst / pas ISystem),
    /// même contrainte que <see cref="MapDisplaySystem"/>.
    ///
    /// PARTIE 1 : inventaire des modèles Codex + sprites déterministes (cache disque hors Art/).
    /// PARTIE 2 : composition selon good_tag / activités / niveau de zoom.
    /// PARTIE 3 : fiche pays (données réelles).
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(MapDisplaySystem))]
    public partial class MapSpriteOverlaySystem : SystemBase
    {
        protected override void OnUpdate()
        {
            // Composition appelée depuis MapDisplaySystem (même frame de pixels).
            // Ce système garantit le chargement du catalogue une fois le monde prêt.
            if (!SystemAPI.HasSingleton<WorldState>())
                return;
            MapSpriteCatalog.EnsureReady();
        }
    }

    /// <summary>
    /// Arbitrage de lisibilité par niveau d'observation (documenté + mesuré).
    /// Monde : 0 sprite (carte politique intacte).
    /// Pays : prop du bien principal, petit, opacite 180.
    /// Province : prop principal + activités + bâtiment d'activité, plus grand.
    /// Couches thématiques : sprites masqués (domaines v1_005 inchangés).
    /// </summary>
    public static class MapSpriteVisibility
    {
        public const int WorldMaxMarkers = 0;
        /// <summary>Tailles neutres (bit-identiques v1_034 / avant v1_073).</summary>
        public const int CountrySpriteSize = 10;
        public const int ProvinceSpriteSize = 22;
        public const int ProvinceActivitySize = 14;
        /// <summary>Tailles zoom (v1_073) — mêmes modèles 32×32 redimensionnés.</summary>
        public const int ZoomCountrySpriteSize = 16;
        public const int ZoomProvinceSpriteSize = 28;
        public const int ZoomProvinceActivitySize = 18;
        public const byte CountryAlpha = 180;
        public const byte ProvinceAlpha = 230;
        public const byte ThematicAlpha = 0;

        public static int SpriteSizeFor(MapObservationLevel level)
        {
            if (!MapSnapshotExporter.ZoomScaleEnabled)
            {
                return level == MapObservationLevel.Province
                    ? ProvinceSpriteSize
                    : CountrySpriteSize;
            }

            return level == MapObservationLevel.Province
                ? ZoomProvinceSpriteSize
                : ZoomCountrySpriteSize;
        }

        public static int ActivitySizeFor(MapObservationLevel level)
        {
            if (level != MapObservationLevel.Province)
                return 0;
            return MapSnapshotExporter.ZoomScaleEnabled
                ? ZoomProvinceActivitySize
                : ProvinceActivitySize;
        }

        public static bool ShowPrimaryGood(MapObservationLevel level) =>
            level == MapObservationLevel.Country || level == MapObservationLevel.Province;

        public static bool ShowActivities(MapObservationLevel level) =>
            level == MapObservationLevel.Province;

        public static bool ShowActivityBuilding(MapObservationLevel level) =>
            level == MapObservationLevel.Province;

        public static string DocumentedPolicy() =>
            "WORLD:0 sprites (lisibilité politique). " +
            "COUNTRY: prop good_tag size=" + SpriteSizeFor(MapObservationLevel.Country) +
            " (neutre=" + CountrySpriteSize + " zoom=" + ZoomCountrySpriteSize + ") a=180. " +
            "PROVINCE: prop+activités+bâtiment-activité size=" +
            SpriteSizeFor(MapObservationLevel.Province) + "/" +
            ActivitySizeFor(MapObservationLevel.Province) +
            " (neutre=" + ProvinceSpriteSize + "/" + ProvinceActivitySize +
            " zoom=" + ZoomProvinceSpriteSize + "/" + ZoomProvinceActivitySize + ") a=230. " +
            "THEMATIC: sprites masqués (domaines fixes v1_005). " +
            "ZoomScaleEnabled=" + MapSnapshotExporter.ZoomScaleEnabled + ".";
    }

    /// <summary>
    /// Catalogue modèles → sprites. Les modèles Codex sont LUS (jamais modifiés).
    /// Sprites générés hors <c>Assets/Art/**</c> dans PresentationCache (déterministe).
    /// Cinq navires sans .meta : signalés, écartés — pas de .meta écrit à la main.
    /// </summary>
    public static class MapSpriteCatalog
    {
        public const string CacheRelativeDir = "PresentationCache/Sprites";
        public const int SpriteResolution = 32;

        static readonly string[] ExpectedProps =
        {
            "prop_grain_1400", "prop_fish_1400", "prop_livestock_1400", "prop_wood_1400",
            "prop_iron_1400", "prop_wool_1400", "prop_coal_1400", "prop_cloth_1400",
            "prop_weapons_1400", "prop_paper_1400", "prop_spices_1400", "prop_silk_1400",
            "prop_wine_1400", "prop_dyes_1400"
        };

        static readonly string[] ExpectedBuildings =
        {
            "building_farm_1400", "building_sawmill_1400", "building_workshop_1400"
        };

        /// <summary>Marqueurs ville (v1_037) — même pipeline de pré-rendu que props/bâtiments.</summary>
        static readonly string[] ExpectedCities =
        {
            "city_capital_1400", "city_port_1400", "city_episcopal_1400", "city_borough_1400"
        };

        static readonly string[] ExpectedShipsWithMeta =
        {
            "unit_cog_1400", "unit_galley_1400", "unit_carrack_1450", "unit_galleon_1550"
        };

        /// <summary>Navires livrés sans .meta — Unity ne les importe pas. Ne pas réparer.</summary>
        static readonly string[] ShipsWithoutMeta =
        {
            "unit_frigate_1700", "unit_ship_of_line_1700", "unit_man_of_war_1750",
            "unit_steam_frigate_1840", "unit_ironclad_1860"
        };

        static bool _ready;
        static readonly Dictionary<string, Color32[]> _sprites =
            new Dictionary<string, Color32[]>(64);
        static InventoryReport _lastInventory;

        public struct InventoryReport
        {
            public int ModelsFound;
            public int SpritesConverted;
            public int Discarded;
            public string DiscardReasons;
            public string ModelsRoot;
            public string CacheRoot;
        }

        public static InventoryReport LastInventory => _lastInventory;
        public static bool IsReady => _ready;
        public static int SpriteCount => _sprites.Count;

        public static void EnsureReady()
        {
            if (_ready)
                return;
            Rebuild();
        }

        public static void Rebuild()
        {
            _sprites.Clear();
            var modelsRoot = ResolveModelsRoot();
            var cacheRoot = ResolveCacheRoot();
            Directory.CreateDirectory(cacheRoot);

            var found = 0;
            var converted = 0;
            var discarded = 0;
            var reasons = new StringBuilder(512);

            void TryModel(string stem, bool requireMeta)
            {
                var fbx = Path.Combine(modelsRoot, stem + ".fbx");
                var meta = fbx + ".meta";
                if (!File.Exists(fbx))
                    return;
                found++;
                if (requireMeta && !File.Exists(meta))
                {
                    discarded++;
                    reasons.Append(stem).Append(" (fbx sans .meta — écarté, périmètre Codex); ");
                    return;
                }

                // Pré-rendu orthographique déterministe (silhouette cataloguée).
                // Les FBX ne sont pas importés dans le projet sim (INTERDIT Art/) :
                // le sprite est dérivé du stem + empreinte fichier, regeneré si mtime change.
                var pixels = LoadOrRenderSprite(cacheRoot, stem, fbx);
                _sprites[stem] = pixels;
                converted++;
            }

            foreach (var p in ExpectedProps)
                TryModel(p, requireMeta: true);
            foreach (var b in ExpectedBuildings)
                TryModel(b, requireMeta: true);
            foreach (var s in ExpectedShipsWithMeta)
                TryModel(s, requireMeta: true);

            foreach (var s in ShipsWithoutMeta)
            {
                var fbx = Path.Combine(modelsRoot, s + ".fbx");
                if (!File.Exists(fbx))
                    continue;
                found++;
                discarded++;
                reasons.Append(s).Append(" (sans .meta — signalé, non réparé); ");
            }

            // Garantir un sprite pour chaque good_tag même si le modèle manque.
            foreach (var p in ExpectedProps)
            {
                if (_sprites.ContainsKey(p))
                    continue;
                var good = GoodTagFromPropStem(p);
                _sprites[p] = LoadOrRenderSprite(cacheRoot, p, fingerprintSource: null);
                converted++;
                reasons.Append(p).Append(" (fallback procédural, modèle absent); ");
            }

            foreach (var b in ExpectedBuildings)
            {
                if (_sprites.ContainsKey(b))
                    continue;
                _sprites[b] = LoadOrRenderSprite(cacheRoot, b, fingerprintSource: null);
                converted++;
                reasons.Append(b).Append(" (fallback procédural, modèle absent); ");
            }

            foreach (var c in ExpectedCities)
            {
                if (_sprites.ContainsKey(c))
                    continue;
                _sprites[c] = LoadOrRenderSprite(cacheRoot, c, fingerprintSource: null);
                converted++;
            }

            _lastInventory = new InventoryReport
            {
                ModelsFound = found,
                SpritesConverted = converted,
                Discarded = discarded,
                DiscardReasons = reasons.ToString(),
                ModelsRoot = modelsRoot,
                CacheRoot = cacheRoot
            };
            _ready = true;
            Debug.Log(
                $"MapSpriteCatalog: found={found} converted={converted} discarded={discarded} " +
                $"root={modelsRoot} cache={cacheRoot}");
        }

        /// <summary>Assure les 4 sprites ville (réutilise Rebuild / cache disque v1_034).</summary>
        public static void EnsureCitySprites()
        {
            EnsureReady();
            var cacheRoot = ResolveCacheRoot();
            for (var i = 0; i < ExpectedCities.Length; i++)
            {
                var stem = ExpectedCities[i];
                if (_sprites.ContainsKey(stem))
                    continue;
                _sprites[stem] = LoadOrRenderSprite(cacheRoot, stem, fingerprintSource: null);
            }
        }

        public static string CityStemForStatus(CityStatus status) => status switch
        {
            CityStatus.Capital => "city_capital_1400",
            CityStatus.Port => "city_port_1400",
            CityStatus.Episcopal => "city_episcopal_1400",
            _ => "city_borough_1400",
        };

        public static bool TryGetSprite(string stem, out Color32[] pixels)
        {
            EnsureReady();
            return _sprites.TryGetValue(stem, out pixels);
        }

        public static string PropStemForGood(string goodTag)
        {
            if (string.IsNullOrEmpty(goodTag))
                return null;
            return "prop_" + goodTag.Trim().ToLowerInvariant() + "_1400";
        }

        public static string BuildingStemForActivity(string goodTag)
        {
            if (string.IsNullOrEmpty(goodTag))
                return "building_workshop_1400";
            switch (goodTag.Trim().ToLowerInvariant())
            {
                case "wood":
                    return "building_sawmill_1400";
                case "grain":
                case "livestock":
                case "wine":
                case "fish":
                case "wool":
                    return "building_farm_1400";
                default:
                    return "building_workshop_1400";
            }
        }

        public static string BuildingStemForType(BuildingType type) => type switch
        {
            BuildingType.Farm => "building_farm_1400",
            BuildingType.Sawmill => "building_sawmill_1400",
            BuildingType.Workshop => "building_workshop_1400",
            _ => "building_workshop_1400",
        };

        static string GoodTagFromPropStem(string stem)
        {
            // prop_grain_1400 → grain
            if (string.IsNullOrEmpty(stem) || !stem.StartsWith("prop_", StringComparison.Ordinal))
                return "";
            var rest = stem.Substring(5);
            var us = rest.LastIndexOf('_');
            return us > 0 ? rest.Substring(0, us) : rest;
        }

        static string ResolveModelsRoot()
        {
            // 1) Copie présentation locale (si présente, hors Art/)
            var local = Path.GetFullPath(Path.Combine(
                Application.dataPath, "..", "PresentationCache", "ModelsReadOnly"));
            if (Directory.Exists(local) && Directory.GetFiles(local, "*.fbx").Length > 0)
                return local;

            // 2) Arbre Codex (lecture seule) — sibling du repo sim :
            //    dataPath = <repo>/game_unity/Assets → <parent>/VictoriaProject-assets/...
            var repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            var parentDir = Path.GetDirectoryName(repoRoot);
            if (!string.IsNullOrEmpty(parentDir))
            {
                var assetsTree = Path.Combine(
                    parentDir, "VictoriaProject-assets",
                    "game_unity", "Assets", "Art", "Models");
                if (Directory.Exists(assetsTree))
                    return assetsTree;
            }

            // 3) Art/Models du projet sim (souvent vide — Codex travaille ailleurs)
            var art = Path.Combine(Application.dataPath, "Art", "Models");
            return art;
        }

        static string ResolveCacheRoot() =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", CacheRelativeDir));

        static Color32[] LoadOrRenderSprite(string cacheRoot, string stem, string fingerprintSource)
        {
            var pngPath = Path.Combine(cacheRoot, stem + ".png");
            var stampPath = Path.Combine(cacheRoot, stem + ".stamp");
            var stamp = ComputeStamp(stem, fingerprintSource);

            if (File.Exists(pngPath) && File.Exists(stampPath) &&
                File.ReadAllText(stampPath).Trim() == stamp)
            {
                var cached = TryReadPng(pngPath);
                if (cached != null && cached.Length == SpriteResolution * SpriteResolution)
                    return cached;
            }

            var pixels = RenderDeterministicOrthoSprite(stem);
            WritePng(pngPath, pixels, SpriteResolution, SpriteResolution);
            File.WriteAllText(stampPath, stamp);
            return pixels;
        }

        static string ComputeStamp(string stem, string fingerprintSource)
        {
            long len = 0;
            long mtime = 0;
            if (!string.IsNullOrEmpty(fingerprintSource) && File.Exists(fingerprintSource))
            {
                var fi = new FileInfo(fingerprintSource);
                len = fi.Length;
                mtime = fi.LastWriteTimeUtc.Ticks;
            }

            // Empreinte déterministe (pas Guid/Random/DateTime.Now).
            unchecked
            {
                var h = (uint)stem.Length * 16777619u;
                for (var i = 0; i < stem.Length; i++)
                    h = (h ^ stem[i]) * 16777619u;
                h ^= (uint)len;
                h ^= (uint)(mtime ^ (mtime >> 32));
                return h.ToString("x8", CultureInfo.InvariantCulture) +
                       "_" + len.ToString(CultureInfo.InvariantCulture);
            }
        }

        /// <summary>
        /// Pré-rendu orthographique fixe (angle identique pour tous) : fond transparent,
        /// silhouette cataloguée. Remplace le rendu FBX tant que les modèles restent
        /// hors du projet sim (INTERDIT d'écrire dans Art/).
        /// </summary>
        public static Color32[] RenderDeterministicOrthoSprite(string stem)
        {
            var n = SpriteResolution;
            var px = new Color32[n * n];
            var kind = ClassifyStem(stem);
            var rgb = ColorForStem(stem);

            for (var y = 0; y < n; y++)
            for (var x = 0; x < n; x++)
            {
                var u = (x + 0.5f) / n;
                var v = (y + 0.5f) / n;
                // Projection orthographique fixe : léger biais « 3/4 » identique.
                var ox = u * 0.92f + 0.04f;
                var oy = v * 0.88f + 0.06f + (u - 0.5f) * 0.08f;
                var inside = kind switch
                {
                    StemKind.Prop => InsideProp(ox, oy, stem),
                    StemKind.Building => InsideBuilding(ox, oy, stem),
                    StemKind.Ship => InsideShip(ox, oy),
                    _ => false
                };
                if (!inside)
                {
                    px[y * n + x] = new Color32(0, 0, 0, 0);
                    continue;
                }

                var shade = (byte)math.clamp((int)(180 + oy * 60 + ox * 20), 120, 255);
                px[y * n + x] = new Color32(
                    (byte)(rgb.r * shade / 255),
                    (byte)(rgb.g * shade / 255),
                    (byte)(rgb.b * shade / 255),
                    255);
            }

            return px;
        }

        enum StemKind : byte { Prop, Building, Ship, Other }

        static StemKind ClassifyStem(string stem)
        {
            if (stem.StartsWith("prop_", StringComparison.Ordinal)) return StemKind.Prop;
            if (stem.StartsWith("building_", StringComparison.Ordinal)) return StemKind.Building;
            if (stem.StartsWith("city_", StringComparison.Ordinal)) return StemKind.Building;
            if (stem.StartsWith("unit_", StringComparison.Ordinal)) return StemKind.Ship;
            return StemKind.Other;
        }

        static Color32 ColorForStem(string stem)
        {
            var tag = stem;
            if (stem.StartsWith("prop_", StringComparison.Ordinal))
                tag = GoodTagFromPropStem(stem);
            switch (tag)
            {
                case "grain": return new Color32(210, 180, 60, 255);
                case "fish": return new Color32(70, 140, 200, 255);
                case "livestock": return new Color32(160, 100, 60, 255);
                case "wood": return new Color32(90, 140, 50, 255);
                case "iron": return new Color32(120, 120, 130, 255);
                case "wool": return new Color32(220, 220, 230, 255);
                case "coal": return new Color32(40, 40, 45, 255);
                case "cloth": return new Color32(180, 70, 120, 255);
                case "weapons": return new Color32(150, 150, 160, 255);
                case "paper": return new Color32(240, 235, 210, 255);
                case "spices": return new Color32(200, 90, 40, 255);
                case "silk": return new Color32(200, 160, 220, 255);
                case "wine": return new Color32(120, 30, 60, 255);
                case "dyes": return new Color32(80, 40, 160, 255);
                case "building_farm_1400": return new Color32(170, 140, 90, 255);
                case "building_sawmill_1400": return new Color32(110, 90, 60, 255);
                case "building_workshop_1400": return new Color32(140, 110, 100, 255);
                case "city_capital_1400": return new Color32(220, 40, 40, 255);
                case "city_port_1400": return new Color32(40, 100, 210, 255);
                case "city_episcopal_1400": return new Color32(210, 190, 40, 255);
                case "city_borough_1400": return new Color32(60, 60, 60, 255);
                default:
                    // Couleur stable dérivée du nom (pas Random).
                    unchecked
                    {
                        uint h = 2166136261u;
                        for (var i = 0; i < stem.Length; i++)
                            h = (h ^ stem[i]) * 16777619u;
                        return new Color32(
                            (byte)(80 + (h & 0x7F)),
                            (byte)(80 + ((h >> 8) & 0x7F)),
                            (byte)(80 + ((h >> 16) & 0x7F)),
                            255);
                    }
            }
        }

        static bool InsideProp(float u, float v, string stem)
        {
            var dx = u - 0.5f;
            var dy = v - 0.45f;
            var good = GoodTagFromPropStem(stem);
            switch (good)
            {
                case "grain":
                    return math.abs(dx) < 0.08f && v > 0.2f && v < 0.75f ||
                           (v > 0.65f && v < 0.85f && math.abs(dx) < 0.28f);
                case "fish":
                    return dx * dx * 2.2f + dy * dy < 0.09f && u < 0.75f;
                case "wine":
                    return math.abs(dx) < 0.12f && v > 0.25f && v < 0.7f ||
                           (v > 0.15f && v < 0.3f && math.abs(dx) < 0.2f);
                default:
                    return dx * dx + dy * dy < 0.12f;
            }
        }

        static bool InsideBuilding(float u, float v, string stem)
        {
            // Marqueurs ville : disque / pointe selon statut (même pipeline de raster).
            if (stem != null && stem.StartsWith("city_", StringComparison.Ordinal))
            {
                var dx = u - 0.5f;
                var dy = v - 0.5f;
                if (stem.Contains("capital"))
                    return dx * dx + dy * dy < 0.18f * 0.18f ||
                           (v > 0.55f && v < 0.92f && math.abs(dx) < 0.06f);
                if (stem.Contains("port"))
                    return (dx * dx + dy * dy < 0.16f * 0.16f) ||
                           (v > 0.15f && v < 0.35f && math.abs(dx) < 0.28f);
                if (stem.Contains("episcopal"))
                    return math.abs(dx) < 0.12f && v > 0.2f && v < 0.75f ||
                           (v > 0.7f && v < 0.9f && math.abs(dx) < 0.22f);
                return dx * dx + dy * dy < 0.15f * 0.15f;
            }

            // Corps + toit (angle ortho fixe).
            if (v > 0.2f && v < 0.55f && u > 0.25f && u < 0.75f)
                return true;
            var roofY = 0.55f + (0.5f - math.abs(u - 0.5f)) * 0.7f;
            return v >= 0.55f && v <= roofY && u > 0.22f && u < 0.78f;
        }

        static bool InsideShip(float u, float v)
        {
            var hull = v > 0.25f && v < 0.45f && u > 0.15f && u < 0.85f;
            var mast = math.abs(u - 0.5f) < 0.04f && v > 0.4f && v < 0.85f;
            return hull || mast;
        }

        static Color32[] TryReadPng(string path)
        {
            try
            {
                var bytes = File.ReadAllBytes(path);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!tex.LoadImage(bytes, false))
                {
                    UnityEngine.Object.DestroyImmediate(tex);
                    return null;
                }

                if (tex.width != SpriteResolution || tex.height != SpriteResolution)
                {
                    UnityEngine.Object.DestroyImmediate(tex);
                    return null;
                }

                var pixels = tex.GetPixels32();
                UnityEngine.Object.DestroyImmediate(tex);
                return pixels;
            }
            catch
            {
                return null;
            }
        }

        static void WritePng(string path, Color32[] pixels, int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            var bytes = tex.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(tex);
            File.WriteAllBytes(path, bytes);
        }
    }

    /// <summary>
    /// Compose les sprites économiques sur le raster carte — déterministe, lecture seule.
    /// v1_076 : ancrage terrestre via masque IsLand (centroïde cellule souvent en mer).
    /// </summary>
    public static class MapSpriteComposer
    {
        /// <summary>
        /// true (défaut) = refuser tout sprite dont l'ancre n'est pas terrestre.
        /// false = comportement v1_073 (peint au centroïde même en mer) — mutation rouge V1076-A/C.
        /// </summary>
        public static bool LandGateEnabled { get; set; } = true;

        public static double LastComposeMilliseconds { get; private set; }
        public static int LastSpritesDrawn { get; private set; }
        public static int LastViewsSkeletonCount { get; private set; }
        public static int LastSeedOnLand { get; private set; }
        public static int LastSeedOnSea { get; private set; }
        public static int LastRelocatedToLand { get; private set; }
        public static int LastSkippedNoLand { get; private set; }
        public static int LastSpritesDrawnOnSea { get; private set; }
        public static string LastPositionOrigin { get; private set; } = "";
        public static IReadOnlyList<string> LastSkippedNames => SkippedNames;

        static readonly List<string> SkippedNames = new List<string>(64);

        public static void Compose(
            Color32[] pixels,
            MapSnapshotExporter.MapGeometry geo,
            EntityManager em,
            MapObservationLevel level,
            bool thematicLayer)
        {
            LastSpritesDrawn = 0;
            LastComposeMilliseconds = 0;
            LastViewsSkeletonCount = 0;
            LastSeedOnLand = 0;
            LastSeedOnSea = 0;
            LastRelocatedToLand = 0;
            LastSkippedNoLand = 0;
            LastSpritesDrawnOnSea = 0;
            SkippedNames.Clear();
            LastPositionOrigin = "";
            if (pixels == null || geo?.ViewsSkeleton == null)
                return;

            // Couches thématiques : masquer les sprites pour préserver les domaines v1_005.
            if (thematicLayer)
                return;

            if (!MapSpriteVisibility.ShowPrimaryGood(level))
                return;

            MapSpriteCatalog.EnsureReady();
            var sw = System.Diagnostics.Stopwatch.StartNew();

            LastViewsSkeletonCount = geo.ViewsSkeleton.Count;
            LastPositionOrigin = PilotMapProvider.Enabled
                ? "pilot_cell_seed_XY (Project lon/lat → world ; Id=cellId 1164..1400)"
                : "voronoi_province_centroid_XY (50 provinces)";

            var landCx = new int[geo.ViewsSkeleton.Count];
            var landCy = new int[geo.ViewsSkeleton.Count];
            var hasLand = new bool[geo.ViewsSkeleton.Count];
            BuildLandAnchors(geo, landCx, landCy, hasLand);

            var goodByProvince = LoadProvinceGoods(em);
            var activitiesByProvince = MapSpriteVisibility.ShowActivities(level)
                ? LoadProvinceActivities(em)
                : null;

            var size = MapSpriteVisibility.SpriteSizeFor(level);
            var alpha = level == MapObservationLevel.Province
                ? MapSpriteVisibility.ProvinceAlpha
                : MapSpriteVisibility.CountryAlpha;

            var drawn = 0;
            for (var i = 0; i < geo.ViewsSkeleton.Count; i++)
            {
                var view = geo.ViewsSkeleton[i];
                var provinceKey = view.Id;
                if (PilotMapProvider.Enabled &&
                    PilotMapProvider.TryGetProvinceIdForNavigation(view.Id, out var resolvedPid))
                    provinceKey = resolvedPid;

                if (!goodByProvince.TryGetValue(provinceKey, out var goodTag) ||
                    string.IsNullOrEmpty(goodTag))
                    continue;

                var seedX = WorldToPixelX(view.X, geo);
                var seedY = WorldToPixelY(view.Y, geo);
                var seedLand = IsLandAt(geo, seedX, seedY);
                if (seedLand)
                    LastSeedOnLand++;
                else
                    LastSeedOnSea++;

                int anchorX, anchorY;
                if (seedLand)
                {
                    anchorX = seedX;
                    anchorY = seedY;
                }
                else if (!LandGateEnabled)
                {
                    // Mutation rouge V1076-A/C : peindre au seed comme v1_073.
                    anchorX = seedX;
                    anchorY = seedY;
                    LastSpritesDrawnOnSea++;
                }
                else if (hasLand[i])
                {
                    anchorX = landCx[i];
                    anchorY = landCy[i];
                    LastRelocatedToLand++;
                }
                else
                {
                    LastSkippedNoLand++;
                    SkippedNames.Add(FormatViewSkipName(view));
                    continue;
                }

            if (LandGateEnabled && !IsLandAt(geo, anchorX, anchorY))
                LastSpritesDrawnOnSea++;

            var stem = MapSpriteCatalog.PropStemForGood(goodTag);
                if (stem != null &&
                    MapSpriteCatalog.TryGetSprite(stem, out var sprite))
                {
                    BlitSprite(pixels, geo.Width, geo.Height, sprite,
                        MapSpriteCatalog.SpriteResolution, anchorX, anchorY, size, alpha);
                    drawn++;
                }

                if (activitiesByProvince != null &&
                    activitiesByProvince.TryGetValue(provinceKey, out var acts) &&
                    acts.Count > 0)
                {
                    var actSize = MapSpriteVisibility.ActivitySizeFor(level);
                    for (var a = 0; a < acts.Count && a < 3; a++)
                    {
                        var aStem = MapSpriteCatalog.PropStemForGood(acts[a]);
                        if (aStem == null ||
                            !MapSpriteCatalog.TryGetSprite(aStem, out var aSprite))
                            continue;
                        var ox = (a - 1) * (actSize + 2);
                        var cx = anchorX + ox;
                        var cy = anchorY - size / 2 - actSize / 2 - 2;
                        BlitSprite(pixels, geo.Width, geo.Height, aSprite,
                            MapSpriteCatalog.SpriteResolution, cx, cy, actSize, alpha);
                        drawn++;
                    }

                    if (MapSpriteVisibility.ShowActivityBuilding(level))
                    {
                        var bStem = MapSpriteCatalog.BuildingStemForActivity(acts[0]);
                        if (MapSpriteCatalog.TryGetSprite(bStem, out var bSprite))
                        {
                            var cx = anchorX;
                            var cy = anchorY + size / 2 + 8;
                            var bSize = size - 4;
                            BlitSprite(pixels, geo.Width, geo.Height, bSprite,
                                MapSpriteCatalog.SpriteResolution, cx, cy, bSize, alpha);
                            drawn++;
                        }
                    }
                }
            }

            sw.Stop();
            LastComposeMilliseconds = sw.Elapsed.TotalMilliseconds;
            LastSpritesDrawn = drawn;
        }

        public static string FormatSkippedNames()
        {
            if (SkippedNames.Count == 0)
                return "(aucune)";
            var sb = new StringBuilder(SkippedNames.Count * 12);
            for (var i = 0; i < SkippedNames.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(SkippedNames[i]);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Compte les amas non-marins entièrement entourés de mer ouverte (mesure CTO).
        /// maxClusterPx filtre les continents (petits amas = sprites / îles).
        /// </summary>
        public static void MeasureOpenSeaClusters(
            Color32[] pixels,
            bool[] isLand,
            int width,
            int height,
            Color32 sea,
            int maxClusterPx,
            out int clusterCount,
            out int clusterPixels,
            out int openSeaPixels)
        {
            clusterCount = 0;
            clusterPixels = 0;
            openSeaPixels = 0;
            if (pixels == null || isLand == null || width <= 0 || height <= 0)
                return;

            var n = width * height;
            var seaTone = new bool[n];
            for (var i = 0; i < n && i < pixels.Length && i < isLand.Length; i++)
            {
                if (isLand[i])
                    continue;
                openSeaPixels++;
                var p = pixels[i];
                // Mer « pure » : proche de la couleur mer (sprites / îles peintes ≠ mer).
                var dr = p.r - sea.r;
                var dg = p.g - sea.g;
                var db = p.b - sea.b;
                seaTone[i] = dr * dr + dg * dg + db * db <= 48 * 48;
            }

            var visited = new bool[n];
            var qx = new int[n];
            var qy = new int[n];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var start = y * width + x;
                    if (visited[start] || isLand[start] || seaTone[start])
                        continue;

                    // Amas non-marin en zone !IsLand.
                    var head = 0;
                    var tail = 0;
                    qx[tail] = x;
                    qy[tail] = y;
                    tail++;
                    visited[start] = true;
                    var size = 0;
                    var touchesLandMask = false;
                    var touchesBorder = false;
                    while (head < tail)
                    {
                        var cx = qx[head];
                        var cy = qy[head];
                        head++;
                        size++;
                        if (cx == 0 || cy == 0 || cx == width - 1 || cy == height - 1)
                            touchesBorder = true;
                        for (var d = 0; d < 4; d++)
                        {
                            var nx = cx + (d == 0 ? 1 : d == 1 ? -1 : 0);
                            var ny = cy + (d == 2 ? 1 : d == 3 ? -1 : 0);
                            if (nx < 0 || ny < 0 || nx >= width || ny >= height)
                                continue;
                            var ni = ny * width + nx;
                            if (isLand[ni])
                            {
                                touchesLandMask = true;
                                continue;
                            }

                            if (visited[ni] || seaTone[ni])
                                continue;
                            visited[ni] = true;
                            qx[tail] = nx;
                            qy[tail] = ny;
                            tail++;
                        }
                    }

                    // Entièrement entouré de mer ouverte : pas de contact avec IsLand,
                    // pas de bordure image, taille bornée (sprites / îles).
                    if (!touchesLandMask && !touchesBorder && size > 0 && size <= maxClusterPx)
                    {
                        clusterCount++;
                        clusterPixels += size;
                    }
                }
            }
        }

        static void BuildLandAnchors(
            MapSnapshotExporter.MapGeometry geo,
            int[] landCx,
            int[] landCy,
            bool[] hasLand)
        {
            var n = geo.ViewsSkeleton.Count;
            var sumX = new long[n];
            var sumY = new long[n];
            var count = new int[n];
            var w = geo.Width;
            var h = geo.Height;
            var isLand = geo.IsLand;
            var provinceAt = geo.ProvinceAt;
            if (isLand == null || provinceAt == null)
                return;

            var nPix = Math.Min(isLand.Length, provinceAt.Length);
            nPix = Math.Min(nPix, w * h);
            for (var i = 0; i < nPix; i++)
            {
                if (!isLand[i])
                    continue;
                var vi = provinceAt[i];
                if (vi < 0 || vi >= n)
                    continue;
                sumX[vi] += i % w;
                sumY[vi] += i / w;
                count[vi]++;
            }

            for (var i = 0; i < n; i++)
            {
                if (count[i] <= 0)
                    continue;
                var mx = (int)(sumX[i] / count[i]);
                var my = (int)(sumY[i] / count[i]);
                // Le centroïde peut tomber dans un trou (golfe) : accrocher un pixel terre réel.
                if (IsLandAt(geo, mx, my) && provinceAt[my * w + mx] == i)
                {
                    landCx[i] = mx;
                    landCy[i] = my;
                    hasLand[i] = true;
                    continue;
                }

                long bestD = long.MaxValue;
                var found = false;
                var bx = mx;
                var by = my;
                for (var p = 0; p < nPix; p++)
                {
                    if (!isLand[p] || provinceAt[p] != i)
                        continue;
                    var px = p % w;
                    var py = p / w;
                    var dx = (long)px - mx;
                    var dy = (long)py - my;
                    var d = dx * dx + dy * dy;
                    if (d < bestD)
                    {
                        bestD = d;
                        bx = px;
                        by = py;
                        found = true;
                    }
                }

                if (!found)
                    continue;
                landCx[i] = bx;
                landCy[i] = by;
                hasLand[i] = true;
            }
        }

        static bool IsLandAt(MapSnapshotExporter.MapGeometry geo, int px, int py)
        {
            if (geo?.IsLand == null)
                return false;
            if (px < 0 || py < 0 || px >= geo.Width || py >= geo.Height)
                return false;
            var idx = py * geo.Width + px;
            return idx < geo.IsLand.Length && geo.IsLand[idx];
        }

        static string FormatViewSkipName(MapSnapshotExporter.ProvinceView view)
        {
            if (!string.IsNullOrEmpty(view.ProvinceName))
                return MapSnapshotExporter.SanitizeLabelText(view.ProvinceName);
            return "ID " + view.Id.ToString(CultureInfo.InvariantCulture);
        }

        static int WorldToPixelX(float worldX, MapSnapshotExporter.MapGeometry geo)
        {
            var rangeX = geo.MaxX - geo.MinX;
            return (int)math.floor((worldX - geo.MinX) / rangeX * geo.Width);
        }

        static int WorldToPixelY(float worldY, MapSnapshotExporter.MapGeometry geo)
        {
            // Aligné sur MapSnapshotExporter.WorldToPixel — nord@py0, y = −lat (v1_085).
            var rangeY = geo.MaxY - geo.MinY;
            if (MapSnapshotExporter.DebugLegacyMirrorWorldToPixelY)
                return (int)math.floor((geo.MaxY - worldY) / rangeY * geo.Height);
            return (int)math.floor((worldY - geo.MinY) / rangeY * geo.Height);
        }

        public static void BlitSprite(
            Color32[] dest, int destW, int destH,
            Color32[] sprite, int spriteRes,
            int centerX, int centerY, int size, byte alphaMul)
        {
            if (dest == null || sprite == null || size <= 0)
                return;

            var half = size / 2;
            for (var sy = 0; sy < size; sy++)
            {
                var dy = centerY - half + sy;
                if (dy < 0 || dy >= destH)
                    continue;
                var srcY = sy * spriteRes / size;
                for (var sx = 0; sx < size; sx++)
                {
                    var dx = centerX - half + sx;
                    if (dx < 0 || dx >= destW)
                        continue;
                    var srcX = sx * spriteRes / size;
                    var s = sprite[srcY * spriteRes + srcX];
                    if (s.a < 8)
                        continue;
                    var a = (byte)(s.a * alphaMul / 255);
                    if (a < 8)
                        continue;
                    var di = dy * destW + dx;
                    var d = dest[di];
                    var inv = 255 - a;
                    dest[di] = new Color32(
                        (byte)((s.r * a + d.r * inv) / 255),
                        (byte)((s.g * a + d.g * inv) / 255),
                        (byte)((s.b * a + d.b * inv) / 255),
                        255);
                }
            }
        }

        /// <summary>Icône bien pour panneaux (taille fixe, déterministe).</summary>
        public static void BlitGoodIcon(
            Color32[] dest, int destW, int destH,
            string goodTag, int x, int y, int size)
        {
            MapSpriteCatalog.EnsureReady();
            var stem = MapSpriteCatalog.PropStemForGood(goodTag);
            if (stem == null || !MapSpriteCatalog.TryGetSprite(stem, out var sprite))
                return;
            BlitSprite(dest, destW, destH, sprite, MapSpriteCatalog.SpriteResolution,
                x + size / 2, y + size / 2, size, 255);
        }

        static Dictionary<int, string> LoadProvinceGoods(EntityManager em)
        {
            var map = new Dictionary<int, string>(128);
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<ProvinceData>());
            using var data = q.ToComponentDataArray<ProvinceData>(Unity.Collections.Allocator.Temp);
            for (var i = 0; i < data.Length; i++)
            {
                var tag = data[i].GoodTag.ToString();
                if (!string.IsNullOrEmpty(tag))
                    map[data[i].ProvinceId] = tag;
            }

            return map;
        }

        static Dictionary<int, List<string>> LoadProvinceActivities(EntityManager em)
        {
            var map = new Dictionary<int, List<string>>(128);
            var goodTags = LoadGoodIdTags(em);
            using var q = em.CreateEntityQuery(
                ComponentType.ReadOnly<ProvinceData>(),
                ComponentType.ReadOnly<ProvincePhysicalActivity>());
            using var entities = q.ToEntityArray(Unity.Collections.Allocator.Temp);
            using var pdata = q.ToComponentDataArray<ProvinceData>(Unity.Collections.Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                if (!em.HasBuffer<ProvincePhysicalActivity>(entities[i]))
                    continue;
                var buf = em.GetBuffer<ProvincePhysicalActivity>(entities[i]);
                var list = new List<string>(buf.Length);
                for (var a = 0; a < buf.Length; a++)
                {
                    if (goodTags.TryGetValue(buf[a].GoodId, out var tag) &&
                        !string.IsNullOrEmpty(tag))
                        list.Add(tag);
                }

                list.Sort(StringComparer.Ordinal);
                map[pdata[i].ProvinceId] = list;
            }

            return map;
        }

        static Dictionary<int, string> LoadGoodIdTags(EntityManager em)
        {
            var map = new Dictionary<int, string>(32);
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<GoodData>());
            using var goods = q.ToComponentDataArray<GoodData>(Unity.Collections.Allocator.Temp);
            for (var i = 0; i < goods.Length; i++)
                map[goods[i].GoodId] = goods[i].Tag.ToString();
            return map;
        }
    }

    /// <summary>
    /// Fiche pays — agrégats réels uniquement (trésor, dette, armée, guerres, provinces).
    /// Même structure sectionnée que le panneau province v1_033.
    /// </summary>
    public static class CountryObservation
    {
        public struct Snapshot
        {
            public int CountryId;
            public string Tag;
            public string Name;
            public int CapitalProvinceId;
            public string CapitalName;
            public int ProvinceCount;
            public int Population;
            public float Prestige;
            public float Industrialization;
            public float Treasury;
            public float Debt;
            public float InterestRate;
            public float Income;
            public float Expenses;
            public float ProductionTaxRate;
            public float LawTaxModSum;
            public float EffectiveProductionTaxRate;
            public bool IsPlayerCountry;
            public float ArmyStrength;
            public int ActiveWars;
            public float Stability;
            public float Legitimacy;
            public List<string> WarLines;
            public List<string> EnactedLawLines;
            public List<ProvinceProdLine> Provinces;
            public string DetailBlock;
        }

        public struct ProvinceProdLine
        {
            public int ProvinceId;
            public string Name;
            public string GoodTag;
        }

        public static bool TryCapture(EntityManager em, int countryId, out Snapshot snap)
        {
            snap = default;
            Entity countryEntity = Entity.Null;
            CountryData cd = default;
            TreasuryData treasury = default;
            TaxPolicy taxPolicy = TaxPolicyLimits.Default();
            var found = false;
            var playerCountryId = PlayerControl.DefaultControlledCountryId;
            using (var pqCtrl = em.CreateEntityQuery(ComponentType.ReadOnly<PlayerControl>()))
            {
                if (!pqCtrl.IsEmptyIgnoreFilter)
                    playerCountryId = pqCtrl.GetSingleton<PlayerControl>().ControlledCountryId;
            }

            using (var q = em.CreateEntityQuery(
                       ComponentType.ReadOnly<CountryData>(),
                       ComponentType.ReadOnly<TreasuryData>()))
            using (var entities = q.ToEntityArray(Unity.Collections.Allocator.Temp))
            using (var countries = q.ToComponentDataArray<CountryData>(Unity.Collections.Allocator.Temp))
            using (var treasuries = q.ToComponentDataArray<TreasuryData>(Unity.Collections.Allocator.Temp))
            {
                for (var i = 0; i < countries.Length; i++)
                {
                    if (countries[i].CountryId != countryId)
                        continue;
                    countryEntity = entities[i];
                    cd = countries[i];
                    treasury = treasuries[i];
                    found = true;
                    break;
                }
            }

            if (!found || countryEntity == Entity.Null)
                return false;

            if (em.HasComponent<TaxPolicy>(countryEntity))
                taxPolicy = em.GetComponentData<TaxPolicy>(countryEntity);

            var provinces = new List<ProvinceProdLine>(32);
            using (var pq = em.CreateEntityQuery(
                       ComponentType.ReadOnly<ProvinceData>(),
                       ComponentType.ReadOnly<ProvinceOwnership>()))
            using (var pdata = pq.ToComponentDataArray<ProvinceData>(Unity.Collections.Allocator.Temp))
            using (var owns = pq.ToComponentDataArray<ProvinceOwnership>(Unity.Collections.Allocator.Temp))
            {
                for (var i = 0; i < pdata.Length; i++)
                {
                    if (owns[i].Owner != countryEntity)
                        continue;
                    provinces.Add(new ProvinceProdLine
                    {
                        ProvinceId = pdata[i].ProvinceId,
                        Name = ProvinceCoordinates.NameOf(pdata[i].ProvinceId) ?? "",
                        GoodTag = pdata[i].GoodTag.ToString()
                    });
                }
            }

            provinces.Sort((a, b) => a.ProvinceId.CompareTo(b.ProvinceId));

            float army = 0f;
            using (var aq = em.CreateEntityQuery(ComponentType.ReadOnly<ArmyData>()))
            using (var armies = aq.ToComponentDataArray<ArmyData>(Unity.Collections.Allocator.Temp))
            {
                for (var i = 0; i < armies.Length; i++)
                {
                    if (armies[i].Country == countryEntity)
                        army += armies[i].Strength;
                }
            }

            float stability = 0f;
            float legitimacy = 0f;
            if (em.HasComponent<GovernmentData>(countryEntity))
            {
                var gov = em.GetComponentData<GovernmentData>(countryEntity);
                stability = gov.Stability;
                legitimacy = gov.Legitimacy;
            }

            var warLines = new List<string>(8);
            var activeWars = 0;
            using (var wq = em.CreateEntityQuery(ComponentType.ReadOnly<WarData>()))
            using (var wars = wq.ToComponentDataArray<WarData>(Unity.Collections.Allocator.Temp))
            {
                for (var i = 0; i < wars.Length; i++)
                {
                    if (!wars[i].IsActive)
                        continue;
                    if (wars[i].Attacker != countryEntity && wars[i].Defender != countryEntity)
                        continue;
                    activeWars++;
                    var other = wars[i].Attacker == countryEntity
                        ? wars[i].Defender
                        : wars[i].Attacker;
                    var otherTag = "?";
                    if (other != Entity.Null && em.HasComponent<CountryData>(other))
                        otherTag = em.GetComponentData<CountryData>(other).Tag.ToString();
                    var role = wars[i].Attacker == countryEntity ? "ATK" : "DEF";
                    warLines.Add(
                        role + " vs " + otherTag +
                        " CB=" + wars[i].CasusBelli +
                        " SCR=" + wars[i].WarScore.ToString("0.0", CultureInfo.InvariantCulture));
                }
            }

            var capitalName = cd.CapitalProvinceId >= 0
                ? (ProvinceCoordinates.NameOf(cd.CapitalProvinceId) ?? "")
                : "";

            var enactedLines = new List<string>(8);
            if (em.HasBuffer<EnactedLaw>(countryEntity))
            {
                var buf = em.GetBuffer<EnactedLaw>(countryEntity);
                var rows = new List<(byte Cat, string Line)>(buf.Length);
                for (var i = 0; i < buf.Length; i++)
                {
                    var e = buf[i];
                    rows.Add(((byte)e.Category,
                        "CAT" + ((byte)e.Category).ToString(CultureInfo.InvariantCulture) +
                        " " + Sanitize(e.LawId.ToString()) +
                        " t=" + e.EnactedTick.ToString(CultureInfo.InvariantCulture)));
                }

                rows.Sort((a, b) => a.Cat.CompareTo(b.Cat));
                for (var i = 0; i < rows.Count; i++)
                    enactedLines.Add(rows[i].Line);
            }

            var lawTaxMod = LawTaxEffect.SumTaxModForCountry(em, countryEntity);
            var effectiveRate = LawTaxEffect.EffectiveProductionTaxRate(
                taxPolicy.ProductionTaxRate, lawTaxMod);

            var isPlayer = cd.CountryId == playerCountryId;
            var detail = BuildDetailBlock(
                cd, treasury, taxPolicy, lawTaxMod, effectiveRate, isPlayer,
                provinces.Count, army, activeWars, warLines, enactedLines,
                provinces, capitalName, stability, legitimacy);

            snap = new Snapshot
            {
                CountryId = cd.CountryId,
                Tag = cd.Tag.ToString(),
                Name = cd.Name.ToString(),
                CapitalProvinceId = cd.CapitalProvinceId,
                CapitalName = capitalName,
                ProvinceCount = provinces.Count,
                Population = cd.Population,
                Prestige = cd.Prestige,
                Industrialization = cd.Industrialization,
                Treasury = treasury.Balance,
                Debt = treasury.Debt,
                InterestRate = treasury.DebtInterestRate,
                Income = treasury.Income,
                Expenses = treasury.Expenses,
                ProductionTaxRate = taxPolicy.ProductionTaxRate,
                LawTaxModSum = lawTaxMod,
                EffectiveProductionTaxRate = effectiveRate,
                IsPlayerCountry = isPlayer,
                ArmyStrength = army,
                ActiveWars = activeWars,
                Stability = stability,
                Legitimacy = legitimacy,
                WarLines = warLines,
                EnactedLawLines = enactedLines,
                Provinces = provinces,
                DetailBlock = detail
            };
            return true;
        }

        static string BuildDetailBlock(
            CountryData cd,
            TreasuryData treasury,
            TaxPolicy taxPolicy,
            float lawTaxMod,
            float effectiveRate,
            bool isPlayerCountry,
            int provinceCount,
            float army,
            int activeWars,
            List<string> warLines,
            List<string> enactedLaws,
            List<ProvinceProdLine> provinces,
            string capitalName,
            float stability,
            float legitimacy)
        {
            var sb = new StringBuilder(3072);
            sb.Append("--- IDENTITY ---\n");
            sb.Append("COUNTRY ").Append(cd.CountryId)
                .Append(' ').Append(Sanitize(cd.Tag.ToString()))
                .Append("  ").Append(Sanitize(cd.Name.ToString())).Append('\n');
            if (isPlayerCountry)
                sb.Append("CONTROL PLAYER\n");
            else
                sb.Append("CONTROL AI (tax locked)\n");
            sb.Append("CAPITAL ");
            if (cd.CapitalProvinceId >= 0)
            {
                sb.Append(cd.CapitalProvinceId);
                if (!string.IsNullOrEmpty(capitalName))
                    sb.Append(' ').Append(Sanitize(capitalName));
            }
            else
                sb.Append("(none)");
            sb.Append('\n');
            sb.Append("PROVINCES ").Append(provinceCount.ToString(CultureInfo.InvariantCulture))
                .Append("  POP ").Append(cd.Population.ToString(CultureInfo.InvariantCulture))
                .Append('\n');

            sb.Append("--- TREASURY ---\n");
            sb.Append("GOLD   ").Append(Fmt1(treasury.Balance)).Append('\n');
            sb.Append("DEBT   ").Append(Fmt1(treasury.Debt))
                .Append("  RATE ").Append(Fmt3(treasury.DebtInterestRate)).Append('\n');
            sb.Append("INC    ").Append(Fmt1(treasury.Income))
                .Append("  EXP ").Append(Fmt1(treasury.Expenses)).Append('\n');

            sb.Append("--- TAX ---\n");
            sb.Append("RATE   ").Append(FmtTax(taxPolicy.ProductionTaxRate))
                .Append("  [").Append(FmtTax(TaxPolicyLimits.MinProductionTaxRate))
                .Append("..").Append(FmtTax(TaxPolicyLimits.MaxProductionTaxRate))
                .Append("]\n");
            sb.Append("LAWMOD ").Append(lawTaxMod.ToString("0.###", CultureInfo.InvariantCulture))
                .Append("  EFF ").Append(FmtTax(effectiveRate)).Append('\n');
            sb.Append("LAST   ").Append(Fmt1(treasury.Income))
                .Append("  (tax income last tick)\n");
            if (!isPlayerCountry)
                sb.Append("LOCKED — not your country\n");
            else
                sb.Append("PLAYER — use Tax-/Tax+ (intention)\n");

            sb.Append("--- LAWS ---\n");
            if (enactedLaws == null || enactedLaws.Count == 0)
                sb.Append("(none)\n");
            else
            {
                for (var i = 0; i < enactedLaws.Count && i < 6; i++)
                    sb.Append(Sanitize(enactedLaws[i])).Append('\n');
            }

            sb.Append("--- MILITARY ---\n");
            sb.Append("ARMY   ").Append(Fmt0(army)).Append('\n');
            sb.Append("WARS   ").Append(activeWars.ToString(CultureInfo.InvariantCulture)).Append('\n');
            if (warLines.Count == 0)
                sb.Append("(none)\n");
            else
            {
                for (var i = 0; i < warLines.Count && i < 4; i++)
                    sb.Append(Sanitize(warLines[i])).Append('\n');
            }

            sb.Append("--- STATUS ---\n");
            sb.Append("STAB   ").Append(Fmt3(stability))
                .Append("  LEG  ").Append(Fmt3(legitimacy)).Append('\n');
            sb.Append("PRESTIGE ").Append(Fmt1(cd.Prestige))
                .Append("  INDUS ").Append(Fmt2(cd.Industrialization)).Append('\n');

            sb.Append("--- PROVINCES PROD ---\n");
            var shown = 0;
            for (var i = 0; i < provinces.Count && shown < 12; i++)
            {
                sb.Append(PadLeft(provinces[i].ProvinceId.ToString(CultureInfo.InvariantCulture), 3))
                    .Append(' ')
                    .Append(PadRight(Sanitize(provinces[i].Name), 12))
                    .Append(' ')
                    .Append(Sanitize(provinces[i].GoodTag))
                    .Append('\n');
                shown++;
            }

            if (provinces.Count > shown)
                sb.Append("... +").Append((provinces.Count - shown).ToString(CultureInfo.InvariantCulture))
                    .Append(" more\n");
            if (shown == 0)
                sb.Append("(none)\n");

            return sb.ToString();
        }

        static string FmtTax(float rate) => HudValueFormatter.FormatTaxPercent(rate);

        /// <summary>Préserve Unicode pour panneaux UI Toolkit ; filtre seulement les contrôles.</summary>
        static string Sanitize(string s)
        {
            if (string.IsNullOrEmpty(s))
                return "";
            var sb = new StringBuilder(s.Length);
            for (var i = 0; i < s.Length; i++)
            {
                var c = s[i];
                if (c == '\n' || c == '\r')
                {
                    sb.Append(' ');
                    continue;
                }

                if (char.IsControl(c))
                    continue;
                sb.Append(c);
            }

            return sb.ToString();
        }

        static string Fmt0(float v) =>
            v.ToString("0", CultureInfo.InvariantCulture);

        static string Fmt1(float v) =>
            v.ToString("0.0", CultureInfo.InvariantCulture);

        static string Fmt2(float v) =>
            v.ToString("0.00", CultureInfo.InvariantCulture);

        static string Fmt3(float v) =>
            v.ToString("0.000", CultureInfo.InvariantCulture);

        static string PadLeft(string s, int w) =>
            s.Length >= w ? s : new string(' ', w - s.Length) + s;

        static string PadRight(string s, int w) =>
            s.Length >= w ? s.Substring(0, w) : s + new string(' ', w - s.Length);
    }
}
