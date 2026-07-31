using Unity.Entities;
using System;

namespace VictoriaGame.Population
{
    [Serializable]
    public struct PopulationData : IComponentData
    {
        public int Total;

        /// <summary>
        /// Taux de croissance annuel attendu dans la range [0, 1].
        /// Exemple: 0.012 = 1.2%.
        /// </summary>
        public float GrowthRate;

        /// <summary>
        /// Taux d'alphabetisation attendu dans la range [0, 1].
        /// </summary>
        public float Literacy;

        public float AverageLifeExpectancy;

        /// <summary>
        /// Tension sociale attendue dans la range [0, 1] (0 = calme, 1 = revolution).
        /// </summary>
        public float Militancy;
    }
}
