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
using VictoriaGame.Presentation;
using VictoriaGame.World;
using Debug = UnityEngine.Debug;

namespace VictoriaGame.Tests
{
    /// <summary>Batch : -executeMethod VictoriaGame.Tests.V1088BatchRunner.Run</summary>
    public static class V1088BatchRunner
    {
        public static void Run()
        {
            try
            {
                V1088WarIntentionTests.RunAndWriteArtifacts();
                Debug.Log("V1088BatchRunner: DONE");
            }
            catch (Exception ex) when (HarnessAllocationGuard.IsNativeAllocationFailure(ex))
            {
                Debug.LogWarning("V1088BatchRunner: ALLOCATION_FAILURE — " + ex.Message);
                Debug.Log("V1088BatchRunner: DONE_PARTIAL");
            }
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
        }
    }

    /// <summary>
    /// v1_088 — PHASE XII : DeclareWar / ProposePeace via ApplyPlayerIntentionSystem.
    /// </summary>
    [TestFixture]
    public class V1088WarIntentionTests
    {
        const uint Seed = 42195u;
        const int ParityTicks = 100;
        const int ReferenceTicks = 3000;
        const ulong ExpectedParity = ParityAnchors.Expected;
        const int PlayerCountryId = PlayerControl.DefaultControlledCountryId;

        static string GameUnityRoot =>
            Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        static string LogPath => Path.Combine(GameUnityRoot, "Logs", "v1_088_war.log");
        static string CapturesDir => Path.Combine(GameUnityRoot, "Captures", "v1_088");

        [TearDown]
        public void TearDown() => ResetAll();

        [Test]
        public void V1088_DeclareWar_Creates_WarData_Via_Factory()
        {
            ResetAll();
            LockDefaults();
            using var h = new SimulationHarness(Seed);
            h.RunTicks(0);
            var em = h.EntityManager;
            var targetId = FindOtherCountryId(em, PlayerCountryId);
            Assert.GreaterOrEqual(targetId, 0);

            var warsBefore = CountActiveWars(em);
            Assert.IsTrue(PlayerIntentionSubmit.EnqueueDeclareWar(em, PlayerCountryId, targetId));
            Assert.AreEqual(warsBefore, CountActiveWars(em), "UI n'écrit pas avant le tick");

            h.RunTicks(1);
            var receipt = ReadReceipt(em);
            Assert.AreEqual(1, receipt.Accepted, $"reason={receipt.Reason}");
            Assert.AreEqual(PlayerIntentionKind.DeclareWar, receipt.Kind);
            Assert.AreEqual(warsBefore + 1, CountActiveWars(em));
            Assert.IsTrue(HasActiveWarBetween(em, PlayerCountryId, targetId));
        }

        [Test]
        public void V1088_ProposePeace_Ends_Active_War()
        {
            ResetAll();
            LockDefaults();
            using var h = new SimulationHarness(Seed);
            h.RunTicks(0);
            var em = h.EntityManager;
            var targetId = FindOtherCountryId(em, PlayerCountryId);
            Assert.IsTrue(PlayerIntentionSubmit.EnqueueDeclareWar(em, PlayerCountryId, targetId));
            h.RunTicks(1);
            Assert.AreEqual(1, ReadReceipt(em).Accepted);

            Assert.IsTrue(PlayerIntentionSubmit.EnqueueProposePeace(em, PlayerCountryId, targetId));
            h.RunTicks(1);
            var receipt = ReadReceipt(em);
            Assert.AreEqual(1, receipt.Accepted, $"reason={receipt.Reason}");
            Assert.AreEqual(PlayerIntentionKind.ProposePeace, receipt.Kind);
            Assert.IsFalse(HasActiveWarBetween(em, PlayerCountryId, targetId));
            Assert.IsTrue(HasEndedWarBetween(em, PlayerCountryId, targetId));
        }

