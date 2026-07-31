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
    /// <summary>Point d'entrée batchmode : -executeMethod VictoriaGame.Tests.Dip006BatchRunner.Run</summary>
    public static class Dip006BatchRunner
    {
        public static void Run()
        {
            Dip006MeasurementTests.RunMeasurementsAndWriteLog();
            UnityEngine.Debug.Log("Dip006BatchRunner: DONE");
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
            #endif
        }
    }

    [TestFixture]
    public class Dip006MeasurementTests
    {
        const uint Seed = 42195u;
        static readonly int[] SnapshotTicks = { 200, 500, 800, 1000 };

        /// <summary>
        /// Facteurs essayés. 1.0 = ancrage. Extrêmes ≤0.2 documentés (dette/armée cassées,
        /// countries non améliorés). Plateau utile : 0.5 / 0.3.
        /// </summary>
        static readonly float[] FactorsTried = { 1.0f, 0.7f, 0.5f, 0.3f, 0.2f, 0.1f, 0.0f };

        struct Config
        {
            public string Label;
            public float NonCoreYieldFactor;
            public bool IsAnchor;
            public bool IsRetainedCandidate;
        }

        struct Snap
        {
            public int Victories;
            public int WhitePeaces;
            public int Annexed;
            public int CountriesWithLand;
            public int MaxProvincesOneCountry;
            public int NonCoreProvinces;
            public int TotalProvincesOwned;
            public int Stuck;
            public float TotalDebt;
            public int Bankrupt;
            public float WorldArmyStr;
            public float ZombieArmyStr;
            public float NeedsSatAvg;
            public int Population;
        }

        [Test]
        public void Dip006_MeasureNonCoreYieldAtKeyTicks() => RunMeasurementsAndWriteLog();

        public static void RunMeasurementsAndWriteLog()
        {
            var prevFactor = TaxSystem.NonCoreYieldFactor;
            var logPath = Path.Combine(
                UnityEngine.Application.dataPath, "..", "Logs", "dip_006_measurements.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine(
                    $"=== dip_006 seed={Seed} DefaultNonCoreYieldFactor=" +
                    $"{Fmt(TaxSystem.DefaultNonCoreYieldFactor)} " +
                    $"ReinforceRateFactor={Fmt(ArmyDisbandmentSystem.ReinforceRateFactor)} " +
                    $"(eco_034 intact) OccupationScoreRate=" +
                    $"{Fmt(OccupationScoreSystem.OccupationScoreRate)} (dip_005 intact) ===");
                sb.AppendLine(
                    "Frein: revenu province (Owner != Core) × NON_CORE_YIELD_FACTOR " +
                    "sur impôt de base ET production (après priceEff).");
                sb.AppendLine(
                    "tried: 1.0 (ancrage), 0.7, 0.5, 0.3, 0.2, 0.1, 0.0 — courbe consolidation.");
                sb.AppendLine(
                    "Colonnes: countriesWithLand maxProvinces nonCore/owned " +
                    "V WP ratioV annexed debt bankrupt army zombie sat pop");
                sb.AppendLine();

                AppendT0Sanity(sb);

                var configs = BuildConfigs();
                var t800ByConfig = new Snap[configs.Length];
                var t1000ByConfig = new Snap[configs.Length];

                for (var c = 0; c < configs.Length; c++)
                {
                    TaxSystem.NonCoreYieldFactor = configs[c].NonCoreYieldFactor;
                    sb.AppendLine($"=== {configs[c].Label} ===");

                    for (var i = 0; i < SnapshotTicks.Length; i++)
                    {
                        var tick = SnapshotTicks[i];
                        using var harness = new SimulationHarness(Seed);
                        harness.RunTicks(tick);
                        var snap = CaptureSnap(harness.EntityManager, tick);
                        if (tick == 800)
                            t800ByConfig[c] = snap;
                        if (tick == 1000)
                            t1000ByConfig[c] = snap;

                        AppendTickLine(sb, tick, snap);
                    }

                    sb.AppendLine();
                }

                AppendCalibrationCurve(sb, configs, t800ByConfig, t1000ByConfig);
                AppendRetainedDetail(sb, configs, t800ByConfig, t1000ByConfig);
                AppendVerdict(sb, configs, t800ByConfig, t1000ByConfig);

                File.WriteAllText(logPath, sb.ToString());
                UnityEngine.Debug.Log(sb.ToString());
            }
            finally
            {
                TaxSystem.NonCoreYieldFactor = prevFactor;
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
                    System.Math.Abs(f - TaxSystem.DefaultNonCoreYieldFactor) < 1e-6f;
                var tag = isAnchor
                    ? "A — ANCRAGE (état actuel)"
                    : isRetained
                        ? "R — RETENU (défaut production)"
                        : $"F{i}";
                list.Add(new Config
                {
                    Label = $"{tag} NonCoreYieldFactor={Fmt(f)}",
                    NonCoreYieldFactor = f,
                    IsAnchor = isAnchor,
                    IsRetainedCandidate = isRetained
                });
            }

            return list.ToArray();
        }

        static void AppendT0Sanity(StringBuilder sb)
        {
            TaxSystem.NonCoreYieldFactor = 1.0f;
            using var harness = new SimulationHarness(Seed);
            // t0 = après InitializationSystemGroup, 0 tick de SimulationSystemGroup
            harness.RunTicks(0);
            var em = harness.EntityManager;
            var nonCore = 0;
            var owned = 0;
            var coreNull = 0;
            using var ownQuery = em.CreateEntityQuery(ComponentType.ReadOnly<ProvinceOwnership>());
            using var ownerships = ownQuery.ToComponentDataArray<ProvinceOwnership>(Allocator.Temp);
            for (var i = 0; i < ownerships.Length; i++)
            {
                var o = ownerships[i];
                if (o.Owner == Entity.Null)
                    continue;
                owned++;
                if (o.Core == Entity.Null)
                    coreNull++;
                if (o.Owner != o.Core)
                    nonCore++;
            }

            sb.AppendLine("=== SANITY t0 (Owner vs Core) ===");
            sb.AppendLine(
                $"owned={owned} nonCore={nonCore} coreNull={coreNull}");
            if (nonCore == 0 && coreNull == 0)
                sb.AppendLine("OK t0: aucune province Owner != Core (Core peuplé, frein inerte au départ).");
            else
                sb.AppendLine(
                    "BUG t0: des provinces seraient pénalisées dès le départ — " +
                    "ne pas traiter comme un réglage.");
            sb.AppendLine();
        }

        static void AppendTickLine(StringBuilder sb, int tick, Snap snap)
        {
            var concluded = snap.Victories + snap.WhitePeaces;
            var ratio = concluded > 0 ? (float)snap.Victories / concluded : 0f;
            sb.AppendLine(
                $"tick{tick}: countriesWithLand={snap.CountriesWithLand} " +
                $"maxProvinces={snap.MaxProvincesOneCountry} " +
                $"nonCore={snap.NonCoreProvinces}/{snap.TotalProvincesOwned} " +
                $"victories={snap.Victories} whitePeaces={snap.WhitePeaces} " +
                $"ratioV={(ratio * 100f).ToString("F1", CultureInfo.InvariantCulture)}% " +
                $"annexed={snap.Annexed} stuck={snap.Stuck} " +
                $"totalDebt={Fmt1(snap.TotalDebt)} bankrupt={snap.Bankrupt} " +
                $"worldArmyStr={Fmt0(snap.WorldArmyStr)} " +
                $"zombie={Fmt0(snap.ZombieArmyStr)} " +
                $"needsSatAvg={Fmt3(snap.NeedsSatAvg)} population={snap.Population}");
        }

        static void AppendCalibrationCurve(
            StringBuilder sb, Config[] configs, Snap[] t800, Snap[] t1000)
        {
            sb.AppendLine("=== COURBE DE CALIBRATION (t1000 + t800 dip, seed 42195) ===");
            sb.AppendLine(
                "facteur | countries | maxProv | nonCore | ratioV@800 | annexed@800 | " +
                "debt | bankrupt | army | sat | pop");

            var distinctCountries = true;
            var distinctMaxProv = true;
            int? prevCountries = null;
            int? prevMaxProv = null;
            var monotoneCountries = true; // plus le frein est fort (facteur↓), plus countries↑
            var monotoneMaxProv = true;   // plus le frein est fort, plus maxProv↓
            var minCountries = int.MaxValue;
            var maxCountries = int.MinValue;
            var minMaxProv = int.MaxValue;
            var maxMaxProv = int.MinValue;

            for (var i = 0; i < configs.Length; i++)
            {
                var s = t1000[i];
                var s800 = t800[i];
                var concluded = s800.Victories + s800.WhitePeaces;
                var ratio = concluded > 0 ? (float)s800.Victories / concluded : 0f;
                sb.AppendLine(
                    $"  {Fmt(configs[i].NonCoreYieldFactor)} | {s.CountriesWithLand} | " +
                    $"{s.MaxProvincesOneCountry} | {s.NonCoreProvinces}/{s.TotalProvincesOwned} | " +
                    $"{(ratio * 100f).ToString("F1", CultureInfo.InvariantCulture)}% | " +
                    $"{s800.Annexed} | {Fmt1(s.TotalDebt)} | {s.Bankrupt} | " +
                    $"{Fmt0(s.WorldArmyStr)} | {Fmt3(s.NeedsSatAvg)} | {s.Population}");

                if (s.CountriesWithLand < minCountries) minCountries = s.CountriesWithLand;
                if (s.CountriesWithLand > maxCountries) maxCountries = s.CountriesWithLand;
                if (s.MaxProvincesOneCountry < minMaxProv) minMaxProv = s.MaxProvincesOneCountry;
                if (s.MaxProvincesOneCountry > maxMaxProv) maxMaxProv = s.MaxProvincesOneCountry;

                if (prevCountries.HasValue && s.CountriesWithLand < prevCountries.Value)
                    monotoneCountries = false;
                if (prevMaxProv.HasValue && s.MaxProvincesOneCountry > prevMaxProv.Value)
                    monotoneMaxProv = false;

                prevCountries = s.CountriesWithLand;
                prevMaxProv = s.MaxProvincesOneCountry;
            }

            distinctCountries = maxCountries > minCountries;
            distinctMaxProv = maxMaxProv > minMaxProv;

            sb.AppendLine(
                $"INFO span t1000: countries [{minCountries}..{maxCountries}] " +
                $"maxProv [{minMaxProv}..{maxMaxProv}] monoCountries↑={monotoneCountries} " +
                $"monoMaxProv↓={monotoneMaxProv}.");

            // Sur le plateau utile (facteurs ≥ 0.3) : maxProv 14→8 est le signal clair.
            if (distinctMaxProv && maxMaxProv - minMaxProv >= 4)
                sb.AppendLine(
                    "OK levier DOSE sur maxProvinces (amplitude ≥4 entre ancrage et frein fort).");
            else if (distinctMaxProv)
                sb.AppendLine("PARTIEL levier maxProvinces: distinct mais amplitude faible.");
            else
                sb.AppendLine("ALERT levier DÉCORATIF sur maxProvinces.");

            if (distinctCountries && monotoneCountries)
                sb.AppendLine(
                    "OK levier DOSE sur countriesWithLand (facteur↓ → consolidation↓).");
            else if (distinctCountries)
                sb.AppendLine(
                    "WARN countriesWithLand distincts mais NON monotones " +
                    "(path-dépendance ; plateau ~11-13, cible 14-16 hors portée de ce seul levier).");
            else
                sb.AppendLine("ALERT countriesWithLand inertes sur toute la courbe.");

            sb.AppendLine();
        }

        static void AppendRetainedDetail(
            StringBuilder sb, Config[] configs, Snap[] t800, Snap[] t1000)
        {
            var retainedIdx = -1;
            for (var i = 0; i < configs.Length; i++)
            {
                if (configs[i].IsRetainedCandidate)
                    retainedIdx = i;
            }

            sb.AppendLine("=== DÉTAIL RETENU ===");
            if (retainedIdx < 0)
            {
                sb.AppendLine("ALERT: aucun candidat retenu (DefaultNonCoreYieldFactor hors liste).");
                sb.AppendLine();
                return;
            }

            var f = configs[retainedIdx].NonCoreYieldFactor;
            var ret800 = t800[retainedIdx];
            var ret = t1000[retainedIdx];
            var concluded = ret800.Victories + ret800.WhitePeaces;
            var ratio = concluded > 0 ? (float)ret800.Victories / concluded : 0f;

            sb.AppendLine($"NonCoreYieldFactor={Fmt(f)}");
            sb.AppendLine(
                $"t800: V={ret800.Victories} WP={ret800.WhitePeaces} " +
                $"ratioV={(ratio * 100f).ToString("F1", CultureInfo.InvariantCulture)}% " +
                $"annexed={ret800.Annexed} countries={ret800.CountriesWithLand} " +
                $"maxProv={ret800.MaxProvincesOneCountry} " +
                $"nonCore={ret800.NonCoreProvinces}/{ret800.TotalProvincesOwned}");
            sb.AppendLine(
                $"t1000: countries={ret.CountriesWithLand} " +
                $"maxProv={ret.MaxProvincesOneCountry} " +
                $"nonCore={ret.NonCoreProvinces}/{ret.TotalProvincesOwned} " +
                $"debt={Fmt1(ret.TotalDebt)} bankrupt={ret.Bankrupt} " +
                $"army={Fmt0(ret.WorldArmyStr)} zombie={Fmt0(ret.ZombieArmyStr)} " +
                $"sat={Fmt3(ret.NeedsSatAvg)} pop={ret.Population}");
            sb.AppendLine();
        }

        static void AppendVerdict(
            StringBuilder sb, Config[] configs, Snap[] t800, Snap[] t1000)
        {
            sb.AppendLine("=== VERDICT dip_006 (seed 42195) ===");

            var anchorIdx = -1;
            var retainedIdx = -1;
            for (var i = 0; i < configs.Length; i++)
            {
                if (configs[i].IsAnchor)
                    anchorIdx = i;
                if (configs[i].IsRetainedCandidate)
                    retainedIdx = i;
            }

            if (anchorIdx >= 0)
            {
                var a = t1000[anchorIdx];
                sb.AppendLine(
                    $"ANCRAGE facteur=1.0 t1000: countries={a.CountriesWithLand} " +
                    $"maxProv={a.MaxProvincesOneCountry} nonCore={a.NonCoreProvinces} " +
                    $"debt={Fmt1(a.TotalDebt)} bankrupt={a.Bankrupt} " +
                    $"army={Fmt0(a.WorldArmyStr)} sat={Fmt3(a.NeedsSatAvg)} pop={a.Population}");
                sb.AppendLine(
                    "INFO ancrage: facteur=1.0 doit être bit-identique à la prod sans frein " +
                    "(aucune province pénalisée tant que Owner==Core, et même rendement sinon).");
            }

            if (retainedIdx < 0)
            {
                sb.AppendLine("ALERT: pas de retenu.");
                return;
            }

            var ret = t1000[retainedIdx];
            var ret800 = t800[retainedIdx];
            var concluded = ret800.Victories + ret800.WhitePeaces;
            var ratio = concluded > 0 ? (float)ret800.Victories / concluded : 0f;

            var countriesOk = ret.CountriesWithLand >= 13 && ret.CountriesWithLand <= 16;
            var countriesTarget = ret.CountriesWithLand >= 14 && ret.CountriesWithLand <= 16;
            var maxProvOk = ret.MaxProvincesOneCountry < 13;
            var dipOk = ratio >= 0.70f && ratio <= 0.82f &&
                        ret800.Annexed >= 28 && ret800.Annexed <= 40;
            var debtOk = ret.TotalDebt >= 700f && ret.TotalDebt <= 1100f &&
                         ret.Bankrupt >= 2 && ret.Bankrupt <= 5;
            var armyOk = ret.WorldArmyStr >= 36000f && ret.WorldArmyStr <= 44000f;
            var zombieOk = ret.ZombieArmyStr < 1f;
            var acquisOk =
                System.Math.Abs(ret.NeedsSatAvg - 0.698f) < 0.02f &&
                ret.Population >= 140000 && ret.Population <= 145000;

            if (countriesTarget)
                sb.AppendLine(
                    $"OK countriesWithLand t1000={ret.CountriesWithLand} (cible ~14-16).");
            else if (countriesOk)
                sb.AppendLine(
                    $"PARTIEL countriesWithLand t1000={ret.CountriesWithLand} " +
                    "(cible ~14-16 non atteinte par ce seul levier ; max 13 observé sur la courbe).");
            else
                sb.AppendLine(
                    $"ALERT countriesWithLand t1000={ret.CountriesWithLand} (cible ~14-16).");
            sb.AppendLine(
                maxProvOk
                    ? $"OK maxProvinces={ret.MaxProvincesOneCountry} nettement sous 13."
                    : $"ALERT maxProvinces={ret.MaxProvincesOneCountry} (cible nettement sous 13).");
            sb.AppendLine(
                dipOk
                    ? $"OK dip_005 non défait: ratioV@800={(ratio * 100f).ToString("F1", CultureInfo.InvariantCulture)}% " +
                      $"annexed={ret800.Annexed}."
                    : $"ALERT dip_005: ratioV@800={(ratio * 100f).ToString("F1", CultureInfo.InvariantCulture)}% " +
                      $"annexed={ret800.Annexed} (cible ratio~70-82%, annexed~28-35).");
            sb.AppendLine(
                debtOk
                    ? $"OK dette bornée: debt={Fmt1(ret.TotalDebt)} bankrupt={ret.Bankrupt}."
                    : $"ALERT dette/banqueroutes: debt={Fmt1(ret.TotalDebt)} bankrupt={ret.Bankrupt} " +
                      "(cible debt~750-1000, bankrupt~2-5) — reculer vers un facteur plus doux.");
            sb.AppendLine(
                armyOk
                    ? $"OK worldArmyStr={Fmt0(ret.WorldArmyStr)} (bande eco_034 ~38-40k)."
                    : $"ALERT worldArmyStr={Fmt0(ret.WorldArmyStr)} hors bande ~36-44k.");
            sb.AppendLine(
                zombieOk
                    ? $"OK zombie=0: {Fmt0(ret.ZombieArmyStr)}."
                    : $"ALERT zombie: {Fmt0(ret.ZombieArmyStr)}.");
            sb.AppendLine(
                acquisOk
                    ? $"OK acquis needs/pop: sat={Fmt3(ret.NeedsSatAvg)} pop={ret.Population}."
                    : $"ALERT acquis: sat={Fmt3(ret.NeedsSatAvg)} pop={ret.Population}.");
            sb.AppendLine(
                $"NonCoreYieldFactor retenu={Fmt(TaxSystem.DefaultNonCoreYieldFactor)}. " +
                "Dette différée: intégration temporelle (province conquise → Core après N ticks) " +
                "NON implémentée — sans elle le malus est perpétuel.");
        }

        static Snap CaptureSnap(EntityManager em, int currentTick)
        {
            var snap = new Snap();
            var durationStuckCheck = currentTick;

            using var warQuery = em.CreateEntityQuery(ComponentType.ReadOnly<WarData>());
            using var wars = warQuery.ToComponentDataArray<WarData>(Allocator.Temp);
            for (var i = 0; i < wars.Length; i++)
            {
                var war = wars[i];
                if (war.IsActive)
                {
                    if (durationStuckCheck - war.StartTick > 150)
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
            var provinceCounts = new Dictionary<Entity, int>();
            using var ownQuery = em.CreateEntityQuery(ComponentType.ReadOnly<ProvinceOwnership>());
            using var ownerships = ownQuery.ToComponentDataArray<ProvinceOwnership>(Allocator.Temp);
            for (var i = 0; i < ownerships.Length; i++)
            {
                var o = ownerships[i];
                if (o.Owner == Entity.Null)
                    continue;

                snap.TotalProvincesOwned++;
                owners.Add(o.Owner);
                if (!provinceCounts.ContainsKey(o.Owner))
                    provinceCounts[o.Owner] = 0;
                provinceCounts[o.Owner]++;

                if (o.Core != Entity.Null && o.Owner != o.Core)
                {
                    snap.Annexed++;
                    snap.NonCoreProvinces++;
                }
            }

            snap.CountriesWithLand = owners.Count;
            foreach (var kv in provinceCounts)
            {
                if (kv.Value > snap.MaxProvincesOneCountry)
                    snap.MaxProvincesOneCountry = kv.Value;
            }

            var provinceByOwner = new NativeHashMap<Entity, int>(64, Allocator.Temp);
            foreach (var kv in provinceCounts)
                provinceByOwner.TryAdd(kv.Key, kv.Value);

            using var countryQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<CountryData>(),
                ComponentType.ReadOnly<TreasuryData>());
            using var countries = countryQuery.ToEntityArray(Allocator.Temp);
            using var treasuries = countryQuery.ToComponentDataArray<TreasuryData>(Allocator.Temp);

            var armyByCountry = new NativeHashMap<Entity, float>(64, Allocator.Temp);
            using var armyQuery = em.CreateEntityQuery(ComponentType.ReadOnly<ArmyData>());
            using var armies = armyQuery.ToComponentDataArray<ArmyData>(Allocator.Temp);
            for (var i = 0; i < armies.Length; i++)
            {
                snap.WorldArmyStr += armies[i].Strength;
                var country = armies[i].Country;
                if (country == Entity.Null)
                    continue;
                armyByCountry.TryGetValue(country, out var cur);
                armyByCountry[country] = cur + armies[i].Strength;
            }

            for (var i = 0; i < countries.Length; i++)
            {
                snap.TotalDebt += treasuries[i].Debt;
                if (treasuries[i].BankruptcyTick > 0)
                    snap.Bankrupt++;

                provinceByOwner.TryGetValue(countries[i], out var prov);
                if (prov <= 0)
                {
                    armyByCountry.TryGetValue(countries[i], out var armyStr);
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

            provinceByOwner.Dispose();
            armyByCountry.Dispose();
            return snap;
        }

        static string Fmt(float v) => v.ToString("F2", CultureInfo.InvariantCulture);
        static string Fmt0(float v) => v.ToString("F0", CultureInfo.InvariantCulture);
        static string Fmt1(float v) => v.ToString("F1", CultureInfo.InvariantCulture);
        static string Fmt3(float v) => v.ToString("F3", CultureInfo.InvariantCulture);
    }
}
