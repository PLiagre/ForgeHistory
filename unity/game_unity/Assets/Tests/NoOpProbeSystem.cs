using Unity.Entities;
using Unity.Burst;

namespace VictoriaGame.Tests
{
    /// <summary>
    /// Système NO-OP installable uniquement dans le monde de mesure (partie 3 / dip_008).
    /// Ne doit JAMAIS être dans VictoriaGame (sinon le monde joué l'aurait aussi).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct NoOpProbeSystem : ISystem
    {
        public void OnCreate(ref SystemState state) { }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) { }

        public void OnDestroy(ref SystemState state) { }
    }
}