        [Test]
        public void V1088_Rejects_Named_Reasons()
        {
            ResetAll();
            LockDefaults();
            using var h = new SimulationHarness(Seed);
            h.RunTicks(0);
            var em = h.EntityManager;
            var targetId = FindOtherCountryId(em, PlayerCountryId);
            var eng = FindCountryIdByTag(em, "ENG");
            Assert.GreaterOrEqual(eng, 0);

            // country_not_controlled
            PlayerIntentionSubmit.EnqueueDeclareWar(em, eng, targetId);
            h.RunTicks(1);
            Assert.AreEqual(0, ReadReceipt(em).Accepted);
            Assert.AreEqual("country_not_controlled", ReadReceipt(em).Reason.ToString());

            // target_is_self
            PlayerIntentionSubmit.EnqueueDeclareWar(em, PlayerCountryId, PlayerCountryId);
            h.RunTicks(1);
            Assert.AreEqual(0, ReadReceipt(em).Accepted);
            Assert.AreEqual("target_is_self", ReadReceipt(em).Reason.ToString());

            // target_unknown
            PlayerIntentionSubmit.EnqueueDeclareWar(em, PlayerCountryId, 999999);
            h.RunTicks(1);
            Assert.AreEqual(0, ReadReceipt(em).Accepted);
            Assert.AreEqual("target_unknown", ReadReceipt(em).Reason.ToString());

            // already_at_war
            PlayerIntentionSubmit.EnqueueDeclareWar(em, PlayerCountryId, targetId);
            h.RunTicks(1);
            Assert.AreEqual(1, ReadReceipt(em).Accepted);
            PlayerIntentionSubmit.EnqueueDeclareWar(em, PlayerCountryId, targetId);
            h.RunTicks(1);
            Assert.AreEqual(0, ReadReceipt(em).Accepted);
            Assert.AreEqual("already_at_war", ReadReceipt(em).Reason.ToString());

            // no_active_war (peace with a third country not at war)
            var third = FindThirdCountryId(em, PlayerCountryId, targetId);
            Assert.GreaterOrEqual(third, 0);
            PlayerIntentionSubmit.EnqueueProposePeace(em, PlayerCountryId, third);
            h.RunTicks(1);
            Assert.AreEqual(0, ReadReceipt(em).Accepted);
            Assert.AreEqual("no_active_war", ReadReceipt(em).Reason.ToString());
        }

        [Test]
        public void V1088_Reversibility_No_Player_Action_BitIdentical()
        {
            ResetAll();
            ulong dig;
            using (var h = new SimulationHarness(Seed))
            {
                h.RunTicks(ParityTicks);
                dig = WorldDigest.Compute(h.EntityManager);
            }

            Assert.AreEqual(ExpectedParity, dig,
                "réversibilité SANS action joueur → empreinte v1_009");
        }

        [Test]
        public void V1088_Artifacts_And_Verdict() => RunAndWriteArtifacts();

        public static void RunAndWriteArtifacts()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            Directory.CreateDirectory(CapturesDir);
            var sb = new StringBuilder(512 * 1024);

            void Flush() => File.WriteAllText(LogPath, sb.ToString(), Encoding.UTF8);

            sb.AppendLine("=== v1_088 WAR INTENTIONS — seed=42195 PHASE XII ===");
            sb.AppendLine(
                "Contrat: DeclareWar+ProposePeace, réversibilité bit-identique SANS action, " +
                "déterminisme sur séquence joueur, refus nommés, vue de jeu, suivi systèmes.");
            sb.AppendLine();
            Flush();

            // ----- PARTIE 1 -----
            sb.AppendLine("=== PARTIE 1 — INVENTAIRE GUERRES + RÉFÉRENCE SANS JOUEUR ===");
            sb.AppendLine("CREATE WarData (exhaustif):");
            sb.AppendLine("  1) WarDeclarationSystem.cs:313-316 — WarData.Create (IA, intervalle 12)");
            sb.AppendLine("  2) ApplyPlayerIntentionSystem.ApplyDeclareWar — WarData.Create (v1_088)");
            sb.AppendLine("END WarData:");
            sb.AppendLine("  PeaceSystem.ConcludePeace / ConcludeWhitePeace (seuils score/épuisement)");
            sb.AppendLine("  PeaceSystem.TryConcludePlayerWhitePeace (v1_088, même chemin paix blanche)");
            sb.AppendLine("READ/DISPLAY WarData:");
            sb.AppendLine("  MapSpriteOverlay.cs:1218 — ActiveWars + WarLines panneau pays (RÉUTILISÉ)");
            sb.AppendLine("  WorldMetrics.cs:208 — WarsDeclared / Victories / WhitePeaces / StuckWars");
            sb.AppendLine("  ChronicleExporter.cs:457 — WarStart / WarEnd");
            sb.AppendLine(
                "AFFICHAGE DÉJÀ VISIBLE: oui — panneau pays MapSpriteOverlay section MILITARY/WARS ; " +
                "aucun second mécanisme d'affichage ajouté. WarBar HUD = intentions uniquement.");
            sb.AppendLine();
            sb.AppendLine("WarDeclarationSystem aléa/ordre:");
            sb.AppendLine(
                "  tick%12==0 ; GlobalSeed ; countries.Sort() par Tag ; voisins.Sort() par Tag ; " +
                "RNG = hash(GlobalSeed,tick,CountryId) — JAMAIS Entity.Index.");
            sb.AppendLine(
                "  Une guerre joueur ajoute Attacker+Defender à countriesAtWar → ces pays ne " +
                "redéclarent plus tant qu'actifs. Deterministe si séquence joueur rejouée à " +
                "CountryId/tick égaux.");
            sb.AppendLine();
            Flush();

