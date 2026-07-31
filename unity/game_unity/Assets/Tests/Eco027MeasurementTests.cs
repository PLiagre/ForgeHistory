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
    /// <summary>Point d'entrée batchmode : -executeMethod VictoriaGame.Tests.Eco027BatchRunner.Run</summary>
    public static class Eco027BatchRunner
    {
        public static void Run()
        {
            Eco027MeasurementTests.RunMeasurementsAndWriteLog();
        }
    }

    [TestFixture]
    public class Eco027MeasurementTests
    {
        const uint Seed = 42195u;
        static readonly int[] SnapshotTicks = { 50, 200, 500, 1000 };
        static readonly string[] FocusTags = { "MIL", "FRA", "VEN", "BYZ" };

        [Test]
        public void Eco027_MeasureFluxGateBaselineAtKeyTicks() => RunMeasurementsAndWriteLog();

        public static void RunMeasurementsAndWriteLog()
        {
            var previousMode = ArmyDisbandmentSystem.GateMode;
            var logPath = Path.Combine(
                UnityEngine.Application.dataPath, "..", "Logs", "eco_027_measurements.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine(
                    $"=== eco_027 seed={Seed} BROKE_THRESHOLD={ArmyDisbandmentSystem.BrokeThreshold} " +
                    $"RECRUIT_MARGIN={ArmyDisbandmentSystem.RecruitMargin} " +
                    $"DISBAND_INTERVAL={ArmyDisbandmentSystem.DisbandInterval} " +
                    $"ARMY_UPKEEP={MilitaryUpkeepSystem.ArmyUpkeepRate} " +
                    $"matureUpkeep={ArmyDisbandmentSystem.MatureRegimentUpkeep:F5} ===");
                sb.AppendLine(
                    "tried: RecruitMargin=0.05 (VEN vise ~3 régiments : surplus~0.5, coût/rég=0.12)");
                sb.AppendLine();

                AppendScenario(sb, "AVANT eco_025 (gates Disabled)", ArmySolvencyGateMode.Disabled);
                AppendScenario(sb, "eco_026 (StockBalance)", ArmySolvencyGateMode.StockBalance);
                AppendScenario(sb, "eco_027 (FluxCommitted)", ArmySolvencyGateMode.FluxCommitted);

                AppendVerdict(sb);

                File.WriteAllText(logPath, sb.ToString());
                UnityEngine.Debug.Log(sb.ToString());
            }
            finally
            {
                ArmyDisbandmentSystem.GateMode = previousMode;
            }
        }

        static void AppendScenario(StringBuilder sb, string title, ArmySolvencyGateMode mode)
        {
            ArmyDisbandmentSystem.GateMode = mode;
            sb.AppendLine($"=== {title} GateMode={mode} ===");

            foreach (var tick in SnapshotTicks)
            {
                using var harness = new SimulationHarness(Seed);
                harness.RunTicks(tick);
                var em = harness.EntityManager;

                var armyStrengthByCountry = SumArmyStrengthByCountry(em);
                var regimentCounts = CountRegimentsByCountry(em);
                var snapshots = CaptureCountrySnapshots(em, armyStrengthByCountry, regimentCounts);

                var totalDebt = 0f;
                var bankruptcyCount = 0;
                var insolventCount = 0;
                var worldStrength = 0f;
                foreach (var snap in snapshots)
                {
                    totalDebt += snap.Debt;
                    if (snap.BankruptcyTick > 0)
                        bankruptcyCount++;
                    if (ArmyDisbandmentSystem.ShouldDisband(new TreasuryData
                        {
                            Balance = snap.Balance,
                            Income = snap.Income,
                            Expenses = snap.Expenses
                        }, mode))
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
                            $"armyStr={snap.ArmyStrength:F0} regiments={snap.RegimentCount} " +
                            $"broke={ArmyDisbandmentSystem.IsBroke(snap.Balance)} " +
                            $"deficit={ArmyDisbandmentSystem.IsInDeficit(snap.Income, snap.Expenses)} " +
                            $"bankruptTick={snap.BankruptcyTick}");
                    }
                }

                armyStrengthByCountry.Dispose();
                regimentCounts.Dispose();
            }

            sb.AppendLine();
        }

        static void AppendVerdict(StringBuilder sb)
        {
            // Re-run t1000 only for compact verdict numbers
            float Str(ArmySolvencyGateMode mode)
            {
                ArmyDisbandmentSystem.GateMode = mode;
                using var harness = new SimulationHarness(Seed);
                harness.RunTicks(1000);
                var map = SumArmyStrengthByCountry(harness.EntityManager);
                float sum = 0f;
                using var armies = harness.EntityManager
                    .CreateEntityQuery(ComponentType.ReadOnly<ArmyData>())
                    .ToComponentDataArray<ArmyData>(Allocator.Temp);
                for (var i = 0; i < armies.Length; i++)
                    sum += armies[i].Strength;
                map.Dispose();
                return sum;
            }

            float FocusStr(ArmySolvencyGateMode mode, string tag)
            {
                ArmyDisbandmentSystem.GateMode = mode;
                using var harness = new SimulationHarness(Seed);
                harness.RunTicks(1000);
                var map = SumArmyStrengthByCountry(harness.EntityManager);
                using var q = harness.EntityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<CountryData>(),
                    ComponentType.ReadOnly<TreasuryData>());
                using var entities = q.ToEntityArray(Allocator.Temp);
                using var countries = q.ToComponentDataArray<CountryData>(Allocator.Temp);
                for (var i = 0; i < countries.Length; i++)
                {
                    if (countries[i].Tag.ToString() != tag)
                        continue;
                    var v = map.TryGetValue(entities[i], out var s) ? s : 0f;
                    map.Dispose();
                    return v;
                }

                map.Dispose();
                return 0f;
            }

            var avant = Str(ArmySolvencyGateMode.Disabled);
            var eco026 = Str(ArmySolvencyGateMode.StockBalance);
            var eco027 = Str(ArmySolvencyGateMode.FluxCommitted);
            var milAvant = FocusStr(ArmySolvencyGateMode.Disabled, "MIL");
            var mil026 = FocusStr(ArmySolvencyGateMode.StockBalance, "MIL");
            var mil027 = FocusStr(ArmySolvencyGateMode.FluxCommitted, "MIL");
            var ven027 = FocusStr(ArmySolvencyGateMode.FluxCommitted, "VEN");
            var byz027 = FocusStr(ArmySolvencyGateMode.FluxCommitted, "BYZ");

            sb.AppendLine("=== VERDICT démilitarisation (t1000, seed 42195) ===");
            sb.AppendLine(
                $"worldArmyStr AVANT(eco_025)={avant:F0} | eco_026={eco026:F0} | eco_027={eco027:F0}");
            sb.AppendLine(
                $"delta vs AVANT: eco_026={eco026 - avant:F0} ({100f * (eco026 - avant) / avant:F1}%) ; " +
                $"eco_027={eco027 - avant:F0} ({100f * (eco027 - avant) / avant:F1}%)");
            sb.AppendLine(
                $"témoin solvable MIL armyStr: AVANT={milAvant:F0} eco_026={mil026:F0} eco_027={mil027:F0} " +
                $"(identique attendu)");
            sb.AppendLine(
                $"VEN eco_027 armyStr={ven027:F0} (cible ~3000-4000 = 3-4 régiments) ; " +
                $"BYZ eco_027 armyStr={byz027:F0} (ne doit pas régresser vs ~580 eco_026)");

            // Les guerres divergent dès qu'un insolvable désarme : comparer MIL eco_026 vs eco_027
            // (le gate ne doit pas freiner un solvable). AVANT≠eco_026 est attendu (guerres).
            if (System.Math.Abs(mil026 - mil027) > 1f)
                sb.AppendLine(
                    $"ALERT: MIL eco_026≠eco_027 ({mil026:F0} vs {mil027:F0}) — gate fuit sur solvable.");
            else
                sb.AppendLine("OK: témoin solvable MIL identique eco_026 / eco_027.");

            if (ven027 < 500f)
                sb.AppendLine("ALERT: VEN toujours verrouillé (~0) — gate flux non déverrouillé.");
            else if (ven027 > 8000f)
                sb.AppendLine("ALERT: VEN sur-recrute (piège coût différé / oscillation).");
            else
                sb.AppendLine("OK: VEN a une petite armée soutenable (hors zéro).");

            if (byz027 < 100f)
                sb.AppendLine($"ALERT: BYZ régresse (armyStr={byz027:F0}, attendu ~580).");
            else
                sb.AppendLine($"OK: BYZ stabilisé (armyStr={byz027:F0}).");

            var demil026 = (avant - eco026) / avant;
            var demil027 = (avant - eco027) / avant;
            sb.AppendLine(
                $"Démilitarisation vs AVANT: eco_026={100f * demil026:F1}% ; eco_027={100f * demil027:F1}%.");
            if (demil027 > 0.25f && demil027 > demil026 + 0.05f)
                sb.AppendLine(
                    "RÉGRESSION: eco_027 démilitarise nettement plus qu'eco_026 — à signaler.");
            else if (demil026 > 0.20f)
                sb.AppendLine(
                    "VERDICT: baisse worldArmyStr déjà présente à eco_026 (désarmement des insolvables), " +
                    "eco_027 du même ordre — pas une démilitarisation discrète additionnelle massive.");
            else
                sb.AppendLine("Pas de démilitarisation massive vs AVANT.");
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
