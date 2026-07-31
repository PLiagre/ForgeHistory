using NUnit.Framework;
using Unity.Entities;
using VictoriaGame.Presentation;

namespace VictoriaGame.Tests
{
    /// <summary>
    /// Garantie v1_007 : SimulationHarness n'installe aucun système de VictoriaGame.Presentation.
    /// </summary>
    public class PresentationIsolationTests
    {
        [Test]
        public void SimulationHarness_Excludes_Presentation_Assembly_Systems()
        {
            Assert.AreEqual(
                "VictoriaGame.Presentation",
                typeof(MapDisplaySystem).Assembly.GetName().Name);

            using var harness = new SimulationHarness(42195u);
            var world = harness.EntityManager.World;

            Assert.IsNull(
                world.GetExistingSystemManaged<MapDisplaySystem>(),
                "Le World du harnais ne doit contenir aucun MapDisplaySystem (assembly Presentation).");

            AssertNoPresentationSystems(world.GetExistingSystemManaged<InitializationSystemGroup>());
            AssertNoPresentationSystems(world.GetExistingSystemManaged<SimulationSystemGroup>());
            AssertNoPresentationSystems(world.GetExistingSystemManaged<PresentationSystemGroup>());
        }

        static void AssertNoPresentationSystems(ComponentSystemGroup group)
        {
            if (group == null)
                return;

            var systems = group.ManagedSystems;
            if (systems == null)
                return;

            for (var i = 0; i < systems.Count; i++)
            {
                var sys = systems[i];
                if (sys == null)
                    continue;
                var asm = sys.GetType().Assembly.GetName().Name;
                Assert.AreNotEqual(
                    "VictoriaGame.Presentation", asm,
                    "Système Presentation installé dans le harnais : " + sys.GetType().FullName);
            }
        }
    }
}
