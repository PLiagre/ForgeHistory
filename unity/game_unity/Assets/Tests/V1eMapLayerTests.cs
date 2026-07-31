using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using VictoriaGame.Presentation;

namespace VictoriaGame.Tests
{
    /// <summary>Point d'entrée batchmode : -executeMethod VictoriaGame.Tests.V1eMapLayerBatchRunner.Run</summary>
    public static class V1eMapLayerBatchRunner
    {
        public static void Run()
        {
            V1eMapLayerTests.RunAndWriteArtifacts();
            UnityEngine.Debug.Log("V1eMapLayerBatchRunner: DONE");
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
            #endif
        }
    }

    /// <summary>
    /// Harnais V1e — correctif v1_006 : snapshots Owner/Controller par tick,
    /// canari prov1/prov6, diffs political/tradenode t0≠t1000.
    /// </summary>
    [TestFixture]
    public class V1eMapLayerTests
    {
        const uint Seed = 42195u;

        const int RefNonCore = 18;
        const int RefCountries = 14;
        const int RefMaxProv = 11;
        const string RefDebt = "750.9";
        const int RefBankrupt = 4;
        const string RefArmy = "38953";
        const int RefZombie = 0;
        const string RefSat = "0.698";
        const int RefPop = 142551;
        const string RefRatioV800 = "72.5";
        const int RefStuck800 = 0;
        const int RefAnnexed800 = 15;

        [Test]
        public void V1e_ExportThematicLayers() => RunAndWriteArtifacts();

