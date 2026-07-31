using Unity.Entities;
using Unity.Collections;
using System;

namespace VictoriaGame.Military
{
    /// <summary>
    /// Hub de ravitaillement sur une province (capacité, stock, portée).
    /// Lu par SupplyCalculationSystem et EncirclementSystem.
    /// </summary>
    [Serializable]
    public struct SupplyHubData : IComponentData
    {
        public int ProvinceId;
        public float MaxCapacity;
        public float CurrentStock;
        public int SupplyRange;
        public bool IsActive;
    }

    /// <summary>
    /// Détail du ravitaillement d'une armée pour le tick courant.
    /// <see cref="ArmyData.SupplyLevel"/> reste la valeur opérative pour ArmyOrganizationSystem.
    /// Sentinels : <see cref="NearestHubProvinceId"/> = -1 (aucun hub), <see cref="DistanceToHub"/> = int.MaxValue (hors portée).
    /// </summary>
    [Serializable]
    public struct ArmySupplyState : IComponentData
    {
        public int NearestHubProvinceId;
        public int DistanceToHub;
        public float SupplyReceived;
        public bool IsSupplied;
        public int LastSupplyTick;

        /// <summary>Valeur initiale cohérente avec les sentinelles (non ravitaillé / hors portée).</summary>
        public static ArmySupplyState CreateUnsupplied()
        {
            return new ArmySupplyState
            {
                NearestHubProvinceId = -1,
                DistanceToHub = int.MaxValue,
                SupplyReceived = 0f,
                IsSupplied = false,
                LastSupplyTick = 0
            };
        }
    }

    /// <summary>
    /// Un hop de province sur la route hub → armée. Buffer rempli par SupplyCalculationSystem.
    /// </summary>
    [Serializable]
    public struct SupplyRouteData : IBufferElementData
    {
        public int ProvinceId;
    }
}
