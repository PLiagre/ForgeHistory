using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using VictoriaGame.Core;
using VictoriaGame.World;

namespace VictoriaGame.Military
{
    /// <summary>
    /// Déclare des guerres lorsque les conditions diplomatiques et militaires sont réunies :
    /// voisinage terrestre, casus belli, prestige ou reconquête, treve respectée.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(SimulationTickSystem))]
    [UpdateBefore(typeof(FrontLineSystem))]
    public partial struct WarDeclarationSystem : ISystem
    {
        private const int TRUCE_TICKS = 120;
        private const int WAR_CHECK_INTERVAL = 12;
        private const float CONQUEST_DECLARE_CHANCE = 0.35f;

        private struct CountryEntry : System.IComparable<CountryEntry>
        {
            public Entity Entity;
            public int CountryId;
            public FixedString32Bytes Tag;
            public float Prestige;

            public int CompareTo(CountryEntry other)
            {
                return Tag.CompareTo(other.Tag);
            }
        }

        private struct NeighborEntry : System.IComparable<NeighborEntry>
        {
            public Entity Entity;
            public int CountryId;
            public FixedString32Bytes Tag;
            public float Prestige;

            public int CompareTo(NeighborEntry other)
            {
                return Tag.CompareTo(other.Tag);
            }
        }

        private struct PendingWar
        {
            public Entity Attacker;
            public Entity Defender;
            public CasusBelli CasusBelli;
        }

        /// <summary>
        /// Paire de pays ordonnée par CountryId (rang countries.json), jamais Entity.Index.
        /// </summary>
        private struct CountryPair : System.IEquatable<CountryPair>
        {
            public int A;
            public int B;

            public CountryPair(int firstId, int secondId)
            {
                if (firstId < secondId)
                {
                    A = firstId;
                    B = secondId;
                }
                else
                {
                    A = secondId;
                    B = firstId;
                }
            }

            public bool Equals(CountryPair other)
            {
                return A == other.A && B == other.B;
            }

            public override int GetHashCode()
            {
                return (int)math.hash(new int2(A, B));
            }
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
            if (currentTick % WAR_CHECK_INTERVAL != 0)
            {
                return;
            }

            var globalSeed = worldState.GlobalSeed;

            var countries = new NativeList<CountryEntry>(32, Allocator.Temp);
            var countryIds = new NativeHashMap<Entity, int>(32, Allocator.Temp);
            foreach (var (country, entity) in SystemAPI.Query<RefRO<CountryData>>().WithEntityAccess())
            {
                countries.Add(new CountryEntry
                {
                    Entity = entity,
                    CountryId = country.ValueRO.CountryId,
                    Tag = country.ValueRO.Tag,
                    Prestige = country.ValueRO.Prestige
                });
                countryIds.TryAdd(entity, country.ValueRO.CountryId);
            }

            countries.Sort();

            var provinceControllers = new NativeHashMap<int, Entity>(64, Allocator.Temp);
            foreach (var (provinceData, ownership) in SystemAPI.Query<RefRO<ProvinceData>, RefRO<ProvinceOwnership>>())
            {
                provinceControllers[provinceData.ValueRO.ProvinceId] = ownership.ValueRO.Controller;
            }

            var landAdjacency = new NativeParallelMultiHashMap<int, int>(128, Allocator.Temp);
            foreach (var (provinceData, neighbors) in SystemAPI.Query<RefRO<ProvinceData>, DynamicBuffer<ProvinceNeighbor>>())
            {
                var provinceId = provinceData.ValueRO.ProvinceId;
                for (var i = 0; i < neighbors.Length; i++)
                {
                    var neighbor = neighbors[i];
                    if (!neighbor.IsStrait)
                    {
                        landAdjacency.Add(provinceId, neighbor.NeighborProvinceId);
                    }
                }
            }

            var countryLandNeighbors = new NativeParallelMultiHashMap<Entity, Entity>(64, Allocator.Temp);
            BuildCountryLandNeighbors(provinceControllers, landAdjacency, countryLandNeighbors);

