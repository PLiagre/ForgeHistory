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
    /// <summary>Point d'entrée batchmode : -executeMethod VictoriaGame.Tests.Dip005BatchRunner.Run</summary>
    public static class Dip005BatchRunner
    {
        public static void Run()
        {
            Dip005MeasurementTests.RunMeasurementsAndWriteLog();
            UnityEngine.Debug.Log("Dip005BatchRunner: DONE");
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
            #endif
        }
    }

    [TestFixture]
    public class Dip005MeasurementTests
    {
        const uint Seed = 42195u;
        static readonly int[] SnapshotTicks = { 200, 500, 800, 1000 };
        /// <summary>0 = baseline (dip_004) ; 0.5 = départ brief ; 0.35 / 0.65 = essais de calibration.</summary>
        static readonly float[] RatesTried = { 0f, 0.35f, 0.5f, 0.65f };

        [Test]
        public void Dip005_MeasureOccupationScoreAtKeyTicks() => RunMeasurementsAndWriteLog();

        public static void RunMeasurementsAndWriteLog()
        {
            var previousRate = OccupationScoreSystem.OccupationScoreRate;
            var logPath = Path.Combine(
                UnityEngine.Application.dataPath, "..", "Logs", "dip_005_measurements.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine(
                    $"=== dip_005 seed={Seed} DefaultOccupationScoreRate=" +
                    $"{OccupationScoreSystem.DefaultOccupationScoreRate.ToString("F2", CultureInfo.InvariantCulture)} " +
                    $"PEACE_THRESHOLD=60 WAR_EXHAUSTION_TICKS=150 ===");
                sb.AppendLine(
                    "tried: OCCUPATION_SCORE_RATE=0 (baseline dip_004), 0.35, 0.5 (cible brief), 0.65");
                sb.AppendLine(
                    "Pondération plate (1 province = 1). Convention WarScore: + = attaquant.");
                sb.AppendLine();

                foreach (var rate in RatesTried)
                {
                    AppendScenario(sb, rate);
                }

                AppendVerdict(sb);
                File.WriteAllText(logPath, sb.ToString());
                UnityEngine.Debug.Log(sb.ToString());
            }
            finally
            {
                OccupationScoreSystem.OccupationScoreRate = previousRate;
            }
        }

        static void AppendScenario(StringBuilder sb, float rate)
        {
            OccupationScoreSystem.OccupationScoreRate = rate;
            var label = rate == 0f
                ? "A — OCCUPATION_SCORE_RATE=0 (baseline)"
                : $"rate={rate.ToString("F2", CultureInfo.InvariantCulture)}";
            sb.AppendLine(
                $"=== {label} OCCUPATION_SCORE_RATE={rate.ToString("F2", CultureInfo.InvariantCulture)} ===");

            foreach (var tick in SnapshotTicks)
            {
                using var harness = new SimulationHarness(Seed);
                harness.RunTicks(tick);
                var em = harness.EntityManager;
                var snap = CaptureWarSnapshot(em, tick);
                var eco = CaptureEcoMetrics(em);

                var concluded = snap.Victories + snap.WhitePeaces;
                var ratio = concluded > 0
                    ? (float)snap.Victories / concluded
                    : 0f;

                sb.AppendLine(
                    $"tick{tick}: declared={snap.Declared} active={snap.Active} " +
                    $"victories={snap.Victories} whitePeaces={snap.WhitePeaces} " +
                    $"ratioV={(ratio * 100f).ToString("F1", CultureInfo.InvariantCulture)}% " +
                    $"annexed={snap.Annexed} avgDuration=" +
                    $"{snap.AvgConcludedDuration.ToString("F1", CultureInfo.InvariantCulture)} " +
                    $"countriesWithLand={snap.CountriesWithLand} stuck={snap.Stuck} " +
                    $"totalDebt={eco.TotalDebt.ToString("F1", CultureInfo.InvariantCulture)} " +
                    $"bankrupt={eco.Bankrupt} needsSatAvg=" +
                    $"{eco.NeedsSatAvg.ToString("F3", CultureInfo.InvariantCulture)} " +
                    $"population={eco.Population} worldArmyStr=" +
                    $"{eco.WorldArmyStr.ToString("F0", CultureInfo.InvariantCulture)}");
            }

            sb.AppendLine();
        }

        static void AppendVerdict(StringBuilder sb)
        {
            sb.AppendLine("=== VERDICT (à t800, critère principal) ===");
            OccupationScoreSystem.OccupationScoreRate = 0f;
            var baseline = CaptureAt(800);
            OccupationScoreSystem.OccupationScoreRate = OccupationScoreSystem.DefaultOccupationScoreRate;
            var retained = CaptureAt(800);

            var baseConcluded = baseline.Victories + baseline.WhitePeaces;
            var retConcluded = retained.Victories + retained.WhitePeaces;
            var baseRatio = baseConcluded > 0 ? (float)baseline.Victories / baseConcluded : 0f;
            var retRatio = retConcluded > 0 ? (float)retained.Victories / retConcluded : 0f;

            sb.AppendLine(
                $"baseline rate=0: V={baseline.Victories} WP={baseline.WhitePeaces} " +
                $"ratio={baseRatio.ToString("P1", CultureInfo.InvariantCulture)} " +
                $"annexed={baseline.Annexed} countries={baseline.CountriesWithLand}");
            sb.AppendLine(
                $"retenu rate={OccupationScoreSystem.DefaultOccupationScoreRate.ToString("F2", CultureInfo.InvariantCulture)}: " +
                $"V={retained.Victories} WP={retained.WhitePeaces} " +
                $"ratio={retRatio.ToString("P1", CultureInfo.InvariantCulture)} " +
                $"annexed={retained.Annexed} countries={retained.CountriesWithLand} stuck={retained.Stuck}");

            if (retRatio >= 0.45f && retained.CountriesWithLand >= 8 && retained.Stuck == 0)
                sb.AppendLine("OK: ratio victoires proche parité ou mieux, monde non réduit à 2-3 empires, zéro enlisement.");
            else if (retRatio > baseRatio && retained.Stuck == 0)
                sb.AppendLine("PARTIEL: ratio amélioré vs baseline, vérifier calibration.");
            else
                sb.AppendLine("ALERT: calibration insuffisante ou régression.");
        }

        static WarSnapshot CaptureAt(int tick)
        {
            using var harness = new SimulationHarness(Seed);
            harness.RunTicks(tick);
            return CaptureWarSnapshot(harness.EntityManager, tick);
        }

        struct WarSnapshot
        {
            public int Declared;
            public int Active;
            public int Victories;
            public int WhitePeaces;
            public int Annexed;
            public float AvgConcludedDuration;
            public int CountriesWithLand;
            public int Stuck;
        }

        struct EcoMetrics
        {
            public float TotalDebt;
            public int Bankrupt;
            public float NeedsSatAvg;
            public int Population;
            public float WorldArmyStr;
        }

        static WarSnapshot CaptureWarSnapshot(EntityManager em, int currentTick)
        {
            var snap = new WarSnapshot();
            var durationSum = 0;
            var concludedCount = 0;

            using var warQuery = em.CreateEntityQuery(ComponentType.ReadOnly<WarData>());
            using var wars = warQuery.ToComponentDataArray<WarData>(Allocator.Temp);

            for (var i = 0; i < wars.Length; i++)
            {
                var war = wars[i];
                snap.Declared++;

                if (war.IsActive)
                {
                    snap.Active++;
                    if (currentTick - war.StartTick > 150)
                        snap.Stuck++;
                    continue;
                }

                if (war.EndTick <= 0)
                    continue;

                var duration = war.EndTick - war.StartTick;
                durationSum += duration;
                concludedCount++;

                if (System.Math.Abs(war.WarScore) >= 60f)
                    snap.Victories++;
                else
                    snap.WhitePeaces++;
            }

            snap.AvgConcludedDuration = concludedCount > 0
                ? (float)durationSum / concludedCount
                : 0f;

            var owners = new HashSet<Entity>();
            using var ownQuery = em.CreateEntityQuery(ComponentType.ReadOnly<ProvinceOwnership>());
            using var ownerships = ownQuery.ToComponentDataArray<ProvinceOwnership>(Allocator.Temp);
            for (var i = 0; i < ownerships.Length; i++)
            {
                var o = ownerships[i];
                if (o.Owner != Entity.Null)
                    owners.Add(o.Owner);
                if (o.Owner != Entity.Null && o.Core != Entity.Null && o.Owner != o.Core)
                    snap.Annexed++;
            }

            snap.CountriesWithLand = owners.Count;
            return snap;
        }

        static EcoMetrics CaptureEcoMetrics(EntityManager em)
        {
            var eco = new EcoMetrics();

            using var treasuryQuery = em.CreateEntityQuery(ComponentType.ReadOnly<TreasuryData>());
            using var treasuries = treasuryQuery.ToComponentDataArray<TreasuryData>(Allocator.Temp);
            for (var i = 0; i < treasuries.Length; i++)
            {
                eco.TotalDebt += treasuries[i].Debt;
                if (treasuries[i].BankruptcyTick > 0)
                    eco.Bankrupt++;
            }

            using var armyQuery = em.CreateEntityQuery(ComponentType.ReadOnly<ArmyData>());
            using var armies = armyQuery.ToComponentDataArray<ArmyData>(Allocator.Temp);
            for (var i = 0; i < armies.Length; i++)
                eco.WorldArmyStr += armies[i].Strength;

            var totalPop = 0;
            var satSum = 0f;
            var popCount = 0;
            using var popQuery = em.CreateEntityQuery(ComponentType.ReadOnly<PopData>());
            using var pops = popQuery.ToComponentDataArray<PopData>(Allocator.Temp);
            for (var i = 0; i < pops.Length; i++)
            {
                totalPop += pops[i].Size;
                satSum += pops[i].NeedsSatisfaction;
                popCount++;
            }

            eco.Population = totalPop;
            eco.NeedsSatAvg = popCount > 0 ? satSum / popCount : 0f;
            return eco;
        }
    }
}
