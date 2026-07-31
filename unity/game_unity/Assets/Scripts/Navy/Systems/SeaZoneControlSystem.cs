using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using VictoriaGame.Core;
using VictoriaGame.Military;

namespace VictoriaGame.Navy
{
    /// <summary>
    /// Agrège la présence navale par zone et pays, met à jour SeaZoneData et les entités NavalControl.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(NavalRecruitmentSystem))]
    public partial struct SeaZoneControlSystem : ISystem
    {
        private struct ZoneCountryKey : System.IEquatable<ZoneCountryKey>
        {
            public int ZoneId;
            public Entity Country;
            public int CountryId;

            public bool Equals(ZoneCountryKey other)
            {
                return ZoneId == other.ZoneId && Country == other.Country;
            }

            public override int GetHashCode()
            {
                return DomainKeys.HashZoneCountry(ZoneId, CountryId);
            }
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

            var zoneIsOcean = new NativeHashMap<int, bool>(16, Allocator.Temp);
            foreach (var zone in SystemAPI.Query<RefRO<SeaZoneData>>())
            {
                zoneIsOcean[zone.ValueRO.ZoneId] = zone.ValueRO.IsOcean;
            }

            var activeWars = new NativeList<WarData>(8, Allocator.Temp);
            foreach (var war in SystemAPI.Query<RefRO<WarData>>())
            {
                if (war.ValueRO.IsActive)
                {
                    activeWars.Add(war.ValueRO);
                }
            }

            var countryIds = new NativeHashMap<Entity, int>(32, Allocator.Temp);
            foreach (var (country, entity) in SystemAPI.Query<RefRO<CountryData>>().WithEntityAccess())
            {
                countryIds.TryAdd(entity, country.ValueRO.CountryId);
            }

            var presenceByZoneCountry = new NativeHashMap<ZoneCountryKey, float>(32, Allocator.Temp);

            foreach (var (navy, squadrons) in SystemAPI.Query<RefRO<NavyData>, DynamicBuffer<ShipSquadron>>())
            {
                if (navy.ValueRO.Country == Entity.Null)
                {
                    continue;
                }

                var zoneId = navy.ValueRO.SeaZoneId;
                var isOcean = zoneIsOcean.TryGetValue(zoneId, out var ocean) && ocean;
                var effectiveStrength = ComputeEffectiveStrength(squadrons, isOcean);
                if (effectiveStrength <= 0f)
                {
                    continue;
                }

                var presence = effectiveStrength
                               * math.clamp(navy.ValueRO.NavalMorale, 0f, 1f)
                               * GetMissionMultiplier(navy.ValueRO.Mission);
                if (presence <= 0f)
                {
                    continue;
                }

                countryIds.TryGetValue(navy.ValueRO.Country, out var navyCountryId);
                var key = new ZoneCountryKey
                {
                    ZoneId = zoneId,
                    Country = navy.ValueRO.Country,
                    CountryId = navyCountryId
                };

                if (presenceByZoneCountry.TryGetValue(key, out var existing))
                {
                    presenceByZoneCountry[key] = existing + presence;
                }
                else
                {
                    presenceByZoneCountry.Add(key, presence);
                }
            }

            var totalByZone = new NativeHashMap<int, float>(16, Allocator.Temp);
            var controllerByZone = new NativeHashMap<int, Entity>(16, Allocator.Temp);
            var controllerIdByZone = new NativeHashMap<int, int>(16, Allocator.Temp);
            var controllerPresenceByZone = new NativeHashMap<int, float>(16, Allocator.Temp);

            foreach (var kvp in presenceByZoneCountry)
            {
                var zoneId = kvp.Key.ZoneId;
                var presence = kvp.Value;

                if (totalByZone.TryGetValue(zoneId, out var total))
                {
                    totalByZone[zoneId] = total + presence;
                }
                else
                {
                    totalByZone.Add(zoneId, presence);
                }

                if (!controllerPresenceByZone.TryGetValue(zoneId, out var bestPresence)
                    || presence > bestPresence
                    || (math.abs(presence - bestPresence) < 1e-6f
                        && kvp.Key.CountryId < controllerIdByZone[zoneId]))
                {
                    controllerByZone[zoneId] = kvp.Key.Country;
                    controllerIdByZone[zoneId] = kvp.Key.CountryId;
                    controllerPresenceByZone[zoneId] = presence;
                }
            }

