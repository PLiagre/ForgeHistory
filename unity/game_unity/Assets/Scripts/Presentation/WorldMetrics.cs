using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Unity.Collections;
using Unity.Entities;
using VictoriaGame.Core;
using VictoriaGame.Economy;
using VictoriaGame.Military;
using VictoriaGame.Population;
using VictoriaGame.World;

namespace VictoriaGame.Presentation
{
    /// <summary>
    /// Module de métriques CANONIQUE partagé — source unique pour tous les harnais de mesure.
    ///
    /// DÉFINITIONS (population de référence explicite ; ratios toujours nommés avec dénominateur) :
    ///
    /// Économie
    /// - TotalDebt : somme de TreasuryData.Debt sur TOUS les pays (CountryData+TreasuryData).
    /// - BankruptCount : pays avec BankruptcyTick &gt; 0 (même population : tous les pays).
    /// - NeedsSatAvg : moyenne de PopData.NeedsSatisfaction PONDÉRÉE par PopData.Size
    ///   (Σ sat×size / Σ size). PAS une moyenne simple par pop.
    /// - Population : Σ PopData.Size (toutes les pops).
    ///
    /// Militaire
    /// - WorldArmyStr : Σ ArmyData.Strength (toutes les armées).
    /// - TotalRegiments : Σ longueur des buffers RegimentSlot.
    /// - AvgStrPerRegiment : Σ RegimentSlot.Strength / TotalRegiments (0 si aucun régiment).
    /// - ZombieArmyStrLandless : Σ Strength des armées dont le pays a 0 province (Owner).
    /// - LivingArmies : nombre d'entités ArmyData (dénominateur naturel de WorldArmyStr).
    ///
    /// Politique / carte
    /// - CountriesWithLand : pays distincts avec ≥1 ProvinceOwnership.Owner.
    /// - MaxProvincesOneCountry : max du nombre de provinces par Owner.
    /// - NonCoreProvinces / TotalProvincesOwned : provinces avec Owner≠Core, sur toutes
    ///   les provinces ayant un Owner non-null (ex. « 18/50 »).
    /// - AllCountries : nombre d'entités CountryData+TreasuryData (dénominateur « all »).
    ///
    /// Guerre (currentTick requis pour stuck)
    /// - WarsDeclared : nombre d'entités WarData.
    /// - Victories : guerres terminées (EndTick&gt;0, !IsActive) avec |WarScore|≥60.
    /// - WhitePeaces : guerres terminées avec |WarScore|&lt;60.
    /// - RatioVictories : Victories / (Victories+WhitePeaces), 0 si aucune conclusion.
    /// - AnnexedProvinces : Owner≠Core (identique à NonCoreProvinces à un tick donné ;
    ///   le nom « annexed@T » dans les logs désigne la même définition lue à T).
    /// - StuckWars : guerres encore IsActive avec (currentTick−StartTick)&gt;150.
    ///
    /// Solvabilité (leçon mil_023 vs eco_033 — JAMAIS un ratio nu)
    /// - InsolventGatedGrowthWithLand / CountriesWithLand : pays AVEC terre pour lesquels
    ///   !CanAffordGrowth(..., FluxCommitted). C'est le dénominateur correct (eco_033).
    /// - InsolventGatedGrowthAllCountries / AllCountries : même gate sur TOUS les pays
    ///   (y compris sans terre) — exposé pour éviter l'ambiguïté mil_023.
    /// - InsolventGatedRecruitWithLand / CountriesWithLand : !CanAffordRecruit sur pays terriens.
    /// - InsolventGatedRecruitAllCountries / AllCountries : !CanAffordRecruit sur tous.
    ///
    /// Référence production (seed 42195, INTEGRATION_TICKS=400, t1000) :
    /// nonCore=18/50, countriesWithLand=14, maxProvinces=11, totalDebt=750.9, bankrupt=4,
    /// worldArmyStr=38953, zombie=0, needsSatAvg=0.698, population=142551 ;
    /// ratioV@800=72.5%, annexed@800=31, stuck@800=0.
    /// </summary>
    public static class WorldMetrics
    {
        public struct Snapshot
        {
            // Économie
            public float TotalDebt;
            /// <summary>Somme de TreasuryData.Balance (tous les pays) — bandeau joueur.</summary>
            public float TotalTreasury;
            public int BankruptCount;
            public float NeedsSatAvg;
            public int Population;

            // Militaire
            public float WorldArmyStr;
            public int TotalRegiments;
            public float AvgStrPerRegiment;
            public float ZombieArmyStrLandless;
            public int LivingArmies;

            // Politique / carte
            public int CountriesWithLand;
            public int MaxProvincesOneCountry;
            public int NonCoreProvinces;
            public int TotalProvincesOwned;
            public int AllCountries;

