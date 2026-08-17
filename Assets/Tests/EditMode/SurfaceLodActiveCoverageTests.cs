using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class SurfaceLodActiveCoverageTests
    {
        [Test]
        public void RefineDoesNotRemoveParentUntilEveryChildIsComplete()
        {
            var state = new SurfaceLodCoverageState();
            var active = new SurfaceLodActiveCoverage();
            var parent = new SurfaceLodNodeKey(4, new int3(-2, 3, -5));

            state.SetDesiredGeneration(parent, 1);
            Assert.IsTrue(state.TryPublishCompletion(parent, 1, SurfaceLodCompletionKind.Ready));
            Assert.IsTrue(active.TryActivateCompleteNode(parent, state));

            for (int childIndex = 0;
                 childIndex < SurfaceLodHierarchy.ChildrenPerParent;
                 childIndex++)
            {
                var child = new SurfaceLodNodeKey(
                    2,
                    SurfaceLodHierarchy.ChildCoordinate(parent.Coordinate, childIndex));
                state.SetDesiredGeneration(child, 10UL + (ulong)childIndex);
                if (childIndex < SurfaceLodHierarchy.ChildrenPerParent - 1)
                {
                    Assert.IsTrue(state.TryPublishCompletion(
                        child,
                        10UL + (ulong)childIndex,
                        childIndex % 2 == 0
                            ? SurfaceLodCompletionKind.Ready
                            : SurfaceLodCompletionKind.KnownEmpty));
                }
            }

            Assert.IsFalse(active.TryRefine(parent, state));
            Assert.IsTrue(active.IsActive(parent));
            Assert.AreEqual(1, active.Count);

            int finalIndex = SurfaceLodHierarchy.ChildrenPerParent - 1;
            var finalChild = new SurfaceLodNodeKey(
                2,
                SurfaceLodHierarchy.ChildCoordinate(parent.Coordinate, finalIndex));
            Assert.IsTrue(state.TryPublishCompletion(
                finalChild,
                10UL + (ulong)finalIndex,
                SurfaceLodCompletionKind.Ready));

            Assert.IsTrue(active.TryRefine(parent, state));
            Assert.IsFalse(active.IsActive(parent));
            Assert.AreEqual(SurfaceLodHierarchy.ChildrenPerParent, active.Count);
        }

        [Test]
        public void KnownEmptyChildrenRemainLogicalActiveLeaves()
        {
            var state = new SurfaceLodCoverageState();
            var active = new SurfaceLodActiveCoverage();
            var parent = new SurfaceLodNodeKey(2, new int3(1, -2, 3));

            state.SetDesiredGeneration(parent, 1);
            Assert.IsTrue(state.TryPublishCompletion(parent, 1, SurfaceLodCompletionKind.Ready));
            Assert.IsTrue(active.TryActivateCompleteNode(parent, state));

            for (int childIndex = 0;
                 childIndex < SurfaceLodHierarchy.ChildrenPerParent;
                 childIndex++)
            {
                var child = new SurfaceLodNodeKey(
                    1,
                    SurfaceLodHierarchy.ChildCoordinate(parent.Coordinate, childIndex));
                state.SetDesiredGeneration(child, 2);
                Assert.IsTrue(state.TryPublishCompletion(
                    child,
                    2,
                    childIndex == 0
                        ? SurfaceLodCompletionKind.Ready
                        : SurfaceLodCompletionKind.KnownEmpty));
            }

            Assert.IsTrue(active.TryRefine(parent, state));
            Assert.AreEqual(8, active.Count,
                "Empty children still participate in complete logical coverage.");
        }

        [Test]
        public void MergeWaitsForCurrentParentGenerationThenReplacesAllDescendants()
        {
            var state = new SurfaceLodCoverageState();
            var active = new SurfaceLodActiveCoverage();
            var root = new SurfaceLodNodeKey(8, new int3(-1, 0, 1));

            Complete(state, root, 1, SurfaceLodCompletionKind.Ready);
            Assert.IsTrue(active.TryActivateCompleteNode(root, state));
            CompleteChildren(state, root, 10);
            Assert.IsTrue(active.TryRefine(root, state));

            var refinedChild = new SurfaceLodNodeKey(
                4,
                SurfaceLodHierarchy.ChildCoordinate(root.Coordinate, 3));
            CompleteChildren(state, refinedChild, 100);
            Assert.IsTrue(active.TryRefine(refinedChild, state));
            Assert.AreEqual(15, active.Count,
                "Seven step-4 siblings plus eight step-2 grandchildren should be active.");

            // Invalidate the root. Its previous generation remains drawable, but current children
            // must not be retired until the replacement root generation completes.
            state.SetDesiredGeneration(root, 2);
            Assert.IsFalse(active.TryMerge(root, state));
            Assert.AreEqual(15, active.Count);

            Assert.IsTrue(state.TryPublishCompletion(root, 2, SurfaceLodCompletionKind.Ready));
            Assert.IsTrue(active.TryMerge(root, state));
            Assert.AreEqual(1, active.Count);
            Assert.IsTrue(active.IsActive(root));
        }

        [Test]
        public void ActiveSetRejectsOverlappingSeedNodes()
        {
            var state = new SurfaceLodCoverageState();
            var active = new SurfaceLodActiveCoverage();
            var parent = new SurfaceLodNodeKey(4, new int3(-2, -2, -2));
            var child = new SurfaceLodNodeKey(
                2,
                SurfaceLodHierarchy.ChildCoordinate(parent.Coordinate, 5));

            Complete(state, parent, 1, SurfaceLodCompletionKind.Ready);
            Complete(state, child, 1, SurfaceLodCompletionKind.Ready);

            Assert.IsTrue(active.TryActivateCompleteNode(parent, state));
            Assert.IsFalse(active.TryActivateCompleteNode(child, state));
            Assert.AreEqual(1, active.Count);
        }

        [Test]
        public void ActiveNodesCanBeCopiedWithoutExposingMutableSet()
        {
            var state = new SurfaceLodCoverageState();
            var active = new SurfaceLodActiveCoverage();
            var node = new SurfaceLodNodeKey(8, new int3(2, 0, -2));
            Complete(state, node, 1, SurfaceLodCompletionKind.KnownEmpty);
            Assert.IsTrue(active.TryActivateCompleteNode(node, state));

            var destination = new List<SurfaceLodNodeKey>();
            Assert.AreEqual(1, active.CopyActiveTo(destination));
            Assert.AreEqual(node, destination[0]);
            Assert.AreEqual(1, active.Count);
        }

        private static void Complete(SurfaceLodCoverageState state,
                                     SurfaceLodNodeKey key,
                                     ulong generation,
                                     SurfaceLodCompletionKind kind)
        {
            state.SetDesiredGeneration(key, generation);
            Assert.IsTrue(state.TryPublishCompletion(key, generation, kind));
        }

        private static void CompleteChildren(SurfaceLodCoverageState state,
                                             SurfaceLodNodeKey parent,
                                             ulong firstGeneration)
        {
            Assert.IsTrue(SurfaceLodHierarchy.TryGetChildSourceStep(
                parent.SourceStep, out int childStep));
            for (int childIndex = 0;
                 childIndex < SurfaceLodHierarchy.ChildrenPerParent;
                 childIndex++)
            {
                var child = new SurfaceLodNodeKey(
                    childStep,
                    SurfaceLodHierarchy.ChildCoordinate(parent.Coordinate, childIndex));
                Complete(
                    state,
                    child,
                    firstGeneration + (ulong)childIndex,
                    childIndex % 3 == 0
                        ? SurfaceLodCompletionKind.KnownEmpty
                        : SurfaceLodCompletionKind.Ready);
            }
        }
    }
}
