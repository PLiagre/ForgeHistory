using Unity.Entities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Unity.Mathematics;
using UnityEngine;
using VictoriaGame.Core;
using VictoriaGame.World;

namespace VictoriaGame.Presentation
{
    /// <summary>
    /// Visibilité des marqueurs ville selon le niveau d'observation (v1_036 / v1_037).
    /// Monde : capitales majeures seulement (ne pas noyer la carte politique).
    /// Pays / Province : toutes les villes du viewport, taille selon population.
    /// </summary>
    public static class CityMarkerVisibility
    {
        /// <summary>Seuil monde recalibré v1_037 (pops urbaines ~11 % du monde).</summary>
        public const int WorldMinPopulation = 250;
        public const int WorldMarkerSize = 8;
        public const int CountryMarkerMinSize = 8;
        public const int CountryMarkerMaxSize = 14;
        public const int ProvinceMarkerMinSize = 12;
        public const int ProvinceMarkerMaxSize = 22;

        public static bool ShowAtLevel(MapObservationLevel level) =>
            level == MapObservationLevel.World ||
            level == MapObservationLevel.Country ||
            level == MapObservationLevel.Province ||
            level == MapObservationLevel.City;

        public static bool ShowLabels(MapObservationLevel level) =>
            level == MapObservationLevel.Province ||
            level == MapObservationLevel.Country ||
            level == MapObservationLevel.City;

        public static bool IncludeCity(MapObservationLevel level, in CityData city)
        {
            if (level == MapObservationLevel.World)
                return city.Status == CityStatus.Capital && city.Population >= WorldMinPopulation;
            return true;
        }

        public static int MarkerSize(MapObservationLevel level, int population)
        {
            if (level == MapObservationLevel.World)
                return WorldMarkerSize;
            var minS = level == MapObservationLevel.Province
                ? ProvinceMarkerMinSize
                : CountryMarkerMinSize;
            var maxS = level == MapObservationLevel.Province
                ? ProvinceMarkerMaxSize
                : CountryMarkerMaxSize;
            var t = population <= 0 ? 0f : math.log(1f + population) / math.log(1f + 450f);
            t = math.clamp(t, 0f, 1f);
            return minS + (int)math.floor(t * (maxS - minS));
        }

        public static string DocumentedPolicy() =>
            "WORLD: capitales pop>=" + WorldMinPopulation + " sprite=" + WorldMarkerSize + ". " +
            "COUNTRY: toutes villes sprite=" + CountryMarkerMinSize + "-" + CountryMarkerMaxSize +
            " (noms via MapLabelVisibility). " +
            "PROVINCE: toutes villes sprite=" + ProvinceMarkerMinSize + "-" + ProvinceMarkerMaxSize +
            " +noms. Coords=city_coordinates.json (pas OffsetFromCityId). " +
            MapLabelVisibility.DocumentedPolicy();
    }

    /// <summary>
    /// Dessin + hit-test des villes (lecture seule ECS). Positions lues dans
    /// city_coordinates.json ; sprites via le catalogue v1_034 (<see cref="MapSpriteComposer.BlitSprite"/>).
    /// </summary>
    public static class CityMarkerComposer
    {
        public struct DrawnMarker
        {
            public int CityId;
            public int PixelX;
            public int PixelY;
            public int Radius;
        }

        struct LabelCandidate
        {
            public CityData City;
            public int Px;
            public int Py;
            public int Size;
            public string Label;
        }

        static readonly List<DrawnMarker> _lastDrawn = new List<DrawnMarker>(128);
        static readonly List<int> _enqueuedCityIds = new List<int>(64);
        public static IReadOnlyList<DrawnMarker> LastDrawn => _lastDrawn;
        public static int LastMarkersDrawn { get; private set; }
        public static int LastLabelsDrawn { get; private set; }
        public static int LastLabelsMoved { get; private set; }
        public static int LastLabelsOmitted { get; private set; }
        public static int LastMissingCoordinates { get; private set; }
        public static double LastComposeMilliseconds { get; private set; }

        /// <summary>
        /// Après MapLabelLayout.Flush : aligne les compteurs ville sur LastPlaced.
        /// </summary>
        public static void SyncStatsAfterFlush()
        {
            if (!MapLabelLayout.IsActive || !MapLabelLayout.UseImportanceQueue)
                return;

            var drawn = 0;
            var moved = 0;
            var placed = MapLabelLayout.LastPlaced;
            for (var i = 0; i < placed.Count; i++)
            {
                if (placed[i].Kind != MapLabelKind.City)
                    continue;
                drawn++;
                if (placed[i].Moved)
                    moved++;
            }

            LastLabelsDrawn = drawn;
            LastLabelsMoved = moved;
            LastLabelsOmitted = math.max(0, _enqueuedCityIds.Count - drawn);
        }

