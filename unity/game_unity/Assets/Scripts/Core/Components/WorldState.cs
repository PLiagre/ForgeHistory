using System;
using Unity.Entities;

namespace VictoriaGame.Core
{
    /// <summary>
    /// Singleton: état global de la simulation.
    /// Créé par WorldBootstrapSystem au démarrage, jamais recréé.
    /// </summary>
    public struct WorldState : IComponentData
    {
        /// <summary>Tick courant (1 tick = 1 mois de jeu).</summary>
        public int CurrentTick;

        /// <summary>Année courante [1400..1900].</summary>
        public int Year;

        /// <summary>Mois courant [1..12].</summary>
        public int Month;

        /// <summary>
        /// Multiplicateur de vitesse de simulation (1 = temps normal).
        /// Lu par SimulationTickSystem lorsque le singleton TickControl est présent
        /// (monde joué interactif). Absent TickControl → 1 tick/update (harnais).
        /// </summary>
        public float SimulationSpeed;

        /// <summary>Si true, le tick ne s'incrémente pas.</summary>
        public bool IsPaused;

        /// <summary>
        /// Seed de déterminisme global.
        /// Tous les systèmes utilisant l'aléatoire doivent partir de cette seed.
        /// Ne jamais utiliser Random.Range ou System.Random directement.
        /// </summary>
        public uint GlobalSeed;
    }
}
