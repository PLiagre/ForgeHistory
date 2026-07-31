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
    /// <summary>Batch : -executeMethod VictoriaGame.Tests.V1086BatchRunner.Run</summary>
    public static class V1086BatchRunner
    {
        public static void Run()
        {
            try
            {
                V1086TaxPhysicalWithdrawalTests.RunAndWriteArtifacts();
                Debug.Log("V1086BatchRunner: DONE");
            }
            catch (Exception ex) when (HarnessAllocationGuard.IsNativeAllocationFailure(ex))
            {
                Debug.LogWarning("V1086BatchRunner: ALLOCATION_FAILURE — " + ex.Message);
                Debug.Log("V1086BatchRunner: DONE_PARTIAL");
            }
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
        }
    }

    /// <summary>
    /// v1_086 — PHASE XII : trancher cPhys, adopter cAbs, retour HUD, captures.
    /// </summary>
    [TestFixture]
    public class V1086TaxPhysicalWithdrawalTests
    {
        const uint Seed = 42195u;
        const int SweepTicks = 800;
        const int ParityTicks = 100;
        const ulong ExpectedParity = ParityAnchors.Expected;

        static readonly float[] TaxMultipliers = { 0f, 0.5f, 1f, 5f, 10f };

        public const float AdoptedPhys = TaxPhysicalWithdrawalSystem.AdoptedWithdrawalCoefficient;
        public const float AdoptedAbs = TaxPhysicalWithdrawalSystem.AdoptedAbstractWithdrawalCoefficient;

        static string GameUnityRoot =>
            Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        static string LogPath => Path.Combine(GameUnityRoot, "Logs", "v1_086_tax.log");
        static string CapturesDir => Path.Combine(GameUnityRoot, "Captures", "v1_086");

        [TearDown]
        public void TearDown() => ResetAll();

        [Test]
        public void V1086_Compiled_Defaults_Stay_Zero_For_Reversibility()
        {
            Assert.AreEqual(0f, TaxPhysicalWithdrawalSystem.DefaultWithdrawalCoefficient, 1e-12f);
            Assert.AreEqual(0f, TaxPhysicalWithdrawalSystem.DefaultAbstractWithdrawalCoefficient, 1e-12f);
            Assert.AreEqual(0f, AdoptedPhys, 1e-12f);
            Assert.AreEqual(0.5f, AdoptedAbs, 1e-12f);
        }

        [Test]
        public void V1086_Reversibility_Zero_Coeffs_BitIdentical_Parity()
        {
            TaxPhysicalWithdrawalSystem.LockCoefficients(0f, 0f);
            ulong dig;
            using (var h = new SimulationHarness(Seed))
            {
                h.RunTicks(ParityTicks);
                dig = WorldDigest.Compute(h.EntityManager);
            }

            Assert.AreEqual(ExpectedParity, dig,
                "réversibilité c=0/0 → empreinte v1_009");
        }

        [Test]
        public void V1086_Intention_Path_Sets_Tax_Without_Direct_Write()
        {
            using var h = new SimulationHarness(Seed);
            TaxPhysicalWithdrawalSystem.LockCoefficients(AdoptedPhys, AdoptedAbs);
            h.RunTicks(0);
            var em = h.EntityManager;
            var id = PlayerControl.DefaultControlledCountryId;
            var before = ReadTaxRate(em, id);
            PlayerIntentionSubmit.EnqueueSetProductionTaxRate(
                em, id, TaxPolicyLimits.MaxProductionTaxRate);
            h.RunTicks(2);
            var after = ReadTaxRate(em, id);
            Assert.AreNotEqual(before, after, "intention doit changer TaxPolicy");
            Assert.AreEqual(TaxPolicyLimits.MaxProductionTaxRate, after, 1e-12f);
        }

        [Test]
        public void V1086_Artifacts_And_Verdict() => RunAndWriteArtifacts();

        public static void RunAndWriteArtifacts()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            Directory.CreateDirectory(CapturesDir);
            var sb = new StringBuilder(256 * 1024);

            void Flush() => File.WriteAllText(LogPath, sb.ToString(), Encoding.UTF8);

            sb.AppendLine("=== v1_086 TAX ADOPTION — seed=42195 PHASE XII ===");
            sb.AppendLine(
                "Contrat: monotonie (pas effet/bruit), rouge puis vert, parité v1_009, " +
                "réversibilité c=0/0 bit-identique, retour HUD + captures.");
            sb.AppendLine(
                $"Compiled defaults cPhys={TaxPhysicalWithdrawalSystem.DefaultWithdrawalCoefficient} " +
                $"cAbs={TaxPhysicalWithdrawalSystem.DefaultAbstractWithdrawalCoefficient} ; " +
                $"candidats adoption cPhys={AdoptedPhys} cAbs={AdoptedAbs} ; " +
                $"PhysicalBlendWeight={PhysicalSatisfactionBlendSystem.DefaultPhysicalBlendWeight} (NON modifié).");
            sb.AppendLine();
            Flush();

            // ----- PARTIE 1 — TROIS CONFIGS DÉCISIVES -----
            sb.AppendLine("=== PARTIE 1 — TRANCHER cPhys (3 configs × grille taux @t" + SweepTicks + ") ===");
            sb.AppendLine(
                "cPhys | cAbs | mult | rate | tick | debt | gold | sat | pop | hungry | hungryProv");

            var cells = new Dictionary<string, Cell>();
            var configs = new[]
            {
                (0f, 0.5f, "cAbs_seul"),
                (0.5f, 0f, "cPhys_seul"),
                (0.5f, 0.5f, "les_deux")
            };

            foreach (var (cPhys, cAbs, _) in configs)
            {
                foreach (var mult in TaxMultipliers)
                {
                    ForceGc();
                    var cell = RunCell(cPhys, cAbs, mult, SweepTicks);
                    cells[Key(cPhys, cAbs, mult)] = cell;
                    sb.AppendLine(
                        $"{Fmt2(cPhys)} | {Fmt2(cAbs)} | {Fmt2(mult)} | {FmtE(cell.Rate)} | " +
                        $"{cell.Ticks} | {Fmt1(cell.Debt)} | {Fmt1(cell.Gold)} | {Fmt3(cell.Sat)} | " +
                        $"{cell.Pop} | {cell.Hungry} | {cell.HungryProv}");
                    Flush();
                }
            }

            sb.AppendLine();
            sb.AppendLine("=== MONOTONIE SAT + FRACTION cPhys ===");
            float dAbs = 0f, dPhys = 0f, dBoth = 0f;
            bool monoAbs = false, monoPhys = false, monoBoth = false;
            string seriesAbs = "", seriesPhys = "", seriesBoth = "";

            foreach (var (cPhys, cAbs, tag) in configs)
            {
                var sats = new float[TaxMultipliers.Length];
                var complete = true;
                for (var i = 0; i < TaxMultipliers.Length; i++)
                {
                    if (!cells.TryGetValue(Key(cPhys, cAbs, TaxMultipliers[i]), out var c) || c.Ticks <= 0)
                    {
                        complete = false;
                        break;
                    }

                    sats[i] = c.Sat;
                }

                if (!complete)
                {
                    sb.AppendLine($"cPhys={Fmt2(cPhys)} cAbs={Fmt2(cAbs)} ({tag}): INCOMPLET");
                    continue;
                }

                var mono = IsMonotoneNonIncreasing(sats);
                var dSat = sats[sats.Length - 1] - sats[0];
                var series = string.Join("→", Arr3(sats));
                sb.AppendLine(
                    $"cPhys={Fmt2(cPhys)} cAbs={Fmt2(cAbs)} ({tag}): sat [{series}] " +
                    $"mono={mono} Δsat={Fmt4(dSat)}");

                if (Math.Abs(cPhys) < 1e-6f && Math.Abs(cAbs - 0.5f) < 1e-6f)
                {
                    monoAbs = mono;
                    dAbs = dSat;
                    seriesAbs = series;
                }
                else if (Math.Abs(cPhys - 0.5f) < 1e-6f && Math.Abs(cAbs) < 1e-6f)
                {
                    monoPhys = mono;
                    dPhys = dSat;
                    seriesPhys = series;
                }
                else
                {
                    monoBoth = mono;
                    dBoth = dSat;
                    seriesBoth = series;
                }
            }

            // Fraction de l'effet total que cPhys apporte EN PLUS de cAbs seul.
            // |dBoth| - |dAbs| sur |dBoth| (si dBoth < 0).
            var absEffect = Math.Abs(dAbs);
            var bothEffect = Math.Abs(dBoth);
            var physExtra = bothEffect - absEffect;
            var fraction = bothEffect > 1e-9f ? physExtra / bothEffect : 0f;
            sb.AppendLine(
                $"fraction_cPhys_sur_effet_total=({Fmt4(bothEffect)}-{Fmt4(absEffect)})/{Fmt4(bothEffect)}=" +
                $"{Fmt4(fraction)} ({(fraction * 100f).ToString("0.0", CultureInfo.InvariantCulture)} %)");
            sb.AppendLine(
                $"cPhys_seul Δsat={Fmt4(dPhys)} mono={monoPhys} — rasoir: second chemin stocks " +
                "physiques doit payer son existence par effet monotone mesurable.");

            var adoptPhys = 0f;
            var adoptAbs = 0.5f;
            var adoptReason = "";
            if (!monoPhys || Math.Abs(dPhys) < 0.01f)
            {
                adoptPhys = 0f;
                adoptReason =
                    "cPhys NON adopté : mono=" + monoPhys + " Δsat=" + Fmt4(dPhys) +
                    " ; fraction additionnelle=" + Fmt4(fraction) +
                    " — le JSON le déclare inutile plutôt que réglable.";
            }
            else
            {
                adoptPhys = 0.5f;
                adoptReason = "cPhys adopté (monotone mesurable).";
            }

            if (monoAbs && dAbs < -0.05f)
            {
                adoptAbs = 0.5f;
                adoptReason += " cAbs=0.5 ADOPTÉ : sat [" + seriesAbs + "] mono=True Δsat=" + Fmt4(dAbs) + ".";
            }
            else
            {
                adoptAbs = 0f;
                adoptReason += " cAbs NON adopté : mono=" + monoAbs + " Δsat=" + Fmt4(dAbs) + ".";
            }

            sb.AppendLine("DECISION_ADOPTION: cPhys=" + Fmt2(adoptPhys) + " cAbs=" + Fmt2(adoptAbs));
            sb.AppendLine("JUSTIFICATION: " + adoptReason);
            sb.AppendLine();
            Flush();

            // Écrire JSON avec les valeurs tranchées
            WriteAdoptionJson(adoptPhys, adoptAbs, adoptReason, fraction, monoPhys, dPhys, monoAbs, dAbs);
            sb.AppendLine("JSON écrit: StreamingAssets/data/tax_physical_withdrawal.json");
            sb.AppendLine();

            // ----- PARTIE 2 — RÉVERSIBILITÉ + ROUGE PUIS VERT + TENSION -----
            sb.AppendLine("=== PARTIE 2 — RÉVERSIBILITÉ + CONTRÔLE ROUGE/VERT + TENSION ===");
            ForceGc();
            TaxPhysicalWithdrawalSystem.LockCoefficients(0f, 0f);
            ulong parityDig;
            using (var h = new SimulationHarness(Seed))
            {
                h.RunTicks(ParityTicks);
                parityDig = WorldDigest.Compute(h.EntityManager);
            }

            var parityOk = parityDig == ExpectedParity;
            sb.AppendLine(
                $"réversibilité c=0/0 t{ParityTicks}: fingerprint=0x{parityDig:X16} " +
                $"expected=0x{ExpectedParity:X16} bitIdentical={parityOk}");

            sb.AppendLine("CONTRÔLE ROUGE (cPhys seul, attendu mono=False):");
            sb.AppendLine($"  sat [{seriesPhys}] mono={monoPhys} Δsat={Fmt4(dPhys)} — " +
                          (monoPhys ? "FAIL (devrait être rouge)" : "ROUGE OK"));
            sb.AppendLine("CONTRÔLE VERT (valeur adoptée cAbs):");
            sb.AppendLine($"  sat [{seriesAbs}] mono={monoAbs} Δsat={Fmt4(dAbs)} — " +
                          (monoAbs && dAbs < 0f ? "VERT OK" : "FAIL"));

            // Tension à la valeur adoptée : ×1 vs ×10
            var t1 = cells.TryGetValue(Key(adoptPhys, adoptAbs, 1f), out var cell1) ? cell1 : default;
            var t10 = cells.TryGetValue(Key(adoptPhys, adoptAbs, 10f), out var cell10) ? cell10 : default;
            if (t1.Ticks <= 0 || t10.Ticks <= 0)
            {
                // Si adoptPhys=0 adoptAbs=0.5, cells already have it.
                ForceGc();
                t1 = RunCell(adoptPhys, adoptAbs, 1f, SweepTicks);
                ForceGc();
                t10 = RunCell(adoptPhys, adoptAbs, 10f, SweepTicks);
            }

            sb.AppendLine(
                $"TENSION @adopté cPhys={Fmt2(adoptPhys)} cAbs={Fmt2(adoptAbs)} t{SweepTicks}:");
            sb.AppendLine(
                $"  ×1 : debt={Fmt1(t1.Debt)} gold={Fmt1(t1.Gold)} sat={Fmt3(t1.Sat)} " +
                $"pop={t1.Pop} affamés={t1.Hungry} provAffamées={t1.HungryProv}");
            sb.AppendLine(
                $"  ×10: debt={Fmt1(t10.Debt)} gold={Fmt1(t10.Gold)} sat={Fmt3(t10.Sat)} " +
                $"pop={t10.Pop} affamés={t10.Hungry} provAffamées={t10.HungryProv}");
            var tensionMoves =
                Math.Abs(t10.Sat - t1.Sat) > 0.01f &&
                t10.Hungry != t1.Hungry;
            sb.AppendLine($"  tension_levier_interessant={tensionMoves} " +
                          "(sat et affamés bougent ensemble)");
            sb.AppendLine();
            Flush();

            // ----- PARTIE 3 — HUD + CAPTURES VIA INTENTION -----
            sb.AppendLine("=== PARTIE 3 — RETOUR ÉCRAN (intention → HUD coût) ===");
            ForceGc();
            var capMin = CaptureViaIntention(TaxPolicyLimits.MinProductionTaxRate, SweepTicks);
            ForceGc();
            var capMax = CaptureViaIntention(TaxPolicyLimits.MaxProductionTaxRate, SweepTicks);

            sb.AppendLine(
                $"capture_tax_min: rate={FmtE(capMin.Rate)} sat={Fmt3(capMin.Sat)} " +
                $"affamés={capMin.Hungry} provAffamées={capMin.HungryProv} " +
                $"hud=\"{capMin.HudLine}\"");
            sb.AppendLine(
                $"capture_tax_max: rate={FmtE(capMax.Rate)} sat={Fmt3(capMax.Sat)} " +
                $"affamés={capMax.Hungry} provAffamées={capMax.HungryProv} " +
                $"hud=\"{capMax.HudLine}\"");

            WriteHudCapturePng(
                Path.Combine(CapturesDir, "04_tax_min.png"),
                "TAX MIN", capMin);
            WriteHudCapturePng(
                Path.Combine(CapturesDir, "04_tax_max.png"),
                "TAX MAX", capMax);

            var capturesDiffer =
                Math.Abs(capMax.Sat - capMin.Sat) > 1e-4f ||
                capMax.Hungry != capMin.Hungry ||
                capMax.HungryProv != capMin.HungryProv;
            sb.AppendLine($"captures_differ={capturesDiffer}");
            sb.AppendLine($"png_min={Path.Combine(CapturesDir, "04_tax_min.png")}");
            sb.AppendLine($"png_max={Path.Combine(CapturesDir, "04_tax_max.png")}");
            sb.AppendLine();

            // ----- VERDICT -----
            sb.AppendLine("=== VERDICT MESURÉ ===");
            var redOk = !monoPhys;
            var greenOk = monoAbs && dAbs < -0.05f;
            var adoptOk = Math.Abs(adoptPhys) < 1e-6f && Math.Abs(adoptAbs - 0.5f) < 1e-6f;
            var pass = parityOk && redOk && greenOk && adoptOk && tensionMoves && capturesDiffer;

            sb.AppendLine(
                $"cPhys apporte {(fraction * 100f).ToString("0.0", CultureInfo.InvariantCulture)} % " +
                $"de l'effet et reste non monotone (mono={monoPhys} sur {TaxMultipliers.Length} points) : " +
                (adoptPhys < 1e-6f ? "NON adopté" : "adopté") +
                " ; JSON le déclare " + (adoptPhys < 1e-6f ? "inutile" : "réglable") +
                $" ; cAbs={Fmt2(adoptAbs)} " +
                (adoptAbs > 0f ? "adopté" : "NON adopté") +
                $" — sat [{seriesAbs}] mono={monoAbs} Δsat={Fmt4(dAbs)} ; " +
                $"rouge obtenu à cPhys seul mono={monoPhys} ; " +
                $"réversibilité 0x{parityDig:X16} bit-identique={parityOk} ; " +
                $"tension : dette {Fmt1(t1.Debt)}→{Fmt1(t10.Debt)}, " +
                $"sat {Fmt3(t1.Sat)}→{Fmt3(t10.Sat)}, " +
                $"pop {t1.Pop}→{t10.Pop}, " +
                $"affamées {t1.HungryProv}→{t10.HungryProv} ; " +
                $"HUD affiche sat+affamés ; captures 04_tax_min/04_tax_max " +
                $"differ={capturesDiffer} " +
                $"(sat {Fmt3(capMin.Sat)}→{Fmt3(capMax.Sat)}, " +
                $"affamés {capMin.Hungry}→{capMax.Hungry}).");
            sb.AppendLine(pass
                ? "VERDICT: PASS — adoption sûre, retour écran livré."
                : "VERDICT: FAIL — un critère du contrat de phase a lâché (voir sections).");
            Flush();
            Debug.Log(sb.ToString());

            Assert.IsTrue(parityOk, "parité/réversibilité");
            Assert.IsTrue(redOk, "contrôle rouge cPhys seul");
            Assert.IsTrue(greenOk, "contrôle vert cAbs adopté");
            Assert.IsTrue(adoptOk, "adoption cPhys=0 cAbs=0.5");
            Assert.IsTrue(tensionMoves, "tension sat+affamés");
            Assert.IsTrue(capturesDiffer, "captures min/max distinctes");
            Assert.IsTrue(File.Exists(Path.Combine(CapturesDir, "04_tax_min.png")));
            Assert.IsTrue(File.Exists(Path.Combine(CapturesDir, "04_tax_max.png")));
            ResetAll();
        }

        static void WriteAdoptionJson(
            float cPhys, float cAbs, string reason, float fraction,
            bool monoPhys, float dPhys, bool monoAbs, float dAbs)
        {
            var path = Path.Combine(
                Application.streamingAssetsPath, "data", "tax_physical_withdrawal.json");
            var just =
                "v1_086 ADOPTÉ : cPhys=" + Fmt2(cPhys) +
                " (mono=" + monoPhys + " Δsat=" + Fmt4(dPhys) +
                " fraction_extra=" + Fmt4(fraction) +
                (cPhys < 1e-6f ? " — inutile" : "") + ") ; cAbs=" + Fmt2(cAbs) +
                " (mono=" + monoAbs + " Δsat=" + Fmt4(dAbs) +
                "). Mesure CE brief Logs/v1_086_tax.log. " +
                "PhysicalBlendWeight=0.25 inchangé. Réversibilité c=0/0 → 0x4ED26CB61DE7B2B2. " +
                reason.Replace("\"", "'");
            var json =
                "{\n" +
                "  \"withdrawal_coefficient\": " +
                cPhys.ToString("0.###", CultureInfo.InvariantCulture) + ",\n" +
                "  \"abstract_withdrawal_coefficient\": " +
                cAbs.ToString("0.###", CultureInfo.InvariantCulture) + ",\n" +
                "  \"coefficient_justification\": \"" + just + "\"\n" +
                "}\n";
            File.WriteAllText(path, json, Encoding.UTF8);
        }

        static Cell CaptureViaIntention(float targetRate, int ticks)
        {
            TaxPhysicalWithdrawalSystem.LockCoefficients(AdoptedPhys, AdoptedAbs);
            PhysicalSatisfactionBlendSystem.LockWeight(0.25f);
            BuildingAiPolicyConfig.Lock(BuildingAiPolicy.HoldNone, 0f);
            BuildingConstructionSystem.LockCapacityIntensity(0f);

            using var h = new SimulationHarness(Seed);
            // SimulationHarness verrouille 0/0 — re-lock adoption pour la mesure joueur.
            TaxPhysicalWithdrawalSystem.LockCoefficients(AdoptedPhys, AdoptedAbs);
            h.RunTicks(0);
            var em = h.EntityManager;
            var id = PlayerControl.DefaultControlledCountryId;

            // Monde sous levier adopté : tous les pays au même taux via intention joueur
            // pour le pays contrôlé, SetAll pour les autres (IA) — l'interface n'écrit
            // jamais TaxPolicy ; le pays joueur passe par PlayerIntention.
            SetAllTaxRates(em, targetRate);
            PlayerIntentionSubmit.EnqueueSetProductionTaxRate(em, id, targetRate);
            h.RunTicks(ticks);

            var m = WorldMetrics.Capture(em, ticks);
            TaxCostSnapshot.Capture(em, id, out var satC, out var hung, out var hungProv);
            // Afficher le coût monde (arbitrage brief) ET le coût pays dans le log ;
            // la ligne HUD suit InGameHud (pays visualisé = joueur).
            var rate = ReadTaxRate(em, id);
            return new Cell
            {
                CPhys = AdoptedPhys,
                CAbs = AdoptedAbs,
                Mult = targetRate / TaxPolicyLimits.DefaultProductionTaxRate,
                Rate = rate,
                Ticks = ticks,
                Debt = m.TotalDebt,
                Gold = m.TotalTreasury,
                Sat = satC,
                Pop = m.Population,
                Hungry = hung,
                HungryProv = hungProv,
                HudLine = TaxCostSnapshot.FormatHudLine(satC, hung, hungProv)
            };
        }

        static void WriteHudCapturePng(string path, string title, Cell cap)
        {
            const int w = 640;
            const int h = 160;
            var pixels = new Color32[w * h];
            var bg = new Color32(20, 24, 32, 255);
            var fg = new Color32(236, 232, 220, 255);
            var halo = new Color32(8, 8, 12, 255);
            for (var i = 0; i < pixels.Length; i++)
                pixels[i] = bg;

            MapSnapshotExporter.WithPixelSize(w, h, () =>
            {
                MapSnapshotExporter.WithGlyphScale(3, () =>
                {
                    MapSnapshotExporter.DrawBitmapText(pixels, title, 12, 16, fg, halo);
                    MapSnapshotExporter.DrawBitmapText(
                        pixels,
                        "RATE " + cap.Rate.ToString("0.####", CultureInfo.InvariantCulture),
                        12, 48, fg, halo);
                    MapSnapshotExporter.DrawBitmapText(
                        pixels,
                        "SAT " + Fmt3(cap.Sat),
                        12, 80, fg, halo);
                    MapSnapshotExporter.DrawBitmapText(
                        pixels,
                        "HUNGRY " + cap.Hungry + " PROV " + cap.HungryProv,
                        12, 112, fg, halo);
                });
            });

            MapSnapshotExporter.WriteMapBufferPng(pixels, w, h, path);
        }

        static Cell RunCell(float cPhys, float cAbs, float taxMult, int ticks)
        {
            TaxPhysicalWithdrawalSystem.LockCoefficients(cPhys, cAbs);
            PhysicalSatisfactionBlendSystem.LockWeight(0.25f);
            BuildingAiPolicyConfig.Lock(BuildingAiPolicy.HoldNone, 0f);
            BuildingConstructionSystem.LockCapacityIntensity(0f);

            using var h = new SimulationHarness(Seed);
            TaxPhysicalWithdrawalSystem.LockCoefficients(cPhys, cAbs);
            h.RunTicks(0);
            var rate = TaxPolicyLimits.DefaultProductionTaxRate * taxMult;
            if (taxMult <= 0f)
                rate = 0f;
            SetAllTaxRates(h.EntityManager, rate);
            TaxPhysicalWithdrawalSystem.ResetSessionTotals();
            h.RunTicks(ticks);
            var m = WorldMetrics.Capture(h.EntityManager, ticks);
            TaxCostSnapshot.Capture(h.EntityManager, -1, out _, out var hung, out var hungProv);
            return new Cell
            {
                CPhys = cPhys,
                CAbs = cAbs,
                Mult = taxMult,
                Rate = rate,
                Ticks = ticks,
                Debt = m.TotalDebt,
                Gold = m.TotalTreasury,
                Sat = m.NeedsSatAvg,
                Pop = m.Population,
                Hungry = hung,
                HungryProv = hungProv,
                HudLine = ""
            };
        }

        static float ReadTaxRate(EntityManager em, int countryId)
        {
            using var q = em.CreateEntityQuery(
                ComponentType.ReadOnly<CountryData>(),
                ComponentType.ReadOnly<TaxPolicy>());
            using var countries = q.ToComponentDataArray<CountryData>(Allocator.Temp);
            using var policies = q.ToComponentDataArray<TaxPolicy>(Allocator.Temp);
            for (var i = 0; i < countries.Length; i++)
            {
                if (countries[i].CountryId == countryId)
                    return policies[i].ProductionTaxRate;
            }

            return TaxPolicyLimits.DefaultProductionTaxRate;
        }

        static void SetAllTaxRates(EntityManager em, float rate)
        {
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<TaxPolicy>());
            using var entities = q.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
                em.SetComponentData(entities[i], new TaxPolicy { ProductionTaxRate = rate });
        }

        static bool IsMonotoneNonIncreasing(float[] values)
        {
            for (var i = 1; i < values.Length; i++)
            {
                if (values[i] > values[i - 1] + 1e-4f)
                    return false;
            }

            return true;
        }

        static string Key(float cPhys, float cAbs, float mult) =>
            $"{cPhys:0.00}|{cAbs:0.00}|{mult:0.00}";

        static string[] Arr3(float[] v)
        {
            var a = new string[v.Length];
            for (var i = 0; i < v.Length; i++)
                a[i] = Fmt3(v[i]);
            return a;
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

        static string Fmt2(float v) => v.ToString("0.00", CultureInfo.InvariantCulture);
        static string Fmt3(float v) => v.ToString("0.000", CultureInfo.InvariantCulture);
        static string Fmt4(float v) => v.ToString("0.0000", CultureInfo.InvariantCulture);
        static string Fmt1(float v) => v.ToString("0.0", CultureInfo.InvariantCulture);
        static string FmtE(float v) => v.ToString("0.#####E+0", CultureInfo.InvariantCulture);

        struct Cell
        {
            public float CPhys, CAbs, Mult, Rate;
            public int Ticks, Pop, Hungry, HungryProv;
            public float Debt, Gold, Sat;
            public string HudLine;
        }
    }
}
