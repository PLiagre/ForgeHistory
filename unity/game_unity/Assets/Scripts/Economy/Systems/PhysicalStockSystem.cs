using System.Collections.Generic;
using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using VictoriaGame.Core;
using VictoriaGame.Population;
using VictoriaGame.World;

namespace VictoriaGame.Economy
{
    /// <summary>
    /// Couche fantôme d'économie physique (v1_020/v1_021) : stocks localisés, consommation
    /// locale, transport terrestre à capacité/délai, cargaisons en transit.
    ///
    /// v1_021 : le dépôt de production est fait par PhysicalProductionSystem (intrants).
    /// Le transport dessert aussi la demande d'intrants (PhysicalInputDeficit).
    /// Conservation à puissance constante (dérive par tick bornée au flux du tick).
    ///
    /// STRICTEMENT ADDITIVE — ne lit que ProductionSite / PopNeeds / PopData en lecture
    /// seule (via PhysicalProductionSystem pour la prod), n'écrit QUE dans ProvinceStock,
    /// CargoInTransit, ledger, PhysicalDemandSnapshot, PhysicalInputDeficit.
    ///
    /// Déterminisme : tri sur ProvinceId puis GoodId, jamais Entity.Index.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ProductionSystem))]
    [UpdateAfter(typeof(MarketAggregationSystem))]
    [UpdateAfter(typeof(PhysicalProductionSystem))]
    public partial struct PhysicalStockSystem : ISystem
    {
        /// <summary>
        /// Epsilon absolu de conservation par tick (et plancher d'identité ledger).
        /// </summary>
        public const float ConservationEpsilonAbs = 1e-2f;

        /// <summary>
        /// Plafond ABSOLU constant pour l'identité cumuls↔stock (erreur float d'agrégation).
        /// Informative seulement : les stocks sont en float, la précision ~1 aux totaux 1e7+.
        /// Le critère DURCI est <see cref="CheckConservationPerTick"/> / MaxTickConservationDrift.
        /// </summary>
        public const float ConservationEpsilonAbsIdentity = 50f;

        /// <summary>
        /// Epsilon relatif au FLUX DU TICK (prod + conso + |Δtransit|), pas aux cumuls.
        /// </summary>
        public const float ConservationEpsilonRelTick = 1e-3f;

        /// <summary>Dernier coût CPU de la couche (ms) — hors Burst, pour le rapport.</summary>
        public static double LastTickCpuMs;

        /// <summary>
        /// Mode diagnostic « monde idéal » (v1_023) : stock mondial unique, transport
        /// instantané (pas de cargaisons). Sert à isoler définitions vs géographie.
        /// Static mutable — pas de [BurstCompile] sur OnUpdate (BC1040), comme LockWeight.
        /// </summary>
        public static bool IdealPoolMode;

        /// <summary>
        /// Transport multi-sauts (v1_023/v1_024) : BFS vers le déficit le plus proche.
        /// v1_024 cellule×capacité : à cap desserrée, clothServedShare 0.038→0.142
        /// (intrants aux ateliers) pour +0.025 physMean — GARDÉ (mission Partie 2).
        /// CPU ≈×2 ; désactivable via ce drapeau.
        /// </summary>
        public static bool MultiHopTransport = true;

        /// <summary>
        /// Ordre de service du transport sur une arête (v1_030).
        /// ByGoodId = règle magique historique (GoodId croissant consomme la capacité).
        /// ByDeficitSeverity = priorité émergente du pull/déficit destinataire ; GoodId
        /// ne départage qu'à égalité. Défaut = sévérité (correction v1_030).
        /// </summary>
        public enum TransportServiceOrder : byte
        {
            ByGoodId = 0,
            ByDeficitSeverity = 1
        }

        public static TransportServiceOrder ServiceOrderMode = TransportServiceOrder.ByDeficitSeverity;

        /// <summary>Plafond GoodId pour les compteurs de diagnostic transport.</summary>
        public const int TransportShareSlots = 32;

        /// <summary>Active l'agrégation LastTickShippedByGood / CapRoom* (hors Burst).</summary>
        public static bool RecordTransportShares;

        /// <summary>
        /// GoodId drap (goods.json id=8). Compteurs de bilan v1_084 — lecture seule,
        /// n'altère pas la simulation.
        /// </summary>
        public const int ClothGoodId = 8;

        /// <summary>
        /// Active LastTickClothDelivered / LastTickClothConsumedId (hors Burst).
        /// Couplé à RecordTransportShares pour le chargé.
        /// </summary>
        public static bool RecordClothBalance;

        /// <summary>Quantité embarquée ce tick par GoodId (si RecordTransportShares).</summary>
        public static readonly double[] LastTickShippedByGood = new double[TransportShareSlots];

        /// <summary>Somme des room d'arête au moment où le bien est servi.</summary>
        public static readonly double[] LastTickCapRoomSumByGood = new double[TransportShareSlots];

        /// <summary>Nombre de tentatives de service par GoodId.</summary>
        public static readonly int[] LastTickCapRoomCountByGood = new int[TransportShareSlots];

        /// <summary>Tentatives où room ≤ epsilon (capacité déjà mangée).</summary>
        public static readonly int[] LastTickCapExhaustedByGood = new int[TransportShareSlots];

        /// <summary>Drap livré des cargaisons ce tick (si RecordClothBalance).</summary>
        public static double LastTickClothDelivered;

        /// <summary>Drap (GoodId=8) embarqué ce tick (si RecordClothBalance).</summary>
        public static double LastTickClothShipped;

        /// <summary>Drap (GoodId=8) retiré du stock pour demande Manufactured ce tick.</summary>
        public static double LastTickClothConsumedId;

        public static void ClearTransportShareCounters()
        {
            for (var i = 0; i < TransportShareSlots; i++)
            {
                LastTickShippedByGood[i] = 0;
                LastTickCapRoomSumByGood[i] = 0;
                LastTickCapRoomCountByGood[i] = 0;
                LastTickCapExhaustedByGood[i] = 0;
            }
        }

        public static void ClearClothBalanceCounters()
        {
            LastTickClothDelivered = 0;
            LastTickClothShipped = 0;
            LastTickClothConsumedId = 0;
        }

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WorldState>();
            state.RequireForUpdate<PhysicalEconomySingleton>();
        }

        // Pas de [BurstCompile] : chronométrage + lecture static LastTickCpuMs (BC1040).
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
            if (RecordClothBalance)
            {
                ClearClothBalanceCounters();
            }

            // Chantiers → PhysicalInputDeficit (après rebuild prod) pour tirer bois/fer.
            BuildingConstructionSystem.RegisterConstructionMaterialDemand(state.EntityManager);
            ExecuteTick(ref state);
            var end = System.Diagnostics.Stopwatch.GetTimestamp();
            LastTickCpuMs = (end - start) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;

