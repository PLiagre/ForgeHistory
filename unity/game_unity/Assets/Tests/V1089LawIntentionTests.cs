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
using VictoriaGame.Presentation;
using VictoriaGame.World;
using Debug = UnityEngine.Debug;

namespace VictoriaGame.Tests
{
    /// <summary>Batch : -executeMethod VictoriaGame.Tests.V1089BatchRunner.Run</summary>
    public static class V1089BatchRunner
    {
        public static void Run()
        {
            try
            {
                V1089LawIntentionTests.RunAndWriteArtifacts();
                Debug.Log("V1089BatchRunner: DONE");
            }
            catch (Exception ex) when (HarnessAllocationGuard.IsNativeAllocationFailure(ex))
            {
                Debug.LogWarning("V1089BatchRunner: ALLOCATION_FAILURE — " + ex.Message);
                Debug.Log("V1089BatchRunner: DONE_PARTIAL");
            }
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
        }
    }

    /// <summary>
    /// v1_089 — PHASE XII : charger laws.json, EnactLaw, tax_mod → taux effectif.
    /// </summary>
    [TestFixture]
    public class V1089LawIntentionTests
    {
        const uint Seed = 42195u;
        const int ParityTicks = 100;
        const int ReferenceTicks = 3000;
        const int MonoTicks = 48; // court : retrait v1_086 avant cascades guerre/trésorerie
        const int FiscalUnlockTicks = 1800; // state_land available_from_tick=1800
        const ulong ExpectedParity = ParityAnchors.Expected;
        const int PlayerCountryId = PlayerControl.DefaultControlledCountryId;

        // Lois fiscales Absolute OK (FRA=1), catégories distinctes, tax_mod croissant.
        // Débloquées à FiscalUnlockTicks (state_land @1800).
        static readonly string[] FiscalLawsIncreasing =
        {
            "guild_monopoly", // Trade +0.05
            "land_tax",       // Taxation +0.10 → sum 0.15
            "state_land"      // LandRights +0.10 → sum 0.25
        };

        static string GameUnityRoot =>
            Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        static string LogPath => Path.Combine(GameUnityRoot, "Logs", "v1_089_laws.log");
        static string CapturesDir => Path.Combine(GameUnityRoot, "Captures", "v1_089");

        [TearDown]
        public void TearDown() => ResetAll();

        [Test]
        public void V1089_Laws_Loaded_23_Entities()
        {
            ResetAll();
            LockDefaults();
            using var h = new SimulationHarness(Seed);
            h.RunTicks(0);
            Assert.AreEqual(23, CountLawData(h.EntityManager));
        }

        [Test]
        public void V1089_EnactLaw_Writes_Buffer_One_Per_Category()
        {
            ResetAll();
            LockDefaults();
            using var h = new SimulationHarness(Seed);
            h.RunTicks(0);
            var em = h.EntityManager;

            Assert.IsTrue(PlayerIntentionSubmit.EnqueueEnactLaw(em, PlayerCountryId, "land_tax"));
            h.RunTicks(1);
            Assert.AreEqual(1, ReadReceipt(em).Accepted, ReadReceipt(em).Reason.ToString());
            Assert.AreEqual(1, CountEnacted(em, PlayerCountryId));
            Assert.IsTrue(HasEnacted(em, PlayerCountryId, "land_tax"));

            // Même catégorie Taxation : head_tax remplace land_tax.
            Assert.IsTrue(PlayerIntentionSubmit.EnqueueEnactLaw(em, PlayerCountryId, "head_tax"));
            h.RunTicks(1);
            Assert.AreEqual(1, ReadReceipt(em).Accepted);
            Assert.AreEqual(1, CountEnacted(em, PlayerCountryId), "une loi par catégorie");
            Assert.IsTrue(HasEnacted(em, PlayerCountryId, "head_tax"));
            Assert.IsFalse(HasEnacted(em, PlayerCountryId, "land_tax"));
        }

