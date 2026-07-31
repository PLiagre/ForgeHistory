using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using VictoriaGame.Core;
using VictoriaGame.World;

namespace VictoriaGame.Military
{
    /// <summary>
    /// Pertes hors combat : terrain, climat/saison, surpopulation et usure de ravitaillement (Strength).
    /// Org/Moral hors supply sont gérés par <see cref="EncirclementSystem"/> — pas de double pénalité.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(SiegeSystem))]
    public partial struct AttritionSystem : ISystem
    {
        private const float PLAINS_ATTRITION = 0.0005f;
        private const float HILLS_ATTRITION = 0.003f;
        private const float MOUNTAIN_ATTRITION = 0.012f;
        private const float DESERT_ATTRITION = 0.007f;
        private const float FOREST_ATTRITION = 0.002f;
        private const float COASTAL_ATTRITION = 0.001f;

        private const float COLD_WINTER_MULT = 2.5f;
        private const float COLD_SUMMER_MULT = 0.4f;
        private const float TROPICAL_MULT = 1.3f;
        private const float ARID_MULT = 1.4f;
        private const float TEMPERATE_WINTER_MULT = 1.1f;
        private const float MEDITERRANEAN_MULT = 0.7f;

        private const float OVERCROWDING_PER_EXTRA_ARMY = 0.008f;
        private const float BASE_SUPPLY_ATTRITION = 0.0006f;

        private const float HARSH_ORG_PENALTY = 0.4f;
        private const float HARSH_MORALE_PENALTY = 0.25f;

        private struct ProvinceAttritionInfo
        {
            public TerrainType Terrain;
            public ClimateType Climate;
        }

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WorldState>();
        }

