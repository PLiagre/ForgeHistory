using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using VictoriaGame.Core;
using VictoriaGame.Economy;

namespace VictoriaGame.Economy
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ProductionSystem))]
    public partial struct MarketAggregationSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WorldState>();
        }

        [BurstCompile]
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

            var supplyMap = new NativeHashMap<int, float>(64, Allocator.TempJob);

            var accumulationJob = new SupplyAccumulationJob
            {
                SupplyMap = supplyMap
            };
            accumulationJob.Run();

            var updateJob = new MarketSupplyUpdateJob
            {
                SupplyMap = supplyMap
            };
            updateJob.Run();

            supplyMap.Dispose();
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstCompile]
        private partial struct SupplyAccumulationJob : IJobEntity
        {
            public NativeHashMap<int, float> SupplyMap;

            public void Execute(in ProductionSite site)
            {
                if (SupplyMap.TryGetValue(site.GoodId, out float current))
                {
                    SupplyMap[site.GoodId] = current + site.LastOutput;
                }
                else
                {
                    SupplyMap[site.GoodId] = site.LastOutput;
                }
            }
        }

        [BurstCompile]
        private partial struct MarketSupplyUpdateJob : IJobEntity
        {
            [ReadOnly] public NativeHashMap<int, float> SupplyMap;

            public void Execute(ref MarketPrice price, in GoodData good)
            {
                price.Supply = SupplyMap.TryGetValue(good.GoodId, out float supply) ? supply : 0f;
            }
        }
    }
}
