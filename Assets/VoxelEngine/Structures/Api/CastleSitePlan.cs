namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Planned surface styling for the castle site. Runtime consumes this as immutable realization
    /// input; it does not own a random stream for spatial builds.
    /// </summary>
    public readonly struct CastleSitePlan
    {
        public readonly uint GrassPatternSeed;
        public readonly byte GrassCoveragePercent;

        public CastleSitePlan(uint grassPatternSeed, byte grassCoveragePercent)
        {
            GrassPatternSeed = grassPatternSeed;
            GrassCoveragePercent = grassCoveragePercent > 100
                ? (byte)100
                : grassCoveragePercent;
        }

        /// <summary>
        /// Stable per-column grass decision. This is a pure lookup from planned seed + local X/Z;
        /// realization order and frame slicing cannot perturb the result.
        /// </summary>
        public bool ShouldGrassCap(int localX, int localZ)
        {
            if (GrassCoveragePercent == 0) return false;
            if (GrassCoveragePercent >= 100) return true;

            unchecked
            {
                uint value = GrassPatternSeed;
                value ^= (uint)localX * 0x8DA6B343u;
                value ^= (uint)localZ * 0xD8163841u;
                value ^= value >> 16;
                value *= 0x7FEB352Du;
                value ^= value >> 15;
                value *= 0x846CA68Bu;
                value ^= value >> 16;
                return value % 100u < GrassCoveragePercent;
            }
        }
    }

    /// <summary>Creates the site-style choices attached to generated castle topology.</summary>
    public static class CastleSitePlanner
    {
        private const uint GrassPatternElementId = 0x53495445u; // "SITE"

        public static CastleSitePlan Create(uint rootSeed) =>
            new CastleSitePlan(
                CastleSeedPartition.Derive(
                    rootSeed, CastleSeedDomain.Decor, GrassPatternElementId),
                92);
    }
}
