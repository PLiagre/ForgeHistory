using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using VictoriaGame.Core;

namespace VictoriaGame.Tests
{
    /// <summary>
    /// v1_012 — preuve que le pacing lit DeltaTime × SimulationSpeed et respecte la pause.
    /// Sans TickControl : 1 tick/update (inchangé pour le harnais).
    /// </summary>
    public class V1012TickPacingTests
    {
        const float SecondsPerTick = 1f;

        [Test]
        public void Without_TickControl_OneTickPerUpdate_Unchanged()
        {
            using var harness = new SimulationHarness(42195u);
            harness.RunTicks(0);
            Assert.IsFalse(HasTickControl(harness.EntityManager));

            var sim = harness.World.GetExistingSystemManaged<SimulationSystemGroup>();
            for (var i = 0; i < 7; i++)
                sim.Update();

            Assert.AreEqual(7, GetTick(harness.EntityManager));
        }

        [Test]
        public void With_TickControl_SyntheticDeltaTime_Advances_T_Over_SecondsPerTick()
        {
            using var harness = new SimulationHarness(42195u);
            harness.RunTicks(0);
            AddTickControl(harness.EntityManager, SecondsPerTick);

            var ws = GetWorldState(harness.EntityManager);
            ws.SimulationSpeed = 1f;
            ws.IsPaused = false;
            SetWorldState(harness.EntityManager, ws);

            // T = 3 s à 1 tick/s → 3 ticks (1 update par seconde synthétique).
            const float T = 3f;
            AdvanceWithDelta(harness.World, SecondsPerTick, (int)(T / SecondsPerTick));

            Assert.AreEqual(
                (int)(T / SecondsPerTick), GetTick(harness.EntityManager),
                "À vitesse 1, T secondes → T/SecondsPerTick ticks.");
        }

        [Test]
        public void With_TickControl_Paused_Advances_Zero()
        {
            using var harness = new SimulationHarness(42195u);
            harness.RunTicks(0);
            AddTickControl(harness.EntityManager, SecondsPerTick);

            var ws = GetWorldState(harness.EntityManager);
            ws.SimulationSpeed = 1f;
            ws.IsPaused = true;
            SetWorldState(harness.EntityManager, ws);

            AdvanceWithDelta(harness.World, SecondsPerTick, 5);
            Assert.AreEqual(0, GetTick(harness.EntityManager), "IsPaused → 0 tick.");
        }

        [Test]
        public void With_TickControl_SpeedTimesTwo_Doubles_TickRate()
        {
            using var harness = new SimulationHarness(42195u);
            harness.RunTicks(0);
            AddTickControl(harness.EntityManager, SecondsPerTick);

            var ws = GetWorldState(harness.EntityManager);
            ws.SimulationSpeed = 2f;
            ws.IsPaused = false;
            SetWorldState(harness.EntityManager, ws);

            // 3 updates × dt=1s × speed=2 → accumulateur +2 par update → 2 ticks/update → 6 ticks.
            // (max 1 tick/update) → 3 ticks en 3 updates si dt*speed >= SecondsPerTick chaque fois.
            // dt=1, speed=2 → +2 accum, SecondsPerTick=1 → 1 tick/update, reste 1 → next update
            // also 1 tick. Over 3 updates: 3 ticks.
            // Pour prouver le double : comparer vitesse 1 vs 2 sur le MÊME budget de temps total
            // en updates plus fins.

            // Reset via nouveau harnais pour vitesse 1
            int ticksAtSpeed1;
            using (var h1 = new SimulationHarness(42195u))
            {
                h1.RunTicks(0);
                AddTickControl(h1.EntityManager, SecondsPerTick);
                var w = GetWorldState(h1.EntityManager);
                w.SimulationSpeed = 1f;
                SetWorldState(h1.EntityManager, w);
                // 10 updates × dt=0.5 → temps total 5s ×1 → 5 ticks
                AdvanceWithDelta(h1.World, 0.5f, 10);
                ticksAtSpeed1 = GetTick(h1.EntityManager);
            }

            int ticksAtSpeed2;
            using (var h2 = new SimulationHarness(42195u))
            {
                h2.RunTicks(0);
                AddTickControl(h2.EntityManager, SecondsPerTick);
                var w = GetWorldState(h2.EntityManager);
                w.SimulationSpeed = 2f;
                SetWorldState(h2.EntityManager, w);
                // 10 updates × dt=0.5 → temps total 5s ×2 = 10s effectifs → 10 ticks
                AdvanceWithDelta(h2.World, 0.5f, 10);
                ticksAtSpeed2 = GetTick(h2.EntityManager);
            }

            Assert.AreEqual(5, ticksAtSpeed1, "vitesse ×1 : 5s → 5 ticks");
            Assert.AreEqual(10, ticksAtSpeed2, "vitesse ×2 : même horloge → double de ticks");
            Assert.AreEqual(ticksAtSpeed1 * 2, ticksAtSpeed2);
        }

        [Test]
        public void SimulationSpeed_IsRead_When_TickControl_Present()
        {
            using var harness = new SimulationHarness(42195u);
            harness.RunTicks(0);
            AddTickControl(harness.EntityManager, SecondsPerTick);

            var ws = GetWorldState(harness.EntityManager);
            ws.SimulationSpeed = 0f; // vitesse nulle → aucun tick malgré du delta
            SetWorldState(harness.EntityManager, ws);

            AdvanceWithDelta(harness.World, SecondsPerTick, 4);
            Assert.AreEqual(0, GetTick(harness.EntityManager),
                "SimulationSpeed=0 doit bloquer l'avance (champ enfin consommé).");
        }

        static void AdvanceWithDelta(Unity.Entities.World world, float deltaTime, int updates)
        {
            var sim = world.GetExistingSystemManaged<SimulationSystemGroup>();
            double elapsed = 0;
            for (var i = 0; i < updates; i++)
            {
                elapsed += deltaTime;
                world.SetTime(new TimeData(elapsed, deltaTime));
                sim.Update();
            }
        }

        static void AddTickControl(EntityManager em, float secondsPerTick)
        {
            var e = em.CreateEntity();
            em.AddComponentData(e, new TickControl
            {
                SecondsPerTick = secondsPerTick,
                Accumulator = 0f,
            });
        }

        static bool HasTickControl(EntityManager em)
        {
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<TickControl>());
            return q.CalculateEntityCount() > 0;
        }

        static int GetTick(EntityManager em) => GetWorldState(em).CurrentTick;

        static WorldState GetWorldState(EntityManager em)
        {
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<WorldState>());
            return q.GetSingleton<WorldState>();
        }

        static void SetWorldState(EntityManager em, WorldState ws)
        {
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<WorldState>());
            em.SetComponentData(q.GetSingletonEntity(), ws);
        }
    }
}
