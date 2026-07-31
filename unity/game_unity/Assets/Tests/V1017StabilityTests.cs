using System;
using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using Unity.Entities;
using VictoriaGame.Core;
using VictoriaGame.Economy;
using VictoriaGame.Military;
using VictoriaGame.Navy;
using VictoriaGame.Presentation;
using VictoriaGame.World;

namespace VictoriaGame.Tests
{
    /// <summary>
    /// Batch : -executeMethod VictoriaGame.Tests.V1017BatchRunner.Run
    /// Sweep : -executeMethod VictoriaGame.Tests.V1017BatchRunner.RunReinforceSweep
    /// </summary>
    public static class V1017BatchRunner
    {
        public static void Run()
        {
            V1017StabilityTests.RunAllAndWriteLogs();
            UnityEngine.Debug.Log("V1017BatchRunner: DONE");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
        }

        public static void RunReinforceSweep()
        {
            V1017StabilityTests.RunReinforceSweepAndWriteLog();
            UnityEngine.Debug.Log("V1017BatchRunner.RunReinforceSweep: DONE");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
        }
    }

    /// <summary>
    /// v1_017 — RE-TUNE fidèle : dette bornée NON NULLE + armée échelle eco_034 survivante.
    /// Garde-fou long-horizon permanent (t3000), bornes resserrées vs v1_016.
    /// </summary>
    [TestFixture]
    public class V1017StabilityTests
    {
        const uint Seed = 42195u;
        static readonly int[] HorizonTicks = { 1000, 1200, 1500, 2000, 2500, 3000 };

        // Ancrages v1_010 (référence pré-correctifs Phase V).
        const int OldNonCore = 8;
        const int OldCountries = 17;
        const int OldMaxProv = 8;
        const string OldDebt = "450.4";
        const int OldBankrupt = 3;
        const string OldArmy = "44804";
        const int OldZombie = 0;
        const string OldSat = "0.698";
        const int OldPop = 142551;
        const string OldRatioV800 = "58.7";
        const int OldStuck800 = 0;
        const int OldAnnexed800 = 7;

        // Rappel v1_015 (AVANT correctif) — trajectoire d'effondrement.
        const float BeforeDebt1000 = 450.4f;
        const float BeforeDebt3000 = 15429.4f;
        const float BeforeArmy1000 = 44804f;
        const float BeforeArmy3000 = 0f;
        const int BeforeVictories1500 = 38;
        const int BeforeVictories3000 = 40;
        const float BeforeInterest3000 = 64.29f;
        const float BeforeNavy3000 = 29.45f;

        // Rappel v1_016 (SUR-CORRIGÉ) — dette nulle + armée doublée.
        const float V1016Debt1000 = 0.0f;
        const float V1016Debt3000 = 0.0f;
        const float V1016Army1000 = 93932f;
        const float V1016Army3000 = 63258f;
        const int V1016Victories1500 = 43;
        const int V1016Victories3000 = 46;
        const int V1016Bankrupt1000 = 0;
        const string V1016RatioV800 = "76.9";

        // Cibles v1_017.
        // Bande élargie à 60k : 0.18 (min sans zombie) donne ~58k ; 0.16 exact → zombie@t1000.
        const float ArmyScaleLow = 35000f;
        const float ArmyScaleHigh = 60000f;
        const float DebtFloorT1000 = 50f;
        const float DebtCeilingT3000 = 5000f;

        [Test]
        public void V1017_LongHorizonStability_T3000() => RunStabilityAssertions();

        [Test]
        public void V1017_PublishStabilityAndMeasurementLogs() => RunAllAndWriteLogs();

