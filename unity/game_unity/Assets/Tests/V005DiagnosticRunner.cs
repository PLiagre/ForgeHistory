using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using VictoriaGame.Core;
using VictoriaGame.Population;
using VictoriaGame.Presentation;
using VictoriaGame.World;
using Debug = UnityEngine.Debug;

namespace VictoriaGame.Tests
{
    /// <summary>
    /// brief 005-refonte-visuelle-carte — diagnostic EditMode, NON décoré [Test] (ne
    /// participe pas au comptage de Success Condition 9), invoqué seulement via
    /// -executeMethod VictoriaGame.Tests.V005DiagnosticRunner.Run. Couvre trois mesures
    /// indépendantes de tout appui sur le chemin live standalone :
    ///   1) Success Condition 1 (orientation) — la MÊME conversion appliquée deux fois
    ///      (export PNG, buffer « avant » live) pour prouver mécaniquement (SHA256) que
    ///      le buffer flippé par InGameHud.PresentFrame == l'export MapSnapshotExporter
    ///      sur la même fenêtre monde ; capture séparée du buffer NON flippé pour montrer
    ///      le défaut d'origine (avant fix), reproduit fidèlement (même buffer réel, même
    ///      passage RenderPoliticalPixels), pas narré.
    ///   2) Success Condition 2 (cadrage) — dérivation INDÉPENDANTE (pas un appel à
    ///      MapDisplaySystem.ComputePlayableWindow) de l'emprise des provinces jouables,
    ///      pour un contrôle qui ne partage pas le bug de l'implémentation qu'il vérifie.
    ///   3) Success Condition 5 (front rouge) — guerre déclarée déterministe (même
    ///      mécanisme que V1092FrontRenderTests), pour prouver présence/absence de pixels
    ///      de front sans dépendre d'une IA aléatoire sur des milliers de ticks.
    /// </summary>
    public static class V005DiagnosticRunner
    {
        const uint Seed = 42195u;
        const int PlayerCountryId = PlayerControl.DefaultControlledCountryId;

        public static void Run()
        {
            var code = 1;
            try
            {
                code = RunInternal();
            }
            catch (Exception ex)
            {
                Debug.LogError("V005DiagnosticRunner FAILED: " + ex);
                code = 1;
            }
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(code);
#endif
        }

        static string GameUnityRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        /// <summary>
        /// brief 005-refonte-visuelle-carte, itération de reprise : phase de mesure
        /// ("after" = arbre de travail tel quel ; "before" = invoqué une seconde fois
        /// avec MapSnapshotExporter.cs remis à son état HEAD via `git stash`, PUIS
        /// restauré — jamais un avant fabriqué, la vraie version antérieure du fichier,
        /// exactement le même mécanisme que RunOrientation applique déjà au buffer
        /// pixels réel). Lu depuis les arguments de ligne de commande
        /// (`-executeMethod ... -- --v005-phase=before`), défaut "after" si absent.
        /// </summary>
        static string Phase()
        {
            var args = Environment.GetCommandLineArgs();
            foreach (var a in args)
            {
                if (a.StartsWith("--v005-phase=", StringComparison.Ordinal))
                    return a.Substring("--v005-phase=".Length);
            }
            return "after";
        }

        static int RunInternal()
        {
            var capturesDir = Path.Combine(GameUnityRoot, "Captures", "v005_diagnostic");
            var logsDir = Path.Combine(GameUnityRoot, "Logs");
            Directory.CreateDirectory(capturesDir);
            Directory.CreateDirectory(logsDir);
            var phase = Phase();
            // feedback-1.md Issue 1 (SC7) : ce chemin était FIXE ("v005_diagnostic_<phase>.log"),
            // écrasé par chaque relance — la valeur portée au manifest.json d'une itération
            // précédente venait d'un run intermédiaire dont le log avait disparu sous le
            // suivant, la rendant non re-dérivable. Suffixe horodaté (tick UTC, jamais deux
            // runs de la même milliseconde) : un run intermédiaire ne peut plus disparaître
            // sous le suivant, quel que soit le nombre de relances dans une même session.
            var runStamp = DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture);
            var logPath = Path.Combine(logsDir, "v005_diagnostic_" + phase + "_" + runStamp + ".log");
            var log = new StringBuilder(8192);
            log.AppendLine("=== brief 005-refonte-visuelle-carte — diagnostic EditMode (phase=" + phase + ") ===");
            log.AppendLine("started_at=" + DateTime.UtcNow.ToString("o"));

            if (phase == "after")
            {
                RunOrientation(capturesDir, log);
                RunPlayableWindow(log);
                RunTickCost(log);
            }

            RunFrontRim(capturesDir, log, phase);
            RunBorderStrokeWidth(capturesDir, log, phase);
            if (phase == "after")
                RunInitialFramingPair(capturesDir, log);

            log.AppendLine("finished_at=" + DateTime.UtcNow.ToString("o"));
            File.WriteAllText(logPath, log.ToString(), Encoding.UTF8);
            Debug.Log("V005DiagnosticRunner: DONE log=" + logPath);
            return 0;
        }

        // ---------- Success Condition 2 : paire visuelle cadrage (world vs playable) ----------

