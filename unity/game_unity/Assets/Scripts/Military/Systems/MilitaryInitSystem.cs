using System.Collections.Generic;
using Unity.Entities;
using VictoriaGame.Core;
using VictoriaGame.Utils;
using VictoriaGame.World;
using UnityEngine;

namespace VictoriaGame.Military
{
    /// <summary>
    /// Crée les entités d'entrée militaires : gabarit de recrutement sur chaque pays,
    /// dépôt de ravitaillement et armée sur la province capitale.
    /// Les systèmes Sprint 13-14 (TemplateRecruitSystem, SupplyCalculationSystem, etc.)
    /// consomment ces entités mais ne les créent jamais.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(CountryInitSystem))]
    [UpdateAfter(typeof(MapInitSystem))]
    public partial struct MilitaryInitSystem : ISystem
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

            var templatesCreated = 0;
            var hubsCreated = 0;
            var armiesCreated = 0;
            var missingCapital = 0;

            foreach (var def in countries)
            {
                if (string.IsNullOrEmpty(def.tag))
                {
                    continue;
                }

                if (!countryByTag.TryGetValue(def.tag, out var countryEntity))
                {
                    Debug.LogWarning(
                        $"MilitaryInitSystem: pays introuvable pour le tag '{def.tag}' — " +
                        "pas de gabarit, dépôt ni armée");
                    continue;
                }

                em.AddComponentData(countryEntity, new RegimentTemplate
                {
                    Country = countryEntity,
                    MilTechRequired = 0,
                    MaxRegiments = 10,
                    RecruitCostGold = 10
                });
                templatesCreated++;

                var capitalId = def.capital_province_id;
                if (!provinceIds.Contains(capitalId))
                {
                    Debug.LogWarning(
                        $"MilitaryInitSystem: capitale {capitalId} introuvable pour le pays '{def.tag}' — " +
                        "pas de dépôt ni armée");
                    missingCapital++;
                    continue;
                }

                var hubEntity = em.CreateEntity();
                em.AddComponentData(hubEntity, new SupplyHubData
                {
                    ProvinceId = capitalId,
                    MaxCapacity = 1000,
                    CurrentStock = 1000,
                    SupplyRange = 3,
                    IsActive = true
                });
                hubsCreated++;

                var armyEntity = em.CreateEntity();
                em.AddComponentData(armyEntity, new ArmyData
                {
                    ArmyGroup = Entity.Null,
                    Country = countryEntity,
                    ProvinceId = capitalId,
                    Organization = 50,
                    Morale = 50,
                    Strength = 0,
                    SupplyLevel = 1,
                    IsEngaged = false
                });
                em.AddComponentData(armyEntity, ArmySupplyState.CreateUnsupplied());
                em.AddBuffer<RegimentSlot>(armyEntity);
                em.AddBuffer<SupplyRouteData>(armyEntity);
                armiesCreated++;
            }

            Debug.Log(
                $"MilitaryInitSystem: {templatesCreated} gabarits, {hubsCreated} dépôts, {armiesCreated} armées");

            if (missingCapital > 0)
            {
                Debug.LogWarning(
                    $"MilitaryInitSystem: {missingCapital} pays sans capitale résolue — " +
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
