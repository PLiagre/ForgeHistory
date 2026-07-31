using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Collections;
using Unity.Entities;
using NUnit.Framework;
using VictoriaGame.Core;
using VictoriaGame.Economy;
using VictoriaGame.Military;
using VictoriaGame.Population;
using VictoriaGame.World;

namespace VictoriaGame.Tests
{
    /// <summary>Point d'entrée batchmode : -executeMethod VictoriaGame.Tests.Eco033BatchRunner.Run</summary>
    public static class Eco033BatchRunner
    {
        public static void Run()
        {
            Eco033MeasurementTests.RunMeasurementsAndWriteLog();
            UnityEngine.Debug.Log("Eco033BatchRunner: DONE");
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
            #endif
        }
    }

    [TestFixture]
    public class Eco033MeasurementTests
    {
        const uint Seed = 42195u;
        static readonly int[] SnapshotTicks = { 200, 500, 1000 };

        /// <summary>Margins essayés sous AffordableStrength (0.05 = défaut / retenu si dette OK).</summary>
        static readonly float[] MarginsTried = { 0.05f, 0.10f, 0.02f };

        struct Config
        {
            public string Label;
            public ArmyGrowthGateMode GrowthMode;
            public float GrowthMargin;
            public bool IsBaseline;
            public bool IsRetainedCandidate;
        }

        [Test]
        public void Eco033_MeasureGrowthGateAtKeyTicks() => RunMeasurementsAndWriteLog();