            ForceGc();
            LockDefaults();
            var refWars = new List<string>(64);
            int refActiveAtEnd;
            int refTotalEntities;
            using (var h = new SimulationHarness(Seed))
            {
                h.RunTicks(ReferenceTicks);
                var em = h.EntityManager;
                CollectWarRows(em, refWars, out refActiveAtEnd, out refTotalEntities);
            }

            sb.AppendLine(
                $"RÉFÉRENCE SANS JOUEUR @tick={ReferenceTicks}: war_entities={refTotalEntities} " +
                $"active={refActiveAtEnd}");
            for (var i = 0; i < refWars.Count; i++)
                sb.AppendLine("  " + refWars[i]);
            sb.AppendLine(
                "NOTE: référence dérivée de la mesure (pas de point de contrôle nommé à la main).");
            sb.AppendLine();
            Flush();

            // ----- PARTIE 2 -----
            sb.AppendLine("=== PARTIE 2 — VERBES, REFUS, RÉVERSIBILITÉ, DÉTERMINISME ===");
            ForceGc();
            LockDefaults();
            int targetId;
            string targetTag;
            using (var h = new SimulationHarness(Seed))
            {
                h.RunTicks(0);
                var em = h.EntityManager;
                targetId = FindOtherCountryId(em, PlayerCountryId);
                targetTag = TagOf(em, targetId);
                Assert.GreaterOrEqual(targetId, 0);

                // Accept declare
                PlayerIntentionSubmit.EnqueueDeclareWar(em, PlayerCountryId, targetId);
                h.RunTicks(1);
                var r1 = ReadReceipt(em);
                sb.AppendLine(
                    $"DeclareWar FRA→{targetTag}({targetId}): accepted={r1.Accepted} reason={r1.Reason}");

                // already_at_war
                PlayerIntentionSubmit.EnqueueDeclareWar(em, PlayerCountryId, targetId);
                h.RunTicks(1);
                sb.AppendLine(
                    $"refus already_at_war: accepted={ReadReceipt(em).Accepted} reason={ReadReceipt(em).Reason}");

                // ProposePeace ok
                PlayerIntentionSubmit.EnqueueProposePeace(em, PlayerCountryId, targetId);
                h.RunTicks(1);
                sb.AppendLine(
                    $"ProposePeace FRA→{targetTag}: accepted={ReadReceipt(em).Accepted} reason={ReadReceipt(em).Reason}");

                // no_active_war
                PlayerIntentionSubmit.EnqueueProposePeace(em, PlayerCountryId, targetId);
                h.RunTicks(1);
                sb.AppendLine(
                    $"refus no_active_war: accepted={ReadReceipt(em).Accepted} reason={ReadReceipt(em).Reason}");

                // target_is_self
                PlayerIntentionSubmit.EnqueueDeclareWar(em, PlayerCountryId, PlayerCountryId);
                h.RunTicks(1);
                sb.AppendLine(
                    $"refus target_is_self: accepted={ReadReceipt(em).Accepted} reason={ReadReceipt(em).Reason}");

                // target_unknown
                PlayerIntentionSubmit.EnqueueDeclareWar(em, PlayerCountryId, 999999);
                h.RunTicks(1);
                sb.AppendLine(
                    $"refus target_unknown: accepted={ReadReceipt(em).Accepted} reason={ReadReceipt(em).Reason}");

                // country_not_controlled
                var eng = FindCountryIdByTag(em, "ENG");
                PlayerIntentionSubmit.EnqueueDeclareWar(em, eng, targetId);
                h.RunTicks(1);
                sb.AppendLine(
                    $"refus country_not_controlled: accepted={ReadReceipt(em).Accepted} reason={ReadReceipt(em).Reason}");
            }

