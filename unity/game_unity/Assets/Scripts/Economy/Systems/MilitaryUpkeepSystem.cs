using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using VictoriaGame.Military;
using VictoriaGame.Navy;
using VictoriaGame.World;

namespace VictoriaGame.Economy
{
    /// <summary>
    /// Mode A/B du coût administratif : forfait plat (baseline dip_005) vs ∝ territoire.
    /// </summary>
    public enum AdminCostMode : byte
    {
        /// <summary>Forfait plat BaseAdminCost=0.5 par pays (baseline dip_005 exacte).</summary>
        FlatBaseline = 0,

        /// <summary>adminCost = AdminCostPerProvince × provinces × (1 + AdminSuperlinearPerProvince × provinces).</summary>
        PerProvince = 1
    }

    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TaxSystem))]
    [UpdateBefore(typeof(TreasurySystem))]
    public partial struct MilitaryUpkeepSystem : ISystem
    {
        /// <summary>
        /// Entretien armée / point de force. eco_024 : 0.00012.
        /// v1_018 : 0.00012 → 0.00028 — la guerre (armée mobilisée) creuse un déficit réel
        /// sans ruiner dès la déclaration ; calibré par balayage pression vs survie
        /// (voir Logs/v1_018_sweep.log).
        /// </summary>
        public const float DefaultArmyUpkeepRate = 0.00028f;

        /// <summary>Mutable pour mesures / calibration (lue hors Burst dans OnUpdate).</summary>
        public static float ArmyUpkeepRate = DefaultArmyUpkeepRate;

        /// <summary>Force navale plus faible en magnitude ; taux plus élevé pour contribuer au budget.</summary>
        public const float NavyUpkeepRate = 0.05f;

        /// <summary>
        /// Plafond d'entretien naval = Income × fraction (v1_016 : ∞ → 0.5).
        /// Un pays ne peut plus porter une flotte structurellement au-dessus de ses moyens
        /// (AUS/NAP : navy 1.35–3.15 pour income 0.5–2.5 mesurés v1_015).
        /// GARDÉ intact v1_018 (filet anti-spirale).
        /// </summary>
        public const float MaxNavyIncomeFraction = 0.5f;

        /// <summary>Forfait plat A/B (baseline dip_005) — inchangé pour le mode FlatBaseline.</summary>
        public const float BaseAdminCost = 0.5f;

        /// <summary>
        /// Coût admin / province (linéaire de base). eco_032 : 0.10.
        /// v1_018 : 0.10 → 0.45 — surextension fiscale (anti-blob) ; le filet anti-spirale
        /// (MaxCountryDebt / MaxInterestPerTick / navy) borne la casse. Calibré par balayage.
        /// </summary>
        public const float DefaultAdminCostPerProvince = 0.45f;

        /// <summary>Mutable pour mesures / calibration (lue hors Burst dans OnUpdate).</summary>
        public static float AdminCostPerProvince = DefaultAdminCostPerProvince;

        /// <summary>
        /// Terme surlinéaire : admin = rate × n × (1 + k × n). k=0 → plat ∝ n.
        /// v1_018 : 0 → 0.012 — chaque province supplémentaire coûte un peu plus (anti-blob net)
        /// sans tuer les empires moyens. Calibré par balayage.
        /// </summary>
        public const float DefaultAdminSuperlinearPerProvince = 0.012f;

        /// <summary>Mutable pour mesures / calibration.</summary>
        public static float AdminSuperlinearPerProvince = DefaultAdminSuperlinearPerProvince;

        /// <summary>Mutable pour mesures A/B (FlatBaseline = dip_005 exact).</summary>
        public static AdminCostMode CostMode = AdminCostMode.PerProvince;

        /// <summary>
        /// Coût administratif pour <paramref name="provinces"/> possédées.
        /// Formule : rate × n × (1 + k × n). Burst-safe, déterministe.
        /// </summary>
        public static float ComputeAdminCost(int provinces, float perProvince, float superlinear)
        {
            if (provinces <= 0)
            {
                return 0f;
            }

            float n = provinces;
            return perProvince * n * (1f + superlinear * n);
        }

        private ComponentLookup<TreasuryData> _treasuryLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<VictoriaGame.Core.WorldState>();
            _treasuryLookup = state.GetComponentLookup<TreasuryData>(true);
        }

        // Pas de [BurstCompile] sur OnUpdate : lecture des statics mutables hors Burst (BC1040).
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

            state.Dependency.Complete();
            _treasuryLookup.Update(ref state);

            var expensesByCountry = new NativeHashMap<Entity, float>(64, Allocator.TempJob);
            var provinceCountByCountry = new NativeHashMap<Entity, int>(64, Allocator.TempJob);

            // Comptage ENTIER par Owner — .Run() uniquement (déterministe, jamais parallel).
            var countJob = new ProvinceCountByOwnerJob
            {
                ProvinceCountByCountry = provinceCountByCountry
            };
            countJob.Run();

            // v1_016 P1 : pays sans terre → flotte démobilisée (plus d'entretien fantôme).
            var demobJob = new LandlessNavyDemobilizationJob
            {
                ProvinceCountByCountry = provinceCountByCountry
            };
            demobJob.Run();

            var armyJob = new ArmyUpkeepAccumulationJob
            {
                ExpensesByCountry = expensesByCountry,
                ArmyUpkeepRate = ArmyUpkeepRate
            };
            armyJob.Run();

            var navyJob = new NavyUpkeepAccumulationJob
            {
                ExpensesByCountry = expensesByCountry,
                ProvinceCountByCountry = provinceCountByCountry,
                TreasuryLookup = _treasuryLookup,
                NavyUpkeepRate = NavyUpkeepRate,
                MaxNavyIncomeFraction = MaxNavyIncomeFraction
            };
            navyJob.Run();

            var mode = CostMode;
            var adminPerProvince = AdminCostPerProvince;
            var adminSuperlinear = AdminSuperlinearPerProvince;
            var flatAdmin = BaseAdminCost;

            var updateJob = new TreasuryExpensesUpdateJob
            {
                ExpensesByCountry = expensesByCountry,
                ProvinceCountByCountry = provinceCountByCountry,
                Mode = mode,
                AdminCostPerProvince = adminPerProvince,
                AdminSuperlinearPerProvince = adminSuperlinear,
                FlatAdminCost = flatAdmin
            };
            updateJob.Run();

            expensesByCountry.Dispose();
            provinceCountByCountry.Dispose();
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstCompile]
        private partial struct ArmyUpkeepAccumulationJob : IJobEntity
        {
            public NativeHashMap<Entity, float> ExpensesByCountry;
            public float ArmyUpkeepRate;

            public void Execute(in ArmyData army)
            {
                if (army.Country == Entity.Null)
                {
                    return;
                }

                ExpensesByCountry.TryGetValue(army.Country, out float current);
                ExpensesByCountry[army.Country] = current + army.Strength * ArmyUpkeepRate;
            }
        }

        /// <summary>
        /// v1_016 : à la perte de la dernière province, flotte démobilisée
        /// (NavalStrength=0, escadrons vidés) — corrige GEN/FRA mesurés à revenu=0 + navy éternel.
        /// GARDÉ intact v1_018 (filet anti-spirale).
        /// </summary>
        [BurstCompile]
        private partial struct LandlessNavyDemobilizationJob : IJobEntity
        {
            [ReadOnly] public NativeHashMap<Entity, int> ProvinceCountByCountry;

            public void Execute(ref NavyData navy, DynamicBuffer<ShipSquadron> squadrons)
            {
                if (navy.Country == Entity.Null)
                {
                    return;
                }

                int provinces = ProvinceCountByCountry.TryGetValue(navy.Country, out int count)
                    ? count
                    : 0;
                if (provinces > 0)
                {
                    return;
                }

                if (navy.NavalStrength <= 0f && squadrons.Length == 0)
                {
                    return;
                }

                squadrons.Clear();
                navy.NavalStrength = 0f;
            }
        }

        [BurstCompile]
        private partial struct NavyUpkeepAccumulationJob : IJobEntity
        {
            public NativeHashMap<Entity, float> ExpensesByCountry;
            [ReadOnly] public NativeHashMap<Entity, int> ProvinceCountByCountry;
            [ReadOnly] public ComponentLookup<TreasuryData> TreasuryLookup;
            public float NavyUpkeepRate;
            public float MaxNavyIncomeFraction;

            public void Execute(in NavyData navy)
            {
                if (navy.Country == Entity.Null)
                {
                    return;
                }

                int provinces = ProvinceCountByCountry.TryGetValue(navy.Country, out int count)
                    ? count
                    : 0;
                if (provinces <= 0)
                {
                    return;
                }

                float raw = navy.NavalStrength * NavyUpkeepRate;
                float cap = raw;
                if (TreasuryLookup.HasComponent(navy.Country))
                {
                    float income = TreasuryLookup[navy.Country].Income;
                    cap = math.max(0f, income * MaxNavyIncomeFraction);
                }

                float upkeep = math.min(raw, cap);
                ExpensesByCountry.TryGetValue(navy.Country, out float current);
                ExpensesByCountry[navy.Country] = current + upkeep;
            }
        }

        [BurstCompile]
        private partial struct ProvinceCountByOwnerJob : IJobEntity
        {
            public NativeHashMap<Entity, int> ProvinceCountByCountry;

            public void Execute(in ProvinceOwnership ownership)
            {
                if (ownership.Owner == Entity.Null)
                {
                    return;
                }

                ProvinceCountByCountry.TryGetValue(ownership.Owner, out int current);
                ProvinceCountByCountry[ownership.Owner] = current + 1;
            }
        }

        [BurstCompile]
        private partial struct TreasuryExpensesUpdateJob : IJobEntity
        {
            [ReadOnly] public NativeHashMap<Entity, float> ExpensesByCountry;
            [ReadOnly] public NativeHashMap<Entity, int> ProvinceCountByCountry;
            public AdminCostMode Mode;
            public float AdminCostPerProvince;
            public float AdminSuperlinearPerProvince;
            public float FlatAdminCost;

            public void Execute(ref TreasuryData treasury, Entity country)
            {
                float militaryUpkeep = ExpensesByCountry.TryGetValue(country, out float upkeep) ? upkeep : 0f;
                float adminCost;
                if (Mode == AdminCostMode.FlatBaseline)
                {
                    adminCost = FlatAdminCost;
                }
                else
                {
                    int provinces = ProvinceCountByCountry.TryGetValue(country, out int count) ? count : 0;
                    // Formule inline (Burst) : rate × n × (1 + k × n) — même que ComputeAdminCost.
                    float n = provinces;
                    adminCost = AdminCostPerProvince * n * (1f + AdminSuperlinearPerProvince * n);
                }

                treasury.Expenses = militaryUpkeep + adminCost;
            }
        }
    }
}