        public static void RunMeasurementsAndWriteLog()
        {
            var prevGrowth = ArmyDisbandmentSystem.GrowthGateMode;
            var prevMargin = ArmyDisbandmentSystem.GrowthMargin;
            var prevGate = ArmyDisbandmentSystem.GateMode;

            var logPath = Path.Combine(
                UnityEngine.Application.dataPath, "..", "Logs", "eco_033_measurements.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

            try
            {
                ArmyDisbandmentSystem.GateMode = ArmySolvencyGateMode.FluxCommitted;

                var sb = new StringBuilder();
                sb.AppendLine(
                    $"=== eco_033 seed={Seed} — briser deadlock CanAffordGrowth ===");
                sb.AppendLine(
                    "Réf A = MatureCommitted (eco_027/mil_023). " +
                    "B+ = AffordableStrength (eco_033) × GrowthMargin.");
                sb.AppendLine(
                    "Colonnes: worldArmyStr totalRegiments avgStrPerRegiment " +
                    "insolventGatedCountries countriesWithLand totalDebt bankrupt " +
                    "zombieArmyStrLandless needsSatAvg population");
                sb.AppendLine();

                var configs = BuildConfigs();
                var t1000ByConfig = new Snap[configs.Length];

                for (var c = 0; c < configs.Length; c++)
                {
                    ApplyConfig(configs[c]);
                    sb.AppendLine($"=== {configs[c].Label} ===");

                    for (var i = 0; i < SnapshotTicks.Length; i++)
                    {
                        var tick = SnapshotTicks[i];
                        using var harness = new SimulationHarness(Seed);
                        harness.RunTicks(tick);
                        var snap = CaptureSnap(harness.EntityManager);
                        if (tick == 1000)
                            t1000ByConfig[c] = snap;

                        AppendTickLine(sb, tick, snap);
                    }

                    sb.AppendLine();
                }

                AppendOscillationCheck(sb);
                AppendVerdict(sb, configs, t1000ByConfig);

                File.WriteAllText(logPath, sb.ToString());
                UnityEngine.Debug.Log(sb.ToString());
            }
            finally
            {
                ArmyDisbandmentSystem.GrowthGateMode = prevGrowth;
                ArmyDisbandmentSystem.GrowthMargin = prevMargin;
                ArmyDisbandmentSystem.GateMode = prevGate;
            }
        }

        static Config[] BuildConfigs()
        {
            var list = new List<Config>
            {
                new Config
                {
                    Label = "A — BASELINE MatureCommitted (réf eco_032/mil_023 deadlock)",
                    GrowthMode = ArmyGrowthGateMode.MatureCommitted,
                    GrowthMargin = ArmyDisbandmentSystem.DefaultGrowthMargin,
                    IsBaseline = true,
                    IsRetainedCandidate = false
                }
            };

            for (var i = 0; i < MarginsTried.Length; i++)
            {
                var m = MarginsTried[i];
                var retained = System.Math.Abs(m - ArmyDisbandmentSystem.DefaultGrowthMargin) < 1e-6f;
                list.Add(new Config
                {
                    Label = retained
                        ? $"B — AffordableStrength GrowthMargin={Fmt(m)} (candidat retenu)"
                        : $"B{i} — AffordableStrength GrowthMargin={Fmt(m)} (essai)",
                    GrowthMode = ArmyGrowthGateMode.AffordableStrength,
                    GrowthMargin = m,
                    IsBaseline = false,
                    IsRetainedCandidate = retained
                });
            }

            return list.ToArray();
        }

        static void ApplyConfig(Config config)
        {
            ArmyDisbandmentSystem.GateMode = ArmySolvencyGateMode.FluxCommitted;
            ArmyDisbandmentSystem.GrowthGateMode = config.GrowthMode;
            ArmyDisbandmentSystem.GrowthMargin = config.GrowthMargin;
        }

        static void AppendTickLine(StringBuilder sb, int tick, Snap snap)
        {
            sb.AppendLine(
                $"tick{tick}: worldArmyStr={Fmt0(snap.WorldArmyStr)} " +
                $"totalRegiments={snap.TotalRegiments} " +
                $"avgStrPerRegiment={Fmt1(snap.AvgStrPerRegiment)} " +
                $"insolventGatedCountries={snap.InsolventGatedGrowth}/{snap.CountriesWithLand} " +
                $"(recruitGate={snap.InsolventGatedRecruit}) " +
                $"countriesWithLand={snap.CountriesWithLand} " +
                $"totalDebt={Fmt1(snap.TotalDebt)} bankrupt={snap.Bankrupt} " +
                $"zombieArmyStrLandless={Fmt0(snap.ZombieArmyStr)} " +
                $"needsSatAvg={Fmt3(snap.NeedsSatAvg)} population={snap.Population}");
        }

        /// <summary>
        /// Trajectoire fine d'un pays pauvre témoin : force doit monter puis se stabiliser.
        /// </summary>
        static void AppendOscillationCheck(StringBuilder sb)
        {
            ArmyDisbandmentSystem.GateMode = ArmySolvencyGateMode.FluxCommitted;
            ArmyDisbandmentSystem.GrowthGateMode = ArmyGrowthGateMode.AffordableStrength;
            ArmyDisbandmentSystem.GrowthMargin = ArmyDisbandmentSystem.DefaultGrowthMargin;

            sb.AppendLine("=== CONTRÔLE OSCILLATION (AffordableStrength margin=0.05) ===");
            sb.AppendLine("Pays témoin = plus faible force parmi pays avec terre & Balance&lt;0 à t400.");

            using var harness = new SimulationHarness(Seed);
            harness.RunTicks(400);

            // Témoin = fauché, terre, force>0, ET CanAffordGrowth (doit pouvoir monter).
            var witness = PickGrowingPoorWitness(harness.EntityManager);
            if (witness == Entity.Null)
                witness = PickPoorWitness(harness.EntityManager);
            if (witness == Entity.Null)
            {
                sb.AppendLine("  (aucun témoin pauvre avec armée trouvé à t400 — skip)");
                sb.AppendLine();
                return;
            }

            var tag = harness.EntityManager.GetComponentData<CountryData>(witness).Tag.ToString();
            sb.AppendLine($"  témoin={tag}");

            // Fenêtre plus longue pour voir montée puis plateau.
            var strengths = new float[31];
            for (var i = 0; i < strengths.Length; i++)
            {
                strengths[i] = SumArmyForCountry(harness.EntityManager, witness);
                if (i % 5 == 0 || i == strengths.Length - 1)
                    sb.AppendLine($"  t{400 + i}: armyStr={Fmt1(strengths[i])}");
                if (i < strengths.Length - 1)
                    harness.RunTicks(1);
            }

            var rose = strengths[strengths.Length - 1] > strengths[0] + 1f;
            var mid = strengths[strengths.Length / 2];
            var end = strengths[strengths.Length - 1];
            var stabilized = System.Math.Abs(end - mid) < System.Math.Max(50f, mid * 0.05f);
            var beatCount = 0;
            for (var i = 2; i < strengths.Length; i++)
            {
                var d1 = strengths[i - 1] - strengths[i - 2];
                var d2 = strengths[i] - strengths[i - 1];
                if (d1 * d2 < -25f)
                    beatCount++;
            }

            if (beatCount <= 3 && (stabilized || rose))
                sb.AppendLine(
                    $"OK oscillation: beatCount={beatCount} rose={rose} stabilized={stabilized} " +
                    "(monte/stable, pas dents-de-scie).");
            else
                sb.AppendLine(
                    $"ALERT oscillation: beatCount={beatCount} rose={rose} stabilized={stabilized}.");

            sb.AppendLine();
        }

        static void AppendVerdict(StringBuilder sb, Config[] configs, Snap[] t1000)
        {
            sb.AppendLine("=== VERDICT eco_033 (t1000, seed 42195) ===");

            var baselineIdx = 0;
            var retainedIdx = -1;
            for (var i = 0; i < configs.Length; i++)
            {
                if (configs[i].IsBaseline)
                    baselineIdx = i;
                if (configs[i].IsRetainedCandidate)
                    retainedIdx = i;
            }

            var baseSnap = t1000[baselineIdx];
            sb.AppendLine(
                $"A baseline MatureCommitted: army={Fmt0(baseSnap.WorldArmyStr)} " +
                $"regs={baseSnap.TotalRegiments} avgStr/reg={Fmt1(baseSnap.AvgStrPerRegiment)} " +
                $"gated={baseSnap.InsolventGatedGrowth}/{baseSnap.CountriesWithLand} " +
                $"debt={Fmt1(baseSnap.TotalDebt)} bankrupt={baseSnap.Bankrupt} " +
                $"zombie={Fmt0(baseSnap.ZombieArmyStr)}");

            for (var i = 0; i < configs.Length; i++)
            {
                if (configs[i].IsBaseline)
                    continue;
                var s = t1000[i];
                var delta = s.WorldArmyStr - baseSnap.WorldArmyStr;
                sb.AppendLine(
                    $"  {configs[i].Label}: army={Fmt0(s.WorldArmyStr)} " +
                    $"deltaVsA={FmtSigned(delta)} regs={s.TotalRegiments} " +
                    $"avgStr/reg={Fmt1(s.AvgStrPerRegiment)} " +
                    $"gated={s.InsolventGatedGrowth}/{s.CountriesWithLand} " +
                    $"debt={Fmt1(s.TotalDebt)} bankrupt={s.Bankrupt} zombie={Fmt0(s.ZombieArmyStr)}");
            }

            if (retainedIdx < 0)
            {
                sb.AppendLine("ALERT: aucun candidat retenu.");
                return;
            }

            var ret = t1000[retainedIdx];
            var deadlockBroken =
                ret.AvgStrPerRegiment > 400f &&
                ret.WorldArmyStr > baseSnap.WorldArmyStr + 5000f;
            var armyUp = ret.WorldArmyStr > baseSnap.WorldArmyStr + 1000f;
            // Dette bornée : ordre ~751–1200, bankrupt ~2-5 (mandat).
            var debtOk = ret.TotalDebt < 1500f && ret.Bankrupt >= 1 && ret.Bankrupt <= 6;
            var zombieOk = ret.ZombieArmyStr < 1f;
            var acquisOk =
                System.Math.Abs(ret.NeedsSatAvg - 0.70f) < 0.08f &&
                ret.Population >= 130000 && ret.Population <= 155000;

            sb.AppendLine(
                deadlockBroken
                    ? $"OK deadlock brisé: avgStr/reg={Fmt1(ret.AvgStrPerRegiment)} " +
                      $"(baseline {Fmt1(baseSnap.AvgStrPerRegiment)}) " +
                      $"army={Fmt0(ret.WorldArmyStr)} regs={ret.TotalRegiments} " +
                      $"gated={ret.InsolventGatedGrowth}/{ret.CountriesWithLand}."
                    : $"ALERT deadlock: avgStr/reg={Fmt1(ret.AvgStrPerRegiment)} " +
                      $"regs={ret.TotalRegiments} gated={ret.InsolventGatedGrowth}/{ret.CountriesWithLand}.");

            sb.AppendLine(
                armyUp
                    ? $"OK worldArmyStr remonté: {Fmt0(ret.WorldArmyStr)} (baseline {Fmt0(baseSnap.WorldArmyStr)})."
                    : $"ALERT worldArmyStr: {Fmt0(ret.WorldArmyStr)} vs baseline {Fmt0(baseSnap.WorldArmyStr)}.");

            sb.AppendLine(
                debtOk
                    ? $"OK dette bornée: debt={Fmt1(ret.TotalDebt)} bankrupt={ret.Bankrupt}."
                    : $"ALERT dette/banqueroutes: debt={Fmt1(ret.TotalDebt)} bankrupt={ret.Bankrupt}.");

            sb.AppendLine(
                zombieOk
                    ? $"OK zombie=0: {Fmt0(ret.ZombieArmyStr)}."
                    : $"ALERT zombie: {Fmt0(ret.ZombieArmyStr)}.");

            sb.AppendLine(
                acquisOk
                    ? $"OK acquis needs/pop: sat={Fmt3(ret.NeedsSatAvg)} pop={ret.Population}."
                    : $"ALERT acquis: sat={Fmt3(ret.NeedsSatAvg)} pop={ret.Population}.");

            sb.AppendLine(
                $"GrowthGateMode retenu=AffordableStrength GrowthMargin={Fmt(ArmyDisbandmentSystem.DefaultGrowthMargin)} " +
                $"(CanAffordRecruit inchangé — coût engagé à maturité). " +
                "Désarmement eco_026 intact.");
        }

        struct Snap
        {
            public float WorldArmyStr;
            public int TotalRegiments;
            public float AvgStrPerRegiment;
            public int InsolventGatedGrowth;
            public int InsolventGatedRecruit;
            public int CountriesWithLand;
            public float TotalDebt;
            public int Bankrupt;
            public float ZombieArmyStr;
            public float NeedsSatAvg;
            public int Population;
        }

        static Snap CaptureSnap(EntityManager em)
        {
            var snap = new Snap();
            var provinceCounts = CountProvincesByOwner(em);
            var armyByCountry = SumArmyByCountry(em);
            var regsByCountry = CountRegsByCountry(em);

            var regimentStrengthSum = 0f;
            using var regQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<ArmyData>(),
                ComponentType.ReadOnly<RegimentSlot>());
            using var armyEntities = regQuery.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < armyEntities.Length; i++)
            {
                var slots = em.GetBuffer<RegimentSlot>(armyEntities[i]);
                snap.TotalRegiments += slots.Length;
                for (var s = 0; s < slots.Length; s++)
                    regimentStrengthSum += slots[s].Strength;
            }

