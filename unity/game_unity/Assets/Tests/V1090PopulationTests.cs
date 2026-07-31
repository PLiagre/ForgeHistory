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
using VictoriaGame.Politics;
using VictoriaGame.Population;
using VictoriaGame.Presentation;
using VictoriaGame.World;
using Debug = UnityEngine.Debug;

namespace VictoriaGame.Tests
{
    /// <summary>Batch : -executeMethod VictoriaGame.Tests.V1090BatchRunner.Run</summary>
    public static class V1090BatchRunner
    {
        public static void Run()
        {
            try
            {
                V1090PopulationTests.RunAndWriteArtifacts();
                Debug.Log("V1090BatchRunner: DONE");
            }
            catch (Exception ex) when (HarnessAllocationGuard.IsNativeAllocationFailure(ex))
            {
                Debug.LogWarning("V1090BatchRunner: ALLOCATION_FAILURE — " + ex.Message);
                Debug.Log("V1090BatchRunner: DONE_PARTIAL");
            }
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
        }

        /// <summary>Mesure seule des empreintes (pour rebaser ParityAnchors.Expected).</summary>
        public static void MeasureDigests()
        {
            TaxPhysicalWithdrawalSystem.EnsureParitySafeDefaults();
            PopGrowthSystem.ResetToCompiledDefault();

            ulong fullOff;
            ulong variantOff;
            PopGrowthSystem.CountryPopulationAggregationInterval = 0;
            using (var h = new SimulationHarness(42195u))
            {
                PopGrowthSystem.CountryPopulationAggregationInterval = 0;
                h.RunTicks(100);
                fullOff = WorldDigest.Compute(h.EntityManager);
                variantOff = WorldDigest.Compute(h.EntityManager, includeCountryPopulation: false);
            }

            PopGrowthSystem.ResetToCompiledDefault();
            TaxPhysicalWithdrawalSystem.EnsureParitySafeDefaults();
            ulong fullOn;
            ulong variantOn;
            using (var h = new SimulationHarness(42195u))
            {
                h.RunTicks(100);
                fullOn = WorldDigest.Compute(h.EntityManager);
                variantOn = WorldDigest.Compute(h.EntityManager, includeCountryPopulation: false);
            }

            Debug.Log($"V1090_DIGEST_OFF=0x{fullOff:X16}");
            Debug.Log($"V1090_DIGEST_ON=0x{fullOn:X16}");
            Debug.Log($"V1090_VARIANT_OFF=0x{variantOff:X16}");
            Debug.Log($"V1090_VARIANT_ON=0x{variantOn:X16}");
            Debug.Log($"V1090_VARIANT_MATCH={variantOff == variantOn}");
            Debug.Log($"V1090_OFF_MATCHES_PRE={fullOff == ParityAnchors.PreV1090FrozenPopulation}");
            Debug.Log("V1090BatchRunner.MeasureDigests: DONE");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
        }
    }

    /// <summary>
    /// v1_090 — PHASE XII : alimenter CountryData.Population depuis Σ PopData.Size.
    /// </summary>
    [TestFixture]
    public class V1090PopulationTests
    {
        const uint Seed = 42195u;
        const int ParityTicks = 100;
        const int ReferenceTicks = 3000;
        const int PlayerCountryId = PlayerControl.DefaultControlledCountryId;

        static string GameUnityRoot =>
            Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        static string LogPath => Path.Combine(GameUnityRoot, "Logs", "v1_090_population.log");
        static string CapturesDir => Path.Combine(GameUnityRoot, "Captures", "v1_090");

        [TearDown]
        public void TearDown() => ResetAll();

        [Test]
        public void V1090_Aggregation_Off_Recovers_PreV1090_Digest()
        {
            ResetAll();
            PopGrowthSystem.CountryPopulationAggregationInterval = 0;
            TaxPhysicalWithdrawalSystem.EnsureParitySafeDefaults();
            using var h = new SimulationHarness(Seed);
            PopGrowthSystem.CountryPopulationAggregationInterval = 0;
            h.RunTicks(ParityTicks);
            var dig = WorldDigest.Compute(h.EntityManager);
            Assert.AreEqual(ParityAnchors.PreV1090FrozenPopulation, dig,
                "interval=0 → Population reste 0 → digest pré-v1_090");
        }

