using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using VictoriaGame.Core;
using VictoriaGame.Economy;

namespace VictoriaGame.Military
{
    /// <summary>
    /// Mode de gate solvabilité (mesures eco_027 : AVANT / eco_026 / eco_027).
    /// Production = <see cref="FluxCommitted"/>.
    /// </summary>
    public enum ArmySolvencyGateMode : byte
    {
        /// <summary>eco_025 — aucun désarmement forcé, aucun frein recrutement.</summary>
        Disabled = 0,
        /// <summary>eco_026 — juge le STOCK (Balance &lt; 0).</summary>
        StockBalance = 1,
        /// <summary>eco_027 — juge le FLUX + coût engagé à maturité.</summary>
        FluxCommitted = 2
    }

    /// <summary>
    /// Mode de gate CROISSANCE (eco_033). Recrutement reste toujours sur coût engagé.
    /// Production = <see cref="AffordableStrength"/>.
    /// </summary>
    public enum ArmyGrowthGateMode : byte
    {
        /// <summary>eco_027 — coût engagé à maturité de tous les slots (DEADLOCK, réf. mesure).</summary>
        MatureCommitted = 0,
        /// <summary>eco_033 — plafond force soutenable dérivé du flux courant.</summary>
        AffordableStrength = 1
    }

    /// <summary>
    /// Désarmement forcé progressif : licencie un <see cref="RegimentSlot"/> quand le pays
    /// est fauché ET en déficit de flux (Income &lt; Expenses). Critères partagés avec
    /// <see cref="TemplateRecruitSystem"/> et <see cref="ArmyOrganizationSystem"/>.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TreasurySystem))]
    public partial struct ArmyDisbandmentSystem : ISystem
    {
        /// <summary>Mode actif (défaut production = flux engagé). Les mesures basculent ce flag.</summary>
        public static ArmySolvencyGateMode GateMode = ArmySolvencyGateMode.FluxCommitted;

        /// <summary>Gate croissance (défaut = plafond force soutenable eco_033).</summary>
        public static ArmyGrowthGateMode GrowthGateMode = ArmyGrowthGateMode.AffordableStrength;

        /// <summary>Solde en dessous duquel le pays est fauché (BROKE_THRESHOLD).</summary>
        public const float BrokeThreshold = 0f;

        /// <summary>Alias eco_026 — même valeur que <see cref="BrokeThreshold"/>.</summary>
        public const float InsolvencyThreshold = BrokeThreshold;

        /// <summary>
        /// Excédent minimum au coût ENGAGÉ pour autoriser recrutement.
        /// Bande morte vs désarmement (déficit) : évite l'oscillation tick-à-tick.
        /// </summary>
        public const float RecruitMargin = 0.05f;

        /// <summary>
        /// Marge or/tick réservée avant le plafond de force soutenable (hystérésis eco_033).
        /// Défaut = RecruitMargin ; mutable pour calibration mesure.
        /// </summary>
        public const float DefaultGrowthMargin = 0.05f;

        /// <summary>Mutable pour mesures / calibration (lue hors Burst dans CanAffordGrowth).</summary>
        public static float GrowthMargin = DefaultGrowthMargin;

        /// <summary>
        /// Multiplicateur de vitesse de reconstitution des VÉTÉRANS sous-effectif
        /// (eco_034). Recrutement initial (IsRecruiting) reste à pleine vitesse.
        /// eco_034 : 0.16 → worldArmyStr t1000 ≈ 40k.
        /// v1_016 : 0.16 → 0.45 — buff militaire global (armée ×2) hors périmètre.
        /// v1_017 : 0.45 → 0.18 — proche de eco_034. À 0.16 exact : survie OK (ratio~0.35)
        /// mais zombie@t1000=5395 (annexion mid-disband). Sweep : 0.18 = min sans zombie
        /// avec scale~58k (≪ 93k du buff) et survie@t3000. Demi-t1000 exige ≥0.24 (scale hors bande).
        /// </summary>
        public const float DefaultReinforceRateFactor = 0.18f;

        /// <summary>Mutable pour mesures / calibration ; passé en champ au job Burst.</summary>
        public static float ReinforceRateFactor = DefaultReinforceRateFactor;

        /// <summary>Force d'un régiment à maturité (ArmyOrganizationSystem).</summary>
        public const float MatureRegimentStrength = 1000f;

        /// <summary>Intervalle de ticks entre deux licenciements (calibré eco_026).</summary>
        public const int DisbandInterval = 8;

        private ComponentLookup<TreasuryData> _treasuryLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WorldState>();
            _treasuryLookup = state.GetComponentLookup<TreasuryData>(true);
        }

