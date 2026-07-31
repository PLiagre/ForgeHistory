using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using VictoriaGame.Core;
using VictoriaGame.World;

namespace VictoriaGame.Economy
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(ProductionSystem))]
    public partial struct BuildingEfficiencySystem : ISystem
    {
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

            var job = new EfficiencyJob();
            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstCompile]
        private partial struct EfficiencyJob : IJobEntity
        {
            public void Execute(in ProvinceData pData, in ProvinceDevelopment dev, ref ProductionSite site)
            {
                site.Efficiency = math.clamp(0.5f + dev.Production * 0.05f, 0.1f, 2.0f);
            }
        }
    }
}