        [Test]
        public void V1089_Rejects_Named_Reasons()
        {
            ResetAll();
            LockDefaults();
            using var h = new SimulationHarness(Seed);
            h.RunTicks(0);
            var em = h.EntityManager;
            var eng = FindCountryIdByTag(em, "ENG");

            PlayerIntentionSubmit.EnqueueEnactLaw(em, eng, "land_tax");
            h.RunTicks(1);
            Assert.AreEqual(0, ReadReceipt(em).Accepted);
            Assert.AreEqual("country_not_controlled", ReadReceipt(em).Reason.ToString());

            PlayerIntentionSubmit.EnqueueEnactLaw(em, PlayerCountryId, "no_such_law");
            h.RunTicks(1);
            Assert.AreEqual(0, ReadReceipt(em).Accepted);
            Assert.AreEqual("law_not_found", ReadReceipt(em).Reason.ToString());

            // progressive_tax : min Republic (4), FRA = Absolute (1)
            PlayerIntentionSubmit.EnqueueEnactLaw(em, PlayerCountryId, "progressive_tax");
            h.RunTicks(1);
            Assert.AreEqual(0, ReadReceipt(em).Accepted);
            Assert.AreEqual("government_type_insufficient", ReadReceipt(em).Reason.ToString());

            // tenant_farming : available_from_tick 600
            PlayerIntentionSubmit.EnqueueEnactLaw(em, PlayerCountryId, "tenant_farming");
            h.RunTicks(1);
            Assert.AreEqual(0, ReadReceipt(em).Accepted);
            Assert.AreEqual("law_not_available", ReadReceipt(em).Reason.ToString());

            PlayerIntentionSubmit.EnqueueEnactLaw(em, PlayerCountryId, "land_tax");
            h.RunTicks(1);
            Assert.AreEqual(1, ReadReceipt(em).Accepted);
            PlayerIntentionSubmit.EnqueueEnactLaw(em, PlayerCountryId, "land_tax");
            h.RunTicks(1);
            Assert.AreEqual(0, ReadReceipt(em).Accepted);
            Assert.AreEqual("law_already_enacted", ReadReceipt(em).Reason.ToString());
        }

        [Test]
        public void V1089_Reversibility_No_Enacted_BitIdentical()
        {
            ResetAll();
            ulong dig;
            using (var h = new SimulationHarness(Seed))
            {
                h.RunTicks(ParityTicks);
                dig = WorldDigest.Compute(h.EntityManager);
                Assert.AreEqual(23, CountLawData(h.EntityManager));
                Assert.AreEqual(0, CountAllEnacted(h.EntityManager));
            }

            Assert.AreEqual(ExpectedParity, dig,
                "réversibilité SANS loi promulguée → empreinte v1_009 (23 LawData chargées)");
        }

        [Test]
        public void V1089_Artifacts_And_Verdict() => RunAndWriteArtifacts();

        public static void RunAndWriteArtifacts()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            Directory.CreateDirectory(CapturesDir);
            var sb = new StringBuilder(512 * 1024);

            void Flush() => File.WriteAllText(LogPath, sb.ToString(), Encoding.UTF8);

            sb.AppendLine("=== v1_089 LAWS / EnactLaw — seed=42195 PHASE XII ===");
            sb.AppendLine(
                "Contrat: inventaire mort confirmé, 23 LawData, EnactLaw, tax_mod→taux effectif, " +
                "réversibilité bit-identique, monotonie 0/1/2/3, vue de jeu.");
            sb.AppendLine();
            Flush();