            var metrics = SystemAPI.GetSingletonRW<PhysicalEconomyMetrics>();
            metrics.ValueRW.LastTickCpuMs = (float)LastTickCpuMs;
            var tickDrift = s_LastTickMaxDrift;
            if (tickDrift > metrics.ValueRO.MaxTickConservationDrift)
            {
                metrics.ValueRW.MaxTickConservationDrift = tickDrift;
            }
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        void ExecuteTick(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<PhysicalTransportConfig>();
            var epsilon = config.QuantityEpsilon;
            var capacity = config.EdgeCapacityPerTick;
            var capacityPerDev = config.CapacityPerDevPoint;
            var transitTicks = config.TransitTicksPerEdge;
            if (transitTicks < 1)
            {
                transitTicks = 1;
            }

            var goodIdToType = new NativeHashMap<int, GoodType>(16, Allocator.Temp);
            foreach (var good in SystemAPI.Query<RefRO<GoodData>>())
            {
                goodIdToType.TryAdd(good.ValueRO.GoodId, good.ValueRO.Type);
            }

            var provinceEntityById = new NativeHashMap<int, Entity>(64, Allocator.Temp);
            var provinceIds = new NativeList<int>(64, Allocator.Temp);
            foreach (var (prov, entity) in SystemAPI
                         .Query<RefRO<ProvinceData>>()
                         .WithAll<ProvinceStock, PhysicalDemandSnapshot>()
                         .WithEntityAccess())
            {
                var id = prov.ValueRO.ProvinceId;
                if (provinceEntityById.TryAdd(id, entity))
                {
                    provinceIds.Add(id);
                }
            }

            provinceIds.Sort();

            var em = state.EntityManager;
            var singletonEntity = SystemAPI.GetSingletonEntity<PhysicalEconomySingleton>();
            var cargos = em.GetBuffer<CargoInTransit>(singletonEntity);
            var ledger = em.GetBuffer<PhysicalLedgerEntry>(singletonEntity);

            // --- 1. Livrer les cargaisons arrivées, avancer les autres ---
            DeliverAndAdvanceCargos(cargos, provinceEntityById, em, epsilon);

            // --- 2. Production : faite par PhysicalProductionSystem (avant ce système) ---

            // --- 3. Demande locale + consommation physique (fantôme) ---
            var foodDemand = new NativeHashMap<int, float>(64, Allocator.Temp);
            var clothDemand = new NativeHashMap<int, float>(64, Allocator.Temp);
            var luxuryDemand = new NativeHashMap<int, float>(64, Allocator.Temp);
            AggregateLocalDemand(ref state, provinceEntityById, foodDemand, clothDemand, luxuryDemand);

            var foodDeficit = new NativeHashMap<int, float>(64, Allocator.Temp);
            var clothDeficit = new NativeHashMap<int, float>(64, Allocator.Temp);
            var luxuryDeficit = new NativeHashMap<int, float>(64, Allocator.Temp);

            if (IdealPoolMode)
            {
                ConsumeIdealPool(
                    provinceIds,
                    provinceEntityById,
                    em,
                    goodIdToType,
                    ledger,
                    foodDemand,
                    clothDemand,
                    luxuryDemand,
                    foodDeficit,
                    clothDeficit,
                    luxuryDeficit,
                    epsilon);
                // Monde idéal : pas de transit — vider les cargaisons résiduelles dans le pool.
                DrainCargosIntoStock(cargos, provinceEntityById, em, epsilon);
            }
            else
            {
                ConsumeLocal(
                    provinceIds,
                    provinceEntityById,
                    em,
                    goodIdToType,
                    ledger,
                    foodDemand,
                    clothDemand,
                    luxuryDemand,
                    foodDeficit,
                    clothDeficit,
                    luxuryDeficit,
                    epsilon);

                // --- 4. Transport gradient : pop déficit + déficit d'intrants ---
                var edgeUsed = new NativeHashMap<long, float>(128, Allocator.Temp);
                DispatchTransport(
                    provinceIds,
                    provinceEntityById,
                    em,
                    cargos,
                    goodIdToType,
                    foodDeficit,
                    clothDeficit,
                    luxuryDeficit,
                    edgeUsed,
                    capacity,
                    capacityPerDev,
                    transitTicks,
                    epsilon);
                edgeUsed.Dispose();
            }

            // --- 5. Métriques + conservation par tick ---
            UpdateMetrics(
                ref state,
                provinceIds,
                provinceEntityById,
                em,
                cargos,
                foodDeficit,
                clothDeficit,
                luxuryDeficit,
                epsilon);

            UpdateTickConservation(provinceIds, provinceEntityById, em, cargos, ledger, epsilon);

            luxuryDeficit.Dispose();
            clothDeficit.Dispose();
            foodDeficit.Dispose();
            luxuryDemand.Dispose();
            clothDemand.Dispose();
            foodDemand.Dispose();
            provinceIds.Dispose();
            provinceEntityById.Dispose();
            goodIdToType.Dispose();
        }

        static void DeliverAndAdvanceCargos(
            DynamicBuffer<CargoInTransit> cargos,
            NativeHashMap<int, Entity> provinceEntityById,
            EntityManager em,
            float epsilon)
        {
            var kept = new NativeList<CargoInTransit>(cargos.Length, Allocator.TempJob);

            for (var i = 0; i < cargos.Length; i++)
            {
                var c = cargos[i];
                c.TicksRemaining -= 1;
                if (c.TicksRemaining <= 0)
                {
                    if (c.Quantity > epsilon &&
                        provinceEntityById.TryGetValue(c.DestProvinceId, out var destEntity))
                    {
                        var stock = em.GetBuffer<ProvinceStock>(destEntity);
                        AddToStock(stock, c.GoodId, c.Quantity);
                        if (RecordClothBalance && c.GoodId == ClothGoodId)
                        {
                            LastTickClothDelivered += c.Quantity;
                        }
                    }
                }
                else if (c.Quantity > epsilon)
                {
                    kept.Add(c);
                }
            }

            kept.Sort(new CargoComparer());
            cargos.Clear();
            for (var i = 0; i < kept.Length; i++)
            {
                cargos.Add(kept[i]);
            }

            kept.Dispose();
        }

