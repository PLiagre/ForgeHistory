using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using VictoriaGame.Core;

namespace VictoriaGame.Military
{
    /// <summary>
    /// Pilote la mission stratégique de chaque groupe d'armées : Hold (paix), Regroup (retraite),
    /// Besiege (fort ennemi), Advance (offensive).
    /// Doit s'exécuter après BattleResolutionSystem (qui peut poser Regroup) et avant SiegeSystem.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(BattleResolutionSystem))]
    [UpdateBefore(typeof(SiegeSystem))]
    public partial struct ArmyMissionSystem : ISystem
    {
        private const float RETREAT_ORG_THRESHOLD = 20f;
        private const float RECOVERY_ORG_THRESHOLD = 50f;

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

            var countriesAtWar = new NativeHashSet<Entity>(32, Allocator.Temp);
            foreach (var war in SystemAPI.Query<RefRO<WarData>>())
            {
                if (!war.ValueRO.IsActive)
                {
                    continue;
                }

                countriesAtWar.Add(war.ValueRO.Attacker);
                countriesAtWar.Add(war.ValueRO.Defender);
            }

            var enemyFortByProvince = BuildEnemyFortIndex(ref state);

            foreach (var (groupRef, groupEntity) in
                     SystemAPI.Query<RefRW<ArmyGroupData>>().WithEntityAccess())
            {
                var groupCountry = groupRef.ValueRO.Country;

                if (!countriesAtWar.Contains(groupCountry))
                {
                    var idleGroup = groupRef.ValueRO;
                    idleGroup.Mission = ArmyMission.Hold;
                    groupRef.ValueRW = idleGroup;
                    continue;
                }

                var currentMission = groupRef.ValueRO.Mission;

                if (currentMission == ArmyMission.Regroup)
                {
                    var allRecovered = true;

                    foreach (var army in SystemAPI.Query<RefRO<ArmyData>>())
                    {
                        if (army.ValueRO.ArmyGroup != groupEntity)
                        {
                            continue;
                        }

                        if (army.ValueRO.Organization < RECOVERY_ORG_THRESHOLD)
                        {
                            allRecovered = false;
                            break;
                        }
                    }

                    if (!allRecovered)
                    {
                        var regroupGroup = groupRef.ValueRO;
                        regroupGroup.Mission = ArmyMission.Regroup;
                        groupRef.ValueRW = regroupGroup;
                        continue;
                    }
                }

                var shouldRegroup = false;

                foreach (var army in SystemAPI.Query<RefRO<ArmyData>>())
                {
                    if (army.ValueRO.ArmyGroup != groupEntity)
                    {
                        continue;
                    }

                    if (army.ValueRO.Organization < RETREAT_ORG_THRESHOLD)
                    {
                        shouldRegroup = true;
                        break;
                    }
                }

                if (shouldRegroup)
                {
                    var regroupGroup = groupRef.ValueRO;
                    regroupGroup.Mission = ArmyMission.Regroup;
                    groupRef.ValueRW = regroupGroup;
                    continue;
                }

                var shouldBesiege = false;

                foreach (var army in SystemAPI.Query<RefRO<ArmyData>>())
                {
                    if (army.ValueRO.ArmyGroup != groupEntity)
                    {
                        continue;
                    }

                    if (!enemyFortByProvince.TryGetValue(army.ValueRO.ProvinceId, out var fortOwner))
                    {
                        continue;
                    }

                    if (fortOwner != groupCountry)
                    {
                        shouldBesiege = true;
                        break;
                    }
                }

                var group = groupRef.ValueRO;
                group.Mission = shouldBesiege ? ArmyMission.Besiege : ArmyMission.Advance;
                groupRef.ValueRW = group;
            }

            enemyFortByProvince.Dispose();
            countriesAtWar.Dispose();
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        private NativeHashMap<int, Entity> BuildEnemyFortIndex(ref SystemState state)
        {
            var map = new NativeHashMap<int, Entity>(64, Allocator.Temp);

            foreach (var fort in SystemAPI.Query<RefRO<FortData>>())
            {
                map[fort.ValueRO.ProvinceId] = fort.ValueRO.OwnerCountry;
            }

            return map;
        }
    }
}
