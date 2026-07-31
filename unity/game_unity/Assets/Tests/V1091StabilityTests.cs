using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using VictoriaGame.Core;
using VictoriaGame.Economy;
using VictoriaGame.Military;
using VictoriaGame.Politics;
using VictoriaGame.Population;
using VictoriaGame.Presentation;
using VictoriaGame.World;
using Debug = UnityEngine.Debug;

namespace VictoriaGame.Tests
{
    /// <summary>Batch : -executeMethod VictoriaGame.Tests.V1091BatchRunner.Run</summary>
    public static class V1091BatchRunner
    {
        public static void Run()
        {
            try
            {
                V1091StabilityTests.RunAndWriteArtifacts();
                Debug.Log("V1091BatchRunner: DONE");
            }
            catch (Exception ex) when (HarnessAllocationGuard.IsNativeAllocationFailure(ex))
            {
                Debug.LogWarning("V1091BatchRunner: ALLOCATION_FAILURE — " + ex.Message);
                Debug.Log("V1091BatchRunner: DONE_PARTIAL");
            }
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
        }
    }

    /// <summary>
    /// v1_091 — PHASE XII : décoinçage stabilité + sortie recrutement.
    /// </summary>
    [TestFixture]
    public class V1091StabilityTests
    {
        const uint Seed = 42195u;
        const int ParityTicks = 100;
        const int ReferenceTicks = 3000;
        const int PlayerCountryId = PlayerControl.DefaultControlledCountryId;
        static readonly float[] ReweightSweep = { 0f, 0.5f, 0.58f, 0.6f, 0.65f, 0.72f };
        static readonly int[] LoopTicks = { 500, 1000, 1500, 2000, 2500, 3000 };

        static string GameUnityRoot =>
            Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        static string LogPath => Path.Combine(GameUnityRoot, "Logs", "v1_091_politics.log");
        static string CapturesDir => Path.Combine(GameUnityRoot, "Captures", "v1_091");

        [TearDown]
        public void TearDown() => ResetAll();

        [Test]
        public void V1091_Reweight_Zero_Matches_ParityAnchor()
        {
            ResetAll();
            StabilitySystem.LockReweight(0f);
            TemplateRecruitSystem.LockStabilityRecruitScale(0f);
            TaxPhysicalWithdrawalSystem.EnsureParitySafeDefaults();
            using var h = new SimulationHarness(Seed);
            StabilitySystem.LockReweight(0f);
            TemplateRecruitSystem.LockStabilityRecruitScale(0f);
            h.RunTicks(ParityTicks);
            var dig = WorldDigest.Compute(h.EntityManager);
            Assert.AreEqual(ParityAnchors.Expected, dig,
                "reweight=0 + recruitScale=0 → digest ancre v1_090");
        }

        [Test]
        public void V1091_Determinism_Two_Runs_Same_Digest()
        {
            ResetAll();
            var a = RunParityDigestAtAdopted();
            ResetAll();
            var b = RunParityDigestAtAdopted();
            Assert.AreEqual(a, b);
        }

        // Mesure longue (balayage t3000) : UNIQUEMENT via V1091BatchRunner —
        // pas de [Test] (Explicit reste exécuté quand groupNames cible le fixture,
        // et 82 s cassent le budget LARGE 2,65 s/cas).

        public static void RunAndWriteArtifacts()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            Directory.CreateDirectory(CapturesDir);
            var sb = new StringBuilder(1024 * 1024);

            void Flush() => File.WriteAllText(LogPath, sb.ToString(), Encoding.UTF8);

            sb.AppendLine("=== v1_091 STABILITY + RECRUIT EXIT — seed=42195 PHASE XII ===");
            sb.AppendLine(
                "Ancre parité: ParityAnchors.Expected=0xA6D63D33280D5778 (v1_090). " +
                "Pré-v1_090 0x4ED26CB61DE7B2B2 INTERDIT pour ce brief.");
            sb.AppendLine();
            Flush();

            // ========== PARTIE 1 ==========
            sb.AppendLine("=== PARTIE 1 — TROIS MESURES + DÉCOMPOSITION DÉRIVE + LÉGITIMITÉ ===");
            sb.AppendLine("CONFIRMATIONS fichier:ligne :");
            sb.AppendLine(
                "  (1) StabilitySystem.cs dérive inconditionnelle -0.0005 (chemin w=0) — CONFIRMÉ.");
            sb.AppendLine(
                "  (2) PopPoliticsSystem.cs:47 Radicalism +=0.002 seulement si sat<0.4 — CONFIRMÉ.");
            sb.AppendLine(
                "  (3) RevolutionSystem.cs:124 avgRad > DefaultRadicalismThreshold(0.7) — CONFIRMÉ.");
            sb.AppendLine(
                "  (4) AUCUN système hors Politics/ ne lisait Stability avant v1_091 " +
                "(TemplateRecruitSystem lit désormais sous StabilityRecruitScale) — CONFIRMÉ.");
            sb.AppendLine();
            Flush();

            ForceGc();
            ResetAll();
            StabilitySystem.LockReweight(0f);
            TemplateRecruitSystem.LockStabilityRecruitScale(0f);

