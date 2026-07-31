using System;
using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using VictoriaGame.Presentation;
using Debug = UnityEngine.Debug;

namespace VictoriaGame.Tests
{
    /// <summary>
    /// Point d'entrée batchmode :
    /// -executeMethod VictoriaGame.Tests.V1074RiverBatchRunner.Run
    /// </summary>
    public static class V1074RiverBatchRunner
    {
        public static void Run()
        {
            V1074RiverTests.RunAndWriteArtifacts();
            Debug.Log("V1074RiverBatchRunner: DONE");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
        }
    }

    /// <summary>v1_074 — fleuves g5c, CRS lon/lat, épaisseurs, lazy load, réversibilité.</summary>
    [TestFixture]
    public class V1074RiverTests
    {
        [TearDown]
        public void TearDown()
        {
            PilotMapProvider.ShowRivers = false;
            PilotMapProvider.Enabled = false;
            MapSnapshotExporter.ResetZoomScaleToNeutral();
            MapGeometryCache.ResetStatsAndClear();
        }

        [Test]
        public void V1074_A_CrsLonLatChain_ControlSeineRouen()
        {
            Assert.IsTrue(PilotMapProvider.CheckV1074A(out var detail), detail);
            // Rouge : mètres EPSG:3035 lus comme lon/lat → loin de Rouen.
            PilotMapProvider.EnsureLoaded();
            const float meterX = 3696963.9f;
            const float meterY = 2543763.5f;
            ProvinceCoordinates.LoadProjected(out var mid);
            ProvinceCoordinates.Project(meterX, meterY, mid, out var badX, out var badY);
            PilotMapProvider.TryControlPointSeineRouen(
                out _, out _, out var goodX, out var goodY, out _, out _, out _);
            Assert.Greater(Math.Abs(badX - goodX) + Math.Abs(badY - goodY), 10f,
                "rouge V1074-A: mètres-as-lonlat devraient être loin");
        }

        [Test]
        public void V1074_B_ThicknessMeasuredDistinct()
        {
            Assert.IsTrue(PilotMapProvider.CheckV1074B(out var detail), detail);
            var nav = PilotMapProvider.NavigableThicknessFor(MapObservationLevel.Province);
            var non = PilotMapProvider.NonNavigableThicknessFor(MapObservationLevel.Province);
            Assert.AreNotEqual(nav, non, "rouge V1074-B: même épaisseur déclarée");
            Assert.Greater(nav, non);
        }

        [Test]
        public void V1074_C_IndeterminateNotPromoted()
        {
            Assert.IsTrue(PilotMapProvider.CheckV1074C(out var detail), detail);
            Assert.AreEqual(18, PilotMapProvider.CountIndeterminateSegments());
            // Rouge : peindre un indéterminé avec épaisseur/couleur navigable.
            var indT = PilotMapProvider.IndeterminateThicknessFor(MapObservationLevel.Province);
            var navT = PilotMapProvider.NavigableThicknessFor(MapObservationLevel.Province);
            Assert.Less(indT, navT, "rouge V1074-C: indéterminé promu (épaisseur nav)");
            Assert.AreNotEqual(
                PilotMapProvider.GetIndeterminateRiverColor(),
                PilotMapProvider.GetNavigableRiverColor(),
                "rouge V1074-C: même couleur que navigable");
        }

        [Test]
        public void V1074_D_RiversOff_BitIdentical()
        {
            PilotMapProvider.ApplyPresentationSettings(clearCache: true);
            PilotMapProvider.SetEnabled(true, clearCache: true);
            var colors = CountryColors.Load();
            Assert.IsTrue(
                PilotMapProvider.CheckV1074D(null, colors, out var detail), detail);
        }

        [Test]
        public void V1074_E_RiversNotLoadedAtStartup()
        {
            Assert.IsTrue(PilotMapProvider.CheckV1074E(out var detail), detail);
        }

        [Test]
        public void V1074_Artifacts_And_Verdict() => RunAndWriteArtifacts();

        public static void RunAndWriteArtifacts()
        {
            var captureDir = Path.Combine(Application.dataPath, "..", "Captures", "v1_074");
            var logPath = Path.Combine(Application.dataPath, "..", "Logs", "v1_074_rivers.log");
            var written = PilotMapProvider.WriteRiversProofAndCaptures(captureDir, logPath);
            Assert.IsTrue(File.Exists(written), "log manquant: " + written);
            var log = File.ReadAllText(written, Encoding.UTF8);
            StringAssert.Contains("V1074-A", log);
            StringAssert.Contains("V1074-E", log);
            StringAssert.Contains("crs_chosen:", log);
            StringAssert.Contains("Seine=", log);
            StringAssert.Contains("Loire=", log);
            StringAssert.Contains("Rhin=", log);
            Assert.IsTrue(File.Exists(Path.Combine(captureDir, "rivers_on_world.png")));
            Assert.IsTrue(File.Exists(Path.Combine(captureDir, "rivers_on_country.png")));
            Assert.IsTrue(File.Exists(Path.Combine(captureDir, "rivers_on_province.png")));
            Assert.IsTrue(File.Exists(Path.Combine(captureDir, "rivers_off_world.png")));
            Assert.IsTrue(File.Exists(Path.Combine(captureDir, "rivers_off_country.png")));
            Assert.IsTrue(File.Exists(Path.Combine(captureDir, "rivers_off_province.png")));

            Assert.IsTrue(PilotMapProvider.CheckV1074A(out var a), a);
            Assert.IsTrue(PilotMapProvider.CheckV1074B(out var b), b);
            Assert.IsTrue(PilotMapProvider.CheckV1074C(out var c), c);
            Assert.IsTrue(PilotMapProvider.CheckV1074E(out var e), e);

            Debug.Log(
                "V1074 verdict: segments=" +
                PilotMapProvider.RiverSegmentCount.ToString(CultureInfo.InvariantCulture) +
                " mouths=" +
                PilotMapProvider.RiverMouthCount.ToString(CultureInfo.InvariantCulture) +
                " log=" + written);
        }
    }
}
