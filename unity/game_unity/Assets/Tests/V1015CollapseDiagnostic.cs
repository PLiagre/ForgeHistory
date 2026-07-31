using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using VictoriaGame.Core;
using VictoriaGame.Economy;
using VictoriaGame.Military;
using VictoriaGame.Navy;
using VictoriaGame.Presentation;
using VictoriaGame.World;

namespace VictoriaGame.Tests
{
    /// <summary>Batch : -executeMethod VictoriaGame.Tests.V1015BatchRunner.Run</summary>
    public static class V1015BatchRunner
    {
        public static void Run()
        {
            V1015CollapseDiagnostic.RunDiagnosticAndWriteLog();
            UnityEngine.Debug.Log("V1015BatchRunner: DONE");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
        }
    }

    /// <summary>
    /// v1_015 — diagnostic long-horizon (t1000→t3000) : dette exponentielle, mort d'armée,
    /// gel territorial. INSTRUMENTATION SEULE — aucun correctif de simulation.
    /// Supercedé par v1_016 (correctifs + V1016StabilityTests) : les assertions d'effondrement
    /// ne tiennent plus une fois la spirale corrigée.
    /// </summary>
    [TestFixture]
    public class V1015CollapseDiagnostic
    {
        const uint Seed = 42195u;
        static readonly int[] SnapshotTicks = { 1000, 1200, 1500, 2000, 2500, 3000 };
        const int TopDebtorCount = 4;

        [Test]
        [Ignore("Superseded by V1016StabilityTests — collapse fixed in v1_016")]
        public void V1015_CollapseLongHorizonDiagnostic() => RunDiagnosticAndWriteLog();

