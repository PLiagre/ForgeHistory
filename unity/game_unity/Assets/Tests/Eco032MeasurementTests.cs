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
    /// <summary>Point d'entrée batchmode : -executeMethod VictoriaGame.Tests.Eco032BatchRunner.Run</summary>
    public static class Eco032BatchRunner
    {
        public static void Run()
        {
            Eco032MeasurementTests.RunMeasurementsAndWriteLog();
            UnityEngine.Debug.Log("Eco032BatchRunner: DONE");
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
            #endif
        }
    }

    [TestFixture]
    public class Eco032MeasurementTests
    {
        const uint Seed = 42195u;
        static readonly int[] SnapshotTicks = { 200, 500, 800, 1000 };

        /// <summary>Taux PerProvince essayés ; 0.2 = neutre arithmétique, 0.10 = retenu mesure.</summary>
        static readonly float[] RatesTried = { 0.10f, 0.15f, 0.20f, 0.25f };

        [Test]
        public void Eco032_MeasureAdminCostAtKeyTicks() => RunMeasurementsAndWriteLog();

        public static void RunMeasurementsAndWriteLog()
        {
            var previousMode = MilitaryUpkeepSystem.CostMode;
            var previousRate = MilitaryUpkeepSystem.AdminCostPerProvince;
            var logPath = Path.Combine(
                UnityEngine.Application.dataPath, "..", "Logs", "eco_032_measurements.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine(
                    $"=== eco_032 seed={Seed} DefaultAdminCostPerProvince=" +
                    $"{MilitaryUpkeepSystem.DefaultAdminCostPerProvince.ToString("F2", CultureInfo.InvariantCulture)} " +
                    $"BaseAdminCost={MilitaryUpkeepSystem.BaseAdminCost.ToString("F1", CultureInfo.InvariantCulture)} " +
                    $"ArmyUpkeepRate={MilitaryUpkeepSystem.ArmyUpkeepRate.ToString("G", CultureInfo.InvariantCulture)} " +
                    $"NavyUpkeepRate={MilitaryUpkeepSystem.NavyUpkeepRate.ToString("G", CultureInfo.InvariantCulture)} ===");
                sb.AppendLine(
                    "tried: FlatBaseline BaseAdminCost=0.5 (réf. dip_005), " +
                    "PerProvince 0.10 (retenu) / 0.15 / 0.20 (neutre arithm.) / 0.25");
                sb.AppendLine(
                    "Admin ∝ territoire : adminCost = ADMIN_COST_PER_PROVINCE × provinces(Owner). " +
                    "Pays à 0 province → adminCost=0.");
                sb.AppendLine();

                AppendScenarioFlat(sb);
                foreach (var rate in RatesTried)
                    AppendScenarioPerProvince(sb, rate);

                AppendVerdict(sb);

                File.WriteAllText(logPath, sb.ToString());
                UnityEngine.Debug.Log(sb.ToString());
            }
            finally
            {
                MilitaryUpkeepSystem.CostMode = previousMode;
                MilitaryUpkeepSystem.AdminCostPerProvince = previousRate;
            }
        }

        static void AppendScenarioFlat(StringBuilder sb)
        {
            MilitaryUpkeepSystem.CostMode = AdminCostMode.FlatBaseline;
            MilitaryUpkeepSystem.AdminCostPerProvince = MilitaryUpkeepSystem.DefaultAdminCostPerProvince;
            sb.AppendLine("=== A — FlatBaseline BaseAdminCost=0.5 (baseline dip_005) ===");

            foreach (var tick in SnapshotTicks)
                AppendTickLine(sb, tick);

            sb.AppendLine();
        }

        static void AppendScenarioPerProvince(StringBuilder sb, float rate)
        {
            MilitaryUpkeepSystem.CostMode = AdminCostMode.PerProvince;
            MilitaryUpkeepSystem.AdminCostPerProvince = rate;
            var label = System.Math.Abs(rate - MilitaryUpkeepSystem.DefaultAdminCostPerProvince) < 1e-6f
                ? "retenu"
                : "essai";
            sb.AppendLine(
                $"=== B — PerProvince ADMIN_COST_PER_PROVINCE=" +
                $"{rate.ToString("F2", CultureInfo.InvariantCulture)} ({label}) ===");

            foreach (var tick in SnapshotTicks)
                AppendTickLine(sb, tick);

            sb.AppendLine();
        }

        static void AppendTickLine(StringBuilder sb, int tick)
        {
            using var harness = new SimulationHarness(Seed);
            harness.RunTicks(tick);
            var em = harness.EntityManager;
            var war = CaptureWarSnapshot(em, tick);
            var eco = CaptureEcoMetrics(em);
            var detail = CaptureCountryDetail(em);

            var concluded = war.Victories + war.WhitePeaces;
            var ratio = concluded > 0 ? (float)war.Victories / concluded : 0f;

            sb.AppendLine(
                $"tick{tick}: bankrupt={eco.Bankrupt} totalDebt=" +
                $"{eco.TotalDebt.ToString("F1", CultureInfo.InvariantCulture)} " +
                $"totalAdminCost={eco.TotalAdminCost.ToString("F2", CultureInfo.InvariantCulture)} " +
                $"needsSatAvg={eco.NeedsSatAvg.ToString("F3", CultureInfo.InvariantCulture)} " +
                $"population={eco.Population} worldArmyStr=" +
                $"{eco.WorldArmyStr.ToString("F0", CultureInfo.InvariantCulture)} " +
                $"victories={war.Victories} whitePeaces={war.WhitePeaces} " +
                $"ratioV={(ratio * 100f).ToString("F1", CultureInfo.InvariantCulture)}% " +
                $"annexed={war.Annexed} countriesWithLand={war.CountriesWithLand} stuck={war.Stuck}");

            sb.AppendLine(
                $"  BIG {detail.BigTag} provinces={detail.BigProvinces} adminCost=" +
                $"{detail.BigAdmin.ToString("F2", CultureInfo.InvariantCulture)} " +
                $"Exp={detail.BigExpenses.ToString("F2", CultureInfo.InvariantCulture)} " +
                $"Debt={detail.BigDebt.ToString("F1", CultureInfo.InvariantCulture)}");
            sb.AppendLine(
                $"  SMALL {detail.SmallTag} provinces={detail.SmallProvinces} adminCost=" +
                $"{detail.SmallAdmin.ToString("F2", CultureInfo.InvariantCulture)} " +
                $"Exp={detail.SmallExpenses.ToString("F2", CultureInfo.InvariantCulture)} " +
                $"Debt={detail.SmallDebt.ToString("F1", CultureInfo.InvariantCulture)}");
            sb.AppendLine(
                $"  VEN provinces={detail.VenProvinces} adminCost=" +
                $"{detail.VenAdmin.ToString("F2", CultureInfo.InvariantCulture)} " +
                $"regs={detail.VenRegs} Debt={detail.VenDebt.ToString("F1", CultureInfo.InvariantCulture)}");
            sb.AppendLine(
                $"  BYZ provinces={detail.ByzProvinces} adminCost=" +
                $"{detail.ByzAdmin.ToString("F2", CultureInfo.InvariantCulture)} " +
                $"armyStr={detail.ByzArmy.ToString("F0", CultureInfo.InvariantCulture)} " +
                $"Debt={detail.ByzDebt.ToString("F1", CultureInfo.InvariantCulture)}");

            if (detail.LandlessTag.Length > 0)
            {
                sb.AppendLine(
                    $"  LANDLESS {detail.LandlessTag} provinces=0 adminCost=" +
                    $"{detail.LandlessAdmin.ToString("F2", CultureInfo.InvariantCulture)} " +
                    $"Debt={detail.LandlessDebt.ToString("F1", CultureInfo.InvariantCulture)} " +
                    $"Balance={detail.LandlessBalance.ToString("F1", CultureInfo.InvariantCulture)}");
            }
            else
            {
                sb.AppendLine("  LANDLESS: (aucun pays sans terre à ce tick)");
            }
        }

        static void AppendVerdict(StringBuilder sb)
        {
            sb.AppendLine("=== VERDICT eco_032 (t1000 + t800 dip, seed 42195) ===");

            MilitaryUpkeepSystem.CostMode = AdminCostMode.FlatBaseline;
            var flat1000 = CaptureFull(1000);
            var flat800 = CaptureFull(800);

            MilitaryUpkeepSystem.CostMode = AdminCostMode.PerProvince;
            MilitaryUpkeepSystem.AdminCostPerProvince = MilitaryUpkeepSystem.DefaultAdminCostPerProvince;
            var ret1000 = CaptureFull(1000);
            var ret800 = CaptureFull(800);

            // Dette landless entre t800 et t1000 (mode retenu).
            var landless800 = ret800.Detail;
            var landless1000 = ret1000.Detail;

            sb.AppendLine(
                $"FlatBaseline t1000: bankrupt={flat1000.Eco.Bankrupt} debt=" +
                $"{flat1000.Eco.TotalDebt.ToString("F1", CultureInfo.InvariantCulture)} " +
                $"totalAdmin={flat1000.Eco.TotalAdminCost.ToString("F2", CultureInfo.InvariantCulture)}");
            sb.AppendLine(
                $"PerProvince={MilitaryUpkeepSystem.DefaultAdminCostPerProvince.ToString("F2", CultureInfo.InvariantCulture)} t1000: " +
                $"bankrupt={ret1000.Eco.Bankrupt} debt=" +
                $"{ret1000.Eco.TotalDebt.ToString("F1", CultureInfo.InvariantCulture)} " +
                $"totalAdmin={ret1000.Eco.TotalAdminCost.ToString("F2", CultureInfo.InvariantCulture)} " +
                $"sat={ret1000.Eco.NeedsSatAvg.ToString("F3", CultureInfo.InvariantCulture)} " +
                $"pop={ret1000.Eco.Population} VEN.regs={ret1000.Detail.VenRegs} " +
                $"BYZ.army={ret1000.Detail.ByzArmy.ToString("F0", CultureInfo.InvariantCulture)}");

            var flatConcluded = flat800.War.Victories + flat800.War.WhitePeaces;
            var retConcluded = ret800.War.Victories + ret800.War.WhitePeaces;
            var flatRatio = flatConcluded > 0 ? (float)flat800.War.Victories / flatConcluded : 0f;
            var retRatio = retConcluded > 0 ? (float)ret800.War.Victories / retConcluded : 0f;

            sb.AppendLine(
                $"dip_005 t800 Flat: V={flat800.War.Victories} WP={flat800.War.WhitePeaces} " +
                $"ratio={(flatRatio * 100f).ToString("F1", CultureInfo.InvariantCulture)}% " +
                $"annexed={flat800.War.Annexed} countries={flat800.War.CountriesWithLand}");
            sb.AppendLine(
                $"dip_005 t800 PerProvince: V={ret800.War.Victories} WP={ret800.War.WhitePeaces} " +
                $"ratio={(retRatio * 100f).ToString("F1", CultureInfo.InvariantCulture)}% " +
                $"annexed={ret800.War.Annexed} countries={ret800.War.CountriesWithLand} " +
                $"stuck={ret800.War.Stuck}");

            // Critères
            if (ret1000.Eco.Bankrupt >= 2 && ret1000.Eco.Bankrupt <= 4)
                sb.AppendLine($"OK banqueroutes: {ret1000.Eco.Bankrupt} (~2-4, Flat={flat1000.Eco.Bankrupt}).");
            else if (ret1000.Eco.Bankrupt < flat1000.Eco.Bankrupt)
                sb.AppendLine(
                    $"PARTIEL banqueroutes: {ret1000.Eco.Bankrupt} (Flat={flat1000.Eco.Bankrupt}, cible ~2-4).");
            else
                sb.AppendLine(
                    $"ALERT banqueroutes: {ret1000.Eco.Bankrupt} (Flat={flat1000.Eco.Bankrupt}, cible ~2-4).");

            if (ret1000.Eco.TotalDebt < 2104.8f)
                sb.AppendLine(
                    $"OK dette sous dip_005: {ret1000.Eco.TotalDebt.ToString("F1", CultureInfo.InvariantCulture)} < 2104.8.");
            else
                sb.AppendLine(
                    $"ALERT dette: {ret1000.Eco.TotalDebt.ToString("F1", CultureInfo.InvariantCulture)} ≥ 2104.8.");

            if (retRatio >= 0.70f && ret800.War.Annexed >= 25 && ret800.War.Stuck == 0)
                sb.AppendLine(
                    $"OK dip_005 non défait: ratio={(retRatio * 100f).ToString("F1", CultureInfo.InvariantCulture)}% " +
                    $"annexed={ret800.War.Annexed} stuck=0.");
            else if (retRatio >= 0.60f && ret800.War.Stuck == 0)
                sb.AppendLine(
                    $"PARTIEL dip_005: ratio={(retRatio * 100f).ToString("F1", CultureInfo.InvariantCulture)}% " +
                    $"annexed={ret800.War.Annexed}.");
            else
                sb.AppendLine(
                    $"ALERT dip_005 régression: ratio={(retRatio * 100f).ToString("F1", CultureInfo.InvariantCulture)}% " +
                    $"annexed={ret800.War.Annexed} stuck={ret800.War.Stuck}.");

            if (ret1000.Detail.BigAdmin > ret1000.Detail.SmallAdmin + 0.01f &&
                ret1000.Detail.BigProvinces > ret1000.Detail.SmallProvinces)
            {
                sb.AppendLine(
                    $"OK surextension: BIG {ret1000.Detail.BigTag} admin=" +
                    $"{ret1000.Detail.BigAdmin.ToString("F2", CultureInfo.InvariantCulture)} " +
                    $"> SMALL {ret1000.Detail.SmallTag} admin=" +
                    $"{ret1000.Detail.SmallAdmin.ToString("F2", CultureInfo.InvariantCulture)}.");
            }
            else
            {
                sb.AppendLine("ALERT surextension: gros pays ne paie pas plus qu'un petit.");
            }

            if (landless1000.LandlessTag.Length > 0)
            {
                var debtGrew = landless1000.LandlessDebt > landless800.LandlessDebt + 0.5f;
                if (landless1000.LandlessAdmin < 1e-4f && !debtGrew)
                {
                    sb.AppendLine(
                        $"OK pays mort {landless1000.LandlessTag}: adminCost=0, dette t800→t1000 " +
                        $"{landless800.LandlessDebt.ToString("F1", CultureInfo.InvariantCulture)}→" +
                        $"{landless1000.LandlessDebt.ToString("F1", CultureInfo.InvariantCulture)} (stable/↓).");
                }
                else
                {
                    sb.AppendLine(
                        $"ALERT pays mort {landless1000.LandlessTag}: admin=" +
                        $"{landless1000.LandlessAdmin.ToString("F2", CultureInfo.InvariantCulture)} " +
                        $"debt {landless800.LandlessDebt.ToString("F1", CultureInfo.InvariantCulture)}→" +
                        $"{landless1000.LandlessDebt.ToString("F1", CultureInfo.InvariantCulture)}.");
                }
            }
            else
            {
                sb.AppendLine("ALERT: aucun pays sans terre à t1000 (attendu après dip_005).");
            }

            // Neutralité : 0.20 ≈ Flat (10) ; 0.10 retenu pour bankrupt cible (totalAdmin ~5).
            if (ret1000.Eco.TotalAdminCost >= 4f && ret1000.Eco.TotalAdminCost <= 15f)
                sb.AppendLine(
                    $"OK totalAdmin mondial ordre cible: " +
                    $"{ret1000.Eco.TotalAdminCost.ToString("F2", CultureInfo.InvariantCulture)} " +
                    $"(Flat={flat1000.Eco.TotalAdminCost.ToString("F2", CultureInfo.InvariantCulture)}; " +
                    "0.10 < neutre 0.20 pour atteindre bankrupt~2-4).");
            else
                sb.AppendLine(
                    $"ALERT totalAdmin: {ret1000.Eco.TotalAdminCost.ToString("F2", CultureInfo.InvariantCulture)} hors ~4-15.");

            // Acquis eco : Flat baseline post-dip_005 a aussi VEN conquis (0 rég) — pas une régression eco_032.
            var flatVenRegs = flat1000.Detail.VenRegs;
            if (ret1000.Detail.VenRegs >= 2 && ret1000.Detail.VenRegs <= 5 &&
                ret1000.Detail.ByzArmy >= 450f &&
                System.Math.Abs(ret1000.Eco.NeedsSatAvg - 0.72f) < 0.05f &&
                ret1000.Eco.Population >= 130000 && ret1000.Eco.Population <= 150000)
            {
                sb.AppendLine(
                    $"OK acquis eco_026→031: VEN={ret1000.Detail.VenRegs}rég " +
                    $"BYZ={ret1000.Detail.ByzArmy.ToString("F0", CultureInfo.InvariantCulture)} " +
                    $"sat={ret1000.Eco.NeedsSatAvg.ToString("F3", CultureInfo.InvariantCulture)} " +
                    $"pop={ret1000.Eco.Population}.");
            }
            else if (flatVenRegs == 0 && ret1000.Detail.VenRegs == 0 &&
                     ret1000.Eco.Population >= 130000 && ret1000.Eco.Population <= 150000)
            {
                sb.AppendLine(
                    $"OK acquis (post-dip_005): VEN conquis aussi en Flat (regs={flatVenRegs}), " +
                    $"BYZ army={ret1000.Detail.ByzArmy.ToString("F0", CultureInfo.InvariantCulture)}, " +
                    $"sat={ret1000.Eco.NeedsSatAvg.ToString("F3", CultureInfo.InvariantCulture)} " +
                    $"pop={ret1000.Eco.Population} — pas de régression eco_032.");
            }
            else
            {
                sb.AppendLine(
                    $"ALERT acquis: VEN={ret1000.Detail.VenRegs} BYZ=" +
                    $"{ret1000.Detail.ByzArmy.ToString("F0", CultureInfo.InvariantCulture)} " +
                    $"sat={ret1000.Eco.NeedsSatAvg.ToString("F3", CultureInfo.InvariantCulture)} " +
                    $"pop={ret1000.Eco.Population}.");
            }

            sb.AppendLine(
                $"ADMIN_COST_PER_PROVINCE retenu=" +
                $"{MilitaryUpkeepSystem.DefaultAdminCostPerProvince.ToString("F2", CultureInfo.InvariantCulture)} " +
                $"(essayés 0.10 / 0.15 / 0.20 / 0.25 ; FlatBaseline=0.5 pour A/B). " +
                "0.20 neutre total mondial mais bankrupt t1000=7 ; 0.10 → bankrupt=4 debt~751.");
        }

        struct WarSnapshot
        {
            public int Victories;
            public int WhitePeaces;
            public int Annexed;
            public int CountriesWithLand;
            public int Stuck;
        }

        struct EcoMetrics
        {
            public float TotalDebt;
            public int Bankrupt;
            public float TotalAdminCost;
            public float NeedsSatAvg;
            public int Population;
            public float WorldArmyStr;
        }

        struct CountryDetail
        {
            public FixedString32Bytes BigTag;
            public int BigProvinces;
            public float BigAdmin;
            public float BigExpenses;
            public float BigDebt;
            public FixedString32Bytes SmallTag;
            public int SmallProvinces;
            public float SmallAdmin;
            public float SmallExpenses;
            public float SmallDebt;
            public int VenProvinces;
            public float VenAdmin;
            public int VenRegs;
            public float VenDebt;
            public int ByzProvinces;
            public float ByzAdmin;
            public float ByzArmy;
            public float ByzDebt;
            public FixedString32Bytes LandlessTag;
            public float LandlessAdmin;
            public float LandlessDebt;
            public float LandlessBalance;
        }

        struct FullSnap
        {
            public WarSnapshot War;
            public EcoMetrics Eco;
            public CountryDetail Detail;
        }

        static FullSnap CaptureFull(int tick)
        {
            using var harness = new SimulationHarness(Seed);
            harness.RunTicks(tick);
            var em = harness.EntityManager;
            return new FullSnap
            {
                War = CaptureWarSnapshot(em, tick),
                Eco = CaptureEcoMetrics(em),
                Detail = CaptureCountryDetail(em)
            };
        }

        static WarSnapshot CaptureWarSnapshot(EntityManager em, int currentTick)
        {
            var snap = new WarSnapshot();

            using var warQuery = em.CreateEntityQuery(ComponentType.ReadOnly<WarData>());
            using var wars = warQuery.ToComponentDataArray<WarData>(Allocator.Temp);

            for (var i = 0; i < wars.Length; i++)
            {
                var war = wars[i];
                if (war.IsActive)
                {
                    if (currentTick - war.StartTick > 150)
                        snap.Stuck++;
                    continue;
                }

                if (war.EndTick <= 0)
                    continue;

                if (System.Math.Abs(war.WarScore) >= 60f)
                    snap.Victories++;
                else
                    snap.WhitePeaces++;
            }

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
            var provinceCounts = CountProvincesByOwner(em);

            using var countryQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<CountryData>(),
                ComponentType.ReadOnly<TreasuryData>());
            using var countries = countryQuery.ToEntityArray(Allocator.Temp);
            using var treasuries = countryQuery.ToComponentDataArray<TreasuryData>(Allocator.Temp);

            for (var i = 0; i < countries.Length; i++)
            {
                eco.TotalDebt += treasuries[i].Debt;
                if (treasuries[i].BankruptcyTick > 0)
                    eco.Bankrupt++;
                eco.TotalAdminCost += ComputeAdminCost(countries[i], provinceCounts);
            }

            provinceCounts.Dispose();

            using var armyQuery = em.CreateEntityQuery(ComponentType.ReadOnly<ArmyData>());
            using var armies = armyQuery.ToComponentDataArray<ArmyData>(Allocator.Temp);
            for (var i = 0; i < armies.Length; i++)
                eco.WorldArmyStr += armies[i].Strength;

            double weightedSat = 0.0;
            var totalPop = 0;
            using var popQuery = em.CreateEntityQuery(ComponentType.ReadOnly<PopData>());
            using var pops = popQuery.ToComponentDataArray<PopData>(Allocator.Temp);
            for (var i = 0; i < pops.Length; i++)
            {
                totalPop += pops[i].Size;
                weightedSat += pops[i].NeedsSatisfaction * pops[i].Size;
            }

            eco.Population = totalPop;
            eco.NeedsSatAvg = totalPop > 0 ? (float)(weightedSat / totalPop) : 0f;
            return eco;
        }

        static CountryDetail CaptureCountryDetail(EntityManager em)
        {
            var detail = new CountryDetail();
            var provinceCounts = CountProvincesByOwner(em);
            var armyByCountry = SumArmyByCountry(em);
            var regsByCountry = CountRegsByCountry(em);

            using var countryQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<CountryData>(),
                ComponentType.ReadOnly<TreasuryData>());
            using var countries = countryQuery.ToEntityArray(Allocator.Temp);
            using var countryData = countryQuery.ToComponentDataArray<CountryData>(Allocator.Temp);
            using var treasuries = countryQuery.ToComponentDataArray<TreasuryData>(Allocator.Temp);

            var maxProv = -1;
            var minProvWithLand = int.MaxValue;

            for (var i = 0; i < countries.Length; i++)
            {
                var entity = countries[i];
                var tag = countryData[i].Tag;
                var tagStr = tag.ToString();
                provinceCounts.TryGetValue(entity, out var prov);
                var admin = ComputeAdminCost(entity, provinceCounts);
                var treasury = treasuries[i];

                if (prov > maxProv)
                {
                    maxProv = prov;
                    detail.BigTag = tag;
                    detail.BigProvinces = prov;
                    detail.BigAdmin = admin;
                    detail.BigExpenses = treasury.Expenses;
                    detail.BigDebt = treasury.Debt;
                }

                if (prov >= 1 && prov < minProvWithLand)
                {
                    minProvWithLand = prov;
                    detail.SmallTag = tag;
                    detail.SmallProvinces = prov;
                    detail.SmallAdmin = admin;
                    detail.SmallExpenses = treasury.Expenses;
                    detail.SmallDebt = treasury.Debt;
                }

                if (prov == 0 && detail.LandlessTag.Length == 0)
                {
                    detail.LandlessTag = tag;
                    detail.LandlessAdmin = admin;
                    detail.LandlessDebt = treasury.Debt;
                    detail.LandlessBalance = treasury.Balance;
                }

                if (tagStr == "VEN")
                {
                    detail.VenProvinces = prov;
                    detail.VenAdmin = admin;
                    detail.VenDebt = treasury.Debt;
                    detail.VenRegs = regsByCountry.TryGetValue(entity, out var r) ? r : 0;
                }
                else if (tagStr == "BYZ")
                {
                    detail.ByzProvinces = prov;
                    detail.ByzAdmin = admin;
                    detail.ByzDebt = treasury.Debt;
                    detail.ByzArmy = armyByCountry.TryGetValue(entity, out var a) ? a : 0f;
                }
            }

            provinceCounts.Dispose();
            armyByCountry.Dispose();
            regsByCountry.Dispose();
            return detail;
        }

        static float ComputeAdminCost(Entity country, NativeHashMap<Entity, int> provinceCounts)
        {
            if (MilitaryUpkeepSystem.CostMode == AdminCostMode.FlatBaseline)
                return MilitaryUpkeepSystem.BaseAdminCost;

            provinceCounts.TryGetValue(country, out var prov);
            return MilitaryUpkeepSystem.AdminCostPerProvince * prov;
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
    }
}
