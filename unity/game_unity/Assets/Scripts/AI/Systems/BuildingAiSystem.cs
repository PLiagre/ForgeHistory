using System;
using System.Collections.Generic;
using System.IO;
using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VictoriaGame.Core;
using VictoriaGame.Economy;
using VictoriaGame.World;

namespace VictoriaGame.AI
{
    /// <summary>
    /// IA de construction — soumet des intentions dans le MÊME buffer que le joueur.
    /// N'écrit JAMAIS TreasuryData / BuildingData / stocks : uniquement PlayerIntention.
    ///
    /// RÈGLE DE DÉCISION (non magique, même principe que TransportServiceOrder.ByDeficitSeverity
    /// en v1_030) :
    ///   Pour chaque pays NON contrôlé par le joueur, pour chaque ville possédée sans chantier,
    ///   la sévérité locale est lue dans des états DÉJÀ OBSERVABLES :
    ///     foodGap  = max(0, FoodDemand − FoodSatisfied)     → Farm
    ///     clothGap = max(0, ClothDemand − ClothSatisfied)   → Workshop
    ///     woodDef  = PhysicalInputDeficit[wood]             → Sawmill
    ///   Le type choisi est celui du signal le plus sévère. Faisabilité d'émission =
    ///   trésorerie (après réserve budgétaire) — MÊME porte que le joueur (Apply ne
    ///   vérifie PAS les stocks). Bois/fer : le chantier (BuildingConstructionSystem)
    ///   juge tick par tick (BlockedThisTick) — pas de pré-filtre IA plus strict
    ///   que le joueur (sinon le puits reste fermé alors que Apply accepterait).
    ///   Aucun seuil de satisfaction codé, aucune cadence, aucun quota par pays.
    ///
    /// Ordre d'émission déterministe : CountryId, ProvinceId, BuildingType — jamais Entity.Index.
    /// Mode HoldNone = zéro intention (bit-identique v1_038).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(VictoriaGame.Politics.ApplyPlayerIntentionSystem))]
    public partial struct BuildingAiSystem : ISystem
    {
        const float SeverityEpsilon = 1e-4f;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WorldState>();
            state.RequireForUpdate<PlayerIntentionQueueTag>();
            state.RequireForUpdate<PlayerControl>();
            ApplyJsonDefaultIfUnlocked();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.HasSingleton<WorldState>())
                return;
            if (SystemAPI.GetSingleton<WorldState>().IsPaused)
                return;

            ApplyJsonDefaultIfUnlocked();
            if (BuildingAiPolicyConfig.Mode == BuildingAiPolicy.HoldNone)
                return;

            if (!SystemAPI.HasSingleton<BuildingEconomySingleton>())
                return;

            var worldState = SystemAPI.GetSingleton<WorldState>();
            var control = SystemAPI.GetSingleton<PlayerControl>();
            var tick = worldState.CurrentTick;
            var em = state.EntityManager;
            state.Dependency.Complete();

            var queueEntity = SystemAPI.GetSingletonEntity<PlayerIntentionQueueTag>();
            var intentionBuffer = em.GetBuffer<PlayerIntention>(queueEntity);

            // --- Cartes stables (clés de domaine, jamais Entity.Index) ---
            var treasuryByCountry = new NativeHashMap<int, float>(64, Allocator.Temp);
            foreach (var (cd, treasury) in SystemAPI
                         .Query<RefRO<CountryData>, RefRO<TreasuryData>>())
            {
                treasuryByCountry.TryAdd(cd.ValueRO.CountryId, treasury.ValueRO.Balance);
            }

            var ownerCountryIdByProvince = new NativeHashMap<int, int>(128, Allocator.Temp);
            foreach (var (prov, own) in SystemAPI.Query<RefRO<ProvinceData>, RefRO<ProvinceOwnership>>())
            {
                var owner = own.ValueRO.Owner;
                if (owner == Entity.Null || !em.HasComponent<CountryData>(owner))
                    continue;
                ownerCountryIdByProvince.TryAdd(
                    prov.ValueRO.ProvinceId,
                    em.GetComponentData<CountryData>(owner).CountryId);
            }