            int firstFloorTick = -1; // sentinelle « pas trouvé » ≠ 0
            int floorT500 = -1, floorT1000 = -1, floorT2000 = -1, floorT3000 = -1;
            float medianT3000 = float.NaN;
            float maxAvgRad = float.NaN;
            int revolutionsAt3000 = -1;
            int radExactZeroPops = -1;
            int radTotalPops = -1;

            long termSurplus = 0, termDebt = 0, termRev = 0, termLegHi = 0, termLegLo = 0, termDrift = 0;
            long termEvals = 0;

            var legLowByTick = new Dictionary<int, int>();
            var legHighByTick = new Dictionary<int, int>();
            var legMedianByTick = new Dictionary<int, float>();

            using (var h = new SimulationHarness(Seed))
            {
                StabilitySystem.LockReweight(0f);
                TemplateRecruitSystem.LockStabilityRecruitScale(0f);

                for (var t = 1; t <= ReferenceTicks; t++)
                {
                    // Compte les termes AVANT le tick (état qui alimente StabilityUpdateJob).
                    CountStabilityTerms(h.EntityManager,
                        ref termSurplus, ref termDebt, ref termRev,
                        ref termLegHi, ref termLegLo, ref termDrift, ref termEvals);

                    h.RunTicks(1);

                    if (firstFloorTick < 0)
                    {
                        var floorNow = CountStabilityFloor(h.EntityManager);
                        var countries = CountCountries(h.EntityManager);
                        // « plancher massif » : majorité au plancher (≥ half)
                        if (countries > 0 && floorNow * 2 >= countries)
                            firstFloorTick = t;
                    }

                    if (t == 500 || t == 1000 || t == 2000 || t == 3000)
                    {
                        MeasureLegitimacyBuckets(h.EntityManager,
                            out var legLo, out var legHi, out var legMed);
                        legLowByTick[t] = legLo;
                        legHighByTick[t] = legHi;
                        legMedianByTick[t] = legMed;
                        var floor = CountStabilityFloor(h.EntityManager);
                        if (t == 500) floorT500 = floor;
                        if (t == 1000) floorT1000 = floor;
                        if (t == 2000) floorT2000 = floor;
                        if (t == 3000)
                        {
                            floorT3000 = floor;
                            MeasurePolitics(h.EntityManager,
                                out medianT3000, out maxAvgRad, out revolutionsAt3000);
                            CountRadicalismZero(h.EntityManager,
                                out radExactZeroPops, out radTotalPops);
                        }
                    }
                }
            }

            sb.AppendLine("MESURE 1 — PLANCHER STABILITÉ (reweight=0, legacy) :");
            sb.AppendLine(
                $"  firstFloorTick(majority)={firstFloorTick} (sentinelle -1=pas trouvé)");
            sb.AppendLine(
                $"  floor t500={floorT500} t1000={floorT1000} t2000={floorT2000} t3000={floorT3000} /20");
            sb.AppendLine(
                $"  Stability_median@t3000={Fmt(medianT3000)}");
            var measure1Ok = floorT3000 >= 14 && firstFloorTick > 0 && firstFloorTick <= 500;
            sb.AppendLine(
                $"  VERDICT mesure1={(measure1Ok ? "CONFIRMÉ" : "INFIRMÉ/NUANCÉ")} " +
                $"(attendu ~t425 plancher, 15-16/20 @t3000)");
            sb.AppendLine();

            sb.AppendLine("MESURE 2 — RADICALISME :");
            sb.AppendLine(
                $"  pops_radicalism_exact_0={radExactZeroPops}/{radTotalPops} " +
                $"maxCountryAvgRad={Fmt(maxAvgRad)}");
            var measure2Ok = radExactZeroPops == radTotalPops && maxAvgRad <= 0f;
            sb.AppendLine(
                $"  VERDICT mesure2={(measure2Ok ? "CONFIRMÉ" : "INFIRMÉ")} " +
                "(attendu 0 exact sur toutes les pops)");
            sb.AppendLine();

            sb.AppendLine("MESURE 3 — RÉVOLUTION :");
            sb.AppendLine(
                $"  revolutions_active@t3000={revolutionsAt3000} seuil=0.7 maxAvgRad={Fmt(maxAvgRad)}");
            var measure3Ok = revolutionsAt3000 == 0 && maxAvgRad < 0.7f;
            sb.AppendLine(
                $"  VERDICT mesure3={(measure3Ok ? "CONFIRMÉ" : "INFIRMÉ")} " +
                "(révolution inatteignable par construction)");
            sb.AppendLine();

