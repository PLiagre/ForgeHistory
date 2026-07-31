using System.Collections.Generic;
using Unity.Entities;
using Unity.Collections;
using VictoriaGame.Core;
using VictoriaGame.Utils;
using VictoriaGame.World;
using UnityEngine;

namespace VictoriaGame.Military
{
    /// <summary>
    /// Crée un groupe d'armées par pays et y rattache les armées produites par MilitaryInitSystem.
    /// Sans ce rattachement, SiegeSystem ignore toute armée (ArmyGroup = Entity.Null).
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(MilitaryInitSystem))]
    public partial struct ArmyGroupInitSystem : ISystem
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

            var armiesByCountry = CollectArmiesByCountry(ref state);

            var groupsCreated = 0;
            var armiesLinked = 0;
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
                        $"ArmyGroupInitSystem: pays introuvable pour le tag '{def.tag}' — pas de groupe");
                    continue;
                }

                var capitalId = def.capital_province_id;
                if (!provinceIds.Contains(capitalId))
                {
                    Debug.LogWarning(
                        $"ArmyGroupInitSystem: capitale {capitalId} introuvable pour '{def.tag}' — " +
                        "groupe créé sans capitale stratégique valide");
                    missingCapital++;
                }

                var groupEntity = em.CreateEntity();
                em.AddComponentData(groupEntity, new ArmyGroupData
                {
                    Name = new FixedString32Bytes(def.tag),
                    Country = countryEntity,
                    CommandingGeneral = Entity.Null,
                    Mission = ArmyMission.Advance,
                    StrategicProvinceId = capitalId,
                    Organization = 50f,
                    Morale = 50f
                });
                groupsCreated++;

                if (!armiesByCountry.TryGetValue(countryEntity, out var armies))
                {
                    continue;
                }

                foreach (var (armyEntity, armyData) in armies)
                {
                    var updated = armyData;
                    updated.ArmyGroup = groupEntity;
                    em.SetComponentData(armyEntity, updated);
                    armiesLinked++;
                }
            }

            Debug.Log(
                $"ArmyGroupInitSystem: {groupsCreated} groupes, {armiesLinked} armées rattachées");

            if (missingCapital > 0)
            {
                Debug.LogWarning(
                    $"ArmyGroupInitSystem: {missingCapital} pays sans capitale résolue — " +
                    "vérifier data/countries.json (capital_province_id)");
            }

            state.Enabled = false;
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        private Dictionary<Entity, List<(Entity Entity, ArmyData Data)>> CollectArmiesByCountry(
            ref SystemState state)
        {
            var map = new Dictionary<Entity, List<(Entity, ArmyData)>>();

            foreach (var (army, entity) in SystemAPI.Query<RefRO<ArmyData>>().WithEntityAccess())
            {
                var country = army.ValueRO.Country;
                if (!map.TryGetValue(country, out var list))
                {
                    list = new List<(Entity, ArmyData)>();
                    map[country] = list;
                }

                list.Add((entity, army.ValueRO));
            }

            return map;
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