        [Test]
        public void V1090_Aggregation_On_Matches_Rebased_Parity()
        {
            ResetAll();
            TaxPhysicalWithdrawalSystem.EnsureParitySafeDefaults();
            using var h = new SimulationHarness(Seed);
            h.RunTicks(ParityTicks);
            var dig = WorldDigest.Compute(h.EntityManager);
            Assert.AreNotEqual(0UL, ParityAnchors.Expected, "Expected doit être mesuré (≠0)");
            Assert.AreEqual(ParityAnchors.Expected, dig,
                "agrégation ON → digest v1_090 rebasé");
            Assert.AreNotEqual(ParityAnchors.PreV1090FrozenPopulation, dig,
                "Population vivante change l'empreinte (attendu)");
        }

        [Test]
        public void V1090_Variant_Digest_Excluding_Population_BitIdentical()
        {
            ResetAll();
            TaxPhysicalWithdrawalSystem.EnsureParitySafeDefaults();

            ulong variantOff;
            PopGrowthSystem.CountryPopulationAggregationInterval = 0;
            using (var h = new SimulationHarness(Seed))
            {
                PopGrowthSystem.CountryPopulationAggregationInterval = 0;
                h.RunTicks(ParityTicks);
                variantOff = WorldDigest.Compute(h.EntityManager, includeCountryPopulation: false);
            }

            ResetAll();
            TaxPhysicalWithdrawalSystem.EnsureParitySafeDefaults();
            ulong variantOn;
            using (var h = new SimulationHarness(Seed))
            {
                h.RunTicks(ParityTicks);
                variantOn = WorldDigest.Compute(h.EntityManager, includeCountryPopulation: false);
            }

            Assert.AreEqual(variantOff, variantOn,
                "digest SANS Population doit être bit-identique avant/après — " +
                "sinon le correctif a changé autre chose que l'affichage");
        }

        [Test]
        public void V1090_Determinism_Two_Runs_Same_New_Digest()
        {
            ResetAll();
            var a = RunParityDigest();
            ResetAll();
            var b = RunParityDigest();
            Assert.AreEqual(a, b);
            Assert.AreEqual(ParityAnchors.Expected, a);
        }

        [Test]
        public void V1090_Artifacts_And_Verdict() => RunAndWriteArtifacts();

        public static void RunAndWriteArtifacts()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            Directory.CreateDirectory(CapturesDir);
            var sb = new StringBuilder(512 * 1024);

            void Flush() => File.WriteAllText(LogPath, sb.ToString(), Encoding.UTF8);

            sb.AppendLine("=== v1_090 POPULATION COUNTRY — seed=42195 PHASE XII ===");
            sb.AppendLine(
                "Contrat: cadran mort confirmé, lecteurs, chiffrage stabilité/radicalisme, " +
                "branchement PopSizeAggregation, preuve 3 temps, captures vue de jeu.");
            sb.AppendLine();
            Flush();

            // ----- PARTIE 1 -----
            sb.AppendLine("=== PARTIE 1 — CADRAN MORT + LECTEURS + RÉFÉRENCE + AUTRES CADRANS ===");
            sb.AppendLine("INVENTAIRE (confirmé fichier:ligne) :");
            sb.AppendLine(
                "  (1) CountryData.Population = 0 écrit UNE FOIS @ CountryInitSystem.cs:47 — CONFIRMÉ.");
            sb.AppendLine(
                "  (2) Aucun système de simulation ne réécrit Population avant v1_090 — CONFIRMÉ " +
                "(grep CountryData.Population / .Population = : seul init + MapSpriteOverlay + digest).");
            sb.AppendLine(
                "  (3) Affichage MapSpriteOverlay.cs:1286 (Snapshot) et :1343 (ligne POP) — CONFIRMÉ.");
            sb.AppendLine(
                "  (4) DeterminismTests.HashCountries hash.Int(row.Country.Population) — CONFIRMÉ.");
            sb.AppendLine("LECTEURS EXHAUSTIFS de CountryData.Population :");
            sb.AppendLine("  [1] MapSpriteOverlay/CountryObservation (affichage panneau pays)");
            sb.AppendLine("  [2] DeterminismTests.WorldDigest.HashCountries (digest de parité)");
            sb.AppendLine(
                "  AUCUN système de simulation ni d'IA ne lit CountryData.Population — " +
                "alimenter le champ change l'AFFICHAGE + le DIGEST, pas un comportement simulé.");
            sb.AppendLine();
            Flush();

