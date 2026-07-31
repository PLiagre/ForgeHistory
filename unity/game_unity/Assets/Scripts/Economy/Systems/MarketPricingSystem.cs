using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using VictoriaGame.Core;
using VictoriaGame.Economy;

namespace VictoriaGame.Economy
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(MarketAggregationSystem))]
    public partial struct MarketPricingSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WorldState>();
        }

        [BurstCompile]
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

            var pricingJob = new MarketPricingJob();
            pricingJob.Run();
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstCompile]
        private partial struct MarketPricingJob : IJobEntity
        {
            public void Execute(ref MarketPrice price)
            {
                float prevPrice = price.CurrentPrice;

                float demand = math.max(price.Demand, 0.01f);
                float ratio = price.Supply / demand;

                float supplyFactor = math.clamp((1f - ratio) * 0.1f, -0.05f, 0.05f);

                float newPrice = prevPrice * (1f + supplyFactor);

                float minPrice = price.BasePrice * 0.1f;
                float maxPrice = price.BasePrice * 5.0f;
                price.CurrentPrice = math.clamp(newPrice, minPrice, maxPrice);

                price.PriceTrend = math.clamp(
                    (price.CurrentPrice - prevPrice) / math.max(prevPrice, 0.01f),
                    -1f,
                    1f
                );
            }
        }
    }
}
