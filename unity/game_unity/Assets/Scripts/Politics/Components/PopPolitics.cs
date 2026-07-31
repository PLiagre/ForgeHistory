using Unity.Entities;
using VictoriaGame.Core;

namespace VictoriaGame.Politics
{
    public struct PopPolitics : IComponentData
    {
        public IdeologyType Ideology;
        public float Radicalism;
        public float Loyalty;
        public float PoliticalPower;
        public int LastUnrestTick;
    }
}