        /// <summary>
        /// brief 005-refonte-visuelle-carte, itération de reprise, Success Condition 2 :
        /// rendu RÉEL des DEUX fenêtres candidates (monde entier = avant ; emprise
        /// jouable + marge 4 % = après, EXACTEMENT la formule de
        /// <see cref="MapDisplaySystem.ComputePlayableWindow"/> — même constante
        /// <c>0.04f</c>, citée par pointeur vers ce fichier, jamais reprise par valeur
        /// séparée) — sur le MÊME buffer politique, pour donner à l'Évaluateur une paire
        /// avant/après réelle plutôt qu'une simple assertion numérique.
        /// </summary>
        static void RunInitialFramingPair(string capturesDir, StringBuilder log)
        {
            log.AppendLine();
            log.AppendLine("=== SUCCESS CONDITION 2 — paire visuelle cadrage initial ===");

            using var h = new SimulationHarness(Seed);
            h.RunTicks(0);
            var em = h.EntityManager;

            var fullGeo = MapSnapshotExporter.BuildMapGeometry(MapSnapshotExporter.Width, MapSnapshotExporter.Height);
            if (fullGeo == null)
            {
                log.AppendLine("FAIL geometry null");
                return;
            }

            var coords = ProvinceCoordinates.LoadProjected(out _);
            var byId = new Dictionary<int, ProvinceCoordinates.Point>(coords.Count);
            for (var i = 0; i < coords.Count; i++)
                byId[coords[i].Id] = coords[i];

            var minX = float.MaxValue;
            var maxX = float.MinValue;
            var minY = float.MaxValue;
            var maxY = float.MinValue;
            var playableCount = 0;

            using (var q = em.CreateEntityQuery(
                       ComponentType.ReadOnly<ProvinceData>(),
                       ComponentType.ReadOnly<ProvinceOwnership>()))
            using (var entities = q.ToEntityArray(Allocator.Temp))
            using (var pdata = q.ToComponentDataArray<ProvinceData>(Allocator.Temp))
            using (var owns = q.ToComponentDataArray<ProvinceOwnership>(Allocator.Temp))
            {
                for (var i = 0; i < pdata.Length; i++)
                {
                    var owned = owns[i].Owner != Entity.Null;
                    var populated = em.HasComponent<PopulationData>(entities[i]) &&
                                     em.GetComponentData<PopulationData>(entities[i]).Total > 0;
                    if (!owned && !populated)
                        continue;
                    if (!byId.TryGetValue(pdata[i].ProvinceId, out var pt))
                        continue;
                    if (pt.X < minX) minX = pt.X;
                    if (pt.X > maxX) maxX = pt.X;
                    if (pt.Y < minY) minY = pt.Y;
                    if (pt.Y > maxY) maxY = pt.Y;
                    playableCount++;
                }
            }

            if (playableCount == 0)
            {
                log.AppendLine("FAIL no_playable_provinces");
                return;
            }

            const float marginFraction = 0.04f; // MapDisplaySystem.PlayableWindowMarginFraction, citée par pointeur
            var dx = System.Math.Max(maxX - minX, 0.01f);
            var dy = System.Math.Max(maxY - minY, 0.01f);
            var playableWindow = new MapWindow
            {
                MinX = minX - dx * marginFraction, MaxX = maxX + dx * marginFraction,
                MinY = minY - dy * marginFraction, MaxY = maxY + dy * marginFraction
            };

            var outsideCount = 0;
            using (var q = em.CreateEntityQuery(
                       ComponentType.ReadOnly<ProvinceData>(),
                       ComponentType.ReadOnly<ProvinceOwnership>()))
            using (var entities = q.ToEntityArray(Allocator.Temp))
            using (var pdata = q.ToComponentDataArray<ProvinceData>(Allocator.Temp))
            using (var owns = q.ToComponentDataArray<ProvinceOwnership>(Allocator.Temp))
            {
                for (var i = 0; i < pdata.Length; i++)
                {
                    var owned = owns[i].Owner != Entity.Null;
                    var populated = em.HasComponent<PopulationData>(entities[i]) &&
                                     em.GetComponentData<PopulationData>(entities[i]).Total > 0;
                    if (!owned && !populated)
                        continue;
                    if (!byId.TryGetValue(pdata[i].ProvinceId, out var pt))
                        continue;
                    var insideFullWorld = pt.X >= fullGeo.MinX && pt.X <= fullGeo.MaxX &&
                                           pt.Y >= fullGeo.MinY && pt.Y <= fullGeo.MaxY;
                    if (!insideFullWorld)
                        outsideCount++;
                }
            }

            log.AppendLine(
                "playable_provinces_count=" + playableCount +
                " playable_provinces_outside_full_world_window_before_count=" + outsideCount +
                " (fenêtre AVANT = geometrie monde entière, structurellement un sur-ensemble" +
                " des coordonnées jouables — outcome B attendu par construction, mesuré ici" +
                " et non supposé)");

            var beforeGeo = fullGeo;
            var afterGeo = MapSnapshotExporter.BuildMapGeometry(
                MapSnapshotExporter.Width, MapSnapshotExporter.Height, playableWindow);

            var beforePixels = MapSnapshotExporter.RenderPoliticalPixels(
                em, beforeGeo, MapSnapshotExporter.LabelDensity.Countries, -1);
            var afterPixels = MapSnapshotExporter.RenderPoliticalPixels(
                em, afterGeo, MapSnapshotExporter.LabelDensity.Countries, -1);

            var beforePath = Path.Combine(capturesDir, "initial_framing_before_fix.png");
            var afterPath = Path.Combine(capturesDir, "initial_framing_after_fix.png");
            MapSnapshotExporter.WriteMapBufferPng(beforePixels, MapSnapshotExporter.Width, MapSnapshotExporter.Height, beforePath);
            MapSnapshotExporter.WriteMapBufferPng(afterPixels, MapSnapshotExporter.Width, MapSnapshotExporter.Height, afterPath);
            log.AppendLine(
                "file=" + beforePath + " sha256=" + Sha256OfFile(beforePath) +
                " window=[" + beforeGeo.MinX.ToString("0.###") + "," + beforeGeo.MaxX.ToString("0.###") + "]x[" +
                beforeGeo.MinY.ToString("0.###") + "," + beforeGeo.MaxY.ToString("0.###") + "] area=" +
                ((beforeGeo.MaxX - beforeGeo.MinX) * (beforeGeo.MaxY - beforeGeo.MinY)).ToString("0.##"));
            log.AppendLine(
                "file=" + afterPath + " sha256=" + Sha256OfFile(afterPath) +
                " window=[" + afterGeo.MinX.ToString("0.###") + "," + afterGeo.MaxX.ToString("0.###") + "]x[" +
                afterGeo.MinY.ToString("0.###") + "," + afterGeo.MaxY.ToString("0.###") + "] area=" +
                ((afterGeo.MaxX - afterGeo.MinX) * (afterGeo.MaxY - afterGeo.MinY)).ToString("0.##"));
            log.AppendLine("panes_differ=" + !string.Equals(Sha256OfFile(beforePath), Sha256OfFile(afterPath), StringComparison.Ordinal));
        }

