using Unity.Entities;

namespace VictoriaGame.Core
{
    /// <summary>
    /// Singleton de rythme temps-réel pour le monde JOUÉ uniquement.
    /// Absent du harnais / captures / parité → SimulationTickSystem avance 1 tick/update (inchangé).
    /// Présent (créé par InGameHud) → accumulateur sur DeltaTime × WorldState.SimulationSpeed.
    /// La vitesse unique est <see cref="WorldState.SimulationSpeed"/> (pas de second champ mort).
    /// Pause unique : <see cref="WorldState.IsPaused"/>.
    /// </summary>
    public struct TickControl : IComponentData
    {
        /// <summary>~3.33 ticks/s à SimulationSpeed=1 (1 an ≈ 3.6 s).</summary>
        public const float DefaultSecondsPerTick = 0.3f;

        /// <summary>
        /// Secondes d'horloge (à vitesse ×1) entre deux ticks.
        /// Défaut 0.3 → ~3.33 ticks/s → ~1 an toutes les 3.6 s.
        /// </summary>
        public float SecondsPerTick;

        /// <summary>Temps accumulé (secondes × vitesse) en attente du prochain tick.</summary>
        public float Accumulator;
    }

    /// <summary>
    /// Bootstrap / garde-fous pour le pacing interactif.
    /// </summary>
    public static class TickControlBootstrap
    {
        /// <summary>~3.33 ticks/s à SimulationSpeed=1 (1 an ≈ 3.6 s).</summary>
        public const float DefaultSecondsPerTick = TickControl.DefaultSecondsPerTick;

        /// <summary>
        /// Si true, <see cref="Ensure"/> ne crée pas le singleton (tests PlayMode qui doivent
        /// atteindre t100/t200 en N frames PlayerLoop).
        /// </summary>
        public static bool SuppressInteractivePacing;

        public static void Ensure(EntityManager em)
        {
            // Batchmode (tests automatisés) : DeltaTime trop faible / irrégulier —
            // garder 1 tick/update. Le pacing n'existe qu'en Play Mode interactif éditeur.
            if (SuppressInteractivePacing || UnityEngine.Application.isBatchMode || !em.World.IsCreated)
                return;

            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<TickControl>());
            if (q.CalculateEntityCount() > 0)
                return;

            var entity = em.CreateEntity();
            em.AddComponentData(entity, new TickControl
            {
                SecondsPerTick = TickControl.DefaultSecondsPerTick,
                Accumulator = 0f,
            });
        }

        public static void RemoveIfPresent(Unity.Entities.World world)
        {
            if (world == null || !world.IsCreated)
                return;

            using var q = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<TickControl>());
            if (q.CalculateEntityCount() > 0)
                world.EntityManager.DestroyEntity(q);
        }
    }
}
