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
    /// <summary>Point d'entrée batchmode : -executeMethod VictoriaGame.Tests.Dip007BatchRunner.Run</summary>
    public static class Dip007BatchRunner
    {
        public static void Run()
        {
            Dip007MeasurementTests.RunMeasurementsAndWriteLog();
            UnityEngine.Debug.Log("Dip007BatchRunner: DONE");
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
            #endif
        }
    }

    [TestFixture]
    public class Dip007MeasurementTests
    {
        const uint Seed = 42195u;
        static readonly int[] SnapshotTicks = { 200, 500, 800, 1000 };

        static readonly RumpStateMode[] ModesTried =
        {
            RumpStateMode.Disabled,
            RumpStateMode.Always,
            RumpStateMode.UnlessCapitalOccupied
        };

        struct Snap
        {
            public int Victories;
            public int WhitePeaces;
            public int Annexed;
            public int CountriesWithLand;
            public int MaxProvincesOneCountry;
            public int Stuck;
            public float TotalDebt;
            public int Bankrupt;
            public float WorldArmyStr;
            public float ZombieArmyStr;
            public float NeedsSatAvg;
            public int Population;
            public int RumpStatesCreated;
            public int SparedLeftOccupied;
            public int SmallCountries;
            public float SmallCountryArmyAvg;
            public int CapitalsResolved;
            public int CapitalsMissing;
        }

        [Test]
        public void Dip007_MeasureRumpStateModesAtKeyTicks() => RunMeasurementsAndWriteLog();

        public static void RunMeasurementsAndWriteLog()
        {
            var prevMode = PeaceSystem.Mode;
            var logPath = Path.Combine(
                UnityEngine.Application.dataPath, "..", "Logs", "dip_007_measurements.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine(
                    $"=== dip_007 seed={Seed} DefaultRumpStateMode={PeaceSystem.DefaultRumpStateMode} " +
                    $"NonCoreYieldFactor={Fmt(TaxSystem.NonCoreYieldFactor)} (dip_006) " +
                    $"OccupationScoreRate={Fmt(OccupationScoreSystem.OccupationScoreRate)} (dip_005) " +
                    $"ReinforceRateFactor={Fmt(ArmyDisbandmentSystem.ReinforceRateFactor)} (eco_034) ===");
                sb.AppendLine(
                    "État croupion: ConcludePeace épargne 1 province (capitale sinon min ProvinceId) " +
                    "+ libère occupation (Controller=Owner). ConcludeWhitePeace inchangé.");
                sb.AppendLine(
                    "tried: Disabled (ancrage dip_006), Always, UnlessCapitalOccupied.");
                sb.AppendLine(
                    "Colonnes: countriesWithLand maxProvinces V WP ratioV annexed stuck debt bankrupt " +
                    "army zombie sat pop rumps sparedOcc smallAvgArmy");
                sb.AppendLine();

                AppendT0CapitalSanity(sb);

                var t800ByMode = new Snap[ModesTried.Length];
                var t1000ByMode = new Snap[ModesTried.Length];

                for (var m = 0; m < ModesTried.Length; m++)
                {
                    var mode = ModesTried[m];
                    PeaceSystem.Mode = mode;
                    sb.AppendLine($"=== MODE {mode} ===");

                    for (var i = 0; i < SnapshotTicks.Length; i++)
                    {
                        var tick = SnapshotTicks[i];
                        PeaceSystem.RumpStatesCreated = 0;
                        PeaceSystem.SparedProvincesLeftOccupied = 0;

                        using var harness = new SimulationHarness(Seed);
                        harness.RunTicks(tick);
                        var snap = CaptureSnap(harness.EntityManager, tick);
                        snap.RumpStatesCreated = PeaceSystem.RumpStatesCreated;
                        snap.SparedLeftOccupied = PeaceSystem.SparedProvincesLeftOccupied;

                        if (tick == 800)
                            t800ByMode[m] = snap;
                        if (tick == 1000)
                            t1000ByMode[m] = snap;

                        AppendTickLine(sb, tick, snap);
                    }

                    sb.AppendLine();
                }

                AppendComparativeTable(sb, t800ByMode, t1000ByMode);
                AppendVerdict(sb, t800ByMode, t1000ByMode);

                File.WriteAllText(logPath, sb.ToString());
                UnityEngine.Debug.Log(sb.ToString());
            }
            finally
            {
                PeaceSystem.Mode = prevMode;
                PeaceSystem.RumpStatesCreated = 0;
                PeaceSystem.SparedProvincesLeftOccupied = 0;
            }
        }

        static void AppendT0CapitalSanity(StringBuilder sb)
        {
            PeaceSystem.Mode = RumpStateMode.Disabled;
            using var harness = new SimulationHarness(Seed);
            harness.RunTicks(0);
            var em = harness.EntityManager;

            var resolved = 0;
            var missing = 0;
            var notOwned = 0;

            using var countryQuery = em.CreateEntityQuery(ComponentType.ReadOnly<CountryData>());
            using var countries = countryQuery.ToEntityArray(Allocator.Temp);
            using var countryData = countryQuery.ToComponentDataArray<CountryData>(Allocator.Temp);

            var ownerByProvince = new Dictionary<int, Entity>();
            using var ownQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<ProvinceData>(),
                ComponentType.ReadOnly<ProvinceOwnership>());
            using var provEntities = ownQuery.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < provEntities.Length; i++)
            {
                var pid = em.GetComponentData<ProvinceData>(provEntities[i]).ProvinceId;
                var own = em.GetComponentData<ProvinceOwnership>(provEntities[i]).Owner;
                ownerByProvince[pid] = own;
            }

            for (var i = 0; i < countries.Length; i++)
            {
                var cap = countryData[i].CapitalProvinceId;
                if (cap <= 0)
                {
                    missing++;
                    continue;
                }

                if (!ownerByProvince.TryGetValue(cap, out var owner) || owner != countries[i])
                {
                    notOwned++;
                    continue;
                }

                resolved++;
            }

            sb.AppendLine("=== SANITY t0 (CapitalProvinceId) ===");
            sb.AppendLine(
                $"countries={countries.Length} capitalsResolved={resolved} " +
                $"missing={missing} notOwnedAtT0={notOwned}");
            if (resolved == countries.Length && missing == 0 && notOwned == 0)
                sb.AppendLine("OK t0: 20/20 capitales résolues et possédées.");
            else
                sb.AppendLine("BUG t0: capitale manquante ou non possédée — bug de données.");
            sb.AppendLine();
        }

        static void AppendTickLine(StringBuilder sb, int tick, Snap snap)
        {
            var concluded = snap.Victories + snap.WhitePeaces;
            var ratio = concluded > 0 ? (float)snap.Victories / concluded : 0f;
            sb.AppendLine(
                $"tick{tick}: countriesWithLand={snap.CountriesWithLand} " +
                $"maxProvinces={snap.MaxProvincesOneCountry} " +
                $"victories={snap.Victories} whitePeaces={snap.WhitePeaces} " +
                $"ratioV={(ratio * 100f).ToString("F1", CultureInfo.InvariantCulture)}% " +
                $"annexed={snap.Annexed} stuck={snap.Stuck} " +
                $"totalDebt={Fmt1(snap.TotalDebt)} bankrupt={snap.Bankrupt} " +
                $"worldArmyStr={Fmt0(snap.WorldArmyStr)} " +
                $"zombie={Fmt0(snap.ZombieArmyStr)} " +
                $"needsSatAvg={Fmt3(snap.NeedsSatAvg)} population={snap.Population} " +
                $"rumps={snap.RumpStatesCreated} sparedOcc={snap.SparedLeftOccupied} " +
                $"smallCountries={snap.SmallCountries} " +
                $"smallArmyAvg={Fmt0(snap.SmallCountryArmyAvg)}");
        }

        static void AppendComparativeTable(StringBuilder sb, Snap[] t800, Snap[] t1000)
        {
            sb.AppendLine("=== TABLEAU COMPARATIF (seed 42195) ===");
            sb.AppendLine(
                "mode | countries@1000 | maxProv | rumps | ratioV@800 | annexed@800 | " +
                "debt | bankrupt | army | smallArmyAvg | sparedOcc");

            for (var i = 0; i < ModesTried.Length; i++)
            {
                var s = t1000[i];
                var s800 = t800[i];
                var concluded = s800.Victories + s800.WhitePeaces;
                var ratio = concluded > 0 ? (float)s800.Victories / concluded : 0f;
                sb.AppendLine(
                    $"  {ModesTried[i]} | {s.CountriesWithLand} | {s.MaxProvincesOneCountry} | " +
                    $"{s.RumpStatesCreated} | " +
                    $"{(ratio * 100f).ToString("F1", CultureInfo.InvariantCulture)}% | " +
                    $"{s800.Annexed} | {Fmt1(s.TotalDebt)} | {s.Bankrupt} | " +
                    $"{Fmt0(s.WorldArmyStr)} | {Fmt0(s.SmallCountryArmyAvg)} | {s.SparedLeftOccupied}");
            }

            sb.AppendLine();
        }

        static void AppendVerdict(StringBuilder sb, Snap[] t800, Snap[] t1000)
        {
            sb.AppendLine("=== VERDICT dip_007 (seed 42195) ===");

            var disabledIdx = IndexOf(RumpStateMode.Disabled);
            var alwaysIdx = IndexOf(RumpStateMode.Always);
            var unlessIdx = IndexOf(RumpStateMode.UnlessCapitalOccupied);

            if (disabledIdx >= 0)
            {
                var d = t1000[disabledIdx];
                sb.AppendLine(
                    $"ANCRAGE Disabled t1000: countries={d.CountriesWithLand} maxProv={d.MaxProvincesOneCountry} " +
                    $"debt={Fmt1(d.TotalDebt)} bankrupt={d.Bankrupt} army={Fmt0(d.WorldArmyStr)} " +
                    $"sat={Fmt3(d.NeedsSatAvg)} pop={d.Population} rumps={d.RumpStatesCreated}");
                // dip_006 retenu: countries=13 maxProv=8 debt~750.9 bankrupt=4 army=36468 sat=0.698 pop=142551
                var anchorOk =
                    d.CountriesWithLand == 13 &&
                    d.MaxProvincesOneCountry == 8 &&
                    d.Bankrupt == 4 &&
                    d.Population == 142551 &&
                    System.Math.Abs(d.NeedsSatAvg - 0.698f) < 0.002f &&
                    System.Math.Abs(d.WorldArmyStr - 36468f) < 2f &&
                    System.Math.Abs(d.TotalDebt - 750.9f) < 2f &&
                    d.RumpStatesCreated == 0;
                sb.AppendLine(
                    anchorOk
                        ? "OK Disabled = dip_006 exact (ancrage non-régression)."
                        : "ALERT Disabled ≠ dip_006 — régression sur l'ancrage.");
            }

            // Choisir le mode le plus proche de 14-16 sans défaire dip_005.
            var bestIdx = -1;
            var bestDist = int.MaxValue;
            for (var i = 0; i < ModesTried.Length; i++)
            {
                if (ModesTried[i] == RumpStateMode.Disabled)
                    continue;

                var c = t1000[i].CountriesWithLand;
                var dist = DistanceToTargetBand(c, 14, 16);
                var s800 = t800[i];
                var concluded = s800.Victories + s800.WhitePeaces;
                var ratio = concluded > 0 ? (float)s800.Victories / concluded : 0f;
                var dipOk = ratio >= 0.70f && ratio <= 0.82f && s800.Stuck == 0;
                var debtOk = t1000[i].TotalDebt >= 700f && t1000[i].TotalDebt <= 1100f &&
                             t1000[i].Bankrupt >= 2 && t1000[i].Bankrupt <= 5;
                var armyOk = t1000[i].WorldArmyStr >= 36000f && t1000[i].WorldArmyStr <= 44000f;
                var spareOk = t1000[i].SparedLeftOccupied == 0;

                sb.AppendLine(
                    $"CANDIDAT {ModesTried[i]}: countries={c} distBand={dist} " +
                    $"dipOk={dipOk} debtOk={debtOk} armyOk={armyOk} spareOk={spareOk} " +
                    $"rumps={t1000[i].RumpStatesCreated} smallArmyAvg={Fmt0(t1000[i].SmallCountryArmyAvg)}");

                if (!dipOk || !debtOk || !armyOk || !spareOk)
                    continue;

                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestIdx = i;
                }
            }

            if (alwaysIdx >= 0 && unlessIdx >= 0)
            {
                sb.AppendLine(
                    $"HYPOTHÈSE: Always→countries={t1000[alwaysIdx].CountriesWithLand} " +
                    $"(fige à 20?) ; Unless→{t1000[unlessIdx].CountriesWithLand} (intermédiaire?).");
            }

            if (bestIdx < 0)
            {
                sb.AppendLine("ALERT: aucun mode actif ne satisfait dip_005 + dette + armée.");
                // Fallback: plus proche de la bande même hors contraintes dures (diagnostic).
                bestIdx = unlessIdx >= 0 ? unlessIdx : alwaysIdx;
            }

            if (bestIdx >= 0)
            {
                var ret = t1000[bestIdx];
                var ret800 = t800[bestIdx];
                var concluded = ret800.Victories + ret800.WhitePeaces;
                var ratio = concluded > 0 ? (float)ret800.Victories / concluded : 0f;
                sb.AppendLine(
                    $"RETENU: {ModesTried[bestIdx]} — countries@1000={ret.CountriesWithLand} " +
                    $"(cible ~14-16) maxProv={ret.MaxProvincesOneCountry} rumps={ret.RumpStatesCreated} " +
                    $"ratioV@800={(ratio * 100f).ToString("F1", CultureInfo.InvariantCulture)}% " +
                    $"annexed={ret800.Annexed} stuck={ret800.Stuck} " +
                    $"debt={Fmt1(ret.TotalDebt)} bankrupt={ret.Bankrupt} " +
                    $"army={Fmt0(ret.WorldArmyStr)} zombie={Fmt0(ret.ZombieArmyStr)} " +
                    $"sat={Fmt3(ret.NeedsSatAvg)} pop={ret.Population} " +
                    $"smallArmyAvg={Fmt0(ret.SmallCountryArmyAvg)} sparedOcc={ret.SparedLeftOccupied}");
                sb.AppendLine(
                    $"DefaultRumpStateMode production attendu={ModesTried[bestIdx]} " +
                    $"(vérifier PeaceSystem.DefaultRumpStateMode).");
            }
        }

        static int DistanceToTargetBand(int value, int lo, int hi)
        {
            if (value >= lo && value <= hi)
                return 0;
            if (value < lo)
                return lo - value;
            return value - hi;
        }

        static int IndexOf(RumpStateMode mode)
        {
            for (var i = 0; i < ModesTried.Length; i++)
            {
                if (ModesTried[i] == mode)
                    return i;
            }

            return -1;
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

                owners.Add(o.Owner);
                if (!provinceCounts.ContainsKey(o.Owner))
                    provinceCounts[o.Owner] = 0;
                provinceCounts[o.Owner]++;

                if (o.Core != Entity.Null && o.Owner != o.Core)
                    snap.Annexed++;
            }

            snap.CountriesWithLand = owners.Count;
            foreach (var kv in provinceCounts)
            {
                if (kv.Value > snap.MaxProvincesOneCountry)
                    snap.MaxProvincesOneCountry = kv.Value;
            }

            var armyByCountry = new Dictionary<Entity, float>();
            using var armyQuery = em.CreateEntityQuery(ComponentType.ReadOnly<ArmyData>());
            using var armies = armyQuery.ToComponentDataArray<ArmyData>(Allocator.Temp);
            for (var i = 0; i < armies.Length; i++)
            {
                snap.WorldArmyStr += armies[i].Strength;
                var country = armies[i].Country;
                if (country == Entity.Null)
                    continue;
                if (!armyByCountry.ContainsKey(country))
                    armyByCountry[country] = 0f;
                armyByCountry[country] += armies[i].Strength;
            }

            using var countryQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<CountryData>(),
                ComponentType.ReadOnly<TreasuryData>());
            using var countryEntities = countryQuery.ToEntityArray(Allocator.Temp);
            using var treasuries = countryQuery.ToComponentDataArray<TreasuryData>(Allocator.Temp);
            using var countryDatas = countryQuery.ToComponentDataArray<CountryData>(Allocator.Temp);

            double smallArmySum = 0.0;
            for (var i = 0; i < countryEntities.Length; i++)
            {
                snap.TotalDebt += treasuries[i].Debt;
                if (treasuries[i].BankruptcyTick > 0)
                    snap.Bankrupt++;

                provinceCounts.TryGetValue(countryEntities[i], out var prov);
                armyByCountry.TryGetValue(countryEntities[i], out var armyStr);
                if (prov <= 0)
                    snap.ZombieArmyStr += armyStr;
                else if (prov <= 2)
                {
                    snap.SmallCountries++;
                    smallArmySum += armyStr;
                }

                if (countryDatas[i].CapitalProvinceId > 0)
                    snap.CapitalsResolved++;
                else
                    snap.CapitalsMissing++;
            }

            snap.SmallCountryArmyAvg = snap.SmallCountries > 0
                ? (float)(smallArmySum / snap.SmallCountries)
                : 0f;

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
            return snap;
        }

        static string Fmt(float v) => v.ToString("F2", CultureInfo.InvariantCulture);
        static string Fmt0(float v) => v.ToString("F0", CultureInfo.InvariantCulture);
        static string Fmt1(float v) => v.ToString("F1", CultureInfo.InvariantCulture);
        static string Fmt3(float v) => v.ToString("F3", CultureInfo.InvariantCulture);
    }
}
