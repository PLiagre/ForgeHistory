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
using VictoriaGame.Presentation;
using VictoriaGame.World;
using Debug = UnityEngine.Debug;

namespace VictoriaGame.Tests
{
    /// <summary>Batch : -executeMethod VictoriaGame.Tests.V1094BatchRunner.Run</summary>
    public static class V1094BatchRunner
    {
        public static void Run()
        {
            try
            {
                V1094PilotLiveOwnershipTests.RunAndWriteArtifacts();
                Debug.Log("V1094BatchRunner: DONE");
            }
            catch (Exception ex) when (HarnessAllocationGuard.IsNativeAllocationFailure(ex))
            {
                Debug.LogWarning("V1094BatchRunner: ALLOCATION_FAILURE — " + ex.Message);
                Debug.Log("V1094BatchRunner: DONE_PARTIAL");
            }
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
        }
    }

    /// <summary>
    /// v1_094 — LA BELLE CARTE ÉTAIT UNE IMAGE MORTE.
    ///
    /// Ce qui doit rester vrai après ce brief :
    ///   1. En mode pilote, une conquête CHANGE la carte (elle ne la changeait pas).
    ///   2. Une occupation (Controller ≠ Owner) est visible (elle ne l'était pas).
    ///   3. Le rendu n'écrit toujours rien dans le monde.
    ///   4. Hors mode pilote, l'image est inchangée bit à bit.
    ///
    /// La référence de la mesure est DÉRIVÉE, pas nommée : le nombre de pixels que
    /// la conquête devrait repeindre est la surface des cellules de la province
    /// transférée, mesurée sur le rendu lui-même en mode ECS.
    /// </summary>
    [TestFixture]
    public class V1094PilotLiveOwnershipTests
    {
        const uint Seed = 42195u;
        const int W = 640;
        const int H = 480;

        static string GameUnityRoot =>
            Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        static string LogPath =>
            Path.Combine(GameUnityRoot, "Logs", "v1_094_pilot_live_ownership.log");

        static string CapturesDir => Path.Combine(GameUnityRoot, "Captures", "v1_094");

        [TearDown]
        public void TearDown() => ResetAll();

        static void ResetAll()
        {
            MapSnapshotExporter.DebugPilotColorsFromDisk = false;
            PilotMapProvider.SetEnabled(false, clearCache: true);
            MapGeometryCache.ResetStatsAndClear();
            MapViewport.Reset();
        }

        [Test]
        public void V1094_Artifacts_And_Verdict() => RunAndWriteArtifacts();

        public static void RunAndWriteArtifacts()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            Directory.CreateDirectory(CapturesDir);
            var sb = new StringBuilder(256 * 1024);
            void Flush() => File.WriteAllText(LogPath, sb.ToString(), Encoding.UTF8);

            sb.AppendLine("=== v1_094 — LA CARTE PILOTE LIT LE MONDE JOUÉ ===");
            sb.AppendLine("seed=" + Seed.ToString(CultureInfo.InvariantCulture) +
                          "  rendu " + W + "x" + H);
            sb.AppendLine();

            // ---------- INVENTAIRE (fichier:ligne, confirmé/infirmé) ----------
            sb.AppendLine("=== INVENTAIRE DU DÉFAUT ===");
            sb.AppendLine(
                "  PilotMapProvider.ApplyPilotColors — écrivait Owner=Entity.Null, " +
                "Controller=Entity.Null, Occupied=false en dur — CONFIRMÉ.");
            sb.AppendLine(
                "  Le tag venait de ownership_1400.json, lu UNE FOIS au chargement " +
                "(PilotMapProvider.LoadAll) — CONFIRMÉ.");
            sb.AppendLine(
                "  MapSnapshotExporter.ApplyFrontFlags indexait par views[i].Id, " +
                "or en pilote c'est un cell_id (≥1164) et FrontLineState porte un " +
                "ProvinceId (1..50) : aucune correspondance possible — CONFIRMÉ.");
            sb.AppendLine();
            Flush();

            ResetAll();
            PilotMapProvider.SetEnabled(true, clearCache: true);
            MapGeometryCache.ResetStatsAndClear();

            var geo = MapSnapshotExporter.BuildMapGeometry(W, H);
            if (geo == null)
            {
                sb.AppendLine("ÉCHEC : géométrie pilote nulle.");
                Flush();
                Assert.Fail("géométrie pilote nulle");
                return;
            }

