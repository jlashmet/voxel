using System;
using Game.Kentridge.PlayableSlice;
using NUnit.Framework;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class KentridgeStreamingCoveragePolicyTests
    {
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void GuaranteedNearSurfaceRadiusMatchesDiscreteCircularResidencyBoundary(int loadRadiusRegions)
        {
            const float regionMetres = 51.2f;
            float expected = MinimumDistanceToExcludedColumnFromWorstCaseDemandPoint(
                loadRadiusRegions,
                regionMetres);

            float actual = KentridgeStreamingCoveragePolicy.GuaranteedNearSurfaceRadiusMetres(
                loadRadiusRegions,
                regionMetres);

            Assert.That(actual, Is.EqualTo(expected).Within(0.001f));
        }

        [Test]
        public void RadiusThreeDoesNotPromiseNominalThreeRegionMetricDisk()
        {
            const int loadRadiusRegions = 3;
            const float regionMetres = 51.2f;

            float guaranteed = KentridgeStreamingCoveragePolicy.GuaranteedNearSurfaceRadiusMetres(
                loadRadiusRegions,
                regionMetres);

            Assert.That(guaranteed, Is.EqualTo(102.4f).Within(0.001f));
            Assert.That(guaranteed, Is.LessThan(loadRadiusRegions * regionMetres),
                "A circular lattice of resident region coordinates does not guarantee a full metric disk out to the outer region-centre radius.");
        }

        [Test]
        public void InvalidCoverageInputsAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                KentridgeStreamingCoveragePolicy.GuaranteedNearSurfaceRadiusMetres(-1, 51.2f));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                KentridgeStreamingCoveragePolicy.GuaranteedNearSurfaceRadiusMetres(3, 0f));
        }

        private static float MinimumDistanceToExcludedColumnFromWorstCaseDemandPoint(
            int radius,
            float regionMetres)
        {
            // Worst-case demand lies on the +X/+Z edge of the centre region. Independently walk
            // excluded lattice cells and measure point-to-square distance; this deliberately does
            // not reproduce the production (R-1)*edge formula.
            float demandX = 0.5f * regionMetres;
            float demandZ = 0.5f * regionMetres;
            float best = float.PositiveInfinity;
            int extent = radius + 2;

            for (var x = -extent; x <= extent; x++)
            for (var z = -extent; z <= extent; z++)
            {
                if (x * x + z * z <= radius * radius) continue;

                float minX = (x - 0.5f) * regionMetres;
                float maxX = (x + 0.5f) * regionMetres;
                float minZ = (z - 0.5f) * regionMetres;
                float maxZ = (z + 0.5f) * regionMetres;
                float dx = demandX < minX ? minX - demandX : demandX > maxX ? demandX - maxX : 0f;
                float dz = demandZ < minZ ? minZ - demandZ : demandZ > maxZ ? demandZ - maxZ : 0f;
                float distance = (float)Math.Sqrt(dx * dx + dz * dz);
                if (distance < best) best = distance;
            }

            return best;
        }
    }
}
