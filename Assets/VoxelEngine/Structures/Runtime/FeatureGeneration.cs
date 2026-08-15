using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;

using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>What a region's feature generation produced.</summary>
    public struct FeatureGenerationReport
    {
        public int InstancesConsidered;
        public int InstancesRasterised;
        public int PrimitivesEmitted;
        public int VoxelsWritten;

        /// <summary>Set when a batch exceeded <see cref="FeatureBudget.MaxPrimitivesPerRegion"/>.</summary>
        public bool BudgetExceeded;

        public EvaluationResult LastEvaluationResult;
    }

    /// <summary>
    /// Generates the feature content of one region.
    ///
    /// Everything below is a function of <c>(seed, catalogue, region coordinate)</c>. An instance is
    /// evaluated in full and clipped to the requested region, so generation order cannot change the
    /// authored result.
    /// </summary>
    public static class FeatureGeneration
    {
        /// <summary>
        /// Rasterises every explicit feature overlapping <paramref name="regionCoord"/>.
        ///
        /// Structures and Infrastructure carry hard-surface semantics into storage. Landforms remain
        /// untagged so terrain, roads, grading, and soil shoulders stay on the smooth renderer path.
        /// </summary>
        public static FeatureGenerationReport GenerateRegion(
            in FeatureCatalogue catalogue,
            uint seed,
            int3 regionCoord,
            IRegionReadSource reads,
            IRegionMutationStore mutations)
        {
            var report = new FeatureGenerationReport();

            if (!catalogue.IsCreated) return report;

            int regionEdge = VoxelGrid.RegionVoxelEdge;
            int3 regionMin = regionCoord * regionEdge;
            int3 regionMax = regionMin + regionEdge;

            var primitives = new NativeList<Primitive>(64, Allocator.Temp);
            var anchors = new NativeList<ResolvedAnchor>(8, Allocator.Temp);

            for (var ruleIndex = 0; ruleIndex < catalogue.Rules.Length; ruleIndex++)
            {
                var rule = catalogue.Rules[ruleIndex];

                if ((uint)rule.DefinitionId >= (uint)catalogue.DefinitionCount) continue;

                var definition = catalogue.Definitions[rule.DefinitionId];
                bool markHardSurface = definition.Kind == FeatureKind.Structure
                                    || definition.Kind == FeatureKind.Infrastructure;

                for (var e = 0; e < rule.ExplicitCount; e++)
                {
                    int index = rule.ExplicitOffset + e;
                    if ((uint)index >= (uint)catalogue.ExplicitPlacements.Length) continue;

                    var placement = catalogue.ExplicitPlacements[index];
                    report.InstancesConsidered++;

                    if (!FootprintIntersects(placement.Position, definition.Footprint, regionMin, regionMax))
                        continue;

                    primitives.Clear();
                    anchors.Clear();

                    var parameters = ResolveParameters(in catalogue, in definition, in placement,
                                                       rule.DefinitionId, placement.Position, seed);

                    ulong instanceSeed = InstanceSeed(seed, rule.DefinitionId, placement.Position);

                    var evaluation = ShapeProgram.Evaluate(
                        in catalogue, rule.DefinitionId, in parameters,
                        placement.Position, placement.Orientation,
                        seed, instanceSeed, primitives, anchors);

                    report.LastEvaluationResult = evaluation;

                    if (evaluation != EvaluationResult.Ok)
                    {
                        report.BudgetExceeded |= evaluation == EvaluationResult.PrimitiveLimitExceeded;
                        continue;
                    }

                    report.PrimitivesEmitted += primitives.Length;

                    var raster = PrimitiveRasteriser.Rasterise(
                        primitives.AsArray(), regionMin, regionMax,
                        reads, mutations, markHardSurface);

                    report.VoxelsWritten += raster.VoxelsWritten;
                    report.BudgetExceeded |= raster.BudgetExceeded;

                    if (raster.PrimitivesRasterised > 0) report.InstancesRasterised++;
                }
            }

            primitives.Dispose();
            anchors.Dispose();

            return report;
        }

        /// <summary>
        /// Parameter values for an instance: authored overrides where present, seeded draws
        /// otherwise.
        ///
        /// An override of -1 means "draw this one", so an author can pin the dimensions that matter
        /// to a landmark and let the rest vary.
        /// </summary>
        public static ParameterSet ResolveParameters(
            in FeatureCatalogue catalogue,
            in FeatureDefinition definition,
            in ExplicitPlacement placement,
            int definitionId,
            int3 position,
            uint seed)
        {
            var set = new ParameterSet();
            ulong state = InstanceSeed(seed, definitionId, position);

            for (var i = 0; i < definition.ParameterCount && i < ParameterSet.MaxParameters; i++)
            {
                var spec = catalogue.Parameters[definition.ParameterOffset + i];

                int value;
                int overrideIndex = placement.OverrideOffset + i;

                if (i < placement.OverrideCount &&
                    (uint)overrideIndex < (uint)catalogue.ParameterOverrides.Length &&
                    catalogue.ParameterOverrides[overrideIndex] >= 0)
                {
                    value = catalogue.ParameterOverrides[overrideIndex];
                }
                else
                {
                    value = FeatureHash.Range(ref state, spec.Min, spec.Max);
                }

                set[i] = spec.Clamp(value);
            }

            return set;
        }

        /// <summary>
        /// The seed for one instance's draws. Derived from position rather than allocation order.
        /// </summary>
        public static ulong InstanceSeed(uint seed, int definitionId, int3 position) =>
            FeatureHash.Cell(seed, definitionId, position);

        /// <summary>True when a footprint placed at <paramref name="origin"/> reaches into the volume.</summary>
        public static bool FootprintIntersects(int3 origin, int3 footprint, int3 volumeMin, int3 volumeMax)
        {
            int3 max = origin + footprint;

            return origin.x < volumeMax.x && max.x > volumeMin.x
                && origin.y < volumeMax.y && max.y > volumeMin.y
                && origin.z < volumeMax.z && max.z > volumeMin.z;
        }
    }
}