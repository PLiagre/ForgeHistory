using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using VictoriaGame.Core;
using VictoriaGame.Economy;
using VictoriaGame.Military;
using VictoriaGame.World;

namespace VictoriaGame.Navy
{
    /// <summary>
    /// Construit des navires pour les pays disposant d'une façade maritime,
    /// selon la trésorerie, la tech militaire et les zones maritimes accessibles.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TemplateRecruitSystem))]
    public partial struct NavalRecruitmentSystem : ISystem
    {
        private const int MILITARY_LEVEL_SCALE = 10;

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

            var zoneIsOcean = new NativeHashMap<int, bool>(16, Allocator.Temp);
            var provinceSeaZones = new NativeParallelMultiHashMap<int, int>(64, Allocator.Temp);
            foreach (var (zone, coasts) in SystemAPI.Query<RefRO<SeaZoneData>, DynamicBuffer<SeaZoneCoast>>())
            {
                zoneIsOcean[zone.ValueRO.ZoneId] = zone.ValueRO.IsOcean;
                for (var i = 0; i < coasts.Length; i++)
                {
                    provinceSeaZones.Add(coasts[i].ProvinceId, zone.ValueRO.ZoneId);
                }
            }

            var countryCoastalProvinces = new NativeParallelMultiHashMap<Entity, int>(32, Allocator.Temp);
            foreach (var (province, ownership) in SystemAPI.Query<RefRO<ProvinceData>, RefRO<ProvinceOwnership>>())
            {
                if (!province.ValueRO.IsCoastal || ownership.ValueRO.Owner == Entity.Null)
                {
                    continue;
                }

                countryCoastalProvinces.Add(ownership.ValueRO.Owner, province.ValueRO.ProvinceId);
            }

            var countryToNavy = new NativeHashMap<Entity, Entity>(32, Allocator.Temp);
            foreach (var (navy, navyEntity) in SystemAPI.Query<RefRO<NavyData>>().WithEntityAccess())
            {
                if (navy.ValueRO.Country != Entity.Null)
                {
                    countryToNavy[navy.ValueRO.Country] = navyEntity;
                }
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (treasury, tech, countryData, countryEntity) in
                     SystemAPI.Query<RefRW<TreasuryData>, RefRO<TechData>, RefRO<CountryData>>()
                         .WithEntityAccess())
            {
                if (!countryCoastalProvinces.ContainsKey(countryEntity))
                {
                    continue;
                }

                var countryId = countryData.ValueRO.CountryId;

                var canBuildGalley = CountryCanBuildGalleys(
                    countryEntity,
                    countryCoastalProvinces,
                    provinceSeaZones,
                    zoneIsOcean);

                var militaryLevel = math.clamp(tech.ValueRO.MilTech * MILITARY_LEVEL_SCALE, 0, 100);
                var shipType = SelectShipType(militaryLevel, canBuildGalley, globalSeed, currentTick, countryId);
                if (shipType < 0)
                {
                    continue;
                }

                var cost = GetShipCost((ShipType)shipType);
                if (treasury.ValueRO.Balance < cost)
                {
                    continue;
                }

                treasury.ValueRW.Balance -= cost;

                if (countryToNavy.TryGetValue(countryEntity, out var navyEntity))
                {
                    var squadrons = SystemAPI.GetBuffer<ShipSquadron>(navyEntity);
                    AddShipToSquadron(squadrons, (ShipType)shipType);

                    var navy = SystemAPI.GetComponentRW<NavyData>(navyEntity);
                    navy.ValueRW.NavalStrength = ComputeNavalStrength(squadrons);
                }
                else
                {
                    var preferNonOcean = canBuildGalley;
                    var seaZoneId = FindSeaZoneForCountry(
                        countryEntity,
                        countryCoastalProvinces,
                        provinceSeaZones,
                        zoneIsOcean,
                        globalSeed,
                        currentTick,
                        countryId,
                        preferNonOcean);

                    if (seaZoneId < 0)
                    {
                        treasury.ValueRW.Balance += cost;
                        continue;
                    }

                    var newNavyEntity = ecb.CreateEntity();
                    var navyData = NavyData.Create(countryEntity, seaZoneId, NavyMission.Patrol);
                    navyData.NavalStrength = GetShipPower((ShipType)shipType);
                    ecb.AddComponent(newNavyEntity, navyData);

                    var squadrons = ecb.AddBuffer<ShipSquadron>(newNavyEntity);
                    squadrons.Add(new ShipSquadron
                    {
                        Type = (ShipType)shipType,
                        Count = 1,
                        Condition = 1f
                    });

                    countryToNavy[countryEntity] = newNavyEntity;
                }
            }