            var citiesBusy = new NativeHashSet<int>(32, Allocator.Temp);
            foreach (var (building, _) in SystemAPI
                         .Query<RefRO<BuildingData>, RefRO<BuildingConstruction>>())
            {
                if (building.ValueRO.IsComplete == 0)
                    citiesBusy.Add(building.ValueRO.CityId);
            }

            // Catalogue coûts (3 types) — lecture une fois.
            if (!BuildingConstructionSystem.TryGetCatalogEntry(em, BuildingType.Farm, out var farmCat) ||
                !BuildingConstructionSystem.TryGetCatalogEntry(em, BuildingType.Sawmill, out var sawCat) ||
                !BuildingConstructionSystem.TryGetCatalogEntry(em, BuildingType.Workshop, out var shopCat))
            {
                treasuryByCountry.Dispose();
                ownerCountryIdByProvince.Dispose();
                citiesBusy.Dispose();
                return;
            }

            var proposals = new NativeList<Proposal>(64, Allocator.Temp);

            foreach (var city in SystemAPI.Query<RefRO<CityData>>())
            {
                var cityId = city.ValueRO.CityId;
                var provinceId = city.ValueRO.ProvinceId;
                if (citiesBusy.Contains(cityId))
                    continue;
                if (!ownerCountryIdByProvince.TryGetValue(provinceId, out var countryId))
                    continue;
                // Le joueur décide pour son pays via l'UI — l'IA ne le double pas.
                if (countryId == control.ControlledCountryId)
                    continue;
                if (!treasuryByCountry.TryGetValue(countryId, out _))
                    continue;

                if (!TryReadLocalSignals(em, city.ValueRO.Province, provinceId,
                        out var foodGap, out var clothGap, out var woodDef))
                    continue;

                // Argmax émergent — pas d'ordre magique Farm→Sawmill→Workshop.
                var bestType = BuildingType.Farm;
                var bestSev = foodGap;
                if (clothGap > bestSev)
                {
                    bestSev = clothGap;
                    bestType = BuildingType.Workshop;
                }

                if (woodDef > bestSev)
                {
                    bestSev = woodDef;
                    bestType = BuildingType.Sawmill;
                }

                if (bestSev <= SeverityEpsilon)
                    continue;

                BuildingCatalogEntry cat;
                if (bestType == BuildingType.Farm) cat = farmCat;
                else if (bestType == BuildingType.Sawmill) cat = sawCat;
                else cat = shopCat;

                // Pas de pré-filtre stocks : Apply n'en a pas non plus (même porte).
                // BuildingConstructionSystem bloque le chantier si famine d'intrants.
                proposals.Add(new Proposal
                {
                    CountryId = countryId,
                    ProvinceId = provinceId,
                    CityId = cityId,
                    Type = bestType,
                    MoneyCost = cat.MoneyCost,
                    Severity = bestSev
                });
            }

            // Tri déterministe CountryId → ProvinceId → Type (leçon v1_009).
            proposals.Sort(default(ProposalComparer));

            var reserve = BuildingAiPolicyConfig.BudgetReserveFraction;
            var plannedSpend = new NativeHashMap<int, float>(32, Allocator.Temp);

            for (var i = 0; i < proposals.Length; i++)
            {
                var p = proposals[i];
                if (!treasuryByCountry.TryGetValue(p.CountryId, out var balance))
                    continue;
                plannedSpend.TryGetValue(p.CountryId, out var spent);
                var remaining = balance - spent;
                // balance − coût ≥ balance × reserve  ⇔  coût ≤ remaining × (1 − reserve)
                var maxSpend = remaining * (1f - reserve);
                if (p.MoneyCost > maxSpend + 1e-4f)
                    continue;

                intentionBuffer.Add(new PlayerIntention
                {
                    Kind = PlayerIntentionKind.StartBuildingConstruction,
                    CountryId = p.CountryId,
                    Value = p.CityId,
                    ValueB = (float)(int)p.Type,
                    SubmittedTick = tick
                });
                plannedSpend[p.CountryId] = spent + p.MoneyCost;
            }

