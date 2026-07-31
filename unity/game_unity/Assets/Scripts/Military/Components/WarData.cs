using Unity.Entities;
using System;
using VictoriaGame.Core;

namespace VictoriaGame.Military
{
    /// <summary>
    /// État de guerre entre deux pays. Une entité War porte ce composant ;
    /// plusieurs guerres peuvent coexister (~50 max).
    /// </summary>
    [Serializable]
    public struct WarData : IComponentData
    {
        /// <summary>Pays attaquant (celui qui a déclaré la guerre).</summary>
        public Entity Attacker;

        /// <summary>Pays défenseur.</summary>
        public Entity Defender;

        /// <summary>Tick WorldState de la déclaration de guerre.</summary>
        public int StartTick;

        /// <summary>Tick WorldState de la conclusion de paix (0 = guerre en cours).</summary>
        public int EndTick;

        /// <summary>Score de guerre : -100 (défenseur gagne) à +100 (attaquant gagne).</summary>
        public float WarScore;

        /// <summary>Motif de la guerre.</summary>
        public CasusBelli CasusBelli;

        /// <summary>False après la paix ; les systèmes ignorent les guerres inactives.</summary>
        public bool IsActive;

        /// <summary>Initialise une guerre active avec un score neutre.</summary>
        public static WarData Create(Entity attacker, Entity defender, CasusBelli casusBelli, int startTick)
        {
            return new WarData
            {
                Attacker = attacker,
                Defender = defender,
                StartTick = startTick,
                EndTick = 0,
                WarScore = 0f,
                CasusBelli = casusBelli,
                IsActive = true
            };
        }
    }
}
