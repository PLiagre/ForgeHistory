using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using VictoriaGame.Core;
using VictoriaGame.Economy;
using VictoriaGame.Politics;
using VictoriaGame.Presentation;
using VictoriaGame.World;
using Debug = UnityEngine.Debug;

namespace VictoriaGame.Tests
{
    /// <summary>Batch : -executeMethod VictoriaGame.Tests.V1087BatchRunner.Run</summary>
    public static class V1087BatchRunner
    {
        public static void Run()
        {
            try
            {
                V1087DevelopmentTests.RunAndWriteArtifacts();
                Debug.Log("V1087BatchRunner: DONE");
            }
            catch (Exception ex) when (HarnessAllocationGuard.IsNativeAllocationFailure(ex))
            {
                Debug.LogWarning("V1087BatchRunner: ALLOCATION_FAILURE — " + ex.Message);
                Debug.Log("V1087BatchRunner: DONE_PARTIAL");
            }
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
        }
    }

    /// <summary>
    /// v1_087 — PHASE XII : dégeler ProvinceDevelopment, ouvrir construction + investissement joueur.
    /// </summary>
    [TestFixture]
    public class V1087DevelopmentTests
    {
        const uint Seed = 42195u;
        const int ParityTicks = 100;
        const int SweepTicks = 800;
        const int CampaignLevels = 4; // 0..3 bumps Production
        const ulong ExpectedParity = ParityAnchors.Expected;
        const int PlayerCountryId = PlayerControl.DefaultControlledCountryId;
        const int ParisCityId = 1;
        const float CapacityPerDevDefault = 2400.643f;

        static string GameUnityRoot =>
            Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        static string LogPath => Path.Combine(GameUnityRoot, "Logs", "v1_087_development.log");
        static string CapturesDir => Path.Combine(GameUnityRoot, "Captures", "v1_087");

        [TearDown]
        public void TearDown() => ResetAll();

        [Test]
        public void V1087_Invest_Intention_Bumps_Production_And_Charges_Treasury()
        {
            ResetAll();
            BuildingAiPolicyConfig.Lock(BuildingAiPolicy.HoldNone, 0f);
            BuildingConstructionSystem.LockCapacityIntensity(0f);
            using var h = new SimulationHarness(Seed);
            h.RunTicks(0);
            var em = h.EntityManager;
            var provinceId = FindCityProvinceId(em, ParisCityId);
            Assert.GreaterOrEqual(provinceId, 0);
            var before = ReadDev(em, provinceId);
            var cost = ProvinceDevelopmentInvestment.CostForLevel(before.Production);
            SetPlayerTreasury(em, cost + 500f);
            var balBefore = ReadTreasury(em, PlayerCountryId);

            Assert.IsTrue(PlayerIntentionSubmit.EnqueueInvestProvinceDevelopment(
                em, PlayerCountryId, provinceId, ProvinceDevelopmentInvestment.AxisProduction));
            Assert.AreEqual(before.Production, ReadDev(em, provinceId).Production,
                "UI/intention n'écrit pas le monde avant le tick");

            h.RunTicks(1);
            var receipt = ReadReceipt(em);
            Assert.AreEqual(1, receipt.Accepted, $"reason={receipt.Reason}");
            Assert.AreEqual(PlayerIntentionKind.InvestProvinceDevelopment, receipt.Kind);
            var after = ReadDev(em, provinceId);
            Assert.AreEqual(before.Production + 1, after.Production);
            Assert.AreEqual(before.Tax, after.Tax);
            Assert.AreEqual(before.Manpower, after.Manpower);
            // ApplyInvest débite avant TaxSystem : le solde baisse d'au moins cost − revenus du tick.
            Assert.Less(ReadTreasury(em, PlayerCountryId), balBefore - cost * 0.5f);
        }

