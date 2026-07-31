using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VictoriaGame.Core;
using VictoriaGame.Utils;

namespace VictoriaGame.Politics
{
    /// <summary>
    /// v1_089 — charge laws.json via <see cref="GameDataLoader.LoadLaws"/> (jamais appelé
    /// avant ce brief) et crée une entité <see cref="LawData"/> par loi.
    /// Ordre de création STABLE : tri par LawId (clé de domaine), jamais Entity.Index.
    /// Exécuté EN DERNIER dans l'init : créer 23 entités trop tôt décale les Entity.Index
    /// des provinces/pops/armées et casse la parité v1_009 (incident de conception v1_009).
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(VictoriaGame.Economy.BuildingInitSystem))]
    [UpdateAfter(typeof(VictoriaGame.Population.PopInitSystem))]
    [UpdateAfter(typeof(VictoriaGame.Economy.PhysicalEndowmentInitSystem))]
    public partial struct LawInitSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WorldState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var laws = GameDataLoader.LoadLaws();
            if (laws == null || laws.Count == 0)
            {
                Debug.LogWarning("LawInitSystem: aucune loi chargée (laws.json vide ou absent)");
                state.Enabled = false;
                return;
            }

            // Tri stable par id — ordre déterministe dérivé de la donnée.
            laws.Sort(CompareById);

            var em = state.EntityManager;
            for (var i = 0; i < laws.Count; i++)
            {
                var def = laws[i];
                var entity = em.CreateEntity();
                em.AddComponentData(entity, new LawData
                {
                    LawId = new FixedString32Bytes(def.id ?? string.Empty),
                    Category = (LawCategory)(byte)ClampCategory(def.category),
                    MinGovernmentTypeByte = (byte)math.max(0, def.min_government_type),
                    LegitimacyMod = def.legitimacy_mod,
                    StabilityMod = def.stability_mod,
                    TaxMod = def.tax_mod,
                    ManpowerMod = def.manpower_mod,
                    AvailableFromTick = def.available_from_tick
                });
            }

            Debug.Log($"LawInitSystem: {laws.Count} lois créées (ordre LawId)");
            state.Enabled = false;
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        static int CompareById(
            GameDataLoader.LawDefinition a,
            GameDataLoader.LawDefinition b)
        {
            var idA = a?.id ?? string.Empty;
            var idB = b?.id ?? string.Empty;
            return string.CompareOrdinal(idA, idB);
        }

        static int ClampCategory(int category)
        {
            if (category < 0)
                return 0;
            if (category > (int)LawCategory.Succession)
                return (int)LawCategory.Succession;
            return category;
        }
    }

    /// <summary>
    /// Helpers partagés : somme des tax_mod des lois en vigueur, taux effectif borné.
    /// Formule : Clamp(policyRate × (1 + Σ tax_mod)). À Σ=0 → bit-identique au taux politique.
    /// </summary>
    public static class LawTaxEffect
    {
        public static float EffectiveProductionTaxRate(float policyRate, float lawTaxModSum)
        {
            return TaxPolicyLimits.Clamp(policyRate * (1f + lawTaxModSum));
        }

        /// <summary>
        /// Somme des TaxMod des lois en vigueur d'un pays (lookup LawId → LawData).
        /// Clés de domaine uniquement.
        /// </summary>
        public static float SumTaxModForCountry(EntityManager em, Entity countryEntity)
        {
            if (countryEntity == Entity.Null)
                return 0f;
            if (em.HasComponent<LawTaxMods>(countryEntity))
                return em.GetComponentData<LawTaxMods>(countryEntity).TaxModSum;
            return 0f;
        }
    }
}
