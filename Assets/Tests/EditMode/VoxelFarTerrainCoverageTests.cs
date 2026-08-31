using NUnit.Framework;
using UnityEngine;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class VoxelFarTerrainCoverageTests
    {
        private const float InnerRadiusMetres = 409.6f;
        private const float OuterRadiusMetres = 12000f;
        private const int Resolution = 96;

        [Test]
        public void ShippedConfiguration_SelectsMinimumRingCountThatGuaranteesTwelveKilometres()
        {
            int rings = FarTerrainCoverageMath.CalculateRequiredRingCount(
                InnerRadiusMetres,
                OuterRadiusMetres,
                Resolution);

            Assert.That(rings, Is.EqualTo(6));
            Assert.That(
                FarTerrainCoverageMath.GuaranteedCardinalCoverageMetres(
                    InnerRadiusMetres,
                    Resolution,
                    rings - 1),
                Is.GreaterThanOrEqualTo(OuterRadiusMetres));
            Assert.That(
                FarTerrainCoverageMath.GuaranteedCardinalCoverageMetres(
                    InnerRadiusMetres,
                    Resolution,
                    rings - 2),
                Is.LessThan(OuterRadiusMetres));
        }

        [TestCase(0f)]
        [TestCase(204.8f)]
        [TestCase(409.599f)]
        [TestCase(-0.001f)]
        public void ShippedConfiguration_CoversEveryCardinalSideAcrossCameraSnapPhases(
            float cameraAxisMetres)
        {
            int rings = FarTerrainCoverageMath.CalculateRequiredRingCount(
                InnerRadiusMetres,
                OuterRadiusMetres,
                Resolution);
            int outerRing = rings - 1;

            Assert.That(
                FarTerrainCoverageMath.SnappedCardinalCoverageMetres(
                    cameraAxisMetres,
                    InnerRadiusMetres,
                    Resolution,
                    outerRing,
                    positiveSide: false),
                Is.GreaterThanOrEqualTo(OuterRadiusMetres),
                "negative X/Z cardinal side under-covered");
            Assert.That(
                FarTerrainCoverageMath.SnappedCardinalCoverageMetres(
                    cameraAxisMetres,
                    InnerRadiusMetres,
                    Resolution,
                    outerRing,
                    positiveSide: true),
                Is.GreaterThanOrEqualTo(OuterRadiusMetres),
                "positive X/Z cardinal side under-covered");
        }

        [Test]
        public void RequiredRingCount_ReturnsGuardWhenRequestedRadiusCannotBeCovered()
        {
            bool covered = FarTerrainCoverageMath.TryCalculateRequiredRingCount(
                InnerRadiusMetres,
                100000000f,
                Resolution,
                out int rings,
                out float guaranteedCoverageMetres);

            Assert.That(covered, Is.False);
            Assert.That(rings, Is.EqualTo(FarTerrainCoverageMath.MaxRings));
            Assert.That(guaranteedCoverageMetres, Is.LessThan(100000000f));
            Assert.That(
                FarTerrainCoverageMath.CanRetireStartupFallback(
                    rings,
                    InnerRadiusMetres,
                    100000000f,
                    Resolution),
                Is.False);
        }

        [Test]
        public void VoxelFarTerrain_UsesGuaranteedCoverageRingCount()
        {
            VoxelFarTerrain far = VoxelFarTerrain.Create(
                parent: null,
                seed: 1,
                innerRadiusMetres: InnerRadiusMetres,
                outerRadiusMetres: OuterRadiusMetres);
            try
            {
                Assert.That(
                    far.RingCount,
                    Is.EqualTo(FarTerrainCoverageMath.CalculateRequiredRingCount(
                        InnerRadiusMetres,
                        OuterRadiusMetres,
                        Resolution)));
            }
            finally
            {
                Object.DestroyImmediate(far.gameObject);
            }
        }

        [Test]
        public void StartupFallback_RetiresOnlyAfterGapFreeRequiredAuthoritativePrefix()
        {
            int requiredRings = FarTerrainCoverageMath.CalculateRequiredRingCount(
                InnerRadiusMetres,
                OuterRadiusMetres,
                Resolution);

            Assert.That(requiredRings, Is.EqualTo(6));
            Assert.That(
                FarTerrainCoverageMath.CanRetireStartupFallback(
                    requiredRings - 1,
                    InnerRadiusMetres,
                    OuterRadiusMetres,
                    Resolution),
                Is.False,
                "publishing the outer slot must not retire fallback while a required ring is missing or stale");
            Assert.That(
                FarTerrainCoverageMath.CanRetireStartupFallback(
                    requiredRings,
                    InnerRadiusMetres,
                    OuterRadiusMetres,
                    Resolution),
                Is.True);

            float coverageImmediatelyAfterRetirement =
                FarTerrainCoverageMath.GuaranteedCardinalCoverageMetres(
                    InnerRadiusMetres,
                    Resolution,
                    requiredRings - 1);
            Assert.That(
                coverageImmediatelyAfterRetirement,
                Is.GreaterThanOrEqualTo(OuterRadiusMetres),
                "fallback retirement must never shrink requested far coverage");
        }
    }
}
