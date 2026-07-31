using System;
using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using VictoriaGame.Core;
using VictoriaGame.Economy;
using VictoriaGame.World;
using Debug = UnityEngine.Debug;

namespace VictoriaGame.Tests
{
    /// <summary>
    /// Point d'entrée batchmode :
    /// -executeMethod VictoriaGame.Tests.V1084DrapBalanceBatchRunner.Run
    /// </summary>
    public static class V1084DrapBalanceBatchRunner
    {
        public static void Run()
        {
            V1084DrapBalanceTests.RunAndWriteArtifacts();
            Debug.Log("V1084DrapBalanceBatchRunner: DONE");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
        }
    }

    /// <summary>
    /// v1_084 — unités de la chaîne drap v1_083 + bilan qui boucle + dette LARGE.
    /// MESURE uniquement : ne modifie ni PhysicalBlendWeight ni CitySeedCoefficient.
    /// </summary>
    [TestFixture]
    public class V1084DrapBalanceTests
    {
        const uint Seed = 42195u;
        const int ClothTicks = 300;
        const int DeterminismTicks = 100;
        const ulong ExpectedParity = ParityAnchors.Expected;
        const int ClothGoodId = PhysicalStockSystem.ClothGoodId;
        const float PerCaseBudgetS = 2.65f;

        static string GameUnityRoot =>
            Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        static string LogPath =>
            Path.Combine(GameUnityRoot, "Logs", "v1_084_drap.log");

        [TearDown]
        public void TearDown()
        {
            BuildingInitSystem.UnlockCitySeedCoefficient();
            BuildingConstructionSystem.UnlockCapacityIntensity();
            PhysicalSatisfactionBlendSystem.UnlockWeight();
            PhysicalProductionSystem.UnlockOutletCap();
            PhysicalStockSystem.IdealPoolMode = false;
            PhysicalStockSystem.MultiHopTransport = true;
            PhysicalStockSystem.ServiceOrderMode =
                PhysicalStockSystem.TransportServiceOrder.ByDeficitSeverity;
            PhysicalStockSystem.RecordTransportShares = false;
            PhysicalStockSystem.RecordClothBalance = false;
            PhysicalStockSystem.ClearTransportShareCounters();
            PhysicalStockSystem.ClearClothBalanceCounters();
        }

        [Test]
        public void V1084_A_DefaultCitySeedCoefficient_IsZero()
        {
            Assert.AreEqual(0f, BuildingInitSystem.DefaultCitySeedCoefficient, 1e-8f);
            Assert.AreEqual(0f, BuildingInitSystem.CitySeedCoefficient, 1e-8f);
        }

        [Test]
        public void V1084_B_ParityFingerprint_Unchanged()
        {
            BuildingInitSystem.LockCitySeedCoefficient(0f);
            ulong digest;
            using (var h = new SimulationHarness(Seed))
            {
                h.RunTicks(DeterminismTicks);
                digest = WorldDigest.Compute(h.EntityManager);
            }

            Assert.AreEqual(ExpectedParity, digest,
                "monde inchangé — empreinte v1_009 / v1_083");
        }

        [Test]
        public void V1084_Artifacts_And_Verdict() => RunAndWriteArtifacts();

        public static void RunAndWriteArtifacts()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            var sb = new StringBuilder(128 * 1024);
            sb.AppendLine("=== v1_084 — BILAN DRAP (unités + conservation) ===");
            sb.AppendLine("seed=" + Seed);
            sb.AppendLine("cloth_ticks=" + ClothTicks);
            sb.AppendLine("DefaultCitySeedCoefficient=" +
                          BuildingInitSystem.DefaultCitySeedCoefficient.ToString(
                              "0.###", CultureInfo.InvariantCulture));
            sb.AppendLine("PhysicalBlendWeight_default=" +
                          PhysicalSatisfactionBlendSystem.DefaultPhysicalBlendWeight.ToString(
                              "0.###", CultureInfo.InvariantCulture));
            sb.AppendLine("OutletCapIntensity_default=" +
                          PhysicalProductionSystem.DefaultOutletCapIntensity.ToString(
                              "0.###", CultureInfo.InvariantCulture));
            sb.AppendLine("CapacityIntensity_default=" +
                          BuildingConstructionSystem.DefaultCapacityIntensity.ToString(
                              "0.###", CultureInfo.InvariantCulture));
            sb.AppendLine("ServiceOrderMode=" + PhysicalStockSystem.ServiceOrderMode);
            sb.AppendLine();