            if (ecb.ShouldPlayback)
            {
                ecb.Playback(state.EntityManager);
            }

            ecb.Dispose();
            countryToNavy.Dispose();
            countryCoastalProvinces.Dispose();
            provinceSeaZones.Dispose();
            zoneIsOcean.Dispose();
        }

        public void OnDestroy(ref SystemState state) { }

        private static bool CountryCanBuildGalleys(
            Entity country,
            NativeParallelMultiHashMap<Entity, int> countryCoastalProvinces,
            NativeParallelMultiHashMap<int, int> provinceSeaZones,
            NativeHashMap<int, bool> zoneIsOcean)
        {
            if (!countryCoastalProvinces.TryGetFirstValue(country, out var provinceId, out var it))
            {
                return false;
            }

            do
            {
                if (provinceSeaZones.TryGetFirstValue(provinceId, out var zoneId, out var zoneIt))
                {
                    do
                    {
                        if (zoneIsOcean.TryGetValue(zoneId, out var isOcean) && !isOcean)
                        {
                            return true;
                        }
                    }
                    while (provinceSeaZones.TryGetNextValue(out zoneId, ref zoneIt));
                }
            }
            while (countryCoastalProvinces.TryGetNextValue(out provinceId, ref it));

            return false;
        }

        private static int FindSeaZoneForCountry(
            Entity country,
            NativeParallelMultiHashMap<Entity, int> countryCoastalProvinces,
            NativeParallelMultiHashMap<int, int> provinceSeaZones,
            NativeHashMap<int, bool> zoneIsOcean,
            uint globalSeed,
            int tick,
            int countryId,
            bool preferNonOcean)
        {
            var candidates = new NativeList<int>(8, Allocator.Temp);

            if (countryCoastalProvinces.TryGetFirstValue(country, out var provinceId, out var it))
            {
                do
                {
                    if (provinceSeaZones.TryGetFirstValue(provinceId, out var zoneId, out var zoneIt))
                    {
                        do
                        {
                            if (!ContainsZone(candidates, zoneId))
                            {
                                if (!preferNonOcean)
                                {
                                    candidates.Add(zoneId);
                                }
                                else if (zoneIsOcean.TryGetValue(zoneId, out var isOcean) && !isOcean)
                                {
                                    candidates.Add(zoneId);
                                }
                            }
                        }
                        while (provinceSeaZones.TryGetNextValue(out zoneId, ref zoneIt));
                    }
                }
                while (countryCoastalProvinces.TryGetNextValue(out provinceId, ref it));
            }

            if (candidates.Length == 0 && preferNonOcean)
            {
                if (countryCoastalProvinces.TryGetFirstValue(country, out provinceId, out it))
                {
                    do
                    {
                        if (provinceSeaZones.TryGetFirstValue(provinceId, out var zoneId, out var zoneIt))
                        {
                            do
                            {
                                if (!ContainsZone(candidates, zoneId))
                                {
                                    candidates.Add(zoneId);
                                }
                            }
                            while (provinceSeaZones.TryGetNextValue(out zoneId, ref zoneIt));
                        }
                    }
                    while (countryCoastalProvinces.TryGetNextValue(out provinceId, ref it));
                }
            }

            if (candidates.Length == 0)
            {
                candidates.Dispose();
                return -1;
            }

            var rng = CreateRecruitmentRandom(globalSeed, tick, countryId);
            var index = rng.NextInt(0, candidates.Length);
            var selected = candidates[index];
            candidates.Dispose();
            return selected;
        }

        private static bool ContainsZone(NativeList<int> zones, int zoneId)
        {
            for (var i = 0; i < zones.Length; i++)
            {
                if (zones[i] == zoneId)
                {
                    return true;
                }
            }

            return false;
        }

