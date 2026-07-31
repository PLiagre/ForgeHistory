using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using VictoriaGame.Core;
using VictoriaGame.Economy;

namespace VictoriaGame.Military
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TemplateRecruitSystem))]
    public partial struct ArmyOrganizationSystem : ISystem
    {
        private ComponentLookup<TreasuryData> _treasuryLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WorldState>();
            _treasuryLookup = state.GetComponentLookup<TreasuryData>(true);
        }

        // Pas de [BurstCompile] sur OnUpdate : lecture des statics mutables
        // ArmyDisbandmentSystem.GateMode/GrowthGateMode/… hors Burst (BC1040).
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

            _treasuryLookup.Update(ref state);
            state.Dependency.Complete();

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

            var job = new OrgRegenJob
            {
                TreasuryLookup = _treasuryLookup,
                RegimentCounts = regimentCounts,
                ArmyStrengths = armyStrengths,
                GateMode = ArmyDisbandmentSystem.GateMode,
                GrowthMode = ArmyDisbandmentSystem.GrowthGateMode,
                GrowthMargin = ArmyDisbandmentSystem.GrowthMargin,
                ReinforceRateFactor = ArmyDisbandmentSystem.ReinforceRateFactor,
                ArmyUpkeepRate = MilitaryUpkeepSystem.ArmyUpkeepRate
            };
            state.Dependency = job.ScheduleParallel(state.Dependency);
            state.Dependency.Complete();

            regimentCounts.Dispose();
            armyStrengths.Dispose();
        }

        public void OnDestroy(ref SystemState state) { }

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
        private partial struct OrgRegenJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<TreasuryData> TreasuryLookup;
            [ReadOnly] public NativeHashMap<Entity, int> RegimentCounts;
            [ReadOnly] public NativeHashMap<Entity, float> ArmyStrengths;
            public ArmySolvencyGateMode GateMode;
            public ArmyGrowthGateMode GrowthMode;
            public float GrowthMargin;
            public float ReinforceRateFactor;
            public float ArmyUpkeepRate;

            public void Execute(ref ArmyData army, DynamicBuffer<RegimentSlot> slots)
            {
                var canGrow = true;
                if (army.Country != Entity.Null && TreasuryLookup.HasComponent(army.Country))
                {
                    RegimentCounts.TryGetValue(army.Country, out int countryRegiments);
                    ArmyStrengths.TryGetValue(army.Country, out float countryStrength);
                    canGrow = ArmyDisbandmentSystem.CanAffordGrowth(
                        TreasuryLookup[army.Country],
                        countryRegiments,
                        countryStrength,
                        GateMode,
                        GrowthMode,
                        GrowthMargin,
                        ArmyUpkeepRate);
                }

                // Org/morale : uniquement hors combat. Force : aussi pendant l'engagement
                // (v1_016) — sinon les fronts permanents + attrition → worldArmyStr→0
                // malgré la solvabilité retrouvée (P1–P3), et les armées à 0 force
                // coincées IsEngaged ne se reconstituaient jamais.
                var regenRate = math.lerp(0.1f, 2.0f, army.SupplyLevel);
                if (!army.IsEngaged)
                {
                    army.Organization = math.min(100f, army.Organization + regenRate);
                    army.Morale = math.min(100f, army.Morale + regenRate * 0.5f);
                }

                // eco_033 AffordableStrength : renforcer tout régiment sous-effectif
                // (y compris pertes de combat). MatureCommitted : uniquement IsRecruiting
                // (identité mil_023 / eco_027 pour la baseline mesure).
                // eco_034 : REINFORCE_RATE_FACTOR ne ralentit que la RÉPARATION des
                // vétérans (!wasRecruiting) ; recrutement initial à pleine vitesse.
                // On ne repasse PAS IsRecruiting=true sur un vétéran en réparation,
                // sinon il devient indiscernable d'une recrue au tick suivant.
                for (int i = 0; i < slots.Length; i++)
                {
                    var slot = slots[i];
                    var wasRecruiting = slot.IsRecruiting;
                    var understrength = slot.Strength < ArmyDisbandmentSystem.MatureRegimentStrength;
                    var mayReinforce = wasRecruiting
                        || GrowthMode == ArmyGrowthGateMode.AffordableStrength;

                    if (understrength && mayReinforce)
                    {
                        if (canGrow)
                        {
                            var rateFactor = wasRecruiting ? 1f : ReinforceRateFactor;
                            // Front engagé : reconstitution ralentie mais non nulle.
                            if (army.IsEngaged && !wasRecruiting)
                            {
                                rateFactor *= 0.5f;
                            }

                            // Solvable + SupplyLevel≈0 : plancher de reconstitution
                            // (évite les régiments fantômes à 0 force bloqués sans logistique).
                            var supply = army.SupplyLevel;
                            if (canGrow && supply < 0.25f)
                            {
                                supply = 0.25f;
                            }

                            slot.Strength = math.min(
                                ArmyDisbandmentSystem.MatureRegimentStrength,
                                slot.Strength + 10f * supply * rateFactor);
                            if (slot.Strength >= ArmyDisbandmentSystem.MatureRegimentStrength)
                            {
                                slot.IsRecruiting = false;
                                slot.Organization = 50f;
                                slot.Morale = 75f;
                            }
                            else if (wasRecruiting)
                            {
                                slot.IsRecruiting = true;
                            }
                            // else : vétéran en réparation — laisse IsRecruiting=false
                        }
                    }
                    else if (!slot.IsRecruiting && !army.IsEngaged)
                    {
                        slot.Organization = math.min(100f, slot.Organization + regenRate);
                        slot.Morale = math.min(100f, slot.Morale + regenRate * 0.5f);
                    }

                    slots[i] = slot;
                }

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
