using Unity.Burst;
using Unity.Entities;
using VictoriaGame.Core;
using VictoriaGame.World;

namespace VictoriaGame.Population
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PopGrowthSystem))]
    public partial struct PopMigrationSystem : ISystem
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

            if (SystemAPI.GetSingleton<WorldState>().IsPaused)
            {
                return;
            }

            Entity bestProvince = Entity.Null;
            var bestScore = int.MinValue;

            foreach (var (devRef, entity) in
                     SystemAPI.Query<RefRO<ProvinceDevelopment>>().WithEntityAccess())
            {
                var dev = devRef.ValueRO;
                var score = dev.Tax + dev.Production + dev.Manpower;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestProvince = entity;
                }
            }

            if (bestProvince == Entity.Null)
            {
                return;
            }

            var job = new PopMigrationJob
            {
                BestProvince = bestProvince
            };
            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        private partial struct PopMigrationJob : IJobEntity
        {
            public Entity BestProvince;

            public void Execute(ref PopData pop)
            {
                if (pop.NeedsSatisfaction < 0.3f && pop.Province != BestProvince)
                {
                    pop.Province = BestProvince;
                }
            }
        }
    }
}
