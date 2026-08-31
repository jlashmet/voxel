using MountingForce.WorldGen.Architecture;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Rendering.Api;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class FarWorldVisibilityPolicyTests
    {
        private static readonly FarWorldVisibilityPolicy.Thresholds Thresholds =
            new FarWorldVisibilityPolicy.Thresholds(
                midEnterPixels: 100f,
                midExitPixels: 80f,
                farEnterPixels: 40f,
                farExitPixels: 30f,
                horizonEnterPixels: 10f,
                horizonExitPixels: 5f);

        [Test]
        public void ProjectedPixels_DecreaseWithDistanceAndIncreaseWithStructureSize()
        {
            StructureFarPresentation small = Record(1UL, StructureVisibilityClass.OrdinaryStructure, 100);
            StructureFarPresentation large = Record(2UL, StructureVisibilityClass.Landmark, 200);

            float near = FarWorldVisibilityPolicy.ProjectedPixels(small, new float2(-40f, 5f), 90f, 1000);
            float far = FarWorldVisibilityPolicy.ProjectedPixels(small, new float2(-90f, 5f), 90f, 1000);
            float largeFar = FarWorldVisibilityPolicy.ProjectedPixels(large, new float2(-90f, 5f), 90f, 1000);

            Assert.That(near, Is.GreaterThan(far));
            Assert.That(largeFar, Is.GreaterThan(far));
        }

        [Test]
        public void TierSelection_HoldsAcrossBoundaryUntilExitThreshold()
        {
            StructureFarPresentation record = Record(7UL, StructureVisibilityClass.Landmark, 100);
            var policy = new FarWorldVisibilityPolicy(Thresholds, 90f, 1000);

            Assert.That(policy.Select(record, new float2(-44f, 5f)), Is.EqualTo(FarStructureTier.Mid));
            Assert.That(policy.Select(record, new float2(-53f, 5f)), Is.EqualTo(FarStructureTier.Mid));
            Assert.That(policy.Select(record, new float2(-65f, 5f)), Is.EqualTo(FarStructureTier.Far));
            Assert.That(policy.Select(record, new float2(-55f, 5f)), Is.EqualTo(FarStructureTier.Far));
            Assert.That(policy.Select(record, new float2(-44f, 5f)), Is.EqualTo(FarStructureTier.Mid));
        }

        [Test]
        public void SemanticClass_AllowsLandmarkHorizonWhileOrdinaryStructureCulls()
        {
            var policy = new FarWorldVisibilityPolicy(Thresholds, 90f, 1000);
            StructureFarPresentation landmark = Record(10UL, StructureVisibilityClass.Landmark, 100);
            StructureFarPresentation ordinary = Record(11UL, StructureVisibilityClass.OrdinaryStructure, 100);

            float2 horizonCamera = new float2(-445f, 5f);
            Assert.That(policy.Select(landmark, horizonCamera), Is.EqualTo(FarStructureTier.Horizon));
            Assert.That(policy.Select(ordinary, horizonCamera), Is.EqualTo(FarStructureTier.Culled));

            Assert.That(policy.Select(landmark, new float2(-595f, 5f)), Is.EqualTo(FarStructureTier.Horizon));
            Assert.That(policy.Select(landmark, new float2(-1100f, 5f)), Is.EqualTo(FarStructureTier.Culled));
        }

        private static StructureFarPresentation Record(
            ulong key,
            StructureVisibilityClass visibility,
            int sizeDm)
        {
            return new StructureFarPresentation(
                key,
                1UL,
                new Int2(0, 0),
                new Int2(sizeDm, sizeDm),
                sizeDm,
                (FrontageDirection)0,
                (StructureArchetype)0,
                2UL,
                3UL,
                visibility,
                4UL);
        }
    }
}