            // ── PARTIE 1 — UNITÉS ──────────────────────────────────────────
            sb.AppendLine("=== PARTIE 1 — UNITÉS ET PORTÉE (chaîne v1_083) ===");
            sb.AppendLine(
                "SOURCE CODE v1_083 Snapshot: drap_out = sum(ProductionSite.LastOutput " +
                "où GoodTag=cloth) + workshopCap × CapacityIntensity ; " +
                "drap_demand/satisfied = sum(PhysicalDemandSnapshot.Cloth*) ; " +
                "ticks=ClothTicks=300 ; PhysicalBlendWeight verrouillé à 0 pendant la mesure.");
            sb.AppendLine(
                "LastOutput = BaseOutput×Efficiency×laborFactor (ProductionSystem) — " +
                "FLUX DU TICK, pas un cumul.");
            sb.AppendLine(
                "PhysicalDemandSnapshot écrit dans PhysicalStockSystem.ConsumeLocal " +
                "APRÈS livraison des cargaisons du tick — FLUX DU TICK, monde entier.");
            sb.AppendLine(
                "ATTENTION: drap_out ≠ production physique déposée. " +
                "PhysicalProductionSystem desired = lerp(lodOut, buildingCap, CapacityIntensity) ; " +
                "à CapacityIntensity=1 → desired=buildingCap, puis plafonds intrants/débouchés.");

            BuildingInitSystem.LockCitySeedCoefficient(0f);
            PhysicalSatisfactionBlendSystem.LockWeight(0f);
            PhysicalStockSystem.MultiHopTransport = true;
            PhysicalStockSystem.RecordClothBalance = true;
            PhysicalStockSystem.RecordTransportShares = true;

            double prodBefore;
            var tickSnap = default(PhysicalStockSystem.ClothBalanceSample);
            double shippedWindow = 0, deliveredWindow = 0, consumedIdWindow = 0;
            double prodWindow = 0, consWindow = 0;

            using (var h = new SimulationHarness(Seed))
            {
                h.RunTicks(ClothTicks - 1);
                var pre = PhysicalStockSystem.SampleClothBalance(h.EntityManager);
                prodBefore = pre.CumulativeProduction;
                var consBefore = pre.CumulativeConsumption;

                PhysicalStockSystem.ClearTransportShareCounters();
                h.RunTicks(1);
                tickSnap = PhysicalStockSystem.SampleClothBalance(h.EntityManager);
                shippedWindow = tickSnap.LastTickShipped;
                deliveredWindow = tickSnap.LastTickDelivered;
                consumedIdWindow = tickSnap.LastTickConsumedId;
                prodWindow = tickSnap.CumulativeProduction - prodBefore;
                consWindow = tickSnap.CumulativeConsumption - consBefore;
            }

            var v1083Out = tickSnap.LodClothOutProxy;
            var v1083Demand = tickSnap.ClothDemand;
            var v1083Sat = tickSnap.ClothSatisfied;
            var ratioProxy = v1083Demand > 1e-6f ? v1083Out / v1083Demand : 0f;
            var ratioSat = v1083Demand > 1e-6f ? v1083Sat / v1083Demand : 0f;
            var ratioPhys = v1083Demand > 1e-6f ? (float)(prodWindow / v1083Demand) : 0f;

