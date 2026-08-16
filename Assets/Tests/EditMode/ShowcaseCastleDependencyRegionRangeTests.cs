using System;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Showcase;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class ShowcaseCastleDependencyRegionRangeTests
    {
        private static int RegionEdge => 1 << VoxelDimensions.RegionVoxelEdgeLog2;

        [Test]
        public void ExactExclusiveBoundaryDoesNotQueueFollowingRegion()
        {
            int edge = RegionEdge;
            ShowcaseCastleDependencyRegionRange range =
                ShowcaseCastleDependencyRegionRange.FromVoxelBounds(
                    int3.zero,
                    new int3(edge, edge, edge));

            Assert.AreEqual(int3.zero, range.Min);
            Assert.AreEqual(int3.zero, range.MaxInclusive);
        }

        [Test]
        public void NegativeExactBoundaryStaysInNegativeRegion()
        {
            int edge = RegionEdge;
            ShowcaseCastleDependencyRegionRange range =
                ShowcaseCastleDependencyRegionRange.FromVoxelBounds(
                    new int3(-edge, -edge, -edge),
                    int3.zero);

            Assert.AreEqual(new int3(-1, -1, -1), range.Min);
            Assert.AreEqual(new int3(-1, -1, -1), range.MaxInclusive);
        }

        [Test]
        public void SignedBoundsCrossNegativeAndPositiveRegionLayersByFloorDivision()
        {
            int edge = RegionEdge;
            ShowcaseCastleDependencyRegionRange range =
                ShowcaseCastleDependencyRegionRange.FromVoxelBounds(
                    new int3(-1, -edge - 1, -edge * 2),
                    new int3(edge, 0, -edge + 1));

            Assert.AreEqual(new int3(-1, -2, -2), range.Min);
            Assert.AreEqual(new int3(0, -1, -1), range.MaxInclusive);
        }

        [Test]
        public void EmptyVoxelBoundsAreRejected()
        {
            Assert.Throws<ArgumentException>(() =>
                ShowcaseCastleDependencyRegionRange.FromVoxelBounds(
                    new int3(10, 20, 30),
                    new int3(10, 21, 31)));
        }
    }
}
