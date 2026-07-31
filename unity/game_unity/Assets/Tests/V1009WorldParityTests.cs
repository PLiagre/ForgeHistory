using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using VictoriaGame.Core;
using VictoriaGame.Economy;
using VictoriaGame.Military;
using VictoriaGame.Navy;
using VictoriaGame.Population;
using VictoriaGame.Presentation;
using VictoriaGame.World;

namespace VictoriaGame.Tests
{
    /// <summary>Batch : -executeMethod VictoriaGame.Tests.V1009BatchRunner.Run</summary>
    public static class V1009BatchRunner
    {
        public static void Run()
        {
            V1009WorldParityTests.RunDomainKeyProofs();
            V1009WorldParityTests.RunPart3Parity();
            V1009WorldParityTests.RunPart3NoOp();
            V1009WorldParityTests.RunPart4Anchors();
            UnityEngine.Debug.Log("V1009BatchRunner: DONE");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#endif
        }
    }

    /// <summary>
    /// v1_010 — correctif Entity.Index → CountryId ; parité stricte ; no-op dip_008 ; ancrages.
    /// Reprend V1009WorldParityTests (pas de 9e test de parité).
    /// </summary>
    [TestFixture]
    public class V1009WorldParityTests
    {
        const uint Seed = 42195u;

        [Test]
        public void V1010_DomainKeys_HashTag_Stable_And_CountryIds_Unique() =>
            RunDomainKeyProofs();

        [Test]
        public void V1009_Part3_Parity_At_T100_And_T200() =>
            RunPart3Parity();

        [Test]
        public void V1010_Part3_NoOp_Does_Not_Change_Metrics() =>
            RunPart3NoOp();

        [Test]
        public void V1009_Part4_Anchors_After_Ordering_Fix() =>
            RunPart4Anchors();

        /// <summary>
        /// Preuves de stabilité : HashTag("FRA") == constante ; CountryId uniques ;
        /// clés de tri armée sans ex-aequo.
        /// </summary>
        public static void RunDomainKeyProofs()
        {
            var fra = new FixedString32Bytes("FRA");
            var hash = DomainKeys.HashTag(fra);
            Assert.AreEqual(
                DomainKeys.ExpectedHashFra,
                hash,
                "HashTag(FRA) a changé — hash d'octets non stable ou constante périmée.");

            using var harness = new SimulationHarness(Seed);
            harness.RunTicks(0);

            var countryIds = new List<int>();
            var tags = new List<string>();
            using (var q = harness.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<CountryData>()))
            using (var data = q.ToComponentDataArray<CountryData>(Allocator.Temp))
            {
                for (var i = 0; i < data.Length; i++)
                {
                    countryIds.Add(data[i].CountryId);
                    tags.Add(data[i].Tag.ToString());
                }
            }

            Assert.AreEqual(20, countryIds.Count);
            Assert.AreEqual(countryIds.Count, countryIds.Distinct().Count(),
                "CountryId doit être unique (rang countries.json).");
            var fraId = -1;
            for (var i = 0; i < tags.Count; i++)
            {
                if (tags[i] == "FRA")
                {
                    fraId = countryIds[i];
                    break;
                }
            }

            Assert.AreEqual(0, fraId, "FRA doit avoir CountryId=0 (premier de countries.json).");

            // Ordre total armées : CountryId unique ⇒ aucun ex-aequo CompareArmyKeys.
            var armyKeys = new List<(int CountryId, int ProvinceId)>();
            using (var q = harness.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<ArmyData>()))
            using (var armies = q.ToComponentDataArray<ArmyData>(Allocator.Temp))
            {
                var em = harness.EntityManager;
                for (var i = 0; i < armies.Length; i++)
                {
                    var cid = em.GetComponentData<CountryData>(armies[i].Country).CountryId;
                    armyKeys.Add((cid, armies[i].ProvinceId));
                }
            }