            // Guerre
            public int WarsDeclared;
            public int ActiveWars;
            public int Victories;
            public int WhitePeaces;
            public float RatioVictories;
            public int AnnexedProvinces;
            public int StuckWars;

            // Solvabilité — croisssance (gate eco_033)
            public int InsolventGatedGrowthWithLand;
            public int InsolventGatedGrowthAllCountries;

            // Solvabilité — recrutement (gate eco_027 / mil_023)
            public int InsolventGatedRecruitWithLand;
            public int InsolventGatedRecruitAllCountries;
        }

        /// <summary>
        /// Calcule le snapshot canonique depuis un EntityManager.
        /// <paramref name="currentTick"/> est le tick de simulation (pour StuckWars).
        /// </summary>
        public static Snapshot Capture(EntityManager em, int currentTick)
        {
            var snap = new Snapshot();
            var provinceCounts = new Dictionary<Entity, int>();

            CaptureMap(em, ref snap, provinceCounts);
            CaptureWars(em, currentTick, ref snap);
            CaptureMilitary(em, provinceCounts, ref snap);
            CaptureEconomyAndSolvency(em, provinceCounts, ref snap);
            CapturePopulation(em, ref snap);

            var concluded = snap.Victories + snap.WhitePeaces;
            snap.RatioVictories = concluded > 0 ? (float)snap.Victories / concluded : 0f;
            return snap;
        }

        /// <summary>
        /// Ligne de log standard — même forme d'une tâche à l'autre.
        /// Formats : debt F1, army/zombie F0, sat F3, ratioV F1%.
        /// </summary>
        public static string FormatStandardLine(int tick, in Snapshot snap)
        {
            var sb = new StringBuilder(512);
            sb.Append("tick").Append(tick.ToString(CultureInfo.InvariantCulture)).Append(": ");
            sb.Append("countriesWithLand=").Append(snap.CountriesWithLand).Append(' ');
            sb.Append("maxProvinces=").Append(snap.MaxProvincesOneCountry).Append(' ');
            sb.Append("nonCore=").Append(snap.NonCoreProvinces).Append('/')
                .Append(snap.TotalProvincesOwned).Append(' ');
            sb.Append("victories=").Append(snap.Victories).Append(' ');
            sb.Append("whitePeaces=").Append(snap.WhitePeaces).Append(' ');
            sb.Append("warsDeclared=").Append(snap.WarsDeclared).Append(' ');
            sb.Append("ratioV=").Append(Fmt1(snap.RatioVictories * 100f)).Append("% ");
            sb.Append("annexedProvinces=").Append(snap.AnnexedProvinces).Append(' ');
            sb.Append("stuckWars=").Append(snap.StuckWars).Append(' ');
            sb.Append("totalDebt=").Append(Fmt1(snap.TotalDebt)).Append(' ');
            sb.Append("bankruptCount=").Append(snap.BankruptCount).Append(' ');
            sb.Append("worldArmyStr=").Append(Fmt0(snap.WorldArmyStr)).Append(' ');
            sb.Append("totalRegiments=").Append(snap.TotalRegiments).Append(' ');
            sb.Append("avgStrPerRegiment=").Append(Fmt1(snap.AvgStrPerRegiment)).Append(' ');
            sb.Append("zombieArmyStrLandless=").Append(Fmt0(snap.ZombieArmyStrLandless)).Append(' ');
            sb.Append("livingArmies=").Append(snap.LivingArmies).Append(' ');
            sb.Append("needsSatAvg=").Append(Fmt3(snap.NeedsSatAvg)).Append(' ');
            sb.Append("population=").Append(snap.Population).Append(' ');
            sb.Append("insolventGatedGrowth=")
                .Append(snap.InsolventGatedGrowthWithLand).Append('/')
                .Append(snap.CountriesWithLand)
                .Append("(withLand) ")
                .Append(snap.InsolventGatedGrowthAllCountries).Append('/')
                .Append(snap.AllCountries)
                .Append("(allCountries) ");
            sb.Append("insolventGatedRecruit=")
                .Append(snap.InsolventGatedRecruitWithLand).Append('/')
                .Append(snap.CountriesWithLand)
                .Append("(withLand) ")
                .Append(snap.InsolventGatedRecruitAllCountries).Append('/')
                .Append(snap.AllCountries)
                .Append("(allCountries)");
            return sb.ToString();
        }

        public static string Fmt0(float v) => v.ToString("F0", CultureInfo.InvariantCulture);
        public static string Fmt1(float v) => v.ToString("F1", CultureInfo.InvariantCulture);
        public static string Fmt3(float v) => v.ToString("F3", CultureInfo.InvariantCulture);

        static void CaptureMap(EntityManager em, ref Snapshot snap, Dictionary<Entity, int> provinceCounts)
        {
            var owners = new HashSet<Entity>();
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
                    snap.AnnexedProvinces++;
                }
            }