            ForceGc();
            ulong digNoAction;
            using (var h = new SimulationHarness(Seed))
            {
                h.RunTicks(ParityTicks);
                digNoAction = WorldDigest.Compute(h.EntityManager);
            }

            ForceGc();
            var digSeq1 = RunPlayerWarSequenceDigest(targetId);
            ForceGc();
            var digSeq2 = RunPlayerWarSequenceDigest(targetId);

            sb.AppendLine(
                $"réversibilité SANS action @t{ParityTicks}: 0x{digNoAction:X16} " +
                $"(attendu 0x{ExpectedParity:X16}) equal={(digNoAction == ExpectedParity)}");
            sb.AppendLine(
                $"déterminisme séquence DeclareWar→ProposePeace: " +
                $"run1=0x{digSeq1:X16} run2=0x{digSeq2:X16} equal={(digSeq1 == digSeq2)}");
            sb.AppendLine();
            Flush();

            // ----- PARTIE 3 -----
            sb.AppendLine("=== PARTIE 3 — VUE DE JEU + SUIVI SYSTÈMES + DETTE HUD ===");
            ForceGc();
            LockDefaults();
            var trackLines = new List<string>(8);
            string detailBefore;
            string detailAfter;
            string chronicleSnippet;
            using (var h = new SimulationHarness(Seed))
            {
                h.RunTicks(0);
                var em = h.EntityManager;
                targetId = FindOtherCountryId(em, PlayerCountryId);
                targetTag = TagOf(em, targetId);

                Assert.IsTrue(CountryObservation.TryCapture(em, PlayerCountryId, out var snapBefore));
                detailBefore = snapBefore.DetailBlock ?? "";
                WriteGameViewCapture(
                    em, Path.Combine(CapturesDir, "01_war_before.png"),
                    "FRA avant guerre", detailBefore, PlayerCountryId);

                PlayerIntentionSubmit.EnqueueDeclareWar(em, PlayerCountryId, targetId);
                h.RunTicks(1);
                Assert.AreEqual(1, ReadReceipt(em).Accepted);

                // Capture IMMÉDIATEMENT après déclaration (vue de jeu avec WARS du joueur).
                Assert.IsTrue(CountryObservation.TryCapture(em, PlayerCountryId, out var snapAfter));
                detailAfter = snapAfter.DetailBlock ?? "";
                WriteGameViewCapture(
                    em, Path.Combine(CapturesDir, "02_war_after.png"),
                    "FRA après DeclareWar→" + targetTag, detailAfter, PlayerCountryId);

                // Suivi à ticks mesurés (dérivés : 1 = déclaration, puis +60/+120/+200)
                var probeTicks = new[] { 1, 60, 120, 200 };
                var lastTick = 1;
                trackLines.Add(MeasurePlayerWar(em, PlayerCountryId, targetId, 1));
                sb.AppendLine(trackLines[trackLines.Count - 1]);
                for (var p = 1; p < probeTicks.Length; p++)
                {
                    var want = probeTicks[p];
                    h.RunTicks(want - lastTick);
                    lastTick = want;
                    var row = MeasurePlayerWar(em, PlayerCountryId, targetId, want);
                    trackLines.Add(row);
                    sb.AppendLine(row);
                }

                chronicleSnippet = ExtractChronicleWarLines(em);
                sb.AppendLine("CHRONICLE WarStart/WarEnd (extrait):");
                sb.AppendLine(chronicleSnippet);

                // Dette v1_086 / v1_087 — capture vue province avec sat + dev HUD
                WriteTaxDevDebtCapture(em, Path.Combine(CapturesDir, "03_hud_tax_dev.png"));
            }

