using System;
using System.Linq;
using Unity.Entities;
using VictoriaGame.Core;
using VictoriaGame.Economy;
using VictoriaGame.Military;
using VictoriaGame.Politics;
using VictoriaGame.Population;

namespace VictoriaGame.Tests
{
    /// <summary>
    /// Harnais headless pour exécuter la simulation DOTS en isolation (EditMode / batchmode).
    /// </summary>
    public sealed class SimulationHarness : IDisposable
    {
        const string ProjectAssemblyName = "VictoriaGame";

        readonly global::Unity.Entities.World _world;
        bool _disposed;

        public EntityManager EntityManager => _world.EntityManager;
        public global::Unity.Entities.World World => _world;

        public SimulationHarness(uint? seedOverride = null, bool installNoOpProbe = false)
        {
            if (seedOverride.HasValue)
                WorldBootstrapConfig.GlobalSeedOverride = seedOverride.Value;

            // Parité v1_009 : empêcher tax_physical_withdrawal.json (cAbs adopté)
            // de polluer OnCreate — sans écraser un LockCoefficients du test.
            TaxPhysicalWithdrawalSystem.EnsureParitySafeDefaults();
            StabilitySystem.EnsureParitySafeDefaults();
            TemplateRecruitSystem.EnsureParitySafeDefaults();

            _world = new global::Unity.Entities.World("TestWorld");
            InstallProjectSystems();

            if (installNoOpProbe)
                InstallNoOpProbe();
        }

        void InstallProjectSystems()
        {
            var projectSystems = DefaultWorldInitialization
                .GetAllSystems(WorldSystemFilterFlags.Default)
                .Where(systemType => systemType.Assembly.GetName().Name == ProjectAssemblyName)
                .ToList();

            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(_world, projectSystems);
        }

        /// <summary>
        /// Ajoute <see cref="NoOpProbeSystem"/> au SimulationSystemGroup (test dip_008 / v1_010).
        /// Le système est une entité ECS de plus : avant v1_010, cela décalait Entity.Index.
        /// </summary>
        public void InstallNoOpProbe()
        {
            var handle = _world.GetOrCreateSystem<NoOpProbeSystem>();
            var sim = _world.GetExistingSystemManaged<SimulationSystemGroup>();
            sim.AddSystemToUpdateList(handle);
            sim.SortSystems();
        }

        /// <summary>
        /// Exécute InitializationSystemGroup une fois, puis SimulationSystemGroup <paramref name="count"/> fois.
        /// </summary>
        public void RunTicks(int count)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count), "Le nombre de ticks doit être >= 0.");

            _world.GetExistingSystemManaged<InitializationSystemGroup>().Update();

            var simulationGroup = _world.GetExistingSystemManaged<SimulationSystemGroup>();
            for (var i = 0; i < count; i++)
                simulationGroup.Update();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            if (_world.IsCreated)
                _world.Dispose();

            WorldBootstrapConfig.ClearOverride();
            // Remet les statics mutables des couches physiques / démographie
            // pour éviter la pollution inter-tests (Lock* dans un harnais).
            // ResetToCompiledDefault (pas Unlock) : Unlock relirait le JSON adopté.
            TaxPhysicalWithdrawalSystem.ResetToCompiledDefault();
            PhysicalProductionSystem.UnlockOutletCap();
            PhysicalProductionSystem.ResetToCompiledDefault();
            BuildingConstructionSystem.UnlockCapacityIntensity();
            BuildingConstructionSystem.ResetToCompiledDefault();
            BuildingAiPolicyConfig.Unlock();
            BuildingAiPolicyConfig.ResetToCompiledDefault();
            PopGrowthSystem.ResetToCompiledDefault();
            StabilitySystem.ResetToCompiledDefault();
            TemplateRecruitSystem.ResetStabilityRecruitToCompiledDefault();
            _disposed = true;
        }
    }
}