            sb.AppendLine("--- mesure tick=" + ClothTicks + " (monde entier) ---");
            sb.AppendLine("drap_out_v1083_proxy=" + Fmt1(v1083Out) +
                          " UNITÉ=flux/tick PORTÉE=monde NATURE=proxy_LOD+workshopCap " +
                          "(PAS ledger)");
            sb.AppendLine("drap_demand=" + Fmt1(v1083Demand) +
                          " UNITÉ=flux/tick PORTÉE=monde NATURE=PhysicalDemandSnapshot " +
                          "APRÈS livraison cargaisons");
            sb.AppendLine("drap_satisfied=" + Fmt1(v1083Sat) +
                          " UNITÉ=flux/tick PORTÉE=monde NATURE=conso physique " +
                          "GoodType.Manufactured contre ClothNeed");
            sb.AppendLine("drap_phys_produced_tick=" + Fmt1(prodWindow) +
                          " UNITÉ=flux/tick PORTÉE=monde NATURE=Δ ledger CumulativeProduction " +
                          "GoodId=8");
            sb.AppendLine("drap_phys_consumed_tick=" + Fmt1(consWindow) +
                          " UNITÉ=flux/tick PORTÉE=monde NATURE=Δ ledger CumulativeConsumption " +
                          "GoodId=8");
            sb.AppendLine("workshop_cap=" + Fmt1(tickSnap.WorkshopCapCloth) +
                          " capacity_intensity=" +
                          BuildingConstructionSystem.CapacityIntensity.ToString(
                              "0.###", CultureInfo.InvariantCulture));
            sb.AppendLine("ratio_proxy_out_over_demand=" + Fmt(ratioProxy) +
                          " (v1_083 annonçait ~4.32)");
            sb.AppendLine("ratio_phys_prod_over_demand=" + Fmt(ratioPhys));
            sb.AppendLine("ratio_satisfied_over_demand=" + Fmt(ratioSat) +
                          " (v1_083 = 0.280 ; questions ouvertes ~0.29)");

            string unitsVerdict;
            bool abundanceReal;
            if (math.abs(ratioProxy - 4.32f) < 0.5f && ratioPhys < 0.8f)
            {
                abundanceReal = false;
                unitsVerdict =
                    "UNITÉS COMPARABLES (toutes flux/tick monde) MAIS drap_out ÉTAIT UN PROXY " +
                    "DE CAPACITÉ DÉSIRÉE, PAS LA PRODUCTION DÉPOSÉE. Rapport proxy/demande≈" +
                    Fmt(ratioProxy) + " ; production physique/demande≈" + Fmt(ratioPhys) +
                    ". Le « 4,3× d'abondance » NE SURVIT PAS. " +
                    "ratio satisfied/demand=" + Fmt(ratioSat) +
                    " confirme le plafond ~0,28–0,29 — c'est un plafond de LIVRAISON/" +
                    "ABSORPTION physique, pas la preuve d'une production 4× trop haute.";
            }
            else if (ratioPhys >= 2f && ratioSat < 0.4f)
            {
                abundanceReal = true;
                unitsVerdict =
                    "UNITÉS COMPARABLES et production physique réellement abondante " +
                    "(phys/demande=" + Fmt(ratioPhys) + ") ; satisfied/demand=" +
                    Fmt(ratioSat) + " = plafond livraison.";
            }
            else
            {
                abundanceReal = ratioPhys >= 1.2f;
                unitsVerdict =
                    "UNITÉS = flux/tick monde pour les trois. proxy/demande=" +
                    Fmt(ratioProxy) + " phys/demande=" + Fmt(ratioPhys) +
                    " sat/demande=" + Fmt(ratioSat) +
                    ". Abondance réelle=" + (abundanceReal ? "OUI" : "NON") + ".";
            }

            sb.AppendLine("units_verdict=" + unitsVerdict);
            sb.AppendLine("ceiling_0.29_confirmed=" +
                          (ratioSat > 0.20f && ratioSat < 0.35f ? "YES" : "NO"));
            sb.AppendLine();

            // ── PARTIE 2 — BILAN QUI BOUCLE ───────────────────────────────
            sb.AppendLine("=== PARTIE 2 — BILAN COMPLET DU DRAP ===");

            // Cumul 0→300 + tick 300 detail
            double cumProd, cumCons, stockEnd, transitEnd;
            double shippedCum = 0, deliveredCum = 0, consumedIdCum = 0;
            float missOutlet = 0, missInput = 0;
            int capAttempts = 0, capExhausted = 0;
            double capRoomSum = 0;
            float demandCum = 0, satCum = 0;

