using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class SurfaceLodCoverageStateTests
    {
        [Test]
        public void InvalidationKeepsOldDrawableProofButDesiredGenerationIsIncomplete()
        {
            var state = new SurfaceLodCoverageState();
            var key = new SurfaceLodNodeKey(2, new int3(-3, 4, 7));

            state.SetDesiredGeneration(key, 10);
            Assert.IsTrue(state.TryPublishCompletion(
                key, 10, SurfaceLodCompletionKind.Ready));

            state.SetDesiredGeneration(key, 11);
            SurfaceLodNodeState node = state.GetOrDefault(key);

            Assert.IsTrue(node.HasDrawableProof,
                "Previous geometry should remain available while its replacement is built.");
            Assert.IsTrue(node.IsDrawableGeometry);
            Assert.AreEqual(10UL, node.DrawableGeneration);
            Assert.AreEqual(11UL, node.DesiredGeneration);
            Assert.IsFalse(node.IsDesiredComplete,
                "Old geometry may cover temporarily but cannot authorize an atomic LOD switch.");
        }

        [Test]
        public void StaleAsyncCompletionCannotBecomeHierarchyComplete()
        {
            var state = new SurfaceLodCoverageState();
            var key = new SurfaceLodNodeKey(4, new int3(1, 2, 3));

            state.SetDesiredGeneration(key, 40);
            state.SetDesiredGeneration(key, 41);

            Assert.IsFalse(state.TryPublishCompletion(
                key, 40, SurfaceLodCompletionKind.Ready));
            Assert.IsFalse(state.IsDesiredComplete(key));
            Assert.IsTrue(state.TryPublishCompletion(
                key, 41, SurfaceLodCompletionKind.Ready));
            Assert.IsTrue(state.IsDesiredComplete(key));
        }

        [Test]
        public void KnownEmptyCountsAsCompleteForCurrentGeneration()
        {
            var state = new SurfaceLodCoverageState();
            var key = new SurfaceLodNodeKey(1, new int3(-1, 0, 1));

            state.SetDesiredGeneration(key, 5);
            Assert.IsTrue(state.TryPublishCompletion(
                key, 5, SurfaceLodCompletionKind.KnownEmpty));

            SurfaceLodNodeState node = state.GetOrDefault(key);
            Assert.IsTrue(node.IsKnownEmpty);
            Assert.IsTrue(node.IsDesiredComplete);
            Assert.IsFalse(node.IsDrawableGeometry);
        }

        [Test]
        public void ParentCannotRefineUntilAllEightChildrenAreComplete()
        {
            var state = new SurfaceLodCoverageState();
            var parent = new SurfaceLodNodeKey(4, new int3(-2, 3, -5));

            for (int childIndex = 0;
                 childIndex < SurfaceLodHierarchy.ChildrenPerParent;
                 childIndex++)
            {
                var child = new SurfaceLodNodeKey(
                    2,
                    SurfaceLodHierarchy.ChildCoordinate(parent.Coordinate, childIndex));
                state.SetDesiredGeneration(child, (ulong)(100 + childIndex));

                if (childIndex < SurfaceLodHierarchy.ChildrenPerParent - 1)
                {
                    SurfaceLodCompletionKind kind = childIndex % 2 == 0
                        ? SurfaceLodCompletionKind.Ready
                        : SurfaceLodCompletionKind.KnownEmpty;
                    Assert.IsTrue(state.TryPublishCompletion(
                        child, (ulong)(100 + childIndex), kind));
                }
            }

            Assert.IsFalse(state.AreChildrenDesiredComplete(parent));

            int finalIndex = SurfaceLodHierarchy.ChildrenPerParent - 1;
            var finalChild = new SurfaceLodNodeKey(
                2,
                SurfaceLodHierarchy.ChildCoordinate(parent.Coordinate, finalIndex));
            Assert.IsTrue(state.TryPublishCompletion(
                finalChild, (ulong)(100 + finalIndex), SurfaceLodCompletionKind.Ready));

            Assert.IsTrue(state.AreChildrenDesiredComplete(parent));
        }

        [Test]
        public void ChildInvalidationRevokesAtomicRefinementWithoutDiscardingOldDrawable()
        {
            var state = new SurfaceLodCoverageState();
            var parent = new SurfaceLodNodeKey(8, new int3(0, 0, 0));

            for (int childIndex = 0;
                 childIndex < SurfaceLodHierarchy.ChildrenPerParent;
                 childIndex++)
            {
                var child = new SurfaceLodNodeKey(
                    4,
                    SurfaceLodHierarchy.ChildCoordinate(parent.Coordinate, childIndex));
                state.SetDesiredGeneration(child, 1);
                Assert.IsTrue(state.TryPublishCompletion(
                    child, 1, SurfaceLodCompletionKind.Ready));
            }
            Assert.IsTrue(state.AreChildrenDesiredComplete(parent));

            var invalidatedChild = new SurfaceLodNodeKey(
                4,
                SurfaceLodHierarchy.ChildCoordinate(parent.Coordinate, 3));
            state.SetDesiredGeneration(invalidatedChild, 2);

            Assert.IsFalse(state.AreChildrenDesiredComplete(parent));
            SurfaceLodNodeState childState = state.GetOrDefault(invalidatedChild);
            Assert.IsTrue(childState.HasDrawableProof);
            Assert.AreEqual(1UL, childState.DrawableGeneration);
            Assert.AreEqual(2UL, childState.DesiredGeneration);
        }

        [Test]
        public void DesiredGenerationCannotMoveBackward()
        {
            var state = new SurfaceLodCoverageState();
            var key = new SurfaceLodNodeKey(1, new int3(0, 0, 0));
            state.SetDesiredGeneration(key, 9);

            Assert.Throws<System.InvalidOperationException>(() =>
                state.SetDesiredGeneration(key, 8));
        }
    }
}
