using System;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Composition
{
    /// <summary>
    /// Composition-owned feature-generation result. Runtime-only evaluator details stay behind
    /// this boundary while the Showcase world consumes the counters it needs for diagnostics.
    /// </summary>
    internal readonly struct FeatureGenerationReport
    {
        public readonly int InstancesRasterised;
        public readonly int VoxelsWritten;
        public readonly bool BudgetExceeded;

        public FeatureGenerationReport(int instancesRasterised, int voxelsWritten, bool budgetExceeded)
        {
            InstancesRasterised = instancesRasterised;
            VoxelsWritten = voxelsWritten;
            BudgetExceeded = budgetExceeded;
        }
    }

    /// <summary>
    /// Composition bridge for Structures.Runtime feature evaluation. Concrete generation and
    /// rasterisation remain implementation details of Structures.Runtime.
    /// </summary>
    internal static class FeatureGeneration
    {
        public static FeatureGenerationReport GenerateRegion(
            in FeatureCatalogue catalogue,
            uint seed,
            int3 regionCoord,
            IRegionReadSource reads,
            IRegionMutationStore mutations)
        {
            VoxelEngine.Structures.Runtime.FeatureGenerationReport report =
                VoxelEngine.Structures.Runtime.FeatureGeneration.GenerateRegion(
                    in catalogue, seed, regionCoord, reads, mutations);

            return new FeatureGenerationReport(
                report.InstancesRasterised,
                report.VoxelsWritten,
                report.BudgetExceeded);
        }
    }

    /// <summary>
    /// Composition bridge for a region's feature generation spread across frames. See
    /// <see cref="VoxelEngine.Structures.Runtime.FeatureRegionBuild"/> for why it is sliced.
    /// </summary>
    internal sealed class FeatureRegionBuild : IDisposable
    {
        private readonly VoxelEngine.Structures.Runtime.FeatureRegionBuild _build;

        public FeatureRegionBuild(int3 regionCoord) =>
            _build = new VoxelEngine.Structures.Runtime.FeatureRegionBuild(regionCoord);

        public int3 RegionCoord => _build.RegionCoord;
        public bool IsComplete => _build.IsComplete;

        public FeatureGenerationReport Report => new(
            _build.Report.InstancesRasterised,
            _build.Report.VoxelsWritten,
            _build.Report.BudgetExceeded);

        public bool Step(in FeatureCatalogue catalogue, uint seed,
                         IRegionReadSource reads, IRegionMutationStore mutations,
                         int maxTiles) =>
            _build.Step(in catalogue, seed, reads, mutations, maxTiles);

        public void Dispose() => _build.Dispose();
    }
}