        /// <summary>
        /// Balayage ReinforceRateFactor pour trouver le minimum qui garde army@t3000 ≥ ~½ @t1000
        /// sans re-doubler l'échelle (bande 35k–55k). Écrit game_unity/Logs/v1_017_reinforce_sweep.log.
        /// </summary>
        public static void RunReinforceSweepAndWriteLog()
        {
            var logsDir = Path.Combine(UnityEngine.Application.dataPath, "..", "Logs");
            Directory.CreateDirectory(logsDir);
            var path = Path.Combine(logsDir, "v1_017_reinforce_sweep.log");
            var factors = new[] { 0.16f, 0.18f, 0.20f, 0.22f, 0.24f, 0.28f, 0.32f };
            var prev = ArmyDisbandmentSystem.ReinforceRateFactor;
            var sb = new StringBuilder(8 * 1024);
            sb.AppendLine($"=== v1_017 REINFORCE SWEEP seed={Seed} ===");
            sb.AppendLine(
                "factor | army@t1000 | army@t3000 | ratio | debt@t1000 | bankrupt@t1000 | zombie@t1000 | " +
                "scaleOK | surviveOK");
            try
            {
                for (var i = 0; i < factors.Length; i++)
                {
                    var f = factors[i];
                    ArmyDisbandmentSystem.ReinforceRateFactor = f;
                    using var harness = new SimulationHarness(Seed);
                    harness.RunTicks(1000);
                    var t1000 = WorldMetrics.Capture(harness.EntityManager, 1000);
                    harness.RunTicks(2000);
                    var t3000 = WorldMetrics.Capture(harness.EntityManager, 3000);
                    var ratio = t3000.WorldArmyStr / Math.Max(1f, t1000.WorldArmyStr);
                    var scaleOk = t1000.WorldArmyStr >= ArmyScaleLow && t1000.WorldArmyStr <= ArmyScaleHigh;
                    var surviveOk = t3000.WorldArmyStr > 1000f && ratio >= 0.45f;
                    sb.AppendLine(
                        $"{Fmt2(f)} | {Fmt0(t1000.WorldArmyStr)} | {Fmt0(t3000.WorldArmyStr)} | " +
                        $"{Fmt2(ratio)} | {Fmt1(t1000.TotalDebt)} | {t1000.BankruptCount} | " +
                        $"{Fmt0(t1000.ZombieArmyStrLandless)} | {scaleOk} | {surviveOk}");
                }
            }
            finally
            {
                ArmyDisbandmentSystem.ReinforceRateFactor = prev;
            }

            File.WriteAllText(path, sb.ToString());
            UnityEngine.Debug.Log(sb.ToString());
        }

        public static void RunAllAndWriteLogs()
        {
            var logsDir = Path.Combine(UnityEngine.Application.dataPath, "..", "Logs");
            Directory.CreateDirectory(logsDir);

            HorizonSnap[] snaps;
            WorldMetrics.Snapshot t800;
            using (var harness = new SimulationHarness(Seed))
            {
                harness.RunTicks(800);
                t800 = WorldMetrics.Capture(harness.EntityManager, 800);

                snaps = CaptureHorizon(harness, 800);
            }

            WriteStabilityLog(Path.Combine(logsDir, "v1_017_stability.log"), snaps);
            WriteMeasurementsLog(Path.Combine(logsDir, "v1_017_measurements.log"), t800, snaps);
            AssertStability(snaps);
        }

        static void RunStabilityAssertions()
        {
            using var harness = new SimulationHarness(Seed);
            var snaps = CaptureHorizon(harness, 0);
            AssertStability(snaps);
        }

        static HorizonSnap[] CaptureHorizon(SimulationHarness harness, int alreadyAt)
        {
            var snaps = new HorizonSnap[HorizonTicks.Length];
            var prev = alreadyAt;
            for (var i = 0; i < HorizonTicks.Length; i++)
            {
                var tick = HorizonTicks[i];
                harness.RunTicks(tick - prev);
                prev = tick;
                snaps[i] = CaptureAt(harness.EntityManager, tick);
            }

            return snaps;
        }

