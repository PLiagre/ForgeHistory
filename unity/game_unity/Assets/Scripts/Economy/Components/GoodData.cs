using Unity.Entities;
using Unity.Collections;
using VictoriaGame.Core;

namespace VictoriaGame.Economy
{
    /// <summary>
    /// Données d'un bien échangeable. Une entité par bien (15 au démarrage).
    /// Créées par GoodInitSystem depuis goods.json.
    /// </summary>
    public struct GoodData : IComponentData
    {
        /// <summary>Identifiant unique (correspond à id dans goods.json).</summary>
        public int GoodId;

        /// <summary>Catégorie du bien.</summary>
        public GoodType Type;

        /// <summary>Nom court pour debug.</summary>
        public FixedString32Bytes Tag;
    }
}