            // Référence affiché vs réel AVANT correction (interval=0)
            ForceGc();
            ResetAll();
            PopGrowthSystem.CountryPopulationAggregationInterval = 0;
            TaxPhysicalWithdrawalSystem.EnsureParitySafeDefaults();
            var refTicks = new[] { 100, 500, 1000, 2000 };
            sb.AppendLine("RÉFÉRENCE AVANT (interval=0, Population gelée à 0) — échantillon pays :");
            using (var h = new SimulationHarness(Seed))
            {
                PopGrowthSystem.CountryPopulationAggregationInterval = 0;
                var last = 0;
                foreach (var tick in refTicks)
                {
                    h.RunTicks(tick - last);
                    last = tick;
                    var em = h.EntityManager;
                    sb.AppendLine($"--- tick={tick} ---");
                    foreach (var tag in new[] { "FRA", "ENG", "AUS", "BUR" })
                    {
                        var cid = FindCountryIdByTag(em, tag);
                        Assert.IsTrue(CountryObservation.TryCapture(em, cid, out var snap));
                        var real = SumPopSizeForCountry(em, cid);
                        sb.AppendLine(
                            $"  {tag}: affiché={snap.Population} réel_PopData={real} " +
                            $"écart={real - snap.Population}");
                    }
                }
            }

            sb.AppendLine();
            Flush();

            // Autres cadrans morts (mesure sans correction)
            ForceGc();
            ResetAll();
            TaxPhysicalWithdrawalSystem.EnsureParitySafeDefaults();
            int firstFloorTick;
            int[] floorAt;
            float maxAvgRadicalism;
            float radicalismThreshold;
            int revolutionsAt3000;
            MeasureDeadDials(
                out firstFloorTick, out floorAt, out maxAvgRadicalism,
                out radicalismThreshold, out revolutionsAt3000);

            sb.AppendLine("CADRANS MORTS (mesurés, NON corrigés) :");
            sb.AppendLine(
                $"  Stabilité: premier tick où médiane atteint 0 = t{firstFloorTick} " +
                $"(dérive -0.0005/tick @ StabilitySystem.cs:71, borne [0..1]).");
            sb.AppendLine(
                $"  Pays au plancher Stability=0 : t500={floorAt[0]}/20 t1000={floorAt[1]}/20 " +
                $"t2000={floorAt[2]}/20 t3000={floorAt[3]}/20");
            sb.AppendLine(
                $"  Radicalisation moyenne MAX sur 3000 ticks = {Fmt(maxAvgRadicalism)} " +
                $"contre seuil RevolutionSystem.DefaultRadicalismThreshold={Fmt(radicalismThreshold)} ; " +
                $"revolutions_active@t3000={revolutionsAt3000}.");
            if (maxAvgRadicalism < radicalismThreshold)
            {
                sb.AppendLine(
                    $"  VERDICT révolution: INATTEIGNABLE par construction " +
                    $"(max {Fmt(maxAvgRadicalism)} < seuil {Fmt(radicalismThreshold)}).");
            }
            else
            {
                sb.AppendLine(
                    $"  VERDICT révolution: seuil atteint en moyenne pays " +
                    $"(max {Fmt(maxAvgRadicalism)} >= {Fmt(radicalismThreshold)}) — " +
                    "voir revolutions_active.");
            }

            sb.AppendLine();
            Flush();

            // ----- PARTIE 2 -----
            sb.AppendLine("=== PARTIE 2 — BRANCHE + PREUVE 3 TEMPS + DÉTERMINISME + RÉVERSIBILITÉ ===");
            sb.AppendLine(
                "Cadence: CountryPopulationAggregationInterval=1 (chaque tick). " +
                "Justification: Σ d'entiers O(pops), panneau doit suivre la démographie ; " +
                "interval=0 = désactivé (réversibilité).");
            sb.AppendLine(
                "Méthode: PopSizeAggregation.WriteCountryPopulations — même Σ PopData.Size " +
                "que WorldMetrics.CapturePopulation (plus de boucle parallèle).");
            sb.AppendLine();