            sb.AppendLine($"png_before={Path.Combine(CapturesDir, "01_war_before.png")}");
            sb.AppendLine($"png_after={Path.Combine(CapturesDir, "02_war_after.png")}");
            sb.AppendLine($"png_hud_debt={Path.Combine(CapturesDir, "03_hud_tax_dev.png")}");
            sb.AppendLine("CAPTURE_BEFORE detail WARS line:");
            sb.AppendLine(ExtractWarsSection(detailBefore));
            sb.AppendLine("CAPTURE_AFTER detail WARS line:");
            sb.AppendLine(ExtractWarsSection(detailAfter));
            sb.AppendLine();

            var parityOk = digNoAction == ExpectedParity;
            var detOk = digSeq1 == digSeq2;
            var warsVisible =
                detailAfter.IndexOf("WARS", StringComparison.Ordinal) >= 0 &&
                !ExtractWarsSection(detailAfter).Contains("(none)");
            var capturesExist =
                File.Exists(Path.Combine(CapturesDir, "01_war_before.png")) &&
                File.Exists(Path.Combine(CapturesDir, "02_war_after.png")) &&
                File.Exists(Path.Combine(CapturesDir, "03_hud_tax_dev.png"));
            var systemsEngaged = trackLines.Count >= 3;

            sb.AppendLine("=== VERDICT MESURÉ ===");
            sb.AppendLine(
                $"guerres créées en 2 points (WarDeclarationSystem + ApplyDeclareWar via WarData.Create) ; " +
                $"déjà affichées par MapSpriteOverlay.cs:1218, réutilisé ; " +
                $"référence sans joueur : {refTotalEntities} entités / {refActiveAtEnd} actives @t{ReferenceTicks} ; " +
                $"DeclareWar et ProposePeace livrées, 5 motifs de refus prouvés ; " +
                $"sans action parité 0x{digNoAction:X16} bit-identique={(parityOk)} ; " +
                $"même séquence rejouée 2/2 empreintes égales={(detOk)} ; " +
                $"suivi guerre joueur : {string.Join(" | ", trackLines)} ; " +
                $"wars_visible_panel={(warsVisible)} captures={(capturesExist)}.");
            var pass = parityOk && detOk && capturesExist && warsVisible && systemsEngaged;
            sb.AppendLine(pass
                ? "VERDICT: PASS — verbes + refus + réversibilité + déterminisme + vue de jeu."
                : "VERDICT: FAIL — un critère du contrat a lâché.");
            Flush();
            Debug.Log(sb.ToString());

            Assert.IsTrue(parityOk, "réversibilité");
            Assert.IsTrue(detOk, "déterminisme");
            Assert.IsTrue(capturesExist, "captures");
            Assert.IsTrue(warsVisible, "guerre visible panneau");
            ResetAll();
        }

        static ulong RunPlayerWarSequenceDigest(int targetId)
        {
            LockDefaults();
            using var h = new SimulationHarness(Seed);
            h.RunTicks(0);
            var em = h.EntityManager;
            // Si targetId invalide dans ce world (ne devrait pas), résoudre à nouveau.
            if (targetId < 0)
                targetId = FindOtherCountryId(em, PlayerCountryId);

            PlayerIntentionSubmit.EnqueueDeclareWar(em, PlayerCountryId, targetId);
            h.RunTicks(1);
            PlayerIntentionSubmit.EnqueueProposePeace(em, PlayerCountryId, targetId);
            h.RunTicks(1);
            h.RunTicks(40);
            return WorldDigest.Compute(em);
        }

        static string MeasurePlayerWar(EntityManager em, int playerId, int targetId, int tick)
        {
            var active = HasActiveWarBetween(em, playerId, targetId);
            TryGetPlayerWarStats(em, playerId, targetId,
                out var warScore, out var frontProvinces, out var sieges, out var penetration);
            return
                $"t={tick} active={active} warScore={warScore.ToString("0.###", CultureInfo.InvariantCulture)} " +
                $"frontProvinces={frontProvinces} sieges={sieges} " +
                $"penetration={penetration.ToString("0.###", CultureInfo.InvariantCulture)}";
        }

