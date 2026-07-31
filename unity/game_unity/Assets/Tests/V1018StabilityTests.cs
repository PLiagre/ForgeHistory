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
    /// Batch : -executeMethod VictoriaGame.Tests.V1018BatchRunner.Run
    /// Sweep : -executeMethod VictoriaGame.Tests.V1018BatchRunner.RunSweep
    /// </summary>
    public static class V1018BatchRunner
    {
        public static void Run()
        {
            V1018StabilityTests.RunAllAndWriteLogs();
            UnityEngine.Debug.Log("V1018BatchRunner: DONE");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
        }

        public static void RunSweep()
        {
            V1018StabilityTests.RunSweepAndWriteLog();
            UnityEngine.Debug.Log("V1018BatchRunner.RunSweep: DONE");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
        }
    }

    /// <summary>
    /// v1_018 — pression fiscale réelle (guerre + surextension) sous filet anti-spirale.
    /// Garde-fou long-horizon permanent (t3000) : dette bornée ET non nulle, armée survivante, monde vivant.
    /// </summary>
    [TestFixture]
    public class V1018StabilityTests
    {
        const uint Seed = 42195u;
        static readonly int[] HorizonTicks = { 1000, 1200, 1500, 2000, 2500, 3000 };

        // Ancrages v1_017 (confortable : dette nulle).
        const float V1017Debt1000 = 0.0f;
        const float V1017Debt3000 = 0.0f;
        const float V1017Army1000 = 57927f;
        const float V1017Army3000 = 22104f;
        const int V1017Victories1500 = 36;
        const int V1017Victories3000 = 42;
        const int V1017Bankrupt1000 = 0;
        const string V1017RatioV800 = "66.7";
        const string V1017Sat = "0.698";
        const int V1017Pop = 142551;
        const int V1017NonCore = 14;
        const int V1017Countries = 15;
        const int V1017MaxProv = 11;
        const int V1017Stuck800 = 0;
        const int V1017Annexed800 = 14;

        // Rappel spirale v1_015.
        const float BeforeDebt3000 = 15429.4f;
        const float BeforeArmy3000 = 0f;
        const float BeforeInterest3000 = 64.29f;
        const float BeforeNavy3000 = 29.45f;

        // Bornes v1_018.
        const float ArmyScaleLow = 25000f;
        const float ArmyScaleHigh = 70000f;
        const float DebtFloorAny = 1.0f;
        const float DebtCeilingT3000 = 5000f;

        struct CostConfig
        {
            public string Label;
            public float ArmyUpkeep;
            public float RecruitScale;
            public float AdminPerProvince;
            public float AdminSuperlinear;
        }

        [Test]
        public void V1018_LongHorizonStability_T3000() => RunStabilityAssertions();

        [Test]
        public void V1018_PublishStabilityAndMeasurementLogs() => RunAllAndWriteLogs();

        /// <summary>
        /// Balayage pression (guerre + surextension) vs survie. Écrit game_unity/Logs/v1_018_sweep.log.
        /// </summary>
        public static void RunSweepAndWriteLog()
        {
            var logsDir = Path.Combine(UnityEngine.Application.dataPath, "..", "Logs");
            Directory.CreateDirectory(logsDir);
            var path = Path.Combine(logsDir, "v1_018_sweep.log");

            var configs = BuildSweepConfigs();
            var prevArmy = MilitaryUpkeepSystem.ArmyUpkeepRate;
            var prevRecruit = TemplateRecruitSystem.RecruitCostScale;
            var prevAdmin = MilitaryUpkeepSystem.AdminCostPerProvince;
            var prevSuper = MilitaryUpkeepSystem.AdminSuperlinearPerProvince;

            var sb = new StringBuilder(16 * 1024);
            sb.AppendLine($"=== v1_018 SWEEP pression vs survie seed={Seed} ===");
            sb.AppendLine(
                "Filet GARDÉ: MaxNavyIncomeFraction=0.5, MaxInterestPerTick=1.5, MaxCountryDebt=1200,");
            sb.AppendLine(
                $"DebtInterestRateAnnual={Fmt4(TreasurySystem.DebtInterestRateAnnual)}, démob flotte landless.");
            sb.AppendLine(
                "cfg | armyRate | recruit | admin | k | debt@t1000 | debt@t3000 | bankruptMax | " +
                "army@t1000 | army@t3000 | ratio | vict1500→3000 | pressureOK | surviveOK | livingOK");

            try
            {
                for (var i = 0; i < configs.Length; i++)
                {
                    var c = configs[i];
                    ApplyConfig(c);
                    using var harness = new SimulationHarness(Seed);
                    harness.RunTicks(1000);
                    var t1000 = CaptureAt(harness.EntityManager, 1000);
                    harness.RunTicks(500);
                    var t1500 = CaptureAt(harness.EntityManager, 1500);
                    harness.RunTicks(1500);
                    var t3000 = CaptureAt(harness.EntityManager, 3000);

                    var bankruptMax = Math.Max(
                        t1000.Metrics.BankruptCount,
                        Math.Max(t1500.Metrics.BankruptCount, t3000.Metrics.BankruptCount));
                    // Pression : dette non nulle à un horizon OU banqueroute observée.
                    var pressureOk = t1000.Metrics.TotalDebt > DebtFloorAny
                                     || t3000.Metrics.TotalDebt > DebtFloorAny
                                     || bankruptMax > 0;
                    var debtBounded = t3000.Metrics.TotalDebt <= DebtCeilingT3000
                                      && t3000.Metrics.TotalDebt < BeforeDebt3000 * 0.5f;
                    var ratio = t3000.Metrics.WorldArmyStr / Math.Max(1f, t1000.Metrics.WorldArmyStr);
                    var surviveOk = t3000.Metrics.WorldArmyStr > 1000f
                                    && ratio >= 0.25f
                                    && t1000.Metrics.ZombieArmyStrLandless < 0.5f
                                    && t3000.Metrics.ZombieArmyStrLandless < 0.5f
                                    && debtBounded;
                    var livingOk = t3000.Metrics.Victories > t1500.Metrics.Victories
                                   && t3000.Metrics.WarsDeclared > t1500.Metrics.WarsDeclared;

                    sb.AppendLine(
                        $"{c.Label} | {Fmt6(c.ArmyUpkeep)} | {Fmt2(c.RecruitScale)} | " +
                        $"{Fmt2(c.AdminPerProvince)} | {Fmt3(c.AdminSuperlinear)} | " +
                        $"{Fmt1(t1000.Metrics.TotalDebt)} | {Fmt1(t3000.Metrics.TotalDebt)} | " +
                        $"{bankruptMax} | {Fmt0(t1000.Metrics.WorldArmyStr)} | " +
                        $"{Fmt0(t3000.Metrics.WorldArmyStr)} | {Fmt2(ratio)} | " +
                        $"{t1500.Metrics.Victories}→{t3000.Metrics.Victories} | " +
                        $"{pressureOk} | {surviveOk} | {livingOk}");
                }
            }
            finally
            {
                MilitaryUpkeepSystem.ArmyUpkeepRate = prevArmy;
                TemplateRecruitSystem.RecruitCostScale = prevRecruit;
                MilitaryUpkeepSystem.AdminCostPerProvince = prevAdmin;
                MilitaryUpkeepSystem.AdminSuperlinearPerProvince = prevSuper;
            }

            sb.AppendLine();
            sb.AppendLine("=== LECTURE DU GENOU ===");
            sb.AppendLine(
                "Retenir le réglage où pressureOK∧surviveOK∧livingOK, dette qui respire, " +
                "sans army@t3000→0. Si aucun : meilleur compromis + tension nommée.");
            sb.AppendLine(
                $"Défauts production: ArmyUpkeep={Fmt6(MilitaryUpkeepSystem.DefaultArmyUpkeepRate)} " +
                $"Recruit={Fmt2(TemplateRecruitSystem.DefaultRecruitCostScale)} " +
                $"Admin={Fmt2(MilitaryUpkeepSystem.DefaultAdminCostPerProvince)} " +
                $"k={Fmt3(MilitaryUpkeepSystem.DefaultAdminSuperlinearPerProvince)}");

            File.WriteAllText(path, sb.ToString());
            UnityEngine.Debug.Log(sb.ToString());
        }

        public static void RunAllAndWriteLogs()
        {
            var logsDir = Path.Combine(UnityEngine.Application.dataPath, "..", "Logs");
            Directory.CreateDirectory(logsDir);

            // Remettre les défauts production (au cas où un sweep précédent a laissé des statics).
            ResetToDefaults();

            HorizonSnap[] snaps;
            WorldMetrics.Snapshot t800;
            using (var harness = new SimulationHarness(Seed))
            {
                harness.RunTicks(800);
                t800 = WorldMetrics.Capture(harness.EntityManager, 800);
                snaps = CaptureHorizon(harness, 800);
            }

            WriteStabilityLog(Path.Combine(logsDir, "v1_018_stability.log"), snaps);
            WriteMeasurementsLog(Path.Combine(logsDir, "v1_018_measurements.log"), t800, snaps);
            AssertStability(snaps);
        }

        static void RunStabilityAssertions()
        {
            ResetToDefaults();
            using var harness = new SimulationHarness(Seed);
            var snaps = CaptureHorizon(harness, 0);
            AssertStability(snaps);
        }

        static CostConfig[] BuildSweepConfigs()
        {
            // Courbe curatée : baseline v1_017 → guerre seule → admin seule → combinés → agressif.
            return new[]
            {
                new CostConfig
                {
                    Label = "A_base_v017", ArmyUpkeep = 0.00012f, RecruitScale = 0.05f,
                    AdminPerProvince = 0.10f, AdminSuperlinear = 0f
                },
                new CostConfig
                {
                    Label = "B_war_mild", ArmyUpkeep = 0.00022f, RecruitScale = 0.10f,
                    AdminPerProvince = 0.10f, AdminSuperlinear = 0f
                },
                new CostConfig
                {
                    Label = "C_war_strong", ArmyUpkeep = 0.00036f, RecruitScale = 0.15f,
                    AdminPerProvince = 0.10f, AdminSuperlinear = 0f
                },
                new CostConfig
                {
                    Label = "D_admin_mild", ArmyUpkeep = 0.00012f, RecruitScale = 0.05f,
                    AdminPerProvince = 0.40f, AdminSuperlinear = 0f
                },
                new CostConfig
                {
                    Label = "E_admin_strong", ArmyUpkeep = 0.00012f, RecruitScale = 0.05f,
                    AdminPerProvince = 0.80f, AdminSuperlinear = 0f
                },
                new CostConfig
                {
                    Label = "F_both_mild", ArmyUpkeep = 0.00022f, RecruitScale = 0.10f,
                    AdminPerProvince = 0.35f, AdminSuperlinear = 0f
                },
                new CostConfig
                {
                    Label = "G_retenu", ArmyUpkeep = MilitaryUpkeepSystem.DefaultArmyUpkeepRate,
                    RecruitScale = TemplateRecruitSystem.DefaultRecruitCostScale,
                    AdminPerProvince = MilitaryUpkeepSystem.DefaultAdminCostPerProvince,
                    AdminSuperlinear = MilitaryUpkeepSystem.DefaultAdminSuperlinearPerProvince
                },
                new CostConfig
                {
                    Label = "H_both_super", ArmyUpkeep = 0.00028f, RecruitScale = 0.12f,
                    AdminPerProvince = 0.40f, AdminSuperlinear = 0.020f
                },
                new CostConfig
                {
                    Label = "I_aggressive", ArmyUpkeep = 0.00040f, RecruitScale = 0.18f,
                    AdminPerProvince = 0.70f, AdminSuperlinear = 0.020f
                },
            };
        }

        static void ApplyConfig(CostConfig c)
        {
            MilitaryUpkeepSystem.ArmyUpkeepRate = c.ArmyUpkeep;
            TemplateRecruitSystem.RecruitCostScale = c.RecruitScale;
            MilitaryUpkeepSystem.AdminCostPerProvince = c.AdminPerProvince;
            MilitaryUpkeepSystem.AdminSuperlinearPerProvince = c.AdminSuperlinear;
            MilitaryUpkeepSystem.CostMode = AdminCostMode.PerProvince;
        }

        static void ResetToDefaults()
        {
            MilitaryUpkeepSystem.ArmyUpkeepRate = MilitaryUpkeepSystem.DefaultArmyUpkeepRate;
            TemplateRecruitSystem.RecruitCostScale = TemplateRecruitSystem.DefaultRecruitCostScale;
            MilitaryUpkeepSystem.AdminCostPerProvince = MilitaryUpkeepSystem.DefaultAdminCostPerProvince;
            MilitaryUpkeepSystem.AdminSuperlinearPerProvince =
                MilitaryUpkeepSystem.DefaultAdminSuperlinearPerProvince;
            MilitaryUpkeepSystem.CostMode = AdminCostMode.PerProvince;
            ArmyDisbandmentSystem.ReinforceRateFactor =
                ArmyDisbandmentSystem.DefaultReinforceRateFactor;
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

            // (A) DETTE BORNÉE — pas de spirale.
            Assert.Less(t3000.Metrics.TotalDebt, BeforeDebt3000 * 0.5f,
                "DETTE: doit rester franchement sous la spirale v1_015 (15429).");
            Assert.LessOrEqual(t3000.Metrics.TotalDebt, DebtCeilingT3000,
                $"DETTE BORNÉE: debt@t3000={Fmt1(t3000.Metrics.TotalDebt)} plafond {DebtCeilingT3000}.");
            Assert.Less(t3000.Metrics.TotalDebt / Math.Max(1f, t2500.Metrics.TotalDebt), 1.8f,
                "DETTE: doublement t2500→t3000 interdit.");
            Assert.Less(t2500.Metrics.TotalDebt / Math.Max(1f, t2000.Metrics.TotalDebt), 1.8f,
                "DETTE: doublement t2000→t2500 interdit.");

            // Pression : dette non nulle OU banqueroute observée sur l'horizon.
            var anyBankrupt = false;
            var anyDebt = false;
            for (var i = 0; i < snaps.Length; i++)
            {
                if (snaps[i].Metrics.BankruptCount > 0) anyBankrupt = true;
                if (snaps[i].Metrics.TotalDebt > DebtFloorAny) anyDebt = true;
            }

            Assert.IsTrue(anyDebt || anyBankrupt,
                "PRESSION: dette mondiale doit être non nulle sur des périodes OU bankruptCount>0 " +
                "(l'argent doit contraindre — échec si confort v1_017).");

            // (B) ARMÉE SURVIVANTE (ReinforceRateFactor INTANGIBLE = 0.18)
            Assert.GreaterOrEqual(t1000.Metrics.WorldArmyStr, ArmyScaleLow,
                $"ARMÉE: @t1000={Fmt0(t1000.Metrics.WorldArmyStr)} trop faible.");
            Assert.LessOrEqual(t1000.Metrics.WorldArmyStr, ArmyScaleHigh,
                $"ARMÉE: @t1000={Fmt0(t1000.Metrics.WorldArmyStr)} hors bande (gates trop lâches?).");
            Assert.Greater(t3000.Metrics.WorldArmyStr, 1000f,
                $"ARMÉE SURVIE: worldArmyStr@t3000={Fmt0(t3000.Metrics.WorldArmyStr)} (effondrement interdit).");
            Assert.GreaterOrEqual(t3000.Metrics.WorldArmyStr, t1000.Metrics.WorldArmyStr * 0.25f,
                $"ARMÉE SURVIE: @t3000={Fmt0(t3000.Metrics.WorldArmyStr)} fraction de " +
                $"@t1000={Fmt0(t1000.Metrics.WorldArmyStr)} trop basse.");
            Assert.Less(t1000.Metrics.ZombieArmyStrLandless, 0.5f, "ZOMBIE@t1000: doit être 0.");
            Assert.Less(t3000.Metrics.ZombieArmyStrLandless, 0.5f, "ZOMBIE@t3000: doit être 0.");

            // (C) MONDE VIVANT
            Assert.Greater(t3000.Metrics.Victories, t1500.Metrics.Victories,
                $"MONDE: victories@t3000={t3000.Metrics.Victories} vs @t1500={t1500.Metrics.Victories}.");
            Assert.Greater(t3000.Metrics.WarsDeclared, t1500.Metrics.WarsDeclared,
                "MONDE: warsDeclared doit continuer après t1500.");
        }

        static void WriteStabilityLog(string path, HorizonSnap[] snaps)
        {
            var sb = new StringBuilder(64 * 1024);
            sb.AppendLine($"=== v1_018 STABILITÉ LONG-HORIZON seed={Seed} ===");
            sb.AppendLine("PRESSION FISCALE v1_017 → v1_018 (filet anti-spirale GARDÉ) :");
            sb.AppendLine(
                $"  ArmyUpkeepRate: 0.000120 → {Fmt6(MilitaryUpkeepSystem.DefaultArmyUpkeepRate)} " +
                "(guerre coûte — entretien)");
            sb.AppendLine(
                $"  RecruitCostScale: 0.05 → {Fmt2(TemplateRecruitSystem.DefaultRecruitCostScale)} " +
                "(guerre coûte — recrutement)");
            sb.AppendLine(
                $"  AdminCostPerProvince: 0.10 → {Fmt2(MilitaryUpkeepSystem.DefaultAdminCostPerProvince)} " +
                "(surextension)");
            sb.AppendLine(
                $"  AdminSuperlinearPerProvince: 0 → {Fmt3(MilitaryUpkeepSystem.DefaultAdminSuperlinearPerProvince)} " +
                "(anti-blob surlinéaire)");
            sb.AppendLine(
                "  GARDÉS: MaxNavyIncomeFraction=0.5, MaxInterestPerTick=1.5, MaxCountryDebt=1200,");
            sb.AppendLine(
                $"          démob flotte landless, DebtInterestRateAnnual={Fmt4(TreasurySystem.DebtInterestRateAnnual)},");
            sb.AppendLine(
                $"          DebtRepayBuffer={Fmt1(TreasuryManagementSystem.DebtRepayBuffer)}, " +
                $"DebtRepayFraction={Fmt2(TreasuryManagementSystem.DebtRepayFraction)}, " +
                $"Haircut={Fmt2(TreasurySystem.BankruptcyHaircut)},");
            sb.AppendLine(
                $"          ReinforceRateFactor={Fmt2(ArmyDisbandmentSystem.DefaultReinforceRateFactor)} (INTANGIBLE).");
            sb.AppendLine();

            sb.AppendLine("=== CÔTE À CÔTE v1_017 (confort) / v1_018 (pression) ===");
            sb.AppendLine(
                "tick | debt_v017 | debt_v018 | army_v017 | army_v018 | vict_v017 | vict_v018 | bankrupt_v018");
            sb.AppendLine(
                $"1000 | {Fmt1(V1017Debt1000)} | {Fmt1(Find(snaps, 1000).Metrics.TotalDebt)} | " +
                $"{Fmt0(V1017Army1000)} | {Fmt0(Find(snaps, 1000).Metrics.WorldArmyStr)} | " +
                $"28 | {Find(snaps, 1000).Metrics.Victories} | {Find(snaps, 1000).Metrics.BankruptCount}");
            sb.AppendLine(
                $"1500 | 0.0 | {Fmt1(Find(snaps, 1500).Metrics.TotalDebt)} | " +
                $"{Fmt0(26185f)} | {Fmt0(Find(snaps, 1500).Metrics.WorldArmyStr)} | " +
                $"{V1017Victories1500} | {Find(snaps, 1500).Metrics.Victories} | " +
                $"{Find(snaps, 1500).Metrics.BankruptCount}");
            sb.AppendLine(
                $"3000 | {Fmt1(V1017Debt3000)} | {Fmt1(Find(snaps, 3000).Metrics.TotalDebt)} | " +
                $"{Fmt0(V1017Army3000)} | {Fmt0(Find(snaps, 3000).Metrics.WorldArmyStr)} | " +
                $"{V1017Victories3000} | {Find(snaps, 3000).Metrics.Victories} | " +
                $"{Find(snaps, 3000).Metrics.BankruptCount}");
            sb.AppendLine();

            sb.AppendLine("=== TRAJECTOIRE v1_018 (dette qui respire) ===");
            sb.AppendLine(
                "tick | debt | army | victories | whitePeaces | annexed | warsDeclared | " +
                "bankrupt | interest | navy | armyUp | income | expenses | I-E | net | zombie");
            for (var i = 0; i < snaps.Length; i++)
            {
                var s = snaps[i];
                var m = s.Metrics;
                sb.AppendLine(
                    $"{s.Tick} | {Fmt1(m.TotalDebt)} | {Fmt0(m.WorldArmyStr)} | {m.Victories} | " +
                    $"{m.WhitePeaces} | {m.AnnexedProvinces} | {m.WarsDeclared} | " +
                    $"{m.BankruptCount} | {Fmt2(s.SumInterest)} | {Fmt2(s.SumNavyUpkeep)} | " +
                    $"{Fmt2(s.SumArmyUpkeep)} | {Fmt2(s.SumIncome)} | {Fmt2(s.SumExpenses)} | " +
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

            var anyBankrupt = false;
            var peakDebt = 0f;
            var troughDebtAfterPeak = float.MaxValue;
            var sawPeak = false;
            for (var i = 0; i < snaps.Length; i++)
            {
                var d = snaps[i].Metrics.TotalDebt;
                if (snaps[i].Metrics.BankruptCount > 0) anyBankrupt = true;
                if (d > peakDebt) { peakDebt = d; sawPeak = true; troughDebtAfterPeak = d; }
                else if (sawPeak && d < troughDebtAfterPeak) troughDebtAfterPeak = d;
            }

            var debtNonZero = peakDebt > DebtFloorAny || anyBankrupt;
            var debtBounded = t3000.Metrics.TotalDebt <= DebtCeilingT3000
                              && t3000.Metrics.TotalDebt < BeforeDebt3000 * 0.5f
                              && t3000.Metrics.TotalDebt / Math.Max(1f, t2500.Metrics.TotalDebt) < 1.8f;
            var debtBreathes = peakDebt > DebtFloorAny
                               && troughDebtAfterPeak < peakDebt * 0.85f;
            var debtOk = debtNonZero && debtBounded;

            var armyRatio = t3000.Metrics.WorldArmyStr / Math.Max(1f, t1000.Metrics.WorldArmyStr);
            var armyOk = t1000.Metrics.WorldArmyStr >= ArmyScaleLow
                         && t1000.Metrics.WorldArmyStr <= ArmyScaleHigh
                         && t3000.Metrics.WorldArmyStr > 1000f
                         && armyRatio >= 0.25f;
            var livingOk = t3000.Metrics.Victories > t1500.Metrics.Victories
                           && t3000.Metrics.WarsDeclared > t1500.Metrics.WarsDeclared;
            var zombieOk = t3000.Metrics.ZombieArmyStrLandless < 0.5f;

            sb.AppendLine();
            sb.AppendLine("=== VERDICT PAR CRITÈRE ===");
            sb.AppendLine(
                $"(A) DETTE BORNÉE NON NULLE: {(debtOk ? "OUI" : "NON")} — " +
                $"peak={Fmt1(peakDebt)} troughAprès={Fmt1(troughDebtAfterPeak)} " +
                $"@t1000={Fmt1(t1000.Metrics.TotalDebt)} → @t3000={Fmt1(t3000.Metrics.TotalDebt)} " +
                $"(v1_017=0 ; v1_015 spirale {Fmt1(BeforeDebt3000)}). " +
                $"bankruptAny={anyBankrupt} ; respire={(debtBreathes ? "OUI" : "PARTIEL/NON")} ; " +
                $"interest@t3000={Fmt2(t3000.SumInterest)} (v1_015={Fmt2(BeforeInterest3000)}).");
            if (!debtNonZero)
            {
                sb.AppendLine(
                    "  TENSION: pression nulle — leviers trop faibles face au filet / gates.");
            }

            if (!debtBounded)
                sb.AppendLine("  TENSION: spirale ou doublement — pression trop haute.");
            if (debtNonZero && !debtBreathes)
                sb.AppendLine(
                    "  NOTE: dette non nulle mais peu de respiration paix/guerre sur les snapshots.");

            sb.AppendLine(
                $"(B) ARMÉE SURVIE: {(armyOk ? "OUI" : "NON")} — " +
                $"army@t1000={Fmt0(t1000.Metrics.WorldArmyStr)} → @t3000={Fmt0(t3000.Metrics.WorldArmyStr)} " +
                $"(ratio={Fmt2(armyRatio)} ; v1_017 {Fmt0(V1017Army1000)}→{Fmt0(V1017Army3000)} ; " +
                $"v1_015→{Fmt0(BeforeArmy3000)}). zombie@t3000={Fmt0(t3000.Metrics.ZombieArmyStrLandless)}.");
            if (!armyOk)
                sb.AppendLine("  TENSION: armée gated / effondrée sous la pression fiscale.");

            sb.AppendLine(
                $"(C) MONDE VIVANT: {(livingOk ? "OUI" : "NON")} — " +
                $"victories @t1500={t1500.Metrics.Victories} → @t3000={t3000.Metrics.Victories} ; " +
                $"warsDeclared {t1500.Metrics.WarsDeclared}→{t3000.Metrics.WarsDeclared} ; " +
                $"annexed {t1500.Metrics.AnnexedProvinces}→{t3000.Metrics.AnnexedProvinces}.");
            sb.AppendLine(
                $"ZOMBIE eco_026: {(zombieOk ? "OUI" : "NON")} — navy@t3000={Fmt2(t3000.SumNavyUpkeep)} " +
                $"(v1_015={Fmt2(BeforeNavy3000)}).");

            sb.AppendLine();
            if (debtOk && armyOk && livingOk && zombieOk && anyBankrupt && debtBreathes)
            {
                sb.AppendLine(
                    "VERDICT GLOBAL: OUI — pression réelle (dette+banqueroute+respiration) " +
                    "+ armée survivante + monde vivant sous filet.");
            }
            else if (debtOk && armyOk && livingOk && zombieOk)
            {
                sb.AppendLine(
                    "VERDICT GLOBAL: OUI (pression partielle) — dette bornée non nulle + survie ; " +
                    "banqueroute/respiration éventuellement partielles (voir notes).");
            }
            else if (debtBounded && armyOk && livingOk && zombieOk)
            {
                sb.AppendLine(
                    "VERDICT GLOBAL: COMPROMIS — survie OK, dette bornée, mais pression insuffisante " +
                    "ou tension nommée ci-dessus.");
            }
            else
            {
                sb.AppendLine(
                    "VERDICT GLOBAL: NON / COMPROMIS — critères non joints ; voir tensions.");
            }

            File.WriteAllText(path, sb.ToString());
            UnityEngine.Debug.Log(sb.ToString());
        }

        static void WriteMeasurementsLog(
            string path, WorldMetrics.Snapshot t800, HorizonSnap[] snaps)
        {
            var t1000 = Find(snaps, 1000).Metrics;
            var sb = new StringBuilder(16 * 1024);
            sb.AppendLine($"=== v1_018 ANCRAGES seed={Seed} ===");
            sb.AppendLine(
                $"ArmyUpkeepRate={Fmt6(MilitaryUpkeepSystem.DefaultArmyUpkeepRate)} " +
                $"RecruitCostScale={Fmt2(TemplateRecruitSystem.DefaultRecruitCostScale)} " +
                $"AdminCostPerProvince={Fmt2(MilitaryUpkeepSystem.DefaultAdminCostPerProvince)} " +
                $"AdminSuperlinear={Fmt3(MilitaryUpkeepSystem.DefaultAdminSuperlinearPerProvince)} " +
                $"MaxNavyIncomeFraction={Fmt2(MilitaryUpkeepSystem.MaxNavyIncomeFraction)} " +
                $"MaxInterestPerTick={Fmt2(TreasurySystem.MaxInterestPerTick)} " +
                $"MaxCountryDebt={Fmt1(TreasurySystem.MaxCountryDebt)} " +
                $"ReinforceRateFactor={Fmt2(ArmyDisbandmentSystem.DefaultReinforceRateFactor)}");
            sb.AppendLine("WorldMetrics.Capture / FormatStandardLine (règle test_001).");
            sb.AppendLine("Colonnes: v1_017 (confort) / v1_018 (pression).");
            sb.AppendLine();

            sb.AppendLine(WorldMetrics.FormatStandardLine(800, t800));
            sb.AppendLine(WorldMetrics.FormatStandardLine(1000, t1000));
            sb.AppendLine();

            sb.AppendLine("=== COMPARAISON v1_017 / v1_018 (12 ancrages) ===");
            Compare2(sb, "nonCore", $"{V1017NonCore}/50",
                $"{t1000.NonCoreProvinces}/{t1000.TotalProvincesOwned}");
            Compare2(sb, "countriesWithLand", V1017Countries.ToString(),
                t1000.CountriesWithLand.ToString());
            Compare2(sb, "maxProvinces", V1017MaxProv.ToString(),
                t1000.MaxProvincesOneCountry.ToString());
            Compare2(sb, "totalDebt", "0.0", WorldMetrics.Fmt1(t1000.TotalDebt));
            Compare2(sb, "bankrupt", V1017Bankrupt1000.ToString(), t1000.BankruptCount.ToString());
            Compare2(sb, "worldArmyStr", "57927", WorldMetrics.Fmt0(t1000.WorldArmyStr));
            Compare2(sb, "zombie", "0", WorldMetrics.Fmt0(t1000.ZombieArmyStrLandless));
            Compare2(sb, "needsSatAvg", V1017Sat, WorldMetrics.Fmt3(t1000.NeedsSatAvg));
            Compare2(sb, "population", V1017Pop.ToString(), t1000.Population.ToString());
            Compare2(sb, "ratioV@800", V1017RatioV800 + "%",
                WorldMetrics.Fmt1(t800.RatioVictories * 100f) + "%");
            Compare2(sb, "stuck@800", V1017Stuck800.ToString(), t800.StuckWars.ToString());
            Compare2(sb, "annexed@800", V1017Annexed800.ToString(),
                t800.AnnexedProvinces.ToString());

            sb.AppendLine();
            sb.AppendLine("=== NOUVEAU JEU D'ANCRAGES DE RÉFÉRENCE (v1_018) ===");
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

            sb.AppendLine();
            sb.AppendLine("=== NON-RÉGRESSION (leviers fiscaux, pas démographiques) ===");
            var satDelta = Math.Abs(t1000.NeedsSatAvg - 0.698);
            var popDelta = Math.Abs(t1000.Population - V1017Pop);
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
                ? "VERDICT FENÊTRE SAINE: OUI — pop/sat ~inchangés ; boucle guerre pré-t1000 riche."
                : "VERDICT FENÊTRE SAINE: NON — écart majeur.");

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

        static void Compare2(StringBuilder sb, string name, string v017, string v018)
        {
            sb.AppendLine($"  {name}: {v017} / {v018}");
        }

        static string Fmt0(float v) => v.ToString("F0", CultureInfo.InvariantCulture);
        static string Fmt1(float v) => v.ToString("F1", CultureInfo.InvariantCulture);
        static string Fmt2(float v) => v.ToString("F2", CultureInfo.InvariantCulture);
        static string Fmt3(float v) => v.ToString("F3", CultureInfo.InvariantCulture);
        static string Fmt4(float v) => v.ToString("F4", CultureInfo.InvariantCulture);
        static string Fmt6(float v) => v.ToString("F6", CultureInfo.InvariantCulture);

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