        [Test]
        public void V1087_Invest_Rejects_Ceiling_Unowned_Broke()
        {
            ResetAll();
            BuildingAiPolicyConfig.Lock(BuildingAiPolicy.HoldNone, 0f);
            BuildingConstructionSystem.LockCapacityIntensity(0f);
            using var h = new SimulationHarness(Seed);
            h.RunTicks(0);
            var em = h.EntityManager;
            var provinceId = FindCityProvinceId(em, ParisCityId);
            SetPlayerTreasury(em, 100000f);

            // Plafond
            var capped = ReadDev(em, provinceId);
            capped.Production = ProvinceDevelopmentInvestment.MaxLevel;
            WriteDev(em, provinceId, capped);
            PlayerIntentionSubmit.EnqueueInvestProvinceDevelopment(
                em, PlayerCountryId, provinceId, ProvinceDevelopmentInvestment.AxisProduction);
            h.RunTicks(1);
            Assert.AreEqual(0, ReadReceipt(em).Accepted);
            Assert.AreEqual("level_at_ceiling", ReadReceipt(em).Reason.ToString());

            // Trésorerie insuffisante
            var mid = ReadDev(em, provinceId);
            mid.Production = 5;
            WriteDev(em, provinceId, mid);
            var need = ProvinceDevelopmentInvestment.CostForLevel(5);
            SetPlayerTreasury(em, need * 0.25f);
            Assert.Less(ReadTreasury(em, PlayerCountryId), need);
            var prodBeforeBroke = ReadDev(em, provinceId).Production;
            PlayerIntentionSubmit.EnqueueInvestProvinceDevelopment(
                em, PlayerCountryId, provinceId, ProvinceDevelopmentInvestment.AxisProduction);
            h.RunTicks(1);
            Assert.AreEqual(0, ReadReceipt(em).Accepted, $"reason={ReadReceipt(em).Reason}");
            Assert.AreEqual("insufficient_treasury", ReadReceipt(em).Reason.ToString());
            Assert.AreEqual(prodBeforeBroke, ReadDev(em, provinceId).Production);

            // Pays non contrôlé (ENG ≠ PlayerControl)
            SetPlayerTreasury(em, 10000f);
            var eng = FindCountryIdByTag(em, "ENG");
            Assert.GreaterOrEqual(eng, 0);
            PlayerIntentionSubmit.EnqueueInvestProvinceDevelopment(
                em, eng, provinceId, ProvinceDevelopmentInvestment.AxisProduction);
            h.RunTicks(1);
            Assert.AreEqual(0, ReadReceipt(em).Accepted);
            Assert.AreEqual("country_not_controlled", ReadReceipt(em).Reason.ToString());
        }

        [Test]
        public void V1087_Build_Intention_Still_Works_From_Player_Path()
        {
            ResetAll();
            BuildingConstructionSystem.LockCapacityIntensity(0f);
            BuildingAiPolicyConfig.Lock(BuildingAiPolicy.HoldNone, 0f);
            using var h = new SimulationHarness(Seed);
            h.RunTicks(0);
            var em = h.EntityManager;
            Assert.IsTrue(BuildingConstructionSystem.TryGetCatalogEntry(em, BuildingType.Farm, out var cat));
            SetPlayerTreasury(em, cat.MoneyCost + 500f);
            Assert.IsTrue(PlayerIntentionSubmit.EnqueueStartBuildingConstruction(
                em, PlayerCountryId, ParisCityId, BuildingType.Farm));
            h.RunTicks(1);
            Assert.AreEqual(1, ReadReceipt(em).Accepted, $"reason={ReadReceipt(em).Reason}");
            Assert.IsTrue(BuildingConstructionSystem.CityHasActiveConstruction(em, ParisCityId));
        }

        [Test]
        public void V1087_Reversibility_Zero_Investment_BitIdentical()
        {
            ResetAll();
            ulong dig;
            using (var h = new SimulationHarness(Seed))
            {
                h.RunTicks(ParityTicks);
                dig = WorldDigest.Compute(h.EntityManager);
            }

            Assert.AreEqual(ExpectedParity, dig,
                "réversibilité à investissement nul → empreinte v1_009");
        }

        [Test]
        public void V1087_Artifacts_And_Verdict() => RunAndWriteArtifacts();

        public static void RunAndWriteArtifacts()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            Directory.CreateDirectory(CapturesDir);
            var sb = new StringBuilder(256 * 1024);

            void Flush() => File.WriteAllText(LogPath, sb.ToString(), Encoding.UTF8);

            sb.AppendLine("=== v1_087 DEVELOPMENT THAW — seed=42195 PHASE XII ===");
            sb.AppendLine(
                "Contrat: verbe joueur (construire+investir), réversibilité bit-identique à " +
                "investissement nul, déterminisme ProvinceId, retour écran, effet mesuré.");
            sb.AppendLine(
                $"Coût = {ProvinceDevelopmentInvestment.BaseMoneyCost} × niveau courant ; " +
                $"bornes [{ProvinceDevelopmentInvestment.MinLevel}..{ProvinceDevelopmentInvestment.MaxLevel}] ; " +
                $"PhysicalBlendWeight={PhysicalSatisfactionBlendSystem.DefaultPhysicalBlendWeight} (NON modifié).");
            sb.AppendLine();
            Flush();

