using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using VictoriaGame.Core;
using VictoriaGame.World;

namespace VictoriaGame.Military
{
    /// <summary>
    /// Progression des sièges sur les forts : détection des assiégeants, avancement,
    /// attrition de garnison, chute du fort et transfert du contrôle militaire.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(BattleResolutionSystem))]
    public partial struct SiegeSystem : ISystem
    {
        private const float BASE_SIEGE_RATE = 0.025f;
        private const float SIEGE_ARTILLERY_FACTOR = 0.003f;
        private const float FIELD_ARTILLERY_FACTOR = 0.001f;
        private const float MIN_SUPPLY_FACTOR = 0.1f;
        private const float SIEGE_RECOVERY_RATE = 0.004f;
        private const float GARRISON_RECOVERY_RATE = 0.02f;
        private const float GARRISON_ATTRITION_FACTOR = 0.85f;
        private const float GARRISON_REBUILD_FRACTION = 0.25f;
        private const float WAR_SCORE_SIEGE_DELTA = 4f;

        private struct BesiegerStats
        {
            public Entity Country;
            public float SupplyFactorSum;
            public int SupplyCount;
            public float SiegeArtilleryStrength;
            public float FieldArtilleryStrength;
            public float ArmyStrength;
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

            var currentTick = worldState.CurrentTick;

            var provinceEntities = new NativeHashMap<int, Entity>(64, Allocator.Temp);
            foreach (var (provinceData, provinceEntity) in SystemAPI.Query<RefRO<ProvinceData>>().WithEntityAccess())
            {
                provinceEntities[provinceData.ValueRO.ProvinceId] = provinceEntity;
            }

            var armyGroupLookup = SystemAPI.GetComponentLookup<ArmyGroupData>(true);
            armyGroupLookup.Update(ref state);

