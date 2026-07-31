using Unity.Entities;
using System;

namespace VictoriaGame.Military
{
    /// <summary>
    /// Secteur de front pour une guerre active. Une entité FrontSector porte ce composant
    /// et un buffer <see cref="FrontLineState"/> listant les provinces en contact.
    /// </summary>
    [Serializable]
    public struct FrontSectorData : IComponentData
    {
        /// <summary>Guerre à laquelle ce secteur appartient.</summary>
        public Entity War;

        /// <summary>Pays attaquant (recopié depuis WarData pour éviter un lookup).</summary>
        public Entity AttackerCountry;

        /// <summary>Pays défenseur (recopié depuis WarData pour éviter un lookup).</summary>
        public Entity DefenderCountry;

        /// <summary>Progression de l'attaquant ; 0 au contact initial.</summary>
        public float PenetrationDepth;

        /// <summary>False si plus aucune province en contact.</summary>
        public bool IsActive;

        /// <summary>Tick du dernier recalcul par FrontLineSystem.</summary>
        public int LastEvaluatedTick;
    }

    /// <summary>
    /// Province en contact sur le front et son état tactique.
    /// Un seul buffer sur l'entité FrontSector : liste des provinces et pressions associées.
    /// </summary>
    [InternalBufferCapacity(8)]
    [Serializable]
    public struct FrontLineState : IBufferElementData
    {
        /// <summary>Province en contact (identifiant int, cohérent avec ArmyData.ProvinceId).</summary>
        public int ProvinceId;

        /// <summary>True si attaquant et défenseur y ont des forces.</summary>
        public bool IsContested;

        /// <summary>Somme des forces attaquantes sur la province.</summary>
        public float AttackerPressure;

        /// <summary>Somme des forces défensives (armées + garnison du fort).</summary>
        public float DefenderPressure;
    }
}