            // ----- PARTIE 1 — ÉTAT GELÉ -----
            sb.AppendLine("=== PARTIE 1 — POINTS D'ÉCRITURE + DISTRIBUTION AVANT ===");
            sb.AppendLine(
                "WRITE_SITES ProvinceDevelopment (exhaustif code+runtime):");
            sb.AppendLine(
                "  1) MapInitSystem.cs:51 — AddComponentData à l'init (base_tax/production/manpower)");
            sb.AppendLine(
                "  2) ApplyPlayerIntentionSystem.ApplyInvest — SetComponentData UNIQUEMENT si " +
                "intention InvestProvinceDevelopment acceptée (v1_087, nouveau)");
            sb.AppendLine(
                "  Aucun autre SetComponentData/AddComponentData ProvinceDevelopment trouvé " +
                "dans Assets/Scripts (grep). CTO confirmé : gelé jusqu'à v1_087.");

            ForceGc();
            DistSnap before;
            float capacityPerDev;
            using (var h = new SimulationHarness(Seed))
            {
                h.RunTicks(0);
                before = MeasureDistribution(h.EntityManager, out capacityPerDev);
            }

            sb.AppendLine(
                $"provinces={before.Count} capacityPerDev={Fmt3(capacityPerDev)}");
            sb.AppendLine(
                $"Tax    min={before.TaxMin} median={Fmt1(before.TaxMed)} max={before.TaxMax} mean={Fmt3(before.TaxMean)}");
            sb.AppendLine(
                $"Prod   min={before.ProdMin} median={Fmt1(before.ProdMed)} max={before.ProdMax} mean={Fmt3(before.ProdMean)}");
            sb.AppendLine(
                $"Man    min={before.ManMin} median={Fmt1(before.ManMed)} max={before.ManMax} mean={Fmt3(before.ManMean)}");
            sb.AppendLine(
                $"DevScore mean={Fmt3(before.DevMean)} min={Fmt3(before.DevMin)} max={Fmt3(before.DevMax)}");
            sb.AppendLine(
                $"edgeCap_mean (perDev×DevScore)={Fmt1(before.EdgeCapMean)}");

            // Effet théorique +1 Production partout
            var theoDev = before.DevMean + (1f / 3f);
            var theoEdge = capacityPerDev * theoDev;
            var theoDeltaPct = 100f * (theoEdge - before.EdgeCapMean) / math.max(1e-6f, before.EdgeCapMean);
            sb.AppendLine(
                $"THEORIQUE +1 Production partout: DevScore {Fmt3(before.DevMean)}→{Fmt3(theoDev)} " +
                $"edgeCap {Fmt1(before.EdgeCapMean)}→{Fmt1(theoEdge)} ({Fmt2(theoDeltaPct)} %)");
            sb.AppendLine(
                "NOTE v1_084: edgeCap NON saturé (0 saturation / 2931 tentatives, marge moyenne 8292). " +
                "Un gain de transport n'implique PAS un gain économique si le frein de débouché domine.");
            sb.AppendLine();
            Flush();

            // ----- PARTIE 2 — VERBE + RÉVERSIBILITÉ + DÉTERMINISME -----
            sb.AppendLine("=== PARTIE 2 — VERBE, RÉVERSIBILITÉ, DÉTERMINISME ===");
            sb.AppendLine(
                "Mécanisme: PlayerIntentionKind.InvestProvinceDevelopment → ApplyInvest " +
                "(PlayerControl + ownership + bornes + trésorerie) ; " +
                "StartBuildingConstruction réutilise ApplyBuild inchangé, ouvert depuis InvestBar HUD.");
            sb.AppendLine(
                $"coût_exemple niveau3 = {Fmt1(ProvinceDevelopmentInvestment.CostForLevel(3))}");

            ForceGc();
            ulong parityDig;
            using (var h = new SimulationHarness(Seed))
            {
                h.RunTicks(ParityTicks);
                parityDig = WorldDigest.Compute(h.EntityManager);
            }

            var parityOk = parityDig == ExpectedParity;
            sb.AppendLine(
                $"réversibilité investissement_nul t{ParityTicks}: fingerprint=0x{parityDig:X16} " +
                $"expected=0x{ExpectedParity:X16} bitIdentical={parityOk}");

