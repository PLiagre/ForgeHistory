using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using VictoriaGame.Core;
using VictoriaGame.Population;

namespace VictoriaGame.Economy
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(BuildingEfficiencySystem))]
    public partial struct ProductionSystem : ISystem
    {
        const float LaborFactorCap = 10f;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WorldState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var worldState = SystemAPI.GetSingleton<WorldState>();
            if (worldState.IsPaused)
            {
                return;
            }

            state.Dependency.Complete();

            var laborByProvince = new NativeHashMap<Entity, float>(64, Allocator.TempJob);

            new AggregateLaborJob
            {
                LaborByProvince = laborByProvince
            }.Run();

            new ProductionJob
            {
                LaborByProvince = laborByProvince,
                LaborFactorCap = LaborFactorCap
            }.Run();

            laborByProvince.Dispose();
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstCompile]
        private partial struct AggregateLaborJob : IJobEntity
        {
            public NativeHashMap<Entity, float> LaborByProvince;

            public void Execute(in PopData pop)
            {
                if (LaborByProvince.TryGetValue(pop.Province, out var current))
                {
                    LaborByProvince[pop.Province] = current + pop.Size;
                }
                else
                {
                    LaborByProvince[pop.Province] = pop.Size;
                }
            }
        }

        [BurstCompile]
        private partial struct ProductionJob : IJobEntity
        {
            [ReadOnly] public NativeHashMap<Entity, float> LaborByProvince;
            public float LaborFactorCap;

            public void Execute(Entity entity, ref ProductionSite site)
            {
                float labor = LaborByProvince.TryGetValue(entity, out var l) ? l : 0f;

                if (site.BaselineLabor <= 0f)
                {
                    site.BaselineLabor = labor;
                }

                float factor = site.BaselineLabor > 0f ? labor / site.BaselineLabor : 1f;
                factor = math.clamp(factor, 0f, LaborFactorCap);

                site.LastOutput = math.max(0f, site.BaseOutput * site.Efficiency * factor);
            }
        }
    }
}
