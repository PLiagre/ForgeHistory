using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using VictoriaGame.Core;
using VictoriaGame.World;

namespace VictoriaGame.Military
{
    /// <summary>
    /// Pour chaque guerre active, détecte les provinces en contact terrestre entre
    /// attaquant et défenseur et remplit le buffer <see cref="FrontLineState"/> du secteur.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(EncirclementSystem))]
    public partial struct FrontLineSystem : ISystem
    {
        private struct ArmySnapshot
        {
            public Entity Country;
            public int ProvinceId;
            public float Strength;
        }

        private struct FortSnapshot
        {
            public int ProvinceId;
            public Entity OwnerCountry;
            public float GarrisonStrength;
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

            var provinceControllers = new NativeHashMap<int, Entity>(64, Allocator.Temp);
            foreach (var (provinceData, ownership) in SystemAPI.Query<RefRO<ProvinceData>, RefRO<ProvinceOwnership>>())
            {
                provinceControllers[provinceData.ValueRO.ProvinceId] = ownership.ValueRO.Controller;
            }

            var landAdjacency = new NativeParallelMultiHashMap<int, int>(128, Allocator.Temp);
            foreach (var (provinceData, neighbors) in SystemAPI.Query<RefRO<ProvinceData>, DynamicBuffer<ProvinceNeighbor>>())
            {
                var provinceId = provinceData.ValueRO.ProvinceId;
                for (var i = 0; i < neighbors.Length; i++)
                {
                    var neighbor = neighbors[i];
                    if (!neighbor.IsStrait)
                    {
                        landAdjacency.Add(provinceId, neighbor.NeighborProvinceId);
                    }
                }
            }

            var armySnapshots = new NativeList<ArmySnapshot>(Allocator.Temp);
            foreach (var army in SystemAPI.Query<RefRO<ArmyData>>())
            {
                armySnapshots.Add(new ArmySnapshot
                {
                    Country = army.ValueRO.Country,
                    ProvinceId = army.ValueRO.ProvinceId,
                    Strength = army.ValueRO.Strength
                });
            }

            var fortSnapshots = new NativeList<FortSnapshot>(Allocator.Temp);
            foreach (var fort in SystemAPI.Query<RefRO<FortData>>())
            {
                fortSnapshots.Add(new FortSnapshot
                {
                    ProvinceId = fort.ValueRO.ProvinceId,
                    OwnerCountry = fort.ValueRO.OwnerCountry,
                    GarrisonStrength = fort.ValueRO.GarrisonStrength
                });
            }

            var warToSector = new NativeHashMap<Entity, Entity>(8, Allocator.Temp);
            foreach (var (sector, entity) in SystemAPI.Query<RefRO<FrontSectorData>>().WithEntityAccess())
            {
                warToSector[sector.ValueRO.War] = entity;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (war, warEntity) in SystemAPI.Query<RefRO<WarData>>().WithEntityAccess())
            {
                if (!war.ValueRO.IsActive)
                {
                    continue;
                }

                if (!warToSector.ContainsKey(warEntity))
                {
                    var sectorEntity = ecb.CreateEntity();
                    ecb.AddComponent(sectorEntity, new FrontSectorData
                    {
                        War = warEntity,
                        AttackerCountry = war.ValueRO.Attacker,
                        DefenderCountry = war.ValueRO.Defender,
                        PenetrationDepth = 0f,
                        IsActive = false,
                        LastEvaluatedTick = 0
                    });
                    ecb.AddBuffer<FrontLineState>(sectorEntity);
                    warToSector[warEntity] = sectorEntity;
                }
            }

            if (ecb.ShouldPlayback)
            {
                ecb.Playback(state.EntityManager);
            }

            ecb.Dispose();

            warToSector.Clear();
            foreach (var (sector, entity) in SystemAPI.Query<RefRO<FrontSectorData>>().WithEntityAccess())
            {
                warToSector[sector.ValueRO.War] = entity;
            }

            foreach (var (war, warEntity) in SystemAPI.Query<RefRO<WarData>>().WithEntityAccess())
            {
                if (!war.ValueRO.IsActive)
                {
                    continue;
                }

                if (!warToSector.TryGetValue(warEntity, out var sectorEntity))
                {
                    continue;
                }

                var frontBuffer = state.EntityManager.GetBuffer<FrontLineState>(sectorEntity);
                frontBuffer.Clear();

                var attacker = war.ValueRO.Attacker;
                var defender = war.ValueRO.Defender;
                var provinceKeys = provinceControllers.GetKeyArray(Allocator.Temp);

                for (var i = 0; i < provinceKeys.Length; i++)
                {
                    var provinceId = provinceKeys[i];
                    if (!IsProvinceOnFront(
                            provinceId,
                            attacker,
                            defender,
                            provinceControllers,
                            landAdjacency))
                    {
                        continue;
                    }

                    var attackerPressure = 0f;
                    var defenderPressure = 0f;

                    for (var a = 0; a < armySnapshots.Length; a++)
                    {
                        var army = armySnapshots[a];
                        if (army.ProvinceId != provinceId)
                        {
                            continue;
                        }

                        if (army.Country == attacker)
                        {
                            attackerPressure += army.Strength;
                        }
                        else if (army.Country == defender)
                        {
                            defenderPressure += army.Strength;
                        }
                    }

                    for (var f = 0; f < fortSnapshots.Length; f++)
                    {
                        var fort = fortSnapshots[f];
                        if (fort.ProvinceId == provinceId && fort.OwnerCountry == defender)
                        {
                            defenderPressure += fort.GarrisonStrength;
                        }
                    }

                    frontBuffer.Add(new FrontLineState
                    {
                        ProvinceId = provinceId,
                        AttackerPressure = attackerPressure,
                        DefenderPressure = defenderPressure,
                        IsContested = attackerPressure > 0f && defenderPressure > 0f
                    });
                }

                provinceKeys.Dispose();

                var sectorData = state.EntityManager.GetComponentData<FrontSectorData>(sectorEntity);
                sectorData.IsActive = frontBuffer.Length > 0;
                sectorData.LastEvaluatedTick = currentTick;
                state.EntityManager.SetComponentData(sectorEntity, sectorData);
            }

            armySnapshots.Dispose();
            fortSnapshots.Dispose();
            warToSector.Dispose();
            landAdjacency.Dispose();
            provinceControllers.Dispose();
        }

        public void OnDestroy(ref SystemState state) { }

        private static bool IsProvinceOnFront(
            int provinceId,
            Entity attacker,
            Entity defender,
            NativeHashMap<int, Entity> provinceControllers,
            NativeParallelMultiHashMap<int, int> landAdjacency)
        {
            if (!provinceControllers.TryGetValue(provinceId, out var controller))
            {
                return false;
            }

            if (controller != attacker && controller != defender)
            {
                return false;
            }

            if (!landAdjacency.TryGetFirstValue(provinceId, out var neighborId, out var iterator))
            {
                return false;
            }

            do
            {
                if (provinceControllers.TryGetValue(neighborId, out var neighborController))
                {
                    if ((controller == attacker && neighborController == defender) ||
                        (controller == defender && neighborController == attacker))
                    {
                        return true;
                    }
                }
            }
            while (landAdjacency.TryGetNextValue(out neighborId, ref iterator));

            return false;
        }
    }
}