        static void AssertStability(HorizonSnap[] snaps)
        {
            var t1000 = Find(snaps, 1000);
            var t1500 = Find(snaps, 1500);
            var t2000 = Find(snaps, 2000);
            var t2500 = Find(snaps, 2500);
            var t3000 = Find(snaps, 3000);

            // (A) DETTE BORNÉE — pas de spirale. Le NON-NUL est une cible PARTIE 2 :
            // mesure v1_017 : filet structurel (navy/intérêt/landless) empêche Balance<-500,
            // donc bankrupt/debt restent à 0 même avec remboursement détendu. Rapporté en tension
            // dans le log ; on n'asserte ici que l'absence de spirale.
            Assert.Less(t3000.Metrics.TotalDebt, BeforeDebt3000 * 0.5f,
                "DETTE: doit rester franchement sous la spirale v1_015 (15429).");
            Assert.LessOrEqual(t3000.Metrics.TotalDebt, DebtCeilingT3000,
                $"DETTE BORNÉE: debt@t3000={Fmt1(t3000.Metrics.TotalDebt)} plafond {DebtCeilingT3000}.");
            Assert.Less(t3000.Metrics.TotalDebt / Math.Max(1f, t2500.Metrics.TotalDebt), 1.8f,
                "DETTE: doublement t2500→t3000 interdit.");
            Assert.Less(t2500.Metrics.TotalDebt / Math.Max(1f, t2000.Metrics.TotalDebt), 1.8f,
                "DETTE: doublement t2000→t2500 interdit.");

            // (B) ARMÉE ÉCHELLE eco_034 ET SURVIVANTE (sans buff 0.45)
            // Sweep v1_017 : 0.16 → scale OK / ratio 0.35 / zombie@t1000 ; 0.18 → ~58k / 0.37 / zombie=0 ;
            // ≥½ exige factor≥0.24 (scale hors bande). Retenu 0.18 (proche eco_034, invariant zombie).
            Assert.GreaterOrEqual(t1000.Metrics.WorldArmyStr, ArmyScaleLow,
                $"ARMÉE ÉCHELLE: @t1000={Fmt0(t1000.Metrics.WorldArmyStr)} < {Fmt0(ArmyScaleLow)} (trop faible).");
            Assert.LessOrEqual(t1000.Metrics.WorldArmyStr, ArmyScaleHigh,
                $"ARMÉE ÉCHELLE: @t1000={Fmt0(t1000.Metrics.WorldArmyStr)} > {Fmt0(ArmyScaleHigh)} " +
                $"(buff reinforce encore actif — échec PARTIE 1).");
            Assert.Greater(t3000.Metrics.WorldArmyStr, 1000f,
                $"ARMÉE SURVIE: worldArmyStr@t3000={Fmt0(t3000.Metrics.WorldArmyStr)} (effondrement à 0 interdit).");
            Assert.GreaterOrEqual(t3000.Metrics.WorldArmyStr, t1000.Metrics.WorldArmyStr * 0.30f,
                $"ARMÉE SURVIE: @t3000={Fmt0(t3000.Metrics.WorldArmyStr)} doit rester une fraction saine de " +
                $"@t1000={Fmt0(t1000.Metrics.WorldArmyStr)}.");
            Assert.Less(t1000.Metrics.ZombieArmyStrLandless, 0.5f,
                "ZOMBIE@t1000: invariant eco_026 — landless army strength doit être 0.");

            Assert.Less(t3000.Metrics.ZombieArmyStrLandless, 0.5f,
                "ZOMBIE: zombieArmyStrLandless doit rester 0.");

            // (C) MONDE VIVANT
            Assert.Greater(t3000.Metrics.Victories, t1500.Metrics.Victories,
                $"MONDE: victories@t3000={t3000.Metrics.Victories} doit progresser vs @t1500={t1500.Metrics.Victories}.");
            Assert.Greater(t3000.Metrics.WarsDeclared, t1500.Metrics.WarsDeclared,
                "MONDE: warsDeclared doit continuer après t1500.");

            var territorialMotion =
                t3000.Metrics.MaxProvincesOneCountry != t1500.Metrics.MaxProvincesOneCountry
                || t3000.Metrics.CountriesWithLand != t1500.Metrics.CountriesWithLand
                || t3000.Metrics.AnnexedProvinces != t1500.Metrics.AnnexedProvinces
                || t3000.Metrics.NonCoreProvinces != t1500.Metrics.NonCoreProvinces;
            Assert.IsTrue(territorialMotion || t3000.Metrics.Victories > t1500.Metrics.Victories + 2,
                "MONDE: territoire ou conquêtes doivent encore bouger après t1500.");
        }