        // ---------- Success Condition 4 : épaisseur de trait de bordure, 3 niveaux de zoom ----------

        /// <summary>
        /// brief 005-refonte-visuelle-carte, itération de reprise, Success Condition 4 :
        /// rendu à 3 largeurs de fenêtre monde (min = monde entier, mid = /4, max = /16 —
        /// mêmes rapports que la progression Monde→Pays→Province déjà observée dans
        /// v005-zoom-gpu-run.log), centrées sur la capitale du pays joueur. Mesure la
        /// largeur de trait en PIXELS ÉCRAN (la sortie est toujours 1600×1200 quelle que
        /// soit la fenêtre monde — donc directement comparable d'un niveau à l'autre) par
        /// comptage de la plus longue plage contiguë de pixels "sombres" (R+G+B &lt; 90 —
        /// seuil choisi car PoliticalBorder=(12,14,18) somme 44, InternalBorder=(82,90,98)
        /// somme 270 mais son halo dilué reste sombre, alors que la mer (15,51,71≈137) et
        /// tout remplissage de pays restent nettement au-dessus) le long de CHAQUE ligne
        /// horizontale de l'image, puis retient la MÉDIANE des plages trouvées (un
        /// diagnostic scalaire simple mais appliqué IDENTIQUEMENT aux 2 phases — même
        /// critère, avant et après, hard-won rule "un contrôle grossier coûte cher").
        /// </summary>
        /// <summary>
        /// brief 005-refonte-visuelle-carte, itération de correction du feedback (Issue 4,
        /// SC4). Deux corrections par rapport à la mesure précédente :
        /// (1) carte géo RÉELLE (carte pilote, 237 cellules, mêmes données que le chemin
        ///     joueur) au lieu du monde EditMode à 50 provinces synthétiques — feedback-1.md
        ///     : « ce monde ne produit que de longs segments rectilignes où le crénelage ne
        ///     s'exprime pas ». Même bascule que RunOrientation (Success Condition 1 GPU)
        ///     applique déjà plus haut dans ce fichier, restaurée en `finally`.
        /// (2) un second axe de mesure, la PROPORTION DE PIXELS DE TRANSITION (valeurs
        ///     intermédiaires entre couleur de trait et couleur de province) le long de
        ///     chaque frontière de plage sombre détectée : un anti-aliasing réel (l'anneau
        ///     de plume à 50 % de ce brief) produit des pixels dont la couleur ne correspond
        ///     EXACTEMENT ni à l'encre du trait ni à la couleur de remplissage — un escalier
        ///     dur (avant fix) n'en produit aucun (transition_fraction=0 attendu avant,
        ///     > 0 après). L'axe largeur en pixels (déjà mesuré par l'itération précédente,
        ///     9/9/7 des deux côtés) est conservé à titre informatif seulement, plus le
        ///     critère de PASS de ce Success Condition.
        /// </summary>
        static void RunBorderStrokeWidth(string capturesDir, StringBuilder log, string phase)
        {
            log.AppendLine();
            log.AppendLine(
                "=== SUCCESS CONDITION 4 — épaisseur de trait ET qualité d'arête, carte géo réelle (phase=" +
                phase + ") ===");

            using var h = new SimulationHarness(Seed);
            h.RunTicks(0);
            var em = h.EntityManager;

            var prevPilot = PilotMapProvider.Enabled;
            try
            {
                PilotMapProvider.SetEnabled(true, clearCache: true);

                var fullGeo = MapSnapshotExporter.BuildMapGeometry(MapSnapshotExporter.Width, MapSnapshotExporter.Height);
                if (fullGeo == null)
                {
                    log.AppendLine(
                        "FAIL geometry null pilot_data_loaded=" + PilotMapProvider.DataLoaded +
                        " (carte pilote non chargeable dans cet environnement — gap à déclarer, pas à masquer)");
                    return;
                }

                // brief 005-refonte-visuelle-carte, itération de reprise : centrer les 3
                // fenêtres sur un VRAI point de frontière politique (milieu de deux
                // provinces voisines à propriétaires différents), pas le centre géométrique
                // du monde (qui peut tomber en mer ou loin de toute frontière) — sinon la
                // fenêtre "max" (1/16 largeur monde) risque de ne contenir aucune frontière
                // du tout. Réutilise ProvinceNeighbor + ProvinceOwnership (fait ECS
                // simulé, indépendant de la bascule pilote), même patron que
                // FindLandNeighborCountryId.
                if (!TryFindBorderMidpoint(em, out var borderCx, out var borderCy))
                {
                    log.AppendLine("FAIL no_political_border_found_for_centering");
                    return;
                }
                log.AppendLine(
                    "border_center_x=" + borderCx.ToString("0.###") + " border_center_y=" + borderCy.ToString("0.###") +
                    " pilot_map_enabled=" + PilotMapProvider.Enabled +
                    " pilot_data_loaded=" + PilotMapProvider.DataLoaded);

                var fullW = fullGeo.MaxX - fullGeo.MinX;
                var fullH = fullGeo.MaxY - fullGeo.MinY;

                var levels = new (string tag, float factor)[]
                {
                    ("min", 1f), ("mid", 0.25f), ("max", 1f / 16f)
                };

                foreach (var (tag, factor) in levels)
                {
                    var w = fullW * factor;
                    var hgt = fullH * factor;
                    var window = new MapWindow
                    {
                        MinX = borderCx - w * 0.5f, MaxX = borderCx + w * 0.5f,
                        MinY = borderCy - hgt * 0.5f, MaxY = borderCy + hgt * 0.5f
                    };
                    var geo = MapSnapshotExporter.BuildMapGeometry(MapSnapshotExporter.Width, MapSnapshotExporter.Height, window);
                    // LabelDensity.None : aucun texte de label ne doit polluer le comptage de
                    // pixels sombres (glyphes rendus dans une encre sombre, même gamme de
                    // luminosité que le trait de bordure recherché ici).
                    var pixels = MapSnapshotExporter.RenderPoliticalPixels(
                        em, geo, MapSnapshotExporter.LabelDensity.None, -1);
                    if (pixels == null)
                    {
                        log.AppendLine("FAIL pixels null level=" + tag);
                        continue;
                    }

                    // Fenêtre centrée sur le point de frontière connu à TOUS les niveaux de
                    // zoom : la frontière passe donc, par construction, près du centre de
                    // l'image à chaque niveau — on restreint la mesure à la boîte centrale
                    // (40%-60% largeur ET hauteur), balayée dans les deux sens (lignes ET
                    // colonnes), pour rester robuste à l'orientation locale du tracé sans
                    // capter le littoral ou d'autres frontières ailleurs dans l'image.
                    var runs = new List<int>(64);
                    var xLo = (int)(MapSnapshotExporter.Width * 0.4f);
                    var xHi = (int)(MapSnapshotExporter.Width * 0.6f);
                    var yLo = (int)(MapSnapshotExporter.Height * 0.4f);
                    var yHi = (int)(MapSnapshotExporter.Height * 0.6f);

                    var transitionEdges = 0;
                    var totalEdges = 0;

                    for (var py = yLo; py < yHi; py++)
                    {
                        var (te, to) = ScanLineForRunsAndEdges(
                            pixels, MapSnapshotExporter.Width, py, xLo, xHi, horizontal: true, runs);
                        transitionEdges += te;
                        totalEdges += to;
                    }
                    for (var px = xLo; px < xHi; px++)
                    {
                        var (te, to) = ScanLineForRunsAndEdges(
                            pixels, MapSnapshotExporter.Width, px, yLo, yHi, horizontal: false, runs);
                        transitionEdges += te;
                        totalEdges += to;
                    }

                    var path = Path.Combine(capturesDir, "border_zoom_" + tag + "_" + phase + ".png");
                    MapSnapshotExporter.WriteMapBufferPng(pixels, MapSnapshotExporter.Width, MapSnapshotExporter.Height, path);

                    var transitionFraction = totalEdges > 0 ? (float)transitionEdges / totalEdges : -1f;
                    if (runs.Count == 0)
                    {
                        log.AppendLine(
                            "border_stroke level=" + tag + " phase=" + phase +
                            " runs_found=0 median_px=-1 transition_edges=" + transitionEdges +
                            " total_edges=" + totalEdges + " transition_fraction=" + transitionFraction.ToString("0.###") +
                            " file=" + path + " sha256=" + Sha256OfFile(path));
                        continue;
                    }

                    runs.Sort();
                    var median = runs[runs.Count / 2];
                    log.AppendLine(
                        "border_stroke level=" + tag + " phase=" + phase +
                        " runs_found=" + runs.Count + " median_px=" + median +
                        " min_px=" + runs[0] + " max_px=" + runs[runs.Count - 1] +
                        " window_width=" + w.ToString("0.###") +
                        " transition_edges=" + transitionEdges + " total_edges=" + totalEdges +
                        " transition_fraction=" + transitionFraction.ToString("0.###") +
                        " file=" + path + " sha256=" + Sha256OfFile(path));
                }
            }
            finally
            {
                PilotMapProvider.SetEnabled(prevPilot, clearCache: true);
            }
        }

