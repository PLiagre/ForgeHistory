using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using VictoriaGame.Presentation;

namespace VictoriaGame.Tests
{
    /// <summary>Point d'entrée batchmode : -executeMethod VictoriaGame.Tests.V1dChronicleBatchRunner.Run</summary>
    public static class V1dChronicleBatchRunner
    {
        public static void Run()
        {
            V1dChronicleTests.RunAndWriteArtifacts();
            UnityEngine.Debug.Log("V1dChronicleBatchRunner: DONE");
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
            #endif
        }
    }

    /// <summary>
    /// Harnais V1d — chronique 41 frames + journal dérivé + ancrages.
    /// UNE seule SimulationHarness avancée par pas de 25 ticks (lecture seule aux paliers).
    /// </summary>
    [TestFixture]
    public class V1dChronicleTests
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
        public void V1d_ExportChronicleAndJournal() => RunAndWriteArtifacts();

        public static void RunAndWriteArtifacts()
        {
            var chronicleDir = Path.Combine(
                UnityEngine.Application.dataPath, "..", "Logs", "v1_004_chronicle");
            var mapsDir = Path.Combine(
                UnityEngine.Application.dataPath, "..", "Logs", "v1_004_maps");
            var measurePath = Path.Combine(
                UnityEngine.Application.dataPath, "..", "Logs", "v1_004_measurements.log");
            var journalPath = Path.Combine(
                UnityEngine.Application.dataPath, "..", "Logs", "v1_004_chronicle.log");

            Directory.CreateDirectory(chronicleDir);
            Directory.CreateDirectory(mapsDir);
            Directory.CreateDirectory(Path.GetDirectoryName(measurePath)!);

            var sb = new StringBuilder();
            sb.AppendLine($"=== v1_004 ChronicleExporter seed={Seed} ===");
            sb.AppendLine(MapSnapshotExporter.FormatConstantsLine());
            sb.AppendLine(
                "Géométrie (masque + provinceAt) calculée UNE FOIS à 480×360 et UNE FOIS à 1600×1200.");
            sb.AppendLine(
                "UNE SimulationHarness continue 0→25→…→1000. Presentation = classes statiques, hors SimulationSystemGroup.");
            sb.AppendLine();

            WorldMetrics.Snapshot t800 = default;
            WorldMetrics.Snapshot t1000 = default;
            var landOk = false;
            string journalHash = "";
            var journalRepro = false;

            using (var harness = new SimulationHarness(Seed))
            {
                harness.RunTicks(0);

                ChronicleExporter.Run(
                    harness.EntityManager,
                    advanceBy: delta => harness.RunTicks(delta),
                    onTick: tick =>
                    {
                        if (tick == 0 || tick == 200 || tick == 500 || tick == 800 || tick == 1000)
                        {
                            var snap = WorldMetrics.Capture(harness.EntityManager, tick);
                            sb.AppendLine(WorldMetrics.FormatStandardLine(tick, snap));
                            sb.AppendLine(MapSnapshotExporter.FormatLegend(harness.EntityManager));
                            sb.AppendLine(MapSnapshotExporter.FormatMaskStatsLine());
                            sb.AppendLine(MapSnapshotExporter.LastLandMassReport.Summary);
                            sb.AppendLine();

                            if (tick == 800) t800 = snap;
                            if (tick == 1000) t1000 = snap;
                        }
                    },
                    chronicleDir,
                    mapsDir,
                    journalPath,
                    out journalHash,
                    out journalRepro,
                    out landOk);
            }

            sb.AppendLine($"JOURNAL sha256={journalHash} repro={(journalRepro ? "OK" : "FAIL")}");
            sb.AppendLine(MapSnapshotExporter.LastLandMassReport.Summary);
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
                : "VERDICT: LANDMASSES FAIL — voir LANDMASSES ci-dessus.");
            sb.AppendLine(journalRepro
                ? "VERDICT: JOURNAL REPRO OK — double FormatJournal + SHA-256 identiques."
                : "VERDICT: JOURNAL REPRO FAIL.");

            File.WriteAllText(measurePath, sb.ToString());
            UnityEngine.Debug.Log(sb.ToString());

            Assert.IsTrue(allMatch, "Ancrages t1000/t800 non bit-identiques.");
            Assert.IsTrue(landOk, "Flood-fill landmasses ≠ {39,4,2,2,1,1,1}.");
            Assert.IsTrue(journalRepro, "Journal non reproductible.");
            Assert.IsTrue(File.Exists(Path.Combine(chronicleDir, "frame_t0000.png")));
            Assert.IsTrue(File.Exists(Path.Combine(chronicleDir, "frame_t1000.png")));
            Assert.IsTrue(File.Exists(Path.Combine(chronicleDir, "contact_sheet.png")));
            Assert.IsTrue(File.Exists(journalPath));
            Assert.IsTrue(File.Exists(Path.Combine(mapsDir, "map_t1000.png")));
            Assert.IsTrue(File.Exists(Path.Combine(mapsDir, "graph_adjacency.png")));
        }

        static bool Check(StringBuilder sb, string name, string expected, string actual, bool ok)
        {
            sb.AppendLine($"{name}: expected={expected} actual={actual} {(ok ? "OK" : "FAIL")}");
            return ok;
        }
    }
}
