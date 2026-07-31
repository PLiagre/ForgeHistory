using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using VictoriaGame.Presentation;

namespace VictoriaGame.Tests
{
    /// <summary>v1_071 — porte d'entrée, bascule F9, chargement paresseux, budget Z0.</summary>
    public class V1071PilotMapVisibilityTests
    {
        [TearDown]
        public void TearDown()
        {
            PilotMapProvider.Enabled = false;
            MapSnapshotExporter.ResetZoomScaleToNeutral();
            MapGeometryCache.ResetStatsAndClear();
        }

        [Test]
        public void V1071_A_SettingsEnablePilotGeometry()
        {
            Assert.IsTrue(
                File.Exists(Path.Combine(
                    Application.streamingAssetsPath, "data", "presentation_settings.json")),
                "presentation_settings.json manquant");

            PilotMapProvider.ApplyPresentationSettings(clearCache: true);
            Assert.IsTrue(PilotMapProvider.SettingsFileFound);
            Assert.IsTrue(PilotMapProvider.Enabled, "réglage true doit allumer Enabled");

            var geo = MapSnapshotExporter.BuildMapGeometry(320, 240);
            Assert.IsNotNull(geo);
            Assert.AreEqual(237, geo.ViewsSkeleton.Count,
                "rouge V1071-A: réglage true et géométrie restée Voronoï");
        }

        [Test]
        public void V1071_B_SettingsFalse_BitIdenticalVoronoi()
        {
            PilotMapProvider.SetEnabled(false, clearCache: true);
            MapGeometryCache.ResetStatsAndClear();
            var a = MapSnapshotExporter.BuildMapGeometry(
                MapSnapshotExporter.Width, MapSnapshotExporter.Height);
            var ha = HashGeometry(a);

            MapGeometryCache.ResetStatsAndClear();
            var b = MapSnapshotExporter.BuildMapGeometry(
                MapSnapshotExporter.Width, MapSnapshotExporter.Height);
            var hb = HashGeometry(b);
            Assert.AreEqual(ha, hb, "rouge V1071-B: un pixel change (Enabled=false)");
        }

        [Test]
        public void V1071_C_HotToggleClearsCache_ShaDiffer()
        {
            MapGeometryCache.ResetStatsAndClear();
            PilotMapProvider.SetEnabled(false, clearCache: true);
            var voronoi = MapGeometryCache.GetOrBuild(320, 240, null, out _);
            var shaV = HashGeometry(voronoi);

            // Mutation rouge : bascule Enabled SANS Clear → cache renvoie le Voronoï.
            PilotMapProvider.Enabled = true;
            var stale = MapGeometryCache.GetOrBuild(320, 240, null, out var hit);
            Assert.IsTrue(hit, "cache doit hit sans Clear");
            Assert.AreEqual(shaV, HashGeometry(stale),
                "rouge constaté: bascule sans Clear ⇒ SHA égaux");

            // Correct : Clear puis rebuild pilote.
            MapGeometryCache.Clear();
            var pilot = MapGeometryCache.GetOrBuild(320, 240, null, out _);
            Assert.AreEqual(237, pilot.ViewsSkeleton.Count);
            Assert.AreNotEqual(shaV, HashGeometry(pilot),
                "bascule avec Clear doit changer l'image");
        }

        [Test]
        public void V1071_D_UnrequestedLodNotLoaded()
        {
            PilotMapProvider.EnsureLoaded();
            PilotMapProvider.ReleaseLod(0);
            PilotMapProvider.ReleaseLod(1);
            PilotMapProvider.ReleaseLod(2);
            var loads = PilotMapProvider.LodTextureLoadCount;

            PilotMapProvider.Enabled = true;
            var geo = PilotMapProvider.BuildMapGeometry(160, 120, null, 2);
            Assert.IsNotNull(geo);
            Assert.IsTrue(PilotMapProvider.IsLodLoaded(2));
            Assert.IsFalse(PilotMapProvider.IsLodLoaded(0),
                "rouge V1071-D: LOD0 chargé alors que non demandé");
            Assert.IsFalse(PilotMapProvider.IsLodLoaded(1));
            Assert.AreEqual(loads + 1, PilotMapProvider.LodTextureLoadCount);
        }

        [Test]
        public void V1071_E_BlackCountersMatchV1070AfterAllLods()
        {
            PilotMapProvider.EnsureLoaded();
            PilotMapProvider.RescanAllBlackCounters();
            Assert.AreEqual(3616049, PilotMapProvider.LastBlackMissingDemPixels,
                "rouge V1071-E: scan partiel (missing DEM)");
            Assert.AreEqual(51354, PilotMapProvider.LastBlackElevZeroPixels,
                "rouge V1071-E: scan partiel (elev zero)");
        }

        [Test]
        public void V1071_ProvinceIdResolution_AndBudgetLog()
        {
            PilotMapProvider.EnsureLoaded();
            PilotMapProvider.MeasureProvinceIdResolution();
            Assert.AreEqual(237,
                PilotMapProvider.ResolvedProvinceIdCount + PilotMapProvider.UnresolvedProvinceIdCount);
            Assert.AreEqual(194, PilotMapProvider.ResolvedProvinceIdCount);
            Assert.AreEqual(43, PilotMapProvider.UnresolvedProvinceIdCount);

            var captureDir = Path.Combine(Application.dataPath, "..", "Captures", "v1_071");
            var logPath = Path.Combine(Application.dataPath, "..", "Logs", "v1_071_budget.log");
            var written = PilotMapProvider.WriteBudgetProofAndCaptures(captureDir, logPath);
            Assert.IsTrue(File.Exists(written));
            var log = File.ReadAllText(written);
            StringAssert.Contains("V1071-A", log);
            StringAssert.Contains("V1071-E", log);
            StringAssert.Contains("presentation_settings", log);
            StringAssert.Contains("z1_threshold", log);
            Assert.IsTrue(File.Exists(Path.Combine(captureDir, "play_world_pilot_on.png")));
            Assert.IsTrue(File.Exists(Path.Combine(captureDir, "play_world_pilot_off.png")));
            Assert.IsTrue(File.Exists(Path.Combine(captureDir, "play_country_pilot_on.png")));
            Assert.IsTrue(File.Exists(Path.Combine(captureDir, "play_province_pilot_on.png")));
        }

        [Test]
        public void V1071_HotToggleKey_IsF9()
        {
            Assert.AreEqual(KeyCode.F9, PilotMapProvider.HotToggleKey);
            Assert.IsFalse(PilotMapProvider.AutoReleaseUnusedLods,
                "auto-release non livré — déclaré");
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
    }
}
