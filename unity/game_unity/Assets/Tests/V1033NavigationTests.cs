using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;
using VictoriaGame.Core;
using VictoriaGame.Presentation;
using VictoriaGame.World;
using Debug = UnityEngine.Debug;

namespace VictoriaGame.Tests
{
    /// <summary>
    /// Point d'entrée batchmode SANS -nographics :
    /// -executeMethod VictoriaGame.Tests.V1033NavigationBatchRunner.Run
    /// </summary>
    public static class V1033NavigationBatchRunner
    {
        public static void Run()
        {
            V1033NavigationTests.RunAndWriteArtifacts();
            Debug.Log("V1033NavigationBatchRunner: DONE");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
        }
    }

    /// <summary>
    /// v1_033 — clic→province, cache transparent, panneau lisible, preuves &lt;100 ms.
    /// </summary>
    [TestFixture]
    public class V1033NavigationTests
    {
        const uint Seed = 42195u;
        const int CaptureTick = 1000;
        const int BourgogneProvinceId = 6;

        [Test]
        public void V1033_ClickPick_Uses_ProvinceAt_WithoutRender()
        {
            // Géométrie synthétique minimale — pas de BuildMapGeometry lourd.
            var geo = new MapSnapshotExporter.MapGeometry
            {
                Width = 4,
                Height = 2,
                MinX = 0, MaxX = 4, MinY = 0, MaxY = 2,
                IsLand = new[] { true, true, false, true, true, true, true, true },
                ProvinceAt = new[] { 0, 0, -1, 1, 0, 1, 1, 1 },
                ViewsSkeleton = new System.Collections.Generic.List<MapSnapshotExporter.ProvinceView>
                {
                    new MapSnapshotExporter.ProvinceView { Id = 10, ProvinceName = "ALPHA" },
                    new MapSnapshotExporter.ProvinceView { Id = 20, ProvinceName = "BETA" }
                }
            };

            Assert.IsTrue(MapClickPicker.TryPickProvinceId(geo, 0, 0, out var id0));
            Assert.AreEqual(10, id0);
            Assert.IsTrue(MapClickPicker.TryPickProvinceId(geo, 3, 0, out var id1));
            Assert.AreEqual(20, id1);
            Assert.IsFalse(MapClickPicker.TryPickProvinceId(geo, 2, 0, out _)); // mer

            Assert.IsTrue(MapClickPicker.TryPickProvinceName(geo, 0, 1, out var id2, out var name));
            Assert.AreEqual(10, id2);
            Assert.AreEqual("ALPHA", name);

            Assert.IsTrue(MapClickPicker.TryLocalToTexturePixel(
                50f, 25f, 100f, 50f, 100, 50, uiYDown: true, out var px, out var py));
            Assert.AreEqual(50, px);
            Assert.AreEqual(24, py); // y-down → bas texture
        }

        [Test]
        public void V1033_Navigation_And_PanZoom_Deterministic()
        {
            var world = MapViewportNavigation.FitAspectWithMargin(
                -10f, 40f, -60f, -30f, 0.05f, 1600f / 1200f);
            var countryWin = MapViewportNavigation.FitAspectWithMargin(
                0f, 8f, -50f, -42f, 0.18f, 1600f / 1200f);

            var a = MapViewportNavigation.CreateWorld(world);
            Assert.IsTrue(MapViewportNavigation.TrySelectCountry(
                a, Entity.Null, 0, countryWin, out var b));
            Assert.AreEqual(MapObservationLevel.Country, b.Level);

            var panned = MapViewportNavigation.PanWindow(b.Window, 1f, -0.5f, world);
            Assert.AreNotEqual(b.Window, panned);
            Assert.AreEqual(b.Window.Width, panned.Width, 0.0001f);

            var zoomed = MapViewportNavigation.ZoomWindowAt(
                b.Window, (b.Window.MinX + b.Window.MaxX) * 0.5f,
                (b.Window.MinY + b.Window.MaxY) * 0.5f, 0.85f, world);
            Assert.Less(zoomed.Width, b.Window.Width);

            // Déterminisme
            var panned2 = MapViewportNavigation.PanWindow(b.Window, 1f, -0.5f, world);
            Assert.AreEqual(panned, panned2);
        }

        [Test]
        public void V1033_Navigation_Captures_Cache_Panel() => RunAndWriteArtifacts();