        public static void Compose(
            Color32[] pixels,
            MapSnapshotExporter.MapGeometry geo,
            EntityManager em,
            MapObservationLevel level,
            int filterProvinceId = -1,
            int filterCountryId = -1)
        {
            _lastDrawn.Clear();
            _enqueuedCityIds.Clear();
            LastMarkersDrawn = 0;
            LastLabelsDrawn = 0;
            LastLabelsMoved = 0;
            LastLabelsOmitted = 0;
            LastMissingCoordinates = 0;
            LastComposeMilliseconds = 0;
            if (pixels == null || geo == null || !CityMarkerVisibility.ShowAtLevel(level))
                return;

            var sw = System.Diagnostics.Stopwatch.StartNew();
            MapSpriteCatalog.EnsureCitySprites();
            var provinceOwnerCountry = BuildProvinceOwnerCountry(em);
            var nationalCapitalProvinces = BuildNationalCapitalProvinces(em);
            var cities = new List<CityData>(128);
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<CityData>()))
            using (var arr = q.ToComponentDataArray<CityData>(Unity.Collections.Allocator.Temp))
            {
                for (var i = 0; i < arr.Length; i++)
                    cities.Add(arr[i]);
            }

            // Ordre stable CityId pour le dessin des marqueurs (jamais Entity.Index).
            cities.Sort((a, b) => a.CityId.CompareTo(b.CityId));

            var labelCandidates = new List<LabelCandidate>(cities.Count);
            var missing = 0;
            var labelsDrawnBefore = MapLabelLayout.IsActive ? MapLabelLayout.LastDrawn : 0;
            var labelsMovedBefore = MapLabelLayout.IsActive ? MapLabelLayout.LastMoved : 0;
            var labelsOmittedBefore = MapLabelLayout.IsActive ? MapLabelLayout.LastOmitted : 0;

            // Pass 1 — marqueurs + bâtiments + réservation d'icônes (ordre CityId).
            for (var i = 0; i < cities.Count; i++)
            {
                var city = cities[i];
                if (!CityMarkerVisibility.IncludeCity(level, city))
                    continue;
                if (filterProvinceId >= 0 && city.ProvinceId != filterProvinceId)
                    continue;
                if (filterCountryId >= 0)
                {
                    if (!provinceOwnerCountry.TryGetValue(city.ProvinceId, out var cid) ||
                        cid != filterCountryId)
                        continue;
                }

                if (!CityCoordinates.TryGet(city.CityId, out var pt))
                {
                    missing++;
                    Debug.LogWarning(
                        $"CityMarkerComposer: ville id={city.CityId} '{city.Name}' " +
                        "sans entrée city_coordinates.json — non dessinée.");
                    continue;
                }

                var px = WorldToPixelX(pt.X, geo);
                var py = WorldToPixelY(pt.Y, geo);
                var size = CityMarkerVisibility.MarkerSize(level, city.Population);
                var stem = MapSpriteCatalog.CityStemForStatus(city.Status);
                if (MapSpriteCatalog.TryGetSprite(stem, out var sprite))
                {
                    MapSpriteComposer.BlitSprite(
                        pixels, geo.Width, geo.Height, sprite,
                        MapSpriteCatalog.SpriteResolution, px, py, size, 255);
                }

                if (MapLabelLayout.IsActive)
                    MapLabelLayout.ReserveCentered(px, py, size);

                // v1_038/v1_039 — bâtiments près de la ville ; inscrits dans la réservation.
                if (level == MapObservationLevel.Province || level == MapObservationLevel.City)
                {
                    DrawBuildingsNearCity(em, pixels, geo, city.CityId, px, py, size);
                }

                if (MapLabelVisibility.IncludeCityLabel(level, city))
                {
                    var label = MapSnapshotExporter.SanitizeLabelText(city.Name.ToString());
                    if (!string.IsNullOrEmpty(label))
                    {
                        labelCandidates.Add(new LabelCandidate
                        {
                            City = city,
                            Px = px,
                            Py = py,
                            Size = size,
                            Label = label,
                        });
                    }
                }

                _lastDrawn.Add(new DrawnMarker
                {
                    CityId = city.CityId,
                    PixelX = px,
                    PixelY = py,
                    Radius = size / 2 + 3,
                });
            }

            // Pass 2 — étiquettes : file d'importance (v1_041) ou tri local immédiat (v1_040).
            labelCandidates.Sort((a, b) =>
                MapLabelVisibility.CompareCityImportance(a.City, b.City));

