namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Pure startup-publication rules for the far-terrain clipmap. The startup fallback may occupy
    /// the final ring mesh while that ring's authoritative samples are prepared, so readiness and
    /// publication are deliberately separate facts.
    /// </summary>
    internal static class FarTerrainStartupCoverage
    {
        internal static ulong RequiredMask(int ringCount)
        {
            if (ringCount <= 0) return 0UL;
            if (ringCount >= 64) return ulong.MaxValue;
            return (1UL << ringCount) - 1UL;
        }

        internal static int ContiguousPublishedRing(ulong publishedMask, int ringCount)
        {
            int contiguous = -1;
            for (int ring = 0; ring < ringCount; ring++)
            {
                if ((publishedMask & (1UL << ring)) == 0UL) break;
                contiguous = ring;
            }
            return contiguous;
        }

        internal static bool CanPublishFinalRingAndRetireFallback(
            int ringCount,
            int fallbackRing,
            ulong authoritativePublishedMask,
            bool fallbackRingSamplesReady,
            float finalGuaranteedCoverageMetres,
            float requestedCoverageMetres)
        {
            if (ringCount <= 0 || fallbackRing < 0 || fallbackRing != ringCount - 1)
                return false;
            if (!fallbackRingSamplesReady || finalGuaranteedCoverageMetres < requestedCoverageMetres)
                return false;

            ulong finalBit = 1UL << fallbackRing;
            ulong lowerRequired = RequiredMask(ringCount) & ~finalBit;
            return (authoritativePublishedMask & lowerRequired) == lowerRequired;
        }

        internal static float EffectiveCoverageMetres(
            float requestedCoverageMetres,
            float contiguousAuthoritativeCoverageMetres,
            bool fallbackActive)
        {
            return fallbackActive
                ? System.Math.Max(requestedCoverageMetres, contiguousAuthoritativeCoverageMetres)
                : contiguousAuthoritativeCoverageMetres;
        }
    }
}
