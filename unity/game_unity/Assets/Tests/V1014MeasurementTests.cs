using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using VictoriaGame.Military;
using VictoriaGame.Presentation;

namespace VictoriaGame.Tests
{
    /// <summary>Batch : -executeMethod VictoriaGame.Tests.V1014BatchRunner.Run</summary>
    public static class V1014BatchRunner
    {
        public static void Run()
        {
            V1014MeasurementTests.RunSweepAndAnchors();
            UnityEngine.Debug.Log("V1014BatchRunner: DONE");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
        }
    }

    /// <summary>
    /// v1_014 — recalibrage OccupationScoreRate sur le monde corrigé (v1_010).
    /// Balayage décisivité @800 VS économie @1000 ; genou = meilleure décisivité
    /// sans casser dette/banqueroutes vs baseline rate=0.5.
    /// </summary>
    [TestFixture]
    public class V1014MeasurementTests
    {
        const uint Seed = 42195u;

        /// <summary>Témoin 0 + gamme qui encadre / dépasse le rate actuel 0.5.</summary>
        static readonly float[] RatesTried =
        {
            0f, 0.5f, 0.8f, 1.1f, 1.4f, 1.7f, 2.0f
        };

        // Ancrages v1_010 (monde corrigé CountryId, rate=0.5).
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

        struct RateRow
        {
            public float Rate;
            public WorldMetrics.Snapshot T800;
            public WorldMetrics.Snapshot T1000;
        }

        [Test]
        public void V1014_SweepOccupationScoreRateAndReanchor() => RunSweepAndAnchors();

        public static void RunSweepAndAnchors()
        {
            var previousRate = OccupationScoreSystem.OccupationScoreRate;
            var sweepPath = Path.Combine(
                UnityEngine.Application.dataPath, "..", "Logs", "v1_014_sweep.log");
            var measurePath = Path.Combine(
                UnityEngine.Application.dataPath, "..", "Logs", "v1_014_measurements.log");
            Directory.CreateDirectory(Path.GetDirectoryName(sweepPath)!);

            try
            {
                var rows = new RateRow[RatesTried.Length];
                for (var i = 0; i < RatesTried.Length; i++)
                {
                    var rate = RatesTried[i];
                    OccupationScoreSystem.OccupationScoreRate = rate;
                    rows[i].Rate = rate;

                    using var harness = new SimulationHarness(Seed);
                    harness.RunTicks(800);
                    rows[i].T800 = WorldMetrics.Capture(harness.EntityManager, 800);
                    harness.RunTicks(200);
                    rows[i].T1000 = WorldMetrics.Capture(harness.EntityManager, 1000);
                }

                var baselineIdx = IndexOfRate(rows, 0.5f);
                Assert.GreaterOrEqual(baselineIdx, 0, "rate=0.5 doit être dans le balayage");
                var baseline = rows[baselineIdx];

                var kneeIdx = ChooseKnee(rows, baselineIdx);
                var knee = rows[kneeIdx];
                var kneeRate = knee.Rate;

                WriteSweepLog(sweepPath, rows, baselineIdx, kneeIdx);
                WriteMeasurementsLog(measurePath, knee);

                Assert.AreEqual(
                    kneeRate,
                    OccupationScoreSystem.DefaultOccupationScoreRate,
                    0.001f,
                    "DefaultOccupationScoreRate doit égaler le genou retenu.");

                // Contrôle levier : témoin rate=0 plus bas que rate retenu (si rate>0).
                var zeroIdx = IndexOfRate(rows, 0f);
                if (zeroIdx >= 0 && kneeRate > 0f)
                {
                    Assert.Less(
                        rows[zeroIdx].T800.RatioVictories,
                        knee.T800.RatioVictories + 0.001f,
                        "Témoin rate=0 : ratioV doit être ≤ au genou (levier isolé).");
                }

                Assert.AreEqual(0, knee.T800.StuckWars, "stuck@800 doit rester 0 au genou");
                Assert.LessOrEqual(
                    knee.T1000.ZombieArmyStrLandless, 0.5f, "zombie doit rester 0 au genou");
            }
            finally
            {
                OccupationScoreSystem.OccupationScoreRate = previousRate;
            }
        }

