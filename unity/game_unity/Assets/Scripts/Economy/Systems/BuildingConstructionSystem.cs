using System;
using System.Collections.Generic;
using System.IO;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VictoriaGame.Core;
using VictoriaGame.World;

namespace VictoriaGame.Economy
{
    /// <summary>
    /// Avance les chantiers : consomme bois/fer du stock provincial tick par tick,
    /// inscrit la consommation au ledger physique, complète le bâtiment quand
    /// durée ET matériaux sont satisfaits. L'IA de construction est dans
    /// VictoriaGame.AI.BuildingAiSystem (intentions → ApplyPlayerIntention).
    ///
    /// Porte aussi l'intensité réversible de capacité bâtiment
    /// (<see cref="CapacityIntensity"/>) — défaut adopté v1_038 = 1.0
    /// (0 = bit-identique, capacité = scalaire développement).
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(VictoriaGame.Politics.ApplyPlayerIntentionSystem))]
    [UpdateBefore(typeof(PhysicalProductionSystem))]
    public partial struct BuildingConstructionSystem : ISystem
    {
        /// <summary>
        /// Intensité adoptée v1_038 (balayage t3000 seed=42195) : palier 1.0 —
        /// plus fort qui garde le monde vivant (sat≈0.64, pop≈154k).
        /// Aligné building_capacity.json. LockCapacityIntensity(x) pour forcer en harnais.
        /// 0 = bit-identique (capacité = scalaire développement).
        /// </summary>
        public const float DefaultCapacityIntensity = 1f;
        public const int WoodGoodId = 4;
        public const int IronGoodId = 5;

        /// <summary>
        /// [0..1] : 0 = capacité entièrement du scalaire de développement (bit-identique) ;
        /// 1 = capacité entièrement des bâtiments achevés.
        /// </summary>
        public static float CapacityIntensity = DefaultCapacityIntensity;

        static bool _harnessLocked;
        static bool _jsonApplied;

        public static double LastTickCpuMs;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WorldState>();
            ApplyJsonDefaultIfUnlocked();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.HasSingleton<WorldState>())
                return;
            if (SystemAPI.GetSingleton<WorldState>().IsPaused)
                return;

            var start = System.Diagnostics.Stopwatch.GetTimestamp();
            state.Dependency.Complete();
            AdvanceSites(ref state);
            var end = System.Diagnostics.Stopwatch.GetTimestamp();
            LastTickCpuMs = (end - start) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;

            if (SystemAPI.HasSingleton<BuildingEconomyMetrics>())
            {
                var metrics = SystemAPI.GetSingleton<BuildingEconomyMetrics>();
                metrics.LastTickCpuMs = (float)LastTickCpuMs;
                SystemAPI.SetSingleton(metrics);
            }
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        public static void LockCapacityIntensity(float intensity)
        {
            CapacityIntensity = math.saturate(intensity);
            _harnessLocked = true;
            _jsonApplied = true;
        }

        public static void UnlockCapacityIntensity()
        {
            _harnessLocked = false;
            _jsonApplied = false;
            ApplyJsonDefaultIfUnlocked();
        }

        public static void ResetToCompiledDefault()
        {
            CapacityIntensity = DefaultCapacityIntensity;
            _harnessLocked = false;
            _jsonApplied = false;
        }

        static void ApplyJsonDefaultIfUnlocked()
        {
            if (_harnessLocked || _jsonApplied)
                return;
            _jsonApplied = true;
            LoadFromJson(DefaultCapacityIntensity);
        }

        static void LoadFromJson(float fallback)
        {
            var path = Path.Combine(
                Application.streamingAssetsPath, "data", "building_capacity.json");
            if (!File.Exists(path))
            {
                Debug.LogWarning(
                    "BuildingConstructionSystem: building_capacity.json introuvable — " +
                    $"défaut intensity={fallback}");
                CapacityIntensity = fallback;
                return;
            }

            var data = JsonUtility.FromJson<CapacityFile>(File.ReadAllText(path));
            var intensity = data.building_capacity_intensity;
            if (intensity < 0f || intensity > 1f || float.IsNaN(intensity) || float.IsInfinity(intensity))
            {
                Debug.LogWarning(
                    $"BuildingConstructionSystem: intensity JSON invalide ({intensity}) — défaut={fallback}");
                intensity = fallback;
            }

            CapacityIntensity = intensity;
            Debug.Log($"BuildingConstructionSystem: capacity_intensity={intensity} (depuis JSON)");
        }

        [Serializable]
        class CapacityFile
        {
            public float building_capacity_intensity = DefaultCapacityIntensity;
            public string intensity_justification = "";
        }

        void AdvanceSites(ref SystemState state)
        {
            if (!SystemAPI.HasSingleton<PhysicalEconomySingleton>())
                return;

            var em = state.EntityManager;
            var singletonEntity = SystemAPI.GetSingletonEntity<PhysicalEconomySingleton>();
            var ledger = em.GetBuffer<PhysicalLedgerEntry>(singletonEntity);
            var epsilon = 1e-6f;
            if (SystemAPI.HasSingleton<PhysicalTransportConfig>())
                epsilon = SystemAPI.GetSingleton<PhysicalTransportConfig>().QuantityEpsilon;

            var provinceById = new NativeHashMap<int, Entity>(64, Allocator.Temp);
            foreach (var (prov, entity) in SystemAPI.Query<RefRO<ProvinceData>>().WithEntityAccess())
                provinceById.TryAdd(prov.ValueRO.ProvinceId, entity);

            var completed = 0;
            var blocked = 0;
            double woodConsumed = 0.0;
            double ironConsumed = 0.0;
            var active = 0;

            var toComplete = new NativeList<Entity>(8, Allocator.Temp);

            foreach (var (building, construction, entity) in SystemAPI
                         .Query<RefRW<BuildingData>, RefRW<BuildingConstruction>>()
                         .WithEntityAccess())
            {
                if (building.ValueRO.IsComplete != 0)
                    continue;

                active++;
                construction.ValueRW.BlockedThisTick = 0;

                if (!provinceById.TryGetValue(building.ValueRO.ProvinceId, out var provEntity) ||
                    !em.HasBuffer<ProvinceStock>(provEntity))
                {
                    construction.ValueRW.BlockedThisTick = 1;
                    blocked++;
                    continue;
                }

                var stock = em.GetBuffer<ProvinceStock>(provEntity);
                var dur = math.max(1, construction.ValueRO.DurationTicks);
                var woodLeft = math.max(0f, construction.ValueRO.WoodTotal - construction.ValueRO.WoodDelivered);
                var ironLeft = math.max(0f, construction.ValueRO.IronTotal - construction.ValueRO.IronDelivered);
                var ticksLeft = math.max(1, dur - construction.ValueRO.ProgressTicks);
                // Redistribuer le restant sur les ticks restants — évite le sous-prélèvement
                // flottant (40/12×12 < 40) qui bloquait la complétion à jamais.
                var woodNeed = woodLeft / ticksLeft;
                var ironNeed = ironLeft / ticksLeft;

                var woodAvail = PhysicalStockSystem.GetStockQuantity(stock, WoodGoodId);
                var ironAvail = PhysicalStockSystem.GetStockQuantity(stock, IronGoodId);

                if ((woodNeed > epsilon && woodAvail + 1e-9 < woodNeed) ||
                    (ironNeed > epsilon && ironAvail + 1e-9 < ironNeed))
                {
                    construction.ValueRW.BlockedThisTick = 1;
                    blocked++;
                    continue;
                }

                if (woodNeed > epsilon)
                {
                    var taken = PhysicalStockSystem.TryRemoveFromStock(stock, WoodGoodId, woodNeed);
                    PhysicalStockSystem.AddLedgerConsumptionPublic(ledger, WoodGoodId, taken);
                    construction.ValueRW.WoodDelivered += (float)taken;
                    woodConsumed += taken;
                }

                if (ironNeed > epsilon)
                {
                    var taken = PhysicalStockSystem.TryRemoveFromStock(stock, IronGoodId, ironNeed);
                    PhysicalStockSystem.AddLedgerConsumptionPublic(ledger, IronGoodId, taken);
                    construction.ValueRW.IronDelivered += (float)taken;
                    ironConsumed += taken;
                }

                construction.ValueRW.ProgressTicks += 1;

                var materialsDone =
                    construction.ValueRO.WoodDelivered + epsilon >= construction.ValueRO.WoodTotal &&
                    construction.ValueRO.IronDelivered + epsilon >= construction.ValueRO.IronTotal;
                var timeDone = construction.ValueRO.ProgressTicks >= construction.ValueRO.DurationTicks;

                if (materialsDone && timeDone)
                    toComplete.Add(entity);
            }

            for (var i = 0; i < toComplete.Length; i++)
            {
                var entity = toComplete[i];
                var data = em.GetComponentData<BuildingData>(entity);
                data.IsComplete = 1;
                em.SetComponentData(entity, data);
                if (em.HasComponent<BuildingConstruction>(entity))
                    em.RemoveComponent<BuildingConstruction>(entity);
                completed++;
            }

            provinceById.Dispose();
            toComplete.Dispose();

            if (SystemAPI.HasSingleton<BuildingEconomyMetrics>())
            {
                var metrics = SystemAPI.GetSingleton<BuildingEconomyMetrics>();
                metrics.ActiveSites = active;
                metrics.CompletedThisRun += completed;
                metrics.BlockedTicks += blocked;
                metrics.WoodConsumed += woodConsumed;
                metrics.IronConsumed += ironConsumed;
                SystemAPI.SetSingleton(metrics);
            }
        }

        /// <summary>
        /// Publie le besoin matériaux des chantiers actifs dans PhysicalInputDeficit
        /// (après le clear/rebuild de PhysicalProductionSystem). Sans ça, le transport
        /// ByDeficitSeverity ne voit jamais la famine de chantier — bois/fer restent
        /// à woodCons=0 alors que MoneySpent &gt; 0 (constat v1_039).
        /// </summary>
        public static void RegisterConstructionMaterialDemand(EntityManager em)
        {
            using var q = em.CreateEntityQuery(
                ComponentType.ReadOnly<BuildingData>(),
                ComponentType.ReadOnly<BuildingConstruction>());
            if (q.IsEmptyIgnoreFilter)
                return;

            using var buildings = q.ToComponentDataArray<BuildingData>(Allocator.Temp);
            using var constructions = q.ToComponentDataArray<BuildingConstruction>(Allocator.Temp);

            var provinceById = new NativeHashMap<int, Entity>(64, Allocator.Temp);
            using (var pq = em.CreateEntityQuery(ComponentType.ReadOnly<ProvinceData>()))
            using (var pdata = pq.ToComponentDataArray<ProvinceData>(Allocator.Temp))
            using (var pents = pq.ToEntityArray(Allocator.Temp))
            {
                for (var i = 0; i < pdata.Length; i++)
                    provinceById.TryAdd(pdata[i].ProvinceId, pents[i]);
            }

            // Agréger par ProvinceId (clés stables) puis écrire les déficits.
            var woodNeedByProv = new NativeHashMap<int, float>(32, Allocator.Temp);
            var ironNeedByProv = new NativeHashMap<int, float>(32, Allocator.Temp);

            for (var i = 0; i < buildings.Length; i++)
            {
                if (buildings[i].IsComplete != 0)
                    continue;
                var c = constructions[i];
                var dur = math.max(1, c.DurationTicks);
                var woodLeft = math.max(0f, c.WoodTotal - c.WoodDelivered);
                var ironLeft = math.max(0f, c.IronTotal - c.IronDelivered);
                var ticksLeft = math.max(1, dur - c.ProgressTicks);
                var woodNeed = woodLeft / ticksLeft;
                var ironNeed = ironLeft / ticksLeft;
                var pid = buildings[i].ProvinceId;
                if (woodNeed > 1e-6f)
                {
                    woodNeedByProv.TryGetValue(pid, out var w);
                    woodNeedByProv[pid] = w + woodNeed;
                }

                if (ironNeed > 1e-6f)
                {
                    ironNeedByProv.TryGetValue(pid, out var ir);
                    ironNeedByProv[pid] = ir + ironNeed;
                }
            }

            foreach (var kv in woodNeedByProv)
            {
                if (!provinceById.TryGetValue(kv.Key, out var ent) || !em.HasBuffer<PhysicalInputDeficit>(ent))
                    continue;
                AddInputDeficit(em.GetBuffer<PhysicalInputDeficit>(ent), WoodGoodId, kv.Value);
            }

            foreach (var kv in ironNeedByProv)
            {
                if (!provinceById.TryGetValue(kv.Key, out var ent) || !em.HasBuffer<PhysicalInputDeficit>(ent))
                    continue;
                AddInputDeficit(em.GetBuffer<PhysicalInputDeficit>(ent), IronGoodId, kv.Value);
            }

            woodNeedByProv.Dispose();
            ironNeedByProv.Dispose();
            provinceById.Dispose();
        }

        static void AddInputDeficit(DynamicBuffer<PhysicalInputDeficit> buf, int goodId, float amount)
        {
            if (amount <= 1e-6f)
                return;
            for (var i = 0; i < buf.Length; i++)
            {
                if (buf[i].GoodId != goodId)
                    continue;
                var e = buf[i];
                e.Amount += amount;
                buf[i] = e;
                return;
            }

            buf.Add(new PhysicalInputDeficit { GoodId = goodId, Amount = amount });
        }

        /// <summary>
        /// Agrège la capacité des bâtiments achevés par (ProvinceId, GoodId).
        /// Clés de domaine stables — jamais Entity.Index.
        /// </summary>
        public static void AggregateCompletedCapacity(
            EntityManager em,
            NativeHashMap<long, float> capacityByProvinceGood)
        {
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<BuildingData>());
            using var arr = q.ToComponentDataArray<BuildingData>(Allocator.Temp);
            for (var i = 0; i < arr.Length; i++)
            {
                var b = arr[i];
                if (b.IsComplete == 0 || b.OutputGoodId <= 0 || b.CapacityContribution <= 0f)
                    continue;
                var key = ProvinceGoodKey(b.ProvinceId, b.OutputGoodId);
                if (capacityByProvinceGood.TryGetValue(key, out var cur))
                    capacityByProvinceGood[key] = cur + b.CapacityContribution;
                else
                    capacityByProvinceGood.TryAdd(key, b.CapacityContribution);
            }
        }

        public static long ProvinceGoodKey(int provinceId, int goodId) =>
            ((long)provinceId << 32) | (uint)goodId;

        /// <summary>Mappe un bien provincial vers un BuildingType du périmètre v1_038.</summary>
        public static BuildingType TypeForGoodTag(FixedString64Bytes tag)
        {
            var t = tag.ToString();
            if (string.IsNullOrEmpty(t))
                return BuildingType.Workshop;
            switch (t.Trim().ToLowerInvariant())
            {
                case "wood":
                    return BuildingType.Sawmill;
                case "grain":
                case "fish":
                case "livestock":
                case "wine":
                case "wool":
                    return BuildingType.Farm;
                default:
                    return BuildingType.Workshop;
            }
        }

        public static bool IsConstructibleType(BuildingType type) =>
            type == BuildingType.Farm ||
            type == BuildingType.Sawmill ||
            type == BuildingType.Workshop;

        public static bool TryGetCatalogEntry(
            EntityManager em,
            BuildingType type,
            out BuildingCatalogEntry entry)
        {
            entry = default;
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<BuildingEconomySingleton>());
            if (q.IsEmptyIgnoreFilter)
                return false;
            var e = q.GetSingletonEntity();
            if (!em.HasBuffer<BuildingCatalogEntry>(e))
                return false;
            var buf = em.GetBuffer<BuildingCatalogEntry>(e);
            for (var i = 0; i < buf.Length; i++)
            {
                if (buf[i].Type != type)
                    continue;
                entry = buf[i];
                return true;
            }

            return false;
        }

        /// <summary>
        /// Crée un chantier (après paiement trésorerie). BuildingId fourni par l'appelant
        /// (clé stable). Retourne false si type hors catalogue.
        /// </summary>
        public static bool TryCreateConstructionSite(
            EntityManager em,
            int buildingId,
            BuildingType type,
            int cityId,
            int provinceId,
            int countryId,
            int outputGoodId,
            float moneyPaid,
            out Entity entity)
        {
            entity = Entity.Null;
            if (!TryGetCatalogEntry(em, type, out var cat))
                return false;

            entity = em.CreateEntity();
            em.AddComponentData(entity, new BuildingData
            {
                BuildingId = buildingId,
                Type = type,
                CityId = cityId,
                ProvinceId = provinceId,
                CountryId = countryId,
                OutputGoodId = outputGoodId > 0 ? outputGoodId : cat.DefaultOutputGoodId,
                CapacityContribution = cat.Capacity,
                IsComplete = 0
            });
            em.AddComponentData(entity, new BuildingConstruction
            {
                DurationTicks = cat.DurationTicks,
                ProgressTicks = 0,
                WoodTotal = cat.WoodCost,
                IronTotal = cat.IronCost,
                WoodDelivered = 0f,
                IronDelivered = 0f,
                MoneyPaid = moneyPaid,
                BlockedThisTick = 0
            });
            return true;
        }

        public static int NextBuildingId(EntityManager em)
        {
            var maxId = 0;
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<BuildingData>());
            using var arr = q.ToComponentDataArray<BuildingData>(Allocator.Temp);
            for (var i = 0; i < arr.Length; i++)
            {
                if (arr[i].BuildingId > maxId)
                    maxId = arr[i].BuildingId;
            }

            return maxId + 1;
        }

        public static bool CityHasActiveConstruction(EntityManager em, int cityId)
        {
            using var q = em.CreateEntityQuery(
                ComponentType.ReadOnly<BuildingData>(),
                ComponentType.ReadOnly<BuildingConstruction>());
            using var arr = q.ToComponentDataArray<BuildingData>(Allocator.Temp);
            for (var i = 0; i < arr.Length; i++)
            {
                if (arr[i].CityId == cityId && arr[i].IsComplete == 0)
                    return true;
            }

            return false;
        }
    }
}
