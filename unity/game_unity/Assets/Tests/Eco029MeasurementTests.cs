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

namespace VictoriaGame.Tests
{
    /// <summary>Point d'entrée batchmode : -executeMethod VictoriaGame.Tests.Eco029BatchRunner.Run</summary>
    public static class Eco029BatchRunner
    {
        public static void Run()
        {
            Eco029MeasurementTests.RunMeasurementsAndWriteLog();
        }
    }

    [TestFixture]
    public class Eco029MeasurementTests
    {
        const uint Seed = 42195u;
        static readonly int[] SnapshotTicks = { 50, 200, 500, 1000 };
        static readonly string[] FocusTags = { "VEN", "BYZ" };
        static readonly float[] HaircutsTried = { 0f, 0.3f, 0.5f, 0.7f };

        [Test]
        public void Eco029_MeasureRepeatableBankruptcyAtKeyTicks() => RunMeasurementsAndWriteLog();

        public static void RunMeasurementsAndWriteLog()
        {
            var previousMode = TreasurySystem.Mode;
            var previousHaircut = TreasurySystem.Haircut;
            var logPath = Path.Combine(
                UnityEngine.Application.dataPath, "..", "Logs", "eco_029_measurements.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine(
                    $"=== eco_029 seed={Seed} BANKRUPTCY_THRESHOLD={TreasurySystem.BankruptcyThreshold} " +
                    $"BANKRUPTCY_HAIRCUT_default={TreasurySystem.BankruptcyHaircut:F2} " +
                    $"GateMode={ArmyDisbandmentSystem.GateMode} ===");
                sb.AppendLine(
                    "tried haircuts: 0.0 (spirale debt~6044), 0.3 (soft~2811), 0.5 (soft~1254), " +
                    "0.7 RETENU (borné~601)");
                sb.AppendLine(
                    "Colonnes: eco_028 AVANT = OneShotLegacy ; eco_029 = RepeatableHaircut calibré.");
                sb.AppendLine();

                AppendScenario(sb, "AVANT eco_028 (OneShotLegacy)", BankruptcyMode.OneShotLegacy, 0f);
                foreach (var h in HaircutsTried)
                {
                    AppendScenario(
                        sb,
                        $"eco_029 RepeatableHaircut haircut={h.ToString("F1", CultureInfo.InvariantCulture)}",
                        BankruptcyMode.RepeatableHaircut,
                        h);
                }

                AppendVerdict(sb);

                File.WriteAllText(logPath, sb.ToString());
                UnityEngine.Debug.Log(sb.ToString());
            }
            finally
            {
                TreasurySystem.Mode = previousMode;
                TreasurySystem.Haircut = previousHaircut;
            }
        }

        static void AppendScenario(StringBuilder sb, string title, BankruptcyMode mode, float haircut)
        {
            TreasurySystem.Mode = mode;
            TreasurySystem.Haircut = haircut;
            sb.AppendLine(
                $"=== {title} Mode={mode} Haircut={haircut.ToString("F2", CultureInfo.InvariantCulture)} ===");

            foreach (var tick in SnapshotTicks)
            {
                using var harness = new SimulationHarness(Seed);
                harness.RunTicks(tick);
                var em = harness.EntityManager;

                var armyStrengthByCountry = SumArmyStrengthByCountry(em);
                var regimentCounts = CountRegimentsByCountry(em);
                var snapshots = CaptureCountrySnapshots(em, armyStrengthByCountry, regimentCounts);
                var metrics = Aggregate(snapshots);
                var (needsSatAvg, population) = CapturePopMetrics(em);

                sb.AppendLine(
                    $"tick{tick}: totalDebt={metrics.TotalDebt:F1} bankrupt={metrics.BankruptCountries} " +
                    $"bankruptcyEvents={metrics.BankruptcyEvents} maxBankruptcyCount={metrics.MaxBankruptcyCount} " +
                    $"minBalance={metrics.MinBalance:F1} worldArmyStr={metrics.WorldArmyStr:F0} " +
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
                            $"armyStr={snap.ArmyStrength:F0} regiments={snap.RegimentCount} " +
                            $"bankruptTick={snap.BankruptcyTick} bankruptcyCount={snap.BankruptcyCount}");
                    }
                }

