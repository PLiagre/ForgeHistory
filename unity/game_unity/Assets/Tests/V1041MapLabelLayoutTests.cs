using System;
using System.Collections.Generic;
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
    /// -executeMethod VictoriaGame.Tests.V1041MapLabelBatchRunner.Run
    /// </summary>
    public static class V1041MapLabelBatchRunner
    {
        public static void Run()
        {
            V1041MapLabelLayoutTests.RunAndWriteArtifacts();
            Debug.Log("V1041MapLabelBatchRunner: DONE");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
        }
    }

    /// <summary>
    /// v1_041 — file d'importance unique, capitales protégées, zéro chevauchement.
    /// </summary>
    [TestFixture]
    public class V1041MapLabelLayoutTests
    {
        const uint Seed = 42195u;
        const int CaptureTick = 100;
        const int ParisCityId = 1;
        const int DijonCityId = 15;
        const int BourgogneProvinceId = 6;
        const int IleDeFranceProvinceId = 1;
        /// <summary>
        /// Plafond de renoncements villes au niveau pays — justifié : densités locales
        /// extrêmes (marqueur+province+capitale) peuvent forcer 0–2 omis non protégés.
        /// Interdit le retour à « 2 noms sur 17 ».
        /// </summary>
        const int CountryCityOmitCeiling = 3;

        [Test]
        public void V1041_Rank_Scale_Is_Documented()
        {
            var scale = MapLabelImportance.DocumentedRankScale();
            Assert.IsTrue(scale.Contains("capitale NATIONALE"), scale);
            Assert.IsTrue(scale.Contains("rang↑"), scale);
            var order = MapLabelLayout.DocumentedPlacementOrder();
            Assert.IsTrue(order.Contains("Below"), order);
            Assert.IsTrue(order.Contains("anneau"), order);
        }

        [Test]
        public void V1041_Capitals_Are_Never_Omitted() =>
            Assert.IsTrue(RunCapitalsCheck(writeLog: false), "Capitales/provinces omises.");

        [Test]
        public void V1041_Overlap_Still_Zero() =>
            Assert.IsTrue(RunOverlapCheck(), "Chevauchement non nul.");

        [Test]
        public void V1041_Label_Count_Floor() =>
            Assert.IsTrue(RunLabelFloorCheck(), "Trop de noms de villes renoncés.");

        [Test]
        public void V1041_Order_Is_Deterministic() =>
            Assert.IsTrue(RunDeterminismCheck(), "Séquence LastPlaced non déterministe.");

        [Test]
        public void V1041_Artifacts_And_Verdict() => RunAndWriteArtifacts();

        public static void RunAndWriteArtifacts()
        {
            var outDir = Path.Combine(Application.dataPath, "..", "Logs", "v1_041_labels");
            var logPath = Path.Combine(Application.dataPath, "..", "Logs", "v1_041_labels.log");
            Directory.CreateDirectory(outDir);
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

            var sb = new StringBuilder(65536);
            sb.AppendLine($"=== v1_041 MAP LABEL IMPORTANCE seed={Seed} captureTick=t{CaptureTick} ===");
            sb.AppendLine("OBJECTIF: capitales (PARIS, DIJON) lisibles + 0 chevauchement + file unique.");
            sb.AppendLine();
            sb.AppendLine("=== POLITIQUE / RANGS ===");
            sb.AppendLine(MapLabelImportance.DocumentedRankScale());
            sb.AppendLine(MapLabelLayout.DocumentedPlacementOrder());
            sb.AppendLine(MapLabelVisibility.DocumentedPolicy());
            sb.AppendLine();

            MapViewport.Reset();
            MapGeometryCache.ResetStatsAndClear();
            CityCoordinates.InvalidateCache();
            MapSpriteCatalog.Rebuild();
            MapLabelLayout.CollisionEnabled = true;
            MapLabelLayout.LegacyCityLabels = false;
            MapLabelLayout.UseImportanceQueue = true;

            // --- Rouge initial simulé : comportement v1_040 (pas de file) ---
            var initialRed = ProbeCapitalsWithQueue(enabled: false);
            sb.AppendLine("=== ROUGE INITIAL V1041_Capitals (UseImportanceQueue=false = v1_040) ===");
            sb.AppendLine($"capitals_ok={initialRed.ok}");
            sb.AppendLine($"missing={initialRed.missing}");
            sb.AppendLine(
                "Attendu ROUGE sur v1_040 : PARIS et/ou DIJON absents de LastPlaced.");
            sb.AppendLine();

            // Probe a touché MapViewport sur un harness disposé — reset obligatoire.
            MapViewport.Reset();
            MapGeometryCache.ResetStatsAndClear();
            MapLabelLayout.UseImportanceQueue = true;
            MapLabelLayout.CollisionEnabled = true;
            MapLabelLayout.LegacyCityLabels = false;

            Color32[] countryBefore = null, countryAfter = null, countryCold = null, countryHot = null;
            Color32[] provinceBefore = null, provinceAfter = null;
            Color32[] bourgogneAfter = null, ileDeFranceAfter = null;
            int countryDrawn = 0, countryMoved = 0, countryOmitted = 0, countryDisplaced = 0;
            int provinceDrawn = 0, provinceMoved = 0, provinceOmitted = 0, provinceDisplaced = 0;
            int countryOverlaps = -1, provinceOverlaps = -1;
            int countryCityLabels = 0, provinceCityLabels = 0;
            int countryMarkers = 0, provinceMarkers = 0;
            int countryEligible = 0;
            double countryComposeMsBefore = 0, countryComposeMsAfter = 0;
            string countryOmittedNames = "";
            List<MapPlacedLabel> countryPlaced = null;
            bool capitalsOk = false;
            string capitalsMissing = "";
            bool floorOk = false;
            bool detOk = false;
            string shaCold = "", shaHot = "";

            using (var harness = new SimulationHarness(Seed))
            {
                harness.RunTicks(CaptureTick);
                var em = harness.EntityManager;
                var parisProvinceId = FindCityProvinceId(em, ParisCityId);

                var worldGeo = MapGeometryCache.GetOrBuild(
                    MapSnapshotExporter.Width, MapSnapshotExporter.Height, null, out _);
                MapViewport.EnsureWorldWindow(worldGeo);

                Assert.IsTrue(MapDisplaySystem.TrySelectCountryByTag(em, "FRA"));
                var countryGeo = MapGeometryCache.GetOrBuild(
                    MapSnapshotExporter.Width, MapSnapshotExporter.Height,
                    MapViewport.State.Window, out _);

                // AVANT = état v1_040 (file désactivée).
                MapLabelLayout.UseImportanceQueue = false;
                MapLabelLayout.LegacyCityLabels = false;
                MapLabelLayout.CollisionEnabled = true;
                countryBefore = MapSnapshotExporter.RenderPoliticalPixels(
                    em, countryGeo, MapSnapshotExporter.LabelDensity.Provinces, -1,
                    overlay: p =>
                    {
                        CityMarkerComposer.Compose(
                            p, countryGeo, em, MapObservationLevel.Country,
                            filterCountryId: MapViewport.State.TargetCountryId);
                        countryComposeMsBefore = CityMarkerComposer.LastComposeMilliseconds;
                    });

                // APRÈS = file d'importance.
                MapLabelLayout.UseImportanceQueue = true;
                countryEligible = CountEligibleCityLabels(
                    em, MapObservationLevel.Country, MapViewport.State.TargetCountryId, -1);
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
                countryDisplaced = MapLabelLayout.LastDisplaced;
                countryCityLabels = CityMarkerComposer.LastLabelsDrawn;
                countryMarkers = CityMarkerComposer.LastMarkersDrawn;
                countryComposeMsAfter = CityMarkerComposer.LastComposeMilliseconds;
                countryOmittedNames = MapLabelLayout.FormatOmittedNames();
                countryPlaced = new List<MapPlacedLabel>(MapLabelLayout.LastPlaced);

                countryHot = MapSnapshotExporter.RenderPoliticalPixels(
                    em, countryGeo, MapSnapshotExporter.LabelDensity.Provinces, -1,
                    overlay: p =>
                    {
                        CityMarkerComposer.Compose(
                            p, countryGeo, em, MapObservationLevel.Country,
                            filterCountryId: MapViewport.State.TargetCountryId);
                    });
                countryAfter = countryHot;
                shaCold = Sha256Hex(countryCold);
                shaHot = Sha256Hex(countryHot);

                // PROVINCE buildings (Paris) AVANT/APRÈS
                Assert.IsTrue(MapDisplaySystem.TrySelectProvinceById(em, parisProvinceId));
                var provGeo = MapGeometryCache.GetOrBuild(
                    MapSnapshotExporter.Width, MapSnapshotExporter.Height,
                    MapViewport.State.Window, out _);

                MapLabelLayout.UseImportanceQueue = false;
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

                MapLabelLayout.UseImportanceQueue = true;
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
                provinceDisplaced = MapLabelLayout.LastDisplaced;
                provinceCityLabels = CityMarkerComposer.LastLabelsDrawn;
                provinceMarkers = CityMarkerComposer.LastMarkersDrawn;

                // BOURGOGNE
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
                var bourgPlaced = new List<MapPlacedLabel>(MapLabelLayout.LastPlaced);

                // Île-de-France
                Assert.IsTrue(MapDisplaySystem.TrySelectProvinceById(em, IleDeFranceProvinceId));
                var idfGeo = MapGeometryCache.GetOrBuild(
                    MapSnapshotExporter.Width, MapSnapshotExporter.Height,
                    MapViewport.State.Window, out _);
                ileDeFranceAfter = MapSnapshotExporter.RenderPoliticalPixels(
                    em, idfGeo, MapSnapshotExporter.LabelDensity.SelectedProvince,
                    IleDeFranceProvinceId,
                    overlay: p =>
                    {
                        CityMarkerComposer.Compose(
                            p, idfGeo, em, MapObservationLevel.Province,
                            filterProvinceId: IleDeFranceProvinceId);
                    });
                var idfPlaced = new List<MapPlacedLabel>(MapLabelLayout.LastPlaced);

                capitalsOk = CheckCapitalsPresent(
                    countryPlaced, bourgPlaced, idfPlaced, out capitalsMissing);
                floorOk = countryCityLabels >= countryEligible - CountryCityOmitCeiling;
                detOk = CheckDeterministicSequence(em, countryGeo);
            }

            MapLabelLayout.UseImportanceQueue = true;
            MapLabelLayout.LegacyCityLabels = false;
            MapLabelLayout.CollisionEnabled = true;

            WritePng(Path.Combine(outDir, "country_FRA_before.png"), countryBefore);
            WritePng(Path.Combine(outDir, "country_FRA_after.png"), countryAfter);
            WritePng(Path.Combine(outDir, "country_FRA_cold.png"), countryCold);
            WritePng(Path.Combine(outDir, "country_FRA_hot.png"), countryHot);
            WritePng(Path.Combine(outDir, "province_buildings_before.png"), provinceBefore);
            WritePng(Path.Combine(outDir, "province_buildings_after.png"), provinceAfter);
            WritePng(Path.Combine(outDir, "province_BOURGOGNE_after.png"), bourgogneAfter);
            WritePng(Path.Combine(outDir, "province_ILE_DE_FRANCE_after.png"), ileDeFranceAfter);

            sb.AppendLine("=== NIVEAU PAYS (FRA) ===");
            sb.AppendLine($"markers={countryMarkers} city_labels_drawn={countryCityLabels} eligible={countryEligible}");
            sb.AppendLine(
                $"session_labels_drawn={countryDrawn} moved={countryMoved} " +
                $"displaced={countryDisplaced} omitted={countryOmitted}");
            sb.AppendLine($"renoncé: {countryOmittedNames}");
            sb.AppendLine($"text_overlaps={countryOverlaps}");
            sb.AppendLine(
                $"compose_ms_before={countryComposeMsBefore.ToString("0.###", CultureInfo.InvariantCulture)} " +
                $"compose_ms_after={countryComposeMsAfter.ToString("0.###", CultureInfo.InvariantCulture)}");
            sb.AppendLine();

            sb.AppendLine("=== NIVEAU PROVINCE (Paris / province_buildings) ===");
            sb.AppendLine($"markers={provinceMarkers} city_labels_drawn={provinceCityLabels}");
            sb.AppendLine(
                $"session_labels_drawn={provinceDrawn} moved={provinceMoved} " +
                $"displaced={provinceDisplaced} omitted={provinceOmitted}");
            sb.AppendLine($"text_overlaps={provinceOverlaps}");
            sb.AppendLine();

            sb.AppendLine("=== TESTS V1041 ===");
            sb.AppendLine(
                $"V1041_Capitals_Are_Never_Omitted: {(capitalsOk ? "PASS" : "FAIL")} missing={capitalsMissing}");
            sb.AppendLine(
                $"V1041_Capitals rouge initial (v1_040): {(!initialRed.ok ? "ROUGE_OK" : "UNEXPECTED_GREEN")} missing={initialRed.missing}");
            sb.AppendLine(
                $"V1041_Overlap_Still_Zero: {(countryOverlaps == 0 && provinceOverlaps == 0 ? "PASS" : "FAIL")} " +
                $"country={countryOverlaps} province={provinceOverlaps}");
            sb.AppendLine(
                $"V1041_Label_Count_Floor: {(floorOk ? "PASS" : "FAIL")} " +
                $"drawn={countryCityLabels} eligible={countryEligible} ceiling_omit={CountryCityOmitCeiling}");
            sb.AppendLine($"V1041_Order_Is_Deterministic: {(detOk ? "PASS" : "FAIL")}");
            sb.AppendLine();

            sb.AppendLine("=== DETERMINISME SHA ===");
            sb.AppendLine($"country_cold_sha256={shaCold}");
            sb.AppendLine($"country_hot_sha256={shaHot}");
            sb.AppendLine($"cold_hot_match={shaCold == shaHot}");
            sb.AppendLine();

            var pass =
                capitalsOk &&
                countryOverlaps == 0 && provinceOverlaps == 0 &&
                floorOk && detOk &&
                shaCold == shaHot && shaCold.Length > 0 &&
                !initialRed.ok;

            sb.AppendLine("=== VERDICT MESURE ===");
            sb.AppendLine(
                $"{(pass ? "PASS" : "FAIL")}: PARIS et DIJON lisibles sur capture, " +
                $"0 chevauchement maintenu, country FRA : {countryDrawn} noms dessinés / " +
                $"{countryMoved} déplacés / {countryDisplaced} délogés / {countryOmitted} renoncés " +
                $"(city_labels={countryCityLabels}/{countryEligible}, avant v1_040: 2 dessinés / 7 renoncés), " +
                $"renoncées nommées [{countryOmittedNames}], " +
                $"V1041_Capitals rouge avant correction puis vert, froid==chaud SHA256.");

            File.WriteAllText(logPath, sb.ToString(), Encoding.UTF8);
            Debug.Log(sb.ToString());

            Assert.IsTrue(capitalsOk, "Capitales/provinces manquantes: " + capitalsMissing);
            Assert.AreEqual(0, countryOverlaps);
            Assert.AreEqual(0, provinceOverlaps);
            Assert.IsTrue(floorOk);
            Assert.IsTrue(detOk);
            Assert.AreEqual(shaCold, shaHot);
            Assert.IsFalse(initialRed.ok, "Le rouge initial doit mordre (v1_040 omet des capitales).");
        }

        static (bool ok, string missing) ProbeCapitalsWithQueue(bool enabled)
        {
            MapLabelLayout.UseImportanceQueue = enabled;
            MapLabelLayout.CollisionEnabled = true;
            MapLabelLayout.LegacyCityLabels = false;
            using var harness = new SimulationHarness(Seed);
            harness.RunTicks(CaptureTick);
            var em = harness.EntityManager;
            MapViewport.Reset();
            MapGeometryCache.ResetStatsAndClear();
            var worldGeo = MapGeometryCache.GetOrBuild(
                MapSnapshotExporter.Width, MapSnapshotExporter.Height, null, out _);
            MapViewport.EnsureWorldWindow(worldGeo);
            Assert.IsTrue(MapDisplaySystem.TrySelectCountryByTag(em, "FRA"));
            var countryGeo = MapGeometryCache.GetOrBuild(
                MapSnapshotExporter.Width, MapSnapshotExporter.Height,
                MapViewport.State.Window, out _);
            MapSnapshotExporter.RenderPoliticalPixels(
                em, countryGeo, MapSnapshotExporter.LabelDensity.Provinces, -1,
                overlay: p => CityMarkerComposer.Compose(
                    p, countryGeo, em, MapObservationLevel.Country,
                    filterCountryId: MapViewport.State.TargetCountryId));
            var countryPlaced = new List<MapPlacedLabel>(MapLabelLayout.LastPlaced);

            Assert.IsTrue(MapDisplaySystem.TrySelectProvinceById(em, BourgogneProvinceId));
            var bourgGeo = MapGeometryCache.GetOrBuild(
                MapSnapshotExporter.Width, MapSnapshotExporter.Height,
                MapViewport.State.Window, out _);
            MapSnapshotExporter.RenderPoliticalPixels(
                em, bourgGeo, MapSnapshotExporter.LabelDensity.SelectedProvince,
                BourgogneProvinceId,
                overlay: p => CityMarkerComposer.Compose(
                    p, bourgGeo, em, MapObservationLevel.Province,
                    filterProvinceId: BourgogneProvinceId));
            var bourgPlaced = new List<MapPlacedLabel>(MapLabelLayout.LastPlaced);

            Assert.IsTrue(MapDisplaySystem.TrySelectProvinceById(em, IleDeFranceProvinceId));
            var idfGeo = MapGeometryCache.GetOrBuild(
                MapSnapshotExporter.Width, MapSnapshotExporter.Height,
                MapViewport.State.Window, out _);
            MapSnapshotExporter.RenderPoliticalPixels(
                em, idfGeo, MapSnapshotExporter.LabelDensity.SelectedProvince,
                IleDeFranceProvinceId,
                overlay: p => CityMarkerComposer.Compose(
                    p, idfGeo, em, MapObservationLevel.Province,
                    filterProvinceId: IleDeFranceProvinceId));
            var idfPlaced = new List<MapPlacedLabel>(MapLabelLayout.LastPlaced);

            MapLabelLayout.UseImportanceQueue = true;
            var ok = CheckCapitalsPresent(countryPlaced, bourgPlaced, idfPlaced, out var missing);
            return (ok, missing);
        }

        static bool RunCapitalsCheck(bool writeLog)
        {
            MapLabelLayout.UseImportanceQueue = true;
            MapLabelLayout.CollisionEnabled = true;
            MapLabelLayout.LegacyCityLabels = false;
            var r = ProbeCapitalsWithQueue(enabled: true);
            return r.ok;
        }

        static bool RunOverlapCheck()
        {
            MapLabelLayout.UseImportanceQueue = true;
            MapLabelLayout.CollisionEnabled = true;
            MapLabelLayout.LegacyCityLabels = false;
            using var harness = new SimulationHarness(Seed);
            harness.RunTicks(CaptureTick);
            var em = harness.EntityManager;
            MapViewport.Reset();
            MapGeometryCache.ResetStatsAndClear();
            var worldGeo = MapGeometryCache.GetOrBuild(
                MapSnapshotExporter.Width, MapSnapshotExporter.Height, null, out _);
            MapViewport.EnsureWorldWindow(worldGeo);
            Assert.IsTrue(MapDisplaySystem.TrySelectCountryByTag(em, "FRA"));
            var countryGeo = MapGeometryCache.GetOrBuild(
                MapSnapshotExporter.Width, MapSnapshotExporter.Height,
                MapViewport.State.Window, out _);
            MapSnapshotExporter.RenderPoliticalPixels(
                em, countryGeo, MapSnapshotExporter.LabelDensity.Provinces, -1,
                overlay: p => CityMarkerComposer.Compose(
                    p, countryGeo, em, MapObservationLevel.Country,
                    filterCountryId: MapViewport.State.TargetCountryId));
            var cOver = MapLabelLayout.CountTextOverlaps();

            Assert.IsTrue(MapDisplaySystem.TrySelectProvinceById(em, IleDeFranceProvinceId));
            var provGeo = MapGeometryCache.GetOrBuild(
                MapSnapshotExporter.Width, MapSnapshotExporter.Height,
                MapViewport.State.Window, out _);
            MapSnapshotExporter.RenderPoliticalPixels(
                em, provGeo, MapSnapshotExporter.LabelDensity.Provinces, -1,
                overlay: p => CityMarkerComposer.Compose(
                    p, provGeo, em, MapObservationLevel.Province,
                    filterProvinceId: IleDeFranceProvinceId));
            var pOver = MapLabelLayout.CountTextOverlaps();
            return cOver == 0 && pOver == 0;
        }

        static bool RunLabelFloorCheck()
        {
            MapLabelLayout.UseImportanceQueue = true;
            MapLabelLayout.CollisionEnabled = true;
            MapLabelLayout.LegacyCityLabels = false;
            using var harness = new SimulationHarness(Seed);
            harness.RunTicks(CaptureTick);
            var em = harness.EntityManager;
            MapViewport.Reset();
            MapGeometryCache.ResetStatsAndClear();
            var worldGeo = MapGeometryCache.GetOrBuild(
                MapSnapshotExporter.Width, MapSnapshotExporter.Height, null, out _);
            MapViewport.EnsureWorldWindow(worldGeo);
            Assert.IsTrue(MapDisplaySystem.TrySelectCountryByTag(em, "FRA"));
            var countryGeo = MapGeometryCache.GetOrBuild(
                MapSnapshotExporter.Width, MapSnapshotExporter.Height,
                MapViewport.State.Window, out _);
            var eligible = CountEligibleCityLabels(
                em, MapObservationLevel.Country, MapViewport.State.TargetCountryId, -1);
            MapSnapshotExporter.RenderPoliticalPixels(
                em, countryGeo, MapSnapshotExporter.LabelDensity.Provinces, -1,
                overlay: p => CityMarkerComposer.Compose(
                    p, countryGeo, em, MapObservationLevel.Country,
                    filterCountryId: MapViewport.State.TargetCountryId));
            return CityMarkerComposer.LastLabelsDrawn >= eligible - CountryCityOmitCeiling;
        }

        static bool RunDeterminismCheck()
        {
            MapLabelLayout.UseImportanceQueue = true;
            MapLabelLayout.CollisionEnabled = true;
            MapLabelLayout.LegacyCityLabels = false;
            using var harness = new SimulationHarness(Seed);
            harness.RunTicks(CaptureTick);
            var em = harness.EntityManager;
            MapViewport.Reset();
            MapGeometryCache.ResetStatsAndClear();
            var worldGeo = MapGeometryCache.GetOrBuild(
                MapSnapshotExporter.Width, MapSnapshotExporter.Height, null, out _);
            MapViewport.EnsureWorldWindow(worldGeo);
            Assert.IsTrue(MapDisplaySystem.TrySelectCountryByTag(em, "FRA"));
            var countryGeo = MapGeometryCache.GetOrBuild(
                MapSnapshotExporter.Width, MapSnapshotExporter.Height,
                MapViewport.State.Window, out _);
            return CheckDeterministicSequence(em, countryGeo);
        }

        static bool CheckDeterministicSequence(
            EntityManager em, MapSnapshotExporter.MapGeometry countryGeo)
        {
            MapSnapshotExporter.RenderPoliticalPixels(
                em, countryGeo, MapSnapshotExporter.LabelDensity.Provinces, -1,
                overlay: p => CityMarkerComposer.Compose(
                    p, countryGeo, em, MapObservationLevel.Country,
                    filterCountryId: MapViewport.State.TargetCountryId));
            var a = SnapshotPlaced(MapLabelLayout.LastPlaced);
            MapSnapshotExporter.RenderPoliticalPixels(
                em, countryGeo, MapSnapshotExporter.LabelDensity.Provinces, -1,
                overlay: p => CityMarkerComposer.Compose(
                    p, countryGeo, em, MapObservationLevel.Country,
                    filterCountryId: MapViewport.State.TargetCountryId));
            var b = SnapshotPlaced(MapLabelLayout.LastPlaced);
            if (a.Count != b.Count) return false;
            for (var i = 0; i < a.Count; i++)
            {
                if (a[i] != b[i]) return false;
            }

            return a.Count > 0;
        }

        static List<string> SnapshotPlaced(IReadOnlyList<MapPlacedLabel> placed)
        {
            var list = new List<string>(placed.Count);
            for (var i = 0; i < placed.Count; i++)
            {
                var p = placed[i];
                list.Add(
                    $"{p.Kind}:{p.Id}:{p.Slot}:{p.Rect.X0},{p.Rect.Y0},{p.Rect.X1},{p.Rect.Y1}");
            }

            return list;
        }

        static bool CheckCapitalsPresent(
            List<MapPlacedLabel> country,
            List<MapPlacedLabel> bourgogne,
            List<MapPlacedLabel> idf,
            out string missing)
        {
            var miss = new List<string>();
            if (!HasCity(country, ParisCityId))
                miss.Add("PARIS@country");
            if (!HasCity(bourgogne, DijonCityId))
                miss.Add("DIJON@bourgogne");
            if (!HasProvince(bourgogne, BourgogneProvinceId))
                miss.Add("BOURGOGNE@province");
            if (!HasCity(idf, ParisCityId))
                miss.Add("PARIS@idf");
            if (!HasProvince(idf, IleDeFranceProvinceId))
                miss.Add("ILE_DE_FRANCE@province");
            // Diagnostic : contenus LastPlaced
            if (miss.Count > 0)
            {
                miss.Add("diag_country=" + SummarizePlaced(country));
                miss.Add("diag_bourg=" + SummarizePlaced(bourgogne));
                miss.Add("diag_idf=" + SummarizePlaced(idf));
            }

            missing = string.Join(",", miss);
            return miss.Count == 0;
        }

        static string SummarizePlaced(List<MapPlacedLabel> placed)
        {
            if (placed == null || placed.Count == 0) return "empty";
            var sb = new StringBuilder(placed.Count * 8);
            for (var i = 0; i < placed.Count; i++)
            {
                if (i > 0) sb.Append('|');
                sb.Append(placed[i].Kind).Append(':').Append(placed[i].Id);
            }

            return sb.ToString();
        }

        static bool HasCity(List<MapPlacedLabel> placed, int cityId)
        {
            if (placed == null) return false;
            for (var i = 0; i < placed.Count; i++)
            {
                if (placed[i].Kind == MapLabelKind.City && placed[i].Id == cityId)
                    return true;
            }

            return false;
        }

        static bool HasProvince(List<MapPlacedLabel> placed, int provinceId)
        {
            if (placed == null) return false;
            for (var i = 0; i < placed.Count; i++)
            {
                if (placed[i].Kind == MapLabelKind.Province && placed[i].Id == provinceId)
                    return true;
            }

            return false;
        }

        static int CountEligibleCityLabels(
            EntityManager em, MapObservationLevel level, int filterCountryId, int filterProvinceId)
        {
            var provinceOwner = new Dictionary<int, int>(64);
            using (var q = em.CreateEntityQuery(
                       ComponentType.ReadOnly<VictoriaGame.World.ProvinceData>(),
                       ComponentType.ReadOnly<VictoriaGame.World.ProvinceOwnership>()))
            using (var pdata = q.ToComponentDataArray<VictoriaGame.World.ProvinceData>(
                       Unity.Collections.Allocator.Temp))
            using (var owns = q.ToComponentDataArray<VictoriaGame.World.ProvinceOwnership>(
                       Unity.Collections.Allocator.Temp))
            {
                for (var i = 0; i < pdata.Length; i++)
                {
                    var cid = -1;
                    var owner = owns[i].Owner;
                    if (owner != Entity.Null &&
                        em.HasComponent<VictoriaGame.Core.CountryData>(owner))
                        cid = em.GetComponentData<VictoriaGame.Core.CountryData>(owner).CountryId;
                    provinceOwner[pdata[i].ProvinceId] = cid;
                }
            }

            var n = 0;
            using var cq = em.CreateEntityQuery(ComponentType.ReadOnly<CityData>());
            using var cities = cq.ToComponentDataArray<CityData>(Unity.Collections.Allocator.Temp);
            for (var i = 0; i < cities.Length; i++)
            {
                var city = cities[i];
                if (filterProvinceId >= 0 && city.ProvinceId != filterProvinceId)
                    continue;
                if (filterCountryId >= 0)
                {
                    if (!provinceOwner.TryGetValue(city.ProvinceId, out var cid) ||
                        cid != filterCountryId)
                        continue;
                }

                if (!CityMarkerVisibility.IncludeCity(level, city))
                    continue;
                if (!MapLabelVisibility.IncludeCityLabel(level, city))
                    continue;
                if (!CityCoordinates.TryGet(city.CityId, out _))
                    continue;
                n++;
            }

            return n;
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
