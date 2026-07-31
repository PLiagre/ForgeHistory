using System;
using Unity.Entities;

namespace VictoriaGame.Core
{
    [Serializable]
    public struct TechData : IComponentData
    {
        public int MilTech;
        public int EcoTech;
        public int AdmTech;
    }
}
