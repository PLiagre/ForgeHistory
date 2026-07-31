using Unity.Entities;
using Unity.Collections;
using NUnit.Framework;
using VictoriaGame.Core;
using VictoriaGame.World;
using VictoriaGame.Population;
using VictoriaGame.Economy;
using VictoriaGame.Navy;

namespace VictoriaGame.Tests
{
    [TestFixture]
    public class BootstrapTests
    {
        const int ExpectedProvinceCount = 50;
        const int ExpectedCountryCount = 20;
        const int ExpectedSeaZoneCount = 14;

        static WorldState GetWorldState(EntityManager em)
        {
            using var query = em.CreateEntityQuery(typeof(WorldState));
            Assert.AreEqual(1, query.CalculateEntityCount(),
                "Le singleton WorldState doit exister exactement une fois après l'initialisation.");
            return query.GetSingleton<WorldState>();
        }

        static int CountEntities<T>(EntityManager em) where T : unmanaged, IComponentData
        {
            using var query = em.CreateEntityQuery(typeof(T));
            return query.CalculateEntityCount();
        }

        static void AssertAllProvincesHaveNeighbors(EntityManager em)
        {
            using var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<ProvinceData>(),
                ComponentType.ReadOnly<ProvinceNeighbor>());

            using var entities = query.ToEntityArray(Allocator.Temp);
            Assert.AreEqual(ExpectedProvinceCount, entities.Length,
                $"Attendu {ExpectedProvinceCount} provinces après MapInitSystem, trouvé {entities.Length}.");

            foreach (var entity in entities)
            {
                var province = em.GetComponentData<ProvinceData>(entity);
                var neighbors = em.GetBuffer<ProvinceNeighbor>(entity);
                Assert.Greater(neighbors.Length, 0,
                    $"La province {province.ProvinceId} n'a aucun voisin — une province isolée serait injouable.");
            }
        }

        static void AssertAllSeaZonesHaveNeighbors(EntityManager em)
        {
            using var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<SeaZoneData>(),
                ComponentType.ReadOnly<SeaZoneNeighbor>());

            using var entities = query.ToEntityArray(Allocator.Temp);
            Assert.AreEqual(ExpectedSeaZoneCount, entities.Length,
                $"Attendu {ExpectedSeaZoneCount} zones maritimes après SeaZoneInitSystem, trouvé {entities.Length}.");

            foreach (var entity in entities)
            {
                var zone = em.GetComponentData<SeaZoneData>(entity);
                var neighbors = em.GetBuffer<SeaZoneNeighbor>(entity);
                Assert.Greater(neighbors.Length, 0,
                    $"La zone maritime {zone.ZoneId} n'a aucun voisin.");
            }
        }

        [Test]
        public void Bootstrap_WorldState_InitializedBeforeFirstTick()
        {
            using var harness = new SimulationHarness();
            harness.RunTicks(0);

            var ws = GetWorldState(harness.EntityManager);
            Assert.AreEqual(0, ws.CurrentTick, "CurrentTick doit être 0 avant le premier tick de simulation.");
            Assert.AreEqual(1400, ws.Year, "L'année de départ doit être 1400.");
            Assert.AreNotEqual(0u, ws.GlobalSeed, "GlobalSeed ne doit pas être nulle après WorldBootstrapSystem.");
        }

        [Test]
        public void Bootstrap_MapEntities_CreatedAfterInit()
        {
            using var harness = new SimulationHarness();
            harness.RunTicks(0);

            var em = harness.EntityManager;

            Assert.AreEqual(ExpectedProvinceCount, CountEntities<ProvinceData>(em),
                $"Attendu {ExpectedProvinceCount} entités ProvinceData depuis provinces.json.");
            AssertAllProvincesHaveNeighbors(em);

            Assert.AreEqual(ExpectedCountryCount, CountEntities<CountryData>(em),
                $"Attendu {ExpectedCountryCount} entités CountryData depuis countries.json.");

            var popCount = CountEntities<PopData>(em);
            Assert.Greater(popCount, 0,
                "PopInitSystem doit créer au moins une entité PopData.");

            var goodCount = CountEntities<GoodData>(em);
            Assert.Greater(goodCount, 0,
                "GoodInitSystem doit créer au moins une entité GoodData.");

            AssertAllSeaZonesHaveNeighbors(em);
        }

        [Test]
        public void Time_AfterOneTick_CurrentTickAdvances()
        {
            using var harness = new SimulationHarness();
            harness.RunTicks(1);

            var ws = GetWorldState(harness.EntityManager);
            Assert.AreEqual(1, ws.CurrentTick, "Après 1 tick, CurrentTick doit valoir 1.");
        }

        [Test]
        public void Time_AfterTwelveTicks_YearAdvancesTo1401()
        {
            using var harness = new SimulationHarness();
            harness.RunTicks(12);

            var ws = GetWorldState(harness.EntityManager);
            Assert.AreEqual(1401, ws.Year,
                "Après 12 ticks (12 mois), l'année doit passer de 1400 à 1401.");
        }

        [Test]
        public void Time_After120Ticks_YearIs1410AndMonthIsValid()
        {
            using var harness = new SimulationHarness();
            harness.RunTicks(120);

            var ws = GetWorldState(harness.EntityManager);
            Assert.AreEqual(1410, ws.Year,
                "Après 120 ticks (10 années), l'année doit valoir 1410.");
            Assert.GreaterOrEqual(ws.Month, 1, "Le mois doit rester >= 1.");
            Assert.LessOrEqual(ws.Month, 12, "Le mois doit rester <= 12.");
        }

        [Test]
        public void Time_WhenPaused_TickDoesNotAdvance()
        {
            using var harness = new SimulationHarness();
            harness.RunTicks(0);

            var em = harness.EntityManager;
            using var query = em.CreateEntityQuery(typeof(WorldState));
            var entity = query.GetSingletonEntity();

            var ws = em.GetComponentData<WorldState>(entity);
            ws.IsPaused = true;
            em.SetComponentData(entity, ws);

            harness.RunTicks(5);

            ws = GetWorldState(em);
            Assert.AreEqual(0, ws.CurrentTick,
                "Avec IsPaused = true, CurrentTick ne doit pas avancer même après plusieurs ticks.");
        }

        [Test]
        public void Survival_100Ticks_NoException()
        {
            using var harness = new SimulationHarness();
            Assert.DoesNotThrow(() => harness.RunTicks(100),
                "100 ticks consécutifs ne doivent lever aucune exception — smoke test des 60+ systèmes.");

            var ws = GetWorldState(harness.EntityManager);
            Assert.AreEqual(100, ws.CurrentTick,
                "Après 100 ticks sans pause, CurrentTick doit valoir 100.");
        }
    }
}