        // Pas de [BurstCompile] sur OnUpdate : lecture du static mutable GateMode
        // hors Burst (BC1040), passé en champ au DisbandJob Burst.
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.HasSingleton<WorldState>())
            {
                return;
            }

            var worldState = SystemAPI.GetSingleton<WorldState>();
            if (worldState.IsPaused)
            {
                return;
            }

            var gateMode = GateMode;
            if (gateMode == ArmySolvencyGateMode.Disabled)
            {
                return;
            }

            _treasuryLookup.Update(ref state);

            var job = new DisbandJob
            {
                TreasuryLookup = _treasuryLookup,
                CurrentTick = worldState.CurrentTick,
                GateMode = gateMode
            };
            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        public void OnDestroy(ref SystemState state) { }

        /// <summary>Critère stock (eco_026) : Balance strictement inférieur au seuil.</summary>
        public static bool IsInsolvent(float balance) => balance < BrokeThreshold;

        public static bool IsBroke(float balance) => balance < BrokeThreshold;

        public static bool IsInDeficit(float income, float expenses) => income < expenses;

        /// <summary>Entretien d'un régiment mûr (1000 × ArmyUpkeepRate).</summary>
        public static float MatureRegimentUpkeep =>
            MatureRegimentStrength * MilitaryUpkeepSystem.ArmyUpkeepRate;

        public static float MatureRegimentUpkeepAt(float armyUpkeepRate) =>
            MatureRegimentStrength * armyUpkeepRate;

        public static float CommittedArmyUpkeep(int regimentCount) =>
            regimentCount * MatureRegimentUpkeep;

        public static float CommittedArmyUpkeep(int regimentCount, float armyUpkeepRate) =>
            regimentCount * MatureRegimentUpkeepAt(armyUpkeepRate);

        /// <summary>Dépenses hors entretien d'armée (admin + marine), dérivées du Expenses constaté.</summary>
        public static float OtherExpenses(float totalExpenses, float actualArmyStrength) =>
            OtherExpenses(totalExpenses, actualArmyStrength, MilitaryUpkeepSystem.ArmyUpkeepRate);

        public static float OtherExpenses(
            float totalExpenses, float actualArmyStrength, float armyUpkeepRate)
        {
            var armyUpkeep = actualArmyStrength * armyUpkeepRate;
            var other = totalExpenses - armyUpkeep;
            return other > 0f ? other : 0f;
        }

        /// <summary>Désarmer ? Fauché + déficit (flux), ou Balance&lt;0 en mode stock.</summary>
        public static bool ShouldDisband(in TreasuryData treasury, ArmySolvencyGateMode mode)
        {
            switch (mode)
            {
                case ArmySolvencyGateMode.Disabled:
                    return false;
                case ArmySolvencyGateMode.StockBalance:
                    return IsInsolvent(treasury.Balance);
                default:
                    return IsBroke(treasury.Balance)
                        && IsInDeficit(treasury.Income, treasury.Expenses);
            }
        }

        public static bool ShouldDisband(in TreasuryData treasury) =>
            ShouldDisband(treasury, GateMode);

        /// <summary>
        /// Force maximale soutenable par le flux (hors entretien armée actuel),
        /// après réserve <see cref="GrowthMargin"/>.
        /// </summary>
        public static float AffordableArmyStrength(
            in TreasuryData treasury,
            float actualArmyStrength,
            float growthMargin) =>
            AffordableArmyStrength(
                treasury, actualArmyStrength, growthMargin, MilitaryUpkeepSystem.ArmyUpkeepRate);

        public static float AffordableArmyStrength(
            in TreasuryData treasury,
            float actualArmyStrength,
            float growthMargin,
            float armyUpkeepRate)
        {
            var other = OtherExpenses(treasury.Expenses, actualArmyStrength, armyUpkeepRate);
            var net = treasury.Income - other - growthMargin;
            if (net <= 0f)
            {
                return 0f;
            }

            return net / armyUpkeepRate;
        }

        /// <summary>
        /// Croissance des régiments en recrutement. Pays solvable (Balance ≥ 0) : libre.
        /// Pays fauché (eco_033) : autorisé tant que force ACTUELLE &lt; plafond soutenable
        /// dérivé du flux — plus le coût engagé à maturité (deadlock eco_027).
        /// </summary>
        public static bool CanAffordGrowth(
            in TreasuryData treasury,
            int regimentCount,
            float actualArmyStrength,
            ArmySolvencyGateMode mode)
        {
            return CanAffordGrowth(
                treasury, regimentCount, actualArmyStrength, mode, GrowthGateMode, GrowthMargin,
                MilitaryUpkeepSystem.ArmyUpkeepRate);
        }

        public static bool CanAffordGrowth(
            in TreasuryData treasury,
            int regimentCount,
            float actualArmyStrength,
            ArmySolvencyGateMode mode,
            ArmyGrowthGateMode growthMode,
            float growthMargin)
        {
            return CanAffordGrowth(
                treasury, regimentCount, actualArmyStrength, mode, growthMode, growthMargin,
                MilitaryUpkeepSystem.ArmyUpkeepRate);
        }

        public static bool CanAffordGrowth(
            in TreasuryData treasury,
            int regimentCount,
            float actualArmyStrength,
            ArmySolvencyGateMode mode,
            ArmyGrowthGateMode growthMode,
            float growthMargin,
            float armyUpkeepRate)
        {
            switch (mode)
            {
                case ArmySolvencyGateMode.Disabled:
                    return true;
                case ArmySolvencyGateMode.StockBalance:
                    return !IsInsolvent(treasury.Balance);
                default:
                    // Solvable : ne pas freiner (sinon divergence MIL/FRA/BYZ vs eco_026).
                    if (!IsBroke(treasury.Balance))
                    {
                        return true;
                    }

                    if (growthMode == ArmyGrowthGateMode.MatureCommitted)
                    {
                        // Référence eco_027 / mil_023 (deadlock) — pour mesures A/B seulement.
                        var other = OtherExpenses(treasury.Expenses, actualArmyStrength, armyUpkeepRate);
                        var surplus = treasury.Income - other
                                      - CommittedArmyUpkeep(regimentCount, armyUpkeepRate);
                        return surplus > RecruitMargin;
                    }

                    {
                        // eco_033 : converge vers l'armée réellement payable.
                        var ceiling = AffordableArmyStrength(
                            treasury, actualArmyStrength, growthMargin, armyUpkeepRate);
                        return actualArmyStrength < ceiling;
                    }
            }
        }

        public static bool CanAffordGrowth(
            in TreasuryData treasury,
            int regimentCount,
            float actualArmyStrength) =>
            CanAffordGrowth(treasury, regimentCount, actualArmyStrength, GateMode);

        /// <summary>
        /// Recruter un régiment de plus. Solvable : libre. Fauché : projette le coût à
        /// maturité de (count+1) slots — jamais les Expenses constatées (coût différé).
        /// </summary>
        public static bool CanAffordRecruit(
            in TreasuryData treasury,
            int regimentCount,
            float actualArmyStrength,
            ArmySolvencyGateMode mode) =>
            CanAffordRecruit(
                treasury, regimentCount, actualArmyStrength, mode,
                MilitaryUpkeepSystem.ArmyUpkeepRate);

        public static bool CanAffordRecruit(
            in TreasuryData treasury,
            int regimentCount,
            float actualArmyStrength,
            ArmySolvencyGateMode mode,
            float armyUpkeepRate)
        {
            switch (mode)
            {
                case ArmySolvencyGateMode.Disabled:
                    return true;
                case ArmySolvencyGateMode.StockBalance:
                    return !IsInsolvent(treasury.Balance);
                default:
                    if (!IsBroke(treasury.Balance))
                    {
                        return true;
                    }

                    {
                        var other = OtherExpenses(treasury.Expenses, actualArmyStrength, armyUpkeepRate);
                        var surplus = treasury.Income - other
                                      - CommittedArmyUpkeep(regimentCount + 1, armyUpkeepRate);
                        return surplus > RecruitMargin;
                    }
            }
        }

        public static bool CanAffordRecruit(
            in TreasuryData treasury,
            int regimentCount,
            float actualArmyStrength) =>
            CanAffordRecruit(treasury, regimentCount, actualArmyStrength, GateMode);

        [BurstCompile]
        private partial struct DisbandJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<TreasuryData> TreasuryLookup;
            public int CurrentTick;
            public ArmySolvencyGateMode GateMode;

            public void Execute(ref ArmyData army, DynamicBuffer<RegimentSlot> slots)
            {
                if (army.Country == Entity.Null || !TreasuryLookup.HasComponent(army.Country))
                {
                    return;
                }

                if (!ShouldDisband(TreasuryLookup[army.Country], GateMode))
                {
                    return;
                }

                if (army.IsEngaged || slots.Length == 0)
                {
                    return;
                }

                if (CurrentTick % DisbandInterval != 0)
                {
                    return;
                }

                slots.RemoveAt(slots.Length - 1);

                float sum = 0f;
                for (int i = 0; i < slots.Length; i++)
                {
                    sum += slots[i].Strength;
                }

                army.Strength = sum;
            }
        }
    }
}