        void AggregateLocalDemand(
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

        static void ConsumeLocal(
            NativeList<int> provinceIds,
            NativeHashMap<int, Entity> provinceEntityById,
            EntityManager em,
            NativeHashMap<int, GoodType> goodIdToType,
            DynamicBuffer<PhysicalLedgerEntry> ledger,
            NativeHashMap<int, float> foodDemand,
            NativeHashMap<int, float> clothDemand,
            NativeHashMap<int, float> luxuryDemand,
            NativeHashMap<int, float> foodDeficit,
            NativeHashMap<int, float> clothDeficit,
            NativeHashMap<int, float> luxuryDeficit,
            float epsilon)
        {
            for (var p = 0; p < provinceIds.Length; p++)
            {
                var provinceId = provinceIds[p];
                var entity = provinceEntityById[provinceId];
                var stock = em.GetBuffer<ProvinceStock>(entity);

                var dFood = foodDemand.TryGetValue(provinceId, out var df) ? df : 0f;
                var dCloth = clothDemand.TryGetValue(provinceId, out var dc) ? dc : 0f;
                var dLux = luxuryDemand.TryGetValue(provinceId, out var dl) ? dl : 0f;

                var sFood = ConsumeType(stock, goodIdToType, ledger, GoodType.Food, dFood, epsilon);
                var sCloth = ConsumeType(stock, goodIdToType, ledger, GoodType.Manufactured, dCloth, epsilon);
                var sLux = ConsumeType(stock, goodIdToType, ledger, GoodType.Luxury, dLux, epsilon);

                foodDeficit[provinceId] = math.max(0f, dFood - sFood);
                clothDeficit[provinceId] = math.max(0f, dCloth - sCloth);
                luxuryDeficit[provinceId] = math.max(0f, dLux - sLux);

                var snap = em.GetComponentData<PhysicalDemandSnapshot>(entity);
                snap.FoodDemand = dFood;
                snap.ClothDemand = dCloth;
                snap.LuxuryDemand = dLux;
                snap.FoodSatisfied = sFood;
                snap.ClothSatisfied = sCloth;
                snap.LuxurySatisfied = sLux;
                var rFood = dFood > epsilon ? math.min(1f, sFood / dFood) : 1f;
                var rCloth = dCloth > epsilon ? math.min(1f, sCloth / dCloth) : 1f;
                var rLux = dLux > epsilon ? math.min(1f, sLux / dLux) : 1f;
                snap.PhysicalSatisfaction = rFood * 0.6f + rCloth * 0.3f + rLux * 0.1f;
                em.SetComponentData(entity, snap);
            }
        }

        /// <summary>
        /// Consommation monde idéal : une seule offre mondiale par type (comme le LOD),
        /// satisfaction identique partout. Prouve si l'écart est dans les définitions.
        /// </summary>
        static void ConsumeIdealPool(
            NativeList<int> provinceIds,
            NativeHashMap<int, Entity> provinceEntityById,
            EntityManager em,
            NativeHashMap<int, GoodType> goodIdToType,
            DynamicBuffer<PhysicalLedgerEntry> ledger,
            NativeHashMap<int, float> foodDemand,
            NativeHashMap<int, float> clothDemand,
            NativeHashMap<int, float> luxuryDemand,
            NativeHashMap<int, float> foodDeficit,
            NativeHashMap<int, float> clothDeficit,
            NativeHashMap<int, float> luxuryDeficit,
            float epsilon)
        {
            var dFood = 0f;
            var dCloth = 0f;
            var dLux = 0f;
            for (var p = 0; p < provinceIds.Length; p++)
            {
                var id = provinceIds[p];
                dFood += foodDemand.TryGetValue(id, out var df) ? df : 0f;
                dCloth += clothDemand.TryGetValue(id, out var dc) ? dc : 0f;
                dLux += luxuryDemand.TryGetValue(id, out var dl) ? dl : 0f;
            }

            // Fusionner tous les stocks dans un buffer virtuel (province min id).
            var hubId = provinceIds.Length > 0 ? provinceIds[0] : -1;
            if (hubId < 0)
            {
                return;
            }

            var hubEntity = provinceEntityById[hubId];
            var hubStock = em.GetBuffer<ProvinceStock>(hubEntity);

            for (var p = 0; p < provinceIds.Length; p++)
            {
                var id = provinceIds[p];
                if (id == hubId)
                {
                    continue;
                }

                var entity = provinceEntityById[id];
                var stock = em.GetBuffer<ProvinceStock>(entity);
                for (var i = 0; i < stock.Length; i++)
                {
                    if (stock[i].Quantity > epsilon)
                    {
                        AddToStock(hubStock, stock[i].GoodId, stock[i].Quantity);
                    }
                }

                stock.Clear();
            }

            var sFood = ConsumeType(hubStock, goodIdToType, ledger, GoodType.Food, dFood, epsilon);
            var sCloth = ConsumeType(hubStock, goodIdToType, ledger, GoodType.Manufactured, dCloth, epsilon);
            var sLux = ConsumeType(hubStock, goodIdToType, ledger, GoodType.Luxury, dLux, epsilon);

            var rFood = dFood > epsilon ? math.min(1f, sFood / dFood) : 1f;
            var rCloth = dCloth > epsilon ? math.min(1f, sCloth / dCloth) : 1f;
            var rLux = dLux > epsilon ? math.min(1f, sLux / dLux) : 1f;
            var sat = rFood * 0.6f + rCloth * 0.3f + rLux * 0.1f;

            for (var p = 0; p < provinceIds.Length; p++)
            {
                var id = provinceIds[p];
                var entity = provinceEntityById[id];
                var localFood = foodDemand.TryGetValue(id, out var df) ? df : 0f;
                var localCloth = clothDemand.TryGetValue(id, out var dc) ? dc : 0f;
                var localLux = luxuryDemand.TryGetValue(id, out var dl) ? dl : 0f;

                var snap = em.GetComponentData<PhysicalDemandSnapshot>(entity);
                snap.FoodDemand = localFood;
                snap.ClothDemand = localCloth;
                snap.LuxuryDemand = localLux;
                snap.FoodSatisfied = localFood * rFood;
                snap.ClothSatisfied = localCloth * rCloth;
                snap.LuxurySatisfied = localLux * rLux;
                snap.PhysicalSatisfaction = sat;
                em.SetComponentData(entity, snap);

                foodDeficit[id] = localFood * (1f - rFood);
                clothDeficit[id] = localCloth * (1f - rCloth);
                luxuryDeficit[id] = localLux * (1f - rLux);
            }
        }

        static void DrainCargosIntoStock(
            DynamicBuffer<CargoInTransit> cargos,
            NativeHashMap<int, Entity> provinceEntityById,
            EntityManager em,
            float epsilon)
        {
            for (var i = 0; i < cargos.Length; i++)
            {
                var c = cargos[i];
                if (c.Quantity <= epsilon)
                {
                    continue;
                }

                if (provinceEntityById.TryGetValue(c.DestProvinceId, out var dest))
                {
                    AddToStock(em.GetBuffer<ProvinceStock>(dest), c.GoodId, c.Quantity);
                }
                else if (provinceEntityById.TryGetValue(c.OriginProvinceId, out var origin))
                {
                    AddToStock(em.GetBuffer<ProvinceStock>(origin), c.GoodId, c.Quantity);
                }
            }

            cargos.Clear();
        }

        static float ConsumeType(
            DynamicBuffer<ProvinceStock> stock,
            NativeHashMap<int, GoodType> goodIdToType,
            DynamicBuffer<PhysicalLedgerEntry> ledger,
            GoodType type,
            float demand,
            float epsilon)
        {
            if (demand <= epsilon)
            {
                return 0f;
            }

            var indices = new NativeList<int>(8, Allocator.Temp);
            for (var i = 0; i < stock.Length; i++)
            {
                if (stock[i].Quantity <= epsilon)
                {
                    continue;
                }

                if (!goodIdToType.TryGetValue(stock[i].GoodId, out var t) || t != type)
                {
                    continue;
                }

                indices.Add(i);
            }

            for (var a = 1; a < indices.Length; a++)
            {
                var key = indices[a];
                var keyGood = stock[key].GoodId;
                var b = a - 1;
                while (b >= 0 && stock[indices[b]].GoodId > keyGood)
                {
                    indices[b + 1] = indices[b];
                    b--;
                }

                indices[b + 1] = key;
            }

            var remaining = (double)demand;
            var satisfied = 0.0;
            for (var k = 0; k < indices.Length && remaining > epsilon; k++)
            {
                var idx = indices[k];
                var entry = stock[idx];
                var take = math.min(entry.Quantity, remaining);
                if (take <= epsilon)
                {
                    continue;
                }

                entry.Quantity -= take;
                stock[idx] = entry;
                AddLedgerConsumption(ledger, entry.GoodId, take);
                if (RecordClothBalance && entry.GoodId == ClothGoodId)
                {
                    LastTickClothConsumedId += take;
                }

                remaining -= take;
                satisfied += take;
            }

            indices.Dispose();
            return (float)satisfied;
        }

        static void DispatchTransport(
            NativeList<int> provinceIds,
            NativeHashMap<int, Entity> provinceEntityById,
            EntityManager em,
            DynamicBuffer<CargoInTransit> cargos,
            NativeHashMap<int, GoodType> goodIdToType,
            NativeHashMap<int, float> foodDeficit,
            NativeHashMap<int, float> clothDeficit,
            NativeHashMap<int, float> luxuryDeficit,
            NativeHashMap<long, float> edgeUsed,
            float capacity,
            float capacityPerDev,
            int transitTicks,
            float epsilon)
        {
            var shipments = new NativeList<Shipment>(64, Allocator.TempJob);
            var bySeverity = ServiceOrderMode == TransportServiceOrder.ByDeficitSeverity;

            if (RecordTransportShares)
            {
                ClearTransportShareCounters();
            }

            NativeParallelMultiHashMap<int, int> adjacency = default;
            var hasAdj = MultiHopTransport;
            if (hasAdj)
            {
                adjacency = BuildLandAdjacency(provinceIds, provinceEntityById, em);
            }

            for (var p = 0; p < provinceIds.Length; p++)
            {
                var fromId = provinceIds[p];
                var fromEntity = provinceEntityById[fromId];
                var neighbors = em.GetBuffer<ProvinceNeighbor>(fromEntity);
                var stock = em.GetBuffer<ProvinceStock>(fromEntity);

                var candidates = new NativeList<GoodCandidate>(stock.Length, Allocator.Temp);
                for (var i = 0; i < stock.Length; i++)
                {
                    if (stock[i].Quantity > epsilon)
                    {
                        candidates.Add(new GoodCandidate
                        {
                            GoodId = stock[i].GoodId,
                            Priority = 0f,
                            BestNeighbor = -1
                        });
                    }
                }

                // Pré-calcul du meilleur pull (priorité) avant tri — sinon ByGoodId
                // consommait la capacité avant même d'évaluer le drap (v1_030).
                for (var g = 0; g < candidates.Length; g++)
                {
                    var cand = candidates[g];
                    if (!goodIdToType.TryGetValue(cand.GoodId, out var preType))
                    {
                        continue;
                    }

                    FindBestTransportTarget(
                        fromId, cand.GoodId, preType, epsilon, neighbors, hasAdj, adjacency,
                        provinceEntityById, em, foodDeficit, clothDeficit, luxuryDeficit,
                        out var neigh, out var prePull);
                    cand.Priority = prePull;
                    cand.BestNeighbor = neigh;
                    candidates[g] = cand;
                }

                if (bySeverity)
                {
                    candidates.Sort(new GoodCandidateSeverityComparer());
                }
                else
                {
                    candidates.Sort(new GoodCandidateIdComparer());
                }

                for (var g = 0; g < candidates.Length; g++)
                {
                    var goodId = candidates[g].GoodId;
                    var priority = candidates[g].Priority;
                    var bestNeighbor = candidates[g].BestNeighbor;
                    var bestPull = priority;
                    if (!goodIdToType.TryGetValue(goodId, out var type))
                    {
                        continue;
                    }

                    var stockIdx = FindStockIndex(stock, goodId);
                    if (stockIdx < 0)
                    {
                        continue;
                    }

                    var surplus = stock[stockIdx].Quantity;
                    if (surplus <= epsilon)
                    {
                        continue;
                    }

                    if (bestNeighbor < 0 || bestPull <= epsilon)
                    {
                        continue;
                    }

                    var edgeKey = EdgeKey(fromId, bestNeighbor);
                    var used = edgeUsed.TryGetValue(edgeKey, out var u) ? u : 0f;
                    var edgeCap = ResolveEdgeCapacity(
                        em, provinceEntityById, fromId, bestNeighbor, capacity, capacityPerDev);
                    var room = edgeCap - used;
                    if (room <= epsilon)
                    {
                        continue;
                    }

                    var amount = math.min(surplus, (double)math.min(bestPull, room));
                    if (amount <= epsilon)
                    {
                        continue;
                    }

                    shipments.Add(new Shipment
                    {
                        OriginProvinceId = fromId,
                        DestProvinceId = bestNeighbor,
                        GoodId = goodId,
                        Quantity = amount,
                        Priority = priority
                    });
                }

                candidates.Dispose();
            }

            if (hasAdj)
            {
                adjacency.Dispose();
            }

            if (bySeverity)
            {
                shipments.Sort(new ShipmentSeverityComparer());
            }
            else
            {
                shipments.Sort(new ShipmentComparer());
            }

            for (var i = 0; i < shipments.Length; i++)
            {
                var s = shipments[i];
                if (!provinceEntityById.TryGetValue(s.OriginProvinceId, out var fromEntity))
                {
                    continue;
                }

                if (!provinceEntityById.TryGetValue(s.DestProvinceId, out var destEntity))
                {
                    continue;
                }

                if (!goodIdToType.TryGetValue(s.GoodId, out var type))
                {
                    continue;
                }

                var stock = em.GetBuffer<ProvinceStock>(fromEntity);
                var idx = FindStockIndex(stock, s.GoodId);
                if (idx < 0)
                {
                    continue;
                }

                var available = stock[idx].Quantity;
                var edgeKey = EdgeKey(s.OriginProvinceId, s.DestProvinceId);
                var used = edgeUsed.TryGetValue(edgeKey, out var u) ? u : 0f;
                var edgeCap = ResolveEdgeCapacity(
                    em, provinceEntityById, s.OriginProvinceId, s.DestProvinceId,
                    capacity, capacityPerDev);
                var room = edgeCap - used;

                if (RecordTransportShares &&
                    s.GoodId >= 0 && s.GoodId < TransportShareSlots)
                {
                    LastTickCapRoomSumByGood[s.GoodId] += room;
                    LastTickCapRoomCountByGood[s.GoodId]++;
                    if (room <= epsilon)
                    {
                        LastTickCapExhaustedByGood[s.GoodId]++;
                    }
                }

                // s.Quantity déjà borné au pull (direct ou multi-saut). Ne pas
                // re-exiger un pull local : le premier saut peut être un relais.
                var amount = math.min(available, math.min((double)room, s.Quantity));
                if (amount <= epsilon)
                {
                    continue;
                }

                var entry = stock[idx];
                entry.Quantity -= amount;
                stock[idx] = entry;

                edgeUsed[edgeKey] = used + (float)amount;
                var inputBuf = em.GetBuffer<PhysicalInputDeficit>(destEntity);
                ReducePull(
                    foodDeficit, clothDeficit, luxuryDeficit, inputBuf,
                    s.DestProvinceId, s.GoodId, type, (float)amount);

                if (RecordTransportShares &&
                    s.GoodId >= 0 && s.GoodId < TransportShareSlots)
                {
                    LastTickShippedByGood[s.GoodId] += amount;
                }

                if (RecordClothBalance && s.GoodId == ClothGoodId)
                {
                    LastTickClothShipped += amount;
                }

                cargos.Add(new CargoInTransit
                {
                    OriginProvinceId = s.OriginProvinceId,
                    DestProvinceId = s.DestProvinceId,
                    GoodId = s.GoodId,
                    Quantity = amount,
                    TicksRemaining = transitTicks
                });
            }

            var ordered = new NativeList<CargoInTransit>(cargos.Length, Allocator.TempJob);
            for (var i = 0; i < cargos.Length; i++)
            {
                ordered.Add(cargos[i]);
            }

            ordered.Sort(new CargoComparer());
            cargos.Clear();
            for (var i = 0; i < ordered.Length; i++)
            {
                cargos.Add(ordered[i]);
            }

            ordered.Dispose();
            shipments.Dispose();
        }

        /// <summary>
        /// Capacité d'arête : si CapacityPerDevPoint &gt; 0, émerge du développement des
        /// deux provinces ; sinon constante EdgeCapacityPerTick (mode test / legacy).
        /// </summary>
        public static float ResolveEdgeCapacity(
            EntityManager em,
            NativeHashMap<int, Entity> provinceEntityById,
            int fromId,
            int toId,
            float baseCapacity,
            float capacityPerDev)
        {
            if (capacityPerDev <= 0f)
            {
                return baseCapacity;
            }

            var scoreA = DevScore(em, provinceEntityById, fromId);
            var scoreB = DevScore(em, provinceEntityById, toId);
            return capacityPerDev * 0.5f * (scoreA + scoreB);
        }

        static float DevScore(
            EntityManager em,
            NativeHashMap<int, Entity> provinceEntityById,
            int provinceId)
        {
            if (!provinceEntityById.TryGetValue(provinceId, out var entity) ||
                !em.HasComponent<ProvinceDevelopment>(entity))
            {
                return 1f;
            }

            var d = em.GetComponentData<ProvinceDevelopment>(entity);
            var avg = (d.Tax + d.Production + d.Manpower) / 3f;
            return math.max(1f, avg);
        }

        static void FindBestTransportTarget(
            int fromId,
            int goodId,
            GoodType type,
            float epsilon,
            DynamicBuffer<ProvinceNeighbor> neighbors,
            bool hasAdj,
            NativeParallelMultiHashMap<int, int> adjacency,
            NativeHashMap<int, Entity> provinceEntityById,
            EntityManager em,
            NativeHashMap<int, float> foodDeficit,
            NativeHashMap<int, float> clothDeficit,
            NativeHashMap<int, float> luxuryDeficit,
            out int bestNeighbor,
            out float bestPull)
        {
            bestNeighbor = -1;
            bestPull = 0f;
            var landNeighbors = new NativeList<int>(neighbors.Length, Allocator.Temp);
            for (var n = 0; n < neighbors.Length; n++)
            {
                if (!neighbors[n].IsStrait)
                {
                    landNeighbors.Add(neighbors[n].NeighborProvinceId);
                }
            }

            landNeighbors.Sort();

            for (var n = 0; n < landNeighbors.Length; n++)
            {
                var nid = landNeighbors[n];
                if (!provinceEntityById.TryGetValue(nid, out var destEntity))
                {
                    continue;
                }

                var pull = NeighborPull(
                    foodDeficit, clothDeficit, luxuryDeficit,
                    em.GetBuffer<PhysicalInputDeficit>(destEntity),
                    nid, goodId, type, epsilon);

                if (pull > bestPull + epsilon ||
                    (math.abs(pull - bestPull) <= epsilon && pull > epsilon &&
                     (bestNeighbor < 0 || nid < bestNeighbor)))
                {
                    if (pull > epsilon)
                    {
                        bestPull = pull;
                        bestNeighbor = nid;
                    }
                }
            }

            // Multi-saut : aucun voisin direct n'a de pull → chemine vers le
            // déficit le plus proche (sinon stock mort : bois×34 mesuré v1_021).
            if (bestNeighbor < 0 && hasAdj)
            {
                if (TryFindMultiHopTarget(
                        fromId, goodId, type, epsilon,
                        adjacency, provinceEntityById, em,
                        foodDeficit, clothDeficit, luxuryDeficit,
                        out var hop, out var remotePull))
                {
                    bestNeighbor = hop;
                    bestPull = remotePull;
                }
            }

            landNeighbors.Dispose();
        }

        static float NeighborPull(
            NativeHashMap<int, float> food,
            NativeHashMap<int, float> cloth,
            NativeHashMap<int, float> luxury,
            DynamicBuffer<PhysicalInputDeficit> inputDeficit,
            int provinceId,
            int goodId,
            GoodType type,
            float epsilon)
        {
            var pull = 0f;
            if (type == GoodType.Food || type == GoodType.Manufactured || type == GoodType.Luxury)
            {
                pull += TypeDeficit(food, cloth, luxury, provinceId, type);
            }

            for (var i = 0; i < inputDeficit.Length; i++)
            {
                if (inputDeficit[i].GoodId == goodId && inputDeficit[i].Amount > epsilon)
                {
                    pull += inputDeficit[i].Amount;
                    break;
                }
            }

            return pull;
        }

        static NativeParallelMultiHashMap<int, int> BuildLandAdjacency(
            NativeList<int> provinceIds,
            NativeHashMap<int, Entity> provinceEntityById,
            EntityManager em)
        {
            var adj = new NativeParallelMultiHashMap<int, int>(provinceIds.Length * 4, Allocator.TempJob);
            for (var p = 0; p < provinceIds.Length; p++)
            {
                var id = provinceIds[p];
                var neighbors = em.GetBuffer<ProvinceNeighbor>(provinceEntityById[id]);
                for (var n = 0; n < neighbors.Length; n++)
                {
                    if (!neighbors[n].IsStrait &&
                        provinceEntityById.ContainsKey(neighbors[n].NeighborProvinceId))
                    {
                        adj.Add(id, neighbors[n].NeighborProvinceId);
                    }
                }
            }

            return adj;
        }

        /// <summary>
        /// BFS déterministe : premier saut vers le déficit (pop ou intrant) le plus proche.
        /// En cas d'égalité de distance, ProvinceId croissant.
        /// </summary>
        static bool TryFindMultiHopTarget(
            int fromId,
            int goodId,
            GoodType type,
            float epsilon,
            NativeParallelMultiHashMap<int, int> adjacency,
            NativeHashMap<int, Entity> provinceEntityById,
            EntityManager em,
            NativeHashMap<int, float> foodDeficit,
            NativeHashMap<int, float> clothDeficit,
            NativeHashMap<int, float> luxuryDeficit,
            out int firstHop,
            out float remotePull)
        {
            firstHop = -1;
            remotePull = 0f;

            var visited = new NativeHashMap<int, byte>(provinceEntityById.Count, Allocator.TempJob);
            var parentHop = new NativeHashMap<int, int>(provinceEntityById.Count, Allocator.TempJob);
            var queue = new NativeList<int>(provinceEntityById.Count, Allocator.TempJob);

            visited.TryAdd(fromId, 1);
            queue.Add(fromId);
            var head = 0;

            var bestTarget = -1;
            var bestDist = int.MaxValue;
            var bestPull = 0f;
            var distOf = new NativeHashMap<int, int>(provinceEntityById.Count, Allocator.TempJob);
            distOf.TryAdd(fromId, 0);

            while (head < queue.Length)
            {
                var cur = queue[head++];
                var dist = distOf[cur];

                if (cur != fromId && provinceEntityById.TryGetValue(cur, out var ent))
                {
                    var pull = NeighborPull(
                        foodDeficit, clothDeficit, luxuryDeficit,
                        em.GetBuffer<PhysicalInputDeficit>(ent),
                        cur, goodId, type, epsilon);
                    if (pull > epsilon)
                    {
                        if (dist < bestDist ||
                            (dist == bestDist && (bestTarget < 0 || cur < bestTarget)))
                        {
                            bestDist = dist;
                            bestTarget = cur;
                            bestPull = pull;
                        }
                    }
                }

                // Élagage : une fois un déficit trouvé, ne pas explorer plus loin.
                if (dist >= bestDist)
                {
                    continue;
                }

                if (adjacency.TryGetFirstValue(cur, out var neigh, out var it))
                {
                    do
                    {
                        if (visited.TryAdd(neigh, 1))
                        {
                            distOf.TryAdd(neigh, dist + 1);
                            if (cur == fromId)
                            {
                                parentHop.TryAdd(neigh, neigh);
                            }
                            else if (parentHop.TryGetValue(cur, out var curHop))
                            {
                                parentHop.TryAdd(neigh, curHop);
                            }

                            queue.Add(neigh);
                        }
                    }
                    while (adjacency.TryGetNextValue(out neigh, ref it));
                }
            }

            var found = bestTarget >= 0 && parentHop.TryGetValue(bestTarget, out firstHop) &&
                        firstHop >= 0;
            if (found)
            {
                remotePull = bestPull;
            }
            else
            {
                firstHop = -1;
            }

            distOf.Dispose();
            queue.Dispose();
            parentHop.Dispose();
            visited.Dispose();
            return found;
        }

        static void ReducePull(
            NativeHashMap<int, float> food,
            NativeHashMap<int, float> cloth,
            NativeHashMap<int, float> luxury,
            DynamicBuffer<PhysicalInputDeficit> inputDeficit,
            int provinceId,
            int goodId,
            GoodType type,
            float amount)
        {
            var remaining = amount;

            // D'abord le déficit d'intrant spécifique (RawMaterial etc.).
            for (var i = 0; i < inputDeficit.Length && remaining > 0f; i++)
            {
                if (inputDeficit[i].GoodId != goodId)
                {
                    continue;
                }

                var e = inputDeficit[i];
                var take = math.min(e.Amount, remaining);
                e.Amount -= take;
                inputDeficit[i] = e;
                remaining -= take;
            }

            if (remaining > 0f &&
                (type == GoodType.Food || type == GoodType.Manufactured || type == GoodType.Luxury))
            {
                ReduceDeficit(food, cloth, luxury, provinceId, type, remaining);
            }
        }

        void UpdateMetrics(
            ref SystemState state,
            NativeList<int> provinceIds,
            NativeHashMap<int, Entity> provinceEntityById,
            EntityManager em,
            DynamicBuffer<CargoInTransit> cargos,
            NativeHashMap<int, float> foodDeficit,
            NativeHashMap<int, float> clothDeficit,
            NativeHashMap<int, float> luxuryDeficit,
            float epsilon)
        {
            var deficitProvinces = 0;
            var totalStock = 0.0;
            var isolatedStock = 0.0;
            var isolated = 0;

            for (var p = 0; p < provinceIds.Length; p++)
            {
                var id = provinceIds[p];
                var entity = provinceEntityById[id];
                var inDeficit =
                    (foodDeficit.TryGetValue(id, out var fd) && fd > epsilon) ||
                    (clothDeficit.TryGetValue(id, out var cd) && cd > epsilon) ||
                    (luxuryDeficit.TryGetValue(id, out var ld) && ld > epsilon);
                if (inDeficit)
                {
                    deficitProvinces++;
                }

                var neighbors = em.GetBuffer<ProvinceNeighbor>(entity);
                var hasLand = false;
                for (var n = 0; n < neighbors.Length; n++)
                {
                    if (!neighbors[n].IsStrait)
                    {
                        hasLand = true;
                        break;
                    }
                }

                var stock = em.GetBuffer<ProvinceStock>(entity);
                var local = 0.0;
                for (var i = 0; i < stock.Length; i++)
                {
                    local += stock[i].Quantity;
                }

                totalStock += local;
                if (!hasLand)
                {
                    isolated++;
                    isolatedStock += local;
                }
            }

            var transit = 0.0;
            var delaySum = 0f;
            for (var i = 0; i < cargos.Length; i++)
            {
                transit += cargos[i].Quantity;
                delaySum += cargos[i].TicksRemaining;
            }

            var metrics = SystemAPI.GetSingletonRW<PhysicalEconomyMetrics>();
            metrics.ValueRW.LandIsolatedProvinceCount = isolated;
            metrics.ValueRW.ProvincesInDeficit = deficitProvinces;
            metrics.ValueRW.TotalInTransit = (float)transit;
            metrics.ValueRW.MeanDeliveryDelayTicks = cargos.Length > 0
                ? delaySum / cargos.Length
                : 0f;
            metrics.ValueRW.BlockedProductionShare = totalStock > epsilon
                ? (float)(isolatedStock / totalStock)
                : 0f;
            metrics.ValueRW.CargoCount = cargos.Length;
        }

        static void UpdateTickConservation(
            NativeList<int> provinceIds,
            NativeHashMap<int, Entity> provinceEntityById,
            EntityManager em,
            DynamicBuffer<CargoInTransit> cargos,
            DynamicBuffer<PhysicalLedgerEntry> ledger,
            float epsilon)
        {
            // Agrégation en double : chaque Quantity est float (OK localement), mais la
            // somme mondiale post-endowment dépasse la mantissa float — le Δ tick doit
            // comparer des totaux double au ledger double.
            var stockByGood = new NativeHashMap<int, double>(16, Allocator.Temp);
            for (var p = 0; p < provinceIds.Length; p++)
            {
                var stock = em.GetBuffer<ProvinceStock>(provinceEntityById[provinceIds[p]]);
                for (var i = 0; i < stock.Length; i++)
                {
                    var g = stock[i].GoodId;
                    stockByGood[g] = (stockByGood.TryGetValue(g, out var cur) ? cur : 0.0) +
                                     stock[i].Quantity;
                }
            }

            var transitByGood = new NativeHashMap<int, double>(16, Allocator.Temp);
            for (var i = 0; i < cargos.Length; i++)
            {
                var g = cargos[i].GoodId;
                transitByGood[g] = (transitByGood.TryGetValue(g, out var cur) ? cur : 0.0) +
                                   cargos[i].Quantity;
            }

            var maxDrift = 0f;
            for (var i = 0; i < ledger.Length; i++)
            {
                var e = ledger[i];
                var stock = stockByGood.TryGetValue(e.GoodId, out var s) ? s : 0.0;
                var transit = transitByGood.TryGetValue(e.GoodId, out var t) ? t : 0.0;
                var st = stock + transit;

                // Critère = abs OU relatif (CheckConservationPerTick). Ne reporter que les
                // dérives hors tolérance — sinon les totaux 1e7+ font échouer un seuil abs
                // de 50 alors que le critère relatif (1e-3 × flux) est respecté.
                if (!CheckConservationPerTick(
                        st,
                        e.PrevStockPlusTransit,
                        e.CumulativeProduction,
                        e.PrevProduction,
                        e.CumulativeConsumption,
                        e.PrevConsumption,
                        out var tickDrift,
                        out _))
                {
                    if (tickDrift > maxDrift)
                    {
                        maxDrift = tickDrift;
                    }
                }

                e.PrevProduction = e.CumulativeProduction;
                e.PrevConsumption = e.CumulativeConsumption;
                e.PrevStockPlusTransit = st;
                ledger[i] = e;
            }

            s_LastTickMaxDrift = maxDrift;

            stockByGood.Dispose();
            transitByGood.Dispose();
        }

        /// <summary>Staging hors Burst pour publier MaxTickConservationDrift.</summary>
        static float s_LastTickMaxDrift;

        // ----- Helpers stock / ledger / clés -----

        /// <summary>
        /// Instantané monde du drap (GoodId=8) : stocks, transit, ledger, flux tick.
        /// Lecture seule — n'écrit rien. v1_084 bilan.
        /// </summary>
        public struct ClothBalanceSample
        {
            public double Stock;
            public double Transit;
            public double CumulativeProduction;
            public double CumulativeConsumption;
            public double LastTickShipped;
            public double LastTickDelivered;
            public double LastTickConsumedId;
            public float ClothDemand;
            public float ClothSatisfied;
            public float LodClothOutProxy;
            public float WorkshopCapCloth;
            public float MissedOutletShare;
            public float MissedInputShare;
            public float PhysicalOutputTotal;
            public float LodOutputTotal;
            public int CapRoomAttempts;
            public int CapExhaustedAttempts;
            public double CapRoomSum;
        }

        /// <summary>
        /// Agrège le bilan drap mondial. LodClothOutProxy = formule v1_083
        /// (sum LastOutput good_tag=cloth + workshopCap × CapacityIntensity).
        /// </summary>
        public static ClothBalanceSample SampleClothBalance(EntityManager em)
        {
            var sample = new ClothBalanceSample
            {
                LastTickShipped = LastTickClothShipped,
                LastTickDelivered = LastTickClothDelivered,
                LastTickConsumedId = LastTickClothConsumedId
            };

            if (ClothGoodId < TransportShareSlots)
            {
                sample.CapRoomAttempts = LastTickCapRoomCountByGood[ClothGoodId];
                sample.CapExhaustedAttempts = LastTickCapExhaustedByGood[ClothGoodId];
                sample.CapRoomSum = LastTickCapRoomSumByGood[ClothGoodId];
            }

            using (var q = em.CreateEntityQuery(
                       ComponentType.ReadOnly<ProvinceData>(),
                       ComponentType.ReadOnly<ProvinceStock>()))
            using (var entities = q.ToEntityArray(Allocator.Temp))
            {
                for (var i = 0; i < entities.Length; i++)
                {
                    var stock = em.GetBuffer<ProvinceStock>(entities[i]);
                    sample.Stock += GetStockQuantity(stock, ClothGoodId);
                }
            }

            using (var qSing = em.CreateEntityQuery(ComponentType.ReadOnly<PhysicalEconomySingleton>()))
            {
                if (qSing.CalculateEntityCount() > 0)
                {
                    var singleton = qSing.GetSingletonEntity();
                    if (em.HasBuffer<CargoInTransit>(singleton))
                    {
                        var cargos = em.GetBuffer<CargoInTransit>(singleton);
                        for (var i = 0; i < cargos.Length; i++)
                        {
                            if (cargos[i].GoodId == ClothGoodId)
                                sample.Transit += cargos[i].Quantity;
                        }
                    }

                    if (em.HasBuffer<PhysicalLedgerEntry>(singleton))
                    {
                        var ledger = em.GetBuffer<PhysicalLedgerEntry>(singleton);
                        for (var i = 0; i < ledger.Length; i++)
                        {
                            if (ledger[i].GoodId != ClothGoodId)
                                continue;
                            sample.CumulativeProduction = ledger[i].CumulativeProduction;
                            sample.CumulativeConsumption = ledger[i].CumulativeConsumption;
                            break;
                        }
                    }
                }
            }

            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<PhysicalDemandSnapshot>()))
            using (var snaps = q.ToComponentDataArray<PhysicalDemandSnapshot>(Allocator.Temp))
            {
                for (var i = 0; i < snaps.Length; i++)
                {
                    sample.ClothDemand += snaps[i].ClothDemand;
                    sample.ClothSatisfied += snaps[i].ClothSatisfied;
                }
            }

