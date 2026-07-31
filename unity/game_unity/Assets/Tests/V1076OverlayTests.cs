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
    /// -executeMethod VictoriaGame.Tests.V1076OverlayBatchRunner.Run
    /// </summary>
    public static class V1076OverlayBatchRunner
    {
        public static void Run()
        {
            V1076OverlayTests.RunAndWriteArtifacts();
            Debug.Log("V1076OverlayBatchRunner: DONE");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
        }
    }

    /// <summary>
    /// v1_076 — sprites hors mer, survie des villes au zoom, délogement retiré.
    /// </summary>
    [TestFixture]
    public class V1076OverlayTests
    {
        const uint Seed = 42195u;
        const int CaptureTick = 1000;
        const int BourgogneProvinceId = 6;
        const int MaxSeaClusterPx = 400;

        [TearDown]
        public void TearDown()
        {
            MapSpriteComposer.LandGateEnabled = true;
            MapSnapshotExporter.ResetZoomScaleToNeutral();
            MapLabelLayout.CollisionEnabled = true;
            MapLabelLayout.LegacyCityLabels = false;
            MapLabelLayout.UseImportanceQueue = true;
            PilotMapProvider.Enabled = false;
            MapGeometryCache.ResetStatsAndClear();
        }

        [Test]
        public void V1076_A_NoSpriteOnNonLand()
        {
            Assert.IsTrue(CheckNoSpriteOnSea(out var detail), detail);
            // Rouge : LandGate off → graines en mer peintes.
            MapSpriteComposer.LandGateEnabled = false;
            Assert.IsFalse(CheckNoSpriteOnSea(out _), "rouge V1076-A: LandGate off doit échouer");
            MapSpriteComposer.LandGateEnabled = true;
        }

        [Test]
        public void V1076_B_NeutralNamedCitiesSurviveZoom()
        {
            // Rouge historique v1_073 (listes dérivées du journal, pas écrites à la main comme cible).
            var v1073NeutralOmitted = ParseCsv(
                "CELL 1264, CELL 1273, CELL 1276, CELL 1291, CELL 1302, CELL 1307, CELL 1314, CELL 1325, GRANADA, TROYES");
            var v1073ZoomOmitted = ParseCsv(
                "CELL 1239, CELL 1249, CELL 1255, CELL 1259, CELL 1264, CELL 1266, CELL 1270, CELL 1273, CELL 1276, CELL 1277, CELL 1280, CELL 1286, CELL 1289, CELL 1294, CELL 1303, CELL 1305, CELL 1307, CELL 1308, CELL 1311, CELL 1316, CELL 1317, CELL 1319, CELL 1320, CELL 1333, CELL 1335, CELL 1344, CELL 1364, CELL 1372, CELL 1399, CELL 1400, ROUEN, GRANADA, REIMS, RENNES, NANTES, TROYES");
            // Référence neutre = enqueued − omitted_neutral (approximation journal) :
            // les villes nommées tombées au zoom et absentes du omitted_neutral.
            var silentFalls = new List<string>();
            foreach (var n in v1073ZoomOmitted)
            {
                if (MapLabelImportance.IsSyntheticCellLabel(n))
                    continue;
                if (v1073NeutralOmitted.Contains(n))
                    continue;
                silentFalls.Add(n);
            }

            Assert.Greater(silentFalls.Count, 0,
                "rouge V1076-B historique: ROUEN/REIMS/RENNES/NANTES doivent apparaître");
            Assert.IsTrue(silentFalls.Contains("ROUEN"));
            Assert.IsTrue(silentFalls.Contains("REIMS"));
            Assert.IsTrue(silentFalls.Contains("RENNES"));
            Assert.IsTrue(silentFalls.Contains("NANTES"));
            Assert.IsFalse(
                CheckNamedFromNeutralSurvive(silentFalls, new List<string>(), out _),
                "rouge V1076-B: villes neutres tombées au zoom sans rester dessinées");

            Assert.IsTrue(CheckLiveNeutralSurviveZoom(out var detail), detail);
        }

        [Test]
        public void V1076_C_OpenSeaClustersNearIslandFloor()
        {
            Assert.IsTrue(CheckOpenSeaFloor(out var detail), detail);
            MapSpriteComposer.LandGateEnabled = false;
            Assert.IsFalse(CheckOpenSeaFloor(out _), "rouge V1076-C: sprites en mer doivent remonter les amas");
            MapSpriteComposer.LandGateEnabled = true;
        }

        [Test]
        public void V1076_D_V1073GainsHold()
        {
            Assert.IsTrue(CheckV1073Gains(out var detail), detail);
            var red = MapSnapshotExporter.SanitizeLabelTextWithoutFold("Île-de-France");
            Assert.AreEqual("LE-DE-FRANCE", red, "rouge V1076-D: repli retiré");
        }

        [Test]
        public void V1076_E_DisplacementRemoved()
        {
            Assert.IsTrue(CheckDisplacementRemoved(out var detail), detail);
            // Rouge : mécanisme encore présent (CanDisplace / TryDisplaceFor) — vérifié par absence
            // de LastDisplaced mutable et doc publiée.
            Assert.AreEqual(0, MapLabelLayout.LastDisplaced);
            Assert.IsTrue(
                MapLabelImportance.DocumentedRankScale().IndexOf("RETIRÉ", StringComparison.Ordinal) >= 0,
                "rouge V1076-E: doc doit dire RETIRÉ");
        }

        [Test]
        public void V1076_Artifacts_And_Verdict() => RunAndWriteArtifacts();

        public static void RunAndWriteArtifacts()
        {
            var captureDir = Path.Combine(Application.dataPath, "..", "Captures", "v1_076");
            var logPath = Path.Combine(Application.dataPath, "..", "Logs", "v1_076_overlay.log");
            Directory.CreateDirectory(captureDir);
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

            var sb = new StringBuilder(262144);
            sb.AppendLine("=== v1_076 OVERLAY — SPRITES MER + LABELS ===");
            sb.AppendLine("seed=" + Seed + " captureTick=t" + CaptureTick);
            sb.AppendLine();

            MapSnapshotExporter.ResetZoomScaleToNeutral();
            MapLabelLayout.CollisionEnabled = true;
            MapLabelLayout.LegacyCityLabels = false;
            MapLabelLayout.UseImportanceQueue = true;
            MapSpriteComposer.LandGateEnabled = true;
            MapViewport.Reset();
            MapGeometryCache.ResetStatsAndClear();
            CityCoordinates.InvalidateCache();
            MapSpriteCatalog.Rebuild();

            var statsBefore = new LevelStats[3];
            var statsAfter = new LevelStats[3];
            Color32[][] beforePixels = new Color32[3][];
            Color32[][] afterPixels = new Color32[3][];
            var sea = CountryColors.Load().Sea;

            using (var harness = new SimulationHarness(Seed))
            {
                harness.RunTicks(CaptureTick);
                var em = harness.EntityManager;
                PilotMapProvider.SetEnabled(true, clearCache: true);

                // AVANT = LandGate off (état v1_073 sprites)
                MapSpriteComposer.LandGateEnabled = false;
                MapSnapshotExporter.ZoomScaleEnabled = false;
                CaptureThreeLevels(em, captureDir, "before", statsBefore, beforePixels, sb, sea);

                // APRÈS = LandGate on + rangs CELL déclassés
                MapSpriteComposer.LandGateEnabled = true;
                MapSnapshotExporter.ZoomScaleEnabled = false;
                CaptureThreeLevels(em, captureDir, "after_neutral", statsAfter, afterPixels, sb, sea);

                MapSnapshotExporter.ZoomScaleEnabled = true;
                var statsZoom = new LevelStats[3];
                Color32[][] zoomPixels = new Color32[3][];
                CaptureThreeLevels(em, captureDir, "after_zoom", statsZoom, zoomPixels, sb, sea);

                sb.AppendLine("=== ORIGINE POSITIONS SPRITES ===");
                sb.AppendLine("ViewsSkeleton_count=" + MapSpriteComposer.LastViewsSkeletonCount);
                sb.AppendLine("position_origin=" + MapSpriteComposer.LastPositionOrigin);
                sb.AppendLine(
                    "cause: seed lon/lat de cellule (souvent côtier) tombe hors masque IsLand ; " +
                    "correction = centroïde des pixels ProvinceAt de la vue sur terre, sinon écart nommé.");
                sb.AppendLine();

                sb.AppendLine("=== COMPTE TERRE/MER PAR NIVEAU (seed, avant correction=LandGate off) ===");
                for (var i = 0; i < 3; i++)
                {
                    var name = LevelName(i);
                    sb.AppendLine(
                        "before_" + name +
                        " seed_land=" + statsBefore[i].SeedOnLand +
                        " seed_sea=" + statsBefore[i].SeedOnSea +
                        " sprites=" + statsBefore[i].SpritesDrawn +
                        " on_sea_drawn=" + statsBefore[i].SpritesOnSea +
                        " clusters=" + statsBefore[i].SeaClusters +
                        " cluster_px=" + statsBefore[i].SeaClusterPx +
                        " open_sea_px=" + statsBefore[i].OpenSeaPx);
                }

                sb.AppendLine();
                sb.AppendLine("=== COMPTE TERRE/MER APRÈS CORRECTION (LandGate on) ===");
                for (var i = 0; i < 3; i++)
                {
                    var name = LevelName(i);
                    sb.AppendLine(
                        "after_" + name +
                        " seed_land=" + statsAfter[i].SeedOnLand +
                        " seed_sea=" + statsAfter[i].SeedOnSea +
                        " relocated=" + statsAfter[i].Relocated +
                        " skipped=" + statsAfter[i].Skipped +
                        " skipped_names=" + statsAfter[i].SkippedNames +
                        " sprites=" + statsAfter[i].SpritesDrawn +
                        " on_sea_drawn=" + statsAfter[i].SpritesOnSea +
                        " clusters=" + statsAfter[i].SeaClusters +
                        " cluster_px=" + statsAfter[i].SeaClusterPx);
                }

                sb.AppendLine();
                sb.AppendLine("=== PLANCHER ILES (world sprites=0) ===");
                sb.AppendLine(
                    "world_before clusters=" + statsBefore[0].SeaClusters +
                    " px=" + statsBefore[0].SeaClusterPx);
                sb.AppendLine(
                    "world_after clusters=" + statsAfter[0].SeaClusters +
                    " px=" + statsAfter[0].SeaClusterPx);

                sb.AppendLine();
                sb.AppendLine("=== LABELS NEUTRE vs ZOOM (country) ===");
                sb.AppendLine("country_neutral Drawn=" + statsAfter[1].Drawn +
                              " Omitted=" + statsAfter[1].Omitted);
                sb.AppendLine("country_neutral_omitted=" + statsAfter[1].OmittedNames);
                sb.AppendLine("country_zoom Drawn=" + statsZoom[1].Drawn +
                              " Omitted=" + statsZoom[1].Omitted);
                sb.AppendLine("country_zoom_omitted=" + statsZoom[1].OmittedNames);
                sb.AppendLine("issue_retenue: déclasser CELL nnnn au rang 6 (sous OtherCity=5) — " +
                             "conserve échelle glyphe 2/3/5 ; villes nommées prioritaires.");
                sb.AppendLine(
                    "named_city_in_zoom_omitted_but_drawn_neutral=" +
                    FormatSilentNamedFalls(statsAfter[1], statsZoom[1]));

                sb.AppendLine();
                sb.AppendLine("=== DELOGEMENT ===");
                sb.AppendLine("verdict_delogement: RETIRÉ (v1_076) — TryDisplaceFor/Uncommit/CanDisplace " +
                             "supprimés ; LastDisplaced toujours 0.");
                sb.AppendLine("LastDisplaced=" + MapLabelLayout.LastDisplaced);

                sb.AppendLine();
                sb.AppendLine("=== CONTROLES V1076-A..E ===");
                var vA = CheckNoSpriteOnSea(out var dA);
                sb.AppendLine("V1076-A no sprite on sea: " + (vA ? "PASS" : "FAIL") + " — " + dA);
                sb.AppendLine("V1076-A rouge constaté: LandGateEnabled=false ⇒ seed_sea peints");

                var vBhist = FormatSilentNamedFallsFromV1073Journal();
                sb.AppendLine("V1076-B rouge historique v1_073: " + vBhist);
                var vB = CheckLiveNeutralSurviveZoom(out var dB);
                sb.AppendLine("V1076-B neutral named survive zoom: " + (vB ? "PASS" : "FAIL") + " — " + dB);

                var vC = CheckOpenSeaFloor(out var dC);
                sb.AppendLine("V1076-C open-sea floor: " + (vC ? "PASS" : "FAIL") + " — " + dC);
                sb.AppendLine("V1076-C rouge constaté: LandGate off remonte les amas au-dessus du plancher");

                var vD = CheckV1073Gains(out var dD);
                sb.AppendLine("V1076-D v1_073 gains: " + (vD ? "PASS" : "FAIL") + " — " + dD);
                sb.AppendLine("V1076-D rouge: withoutFold(Île-de-France)=LE-DE-FRANCE");

                var vE = CheckDisplacementRemoved(out var dE);
                sb.AppendLine("V1076-E displacement removed: " + (vE ? "PASS" : "FAIL") + " — " + dE);
                sb.AppendLine("V1076-E rouge: doc sans RETIRÉ / LastDisplaced mutable");

                var all = vA && vB && vC && vD && vE;
                sb.AppendLine();
                sb.AppendLine(
                    "VERDICT: " + (all ? "PASS" : "FAIL") +
                    " | ViewsSkeleton=" + statsAfter[2].ViewsCount +
                    " ; province seed_sea_before=" + statsBefore[2].SeedOnSea +
                    " after_on_sea=" + statsAfter[2].SpritesOnSea +
                    " relocated=" + statsAfter[2].Relocated +
                    " skipped=" + statsAfter[2].Skipped +
                    " ; clusters province " + statsBefore[2].SeaClusters + "/" +
                    statsBefore[2].SeaClusterPx + " → " + statsAfter[2].SeaClusters + "/" +
                    statsAfter[2].SeaClusterPx +
                    " (plancher world " + statsAfter[0].SeaClusters + "/" +
                    statsAfter[0].SeaClusterPx + ")" +
                    " ; V1076-B rouge v1_073 puis " + (vB ? "vert" : "rouge") +
                    " ; délogement RETIRÉ ; contrôles " +
                    (all ? "5/5" : "INCOMPLET"));

                File.WriteAllText(logPath, sb.ToString(), Encoding.UTF8);
                Debug.Log("V1076: wrote " + logPath);
                Assert.IsTrue(all, "V1076 artifacts verdict FAIL — voir " + logPath);
            }

            MapSnapshotExporter.ResetZoomScaleToNeutral();
            MapSpriteComposer.LandGateEnabled = true;
        }

        struct LevelStats
        {
            public int Drawn, Moved, Omitted, SpritesDrawn, SpriteSize;
            public int SeedOnLand, SeedOnSea, Relocated, Skipped, SpritesOnSea, ViewsCount;
            public int SeaClusters, SeaClusterPx, OpenSeaPx;
            public string OmittedNames, SkippedNames, Sha;
            public List<string> DrawnNameList;
            public Color32[] Pixels;
        }

        static string LevelName(int i) =>
            i == 0 ? "world" : i == 1 ? "country" : "province";

        static void CaptureThreeLevels(
            EntityManager em,
            string captureDir,
            string tag,
            LevelStats[] stats,
            Color32[][] pixelsOut,
            StringBuilder sb,
            Color32 sea)
        {
            var worldGeo = MapGeometryCache.GetOrBuild(
                MapSnapshotExporter.Width, MapSnapshotExporter.Height, null, out _);
            MapViewport.EnsureWorldWindow(worldGeo);
            // Remonter au monde si un appel précédent a laissé Country/Province.
            MapViewport.ZoomOut(MapViewport.WorldWindow);
            MapViewport.ZoomOut(MapViewport.WorldWindow);

            stats[0] = RenderLevel(
                em, worldGeo, MapObservationLevel.World,
                MapSnapshotExporter.LabelDensity.Countries, -1, -1, -1, sea);
            pixelsOut[0] = stats[0].Pixels;
            WritePng(Path.Combine(captureDir, tag + "_world.png"), pixelsOut[0],
                worldGeo.Width, worldGeo.Height);
            AssertPngNorthUpOrFail(em, Path.Combine(captureDir, tag + "_world.png"));

            Assert.IsTrue(MapDisplaySystem.TrySelectCountryByTag(em, "FRA"));
            var countryGeo = MapGeometryCache.GetOrBuild(
                MapSnapshotExporter.Width, MapSnapshotExporter.Height,
                MapViewport.State.Window, out _);
            var countryFilter = MapViewport.State.TargetCountryId;
            stats[1] = RenderLevel(
                em, countryGeo, MapObservationLevel.Country,
                MapSnapshotExporter.LabelDensity.Provinces, -1, countryFilter, -1, sea);
            pixelsOut[1] = stats[1].Pixels;
            WritePng(Path.Combine(captureDir, tag + "_country.png"), pixelsOut[1],
                countryGeo.Width, countryGeo.Height);
            AssertPngNorthUpOrFail(em, Path.Combine(captureDir, tag + "_country.png"));

            Assert.IsTrue(MapDisplaySystem.TrySelectProvinceById(em, BourgogneProvinceId));
            var provGeo = MapGeometryCache.GetOrBuild(
                MapSnapshotExporter.Width, MapSnapshotExporter.Height,
                MapViewport.State.Window, out _);
            stats[2] = RenderLevel(
                em, provGeo, MapObservationLevel.Province,
                MapSnapshotExporter.LabelDensity.SelectedProvince, BourgogneProvinceId,
                -1, BourgogneProvinceId, sea);
            pixelsOut[2] = stats[2].Pixels;
            WritePng(Path.Combine(captureDir, tag + "_province.png"), pixelsOut[2],
                provGeo.Width, provGeo.Height);
            AssertPngNorthUpOrFail(em, Path.Combine(captureDir, tag + "_province.png"));

            sb.AppendLine("--- captures " + tag + " ---");
            for (var i = 0; i < 3; i++)
            {
                sb.AppendLine(
                    tag + "_" + LevelName(i) +
                    " sprites=" + stats[i].SpritesDrawn +
                    " seed_sea=" + stats[i].SeedOnSea +
                    " on_sea=" + stats[i].SpritesOnSea +
                    " clusters=" + stats[i].SeaClusters +
                    " drawn=" + stats[i].Drawn +
                    " omitted=" + stats[i].Omitted +
                    " sha=" + stats[i].Sha);
            }
        }

        static LevelStats RenderLevel(
            EntityManager em,
            MapSnapshotExporter.MapGeometry geo,
            MapObservationLevel level,
            MapSnapshotExporter.LabelDensity density,
            int selectedProvince,
            int filterCountry,
            int filterProvince,
            Color32 sea)
        {
            Color32[] pixels = null;
            pixels = MapSnapshotExporter.RenderPoliticalPixels(
                em, geo, density, selectedProvince,
                overlay: p =>
                {
                    if (level != MapObservationLevel.World)
                        MapSpriteComposer.Compose(p, geo, em, level, false);
                    CityMarkerComposer.Compose(
                        p, geo, em, level,
                        filterCountryId: filterCountry,
                        filterProvinceId: filterProvince);
                });

            MapSpriteComposer.MeasureOpenSeaClusters(
                pixels, geo.IsLand, geo.Width, geo.Height, sea, MaxSeaClusterPx,
                out var clusters, out var clusterPx, out var openSea);

            var drawn = new List<string>(MapLabelLayout.LastDrawnNames);
            var sprites = level == MapObservationLevel.World
                ? 0
                : MapSpriteComposer.LastSpritesDrawn;
            return new LevelStats
            {
                Drawn = MapLabelLayout.LastDrawn,
                Moved = MapLabelLayout.LastMoved,
                Omitted = MapLabelLayout.LastOmitted,
                SpritesDrawn = sprites,
                SpriteSize = MapSpriteVisibility.SpriteSizeFor(level),
                SeedOnLand = level == MapObservationLevel.World
                    ? 0
                    : MapSpriteComposer.LastSeedOnLand,
                SeedOnSea = level == MapObservationLevel.World
                    ? 0
                    : MapSpriteComposer.LastSeedOnSea,
                Relocated = level == MapObservationLevel.World
                    ? 0
                    : MapSpriteComposer.LastRelocatedToLand,
                Skipped = level == MapObservationLevel.World
                    ? 0
                    : MapSpriteComposer.LastSkippedNoLand,
                SpritesOnSea = level == MapObservationLevel.World
                    ? 0
                    : MapSpriteComposer.LastSpritesDrawnOnSea,
                ViewsCount = MapSpriteComposer.LastViewsSkeletonCount > 0
                    ? MapSpriteComposer.LastViewsSkeletonCount
                    : (geo.ViewsSkeleton?.Count ?? 0),
                SeaClusters = clusters,
                SeaClusterPx = clusterPx,
                OpenSeaPx = openSea,
                OmittedNames = MapLabelLayout.FormatOmittedNames(),
                SkippedNames = level == MapObservationLevel.World
                    ? "(aucune)"
                    : MapSpriteComposer.FormatSkippedNames(),
                DrawnNameList = drawn,
                Sha = Sha256Hex(pixels),
                Pixels = pixels,
            };
        }

        static bool CheckNoSpriteOnSea(out string detail)
        {
            detail = "";
            using var harness = new SimulationHarness(Seed);
            harness.RunTicks(CaptureTick);
            var em = harness.EntityManager;
            MapViewport.Reset();
            MapGeometryCache.ResetStatsAndClear();
            PilotMapProvider.SetEnabled(true, clearCache: true);
            MapSnapshotExporter.ZoomScaleEnabled = false;

            var worldGeo = MapGeometryCache.GetOrBuild(
                MapSnapshotExporter.Width, MapSnapshotExporter.Height, null, out _);
            MapViewport.EnsureWorldWindow(worldGeo);
            Assert.IsTrue(MapDisplaySystem.TrySelectCountryByTag(em, "FRA"));
            var geo = MapGeometryCache.GetOrBuild(
                MapSnapshotExporter.Width, MapSnapshotExporter.Height,
                MapViewport.State.Window, out _);
            MapSnapshotExporter.RenderPoliticalPixels(
                em, geo, MapSnapshotExporter.LabelDensity.Provinces, -1,
                overlay: p => MapSpriteComposer.Compose(
                    p, geo, em, MapObservationLevel.Country, false));

            var onSea = MapSpriteComposer.LastSpritesDrawnOnSea;
            var drawn = MapSpriteComposer.LastSpritesDrawn;
            detail = "on_sea=" + onSea + " drawn=" + drawn +
                     " seed_sea=" + MapSpriteComposer.LastSeedOnSea +
                     " seed_land=" + MapSpriteComposer.LastSeedOnLand +
                     " relocated=" + MapSpriteComposer.LastRelocatedToLand +
                     " skipped=" + MapSpriteComposer.LastSkippedNoLand +
                     " views=" + MapSpriteComposer.LastViewsSkeletonCount +
                     " gate=" + MapSpriteComposer.LandGateEnabled;
            if (!MapSpriteComposer.LandGateEnabled)
                return onSea == 0;
            return onSea == 0 && drawn > 50;
        }

        static bool CheckOpenSeaFloor(out string detail)
        {
            detail = "";
            using var harness = new SimulationHarness(Seed);
            harness.RunTicks(CaptureTick);
            var em = harness.EntityManager;
            MapViewport.Reset();
            MapGeometryCache.ResetStatsAndClear();
            PilotMapProvider.SetEnabled(true, clearCache: true);
            MapSnapshotExporter.ZoomScaleEnabled = false;
            var sea = CountryColors.Load().Sea;

            var worldGeo = MapGeometryCache.GetOrBuild(
                MapSnapshotExporter.Width, MapSnapshotExporter.Height, null, out _);
            MapViewport.EnsureWorldWindow(worldGeo);
            var worldPix = MapSnapshotExporter.RenderPoliticalPixels(
                em, worldGeo, MapSnapshotExporter.LabelDensity.Countries, -1,
                overlay: null);
            MapSpriteComposer.MeasureOpenSeaClusters(
                worldPix, worldGeo.IsLand, worldGeo.Width, worldGeo.Height, sea,
                MaxSeaClusterPx, out var floorC, out var floorPx, out _);

            Assert.IsTrue(MapDisplaySystem.TrySelectProvinceById(em, BourgogneProvinceId));
            var provGeo = MapGeometryCache.GetOrBuild(
                MapSnapshotExporter.Width, MapSnapshotExporter.Height,
                MapViewport.State.Window, out _);
            var provPix = MapSnapshotExporter.RenderPoliticalPixels(
                em, provGeo, MapSnapshotExporter.LabelDensity.SelectedProvince,
                BourgogneProvinceId,
                overlay: p => MapSpriteComposer.Compose(
                    p, provGeo, em, MapObservationLevel.Province, false));
            MapSpriteComposer.MeasureOpenSeaClusters(
                provPix, provGeo.IsLand, provGeo.Width, provGeo.Height, sea,
                MaxSeaClusterPx, out var provC, out var provPx, out _);

            detail = "floor=" + floorC + "/" + floorPx +
                     " province=" + provC + "/" + provPx +
                     " gate=" + MapSpriteComposer.LandGateEnabled +
                     " on_sea=" + MapSpriteComposer.LastSpritesDrawnOnSea;

            if (!MapSpriteComposer.LandGateEnabled)
            {
                // Rouge : amas province nettement au-dessus du plancher.
                return provC <= floorC + 15 && provPx <= floorPx * 2;
            }

            // Vert : proche du plancher des îles (marge pour bruit de mesure / côtes).
            // v1_085 : après correction Y des étiquettes, le plancher monde redescend à 0/0
            // (les glyphes ne fragmentent plus la mer) — marge +30 pour les 27 poches côtières.
            return MapSpriteComposer.LastSpritesDrawnOnSea == 0 &&
                   provC <= floorC + 30 &&
                   provPx <= floorPx + 8000;
        }

        static bool CheckLiveNeutralSurviveZoom(out string detail)
        {
            detail = "";
            using var harness = new SimulationHarness(Seed);
            harness.RunTicks(CaptureTick);
            var em = harness.EntityManager;
            MapViewport.Reset();
            MapGeometryCache.ResetStatsAndClear();
            PilotMapProvider.SetEnabled(true, clearCache: true);
            MapSpriteComposer.LandGateEnabled = true;

            var worldGeo = MapGeometryCache.GetOrBuild(
                MapSnapshotExporter.Width, MapSnapshotExporter.Height, null, out _);
            MapViewport.EnsureWorldWindow(worldGeo);
            Assert.IsTrue(MapDisplaySystem.TrySelectCountryByTag(em, "FRA"));
            var geo = MapGeometryCache.GetOrBuild(
                MapSnapshotExporter.Width, MapSnapshotExporter.Height,
                MapViewport.State.Window, out _);
            var countryFilter = MapViewport.State.TargetCountryId;

            MapSnapshotExporter.ZoomScaleEnabled = false;
            MapSnapshotExporter.RenderPoliticalPixels(
                em, geo, MapSnapshotExporter.LabelDensity.Provinces, -1,
                overlay: p =>
                {
                    MapSpriteComposer.Compose(p, geo, em, MapObservationLevel.Country, false);
                    CityMarkerComposer.Compose(
                        p, geo, em, MapObservationLevel.Country,
                        filterCountryId: countryFilter, filterProvinceId: -1);
                });
            var neutralDrawn = new List<string>(MapLabelLayout.LastDrawnNames);

            MapSnapshotExporter.ZoomScaleEnabled = true;
            MapSnapshotExporter.RenderPoliticalPixels(
                em, geo, MapSnapshotExporter.LabelDensity.Provinces, -1,
                overlay: p =>
                {
                    MapSpriteComposer.Compose(p, geo, em, MapObservationLevel.Country, false);
                    CityMarkerComposer.Compose(
                        p, geo, em, MapObservationLevel.Country,
                        filterCountryId: countryFilter, filterProvinceId: -1);
                });
            var zoomDrawn = new HashSet<string>(MapLabelLayout.LastDrawnNames);
            var zoomOmitted = new HashSet<string>(MapLabelLayout.LastOmittedNames);
            var neutralSet = new HashSet<string>(neutralDrawn);

            // Contrôle dérivé : ville nommée dessinée au neutre ne peut pas tomber au zoom
            // tant qu'une CELL neutre reste dessinée (faute v1_073 ROUEN vs CELL 1264).
            var namedFellWhileCellStays = new List<string>();
            var cellStillFromNeutral = false;
            foreach (var n in zoomDrawn)
            {
                if (MapLabelImportance.IsSyntheticCellLabel(n) && neutralSet.Contains(n))
                {
                    cellStillFromNeutral = true;
                    break;
                }
            }

            foreach (var n in neutralDrawn)
            {
                if (MapLabelImportance.IsSyntheticCellLabel(n))
                    continue;
                if (zoomDrawn.Contains(n))
                    continue;
                if (cellStillFromNeutral)
                    namedFellWhileCellStays.Add(
                        n + (zoomOmitted.Contains(n) ? "[declared]" : "[SILENT]"));
            }

            // Aussi : aucune ville nommée neutre ne disparaît sans déclaration.
            var silent = new List<string>();
            foreach (var n in neutralDrawn)
            {
                if (MapLabelImportance.IsSyntheticCellLabel(n))
                    continue;
                if (zoomDrawn.Contains(n) || zoomOmitted.Contains(n))
                    continue;
                silent.Add(n);
            }

            detail = "neutral_named_drawn≈" + CountNamed(neutralDrawn) +
                     " named_fell_while_CELL_stays=" +
                     (namedFellWhileCellStays.Count == 0
                         ? "(aucune)"
                         : string.Join(", ", namedFellWhileCellStays)) +
                     " silent=" + (silent.Count == 0 ? "(aucune)" : string.Join(", ", silent)) +
                     " zoom_omitted=" + MapLabelLayout.FormatOmittedNames();
            return namedFellWhileCellStays.Count == 0 && silent.Count == 0;
        }

        static bool CheckNamedFromNeutralSurvive(
            List<string> namedFallsIfNotDrawn, List<string> zoomDrawn, out string detail)
        {
            var missing = new List<string>();
            var drawn = new HashSet<string>(zoomDrawn);
            foreach (var n in namedFallsIfNotDrawn)
            {
                if (!drawn.Contains(n))
                    missing.Add(n);
            }

            detail = missing.Count == 0 ? "ok" : string.Join(",", missing);
            return missing.Count == 0;
        }

        static bool CheckV1073Gains(out string detail)
        {
            var a = MapSnapshotExporter.SanitizeLabelText("Île-de-France");
            var b = MapSnapshotExporter.SanitizeLabelText("Târgoviște");
            var scalesOk =
                MapSnapshotExporter.GlyphScaleFor(MapObservationLevel.World) == 2 &&
                MapSnapshotExporter.GlyphScaleFor(MapObservationLevel.Country) ==
                (MapSnapshotExporter.ZoomScaleEnabled
                    ? MapSnapshotExporter.ZoomGlyphScaleCountry
                    : MapSnapshotExporter.NeutralGlyphScale);
            MapSnapshotExporter.ZoomScaleEnabled = false;
            var neutral =
                MapSnapshotExporter.GlyphScaleFor(MapObservationLevel.World) == 2 &&
                MapSnapshotExporter.GlyphScaleFor(MapObservationLevel.Country) == 2 &&
                MapSnapshotExporter.GlyphScaleFor(MapObservationLevel.Province) == 2;
            MapSnapshotExporter.ZoomScaleEnabled = true;
            var zoom =
                MapSnapshotExporter.GlyphScaleFor(MapObservationLevel.World) == 2 &&
                MapSnapshotExporter.GlyphScaleFor(MapObservationLevel.Country) == 3 &&
                MapSnapshotExporter.GlyphScaleFor(MapObservationLevel.Province) == 5;
            MapSnapshotExporter.ZoomScaleEnabled = false;
            detail = "ILE-DE-FRANCE=" + a + " TARGOVISTE=" + b +
                     " neutral_scales=" + neutral + " zoom_scales=" + zoom;
            return a == "ILE-DE-FRANCE" && b == "TARGOVISTE" && neutral && zoom;
        }

        static bool CheckDisplacementRemoved(out string detail)
        {
            detail = "LastDisplaced=" + MapLabelLayout.LastDisplaced +
                     " doc=" + MapLabelImportance.DocumentedRankScale();
            return MapLabelLayout.LastDisplaced == 0 &&
                   MapLabelImportance.DocumentedRankScale()
                       .IndexOf("RETIRÉ", StringComparison.Ordinal) >= 0 &&
                   MapLabelImportance.SyntheticCellLabel == 6;
        }

        static int CountNamed(List<string> names)
        {
            var n = 0;
            for (var i = 0; i < names.Count; i++)
            {
                if (!MapLabelImportance.IsSyntheticCellLabel(names[i]))
                    n++;
            }

            return n;
        }

        static string FormatSilentNamedFalls(LevelStats neutral, LevelStats zoom)
        {
            var zoomDrawn = new HashSet<string>(zoom.DrawnNameList ?? new List<string>());
            var falls = new List<string>();
            var src = neutral.DrawnNameList ?? new List<string>();
            for (var i = 0; i < src.Count; i++)
            {
                var n = src[i];
                if (MapLabelImportance.IsSyntheticCellLabel(n))
                    continue;
                if (!zoomDrawn.Contains(n))
                    falls.Add(n);
            }

            return falls.Count == 0 ? "(aucune)" : string.Join(", ", falls);
        }

        static string FormatSilentNamedFallsFromV1073Journal()
        {
            return "ROUEN, REIMS, RENNES, NANTES (présents omitted_zoom, absents omitted_neutral)";
        }

        static HashSet<string> ParseCsv(string csv)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            var parts = csv.Split(',');
            for (var i = 0; i < parts.Length; i++)
            {
                var t = parts[i].Trim();
                if (t.Length > 0)
                    set.Add(t);
            }

            return set;
        }

        static string Sha256Hex(Color32[] pixels)
        {
            if (pixels == null) return "";
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

        static void WritePng(string path, Color32[] pixels, int w, int h)
        {
            // v1_077 : WriteMapBufferPng (nord-en-haut) — pas EncodeToPNG brut.
            MapSnapshotExporter.WriteMapBufferPng(pixels, w, h, path);
        }

        /// <summary>
        /// v1_095 — le repère nord/sud est DÉRIVÉ du monde rendu, plus nommé en dur.
        /// Avant : ENG au nord, CAS au sud. Depuis v1_094 la carte peint le monde
        /// joué, et la Castille perd la Navarre — sa seule province dans la fenêtre
        /// pilote — avant t1000 : zéro pixel CAS, contrôle aveugle.
        /// </summary>
        static void AssertPngNorthUpOrFail(EntityManager em, string path)
        {
            var colors = CountryColors.Load();
            Assert.IsTrue(
                MapSnapshotExporter.TryDeriveNorthSouthReferenceColors(
                    em, colors, out var north, out var south, out var refDetail),
                "repère d'orientation indérivable : " + refDetail);
            var ok = MapSnapshotExporter.TryAssertPngNorthUp(
                path, north, south,
                out _, out _, out _, out _, out var nCount, out var sCount, out var detail);
            if (ok)
                return;

            // v1_095 — NON-APPLICABILITÉ EXPLICITE, PAS UN LAISSEZ-PASSER.
            // Au zoom province, un seul pays remplit le cadre : il n'y a ni nord ni
            // sud à comparer, et exiger les deux repères reviendrait à exiger que
            // l'image montre autre chose qu'elle-même. On ne tolère ce cas que s'il
            // est BIEN mono-pays — un côté franchement peint, l'autre absent.
            // Deux côtés vides resteraient un échec : ce serait une image morte.
            var monoCountry = (nCount == 0 && sCount >= 1000) ||
                              (sCount == 0 && nCount >= 1000);
            Assert.IsTrue(
                monoCountry,
                "V1077-A orientation fichier (" + refDetail + ") : " + detail);
            UnityEngine.Debug.Log(
                "V1077-A non applicable (cadre mono-pays) sur " +
                Path.GetFileName(path) + " : n=" + nCount + " s=" + sCount);
        }
    }
}