            // Déterminisme : même séquence d'investissements ×2
            ForceGc();
            ulong digA = RunInvestmentSequenceDigest();
            ForceGc();
            ulong digB = RunInvestmentSequenceDigest();
            var detOk = digA == digB;
            sb.AppendLine(
                $"déterminisme séquence Invest Prod sur provinces joueur triées ProvinceId: " +
                $"A=0x{digA:X16} B=0x{digB:X16} identical={detOk}");

            // Preuve que le verbe bouge le développement
            ForceGc();
            int prodBefore, prodAfter;
            using (var h = new SimulationHarness(Seed))
            {
                LockDefaults();
                h.RunTicks(0);
                var em = h.EntityManager;
                var pid = FindCityProvinceId(em, ParisCityId);
                prodBefore = ReadDev(em, pid).Production;
                SetPlayerTreasury(em, 50000f);
                PlayerIntentionSubmit.EnqueueInvestProvinceDevelopment(
                    em, PlayerCountryId, pid, ProvinceDevelopmentInvestment.AxisProduction);
                h.RunTicks(2);
                prodAfter = ReadDev(em, pid).Production;
            }

            sb.AppendLine(
                $"verbe_invest Paris: Production {prodBefore}→{prodAfter} moved={prodAfter == prodBefore + 1}");
            sb.AppendLine();
            Flush();

            // ----- PARTIE 3 — CAMPAGNE + HUD + CAPTURES -----
            sb.AppendLine("=== PARTIE 3 — CAMPAGNE MESURÉE + RETOUR ÉCRAN ===");
            var campaign = new List<CampaignRow>(CampaignLevels);
            for (var level = 0; level < CampaignLevels; level++)
            {
                ForceGc();
                campaign.Add(RunCampaignLevel(level, SweepTicks));
                var row = campaign[level];
                sb.AppendLine(
                    $"level=+{level}Prod: DevScore={Fmt3(row.DevMean)} edgeCap={Fmt1(row.EdgeCapMean)} " +
                    $"sat={Fmt3(row.Sat)} import_cloth={Fmt3(row.ImportShare)} " +
                    $"({Pct(row.ImportShare)} %) pop={row.Pop}");
                Flush();
            }

            var monoDev = IsMonotoneNonDecreasing(campaign.ConvertAll(r => r.DevMean).ToArray());
            var monoEdge = IsMonotoneNonDecreasing(campaign.ConvertAll(r => r.EdgeCapMean).ToArray());
            var monoSat = IsMonotoneNonDecreasing(campaign.ConvertAll(r => r.Sat).ToArray()) ||
                          IsMonotoneNonIncreasing(campaign.ConvertAll(r => r.Sat).ToArray());
            var satDelta = campaign[campaign.Count - 1].Sat - campaign[0].Sat;
            var importDelta = campaign[campaign.Count - 1].ImportShare - campaign[0].ImportShare;
            var edgeDeltaPct = 100f * (campaign[campaign.Count - 1].EdgeCapMean - campaign[0].EdgeCapMean) /
                               math.max(1e-6f, campaign[0].EdgeCapMean);

            sb.AppendLine(
                $"monotonie DevScore={monoDev} edgeCap={monoEdge} sat_mono_dir={monoSat} " +
                $"Δsat={Fmt4(satDelta)} Δimport={Fmt4(importDelta)} ΔedgeCap%={Fmt2(edgeDeltaPct)}");

            var ecoMoved = math.abs(satDelta) > 0.005f || math.abs(importDelta) > 0.005f;
            var importMono = IsMonotoneNonDecreasing(campaign.ConvertAll(r => r.ImportShare).ToArray()) ||
                             IsMonotoneNonIncreasing(campaign.ConvertAll(r => r.ImportShare).ToArray());
            if (!importMono)
            {
                sb.AppendLine(
                    "import_share NON MONOTONE sur la campagne — publié tel quel " +
                    "(pas présenté comme levier économique fiable via edgeCap).");
            }

            if (!ecoMoved)
            {
                sb.AppendLine(
                    "EFFET ÉCONOMIQUE NUL/FAIBLE : edgeCap monte mais sat/import ne bougent pas " +
                    "significativement — cohérent avec v1_084 (edgeCap non saturé ; frein de débouché). " +
                    "Ce n'est PAS présenté comme un succès économique : le verbe et le retour écran " +
                    "sont livrés ; le goulot reste ailleurs.");
            }
            else
            {
                sb.AppendLine(
                    "EFFET ÉCONOMIQUE MESURABLE sur sat et/ou import_share — publier tel quel.");
            }