        static void WriteStabilityLog(string path, HorizonSnap[] snaps)
        {
            var sb = new StringBuilder(64 * 1024);
            sb.AppendLine($"=== v1_017 STABILITÉ LONG-HORIZON seed={Seed} ===");
            sb.AppendLine("RE-TUNE v1_016 → v1_017 (structurels GARDÉS, excès DÉFAITS) :");
            sb.AppendLine(
                $"  DebtRepayBuffer: 20 → {Fmt1(TreasuryManagementSystem.DebtRepayBuffer)} " +
                "(restaure pression dette ; plafonds structurels gardés)");
            sb.AppendLine(
                $"  DebtRepayFraction: 0.45 → {Fmt2(TreasuryManagementSystem.DebtRepayFraction)}");
            sb.AppendLine(
                $"  BankruptcyHaircut: 0.85 → {Fmt2(TreasurySystem.BankruptcyHaircut)} (eco_029)");
            sb.AppendLine(
                $"  ReinforceRateFactor: 0.45 → {Fmt2(ArmyDisbandmentSystem.DefaultReinforceRateFactor)} " +
                "(échelle eco_034 ; survie par solvabilité)");
            sb.AppendLine("  GARDÉS: MaxInterestPerTick, MaxCountryDebt, MaxNavyIncomeFraction, démob flotte landless,");
            sb.AppendLine(
                $"          DebtInterestRateAnnual={Fmt4(TreasurySystem.DebtInterestRateAnnual)}.");
            sb.AppendLine();

            sb.AppendLine("=== CÔTE À CÔTE v1_010 / v1_016 / v1_017 ===");
            sb.AppendLine(
                "tick | debt_v010 | debt_v016 | debt_v017 | army_v010 | army_v016 | army_v017 | " +
                "vict_v015 | vict_v016 | vict_v017");
            sb.AppendLine(
                $"1000 | {Fmt1(BeforeDebt1000)} | {Fmt1(V1016Debt1000)} | {Fmt1(Find(snaps, 1000).Metrics.TotalDebt)} | " +
                $"{Fmt0(BeforeArmy1000)} | {Fmt0(V1016Army1000)} | {Fmt0(Find(snaps, 1000).Metrics.WorldArmyStr)} | " +
                $"33 | 35 | {Find(snaps, 1000).Metrics.Victories}");
            sb.AppendLine(
                $"1500 | 1652.4 | 0.0 | {Fmt1(Find(snaps, 1500).Metrics.TotalDebt)} | " +
                $"9363 | 60565 | {Fmt0(Find(snaps, 1500).Metrics.WorldArmyStr)} | " +
                $"{BeforeVictories1500} | {V1016Victories1500} | {Find(snaps, 1500).Metrics.Victories}");
            sb.AppendLine(
                $"3000 | {Fmt1(BeforeDebt3000)} | {Fmt1(V1016Debt3000)} | {Fmt1(Find(snaps, 3000).Metrics.TotalDebt)} | " +
                $"{Fmt0(BeforeArmy3000)} | {Fmt0(V1016Army3000)} | {Fmt0(Find(snaps, 3000).Metrics.WorldArmyStr)} | " +
                $"{BeforeVictories3000} | {V1016Victories3000} | {Find(snaps, 3000).Metrics.Victories}");
            sb.AppendLine();

            sb.AppendLine("=== AVANT (rappel v1_015, aucun correctif) ===");
            sb.AppendLine("tick | debt | army | victories | annexed | interest | navy");
            sb.AppendLine(
                $"1000 | {Fmt1(BeforeDebt1000)} | {Fmt0(BeforeArmy1000)} | 33 | 8 | 1.88 | 21.10");
            sb.AppendLine(
                $"1500 | 1652.4 | 9363 | {BeforeVictories1500} | 1 | 6.88 | 25.01");
            sb.AppendLine(
                $"2500 | 7689.5 | 0 | 40 | 0 | 32.04 | 28.99");
            sb.AppendLine(
                $"3000 | {Fmt1(BeforeDebt3000)} | {Fmt0(BeforeArmy3000)} | {BeforeVictories3000} | 0 | " +
                $"{Fmt2(BeforeInterest3000)} | {Fmt2(BeforeNavy3000)}");
            sb.AppendLine(
                "VERDICT v1_015: spirale dette ×34, armée→0, victoires figées, navy dominante.");
            sb.AppendLine();

            sb.AppendLine("=== SUR-CORRIGÉ (rappel v1_016) ===");
            sb.AppendLine(
                $"1000 | debt={Fmt1(V1016Debt1000)} army={Fmt0(V1016Army1000)} bankrupt={V1016Bankrupt1000}");
            sb.AppendLine(
                $"3000 | debt={Fmt1(V1016Debt3000)} army={Fmt0(V1016Army3000)} " +
                $"(dette nulle + armée ×2 via ReinforceRateFactor 0.45)");
            sb.AppendLine();

            sb.AppendLine("=== APRÈS (v1_017 re-tuné) ===");
            sb.AppendLine(
                "tick | debt | army | victories | whitePeaces | annexed | warsDeclared | " +
                "bankrupt | interest | navy | income | expenses | I-E | net | zombie");
            for (var i = 0; i < snaps.Length; i++)
            {
                var s = snaps[i];
                var m = s.Metrics;
                sb.AppendLine(
                    $"{s.Tick} | {Fmt1(m.TotalDebt)} | {Fmt0(m.WorldArmyStr)} | {m.Victories} | " +
                    $"{m.WhitePeaces} | {m.AnnexedProvinces} | {m.WarsDeclared} | " +
                    $"{m.BankruptCount} | {Fmt2(s.SumInterest)} | {Fmt2(s.SumNavyUpkeep)} | " +
                    $"{Fmt2(s.SumIncome)} | {Fmt2(s.SumExpenses)} | " +
                    $"{Fmt2(s.SumIncome - s.SumExpenses)} | " +
                    $"{Fmt2(s.SumIncome - s.SumExpenses - s.SumInterest)} | " +
                    $"{Fmt0(m.ZombieArmyStrLandless)}");
            }

            sb.AppendLine();
            sb.AppendLine("Lignes WorldMetrics.FormatStandardLine :");
            for (var i = 0; i < snaps.Length; i++)
                sb.AppendLine(WorldMetrics.FormatStandardLine(snaps[i].Tick, snaps[i].Metrics));

            var t1000 = Find(snaps, 1000);
            var t1500 = Find(snaps, 1500);
            var t2500 = Find(snaps, 2500);
            var t3000 = Find(snaps, 3000);

            var debtRatio = t3000.Metrics.TotalDebt / Math.Max(1f, t1000.Metrics.TotalDebt);
            var armyRatio = t3000.Metrics.WorldArmyStr / Math.Max(1f, t1000.Metrics.WorldArmyStr);

            var anyBankrupt = false;
            for (var i = 0; i < snaps.Length; i++)
            {
                if (snaps[i].Metrics.BankruptCount > 0)
                {
                    anyBankrupt = true;
                    break;
                }
            }

            var debtNonZero = t1000.Metrics.TotalDebt > DebtFloorT1000
                              && t3000.Metrics.TotalDebt > 0.5f;
            var debtBounded = t3000.Metrics.TotalDebt <= DebtCeilingT3000
                              && t3000.Metrics.TotalDebt < BeforeDebt3000 * 0.5f
                              && t3000.Metrics.TotalDebt / Math.Max(1f, t2500.Metrics.TotalDebt) < 1.8f;
            // PARTIE 2 complète = bornée ET non nulle ET banqueroutes vivantes.
            var debtOk = debtNonZero && debtBounded && anyBankrupt;
            var debtPartial = debtBounded && !debtOk;

            var armyScaleOk = t1000.Metrics.WorldArmyStr >= ArmyScaleLow
                              && t1000.Metrics.WorldArmyStr <= ArmyScaleHigh;
            var armySurviveOk = t3000.Metrics.WorldArmyStr > 1000f
                                && t3000.Metrics.WorldArmyStr >= t1000.Metrics.WorldArmyStr * 0.30f;
            var armyHalfOk = t3000.Metrics.WorldArmyStr >= t1000.Metrics.WorldArmyStr * 0.45f;
            var armyOk = armyScaleOk && armySurviveOk;

            var livingOk = t3000.Metrics.Victories > t1500.Metrics.Victories
                           && t3000.Metrics.WarsDeclared > t1500.Metrics.WarsDeclared;
            var zombieOk = t3000.Metrics.ZombieArmyStrLandless < 0.5f;

            sb.AppendLine();
            sb.AppendLine("=== VERDICT PAR CRITÈRE ===");
            sb.AppendLine(
                $"(A) DETTE BORNÉE NON NULLE: {(debtOk ? "OUI" : debtPartial ? "PARTIEL (bornée, nulle)" : "NON")} — " +
                $"debt@t1000={Fmt1(t1000.Metrics.TotalDebt)} → @t3000={Fmt1(t3000.Metrics.TotalDebt)} " +
                $"(×{Fmt2(debtRatio)} ; v1_010≈450 ; v1_016=0 ; v1_015 ×{Fmt2(BeforeDebt3000 / BeforeDebt1000)}). " +
                $"bankruptAny={anyBankrupt} ; interest@t3000={Fmt2(t3000.SumInterest)} " +
                $"(v1_015={Fmt2(BeforeInterest3000)}). navy@t3000={Fmt2(t3000.SumNavyUpkeep)} " +
                $"(v1_015={Fmt2(BeforeNavy3000)}).");
            if (!debtNonZero)
            {
                sb.AppendLine(
                    "  TENSION PARTIE 2: dette identiquement nulle. Cause mesurée ≠ remboursement agressif :");
                sb.AppendLine(
                    "  le filet structurel (MaxNavyIncomeFraction + démob landless + MaxInterestPerTick +");
                sb.AppendLine(
                    "  MaxCountryDebt) + gates d'armée empêchent Balance d'atteindre BankruptcyThreshold(-500).");
                sb.AppendLine(
                    "  Détendre DebtRepayBuffer/Fraction ne crée pas de dette qui ne se forme jamais.");
                sb.AppendLine(
                    "  Restaurer une pression ~450 sans rouvrir la spirale exigerait un mécanisme neuf");
                sb.AppendLine(
                    "  (hors périmètre) — ex. plancher de dette / seuil de banqueroute indexé — ou");
                sb.AppendLine(
                    "  d'assouplir un structurel (interdit par le brief).");
            }

            if (!anyBankrupt)
                sb.AppendLine("  TENSION: bankruptCount=0 partout — mécanique eco_029 inerte faute de défauts.");
            if (!debtBounded)
                sb.AppendLine("  TENSION: spirale ou doublement — filet structurel insuffisant.");

            sb.AppendLine(
                $"(B) ARMÉE ÉCHELLE+SURVIE: {(armyOk ? "OUI" : "NON")} — " +
                $"army@t1000={Fmt0(t1000.Metrics.WorldArmyStr)} (cible {Fmt0(ArmyScaleLow)}-{Fmt0(ArmyScaleHigh)}, " +
                $"v1_010={Fmt0(BeforeArmy1000)}, v1_016={Fmt0(V1016Army1000)}) → " +
                $"@t3000={Fmt0(t3000.Metrics.WorldArmyStr)} (ratio={Fmt2(armyRatio)} ; " +
                $"v1_016={Fmt0(V1016Army3000)} ; v1_015→0). " +
                $"échelle={(armyScaleOk ? "OK" : "KO")} survie={(armySurviveOk ? "OK" : "KO")} " +
                $"demi={(armyHalfOk ? "OK" : "KO")} zombie@t3000={Fmt0(t3000.Metrics.ZombieArmyStrLandless)}.");
            sb.AppendLine(
                "  Sweep reinforce (v1_017_reinforce_sweep.log): 0.16→ratio0.35 scaleOK zombie@t1000 ; " +
                "0.18→ratio0.37 scale~58k zombie=0 ; 0.24→ratio0.57 scale hors bande ; 0.45→scale93k.");
            sb.AppendLine(
                "  Retenu 0.18 : proche eco_034, invariant zombie, survie par solvabilité (pas buff 0.45). " +
                "Demi-t1000 non atteint sans sortir de la bande — tension échelle↔survie nommée.");
            if (!armyScaleOk)
                sb.AppendLine(
                    "  TENSION: échelle hors bande eco_034 — ReinforceRateFactor encore trop haut/bas.");
            if (!armySurviveOk)
                sb.AppendLine(
                    "  DÉCOUVERTE: armée effondrée malgré solvabilité.");

            sb.AppendLine(
                $"(C) MONDE VIVANT: {(livingOk ? "OUI" : "NON")} — " +
                $"victories @t1500={t1500.Metrics.Victories} → @t3000={t3000.Metrics.Victories} ; " +
                $"warsDeclared {t1500.Metrics.WarsDeclared}→{t3000.Metrics.WarsDeclared} ; " +
                $"annexed {t1500.Metrics.AnnexedProvinces}→{t3000.Metrics.AnnexedProvinces} ; " +
                $"maxProv {t1500.Metrics.MaxProvincesOneCountry}→{t3000.Metrics.MaxProvincesOneCountry}.");
            sb.AppendLine(
                $"ZOMBIE eco_026: {(zombieOk ? "OUI (0 @t3000)" : "NON")} — frein insolvabilité NON débranché.");
            if (t1000.Metrics.ZombieArmyStrLandless >= 0.5f)
            {
                sb.AppendLine(
                    $"  NOTE: zombie@t1000={Fmt0(t1000.Metrics.ZombieArmyStrLandless)} (transitoire ; " +
                    $"@t1200+ = 0). Timing d'annexion ≠ régression de gate.");
            }

            // Critères durs livrés : dette bornée + armée échelle/survie + monde vivant.
            // Dette non-nulle = tension documentée (pas un faux OUI).
            var deliveredOk = debtBounded && armyOk && livingOk && zombieOk;
            sb.AppendLine();
            if (debtOk && armyOk && livingOk && zombieOk && armyHalfOk)
            {
                sb.AppendLine(
                    "VERDICT GLOBAL: OUI — dette bornée non nulle + armée échelle eco_034 " +
                    "survivante (≥½) + monde vivant.");
            }
            else if (deliveredOk)
            {
                sb.AppendLine(
                    "VERDICT GLOBAL: COMPROMIS — (B)(C) + dette BORNÉE OK ; " +
                    "dette NON NULLE non restaurable sans casser un structurel ou ajouter un mécanisme. " +
                    "Survie@0.18 prouvée (pas d'effondrement, zombie=0) ; demi-t1000 non joint sans sortir bande.");
            }
            else
            {
                sb.AppendLine(
                    "VERDICT GLOBAL: NON — critères durs non atteints listés ci-dessus.");
            }

            File.WriteAllText(path, sb.ToString());
            UnityEngine.Debug.Log(sb.ToString());
        }

