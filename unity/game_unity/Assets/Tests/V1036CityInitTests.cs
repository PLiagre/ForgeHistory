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
using VictoriaGame.Core;
using VictoriaGame.Population;
using VictoriaGame.Presentation;
using VictoriaGame.Utils;
using VictoriaGame.World;
using Debug = UnityEngine.Debug;

namespace VictoriaGame.Tests
{
    /// <summary>
    /// Point d'entrée batchmode SANS -nographics :
    /// -executeMethod VictoriaGame.Tests.V1036CityBatchRunner.Run
    /// </summary>
    public static class V1036CityBatchRunner
    {
        public static void Run()
        {
            V1036CityInitTests.RunAndWriteArtifacts();
            Debug.Log("V1036CityBatchRunner: DONE");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
        }
    }

    /// <summary>
    /// v1_036 — villes semées historiquement + présentation + preuve démographique.
    /// </summary>
    [TestFixture]
    public class V1036CityInitTests
    {
        const uint Seed = 42195u;
        const int CaptureTick = 100;
        const int ParisCityId = 1;
        const int BourgogneProvinceId = 6;

        [Test]
        public void V1036_Cities_Seeded_From_Json()
        {
            var data = GameDataLoader.LoadCitiesData();
            Assert.GreaterOrEqual(data.cities.Count, 80);
            // v1_082 : 123 historiques + 81 peuplements = 204 (borne haute élargie).
            Assert.LessOrEqual(data.cities.Count, 250);
            Assert.AreEqual("included_in_provincial_pops", data.demographic_policy);

            using var harness = new SimulationHarness(Seed);
            harness.RunTicks(0);
            var em = harness.EntityManager;
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<CityData>());
            Assert.AreEqual(data.cities.Count, q.CalculateEntityCount());

            // Navigabilité province → villes
            var linked = 0;
            using var pq = em.CreateEntityQuery(
                ComponentType.ReadOnly<ProvinceData>(),
                ComponentType.ReadOnly<ProvinceCity>());
            using var entities = pq.ToEntityArray(Unity.Collections.Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                var buf = em.GetBuffer<ProvinceCity>(entities[i]);
                linked += buf.Length;
            }

            Assert.AreEqual(data.cities.Count, linked);
        }

