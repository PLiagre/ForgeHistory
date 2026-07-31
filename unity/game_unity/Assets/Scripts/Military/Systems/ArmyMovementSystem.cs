using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using VictoriaGame.Core;
using VictoriaGame.World;

namespace VictoriaGame.Military
{
    /// <summary>
    /// Déplace les armées d'un pays en guerre d'un saut terrestre par tick vers
    /// la province ennemie la plus proche (BFS déterministe sur ProvinceNeighbor).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(WarDeclarationSystem))]
    [UpdateAfter(typeof(ArmyOrganizationSystem))]
    [UpdateBefore(typeof(FrontLineSystem))]
    public partial struct ArmyMovementSystem : ISystem
    {
        private struct ArmyMovePlan : System.IComparable<ArmyMovePlan>
        {
            public Entity Entity;
            public Entity Country;
            public int CountryId;
            public int CurrentProvinceId;
            public int NextProvinceId;
            public bool WillMove;
            public bool IsRetreat;
            public bool Disengage;

            public int CompareTo(ArmyMovePlan other)
            {
                return DomainKeys.CompareArmyKeys(
                    CountryId, CurrentProvinceId,
                    other.CountryId, other.CurrentProvinceId);
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

            var provinceControllers = new NativeHashMap<int, Entity>(64, Allocator.Temp);
            foreach (var (provinceData, ownership) in SystemAPI.Query<RefRO<ProvinceData>, RefRO<ProvinceOwnership>>())
            {
                provinceControllers[provinceData.ValueRO.ProvinceId] = ownership.ValueRO.Controller;
            }

            var countryIds = new NativeHashMap<Entity, int>(32, Allocator.Temp);
            foreach (var (country, entity) in SystemAPI.Query<RefRO<CountryData>>().WithEntityAccess())
            {
                countryIds.TryAdd(entity, country.ValueRO.CountryId);
            }

            var countryToEnemy = new NativeHashMap<Entity, Entity>(32, Allocator.Temp);
            foreach (var war in SystemAPI.Query<RefRO<WarData>>())
            {
                if (!war.ValueRO.IsActive)
                {
                    continue;
                }

                RegisterWarEnemy(countryToEnemy, countryIds, war.ValueRO.Attacker, war.ValueRO.Defender);
                RegisterWarEnemy(countryToEnemy, countryIds, war.ValueRO.Defender, war.ValueRO.Attacker);
            }

            var armyGroupLookup = SystemAPI.GetComponentLookup<ArmyGroupData>(true);
            armyGroupLookup.Update(ref state);

            var movePlans = new NativeList<ArmyMovePlan>(32, Allocator.Temp);
            foreach (var (army, entity) in SystemAPI.Query<RefRO<ArmyData>>().WithEntityAccess())
            {
                var currentProvinceId = army.ValueRO.ProvinceId;
                var nextProvinceId = currentProvinceId;
                var willMove = false;
                var isRetreat = false;
                var disengage = false;
                countryIds.TryGetValue(army.ValueRO.Country, out var armyCountryId);

                if (army.ValueRO.Strength > 0f &&
                    countryToEnemy.TryGetValue(army.ValueRO.Country, out var enemyCountry))
                {
                    if (armyGroupLookup.HasComponent(army.ValueRO.ArmyGroup))
                    {
                        var group = armyGroupLookup[army.ValueRO.ArmyGroup];
                        if (group.Mission == ArmyMission.Regroup)
                        {
                            isRetreat = true;
                            disengage = true;
                            var fallbackProvinceId = group.StrategicProvinceId;

                            if (currentProvinceId != fallbackProvinceId &&
                                TryFindFirstStepTowardProvince(
                                    currentProvinceId,
                                    fallbackProvinceId,
                                    landAdjacency,
                                    out var retreatStepProvinceId))
                            {
                                nextProvinceId = retreatStepProvinceId;
                                willMove = nextProvinceId != currentProvinceId;
                            }
                        }
                        else if (!army.ValueRO.IsEngaged &&
                                 provinceControllers.TryGetValue(currentProvinceId, out var controller) &&
                                 controller != enemyCountry &&
                                 TryFindFirstStepTowardClosestEnemyProvince(
                                     currentProvinceId,
                                     enemyCountry,
                                     landAdjacency,
                                     provinceControllers,
                                     out var stepProvinceId))
                        {
                            nextProvinceId = stepProvinceId;
                            willMove = nextProvinceId != currentProvinceId;
                        }
                    }
                }

                movePlans.Add(new ArmyMovePlan
                {
                    Entity = entity,
                    Country = army.ValueRO.Country,
                    CountryId = armyCountryId,
                    CurrentProvinceId = currentProvinceId,
                    NextProvinceId = nextProvinceId,
                    WillMove = willMove,
                    IsRetreat = isRetreat,
                    Disengage = disengage
                });
            }

            movePlans.Sort();

            for (var i = 0; i < movePlans.Length; i++)
            {
                var plan = movePlans[i];
                if (!plan.WillMove || plan.IsRetreat)
                {
                    continue;
                }

                if (!countryToEnemy.TryGetValue(plan.Country, out var enemyCountry))
                {
                    continue;
                }

                var incomingEnemy = false;
                for (var j = 0; j < movePlans.Length; j++)
                {
                    var other = movePlans[j];
                    if (!other.WillMove || other.Entity == plan.Entity)
                    {
                        continue;
                    }

                    if (!countryToEnemy.TryGetValue(other.Country, out var otherEnemy) ||
                        otherEnemy != plan.Country)
                    {
                        continue;
                    }

                    if (other.NextProvinceId == plan.CurrentProvinceId)
                    {
                        incomingEnemy = true;
                        break;
                    }
                }

                if (incomingEnemy)
                {
                    movePlans[i] = new ArmyMovePlan
                    {
                        Entity = plan.Entity,
                        Country = plan.Country,
                        CountryId = plan.CountryId,
                        CurrentProvinceId = plan.CurrentProvinceId,
                        NextProvinceId = plan.CurrentProvinceId,
                        WillMove = false
                    };
                }
            }

            foreach (var (armyRw, entity) in SystemAPI.Query<RefRW<ArmyData>>().WithEntityAccess())
            {
                for (var i = 0; i < movePlans.Length; i++)
                {
                    if (movePlans[i].Entity != entity)
                    {
                        continue;
                    }

                    if (movePlans[i].WillMove)
                    {
                        armyRw.ValueRW.ProvinceId = movePlans[i].NextProvinceId;
                    }

                    if (movePlans[i].Disengage)
                    {
                        armyRw.ValueRW.IsEngaged = false;
                    }

                    break;
                }
            }

            movePlans.Dispose();
            countryToEnemy.Dispose();
            countryIds.Dispose();
            provinceControllers.Dispose();
            landAdjacency.Dispose();
        }