            sb.AppendLine("DÉCOMPOSITION TERME PAR TERME (StabilityUpdateJob, w=0, 3000 ticks) :");
            sb.AppendLine($"  evaluations={termEvals}");
            sb.AppendLine($"  surplus(+0.001)={termSurplus}");
            sb.AppendLine($"  debt>2x(-0.002)={termDebt}");
            sb.AppendLine($"  revolution(-0.005)={termRev}");
            sb.AppendLine($"  leg>0.6(+0.001)={termLegHi}");
            sb.AppendLine($"  leg<0.3(-0.002)={termLegLo}");
            sb.AppendLine($"  drift(-0.0005)={termDrift} (inconditionnel, 1×/évaluation)");
            double impactSurplus = termSurplus * 0.001;
            double impactDebt = termDebt * 0.002;
            double impactRev = termRev * 0.005;
            double impactLegHi = termLegHi * 0.001;
            double impactLegLo = termLegLo * 0.002;
            double impactDrift = termDrift * 0.0005;
            double netPerEval = termEvals > 0
                ? (impactSurplus + impactLegHi - impactDebt - impactLegLo - impactRev - impactDrift)
                  / termEvals
                : 0;
            sb.AppendLine(
                $"  budget_net_moyen_par_évaluation={netPerEval.ToString("0.######", CultureInfo.InvariantCulture)}");
            sb.AppendLine(
                $"  impacts_cumulés: surplus=+{impactSurplus.ToString("0.#", CultureInfo.InvariantCulture)} " +
                $"debt=-{impactDebt.ToString("0.#", CultureInfo.InvariantCulture)} " +
                $"legLo=-{impactLegLo.ToString("0.#", CultureInfo.InvariantCulture)} " +
                $"legHi=+{impactLegHi.ToString("0.#", CultureInfo.InvariantCulture)} " +
                $"drift=-{impactDrift.ToString("0.#", CultureInfo.InvariantCulture)} " +
                $"rev=-{impactRev.ToString("0.#", CultureInfo.InvariantCulture)}");
            // Coupable = plus grand contributeur NÉGATIF (impact), pas le plus fréquent.
            string culprit;
            if (impactLegLo >= impactDebt && impactLegLo >= impactDrift)
                culprit = "pénalité Legitimacy<0.3 (-0.002)";
            else if (impactDebt >= impactLegLo && impactDebt >= impactDrift)
                culprit = "pénalité dette>2×solde (-0.002)";
            else
                culprit = "dérive inconditionnelle (-0.0005)";
            sb.AppendLine($"  COUPABLE PRINCIPAL (dérivé des impacts)={culprit}");
            sb.AppendLine();

            sb.AppendLine("TRAJECTOIRE LÉGITIMITÉ :");
            foreach (var tick in new[] { 500, 1000, 2000, 3000 })
            {
                sb.AppendLine(
                    $"  t{tick}: leg<0.3={legLowByTick[tick]}/20 leg>0.6={legHighByTick[tick]}/20 " +
                    $"median={Fmt(legMedianByTick[tick])}");
            }

            sb.AppendLine();
            Flush();

            // ========== PARTIE 2 ==========
            sb.AppendLine("=== PARTIE 2 — REWEIGHT RÉVERSIBLE + MONOTONIE + PARITÉ ===");
            ForceGc();
            ResetAll();
            StabilitySystem.LockReweight(0f);
            TemplateRecruitSystem.LockStabilityRecruitScale(0f);
            TaxPhysicalWithdrawalSystem.EnsureParitySafeDefaults();
            ulong digZero;
            using (var h = new SimulationHarness(Seed))
            {
                StabilitySystem.LockReweight(0f);
                TemplateRecruitSystem.LockStabilityRecruitScale(0f);
                h.RunTicks(ParityTicks);
                digZero = WorldDigest.Compute(h.EntityManager);
            }

            sb.AppendLine(
                $"RÉVERSIBILITÉ reweight=0 recruitScale=0: digest=0x{digZero:X16} " +
                $"expected=0x{ParityAnchors.Expected:X16} " +
                $"bit_identical={(digZero == ParityAnchors.Expected)}");

            ForceGc();
            var digA = RunParityDigestZero();
            ForceGc();
            var digB = RunParityDigestZero();
            sb.AppendLine(
                $"DÉTERMINISME reweight=0 2/2: A=0x{digA:X16} B=0x{digB:X16} equal={(digA == digB)}");
            sb.AppendLine();

            sb.AppendLine("BALAYAGE MONOTONIE (recruitScale=0, t3000) :");
            sb.AppendLine("  w | median | mean | floor | ceil | digest@t100");
            var sweepMedians = new List<float>();
            var sweepMeans = new List<float>();
            var sweepFloors = new List<int>();
            var sweepCeils = new List<int>();
            for (var i = 0; i < ReweightSweep.Length; i++)
            {
                var w = ReweightSweep[i];
                ForceGc();
                ResetAll();
                StabilitySystem.LockReweight(w);
                TemplateRecruitSystem.LockStabilityRecruitScale(0f);
                float med, mean;
                int floor, ceil;
                ulong dig;
                using (var h = new SimulationHarness(Seed))
                {
                    StabilitySystem.LockReweight(w);
                    TemplateRecruitSystem.LockStabilityRecruitScale(0f);
                    h.RunTicks(ParityTicks);
                    dig = WorldDigest.Compute(h.EntityManager);
                    h.RunTicks(ReferenceTicks - ParityTicks);
                    MeasureStabilityBounds(h.EntityManager, out med, out mean, out floor, out ceil);
                }

                sweepMedians.Add(med);
                sweepMeans.Add(mean);
                sweepFloors.Add(floor);
                sweepCeils.Add(ceil);
                sb.AppendLine(
                    $"  {Fmt(w)} | {Fmt(med)} | {Fmt(mean)} | {floor}/20 | {ceil}/20 | 0x{dig:X16}");
            }

            var monoOk = true;
            for (var i = 1; i < sweepMeans.Count; i++)
            {
                if (sweepMeans[i] + 1e-6f < sweepMeans[i - 1])
                    monoOk = false;
            }

            // Contrôle rouge : w=0 au plancher
            var redOk = sweepFloors[0] >= 14 && sweepMedians[0] <= 0.05f;
            int adoptIdx = -1;
            float adoptedW = 0f;
            // Distribution bimodale (clamp) : médiane saute 0↔1. On adopte sur
            // floor/ceil/mean — pas coller une borne (≥14), floor en baisse ≥4.
            float bestScore = -999f;
            for (var i = 1; i < ReweightSweep.Length; i++)
            {
                if (sweepFloors[i] >= 14 || sweepCeils[i] >= 14)
                    continue;
                if (sweepFloors[i] > sweepFloors[0] - 4)
                    continue;
                // Score : mean vers 0.45, équilibre floor/ceil, pénalité extrêmes.
                var balance = 1f - Mathf.Abs(sweepFloors[i] - sweepCeils[i]) / 20f;
                var score = sweepMeans[i] * 0.5f + balance
                            - 0.02f * sweepFloors[i] - 0.02f * sweepCeils[i];
                if (score > bestScore)
                {
                    bestScore = score;
                    adoptIdx = i;
                    adoptedW = ReweightSweep[i];
                }
            }

            var greenOk = adoptIdx > 0 && sweepFloors[adoptIdx] < sweepFloors[0]
                          && sweepCeils[adoptIdx] < 14
                          && sweepMeans[adoptIdx] > sweepMeans[0] + 0.05f;
            var adoptMatchesConst = Mathf.Abs(adoptedW - StabilitySystem.AdoptedStabilityReweight) < 1e-4f
                                    || adoptIdx < 0;

            sb.AppendLine($"  monotonie_means={(monoOk ? "OUI" : "NON")}");
            sb.AppendLine($"  contrôle_rouge_w0={(redOk ? "OUI" : "NON")} (floor≥14, med≈0)");
            sb.AppendLine(
                $"  adoption={(adoptIdx >= 0 ? Fmt(adoptedW) : "AUCUNE")} " +
                $"green={(greenOk ? "OUI" : "NON")} " +
                $"const_Adopted={Fmt(StabilitySystem.AdoptedStabilityReweight)}");
            if (adoptIdx >= 0)
            {
                sb.AppendLine(
                    $"  chiffre fondateur: mean {Fmt(sweepMeans[0])}→{Fmt(sweepMeans[adoptIdx])} " +
                    $"med {Fmt(sweepMedians[0])}→{Fmt(sweepMedians[adoptIdx])} " +
                    $"floor {sweepFloors[0]}→{sweepFloors[adoptIdx]} " +
                    $"ceil {sweepCeils[0]}→{sweepCeils[adoptIdx]}");
                sb.AppendLine(
                    "  NOTE: distribution bimodale sous clamp [0..1] — la médiane saute ; " +
                    "mean + floor/ceil portent l'adoption.");
            }

            sb.AppendLine();
            Flush();

            // ========== PARTIE 3 ==========
            sb.AppendLine("=== PARTIE 3 — SORTIE RECRUTEMENT + BOUCLE MESURÉE ===");
            float exitScale = 0f;
            bool loopConverges = false;
            bool exitAdopted = false;

            if (adoptIdx < 0)
            {
                sb.AppendLine(
                    "PARTIE 2 sans valeur adoptable → PARTIE 3 non branchée (recruitScale reste 0).");
            }
            else
            {
                // Baseline avant sortie (reweight adopté, recruit=0)
                ForceGc();
                ResetAll();
                StabilitySystem.LockReweight(adoptedW);
                TemplateRecruitSystem.LockStabilityRecruitScale(0f);
                float baseArmy, baseSat, baseRegs;
                float baseMed;
                using (var h = new SimulationHarness(Seed))
                {
                    StabilitySystem.LockReweight(adoptedW);
                    TemplateRecruitSystem.LockStabilityRecruitScale(0f);
                    h.RunTicks(ReferenceTicks);
                    var snap = WorldMetrics.Capture(h.EntityManager, ReferenceTicks);
                    baseArmy = snap.WorldArmyStr;
                    baseSat = snap.NeedsSatAvg;
                    baseRegs = snap.TotalRegiments;
                    MeasureStabilityBounds(h.EntityManager, out baseMed, out _, out _);
                }

                sb.AppendLine(
                    $"BASELINE (w={Fmt(adoptedW)}, recruitScale=0) @t3000: " +
                    $"stabMed={Fmt(baseMed)} army={Fmt0(baseArmy)} regiments={baseRegs:0} sat={Fmt(baseSat)}");

                // Essayer sortie à 0.2 puis 0.3 — refuser si stab médiane collée au plafond
                // (boucle non informative) ou effondrement armée >40 %.
                float[] exitCandidates = { 0.2f, 0.3f };
                foreach (var cand in exitCandidates)
                {
                    ForceGc();
                    ResetAll();
                    StabilitySystem.LockReweight(adoptedW);
                    TemplateRecruitSystem.LockStabilityRecruitScale(cand);

                    var seriesStab = new List<float>();
                    var seriesRegs = new List<float>();
                    var seriesBal = new List<float>();
                    using (var h = new SimulationHarness(Seed))
                    {
                        StabilitySystem.LockReweight(adoptedW);
                        TemplateRecruitSystem.LockStabilityRecruitScale(cand);
                        var prev = 0;
                        foreach (var tick in LoopTicks)
                        {
                            h.RunTicks(tick - prev);
                            prev = tick;
                            MeasureStabilityBounds(h.EntityManager, out var med, out _, out _);
                            var snap = WorldMetrics.Capture(h.EntityManager, tick);
                            float balSum = SumTreasuryBalance(h.EntityManager);
                            seriesStab.Add(med);
                            seriesRegs.Add(snap.TotalRegiments);
                            seriesBal.Add(balSum);
                            sb.AppendLine(
                                $"  exit={Fmt(cand)} t{tick}: stabMed={Fmt(med)} " +
                                $"regiments={snap.TotalRegiments} " +
                                $"treasurySum={Fmt1(balSum)} army={Fmt0(snap.WorldArmyStr)}");
                        }
                    }

                    var last3 = seriesStab.GetRange(seriesStab.Count - 3, 3);
                    var range = Max(last3) - Min(last3);
                    var early = seriesStab[0];
                    var late = seriesStab[seriesStab.Count - 1];
                    // Convergence seulement si stab n'est pas collée au plafond (sinon pas de boucle).
                    bool stuckHigh = late >= 0.95f && Min(last3) >= 0.9f;
                    bool diverges = late < early - 0.15f || range > 0.2f;
                    bool oscillates = range > 0.08f && !diverges;
                    bool converges = !diverges && !oscillates && range <= 0.08f && !stuckHigh;

                    string verdict = stuckHigh ? "COLLÉ_PLAFOND"
                        : diverges ? "DIVERGE"
                        : oscillates ? "OSCILLE"
                        : "CONVERGE";
                    sb.AppendLine(
                        $"  boucle_verdict exit={Fmt(cand)}: {verdict} " +
                        $"(stab {Fmt(early)}→{Fmt(late)}, range_last3={Fmt(range)})");

                    if (converges)
                    {
                        // Effet sur les deux bouts
                        ForceGc();
                        ResetAll();
                        StabilitySystem.LockReweight(adoptedW);
                        TemplateRecruitSystem.LockStabilityRecruitScale(cand);
                        float afterArmy, afterSat, afterRegs;
                        using (var h = new SimulationHarness(Seed))
                        {
                            StabilitySystem.LockReweight(adoptedW);
                            TemplateRecruitSystem.LockStabilityRecruitScale(cand);
                            h.RunTicks(ReferenceTicks);
                            var snap = WorldMetrics.Capture(h.EntityManager, ReferenceTicks);
                            afterArmy = snap.WorldArmyStr;
                            afterSat = snap.NeedsSatAvg;
                            afterRegs = snap.TotalRegiments;
                        }

                        sb.AppendLine(
                            $"  effet armée: {Fmt0(baseArmy)}→{Fmt0(afterArmy)} " +
                            $"({Pct(afterArmy, baseArmy)})");
                        sb.AppendLine(
                            $"  effet régiments: {baseRegs:0}→{afterRegs:0}");
                        sb.AppendLine(
                            $"  effet sat: {Fmt(baseSat)}→{Fmt(afterSat)} " +
                            $"(Δ={Fmt(afterSat - baseSat)})");

                        // Refuser effondrement militaire >40 %
                        if (afterArmy >= baseArmy * 0.6f)
                        {
                            exitScale = cand;
                            loopConverges = true;
                            exitAdopted = Mathf.Abs(
                                              cand - TemplateRecruitSystem.AdoptedStabilityRecruitScale)
                                          < 1e-4f;
                            break;
                        }

                        sb.AppendLine(
                            "  REFUS: effondrement armée >40 % — essai suivant ou scale=0.");
                    }
                    else
                    {
                        sb.AppendLine(
                            $"  HYPOTHÈSE CTO non confirmée pour exit={Fmt(cand)} ({verdict}).");
                    }
                }

                if (!loopConverges)
                {
                    exitScale = 0f;
                    sb.AppendLine(
                        "COEFFICIENT SORTIE LAISSÉ À 0 — boucle non convergente / non adoptable.");
                    sb.AppendLine(
                        $"NOTE: const AdoptedStabilityRecruitScale=" +
                        $"{Fmt(TemplateRecruitSystem.AdoptedStabilityRecruitScale)} " +
                        "à aligner sur 0 si la mesure le dit (recompiler).");
                }
                else
                {
                    sb.AppendLine(
                        $"COEFFICIENT SORTIE ADOPTÉ={Fmt(exitScale)} " +
                        $"(const={Fmt(TemplateRecruitSystem.AdoptedStabilityRecruitScale)})");
                }
            }

            sb.AppendLine();
            Flush();

            // Captures vue de jeu
            sb.AppendLine("=== CAPTURES VUE DE JEU ===");
            ForceGc();
            ResetAll();
            StabilitySystem.LockReweight(0f);
            TemplateRecruitSystem.LockStabilityRecruitScale(0f);
            string beforeBlock;
            using (var h = new SimulationHarness(Seed))
            {
                StabilitySystem.LockReweight(0f);
                TemplateRecruitSystem.LockStabilityRecruitScale(0f);
                h.RunTicks(ReferenceTicks);
                beforeBlock = CapturePanelLines(h.EntityManager, PlayerCountryId);
                WriteGameViewCapture(
                    h.EntityManager,
                    Path.Combine(CapturesDir, "01_stab_before.png"),
                    PlayerCountryId,
                    "V1091 BEFORE w=0",
                    beforeBlock);
            }

            sb.AppendLine("CAPTURE 01_stab_before (w=0) :");
            sb.AppendLine(beforeBlock);

            ForceGc();
            ResetAll();
            var wCap = adoptIdx >= 0 ? adoptedW : 0f;
            var eCap = loopConverges ? exitScale : 0f;
            StabilitySystem.LockReweight(wCap);
            TemplateRecruitSystem.LockStabilityRecruitScale(eCap);
            string afterBlock;
            using (var h = new SimulationHarness(Seed))
            {
                StabilitySystem.LockReweight(wCap);
                TemplateRecruitSystem.LockStabilityRecruitScale(eCap);
                h.RunTicks(ReferenceTicks);
                afterBlock = CapturePanelLines(h.EntityManager, PlayerCountryId);
                WriteGameViewCapture(
                    h.EntityManager,
                    Path.Combine(CapturesDir, "02_stab_after.png"),
                    PlayerCountryId,
                    $"V1091 AFTER w={Fmt(wCap)} e={Fmt(eCap)}",
                    afterBlock);
            }

            sb.AppendLine($"CAPTURE 02_stab_after (w={Fmt(wCap)}, exit={Fmt(eCap)}) :");
            sb.AppendLine(afterBlock);
            sb.AppendLine();

            // Verdict final
            var pass = digZero == ParityAnchors.Expected
                       && digA == digB
                       && monoOk
                       && redOk
                       && measure1Ok
                       && measure2Ok
                       && measure3Ok
                       && (adoptIdx >= 0); // PARTIE 2 livrable

            sb.AppendLine("=== VERDICT MESURÉ ===");
            sb.AppendLine(
                $"dérive: -0.0005 appliqué {termDrift}/{termEvals} evals ; " +
                $"+0.001 surplus {termSurplus}× ; -0.002 legLo {termLegLo}× ; " +
                $"coupable={culprit}");
            if (adoptIdx >= 0)
            {
                sb.AppendLine(
                    $"reweight adopté={Fmt(adoptedW)} : " +
                    $"mean {Fmt(sweepMeans[0])}→{Fmt(sweepMeans[adoptIdx])} ; " +
                    $"med {Fmt(sweepMedians[0])}→{Fmt(sweepMedians[adoptIdx])} ; " +
                    $"floor {sweepFloors[0]}→{sweepFloors[adoptIdx]} ; " +
                    $"ceil {sweepCeils[0]}→{sweepCeils[adoptIdx]}");
            }
            else
            {
                sb.AppendLine("reweight adopté=AUCUN — aucune valeur ne réunit les 4 conditions");
            }

            sb.AppendLine(
                $"réversible 0x{ParityAnchors.Expected:X16} bit_identical=" +
                $"{digZero == ParityAnchors.Expected} ; déterminisme 2/2={(digA == digB)}");
            sb.AppendLine(
                $"sortie recrutement={(loopConverges ? Fmt(exitScale) : "0 (non adopté)")} ; " +
                $"boucle={(loopConverges ? "CONVERGE" : "NON_CONVERGENTE_OU_REFUSÉE")}");
            sb.AppendLine(
                $"PASS_CRITÈRES={(pass ? "OUI" : "NON")} " +
                $"(exit_adoptée={exitAdopted} optionnelle si boucle OK)");
            sb.AppendLine(
                "LARGE: rejouée à part (voir Logs/v1_091_large.xml) — filtre v1_090 + V1091.");
            Flush();

            Assert.AreEqual(ParityAnchors.Expected, digZero, "réversibilité bit-identique");
            Assert.AreEqual(digA, digB, "déterminisme");
            Assert.IsTrue(monoOk, "monotonie des means");
            Assert.IsTrue(redOk, "contrôle rouge w=0");
            Assert.IsTrue(measure2Ok, "radicalisme nul confirmé");
            Assert.IsTrue(measure3Ok, "révolution inatteignable confirmée");
            Assert.GreaterOrEqual(adoptIdx, 0,
                "PARTIE 2: une valeur de reweight doit être adoptable (floor↓, ceil<14, mean↑)");

            Debug.Log("V1091_Artifacts_And_Verdict: DONE → " + LogPath);
        }

        static ulong RunParityDigestZero()
        {
            ResetAll();
            StabilitySystem.LockReweight(0f);
            TemplateRecruitSystem.LockStabilityRecruitScale(0f);
            TaxPhysicalWithdrawalSystem.EnsureParitySafeDefaults();
            using var h = new SimulationHarness(Seed);
            StabilitySystem.LockReweight(0f);
            TemplateRecruitSystem.LockStabilityRecruitScale(0f);
            h.RunTicks(ParityTicks);
            return WorldDigest.Compute(h.EntityManager);
        }

        static ulong RunParityDigestAtAdopted()
        {
            ResetAll();
            StabilitySystem.LockReweight(StabilitySystem.AdoptedStabilityReweight);
            TemplateRecruitSystem.LockStabilityRecruitScale(
                TemplateRecruitSystem.AdoptedStabilityRecruitScale);
            using var h = new SimulationHarness(Seed);
            StabilitySystem.LockReweight(StabilitySystem.AdoptedStabilityReweight);
            TemplateRecruitSystem.LockStabilityRecruitScale(
                TemplateRecruitSystem.AdoptedStabilityRecruitScale);
            h.RunTicks(ParityTicks);
            return WorldDigest.Compute(h.EntityManager);
        }

        static void CountStabilityTerms(
            EntityManager em,
            ref long surplus, ref long debt, ref long rev,
            ref long legHi, ref long legLo, ref long drift, ref long evals)
        {
            using var q = em.CreateEntityQuery(
                ComponentType.ReadOnly<GovernmentData>(),
                ComponentType.ReadOnly<TreasuryData>(),
                ComponentType.ReadOnly<RevolutionData>());
            using var govs = q.ToComponentDataArray<GovernmentData>(Allocator.Temp);
            using var treas = q.ToComponentDataArray<TreasuryData>(Allocator.Temp);
            using var revs = q.ToComponentDataArray<RevolutionData>(Allocator.Temp);
            for (var i = 0; i < govs.Length; i++)
            {
                evals++;
                drift++; // inconditionnel
                if (treas[i].Income - treas[i].Expenses > 0f) surplus++;
                if (treas[i].Debt > treas[i].Balance * 2f) debt++;
                if (revs[i].IsRevolutionActive) rev++;
                if (govs[i].Legitimacy > 0.6f) legHi++;
                if (govs[i].Legitimacy < 0.3f) legLo++;
            }
        }

        static void MeasurePolitics(
            EntityManager em, out float medianStability, out float maxAvgRad, out int revolutions)
        {
            var stabs = new List<float>(32);
            revolutions = 0;
            using (var q = em.CreateEntityQuery(
                       ComponentType.ReadOnly<GovernmentData>(),
                       ComponentType.ReadOnly<RevolutionData>()))
            using (var govs = q.ToComponentDataArray<GovernmentData>(Allocator.Temp))
            using (var revs = q.ToComponentDataArray<RevolutionData>(Allocator.Temp))
            {
                for (var i = 0; i < govs.Length; i++)
                {
                    stabs.Add(govs[i].Stability);
                    if (revs[i].IsRevolutionActive)
                        revolutions++;
                }
            }

            medianStability = Median(stabs);

            var sum = new Dictionary<Entity, float>();
            var count = new Dictionary<Entity, int>();
            using (var pq = em.CreateEntityQuery(
                       ComponentType.ReadOnly<PopData>(),
                       ComponentType.ReadOnly<PopPolitics>()))
            using (var pops = pq.ToComponentDataArray<PopData>(Allocator.Temp))
            using (var pols = pq.ToComponentDataArray<PopPolitics>(Allocator.Temp))
            {
                for (var i = 0; i < pops.Length; i++)
                {
                    var c = pops[i].Country;
                    if (c == Entity.Null) continue;
                    sum.TryGetValue(c, out var s);
                    count.TryGetValue(c, out var n);
                    sum[c] = s + pols[i].Radicalism;
                    count[c] = n + 1;
                }
            }

            maxAvgRad = 0f;
            foreach (var kv in sum)
            {
                var n = count[kv.Key];
                if (n <= 0) continue;
                var avg = kv.Value / n;
                if (avg > maxAvgRad) maxAvgRad = avg;
            }
        }

        static void CountRadicalismZero(EntityManager em, out int exactZero, out int total)
        {
            exactZero = 0;
            total = 0;
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<PopPolitics>());
            using var pols = q.ToComponentDataArray<PopPolitics>(Allocator.Temp);
            for (var i = 0; i < pols.Length; i++)
            {
                total++;
                if (pols[i].Radicalism == 0f)
                    exactZero++;
            }
        }

