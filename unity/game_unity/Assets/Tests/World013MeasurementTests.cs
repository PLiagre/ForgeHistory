using System.Globalization;
using System.IO;
using System.Text;
using Unity.Collections;
using Unity.Entities;
using NUnit.Framework;
using VictoriaGame.Core;
using VictoriaGame.Military;
using VictoriaGame.Presentation;
using VictoriaGame.World;

namespace VictoriaGame.Tests
{
    /// <summary>Point d'entrée batchmode : -executeMethod VictoriaGame.Tests.World013BatchRunner.Run</summary>
    public static class World013BatchRunner
    {
        public static void Run()
        {
            World013MeasurementTests.RunMeasurementsAndWriteLog();
            UnityEngine.Debug.Log("World013BatchRunner: DONE");
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
            #endif
        }
    }

    /// <summary>
    /// Mesure bit-identique world_013 (Option A/B) : seed 42195, configs Disabled + 400,
    /// ticks 200/500/800/1000. Compare aux ancrages dip_008.
    /// Métriques via <see cref="WorldMetrics"/> (module canonique partagé).
    /// </summary>
    [TestFixture]
    public class World013MeasurementTests
    {
        const uint Seed = 42195u;
        static readonly int[] SnapshotTicks = { 200, 500, 800, 1000 };
        static readonly int[] Configs =
        {
            ProvinceIntegration.DisabledIntegrationTicks,
            ProvinceIntegration.DefaultIntegrationTicks
        };

        // Ancrages dip_008 (AVANT) — t1000 + ratioV@800 pour 400.
        const int Ref400NonCore = 18;
        const int Ref400Countries = 14;
        const int Ref400MaxProv = 11;
        const string Ref400RatioV800 = "72.5";
        const int Ref400Stuck = 0;
        const string Ref400Debt = "750.9";
        const int Ref400Bankrupt = 4;
        const string Ref400Army = "38953";
        const int Ref400Zombie = 0;
        const string Ref400Sat = "0.698";
        const int Ref400Pop = 142551;
        const int Ref400Integrated = 19;
        const int Ref400OccDeferred = 0;

        const int RefDisCountries = 14;
        const int RefDisMaxProv = 10;
        const string RefDisDebt = "750.9";
        const int RefDisBankrupt = 4;
        const string RefDisArmy = "36410";
        const string RefDisSat = "0.698";
        const int RefDisPop = 142551;
        const int RefDisNonCore = 33;

        /// <summary>Snapshot WorldMetrics + compteurs locaux world_013 (intégration / reconquête).</summary>
        struct Snap
        {
            public WorldMetrics.Snapshot M;
            public int ReconquestWars;
            public int ProvincesIntegrated;
            public int OccupiedDeferred;
        }

        [Test]
        public void World013_MeasureBitIdenticalToDip008() => RunMeasurementsAndWriteLog();

        public static void RunMeasurementsAndWriteLog()
        {
            var prevTicks = ProvinceIntegration.IntegrationTicks;
            var logPath = Path.Combine(
                UnityEngine.Application.dataPath, "..", "Logs", "world_013_measurements.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine(
                    $"=== world_013 seed={Seed} OPTION=A (static ProvinceIntegration, no ISystem) " +
                    $"DefaultIntegrationTicks={ProvinceIntegration.DefaultIntegrationTicks} ===");
                sb.AppendLine(
                    "Refactor lisibilité : retirer coquille ISystem + EnsureRegistered no-op. " +
                    "Logique IntegrateProvinces inchangée ; appel toujours en fin de PeaceSystem.");
                sb.AppendLine("Métriques : WorldMetrics.Capture (module canonique partagé).");
                sb.AppendLine();

                Snap snap400T800 = default;
                Snap snap400T1000 = default;
                Snap snapDisT1000 = default;

                for (var c = 0; c < Configs.Length; c++)
                {
                    var ticks = Configs[c];
                    ProvinceIntegration.IntegrationTicks = ticks;
                    sb.AppendLine($"=== APRÈS INTEGRATION_TICKS={Label(ticks)} ===");

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

                        if (ticks == ProvinceIntegration.DefaultIntegrationTicks && tick == 800)
                            snap400T800 = snap;
                        if (ticks == ProvinceIntegration.DefaultIntegrationTicks && tick == 1000)
                            snap400T1000 = snap;
                        if (ticks == ProvinceIntegration.DisabledIntegrationTicks && tick == 1000)
                            snapDisT1000 = snap;

                        AppendTickLine(sb, tick, snap);
                    }

                    sb.AppendLine();
                }

