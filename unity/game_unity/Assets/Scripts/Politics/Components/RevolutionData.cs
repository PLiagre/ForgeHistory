using Unity.Entities;

namespace VictoriaGame.Politics
{
    /// <summary>État de la révolution pour une entité-pays (seuil, progression, période active).</summary>
    public struct RevolutionData : IComponentData
    {
        public bool IsRevolutionActive;
        public float RevolutionProgress;
        /// <summary>Seuil de radicalisme moyen (pops) pour déclencher une révolution. Défaut attendu 0,7f.</summary>
        public float RadicalismThreshold;
        public int RevolutionStartTick;
        public int RevolutionEndTick;
    }
}
