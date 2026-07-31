using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using VictoriaGame.Core;

namespace VictoriaGame.Politics
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(StabilitySystem))]
    public partial struct GovernmentSystem : ISystem
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

            state.Dependency = new GovernmentUpdateJob().ScheduleParallel(state.Dependency);
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstCompile]
        private partial struct GovernmentUpdateJob : IJobEntity
        {
            public void Execute(ref GovernmentData gov, DynamicBuffer<EnactedLaw> laws)
            {
                float leg = gov.Legitimacy;
                float stab = gov.Stability;

                if (leg > 0.3f)
                {
                    leg -= 0.001f;
                }

                switch (gov.Type)
                {
                    case GovernmentType.Absolute:
                        leg += 0.002f;
                        break;
                    case GovernmentType.Theocratic:
                        leg += 0.001f;
                        break;
                    case GovernmentType.Feudal:
                        break;
                    case GovernmentType.Oligarchic:
                        leg -= 0.001f;
                        break;
                    case GovernmentType.Republic:
                        leg -= 0.002f;
                        break;
                }

                int n = laws.Length;
                const float kPerLaw = 0.00005f;
                leg += n * kPerLaw;
                stab += n * kPerLaw;

                gov.Legitimacy = math.clamp(leg, 0f, 1f);
                gov.Stability = math.clamp(stab, 0f, 1f);
            }
        }
    }
}