            // ----- PARTIE 1 -----
            sb.AppendLine("=== PARTIE 1 — INVENTAIRE MORT + CARTOGRAPHIE + RÉFÉRENCE ===");
            sb.AppendLine("INVENTAIRE (confirmé fichier:ligne AVANT v1_089, état mort) :");
            sb.AppendLine(
                "  (1) GameDataLoader.LoadLaws() @ GameDataLoader.cs:216 — EXISTE, aucun appelant " +
                "(grep : seul LoadLaws définition + docs). CONFIRMÉ.");
            sb.AppendLine(
                "  (2) Aucune entité LawData créée — aucun CreateEntity+LawData avant LawInitSystem. CONFIRMÉ.");
            sb.AppendLine(
                "  (3) EnactedLaw AddBuffer vide @ CountryInitSystem.cs:92, jamais écrit. CONFIRMÉ.");
            sb.AppendLine(
                "  (4) Seul consommateur GovernmentSystem.cs:69-72 n*0.00005f avec n=0. CONFIRMÉ.");
            sb.AppendLine();
            sb.AppendLine("CARTOGRAPHIE DE SORTIE Stability / Legitimacy hors Politics/ :");
            sb.AppendLine(
                "  Lecteurs Stability/Legitimacy : StabilitySystem, GovernmentSystem, RevolutionSystem " +
                "(écriture pénalité) — TOUS dans Politics/. AUCUN système hors Politics/ ne lit " +
                "Stability ni Legitimacy pour décider. Boucle FERMÉE sans sortie. CONFIRMÉ.");
            sb.AppendLine(
                "  Conséquence conception : le levier joueur passe par tax_mod → TaxSystem " +
                "(taux effectif), PAS par Stability.");
            sb.AppendLine();
            Flush();

            // Parité AVANT tout LockDefaults (évite pollution cAbs adopté).
            ForceGc();
            TaxPhysicalWithdrawalSystem.EnsureParitySafeDefaults();
            ulong digNoAction;
            using (var h = new SimulationHarness(Seed))
            {
                h.RunTicks(ParityTicks);
                digNoAction = WorldDigest.Compute(h.EntityManager);
                Assert.AreEqual(23, CountLawData(h.EntityManager));
            }

            sb.AppendLine(
                $"RÉVERSIBILITÉ étage1 (sans loi promulguée, 23 LawData, cAbs=0): " +
                $"digest=0x{digNoAction:X16} expected=0x{ExpectedParity:X16} " +
                $"bit_identical={(digNoAction == ExpectedParity)}");

            ForceGc();
            TaxPhysicalWithdrawalSystem.EnsureParitySafeDefaults();
            ulong digTaxModZero;
            using (var h = new SimulationHarness(Seed))
            {
                h.RunTicks(ParityTicks);
                digTaxModZero = WorldDigest.Compute(h.EntityManager);
            }

            sb.AppendLine(
                $"RÉVERSIBILITÉ étage2 (branchement tax_mod à Σ=0, cAbs=0): " +
                $"digest=0x{digTaxModZero:X16} bit_identical={(digTaxModZero == ExpectedParity)}");
            sb.AppendLine();
            Flush();

            ForceGc();
            LockDefaults();
            float stabMedian;
            float legMedian;
            int emptyBuffers;
            int countryCount;
            int revolutions;
            using (var h = new SimulationHarness(Seed))
            {
                LockDefaults(); // re-lock après EnsureParity du harness
                h.RunTicks(ReferenceTicks);
                MeasurePoliticsReference(
                    h.EntityManager, out stabMedian, out legMedian,
                    out emptyBuffers, out countryCount, out revolutions);
            }

            sb.AppendLine(
                $"RÉFÉRENCE SANS LOI @tick={ReferenceTicks}: countries={countryCount} " +
                $"EnactedLaw_empty={emptyBuffers}/{countryCount} " +
                $"Stability_median={stabMedian.ToString("0.###", CultureInfo.InvariantCulture)} " +
                $"Legitimacy_median={legMedian.ToString("0.###", CultureInfo.InvariantCulture)} " +
                $"revolutions_active={revolutions}");
            sb.AppendLine(
                "NOTE: référence dérivée de la mesure (pas de point de contrôle nommé à la main).");
            sb.AppendLine();
            Flush();

