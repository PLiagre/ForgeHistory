using Unity.Entities;
using System;
using VictoriaGame.Core;

namespace VictoriaGame.Navy
{
    /// <summary>Zone maritime (océan ou mer intérieure) avec contrôle naval.</summary>
    [Serializable]
    public struct SeaZoneData : IComponentData
    {
        /// <summary>Identifiant de la zone (cohérent avec sea_zones.json).</summary>
        public int ZoneId;

        /// <summary>Si true, les Galley ne peuvent pas opérer dans cette zone.</summary>
        public bool IsOcean;

        /// <summary>Pays dominant la zone ; Entity.Null si aucun contrôle.</summary>
        public Entity Controller;

        /// <summary>Force du contrôle (0.0 → 1.0).</summary>
        public float ControlStrength;
    }

    /// <summary>Zone maritime adjacente, pour le déplacement des flottes.</summary>
    [InternalBufferCapacity(8)]
    [Serializable]
    public struct SeaZoneNeighbor : IBufferElementData
    {
        public int NeighborZoneId;
    }

    /// <summary>Province côtière bordant la zone (blocus, débarquement Sprint 16).</summary>
    [InternalBufferCapacity(8)]
    [Serializable]
    public struct SeaZoneCoast : IBufferElementData
    {
        public int ProvinceId;
    }

    /// <summary>Flotte d'un pays déployée dans une zone maritime.</summary>
    [Serializable]
    public struct NavyData : IComponentData
    {
        public Entity Country;
        public int SeaZoneId;
        public NavyMission Mission;
        public float NavalMorale;
        public float NavalStrength;

        /// <summary>Initialise une flotte avec moral plein et force nulle (escadrons à remplir).</summary>
        public static NavyData Create(Entity country, int seaZoneId, NavyMission mission)
        {
            return new NavyData
            {
                Country = country,
                SeaZoneId = seaZoneId,
                Mission = mission,
                NavalMorale = 1f,
                NavalStrength = 0f
            };
        }
    }

    /// <summary>Escadron de navires d'un type donné au sein d'une flotte.</summary>
    [InternalBufferCapacity(9)]
    [Serializable]
    public struct ShipSquadron : IBufferElementData
    {
        public ShipType Type;
        public int Count;
        public float Condition;
    }

    /// <summary>Présence navale d'un pays dans une zone (contrôle et suprématie).</summary>
    [Serializable]
    public struct NavalControl : IComponentData
    {
        public int SeaZoneId;
        public Entity Country;
        public float PresenceStrength;
        public bool IsSupremacy;
    }
}
