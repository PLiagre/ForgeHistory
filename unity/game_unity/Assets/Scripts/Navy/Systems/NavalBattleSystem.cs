using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using VictoriaGame.Core;
using VictoriaGame.Military;

namespace VictoriaGame.Navy
{
    /// <summary>
    /// Résout les combats navals entre flottes de pays en guerre dans une même zone maritime.
    /// Applique pertes d'escadrons, moral, retraites et met à jour WarData.WarScore.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(SeaZoneControlSystem))]
    public partial struct NavalBattleSystem : ISystem
    {
        private const float BASE_CASUALTY_RATE = 0.05f;
        private const float LOSER_CASUALTY_MULTIPLIER = 1.75f;
        private const float WAR_SCORE_NAVAL_DELTA = 0.5f;
        private const float MORALE_COLLAPSE_THRESHOLD = 0.15f;
        private const float MIN_MORALE_FACTOR = 0.1f;
        private const float TRANSPORT_COMBAT_MULTIPLIER = 0.35f;

        private struct NavyBattleEntry
        {
            public Entity Entity;
            public Entity Country;
            public int CountryId;
            public int SeaZoneId;
            public NavyMission Mission;
            public float NavalMorale;
        }

        private struct PendingNavalBattle
        {
            public Entity NavyA;
            public Entity NavyB;
            public Entity CountryA;
            public Entity CountryB;
            public int ZoneId;
            public Entity WarEntity;
            public bool CountryAIsAttacker;
        }

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WorldState>();
        }

