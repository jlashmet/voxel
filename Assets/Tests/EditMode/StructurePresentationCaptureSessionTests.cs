using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class StructurePresentationCaptureSessionTests
    {
        [Test]
        public void UnrelatedSemanticProducersUseSameBoundedDeterministicCapture()
        {
            FeaturePresentationBake first = CaptureFortification();
            FeaturePresentationBake repeat = CaptureFortification();
            FeaturePresentationBake second = CaptureRoofedWalkway();

            Assert.That(first.SourceId, Is.EqualTo(repeat.SourceId));
            Assert.That(first.Revision, Is.EqualTo(repeat.Revision));
            Assert.That(first.BoundsMin, Is.EqualTo(repeat.BoundsMin));
            Assert.That(first.BoundsMax, Is.EqualTo(repeat.BoundsMax));
            Assert.That(first.PrimitiveCount, Is.EqualTo(repeat.PrimitiveCount));
            Assert.That(first.PrimitiveCount, Is.LessThanOrEqualTo(64));
            Assert.That(second.PrimitiveCount, Is.LessThanOrEqualTo(64));

            Assert.That(first.BoundsMin, Is.EqualTo(new int3(-8, 4, -8)));
            Assert.That(first.BoundsMax.x, Is.GreaterThanOrEqualTo(8));
            Assert.That(first.BoundsMax.y, Is.GreaterThanOrEqualTo(15));
            Assert.That(first.BoundsMax.z, Is.GreaterThanOrEqualTo(8));

            Assert.That(second.BoundsMin, Is.EqualTo(new int3(30, 6, -3)));
            Assert.That(second.BoundsMax.x, Is.GreaterThanOrEqualTo(49));
            Assert.That(second.BoundsMax.y, Is.GreaterThanOrEqualTo(17));
            Assert.That(second.BoundsMax.z, Is.GreaterThanOrEqualTo(6));
            Assert.That(second.Revision, Is.Not.EqualTo(first.Revision));
        }

        private static FeaturePresentationBake CaptureFortification()
        {
            var capture = new StructurePresentationCaptureSession();
            capture.Cylinder(0, 4, 0, 8, 12, 1, 5);
            capture.CrenellateRing(0, 16, 0, 8, 2, 1);
            capture.Carve(new int3(-2, 4, -9), new int3(4, 5, 4));
            return capture.Bake(101, 77, FeatureKind.Structure, new int3(0, 4, 0));
        }

        private static FeaturePresentationBake CaptureRoofedWalkway()
        {
            var capture = new StructurePresentationCaptureSession();
            capture.Box(new int3(30, 6, -3), new int3(20, 4, 10), 2);
            capture.Gable(new int3(30, 10, -3), new int3(20, 8, 10), true, 3);
            capture.Stairs(new int3(30, 6, -3), 4, 6, 1, 1, 0, 2);
            return capture.Bake(202, 88, FeatureKind.Structure, new int3(30, 6, -3));
        }
    }
}