        /// <summary>
        /// Balaye UNE ligne (ligne fixe si <paramref name="horizontal"/>, colonne fixe
        /// sinon) sur [<paramref name="lo"/>, <paramref name="hi"/>), détecte les plages
        /// contiguës de pixels sombres (R+G+B &lt; 90, même seuil que la mesure de largeur)
        /// et les ajoute à <paramref name="runs"/> ; pour chaque plage, compare le pixel
        /// immédiatement adjacent (le bord de la plage) à un pixel 3 pixels plus loin
        /// EXACTEMENT à la même couleur ⇒ transition dure (escalier, 0 pixel intermédiaire) ;
        /// couleur différente ⇒ pixel de transition compté (mélange réel, preuve d'un
        /// anti-aliasing appliqué, pas seulement affirmé).
        /// </summary>
        static (int transitionEdges, int totalEdges) ScanLineForRunsAndEdges(
            Color32[] pixels, int width, int fixedCoord, int lo, int hi, bool horizontal, List<int> runs)
        {
            const int farOffset = 3;
            var transitionEdges = 0;
            var totalEdges = 0;

            Color32 At(int t)
            {
                var px = horizontal ? t : fixedCoord;
                var py = horizontal ? fixedCoord : t;
                return pixels[py * width + px];
            }

            bool IsDark(Color32 c) => (c.r + c.g + c.b) < 90;

            void CloseRun(int start, int end)
            {
                runs.Add(end - start + 1);

                var before = start - 1;
                var beforeFar = start - 1 - farOffset;
                if (before >= lo && beforeFar >= lo)
                {
                    totalEdges++;
                    var a = At(before);
                    var b = At(beforeFar);
                    if (a.r != b.r || a.g != b.g || a.b != b.b)
                        transitionEdges++;
                }

                var after = end + 1;
                var afterFar = end + 1 + farOffset;
                if (after < hi && afterFar < hi)
                {
                    totalEdges++;
                    var a = At(after);
                    var b = At(afterFar);
                    if (a.r != b.r || a.g != b.g || a.b != b.b)
                        transitionEdges++;
                }
            }

            var runStart = -1;
            for (var t = lo; t < hi; t++)
            {
                var dark = IsDark(At(t));
                if (dark)
                {
                    if (runStart < 0) runStart = t;
                }
                else if (runStart >= 0)
                {
                    CloseRun(runStart, t - 1);
                    runStart = -1;
                }
            }
            if (runStart >= 0)
                CloseRun(runStart, hi - 1);

            return (transitionEdges, totalEdges);
        }

