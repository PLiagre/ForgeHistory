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
    /// <summary>Point d'entrée batchmode : -executeMethod VictoriaGame.Tests.Dip008BatchRunner.Run</summary>
    public static class Dip008BatchRunner
    {
        public static void Run()
        {
            Dip008MeasurementTests.RunMeasurementsAndWriteLog();
            UnityEngine.Debug.Log("Dip008BatchRunner: DONE");
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
            #endif
        }
    }

    [TestFixture]
    public class Dip008MeasurementTests
    {
        const uint Seed = 42195u;
        static readonly int[] SnapshotTicks = { 200, 500, 800, 1000 };

        /// <summary>
        /// Courbe : Disabled (ancrage dip_007) + 100 / 200 / 400 autour guerre~108 / trêve 120.
        /// </summary>
        static readonly int[] TicksTried =
        {
            ProvinceIntegration.DisabledIntegrationTicks,
            100,
            200,
            400
        };

        struct Snap
        {
            public int Victories;
            public int WhitePeaces;
            public int WarsDeclared;
            public int ReconquestWars;
            public int ConquestWars;
            public int Annexed;
            public int NonCoreProvinces;
            public int TotalProvincesOwned;
            public int CountriesWithLand;
            public int MaxProvincesOneCountry;
            public int Stuck;
            public float TotalDebt;
            public int Bankrupt;
            public float WorldArmyStr;
            public float ZombieArmyStr;
            public float NeedsSatAvg;
            public int Population;
            public int ProvincesIntegrated;
            public int OccupiedDeferred;
            /// <summary>Core==Owner + Controller!=Owner + OwnerChangedTick&gt;0 (réoccupation après intégration, informatif).</summary>
            public int IntegratedThenReoccupied;
        }

        [Test]
        public void Dip008_MeasureIntegrationTicksAtKeyTicks() => RunMeasurementsAndWriteLog();

        public static void RunMeasurementsAndWriteLog()
        {
            var prevTicks = ProvinceIntegration.IntegrationTicks;
            var logPath = Path.Combine(
                UnityEngine.Application.dataPath, "..", "Logs", "dip_008_measurements.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine(
                    $"=== dip_008 seed={Seed} DefaultIntegrationTicks=" +
                    $"{ProvinceIntegration.DefaultIntegrationTicks} " +
                    $"RumpMode={PeaceSystem.DefaultRumpStateMode} (dip_007) " +
                    $"NonCoreYieldFactor={Fmt(TaxSystem.NonCoreYieldFactor)} (dip_006) " +
                    $"OccupationScoreRate={Fmt(OccupationScoreSystem.OccupationScoreRate)} (dip_005) ===");
                sb.AppendLine(
                    "Intégration: Owner!=Core, Controller==Owner, (tick−OwnerChangedTick)>=INTEGRATION_TICKS " +
                    "→ Core=Owner. Occupé (Controller!=Owner) : différé, pas d'intégration.");
                sb.AppendLine(
                    "tried: Disabled=0 (ancrage dip_007), 100, 200, 400.");
                sb.AppendLine(
                    "Colonnes: countries maxProv nonCore V WP wars reconq ratioV annexed stuck " +
                    "debt bankrupt army zombie sat pop integrated occDeferred reoccAfterInteg");
                sb.AppendLine();

                AppendT0Sanity(sb);

                var t800ByCfg = new Snap[TicksTried.Length];
                var t1000ByCfg = new Snap[TicksTried.Length];

                for (var c = 0; c < TicksTried.Length; c++)
                {
                    var ticks = TicksTried[c];
                    ProvinceIntegration.IntegrationTicks = ticks;
                    sb.AppendLine($"=== INTEGRATION_TICKS={Label(ticks)} ===");

                    for (var i = 0; i < SnapshotTicks.Length; i++)
                    {
                        var tick = SnapshotTicks[i];
                        ProvinceIntegration.ProvincesIntegrated = 0;
                        ProvinceIntegration.OccupiedIntegrationDeferred = 0;

                        using var harness = new SimulationHarness(Seed);
                        harness.RunTicks(tick);
                        var snap = CaptureSnap(harness.EntityManager, tick);
                        snap.ProvincesIntegrated = ProvinceIntegration.ProvincesIntegrated;
                        snap.OccupiedDeferred = ProvinceIntegration.OccupiedIntegrationDeferred;

                        if (tick == 800)
                            t800ByCfg[c] = snap;
                        if (tick == 1000)
                            t1000ByCfg[c] = snap;

                        AppendTickLine(sb, tick, snap);
                    }

                    sb.AppendLine();
                }

                AppendCalibrationCurve(sb, t800ByCfg, t1000ByCfg);
                AppendVerdict(sb, t800ByCfg, t1000ByCfg);

                File.WriteAllText(logPath, sb.ToString());
                UnityEngine.Debug.Log(sb.ToString());
            }
            finally
            {
                ProvinceIntegration.IntegrationTicks = prevTicks;
                ProvinceIntegration.ProvincesIntegrated = 0;
                ProvinceIntegration.OccupiedIntegrationDeferred = 0;
            }
        }

        static string Label(int ticks) =>
            ticks <= ProvinceIntegration.DisabledIntegrationTicks
                ? "Disabled"
                : ticks.ToString(CultureInfo.InvariantCulture);

        static void AppendT0Sanity(StringBuilder sb)
        {
            ProvinceIntegration.IntegrationTicks =
                ProvinceIntegration.DisabledIntegrationTicks;
            using var harness = new SimulationHarness(Seed);
            harness.RunTicks(0);
            var em = harness.EntityManager;

            var nonCore = 0;
            var owned = 0;
            var badTick = 0;
            using var ownQuery = em.CreateEntityQuery(ComponentType.ReadOnly<ProvinceOwnership>());
            using var ownerships = ownQuery.ToComponentDataArray<ProvinceOwnership>(Allocator.Temp);
            for (var i = 0; i < ownerships.Length; i++)
            {
                var o = ownerships[i];
                if (o.Owner == Entity.Null)
                    continue;
                owned++;
                if (o.Owner != o.Core)
                    nonCore++;
                if (o.OwnerChangedTick != 0)
                    badTick++;
            }

            sb.AppendLine("=== SANITY t0 (OwnerChangedTick + Core) ===");
            sb.AppendLine(
                $"owned={owned} nonCore={nonCore} ownerChangedTickNonZero={badTick}");
            if (nonCore == 0 && badTick == 0)
                sb.AppendLine("OK t0: Core=Owner partout, OwnerChangedTick=0.");
            else
                sb.AppendLine("BUG t0: état d'init incohérent.");
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
                $"wars={snap.WarsDeclared} reconquest={snap.ReconquestWars} " +
                $"conquest={snap.ConquestWars} " +
                $"ratioV={(ratio * 100f).ToString("F1", CultureInfo.InvariantCulture)}% " +
                $"annexed={snap.Annexed} stuck={snap.Stuck} " +
                $"totalDebt={Fmt1(snap.TotalDebt)} bankrupt={snap.Bankrupt} " +
                $"worldArmyStr={Fmt0(snap.WorldArmyStr)} " +
                $"zombie={Fmt0(snap.ZombieArmyStr)} " +
                $"needsSatAvg={Fmt3(snap.NeedsSatAvg)} population={snap.Population} " +
                $"integrated={snap.ProvincesIntegrated} " +
                $"occDeferred={snap.OccupiedDeferred} " +
                $"reoccAfterInteg={snap.IntegratedThenReoccupied}");
        }

        static void AppendCalibrationCurve(StringBuilder sb, Snap[] t800, Snap[] t1000)
        {
            sb.AppendLine("=== COURBE DE CALIBRATION (seed 42195) ===");
            sb.AppendLine(
                "INTEGRATION_TICKS | nonCore@1000 | countries | maxProv | wars | reconq | " +
                "ratioV@800 | debt | bankrupt | army | sat | pop | integrated | occDeferred");

            for (var i = 0; i < TicksTried.Length; i++)
            {
                var s = t1000[i];
                var s800 = t800[i];
                var concluded = s800.Victories + s800.WhitePeaces;
                var ratio = concluded > 0 ? (float)s800.Victories / concluded : 0f;
                sb.AppendLine(
                    $"  {Label(TicksTried[i])} | {s.NonCoreProvinces}/{s.TotalProvincesOwned} | " +
                    $"{s.CountriesWithLand} | {s.MaxProvincesOneCountry} | " +
                    $"{s.WarsDeclared} | {s.ReconquestWars} | " +
                    $"{(ratio * 100f).ToString("F1", CultureInfo.InvariantCulture)}% | " +
                    $"{Fmt1(s.TotalDebt)} | {s.Bankrupt} | {Fmt0(s.WorldArmyStr)} | " +
                    $"{Fmt3(s.NeedsSatAvg)} | {s.Population} | " +
                    $"{s.ProvincesIntegrated} | {s.OccupiedDeferred}");
            }

            sb.AppendLine();
        }

        static void AppendVerdict(StringBuilder sb, Snap[] t800, Snap[] t1000)
        {
            sb.AppendLine("=== VERDICT dip_008 (seed 42195) ===");

            var disabledIdx = IndexOf(ProvinceIntegration.DisabledIntegrationTicks);
            if (disabledIdx >= 0)
            {
                var d = t1000[disabledIdx];
                sb.AppendLine(
                    $"ANCRAGE Disabled t1000: countries={d.CountriesWithLand} " +
                    $"maxProv={d.MaxProvincesOneCountry} nonCore={d.NonCoreProvinces}/" +
                    $"{d.TotalProvincesOwned} debt={Fmt1(d.TotalDebt)} bankrupt={d.Bankrupt} " +
                    $"army={Fmt0(d.WorldArmyStr)} sat={Fmt3(d.NeedsSatAvg)} pop={d.Population} " +
                    $"wars={d.WarsDeclared} reconq={d.ReconquestWars} integrated={d.ProvincesIntegrated}");
                // dip_007 retenu: countries=14 maxProv=10 debt~750.9 bankrupt=4 army=36410 sat=0.698 pop=142551
                var anchorOk =
                    d.CountriesWithLand == 14 &&
                    d.MaxProvincesOneCountry == 10 &&
                    d.Bankrupt == 4 &&
                    d.Population == 142551 &&
                    d.ProvincesIntegrated == 0 &&
                    System.Math.Abs(d.NeedsSatAvg - 0.698f) < 0.002f &&
                    System.Math.Abs(d.WorldArmyStr - 36410f) < 2f &&
                    System.Math.Abs(d.TotalDebt - 750.9f) < 2f;
                sb.AppendLine(
                    anchorOk
                        ? "OK Disabled = dip_007 exact (ancrage non-régression)."
                        : "ALERT Disabled ≠ dip_007 — régression sur l'ancrage.");
            }

            // Tendance nonCore : plus INTEGRATION_TICKS est bas (actif), plus nonCore doit baisser.
            sb.AppendLine("TENDANCE nonCore@1000 (Disabled → 100 → 200 → 400):");
            for (var i = 0; i < TicksTried.Length; i++)
            {
                sb.AppendLine(
                    $"  {Label(TicksTried[i])}: nonCore={t1000[i].NonCoreProvinces}/" +
                    $"{t1000[i].TotalProvincesOwned} countries={t1000[i].CountriesWithLand} " +
                    $"maxProv={t1000[i].MaxProvincesOneCountry} " +
                    $"reconq={t1000[i].ReconquestWars}/{t1000[i].WarsDeclared}");
            }

            var bestIdx = -1;
            var bestNonCore = int.MaxValue;
            for (var i = 0; i < TicksTried.Length; i++)
            {
                if (TicksTried[i] <= ProvinceIntegration.DisabledIntegrationTicks)
                    continue;

                var s = t1000[i];
                var s800 = t800[i];
                var concluded = s800.Victories + s800.WhitePeaces;
                var ratio = concluded > 0 ? (float)s800.Victories / concluded : 0f;
                var consolOk = s.CountriesWithLand >= 13 && s.CountriesWithLand <= 16 &&
                               s.MaxProvincesOneCountry <= 11;
                var dipOk = ratio >= 0.70f && ratio <= 0.82f && s800.Stuck == 0;
                var debtOk = s.TotalDebt >= 700f && s.TotalDebt <= 1100f &&
                             s.Bankrupt >= 2 && s.Bankrupt <= 5;
                var armyOk = s.WorldArmyStr >= 36000f && s.WorldArmyStr <= 44000f;
                var effectOk = s.NonCoreProvinces < 33; // baisse nette vs dip_007 ~33/50
                // Intégration sous occupation impossible par construction (Controller!=Owner → defer).

                sb.AppendLine(
                    $"CANDIDAT {Label(TicksTried[i])}: nonCore={s.NonCoreProvinces} " +
                    $"consolOk={consolOk} dipOk={dipOk} debtOk={debtOk} armyOk={armyOk} " +
                    $"effectOk={effectOk} occDeferred={s.OccupiedDeferred} " +
                    $"reconq={s.ReconquestWars}/{s.WarsDeclared}");

                if (!consolOk || !dipOk || !debtOk || !armyOk || !effectOk)
                    continue;

                if (s.NonCoreProvinces < bestNonCore ||
                    (s.NonCoreProvinces == bestNonCore &&
                     (bestIdx < 0 || TicksTried[i] > TicksTried[bestIdx])))
                {
                    // Préférer nonCore bas ; à égalité, ticks plus longs (moins agressif).
                    bestNonCore = s.NonCoreProvinces;
                    bestIdx = i;
                }
            }

            if (bestIdx < 0)
            {
                sb.AppendLine(
                    "ALERT: aucun INTEGRATION_TICKS actif ne satisfait consolidation + dip_005 + " +
                    "dette + armée + baisse nonCore. Hypothèse peut-être RÉFUTÉE — " +
                    "rapporter franchement ; allonger / Disabled si consolidation régresse.");
                // Fallback diagnostic : 200 si présent
                bestIdx = IndexOf(200);
                if (bestIdx < 0)
                    bestIdx = IndexOf(400);
            }

            if (bestIdx >= 0)
            {
                var ret = t1000[bestIdx];
                var ret800 = t800[bestIdx];
                var concluded = ret800.Victories + ret800.WhitePeaces;
                var ratio = concluded > 0 ? (float)ret800.Victories / concluded : 0f;
                var disabledReconq = disabledIdx >= 0 ? t1000[disabledIdx].ReconquestWars : -1;
                var disabledWars = disabledIdx >= 0 ? t1000[disabledIdx].WarsDeclared : -1;

                sb.AppendLine(
                    $"RETENU: INTEGRATION_TICKS={Label(TicksTried[bestIdx])} — " +
                    $"nonCore@1000={ret.NonCoreProvinces}/{ret.TotalProvincesOwned} " +
                    $"countries={ret.CountriesWithLand} maxProv={ret.MaxProvincesOneCountry} " +
                    $"ratioV@800={(ratio * 100f).ToString("F1", CultureInfo.InvariantCulture)}% " +
                    $"annexed={ret800.Annexed} stuck={ret800.Stuck} " +
                    $"wars={ret.WarsDeclared} reconq={ret.ReconquestWars} " +
                    $"(Disabled wars={disabledWars} reconq={disabledReconq}) " +
                    $"debt={Fmt1(ret.TotalDebt)} bankrupt={ret.Bankrupt} " +
                    $"army={Fmt0(ret.WorldArmyStr)} zombie={Fmt0(ret.ZombieArmyStr)} " +
                    $"sat={Fmt3(ret.NeedsSatAvg)} pop={ret.Population} " +
                    $"integrated={ret.ProvincesIntegrated} occDeferred={ret.OccupiedDeferred} " +
                    $"reoccAfterInteg={ret.IntegratedThenReoccupied}");
                sb.AppendLine(
                    $"DefaultIntegrationTicks production attendu={TicksTried[bestIdx]} " +
                    $"(vérifier ProvinceIntegration.DefaultIntegrationTicks).");
                sb.AppendLine(
                    "OK: intégration sous occupation refusée par construction " +
                    $"(Controller!=Owner → OccupiedIntegrationDeferred; occDeferred={ret.OccupiedDeferred}).");
            }
        }

        static int IndexOf(int ticks)
        {
            for (var i = 0; i < TicksTried.Length; i++)
            {
                if (TicksTried[i] == ticks)
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
                snap.WarsDeclared++;
                if (war.CasusBelli == CasusBelli.Reconquest)
                    snap.ReconquestWars++;
                else if (war.CasusBelli == CasusBelli.Conquest)
                    snap.ConquestWars++;

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
                snap.TotalProvincesOwned++;

                if (o.Owner != o.Core)
                {
                    snap.NonCoreProvinces++;
                    snap.Annexed++;
                }

                // Informatif : conquise (OwnerChangedTick>0), intégrée (Core==Owner), réoccupée.
                if (o.Core == o.Owner && o.Controller != o.Owner && o.OwnerChangedTick > 0)
                    snap.IntegratedThenReoccupied++;
            }

            snap.CountriesWithLand = owners.Count;
            foreach (var kv in provinceCounts)
            {
                if (kv.Value > snap.MaxProvincesOneCountry)
                    snap.MaxProvincesOneCountry = kv.Value;
            }

            using var armyQuery = em.CreateEntityQuery(ComponentType.ReadOnly<ArmyData>());
            using var armies = armyQuery.ToComponentDataArray<ArmyData>(Allocator.Temp);
            for (var i = 0; i < armies.Length; i++)
            {
                snap.WorldArmyStr += armies[i].Strength;
                if (armies[i].Country != Entity.Null &&
                    (!provinceCounts.TryGetValue(armies[i].Country, out var pc) || pc <= 0))
                    snap.ZombieArmyStr += armies[i].Strength;
            }

            using var countryQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<CountryData>(),
                ComponentType.ReadOnly<TreasuryData>());
            using var treasuries = countryQuery.ToComponentDataArray<TreasuryData>(Allocator.Temp);
            for (var i = 0; i < treasuries.Length; i++)
            {
                snap.TotalDebt += treasuries[i].Debt;
                if (treasuries[i].BankruptcyTick > 0)
                    snap.Bankrupt++;
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
            return snap;
        }

        static string Fmt(float v) => v.ToString("F2", CultureInfo.InvariantCulture);
        static string Fmt0(float v) => v.ToString("F0", CultureInfo.InvariantCulture);
        static string Fmt1(float v) => v.ToString("F1", CultureInfo.InvariantCulture);
        static string Fmt3(float v) => v.ToString("F3", CultureInfo.InvariantCulture);
    }
}
