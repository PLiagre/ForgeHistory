using Unity.Entities;

namespace VictoriaGame.World
{
    /// <summary>
    /// Ownership dynamique d'une province.
    /// Owner : pays qui perçoit la taxe.
    /// Core : pays qui revendique légitimement la province (peut différer de Owner).
    /// Controller : pays qui contrôle militairement (peut différer si occupée).
    /// OwnerChangedTick : tick WorldState du dernier changement d'Owner (0 à l'init).
    /// Entity.Null = non assigné / province non réclamée.
    /// </summary>
    public struct ProvinceOwnership : IComponentData
    {
        public Entity Owner;
        public Entity Core;
        public Entity Controller;

        /// <summary>
        /// Tick du dernier transfert d'Owner (PeaceSystem uti possidetis).
        /// Sur ProvinceOwnership plutôt qu'un composant dédié : même cycle de vie,
        /// un seul site d'écriture à l'annexion, zéro lookup supplémentaire.
        /// </summary>
        public int OwnerChangedTick;
    }
}
