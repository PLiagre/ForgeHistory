using Unity.Entities;
using Unity.Collections;
using System;

namespace VictoriaGame.Politics
{
    public enum GovernmentType : byte
    {
        Feudal = 0,
        Absolute = 1,
        Oligarchic = 2,
        Theocratic = 3,
        Republic = 4,
    }

    /// <summary>
    /// Régime politique, légitimité et stabilité du gouvernement (entité pays).
    /// </summary>
    [Serializable]
    public struct GovernmentData : IComponentData
    {
        public GovernmentType Type;
        public float Legitimacy;
        public float Stability;
        public float Autonomy;
        public int ReformProgress;
        public FixedString32Bytes RulerTag;
        public int RulerAge;
        public int ReignStartTick;
    }
}