            var countriesAtWar = new NativeHashSet<Entity>(16, Allocator.Temp);
            foreach (var war in SystemAPI.Query<RefRO<WarData>>())
            {
                if (!war.ValueRO.IsActive)
                {
                    continue;
                }

                countriesAtWar.Add(war.ValueRO.Attacker);
                countriesAtWar.Add(war.ValueRO.Defender);
            }

            var lastTruceEndTick = new NativeHashMap<CountryPair, int>(32, Allocator.Temp);
            foreach (var war in SystemAPI.Query<RefRO<WarData>>())
            {
                if (war.ValueRO.IsActive || war.ValueRO.EndTick <= 0)
                {
                    continue;
                }

                if (!TryGetCountryId(countryIds, war.ValueRO.Attacker, out var attackerId) ||
                    !TryGetCountryId(countryIds, war.ValueRO.Defender, out var defenderId))
                {
                    continue;
                }

                var pair = new CountryPair(attackerId, defenderId);
                if (lastTruceEndTick.TryGetValue(pair, out var existingEndTick))
                {
                    if (war.ValueRO.EndTick > existingEndTick)
                    {
                        lastTruceEndTick[pair] = war.ValueRO.EndTick;
                    }
                }
                else
                {
                    lastTruceEndTick.Add(pair, war.ValueRO.EndTick);
                }
            }

            var reconquestRights = new NativeParallelMultiHashMap<Entity, Entity>(16, Allocator.Temp);
            foreach (var ownership in SystemAPI.Query<RefRO<ProvinceOwnership>>())
            {
                var core = ownership.ValueRO.Core;
                var owner = ownership.ValueRO.Owner;
                if (core == Entity.Null || owner == Entity.Null || core == owner)
                {
                    continue;
                }

                reconquestRights.Add(core, owner);
            }

            var pendingWars = new NativeList<PendingWar>(4, Allocator.Temp);
            var neighborBuffer = new NativeList<NeighborEntry>(16, Allocator.Temp);

            for (var c = 0; c < countries.Length; c++)
            {
                var attacker = countries[c];
                if (countriesAtWar.Contains(attacker.Entity))
                {
                    continue;
                }

                neighborBuffer.Clear();
                if (countryLandNeighbors.TryGetFirstValue(attacker.Entity, out var neighborEntity, out var iterator))
                {
                    do
                    {
                        if (neighborEntity == attacker.Entity)
                        {
                            continue;
                        }

                        var neighborCountry = FindCountry(countries, neighborEntity);
                        if (neighborCountry.Entity == Entity.Null)
                        {
                            continue;
                        }

                        neighborBuffer.Add(new NeighborEntry
                        {
                            Entity = neighborCountry.Entity,
                            CountryId = neighborCountry.CountryId,
                            Tag = neighborCountry.Tag,
                            Prestige = neighborCountry.Prestige
                        });
                    }
                    while (countryLandNeighbors.TryGetNextValue(out neighborEntity, ref iterator));
                }

                neighborBuffer.Sort();

                Entity reconquestTarget = Entity.Null;
                for (var n = 0; n < neighborBuffer.Length; n++)
                {
                    var target = neighborBuffer[n];
                    var pair = new CountryPair(attacker.CountryId, target.CountryId);
                    if (IsTruceActive(currentTick, pair, lastTruceEndTick))
                    {
                        continue;
                    }

                    if (HasReconquestRight(attacker.Entity, target.Entity, reconquestRights))
                    {
                        reconquestTarget = target.Entity;
                        break;
                    }
                }

                if (reconquestTarget != Entity.Null)
                {
                    pendingWars.Add(new PendingWar
                    {
                        Attacker = attacker.Entity,
                        Defender = reconquestTarget,
                        CasusBelli = CasusBelli.Reconquest
                    });
                    countriesAtWar.Add(attacker.Entity);
                    continue;
                }

                for (var n = 0; n < neighborBuffer.Length; n++)
                {
                    var target = neighborBuffer[n];
                    var pair = new CountryPair(attacker.CountryId, target.CountryId);

                    if (IsTruceActive(currentTick, pair, lastTruceEndTick))
                    {
                        continue;
                    }

                    if (attacker.Prestige < target.Prestige)
                    {
                        continue;
                    }

                    var rng = CreateWarRandom(globalSeed, currentTick, attacker.CountryId);
                    if (rng.NextFloat() >= CONQUEST_DECLARE_CHANCE)
                    {
                        continue;
                    }

                    pendingWars.Add(new PendingWar
                    {
                        Attacker = attacker.Entity,
                        Defender = target.Entity,
                        CasusBelli = CasusBelli.Conquest
                    });
                    countriesAtWar.Add(attacker.Entity);
                    break;
                }
            }

