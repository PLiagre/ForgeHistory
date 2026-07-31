using System;
using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using VictoriaGame.Core;
using VictoriaGame.Economy;
using VictoriaGame.Politics;
using VictoriaGame.Presentation;

namespace VictoriaGame.Tests
{
    /// <summary>Batch : -executeMethod VictoriaGame.Tests.V1035BatchRunner.Run</summary>
    public static class V1035BatchRunner
    {
        public static void Run()
        {
            V1035TaxPolicyTests.RunSweepAndWriteLog();
            UnityEngine.Debug.Log("V1035BatchRunner: DONE");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
        }
    }

    /// <summary>
    /// v1_035 — politique fiscale par pays + chemin d'intention + balayage du levier.
    /// </summary>
    [TestFixture]
    public class V1035TaxPolicyTests
    {
        const uint Seed = 42195u;
        const int PlayerCountryId = PlayerControl.DefaultControlledCountryId; // FRA=0

        /// <summary>5 valeurs couvrant [0 .. 10×] : 0, 0.5×, 1×, 5×, 10×.</summary>
        static readonly float[] SweepMultipliers = { 0f, 0.5f, 1f, 5f, 10f };

        [TearDown]
        public void TearDown()
        {
            BuildingAiPolicyConfig.Unlock();
            BuildingAiPolicyConfig.ResetToCompiledDefault();
        }

        [Test]
        public void V1035_TaxPolicy_Defaults_On_All_Countries()
        {
            BuildingAiPolicyConfig.Lock(BuildingAiPolicy.HoldNone, 0f);
            using var harness = new SimulationHarness(Seed);
            harness.RunTicks(0);

            var count = 0;
            using var q = harness.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<CountryData>(),
                ComponentType.ReadOnly<TaxPolicy>());
            using var policies = q.ToComponentDataArray<TaxPolicy>(Allocator.Temp);
            for (var i = 0; i < policies.Length; i++)
            {
                Assert.AreEqual(
                    TaxPolicyLimits.DefaultProductionTaxRate,
                    policies[i].ProductionTaxRate,
                    1e-12f,
                    "TaxPolicy doit démarrer au défaut bit-identique.");
                count++;
            }

            Assert.Greater(count, 0);
            Assert.AreEqual(TaxSystem.ProductionTaxRate, TaxPolicyLimits.DefaultProductionTaxRate, 1e-12f);
            Assert.AreEqual(TaxAiPolicy.HoldDefault, TaxAiPolicyConfig.Mode);
        }

        [Test]
        public void V1035_Intention_Accepts_InBounds_On_Player_Country()
        {
            using var harness = new SimulationHarness(Seed);
            harness.RunTicks(0);

            var target = TaxPolicyLimits.DefaultProductionTaxRate * 2f;
            Assert.IsTrue(TaxPolicyLimits.IsInBounds(target));
            Assert.IsTrue(PlayerIntentionSubmit.EnqueueSetProductionTaxRate(
                harness.EntityManager, PlayerCountryId, target));

            harness.RunTicks(1);

            Assert.AreEqual(target, ReadTaxRate(harness.EntityManager, PlayerCountryId), 1e-12f);
            var receipt = ReadReceipt(harness.EntityManager);
            Assert.AreEqual(1, receipt.Accepted);
            Assert.AreEqual(PlayerIntentionKind.SetProductionTaxRate, receipt.Kind);
            Assert.AreEqual("accepted", receipt.Reason.ToString());
        }

