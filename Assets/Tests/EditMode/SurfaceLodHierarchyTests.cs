using System;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class SurfaceLodHierarchyTests
    {
        [TestCase(1, 2)]
        [TestCase(2, 4)]
        [TestCase(4, 8)]
        public void AdjacentSourceStepsMapInBothDirections(int childStep, int parentStep)
        {
            Assert.IsTrue(SurfaceLodHierarchy.TryGetParentSourceStep(childStep, out int actualParent));
            Assert.AreEqual(parentStep, actualParent);
            Assert.IsTrue(SurfaceLodHierarchy.TryGetChildSourceStep(parentStep, out int actualChild));
            Assert.AreEqual(childStep, actualChild);
        }

        [Test]
        public void HierarchyEndsRejectFurtherParentOrChild()
        {
            Assert.IsFalse(SurfaceLodHierarchy.TryGetChildSourceStep(1, out _));
            Assert.IsFalse(SurfaceLodHierarchy.TryGetParentSourceStep(8, out _));
            Assert.IsFalse(SurfaceLodHierarchy.TryGetParentSourceStep(3, out _));
            Assert.IsFalse(SurfaceLodHierarchy.TryGetChildSourceStep(16, out _));
        }

        [TestCase(0, 0)]
        [TestCase(1, 0)]
        [TestCase(2, 1)]
        [TestCase(3, 1)]
        [TestCase(-1, -1)]
        [TestCase(-2, -1)]
        [TestCase(-3, -2)]
        [TestCase(-4, -2)]
        [TestCase(int.MinValue, -1073741824)]
        [TestCase(int.MaxValue, 1073741823)]
        public void ParentCoordinateUsesFloorDivisionForEverySign(int childX, int expectedParentX)
        {
            int3 parent = SurfaceLodHierarchy.ParentCoordinate(new int3(childX, childX, childX));
            Assert.AreEqual(new int3(expectedParentX, expectedParentX, expectedParentX), parent);
        }

        [Test]
        public void EveryChildMapsBackToItsParentAcrossNegativeCoordinates()
        {
            int3[] parents =
            {
                new int3(0, 0, 0),
                new int3(3, 7, 11),
                new int3(-1, -1, -1),
                new int3(-4, 2, -9),
                new int3(5, -6, 7),
            };

            foreach (int3 parent in parents)
            {
                for (int childIndex = 0; childIndex < SurfaceLodHierarchy.ChildrenPerParent; childIndex++)
                {
                    int3 child = SurfaceLodHierarchy.ChildCoordinate(parent, childIndex);
                    Assert.AreEqual(parent, SurfaceLodHierarchy.ParentCoordinate(child),
                        $"Child {childIndex} at {child} did not map back to parent {parent}.");
                    Assert.AreEqual(childIndex, SurfaceLodHierarchy.ChildIndexWithinParent(child),
                        $"Child index round-trip failed for parent {parent}.");
                }
            }
        }

        [Test]
        public void ChildOffsetsCoverExactTwoByTwoByTwoVolume()
        {
            int3 parent = new int3(-3, 5, -7);
            var seen = new bool[SurfaceLodHierarchy.ChildrenPerParent];

            for (int childIndex = 0; childIndex < SurfaceLodHierarchy.ChildrenPerParent; childIndex++)
            {
                int3 child = SurfaceLodHierarchy.ChildCoordinate(parent, childIndex);
                int3 offset = child - parent * 2;

                Assert.That(offset.x, Is.InRange(0, 1));
                Assert.That(offset.y, Is.InRange(0, 1));
                Assert.That(offset.z, Is.InRange(0, 1));
                int encoded = offset.x | (offset.y << 1) | (offset.z << 2);
                Assert.AreEqual(childIndex, encoded);
                Assert.IsFalse(seen[encoded]);
                seen[encoded] = true;
            }

            Assert.That(seen, Is.All.True);
        }

        [Test]
        public void StepValidatedOverloadsRejectNonAdjacentMappings()
        {
            Assert.Throws<ArgumentException>(() =>
                SurfaceLodHierarchy.ParentCoordinate(new int3(1, 2, 3), 1, 4));
            Assert.Throws<ArgumentException>(() =>
                SurfaceLodHierarchy.ChildCoordinate(new int3(1, 2, 3), 4, 1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SurfaceLodHierarchy.ChildCoordinate(new int3(1, 2, 3), 8));
        }
    }
}
