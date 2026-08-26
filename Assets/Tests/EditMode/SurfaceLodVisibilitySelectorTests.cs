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
            Assert.AreEqual(1, selector.Count,
                "Partial finer coverage must not draw over the fallback coarse parent.");

            int last = SurfaceLodHierarchy.ChildrenPerParent - 1;
            var knownEmptyChild = new SurfaceLodNodeKey(
                childStep, SurfaceLodHierarchy.ChildCoordinate(parent.Coordinate, last));
            current.Add(knownEmptyChild); // logical completion with no drawable geometry

            selector.Rebuild(drawable, current);
            Assert.False(selector.IsActive(parent));
            Assert.AreEqual(8, selector.Count,
                "Known-empty coverage must complete the atomic parent-to-children handoff.");
            for (int i = 0; i < SurfaceLodHierarchy.ChildrenPerParent; i++)
            {
                var child = new SurfaceLodNodeKey(
                    childStep, SurfaceLodHierarchy.ChildCoordinate(parent.Coordinate, i));
                Assert.True(selector.IsActive(child));
            }
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
        }
    }
}