        [Test]
        public void V1035_Intention_Rejects_OutOfBounds_CountryNotControlled_UnknownCountry()
        {
            // HoldNone : sinon l'IA émet des constructions le même tick et écrase le reçu.
            BuildingAiPolicyConfig.Lock(BuildingAiPolicy.HoldNone, 0f);
            using var harness = new SimulationHarness(Seed);
            harness.RunTicks(0);

            // Hors bornes
            var tooHigh = TaxPolicyLimits.MaxProductionTaxRate * 2f;
            PlayerIntentionSubmit.EnqueueSetProductionTaxRate(
                harness.EntityManager, PlayerCountryId, tooHigh);
            harness.RunTicks(1);
            var r1 = ReadReceipt(harness.EntityManager);
            Assert.AreEqual(0, r1.Accepted);
            Assert.AreEqual("rate_out_of_bounds", r1.Reason.ToString());
            Assert.AreEqual(
                TaxPolicyLimits.DefaultProductionTaxRate,
                ReadTaxRate(harness.EntityManager, PlayerCountryId),
                1e-12f);

            // Pays non contrôlé (ENG ≠ FRA)
            var otherId = FindCountryIdByTag(harness.EntityManager, "ENG");
            Assert.GreaterOrEqual(otherId, 0);
            var beforeOther = ReadTaxRate(harness.EntityManager, otherId);
            PlayerIntentionSubmit.EnqueueSetProductionTaxRate(
                harness.EntityManager, otherId, TaxPolicyLimits.DefaultProductionTaxRate * 3f);
            harness.RunTicks(1);
            var r2 = ReadReceipt(harness.EntityManager);
            Assert.AreEqual(0, r2.Accepted);
            Assert.AreEqual("country_not_controlled", r2.Reason.ToString());
            Assert.AreEqual(beforeOther, ReadTaxRate(harness.EntityManager, otherId), 1e-12f);

            // Pays inexistant (forcer le contrôle sur 999 puis intention 999 → not_found)
            using (var q = harness.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<PlayerControl>()))
            {
                var e = q.GetSingletonEntity();
                harness.EntityManager.SetComponentData(e, new PlayerControl { ControlledCountryId = 999 });
            }

