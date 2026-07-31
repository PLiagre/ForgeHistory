using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using VictoriaGame.Core;
using VictoriaGame.Politics;
using VictoriaGame.Population;
using VictoriaGame.World;

namespace VictoriaGame.Economy
{
    /// <summary>
    /// Impôt : base (dev.Tax) + revenu de production au prix effectif régional.
    /// priceEff = CurrentPrice × (w · regionalFactor + (1−w) · 1).
    /// LOCALITY_WEIGHT=0 reproduit le prix mondial pur (eco_029 bit-identique).
    /// v1_035 : le taux de production est lu par pays via <see cref="TaxPolicy"/>
    /// (défaut = constante historique 0.00002f → bit-identique).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(MarketPricingSystem))]
    [UpdateBefore(typeof(TreasurySystem))]
    public partial struct TaxSystem : ISystem
    {
        /// <summary>
        /// Ancre historique (pré-v1_035). Préférer <see cref="TaxPolicyLimits.DefaultProductionTaxRate"/>.
        /// Conservée pour compat tests / docs ; TaxSystem lit TaxPolicy par pays.
        /// </summary>
        public const float ProductionTaxRate = TaxPolicyLimits.DefaultProductionTaxRate;

        /// <summary>
        /// Poids du facteur régional dans le mélange prix (0 = mondial pur, 1 = régional pur).
        /// Calibré seed 42195 : w=0.5 fait chuter BYZ army (~490 vs ~596) et debt~750 ;
        /// w=0.25 conserve l'hétérogénéité priceEff sans sortir des bandes eco_029.
        /// Mutable pour les harnais de mesure (A: w=0 vs B: w retenu).
        /// </summary>
        public const float DefaultLocalityWeight = 0.25f;
        public static float LocalityWeight = DefaultLocalityWeight;

        /// <summary>
        /// Rendement fiscal d'une province conquise non intégrée (Owner != Core).
        /// 1.0 = ancrage non-régression (état actuel exact). &lt; 1 freine la boule de neige.
        /// Calibré seed 42195 (dip_006) : 0.30 retenu — maxProvinces 14→8, countries 12→13,
        /// dip_005 intact (ratioV~79 %, annexed=32), debt~751 / bankrupt=4, army~36.5k.
        /// Mutable pour les harnais de mesure — lu hors Burst, passé en champ aux jobs.
        /// </summary>
        public const float DefaultNonCoreYieldFactor = 0.3f;
        public static float NonCoreYieldFactor = DefaultNonCoreYieldFactor;

        /// <summary>Stride clé composite nœud×goodId (marge ≥ max goodId=13).</summary>
        public const int SupplyStride = 16;

        /// <summary>Stride clé composite nœud×GoodType (marge ≥ |GoodType|).</summary>
        public const int DemandStride = 8;

        public const float FactorMin = 0.5f;
        public const float FactorMax = 2.0f;
        public const float ScarcityEpsilon = 1e-3f;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<VictoriaGame.Core.WorldState>();
        }

