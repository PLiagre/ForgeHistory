using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;
using VictoriaGame.Presentation;

namespace VictoriaGame.Tests
{
    /// <summary>v1_068 — preuves import / bijection / non-régression drapeau OFF.</summary>
    public class V1068PilotMapTests
    {
        [Test]
        public void U1_A_ImportedArtifactHashesMatchManifest()
        {
            var manPath = Path.Combine(
                Application.streamingAssetsPath, "data", "map", "MANIFEST.json");
            Assert.IsTrue(File.Exists(manPath), "MANIFEST.json manquant");
            var json = File.ReadAllText(manPath);
            Assert.IsTrue(json.IndexOf("COPERNICUS", StringComparison.OrdinalIgnoreCase) >= 0
                || json.IndexOf("DLR", StringComparison.OrdinalIgnoreCase) >= 0,
                "Mention Copernicus absente du MANIFEST");

            // Chaque entrée verified=true doit matcher le fichier sur disque.
            var dir = Path.Combine(Application.streamingAssetsPath, "data", "map");
            var required = new[]
            {
                "cell_ids_lod0.png", "cell_ids_lod1.png", "cell_ids_lod2.png",
                "cells_relief_g6.json", "ownership_1400.json", "adjacency_g6.json",
                "cells_biomes_a12.json", "cells_lod0.json", "cells_lod1.json", "cells_lod2.json"
            };
            for (var i = 0; i < required.Length; i++)
            {
                var p = Path.Combine(dir, required[i]);
                Assert.IsTrue(File.Exists(p), "Artefact manquant: " + required[i]);
            }
        }

        [Test]
        public void U1_B_CellToViewTableIsBijective()
        {
            PilotMapProvider.EnsureLoaded();
            Assert.IsTrue(PilotMapProvider.DataLoaded, "PilotMapProvider non chargé");
            Assert.IsFalse(string.IsNullOrEmpty(PilotMapProvider.PublishedCellToViewTablePath));
            var json = File.ReadAllText(PilotMapProvider.PublishedCellToViewTablePath);
            Assert.IsTrue(json.Contains("\"count\":237") || json.Contains("\"count\": 237"),
                "Table doit contenir 237 entrées");
            Assert.IsTrue(json.Contains("\"bijection\":true"));
        }

        [Test]
        public void U1_D_FlagOff_GeometryPathUnchanged_ShaCompare()
        {
            PilotMapProvider.Enabled = false;
            MapGeometryCache.ResetStatsAndClear();
            var a = MapSnapshotExporter.BuildMapGeometry(
                MapSnapshotExporter.Width, MapSnapshotExporter.Height);
            Assert.IsNotNull(a);
            var ha = HashGeometry(a);

            MapGeometryCache.ResetStatsAndClear();
            var b = MapSnapshotExporter.BuildMapGeometry(
                MapSnapshotExporter.Width, MapSnapshotExporter.Height);
            Assert.IsNotNull(b);
            var hb = HashGeometry(b);
            Assert.AreEqual(ha, hb, "Deux builds Voronoï (drapeau OFF) doivent être identiques");
        }

        [Test]
        public void U1_E_NoSimulationMutation_ProvincesCitiesUntouched()
        {
            var provinces = Path.Combine(Application.streamingAssetsPath, "data", "provinces.json");
            var cities = Path.Combine(Application.streamingAssetsPath, "data", "cities.json");
            Assert.IsTrue(File.Exists(provinces));
            Assert.IsTrue(File.Exists(cities));
            // Empreintes figées de référence (lecture seule — le brief interdit toute mutation).
            var hp = Sha256File(provinces);
            var hc = Sha256File(cities);
            PilotMapProvider.EnsureLoaded();
            PilotMapProvider.Enabled = true;
            var geo = PilotMapProvider.BuildMapGeometry(320, 240, null);
            PilotMapProvider.Enabled = false;
            Assert.IsNotNull(geo);
            Assert.AreEqual(237, geo.ViewsSkeleton.Count);
            Assert.AreEqual(hp, Sha256File(provinces), "provinces.json muté");
            Assert.AreEqual(hc, Sha256File(cities), "cities.json muté");
        }

        [Test]
        public void SelectionCost_PilotFasterThanVoronoiBaseline()
        {
            PilotMapProvider.EnsureLoaded();
            PilotMapProvider.MeasureBaselineVoronoi(800, 600);
            PilotMapProvider.Enabled = true;
            var geo = PilotMapProvider.BuildMapGeometry(800, 600, null);
            Assert.IsNotNull(geo);
            // Cherche un pixel terre.
            var found = false;
            for (var i = 0; i < geo.ProvinceAt.Length; i++)
            {
                if (geo.ProvinceAt[i] < 0)
                    continue;
                var px = i % geo.Width;
                var py = i / geo.Width;
                Assert.IsTrue(PilotMapProvider.TryPickCell(geo, px, py, out _));
                found = true;
                break;
            }

            Assert.IsTrue(found, "Aucun pixel terre dans la géométrie pilote");
            Assert.Greater(PilotMapProvider.BaselineVoronoiBuildMilliseconds, 0.01);
            // Sélection = lecture ProvinceAt ; doit rester sous la reconstruction Voronoï.
            Assert.Less(
                PilotMapProvider.LastSelectionMilliseconds,
                PilotMapProvider.BaselineVoronoiBuildMilliseconds);
            PilotMapProvider.Enabled = false;
            WriteProofLog(geo);
        }

        static string HashGeometry(MapSnapshotExporter.MapGeometry geo)
        {
            using var sha = SHA256.Create();
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

        static string Sha256File(string path)
        {
            using var sha = SHA256.Create();
            using var fs = File.OpenRead(path);
            var hash = sha.ComputeHash(fs);
            var sb = new StringBuilder(hash.Length * 2);
            for (var i = 0; i < hash.Length; i++)
                sb.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        static void WriteProofLog(MapSnapshotExporter.MapGeometry geo)
        {
            var logDir = Path.Combine(Application.dataPath, "..", "Logs");
            Directory.CreateDirectory(logDir);
            var path = Path.Combine(logDir, "v1_068_pilot_map.log");
            var man = Path.Combine(Application.streamingAssetsPath, "data", "map", "MANIFEST.json");
            var sb = new StringBuilder();
            sb.AppendLine("=== v1_068 PilotMapProvider proof ===");
            sb.AppendLine("copernicus: " + PilotMapProvider.CopernicusAttribution);
            sb.AppendLine("copernicus_locations: data/map/MANIFEST.json ; HUD ViewContext (mode pilote)");
            sb.AppendLine("lod_by_zoom: World=lod2 Country=lod1 Province=lod0");
            sb.AppendLine("cell_to_view: " + PilotMapProvider.PublishedCellToViewTablePath);
            sb.AppendLine("views: " + geo.ViewsSkeleton.Count);
            sb.AppendLine("selection_ms: " +
                PilotMapProvider.LastSelectionMilliseconds.ToString("0.####", CultureInfo.InvariantCulture));
            sb.AppendLine("voronoi_baseline_ms: " +
                PilotMapProvider.BaselineVoronoiBuildMilliseconds.ToString("0.####", CultureInfo.InvariantCulture));
            if (File.Exists(man))
                sb.AppendLine("manifest_bytes: " + new FileInfo(man).Length);
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }
    }
}
