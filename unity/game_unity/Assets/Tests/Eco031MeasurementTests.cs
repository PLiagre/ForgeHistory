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
    /// <summary>Point d'entrée batchmode : -executeMethod VictoriaGame.Tests.Eco031BatchRunner.Run</summary>
    public static class Eco031BatchRunner
    {
        public static void Run()
        {
            Eco031MeasurementTests.RunMeasurementsAndWriteLog();
            UnityEngine.Debug.Log("Eco031BatchRunner: DONE");
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
            #endif
        }
    }

    [TestFixture]
    public class Eco031MeasurementTests
    {
        const uint Seed = 42195u;
        static readonly int[] SnapshotTicks = { 50, 200, 500, 1000 };
        static readonly string[] FocusTags = { "VEN", "BYZ", "FRA" };
        static readonly float[] ScalesTried = { 0f, 0.05f, 0.1f };

        [Test]
        public void Eco031_MeasureRecruitCostGoldAtKeyTicks() => RunMeasurementsAndWriteLog();

        public static void RunMeasurementsAndWriteLog()
        {
            var previousScale = TemplateRecruitSystem.RecruitCostScale;
            var logPath = Path.Combine(
                UnityEngine.Application.dataPath, "..", "Logs", "eco_031_measurements.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine(
                    $"=== eco_031 seed={Seed} DefaultRecruitCostScale=" +
                    $"{TemplateRecruitSystem.DefaultRecruitCostScale.ToString("F2", CultureInfo.InvariantCulture)} " +
                    $"RecruitCostGold=10 GateMode={ArmyDisbandmentSystem.GateMode} ===");
                sb.AppendLine(
                    "tried: RECRUIT_COST_SCALE=0 (réf. eco_030, recrutement gratuit), " +
                    "0.05 (retenu — coût capital 0.5/rég), 0.1 (essai — coût 1.0/rég)");
                sb.AppendLine(
                    "Note: coût via Expenses (pas SupplyLevel). Gate recrutement inchangé (flux eco_027). " +
                    "MaintenanceCostGold supprimé.");
                sb.AppendLine();

                AppendScenario(sb, "A — RECRUIT_COST_SCALE=0 (réf. eco_030)", 0f);
                AppendScenario(sb, "B — RECRUIT_COST_SCALE=0.05 (retenu)", 0.05f);
                AppendScenario(sb, "C — RECRUIT_COST_SCALE=0.1 (essai)", 0.1f);

                AppendBuildUpDetail(sb);
                AppendVerdict(sb);

                File.WriteAllText(logPath, sb.ToString());
                UnityEngine.Debug.Log(sb.ToString());
            }
            finally
            {
                TemplateRecruitSystem.RecruitCostScale = previousScale;
            }
        }

        static void AppendScenario(StringBuilder sb, string title, float scale)
        {
            TemplateRecruitSystem.RecruitCostScale = scale;
            sb.AppendLine(
                $"=== {title} RECRUIT_COST_SCALE={scale.ToString("F2", CultureInfo.InvariantCulture)} ===");

            foreach (var tick in SnapshotTicks)
            {
                using var harness = new SimulationHarness(Seed);
                harness.RunTicks(tick);
                var em = harness.EntityManager;

                var army = SumArmyStrengthByCountry(em);
                var regs = CountRegimentsByCountry(em);
                var supply = CaptureSupplyByCountry(em);
                var snaps = CaptureCountrySnapshots(em, army, regs, supply);
                var metrics = Aggregate(snaps);
                var (needsSatAvg, population) = CapturePopMetrics(em);

                sb.AppendLine(
                    $"tick{tick}: totalDebt={metrics.TotalDebt:F1} bankrupt={metrics.BankruptCountries} " +
                    $"worldArmyStr={metrics.WorldArmyStr:F0} needsSatAvg={needsSatAvg:F3} " +
                    $"population={population} minSupply={metrics.MinSupply:F3}");

                foreach (var tag in FocusTags)
                {
                    foreach (var snap in snaps)
                    {
                        if (snap.Tag.ToString() != tag)
                            continue;

                        sb.AppendLine(
                            $"  {tag}: Balance={snap.Balance:F1} Debt={snap.Debt:F1} " +
                            $"Income={snap.Income:F1} Expenses={snap.Expenses:F1} " +
                            $"armyStr={snap.ArmyStrength:F0} regiments={snap.RegimentCount} " +
                            $"supply={snap.SupplyLevel:F3}");
                    }
                }

                army.Dispose();
                regs.Dispose();
                supply.Dispose();
            }

            sb.AppendLine();
        }

        /// <summary>
        /// Build-up t1→t20 : FRA recrute → Expenses plus élevés à scale&gt;0 vs scale=0
        /// (preuve que le coût frappe le trésor, pas SupplyLevel).
        /// </summary>
        static void AppendBuildUpDetail(StringBuilder sb)
        {
            sb.AppendLine("=== BUILD-UP détail (t1..t20, FRA) scale=0 vs 0.05 ===");
            AppendBuildUpSeries(sb, 0f);
            AppendBuildUpSeries(sb, 0.05f);
            sb.AppendLine();
        }

        static void AppendBuildUpSeries(StringBuilder sb, float scale)
        {
            TemplateRecruitSystem.RecruitCostScale = scale;
            sb.AppendLine($"  scale={scale.ToString("F2", CultureInfo.InvariantCulture)}:");

            using var harness = new SimulationHarness(Seed);
            for (var tick = 1; tick <= 20; tick++)
            {
                harness.RunTicks(1);
                if (tick != 1 && tick != 5 && tick != 10 && tick != 15 && tick != 20)
                    continue;

                var em = harness.EntityManager;
                var army = SumArmyStrengthByCountry(em);
                var regs = CountRegimentsByCountry(em);
                var supply = CaptureSupplyByCountry(em);
                var snaps = CaptureCountrySnapshots(em, army, regs, supply);
                var fra = FindTag(snaps, "FRA");
                sb.AppendLine(
                    $"    t{tick}: FRA Balance={fra.Balance:F1} Debt={fra.Debt:F1} " +
                    $"Inc={fra.Income:F1} Exp={fra.Expenses:F1} regs={fra.RegimentCount} " +
                    $"supply={fra.SupplyLevel:F3}");
                army.Dispose();
                regs.Dispose();
                supply.Dispose();
            }
        }

        static void AppendVerdict(StringBuilder sb)
        {
            var s0 = CaptureAt(1000, 0f);
            var sRetained = CaptureAt(1000, TemplateRecruitSystem.DefaultRecruitCostScale);
            var s10 = CaptureAt(1000, 0.1f);

            // Build-up t10 : Expenses FRA à scale retenu vs 0.
            var fra0 = CaptureCountryAt(10, 0f, "FRA");
            var fraRet = CaptureCountryAt(10, TemplateRecruitSystem.DefaultRecruitCostScale, "FRA");

            sb.AppendLine("=== VERDICT eco_031 (t1000, seed 42195) ===");
            sb.AppendLine(
                $"scale=0: debt={s0.TotalDebt:F1} bankrupt={s0.BankruptCountries} " +
                $"army={s0.WorldArmyStr:F0} sat={s0.NeedsSatAvg:F3} pop={s0.Population} " +
                $"VEN.regs={s0.Ven.RegimentCount} BYZ.army={s0.Byz.ArmyStrength:F0} " +
                $"minSupply={s0.MinSupply:F3}");
            sb.AppendLine(
                $"scale={TemplateRecruitSystem.DefaultRecruitCostScale.ToString("F2", CultureInfo.InvariantCulture)}: " +
                $"debt={sRetained.TotalDebt:F1} bankrupt={sRetained.BankruptCountries} " +
                $"army={sRetained.WorldArmyStr:F0} sat={sRetained.NeedsSatAvg:F3} pop={sRetained.Population} " +
                $"VEN.regs={sRetained.Ven.RegimentCount} BYZ.army={sRetained.Byz.ArmyStrength:F0} " +
                $"minSupply={sRetained.MinSupply:F3}");
            sb.AppendLine(
                $"scale=0.1: debt={s10.TotalDebt:F1} bankrupt={s10.BankruptCountries} " +
                $"army={s10.WorldArmyStr:F0} VEN.regs={s10.Ven.RegimentCount} " +
                $"BYZ.army={s10.Byz.ArmyStrength:F0}");

            // Preuve : coût frappe Expenses (build-up), pas SupplyLevel.
            var expDelta = fraRet.Expenses - fra0.Expenses;
            if (expDelta > 0.01f)
            {
                sb.AppendLine(
                    $"OK COÛT TRÉSOR: FRA t10 Expenses scale0={fra0.Expenses:F2} → " +
                    $"scaleRet={fraRet.Expenses:F2} (Δ={expDelta:F2} > 0).");
            }
            else
            {
                sb.AppendLine(
                    $"ALERT COÛT TRÉSOR: FRA t10 Expenses inchangés " +
                    $"(scale0={fra0.Expenses:F2} scaleRet={fraRet.Expenses:F2}).");
            }

            if (System.Math.Abs(fra0.SupplyLevel - fraRet.SupplyLevel) < 1e-3f)
            {
                sb.AppendLine(
                    $"OK SupplyLevel non ponctionné: FRA t10 supply={fraRet.SupplyLevel:F3} " +
                    "(identique scale=0 vs retenu).");
            }
            else
            {
                sb.AppendLine(
                    $"ALERT SupplyLevel: FRA t10 scale0={fra0.SupplyLevel:F3} " +
                    $"vs retenu={fraRet.SupplyLevel:F3}.");
            }

            if (System.Math.Abs(s0.NeedsSatAvg - sRetained.NeedsSatAvg) < 1e-4 &&
                s0.Population == sRetained.Population)
            {
                sb.AppendLine(
                    $"OK isolation needs/pop: sat={sRetained.NeedsSatAvg:F3} pop={sRetained.Population}.");
            }
            else
            {
                sb.AppendLine(
                    $"ALERT isolation: s0 sat={s0.NeedsSatAvg:F3} pop={s0.Population} vs " +
                    $"ret sat={sRetained.NeedsSatAvg:F3} pop={sRetained.Population}");
            }

            if (sRetained.TotalDebt <= 1100f)
                sb.AppendLine($"OK DETTE BORNÉE: totalDebt={sRetained.TotalDebt:F1} ≤ ~1000 (+marge).");
            else
                sb.AppendLine($"ALERT SPIRALE: totalDebt={sRetained.TotalDebt:F1} > 1100.");

            if (sRetained.BankruptCountries >= 2 && sRetained.BankruptCountries <= 4)
                sb.AppendLine($"OK banqueroutes: {sRetained.BankruptCountries} (~2-3).");
            else
                sb.AppendLine($"ALERT banqueroutes: {sRetained.BankruptCountries} (attendu ~2-3).");

            if (sRetained.Ven.RegimentCount >= 2 && sRetained.Ven.RegimentCount <= 5 &&
                sRetained.Byz.ArmyStrength >= 450f &&
                sRetained.WorldArmyStr >= 20000f && sRetained.WorldArmyStr <= 60000f)
            {
                sb.AppendLine(
                    $"OK acquis eco_026/027/029/030: VEN~{sRetained.Ven.RegimentCount} rég, " +
                    $"BYZ armyStr={sRetained.Byz.ArmyStrength:F0}, worldArmy={sRetained.WorldArmyStr:F0}.");
            }
            else
            {
                sb.AppendLine(
                    $"ALERT acquis: VEN regs={sRetained.Ven.RegimentCount} " +
                    $"BYZ army={sRetained.Byz.ArmyStrength:F0} worldArmy={sRetained.WorldArmyStr:F0}.");
            }

            if (sRetained.Ven.RegimentCount == 0)
                sb.AppendLine("ALERT VEN RE-VERROUILLÉ: 0 régiments — le coût capital a cassé eco_027.");
            else
                sb.AppendLine($"OK VEN NON re-verrouillé: {sRetained.Ven.RegimentCount} régiments.");

            sb.AppendLine(
                $"RECRUIT_COST_SCALE retenu=" +
                $"{TemplateRecruitSystem.DefaultRecruitCostScale.ToString("F2", CultureInfo.InvariantCulture)} " +
                $"(0 = eco_030 debt={s0.TotalDebt:F0}/VEN={s0.Ven.RegimentCount}; " +
                $"0.1 essayé debt={s10.TotalDebt:F0}/VEN={s10.Ven.RegimentCount}).");
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
            public float SupplyLevel;
        }

        struct AggregateMetrics
        {
            public float TotalDebt;
            public int BankruptCountries;
            public float WorldArmyStr;
            public float MinSupply;
        }

        struct WorldSnap
        {
            public float TotalDebt;
            public int BankruptCountries;
            public float WorldArmyStr;
            public float NeedsSatAvg;
            public int Population;
            public float MinSupply;
            public CountrySnapshot Ven;
            public CountrySnapshot Byz;
        }

        static WorldSnap CaptureAt(int ticks, float scale)
        {
            TemplateRecruitSystem.RecruitCostScale = scale;
            using var harness = new SimulationHarness(Seed);
            harness.RunTicks(ticks);
            var em = harness.EntityManager;
            var army = SumArmyStrengthByCountry(em);
            var regs = CountRegimentsByCountry(em);
            var supply = CaptureSupplyByCountry(em);
            var snaps = CaptureCountrySnapshots(em, army, regs, supply);
            var agg = Aggregate(snaps);
            var (sat, pop) = CapturePopMetrics(em);
            var ven = FindTag(snaps, "VEN");
            var byz = FindTag(snaps, "BYZ");
            army.Dispose();
            regs.Dispose();
            supply.Dispose();
            return new WorldSnap
            {
                TotalDebt = agg.TotalDebt,
                BankruptCountries = agg.BankruptCountries,
                WorldArmyStr = agg.WorldArmyStr,
                NeedsSatAvg = sat,
                Population = pop,
                MinSupply = agg.MinSupply,
                Ven = ven,
                Byz = byz
            };
        }

        static CountrySnapshot CaptureCountryAt(int ticks, float scale, string tag)
        {
            TemplateRecruitSystem.RecruitCostScale = scale;
            using var harness = new SimulationHarness(Seed);
            harness.RunTicks(ticks);
            var em = harness.EntityManager;
            var army = SumArmyStrengthByCountry(em);
            var regs = CountRegimentsByCountry(em);
            var supply = CaptureSupplyByCountry(em);
            var snaps = CaptureCountrySnapshots(em, army, regs, supply);
            var found = FindTag(snaps, tag);
            army.Dispose();
            regs.Dispose();
            supply.Dispose();
            return found;
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
            var bankrupt = 0;
            float worldArmy = 0f;
            float minSupply = float.MaxValue;

            foreach (var snap in snapshots)
            {
                totalDebt += snap.Debt;
                if (snap.Debt > 0.01f && snap.Balance <= 0f)
                    bankrupt++;
                worldArmy += snap.ArmyStrength;
                if (snap.SupplyLevel < minSupply)
                    minSupply = snap.SupplyLevel;
            }

            if (minSupply == float.MaxValue)
                minSupply = 0f;

            return new AggregateMetrics
            {
                TotalDebt = totalDebt,
                BankruptCountries = bankrupt,
                WorldArmyStr = worldArmy,
                MinSupply = minSupply
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

        static NativeHashMap<Entity, float> CaptureSupplyByCountry(EntityManager em)
        {
            var map = new NativeHashMap<Entity, float>(32, Allocator.Temp);
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<ArmyData>());
            using var armies = query.ToComponentDataArray<ArmyData>(Allocator.Temp);

            for (var i = 0; i < armies.Length; i++)
            {
                var country = armies[i].Country;
                if (country == Entity.Null)
                    continue;

                // Une armée / pays typiquement ; on garde le min si plusieurs.
                if (map.TryGetValue(country, out var current))
                    map[country] = current < armies[i].SupplyLevel ? current : armies[i].SupplyLevel;
                else
                    map[country] = armies[i].SupplyLevel;
            }

            return map;
        }

        static List<CountrySnapshot> CaptureCountrySnapshots(
            EntityManager em,
            NativeHashMap<Entity, float> armyStrengthByCountry,
            NativeHashMap<Entity, int> regimentCounts,
            NativeHashMap<Entity, float> supplyByCountry)
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
                var supply = supplyByCountry.TryGetValue(entity, out var s) ? s : 0f;

                results.Add(new CountrySnapshot
                {
                    Tag = countryData[i].Tag,
                    Balance = treasury.Balance,
                    Debt = treasury.Debt,
                    Income = treasury.Income,
                    Expenses = treasury.Expenses,
                    ArmyStrength = armyStr,
                    RegimentCount = regs,
                    SupplyLevel = supply
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
