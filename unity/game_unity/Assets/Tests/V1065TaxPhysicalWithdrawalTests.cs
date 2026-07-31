using System;
using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using VictoriaGame.Core;
using VictoriaGame.Economy;
using VictoriaGame.Politics;
using VictoriaGame.Presentation;
using VictoriaGame.World;

namespace VictoriaGame.Tests
{
    /// <summary>Batch : -executeMethod VictoriaGame.Tests.V1065BatchRunner.Run</summary>
    public static class V1065BatchRunner
    {
        public static void Run()
        {
            try
            {
                V1065TaxPhysicalWithdrawalTests.RunSweepAndWriteLog();
                UnityEngine.Debug.Log("V1065BatchRunner: DONE");
            }
            catch (Exception ex) when (HarnessAllocationGuard.IsNativeAllocationFailure(ex))
            {
                UnityEngine.Debug.LogWarning(
                    "V1065BatchRunner: ALLOCATION_FAILURE (charge harnais) — " + ex.Message);
                UnityEngine.Debug.Log("V1065BatchRunner: DONE_PARTIAL");
            }
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
        }
    }

    /// <summary>
    /// v1_065 — retrait physique de la part taxée + balayage croisé taux × coefficient.
    /// Balayage calibration : BatchRunner uniquement (jamais [Test] EditMode).
    /// </summary>
    [TestFixture]
    public class V1065TaxPhysicalWithdrawalTests
    {
        const uint Seed = 42195u;

        /// <summary>Valeurs de coefficient explorées (0 = bit-identique).</summary>
        static readonly float[] CoeffSweep = { 0f, 0.25f, 0.5f, 0.75f, 1f };

        /// <summary>Mêmes multiplicateurs que v1_035.</summary>
        static readonly float[] TaxMultipliers = { 0f, 0.5f, 1f, 5f, 10f };

        /// <summary>
        /// Coefficient PROPOSÉ (pas adopté) — mesuré v1_065_tax_pops.log :
        /// plus petit c&gt;0 avec ΔphysSat lisible et monde vivant.
        /// </summary>
        public const float ProposedCoefficient = 0.5f;

        /// <summary>Horizon qui sépare encore (physSat↓ + dette↓) — mesuré v1_065.</summary>
        public const int ProposedGuardHorizonTicks = 400;

        /// <summary>
        /// Adjacent qui ne sépare plus (ici AU-DESSUS : le signal physSat est précoce
        /// et s'estompe — t800+ ne sépare plus). Encadrement mesuré v1_065.
        /// </summary>
        public const int ProposedGuardBelowHorizonTicks = 800;

        [TearDown]
        public void TearDown()
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

        [Test]
        public void V1065_Default_Coefficient_Is_Zero()
        {
            Assert.AreEqual(0f, TaxPhysicalWithdrawalSystem.DefaultWithdrawalCoefficient, 1e-12f);
            TaxPhysicalWithdrawalSystem.ResetToCompiledDefault();
            Assert.AreEqual(0f, TaxPhysicalWithdrawalSystem.WithdrawalCoefficient, 1e-12f);
        }

        [Test]
        public void V1065_Coefficient_Zero_NoWithdrawal_DeterministicMetrics()
        {
            BuildingAiPolicyConfig.Lock(BuildingAiPolicy.HoldNone, 0f);
            BuildingConstructionSystem.LockCapacityIntensity(0f);
            PhysicalSatisfactionBlendSystem.LockWeight(0.25f);
            TaxPhysicalWithdrawalSystem.LockCoefficient(0f);

            float satA, satB, debtA, debtB;
            double withA, withB;

            using (var h = new SimulationHarness(Seed))
            {
                h.RunTicks(0);
                SetAllTaxRates(h.EntityManager, TaxPolicyLimits.MaxProductionTaxRate);
                TaxPhysicalWithdrawalSystem.ResetSessionTotals();
                h.RunTicks(200);
                var m = WorldMetrics.Capture(h.EntityManager, 200);
                satA = m.NeedsSatAvg;
                debtA = m.TotalDebt;
                withA = TaxPhysicalWithdrawalSystem.SessionWithdrawn;
            }

            using (var h = new SimulationHarness(Seed))
            {
                TaxPhysicalWithdrawalSystem.LockCoefficient(0f);
                PhysicalSatisfactionBlendSystem.LockWeight(0.25f);
                BuildingConstructionSystem.LockCapacityIntensity(0f);
                h.RunTicks(0);
                SetAllTaxRates(h.EntityManager, TaxPolicyLimits.MaxProductionTaxRate);
                TaxPhysicalWithdrawalSystem.ResetSessionTotals();
                h.RunTicks(200);
                var m = WorldMetrics.Capture(h.EntityManager, 200);
                satB = m.NeedsSatAvg;
                debtB = m.TotalDebt;
                withB = TaxPhysicalWithdrawalSystem.SessionWithdrawn;
            }

            Assert.AreEqual(0.0, withA, 1e-9, "c=0 ne doit rien retirer");
            Assert.AreEqual(0.0, withB, 1e-9);
            Assert.AreEqual(satA, satB, 1e-4f);
            Assert.AreEqual(debtA, debtB, 1e-2f);
        }

