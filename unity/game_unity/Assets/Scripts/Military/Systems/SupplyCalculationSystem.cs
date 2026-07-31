using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using VictoriaGame.Core;
using VictoriaGame.World;

namespace VictoriaGame.Military
{
    /// <summary>
    /// Hub → armée : sélection du hub le plus proche en portée (BFS terrestre),
    /// mise à jour du stock et de <see cref="ArmyData.SupplyLevel"/>.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TemplateRecruitSystem))]
    [UpdateBefore(typeof(ArmyOrganizationSystem))]
    public partial struct SupplyCalculationSystem : ISystem
    {
        private struct HubSnapshot
        {
            public int ProvinceId;
            public int SupplyRange;
            public float CurrentStock;
            public bool IsActive;
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

            var hubList = new NativeList<HubSnapshot>(Allocator.Temp);
            foreach (var hubRo in SystemAPI.Query<RefRO<SupplyHubData>>())
            {
                var h = hubRo.ValueRO;
                hubList.Add(new HubSnapshot
                {
                    ProvinceId = h.ProvinceId,
                    SupplyRange = h.SupplyRange,
                    CurrentStock = h.CurrentStock,
                    IsActive = h.IsActive
                });
            }

            var hubCount = hubList.Length;
            var hubs = new NativeArray<HubSnapshot>(hubCount, Allocator.Temp);
            for (var i = 0; i < hubCount; i++)
            {
                hubs[i] = hubList[i];
            }

            hubList.Dispose();

            var hubDistances = new NativeArray<NativeHashMap<int, int>>(hubCount, Allocator.Temp);
            var hubParents = new NativeArray<NativeHashMap<int, int>>(hubCount, Allocator.Temp);

            for (var i = 0; i < hubCount; i++)
            {
                var hub = hubs[i];
                if (!hub.IsActive || hub.CurrentStock <= 0f)
                {
                    hubDistances[i] = default;
                    hubParents[i] = default;
                    continue;
                }

                ComputeReachability(
                    hub.ProvinceId,
                    hub.SupplyRange,
                    landAdjacency,
                    out var distances,
                    out var parents,
                    Allocator.Temp);
                hubDistances[i] = distances;
                hubParents[i] = parents;
            }

            var hubStockConsumed = new NativeArray<float>(hubCount, Allocator.Temp);

            foreach (var (armyRw, supplyStateRw, route) in SystemAPI
                         .Query<RefRW<ArmyData>, RefRW<ArmySupplyState>, DynamicBuffer<SupplyRouteData>>())
            {
                route.Clear();

                var bestDist = int.MaxValue;
                var bestHubIdx = -1;

                for (var i = 0; i < hubCount; i++)
                {
                    var hub = hubs[i];
                    if (!hub.IsActive || hub.CurrentStock <= 0f)
                    {
                        continue;
                    }

                    if (!hubDistances[i].IsCreated)
                    {
                        continue;
                    }

                    if (!hubDistances[i].TryGetValue(armyRw.ValueRO.ProvinceId, out var dist))
                    {
                        continue;
                    }

                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestHubIdx = i;
                    }
                }

                if (bestHubIdx >= 0)
                {
                    var hubSnap = hubs[bestHubIdx];
                    var consumed = math.min(1f, hubSnap.CurrentStock);
                    hubStockConsumed[bestHubIdx] += consumed;

                    supplyStateRw.ValueRW.NearestHubProvinceId = hubSnap.ProvinceId;
                    supplyStateRw.ValueRW.DistanceToHub = bestDist;
                    supplyStateRw.ValueRW.SupplyReceived = consumed;
                    supplyStateRw.ValueRW.IsSupplied = true;
                    supplyStateRw.ValueRW.LastSupplyTick = worldState.CurrentTick;

                    FillSupplyRoute(
                        hubSnap.ProvinceId,
                        armyRw.ValueRO.ProvinceId,
                        hubParents[bestHubIdx],
                        route);

                    armyRw.ValueRW.SupplyLevel = math.saturate(consumed);
                }
                else
                {
                    supplyStateRw.ValueRW = ArmySupplyState.CreateUnsupplied();
                    armyRw.ValueRW.SupplyLevel = 0f;
                }
            }

            var drainIdx = 0;
            foreach (var hubRw in SystemAPI.Query<RefRW<SupplyHubData>>())
            {
                var consumed = hubStockConsumed[drainIdx];
                hubRw.ValueRW.CurrentStock = math.max(0f, hubRw.ValueRO.CurrentStock - consumed);
                drainIdx++;
            }

            for (var i = 0; i < hubCount; i++)
            {
                if (hubDistances[i].IsCreated)
                {
                    hubDistances[i].Dispose();
                }

                if (hubParents[i].IsCreated)
                {
                    hubParents[i].Dispose();
                }
            }

            hubDistances.Dispose();
            hubParents.Dispose();
            hubStockConsumed.Dispose();
            hubs.Dispose();
            landAdjacency.Dispose();
        }

        public void OnDestroy(ref SystemState state) { }

        private static void ComputeReachability(
            int hubProvinceId,
            int maxRange,
            NativeParallelMultiHashMap<int, int> landAdjacency,
            out NativeHashMap<int, int> distances,
            out NativeHashMap<int, int> parents,
            Allocator allocator)
        {
            distances = new NativeHashMap<int, int>(64, allocator);
            parents = new NativeHashMap<int, int>(64, allocator);

            var queue = new NativeQueue<int>(allocator);
            var neighborBuffer = new NativeList<int>(8, allocator);

            queue.Enqueue(hubProvinceId);
            distances[hubProvinceId] = 0;
            parents[hubProvinceId] = hubProvinceId;

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var currentDist = distances[current];
                if (currentDist >= maxRange)
                {
                    continue;
                }

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

            queue.Dispose();
            neighborBuffer.Dispose();
        }

        private static void FillSupplyRoute(
            int hubProvinceId,
            int armyProvinceId,
            NativeHashMap<int, int> parents,
            DynamicBuffer<SupplyRouteData> route)
        {
            var path = new NativeList<int>(Allocator.Temp);
            var current = armyProvinceId;

            path.Add(current);
            while (current != hubProvinceId)
            {
                current = parents[current];
                path.Add(current);
            }

            for (var i = path.Length - 1; i >= 0; i--)
            {
                route.Add(new SupplyRouteData { ProvinceId = path[i] });
            }

            path.Dispose();
        }
    }
}
