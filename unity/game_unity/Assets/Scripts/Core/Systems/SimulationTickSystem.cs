using Unity.Entities;
using Unity.Burst;

namespace VictoriaGame.Core
{
    /// <summary>
    /// Avance le tick de simulation global et met à jour la date.
    /// Sans <see cref="TickControl"/> : 1 tick par update (harnais / parité — bit-identique).
    /// Avec <see cref="TickControl"/> : rythme temps-réel via DeltaTime × WorldState.SimulationSpeed
    /// (au plus 1 tick par update — les autres systèmes du groupe n'en traitent qu'un).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct SimulationTickSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WorldState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var worldState = SystemAPI.GetSingletonRW<WorldState>();
            ref var ws = ref worldState.ValueRW;

            if (ws.IsPaused) return;

            if (SystemAPI.HasSingleton<TickControl>())
            {
                var tickControl = SystemAPI.GetSingletonRW<TickControl>();
                ref var tc = ref tickControl.ValueRW;

                var secondsPerTick = tc.SecondsPerTick;
                if (secondsPerTick <= 0f)
                    secondsPerTick = TickControl.DefaultSecondsPerTick;

                // Source unique de vitesse : WorldState.SimulationSpeed (enfin lu).
                var speed = ws.SimulationSpeed;
                if (speed < 0f)
                    speed = 0f;

                tc.Accumulator += SystemAPI.Time.DeltaTime * speed;
                if (tc.Accumulator < secondsPerTick)
                    return;

                tc.Accumulator -= secondsPerTick;
                // Évite une spirale après un hitch : au plus ~4 ticks d'avance en file.
                var maxCarry = secondsPerTick * 4f;
                if (tc.Accumulator > maxCarry)
                    tc.Accumulator = maxCarry;
            }

            ws.CurrentTick++;

            // 12 ticks = 1 an (simplification Sprint 1)
            if (ws.CurrentTick % 12 == 0)
            {
                ws.Year++;
                ws.Month = 1;
            }
            else
            {
                ws.Month = (ws.CurrentTick % 12) + 1;
            }
        }

        public void OnDestroy(ref SystemState state) { }
    }
}
