using Unity.Entities;
using System.Collections.Generic;
using VictoriaGame.Core;
using VictoriaGame.Utils;
using VictoriaGame.World;
using UnityEngine;

namespace VictoriaGame.Navy
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(MapInitSystem))]
    public partial struct SeaZoneInitSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WorldState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var zones = GameDataLoader.LoadSeaZones();

            var count = 0;
            var missingLinks = 0;

            foreach (var def in zones)
            {
                var entity = state.EntityManager.CreateEntity();

                state.EntityManager.AddComponentData(entity, new SeaZoneData
                {
                    ZoneId = def.id,
                    IsOcean = def.is_ocean,
                    Controller = Entity.Null,
                    ControlStrength = 0f,
                });

                var neighbors = state.EntityManager.AddBuffer<SeaZoneNeighbor>(entity);
                AppendNeighbors(neighbors, def.adjacent_zones);

                var coasts = state.EntityManager.AddBuffer<SeaZoneCoast>(entity);
                AppendCoasts(coasts, def.coastal_provinces);

                if ((def.adjacent_zones == null || def.adjacent_zones.Count == 0) &&
                    (def.coastal_provinces == null || def.coastal_provinces.Count == 0))
                {
                    missingLinks++;
                }

                count++;
            }

            Debug.Log($"SeaZoneInitSystem: {count} zones maritimes créées");

            if (missingLinks > 0)
            {
                Debug.LogWarning(
                    $"SeaZoneInitSystem: {missingLinks} zone(s) sans voisin ni côte — " +
                    "vérifier data/sea_zones.json");
            }

            state.Enabled = false;
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        private static void AppendNeighbors(
            DynamicBuffer<SeaZoneNeighbor> buffer,
            List<int> zoneIds)
        {
            if (zoneIds == null)
            {
                return;
            }

            foreach (var zoneId in zoneIds)
            {
                buffer.Add(new SeaZoneNeighbor
                {
                    NeighborZoneId = zoneId,
                });
            }
        }

        private static void AppendCoasts(
            DynamicBuffer<SeaZoneCoast> buffer,
            List<int> provinceIds)
        {
            if (provinceIds == null)
            {
                return;
            }

            foreach (var provinceId in provinceIds)
            {
                buffer.Add(new SeaZoneCoast
                {
                    ProvinceId = provinceId,
                });
            }
        }
    }
}