        /// <summary>
        /// Genou : parmi les rates dont bankrupt ≤ baseline et dette ≤ baseline×1.10,
        /// maximiser ratioV@800. Si aucun meilleur que 0.5, garder 0.5.
        /// </summary>
        static int ChooseKnee(RateRow[] rows, int baselineIdx)
        {
            var baseDebt = rows[baselineIdx].T1000.TotalDebt;
            var baseBankrupt = rows[baselineIdx].T1000.BankruptCount;
            var debtCap = baseDebt * 1.10f;

            var bestIdx = baselineIdx;
            var bestRatio = rows[baselineIdx].T800.RatioVictories;

            for (var i = 0; i < rows.Length; i++)
            {
                if (rows[i].Rate <= 0f)
                    continue;

                var eco = rows[i].T1000;
                if (eco.BankruptCount > baseBankrupt)
                    continue;
                if (eco.TotalDebt > debtCap)
                    continue;

                var ratio = rows[i].T800.RatioVictories;
                // Préférer une hausse franche de décisivité ; à égalité, rate plus bas (conservateur).
                if (ratio > bestRatio + 0.005f ||
                    (System.Math.Abs(ratio - bestRatio) <= 0.005f && rows[i].Rate < rows[bestIdx].Rate))
                {
                    bestRatio = ratio;
                    bestIdx = i;
                }
            }

            return bestIdx;
        }

        static int IndexOfRate(RateRow[] rows, float rate)
        {
            for (var i = 0; i < rows.Length; i++)
            {
                if (System.Math.Abs(rows[i].Rate - rate) < 0.001f)
                    return i;
            }

            return -1;
        }

        static void WriteSweepLog(string path, RateRow[] rows, int baselineIdx, int kneeIdx)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== v1_014 SWEEP OccupationScoreRate seed={Seed} ===");
            sb.AppendLine(
                "WorldMetrics.Capture / FormatStandardLine uniquement (règle test_001).");
            sb.AppendLine(
                "Décisivité @800 ; santé économique + stabilité politique @1000.");
            sb.AppendLine(
                "tried: 0 (témoin), 0.5 (baseline v1_010), 0.8, 1.1, 1.4, 1.7, 2.0");
            sb.AppendLine(
                "Critère genou : max ratioV@800 parmi rates avec bankrupt≤baseline " +
                "et debt≤baseline×1.10.");
            sb.AppendLine();

            sb.AppendLine(
                "rate | ratioV@800 | V | WP | annexed | stuck | " +
                "debt@1000 | bankrupt | army | zombie | land | maxProv | nonCore | sat | pop");
            sb.AppendLine(new string('-', 120));

