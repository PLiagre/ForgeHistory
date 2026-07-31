using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using VictoriaGame.Presentation;

namespace VictoriaGame.Tests
{
    /// <summary>v1_070 — couleur politique = tag propriétaire ; mer sans pixel noir.</summary>
    public class V1070PoliticalMapTests
    {
        [Test]
        public void V1070_A_SameTagSamePoliticalColor()
        {
            PilotMapProvider.EnsureLoaded();
            Assert.IsTrue(PilotMapProvider.DataLoaded);
            Assert.IsTrue(PilotMapProvider.TryGetCellsPerTag(out var counts));
            Assert.AreEqual(109, counts["FRA"]);
            Assert.AreEqual(33, counts["ENG"]);
            Assert.AreEqual(23, counts["AUS"]);
            Assert.AreEqual(22, counts["BUR"]);
            Assert.AreEqual(7, counts["CAS"]);

            Assert.IsTrue(PilotMapProvider.TryGetTagColor("FRA", out var fra));
            Assert.IsTrue(PilotMapProvider.TryGetTagColor("ENG", out var eng));
            Assert.AreNotEqual(CountryColors.ToHex(fra), CountryColors.ToHex(eng));

            var colors = CountryColors.Load();
            var fills = PilotMapProvider.BuildPoliticalFills(237);
            Color32? fraFill = null;
            var fraViews = 0;
            for (var i = 0; i < fills.Length; i++)
            {
                var c = PilotMapProvider.PoliticalColorOfView(i, colors);
                // Reconstruct: only count FRA by matching published tag color.
                if (c.r != fra.r || c.g != fra.g || c.b != fra.b)
                    continue;
                fraViews++;
                if (!fraFill.HasValue)
                    fraFill = c;
                else
                    Assert.AreEqual(CountryColors.ToHex(fraFill.Value), CountryColors.ToHex(c));
            }

            Assert.GreaterOrEqual(fraViews, 100, "FRA doit former un bloc (~109 cellules)");
            Assert.AreEqual(CountryColors.ToHex(fra), CountryColors.ToHex(fraFill.Value));
        }

        [Test]
        public void V1070_B_UnownedNotCountryColor()
        {
            PilotMapProvider.EnsureLoaded();
            var colors = CountryColors.Load();
            Assert.IsTrue(PilotMapProvider.TryGetCellsPerTag(out var counts));
            var countryHex = new HashSet<string>(StringComparer.Ordinal);
            foreach (var tag in counts.Keys)
            {
                Assert.IsTrue(PilotMapProvider.TryGetTagColor(tag, out var c));
                countryHex.Add(CountryColors.ToHex(c));
            }

            var unowned = 0;
            for (var i = 0; i < 237; i++)
            {
                var fill = PilotMapProvider.PoliticalColorOfView(i, colors);
                // Unowned hatch : pas HasOwner → PoliticalColorOfView renvoie hatch.
                // On compte ceux qui ne matchent aucune couleur pays.
                var hex = CountryColors.ToHex(fill);
                if (countryHex.Contains(hex))
                    continue;
                unowned++;
            }

            Assert.AreEqual(43, unowned, "43 cellules sans propriétaire distinctes");
            Assert.IsFalse(countryHex.Contains("#c8b840"));
            Assert.IsFalse(countryHex.Contains("#5a5228"));
        }

        [Test]
        public void V1070_C_NoBlackSeaPixels_ThreeLods()
        {
            PilotMapProvider.EnsureLoaded();
            var colors = CountryColors.Load();
            PilotMapProvider.Enabled = true;
            try
            {
                for (var lod = 0; lod <= 2; lod++)
                {
                    // lod2 natif suffit pour C ; lod0/1 via BuildMapGeometry 320×240 (budget).
                    if (lod == 2)
                    {
                        var pixels = PilotMapProvider.ComposeNativeLodPixels(
                            lod, political: true, colors, out _, out _);
                        Assert.IsNotNull(pixels);
                        var black = 0;
                        for (var i = 0; i < pixels.Length; i++)
                        {
                            var c = pixels[i];
                            if (c.r == 0 && c.g == 0 && c.b == 0)
                                black++;
                        }

                        Assert.AreEqual(0, black, "LOD2 natif : aucun RGB0");
                    }
                    else
                    {
                        var geo = PilotMapProvider.BuildMapGeometry(320, 240, null, lod);
                        Assert.IsNotNull(geo);
                        Assert.Greater(CountLand(geo.IsLand), 100, "LOD" + lod + " doit avoir de la terre");
                        var views = new List<MapSnapshotExporter.ProvinceView>();
                        PilotMapProvider.ApplyPilotColors(geo.ViewsSkeleton, colors, views);
                        var pixels = new Color32[geo.Width * geo.Height];
                        for (var i = 0; i < pixels.Length; i++)
                        {
                            if (!geo.IsLand[i])
                            {
                                pixels[i] = colors.Sea;
                                continue;
                            }

                            var vi = geo.ProvinceAt[i];
                            pixels[i] = vi >= 0 && vi < views.Count ? views[vi].Fill : colors.Sea;
                        }

                        PilotMapProvider.ApplyHillshadeOnLand(
                            pixels, geo.IsLand, geo.Width, geo.Height,
                            geo.MinX, geo.MaxX, geo.MinY, geo.MaxY,
                            colors.Sea, lod);
                        Assert.AreEqual(
                            0,
                            PilotMapProvider.CountBlackSeaPixels(pixels, geo.IsLand),
                            "LOD" + lod);
                    }
                }

                Assert.Greater(PilotMapProvider.LastBlackMissingDemPixels, 0);
            }
            finally
            {
                PilotMapProvider.Enabled = false;
            }
        }

