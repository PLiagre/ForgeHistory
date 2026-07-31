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
using VictoriaGame.Politics;
using VictoriaGame.Population;
using VictoriaGame.Presentation;
using VictoriaGame.World;

namespace VictoriaGame.Tests
{
    /// <summary>Batch : -executeMethod VictoriaGame.Tests.V1075BatchRunner.Run</summary>
    public static class V1075BatchRunner
    {
        public static void Run()
        {
            try
            {
                V1075TaxPhysicalWithdrawalTests.RunSweepAndWriteLog();
                UnityEngine.Debug.Log("V1075BatchRunner: DONE");
            }
            catch (Exception ex) when (HarnessAllocationGuard.IsNativeAllocationFailure(ex))
            {
                UnityEngine.Debug.LogWarning(
                    "V1075BatchRunner: ALLOCATION_FAILURE (charge harnais) — " + ex.Message);
                UnityEngine.Debug.Log("V1075BatchRunner: DONE_PARTIAL");
            }
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
        }
    }

    /// <summary>
    /// v1_075 — bruit, bit-identité, retrait abstrait (lodSat), monotonie, horizon encadré.
    /// Balayage calibration : BatchRunner uniquement (jamais [Test] EditMode).
    /// </summary>
    [TestFixture]
    public class V1075TaxPhysicalWithdrawalTests
    {
        const uint Seed = 42195u;
        const int NoiseReps = 5;
        const int NoiseTicks = 800;

        static readonly float[] TaxMultipliers = { 0f, 0.5f, 1f, 5f, 10f };
        static readonly float[] PhysCoeffs = { 0f, 0.5f };
        /// <summary>cAbs=1 à tax10× affame le monde (mesuré) — exclu de la grille lourde.</summary>
        static readonly float[] AbsCoeffs = { 0f, 0.5f };

        /// <summary>Point PROPOSÉ (non adopté) — affiné par le batch.</summary>
        public const float ProposedPhysicalCoefficient = 0.5f;
        public const float ProposedAbstractCoefficient = 0.5f;

        /// <summary>Horizon qui sépare — mesuré v1_075_tax_pops.log (t300).</summary>
        public static int ProposedGuardHorizonTicks = 300;

        /// <summary>Adjacent INFÉRIEUR qui ne sépare plus — mesuré (t250).</summary>
        public static int ProposedGuardBelowHorizonTicks = 250;

        [TearDown]
        public void TearDown()
        {
            ResetAll();
        }

        [Test]
        public void V1075_Defaults_Are_Zero_Both_Layers()
        {
            Assert.AreEqual(0f, TaxPhysicalWithdrawalSystem.DefaultWithdrawalCoefficient, 1e-12f);
            Assert.AreEqual(0f, TaxPhysicalWithdrawalSystem.DefaultAbstractWithdrawalCoefficient, 1e-12f);
            TaxPhysicalWithdrawalSystem.ResetToCompiledDefault();
            Assert.AreEqual(0f, TaxPhysicalWithdrawalSystem.WithdrawalCoefficient, 1e-12f);
            Assert.AreEqual(0f, TaxPhysicalWithdrawalSystem.AbstractWithdrawalCoefficient, 1e-12f);
        }

        [Test]
        public void V1075_Coefficients_Zero_BitIdentical_Digests()
        {
            LockHarnessBaseline();
            TaxPhysicalWithdrawalSystem.LockCoefficients(0f, 0f);

            var digA = RunStockDigest(0f, 0f, TaxPolicyLimits.MaxProductionTaxRate, 200);
            var digB = RunStockDigest(0f, 0f, TaxPolicyLimits.MaxProductionTaxRate, 200);
            var mktA = RunMarketDigest(0f, 0f, TaxPolicyLimits.MaxProductionTaxRate, 200);
            var mktB = RunMarketDigest(0f, 0f, TaxPolicyLimits.MaxProductionTaxRate, 200);

            Assert.AreEqual(digA, digB,
                $"digest stocks phys non déterministe ou mécanisme touche à c=0: {digA:X16} vs {digB:X16}");
            Assert.AreEqual(mktA, mktB,
                $"digest marché abstrait non déterministe: {mktA:X16} vs {mktB:X16}");
        }

        [Test]
        public void V1075_Abstract_Positive_Withdraws_From_Lod_Supply()
        {
            LockHarnessBaseline();
            double absZero, absFull;

            TaxPhysicalWithdrawalSystem.LockCoefficients(0f, 0f);
            using (var h = new SimulationHarness(Seed))
            {
                h.RunTicks(0);
                SetAllTaxRates(h.EntityManager, TaxPolicyLimits.MaxProductionTaxRate);
                TaxPhysicalWithdrawalSystem.ResetSessionTotals();
                h.RunTicks(50);
                absZero = TaxPhysicalWithdrawalSystem.SessionAbstractWithdrawn;
            }

            TaxPhysicalWithdrawalSystem.LockCoefficients(0f, 1f);
            using (var h = new SimulationHarness(Seed))
            {
                h.RunTicks(0);
                SetAllTaxRates(h.EntityManager, TaxPolicyLimits.MaxProductionTaxRate);
                TaxPhysicalWithdrawalSystem.ResetSessionTotals();
                h.RunTicks(50);
                absFull = TaxPhysicalWithdrawalSystem.SessionAbstractWithdrawn;
            }

            Assert.AreEqual(0.0, absZero, 1e-9, "cAbs=0 ne doit rien retirer côté LOD");
            Assert.Greater(absFull, 0.0, "cAbs=1 au taux max doit retirer de l'offre LOD");
        }