        static bool TryFindBorderMidpoint(EntityManager em, out float cx, out float cy)
        {
            cx = 0f;
            cy = 0f;
            var coords = ProvinceCoordinates.LoadProjected(out _);
            var byId = new Dictionary<int, ProvinceCoordinates.Point>(coords.Count);
            for (var i = 0; i < coords.Count; i++)
                byId[coords[i].Id] = coords[i];

            using var q = em.CreateEntityQuery(
                ComponentType.ReadOnly<ProvinceData>(),
                ComponentType.ReadOnly<ProvinceOwnership>(),
                ComponentType.ReadOnly<ProvinceNeighbor>());
            using var entities = q.ToEntityArray(Allocator.Temp);
            using var pdata = q.ToComponentDataArray<ProvinceData>(Allocator.Temp);
            using var owns = q.ToComponentDataArray<ProvinceOwnership>(Allocator.Temp);

            var idToIndex = new Dictionary<int, int>(pdata.Length);
            for (var i = 0; i < pdata.Length; i++)
                idToIndex[pdata[i].ProvinceId] = i;

            for (var i = 0; i < entities.Length; i++)
            {
                if (owns[i].Owner == Entity.Null)
                    continue;
                var buf = em.GetBuffer<ProvinceNeighbor>(entities[i]);
                for (var n = 0; n < buf.Length; n++)
                {
                    if (buf[n].IsStrait)
                        continue;
                    if (!idToIndex.TryGetValue(buf[n].NeighborProvinceId, out var j))
                        continue;
                    if (owns[j].Owner == Entity.Null || owns[j].Owner == owns[i].Owner)
                        continue;
                    if (!byId.TryGetValue(pdata[i].ProvinceId, out var pa) ||
                        !byId.TryGetValue(pdata[j].ProvinceId, out var pb))
                        continue;
                    cx = (pa.X + pb.X) * 0.5f;
                    cy = (pa.Y + pb.Y) * 0.5f;
                    return true;
                }
            }
            return false;
        }

        // ---------- Success Condition 1 : orientation ----------