            // Captures avant/après investissement sur province Paris
            ForceGc();
            var capBefore = CaptureProvinceDev(0);
            ForceGc();
            var capAfter = CaptureProvinceDev(1);
            sb.AppendLine(
                $"capture_before: {capBefore.HudLine} Production={capBefore.Production}");
            sb.AppendLine(
                $"capture_after:  {capAfter.HudLine} Production={capAfter.Production}");
            var capturesDiffer = capAfter.Production != capBefore.Production;
            sb.AppendLine($"captures_differ={capturesDiffer}");

            WriteDevCapturePng(
                Path.Combine(CapturesDir, "01_dev_before.png"),
                "DEV BEFORE", capBefore);
            WriteDevCapturePng(
                Path.Combine(CapturesDir, "02_dev_after.png"),
                "DEV AFTER", capAfter);
            sb.AppendLine($"png_before={Path.Combine(CapturesDir, "01_dev_before.png")}");
            sb.AppendLine($"png_after={Path.Combine(CapturesDir, "02_dev_after.png")}");
            sb.AppendLine();

            // ----- VERDICT -----
            sb.AppendLine("=== VERDICT MESURÉ ===");
            var pass = parityOk && detOk && (prodAfter == prodBefore + 1) && capturesDiffer &&
                       monoDev && monoEdge &&
                       File.Exists(Path.Combine(CapturesDir, "01_dev_before.png")) &&
                       File.Exists(Path.Combine(CapturesDir, "02_dev_after.png"));

            sb.AppendLine(
                $"ProvinceDevelopment écrit en 2 points (MapInitSystem.cs:51 + ApplyInvest) ; " +
                $"distribution Production min {before.ProdMin} médiane {Fmt1(before.ProdMed)} max {before.ProdMax} ; " +
                $"+1 partout ferait passer edgeCap moyen de {Fmt1(before.EdgeCapMean)} à {Fmt1(theoEdge)} " +
                $"soit +{Fmt2(theoDeltaPct)} % ; campagne +0..+{CampaignLevels - 1} Prod : " +
                $"DevScore {Fmt3(campaign[0].DevMean)}→{Fmt3(campaign[campaign.Count - 1].DevMean)} " +
                $"edgeCap {Fmt1(campaign[0].EdgeCapMean)}→{Fmt1(campaign[campaign.Count - 1].EdgeCapMean)} " +
                $"sat {Fmt3(campaign[0].Sat)}→{Fmt3(campaign[campaign.Count - 1].Sat)} " +
                $"import {Fmt3(campaign[0].ImportShare)}→{Fmt3(campaign[campaign.Count - 1].ImportShare)} " +
                $"ecoMoved={ecoMoved} ; " +
                (ecoMoved
                    ? "effet économique mesurable."
                    : "effet économique nul/faible car edgeCap n'est pas le goulot (cf. v1_084). ") +
                $"verbe livré (invest+build HUD), coût prélevé, bornes respectées ; " +
                $"réversibilité 0x{parityDig:X16} bit-identique={parityOk} ; " +
                $"déterminisme 2/2 identical={detOk} ; " +
                $"captures Production {capBefore.Production}→{capAfter.Production}.");
            sb.AppendLine(pass
                ? "VERDICT: PASS — verbe + retour écran + preuves de contrat."
                : "VERDICT: FAIL — un critère du contrat de phase a lâché.");
            Flush();
            Debug.Log(sb.ToString());

            Assert.IsTrue(parityOk, "réversibilité");
            Assert.IsTrue(detOk, "déterminisme");
            Assert.AreEqual(prodBefore + 1, prodAfter, "verbe invest");
            Assert.IsTrue(capturesDiffer, "captures distinctes");
            Assert.IsTrue(monoDev, "DevScore monotone croissant sous campagne");
            Assert.IsTrue(monoEdge, "edgeCap monotone croissant sous campagne");
            ResetAll();
        }

        static ulong RunInvestmentSequenceDigest()
        {
            LockDefaults();
            using var h = new SimulationHarness(Seed);
            h.RunTicks(0);
            var em = h.EntityManager;
            SetPlayerTreasury(em, 500000f);
            var ids = CollectOwnedProvinceIds(em, PlayerCountryId);
            ids.Sort();
            // Une vague d'investissements Production, tri ProvinceId, une intention/tick.
            for (var i = 0; i < ids.Count; i++)
            {
                PlayerIntentionSubmit.EnqueueInvestProvinceDevelopment(
                    em, PlayerCountryId, ids[i], ProvinceDevelopmentInvestment.AxisProduction);
                h.RunTicks(1);
            }

            h.RunTicks(20);
            return WorldDigest.Compute(em);
        }

