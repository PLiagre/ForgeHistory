using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using System;

namespace VictoriaGame.Military
{
    /// <summary>Fort provincial (niveaux 1-6), garnison et état de siège pour un futur SiegeSystem.</summary>
    [Serializable]
    public struct FortData : IComponentData
    {
        public static readonly float BASE_GARRISON_PER_LEVEL = 500f;
        public static readonly float DEFENSE_BONUS_PER_LEVEL = 0.15f;

        /// <summary>Province hébergeant le fort.</summary>
        public int ProvinceId;

        /// <summary>Pays propriétaire du fort.</summary>
        public Entity OwnerCountry;

        /// <summary>Niveau du fort (1 à 6).</summary>
        public int Level;

        /// <summary>Force actuelle de la garnison (0 → MaxGarrisonStrength).</summary>
        public float GarrisonStrength;

        /// <summary>Garnison maximale (Level × BASE_GARRISON_PER_LEVEL).</summary>
        public float MaxGarrisonStrength;

        /// <summary>Bonus de défense en siège (Level × DEFENSE_BONUS_PER_LEVEL).</summary>
        public float DefenseBonus;

        /// <summary>Vrai si un assiégeant ennemi est présent.</summary>
        public bool IsUnderSiege;

        /// <summary>Progression du siège (0.0 début → 1.0 chute du fort).</summary>
        public float SiegeProgress;

        /// <summary>Tick WorldState au début du siège (0 si pas de siège).</summary>
        public int SiegeStartTick;

        public static FortData Create(int provinceId, Entity ownerCountry, int level)
        {
            int clampedLevel = math.clamp(level, 1, 6);
            float maxGarrison = clampedLevel * BASE_GARRISON_PER_LEVEL;
            float defenseBonus = clampedLevel * DEFENSE_BONUS_PER_LEVEL;

            return new FortData
            {
                ProvinceId = provinceId,
                OwnerCountry = ownerCountry,
                Level = clampedLevel,
                GarrisonStrength = maxGarrison,
                MaxGarrisonStrength = maxGarrison,
                DefenseBonus = defenseBonus,
                IsUnderSiege = false,
                SiegeProgress = 0f,
                SiegeStartTick = 0
            };
        }
    }
}