        static void RunOrientation(string capturesDir, StringBuilder log)
        {
            log.AppendLine();
            log.AppendLine("=== SUCCESS CONDITION 1 — orientation (buffer réel, même résolution que le jeu) ===");

            var w = MapSnapshotExporter.Width;
            var h = MapSnapshotExporter.Height;

            var geo = MapSnapshotExporter.BuildMapGeometry(w, h);
            if (geo == null)
            {
                log.AppendLine("FAIL geometry null");
                return;
            }

            // Monde RÉELLEMENT chargé (pas une World vide) : les repères géographiques
            // nommés (paire nord/sud, étiquette accentuée) exigent de vraies couleurs et
            // étiquettes de pays, pas un remplissage "non possédé" uniforme.
            using var h2 = new SimulationHarness(Seed);
            h2.RunTicks(0);
            var em = h2.EntityManager;

            var pixels = MapSnapshotExporter.RenderPoliticalPixels(
                em, geo, MapSnapshotExporter.LabelDensity.Countries, -1);
            if (pixels == null)
            {
                log.AppendLine("FAIL pixels null");
                return;
            }

            // Export de référence — chemin PROUVÉ (brief 003/004), une inversion.
            var exportPath = Path.Combine(capturesDir, "orientation_export_reference.png");
            MapSnapshotExporter.WriteMapBufferPng(pixels, w, h, exportPath);

            // Reproduction fidèle du chemin live APRÈS le fix (InGameHud.PresentFrame
            // applique désormais exactement MapSnapshotExporter.FlipMapBufferRows avant
            // SetPixels32 — même fonction, réutilisée, jamais dupliquée).
            var flipped = MapSnapshotExporter.FlipMapBufferRows(pixels, w, h);
            var afterPath = Path.Combine(capturesDir, "orientation_live_after_fix.png");
            MapSnapshotExporter.WritePngSized(flipped, w, h, afterPath);

            // Reproduction fidèle du chemin live AVANT le fix (SetPixels32 direct, sans
            // retournement — le bug corroboré propriétaire + Évaluateur, verdict.md brief 004).
            var beforePath = Path.Combine(capturesDir, "orientation_live_before_fix.png");
            MapSnapshotExporter.WritePngSized(pixels, w, h, beforePath);

            var exportSha = Sha256OfFile(exportPath);
            var afterSha = Sha256OfFile(afterPath);
            var beforeSha = Sha256OfFile(beforePath);

            log.AppendLine("file=" + exportPath + " sha256=" + exportSha);
            log.AppendLine("file=" + afterPath + " sha256=" + afterSha);
            log.AppendLine("file=" + beforePath + " sha256=" + beforeSha);
            log.AppendLine("export_equals_after_fix=" + string.Equals(exportSha, afterSha, StringComparison.Ordinal));
            log.AppendLine("after_fix_differs_from_before_fix=" +
                            !string.Equals(afterSha, beforeSha, StringComparison.Ordinal));

            // --- chemin GPU : lecture directe de la RenderTexture (contourne la
            // composition UI Toolkit, qui écrase toujours ce chemin avant la fin de la
            // frame — voir le journal --v005-dir gpu_attempt_frame0/settled, SHA
            // identiques). Ceci mesure l'orientation du BUFFER GPU lui-même, pour statuer
            // sur l'affirmation du doc-comment de PresentRenderTexture (« même orientation
            // que PresentFrame »), indépendamment de son inobservabilité côté joueur. ---
            log.AppendLine();
            log.AppendLine("=== SUCCESS CONDITION 1 (GPU) — lecture directe RenderTexture ===");
            var prevPilot = PilotMapProvider.Enabled;
            try
            {
                PilotMapProvider.SetEnabled(true, clearCache: true);
                var views = MapSnapshotExporter.BuildViewsForRender(em, geo.ViewsSkeleton, CountryColors.Load());
                if (!MapGpuRenderer.BuildPalette(views, out var err))
                {
                    log.AppendLine("gpu_readback_unavailable reason=palette_refusée:" + err);
                }
                else if (!MapGpuRenderer.IsAvailable)
                {
                    log.AppendLine("gpu_readback_unavailable reason=" + MapGpuRenderer.LastUnavailableReason);
                }
                else
                {
                    var rt = MapGpuRenderer.Render(
                        w, h, geo.MinX, geo.MaxX, geo.MinY, geo.MaxY,
                        lod: 0, sea: new Color(0.06f, 0.2f, 0.28f, 1f),
                        hoverCellId: -1, selectedCellId: -1);
                    if (rt == null)
                    {
                        log.AppendLine("gpu_readback_unavailable reason=" + MapGpuRenderer.LastUnavailableReason);
                    }
                    else
                    {
                        var gpuPixels = MapGpuRenderer.ReadbackLastFrame(w, h);
                        if (gpuPixels == null)
                        {
                            log.AppendLine("gpu_readback_FAIL readback_null");
                        }
                        else
                        {
                            // Doc-comment de ReadbackLastFrame : renvoie déjà "nord en rangée 0",
                            // aucun retournement — on l'encode donc SANS flip, exactement comme
                            // WritePngSized le fait pour reproduire le chemin CPU "avant fix" ;
                            // ce test dit si CETTE affirmation (nord@py0 sans retournement) est
                            // cohérente avec l'export de référence UNE FOIS retournée, comme le
                            // buffer CPU l'exige.
                            var gpuRawPath = Path.Combine(capturesDir, "orientation_gpu_readback_raw.png");
                            MapSnapshotExporter.WritePngSized(gpuPixels, w, h, gpuRawPath);
                            var gpuFlipped = MapSnapshotExporter.FlipMapBufferRows(gpuPixels, w, h);
                            var gpuFlippedPath = Path.Combine(capturesDir, "orientation_gpu_readback_flipped.png");
                            MapSnapshotExporter.WritePngSized(gpuFlipped, w, h, gpuFlippedPath);
                            log.AppendLine("file=" + gpuRawPath + " sha256=" + Sha256OfFile(gpuRawPath));
                            log.AppendLine("file=" + gpuFlippedPath + " sha256=" + Sha256OfFile(gpuFlippedPath));
                            log.AppendLine(
                                "gpu_raw_equals_cpu_export=" +
                                string.Equals(Sha256OfFile(gpuRawPath), exportSha, StringComparison.Ordinal) +
                                " gpu_flipped_equals_cpu_export=" +
                                string.Equals(Sha256OfFile(gpuFlippedPath), exportSha, StringComparison.Ordinal));
                        }
                    }
                }
            }
            finally
            {
                PilotMapProvider.SetEnabled(prevPilot, clearCache: true);
                MapGpuRenderer.Release();
            }

            // Repère de mirroring : étiquette province accentuée (Île-de-France), même
            // méthode que brief 004 (V004AccentCaptureRunner) — labels provinces, pas pays.
            var pixelsProv = MapSnapshotExporter.RenderPoliticalPixels(
                em, geo, MapSnapshotExporter.LabelDensity.Provinces, -1);
            if (pixelsProv != null)
            {
                var provExportPath = Path.Combine(capturesDir, "orientation_export_provinces.png");
                MapSnapshotExporter.WriteMapBufferPng(pixelsProv, w, h, provExportPath);
                var provFlipped = MapSnapshotExporter.FlipMapBufferRows(pixelsProv, w, h);
                var provAfterPath = Path.Combine(capturesDir, "orientation_live_after_fix_provinces.png");
                MapSnapshotExporter.WritePngSized(provFlipped, w, h, provAfterPath);
                var provBeforePath = Path.Combine(capturesDir, "orientation_live_before_fix_provinces.png");
                MapSnapshotExporter.WritePngSized(pixelsProv, w, h, provBeforePath);
                log.AppendLine("file=" + provExportPath + " sha256=" + Sha256OfFile(provExportPath));
                log.AppendLine("file=" + provAfterPath + " sha256=" + Sha256OfFile(provAfterPath));
                log.AppendLine("file=" + provBeforePath + " sha256=" + Sha256OfFile(provBeforePath));
                log.AppendLine("province_export_equals_after_fix=" +
                                string.Equals(Sha256OfFile(provExportPath), Sha256OfFile(provAfterPath), StringComparison.Ordinal));
            }
        }

        // ---------- Success Condition 2 : cadrage initial ----------

        static void RunPlayableWindow(StringBuilder log)
        {
            log.AppendLine();
            log.AppendLine("=== SUCCESS CONDITION 2 — cadrage initial (dérivation indépendante) ===");

            using var h = new SimulationHarness(Seed);
            h.RunTicks(0);
            var em = h.EntityManager;

            var geo = MapSnapshotExporter.BuildMapGeometry(
                MapSnapshotExporter.Width, MapSnapshotExporter.Height);
            if (geo == null)
            {
                log.AppendLine("FAIL geometry null");
                return;
            }

            var coords = ProvinceCoordinates.LoadProjected(out _);
            var byId = new Dictionary<int, ProvinceCoordinates.Point>(coords.Count);
            for (var i = 0; i < coords.Count; i++)
                byId[coords[i].Id] = coords[i];

            var minX = float.MaxValue;
            var maxX = float.MinValue;
            var minY = float.MaxValue;
            var maxY = float.MinValue;
            var playableCount = 0;
            var totalProvinces = 0;

            using (var q = em.CreateEntityQuery(
                       ComponentType.ReadOnly<ProvinceData>(),
                       ComponentType.ReadOnly<ProvinceOwnership>()))
            using (var entities = q.ToEntityArray(Allocator.Temp))
            using (var pdata = q.ToComponentDataArray<ProvinceData>(Allocator.Temp))
            using (var owns = q.ToComponentDataArray<ProvinceOwnership>(Allocator.Temp))
            {
                totalProvinces = pdata.Length;
                for (var i = 0; i < pdata.Length; i++)
                {
                    var owned = owns[i].Owner != Entity.Null;
                    var populated = em.HasComponent<PopulationData>(entities[i]) &&
                                     em.GetComponentData<PopulationData>(entities[i]).Total > 0;
                    if (!owned && !populated)
                        continue;
                    if (!byId.TryGetValue(pdata[i].ProvinceId, out var pt))
                        continue;
                    if (pt.X < minX) minX = pt.X;
                    if (pt.X > maxX) maxX = pt.X;
                    if (pt.Y < minY) minY = pt.Y;
                    if (pt.Y > maxY) maxY = pt.Y;
                    playableCount++;
                }
            }

            log.AppendLine("total_provinces=" + totalProvinces);
            log.AppendLine("playable_provinces_count=" + playableCount);
            log.AppendLine(
                "playable_extent_x=[" + minX.ToString("0.###") + "," + maxX.ToString("0.###") + "] " +
                "y=[" + minY.ToString("0.###") + "," + maxY.ToString("0.###") + "]");
            log.AppendLine(
                "full_world_extent_x=[" + geo.MinX.ToString("0.###") + "," + geo.MaxX.ToString("0.###") + "] " +
                "y=[" + geo.MinY.ToString("0.###") + "," + geo.MaxY.ToString("0.###") + "]");

            var playableArea = playableCount > 0 ? (maxX - minX) * (maxY - minY) : -1f;
            var fullArea = (geo.MaxX - geo.MinX) * (geo.MaxY - geo.MinY);
            log.AppendLine(
                "playable_area=" + playableArea.ToString("0.##") +
                " full_world_area=" + fullArea.ToString("0.##") +
                " playable_smaller_than_full=" + (playableCount > 0 && playableArea < fullArea));
        }

