using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Entities;
using UnityEngine;
using VictoriaGame.Core;
using VictoriaGame.World;

namespace VictoriaGame.Economy
{
    /// <summary>
    /// Initialise la couche fantôme : buffers ProvinceStock, snapshots, singleton
    /// cargaisons / ledger / config / recettes. Strictement additif — ne modifie aucun
    /// composant de simulation existant.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(ProductionSiteInitSystem))]
    [UpdateAfter(typeof(MapInitSystem))]
    public partial struct PhysicalStockInitSystem : ISystem
    {
        const float DefaultEdgeCapacity = 500f;
        const float DefaultCapacityPerDev = 2400.643f;
        const int DefaultTransitTicks = 1;
        const float DefaultEpsilon = 1e-4f;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WorldState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var config = LoadConfig();

            // Collecte d'abord (pas de changement structurel pendant l'itération).
            var pending = new List<(Entity Entity, bool HasLand)>();
            foreach (var (neighbors, entity) in SystemAPI
                         .Query<DynamicBuffer<ProvinceNeighbor>>()
                         .WithAll<ProvinceData>()
                         .WithNone<PhysicalDemandSnapshot>()
                         .WithEntityAccess())
            {
                var hasLand = false;
                for (var i = 0; i < neighbors.Length; i++)
                {
                    if (!neighbors[i].IsStrait)
                    {
                        hasLand = true;
                        break;
                    }
                }

                pending.Add((entity, hasLand));
            }

            var isolated = 0;
            foreach (var (entity, hasLand) in pending)
            {
                if (!em.HasBuffer<ProvinceStock>(entity))
                {
                    em.AddBuffer<ProvinceStock>(entity);
                }

                if (!em.HasBuffer<PhysicalInputDeficit>(entity))
                {
                    em.AddBuffer<PhysicalInputDeficit>(entity);
                }

                em.AddComponentData(entity, new PhysicalDemandSnapshot());

                if (!hasLand)
                {
                    isolated++;
                }
            }

            if (!SystemAPI.HasSingleton<PhysicalEconomySingleton>())
            {
                var singleton = em.CreateEntity();
                em.AddComponentData(singleton, new PhysicalEconomySingleton());
                em.AddComponentData(singleton, config);
                em.AddComponentData(singleton, new PhysicalEconomyMetrics
                {
                    LandIsolatedProvinceCount = isolated
                });
                em.AddBuffer<CargoInTransit>(singleton);
                em.AddBuffer<PhysicalLedgerEntry>(singleton);
                var recipes = em.AddBuffer<PhysicalRecipeEntry>(singleton);
                LoadRecipes(recipes);
            }
            else
            {
                var singleton = SystemAPI.GetSingletonEntity<PhysicalEconomySingleton>();
                if (!em.HasBuffer<PhysicalRecipeEntry>(singleton))
                {
                    var recipes = em.AddBuffer<PhysicalRecipeEntry>(singleton);
                    LoadRecipes(recipes);
                }
            }

            Debug.Log(
                $"PhysicalStockInitSystem: {pending.Count} provinces stockées, " +
                $"{isolated} isolée(s) du réseau terrestre (détroits exclus), " +
                $"capacity={config.EdgeCapacityPerTick} perDev={config.CapacityPerDevPoint} " +
                $"delay={config.TransitTicksPerEdge}");

            state.Enabled = false;
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        [Serializable]
        class TransportFile
        {
            public float edge_capacity_per_tick = DefaultEdgeCapacity;
            public float capacity_per_dev_point = DefaultCapacityPerDev;
            public int transit_ticks_per_edge = DefaultTransitTicks;
            public float quantity_epsilon = DefaultEpsilon;
        }

        [Serializable]
        class RecipeFile
        {
            public RecipeJson[] recipes;
            public string ratio_justification;
        }

        [Serializable]
        class RecipeJson
        {
            public int output_good_id;
            public string output_tag;
            public RecipeInputJson[] inputs;
        }

        [Serializable]
        class RecipeInputJson
        {
            public int good_id;
            public string tag;
            public float qty_per_unit;
        }

        static PhysicalTransportConfig LoadConfig()
        {
            var path = Path.Combine(
                Application.streamingAssetsPath, "data", "physical_transport.json");

            if (!File.Exists(path))
            {
                Debug.LogWarning(
                    "PhysicalStockInitSystem: physical_transport.json introuvable — défauts.");
                return new PhysicalTransportConfig
                {
                    EdgeCapacityPerTick = DefaultEdgeCapacity,
                    CapacityPerDevPoint = DefaultCapacityPerDev,
                    TransitTicksPerEdge = DefaultTransitTicks,
                    QuantityEpsilon = DefaultEpsilon
                };
            }

            var data = JsonUtility.FromJson<TransportFile>(File.ReadAllText(path));
            var ticks = data.transit_ticks_per_edge;
            if (ticks < 1)
            {
                ticks = 1;
            }

            return new PhysicalTransportConfig
            {
                EdgeCapacityPerTick = data.edge_capacity_per_tick > 0f
                    ? data.edge_capacity_per_tick
                    : DefaultEdgeCapacity,
                CapacityPerDevPoint = data.capacity_per_dev_point,
                TransitTicksPerEdge = ticks,
                QuantityEpsilon = data.quantity_epsilon > 0f
                    ? data.quantity_epsilon
                    : DefaultEpsilon
            };
        }

        static void LoadRecipes(DynamicBuffer<PhysicalRecipeEntry> recipes)
        {
            recipes.Clear();
            var path = Path.Combine(
                Application.streamingAssetsPath, "data", "production_recipes.json");

            if (!File.Exists(path))
            {
                Debug.LogWarning(
                    "PhysicalStockInitSystem: production_recipes.json introuvable — " +
                    "toute production physique = LastOutput (pas d'intrants).");
                return;
            }

            var file = JsonUtility.FromJson<RecipeFile>(File.ReadAllText(path));
            var sb = new StringBuilder();
            sb.AppendLine("PhysicalStockInitSystem: recettes de production physique:");

            if (file.recipes != null)
            {
                for (var i = 0; i < file.recipes.Length; i++)
                {
                    var r = file.recipes[i];
                    if (r.inputs == null || r.inputs.Length == 0)
                    {
                        continue;
                    }

                    sb.Append($"  {r.output_tag}(id={r.output_good_id}) <- ");
                    for (var j = 0; j < r.inputs.Length; j++)
                    {
                        var inp = r.inputs[j];
                        if (j > 0)
                        {
                            sb.Append(" + ");
                        }

                        sb.Append($"{inp.qty_per_unit} {inp.tag}(id={inp.good_id})");
                        recipes.Add(new PhysicalRecipeEntry
                        {
                            OutputGoodId = r.output_good_id,
                            InputGoodId = inp.good_id,
                            QtyPerUnit = inp.qty_per_unit
                        });
                    }

                    sb.AppendLine();
                }
            }

            if (!string.IsNullOrEmpty(file.ratio_justification))
            {
                sb.AppendLine($"Justification ratios: {file.ratio_justification}");
            }

            Debug.Log(sb.ToString());
        }
    }
}
