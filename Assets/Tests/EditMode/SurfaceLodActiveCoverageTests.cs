using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class SurfaceLodActiveCoverageTests
    {
        [Test]
        public void RefineKeepsReadyParentUntilAllEightChildrenAreCurrentComplete()
        {
            var state = new SurfaceLodCoverageState();
            var active = new SurfaceLodActiveCoverage();
            var parent = new SurfaceLodNodeKey(4, new int3(-2, 3, -5));

            Complete(state, parent, 1, SurfaceLodCompletionKind.Ready);
            Assert.True(active.TryActivateCompleteNode(parent, state));

            for (int childIndex = 0; childIndex < SurfaceLodHierarchy.ChildrenPerParent; childIndex++)
            {
                var child = new SurfaceLodNodeKey(
                    2, SurfaceLodHierarchy.ChildCoordinate(parent.Coordinate, childIndex));
                state.SetDesiredGeneration(child, 10UL + (ulong)childIndex);
                if (childIndex < SurfaceLodHierarchy.ChildrenPerParent - 1)
                    Assert.True(state.TryPublishCompletion(
                        child, 10UL + (ulong)childIndex,
                        childIndex % 2 == 0
                            ? SurfaceLodCompletionKind.Ready
                            : SurfaceLodCompletionKind.KnownEmpty));
            }

            Assert.False(active.TryRefine(parent, state),
                "Partial finer coverage must never remove the drawable coarse parent.");
            Assert.True(active.IsActive(parent));
            Assert.AreEqual(1, active.Count);

            int last = SurfaceLodHierarchy.ChildrenPerParent - 1;
            var finalChild = new SurfaceLodNodeKey(
                2, SurfaceLodHierarchy.ChildCoordinate(parent.Coordinate, last));
            Assert.True(state.TryPublishCompletion(
                finalChild, 10UL + (ulong)last, SurfaceLodCompletionKind.Ready));

            Assert.True(active.TryRefine(parent, state));
            Assert.False(active.IsActive(parent));
            Assert.AreEqual(8, active.Count,
                "Atomic refinement must replace one parent with all eight complete children.");
        }

        [Test]
        public void StaleDrawableParentDoesNotRetireChildrenUntilReplacementParentIsCurrent()
        {
            var state = new SurfaceLodCoverageState();
            var active = new SurfaceLodActiveCoverage();
            var parent = new SurfaceLodNodeKey(4, new int3(1, -1, 2));

            Complete(state, parent, 1, SurfaceLodCompletionKind.Ready);
            Assert.True(active.TryActivateCompleteNode(parent, state));
            CompleteChildren(state, parent, 20);
            Assert.True(active.TryRefine(parent, state));
            Assert.AreEqual(8, active.Count);

            state.SetDesiredGeneration(parent, 2);
            Assert.False(active.TryMerge(parent, state),
                "An older drawable parent is not proof for the current generation.");
            Assert.AreEqual(8, active.Count);

            Assert.True(state.TryPublishCompletion(parent, 2, SurfaceLodCompletionKind.Ready));
            Assert.True(active.TryMerge(parent, state));
            Assert.AreEqual(1, active.Count);
            Assert.True(active.IsActive(parent));
        }

        [Test]
        public void NegativeCoordinatesRoundTripThroughParentChildHierarchy()
        {
            var parent = new int3(-3, 2, -5);
            for (int i = 0; i < SurfaceLodHierarchy.ChildrenPerParent; i++)
            {
                int3 child = SurfaceLodHierarchy.ChildCoordinate(parent, i);
                Assert.AreEqual(parent, SurfaceLodHierarchy.ParentCoordinate(child));
                Assert.AreEqual(i, SurfaceLodHierarchy.ChildIndexWithinParent(child));
            }
        }

        [Test]
        public void VisibleDrawOwnershipKeepsCoarseParentAcrossPartialFinerOverlap()
        {
            var ownership = new SurfaceLodVisibleOwnership();
            var parent = new SurfaceLodNodeKey(4, new int3(-2, 3, -5));
            var child = new SurfaceLodNodeKey(
                2, SurfaceLodHierarchy.ChildCoordinate(parent.Coordinate, 6));

            ownership.Add(parent);
            ownership.Add(child);

            Assert.True(ownership.ShouldDraw(parent));
            Assert.False(ownership.ShouldDraw(child),
                "A visible coarse fallback must own the overlap instead of being double-drawn with a finer descendant.");
        }

        [Test]
        public void VisibleDrawOwnershipHandsOffAfterCoarseParentLeavesVisibleSet()
        {
            var ownership = new SurfaceLodVisibleOwnership();
            var parent = new SurfaceLodNodeKey(4, new int3(1, -2, 3));

            for (int childIndex = 0; childIndex < SurfaceLodHierarchy.ChildrenPerParent; childIndex++)
            {
                ownership.Add(new SurfaceLodNodeKey(
                    2, SurfaceLodHierarchy.ChildCoordinate(parent.Coordinate, childIndex)));
            }

            Assert.False(ownership.ShouldDraw(parent));
            for (int childIndex = 0; childIndex < SurfaceLodHierarchy.ChildrenPerParent; childIndex++)
            {
                var child = new SurfaceLodNodeKey(
                    2, SurfaceLodHierarchy.ChildCoordinate(parent.Coordinate, childIndex));
                Assert.True(ownership.ShouldDraw(child));
            }
        }

        [Test]
        public void VisibleDrawOwnershipRetainedAncestorSuppressesNestedDescendants()
        {
            var ownership = new SurfaceLodVisibleOwnership();
            var coarse = new SurfaceLodNodeKey(8, new int3(-1, 0, 2));
            var middle = new SurfaceLodNodeKey(
                4, SurfaceLodHierarchy.ChildCoordinate(coarse.Coordinate, 3));
            var fine = new SurfaceLodNodeKey(
                2, SurfaceLodHierarchy.ChildCoordinate(middle.Coordinate, 5));

            ownership.Add(coarse);
            ownership.Add(middle);
            ownership.Add(fine);

            Assert.True(ownership.ShouldDraw(coarse));
            Assert.False(ownership.ShouldDraw(middle));
            Assert.False(ownership.ShouldDraw(fine));
        }

        private static void Complete(SurfaceLodCoverageState state, SurfaceLodNodeKey key,
                                     ulong generation, SurfaceLodCompletionKind kind)
        {
            state.SetDesiredGeneration(key, generation);
            Assert.True(state.TryPublishCompletion(key, generation, kind));
        }

        private static void CompleteChildren(SurfaceLodCoverageState state,
                                             SurfaceLodNodeKey parent, ulong firstGeneration)
        {
            Assert.True(SurfaceLodHierarchy.TryGetChildSourceStep(parent.SourceStep, out int childStep));
            for (int i = 0; i < SurfaceLodHierarchy.ChildrenPerParent; i++)
            {
                var child = new SurfaceLodNodeKey(
                    childStep, SurfaceLodHierarchy.ChildCoordinate(parent.Coordinate, i));
                Complete(state, child, firstGeneration + (ulong)i,
                    i % 3 == 0 ? SurfaceLodCompletionKind.KnownEmpty : SurfaceLodCompletionKind.Ready);
            }
        }
    }
}
