using System;
using System.Collections.Generic;
using System.IO;
using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VictoriaGame.Core;
using VictoriaGame.Population;
using VictoriaGame.World;

namespace VictoriaGame.Economy
{
    /// <summary>
    /// Production physique à intrants + débouchés (v1_021 / v1_025 / v1_031).
    ///
    /// outputPhysique = min(
    ///   ce que les intrants locaux permettent (acquis v1_021),
    ///   ce que les débouchés permettent (v1_031) ).
    ///
    /// Débouchés = conso locale (pops + ateliers) + évacuation (capacité d'arête
    /// vers voisins à déficit) + place d'entreposage libre.
    /// Intensité 0 → chemin STRICTEMENT inchangé (bit-identique).
    /// Aucune règle « si stock &gt; seuil alors ×Y » — contrainte de placement.
    /// </summary>
    // Pas de [BurstCompile] sur le système : JSON + static mutable (BC1040),
    // même patron que PhysicalSatisfactionBlendSystem / PopGrowthSystem.
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ProductionSystem))]
    [UpdateBefore(typeof(PhysicalStockSystem))]
    public partial struct PhysicalProductionSystem : ISystem
    {
        /// <summary>
        /// Intensité adoptée v1_032 (balayage t3000 seed=42195) : palier 1.0 —
        /// plus fort qui garde le monde vivant et stabilityLite verte.
        /// Aligné physical_outlet_cap.json. LockOutletCap(x) pour forcer en harnais.
        /// 0 = bit-identique (chemin v1_030) ; 1 = frein plein mesuré.
        /// </summary>
        public const float DefaultOutletCapIntensity = 1f;

        /// <summary>3 mois de demande locale / tick (1 tick ≈ 1 mois).</summary>
        public const float DefaultStorageMonths = 3f;

        /// <summary>
        /// Intensité [0..1]. Mutable pour harnais.
        /// 0 → pas de plafond aval (bit-identique v1_030).
        /// </summary>
        public static float OutletCapIntensity = DefaultOutletCapIntensity;

        /// <summary>Mois de réserve → capacité d'entreposage = mois × demande locale / tick.</summary>
        public static float StorageMonthsOfLocalDemand = DefaultStorageMonths;

        static bool _harnessLocked;
        static bool _jsonApplied;

        /// <summary>Dernier coût CPU de la production physique (ms) — outillage v1_027.</summary>
        public static double LastTickCpuMs;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WorldState>();
            state.RequireForUpdate<PhysicalEconomySingleton>();
            ApplyJsonDefaultIfUnlocked();
        }

        // Pas de [BurstCompile] : chronométrage + lecture statics (BC1040).
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

            var start = System.Diagnostics.Stopwatch.GetTimestamp();
            state.Dependency.Complete();
            ExecuteTick(ref state);
            var end = System.Diagnostics.Stopwatch.GetTimestamp();
            LastTickCpuMs = (end - start) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        /// <summary>Verrouille intensité + mois de stockage pour un harnais.</summary>
        public static void LockOutletCap(float intensity, float storageMonths = DefaultStorageMonths)
        {
            OutletCapIntensity = math.saturate(intensity);
            StorageMonthsOfLocalDemand = math.max(0f, storageMonths);
            _harnessLocked = true;
            _jsonApplied = true;
        }

        public static void UnlockOutletCap()
        {
            _harnessLocked = false;
            _jsonApplied = false;
            ApplyJsonDefaultIfUnlocked();
        }

        public static void ResetToCompiledDefault()
        {
            OutletCapIntensity = DefaultOutletCapIntensity;
            StorageMonthsOfLocalDemand = DefaultStorageMonths;
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
            LoadFromJson(DefaultOutletCapIntensity, DefaultStorageMonths);
        }

        static void LoadFromJson(float intensityFallback, float monthsFallback)
        {
            var path = Path.Combine(
                Application.streamingAssetsPath, "data", "physical_outlet_cap.json");

            if (!File.Exists(path))
            {
                Debug.LogWarning(
                    "PhysicalProductionSystem: physical_outlet_cap.json introuvable — " +
                    $"défauts intensity={intensityFallback} months={monthsFallback}");
                OutletCapIntensity = intensityFallback;
                StorageMonthsOfLocalDemand = monthsFallback;
                return;
            }

            var data = JsonUtility.FromJson<OutletCapFile>(File.ReadAllText(path));
            var intensity = data.outlet_cap_intensity;
            if (intensity < 0f || intensity > 1f || float.IsNaN(intensity) || float.IsInfinity(intensity))
            {
                Debug.LogWarning(
                    $"PhysicalProductionSystem: intensity JSON invalide ({intensity}) — " +
                    $"défaut={intensityFallback}");
                intensity = intensityFallback;
            }

            var months = data.storage_months_of_local_demand;
            if (months < 0f || float.IsNaN(months) || float.IsInfinity(months))
            {
                Debug.LogWarning(
                    $"PhysicalProductionSystem: storage_months JSON invalide ({months}) — " +
                    $"défaut={monthsFallback}");
                months = monthsFallback;
            }

            OutletCapIntensity = intensity;
            StorageMonthsOfLocalDemand = months;
            Debug.Log(
                $"PhysicalProductionSystem: outlet_cap_intensity={intensity} " +
                $"storage_months={months} (depuis JSON)");
        }

        [Serializable]
        class OutletCapFile
        {
            public float outlet_cap_intensity = DefaultOutletCapIntensity;
            public float storage_months_of_local_demand = DefaultStorageMonths;
            public string intensity_justification = "";
            public string storage_justification = "";
        }

        void ExecuteTick(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<PhysicalTransportConfig>();
            var epsilon = config.QuantityEpsilon;
            var em = state.EntityManager;
            var singletonEntity = SystemAPI.GetSingletonEntity<PhysicalEconomySingleton>();
            var ledger = em.GetBuffer<PhysicalLedgerEntry>(singletonEntity);
            var recipes = em.GetBuffer<PhysicalRecipeEntry>(singletonEntity);

            var provinceEntityById = new NativeHashMap<int, Entity>(64, Allocator.Temp);
            var provinceIds = new NativeList<int>(64, Allocator.Temp);
            foreach (var (prov, entity) in SystemAPI
                         .Query<RefRO<ProvinceData>>()
                         .WithAll<ProvinceStock, PhysicalInputDeficit>()
                         .WithEntityAccess())
            {
                var id = prov.ValueRO.ProvinceId;
                if (provinceEntityById.TryAdd(id, entity))
                {
                    provinceIds.Add(id);
                }
            }

            provinceIds.Sort();

            if (PhysicalStockSystem.IdealPoolMode && provinceIds.Length > 0)
            {
                ConsolidateStocksToHub(provinceIds, provinceEntityById, em, epsilon);
            }

            // Sites LOD + bâtiments (v1_038) + activités endowment, triés ProvinceId / GoodId.
            // Intensité capacité bâtiment : 0 = scalaire développement bit-identique ;
            // 1 = capacité uniquement des bâtiments achevés (porte SiteRow existante).
            var buildingCapIntensity = math.saturate(BuildingConstructionSystem.CapacityIntensity);
            var buildingCapacity = new NativeHashMap<long, float>(128, Allocator.Temp);
            BuildingConstructionSystem.AggregateCompletedCapacity(em, buildingCapacity);

            var rows = new NativeList<SiteRow>(128, Allocator.Temp);
            var lodGoodByProvince = new NativeHashMap<int, int>(64, Allocator.Temp);
            var coveredKeys = new NativeHashSet<long>(128, Allocator.Temp);
            foreach (var (site, prov) in SystemAPI.Query<RefRO<ProductionSite>, RefRO<ProvinceData>>())
            {
                lodGoodByProvince.TryAdd(prov.ValueRO.ProvinceId, site.ValueRO.GoodId);
                if (site.ValueRO.GoodId <= 0)
                {
                    continue;
                }

                var lodOut = site.ValueRO.LastOutput;
                var key = BuildingConstructionSystem.ProvinceGoodKey(
                    prov.ValueRO.ProvinceId, site.ValueRO.GoodId);
                coveredKeys.Add(key);
                buildingCapacity.TryGetValue(key, out var bCap);
                var desired = math.lerp(lodOut, bCap, buildingCapIntensity);
                if (desired <= epsilon)
                {
                    continue;
                }

                rows.Add(new SiteRow
                {
                    ProvinceId = prov.ValueRO.ProvinceId,
                    GoodId = site.ValueRO.GoodId,
                    DesiredOutput = desired,
                    FromLodSite = true
                });
            }

            // Bâtiments produisant un bien hors site LOD : contribuent à hauteur de l'intensité.
            if (buildingCapIntensity > 0f)
            {
                using var bKeys = buildingCapacity.GetKeyArray(Allocator.Temp);
                for (var bi = 0; bi < bKeys.Length; bi++)
                {
                    var key = bKeys[bi];
                    if (coveredKeys.Contains(key))
                        continue;
                    var bCap = buildingCapacity[key];
                    var desired = bCap * buildingCapIntensity;
                    if (desired <= epsilon)
                        continue;
                    var provinceId = (int)(key >> 32);
                    var goodId = (int)(key & 0xffffffffL);
                    coveredKeys.Add(key);
                    rows.Add(new SiteRow
                    {
                        ProvinceId = provinceId,
                        GoodId = goodId,
                        DesiredOutput = desired,
                        FromLodSite = false
                    });
                }
            }

            foreach (var (activities, prov) in SystemAPI
                         .Query<DynamicBuffer<ProvincePhysicalActivity>, RefRO<ProvinceData>>())
            {
                var pid = prov.ValueRO.ProvinceId;
                lodGoodByProvince.TryGetValue(pid, out var lodGoodId);
                for (var a = 0; a < activities.Length; a++)
                {
                    var act = activities[a];
                    if (act.GoodId <= 0 || act.BaseCapacity <= epsilon)
                    {
                        continue;
                    }

                    if (act.GoodId == lodGoodId)
                    {
                        continue;
                    }

                    var key = BuildingConstructionSystem.ProvinceGoodKey(pid, act.GoodId);
                    if (coveredKeys.Contains(key))
                    {
                        continue;
                    }

                    coveredKeys.Add(key);
                    rows.Add(new SiteRow
                    {
                        ProvinceId = pid,
                        GoodId = act.GoodId,
                        DesiredOutput = act.BaseCapacity,
                        FromLodSite = false
                    });
                }
            }

            lodGoodByProvince.Dispose();
            coveredKeys.Dispose();
            buildingCapacity.Dispose();
            rows.Sort(new SiteRowComparer());

            var intensity = OutletCapIntensity;
            var applyOutlet = intensity > 0f;

            // Contexte débouchés (uniquement si frein actif — sinon bit-identité stricte).
            var goodIdToType = new NativeHashMap<int, GoodType>(16, Allocator.Temp);
            var foodDemand = new NativeHashMap<int, float>(64, Allocator.Temp);
            var clothDemand = new NativeHashMap<int, float>(64, Allocator.Temp);
            var luxuryDemand = new NativeHashMap<int, float>(64, Allocator.Temp);
            var inputDesire = new NativeHashMap<long, float>(128, Allocator.Temp);
            var foodDeficitPrev = new NativeHashMap<int, float>(64, Allocator.Temp);
            var clothDeficitPrev = new NativeHashMap<int, float>(64, Allocator.Temp);
            var luxuryDeficitPrev = new NativeHashMap<int, float>(64, Allocator.Temp);
            var inputDeficitPrev = new NativeHashMap<long, float>(128, Allocator.Temp);
            var edgeUsed = new NativeHashMap<long, float>(128, Allocator.Temp);
            var remainingFood = new NativeHashMap<int, float>(64, Allocator.Temp);
            var remainingCloth = new NativeHashMap<int, float>(64, Allocator.Temp);
            var remainingLuxury = new NativeHashMap<int, float>(64, Allocator.Temp);
            var remainingInputDesire = new NativeHashMap<long, float>(128, Allocator.Temp);

            if (applyOutlet)
            {
                foreach (var good in SystemAPI.Query<RefRO<GoodData>>())
                {
                    goodIdToType.TryAdd(good.ValueRO.GoodId, good.ValueRO.Type);
                }

                AggregateLocalPopDemand(ref state, provinceEntityById, foodDemand, clothDemand, luxuryDemand);
                CopyMap(foodDemand, remainingFood);
                CopyMap(clothDemand, remainingCloth);
                CopyMap(luxuryDemand, remainingLuxury);

                BuildInputDesire(rows, recipes, inputDesire, epsilon);
                CopyMapLong(inputDesire, remainingInputDesire);

                // Déficits du tick précédent (avant clear) → cibles d'évacuation.
                SnapshotDeficits(
                    provinceIds, provinceEntityById, em, epsilon,
                    foodDeficitPrev, clothDeficitPrev, luxuryDeficitPrev, inputDeficitPrev);
            }

            // Reset déficits d'intrants (reconstruits après conso).
            foreach (var pid in provinceEntityById)
            {
                em.GetBuffer<PhysicalInputDeficit>(pid.Value).Clear();
            }

            var lodTotal = 0f;
            var physicalTotal = 0f;
            var postInputTotal = 0f;
            var outletRemoved = 0f;
            var storageCapTotal = 0.0;
            var saturatedProvinceIds = new NativeHashSet<int>(64, Allocator.Temp);

            var desiredInputs = new NativeList<InputDesire>(64, Allocator.Temp);

            Entity hubEntity = default;
            var useHub = PhysicalStockSystem.IdealPoolMode && provinceIds.Length > 0;
            if (useHub)
            {
                hubEntity = provinceEntityById[provinceIds[0]];
            }

            var storageMonths = StorageMonthsOfLocalDemand;
            var capacity = config.EdgeCapacityPerTick;
            var capacityPerDev = config.CapacityPerDevPoint;

            for (var i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                if (row.FromLodSite)
                {
                    lodTotal += row.DesiredOutput;
                }

                if (!provinceEntityById.TryGetValue(row.ProvinceId, out var entity))
                {
                    continue;
                }

                var stockEntity = useHub ? hubEntity : entity;
                var stock = em.GetBuffer<ProvinceStock>(stockEntity);
                var hasRecipe = HasRecipe(recipes, row.GoodId);
                float inputCapped;

                if (!hasRecipe)
                {
                    inputCapped = row.DesiredOutput;
                }
                else
                {
                    inputCapped = CapByInputs(stock, recipes, row.GoodId, row.DesiredOutput, epsilon);

                    for (var r = 0; r < recipes.Length; r++)
                    {
                        if (recipes[r].OutputGoodId != row.GoodId)
                        {
                            continue;
                        }

                        desiredInputs.Add(new InputDesire
                        {
                            ProvinceId = useHub ? provinceIds[0] : row.ProvinceId,
                            GoodId = recipes[r].InputGoodId,
                            Amount = row.DesiredOutput * recipes[r].QtyPerUnit
                        });
                    }
                }

                postInputTotal += inputCapped;
                var physical = inputCapped;

                if (applyOutlet && inputCapped > epsilon)
                {
                    goodIdToType.TryGetValue(row.GoodId, out var gType);
                    var localPop = LocalPopRoom(
                        row.ProvinceId, gType, remainingFood, remainingCloth, remainingLuxury);
                    var desireKey = DesireKey(row.ProvinceId, row.GoodId);
                    var localInput = remainingInputDesire.TryGetValue(desireKey, out var di) ? di : 0f;

                    var localDemandForStorage =
                        TypeDemand(row.ProvinceId, gType, foodDemand, clothDemand, luxuryDemand) +
                        (inputDesire.TryGetValue(desireKey, out var id0) ? id0 : 0f);
                    var storageCap = storageMonths * localDemandForStorage;
                    storageCapTotal += storageCap;

                    var onHand = PhysicalStockSystem.GetStockQuantity(stock, row.GoodId);
                    var freeStore = (float)math.max(0.0, storageCap - onHand);

                    var evacuate = ComputeEvacuateRoom(
                        row.ProvinceId,
                        row.GoodId,
                        gType,
                        epsilon,
                        entity,
                        em,
                        provinceEntityById,
                        foodDeficitPrev,
                        clothDeficitPrev,
                        luxuryDeficitPrev,
                        inputDeficitPrev,
                        edgeUsed,
                        capacity,
                        capacityPerDev);

                    var room = localPop + localInput + evacuate + freeStore;

                    // Mode « monde idéal » : par définition, AUCUNE contrainte
                    // logistique — transport instantané, stock mis en commun. Le
                    // plafond aval n'a donc pas lieu de s'y appliquer.
                    //
                    // Sans cette exception, le regroupement des stocks sur une
                    // province-pivot faisait dimensionner l'entreposage sur la
                    // demande locale d'UNE seule province au lieu de la demande
                    // mondiale : le frein étranglait l'expérience de contrôle et
                    // V1023_IdealPool_ClosesGeographyGap tombait de 0.75+ à 0.703.
                    // Cette expérience est notre référence pour distinguer un
                    // écart LOGISTIQUE d'un écart de DÉFINITION : la fausser
                    // fausserait tous les diagnostics qui s'appuient dessus.
                    if (useHub)
                    {
                        room = float.MaxValue;
                    }

                    var target = math.min(inputCapped, room);
                    physical = math.lerp(inputCapped, target, math.saturate(intensity));
                    outletRemoved += inputCapped - physical;

                    // Consommer les débouchés « locaux » réservés ; l'évacuation
                    // décrémente edgeUsed + déficits voisins dans ComputeEvacuateRoom.
                    ConsumeLocalOutletRoom(
                        row.ProvinceId, gType, physical,
                        remainingFood, remainingCloth, remainingLuxury,
                        desireKey, remainingInputDesire);

                    if (storageCap > epsilon && onHand + physical >= storageCap - epsilon)
                    {
                        saturatedProvinceIds.Add(row.ProvinceId);
                    }
                }

                if (hasRecipe && physical > epsilon)
                {
                    ConsumeInputs(stock, ledger, recipes, row.GoodId, physical, epsilon);
                }

                if (physical > epsilon)
                {
                    PhysicalStockSystem.AddToStock(stock, row.GoodId, physical);
                    AddLedgerProduction(ledger, row.GoodId, physical);
                }

                physicalTotal += physical;
            }

            // Agréger désir d'intrants, puis déficit = désir − stock restant (après conso).
            desiredInputs.Sort(new InputDesireComparer());
            var agg = new NativeList<InputDesire>(desiredInputs.Length, Allocator.Temp);
            for (var i = 0; i < desiredInputs.Length; i++)
            {
                var d = desiredInputs[i];
                if (agg.Length > 0 &&
                    agg[agg.Length - 1].ProvinceId == d.ProvinceId &&
                    agg[agg.Length - 1].GoodId == d.GoodId)
                {
                    var last = agg[agg.Length - 1];
                    last.Amount += d.Amount;
                    agg[agg.Length - 1] = last;
                }
                else
                {
                    agg.Add(d);
                }
            }

            for (var i = 0; i < agg.Length; i++)
            {
                var d = agg[i];
                if (!provinceEntityById.TryGetValue(d.ProvinceId, out var entity))
                {
                    continue;
                }

                var stock = em.GetBuffer<ProvinceStock>(entity);
                var available = PhysicalStockSystem.GetStockQuantity(stock, d.GoodId);
                var deficit = (float)math.max(0.0, d.Amount - available);
                if (deficit <= epsilon)
                {
                    continue;
                }

                var buf = em.GetBuffer<PhysicalInputDeficit>(entity);
                SetOrAddDeficit(buf, d.GoodId, deficit);
            }

            var missed = lodTotal > epsilon
                ? math.saturate((lodTotal - physicalTotal) / lodTotal)
                : 0f;
            var missedOutlet = postInputTotal > epsilon
                ? math.saturate(outletRemoved / postInputTotal)
                : 0f;

            var metrics = SystemAPI.GetSingletonRW<PhysicalEconomyMetrics>();
            metrics.ValueRW.LodOutputTotal = lodTotal;
            metrics.ValueRW.PhysicalOutputTotal = physicalTotal;
            metrics.ValueRW.MissedInputShare = missed;
            metrics.ValueRW.MissedOutletShare = missedOutlet;
            metrics.ValueRW.StorageCapacityTotal = (float)storageCapTotal;
            metrics.ValueRW.StorageSaturatedProvinceCount = saturatedProvinceIds.Count;

            agg.Dispose();
            desiredInputs.Dispose();
            rows.Dispose();
            provinceIds.Dispose();
            provinceEntityById.Dispose();
            goodIdToType.Dispose();
            foodDemand.Dispose();
            clothDemand.Dispose();
            luxuryDemand.Dispose();
            inputDesire.Dispose();
            foodDeficitPrev.Dispose();
            clothDeficitPrev.Dispose();
            luxuryDeficitPrev.Dispose();
            inputDeficitPrev.Dispose();
            edgeUsed.Dispose();
            remainingFood.Dispose();
            remainingCloth.Dispose();
            remainingLuxury.Dispose();
            remainingInputDesire.Dispose();
            saturatedProvinceIds.Dispose();
        }

        void AggregateLocalPopDemand(
            ref SystemState state,
            NativeHashMap<int, Entity> provinceEntityById,
            NativeHashMap<int, float> foodDemand,
            NativeHashMap<int, float> clothDemand,
            NativeHashMap<int, float> luxuryDemand)
        {
            var provinceLookup = SystemAPI.GetComponentLookup<ProvinceData>(true);
            provinceLookup.Update(ref state);

            foreach (var (needs, pop) in SystemAPI.Query<RefRO<PopNeeds>, RefRO<PopData>>())
            {
                if (!provinceLookup.HasComponent(pop.ValueRO.Province))
                {
                    continue;
                }

                var provinceId = provinceLookup[pop.ValueRO.Province].ProvinceId;
                if (!provinceEntityById.ContainsKey(provinceId))
                {
                    continue;
                }

                var scale = pop.ValueRO.Size;
                AddMap(foodDemand, provinceId, needs.ValueRO.FoodNeed * scale);
                AddMap(clothDemand, provinceId, needs.ValueRO.ClothNeed * scale);
                AddMap(luxuryDemand, provinceId, needs.ValueRO.LuxuryNeed * scale);
            }
        }

        static void BuildInputDesire(
            NativeList<SiteRow> rows,
            DynamicBuffer<PhysicalRecipeEntry> recipes,
            NativeHashMap<long, float> inputDesire,
            float epsilon)
        {
            for (var i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                if (row.DesiredOutput <= epsilon)
                {
                    continue;
                }

                for (var r = 0; r < recipes.Length; r++)
                {
                    if (recipes[r].OutputGoodId != row.GoodId)
                    {
                        continue;
                    }

                    var amount = row.DesiredOutput * recipes[r].QtyPerUnit;
                    if (amount <= epsilon)
                    {
                        continue;
                    }

                    var key = DesireKey(row.ProvinceId, recipes[r].InputGoodId);
                    inputDesire[key] = (inputDesire.TryGetValue(key, out var cur) ? cur : 0f) + amount;
                }
            }
        }

        static void SnapshotDeficits(
            NativeList<int> provinceIds,
            NativeHashMap<int, Entity> provinceEntityById,
            EntityManager em,
            float epsilon,
            NativeHashMap<int, float> foodDeficit,
            NativeHashMap<int, float> clothDeficit,
            NativeHashMap<int, float> luxuryDeficit,
            NativeHashMap<long, float> inputDeficit)
        {
            for (var p = 0; p < provinceIds.Length; p++)
            {
                var id = provinceIds[p];
                var entity = provinceEntityById[id];
                if (em.HasComponent<PhysicalDemandSnapshot>(entity))
                {
                    var snap = em.GetComponentData<PhysicalDemandSnapshot>(entity);
                    var fd = math.max(0f, snap.FoodDemand - snap.FoodSatisfied);
                    var cd = math.max(0f, snap.ClothDemand - snap.ClothSatisfied);
                    var ld = math.max(0f, snap.LuxuryDemand - snap.LuxurySatisfied);
                    if (fd > epsilon)
                    {
                        foodDeficit[id] = fd;
                    }

                    if (cd > epsilon)
                    {
                        clothDeficit[id] = cd;
                    }

                    if (ld > epsilon)
                    {
                        luxuryDeficit[id] = ld;
                    }
                }

                var buf = em.GetBuffer<PhysicalInputDeficit>(entity);
                for (var i = 0; i < buf.Length; i++)
                {
                    if (buf[i].Amount > epsilon)
                    {
                        inputDeficit[DesireKey(id, buf[i].GoodId)] = buf[i].Amount;
                    }
                }
            }
        }

        static float ComputeEvacuateRoom(
            int fromId,
            int goodId,
            GoodType type,
            float epsilon,
            Entity fromEntity,
            EntityManager em,
            NativeHashMap<int, Entity> provinceEntityById,
            NativeHashMap<int, float> foodDeficit,
            NativeHashMap<int, float> clothDeficit,
            NativeHashMap<int, float> luxuryDeficit,
            NativeHashMap<long, float> inputDeficit,
            NativeHashMap<long, float> edgeUsed,
            float baseCapacity,
            float capacityPerDev)
        {
            if (!em.HasBuffer<ProvinceNeighbor>(fromEntity))
            {
                return 0f;
            }

            var neighbors = em.GetBuffer<ProvinceNeighbor>(fromEntity);
            var room = 0f;

            // Voisins terrestres triés (déterminisme).
            var land = new NativeList<int>(neighbors.Length, Allocator.Temp);
            for (var n = 0; n < neighbors.Length; n++)
            {
                if (!neighbors[n].IsStrait)
                {
                    land.Add(neighbors[n].NeighborProvinceId);
                }
            }

            land.Sort();

            for (var n = 0; n < land.Length; n++)
            {
                var nid = land[n];
                if (!provinceEntityById.ContainsKey(nid))
                {
                    continue;
                }

                var pull = NeighborPullApprox(
                    foodDeficit, clothDeficit, luxuryDeficit, inputDeficit,
                    nid, goodId, type, epsilon);
                if (pull <= epsilon)
                {
                    continue;
                }

                var edgeKey = PhysicalStockSystem.EdgeKey(fromId, nid);
                var used = edgeUsed.TryGetValue(edgeKey, out var u) ? u : 0f;
                var edgeCap = PhysicalStockSystem.ResolveEdgeCapacity(
                    em, provinceEntityById, fromId, nid, baseCapacity, capacityPerDev);
                var edgeRoom = edgeCap - used;
                if (edgeRoom <= epsilon)
                {
                    continue;
                }

                var ship = math.min(pull, edgeRoom);
                if (ship <= epsilon)
                {
                    continue;
                }

                room += ship;
                edgeUsed[edgeKey] = used + ship;

                // Réduit le pull voisin pour ne pas compter deux fois la même demande.
                ReduceNeighborPull(
                    foodDeficit, clothDeficit, luxuryDeficit, inputDeficit,
                    nid, goodId, type, ship);
            }

            land.Dispose();
            return room;
        }

        static float NeighborPullApprox(
            NativeHashMap<int, float> food,
            NativeHashMap<int, float> cloth,
            NativeHashMap<int, float> luxury,
            NativeHashMap<long, float> inputDeficit,
            int provinceId,
            int goodId,
            GoodType type,
            float epsilon)
        {
            var pull = 0f;
            if (type == GoodType.Food || type == GoodType.Manufactured || type == GoodType.Luxury)
            {
                pull += TypeDemand(provinceId, type, food, cloth, luxury);
            }

            var key = DesireKey(provinceId, goodId);
            if (inputDeficit.TryGetValue(key, out var inp) && inp > epsilon)
            {
                pull += inp;
            }

            return pull;
        }

        static void ReduceNeighborPull(
            NativeHashMap<int, float> food,
            NativeHashMap<int, float> cloth,
            NativeHashMap<int, float> luxury,
            NativeHashMap<long, float> inputDeficit,
            int provinceId,
            int goodId,
            GoodType type,
            float amount)
        {
            var remaining = amount;
            var key = DesireKey(provinceId, goodId);
            if (inputDeficit.TryGetValue(key, out var inp) && inp > 0f)
            {
                var take = math.min(inp, remaining);
                var left = inp - take;
                if (left > 0f)
                {
                    inputDeficit[key] = left;
                }
                else
                {
                    inputDeficit.Remove(key);
                }

                remaining -= take;
            }

            if (remaining <= 0f)
            {
                return;
            }

            if (type == GoodType.Food)
            {
                ReduceMap(food, provinceId, remaining);
            }
            else if (type == GoodType.Manufactured)
            {
                ReduceMap(cloth, provinceId, remaining);
            }
            else if (type == GoodType.Luxury)
            {
                ReduceMap(luxury, provinceId, remaining);
            }
        }

        static void ConsumeLocalOutletRoom(
            int provinceId,
            GoodType type,
            float amount,
            NativeHashMap<int, float> remainingFood,
            NativeHashMap<int, float> remainingCloth,
            NativeHashMap<int, float> remainingLuxury,
            long desireKey,
            NativeHashMap<long, float> remainingInputDesire)
        {
            var remaining = amount;
            if (remainingInputDesire.TryGetValue(desireKey, out var di) && di > 0f)
            {
                var take = math.min(di, remaining);
                var left = di - take;
                if (left > 0f)
                {
                    remainingInputDesire[desireKey] = left;
                }
                else
                {
                    remainingInputDesire.Remove(desireKey);
                }

                remaining -= take;
            }

            if (remaining <= 0f)
            {
                return;
            }

            if (type == GoodType.Food)
            {
                ReduceMap(remainingFood, provinceId, remaining);
            }
            else if (type == GoodType.Manufactured)
            {
                ReduceMap(remainingCloth, provinceId, remaining);
            }
            else if (type == GoodType.Luxury)
            {
                ReduceMap(remainingLuxury, provinceId, remaining);
            }
        }

        static float LocalPopRoom(
            int provinceId,
            GoodType type,
            NativeHashMap<int, float> food,
            NativeHashMap<int, float> cloth,
            NativeHashMap<int, float> luxury)
        {
            return TypeDemand(provinceId, type, food, cloth, luxury);
        }

        static float TypeDemand(
            int provinceId,
            GoodType type,
            NativeHashMap<int, float> food,
            NativeHashMap<int, float> cloth,
            NativeHashMap<int, float> luxury)
        {
            if (type == GoodType.Food)
            {
                return food.TryGetValue(provinceId, out var v) ? v : 0f;
            }

            if (type == GoodType.Manufactured)
            {
                return cloth.TryGetValue(provinceId, out var v) ? v : 0f;
            }

            if (type == GoodType.Luxury)
            {
                return luxury.TryGetValue(provinceId, out var v) ? v : 0f;
            }

            return 0f;
        }

        static long DesireKey(int provinceId, int goodId) =>
            ((long)provinceId << 32) | (uint)goodId;

        static void AddMap(NativeHashMap<int, float> map, int key, float amount)
        {
            map[key] = (map.TryGetValue(key, out var cur) ? cur : 0f) + amount;
        }

        static void ReduceMap(NativeHashMap<int, float> map, int key, float amount)
        {
            if (!map.TryGetValue(key, out var cur))
            {
                return;
            }

            var left = cur - amount;
            if (left > 0f)
            {
                map[key] = left;
            }
            else
            {
                map.Remove(key);
            }
        }

        static void CopyMap(NativeHashMap<int, float> src, NativeHashMap<int, float> dst)
        {
            foreach (var kv in src)
            {
                dst[kv.Key] = kv.Value;
            }
        }

        static void CopyMapLong(NativeHashMap<long, float> src, NativeHashMap<long, float> dst)
        {
            foreach (var kv in src)
            {
                dst[kv.Key] = kv.Value;
            }
        }

        static void ConsolidateStocksToHub(
            NativeList<int> provinceIds,
            NativeHashMap<int, Entity> provinceEntityById,
            EntityManager em,
            float epsilon)
        {
            var hubId = provinceIds[0];
            var hubStock = em.GetBuffer<ProvinceStock>(provinceEntityById[hubId]);
            for (var p = 1; p < provinceIds.Length; p++)
            {
                var stock = em.GetBuffer<ProvinceStock>(provinceEntityById[provinceIds[p]]);
                for (var i = 0; i < stock.Length; i++)
                {
                    if (stock[i].Quantity > epsilon)
                    {
                        PhysicalStockSystem.AddToStock(hubStock, stock[i].GoodId, stock[i].Quantity);
                    }
                }

                stock.Clear();
            }
        }

        static bool HasRecipe(DynamicBuffer<PhysicalRecipeEntry> recipes, int outputGoodId)
        {
            for (var i = 0; i < recipes.Length; i++)
            {
                if (recipes[i].OutputGoodId == outputGoodId)
                {
                    return true;
                }
            }

            return false;
        }

        static float CapByInputs(
            DynamicBuffer<ProvinceStock> stock,
            DynamicBuffer<PhysicalRecipeEntry> recipes,
            int outputGoodId,
            float lodOutput,
            float epsilon)
        {
            var capped = (double)lodOutput;
            var any = false;
            for (var i = 0; i < recipes.Length; i++)
            {
                if (recipes[i].OutputGoodId != outputGoodId)
                {
                    continue;
                }

                any = true;
                var qtyPer = recipes[i].QtyPerUnit;
                if (qtyPer <= epsilon)
                {
                    continue;
                }

                var available = PhysicalStockSystem.GetStockQuantity(stock, recipes[i].InputGoodId);
                capped = math.min(capped, available / qtyPer);
            }

            return any ? (float)math.max(0.0, capped) : lodOutput;
        }

        static void ConsumeInputs(
            DynamicBuffer<ProvinceStock> stock,
            DynamicBuffer<PhysicalLedgerEntry> ledger,
            DynamicBuffer<PhysicalRecipeEntry> recipes,
            int outputGoodId,
            float physicalOutput,
            float epsilon)
        {
            for (var i = 0; i < recipes.Length; i++)
            {
                if (recipes[i].OutputGoodId != outputGoodId)
                {
                    continue;
                }

                var need = physicalOutput * recipes[i].QtyPerUnit;
                if (need <= epsilon)
                {
                    continue;
                }

                var idx = PhysicalStockSystem.FindStockIndex(stock, recipes[i].InputGoodId);
                if (idx < 0)
                {
                    continue;
                }

                var entry = stock[idx];
                var take = math.min(entry.Quantity, (double)need);
                if (take <= epsilon)
                {
                    continue;
                }

                entry.Quantity -= take;
                stock[idx] = entry;
                AddLedgerConsumption(ledger, recipes[i].InputGoodId, take);
            }
        }

        static void SetOrAddDeficit(DynamicBuffer<PhysicalInputDeficit> buf, int goodId, float amount)
        {
            for (var i = 0; i < buf.Length; i++)
            {
                if (buf[i].GoodId == goodId)
                {
                    var e = buf[i];
                    e.Amount += amount;
                    buf[i] = e;
                    return;
                }
            }

            buf.Add(new PhysicalInputDeficit { GoodId = goodId, Amount = amount });
        }

        static void AddLedgerProduction(DynamicBuffer<PhysicalLedgerEntry> ledger, int goodId, double qty)
        {
            var idx = FindLedgerIndex(ledger, goodId);
            if (idx >= 0)
            {
                var e = ledger[idx];
                e.CumulativeProduction += qty;
                ledger[idx] = e;
            }
            else
            {
                ledger.Add(new PhysicalLedgerEntry
                {
                    GoodId = goodId,
                    CumulativeProduction = qty,
                    CumulativeConsumption = 0.0
                });
            }
        }

        static void AddLedgerConsumption(DynamicBuffer<PhysicalLedgerEntry> ledger, int goodId, double qty)
        {
            var idx = FindLedgerIndex(ledger, goodId);
            if (idx >= 0)
            {
                var e = ledger[idx];
                e.CumulativeConsumption += qty;
                ledger[idx] = e;
            }
            else
            {
                ledger.Add(new PhysicalLedgerEntry
                {
                    GoodId = goodId,
                    CumulativeProduction = 0.0,
                    CumulativeConsumption = qty
                });
            }
        }

        static int FindLedgerIndex(DynamicBuffer<PhysicalLedgerEntry> ledger, int goodId)
        {
            for (var i = 0; i < ledger.Length; i++)
            {
                if (ledger[i].GoodId == goodId)
                {
                    return i;
                }
            }

            return -1;
        }

        struct SiteRow
        {
            public int ProvinceId;
            public int GoodId;
            public float DesiredOutput;
            public bool FromLodSite;
        }

        struct SiteRowComparer : IComparer<SiteRow>
        {
            public int Compare(SiteRow a, SiteRow b)
            {
                var c = a.ProvinceId.CompareTo(b.ProvinceId);
                return c != 0 ? c : a.GoodId.CompareTo(b.GoodId);
            }
        }

        struct InputDesire
        {
            public int ProvinceId;
            public int GoodId;
            public float Amount;
        }

        struct InputDesireComparer : IComparer<InputDesire>
        {
            public int Compare(InputDesire a, InputDesire b)
            {
                var c = a.ProvinceId.CompareTo(b.ProvinceId);
                return c != 0 ? c : a.GoodId.CompareTo(b.GoodId);
            }
        }
    }
}