        [Test]
        public void V1075_Physical_Conservation_Holds_At_Proposed()
        {
            LockHarnessBaseline();
            TaxPhysicalWithdrawalSystem.LockCoefficients(
                ProposedPhysicalCoefficient, ProposedAbstractCoefficient);

            using var h = new SimulationHarness(Seed);
            h.RunTicks(0);
            SetAllTaxRates(h.EntityManager, TaxPolicyLimits.DefaultProductionTaxRate * 10f);
            h.RunTicks(100);

            var metrics = GetPhysicalMetrics(h.EntityManager);
            Assert.IsTrue(
                PhysicalConservationGate.PerTickHolds(metrics),
                $"Conservation per-tick cassée: drift={metrics.MaxTickConservationDrift}");
            Assert.Greater(TaxPhysicalWithdrawalSystem.SessionWithdrawn, 0.0);
            Assert.Greater(TaxPhysicalWithdrawalSystem.SessionAbstractWithdrawn, 0.0);
        }

        [Test]
        public void V1075_ProposedPoint_Guard()
        {
            Assert.IsTrue(
                TryProposedPointGuard(
                    ProposedGuardHorizonTicks,
                    ProposedPhysicalCoefficient,
                    ProposedAbstractCoefficient,
                    out var detail),
                detail);
        }

        [Test]
        public void V1075_ProposedPoint_Guard_Below_Horizon_Does_Not_Separate()
        {
            Assert.IsFalse(
                TryProposedPointGuard(
                    ProposedGuardBelowHorizonTicks,
                    ProposedPhysicalCoefficient,
                    ProposedAbstractCoefficient,
                    out var detail),
                "Sous l'horizon, le garde-fou ne doit pas encore séparer: " + detail);
        }

