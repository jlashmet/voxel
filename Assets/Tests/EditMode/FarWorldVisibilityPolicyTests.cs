using Game.WorldBuilder.Runtime;
using MountingForce.WorldGen;
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

        private static readonly FarWorldVisibilityPolicy.DistanceCaps DistanceCaps =
            new FarWorldVisibilityPolicy.DistanceCaps(
                ordinaryMetres: 200f,
                settlementAnchorMetres: 800f,
                landmarkMetres: 1200f,
                horizonLandmarkMetres: 2000f);

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
            var policy = Policy();

            Assert.That(policy.Select(record, new float2(-44f, 5f)), Is.EqualTo(FarStructureTier.Mid));
            Assert.That(policy.Select(record, new float2(-53f, 5f)), Is.EqualTo(FarStructureTier.Mid));
            Assert.That(policy.Select(record, new float2(-65f, 5f)), Is.EqualTo(FarStructureTier.Far));
            Assert.That(policy.Select(record, new float2(-55f, 5f)), Is.EqualTo(FarStructureTier.Far));
            Assert.That(policy.Select(record, new float2(-44f, 5f)), Is.EqualTo(FarStructureTier.Mid));
        }

        [Test]
        public void ClusterSelection_HoldsFarRepresentationUntilMemberMidEnterThreshold()
        {
            StructureFarPresentation member = Record(8UL, StructureVisibilityClass.OrdinaryStructure, 100);
            WorldVisibilityClusterBuilder.Cluster cluster = WorldVisibilityClusterBuilder.Build(
                new[] { member },
                sectorSizeDm: 1000)[0];
            var policy = Policy();

            Assert.That(policy.SelectCluster(cluster, new float2(-44f, 5f)), Is.EqualTo(FarStructureTier.Culled));
            Assert.That(policy.SelectCluster(cluster, new float2(-65f, 5f)), Is.EqualTo(FarStructureTier.Far));
            Assert.That(policy.SelectCluster(cluster, new float2(-50f, 5f)), Is.EqualTo(FarStructureTier.Far));
            Assert.That(policy.SelectCluster(cluster, new float2(-44f, 5f)), Is.EqualTo(FarStructureTier.Culled));
        }

        [Test]
        public void SemanticClass_AllowsLandmarkHorizonWhileOrdinaryStructureCulls()
        {
            var policy = Policy();
            StructureFarPresentation landmark = Record(10UL, StructureVisibilityClass.Landmark, 100);
            StructureFarPresentation ordinary = Record(11UL, StructureVisibilityClass.OrdinaryStructure, 100);

            float2 horizonCamera = new float2(-445f, 5f);
            Assert.That(policy.Select(landmark, horizonCamera), Is.EqualTo(FarStructureTier.Horizon));
            Assert.That(policy.Select(ordinary, horizonCamera), Is.EqualTo(FarStructureTier.Culled));

            Assert.That(policy.Select(landmark, new float2(-595f, 5f)), Is.EqualTo(FarStructureTier.Horizon));
            Assert.That(policy.Select(landmark, new float2(-1100f, 5f)), Is.EqualTo(FarStructureTier.Culled));
        }

        [Test]
        public void SemanticDistanceCaps_CullOrdinaryBeforeLandmarkAtSameProjectedSize()
        {
            var policy = Policy();
            StructureFarPresentation ordinary = Record(20UL, StructureVisibilityClass.OrdinaryStructure, 1000);
            StructureFarPresentation landmark = Record(21UL, StructureVisibilityClass.Landmark, 1000);
            float2 camera = new float2(-250f, 50f);

            Assert.That(FarWorldVisibilityPolicy.ProjectedPixels(ordinary, camera, 90f, 1000),
                Is.GreaterThan(Thresholds.HorizonEnterPixels));
            Assert.That(policy.Select(ordinary, camera), Is.EqualTo(FarStructureTier.Culled));
            Assert.That(policy.Select(landmark, camera), Is.Not.EqualTo(FarStructureTier.Culled));
        }

        [TestCase(8000f, false)]
        [TestCase(10000f, false)]
        [TestCase(12000f, false)]
        [TestCase(8000f, true)]
        [TestCase(10000f, true)]
        [TestCase(12000f, true)]
        public void HorizonLandmark_RemainsSelectedAtRequiredCardinalAndDiagonalRanges(
            float distanceMetres,
            bool diagonal)
        {
            StructureFarPresentation landmark = Record(
                0xCA57UL,
                StructureVisibilityClass.HorizonLandmark,
                sizeDm: 1200);
            var policy = new FarWorldVisibilityPolicy(
                new FarWorldVisibilityPolicy.Thresholds(
                    midEnterPixels: 20f,
                    midExitPixels: 16f,
                    farEnterPixels: 8f,
                    farExitPixels: 6f,
                    horizonEnterPixels: 3f,
                    horizonExitPixels: 2f),
                new FarWorldVisibilityPolicy.DistanceCaps(
                    ordinaryMetres: 800f,
                    settlementAnchorMetres: 4000f,
                    landmarkMetres: 10000f,
                    horizonLandmarkMetres: 12050f),
                verticalFovDegrees: 90f,
                viewportHeightPixels: 1000);

            float2 centre = new float2(60f, 60f);
            float2 offset = diagonal
                ? new float2(distanceMetres * math.rsqrt(2f), distanceMetres * math.rsqrt(2f))
                : new float2(distanceMetres, 0f);
            float2 camera = centre + offset;

            Assert.That(
                FarWorldVisibilityPolicy.ProjectedPixels(landmark, camera, 90f, 1000),
                Is.GreaterThanOrEqualTo(3f),
                "the declared horizon landmark must remain above the configured horizon threshold");
            Assert.That(policy.Select(landmark, camera), Is.EqualTo(FarStructureTier.Horizon));
        }

        private static FarWorldVisibilityPolicy Policy() =>
            new FarWorldVisibilityPolicy(Thresholds, DistanceCaps, 90f, 1000);

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