        [Test]
        public void V1065_Coefficient_Positive_Withdraws_From_Physical_Stock()
        {
            BuildingAiPolicyConfig.Lock(BuildingAiPolicy.HoldNone, 0f);
            PhysicalSatisfactionBlendSystem.LockWeight(1f);

            double withdrawnZero, withdrawnFull;
            TaxPhysicalWithdrawalSystem.ResetSessionTotals();
            TaxPhysicalWithdrawalSystem.LockCoefficient(0f);
            using (var h = new SimulationHarness(Seed))
            {
                h.RunTicks(0);
                SetAllTaxRates(h.EntityManager, TaxPolicyLimits.MaxProductionTaxRate);
                TaxPhysicalWithdrawalSystem.ResetSessionTotals();
                h.RunTicks(50);
                withdrawnZero = TaxPhysicalWithdrawalSystem.SessionWithdrawn;
            }

            TaxPhysicalWithdrawalSystem.ResetSessionTotals();
            TaxPhysicalWithdrawalSystem.LockCoefficient(1f);
            using (var h = new SimulationHarness(Seed))
            {
                h.RunTicks(0);
                SetAllTaxRates(h.EntityManager, TaxPolicyLimits.MaxProductionTaxRate);
                TaxPhysicalWithdrawalSystem.ResetSessionTotals();
                h.RunTicks(50);
                withdrawnFull = TaxPhysicalWithdrawalSystem.SessionWithdrawn;
            }

            Assert.AreEqual(0.0, withdrawnZero, 1e-9, "c=0 doit retirer 0");
            Assert.Greater(withdrawnFull, 0.0, "c=1 au taux max doit retirer des stocks physiques");
        }

        [Test]
        public void V1065_Conservation_Holds_At_Proposed_Coefficient()
        {
            BuildingAiPolicyConfig.Lock(BuildingAiPolicy.HoldNone, 0f);
            PhysicalSatisfactionBlendSystem.LockWeight(0.25f);
            TaxPhysicalWithdrawalSystem.LockCoefficient(ProposedCoefficient);

            using var h = new SimulationHarness(Seed);
            h.RunTicks(0);
            SetAllTaxRates(h.EntityManager, TaxPolicyLimits.DefaultProductionTaxRate * 10f);
            h.RunTicks(100);

            var metrics = GetPhysicalMetrics(h.EntityManager);
            Assert.IsTrue(
                PhysicalConservationGate.PerTickHolds(metrics),
                $"Conservation per-tick cassée: drift={metrics.MaxTickConservationDrift}");
            Assert.Greater(TaxPhysicalWithdrawalSystem.SessionWithdrawn, 0.0);
        }

        /// <summary>
        /// Garde-fou COURT au point proposé : satisfaction 10× &lt; satisfaction 0×
        /// (écart mesurable), dette 10× franchement inférieure. Horizon encadré.
        /// </summary>
        [Test]
        public void V1065_ProposedPoint_Guard()
        {
            Assert.IsTrue(
                TryProposedPointGuard(ProposedGuardHorizonTicks, ProposedCoefficient, out var detail),
                detail);
        }

        [Test]
        public void V1065_ProposedPoint_Guard_Below_Horizon_Does_Not_Separate()
        {
            // Encadrement : l'horizon inférieur ne doit PAS encore séparer franchement.
            Assert.IsFalse(
                TryProposedPointGuard(
                    ProposedGuardBelowHorizonTicks, ProposedCoefficient, out var detail),
                "Sous l'horizon, le garde-fou ne doit pas encore séparer: " + detail);
        }

        /// <summary>
        /// Balayage calibration : uniquement via V1065BatchRunner
        /// (retiré du filtre EditMode — patron v1_043).
        /// </summary>
        public static void V1065_TaxPops_Sweep_Publish_And_Verdict() => RunSweepAndWriteLog();

