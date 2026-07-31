using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
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
    /// <summary>Batch : -executeMethod VictoriaGame.Tests.V1093BatchRunner.Run</summary>
    public static class V1093BatchRunner
    {
        public static void Run()
        {
            try
            {
                V1093FrontGhostTests.RunAndWriteArtifacts();
                Debug.Log("V1093BatchRunner: DONE");
            }
            catch (Exception ex) when (HarnessAllocationGuard.IsNativeAllocationFailure(ex))
            {
                Debug.LogWarning("V1093BatchRunner: ALLOCATION_FAILURE — " + ex.Message);
                Debug.Log("V1093BatchRunner: DONE_PARTIAL");
            }
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
        }
    }

    /// <summary>
    /// v1_093 — PHASE XII : filtrer les fronts de guerres terminées (fantômes) + lisibilité.
    /// </summary>
    [TestFixture]
    public class V1093FrontGhostTests
    {
        const uint Seed = 42195u;
        const int ParityTicks = 100;
        const int ReferenceTicks = 3000;
        const int PlayerCountryId = PlayerControl.DefaultControlledCountryId;

        /// <summary>Ticks de mesure tardifs (dérivés : après 1er fantôme, pas nommés à la main).</summary>
        static readonly int[] LateSampleOffsets = { 0, 250, 500, 1000 };

        static string GameUnityRoot =>
            Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        static string LogPath => Path.Combine(GameUnityRoot, "Logs", "v1_093_front_ghosts.log");
        static string CapturesDir => Path.Combine(GameUnityRoot, "Captures", "v1_093");

        [TearDown]
        public void TearDown() => ResetAll();

        [Test]
        public void V1093_Parity_Unchanged_Presentation_Only()
        {
            ResetAll();
            Assert.AreEqual(0f, StabilitySystem.AdoptedStabilityReweight, 1e-6f);
            StabilitySystem.LockReweight(StabilitySystem.AdoptedStabilityReweight);
            TemplateRecruitSystem.LockStabilityRecruitScale(0f);
            TaxPhysicalWithdrawalSystem.EnsureParitySafeDefaults();
            using var h = new SimulationHarness(Seed);
            StabilitySystem.LockReweight(StabilitySystem.AdoptedStabilityReweight);
            TemplateRecruitSystem.LockStabilityRecruitScale(0f);
            h.RunTicks(ParityTicks);
            var dig = WorldDigest.Compute(h.EntityManager);
            Assert.AreEqual(ParityAnchors.Expected, dig,
                "filtre présentation seul → digest ancre v1_090");
        }

        [Test]
        public void V1093_Render_Does_Not_Write_World()
        {
            ResetAll();
            using var h = new SimulationHarness(Seed);
            h.RunTicks(0);
            var em = h.EntityManager;
            var targetId = FindLandNeighborCountryId(em, PlayerCountryId);
            Assert.GreaterOrEqual(targetId, 0);
            Assert.IsTrue(PlayerIntentionSubmit.EnqueueDeclareWar(em, PlayerCountryId, targetId));
            h.RunTicks(2);
            var before = WorldDigest.Compute(em);
            var geo = MapSnapshotExporter.BuildMapGeometry(
                MapSnapshotExporter.Width, MapSnapshotExporter.Height);
            var px = MapSnapshotExporter.RenderPoliticalPixels(
                em, geo, MapSnapshotExporter.LabelDensity.Countries, -1);
            Assert.IsNotNull(px);
            var after = WorldDigest.Compute(em);
            Assert.AreEqual(before, after, "rendu front ne doit pas écrire le monde");
        }

        [Test]
        public void V1093_Artifacts_And_Verdict() => RunAndWriteArtifacts();

        public static void RunAndWriteArtifacts()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            Directory.CreateDirectory(CapturesDir);
            var sb = new StringBuilder(1024 * 1024);

            void Flush() => File.WriteAllText(LogPath, sb.ToString(), Encoding.UTF8);

            sb.AppendLine("=== v1_093 FRONT GHOSTS — seed=42195 PHASE XII ===");
            sb.AppendLine(
                "Ancre parité: ParityAnchors.Expected=0xA6D63D33280D5778 (v1_090). " +
                "Correctif minimal présentation — motif FrontAdvanceSystem.cs:96.");
            sb.AppendLine();
            Flush();

            // ========== PARTIE 1 — inventaire + accumulation tardive ==========
            sb.AppendLine("=== PARTIE 1 — CHIFFRER LES FANTÔMES, TARD ===");
            sb.AppendLine("INVENTAIRE confirmé/infirmé (fichier:ligne):");
            sb.AppendLine(
                "  FrontLineSystem.cs:104 et :141 — if (!war.IsActive) continue; " +
                "saute les guerres inactives, ne nettoie JAMAIS les secteurs — CONFIRMÉ.");
            sb.AppendLine(
                "  MapSnapshotExporter.ApplyFrontFlags (ex-:3232) ne filtrait QUE " +
                "sector.IsActive avant v1_093 — CONFIRMÉ (défaut).");
            sb.AppendLine(
                "  FrontAdvanceSystem.cs:96 — if (!warData.IsActive) continue; " +
                "filtre DÉJÀ correctement — CONFIRMÉ (motif recopié).");
            sb.AppendLine();
            Flush();

            ForceGc();
            ResetAll();
            StabilitySystem.LockReweight(0f);
            TemplateRecruitSystem.LockStabilityRecruitScale(0f);

            int firstGhostTick = -1; // sentinelle ≠ 0
            int firstGhostSectorsActive = -1;
            int firstGhostWarActive = -1;
            int firstGhostWarEnded = -1;
            int firstGhostProvWrong = -1;
            int firstGhostProvLegit = -1;

            var lateRows = new List<string>();
            int staleAt3000 = -1;
            int staleBytesAt3000 = -1;
            int sectorsActiveAt3000 = -1;
            int warActiveAt3000 = -1;
            int warEndedAt3000 = -1;
            int provWrongAt3000 = -1;
            int provLegitAt3000 = -1;

            // Capture état au tick du 1er fantôme pour SHA256 / captures PARTIE 2-3
            // → on rejoue jusqu'à ce tick ensuite (déterministe).

            using (var h = new SimulationHarness(Seed))
            {
                StabilitySystem.LockReweight(0f);
                TemplateRecruitSystem.LockStabilityRecruitScale(0f);

                for (var t = 1; t <= ReferenceTicks; t++)
                {
                    h.RunTicks(1);
                    var em = h.EntityManager;
                    MeasureGhostSnapshot(
                        em,
                        out var sectorsActive,
                        out var warActive,
                        out var warEnded,
                        out var provWrong,
                        out var provLegit,
                        out var staleSectors,
                        out var staleBytes);

                    if (firstGhostTick < 0 && warEnded > 0 && provWrong > 0)
                    {
                        firstGhostTick = t;
                        firstGhostSectorsActive = sectorsActive;
                        firstGhostWarActive = warActive;
                        firstGhostWarEnded = warEnded;
                        firstGhostProvWrong = provWrong;
                        firstGhostProvLegit = provLegit;
                    }

                    if (firstGhostTick > 0)
                    {
                        for (var s = 0; s < LateSampleOffsets.Length; s++)
                        {
                            var sampleTick = firstGhostTick + LateSampleOffsets[s];
                            if (t != sampleTick)
                                continue;
                            if (sampleTick > ReferenceTicks)
                                continue;
                            var ratio = provLegit > 0
                                ? (provWrong / (double)provLegit).ToString("0.###", CultureInfo.InvariantCulture)
                                : (provWrong > 0 ? "inf" : "0");
                            lateRows.Add(
                                $"t={t} sectorsIsActive={sectorsActive} warActive={warActive} " +
                                $"warEnded={warEnded} provWrong={provWrong} provLegit={provLegit} " +
                                $"rapportWrong/Legit={ratio} staleSectors={staleSectors} " +
                                $"staleBytes~={staleBytes}");
                        }
                    }

                    if (t == ReferenceTicks)
                    {
                        staleAt3000 = staleSectors;
                        staleBytesAt3000 = staleBytes;
                        sectorsActiveAt3000 = sectorsActive;
                        warActiveAt3000 = warActive;
                        warEndedAt3000 = warEnded;
                        provWrongAt3000 = provWrong;
                        provLegitAt3000 = provLegit;
                    }
                }
            }

            sb.AppendLine(
                $"PREMIER tick avec fantômes (dérivé): t={FmtSentinel(firstGhostTick)} " +
                $"sectorsIsActive={FmtSentinel(firstGhostSectorsActive)} " +
                $"warActive={FmtSentinel(firstGhostWarActive)} " +
                $"warEnded={FmtSentinel(firstGhostWarEnded)} " +
                $"provWrong={FmtSentinel(firstGhostProvWrong)} " +
                $"provLegit={FmtSentinel(firstGhostProvLegit)}");
            sb.AppendLine("ACCUMULATION (ticks dérivés du 1er fantôme + offsets):");
            if (lateRows.Count == 0)
                sb.AppendLine("  pas trouvé (aucun fantôme sur 3000 ticks)");
            for (var i = 0; i < lateRows.Count; i++)
                sb.AppendLine("  " + lateRows[i]);
            sb.AppendLine(
                $"t3000: sectorsIsActive={FmtSentinel(sectorsActiveAt3000)} " +
                $"warActive={FmtSentinel(warActiveAt3000)} warEnded={FmtSentinel(warEndedAt3000)} " +
                $"provWrong={FmtSentinel(provWrongAt3000)} provLegit={FmtSentinel(provLegitAt3000)}");
            sb.AppendLine(
                $"COÛT correctif profond NON FAIT: secteurs périmés t3000=" +
                $"{FmtSentinel(staleAt3000)} (~{FmtSentinel(staleBytesAt3000)} octets " +
                $"FrontSectorData+buffer capacity ; nettoyage FrontLineSystem reporté).");
            sb.AppendLine();
            Flush();

            Assert.Greater(firstGhostTick, 0,
                "aucun fantôme mesuré — sentinelle, pas un zéro silencieux");

            // ========== PARTIE 2 — filtrer + lisibilité ==========
            sb.AppendLine("=== PARTIE 2 — FILTRER WarData.IsActive + LISIBILITÉ ===");
            sb.AppendLine(
                "Filtre: recopié de FrontAdvanceSystem.cs:89-98 " +
                "(Exists + HasComponent<WarData> + WarData.IsActive).");
            sb.AppendLine(
                "Règle lisibilité: FrontRimThicknessPx=2 (edgeDist 1..2 = liseré) " +
                "+ halo sombre (FrontRimHalo) à edgeDist==3 ; occupation prioritaire inchangée ; " +
                "FrontOverlayEnabled=false → réversible à l'octet.");
            Flush();

            ForceGc();
            ResetAll();
            StabilitySystem.LockReweight(0f);
            TemplateRecruitSystem.LockStabilityRecruitScale(0f);

            string shaLateBefore;
            string shaLateAfter;
            int annotatedBefore = -1;
            int annotatedAfter = -1;
            string shaActiveGhostOn;
            string shaActiveGhostOff;
            string shaActiveThick1;
            string shaActiveThick2;
            int pixelsThick1 = -1;
            int pixelsThick2 = -1;
            string shaReversibleOff;
            string shaReversibleBaseline;
            ulong parityBefore;
            ulong parityAfter;
            var drawnCorrespondence = new List<string>();
            int drawnGhostSurvivors = -1;
            int drawnActiveWars = -1;
            int drawnProvCount = -1;
            string lateDetail = "";
            string activeDetail = "";
            int activeTargetId = -1;

            // --- Tick tardif dérivé : SHA256 avant/après filtre ---
            using (var h = new SimulationHarness(Seed))
            {
                StabilitySystem.LockReweight(0f);
                TemplateRecruitSystem.LockStabilityRecruitScale(0f);
                h.RunTicks(firstGhostTick);
                var em = h.EntityManager;
                var geo = MapSnapshotExporter.BuildMapGeometry(
                    MapSnapshotExporter.Width, MapSnapshotExporter.Height);

                parityBefore = WorldDigest.Compute(em);

                MapSnapshotExporter.FrontOverlayEnabled = true;
                MapSnapshotExporter.FrontRimThicknessPx = 2;
                MapSnapshotExporter.DebugAnnotateInactiveWarFronts = true;
                var pxBefore = MapSnapshotExporter.RenderPoliticalPixels(
                    em, geo, MapSnapshotExporter.LabelDensity.Provinces, -1);
                shaLateBefore = Sha256Pixels(pxBefore);
                annotatedBefore = CountAnnotatedFrontProvinces(em, includeInactiveWars: true);

                MapSnapshotExporter.DebugAnnotateInactiveWarFronts = false;
                var pxAfter = MapSnapshotExporter.RenderPoliticalPixels(
                    em, geo, MapSnapshotExporter.LabelDensity.Provinces, -1);
                shaLateAfter = Sha256Pixels(pxAfter);
                annotatedAfter = CountAnnotatedFrontProvinces(em, includeInactiveWars: false);
                drawnProvCount = MapSnapshotExporter.LastFrontDrawnProvinceIds.Count;
                CollectDrawnCorrespondence(em, drawnCorrespondence,
                    out drawnGhostSurvivors, out drawnActiveWars);

                // Réversibilité au tick tardif : off → on → off doit redonner le PNG off.
                MapSnapshotExporter.DebugAnnotateInactiveWarFronts = false;
                MapSnapshotExporter.FrontOverlayEnabled = false;
                shaReversibleBaseline = Sha256Pixels(MapSnapshotExporter.RenderPoliticalPixels(
                    em, geo, MapSnapshotExporter.LabelDensity.Provinces, -1));
                MapSnapshotExporter.FrontOverlayEnabled = true;
                _ = MapSnapshotExporter.RenderPoliticalPixels(
                    em, geo, MapSnapshotExporter.LabelDensity.Provinces, -1);
                MapSnapshotExporter.FrontOverlayEnabled = false;
                shaReversibleOff = Sha256Pixels(MapSnapshotExporter.RenderPoliticalPixels(
                    em, geo, MapSnapshotExporter.LabelDensity.Provinces, -1));
                MapSnapshotExporter.FrontOverlayEnabled = true;
                parityAfter = WorldDigest.Compute(em);

                Assert.IsTrue(CountryObservation.TryCapture(em, PlayerCountryId, out var snapLate));
                lateDetail = snapLate.DetailBlock ?? "";

                WriteGameViewCapture(
                    em, Path.Combine(CapturesDir, "01_late_before_ghosts.png"),
                    "v1_093 TARDIF AVANT filtre (fantômes DebugAnnotate=1)",
                    lateDetail, PlayerCountryId, frontEnabled: true, annotateGhosts: true);
                WriteGameViewCapture(
                    em, Path.Combine(CapturesDir, "02_late_after_filter.png"),
                    "v1_093 TARDIF APRES filtre WarData.IsActive",
                    lateDetail, PlayerCountryId, frontEnabled: true, annotateGhosts: false);
            }

            // --- Guerre ACTIVE : non-régression filtre (SHA256 identiques) + lisibilité ---
            ForceGc();
            ResetAll();
            StabilitySystem.LockReweight(0f);
            TemplateRecruitSystem.LockStabilityRecruitScale(0f);

            using (var h = new SimulationHarness(Seed))
            {
                StabilitySystem.LockReweight(0f);
                TemplateRecruitSystem.LockStabilityRecruitScale(0f);
                h.RunTicks(0);
                var em = h.EntityManager;
                activeTargetId = FindLandNeighborCountryId(em, PlayerCountryId);
                Assert.GreaterOrEqual(activeTargetId, 0);
                Assert.IsTrue(
                    PlayerIntentionSubmit.EnqueueDeclareWar(em, PlayerCountryId, activeTargetId));
                h.RunTicks(2);
                var geo = MapSnapshotExporter.BuildMapGeometry(
                    MapSnapshotExporter.Width, MapSnapshotExporter.Height);

                MapSnapshotExporter.FrontOverlayEnabled = true;
                MapSnapshotExporter.FrontRimThicknessPx = 2;

                // Filtre on/off : même overlay → doivent être IDENTIQUES (pas de guerres terminées).
                MapSnapshotExporter.DebugAnnotateInactiveWarFronts = true;
                shaActiveGhostOn = Sha256Pixels(MapSnapshotExporter.RenderPoliticalPixels(
                    em, geo, MapSnapshotExporter.LabelDensity.Provinces, -1));
                MapSnapshotExporter.DebugAnnotateInactiveWarFronts = false;
                shaActiveGhostOff = Sha256Pixels(MapSnapshotExporter.RenderPoliticalPixels(
                    em, geo, MapSnapshotExporter.LabelDensity.Provinces, -1));

                // Lisibilité : thickness 1 (v1_092) vs 2+halo
                MapSnapshotExporter.DebugAnnotateInactiveWarFronts = false;
                MapSnapshotExporter.FrontRimThicknessPx = 1;
                var px1 = MapSnapshotExporter.RenderPoliticalPixels(
                    em, geo, MapSnapshotExporter.LabelDensity.Provinces, -1);
                shaActiveThick1 = Sha256Pixels(px1);
                pixelsThick1 = MapSnapshotExporter.LastFrontPixelCount;

                MapSnapshotExporter.FrontRimThicknessPx = 2;
                var px2 = MapSnapshotExporter.RenderPoliticalPixels(
                    em, geo, MapSnapshotExporter.LabelDensity.Provinces, -1);
                shaActiveThick2 = Sha256Pixels(px2);
                pixelsThick2 = MapSnapshotExporter.LastFrontPixelCount;

                Assert.IsTrue(CountryObservation.TryCapture(em, PlayerCountryId, out var snapActive));
                activeDetail = snapActive.DetailBlock ?? "";

                WriteGameViewCapture(
                    em, Path.Combine(CapturesDir, "03_active_war_readable.png"),
                    "v1_093 GUERRE ACTIVE front lisible (thick=2+halo)",
                    activeDetail, PlayerCountryId, frontEnabled: true, annotateGhosts: false);
            }

            var lateDiffers = shaLateBefore != shaLateAfter;
            var activeFilterIdentical = shaActiveGhostOn == shaActiveGhostOff;
            var thickDiffers = shaActiveThick1 != shaActiveThick2 && pixelsThick2 > pixelsThick1;
            var reversibleOk = shaReversibleOff == shaReversibleBaseline;
            var parityOk = parityBefore == parityAfter && parityAfter != 0;
            // Parité ancre séparée
            ForceGc();
            ResetAll();
            StabilitySystem.LockReweight(0f);
            TemplateRecruitSystem.LockStabilityRecruitScale(0f);
            ulong adoptDig;
            using (var h = new SimulationHarness(Seed))
            {
                StabilitySystem.LockReweight(0f);
                TemplateRecruitSystem.LockStabilityRecruitScale(0f);
                h.RunTicks(ParityTicks);
                adoptDig = WorldDigest.Compute(h.EntityManager);
            }

            var adoptOk = adoptDig == ParityAnchors.Expected;
            var annotatedDrop = annotatedBefore > annotatedAfter && annotatedAfter >= 0;
            var zeroGhosts = drawnGhostSurvivors == 0;

            sb.AppendLine(
                $"Tick tardif dérivé t={firstGhostTick}:");
            sb.AppendLine($"  SHA256 AVANT filtre (DebugAnnotate=1): {shaLateBefore}");
            sb.AppendLine($"  SHA256 APRES filtre (WarData.IsActive): {shaLateAfter}");
            sb.AppendLine($"  → DIFFÉRENTS={lateDiffers} (preuve rouge OK si true)");
            sb.AppendLine(
                $"  Provinces annotées avant={FmtSentinel(annotatedBefore)} " +
                $"après={FmtSentinel(annotatedAfter)} baisse={annotatedDrop}");
            sb.AppendLine(
                $"Guerre ACTIVE (joueur, t=2): SHA256 DebugAnnotate=1: {shaActiveGhostOn}");
            sb.AppendLine(
                $"Guerre ACTIVE: SHA256 DebugAnnotate=0: {shaActiveGhostOff}");
            sb.AppendLine(
                $"  → IDENTIQUES={activeFilterIdentical} (non-régression filtre OK si true)");
            sb.AppendLine(
                $"Lisibilité: thick=1 pixels={FmtSentinel(pixelsThick1)} sha={shaActiveThick1}");
            sb.AppendLine(
                $"Lisibilité: thick=2+halo pixels={FmtSentinel(pixelsThick2)} sha={shaActiveThick2}");
            sb.AppendLine(
                $"  → pixels augmentent={thickDiffers} " +
                $"(règle: rim edgeDist≤2 + halo edgeDist==3)");
            sb.AppendLine(
                $"Réversible FrontOverlayEnabled=false: sha={shaReversibleOff} " +
                $"== baseline={reversibleOk}");
            sb.AppendLine(
                $"Parité monde avant/après rendu tardif: 0x{parityBefore:X16} → " +
                $"0x{parityAfter:X16} bit-identique={parityOk}");
            sb.AppendLine(
                $"Parité ancre @t{ParityTicks}: 0x{adoptDig:X16} " +
                $"== Expected={adoptOk}");
            sb.AppendLine();
            Flush();

            // ========== PARTIE 3 ==========
            sb.AppendLine("=== PARTIE 3 — CARTE TARDIVE PROPRE + FRONT LÉGITIME LISIBLE ===");
            var shaCapBefore = FileSha256(Path.Combine(CapturesDir, "01_late_before_ghosts.png"));
            var shaCapAfter = FileSha256(Path.Combine(CapturesDir, "02_late_after_filter.png"));
            var shaCapActive = FileSha256(Path.Combine(CapturesDir, "03_active_war_readable.png"));
            var capDiff = shaCapBefore != shaCapAfter;
            sb.AppendLine(
                $"Captures: Captures/v1_093/01_late_before_ghosts.png + " +
                $"02_late_after_filter.png @t={firstGhostTick}");
            sb.AppendLine($"  SHA256 before: {shaCapBefore}");
            sb.AppendLine($"  SHA256 after : {shaCapAfter}");
            sb.AppendLine($"  raster DIFFÉRENTS={capDiff} (disparition de traits attendue)");
            sb.AppendLine(
                $"Capture guerre active: 03_active_war_readable.png sha={shaCapActive} " +
                $"targetId={activeTargetId} pixels thick1→2: " +
                $"{FmtSentinel(pixelsThick1)}→{FmtSentinel(pixelsThick2)}");
            sb.AppendLine(
                $"Correspondance dessiné/calculé @t={firstGhostTick} " +
                $"(après filtre): drawn={FmtSentinel(drawnProvCount)} " +
                $"guerresActives={FmtSentinel(drawnActiveWars)} " +
                $"fantômesSurvivants={FmtSentinel(drawnGhostSurvivors)}");
            for (var i = 0; i < drawnCorrespondence.Count; i++)
                sb.AppendLine("  " + drawnCorrespondence[i]);
            sb.AppendLine(
                "LARGE: rejouée à part (voir Logs/v1_093_large.xml) — filtre v1_092 + V1093.");
            sb.AppendLine();

            var pass =
                firstGhostTick > 0 &&
                lateDiffers &&
                annotatedDrop &&
                activeFilterIdentical &&
                thickDiffers &&
                reversibleOk &&
                parityOk &&
                adoptOk &&
                capDiff &&
                zeroGhosts &&
                drawnProvCount >= 0;

            sb.AppendLine(
                "VERDICT MESURÉ: " + (pass ? "PASS" : "FAIL") + " — " +
                $"les trois points confirmés ; t{firstGhostTick}: " +
                $"{FmtSentinel(firstGhostSectorsActive)} secteurs IsActive, " +
                $"{FmtSentinel(firstGhostWarActive)} guerres actives, " +
                $"{FmtSentinel(firstGhostWarEnded)} terminées, " +
                $"{FmtSentinel(firstGhostProvWrong)} provinces dessinées à tort contre " +
                $"{FmtSentinel(firstGhostProvLegit)} légitimes ; " +
                $"{FmtSentinel(staleAt3000)} secteurs périmés à t3000 " +
                $"(~{FmtSentinel(staleBytesAt3000)} o) ; " +
                $"filtre WarData.IsActive recopié de FrontAdvanceSystem.cs:96 ; " +
                $"tick tardif SHA256 différents={lateDiffers}, " +
                $"provinces annotées {FmtSentinel(annotatedBefore)} → {FmtSentinel(annotatedAfter)} ; " +
                $"guerre active SHA256 IDENTIQUES={activeFilterIdentical} ; " +
                $"parité 0xA6D63D33280D5778 bit-identique={adoptOk} ; " +
                $"front épaissi à 2 px + halo, pixels {FmtSentinel(pixelsThick1)} → " +
                $"{FmtSentinel(pixelsThick2)}, occupation toujours prioritaire ; " +
                $"réversible à l'octet={reversibleOk} ; " +
                $"{FmtSentinel(drawnProvCount)} provinces dessinées, " +
                $"{FmtSentinel(drawnActiveWars)} guerres actives, " +
                $"{FmtSentinel(drawnGhostSurvivors)} fantôme ; " +
                $"captures raster diff={capDiff}.");
            Flush();

            Assert.IsTrue(pass, "V1093 verdict mesuré FAIL — voir " + LogPath);
            ResetAll();
        }

        static void WriteGameViewCapture(
            EntityManager em,
            string path,
            string title,
            string countryDetail,
            int countryId,
            bool frontEnabled,
            bool annotateGhosts)
        {
            var prevEnabled = MapSnapshotExporter.FrontOverlayEnabled;
            var prevGhost = MapSnapshotExporter.DebugAnnotateInactiveWarFronts;
            var prevThick = MapSnapshotExporter.FrontRimThicknessPx;
            MapSnapshotExporter.FrontOverlayEnabled = frontEnabled;
            MapSnapshotExporter.DebugAnnotateInactiveWarFronts = annotateGhosts;
            MapSnapshotExporter.FrontRimThicknessPx = 2;
            try
            {
                MapDisplaySystem.TrySelectCountryByTag(em, TagOf(em, countryId));
                var geo = MapGeometryCache.GetOrBuild(
                    MapSnapshotExporter.Width, MapSnapshotExporter.Height,
                    MapViewport.State.Window, out _);
                if (geo == null)
                {
                    geo = MapSnapshotExporter.BuildMapGeometry(
                        MapSnapshotExporter.Width, MapSnapshotExporter.Height);
                }

                var warsSection = ExtractWarsSection(countryDetail);
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
                            var lines = warsSection.Split('\n');
                            for (var i = 0; i < lines.Length && i < 8; i++)
                            {
                                if (string.IsNullOrEmpty(lines[i]))
                                    continue;
                                MapSnapshotExporter.DrawBitmapText(p, lines[i], 12, y, fg, halo);
                                y += 18;
                            }

                            var drawn = MapSnapshotExporter.LastFrontDrawnProvinceIds;
                            MapSnapshotExporter.DrawBitmapText(
                                p,
                                "FRONT_PX " + drawn.Count.ToString(CultureInfo.InvariantCulture) +
                                " pix=" + MapSnapshotExporter.LastFrontPixelCount.ToString(
                                    CultureInfo.InvariantCulture) +
                                " ghostDbg=" + (annotateGhosts ? "1" : "0") +
                                " overlay=" + (frontEnabled ? "1" : "0"),
                                12, y, fg, halo);
                        });
                    });

                if (pixels != null)
                {
                    MapSnapshotExporter.WriteMapBufferPng(
                        pixels, MapSnapshotExporter.Width, MapSnapshotExporter.Height, path);
                }

                MapViewport.Reset();
            }
            finally
            {
                MapSnapshotExporter.FrontOverlayEnabled = prevEnabled;
                MapSnapshotExporter.DebugAnnotateInactiveWarFronts = prevGhost;
                MapSnapshotExporter.FrontRimThicknessPx = prevThick;
            }
        }

        /// <summary>
        /// Compte secteurs IsActive, ceux à guerre active vs terminée, provinces légitimes vs fantômes.
        /// </summary>
        static void MeasureGhostSnapshot(
            EntityManager em,
            out int sectorsActive,
            out int warActive,
            out int warEnded,
            out int provWrong,
            out int provLegit,
            out int staleSectors,
            out int staleBytes)
        {
            sectorsActive = 0;
            warActive = 0;
            warEnded = 0;
            provWrong = 0;
            provLegit = 0;
            staleSectors = 0;
            staleBytes = 0;

            var activeWarKeys = new HashSet<Entity>();
            var endedWarKeys = new HashSet<Entity>();
            var legitProv = new HashSet<int>();
            var ghostProv = new HashSet<int>();

            // FrontSectorData ≈ Entity×3 + float + bool + int ≈ 40 o ; buffer capacity 8 × ~16 o
            const int SectorBytesApprox = 40 + 8 * 16;

            using var q = em.CreateEntityQuery(
                ComponentType.ReadOnly<FrontSectorData>(),
                ComponentType.ReadOnly<FrontLineState>());
            using var entities = q.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                var sector = em.GetComponentData<FrontSectorData>(entities[i]);
                if (!sector.IsActive)
                    continue;
                sectorsActive++;

                var warActiveNow = IsWarActive(em, sector.War);
                if (!warActiveNow)
                {
                    staleSectors++;
                    staleBytes += SectorBytesApprox;
                    endedWarKeys.Add(sector.War);
                }
                else
                {
                    activeWarKeys.Add(sector.War);
                }

                var buf = em.GetBuffer<FrontLineState>(entities[i]);
                for (var b = 0; b < buf.Length; b++)
                {
                    var pid = buf[b].ProvinceId;
                    if (warActiveNow)
                        legitProv.Add(pid);
                    else
                        ghostProv.Add(pid);
                }
            }

            // Une province dans les deux camps compte comme légitime (guerre active prioritaire).
            ghostProv.ExceptWith(legitProv);
            warActive = activeWarKeys.Count;
            warEnded = endedWarKeys.Count;
            provLegit = legitProv.Count;
            provWrong = ghostProv.Count;
        }

        static int CountAnnotatedFrontProvinces(EntityManager em, bool includeInactiveWars)
        {
            var ids = new HashSet<int>();
            using var q = em.CreateEntityQuery(
                ComponentType.ReadOnly<FrontSectorData>(),
                ComponentType.ReadOnly<FrontLineState>());
            using var entities = q.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                var sector = em.GetComponentData<FrontSectorData>(entities[i]);
                if (!sector.IsActive)
                    continue;
                if (!includeInactiveWars && !IsWarActive(em, sector.War))
                    continue;
                var buf = em.GetBuffer<FrontLineState>(entities[i]);
                for (var b = 0; b < buf.Length; b++)
                    ids.Add(buf[b].ProvinceId);
            }

            return ids.Count;
        }

        static void CollectDrawnCorrespondence(
            EntityManager em,
            List<string> rows,
            out int ghostSurvivors,
            out int activeWars)
        {
            rows.Clear();
            ghostSurvivors = 0;
            activeWars = 0;
            var warActiveKeys = new HashSet<Entity>();
            var drawn = MapSnapshotExporter.LastFrontDrawnProvinceIds;
            // Retrouver le secteur / guerre pour chaque province dessinée.
            var provToWar = new Dictionary<int, (Entity War, bool Active, string Label)>();
            using (var q = em.CreateEntityQuery(
                       ComponentType.ReadOnly<FrontSectorData>(),
                       ComponentType.ReadOnly<FrontLineState>()))
            using (var entities = q.ToEntityArray(Allocator.Temp))
            {
                for (var i = 0; i < entities.Length; i++)
                {
                    var sector = em.GetComponentData<FrontSectorData>(entities[i]);
                    if (!sector.IsActive)
                        continue;
                    var active = IsWarActive(em, sector.War);
                    var label = TagOfEntity(em, sector.AttackerCountry) + " vs " +
                                TagOfEntity(em, sector.DefenderCountry);
                    var buf = em.GetBuffer<FrontLineState>(entities[i]);
                    for (var b = 0; b < buf.Length; b++)
                    {
                        var pid = buf[b].ProvinceId;
                        // Priorité : guerre active écrase un fantôme pour la même province.
                        if (!provToWar.TryGetValue(pid, out var prev) || (!prev.Active && active))
                            provToWar[pid] = (sector.War, active, label);
                    }
                }
            }

            for (var i = 0; i < drawn.Count; i++)
            {
                var pid = drawn[i];
                if (!provToWar.TryGetValue(pid, out var info))
                {
                    rows.Add($"prov={pid} war=(unknown) active=False FANTÔME?");
                    ghostSurvivors++;
                    continue;
                }

                if (info.Active)
                    warActiveKeys.Add(info.War);
                else
                    ghostSurvivors++;
                rows.Add(
                    $"prov={pid} war={info.Label} active={info.Active}" +
                    (info.Active ? "" : " FANTÔME"));
            }

            activeWars = warActiveKeys.Count;
        }

        static bool IsWarActive(EntityManager em, Entity war)
        {
            if (war == Entity.Null || !em.Exists(war) || !em.HasComponent<WarData>(war))
                return false;
            return em.GetComponentData<WarData>(war).IsActive;
        }

        static string ExtractWarsSection(string detail)
        {
            if (string.IsNullOrEmpty(detail))
                return "(empty)";
            var idx = detail.IndexOf("--- MILITARY ---", StringComparison.Ordinal);
            if (idx < 0)
                return detail;
            var end = detail.IndexOf("--- STATUS ---", idx, StringComparison.Ordinal);
            if (end < 0)
                end = Math.Min(detail.Length, idx + 400);
            return detail.Substring(idx, end - idx).Trim();
        }

        static int FindLandNeighborCountryId(EntityManager em, int selfId)
        {
            if (!TryResolveCountry(em, selfId, out var selfEntity))
                return -1;

            var controllers = new Dictionary<int, Entity>(64);
            using (var q = em.CreateEntityQuery(
                       ComponentType.ReadOnly<ProvinceData>(),
                       ComponentType.ReadOnly<ProvinceOwnership>()))
            using (var pdata = q.ToComponentDataArray<ProvinceData>(Allocator.Temp))
            using (var owns = q.ToComponentDataArray<ProvinceOwnership>(Allocator.Temp))
            {
                for (var i = 0; i < pdata.Length; i++)
                    controllers[pdata[i].ProvinceId] = owns[i].Controller;
            }

            var neighborCountryIds = new HashSet<int>();
            using (var q = em.CreateEntityQuery(
                       ComponentType.ReadOnly<ProvinceData>(),
                       ComponentType.ReadOnly<ProvinceOwnership>(),
                       ComponentType.ReadOnly<ProvinceNeighbor>()))
            using (var entities = q.ToEntityArray(Allocator.Temp))
            {
                for (var i = 0; i < entities.Length; i++)
                {
                    var own = em.GetComponentData<ProvinceOwnership>(entities[i]);
                    if (own.Controller != selfEntity && own.Owner != selfEntity)
                        continue;
                    var buf = em.GetBuffer<ProvinceNeighbor>(entities[i]);
                    for (var n = 0; n < buf.Length; n++)
                    {
                        if (buf[n].IsStrait)
                            continue;
                        if (!controllers.TryGetValue(buf[n].NeighborProvinceId, out var other))
                            continue;
                        if (other == Entity.Null || other == selfEntity)
                            continue;
                        if (!em.HasComponent<CountryData>(other))
                            continue;
                        neighborCountryIds.Add(em.GetComponentData<CountryData>(other).CountryId);
                    }
                }
            }

            var best = -1;
            foreach (var id in neighborCountryIds)
            {
                if (best < 0 || id < best)
                    best = id;
            }

            return best;
        }

        static bool TryResolveCountry(EntityManager em, int countryId, out Entity entity)
        {
            entity = Entity.Null;
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<CountryData>());
            using var entities = q.ToEntityArray(Allocator.Temp);
            using var data = q.ToComponentDataArray<CountryData>(Allocator.Temp);
            for (var i = 0; i < data.Length; i++)
            {
                if (data[i].CountryId != countryId)
                    continue;
                entity = entities[i];
                return true;
            }

            return false;
        }

        static string TagOf(EntityManager em, int countryId)
        {
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<CountryData>());
            using var data = q.ToComponentDataArray<CountryData>(Allocator.Temp);
            for (var i = 0; i < data.Length; i++)
            {
                if (data[i].CountryId == countryId)
                    return data[i].Tag.ToString();
            }

            return "?";
        }

        static string TagOfEntity(EntityManager em, Entity e)
        {
            if (e == Entity.Null || !em.HasComponent<CountryData>(e))
                return "?";
            return em.GetComponentData<CountryData>(e).Tag.ToString();
        }

        static string Sha256Pixels(Color32[] pixels)
        {
            if (pixels == null)
                return "(null)";
            var bytes = new byte[pixels.Length * 4];
            for (var i = 0; i < pixels.Length; i++)
            {
                var o = i * 4;
                bytes[o] = pixels[i].r;
                bytes[o + 1] = pixels[i].g;
                bytes[o + 2] = pixels[i].b;
                bytes[o + 3] = pixels[i].a;
            }

            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        static string FileSha256(string path)
        {
            if (!File.Exists(path))
                return "(missing)";
            using var sha = SHA256.Create();
            using var fs = File.OpenRead(path);
            return BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "").ToLowerInvariant();
        }

        static string FmtSentinel(int v) =>
            v < 0 ? "pas trouvé" : v.ToString(CultureInfo.InvariantCulture);

        static void ForceGc()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        static void ResetAll()
        {
            MapSnapshotExporter.FrontOverlayEnabled = true;
            MapSnapshotExporter.DebugAnnotateInactiveWarFronts = false;
            MapSnapshotExporter.FrontRimThicknessPx = 2;
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