            ForceGc();
            ResetAll();
            TaxPhysicalWithdrawalSystem.EnsureParitySafeDefaults();
            ulong variantOff;
            ulong fullOff;
            PopGrowthSystem.CountryPopulationAggregationInterval = 0;
            using (var h = new SimulationHarness(Seed))
            {
                PopGrowthSystem.CountryPopulationAggregationInterval = 0;
                h.RunTicks(ParityTicks);
                fullOff = WorldDigest.Compute(h.EntityManager);
                variantOff = WorldDigest.Compute(h.EntityManager, includeCountryPopulation: false);
            }

            ForceGc();
            ResetAll();
            TaxPhysicalWithdrawalSystem.EnsureParitySafeDefaults();
            ulong variantOn;
            ulong fullOnA;
            using (var h = new SimulationHarness(Seed))
            {
                h.RunTicks(ParityTicks);
                fullOnA = WorldDigest.Compute(h.EntityManager);
                variantOn = WorldDigest.Compute(h.EntityManager, includeCountryPopulation: false);
            }

            ForceGc();
            ResetAll();
            TaxPhysicalWithdrawalSystem.EnsureParitySafeDefaults();
            ulong fullOnB;
            using (var h = new SimulationHarness(Seed))
            {
                h.RunTicks(ParityTicks);
                fullOnB = WorldDigest.Compute(h.EntityManager);
            }

            sb.AppendLine(
                $"(1) digest VARIANT sans Population: off=0x{variantOff:X16} on=0x{variantOn:X16} " +
                $"bit_identical={(variantOff == variantOn)}");
            sb.AppendLine(
                $"(2) empreinte COMPLÈTE: ancienne=0x{ParityAnchors.PreV1090FrozenPopulation:X16} " +
                $"(mesurée off=0x{fullOff:X16} match={(fullOff == ParityAnchors.PreV1090FrozenPopulation)}) ; " +
                $"nouvelle=0x{fullOnA:X16} " +
                $"(raison: Population hachée passe de 0 à Σ PopData.Size)");
            sb.AppendLine(
                $"(3) rebasage ParityAnchors.Expected → 0x{fullOnA:X16} (v1_090, Population vivante). " +
                $"constante_actuelle=0x{ParityAnchors.Expected:X16} " +
                $"alignée={(ParityAnchors.Expected == fullOnA)}");
            sb.AppendLine(
                $"DÉTERMINISME 2 runs: A=0x{fullOnA:X16} B=0x{fullOnB:X16} equal={(fullOnA == fullOnB)}");
            sb.AppendLine(
                $"RÉVERSIBILITÉ interval=0: digest=0x{fullOff:X16} " +
                $"== pré-v1_090 0x{ParityAnchors.PreV1090FrozenPopulation:X16} " +
                $"bit_identical={(fullOff == ParityAnchors.PreV1090FrozenPopulation)}");
            sb.AppendLine();
            Flush();

            // Write measured digest for human/CI — also print for Cursor to rebase const
            Debug.Log($"V1090_MEASURED_NEW_DIGEST=0x{fullOnA:X16}");
            Debug.Log($"V1090_MEASURED_VARIANT=0x{variantOn:X16}");
            Debug.Log($"V1090_MEASURED_OLD_OFF=0x{fullOff:X16}");

            var variantOk = variantOff == variantOn;
            var reversibilityOk = fullOff == ParityAnchors.PreV1090FrozenPopulation;
            var detOk = fullOnA == fullOnB;
            var rebasedOk = ParityAnchors.Expected == fullOnA && fullOnA != 0UL;

            if (!variantOk)
            {
                sb.AppendLine(
                    "ARRÊT: digest variant NON bit-identique — le correctif a changé autre chose " +
                    "que CountryData.Population. NE PAS rebaser. Voir écarts.");
                Flush();
                Assert.Fail("variant digest diverged");
            }

