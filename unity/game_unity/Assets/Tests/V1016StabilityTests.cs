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
    /// <summary>Batch : -executeMethod VictoriaGame.Tests.V1016BatchRunner.Run</summary>
    public static class V1016BatchRunner
    {
        public static void Run()
        {
            V1016StabilityTests.RunAllAndWriteLogs();
            UnityEngine.Debug.Log("V1016BatchRunner: DONE");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
        }
    }

    /// <summary>
    /// v1_016 — correctifs dette/marine + garde-fou long-horizon permanent (t3000).
    /// </summary>
    [TestFixture]
    public class V1016StabilityTests
    {
        const uint Seed = 42195u;
        static readonly int[] HorizonTicks = { 1000, 1200, 1500, 2000, 2500, 3000 };

        // Ancrages v1_010 (référence pré-v1_016 pour P6).
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

        [Test]
        public void V1016_LongHorizonStability_T3000() => RunStabilityAssertions();

        [Test]
        public void V1016_PublishStabilityAndMeasurementLogs() => RunAllAndWriteLogs();

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

            WriteStabilityLog(Path.Combine(logsDir, "v1_016_stability.log"), snaps);
            WriteMeasurementsLog(Path.Combine(logsDir, "v1_016_measurements.log"), t800, snaps);
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

            // (A) DETTE BORNÉE — pas de spirale ×34 ; viser ≤ ~2× debt@t1000, plafond dur.
            Assert.LessOrEqual(t3000.Metrics.TotalDebt, Math.Max(t1000.Metrics.TotalDebt * 2.5f, 2500f),
                $"DETTE: debt@t3000={Fmt1(t3000.Metrics.TotalDebt)} vs debt@t1000={Fmt1(t1000.Metrics.TotalDebt)}");
            Assert.Less(t3000.Metrics.TotalDebt, BeforeDebt3000 * 0.5f,
                "DETTE: doit rester franchement sous la spirale v1_015 (15429).");

            // Pas de doublement systématique tous les ~500 ticks sur la fin.
            Assert.Less(t3000.Metrics.TotalDebt / Math.Max(1f, t2500.Metrics.TotalDebt), 1.8f,
                "DETTE: doublement t2500→t3000 interdit.");
            Assert.Less(t2500.Metrics.TotalDebt / Math.Max(1f, t2000.Metrics.TotalDebt), 1.8f,
                "DETTE: doublement t2000→t2500 interdit.");

            // (B) ARMÉE SURVIVANTE
            Assert.Greater(t3000.Metrics.WorldArmyStr, 1000f,
                $"ARMÉE: worldArmyStr@t3000={Fmt0(t3000.Metrics.WorldArmyStr)} (effondrement à 0 interdit).");
            Assert.GreaterOrEqual(t3000.Metrics.WorldArmyStr, t1000.Metrics.WorldArmyStr * 0.35f,
                $"ARMÉE: @t3000={Fmt0(t3000.Metrics.WorldArmyStr)} doit rester une fraction saine de @t1000={Fmt0(t1000.Metrics.WorldArmyStr)}.");

            // Pas de zombie landless (eco_026).
            Assert.Less(t3000.Metrics.ZombieArmyStrLandless, 0.5f,
                "ZOMBIE: zombieArmyStrLandless doit rester 0.");

            // (C) MONDE VIVANT — victoires après t1500 ; territoire non figé.
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
            var sb = new StringBuilder(48 * 1024);
            sb.AppendLine($"=== v1_016 STABILITÉ LONG-HORIZON seed={Seed} ===");
            sb.AppendLine("Correctifs appliqués (ancienne → nouvelle) :");
            sb.AppendLine(
                $"  DebtInterestRateAnnual: 0.05 → {Fmt4(TreasurySystem.DebtInterestRateAnnual)}");
            sb.AppendLine(
                $"  MaxInterestPerTick: ∞ → {Fmt2(TreasurySystem.MaxInterestPerTick)}");
            sb.AppendLine(
                $"  MaxCountryDebt: ∞ → {Fmt1(TreasurySystem.MaxCountryDebt)}");
            sb.AppendLine(
                $"  BankruptcyHaircut: 0.70 → {Fmt2(TreasurySystem.BankruptcyHaircut)} (retained {Fmt2(1f - TreasurySystem.BankruptcyHaircut)})");
            sb.AppendLine(
                $"  DebtRepayBuffer: 75 → {Fmt1(TreasuryManagementSystem.DebtRepayBuffer)}");
            sb.AppendLine(
                $"  DebtRepayFraction: 0.20 → {Fmt2(TreasuryManagementSystem.DebtRepayFraction)}");
            sb.AppendLine(
                $"  MaxNavyIncomeFraction: ∞ → {Fmt2(MilitaryUpkeepSystem.MaxNavyIncomeFraction)}");
            sb.AppendLine(
                "  Landless navy: flotte démobilisée si 0 province (MilitaryUpkeepSystem).");
            sb.AppendLine(
                $"  ReinforceRateFactor: 0.16 → {Fmt2(ArmyDisbandmentSystem.DefaultReinforceRateFactor)}");
            sb.AppendLine(
                "  ArmyOrganization: reinforce pendant IsEngaged (×0.5 vétérans) + plancher supply 0.25.");
            sb.AppendLine(
                "  BattleResolution: IsEngaged=false si Strength≤0 (déblocage reconstitution).");
            sb.AppendLine();

            sb.AppendLine("=== AVANT (rappel v1_015, aucun correctif) ===");
            sb.AppendLine(
                $"tick | debt | army | victories | annexed | interest | navy");
            sb.AppendLine(
                $"1000 | {Fmt1(BeforeDebt1000)} | {Fmt0(BeforeArmy1000)} | 33 | 8 | 1.88 | 21.10");
            sb.AppendLine(
                $"1500 | 1652.4 | 9363 | {BeforeVictories1500} | 1 | 6.88 | 25.01");
            sb.AppendLine(
                $"2500 | 7689.5 | 0 | 40 | 0 | 32.04 | 28.99");
            sb.AppendLine(
                $"3000 | {Fmt1(BeforeDebt3000)} | {Fmt0(BeforeArmy3000)} | {BeforeVictories3000} | 0 | {Fmt2(BeforeInterest3000)} | {Fmt2(BeforeNavy3000)}");
            sb.AppendLine(
                "VERDICT v1_015: spirale dette ×34, armée→0, victoires figées, navy dominante.");
            sb.AppendLine();

            sb.AppendLine("=== APRÈS (v1_016 correctifs) ===");
            sb.AppendLine(
                "tick | debt | army | victories | whitePeaces | annexed | warsDeclared | " +
                "interest | navy | income | expenses | I-E | net | zombie");
            for (var i = 0; i < snaps.Length; i++)
            {
                var s = snaps[i];
                var m = s.Metrics;
                sb.AppendLine(
                    $"{s.Tick} | {Fmt1(m.TotalDebt)} | {Fmt0(m.WorldArmyStr)} | {m.Victories} | " +
                    $"{m.WhitePeaces} | {m.AnnexedProvinces} | {m.WarsDeclared} | " +
                    $"{Fmt2(s.SumInterest)} | {Fmt2(s.SumNavyUpkeep)} | {Fmt2(s.SumIncome)} | " +
                    $"{Fmt2(s.SumExpenses)} | {Fmt2(s.SumIncome - s.SumExpenses)} | " +
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
            var debtOk = t3000.Metrics.TotalDebt <= Math.Max(t1000.Metrics.TotalDebt * 2.5f, 2500f)
                         && t3000.Metrics.TotalDebt < BeforeDebt3000 * 0.5f
                         && t3000.Metrics.TotalDebt / Math.Max(1f, t2500.Metrics.TotalDebt) < 1.8f;
            var armyOk = t3000.Metrics.WorldArmyStr > 1000f
                         && t3000.Metrics.WorldArmyStr >= t1000.Metrics.WorldArmyStr * 0.35f;
            var livingOk = t3000.Metrics.Victories > t1500.Metrics.Victories
                           && t3000.Metrics.WarsDeclared > t1500.Metrics.WarsDeclared;
            var zombieOk = t3000.Metrics.ZombieArmyStrLandless < 0.5f;

            sb.AppendLine();
            sb.AppendLine("=== VERDICT PAR CRITÈRE ===");
            sb.AppendLine(
                $"(A) DETTE BORNÉE: {(debtOk ? "OUI" : "NON")} — " +
                $"debt@t1000={Fmt1(t1000.Metrics.TotalDebt)} → @t3000={Fmt1(t3000.Metrics.TotalDebt)} " +
                $"(×{Fmt2(debtRatio)} ; avant v1_015 ×{Fmt2(BeforeDebt3000 / BeforeDebt1000)}). " +
                $"interest@t3000={Fmt2(t3000.SumInterest)} (avant {Fmt2(BeforeInterest3000)}). " +
                $"navy@t3000={Fmt2(t3000.SumNavyUpkeep)} (avant {Fmt2(BeforeNavy3000)}).");
            sb.AppendLine(
                $"(B) ARMÉE SURVIVANTE: {(armyOk ? "OUI" : "NON")} — " +
                $"army@t1000={Fmt0(t1000.Metrics.WorldArmyStr)} → @t3000={Fmt0(t3000.Metrics.WorldArmyStr)} " +
                $"(ratio={Fmt2(armyRatio)} ; avant → 0). zombie={Fmt0(t3000.Metrics.ZombieArmyStrLandless)}.");
            sb.AppendLine(
                $"(C) MONDE VIVANT: {(livingOk ? "OUI" : "NON")} — " +
                $"victories @t1500={t1500.Metrics.Victories} → @t3000={t3000.Metrics.Victories} ; " +
                $"warsDeclared {t1500.Metrics.WarsDeclared}→{t3000.Metrics.WarsDeclared} ; " +
                $"annexed {t1500.Metrics.AnnexedProvinces}→{t3000.Metrics.AnnexedProvinces} ; " +
                $"maxProv {t1500.Metrics.MaxProvincesOneCountry}→{t3000.Metrics.MaxProvincesOneCountry}.");
            sb.AppendLine(
                $"ZOMBIE eco_026: {(zombieOk ? "OUI (0)" : "NON")} — frein insolvabilité NON débranché.");

            var allOk = debtOk && armyOk && livingOk && zombieOk;
            sb.AppendLine();
            sb.AppendLine(allOk
                ? "VERDICT GLOBAL: OUI — monde vivant et stable jusqu'à t3000."
                : "VERDICT GLOBAL: NON — correctif partiel ; critères non atteints listés ci-dessus.");

            File.WriteAllText(path, sb.ToString());
            UnityEngine.Debug.Log(sb.ToString());
        }

        static void WriteMeasurementsLog(
            string path, WorldMetrics.Snapshot t800, HorizonSnap[] snaps)
        {
            var t1000 = Find(snaps, 1000).Metrics;
            var sb = new StringBuilder(16 * 1024);
            sb.AppendLine($"=== v1_016 ANCRAGES seed={Seed} ===");
            sb.AppendLine(
                $"DebtInterestRateAnnual={Fmt4(TreasurySystem.DebtInterestRateAnnual)} " +
                $"MaxInterestPerTick={Fmt2(TreasurySystem.MaxInterestPerTick)} " +
                $"MaxCountryDebt={Fmt1(TreasurySystem.MaxCountryDebt)} " +
                $"Haircut={Fmt2(TreasurySystem.BankruptcyHaircut)} " +
                $"DebtRepayBuffer={Fmt1(TreasuryManagementSystem.DebtRepayBuffer)} " +
                $"DebtRepayFraction={Fmt2(TreasuryManagementSystem.DebtRepayFraction)} " +
                $"MaxNavyIncomeFraction={Fmt2(MilitaryUpkeepSystem.MaxNavyIncomeFraction)}");
            sb.AppendLine("WorldMetrics.Capture / FormatStandardLine (règle test_001).");
            sb.AppendLine(
                "Anciens (v1_010 @ rate=0.5): nonCore=8/50, land=17, maxProv=8, " +
                "debt=450.4, bankrupt=3, army=44804, zombie=0, sat=0.698, pop=142551 ; " +
                "ratioV@800=58.7%, stuck=0, annexed=7.");
            sb.AppendLine();

            sb.AppendLine(WorldMetrics.FormatStandardLine(800, t800));
            sb.AppendLine(WorldMetrics.FormatStandardLine(1000, t1000));
            sb.AppendLine();

            sb.AppendLine("=== COMPARAISON ANCIEN(v1_010) / NOUVEAU (12 ancrages) ===");
            Compare(sb, "nonCore", $"{OldNonCore}/50",
                $"{t1000.NonCoreProvinces}/{t1000.TotalProvincesOwned}");
            Compare(sb, "countriesWithLand", OldCountries.ToString(),
                t1000.CountriesWithLand.ToString());
            Compare(sb, "maxProvinces", OldMaxProv.ToString(),
                t1000.MaxProvincesOneCountry.ToString());
            Compare(sb, "totalDebt", OldDebt, WorldMetrics.Fmt1(t1000.TotalDebt));
            Compare(sb, "bankrupt", OldBankrupt.ToString(), t1000.BankruptCount.ToString());
            Compare(sb, "worldArmyStr", OldArmy, WorldMetrics.Fmt0(t1000.WorldArmyStr));
            Compare(sb, "zombie", OldZombie.ToString(),
                WorldMetrics.Fmt0(t1000.ZombieArmyStrLandless));
            Compare(sb, "needsSatAvg", OldSat, WorldMetrics.Fmt3(t1000.NeedsSatAvg));
            Compare(sb, "population", OldPop.ToString(), t1000.Population.ToString());
            Compare(sb, "ratioV@800", OldRatioV800 + "%",
                WorldMetrics.Fmt1(t800.RatioVictories * 100f) + "%");
            Compare(sb, "stuck@800", OldStuck800.ToString(), t800.StuckWars.ToString());
            Compare(sb, "annexed@800", OldAnnexed800.ToString(),
                t800.AnnexedProvinces.ToString());

            sb.AppendLine();
            sb.AppendLine("=== NOUVEAU JEU D'ANCRAGES DE RÉFÉRENCE ===");
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

        static void Compare(StringBuilder sb, string name, string oldVal, string newVal)
        {
            var mark = oldVal == newVal ? "=" : "→";
            sb.AppendLine($"  {name}: {oldVal} {mark} {newVal}");
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