        public static void RunAndWriteArtifacts()
        {
            var layersDir = Path.Combine(
                UnityEngine.Application.dataPath, "..", "Logs", "v1_006_layers");
            var measurePath = Path.Combine(
                UnityEngine.Application.dataPath, "..", "Logs", "v1_006_measurements.log");

            Directory.CreateDirectory(layersDir);
            Directory.CreateDirectory(Path.GetDirectoryName(measurePath)!);

            var sb = new StringBuilder();
            sb.AppendLine($"=== v1_006 MapLayerRenderer seed={Seed} ===");
            sb.AppendLine(MapSnapshotExporter.FormatConstantsLine());
            sb.AppendLine(
                "CORRECTIF v1_006 : snapshot Owner/Controller TAG par tick ; " +
                "rendu sans EntityManager. Domaines FIXES v1_005. Canari + pixeldiff.");
            sb.AppendLine(
                "Presentation = classes statiques, hors SimulationSystemGroup. Aucune écriture ECS.");
            sb.AppendLine();

            MapLayerRenderer.FixedDomains domains;
            System.Collections.Generic.List<MapLayerRenderer.LayerStats> stats;
            System.Collections.Generic.List<MapLayerRenderer.CanaryResult> canaries;
            System.Collections.Generic.List<MapLayerRenderer.PixelDiffResult> pixelDiffs;
            var landOk = false;

            using (var harness = new SimulationHarness(Seed))
            {
                harness.RunTicks(0);
                domains = MapLayerRenderer.Run(
                    harness.EntityManager,
                    delta => harness.RunTicks(delta),
                    layersDir,
                    out stats,
                    out landOk,
                    out canaries,
                    out pixelDiffs);
            }

            sb.AppendLine(MapLayerRenderer.FormatDomainsLine(domains));
            sb.AppendLine($"GEOMETRY_BUILDS={MapLayerRenderer.GeometryBuildCount} (attendu=1)");
            sb.AppendLine(MapSnapshotExporter.LastLandMassReport.Summary);
            sb.AppendLine(MapSnapshotExporter.FormatMaskStatsLine());
            sb.AppendLine();

            sb.AppendLine("=== CANARY prov1/prov6 (snapshots) ===");
            var canaryOk = true;
            if (canaries != null)
            {
                for (var i = 0; i < canaries.Count; i++)
                {
                    sb.AppendLine(canaries[i].Line);
                    if (canaries[i].Tick == 0 || canaries[i].Tick == 1000)
                        canaryOk &= canaries[i].Ok;
                }
            }
            sb.AppendLine(canaryOk
                ? "VERDICT: CANARY OK — t0000 FRA/BUR, t1000 BUR/FRA."
                : "VERDICT: CANARY FAIL — snapshot ownership incorrect.");
            sb.AppendLine();

            sb.AppendLine("=== PIXELDIFF t0000 vs t1000 ===");
            var pixelOk = true;
            if (pixelDiffs != null)
            {
                for (var i = 0; i < pixelDiffs.Count; i++)
                {
                    sb.AppendLine(pixelDiffs[i].Line);
                    pixelOk &= pixelDiffs[i].Ok;
                }
            }
            sb.AppendLine(pixelOk
                ? "VERDICT: PIXELDIFF OK — political et tradenode diffèrent hors légende."
                : "VERDICT: PIXELDIFF FAIL — images encore figées sur le monde final.");
            sb.AppendLine();

            if (stats != null)
            {
                sb.AppendLine("=== LECTURE COUCHES (min/median/max) AUX QUATRE TICKS ===");
                for (var i = 0; i < stats.Count; i++)
                    sb.AppendLine(MapLayerRenderer.FormatStatsLine(stats[i]));
                sb.AppendLine();
            }

            WorldMetrics.Snapshot t800 = default;
            WorldMetrics.Snapshot t1000 = default;

            using (var harness = new SimulationHarness(Seed))
            {
                harness.RunTicks(0);
                AppendMetrics(sb, harness, 0);

                harness.RunTicks(200);
                AppendMetrics(sb, harness, 200);

                harness.RunTicks(300);
                AppendMetrics(sb, harness, 500);

                harness.RunTicks(300);
                t800 = WorldMetrics.Capture(harness.EntityManager, 800);
                sb.AppendLine(WorldMetrics.FormatStandardLine(800, t800));

                harness.RunTicks(200);
                t1000 = WorldMetrics.Capture(harness.EntityManager, 1000);
                sb.AppendLine(WorldMetrics.FormatStandardLine(1000, t1000));
            }

            sb.AppendLine();
            sb.AppendLine("=== ANCRAGES t1000 (bit-identique obligatoire) ===");
            var allMatch = true;
            allMatch &= Check(sb, "nonCore", $"{RefNonCore}/50",
                $"{t1000.NonCoreProvinces}/{t1000.TotalProvincesOwned}",
                t1000.NonCoreProvinces == RefNonCore && t1000.TotalProvincesOwned == 50);
            allMatch &= Check(sb, "countriesWithLand", RefCountries.ToString(),
                t1000.CountriesWithLand.ToString(), t1000.CountriesWithLand == RefCountries);
            allMatch &= Check(sb, "maxProvinces", RefMaxProv.ToString(),
                t1000.MaxProvincesOneCountry.ToString(),
                t1000.MaxProvincesOneCountry == RefMaxProv);
            allMatch &= Check(sb, "totalDebt", RefDebt,
                WorldMetrics.Fmt1(t1000.TotalDebt), WorldMetrics.Fmt1(t1000.TotalDebt) == RefDebt);
            allMatch &= Check(sb, "bankrupt", RefBankrupt.ToString(),
                t1000.BankruptCount.ToString(), t1000.BankruptCount == RefBankrupt);
            allMatch &= Check(sb, "worldArmyStr", RefArmy,
                WorldMetrics.Fmt0(t1000.WorldArmyStr),
                WorldMetrics.Fmt0(t1000.WorldArmyStr) == RefArmy);
            allMatch &= Check(sb, "zombie", RefZombie.ToString(),
                WorldMetrics.Fmt0(t1000.ZombieArmyStrLandless),
                WorldMetrics.Fmt0(t1000.ZombieArmyStrLandless) == RefZombie.ToString());
            allMatch &= Check(sb, "needsSatAvg", RefSat,
                WorldMetrics.Fmt3(t1000.NeedsSatAvg),
                WorldMetrics.Fmt3(t1000.NeedsSatAvg) == RefSat);
            allMatch &= Check(sb, "population", RefPop.ToString(),
                t1000.Population.ToString(), t1000.Population == RefPop);

            var ratio800 = WorldMetrics.Fmt1(t800.RatioVictories * 100f);
            allMatch &= Check(sb, "ratioV@800", RefRatioV800 + "%", ratio800 + "%",
                ratio800 == RefRatioV800);
            allMatch &= Check(sb, "stuck@800", RefStuck800.ToString(),
                t800.StuckWars.ToString(), t800.StuckWars == RefStuck800);
            allMatch &= Check(sb, "annexed@800", RefAnnexed800.ToString(),
                t800.AnnexedProvinces.ToString(), t800.AnnexedProvinces == RefAnnexed800);

            sb.AppendLine(allMatch
                ? "VERDICT: ANCRAGES OK — présentation n'a pas touché la simulation."
                : "VERDICT: ÉCART ANCRAGE — STOP, ne pas livrer.");
            sb.AppendLine(landOk
                ? "VERDICT: LANDMASSES OK — 7 composantes {39,4,2,2,1,1,1}."
                : "VERDICT: LANDMASSES FAIL.");
            sb.AppendLine(MapLayerRenderer.GeometryBuildCount == 1
                ? "VERDICT: GEOMETRY OK — 1 seul BuildMapGeometry."
                : $"VERDICT: GEOMETRY FAIL — builds={MapLayerRenderer.GeometryBuildCount}.");

            File.WriteAllText(measurePath, sb.ToString());
            UnityEngine.Debug.Log(sb.ToString());

            Assert.IsTrue(canaryOk, "Canari prov1/prov6 échoué (snapshot ownership).");
            Assert.IsTrue(pixelOk, "Pixeldiff political/tradenode = 0 (images figées).");
            Assert.IsTrue(allMatch, "Ancrages t1000/t800 non bit-identiques.");
            Assert.IsTrue(landOk, "Flood-fill landmasses ≠ {39,4,2,2,1,1,1}.");
            Assert.AreEqual(1, MapLayerRenderer.GeometryBuildCount, "Géométrie recalculée.");
            Assert.IsTrue(File.Exists(Path.Combine(layersDir, "satisfaction_t0000.png")));
            Assert.IsTrue(File.Exists(Path.Combine(layersDir, "satisfaction_t1000.png")));
            Assert.IsTrue(File.Exists(Path.Combine(layersDir, "population_t1000.png")));
            Assert.IsTrue(File.Exists(Path.Combine(layersDir, "army_t1000.png")));
            Assert.IsTrue(File.Exists(Path.Combine(layersDir, "treasury_t1000.png")));
            Assert.IsTrue(File.Exists(Path.Combine(layersDir, "tradenode_t1000.png")));
            Assert.IsTrue(File.Exists(Path.Combine(layersDir, "political_t0000.png")));
            Assert.IsTrue(File.Exists(Path.Combine(layersDir, "political_t1000.png")));
            Assert.IsTrue(File.Exists(Path.Combine(layersDir, "compare_t0000.png")));
            Assert.IsTrue(File.Exists(Path.Combine(layersDir, "compare_t1000.png")));
        }

        static void AppendMetrics(StringBuilder sb, SimulationHarness harness, int tick)
        {
            var snap = WorldMetrics.Capture(harness.EntityManager, tick);
            sb.AppendLine(WorldMetrics.FormatStandardLine(tick, snap));
        }

        static bool Check(StringBuilder sb, string name, string expected, string actual, bool ok)
        {
            sb.AppendLine($"{name}: expected={expected} actual={actual} {(ok ? "OK" : "FAIL")}");
            return ok;
        }
    }
}