            using (var h = new SimulationHarness(Seed))
            {
                PhysicalStockSystem.RecordClothBalance = true;
                PhysicalStockSystem.RecordTransportShares = true;
                PhysicalSatisfactionBlendSystem.LockWeight(0f);
                BuildingInitSystem.LockCitySeedCoefficient(0f);

                h.RunTicks(0);
                var t0 = PhysicalStockSystem.SampleClothBalance(h.EntityManager);
                var stock0 = t0.Stock;
                var transit0 = t0.Transit;
                var prod0 = t0.CumulativeProduction;
                var cons0 = t0.CumulativeConsumption;

                for (var t = 0; t < ClothTicks; t++)
                {
                    PhysicalStockSystem.ClearTransportShareCounters();
                    h.RunTicks(1);
                    var s = PhysicalStockSystem.SampleClothBalance(h.EntityManager);
                    shippedCum += s.LastTickShipped;
                    deliveredCum += s.LastTickDelivered;
                    consumedIdCum += s.LastTickConsumedId;
                    demandCum += s.ClothDemand;
                    satCum += s.ClothSatisfied;
                    if (t == ClothTicks - 1)
                    {
                        missOutlet = s.MissedOutletShare;
                        missInput = s.MissedInputShare;
                        capAttempts = s.CapRoomAttempts;
                        capExhausted = s.CapExhaustedAttempts;
                        capRoomSum = s.CapRoomSum;
                        tickSnap = s;
                    }
                }

                cumProd = tickSnap.CumulativeProduction - prod0;
                cumCons = tickSnap.CumulativeConsumption - cons0;
                stockEnd = tickSnap.Stock;
                transitEnd = tickSnap.Transit;

                // Identité conservation : Δ(stock+transit) = prod − cons
                var deltaSt = (stockEnd + transitEnd) - (stock0 + transit0);
                var expectedDelta = cumProd - cumCons;
                var residual = deltaSt - expectedDelta;

                sb.AppendLine("--- cumul ticks 1.." + ClothTicks + " ---");
                sb.AppendLine("stock0=" + Fmt1(stock0) + " transit0=" + Fmt1(transit0));
                sb.AppendLine("produced=" + Fmt1(cumProd));
                sb.AppendLine("entered_stock≈produced (dépôt PhysicalProductionSystem)");
                sb.AppendLine("shipped=" + Fmt1(shippedCum));
                sb.AppendLine("delivered=" + Fmt1(deliveredCum));
                sb.AppendLine("consumed_goodId8_ledger=" + Fmt1(cumCons));
                sb.AppendLine("consumed_goodId8_counter=" + Fmt1(consumedIdCum));
                sb.AppendLine("demand_sum=" + Fmt1(demandCum) +
                             " satisfied_sum=" + Fmt1(satCum));
                sb.AppendLine("stock_end=" + Fmt1(stockEnd) +
                             " transit_end=" + Fmt1(transitEnd));
                sb.AppendLine("delta_stock_plus_transit=" + Fmt1(deltaSt));
                sb.AppendLine("produced_minus_consumed=" + Fmt1(expectedDelta));
                sb.AppendLine("conservation_residual=" + Fmt1(residual) +
                             " (doit ≈0 ; |r|<max(1,1e-3×flux))");

                var flux = math.abs(cumProd) + math.abs(cumCons);
                var tol = math.max(1.0, 1e-3 * flux);
                var closes = math.abs(residual) <= tol;
                sb.AppendLine("bilan_boucle=" + (closes ? "YES" : "NO") +
                              " tol=" + Fmt1(tol));

                // Trou nommé
                var neverShipped = cumProd - shippedCum;
                var shippedShare = cumProd > 1e-6 ? shippedCum / cumProd : 0;
                var stockShare = cumProd > 1e-6 ? stockEnd / cumProd : 0;
                var consShare = cumProd > 1e-6 ? cumCons / cumProd : 0;
                sb.AppendLine("share_shipped_of_produced=" + Fmt(shippedShare));
                sb.AppendLine("share_stock_of_produced=" + Fmt(stockShare));
                sb.AppendLine("share_consumed_of_produced=" + Fmt(consShare));
                sb.AppendLine("never_shipped_approx=" + Fmt1(neverShipped) +
                             " (produit − chargé ; reste local ou stock dormant)");

                string hole;
                if (!closes)
                {
                    hole = "ÉCART DE CONSERVATION=" + Fmt1(residual) +
                           " — le trou est le résidu lui-même ; NE PAS corriger.";
                }
                else if (stockShare > 0.5 && shippedShare < 0.4)
                {
                    hole = "STOCK DORMANT : " + Pct(stockShare) +
                           " % du drap produit reste en stock ; " +
                           Pct(shippedShare) + " % seulement est chargé.";
                }
                else if (shippedShare > 0.5 && consShare < 0.35)
                {
                    hole = "TRANSPORTÉ MAIS PEU CONSOMMÉ : chargé " +
                           Pct(shippedShare) + " %, consommé " + Pct(consShare) +
                           " % — saturation destination / ordre / blend.";
                }
                else if (ratioPhys < 0.5f)
                {
                    hole = "PRODUCTION PHYSIQUE FAIBLE face à la demande (phys/dem=" +
                           Fmt(ratioPhys) + ") — le frein de débouché / intrants " +
                           "étrangle AVANT le stock ; le proxy v1_083 mentait.";
                }
                else
                {
                    hole = "bilan clos ; examiner candidats ci-dessous.";
                }

                sb.AppendLine("trou_nomme=" + hole);
                sb.AppendLine();

                // Candidats
                sb.AppendLine("--- candidats (chiffre qui accuse ou innocente) ---");
                var capSat = capAttempts > 0
                    ? (double)capExhausted / capAttempts
                    : 0;
                var avgRoom = capAttempts > 0 ? capRoomSum / capAttempts : 0;
                sb.AppendLine(
                    "v1_024_edge_cap: attempts=" + capAttempts +
                    " exhausted=" + capExhausted +
                    " exhausted_frac=" + Fmt(capSat) +
                    " avg_room_when_served=" + Fmt1(avgRoom) +
                    " → " + (capSat < 0.15
                        ? "INNOCENT (capacité d'arête rarement saturée)"
                        : "ACCUSÉ (saturation arête fréquente)"));

                sb.AppendLine(
                    "v1_030_service_order: mode=" + PhysicalStockSystem.ServiceOrderMode +
                    " (ByDeficitSeverity adopté) cloth_shipped_cum=" + Fmt1(shippedCum) +
                    " → pas de 4e hypothèse ; chiffre de référence publié, " +
                    "pas de rebascule ByGoodId ici.");

                sb.AppendLine(
                    "v1_031_032_outlet_brake: OutletCapIntensity=" +
                    PhysicalProductionSystem.OutletCapIntensity.ToString(
                        "0.###", CultureInfo.InvariantCulture) +
                    " missed_outlet_share_tick300=" + Fmt(missOutlet) +
                    " missed_input_share_tick300=" + Fmt(missInput) +
                    " → " + (missOutlet > 0.3f
                        ? "ACCUSÉ (frein débouché retire une part majeure)"
                        : missOutlet > 0.05f
                            ? "PARTIEL (frein actif mais secondaire)"
                            : "INNOCENT ce tick (MissedOutlet≈0)"));

                sb.AppendLine(
                    "stocks_dormants: stock_end/produced=" + Fmt(stockShare) +
                    " stock_end=" + Fmt1(stockEnd) +
                    " → " + (stockShare > 0.5
                        ? "ACCUSÉ"
                        : stockShare > 0.2
                            ? "PARTIEL"
                            : "INNOCENT"));

                var blend = PhysicalSatisfactionBlendSystem.DefaultPhysicalBlendWeight;
                // Avec LockWeight(0), pops lisent 0 % physique. Au défaut 0.25 :
                // physical weight = 0.25, abstract = 0.75.
                sb.AppendLine(
                    "v1_022_blend: DefaultPhysicalBlendWeight=" +
                    blend.ToString("0.###", CultureInfo.InvariantCulture) +
                    " (mesure v1_083/v1_084 à weight=0 pour isoler le physique) ; " +
                    "au runtime défaut les pops lisent " +
                    Pct(blend) + " % physique + " + Pct(1f - blend) +
                    " % abstrait. satisfied_phys/demand=" + Fmt(ratioSat) +
                    " → le mixte abstrait peut masquer un stock physique local, " +
                    "mais le plafond 0,28 est déjà mesuré en weight=0 (pur physique) " +
                    "donc le blend N'EST PAS la cause du plafond physique.");

                sb.AppendLine();
                sb.AppendLine("--- tick " + ClothTicks + " (détail) ---");
                sb.AppendLine(
                    "tick_produced=" + Fmt1(prodWindow) +
                    " tick_shipped=" + Fmt1(shippedWindow) +
                    " tick_delivered=" + Fmt1(deliveredWindow) +
                    " tick_consumed_id=" + Fmt1(consumedIdWindow) +
                    " tick_demand=" + Fmt1(v1083Demand) +
                    " tick_satisfied=" + Fmt1(v1083Sat) +
                    " tick_stock=" + Fmt1(tickSnap.Stock) +
                    " tick_transit=" + Fmt1(tickSnap.Transit));
                sb.AppendLine(
                    "tick_identity: stock+transit=" +
                    Fmt1(tickSnap.Stock + tickSnap.Transit) +
                    " prod−cons_cum=" +
                    Fmt1(tickSnap.CumulativeProduction - tickSnap.CumulativeConsumption) +
                    " residual_vs_ledger=" +
                    Fmt1((tickSnap.Stock + tickSnap.Transit) -
                         (tickSnap.CumulativeProduction - tickSnap.CumulativeConsumption)));

                // ── PARTIE 3 — parité ──────────────────────────────────────
                sb.AppendLine();
                sb.AppendLine("=== PARTIE 3 — PARITÉ + DETTE LARGE ===");
                PhysicalSatisfactionBlendSystem.ResetToCompiledDefault();
                BuildingInitSystem.ResetCitySeedCoefficientToCompiledDefault();
                BuildingConstructionSystem.UnlockCapacityIntensity();
                PhysicalProductionSystem.UnlockOutletCap();
                PhysicalStockSystem.RecordClothBalance = false;
                PhysicalStockSystem.RecordTransportShares = false;
                PhysicalStockSystem.ClearTransportShareCounters();
                PhysicalStockSystem.ClearClothBalanceCounters();

                BuildingInitSystem.LockCitySeedCoefficient(0f);
                ulong dig;
                using (var hp = new SimulationHarness(Seed))
                {
                    hp.RunTicks(DeterminismTicks);
                    dig = WorldDigest.Compute(hp.EntityManager);
                }

                sb.AppendLine(
                    "DefaultCitySeedCoefficient=" +
                    BuildingInitSystem.DefaultCitySeedCoefficient.ToString(
                        "0.###", CultureInfo.InvariantCulture) +
                    " (doit rester 0)");
                sb.AppendLine(
                    "PhysicalBlendWeight=" +
                    PhysicalSatisfactionBlendSystem.PhysicalBlendWeight.ToString(
                        "0.###", CultureInfo.InvariantCulture) +
                    " (défaut compilé avant empreinte)");
                sb.AppendLine(
                    "parity_v1_009_fingerprint=0x" + dig.ToString("X16") +
                    " expected=0x" + ExpectedParity.ToString("X16") +
                    " " + (dig == ExpectedParity ? "PASS" : "FAIL"));
                sb.AppendLine(
                    "LARGE: rejouée à part (voir Logs/v1_084_large.xml) — " +
                    "filtre v1_085 + V1084 ; budget " +
                    PerCaseBudgetS.ToString("0.##", CultureInfo.InvariantCulture) +
                    " s/cas (v1_078).");

                // Verdict
                sb.AppendLine();
                sb.AppendLine("=== VERDICT MESURE ===");
                var verdict =
                    (closes ? "PASS_BILAN" : "FAIL_BILAN") +
                    " ; " + unitsVerdict +
                    " ; trou=" + hole +
                    " ; conservation_residual=" + Fmt1(residual) +
                    " ; parity=" + (dig == ExpectedParity ? "PASS" : "FAIL") +
                    " ; DefaultCitySeedCoefficient reste 0 ; " +
                    "PhysicalBlendWeight/CitySeed non adoptés (mesure).";
                sb.AppendLine(verdict);

                File.WriteAllText(LogPath, sb.ToString(), Encoding.UTF8);
                Debug.Log("V1084DrapBalanceTests: wrote " + LogPath);

                BuildingInitSystem.UnlockCitySeedCoefficient();
                PhysicalSatisfactionBlendSystem.UnlockWeight();

                Assert.IsTrue(closes,
                    "PARTIE 2: le bilan drap doit se boucler (conservation)");
                Assert.AreEqual(ExpectedParity, dig, "parité v1_009");
                Assert.AreEqual(0f, BuildingInitSystem.DefaultCitySeedCoefficient, 1e-8f);
            }
        }

        static string Fmt(double v) =>
            v.ToString("0.###", CultureInfo.InvariantCulture);

        static string Fmt1(double v) =>
            v.ToString("0.0", CultureInfo.InvariantCulture);

        static string Pct(double v) =>
            (v * 100.0).ToString("0.0", CultureInfo.InvariantCulture);
    }
}