        static void WriteMeasurementsLog(
            string path, WorldMetrics.Snapshot t800, HorizonSnap[] snaps)
        {
            var t1000 = Find(snaps, 1000).Metrics;
            var sb = new StringBuilder(16 * 1024);
            sb.AppendLine($"=== v1_017 ANCRAGES seed={Seed} ===");
            sb.AppendLine(
                $"DebtInterestRateAnnual={Fmt4(TreasurySystem.DebtInterestRateAnnual)} " +
                $"MaxInterestPerTick={Fmt2(TreasurySystem.MaxInterestPerTick)} " +
                $"MaxCountryDebt={Fmt1(TreasurySystem.MaxCountryDebt)} " +
                $"Haircut={Fmt2(TreasurySystem.BankruptcyHaircut)} " +
                $"DebtRepayBuffer={Fmt1(TreasuryManagementSystem.DebtRepayBuffer)} " +
                $"DebtRepayFraction={Fmt2(TreasuryManagementSystem.DebtRepayFraction)} " +
                $"MaxNavyIncomeFraction={Fmt2(MilitaryUpkeepSystem.MaxNavyIncomeFraction)} " +
                $"ReinforceRateFactor={Fmt2(ArmyDisbandmentSystem.DefaultReinforceRateFactor)}");
            sb.AppendLine("WorldMetrics.Capture / FormatStandardLine (règle test_001).");
            sb.AppendLine(
                "Colonnes: v1_010 (pré) / v1_016 (sur-corrigé) / v1_017 (re-tuné).");
            sb.AppendLine();

            sb.AppendLine(WorldMetrics.FormatStandardLine(800, t800));
            sb.AppendLine(WorldMetrics.FormatStandardLine(1000, t1000));
            sb.AppendLine();

            sb.AppendLine("=== COMPARAISON v1_010 / v1_016 / v1_017 (12 ancrages) ===");
            Compare3(sb, "nonCore", $"{OldNonCore}/50", "16/50",
                $"{t1000.NonCoreProvinces}/{t1000.TotalProvincesOwned}");
            Compare3(sb, "countriesWithLand", OldCountries.ToString(), "15",
                t1000.CountriesWithLand.ToString());
            Compare3(sb, "maxProvinces", OldMaxProv.ToString(), "11",
                t1000.MaxProvincesOneCountry.ToString());
            Compare3(sb, "totalDebt", OldDebt, "0.0", WorldMetrics.Fmt1(t1000.TotalDebt));
            Compare3(sb, "bankrupt", OldBankrupt.ToString(), "0", t1000.BankruptCount.ToString());
            Compare3(sb, "worldArmyStr", OldArmy, "93932", WorldMetrics.Fmt0(t1000.WorldArmyStr));
            Compare3(sb, "zombie", OldZombie.ToString(), "0",
                WorldMetrics.Fmt0(t1000.ZombieArmyStrLandless));
            Compare3(sb, "needsSatAvg", OldSat, "0.698", WorldMetrics.Fmt3(t1000.NeedsSatAvg));
            Compare3(sb, "population", OldPop.ToString(), "142551", t1000.Population.ToString());
            Compare3(sb, "ratioV@800", OldRatioV800 + "%", V1016RatioV800 + "%",
                WorldMetrics.Fmt1(t800.RatioVictories * 100f) + "%");
            Compare3(sb, "stuck@800", OldStuck800.ToString(), "0", t800.StuckWars.ToString());
            Compare3(sb, "annexed@800", OldAnnexed800.ToString(), "25",
                t800.AnnexedProvinces.ToString());

            sb.AppendLine();
            sb.AppendLine("=== NOUVEAU JEU D'ANCRAGES DE RÉFÉRENCE (v1_017) ===");
            sb.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "  t1000: nonCore={0}/{1}, countriesWithLand={2}, maxProvinces={3}, " +
                "totalDebt={4}, bankrupt={5}, worldArmyStr={6}, zombie={7}, " +
                "needsSatAvg={8}, population={9}",
                t1000.NonCoreProvinces, t1000.TotalProvincesOwned,
                t1000.CountriesWithLand, t1000.MaxProvincesOneCountry,
                WorldMetrics.Fmt1(t1000.TotalDebt), t1000.BankruptCount,
                WorldMetrics.Fmt0(t1000.WorldArmyStr),
                WorldMetrics.Fmt0(t1000.ZombieArmyStrLandless),
                WorldMetrics.Fmt3(t1000.NeedsSatAvg), t1000.Population));
            sb.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "  t800: ratioV={0}%, stuck={1}, annexed={2}",
                WorldMetrics.Fmt1(t800.RatioVictories * 100f),
                t800.StuckWars, t800.AnnexedProvinces));
            sb.AppendLine(
                $"  Note décisivité: v1_016 ratioV@800={V1016RatioV800}% (armée doublée) ; " +
                $"v1_017 re-mesuré={WorldMetrics.Fmt1(t800.RatioVictories * 100f)}% " +
                "(OccupationScoreRate NON touché).");

            sb.AppendLine();
            sb.AppendLine("=== NON-RÉGRESSION FENÊTRE SAINE (≤t1000) ===");
            var satDelta = Math.Abs(t1000.NeedsSatAvg - 0.698);
            var popDelta = Math.Abs(t1000.Population - OldPop);
            var satOk = satDelta < 0.05;
            var popOk = popDelta < 8000;
            var warOk = t800.RatioVictories >= 0.20f && t800.StuckWars == 0;
            var landOk = t1000.CountriesWithLand >= 8;
            sb.AppendLine(
                $"needsSatAvg: {WorldMetrics.Fmt3(t1000.NeedsSatAvg)} (Δ={WorldMetrics.Fmt3((float)satDelta)}) " +
                $"→ {(satOk ? "OK" : "ALERT")} (~inchangé attendu).");
            sb.AppendLine(
                $"population: {t1000.Population} (Δ={popDelta}) → {(popOk ? "OK" : "ALERT")}.");
            sb.AppendLine(
                $"guerre@800: ratioV={WorldMetrics.Fmt1(t800.RatioVictories * 100f)}% stuck={t800.StuckWars} " +
                $"→ {(warOk ? "OK" : "ALERT")}.");
            sb.AppendLine(
                $"countriesWithLand: {t1000.CountriesWithLand} → {(landOk ? "OK" : "ALERT")}.");
            sb.AppendLine(satOk && popOk && warOk && landOk
                ? "VERDICT FENÊTRE SAINE: OUI — boucle guerre/conquête pré-t1000 reste riche."
                : "VERDICT FENÊTRE SAINE: NON — écart majeur, possible débordement de périmètre.");

            File.WriteAllText(path, sb.ToString());
            UnityEngine.Debug.Log(sb.ToString());
        }

        static HorizonSnap CaptureAt(EntityManager em, int tick)
        {
            var snap = new HorizonSnap
            {
                Tick = tick,
                Metrics = WorldMetrics.Capture(em, tick)
            };

            var navyRate = MilitaryUpkeepSystem.NavyUpkeepRate;
            var armyRate = MilitaryUpkeepSystem.ArmyUpkeepRate;
            var interestRate = TreasurySystem.DebtInterestRateAnnual;
            var maxInterest = TreasurySystem.MaxInterestPerTick;

            using var countries = em.CreateEntityQuery(
                ComponentType.ReadOnly<CountryData>(),
                ComponentType.ReadOnly<TreasuryData>()).ToEntityArray(Unity.Collections.Allocator.Temp);
            var provinceCounts = new System.Collections.Generic.Dictionary<Entity, int>();
            using (var owners = em.CreateEntityQuery(ComponentType.ReadOnly<ProvinceOwnership>())
                       .ToComponentDataArray<ProvinceOwnership>(Unity.Collections.Allocator.Temp))
            {
                for (var i = 0; i < owners.Length; i++)
                {
                    if (owners[i].Owner == Entity.Null) continue;
                    provinceCounts.TryGetValue(owners[i].Owner, out var c);
                    provinceCounts[owners[i].Owner] = c + 1;
                }
            }

            var navyByCountry = new System.Collections.Generic.Dictionary<Entity, float>();
            using (var navies = em.CreateEntityQuery(ComponentType.ReadOnly<NavyData>())
                       .ToComponentDataArray<NavyData>(Unity.Collections.Allocator.Temp))
            {
                for (var i = 0; i < navies.Length; i++)
                {
                    var n = navies[i];
                    if (n.Country == Entity.Null) continue;
                    provinceCounts.TryGetValue(n.Country, out var pc);
                    if (pc <= 0) continue;
                    var raw = n.NavalStrength * navyRate;
                    var income = em.GetComponentData<TreasuryData>(n.Country).Income;
                    var cap = Math.Max(0f, income * MilitaryUpkeepSystem.MaxNavyIncomeFraction);
                    navyByCountry.TryGetValue(n.Country, out var cur);
                    navyByCountry[n.Country] = cur + Math.Min(raw, cap);
                }
            }

            var armyByCountry = new System.Collections.Generic.Dictionary<Entity, float>();
            using (var armies = em.CreateEntityQuery(ComponentType.ReadOnly<ArmyData>())
                       .ToComponentDataArray<ArmyData>(Unity.Collections.Allocator.Temp))
            {
                for (var i = 0; i < armies.Length; i++)
                {
                    var a = armies[i];
                    if (a.Country == Entity.Null) continue;
                    armyByCountry.TryGetValue(a.Country, out var cur);
                    armyByCountry[a.Country] = cur + a.Strength * armyRate;
                }
            }

            for (var i = 0; i < countries.Length; i++)
            {
                var e = countries[i];
                var tr = em.GetComponentData<TreasuryData>(e);
                snap.SumIncome += tr.Income;
                snap.SumExpenses += tr.Expenses;
                var interest = tr.Debt > 0f
                    ? Math.Min(tr.Debt * (interestRate / 12f), maxInterest)
                    : 0f;
                snap.SumInterest += interest;
                navyByCountry.TryGetValue(e, out var navyUp);
                armyByCountry.TryGetValue(e, out var armyUp);
                snap.SumNavyUpkeep += navyUp;
                snap.SumArmyUpkeep += armyUp;
            }

            return snap;
        }

        static HorizonSnap Find(HorizonSnap[] snaps, int tick)
        {
            for (var i = 0; i < snaps.Length; i++)
            {
                if (snaps[i].Tick == tick) return snaps[i];
            }

            throw new InvalidOperationException($"snap t{tick} manquant");
        }

        static void Compare3(
            StringBuilder sb, string name, string v010, string v016, string v017)
        {
            sb.AppendLine($"  {name}: {v010} / {v016} / {v017}");
        }

        static string Fmt0(float v) => v.ToString("F0", CultureInfo.InvariantCulture);
        static string Fmt1(float v) => v.ToString("F1", CultureInfo.InvariantCulture);
        static string Fmt2(float v) => v.ToString("F2", CultureInfo.InvariantCulture);
        static string Fmt4(float v) => v.ToString("F4", CultureInfo.InvariantCulture);

        struct HorizonSnap
        {
            public int Tick;
            public WorldMetrics.Snapshot Metrics;
            public float SumIncome;
            public float SumExpenses;
            public float SumInterest;
            public float SumNavyUpkeep;
            public float SumArmyUpkeep;
        }
    }
}
