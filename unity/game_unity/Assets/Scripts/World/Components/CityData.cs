using Unity.Entities;
using Unity.Collections;

namespace VictoriaGame.World
{
    /// <summary>
    /// Statut historique d'une ville à l'amorçage (1400). Pas de seuils magiques
    /// ni de transitions automatiques — les changements viendront de mécanismes
    /// mesurés plus tard.
    /// </summary>
    public enum CityStatus : byte
    {
        /// <summary>Capitale politique (siège du pouvoir local / national).</summary>
        Capital = 0,
        /// <summary>Ville portuaire (commerce maritime, pêche).</summary>
        Port = 1,
        /// <summary>Ville épiscopale / siège religieux.</summary>
        Episcopal = 2,
        /// <summary>Bourg / ville moyenne sans rôle spécial dominant.</summary>
        Borough = 3,
    }

    /// <summary>
    /// Entité ville semée depuis data/cities.json (ADR-002).
    /// Population = PART de la population provinciale (PopData), pas un ajout.
    /// Clé stable : CityId (jamais Entity.Index).
    ///
    /// Accroches futures (non implémentées ici) :
    /// — Bâtiments : entité Building avec CityId (ou ProvinceId) + BuildingType.
    /// — Croissance urbaine : système qui met à jour CityData.Population depuis les pops.
    /// — Nouvelles villes : CreateEntity + ProvinceCity buffer append (clé CityId max+1).
    /// — Quartiers : buffer IBufferElementData sur l'entité City.
    /// </summary>
    public struct CityData : IComponentData
    {
        public int CityId;
        public FixedString64Bytes Name;
        public int ProvinceId;
        public Entity Province;
        /// <summary>Habitants urbains (échelle PopData) — inclus dans les pops provinciales.</summary>
        public int Population;
        public CityStatus Status;
    }
}