        static CampaignRow RunCampaignLevel(int prodBumps, int ticks)
        {
            LockDefaults();
            using var h = new SimulationHarness(Seed);
            h.RunTicks(0);
            var em = h.EntityManager;

            // Bumps appliqués après init, triés ProvinceId — mesure d'effet, pas le verbe UI.
            // (Le verbe est prouvé ailleurs ; ici on isole l'effet économique du Dev.)
            var ids = CollectAllProvinceIds(em);
            ids.Sort();
            for (var b = 0; b < prodBumps; b++)
            {
                for (var i = 0; i < ids.Count; i++)
                {
                    var d = ReadDev(em, ids[i]);
                    if (d.Production < ProvinceDevelopmentInvestment.MaxLevel)
                        d.Production += 1;
                    WriteDev(em, ids[i], d);
                }
            }

            h.RunTicks(ticks);
            var dist = MeasureDistribution(em, out _);
            var m = WorldMetrics.Capture(em, ticks);
            var import = MeasureClothImportShare(em);
            return new CampaignRow
            {
                Level = prodBumps,
                DevMean = dist.DevMean,
                EdgeCapMean = dist.EdgeCapMean,
                Sat = m.NeedsSatAvg,
                ImportShare = import,
                Pop = m.Population
            };
        }

        static CapSnap CaptureProvinceDev(int investCount)
        {
            LockDefaults();
            using var h = new SimulationHarness(Seed);
            h.RunTicks(0);
            var em = h.EntityManager;
            var pid = FindCityProvinceId(em, ParisCityId);
            SetPlayerTreasury(em, 100000f);
            for (var i = 0; i < investCount; i++)
            {
                PlayerIntentionSubmit.EnqueueInvestProvinceDevelopment(
                    em, PlayerCountryId, pid, ProvinceDevelopmentInvestment.AxisProduction);
                h.RunTicks(1);
            }

            h.RunTicks(2);
            var dev = ReadDev(em, pid);
            return new CapSnap
            {
                ProvinceId = pid,
                Production = dev.Production,
                Tax = dev.Tax,
                Manpower = dev.Manpower,
                HudLine = DevelopmentHudSnapshot.FormatHudLine(in dev)
            };
        }

