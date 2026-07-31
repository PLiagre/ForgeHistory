using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using VictoriaGame.Core;
using VictoriaGame.Economy;
using VictoriaGame.Politics;
using VictoriaGame.World;

namespace VictoriaGame.Population
{
    /// <summary>
    /// Consommation des pops : satisfaction régionale par TradeNodeId, prix marchés globaux.
    /// ratioEffectif = LOCALITY_WEIGHT * ratioRégional + (1 − LOCALITY_WEIGHT) * ratioGlobal.
    /// LOCALITY_WEIGHT=0 reproduit la satisfaction mondiale unique (eco_027 bit-identique).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ProductionSystem))]
    [UpdateBefore(typeof(MarketAggregationSystem))]
    public partial struct PopConsumptionSystem : ISystem
    {
        /// <summary>
        /// Poids du marché régional dans le mélange (0 = mondial pur, 1 = autarcie pure).
        /// Calibré seed 42195 : w=0.5 rend l'hétérogénéité visible sans effondrer le node3.
        /// Mutable pour les harnais de mesure (A: w=0 vs B: w retenu).
        /// </summary>
        public const float DefaultLocalityWeight = 0.5f;
        public static float LocalityWeight = DefaultLocalityWeight;

        /// <summary>Stride de la clé composite nœud*GoodTypeStride+(byte)type (marge ≥ |GoodType|).</summary>
        public const int GoodTypeStride = 8;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WorldState>();
        }