            var fg = new Color32(250, 245, 230, 255);
            var halo = new Color32(20, 20, 20, 255);
            var labelsDrawn = 0;
            var labelsMoved = 0;
            var labelsOmitted = 0;

            for (var i = 0; i < labelCandidates.Count; i++)
            {
                var c = labelCandidates[i];
                var isNational = c.City.Status == CityStatus.Capital &&
                                 c.City.ProvinceId >= 0 &&
                                 nationalCapitalProvinces.Contains(c.City.ProvinceId);
                var isProvinceCapital = c.City.Status == CityStatus.Capital && !isNational;
                var rank = MapLabelImportance.RankForCity(
                    isNational, isProvinceCapital, c.City.Population);
                var isProtected = MapLabelImportance.IsProtectedRank(
                    rank, c.City.Status == CityStatus.Capital);

                if (MapLabelLayout.IsActive && !MapLabelLayout.LegacyCityLabels)
                {
                    if (MapLabelLayout.UseImportanceQueue)
                    {
                        MapLabelLayout.Enqueue(
                            c.Label, c.Px, c.Py, c.Size, fg, halo,
                            MapLabelKind.City, c.City.CityId,
                            rank, c.City.Population,
                            MapLabelVisibility.StatusRank(c.City.Status),
                            c.City.CityId,
                            useAnchorSlots: true,
                            isProtected: isProtected);
                        _enqueuedCityIds.Add(c.City.CityId);
                    }
                    else if (MapLabelLayout.TryPlaceAroundAnchor(
                            pixels, c.Label, c.Px, c.Py, c.Size, fg, halo,
                            MapLabelKind.City, c.City.CityId,
                            out var slot, out _))
                    {
                        labelsDrawn++;
                        if (slot != MapLabelSlot.Below)
                            labelsMoved++;
                    }
                    else
                        labelsOmitted++;
                }
                else
                {
                    // Legacy : centré sous le marqueur, sans test de recouvrement.
                    var tw = MapSnapshotExporter.MeasureBitmapText(c.Label);
                    var ox = c.Px - tw / 2;
                    var oy = c.Py + c.Size / 2 + 2;
                    MapSnapshotExporter.DrawBitmapText(pixels, c.Label, ox, oy, fg, halo);
                    labelsDrawn++;
                }
            }

            sw.Stop();
            LastComposeMilliseconds = sw.Elapsed.TotalMilliseconds;
            LastMarkersDrawn = _lastDrawn.Count;
            if (MapLabelLayout.IsActive && MapLabelLayout.UseImportanceQueue &&
                !MapLabelLayout.LegacyCityLabels)
            {
                // Flush n'a pas encore tourné — SyncStatsAfterFlush complétera.
                LastLabelsDrawn = 0;
                LastLabelsMoved = 0;
                LastLabelsOmitted = 0;
            }
            else if (MapLabelLayout.IsActive)
            {
                // Compteurs ville = delta session (provinces déjà comptées avant).
                LastLabelsDrawn = MapLabelLayout.LastDrawn - labelsDrawnBefore;
                LastLabelsMoved = MapLabelLayout.LastMoved - labelsMovedBefore;
                LastLabelsOmitted = MapLabelLayout.LastOmitted - labelsOmittedBefore;
                if (LastLabelsDrawn < 0) LastLabelsDrawn = labelsDrawn;
                if (LastLabelsMoved < 0) LastLabelsMoved = labelsMoved;
                if (LastLabelsOmitted < 0) LastLabelsOmitted = labelsOmitted;
            }
            else
            {
                LastLabelsDrawn = labelsDrawn;
                LastLabelsMoved = labelsMoved;
                LastLabelsOmitted = labelsOmitted;
            }