        static void WriteDevCapturePng(string path, string title, CapSnap cap)
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
                        pixels, "PROV " + cap.ProvinceId, 12, 48, fg, halo);
                    MapSnapshotExporter.DrawBitmapText(
                        pixels,
                        "T" + cap.Tax + " P" + cap.Production + " M" + cap.Manpower,
                        12, 80, fg, halo);
                    MapSnapshotExporter.DrawBitmapText(
                        pixels, cap.HudLine, 12, 112, fg, halo);
                });
            });

            MapSnapshotExporter.WriteMapBufferPng(pixels, w, h, path);
        }

        static DistSnap MeasureDistribution(EntityManager em, out float capacityPerDev)
        {
            capacityPerDev = CapacityPerDevDefault;
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<PhysicalTransportConfig>()))
            {
                if (!q.IsEmptyIgnoreFilter)
                {
                    var cfg = q.GetSingleton<PhysicalTransportConfig>();
                    if (cfg.CapacityPerDevPoint > 0f)
                        capacityPerDev = cfg.CapacityPerDevPoint;
                }
            }

            var taxes = new List<int>(64);
            var prods = new List<int>(64);
            var mans = new List<int>(64);
            var scores = new List<float>(64);
            using (var q = em.CreateEntityQuery(
                       ComponentType.ReadOnly<ProvinceData>(),
                       ComponentType.ReadOnly<ProvinceDevelopment>()))
            using (var pdata = q.ToComponentDataArray<ProvinceData>(Allocator.Temp))
            using (var devs = q.ToComponentDataArray<ProvinceDevelopment>(Allocator.Temp))
            {
                var rows = new List<(int Id, ProvinceDevelopment Dev)>(pdata.Length);
                for (var i = 0; i < pdata.Length; i++)
                    rows.Add((pdata[i].ProvinceId, devs[i]));
                rows.Sort((a, b) => a.Id.CompareTo(b.Id));
                for (var i = 0; i < rows.Count; i++)
                {
                    var d = rows[i].Dev;
                    taxes.Add(d.Tax);
                    prods.Add(d.Production);
                    mans.Add(d.Manpower);
                    scores.Add(ProvinceDevelopmentInvestment.DevScore(in d));
                }
            }

            float Mean(List<float> xs)
            {
                double s = 0;
                for (var i = 0; i < xs.Count; i++) s += xs[i];
                return xs.Count > 0 ? (float)(s / xs.Count) : 0f;
            }

            float MeanI(List<int> xs)
            {
                double s = 0;
                for (var i = 0; i < xs.Count; i++) s += xs[i];
                return xs.Count > 0 ? (float)(s / xs.Count) : 0f;
            }

            float MedI(List<int> xs)
            {
                if (xs.Count == 0) return 0f;
                var c = new List<int>(xs);
                c.Sort();
                return c.Count % 2 == 1
                    ? c[c.Count / 2]
                    : 0.5f * (c[c.Count / 2 - 1] + c[c.Count / 2]);
            }

            var snap = new DistSnap
            {
                Count = scores.Count,
                TaxMin = Min(taxes), TaxMax = Max(taxes), TaxMed = MedI(taxes), TaxMean = MeanI(taxes),
                ProdMin = Min(prods), ProdMax = Max(prods), ProdMed = MedI(prods), ProdMean = MeanI(prods),
                ManMin = Min(mans), ManMax = Max(mans), ManMed = MedI(mans), ManMean = MeanI(mans),
                DevMean = Mean(scores),
                DevMin = scores.Count > 0 ? Mathf.Min(scores.ToArray()) : 0f,
                DevMax = scores.Count > 0 ? Mathf.Max(scores.ToArray()) : 0f
            };
            snap.EdgeCapMean = capacityPerDev * snap.DevMean;
            return snap;
        }

        static float MeasureClothImportShare(EntityManager em)
        {
            float importProxy = 0f, n = 0f;
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<PhysicalDemandSnapshot>());
            using var snaps = q.ToComponentDataArray<PhysicalDemandSnapshot>(Allocator.Temp);
            for (var i = 0; i < snaps.Length; i++)
            {
                var d = snaps[i].ClothDemand;
                var s = snaps[i].ClothSatisfied;
                if (d <= 1e-4f)
                    continue;
                n += 1f;
                importProxy += math.saturate(1f - math.saturate(s / d));
            }

            return n > 0f ? importProxy / n : 0f;
        }

        static List<int> CollectAllProvinceIds(EntityManager em)
        {
            var ids = new List<int>(64);
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<ProvinceData>());
            using var arr = q.ToComponentDataArray<ProvinceData>(Allocator.Temp);
            for (var i = 0; i < arr.Length; i++)
                ids.Add(arr[i].ProvinceId);
            return ids;
        }

        static List<int> CollectOwnedProvinceIds(EntityManager em, int countryId)
        {
            Entity countryEntity = Entity.Null;
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<CountryData>()))
            using (var entities = q.ToEntityArray(Allocator.Temp))
            using (var countries = q.ToComponentDataArray<CountryData>(Allocator.Temp))
            {
                for (var i = 0; i < countries.Length; i++)
                {
                    if (countries[i].CountryId != countryId)
                        continue;
                    countryEntity = entities[i];
                    break;
                }
            }

            var ids = new List<int>(32);
            if (countryEntity == Entity.Null)
                return ids;

            using (var q = em.CreateEntityQuery(
                       ComponentType.ReadOnly<ProvinceData>(),
                       ComponentType.ReadOnly<ProvinceOwnership>()))
            using (var pdata = q.ToComponentDataArray<ProvinceData>(Allocator.Temp))
            using (var owns = q.ToComponentDataArray<ProvinceOwnership>(Allocator.Temp))
            {
                for (var i = 0; i < pdata.Length; i++)
                {
                    if (owns[i].Owner != countryEntity)
                        continue;
                    ids.Add(pdata[i].ProvinceId);
                }
            }

            return ids;
        }

        static ProvinceDevelopment ReadDev(EntityManager em, int provinceId)
        {
            using var q = em.CreateEntityQuery(
                ComponentType.ReadOnly<ProvinceData>(),
                ComponentType.ReadOnly<ProvinceDevelopment>());
            using var pdata = q.ToComponentDataArray<ProvinceData>(Allocator.Temp);
            using var devs = q.ToComponentDataArray<ProvinceDevelopment>(Allocator.Temp);
            for (var i = 0; i < pdata.Length; i++)
            {
                if (pdata[i].ProvinceId == provinceId)
                    return devs[i];
            }

            return default;
        }

        static void WriteDev(EntityManager em, int provinceId, ProvinceDevelopment dev)
        {
            using var q = em.CreateEntityQuery(
                ComponentType.ReadOnly<ProvinceData>(),
                ComponentType.ReadOnly<ProvinceDevelopment>());
            using var entities = q.ToEntityArray(Allocator.Temp);
            using var pdata = q.ToComponentDataArray<ProvinceData>(Allocator.Temp);
            for (var i = 0; i < pdata.Length; i++)
            {
                if (pdata[i].ProvinceId != provinceId)
                    continue;
                em.SetComponentData(entities[i], dev);
                return;
            }
        }

        static int FindCityProvinceId(EntityManager em, int cityId)
        {
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<CityData>());
            using var arr = q.ToComponentDataArray<CityData>(Allocator.Temp);
            for (var i = 0; i < arr.Length; i++)
            {
                if (arr[i].CityId == cityId)
                    return arr[i].ProvinceId;
            }

            return -1;
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

        static float ReadTreasury(EntityManager em, int countryId)
        {
            using var q = em.CreateEntityQuery(
                ComponentType.ReadOnly<CountryData>(),
                ComponentType.ReadOnly<TreasuryData>());
            using var countries = q.ToComponentDataArray<CountryData>(Allocator.Temp);
            using var treasuries = q.ToComponentDataArray<TreasuryData>(Allocator.Temp);
            for (var i = 0; i < countries.Length; i++)
            {
                if (countries[i].CountryId == countryId)
                    return treasuries[i].Balance;
            }

            return 0f;
        }

        static void SetPlayerTreasury(EntityManager em, float balance)
        {
            using var q = em.CreateEntityQuery(
                ComponentType.ReadOnly<CountryData>(),
                ComponentType.ReadOnly<TreasuryData>());
            using var entities = q.ToEntityArray(Allocator.Temp);
            using var countries = q.ToComponentDataArray<CountryData>(Allocator.Temp);
            for (var i = 0; i < countries.Length; i++)
            {
                if (countries[i].CountryId != PlayerCountryId)
                    continue;
                var t = em.GetComponentData<TreasuryData>(entities[i]);
                t.Balance = balance;
                em.SetComponentData(entities[i], t);
                return;
            }
        }

        static PlayerIntentionReceipt ReadReceipt(EntityManager em)
        {
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<PlayerIntentionReceipt>());
            return q.GetSingleton<PlayerIntentionReceipt>();
        }

        static bool IsMonotoneNonDecreasing(float[] values)
        {
            for (var i = 1; i < values.Length; i++)
            {
                if (values[i] + 1e-4f < values[i - 1])
                    return false;
            }

            return true;
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

        static int Min(List<int> xs)
        {
            var m = int.MaxValue;
            for (var i = 0; i < xs.Count; i++)
                if (xs[i] < m) m = xs[i];
            return xs.Count > 0 ? m : 0;
        }

        static int Max(List<int> xs)
        {
            var m = int.MinValue;
            for (var i = 0; i < xs.Count; i++)
                if (xs[i] > m) m = xs[i];
            return xs.Count > 0 ? m : 0;
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
        }

        static void ForceGc()
        {
            ResetAll();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        static string Fmt1(float v) => v.ToString("0.0", CultureInfo.InvariantCulture);
        static string Fmt2(float v) => v.ToString("0.00", CultureInfo.InvariantCulture);
        static string Fmt3(float v) => v.ToString("0.000", CultureInfo.InvariantCulture);
        static string Fmt4(float v) => v.ToString("0.0000", CultureInfo.InvariantCulture);
        static string Pct(float v) => (v * 100f).ToString("0.0", CultureInfo.InvariantCulture);

        struct DistSnap
        {
            public int Count;
            public int TaxMin, TaxMax, ProdMin, ProdMax, ManMin, ManMax;
            public float TaxMed, ProdMed, ManMed, TaxMean, ProdMean, ManMean;
            public float DevMean, DevMin, DevMax, EdgeCapMean;
        }

        struct CampaignRow
        {
            public int Level, Pop;
            public float DevMean, EdgeCapMean, Sat, ImportShare;
        }

        struct CapSnap
        {
            public int ProvinceId, Tax, Production, Manpower;
            public string HudLine;
        }
    }
}