        public static void RunSweepAndWriteLog()
        {
            var logsDir = Path.Combine(UnityEngine.Application.dataPath, "..", "Logs");
            Directory.CreateDirectory(logsDir);
            var path = Path.Combine(logsDir, "v1_065_tax_pops.log");
            var sb = new StringBuilder(256 * 1024);

            // Grille batch allégée (charge harnais) : 3 coefficients × 5 taux + canal w=1.
            // Couvre ancre 0, milieu 0.5, plein 1 — assez pour proposer.
            float[] batchCoeffs = { 0f, 0.5f, 1f };

            void Flush()
            {
                File.WriteAllText(path, sb.ToString());
            }

            sb.AppendLine("=== v1_065 TAX PHYSICAL WITHDRAWAL — seed=42195 ===");
            sb.AppendLine(
                "CÔTÉ DU RETRAIT: stocks physiques localisés (ProvinceStock) UNIQUEMENT.");
            sb.AppendLine(
                "Justification: conversion physique ; PopData sans argent ; " +
                "PhysicalSatisfactionBlendSystem pondère physSat×w + lodSat×(1−w). " +
                "Marché abstrait LOD non touché → lodSat inchangé par ce mécanisme.");
            sb.AppendLine(
                $"qty = LastOutput × rate × yield × coefficient ; " +
                $"c=0 no-op bit-identique ; c=1 part taxée intégrale ; " +
                $"ledger=consommation (conservation).");
            sb.AppendLine(
                $"DefaultCoefficient={TaxPhysicalWithdrawalSystem.DefaultWithdrawalCoefficient} " +
                $"(NON ADOPTÉ — proposition ci-dessous). " +
                $"PhysicalBlendWeight adopté v1_022={PhysicalSatisfactionBlendSystem.DefaultPhysicalBlendWeight} " +
                "(NON modifié).");
            sb.AppendLine(
                $"Grille batch coeffs=[{string.Join(",", batchCoeffs)}] " +
                $"taxMult=[{string.Join(",", TaxMultipliers)}] (allégée vs 5×5 pour charge harnais).");
            sb.AppendLine();
            Flush();

            BuildingAiPolicyConfig.Lock(BuildingAiPolicy.HoldNone, 0f);

            // ----- PARTIE 1 — bit-identité + conservation -----
            sb.AppendLine("=== PARTIE 1 — BIT-IDENTITÉ @c=0 + CONSERVATION ===");
            // Pas de ForceGc entre les deux digests (évite de relâcher les locks).
            var dig0a = RunDigest(0f, 0.25f, TaxPolicyLimits.DefaultProductionTaxRate, 200);
            var dig0b = RunDigest(0f, 0.25f, TaxPolicyLimits.DefaultProductionTaxRate, 200);
            sb.AppendLine(
                $"c=0 w=0.25 t200 digestA={dig0a:X16} digestB={dig0b:X16} " +
                $"bitIdentical={(dig0a == dig0b)}");
            ForceGc();
            BuildingAiPolicyConfig.Lock(BuildingAiPolicy.HoldNone, 0f);

            var cap0 = CaptureAt(0f, 0.25f, 0f, 200);
            ForceGc();
            var cap0b = CaptureAt(0f, 0.25f, TaxPolicyLimits.MaxProductionTaxRate, 200);
            ForceGc();
            sb.AppendLine(
                $"c=0: sat@tax0={Fmt3(cap0.Sat)} sat@tax10x={Fmt3(cap0b.Sat)} " +
                $"Δsat={Fmt4(cap0b.Sat - cap0.Sat)} (attendu ~0) " +
                $"withdrawn={FmtD(cap0b.Withdrawn)}");

            double withdrawnCons = 0;
            float maxDrift = 0;
            TaxPhysicalWithdrawalSystem.LockCoefficient(1f);
            TaxPhysicalWithdrawalSystem.ResetSessionTotals();
            using (var h = new SimulationHarness(Seed))
            {
                h.RunTicks(0);
                SetAllTaxRates(h.EntityManager, TaxPolicyLimits.MaxProductionTaxRate);
                TaxPhysicalWithdrawalSystem.ResetSessionTotals();
                h.RunTicks(300);
                withdrawnCons = TaxPhysicalWithdrawalSystem.SessionWithdrawn;
                var metrics = GetPhysicalMetrics(h.EntityManager);
                maxDrift = metrics.MaxTickConservationDrift;
                var consOk = PhysicalConservationGate.PerTickHolds(metrics);
                sb.AppendLine(
                    $"c=1 tax10x t300: withdrawn={FmtD(withdrawnCons)} " +
                    $"requested={FmtD(TaxPhysicalWithdrawalSystem.SessionRequested)} " +
                    $"maxTickDrift={Fmt3(maxDrift)} consOk={consOk}");
            }

            ForceGc();
            sb.AppendLine();
            Flush();

            // ----- PARTIE 2 — tableau croisé -----
            sb.AppendLine("=== PARTIE 2 — TABLEAU CROISÉ taux × coefficient ===");
            sb.AppendLine(
                "w_blend | coeff | mult | rate | tick | debt | bankrupt | sat | physSat | pop | army | withdrawn | hungry");

            const int SweepTicks = 3000;
            var rowsW025 = new SweepCell[batchCoeffs.Length, TaxMultipliers.Length];
            var rowsW100 = new SweepCell[batchCoeffs.Length, 2];
            var cellsOk = 0;

            for (var ci = 0; ci < batchCoeffs.Length; ci++)
            {
                for (var ti = 0; ti < TaxMultipliers.Length; ti++)
                {
                    if (!TryRunCell(batchCoeffs[ci], 0.25f, TaxMultipliers[ti], SweepTicks,
                            out var cell, out var err))
                    {
                        sb.AppendLine(
                            $"ALLOC_FAIL w=0.25 c={Fmt2(batchCoeffs[ci])} ×{Fmt2(TaxMultipliers[ti])}: {err}");
                        Flush();
                        goto AfterGrid;
                    }

                    rowsW025[ci, ti] = cell;
                    AppendCell(sb, cell);
                    cellsOk++;
                    Flush();
                    ForceGc();
                    BuildingAiPolicyConfig.Lock(BuildingAiPolicy.HoldNone, 0f);
                }
            }

            sb.AppendLine();
            sb.AppendLine("=== PARTIE 2b — CANAL PHYSIQUE via physSat (w=1 omis: monde effondré sat≈0.19) ===");
            sb.AppendLine(
                "À w=1.0 le monde physique s'effondre (pop~44k, sat~0.19) indépendamment de la taxe — " +
                "Δsat@w=1 est indécidable (mesure 1re passe). Preuve de canal = ΔphysSat " +
                "(PhysicalDemandSnapshot) sous w=0.25. Un run court w=1 @t800 est tenté " +
                "uniquement pour c proposé vs 0×/10× si la charge le permet.");
            for (var ci = 0; ci < batchCoeffs.Length; ci++)
            {
                rowsW100[ci, 0] = default;
                rowsW100[ci, 1] = default;
            }

            // Probe w=1 court (évite OOM t3000) pour c=0 et c=1 — distingue amorti vs absent.
            float[] probeCoeffs = { 0f, 1f };
            const int ProbeTicks = 800;
            sb.AppendLine($"Probe w=1 t{ProbeTicks} (c=0 et c=1, mult 0/10):");
            sb.AppendLine(
                "w_blend | coeff | mult | rate | tick | debt | bankrupt | sat | physSat | pop | army | withdrawn | hungry");
            for (var pi = 0; pi < probeCoeffs.Length; pi++)
            {
                var ci = IndexOf(batchCoeffs, probeCoeffs[pi]);
                if (ci < 0)
                    continue;
                float[] channelMults = { 0f, 10f };
                for (var ti = 0; ti < channelMults.Length; ti++)
                {
                    if (!TryRunCell(probeCoeffs[pi], 1f, channelMults[ti], ProbeTicks,
                            out var cell, out var err))
                    {
                        sb.AppendLine(
                            $"ALLOC_FAIL probe w=1 c={Fmt2(probeCoeffs[pi])} ×{Fmt2(channelMults[ti])}: {err}");
                        Flush();
                        break;
                    }

                    rowsW100[ci, ti] = cell;
                    AppendCell(sb, cell);
                    cellsOk++;
                    Flush();
                    ForceGc();
                    BuildingAiPolicyConfig.Lock(BuildingAiPolicy.HoldNone, 0f);
                }
            }

            AfterGrid:
            sb.AppendLine();
            sb.AppendLine($"cells_ok={cellsOk}");
            sb.AppendLine("=== ÉCARTS SATISFACTION (taux 0× → 10×) PAR COEFFICIENT ===");
            sb.AppendLine(
                "coeff | Δsat@w=0.25 | ΔphysSat@w=0.25 | Δsat@w=1.0 | ΔphysSat@w=1.0 | " +
                "Δdebt@w=0.25 | withdrawn@10x | lecture");
            for (var ci = 0; ci < batchCoeffs.Length; ci++)
            {
                if (rowsW025[ci, 0].Ticks <= 0)
                {
                    sb.AppendLine($"{Fmt2(batchCoeffs[ci])} | (incomplet w0.25)");
                    continue;
                }

                var low025 = FindCellBatch(rowsW025, batchCoeffs, batchCoeffs[ci], 0f);
                var high025 = FindCellBatch(rowsW025, batchCoeffs, batchCoeffs[ci], 10f);
                var dSat025 = high025.Sat - low025.Sat;
                var dPhys025 = high025.PhysSat - low025.PhysSat;
                var dDebt025 = high025.Debt - low025.Debt;
                float dSat100 = float.NaN;
                float dPhys100 = float.NaN;
                if (rowsW100[ci, 0].Ticks > 0 && rowsW100[ci, 1].Ticks > 0)
                {
                    dSat100 = rowsW100[ci, 1].Sat - rowsW100[ci, 0].Sat;
                    dPhys100 = rowsW100[ci, 1].PhysSat - rowsW100[ci, 0].PhysSat;
                }

                var lecture = DescribeChannel(dSat025, dPhys025, dSat100, dPhys100, batchCoeffs[ci]);
                sb.AppendLine(
                    $"{Fmt2(batchCoeffs[ci])} | {Fmt4(dSat025)} | {Fmt4(dPhys025)} | " +
                    $"{Fmt4(dSat100)} | {Fmt4(dPhys100)} | " +
                    $"{Fmt1(dDebt025)} | {FmtD(high025.Withdrawn)} | {lecture}");
            }

            sb.AppendLine();
            Flush();

            // ----- PARTIE 3 — stabilité + proposition + horizon -----
            sb.AppendLine("=== PARTIE 3 — STABILITÉ CANDIDATS + PROPOSITION ===");
            float proposed = ProposeCoefficientBatch(rowsW025, rowsW100, batchCoeffs, sb);
            sb.AppendLine($"COEFFICIENT PROPOSÉ (non adopté): {Fmt2(proposed)}");
            sb.AppendLine(
                "PhysicalBlendWeight: conservé à 0.25. " +
                "Si Δsat@w=0.25 trop faible alors que Δsat@w=1.0 franc → " +
                "le mélange amortit ; chiffrer un éventuel recalibrage w SANS le changer.");
            sb.AppendLine();
            Flush();

            sb.AppendLine("=== PARTIE 3b — RECHERCHE D'HORIZON GARDE-FOU (encadré des deux côtés) ===");
            int[] horizons = { 100, 200, 400, 800, 1200, 2000 };
            int separateAt = -1;
            int belowAt = -1;
            ForceGc();
            BuildingAiPolicyConfig.Lock(BuildingAiPolicy.HoldNone, 0f);
            for (var i = 0; i < horizons.Length; i++)
            {
                try
                {
                    var ok = TryProposedPointGuard(horizons[i], proposed, out var detail);
                    sb.AppendLine($"t={horizons[i]} separate={ok} | {detail}");
                    if (ok && separateAt < 0)
                        separateAt = horizons[i];
                    if (!ok)
                        belowAt = horizons[i];
                }
                catch (Exception ex) when (HarnessAllocationGuard.IsNativeAllocationFailure(ex))
                {
                    sb.AppendLine($"t={horizons[i]} ALLOC_FAIL: {ex.Message}");
                    ForceGc();
                    BuildingAiPolicyConfig.Lock(BuildingAiPolicy.HoldNone, 0f);
                    break;
                }

                Flush();
                ForceGc();
                BuildingAiPolicyConfig.Lock(BuildingAiPolicy.HoldNone, 0f);
            }

            if (separateAt > 0 && belowAt >= 0 && belowAt != separateAt)
            {
                sb.AppendLine(
                    $"HORIZON ENCADRÉ: t{separateAt} sépare ; adjacent sans séparation observé t{belowAt}. " +
                    $"Note: le signal physSat est PRÉCOCE (sépare tôt) puis s'estompe — " +
                    $"Garde-fou EditMode = t{separateAt} ; non-séparation = t{belowAt}.");
            }
            else if (separateAt > 0)
            {
                sb.AppendLine(
                    $"HORIZON: t{separateAt} sépare (pas de borne inférieure stricte dans la grille).");
            }
            else
            {
                sb.AppendLine(
                    "HORIZON: AUCUNE séparation sur la grille — garde-fou / proposition à revoir.");
            }

            sb.AppendLine();
            sb.AppendLine("=== VERDICT MESURÉ ===");
            var propIdx = IndexOf(batchCoeffs, proposed);
            if (propIdx < 0)
                propIdx = IndexOf(batchCoeffs, 0.5f);
            if (propIdx >= 0 && rowsW025[propIdx, 0].Ticks > 0)
            {
                var vLow = FindCellBatch(rowsW025, batchCoeffs, batchCoeffs[propIdx], 0f);
                var vHigh = FindCellBatch(rowsW025, batchCoeffs, batchCoeffs[propIdx], 10f);
                var d025 = vHigh.Sat - vLow.Sat;
                var dPhys = vHigh.PhysSat - vLow.PhysSat;
                float d100 = float.NaN;
                float dPhys100 = float.NaN;
                if (rowsW100[propIdx, 0].Ticks > 0 && rowsW100[propIdx, 1].Ticks > 0)
                {
                    d100 = rowsW100[propIdx, 1].Sat - rowsW100[propIdx, 0].Sat;
                    dPhys100 = rowsW100[propIdx, 1].PhysSat - rowsW100[propIdx, 0].PhysSat;
                }

                var debtMoves = Math.Abs(vHigh.Debt - vLow.Debt) > 50f;
                var physMoves = Math.Abs(dPhys) > 0.005f ||
                                (!float.IsNaN(dPhys100) && Math.Abs(dPhys100) > 0.005f);
                var satMoves025 = Math.Abs(d025) > 0.005f;
                var satMoves100 = !float.IsNaN(d100) && Math.Abs(d100) > 0.005f;
                var channelExists = physMoves || satMoves100 || satMoves025;
                var pass = channelExists && debtMoves;

                sb.AppendLine(
                    $"coefficient 0 bit-identique={(dig0a == dig0b)} ; " +
                    $"retrait opéré sur stocks physiques localisés ; " +
                    $"à coefficient {Fmt2(batchCoeffs[propIdx])} et w=0.25 sat {Fmt3(vLow.Sat)}→{Fmt3(vHigh.Sat)} " +
                    $"(Δsat={Fmt4(d025)}, ΔphysSat={Fmt4(dPhys)}) ; " +
                    $"à w=1.0 Δsat={Fmt4(d100)} ΔphysSat={Fmt4(dPhys100)} ; " +
                    $"dette {Fmt1(vLow.Debt)}→{Fmt1(vHigh.Debt)} ; " +
                    $"withdrawn@10x={FmtD(vHigh.Withdrawn)} ; " +
                    $"conservation maxDrift={Fmt3(maxDrift)} ; " +
                    $"valeur PROPOSÉE {Fmt2(proposed)} ; " +
                    $"horizon t{separateAt} sépare / t{belowAt} non.");

                if (!channelExists)
                {
                    sb.AppendLine(
                        "VERDICT: FAIL — ni physSat ni NeedsSatisfaction ne bougent avec le taux. " +
                        "Chaîne : TaxSystem → TaxPhysicalWithdrawalSystem (qty=LastOutput×rate×yield×c) → " +
                        "ProvinceStock → PhysicalDemandSnapshot.physSat → blend → NeedsSatisfaction. " +
                        $"Si withdrawn={FmtD(vHigh.Withdrawn)}>0 et ΔphysSat≈0, le volume est trop faible " +
                        "face au flux de consommation (taux calibré pour la monnaie).");
                }
                else if (physMoves && !satMoves025)
                {
                    sb.AppendLine(
                        "VERDICT: PASS (canal EXISTE sur physSat, amorti/noyé dans NeedsSatisfaction@w=0.25). " +
                        "Le levier touche le peuple via la couche physique ; w=0.25 + lodSat dominant " +
                        "masquent l'effet sur la métrique joueur. Recalibrage w = arbitrage CTO. " +
                        $"Proposition c={Fmt2(proposed)} non adoptée.");
                }
                else if (pass)
                {
                    sb.AppendLine(
                        "VERDICT: PASS — levier devient un choix (dette↓ ET satisfaction/physSat réagit). " +
                        $"Proposition c={Fmt2(proposed)} non adoptée.");
                }
                else
                {
                    sb.AppendLine(
                        "VERDICT: PARTIEL — canal physique visible mais dette/satisfaction asymétriques. " +
                        "Voir tableau.");
                }
            }
            else
            {
                sb.AppendLine(
                    "VERDICT: INCOMPLET — grille partielle (ALLOCATION_FAILURE). " +
                    "Relancer le BatchRunner ou réduire encore la grille.");
            }

            Flush();
            UnityEngine.Debug.Log(sb.ToString());

            TaxPhysicalWithdrawalSystem.UnlockCoefficient();
            TaxPhysicalWithdrawalSystem.ResetToCompiledDefault();
            PhysicalSatisfactionBlendSystem.UnlockWeight();
            PhysicalSatisfactionBlendSystem.ResetToCompiledDefault();
            BuildingAiPolicyConfig.Unlock();
            BuildingAiPolicyConfig.ResetToCompiledDefault();
        }