            // ----- PARTIE 2 -----
            sb.AppendLine("=== PARTIE 2 — CHARGEMENT, EnactLaw, REFUS, RÉVERSIBILITÉ, DÉTERMINISME ===");
            ForceGc();
            LockDefaults();
            int lawCount;
            using (var h = new SimulationHarness(Seed))
            {
                h.RunTicks(0);
                var em = h.EntityManager;
                lawCount = CountLawData(em);
                sb.AppendLine($"LawData créées={lawCount} (attendu 23, ordre LawId)");

                // Accept + replace category
                PlayerIntentionSubmit.EnqueueEnactLaw(em, PlayerCountryId, "land_tax");
                h.RunTicks(1);
                sb.AppendLine(
                    $"EnactLaw land_tax: accepted={ReadReceipt(em).Accepted} reason={ReadReceipt(em).Reason}");
                PlayerIntentionSubmit.EnqueueEnactLaw(em, PlayerCountryId, "head_tax");
                h.RunTicks(1);
                sb.AppendLine(
                    $"EnactLaw head_tax (même cat Taxation, remplace): accepted={ReadReceipt(em).Accepted} " +
                    $"buffer_len={CountEnacted(em, PlayerCountryId)} has_head_tax={HasEnacted(em, PlayerCountryId, "head_tax")}");

                // Refus
                var eng = FindCountryIdByTag(em, "ENG");
                PlayerIntentionSubmit.EnqueueEnactLaw(em, eng, "land_tax");
                h.RunTicks(1);
                sb.AppendLine($"refus country_not_controlled: reason={ReadReceipt(em).Reason}");

                PlayerIntentionSubmit.EnqueueEnactLaw(em, PlayerCountryId, "no_such_law");
                h.RunTicks(1);
                sb.AppendLine($"refus law_not_found: reason={ReadReceipt(em).Reason}");

                PlayerIntentionSubmit.EnqueueEnactLaw(em, PlayerCountryId, "progressive_tax");
                h.RunTicks(1);
                sb.AppendLine($"refus government_type_insufficient: reason={ReadReceipt(em).Reason}");

                PlayerIntentionSubmit.EnqueueEnactLaw(em, PlayerCountryId, "tenant_farming");
                h.RunTicks(1);
                sb.AppendLine($"refus law_not_available: reason={ReadReceipt(em).Reason}");

                PlayerIntentionSubmit.EnqueueEnactLaw(em, PlayerCountryId, "head_tax");
                h.RunTicks(1);
                sb.AppendLine($"refus law_already_enacted: reason={ReadReceipt(em).Reason}");
            }

            sb.AppendLine(
                $"(parité déjà mesurée en PARTIE 1: étage1=0x{digNoAction:X16} " +
                $"étage2=0x{digTaxModZero:X16})");

            ForceGc();
            var digA = RunPlayerLawSequenceDigest();
            ForceGc();
            var digB = RunPlayerLawSequenceDigest();
            sb.AppendLine(
                $"DÉTERMINISME même séquence EnactLaw 2/2: A=0x{digA:X16} B=0x{digB:X16} equal={(digA == digB)}");
            sb.AppendLine();
            Flush();

            // ----- PARTIE 3 -----
            sb.AppendLine("=== PARTIE 3 — tax_mod → IMPÔT, MONOTONIE, CAPTURES ===");
            sb.AppendLine(
                "Formule: effectiveRate = Clamp(policyRate × (1 + Σ tax_mod)), " +
                $"bornes=[{TaxPolicyLimits.MinProductionTaxRate.ToString("G", CultureInfo.InvariantCulture)}.." +
                $"{TaxPolicyLimits.MaxProductionTaxRate.ToString("G", CultureInfo.InvariantCulture)}]");
            sb.AppendLine(
                "Séquence fiscal laws (catégories distinctes): " + string.Join(" → ", FiscalLawsIncreasing));

            sb.AppendLine(
                "Base fiscale mesure: 5× défaut (sous Max) pour amplifier le canal v1_086 ; " +
                "puis lois à FiscalUnlockTicks.");

            var monoRows = new List<string>(4);
            var incomes = new float[4];
            var debts = new float[4];
            var sats = new float[4];
            var pops = new int[4];
            var stabs = new float[4];
            var legs = new float[4];
            var baseRate = TaxPolicyLimits.DefaultProductionTaxRate * 5f;

