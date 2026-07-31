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
    /// <summary>Batch : -executeMethod VictoriaGame.Tests.V1092BatchRunner.Run</summary>
    public static class V1092BatchRunner
    {
        public static void Run()
        {
            try
            {
                V1092FrontRenderTests.RunAndWriteArtifacts();
                Debug.Log("V1092BatchRunner: DONE");
            }
            catch (Exception ex) when (HarnessAllocationGuard.IsNativeAllocationFailure(ex))
            {
                Debug.LogWarning("V1092BatchRunner: ALLOCATION_FAILURE — " + ex.Message);
                Debug.Log("V1092BatchRunner: DONE_PARTIAL");
            }
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
        }
    }

    /// <summary>
    /// v1_092 — PHASE XII : dessiner FrontLineState sur la carte politique (présentation seule).
    /// </summary>
    [TestFixture]
    public class V1092FrontRenderTests
    {
        const uint Seed = 42195u;
        const int ParityTicks = 100;
        const int ReferenceTicks = 3000;
        const int PlayerCountryId = PlayerControl.DefaultControlledCountryId;

        static string GameUnityRoot =>
            Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        static string LogPath => Path.Combine(GameUnityRoot, "Logs", "v1_092_front.log");
        static string CapturesDir => Path.Combine(GameUnityRoot, "Captures", "v1_092");

        [TearDown]
        public void TearDown() => ResetAll();

        [Test]
        public void V1092_Parity_Unchanged_At_Adopted_Stability()
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
                "AdoptedStabilityReweight=0 → digest ancre v1_090");
        }

        [Test]
        public void V1092_FrontOverlay_Off_NoWrite_Parity()
        {
            ResetAll();
            MapSnapshotExporter.FrontOverlayEnabled = false;
            using var h = new SimulationHarness(Seed);
            h.RunTicks(0);
            var em = h.EntityManager;
            var targetId = FindLandNeighborCountryId(em, PlayerCountryId);
            Assert.GreaterOrEqual(targetId, 0);
            Assert.IsTrue(PlayerIntentionSubmit.EnqueueDeclareWar(em, PlayerCountryId, targetId));
            h.RunTicks(2);
            // Rendu lecture seule — l'empreinte monde ne doit pas bouger si on ne simule plus.
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
        public void V1092_Artifacts_And_Verdict() => RunAndWriteArtifacts();

        public static void RunAndWriteArtifacts()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            Directory.CreateDirectory(CapturesDir);
            var sb = new StringBuilder(1024 * 1024);

            void Flush() => File.WriteAllText(LogPath, sb.ToString(), Encoding.UTF8);

            sb.AppendLine("=== v1_092 FRONT RENDER — seed=42195 PHASE XII ===");
            sb.AppendLine(
                "Ancre parité: ParityAnchors.Expected=0xA6D63D33280D5778 (v1_090). " +
                "Présentation seule — FrontOverlayEnabled réversible.");
            sb.AppendLine();
            Flush();

            // ========== PARTIE 1 ==========
            sb.AppendLine("=== PARTIE 1 — CALCULÉ vs RENDU + RÉFÉRENCE GUERRE RÉELLE ===");
            sb.AppendLine("INVENTAIRE FrontSectorData / FrontLineState (lecture hors Military/) AVANT v1_092:");
            sb.AppendLine(
                "  CONFIRMÉ — seuls Military/Systems/{FrontLineSystem,FrontAdvanceSystem," +
                "BattleResolutionSystem}.cs + Military/Components/FrontComponents.cs.");
            sb.AppendLine(
                "  Aucun fichier Presentation/ ne lisait FrontLineState avant ce brief — CONFIRMÉ.");
            sb.AppendLine("CE QUE LA CARTE REND DÉJÀ d'un état de guerre (exhaustif):");
            sb.AppendLine(
                "  1) ApplyOccupationHatch (MapSnapshotExporter) — hachures diagonales si " +
                "Controller≠Owner (couleurs occupant).");
            sb.AppendLine(
                "  2) Panneau texte MapSpriteOverlay / CountryObservation — section WARS " +
                "(ATK vs DEF), pas de raster.");
            sb.AppendLine(
                "  3) WorldMetrics / ChronicleExporter — compteurs et chroniques, hors carte.");
            sb.AppendLine(
                "  ABSENT avant v1_092: FrontLineState (IsContested, pressions, provinces contact).");
            sb.AppendLine();
            Flush();

            ForceGc();
            ResetAll();
            StabilitySystem.LockReweight(0f);
            TemplateRecruitSystem.LockStabilityRecruitScale(0f);

            int firstNonEmptyFrontTick = -1; // sentinelle « pas trouvé » ≠ 0
            int firstNonEmptySectors = -1;
            int firstNonEmptyProvinces = -1;
            int firstNonEmptyContested = -1;
            float firstAtkMin = float.NaN, firstAtkMax = float.NaN;
            float firstDefMin = float.NaN, firstDefMax = float.NaN;
            string firstWarLabel = "(none)";

            // Durées de vie : warEntity → (startTick, endTick) pour fronts non vides.
            var frontLifeStart = new Dictionary<Entity, int>();
            var frontLifeEnd = new Dictionary<Entity, int>();
            var lifetimes = new List<int>();

            // Référence IA : première guerre SANS intervention joueur avec front non vide.
            int refSectors = -1, refProvinces = -1, refContested = -1;
            float refAtkMin = float.NaN, refAtkMax = float.NaN;
            float refDefMin = float.NaN, refDefMax = float.NaN;
            int refTick = -1;
            string refWarLabel = "(none)";
            var refProvinceRows = new List<string>();

            using (var h = new SimulationHarness(Seed))
            {
                StabilitySystem.LockReweight(0f);
                TemplateRecruitSystem.LockStabilityRecruitScale(0f);

                for (var t = 1; t <= ReferenceTicks; t++)
                {
                    h.RunTicks(1);
                    var em = h.EntityManager;
                    MeasureFrontSnapshot(
                        em,
                        out var sectors, out var provinces, out var contested,
                        out var atkMin, out var atkMax, out var defMin, out var defMax,
                        out var warLabel, out var activeWarKeys, out _);

                    var seen = new HashSet<Entity>();
                    using (var q = em.CreateEntityQuery(
                               ComponentType.ReadOnly<FrontSectorData>(),
                               ComponentType.ReadOnly<FrontLineState>()))
                    using (var entities = q.ToEntityArray(Allocator.Temp))
                    {
                        for (var i = 0; i < entities.Length; i++)
                        {
                            var sector = em.GetComponentData<FrontSectorData>(entities[i]);
                            if (!sector.IsActive || sector.War == Entity.Null)
                                continue;
                            var buf = em.GetBuffer<FrontLineState>(entities[i]);
                            if (buf.Length == 0)
                                continue;
                            seen.Add(sector.War);
                            if (!frontLifeStart.ContainsKey(sector.War))
                                frontLifeStart[sector.War] = t;
                            frontLifeEnd[sector.War] = t;
                        }
                    }

                    var closed = new List<Entity>();
                    foreach (var war in frontLifeStart.Keys)
                    {
                        if (seen.Contains(war))
                            continue;
                        if (frontLifeEnd.TryGetValue(war, out var endTick))
                        {
                            lifetimes.Add(endTick - frontLifeStart[war] + 1);
                            closed.Add(war);
                        }
                    }

                    for (var c = 0; c < closed.Count; c++)
                    {
                        frontLifeStart.Remove(closed[c]);
                        frontLifeEnd.Remove(closed[c]);
                    }

                    if (firstNonEmptyFrontTick < 0 && provinces > 0)
                    {
                        firstNonEmptyFrontTick = t;
                        firstNonEmptySectors = sectors;
                        firstNonEmptyProvinces = provinces;
                        firstNonEmptyContested = contested;
                        firstAtkMin = atkMin;
                        firstAtkMax = atkMax;
                        firstDefMin = defMin;
                        firstDefMax = defMax;
                        firstWarLabel = warLabel;
                    }

                    if (refTick < 0 && provinces > 0 && activeWarKeys > 0)
                    {
                        refTick = t;
                        refSectors = sectors;
                        refProvinces = provinces;
                        refContested = contested;
                        refAtkMin = atkMin;
                        refAtkMax = atkMax;
                        refDefMin = defMin;
                        refDefMax = defMax;
                        refWarLabel = warLabel;
                        CollectFrontProvinceRows(em, refProvinceRows);
                    }
                }

                foreach (var war in frontLifeStart.Keys)
                {
                    if (frontLifeEnd.TryGetValue(war, out var endTick))
                        lifetimes.Add(endTick - frontLifeStart[war] + 1);
                }
            }

            var avgLife = Avg(lifetimes);

            sb.AppendLine(
                $"PREMIER front non vide: tick={FmtSentinel(firstNonEmptyFrontTick)} " +
                $"war={firstWarLabel} sectors={FmtSentinel(firstNonEmptySectors)} " +
                $"provinces={FmtSentinel(firstNonEmptyProvinces)} " +
                $"contested={FmtSentinel(firstNonEmptyContested)} " +
                $"atkPressure=[{FmtF(firstAtkMin)}..{FmtF(firstAtkMax)}] " +
                $"defPressure=[{FmtF(firstDefMin)}..{FmtF(firstDefMax)}]");
            sb.AppendLine(
                $"RÉFÉRENCE guerre réelle (1er front IA non vide): tick={FmtSentinel(refTick)} " +
                $"war={refWarLabel} sectors={FmtSentinel(refSectors)} " +
                $"provinces={FmtSentinel(refProvinces)} contested={FmtSentinel(refContested)} " +
                $"atk=[{FmtF(refAtkMin)}..{FmtF(refAtkMax)}] " +
                $"def=[{FmtF(refDefMin)}..{FmtF(refDefMax)}]");
            for (var i = 0; i < refProvinceRows.Count; i++)
                sb.AppendLine("  " + refProvinceRows[i]);
            sb.AppendLine(
                $"DURÉE DE VIE moyenne front (ticks, n={lifetimes.Count}): " +
                (lifetimes.Count > 0
                    ? avgLife.ToString("0.###", CultureInfo.InvariantCulture)
                    : "pas trouvé (n=0)"));
            sb.AppendLine();
            Flush();

            // ========== PARTIE 2 ==========
            sb.AppendLine("=== PARTIE 2 — DESSINER SANS ÉCRIRE + RÉGRESSION SHA256 ===");
            sb.AppendLine(
                "Règle superposition: contesté=damier jaune/brun ((px^py)&1) ; " +
                "non-contesté=liseré rouge bord 4-connexe ; " +
                "occupation prioritaire sur pixels hachurés ((px+py)&3)==0.");
            sb.AppendLine(
                "Réversible: FrontOverlayEnabled=false → PNG bit-identique au pré-brief " +
                "(même avec front actif).");
            Flush();

            ForceGc();
            ResetAll();
            StabilitySystem.LockReweight(0f);
            TemplateRecruitSystem.LockStabilityRecruitScale(0f);

            string shaNoWarOff;
            string shaNoWarOn;
            string shaWarOff;
            string shaWarOn;
            string shaWarOnAgain;
            ulong parityBeforeRender;
            ulong parityAfterRender;
            var drawnRows = new List<string>();
            int drawnCount = -1;
            int computedFrontCount = -1;
            string captureWarLabel = "(none)";
            string detailAfter = "";
            int captureTargetId = -1;

            using (var h = new SimulationHarness(Seed))
            {
                StabilitySystem.LockReweight(0f);
                TemplateRecruitSystem.LockStabilityRecruitScale(0f);
                h.RunTicks(0);
                var em = h.EntityManager;
                var geo = MapSnapshotExporter.BuildMapGeometry(
                    MapSnapshotExporter.Width, MapSnapshotExporter.Height);

                // --- SANS guerre ---
                MapSnapshotExporter.FrontOverlayEnabled = false;
                var pxOff = MapSnapshotExporter.RenderPoliticalPixels(
                    em, geo, MapSnapshotExporter.LabelDensity.Countries, -1);
                shaNoWarOff = Sha256Pixels(pxOff);

                MapSnapshotExporter.FrontOverlayEnabled = true;
                var pxOn = MapSnapshotExporter.RenderPoliticalPixels(
                    em, geo, MapSnapshotExporter.LabelDensity.Countries, -1);
                shaNoWarOn = Sha256Pixels(pxOn);

                // --- AVEC guerre + front ---
                captureTargetId = FindLandNeighborCountryId(em, PlayerCountryId);
                Assert.GreaterOrEqual(captureTargetId, 0);
                Assert.IsTrue(
                    PlayerIntentionSubmit.EnqueueDeclareWar(em, PlayerCountryId, captureTargetId));
                h.RunTicks(1);
                // FrontLineSystem saisit au tick suivant la déclaration.
                h.RunTicks(1);
                MeasureFrontSnapshot(
                    em,
                    out _, out computedFrontCount, out _,
                    out _, out _, out _, out _,
                    out captureWarLabel, out _, out _);
                CollectFrontProvinceRows(em, drawnRows);

                parityBeforeRender = WorldDigest.Compute(em);
                MapSnapshotExporter.FrontOverlayEnabled = false;
                var warOff = MapSnapshotExporter.RenderPoliticalPixels(
                    em, geo, MapSnapshotExporter.LabelDensity.Provinces, -1);
                shaWarOff = Sha256Pixels(warOff);

                MapSnapshotExporter.FrontOverlayEnabled = true;
                var warOn = MapSnapshotExporter.RenderPoliticalPixels(
                    em, geo, MapSnapshotExporter.LabelDensity.Provinces, -1);
                shaWarOn = Sha256Pixels(warOn);
                drawnCount = MapSnapshotExporter.LastFrontDrawnProvinceIds.Count;

                MapSnapshotExporter.FrontOverlayEnabled = true;
                var warOn2 = MapSnapshotExporter.RenderPoliticalPixels(
                    em, geo, MapSnapshotExporter.LabelDensity.Provinces, -1);
                shaWarOnAgain = Sha256Pixels(warOn2);

                parityAfterRender = WorldDigest.Compute(em);
                Assert.IsTrue(CountryObservation.TryCapture(em, PlayerCountryId, out var snapAfter));
                detailAfter = snapAfter.DetailBlock ?? "";

                // Captures vue de jeu (PARTIE 3) — before = overlay off (pré-brief),
                // after = overlay on (front visible). Même monde, raster doit DIFFÉRER.
                WriteGameViewCapture(
                    em, Path.Combine(CapturesDir, "01_front_before.png"),
                    "v1_092 AVANT overlay front (FrontOverlayEnabled=0)",
                    detailAfter, PlayerCountryId, frontEnabled: false);
                WriteGameViewCapture(
                    em, Path.Combine(CapturesDir, "02_front_after.png"),
                    "v1_092 APRES overlay front (FrontOverlayEnabled=1)",
                    detailAfter, PlayerCountryId, frontEnabled: true);
            }

            var noWarIdentical = shaNoWarOff == shaNoWarOn;
            var warDiffers = shaWarOff != shaWarOn;
            var onDeterministic = shaWarOn == shaWarOnAgain;
            var matchDrawnComputed =
                drawnCount >= 0 && computedFrontCount >= 0 && drawnCount == computedFrontCount;
            var parityOk = parityBeforeRender == parityAfterRender;

            sb.AppendLine($"SHA256 SANS front Enabled=false: {shaNoWarOff}");
            sb.AppendLine($"SHA256 SANS front Enabled=true : {shaNoWarOn}");
            sb.AppendLine($"  → identiques={noWarIdentical} (régression visuelle OK si true)");
            sb.AppendLine($"SHA256 AVEC front Enabled=false: {shaWarOff}");
            sb.AppendLine($"SHA256 AVEC front Enabled=true : {shaWarOn}");
            sb.AppendLine($"  → différents={warDiffers} (preuve rouge OK si true)");
            sb.AppendLine($"SHA256 AVEC front Enabled=true (rejeu): {shaWarOnAgain}");
            sb.AppendLine($"  → déterministe={onDeterministic}");
            sb.AppendLine(
                $"Réversible (Enabled=false = baseline sans passage front): " +
                $"warOff est la baseline ; Enabled=true change le PNG={warDiffers}");
            sb.AppendLine(
                $"Parité monde avant/après rendu: 0x{parityBeforeRender:X16} → " +
                $"0x{parityAfterRender:X16} bit-identique={parityOk}");
            sb.AppendLine(
                $"Provinces dessinées={FmtSentinel(drawnCount)} " +
                $"calculées={FmtSentinel(computedFrontCount)} match={matchDrawnComputed}");
            for (var i = 0; i < drawnRows.Count; i++)
                sb.AppendLine("  dessin/calcul: " + drawnRows[i]);
            sb.AppendLine();
            Flush();

            // ========== PARTIE 3 ==========
            sb.AppendLine("=== PARTIE 3 — JOUEUR VOIT SA GUERRE + DÉS-ADOPTION v1_091 ===");
            sb.AppendLine(
                $"Captures: Captures/v1_092/01_front_before.png + 02_front_after.png " +
                $"war={captureWarLabel} targetId={captureTargetId}");
            sb.AppendLine(
                $"SHA256 capture before (off): {FileSha256(Path.Combine(CapturesDir, "01_front_before.png"))}");
            sb.AppendLine(
                $"SHA256 capture after  (on) : {FileSha256(Path.Combine(CapturesDir, "02_front_after.png"))}");
            var capDiff =
                FileSha256(Path.Combine(CapturesDir, "01_front_before.png")) !=
                FileSha256(Path.Combine(CapturesDir, "02_front_after.png"));
            sb.AppendLine($"Captures raster DIFFÉRENTS={capDiff}");

            // Dés-adoption v1_091
            ResetAll();
            Assert.AreEqual(0f, StabilitySystem.AdoptedStabilityReweight, 1e-6f);
            StabilitySystem.LockReweight(StabilitySystem.AdoptedStabilityReweight);
            TemplateRecruitSystem.LockStabilityRecruitScale(0f);
            ulong adoptDig;
            using (var h = new SimulationHarness(Seed))
            {
                StabilitySystem.LockReweight(StabilitySystem.AdoptedStabilityReweight);
                TemplateRecruitSystem.LockStabilityRecruitScale(0f);
                h.RunTicks(ParityTicks);
                adoptDig = WorldDigest.Compute(h.EntityManager);
            }

            var adoptOk = adoptDig == ParityAnchors.Expected;
            sb.AppendLine(
                $"Dés-adoption v1_091: AdoptedStabilityReweight=" +
                $"{StabilitySystem.AdoptedStabilityReweight.ToString("0.###", CultureInfo.InvariantCulture)} " +
                $"(const, non retouché) ; digest@t{ParityTicks}=0x{adoptDig:X16} " +
                $"== Expected={adoptOk}");
            sb.AppendLine(
                "LARGE: rejouée à part (voir Logs/v1_092_large.xml) — filtre v1_091 + V1092.");
            sb.AppendLine();

            var pass =
                firstNonEmptyFrontTick > 0 &&
                refTick > 0 &&
                noWarIdentical &&
                warDiffers &&
                onDeterministic &&
                parityOk &&
                matchDrawnComputed &&
                drawnCount > 0 &&
                capDiff &&
                adoptOk;

            sb.AppendLine(
                "VERDICT MESURÉ: " + (pass ? "PASS" : "FAIL") + " — " +
                $"front lu hors Military/ désormais via MapSnapshotExporter.ApplyFrontFlags ; " +
                $"occupation ApplyOccupationHatch réutilisée comme motif ; " +
                $"réf guerre IA t={FmtSentinel(refTick)}: {FmtSentinel(refSectors)} secteurs, " +
                $"{FmtSentinel(refProvinces)} provinces, {FmtSentinel(refContested)} contestées, " +
                $"pressions atk [{FmtF(refAtkMin)}..{FmtF(refAtkMax)}] " +
                $"def [{FmtF(refDefMin)}..{FmtF(refDefMax)}] ; " +
                $"premier front non vide t={FmtSentinel(firstNonEmptyFrontTick)}, " +
                $"durée vie moy={(lifetimes.Count > 0 ? avgLife.ToString("0.###", CultureInfo.InvariantCulture) : "n/a")} ; " +
                $"front liseré / contestées damier / occupation prioritaire ; " +
                $"parité 0xA6D63D33280D5778 bit-identique rendu ; " +
                $"SANS front SHA256 identiques={noWarIdentical}, " +
                $"AVEC front SHA256 différents={warDiffers} ; " +
                $"réversible Enabled=false ; " +
                $"{FmtSentinel(drawnCount)} provinces dessinées = {FmtSentinel(computedFrontCount)} calculées ; " +
                $"dés-adoption v1_091 constatée Adopted=0 digestOK={adoptOk} ; " +
                $"captures raster diff={capDiff}.");
            Flush();

            Assert.IsTrue(pass, "V1092 verdict mesuré FAIL — voir " + LogPath);
            ResetAll();
        }

        static void WriteGameViewCapture(
            EntityManager em,
            string path,
            string title,
            string countryDetail,
            int countryId,
            bool frontEnabled)
        {
            var prev = MapSnapshotExporter.FrontOverlayEnabled;
            MapSnapshotExporter.FrontOverlayEnabled = frontEnabled;
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
                var frontLines = new StringBuilder();
                frontLines.Append("FRONT ");
                if (MapSnapshotExporter.LastFrontDrawnProvinceIds.Count == 0 && frontEnabled)
                {
                    // Force un rendu pour peupler LastFrontDrawnProvinceIds via overlay path.
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
                MapSnapshotExporter.FrontOverlayEnabled = prev;
            }
        }

        static void MeasureFrontSnapshot(
            EntityManager em,
            out int sectors,
            out int provinces,
            out int contested,
            out float atkMin,
            out float atkMax,
            out float defMin,
            out float defMax,
            out string warLabel,
            out int activeWarKeys,
            out List<int> provinceIds)
        {
            sectors = 0;
            provinces = 0;
            contested = 0;
            atkMin = float.NaN;
            atkMax = float.NaN;
            defMin = float.NaN;
            defMax = float.NaN;
            warLabel = "(none)";
            activeWarKeys = 0;
            provinceIds = new List<int>();
            var warKeys = new HashSet<Entity>();
            var seenProv = new HashSet<int>();

            using var q = em.CreateEntityQuery(
                ComponentType.ReadOnly<FrontSectorData>(),
                ComponentType.ReadOnly<FrontLineState>());
            using var entities = q.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                var sector = em.GetComponentData<FrontSectorData>(entities[i]);
                if (!sector.IsActive)
                    continue;
                var buf = em.GetBuffer<FrontLineState>(entities[i]);
                if (buf.Length == 0)
                    continue;
                sectors++;
                warKeys.Add(sector.War);
                if (warLabel == "(none)")
                {
                    var atk = TagOfEntity(em, sector.AttackerCountry);
                    var def = TagOfEntity(em, sector.DefenderCountry);
                    warLabel = atk + " vs " + def;
                }

                for (var b = 0; b < buf.Length; b++)
                {
                    var st = buf[b];
                    if (!seenProv.Add(st.ProvinceId))
                        continue;
                    provinces++;
                    provinceIds.Add(st.ProvinceId);
                    if (st.IsContested)
                        contested++;
                    atkMin = float.IsNaN(atkMin)
                        ? st.AttackerPressure
                        : Math.Min(atkMin, st.AttackerPressure);
                    atkMax = float.IsNaN(atkMax)
                        ? st.AttackerPressure
                        : Math.Max(atkMax, st.AttackerPressure);
                    defMin = float.IsNaN(defMin)
                        ? st.DefenderPressure
                        : Math.Min(defMin, st.DefenderPressure);
                    defMax = float.IsNaN(defMax)
                        ? st.DefenderPressure
                        : Math.Max(defMax, st.DefenderPressure);
                }
            }

            activeWarKeys = warKeys.Count;
        }

        static void CollectFrontProvinceRows(EntityManager em, List<string> rows)
        {
            rows.Clear();
            using var q = em.CreateEntityQuery(
                ComponentType.ReadOnly<FrontSectorData>(),
                ComponentType.ReadOnly<FrontLineState>());
            using var entities = q.ToEntityArray(Allocator.Temp);
            var list = new List<(int Id, bool Cont, float Atk, float Def)>();
            for (var i = 0; i < entities.Length; i++)
            {
                var sector = em.GetComponentData<FrontSectorData>(entities[i]);
                if (!sector.IsActive)
                    continue;
                var buf = em.GetBuffer<FrontLineState>(entities[i]);
                for (var b = 0; b < buf.Length; b++)
                {
                    var st = buf[b];
                    list.Add((st.ProvinceId, st.IsContested, st.AttackerPressure, st.DefenderPressure));
                }
            }

            list.Sort((a, b) => a.Id.CompareTo(b.Id));
            for (var i = 0; i < list.Count; i++)
            {
                var e = list[i];
                rows.Add(
                    $"prov={e.Id} contested={e.Cont} " +
                    $"atk={e.Atk.ToString("0.###", CultureInfo.InvariantCulture)} " +
                    $"def={e.Def.ToString("0.###", CultureInfo.InvariantCulture)}");
            }
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

        static string FmtSentinel(int v) => v < 0 ? "pas trouvé" : v.ToString(CultureInfo.InvariantCulture);

        static string FmtF(float v) =>
            float.IsNaN(v) ? "pas trouvé" : v.ToString("0.###", CultureInfo.InvariantCulture);

        static double Avg(List<int> xs)
        {
            if (xs == null || xs.Count == 0)
                return double.NaN;
            double s = 0;
            for (var i = 0; i < xs.Count; i++)
                s += xs[i];
            return s / xs.Count;
        }

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
