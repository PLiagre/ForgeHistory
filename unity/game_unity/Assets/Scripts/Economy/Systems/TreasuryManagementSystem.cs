using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;

namespace VictoriaGame.Economy
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TreasurySystem))]
    public partial struct TreasuryManagementSystem : ISystem
    {
        /// <summary>
        /// Coussin de trésorerie conservé avant remboursement de dette.
        /// v1_016 : 75 → 20 (sur-correction : dette mondiale → 0 partout).
        /// v1_017 : 20 → 75 — restaure la pression de dette ; le filet anti-spirale
        /// reste MaxCountryDebt / MaxInterestPerTick / navy landless, pas le remboursement.
        /// </summary>
        public const float DebtRepayBuffer = 75f;

        /// <summary>
        /// Fraction de l'excédent au-dessus du buffer affectée au remboursement par tick.
        /// v1_016 : 0.20 → 0.45 (sur-correction : dette éliminée trop vite).
        /// v1_017 : 0.45 → 0.22 — proche de l'original 0.20, léger coup de pouce pour
        /// rester dans la bande ~450@t1000 sans plat à zéro.
        /// </summary>
        public const float DebtRepayFraction = 0.22f;

        /// <summary>Plafond de réserve pour les pays sans dette — l'excédent est drainé en douceur.</summary>
        public const float ReserveCap = 750f;

        /// <summary>Taux de drain de l'excédent au-dessus de <see cref="ReserveCap"/> par tick.</summary>
        public const float SkimRate = 0.08f;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<VictoriaGame.Core.WorldState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.HasSingleton<VictoriaGame.Core.WorldState>())
            {
                return;
            }

            var worldState = SystemAPI.GetSingleton<VictoriaGame.Core.WorldState>();
            if (worldState.IsPaused)
            {
                return;
            }

            new TreasuryManagementJob().Run();
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstCompile]
        private partial struct TreasuryManagementJob : IJobEntity
        {
            public void Execute(ref TreasuryData treasury)
            {
                if (treasury.Debt > 0f && treasury.Balance > DebtRepayBuffer)
                {
                    var repay = math.min(
                        treasury.Debt,
                        (treasury.Balance - DebtRepayBuffer) * DebtRepayFraction);
                    treasury.Debt -= repay;
                    treasury.Balance -= repay;
                }
                else if (treasury.Debt <= 0f && treasury.Balance > ReserveCap)
                {
                    var skim = (treasury.Balance - ReserveCap) * SkimRate;
                    treasury.Balance -= skim;
                }
            }
        }
    }
}
