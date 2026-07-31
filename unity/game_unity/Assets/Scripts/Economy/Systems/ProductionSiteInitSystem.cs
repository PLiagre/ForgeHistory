using System.Collections.Generic;
using Unity.Entities;
using VictoriaGame.Core;
using VictoriaGame.World;
using UnityEngine;

namespace VictoriaGame.Economy
{
    /// <summary>
    /// Ajoute ProductionSite sur chaque entité province, d'après ProvinceData.GoodTag
    /// et ProvinceDevelopment.Production.
    ///
    /// BuildingEfficiencySystem et ProductionSystem requièrent ProductionSite sur la même
    /// entité que ProvinceData — pas sur une entité bâtiment séparée.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(MapInitSystem))]
    [UpdateAfter(typeof(GoodInitSystem))]
    public partial struct ProductionSiteInitSystem : ISystem
    {
        const float ProductionScale = 2000f;

        /// <summary>
        /// Multiplicateur de rendement Food (calibré seed 42195) pour une scarcité résiduelle :
        /// ratioFood ~0,65–0,70 à t50, needsSatAvg ~0,78 (croissance posée, sous le seuil 0,8).
        /// RawMaterial / Manufactured / Luxury restent à yield 1.
        /// </summary>
        const float FoodYield = 2.0f;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WorldState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var goodIdByTag = IndexGoodsByTag(ref state);
            var pending = new List<(Entity entity, ProductionSite site)>();
            var unknownTag = 0;

            foreach (var (province, dev, entity) in
                     SystemAPI.Query<RefRO<ProvinceData>, RefRO<ProvinceDevelopment>>()
                         .WithNone<ProductionSite>()
                         .WithEntityAccess())
            {
                var tag = province.ValueRO.GoodTag.ToString();
                if (string.IsNullOrEmpty(tag))
                {
                    unknownTag++;
                    continue;
                }

                if (!goodIdByTag.TryGetValue(tag, out var goodEntry))
                {
                    Debug.LogWarning(
                        $"ProductionSiteInitSystem: GoodTag '{tag}' (province {province.ValueRO.ProvinceId}) " +
                        "ne correspond à aucun GoodData — pas de ProductionSite");
                    unknownTag++;
                    continue;
                }

                var yield = YieldForType(goodEntry.Type);

                pending.Add((entity, new ProductionSite
                {
                    GoodId = goodEntry.GoodId,
                    BaseOutput = dev.ValueRO.Production * ProductionScale * yield,
                    Efficiency = 1f,
                    LastOutput = 0f,
                    Method = default,
                }));
            }

            foreach (var (entity, site) in pending)
            {
                em.AddComponentData(entity, site);
            }

            Debug.Log($"ProductionSiteInitSystem: {pending.Count} sites de production créés");

            if (unknownTag > 0)
            {
                Debug.LogWarning(
                    $"ProductionSiteInitSystem: {unknownTag} province(s) sans ProductionSite — " +
                    "GoodTag absent ou introuvable dans goods.json");
            }

            state.Enabled = false;
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        private struct GoodIndexEntry
        {
            public int GoodId;
            public GoodType Type;
        }

        private static float YieldForType(GoodType type)
        {
            return type switch
            {
                GoodType.Food => FoodYield,
                _ => 1f,
            };
        }

        private Dictionary<string, GoodIndexEntry> IndexGoodsByTag(ref SystemState state)
        {
            var map = new Dictionary<string, GoodIndexEntry>();

            foreach (var good in SystemAPI.Query<RefRO<GoodData>>())
            {
                var tag = good.ValueRO.Tag.ToString();
                if (!string.IsNullOrEmpty(tag))
                {
                    map[tag] = new GoodIndexEntry
                    {
                        GoodId = good.ValueRO.GoodId,
                        Type = good.ValueRO.Type,
                    };
                }
            }

            return map;
        }
    }
}