        public void OnDestroy(ref SystemState state) { }

        private static void RegisterWarEnemy(
            NativeHashMap<Entity, Entity> countryToEnemy,
            NativeHashMap<Entity, int> countryIds,
            Entity country,
            Entity enemy)
        {
            if (countryToEnemy.TryGetValue(country, out var existing))
            {
                countryIds.TryGetValue(enemy, out var enemyId);
                countryIds.TryGetValue(existing, out var existingId);
                if (enemyId < existingId)
                {
                    countryToEnemy[country] = enemy;
                }
            }
            else
            {
                countryToEnemy[country] = enemy;
            }
        }

        private static bool TryFindFirstStepTowardProvince(
            int startProvinceId,
            int targetProvinceId,
            NativeParallelMultiHashMap<int, int> landAdjacency,
            out int firstStepProvinceId)
        {
            firstStepProvinceId = -1;

            if (startProvinceId == targetProvinceId)
            {
                return false;
            }

            var distances = new NativeHashMap<int, int>(64, Allocator.Temp);
            var parents = new NativeHashMap<int, int>(64, Allocator.Temp);
            var queue = new NativeQueue<int>(Allocator.Temp);
            var neighborBuffer = new NativeList<int>(8, Allocator.Temp);

            queue.Enqueue(startProvinceId);
            distances[startProvinceId] = 0;
            parents[startProvinceId] = startProvinceId;

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var currentDist = distances[current];

                neighborBuffer.Clear();
                if (landAdjacency.TryGetFirstValue(current, out var neighborId, out var iterator))
                {
                    do
                    {
                        neighborBuffer.Add(neighborId);
                    }
                    while (landAdjacency.TryGetNextValue(out neighborId, ref iterator));
                }

                neighborBuffer.Sort();

                for (var i = 0; i < neighborBuffer.Length; i++)
                {
                    var neighbor = neighborBuffer[i];
                    if (distances.ContainsKey(neighbor))
                    {
                        continue;
                    }

                    distances[neighbor] = currentDist + 1;
                    parents[neighbor] = current;
                    queue.Enqueue(neighbor);
                }
            }

