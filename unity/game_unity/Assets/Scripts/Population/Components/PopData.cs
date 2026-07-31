using Unity.Entities;
using Unity.Collections;
using System;
using VictoriaGame.Core;

namespace VictoriaGame.Population
{
    [Serializable]
    public struct PopData : IComponentData
    {
        public PopType Type;
        public int Size;
        public Entity Province;
        public Entity Country;
        public FixedString32Bytes CultureTag;
        public FixedString32Bytes ReligionTag;
        public float Literacy;
        public float NeedsSatisfaction;
        public float PoliticalRadicalism;
        public int BirthTick;
    }
}