            foreach (var (zoneRw, _) in SystemAPI.Query<RefRW<SeaZoneData>>().WithEntityAccess())
            {
                var zoneId = zoneRw.ValueRO.ZoneId;
                if (!totalByZone.TryGetValue(zoneId, out var total) || total <= 0f)
                {
                    zoneRw.ValueRW.Controller = Entity.Null;
                    zoneRw.ValueRW.ControlStrength = 0f;
                    continue;
                }

                var controller = controllerByZone[zoneId];
                var controllerPresence = controllerPresenceByZone[zoneId];
                zoneRw.ValueRW.Controller = controller;
                zoneRw.ValueRW.ControlStrength = math.clamp(controllerPresence / total, 0f, 1f);
            }

            var existingControls = new NativeHashMap<ZoneCountryKey, Entity>(32, Allocator.Temp);
            foreach (var (control, controlEntity) in SystemAPI.Query<RefRO<NavalControl>>().WithEntityAccess())
            {
                countryIds.TryGetValue(control.ValueRO.Country, out var controlCountryId);
                existingControls.Add(new ZoneCountryKey
                {
                    ZoneId = control.ValueRO.SeaZoneId,
                    Country = control.ValueRO.Country,
                    CountryId = controlCountryId
                }, controlEntity);
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var kvp in presenceByZoneCountry)
            {
                var key = kvp.Key;
                var presence = kvp.Value;
                if (presence <= 0f)
                {
                    continue;
                }

                var controller = controllerByZone.TryGetValue(key.ZoneId, out var c) ? c : Entity.Null;
                var isSupremacy = key.Country == controller
                                  && !HasHostilePresenceInZone(
                                      key.ZoneId,
                                      key.Country,
                                      presenceByZoneCountry,
                                      activeWars);

                if (existingControls.TryGetValue(key, out var controlEntity))
                {
                    ecb.SetComponent(controlEntity, new NavalControl
                    {
                        SeaZoneId = key.ZoneId,
                        Country = key.Country,
                        PresenceStrength = presence,
                        IsSupremacy = isSupremacy
                    });
                    existingControls.Remove(key);
                }
                else
                {
                    var newEntity = ecb.CreateEntity();
                    ecb.AddComponent(newEntity, new NavalControl
                    {
                        SeaZoneId = key.ZoneId,
                        Country = key.Country,
                        PresenceStrength = presence,
                        IsSupremacy = isSupremacy
                    });
                }
            }

            foreach (var stale in existingControls)
            {
                ecb.DestroyEntity(stale.Value);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            activeWars.Dispose();
            existingControls.Dispose();
            controllerPresenceByZone.Dispose();
            controllerIdByZone.Dispose();
            controllerByZone.Dispose();
            totalByZone.Dispose();
            presenceByZoneCountry.Dispose();
            countryIds.Dispose();
            zoneIsOcean.Dispose();
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        private static float ComputeEffectiveStrength(DynamicBuffer<ShipSquadron> squadrons, bool isOcean)
        {
            var sum = 0f;
            for (var i = 0; i < squadrons.Length; i++)
            {
                var squadron = squadrons[i];
                if (isOcean && squadron.Type == ShipType.Galley)
                {
                    continue;
                }

                sum += squadron.Count * GetShipPower(squadron.Type)
                       * math.clamp(squadron.Condition, 0f, 1f);
            }

            return sum;
        }

        private static float GetMissionMultiplier(NavyMission mission)
        {
            switch (mission)
            {
                case NavyMission.Patrol:
                    return 1f;
                case NavyMission.Blockade:
                    return 1.5f;
                case NavyMission.Battle:
                    return 1f;
                case NavyMission.Transport:
                    return 0.5f;
                case NavyMission.ConvoyEscort:
                    return 0.75f;
                case NavyMission.ConvoyRaid:
                    return 1f;
                default:
                    return 1f;
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

        private static bool HasHostilePresenceInZone(
            int zoneId,
            Entity controller,
            NativeHashMap<ZoneCountryKey, float> presenceByZoneCountry,
            NativeList<WarData> activeWars)
        {
            foreach (var kvp in presenceByZoneCountry)
            {
                if (kvp.Key.ZoneId != zoneId || kvp.Value <= 0f)
                {
                    continue;
                }

                if (kvp.Key.Country == controller)
                {
                    continue;
                }

                if (AreAtWar(controller, kvp.Key.Country, activeWars))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool AreAtWar(Entity a, Entity b, NativeList<WarData> activeWars)
        {
            for (var i = 0; i < activeWars.Length; i++)
            {
                var war = activeWars[i];
                if ((war.Attacker == a && war.Defender == b)
                    || (war.Attacker == b && war.Defender == a))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