        public static void RunSweepAndWriteLog()
        {
            var logsDir = Path.Combine(UnityEngine.Application.dataPath, "..", "Logs");
            Directory.CreateDirectory(logsDir);
            var path = Path.Combine(logsDir, "v1_075_tax_pops.log");
            var sb = new StringBuilder(512 * 1024);

            void Flush() => File.WriteAllText(path, sb.ToString());

            sb.AppendLine("=== v1_075 TAX PHYSICAL+ABSTRACT WITHDRAWAL — seed=42195 ===");
            sb.AppendLine(
                "CÔTÉS DU RETRAIT: (1) ProvinceStock qty=LastOutput×rate×yield×cPhys ; " +
                "(2) offre LOD share=(rate/MaxRate)×yield×cAbs — le taux monétaire 2e-5 " +
                "laisserait lodSat inerte en flux ; la normalisation par MaxRate convertit " +
                "le levier fiscal en part de marchandises.");
            sb.AppendLine(
                "c=0 no-op bit-identique par couche ; ledger physique = consommation ; " +
                "abstrait : requested=withdrawn (plafond LastOutput).");
            sb.AppendLine(
                $"Default cPhys={TaxPhysicalWithdrawalSystem.DefaultWithdrawalCoefficient} " +
                $"cAbs={TaxPhysicalWithdrawalSystem.DefaultAbstractWithdrawalCoefficient} " +
                "(NON ADOPTÉS). PhysicalBlendWeight=" +
                $"{PhysicalSatisfactionBlendSystem.DefaultPhysicalBlendWeight} (NON modifié).");
            sb.AppendLine();
            Flush();

            LockHarnessBaseline();

            // ----- PARTIE 1 — BRUIT -----
            sb.AppendLine("=== PARTIE 1 — BRUIT À CONFIGURATION IDENTIQUE ===");
            sb.AppendLine(
                $"config: cPhys=0.5 cAbs=0.5 tax=1× w=0.25 seed={Seed} t={NoiseTicks} reps={NoiseReps}");
            var noise = MeasureNoise(0.5f, 0.5f, 1f, NoiseTicks, NoiseReps, sb);
            Flush();

            if (!noise.DeterministicEnough)
            {
                sb.AppendLine(
                    "DÉTERMINISME: répétitions non identiques — σ(sat)>0. " +
                    "Priorité absolue : chercher la source avant toute conclusion de canal.");
            }

            sb.AppendLine();
            Flush();

            // ----- PARTIE 2 — BIT-IDENTITÉ -----
            sb.AppendLine("=== PARTIE 2 — BIT-IDENTITÉ @cPhys=0 cAbs=0 ===");
            sb.AppendLine(
                "Instrument: digest stocks agrégé (ProvinceId,GoodId) + digest MarketPrice " +
                "(GoodId) — jamais Entity.Index. Emplacements GoodId≤0 ignorés.");
            ForceGc();
            LockHarnessBaseline();
            var dig0a = RunStockDigest(0f, 0f, TaxPolicyLimits.DefaultProductionTaxRate, 200);
            var dig0b = RunStockDigest(0f, 0f, TaxPolicyLimits.DefaultProductionTaxRate, 200);
            var mkt0a = RunMarketDigest(0f, 0f, TaxPolicyLimits.DefaultProductionTaxRate, 200);
            var mkt0b = RunMarketDigest(0f, 0f, TaxPolicyLimits.DefaultProductionTaxRate, 200);
            var bitOk = dig0a == dig0b && mkt0a == mkt0b;
            sb.AppendLine(
                $"c=0/0 w=0.25 t200 stockA={dig0a:X16} stockB={dig0b:X16} " +
                $"mktA={mkt0a:X16} mktB={mkt0b:X16} bitIdentical={bitOk}");
            if (bitOk)
            {
                sb.AppendLine(
                    "VERDICT PARTIE 2: PASS — digests égaux à coefficient nul des deux côtés. " +
                    "La non-identité v1_065 venait de l'instrument (parcours/agrégation), pas d'un " +
                    "toucher à c=0 (mécanisme no-op strict conservé).");
            }
            else
            {
                sb.AppendLine(
                    "VERDICT PARTIE 2: FAIL — digests encore différents à c=0. " +
                    "Réversibilité non établie ; ne pas adopter.");
            }

            ForceGc();
            LockHarnessBaseline();
            var cap0 = CaptureAt(0f, 0f, 0f, 200);
            ForceGc();
            LockHarnessBaseline();
            var cap0b = CaptureAt(0f, 0f, TaxPolicyLimits.MaxProductionTaxRate, 200);
            sb.AppendLine(
                $"c=0/0: sat@tax0={Fmt3(cap0.Sat)} sat@tax10x={Fmt3(cap0b.Sat)} " +
                $"Δsat={Fmt4(cap0b.Sat - cap0.Sat)} withdrawnPhys={FmtD(cap0b.WithdrawnPhys)} " +
                $"withdrawnAbs={FmtD(cap0b.WithdrawnAbs)}");
            sb.AppendLine();
            Flush();

            // Conservation
            sb.AppendLine("=== BILAN CONSERVATION PAR COUCHE ===");
            ForceGc();
            LockHarnessBaseline();
            TaxPhysicalWithdrawalSystem.LockCoefficients(1f, 1f);
            TaxPhysicalWithdrawalSystem.ResetSessionTotals();
            float maxDrift;
            bool consOk;
            using (var h = new SimulationHarness(Seed))
            {
                h.RunTicks(0);
                SetAllTaxRates(h.EntityManager, TaxPolicyLimits.MaxProductionTaxRate);
                TaxPhysicalWithdrawalSystem.ResetSessionTotals();
                h.RunTicks(300);
                var metrics = GetPhysicalMetrics(h.EntityManager);
                maxDrift = metrics.MaxTickConservationDrift;
                consOk = PhysicalConservationGate.PerTickHolds(metrics);
                sb.AppendLine(
                    $"physique c=1 tax10x t300: withdrawn={FmtD(TaxPhysicalWithdrawalSystem.SessionWithdrawn)} " +
                    $"requested={FmtD(TaxPhysicalWithdrawalSystem.SessionRequested)} " +
                    $"maxTickDrift={Fmt3(maxDrift)} consOk={consOk}");
                sb.AppendLine(
                    $"abstrait cAbs=1 tax10x t300: withdrawn={FmtD(TaxPhysicalWithdrawalSystem.SessionAbstractWithdrawn)} " +
                    $"requested={FmtD(TaxPhysicalWithdrawalSystem.SessionAbstractRequested)} " +
                    $"consOkAbs={(Math.Abs(TaxPhysicalWithdrawalSystem.SessionAbstractWithdrawn - TaxPhysicalWithdrawalSystem.SessionAbstractRequested) < 1e-6)}");
            }

            sb.AppendLine();
            Flush();

            // ----- PARTIE 3 — TABLEAU CROISÉ -----
            sb.AppendLine("=== PARTIE 3 — TABLEAU CROISÉ taux × cPhys × cAbs (w=0.25, t1200) ===");
            sb.AppendLine(
                "cPhys | cAbs | mult | rate | tick | debt | bankrupt | sat | physSat | lodSat | pop | army | wPhys | wAbs | hungry");

            const int SweepTicks = 1200;
            var cells = new Dictionary<string, SweepCell>();
            var cellsOk = 0;

            for (var pi = 0; pi < PhysCoeffs.Length; pi++)
            for (var ai = 0; ai < AbsCoeffs.Length; ai++)
            for (var ti = 0; ti < TaxMultipliers.Length; ti++)
            {
                if (!TryRunCell(PhysCoeffs[pi], AbsCoeffs[ai], TaxMultipliers[ti], SweepTicks,
                        out var cell, out var err))
                {
                    sb.AppendLine(
                        $"ALLOC_FAIL cPhys={Fmt2(PhysCoeffs[pi])} cAbs={Fmt2(AbsCoeffs[ai])} " +
                        $"×{Fmt2(TaxMultipliers[ti])}: {err}");
                    Flush();
                    ForceGc();
                    LockHarnessBaseline();
                    continue;
                }

                cells[CellKey(PhysCoeffs[pi], AbsCoeffs[ai], TaxMultipliers[ti])] = cell;
                AppendCell(sb, cell);
                cellsOk++;
                Flush();
                ForceGc();
                LockHarnessBaseline();
            }

            sb.AppendLine($"cells_ok={cellsOk}");
            sb.AppendLine();

            // Monotonie + effet/bruit — choisir le plus petit (cPhys,cAbs) vivant.
            sb.AppendLine("=== MONOTONIE SAT SUR GRILLE DE TAUX + EFFET/BRUIT ===");
            float proposedPhys = ProposedPhysicalCoefficient;
            float proposedAbs = ProposedAbstractCoefficient;
            bool monoOk = false;
            bool effectAboveNoise = false;
            float bestDelta = 0f;
            var foundProposal = false;

            foreach (var cPhys in PhysCoeffs)
            foreach (var cAbs in AbsCoeffs)
            {
                if (cPhys <= 0f && cAbs <= 0f)
                {
                    sb.AppendLine($"cPhys={Fmt2(cPhys)} cAbs={Fmt2(cAbs)}: ancre (Δ attendu ~0)");
                    continue;
                }

                var sats = new float[TaxMultipliers.Length];
                var lods = new float[TaxMultipliers.Length];
                var phys = new float[TaxMultipliers.Length];
                var complete = true;
                SweepCell mid = default;
                SweepCell high = default;
                for (var ti = 0; ti < TaxMultipliers.Length; ti++)
                {
                    if (!cells.TryGetValue(CellKey(cPhys, cAbs, TaxMultipliers[ti]), out var cell) ||
                        cell.Ticks <= 0)
                    {
                        complete = false;
                        break;
                    }

                    sats[ti] = cell.Sat;
                    lods[ti] = cell.LodSat;
                    phys[ti] = cell.PhysSat;
                    if (Math.Abs(TaxMultipliers[ti] - 1f) < 1e-6f)
                        mid = cell;
                    if (Math.Abs(TaxMultipliers[ti] - 10f) < 1e-6f)
                        high = cell;
                }

                if (!complete)
                {
                    sb.AppendLine($"cPhys={Fmt2(cPhys)} cAbs={Fmt2(cAbs)}: (incomplet)");
                    continue;
                }

                var mono = IsMonotoneNonIncreasing(sats);
                var dSat = sats[sats.Length - 1] - sats[0];
                var dLod = lods[lods.Length - 1] - lods[0];
                var dPhys = phys[phys.Length - 1] - phys[0];
                var ratio = noise.SigmaSat > 1e-9f ? Math.Abs(dSat) / noise.SigmaSat : float.PositiveInfinity;
                var above = Math.Abs(dSat) > Math.Max(noise.SigmaSat * 2f, 0.005f) &&
                            Math.Abs(dSat) >= noise.RangeSat;
                var alive = mid.Ticks > 0 && mid.Army > 1000f && mid.Bankrupt < 10 &&
                            mid.Pop > 100000 && mid.Sat > 0.35f &&
                            high.Ticks > 0 && high.Pop > 80000 && high.Sat > 0.25f;
                sb.AppendLine(
                    $"cPhys={Fmt2(cPhys)} cAbs={Fmt2(cAbs)}: sat [{string.Join("→", Arr3(sats))}] " +
                    $"mono={mono} Δsat={Fmt4(dSat)} ΔlodSat={Fmt4(dLod)} ΔphysSat={Fmt4(dPhys)} " +
                    $"effet/bruit={Fmt2(ratio)} aboveNoise={above} alive={alive}");

                if (mono && above && dSat < 0f && alive)
                {
                    // Préférer les deux couches actives, puis plus petit cAbs, puis cPhys.
                    var both = cPhys > 0f && cAbs > 0f;
                    var bestBoth = proposedPhys > 0f && proposedAbs > 0f;
                    var better = !foundProposal ||
                                 (both && !bestBoth) ||
                                 (both == bestBoth && cAbs < proposedAbs - 1e-6f) ||
                                 (both == bestBoth && Math.Abs(cAbs - proposedAbs) < 1e-6f &&
                                  cPhys < proposedPhys - 1e-6f);
                    if (better)
                    {
                        foundProposal = true;
                        monoOk = true;
                        effectAboveNoise = true;
                        bestDelta = dSat;
                        proposedPhys = cPhys;
                        proposedAbs = cAbs;
                    }
                }
            }

            if (!foundProposal)
            {
                sb.AppendLine(
                    "Aucun couple (cPhys,cAbs) monotone + aboveNoise + monde vivant — " +
                    "repli proposition sur constantes ; VERDICT attendu FAIL.");
            }
            else
            {
                sb.AppendLine(
                    $"Choix proposition: plus petit cAbs vivant avec canal monotone → " +
                    $"cPhys={Fmt2(proposedPhys)} cAbs={Fmt2(proposedAbs)} Δsat={Fmt4(bestDelta)}.");
            }

            sb.AppendLine();
            Flush();

            // ----- PARTIE 4 — HORIZON ENCADRÉ -----
            sb.AppendLine("=== PARTIE 4 — HORIZON GARDE-FOU ENCADRÉ DES DEUX CÔTÉS ===");
            sb.AppendLine("INTERDIT de conclure depuis le bord — descendre sous t400.");
            // GC agressif avant la série d'horizons (fuite harnais constatée en 1re passe).
            for (var g = 0; g < 3; g++)
            {
                ForceGc();
            }

            LockHarnessBaseline();
            int[] horizons = { 100, 150, 200, 250, 300, 350, 400, 500, 600, 800 };
            int separateAt = -1;
            int belowAt = -1;
            for (var i = 0; i < horizons.Length; i++)
            {
                try
                {
                    var ok = TryProposedPointGuard(
                        horizons[i], proposedPhys, proposedAbs, out var detail);
                    sb.AppendLine($"t={horizons[i]} separate={ok} | {detail}");
                    if (ok)
                    {
                        if (separateAt < 0)
                            separateAt = horizons[i];
                    }
                    else if (separateAt < 0)
                    {
                        belowAt = horizons[i];
                    }
                }
                catch (Exception ex) when (HarnessAllocationGuard.IsNativeAllocationFailure(ex))
                {
                    sb.AppendLine($"t={horizons[i]} ALLOC_FAIL: {ex.Message}");
                    ForceGc();
                    LockHarnessBaseline();
                    continue;
                }

                Flush();
                ForceGc();
                LockHarnessBaseline();
            }

            if (separateAt > 0 && belowAt >= 0 && belowAt < separateAt)
            {
                sb.AppendLine(
                    $"HORIZON ENCADRÉ: t{separateAt} sépare ; t{belowAt} ne sépare plus. " +
                    $"Garde-fou EditMode = t{separateAt} ; non-séparation = t{belowAt}.");
                ProposedGuardHorizonTicks = separateAt;
                ProposedGuardBelowHorizonTicks = belowAt;
            }
            else if (separateAt > 0)
            {
                sb.AppendLine(
                    $"HORIZON INCOMPLET: t{separateAt} sépare mais pas de borne inférieure trouvée " +
                    "dans la grille — FAIL critère encadrement.");
            }
            else
            {
                sb.AppendLine("HORIZON: AUCUNE séparation — proposition à revoir.");
            }

            sb.AppendLine();
            sb.AppendLine("=== VALEURS PROPOSÉES (NON ADOPTÉES) ===");
            sb.AppendLine(
                $"cPhys={Fmt2(proposedPhys)} cAbs={Fmt2(proposedAbs)} — PhysicalBlendWeight inchangé " +
                $"à {PhysicalSatisfactionBlendSystem.DefaultPhysicalBlendWeight}.");
            if (cells.TryGetValue(CellKey(proposedPhys, proposedAbs, 0f), out var vLow) &&
                cells.TryGetValue(CellKey(proposedPhys, proposedAbs, 10f), out var vHigh))
            {
                sb.AppendLine(
                    $"Coût mesuré @t{SweepTicks}: sat {Fmt3(vLow.Sat)}→{Fmt3(vHigh.Sat)} " +
                    $"(Δ={Fmt4(vHigh.Sat - vLow.Sat)}) ; lodSat {Fmt3(vLow.LodSat)}→{Fmt3(vHigh.LodSat)} ; " +
                    $"physSat {Fmt3(vLow.PhysSat)}→{Fmt3(vHigh.PhysSat)} ; " +
                    $"dette {Fmt1(vLow.Debt)}→{Fmt1(vHigh.Debt)} ; pop {vLow.Pop}→{vHigh.Pop}.");
            }

            sb.AppendLine();
            sb.AppendLine("=== VERDICT MESURÉ ===");
            var pass = bitOk && consOk && monoOk && effectAboveNoise &&
                       separateAt > 0 && belowAt >= 0 && belowAt < separateAt;
            sb.AppendLine(
                $"bruit σ(sat)={Fmt4(noise.SigmaSat)} étendue={Fmt4(noise.RangeSat)} sur {NoiseReps} reps ; " +
                $"digests égaux à c=0={bitOk} ; conservation phys maxDrift={Fmt3(maxDrift)} consOk={consOk} ; " +
                $"monotonie+effet>bruit={monoOk && effectAboveNoise} Δsat={Fmt4(bestDelta)} ; " +
                $"horizon t{separateAt} sépare / t{belowAt} ne sépare plus ; " +
                $"PROPOSÉ cPhys={Fmt2(proposedPhys)} cAbs={Fmt2(proposedAbs)} non adopté ; " +
                $"PhysicalBlendWeight inchangé.");
            sb.AppendLine(pass
                ? "VERDICT: PASS — canal monotone au-dessus du bruit, bit-identité, conservation, horizon encadré."
                : "VERDICT: FAIL — un ou plusieurs critères non atteints (voir sections). Droit explicite d'écrire FAIL.");

            Flush();
            UnityEngine.Debug.Log(sb.ToString());
            ResetAll();
        }

