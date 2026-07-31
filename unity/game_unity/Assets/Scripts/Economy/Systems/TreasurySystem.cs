using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;

namespace VictoriaGame.Economy
{
    public enum BankruptcyMode : byte
    {
        /// <summary>Comportement pré-eco_029 : une seule conversion Balance→Debt, puis plus de plancher.</summary>
        OneShotLegacy = 0,
        /// <summary>Banqueroute répétable avec haircut de défaut (eco_029).</summary>
        RepeatableHaircut = 1
    }

    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TaxSystem))]
    public partial struct TreasurySystem : ISystem
    {
        /// <summary>Solde sous lequel la banqueroute convertit le déficit en dette.</summary>
        public const float BankruptcyThreshold = -500f;

        /// <summary>
        /// Fraction de la créance effacée à chaque défaut (0 = report intégral / spirale ;
        /// 1 = annulation totale / pression nulle). Calibré eco_029 : 0.5 → debt~1254 (spirale soft) ;
        /// 0.7 → debt~601 bornée ≤~1000 ; 0.0 → debt~6044 spirale.
        /// v1_016 : 0.7 → 0.85 (retained 0.15) — défauts répétés bornaient mal sous navy+intérêt.
        /// v1_017 : 0.85 → 0.70 — navy/intérêt déjà plafonnés ; haircut eco_029 suffit.
        /// (La dette reste ~0 faute de banqueroutes : le filet structurel empêche Balance&lt;-500.)
        /// </summary>
        public const float BankruptcyHaircut = 0.70f;

        /// <summary>
        /// Taux d'intérêt annuel appliqué sur Debt (v1_016 : 0.05 → 0.02).
        /// À 0.05 l'intérêt agrégé dépassait le surplus d'exploitation (~t2500).
        /// </summary>
        public const float DebtInterestRateAnnual = 0.02f;

        /// <summary>
        /// Plafond d'intérêt débités par tick et par pays (v1_016 : ∞ → 1.5).
        /// Empêche l'amplificateur composé de dépasser durablement un surplus local viable.
        /// </summary>
        public const float MaxInterestPerTick = 1.5f;

        /// <summary>
        /// Plafond dur de dette par pays (v1_016 : ∞ → 1200).
        /// Garantit que les défauts répétés ne peuvent plus faire croître Debt sans limite.
        /// </summary>
        public const float MaxCountryDebt = 1200f;

        public static float Haircut = BankruptcyHaircut;
        public static BankruptcyMode Mode = BankruptcyMode.RepeatableHaircut;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<VictoriaGame.Core.WorldState>();
        }

        // Pas de [BurstCompile] : lecture de Haircut/Mode (static mutable) hors Burst (BC1040).
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

            var treasuryJob = new TreasuryUpdateJob
            {
                CurrentTick = worldState.CurrentTick,
                Haircut = Haircut,
                Mode = Mode,
                InterestRateAnnual = DebtInterestRateAnnual,
                MaxInterestPerTick = MaxInterestPerTick,
                MaxCountryDebt = MaxCountryDebt
            };
            treasuryJob.Run();
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstCompile]
        private partial struct TreasuryUpdateJob : IJobEntity
        {
            public int CurrentTick;
            public float Haircut;
            public BankruptcyMode Mode;
            public float InterestRateAnnual;
            public float MaxInterestPerTick;
            public float MaxCountryDebt;

            public void Execute(ref TreasuryData treasury)
            {
                treasury.Balance += treasury.Income - treasury.Expenses;

                if (treasury.Debt > 0f)
                {
                    var interest = treasury.Debt * (InterestRateAnnual / 12f);
                    interest = math.min(interest, MaxInterestPerTick);
                    treasury.Balance -= interest;
                }

                if (Mode == BankruptcyMode.OneShotLegacy)
                {
                    if (treasury.Balance < BankruptcyThreshold && treasury.BankruptcyTick == 0)
                    {
                        treasury.BankruptcyTick = CurrentTick;
                        treasury.Debt = math.min(
                            MaxCountryDebt,
                            treasury.Debt + math.abs(treasury.Balance));
                        treasury.Balance = 0f;
                        treasury.BankruptcyCount = 1;
                    }

                    return;
                }

                // Banqueroute répétable : vrai plancher du solde + haircut anti-spirale + plafond.
                if (treasury.Balance < BankruptcyThreshold)
                {
                    var absBalance = math.abs(treasury.Balance);
                    var retained = math.clamp(1f - Haircut, 0f, 1f);
                    treasury.Debt = math.min(
                        MaxCountryDebt,
                        treasury.Debt + absBalance * retained);
                    treasury.Balance = 0f;
                    treasury.BankruptcyTick = CurrentTick;
                    treasury.BankruptcyCount += 1;
                }
            }
        }
    }
}
