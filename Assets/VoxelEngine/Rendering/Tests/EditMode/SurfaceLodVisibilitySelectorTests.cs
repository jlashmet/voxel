using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class SurfaceLodVisibilitySelectorTests
    {
        [Test]
        public void PartialChildrenKeepDrawableParentUntilAllEightAreCurrentComplete()
        {
            var selector = new SurfaceLodVisibilitySelector();
            var parent = new SurfaceLodNodeKey(4, new int3(-2, 1, 3));
            var drawable = new List<SurfaceLodNodeKey> { parent };
            var current = new List<SurfaceLodNodeKey> { parent };

            Assert.True(SurfaceLodHierarchy.TryGetChildSourceStep(parent.SourceStep,
                                                                  out int childStep));
            for (int i = 0; i < SurfaceLodHierarchy.ChildrenPerParent - 1; i++)
            {
                var child = new SurfaceLodNodeKey(
                    childStep, SurfaceLodHierarchy.ChildCoordinate(parent.Coordinate, i));
                drawable.Add(child);
                current.Add(child);
            }

            selector.Rebuild(drawable, current);
            Assert.True(selector.IsActive(parent));
            Assert.True(selector.IsLogicallyActive(parent));
            Assert.AreEqual(1, selector.DrawCount,
                "Partial finer coverage must keep the fallback coarse parent drawable.");

            int last = SurfaceLodHierarchy.ChildrenPerParent - 1;
            var knownEmptyChild = new SurfaceLodNodeKey(
                childStep, SurfaceLodHierarchy.ChildCoordinate(parent.Coordinate, last));
            current.Add(knownEmptyChild); // logical completion with no drawable geometry

            selector.Rebuild(drawable, current);
            Assert.False(selector.IsLogicallyActive(parent));
            Assert.False(selector.IsActive(parent),
                "Physical finer replacements exist, so the coarse parent can retire.");
            Assert.AreEqual(8, selector.Count,
                "Known-empty coverage still participates in the logical atomic handoff.");
            Assert.AreEqual(7, selector.DrawCount,
                "Proof-only empty coverage is not emitted as geometry.");
            for (int i = 0; i < SurfaceLodHierarchy.ChildrenPerParent - 1; i++)
            {
                var child = new SurfaceLodNodeKey(
                    childStep, SurfaceLodHierarchy.ChildCoordinate(parent.Coordinate, i));
                Assert.True(selector.IsActive(child));
            }
            Assert.True(selector.IsLogicallyActive(knownEmptyChild));
            Assert.False(selector.IsActive(knownEmptyChild));
        }

        [Test]
        public void ProofOnlyReplacementKeepsCoarsePhysicalFallback()
        {
            var selector = new SurfaceLodVisibilitySelector();
            var parent = new SurfaceLodNodeKey(4, new int3(1, 2, -2));
            var current = new List<SurfaceLodNodeKey>();

            Assert.True(SurfaceLodHierarchy.TryGetChildSourceStep(parent.SourceStep,
                                                                  out int childStep));
            for (int i = 0; i < SurfaceLodHierarchy.ChildrenPerParent; i++)
            {
                current.Add(new SurfaceLodNodeKey(
                    childStep, SurfaceLodHierarchy.ChildCoordinate(parent.Coordinate, i)));
            }

            selector.Rebuild(new List<SurfaceLodNodeKey> { parent }, current);

            Assert.False(selector.IsLogicallyActive(parent),
                "The logical selector may prove the child coverage complete.");
            Assert.True(selector.IsActive(parent),
                "Logical proof without any physical replacement must not erase drawable fallback.");
            Assert.AreEqual(1, selector.DrawCount);
        }

        [Test]
        public void StaleDrawableParentRemainsFallbackUntilCurrentChildrenComplete()
        {
            var selector = new SurfaceLodVisibilitySelector();
            var parent = new SurfaceLodNodeKey(4, new int3(2, -1, -3));
            var drawable = new List<SurfaceLodNodeKey> { parent };
            var current = new List<SurfaceLodNodeKey>();

            Assert.True(SurfaceLodHierarchy.TryGetChildSourceStep(parent.SourceStep,
                                                                  out int childStep));
            for (int i = 0; i < SurfaceLodHierarchy.ChildrenPerParent - 1; i++)
            {
                var child = new SurfaceLodNodeKey(
                    childStep, SurfaceLodHierarchy.ChildCoordinate(parent.Coordinate, i));
                drawable.Add(child);
                current.Add(child);
            }

            selector.Rebuild(drawable, current);
            Assert.True(selector.IsActive(parent),
                "A stale-but-drawable parent is the fallback while replacement coverage is partial.");

            var finalChild = new SurfaceLodNodeKey(
                childStep, SurfaceLodHierarchy.ChildCoordinate(
                    parent.Coordinate, SurfaceLodHierarchy.ChildrenPerParent - 1));
            current.Add(finalChild);
            selector.Rebuild(drawable, current);

            Assert.False(selector.IsActive(parent));
            Assert.AreEqual(8, selector.Count);
            Assert.AreEqual(7, selector.DrawCount);
        }

        [Test]
        public void KnownEmptyAncestorDoesNotSuppressDrawableChildWithoutFallbackGeometry()
        {
            var selector = new SurfaceLodVisibilitySelector();
            var parent = new SurfaceLodNodeKey(4, new int3(0, 0, 0));
            Assert.True(SurfaceLodHierarchy.TryGetChildSourceStep(parent.SourceStep,
                                                                  out int childStep));
            var child = new SurfaceLodNodeKey(
                childStep, SurfaceLodHierarchy.ChildCoordinate(parent.Coordinate, 0));

            selector.Rebuild(
                new List<SurfaceLodNodeKey> { child },
                new List<SurfaceLodNodeKey> { parent, child });

            Assert.True(selector.IsActive(child));
            Assert.AreEqual(1, selector.DrawCount,
                "Logical empty completion is proof, not drawable fallback coverage.");
        }

        [TestCase(false, false, false, false, false, TestName = "OutOfBandDoesNotCompleteViewHandoff")]
        [TestCase(true, false, false, false, true, TestName = "OffFrustumInBandChildIsViewComplete")]
        [TestCase(true, true, false, false, false, TestName = "MissingVisibleChildKeepsFallback")]
        [TestCase(true, true, true, false, true, TestName = "CurrentReadyVisibleChildCompletesHandoff")]
        [TestCase(true, true, false, true, true, TestName = "CurrentEmptyVisibleChildCompletesHandoff")]
        public void CurrentViewCompletionRequiresRingOwnershipAndVisibleProof(
            bool inBand, bool inFrustum, bool currentReady, bool currentEmpty, bool expected)
        {
            Assert.AreEqual(expected,
                SurfaceLodVisibilitySelector.IsCurrentViewComplete(
                    inBand, inFrustum, currentReady, currentEmpty));
        }
    }
}