                armyStrengthByCountry.Dispose();
                regimentCounts.Dispose();
            }

            sb.AppendLine();
        }

        static void AppendVerdict(StringBuilder sb)
        {
            var avant = CaptureAt(1000, BankruptcyMode.OneShotLegacy, 0f);
            var h0 = CaptureAt(1000, BankruptcyMode.RepeatableHaircut, 0f);
            var h03 = CaptureAt(1000, BankruptcyMode.RepeatableHaircut, 0.3f);
            var h05 = CaptureAt(1000, BankruptcyMode.RepeatableHaircut, 0.5f);
            var h07 = CaptureAt(1000, BankruptcyMode.RepeatableHaircut, 0.7f);

            // 0.5 ne borne pas (debt~1254) ; 0.7 borne (debt~601). Production = 0.7.
            var retainedHaircut = TreasurySystem.BankruptcyHaircut;
            var retained = retainedHaircut >= 0.65f ? h07 : h05;

            sb.AppendLine("=== VERDICT eco_029 (t1000, seed 42195) ===");
            sb.AppendLine(
                $"AVANT(eco_028 one-shot): debt={avant.TotalDebt:F1} bankrupt={avant.BankruptCountries} " +
                $"events={avant.BankruptcyEvents} maxCount={avant.MaxBankruptcyCount} " +
                $"minBal={avant.MinBalance:F1} army={avant.WorldArmyStr:F0} " +
                $"sat={avant.NeedsSatAvg:F3} pop={avant.Population}");
            sb.AppendLine(
                $"haircut=0.0: debt={h0.TotalDebt:F1} events={h0.BankruptcyEvents} " +
                $"maxCount={h0.MaxBankruptcyCount} minBal={h0.MinBalance:F1} army={h0.WorldArmyStr:F0}");
            sb.AppendLine(
                $"haircut=0.3: debt={h03.TotalDebt:F1} events={h03.BankruptcyEvents} " +
                $"maxCount={h03.MaxBankruptcyCount} minBal={h03.MinBalance:F1} army={h03.WorldArmyStr:F0}");
            sb.AppendLine(
                $"haircut=0.5: debt={h05.TotalDebt:F1} events={h05.BankruptcyEvents} " +
                $"maxCount={h05.MaxBankruptcyCount} minBal={h05.MinBalance:F1} army={h05.WorldArmyStr:F0}");
            sb.AppendLine(
                $"haircut=0.7: debt={h07.TotalDebt:F1} events={h07.BankruptcyEvents} " +
                $"maxCount={h07.MaxBankruptcyCount} minBal={h07.MinBalance:F1} army={h07.WorldArmyStr:F0}");

            sb.AppendLine(
                $"VEN retenu: Bal={retained.Ven.Balance:F1} Debt={retained.Ven.Debt:F1} " +
                $"army={retained.Ven.ArmyStrength:F0} regs={retained.Ven.RegimentCount} " +
                $"bkCount={retained.Ven.BankruptcyCount}");
            sb.AppendLine(
                $"BYZ retenu: Bal={retained.Byz.Balance:F1} Debt={retained.Byz.Debt:F1} " +
                $"army={retained.Byz.ArmyStrength:F0} regs={retained.Byz.RegimentCount} " +
                $"bkCount={retained.Byz.BankruptcyCount}");

            if (retained.MinBalance >= -520f)
                sb.AppendLine($"OK PLANCHER: minBalance={retained.MinBalance:F1} (≥ ~-500, déficit d'1 tick OK).");
            else
                sb.AppendLine($"ALERT PLANCHER: minBalance={retained.MinBalance:F1} plonge sous le seuil.");

            if (retained.TotalDebt <= 1100f)
                sb.AppendLine($"OK DETTE BORNÉE: totalDebt={retained.TotalDebt:F1} ≤ ~1000 (+marge).");
            else
                sb.AppendLine($"ALERT SPIRALE: totalDebt={retained.TotalDebt:F1} > 1100.");

            if (retained.MaxBankruptcyCount > 1)
                sb.AppendLine(
                    $"OK RÉPÉTABILITÉ: maxBankruptcyCount={retained.MaxBankruptcyCount} (>1).");
            else
                sb.AppendLine(
                    $"ALERT RÉPÉTABILITÉ: maxBankruptcyCount={retained.MaxBankruptcyCount} (one-shot encore?).");

            if (retained.Ven.RegimentCount >= 2 && retained.Ven.RegimentCount <= 5 &&
                retained.Byz.ArmyStrength >= 100f)
            {
                sb.AppendLine(
                    $"OK acquis eco_026/027: VEN~{retained.Ven.RegimentCount} rég, " +
                    $"BYZ armyStr={retained.Byz.ArmyStrength:F0}.");
            }
            else
            {
                sb.AppendLine(
                    $"ALERT acquis: VEN regs={retained.Ven.RegimentCount} " +
                    $"BYZ army={retained.Byz.ArmyStrength:F0}.");
            }

            if (System.Math.Abs(retained.NeedsSatAvg - 0.70) < 0.15 &&
                System.Math.Abs(retained.Population - 140000) < 40000)
            {
                sb.AppendLine(
                    $"OK needs/pop: sat={retained.NeedsSatAvg:F3} pop={retained.Population} " +
                    "(ordre eco_028 w=0.5).");
            }
            else
            {
                sb.AppendLine(
                    $"ALERT needs/pop: sat={retained.NeedsSatAvg:F3} pop={retained.Population}.");
            }

            sb.AppendLine(
                $"BANKRUPTCY_HAIRCUT retenu={retainedHaircut.ToString("F2", CultureInfo.InvariantCulture)} " +
                $"(0.0 spirale debt={h0.TotalDebt:F0}; 0.5 soft-spirale debt={h05.TotalDebt:F0}; " +
                $"0.7 borné debt={h07.TotalDebt:F0}).");
        }

        struct CountrySnapshot
        {
            public FixedString32Bytes Tag;
            public float Balance;
            public float Debt;
            public float Income;
            public float Expenses;
            public float ArmyStrength;
            public int RegimentCount;
            public int BankruptcyTick;
            public int BankruptcyCount;
        }

        struct AggregateMetrics
        {
            public float TotalDebt;
            public int BankruptCountries;
            public int BankruptcyEvents;
            public int MaxBankruptcyCount;
            public float MinBalance;
            public float WorldArmyStr;
        }

        struct WorldSnap
        {
            public float TotalDebt;
            public int BankruptCountries;
            public int BankruptcyEvents;
            public int MaxBankruptcyCount;
            public float MinBalance;
            public float WorldArmyStr;
            public float NeedsSatAvg;
            public int Population;
            public CountrySnapshot Ven;
            public CountrySnapshot Byz;
        }

        static WorldSnap CaptureAt(int ticks, BankruptcyMode mode, float haircut)
        {
            TreasurySystem.Mode = mode;
            TreasurySystem.Haircut = haircut;
            using var harness = new SimulationHarness(Seed);
            harness.RunTicks(ticks);
            var em = harness.EntityManager;
            var army = SumArmyStrengthByCountry(em);
            var regs = CountRegimentsByCountry(em);
            var snaps = CaptureCountrySnapshots(em, army, regs);
            var agg = Aggregate(snaps);
            var (sat, pop) = CapturePopMetrics(em);
            var ven = FindTag(snaps, "VEN");
            var byz = FindTag(snaps, "BYZ");
            army.Dispose();
            regs.Dispose();
            return new WorldSnap
            {
                TotalDebt = agg.TotalDebt,
                BankruptCountries = agg.BankruptCountries,
                BankruptcyEvents = agg.BankruptcyEvents,
                MaxBankruptcyCount = agg.MaxBankruptcyCount,
                MinBalance = agg.MinBalance,
                WorldArmyStr = agg.WorldArmyStr,
                NeedsSatAvg = sat,
                Population = pop,
                Ven = ven,
                Byz = byz
            };
        }

        static CountrySnapshot FindTag(List<CountrySnapshot> snaps, string tag)
        {
            foreach (var s in snaps)
            {
                if (s.Tag.ToString() == tag)
                    return s;
            }

            return default;
        }

        static AggregateMetrics Aggregate(List<CountrySnapshot> snapshots)
        {
            float totalDebt = 0f;
            var bankruptCountries = 0;
            var events = 0;
            var maxCount = 0;
            var minBalance = float.MaxValue;
            float worldArmy = 0f;

            foreach (var snap in snapshots)
            {
                totalDebt += snap.Debt;
                if (snap.BankruptcyTick > 0)
                    bankruptCountries++;
                events += snap.BankruptcyCount;
                if (snap.BankruptcyCount > maxCount)
                    maxCount = snap.BankruptcyCount;
                if (snap.Balance < minBalance)
                    minBalance = snap.Balance;
                worldArmy += snap.ArmyStrength;
            }

            if (snapshots.Count == 0)
                minBalance = 0f;

            return new AggregateMetrics
            {
                TotalDebt = totalDebt,
                BankruptCountries = bankruptCountries,
                BankruptcyEvents = events,
                MaxBankruptcyCount = maxCount,
                MinBalance = minBalance,
                WorldArmyStr = worldArmy
            };
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

        static NativeHashMap<Entity, int> CountRegimentsByCountry(EntityManager em)
        {
            var map = new NativeHashMap<Entity, int>(32, Allocator.Temp);
            using var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<ArmyData>(),
                ComponentType.ReadOnly<RegimentSlot>());
            using var entities = query.ToEntityArray(Allocator.Temp);
            using var armies = query.ToComponentDataArray<ArmyData>(Allocator.Temp);

            for (var i = 0; i < entities.Length; i++)
            {
                var country = armies[i].Country;
                if (country == Entity.Null)
                    continue;

                var slots = em.GetBuffer<RegimentSlot>(entities[i]);
                map.TryGetValue(country, out var current);
                map[country] = current + slots.Length;
            }

            return map;
        }

        static List<CountrySnapshot> CaptureCountrySnapshots(
            EntityManager em,
            NativeHashMap<Entity, float> armyStrengthByCountry,
            NativeHashMap<Entity, int> regimentCounts)
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
                var regs = regimentCounts.TryGetValue(entity, out var r) ? r : 0;

                results.Add(new CountrySnapshot
                {
                    Tag = countryData[i].Tag,
                    Balance = treasury.Balance,
                    Debt = treasury.Debt,
                    Income = treasury.Income,
                    Expenses = treasury.Expenses,
                    ArmyStrength = armyStr,
                    RegimentCount = regs,
                    BankruptcyTick = treasury.BankruptcyTick,
                    BankruptcyCount = treasury.BankruptcyCount
                });
            }

            results.Sort((a, b) => a.Tag.CompareTo(b.Tag));
            return results;
        }

        static (float needsSatAvg, int population) CapturePopMetrics(EntityManager em)
        {
            double weightedSat = 0.0;
            var totalPop = 0;

            using var popQuery = em.CreateEntityQuery(typeof(PopData));
            using var pops = popQuery.ToComponentDataArray<PopData>(Allocator.Temp);
            foreach (var pop in pops)
            {
                totalPop += pop.Size;
                weightedSat += pop.NeedsSatisfaction * pop.Size;
            }

            var satAvg = totalPop > 0 ? (float)(weightedSat / totalPop) : 0f;
            return (satAvg, totalPop);
        }
    }
}
