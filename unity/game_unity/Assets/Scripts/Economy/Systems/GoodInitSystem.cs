using Unity.Entities;
using Unity.Collections;
using VictoriaGame.Core;
using VictoriaGame.Utils;
using UnityEngine;

namespace VictoriaGame.Economy
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(WorldBootstrapSystem))]
    public partial struct GoodInitSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WorldState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var goods = GameDataLoader.LoadGoods();

            foreach (var def in goods)
            {
                var entity = state.EntityManager.CreateEntity();

                state.EntityManager.AddComponentData(entity, new GoodData
                {
                    GoodId = def.id,
                    Type = ParseGoodType(def.type),
                    Tag = new FixedString32Bytes(def.tag ?? string.Empty),
                });

                state.EntityManager.AddComponentData(entity, new MarketPrice
                {
                    BasePrice = def.base_price,
                    CurrentPrice = def.base_price,
                    Supply = 0f,
                    Demand = 0f,
                    PriceTrend = 0f,
                });
            }

            Debug.Log($"GoodInitSystem: {goods.Count} biens créés");
            state.Enabled = false;
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        private static GoodType ParseGoodType(string type)
        {
            return type switch
            {
                "Food" => GoodType.Food,
                "RawMaterial" => GoodType.RawMaterial,
                "Manufactured" => GoodType.Manufactured,
                "Luxury" => GoodType.Luxury,
                _ => GoodType.Food,
            };
        }
    }
}
