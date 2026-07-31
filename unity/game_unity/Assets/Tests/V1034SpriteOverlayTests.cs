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
    /// -executeMethod VictoriaGame.Tests.V1034SpriteBatchRunner.Run
    /// </summary>
    public static class V1034SpriteBatchRunner
    {
        public static void Run()
        {
            V1034SpriteOverlayTests.RunAndWriteArtifacts();
            Debug.Log("V1034SpriteBatchRunner: DONE");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
        }
    }

    /// <summary>
    /// v1_034 — sprites économiques + fiche pays + preuves de lisibilité.
    /// </summary>
    [TestFixture]
    public class V1034SpriteOverlayTests
    {
        const uint Seed = 42195u;
        const int CaptureTick = 1000;
        const int BourgogneProvinceId = 6;

        [Test]
        public void V1034_Catalog_Inventory_And_Deterministic_Sprites()
        {
            MapSpriteCatalog.Rebuild();
            var inv = MapSpriteCatalog.LastInventory;
            Assert.GreaterOrEqual(MapSpriteCatalog.SpriteCount, 14,
                "Au moins les 13 props + bâtiments en sprites.");
            Assert.GreaterOrEqual(inv.Discarded, 0);
            // Les 5 navires sans .meta doivent être signalés s'ils sont trouvés.
            if (inv.ModelsFound >= 20)
                Assert.GreaterOrEqual(inv.Discarded, 5,
                    "Cinq navires sans .meta doivent être écartés.");

            Assert.IsTrue(MapSpriteCatalog.TryGetSprite("prop_grain_1400", out var a));
            Assert.IsTrue(MapSpriteCatalog.TryGetSprite("prop_grain_1400", out var b));
            Assert.AreEqual(a.Length, b.Length);
            for (var i = 0; i < a.Length; i++)
                Assert.AreEqual(a[i], b[i]);

            var again = MapSpriteCatalog.RenderDeterministicOrthoSprite("prop_grain_1400");
            for (var i = 0; i < a.Length; i++)
                Assert.AreEqual(a[i], again[i], "Sprite grain non déterministe.");
        }

        [Test]
        public void V1034_Visibility_Policy_By_Level()
        {
            Assert.IsFalse(MapSpriteVisibility.ShowPrimaryGood(MapObservationLevel.World));
            Assert.IsTrue(MapSpriteVisibility.ShowPrimaryGood(MapObservationLevel.Country));
            Assert.IsTrue(MapSpriteVisibility.ShowActivities(MapObservationLevel.Province));
            Assert.IsFalse(MapSpriteVisibility.ShowActivities(MapObservationLevel.Country));
            Assert.AreEqual(0, MapSpriteVisibility.WorldMaxMarkers);
        }

        [Test]
        public void V1034_Country_Sheet_And_Sprite_Captures() => RunAndWriteArtifacts();

        public static void RunAndWriteArtifacts()
        {
            var outDir = Path.Combine(Application.dataPath, "..", "Logs", "v1_034_sprites");
            var logPath = Path.Combine(Application.dataPath, "..", "Logs", "v1_034_sprites.log");
            Directory.CreateDirectory(outDir);
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

            var sb = new StringBuilder(32768);
            sb.AppendLine($"=== v1_034 SPRITES seed={Seed} captureTick=t{CaptureTick} ===");
            sb.AppendLine("OBJECTIF: carte économique (good_tag/activités) + fiche pays + preuves.");
            sb.AppendLine();

            MapViewport.Reset();
            MapGeometryCache.ResetStatsAndClear();
            MapSpriteCatalog.Rebuild();
            var inv = MapSpriteCatalog.LastInventory;
            var colors = CountryColors.Load();

            sb.AppendLine("=== INVENTAIRE MODELES ===");
            sb.AppendLine($"modelsRoot={inv.ModelsRoot}");
            sb.AppendLine($"cacheRoot={inv.CacheRoot}");
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "found={0} converted={1} discarded={2} spriteCount={3}",
                inv.ModelsFound, inv.SpritesConverted, inv.Discarded, MapSpriteCatalog.SpriteCount));
            sb.AppendLine($"discardReasons={inv.DiscardReasons}");
            sb.AppendLine();
            sb.AppendLine("=== ARBITRAGE LISIBILITE ===");
            sb.AppendLine(MapSpriteVisibility.DocumentedPolicy());
            sb.AppendLine();

            Color32[] worldPixels = null;
            Color32[] countryPixels = null;
            Color32[] provincePixels = null;
            Color32[] provinceCold = null;
            Color32[] provinceHot = null;
            Color32[] thematicPixels = null;
            Color32[] countryPanelPixels = null;
            string countryDetail = "";
            string provinceDetail = "";
            double worldColdMs = 0, countryColdMs = 0, provinceColdMs = 0;
            double worldHotMs = 0, countryHotMs = 0, provinceHotMs = 0;
            double composeWorldMs = 0, composeCountryMs = 0, composeProvinceMs = 0;
            int spritesWorld = 0, spritesCountry = 0, spritesProvince = 0;
            bool countrySections = false;
            int countryProvLines = 0;

            using (var harness = new SimulationHarness(Seed))
            {
                harness.RunTicks(CaptureTick);
                var em = harness.EntityManager;

                // --- Monde : 0 sprites ---
                var sw = Stopwatch.StartNew();
                var worldGeo = MapGeometryCache.GetOrBuild(
                    MapSnapshotExporter.Width, MapSnapshotExporter.Height, null, out var worldHitCold);
                sw.Stop();
                worldColdMs = sw.Elapsed.TotalMilliseconds;
                Assert.IsNotNull(worldGeo);
                Assert.IsFalse(worldHitCold);
                MapViewport.EnsureWorldWindow(worldGeo);

                worldPixels = MapSnapshotExporter.RenderPoliticalPixels(
                    em, worldGeo, MapSnapshotExporter.LabelDensity.Countries, -1,
                    overlay: p =>
                    {
                        MapSpriteComposer.Compose(
                            p, worldGeo, em, MapObservationLevel.World, thematicLayer: false);
                    });
                composeWorldMs = MapSpriteComposer.LastComposeMilliseconds;
                spritesWorld = MapSpriteComposer.LastSpritesDrawn;
                Assert.AreEqual(0, spritesWorld, "Monde : aucun sprite (lisibilité politique).");
                MapSnapshotExporter.WriteMapBufferPng(
                    worldPixels, worldGeo.Width, worldGeo.Height,
                    Path.Combine(outDir, "world.png"));

                // --- Pays FRA + fiche ---
                Assert.IsTrue(MapDisplaySystem.TrySelectCountryByTag(em, "FRA"));
                Assert.AreEqual(MapObservationLevel.Country, MapViewport.State.Level);
                Assert.IsTrue(CountryObservation.TryCapture(
                    em, MapViewport.State.TargetCountryId, out var countrySnap));
                countryDetail = countrySnap.DetailBlock;
                countrySections = countryDetail.Contains("--- IDENTITY ---") &&
                                  countryDetail.Contains("--- TREASURY ---") &&
                                  countryDetail.Contains("--- MILITARY ---") &&
                                  countryDetail.Contains("--- PROVINCES PROD ---");
                countryProvLines = countrySnap.Provinces.Count;
                File.WriteAllText(
                    Path.Combine(outDir, "country_panel.txt"), countryDetail, Encoding.UTF8);

                sw.Restart();
                var countryGeo = MapGeometryCache.GetOrBuild(
                    MapSnapshotExporter.Width, MapSnapshotExporter.Height,
                    MapViewport.State.Window, out var countryHitCold);
                sw.Stop();
                countryColdMs = sw.Elapsed.TotalMilliseconds;
                Assert.IsFalse(countryHitCold);

                countryPixels = MapSnapshotExporter.RenderPoliticalPixels(
                    em, countryGeo, MapSnapshotExporter.LabelDensity.Provinces, -1,
                    overlay: p =>
                    {
                        MapSpriteComposer.Compose(
                            p, countryGeo, em, MapObservationLevel.Country, thematicLayer: false);
                        MapSnapshotExporter.DrawProvinceDetailPanel(
                            p, countryGeo.Width, countryGeo.Height, countryDetail);
                    });
                composeCountryMs = MapSpriteComposer.LastComposeMilliseconds;
                spritesCountry = MapSpriteComposer.LastSpritesDrawn;
                Assert.Greater(spritesCountry, 0, "Niveau pays : sprites good_tag attendus.");
                MapSnapshotExporter.WriteMapBufferPng(
                    countryPixels, countryGeo.Width, countryGeo.Height,
                    Path.Combine(outDir, "country_FRA.png"));
                countryPanelPixels = countryPixels;

                // --- Province ---
                Assert.IsTrue(MapDisplaySystem.TrySelectProvinceById(em, BourgogneProvinceId));
                var name = ProvinceCoordinates.NameOf(BourgogneProvinceId);
                Assert.IsTrue(ProvinceObservation.TryCapture(em, BourgogneProvinceId, name, out var obs));
                provinceDetail = obs.DetailBlock;
                File.WriteAllText(
                    Path.Combine(outDir, "province_panel.txt"), provinceDetail, Encoding.UTF8);

                sw.Restart();
                var provGeo = MapGeometryCache.GetOrBuild(
                    MapSnapshotExporter.Width, MapSnapshotExporter.Height,
                    MapViewport.State.Window, out var provHitCold);
                sw.Stop();
                provinceColdMs = sw.Elapsed.TotalMilliseconds;
                Assert.IsFalse(provHitCold);

                provinceCold = MapSnapshotExporter.RenderPoliticalPixels(
                    em, provGeo, MapSnapshotExporter.LabelDensity.SelectedProvince,
                    BourgogneProvinceId,
                    overlay: p =>
                    {
                        MapSpriteComposer.Compose(
                            p, provGeo, em, MapObservationLevel.Province, thematicLayer: false);
                    });
                composeProvinceMs = MapSpriteComposer.LastComposeMilliseconds;
                spritesProvince = MapSpriteComposer.LastSpritesDrawn;
                Assert.Greater(spritesProvince, 0);
                provincePixels = provinceCold;
                MapSnapshotExporter.WriteMapBufferPng(
                    provinceCold, provGeo.Width, provGeo.Height,
                    Path.Combine(outDir, "province_BOURGOGNE_cold.png"));
                MapSnapshotExporter.WriteMapBufferPng(
                    provinceCold, provGeo.Width, provGeo.Height,
                    Path.Combine(outDir, "province.png"));

                // --- Couche thématique (sprites masqués) ---
                var frameTheme = MapLayerRenderer.CaptureFrame(em, countryGeo, colors, CaptureTick);
                var palettes = MapLayerRenderer.LoadPalettes();
                var domains = MapLayerRenderer.GetFixedDomains(palettes);
                thematicPixels = MapLayerRenderer.RenderLayerToPixels(
                    countryGeo, frameTheme, MapLayerRenderer.LayerKind.Satisfaction,
                    palettes, domains, colors,
                    extraOverlay: p =>
                    {
                        MapSpriteComposer.Compose(
                            p, countryGeo, em, MapObservationLevel.Country, thematicLayer: true);
                    });
                Assert.AreEqual(0, MapSpriteComposer.LastSpritesDrawn,
                    "Couche thématique : sprites masqués.");
                MapSnapshotExporter.WriteMapBufferPng(
                    thematicPixels, countryGeo.Width, countryGeo.Height,
                    Path.Combine(outDir, "thematic_satisfaction.png"));

                // Cache chaud province
                var countryWin = MapViewport.BuildCountryWindow(worldGeo, CollectFraIds(em));
                Assert.IsTrue(MapViewport.ZoomOut(countryWin));
                Assert.IsTrue(MapViewport.ZoomOut(MapViewport.WorldWindow));

                sw.Restart();
                MapGeometryCache.GetOrBuild(
                    MapSnapshotExporter.Width, MapSnapshotExporter.Height, null, out var worldHitHot);
                sw.Stop();
                worldHotMs = sw.Elapsed.TotalMilliseconds;
                Assert.IsTrue(worldHitHot);

                Assert.IsTrue(MapDisplaySystem.TrySelectCountryByTag(em, "FRA"));
                sw.Restart();
                MapGeometryCache.GetOrBuild(
                    MapSnapshotExporter.Width, MapSnapshotExporter.Height,
                    MapViewport.State.Window, out var countryHitHot);
                sw.Stop();
                countryHotMs = sw.Elapsed.TotalMilliseconds;
                Assert.IsTrue(countryHitHot);

                Assert.IsTrue(MapDisplaySystem.TrySelectProvinceById(em, BourgogneProvinceId));
                sw.Restart();
                var provHotGeo = MapGeometryCache.GetOrBuild(
                    MapSnapshotExporter.Width, MapSnapshotExporter.Height,
                    MapViewport.State.Window, out var provHitHot);
                sw.Stop();
                provinceHotMs = sw.Elapsed.TotalMilliseconds;
                Assert.IsTrue(provHitHot);

                provinceHot = MapSnapshotExporter.RenderPoliticalPixels(
                    em, provHotGeo, MapSnapshotExporter.LabelDensity.SelectedProvince,
                    BourgogneProvinceId,
                    overlay: p =>
                    {
                        MapSpriteComposer.Compose(
                            p, provHotGeo, em, MapObservationLevel.Province, thematicLayer: false);
                    });
                MapSnapshotExporter.WriteMapBufferPng(
                    provinceHot, provHotGeo.Width, provHotGeo.Height,
                    Path.Combine(outDir, "province_BOURGOGNE_hot.png"));

                // Fiche pays seule (bitmap)
                if (countryPanelPixels != null)
                {
                    MapSnapshotExporter.WriteMapBufferPng(
                        countryPanelPixels, countryGeo.Width, countryGeo.Height,
                        Path.Combine(outDir, "country_panel.png"));
                }
            }

            var hashCold = Sha256File(Path.Combine(outDir, "province_BOURGOGNE_cold.png"));
            var hashHot = Sha256File(Path.Combine(outDir, "province_BOURGOGNE_hot.png"));
            var byteIdentical = hashCold != null && hashCold == hashHot;
            var maxHotMs = Math.Max(worldHotMs, Math.Max(countryHotMs, provinceHotMs));
            var under100 = maxHotMs < 100.0;
            var composeMax = Math.Max(composeWorldMs, Math.Max(composeCountryMs, composeProvinceMs));

            sb.AppendLine("=== SPRITES DESSINES ===");
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "world={0} country={1} province={2}",
                spritesWorld, spritesCountry, spritesProvince));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "composeMs world={0:F3} country={1:F3} province={2:F3} max={3:F3}",
                composeWorldMs, composeCountryMs, composeProvinceMs, composeMax));
            sb.AppendLine();

            sb.AppendLine("=== TEMPS GEOMETRIE (ms) ===");
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "COLD  world={0:F1} country={1:F1} province={2:F1}",
                worldColdMs, countryColdMs, provinceColdMs));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "HOT   world={0:F3} country={1:F3} province={2:F3}  maxHot={3:F3}",
                worldHotMs, countryHotMs, provinceHotMs, maxHotMs));
            sb.AppendLine();

            sb.AppendLine("=== CACHE / REPRODUCTIBILITE ===");
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "cold_hot_sha256_match={0}", byteIdentical));
            sb.AppendLine($"sha256 cold={hashCold}");
            sb.AppendLine($"sha256 hot ={hashHot}");
            sb.AppendLine();

            sb.AppendLine("=== FICHE PAYS ===");
            sb.AppendLine(countryDetail);
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "sectionsOk={0} provinceLines={1}", countrySections, countryProvLines));
            sb.AppendLine();

            var filesOk =
                File.Exists(Path.Combine(outDir, "world.png")) &&
                File.Exists(Path.Combine(outDir, "country_FRA.png")) &&
                File.Exists(Path.Combine(outDir, "province.png")) &&
                File.Exists(Path.Combine(outDir, "country_panel.png")) &&
                File.Exists(Path.Combine(outDir, "thematic_satisfaction.png"));

            var verdict = filesOk && countrySections && spritesWorld == 0 &&
                          spritesCountry > 0 && spritesProvince > 0 &&
                          byteIdentical && under100;

            sb.AppendLine("=== VERDICT MESURE ===");
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "filesOk={0} countrySections={1} spritesWorld0={2} spritesCountry>0={3} " +
                "spritesProvince>0={4} shaMatch={5} under100msHot={6} composeMaxMs={7:F3}",
                filesOk, countrySections, spritesWorld == 0, spritesCountry > 0,
                spritesProvince > 0, byteIdentical, under100, composeMax));
            sb.AppendLine(verdict ? "VERDICT: PASS" : "VERDICT: FAIL");

            File.WriteAllText(logPath, sb.ToString(), Encoding.UTF8);
            Debug.Log(sb.ToString());

            Assert.IsTrue(filesOk, "Captures manquantes dans Logs/v1_034_sprites/");
            Assert.IsTrue(countrySections, "Fiche pays incomplète.");
            Assert.AreEqual(0, spritesWorld);
            Assert.Greater(spritesCountry, 0);
            Assert.Greater(spritesProvince, 0);
            Assert.IsTrue(byteIdentical, "cold/hot SHA256 divergent — cache non transparent.");
            Assert.IsTrue(under100, $"maxHot={maxHotMs} ms >= 100");
            Assert.IsTrue(verdict);
        }

        static System.Collections.Generic.HashSet<int> CollectFraIds(EntityManager em)
        {
            var ids = new System.Collections.Generic.HashSet<int>();
            Entity fra = Entity.Null;
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<CountryData>()))
            using (var entities = q.ToEntityArray(Unity.Collections.Allocator.Temp))
            using (var data = q.ToComponentDataArray<CountryData>(Unity.Collections.Allocator.Temp))
            {
                for (var i = 0; i < data.Length; i++)
                {
                    if (data[i].Tag.ToString() != "FRA")
                        continue;
                    fra = entities[i];
                    break;
                }
            }

            if (fra == Entity.Null)
                return ids;

            using (var q = em.CreateEntityQuery(
                       ComponentType.ReadOnly<ProvinceData>(),
                       ComponentType.ReadOnly<ProvinceOwnership>()))
            using (var pdata = q.ToComponentDataArray<ProvinceData>(Unity.Collections.Allocator.Temp))
            using (var owns = q.ToComponentDataArray<ProvinceOwnership>(Unity.Collections.Allocator.Temp))
            {
                for (var i = 0; i < pdata.Length; i++)
                {
                    if (owns[i].Owner == fra)
                        ids.Add(pdata[i].ProvinceId);
                }
            }

            return ids;
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
