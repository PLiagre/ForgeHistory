using System.Collections.Generic;
using Unity.Entities;
using VictoriaGame.Core;
using VictoriaGame.Population;
using VictoriaGame.Utils;
using UnityEngine;

namespace VictoriaGame.World
{
    /// <summary>
    /// Rattache chaque province à son pays, d'après le champ owner_tag de provinces.json.
    ///
    /// Sans ce système, MapInitSystem laisse Owner/Controller/Core à Entity.Null : les 50
    /// provinces n'appartiennent à personne. Toute la chaîne en dépend — TaxSystem agrège
    /// l'impôt par propriétaire (aucun propriétaire ⇒ aucun revenu), PopInitSystem assigne
    /// PopData.Country depuis l'Owner (aucun propriétaire ⇒ pops apatrides), et
    /// NavalRecruitmentSystem exige des provinces côtières possédées pour construire.
    ///
    /// Il doit donc tourner APRÈS MapInitSystem (les provinces existent) et CountryInitSystem
    /// (les pays existent, pour résoudre le tag), mais AVANT PopInitSystem, qui lit l'Owner.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(MapInitSystem))]
    [UpdateAfter(typeof(CountryInitSystem))]
    [UpdateBefore(typeof(PopInitSystem))]
    public partial struct ProvinceOwnershipInitSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WorldState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var countryByTag = IndexCountriesByTag(ref state);
            var ownerTagByProvinceId = IndexOwnerTags();

            var assigned = 0;
            var withoutOwnerTag = 0;
            var unknownTag = 0;

            foreach (var (province, ownership) in
                     SystemAPI.Query<RefRO<ProvinceData>, RefRW<ProvinceOwnership>>())
            {
                if (!ownerTagByProvinceId.TryGetValue(province.ValueRO.ProvinceId, out var tag)
                    || string.IsNullOrEmpty(tag))
                {
                    withoutOwnerTag++;
                    continue;
                }

                if (!countryByTag.TryGetValue(tag, out var country))
                {
                    unknownTag++;
                    continue;
                }

                // Au début du scénario, le propriétaire contrôle et core ses provinces.
                // Le contrôle militaire s'en détache ensuite (SiegeSystem, FrontAdvanceSystem) ;
                // le Core, lui, ne suit pas la conquête.
                // OwnerChangedTick = 0 : provinces d'origine non traitées comme fraîchement conquises.
                ownership.ValueRW.Owner = country;
                ownership.ValueRW.Controller = country;
                ownership.ValueRW.Core = country;
                ownership.ValueRW.OwnerChangedTick = 0;
                assigned++;
            }

            Debug.Log($"ProvinceOwnershipInitSystem: {assigned} provinces rattachées à leur pays");

            if (withoutOwnerTag > 0)
            {
                Debug.LogWarning(
                    $"ProvinceOwnershipInitSystem: {withoutOwnerTag} province(s) sans owner_tag — " +
                    "elles ne rapporteront aucun impôt et leurs pops seront apatrides. " +
                    "Vérifier data/provinces.json");
            }

            if (unknownTag > 0)
            {
                Debug.LogWarning(
                    $"ProvinceOwnershipInitSystem: {unknownTag} province(s) dont l'owner_tag ne " +
                    "correspond à aucun pays de data/countries.json");
            }

            state.Enabled = false;
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        // Non statique : SystemAPI.Query est réécrit par le générateur de source DOTS,
        // qui a besoin de l'instance du système (__TypeHandle).
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

        private static Dictionary<int, string> IndexOwnerTags()
        {
            var provinces = GameDataLoader.LoadProvinces();
            var map = new Dictionary<int, string>(provinces.Count);

            foreach (var def in provinces)
            {
                map[def.id] = def.owner_tag;
            }

            return map;
        }
    }
}