            float lodCloth = 0f;
            using (var q = em.CreateEntityQuery(
                       ComponentType.ReadOnly<ProductionSite>(),
                       ComponentType.ReadOnly<ProvinceData>()))
            using (var sites = q.ToComponentDataArray<ProductionSite>(Allocator.Temp))
            using (var provs = q.ToComponentDataArray<ProvinceData>(Allocator.Temp))
            {
                for (var i = 0; i < sites.Length; i++)
                {
                    if (sites[i].GoodId == ClothGoodId)
                        lodCloth += sites[i].LastOutput;
                    else
                    {
                        var tag = provs[i].GoodTag.ToString();
                        if (tag.Equals("cloth", System.StringComparison.OrdinalIgnoreCase))
                            lodCloth += sites[i].LastOutput;
                    }
                }
            }

            float workshopCap = 0f;
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<BuildingData>()))
            using (var buildings = q.ToComponentDataArray<BuildingData>(Allocator.Temp))
            {
                for (var i = 0; i < buildings.Length; i++)
                {
                    if (buildings[i].IsComplete == 0 || buildings[i].Type != BuildingType.Workshop)
                        continue;
                    workshopCap += buildings[i].CapacityContribution;
                }
            }

            sample.WorkshopCapCloth = workshopCap;
            sample.LodClothOutProxy =
                lodCloth + workshopCap * BuildingConstructionSystem.CapacityIntensity;

            using (var qMet = em.CreateEntityQuery(ComponentType.ReadOnly<PhysicalEconomyMetrics>()))
            {
                if (qMet.CalculateEntityCount() > 0)
                {
                    var m = qMet.GetSingleton<PhysicalEconomyMetrics>();
                    sample.MissedOutletShare = m.MissedOutletShare;
                    sample.MissedInputShare = m.MissedInputShare;
                    sample.PhysicalOutputTotal = m.PhysicalOutputTotal;
                    sample.LodOutputTotal = m.LodOutputTotal;
                }
            }

            return sample;
        }

        public static void AddToStock(DynamicBuffer<ProvinceStock> stock, int goodId, double qty)
        {
            var idx = FindStockIndex(stock, goodId);
            if (idx >= 0)
            {
                var e = stock[idx];
                e.Quantity += qty;
                stock[idx] = e;
            }
            else
            {
                stock.Add(new ProvinceStock { GoodId = goodId, Quantity = qty });
            }
        }

        public static int FindStockIndex(DynamicBuffer<ProvinceStock> stock, int goodId)
        {
            for (var i = 0; i < stock.Length; i++)
            {
                if (stock[i].GoodId == goodId)
                {
                    return i;
                }
            }

            return -1;
        }

        public static double GetStockQuantity(DynamicBuffer<ProvinceStock> stock, int goodId)
        {
            var idx = FindStockIndex(stock, goodId);
            return idx >= 0 ? stock[idx].Quantity : 0.0;
        }

        /// <summary>
        /// Retire jusqu'à <paramref name="qty"/> du stock ; retourne la quantité effectivement retirée.
        /// Utilisé par les chantiers (v1_038) — même pool que la production.
        /// </summary>
        public static double TryRemoveFromStock(DynamicBuffer<ProvinceStock> stock, int goodId, double qty)
        {
            if (qty <= 0.0)
                return 0.0;
            var idx = FindStockIndex(stock, goodId);
            if (idx < 0)
                return 0.0;
            var e = stock[idx];
            var take = math.min(e.Quantity, qty);
            if (take <= 0.0)
                return 0.0;
            e.Quantity -= take;
            stock[idx] = e;
            return take;
        }

        /// <summary>Inscription ledger consommation (chantiers / tests) — même registre que la production.</summary>
        public static void AddLedgerConsumptionPublic(
            DynamicBuffer<PhysicalLedgerEntry> ledger, int goodId, double qty)
        {
            AddLedgerConsumption(ledger, goodId, qty);
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

        static void AddMap(NativeHashMap<int, float> map, int key, float amount)
        {
            if (map.TryGetValue(key, out var cur))
            {
                map[key] = cur + amount;
            }
            else
            {
                map[key] = amount;
            }
        }

        static float TypeDeficit(
            NativeHashMap<int, float> food,
            NativeHashMap<int, float> cloth,
            NativeHashMap<int, float> luxury,
            int provinceId,
            GoodType type)
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

        static void ReduceDeficit(
            NativeHashMap<int, float> food,
            NativeHashMap<int, float> cloth,
            NativeHashMap<int, float> luxury,
            int provinceId,
            GoodType type,
            float amount)
        {
            if (type == GoodType.Food && food.TryGetValue(provinceId, out var f))
            {
                food[provinceId] = math.max(0f, f - amount);
            }
            else if (type == GoodType.Manufactured && cloth.TryGetValue(provinceId, out var c))
            {
                cloth[provinceId] = math.max(0f, c - amount);
            }
            else if (type == GoodType.Luxury && luxury.TryGetValue(provinceId, out var l))
            {
                luxury[provinceId] = math.max(0f, l - amount);
            }
        }

        public static long EdgeKey(int fromId, int toId) =>
            ((long)fromId << 32) | (uint)toId;

        /// <summary>
        /// Identité ledger : stock + transit ≈ prod − conso.
        /// Plafond ABSOLU constant (pas relatif aux cumuls — sinon asymptotiquement infaillible).
        /// Le critère durci est <see cref="CheckConservationPerTick"/>.
        /// </summary>
        public static bool CheckConservation(
            double stockSum,
            double transitSum,
            double cumulativeProduction,
            double cumulativeConsumption,
            out float delta)
        {
            var lhs = stockSum + transitSum;
            var rhs = cumulativeProduction - cumulativeConsumption;
            delta = (float)math.abs(lhs - rhs);
            return delta <= ConservationEpsilonAbsIdentity;
        }

        /// <summary>
        /// Conservation à puissance constante : dérive du tick bornée au flux du tick.
        /// </summary>
        public static bool CheckConservationPerTick(
            double stockPlusTransit,
            double prevStockPlusTransit,
            double cumulativeProduction,
            double prevProduction,
            double cumulativeConsumption,
            double prevConsumption,
            out float tickDrift,
            out float tickFlux)
        {
            var dSt = stockPlusTransit - prevStockPlusTransit;
            var dProd = cumulativeProduction - prevProduction;
            var dCons = cumulativeConsumption - prevConsumption;
            var expected = dProd - dCons;
            tickDrift = (float)math.abs(dSt - expected);
            tickFlux = (float)(math.abs(dProd) + math.abs(dCons) + math.abs(dSt));
            if (tickFlux < 1e-4f)
            {
                tickFlux = 1e-4f;
            }

            return tickDrift <= ConservationEpsilonAbs ||
                   tickDrift <= ConservationEpsilonRelTick * tickFlux;
        }

        /// <summary>
        /// Non-téléportation : une cargaison ne peut franchir qu'une arête par
        /// TransitTicksPerEdge ticks. Distance N ⇒ arrivée au plus tôt à N * delay.
        /// </summary>
        public static int MinTicksToArrive(int edgeDistance, int transitTicksPerEdge)
        {
            if (edgeDistance <= 0)
            {
                return 0;
            }

            var delay = transitTicksPerEdge < 1 ? 1 : transitTicksPerEdge;
            return edgeDistance * delay;
        }

        /// <summary>Publie le max drift du dernier tick dans les métriques (appelé en fin d'OnUpdate).</summary>
        public static float ConsumeLastTickMaxDrift()
        {
            var v = s_LastTickMaxDrift;
            return v;
        }

        struct Shipment
        {
            public int OriginProvinceId;
            public int DestProvinceId;
            public int GoodId;
            public double Quantity;
            /// <summary>Pull/déficit destinataire — critère principal en mode sévérité.</summary>
            public float Priority;
        }

        struct GoodCandidate
        {
            public int GoodId;
            public float Priority;
            public int BestNeighbor;
        }

        struct GoodCandidateIdComparer : IComparer<GoodCandidate>
        {
            public int Compare(GoodCandidate a, GoodCandidate b) =>
                a.GoodId.CompareTo(b.GoodId);
        }

        /// <summary>Sévérité décroissante ; GoodId croissant à égalité (déterministe).</summary>
        struct GoodCandidateSeverityComparer : IComparer<GoodCandidate>
        {
            public int Compare(GoodCandidate a, GoodCandidate b)
            {
                var c = b.Priority.CompareTo(a.Priority);
                return c != 0 ? c : a.GoodId.CompareTo(b.GoodId);
            }
        }

        struct ShipmentComparer : IComparer<Shipment>
        {
            public int Compare(Shipment a, Shipment b)
            {
                var c = a.OriginProvinceId.CompareTo(b.OriginProvinceId);
                if (c != 0)
                {
                    return c;
                }

                c = a.DestProvinceId.CompareTo(b.DestProvinceId);
                if (c != 0)
                {
                    return c;
                }

                return a.GoodId.CompareTo(b.GoodId);
            }
        }

        /// <summary>
        /// Même arête : sévérité du déficit d'abord (desc), GoodId ensuite (asc).
        /// </summary>
        struct ShipmentSeverityComparer : IComparer<Shipment>
        {
            public int Compare(Shipment a, Shipment b)
            {
                var c = a.OriginProvinceId.CompareTo(b.OriginProvinceId);
                if (c != 0)
                {
                    return c;
                }

                c = a.DestProvinceId.CompareTo(b.DestProvinceId);
                if (c != 0)
                {
                    return c;
                }

                c = b.Priority.CompareTo(a.Priority);
                return c != 0 ? c : a.GoodId.CompareTo(b.GoodId);
            }
        }

        public struct CargoComparer : IComparer<CargoInTransit>
        {
            public int Compare(CargoInTransit a, CargoInTransit b)
            {
                var c = a.OriginProvinceId.CompareTo(b.OriginProvinceId);
                if (c != 0)
                {
                    return c;
                }

                c = a.DestProvinceId.CompareTo(b.DestProvinceId);
                if (c != 0)
                {
                    return c;
                }

                c = a.GoodId.CompareTo(b.GoodId);
                if (c != 0)
                {
                    return c;
                }

                return a.TicksRemaining.CompareTo(b.TicksRemaining);
            }
        }
    }
}
