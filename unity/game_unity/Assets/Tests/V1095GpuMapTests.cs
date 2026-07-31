using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;
using VictoriaGame.Presentation;
using Debug = UnityEngine.Debug;

namespace VictoriaGame.Tests
{
    /// <summary>Batch : -executeMethod VictoriaGame.Tests.V1095BatchRunner.Run (SANS -nographics)</summary>
    public static class V1095BatchRunner
    {
        public static void Run()
        {
            try
            {
                V1095GpuMapTests.RunAndWriteArtifacts();
                Debug.Log("V1095BatchRunner: DONE");
            }
            catch (Exception ex) when (HarnessAllocationGuard.IsNativeAllocationFailure(ex))
            {
                Debug.LogWarning("V1095BatchRunner: ALLOCATION_FAILURE — " + ex.Message);
                Debug.Log("V1095BatchRunner: DONE_PARTIAL");
            }
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
        }
    }

    /// <summary>
    /// v1_095 — LE FOND DE CARTE PASSE SUR LE GPU.
    ///
    /// Ce qui doit rester vrai :
    ///   1. Le shader se charge et rend une image non vide.
    ///   2. Une conquête change l'image GPU (la palette est bien la seule entrée).
    ///   3. Déplacer la fenêtre coûte MOINS que la reconstruction CPU équivalente.
    ///      Référence DÉRIVÉE : le coût CPU du même déplacement, mesuré ici même.
    ///   4. Le rendu GPU n'écrit pas dans le monde.
    /// </summary>
    [TestFixture]
    public class V1095GpuMapTests
    {
        const uint Seed = 42195u;
        const int W = 960;
        const int H = 720;
        const int PanSteps = 24;

        static string GameUnityRoot =>
            Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        static string LogPath => Path.Combine(GameUnityRoot, "Logs", "v1_095_gpu_map.log");
        static string CapturesDir => Path.Combine(GameUnityRoot, "Captures", "v1_095");

        [TearDown]
        public void TearDown()
        {
            MapGpuRenderer.Release();
            PilotMapProvider.SetEnabled(false, clearCache: true);
            MapGeometryCache.ResetStatsAndClear();
            MapViewport.Reset();
        }

        [Test]
        public void V1095_Artifacts_And_Verdict() => RunAndWriteArtifacts();

        public static void RunAndWriteArtifacts()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            Directory.CreateDirectory(CapturesDir);
            var sb = new StringBuilder(128 * 1024);
            void Flush() => File.WriteAllText(LogPath, sb.ToString(), Encoding.UTF8);