            for (var i = 0; i < pendingWars.Length; i++)
            {
                var pending = pendingWars[i];
                var warEntity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponentData(
                    warEntity,
                    WarData.Create(pending.Attacker, pending.Defender, pending.CasusBelli, currentTick));
            }

            neighborBuffer.Dispose();
            pendingWars.Dispose();
            reconquestRights.Dispose();
            lastTruceEndTick.Dispose();
            countriesAtWar.Dispose();
            countryLandNeighbors.Dispose();
            landAdjacency.Dispose();
            provinceControllers.Dispose();
            countryIds.Dispose();
            countries.Dispose();
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        private static void BuildCountryLandNeighbors(
            NativeHashMap<int, Entity> provinceControllers,
            NativeParallelMultiHashMap<int, int> landAdjacency,
            NativeParallelMultiHashMap<Entity, Entity> countryLandNeighbors)
        {
            var provinceKeys = provinceControllers.GetKeyArray(Allocator.Temp);

            for (var i = 0; i < provinceKeys.Length; i++)
            {
                var provinceId = provinceKeys[i];
                if (!provinceControllers.TryGetValue(provinceId, out var controller) ||
                    controller == Entity.Null)
                {
                    continue;
                }

                if (!landAdjacency.TryGetFirstValue(provinceId, out var neighborId, out var iterator))
                {
                    continue;
                }

                do
                {
                    if (!provinceControllers.TryGetValue(neighborId, out var neighborController) ||
                        neighborController == Entity.Null ||
                        neighborController == controller)
                    {
                        continue;
                    }

                    countryLandNeighbors.Add(controller, neighborController);
                }
                while (landAdjacency.TryGetNextValue(out neighborId, ref iterator));
            }

            provinceKeys.Dispose();
        }

        private static CountryEntry FindCountry(NativeList<CountryEntry> countries, Entity entity)
        {
            for (var i = 0; i < countries.Length; i++)
            {
                if (countries[i].Entity == entity)
                {
                    return countries[i];
                }
            }

            return default;
        }

        private static bool IsTruceActive(
            int currentTick,
            CountryPair pair,
            NativeHashMap<CountryPair, int> lastTruceEndTick)
        {
            if (!lastTruceEndTick.TryGetValue(pair, out var endTick))
            {
                return false;
            }

            return currentTick - endTick < TRUCE_TICKS;
        }

        private static bool HasReconquestRight(
            Entity attacker,
            Entity target,
            NativeParallelMultiHashMap<Entity, Entity> reconquestRights)
        {
            if (!reconquestRights.TryGetFirstValue(attacker, out var owner, out var iterator))
            {
                return false;
            }

            do
            {
                if (owner == target)
                {
                    return true;
                }
            }
            while (reconquestRights.TryGetNextValue(out owner, ref iterator));

            return false;
        }

        private static bool TryGetCountryId(
            NativeHashMap<Entity, int> countryIds,
            Entity country,
            out int countryId)
        {
            return countryIds.TryGetValue(country, out countryId);
        }

        private static Random CreateWarRandom(uint globalSeed, int tick, int countryId)
        {
            var seed = math.hash(new uint3(globalSeed, (uint)tick, (uint)countryId));
            return Random.CreateFromIndex(seed);
        }
    }
}
