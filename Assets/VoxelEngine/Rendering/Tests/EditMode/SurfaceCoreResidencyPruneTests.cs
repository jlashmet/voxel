using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;
using VoxelEngine.Storage.Api;
using VoxelEngine.Storage.Runtime;

namespace VoxelEngine.Tests.EditMode
{
    /// <summary>
    /// Regression for exact-snapshot chunks whose extraction halo reaches resident Storage while
    /// their authoritative owned core does not. Such a chunk can never satisfy the required-core
    /// metadata contract, so render-residency pruning must not keep it alive solely for the halo.
    /// </summary>
    public sealed class SurfaceCoreResidencyPruneTests
    {
        [Test]
        public void HaloResidencyDoesNotKeepStep4ChunkWithMissingOwnedCore()
        {
            var table = new RegionTable(4, Allocator.Persistent);
            var pool = new BrickPool(16, Allocator.Persistent);
            var journal = new VoxelChangeJournal();
            try
            {
                // Only region zero is logically resident. A step-4 chunk spans 256 voxels, so
                // chunk (-1,0,0) owns x=[-256,0) in region -1 while its four-voxel extraction
                // halo reaches into the resident region at x=0.
                table.LoadRegion(int3.zero);
                var source = new RegionReadSource(in table, in pool, journal);
                Assert.True(source.IsRegionResident(int3.zero));
                Assert.False(source.IsRegionResident(new int3(-1, 0, 0)));
                Assert.False(source.TryPinRegionBlockRefs(
                    new int3(-1, 0, 0), out PinnedRegionBlockRefs missingCorePin));
                Assert.False(missingCorePin.IsCreated);

                using var cache = new CpuTransvoxelChunkCache(sourceStep: 4);
                cache.SetClipmapWindow(int3.zero, radius: 2);

                // Mutation invalidation is intentionally border-aware. Brick x=0 admits both the
                // owning chunk 0 and halo neighbour -1, reproducing the production liveness case
                // without relying on camera timing or surface-discovery order.
                cache.InvalidateSurfaceBricks(new[] { new int3(0, 16, 16) });
                Assert.AreEqual(2, cache.KnownCount,
                    "Fixture must admit the resident owner and its halo-only neighbour.");

                MethodInfo prune = typeof(CpuTransvoxelChunkCache).GetMethod(
                    "StepResidencyPrune", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(prune);

                // The prune queue is bounded and round-robin. Two passes are enough for this
                // two-record fixture; use four so queue-order changes cannot make the gate flaky.
                for (int i = 0; i < 4; i++)
                    prune.Invoke(cache, new object[] { source });

                Assert.AreEqual(1, cache.KnownCount,
                    "A chunk whose owned core is nonresident cannot ever complete an exact "
                  + "snapshot. Keeping it alive because only its halo overlaps resident Storage "
                  + "creates a permanent required-core pin retry.");
            }
            finally
            {
                pool.Dispose();
                table.Dispose();
            }
        }
    }
}