            sb.AppendLine("=== v1_095 — FOND DE CARTE SUR GPU ===");
            sb.AppendLine("rendu " + W + "x" + H + "  seed=" +
                          Seed.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("SystemInfo.graphicsDeviceType = " +
                          SystemInfo.graphicsDeviceType);
            sb.AppendLine("supportsComputeShaders=" + SystemInfo.supportsComputeShaders);
            sb.AppendLine();
            Flush();

            PilotMapProvider.SetEnabled(true, clearCache: true);
            MapGeometryCache.ResetStatsAndClear();

            // ---------- CONTRÔLE 1 — le shader existe et rend ----------
            sb.AppendLine("=== CONTRÔLE 1 — LE SHADER SE CHARGE ET REND ===");
            var available = MapGpuRenderer.IsAvailable;
            sb.AppendLine("MapGpuRenderer.IsAvailable = " + available);
            if (!available)
                sb.AppendLine("raison = " + MapGpuRenderer.LastUnavailableReason);
            Flush();
            Assert.IsTrue(available,
                "shader indisponible : " + MapGpuRenderer.LastUnavailableReason);

            var geo = MapSnapshotExporter.BuildMapGeometry(W, H);
            Assert.IsNotNull(geo, "géométrie pilote nulle");
            sb.AppendLine("vues pilote = " + geo.ViewsSkeleton.Count);
            Flush();

            var colors = CountryColors.Load();
            var sea = new Color32(
                colors.Sea.r, colors.Sea.g, colors.Sea.b, colors.Sea.a);

            var window = MapViewport.WorldWindow;
            if (window.Width <= 0f)
            {
                MapViewport.EnsureWorldWindow(geo);
                window = MapViewport.WorldWindow;
            }

            using (var h = new SimulationHarness(Seed))
            {
                h.RunTicks(1);
                var em = h.EntityManager;

                var digestBefore = WorldDigest.Compute(em);

                var views = MapSnapshotExporter.BuildViewsForRender(
                    em, geo.ViewsSkeleton, colors);
                Assert.IsTrue(
                    MapGpuRenderer.BuildPalette(views, out var palErr),
                    "palette : " + palErr);
                sb.AppendLine("palette construite : largeur = " +
                              MapGpuRenderer.PaletteWidth);
                Flush();

                var rt = MapGpuRenderer.Render(
                    W, H, window.MinX, window.MaxX, window.MinY, window.MaxY,
                    lod: 1, sea, hoverCellId: -1, selectedCellId: -1);
                Assert.IsNotNull(rt, "rendu GPU nul : " + MapGpuRenderer.LastUnavailableReason);

                var gpuPixels = MapGpuRenderer.ReadbackLastFrame(W, H);
                Assert.IsNotNull(gpuPixels, "relecture GPU nulle");

                var distinct = CountDistinctColors(gpuPixels);
                var seaShare = ShareEqualTo(gpuPixels, sea);
                sb.AppendLine("couleurs distinctes dans l'image GPU = " + distinct);
                sb.AppendLine("part de mer = " +
                              (100.0 * seaShare).ToString("0.0", CultureInfo.InvariantCulture) + " %");
                var c1 = distinct > 8 && seaShare < 0.98;
                sb.AppendLine("attendu : plus de 8 couleurs, et pas une image de mer");
                sb.AppendLine("VERDICT 1 : " + (c1 ? "VERT" : "ROUGE"));
                sb.AppendLine();
                Flush();

                MapSnapshotExporter.WriteMapBufferPng(
                    gpuPixels, W, H, Path.Combine(CapturesDir, "01_gpu_monde.png"));

                var digestAfter = WorldDigest.Compute(em);
                var pure = digestBefore == digestAfter;
                sb.AppendLine("=== CONTRÔLE 4 — LE RENDU GPU N'ÉCRIT PAS LE MONDE ===");
                sb.AppendLine("empreinte avant = 0x" + digestBefore.ToString("X16"));
                sb.AppendLine("empreinte après = 0x" + digestAfter.ToString("X16"));
                sb.AppendLine("VERDICT 4 : " + (pure ? "VERT" : "ROUGE"));
                sb.AppendLine();
                Flush();

                // ---------- CONTRÔLE 3 — COÛT DU DÉPLACEMENT ----------
                // Référence dérivée : le MÊME déplacement, payé par le chemin CPU.
                sb.AppendLine("=== CONTRÔLE 3 — COÛT D'UN DÉPLACEMENT ===");
                sb.AppendLine("protocole : " + PanSteps +
                              " fenêtres successives, translation de 2 % de la largeur.");

                var windows = new MapWindow[PanSteps];
                var stepX = window.Width * 0.02f;
                for (var i = 0; i < PanSteps; i++)
                {
                    windows[i] = new MapWindow
                    {
                        MinX = window.MinX + stepX * i,
                        MaxX = window.MaxX + stepX * i,
                        MinY = window.MinY,
                        MaxY = window.MaxY
                    };
                }

                // GPU : palette inchangée (le monde n'a pas bougé), seule la
                // fenêtre change. C'est exactement le cas d'un déplacement souris.
                var swGpu = Stopwatch.StartNew();
                for (var i = 0; i < PanSteps; i++)
                {
                    MapGpuRenderer.Render(
                        W, H, windows[i].MinX, windows[i].MaxX,
                        windows[i].MinY, windows[i].MaxY,
                        lod: 1, sea, -1, -1);
                }

                GL.Flush();
                swGpu.Stop();
                var gpuMs = swGpu.Elapsed.TotalMilliseconds;

                // CPU : chemin actuel, cache de géométrie compris — c'est le coût
                // réellement payé aujourd'hui quand on déplace la carte.
                var swCpu = Stopwatch.StartNew();
                for (var i = 0; i < PanSteps; i++)
                {
                    var g = MapGeometryCache.GetOrBuild(W, H, windows[i], out _);
                    if (g == null)
                        continue;
                    MapSnapshotExporter.RenderPoliticalPixels(
                        em, g, MapSnapshotExporter.LabelDensity.None, -1);
                }

                swCpu.Stop();
                var cpuMs = swCpu.Elapsed.TotalMilliseconds;

                sb.AppendLine("GPU : " + gpuMs.ToString("0.00", CultureInfo.InvariantCulture) +
                              " ms au total, soit " +
                              (gpuMs / PanSteps).ToString("0.000", CultureInfo.InvariantCulture) +
                              " ms par image");
                sb.AppendLine("CPU : " + cpuMs.ToString("0.00", CultureInfo.InvariantCulture) +
                              " ms au total, soit " +
                              (cpuMs / PanSteps).ToString("0.000", CultureInfo.InvariantCulture) +
                              " ms par image");
                var ratio = gpuMs > 0.0001 ? cpuMs / gpuMs : double.PositiveInfinity;
                sb.AppendLine("rapport CPU/GPU = " +
                              ratio.ToString("0.0", CultureInfo.InvariantCulture) + "x");
                sb.AppendLine("budget 60 images/s = 16,7 ms par image.");
                sb.AppendLine("  GPU tient le budget : " + ((gpuMs / PanSteps) < 16.7));
                sb.AppendLine("  CPU tient le budget : " + ((cpuMs / PanSteps) < 16.7));
                var c3 = gpuMs < cpuMs;
                sb.AppendLine("VERDICT 3 : " + (c3 ? "VERT" : "ROUGE"));
                sb.AppendLine();
                Flush();

                // ---------- CONTRÔLE 2 — une conquête change l'image GPU ----------
                MapGpuRenderer.Render(
                    W, H, window.MinX, window.MaxX, window.MinY, window.MaxY,
                    lod: 1, sea, -1, -1);
                var beforeConquest = MapGpuRenderer.ReadbackLastFrame(W, H);

                var changedViews = TransferLargestProvince(em, geo, colors, sb);
                Assert.IsTrue(
                    MapGpuRenderer.BuildPalette(changedViews, out var palErr2),
                    "palette après conquête : " + palErr2);
                MapGpuRenderer.Render(
                    W, H, window.MinX, window.MaxX, window.MinY, window.MaxY,
                    lod: 1, sea, -1, -1);
                var afterConquest = MapGpuRenderer.ReadbackLastFrame(W, H);

                var changed = CountDifferent(beforeConquest, afterConquest);
                MapSnapshotExporter.WriteMapBufferPng(
                    afterConquest, W, H,
                    Path.Combine(CapturesDir, "02_gpu_apres_conquete.png"));

                sb.AppendLine("=== CONTRÔLE 2 — LA PALETTE EST LA SEULE ENTRÉE ===");
                sb.AppendLine("pixels repeints après conquête = " +
                              changed.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("part de l'image = " +
                              (100.0 * changed / (W * H)).ToString("0.00", CultureInfo.InvariantCulture) +
                              " %");
                var c2 = changed > 0;
                sb.AppendLine("VERDICT 2 : " + (c2 ? "VERT" : "ROUGE"));
                sb.AppendLine();
                Flush();

                // ---------- CONTRÔLE 5 — GPU ET CPU DÉCRIVENT LA MÊME TERRE ----------
                // Ce contrôle existe parce qu'une inversion haut-bas ne se voit dans
                // AUCUNE des mesures précédentes : un écart de pixels est insensible
                // à l'orientation. On compare donc les silhouettes terre/mer des deux
                // chemins sur la MÊME fenêtre. Une image retournée effondre l'accord.
                MapGeometryCache.ResetStatsAndClear();
                var cpuGeo = MapGeometryCache.GetOrBuild(W, H, window, out _);
                var cpuPixels = MapSnapshotExporter.RenderPoliticalPixels(
                    em, cpuGeo, MapSnapshotExporter.LabelDensity.None, -1);
                MapGpuRenderer.Render(
                    W, H, window.MinX, window.MaxX, window.MinY, window.MaxY,
                    lod: 1, sea, -1, -1);
                var gpuSame = MapGpuRenderer.ReadbackLastFrame(W, H);

                var agree = SeaSilhouetteAgreement(cpuPixels, gpuSame, sea);
                var agreeFlipped = SeaSilhouetteAgreement(
                    cpuPixels,
                    MapSnapshotExporter.FlipMapBufferRows(gpuSame, W, H),
                    sea);

                sb.AppendLine("=== CONTRÔLE 5 — MÊME TERRE, MÊME SENS ===");
                sb.AppendLine("accord terre/mer CPU vs GPU        = " +
                              (100.0 * agree).ToString("0.0", CultureInfo.InvariantCulture) + " %");
                sb.AppendLine("accord si l'on retourne le GPU     = " +
                              (100.0 * agreeFlipped).ToString("0.0", CultureInfo.InvariantCulture) + " %");
                sb.AppendLine("le bon sens est celui qui accorde le PLUS.");
                var c5 = agree > agreeFlipped && agree > 0.90;
                sb.AppendLine("VERDICT 5 : " + (c5 ? "VERT" : "ROUGE"));
                sb.AppendLine();
                MapSnapshotExporter.WriteMapBufferPng(
                    cpuPixels, W, H, Path.Combine(CapturesDir, "03_cpu_meme_fenetre.png"));
                Flush();

                // ---------- CONTRÔLE 6 — LE CHEMIN DU JEU, PAS UNE MAQUETTE ----------
                // Les mesures ci-dessus appellent MapGpuRenderer directement. Elles ne
                // prouvent donc PAS que la boucle d'affichage s'en sert. Ce contrôle
                // appelle l'entrée réellement câblée dans OnUpdate — c'est le coût
                // que le joueur paie en déplaçant la carte.
                //
                // HISTOIRE DE CE CONTRÔLE, À GARDER : à sa première exécution il est
                // sorti ROUGE à 18,15 ms, alors que le Blit seul coûtait 0,03 ms.
                // La cause n'était pas le rendu mais la RELECTURE CPU (ReadPixels)
                // que le câblage faisait pour réutiliser PresentFrame. Le correctif
                // fut de présenter la RenderTexture sans copie. Aucune des mesures
                // 1 à 5 n'aurait pu le voir : elles appellent MapGpuRenderer
                // directement, donc elles ne payaient pas le câblage.
                MapViewport.EnsureWorldWindow(geo);
                var framesBefore = MapDisplaySystem.GpuBackgroundFrames;
                var swWired = Stopwatch.StartNew();
                var wiredFrames = 0;
                for (var i = 0; i < PanSteps; i++)
                {
                    MapViewport.Pan(stepX, 0f);
                    var frame = MapDisplaySystem.RenderGpuBackgroundForMeasure(em, geo);
                    if (frame != null)
                        wiredFrames++;
                }

                swWired.Stop();
                var wiredMs = swWired.Elapsed.TotalMilliseconds / PanSteps;

                sb.AppendLine("=== CONTRÔLE 6 — LE CHEMIN CÂBLÉ DANS LE JEU ===");
                sb.AppendLine("images produites par l'entrée du jeu = " +
                              wiredFrames + " / " + PanSteps);
                sb.AppendLine("compteur interne GpuBackgroundFrames : " +
                              framesBefore + " → " + MapDisplaySystem.GpuBackgroundFrames);
                sb.AppendLine("coût par image, sans relecture CPU = " +
                              wiredMs.ToString("0.000", CultureInfo.InvariantCulture) + " ms");
                sb.AppendLine("à comparer au CPU mesuré plus haut : " +
                              (cpuMs / PanSteps).ToString("0.00", CultureInfo.InvariantCulture) + " ms");
                sb.AppendLine("budget 60 images/s = 16,7 ms  → tenu : " + (wiredMs < 16.7));
                var c6 = wiredFrames == PanSteps && wiredMs < 16.7;
                sb.AppendLine("VERDICT 6 : " + (c6 ? "VERT" : "ROUGE"));
                sb.AppendLine();
                Flush();

                sb.AppendLine("=== VERDICTS ===");
                sb.AppendLine("  6_chemin_du_jeu=" + (c6 ? "VERT" : "ROUGE"));
                sb.AppendLine("  5_orientation_accordee=" + (c5 ? "VERT" : "ROUGE"));
                sb.AppendLine("  1_shader_rend=" + (c1 ? "VERT" : "ROUGE"));
                sb.AppendLine("  2_conquete_visible_gpu=" + (c2 ? "VERT" : "ROUGE"));
                sb.AppendLine("  3_deplacement_moins_cher=" + (c3 ? "VERT" : "ROUGE"));
                sb.AppendLine("  4_gpu_necrit_pas=" + (pure ? "VERT" : "ROUGE"));
                sb.AppendLine();
                sb.AppendLine("CE QUE CE BRIEF NE PRÉTEND PAS : étiquettes, villes et");
                sb.AppendLine("liserés de front restent dessinés par le CPU et ne sont");
                sb.AppendLine("PAS dans ces images. Le fond est GPU, la surcouche non.");
                Flush();

                Assert.IsTrue(c6, "l'entrée câblée dans le jeu doit produire des images sous budget");
                Assert.IsTrue(c5, "GPU et CPU doivent décrire la même terre, dans le même sens");
                Assert.IsTrue(c1, "l'image GPU doit être une carte, pas un aplat");
                Assert.IsTrue(c2, "une conquête doit changer l'image GPU");
                Assert.IsTrue(c3, "le déplacement GPU doit coûter moins que le CPU");
                Assert.IsTrue(pure, "le rendu GPU ne doit pas écrire le monde");
            }
        }

        // ================= outils =================

        static System.Collections.Generic.List<MapSnapshotExporter.ProvinceView>
            TransferLargestProvince(
                EntityManager em,
                MapSnapshotExporter.MapGeometry geo,
                CountryColors.Table colors,
                StringBuilder sb)
        {
            var counts = new System.Collections.Generic.Dictionary<int, int>();
            for (var i = 0; i < geo.ViewsSkeleton.Count; i++)
            {
                var pid = PilotMapProvider.SimulationProvinceIdOfView(geo.ViewsSkeleton[i].Id);
                if (pid <= 0)
                    continue;
                counts.TryGetValue(pid, out var n);
                counts[pid] = n + 1;
            }

            var target = -1;
            var best = -1;
            foreach (var kv in counts)
            {
                if (kv.Value > best || (kv.Value == best && kv.Key < target))
                {
                    target = kv.Key;
                    best = kv.Value;
                }
            }

            var conqueror = Entity.Null;
            var conquerorTag = "";
            var currentTag = "";
            using (var q = em.CreateEntityQuery(
                       ComponentType.ReadOnly<VictoriaGame.Core.CountryData>()))
            using (var entities = q.ToEntityArray(Unity.Collections.Allocator.Temp))
            using (var data = q.ToComponentDataArray<VictoriaGame.Core.CountryData>(
                       Unity.Collections.Allocator.Temp))
            {
                currentTag = OwnerTagOf(em, target);
                var bestId = int.MaxValue;
                for (var i = 0; i < data.Length; i++)
                {
                    if (data[i].Tag.ToString() == currentTag)
                        continue;
                    if (data[i].CountryId >= bestId)
                        continue;
                    bestId = data[i].CountryId;
                    conqueror = entities[i];
                    conquerorTag = data[i].Tag.ToString();
                }
            }

            sb.AppendLine("conquête de mesure : province " + target +
                          " (" + best + " cellules) " + currentTag + " → " + conquerorTag);

            using (var q = em.CreateEntityQuery(
                       ComponentType.ReadOnly<VictoriaGame.World.ProvinceData>(),
                       ComponentType.ReadWrite<VictoriaGame.World.ProvinceOwnership>()))
            using (var entities = q.ToEntityArray(Unity.Collections.Allocator.Temp))
            using (var pdata = q.ToComponentDataArray<VictoriaGame.World.ProvinceData>(
                       Unity.Collections.Allocator.Temp))
            {
                for (var i = 0; i < pdata.Length; i++)
                {
                    if (pdata[i].ProvinceId != target)
                        continue;
                    var own = em.GetComponentData<VictoriaGame.World.ProvinceOwnership>(entities[i]);
                    own.Owner = conqueror;
                    own.Controller = conqueror;
                    em.SetComponentData(entities[i], own);
                    break;
                }
            }

            return MapSnapshotExporter.BuildViewsForRender(em, geo.ViewsSkeleton, colors);
        }

        static string OwnerTagOf(EntityManager em, int provinceId)
        {
            using var q = em.CreateEntityQuery(
                ComponentType.ReadOnly<VictoriaGame.World.ProvinceData>(),
                ComponentType.ReadOnly<VictoriaGame.World.ProvinceOwnership>());
            using var pdata = q.ToComponentDataArray<VictoriaGame.World.ProvinceData>(
                Unity.Collections.Allocator.Temp);
            using var owns = q.ToComponentDataArray<VictoriaGame.World.ProvinceOwnership>(
                Unity.Collections.Allocator.Temp);
            for (var i = 0; i < pdata.Length; i++)
            {
                if (pdata[i].ProvinceId != provinceId)
                    continue;
                if (owns[i].Owner != Entity.Null &&
                    em.HasComponent<VictoriaGame.Core.CountryData>(owns[i].Owner))
                    return em.GetComponentData<VictoriaGame.Core.CountryData>(
                        owns[i].Owner).Tag.ToString();
                return "";
            }

            return "";
        }

        static int CountDifferent(Color32[] a, Color32[] b)
        {
            if (a == null || b == null || a.Length != b.Length)
                return -1;
            var n = 0;
            for (var i = 0; i < a.Length; i++)
            {
                if (a[i].r != b[i].r || a[i].g != b[i].g || a[i].b != b[i].b)
                    n++;
            }

            return n;
        }

        static int CountDistinctColors(Color32[] p)
        {
            var set = new System.Collections.Generic.HashSet<int>();
            for (var i = 0; i < p.Length; i++)
                set.Add((p[i].r << 16) | (p[i].g << 8) | p[i].b);
            return set.Count;
        }

        /// <summary>
        /// Part des pixels où les deux images s'accordent sur « mer ou terre ».
        /// Classifieur volontairement grossier : la mer est peinte d'une couleur
        /// unique dans les deux chemins, la terre ne l'est jamais.
        /// </summary>
        static double SeaSilhouetteAgreement(Color32[] a, Color32[] b, Color32 sea)
        {
            if (a == null || b == null || a.Length != b.Length)
                return 0.0;
            var ok = 0;
            for (var i = 0; i < a.Length; i++)
            {
                var seaA = a[i].r == sea.r && a[i].g == sea.g && a[i].b == sea.b;
                var seaB = b[i].r == sea.r && b[i].g == sea.g && b[i].b == sea.b;
                if (seaA == seaB)
                    ok++;
            }

            return (double)ok / a.Length;
        }

        static double ShareEqualTo(Color32[] p, Color32 c)
        {
            var n = 0;
            for (var i = 0; i < p.Length; i++)
            {
                if (p[i].r == c.r && p[i].g == c.g && p[i].b == c.b)
                    n++;
            }

            return (double)n / p.Length;
        }
    }
}