        private static int SelectShipType(
            int militaryLevel,
            bool canBuildGalley,
            uint globalSeed,
            int tick,
            int countryId)
        {
            var available = new NativeList<int>(9, Allocator.Temp);

            for (var t = (int)ShipType.Ironclad; t >= 0; t--)
            {
                var type = (ShipType)t;
                if (IsShipAvailable(type, militaryLevel, canBuildGalley))
                {
                    available.Add(t);
                }
            }

            if (available.Length == 0)
            {
                available.Dispose();
                return -1;
            }

            var rng = CreateRecruitmentRandom(globalSeed, tick, countryId);
            var pick = rng.NextInt(0, available.Length);
            var selected = available[pick];
            available.Dispose();
            return selected;
        }

        private static bool IsShipAvailable(ShipType type, int militaryLevel, bool canBuildGalley)
        {
            switch (type)
            {
                case ShipType.Galley:
                    return canBuildGalley && militaryLevel >= 0 && militaryLevel <= 40;
                case ShipType.Cog:
                    return militaryLevel >= 0 && militaryLevel <= 20;
                case ShipType.Carrack:
                    return militaryLevel >= 10 && militaryLevel <= 44;
                case ShipType.Galleon:
                    return militaryLevel >= 26 && militaryLevel <= 60;
                case ShipType.Frigate:
                    return militaryLevel >= 50 && militaryLevel <= 100;
                case ShipType.ShipOfLine:
                    return militaryLevel >= 50 && militaryLevel <= 92;
                case ShipType.ManOfWar:
                    return militaryLevel >= 60 && militaryLevel <= 92;
                case ShipType.SteamFrigate:
                    return militaryLevel >= 84 && militaryLevel <= 100;
                case ShipType.Ironclad:
                    return militaryLevel >= 92 && militaryLevel <= 100;
                default:
                    return false;
            }
        }

        private static float GetShipCost(ShipType type)
        {
            switch (type)
            {
                case ShipType.Galley: return 20f;
                case ShipType.Cog: return 25f;
                case ShipType.Carrack: return 40f;
                case ShipType.Galleon: return 60f;
                case ShipType.Frigate: return 50f;
                case ShipType.ShipOfLine: return 80f;
                case ShipType.ManOfWar: return 100f;
                case ShipType.SteamFrigate: return 90f;
                case ShipType.Ironclad: return 120f;
                default: return 50f;
            }
        }

        private static float GetShipPower(ShipType type)
        {
            switch (type)
            {
                case ShipType.Galley: return 1f;
                case ShipType.Cog: return 1f;
                case ShipType.Carrack: return 2f;
                case ShipType.Galleon: return 3f;
                case ShipType.Frigate: return 3f;
                case ShipType.ShipOfLine: return 5f;
                case ShipType.ManOfWar: return 6f;
                case ShipType.SteamFrigate: return 5f;
                case ShipType.Ironclad: return 7f;
                default: return 1f;
            }
        }

        private static void AddShipToSquadron(DynamicBuffer<ShipSquadron> squadrons, ShipType type)
        {
            for (var i = 0; i < squadrons.Length; i++)
            {
                if (squadrons[i].Type == type)
                {
                    var squadron = squadrons[i];
                    squadron.Count++;
                    squadrons[i] = squadron;
                    return;
                }
            }

            squadrons.Add(new ShipSquadron
            {
                Type = type,
                Count = 1,
                Condition = 1f
            });
        }

        private static float ComputeNavalStrength(DynamicBuffer<ShipSquadron> squadrons)
        {
            var sum = 0f;
            for (var i = 0; i < squadrons.Length; i++)
            {
                var squadron = squadrons[i];
                sum += squadron.Count * GetShipPower(squadron.Type) * math.clamp(squadron.Condition, 0f, 1f);
            }

            return sum;
        }

        private static Random CreateRecruitmentRandom(uint globalSeed, int tick, int countryId)
        {
            var seed = math.hash(new uint3(globalSeed, (uint)tick, (uint)countryId));
            return Random.CreateFromIndex(seed);
        }
    }
}