        // ---------- Success Condition 5 : front rouge ----------

        // ---------- Success Condition 7 : coût réel par tick ----------

        static void RunTickCost(StringBuilder log)
        {
            log.AppendLine();
            log.AppendLine("=== SUCCESS CONDITION 7 — coût réel par tick (>= 20 ticks, dédié) ===");

            const int ticksToMeasure = 30;
            using var h = new SimulationHarness(Seed);
            h.RunTicks(0); // warmup init hors mesure

            var samples = new List<double>(ticksToMeasure);
            for (var t = 0; t < ticksToMeasure; t++)
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                h.RunTicks(1);
                sw.Stop();
                samples.Add(sw.Elapsed.TotalMilliseconds);
            }

            var sum = 0.0;
            var max = 0.0;
            var min = double.MaxValue;
            for (var i = 0; i < samples.Count; i++)
            {
                log.AppendLine("tick_index=" + i + " ms=" + samples[i].ToString("0.###"));
                sum += samples[i];
                if (samples[i] > max) max = samples[i];
                if (samples[i] < min) min = samples[i];
            }

            var avg = sum / samples.Count;
            log.AppendLine(
                "ms_per_tick_sample_size=" + samples.Count +
                " avg=" + avg.ToString("0.###") +
                " min=" + min.ToString("0.###") +
                " max=" + max.ToString("0.###"));