            snap.CountriesWithLand = owners.Count;
            foreach (var kv in provinceCounts)
            {
                if (kv.Value > snap.MaxProvincesOneCountry)
                    snap.MaxProvincesOneCountry = kv.Value;
            }
        }

        static void CaptureWars(EntityManager em, int currentTick, ref Snapshot snap)
        {
            using var warQuery = em.CreateEntityQuery(ComponentType.ReadOnly<WarData>());
            using var wars = warQuery.ToComponentDataArray<WarData>(Allocator.Temp);
            for (var i = 0; i < wars.Length; i++)
            {
                var war = wars[i];
                snap.WarsDeclared++;

                if (war.IsActive)
                {
                    snap.ActiveWars++;
                    if (currentTick - war.StartTick > 150)
                        snap.StuckWars++;
                    continue;
                }

                if (war.EndTick <= 0)
                    continue;

                if (System.Math.Abs(war.WarScore) >= 60f)
                    snap.Victories++;
                else
                    snap.WhitePeaces++;
            }
        }

        static void CaptureMilitary(
            EntityManager em,
            Dictionary<Entity, int> provinceCounts,
            ref Snapshot snap)
        {
            using var armyQuery = em.CreateEntityQuery(ComponentType.ReadOnly<ArmyData>());
            using var armies = armyQuery.ToComponentDataArray<ArmyData>(Allocator.Temp);
            snap.LivingArmies = armies.Length;
            for (var i = 0; i < armies.Length; i++)
            {
                snap.WorldArmyStr += armies[i].Strength;
                if (armies[i].Country != Entity.Null &&
                    (!provinceCounts.TryGetValue(armies[i].Country, out var pc) || pc <= 0))
                    snap.ZombieArmyStrLandless += armies[i].Strength;
            }

            var regimentStrengthSum = 0f;
            using var regQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<ArmyData>(),
                ComponentType.ReadOnly<RegimentSlot>());
            using var armyEntities = regQuery.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < armyEntities.Length; i++)
            {
                var slots = em.GetBuffer<RegimentSlot>(armyEntities[i]);
                snap.TotalRegiments += slots.Length;
                for (var s = 0; s < slots.Length; s++)
                    regimentStrengthSum += slots[s].Strength;
            }

            snap.AvgStrPerRegiment = snap.TotalRegiments > 0
                ? regimentStrengthSum / snap.TotalRegiments
                : 0f;
        }

        static void CaptureEconomyAndSolvency(
            EntityManager em,
            Dictionary<Entity, int> provinceCounts,
            ref Snapshot snap)
        {
            var armyByCountry = SumArmyByCountry(em);
            var regsByCountry = CountRegsByCountry(em);

            using var countryQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<CountryData>(),
                ComponentType.ReadOnly<TreasuryData>());
            using var countries = countryQuery.ToEntityArray(Allocator.Temp);
            using var treasuries = countryQuery.ToComponentDataArray<TreasuryData>(Allocator.Temp);

            snap.AllCountries = countries.Length;
            for (var i = 0; i < countries.Length; i++)
            {
                var entity = countries[i];
                provinceCounts.TryGetValue(entity, out var prov);
                armyByCountry.TryGetValue(entity, out var armyStr);
                regsByCountry.TryGetValue(entity, out var regCount);
                var hasLand = prov > 0;

                snap.TotalDebt += treasuries[i].Debt;
                snap.TotalTreasury += treasuries[i].Balance;
                if (treasuries[i].BankruptcyTick > 0)
                    snap.BankruptCount++;

                var canGrow = ArmyDisbandmentSystem.CanAffordGrowth(
                    treasuries[i], regCount, armyStr, ArmySolvencyGateMode.FluxCommitted);
                var canRecruit = ArmyDisbandmentSystem.CanAffordRecruit(
                    treasuries[i], regCount, armyStr, ArmySolvencyGateMode.FluxCommitted);

                if (!canGrow)
                    snap.InsolventGatedGrowthAllCountries++;
                if (!canRecruit)
                    snap.InsolventGatedRecruitAllCountries++;

                if (hasLand)
                {
                    if (!canGrow)
                        snap.InsolventGatedGrowthWithLand++;
                    if (!canRecruit)
                        snap.InsolventGatedRecruitWithLand++;
                }
            }

            armyByCountry.Dispose();
            regsByCountry.Dispose();
        }

        static void CapturePopulation(EntityManager em, ref Snapshot snap)
        {
            // v1_090 : même agrégation que PopSizeAggregation (plus de boucle parallèle).
            PopSizeAggregation.SumAllWithWeightedSatisfaction(
                em, out var totalPop, out var weightedSat);
            snap.Population = totalPop;
            snap.NeedsSatAvg = totalPop > 0 ? (float)(weightedSat / totalPop) : 0f;
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