        // Pas de [BurstCompile] : lecture de LocalityWeight (static mutable) hors Burst (BC1040).
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.HasSingleton<WorldState>())
            {
                return;
            }

            if (SystemAPI.GetSingleton<WorldState>().IsPaused)
            {
                return;
            }

            state.Dependency.Complete();

            var goodIdToType = new NativeHashMap<int, GoodType>(64, Allocator.TempJob);
            foreach (var good in SystemAPI.Query<RefRO<GoodData>>())
            {
                goodIdToType.TryAdd(good.ValueRO.GoodId, good.ValueRO.Type);
            }

            var provinceData = SystemAPI.GetComponentLookup<ProvinceData>(true);
            provinceData.Update(ref state);

            // Globaux (clé = (byte)GoodType) — alimentent MarketPrice + terme (1−w).
            var totalDemand = new NativeHashMap<byte, float>(8, Allocator.TempJob);
            var supplyByType = new NativeHashMap<byte, float>(8, Allocator.TempJob);

            // Régionaux (clé = TradeNodeId * GoodTypeStride + (byte)type).
            var regionalDemand = new NativeHashMap<int, float>(64, Allocator.TempJob);
            var regionalSupply = new NativeHashMap<int, float>(64, Allocator.TempJob);

            new AggregateDemandJob
            {
                ProvinceDataLookup = provinceData,
                TotalDemand = totalDemand,
                RegionalDemand = regionalDemand
            }.Run();

            // Retrait fiscal abstrait (v1_075) : à cAbs≤0 chemin Burst inchangé
            // (bit-identique) ; à cAbs>0 l'offre LOD est réduite sans toucher LastOutput.
            var abstractCoeff = TaxPhysicalWithdrawalSystem.AbstractWithdrawalCoefficient;
            if (abstractCoeff <= 0f)
            {
                TaxPhysicalWithdrawalSystem.RecordAbstractTick(0.0, 0.0);
                new AccumulateProductionSupplyJob
                {
                    GoodIdToType = goodIdToType,
                    SupplyByType = supplyByType,
                    RegionalSupply = regionalSupply
                }.Run();
            }
            else
            {
                AccumulateSupplyWithAbstractTax(
                    state.EntityManager, goodIdToType, supplyByType, regionalSupply, abstractCoeff);
            }

            // Isolation monétaire : MarketPrice.Demand reste GLOBAL.
            new WriteDemandJob
            {
                TotalDemand = totalDemand
            }.Run();

            float ratioFoodGlobal = ComputeRatio(totalDemand, supplyByType, GoodType.Food);
            float ratioClothGlobal = ComputeRatio(totalDemand, supplyByType, GoodType.Manufactured);
            float ratioLuxuryGlobal = ComputeRatio(totalDemand, supplyByType, GoodType.Luxury);

            float w = LocalityWeight;
            float oneMinusW = 1f - w;

            // Ratios effectifs par (nœud, type) : w·régional + (1−w)·global.
            var effectiveRatios = new NativeHashMap<int, float>(64, Allocator.TempJob);
            BuildEffectiveRatios(
                regionalDemand, regionalSupply,
                ratioFoodGlobal, ratioClothGlobal, ratioLuxuryGlobal,
                w, oneMinusW, effectiveRatios);

            new UpdatePopSatisfactionJob
            {
                ProvinceDataLookup = provinceData,
                EffectiveRatios = effectiveRatios,
                RatioFoodGlobal = ratioFoodGlobal,
                RatioClothGlobal = ratioClothGlobal,
                RatioLuxuryGlobal = ratioLuxuryGlobal
            }.Run();

            // Isolation monétaire : MarketPrice.Supply consommé avec ratios/demandes GLOBAUX.
            new ReduceSupplyJob
            {
                TotalDemand = totalDemand,
                SupplyByType = supplyByType,
                RatioFood = ratioFoodGlobal,
                RatioCloth = ratioClothGlobal,
                RatioLuxury = ratioLuxuryGlobal
            }.Run();

            effectiveRatios.Dispose();
            regionalSupply.Dispose();
            regionalDemand.Dispose();
            supplyByType.Dispose();
            totalDemand.Dispose();
            goodIdToType.Dispose();
        }

        public static int CompositeKey(int tradeNodeId, GoodType type) =>
            tradeNodeId * GoodTypeStride + (byte)type;

        /// <summary>
        /// Accumulation LOD avec retrait fiscal abstrait. Parcours déterministe
        /// ProvinceId → GoodId (jamais Entity.Index). Conservation : requested =
        /// withdrawn (plafond = LastOutput).
        /// </summary>
        static void AccumulateSupplyWithAbstractTax(
            EntityManager em,
            NativeHashMap<int, GoodType> goodIdToType,
            NativeHashMap<byte, float> supplyByType,
            NativeHashMap<int, float> regionalSupply,
            float abstractCoefficient)
        {
            var defaultRate = TaxSystem.ProductionTaxRate;
            var nonCoreYield = TaxSystem.NonCoreYieldFactor;

            var rows = new NativeList<LodSupplyRow>(64, Allocator.TempJob);
            using var q = em.CreateEntityQuery(
                ComponentType.ReadOnly<ProvinceOwnership>(),
                ComponentType.ReadOnly<ProductionSite>(),
                ComponentType.ReadOnly<ProvinceData>());
            using var entities = q.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                var e = entities[i];
                var site = em.GetComponentData<ProductionSite>(e);
                if (site.GoodId <= 0 || site.LastOutput <= 0f)
                {
                    continue;
                }

                if (!goodIdToType.TryGetValue(site.GoodId, out var type))
                {
                    continue;
                }

                var ownership = em.GetComponentData<ProvinceOwnership>(e);
                var province = em.GetComponentData<ProvinceData>(e);
                var rate = 0f;
                var yield = 1f;
                if (ownership.Owner != Entity.Null)
                {
                    rate = defaultRate;
                    if (em.HasComponent<TaxPolicy>(ownership.Owner))
                    {
                        rate = em.GetComponentData<TaxPolicy>(ownership.Owner).ProductionTaxRate;
                    }

                    if (em.HasComponent<LawTaxMods>(ownership.Owner))
                    {
                        var lawMod = em.GetComponentData<LawTaxMods>(ownership.Owner).TaxModSum;
                        if (lawMod != 0f)
                            rate = LawTaxEffect.EffectiveProductionTaxRate(rate, lawMod);
                    }

                    yield = ownership.Owner != ownership.Core ? nonCoreYield : 1f;
                }

                var raw = site.LastOutput;
                var withheld = TaxPhysicalWithdrawalSystem.AbstractWithheldAmount(raw, rate, yield);

                rows.Add(new LodSupplyRow
                {
                    ProvinceId = province.ProvinceId,
                    GoodId = site.GoodId,
                    TradeNodeId = province.TradeNodeId,
                    Type = type,
                    Effective = raw - withheld,
                    Withheld = withheld
                });
            }

            rows.Sort(new LodSupplyRowComparer());

            double requestedTotal = 0.0;
            double withdrawnTotal = 0.0;
            for (var i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                requestedTotal += row.Withheld;
                withdrawnTotal += row.Withheld;

                byte typeKey = (byte)row.Type;
                if (supplyByType.TryGetValue(typeKey, out var globalCur))
                {
                    supplyByType[typeKey] = globalCur + row.Effective;
                }
                else
                {
                    supplyByType[typeKey] = row.Effective;
                }

                int regionalKey = row.TradeNodeId * GoodTypeStride + typeKey;
                if (regionalSupply.TryGetValue(regionalKey, out var regionalCur))
                {
                    regionalSupply[regionalKey] = regionalCur + row.Effective;
                }
                else
                {
                    regionalSupply[regionalKey] = row.Effective;
                }
            }

            rows.Dispose();
            TaxPhysicalWithdrawalSystem.RecordAbstractTick(requestedTotal, withdrawnTotal);
        }

        struct LodSupplyRow
        {
            public int ProvinceId;
            public int GoodId;
            public int TradeNodeId;
            public GoodType Type;
            public float Effective;
            public float Withheld;
        }

        struct LodSupplyRowComparer : System.Collections.Generic.IComparer<LodSupplyRow>
        {
            public int Compare(LodSupplyRow a, LodSupplyRow b)
            {
                var c = a.ProvinceId.CompareTo(b.ProvinceId);
                return c != 0 ? c : a.GoodId.CompareTo(b.GoodId);
            }
        }

        private static float ComputeRatio(
            NativeHashMap<byte, float> demand,
            NativeHashMap<byte, float> supply,
            GoodType type)
        {
            byte key = (byte)type;
            float d = demand.TryGetValue(key, out var dv) ? dv : 0f;
            float s = supply.TryGetValue(key, out var sv) ? sv : 0f;
            return d > 0f ? math.min(1f, s / d) : 1f;
        }

        private static float ComputeRegionalRatio(
            NativeHashMap<int, float> demand,
            NativeHashMap<int, float> supply,
            int tradeNodeId,
            GoodType type)
        {
            int key = CompositeKey(tradeNodeId, type);
            float d = demand.TryGetValue(key, out var dv) ? dv : 0f;
            float s = supply.TryGetValue(key, out var sv) ? sv : 0f;
            return d > 0f ? math.min(1f, s / d) : 1f;
        }

        private static void BuildEffectiveRatios(
            NativeHashMap<int, float> regionalDemand,
            NativeHashMap<int, float> regionalSupply,
            float ratioFoodGlobal,
            float ratioClothGlobal,
            float ratioLuxuryGlobal,
            float w,
            float oneMinusW,
            NativeHashMap<int, float> effectiveRatios)
        {
            // Collecte déterministe des nœuds présents (ordre croissant).
            var nodeSet = new NativeHashMap<int, byte>(16, Allocator.Temp);
            CollectNodes(regionalDemand, nodeSet);
            CollectNodes(regionalSupply, nodeSet);

            var nodes = new NativeList<int>(nodeSet.Count, Allocator.Temp);
            foreach (var kv in nodeSet)
            {
                nodes.Add(kv.Key);
            }

            nodes.Sort();

            for (int i = 0; i < nodes.Length; i++)
            {
                int nodeId = nodes[i];
                float rFood = ComputeRegionalRatio(regionalDemand, regionalSupply, nodeId, GoodType.Food);
                float rCloth = ComputeRegionalRatio(regionalDemand, regionalSupply, nodeId, GoodType.Manufactured);
                float rLux = ComputeRegionalRatio(regionalDemand, regionalSupply, nodeId, GoodType.Luxury);

                effectiveRatios[CompositeKey(nodeId, GoodType.Food)] =
                    w * rFood + oneMinusW * ratioFoodGlobal;
                effectiveRatios[CompositeKey(nodeId, GoodType.Manufactured)] =
                    w * rCloth + oneMinusW * ratioClothGlobal;
                effectiveRatios[CompositeKey(nodeId, GoodType.Luxury)] =
                    w * rLux + oneMinusW * ratioLuxuryGlobal;
            }

            nodes.Dispose();
            nodeSet.Dispose();
        }

        private static void CollectNodes(NativeHashMap<int, float> map, NativeHashMap<int, byte> nodeSet)
        {
            foreach (var kv in map)
            {
                int nodeId = kv.Key / GoodTypeStride;
                nodeSet.TryAdd(nodeId, 0);
            }
        }

        [BurstCompile]
        private partial struct AggregateDemandJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<ProvinceData> ProvinceDataLookup;
            public NativeHashMap<byte, float> TotalDemand;
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
                byte typeKey = (byte)type;
                if (TotalDemand.TryGetValue(typeKey, out var globalCur))
                {
                    TotalDemand[typeKey] = globalCur + amount;
                }
                else
                {
                    TotalDemand[typeKey] = amount;
                }

                int regionalKey = tradeNodeId * GoodTypeStride + typeKey;
                if (RegionalDemand.TryGetValue(regionalKey, out var regionalCur))
                {
                    RegionalDemand[regionalKey] = regionalCur + amount;
                }
                else
                {
                    RegionalDemand[regionalKey] = amount;
                }
            }
        }

        [BurstCompile]
        private partial struct AccumulateProductionSupplyJob : IJobEntity
        {
            [ReadOnly] public NativeHashMap<int, GoodType> GoodIdToType;
            public NativeHashMap<byte, float> SupplyByType;
            public NativeHashMap<int, float> RegionalSupply;

            public void Execute(in ProductionSite site, in ProvinceData prov)
            {
                if (!GoodIdToType.TryGetValue(site.GoodId, out var type))
                {
                    return;
                }

                byte typeKey = (byte)type;
                if (SupplyByType.TryGetValue(typeKey, out var globalCur))
                {
                    SupplyByType[typeKey] = globalCur + site.LastOutput;
                }
                else
                {
                    SupplyByType[typeKey] = site.LastOutput;
                }

                int regionalKey = prov.TradeNodeId * GoodTypeStride + typeKey;
                if (RegionalSupply.TryGetValue(regionalKey, out var regionalCur))
                {
                    RegionalSupply[regionalKey] = regionalCur + site.LastOutput;
                }
                else
                {
                    RegionalSupply[regionalKey] = site.LastOutput;
                }
            }
        }

        [BurstCompile]
        private partial struct WriteDemandJob : IJobEntity
        {
            [ReadOnly] public NativeHashMap<byte, float> TotalDemand;

            public void Execute(ref MarketPrice price, in GoodData good)
            {
                byte key = (byte)good.Type;
                price.Demand = TotalDemand.TryGetValue(key, out var dv) ? dv : 0f;
            }
        }

        [BurstCompile]
        private partial struct UpdatePopSatisfactionJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<ProvinceData> ProvinceDataLookup;
            [ReadOnly] public NativeHashMap<int, float> EffectiveRatios;
            public float RatioFoodGlobal;
            public float RatioClothGlobal;
            public float RatioLuxuryGlobal;

            public void Execute(ref PopNeeds needs, ref PopData pop)
            {
                int nodeId = 0;
                if (ProvinceDataLookup.HasComponent(pop.Province))
                {
                    nodeId = ProvinceDataLookup[pop.Province].TradeNodeId;
                }

                float ratioFood = LookupRatio(nodeId, GoodType.Food, RatioFoodGlobal);
                float ratioCloth = LookupRatio(nodeId, GoodType.Manufactured, RatioClothGlobal);
                float ratioLuxury = LookupRatio(nodeId, GoodType.Luxury, RatioLuxuryGlobal);

                needs.FoodSatisfied = ratioFood * needs.FoodNeed;
                needs.ClothSatisfied = ratioCloth * needs.ClothNeed;
                needs.LuxurySatisfied = ratioLuxury * needs.LuxuryNeed;
                pop.NeedsSatisfaction = ratioFood * 0.6f + ratioCloth * 0.3f + ratioLuxury * 0.1f;
            }

            private float LookupRatio(int tradeNodeId, GoodType type, float globalFallback)
            {
                if (EffectiveRatios.TryGetValue(CompositeKey(tradeNodeId, type), out var r))
                {
                    return r;
                }

                return globalFallback;
            }
        }

        [BurstCompile]
        private partial struct ReduceSupplyJob : IJobEntity
        {
            [ReadOnly] public NativeHashMap<byte, float> TotalDemand;
            [ReadOnly] public NativeHashMap<byte, float> SupplyByType;
            public float RatioFood;
            public float RatioCloth;
            public float RatioLuxury;

            public void Execute(ref MarketPrice price, in GoodData good)
            {
                float ratio = SelectRatio(good.Type);
                byte key = (byte)good.Type;
                float demand = TotalDemand.TryGetValue(key, out var d) ? d : 0f;
                float sumSupply = SupplyByType.TryGetValue(key, out var ssum) ? ssum : 0f;
                float supply = price.Supply;

                if (demand > 0f && sumSupply > 0f)
                {
                    float consumed = demand * ratio * (supply / sumSupply);
                    price.Supply = math.max(0f, supply - consumed);
                }
            }

            private float SelectRatio(GoodType type)
            {
                if (type == GoodType.Food)
                {
                    return RatioFood;
                }

                if (type == GoodType.Manufactured)
                {
                    return RatioCloth;
                }

                if (type == GoodType.Luxury)
                {
                    return RatioLuxury;
                }

                return 1f;
            }
        }
    }
}