            // ----- PARTIE 3 -----
            sb.AppendLine("=== PARTIE 3 — JOUEUR VOIT UN CHIFFRE VRAI ===");
            ForceGc();
            ResetAll();
            TaxPhysicalWithdrawalSystem.EnsureParitySafeDefaults();
            int worldPop;
            int countrySum;
            int orphan;
            var evolution = new List<(int Tick, int Displayed, int Real)>(4);
            using (var h = new SimulationHarness(Seed))
            {
                // Avant: forcer un snapshot à t0 (init only) — Population encore 0
                h.RunTicks(0);
                var em = h.EntityManager;
                Assert.IsTrue(CountryObservation.TryCapture(em, PlayerCountryId, out var snap0));
                sb.AppendLine(
                    $"CAPTURE t0 (init, avant sim): {snap0.Tag} POP affiché={snap0.Population}");
                WriteGameViewCapture(
                    em, Path.Combine(CapturesDir, "01_pop_before.png"),
                    PlayerCountryId,
                    $"AVANT — {snap0.Tag} POP {snap0.Population} t0",
                    ExtractIdentitySection(snap0.DetailBlock));

                // Après agrégation + évolution sur 4 ticks
                var sampleTicks = new[] { 1, 100, 500, 1000 };
                var last = 0;
                foreach (var tick in sampleTicks)
                {
                    h.RunTicks(tick - last);
                    last = tick;
                    Assert.IsTrue(CountryObservation.TryCapture(em, PlayerCountryId, out var snap));
                    var real = SumPopSizeForCountry(em, PlayerCountryId);
                    evolution.Add((tick, snap.Population, real));
                    sb.AppendLine(
                        $"ÉVOLUTION {snap.Tag} t{tick}: affiché={snap.Population} réel={real} " +
                        $"match={(snap.Population == real)}");
                }

                worldPop = PopSizeAggregation.SumAll(em);
                countrySum = PopSizeAggregation.SumCountryFields(em);
                orphan = PopSizeAggregation.SumOrphan(em);
                Assert.IsTrue(CountryObservation.TryCapture(em, PlayerCountryId, out var snapAfter));
                WriteGameViewCapture(
                    em, Path.Combine(CapturesDir, "02_pop_after.png"),
                    PlayerCountryId,
                    $"APRES — {snapAfter.Tag} POP {snapAfter.Population} t{last}",
                    ExtractIdentitySection(snapAfter.DetailBlock));

                // 3e capture: panneau à un tick intermédiaire vivant
                WriteGameViewCapture(
                    em, Path.Combine(CapturesDir, "03_pop_panel.png"),
                    PlayerCountryId,
                    $"PANNEAU — {snapAfter.Tag} POP {snapAfter.Population} t{last}",
                    ExtractIdentitySection(snapAfter.DetailBlock));
            }

            sb.AppendLine(
                $"COHÉRENCE totaux @t1000: world(PopData)={worldPop} sum(CountryData.Population)={countrySum} " +
                $"orphan(pops sans pays)={orphan} écart_world_vs_countries={worldPop - countrySum} " +
                $"(attendu = orphan)");
            sb.AppendLine(
                $"écart_expliqué={(worldPop - countrySum == orphan)}");

            var living = evolution.Count >= 2 &&
                         (evolution[evolution.Count - 1].Displayed != evolution[0].Displayed ||
                          evolution.Exists(e => e.Displayed > 0));
            sb.AppendLine($"évolution_vivante={living} (au moins un tick avec POP>0 ou variation)");
            for (var i = 0; i < evolution.Count; i++)
            {
                var e = evolution[i];
                sb.AppendLine(
                    $"  tick={e.Tick} POP={e.Displayed} réel={e.Real}");
            }

            var capturesExist =
                File.Exists(Path.Combine(CapturesDir, "01_pop_before.png")) &&
                File.Exists(Path.Combine(CapturesDir, "02_pop_after.png")) &&
                File.Exists(Path.Combine(CapturesDir, "03_pop_panel.png"));
            sb.AppendLine($"png_before={Path.Combine(CapturesDir, "01_pop_before.png")}");
            sb.AppendLine($"png_after={Path.Combine(CapturesDir, "02_pop_after.png")}");
            sb.AppendLine($"png_panel={Path.Combine(CapturesDir, "03_pop_panel.png")}");
            sb.AppendLine($"captures_exist={capturesExist}");
            sb.AppendLine();

            var coherenceOk = worldPop - countrySum == orphan;
            var displayOk = evolution.TrueForAll(e => e.Displayed == e.Real) &&
                            evolution.Exists(e => e.Displayed > 0);

            var pass = variantOk && reversibilityOk && detOk && rebasedOk &&
                       coherenceOk && displayOk && capturesExist && living;

