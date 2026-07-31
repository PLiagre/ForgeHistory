using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using VictoriaGame.Core;
using VictoriaGame.World;

namespace VictoriaGame.Military
{
    /// <summary>
    /// Résout les combats sur les provinces contestées (FrontLineState.IsContested).
    /// Applique pertes, retraites, IsEngaged et met à jour WarData.WarScore.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(FrontLineSystem))]
    public partial struct BattleResolutionSystem : ISystem
    {
        private const float BASE_CASUALTY_RATE = 0.04f;
        private const float WAR_SCORE_BATTLE_DELTA = 0.75f;
        private const float MIN_DISCIPLINE = 0.1f;
        private const float MIN_SUPPLY_FACTOR = 0.1f;

        private struct ArmyBattleEntry
        {
            public Entity Entity;
            public Entity Country;
            public Entity ArmyGroup;
            public float Power;
            public float Strength;
            public bool IsAttacker;
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
            var globalSeed = worldState.GlobalSeed;

            var provinceTerrain = new NativeHashMap<int, TerrainType>(64, Allocator.Temp);
            foreach (var province in SystemAPI.Query<RefRO<ProvinceData>>())
            {
                provinceTerrain[province.ValueRO.ProvinceId] = province.ValueRO.Terrain;
            }

            var provinceFortBonus = new NativeHashMap<int, float>(16, Allocator.Temp);
            foreach (var fort in SystemAPI.Query<RefRO<FortData>>())
            {
                provinceFortBonus[fort.ValueRO.ProvinceId] = 1f + fort.ValueRO.DefenseBonus;
            }

            var countryMilTech = new NativeHashMap<Entity, int>(32, Allocator.Temp);
            foreach (var (tech, entity) in SystemAPI.Query<RefRO<TechData>>().WithEntityAccess())
            {
                countryMilTech[entity] = tech.ValueRO.MilTech;
            }

            var armyGroupLookup = SystemAPI.GetComponentLookup<ArmyGroupData>(false);
            var generalLookup = SystemAPI.GetComponentLookup<GeneralData>(true);
            armyGroupLookup.Update(ref state);
            generalLookup.Update(ref state);

            var contestedProvinces = new NativeHashSet<int>(32, Allocator.Temp);
            var battleArmies = new NativeList<ArmyBattleEntry>(32, Allocator.Temp);

