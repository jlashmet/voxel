using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>Compatibility facade for the historical castle construction API.</summary>
    public static class CastleBuilder
    {
        public struct IncrementalBuild
        {
            internal CastleBuildPipeline Pipeline;

            public bool IsCreated => Pipeline != null;
            public bool IsComplete => Pipeline != null && Pipeline.IsComplete;
            public int StageNumber => Pipeline != null ? Pipeline.StageNumber : 0;
            public long TotalVoxelsWritten => Pipeline != null ? Pipeline.TotalVoxelsWritten : 0L;
        }

        public static CastlePlan Plan(int3 centre, uint seed) =>
            CastlePlanner.Create(centre, seed);

        public static long EstimateWrites(in CastlePlan plan) =>
            CastleBuildPreflight.EstimateWrites(in plan);

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

        public static bool StepBuild(ref IncrementalBuild build)
        {
            if (!build.IsCreated || build.IsComplete)
                return true;

            return build.Pipeline.Step();
        }
    }
}