        public static void RunAndWriteArtifacts()
        {
            var navDir = Path.Combine(Application.dataPath, "..", "Logs", "v1_033_navigation");
            var navLogPath = Path.Combine(Application.dataPath, "..", "Logs", "v1_033_navigation.log");
            Directory.CreateDirectory(navDir);
            Directory.CreateDirectory(Path.GetDirectoryName(navLogPath)!);

            var sb = new StringBuilder(24576);
            sb.AppendLine($"=== v1_033 NAVIGATION seed={Seed} captureTick=t{CaptureTick} ===");
            sb.AppendLine("OBJECTIF: clic jouable + cache transparent <100ms perçu + panneau lisible.");
            sb.AppendLine();

            MapViewport.Reset();
            MapGeometryCache.ResetStatsAndClear();
            var colors = CountryColors.Load();

            Color32[] worldPixels = null;
            Color32[] countryPixels = null;
            Color32[] provincePixels = null;
            Color32[] provinceCold = null;
            Color32[] provinceHot = null;
            Color32[] panelPixels = null;
            double worldColdMs = 0, countryColdMs = 0, provinceColdMs = 0;
            double worldHotMs = 0, countryHotMs = 0, provinceHotMs = 0;
            string provinceDetail = "";
            int stocksCount = 0, deficitsCount = 0, popsCount = 0;
            bool panelHasSections = false;
            bool panelHasWhyHungry = false;

            using (var harness = new SimulationHarness(Seed))
            {
                harness.RunTicks(CaptureTick);
                var em = harness.EntityManager;

                // --- Monde froid ---
                var sw = Stopwatch.StartNew();
                var worldGeo = MapGeometryCache.GetOrBuild(
                    MapSnapshotExporter.Width, MapSnapshotExporter.Height, null, out var worldHitCold);
                sw.Stop();
                worldColdMs = sw.Elapsed.TotalMilliseconds;
                Assert.IsNotNull(worldGeo);
                Assert.IsFalse(worldHitCold);
                MapViewport.EnsureWorldWindow(worldGeo);

                // Pick province via ProvinceAt (clic simulé sans HUD)
                var pickPx = -1;
                var pickPy = -1;
                for (var i = 0; i < worldGeo.ProvinceAt.Length; i++)
                {
                    var vi = worldGeo.ProvinceAt[i];
                    if (vi < 0) continue;
                    if (worldGeo.ViewsSkeleton[vi].Id != BourgogneProvinceId) continue;
                    pickPx = i % worldGeo.Width;
                    pickPy = i / worldGeo.Width;
                    break;
                }

                Assert.GreaterOrEqual(pickPx, 0, "Bourgogne introuvable dans ProvinceAt monde.");
                Assert.IsTrue(MapClickPicker.TryPickProvinceId(worldGeo, pickPx, pickPy, out var picked));
                Assert.AreEqual(BourgogneProvinceId, picked);

                var frameWorld = MapLayerRenderer.CaptureFrame(em, worldGeo, colors, CaptureTick);
                worldPixels = MapSnapshotExporter.ExportWithGeometryFromViews(
                    frameWorld.PoliticalViews, CaptureTick,
                    Path.Combine(navDir, "world.png"),
                    worldGeo, MapSnapshotExporter.LabelDensity.Countries, -1,
                    null, colors);

                // --- Pays FRA (froid) ---
                Assert.IsTrue(MapDisplaySystem.TrySelectCountryByTag(em, "FRA"));
                sw.Restart();
                var countryGeo = MapGeometryCache.GetOrBuild(
                    MapSnapshotExporter.Width, MapSnapshotExporter.Height,
                    MapViewport.State.Window, out var countryHitCold);
                sw.Stop();
                countryColdMs = sw.Elapsed.TotalMilliseconds;
                Assert.IsFalse(countryHitCold);
                Assert.IsNotNull(countryGeo);

                var frameCountry = MapLayerRenderer.CaptureFrame(em, countryGeo, colors, CaptureTick);
                countryPixels = MapSnapshotExporter.ExportWithGeometryFromViews(
                    frameCountry.PoliticalViews, CaptureTick,
                    Path.Combine(navDir, "country_FRA.png"),
                    countryGeo, MapSnapshotExporter.LabelDensity.Provinces, -1,
                    null, colors);

                // --- Province (froid) ---
                Assert.IsTrue(MapDisplaySystem.TrySelectProvinceById(em, BourgogneProvinceId));
                sw.Restart();
                var provGeo = MapGeometryCache.GetOrBuild(
                    MapSnapshotExporter.Width, MapSnapshotExporter.Height,
                    MapViewport.State.Window, out var provHitCold);
                sw.Stop();
                provinceColdMs = sw.Elapsed.TotalMilliseconds;
                Assert.IsFalse(provHitCold);
                Assert.IsNotNull(provGeo);

                var name = ProvinceCoordinates.NameOf(BourgogneProvinceId);
                Assert.IsTrue(ProvinceObservation.TryCapture(em, BourgogneProvinceId, name, out var obs));
                provinceDetail = obs.DetailBlock;
                stocksCount = obs.Stocks.Count;
                deficitsCount = obs.Deficits.Count;
                popsCount = obs.Pops.Count;
                panelHasSections = provinceDetail.Contains("--- IDENTITY ---") &&
                                   provinceDetail.Contains("--- POPULATION ---") &&
                                   provinceDetail.Contains("--- TRADE FLOWS ---");
                panelHasWhyHungry = provinceDetail.Contains("--- WHY HUNGRY ---");

                File.WriteAllText(
                    Path.Combine(navDir, "province_panel.txt"), provinceDetail, Encoding.UTF8);

                var frameProv = MapLayerRenderer.CaptureFrame(em, provGeo, colors, CaptureTick);
                provinceCold = MapSnapshotExporter.ExportWithGeometryFromViews(
                    frameProv.PoliticalViews, CaptureTick,
                    Path.Combine(navDir, "province_BOURGOGNE_cold.png"),
                    provGeo, MapSnapshotExporter.LabelDensity.SelectedProvince,
                    BourgogneProvinceId, null, colors);
                provincePixels = provinceCold;

                if (provinceCold != null)
                {
                    panelPixels = (Color32[])provinceCold.Clone();
                    MapSnapshotExporter.DrawProvinceDetailPanel(
                        panelPixels, provGeo.Width, provGeo.Height, provinceDetail);
                    MapSnapshotExporter.WriteMapBufferPng(
                        panelPixels, provGeo.Width, provGeo.Height,
                        Path.Combine(navDir, "province_panel.png"));
                }

                // Retour monde puis re-visite (cache chaud)
                var countryWin = MapViewport.BuildCountryWindow(worldGeo, CollectFraIds(em));
                Assert.IsTrue(MapViewport.ZoomOut(countryWin));
                Assert.IsTrue(MapViewport.ZoomOut(MapViewport.WorldWindow));

                sw.Restart();
                var worldHot = MapGeometryCache.GetOrBuild(
                    MapSnapshotExporter.Width, MapSnapshotExporter.Height, null, out var worldHitHot);
                sw.Stop();
                worldHotMs = sw.Elapsed.TotalMilliseconds;
                Assert.IsTrue(worldHitHot);
                Assert.AreSame(worldGeo, worldHot);

                Assert.IsTrue(MapDisplaySystem.TrySelectCountryByTag(em, "FRA"));
                sw.Restart();
                var countryHot = MapGeometryCache.GetOrBuild(
                    MapSnapshotExporter.Width, MapSnapshotExporter.Height,
                    MapViewport.State.Window, out var countryHitHot);
                sw.Stop();
                countryHotMs = sw.Elapsed.TotalMilliseconds;
                Assert.IsTrue(countryHitHot);
                Assert.AreSame(countryGeo, countryHot);

                Assert.IsTrue(MapDisplaySystem.TrySelectProvinceById(em, BourgogneProvinceId));
                sw.Restart();
                var provHotGeo = MapGeometryCache.GetOrBuild(
                    MapSnapshotExporter.Width, MapSnapshotExporter.Height,
                    MapViewport.State.Window, out var provHitHot);
                sw.Stop();
                provinceHotMs = sw.Elapsed.TotalMilliseconds;
                Assert.IsTrue(provHitHot);
                Assert.AreSame(provGeo, provHotGeo);

                provinceHot = MapSnapshotExporter.ExportWithGeometryFromViews(
                    frameProv.PoliticalViews, CaptureTick,
                    Path.Combine(navDir, "province_BOURGOGNE_hot.png"),
                    provHotGeo, MapSnapshotExporter.LabelDensity.SelectedProvince,
                    BourgogneProvinceId, null, colors);

                // Copie lisible « province.png »
                File.Copy(
                    Path.Combine(navDir, "province_BOURGOGNE_cold.png"),
                    Path.Combine(navDir, "province.png"), true);
            }

            var hashCold = Sha256File(Path.Combine(navDir, "province_BOURGOGNE_cold.png"));
            var hashHot = Sha256File(Path.Combine(navDir, "province_BOURGOGNE_hot.png"));
            var byteIdentical = hashCold != null && hashCold == hashHot;

            var maxHotMs = Math.Max(worldHotMs, Math.Max(countryHotMs, provinceHotMs));
            var under100 = maxHotMs < 100.0;
            var hitRate = MapGeometryCache.HitRate;
            var cacheBytes = MapGeometryCache.ApproxBytesUsed;

            sb.AppendLine("=== CLIC → PROVINCE (ProvinceAt) ===");
            sb.AppendLine("Pick Bourgogne via ProvinceAt monde : OK (sans recherche géométrique).");
            sb.AppendLine("Transitions: World→Country(FRA)→Province(6)→Country→World→… cache chaud.");
            sb.AppendLine();

            sb.AppendLine("=== TEMPS DE REPONSE (ms) ===");
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "COLD  world={0:F1} country={1:F1} province={2:F1}",
                worldColdMs, countryColdMs, provinceColdMs));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "HOT   world={0:F3} country={1:F3} province={2:F3}  maxHot={3:F3}",
                worldHotMs, countryHotMs, provinceHotMs, maxHotMs));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "BASELINE_v1_029 world=933 country=5644 province=2983"));
            sb.AppendLine();

            sb.AppendLine("=== CACHE ===");
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "hits={0} misses={1} hitRate={2:F3} entries={3} approxBytes={4} ({5:F1} MiB) maxBytes={6}",
                MapGeometryCache.Hits, MapGeometryCache.Misses, hitRate,
                MapGeometryCache.EntryCount, cacheBytes, cacheBytes / (1024.0 * 1024.0),
                MapGeometryCache.MaxApproxBytes));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "cold_hot_sha256_match={0}", byteIdentical));
            sb.AppendLine($"sha256 cold={hashCold}");
            sb.AppendLine($"sha256 hot ={hashHot}");
            sb.AppendLine();

            sb.AppendLine("=== PANNEAU PROVINCE ===");
            sb.AppendLine(provinceDetail);
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "sections={0} whyHungry={1} stocks={2} deficits={3} pops={4}",
                panelHasSections, panelHasWhyHungry, stocksCount, deficitsCount, popsCount));
            sb.AppendLine();

            var layersOk =
                File.Exists(Path.Combine(navDir, "world.png")) &&
                File.Exists(Path.Combine(navDir, "country_FRA.png")) &&
                File.Exists(Path.Combine(navDir, "province.png")) &&
                File.Exists(Path.Combine(navDir, "province_panel.png"));

            var readable = panelHasSections && panelHasWhyHungry &&
                           (stocksCount > 0 || popsCount > 0);
            var verdict = layersOk && byteIdentical && under100 && readable && hitRate > 0.3f;

            sb.AppendLine("=== VERDICT MESURE ===");
            sb.AppendLine(verdict
                ? "PASS — cache chaud <100ms ; cold/hot octets identiques ; panneau sectionné ; clic ProvinceAt OK."
                : "FAIL — voir critères (100ms / identité octets / panneau / captures).");
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "under_100ms_hot={0} layers_ok={1} readable_panel={2}",
                under100, layersOk, readable));

            File.WriteAllText(navLogPath, sb.ToString(), Encoding.UTF8);
            Debug.Log(sb.ToString());

            Assert.IsTrue(layersOk, "Captures manquantes.");
            Assert.IsTrue(byteIdentical, "Cache non transparent (cold≠hot).");
            Assert.IsTrue(under100, $"Cache chaud trop lent: maxHot={maxHotMs:0.3}ms");
            Assert.IsTrue(readable, "Panneau illisible / sections manquantes.");
            Assert.IsTrue(verdict, "Verdict mesuré FAIL.");
        }

        static System.Collections.Generic.HashSet<int> CollectFraIds(EntityManager em)
        {
            var set = new System.Collections.Generic.HashSet<int>();
            Entity fra = Entity.Null;
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<CountryData>()))
            using (var entities = q.ToEntityArray(Unity.Collections.Allocator.Temp))
            using (var data = q.ToComponentDataArray<CountryData>(Unity.Collections.Allocator.Temp))
            {
                for (var i = 0; i < data.Length; i++)
                {
                    if (data[i].Tag.ToString() == "FRA")
                    {
                        fra = entities[i];
                        break;
                    }
                }
            }

            if (fra == Entity.Null)
                return set;
            using var pq = em.CreateEntityQuery(
                ComponentType.ReadOnly<ProvinceData>(),
                ComponentType.ReadOnly<ProvinceOwnership>());
            using var pdata = pq.ToComponentDataArray<ProvinceData>(Unity.Collections.Allocator.Temp);
            using var owns = pq.ToComponentDataArray<ProvinceOwnership>(Unity.Collections.Allocator.Temp);
            for (var i = 0; i < pdata.Length; i++)
            {
                if (owns[i].Owner == fra)
                    set.Add(pdata[i].ProvinceId);
            }

            return set;
        }

        static string Sha256File(string path)
        {
            if (!File.Exists(path))
                return null;
            using var fs = File.OpenRead(path);
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(fs);
            var sb = new StringBuilder(hash.Length * 2);
            for (var i = 0; i < hash.Length; i++)
                sb.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
            return sb.ToString();
        }
    }
}
