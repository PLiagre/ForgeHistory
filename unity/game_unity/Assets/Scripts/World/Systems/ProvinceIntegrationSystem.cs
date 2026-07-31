using Unity.Entities;
using Unity.Collections;
using VictoriaGame.Core;

namespace VictoriaGame.World
{
    /// <summary>
    /// Intégration de provinces conquises (Owner != Core) après IntegrationTicks de détention
    /// continue sous le même Owner : pose Core = Owner. Classe statique assumée — pas un ISystem
    /// (enregistrement dans SimulationSystemGroup re-trie le groupe et cassait l'ancrage dip_007).
    /// Appelée en fin de PeaceSystem.OnUpdate via <see cref="IntegrateProvinces"/>.
    /// </summary>
    public static class ProvinceIntegration
    {
        /// <summary>Aucune intégration (ancrage non-régression = dip_007 exact).</summary>
        public const int DisabledIntegrationTicks = 0;

        /// <summary>
        /// Ticks de détention avant intégration. Calibré seed 42195 (dip_008) :
        /// 400 retenu — nonCore 33→18, countries=14, maxProv=11, ratioV@800=72.5%,
        /// debt/army/dip_005 préservés ; 100 casse army↑, 200 army↓.
        /// </summary>
        public const int DefaultIntegrationTicks = 400;

        /// <summary>Mutable pour harnais A/B ; ≤0 = Disabled.</summary>
        public static int IntegrationTicks = DefaultIntegrationTicks;

        /// <summary>Compteur mesure : provinces intégrées (Core ← Owner) ce run.</summary>
        public static int ProvincesIntegrated;

        /// <summary>
        /// Compteur mesure : éligibles en durée mais bloquées car Controller != Owner.
        /// </summary>
        public static int OccupiedIntegrationDeferred;

        /// <summary>
        /// Applique l'intégration. Appelé en fin de PeaceSystem (écritures locales).
        /// </summary>
        public static void IntegrateProvinces(EntityManager em, int currentTick)
        {
            var integrationTicks = IntegrationTicks;
            if (integrationTicks <= DisabledIntegrationTicks)
            {
                return;
            }

            using var query = em.CreateEntityQuery(ComponentType.ReadWrite<ProvinceOwnership>());
            using var entities = query.ToEntityArray(Allocator.Temp);
            var integrated = 0;
            var deferred = 0;

            for (var i = 0; i < entities.Length; i++)
            {
                var ownership = em.GetComponentData<ProvinceOwnership>(entities[i]);
                if (ownership.Owner == Entity.Null)
                {
                    continue;
                }

                if (ownership.Owner == ownership.Core)
                {
                    continue;
                }

                if (currentTick - ownership.OwnerChangedTick < integrationTicks)
                {
                    continue;
                }

                if (ownership.Controller != ownership.Owner)
                {
                    deferred++;
                    continue;
                }

                ownership.Core = ownership.Owner;
                em.SetComponentData(entities[i], ownership);
                integrated++;
            }

            ProvincesIntegrated += integrated;
            OccupiedIntegrationDeferred += deferred;
        }
    }
}