            LastMissingCoordinates = missing;
        }

        static HashSet<int> BuildNationalCapitalProvinces(EntityManager em)
        {
            var set = new HashSet<int>();
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<CountryData>());
            using var arr = q.ToComponentDataArray<CountryData>(Unity.Collections.Allocator.Temp);
            for (var i = 0; i < arr.Length; i++)
            {
                var cap = arr[i].CapitalProvinceId;
                // Sentinelle -1 (CountryInitSystem.InvalidCapitalProvinceId) : ignorer.
                if (cap >= 0)
                    set.Add(cap);
            }

            return set;
        }

        static void DrawBuildingsNearCity(
            EntityManager em,
            Color32[] pixels,
            MapSnapshotExporter.MapGeometry geo,
            int cityId,
            int cityPx,
            int cityPy,
            int citySize)
        {
            var list = new List<VictoriaGame.Economy.BuildingData>(8);
            using (var q = em.CreateEntityQuery(
                       ComponentType.ReadOnly<VictoriaGame.Economy.BuildingData>()))
            using (var arr = q.ToComponentDataArray<VictoriaGame.Economy.BuildingData>(
                       Unity.Collections.Allocator.Temp))
            {
                for (var i = 0; i < arr.Length; i++)
                {
                    if (arr[i].CityId == cityId)
                        list.Add(arr[i]);
                }
            }

            if (list.Count == 0)
                return;

            list.Sort((a, b) => a.BuildingId.CompareTo(b.BuildingId));
            var bSize = math.max(6, citySize * 2 / 3);
            var maxDraw = math.min(4, list.Count);
            for (var i = 0; i < maxDraw; i++)
            {
                var b = list[i];
                var stem = MapSpriteCatalog.BuildingStemForType(b.Type);
                if (!MapSpriteCatalog.TryGetSprite(stem, out var sprite))
                    continue;
                var ox = (i - (maxDraw - 1) * 0.5f) * (bSize + 2);
                var bx = cityPx + (int)math.round(ox);
                var by = cityPy + citySize / 2 + bSize / 2 + 4;
                var alpha = b.IsComplete != 0 ? (byte)230 : (byte)110;
                MapSpriteComposer.BlitSprite(
                    pixels, geo.Width, geo.Height, sprite,
                    MapSpriteCatalog.SpriteResolution, bx, by, bSize, alpha);
                if (MapLabelLayout.IsActive)
                    MapLabelLayout.ReserveCentered(bx, by, bSize);
            }
        }

        public static bool TryHit(int px, int py, out int cityId)
        {
            cityId = -1;
            var bestDist = int.MaxValue;
            for (var i = 0; i < _lastDrawn.Count; i++)
            {
                var m = _lastDrawn[i];
                var dx = px - m.PixelX;
                var dy = py - m.PixelY;
                var d2 = dx * dx + dy * dy;
                var r = m.Radius;
                if (d2 <= r * r && d2 < bestDist)
                {
                    bestDist = d2;
                    cityId = m.CityId;
                }
            }

            return cityId >= 0;
        }

        /// <summary>
        /// Pixel carte pour une coordonnée ville projetée (même conversion que le composeur).
        /// </summary>
        public static void WorldToPixel(
            float worldX, float worldY, MapSnapshotExporter.MapGeometry geo,
            out int px, out int py)
        {
            px = WorldToPixelX(worldX, geo);
            py = WorldToPixelY(worldY, geo);
        }

        static Dictionary<int, int> BuildProvinceOwnerCountry(EntityManager em)
        {
            var map = new Dictionary<int, int>(64);
            using var q = em.CreateEntityQuery(
                ComponentType.ReadOnly<ProvinceData>(),
                ComponentType.ReadOnly<ProvinceOwnership>());
            using var pdata = q.ToComponentDataArray<ProvinceData>(Unity.Collections.Allocator.Temp);
            using var owns = q.ToComponentDataArray<ProvinceOwnership>(Unity.Collections.Allocator.Temp);
            for (var i = 0; i < pdata.Length; i++)
            {
                var cid = -1;
                var owner = owns[i].Owner;
                if (owner != Entity.Null && em.HasComponent<CountryData>(owner))
                    cid = em.GetComponentData<CountryData>(owner).CountryId;
                map[pdata[i].ProvinceId] = cid;
            }

            return map;
        }

        static int WorldToPixelX(float worldX, MapSnapshotExporter.MapGeometry geo)
        {
            var rangeX = geo.MaxX - geo.MinX;
            return (int)math.floor((worldX - geo.MinX) / rangeX * geo.Width);
        }

        static int WorldToPixelY(float worldY, MapSnapshotExporter.MapGeometry geo)
        {
            // Convention buffer carte (v1_077 / v1_085) : nord@py0.
            // Project : y = −lat ⇒ worldY DÉCROÎT vers le nord (MinY = nord, MaxY = sud).
            // py = (worldY − MinY) / rangeY × Height : py croît vers le sud / bas d'écran.
            // DebugLegacyMirrorWorldToPixelY : ancienne formule maxY−worldY (miroir N-S).
            var rangeY = geo.MaxY - geo.MinY;
            if (MapSnapshotExporter.DebugLegacyMirrorWorldToPixelY)
                return (int)math.floor((geo.MaxY - worldY) / rangeY * geo.Height);
            return (int)math.floor((worldY - geo.MinY) / rangeY * geo.Height);
        }
    }

    /// <summary>Fiche ville lecture seule — même format de panneau que v1_033/v1_034.</summary>
    public static class CityObservation
    {
        public struct Snapshot
        {
            public int CityId;
            public string Name;
            public int ProvinceId;
            public string ProvinceName;
            public string CountryTag;
            public int Population;
            public CityStatus Status;
            public string DetailBlock;
        }

        public static bool TryCapture(EntityManager em, int cityId, out Snapshot snap)
        {
            snap = default;
            if (cityId < 0)
                return false;

            CityData city = default;
            var found = false;
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<CityData>()))
            using (var arr = q.ToComponentDataArray<CityData>(Unity.Collections.Allocator.Temp))
            {
                for (var i = 0; i < arr.Length; i++)
                {
                    if (arr[i].CityId != cityId)
                        continue;
                    city = arr[i];
                    found = true;
                    break;
                }
            }

            if (!found)
                return false;

            var provinceName = ProvinceCoordinates.NameOf(city.ProvinceId) ?? "";
            var countryTag = "";
            if (city.Province != Entity.Null &&
                em.Exists(city.Province) &&
                em.HasComponent<ProvinceOwnership>(city.Province))
            {
                var owner = em.GetComponentData<ProvinceOwnership>(city.Province).Owner;
                if (owner != Entity.Null && em.HasComponent<CountryData>(owner))
                    countryTag = em.GetComponentData<CountryData>(owner).Tag.ToString();
            }

            var lonLat = "";
            if (CityCoordinates.TryGet(city.CityId, out var pt))
                lonLat = pt.Lon.ToString("0.####", CultureInfo.InvariantCulture) + "/" +
                         pt.Lat.ToString("0.####", CultureInfo.InvariantCulture);

            var sb = new StringBuilder(512);
            sb.AppendLine("=== CITY ===");
            sb.AppendLine("IDENTITY");
            sb.Append("  NAME ").Append(city.Name.ToString()).AppendLine();
            sb.Append("  ID ").Append(city.CityId.ToString(CultureInfo.InvariantCulture)).AppendLine();
            sb.Append("  STATUS ").Append(city.Status.ToString().ToUpperInvariant()).AppendLine();
            sb.AppendLine("LOCATION");
            sb.Append("  PROVINCE ").Append(city.ProvinceId.ToString(CultureInfo.InvariantCulture));
            if (!string.IsNullOrEmpty(provinceName))
                sb.Append(' ').Append(provinceName);
            sb.AppendLine();
            sb.Append("  COUNTRY ").Append(string.IsNullOrEmpty(countryTag) ? "?" : countryTag).AppendLine();
            if (!string.IsNullOrEmpty(lonLat))
                sb.Append("  LONLAT ").Append(lonLat).AppendLine();
            sb.AppendLine("POPULATION");
            sb.Append("  URBAN ").Append(city.Population.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine(" (share of provincial pops — not additive)");

            // v1_038 — bâtiments de la ville (lecture seule).
            sb.AppendLine("BUILDINGS");
            var buildingLines = 0;
            using (var bq = em.CreateEntityQuery(ComponentType.ReadOnly<VictoriaGame.Economy.BuildingData>()))
            using (var buildings = bq.ToComponentDataArray<VictoriaGame.Economy.BuildingData>(
                       Unity.Collections.Allocator.Temp))
            {
                // Tri déterministe BuildingId.
                var idxs = new int[buildings.Length];
                for (var i = 0; i < idxs.Length; i++) idxs[i] = i;
                System.Array.Sort(idxs, (a, b) => buildings[a].BuildingId.CompareTo(buildings[b].BuildingId));
                for (var k = 0; k < idxs.Length; k++)
                {
                    var b = buildings[idxs[k]];
                    if (b.CityId != cityId)
                        continue;
                    sb.Append("  ").Append(b.Type.ToString().ToUpperInvariant());
                    sb.Append(" id=").Append(b.BuildingId.ToString(CultureInfo.InvariantCulture));
                    sb.Append(b.IsComplete != 0 ? " COMPLETE" : " SITE");
                    sb.Append(" cap=").Append(b.CapacityContribution.ToString("0.#", CultureInfo.InvariantCulture));
                    sb.AppendLine();
                    buildingLines++;
                    if (buildingLines >= 12)
                        break;
                }
            }

            if (buildingLines == 0)
                sb.AppendLine("  (none)");

            snap = new Snapshot
            {
                CityId = city.CityId,
                Name = city.Name.ToString(),
                ProvinceId = city.ProvinceId,
                ProvinceName = provinceName,
                CountryTag = countryTag,
                Population = city.Population,
                Status = city.Status,
                DetailBlock = sb.ToString(),
            };
            return true;
        }
    }
}