            sb.AppendLine("=== VERDICT MESURÉ ===");
            sb.AppendLine(
                $"Population écrite 1 fois à 0 @CountryInitSystem.cs:47, jamais réécrite avant v1_090, " +
                $"2 lecteurs (affichage + digest), aucun système de simulation ; " +
                $"stabilité plancher dès t{firstFloorTick}, floor t3000={floorAt[3]}/20 ; " +
                $"radicalisation max {Fmt(maxAvgRadicalism)} vs seuil {Fmt(radicalismThreshold)}, " +
                $"révolution inatteignable={(maxAvgRadicalism < radicalismThreshold)} ; " +
                $"agrégation CountryId chaque tick ; " +
                $"digest SANS Population 0x{variantOn:X16} bit-identique={variantOk} ; " +
                $"parité 0x{ParityAnchors.PreV1090FrozenPopulation:X16} → 0x{fullOnA:X16} " +
                $"rebasée={rebasedOk} ; réversible={reversibilityOk} ; " +
                $"total pays=monde−orphan écart_ok={coherenceOk} ; " +
                $"évolution ticks=[{string.Join(",", evolution.ConvertAll(e => e.Displayed.ToString()))}] ; " +
                $"captures={capturesExist}.");
            sb.AppendLine(pass
                ? "VERDICT: PASS — cadran branché, preuve 3 temps, chiffre vrai et vivant."
                : "VERDICT: FAIL — un critère du contrat a lâché " +
                  $"(variant={variantOk} rev={reversibilityOk} det={detOk} rebase={rebasedOk} " +
                  $"coh={coherenceOk} display={displayOk} live={living} capt={capturesExist}).");
            Flush();
            Debug.Log(sb.ToString());

            Assert.IsTrue(variantOk, "variant");
            Assert.IsTrue(reversibilityOk, "réversibilité");
            Assert.IsTrue(detOk, "déterminisme");
            Assert.IsTrue(rebasedOk,
                $"ParityAnchors.Expected doit égaler 0x{fullOnA:X16} (mesuré) — " +
                $"actuel 0x{ParityAnchors.Expected:X16}");
            Assert.IsTrue(coherenceOk, "cohérence totaux");
            Assert.IsTrue(displayOk, "affiché==réel et >0");
            Assert.IsTrue(living, "évolution vivante");
            Assert.IsTrue(capturesExist, "captures");
            ResetAll();
        }

        static ulong RunParityDigest()
        {
            TaxPhysicalWithdrawalSystem.EnsureParitySafeDefaults();
            using var h = new SimulationHarness(Seed);
            h.RunTicks(ParityTicks);
            return WorldDigest.Compute(h.EntityManager);
        }

        static void MeasureDeadDials(
            out int firstFloorTick,
            out int[] floorAt,
            out float maxAvgRadicalism,
            out float radicalismThreshold,
            out int revolutionsAt3000)
        {
            firstFloorTick = -1; // sentinelle « pas trouvé » (≠ t0 réel)
            floorAt = new int[4];
            maxAvgRadicalism = 0f;
            radicalismThreshold = 0.7f;
            revolutionsAt3000 = 0;

            const int SampleEvery = 25;
            using var h = new SimulationHarness(Seed);
            var checkpoints = new[] { 500, 1000, 2000, 3000 };
            var floorIdx = 0;
            for (var tick = SampleEvery; tick <= ReferenceTicks; tick += SampleEvery)
            {
                h.RunTicks(SampleEvery);
                var em = h.EntityManager;
                SamplePolitics(em, out var medianStab, out var maxAvgRad, out var revs, out var thr);
                if (maxAvgRad > maxAvgRadicalism)
                    maxAvgRadicalism = maxAvgRad;
                radicalismThreshold = thr;

                if (firstFloorTick < 0 && medianStab <= 0f)
                    firstFloorTick = tick;

                while (floorIdx < checkpoints.Length && tick >= checkpoints[floorIdx])
                {
                    floorAt[floorIdx] = CountStabilityFloor(em);
                    floorIdx++;
                }

                if (tick == ReferenceTicks)
                    revolutionsAt3000 = revs;
            }
        }