            foreach (var (sector, frontLine, sectorEntity) in SystemAPI
                         .Query<RefRO<FrontSectorData>, DynamicBuffer<FrontLineState>>()
                         .WithEntityAccess())
            {
                if (!sector.ValueRO.IsActive)
                {
                    continue;
                }

                var attackerCountry = sector.ValueRO.AttackerCountry;
                var defenderCountry = sector.ValueRO.DefenderCountry;
                var warEntity = sector.ValueRO.War;

                if (!state.EntityManager.Exists(warEntity) ||
                    !state.EntityManager.HasComponent<WarData>(warEntity))
                {
                    continue;
                }

                var warData = state.EntityManager.GetComponentData<WarData>(warEntity);
                if (!warData.IsActive)
                {
                    continue;
                }

                var attackerTech = GetMilTech(attackerCountry, countryMilTech);
                var defenderTech = GetMilTech(defenderCountry, countryMilTech);

                for (var i = 0; i < frontLine.Length; i++)
                {
                    var frontState = frontLine[i];
                    if (!frontState.IsContested)
                    {
                        continue;
                    }

                    var provinceId = frontState.ProvinceId;
                    contestedProvinces.Add(provinceId);

                    var terrainFactor = GetTerrainDefenseFactor(
                        provinceTerrain.TryGetValue(provinceId, out var terrain)
                            ? terrain
                            : TerrainType.Plains);

                    var fortFactor = 1f;
                    if (provinceFortBonus.TryGetValue(provinceId, out var fortBonus))
                    {
                        fortFactor = fortBonus;
                    }

                    battleArmies.Clear();
                    var totalAttackerPower = 0f;
                    var totalDefenderPower = 0f;

                    foreach (var (army, armyEntity) in SystemAPI.Query<RefRO<ArmyData>>().WithEntityAccess())
                    {
                        if (army.ValueRO.ProvinceId != provinceId || army.ValueRO.Strength <= 0f)
                        {
                            continue;
                        }

                        var isAttacker = army.ValueRO.Country == attackerCountry;
                        var isDefender = army.ValueRO.Country == defenderCountry;
                        if (!isAttacker && !isDefender)
                        {
                            continue;
                        }

                        var power = ComputeArmyPower(
                            army.ValueRO,
                            isAttacker ? attackerTech : defenderTech,
                            isAttacker,
                            army.ValueRO.ArmyGroup,
                            armyGroupLookup,
                            generalLookup,
                            isDefender ? terrainFactor : 1f,
                            isDefender ? fortFactor : 1f);

                        battleArmies.Add(new ArmyBattleEntry
                        {
                            Entity = armyEntity,
                            Country = army.ValueRO.Country,
                            ArmyGroup = army.ValueRO.ArmyGroup,
                            Power = power,
                            Strength = army.ValueRO.Strength,
                            IsAttacker = isAttacker
                        });

                        if (isAttacker)
                        {
                            totalAttackerPower += power;
                        }
                        else
                        {
                            totalDefenderPower += power;
                        }
                    }

                    if (totalAttackerPower <= 0f || totalDefenderPower <= 0f)
                    {
                        continue;
                    }

                    var rng = CreateBattleRandom(globalSeed, currentTick, provinceId);
                    var variance = rng.NextFloat(0.92f, 1.08f);

                    var powerSum = totalAttackerPower + totalDefenderPower;
                    var attackerShare = totalAttackerPower / powerSum;
                    var defenderShare = totalDefenderPower / powerSum;

                    var attackerLossRate = BASE_CASUALTY_RATE * variance * defenderShare * 2f;
                    var defenderLossRate = BASE_CASUALTY_RATE * variance * attackerShare * 2f;

                    for (var a = 0; a < battleArmies.Length; a++)
                    {
                        var entry = battleArmies[a];
                        var lossRate = entry.IsAttacker ? attackerLossRate : defenderLossRate;
                        ApplyCasualties(ref state, entry.Entity, lossRate, ref armyGroupLookup);
                    }

                    var outcomeBias = (attackerShare - 0.5f) * 2f;
                    var scoreDelta = outcomeBias * WAR_SCORE_BATTLE_DELTA * variance;
                    warData.WarScore = math.clamp(warData.WarScore + scoreDelta, -100f, 100f);
                    state.EntityManager.SetComponentData(warEntity, warData);
                }
            }

            foreach (var (army, armyEntity) in SystemAPI.Query<RefRW<ArmyData>>().WithEntityAccess())
            {
                var provinceId = army.ValueRO.ProvinceId;
                if (contestedProvinces.Contains(provinceId))
                {
                    // v1_016 : force nulle → désengager (sinon IsEngaged reste true à jamais
                    // et ArmyOrganizationSystem ne reconstitue pas — mort d'armée longue durée).
                    if (army.ValueRO.Strength <= 0f)
                    {
                        army.ValueRW.IsEngaged = false;
                    }
                    else if (!army.ValueRO.IsEngaged)
                    {
                        army.ValueRW.IsEngaged = true;
                    }
                }
                else if (army.ValueRO.IsEngaged)
                {
                    army.ValueRW.IsEngaged = false;
                }
            }

            battleArmies.Dispose();
            contestedProvinces.Dispose();
            countryMilTech.Dispose();
            provinceFortBonus.Dispose();
            provinceTerrain.Dispose();
        }

        public void OnDestroy(ref SystemState state) { }

        private static Random CreateBattleRandom(uint globalSeed, int tick, int provinceId)
        {
            var seed = math.hash(new uint3(globalSeed, (uint)tick, (uint)provinceId));
            return Random.CreateFromIndex(seed);
        }

        private static int GetMilTech(Entity country, NativeHashMap<Entity, int> countryMilTech)
        {
            return countryMilTech.TryGetValue(country, out var milTech) ? milTech : 0;
        }

