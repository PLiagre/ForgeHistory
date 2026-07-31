using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace VictoriaGame.Presentation
{
    /// <summary>
    /// Géométrie de présentation : lit province_coordinates.json à chaque appel.
    /// Aucune coordonnée n'est codée en dur — le fichier est éditable à la main.
    /// </summary>
    public static class ProvinceCoordinates
    {
        [Serializable]
        public class ProjectionDef
        {
            public string type;
            public float mid_latitude;
            public string comment;
        }

        [Serializable]
        public class CoordinateEntry
        {
            public int id;
            public string name;
            public float lon;
            public float lat;
        }

        [Serializable]
        class CoordinatesFile
        {
            public ProjectionDef projection;
            public List<CoordinateEntry> coordinates;
        }

        public readonly struct Point
        {
            public readonly int Id;
            public readonly string Name;
            public readonly float Lon;
            public readonly float Lat;
            public readonly float X;
            public readonly float Y;

            public Point(int id, float lon, float lat, float x, float y)
                : this(id, null, lon, lat, x, y)
            {
            }

            public Point(int id, string name, float lon, float lat, float x, float y)
            {
                Id = id;
                Name = name ?? "";
                Lon = lon;
                Lat = lat;
                X = x;
                Y = y;
            }
        }

        /// <summary>
        /// Charge le JSON, projette chaque entrée. Relit le disque à chaque appel.
        /// </summary>
        public static List<Point> LoadProjected(out float midLatitude)
        {
            midLatitude = 0f;
            var path = Path.Combine(Application.streamingAssetsPath, "data", "province_coordinates.json");
            if (!File.Exists(path))
            {
                Debug.LogWarning($"ProvinceCoordinates: fichier introuvable: {path}");
                return new List<Point>();
            }

            var json = File.ReadAllText(path);
            var data = JsonUtility.FromJson<CoordinatesFile>(json);
            if (data?.coordinates == null)
            {
                Debug.LogWarning("ProvinceCoordinates: JSON invalide ou vide.");
                return new List<Point>();
            }

            if (data.projection == null)
            {
                Debug.LogWarning("ProvinceCoordinates: projection absente du JSON — mid_latitude=0.");
            }
            else
            {
                midLatitude = data.projection.mid_latitude;
            }

            var result = new List<Point>(data.coordinates.Count);
            for (var i = 0; i < data.coordinates.Count; i++)
            {
                var c = data.coordinates[i];
                Project(c.lon, c.lat, midLatitude, out var x, out var y);
                result.Add(new Point(c.id, c.name, c.lon, c.lat, x, y));
            }

            return result;
        }

        /// <summary>Nom d'affichage (province_coordinates.json) — lecture présentation seule.</summary>
        public static string NameOf(int provinceId)
        {
            var points = LoadProjected(out _);
            for (var i = 0; i < points.Count; i++)
            {
                if (points[i].Id == provinceId)
                    return points[i].Name ?? "";
            }

            return "";
        }

        /// <summary>
        /// Projection equirectangular : x = lon × cos(mid_latitude rad), y = −lat.
        /// mid_latitude est lu du JSON, jamais codé en dur côté appelant.
        /// </summary>
        public static void Project(float lon, float lat, float midLatitudeDegrees, out float x, out float y)
        {
            var cos = Mathf.Cos(midLatitudeDegrees * Mathf.Deg2Rad);
            x = lon * cos;
            y = -lat;
        }
    }
}