            if (!distances.ContainsKey(targetProvinceId))
            {
                queue.Dispose();
                neighborBuffer.Dispose();
                distances.Dispose();
                parents.Dispose();
                return false;
            }

            var step = targetProvinceId;
            while (parents[step] != startProvinceId)
            {
                step = parents[step];
            }

            firstStepProvinceId = step;

            queue.Dispose();
            neighborBuffer.Dispose();
            distances.Dispose();
            parents.Dispose();
            return true;
        }

        private static bool TryFindFirstStepTowardClosestEnemyProvince(
            int startProvinceId,
            Entity enemyCountry,
            NativeParallelMultiHashMap<int, int> landAdjacency,
            NativeHashMap<int, Entity> provinceControllers,
            out int firstStepProvinceId)
        {
            firstStepProvinceId = -1;

            var distances = new NativeHashMap<int, int>(64, Allocator.Temp);
            var parents = new NativeHashMap<int, int>(64, Allocator.Temp);
            var queue = new NativeQueue<int>(Allocator.Temp);
            var neighborBuffer = new NativeList<int>(8, Allocator.Temp);

            queue.Enqueue(startProvinceId);
            distances[startProvinceId] = 0;
            parents[startProvinceId] = startProvinceId;

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var currentDist = distances[current];

                neighborBuffer.Clear();
                if (landAdjacency.TryGetFirstValue(current, out var neighborId, out var iterator))
                {
                    do
                    {
                        neighborBuffer.Add(neighborId);
                    }
                    while (landAdjacency.TryGetNextValue(out neighborId, ref iterator));
                }

                neighborBuffer.Sort();

                for (var i = 0; i < neighborBuffer.Length; i++)
                {
                    var neighbor = neighborBuffer[i];
                    if (distances.ContainsKey(neighbor))
                    {
                        continue;
                    }

                    distances[neighbor] = currentDist + 1;
                    parents[neighbor] = current;
                    queue.Enqueue(neighbor);
                }
            }

            var bestTargetDist = int.MaxValue;
            var bestTargetId = int.MaxValue;

            var provinceKeys = distances.GetKeyArray(Allocator.Temp);
            for (var i = 0; i < provinceKeys.Length; i++)
            {
                var provinceId = provinceKeys[i];
                if (provinceId == startProvinceId)
                {
                    continue;
                }

                if (!provinceControllers.TryGetValue(provinceId, out var controller) ||
                    controller != enemyCountry)
                {
                    continue;
                }

                var dist = distances[provinceId];
                if (dist < bestTargetDist || (dist == bestTargetDist && provinceId < bestTargetId))
                {
                    bestTargetDist = dist;
                    bestTargetId = provinceId;
                }
            }

            provinceKeys.Dispose();

            if (bestTargetDist == int.MaxValue)
            {
                queue.Dispose();
                neighborBuffer.Dispose();
                distances.Dispose();
                parents.Dispose();
                return false;
            }

            var step = bestTargetId;
            while (parents[step] != startProvinceId)
            {
                step = parents[step];
            }

            firstStepProvinceId = step;

            queue.Dispose();
            neighborBuffer.Dispose();
            distances.Dispose();
            parents.Dispose();
            return true;
        }
    }
}