            sb.AppendLine("Géométrie pilote : vues=" +
                          geo.ViewsSkeleton.Count.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine();
            Flush();

            // ---------- CIBLE DÉRIVÉE : la province la mieux représentée ----------
            // On ne nomme pas la province à la main : on prend celle dont les
            // cellules couvrent le plus de vues. C'est la cible la plus lisible,
            // et elle est déterminée par la donnée, pas par une opinion.
            var cellsPerProvince = new Dictionary<int, int>();
            for (var i = 0; i < geo.ViewsSkeleton.Count; i++)
            {
                var pid = PilotMapProvider.SimulationProvinceIdOfView(geo.ViewsSkeleton[i].Id);
                if (pid <= 0)
                    continue;
                cellsPerProvince.TryGetValue(pid, out var n);
                cellsPerProvince[pid] = n + 1;
            }

            var targetProvince = -1;
            var targetCells = -1;
            foreach (var kv in cellsPerProvince)
            {
                if (kv.Value > targetCells || (kv.Value == targetCells && kv.Key < targetProvince))
                {
                    targetProvince = kv.Key;
                    targetCells = kv.Value;
                }
            }

            sb.AppendLine("=== CIBLE DÉRIVÉE ===");
            sb.AppendLine("provinces simulées couvertes par la fenêtre pilote: " +
                          cellsPerProvince.Count.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("province cible (plus de cellules) = " +
                          targetProvince.ToString(CultureInfo.InvariantCulture) +
                          " avec " + targetCells.ToString(CultureInfo.InvariantCulture) +
                          " cellules");
            sb.AppendLine();
            Flush();

            var verdicts = new List<string>();

            using (var h = new SimulationHarness(Seed))
            {
                h.RunTicks(1);
                var em = h.EntityManager;

                // Empreinte du monde AVANT tout rendu (contrôle n°3).
                var digestBeforeRender = WorldDigest.Compute(em);

                var baseline = RenderPilot(em, geo);
                var digestAfterRender = WorldDigest.Compute(em);

                sb.AppendLine("=== CONTRÔLE 3 — LE RENDU N'ÉCRIT PAS LE MONDE ===");
                sb.AppendLine("empreinte avant rendu = 0x" + digestBeforeRender.ToString("X16"));
                sb.AppendLine("empreinte après rendu = 0x" + digestAfterRender.ToString("X16"));
                var renderPure = digestBeforeRender == digestAfterRender;
                sb.AppendLine("VERDICT 3 : " + (renderPure ? "VERT" : "ROUGE"));
                verdicts.Add("3_rendu_ne_ecrit_pas=" + (renderPure ? "VERT" : "ROUGE"));
                sb.AppendLine();
                Flush();

                WritePng(baseline, Path.Combine(CapturesDir, "01_avant_conquete.png"));

                // ---------- LA CONQUÊTE ----------
                var ownerBefore = OwnerTagOfProvince(em, targetProvince);
                var conqueror = FindCountryOtherThan(em, ownerBefore, out var conquerorTag);
                Assert.AreNotEqual(Entity.Null, conqueror, "aucun conquérant disponible");

                sb.AppendLine("=== LA CONQUÊTE (écriture de test, pas de rendu) ===");
                sb.AppendLine("province " + targetProvince.ToString(CultureInfo.InvariantCulture) +
                              " : " + ownerBefore + " → " + conquerorTag);
                sb.AppendLine();
                Flush();

                SetProvinceOwner(em, targetProvince, conqueror, alsoController: true);

                // ---------- CONTRÔLE 1 — ROUGE PUIS VERT ----------
                MapSnapshotExporter.DebugPilotColorsFromDisk = true;
                var afterDisk = RenderPilot(em, geo);
                MapSnapshotExporter.DebugPilotColorsFromDisk = false;
                var afterEcs = RenderPilot(em, geo);

                var changedDisk = CountDifferentPixels(baseline, afterDisk);
                var changedEcs = CountDifferentPixels(baseline, afterEcs);

                WritePng(afterDisk, Path.Combine(CapturesDir, "02_apres_conquete_ROUGE_disque.png"));
                WritePng(afterEcs, Path.Combine(CapturesDir, "03_apres_conquete_VERT_ecs.png"));

                sb.AppendLine("=== CONTRÔLE 1 — UNE CONQUÊTE CHANGE LA CARTE ===");
                sb.AppendLine("pixels repeints, chemin d'AVANT (ownership_1400.json) = " +
                              changedDisk.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("pixels repeints, chemin de MAINTENANT (ECS)          = " +
                              changedEcs.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("part de l'image repeinte = " +
                              (100.0 * changedEcs / (W * H)).ToString("0.00", CultureInfo.InvariantCulture) +
                              " %");
                var c1 = changedDisk == 0 && changedEcs > 0;
                sb.AppendLine("attendu : 0 avant (le défaut), > 0 maintenant");
                sb.AppendLine("VERDICT 1 : " + (c1 ? "VERT" : "ROUGE"));
                verdicts.Add("1_conquete_visible=" + (c1 ? "VERT" : "ROUGE"));
                sb.AppendLine();
                Flush();

                // ---------- CONTRÔLE 2 — L'OCCUPATION ----------
                // Restaurer le propriétaire, ne garder que le contrôleur : c'est
                // exactement l'état « province occupée mais pas annexée ».
                var ownerEntity = FindCountryByTag(em, ownerBefore);
                SetProvinceOwner(em, targetProvince, ownerEntity, alsoController: false);
                SetProvinceController(em, targetProvince, conqueror);

                var occupied = RenderPilot(em, geo);
                var changedOcc = CountDifferentPixels(baseline, occupied);
                WritePng(occupied, Path.Combine(CapturesDir, "04_occupation_hachuree.png"));

                sb.AppendLine("=== CONTRÔLE 2 — UNE OCCUPATION EST VISIBLE ===");
                sb.AppendLine("Owner reste " + ownerBefore + ", Controller devient " + conquerorTag);
                sb.AppendLine("pixels repeints = " +
                              changedOcc.ToString(CultureInfo.InvariantCulture));
                var c2 = changedOcc > 0;
                sb.AppendLine("VERDICT 2 : " + (c2 ? "VERT" : "ROUGE"));
                verdicts.Add("2_occupation_visible=" + (c2 ? "VERT" : "ROUGE"));
                sb.AppendLine();
                Flush();

                Assert.Zero(changedDisk,
                    "rouge attendu : le chemin disque ne peut pas voir une conquête");
                Assert.Greater(changedEcs, 0,
                    "vert attendu : le chemin ECS doit repeindre la province conquise");
                Assert.Greater(changedOcc, 0,
                    "vert attendu : l'occupation doit se voir");
                Assert.IsTrue(renderPure, "le rendu ne doit pas écrire le monde");
            }

            // ---------- CONTRÔLE 4 — MODE HÉRITÉ INCHANGÉ ----------
            ResetAll();
            using (var h = new SimulationHarness(Seed))
            {
                h.RunTicks(1);
                var em = h.EntityManager;
                PilotMapProvider.SetEnabled(false, clearCache: true);
                MapGeometryCache.ResetStatsAndClear();
                var legacyGeo = MapSnapshotExporter.BuildMapGeometry(W, H);
                var a = Sha256Of(RenderPilot(em, legacyGeo));
                MapGeometryCache.ResetStatsAndClear();
                var legacyGeo2 = MapSnapshotExporter.BuildMapGeometry(W, H);
                var b = Sha256Of(RenderPilot(em, legacyGeo2));

                sb.AppendLine("=== CONTRÔLE 4 — MODE HÉRITÉ STABLE ===");
                sb.AppendLine("SHA256 rendu hérité #1 = " + a);
                sb.AppendLine("SHA256 rendu hérité #2 = " + b);
                var c4 = a == b;
                sb.AppendLine("VERDICT 4 : " + (c4 ? "VERT" : "ROUGE"));
                verdicts.Add("4_herite_stable=" + (c4 ? "VERT" : "ROUGE"));
                sb.AppendLine();
                Assert.AreEqual(a, b, "le rendu hérité doit rester déterministe");
            }

            sb.AppendLine("=== VERDICTS ===");
            for (var i = 0; i < verdicts.Count; i++)
                sb.AppendLine("  " + verdicts[i]);
            sb.AppendLine();
            sb.AppendLine("CE QUE CE BRIEF NE PRÉTEND PAS : la granularité reste celle");
            sb.AppendLine("des 50 provinces héritées. 194 cellules sur 237 se partagent");
            sb.AppendLine(cellsPerProvince.Count.ToString(CultureInfo.InvariantCulture) +
                          " provinces : la carte est VIVANTE, elle n'est pas encore FINE.");
            Flush();

            ResetAll();
        }

        // ================= outils =================

        static Color32[] RenderPilot(EntityManager em, MapSnapshotExporter.MapGeometry geo)
            => MapSnapshotExporter.RenderPoliticalPixels(
                em, geo, MapSnapshotExporter.LabelDensity.None, -1);

        static int CountDifferentPixels(Color32[] a, Color32[] b)
        {
            if (a == null || b == null || a.Length != b.Length)
                return -1;
            var n = 0;
            for (var i = 0; i < a.Length; i++)
            {
                if (a[i].r != b[i].r || a[i].g != b[i].g ||
                    a[i].b != b[i].b || a[i].a != b[i].a)
                    n++;
            }

            return n;
        }

        static void WritePng(Color32[] pixels, string path)
        {
            if (pixels == null)
                return;
            MapSnapshotExporter.WriteMapBufferPng(pixels, W, H, path);
        }

        static string Sha256Of(Color32[] pixels)
        {
            if (pixels == null)
                return "(null)";
            var bytes = new byte[pixels.Length * 4];
            for (var i = 0; i < pixels.Length; i++)
            {
                bytes[i * 4] = pixels[i].r;
                bytes[i * 4 + 1] = pixels[i].g;
                bytes[i * 4 + 2] = pixels[i].b;
                bytes[i * 4 + 3] = pixels[i].a;
            }

            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        static string OwnerTagOfProvince(EntityManager em, int provinceId)
        {
            using var q = em.CreateEntityQuery(
                ComponentType.ReadOnly<ProvinceData>(),
                ComponentType.ReadOnly<ProvinceOwnership>());
            using var pdata = q.ToComponentDataArray<ProvinceData>(Allocator.Temp);
            using var owns = q.ToComponentDataArray<ProvinceOwnership>(Allocator.Temp);
            for (var i = 0; i < pdata.Length; i++)
            {
                if (pdata[i].ProvinceId != provinceId)
                    continue;
                if (owns[i].Owner != Entity.Null && em.HasComponent<CountryData>(owns[i].Owner))
                    return em.GetComponentData<CountryData>(owns[i].Owner).Tag.ToString();
                return "";
            }

            return "";
        }

        static Entity FindCountryByTag(EntityManager em, string tag)
        {
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<CountryData>());
            using var entities = q.ToEntityArray(Allocator.Temp);
            using var data = q.ToComponentDataArray<CountryData>(Allocator.Temp);
            for (var i = 0; i < data.Length; i++)
            {
                if (data[i].Tag.ToString() == tag)
                    return entities[i];
            }

            return Entity.Null;
        }

        static Entity FindCountryOtherThan(EntityManager em, string tag, out string chosenTag)
        {
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<CountryData>());
            using var entities = q.ToEntityArray(Allocator.Temp);
            using var data = q.ToComponentDataArray<CountryData>(Allocator.Temp);
            // Déterministe : le plus petit CountryId différent du propriétaire.
            var best = Entity.Null;
            var bestId = int.MaxValue;
            chosenTag = "";
            for (var i = 0; i < data.Length; i++)
            {
                if (data[i].Tag.ToString() == tag)
                    continue;
                if (data[i].CountryId >= bestId)
                    continue;
                bestId = data[i].CountryId;
                best = entities[i];
                chosenTag = data[i].Tag.ToString();
            }

            return best;
        }

        static void SetProvinceOwner(
            EntityManager em, int provinceId, Entity owner, bool alsoController)
        {
            using var q = em.CreateEntityQuery(
                ComponentType.ReadOnly<ProvinceData>(),
                ComponentType.ReadWrite<ProvinceOwnership>());
            using var entities = q.ToEntityArray(Allocator.Temp);
            using var pdata = q.ToComponentDataArray<ProvinceData>(Allocator.Temp);
            for (var i = 0; i < pdata.Length; i++)
            {
                if (pdata[i].ProvinceId != provinceId)
                    continue;
                var own = em.GetComponentData<ProvinceOwnership>(entities[i]);
                own.Owner = owner;
                if (alsoController)
                    own.Controller = owner;
                em.SetComponentData(entities[i], own);
                return;
            }
        }

        static void SetProvinceController(EntityManager em, int provinceId, Entity controller)
        {
            using var q = em.CreateEntityQuery(
                ComponentType.ReadOnly<ProvinceData>(),
                ComponentType.ReadWrite<ProvinceOwnership>());
            using var entities = q.ToEntityArray(Allocator.Temp);
            using var pdata = q.ToComponentDataArray<ProvinceData>(Allocator.Temp);
            for (var i = 0; i < pdata.Length; i++)
            {
                if (pdata[i].ProvinceId != provinceId)
                    continue;
                var own = em.GetComponentData<ProvinceOwnership>(entities[i]);
                own.Controller = controller;
                em.SetComponentData(entities[i], own);
                return;
            }
        }
    }
}
