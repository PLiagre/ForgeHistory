using Unity.Entities;

namespace VictoriaGame.World
{
    /// <summary>
    /// Niveau de développement d'une province.
    /// Ces valeurs sont les niveaux de base (investissement cumulé).
    /// Augmentées par les actions du joueur ou les événements.
    /// Tax      : revenu fiscal de base.
    /// Production : output de base pour le bien provincial.
    /// Manpower   : pool de recrues disponibles par an.
    /// </summary>
    public struct ProvinceDevelopment : IComponentData
    {
        /// <summary>Niveau fiscal [1..30].</summary>
        public int Tax;

        /// <summary>Niveau de production [1..30].</summary>
        public int Production;

        /// <summary>Niveau de manpower [1..30].</summary>
        public int Manpower;
    }
}