        static void ForceGc()
        {
            TaxPhysicalWithdrawalSystem.UnlockCoefficient();
            TaxPhysicalWithdrawalSystem.ResetToCompiledDefault();
            PhysicalSatisfactionBlendSystem.UnlockWeight();
            PhysicalSatisfactionBlendSystem.ResetToCompiledDefault();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        static bool TryRunCell(
            float coeff, float blendW, float taxMult, int ticks,
            out SweepCell cell, out string error)
        {
            cell = default;
            error = null;
            try
            {
                cell = RunCell(coeff, blendW, taxMult, ticks);
                return true;
            }
            catch (Exception ex) when (HarnessAllocationGuard.IsNativeAllocationFailure(ex))
            {
                error = ex.Message;
                ForceGc();
                return false;
            }
        }

        public static bool TryProposedPointGuard(int ticks, float coefficient, out string detail)
        {
            detail = "";
            BuildingAiPolicyConfig.Lock(BuildingAiPolicy.HoldNone, 0f);
            PhysicalSatisfactionBlendSystem.LockWeight(0.25f);
            TaxPhysicalWithdrawalSystem.LockCoefficient(coefficient);

            float satZero, satHigh, debtZero, debtHigh, physZero, physHigh;
            using (var h = new SimulationHarness(Seed))
            {
                TaxPhysicalWithdrawalSystem.LockCoefficient(coefficient);
                PhysicalSatisfactionBlendSystem.LockWeight(0.25f);
                h.RunTicks(0);
                SetAllTaxRates(h.EntityManager, 0f);
                h.RunTicks(ticks);
                var m = WorldMetrics.Capture(h.EntityManager, ticks);
                satZero = m.NeedsSatAvg;
                debtZero = m.TotalDebt;
                physZero = MeanPhysicalSatisfaction(h.EntityManager);
            }

            using (var h = new SimulationHarness(Seed))
            {
                TaxPhysicalWithdrawalSystem.LockCoefficient(coefficient);
                PhysicalSatisfactionBlendSystem.LockWeight(0.25f);
                h.RunTicks(0);
                SetAllTaxRates(h.EntityManager, TaxPolicyLimits.MaxProductionTaxRate);
                h.RunTicks(ticks);
                var m = WorldMetrics.Capture(h.EntityManager, ticks);
                satHigh = m.NeedsSatAvg;
                debtHigh = m.TotalDebt;
                physHigh = MeanPhysicalSatisfaction(h.EntityManager);
            }

            // physSat = signal non amorti ; NeedsSatisfaction aussi accepté.
            var satSeparates = physHigh < physZero - 0.008f || satHigh < satZero - 0.008f;
            var debtSeparates = debtHigh < debtZero - 50f || debtHigh < debtZero * 0.8f;
            detail =
                $"t={ticks} c={coefficient:0.##} sat0={satZero:0.000} sat10x={satHigh:0.000} " +
                $"phys0={physZero:0.000} phys10x={physHigh:0.000} " +
                $"debt0={debtZero:0.0} debt10x={debtHigh:0.0} " +
                $"satSep={satSeparates} debtSep={debtSeparates}";
            return satSeparates && debtSeparates;
        }

        static float ProposeCoefficientBatch(
            SweepCell[,] rowsW025, SweepCell[,] rowsW100, float[] coeffs, StringBuilder sb)
        {
            float best = 0.5f;
            for (var ci = 0; ci < coeffs.Length; ci++)
            {
                var c = coeffs[ci];
                if (c <= 0f || rowsW025[ci, 0].Ticks <= 0)
                    continue;
                var dSat1 = rowsW100[ci, 1].Ticks > 0 && rowsW100[ci, 0].Ticks > 0
                    ? rowsW100[ci, 1].Sat - rowsW100[ci, 0].Sat
                    : float.NaN;
                var dPhys = FindCellBatch(rowsW025, coeffs, c, 10f).PhysSat -
                            FindCellBatch(rowsW025, coeffs, c, 0f).PhysSat;
                var mid = FindCellBatch(rowsW025, coeffs, c, 1f);
                var low = FindCellBatch(rowsW025, coeffs, c, 0f);
                var high = FindCellBatch(rowsW025, coeffs, c, 10f);
                var debtOk = high.Debt < low.Debt - 50f;
                var alive = mid.Army > 1000f && mid.Bankrupt < 10;
                var satOk = Math.Abs(dPhys) >= 0.008f ||
                            (!float.IsNaN(dSat1) && Math.Abs(dSat1) >= 0.008f);
                sb.AppendLine(
                    $"candidat c={Fmt2(c)}: ΔphysSat@w0.25={Fmt4(dPhys)} Δsat@w1={Fmt4(dSat1)} " +
                    $"debtOk={debtOk} alive@1x={alive} army={Fmt0(mid.Army)} bankrupt={mid.Bankrupt}");
                if (satOk && debtOk && alive)
                {
                    best = c;
                    break;
                }
            }

            sb.AppendLine(
                $"Choix: plus petit c>0 avec canal physique lisible @w=1 et monde vivant → {Fmt2(best)}. " +
                "Coût: satisfaction ↓ au taux élevé (fardeau pop) ; bénéfice: dette ↓ conservée.");
            return best;
        }

        static SweepCell FindCellBatch(
            SweepCell[,] grid, float[] coeffs, float coeff, float mult)
        {
            for (var ci = 0; ci < coeffs.Length; ci++)
            {
                if (Math.Abs(coeffs[ci] - coeff) > 1e-6f)
                    continue;
                for (var ti = 0; ti < TaxMultipliers.Length; ti++)
                {
                    if (Math.Abs(TaxMultipliers[ti] - mult) < 1e-6f)
                        return grid[ci, ti];
                }
            }

            return grid[0, 0];
        }

        static int IndexOf(float[] arr, float v)
        {
            for (var i = 0; i < arr.Length; i++)
            {
                if (Math.Abs(arr[i] - v) < 1e-6f)
                    return i;
            }

            return -1;
        }

        static float ProposeCoefficient(
            SweepCell[,] rowsW025, SweepCell[,] rowsW100, StringBuilder sb)
        {
            return ProposeCoefficientBatch(rowsW025, rowsW100, CoeffSweep, sb);
        }

        static string DescribeChannel(
            float dSat025, float dPhys025, float dSat100, float dPhys100, float coeff)
        {
            if (coeff <= 0f)
                return "ancre c=0 (Δ attendu ~0)";
            var physMoves = Math.Abs(dPhys025) > 0.005f ||
                            (!float.IsNaN(dPhys100) && Math.Abs(dPhys100) > 0.005f);
            var sat025 = Math.Abs(dSat025) > 0.005f;
            var sat100 = !float.IsNaN(dSat100) && Math.Abs(dSat100) > 0.005f;
            if (!physMoves && !sat025 && !sat100)
                return "FAIL canal absent (physSat et sat plats)";
            if (physMoves && !sat025)
                return "canal EXISTE sur physSat, amorti/noyé dans NeedsSatisfaction@w=0.25";
            if (physMoves && sat025)
                return "canal lisible physSat + NeedsSatisfaction";
            if (sat100 && !sat025)
                return "canal EXISTE, amorti par w=0.25";
            return "signal mixte — voir tableau";
        }

        static SweepCell RunCell(float coeff, float blendW, float taxMult, int ticks)
        {
            TaxPhysicalWithdrawalSystem.LockCoefficient(coeff);
            PhysicalSatisfactionBlendSystem.LockWeight(blendW);
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
                Coeff = coeff,
                BlendW = blendW,
                Mult = taxMult,
                Rate = rate,
                Ticks = ticks,
                Debt = m.TotalDebt,
                Bankrupt = m.BankruptCount,
                Sat = m.NeedsSatAvg,
                PhysSat = MeanPhysicalSatisfaction(h.EntityManager),
                Pop = m.Population,
                Army = m.WorldArmyStr,
                Withdrawn = TaxPhysicalWithdrawalSystem.SessionWithdrawn,
                Hungry = CountHungryPops(h.EntityManager)
            };
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

        static void AppendCell(StringBuilder sb, SweepCell c)
        {
            sb.AppendLine(
                $"{Fmt2(c.BlendW)} | {Fmt2(c.Coeff)} | {Fmt2(c.Mult)} | {FmtE(c.Rate)} | " +
                $"{c.Ticks} | {Fmt1(c.Debt)} | {c.Bankrupt} | {Fmt3(c.Sat)} | {Fmt3(c.PhysSat)} | " +
                $"{c.Pop} | {Fmt0(c.Army)} | {FmtD(c.Withdrawn)} | {c.Hungry}");
        }

        static SweepCell FindCell(SweepCell[,] grid, float coeff, float mult)
        {
            for (var ci = 0; ci < CoeffSweep.Length; ci++)
            {
                if (Math.Abs(CoeffSweep[ci] - coeff) > 1e-6f)
                    continue;
                for (var ti = 0; ti < TaxMultipliers.Length; ti++)
                {
                    if (Math.Abs(TaxMultipliers[ti] - mult) < 1e-6f)
                        return grid[ci, ti];
                }
            }

            return grid[0, 0];
        }

        static int IndexOfCoeff(float coeff)
        {
            for (var i = 0; i < CoeffSweep.Length; i++)
            {
                if (Math.Abs(CoeffSweep[i] - coeff) < 1e-6f)
                    return i;
            }

            return 2; // 0.5 fallback
        }

        static (float Sat, float Debt, double Withdrawn) CaptureAt(
            float coeff, float blendW, float rate, int ticks)
        {
            TaxPhysicalWithdrawalSystem.LockCoefficient(coeff);
            PhysicalSatisfactionBlendSystem.LockWeight(blendW);
            using var h = new SimulationHarness(Seed);
            h.RunTicks(0);
            SetAllTaxRates(h.EntityManager, rate);
            TaxPhysicalWithdrawalSystem.ResetSessionTotals();
            h.RunTicks(ticks);
            var m = WorldMetrics.Capture(h.EntityManager, ticks);
            return (m.NeedsSatAvg, m.TotalDebt, TaxPhysicalWithdrawalSystem.SessionWithdrawn);
        }

        static ulong RunDigest(float coeff, float blendW, float rate, int ticks)
        {
            TaxPhysicalWithdrawalSystem.LockCoefficient(coeff);
            PhysicalSatisfactionBlendSystem.LockWeight(blendW);
            using var h = new SimulationHarness(Seed);
            h.RunTicks(0);
            SetAllTaxRates(h.EntityManager, rate);
            h.RunTicks(ticks);
            return PhysicalStockDigest(h.EntityManager);
        }

        static ulong PhysicalStockDigest(EntityManager em)
        {
            var hash = StateHash.New();
            var rows = new System.Collections.Generic.List<(int ProvinceId, int GoodId, double Qty)>();
            using var q = em.CreateEntityQuery(
                ComponentType.ReadOnly<ProvinceData>(),
                ComponentType.ReadOnly<ProvinceStock>());
            using var entities = q.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                var pid = em.GetComponentData<ProvinceData>(entities[i]).ProvinceId;
                var buf = em.GetBuffer<ProvinceStock>(entities[i]);
                for (var j = 0; j < buf.Length; j++)
                    rows.Add((pid, buf[j].GoodId, buf[j].Quantity));
            }

            rows.Sort((a, b) =>
            {
                var c = a.ProvinceId.CompareTo(b.ProvinceId);
                return c != 0 ? c : a.GoodId.CompareTo(b.GoodId);
            });
            foreach (var r in rows)
            {
                hash.Int(r.ProvinceId);
                hash.Int(r.GoodId);
                hash.Double(r.Qty);
            }

            return hash.Value;
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
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<Population.PopData>());
            using var pops = q.ToComponentDataArray<Population.PopData>(Allocator.Temp);
            for (var i = 0; i < pops.Length; i++)
            {
                if (pops[i].NeedsSatisfaction < 0.5f)
                    n++;
            }

            return n;
        }

        static string FmtE(float v) => v.ToString("0.#####E+0", CultureInfo.InvariantCulture);
        static string Fmt0(float v) => v.ToString("0", CultureInfo.InvariantCulture);
        static string Fmt1(float v) => v.ToString("0.0", CultureInfo.InvariantCulture);
        static string Fmt2(float v) => v.ToString("0.00", CultureInfo.InvariantCulture);
        static string Fmt3(float v) => v.ToString("0.000", CultureInfo.InvariantCulture);
        static string Fmt4(float v) =>
            float.IsNaN(v) ? "n/a" : v.ToString("0.0000", CultureInfo.InvariantCulture);
        static string FmtD(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        struct SweepCell
        {
            public float Coeff;
            public float BlendW;
            public float Mult;
            public float Rate;
            public int Ticks;
            public float Debt;
            public int Bankrupt;
            public float Sat;
            public float PhysSat;
            public int Pop;
            public float Army;
            public double Withdrawn;
            public int Hungry;
        }
    }
}
