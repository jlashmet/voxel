using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Composition;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleBuildDependenciesTests
    {
        [Test]
        public void RequiredRegionsCoverFullSignedCastleBounds()
        {
            PlannedCastleBuild planned = StructuresComposition.PlanCastleBuild(
                new int3(256, 220, 376),
                197u,
                197u ^ 0x71A5u);

            CastleBuildBounds bounds = CastleBuildDependencies.ResolveBounds(in planned);
            int3[] regions = CastleBuildDependencies.RequiredRegions(in planned);
            int3 expectedMin = bounds.Min >> VoxelGrid.RegionVoxelEdgeLog2;
            int3 expectedMax = (bounds.MaxExclusive - 1) >> VoxelGrid.RegionVoxelEdgeLog2;

            long expectedCount =
                ((long)expectedMax.x - expectedMin.x + 1L) *
                ((long)expectedMax.y - expectedMin.y + 1L) *
                ((long)expectedMax.z - expectedMin.z + 1L);

            Assert.AreEqual(expectedCount, regions.Length);
            Assert.AreEqual(expectedMin, regions[0],
                "Castle dependency enumeration lost its minimum signed region.");
            Assert.AreEqual(expectedMax, regions[regions.Length - 1],
                "Castle dependency enumeration lost its maximum region.");
            Assert.Less(expectedMin.y, 0,
                "The planned castle should preload underground negative-Y regions.");
            Assert.GreaterOrEqual(expectedMax.y, 1,
                "The planned castle should preload upper-structure regions above Y=0.");

            int cursor = 0;
            for (int y = expectedMin.y; y <= expectedMax.y; y++)
            for (int z = expectedMin.z; z <= expectedMax.z; z++)
            for (int x = expectedMin.x; x <= expectedMax.x; x++)
                Assert.AreEqual(new int3(x, y, z), regions[cursor++]);
        }
    }
}