        private static float ComputeDiscipline(float organization, float morale)
        {
            var orgNorm = math.clamp(organization / 100f, 0f, 1f);
            var moraleNorm = math.clamp(morale / 100f, 0f, 1f);
            return math.max(MIN_DISCIPLINE, orgNorm * 0.5f + moraleNorm * 0.5f);
        }

        private static float ComputeTechFactor(int milTech)
        {
            return 1f + milTech * 0.01f;
        }

        private static float ComputeGeneralFactor(
            Entity armyGroup,
            bool isAttacker,
            ComponentLookup<ArmyGroupData> armyGroupLookup,
            ComponentLookup<GeneralData> generalLookup)
        {
            const int defaultRating = 5;
            var rating = defaultRating;

            if (armyGroupLookup.HasComponent(armyGroup))
            {
                var generalEntity = armyGroupLookup[armyGroup].CommandingGeneral;
                if (generalLookup.HasComponent(generalEntity))
                {
                    var general = generalLookup[generalEntity];
                    rating = isAttacker ? general.AttackRating : general.DefenseRating;
                }
            }

            return 1f + rating * 0.05f;
        }

        private static float GetTerrainDefenseFactor(TerrainType terrain)
        {
            switch (terrain)
            {
                case TerrainType.Hills:
                    return 1.15f;
                case TerrainType.Mountains:
                    return 1.30f;
                case TerrainType.Forest:
                    return 1.10f;
                case TerrainType.Plains:
                case TerrainType.Desert:
                case TerrainType.Coastal:
                default:
                    return 1.0f;
            }
        }

        private static float ComputeArmyPower(
            ArmyData army,
            int milTech,
            bool isAttacker,
            Entity armyGroup,
            ComponentLookup<ArmyGroupData> armyGroupLookup,
            ComponentLookup<GeneralData> generalLookup,
            float terrainFactor,
            float fortFactor)
        {
            var discipline = ComputeDiscipline(army.Organization, army.Morale);
            var techFactor = ComputeTechFactor(milTech);
            var generalFactor = ComputeGeneralFactor(armyGroup, isAttacker, armyGroupLookup, generalLookup);
            var supplyFactor = math.max(MIN_SUPPLY_FACTOR, army.SupplyLevel);

            var power = army.Strength * discipline * techFactor * generalFactor * supplyFactor;

            if (!isAttacker)
            {
                power *= terrainFactor * fortFactor;
            }

            return power;
        }

        private static void ApplyCasualties(
            ref SystemState state,
            Entity armyEntity,
            float lossRate,
            ref ComponentLookup<ArmyGroupData> armyGroupLookup)
        {
            if (!state.EntityManager.HasComponent<ArmyData>(armyEntity))
            {
                return;
            }

            var army = state.EntityManager.GetComponentData<ArmyData>(armyEntity);
            army.Strength = math.max(0f, army.Strength * (1f - lossRate));
            army.Organization = math.max(0f, army.Organization * (1f - lossRate * 1.5f));
            army.Morale = math.max(0f, army.Morale * (1f - lossRate * 0.8f));
            army.IsEngaged = true;

            if (state.EntityManager.HasBuffer<RegimentSlot>(armyEntity))
            {
                var slots = state.EntityManager.GetBuffer<RegimentSlot>(armyEntity);
                for (var i = 0; i < slots.Length; i++)
                {
                    var slot = slots[i];
                    slot.Strength = math.max(0f, slot.Strength * (1f - lossRate));
                    slot.Organization = math.max(0f, slot.Organization * (1f - lossRate * 1.5f));
                    slot.Morale = math.max(0f, slot.Morale * (1f - lossRate * 0.8f));
                    slots[i] = slot;
                }

                var sum = 0f;
                for (var i = 0; i < slots.Length; i++)
                {
                    sum += slots[i].Strength;
                }

                army.Strength = sum;
            }

            if (army.Organization <= 0f)
            {
                army.Organization = 0f;
                if (armyGroupLookup.HasComponent(army.ArmyGroup))
                {
                    var group = armyGroupLookup[army.ArmyGroup];
                    group.Mission = ArmyMission.Regroup;
                    armyGroupLookup[army.ArmyGroup] = group;
                }
            }

            state.EntityManager.SetComponentData(armyEntity, army);
        }
    }
}
