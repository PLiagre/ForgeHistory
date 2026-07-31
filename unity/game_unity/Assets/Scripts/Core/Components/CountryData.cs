using Unity.Entities;
using Unity.Collections;

namespace VictoriaGame.Core
{
    /// <summary>
    /// Données identitaires d'un pays (immuables en cours de simulation).
    /// </summary>
    public struct CountryData : IComponentData
    {
        public FixedString64Bytes Name;
        public FixedString32Bytes Tag;
        /// <summary>
        /// Identifiant de domaine stable : rang 0-based dans countries.json (ordre de chargement).
        /// Remplace Entity.Index pour graines RNG, tris et clés de dictionnaire (v1_010).
        /// </summary>
        public int CountryId;
        public int Population;
        public float Prestige;
        public float Industrialization;
        /// <summary>
        /// Province capitale (countries.json capital_province_id). Sentinelle -1 si absente/irrésolue.
        /// </summary>
        public int CapitalProvinceId;
    }
}