        static NoiseStats MeasureNoise(
            float cPhys, float cAbs, float taxMult, int ticks, int reps, StringBuilder sb)
        {
            var sats = new float[reps];
            var phys = new float[reps];
            var debts = new float[reps];
            var pops = new int[reps];

            for (var r = 0; r < reps; r++)
            {
                ForceGc();
                LockHarnessBaseline();
                var cell = RunCell(cPhys, cAbs, taxMult, ticks);
                sats[r] = cell.Sat;
                phys[r] = cell.PhysSat;
                debts[r] = cell.Debt;
                pops[r] = cell.Pop;
                sb.AppendLine(
                    $"  rep{r + 1}: sat={Fmt3(cell.Sat)} physSat={Fmt3(cell.PhysSat)} " +
                    $"lodSat={Fmt3(cell.LodSat)} debt={Fmt1(cell.Debt)} pop={cell.Pop}");
            }

            var stats = new NoiseStats
            {
                SigmaSat = Stdev(sats),
                RangeSat = Range(sats),
                SigmaPhys = Stdev(phys),
                RangePhys = Range(phys),
                SigmaDebt = Stdev(debts),
                RangeDebt = Range(debts),
                SigmaPop = StdevInt(pops),
                RangePop = RangeInt(pops)
            };
            stats.DeterministicEnough = stats.SigmaSat < 1e-5f && stats.RangeSat < 1e-4f;
            sb.AppendLine(
                $"σ(sat)={Fmt4(stats.SigmaSat)} étendue(sat)={Fmt4(stats.RangeSat)} ; " +
                $"σ(physSat)={Fmt4(stats.SigmaPhys)} étendue={Fmt4(stats.RangePhys)} ; " +
                $"σ(debt)={Fmt2(stats.SigmaDebt)} étendue={Fmt1(stats.RangeDebt)} ; " +
                $"σ(pop)={Fmt2(stats.SigmaPop)} étendue={stats.RangePop} ; " +
                $"reps_identiques={stats.DeterministicEnough}");
            return stats;
        }

