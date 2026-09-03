using NUnit.Framework;

namespace VoxelEngine.Showcase.Tests.EditMode
{
    public sealed class VoxelFarTerrainCoverageTests
    {
        [Test]
        public void ShippedConfigurationGuaranteesTwelveKilometresAcrossSnapPhases()
        {
            const float innerRadiusMetres = 409.6f;
            const float outerRadiusMetres = 12000f;
            const int resolution = 96;
            const int maxRings = 12;

            bool covered = VoxelFarTerrain.TryRequiredRingCount(
                innerRadiusMetres,
                outerRadiusMetres,
                resolution,
                maxRings,
                out int ringCount,
                out float guaranteedCoverageMetres);

            Assert.That(covered, Is.True);
            Assert.That(ringCount, Is.EqualTo(6),
                "The minimum guaranteed layout needs six rings; the old heuristic selected five.");
            Assert.That(guaranteedCoverageMetres, Is.GreaterThanOrEqualTo(outerRadiusMetres));

            int outerSpacing = VoxelFarTerrain.RingSpacingVoxels(
                innerRadiusMetres, resolution, ringCount - 1);
            float[] representativeSnapPhases = { 0f, 0.25f, 0.5f, 0.75f, 0.9999f, 1f };
            foreach (float snapPhase in representativeSnapPhases)
            {
                float coverage = VoxelFarTerrain.CoverageAtSnapPhaseMetres(
                    outerSpacing, resolution, snapPhase);
                Assert.That(coverage, Is.GreaterThanOrEqualTo(outerRadiusMetres),
                    $"Snap phase {snapPhase} reduced cardinal coverage below 12 km.");
            }

            int previousSpacing = VoxelFarTerrain.RingSpacingVoxels(
                innerRadiusMetres, resolution, ringCount - 2);
            Assert.That(
                VoxelFarTerrain.GuaranteedCoverageMetres(previousSpacing, resolution),
                Is.LessThan(outerRadiusMetres),
                "The selected ring count must be the minimum that guarantees the requested radius.");
        }

        [Test]
        public void MaxRingGuardReportsWhenRequestedCoverageCannotBeGuaranteed()
        {
            const float innerRadiusMetres = 409.6f;
            const int resolution = 96;
            const int maxRings = 3;
            const float unreachableOuterRadiusMetres = 50000f;

            bool covered = VoxelFarTerrain.TryRequiredRingCount(
                innerRadiusMetres,
                unreachableOuterRadiusMetres,
                resolution,
                maxRings,
                out int ringCount,
                out float guaranteedCoverageMetres);

            Assert.That(covered, Is.False);
            Assert.That(ringCount, Is.EqualTo(maxRings));
            Assert.That(guaranteedCoverageMetres, Is.LessThan(unreachableOuterRadiusMetres));
        }

        [Test]
        public void CoverageHelpersExposeSpacingHalfExtentAndWorstCaseSnapLoss()
        {
            const float innerRadiusMetres = 409.6f;
            const int resolution = 96;

            int spacing = VoxelFarTerrain.RingSpacingVoxels(innerRadiusMetres, resolution, 0);

            Assert.That(spacing, Is.EqualTo(128));
            Assert.That(VoxelFarTerrain.RingHalfExtentMetres(spacing, resolution), Is.EqualTo(614.4f).Within(0.001f));
            Assert.That(VoxelFarTerrain.CameraSnapLossMetres(spacing), Is.EqualTo(12.8f).Within(0.001f));
            Assert.That(VoxelFarTerrain.GuaranteedCoverageMetres(spacing, resolution), Is.EqualTo(601.6f).Within(0.001f));
        }
    }
}
