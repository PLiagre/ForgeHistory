using System;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using VictoriaGame.Core;
using VictoriaGame.Population;
using VictoriaGame.Utils;

namespace VictoriaGame.World
{
    /// <summary>
    /// Semis historique des villes (ADR-002 / v1_036).
    /// Patron MapInitSystem / CountryInitSystem : JSON → entités à l'init.
    /// Population urbaine = PART des pops provinciales (pas d'ajout au monde).
    /// Clés stables : CityId / ProvinceId (jamais Entity.Index).
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(MapInitSystem))]
    [UpdateAfter(typeof(PopInitSystem))]
    [UpdateAfter(typeof(ProvinceOwnershipInitSystem))]
    public partial struct CityInitSystem : ISystem
    {
        /// <summary>Plafond : part urbaine max d'une province (évite de sur-réclamer les pops).</summary>
        public const float MaxUrbanShareOfProvince = 0.85f;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WorldState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var citiesData = GameDataLoader.LoadCitiesData();
            var defs = citiesData.cities ?? new List<GameDataLoader.CityDefinition>();
            defs.Sort((a, b) => a.id.CompareTo(b.id));

            var em = state.EntityManager;
            var provinceById = IndexProvinces(em);
            var provincePop = MeasureProvincePopulations(em);
            var worldPopBefore = SumValues(provincePop);

            // Grouper par province (ordre CityId déjà trié).
            var byProvince = new Dictionary<int, List<GameDataLoader.CityDefinition>>(64);
            var missingProvince = 0;
            foreach (var def in defs)
            {
                if (!provinceById.ContainsKey(def.province_id))
                {
                    missingProvince++;
                    Debug.LogWarning(
                        $"CityInitSystem: ville id={def.id} '{def.name}' province_id={def.province_id} introuvable");
                    continue;
                }

                if (!byProvince.TryGetValue(def.province_id, out var list))
                {
                    list = new List<GameDataLoader.CityDefinition>(4);
                    byProvince[def.province_id] = list;
                }

                list.Add(def);
            }

            // Assurer un buffer ProvinceCity vide sur chaque province (navigabilité).
            foreach (var kv in provinceById)
            {
                if (!em.HasBuffer<ProvinceCity>(kv.Value))
                    em.AddBuffer<ProvinceCity>(kv.Value);
            }

            var created = 0;
            var urbanTotal = 0;
            var scaledProvinces = 0;
            var citiesPerProvince = new SortedDictionary<int, int>();
            var urbanPerCountry = new SortedDictionary<string, int>();

            // Créer TOUTES les villes d'abord (CreateEntity = changement structurel),
            // puis lier les buffers ProvinceCity — sinon le DynamicBuffer est invalidé.
            var pendingLinks = new List<(Entity province, int cityId, Entity city)>(defs.Count);

            foreach (var kv in byProvince)
            {
                var provinceId = kv.Key;
                var provinceEntity = provinceById[provinceId];
                var list = kv.Value;
                provincePop.TryGetValue(provinceId, out var provPop);
                var rawSum = 0;
                for (var i = 0; i < list.Count; i++)
                    rawSum += Math.Max(0, list[i].population);

                var cap = (int)(provPop * MaxUrbanShareOfProvince);
                if (cap < 0) cap = 0;
                var scale = 1f;
                if (rawSum > cap && rawSum > 0)
                {
                    scale = (float)cap / rawSum;
                    scaledProvinces++;
                }

                for (var i = 0; i < list.Count; i++)
                {
                    var def = list[i];
                    var pop = (int)(Math.Max(0, def.population) * scale);
                    if (pop < 1 && def.population > 0)
                        pop = 1;

                    var entity = em.CreateEntity();
                    em.AddComponentData(entity, new CityData
                    {
                        CityId = def.id,
                        Name = new FixedString64Bytes(def.name ?? string.Empty),
                        ProvinceId = provinceId,
                        Province = provinceEntity,
                        Population = pop,
                        Status = ParseStatus(def.status),
                    });

                    pendingLinks.Add((provinceEntity, def.id, entity));

                    created++;
                    urbanTotal += pop;
                    citiesPerProvince[provinceId] =
                        citiesPerProvince.TryGetValue(provinceId, out var c) ? c + 1 : 1;

                    var ownerTag = ResolveOwnerTag(em, provinceEntity);
                    if (!urbanPerCountry.TryGetValue(ownerTag, out var up))
                        up = 0;
                    urbanPerCountry[ownerTag] = up + pop;
                }
            }

            // Liens province → villes après tous les changements structurels.
            for (var i = 0; i < pendingLinks.Count; i++)
            {
                var (provinceEntity, cityId, cityEntity) = pendingLinks[i];
                var buf = em.GetBuffer<ProvinceCity>(provinceEntity);
                buf.Add(new ProvinceCity
                {
                    CityId = cityId,
                    City = cityEntity,
                });
            }

            var worldPopAfter = MeasureWorldPop(em);
            var urbanShare = worldPopAfter > 0 ? (double)urbanTotal / worldPopAfter : 0.0;
            var deltaPop = worldPopAfter - worldPopBefore;

            var sb = new StringBuilder(2048);
            sb.AppendLine("=== CityInitSystem v1_036 ===");
            sb.AppendLine($"criterion={citiesData.inclusion_criterion}");
            sb.AppendLine($"demographic_policy={citiesData.demographic_policy}");
            sb.AppendLine(
                $"choice=urban_population_INCLUDED_in_provincial_pops " +
                $"(CityData.Population is a share label; PopData untouched)");
            sb.AppendLine(
                $"cities_seeded={created} provinces_with_cities={citiesPerProvince.Count} " +
                $"missing_province={missingProvince} scaled_provinces={scaledProvinces}");
            sb.AppendLine(
                $"urban_total={urbanTotal} world_pop_before={worldPopBefore} " +
                $"world_pop_after={worldPopAfter} delta_world_pop={deltaPop} " +
                $"urban_share={(urbanShare * 100.0):0.###}%");
            sb.AppendLine("extensibility: buildings→CityId; growth→CityData.Population; " +
                          "new cities→CreateEntity+ProvinceCity; districts→buffer on City");
            sb.AppendLine("--- cities_per_province ---");
            foreach (var kv in citiesPerProvince)
                sb.AppendLine($"  province={kv.Key} cities={kv.Value}");
            sb.AppendLine("--- urban_per_owner_tag ---");
            foreach (var kv in urbanPerCountry)
                sb.AppendLine($"  owner={kv.Key} urban={kv.Value}");

            Debug.Log(sb.ToString());
            state.Enabled = false;
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        static Dictionary<int, Entity> IndexProvinces(EntityManager em)
        {
            var map = new Dictionary<int, Entity>(64);
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<ProvinceData>());
            using var entities = q.ToEntityArray(Allocator.Temp);
            using var data = q.ToComponentDataArray<ProvinceData>(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
                map[data[i].ProvinceId] = entities[i];
            return map;
        }

        static Dictionary<int, int> MeasureProvincePopulations(EntityManager em)
        {
            var map = new Dictionary<int, int>(64);
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<PopData>());
            using var pops = q.ToComponentDataArray<PopData>(Allocator.Temp);
            for (var i = 0; i < pops.Length; i++)
            {
                var pop = pops[i];
                if (pop.Province == Entity.Null || !em.Exists(pop.Province))
                    continue;
                if (!em.HasComponent<ProvinceData>(pop.Province))
                    continue;
                var pid = em.GetComponentData<ProvinceData>(pop.Province).ProvinceId;
                map.TryGetValue(pid, out var sum);
                map[pid] = sum + pop.Size;
            }

            return map;
        }

        static int MeasureWorldPop(EntityManager em)
        {
            var total = 0;
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<PopData>());
            using var pops = q.ToComponentDataArray<PopData>(Allocator.Temp);
            for (var i = 0; i < pops.Length; i++)
                total += pops[i].Size;
            return total;
        }

        static int SumValues(Dictionary<int, int> map)
        {
            var t = 0;
            foreach (var kv in map)
                t += kv.Value;
            return t;
        }

        static string ResolveOwnerTag(EntityManager em, Entity provinceEntity)
        {
            if (!em.HasComponent<ProvinceOwnership>(provinceEntity))
                return "?";
            var owner = em.GetComponentData<ProvinceOwnership>(provinceEntity).Owner;
            if (owner == Entity.Null || !em.HasComponent<CountryData>(owner))
                return "?";
            return em.GetComponentData<CountryData>(owner).Tag.ToString();
        }

        static CityStatus ParseStatus(string status)
        {
            if (string.IsNullOrWhiteSpace(status))
                return CityStatus.Borough;
            switch (status.Trim().ToLowerInvariant())
            {
                case "capital": return CityStatus.Capital;
                case "port": return CityStatus.Port;
                case "episcopal": return CityStatus.Episcopal;
                case "borough": return CityStatus.Borough;
                default: return CityStatus.Borough;
            }
        }
    }
}