            for (var i = 0; i < rows.Length; i++)
            {
                var r = rows[i];
                var t8 = r.T800;
                var t1 = r.T1000;
                var marker = i == kneeIdx ? " ← GENOU" : (i == baselineIdx ? " (baseline)" : "");
                sb.AppendLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0,4:F1} | {1,6}% | {2,2} | {3,2} | {4,3} | {5,2} | " +
                    "{6,7} | {7,3} | {8,6} | {9,4} | {10,2} | {11,2} | {12}/{13} | {14} | {15}{16}",
                    r.Rate,
                    WorldMetrics.Fmt1(t8.RatioVictories * 100f),
                    t8.Victories,
                    t8.WhitePeaces,
                    t8.AnnexedProvinces,
                    t8.StuckWars,
                    WorldMetrics.Fmt1(t1.TotalDebt),
                    t1.BankruptCount,
                    WorldMetrics.Fmt0(t1.WorldArmyStr),
                    WorldMetrics.Fmt0(t1.ZombieArmyStrLandless),
                    t1.CountriesWithLand,
                    t1.MaxProvincesOneCountry,
                    t1.NonCoreProvinces,
                    t1.TotalProvincesOwned,
                    WorldMetrics.Fmt3(t1.NeedsSatAvg),
                    t1.Population,
                    marker));
            }

            sb.AppendLine();
            sb.AppendLine("=== DÉTAIL FormatStandardLine (par rate) ===");
            for (var i = 0; i < rows.Length; i++)
            {
                var r = rows[i];
                sb.AppendLine($"--- rate={r.Rate.ToString("F1", CultureInfo.InvariantCulture)} ---");
                sb.AppendLine(WorldMetrics.FormatStandardLine(800, r.T800));
                sb.AppendLine(WorldMetrics.FormatStandardLine(1000, r.T1000));
                sb.AppendLine();
            }

            // Contrôle pop/sat stables
            sb.AppendLine("=== CONTRÔLE PÉRIMÈTRE (pop / needsSatAvg) ===");
            var basePop = rows[baselineIdx].T1000.Population;
            var baseSat = rows[baselineIdx].T1000.NeedsSatAvg;
            var popOk = true;
            for (var i = 0; i < rows.Length; i++)
            {
                var pop = rows[i].T1000.Population;
                var sat = rows[i].T1000.NeedsSatAvg;
                var popDelta = System.Math.Abs(pop - basePop);
                var satDelta = System.Math.Abs(sat - baseSat);
                var ok = popDelta <= 50 && satDelta <= 0.02f;
                if (!ok)
                    popOk = false;
                sb.AppendLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "rate={0:F1}: pop={1} (Δ{2}) sat={3} (Δ{4}) {5}",
                    rows[i].Rate, pop, popDelta,
                    WorldMetrics.Fmt3(sat), WorldMetrics.Fmt3(satDelta),
                    ok ? "OK" : "ALERTE débordement"));
            }

            if (!popOk)
            {
                sb.AppendLine(
                    "ALERTE: pop/sat ont bougé significativement — possible débordement de périmètre.");
            }
            else
            {
                sb.AppendLine("OK: pop/sat ~inchangés d'un rate à l'autre (levier isolé).");
            }

            var knee = rows[kneeIdx];
            var baseRow = rows[baselineIdx];
            sb.AppendLine();
            sb.AppendLine("=== VERDICT GENOU ===");
            sb.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "retenu rate={0:F1}: ratioV={1}% pour debt={2} / bankrupt={3} " +
                "(baseline 0.5: ratioV={4}% debt={5} bankrupt={6})",
                knee.Rate,
                WorldMetrics.Fmt1(knee.T800.RatioVictories * 100f),
                WorldMetrics.Fmt1(knee.T1000.TotalDebt),
                knee.T1000.BankruptCount,
                WorldMetrics.Fmt1(baseRow.T800.RatioVictories * 100f),
                WorldMetrics.Fmt1(baseRow.T1000.TotalDebt),
                baseRow.T1000.BankruptCount));

            // Compromis explicite : chaque rate > genou vs baseline
            sb.AppendLine("compromis (rates > genou vs critère debt≤baseline×1.10 / bankrupt≤baseline):");
            for (var i = 0; i < rows.Length; i++)
            {
                if (rows[i].Rate <= knee.Rate)
                    continue;
                var eco = rows[i].T1000;
                var debtOver = eco.TotalDebt > rows[baselineIdx].T1000.TotalDebt * 1.10f;
                var bankOver = eco.BankruptCount > rows[baselineIdx].T1000.BankruptCount;
                var reason = bankOver
                    ? $"+{eco.BankruptCount - rows[baselineIdx].T1000.BankruptCount} bankrupt"
                    : (debtOver
                        ? $"debt {WorldMetrics.Fmt1(eco.TotalDebt)} (>+10% vs {WorldMetrics.Fmt1(rows[baselineIdx].T1000.TotalDebt)})"
                        : "éligible");
                sb.AppendLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "  rate={0:F1}: ratioV={1}% (+{2:F1}pp) debt={3} bankrupt={4} → {5}",
                    rows[i].Rate,
                    WorldMetrics.Fmt1(rows[i].T800.RatioVictories * 100f),
                    (rows[i].T800.RatioVictories - knee.T800.RatioVictories) * 100f,
                    WorldMetrics.Fmt1(eco.TotalDebt),
                    eco.BankruptCount,
                    bankOver || debtOver ? "NON RENTABLE (" + reason + ")" : "OK"));
            }

            if (System.Math.Abs(knee.Rate - 0.5f) < 0.001f)
            {
                sb.AppendLine(
                    "CONCLUSION: garder 0.5 — 58.7% est le meilleur compromis sur le monde corrigé ; " +
                    "toute hausse de rate casse la dette (ou les banqueroutes) hors baseline.");
            }
            else
            {
                sb.AppendLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "CONCLUSION: retenir rate={0:F1} comme nouveau DefaultOccupationScoreRate.",
                    knee.Rate));
            }

            File.WriteAllText(path, sb.ToString());
            UnityEngine.Debug.Log(sb.ToString());
        }

        static void WriteMeasurementsLog(string path, RateRow knee)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== v1_014 ANCRAGES seed={Seed} ===");
            sb.AppendLine(
                $"DefaultOccupationScoreRate={OccupationScoreSystem.DefaultOccupationScoreRate.ToString("F1", CultureInfo.InvariantCulture)} " +
                $"(genou retenu={knee.Rate.ToString("F1", CultureInfo.InvariantCulture)})");
            sb.AppendLine(
                "WorldMetrics.Capture / FormatStandardLine (règle test_001).");
            sb.AppendLine(
                "Anciens (v1_010 @ rate=0.5): nonCore=8/50, land=17, maxProv=8, " +
                "debt=450.4, bankrupt=3, army=44804, zombie=0, sat=0.698, pop=142551 ; " +
                "ratioV@800=58.7%, stuck=0, annexed=7.");
            sb.AppendLine();

            OccupationScoreSystem.OccupationScoreRate = knee.Rate;
            WorldMetrics.Snapshot t800;
            WorldMetrics.Snapshot t1000;
            using (var harness = new SimulationHarness(Seed))
            {
                harness.RunTicks(0);
                sb.AppendLine(WorldMetrics.FormatStandardLine(0, WorldMetrics.Capture(harness.EntityManager, 0)));
                harness.RunTicks(200);
                sb.AppendLine(WorldMetrics.FormatStandardLine(200, WorldMetrics.Capture(harness.EntityManager, 200)));
                harness.RunTicks(300);
                sb.AppendLine(WorldMetrics.FormatStandardLine(500, WorldMetrics.Capture(harness.EntityManager, 500)));
                harness.RunTicks(300);
                t800 = WorldMetrics.Capture(harness.EntityManager, 800);
                sb.AppendLine(WorldMetrics.FormatStandardLine(800, t800));
                harness.RunTicks(200);
                t1000 = WorldMetrics.Capture(harness.EntityManager, 1000);
                sb.AppendLine(WorldMetrics.FormatStandardLine(1000, t1000));
            }

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
            sb.AppendLine("=== CONTRÔLE DE PLAUSIBILITÉ ===");
            var plausible = true;
            if (t1000.CountriesWithLand < 5)
            {
                plausible = false;
                sb.AppendLine($"FAIL countriesWithLand={t1000.CountriesWithLand} — effondrement.");
            }
            else
                sb.AppendLine($"OK countriesWithLand={t1000.CountriesWithLand} (>=5).");

            if (t1000.TotalDebt > 5000f)
            {
                plausible = false;
                sb.AppendLine($"FAIL totalDebt={WorldMetrics.Fmt1(t1000.TotalDebt)} — explosion.");
            }
            else
                sb.AppendLine($"OK totalDebt={WorldMetrics.Fmt1(t1000.TotalDebt)}.");

            if (t1000.ZombieArmyStrLandless > 0.5f)
            {
                plausible = false;
                sb.AppendLine($"FAIL zombie={WorldMetrics.Fmt0(t1000.ZombieArmyStrLandless)}.");
            }
            else
                sb.AppendLine("OK zombie=0.");

            if (t800.StuckWars != 0)
            {
                plausible = false;
                sb.AppendLine($"FAIL stuck@800={t800.StuckWars}.");
            }
            else
                sb.AppendLine("OK stuck@800=0.");

            var ratioVal = t800.RatioVictories * 100f;
            if (ratioVal < 20f || ratioVal > 95f)
            {
                plausible = false;
                sb.AppendLine(
                    $"FAIL ratioV@800={WorldMetrics.Fmt1(ratioVal)}% — hors ordre de grandeur.");
            }
            else
                sb.AppendLine(
                    $"OK ratioV@800={WorldMetrics.Fmt1(ratioVal)}% (ordre de grandeur plausible).");

            sb.AppendLine();
            sb.AppendLine(plausible
                ? "VERDICT PLAUSIBILITÉ: OUI — monde vivant, pas d'effondrement."
                : "VERDICT PLAUSIBILITÉ: NON — métrique hors bornes.");

            File.WriteAllText(path, sb.ToString());
            UnityEngine.Debug.Log(sb.ToString());

            Assert.IsTrue(plausible, "Contrôle de plausibilité échoué — voir v1_014_measurements.log");
        }

        static void Compare(StringBuilder sb, string name, string oldVal, string newVal)
        {
            var same = oldVal == newVal;
            sb.AppendLine(
                $"{name}: ancien={oldVal} nouveau={newVal} {(same ? "INCHANGÉ" : "DÉPLACÉ")}");
        }
    }
}
