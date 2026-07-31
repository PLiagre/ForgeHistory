using System.Collections.Generic;
using Unity.Entities;
using Unity.Collections;
using VictoriaGame.Core;
using VictoriaGame.Utils;
using UnityEngine;

namespace VictoriaGame.World
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(WorldBootstrapSystem))]
    public partial struct MapInitSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WorldState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var provinces = GameDataLoader.LoadProvinces();
            var adjacencyById = IndexAdjacency(GameDataLoader.LoadProvinceAdjacency());

            var count = 0;
            var missingAdjacency = 0;

            foreach (var def in provinces)
            {
                var entity = state.EntityManager.CreateEntity();

                state.EntityManager.AddComponentData(entity, new ProvinceData
                {
                    ProvinceId = def.id,
                    Terrain = ParseTerrain(def.terrain),
                    Climate = ParseClimate(def.climate),
                    IsCoastal = def.is_coastal,
                    TradeNodeId = def.trade_node_id,
                    CultureTag = ToFixedString32(def.culture),
                    ReligionTag = ToFixedString32(def.religion),
                    GoodTag = ToFixedString32(def.good_tag),
                });

                state.EntityManager.AddComponentData(entity, new ProvinceOwnership
                {
                    Owner = Entity.Null,
                    Core = Entity.Null,
                    Controller = Entity.Null,
                    OwnerChangedTick = 0,
                });

                state.EntityManager.AddComponentData(entity, new ProvinceDevelopment
                {
                    Tax = def.base_tax,
                    Production = def.base_production,
                    Manpower = def.base_manpower,
                });

                var neighbors = state.EntityManager.AddBuffer<ProvinceNeighbor>(entity);

                if (adjacencyById.TryGetValue(def.id, out var adjacency))
                {
                    AppendNeighbors(neighbors, adjacency.neighbors, isStrait: false);
                    AppendNeighbors(neighbors, adjacency.straits, isStrait: true);
                }
                else
                {
                    // Province sans adjacence : injouable pour tout système terrestre
                    // (front, mouvement, ravitaillement). On la crée quand même, mais
                    // le compteur est remonté en warning : c'est un trou de données.
                    missingAdjacency++;
                }

                count++;
            }

            Debug.Log($"MapInitSystem: {count} provinces créées");

            if (missingAdjacency > 0)
            {
                Debug.LogWarning(
                    $"MapInitSystem: {missingAdjacency} province(s) sans adjacence — " +
                    "vérifier data/province_adjacency.json");
            }

            state.Enabled = false;
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        private static Dictionary<int, GameDataLoader.ProvinceAdjacencyDefinition> IndexAdjacency(
            List<GameDataLoader.ProvinceAdjacencyDefinition> entries)
        {
            var map = new Dictionary<int, GameDataLoader.ProvinceAdjacencyDefinition>(entries.Count);
            foreach (var entry in entries)
            {
                map[entry.id] = entry;
            }

            return map;
        }

        private static void AppendNeighbors(
            DynamicBuffer<ProvinceNeighbor> buffer,
            List<int> provinceIds,
            bool isStrait)
        {
            if (provinceIds == null)
            {
                return;
            }

            foreach (var neighborId in provinceIds)
            {
                buffer.Add(new ProvinceNeighbor
                {
                    NeighborProvinceId = neighborId,
                    IsStrait = isStrait,
                });
            }
        }

        private static TerrainType ParseTerrain(string terrain)
        {
            return terrain switch
            {
                "Plains" => TerrainType.Plains,
                "Hills" => TerrainType.Hills,
                "Mountains" => TerrainType.Mountains,
                "Desert" => TerrainType.Desert,
                "Forest" => TerrainType.Forest,
                _ => TerrainType.Plains,
            };
        }

        private static ClimateType ParseClimate(string climate)
        {
            return climate switch
            {
                "Temperate" => ClimateType.Temperate,
                "Mediterranean" => ClimateType.Mediterranean,
                "Cold" => ClimateType.Cold,
                "Arid" => ClimateType.Arid,
                _ => ClimateType.Temperate,
            };
        }

        private static FixedString32Bytes ToFixedString32(string value)
        {
            return new FixedString32Bytes(value ?? string.Empty);
        }

    }
}