        // Pas de [BurstCompile] sur OnUpdate : lecture des statics mutables LocalityWeight /
        // NonCoreYieldFactor (harnais de mesure) — BC1040 sous Burst. Les jobs restent Burst.
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.HasSingleton<VictoriaGame.Core.WorldState>())
            {
                return;
            }

            var worldState = SystemAPI.GetSingleton<VictoriaGame.Core.WorldState>();
            if (worldState.IsPaused)
            {
                return;
            }

            state.Dependency.Complete();

            float localityWeight = LocalityWeight;
            float nonCoreYieldFactor = NonCoreYieldFactor;

            var taxMap = new NativeHashMap<Entity, float>(64, Allocator.TempJob);

            var accumulationJob = new TaxAccumulationJob
            {
                TaxMap = taxMap,
                NonCoreYieldFactor = nonCoreYieldFactor
            };
            accumulationJob.Run();

            // Index biens : prix mondial + offre/demande globales + type.
            var goodIdToPrice = new NativeHashMap<int, float>(32, Allocator.TempJob);
            var goodIdToSupply = new NativeHashMap<int, float>(32, Allocator.TempJob);
            var goodIdToDemand = new NativeHashMap<int, float>(32, Allocator.TempJob);
            var goodIdToType = new NativeHashMap<int, GoodType>(32, Allocator.TempJob);
            foreach (var (price, good) in SystemAPI.Query<RefRO<MarketPrice>, RefRO<GoodData>>())
            {
                int id = good.ValueRO.GoodId;
                goodIdToPrice.TryAdd(id, price.ValueRO.CurrentPrice);
                goodIdToSupply.TryAdd(id, price.ValueRO.Supply);
                goodIdToDemand.TryAdd(id, price.ValueRO.Demand);
                goodIdToType.TryAdd(id, good.ValueRO.Type);
            }

            // Agrégations régionales (déterministes, .Run()) — clé composite.
            var regionalSupply = new NativeHashMap<int, float>(128, Allocator.TempJob);
            var regionalDemand = new NativeHashMap<int, float>(64, Allocator.TempJob);

            new AggregateRegionalSupplyJob
            {
                RegionalSupply = regionalSupply
            }.Run();

            var provinceData = SystemAPI.GetComponentLookup<ProvinceData>(true);
            provinceData.Update(ref state);

            new AggregateRegionalDemandJob
            {
                ProvinceDataLookup = provinceData,
                RegionalDemand = regionalDemand
            }.Run();

            var taxPolicyLookup = SystemAPI.GetComponentLookup<TaxPolicy>(true);
            taxPolicyLookup.Update(ref state);
            var lawTaxModsLookup = SystemAPI.GetComponentLookup<LawTaxMods>(true);
            lawTaxModsLookup.Update(ref state);

            var productionTaxJob = new ProductionTaxAccumulationJob
            {
                GoodIdToPrice = goodIdToPrice,
                GoodIdToSupply = goodIdToSupply,
                GoodIdToDemand = goodIdToDemand,
                GoodIdToType = goodIdToType,
                RegionalSupply = regionalSupply,
                RegionalDemand = regionalDemand,
                TaxPolicyLookup = taxPolicyLookup,
                LawTaxModsLookup = lawTaxModsLookup,
                TaxMap = taxMap,
                DefaultProductionTaxRate = ProductionTaxRate,
                MinProductionTaxRate = TaxPolicyLimits.MinProductionTaxRate,
                MaxProductionTaxRate = TaxPolicyLimits.MaxProductionTaxRate,
                LocalityWeight = localityWeight,
                NonCoreYieldFactor = nonCoreYieldFactor,
                FactorMin = FactorMin,
                FactorMax = FactorMax,
                ScarcityEpsilon = ScarcityEpsilon
            };
            productionTaxJob.Run();

            regionalDemand.Dispose();
            regionalSupply.Dispose();
            goodIdToType.Dispose();
            goodIdToDemand.Dispose();
            goodIdToSupply.Dispose();
            goodIdToPrice.Dispose();

            var updateJob = new TaxUpdateJob
            {
                TaxMap = taxMap
            };
            updateJob.Run();

            taxMap.Dispose();
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        public static int SupplyKey(int tradeNodeId, int goodId) =>
            tradeNodeId * SupplyStride + goodId;

        public static int DemandKey(int tradeNodeId, GoodType type) =>
            tradeNodeId * DemandStride + (byte)type;

        /// <summary>
        /// Facteur de rareté locale relative au monde, clampé [FactorMin, FactorMax].
        /// Abondant localement → &lt; 1 ; rare localement → &gt; 1.
        /// </summary>
        public static float ComputeRegionalFactor(
            float regionalDemand,
            float regionalSupply,
            float globalDemand,
            float globalSupply,
            float eps,
            float factorMin,
            float factorMax)
        {
            // Pas de signal de demande (ex. RawMaterial) → facteur neutre.
            if (regionalDemand < eps && globalDemand < eps)
            {
                return 1f;
            }

            float regionalScarcity = regionalDemand / math.max(regionalSupply, eps);
            float globalScarcity = globalDemand / math.max(globalSupply, eps);
            float factor = regionalScarcity / math.max(globalScarcity, eps);
            return math.clamp(factor, factorMin, factorMax);
        }

        public static float ComputeEffectivePrice(
            float globalPrice,
            float regionalFactor,
            float localityWeight)
        {
            return globalPrice * (localityWeight * regionalFactor + (1f - localityWeight) * 1f);
        }

        [BurstCompile]
        private partial struct TaxAccumulationJob : IJobEntity
        {
            public NativeHashMap<Entity, float> TaxMap;
            public float NonCoreYieldFactor;

            public void Execute(in ProvinceOwnership ownership, in ProvinceDevelopment dev)
            {
                if (ownership.Owner == Entity.Null)
                {
                    return;
                }

                float yield = ownership.Owner != ownership.Core ? NonCoreYieldFactor : 1f;
                TaxMap.TryGetValue(ownership.Owner, out float currentTax);
                TaxMap[ownership.Owner] = currentTax + (dev.Tax * 0.1f * yield);
            }
        }

        [BurstCompile]
        private partial struct AggregateRegionalSupplyJob : IJobEntity
        {
            public NativeHashMap<int, float> RegionalSupply;

            public void Execute(in ProductionSite site, in ProvinceData prov)
            {
                int key = SupplyKey(prov.TradeNodeId, site.GoodId);
                if (RegionalSupply.TryGetValue(key, out float cur))
                {
                    RegionalSupply[key] = cur + site.LastOutput;
                }
                else
                {
                    RegionalSupply[key] = site.LastOutput;
                }
            }
        }

        [BurstCompile]
        private partial struct AggregateRegionalDemandJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<ProvinceData> ProvinceDataLookup;
            public NativeHashMap<int, float> RegionalDemand;

            public void Execute(in PopNeeds needs, in PopData pop)
            {
                float scale = pop.Size;
                int nodeId = 0;
                if (ProvinceDataLookup.HasComponent(pop.Province))
                {
                    nodeId = ProvinceDataLookup[pop.Province].TradeNodeId;
                }

                Add(nodeId, GoodType.Food, needs.FoodNeed * scale);
                Add(nodeId, GoodType.Manufactured, needs.ClothNeed * scale);
                Add(nodeId, GoodType.Luxury, needs.LuxuryNeed * scale);
            }

            private void Add(int tradeNodeId, GoodType type, float amount)
            {
                int key = DemandKey(tradeNodeId, type);
                if (RegionalDemand.TryGetValue(key, out float cur))
                {
                    RegionalDemand[key] = cur + amount;
                }
                else
                {
                    RegionalDemand[key] = amount;
                }
            }
        }

        [BurstCompile]
        private partial struct ProductionTaxAccumulationJob : IJobEntity
        {
            [ReadOnly] public NativeHashMap<int, float> GoodIdToPrice;
            [ReadOnly] public NativeHashMap<int, float> GoodIdToSupply;
            [ReadOnly] public NativeHashMap<int, float> GoodIdToDemand;
            [ReadOnly] public NativeHashMap<int, GoodType> GoodIdToType;
            [ReadOnly] public NativeHashMap<int, float> RegionalSupply;
            [ReadOnly] public NativeHashMap<int, float> RegionalDemand;
            [ReadOnly] public ComponentLookup<TaxPolicy> TaxPolicyLookup;
            [ReadOnly] public ComponentLookup<LawTaxMods> LawTaxModsLookup;
            public NativeHashMap<Entity, float> TaxMap;
            public float DefaultProductionTaxRate;
            public float MinProductionTaxRate;
            public float MaxProductionTaxRate;
            public float LocalityWeight;
            public float NonCoreYieldFactor;
            public float FactorMin;
            public float FactorMax;
            public float ScarcityEpsilon;

            public void Execute(
                in ProvinceOwnership ownership,
                in ProductionSite site,
                in ProvinceData province)
            {
                if (ownership.Owner == Entity.Null)
                {
                    return;
                }

                if (!GoodIdToPrice.TryGetValue(site.GoodId, out float globalPrice))
                {
                    return;
                }

                float priceEff = globalPrice;
                if (LocalityWeight > 0f &&
                    GoodIdToType.TryGetValue(site.GoodId, out GoodType type))
                {
                    float globalSupply = GoodIdToSupply.TryGetValue(site.GoodId, out float gs) ? gs : 0f;
                    float globalDemand = GoodIdToDemand.TryGetValue(site.GoodId, out float gd) ? gd : 0f;

                    int supplyKey = SupplyKey(province.TradeNodeId, site.GoodId);
                    int demandKey = DemandKey(province.TradeNodeId, type);
                    float regSupply = RegionalSupply.TryGetValue(supplyKey, out float rs) ? rs : 0f;
                    float regDemand = RegionalDemand.TryGetValue(demandKey, out float rd) ? rd : 0f;

                    float factor = ComputeRegionalFactor(
                        regDemand, regSupply, globalDemand, globalSupply,
                        ScarcityEpsilon, FactorMin, FactorMax);
                    priceEff = ComputeEffectivePrice(globalPrice, factor, LocalityWeight);
                }

                float rate = DefaultProductionTaxRate;
                if (TaxPolicyLookup.HasComponent(ownership.Owner))
                {
                    rate = TaxPolicyLookup[ownership.Owner].ProductionTaxRate;
                }

                // v1_089 : taux effectif = Clamp(rate × (1 + Σ tax_mod)).
                // Σ=0 → ne pas toucher rate (bit-identique au chemin pré-v1_089).
                if (LawTaxModsLookup.HasComponent(ownership.Owner))
                {
                    float lawMod = LawTaxModsLookup[ownership.Owner].TaxModSum;
                    if (lawMod != 0f)
                    {
                        float effective = rate * (1f + lawMod);
                        if (effective < MinProductionTaxRate)
                            effective = MinProductionTaxRate;
                        else if (effective > MaxProductionTaxRate)
                            effective = MaxProductionTaxRate;
                        rate = effective;
                    }
                }

                float yield = ownership.Owner != ownership.Core ? NonCoreYieldFactor : 1f;
                float productionValue = priceEff * site.LastOutput * rate * yield;
                TaxMap.TryGetValue(ownership.Owner, out float currentTax);
                TaxMap[ownership.Owner] = currentTax + productionValue;
            }
        }

        [BurstCompile]
        private partial struct TaxUpdateJob : IJobEntity
        {
            [ReadOnly] public NativeHashMap<Entity, float> TaxMap;

            public void Execute(ref TreasuryData treasury, Entity country)
            {
                treasury.Income = TaxMap.TryGetValue(country, out float taxIncome) ? taxIncome : 0f;
            }
        }
    }
}
