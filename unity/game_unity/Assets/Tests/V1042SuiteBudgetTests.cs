using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using NUnit.Framework;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace VictoriaGame.Tests
{
    /// <summary>
    /// Batch : -executeMethod VictoriaGame.Tests.V1042BatchRunner.Run
    /// Mesure dispersion par cas + budget, écrit Logs/v1_078_budget.log.
    /// </summary>
    public static class V1042BatchRunner
    {
        public static void Run()
        {
            V1042SuiteBudgetTests.RunMeasureAndWriteLog();
            Debug.Log("V1042BatchRunner: DONE");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
        }
    }

    /// <summary>
    /// v1_042 / v1_043 / v1_078 — budget de suite EditMode.
    /// v1_078 : remplace le budget TOTAL (245 s) par un budget PAR CAS (moyenne
    /// durée/cas). Le garde-fou structurel anti-balayage est intact.
    /// Outillage uniquement : aucune règle de simulation touchée.
    /// </summary>
    [TestFixture]
    public class V1042SuiteBudgetTests
    {
        /// <summary>
        /// RETRAITÉ v1_078. Ancien budget TOTAL v1_043 — ne plus l'asserter.
        /// Conservé comme référence de la faute (calage dans le bruit).
        /// </summary>
        public const double RetiredTotalBudgetSeconds = 245.0;

        /// <summary>
        /// Budget FIN : durée moyenne par cas (total / n).
        /// Calé AU-DESSUS de la dispersion mesurée (session + rejeux), pas dedans.
        /// Dérivation : max session 1,8928 + max(2,5×σ_rejeu, 2,5×(44/147), 20% max)
        /// = 1,8928 + 0,748 ≈ 2,65 s/cas. Voir v1_078_budget.log PARTIE 2.
        /// </summary>
        public const double PerCaseBudgetSeconds = 2.65;

        /// <summary>
        /// Dispersion par rejeu (même filtre LARGE, 3 runs) — s/cas.
        /// Mesuré v1_078_noise_r1/r2/r3 : 1,803 / 1,831 / 1,812.
        /// </summary>
        public const double PerCaseNoiseStdevSeconds = 0.0142;

        /// <summary>
        /// Étendue (max-min) des moyennes par cas sur les 3 rejeux.
        /// </summary>
        public const double PerCaseNoiseRangeSeconds = 0.0278;

        /// <summary>
        /// Maximum des six moyennes de session (JSON result) — s/cas.
        /// </summary>
        public const double SessionMaxPerCaseSeconds = 1.8928;

        /// <summary>
        /// Borne GROSSIÈRE sur la durée totale : n'attrape qu'une explosion.
        /// Calée très au-dessus du bruit documenté (44 s) et des suites ~280 s.
        /// Ce n'est PAS un budget fin — ne pas le faire passer pour tel.
        /// </summary>
        public const double CoarseTotalExplosionSeconds = 600.0;

        /// <summary>False : la borne totale n'est pas le garde-fou fin.</summary>
        public const bool CoarseTotalIsFineGuard = false;

        /// <summary>
        /// Plafond temps d'UN cas : filet pathologique seulement.
        /// </summary>
        public const double CaseCeilingSeconds = 39.1;

        /// <summary>False : le plafond temps ne discrimine pas balayage vs captures.</summary>
        public const bool CaseCeilingIsSweepGuard = false;

        public const double PrecutSuiteSeconds = 267.9;
        public const double PrecutPerCaseSeconds = 3.0443;
        public const int PrecutCaseCount = 88;

        /// <summary>Bruit historique TOTAL suite (v1_043) — documenté, pas le critère v1_078.</summary>
        public const double LegacyNoiseStdevSeconds = 5.294;

        static readonly string[] RetiredCalibrationNames =
        {
            "V1035_TaxSweep_Publish_And_Verdict",
            "V1038_Capacity_Sweep_Publish_And_Verdict",
            "V1039_Ai_Sweep_Publish_And_Verdict",
        };

        [Test]
        public void V1042_Suite_Budget_Holds_On_Latest_Xml()
        {
            Assert.IsTrue(
                TryAssertSuiteBudget(out var detail),
                detail);
        }

        /// <summary>
        /// Preuve ROUGE historique : XML pré-coupe v1_041 (moyenne 3,04 s/cas)
        /// dépasse PerCaseBudgetSeconds.
        /// </summary>
        [Test]
        public void V1042_Suite_Budget_Fails_On_Precut_V1041_Xml()
        {
            var path = Path.Combine(Application.dataPath, "..", "Logs", "v1_041_tests.xml");
            Assert.IsTrue(File.Exists(path), "v1_041_tests.xml requis pour la preuve ROUGE");
            Assert.IsFalse(
                TryAssertSuiteBudgetOnXml(path, out var detail),
                "Le budget par cas doit MORDRE sur le XML pré-coupe : " + detail);
            StringAssert.Contains("perCaseOk=False", detail);
        }

        /// <summary>
        /// Preuve VERT sur un XML LARGE de session (moyenne ~1,8 s/cas ≤ budget).
        /// </summary>
        [Test]
        public void V1042_Suite_Budget_Holds_On_Session_Large_Xml()
        {
            var path = Path.Combine(Application.dataPath, "..", "Logs", "v1_077_large.xml");
            if (!File.Exists(path))
                path = FindLatestTestsXml();
            Assert.IsTrue(path != null && File.Exists(path), "XML LARGE requis");
            Assert.IsTrue(
                TryAssertSuiteBudgetOnXml(path, out var detail),
                detail);
            StringAssert.Contains("perCaseOk=True", detail);
        }

        /// <summary>
        /// V1078-A — budget par cas ROUGE sous suite artificiellement ralentie
        /// (moyenne 1,86 → 3,10 s/cas, un cas représentatif porté à 3,1 s d'effet moyen).
        /// </summary>
        [Test]
        public void V1078_A_PerCase_Budget_Reds_On_Artificially_Slowed_Case()
        {
            var baseline = Path.Combine(Application.dataPath, "..", "Logs", "v1_077_large.xml");
            Assert.IsTrue(File.Exists(baseline), "v1_077_large.xml requis");
            var cases = ParseCases(baseline);
            Assert.Greater(cases.Count, 0);
            double total = 0;
            foreach (var c in cases)
                total += c.Duration;
            var meanBefore = total / cases.Count;

            // Mutation nommée : décaler toutes les durées pour porter la moyenne à 3,1 s/cas
            // (suite ralentie uniformément). Un scale ferait aussi dépasser le plafond cas ;
            // un offset préserve le diagnostic « perCaseOk=False » seul.
            const double targetMean = 3.10;
            var offset = targetMean - meanBefore;
            var mutated = CloneCasesWithOffset(cases, offset);
            Assert.Greater(targetMean, PerCaseBudgetSeconds);
            Assert.IsFalse(
                TryAssertSuiteBudgetOnCases(mutated, "synthetic:slowed_mean_3.10", out var detail),
                "V1078-A doit ROUGIR : " + detail);
            StringAssert.Contains("perCaseOk=False", detail);
            Assert.Greater(meanBefore + offset, PerCaseBudgetSeconds);
            Debug.Log(
                $"V1078-A: mean {meanBefore:0.000} → {meanBefore + offset:0.000} s/cas " +
                $"(offset={offset:0.000}) | {detail}");
        }

        /// <summary>
        /// V1078-B — budget par cas VERT après ajout de cas à coût normal ;
        /// l'ancien budget total 245 s ROUGIT (défaut prouvé absent du nouveau garde-fou).
        /// </summary>
        [Test]
        public void V1078_B_PerCase_Budget_Holds_When_Adding_Normal_Cost_Cases()
        {
            var baseline = Path.Combine(Application.dataPath, "..", "Logs", "v1_077_large.xml");
            Assert.IsTrue(File.Exists(baseline), "v1_077_large.xml requis");
            var cases = ParseCases(baseline);
            Assert.Greater(cases.Count, 0);

            double total = 0;
            foreach (var c in cases)
                total += c.Duration;
            var mean = total / cases.Count;
            // Coût « normal » = moyenne de la suite (pas la médiane : distribution skewée,
            // la médiane tirerait la moyenne et confondrait la preuve).
            var normalCost = mean;

            // Ajouter 12 cas au coût moyen (coût normal)
            const int addCount = 12;
            var expanded = new List<CaseRow>(cases);
            for (var i = 0; i < addCount; i++)
            {
                expanded.Add(new CaseRow
                {
                    Name = "VictoriaGame.Tests.Synthetic.V1078_NormalCost_" + i,
                    Duration = normalCost,
                    Result = "Passed",
                });
            }

            double total2 = 0;
            foreach (var c in expanded)
                total2 += c.Duration;
            var mean2 = total2 / expanded.Count;

            Assert.IsTrue(
                TryAssertSuiteBudgetOnCases(expanded, "synthetic:plus_12_normal", out var detail),
                "V1078-B doit rester VERT : " + detail);
            StringAssert.Contains("perCaseOk=True", detail);

            // Preuve que l'ancien budget TOTAL aurait mordu (et mord dès qu'on dépasse 245)
            Assert.Greater(total, RetiredTotalBudgetSeconds,
                "baseline v1_077 dépasse déjà l'ancien budget total 245 s");
            Assert.Greater(total2, RetiredTotalBudgetSeconds);
            // Combien de cas à coût moyen auraient fait rougir 245 depuis un total juste sous 245
            var justUnder = RetiredTotalBudgetSeconds - 0.01;
            var addedUntilRed = 0;
            var sim = justUnder;
            while (sim <= RetiredTotalBudgetSeconds && addedUntilRed < 100)
            {
                sim += normalCost;
                addedUntilRed++;
            }

            Assert.LessOrEqual(addedUntilRed, 6,
                "l'ancien budget total rougit dès ~6 cas normaux ajoutés depuis 245-ε");
            Assert.Less(
                Math.Abs(mean2 - mean),
                0.001,
                "la moyenne par cas reste identique après ajout de cas au coût moyen");
            Debug.Log(
                $"V1078-B: n {cases.Count}→{expanded.Count} mean {mean:0.000}→{mean2:0.000} ; " +
                $"oldTotalBudget would red after +{addedUntilRed} normal cases from 245-ε | {detail}");
        }

        /// <summary>
        /// V1078-C — garde-fou structurel intact et toujours mordant
        /// (délègue à V1042_No_Calibration_Sweep_Publish_Is_EditMode_Test, non modifié).
        /// </summary>
        [Test]
        public void V1078_C_Structural_Sweep_Guard_Still_Bites()
        {
            Assert.IsTrue(
                TryAssertNoSweepEditModeTests(out var detail),
                detail);

            // Preuve de morsure : le prédicat reconnaît les noms de balayage retirés
            // et les traiterait comme offenders s'ils avaient encore [Test].
            foreach (var name in RetiredCalibrationNames)
            {
                Assert.IsTrue(
                    IsCalibrationSweepPublishName(name),
                    "prédicat doit matcher " + name);
            }

            Assert.IsTrue(
                IsCalibrationSweepPublishName("V1099_FooSweep_Publish_And_Verdict"),
                "prédicat doit matcher tout *Sweep*Publish_And_Verdict");
            Assert.IsFalse(
                IsCalibrationSweepPublishName("V1042_No_Calibration_Sweep_Publish_Is_EditMode_Test"),
                "ne doit pas se flagger soi-même");

            // Les trois méthodes retirées n'ont plus [Test] — si on leur remettait
            // l'attribut, TryAssertNoSweepEditModeTests rougirait (prouvé par
            // V1042_Retired_Sweeps_Are_Not_EditMode_Tests + scan assembly).
            var tax = typeof(V1035TaxPolicyTests)
                .GetMethod(nameof(V1035TaxPolicyTests.V1035_TaxSweep_Publish_And_Verdict));
            Assert.IsNotNull(tax);
            Assert.IsFalse(HasNUnitTestAttribute(tax));
            Assert.IsTrue(IsCalibrationSweepPublishName(tax.Name));
        }

        /// <summary>
        /// Le plafond temps par cas ne peut pas interdire un balayage : démonstration.
        /// </summary>
        [Test]
        public void V1042_Case_Time_Ceiling_Cannot_Reject_Lightest_Sweep()
        {
            const double lightestSweep = 23.305;
            const double maxPostcutCase = 34.033;
            Assert.Greater(
                maxPostcutCase,
                lightestSweep,
                "Un plafond ≥ max cas actuel laisse repasser tout balayage ≤ 32,6 s");
            Assert.IsFalse(
                CaseCeilingIsSweepGuard,
                "CaseCeilingSeconds n'est PAS le garde-fou anti-balayage");
            Assert.GreaterOrEqual(
                CaseCeilingSeconds,
                maxPostcutCase,
                "Le plafond pathologique doit rester au-dessus du high-water capture");
            Assert.Greater(
                CaseCeilingSeconds,
                lightestSweep,
                "Donc il ne rejette pas le balayage léger — d'où le garde-fou structurel");
            Assert.IsFalse(
                CoarseTotalIsFineGuard,
                "CoarseTotalExplosionSeconds n'est PAS le budget fin");
        }

        /// <summary>
        /// Garde-fou structurel : aucun [Test] EditMode de balayage calibration
        /// (nom *Sweep*Publish* ou liste retired) — indépendant du bruit machine.
        /// INTACT depuis v1_043 — ne pas modifier ce test.
        /// </summary>
        [Test]
        public void V1042_No_Calibration_Sweep_Publish_Is_EditMode_Test()
        {
            Assert.IsTrue(
                TryAssertNoSweepEditModeTests(out var detail),
                detail);
        }

        [Test]
        public void V1042_LogPolicy_Protects_Suite_And_Sweep_Logs()
        {
            Assert.IsTrue(HarnessLogPolicy.IsProtectedMeasureLog("v1_042_suite.log"));
            Assert.IsTrue(HarnessLogPolicy.IsProtectedMeasureLog("v1_043_budget.log"));
            Assert.IsTrue(HarnessLogPolicy.IsProtectedMeasureLog("v1_078_budget.log"));
            Assert.IsTrue(HarnessLogPolicy.IsProtectedMeasureLog("v1_035_tax_sweep.log"));
            Assert.IsTrue(HarnessLogPolicy.IsProtectedMeasureLog("v1_065_tax_pops.log"));
            Assert.IsTrue(HarnessLogPolicy.IsProtectedMeasureLog("v1_038_buildings.log"));
            Assert.IsTrue(HarnessLogPolicy.IsProtectedMeasureLog("v1_039_ai_building.log"));
            Assert.IsFalse(HarnessLogPolicy.IsEphemeral("v1_042_suite.log"));
            Assert.IsFalse(HarnessLogPolicy.IsEphemeral("v1_043_budget.log"));
            Assert.IsFalse(HarnessLogPolicy.IsEphemeral("v1_078_budget.log"));
        }

        [Test]
        public void V1042_Retired_Sweeps_Are_Not_EditMode_Tests()
        {
            var tax = typeof(V1035TaxPolicyTests)
                .GetMethod(nameof(V1035TaxPolicyTests.V1035_TaxSweep_Publish_And_Verdict));
            var cap = typeof(V1038BuildingTests)
                .GetMethod(nameof(V1038BuildingTests.V1038_Capacity_Sweep_Publish_And_Verdict));
            var ai = typeof(V1039BuildingAiTests)
                .GetMethod(nameof(V1039BuildingAiTests.V1039_Ai_Sweep_Publish_And_Verdict));
            Assert.IsNotNull(tax);
            Assert.IsNotNull(cap);
            Assert.IsNotNull(ai);
            Assert.IsFalse(HasNUnitTestAttribute(tax), "TaxSweep ne doit plus être [Test]");
            Assert.IsFalse(HasNUnitTestAttribute(cap), "Capacity_Sweep ne doit plus être [Test]");
            Assert.IsFalse(HasNUnitTestAttribute(ai), "Ai_Sweep ne doit plus être [Test]");
        }

        /// <summary>
        /// Seuil publié AU-DESSUS de la dispersion, pas dedans (leçon v1_042).
        /// </summary>
        [Test]
        public void V1078_PerCase_Budget_Dominates_Measured_Dispersion()
        {
            Assert.Greater(
                PerCaseBudgetSeconds,
                SessionMaxPerCaseSeconds,
                "seuil doit dépasser le max de session");
            var margin = PerCaseBudgetSeconds - SessionMaxPerCaseSeconds;
            Assert.Greater(
                margin,
                PerCaseNoiseRangeSeconds,
                "marge doit dépasser l'étendue des rejeux (pas calé dans le bruit)");
            Assert.Greater(
                margin / Math.Max(PerCaseNoiseStdevSeconds, 1e-6),
                5.0,
                "marge ≥ 5×σ rejeu (pas la faute 2,4 % de v1_077→280 s)");
            Assert.Greater(
                PerCaseBudgetSeconds,
                SessionMaxPerCaseSeconds + 0.30,
                "marge ≥ bruit documenté 44 s / ~147 cas ≈ 0,30 s/cas");
        }

        static bool HasNUnitTestAttribute(MethodInfo method)
        {
            foreach (var a in method.GetCustomAttributes(false))
            {
                if (a.GetType().FullName == "NUnit.Framework.TestAttribute")
                    return true;
            }

            return false;
        }

        public static bool TryAssertNoSweepEditModeTests(out string detail)
        {
            var offenders = new List<string>();
            foreach (var t in typeof(V1042SuiteBudgetTests).Assembly.GetTypes())
            {
                if (!t.IsClass)
                    continue;
                foreach (var m in t.GetMethods(
                             BindingFlags.Instance | BindingFlags.Static |
                             BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (!IsCalibrationSweepPublishName(m.Name))
                        continue;
                    if (!HasNUnitTestAttribute(m))
                        continue;
                    offenders.Add(t.Name + "." + m.Name);
                }
            }

            if (offenders.Count == 0)
            {
                detail = "aucun [Test] Sweep_Publish / retired calibration";
                return true;
            }

            detail = "balayage(s) calibration encore [Test]: " + string.Join(", ", offenders);
            return false;
        }

        static bool IsCalibrationSweepPublishName(string methodName)
        {
            foreach (var r in RetiredCalibrationNames)
            {
                if (string.Equals(methodName, r, StringComparison.Ordinal))
                    return true;
            }

            // Patron exact des balayages retirés : ...Sweep...Publish_And_Verdict
            // (évite de se flagger soi-même : V1042_No_Calibration_Sweep_Publish_Is_...)
            return methodName.IndexOf("Sweep", StringComparison.OrdinalIgnoreCase) >= 0 &&
                   methodName.IndexOf("Publish_And_Verdict", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool TryAssertSuiteBudget(out string detail)
        {
            var xmlPath = FindLatestTestsXml();
            if (xmlPath == null || !File.Exists(xmlPath))
            {
                detail = "Aucun *_tests.xml / *_large.xml trouvé sous Logs/";
                return false;
            }

            return TryAssertSuiteBudgetOnXml(xmlPath, out detail);
        }

        /// <summary>
        /// Évalue le budget PAR CAS sur un XML donné.
        /// </summary>
        public static bool TryAssertSuiteBudgetOnXml(string xmlPath, out string detail)
        {
            var cases = ParseCases(xmlPath);
            return TryAssertSuiteBudgetOnCases(cases, Path.GetFileName(xmlPath), out detail);
        }

        public static bool TryAssertSuiteBudgetOnCases(
            List<CaseRow> cases,
            string label,
            out string detail)
        {
            detail = "";
            if (cases == null || cases.Count == 0)
            {
                detail = $"XML vide: {label}";
                return false;
            }

            double totalAll = 0;
            double maxCase = 0;
            string maxName = "";
            var retiredSeen = 0;
            foreach (var c in cases)
            {
                totalAll += c.Duration;
                if (IsRetiredCalibration(c.Name))
                    retiredSeen++;
                if (c.Duration > maxCase)
                {
                    maxCase = c.Duration;
                    maxName = ShortName(c.Name);
                }
            }

            var perCase = totalAll / cases.Count;
            // FIN : moyenne par cas (remplace SuiteBudgetSeconds total).
            var perCaseOk = perCase <= PerCaseBudgetSeconds;
            // GROSSIER : explosion totale — déclaré comme tel.
            var coarseOk = totalAll <= CoarseTotalExplosionSeconds;
            // Plafond cas : filet pathologique.
            var caseOk = maxCase <= CaseCeilingSeconds;
            detail =
                $"xml={label} cases={cases.Count} " +
                $"totalAll={totalAll:0.000}s perCase={perCase:0.000}s " +
                $"perCaseBudget={PerCaseBudgetSeconds:0.00}s perCaseOk={perCaseOk} " +
                $"coarseTotal={CoarseTotalExplosionSeconds:0.0}s coarseOk={coarseOk} " +
                $"coarseIsFine={CoarseTotalIsFineGuard} " +
                $"(retiredSeen={retiredSeen}) max={maxCase:0.000}s ({maxName}) " +
                $"ceiling={CaseCeilingSeconds:0.0}s ceilingIsSweepGuard={CaseCeilingIsSweepGuard} " +
                $"caseOk={caseOk} retiredTotalBudget={RetiredTotalBudgetSeconds:0.0}s";
            return perCaseOk && caseOk && coarseOk;
        }

        public static void RunMeasureAndWriteLog()
        {
            var logsDir = Path.Combine(Application.dataPath, "..", "Logs");
            Directory.CreateDirectory(logsDir);
            var sb = new StringBuilder(128 * 1024);

            sb.AppendLine("=== v1_078 BUDGET PAR CAS — outillage, zéro règle simu ===");
            sb.AppendLine(
                "Leçon v1_042/v1_077: un budget calé DANS son bruit n'est pas un budget. " +
                "Un budget TOTAL interdit d'écrire des tests ; un budget par cas interdit " +
                "de les rendre lents.");
            sb.AppendLine();

            sb.AppendLine("=== PARTIE 1 — SIX SUITES SESSION (recoupées result JSON) ===");
            AppendSessionTable(sb);

            sb.AppendLine();
            sb.AppendLine("=== PARTIE 1b — DISPERSION PAR REJET (même filtre LARGE) ===");
            AppendReplayNoiseSection(sb, logsDir);

            sb.AppendLine();
            sb.AppendLine("=== PARTIE 1c — DIX CAS LES PLUS COÛTEUX (v1_077_large / latest) ===");
            AppendTopExpensiveCases(sb, logsDir);

            sb.AppendLine();
            sb.AppendLine("=== PARTIE 2 — BUDGET PAR CAS RETENU ===");
            var margin = PerCaseBudgetSeconds - SessionMaxPerCaseSeconds;
            var sigmaRatio = margin / Math.Max(PerCaseNoiseStdevSeconds, 1e-9);
            sb.AppendLine(
                $"PerCaseBudgetSeconds={PerCaseBudgetSeconds.ToString("F2", CultureInfo.InvariantCulture)} s/cas");
            sb.AppendLine(
                $"SessionMaxPerCaseSeconds={SessionMaxPerCaseSeconds.ToString("F4", CultureInfo.InvariantCulture)}");
            sb.AppendLine(
                $"PerCaseNoiseStdevSeconds={PerCaseNoiseStdevSeconds.ToString("F4", CultureInfo.InvariantCulture)} " +
                $"PerCaseNoiseRangeSeconds={PerCaseNoiseRangeSeconds.ToString("F4", CultureInfo.InvariantCulture)}");
            sb.AppendLine(
                $"marge_sous_seuil={margin.ToString("F4", CultureInfo.InvariantCulture)} s/cas " +
                $"= {(margin / SessionMaxPerCaseSeconds * 100.0).ToString("F1", CultureInfo.InvariantCulture)}% " +
                $"au-dessus du max session ; {sigmaRatio.ToString("F1", CultureInfo.InvariantCulture)} x sigma rejeu");
            sb.AppendLine(
                $"CoarseTotalExplosionSeconds={CoarseTotalExplosionSeconds.ToString("F0", CultureInfo.InvariantCulture)} " +
                $"CoarseTotalIsFineGuard={CoarseTotalIsFineGuard} " +
                "(GROSSIER — explosion seulement ; ne voit pas une lenteur douce)");
            sb.AppendLine(
                $"RetiredTotalBudgetSeconds={RetiredTotalBudgetSeconds.ToString("F0", CultureInfo.InvariantCulture)} " +
                "(retraité — ne plus asserter)");
            sb.AppendLine(
                $"CaseCeilingSeconds={CaseCeilingSeconds.ToString("F1", CultureInfo.InvariantCulture)} " +
                $"CaseCeilingIsSweepGuard={CaseCeilingIsSweepGuard}");
            sb.AppendLine(
                "CE QUE LE BUDGET PAR CAS NE VOIT PAS: une suite qui devient trop longue " +
                "à force de cas légitimes à coût normal (la moyenne reste stable). " +
                "La borne coarse 600 s n'attrape qu'une explosion, pas une dérive douce.");

            sb.AppendLine();
            sb.AppendLine("=== PARTIE 3 — PREUVES DE MORSURE ===");
            AppendBiteProofs(sb, logsDir);

            sb.AppendLine();
            sb.AppendLine("=== VERDICT MESURÉ ===");
            sb.AppendLine(
                "six suites recoupées, durée par cas 1,893 / 1,770 / 1,854 / 1,778 / 1,796 / 1,861 s, " +
                "étendue ±3,4 % ; dispersion par répétition σ=" +
                PerCaseNoiseStdevSeconds.ToString("F4", CultureInfo.InvariantCulture) +
                " s/cas sur 3 rejeux, étendue " +
                PerCaseNoiseRangeSeconds.ToString("F4", CultureInfo.InvariantCulture) +
                " ; dix cas les plus coûteux publiés (PARTIE 1c) ; seuil retenu " +
                PerCaseBudgetSeconds.ToString("F2", CultureInfo.InvariantCulture) +
                " s/cas, soit " +
                ((PerCaseBudgetSeconds / SessionMaxPerCaseSeconds - 1.0) * 100.0)
                    .ToString("F1", CultureInfo.InvariantCulture) +
                "% au-dessus du maximum observé et " +
                ((PerCaseBudgetSeconds - SessionMaxPerCaseSeconds) /
                    Math.Max(PerCaseNoiseStdevSeconds, 1e-9))
                    .ToString("F1", CultureInfo.InvariantCulture) +
                " écarts-types au-dessus de la dispersion rejeu ; " +
                "V1078-A rouge sur moyenne ralentie 1,8→3,1 s/cas ; " +
                "V1078-B reste vert après +12 cas au coût moyen, là où le budget total 245 s " +
                "rougissait déjà sur la baseline ; garde-fou structurel intact et prouvé mordant ; " +
                "borne coarse 600 s déclarée grossière (ne voit pas dérive douce par ajout de cas).");

            var budgetPath = Path.Combine(logsDir, "v1_078_budget.log");
            File.WriteAllText(budgetPath, sb.ToString(), Encoding.UTF8);
            // Miroir historique (politique _budget.log / _suite.log)
            var suitePath = Path.Combine(logsDir, "v1_042_suite.log");
            File.WriteAllText(suitePath, sb.ToString(), Encoding.UTF8);
            Debug.Log($"V1042SuiteBudgetTests: wrote {budgetPath}");
        }

        static void AppendSessionTable(StringBuilder sb)
        {
            // Couples CTO à recouper ; valeurs exactes des result JSON.
            var rows = new[]
            {
                new SessionRow("v1_070", 198.74, 105, 198.74),
                new SessionRow("v1_071", 198.29, 112, 198.29),
                new SessionRow("v1_073", 218.8, 118, 218.82),
                new SessionRow("v1_074", 220.5, 124, 220.47),
                new SessionRow("v1_076", 255.0, 142, 255.07),
                new SessionRow("v1_077", 273.53, 147, 273.53),
            };

            sb.AppendLine(
                "id       brief_dur  json_dur  n    s/cas    note");
            double minPc = double.MaxValue, maxPc = 0;
            foreach (var r in rows)
            {
                var pc = r.JsonDuration / r.Cases;
                if (pc < minPc) minPc = pc;
                if (pc > maxPc) maxPc = pc;
                var note = "";
                if (Math.Abs(r.BriefDuration - r.JsonDuration) > 0.05)
                    note = $"brief≈{r.BriefDuration:0.##} json={r.JsonDuration:0.##} (arrondi CTO OK)";
                else
                    note = "OK recoupé";
                sb.AppendLine(
                    $"{r.Id}  {r.BriefDuration.ToString("0.00", CultureInfo.InvariantCulture),8}  " +
                    $"{r.JsonDuration.ToString("0.00", CultureInfo.InvariantCulture),8}  " +
                    $"{r.Cases,3}  {pc.ToString("0.000", CultureInfo.InvariantCulture),6}  {note}");
            }

            var mid = (minPc + maxPc) / 2.0;
            var halfRangePct = (maxPc - minPc) * 50.0 / mid;
            var halfStr = halfRangePct.ToString("0.0", CultureInfo.InvariantCulture);
            var minStr = minPc.ToString("0.000", CultureInfo.InvariantCulture);
            var maxStr = maxPc.ToString("0.000", CultureInfo.InvariantCulture);
            var rangeStr = (maxPc - minPc).ToString("0.000", CultureInfo.InvariantCulture);
            var midStr = mid.ToString("0.000", CultureInfo.InvariantCulture);
            sb.AppendLine(
                "étendue s/cas: min=" + minStr +
                " max=" + maxStr +
                " range=" + rangeStr +
                " (±" + halfStr + "% autour de " + midStr + ")");
            sb.AppendLine(
                "Corrections vs brief: v1_073 json=218.82 (brief 218.8) ; " +
                "v1_074 json=220.47 (brief 220.5) ; v1_076 json=255.07 (brief 255) — " +
                "arrondis, conclusion inchangée.");
            sb.AppendLine(
                $"SessionMaxPerCaseSeconds const={SessionMaxPerCaseSeconds.ToString("F4", CultureInfo.InvariantCulture)} " +
                $"(v1_070 = { (198.74 / 105).ToString("F4", CultureInfo.InvariantCulture) })");
        }

        static void AppendReplayNoiseSection(StringBuilder sb, string logsDir)
        {
            var names = new[]
            {
                "v1_078_noise_r1.xml",
                "v1_078_noise_r2.xml",
                "v1_078_noise_r3.xml",
            };
            var perCaseMeans = new List<double>();
            foreach (var name in names)
            {
                var path = Path.Combine(logsDir, name);
                if (!File.Exists(path))
                {
                    sb.AppendLine($"{name}: MANQUANT");
                    continue;
                }

                var cases = ParseCases(path);
                double t = 0;
                foreach (var c in cases)
                    t += c.Duration;
                var pc = cases.Count > 0 ? t / cases.Count : 0;
                perCaseMeans.Add(pc);
                sb.AppendLine(
                    $"{name}: n={cases.Count} total={t.ToString("0.000", CultureInfo.InvariantCulture)}s " +
                    $"perCase={pc.ToString("0.000", CultureInfo.InvariantCulture)}s");
            }

            if (perCaseMeans.Count >= 2)
            {
                double sum = 0, min = perCaseMeans[0], max = perCaseMeans[0];
                foreach (var v in perCaseMeans)
                {
                    sum += v;
                    if (v < min) min = v;
                    if (v > max) max = v;
                }

                var mean = sum / perCaseMeans.Count;
                double varSum = 0;
                foreach (var v in perCaseMeans)
                    varSum += (v - mean) * (v - mean);
                var stdev = Math.Sqrt(varSum / (perCaseMeans.Count - 1));
                sb.AppendLine(
                    $"rejeu perCase: min={min.ToString("0.000", CultureInfo.InvariantCulture)} " +
                    $"max={max.ToString("0.000", CultureInfo.InvariantCulture)} " +
                    $"range={(max - min).ToString("0.000", CultureInfo.InvariantCulture)} " +
                    $"stdev={stdev.ToString("0.000", CultureInfo.InvariantCulture)} " +
                    $"mean={mean.ToString("0.000", CultureInfo.InvariantCulture)}");
                sb.AppendLine(
                    $"constantes publiées: PerCaseNoiseStdevSeconds={PerCaseNoiseStdevSeconds.ToString("F4", CultureInfo.InvariantCulture)} " +
                    $"PerCaseNoiseRangeSeconds={PerCaseNoiseRangeSeconds.ToString("F4", CultureInfo.InvariantCulture)}");
                sb.AppendLine(
                    $"bruit doc 44s suite entière → { (44.0 / 147.0).ToString("0.000", CultureInfo.InvariantCulture) } s/cas @ n=147");
            }
            else
            {
                sb.AppendLine(
                    "Rejeux incomplets — σ publiée dans les constantes est une borne " +
                    "provisoire à remplacer dès que r1/r2/r3 existent.");
            }
        }

        static void AppendTopExpensiveCases(StringBuilder sb, string logsDir)
        {
            var path = Path.Combine(logsDir, "v1_078_noise_r3.xml");
            if (!File.Exists(path))
                path = Path.Combine(logsDir, "v1_078_large.xml");
            if (!File.Exists(path))
                path = Path.Combine(logsDir, "v1_077_large.xml");
            if (!File.Exists(path))
            {
                var latest = FindLatestTestsXml();
                path = latest;
            }

            if (path == null || !File.Exists(path))
            {
                sb.AppendLine("aucun XML pour top10");
                return;
            }

            var cases = ParseCases(path);
            cases.Sort((a, b) => b.Duration.CompareTo(a.Duration));
            sb.AppendLine($"source={Path.GetFileName(path)}");
            for (var i = 0; i < Math.Min(10, cases.Count); i++)
            {
                sb.AppendLine(
                    $"  #{i + 1} {cases[i].Duration.ToString("0.000", CultureInfo.InvariantCulture)}s " +
                    ShortName(cases[i].Name));
            }
        }

        static void AppendBiteProofs(StringBuilder sb, string logsDir)
        {
            var baseline = Path.Combine(logsDir, "v1_077_large.xml");
            if (!File.Exists(baseline))
            {
                sb.AppendLine("V1078-A/B: v1_077_large.xml manquant");
            }
            else
            {
                var cases = ParseCases(baseline);
                double total = 0;
                foreach (var c in cases)
                    total += c.Duration;
                var mean = total / cases.Count;
                const double targetMean = 3.10;
                var slowed = CloneCasesWithOffset(cases, targetMean - mean);
                var red = TryAssertSuiteBudgetOnCases(slowed, "synthetic:slowed_3.10", out var redDetail);
                sb.AppendLine(
                    $"V1078-A ROUGE slowed mean {mean.ToString("0.000", CultureInfo.InvariantCulture)}→" +
                    $"{targetMean.ToString("0.000", CultureInfo.InvariantCulture)}: " +
                    $"assertFails={!red} | {redDetail}");

                var normalCost = mean;
                var expanded = new List<CaseRow>(cases);
                for (var i = 0; i < 12; i++)
                {
                    expanded.Add(new CaseRow
                    {
                        Name = "Synthetic.Normal_" + i,
                        Duration = normalCost,
                        Result = "Passed",
                    });
                }

                var green = TryAssertSuiteBudgetOnCases(
                    expanded, "synthetic:plus_12", out var greenDetail);
                double t2 = 0;
                foreach (var c in expanded)
                    t2 += c.Duration;
                sb.AppendLine(
                    $"V1078-B VERT +12 normal (meanCost={normalCost.ToString("0.000", CultureInfo.InvariantCulture)}): " +
                    $"ok={green} total={t2.ToString("0.000", CultureInfo.InvariantCulture)} " +
                    $"(retired 245 would red: {t2 > RetiredTotalBudgetSeconds}) | {greenDetail}");
            }

            var precut = Path.Combine(logsDir, "v1_041_tests.xml");
            if (File.Exists(precut))
            {
                var preRed = TryAssertSuiteBudgetOnXml(precut, out var preDetail);
                sb.AppendLine(
                    $"pre-coupe v1_041 (perCase≈{PrecutPerCaseSeconds.ToString("F3", CultureInfo.InvariantCulture)}): " +
                    $"assertFails={!preRed} | {preDetail}");
            }

            var structOk = TryAssertNoSweepEditModeTests(out var structDetail);
            sb.AppendLine($"V1078-C structurel Sweep_Publish not [Test]: ok={structOk} | {structDetail}");
            sb.AppendLine(
                "V1078-C morsure: prédicat match retired + *Sweep*Publish_And_Verdict ; " +
                "remettre [Test] sur TaxSweep ferait rougir TryAssertNoSweepEditModeTests.");
        }

        static List<CaseRow> CloneCasesWithOffset(List<CaseRow> source, double offset)
        {
            var list = new List<CaseRow>(source.Count);
            foreach (var c in source)
            {
                list.Add(new CaseRow
                {
                    Name = c.Name,
                    Duration = c.Duration + offset,
                    Result = c.Result,
                });
            }

            return list;
        }

        static List<CaseRow> CloneCasesWithScaledDurations(List<CaseRow> source, double scale)
        {
            var list = new List<CaseRow>(source.Count);
            foreach (var c in source)
            {
                list.Add(new CaseRow
                {
                    Name = c.Name,
                    Duration = c.Duration * scale,
                    Result = c.Result,
                });
            }

            return list;
        }

        static double MedianDuration(List<CaseRow> cases)
        {
            var ds = new List<double>(cases.Count);
            foreach (var c in cases)
                ds.Add(c.Duration);
            return Median(ds);
        }

        static double Median(List<double> values)
        {
            if (values == null || values.Count == 0)
                return 0;
            var tmp = new List<double>(values);
            tmp.Sort();
            var mid = tmp.Count / 2;
            if ((tmp.Count & 1) == 1)
                return tmp[mid];
            return 0.5 * (tmp[mid - 1] + tmp[mid]);
        }

        static bool IsRetiredCalibration(string fullname)
        {
            var n = ShortName(fullname);
            foreach (var r in RetiredCalibrationNames)
            {
                if (n == r)
                    return true;
            }

            return false;
        }

        static string ShortName(string fullname)
        {
            var i = fullname.LastIndexOf('.');
            return i >= 0 ? fullname.Substring(i + 1) : fullname;
        }

        public static string FindLatestTestsXml()
        {
            var logsDir = Path.Combine(Application.dataPath, "..", "Logs");
            if (!Directory.Exists(logsDir))
                return null;

            var preferredNames = new[]
            {
                "v1_078_large.xml",
                "v1_078_noise_r3.xml",
                "v1_078_noise_r2.xml",
                "v1_078_noise_r1.xml",
                "v1_077_large.xml",
                "v1_076_large.xml",
            };
            foreach (var pref in preferredNames)
            {
                var p = Path.Combine(logsDir, pref);
                if (File.Exists(p))
                    return p;
            }

            string best = null;
            DateTime bestWrite = DateTime.MinValue;
            foreach (var pattern in new[] { "v1_*_large.xml", "v1_*_tests.xml" })
            {
                foreach (var f in Directory.GetFiles(logsDir, pattern))
                {
                    var name = Path.GetFileName(f);
                    if (name.IndexOf("playmode", StringComparison.OrdinalIgnoreCase) >= 0)
                        continue;
                    if (name.IndexOf("focus", StringComparison.OrdinalIgnoreCase) >= 0)
                        continue;
                    if (name.IndexOf("quick", StringComparison.OrdinalIgnoreCase) >= 0)
                        continue;
                    if (string.Equals(name, "v1_041_tests.xml", StringComparison.OrdinalIgnoreCase))
                        continue;
                    var wt = File.GetLastWriteTimeUtc(f);
                    if (wt > bestWrite)
                    {
                        bestWrite = wt;
                        best = f;
                    }
                }
            }

            return best;
        }

        public static List<CaseRow> ParseCases(string xmlPath)
        {
            var list = new List<CaseRow>();
            var doc = XDocument.Load(xmlPath);
            foreach (var tc in doc.Descendants("test-case"))
            {
                var name = (string)tc.Attribute("fullname") ?? (string)tc.Attribute("name") ?? "";
                var durStr = (string)tc.Attribute("duration") ?? "0";
                double.TryParse(durStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var dur);
                var result = (string)tc.Attribute("result") ?? "";
                list.Add(new CaseRow { Name = name, Duration = dur, Result = result });
            }

            return list;
        }

        public struct CaseRow
        {
            public string Name;
            public double Duration;
            public string Result;
        }

        struct SessionRow
        {
            public string Id;
            public double BriefDuration;
            public int Cases;
            public double JsonDuration;

            public SessionRow(string id, double brief, int cases, double json)
            {
                Id = id;
                BriefDuration = brief;
                Cases = cases;
                JsonDuration = json;
            }
        }
    }
}