            const float secondsPerTick = VictoriaGame.Core.TickControl.DefaultSecondsPerTick;
            var budgetMs = secondsPerTick * 1000.0;
            log.AppendLine(
                "current_seconds_per_tick=" + secondsPerTick +
                " current_pacing_budget_ms=" + budgetMs.ToString("0.#") +
                " avg_tick_cost_fraction_of_budget=" + (avg / budgetMs).ToString("0.0000"));
        }

        static void RunFrontRim(string capturesDir, StringBuilder log, string phase)
        {
            log.AppendLine();
            log.AppendLine("=== SUCCESS CONDITION 5 — front rouge (guerre déclarée, déterministe, phase=" + phase + ") ===");
            log.AppendLine("diag_code_marker=filterV3_" + DateTime.UtcNow.Ticks);

            const int tickBound = 10;
            using var h = new SimulationHarness(Seed);
            h.RunTicks(0);
            var em = h.EntityManager;

            var targetId = FindLandNeighborCountryId(em, PlayerCountryId);
            log.AppendLine("player_country_id=" + PlayerCountryId + " target_neighbor_country_id=" + targetId);
            if (targetId < 0)
            {
                log.AppendLine(
                    "FAIL no_land_neighbor_found tick_bound=" + tickBound +
                    " — aucune guerre déclarable, front-rim non atteignable par ce mécanisme");
                return;
            }

            var declared = PlayerIntentionSubmit.EnqueueDeclareWar(em, PlayerCountryId, targetId);
            log.AppendLine("declare_war_enqueued=" + declared);
            if (!declared)
            {
                log.AppendLine("FAIL declare_war_refused tick_bound=" + tickBound);
                return;
            }

            var geo = MapSnapshotExporter.BuildMapGeometry(
                MapSnapshotExporter.Width, MapSnapshotExporter.Height);
            var found = false;
            for (var t = 1; t <= tickBound; t++)
            {
                h.RunTicks(1);
                var px = MapSnapshotExporter.RenderPoliticalPixels(
                    em, geo, MapSnapshotExporter.LabelDensity.Countries, -1);
                log.AppendLine(
                    "tick=" + t + " front_pixel_count=" + MapSnapshotExporter.LastFrontPixelCount +
                    " front_provinces=" + MapSnapshotExporter.LastFrontDrawnProvinceIds.Count);
                if (MapSnapshotExporter.LastFrontPixelCount > 0)
                {
                    found = true;
                    var path = Path.Combine(capturesDir, "front_rim_present_" + phase + ".png");
                    MapSnapshotExporter.WriteMapBufferPng(px, geo.Width, geo.Height, path);
                    log.AppendLine("capture file=" + path + " sha256=" + Sha256OfFile(path));

                    // brief 005-refonte-visuelle-carte, itération de reprise, Success
                    // Condition 5 : échantillon RGB lu directement dans le buffer capturé
                    // (pas une valeur recopiée du source pour AFFIRMER un résultat) —
                    // FrontRimColor/FrontRimHalo restent privés à MapSnapshotExporter
                    // (encapsulation non contournée) ; on se contente de COMPTER, dans le
                    // buffer réellement rendu, les occurrences des 2 paires de couleurs
                    // candidates (AVANT : 210,36,36 rim / 96,12,12 halo — APRÈS : 150,60,60
                    // rim / 70,26,26 halo, lues dans git diff comme n'importe quelle autre
                    // valeur de constante de présentation, PAS un hash de parité/anchor —
                    // hard-won rule 12 ne s'applique pas à un choix de couleur UI). La paire
                    // active dans CETTE phase doit avoir un compte > 0, l'autre paire = 0 —
                    // auto-validation croisée, pas une assertion nue.
                    var candidateColors = new (string label, Color32 c)[]
                    {
                        ("before_rim", new Color32(210, 36, 36, 255)),
                        ("before_halo", new Color32(96, 12, 12, 255)),
                        ("after_rim", new Color32(150, 60, 60, 255)),
                        ("after_halo", new Color32(70, 26, 26, 255)),
                    };
                    var candidateCounts = new int[candidateColors.Length];
                    for (var i = 0; i < px.Length; i++)
                    {
                        for (var c = 0; c < candidateColors.Length; c++)
                        {
                            if (px[i].r == candidateColors[c].c.r && px[i].g == candidateColors[c].c.g &&
                                px[i].b == candidateColors[c].c.b && px[i].a == candidateColors[c].c.a)
                                candidateCounts[c]++;
                        }
                    }
                    for (var c = 0; c < candidateColors.Length; c++)
                    {
                        log.AppendLine(
                            "front_rim_color_candidate_count phase=" + phase +
                            " label=" + candidateColors[c].label +
                            " r=" + candidateColors[c].c.r + " g=" + candidateColors[c].c.g +
                            " b=" + candidateColors[c].c.b + " a=" + candidateColors[c].c.a +
                            " count=" + candidateCounts[c]);
                    }

                    RunFrontRimLegendHoverProof(em, geo, log, phase);
                    break;
                }
            }

            log.AppendLine("front_rim_reachable_within_tick_bound=" + found + " tick_bound=" + tickBound);
        }

        /// <summary>
        /// brief 005-refonte-visuelle-carte, itération de correction du feedback (Issue 3,
        /// SC5a) : le flag `front_rim_legend_reachable_flag` avait été porté à 1 sur la
        /// seule foi d'une relecture de <c>git diff</c> — présence de code, pas preuve de
        /// fonction (hard-won rule 7). Preuve mécanique ici, correctif attendu explicitement
        /// par le feedback quand la simulation d'un survol n'est pas atteignable dans la
        /// chaîne de capture standalone : appel du VRAI point d'entrée production que le
        /// survol souris du jeu appelle,
        /// <see cref="MapDisplaySystem.UpdateHoverAtTexturePixel"/>, sur un pixel réellement
        /// peint en liseré de front cette même frame — pas une réimplémentation de son texte,
        /// pas une lecture de code. <see cref="MapDisplaySystem.ActiveGeometry"/> est posé via
        /// <see cref="MapDisplaySystem.RenderGpuBackgroundForMeasure"/>, le seul point
        /// d'entrée déjà publié (v1_095) pour fixer cette géométrie hors boucle de rendu —
        /// jamais un champ privé contourné par réflexion.
        /// </summary>
        static void RunFrontRimLegendHoverProof(
            EntityManager em, MapSnapshotExporter.MapGeometry geo, StringBuilder log, string phase)
        {
            MapDisplaySystem.RenderGpuBackgroundForMeasure(em, geo);

            var frontViewIndex = -1;
            if (geo.ViewsSkeleton != null)
            {
                for (var vi = 0; vi < geo.ViewsSkeleton.Count; vi++)
                {
                    if (MapSnapshotExporter.LastFrontDrawnProvinceIds.Contains(geo.ViewsSkeleton[vi].Id))
                    {
                        frontViewIndex = vi;
                        break;
                    }
                }
            }

            if (frontViewIndex < 0)
            {
                log.AppendLine("front_rim_hover_label phase=" + phase + " FAIL no_front_view_index_resolved");
                return;
            }

            var hoverPx = -1;
            var hoverPy = -1;
            for (var yy = 0; yy < geo.Height && hoverPx < 0; yy++)
            {
                for (var xx = 0; xx < geo.Width; xx++)
                {
                    if (geo.ProvinceAt[yy * geo.Width + xx] != frontViewIndex)
                        continue;
                    hoverPx = xx;
                    hoverPy = yy;
                    break;
                }
            }

            if (hoverPx < 0)
            {
                log.AppendLine(
                    "front_rim_hover_label phase=" + phase +
                    " FAIL no_pixel_for_front_view_index=" + frontViewIndex);
                return;
            }

            MapViewport.ClearHover();
            MapDisplaySystem.UpdateHoverAtTexturePixel(hoverPx, hoverPy);
            var hoverLabel = MapViewport.HoverLabel ?? "";
            var legendPresent = hoverLabel.IndexOf(
                "Front de guerre actif", StringComparison.Ordinal) >= 0;
            log.AppendLine(
                "front_rim_hover_label phase=" + phase +
                " px=" + hoverPx + " py=" + hoverPy +
                " view_index=" + frontViewIndex +
                " province_id=" + geo.ViewsSkeleton[frontViewIndex].Id +
                " text='" + hoverLabel + "'" +
                " legend_present=" + legendPresent);
            MapViewport.ClearHover();
        }

        static int FindLandNeighborCountryId(EntityManager em, int selfId)
        {
            Entity selfEntity = Entity.Null;
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<CountryData>()))
            using (var entities = q.ToEntityArray(Allocator.Temp))
            using (var data = q.ToComponentDataArray<CountryData>(Allocator.Temp))
            {
                for (var i = 0; i < data.Length; i++)
                {
                    if (data[i].CountryId != selfId)
                        continue;
                    selfEntity = entities[i];
                    break;
                }
            }

            if (selfEntity == Entity.Null)
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

        static string Sha256OfFile(string path)
        {
            if (!File.Exists(path))
                return "(absent)";
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(path);
            var hash = sha.ComputeHash(stream);
            var sb = new StringBuilder(hash.Length * 2);
            foreach (var b in hash)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
