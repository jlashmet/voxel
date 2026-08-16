using VoxelEngine.Simulation.Api;
using VoxelEngine.Simulation.Runtime;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Composition
{
    /// <summary>
    /// Composition-owned construction for authoritative world simulation. Production consumers
    /// depend on Simulation.Api; only Composition reaches into Simulation.Runtime.
    /// </summary>
    public static class SimulationComposition
    {
        public static IFireWaterSimulation CreateFireWater(
            IVoxelSurfaceQuery reads,
            IRegionMutationStore mutations,
            in MaterialSimulationView materials) =>
            new FireWaterSimulation(reads, mutations, in materials);

        public static IFireWaterSimulation CreateFireWater(
            IVoxelSurfaceQuery reads,
            IRegionMutationStore mutations,
            in MaterialSimulationView materials,
            FireWaterConfig config) =>
            new FireWaterSimulation(reads, mutations, in materials, config);
    }
}