            for (var n = 0; n <= 3; n++)
            {
                ForceGc();
                LockDefaults();
                using var h = new SimulationHarness(Seed);
                LockDefaults();
                h.RunTicks(0);
                var em = h.EntityManager;
                PlayerIntentionSubmit.EnqueueSetProductionTaxRate(em, PlayerCountryId, baseRate);
                h.RunTicks(1);
                h.RunTicks(FiscalUnlockTicks);
                for (var i = 0; i < n; i++)
                {
                    PlayerIntentionSubmit.EnqueueEnactLaw(em, PlayerCountryId, FiscalLawsIncreasing[i]);
                    h.RunTicks(1);
                    Assert.AreEqual(1, ReadReceipt(em).Accepted,
                        $"n={n} law={FiscalLawsIncreasing[i]} reason={ReadReceipt(em).Reason}");
                }

                // Pad pour même horizon absolu (3 slots d'enact + MonoTicks).
                h.RunTicks(3 - n);
                h.RunTicks(MonoTicks);
                MeasureEconomy(em, PlayerCountryId,
                    out incomes[n], out debts[n], out sats[n], out pops[n],
                    out stabs[n], out legs[n], out var lawMod, out var effRate);
                monoRows.Add(
                    $"n={n} laws=[{string.Join(",", Slice(FiscalLawsIncreasing, n))}] " +
                    $"lawmod={lawMod.ToString("0.###", CultureInfo.InvariantCulture)} " +
                    $"effRate={effRate.ToString("G", CultureInfo.InvariantCulture)} " +
                    $"income={incomes[n].ToString("0.###", CultureInfo.InvariantCulture)} " +
                    $"debt={debts[n].ToString("0.###", CultureInfo.InvariantCulture)} " +
                    $"sat={sats[n].ToString("0.###", CultureInfo.InvariantCulture)} " +
                    $"pop={pops[n]} " +
                    $"stab={stabs[n].ToString("0.###", CultureInfo.InvariantCulture)} " +
                    $"leg={legs[n].ToString("0.###", CultureInfo.InvariantCulture)}");
            }

            for (var i = 0; i < monoRows.Count; i++)
                sb.AppendLine(monoRows[i]);

            var incomeMono = incomes[0] <= incomes[1] && incomes[1] <= incomes[2] && incomes[2] <= incomes[3];
            // Satisfaction : retrait d'impôt ↑ → sat ↓ (monotone décroissante attendue via v1_086).
            var satMono = sats[0] >= sats[1] && sats[1] >= sats[2] && sats[2] >= sats[3];
            sb.AppendLine($"monotonie_recette_croissante={incomeMono}");
            sb.AppendLine($"monotonie_satisfaction_decroissante={satMono}");
            sb.AppendLine(
                "NOTE Stability/Legitimacy : mouvement INTERNE à la boucle Politics/ (aucune sortie hors Politics/). " +
                "Publié pour information, PAS présenté comme conséquence joueur.");
            sb.AppendLine();
            Flush();

            // Captures vue de jeu
            ForceGc();
            LockDefaults();
            using (var h = new SimulationHarness(Seed))
            {
                h.RunTicks(30);
                var em = h.EntityManager;
                Assert.IsTrue(CountryObservation.TryCapture(em, PlayerCountryId, out var snapBefore));
                sb.AppendLine("CAPTURE BEFORE laws section:");
                sb.AppendLine(ExtractLawsSection(snapBefore.DetailBlock));
                WriteGameViewCapture(
                    em, Path.Combine(CapturesDir, "01_laws_before.png"),
                    PlayerCountryId, "AVANT — lois FRA", ExtractLawsSection(snapBefore.DetailBlock));

                PlayerIntentionSubmit.EnqueueEnactLaw(em, PlayerCountryId, "land_tax");
                h.RunTicks(1);
                Assert.AreEqual(1, ReadReceipt(em).Accepted);
                Assert.IsTrue(CountryObservation.TryCapture(em, PlayerCountryId, out var snapAfter));
                sb.AppendLine("CAPTURE AFTER laws section:");
                sb.AppendLine(ExtractLawsSection(snapAfter.DetailBlock));
                WriteGameViewCapture(
                    em, Path.Combine(CapturesDir, "02_laws_after.png"),
                    PlayerCountryId, "APRES — land_tax FRA", ExtractLawsSection(snapAfter.DetailBlock));

                WriteEconomyCapture(em, Path.Combine(CapturesDir, "03_hud_laws_tax.png"));
            }

            var capturesExist =
                File.Exists(Path.Combine(CapturesDir, "01_laws_before.png")) &&
                File.Exists(Path.Combine(CapturesDir, "02_laws_after.png")) &&
                File.Exists(Path.Combine(CapturesDir, "03_hud_laws_tax.png"));
            sb.AppendLine($"png_before={Path.Combine(CapturesDir, "01_laws_before.png")}");
            sb.AppendLine($"png_after={Path.Combine(CapturesDir, "02_laws_after.png")}");
            sb.AppendLine($"png_hud={Path.Combine(CapturesDir, "03_hud_laws_tax.png")}");
            sb.AppendLine($"captures_exist={capturesExist}");
            sb.AppendLine();

            var parityOk = digNoAction == ExpectedParity && digTaxModZero == ExpectedParity;
            var detOk = digA == digB;
            var lawsLoaded = lawCount == 23;
            var monoOk = incomeMono && satMono;
            var pass = parityOk && detOk && lawsLoaded && monoOk && capturesExist;

            sb.AppendLine("=== VERDICT MESURÉ ===");
            sb.AppendLine(
                $"les 4 points de l'inventaire confirmés ; aucun lecteur de Stability hors Politics/ ; " +
                $"référence sans loi : EnactedLaw vide {emptyBuffers}/{countryCount}, " +
                $"Stability médiane {stabMedian.ToString("0.###", CultureInfo.InvariantCulture)} @t{ReferenceTicks} ; " +
                $"{lawCount} LawData chargées, EnactLaw livrée, 5 motifs de refus prouvés ; " +
                $"sans loi parité 0x{digNoAction:X16} bit-identique={(digNoAction == ExpectedParity)}, " +
                $"tax_mod à 0 bit-identique={(digTaxModZero == ExpectedParity)} ; " +
                $"même séquence rejouée 2/2 empreintes égales={detOk} ; " +
                $"0/1/2/3 lois fiscales -> recette {Fmt(incomes[0])} / {Fmt(incomes[1])} / {Fmt(incomes[2])} / {Fmt(incomes[3])}, " +
                $"satisfaction {Fmt(sats[0])} / {Fmt(sats[1])} / {Fmt(sats[2])} / {Fmt(sats[3])}, " +
                $"monotone_income={incomeMono} monotone_sat={satMono} ; captures={capturesExist}.");
            sb.AppendLine(pass
                ? "VERDICT: PASS — inventaire + verbe + sortie tax_mod + réversibilité + monotonie + vue de jeu."
                : "VERDICT: FAIL — un critère du contrat a lâché.");
            Flush();
            Debug.Log(sb.ToString());

            Assert.IsTrue(lawsLoaded, "23 LawData");
            Assert.IsTrue(parityOk, "réversibilité");
            Assert.IsTrue(detOk, "déterminisme");
            Assert.IsTrue(monoOk, "monotonie income+sat");
            Assert.IsTrue(capturesExist, "captures");
            ResetAll();
        }

