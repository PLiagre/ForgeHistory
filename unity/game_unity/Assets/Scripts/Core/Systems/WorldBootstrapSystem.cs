using Unity.Entities;
using Unity.Burst;

namespace VictoriaGame.Core
{
    /// <summary>
    /// Permet de surcharger la seed globale avant la création du World (tests headless).
    /// Comportement par défaut inchangé : 42195 si aucune surcharge.
    /// </summary>
    public static class WorldBootstrapConfig
    {
        public const uint DefaultGlobalSeed = 42195u;

        public static uint? GlobalSeedOverride;

        public static uint GetGlobalSeed() => GlobalSeedOverride ?? DefaultGlobalSeed;

        public static void ClearOverride() => GlobalSeedOverride = null;
    }

    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct WorldBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            var em = state.EntityManager;
            var worldStateEntity = em.CreateEntity();
            em.AddComponentData(worldStateEntity, new WorldState
            {
                CurrentTick = 0,
                Year = 1400,
                Month = 1,
                SimulationSpeed = 1f,
                IsPaused = false,
                GlobalSeed = WorldBootstrapConfig.GetGlobalSeed(),
            });

            // v1_035 — contrôle joueur + file d'intentions (chemin VISION : UI → intention → simu).
            var playerEntity = em.CreateEntity();
            em.AddComponentData(playerEntity, new PlayerControl
            {
                ControlledCountryId = PlayerControl.DefaultControlledCountryId
            });

            var intentionEntity = em.CreateEntity();
            em.AddComponentData(intentionEntity, new PlayerIntentionQueueTag());
            em.AddBuffer<PlayerIntention>(intentionEntity);
            em.AddComponentData(intentionEntity, new PlayerIntentionReceipt
            {
                Kind = PlayerIntentionKind.None,
                CountryId = -1,
                Value = 0f,
                AppliedTick = -1,
                Accepted = 0,
                Reason = default,
                TargetId = default
            });

            state.Enabled = false;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
        }

        public void OnDestroy(ref SystemState state)
        {
        }
    }
}
