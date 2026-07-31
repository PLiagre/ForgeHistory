using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace VictoriaGame.Presentation
{
    /// <summary>
    /// Géométrie de présentation des villes : lit city_coordinates.json.
    /// Réutilise <see cref="ProvinceCoordinates.Project"/> — jamais une seconde projection.
    /// Une ville sans entrée → avertissement, pas de position inventée.
    /// </summary>
    public static class CityCoordinates
    {
        [Serializable]
        class CoordinateEntry
        {
            public int id;
            public string name;
            public float lon;
            public float lat;
        }

        [Serializable]
        class CoordinatesFile
        {
            public ProvinceCoordinates.ProjectionDef projection;
            public List<CoordinateEntry> coordinates;
        }

        static Dictionary<int, ProvinceCoordinates.Point> _cache;
        static float _cachedMidLatitude;
        static string _cacheStamp;

        /// <summary>Charge et projette. Relit le disque si le fichier change.</summary>
        public static List<ProvinceCoordinates.Point> LoadProjected(out float midLatitude)
        {
            EnsureCache();
            midLatitude = _cachedMidLatitude;
            var list = new List<ProvinceCoordinates.Point>(_cache.Count);
            // Ordre stable CityId.
            var ids = new List<int>(_cache.Keys);
            ids.Sort();
            for (var i = 0; i < ids.Count; i++)
                list.Add(_cache[ids[i]]);
            return list;
        }

        public static bool TryGet(int cityId, out ProvinceCoordinates.Point point)
        {
            EnsureCache();
            return _cache.TryGetValue(cityId, out point);
        }

        public static string NameOf(int cityId)
        {
            if (TryGet(cityId, out var p))
                return p.Name ?? "";
            return "";
        }

        public static int Count
        {
            get
            {
                EnsureCache();
                return _cache.Count;
            }
        }

        public static void InvalidateCache()
        {
            _cache = null;
            _cacheStamp = null;
        }

        static void EnsureCache()
        {
            var path = Path.Combine(Application.streamingAssetsPath, "data", "city_coordinates.json");
            var stamp = File.Exists(path)
                ? (path + "|" + new FileInfo(path).Length + "|" + File.GetLastWriteTimeUtc(path).Ticks)
                : "missing";
            if (_cache != null && stamp == _cacheStamp)
                return;

            _cache = new Dictionary<int, ProvinceCoordinates.Point>(128);
            _cachedMidLatitude = 0f;
            _cacheStamp = stamp;

            if (!File.Exists(path))
            {
                Debug.LogWarning($"CityCoordinates: fichier introuvable: {path}");
                return;
            }

            // mid_latitude : même source que les provinces (fichier ville peut la répéter,
            // mais on force la lecture province pour garantir l'identité de projection).
            ProvinceCoordinates.LoadProjected(out _cachedMidLatitude);

            var json = File.ReadAllText(path);
            var data = JsonUtility.FromJson<CoordinatesFile>(json);
            if (data?.coordinates == null)
            {
                Debug.LogWarning("CityCoordinates: JSON invalide ou vide.");
                return;
            }

            for (var i = 0; i < data.coordinates.Count; i++)
            {
                var c = data.coordinates[i];
                ProvinceCoordinates.Project(c.lon, c.lat, _cachedMidLatitude, out var x, out var y);
                _cache[c.id] = new ProvinceCoordinates.Point(c.id, c.name, c.lon, c.lat, x, y);
            }
        }
    }
}