        [BurstDiscard]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.HasSingleton<WorldState>())
            {
                return;
            }

            var worldState = SystemAPI.GetSingleton<WorldState>();
            if (worldState.IsPaused)
            {
                return;
            }

            var currentTick = worldState.CurrentTick;
            var globalSeed = worldState.GlobalSeed;

            var zoneIsOcean = new NativeHashMap<int, bool>(16, Allocator.Temp);
            var zoneController = new NativeHashMap<int, Entity>(16, Allocator.Temp);
            var zoneNeighbors = new NativeParallelMultiHashMap<int, int>(32, Allocator.Temp);

            foreach (var (zone, neighbors) in SystemAPI.Query<RefRO<SeaZoneData>, DynamicBuffer<SeaZoneNeighbor>>())
            {
                var zoneId = zone.ValueRO.ZoneId;
                zoneIsOcean[zoneId] = zone.ValueRO.IsOcean;
                zoneController[zoneId] = zone.ValueRO.Controller;

                for (var i = 0; i < neighbors.Length; i++)
                {
                    zoneNeighbors.Add(zoneId, neighbors[i].NeighborZoneId);
                }
            }

            var countryIds = new NativeHashMap<Entity, int>(32, Allocator.Temp);
            foreach (var (country, entity) in SystemAPI.Query<RefRO<CountryData>>().WithEntityAccess())
            {
                countryIds.TryAdd(entity, country.ValueRO.CountryId);
            }

            var countryMilTech = new NativeHashMap<Entity, int>(32, Allocator.Temp);
            foreach (var (tech, entity) in SystemAPI.Query<RefRO<TechData>>().WithEntityAccess())
            {
                countryMilTech[entity] = tech.ValueRO.MilTech;
            }

            var activeWars = new NativeList<(Entity Entity, WarData Data)>(8, Allocator.Temp);
            foreach (var (war, warEntity) in SystemAPI.Query<RefRO<WarData>>().WithEntityAccess())
            {
                if (war.ValueRO.IsActive)
                {
                    activeWars.Add((warEntity, war.ValueRO));
                }
            }

            var naviesByZone = new NativeParallelMultiHashMap<int, NavyBattleEntry>(32, Allocator.Temp);
            foreach (var (navy, navyEntity) in SystemAPI.Query<RefRO<NavyData>>().WithEntityAccess())
            {
                if (navy.ValueRO.Country == Entity.Null || navy.ValueRO.SeaZoneId <= 0)
                {
                    continue;
                }

                countryIds.TryGetValue(navy.ValueRO.Country, out var navyCountryId);
                naviesByZone.Add(navy.ValueRO.SeaZoneId, new NavyBattleEntry
                {
                    Entity = navyEntity,
                    Country = navy.ValueRO.Country,
                    CountryId = navyCountryId,
                    SeaZoneId = navy.ValueRO.SeaZoneId,
                    Mission = navy.ValueRO.Mission,
                    NavalMorale = navy.ValueRO.NavalMorale
                });
            }

            var pendingBattles = new NativeList<PendingNavalBattle>(16, Allocator.Temp);
            var engagedNavies = new NativeHashSet<Entity>(16, Allocator.Temp);

            foreach (var kvp in naviesByZone)
            {
                var zoneId = kvp.Key;
                if (!naviesByZone.TryGetFirstValue(zoneId, out var first, out var iterator))
                {
                    continue;
                }

                var zoneNavies = new NativeList<NavyBattleEntry>(8, Allocator.Temp);
                do
                {
                    zoneNavies.Add(first);
                }
                while (naviesByZone.TryGetNextValue(out first, ref iterator));

                zoneNavies.Sort(new NavyEntryComparer());

                for (var i = 0; i < zoneNavies.Length; i++)
                {
                    var entryA = zoneNavies[i];
                    if (engagedNavies.Contains(entryA.Entity))
                    {
                        continue;
                    }

                    for (var j = i + 1; j < zoneNavies.Length; j++)
                    {
                        var entryB = zoneNavies[j];
                        if (engagedNavies.Contains(entryB.Entity))
                        {
                            continue;
                        }

                        if (entryA.Country == entryB.Country)
                        {
                            continue;
                        }

                        if (!AreAtWar(entryA.Country, entryB.Country, activeWars, out var warEntity, out var warData, out var aIsAttacker))
                        {
                            continue;
                        }

                        if (!ShouldTriggerBattle(entryA.Mission, entryB.Mission))
                        {
                            continue;
                        }

                        pendingBattles.Add(new PendingNavalBattle
                        {
                            NavyA = entryA.Entity,
                            NavyB = entryB.Entity,
                            CountryA = entryA.Country,
                            CountryB = entryB.Country,
                            ZoneId = zoneId,
                            WarEntity = warEntity,
                            CountryAIsAttacker = aIsAttacker
                        });

                        engagedNavies.Add(entryA.Entity);
                        engagedNavies.Add(entryB.Entity);
                        break;
                    }
                }

                zoneNavies.Dispose();
            }

            for (var b = 0; b < pendingBattles.Length; b++)
            {
                var battle = pendingBattles[b];
                var isOcean = zoneIsOcean.TryGetValue(battle.ZoneId, out var ocean) && ocean;

                var squadronsA = SystemAPI.GetBuffer<ShipSquadron>(battle.NavyA);
                var squadronsB = SystemAPI.GetBuffer<ShipSquadron>(battle.NavyB);
                var navyA = SystemAPI.GetComponent<NavyData>(battle.NavyA);
                var navyB = SystemAPI.GetComponent<NavyData>(battle.NavyB);

                var techA = GetMilTech(battle.CountryA, countryMilTech);
                var techB = GetMilTech(battle.CountryB, countryMilTech);

                var powerA = ComputeFleetCombatPower(
                    squadronsA, squadronsB, isOcean, navyA.NavalMorale, techA, navyA.Mission);
                var powerB = ComputeFleetCombatPower(
                    squadronsB, squadronsA, isOcean, navyB.NavalMorale, techB, navyB.Mission);

                if (powerA <= 0f && powerB <= 0f)
                {
                    continue;
                }

                if (powerA <= 0f || powerB <= 0f)
                {
                    var winnerIsA = powerA > powerB;
                    ApplyOneSidedVictory(
                        ref state,
                        battle,
                        winnerIsA,
                        isOcean,
                        squadronsA,
                        squadronsB,
                        ref navyA,
                        ref navyB,
                        zoneController,
                        zoneNeighbors,
                        globalSeed,
                        currentTick);
                    SystemAPI.SetComponent(battle.NavyA, navyA);
                    SystemAPI.SetComponent(battle.NavyB, navyB);
                    continue;
                }

                var rng = CreateBattleRandom(globalSeed, currentTick, battle.ZoneId);
                var variance = rng.NextFloat(0.92f, 1.08f);

                var powerSum = powerA + powerB;
                var shareA = powerA / powerSum;
                var shareB = powerB / powerSum;

                var lossRateA = BASE_CASUALTY_RATE * variance * shareB * 2f;
                var lossRateB = BASE_CASUALTY_RATE * variance * shareA * 2f;

                if (shareA > shareB)
                {
                    lossRateB *= LOSER_CASUALTY_MULTIPLIER;
                }
                else if (shareB > shareA)
                {
                    lossRateA *= LOSER_CASUALTY_MULTIPLIER;
                }

                ApplySquadronLosses(squadronsA, lossRateA, isOcean, ref rng);
                ApplySquadronLosses(squadronsB, lossRateB, isOcean, ref rng);

                navyA.NavalMorale = math.max(0f, navyA.NavalMorale * (1f - lossRateA * 0.9f));
                navyB.NavalMorale = math.max(0f, navyB.NavalMorale * (1f - lossRateB * 0.9f));

                navyA.NavalStrength = ComputeNavalStrength(squadronsA, isOcean);
                navyB.NavalStrength = ComputeNavalStrength(squadronsB, isOcean);

                TryRetreat(ref navyA, battle.ZoneId, battle.CountryA, zoneController, zoneNeighbors);
                TryRetreat(ref navyB, battle.ZoneId, battle.CountryB, zoneController, zoneNeighbors);

                SystemAPI.SetComponent(battle.NavyA, navyA);
                SystemAPI.SetComponent(battle.NavyB, navyB);

                if (state.EntityManager.Exists(battle.WarEntity) &&
                    state.EntityManager.HasComponent<WarData>(battle.WarEntity))
                {
                    var warData = state.EntityManager.GetComponentData<WarData>(battle.WarEntity);
                    var outcomeBias = (shareA - 0.5f) * 2f;
                    if (!battle.CountryAIsAttacker)
                    {
                        outcomeBias = -outcomeBias;
                    }

                    var scoreDelta = outcomeBias * WAR_SCORE_NAVAL_DELTA * variance;
                    warData.WarScore = math.clamp(warData.WarScore + scoreDelta, -100f, 100f);
                    state.EntityManager.SetComponentData(battle.WarEntity, warData);
                }
            }

            pendingBattles.Dispose();
            engagedNavies.Dispose();
            naviesByZone.Dispose();
            activeWars.Dispose();
            countryMilTech.Dispose();
            countryIds.Dispose();
            zoneNeighbors.Dispose();
            zoneController.Dispose();
            zoneIsOcean.Dispose();
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        private struct NavyEntryComparer : System.Collections.Generic.IComparer<NavyBattleEntry>
        {
            public int Compare(NavyBattleEntry x, NavyBattleEntry y)
            {
                return DomainKeys.CompareNavyKeys(
                    x.CountryId, x.SeaZoneId,
                    y.CountryId, y.SeaZoneId);
            }
        }

        private static Random CreateBattleRandom(uint globalSeed, int tick, int seaZoneId)
        {
            var seed = math.hash(new uint3(globalSeed, (uint)tick, (uint)seaZoneId));
            return Random.CreateFromIndex(seed);
        }

        private static int GetMilTech(Entity country, NativeHashMap<Entity, int> countryMilTech)
        {
            return countryMilTech.TryGetValue(country, out var milTech) ? milTech : 0;
        }

        private static bool IsCombatSeeker(NavyMission mission)
        {
            return mission == NavyMission.Battle
                   || mission == NavyMission.Patrol
                   || mission == NavyMission.Blockade;
        }

        private static bool ShouldTriggerBattle(NavyMission missionA, NavyMission missionB)
        {
            return IsCombatSeeker(missionA) || IsCombatSeeker(missionB);
        }

        private static bool AreAtWar(
            Entity countryA,
            Entity countryB,
            NativeList<(Entity Entity, WarData Data)> activeWars,
            out Entity warEntity,
            out WarData warData,
            out bool countryAIsAttacker)
        {
            for (var i = 0; i < activeWars.Length; i++)
            {
                var entry = activeWars[i];
                if (entry.Data.Attacker == countryA && entry.Data.Defender == countryB)
                {
                    warEntity = entry.Entity;
                    warData = entry.Data;
                    countryAIsAttacker = true;
                    return true;
                }

                if (entry.Data.Attacker == countryB && entry.Data.Defender == countryA)
                {
                    warEntity = entry.Entity;
                    warData = entry.Data;
                    countryAIsAttacker = false;
                    return true;
                }
            }

            warEntity = Entity.Null;
            warData = default;
            countryAIsAttacker = false;
            return false;
        }

        private static float GetCombatMissionMultiplier(NavyMission mission)
        {
            switch (mission)
            {
                case NavyMission.Battle:
                    return 1f;
                case NavyMission.Patrol:
                    return 1f;
                case NavyMission.Blockade:
                    return 0.95f;
                case NavyMission.Transport:
                    return TRANSPORT_COMBAT_MULTIPLIER;
                case NavyMission.ConvoyEscort:
                    return 0.8f;
                case NavyMission.ConvoyRaid:
                    return 1.1f;
                default:
                    return 1f;
            }
        }

        private static float GetShipPower(ShipType type)
        {
            switch (type)
            {
                case ShipType.Galley: return 1f;
                case ShipType.Cog: return 1f;
                case ShipType.Carrack: return 2f;
                case ShipType.Galleon: return 3f;
                case ShipType.Frigate: return 3f;
                case ShipType.ShipOfLine: return 5f;
                case ShipType.ManOfWar: return 6f;
                case ShipType.SteamFrigate: return 5f;
                case ShipType.Ironclad: return 7f;
                default: return 1f;
            }
        }

        private static bool IsSteamType(ShipType type)
        {
            return type == ShipType.SteamFrigate || type == ShipType.Ironclad;
        }

        private static bool IsCargoType(ShipType type)
        {
            return type == ShipType.Cog || type == ShipType.Carrack;
        }

        private static float GetTypeMatchupMultiplier(ShipType own, ShipType enemy)
        {
            if (IsSteamType(own) && !IsSteamType(enemy))
            {
                return 2.5f;
            }

            if (!IsSteamType(own) && IsSteamType(enemy))
            {
                return 0.35f;
            }

            switch (own)
            {
                case ShipType.Galley:
                    if (IsCargoType(enemy))
                    {
                        return 1.6f;
                    }

                    if (enemy == ShipType.ShipOfLine || enemy == ShipType.ManOfWar)
                    {
                        return 0.25f;
                    }

                    if (enemy == ShipType.Frigate)
                    {
                        return 0.75f;
                    }

                    return 1f;

                case ShipType.Frigate:
                    if (IsCargoType(enemy))
                    {
                        return 1.55f;
                    }

                    if (enemy == ShipType.ShipOfLine || enemy == ShipType.ManOfWar)
                    {
                        return 0.5f;
                    }

                    if (enemy == ShipType.Galley)
                    {
                        return 1.2f;
                    }

                    return 1f;

                case ShipType.ShipOfLine:
                case ShipType.ManOfWar:
                    if (enemy == ShipType.Frigate)
                    {
                        return 0.65f;
                    }

                    if (enemy == ShipType.ShipOfLine || enemy == ShipType.ManOfWar)
                    {
                        return 1.15f;
                    }

                    if (IsCargoType(enemy))
                    {
                        return 1.3f;
                    }

                    return 1f;

                case ShipType.Cog:
                case ShipType.Carrack:
                    return 0.6f;

                default:
                    return 1f;
            }
        }

        private static float ComputeAverageMatchup(
            ShipType ownType,
            DynamicBuffer<ShipSquadron> enemySquadrons,
            bool isOcean)
        {
            var weightedSum = 0f;
            var totalWeight = 0f;

            for (var i = 0; i < enemySquadrons.Length; i++)
            {
                var enemy = enemySquadrons[i];
                if (enemy.Count <= 0)
                {
                    continue;
                }

                if (isOcean && enemy.Type == ShipType.Galley)
                {
                    continue;
                }

                var weight = enemy.Count * math.clamp(enemy.Condition, 0f, 1f);
                weightedSum += GetTypeMatchupMultiplier(ownType, enemy.Type) * weight;
                totalWeight += weight;
            }

            if (totalWeight <= 0f)
            {
                return 1f;
            }

            return weightedSum / totalWeight;
        }

        private static float ComputeFleetCombatPower(
            DynamicBuffer<ShipSquadron> squadrons,
            DynamicBuffer<ShipSquadron> enemySquadrons,
            bool isOcean,
            float navalMorale,
            int milTech,
            NavyMission mission)
        {
            var missionMul = GetCombatMissionMultiplier(mission);
            var techFactor = 1f + milTech * 0.01f;
            var moraleFactor = math.max(MIN_MORALE_FACTOR, math.clamp(navalMorale, 0f, 1f));
            var sum = 0f;

            for (var i = 0; i < squadrons.Length; i++)
            {
                var squadron = squadrons[i];
                if (squadron.Count <= 0)
                {
                    continue;
                }

                if (isOcean && squadron.Type == ShipType.Galley)
                {
                    continue;
                }

                var matchup = ComputeAverageMatchup(squadron.Type, enemySquadrons, isOcean);
                var condition = math.clamp(squadron.Condition, 0f, 1f);
                sum += squadron.Count
                       * GetShipPower(squadron.Type)
                       * condition
                       * matchup
                       * moraleFactor
                       * techFactor
                       * missionMul;
            }

            return sum;
        }

        private static float ComputeNavalStrength(DynamicBuffer<ShipSquadron> squadrons, bool isOcean)
        {
            var sum = 0f;
            for (var i = 0; i < squadrons.Length; i++)
            {
                var squadron = squadrons[i];
                if (isOcean && squadron.Type == ShipType.Galley)
                {
                    continue;
                }

                sum += squadron.Count
                       * GetShipPower(squadron.Type)
                       * math.clamp(squadron.Condition, 0f, 1f);
            }

            return sum;
        }

        private static void ApplySquadronLosses(
            DynamicBuffer<ShipSquadron> squadrons,
            float lossRate,
            bool isOcean,
            ref Random rng)
        {
            var clampedRate = math.clamp(lossRate, 0f, 0.95f);

            for (var i = 0; i < squadrons.Length; i++)
            {
                var squadron = squadrons[i];
                if (squadron.Count <= 0)
                {
                    continue;
                }

                if (isOcean && squadron.Type == ShipType.Galley)
                {
                    continue;
                }

                var fractionalLoss = squadron.Count * clampedRate;
                var shipsLost = (int)math.floor(fractionalLoss);
                if (rng.NextFloat() < fractionalLoss - shipsLost)
                {
                    shipsLost++;
                }

                squadron.Count = math.max(0, squadron.Count - shipsLost);
                squadron.Condition = math.clamp(
                    squadron.Condition * (1f - clampedRate * 1.2f),
                    0f,
                    1f);
                squadrons[i] = squadron;
            }
        }

        private static void TryRetreat(
            ref NavyData navy,
            int currentZoneId,
            Entity country,
            NativeHashMap<int, Entity> zoneController,
            NativeParallelMultiHashMap<int, int> zoneNeighbors)
        {
            if (navy.NavalMorale > MORALE_COLLAPSE_THRESHOLD)
            {
                return;
            }

            if (!zoneNeighbors.TryGetFirstValue(currentZoneId, out var neighborId, out var iterator))
            {
                return;
            }

            var retreatZone = -1;
            do
            {
                if (zoneController.TryGetValue(neighborId, out var controller) && controller == country)
                {
                    retreatZone = neighborId;
                    break;
                }
            }
            while (zoneNeighbors.TryGetNextValue(out neighborId, ref iterator));

            if (retreatZone > 0 && retreatZone != currentZoneId)
            {
                navy.SeaZoneId = retreatZone;
                navy.Mission = NavyMission.Patrol;
            }
        }

        private static void ApplyOneSidedVictory(
            ref SystemState state,
            PendingNavalBattle battle,
            bool winnerIsA,
            bool isOcean,
            DynamicBuffer<ShipSquadron> squadronsA,
            DynamicBuffer<ShipSquadron> squadronsB,
            ref NavyData navyA,
            ref NavyData navyB,
            NativeHashMap<int, Entity> zoneController,
            NativeParallelMultiHashMap<int, int> zoneNeighbors,
            uint globalSeed,
            int currentTick)
        {
            var rng = CreateBattleRandom(globalSeed, currentTick, battle.ZoneId);

            var winnerLoss = BASE_CASUALTY_RATE * 0.35f;
            var loserLoss = BASE_CASUALTY_RATE * LOSER_CASUALTY_MULTIPLIER;

            if (winnerIsA)
            {
                ApplySquadronLosses(squadronsA, winnerLoss, isOcean, ref rng);
                ApplySquadronLosses(squadronsB, loserLoss, isOcean, ref rng);
                navyA.NavalMorale = math.max(0f, navyA.NavalMorale * 0.92f);
                navyB.NavalMorale = math.max(0f, navyB.NavalMorale * 0.5f);
            }
            else
            {
                ApplySquadronLosses(squadronsB, winnerLoss, isOcean, ref rng);
                ApplySquadronLosses(squadronsA, loserLoss, isOcean, ref rng);
                navyB.NavalMorale = math.max(0f, navyB.NavalMorale * 0.92f);
                navyA.NavalMorale = math.max(0f, navyA.NavalMorale * 0.5f);
            }

            navyA.NavalStrength = ComputeNavalStrength(squadronsA, isOcean);
            navyB.NavalStrength = ComputeNavalStrength(squadronsB, isOcean);

            TryRetreat(ref navyA, battle.ZoneId, battle.CountryA, zoneController, zoneNeighbors);
            TryRetreat(ref navyB, battle.ZoneId, battle.CountryB, zoneController, zoneNeighbors);

            if (state.EntityManager.Exists(battle.WarEntity) &&
                state.EntityManager.HasComponent<WarData>(battle.WarEntity))
            {
                var warData = state.EntityManager.GetComponentData<WarData>(battle.WarEntity);
                var delta = WAR_SCORE_NAVAL_DELTA * 0.5f;
                if (winnerIsA != battle.CountryAIsAttacker)
                {
                    delta = -delta;
                }

                warData.WarScore = math.clamp(warData.WarScore + delta, -100f, 100f);
                state.EntityManager.SetComponentData(battle.WarEntity, warData);
            }
        }
    }
}