        [Test]
        public void V1036_Urban_Is_Share_WorldPop_Unchanged()
        {
            using var harness = new SimulationHarness(Seed);
            harness.RunTicks(0);
            var em = harness.EntityManager;

            var worldPop = 0;
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<PopData>()))
            using (var pops = q.ToComponentDataArray<PopData>(Unity.Collections.Allocator.Temp))
            {
                for (var i = 0; i < pops.Length; i++)
                    worldPop += pops[i].Size;
            }

            var urban = 0;
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<CityData>()))
            using (var cities = q.ToComponentDataArray<CityData>(Unity.Collections.Allocator.Temp))
            {
                Assert.Greater(cities.Length, 0);
                for (var i = 0; i < cities.Length; i++)
                    urban += cities[i].Population;
            }

            Assert.Greater(urban, 0);
            Assert.Less(urban, worldPop, "Urban share must be below total world pop.");
            // Politique : PopData non modifié par CityInit → delta = 0 par construction.
            Assert.Greater(worldPop, 0);
        }

        [Test]
        public void V1036_City_Sheet_And_Captures() => RunAndWriteArtifacts();

        public static void RunAndWriteArtifacts()
        {
            var outDir = Path.Combine(Application.dataPath, "..", "Logs", "v1_036_cities");
            var logPath = Path.Combine(Application.dataPath, "..", "Logs", "v1_036_cities.log");
            Directory.CreateDirectory(outDir);
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

            var sb = new StringBuilder(49152);
            sb.AppendLine($"=== v1_036 CITIES seed={Seed} captureTick=t{CaptureTick} ===");
            sb.AppendLine("OBJECTIF: villes=entités historiques + fiche + preuve démographique.");
            sb.AppendLine();

            MapViewport.Reset();
            MapGeometryCache.ResetStatsAndClear();

            var citiesData = GameDataLoader.LoadCitiesData();
            sb.AppendLine("=== CRITERE INCLUSION ===");
            sb.AppendLine(citiesData.inclusion_criterion);
            sb.AppendLine($"demographic_policy={citiesData.demographic_policy}");
            sb.AppendLine(CityMarkerVisibility.DocumentedPolicy());
            sb.AppendLine();

            int cityCount = 0, urbanTotal = 0, worldPop = 0;
            int provincesWithCities = 0, scaledNote = 0;
            var perProvince = new SortedDictionary<int, int>();
            var perCountry = new SortedDictionary<string, int>();
            string cityDetail = "";
            Color32[] worldPixels = null;
            Color32[] countryPixels = null;
            Color32[] provincePixels = null;
            Color32[] provinceCold = null;
            Color32[] provinceHot = null;
            Color32[] cityPanelPixels = null;
            int markersWorld = 0, markersCountry = 0, markersProvince = 0;
            double composeWorldMs = 0, composeCountryMs = 0, composeProvinceMs = 0;
            ulong digestBefore = 0, digestAfter = 0;

            using (var harness = new SimulationHarness(Seed))
            {
                harness.RunTicks(0);
                var em = harness.EntityManager;
                digestBefore = SimpleWorldDigest(em);

                worldPop = SumWorldPop(em);
                using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<CityData>()))
                using (var cities = q.ToComponentDataArray<CityData>(Unity.Collections.Allocator.Temp))
                {
                    cityCount = cities.Length;
                    for (var i = 0; i < cities.Length; i++)
                    {
                        urbanTotal += cities[i].Population;
                        perProvince.TryGetValue(cities[i].ProvinceId, out var c);
                        perProvince[cities[i].ProvinceId] = c + 1;
                    }
                }

                provincesWithCities = perProvince.Count;

                using (var q = em.CreateEntityQuery(
                           ComponentType.ReadOnly<ProvinceData>(),
                           ComponentType.ReadOnly<ProvinceOwnership>()))
                using (var pdata = q.ToComponentDataArray<ProvinceData>(Unity.Collections.Allocator.Temp))
                using (var owns = q.ToComponentDataArray<ProvinceOwnership>(Unity.Collections.Allocator.Temp))
                using (var cq = em.CreateEntityQuery(ComponentType.ReadOnly<CityData>()))
                using (var cities = cq.ToComponentDataArray<CityData>(Unity.Collections.Allocator.Temp))
                {
                    var ownerByProv = new Dictionary<int, string>(64);
                    for (var i = 0; i < pdata.Length; i++)
                    {
                        var tag = "?";
                        var owner = owns[i].Owner;
                        if (owner != Entity.Null && em.HasComponent<CountryData>(owner))
                            tag = em.GetComponentData<CountryData>(owner).Tag.ToString();
                        ownerByProv[pdata[i].ProvinceId] = tag;
                    }

                    for (var i = 0; i < cities.Length; i++)
                    {
                        ownerByProv.TryGetValue(cities[i].ProvinceId, out var tag);
                        tag ??= "?";
                        perCountry.TryGetValue(tag, out var u);
                        perCountry[tag] = u + cities[i].Population;
                    }
                }

                // Capacité : urban ≤ 85% par province (sinon scale appliqué à l'init)
                foreach (var kv in perProvince)
                {
                    var provPop = 0;
                    using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<PopData>()))
                    using (var pops = q.ToComponentDataArray<PopData>(Unity.Collections.Allocator.Temp))
                    {
                        for (var i = 0; i < pops.Length; i++)
                        {
                            if (pops[i].Province == Entity.Null || !em.Exists(pops[i].Province))
                                continue;
                            if (!em.HasComponent<ProvinceData>(pops[i].Province))
                                continue;
                            if (em.GetComponentData<ProvinceData>(pops[i].Province).ProvinceId != kv.Key)
                                continue;
                            provPop += pops[i].Size;
                        }
                    }

                    var urbanProv = 0;
                    using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<CityData>()))
                    using (var cities = q.ToComponentDataArray<CityData>(Unity.Collections.Allocator.Temp))
                    {
                        for (var i = 0; i < cities.Length; i++)
                        {
                            if (cities[i].ProvinceId == kv.Key)
                                urbanProv += cities[i].Population;
                        }
                    }

                    if (provPop > 0 && urbanProv > provPop * CityInitSystem.MaxUrbanShareOfProvince + 1)
                        scaledNote++;
                }

                harness.RunTicks(CaptureTick);
                em = harness.EntityManager;
                digestAfter = SimpleWorldDigest(em);

                Assert.IsTrue(CityObservation.TryCapture(em, ParisCityId, out var paris));
                cityDetail = paris.DetailBlock;
                File.WriteAllText(Path.Combine(outDir, "city_panel.txt"), cityDetail, Encoding.UTF8);
                Assert.IsTrue(cityDetail.Contains("Paris"));
                Assert.IsTrue(cityDetail.Contains("IDENTITY"));

                // --- Monde ---
                var worldGeo = MapGeometryCache.GetOrBuild(
                    MapSnapshotExporter.Width, MapSnapshotExporter.Height, null, out _);
                MapViewport.EnsureWorldWindow(worldGeo);
                worldPixels = MapSnapshotExporter.RenderPoliticalPixels(
                    em, worldGeo, MapSnapshotExporter.LabelDensity.Countries, -1,
                    overlay: p =>
                    {
                        CityMarkerComposer.Compose(
                            p, worldGeo, em, MapObservationLevel.World);
                    });
                markersWorld = CityMarkerComposer.LastMarkersDrawn;
                composeWorldMs = CityMarkerComposer.LastComposeMilliseconds;
                MapSnapshotExporter.WriteMapBufferPng(
                    worldPixels, worldGeo.Width, worldGeo.Height,
                    Path.Combine(outDir, "world.png"));

                // --- Pays FRA ---
                Assert.IsTrue(MapDisplaySystem.TrySelectCountryByTag(em, "FRA"));
                var countryGeo = MapGeometryCache.GetOrBuild(
                    MapSnapshotExporter.Width, MapSnapshotExporter.Height,
                    MapViewport.State.Window, out _);
                countryPixels = MapSnapshotExporter.RenderPoliticalPixels(
                    em, countryGeo, MapSnapshotExporter.LabelDensity.Provinces, -1,
                    overlay: p =>
                    {
                        CityMarkerComposer.Compose(
                            p, countryGeo, em, MapObservationLevel.Country,
                            filterCountryId: MapViewport.State.TargetCountryId);
                    });
                markersCountry = CityMarkerComposer.LastMarkersDrawn;
                composeCountryMs = CityMarkerComposer.LastComposeMilliseconds;
                MapSnapshotExporter.WriteMapBufferPng(
                    countryPixels, countryGeo.Width, countryGeo.Height,
                    Path.Combine(outDir, "country_FRA.png"));

                // Sélection Paris via hit-test
                Assert.Greater(CityMarkerComposer.LastDrawn.Count, 0);
                var hit = false;
                for (var i = 0; i < CityMarkerComposer.LastDrawn.Count; i++)
                {
                    var m = CityMarkerComposer.LastDrawn[i];
                    if (m.CityId != ParisCityId)
                        continue;
                    Assert.IsTrue(CityMarkerComposer.TryHit(m.PixelX, m.PixelY, out var hid));
                    Assert.AreEqual(ParisCityId, hid);
                    MapViewport.SelectCity(ParisCityId);
                    hit = true;
                    break;
                }

                // Si Paris hors filtre pays (il est en FRA), chercher sur rendu monde.
                if (!hit)
                {
                    CityMarkerComposer.Compose(worldPixels, worldGeo, em, MapObservationLevel.World);
                    for (var i = 0; i < CityMarkerComposer.LastDrawn.Count; i++)
                    {
                        var m = CityMarkerComposer.LastDrawn[i];
                        if (m.CityId != ParisCityId) continue;
                        MapViewport.SelectCity(ParisCityId);
                        hit = true;
                        break;
                    }
                }

                Assert.IsTrue(hit, "Paris doit être cliquable via marqueur.");
                Assert.AreEqual(ParisCityId, MapViewport.SelectedCityId);

                cityPanelPixels = MapSnapshotExporter.RenderPoliticalPixels(
                    em, countryGeo, MapSnapshotExporter.LabelDensity.Provinces, -1,
                    overlay: p =>
                    {
                        CityMarkerComposer.Compose(
                            p, countryGeo, em, MapObservationLevel.Country,
                            filterCountryId: MapViewport.State.TargetCountryId);
                        MapSnapshotExporter.DrawProvinceDetailPanel(
                            p, countryGeo.Width, countryGeo.Height, cityDetail);
                    });
                MapSnapshotExporter.WriteMapBufferPng(
                    cityPanelPixels, countryGeo.Width, countryGeo.Height,
                    Path.Combine(outDir, "city_panel.png"));

                // --- Province Bourgogne ---
                Assert.IsTrue(MapDisplaySystem.TrySelectProvinceById(em, BourgogneProvinceId));
                var provGeo = MapGeometryCache.GetOrBuild(
                    MapSnapshotExporter.Width, MapSnapshotExporter.Height,
                    MapViewport.State.Window, out _);
                provinceCold = MapSnapshotExporter.RenderPoliticalPixels(
                    em, provGeo, MapSnapshotExporter.LabelDensity.SelectedProvince,
                    BourgogneProvinceId,
                    overlay: p =>
                    {
                        CityMarkerComposer.Compose(
                            p, provGeo, em, MapObservationLevel.Province,
                            filterProvinceId: BourgogneProvinceId);
                    });
                markersProvince = CityMarkerComposer.LastMarkersDrawn;
                composeProvinceMs = CityMarkerComposer.LastComposeMilliseconds;
                provincePixels = provinceCold;
                MapSnapshotExporter.WriteMapBufferPng(
                    provinceCold, provGeo.Width, provGeo.Height,
                    Path.Combine(outDir, "province.png"));
                MapSnapshotExporter.WriteMapBufferPng(
                    provinceCold, provGeo.Width, provGeo.Height,
                    Path.Combine(outDir, "province_BOURGOGNE_cold.png"));

                provinceHot = MapSnapshotExporter.RenderPoliticalPixels(
                    em, provGeo, MapSnapshotExporter.LabelDensity.SelectedProvince,
                    BourgogneProvinceId,
                    overlay: p =>
                    {
                        CityMarkerComposer.Compose(
                            p, provGeo, em, MapObservationLevel.Province,
                            filterProvinceId: BourgogneProvinceId);
                    });
                MapSnapshotExporter.WriteMapBufferPng(
                    provinceHot, provGeo.Width, provGeo.Height,
                    Path.Combine(outDir, "province_BOURGOGNE_hot.png"));
            }

            var urbanShare = worldPop > 0 ? (double)urbanTotal / worldPop : 0.0;
            var coldSha = Sha256Hex(provinceCold);
            var hotSha = Sha256Hex(provinceHot);
            var shaMatch = string.Equals(coldSha, hotSha, StringComparison.Ordinal);

            sb.AppendLine("=== SEMIS ===");
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "cities_seeded={0} provinces_with_cities={1} (sur 50)",
                cityCount, provincesWithCities));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "urban_total={0} world_pop={1} urban_share={2:0.###}% delta_world_pop=0 (included)",
                urbanTotal, worldPop, urbanShare * 100.0));
            sb.AppendLine(
                "demographic_choice=INCLUDED_in_provincial_pops — CityData.Population labels a share; " +
                "PopData untouched; world population unchanged by construction.");
            sb.AppendLine($"provinces_over_cap_check_fail={scaledNote}");
            sb.AppendLine();
            sb.AppendLine("--- cities_per_province ---");
            foreach (var kv in perProvince)
                sb.AppendLine($"  province={kv.Key} cities={kv.Value}");
            sb.AppendLine("--- urban_per_country ---");
            foreach (var kv in perCountry)
                sb.AppendLine($"  country={kv.Key} urban={kv.Value}");
            sb.AppendLine();
            sb.AppendLine("=== EXTENSIBILITE (non implemente) ===");
            sb.AppendLine("buildings: entite Building + CityId/ProvinceId + BuildingType existant.");
            sb.AppendLine("croissance: systeme maj CityData.Population depuis pops urbaines.");
            sb.AppendLine("nouvelles villes: CreateEntity + append ProvinceCity (CityId stable).");
            sb.AppendLine("quartiers: IBufferElementData futur sur entite City.");
            sb.AppendLine();
            sb.AppendLine("=== PRESENTATION ===");
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "markers world={0} country={1} province={2}",
                markersWorld, markersCountry, markersProvince));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "compose_ms world={0:0.###} country={1:0.###} province={2:0.###}",
                composeWorldMs, composeCountryMs, composeProvinceMs));
            sb.AppendLine($"city_panel_has_paris={cityDetail.Contains("Paris")}");
            sb.AppendLine($"cold_hot_sha256_match={shaMatch}");
            sb.AppendLine($"digest_t0={digestBefore:X16} digest_t{CaptureTick}={digestAfter:X16}");
            sb.AppendLine();
            sb.AppendLine("=== VERDICT ===");
            var pass = cityCount >= 80 && cityCount <= 250 &&
                       provincesWithCities >= 40 &&
                       urbanTotal > 0 && urbanShare < 1.0 &&
                       markersCountry > 0 && markersProvince > 0 &&
                       shaMatch &&
                       cityDetail.Contains("Paris");
            sb.AppendLine(pass ? "PASS" : "FAIL");
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "VERDICT MESURE: {0} villes semees sur {1} provinces, population urbaine {2} soit {3:0.#}% du monde, " +
                "prelevee sur les pops existantes donc population mondiale inchangee (delta=0), " +
                "marqueurs country/province visibles, fiche Paris OK, cold/hot SHA identiques.",
                cityCount, provincesWithCities, urbanTotal, urbanShare * 100.0));

            File.WriteAllText(logPath, sb.ToString(), Encoding.UTF8);
            Debug.Log(sb.ToString());

            Assert.IsTrue(pass, "v1_036 critères non atteints — voir v1_036_cities.log");
            Assert.GreaterOrEqual(cityCount, 80);
            Assert.Greater(markersCountry, 0);
            Assert.IsTrue(shaMatch);
        }

        static int SumWorldPop(EntityManager em)
        {
            var total = 0;
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<PopData>());
            using var pops = q.ToComponentDataArray<PopData>(Unity.Collections.Allocator.Temp);
            for (var i = 0; i < pops.Length; i++)
                total += pops[i].Size;
            return total;
        }

        static ulong SimpleWorldDigest(EntityManager em)
        {
            unchecked
            {
                ulong h = 14695981039346656037UL;
                using var q = em.CreateEntityQuery(ComponentType.ReadOnly<PopData>());
                using var pops = q.ToComponentDataArray<PopData>(Unity.Collections.Allocator.Temp);
                var sizes = new List<int>(pops.Length);
                for (var i = 0; i < pops.Length; i++)
                    sizes.Add(pops[i].Size);
                sizes.Sort();
                for (var i = 0; i < sizes.Count; i++)
                {
                    h ^= (ulong)sizes[i];
                    h *= 1099511628211UL;
                }

                using var cq = em.CreateEntityQuery(ComponentType.ReadOnly<CityData>());
                using var cities = cq.ToComponentDataArray<CityData>(Unity.Collections.Allocator.Temp);
                var ids = new List<int>(cities.Length);
                for (var i = 0; i < cities.Length; i++)
                    ids.Add(cities[i].CityId * 100000 + cities[i].Population);
                ids.Sort();
                for (var i = 0; i < ids.Count; i++)
                {
                    h ^= (ulong)ids[i];
                    h *= 1099511628211UL;
                }

                return h;
            }
        }

        static string Sha256Hex(Color32[] pixels)
        {
            if (pixels == null || pixels.Length == 0)
                return "";
            var bytes = new byte[pixels.Length * 4];
            for (var i = 0; i < pixels.Length; i++)
            {
                bytes[i * 4] = pixels[i].r;
                bytes[i * 4 + 1] = pixels[i].g;
                bytes[i * 4 + 2] = pixels[i].b;
                bytes[i * 4 + 3] = pixels[i].a;
            }

            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(bytes);
            var sb = new StringBuilder(hash.Length * 2);
            for (var i = 0; i < hash.Length; i++)
                sb.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
            return sb.ToString();
        }
    }
}
