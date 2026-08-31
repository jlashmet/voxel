using NUnit.Framework;
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
            int rings = VoxelFarTerrain.CalculateRequiredRingCount(
                InnerRadiusMetres,
                OuterRadiusMetres,
                Resolution);

            Assert.That(rings, Is.EqualTo(6));
            Assert.That(
                VoxelFarTerrain.CalculateGuaranteedCardinalCoverageMetres(
                    InnerRadiusMetres,
                    Resolution,
                    rings - 1),
                Is.GreaterThanOrEqualTo(OuterRadiusMetres));
            Assert.That(
                VoxelFarTerrain.CalculateGuaranteedCardinalCoverageMetres(
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
            int rings = VoxelFarTerrain.CalculateRequiredRingCount(
                InnerRadiusMetres,
                OuterRadiusMetres,
                Resolution);
            int outerRing = rings - 1;

            Assert.That(
                VoxelFarTerrain.CalculateSnappedCardinalCoverageMetres(
                    cameraAxisMetres,
                    InnerRadiusMetres,
                    Resolution,
                    outerRing,
                    positiveSide: false),
                Is.GreaterThanOrEqualTo(OuterRadiusMetres),
                "negative X/Z cardinal side under-covered");
            Assert.That(
                VoxelFarTerrain.CalculateSnappedCardinalCoverageMetres(
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
            int rings = VoxelFarTerrain.CalculateRequiredRingCount(
                InnerRadiusMetres,
                100000000f,
                Resolution);

            Assert.That(rings, Is.EqualTo(VoxelFarTerrain.MaxRings));
            Assert.That(
                VoxelFarTerrain.CalculateGuaranteedCardinalCoverageMetres(
                    InnerRadiusMetres,
                    Resolution,
                    rings - 1),
                Is.LessThan(100000000f));
        }
    }
}