        [BurstDiscard]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.HasSingleton<WorldState>())
            {
                return;
            }

            var worldState = SystemAPI.GetSingleton<WorldState>();
            if (worldState.IsPaused)
            {
                return;
            }

            var provinceInfo = new NativeHashMap<int, ProvinceAttritionInfo>(64, Allocator.TempJob);
            foreach (var province in SystemAPI.Query<RefRO<ProvinceData>>())
            {
                var data = province.ValueRO;
                provinceInfo[data.ProvinceId] = new ProvinceAttritionInfo
                {
                    Terrain = data.Terrain,
                    Climate = data.Climate
                };
            }

            var armyCountPerProvince = new NativeHashMap<int, int>(32, Allocator.TempJob);
            foreach (var army in SystemAPI.Query<RefRO<ArmyData>>())
            {
                if (army.ValueRO.Strength <= 0f)
                {
                    continue;
                }

                var provinceId = army.ValueRO.ProvinceId;
                armyCountPerProvince.TryGetValue(provinceId, out var count);
                armyCountPerProvince[provinceId] = count + 1;
            }

            var job = new AttritionJob
            {
                ProvinceInfo = provinceInfo,
                ArmyCountPerProvince = armyCountPerProvince,
                Month = worldState.Month,
                WorldTick = worldState.CurrentTick
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
            state.Dependency.Complete();

            provinceInfo.Dispose();
            armyCountPerProvince.Dispose();
        }

        public void OnDestroy(ref SystemState state) { }

        [BurstCompile]
        private partial struct AttritionJob : IJobEntity
        {
            [ReadOnly] public NativeHashMap<int, ProvinceAttritionInfo> ProvinceInfo;
            [ReadOnly] public NativeHashMap<int, int> ArmyCountPerProvince;
            public int Month;
            public int WorldTick;

            public void Execute(
                ref ArmyData army,
                in ArmySupplyState supplyState,
                DynamicBuffer<RegimentSlot> slots)
            {
                if (army.Strength <= 0f)
                {
                    return;
                }

                var terrainRate = 0f;
                var climateMult = 1f;
                var isHarshEnvironment = false;

                if (ProvinceInfo.TryGetValue(army.ProvinceId, out var province))
                {
                    terrainRate = GetTerrainAttritionRate(province.Terrain);
                    climateMult = GetClimateMultiplier(province.Climate, Month);
                    isHarshEnvironment = terrainRate >= MOUNTAIN_ATTRITION * 0.5f ||
                                          climateMult >= TROPICAL_MULT;
                }

                var overcrowdingMult = 1f;
                if (ArmyCountPerProvince.TryGetValue(army.ProvinceId, out var armyCount) && armyCount > 1)
                {
                    overcrowdingMult = 1f + (armyCount - 1) * OVERCROWDING_PER_EXTRA_ARMY;
                }

                var supplyRate = 0f;
                if (!supplyState.IsSupplied)
                {
                    var ticksOut = supplyState.LastSupplyTick <= 0
                        ? 1
                        : math.max(1, WorldTick - supplyState.LastSupplyTick);
                    supplyRate = BASE_SUPPLY_ATTRITION * ticksOut * ticksOut;
                }

                var totalRate = (terrainRate * climateMult * overcrowdingMult) + supplyRate;
                if (totalRate <= 0f)
                {
                    return;
                }

                var strengthLoss = army.Strength * totalRate;
                if (strengthLoss <= 0f)
                {
                    return;
                }

                ApplyStrengthLoss(ref army, slots, strengthLoss);

                if (supplyState.IsSupplied && isHarshEnvironment)
                {
                    army.Organization = math.max(0f, army.Organization - HARSH_ORG_PENALTY);
                    army.Morale = math.max(0f, army.Morale - HARSH_MORALE_PENALTY);

                    for (var i = 0; i < slots.Length; i++)
                    {
                        var slot = slots[i];
                        slot.Organization = math.max(0f, slot.Organization - HARSH_ORG_PENALTY);
                        slot.Morale = math.max(0f, slot.Morale - HARSH_MORALE_PENALTY);
                        slots[i] = slot;
                    }
                }
            }

            private static float GetTerrainAttritionRate(TerrainType terrain)
            {
                switch (terrain)
                {
                    case TerrainType.Mountains:
                        return MOUNTAIN_ATTRITION;
                    case TerrainType.Desert:
                        return DESERT_ATTRITION;
                    case TerrainType.Hills:
                        return HILLS_ATTRITION;
                    case TerrainType.Forest:
                        return FOREST_ATTRITION;
                    case TerrainType.Coastal:
                        return COASTAL_ATTRITION;
                    default:
                        return PLAINS_ATTRITION;
                }
            }

            private static float GetClimateMultiplier(ClimateType climate, int month)
            {
                var isWinter = month == 11 || month == 12 || month == 1 || month == 2;

                switch (climate)
                {
                    case ClimateType.Cold:
                        return isWinter ? COLD_WINTER_MULT : COLD_SUMMER_MULT;
                    case ClimateType.Tropical:
                        return TROPICAL_MULT;
                    case ClimateType.Arid:
                        return ARID_MULT;
                    case ClimateType.Temperate:
                        return isWinter ? TEMPERATE_WINTER_MULT : 1f;
                    case ClimateType.Mediterranean:
                        return MEDITERRANEAN_MULT;
                    default:
                        return 1f;
                }
            }

            private static void ApplyStrengthLoss(
                ref ArmyData army,
                DynamicBuffer<RegimentSlot> slots,
                float strengthLoss)
            {
                if (slots.Length == 0)
                {
                    army.Strength = math.max(0f, army.Strength - strengthLoss);
                    return;
                }

                var totalSlotStrength = 0f;
                for (var i = 0; i < slots.Length; i++)
                {
                    totalSlotStrength += slots[i].Strength;
                }

                if (totalSlotStrength <= 0f)
                {
                    army.Strength = math.max(0f, army.Strength - strengthLoss);
                    return;
                }

                var lossRatio = math.min(1f, strengthLoss / totalSlotStrength);
                var sum = 0f;

                for (var i = 0; i < slots.Length; i++)
                {
                    var slot = slots[i];
                    slot.Strength = math.max(0f, slot.Strength * (1f - lossRatio));
                    slots[i] = slot;
                    sum += slot.Strength;
                }

                army.Strength = sum;
            }
        }
    }
}
