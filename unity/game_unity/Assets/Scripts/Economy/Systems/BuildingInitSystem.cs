using Unity.Entities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VictoriaGame.Core;
using VictoriaGame.World;

namespace VictoriaGame.Economy
{
    /// <summary>
    /// Charge le catalogue buildings.json, crée le singleton métriques, et sème un parc
    /// initial de bâtiments achevés cohérent avec la production provinciale 1400
    /// (sinon intensité&gt;0 produirait zéro). Clés BuildingId stables, tri ProvinceId.
    ///
    /// v1_083 — diagnostic : <c>target</c> (l.179 historique) est une grandeur de
    /// PROVINCE (<see cref="ProductionSite.BaseOutput"/> × efficiency dérivée de
    /// <see cref="ProvinceDevelopment.Production"/>). Les villes ne sont que des
    /// destinations de répartition (<c>cities[i % Count]</c>). À
    /// <see cref="CitySeedCoefficient"/> = 0 (défaut, non adopté), le nombre total
    /// de bâtiments du monde est indépendant du nombre de villes — bit-identique.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(ProductionSiteInitSystem))]
    [UpdateAfter(typeof(CityInitSystem))]
    [UpdateAfter(typeof(PhysicalStockInitSystem))]
    public partial struct BuildingInitSystem : ISystem
    {
        /// <summary>
        /// Défaut compilé = 0 : count = f(province) uniquement (bit-identique pré-v1_083).
        /// Non adopté — proposer via LockCitySeedCoefficient en harnais.
        /// </summary>
        public const float DefaultCitySeedCoefficient = 0f;

        /// <summary>
        /// [0..+] : 0 = semis purement provincial ; &gt;0 multiplie le count par
        /// (1 + coeff × max(0, nVillesProvince − 1)). Réversible, valeur nulle
        /// bit-identique.
        /// </summary>
        public static float CitySeedCoefficient = DefaultCitySeedCoefficient;

        static bool _harnessLocked;

        public static void LockCitySeedCoefficient(float coefficient)
        {
            CitySeedCoefficient = math.max(0f, coefficient);
            _harnessLocked = true;
        }

        public static void UnlockCitySeedCoefficient()
        {
            _harnessLocked = false;
            CitySeedCoefficient = DefaultCitySeedCoefficient;
        }

        public static void ResetCitySeedCoefficientToCompiledDefault()
        {
            CitySeedCoefficient = DefaultCitySeedCoefficient;
            _harnessLocked = false;
        }

        public static bool IsCitySeedHarnessLocked => _harnessLocked;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WorldState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var catalog = LoadCatalog(em);
            var singleton = em.CreateEntity();
            em.AddComponentData(singleton, new BuildingEconomySingleton());
            var buf = em.AddBuffer<BuildingCatalogEntry>(singleton);
            for (var i = 0; i < catalog.Count; i++)
                buf.Add(catalog[i]);

            em.AddComponentData(singleton, new BuildingEconomyMetrics
            {
                SeededCompleted = 0,
                ActiveSites = 0,
                CompletedThisRun = 0,
                BlockedTicks = 0,
                WoodConsumed = 0.0,
                IronConsumed = 0.0,
                MoneySpent = 0.0,
                LastTickCpuMs = 0f
            });

            var seeded = SeedHistoricalBuildings(em, catalog);
            var metrics = em.GetComponentData<BuildingEconomyMetrics>(singleton);
            metrics.SeededCompleted = seeded;
            em.SetComponentData(singleton, metrics);

