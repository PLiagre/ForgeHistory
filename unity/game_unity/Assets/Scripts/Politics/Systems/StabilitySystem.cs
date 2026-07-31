using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using VictoriaGame.Core;
using VictoriaGame.Economy;

namespace VictoriaGame.Politics
{
    /// <summary>
    /// Stabilité nationale (v1_091) — repondération réversible des termes existants.
    /// <para>
    /// Coefficient <see cref="StabilityReweight"/> : 0 = comportement legacy bit-identique
    /// (parité <c>ParityAnchors.Expected</c>) ; &gt;0 atténue dérive et pénalité de
    /// légitimité basse, amplifie les bonus (solde / légitimité haute). Aucun signal nouveau.
    /// </para>
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(RevolutionSystem))]
    public partial struct StabilitySystem : ISystem
    {
        /// <summary>Défaut compilé = 0 (no-op / réversibilité bit-identique).</summary>
        public const float DefaultStabilityReweight = 0f;

        /// <summary>
        /// DÉS-ADOPTÉ par le CTO le 2026-07-28, le jour même de l'adoption. Le mécanisme
        /// reste, la valeur retombe au no-op — c'est le sens conservateur.
        /// <para>
        /// v1_091 avait adopté <c>0,6</c> sur un balayage monotone en moyenne. La moyenne
        /// est trompeuse ici, et le log du brief le dit lui-même : « distribution bimodale
        /// sous clamp [0..1] ». Les chiffres fondateurs de l'adoption sont
        /// <c>floor 16/20 → 9/20</c> ET <c>ceil 3/20 → 9/20</c> : on passe de 19 pays sur 20
        /// collés à une borne à 18 sur 20. La stabilité reste un BOOLÉEN, pas une jauge.
        /// </para>
        /// <para>
        /// Et la sélection s'est retournée contre elle-même : le score de choix contient
        /// <c>balance = 1 − |floor − ceil| / 20</c>, qui vaut son MAXIMUM quand autant de pays
        /// sont collés en bas qu'en haut. Il a donc récompensé le partage 9/9, c'est-à-dire
        /// la bimodalité parfaite. Le seuil d'acceptation <c>ceil &lt; 14</c> est en outre
        /// nommé à la main, ce que le brief interdisait.
        /// </para>
        /// <para>
        /// AUCUNE valeur du balayage ne produit une distribution étalée, et ce n'est pas un
        /// défaut de réglage : chaque pays a un budget de stabilité de signe FIXE (la
        /// légitimité médiane vaut 0,299 à t500, t1000, t2000 ET t3000 — 14/20 sous 0,3 aux
        /// quatre ticks, la partition ne bouge jamais), donc chaque pays court vers une
        /// borne et s'y colle. Repondérer choisit LAQUELLE, pas si. Il faut un terme dont le
        /// signe VARIE dans le temps — sujet de v1_092.
        /// </para>
        /// </summary>
        public const float AdoptedStabilityReweight = 0f;

        /// <summary>Valeur mesurée par v1_091, conservée pour rejouer le balayage.</summary>
        public const float V1091SweptReweight = 0.6f;

        /// <summary>Mutable pour harnais / calibration (lu hors Burst dans OnUpdate).</summary>
        public static float StabilityReweight = DefaultStabilityReweight;

        static bool _harnessLocked;
        static bool _adoptedApplied;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WorldState>();
            ApplyAdoptedIfUnlocked();
        }

        // Pas de [BurstCompile] sur OnUpdate : lecture du static mutable StabilityReweight
        // hors Burst (BC1040), passé en champ au job Burst.
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.HasSingleton<WorldState>())
            {
                return;
            }

            if (SystemAPI.GetSingleton<WorldState>().IsPaused)
            {
                return;
            }

            float reweight = math.saturate(StabilityReweight);
            state.Dependency = new StabilityUpdateJob
            {
                Reweight = reweight
            }.ScheduleParallel(state.Dependency);
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        /// <summary>Verrouille le coefficient pour un harnais de mesure.</summary>
        public static void LockReweight(float value)
        {
            StabilityReweight = math.saturate(value);
            _harnessLocked = true;
            _adoptedApplied = true;
        }

        /// <summary>
        /// Pose le défaut compilé (0) et empêche l'adoption OnCreate —
        /// appelé par SimulationHarness avant création du monde.
        /// </summary>
        public static void EnsureParitySafeDefaults()
        {
            if (_harnessLocked)
            {
                return;
            }

            StabilityReweight = DefaultStabilityReweight;
            _adoptedApplied = true;
        }

        /// <summary>Remet les statics au défaut compilé (sans ré-adopter).</summary>
        public static void ResetToCompiledDefault()
        {
            StabilityReweight = DefaultStabilityReweight;
            _harnessLocked = false;
            _adoptedApplied = false;
        }

        static void ApplyAdoptedIfUnlocked()
        {
            if (_harnessLocked || _adoptedApplied)
            {
                return;
            }

            StabilityReweight = AdoptedStabilityReweight;
            _adoptedApplied = true;
        }

        [BurstCompile]
        private partial struct StabilityUpdateJob : IJobEntity
        {
            public float Reweight;

            public void Execute(ref GovernmentData gov, in TreasuryData treasury, in RevolutionData rev)
            {
                float stab = gov.Stability;
                float w = Reweight;

                // Chemin legacy explicite : littéraux identiques au pré-v1_091 (réversibilité).
                if (w <= 0f)
                {
                    if (treasury.Income - treasury.Expenses > 0f)
                    {
                        stab += 0.001f;
                    }

                    if (treasury.Debt > treasury.Balance * 2f)
                    {
                        stab -= 0.002f;
                    }

                    if (rev.IsRevolutionActive)
                    {
                        stab -= 0.005f;
                    }

                    if (gov.Legitimacy > 0.6f)
                    {
                        stab += 0.001f;
                    }

                    if (gov.Legitimacy < 0.3f)
                    {
                        stab -= 0.002f;
                    }

                    stab -= 0.0005f;
                }
                else
                {
                    // w>0 → atténue dérive, dette et légitimité basse (co-coupables mesurés) ;
                    // amplifie légèrement les bonus. Aucun signal nouveau.
                    float surplusBonus = 0.001f * (1f + w);
                    float debtPenalty = 0.002f * (1f - w);
                    float revPenalty = 0.005f;
                    float legHighBonus = 0.001f * (1f + w);
                    float legLowPenalty = 0.002f * (1f - w);
                    float drift = 0.0005f * (1f - w);

                    if (treasury.Income - treasury.Expenses > 0f)
                    {
                        stab += surplusBonus;
                    }

                    if (treasury.Debt > treasury.Balance * 2f)
                    {
                        stab -= debtPenalty;
                    }

                    if (rev.IsRevolutionActive)
                    {
                        stab -= revPenalty;
                    }

                    if (gov.Legitimacy > 0.6f)
                    {
                        stab += legHighBonus;
                    }

                    if (gov.Legitimacy < 0.3f)
                    {
                        stab -= legLowPenalty;
                    }

                    stab -= drift;
                }

                gov.Stability = math.clamp(stab, 0f, 1f);
            }
        }
    }
}
