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
            var build = new FeatureRegionBuild(regionCoord);
            while (!build.Step(in catalogue, seed, reads, mutations, int.MaxValue)) { }
            return build.Report;
        }

        /// <summary>
        /// Rasterises one placement into the region. Shared by the whole-region entry point and
        /// the resumable build so a sliced region cannot diverge from an unsliced one.
        /// </summary>
        internal static void RasteriseInstance(
            in FeatureCatalogue catalogue,
            uint seed,
            int definitionId,
            in FeatureDefinition definition,
            in ExplicitPlacement placement,
            int3 regionMin,
            int3 regionMax,
            IRegionReadSource reads,
            IRegionMutationStore mutations,
            NativeList<Primitive> primitives,
            NativeList<ResolvedAnchor> anchors,
            ref FeatureGenerationReport report)
        {
            bool markHardSurface = definition.Kind == FeatureKind.Structure
                                || definition.Kind == FeatureKind.Infrastructure;

            primitives.Clear();
            anchors.Clear();

            var parameters = ResolveParameters(in catalogue, in definition, in placement,
                                               definitionId, placement.Position, seed);

            ulong instanceSeed = InstanceSeed(seed, definitionId, placement.Position);

            var evaluation = ShapeProgram.Evaluate(
                in catalogue, definitionId, in parameters,
                placement.Position, placement.Orientation,
                seed, instanceSeed, primitives, anchors);

            if (evaluation == EvaluationResult.Ok
                && !PrimitivesWithinDeclaredFootprint(
                    primitives.AsArray(), in definition,
                    placement.Position, placement.Orientation))
            {
                evaluation = EvaluationResult.OutsideFootprint;
            }

            report.LastEvaluationResult = evaluation;

            if (evaluation != EvaluationResult.Ok)
            {
                report.BudgetExceeded |= evaluation == EvaluationResult.PrimitiveLimitExceeded;
                return;
            }

            report.PrimitivesEmitted += primitives.Length;

            var raster = PrimitiveRasteriser.Rasterise(
                primitives.AsArray(), regionMin, regionMax,
                reads, mutations, markHardSurface);

            report.VoxelsWritten += raster.VoxelsWritten;
            report.BudgetExceeded |= raster.BudgetExceeded;

            if (raster.PrimitivesRasterised > 0) report.InstancesRasterised++;
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
        /// Runtime backstop for the authoring-time footprint proof. An invalid catalogue must fail
        /// closed before rasterisation instead of clipping escaped geometry into whichever region
        /// happens to request it. Primitive bounds are inclusive; the declared footprint is the
        /// half-open volume [origin, origin + orientedFootprint).
        /// </summary>
        private static bool PrimitivesWithinDeclaredFootprint(
            NativeArray<Primitive> primitives,
            in FeatureDefinition definition,
            int3 origin,
            byte orientation)
        {
            int3 footprint = definition.Footprint;
            if ((orientation & 1) != 0)
                footprint = new int3(footprint.z, footprint.y, footprint.x);

            int3 maxExclusive = origin + footprint;

            for (var i = 0; i < primitives.Length; i++)
            {
                primitives[i].Bounds(out int3 min, out int3 max);
                if (min.x < origin.x || min.y < origin.y || min.z < origin.z
                    || max.x >= maxExclusive.x || max.y >= maxExclusive.y || max.z >= maxExclusive.z)
                    return false;
            }

            return true;
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