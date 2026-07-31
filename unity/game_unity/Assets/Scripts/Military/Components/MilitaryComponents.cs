using Unity.Entities;
using Unity.Collections;
using System;
using VictoriaGame.Core;

namespace VictoriaGame.Military
{
    /// <summary>Commandant de théâtre (général) rattaché à un pays.</summary>
    [Serializable]
    public struct GeneralData : IComponentData
    {
        public FixedString32Bytes Name;
        public Entity Country;
        public int AttackRating;
        public int DefenseRating;
        public int ManeuverRating;
        public int LogisticsRating;
        public int RecruitedTick;
        public bool IsAlive;
    }

    /// <summary>Groupe d'armées sous un général, avec mission stratégique.</summary>
    [Serializable]
    public struct ArmyGroupData : IComponentData
    {
        public FixedString32Bytes Name;
        public Entity Country;
        public Entity CommandingGeneral;
        public ArmyMission Mission;
        public int StrategicProvinceId;
        public float Organization;
        public float Morale;
    }

    /// <summary>Armée déployée sur une province, rattachée à un groupe.</summary>
    [Serializable]
    public struct ArmyData : IComponentData
    {
        public Entity ArmyGroup;
        public Entity Country;
        public int ProvinceId;
        public float Organization;
        public float Morale;
        public float Strength;
        public float SupplyLevel;
        public bool IsEngaged;
    }

    /// <summary>Modèle de recrutement / template de régiment par pays.</summary>
    [Serializable]
    public struct RegimentTemplate : IComponentData
    {
        public Entity Country;
        public int MilTechRequired;
        public int MaxRegiments;
        public float RecruitCostGold;
    }

    /// <summary>Slot de régiment dans le buffer d'une armée.</summary>
    [Serializable]
    public struct RegimentSlot : IBufferElementData
    {
        public RegimentType Type;
        public float Strength;
        public float Organization;
        public float Morale;
        public bool IsRecruiting;
        public int RecruitStartTick;
    }
}
