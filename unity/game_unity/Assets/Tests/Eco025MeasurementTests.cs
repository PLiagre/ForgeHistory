using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Collections;
using Unity.Entities;
using NUnit.Framework;
using VictoriaGame.Core;
using VictoriaGame.Economy;
using VictoriaGame.World;

namespace VictoriaGame.Tests
{
    /// <summary>Point d'entrée batchmode : -executeMethod VictoriaGame.Tests.Eco025BatchRunner.Run</summary>
    public static class Eco025BatchRunner
    {
        public static void Run()
        {
            Eco025MeasurementTests.RunMeasurementsAndWriteLog();
        }
    }

    [TestFixture]
    public class Eco025MeasurementTests
    {
        const uint Seed = 42195u;
        static readonly int[] SnapshotTicks = { 50, 200, 500, 1000 };

        [Test]
        public void Eco025_MeasureTreasuryManagementAtKeyTicks() => RunMeasurementsAndWriteLog();

        public static void RunMeasurementsAndWriteLog()
        {
            var logPath = Path.Combine(
                UnityEngine.Application.dataPath, "..", "Logs", "eco_025_measurements.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

            var sb = new StringBuilder();
            sb.AppendLine("=== AVANT eco_025 (eco_024, pas de gestion surplus/dette) ===");
            sb.AppendLine(
                "t50: MIL(riche) Balance=449.2 Debt=0 | FRA Balance=26.4 | aboveCap=1 totalDebt=0");
            sb.AppendLine(
                "t200: MIL Balance=1323.6 Debt=0 | FRA Balance=8.7 | aboveCap=1 totalDebt=0");
            sb.AppendLine(
                "t500: MIL Balance=3213.4 Debt=0 | FRA Balance=50.0 | POL(endetté) Balance=-488.7 bankrupt=1 | " +
                "aboveCap=1 totalDebt=0");
            sb.AppendLine(
                "t1000: (eco_024 non mesuré — soldes riches continuent de ballonner)");

            sb.AppendLine();
            sb.AppendLine(
                $"=== APRÈS eco_025 seed={Seed} " +
                $"DEBT_REPAY_BUFFER={TreasuryManagementSystem.DebtRepayBuffer} " +
                $"DEBT_REPAY_FRACTION={TreasuryManagementSystem.DebtRepayFraction} " +
                $"RESERVE_CAP={TreasuryManagementSystem.ReserveCap} " +
                $"SKIM_RATE={TreasuryManagementSystem.SkimRate} ===");

            foreach (var tick in SnapshotTicks)
            {
                using var harness = new SimulationHarness(Seed);
                harness.RunTicks(tick);
                var em = harness.EntityManager;

                var snapshots = CaptureCountrySnapshots(em);

                var aboveCapCount = 0;
                var totalDebt = 0f;
                var bankruptcyCount = 0;
                foreach (var snap in snapshots)
                {
                    if (snap.Balance > TreasuryManagementSystem.ReserveCap)
                        aboveCapCount++;
                    totalDebt += snap.Debt;
                    if (snap.BankruptcyTick > 0)
                        bankruptcyCount++;
                }

                sb.AppendLine(
                    $"tick{tick}: countries={snapshots.Count} aboveCap={aboveCapCount} " +
                    $"totalDebt={totalDebt:F1} bankrupt={bankruptcyCount}");

                foreach (var tag in new[] { "MIL", "FRA", "POL", "VEN", "BYZ" })
                {
                    foreach (var snap in snapshots)
                    {
                        if (snap.Tag.ToString() != tag)
                            continue;

                        sb.AppendLine(
                            $"  {tag}: Balance={snap.Balance:F1} Debt={snap.Debt:F1} " +
                            $"Income={snap.Income:F1} Expenses={snap.Expenses:F1} " +
                            $"Stability={snap.Stability:F3} bankruptTick={snap.BankruptcyTick}");
                    }
                }
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
            public float Stability;
            public int BankruptcyTick;
        }

        static List<CountrySnapshot> CaptureCountrySnapshots(EntityManager em)
        {
            var results = new List<CountrySnapshot>();
            using var countryQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<CountryData>(),
                ComponentType.ReadOnly<TreasuryData>(),
                ComponentType.ReadOnly<VictoriaGame.Politics.GovernmentData>());
            using var countries = countryQuery.ToEntityArray(Allocator.Temp);
            using var countryData = countryQuery.ToComponentDataArray<CountryData>(Allocator.Temp);
            using var treasuries = countryQuery.ToComponentDataArray<TreasuryData>(Allocator.Temp);
            using var governments = countryQuery.ToComponentDataArray<VictoriaGame.Politics.GovernmentData>(Allocator.Temp);

            for (var i = 0; i < countries.Length; i++)
            {
                var treasury = treasuries[i];
                results.Add(new CountrySnapshot
                {
                    Tag = countryData[i].Tag,
                    Balance = treasury.Balance,
                    Debt = treasury.Debt,
                    Income = treasury.Income,
                    Expenses = treasury.Expenses,
                    Stability = governments[i].Stability,
                    BankruptcyTick = treasury.BankruptcyTick
                });
            }

            results.Sort((a, b) => a.Tag.CompareTo(b.Tag));
            return results;
        }
    }
}
