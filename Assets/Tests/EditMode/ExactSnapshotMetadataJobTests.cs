using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction.Transvoxel;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class ExactSnapshotMetadataJobTests
    {
        [Test]
        public void StepFourSizedEightRegionMetadataChainCompletes()
        {
            const int cacheEdge = 34;
            const int cacheCount = cacheEdge * cacheEdge * cacheEdge;
            int3 cacheOrigin = new(48, 48, 48);
            var bricks = new NativeArray<TransvoxelDensityBrick>(
                cacheCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var flags = new NativeArray<byte>(
                cacheCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var mixed = new NativeList<int>(cacheCount, Allocator.TempJob);
            var encodedRegion = new NativeArray<int>(
                VoxelReadGrid.BlocksPerRegion, Allocator.TempJob,
                NativeArrayOptions.UninitializedMemory);
            try
            {
                // -2 is uniform material 1 in Storage's stable API encoding.
                for (int i = 0; i < encodedRegion.Length; i++) encodedRegion[i] = -2;

                JobHandle dependency = new ExactBrickMetadataClearJob
                {
                    Bricks = bricks,
                    MixedFlags = flags,
                }.Schedule(cacheCount, 256);

                int edge = VoxelReadGrid.BlocksPerRegionEdge;
                int3 cacheMaxExclusive = cacheOrigin + cacheEdge;
                int3 minRegion = cacheOrigin >> VoxelReadGrid.BlocksPerRegionEdgeLog2;
                int3 maxRegion = (cacheMaxExclusive - 1)
                               >> VoxelReadGrid.BlocksPerRegionEdgeLog2;
                int scheduledRegions = 0;
                for (int rz = minRegion.z; rz <= maxRegion.z; rz++)
                for (int ry = minRegion.y; ry <= maxRegion.y; ry++)
                for (int rx = minRegion.x; rx <= maxRegion.x; rx++)
                {
                    int3 regionCoord = new(rx, ry, rz);
                    int3 regionMin = regionCoord * edge;
                    int3 intersectionMin = math.max(cacheOrigin, regionMin);
                    int3 intersectionMax = math.min(cacheMaxExclusive, regionMin + edge);
                    int3 size = intersectionMax - intersectionMin;
                    int volume = size.x * size.y * size.z;
                    if (volume <= 0) continue;
                    scheduledRegions++;
                    dependency = new ExactBrickMetadataRegionJob
                    {
                        EncodedBlockRefs = encodedRegion,
                        RegionCoord = regionCoord,
                        IntersectionMinWorldBlock = intersectionMin,
                        IntersectionSize = size,
                        CacheOrigin = cacheOrigin,
                        BrickCacheEdge = cacheEdge,
                        Bricks = bricks,
                        MixedFlags = flags,
                    }.Schedule(volume, 128, dependency);
                }

                JobHandle final = new ExactMixedBrickCompactJob
                {
                    MixedFlags = flags,
                    MixedIndices = mixed,
                }.Schedule(dependency);
                JobHandle.ScheduleBatchedJobs();
                final.Complete();

                Assert.AreEqual(8, scheduledRegions,
                    "Fixture must reproduce the 2x2x2 region overlap of a boundary-crossing step-4 cache.");
                Assert.AreEqual(0, mixed.Length);
                Assert.AreEqual(1, bricks[0].Kind);
                Assert.AreEqual(1, bricks[cacheCount - 1].Kind);
                Assert.AreEqual(1, bricks[0].UniformMaterial);
                Assert.AreEqual(1, bricks[cacheCount - 1].UniformMaterial);
            }
            finally
            {
                if (encodedRegion.IsCreated) encodedRegion.Dispose();
                if (mixed.IsCreated) mixed.Dispose();
                if (flags.IsCreated) flags.Dispose();
                if (bricks.IsCreated) bricks.Dispose();
            }
        }
    }
}