        public static bool TryProposedPointGuard(
            int ticks, float cPhys, float cAbs, out string detail)
        {
            detail = "";
            LockHarnessBaseline();
            TaxPhysicalWithdrawalSystem.LockCoefficients(cPhys, cAbs);

            float satZero, satHigh, debtZero, debtHigh, physZero, physHigh, lodZero, lodHigh;
            using (var h = new SimulationHarness(Seed))
            {
                TaxPhysicalWithdrawalSystem.LockCoefficients(cPhys, cAbs);
                PhysicalSatisfactionBlendSystem.LockWeight(0.25f);
                h.RunTicks(0);
                SetAllTaxRates(h.EntityManager, 0f);
                h.RunTicks(ticks);
                var m = WorldMetrics.Capture(h.EntityManager, ticks);
                satZero = m.NeedsSatAvg;
                debtZero = m.TotalDebt;
                physZero = MeanPhysicalSatisfaction(h.EntityManager);
                lodZero = MeanLodSatisfaction(h.EntityManager);
            }

            using (var h = new SimulationHarness(Seed))
            {
                TaxPhysicalWithdrawalSystem.LockCoefficients(cPhys, cAbs);
                PhysicalSatisfactionBlendSystem.LockWeight(0.25f);
                h.RunTicks(0);
                SetAllTaxRates(h.EntityManager, TaxPolicyLimits.MaxProductionTaxRate);
                h.RunTicks(ticks);
                var m = WorldMetrics.Capture(h.EntityManager, ticks);
                satHigh = m.NeedsSatAvg;
                debtHigh = m.TotalDebt;
                physHigh = MeanPhysicalSatisfaction(h.EntityManager);
                lodHigh = MeanLodSatisfaction(h.EntityManager);
            }

            var satSeparates = satHigh < satZero - 0.008f ||
                               lodHigh < lodZero - 0.008f ||
                               physHigh < physZero - 0.008f;
            var debtSeparates = debtHigh < debtZero - 50f || debtHigh < debtZero * 0.8f;
            detail =
                $"t={ticks} cPhys={cPhys:0.##} cAbs={cAbs:0.##} " +
                $"sat0={satZero:0.000} sat10x={satHigh:0.000} " +
                $"lod0={lodZero:0.000} lod10x={lodHigh:0.000} " +
                $"phys0={physZero:0.000} phys10x={physHigh:0.000} " +
                $"debt0={debtZero:0.0} debt10x={debtHigh:0.0} " +
                $"satSep={satSeparates} debtSep={debtSeparates}";
            return satSeparates && debtSeparates;
        }

