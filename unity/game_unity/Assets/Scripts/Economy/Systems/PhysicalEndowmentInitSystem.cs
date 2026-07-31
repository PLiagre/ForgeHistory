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
    /// Amorçage multi-biens de la couche physique (v1_025).
    ///
    /// Dotation DÉRIVÉE du terrain/climat via terrain_endowment.json — jamais écrite
    /// province par province. Le LOD (ProductionSite unique) reste intact.
    ///
    /// Formule (documentée aussi dans le JSON) :
    ///   DevScore = max(1, (Tax + Production + Manpower) / 3)
    ///   BaseCapacity = intensity × climateMod × DevScore × ProductionScale
    /// Pas de typeYield Food ici : le FoodYield LOD reste sur ProductionSite ;
    /// l'appliquer à l'endowment inondait les stocks (dérive float conservation).
    /// ProductionScale = 2000 (aligné ProductionSiteInitSystem).
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(ProductionSiteInitSystem))]
    [UpdateAfter(typeof(PhysicalStockInitSystem))]
    public partial struct PhysicalEndowmentInitSystem : ISystem
    {
        const float DefaultProductionScale = 2000f;
        const float DefaultMinIntensity = 0.05f;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WorldState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var table = LoadTable();
            var goodIdByTag = IndexGoodsByTag(ref state);
            var pending = new List<(Entity Entity, List<ProvincePhysicalActivity> Activities)>();

            foreach (var (prov, dev, entity) in SystemAPI
                         .Query<RefRO<ProvinceData>, RefRO<ProvinceDevelopment>>()
                         .WithNone<ProvincePhysicalActivity>()
                         .WithEntityAccess())
            {
                var activities = BuildActivities(
                    prov.ValueRO, dev.ValueRO, table, goodIdByTag);
                pending.Add((entity, activities));
            }

            var totalActivities = 0;
            var multiGoodProvinces = 0;
            foreach (var (entity, activities) in pending)
            {
                var buf = em.AddBuffer<ProvincePhysicalActivity>(entity);
                for (var i = 0; i < activities.Count; i++)
                {
                    buf.Add(activities[i]);
                }

                totalActivities += activities.Count;
                if (activities.Count > 1)
                {
                    multiGoodProvinces++;
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine(
                $"PhysicalEndowmentInitSystem: {pending.Count} provinces dotées, " +
                $"{totalActivities} activités, {multiGoodProvinces} multi-biens, " +
                $"scale={table.ProductionScale}");
            sb.AppendLine(
                "Découplage: activités hors GoodId du ProductionSite LOD produisent à " +
                "BaseCapacity (pas de plafond LastOutput). Site LOD inchangé.");
            if (!string.IsNullOrEmpty(table.Justification))
            {
                sb.AppendLine($"Formule: {table.Justification}");
            }

            Debug.Log(sb.ToString());
            state.Enabled = false;
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        static List<ProvincePhysicalActivity> BuildActivities(
            ProvinceData prov,
            ProvinceDevelopment dev,
            EndowmentTable table,
            Dictionary<string, GoodIndexEntry> goodIdByTag)
        {
            // goodId → (bestIntensity, tag) — MAX si plusieurs entrées matchent.
            var best = new Dictionary<int, (float Intensity, string Tag)>();
            var terrainName = TerrainName(prov.Terrain);
            var climateName = ClimateName(prov.Climate);
            var devScore = DevScore(dev);

            for (var i = 0; i < table.Entries.Count; i++)
            {
                var e = table.Entries[i];
                if (e.RequiresCoastal && !prov.IsCoastal)
                {
                    continue;
                }

                if (!TerrainMatches(e.Terrain, terrainName))
                {
                    continue;
                }

                if (!ClimateMatches(e.Climate, climateName))
                {
                    continue;
                }

                if (string.IsNullOrEmpty(e.GoodTag) ||
                    !goodIdByTag.TryGetValue(e.GoodTag, out var good))
                {
                    continue;
                }

                var climateMod = LookupClimateMod(table, e.GoodTag, climateName);
                var intensity = e.RelativeIntensity * climateMod;
                if (intensity < table.MinIntensity)
                {
                    continue;
                }

                if (best.TryGetValue(good.GoodId, out var cur))
                {
                    if (intensity > cur.Intensity)
                    {
                        best[good.GoodId] = (intensity, e.GoodTag);
                    }
                }
                else
                {
                    best[good.GoodId] = (intensity, e.GoodTag);
                }
            }

            // Tri déterministe par GoodId (jamais Entity.Index).
            var ids = new List<int>(best.Keys);
            ids.Sort();

            var result = new List<ProvincePhysicalActivity>(ids.Count);
            for (var i = 0; i < ids.Count; i++)
            {
                var goodId = ids[i];
                var (intensity, _) = best[goodId];
                var capacity = intensity * devScore * table.ProductionScale;
                result.Add(new ProvincePhysicalActivity
                {
                    GoodId = goodId,
                    BaseCapacity = capacity,
                    RelativeIntensity = intensity
                });
            }

            return result;
        }

        public static float DevScore(ProvinceDevelopment d)
        {
            var avg = (d.Tax + d.Production + d.Manpower) / 3f;
            return avg < 1f ? 1f : avg;
        }

        static float LookupClimateMod(EndowmentTable table, string goodTag, string climate)
        {
            for (var i = 0; i < table.ClimateMods.Count; i++)
            {
                var m = table.ClimateMods[i];
                if (m.GoodTag == goodTag &&
                    string.Equals(m.Climate, climate, StringComparison.OrdinalIgnoreCase))
                {
                    return m.Multiplier > 0f ? m.Multiplier : 0f;
                }
            }

            return 1f;
        }

        static bool TerrainMatches(string entryTerrain, string provinceTerrain)
        {
            if (string.IsNullOrEmpty(entryTerrain) || entryTerrain == "*")
            {
                return true;
            }

            return string.Equals(entryTerrain, provinceTerrain, StringComparison.OrdinalIgnoreCase);
        }

        static bool ClimateMatches(string entryClimate, string provinceClimate)
        {
            if (string.IsNullOrEmpty(entryClimate) || entryClimate == "*")
            {
                return true;
            }

            return string.Equals(entryClimate, provinceClimate, StringComparison.OrdinalIgnoreCase);
        }

        static string TerrainName(TerrainType t) => t switch
        {
            TerrainType.Plains => "Plains",
            TerrainType.Hills => "Hills",
            TerrainType.Mountains => "Mountains",
            TerrainType.Desert => "Desert",
            TerrainType.Forest => "Forest",
            TerrainType.Coastal => "Coastal",
            _ => "Plains"
        };

        static string ClimateName(ClimateType c) => c switch
        {
            ClimateType.Temperate => "Temperate",
            ClimateType.Mediterranean => "Mediterranean",
            ClimateType.Cold => "Cold",
            ClimateType.Arid => "Arid",
            ClimateType.Tropical => "Tropical",
            _ => "Temperate"
        };

        struct GoodIndexEntry
        {
            public int GoodId;
            public GoodType Type;
        }

        Dictionary<string, GoodIndexEntry> IndexGoodsByTag(ref SystemState state)
        {
            var map = new Dictionary<string, GoodIndexEntry>();
            foreach (var good in SystemAPI.Query<RefRO<GoodData>>())
            {
                var tag = good.ValueRO.Tag.ToString();
                if (!string.IsNullOrEmpty(tag))
                {
                    map[tag] = new GoodIndexEntry
                    {
                        GoodId = good.ValueRO.GoodId,
                        Type = good.ValueRO.Type
                    };
                }
            }

            return map;
        }

        class EndowmentTable
        {
            public float ProductionScale = DefaultProductionScale;
            public float MinIntensity = DefaultMinIntensity;
            public string Justification = "";
            public List<EndowmentEntry> Entries = new();
            public List<ClimateModEntry> ClimateMods = new();
        }

        class EndowmentEntry
        {
            public string Terrain = "";
            public string Climate = "";
            public string GoodTag = "";
            public float RelativeIntensity;
            public bool RequiresCoastal;
        }

        class ClimateModEntry
        {
            public string GoodTag = "";
            public string Climate = "";
            public float Multiplier = 1f;
        }

        [Serializable]
        class EndowmentFile
        {
            public float production_scale = DefaultProductionScale;
            public float min_intensity = DefaultMinIntensity;
            public string capacity_justification;
            public EndowmentEntryJson[] entries;
            public ClimateModJson[] climate_mods;
        }

        [Serializable]
        class EndowmentEntryJson
        {
            public string terrain;
            public string climate;
            public string good_tag;
            public float relative_intensity = 1f;
            public bool requires_coastal;
        }

        [Serializable]
        class ClimateModJson
        {
            public string good_tag;
            public string climate;
            public float multiplier = 1f;
        }

        static EndowmentTable LoadTable()
        {
            var table = new EndowmentTable();
            var path = Path.Combine(
                Application.streamingAssetsPath, "data", "terrain_endowment.json");

            if (!File.Exists(path))
            {
                Debug.LogWarning(
                    "PhysicalEndowmentInitSystem: terrain_endowment.json introuvable — " +
                    "aucune activité physique additionnelle.");
                return table;
            }

            var file = JsonUtility.FromJson<EndowmentFile>(File.ReadAllText(path));
            if (file.production_scale > 0f)
            {
                table.ProductionScale = file.production_scale;
            }

            if (file.min_intensity > 0f)
            {
                table.MinIntensity = file.min_intensity;
            }

            table.Justification = file.capacity_justification ?? "";

            if (file.entries != null)
            {
                for (var i = 0; i < file.entries.Length; i++)
                {
                    var e = file.entries[i];
                    if (e == null || string.IsNullOrEmpty(e.good_tag))
                    {
                        continue;
                    }

                    table.Entries.Add(new EndowmentEntry
                    {
                        Terrain = e.terrain ?? "",
                        Climate = e.climate ?? "",
                        GoodTag = e.good_tag,
                        RelativeIntensity = e.relative_intensity,
                        RequiresCoastal = e.requires_coastal
                    });
                }
            }

            if (file.climate_mods != null)
            {
                for (var i = 0; i < file.climate_mods.Length; i++)
                {
                    var m = file.climate_mods[i];
                    if (m == null || string.IsNullOrEmpty(m.good_tag))
                    {
                        continue;
                    }

                    table.ClimateMods.Add(new ClimateModEntry
                    {
                        GoodTag = m.good_tag,
                        Climate = m.climate ?? "",
                        Multiplier = m.multiplier
                    });
                }
            }

            return table;
        }
    }
}
