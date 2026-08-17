using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class SurfaceLodTransitionMaskTests
    {
        [Test]
        public void CoarseFaceTransitionsOnlyWhenAdjacentRegionHasActiveDescendants()
        {
            var coverage = new SurfaceLodActiveCoverage();
            var state = new SurfaceLodCoverageState();
            var coarse = CompleteAndActivate(coverage, state,
                new SurfaceLodNodeKey(4, int3.zero));

            var neighbour = new SurfaceLodNodeKey(4, new int3(1, 0, 0));
            ActivateAllChildren(coverage, state, neighbour);

            byte mask = SurfaceLodTransitionMask.Compute(coarse, coverage);
            Assert.AreEqual(1 << 1, mask, "+X must be the only active transition face.");
        }

        [Test]
        public void EqualResolutionNeighbourDoesNotEnableTransition()
        {
            var coverage = new SurfaceLodActiveCoverage();
            var state = new SurfaceLodCoverageState();
            var coarse = CompleteAndActivate(coverage, state,
                new SurfaceLodNodeKey(4, int3.zero));
            CompleteAndActivate(coverage, state,
                new SurfaceLodNodeKey(4, new int3(1, 0, 0)));

            Assert.AreEqual(0, SurfaceLodTransitionMask.Compute(coarse, coverage));
        }

        [Test]
        public void FinestLodNeverOwnsTransitionFace()
        {
            var coverage = new SurfaceLodActiveCoverage();
            var state = new SurfaceLodCoverageState();
            var fine = CompleteAndActivate(coverage, state,
                new SurfaceLodNodeKey(1, new int3(-2, 0, 0)));

            Assert.AreEqual(0, SurfaceLodTransitionMask.Compute(fine, coverage));
        }

        [Test]
        public void NegativeCoordinatesUseSameFaceConvention()
        {
            var coverage = new SurfaceLodActiveCoverage();
            var state = new SurfaceLodCoverageState();
            var coarse = CompleteAndActivate(coverage, state,
                new SurfaceLodNodeKey(8, new int3(-3, -2, -1)));

            var neighbour = new SurfaceLodNodeKey(8, new int3(-4, -2, -1));
            ActivateAllChildren(coverage, state, neighbour);

            byte mask = SurfaceLodTransitionMask.Compute(coarse, coverage);
            Assert.AreEqual(1 << 0, mask, "-X must be enabled across negative coordinates.");
        }

        [Test]
        public void MultipleRefinedNeighboursProduceIndependentFaceBits()
        {
            var coverage = new SurfaceLodActiveCoverage();
            var state = new SurfaceLodCoverageState();
            var coarse = CompleteAndActivate(coverage, state,
                new SurfaceLodNodeKey(4, new int3(2, 3, 4)));
            ActivateAllChildren(coverage, state,
                new SurfaceLodNodeKey(4, new int3(3, 3, 4))); // +X
            ActivateAllChildren(coverage, state,
                new SurfaceLodNodeKey(4, new int3(2, 3, 3))); // -Z

            byte mask = SurfaceLodTransitionMask.Compute(coarse, coverage);
            Assert.AreEqual((1 << 1) | (1 << 4), mask);
        }

        private static SurfaceLodNodeKey CompleteAndActivate(
            SurfaceLodActiveCoverage coverage, SurfaceLodCoverageState state,
            SurfaceLodNodeKey key)
        {
            Complete(state, key);
            Assert.True(coverage.TryActivateCompleteNode(key, state),
                $"Failed to activate {key}.");
            return key;
        }

        private static void ActivateAllChildren(SurfaceLodActiveCoverage coverage,
                                                SurfaceLodCoverageState state,
                                                SurfaceLodNodeKey parent)
        {
            Assert.True(SurfaceLodHierarchy.TryGetChildSourceStep(
                parent.SourceStep, out int childStep));
            for (int i = 0; i < SurfaceLodHierarchy.ChildrenPerParent; i++)
            {
                var child = new SurfaceLodNodeKey(childStep,
                    SurfaceLodHierarchy.ChildCoordinate(parent.Coordinate, i));
                CompleteAndActivate(coverage, state, child);
            }
        }

        private static void Complete(SurfaceLodCoverageState state, SurfaceLodNodeKey key)
        {
            state.SetDesiredGeneration(key, 1);
            Assert.True(state.TryPublishCompletion(key, 1, SurfaceLodCompletionKind.Ready));
        }
    }
}
