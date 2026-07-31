using System.Collections.Generic;
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
    /// <summary>Point d'entrée batchmode : -executeMethod VictoriaGame.Tests.Eco026BatchRunner.Run</summary>
    public static class Eco026BatchRunner
    {
        public static void Run()
        {
            Eco026MeasurementTests.RunMeasurementsAndWriteLog();
        }
    }

    [TestFixture]
    public class Eco026MeasurementTests
    {
        const uint Seed = 42195u;
        static readonly int[] SnapshotTicks = { 50, 200, 500, 1000 };
        static readonly string[] FocusTags = { "MIL", "FRA", "VEN", "BYZ" };

        [Test]
        public void Eco026_MeasureArmyDisbandmentAtKeyTicks() => RunMeasurementsAndWriteLog();

        public static void RunMeasurementsAndWriteLog()
        {
            var logPath = Path.Combine(
                UnityEngine.Application.dataPath, "..", "Logs", "eco_026_measurements.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

            var sb = new StringBuilder();
            sb.AppendLine("=== AVANT eco_026 (eco_025, pas de désarmement forcé) seed=42195 ===");
            sb.AppendLine(
                "t50: totalDebt=0 bankrupt=0 | VEN Balance=8.4 Strength=? | BYZ Balance=0.4 | " +
                "MIL Balance=458.3 | needsSatAvg~0.78 pop~129.5k");
            sb.AppendLine(
                "t200: totalDebt=0 bankrupt=0 | VEN Balance=-162.0 | BYZ Balance=-122.8 | MIL Balance=33.0");
            sb.AppendLine(
                "t500: totalDebt=1503 bankrupt=3 | VEN Balance=-448.2 | BYZ Balance=-367.9 | MIL Balance=27.6");
            sb.AppendLine(
                "t1000: totalDebt=3504.7 bankrupt=7 | VEN Balance=43.5 | BYZ Balance=-804.1 Debt=500.6 | " +
                "MIL Balance=31.3 Expenses=7.4");
            sb.AppendLine(
                "tried: threshold=0 interval=6 (BYZ armyStr→0 trop agressif) ; " +
                "retenu threshold=0 interval=8 (BYZ se restabilise à ~580, debt t1000 3504→1001, bankrupt 7→2)");

            sb.AppendLine();
            sb.AppendLine(
                $"=== APRÈS eco_026 seed={Seed} " +
                $"INSOLVENCY_THRESHOLD={ArmyDisbandmentSystem.InsolvencyThreshold} " +
                $"DISBAND_INTERVAL={ArmyDisbandmentSystem.DisbandInterval} ===");

            foreach (var tick in SnapshotTicks)
            {
                using var harness = new SimulationHarness(Seed);
                harness.RunTicks(tick);
                var em = harness.EntityManager;

                var armyStrengthByCountry = SumArmyStrengthByCountry(em);
                var snapshots = CaptureCountrySnapshots(em, armyStrengthByCountry);

                var totalDebt = 0f;
                var bankruptcyCount = 0;
                var insolventCount = 0;
                var worldStrength = 0f;
                foreach (var snap in snapshots)
                {
                    totalDebt += snap.Debt;
                    if (snap.BankruptcyTick > 0)
                        bankruptcyCount++;
                    if (ArmyDisbandmentSystem.IsInsolvent(snap.Balance))
                        insolventCount++;
                    worldStrength += snap.ArmyStrength;
                }

                var (needsSatAvg, population) = CapturePopMetrics(em);
                var activeWars = CountActiveWars(em);

                sb.AppendLine(
                    $"tick{tick}: countries={snapshots.Count} totalDebt={totalDebt:F1} " +
                    $"bankrupt={bankruptcyCount} insolvent={insolventCount} " +
                    $"worldArmyStr={worldStrength:F0} activeWars={activeWars} " +
                    $"needsSatAvg={needsSatAvg:F3} population={population}");

                foreach (var tag in FocusTags)
                {
                    foreach (var snap in snapshots)
                    {
                        if (snap.Tag.ToString() != tag)
                            continue;

                        sb.AppendLine(
                            $"  {tag}: Balance={snap.Balance:F1} Debt={snap.Debt:F1} " +
                            $"Income={snap.Income:F1} Expenses={snap.Expenses:F1} " +
                            $"armyStr={snap.ArmyStrength:F0} insolvent={ArmyDisbandmentSystem.IsInsolvent(snap.Balance)} " +
                            $"bankruptTick={snap.BankruptcyTick}");
                    }
                }

                armyStrengthByCountry.Dispose();
            }

            File.WriteAllText(logPath, sb.ToString());
            UnityEngine.Debug.Log(sb.ToString());
        }

        struct CountrySnapshot
        {
            public FixedString32Bytes Tag;
            public float Balance;
            public float Debt;
            public float Income;
            public float Expenses;
            public float ArmyStrength;
            public int BankruptcyTick;
        }

        static NativeHashMap<Entity, float> SumArmyStrengthByCountry(EntityManager em)
        {
            var map = new NativeHashMap<Entity, float>(32, Allocator.Temp);
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<ArmyData>());
            using var armies = query.ToComponentDataArray<ArmyData>(Allocator.Temp);

            for (var i = 0; i < armies.Length; i++)
            {
                var country = armies[i].Country;
                if (country == Entity.Null)
                    continue;

                map.TryGetValue(country, out var current);
                map[country] = current + armies[i].Strength;
            }

            return map;
        }

        static List<CountrySnapshot> CaptureCountrySnapshots(
            EntityManager em,
            NativeHashMap<Entity, float> armyStrengthByCountry)
        {
            var results = new List<CountrySnapshot>();
            using var countryQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<CountryData>(),
                ComponentType.ReadOnly<TreasuryData>());
            using var countries = countryQuery.ToEntityArray(Allocator.Temp);
            using var countryData = countryQuery.ToComponentDataArray<CountryData>(Allocator.Temp);
            using var treasuries = countryQuery.ToComponentDataArray<TreasuryData>(Allocator.Temp);

            for (var i = 0; i < countries.Length; i++)
            {
                var entity = countries[i];
                var treasury = treasuries[i];
                var armyStr = armyStrengthByCountry.TryGetValue(entity, out var a) ? a : 0f;

                results.Add(new CountrySnapshot
                {
                    Tag = countryData[i].Tag,
                    Balance = treasury.Balance,
                    Debt = treasury.Debt,
                    Income = treasury.Income,
                    Expenses = treasury.Expenses,
                    ArmyStrength = armyStr,
                    BankruptcyTick = treasury.BankruptcyTick
                });
            }

            results.Sort((a, b) => a.Tag.CompareTo(b.Tag));
            return results;
        }

        static (float needsSatAvg, int population) CapturePopMetrics(EntityManager em)
        {
            var totalPop = 0;
            var satSum = 0f;
            var popCount = 0;

            using var popQuery = em.CreateEntityQuery(typeof(PopData));
            using var pops = popQuery.ToComponentDataArray<PopData>(Allocator.Temp);
            foreach (var pop in pops)
            {
                totalPop += pop.Size;
                satSum += pop.NeedsSatisfaction;
                popCount++;
            }

            var satAvg = popCount > 0 ? satSum / popCount : 0f;
            return (satAvg, totalPop);
        }

        static int CountActiveWars(EntityManager em)
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<WarData>());
            using var wars = query.ToComponentDataArray<WarData>(Allocator.Temp);
            var count = 0;
            for (var i = 0; i < wars.Length; i++)
            {
                if (wars[i].IsActive && wars[i].EndTick == 0)
                    count++;
            }

            return count;
        }
    }
}