        static bool TryRunCell(
            float cPhys, float cAbs, float taxMult, int ticks,
            out SweepCell cell, out string error)
        {
            cell = default;
            error = null;
            try
            {
                cell = RunCell(cPhys, cAbs, taxMult, ticks);
                return true;
            }
            catch (Exception ex) when (HarnessAllocationGuard.IsNativeAllocationFailure(ex))
            {
                error = ex.Message;
                ForceGc();
                return false;
            }
        }

        static SweepCell RunCell(float cPhys, float cAbs, float taxMult, int ticks)
        {
            TaxPhysicalWithdrawalSystem.LockCoefficients(cPhys, cAbs);
            PhysicalSatisfactionBlendSystem.LockWeight(0.25f);
            TaxPhysicalWithdrawalSystem.ResetSessionTotals();

            using var h = new SimulationHarness(Seed);
            h.RunTicks(0);
            var rate = TaxPolicyLimits.DefaultProductionTaxRate * taxMult;
            SetAllTaxRates(h.EntityManager, rate);
            TaxPhysicalWithdrawalSystem.ResetSessionTotals();
            h.RunTicks(ticks);
            var m = WorldMetrics.Capture(h.EntityManager, ticks);
            return new SweepCell
            {
                CPhys = cPhys,
                CAbs = cAbs,
                Mult = taxMult,
                Rate = rate,
                Ticks = ticks,
                Debt = m.TotalDebt,
                Bankrupt = m.BankruptCount,
                Sat = m.NeedsSatAvg,
                PhysSat = MeanPhysicalSatisfaction(h.EntityManager),
                LodSat = MeanLodSatisfaction(h.EntityManager),
                Pop = m.Population,
                Army = m.WorldArmyStr,
                WithdrawnPhys = TaxPhysicalWithdrawalSystem.SessionWithdrawn,
                WithdrawnAbs = TaxPhysicalWithdrawalSystem.SessionAbstractWithdrawn,
                Hungry = CountHungryPops(h.EntityManager)
            };
        }

        static SweepCell CaptureAt(float cPhys, float cAbs, float rate, int ticks)
        {
            TaxPhysicalWithdrawalSystem.LockCoefficients(cPhys, cAbs);
            PhysicalSatisfactionBlendSystem.LockWeight(0.25f);
            using var h = new SimulationHarness(Seed);
            h.RunTicks(0);
            SetAllTaxRates(h.EntityManager, rate);
            TaxPhysicalWithdrawalSystem.ResetSessionTotals();
            h.RunTicks(ticks);
            var m = WorldMetrics.Capture(h.EntityManager, ticks);
            return new SweepCell
            {
                Sat = m.NeedsSatAvg,
                Debt = m.TotalDebt,
                WithdrawnPhys = TaxPhysicalWithdrawalSystem.SessionWithdrawn,
                WithdrawnAbs = TaxPhysicalWithdrawalSystem.SessionAbstractWithdrawn,
                PhysSat = MeanPhysicalSatisfaction(h.EntityManager),
                LodSat = MeanLodSatisfaction(h.EntityManager),
                Pop = m.Population,
                Ticks = ticks
            };
        }