            Debug.Log(
                $"BuildingInitSystem: catalogue={catalog.Count} types, bâtiments semés={seeded} " +
                $"city_seed_coeff={CitySeedCoefficient}");
            state.Enabled = false;
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        static List<BuildingCatalogEntry> LoadCatalog(EntityManager em)
        {
            var goodIdByTag = IndexGoodsByTag(em);
            var path = Path.Combine(Application.streamingAssetsPath, "data", "buildings.json");
            var result = new List<BuildingCatalogEntry>(4);
            if (!File.Exists(path))
            {
                Debug.LogError($"BuildingInitSystem: buildings.json introuvable: {path}");
                return result;
            }

            var file = JsonUtility.FromJson<BuildingsFile>(File.ReadAllText(path));
            if (file?.buildings == null)
                return result;

            foreach (var def in file.buildings)
            {
                if (!TryParseType(def.type, out var type) ||
                    !BuildingConstructionSystem.IsConstructibleType(type))
                {
                    Debug.LogWarning($"BuildingInitSystem: type ignoré '{def.type}'");
                    continue;
                }

                var wood = 0f;
                var iron = 0f;
                if (def.materials != null)
                {
                    foreach (var m in def.materials)
                    {
                        if (m == null || string.IsNullOrEmpty(m.tag))
                            continue;
                        if (string.Equals(m.tag, "wood", StringComparison.OrdinalIgnoreCase))
                            wood = m.quantity;
                        else if (string.Equals(m.tag, "iron", StringComparison.OrdinalIgnoreCase))
                            iron = m.quantity;
                    }
                }

                goodIdByTag.TryGetValue(def.default_output_tag ?? "", out var defaultGood);
                result.Add(new BuildingCatalogEntry
                {
                    Type = type,
                    MoneyCost = def.money_cost,
                    DurationTicks = math.max(1, def.duration_ticks),
                    Capacity = math.max(0f, def.capacity),
                    DefaultOutputGoodId = defaultGood,
                    WoodCost = wood,
                    IronCost = iron
                });
            }

            result.Sort((a, b) => ((int)a.Type).CompareTo((int)b.Type));
            return result;
        }

        static int SeedHistoricalBuildings(EntityManager em, List<BuildingCatalogEntry> catalog)
        {
            var catByType = new Dictionary<BuildingType, BuildingCatalogEntry>(4);
            foreach (var c in catalog)
                catByType[c.Type] = c;

            // ProvinceId → (entity, site, goodTag, productionDev)
            var provinces = new List<(int ProvinceId, Entity Entity, ProductionSite Site, FixedString64Bytes GoodTag, float Production)>(64);
            foreach (var (prov, site, dev, entity) in SystemAPIQuerySites(em))
            {
                provinces.Add((prov.ProvinceId, entity, site, prov.GoodTag, dev.Production));
            }

            provinces.Sort((a, b) => a.ProvinceId.CompareTo(b.ProvinceId));

            // ProvinceId → villes triées CityId (destinations seulement à coeff=0).
            var citiesByProvince = new Dictionary<int, List<(int CityId, Entity Entity)>>(64);
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<CityData>()))
            using (var arr = q.ToComponentDataArray<CityData>(Allocator.Temp))
            using (var ents = q.ToEntityArray(Allocator.Temp))
            {
                var tmp = new List<(int CityId, Entity Entity, int ProvinceId)>(arr.Length);
                for (var i = 0; i < arr.Length; i++)
                    tmp.Add((arr[i].CityId, ents[i], arr[i].ProvinceId));
                tmp.Sort((a, b) =>
                {
                    var c = a.ProvinceId.CompareTo(b.ProvinceId);
                    return c != 0 ? c : a.CityId.CompareTo(b.CityId);
                });
                foreach (var t in tmp)
                {
                    if (!citiesByProvince.TryGetValue(t.ProvinceId, out var list))
                    {
                        list = new List<(int, Entity)>(2);
                        citiesByProvince[t.ProvinceId] = list;
                    }

                    list.Add((t.CityId, t.Entity));
                }
            }

            var countryByProvince = BuildCountryByProvince(em);
            var buildingId = 1;
            var seeded = 0;
            var byType = new SortedDictionary<string, int>();
            var byCountry = new SortedDictionary<string, int>();
            var cityCoeff = math.max(0f, CitySeedCoefficient);

