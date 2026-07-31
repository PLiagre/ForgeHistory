using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using VictoriaGame.Presentation;

namespace VictoriaGame.Tests
{
    /// <summary>Batch : -executeMethod VictoriaGame.Tests.V1008MeasurementBatchRunner.Run</summary>
    public static class V1008MeasurementBatchRunner
    {
        public static void Run()
        {
            V1008MeasurementTests.RunAndWriteArtifacts();
            UnityEngine.Debug.Log("V1008MeasurementBatchRunner: DONE");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
        }
    }

    /// <summary>
    /// PARTIE 1 v1_008 — re-mesure des 12 ancrages après le premier ISystem (MapDisplaySystem)
    /// et le déplacement de WorldMetrics. Verdict explicite OUI/NON.
    /// </summary>
    [TestFixture]
    public class V1008MeasurementTests
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
        public void V1008_Anchors_Survive_First_ISystem_And_WorldMetrics_Move() =>
            RunAndWriteArtifacts();

        public static void RunAndWriteArtifacts()
        {
            var measurePath = Path.Combine(
                UnityEngine.Application.dataPath, "..", "Logs", "v1_008_measurements.log");
            Directory.CreateDirectory(Path.GetDirectoryName(measurePath)!);

            var sb = new StringBuilder();
            sb.AppendLine($"=== v1_008 ANCRAGES seed={Seed} ===");
            sb.AppendLine(
                "PARTIE 1 — re-mesure après MapDisplaySystem (PresentationSystemGroup) " +
                "et déplacement WorldMetrics → Presentation.");
            sb.AppendLine(
                "SimulationHarness filtre Assembly == VictoriaGame — Presentation exclu (dip_008).");
            sb.AppendLine("WorldMetrics.Capture / FormatStandardLine uniquement (règle test_001).");
            sb.AppendLine();

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
            sb.AppendLine("=== COMPARAISON ATTENDU / OBTENU (12 ancrages) ===");
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

            sb.AppendLine();
            if (allMatch)
            {
                sb.AppendLine(
                    "VERDICT: ANCRAGES OUI — les 12 ancrages ont survécu à l'introduction " +
                    "du premier ISystem (MapDisplaySystem / PresentationSystemGroup) et au " +
                    "déplacement de WorldMetrics.");
            }
            else
            {
                sb.AppendLine(
                    "VERDICT: ANCRAGES NON — au moins un ancrage a bougé. " +
                    "Cause probable : ordonnancement ou définition de métrique. " +
                    "STOP — ne pas livrer la partie 3.");
            }

            File.WriteAllText(measurePath, sb.ToString());
            UnityEngine.Debug.Log(sb.ToString());

            Assert.IsTrue(
                allMatch,
                "Ancrages t1000/t800 non bit-identiques après MapDisplaySystem / WorldMetrics.");
        }

        static void AppendMetrics(StringBuilder sb, SimulationHarness harness, int tick)
        {
            var snap = WorldMetrics.Capture(harness.EntityManager, tick);
            sb.AppendLine(WorldMetrics.FormatStandardLine(tick, snap));
        }

        static bool Check(StringBuilder sb, string name, string expected, string actual, bool ok)
        {
            sb.AppendLine(
                $"{name}: expected={expected} actual={actual} {(ok ? "OK" : "FAIL")}");
            return ok;
        }
    }
}