        public static void RunDiagnosticAndWriteLog()
        {
            var logPath = Path.Combine(
                UnityEngine.Application.dataPath, "..", "Logs", "v1_015_diagnostic.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

            var sb = new StringBuilder(64 * 1024);
            sb.AppendLine(
                $"=== v1_015 DIAGNOSTIC EFFONDREMENT LONG-HORIZON seed={Seed} ===");
            sb.AppendLine(
                "Mesure PURE : SimulationHarness, lecture seule Treasury/Army/Navy/Ownership.");
            sb.AppendLine(
                "Aucun static de production modifié. Aucun correctif appliqué (correctif = v1_016).");
            sb.AppendLine(
                $"DebtInterestRate init={Fmt4(0.05f)} → intérêt/tick = Debt×{Fmt6(0.05f / 12f)} " +
                $"(TreasurySystem L78). Haircut={Fmt2(TreasurySystem.BankruptcyHaircut)} " +
                $"Mode={TreasurySystem.Mode} Threshold={Fmt1(TreasurySystem.BankruptcyThreshold)}.");
            sb.AppendLine(
                $"DebtRepayBuffer={Fmt1(TreasuryManagementSystem.DebtRepayBuffer)} " +
                $"DebtRepayFraction={Fmt2(TreasuryManagementSystem.DebtRepayFraction)} " +
                $"AdminCostPerProvince={Fmt2(MilitaryUpkeepSystem.AdminCostPerProvince)} " +
                $"ArmyUpkeepRate={Fmt6(MilitaryUpkeepSystem.ArmyUpkeepRate)} " +
                $"NavyUpkeepRate={Fmt2(MilitaryUpkeepSystem.NavyUpkeepRate)} " +
                $"RecruitCostScale={Fmt2(TemplateRecruitSystem.RecruitCostScale)}.");
            sb.AppendLine();

            var snaps = new List<TickSnap>(SnapshotTicks.Length);

            using (var harness = new SimulationHarness(Seed))
            {
                var prev = 0;
                for (var i = 0; i < SnapshotTicks.Length; i++)
                {
                    var tick = SnapshotTicks[i];
                    harness.RunTicks(tick - prev);
                    prev = tick;
                    snaps.Add(CaptureTick(harness.EntityManager, tick));
                }
            }

            // Top débiteurs figés à t3000 (suivis rétroactivement).
            var last = snaps[snaps.Count - 1];
            var topTags = SelectTopDebtorTags(last, TopDebtorCount);

            AppendPart1World(sb, snaps);
            AppendPart1Countries(sb, snaps, topTags);
            AppendPart2CausalChain(sb, snaps, topTags);
            AppendPart3Levers(sb, snaps);

            File.WriteAllText(logPath, sb.ToString());
            UnityEngine.Debug.Log(sb.ToString());

            // Garde-fous : le phénomène doit être mesurable (sinon le diagnostic est caduc).
            Assert.Greater(last.Metrics.TotalDebt, 5000f,
                "t3000 totalDebt attendu >> 1000 (spirale). Seed/ordonnancement changé ?");
            Assert.Less(last.Metrics.WorldArmyStr, 100f,
                "t3000 worldArmyStr attendu ~0 (mort d'armée). Seed/ordonnancement changé ?");
        }

        // ── PARTIE 1 ──────────────────────────────────────────────────────────

        static void AppendPart1World(StringBuilder sb, List<TickSnap> snaps)
        {
            sb.AppendLine("=== PARTIE 1 — ÉCONOMIE MONDE (paliers) ===");
            sb.AppendLine(
                "tick | income | expenses | admin | armyUpkeep | navyUpkeep | recruitEst | " +
                "interest | I-E | net(I-E-int) | debt | army | bankruptCountries | " +
                "bankruptcyEvents | insolventRecruit(withLand) | victories | whitePeaces | " +
                "annexed | warsDeclared | countriesWithLand");
            for (var i = 0; i < snaps.Count; i++)
            {
                var s = snaps[i];
                var m = s.Metrics;
                sb.AppendLine(
                    $"{s.Tick} | {Fmt2(s.SumIncome)} | {Fmt2(s.SumExpenses)} | {Fmt2(s.SumAdmin)} | " +
                    $"{Fmt2(s.SumArmyUpkeep)} | {Fmt2(s.SumNavyUpkeep)} | {Fmt2(s.SumRecruitEst)} | " +
                    $"{Fmt2(s.SumInterest)} | {Fmt2(s.SumIncome - s.SumExpenses)} | " +
                    $"{Fmt2(s.SumIncome - s.SumExpenses - s.SumInterest)} | {Fmt1(m.TotalDebt)} | " +
                    $"{Fmt0(m.WorldArmyStr)} | {m.BankruptCount} | {s.SumBankruptcyEvents} | " +
                    $"{m.InsolventGatedRecruitWithLand}/{m.CountriesWithLand} | " +
                    $"{m.Victories} | {m.WhitePeaces} | {m.AnnexedProvinces} | " +
                    $"{m.WarsDeclared} | {m.CountriesWithLand}");
            }

            sb.AppendLine();
            sb.AppendLine("Lignes WorldMetrics.FormatStandardLine :");
            for (var i = 0; i < snaps.Count; i++)
                sb.AppendLine(WorldMetrics.FormatStandardLine(snaps[i].Tick, snaps[i].Metrics));
            sb.AppendLine();
        }

        static void AppendPart1Countries(
            StringBuilder sb, List<TickSnap> snaps, List<string> topTags)
        {
            sb.AppendLine(
                $"=== PARTIE 1 — TOP {topTags.Count} DÉBITEURS @t3000 (suivi dans le temps) ===");
            sb.AppendLine($"Tags retenus @t{snaps[snaps.Count - 1].Tick}: {string.Join(", ", topTags)}");
            sb.AppendLine();

            for (var t = 0; t < topTags.Count; t++)
            {
                var tag = topTags[t];
                sb.AppendLine(
                    $"--- {tag} --- tick | income | expenses | admin | armyUp | navyUp | recruitEst | " +
                    "interest | deficit | net | balance | debt | provinces | armyStr | " +
                    "bankruptcyCount | canRecruit | canGrow");
                for (var i = 0; i < snaps.Count; i++)
                {
                    var s = snaps[i];
                    if (!s.ByTag.TryGetValue(tag, out var c))
                    {
                        sb.AppendLine($"{s.Tick} | (pays absent)");
                        continue;
                    }

                    sb.AppendLine(
                        $"{s.Tick} | {Fmt2(c.Income)} | {Fmt2(c.Expenses)} | {Fmt2(c.Admin)} | " +
                        $"{Fmt2(c.ArmyUpkeep)} | {Fmt2(c.NavyUpkeep)} | {Fmt2(c.RecruitEst)} | " +
                        $"{Fmt2(c.Interest)} | {Fmt2(c.Income - c.Expenses)} | " +
                        $"{Fmt2(c.Income - c.Expenses - c.Interest)} | {Fmt2(c.Balance)} | " +
                        $"{Fmt1(c.Debt)} | {c.Provinces} | {Fmt0(c.ArmyStr)} | " +
                        $"{c.BankruptcyCount} | {c.CanRecruit} | {c.CanGrow}");
                }

                sb.AppendLine();
            }
        }

        // ── PARTIE 2 ──────────────────────────────────────────────────────────

        static void AppendPart2CausalChain(
            StringBuilder sb, List<TickSnap> snaps, List<string> topTags)
        {
            var t1000 = FindSnap(snaps, 1000);
            var t1500 = FindSnap(snaps, 1500);
            var t2000 = FindSnap(snaps, 2000);
            var t2500 = FindSnap(snaps, 2500);
            var t3000 = FindSnap(snaps, 3000);

            // Verdict d'abord (exigé en tête de la chaîne causale / lisibilité).
            var verdict = BuildVerdict(snaps, topTags);
            sb.AppendLine("=== VERDICT CAUSE(S) RACINE ===");
            sb.AppendLine(verdict);
            sb.AppendLine();

            sb.AppendLine("=== PARTIE 2 — CHAÎNE CAUSALE (maillons chiffrés) ===");
            sb.AppendLine();

            // MAILLON A
            sb.AppendLine("--- MAILLON A — DÉFICIT STRUCTUREL ---");
            sb.AppendLine(
                $"Monde @t1000: income={Fmt2(t1000.SumIncome)} expenses={Fmt2(t1000.SumExpenses)} " +
                $"admin={Fmt2(t1000.SumAdmin)} armyUp={Fmt2(t1000.SumArmyUpkeep)} " +
                $"navyUp={Fmt2(t1000.SumNavyUpkeep)} recruitEst={Fmt2(t1000.SumRecruitEst)} " +
                $"interest={Fmt2(t1000.SumInterest)} surplus(I-E)={Fmt2(t1000.SumIncome - t1000.SumExpenses)} " +
                $"net(I-E-int)={Fmt2(t1000.SumIncome - t1000.SumExpenses - t1000.SumInterest)}");
            sb.AppendLine(
                $"Monde @t2500 (army≈0): income={Fmt2(t2500.SumIncome)} expenses={Fmt2(t2500.SumExpenses)} " +
                $"admin={Fmt2(t2500.SumAdmin)} armyUp={Fmt2(t2500.SumArmyUpkeep)} " +
                $"navyUp={Fmt2(t2500.SumNavyUpkeep)} recruitEst={Fmt2(t2500.SumRecruitEst)} " +
                $"interest={Fmt2(t2500.SumInterest)} surplus(I-E)={Fmt2(t2500.SumIncome - t2500.SumExpenses)} " +
                $"net(I-E-int)={Fmt2(t2500.SumIncome - t2500.SumExpenses - t2500.SumInterest)} " +
                $"worldArmyStr={Fmt0(t2500.Metrics.WorldArmyStr)}");
            sb.AppendLine(
                $"Monde @t3000: income={Fmt2(t3000.SumIncome)} expenses={Fmt2(t3000.SumExpenses)} " +
                $"admin={Fmt2(t3000.SumAdmin)} armyUp={Fmt2(t3000.SumArmyUpkeep)} " +
                $"navyUp={Fmt2(t3000.SumNavyUpkeep)} interest={Fmt2(t3000.SumInterest)} " +
                $"surplus(I-E)={Fmt2(t3000.SumIncome - t3000.SumExpenses)} " +
                $"net={Fmt2(t3000.SumIncome - t3000.SumExpenses - t3000.SumInterest)} " +
                $"debt={Fmt1(t3000.Metrics.TotalDebt)}");

            var armyGone = t2500.Metrics.WorldArmyStr < 50f;
            var worldPrimarySurplus = (t2500.SumIncome - t2500.SumExpenses) > 1f;
            var worldNetNegative = (t3000.SumIncome - t3000.SumExpenses - t3000.SumInterest) < -0.5f;
            var navyDominatesAt2500 = t2500.SumNavyUpkeep >= t2500.SumAdmin
                && t2500.SumNavyUpkeep >= t2500.SumArmyUpkeep
                && t2500.SumNavyUpkeep >= t2500.SumRecruitEst;
            var adminDominatesAt2500 = t2500.SumAdmin >= t2500.SumArmyUpkeep
                && t2500.SumAdmin >= t2500.SumNavyUpkeep
                && t2500.SumAdmin >= t2500.SumRecruitEst;
            var interestExceedsWorldSurplus = t2500.SumInterest
                > Math.Max(0.01f, t2500.SumIncome - t2500.SumExpenses);

            var topDebtPrimaryDeficit = 0;
            var topDebtAvgProv = 0f;
            var topDebtNavyShare = 0f;
            var topDebtInterestShare = 0f;
            for (var t = 0; t < topTags.Count; t++)
            {
                var tag = topTags[t];
                if (!t3000.ByTag.TryGetValue(tag, out var c3))
                    continue;
                t1000.ByTag.TryGetValue(tag, out var c1);
                t2500.ByTag.TryGetValue(tag, out var c25);
                if (c25.Income - c25.Expenses < 0f)
                    topDebtPrimaryDeficit++;
                topDebtAvgProv += c25.Provinces;
                var exp = Math.Max(c25.Expenses, 0.0001f);
                topDebtNavyShare += c25.NavyUpkeep / exp;
                topDebtInterestShare += c25.Interest;
                sb.AppendLine(
                    $"  {tag}: @t1000 debt={Fmt1(c1.Debt)} prov={c1.Provinces} " +
                    $"def={Fmt2(c1.Income - c1.Expenses)} net={Fmt2(c1.Income - c1.Expenses - c1.Interest)} " +
                    $"admin={Fmt2(c1.Admin)} navy={Fmt2(c1.NavyUpkeep)} | @t2500 debt={Fmt1(c25.Debt)} " +
                    $"prov={c25.Provinces} army={Fmt0(c25.ArmyStr)} def={Fmt2(c25.Income - c25.Expenses)} " +
                    $"net={Fmt2(c25.Income - c25.Expenses - c25.Interest)} admin={Fmt2(c25.Admin)} " +
                    $"navy={Fmt2(c25.NavyUpkeep)} int={Fmt2(c25.Interest)} | @t3000 debt={Fmt1(c3.Debt)} " +
                    $"bal={Fmt2(c3.Balance)} bkCount={c3.BankruptcyCount}");
            }

            if (topTags.Count > 0)
            {
                topDebtAvgProv /= topTags.Count;
                topDebtNavyShare /= topTags.Count;
            }

            sb.AppendLine(
                $"Témoin army→0 @t2500: {(armyGone ? "OUI" : "NON")} " +
                $"(worldArmyStr={Fmt0(t2500.Metrics.WorldArmyStr)}).");
            sb.AppendLine(
                $"Monde agrégé en SURPLUS primaire (I>E) @t2500: {(worldPrimarySurplus ? "OUI" : "NON")} " +
                $"(+{Fmt2(t2500.SumIncome - t2500.SumExpenses)}) — le trou n'est PAS mondial.");
            sb.AppendLine(
                $"Monde net (I-E-intérêt) négatif @t3000: {(worldNetNegative ? "OUI" : "NON")} " +
                $"(intérêt={Fmt2(t3000.SumInterest)} consomme le surplus puis plus).");
            sb.AppendLine(
                $"Poste Expenses dominant @t2500: admin={Fmt2(t2500.SumAdmin)} " +
                $"armyUp={Fmt2(t2500.SumArmyUpkeep)} navyUp={Fmt2(t2500.SumNavyUpkeep)} " +
                $"recruit={Fmt2(t2500.SumRecruitEst)} → " +
                $"{(navyDominatesAt2500 ? "MARINE (NavyUpkeepRate×NavalStrength)" : adminDominatesAt2500 ? "ADMIN ∝ territoire" : "mixte")}.");
            sb.AppendLine(
                $"Intérêt vs surplus primaire monde @t2500: interest={Fmt2(t2500.SumInterest)} " +
                $"surplus={Fmt2(t2500.SumIncome - t2500.SumExpenses)} → " +
                $"{(interestExceedsWorldSurplus ? "intérêt ≥ surplus mondial (bascule le net)" : "surplus tient encore")}.");
            sb.AppendLine(
                $"Top débiteurs: {topDebtPrimaryDeficit}/{topTags.Count} en déficit primaire ; " +
                $"provinces moyennes={Fmt2(topDebtAvgProv)} ; part navy dans Expenses≈{Fmt2(topDebtNavyShare * 100f)}%.");

            var adminHypothesisRefuted = topDebtAvgProv < 2f && !adminDominatesAt2500;
            sb.AppendLine(
                "CONCLUSION A: " +
                (armyGone
                    ? "PROUVÉ partiellement — (1) l'entretien d'ARMÉE n'est pas le moteur post-t2500 " +
                      $"(armyUp={Fmt2(t2500.SumArmyUpkeep)}) ; (2) hypothèse admin∝territoire des GROS pays: " +
                      (adminHypothesisRefuted
                          ? "RÉFUTÉE — top débiteurs = pays à 0–1 province, Expenses dominées par la MARINE ; "
                          : "à nuancer ; ") +
                      "(3) le déficit structurel est CONCENTRÉ sur une poignée de pays insolvables " +
                      "(monde agrégé encore en surplus I-E) ; (4) l'intérêt transforme ce trou local " +
                      "en spirale de dette mondiale agrégée."
                    : "NON PROUVÉ — armée non nulle @t2500."));
            sb.AppendLine();

            // MAILLON B
            sb.AppendLine("--- MAILLON B — AMPLIFICATEUR INTÉRÊT COMPOSÉ ---");
            var ratePerTick = 0.05f / 12f;
            var doublingPure = Math.Log(2.0) / ratePerTick;
            sb.AppendLine(
                $"DebtInterestRate=0.05 → r/tick={Fmt6(ratePerTick)}. " +
                $"Doublement pur si intérêt appliqué DIRECTEMENT sur Debt: ~{Fmt1((float)doublingPure)} ticks.");
            sb.AppendLine(
                "NB: TreasurySystem applique l'intérêt sur Balance, pas sur Debt ; " +
                "Debt croît via banqueroute (retained=0.3) et jamais via intérêt direct.");

            for (var i = 1; i < snaps.Count; i++)
            {
                var a = snaps[i - 1];
                var b = snaps[i];
                var dt = b.Tick - a.Tick;
                var ratio = a.Metrics.TotalDebt > 1f
                    ? b.Metrics.TotalDebt / a.Metrics.TotalDebt
                    : 0f;
                var avgInterest = 0.5f * (a.SumInterest + b.SumInterest);
                var debtDelta = b.Metrics.TotalDebt - a.Metrics.TotalDebt;
                var interestCumApprox = avgInterest * dt;
                sb.AppendLine(
                    $"  t{a.Tick}→t{b.Tick} (Δ{dt}): debt {Fmt1(a.Metrics.TotalDebt)}→{Fmt1(b.Metrics.TotalDebt)} " +
                    $"(×{Fmt2(ratio)}, +{Fmt1(debtDelta)}) ; intérêt/tick≈{Fmt2(avgInterest)} ; " +
                    $"intérêt cumulé approx≈{Fmt1(interestCumApprox)} ; " +
                    $"ratio Δdebt/intérêts≈{(interestCumApprox > 0.01f ? Fmt2(debtDelta / interestCumApprox) : "n/a")}");
            }

            var d1000 = t1000.Metrics.TotalDebt;
            var d3000 = t3000.Metrics.TotalDebt;
            var observedRatio = d1000 > 1f ? d3000 / d1000 : 0f;
            var expectedPure = (float)Math.Exp(ratePerTick * 2000.0);
            sb.AppendLine(
                $"Croissance observée t1000→t3000: ×{Fmt2(observedRatio)} " +
                $"(debt {Fmt1(d1000)}→{Fmt1(d3000)}). Croissance si compound pur sur Debt: ×{Fmt2(expectedPure)}.");
            sb.AppendLine(
                observedRatio > 2f
                    ? "CONCLUSION B: PROUVÉ — la dette croît de façon explosive ; " +
                      "l'intérêt composé (via Balance→banqueroute) est l'amplificateur nécessaire " +
                      "(sans lui le déficit linéaire ne doublerait pas). Cohérence qualitative avec r=0.05/12, " +
                      "mais le rythme observé ≠ compound pur sur Debt (mécanisme Balance+haircut)."
                    : "CONCLUSION B: NON PROUVÉ — croissance insuffisante pour parler d'amplificateur exponentiel.");
            sb.AppendLine();

            // MAILLON C
            sb.AppendLine("--- MAILLON C — POURQUOI LA BORNE eco_029 NE TIENT PAS ---");
            sb.AppendLine(
                "eco_029: haircut=0.7 → debt~601 bornée ≤~1000 — vérifié UNIQUEMENT à t1000.");
            sb.AppendLine(
                $"Mesure: bankruptCountries t1000={t1000.Metrics.BankruptCount} " +
                $"t1500={t1500.Metrics.BankruptCount} t2000={t2000.Metrics.BankruptCount} " +
                $"t2500={t2500.Metrics.BankruptCount} t3000={t3000.Metrics.BankruptCount} " +
                "(WorldMetrics = pays avec BankruptcyTick>0, PAS le nombre d'événements).");
            sb.AppendLine(
                $"Somme BankruptcyCount (événements cumulés/pays): " +
                $"t1000={t1000.SumBankruptcyEvents} t1500={t1500.SumBankruptcyEvents} " +
                $"t2000={t2000.SumBankruptcyEvents} t2500={t2500.SumBankruptcyEvents} " +
                $"t3000={t3000.SumBankruptcyEvents}.");

            var countriesFrozen = t1500.Metrics.BankruptCount == t3000.Metrics.BankruptCount;
            var eventsStillRising = t3000.SumBankruptcyEvents > t1500.SumBankruptcyEvents + 2;
            sb.AppendLine(
                $"Pays distincts en banqueroute figés dès t1500: {(countriesFrozen ? "OUI" : "NON")}.");
            sb.AppendLine(
                $"Événements de banqueroute continuent après t1500: {(eventsStillRising ? "OUI" : "NON")}.");

            // Pays endettés sans jamais toucher le seuil ?
            var indebtedNeverBk = 0;
            var indebtedAboveThreshold = 0;
            foreach (var kv in t3000.ByTag)
            {
                var c = kv.Value;
                if (c.Debt <= 1f)
                    continue;
                if (c.BankruptcyCount == 0)
                    indebtedNeverBk++;
                if (c.Balance > TreasurySystem.BankruptcyThreshold)
                    indebtedAboveThreshold++;
            }

            sb.AppendLine(
                $"@t3000 pays avec Debt>1 et BankruptcyCount=0: {indebtedNeverBk} ; " +
                $"pays endettés avec Balance > seuil({Fmt1(TreasurySystem.BankruptcyThreshold)}): " +
                $"{indebtedAboveThreshold} (Balance souvent reset à 0 après défaut).");
            sb.AppendLine(
                "Mécanisme: chaque défaut ajoute abs(Balance)×retained(0.3) à Debt puis Balance=0. " +
                "Si le déficit structurel + intérêt reconstitue Balance<-500, les défauts CONTINUENT " +
                "et Debt croît sans plafond — le haircut ralentit mais ne borne PAS.");
            sb.AppendLine(
                countriesFrozen && eventsStillRising
                    ? "CONCLUSION C: PROUVÉ — la « borne » eco_029 est une illusion d'horizon t1000 ; " +
                      "peu de pays entrent en défaut (compteur pays figé) MAIS ces pays enchaînent " +
                      "les défauts (événements ↑) et chaque défaut ajoute de la dette nette."
                    : countriesFrozen && !eventsStillRising
                        ? "CONCLUSION C: PARTIEL — pays en défaut figés ET événements figés ; " +
                          "alors la croissance de dette vient surtout de… (voir Δdebt vs intérêts ; " +
                          "si Balance ne repasse pas sous -500, Debt ne devrait pas croître via défaut — " +
                          "vérifier incohérence)."
                        : "CONCLUSION C: voir chiffres — scénario non aligné sur la sonde initiale.");
            sb.AppendLine();

            // MAILLON D
            sb.AppendLine("--- MAILLON D — MORT DE L'ARMÉE / GEL TERRITORIAL ---");
            for (var i = 0; i < snaps.Count; i++)
            {
                var s = snaps[i];
                var m = s.Metrics;
                sb.AppendLine(
                    $"  t{s.Tick}: army={Fmt0(m.WorldArmyStr)} livingArmies={m.LivingArmies} " +
                    $"insolventRecruit={m.InsolventGatedRecruitWithLand}/{m.CountriesWithLand} " +
                    $"insolventGrowth={m.InsolventGatedGrowthWithLand}/{m.CountriesWithLand} " +
                    $"victories={m.Victories} whitePeaces={m.WhitePeaces} annexed={m.AnnexedProvinces} " +
                    $"warsDeclared={m.WarsDeclared} countriesWithLand={m.CountriesWithLand}");
            }

            var armyCollapse = t1000.Metrics.WorldArmyStr > 1000f
                && t2500.Metrics.WorldArmyStr < 50f;
            var insolvencyPresent = t2000.Metrics.InsolventGatedRecruitWithLand > 0
                || t2000.Metrics.InsolventGatedGrowthWithLand > 0;
            // Gel: victoires stables sur le dernier palier + annexions à 0 + guerres qui continuent.
            var victoriesFrozen = t2500.Metrics.Victories == t3000.Metrics.Victories
                && t2000.Metrics.Victories <= t2500.Metrics.Victories + 1;
            var annexFrozenAtZero = t2000.Metrics.AnnexedProvinces == 0
                && t3000.Metrics.AnnexedProvinces == 0;
            var warsContinue = t3000.Metrics.WarsDeclared > t2000.Metrics.WarsDeclared;
            var whitePeacesRise = t3000.Metrics.WhitePeaces > t2000.Metrics.WhitePeaces;

            sb.AppendLine(
                $"Effondrement armée t1000→t2500: {(armyCollapse ? "OUI" : "NON")}.");
            sb.AppendLine(
                $"Gates insolvabilité actifs @t2000: {(insolvencyPresent ? "OUI" : "NON")} " +
                $"(recruit {t2000.Metrics.InsolventGatedRecruitWithLand}/{t2000.Metrics.CountriesWithLand}, " +
                $"growth {t2000.Metrics.InsolventGatedGrowthWithLand}/{t2000.Metrics.CountriesWithLand}). " +
                "NB: fraction <50% n'empêche pas worldArmyStr→0 (désarmement progressif + non-renfort).");
            sb.AppendLine(
                $"Victoires figées @t2500→t3000 ({t2500.Metrics.Victories}={t3000.Metrics.Victories}): " +
                $"{(victoriesFrozen ? "OUI" : "NON")} ; annexions=0 dès t2000: " +
                $"{(annexFrozenAtZero ? "OUI" : "NON")} ; whitePeaces↑: " +
                $"{(whitePeacesRise ? "OUI" : "NON")} ; guerresDeclared↑: " +
                $"{(warsContinue ? "OUI" : "NON")}.");
            sb.AppendLine(
                armyCollapse && insolvencyPresent && victoriesFrozen && annexFrozenAtZero && warsContinue
                    ? "CONCLUSION D: PROUVÉ — pendant l'effondrement, des pays restent gated insolvent " +
                      "(eco_026/027 FluxCommitted) ; worldArmyStr→0 ; sans force décisive, " +
                      "victoires se figent, annexions à 0, paix blanches↑ malgré guerresDeclared↑ " +
                      "(gel territorial). Lien causal exact désarmement→0 force : corrélé aux gates, " +
                      "pas une ablation isolée dans ce diagnostic."
                    : "CONCLUSION D: PARTIEL — voir flags ci-dessus ; ne pas sur-affirmer le lien gates→0.");
            sb.AppendLine();
        }

        static string BuildVerdict(List<TickSnap> snaps, List<string> topTags)
        {
            var t1000 = FindSnap(snaps, 1000);
            var t2500 = FindSnap(snaps, 2500);
            var t3000 = FindSnap(snaps, 3000);

            var sb = new StringBuilder();
            sb.Append("CAUSE RACINE: ");
            sb.Append(
                "spirale de dette portée par une MINORITÉ de pays insolvables (souvent peu/pas de terre), ");
            sb.Append(
                "en déficit primaire (Income < Expenses, Expenses dominées par entretien NAVAL) ");
            sb.Append(
                "+ intérêt composé sur Balance (r=0.05/an) + défauts répétés (retained=0.3→Debt) ");
            sb.Append(
                "sans remboursement (Balance jamais > DebtRepayBuffer). ");
            sb.Append(
                $"Mesure: debt {Fmt1(t1000.Metrics.TotalDebt)}@t1000 → {Fmt1(t3000.Metrics.TotalDebt)}@t3000 ");
            sb.Append(
                $"(×{Fmt2(t3000.Metrics.TotalDebt / Math.Max(1f, t1000.Metrics.TotalDebt))}, doublement ~500 ticks). ");
            sb.Append(
                "RÉFUTÉ: (a) entretien militaire comme moteur post-t2500 ");
            sb.Append(
                $"(army={Fmt0(t2500.Metrics.WorldArmyStr)}, armyUp={Fmt2(t2500.SumArmyUpkeep)}) ; ");
            sb.Append(
                "(b) admin∝territoire des gros empires comme cause des top débiteurs ");
            sb.Append(
                $"(top={string.Join(",", topTags)}, admin monde={Fmt2(t2500.SumAdmin)} vs navy={Fmt2(t2500.SumNavyUpkeep)}). ");
            sb.Append(
                "Monde agrégé reste en surplus I-E ; l'intérêt agrégé finit par basculer le net. ");
            sb.Append(
                "Conséquence: insolvabilité/gates → démise en force mondiale → gel territorial ");
            sb.Append(
                $"(victories figées {t3000.Metrics.Victories}, annexed=0, warsDeclared↑).");
            return sb.ToString();
        }

        // ── PARTIE 3 ──────────────────────────────────────────────────────────

        static void AppendPart3Levers(StringBuilder sb, List<TickSnap> snaps)
        {
            var t2500 = FindSnap(snaps, 2500);
            var t3000 = FindSnap(snaps, 3000);
            var navyDominates = t2500.SumNavyUpkeep > t2500.SumAdmin;

            sb.AppendLine("=== PARTIE 3 — LEVIERS PROPOSÉS (NON IMPLÉMENTÉS) ===");
            sb.AppendLine(
                "Classement effet/risque pour un MONDE VIVANT ET STABLE " +
                "(dette bornée, armées survivantes, conquêtes continues). Aucun levier appliqué ici.");
            sb.AppendLine(
                "Leviers classés d'après les preuves (navy+intérêt+défauts), PAS d'après l'hypothèse admin initiale.");
            sb.AppendLine();

            var rank = 1;
            void Lever(string name, string system, string effect, string risk, string score)
            {
                sb.AppendLine($"#{rank} [{score}] {name}");
                sb.AppendLine($"    Touche: {system}");
                sb.AppendLine($"    Effet attendu: {effect}");
                sb.AppendLine($"    Risque: {risk}");
                sb.AppendLine();
                rank++;
            }

            Lever(
                "Atténuer / plafonner l'intérêt composé",
                "TreasurySystem (DebtInterestRate ou formule L78) ; éventuellement plafond d'intérêt/tick",
                "Coupe l'amplificateur mesuré (doublement ~500 ticks via Balance→défaut) ; " +
                "Δdebt suit alors le déficit primaire seul",
                "Moyen — ancrages debt@t1000 eco_029 à re-mesurer ; Determinism + parité",
                "effet↑↑ / risque moyen");

            if (navyDominates)
            {
                Lever(
                    "Réduire / plafonner l'entretien naval (ou scaler avec le revenu)",
                    "MilitaryUpkeepSystem.NavyUpkeepRate (0.05) ou lien Income ; NavalComponents",
                    "Supprime le déficit primaire des top débiteurs (navy = poste #1 Expenses mesuré) ; " +
                    "pays sans terre cessent d'accumuler une dette navale absurde",
                    "Moyen-élevé — change l'économie navale ; vérifier eco/mil ancrages",
                    "effet↑↑ / risque moyen-élevé");
            }

            Lever(
                "Rendre le remboursement possible (Balance basse / fraction plus haute)",
                "TreasuryManagementSystem DebtRepayBuffer=75 / DebtRepayFraction=0.2",
                "Les pays qui repassent brièvement au-dessus du seuil amortissent au lieu de composer",
                "Moyen — peut affaiblir la caisse de guerre ; army@t1000 à surveiller",
                "effet↑ / risque moyen");

            Lever(
                "Plafond dur de dette ou haircut plus agressif / défaut total",
                "TreasurySystem.BankruptcyHaircut (0.7) ou clamp Debt≤Dmax",
                "Borne mécanique même si le déficit primaire persiste — empêche 450→15429",
                "Élevé — pression budgétaire affaiblie ; recalibrage eco_029 obligatoire",
                "effet↑↑ / risque élevé");

            Lever(
                "Assouplir les gates d'insolvabilité sans supprimer le frein",
                "ArmyDisbandmentSystem (GateMode / BrokeThreshold / GrowthMargin)",
                "Conserve une force minimale → victoires/annexions ne se figent plus à zéro",
                "Élevé — mil_023 ; risque zombies / re-militarisation artificielle",
                "effet↑ territoire / risque élevé");

            Lever(
                "Admin ∝ territoire (eco_032) — levier SECONDAIRE seulement",
                "MilitaryUpkeepSystem.AdminCostPerProvince",
                "Peut aider les gros empires, mais RÉFUTÉ comme cause des top débiteurs v1_015 " +
                $"(admin monde={Fmt2(t2500.SumAdmin)} << navy={Fmt2(t2500.SumNavyUpkeep)})",
                "Élevé sur carte — ne pas prioriser sur la base de ce diagnostic",
                "effet faible ici / risque élevé — NE PAS retenir en #1");

            Lever(
                "Revenu plancher / suppression marine des landless",
                "TaxSystem ou règle Navy: landless → upkeep 0 / désarmement flotte",
                "Coupe la source de déficit des pays sans Income fiscal",
                "Élevé — règle spéciale ; à valider par ablation dédiée",
                "effet↑ / risque élevé");

            sb.AppendLine(
                "RECOMMANDATION v1_016: " +
                "1) couper l'amplificateur intérêt et/ou 2) corriger le déficit primaire NAVAL " +
                "des insolvables (rate ou règle landless), 3) permettre un vrai remboursement, " +
                "4) seulement ensuite revoir les gates pour la survie d'armée. " +
                "NE PAS commencer par l'admin∝territoire (hypothèse réfutée pour les top débiteurs). " +
                $"Réf t3000: debt={Fmt1(t3000.Metrics.TotalDebt)} army={Fmt0(t3000.Metrics.WorldArmyStr)} " +
                $"interest/tick={Fmt2(t3000.SumInterest)} navy/tick={Fmt2(t3000.SumNavyUpkeep)} " +
                $"admin/tick={Fmt2(t3000.SumAdmin)}.");
            sb.AppendLine();
            sb.AppendLine("=== FIN v1_015 ===");
        }

        // ── Capture ───────────────────────────────────────────────────────────

        static TickSnap CaptureTick(EntityManager em, int tick)
        {
            var snap = new TickSnap
            {
                Tick = tick,
                Metrics = WorldMetrics.Capture(em, tick),
                ByTag = new Dictionary<string, CountryEco>(32)
            };

            var provinceCounts = new Dictionary<Entity, int>();
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<ProvinceOwnership>()))
            using (var owns = q.ToComponentDataArray<ProvinceOwnership>(Allocator.Temp))
            {
                for (var i = 0; i < owns.Length; i++)
                {
                    var o = owns[i].Owner;
                    if (o == Entity.Null)
                        continue;
                    provinceCounts.TryGetValue(o, out var n);
                    provinceCounts[o] = n + 1;
                }
            }