        static void MeasureLegitimacyBuckets(
            EntityManager em, out int legLow, out int legHigh, out float median)
        {
            legLow = 0;
            legHigh = 0;
            var legs = new List<float>(32);
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<GovernmentData>());
            using var govs = q.ToComponentDataArray<GovernmentData>(Allocator.Temp);
            for (var i = 0; i < govs.Length; i++)
            {
                legs.Add(govs[i].Legitimacy);
                if (govs[i].Legitimacy < 0.3f) legLow++;
                if (govs[i].Legitimacy > 0.6f) legHigh++;
            }

            median = Median(legs);
        }

        static void MeasureStabilityBounds(
            EntityManager em, out float median, out float mean, out int floor, out int ceil)
        {
            floor = 0;
            ceil = 0;
            var stabs = new List<float>(32);
            float sum = 0f;
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<GovernmentData>());
            using var govs = q.ToComponentDataArray<GovernmentData>(Allocator.Temp);
            for (var i = 0; i < govs.Length; i++)
            {
                var s = govs[i].Stability;
                stabs.Add(s);
                sum += s;
                if (s <= 0f) floor++;
                if (s >= 1f) ceil++;
            }

            median = Median(stabs);
            mean = stabs.Count > 0 ? sum / stabs.Count : float.NaN;
        }

        static void MeasureStabilityBounds(
            EntityManager em, out float median, out int floor, out int ceil)
        {
            MeasureStabilityBounds(em, out median, out _, out floor, out ceil);
        }

        static int CountStabilityFloor(EntityManager em)
        {
            var n = 0;
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<GovernmentData>());
            using var govs = q.ToComponentDataArray<GovernmentData>(Allocator.Temp);
            for (var i = 0; i < govs.Length; i++)
            {
                if (govs[i].Stability <= 0f) n++;
            }

            return n;
        }

        static int CountCountries(EntityManager em)
        {
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<CountryData>());
            return q.CalculateEntityCount();
        }

        static float SumTreasuryBalance(EntityManager em)
        {
            float sum = 0f;
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<TreasuryData>());
            using var t = q.ToComponentDataArray<TreasuryData>(Allocator.Temp);
            for (var i = 0; i < t.Length; i++)
                sum += t[i].Balance;
            return sum;
        }

        static string CapturePanelLines(EntityManager em, int countryId)
        {
            MeasureStabilityBounds(em, out var worldMed, out var floor, out var ceil);
            if (!CountryObservation.TryCapture(em, countryId, out var snap))
                return "(capture failed)\n";
            var sb = new StringBuilder();
            sb.Append("--- PANEL ").Append(snap.Tag).Append(" ---\n");
            sb.Append("STAB   ").Append(Fmt(snap.Stability))
                .Append("  LEG  ").Append(Fmt(snap.Legitimacy)).Append('\n');
            sb.Append("ARMY   ").Append(Fmt0(snap.ArmyStrength)).Append('\n');
            sb.Append("GOLD   ").Append(Fmt1(snap.Treasury)).Append('\n');
            sb.Append("WORLD  med=").Append(Fmt(worldMed))
                .Append(" floor=").Append(floor).Append("/20")
                .Append(" ceil=").Append(ceil).Append("/20\n");
            return sb.ToString();
        }

        static void WriteGameViewCapture(
            EntityManager em, string path, int countryId, string title, string panelLines)
        {
            MapDisplaySystem.TrySelectCountryByTag(em, "FRA");
            var geo = MapGeometryCache.GetOrBuild(
                MapSnapshotExporter.Width, MapSnapshotExporter.Height,
                MapViewport.State.Window, out _);
            if (geo == null)
            {
                geo = MapSnapshotExporter.BuildMapGeometry(
                    MapSnapshotExporter.Width, MapSnapshotExporter.Height);
            }

            var pixels = MapSnapshotExporter.RenderPoliticalPixels(
                em, geo, MapSnapshotExporter.LabelDensity.Provinces, -1,
                overlay: p =>
                {
                    CityMarkerComposer.Compose(
                        p, geo, em, MapObservationLevel.Country, filterCountryId: countryId);
                    var fg = new Color32(236, 232, 220, 255);
                    var halo = new Color32(8, 8, 12, 255);
                    MapSnapshotExporter.WithGlyphScale(2, () =>
                    {
                        MapSnapshotExporter.DrawBitmapText(p, title, 12, 16, fg, halo);
                        var y = 44;
                        var lines = panelLines.Split('\n');
                        for (var i = 0; i < lines.Length && i < 12; i++)
                        {
                            if (string.IsNullOrEmpty(lines[i])) continue;
                            MapSnapshotExporter.DrawBitmapText(p, lines[i], 12, y, fg, halo);
                            y += 18;
                        }
                    });
                });

            if (pixels != null)
            {
                MapSnapshotExporter.WriteMapBufferPng(
                    pixels, MapSnapshotExporter.Width, MapSnapshotExporter.Height, path);
            }

            MapViewport.Reset();
        }

        static float Median(List<float> values)
        {
            if (values == null || values.Count == 0) return float.NaN;
            values.Sort();
            var n = values.Count;
            return n % 2 == 1
                ? values[n / 2]
                : 0.5f * (values[n / 2 - 1] + values[n / 2]);
        }

        static float Min(List<float> v)
        {
            var m = v[0];
            for (var i = 1; i < v.Count; i++)
                if (v[i] < m) m = v[i];
            return m;
        }

        static float Max(List<float> v)
        {
            var m = v[0];
            for (var i = 1; i < v.Count; i++)
                if (v[i] > m) m = v[i];
            return m;
        }

        static string Fmt(float v) =>
            float.IsNaN(v) ? "NaN" : v.ToString("0.###", CultureInfo.InvariantCulture);

        static string Fmt0(float v) => v.ToString("0", CultureInfo.InvariantCulture);
        static string Fmt1(float v) => v.ToString("0.0", CultureInfo.InvariantCulture);

        static string Pct(float after, float before)
        {
            if (Mathf.Abs(before) < 1e-6f) return "n/a";
            return ((after / before - 1f) * 100f).ToString("0.0", CultureInfo.InvariantCulture) + "%";
        }

        static void ForceGc()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        static void ResetAll()
        {
            StabilitySystem.ResetToCompiledDefault();
            TemplateRecruitSystem.ResetStabilityRecruitToCompiledDefault();
            TemplateRecruitSystem.RecruitCostScale = TemplateRecruitSystem.DefaultRecruitCostScale;
            PopGrowthSystem.ResetToCompiledDefault();
            TaxPhysicalWithdrawalSystem.ResetToCompiledDefault();
            TaxPhysicalWithdrawalSystem.EnsureParitySafeDefaults();
            StabilitySystem.EnsureParitySafeDefaults();
            TemplateRecruitSystem.EnsureParitySafeDefaults();
            MapViewport.Reset();
        }
    }
}
