using Unity.Entities;
using UnityEngine;

namespace VictoriaGame.Core
{
    /// <summary>
    /// Authoring MonoBehaviour pour configurer WorldState dans l'éditeur Unity.
    /// </summary>
    public class WorldStateAuthoring : MonoBehaviour
    {
        public int startYear = 1836;
        public int startMonth = 1;
        public float simulationSpeed = 1f;
    }

    public class WorldStateBaker : Baker<WorldStateAuthoring>
    {
        public override void Bake(WorldStateAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new WorldState
            {
                CurrentTick = 0,
                Year = authoring.startYear,
                Month = authoring.startMonth,
                SimulationSpeed = authoring.simulationSpeed,
                IsPaused = false,
            });
        }
    }
}
