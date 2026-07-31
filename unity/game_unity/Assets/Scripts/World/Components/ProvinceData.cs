using Unity.Entities;
using Unity.Collections;
using VictoriaGame.Core;

namespace VictoriaGame.World
{
    /// <summary>
    /// Données statiques d'une province (immuables après init).
    /// Une entité Province = une province sur la carte.
    /// </summary>
    public struct ProvinceData : IComponentData
    {
        /// <summary>Identifiant unique de la province (correspond à l'id dans provinces.json).</summary>
        public int ProvinceId;

        /// <summary>Type de terrain, affecte la production et la défense militaire.</summary>
        public TerrainType Terrain;

        /// <summary>Climat, affecte la production agricole et l'attrition militaire.</summary>
        public ClimateType Climate;

        /// <summary>True si la province a un accès côtier (port possible, blocus naval).</summary>
        public bool IsCoastal;

        /// <summary>Identifiant du nœud de commerce auquel cette province contribue.</summary>
        public int TradeNodeId;

        /// <summary>Culture dominante de la province.</summary>
        public FixedString32Bytes CultureTag;

        /// <summary>Religion dominante de la province.</summary>
        public FixedString32Bytes ReligionTag;

        /// <summary>Bien produit principalement par cette province.</summary>
        public FixedString32Bytes GoodTag;
    }
}
