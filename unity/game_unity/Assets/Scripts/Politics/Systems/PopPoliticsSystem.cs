using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using VictoriaGame.Core;
using VictoriaGame.Population;

namespace VictoriaGame.Politics
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PopConsumptionSystem))]
    public partial struct PopPoliticsSystem : ISystem
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

            state.Dependency = new PopPoliticsJob().ScheduleParallel(state.Dependency);
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstCompile]
        private partial struct PopPoliticsJob : IJobEntity
        {
            public void Execute(ref PopData pop, ref PopPolitics pol, in PopNeeds needs)
            {
                float sat = pop.NeedsSatisfaction + (needs.FoodNeed * 0f);

                float rad = pol.Radicalism;
                if (sat < 0.4f)
                {
                    rad += 0.002f;
                }

                if (sat > 0.7f)
                {
                    rad -= 0.001f;
                }

                float loy = pol.Loyalty;
                if (rad > 0.6f)
                {
                    loy -= 0.002f;
                }

                if (rad < 0.3f)
                {
                    loy += 0.001f;
                }

                pol.PoliticalPower = PoliticalPowerForType(pop.Type);
                pol.Radicalism = math.clamp(rad, 0f, 1f);
                pol.Loyalty = math.clamp(loy, 0f, 1f);
                pop.PoliticalRadicalism = pol.Radicalism;
            }

            private static float PoliticalPowerForType(PopType t)
            {
                switch (t)
                {
                    case PopType.Noble: return 0.3f;
                    case PopType.Merchant: return 0.25f;
                    case PopType.Intellectual: return 0.25f;
                    case PopType.Clergy: return 0.2f;
                    case PopType.Artisan: return 0.15f;
                    case PopType.Capitalist: return 0.2f;
                    case PopType.Worker: return 0.05f;
                    case PopType.Peasant: return 0.02f;
                    default: return 0.02f;
                }
            }
        }
    }
}