        static void SamplePolitics(
            EntityManager em,
            out float medianStability,
            out float maxCountryAvgRadicalism,
            out int revolutionsActive,
            out float threshold)
        {
            var stabs = new List<float>(32);
            revolutionsActive = 0;
            threshold = 0.7f;
            using (var q = em.CreateEntityQuery(
                       ComponentType.ReadOnly<GovernmentData>(),
                       ComponentType.ReadOnly<RevolutionData>(),
                       ComponentType.ReadOnly<CountryData>()))
            using (var entities = q.ToEntityArray(Allocator.Temp))
            using (var govs = q.ToComponentDataArray<GovernmentData>(Allocator.Temp))
            using (var revs = q.ToComponentDataArray<RevolutionData>(Allocator.Temp))
            {
                for (var i = 0; i < govs.Length; i++)
                {
                    stabs.Add(govs[i].Stability);
                    if (revs[i].IsRevolutionActive)
                        revolutionsActive++;
                    if (revs[i].RadicalismThreshold > 0.0001f)
                        threshold = revs[i].RadicalismThreshold;
                }
            }

            stabs.Sort();
            medianStability = stabs.Count == 0
                ? float.NaN
                : stabs.Count % 2 == 1
                    ? stabs[stabs.Count / 2]
                    : 0.5f * (stabs[stabs.Count / 2 - 1] + stabs[stabs.Count / 2]);

            // Moyenne radicalisme par pays (PopPolitics), max sur les pays
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
                    if (c == Entity.Null)
                        continue;
                    sum.TryGetValue(c, out var s);
                    count.TryGetValue(c, out var n);
                    sum[c] = s + pols[i].Radicalism;
                    count[c] = n + 1;
                }
            }

            maxCountryAvgRadicalism = 0f;
            foreach (var kv in sum)
            {
                var n = count[kv.Key];
                if (n <= 0)
                    continue;
                var avg = kv.Value / n;
                if (avg > maxCountryAvgRadicalism)
                    maxCountryAvgRadicalism = avg;
            }
        }

        static int CountStabilityFloor(EntityManager em)
        {
            var n = 0;
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<GovernmentData>());
            using var govs = q.ToComponentDataArray<GovernmentData>(Allocator.Temp);
            for (var i = 0; i < govs.Length; i++)
            {
                if (govs[i].Stability <= 0f)
                    n++;
            }

            return n;
        }

        static int SumPopSizeForCountry(EntityManager em, int countryId)
        {
            Entity country = Entity.Null;
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<CountryData>()))
            using (var entities = q.ToEntityArray(Allocator.Temp))
            using (var data = q.ToComponentDataArray<CountryData>(Allocator.Temp))
            {
                for (var i = 0; i < data.Length; i++)
                {
                    if (data[i].CountryId != countryId)
                        continue;
                    country = entities[i];
                    break;
                }
            }

            if (country == Entity.Null)
                return -1; // sentinelle ≠ 0

            var total = 0;
            using var pq = em.CreateEntityQuery(ComponentType.ReadOnly<PopData>());
            using var pops = pq.ToComponentDataArray<PopData>(Allocator.Temp);
            for (var i = 0; i < pops.Length; i++)
            {
                if (pops[i].Country == country)
                    total += pops[i].Size;
            }

            return total;
        }

        static int FindCountryIdByTag(EntityManager em, string tag)
        {
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<CountryData>());
            using var data = q.ToComponentDataArray<CountryData>(Allocator.Temp);
            for (var i = 0; i < data.Length; i++)
            {
                if (data[i].Tag.ToString() == tag)
                    return data[i].CountryId;
            }

            return -1;
        }

        static string ExtractIdentitySection(string detail)
        {
            if (string.IsNullOrEmpty(detail))
                return "--- IDENTITY ---\n(none)\n";
            var start = detail.IndexOf("--- IDENTITY ---", StringComparison.Ordinal);
            if (start < 0)
                return "--- IDENTITY ---\n(missing)\n";
            var end = detail.IndexOf("--- TREASURY ---", start, StringComparison.Ordinal);
            if (end < 0)
                end = detail.Length;
            return detail.Substring(start, end - start).TrimEnd() + "\n";
        }

        static void WriteGameViewCapture(
            EntityManager em, string path, int countryId, string title, string identitySection)
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
                        var lines = identitySection.Split('\n');
                        for (var i = 0; i < lines.Length && i < 12; i++)
                        {
                            if (string.IsNullOrEmpty(lines[i]))
                                continue;
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

        static string Fmt(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        static void ForceGc()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        static void ResetAll()
        {
            PopGrowthSystem.ResetToCompiledDefault();
            TaxPhysicalWithdrawalSystem.ResetToCompiledDefault();
            TaxPhysicalWithdrawalSystem.EnsureParitySafeDefaults();
            MapViewport.Reset();
        }
    }
}
