using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Compatibility facade for the historical castle construction API.
    ///
    /// Castle planning now belongs to <see cref="CastlePlanner"/>, preflight policy belongs to
    /// <see cref="CastleBuildPreflight"/>, and voxel realization belongs to
    /// <see cref="CastleBuildPipeline"/>. Keep this surface only while existing callers migrate.
    /// </summary>
    public static class CastleBuilder
    {
        /// <summary>
        /// Compatibility wrapper for callers that still use the old incremental build handle.
        /// Copies intentionally reference the same pipeline, matching the shared backing storage
        /// semantics of the previous capability-based build state.
        /// </summary>
        public struct IncrementalBuild
        {
            internal CastleBuildPipeline Pipeline;

            public bool IsCreated => Pipeline != null;
            public bool IsComplete => Pipeline != null && Pipeline.IsComplete;
            public int StageNumber => Pipeline != null ? Pipeline.StageNumber : 0;
            public long TotalVoxelsWritten => Pipeline != null ? Pipeline.TotalVoxelsWritten : 0L;
        }

        /// <summary>Compatibility entry point. New code should call <see cref="CastlePlanner.Create"/>.</summary>
        public static CastlePlan Plan(int3 centre, uint seed) =>
            CastlePlanner.Create(centre, seed);

        /// <summary>
        /// Compatibility estimate. New code should call <see cref="CastleBuildPreflight.EstimateWrites"/>.
        /// </summary>
        public static long EstimateWrites(in CastlePlan plan) =>
            CastleBuildPreflight.EstimateWrites(in plan);

        /// <summary>Builds the planned castle to completion through the incremental pipeline.</summary>
        public static VoxelBrush Build(
            IRegionReadSource reads,
            IRegionMutationStore mutations,
            in CastlePlan plan,
            uint terrainSeed,
            IMaterialAuthoringCatalogue materials)
        {
            IncrementalBuild build = BeginBuild(
                reads, mutations, in plan, terrainSeed, materials);
            while (!build.IsComplete)
                StepBuild(ref build);

            return build.Pipeline.Brush;
        }

        /// <summary>Starts a compatibility build backed by <see cref="CastleBuildPipeline"/>.</summary>
        public static IncrementalBuild BeginBuild(
            IRegionReadSource reads,
            IRegionMutationStore mutations,
            in CastlePlan plan,
            uint terrainSeed,
            IMaterialAuthoringCatalogue materials) =>
            new IncrementalBuild
            {
                Pipeline = new CastleBuildPipeline(
                    reads, mutations, in plan, terrainSeed, materials),
            };

        /// <summary>Executes one bounded unit of the compatibility build.</summary>
        public static bool StepBuild(ref IncrementalBuild build)
        {
            // Preserve the old default/completed-handle behavior: stepping either is a no-op
            // that reports completion rather than throwing.
            if (!build.IsCreated || build.IsComplete)
                return true;

            return build.Pipeline.Step();
        }
    }
}
