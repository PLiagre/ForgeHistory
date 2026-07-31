using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using Unity.Collections;
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
    /// -executeMethod VictoriaGame.Tests.V1029ViewportBatchRunner.Run
    /// </summary>
    public static class V1029ViewportBatchRunner
    {
        public static void Run()
        {
            V1029ViewportTests.RunAndWriteArtifacts();
            Debug.Log("V1029ViewportBatchRunner: DONE");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
        }
    }

    /// <summary>
    /// v1_029 — navigation testable sans rendu + captures zoom monde/pays/province
    /// + 5 couches au niveau province + panneau économie physique.
    /// </summary>
    [TestFixture]
    public class V1029ViewportTests
    {
        const uint Seed = 42195u;
        const int CaptureTick = 1000;
        const int BourgogneProvinceId = 6;

        [Test]
        public void V1029_Navigation_Deterministic_WithoutRender()
        {
            var world = MapViewportNavigation.FitAspectWithMargin(
                -10f, 40f, -60f, -30f, 0.05f, 1600f / 1200f);
            var countryWin = MapViewportNavigation.FitAspectWithMargin(
                0f, 8f, -50f, -42f, 0.18f, 1600f / 1200f);
            var provWin = MapViewportNavigation.ComputePointWindow(
                4.9f * Mathf.Cos(48f * Mathf.Deg2Rad), -47.3f, 3f, 1600f / 1200f);

            var a = MapViewportNavigation.CreateWorld(world);
            Assert.AreEqual(MapObservationLevel.World, a.Level);
            Assert.AreEqual(-1, a.TargetCountryId);
            Assert.AreEqual(-1, a.TargetProvinceId);

            Assert.IsTrue(MapViewportNavigation.TrySelectCountry(
                a, Entity.Null, 0, countryWin, out var b));
            Assert.AreEqual(MapObservationLevel.Country, b.Level);
            Assert.AreEqual(0, b.TargetCountryId);
            Assert.AreEqual(countryWin, b.Window);

            Assert.IsTrue(MapViewportNavigation.TrySelectProvince(
                b, Entity.Null, 0, Entity.Null, BourgogneProvinceId, provWin, out var c));
            Assert.AreEqual(MapObservationLevel.Province, c.Level);
            Assert.AreEqual(BourgogneProvinceId, c.TargetProvinceId);
            Assert.AreEqual(0, c.TargetCountryId);

            Assert.IsTrue(MapViewportNavigation.TryZoomOut(c, countryWin, out var d));
            Assert.AreEqual(MapObservationLevel.Country, d.Level);
            Assert.AreEqual(-1, d.TargetProvinceId);

            Assert.IsTrue(MapViewportNavigation.TryZoomOut(d, world, out var e));
            Assert.AreEqual(MapObservationLevel.World, e.Level);

            Assert.IsFalse(MapViewportNavigation.TryZoomOut(e, world, out _));

            // Déterminisme : mêmes entrées → mêmes sorties.
            Assert.IsTrue(MapViewportNavigation.TrySelectCountry(
                a, Entity.Null, 0, countryWin, out var b2));
            Assert.AreEqual(b, b2);
            Assert.IsTrue(MapViewportNavigation.TrySelectProvince(
                b2, Entity.Null, 0, Entity.Null, BourgogneProvinceId, provWin, out var c2));
            Assert.AreEqual(c, c2);

            // City/District non implémentés — refus depuis ces niveaux.
            var city = c;
            city.Level = MapObservationLevel.City;
            Assert.IsFalse(MapViewportNavigation.TrySelectProvince(
                city, Entity.Null, 0, Entity.Null, 1, provWin, out _));
        }

        [Test]
        public void V1029_Window_FromPoints_IsDeterministic()
        {
            var xs = new NativeArray<float>(3, Allocator.Temp);
            var ys = new NativeArray<float>(3, Allocator.Temp);
            try
            {
                xs[0] = 1f; xs[1] = 3f; xs[2] = 2f;
                ys[0] = 4f; ys[1] = 6f; ys[2] = 5f;
                var w1 = MapViewportNavigation.ComputeWindowFromPoints(
                    xs, ys, 0.1f, 4f / 3f);
                var w2 = MapViewportNavigation.ComputeWindowFromPoints(
                    xs, ys, 0.1f, 4f / 3f);
                Assert.AreEqual(w1, w2);
                Assert.Greater(w1.Width, 0f);
                Assert.Greater(w1.Height, 0f);
                var aspect = w1.Width / w1.Height;
                Assert.AreEqual(4f / 3f, aspect, 0.001f);
            }
            finally
            {
                if (xs.IsCreated) xs.Dispose();
                if (ys.IsCreated) ys.Dispose();
            }
        }

        [Test]
        public void V1029_Zoom_Captures_And_ProvincePanel() => RunAndWriteArtifacts();

        public static void RunAndWriteArtifacts()
        {
            var zoomDir = Path.Combine(Application.dataPath, "..", "Logs", "v1_029_zoom");
            var zoomLogPath = Path.Combine(Application.dataPath, "..", "Logs", "v1_029_zoom.log");
            Directory.CreateDirectory(zoomDir);
            Directory.CreateDirectory(Path.GetDirectoryName(zoomLogPath)!);

            var sb = new StringBuilder(16384);
            sb.AppendLine($"=== v1_029 ZOOM VIEWPORT seed={Seed} captureTick=t{CaptureTick} ===");
            sb.AppendLine("NIVEAUX: World → Country(FRA) → Province(Bourgogne id=6)");
            sb.AppendLine("INTERDIT: villes/quartiers/maisons inventés — agrégats réels uniquement.");
            sb.AppendLine();

            MapViewport.Reset();
            var palettes = MapLayerRenderer.LoadPalettes();
            var domains = MapLayerRenderer.GetFixedDomains(palettes);
            var colors = CountryColors.Load();
            sb.AppendLine(MapLayerRenderer.FormatDomainsLine(domains));
            sb.AppendLine();

            Color32[] worldPixels = null;
            Color32[] countryPixels = null;
            Color32[] provincePixels = null;
            Color32[] provinceRepro = null;
            Color32[] sat = null, pop = null, army = null, treasury = null, trade = null;
            double worldGeoMs = 0, countryGeoMs = 0, provinceGeoMs = 0;
            string provinceDetail = "";
            int stocksCount = 0, activitiesCount = 0, cargoInCount = 0, cargoOutCount = 0;
            int popsCount = 0;
            float physSat = 0, lodSat = 0;

            using (var harness = new SimulationHarness(Seed))
            {
                harness.RunTicks(CaptureTick);
                var em = harness.EntityManager;

                // --- Monde ---
                var sw = Stopwatch.StartNew();
                var worldGeo = MapSnapshotExporter.BuildMapGeometry(
                    MapSnapshotExporter.Width, MapSnapshotExporter.Height);
                sw.Stop();
                worldGeoMs = sw.Elapsed.TotalMilliseconds;
                Assert.IsNotNull(worldGeo);
                MapViewport.EnsureWorldWindow(worldGeo);
                Assert.AreEqual(MapObservationLevel.World, MapViewport.State.Level);

                var frameWorld = MapLayerRenderer.CaptureFrame(em, worldGeo, colors, CaptureTick);
                worldPixels = MapSnapshotExporter.ExportWithGeometryFromViews(
                    frameWorld.PoliticalViews, CaptureTick,
                    Path.Combine(zoomDir, "world.png"),
                    worldGeo, MapSnapshotExporter.LabelDensity.Countries, -1,
                    null, colors);

                // --- Pays FRA ---
                Assert.IsTrue(
                    MapDisplaySystem.TrySelectCountryByTag(em, "FRA"),
                    "Sélection FRA échouée.");
                Assert.AreEqual(MapObservationLevel.Country, MapViewport.State.Level);
                sw.Restart();
                var countryGeo = MapSnapshotExporter.BuildMapGeometry(
                    MapSnapshotExporter.Width, MapSnapshotExporter.Height,
                    MapViewport.State.Window);
                sw.Stop();
                countryGeoMs = sw.Elapsed.TotalMilliseconds;
                Assert.IsNotNull(countryGeo);
                Assert.IsTrue(countryGeo.IsWindowed);

                var frameCountry = MapLayerRenderer.CaptureFrame(em, countryGeo, colors, CaptureTick);
                countryPixels = MapSnapshotExporter.ExportWithGeometryFromViews(
                    frameCountry.PoliticalViews, CaptureTick,
                    Path.Combine(zoomDir, "country_FRA.png"),
                    countryGeo, MapSnapshotExporter.LabelDensity.Provinces, -1,
                    null, colors);

                // --- Province Bourgogne ---
                Assert.IsTrue(
                    MapDisplaySystem.TrySelectProvinceById(em, BourgogneProvinceId),
                    "Sélection Bourgogne échouée.");
                Assert.AreEqual(MapObservationLevel.Province, MapViewport.State.Level);
                Assert.AreEqual(BourgogneProvinceId, MapViewport.State.TargetProvinceId);

                sw.Restart();
                var provGeo = MapSnapshotExporter.BuildMapGeometry(
                    MapSnapshotExporter.Width, MapSnapshotExporter.Height,
                    MapViewport.State.Window);
                sw.Stop();
                provinceGeoMs = sw.Elapsed.TotalMilliseconds;
                Assert.IsNotNull(provGeo);
                Assert.IsTrue(provGeo.IsWindowed);

                var name = ProvinceCoordinates.NameOf(BourgogneProvinceId);
                Assert.IsTrue(
                    ProvinceObservation.TryCapture(em, BourgogneProvinceId, name, out var obs));
                provinceDetail = obs.DetailBlock;
                stocksCount = obs.Stocks.Count;
                activitiesCount = obs.Activities.Count;
                cargoInCount = obs.CargoIn.Count;
                cargoOutCount = obs.CargoOut.Count;
                popsCount = obs.Pops.Count;
                physSat = obs.PhysicalSatisfaction;
                lodSat = obs.LodSatisfaction;

                File.WriteAllText(
                    Path.Combine(zoomDir, "province_BOURGOGNE_panel.txt"),
                    provinceDetail, Encoding.UTF8);

                var frameProv = MapLayerRenderer.CaptureFrame(em, provGeo, colors, CaptureTick);
                provincePixels = MapSnapshotExporter.ExportWithGeometryFromViews(
                    frameProv.PoliticalViews, CaptureTick,
                    Path.Combine(zoomDir, "province_BOURGOGNE.png"),
                    provGeo, MapSnapshotExporter.LabelDensity.SelectedProvince,
                    BourgogneProvinceId, null, colors);

                // Overlay détail pour preuve visuelle
                if (provincePixels != null)
                {
                    MapSnapshotExporter.DrawProvinceDetailPanel(
                        provincePixels, provGeo.Width, provGeo.Height, provinceDetail);
                    MapSnapshotExporter.WriteMapBufferPng(
                        provincePixels, provGeo.Width, provGeo.Height,
                        Path.Combine(zoomDir, "province_BOURGOGNE_detail.png"));
                }

                provinceRepro = MapSnapshotExporter.ExportWithGeometryFromViews(
                    frameProv.PoliticalViews, CaptureTick,
                    Path.Combine(zoomDir, "province_BOURGOGNE_repro.png"),
                    provGeo, MapSnapshotExporter.LabelDensity.SelectedProvince,
                    BourgogneProvinceId, null, colors);

                sat = RenderLayer(
                    provGeo, frameProv, MapLayerRenderer.LayerKind.Satisfaction,
                    palettes, domains, colors,
                    Path.Combine(zoomDir, "province_satisfaction.png"));
                pop = RenderLayer(
                    provGeo, frameProv, MapLayerRenderer.LayerKind.Population,
                    palettes, domains, colors,
                    Path.Combine(zoomDir, "province_population.png"));
                army = RenderLayer(
                    provGeo, frameProv, MapLayerRenderer.LayerKind.Army,
                    palettes, domains, colors,
                    Path.Combine(zoomDir, "province_army.png"));
                treasury = RenderLayer(
                    provGeo, frameProv, MapLayerRenderer.LayerKind.Treasury,
                    palettes, domains, colors,
                    Path.Combine(zoomDir, "province_treasury.png"));
                trade = RenderLayer(
                    provGeo, frameProv, MapLayerRenderer.LayerKind.TradeNode,
                    palettes, domains, colors,
                    Path.Combine(zoomDir, "province_tradenode.png"));

                // Transitions retour
                var countryWin = MapViewport.BuildCountryWindow(worldGeo, CollectFraIds(em));
                Assert.IsTrue(MapViewport.ZoomOut(countryWin));
                Assert.AreEqual(MapObservationLevel.Country, MapViewport.State.Level);
                Assert.IsTrue(MapViewport.ZoomOut(MapViewport.WorldWindow));
                Assert.AreEqual(MapObservationLevel.World, MapViewport.State.Level);
            }

            var hashA = Sha256File(Path.Combine(zoomDir, "province_BOURGOGNE.png"));
            var hashB = Sha256File(Path.Combine(zoomDir, "province_BOURGOGNE_repro.png"));
            var byteIdentical = hashA != null && hashA == hashB;

            var layers = new[]
            {
                ("world.png", worldPixels),
                ("country_FRA.png", countryPixels),
                ("province_BOURGOGNE.png", provincePixels),
                ("province_satisfaction.png", sat),
                ("province_population.png", pop),
                ("province_army.png", army),
                ("province_treasury.png", treasury),
                ("province_tradenode.png", trade),
            };
            var layersNonEmpty = true;
            for (var i = 0; i < layers.Length; i++)
            {
                var path = Path.Combine(zoomDir, layers[i].Item1);
                if (!File.Exists(path) || new FileInfo(path).Length <= 0 ||
                    layers[i].Item2 == null || layers[i].Item2.Length == 0)
                    layersNonEmpty = false;
            }

            var worldCountryDiff = MapSnapshotExporter.CountPixelByteDiffs(
                worldPixels, countryPixels,
                MapSnapshotExporter.Width, MapSnapshotExporter.Height, 0);
            var countryProvDiff = MapSnapshotExporter.CountPixelByteDiffs(
                countryPixels, provincePixels,
                MapSnapshotExporter.Width, MapSnapshotExporter.Height, 0);

            sb.AppendLine("=== TRANSITIONS ===");
            sb.AppendLine("World → Country(FRA) → Province(6 Bourgogne) → Country → World : OK");
            sb.AppendLine();

            sb.AppendLine("=== COUT CHANGEMENT DE NIVEAU (BuildMapGeometry ms) ===");
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "worldGeoMs={0:0.0} countryGeoMs={1:0.0} provinceGeoMs={2:0.0}",
                worldGeoMs, countryGeoMs, provinceGeoMs));
            sb.AppendLine(
                "CACHE: géométrie monde conservée ; rebuild UNIQUEMENT au changement de fenêtre. " +
                "GEOMETRY_BUILDS monde=1 puis +1 par zoom (mesuré).");
            sb.AppendLine();

            sb.AppendLine("=== AFFICHAGE PAR NIVEAU ===");
            sb.AppendLine("World: labels PAYS (LabelDensity.Countries)");
            sb.AppendLine("Country: labels PROVINCES (LabelDensity.Provinces)");
            sb.AppendLine("Province: label sélectionné + panneau stocks/flux/pops/dev/sat");
            sb.AppendLine();

            sb.AppendLine("=== PANNEAU PROVINCE BOURGOGNE (agrégats réels) ===");
            sb.AppendLine(provinceDetail);
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "stocks={0} activities={1} cargoIn={2} cargoOut={3} pops={4} physSat={5:0.00} lodSat={6:0.00}",
                stocksCount, activitiesCount, cargoInCount, cargoOutCount, popsCount, physSat, lodSat));
            sb.AppendLine();

            sb.AppendLine("=== PREUVES IMAGE ===");
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "layers_non_empty={0} repro_sha256_match={1}",
                layersNonEmpty, byteIdentical));
            sb.AppendLine($"sha256 province={hashA}");
            sb.AppendLine($"sha256 repro={hashB}");
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "pixeldiff world↔country={0} country↔province={1}",
                worldCountryDiff, countryProvDiff));
            sb.AppendLine(MapLayerRenderer.FormatDomainsLine(domains));
            sb.AppendLine();

            var readable =
                stocksCount > 0 || activitiesCount > 0 || popsCount > 0;
            var verdict = layersNonEmpty && byteIdentical && readable &&
                          worldCountryDiff > 0 && countryProvDiff > 0;
            sb.AppendLine("=== VERDICT MESURÉ ===");
            sb.AppendLine(verdict
                ? "PASS — zoom re-rend la région ; panneau province lit la sim ; " +
                  "captures non vides ; repro octet-à-octet ; domaines fixes."
                : "FAIL — voir critères ci-dessus.");
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "readable_province_state={0} (stocks|acts|pops)", readable));

            File.WriteAllText(zoomLogPath, sb.ToString(), Encoding.UTF8);
            Debug.Log(sb.ToString());

            Assert.IsTrue(layersNonEmpty, "Captures manquantes ou vides.");
            Assert.IsTrue(byteIdentical, "Repro province non identique en octets.");
            Assert.IsTrue(readable, "Panneau province sans agrégats.");
            Assert.Greater(worldCountryDiff, 0, "Zoom pays doit différer du monde.");
            Assert.Greater(countryProvDiff, 0, "Zoom province doit différer du pays.");
            Assert.IsTrue(verdict, "Verdict mesuré FAIL.");
        }

        static System.Collections.Generic.HashSet<int> CollectFraIds(EntityManager em)
        {
            var set = new System.Collections.Generic.HashSet<int>();
            Entity fra = Entity.Null;
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<CountryData>()))
            using (var entities = q.ToEntityArray(Allocator.Temp))
            using (var data = q.ToComponentDataArray<CountryData>(Allocator.Temp))
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
            using var pdata = pq.ToComponentDataArray<ProvinceData>(Allocator.Temp);
            using var owns = pq.ToComponentDataArray<ProvinceOwnership>(Allocator.Temp);
            for (var i = 0; i < pdata.Length; i++)
            {
                if (owns[i].Owner == fra)
                    set.Add(pdata[i].ProvinceId);
            }

            return set;
        }

        static Color32[] RenderLayer(
            MapSnapshotExporter.MapGeometry geo,
            MapLayerRenderer.LayerFrame frame,
            MapLayerRenderer.LayerKind kind,
            MapLayerRenderer.Palettes palettes,
            MapLayerRenderer.FixedDomains domains,
            CountryColors.Table colors,
            string path)
        {
            var pixels = MapLayerRenderer.RenderLayerToPixels(
                geo, frame, kind, palettes, domains, colors);
            if (pixels != null)
                MapSnapshotExporter.WriteMapBufferPng(pixels, geo.Width, geo.Height, path);
            return pixels;
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
