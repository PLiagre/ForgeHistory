using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using VictoriaGame.Core;
using VictoriaGame.World;

namespace VictoriaGame.Military
{
    /// <summary>
    /// Transforme la pression tactique du front en gain de terrain : transfère le contrôle
    /// militaire (ProvinceOwnership.Controller) quand l'attaquant perce la défense.
    /// Le front est recalculé au tick suivant par <see cref="FrontLineSystem"/>.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(AttritionSystem))]
    public partial struct FrontAdvanceSystem : ISystem
    {
        private const float PRESSURE_DOMINANCE_RATIO = 1.5f;
        private const float MIN_COMBAT_ORGANIZATION = 1f;
        private const float WAR_SCORE_PER_DEV_POINT = 0.15f;
        private const float PENETRATION_DEPTH_INCREMENT = 1f;

        private struct FortSnapshot
        {
            public Entity OwnerCountry;
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

            var provinceEntities = new NativeHashMap<int, Entity>(64, Allocator.Temp);
            var provinceDevelopment = new NativeHashMap<int, ProvinceDevelopment>(64, Allocator.Temp);

            foreach (var (provinceData, development, provinceEntity) in SystemAPI
                         .Query<RefRO<ProvinceData>, RefRO<ProvinceDevelopment>>()
                         .WithEntityAccess())
            {
                var provinceId = provinceData.ValueRO.ProvinceId;
                provinceEntities[provinceId] = provinceEntity;
                provinceDevelopment[provinceId] = development.ValueRO;
            }

            var fortsByProvince = new NativeHashMap<int, FortSnapshot>(16, Allocator.Temp);
            foreach (var fort in SystemAPI.Query<RefRO<FortData>>())
            {
                fortsByProvince[fort.ValueRO.ProvinceId] = new FortSnapshot
                {
                    OwnerCountry = fort.ValueRO.OwnerCountry
                };
            }

            var armiesByProvince = new NativeParallelMultiHashMap<int, ArmyData>(64, Allocator.Temp);
            foreach (var army in SystemAPI.Query<RefRO<ArmyData>>())
            {
                armiesByProvince.Add(army.ValueRO.ProvinceId, army.ValueRO);
            }

            foreach (var (sector, frontLine, sectorEntity) in SystemAPI
                         .Query<RefRW<FrontSectorData>, DynamicBuffer<FrontLineState>>()
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

                var penetrationGain = 0f;
                var warScoreChanged = false;

                for (var i = 0; i < frontLine.Length; i++)
                {
                    var frontState = frontLine[i];
                    var provinceId = frontState.ProvinceId;

                    if (!provinceEntities.TryGetValue(provinceId, out var provinceEntity) ||
                        !state.EntityManager.HasComponent<ProvinceOwnership>(provinceEntity))
                    {
                        continue;
                    }

                    var ownership = state.EntityManager.GetComponentData<ProvinceOwnership>(provinceEntity);
                    var previousController = ownership.Controller;

                    var attackerCanFight = HasFightingArmy(
                        provinceId, attackerCountry, armiesByProvince);
                    var defenderCanFight = HasFightingArmy(
                        provinceId, defenderCountry, armiesByProvince);

                    var attackerDominates = IsPressureDominant(
                        frontState.AttackerPressure, frontState.DefenderPressure);
                    var defenderDominates = IsPressureDominant(
                        frontState.DefenderPressure, frontState.AttackerPressure);

                    Entity newController = previousController;
                    var warScoreDelta = 0f;

                    if (previousController == defenderCountry)
                    {
                        if (attackerDominates &&
                            !defenderCanFight &&
                            !IsFortBlocking(fortsByProvince, provinceId, defenderCountry))
                        {
                            newController = attackerCountry;
                            warScoreDelta = ComputeProvinceWarScore(provinceId, provinceDevelopment);
                            penetrationGain += PENETRATION_DEPTH_INCREMENT;
                        }
                    }
                    else if (previousController == attackerCountry)
                    {
                        if (defenderDominates &&
                            !attackerCanFight &&
                            !IsFortBlocking(fortsByProvince, provinceId, attackerCountry))
                        {
                            newController = defenderCountry;
                            warScoreDelta = -ComputeProvinceWarScore(provinceId, provinceDevelopment);
                        }
                    }

                    if (newController == previousController)
                    {
                        continue;
                    }

                    ownership.Controller = newController;
                    state.EntityManager.SetComponentData(provinceEntity, ownership);

                    if (math.abs(warScoreDelta) > 0f)
                    {
                        warData.WarScore = math.clamp(warData.WarScore + warScoreDelta, -100f, 100f);
                        warScoreChanged = true;
                    }
                }

                if (penetrationGain > 0f)
                {
                    var sectorData = sector.ValueRO;
                    sectorData.PenetrationDepth += penetrationGain;
                    sector.ValueRW = sectorData;
                }

                if (warScoreChanged)
                {
                    state.EntityManager.SetComponentData(warEntity, warData);
                }
            }

            armiesByProvince.Dispose();
            fortsByProvince.Dispose();
            provinceDevelopment.Dispose();
            provinceEntities.Dispose();
        }

        public void OnDestroy(ref SystemState state) { }

        private static bool IsPressureDominant(float leadingPressure, float trailingPressure)
        {
            if (leadingPressure <= 0f)
            {
                return false;
            }

            return leadingPressure >= trailingPressure * PRESSURE_DOMINANCE_RATIO;
        }

        private static bool HasFightingArmy(
            int provinceId,
            Entity country,
            NativeParallelMultiHashMap<int, ArmyData> armiesByProvince)
        {
            if (!armiesByProvince.TryGetFirstValue(provinceId, out var army, out var iterator))
            {
                return false;
            }

            do
            {
                if (army.Country == country &&
                    army.Strength > 0f &&
                    army.Organization >= MIN_COMBAT_ORGANIZATION)
                {
                    return true;
                }
            }
            while (armiesByProvince.TryGetNextValue(out army, ref iterator));

            return false;
        }

        private static bool IsFortBlocking(
            NativeHashMap<int, FortSnapshot> fortsByProvince,
            int provinceId,
            Entity defendingCountry)
        {
            return fortsByProvince.TryGetValue(provinceId, out var fort) &&
                   fort.OwnerCountry == defendingCountry;
        }

        private static float ComputeProvinceWarScore(
            int provinceId,
            NativeHashMap<int, ProvinceDevelopment> provinceDevelopment)
        {
            if (!provinceDevelopment.TryGetValue(provinceId, out var development))
            {
                return 0f;
            }

            var value = development.Tax + development.Production + development.Manpower;
            return value * WAR_SCORE_PER_DEV_POINT;
        }
    }
}