            foreach (var (fortRef, fortEntity) in SystemAPI.Query<RefRW<FortData>>().WithEntityAccess())
            {
                var fort = fortRef.ValueRO;
                var wasUnderSiege = fort.IsUnderSiege;

                var bestBesieger = default(BesiegerStats);
                var hasBesieger = false;
                var totalSupplyFactor = 0f;
                var besiegerCount = 0;
                var totalSiegeArtillery = 0f;
                var totalFieldArtillery = 0f;

                foreach (var (army, armyEntity) in SystemAPI.Query<RefRO<ArmyData>>().WithEntityAccess())
                {
                    if (army.ValueRO.ProvinceId != fort.ProvinceId ||
                        army.ValueRO.Strength <= 0f ||
                        army.ValueRO.Country == fort.OwnerCountry)
                    {
                        continue;
                    }

                    if (!armyGroupLookup.HasComponent(army.ValueRO.ArmyGroup) ||
                        armyGroupLookup[army.ValueRO.ArmyGroup].Mission != ArmyMission.Besiege)
                    {
                        continue;
                    }

                    var supplyFactor = math.max(MIN_SUPPLY_FACTOR, army.ValueRO.SupplyLevel);
                    totalSupplyFactor += supplyFactor;
                    besiegerCount++;

                    var siegeArtillery = 0f;
                    var fieldArtillery = 0f;

                    if (state.EntityManager.HasBuffer<RegimentSlot>(armyEntity))
                    {
                        var slots = state.EntityManager.GetBuffer<RegimentSlot>(armyEntity);
                        for (var i = 0; i < slots.Length; i++)
                        {
                            var slot = slots[i];
                            if (slot.Strength <= 0f)
                            {
                                continue;
                            }

                            if (slot.Type == RegimentType.SiegeArtillery)
                            {
                                siegeArtillery += slot.Strength;
                            }
                            else if (slot.Type == RegimentType.FieldArtillery)
                            {
                                fieldArtillery += slot.Strength;
                            }
                        }
                    }

                    totalSiegeArtillery += siegeArtillery;
                    totalFieldArtillery += fieldArtillery;

                    var contribution = siegeArtillery * 2f + fieldArtillery + army.ValueRO.Strength;
                    var bestContribution = bestBesieger.SiegeArtilleryStrength * 2f +
                                           bestBesieger.FieldArtilleryStrength +
                                           bestBesieger.ArmyStrength;

                    if (!hasBesieger || contribution > bestContribution)
                    {
                        hasBesieger = true;
                        bestBesieger = new BesiegerStats
                        {
                            Country = army.ValueRO.Country,
                            SupplyFactorSum = supplyFactor,
                            SupplyCount = 1,
                            SiegeArtilleryStrength = siegeArtillery,
                            FieldArtilleryStrength = fieldArtillery,
                            ArmyStrength = army.ValueRO.Strength
                        };
                    }
                }

                var isUnderSiege = besiegerCount > 0;
                fort.IsUnderSiege = isUnderSiege;

                if (isUnderSiege)
                {
                    if (!wasUnderSiege)
                    {
                        fort.SiegeStartTick = currentTick;
                    }

                    var avgSupplyFactor = totalSupplyFactor / besiegerCount;
                    var artilleryBonus = totalSiegeArtillery * SIEGE_ARTILLERY_FACTOR +
                                         totalFieldArtillery * FIELD_ARTILLERY_FACTOR;
                    var progressDelta = (BASE_SIEGE_RATE / fort.Level) *
                                      (1f + artilleryBonus) *
                                      avgSupplyFactor;

                    fort.SiegeProgress = math.min(1f, fort.SiegeProgress + progressDelta);

                    var garrisonLoss = fort.MaxGarrisonStrength * progressDelta * GARRISON_ATTRITION_FACTOR;
                    fort.GarrisonStrength = math.max(0f, fort.GarrisonStrength - garrisonLoss);

                    if (fort.SiegeProgress >= 1f && hasBesieger)
                    {
                        var besiegerCountry = bestBesieger.Country;
                        var formerOwner = fort.OwnerCountry;

                        fort.GarrisonStrength = 0f;
                        fort.IsUnderSiege = false;
                        fort.SiegeProgress = 0f;
                        fort.SiegeStartTick = 0;
                        fort.OwnerCountry = besiegerCountry;
                        fort.GarrisonStrength = fort.MaxGarrisonStrength * GARRISON_REBUILD_FRACTION;

                        if (provinceEntities.TryGetValue(fort.ProvinceId, out var provinceEntity) &&
                            state.EntityManager.HasComponent<ProvinceOwnership>(provinceEntity))
                        {
                            var ownership = state.EntityManager.GetComponentData<ProvinceOwnership>(provinceEntity);
                            ownership.Controller = besiegerCountry;
                            state.EntityManager.SetComponentData(provinceEntity, ownership);
                        }

                        foreach (var (war, warEntity) in SystemAPI.Query<RefRO<WarData>>().WithEntityAccess())
                        {
                            if (!war.ValueRO.IsActive)
                            {
                                continue;
                            }

                            var besiegerIsAttacker = war.ValueRO.Attacker == besiegerCountry &&
                                                     war.ValueRO.Defender == formerOwner;
                            var besiegerIsDefender = war.ValueRO.Defender == besiegerCountry &&
                                                     war.ValueRO.Attacker == formerOwner;

                            if (!besiegerIsAttacker && !besiegerIsDefender)
                            {
                                continue;
                            }

                            var warData = war.ValueRO;
                            var scoreDelta = besiegerIsAttacker ? WAR_SCORE_SIEGE_DELTA : -WAR_SCORE_SIEGE_DELTA;
                            warData.WarScore = math.clamp(warData.WarScore + scoreDelta, -100f, 100f);
                            state.EntityManager.SetComponentData(warEntity, warData);
                        }
                    }
                }
                else
                {
                    if (fort.SiegeProgress > 0f)
                    {
                        fort.SiegeProgress = math.max(0f, fort.SiegeProgress - SIEGE_RECOVERY_RATE);
                    }

                    if (fort.GarrisonStrength < fort.MaxGarrisonStrength)
                    {
                        var recovery = fort.MaxGarrisonStrength * GARRISON_RECOVERY_RATE;
                        fort.GarrisonStrength = math.min(fort.MaxGarrisonStrength, fort.GarrisonStrength + recovery);
                    }

                    if (!wasUnderSiege)
                    {
                        fort.SiegeStartTick = 0;
                    }
                }

                fortRef.ValueRW = fort;
            }

            provinceEntities.Dispose();
        }

        public void OnDestroy(ref SystemState state) { }
    }
}
