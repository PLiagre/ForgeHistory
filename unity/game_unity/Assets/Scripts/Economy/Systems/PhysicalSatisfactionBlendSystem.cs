using Unity.Entities;
using Unity.Mathematics;
using System;
using System.IO;
using UnityEngine;
using VictoriaGame.Core;
using VictoriaGame.Population;

namespace VictoriaGame.Economy
{
    /// <summary>
    /// Couture unique LOD ↔ physique (v1_022) : mélange NeedsSatisfaction.
    /// <para>
    /// effective = (1 − w) · satisfactionLOD + w · satisfactionPhysique.
    /// À w=0 : no-op strict — aucun écriture, même chemin que la couche fantôme
    /// (bit-identique). Les conséquences de la famine (mortalité, migrations)
    /// émergent via PopGrowthSystem / PopMigrationSystem qui lisent déjà
    /// NeedsSatisfaction — aucune règle magique ici.
    /// </para>
    /// Poids : const de repli + static mutable (harnais) + défaut JSON
    /// <c>data/physical_satisfaction_blend.json</c> (modèle LocalityWeight + BC1040).
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PopConsumptionSystem))]
    [UpdateBefore(typeof(PopGrowthSystem))]
    public partial struct PhysicalSatisfactionBlendSystem : ISystem
    {
        /// <summary>
        /// Défaut adopté (palier de décrochage mesuré). Doit rester aligné avec
        /// <c>physical_satisfaction_blend.json</c>.
        /// </summary>
        public const float DefaultPhysicalBlendWeight = 0.25f;

        /// <summary>
        /// Poids physique [0..1]. Mutable pour les harnais de mesure.
        /// w=0 → monde bit-identique à la couche fantôme (pas d'écriture).
        /// </summary>
        public static float PhysicalBlendWeight = DefaultPhysicalBlendWeight;

        static bool _harnessLocked;
        static bool _jsonApplied;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WorldState>();
            ApplyJsonDefaultIfUnlocked();
        }

        /// <summary>Dernier coût CPU du mélange (ms) — outillage v1_027. 0 si w≤0 (no-op).</summary>
        public static double LastTickCpuMs;

        // Pas de [BurstCompile] : lecture de PhysicalBlendWeight (static mutable) hors Burst (BC1040).
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.HasSingleton<WorldState>())
            {
                return;
            }

            if (SystemAPI.GetSingleton<WorldState>().IsPaused)
            {
                return;
            }

            // Garde-fou bit-identique : à w≤0 on ne touche RIEN.
            var w = PhysicalBlendWeight;
            if (w <= 0f)
            {
                LastTickCpuMs = 0;
                return;
            }

            var start = System.Diagnostics.Stopwatch.GetTimestamp();
            w = math.saturate(w);
            var oneMinusW = 1f - w;

            var snapshots = SystemAPI.GetComponentLookup<PhysicalDemandSnapshot>(true);
            snapshots.Update(ref state);

            foreach (var pop in SystemAPI.Query<RefRW<PopData>>())
            {
                var province = pop.ValueRO.Province;
                if (province == Entity.Null || !snapshots.HasComponent(province))
                {
                    continue;
                }

                var lodSat = pop.ValueRO.NeedsSatisfaction;
                var physSat = snapshots[province].PhysicalSatisfaction;
                pop.ValueRW.NeedsSatisfaction = oneMinusW * lodSat + w * physSat;
            }

            var end = System.Diagnostics.Stopwatch.GetTimestamp();
            LastTickCpuMs = (end - start) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        /// <summary>
        /// Verrouille le poids pour un harnais (empêche le JSON d'écraser).
        /// Appeler avant <c>RunTicks</c>.
        /// </summary>
        public static void LockWeight(float weight)
        {
            PhysicalBlendWeight = math.saturate(weight);
            _harnessLocked = true;
            _jsonApplied = true;
        }

        /// <summary>Relâche le verrou et recharge le défaut JSON / const.</summary>
        public static void UnlockWeight()
        {
            _harnessLocked = false;
            _jsonApplied = false;
            ApplyJsonDefaultIfUnlocked();
        }

        /// <summary>Remet le static au défaut compilé (sans relire le disque).</summary>
        public static void ResetToCompiledDefault()
        {
            PhysicalBlendWeight = DefaultPhysicalBlendWeight;
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
            PhysicalBlendWeight = LoadWeightFromJson(DefaultPhysicalBlendWeight);
        }

        static float LoadWeightFromJson(float fallback)
        {
            var path = Path.Combine(
                Application.streamingAssetsPath, "data", "physical_satisfaction_blend.json");

            if (!File.Exists(path))
            {
                Debug.LogWarning(
                    "PhysicalSatisfactionBlendSystem: physical_satisfaction_blend.json " +
                    $"introuvable — défaut const={fallback}");
                return fallback;
            }

            var data = JsonUtility.FromJson<BlendFile>(File.ReadAllText(path));
            var w = data.physical_blend_weight;
            if (w < 0f || w > 1f || float.IsNaN(w) || float.IsInfinity(w))
            {
                Debug.LogWarning(
                    $"PhysicalSatisfactionBlendSystem: poids JSON invalide ({w}) — " +
                    $"défaut const={fallback}");
                return fallback;
            }

            Debug.Log(
                $"PhysicalSatisfactionBlendSystem: physical_blend_weight={w} " +
                $"(depuis JSON; justification={data.weight_justification})");
            return w;
        }

        [Serializable]
        class BlendFile
        {
            public float physical_blend_weight = DefaultPhysicalBlendWeight;
            public string weight_justification = "";
        }
    }
}
