using Unity.Entities;
using Unity.Collections;
using System;

namespace VictoriaGame.Politics
{
    public enum LawCategory : byte
    {
        LandRights = 0,
        Taxation = 1,
        Military = 2,
        Trade = 3,
        Church = 4,
        Succession = 5,
    }

    /// <summary>
    /// Définition d'une loi (entité loi). MinGovernmentTypeByte aligné sur <see cref="GovernmentType"/>
    /// (0=Feudal, 1=Absolute, 2=Oligarchic, 3=Theocratic, 4=Republic).
    /// </summary>
    [Serializable]
    public struct LawData : IComponentData
    {
        public FixedString32Bytes LawId;
        public LawCategory Category;
        public byte MinGovernmentTypeByte;
        public float LegitimacyMod;
        public float StabilityMod;
        public float TaxMod;
        public float ManpowerMod;
        public int AvailableFromTick;
    }

    /// <summary>
    /// Loi en vigueur pour un pays (buffer sur l'entité pays), par catégorie.
    /// </summary>
    [Serializable]
    public struct EnactedLaw : IBufferElementData
    {
        public FixedString32Bytes LawId;
        public LawCategory Category;
        public int EnactedTick;
    }

    /// <summary>
    /// Cache de Σ tax_mod des lois en vigueur (v1_089). Mis à jour par EnactLaw.
    /// À 0 → TaxSystem / retrait / lodSat bit-identiques au chemin pré-v1_089.
    /// </summary>
    [Serializable]
    public struct LawTaxMods : IComponentData
    {
        public float TaxModSum;
    }
}