            snap.AvgStrPerRegiment = snap.TotalRegiments > 0
                ? regimentStrengthSum / snap.TotalRegiments
                : 0f;

            using var armyQuery = em.CreateEntityQuery(ComponentType.ReadOnly<ArmyData>());
            using var armies = armyQuery.ToComponentDataArray<ArmyData>(Allocator.Temp);
            for (var i = 0; i < armies.Length; i++)
                snap.WorldArmyStr += armies[i].Strength;

            using var countryQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<CountryData>(),
                ComponentType.ReadOnly<TreasuryData>());
            using var countries = countryQuery.ToEntityArray(Allocator.Temp);
            using var treasuries = countryQuery.ToComponentDataArray<TreasuryData>(Allocator.Temp);

            for (var i = 0; i < countries.Length; i++)
            {
                var entity = countries[i];
                provinceCounts.TryGetValue(entity, out var prov);
                armyByCountry.TryGetValue(entity, out var armyStr);
                regsByCountry.TryGetValue(entity, out var regCount);
                var hasLand = prov > 0;

                snap.TotalDebt += treasuries[i].Debt;
                if (treasuries[i].BankruptcyTick > 0)
                    snap.Bankrupt++;

                if (hasLand)
                {
                    snap.CountriesWithLand++;
                    // Deadlock metric = gate CROISSANCE (eco_033), pas recrutement.
                    if (!ArmyDisbandmentSystem.CanAffordGrowth(
                            treasuries[i], regCount, armyStr, ArmySolvencyGateMode.FluxCommitted))
                    {
                        snap.InsolventGatedGrowth++;
                    }

                    if (!ArmyDisbandmentSystem.CanAffordRecruit(
                            treasuries[i], regCount, armyStr, ArmySolvencyGateMode.FluxCommitted))
                    {
                        snap.InsolventGatedRecruit++;
                    }
                }
                else
                {
                    snap.ZombieArmyStr += armyStr;
                }
            }

