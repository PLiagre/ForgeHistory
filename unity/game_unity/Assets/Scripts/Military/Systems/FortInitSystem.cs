using System.Collections.Generic;
using Unity.Entities;
using VictoriaGame.Core;
using VictoriaGame.Utils;
using VictoriaGame.World;
using UnityEngine;

namespace VictoriaGame.Military
{
    /// <summary>
    /// Crée un fort de niveau 2 sur la capitale de chaque pays.
    /// SiegeSystem consomme ces FortData mais ne les crée jamais.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(CountryInitSystem))]
    [UpdateAfter(typeof(MapInitSystem))]
    public partial struct FortInitSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WorldState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var countryByTag = IndexCountriesByTag(ref state);
            var provinceIds = IndexProvinceIds(ref state);
            var countries = GameDataLoader.LoadCountries();

            var fortsCreated = 0;
            var missingCapital = 0;

            const int CapitalFortLevel = 2;

            foreach (var def in countries)
            {
                if (string.IsNullOrEmpty(def.tag))
                {
                    continue;
                }

                if (!countryByTag.TryGetValue(def.tag, out var countryEntity))
                {
                    Debug.LogWarning(
                        $"FortInitSystem: pays introuvable pour le tag '{def.tag}' — pas de fort");
                    continue;
                }

                var capitalId = def.capital_province_id;
                if (!provinceIds.Contains(capitalId))
                {
                    Debug.LogWarning(
                        $"FortInitSystem: capitale {capitalId} introuvable pour le pays '{def.tag}' — pas de fort");
                    missingCapital++;
                    continue;
                }

                var fortEntity = em.CreateEntity();
                em.AddComponentData(fortEntity, FortData.Create(capitalId, countryEntity, CapitalFortLevel));
                fortsCreated++;
            }

            Debug.Log($"FortInitSystem: {fortsCreated} forts créés sur les capitales");

            if (missingCapital > 0)
            {
                Debug.LogWarning(
                    $"FortInitSystem: {missingCapital} pays sans capitale résolue — " +
                    "vérifier data/countries.json (capital_province_id)");
            }

            state.Enabled = false;
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        private Dictionary<string, Entity> IndexCountriesByTag(ref SystemState state)
        {
            var map = new Dictionary<string, Entity>();

            foreach (var (country, entity) in
                     SystemAPI.Query<RefRO<CountryData>>().WithEntityAccess())
            {
                var tag = country.ValueRO.Tag.ToString();
                if (!string.IsNullOrEmpty(tag))
                {
                    map[tag] = entity;
                }
            }

            return map;
        }

        private HashSet<int> IndexProvinceIds(ref SystemState state)
        {
            var set = new HashSet<int>();

            foreach (var province in SystemAPI.Query<RefRO<ProvinceData>>())
            {
                set.Add(province.ValueRO.ProvinceId);
            }

            return set;
        }
    }
}
