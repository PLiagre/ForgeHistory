using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using VictoriaGame.Core;
using VictoriaGame.Economy;
using VictoriaGame.Politics;

namespace VictoriaGame.Military
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(MilitaryUpkeepSystem))]
    [UpdateBefore(typeof(TreasurySystem))]
    public partial struct TemplateRecruitSystem : ISystem
    {
        /// <summary>
        /// Calibré eco_031 seed 42195 : ramène RecruitCostGold (10) à un ordre
        /// soutenable vs budget (~1–8). 0 = gratuit (réf. eco_030).
        /// v1_018 : 0.05 → 0.12 — recrutement en guerre pèse sur le trésor (canal PARTIE 1) ;
        /// calibré avec ArmyUpkeepRate / AdminCost par balayage pression vs survie.
        /// </summary>
        public const float DefaultRecruitCostScale = 0.12f;

        /// <summary>Mutable pour mesures / calibration (lue hors Burst dans OnUpdate).</summary>
        public static float RecruitCostScale = DefaultRecruitCostScale;

        /// <summary>
        /// v1_091 — sortie stabilité → capacité de recrutement.
        /// 0 = no-op strict (ignore Stability) ; &gt;0 module MaxRegiments par lerp(1, stab, scale).
        /// Ne touche ni coût, ni entretien, ni solde.
        /// </summary>
        public const float DefaultStabilityRecruitScale = 0f;

        /// <summary>Valeur adoptée après mesure de boucle (v1_091). 0 si boucle divergente.</summary>
        public const float AdoptedStabilityRecruitScale = 0f;

        /// <summary>Mutable pour harnais / calibration.</summary>
        public static float StabilityRecruitScale = DefaultStabilityRecruitScale;

        static bool _stabRecruitLocked;
        static bool _stabRecruitApplied;

        private ComponentLookup<RegimentTemplate> _templateLookup;
        private ComponentLookup<TechData> _techLookup;
        private ComponentLookup<TreasuryData> _treasuryLookup;
        private ComponentLookup<GovernmentData> _govLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WorldState>();
            _templateLookup = state.GetComponentLookup<RegimentTemplate>(true);
            _techLookup = state.GetComponentLookup<TechData>(true);
            _treasuryLookup = state.GetComponentLookup<TreasuryData>(false);
            _govLookup = state.GetComponentLookup<GovernmentData>(true);
            ApplyAdoptedStabilityRecruitIfUnlocked();
        }

        // Pas de [BurstCompile] sur OnUpdate : lecture des statics mutables
        // hors Burst (BC1040), passés en champs au RecruitJob Burst.
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

            _templateLookup.Update(ref state);
            _techLookup.Update(ref state);
            _treasuryLookup.Update(ref state);
            _govLookup.Update(ref state);

            state.Dependency.Complete();

            // Agrégation pays → slots / force : .Run() uniquement (déterminisme, pas de parallel).
            var regimentCounts = new NativeHashMap<Entity, int>(64, Allocator.TempJob);
            var armyStrengths = new NativeHashMap<Entity, float>(64, Allocator.TempJob);

            new AggregateRegimentCountsJob
            {
                RegimentCounts = regimentCounts
            }.Run();

            new AggregateArmyStrengthJob
            {
                ArmyStrengths = armyStrengths
            }.Run();

            var currentTick = SystemAPI.GetSingleton<WorldState>().CurrentTick;
            var gateMode = ArmyDisbandmentSystem.GateMode;
            float recruitCostScale = RecruitCostScale;
            float armyUpkeepRate = MilitaryUpkeepSystem.ArmyUpkeepRate;
            float stabilityRecruitScale = math.saturate(StabilityRecruitScale);

            // .Run() : met à jour RegimentCounts après chaque recrutement (multi-armées / pays).
            // Écriture TreasuryData.Expenses en RMW séquentiel (déterministe).
            new RecruitJob
            {
                TemplateLookup = _templateLookup,
                TechLookup = _techLookup,
                TreasuryLookup = _treasuryLookup,
                GovLookup = _govLookup,
                RegimentCounts = regimentCounts,
                ArmyStrengths = armyStrengths,
                CurrentTick = currentTick,
                GateMode = gateMode,
                RecruitCostScale = recruitCostScale,
                ArmyUpkeepRate = armyUpkeepRate,
                StabilityRecruitScale = stabilityRecruitScale
            }.Run();

            regimentCounts.Dispose();
            armyStrengths.Dispose();
        }

        public void OnDestroy(ref SystemState state) { }

        /// <summary>Verrouille le coefficient de sortie pour un harnais.</summary>
        public static void LockStabilityRecruitScale(float value)
        {
            StabilityRecruitScale = math.saturate(value);
            _stabRecruitLocked = true;
            _stabRecruitApplied = true;
        }

        /// <summary>Pose 0 et empêche l'adoption OnCreate (parité).</summary>
        public static void EnsureParitySafeDefaults()
        {
            if (_stabRecruitLocked)
            {
                return;
            }

            StabilityRecruitScale = DefaultStabilityRecruitScale;
            _stabRecruitApplied = true;
        }

        /// <summary>Remet StabilityRecruitScale au défaut compilé (0).</summary>
        public static void ResetStabilityRecruitToCompiledDefault()
        {
            StabilityRecruitScale = DefaultStabilityRecruitScale;
            _stabRecruitLocked = false;
            _stabRecruitApplied = false;
        }

        static void ApplyAdoptedStabilityRecruitIfUnlocked()
        {
            if (_stabRecruitLocked || _stabRecruitApplied)
            {
                return;
            }

            StabilityRecruitScale = AdoptedStabilityRecruitScale;
            _stabRecruitApplied = true;
        }

        [BurstCompile]
        private partial struct AggregateRegimentCountsJob : IJobEntity
        {
            public NativeHashMap<Entity, int> RegimentCounts;

            public void Execute(in ArmyData army, in DynamicBuffer<RegimentSlot> slots)
            {
                if (army.Country == Entity.Null)
                {
                    return;
                }

                RegimentCounts.TryGetValue(army.Country, out int current);
                RegimentCounts[army.Country] = current + slots.Length;
            }
        }

        [BurstCompile]
        private partial struct AggregateArmyStrengthJob : IJobEntity
        {
            public NativeHashMap<Entity, float> ArmyStrengths;

            public void Execute(in ArmyData army)
            {
                if (army.Country == Entity.Null)
                {
                    return;
                }

                ArmyStrengths.TryGetValue(army.Country, out float current);
                ArmyStrengths[army.Country] = current + army.Strength;
            }
        }

        [BurstCompile]
        private partial struct RecruitJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<RegimentTemplate> TemplateLookup;
            [ReadOnly] public ComponentLookup<TechData> TechLookup;
            public ComponentLookup<TreasuryData> TreasuryLookup;
            [ReadOnly] public ComponentLookup<GovernmentData> GovLookup;
            public NativeHashMap<Entity, int> RegimentCounts;
            [ReadOnly] public NativeHashMap<Entity, float> ArmyStrengths;
            public int CurrentTick;
            public ArmySolvencyGateMode GateMode;
            public float RecruitCostScale;
            public float ArmyUpkeepRate;
            public float StabilityRecruitScale;

            public void Execute(ref ArmyData army, DynamicBuffer<RegimentSlot> slots)
            {
                if (army.Country == Entity.Null)
                {
                    return;
                }

                if (!TemplateLookup.HasComponent(army.Country) || !TechLookup.HasComponent(army.Country))
                {
                    return;
                }

                if (!TreasuryLookup.HasComponent(army.Country))
                {
                    return;
                }

                RegimentCounts.TryGetValue(army.Country, out int countryRegiments);
                ArmyStrengths.TryGetValue(army.Country, out float countryStrength);

                if (!ArmyDisbandmentSystem.CanAffordRecruit(
                        TreasuryLookup[army.Country],
                        countryRegiments,
                        countryStrength,
                        GateMode,
                        ArmyUpkeepRate))
                {
                    return;
                }

                var template = TemplateLookup[army.Country];
                var tech = TechLookup[army.Country];

                if (tech.MilTech < template.MilTechRequired)
                {
                    return;
                }

                // Capacité effective : scale=0 → MaxRegiments inchangé (no-op).
                int effectiveMax = template.MaxRegiments;
                if (StabilityRecruitScale > 0f && GovLookup.HasComponent(army.Country))
                {
                    float stab = math.saturate(GovLookup[army.Country].Stability);
                    float capacity = math.lerp(1f, stab, StabilityRecruitScale);
                    effectiveMax = (int)math.floor(template.MaxRegiments * capacity + 1e-4f);
                    if (effectiveMax < 1 && capacity > 0f)
                    {
                        effectiveMax = 1;
                    }
                }

                if (slots.Length >= effectiveMax)
                {
                    return;
                }

                var type = GetRegimentTypeForMilTech(tech.MilTech);
                slots.Add(new RegimentSlot
                {
                    Type = type,
                    Strength = 0f,
                    Organization = 0f,
                    Morale = 50f,
                    IsRecruiting = true,
                    RecruitStartTick = CurrentTick
                });

                RegimentCounts[army.Country] = countryRegiments + 1;

                // Coût en capital ponctuel via le canal Expenses (financé par TreasurySystem).
                // Pas de gate Balance ≥ coût — préserve le rebuild VEN (eco_027).
                var treasury = TreasuryLookup[army.Country];
                treasury.Expenses += template.RecruitCostGold * RecruitCostScale;
                TreasuryLookup[army.Country] = treasury;

                float sum = 0f;
                for (int i = 0; i < slots.Length; i++)
                {
                    sum += slots[i].Strength;
                }
                army.Strength = sum;
            }

            private static RegimentType GetRegimentTypeForMilTech(int milTech)
            {
                if (milTech < 1)
                {
                    return RegimentType.MedievalInfantry;
                }
                if (milTech <= 2)
                {
                    return RegimentType.MedievalInfantry;
                }
                if (milTech <= 4)
                {
                    return RegimentType.PikeAndShot;
                }
                if (milTech <= 6)
                {
                    return RegimentType.MusketInfantry;
                }
                if (milTech <= 8)
                {
                    return RegimentType.LineInfantry;
                }
                return RegimentType.RifleInfantry;
            }
        }
    }
}
