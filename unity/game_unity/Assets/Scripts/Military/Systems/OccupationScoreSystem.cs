using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using VictoriaGame.Core;
using VictoriaGame.World;

namespace VictoriaGame.Military
{
    /// <summary>
    /// Accumule du WarScore chaque tick selon l'occupation nette prolongée :
    /// provinces du défenseur tenues par l'attaquant poussent le score vers +
    /// (avantage attaquant), provinces de l'attaquant tenues par le défenseur vers −.
    /// Ordonnancement : après sièges / avances de front, avant PeaceSystem.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(SiegeSystem))]
    [UpdateAfter(typeof(FrontAdvanceSystem))]
    [UpdateBefore(typeof(PeaceSystem))]
    public partial struct OccupationScoreSystem : ISystem
    {
        /// <summary>
        /// Score / tick / province d'occupation nette (pondération plate).
        /// 0.5 → 1 province atteint ~60 en ~120 ticks (avant épuisement 150).
        /// v1_014 (monde CountryId) : balayage 0/0.5/0.8/1.1/1.4/1.7/2.0 —
        /// genou = 0.5 (ratioV@800=58.7%, debt=450.4, bankrupt=3) ; au-delà
        /// +décisivité coûte +dette (600+) puis +banqueroutes (≥4) — non rentable.
        /// </summary>
        public const float DefaultOccupationScoreRate = 0.5f;

        /// <summary>Mutable pour mesures / calibration (lue hors Burst dans OnUpdate).</summary>
        public static float OccupationScoreRate = DefaultOccupationScoreRate;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WorldState>();
        }

        // Pas de [BurstCompile] sur OnUpdate : static mutable + EntityManager (BC1040).
        [BurstDiscard]
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

            var rate = OccupationScoreRate;
            if (rate == 0f)
            {
                return;
            }

            // Agrégation en .Run() — JAMAIS ScheduleParallel (accumulation partagée).
            foreach (var warRef in SystemAPI.Query<RefRW<WarData>>())
            {
                var war = warRef.ValueRO;
                if (!war.IsActive)
                {
                    continue;
                }

                var attacker = war.Attacker;
                var defender = war.Defender;
                if (attacker == Entity.Null || defender == Entity.Null)
                {
                    continue;
                }

                // Occupations nettes en entiers (déterministe, indépendant de l'ordre des chunks).
                var attackerOccupied = 0;
                var defenderOccupied = 0;

                foreach (var ownership in SystemAPI.Query<RefRO<ProvinceOwnership>>())
                {
                    var owner = ownership.ValueRO.Owner;
                    var controller = ownership.ValueRO.Controller;
                    if (owner == Entity.Null || controller == Entity.Null || owner == controller)
                    {
                        continue;
                    }

                    if (owner == defender && controller == attacker)
                    {
                        attackerOccupied++;
                    }
                    else if (owner == attacker && controller == defender)
                    {
                        defenderOccupied++;
                    }
                }

                var netOccupation = attackerOccupied - defenderOccupied;
                if (netOccupation == 0)
                {
                    continue;
                }

                // Convention : WarScore > 0 = avantage attaquant (FrontAdvance / Siege / WarData).
                war.WarScore = math.clamp(war.WarScore + netOccupation * rate, -100f, 100f);
                warRef.ValueRW = war;
            }
        }

        public void OnDestroy(ref SystemState state)
        {
        }
    }
}