            for (var i = 0; i < armyKeys.Count; i++)
            {
                for (var j = i + 1; j < armyKeys.Count; j++)
                {
                    var cmp = DomainKeys.CompareArmyKeys(
                        armyKeys[i].CountryId, armyKeys[i].ProvinceId,
                        armyKeys[j].CountryId, armyKeys[j].ProvinceId);
                    Assert.AreNotEqual(0, cmp,
                        $"Ex-aequo tri armée entre ({armyKeys[i]}) et ({armyKeys[j]}) — " +
                        "l'ordre des chunks ECS reprendrait la main.");
                }
            }
        }

        /// <summary>
        /// PARTIE 2 — égalité STRICTE joué/mesuré à t=12, t=100, t=200.
        /// </summary>
        public static void RunPart3Parity()
        {
            var logPath = Path.Combine(
                UnityEngine.Application.dataPath, "..", "Logs", "v1_010_parity.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

            var sb = new StringBuilder();
            sb.AppendLine($"=== v1_010 PARTIE 2 — PARITÉ joué vs mesuré seed={Seed} ===");
            sb.AppendLine("Comparaison à TICK ÉGAL (WorldState.CurrentTick).");
            sb.AppendLine(
                "9 métriques : warsDeclared, victories, annexedProvinces, maxProvinces, " +
                "worldArmyStr, totalRegiments, population, needsSatAvg, livingArmies.");
            sb.AppendLine("Ticks : t=12 (premier divergent v1_009), t=100, t=200.");
            sb.AppendLine();

            var allStrict = true;
            foreach (var tick in new[] { 12, 100, 200 })
            {
                var played = CaptureDefaultWorldAtTick(Seed, tick);
                var measured = CaptureHarnessAtTick(Seed, tick);
                sb.AppendLine($"--- tick={tick} ---");
                sb.AppendLine("JOUÉ:    " + WorldMetrics.FormatStandardLine(tick, played));
                sb.AppendLine("MESURÉ:  " + WorldMetrics.FormatStandardLine(tick, measured));
                sb.AppendLine("ÉCARTS:");
                var diffs = DiffNine(played, measured);
                if (diffs.Count == 0)
                {
                    sb.AppendLine("  (aucun)");
                }
                else
                {
                    allStrict = false;
                    foreach (var d in diffs)
                        sb.AppendLine("  " + d);
                }

                sb.AppendLine();
            }

            if (allStrict)
            {
                sb.AppendLine(
                    "VERDICT PARITÉ: JOUÉ == MESURÉ — égalité stricte sur les 9 métriques à t12, t100 et t200.");
            }
            else
            {
                sb.AppendLine(
                    "VERDICT PARITÉ: JOUÉ ≠ MESURÉ — dépendance d'allocation résiduelle hors des 8 sites.");
                AppendFirstDivergenceDichotomy(sb);
            }

            File.WriteAllText(logPath, sb.ToString());
            UnityEngine.Debug.Log(sb.ToString());

            Assert.IsTrue(allStrict,
                "Parité stricte joué/mesuré échouée — voir Logs/v1_010_parity.log");
        }

        /// <summary>
        /// PARTIE 3 — système NO-OP : métriques bit-identiques avec/sans (solder dip_008).
        /// </summary>
        public static void RunPart3NoOp()
        {
            var logPath = Path.Combine(
                UnityEngine.Application.dataPath, "..", "Logs", "v1_010_noop.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

            var sb = new StringBuilder();
            sb.AppendLine($"=== v1_010 PARTIE 3 — NO-OP probe (dip_008) seed={Seed} ===");
            sb.AppendLine(
                "Mesure AVEC et SANS NoOpProbeSystem dans SimulationSystemGroup, " +
                "même seed, ticks identiques. Attendu après correctif CountryId : bit-identiques.");
            sb.AppendLine();

            var allSame = true;
            foreach (var tick in new[] { 12, 100, 200 })
            {
                var without = CaptureHarnessAtTick(Seed, tick, installNoOp: false);
                var with = CaptureHarnessAtTick(Seed, tick, installNoOp: true);
                sb.AppendLine($"--- tick={tick} ---");
                sb.AppendLine("SANS: " + WorldMetrics.FormatStandardLine(tick, without));
                sb.AppendLine("AVEC: " + WorldMetrics.FormatStandardLine(tick, with));
                var diffs = DiffNine(without, with);
                sb.AppendLine($"écarts={diffs.Count}");
                if (diffs.Count == 0)
                {
                    sb.AppendLine("  (aucun)");
                }
                else
                {
                    allSame = false;
                    foreach (var d in diffs)
                        sb.AppendLine("  " + d);
                }

                sb.AppendLine();
            }

            if (allSame)
            {
                sb.AppendLine("VERDICT dip_008: SOLDÉE — OUI");
                sb.AppendLine(
                    "Ajouter un système no-op ne change plus les métriques " +
                    "(clés de domaine stables, plus Entity.Index).");
            }
            else
            {
                sb.AppendLine("VERDICT dip_008: SOLDÉE — NON");
                sb.AppendLine("Les métriques bougent encore à l'ajout d'un no-op — dette non close.");
            }

            File.WriteAllText(logPath, sb.ToString());
            UnityEngine.Debug.Log(sb.ToString());

            Assert.IsTrue(allSame,
                "No-op change encore les métriques — voir Logs/v1_010_noop.log");
        }

        public static void RunPart4Anchors()
        {
            const int RefNonCore = 18;
            const int RefCountries = 14;
            const int RefMaxProv = 11;
            const string RefDebt = "750.9";
            const int RefBankrupt = 4;
            const string RefArmy = "38953";
            const int RefZombie = 0;
            const string RefSat = "0.698";
            const int RefPop = 142551;
            const string RefRatioV800 = "72.5";
            const int RefStuck800 = 0;
            const int RefAnnexed800 = 15;

            var measurePath = Path.Combine(
                UnityEngine.Application.dataPath, "..", "Logs", "v1_010_measurements.log");
            Directory.CreateDirectory(Path.GetDirectoryName(measurePath)!);

            var sb = new StringBuilder();
            sb.AppendLine($"=== v1_010 ANCRAGES seed={Seed} ===");
            sb.AppendLine(
                "Re-mesure après remplacement Entity.Index → CountryId. " +
                "WorldMetrics.Capture / FormatStandardLine (règle test_001).");
            sb.AppendLine(
                "Anciens (pré-correctif) : nonCore=18/50, land=14, maxProv=11, " +
                "debt=750.9, bankrupt=4, army=38953, zombie=0, sat=0.698, pop=142551 ; " +
                "ratioV@800=72.5%, stuck=0, annexed=15.");
            sb.AppendLine(
                "ATTENDU : ancrages militaires PEUVENT bouger (changement de graines RNG). " +
                "Démographie/économie ne doivent PAS s'effondrer.");
            sb.AppendLine();

            WorldMetrics.Snapshot t800 = default;
            WorldMetrics.Snapshot t1000 = default;

            using (var harness = new SimulationHarness(Seed))
            {
                harness.RunTicks(0);
                sb.AppendLine(WorldMetrics.FormatStandardLine(0, WorldMetrics.Capture(harness.EntityManager, 0)));

                harness.RunTicks(200);
                sb.AppendLine(WorldMetrics.FormatStandardLine(200, WorldMetrics.Capture(harness.EntityManager, 200)));

                harness.RunTicks(300);
                sb.AppendLine(WorldMetrics.FormatStandardLine(500, WorldMetrics.Capture(harness.EntityManager, 500)));

                harness.RunTicks(300);
                t800 = WorldMetrics.Capture(harness.EntityManager, 800);
                sb.AppendLine(WorldMetrics.FormatStandardLine(800, t800));

                harness.RunTicks(200);
                t1000 = WorldMetrics.Capture(harness.EntityManager, 1000);
                sb.AppendLine(WorldMetrics.FormatStandardLine(1000, t1000));
            }

            sb.AppendLine();
            sb.AppendLine("=== COMPARAISON ANCIEN / NOUVEAU (12 ancrages) ===");
            var allMatch = true;
            allMatch &= Check(sb, "nonCore", $"{RefNonCore}/50",
                $"{t1000.NonCoreProvinces}/{t1000.TotalProvincesOwned}",
                t1000.NonCoreProvinces == RefNonCore && t1000.TotalProvincesOwned == 50);
            allMatch &= Check(sb, "countriesWithLand", RefCountries.ToString(),
                t1000.CountriesWithLand.ToString(), t1000.CountriesWithLand == RefCountries);
            allMatch &= Check(sb, "maxProvinces", RefMaxProv.ToString(),
                t1000.MaxProvincesOneCountry.ToString(),
                t1000.MaxProvincesOneCountry == RefMaxProv);
            allMatch &= Check(sb, "totalDebt", RefDebt,
                WorldMetrics.Fmt1(t1000.TotalDebt), WorldMetrics.Fmt1(t1000.TotalDebt) == RefDebt);
            allMatch &= Check(sb, "bankrupt", RefBankrupt.ToString(),
                t1000.BankruptCount.ToString(), t1000.BankruptCount == RefBankrupt);
            allMatch &= Check(sb, "worldArmyStr", RefArmy,
                WorldMetrics.Fmt0(t1000.WorldArmyStr),
                WorldMetrics.Fmt0(t1000.WorldArmyStr) == RefArmy);
            allMatch &= Check(sb, "zombie", RefZombie.ToString(),
                WorldMetrics.Fmt0(t1000.ZombieArmyStrLandless),
                WorldMetrics.Fmt0(t1000.ZombieArmyStrLandless) == RefZombie.ToString());
            allMatch &= Check(sb, "needsSatAvg", RefSat,
                WorldMetrics.Fmt3(t1000.NeedsSatAvg),
                WorldMetrics.Fmt3(t1000.NeedsSatAvg) == RefSat);
            allMatch &= Check(sb, "population", RefPop.ToString(),
                t1000.Population.ToString(), t1000.Population == RefPop);

            var ratio800 = WorldMetrics.Fmt1(t800.RatioVictories * 100f);
            allMatch &= Check(sb, "ratioV@800", RefRatioV800 + "%", ratio800 + "%",
                ratio800 == RefRatioV800);
            allMatch &= Check(sb, "stuck@800", RefStuck800.ToString(),
                t800.StuckWars.ToString(), t800.StuckWars == RefStuck800);
            allMatch &= Check(sb, "annexed@800", RefAnnexed800.ToString(),
                t800.AnnexedProvinces.ToString(), t800.AnnexedProvinces == RefAnnexed800);

            sb.AppendLine();
            sb.AppendLine("=== NOUVEAU JEU D'ANCRAGES DE RÉFÉRENCE ===");
            sb.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "  t1000: nonCore={0}/{1}, countriesWithLand={2}, maxProvinces={3}, " +
                "totalDebt={4}, bankrupt={5}, worldArmyStr={6}, zombie={7}, " +
                "needsSatAvg={8}, population={9}",
                t1000.NonCoreProvinces, t1000.TotalProvincesOwned,
                t1000.CountriesWithLand, t1000.MaxProvincesOneCountry,
                WorldMetrics.Fmt1(t1000.TotalDebt), t1000.BankruptCount,
                WorldMetrics.Fmt0(t1000.WorldArmyStr),
                WorldMetrics.Fmt0(t1000.ZombieArmyStrLandless),
                WorldMetrics.Fmt3(t1000.NeedsSatAvg), t1000.Population));
            sb.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "  t800: ratioV={0}%, stuck={1}, annexed={2}",
                ratio800, t800.StuckWars, t800.AnnexedProvinces));

            sb.AppendLine();
            sb.AppendLine("=== CONTRÔLE DE SANITÉ (plausibilité) ===");
            var plausible = true;
            if (t1000.CountriesWithLand < 5)
            {
                plausible = false;
                sb.AppendLine($"FAIL countriesWithLand={t1000.CountriesWithLand} — effondrement (<5).");
            }
            else
            {
                sb.AppendLine($"OK countriesWithLand={t1000.CountriesWithLand} (>=5).");
            }

            if (t1000.TotalDebt > 5000f)
            {
                plausible = false;
                sb.AppendLine($"FAIL totalDebt={WorldMetrics.Fmt1(t1000.TotalDebt)} — explosion (>5000).");
            }
            else
            {
                sb.AppendLine($"OK totalDebt={WorldMetrics.Fmt1(t1000.TotalDebt)}.");
            }

            if (t1000.ZombieArmyStrLandless > 0.5f)
            {
                plausible = false;
                sb.AppendLine($"FAIL zombie={WorldMetrics.Fmt0(t1000.ZombieArmyStrLandless)}.");
            }
            else
            {
                sb.AppendLine("OK zombie=0.");
            }

            if (t800.StuckWars != 0)
            {
                plausible = false;
                sb.AppendLine($"FAIL stuck@800={t800.StuckWars}.");
            }
            else
            {
                sb.AppendLine("OK stuck@800=0.");
            }

            var ratioVal = t800.RatioVictories * 100f;
            if (ratioVal < 20f || ratioVal > 95f)
            {
                plausible = false;
                sb.AppendLine($"FAIL ratioV@800={ratio800}% — hors ordre de grandeur.");
            }
            else
            {
                sb.AppendLine($"OK ratioV@800={ratio800}% (ordre de grandeur plausible).");
            }

            sb.AppendLine();
            if (allMatch)
            {
                sb.AppendLine(
                    "VERDICT ANCRAGES: inchangés (12/12) — surprenant après changement de graines, " +
                    "mais bit-identiques à l'historique.");
            }
            else
            {
                sb.AppendLine(
                    "VERDICT ANCRAGES: déplacés (attendu — nouvelles graines CountryId). " +
                    "Les NOUVELLES valeurs ci-dessus sont le jeu de référence.");
            }

            sb.AppendLine(plausible
                ? "VERDICT PLAUSIBILITÉ: OUI — monde reste jouable."
                : "VERDICT PLAUSIBILITÉ: NON — métrique hors bornes (voir ci-dessus).");

            File.WriteAllText(measurePath, sb.ToString());
            UnityEngine.Debug.Log(sb.ToString());

            Assert.Greater(t1000.Population, 0, "population nulle — harnais défaillant.");
            Assert.IsTrue(plausible, "Contrôle de sanité échoué — voir Logs/v1_010_measurements.log");
        }

        static void AppendFirstDivergenceDichotomy(StringBuilder sb)
        {
            int[] ticks = { 1, 2, 5, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 50, 100 };
            sb.AppendLine("=== DICHOTOMIE premier tick divergent ===");

            foreach (var tick in ticks)
            {
                var measured = CaptureHarnessAtTick(Seed, tick);
                var played = CaptureDefaultWorldAtTick(Seed, tick);
                var diffs = DiffNine(played, measured);
                sb.AppendLine($"t={tick}: écarts={diffs.Count}");
                foreach (var d in diffs)
                    sb.AppendLine("  " + d);

                if (diffs.Count > 0)
                {
                    sb.AppendLine($"PREMIER TICK DIVERGENT: t={tick}");
                    break;
                }
            }
        }

        static WorldMetrics.Snapshot CaptureHarnessAtTick(
            uint seed, int tick, bool installNoOp = false)
        {
            using var harness = new SimulationHarness(seed, installNoOp);
            harness.RunTicks(tick);
            return WorldMetrics.Capture(harness.EntityManager, tick);
        }

        static WorldMetrics.Snapshot CaptureDefaultWorldAtTick(uint seed, int tick)
        {
            WorldBootstrapConfig.GlobalSeedOverride = seed;
            // Même garde que SimulationHarness : le JSON adopté (cAbs=0.5) ne doit
            // pas faire diverger joué vs mesuré — la parité se mesure à c=0/0.
            TaxPhysicalWithdrawalSystem.EnsureParitySafeDefaults();
            try
            {
                var old = Unity.Entities.World.DefaultGameObjectInjectionWorld;
                if (old != null && old.IsCreated)
                    old.Dispose();

                DefaultWorldInitialization.Initialize("V1010ParityWorld", false);
                var world = Unity.Entities.World.DefaultGameObjectInjectionWorld;
                world.GetExistingSystemManaged<InitializationSystemGroup>().Update();
                var sim = world.GetExistingSystemManaged<SimulationSystemGroup>();
                for (var i = 0; i < tick; i++)
                    sim.Update();

                var snap = WorldMetrics.Capture(world.EntityManager, tick);
                world.Dispose();
                Unity.Entities.World.DefaultGameObjectInjectionWorld = null;
                return snap;
            }
            finally
            {
                WorldBootstrapConfig.ClearOverride();
                TaxPhysicalWithdrawalSystem.ResetToCompiledDefault();
            }
        }

        static List<string> DiffNine(in WorldMetrics.Snapshot a, in WorldMetrics.Snapshot b)
        {
            var diffs = new List<string>();
            void Cmp(string name, string av, string bv)
            {
                if (av != bv)
                    diffs.Add($"{name}: joué={av} mesuré={bv}");
            }

            Cmp("warsDeclared", a.WarsDeclared.ToString(), b.WarsDeclared.ToString());
            Cmp("victories", a.Victories.ToString(), b.Victories.ToString());
            Cmp("annexedProvinces", a.AnnexedProvinces.ToString(), b.AnnexedProvinces.ToString());
            Cmp("maxProvinces", a.MaxProvincesOneCountry.ToString(), b.MaxProvincesOneCountry.ToString());
            Cmp("worldArmyStr", WorldMetrics.Fmt0(a.WorldArmyStr), WorldMetrics.Fmt0(b.WorldArmyStr));
            Cmp("totalRegiments", a.TotalRegiments.ToString(), b.TotalRegiments.ToString());
            Cmp("population", a.Population.ToString(), b.Population.ToString());
            Cmp("needsSatAvg", WorldMetrics.Fmt3(a.NeedsSatAvg), WorldMetrics.Fmt3(b.NeedsSatAvg));
            Cmp("livingArmies", a.LivingArmies.ToString(), b.LivingArmies.ToString());
            return diffs;
        }

        static bool Check(StringBuilder sb, string name, string expected, string actual, bool ok)
        {
            sb.AppendLine(
                $"{name}: ancien={expected} nouveau={actual} {(ok ? "SAME" : "MOVED")}");
            return ok;
        }
    }
}