        static ulong RunStockDigest(float cPhys, float cAbs, float rate, int ticks)
        {
            LockHarnessBaseline();
            TaxPhysicalWithdrawalSystem.LockCoefficients(cPhys, cAbs);
            PhysicalSatisfactionBlendSystem.LockWeight(0.25f);
            using var h = new SimulationHarness(Seed);
            h.RunTicks(0);
            SetAllTaxRates(h.EntityManager, rate);
            h.RunTicks(ticks);
            return PhysicalStockDigest(h.EntityManager);
        }

        static ulong RunMarketDigest(float cPhys, float cAbs, float rate, int ticks)
        {
            LockHarnessBaseline();
            TaxPhysicalWithdrawalSystem.LockCoefficients(cPhys, cAbs);
            PhysicalSatisfactionBlendSystem.LockWeight(0.25f);
            using var h = new SimulationHarness(Seed);
            h.RunTicks(0);
            SetAllTaxRates(h.EntityManager, rate);
            h.RunTicks(ticks);
            return MarketSupplyDigest(h.EntityManager);
        }

        /// <summary>
        /// Digest déterministe : agrège par (ProvinceId, GoodId), ignore GoodId≤0,
        /// trie les clés — jamais Entity.Index.
        /// </summary>
        static ulong PhysicalStockDigest(EntityManager em)
        {
            var map = new SortedDictionary<(int Pid, int Gid), double>();
            using var q = em.CreateEntityQuery(
                ComponentType.ReadOnly<ProvinceData>(),
                ComponentType.ReadOnly<ProvinceStock>());
            using var entities = q.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                var pid = em.GetComponentData<ProvinceData>(entities[i]).ProvinceId;
                var buf = em.GetBuffer<ProvinceStock>(entities[i]);
                for (var j = 0; j < buf.Length; j++)
                {
                    if (buf[j].GoodId <= 0)
                        continue;
                    var key = (pid, buf[j].GoodId);
                    map.TryGetValue(key, out var cur);
                    map[key] = cur + buf[j].Quantity;
                }
            }

            var hash = StateHash.New();
            foreach (var kv in map)
            {
                hash.Int(kv.Key.Pid);
                hash.Int(kv.Key.Gid);
                hash.Double(kv.Value);
            }

            return hash.Value;
        }

        static ulong MarketSupplyDigest(EntityManager em)
        {
            var map = new SortedDictionary<int, float>();
            using var q = em.CreateEntityQuery(
                ComponentType.ReadOnly<MarketPrice>(),
                ComponentType.ReadOnly<GoodData>());
            using var entities = q.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                var gid = em.GetComponentData<GoodData>(entities[i]).GoodId;
                var supply = em.GetComponentData<MarketPrice>(entities[i]).Supply;
                map[gid] = supply;
            }

            var hash = StateHash.New();
            foreach (var kv in map)
            {
                hash.Int(kv.Key);
                hash.Float(kv.Value);
            }

