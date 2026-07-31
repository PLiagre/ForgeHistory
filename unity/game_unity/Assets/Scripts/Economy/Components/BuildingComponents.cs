using Unity.Entities;
using Unity.Collections;
using VictoriaGame.Core;

namespace VictoriaGame.Economy
{
    /// <summary>
    /// Entité bâtiment (achevé ou chantier). Clé stable : BuildingId (jamais Entity.Index).
    /// Accroché à une ville (CityId) dans une province — coords = celles de la ville (v1_037).
    /// </summary>
    public struct BuildingData : IComponentData
    {
        public int BuildingId;
        public BuildingType Type;
        public int CityId;
        public int ProvinceId;
        public int CountryId;
        public int OutputGoodId;
        public float CapacityContribution;
        /// <summary>1 = achevé et produit de la capacité ; 0 = chantier.</summary>
        public byte IsComplete;
    }

    /// <summary>
    /// Chantier en cours : matériaux livrés tick par tick depuis le stock provincial.
    /// Si les intrants manquent, le chantier n'avance pas (pas de complétion magique).
    /// </summary>
    public struct BuildingConstruction : IComponentData
    {
        public int DurationTicks;
        public int ProgressTicks;
        public float WoodTotal;
        public float IronTotal;
        public float WoodDelivered;
        public float IronDelivered;
        public float MoneyPaid;
        /// <summary>1 = bloqué ce tick faute d'intrants.</summary>
        public byte BlockedThisTick;
    }

    /// <summary>Tag singleton catalogue bâtiments + métriques de chantier.</summary>
    public struct BuildingEconomySingleton : IComponentData
    {
    }

    /// <summary>Entrée catalogue (chargée depuis buildings.json).</summary>
    [InternalBufferCapacity(4)]
    public struct BuildingCatalogEntry : IBufferElementData
    {
        public BuildingType Type;
        public float MoneyCost;
        public int DurationTicks;
        public float Capacity;
        public int DefaultOutputGoodId;
        public float WoodCost;
        public float IronCost;
    }

    /// <summary>Métriques cumulées (lecture tests / log v1_038).</summary>
    public struct BuildingEconomyMetrics : IComponentData
    {
        public int SeededCompleted;
        public int ActiveSites;
        public int CompletedThisRun;
        public int BlockedTicks;
        public double WoodConsumed;
        public double IronConsumed;
        public double MoneySpent;
        public float LastTickCpuMs;
    }

    /// <summary>
    /// IA construction. HoldNone (0) = bit-identique au monde v1_038 (aucune intention).
    /// Active = décision émergente ByDeficitSeverity (v1_039) via le buffer d'intentions.
    /// </summary>
    public enum BuildingAiPolicy : byte
    {
        HoldNone = 0,
        Active = 1
    }

    /// <summary>
    /// Réglage réversible IA construction. Défauts compilés = valeur ADOPTÉE après balayage
    /// t3000 (voir building_ai.json). Lock* pour harnais de preuve.
    /// </summary>
    public static class BuildingAiPolicyConfig
    {
        /// <summary>Adopté v1_039 : Active — HoldNone reste le mode zéro bit-identique.</summary>
        public const BuildingAiPolicy DefaultMode = BuildingAiPolicy.Active;

        /// <summary>
        /// Réserve budgétaire [0..0.9] : n'engager un chantier que si
        /// balance − coût ≥ balance × fraction (limite ÉCONOMIQUE, pas un quota).
        /// 0 = dépenser jusqu'au coût catalogue.
        /// </summary>
        public const float DefaultBudgetReserveFraction = 0f;

        public static BuildingAiPolicy Mode = DefaultMode;
        public static float BudgetReserveFraction = DefaultBudgetReserveFraction;

        static bool _harnessLocked;
        static bool _jsonApplied;

        public static bool IsHarnessLocked => _harnessLocked;
        public static bool JsonApplied => _jsonApplied;

        public static void Lock(BuildingAiPolicy mode, float budgetReserveFraction)
        {
            Mode = mode;
            BudgetReserveFraction = ClampReserve(budgetReserveFraction);
            _harnessLocked = true;
            _jsonApplied = true;
        }

        public static void Unlock()
        {
            _harnessLocked = false;
            _jsonApplied = false;
            Mode = DefaultMode;
            BudgetReserveFraction = DefaultBudgetReserveFraction;
        }

        public static void ResetToCompiledDefault()
        {
            Mode = DefaultMode;
            BudgetReserveFraction = DefaultBudgetReserveFraction;
            _harnessLocked = false;
            _jsonApplied = false;
        }

        public static void ApplyLoaded(BuildingAiPolicy mode, float budgetReserveFraction)
        {
            if (_harnessLocked)
                return;
            Mode = mode;
            BudgetReserveFraction = ClampReserve(budgetReserveFraction);
            _jsonApplied = true;
        }

        static float ClampReserve(float v)
        {
            if (v < 0f) return 0f;
            if (v > 0.9f) return 0.9f;
            return v;
        }
    }
}