            PlayerIntentionSubmit.EnqueueSetProductionTaxRate(
                harness.EntityManager, 999, TaxPolicyLimits.DefaultProductionTaxRate);
            harness.RunTicks(1);
            var r3 = ReadReceipt(harness.EntityManager);
            Assert.AreEqual(0, r3.Accepted);
            Assert.AreEqual("country_not_found", r3.Reason.ToString());
        }

        [Test]
        public void V1035_Intention_Deterministic_Replay()
        {
            var rate = TaxPolicyLimits.DefaultProductionTaxRate * 3f;
            var incomeA = RunWithTaxAtTick50(rate);
            var incomeB = RunWithTaxAtTick50(rate);
            Assert.AreEqual(incomeA, incomeB, 1e-5f, "Même intention → même revenu fiscal.");
        }

        [Test]
        public void V1035_Default_WorldMetrics_Stable_TwoRuns_T200()
        {
            var a = CaptureMetrics(200);
            var b = CaptureMetrics(200);
            Assert.AreEqual(a.TotalDebt, b.TotalDebt, 1e-3f);
            Assert.AreEqual(a.BankruptCount, b.BankruptCount);
            Assert.AreEqual(a.NeedsSatAvg, b.NeedsSatAvg, 1e-5f);
            Assert.AreEqual(a.Population, b.Population);
            Assert.AreEqual(a.WorldArmyStr, b.WorldArmyStr, 1e-2f);
            Assert.AreEqual(a.CountriesWithLand, b.CountriesWithLand);
            Assert.AreEqual(a.ActiveWars, b.ActiveWars);
        }

        /// <summary>
        /// Garde-fou au point adopté (défaut 1×) : dette au taux fort franchement
        /// inférieure à dette au taux nul ; monde défaut vivant. Horizon mesuré v1_042.
        /// </summary>
        [Test]
        public void V1035_Tax_AdoptedPoint_Guard()
        {
            Assert.IsTrue(
                TryTaxAdoptedPointGuard(TaxAdoptedGuardHorizonTicks, out var detail),
                detail);
        }

        /// <summary>Horizon le plus court qui sépare encore (mesuré v1_042_suite.log).</summary>
        public const int TaxAdoptedGuardHorizonTicks = 300;

        /// <summary>
        /// Balayage calibration : uniquement via V1035BatchRunner
        /// (retiré du filtre EditMode [Test] — patron v1_027).
        /// </summary>
        public static void V1035_TaxSweep_Publish_And_Verdict() => RunSweepAndWriteLog();

        public static void RunSweepAndWriteLog()
        {
            var logsDir = Path.Combine(UnityEngine.Application.dataPath, "..", "Logs");
            Directory.CreateDirectory(logsDir);
            var path = Path.Combine(logsDir, "v1_035_tax_sweep.log");
            var sb = new StringBuilder(64 * 1024);

            sb.AppendLine("=== v1_035 TAX POLICY SWEEP seed=42195 ===");
            sb.AppendLine(
                $"Default={FmtE(TaxPolicyLimits.DefaultProductionTaxRate)} " +
                $"Min={FmtE(TaxPolicyLimits.MinProductionTaxRate)} " +
                $"Max={FmtE(TaxPolicyLimits.MaxProductionTaxRate)} " +
                $"AiPolicy={TaxAiPolicyConfig.Mode}");
            sb.AppendLine(
                "AI: HoldDefault — pays non-joueurs gardent le taux défaut (comportement EXPLICITE).");
            sb.AppendLine(
                "Bornes: Min=0 (revenu prod. nul → effondrement trésor) ; " +
                "Max=10× défaut (enveloppe ; seuil vivable publié ci-dessous).");
            sb.AppendLine();
            sb.AppendLine(
                "mult | rate | tick | debt | bankrupt | sat | pop | army | countries | wars | " +
                "fraGold | fraDebt | fraInc | hungryPops");

            SweepRow[] rows = new SweepRow[SweepMultipliers.Length];
            for (var i = 0; i < SweepMultipliers.Length; i++)
            {
                var mult = SweepMultipliers[i];
                var rate = TaxPolicyLimits.DefaultProductionTaxRate * mult;
                rows[i] = RunSweepAtRate(rate, mult, sb);
            }

            sb.AppendLine();
            sb.AppendLine("=== COMPARAISON vs DÉFAUT (1×) @t3000 ===");
            var baseline = FindRow(rows, 1f);
            for (var i = 0; i < rows.Length; i++)
            {
                var r = rows[i];
                var debtDelta = r.Metrics.TotalDebt - baseline.Metrics.TotalDebt;
                var satDelta = r.Metrics.NeedsSatAvg - baseline.Metrics.NeedsSatAvg;
                var goldDelta = r.FraGold - baseline.FraGold;
                sb.AppendLine(
                    $"×{Fmt2(r.Mult)}: debtΔ={Fmt1(debtDelta)} satΔ={Fmt3(satDelta)} " +
                    $"fraGoldΔ={Fmt1(goldDelta)} bankrupt={r.Metrics.BankruptCount} " +
                    $"(base bankrupt={baseline.Metrics.BankruptCount})");
            }

            // Levier intéressant ? Le taux agit sur le trésor (income/gold/debt).
            // Satisfaction n'est PAS taxée directement — effet secondaire seulement.
            var low = FindRow(rows, 0f);
            var high = FindRow(rows, 10f);
            var goldMoves = Math.Abs(high.FraGold - low.FraGold) > 1f
                            || Math.Abs(high.FraInc - low.FraInc) > 0.1f;
            var debtMoves = Math.Abs(high.Metrics.TotalDebt - low.Metrics.TotalDebt) > 1f
                            || high.Metrics.BankruptCount != low.Metrics.BankruptCount;
            var satMoves = Math.Abs(high.Metrics.NeedsSatAvg - low.Metrics.NeedsSatAvg) > 0.01f;
            var interesting = goldMoves || debtMoves;

            sb.AppendLine();
            sb.AppendLine("=== VERDICT LEVIER ===");
            sb.AppendLine(
                $"Trésor/dette réagissent au taux: {(interesting ? "OUI" : "NON")} " +
                $"(fraGold 0×={Fmt1(low.FraGold)} 10×={Fmt1(high.FraGold)} ; " +
                $"worldDebt 0×={Fmt1(low.Metrics.TotalDebt)} 10×={Fmt1(high.Metrics.TotalDebt)}).");
            sb.AppendLine(
                $"Satisfaction réagit (effet 2nd): {(satMoves ? "OUI" : "NON / faible")} " +
                $"(0×={Fmt3(low.Metrics.NeedsSatAvg)} 10×={Fmt3(high.Metrics.NeedsSatAvg)}). " +
                "Attendu: l'impôt production ne prélève pas les pops — levier fiscal ≠ fardeau pop.");
            if (!interesting)
            {
                sb.AppendLine(
                    "VERDICT: levier DÉCORATIF sur les agrégats mondiaux — " +
                    "à publier comme information (pas un échec silencieux).");
            }
            else if (!satMoves)
            {
                sb.AppendLine(
                    "VERDICT: levier INTÉRESSANT pour l'État (trésor/dette) mais AVEUGLE " +
                    "sur le bien-être pop (pas de canal taxe→satisfaction). " +
                    "Enrichit l'État sans coût pop direct.");
            }
            else
            {
                sb.AppendLine(
                    "VERDICT: levier INTÉRESSANT — taux élevé enrichit l'État, " +
                    "taux faible l'inverse, avec effet pop mesurable.");
            }

            // Stabilité aux extrêmes (critères V1016 allégés, info seulement).
            sb.AppendLine();
            sb.AppendLine("=== STABILITÉ (info, sans retoucher v1_015→v1_018) ===");
            foreach (var r in rows)
            {
                var debtOk = r.Metrics.TotalDebt <= Math.Max(2500f, 1f); // plafond dur V1016-ish
                // V1016: debt@t3000 ≤ max(debt@t1000*2.5, 2500) — on n'a que t3000 ici.
                var armyOk = r.Metrics.WorldArmyStr > 1000f;
                var zombieOk = r.Metrics.ZombieArmyStrLandless < 0.5f;
                sb.AppendLine(
                    $"×{Fmt2(r.Mult)} @t3000: debt={Fmt1(r.Metrics.TotalDebt)} " +
                    $"army={Fmt0(r.Metrics.WorldArmyStr)} zombie={Fmt0(r.Metrics.ZombieArmyStrLandless)} " +
                    $"bankrupt={r.Metrics.BankruptCount} " +
                    $"armyOk={armyOk} zombieOk={zombieOk}");
            }

            // Seuil agressif encore « vivable » : plus haut mult avec army>1000 et zombie~0.
            float maxViableMult = 0f;
            for (var i = 0; i < rows.Length; i++)
            {
                var r = rows[i];
                if (r.Metrics.WorldArmyStr > 1000f && r.Metrics.ZombieArmyStrLandless < 0.5f)
                    maxViableMult = r.Mult;
            }

            sb.AppendLine(
                $"Seuil agressif encore vivable (army>1000, zombie≈0): ×{Fmt2(maxViableMult)} " +
                $"rate={FmtE(TaxPolicyLimits.DefaultProductionTaxRate * maxViableMult)}.");
            sb.AppendLine(
                "Si V1016/V1017/V1018 rougissent hors défaut: INFORMATION sur limites du levier " +
                "(calibration NON retouchée).");

            File.WriteAllText(path, sb.ToString());
            UnityEngine.Debug.Log(sb.ToString());

            // Garde-fou EditMode: le défaut 1× doit garder une armée vivante.
            Assert.Greater(baseline.Metrics.WorldArmyStr, 1000f,
                "Au taux défaut l'armée mondiale doit survivre @t3000.");
            Assert.Less(baseline.Metrics.ZombieArmyStrLandless, 0.5f);
        }

        /// <summary>
        /// Propriété adoptée v1_035 : au taux fort la dette mondiale reste franchement
        /// inférieure à celle au taux nul ; au défaut 1× le monde tient (armée &gt; 1000).
        /// </summary>
        public static bool TryTaxAdoptedPointGuard(int ticks, out string detail)
        {
            detail = "";
            BuildingAiPolicyConfig.Lock(BuildingAiPolicy.HoldNone, 0f);

            float debtZero, debtHigh, armyDefault;
            using (var h = new SimulationHarness(Seed))
            {
                h.RunTicks(0);
                SetAllTaxRates(h.EntityManager, 0f);
                h.RunTicks(ticks);
                debtZero = WorldMetrics.Capture(h.EntityManager, ticks).TotalDebt;
            }

            using (var h = new SimulationHarness(Seed))
            {
                h.RunTicks(0);
                SetAllTaxRates(
                    h.EntityManager,
                    TaxPolicyLimits.DefaultProductionTaxRate * 10f);
                h.RunTicks(ticks);
                debtHigh = WorldMetrics.Capture(h.EntityManager, ticks).TotalDebt;
            }

            using (var h = new SimulationHarness(Seed))
            {
                h.RunTicks(ticks);
                var m = WorldMetrics.Capture(h.EntityManager, ticks);
                armyDefault = m.WorldArmyStr;
            }

            // « Franchement inférieure » : écart relatif ≥ 20 % ou absolu ≥ 50.
            var franklyLower = debtHigh < debtZero - 50f ||
                               debtHigh < debtZero * 0.8f;
            var defaultHolds = armyDefault > 1000f;
            detail =
                $"t={ticks} debt0={debtZero:0.0} debt10x={debtHigh:0.0} " +
                $"army1x={armyDefault:0} franklyLower={franklyLower} defaultHolds={defaultHolds}";
            return franklyLower && defaultHolds;
        }

        static SweepRow RunSweepAtRate(float rate, float mult, StringBuilder sb)
        {
            using var harness = new SimulationHarness(Seed);
            harness.RunTicks(0);

            // Pose le taux sur TOUS les pays (mesure du levier mondial).
            // L'IA HoldDefault est le comportement de jeu ; ici on force pour balayer.
            SetAllTaxRates(harness.EntityManager, rate);

            harness.RunTicks(3000);
            var metrics = WorldMetrics.Capture(harness.EntityManager, 3000);
            ReadFraTreasury(harness.EntityManager, out var gold, out var debt, out var inc);
            var hungry = CountHungryPops(harness.EntityManager);

            sb.AppendLine(
                $"{Fmt2(mult)} | {FmtE(rate)} | 3000 | {Fmt1(metrics.TotalDebt)} | " +
                $"{metrics.BankruptCount} | {Fmt3(metrics.NeedsSatAvg)} | {metrics.Population} | " +
                $"{Fmt0(metrics.WorldArmyStr)} | {metrics.CountriesWithLand} | {metrics.ActiveWars} | " +
                $"{Fmt1(gold)} | {Fmt1(debt)} | {Fmt1(inc)} | {hungry}");

            return new SweepRow
            {
                Mult = mult,
                Rate = rate,
                Metrics = metrics,
                FraGold = gold,
                FraDebt = debt,
                FraInc = inc,
                HungryPops = hungry
            };
        }

        static float RunWithTaxAtTick50(float rate)
        {
            using var harness = new SimulationHarness(Seed);
            harness.RunTicks(0);
            PlayerIntentionSubmit.EnqueueSetProductionTaxRate(
                harness.EntityManager, PlayerCountryId, rate);
            harness.RunTicks(50);
            ReadFraTreasury(harness.EntityManager, out _, out _, out var inc);
            return inc;
        }

        static WorldMetrics.Snapshot CaptureMetrics(int ticks)
        {
            using var harness = new SimulationHarness(Seed);
            harness.RunTicks(ticks);
            return WorldMetrics.Capture(harness.EntityManager, ticks);
        }

        static void SetAllTaxRates(EntityManager em, float rate)
        {
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<TaxPolicy>());
            using var entities = q.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                em.SetComponentData(entities[i], new TaxPolicy { ProductionTaxRate = rate });
            }
        }

        static float ReadTaxRate(EntityManager em, int countryId)
        {
            using var q = em.CreateEntityQuery(
                ComponentType.ReadOnly<CountryData>(),
                ComponentType.ReadOnly<TaxPolicy>());
            using var entities = q.ToEntityArray(Allocator.Temp);
            using var countries = q.ToComponentDataArray<CountryData>(Allocator.Temp);
            for (var i = 0; i < countries.Length; i++)
            {
                if (countries[i].CountryId != countryId)
                    continue;
                return em.GetComponentData<TaxPolicy>(entities[i]).ProductionTaxRate;
            }

            return float.NaN;
        }

        static PlayerIntentionReceipt ReadReceipt(EntityManager em)
        {
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<PlayerIntentionReceipt>());
            return q.GetSingleton<PlayerIntentionReceipt>();
        }

        static int FindCountryIdByTag(EntityManager em, string tag)
        {
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<CountryData>());
            using var data = q.ToComponentDataArray<CountryData>(Allocator.Temp);
            for (var i = 0; i < data.Length; i++)
            {
                if (data[i].Tag.ToString() == tag)
                    return data[i].CountryId;
            }

            return -1;
        }

        static void ReadFraTreasury(
            EntityManager em, out float gold, out float debt, out float income)
        {
            gold = 0f;
            debt = 0f;
            income = 0f;
            using var q = em.CreateEntityQuery(
                ComponentType.ReadOnly<CountryData>(),
                ComponentType.ReadOnly<TreasuryData>());
            using var countries = q.ToComponentDataArray<CountryData>(Allocator.Temp);
            using var treasuries = q.ToComponentDataArray<TreasuryData>(Allocator.Temp);
            for (var i = 0; i < countries.Length; i++)
            {
                if (countries[i].CountryId != PlayerCountryId)
                    continue;
                gold = treasuries[i].Balance;
                debt = treasuries[i].Debt;
                income = treasuries[i].Income;
                return;
            }
        }

        static int CountHungryPops(EntityManager em)
        {
            var n = 0;
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<Population.PopData>());
            using var pops = q.ToComponentDataArray<Population.PopData>(Allocator.Temp);
            for (var i = 0; i < pops.Length; i++)
            {
                if (pops[i].NeedsSatisfaction < 0.5f)
                    n++;
            }

            return n;
        }

        static SweepRow FindRow(SweepRow[] rows, float mult)
        {
            for (var i = 0; i < rows.Length; i++)
            {
                if (Math.Abs(rows[i].Mult - mult) < 1e-6f)
                    return rows[i];
            }

            return rows[0];
        }

        static string FmtE(float v) => v.ToString("0.#####E+0", CultureInfo.InvariantCulture);
        static string Fmt0(float v) => v.ToString("0", CultureInfo.InvariantCulture);
        static string Fmt1(float v) => v.ToString("0.0", CultureInfo.InvariantCulture);
        static string Fmt2(float v) => v.ToString("0.00", CultureInfo.InvariantCulture);
        static string Fmt3(float v) => v.ToString("0.000", CultureInfo.InvariantCulture);

        struct SweepRow
        {
            public float Mult;
            public float Rate;
            public WorldMetrics.Snapshot Metrics;
            public float FraGold;
            public float FraDebt;
            public float FraInc;
            public int HungryPops;
        }
    }
}