            return hash.Value;
        }

        static float MeanPhysicalSatisfaction(EntityManager em)
        {
            double sum = 0;
            var n = 0;
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<PhysicalDemandSnapshot>());
            using var snaps = q.ToComponentDataArray<PhysicalDemandSnapshot>(Allocator.Temp);
            for (var i = 0; i < snaps.Length; i++)
            {
                sum += snaps[i].PhysicalSatisfaction;
                n++;
            }

            return n > 0 ? (float)(sum / n) : 0f;
        }

        /// <summary>
        /// lodSat depuis PopNeeds (ratios) — non écrasé par le blend.
        /// </summary>
        static float MeanLodSatisfaction(EntityManager em)
        {
            double sum = 0;
            var n = 0;
            using var q = em.CreateEntityQuery(
                ComponentType.ReadOnly<PopNeeds>(),
                ComponentType.ReadOnly<PopData>());
            using var entities = q.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                var needs = em.GetComponentData<PopNeeds>(entities[i]);
                var rf = needs.FoodNeed > 1e-6f ? needs.FoodSatisfied / needs.FoodNeed : 1f;
                var rc = needs.ClothNeed > 1e-6f ? needs.ClothSatisfied / needs.ClothNeed : 1f;
                var rl = needs.LuxuryNeed > 1e-6f ? needs.LuxurySatisfied / needs.LuxuryNeed : 1f;
                rf = Math.Min(1f, Math.Max(0f, rf));
                rc = Math.Min(1f, Math.Max(0f, rc));
                rl = Math.Min(1f, Math.Max(0f, rl));
                sum += rf * 0.6f + rc * 0.3f + rl * 0.1f;
                n++;
            }

            return n > 0 ? (float)(sum / n) : 0f;
        }

        static PhysicalEconomyMetrics GetPhysicalMetrics(EntityManager em)
        {
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<PhysicalEconomyMetrics>());
            if (q.IsEmptyIgnoreFilter)
                return default;
            return q.GetSingleton<PhysicalEconomyMetrics>();
        }

        static void SetAllTaxRates(EntityManager em, float rate)
        {
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<TaxPolicy>());
            using var entities = q.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
                em.SetComponentData(entities[i], new TaxPolicy { ProductionTaxRate = rate });
        }

        static int CountHungryPops(EntityManager em)
        {
            var n = 0;
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<PopData>());
            using var pops = q.ToComponentDataArray<PopData>(Allocator.Temp);
            for (var i = 0; i < pops.Length; i++)
            {
                if (pops[i].NeedsSatisfaction < 0.5f)
                    n++;
            }

            return n;
        }

        static void LockHarnessBaseline()
        {
            BuildingAiPolicyConfig.Lock(BuildingAiPolicy.HoldNone, 0f);
            BuildingConstructionSystem.LockCapacityIntensity(0f);
            PhysicalSatisfactionBlendSystem.LockWeight(0.25f);
        }

        static void ResetAll()
        {
            TaxPhysicalWithdrawalSystem.UnlockCoefficient();
            TaxPhysicalWithdrawalSystem.ResetToCompiledDefault();
            PhysicalSatisfactionBlendSystem.UnlockWeight();
            PhysicalSatisfactionBlendSystem.ResetToCompiledDefault();
            BuildingConstructionSystem.UnlockCapacityIntensity();
            BuildingConstructionSystem.ResetToCompiledDefault();
            BuildingAiPolicyConfig.Unlock();
            BuildingAiPolicyConfig.ResetToCompiledDefault();
        }

        static void ForceGc()
        {
            ResetAll();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        static void AppendCell(StringBuilder sb, SweepCell c)
        {
            sb.AppendLine(
                $"{Fmt2(c.CPhys)} | {Fmt2(c.CAbs)} | {Fmt2(c.Mult)} | {FmtE(c.Rate)} | " +
                $"{c.Ticks} | {Fmt1(c.Debt)} | {c.Bankrupt} | {Fmt3(c.Sat)} | {Fmt3(c.PhysSat)} | " +
                $"{Fmt3(c.LodSat)} | {c.Pop} | {Fmt0(c.Army)} | {FmtD(c.WithdrawnPhys)} | " +
                $"{FmtD(c.WithdrawnAbs)} | {c.Hungry}");
        }

        static string CellKey(float cPhys, float cAbs, float mult) =>
            $"{cPhys:0.00}|{cAbs:0.00}|{mult:0.00}";

        static bool IsMonotoneNonIncreasing(float[] values)
        {
            for (var i = 1; i < values.Length; i++)
            {
                if (values[i] > values[i - 1] + 1e-4f)
                    return false;
            }

            return true;
        }

        static string[] Arr3(float[] v)
        {
            var a = new string[v.Length];
            for (var i = 0; i < v.Length; i++)
                a[i] = Fmt3(v[i]);
            return a;
        }

        static float Stdev(float[] v)
        {
            if (v.Length < 2) return 0f;
            double mean = 0;
            for (var i = 0; i < v.Length; i++) mean += v[i];
            mean /= v.Length;
            double acc = 0;
            for (var i = 0; i < v.Length; i++)
            {
                var d = v[i] - mean;
                acc += d * d;
            }

            return (float)Math.Sqrt(acc / (v.Length - 1));
        }

        static float Range(float[] v)
        {
            if (v.Length == 0) return 0f;
            var min = v[0];
            var max = v[0];
            for (var i = 1; i < v.Length; i++)
            {
                if (v[i] < min) min = v[i];
                if (v[i] > max) max = v[i];
            }

            return max - min;
        }

        static float StdevInt(int[] v)
        {
            var f = new float[v.Length];
            for (var i = 0; i < v.Length; i++) f[i] = v[i];
            return Stdev(f);
        }

        static int RangeInt(int[] v)
        {
            if (v.Length == 0) return 0;
            var min = v[0];
            var max = v[0];
            for (var i = 1; i < v.Length; i++)
            {
                if (v[i] < min) min = v[i];
                if (v[i] > max) max = v[i];
            }

            return max - min;
        }

        static string FmtE(float v) => v.ToString("0.#####E+0", CultureInfo.InvariantCulture);
        static string Fmt0(float v) => v.ToString("0", CultureInfo.InvariantCulture);
        static string Fmt1(float v) => v.ToString("0.0", CultureInfo.InvariantCulture);
        static string Fmt2(float v) =>
            float.IsNaN(v) || float.IsInfinity(v)
                ? v.ToString(CultureInfo.InvariantCulture)
                : v.ToString("0.00", CultureInfo.InvariantCulture);
        static string Fmt3(float v) => v.ToString("0.000", CultureInfo.InvariantCulture);
        static string Fmt4(float v) =>
            float.IsNaN(v) ? "n/a" : v.ToString("0.0000", CultureInfo.InvariantCulture);
        static string FmtD(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        struct SweepCell
        {
            public float CPhys;
            public float CAbs;
            public float Mult;
            public float Rate;
            public int Ticks;
            public float Debt;
            public int Bankrupt;
            public float Sat;
            public float PhysSat;
            public float LodSat;
            public int Pop;
            public float Army;
            public double WithdrawnPhys;
            public double WithdrawnAbs;
            public int Hungry;
        }

        struct NoiseStats
        {
            public float SigmaSat;
            public float RangeSat;
            public float SigmaPhys;
            public float RangePhys;
            public float SigmaDebt;
            public float RangeDebt;
            public float SigmaPop;
            public int RangePop;
            public bool DeterministicEnough;
        }
    }
}
