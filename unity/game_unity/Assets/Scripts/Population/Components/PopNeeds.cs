using Unity.Entities;

namespace VictoriaGame.Population
{
    [System.Serializable]
    public struct PopNeeds : IComponentData
    {
        public float FoodNeed;
        public float ClothNeed;
        public float LuxuryNeed;
        public float FoodSatisfied;
        public float ClothSatisfied;
        public float LuxurySatisfied;
    }
}
