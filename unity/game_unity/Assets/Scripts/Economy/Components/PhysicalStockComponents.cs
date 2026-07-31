using Unity.Entities;

namespace VictoriaGame.Economy
{
    /// <summary>
    /// Stock physique localisé d'un bien dans une province (couche fantôme v1_020).
    /// Buffer indexé par GoodId — pas de dictionnaire managé.
    /// Quantity en double (v1_025) : l'endowment multi-biens accumule des totaux
    /// hors précision float (~16M entiers exacts) bien avant t3000.
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct ProvinceStock : IBufferElementData
    {
        public int GoodId;
        public double Quantity;
    }

    /// <summary>
    /// Cargaison en transit le long d'une arête terrestre.
    /// La quantité n'est ni chez l'expéditeur ni chez le destinataire — elle compte
    /// dans la conservation.
    /// </summary>
    [InternalBufferCapacity(64)]
    public struct CargoInTransit : IBufferElementData
    {
        public int OriginProvinceId;
        public int DestProvinceId;
        public int GoodId;
        public double Quantity;
        public int TicksRemaining;
    }

    /// <summary>
    /// Ledger cumulatif par bien pour l'invariant de conservation.
    /// stocks + transit == production cumulée − consommation cumulée (± epsilon).
    /// Doubles : les cumuls t3000 dépassent la précision mantissa float (~7 digits).
    /// Prev* : snapshot du tick précédent pour dérive à puissance constante (v1_021).
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct PhysicalLedgerEntry : IBufferElementData
    {
        public int GoodId;
        public double CumulativeProduction;
        public double CumulativeConsumption;
        public double PrevProduction;
        public double PrevConsumption;
        /// <summary>
        /// Snapshot stock+transit du tick précédent en double : les totaux post-endowment
        /// (v1_025) dépassent la précision utile du float (~7 digits) et faussaient le
        /// Δ par tick même quand le ledger double restait exact.
        /// </summary>
        public double PrevStockPlusTransit;
    }

    /// <summary>
    /// Snapshot fantôme de la demande / satisfaction physique locale.
    /// Ne touche JAMAIS PopNeeds ni NeedsSatisfaction.
    /// </summary>
    public struct PhysicalDemandSnapshot : IComponentData
    {
        public float FoodDemand;
        public float ClothDemand;
        public float LuxuryDemand;
        public float FoodSatisfied;
        public float ClothSatisfied;
        public float LuxurySatisfied;

        /// <summary>0.6·food + 0.3·cloth + 0.1·luxury (même pondération que PopConsumption).</summary>
        public float PhysicalSatisfaction;
    }

    /// <summary>
    /// Déficit d'intrant de production par bien (couche fantôme v1_021).
    /// Alimente le gradient de transport au même titre que la demande pop.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct PhysicalInputDeficit : IBufferElementData
    {
        public int GoodId;
        public float Amount;
    }

    /// <summary>
    /// Entrée de recette : pour produire 1 unité de OutputGoodId, il faut QtyPerUnit de InputGoodId.
    /// Plusieurs lignes par output si multi-intrants. Buffer sur le singleton physique.
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct PhysicalRecipeEntry : IBufferElementData
    {
        public int OutputGoodId;
        public int InputGoodId;
        public float QtyPerUnit;
    }

    /// <summary>
    /// Activité productive physique dérivée du terrain/climat (v1_025).
    /// Buffer sur la province — multi-biens, couche physique uniquement.
    /// Capacité native (BaseCapacity) : pas de plafond LastOutput pour les activités
    /// hors site LOD (découplage documenté dans PhysicalProductionSystem).
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct ProvincePhysicalActivity : IBufferElementData
    {
        public int GoodId;

        /// <summary>
        /// Capacité / tick = intensity × DevScore × ProductionScale × typeYield.
        /// Déterminée à l'init, déterministe, jamais tirée au hasard.
        /// </summary>
        public float BaseCapacity;

        /// <summary>Intensité relative terrain×climat après mods (diagnostic).</summary>
        public float RelativeIntensity;
    }

    /// <summary>Tag singleton de la couche économie physique fantôme.</summary>
    public struct PhysicalEconomySingleton : IComponentData
    {
    }

    /// <summary>Paramètres de transport (chargés depuis physical_transport.json).</summary>
    public struct PhysicalTransportConfig : IComponentData
    {
        /// <summary>
        /// Capacité constante par arête (utilisée si <see cref="CapacityPerDevPoint"/> ≤ 0).
        /// Conservée pour les balayages de tests à capacité fixe.
        /// </summary>
        public float EdgeCapacityPerTick;

        /// <summary>
        /// Si &gt; 0 : capacité d'arête = CapacityPerDevPoint × moyenne des scores de
        /// développement des deux provinces (Tax+Production+Manpower)/3.
        /// Émerge de l'infrastructure — forme préférée v1_024.
        /// </summary>
        public float CapacityPerDevPoint;

        /// <summary>Délai minimum (ticks) pour franchir une arête — ≥ 1.</summary>
        public int TransitTicksPerEdge;

        /// <summary>Seuil sous lequel une quantité est traitée comme nulle.</summary>
        public float QuantityEpsilon;
    }

    /// <summary>Métriques du dernier tick (lecture tests / rapport).</summary>
    public struct PhysicalEconomyMetrics : IComponentData
    {
        public int LandIsolatedProvinceCount;
        public int ProvincesInDeficit;
        public float TotalInTransit;
        public float MeanDeliveryDelayTicks;
        public float BlockedProductionShare;
        public float LastTickCpuMs;
        public int CargoCount;

        /// <summary>Output LOD (LastOutput) total du tick.</summary>
        public float LodOutputTotal;

        /// <summary>Output physique réellement déposé ce tick.</summary>
        public float PhysicalOutputTotal;

        /// <summary>(Lod − Physical) / Lod — production manquée faute d'intrants / débouchés.</summary>
        public float MissedInputShare;

        /// <summary>
        /// Part de l'output (post-intrants) retirée par le plafond débouchés ce tick.
        /// 0 si OutletCapIntensity≤0.
        /// </summary>
        public float MissedOutletShare;

        /// <summary>Somme des capacités d'entreposage (toutes provinces × biens touchés) ce tick.</summary>
        public float StorageCapacityTotal;

        /// <summary>Provinces dont au moins un stock atteint sa capacité d'entreposage.</summary>
        public int StorageSaturatedProvinceCount;

        /// <summary>Dérive de conservation max observée sur un tick (puissance constante).</summary>
        public float MaxTickConservationDrift;
    }
}
