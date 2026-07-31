using Unity.Entities;

namespace VictoriaGame.Economy
{
    [System.Serializable]
    public struct MarketPrice : IComponentData
    {
        public float BasePrice;
        public float CurrentPrice;
        public float Supply;
        public float Demand;
        public float PriceTrend;
    }
}
