using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class SurfaceDiscoveryCoverageTests
    {
        [Test]
        public void UnknownAndPartialDiscoveryCannotProveEmpty()
        {
            var coverage = new SurfaceDiscoveryCoverage();
            var empty = new SurfaceLodNodeKey(1, int3.zero);
            Assert.False(coverage.IsKnownEmpty(empty));
            coverage.Begin(int3.zero);
            Assert.False(coverage.IsKnownEmpty(empty));
            coverage.Complete(int3.zero);
            Assert.True(coverage.IsKnownEmpty(empty));
            coverage.Invalidate(int3.zero);
            Assert.False(coverage.IsKnownEmpty(empty));
        }

        [Test]
        public void NegativeSurfaceBlocksInvalidateEveryContainingLodProof()
        {
            var coverage = new SurfaceDiscoveryCoverage();
            coverage.Begin(new int3(-1));
            coverage.AddSurfaceBlock(new int3(-1));
            coverage.Complete(new int3(-1));
            foreach (int step in new[] { 1, 2, 4, 8 })
                Assert.False(coverage.IsKnownEmpty(new SurfaceLodNodeKey(step, new int3(-1))));
            Assert.True(coverage.IsKnownEmpty(new SurfaceLodNodeKey(1, new int3(-2))));
            coverage.Forget(new int3(-1));
            Assert.False(coverage.IsKnownEmpty(new SurfaceLodNodeKey(1, new int3(-2))));
        }

        [Test]
        public void RediscoveryReplacesOldSurfaceEvidenceOnlyAtCompletion()
        {
            var coverage = new SurfaceDiscoveryCoverage();
            coverage.Begin(int3.zero);
            coverage.AddSurfaceBlock(int3.zero);
            coverage.Complete(int3.zero);
            var node = new SurfaceLodNodeKey(1, int3.zero);
            Assert.False(coverage.IsKnownEmpty(node));
            coverage.Begin(int3.zero);
            Assert.False(coverage.IsKnownEmpty(node));
            coverage.Complete(int3.zero);
            Assert.True(coverage.IsKnownEmpty(node));
        }

        [Test]
        public void CapacityFailureStaysUnknownAndEvictionReleasesCapacity()
        {
            var coverage = new SurfaceDiscoveryCoverage();
            for (int x = 0; x < SurfaceDiscoveryCoverage.MaximumRegions; x++)
                coverage.Begin(new int3(x, 0, 0));
            int3 next = new(SurfaceDiscoveryCoverage.MaximumRegions, 0, 0);
            coverage.Begin(next);
            coverage.Complete(next);
            Assert.False(coverage.IsComplete(next));
            Assert.AreEqual(SurfaceDiscoveryCoverage.MaximumRegions, coverage.Count);
            coverage.Forget(int3.zero);
            coverage.Begin(next);
            coverage.Complete(next);
            Assert.True(coverage.IsComplete(next));
        }
    }
}
