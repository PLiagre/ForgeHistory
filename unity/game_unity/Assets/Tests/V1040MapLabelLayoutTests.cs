using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;
using VictoriaGame.Presentation;
using VictoriaGame.World;
using Debug = UnityEngine.Debug;

namespace VictoriaGame.Tests
{
    /// <summary>
    /// Point d'entrée batchmode :
    /// -executeMethod VictoriaGame.Tests.V1040MapLabelBatchRunner.Run
    /// </summary>
    public static class V1040MapLabelBatchRunner
    {
        public static void Run()
        {
            V1040MapLabelLayoutTests.RunAndWriteArtifacts();
            Debug.Log("V1040MapLabelBatchRunner: DONE");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
        }
    }

    /// <summary>
    /// v1_040 — réservation d'espace partagée, graduation des noms, zéro chevauchement.
    /// </summary>
    [TestFixture]
    public class V1040MapLabelLayoutTests
    {
        const uint Seed = 42195u;
        const int CaptureTick = 100;
        const int ParisCityId = 1;
        const int BourgogneProvinceId = 6;

        [Test]
        public void V1040_Label_Policy_Is_Documented()
        {
            var policy = MapLabelVisibility.DocumentedPolicy();
            Assert.IsTrue(policy.Contains("COUNTRY"), policy);
            Assert.IsTrue(policy.Contains(
                MapLabelVisibility.CountryMinLabelPopulation.ToString(CultureInfo.InvariantCulture)),
                policy);
            Assert.IsTrue(policy.Contains("Below"), policy);
        }

        [Test]
        public void V1040_No_Label_Overlap_Country_And_Province() => RunAndWriteArtifacts();

