using Unity.Entities;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VictoriaGame.Economy;
using VictoriaGame.Politics;
using VictoriaGame.Utils;

namespace VictoriaGame.Core
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(WorldBootstrapSystem))]
    public partial struct CountryInitSystem : ISystem
    {
        /// <summary>Sentinelle CapitalProvinceId quand capital_province_id absente ou ≤ 0.</summary>
        public const int InvalidCapitalProvinceId = -1;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WorldState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var countries = GameDataLoader.LoadCountries();
            var ownerTagByProvinceId = IndexProvinceOwnerTags();
            var em = state.EntityManager;
            var missingCapital = 0;

            for (var countryRank = 0; countryRank < countries.Count; countryRank++)
            {
                var def = countries[countryRank];
                var capitalId = ResolveCapitalProvinceId(def, ownerTagByProvinceId, out var capitalOk);
                if (!capitalOk)
                {
                    missingCapital++;
                }

                var entity = em.CreateEntity();

                em.AddComponentData(entity, new CountryData
                {
                    Name = new FixedString64Bytes(def.name ?? string.Empty),
                    Tag = new FixedString32Bytes(def.tag ?? string.Empty),
                    CountryId = countryRank,
                    Population = 0,
                    Prestige = def.prestige,
                    Industrialization = 0f,
                    CapitalProvinceId = capitalId,
                });

                em.AddComponentData(entity, new GovernmentData
                {
                    Type = ParseGovType(def.gov_type),
                    Legitimacy = 0.7f,
                    Stability = math.clamp(def.stability * 0.33f, 0f, 1f),
                    Autonomy = 0.2f,
                    ReformProgress = 0,
                    RulerTag = new FixedString32Bytes(def.tag ?? string.Empty),
                    RulerAge = 40,
                    ReignStartTick = 0,
                });

                em.AddComponentData(entity, new TreasuryData
                {
                    Balance = def.treasury,
                    Income = 0f,
                    Expenses = 0f,
                    Debt = 0f,
                    // v1_016 : 0.05 → 0.02 (aligné TreasurySystem.DebtInterestRateAnnual).
                    DebtInterestRate = TreasurySystem.DebtInterestRateAnnual,
                    BankruptcyTick = 0,
                    BankruptcyCount = 0,
                });

                // v1_035 : politique fiscale par pays — défaut = ancienne constante TaxSystem.
                // IA HoldDefault : le taux reste à DefaultProductionTaxRate sauf intention joueur.
                em.AddComponentData(entity, TaxPolicyLimits.Default());

                em.AddComponentData(entity, new RevolutionData
                {
                    IsRevolutionActive = false,
                    RevolutionProgress = 0f,
                    RadicalismThreshold = 0.7f,
                    RevolutionStartTick = 0,
                    RevolutionEndTick = 0,
                });

                em.AddComponentData(entity, new TechData { MilTech = def.mil_tech, EcoTech = def.eco_tech, AdmTech = def.adm_tech });

                em.AddBuffer<EnactedLaw>(entity);
                em.AddComponentData(entity, new LawTaxMods { TaxModSum = 0f });
            }

            Debug.Log($"CountryInitSystem: {countries.Count} pays crees");

            if (missingCapital > 0)
            {
                Debug.LogWarning(
                    $"CountryInitSystem: {missingCapital} pays sans capitale valide (id résolu + owner_tag à t0) — " +
                    "bug de données dans countries.json / provinces.json, pas un réglage.");
            }
            else
            {
                Debug.Log(
                    $"CountryInitSystem: {countries.Count} capitales résolues et possédées à t0 (owner_tag).");
            }

            state.Enabled = false;
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        private static int ResolveCapitalProvinceId(
            GameDataLoader.CountryDefinition def,
            Dictionary<int, string> ownerTagByProvinceId,
            out bool ok)
        {
            ok = false;
            var capitalId = def.capital_province_id;
            if (capitalId <= 0)
            {
                Debug.LogWarning(
                    $"CountryInitSystem: '{def.tag}' sans capital_province_id — CapitalProvinceId=-1");
                return InvalidCapitalProvinceId;
            }

            if (!ownerTagByProvinceId.TryGetValue(capitalId, out var ownerTag) ||
                string.IsNullOrEmpty(ownerTag))
            {
                Debug.LogWarning(
                    $"CountryInitSystem: '{def.tag}' capitale {capitalId} introuvable dans provinces.json");
                return InvalidCapitalProvinceId;
            }

            if (!string.Equals(ownerTag, def.tag, System.StringComparison.Ordinal))
            {
                Debug.LogWarning(
                    $"CountryInitSystem: '{def.tag}' capitale {capitalId} appartient à '{ownerTag}' à t0 " +
                    "(attendu owner_tag=tag du pays) — bug de données");
                return InvalidCapitalProvinceId;
            }

            ok = true;
            return capitalId;
        }

        private static Dictionary<int, string> IndexProvinceOwnerTags()
        {
            var provinces = GameDataLoader.LoadProvinces();
            var map = new Dictionary<int, string>(provinces.Count);
            foreach (var def in provinces)
            {
                map[def.id] = def.owner_tag;
            }

            return map;
        }

        private static Politics.GovernmentType ParseGovType(string govType)
        {
            if (string.IsNullOrWhiteSpace(govType))
                return Politics.GovernmentType.Feudal;

            switch (govType.Trim().ToLowerInvariant())
            {
                case "absolute":
                    return Politics.GovernmentType.Absolute;
                case "feudal":
                    return Politics.GovernmentType.Feudal;
                case "oligarchic":
                    return Politics.GovernmentType.Oligarchic;
                case "theocratic":
                    return Politics.GovernmentType.Theocratic;
                case "republic":
                    return Politics.GovernmentType.Republic;
                default:
                    return Politics.GovernmentType.Feudal;
            }
        }
    }
}
