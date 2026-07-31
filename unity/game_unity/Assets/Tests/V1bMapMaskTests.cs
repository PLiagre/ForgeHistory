using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using VictoriaGame.Presentation;

namespace VictoriaGame.Tests
{
    /// <summary>Point d'entrée batchmode : -executeMethod VictoriaGame.Tests.V1bMapMaskBatchRunner.Run</summary>
    public static class V1bMapMaskBatchRunner
    {
        public static void Run()
        {
            V1bMapMaskTests.RunAndWriteArtifacts();
            UnityEngine.Debug.Log("V1bMapMaskBatchRunner: DONE");
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
            #endif
        }
    }

    /// <summary>
    /// Harnais V1b — masque graphe + preuve 7 masses + ancrages bit-identiques.
    /// UNE seule SimulationHarness 0→200→500→800→1000.
    /// </summary>
    [TestFixture]
    public class V1bMapMaskTests
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
        public void V1b_ExportGraphLandMaskAtKeyTicks() => RunAndWriteArtifacts();

        public static void RunAndWriteArtifacts()
        {
            var mapsDir = Path.Combine(
                UnityEngine.Application.dataPath, "..", "Logs", "v1_002_maps");
            var logPath = Path.Combine(
                UnityEngine.Application.dataPath, "..", "Logs", "v1_002_measurements.log");
            Directory.CreateDirectory(mapsDir);
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

            var sb = new StringBuilder();
            sb.AppendLine(
                $"=== v1_002 MapSnapshotExporter graph land-mask seed={Seed} ===");
            sb.AppendLine(MapSnapshotExporter.FormatConstantsLine());
            sb.AppendLine(
                "Masque 3 passes : disques CELL_RADIUS → corridors neighbors → chenaux straits.");
            sb.AppendLine(
                "UNE SimulationHarness continue 0→200→500→(800 métriques)→1000. " +
                "Presentation = classe statique, hors SimulationSystemGroup.");
            sb.AppendLine();

            WorldMetrics.Snapshot t800 = default;
            WorldMetrics.Snapshot t1000 = default;
            var landOk = false;

            using (var harness = new SimulationHarness(Seed))
            {
                harness.RunTicks(0);
                ExportMilestone(harness, 0, mapsDir, sb);
                landOk = MapSnapshotExporter.LastLandMassReport.MatchesTarget;
                MapSnapshotExporter.ExportAdjacencyGraph(
                    Path.Combine(mapsDir, "graph_adjacency.png"));
                sb.AppendLine("graph_adjacency.png écrit (une fois).");
                sb.AppendLine();

                harness.RunTicks(200);
                ExportMilestone(harness, 200, mapsDir, sb);

                harness.RunTicks(300); // → 500
                ExportMilestone(harness, 500, mapsDir, sb);

                harness.RunTicks(300); // → 800
                t800 = WorldMetrics.Capture(harness.EntityManager, 800);
                sb.AppendLine(WorldMetrics.FormatStandardLine(800, t800));
                sb.AppendLine(MapSnapshotExporter.FormatLegend(harness.EntityManager));
                sb.AppendLine();

                harness.RunTicks(200); // → 1000
                ExportMilestone(harness, 1000, mapsDir, sb);
                t1000 = WorldMetrics.Capture(harness.EntityManager, 1000);
            }

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
                : "VERDICT: LANDMASSES FAIL — voir LANDMASSES ci-dessus.");
            sb.AppendLine(MapSnapshotExporter.FormatConstantsLine());
            sb.AppendLine(
                "COMPARAISON v1_001→v1_002: les blobs isolés se soudent en continents via " +
                "corridors terrestres ; Manche / Irlande / Messine / Bosphore / détroits danois " +
                "restent en eau grâce aux chenaux soustractifs (straits).");

            File.WriteAllText(logPath, sb.ToString());
            UnityEngine.Debug.Log(sb.ToString());

            Assert.IsTrue(allMatch,
                "Les ancrages t1000/t800 ne sont pas bit-identiques — la présentation a probablement touché la sim.");
            Assert.IsTrue(landOk,
                "Flood-fill landmasses ≠ {{39,4,2,2,1,1,1}} : " +
                MapSnapshotExporter.LastLandMassReport.Summary);
            Assert.IsTrue(File.Exists(Path.Combine(mapsDir, "map_t0.png")));
            Assert.IsTrue(File.Exists(Path.Combine(mapsDir, "map_t200.png")));
            Assert.IsTrue(File.Exists(Path.Combine(mapsDir, "map_t500.png")));
            Assert.IsTrue(File.Exists(Path.Combine(mapsDir, "map_t1000.png")));
            Assert.IsTrue(File.Exists(Path.Combine(mapsDir, "graph_adjacency.png")));
        }

        static void ExportMilestone(
            SimulationHarness harness, int tick, string mapsDir, StringBuilder sb)
        {
            var path = Path.Combine(mapsDir, $"map_t{tick}.png");
            MapSnapshotExporter.Export(harness.EntityManager, tick, path);
            var snap = WorldMetrics.Capture(harness.EntityManager, tick);
            sb.AppendLine(WorldMetrics.FormatStandardLine(tick, snap));
            sb.AppendLine(MapSnapshotExporter.FormatLegend(harness.EntityManager));
            sb.AppendLine(MapSnapshotExporter.LastLandMassReport.Summary);
            sb.AppendLine();
        }

        static bool Check(StringBuilder sb, string name, string expected, string actual, bool ok)
        {
            sb.AppendLine($"{name}: expected={expected} actual={actual} {(ok ? "OK" : "FAIL")}");
            return ok;
        }
    }
}
