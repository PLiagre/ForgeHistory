using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using VictoriaGame.Core;
using VictoriaGame.Population;

namespace VictoriaGame.Politics
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PopPoliticsSystem))]
    public partial struct RevolutionSystem : ISystem
    {
        private const float DefaultRadicalismThreshold = 0.7f;
        private const float RevolutionProgressPerTick = 0.01f;
        private const float CooldownProgressDecay = 0.005f;
        private const float AvgRadicalismCooldown = 0.4f;
        private const float LegitimacyPenaltyOnEnd = 0.3f;
        private const float StabilityPenaltyOnEnd = 0.2f;

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

            var world = SystemAPI.GetSingleton<WorldState>();
            if (world.IsPaused)
            {
                return;
            }

            state.Dependency.Complete();

            var map = new NativeHashMap<Entity, float2>(256, Allocator.TempJob);

            new AggregateJob
            {
                RadicalismByCountry = map
            }.Run();

            state.Dependency = new RevolutionUpdateJob
            {
                RadicalismByCountry = map,
                CurrentTick = world.CurrentTick
            }.ScheduleParallel(state.Dependency);

            state.Dependency.Complete();
            map.Dispose();
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstCompile]
        private partial struct AggregateJob : IJobEntity
        {
            public NativeHashMap<Entity, float2> RadicalismByCountry;

            public void Execute(in PopData pop, in PopPolitics pol)
            {
                if (pop.Country == Entity.Null)
                {
                    return;
                }

                if (RadicalismByCountry.TryGetValue(pop.Country, out var sumCount))
                {
                    RadicalismByCountry[pop.Country] = sumCount + new float2(pol.Radicalism, 1f);
                }
                else
                {
                    RadicalismByCountry[pop.Country] = new float2(pol.Radicalism, 1f);
                }
            }
        }

        [BurstCompile]
        private partial struct RevolutionUpdateJob : IJobEntity
        {
            [ReadOnly] public NativeHashMap<Entity, float2> RadicalismByCountry;
            public int CurrentTick;

            public void Execute(ref GovernmentData gov, ref RevolutionData rev, Entity country)
            {
                if (rev.RadicalismThreshold < 0.0001f)
                {
                    rev.RadicalismThreshold = DefaultRadicalismThreshold;
                }

                float threshold = rev.RadicalismThreshold;
                float avgRad = 0f;

                if (RadicalismByCountry.TryGetValue(country, out var sumCount) && sumCount.y > 0f)
                {
                    avgRad = sumCount.x / sumCount.y;
                }

                if (rev.IsRevolutionActive)
                {
                    float p = rev.RevolutionProgress + RevolutionProgressPerTick;
                    rev.RevolutionProgress = p >= 1f ? 1f : p;
                    if (rev.RevolutionProgress >= 1f)
                    {
                        rev.IsRevolutionActive = false;
                        rev.RevolutionEndTick = CurrentTick;
                        gov.Legitimacy -= LegitimacyPenaltyOnEnd;
                        gov.Stability -= StabilityPenaltyOnEnd;
                        gov.Legitimacy = math.clamp(gov.Legitimacy, 0f, 1f);
                        gov.Stability = math.clamp(gov.Stability, 0f, 1f);
                    }
                }
                else
                {
                    if (avgRad > threshold)
                    {
                        rev.IsRevolutionActive = true;
                        rev.RevolutionStartTick = CurrentTick;
                        rev.RevolutionProgress = 0f;
                    }
                    else if (avgRad < AvgRadicalismCooldown)
                    {
                        rev.RevolutionProgress = math.max(0f, rev.RevolutionProgress - CooldownProgressDecay);
                    }
                }
            }
        }
    }
}