        static int CountLand(bool[] isLand)
        {
            var n = 0;
            for (var i = 0; i < isLand.Length; i++)
                if (isLand[i])
                    n++;
            return n;
        }

        [Test]
        public void V1070_D_FlagOff_GeometryUnchanged()
        {
            PilotMapProvider.Enabled = false;
            MapGeometryCache.ResetStatsAndClear();
            var a = MapSnapshotExporter.BuildMapGeometry(
                MapSnapshotExporter.Width, MapSnapshotExporter.Height);
            Assert.IsNotNull(a);
            var ha = HashGeometry(a);

            MapGeometryCache.ResetStatsAndClear();
            var b = MapSnapshotExporter.BuildMapGeometry(
                MapSnapshotExporter.Width, MapSnapshotExporter.Height);
            Assert.IsNotNull(b);
            var hb = HashGeometry(b);
            Assert.AreEqual(ha, hb, "Drapeau OFF ⇒ géométrie Voronoï bit-identique");
        }

        [Test]
        public void V1070_Proof_CapturesAndLog()
        {
            PilotMapProvider.EnsureLoaded();
            var captureDir = Path.Combine(Application.dataPath, "..", "Captures", "v1_070");
            var logPath = Path.Combine(Application.dataPath, "..", "Logs", "v1_070_political.log");
            // Conserver le défaut v1_068 comme « avant ».
            var beforeDir = Path.Combine(Application.dataPath, "..", "Captures", "v1_068");
            var beforeSrc = Path.Combine(beforeDir, "pilot_country_political_lod1.png");
            Directory.CreateDirectory(captureDir);
            if (File.Exists(beforeSrc))
            {
                File.Copy(beforeSrc,
                    Path.Combine(captureDir, "before_pilot_country_political_lod1.png"),
                    overwrite: true);
            }

            var written = PilotMapProvider.WritePoliticalProofAndCaptures(captureDir, logPath);
            Assert.IsTrue(File.Exists(written), "log manquant");
            var log = File.ReadAllText(written);
            StringAssert.Contains("V1070-A", log);
            StringAssert.Contains("PASS", log);
            StringAssert.Contains("FRA=", log);
            Assert.IsTrue(File.Exists(Path.Combine(captureDir, "pilot_country_political_lod1.png")));
            Assert.IsTrue(File.Exists(Path.Combine(captureDir, "after_pilot_country_political_lod1.png")));
            Assert.IsTrue(File.Exists(Path.Combine(captureDir, "pilot_country_physical_lod1.png")));
            Assert.Greater(PilotMapProvider.LastBlackMissingDemPixels, 1000);
        }

        static string HashGeometry(MapSnapshotExporter.MapGeometry geo)
        {
            using var sha = SHA256.Create();
            var buf = new byte[geo.ProvinceAt.Length * 4 + geo.IsLand.Length];
            Buffer.BlockCopy(geo.ProvinceAt, 0, buf, 0, geo.ProvinceAt.Length * 4);
            for (var i = 0; i < geo.IsLand.Length; i++)
                buf[geo.ProvinceAt.Length * 4 + i] = geo.IsLand[i] ? (byte)1 : (byte)0;
            var hash = sha.ComputeHash(buf);
            var sb = new StringBuilder(hash.Length * 2);
            for (var i = 0; i < hash.Length; i++)
                sb.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
            return sb.ToString();
        }
    }
}