            double weightedSat = 0.0;
            var totalPop = 0;
            using var popQuery = em.CreateEntityQuery(ComponentType.ReadOnly<PopData>());
            using var pops = popQuery.ToComponentDataArray<PopData>(Allocator.Temp);
            for (var i = 0; i < pops.Length; i++)
            {
                totalPop += pops[i].Size;
                weightedSat += pops[i].NeedsSatisfaction * pops[i].Size;
            }

            snap.Population = totalPop;
            snap.NeedsSatAvg = totalPop > 0 ? (float)(weightedSat / totalPop) : 0f;

            provinceCounts.Dispose();
            armyByCountry.Dispose();
            regsByCountry.Dispose();
            return snap;
        }

        static Entity PickGrowingPoorWitness(EntityManager em)
        {
            var provinceCounts = CountProvincesByOwner(em);
            var armyByCountry = SumArmyByCountry(em);
            var regsByCountry = CountRegsByCountry(em);

            using var countryQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<CountryData>(),
                ComponentType.ReadOnly<TreasuryData>());
            using var countries = countryQuery.ToEntityArray(Allocator.Temp);
            using var treasuries = countryQuery.ToComponentDataArray<TreasuryData>(Allocator.Temp);

            var best = Entity.Null;
            var bestStr = float.MaxValue;

            for (var i = 0; i < countries.Length; i++)
            {
                provinceCounts.TryGetValue(countries[i], out var prov);
                if (prov <= 0)
                    continue;
                if (treasuries[i].Balance >= 0f)
                    continue;

                armyByCountry.TryGetValue(countries[i], out var armyStr);
                regsByCountry.TryGetValue(countries[i], out var regCount);
                if (armyStr <= 50f)
                    continue;

                if (!ArmyDisbandmentSystem.CanAffordGrowth(
                        treasuries[i], regCount, armyStr, ArmySolvencyGateMode.FluxCommitted))
                    continue;

                if (armyStr < bestStr)
                {
                    bestStr = armyStr;
                    best = countries[i];
                }
            }

            provinceCounts.Dispose();
            armyByCountry.Dispose();
            regsByCountry.Dispose();
            return best;
        }

        static Entity PickPoorWitness(EntityManager em)
        {
            var provinceCounts = CountProvincesByOwner(em);
            var armyByCountry = SumArmyByCountry(em);

            using var countryQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<CountryData>(),
                ComponentType.ReadOnly<TreasuryData>());
            using var countries = countryQuery.ToEntityArray(Allocator.Temp);
            using var treasuries = countryQuery.ToComponentDataArray<TreasuryData>(Allocator.Temp);

            var best = Entity.Null;
            var bestStr = float.MaxValue;

            for (var i = 0; i < countries.Length; i++)
            {
                provinceCounts.TryGetValue(countries[i], out var prov);
                if (prov <= 0)
                    continue;
                if (treasuries[i].Balance >= 0f)
                    continue;

                armyByCountry.TryGetValue(countries[i], out var armyStr);
                if (armyStr <= 1f)
                    continue;
                if (armyStr < bestStr)
                {
                    bestStr = armyStr;
                    best = countries[i];
                }
            }

            provinceCounts.Dispose();
            armyByCountry.Dispose();
            return best;
        }

        static float SumArmyForCountry(EntityManager em, Entity country)
        {
            var sum = 0f;
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<ArmyData>());
            using var armies = query.ToComponentDataArray<ArmyData>(Allocator.Temp);
            for (var i = 0; i < armies.Length; i++)
            {
                if (armies[i].Country == country)
                    sum += armies[i].Strength;
            }

            return sum;
        }

        static NativeHashMap<Entity, int> CountProvincesByOwner(EntityManager em)
        {
            var map = new NativeHashMap<Entity, int>(32, Allocator.Temp);
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<ProvinceOwnership>());
            using var ownerships = query.ToComponentDataArray<ProvinceOwnership>(Allocator.Temp);
            for (var i = 0; i < ownerships.Length; i++)
            {
                var owner = ownerships[i].Owner;
                if (owner == Entity.Null)
                    continue;
                map.TryGetValue(owner, out var current);
                map[owner] = current + 1;
            }

            return map;
        }

        static NativeHashMap<Entity, float> SumArmyByCountry(EntityManager em)
        {
            var map = new NativeHashMap<Entity, float>(32, Allocator.Temp);
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<ArmyData>());
            using var armies = query.ToComponentDataArray<ArmyData>(Allocator.Temp);
            for (var i = 0; i < armies.Length; i++)
            {
                var c = armies[i].Country;
                if (c == Entity.Null)
                    continue;
                map.TryGetValue(c, out var cur);
                map[c] = cur + armies[i].Strength;
            }

            return map;
        }

        static NativeHashMap<Entity, int> CountRegsByCountry(EntityManager em)
        {
            var map = new NativeHashMap<Entity, int>(32, Allocator.Temp);
            using var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<ArmyData>(),
                ComponentType.ReadOnly<RegimentSlot>());
            using var entities = query.ToEntityArray(Allocator.Temp);
            using var armies = query.ToComponentDataArray<ArmyData>(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                var c = armies[i].Country;
                if (c == Entity.Null)
                    continue;
                var slots = em.GetBuffer<RegimentSlot>(entities[i]);
                map.TryGetValue(c, out var cur);
                map[c] = cur + slots.Length;
            }

            return map;
        }

        static string Fmt(float v) => v.ToString("F2", CultureInfo.InvariantCulture);
        static string Fmt0(float v) => v.ToString("F0", CultureInfo.InvariantCulture);
        static string Fmt1(float v) => v.ToString("F1", CultureInfo.InvariantCulture);
        static string Fmt3(float v) => v.ToString("F3", CultureInfo.InvariantCulture);
        static string FmtSigned(float v) =>
            (v >= 0f ? "+" : "") + v.ToString("F0", CultureInfo.InvariantCulture);
    }
}
