using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using VictoriaGame.Presentation;

namespace VictoriaGame.Tests
{
    /// <summary>Point d'entrée batchmode : -executeMethod VictoriaGame.Tests.V1MapSnapshotBatchRunner.Run</summary>
    public static class V1MapSnapshotBatchRunner
    {
        public static void Run()
        {
            V1MapSnapshotTests.RunAndWriteArtifacts();
            UnityEngine.Debug.Log("V1MapSnapshotBatchRunner: DONE");
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
            #endif
        }
    }

    /// <summary>
    /// Harnais producteur V1 — UNE seule SimulationHarness avancée 0→200→500→1000.
    /// Ce n'est PAS un test de non-régression : il produit les PNG + prouve les ancrages t1000.
    /// </summary>
    [TestFixture]
    public class V1MapSnapshotTests
    {
        const uint Seed = 42195u;

        // Ancrages production (INTEGRATION_TICKS=400, seed 42195).
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

        [Test]
        public void V1_ExportMapSnapshotsAtKeyTicks() => RunAndWriteArtifacts();

        public static void RunAndWriteArtifacts()
        {
            var mapsDir = Path.Combine(
                UnityEngine.Application.dataPath, "..", "Logs", "v1_001_maps");
            var logPath = Path.Combine(
                UnityEngine.Application.dataPath, "..", "Logs", "v1_001_measurements.log");
            Directory.CreateDirectory(mapsDir);
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

            var sb = new StringBuilder();
            sb.AppendLine(
                $"=== v1_001 MapSnapshotExporter seed={Seed} CELL_RADIUS={MapSnapshotExporter.CellRadius} ===");
            sb.AppendLine(
                "UNE SimulationHarness continue 0→200→500→(800 métriques)→1000. " +
                "Presentation = classe statique, hors SimulationSystemGroup.");
            sb.AppendLine(
                "SimulationHarness filtre Assembly.Name == 'VictoriaGame' — " +
                "VictoriaGame.Presentation n'est jamais installé.");
            sb.AppendLine();

            WorldMetrics.Snapshot t800 = default;
            WorldMetrics.Snapshot t1000 = default;

            using (var harness = new SimulationHarness(Seed))
            {
                // t0 — init seule.
                harness.RunTicks(0);
                ExportMilestone(harness, 0, mapsDir, sb);
                MapSnapshotExporter.ExportAdjacencyGraph(
                    Path.Combine(mapsDir, "graph_adjacency.png"));
                sb.AppendLine("graph_adjacency.png écrit (une fois).");
                sb.AppendLine();

                harness.RunTicks(200);
                ExportMilestone(harness, 200, mapsDir, sb);

                harness.RunTicks(300); // → 500
                ExportMilestone(harness, 500, mapsDir, sb);

                harness.RunTicks(300); // → 800 (métriques only)
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

            // annexed@800 : brief cite 31 (Disabled) ; prod INTEGRATION_TICKS=400 donne une autre valeur.
            // On logue sans en faire un ancrage bloquant (leçon world_013).
            sb.AppendLine(
                $"annexed@800 (info)={t800.AnnexedProvinces} " +
                $"(brief citait 31 = Disabled ; prod 400 peut différer)");

            sb.AppendLine(allMatch
                ? "VERDICT: ANCRAGES OK — présentation n'a pas touché la simulation."
                : "VERDICT: ÉCART ANCRAGE — STOP, ne pas livrer.");
            sb.AppendLine($"CELL_RADIUS retenu={MapSnapshotExporter.CellRadius.ToString(CultureInfo.InvariantCulture)}");

            File.WriteAllText(logPath, sb.ToString());
            UnityEngine.Debug.Log(sb.ToString());

            Assert.IsTrue(allMatch,
                "Les ancrages t1000/t800 ne sont pas bit-identiques — la présentation a probablement touché la sim.");
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
            sb.AppendLine();
        }

        static bool Check(StringBuilder sb, string name, string expected, string actual, bool ok)
        {
            sb.AppendLine($"{name}: expected={expected} actual={actual} {(ok ? "OK" : "FAIL")}");
            return ok;
        }
    }
}
