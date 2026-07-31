using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace VictoriaGame.Utils
{
    public static class GameDataLoader
    {
        [Serializable]
        public class GoodDefinition
        {
            public int id;
            public string tag;
            public string name;
            public string type;
            public float base_price;
        }

        [Serializable]
        public class CountryDefinition
        {
            public string tag;
            public string name;
            public int capital_province_id;
            public string culture;
            public string religion;
            public string gov_type;
            public int mil_tech;
            public int eco_tech;
            public int adm_tech;
            public float prestige;
            public int stability;
            public float treasury;
        }

        [Serializable]
        public class ProvinceDefinition
        {
            public int id;
            public string name;
            public string terrain;
            public string climate;
            public bool is_coastal;
            public int base_tax;
            public int base_production;
            public int base_manpower;
            public int trade_node_id;
            public string owner_tag;
            public string culture;
            public string religion;
            public string good_tag;
        }

        /// <summary>
        /// Voisinage d'une province. neighbors = adjacence terrestre,
        /// straits = franchissements maritimes. Les deux listes sont disjointes.
        /// </summary>
        [Serializable]
        public class ProvinceAdjacencyDefinition
        {
            public int id;
            public string name;
            public List<int> neighbors;
            public List<int> straits;
        }

        [Serializable]
        public class GoodsData
        {
            public List<GoodDefinition> goods;
        }

        [Serializable]
        public class CountriesData
        {
            public List<CountryDefinition> countries;
        }

        [Serializable]
        public class ProvincesData
        {
            public List<ProvinceDefinition> provinces;
        }

        [Serializable]
        public class ProvinceAdjacencyData
        {
            public List<ProvinceAdjacencyDefinition> adjacency;
        }

        [Serializable]
        public class SeaZoneDefinition
        {
            public int id;
            public string name;
            public bool is_ocean;
            public List<int> coastal_provinces;
            public List<int> adjacent_zones;
        }

        [Serializable]
        public class SeaZonesData
        {
            public List<SeaZoneDefinition> sea_zones;
        }

        [Serializable]
        public class LawDefinition
        {
            public string id;
            public string name;
            public int category;
            public int min_government_type;
            public float legitimacy_mod;
            public float stability_mod;
            public float tax_mod;
            public float manpower_mod;
            public int available_from_tick;
        }

        [Serializable]
        public class LawsData
        {
            public List<LawDefinition> laws;
        }

        [Serializable]
        public class CityDefinition
        {
            public int id;
            public string name;
            public int province_id;
            public int population;
            public string status;
        }

        [Serializable]
        public class CitiesData
        {
            public string inclusion_criterion;
            public string demographic_policy;
            public List<CityDefinition> cities;
        }

        public static List<GoodDefinition> LoadGoods()
        {
            var path = Path.Combine(Application.streamingAssetsPath, "data", "goods.json");
            if (!File.Exists(path))
            {
                Debug.LogError($"GameDataLoader: fichier introuvable: {path}");
                return new List<GoodDefinition>();
            }

            var json = File.ReadAllText(path);
            var data = JsonUtility.FromJson<GoodsData>(json);
            return data?.goods ?? new List<GoodDefinition>();
        }

        public static List<CountryDefinition> LoadCountries()
        {
            var path = Path.Combine(Application.streamingAssetsPath, "data", "countries.json");
            if (!File.Exists(path))
            {
                Debug.LogError($"GameDataLoader: fichier introuvable: {path}");
                return new List<CountryDefinition>();
            }

            var json = File.ReadAllText(path);
            var data = JsonUtility.FromJson<CountriesData>(json);
            return data?.countries ?? new List<CountryDefinition>();
        }

        public static List<ProvinceDefinition> LoadProvinces()
        {
            var path = Path.Combine(Application.streamingAssetsPath, "data", "provinces.json");
            if (!File.Exists(path))
            {
                Debug.LogError($"GameDataLoader: fichier introuvable: {path}");
                return new List<ProvinceDefinition>();
            }

            var json = File.ReadAllText(path);
            var data = JsonUtility.FromJson<ProvincesData>(json);
            return data?.provinces ?? new List<ProvinceDefinition>();
        }

        public static List<ProvinceAdjacencyDefinition> LoadProvinceAdjacency()
        {
            var path = Path.Combine(Application.streamingAssetsPath, "data", "province_adjacency.json");
            if (!File.Exists(path))
            {
                Debug.LogError($"GameDataLoader: fichier introuvable: {path}");
                return new List<ProvinceAdjacencyDefinition>();
            }

            var json = File.ReadAllText(path);
            var data = JsonUtility.FromJson<ProvinceAdjacencyData>(json);
            return data?.adjacency ?? new List<ProvinceAdjacencyDefinition>();
        }

        public static List<SeaZoneDefinition> LoadSeaZones()
        {
            var path = Path.Combine(Application.streamingAssetsPath, "data", "sea_zones.json");
            if (!File.Exists(path))
            {
                Debug.LogError($"GameDataLoader: fichier introuvable: {path}");
                return new List<SeaZoneDefinition>();
            }

            var json = File.ReadAllText(path);
            var data = JsonUtility.FromJson<SeaZonesData>(json);
            return data?.sea_zones ?? new List<SeaZoneDefinition>();
        }

        public static List<LawDefinition> LoadLaws()
        {
            var path = Path.Combine(Application.streamingAssetsPath, "data", "laws.json");
            if (!File.Exists(path))
            {
                Debug.LogError($"GameDataLoader: fichier introuvable: {path}");
                return new List<LawDefinition>();
            }

            var json = File.ReadAllText(path);
            var data = JsonUtility.FromJson<LawsData>(json);
            return data?.laws ?? new List<LawDefinition>();
        }

        public static CitiesData LoadCitiesData()
        {
            var path = Path.Combine(Application.streamingAssetsPath, "data", "cities.json");
            return LoadCitiesDataFromPath(path);
        }

        /// <summary>Charge cities.json depuis un chemin arbitraire (mesures avant/après).</summary>
        public static CitiesData LoadCitiesDataFromPath(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogError($"GameDataLoader: fichier introuvable: {path}");
                return new CitiesData
                {
                    inclusion_criterion = string.Empty,
                    demographic_policy = string.Empty,
                    cities = new List<CityDefinition>()
                };
            }

            var json = File.ReadAllText(path);
            var data = JsonUtility.FromJson<CitiesData>(json);
            if (data == null)
            {
                return new CitiesData
                {
                    inclusion_criterion = string.Empty,
                    demographic_policy = string.Empty,
                    cities = new List<CityDefinition>()
                };
            }

            data.cities ??= new List<CityDefinition>();
            return data;
        }

        public static List<CityDefinition> LoadCities()
        {
            return LoadCitiesData().cities ?? new List<CityDefinition>();
        }
    }
}