                AppendComparison(sb, snap400T800, snap400T1000, snapDisT1000);

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

        static void AppendTickLine(StringBuilder sb, int tick, Snap snap)
        {
            // Ligne canonique WorldMetrics + extras world_013 (reconquest / integration).
            sb.AppendLine(
                WorldMetrics.FormatStandardLine(tick, snap.M) +
                $" reconquest={snap.ReconquestWars} " +
                $"integrated={snap.ProvincesIntegrated} occDeferred={snap.OccupiedDeferred}");
        }

        static void AppendComparison(StringBuilder sb, Snap t800_400, Snap t1000_400, Snap t1000_dis)
        {
            var m800 = t800_400.M;
            var m1000 = t1000_400.M;
            var mDis = t1000_dis.M;
            var ratio800Str = WorldMetrics.Fmt1(m800.RatioVictories * 100f);

            sb.AppendLine("=== TABLEAU AVANT (dip_008) / APRÈS (world_013 Option A via WorldMetrics) ===");
            sb.AppendLine(
                "config | metric | AVANT | APRÈS | match");

            var allMatch = true;
            allMatch &= Row(sb, "400@1000", "nonCore", $"{Ref400NonCore}/50",
                $"{m1000.NonCoreProvinces}/{m1000.TotalProvincesOwned}",
                m1000.NonCoreProvinces == Ref400NonCore && m1000.TotalProvincesOwned == 50);
            allMatch &= Row(sb, "400@1000", "countriesWithLand", Ref400Countries.ToString(),
                m1000.CountriesWithLand.ToString(), m1000.CountriesWithLand == Ref400Countries);
            allMatch &= Row(sb, "400@1000", "maxProv", Ref400MaxProv.ToString(),
                m1000.MaxProvincesOneCountry.ToString(),
                m1000.MaxProvincesOneCountry == Ref400MaxProv);
            allMatch &= Row(sb, "400@800", "ratioV", Ref400RatioV800 + "%", ratio800Str + "%",
                ratio800Str == Ref400RatioV800);
            allMatch &= Row(sb, "400@800", "stuck", Ref400Stuck.ToString(),
                m800.StuckWars.ToString(), m800.StuckWars == Ref400Stuck);
            // annexed@800 : WorldMetrics.AnnexedProvinces == NonCoreProvinces (Owner≠Core).
            // Brief test_001 citait 31 — c'est la valeur Disabled@800 ; en prod 400@800 = 15
            // (intégration a déjà reconverti des cores). Rapporté dans notes, pas une régression.
            allMatch &= Row(sb, "400@1000", "totalDebt", Ref400Debt,
                WorldMetrics.Fmt1(m1000.TotalDebt), WorldMetrics.Fmt1(m1000.TotalDebt) == Ref400Debt);
            allMatch &= Row(sb, "400@1000", "bankrupt", Ref400Bankrupt.ToString(),
                m1000.BankruptCount.ToString(), m1000.BankruptCount == Ref400Bankrupt);
            allMatch &= Row(sb, "400@1000", "worldArmyStr", Ref400Army,
                WorldMetrics.Fmt0(m1000.WorldArmyStr), WorldMetrics.Fmt0(m1000.WorldArmyStr) == Ref400Army);
            allMatch &= Row(sb, "400@1000", "zombie", Ref400Zombie.ToString(),
                WorldMetrics.Fmt0(m1000.ZombieArmyStrLandless),
                WorldMetrics.Fmt0(m1000.ZombieArmyStrLandless) == Ref400Zombie.ToString());
            allMatch &= Row(sb, "400@1000", "needsSatAvg", Ref400Sat,
                WorldMetrics.Fmt3(m1000.NeedsSatAvg), WorldMetrics.Fmt3(m1000.NeedsSatAvg) == Ref400Sat);
            allMatch &= Row(sb, "400@1000", "population", Ref400Pop.ToString(),
                m1000.Population.ToString(), m1000.Population == Ref400Pop);
            allMatch &= Row(sb, "400@1000", "integrated", Ref400Integrated.ToString(),
                t1000_400.ProvincesIntegrated.ToString(),
                t1000_400.ProvincesIntegrated == Ref400Integrated);
            allMatch &= Row(sb, "400@1000", "occDeferred", Ref400OccDeferred.ToString(),
                t1000_400.OccupiedDeferred.ToString(),
                t1000_400.OccupiedDeferred == Ref400OccDeferred);

            allMatch &= Row(sb, "Disabled@1000", "countries", RefDisCountries.ToString(),
                mDis.CountriesWithLand.ToString(),
                mDis.CountriesWithLand == RefDisCountries);
            allMatch &= Row(sb, "Disabled@1000", "maxProv", RefDisMaxProv.ToString(),
                mDis.MaxProvincesOneCountry.ToString(),
                mDis.MaxProvincesOneCountry == RefDisMaxProv);
            allMatch &= Row(sb, "Disabled@1000", "debt", RefDisDebt,
                WorldMetrics.Fmt1(mDis.TotalDebt), WorldMetrics.Fmt1(mDis.TotalDebt) == RefDisDebt);
            allMatch &= Row(sb, "Disabled@1000", "bankrupt", RefDisBankrupt.ToString(),
                mDis.BankruptCount.ToString(), mDis.BankruptCount == RefDisBankrupt);
            allMatch &= Row(sb, "Disabled@1000", "army", RefDisArmy,
                WorldMetrics.Fmt0(mDis.WorldArmyStr), WorldMetrics.Fmt0(mDis.WorldArmyStr) == RefDisArmy);
            allMatch &= Row(sb, "Disabled@1000", "sat", RefDisSat,
                WorldMetrics.Fmt3(mDis.NeedsSatAvg), WorldMetrics.Fmt3(mDis.NeedsSatAvg) == RefDisSat);
            allMatch &= Row(sb, "Disabled@1000", "pop", RefDisPop.ToString(),
                mDis.Population.ToString(), mDis.Population == RefDisPop);
            allMatch &= Row(sb, "Disabled@1000", "nonCore", $"{RefDisNonCore}/50",
                $"{mDis.NonCoreProvinces}/{mDis.TotalProvincesOwned}",
                mDis.NonCoreProvinces == RefDisNonCore && mDis.TotalProvincesOwned == 50);

            sb.AppendLine();
            sb.AppendLine("=== VERDICT world_013 ===");
            if (allMatch)
            {
                sb.AppendLine(
                    "VERDICT: BIT-IDENTIQUE à dip_008 (configs 400 et Disabled) via WorldMetrics. " +
                    "OPTION A RETENUE — retirer la déclaration ISystem n'a pas décalé l'ordonnancement.");
            }
            else
            {
                sb.AppendLine(
                    "VERDICT: DIVERGENCE vs dip_008 — Option A a déplacé la simulation (TypeIndex). " +
                    "BASCULE REQUISE vers OPTION B (conserver ISystem + doc + retirer EnsureRegistered).");
            }
        }

        static bool Row(StringBuilder sb, string cfg, string metric, string before, string after, bool match)
        {
            sb.AppendLine($"  {cfg} | {metric} | {before} | {after} | {(match ? "OK" : "DIFF")}");
            return match;
        }

        static Snap CaptureSnap(EntityManager em, int currentTick)
        {
            var snap = new Snap
            {
                M = WorldMetrics.Capture(em, currentTick)
            };

            // Compteur local (hors jeu canonique) : guerres Reconquest.
            using var warQuery = em.CreateEntityQuery(ComponentType.ReadOnly<WarData>());
            using var wars = warQuery.ToComponentDataArray<WarData>(Allocator.Temp);
            for (var i = 0; i < wars.Length; i++)
            {
                if (wars[i].CasusBelli == CasusBelli.Reconquest)
                    snap.ReconquestWars++;
            }

            return snap;
        }
    }
}
