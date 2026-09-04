using NUnit.Framework;

namespace VoxelEngine.Showcase.Tests.EditMode
{
    public sealed class FarTerrainStartupCoverageTests
    {
        [Test]
        public void FinalRingReadinessCannotRetireFallbackAcrossIntermediatePublicationHole()
        {
            const int ringCount = 6;
            const int fallbackRing = 5;
            ulong published = 0b11_0111UL; // ring 3 is still missing; final bit is irrelevant while fallback owns it.

            bool canRetire = FarTerrainStartupCoverage.CanPublishFinalRingAndRetireFallback(
                ringCount,
                fallbackRing,
                published,
                fallbackRingSamplesReady: true,
                finalGuaranteedCoverageMetres: 19251.2f,
                requestedCoverageMetres: 12000f);

            Assert.That(canRetire, Is.False);
            Assert.That(FarTerrainStartupCoverage.ContiguousPublishedRing(published, ringCount), Is.EqualTo(2));
            Assert.That(
                FarTerrainStartupCoverage.EffectiveCoverageMetres(12000f, 2400f, fallbackActive: true),
                Is.EqualTo(12000f));
        }

        [Test]
        public void CompleteLowerPublicationAndReadyFinalRingCanRetireWithoutCoverageShrink()
        {
            const int ringCount = 6;
            const int fallbackRing = 5;
            ulong lowerPublished = 0b01_1111UL;
            const float requested = 12000f;
            const float finalCoverage = 19251.2f;

            bool canRetire = FarTerrainStartupCoverage.CanPublishFinalRingAndRetireFallback(
                ringCount,
                fallbackRing,
                lowerPublished,
                fallbackRingSamplesReady: true,
                finalGuaranteedCoverageMetres: finalCoverage,
                requestedCoverageMetres: requested);

            Assert.That(canRetire, Is.True);
            float before = FarTerrainStartupCoverage.EffectiveCoverageMetres(
                requested, 9625.6f, fallbackActive: true);
            float after = FarTerrainStartupCoverage.EffectiveCoverageMetres(
                requested, finalCoverage, fallbackActive: false);
            Assert.That(after, Is.GreaterThanOrEqualTo(before),
                "Retiring fallback must not reduce requested far coverage.");
        }

        [Test]
        public void FinalRingMustBeReadyAndGuaranteeRequestedRadius()
        {
            ulong lowerPublished = 0b01_1111UL;

            Assert.That(FarTerrainStartupCoverage.CanPublishFinalRingAndRetireFallback(
                6, 5, lowerPublished, false, 19251.2f, 12000f), Is.False);
            Assert.That(FarTerrainStartupCoverage.CanPublishFinalRingAndRetireFallback(
                6, 5, lowerPublished, true, 11000f, 12000f), Is.False);
        }
    }
}