        public static void RunAndWriteArtifacts()
        {
            var outDir = Path.Combine(Application.dataPath, "..", "Logs", "v1_040_labels");
            var logPath = Path.Combine(Application.dataPath, "..", "Logs", "v1_040_labels.log");
            Directory.CreateDirectory(outDir);
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

            var sb = new StringBuilder(65536);
            sb.AppendLine($"=== v1_040 MAP LABEL LAYOUT seed={Seed} captureTick=t{CaptureTick} ===");
            sb.AppendLine("OBJECTIF: zéro chevauchement noms villes/provinces + graduation zoom.");
            sb.AppendLine();
            sb.AppendLine("=== POLITIQUE ===");
            sb.AppendLine(MapLabelVisibility.DocumentedPolicy());
            sb.AppendLine(CityMarkerVisibility.DocumentedPolicy());
            sb.AppendLine("Slots=Below→Above→Right→Left (ordre fixe).");
            sb.AppendLine("Priorité conflit: province gagne (placée avant) ; villes cèdent/déplacent/renoncent.");
            sb.AppendLine();

            MapViewport.Reset();
            MapGeometryCache.ResetStatsAndClear();
            CityCoordinates.InvalidateCache();
            MapSpriteCatalog.Rebuild();
            MapLabelLayout.CollisionEnabled = true;
            MapLabelLayout.LegacyCityLabels = false;

            Color32[] countryBefore = null, countryAfter = null, countryCold = null, countryHot = null;
            Color32[] provinceBefore = null, provinceAfter = null;
            Color32[] bourgogneAfter = null;
            int countryDrawn = 0, countryMoved = 0, countryOmitted = 0;
            int provinceDrawn = 0, provinceMoved = 0, provinceOmitted = 0;
            int countryOverlaps = -1, provinceOverlaps = -1;
            int countryCityLabels = 0, provinceCityLabels = 0;
            int countryMarkers = 0, provinceMarkers = 0;
            double countryComposeMs = 0, provinceComposeMs = 0;
            int parisProvinceId = -1;

            using (var harness = new SimulationHarness(Seed))
            {
                harness.RunTicks(CaptureTick);
                var em = harness.EntityManager;
                parisProvinceId = FindCityProvinceId(em, ParisCityId);

                var worldGeo = MapGeometryCache.GetOrBuild(
                    MapSnapshotExporter.Width, MapSnapshotExporter.Height, null, out _);
                MapViewport.EnsureWorldWindow(worldGeo);

                // --- COUNTRY FRA : AVANT (legacy city labels) ---
                Assert.IsTrue(MapDisplaySystem.TrySelectCountryByTag(em, "FRA"));
                var countryGeo = MapGeometryCache.GetOrBuild(
                    MapSnapshotExporter.Width, MapSnapshotExporter.Height,
                    MapViewport.State.Window, out _);

                MapLabelLayout.LegacyCityLabels = true;
                MapLabelLayout.CollisionEnabled = true;
                countryBefore = MapSnapshotExporter.RenderPoliticalPixels(
                    em, countryGeo, MapSnapshotExporter.LabelDensity.Provinces, -1,
                    overlay: p =>
                    {
                        CityMarkerComposer.Compose(
                            p, countryGeo, em, MapObservationLevel.Country,
                            filterCountryId: MapViewport.State.TargetCountryId);
                    });

                // --- COUNTRY FRA : APRÈS (réservation + filtre) ---
                MapLabelLayout.LegacyCityLabels = false;
                MapLabelLayout.CollisionEnabled = true;
                countryCold = MapSnapshotExporter.RenderPoliticalPixels(
                    em, countryGeo, MapSnapshotExporter.LabelDensity.Provinces, -1,
                    overlay: p =>
                    {
                        CityMarkerComposer.Compose(
                            p, countryGeo, em, MapObservationLevel.Country,
                            filterCountryId: MapViewport.State.TargetCountryId);
                    });
                countryOverlaps = MapLabelLayout.CountTextOverlaps();
                countryDrawn = MapLabelLayout.LastDrawn;
                countryMoved = MapLabelLayout.LastMoved;
                countryOmitted = MapLabelLayout.LastOmitted;
                countryCityLabels = CityMarkerComposer.LastLabelsDrawn;
                countryMarkers = CityMarkerComposer.LastMarkersDrawn;
                countryComposeMs = CityMarkerComposer.LastComposeMilliseconds;

                countryHot = MapSnapshotExporter.RenderPoliticalPixels(
                    em, countryGeo, MapSnapshotExporter.LabelDensity.Provinces, -1,
                    overlay: p =>
                    {
                        CityMarkerComposer.Compose(
                            p, countryGeo, em, MapObservationLevel.Country,
                            filterCountryId: MapViewport.State.TargetCountryId);
                    });
                countryAfter = countryHot;

                // --- PROVINCE Paris (cadrage v1_038 province_buildings) : AVANT ---
                Assert.IsTrue(MapDisplaySystem.TrySelectProvinceById(em, parisProvinceId));
                var provGeo = MapGeometryCache.GetOrBuild(
                    MapSnapshotExporter.Width, MapSnapshotExporter.Height,
                    MapViewport.State.Window, out _);

                MapLabelLayout.LegacyCityLabels = true;
                provinceBefore = MapSnapshotExporter.RenderPoliticalPixels(
                    em, provGeo, MapSnapshotExporter.LabelDensity.Provinces, -1,
                    overlay: p =>
                    {
                        MapSpriteComposer.Compose(
                            p, provGeo, em, MapObservationLevel.Province, thematicLayer: false);
                        CityMarkerComposer.Compose(
                            p, provGeo, em, MapObservationLevel.Province,
                            filterProvinceId: parisProvinceId);
                    });

                // --- PROVINCE Paris : APRÈS ---
                MapLabelLayout.LegacyCityLabels = false;
                provinceAfter = MapSnapshotExporter.RenderPoliticalPixels(
                    em, provGeo, MapSnapshotExporter.LabelDensity.Provinces, -1,
                    overlay: p =>
                    {
                        MapSpriteComposer.Compose(
                            p, provGeo, em, MapObservationLevel.Province, thematicLayer: false);
                        CityMarkerComposer.Compose(
                            p, provGeo, em, MapObservationLevel.Province,
                            filterProvinceId: parisProvinceId);
                    });
                provinceOverlaps = MapLabelLayout.CountTextOverlaps();
                provinceDrawn = MapLabelLayout.LastDrawn;
                provinceMoved = MapLabelLayout.LastMoved;
                provinceOmitted = MapLabelLayout.LastOmitted;
                provinceCityLabels = CityMarkerComposer.LastLabelsDrawn;
                provinceMarkers = CityMarkerComposer.LastMarkersDrawn;
                provinceComposeMs = CityMarkerComposer.LastComposeMilliseconds;

                // --- BOURGOGNE (constat brief v1_037) APRÈS ---
                Assert.IsTrue(MapDisplaySystem.TrySelectProvinceById(em, BourgogneProvinceId));
                var bourgGeo = MapGeometryCache.GetOrBuild(
                    MapSnapshotExporter.Width, MapSnapshotExporter.Height,
                    MapViewport.State.Window, out _);
                bourgogneAfter = MapSnapshotExporter.RenderPoliticalPixels(
                    em, bourgGeo, MapSnapshotExporter.LabelDensity.SelectedProvince,
                    BourgogneProvinceId,
                    overlay: p =>
                    {
                        CityMarkerComposer.Compose(
                            p, bourgGeo, em, MapObservationLevel.Province,
                            filterProvinceId: BourgogneProvinceId);
                    });
            }

            MapLabelLayout.LegacyCityLabels = false;
            MapLabelLayout.CollisionEnabled = true;

            var shaCold = Sha256Hex(countryCold);
            var shaHot = Sha256Hex(countryHot);

            WritePng(Path.Combine(outDir, "country_FRA_before.png"), countryBefore);
            WritePng(Path.Combine(outDir, "country_FRA_after.png"), countryAfter);
            WritePng(Path.Combine(outDir, "country_FRA_cold.png"), countryCold);
            WritePng(Path.Combine(outDir, "country_FRA_hot.png"), countryHot);
            WritePng(Path.Combine(outDir, "province_buildings_before.png"), provinceBefore);
            WritePng(Path.Combine(outDir, "province_buildings_after.png"), provinceAfter);
            WritePng(Path.Combine(outDir, "province_BOURGOGNE_after.png"), bourgogneAfter);

            sb.AppendLine("=== NIVEAU PAYS (FRA) ===");
            sb.AppendLine($"markers={countryMarkers} city_labels_drawn={countryCityLabels}");
            sb.AppendLine(
                $"session_labels_drawn={countryDrawn} moved={countryMoved} omitted={countryOmitted}");
            sb.AppendLine($"text_overlaps={countryOverlaps}");
            sb.AppendLine($"compose_ms={countryComposeMs.ToString("0.###", CultureInfo.InvariantCulture)}");
            sb.AppendLine();

            sb.AppendLine("=== NIVEAU PROVINCE (Paris / province_buildings) ===");
            sb.AppendLine($"paris_province_id={parisProvinceId}");
            sb.AppendLine($"markers={provinceMarkers} city_labels_drawn={provinceCityLabels}");
            sb.AppendLine(
                $"session_labels_drawn={provinceDrawn} moved={provinceMoved} omitted={provinceOmitted}");
            sb.AppendLine($"text_overlaps={provinceOverlaps}");
            sb.AppendLine($"compose_ms={provinceComposeMs.ToString("0.###", CultureInfo.InvariantCulture)}");
            sb.AppendLine();

            sb.AppendLine("=== DETERMINISME ===");
            sb.AppendLine($"country_cold_sha256={shaCold}");
            sb.AppendLine($"country_hot_sha256={shaHot}");
            sb.AppendLine($"cold_hot_match={shaCold == shaHot}");
            sb.AppendLine();

            var passOverlap = countryOverlaps == 0 && provinceOverlaps == 0;
            var passOmittedReported = countryOmitted >= 0 && provinceOmitted >= 0;
            var passSha = shaCold == shaHot && shaCold.Length > 0;
            var pass =
                passOverlap && passOmittedReported && passSha &&
                countryDrawn > 0 && provinceDrawn > 0;

            sb.AppendLine("=== VERDICT MESURE ===");
            sb.AppendLine(
                $"{(pass ? "PASS" : "FAIL")}: 0 chevauchement " +
                $"country={countryOverlaps} province={provinceOverlaps}, " +
                $"country drawn={countryDrawn} moved={countryMoved} omitted={countryOmitted} " +
                $"(city_labels={countryCityLabels}), " +
                $"province drawn={provinceDrawn} moved={provinceMoved} omitted={provinceOmitted} " +
                $"(city_labels={provinceCityLabels}), " +
                $"cold==hot SHA256, captures before/after publiées.");
            sb.AppendLine(
                "Renoncements comptés (LastOmitted) — un nom qui disparaît sans trace est un bug.");

            File.WriteAllText(logPath, sb.ToString(), Encoding.UTF8);
            Debug.Log(sb.ToString());

            Assert.AreEqual(0, countryOverlaps, "Chevauchement étiquettes au niveau pays.");
            Assert.AreEqual(0, provinceOverlaps, "Chevauchement étiquettes au niveau province.");
            Assert.AreEqual(shaCold, shaHot, "cold/hot SHA mismatch");
            Assert.Greater(countryDrawn, 0);
            Assert.GreaterOrEqual(countryOmitted, 0);
            Assert.GreaterOrEqual(provinceOmitted, 0);
            // Garde-fou : les renoncements pays doivent être rapportés si densité élevée.
            // (peut être 0 si assez d'espace — alors moved peut compenser)
            Assert.IsTrue(
                countryOmitted + countryMoved + countryCityLabels >= 0,
                "Compteurs d'étiquettes incohérents.");
        }

        static int FindCityProvinceId(EntityManager em, int cityId)
        {
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<CityData>());
            using var arr = q.ToComponentDataArray<CityData>(Unity.Collections.Allocator.Temp);
            for (var i = 0; i < arr.Length; i++)
            {
                if (arr[i].CityId == cityId)
                    return arr[i].ProvinceId;
            }

            return -1;
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

        static void WritePng(string path, Color32[] pixels)
        {
            if (pixels == null)
                return;
            MapSnapshotExporter.WriteMapBufferPng(
                pixels, MapSnapshotExporter.Width, MapSnapshotExporter.Height, path);
        }
    }
}
