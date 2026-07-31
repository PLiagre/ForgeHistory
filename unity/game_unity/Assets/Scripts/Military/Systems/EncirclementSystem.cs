using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using VictoriaGame.Core;

namespace VictoriaGame.Military
{
    /// <summary>
    /// Armées non ravitaillées : dégradation progressive d’org/moral/strength puis effondrement si l’encerclement (rupture supply) se prolonge.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(SupplyCalculationSystem))]
    [UpdateBefore(typeof(ArmyOrganizationSystem))]
    public partial struct EncirclementSystem : ISystem
    {
        private const float ENCIRCLEMENT_MORALE_PENALTY_PER_TICK = 2.0f;
        private const float ENCIRCLEMENT_ORG_PENALTY_PER_TICK = 3.0f;
        private const float ENCIRCLEMENT_STRENGTH_PENALTY_PER_TICK = 10.0f;
        private const int COLLAPSE_THRESHOLD_TICKS = 12;

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

            var job = new EncirclementJob
            {
                WorldTick = worldState.CurrentTick
            };

            state.Dependency = job.Schedule(state.Dependency);
        }

        public void OnDestroy(ref SystemState state) { }

        [BurstCompile]
        private partial struct EncirclementJob : IJobEntity
        {
            public int WorldTick;

            public void Execute(ref ArmyData army, ref ArmySupplyState supplyState, DynamicBuffer<RegimentSlot> slots)
            {
                if (supplyState.IsSupplied)
                {
                    return;
                }

                var encirclementTicks = supplyState.LastSupplyTick == 0
                    ? 1
                    : math.max(1, WorldTick - supplyState.LastSupplyTick);

                var penaltyMult = math.min(1f, (float)encirclementTicks / COLLAPSE_THRESHOLD_TICKS);

                if (encirclementTicks >= COLLAPSE_THRESHOLD_TICKS)
                {
                    army.Organization = 0f;
                    army.Morale = 0f;
                    army.Strength = 0f;

                    for (var i = 0; i < slots.Length; i++)
                    {
                        var slot = slots[i];
                        slot.Organization = 0f;
                        slot.Morale = 0f;
                        slot.Strength = 0f;
                        slots[i] = slot;
                    }

                    return;
                }

                var moralePenalty = ENCIRCLEMENT_MORALE_PENALTY_PER_TICK * penaltyMult;
                var orgPenalty = ENCIRCLEMENT_ORG_PENALTY_PER_TICK * penaltyMult;
                var strengthPenalty = ENCIRCLEMENT_STRENGTH_PENALTY_PER_TICK * penaltyMult;

                army.Morale = math.max(0f, army.Morale - moralePenalty);
                army.Organization = math.max(0f, army.Organization - orgPenalty);
                army.Strength = math.max(0f, army.Strength - strengthPenalty);

                for (var i = 0; i < slots.Length; i++)
                {
                    var slot = slots[i];
                    slot.Morale = math.max(0f, slot.Morale - moralePenalty);
                    slot.Organization = math.max(0f, slot.Organization - orgPenalty);
                    slot.Strength = math.max(0f, slot.Strength - strengthPenalty * 0.5f);
                    slots[i] = slot;
                }

                var sum = 0f;
                for (var i = 0; i < slots.Length; i++)
                {
                    sum += slots[i].Strength;
                }

                army.Strength = sum;
            }
        }
    }
}