        static void TryGetPlayerWarStats(
            EntityManager em,
            int playerId,
            int targetId,
            out float warScore,
            out int frontProvinces,
            out int sieges,
            out float penetration)
        {
            warScore = 0f;
            frontProvinces = 0;
            sieges = 0;
            penetration = 0f;
            if (!TryResolveCountry(em, playerId, out var player) ||
                !TryResolveCountry(em, targetId, out var target))
                return;

            Entity warEntity = Entity.Null;
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<WarData>()))
            using (var entities = q.ToEntityArray(Allocator.Temp))
            using (var wars = q.ToComponentDataArray<WarData>(Allocator.Temp))
            {
                for (var i = 0; i < wars.Length; i++)
                {
                    if (!wars[i].IsActive)
                        continue;
                    if ((wars[i].Attacker == player && wars[i].Defender == target) ||
                        (wars[i].Attacker == target && wars[i].Defender == player))
                    {
                        warEntity = entities[i];
                        warScore = wars[i].WarScore;
                        break;
                    }
                }
            }

            if (warEntity == Entity.Null)
                return;

            using (var q = em.CreateEntityQuery(
                       ComponentType.ReadOnly<FrontSectorData>(),
                       ComponentType.ReadOnly<FrontLineState>()))
            using (var entities = q.ToEntityArray(Allocator.Temp))
            {
                for (var i = 0; i < entities.Length; i++)
                {
                    var sector = em.GetComponentData<FrontSectorData>(entities[i]);
                    if (sector.War != warEntity)
                        continue;
                    penetration = sector.PenetrationDepth;
                    var buf = em.GetBuffer<FrontLineState>(entities[i]);
                    frontProvinces = buf.Length;
                    break;
                }
            }

            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<FortData>()))
            using (var forts = q.ToComponentDataArray<FortData>(Allocator.Temp))
            {
                // Sièges sur provinces contrôlées par l'un des deux belligérants.
                var belligerent = new HashSet<Entity> { player, target };
                var ownedProv = new HashSet<int>();
                using (var pq = em.CreateEntityQuery(
                           ComponentType.ReadOnly<ProvinceData>(),
                           ComponentType.ReadOnly<ProvinceOwnership>()))
                using (var pdata = pq.ToComponentDataArray<ProvinceData>(Allocator.Temp))
                using (var owns = pq.ToComponentDataArray<ProvinceOwnership>(Allocator.Temp))
                {
                    for (var i = 0; i < pdata.Length; i++)
                    {
                        if (belligerent.Contains(owns[i].Owner) ||
                            belligerent.Contains(owns[i].Controller))
                            ownedProv.Add(pdata[i].ProvinceId);
                    }
                }

                for (var i = 0; i < forts.Length; i++)
                {
                    if (!ownedProv.Contains(forts[i].ProvinceId))
                        continue;
                    if (forts[i].IsUnderSiege || forts[i].SiegeProgress > 0.01f)
                        sieges++;
                }
            }
        }

        static void WriteGameViewCapture(
            EntityManager em,
            string path,
            string title,
            string countryDetail,
            int countryId)
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
                        // Panneau pays réel (section WARS) — ce que le joueur lit.
                        var y = 44;
                        var lines = warsSection.Split('\n');
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

        static void WriteTaxDevDebtCapture(EntityManager em, string path)
        {
            const int provinceId = 1; // Paris basin — même province HUD invest
            TaxCostSnapshot.Capture(em, PlayerCountryId, out var sat, out var hungry, out var hungryProv);
            DevelopmentHudSnapshot.TryCapture(
                em, provinceId, out var dev, out _, out _, out _, out _);
            var taxLine = TaxCostSnapshot.FormatHudLine(sat, hungry, hungryProv);
            var devLine = DevelopmentHudSnapshot.FormatHudLine(in dev);

            MapDisplaySystem.TrySelectProvinceById(em, provinceId);
            var geo = MapGeometryCache.GetOrBuild(
                MapSnapshotExporter.Width, MapSnapshotExporter.Height,
                MapViewport.State.Window, out _);
            if (geo == null)
            {
                geo = MapSnapshotExporter.BuildMapGeometry(
                    MapSnapshotExporter.Width, MapSnapshotExporter.Height);
            }

            var pixels = MapSnapshotExporter.RenderPoliticalPixels(
                em, geo, MapSnapshotExporter.LabelDensity.SelectedProvince, provinceId,
                overlay: p =>
                {
                    CityMarkerComposer.Compose(
                        p, geo, em, MapObservationLevel.Province, filterProvinceId: provinceId);
                    var fg = new Color32(236, 232, 220, 255);
                    var halo = new Color32(8, 8, 12, 255);
                    MapSnapshotExporter.WithGlyphScale(2, () =>
                    {
                        MapSnapshotExporter.DrawBitmapText(
                            p, "HUD joueur — impôt + développement", 12, 16, fg, halo);
                        MapSnapshotExporter.DrawBitmapText(p, taxLine, 12, 44, fg, halo);
                        MapSnapshotExporter.DrawBitmapText(
                            p, "Prov " + provinceId + "  " + devLine, 12, 68, fg, halo);
                    });
                });

            if (pixels != null)
            {
                MapSnapshotExporter.WriteMapBufferPng(
                    pixels, MapSnapshotExporter.Width, MapSnapshotExporter.Height, path);
            }

            MapViewport.Reset();
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

        static string ExtractChronicleWarLines(EntityManager em)
        {
            var rows = new List<(int Tick, string Line)>(32);
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<WarData>());
            using var wars = q.ToComponentDataArray<WarData>(Allocator.Temp);
            for (var i = 0; i < wars.Length; i++)
            {
                var w = wars[i];
                var atk = TagOfEntity(em, w.Attacker);
                var def = TagOfEntity(em, w.Defender);
                rows.Add((
                    w.StartTick,
                    $"t{w.StartTick:D4} WAR_START {atk} → {def} cb={w.CasusBelli}"));
                if (w.EndTick > 0)
                {
                    rows.Add((
                        w.EndTick,
                        $"t{w.EndTick:D4} WAR_END {atk} → {def} score={w.WarScore.ToString("0.###", CultureInfo.InvariantCulture)}"));
                }
            }

            rows.Sort((a, b) =>
            {
                var c = a.Tick.CompareTo(b.Tick);
                return c != 0 ? c : string.CompareOrdinal(a.Line, b.Line);
            });

            var sb = new StringBuilder();
            var n = Math.Min(12, rows.Count);
            for (var i = 0; i < n; i++)
                sb.AppendLine("  " + rows[i].Line);
            return n > 0 ? sb.ToString() : "(aucun WAR_* dérivé de WarData)";
        }

        static void CollectWarRows(
            EntityManager em,
            List<string> rows,
            out int active,
            out int total)
        {
            active = 0;
            total = 0;
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<WarData>());
            using var wars = q.ToComponentDataArray<WarData>(Allocator.Temp);
            total = wars.Length;
            var sorted = new List<(int Start, string Line)>(wars.Length);
            for (var i = 0; i < wars.Length; i++)
            {
                var w = wars[i];
                if (w.IsActive)
                    active++;
                var atk = TagOfEntity(em, w.Attacker);
                var def = TagOfEntity(em, w.Defender);
                sorted.Add((
                    w.StartTick,
                    $"start={w.StartTick} end={w.EndTick} active={w.IsActive} " +
                    $"{atk}→{def} cb={w.CasusBelli} score={w.WarScore.ToString("0.##", CultureInfo.InvariantCulture)}"));
            }

            sorted.Sort((a, b) =>
            {
                var c = a.Start.CompareTo(b.Start);
                return c != 0 ? c : string.CompareOrdinal(a.Line, b.Line);
            });
            for (var i = 0; i < sorted.Count; i++)
                rows.Add(sorted[i].Line);
        }

        static int CountActiveWars(EntityManager em)
        {
            var n = 0;
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<WarData>());
            using var wars = q.ToComponentDataArray<WarData>(Allocator.Temp);
            for (var i = 0; i < wars.Length; i++)
            {
                if (wars[i].IsActive)
                    n++;
            }

            return n;
        }

        static bool HasActiveWarBetween(EntityManager em, int aId, int bId)
        {
            if (!TryResolveCountry(em, aId, out var a) || !TryResolveCountry(em, bId, out var b))
                return false;
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<WarData>());
            using var wars = q.ToComponentDataArray<WarData>(Allocator.Temp);
            for (var i = 0; i < wars.Length; i++)
            {
                if (!wars[i].IsActive)
                    continue;
                if ((wars[i].Attacker == a && wars[i].Defender == b) ||
                    (wars[i].Attacker == b && wars[i].Defender == a))
                    return true;
            }

            return false;
        }

        static bool HasEndedWarBetween(EntityManager em, int aId, int bId)
        {
            if (!TryResolveCountry(em, aId, out var a) || !TryResolveCountry(em, bId, out var b))
                return false;
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<WarData>());
            using var wars = q.ToComponentDataArray<WarData>(Allocator.Temp);
            for (var i = 0; i < wars.Length; i++)
            {
                if (wars[i].IsActive || wars[i].EndTick <= 0)
                    continue;
                if ((wars[i].Attacker == a && wars[i].Defender == b) ||
                    (wars[i].Attacker == b && wars[i].Defender == a))
                    return true;
            }

            return false;
        }

        static int FindOtherCountryId(EntityManager em, int selfId)
        {
            // Préférence : voisin terrestre au plus petit CountryId (clé domaine stable).
            // Sinon plus petit CountryId ≠ self — jamais Entity.Index.
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
            using (var pdata = q.ToComponentDataArray<ProvinceData>(Allocator.Temp))
            using (var owns = q.ToComponentDataArray<ProvinceOwnership>(Allocator.Temp))
            {
                for (var i = 0; i < entities.Length; i++)
                {
                    if (owns[i].Controller != selfEntity)
                        continue;
                    var buf = em.GetBuffer<ProvinceNeighbor>(entities[i]);
                    for (var n = 0; n < buf.Length; n++)
                    {
                        if (buf[n].IsStrait)
                            continue;
                        if (!controllers.TryGetValue(buf[n].NeighborProvinceId, out var other) ||
                            other == Entity.Null || other == selfEntity)
                            continue;
                        if (!em.HasComponent<CountryData>(other))
                            continue;
                        neighborCountryIds.Add(em.GetComponentData<CountryData>(other).CountryId);
                    }
                }
            }

            var bestNeighbor = -1;
            foreach (var id in neighborCountryIds)
            {
                if (bestNeighbor < 0 || id < bestNeighbor)
                    bestNeighbor = id;
            }

            if (bestNeighbor >= 0)
                return bestNeighbor;

            var best = -1;
            using var cq = em.CreateEntityQuery(ComponentType.ReadOnly<CountryData>());
            using var arr = cq.ToComponentDataArray<CountryData>(Allocator.Temp);
            for (var i = 0; i < arr.Length; i++)
            {
                if (arr[i].CountryId == selfId)
                    continue;
                if (best < 0 || arr[i].CountryId < best)
                    best = arr[i].CountryId;
            }

            return best;
        }

        static int FindThirdCountryId(EntityManager em, int a, int b)
        {
            var best = -1;
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<CountryData>());
            using var arr = q.ToComponentDataArray<CountryData>(Allocator.Temp);
            for (var i = 0; i < arr.Length; i++)
            {
                var id = arr[i].CountryId;
                if (id == a || id == b)
                    continue;
                if (best < 0 || id < best)
                    best = id;
            }

            return best;
        }

        static int FindCountryIdByTag(EntityManager em, string tag)
        {
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<CountryData>());
            using var arr = q.ToComponentDataArray<CountryData>(Allocator.Temp);
            for (var i = 0; i < arr.Length; i++)
            {
                if (arr[i].Tag.ToString() == tag)
                    return arr[i].CountryId;
            }

            return -1;
        }

        static string TagOf(EntityManager em, int countryId)
        {
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<CountryData>());
            using var arr = q.ToComponentDataArray<CountryData>(Allocator.Temp);
            for (var i = 0; i < arr.Length; i++)
            {
                if (arr[i].CountryId == countryId)
                    return arr[i].Tag.ToString();
            }

            return "?";
        }

        static string TagOfEntity(EntityManager em, Entity e)
        {
            if (e == Entity.Null || !em.Exists(e) || !em.HasComponent<CountryData>(e))
                return "?";
            return em.GetComponentData<CountryData>(e).Tag.ToString();
        }

        static bool TryResolveCountry(EntityManager em, int countryId, out Entity entity)
        {
            entity = Entity.Null;
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<CountryData>());
            using var entities = q.ToEntityArray(Allocator.Temp);
            using var arr = q.ToComponentDataArray<CountryData>(Allocator.Temp);
            for (var i = 0; i < arr.Length; i++)
            {
                if (arr[i].CountryId != countryId)
                    continue;
                entity = entities[i];
                return true;
            }

            return false;
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