            proposals.Dispose();
            plannedSpend.Dispose();
            treasuryByCountry.Dispose();
            ownerCountryIdByProvince.Dispose();
            citiesBusy.Dispose();
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        /// <summary>
        /// Signaux locaux déjà produits par la simulation physique.
        /// Retourne false si la province n'est pas trouvable.
        /// </summary>
        static bool TryReadLocalSignals(
            EntityManager em,
            Entity provinceEntity,
            int provinceId,
            out float foodGap,
            out float clothGap,
            out float woodDef)
        {
            foodGap = 0f;
            clothGap = 0f;
            woodDef = 0f;

            if (provinceEntity == Entity.Null)
            {
                // Retrouver l'entité province par ProvinceId (clé stable).
                using var q = em.CreateEntityQuery(ComponentType.ReadOnly<ProvinceData>());
                using var entities = q.ToEntityArray(Allocator.Temp);
                using var data = q.ToComponentDataArray<ProvinceData>(Allocator.Temp);
                for (var i = 0; i < data.Length; i++)
                {
                    if (data[i].ProvinceId != provinceId)
                        continue;
                    provinceEntity = entities[i];
                    break;
                }
            }

            if (provinceEntity == Entity.Null)
                return false;

            if (em.HasComponent<PhysicalDemandSnapshot>(provinceEntity))
            {
                var snap = em.GetComponentData<PhysicalDemandSnapshot>(provinceEntity);
                foodGap = math.max(0f, snap.FoodDemand - snap.FoodSatisfied);
                clothGap = math.max(0f, snap.ClothDemand - snap.ClothSatisfied);
            }

            if (em.HasBuffer<PhysicalInputDeficit>(provinceEntity))
            {
                var buf = em.GetBuffer<PhysicalInputDeficit>(provinceEntity);
                for (var i = 0; i < buf.Length; i++)
                {
                    if (buf[i].GoodId == BuildingConstructionSystem.WoodGoodId)
                        woodDef = math.max(woodDef, buf[i].Amount);
                }
            }

            return true;
        }

        static void ApplyJsonDefaultIfUnlocked()
        {
            if (BuildingAiPolicyConfig.IsHarnessLocked || BuildingAiPolicyConfig.JsonApplied)
                return;

            var path = Path.Combine(
                Application.streamingAssetsPath, "data", "building_ai.json");
            if (!File.Exists(path))
            {
                BuildingAiPolicyConfig.ApplyLoaded(
                    BuildingAiPolicyConfig.DefaultMode,
                    BuildingAiPolicyConfig.DefaultBudgetReserveFraction);
                return;
            }

            var data = JsonUtility.FromJson<AiFile>(File.ReadAllText(path));
            var modeInt = data.building_ai_mode;
            if (modeInt < 0) modeInt = 0;
            if (modeInt > 1) modeInt = 1;
            var mode = (BuildingAiPolicy)modeInt;
            var reserve = data.budget_reserve_fraction;
            if (float.IsNaN(reserve) || float.IsInfinity(reserve) || reserve < 0f || reserve > 0.9f)
                reserve = BuildingAiPolicyConfig.DefaultBudgetReserveFraction;
            BuildingAiPolicyConfig.ApplyLoaded(mode, reserve);
            Debug.Log(
                $"BuildingAiSystem: mode={mode} budget_reserve={reserve} " +
                "(règle ByDeficitSeverity — gaps Food/Cloth + déficit bois observés ; " +
                "émission si trésorerie OK ; stocks = juge chantier, pas pré-filtre IA)");
        }

        struct Proposal
        {
            public int CountryId;
            public int ProvinceId;
            public int CityId;
            public BuildingType Type;
            public float MoneyCost;
            public float Severity;
        }

        struct ProposalComparer : IComparer<Proposal>
        {
            public int Compare(Proposal a, Proposal b)
            {
                var c = a.CountryId.CompareTo(b.CountryId);
                if (c != 0) return c;
                c = a.ProvinceId.CompareTo(b.ProvinceId);
                if (c != 0) return c;
                return ((int)a.Type).CompareTo((int)b.Type);
            }
        }

        [Serializable]
        class AiFile
        {
            public int building_ai_mode = (int)BuildingAiPolicyConfig.DefaultMode;
            public float budget_reserve_fraction = BuildingAiPolicyConfig.DefaultBudgetReserveFraction;
            public string justification = "";
        }
    }
}
