using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleBuildRegionDependenciesTests
    {
        [Test]
        public void ResolvedBoundsEnumerateEveryIntersectedRegionIncludingUpperLayers()
        {
            CastlePlan plan = CastlePlanner.Create(new int3(256, 220, 376), 211u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(211u);
            topology.KeepPlacement = CastleKeepPlacement.Central;
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);
            CastleBuildBounds bounds = CastleBuildBoundsResolver.Resolve(in plan, spatial);

            int3[] regions = CastleBuildRegionDependencies.Enumerate(in bounds);
            int3 expectedMin = bounds.Min >> VoxelGrid.RegionVoxelEdgeLog2;
            int3 expectedMax = (bounds.MaxExclusive - 1) >> VoxelGrid.RegionVoxelEdgeLog2;
            int expectedCount = (expectedMax.x - expectedMin.x + 1)
                              * (expectedMax.y - expectedMin.y + 1)
                              * (expectedMax.z - expectedMin.z + 1);

            Assert.AreEqual(expectedCount, regions.Length);
            CollectionAssert.Contains(regions, expectedMin);
            CollectionAssert.Contains(regions, expectedMax);
            Assert.Greater(expectedMax.y, expectedMin.y,
                "Castle dependency enumeration must include authored structure above terrain region y=0.");
        }

        [Test]
        public void EnumeratedRegionsContainBothInclusiveBoundsCorners()
        {
            CastlePlan plan = CastlePlanner.Create(new int3(512, 190, 512), 223u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(223u);
            topology.KeepPlacement = CastleKeepPlacement.Rear;
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);
            CastleBuildBounds bounds = CastleBuildBoundsResolver.Resolve(in plan, spatial);
            int3[] regions = CastleBuildRegionDependencies.Enumerate(in bounds);

            int3 minRegion = bounds.Min >> VoxelGrid.RegionVoxelEdgeLog2;
            int3 maxRegion = (bounds.MaxExclusive - 1) >> VoxelGrid.RegionVoxelEdgeLog2;
            bool foundMin = false;
            bool foundMax = false;
            for (int i = 0; i < regions.Length; i++)
            {
                foundMin |= math.all(regions[i] == minRegion);
                foundMax |= math.all(regions[i] == maxRegion);
            }

            Assert.IsTrue(foundMin);
            Assert.IsTrue(foundMax);
        }
    }
}
