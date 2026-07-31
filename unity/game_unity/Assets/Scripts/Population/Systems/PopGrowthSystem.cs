using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using System;
using System.IO;
using UnityEngine;
using VictoriaGame.Core;
using VictoriaGame.World;

namespace VictoriaGame.Population
{
    /// <summary>Fraction de croissance accumulée entre deux ticks (évite la perte d'arrondi entier).</summary>
    public struct PopGrowthRemainder : IComponentData
    {
        public float Value;
    }

    /// <summary>
    /// Croissance démographique LOD. Réponse à la satisfaction :
    /// escalier à 4 marches (héritage) ou courbe continue dérivée des mêmes
    /// points d'ancrage (v1_026), selon <see cref="ResponseContinuity"/>.
    /// À continuité≤0 : chemin de code STRICTEMENT l'ancien (bit-identique).
    /// </summary>
    // Pas de [BurstCompile] sur le système : JSON + static mutable (BC1040), comme
    // PhysicalSatisfactionBlendSystem. Les jobs Aggregate/PopGrowth restent Burst.
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PopConsumptionSystem))]
    public partial struct PopGrowthSystem : ISystem
    {
        /// <summary>
        /// Capacité de charge par point de Manpower provincial (calibré seed 42195).
        /// capacity = Manpower * CAPACITY_PER_MANPOWER ; pop initiale ≈ Manpower * 1000.
        /// </summary>
        public const float CapacityPerManpower = 1300f;

        /// <summary>
        /// Défaut compilé (0 = escalier bit-identique). Aligné avec
        /// <c>demographic_response_continuity.json</c> après adoption mesurée.
        /// </summary>
        public const float DefaultResponseContinuity = 0.5f;

        /// <summary>
        /// Continuité de réponse [0..1]. Mutable pour les harnais de mesure.
        /// 0 → escalier original (no-op / bit-identique). 1 → interpolation continue
        /// entre les taux d'ancrage. BC1040 : lu hors Burst dans OnUpdate.
        /// </summary>
        public static float ResponseContinuity = DefaultResponseContinuity;

        /// <summary>
        /// Cadence d'écriture de <see cref="CountryData.Population"/> depuis Σ PopData.Size.
        /// 0 = désactivé (champ reste à l'init 0 → digest pré-v1_090 0x4ED26CB61DE7B2B2).
        /// N&gt;0 = réécriture tous les N ticks. Défaut 1 = chaque tick : le panneau suit
        /// la démographie sans lag ; Σ d'entiers, coût linéaire en pops.
        /// </summary>
        public const int DefaultCountryPopulationAggregationInterval = 1;

        /// <summary>Mutable pour harnais (réversibilité / mesure). BC1040 : hors Burst.</summary>
        public static int CountryPopulationAggregationInterval =
            DefaultCountryPopulationAggregationInterval;

        static bool _harnessLocked;
        static bool _jsonApplied;

        // Taux d'ancrage (INTERDIT de les modifier dans ce brief).
        public const float RateBelow02 = -0.001667f;
        public const float RateAt02 = -0.000417f;
        public const float RateAt05 = 0.000417f;
        public const float RateAt08 = 0.00125f;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WorldState>();
            ApplyJsonDefaultIfUnlocked();
        }

        // Pas de [BurstCompile] sur OnUpdate : changements structurels managés via ECB.Playback
        // (ajout paresseux de PopGrowthRemainder) + lecture static ResponseContinuity (BC1040).
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.HasSingleton<WorldState>())
            {
                return;
            }

            var worldState = SystemAPI.GetSingleton<WorldState>();
            if (worldState.IsPaused)
            {
                return;
            }

            var popsMissingRemainder = state.GetEntityQuery(
                ComponentType.ReadOnly<PopData>(),
                ComponentType.Exclude<PopGrowthRemainder>());
            if (popsMissingRemainder.CalculateEntityCount() > 0)
            {
                var ecb = new EntityCommandBuffer(Allocator.Temp);
                foreach (var (_, entity) in SystemAPI.Query<RefRO<PopData>>()
                             .WithNone<PopGrowthRemainder>().WithEntityAccess())
                {
                    ecb.AddComponent(entity, new PopGrowthRemainder());
                }

                ecb.Playback(state.EntityManager);
                ecb.Dispose();
            }

            int currentTick = worldState.CurrentTick;

            state.Dependency.Complete();

            var provincePop = new NativeHashMap<Entity, float>(64, Allocator.TempJob);

            new AggregateProvincePopJob
            {
                ProvincePop = provincePop
            }.Run();

            var provinceDevelopment = SystemAPI.GetComponentLookup<ProvinceDevelopment>(true);
            provinceDevelopment.Update(ref state);

            var job = new PopGrowthJob
            {
                CurrentTick = currentTick,
                ProvincePop = provincePop,
                ProvinceDevelopment = provinceDevelopment,
                CapacityPerManpower = CapacityPerManpower,
                ResponseContinuity = ResponseContinuity
            };
            state.Dependency = job.ScheduleParallel(state.Dependency);
            state.Dependency.Complete();

            provincePop.Dispose();

            // v1_090 : brancher CountryData.Population sur Σ PopData.Size (PopSizeAggregation).
            var interval = CountryPopulationAggregationInterval;
            if (interval > 0 && (currentTick % interval) == 0)
            {
                PopSizeAggregation.WriteCountryPopulations(state.EntityManager);
            }
        }

        /// <summary>
        /// Taux de croissance CONTINU dérivé de l'escalier : passe par les mêmes
        /// valeurs aux seuils 0.2 / 0.5 / 0.8 et interpole linéairement entre eux.
        /// </summary>
        public static float ContinuousGrowthRate(float satisfaction)
        {
            var s = math.saturate(satisfaction);
            if (s < 0.2f)
            {
                return math.lerp(RateBelow02, RateAt02, s / 0.2f);
            }

            if (s < 0.5f)
            {
                return math.lerp(RateAt02, RateAt05, (s - 0.2f) / 0.3f);
            }

            if (s < 0.8f)
            {
                return math.lerp(RateAt05, RateAt08, (s - 0.5f) / 0.3f);
            }

            return RateAt08;
        }

        /// <summary>Escalier original (trois seuils secs) — chemin de référence.</summary>
        public static float StaircaseGrowthRate(float satisfaction)
        {
            var s = satisfaction;
            var lower2 = math.select(RateBelow02, RateAt02, s >= 0.2f);
            var lower1 = math.select(lower2, RateAt05, s >= 0.5f);
            return math.select(lower1, RateAt08, s >= 0.8f);
        }

        /// <summary>
        /// Verrouille la continuité pour un harnais (empêche le JSON d'écraser).
        /// </summary>
        public static void LockContinuity(float continuity)
        {
            ResponseContinuity = math.saturate(continuity);
            _harnessLocked = true;
            _jsonApplied = true;
        }

        /// <summary>Relâche le verrou et recharge le défaut JSON / const.</summary>
        public static void UnlockContinuity()
        {
            _harnessLocked = false;
            _jsonApplied = false;
            ApplyJsonDefaultIfUnlocked();
        }

        /// <summary>Remet le static au défaut compilé (sans relire le disque).</summary>
        public static void ResetToCompiledDefault()
        {
            ResponseContinuity = DefaultResponseContinuity;
            CountryPopulationAggregationInterval = DefaultCountryPopulationAggregationInterval;
            _harnessLocked = false;
            _jsonApplied = false;
        }

        static void ApplyJsonDefaultIfUnlocked()
        {
            if (_harnessLocked || _jsonApplied)
            {
                return;
            }

            _jsonApplied = true;
            ResponseContinuity = LoadContinuityFromJson(DefaultResponseContinuity);
        }

        static float LoadContinuityFromJson(float fallback)
        {
            var path = Path.Combine(
                Application.streamingAssetsPath, "data", "demographic_response_continuity.json");

            if (!File.Exists(path))
            {
                Debug.LogWarning(
                    "PopGrowthSystem: demographic_response_continuity.json " +
                    $"introuvable — défaut const={fallback}");
                return fallback;
            }

            var data = JsonUtility.FromJson<ContinuityFile>(File.ReadAllText(path));
            var c = data.response_continuity;
            if (c < 0f || c > 1f || float.IsNaN(c) || float.IsInfinity(c))
            {
                Debug.LogWarning(
                    $"PopGrowthSystem: continuité JSON invalide ({c}) — " +
                    $"défaut const={fallback}");
                return fallback;
            }

            Debug.Log(
                $"PopGrowthSystem: response_continuity={c} " +
                $"(depuis JSON; justification={data.continuity_justification})");
            return c;
        }

        [Serializable]
        class ContinuityFile
        {
            public float response_continuity = DefaultResponseContinuity;
            public string continuity_justification = "";
        }

        [BurstCompile]
        private partial struct AggregateProvincePopJob : IJobEntity
        {
            public NativeHashMap<Entity, float> ProvincePop;

            public void Execute(in PopData pop)
            {
                if (ProvincePop.TryGetValue(pop.Province, out var current))
                {
                    ProvincePop[pop.Province] = current + pop.Size;
                }
                else
                {
                    ProvincePop[pop.Province] = pop.Size;
                }
            }
        }

        [BurstCompile]
        private partial struct PopGrowthJob : IJobEntity
        {
            public int CurrentTick;
            [ReadOnly] public NativeHashMap<Entity, float> ProvincePop;
            [ReadOnly] public ComponentLookup<ProvinceDevelopment> ProvinceDevelopment;
            public float CapacityPerManpower;
            public float ResponseContinuity;

            public void Execute(ref PopData pop, ref PopGrowthRemainder remainder)
            {
                float s = pop.NeedsSatisfaction;

                // CHEMIN ORIGINAL (escalier) — toujours calculé ; à c≤0 c'est le SEUL chemin.
                float lower2 = math.select(RateBelow02, RateAt02, s >= 0.2f);
                float lower1 = math.select(lower2, RateAt05, s >= 0.5f);
                float stair = math.select(lower1, RateAt08, s >= 0.8f);

                float rate = stair;
                // Garde-fou bit-identique : à c≤0 on ne touche PAS à rate (= stair).
                if (ResponseContinuity > 0f)
                {
                    float continuous = ContinuousGrowthRateBurst(s);
                    rate = math.lerp(stair, continuous, math.saturate(ResponseContinuity));
                }

                if (rate > 0f)
                {
                    float provincePopulation = ProvincePop.TryGetValue(pop.Province, out var p) ? p : 0f;
                    float capacity = 0f;
                    if (ProvinceDevelopment.HasComponent(pop.Province))
                    {
                        capacity = ProvinceDevelopment[pop.Province].Manpower * CapacityPerManpower;
                    }

                    float factor = capacity <= 0f
                        ? 1f
                        : math.clamp(1f - provincePopulation / capacity, 0f, 1f);
                    rate *= factor;

                    remainder.Value += pop.Size * rate;
                    int growth = (int)math.floor(remainder.Value);
                    if (growth != 0)
                    {
                        remainder.Value -= growth;
                        pop.Size = math.max(1, pop.Size + growth);
                    }
                }
                else
                {
                    remainder.Value = 0f;
                    float delta = pop.Size * rate;
                    pop.Size = math.max(1, (int)math.round(pop.Size + delta));
                }

                pop.BirthTick = CurrentTick;
            }

            /// <summary>Version Burst de ContinuousGrowthRate (pas d'appel static managé).</summary>
            static float ContinuousGrowthRateBurst(float satisfaction)
            {
                var s = math.saturate(satisfaction);
                if (s < 0.2f)
                {
                    return math.lerp(RateBelow02, RateAt02, s / 0.2f);
                }

                if (s < 0.5f)
                {
                    return math.lerp(RateAt02, RateAt05, (s - 0.2f) / 0.3f);
                }

                if (s < 0.8f)
                {
                    return math.lerp(RateAt05, RateAt08, (s - 0.5f) / 0.3f);
                }

                return RateAt08;
            }
        }
    }
}