            foreach (var p in provinces)
            {
                var type = BuildingConstructionSystem.TypeForGoodTag(p.GoodTag);
                if (!catByType.TryGetValue(type, out var cat) || cat.Capacity <= 0f)
                    continue;
                if (!citiesByProvince.TryGetValue(p.ProvinceId, out var cities) || cities.Count == 0)
                    continue;

                // target = grandeur PROVINCIALE :
                //   Site.BaseOutput (ProductionSiteInit ← ProvinceDevelopment.Production)
                //   × efficiency magique (0.5 + Production×0.05, clamp [0.1..2]).
                // Independant du nombre / de la taille des villes à cityCoeff=0.
                var efficiency = math.clamp(0.5f + p.Production * 0.05f, 0.1f, 2.0f);
                var target = p.Site.BaseOutput * efficiency;
                // Couture v1_083 : (1 + coeff × max(0, nVilles−1)). coeff=0 → bit-identique.
                var cityFactor = 1f + cityCoeff * math.max(0, cities.Count - 1);
                var count = math.max(1, (int)math.round(target * cityFactor / cat.Capacity));
                // Répartir sur les villes de la province (déterministe, ordre CityId).
                countryByProvince.TryGetValue(p.ProvinceId, out var countryId);

                for (var i = 0; i < count; i++)
                {
                    var city = cities[i % cities.Count];
                    var entity = em.CreateEntity();
                    em.AddComponentData(entity, new BuildingData
                    {
                        BuildingId = buildingId++,
                        Type = type,
                        CityId = city.CityId,
                        ProvinceId = p.ProvinceId,
                        CountryId = countryId,
                        OutputGoodId = p.Site.GoodId,
                        CapacityContribution = cat.Capacity,
                        IsComplete = 1
                    });
                    seeded++;
                    var typeKey = type.ToString();
                    byType[typeKey] = byType.TryGetValue(typeKey, out var tc) ? tc + 1 : 1;
                    var cKey = countryId.ToString();
                    byCountry[cKey] = byCountry.TryGetValue(cKey, out var cc) ? cc + 1 : 1;
                }
            }

            var sb = new StringBuilder(512);
            sb.Append("BuildingInitSystem seed: total=").Append(seeded);
            sb.Append(" city_seed_coeff=").Append(cityCoeff.ToString("0.###"));
            foreach (var kv in byType)
                sb.Append(' ').Append(kv.Key).Append('=').Append(kv.Value);
            Debug.Log(sb.ToString());
            return seeded;
        }

        // SystemAPI n'est pas dispo en static — parcours EntityManager.
        static List<(ProvinceData Prov, ProductionSite Site, ProvinceDevelopment Dev, Entity Entity)>
            SystemAPIQuerySites(EntityManager em)
        {
            var list = new List<(ProvinceData, ProductionSite, ProvinceDevelopment, Entity)>(64);
            using var q = em.CreateEntityQuery(
                ComponentType.ReadOnly<ProvinceData>(),
                ComponentType.ReadOnly<ProductionSite>(),
                ComponentType.ReadOnly<ProvinceDevelopment>());
            using var provs = q.ToComponentDataArray<ProvinceData>(Allocator.Temp);
            using var sites = q.ToComponentDataArray<ProductionSite>(Allocator.Temp);
            using var devs = q.ToComponentDataArray<ProvinceDevelopment>(Allocator.Temp);
            using var ents = q.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < ents.Length; i++)
                list.Add((provs[i], sites[i], devs[i], ents[i]));
            return list;
        }

        static Dictionary<int, int> BuildCountryByProvince(EntityManager em)
        {
            var map = new Dictionary<int, int>(64);
            using var q = em.CreateEntityQuery(
                ComponentType.ReadOnly<ProvinceData>(),
                ComponentType.ReadOnly<ProvinceOwnership>());
            using var provs = q.ToComponentDataArray<ProvinceData>(Allocator.Temp);
            using var owns = q.ToComponentDataArray<ProvinceOwnership>(Allocator.Temp);
            for (var i = 0; i < provs.Length; i++)
            {
                var owner = owns[i].Owner;
                var cid = -1;
                if (owner != Entity.Null && em.HasComponent<CountryData>(owner))
                    cid = em.GetComponentData<CountryData>(owner).CountryId;
                map[provs[i].ProvinceId] = cid;
            }

            return map;
        }

        static Dictionary<string, int> IndexGoodsByTag(EntityManager em)
        {
            var map = new Dictionary<string, int>(16);
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<GoodData>());
            using var arr = q.ToComponentDataArray<GoodData>(Allocator.Temp);
            for (var i = 0; i < arr.Length; i++)
            {
                var tag = arr[i].Tag.ToString();
                if (!string.IsNullOrEmpty(tag))
                    map[tag] = arr[i].GoodId;
            }

            return map;
        }

        static bool TryParseType(string s, out BuildingType type)
        {
            type = BuildingType.Farm;
            if (string.IsNullOrEmpty(s))
                return false;
            return Enum.TryParse(s, true, out type);
        }

        [Serializable]
        class BuildingsFile
        {
            public BuildingDef[] buildings;
            public string deferred;
        }

        [Serializable]
        class BuildingDef
        {
            public string type;
            public float money_cost;
            public int duration_ticks;
            public float capacity;
            public string default_output_tag;
            public MaterialDef[] materials;
        }

        [Serializable]
        class MaterialDef
        {
            public string tag;
            public float quantity;
        }
    }
}
