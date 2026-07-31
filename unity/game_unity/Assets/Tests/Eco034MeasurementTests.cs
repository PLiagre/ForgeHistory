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
    /// <summary>Point d'entrée batchmode : -executeMethod VictoriaGame.Tests.Eco034BatchRunner.Run</summary>
    public static class Eco034BatchRunner
    {
        public static void Run()
        {
            Eco034MeasurementTests.RunMeasurementsAndWriteLog();
            UnityEngine.Debug.Log("Eco034BatchRunner: DONE");
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
            #endif
        }
    }

    [TestFixture]
    public class Eco034MeasurementTests
    {
        const uint Seed = 42195u;
        static readonly int[] SnapshotTicks = { 200, 500, 1000 };

        /// <summary>
        /// Facteurs essayés (réparation vétérans uniquement). 1.0 = ancrage eco_033.
        /// </summary>
        static readonly float[] FactorsTried =
            { 1.0f, 0.5f, 0.2f, 0.16f, 0.12f, 0.05f };

        const float TargetArmy = 40000f;
        const float BandLow = 36000f;
        const float BandHigh = 44000f;

        struct Config
        {
            public string Label;
            public float ReinforceRateFactor;
            public bool IsAnchor;
            public bool IsRetainedCandidate;
        }

        [Test]
        public void Eco034_MeasureReinforceRateAtKeyTicks() => RunMeasurementsAndWriteLog();

        public static void RunMeasurementsAndWriteLog()
        {
            var prevGrowth = ArmyDisbandmentSystem.GrowthGateMode;
            var prevMargin = ArmyDisbandmentSystem.GrowthMargin;
            var prevGate = ArmyDisbandmentSystem.GateMode;
            var prevFactor = ArmyDisbandmentSystem.ReinforceRateFactor;

            var logPath = Path.Combine(
                UnityEngine.Application.dataPath, "..", "Logs", "eco_034_measurements.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

            try
            {
                ArmyDisbandmentSystem.GateMode = ArmySolvencyGateMode.FluxCommitted;
                ArmyDisbandmentSystem.GrowthGateMode = ArmyGrowthGateMode.AffordableStrength;
                ArmyDisbandmentSystem.GrowthMargin = ArmyDisbandmentSystem.DefaultGrowthMargin;

                var sb = new StringBuilder();
                sb.AppendLine(
                    $"=== eco_034 seed={Seed} — REINFORCE_RATE_FACTOR (réparation vétérans) ===");
                sb.AppendLine(
                    "Design: facteur appliqué SEULEMENT si !wasRecruiting (vétéran). " +
                    "Recrutement initial à pleine vitesse. IsRecruiting non forcé sur vétérans.");
                sb.AppendLine(
                    "Ancrage: facteur=1.0 doit reproduire eco_033 (worldArmyStr=69403).");
                sb.AppendLine(
                    "Colonnes: worldArmyStr totalRegiments avgStrPerRegiment " +
                    "totalDebt bankrupt zombieArmyStrLandless needsSatAvg population");
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

                AppendCalibrationCurve(sb, configs, t1000ByConfig);
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
                ArmyDisbandmentSystem.ReinforceRateFactor = prevFactor;
            }
        }

        static Config[] BuildConfigs()
        {
            var list = new List<Config>();
            for (var i = 0; i < FactorsTried.Length; i++)
            {
                var f = FactorsTried[i];
                var isAnchor = System.Math.Abs(f - 1.0f) < 1e-6f;
                var isRetained =
                    System.Math.Abs(f - ArmyDisbandmentSystem.DefaultReinforceRateFactor) < 1e-6f;
                var tag = isAnchor
                    ? "A — ANCRAGE eco_033"
                    : isRetained
                        ? "R — RETENU (défaut production)"
                        : $"F{i}";
                list.Add(new Config
                {
                    Label = $"{tag} ReinforceRateFactor={Fmt(f)}",
                    ReinforceRateFactor = f,
                    IsAnchor = isAnchor,
                    IsRetainedCandidate = isRetained
                });
            }

            // Si le défaut n'est pas dans FactorsTried, l'ajouter.
            var hasDefault = false;
            for (var i = 0; i < FactorsTried.Length; i++)
            {
                if (System.Math.Abs(FactorsTried[i] - ArmyDisbandmentSystem.DefaultReinforceRateFactor) < 1e-6f)
                    hasDefault = true;
            }

            if (!hasDefault)
            {
                var d = ArmyDisbandmentSystem.DefaultReinforceRateFactor;
                list.Add(new Config
                {
                    Label = $"R — RETENU (défaut production) ReinforceRateFactor={Fmt(d)}",
                    ReinforceRateFactor = d,
                    IsAnchor = false,
                    IsRetainedCandidate = true
                });
            }

            return list.ToArray();
        }

        static void ApplyConfig(Config config)
        {
            ArmyDisbandmentSystem.GateMode = ArmySolvencyGateMode.FluxCommitted;
            ArmyDisbandmentSystem.GrowthGateMode = ArmyGrowthGateMode.AffordableStrength;
            ArmyDisbandmentSystem.GrowthMargin = ArmyDisbandmentSystem.DefaultGrowthMargin;
            ArmyDisbandmentSystem.ReinforceRateFactor = config.ReinforceRateFactor;
        }

        static void AppendTickLine(StringBuilder sb, int tick, Snap snap)
        {
            sb.AppendLine(
                $"tick{tick}: worldArmyStr={Fmt0(snap.WorldArmyStr)} " +
                $"totalRegiments={snap.TotalRegiments} " +
                $"avgStrPerRegiment={Fmt1(snap.AvgStrPerRegiment)} " +
                $"totalDebt={Fmt1(snap.TotalDebt)} bankrupt={snap.Bankrupt} " +
                $"zombieArmyStrLandless={Fmt0(snap.ZombieArmyStr)} " +
                $"needsSatAvg={Fmt3(snap.NeedsSatAvg)} population={snap.Population}");
        }

        static void AppendCalibrationCurve(StringBuilder sb, Config[] configs, Snap[] t1000)
        {
            sb.AppendLine("=== COURBE DE CALIBRATION (t1000, seed 42195) ===");
            sb.AppendLine(
                "facteur | worldArmyStr | regs | avgStr/reg | debt | bankrupt | zombie | sat | pop");

            var distinct = true;
            float? prevArmy = null;
            var monotone = true;

            for (var i = 0; i < configs.Length; i++)
            {
                // Trier par facteur décroissant pour la courbe (déjà dans FactorsTried).
                var s = t1000[i];
                sb.AppendLine(
                    $"  {Fmt(configs[i].ReinforceRateFactor)} | {Fmt0(s.WorldArmyStr)} | " +
                    $"{s.TotalRegiments} | {Fmt1(s.AvgStrPerRegiment)} | {Fmt1(s.TotalDebt)} | " +
                    $"{s.Bankrupt} | {Fmt0(s.ZombieArmyStr)} | {Fmt3(s.NeedsSatAvg)} | {s.Population}");

                if (prevArmy.HasValue)
                {
                    if (System.Math.Abs(s.WorldArmyStr - prevArmy.Value) < 0.5f)
                        distinct = false;
                    // Facteurs décroissants → armée doit décroître (monotone).
                    if (configs[i].ReinforceRateFactor < configs[i - 1].ReinforceRateFactor
                        && s.WorldArmyStr > prevArmy.Value + 1f)
                        monotone = false;
                    if (configs[i].ReinforceRateFactor > configs[i - 1].ReinforceRateFactor
                        && s.WorldArmyStr < prevArmy.Value - 1f)
                        monotone = false;
                }

                prevArmy = s.WorldArmyStr;
            }

            sb.AppendLine(
                distinct && monotone
                    ? "OK levier DOSE: valeurs DISTINCTES et MONOTONES."
                    : $"ALERT levier: distinct={distinct} monotone={monotone} " +
                      "(si identiques → le facteur ne mord pas).");
            sb.AppendLine();
        }

        /// <summary>
        /// Témoin viable (terre, flux ≥ 0) : monte puis se stabilise.
        /// Témoin insolvable : peut décroître (discipline eco_026).
        /// </summary>
        static void AppendOscillationCheck(StringBuilder sb)
        {
            ArmyDisbandmentSystem.GateMode = ArmySolvencyGateMode.FluxCommitted;
            ArmyDisbandmentSystem.GrowthGateMode = ArmyGrowthGateMode.AffordableStrength;
            ArmyDisbandmentSystem.GrowthMargin = ArmyDisbandmentSystem.DefaultGrowthMargin;
            ArmyDisbandmentSystem.ReinforceRateFactor =
                ArmyDisbandmentSystem.DefaultReinforceRateFactor;

            sb.AppendLine(
                $"=== CONTRÔLE OSCILLATION (ReinforceRateFactor=" +
                $"{Fmt(ArmyDisbandmentSystem.DefaultReinforceRateFactor)}) ===");

            using var harness = new SimulationHarness(Seed);
            harness.RunTicks(400);

            var viable = PickViableWitness(harness.EntityManager);
            var insolvent = PickInsolventWitness(harness.EntityManager);

            if (viable != Entity.Null)
            {
                var tag = harness.EntityManager.GetComponentData<CountryData>(viable).Tag.ToString();
                var treas = harness.EntityManager.GetComponentData<TreasuryData>(viable);
                var flux = treas.Income - treas.Expenses;
                sb.AppendLine(
                    $"  TÉMOIN VIABLE={tag} flux={Fmt1(flux)} " +
                    $"(Income={Fmt1(treas.Income)} Expenses={Fmt1(treas.Expenses)})");
                AppendWitnessTrajectory(sb, harness, viable, 400, 16, "viable");
            }
            else
            {
                sb.AppendLine("  (aucun témoin viable trouvé à t400 — skip)");
            }

            // Nouveau harness pour l'insolvent (évite dérive après trajectoire viable).
            using var harness2 = new SimulationHarness(Seed);
            harness2.RunTicks(400);
            insolvent = PickInsolventWitness(harness2.EntityManager);
            if (insolvent == Entity.Null)
                insolvent = PickPoorWitness(harness2.EntityManager);

            if (insolvent != Entity.Null)
            {
                var tag = harness2.EntityManager.GetComponentData<CountryData>(insolvent).Tag.ToString();
                var treas = harness2.EntityManager.GetComponentData<TreasuryData>(insolvent);
                var flux = treas.Income - treas.Expenses;
                sb.AppendLine(
                    $"  TÉMOIN INSOLVABLE={tag} flux={Fmt1(flux)} Balance={Fmt1(treas.Balance)}");
                AppendWitnessTrajectory(sb, harness2, insolvent, 400, 16, "insolvent");
            }
            else
            {
                sb.AppendLine("  (aucun témoin insolvable — skip)");
            }

            sb.AppendLine();
        }

        static void AppendWitnessTrajectory(
            StringBuilder sb,
            SimulationHarness harness,
            Entity country,
            int startTick,
            int count,
            string kind)
        {
            var strengths = new float[count];
            for (var i = 0; i < strengths.Length; i++)
            {
                strengths[i] = SumArmyForCountry(harness.EntityManager, country);
                if (i % 3 == 0 || i == strengths.Length - 1)
                    sb.AppendLine($"  t{startTick + i}: armyStr={Fmt1(strengths[i])}");
                if (i < strengths.Length - 1)
                    harness.RunTicks(1);
            }

            var start = strengths[0];
            var mid = strengths[strengths.Length / 2];
            var end = strengths[strengths.Length - 1];
            var rose = end > start + 1f;
            var fell = end < start - 1f;
            var stabilized = System.Math.Abs(end - mid) < System.Math.Max(50f, mid * 0.05f);
            var beatCount = 0;
            for (var i = 2; i < strengths.Length; i++)
            {
                var d1 = strengths[i - 1] - strengths[i - 2];
                var d2 = strengths[i] - strengths[i - 1];
                if (d1 * d2 < -25f)
                    beatCount++;
            }

            if (kind == "viable")
            {
                if (beatCount <= 3 && (stabilized || rose) && !fell)
                    sb.AppendLine(
                        $"OK oscillation viable: beatCount={beatCount} rose={rose} " +
                        $"stabilized={stabilized} (monte/stable, pas dents-de-scie).");
                else
                    sb.AppendLine(
                        $"ALERT oscillation viable: beatCount={beatCount} rose={rose} " +
                        $"stabilized={stabilized} fell={fell}.");
            }
            else
            {
                sb.AppendLine(
                    $"INFO insolvable: beatCount={beatCount} rose={rose} fell={fell} " +
                    $"stabilized={stabilized} (décroissance OK si discipline).");
            }
        }

        static void AppendVerdict(StringBuilder sb, Config[] configs, Snap[] t1000)
        {
            sb.AppendLine("=== VERDICT eco_034 (t1000, seed 42195) ===");

            var anchorIdx = -1;
            var retainedIdx = -1;
            var bestBandIdx = -1;
            var bestBandDist = float.MaxValue;

            for (var i = 0; i < configs.Length; i++)
            {
                if (configs[i].IsAnchor)
                    anchorIdx = i;
                if (configs[i].IsRetainedCandidate)
                    retainedIdx = i;

                var army = t1000[i].WorldArmyStr;
                if (army >= BandLow && army <= BandHigh)
                {
                    var dist = System.Math.Abs(army - TargetArmy);
                    if (dist < bestBandDist)
                    {
                        bestBandDist = dist;
                        bestBandIdx = i;
                    }
                }
            }

            if (anchorIdx >= 0)
            {
                var a = t1000[anchorIdx];
                var anchorOk = System.Math.Abs(a.WorldArmyStr - 69403f) < 2f;
                sb.AppendLine(
                    anchorOk
                        ? $"OK ancrage facteur=1.0 = eco_033: army={Fmt0(a.WorldArmyStr)} " +
                          $"regs={a.TotalRegiments} avgStr/reg={Fmt1(a.AvgStrPerRegiment)} " +
                          $"debt={Fmt1(a.TotalDebt)} bankrupt={a.Bankrupt}."
                        : $"ALERT ancrage facteur=1.0: army={Fmt0(a.WorldArmyStr)} " +
                          $"(attendu 69403) regs={a.TotalRegiments} " +
                          $"avgStr/reg={Fmt1(a.AvgStrPerRegiment)} debt={Fmt1(a.TotalDebt)}.");
            }

            if (bestBandIdx >= 0)
            {
                sb.AppendLine(
                    $"INFO meilleur dans bande 36k–44k: facteur=" +
                    $"{Fmt(configs[bestBandIdx].ReinforceRateFactor)} " +
                    $"army={Fmt0(t1000[bestBandIdx].WorldArmyStr)} " +
                    $"(cible ~40000).");
            }
            else
            {
                sb.AppendLine(
                    "ALERT aucun facteur essayé n'atterrit dans 36000–44000 — " +
                    "ajuster DefaultReinforceRateFactor.");
            }

            if (retainedIdx < 0)
            {
                sb.AppendLine("ALERT: aucun candidat retenu (défaut hors liste).");
                return;
            }

            var ret = t1000[retainedIdx];
            var inBand = ret.WorldArmyStr >= BandLow && ret.WorldArmyStr <= BandHigh;
            var debtOk = ret.TotalDebt < 1500f && ret.Bankrupt >= 1 && ret.Bankrupt <= 6;
            var zombieOk = ret.ZombieArmyStr < 1f;
            var avgOk = ret.AvgStrPerRegiment > 300f;
            var acquisOk =
                System.Math.Abs(ret.NeedsSatAvg - 0.70f) < 0.08f &&
                ret.Population >= 130000 && ret.Population <= 155000;

            sb.AppendLine(
                inBand
                    ? $"OK cible bande: army={Fmt0(ret.WorldArmyStr)} " +
                      $"(facteur={Fmt(configs[retainedIdx].ReinforceRateFactor)})."
                    : $"ALERT cible: army={Fmt0(ret.WorldArmyStr)} hors bande 36k–44k " +
                      $"(facteur={Fmt(configs[retainedIdx].ReinforceRateFactor)}).");

            sb.AppendLine(
                debtOk
                    ? $"OK dette bornée: debt={Fmt1(ret.TotalDebt)} bankrupt={ret.Bankrupt}."
                    : $"ALERT dette/banqueroutes: debt={Fmt1(ret.TotalDebt)} bankrupt={ret.Bankrupt}.");

            sb.AppendLine(
                zombieOk
                    ? $"OK zombie=0: {Fmt0(ret.ZombieArmyStr)}."
                    : $"ALERT zombie: {Fmt0(ret.ZombieArmyStr)}.");

            sb.AppendLine(
                avgOk
                    ? $"OK avgStr/reg={Fmt1(ret.AvgStrPerRegiment)} >> 178."
                    : $"ALERT avgStr/reg={Fmt1(ret.AvgStrPerRegiment)} trop bas.");

            sb.AppendLine(
                acquisOk
                    ? $"OK acquis needs/pop: sat={Fmt3(ret.NeedsSatAvg)} pop={ret.Population}."
                    : $"ALERT acquis: sat={Fmt3(ret.NeedsSatAvg)} pop={ret.Population}.");

            sb.AppendLine(
                $"ReinforceRateFactor retenu={Fmt(ArmyDisbandmentSystem.DefaultReinforceRateFactor)} " +
                "(réparation vétérans uniquement ; recrutement initial plein régime). " +
                "CanAffordRecruit inchangé. Désarmement eco_026 intact.");
        }

        struct Snap
        {
            public float WorldArmyStr;
            public int TotalRegiments;
            public float AvgStrPerRegiment;
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
                var hasLand = prov > 0;

                snap.TotalDebt += treasuries[i].Debt;
                if (treasuries[i].BankruptcyTick > 0)
                    snap.Bankrupt++;

                if (!hasLand)
                    snap.ZombieArmyStr += armyStr;
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
            return snap;
        }

        /// <summary>
        /// Pays avec terre, flux ≥ −0.05, armée sous-saturée pouvant encore monter.
        /// </summary>
        static Entity PickViableWitness(EntityManager em)
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
            var bestScore = float.MinValue;

            for (var i = 0; i < countries.Length; i++)
            {
                provinceCounts.TryGetValue(countries[i], out var prov);
                if (prov <= 0)
                    continue;

                armyByCountry.TryGetValue(countries[i], out var armyStr);
                regsByCountry.TryGetValue(countries[i], out var regCount);
                if (armyStr <= 100f || regCount <= 0)
                    continue;

                var flux = treasuries[i].Income - treasuries[i].Expenses;
                if (flux < -0.05f)
                    continue;

                var avgStr = armyStr / regCount;
                if (avgStr >= 950f)
                    continue;

                if (!ArmyDisbandmentSystem.CanAffordGrowth(
                        treasuries[i], regCount, armyStr, ArmySolvencyGateMode.FluxCommitted))
                    continue;

                var score = flux + (1000f - avgStr) * 0.01f;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = countries[i];
                }
            }

            provinceCounts.Dispose();
            armyByCountry.Dispose();
            regsByCountry.Dispose();
            return best;
        }

        /// <summary>Pays avec terre, Balance&lt;0, armée &gt; 0.</summary>
        static Entity PickInsolventWitness(EntityManager em)
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

        /// <summary>Fallback insolvable (comme eco_033) : terre + Balance&lt;0 + armée.</summary>
        static Entity PickPoorWitness(EntityManager em) => PickInsolventWitness(em);

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
    }
}
