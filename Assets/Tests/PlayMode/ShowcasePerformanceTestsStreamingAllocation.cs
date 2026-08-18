using System;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Uses the ShowcasePerformanceTests prefix so the existing isolated showcase performance
    /// shard exercises the world-streaming bookkeeping without requiring a separate CI process.
    /// </summary>
    public sealed class ShowcasePerformanceTestsStreamingAllocation
    {
        [Test]
        public void WarmWantedSetRefreshDoesNotAllocatePerStep()
        {
            // A zero streaming budget still rebuilds and orders the wanted set but cannot begin
            // terrain generation. That isolates the recurring queue bookkeeping from region jobs,
            // feature authoring, rendering, and scene lifetime noise.
            using var world = new ShowcaseWorld(
                seed: 0x5EED1234u,
                brickPoolCapacity: 64,
                loadRadiusRegions: 8,
                unloadRadiusRegions: 11);
            var camera = new float3(13.25f, 28.0f, -7.75f);

            // Warm the terrain-span cache and the List/HashSet capacities before measuring the
            // steady-state path. HashSet.Clear retains its buckets, which is the production intent.
            for (int i = 0; i < 16; i++)
                world.StepStreaming(camera, 0.0);

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 256; i++)
                world.StepStreaming(camera, 0.0);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.LessOrEqual(allocated, 1024L,
                $"Refreshing and sorting the warmed showcase wanted set allocated {allocated:N0} "
              + "managed bytes over 256 calls. This path runs every streaming frame; duplicate "
              + "membership and load ordering must reuse their storage rather than allocate "
              + "capturing delegates or per-refresh collections.");
        }
    }
}