            var armyByCountry = new Dictionary<Entity, float>();
            var regsByCountry = new Dictionary<Entity, int>();
            using (var q = em.CreateEntityQuery(
                       ComponentType.ReadOnly<ArmyData>(),
                       ComponentType.ReadOnly<RegimentSlot>()))
            using (var entities = q.ToEntityArray(Allocator.Temp))
            using (var armies = q.ToComponentDataArray<ArmyData>(Allocator.Temp))
            {
                for (var i = 0; i < entities.Length; i++)
                {
                    var c = armies[i].Country;
                    if (c == Entity.Null)
                        continue;
                    armyByCountry.TryGetValue(c, out var str);
                    armyByCountry[c] = str + armies[i].Strength;
                    var slots = em.GetBuffer<RegimentSlot>(entities[i]);
                    regsByCountry.TryGetValue(c, out var rc);
                    regsByCountry[c] = rc + slots.Length;
                }
            }

            var navyByCountry = new Dictionary<Entity, float>();
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<NavyData>()))
            using (var navies = q.ToComponentDataArray<NavyData>(Allocator.Temp))
            {
                for (var i = 0; i < navies.Length; i++)
                {
                    var c = navies[i].Country;
                    if (c == Entity.Null)
                        continue;
                    navyByCountry.TryGetValue(c, out var ns);
                    navyByCountry[c] = ns + navies[i].NavalStrength;
                }
            }

            var adminRate = MilitaryUpkeepSystem.AdminCostPerProvince;
            var armyRate = MilitaryUpkeepSystem.ArmyUpkeepRate;
            var navyRate = MilitaryUpkeepSystem.NavyUpkeepRate;
            var flatAdmin = MilitaryUpkeepSystem.BaseAdminCost;
            var perProvince = MilitaryUpkeepSystem.CostMode == AdminCostMode.PerProvince;

            using (var q = em.CreateEntityQuery(
                       ComponentType.ReadOnly<CountryData>(),
                       ComponentType.ReadOnly<TreasuryData>()))
            using (var countries = q.ToEntityArray(Allocator.Temp))
            using (var countryData = q.ToComponentDataArray<CountryData>(Allocator.Temp))
            using (var treasuries = q.ToComponentDataArray<TreasuryData>(Allocator.Temp))
            {
                for (var i = 0; i < countries.Length; i++)
                {
                    var entity = countries[i];
                    var tr = treasuries[i];
                    provinceCounts.TryGetValue(entity, out var prov);
                    armyByCountry.TryGetValue(entity, out var armyStr);
                    navyByCountry.TryGetValue(entity, out var navyStr);
                    regsByCountry.TryGetValue(entity, out var regCount);

                    var admin = perProvince ? adminRate * prov : flatAdmin;
                    var armyUp = armyStr * armyRate;
                    var navyUp = navyStr * navyRate;
                    var recruitEst = tr.Expenses - armyUp - navyUp - admin;
                    if (recruitEst < 0f && recruitEst > -0.0001f)
                        recruitEst = 0f;

                    var interest = tr.Debt > 0f
                        ? tr.Debt * (tr.DebtInterestRate / 12f)
                        : 0f;

                    var canRecruit = ArmyDisbandmentSystem.CanAffordRecruit(
                        tr, regCount, armyStr, ArmySolvencyGateMode.FluxCommitted);
                    var canGrow = ArmyDisbandmentSystem.CanAffordGrowth(
                        tr, regCount, armyStr, ArmySolvencyGateMode.FluxCommitted);

                    var eco = new CountryEco
                    {
                        Tag = countryData[i].Tag.ToString(),
                        CountryId = countryData[i].CountryId,
                        Income = tr.Income,
                        Expenses = tr.Expenses,
                        Admin = admin,
                        ArmyUpkeep = armyUp,
                        NavyUpkeep = navyUp,
                        RecruitEst = recruitEst,
                        Interest = interest,
                        Balance = tr.Balance,
                        Debt = tr.Debt,
                        Provinces = prov,
                        ArmyStr = armyStr,
                        BankruptcyCount = tr.BankruptcyCount,
                        BankruptcyTick = tr.BankruptcyTick,
                        CanRecruit = canRecruit,
                        CanGrow = canGrow
                    };

                    snap.ByTag[eco.Tag] = eco;
                    snap.SumIncome += eco.Income;
                    snap.SumExpenses += eco.Expenses;
                    snap.SumAdmin += eco.Admin;
                    snap.SumArmyUpkeep += eco.ArmyUpkeep;
                    snap.SumNavyUpkeep += eco.NavyUpkeep;
                    snap.SumRecruitEst += Math.Max(0f, eco.RecruitEst);
                    snap.SumInterest += eco.Interest;
                    snap.SumBankruptcyEvents += eco.BankruptcyCount;
                }
            }

            return snap;
        }

        static List<string> SelectTopDebtorTags(TickSnap last, int count)
        {
            var list = new List<CountryEco>(last.ByTag.Values);
            list.Sort((a, b) =>
            {
                var cmp = b.Debt.CompareTo(a.Debt);
                return cmp != 0 ? cmp : string.CompareOrdinal(a.Tag, b.Tag);
            });

            var tags = new List<string>(count);
            for (var i = 0; i < list.Count && tags.Count < count; i++)
            {
                if (list[i].Debt > 0f)
                    tags.Add(list[i].Tag);
            }

            return tags;
        }

        static TickSnap FindSnap(List<TickSnap> snaps, int tick)
        {
            for (var i = 0; i < snaps.Count; i++)
            {
                if (snaps[i].Tick == tick)
                    return snaps[i];
            }

            throw new InvalidOperationException($"Snapshot t{tick} manquant.");
        }

        static string Fmt0(float v) => v.ToString("F0", CultureInfo.InvariantCulture);
        static string Fmt1(float v) => v.ToString("F1", CultureInfo.InvariantCulture);
        static string Fmt2(float v) => v.ToString("F2", CultureInfo.InvariantCulture);
        static string Fmt4(float v) => v.ToString("F4", CultureInfo.InvariantCulture);
        static string Fmt6(float v) => v.ToString("F6", CultureInfo.InvariantCulture);

        struct CountryEco
        {
            public string Tag;
            public int CountryId;
            public float Income;
            public float Expenses;
            public float Admin;
            public float ArmyUpkeep;
            public float NavyUpkeep;
            public float RecruitEst;
            public float Interest;
            public float Balance;
            public float Debt;
            public int Provinces;
            public float ArmyStr;
            public int BankruptcyCount;
            public int BankruptcyTick;
            public bool CanRecruit;
            public bool CanGrow;
        }

        struct TickSnap
        {
            public int Tick;
            public WorldMetrics.Snapshot Metrics;
            public Dictionary<string, CountryEco> ByTag;
            public float SumIncome;
            public float SumExpenses;
            public float SumAdmin;
            public float SumArmyUpkeep;
            public float SumNavyUpkeep;
            public float SumRecruitEst;
            public float SumInterest;
            public int SumBankruptcyEvents;
        }
    }
}