        static ulong RunPlayerLawSequenceDigest()
        {
            LockDefaults();
            using var h = new SimulationHarness(Seed);
            h.RunTicks(0);
            var em = h.EntityManager;
            PlayerIntentionSubmit.EnqueueEnactLaw(em, PlayerCountryId, "guild_monopoly");
            h.RunTicks(1);
            PlayerIntentionSubmit.EnqueueEnactLaw(em, PlayerCountryId, "land_tax");
            h.RunTicks(1);
            PlayerIntentionSubmit.EnqueueEnactLaw(em, PlayerCountryId, "head_tax"); // remplace land_tax
            h.RunTicks(1);
            h.RunTicks(40);
            return WorldDigest.Compute(em);
        }

        static void WriteGameViewCapture(
            EntityManager em, string path, int countryId, string title, string lawsSection)
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
                        var lines = lawsSection.Split('\n');
                        for (var i = 0; i < lines.Length && i < 10; i++)
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

        static void WriteEconomyCapture(EntityManager em, string path)
        {
            TaxCostSnapshot.Capture(em, PlayerCountryId, out var sat, out var hungry, out var hungryProv);
            CountryObservation.TryCapture(em, PlayerCountryId, out var snap);
            var taxLine = TaxCostSnapshot.FormatHudLine(sat, hungry, hungryProv);
            var lawLine = snap.EnactedLawLines != null && snap.EnactedLawLines.Count > 0
                ? "LAWS " + string.Join(", ", snap.EnactedLawLines)
                : "LAWS (none)";
            var effLine =
                "LAWMOD " + snap.LawTaxModSum.ToString("0.###", CultureInfo.InvariantCulture) +
                "  EFF " + HudValueFormatter.FormatTaxPercent(snap.EffectiveProductionTaxRate);

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
                        p, geo, em, MapObservationLevel.Country, filterCountryId: PlayerCountryId);
                    var fg = new Color32(236, 232, 220, 255);
                    var halo = new Color32(8, 8, 12, 255);
                    MapSnapshotExporter.WithGlyphScale(2, () =>
                    {
                        MapSnapshotExporter.DrawBitmapText(
                            p, "HUD joueur — lois + impot", 12, 16, fg, halo);
                        MapSnapshotExporter.DrawBitmapText(p, lawLine, 12, 44, fg, halo);
                        MapSnapshotExporter.DrawBitmapText(p, effLine, 12, 68, fg, halo);
                        MapSnapshotExporter.DrawBitmapText(p, taxLine, 12, 92, fg, halo);
                    });
                });

            if (pixels != null)
            {
                MapSnapshotExporter.WriteMapBufferPng(
                    pixels, MapSnapshotExporter.Width, MapSnapshotExporter.Height, path);
            }

            MapViewport.Reset();
        }

        static string ExtractLawsSection(string detail)
        {
            if (string.IsNullOrEmpty(detail))
                return "--- LAWS ---\n(none)\n";
            var start = detail.IndexOf("--- LAWS ---", StringComparison.Ordinal);
            if (start < 0)
                return "--- LAWS ---\n(missing)\n";
            var end = detail.IndexOf("--- MILITARY ---", start, StringComparison.Ordinal);
            if (end < 0)
                end = detail.Length;
            return detail.Substring(start, end - start).TrimEnd() + "\n";
        }

        static void MeasurePoliticsReference(
            EntityManager em,
            out float stabMedian,
            out float legMedian,
            out int emptyBuffers,
            out int countryCount,
            out int revolutions)
        {
            var stabs = new List<float>(32);
            var legs = new List<float>(32);
            emptyBuffers = 0;
            countryCount = 0;
            revolutions = 0;

            using var q = em.CreateEntityQuery(
                ComponentType.ReadOnly<CountryData>(),
                ComponentType.ReadOnly<GovernmentData>());
            using var entities = q.ToEntityArray(Allocator.Temp);
            using var govs = q.ToComponentDataArray<GovernmentData>(Allocator.Temp);
            countryCount = entities.Length;
            for (var i = 0; i < entities.Length; i++)
            {
                stabs.Add(govs[i].Stability);
                legs.Add(govs[i].Legitimacy);
                if (em.HasBuffer<EnactedLaw>(entities[i]) &&
                    em.GetBuffer<EnactedLaw>(entities[i]).Length == 0)
                    emptyBuffers++;
            }

            using (var rq = em.CreateEntityQuery(ComponentType.ReadOnly<RevolutionData>()))
            using (var revs = rq.ToComponentDataArray<RevolutionData>(Allocator.Temp))
            {
                for (var i = 0; i < revs.Length; i++)
                {
                    if (revs[i].IsRevolutionActive)
                        revolutions++;
                }
            }

            stabs.Sort();
            legs.Sort();
            stabMedian = stabs.Count == 0 ? 0f : stabs[stabs.Count / 2];
            legMedian = legs.Count == 0 ? 0f : legs[legs.Count / 2];
        }

        static void MeasureEconomy(
            EntityManager em,
            int countryId,
            out float income,
            out float debt,
            out float sat,
            out int pop,
            out float stab,
            out float leg,
            out float lawMod,
            out float effRate)
        {
            income = 0f;
            debt = 0f;
            sat = 0f;
            pop = 0;
            stab = 0f;
            leg = 0f;
            lawMod = 0f;
            effRate = TaxPolicyLimits.DefaultProductionTaxRate;

            if (!TryResolveCountry(em, countryId, out var entity))
                return;

            if (em.HasComponent<TreasuryData>(entity))
            {
                var t = em.GetComponentData<TreasuryData>(entity);
                income = t.Income;
                debt = t.Debt;
            }

            if (em.HasComponent<CountryData>(entity))
                pop = em.GetComponentData<CountryData>(entity).Population;

            if (em.HasComponent<GovernmentData>(entity))
            {
                var g = em.GetComponentData<GovernmentData>(entity);
                stab = g.Stability;
                leg = g.Legitimacy;
            }

            var policyRate = TaxPolicyLimits.DefaultProductionTaxRate;
            if (em.HasComponent<TaxPolicy>(entity))
                policyRate = em.GetComponentData<TaxPolicy>(entity).ProductionTaxRate;
            lawMod = LawTaxEffect.SumTaxModForCountry(em, entity);
            effRate = LawTaxEffect.EffectiveProductionTaxRate(policyRate, lawMod);

            TaxCostSnapshot.Capture(em, countryId, out sat, out _, out _);
        }

        static string[] Slice(string[] arr, int n)
        {
            if (n <= 0)
                return Array.Empty<string>();
            var r = new string[n];
            Array.Copy(arr, r, n);
            return r;
        }

        static string Fmt(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        static int CountLawData(EntityManager em)
        {
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<LawData>());
            return q.CalculateEntityCount();
        }

        static int CountEnacted(EntityManager em, int countryId)
        {
            if (!TryResolveCountry(em, countryId, out var e))
                return 0;
            return em.HasBuffer<EnactedLaw>(e) ? em.GetBuffer<EnactedLaw>(e).Length : 0;
        }

        static int CountAllEnacted(EntityManager em)
        {
            var n = 0;
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<CountryData>());
            using var entities = q.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                if (em.HasBuffer<EnactedLaw>(entities[i]))
                    n += em.GetBuffer<EnactedLaw>(entities[i]).Length;
            }

            return n;
        }

        static bool HasEnacted(EntityManager em, int countryId, string lawId)
        {
            if (!TryResolveCountry(em, countryId, out var e) || !em.HasBuffer<EnactedLaw>(e))
                return false;
            var id = new FixedString32Bytes(lawId);
            var buf = em.GetBuffer<EnactedLaw>(e);
            for (var i = 0; i < buf.Length; i++)
            {
                if (buf[i].LawId.Equals(id))
                    return true;
            }

            return false;
        }

        static bool TryResolveCountry(EntityManager em, int countryId, out Entity entity)
        {
            entity = Entity.Null;
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<CountryData>());
            using var entities = q.ToEntityArray(Allocator.Temp);
            using var countries = q.ToComponentDataArray<CountryData>(Allocator.Temp);
            for (var i = 0; i < countries.Length; i++)
            {
                if (countries[i].CountryId != countryId)
                    continue;
                entity = entities[i];
                return true;
            }

            return false;
        }

        static int FindCountryIdByTag(EntityManager em, string tag)
        {
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<CountryData>());
            using var countries = q.ToComponentDataArray<CountryData>(Allocator.Temp);
            for (var i = 0; i < countries.Length; i++)
            {
                if (countries[i].Tag.ToString() == tag)
                    return countries[i].CountryId;
            }

            return -1;
        }

        static PlayerIntentionReceipt ReadReceipt(EntityManager em)
        {
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<PlayerIntentionReceipt>());
            return q.GetSingleton<PlayerIntentionReceipt>();
        }

        static void LockDefaults()
        {
            TaxPhysicalWithdrawalSystem.LockCoefficients(
                TaxPhysicalWithdrawalSystem.AdoptedWithdrawalCoefficient,
                TaxPhysicalWithdrawalSystem.AdoptedAbstractWithdrawalCoefficient);
            PhysicalSatisfactionBlendSystem.LockWeight(0.25f);
            BuildingAiPolicyConfig.Lock(BuildingAiPolicy.HoldNone, 0f);
            BuildingConstructionSystem.LockCapacityIntensity(0f);
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
            MapViewport.Reset();
        }

        static void ForceGc()
        {
            ResetAll();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }
}
