namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Planned surface styling for the castle site and courtyard. Runtime consumes this as immutable
    /// realization input; spatial builds do not own mutable random streams for surface materials.
    /// </summary>
    public readonly struct CastleSitePlan
    {
        public readonly uint GrassPatternSeed;
        public readonly byte GrassCoveragePercent;
        public readonly uint CourtyardPatternSeed;
        public readonly byte CourtyardStonePercent;

        public CastleSitePlan(uint grassPatternSeed, byte grassCoveragePercent)
            : this(grassPatternSeed, grassCoveragePercent, 0u, 0)
        {
        }

        public CastleSitePlan(
            uint grassPatternSeed,
            byte grassCoveragePercent,
            uint courtyardPatternSeed,
            byte courtyardStonePercent)
        {
            GrassPatternSeed = grassPatternSeed;
            GrassCoveragePercent = ClampPercent(grassCoveragePercent);
            CourtyardPatternSeed = courtyardPatternSeed;
            CourtyardStonePercent = ClampPercent(courtyardStonePercent);
        }

        /// <summary>
        /// Stable per-column grass decision. This is a pure lookup from planned seed + local X/Z;
        /// realization order and frame slicing cannot perturb the result.
        /// </summary>
        public bool ShouldGrassCap(int localX, int localZ) =>
            PercentHit(GrassPatternSeed, localX, localZ, GrassCoveragePercent);

        /// <summary>Stable planned choice between stone paving (true) and worn dirt (false).</summary>
        public bool ShouldUseCourtyardStone(int localX, int localZ) =>
            PercentHit(CourtyardPatternSeed, localX, localZ, CourtyardStonePercent);

        private static bool PercentHit(uint seed, int localX, int localZ, byte percent)
        {
            if (percent == 0) return false;
            if (percent >= 100) return true;

            unchecked
            {
                uint value = seed;
                value ^= (uint)localX * 0x8DA6B343u;
                value ^= (uint)localZ * 0xD8163841u;
                value ^= value >> 16;
                value *= 0x7FEB352Du;
                value ^= value >> 15;
                value *= 0x846CA68Bu;
                value ^= value >> 16;
                return value % 100u < percent;
            }
        }

        private static byte ClampPercent(byte value) => value > 100 ? (byte)100 : value;
    }

    /// <summary>Creates the site-style choices attached to generated castle topology.</summary>
    public static class CastleSitePlanner
    {
        private const uint GrassPatternElementId = 0x53495445u; // "SITE"
        private const uint CourtyardPatternElementId = 0x43545944u; // "CTYD"

        public static CastleSitePlan Create(uint rootSeed) =>
            new CastleSitePlan(
                CastleSeedPartition.Derive(
                    rootSeed, CastleSeedDomain.Decor, GrassPatternElementId),
                92,
                CastleSeedPartition.Derive(
                    rootSeed, CastleSeedDomain.Decor, CourtyardPatternElementId),
                82);
    }
}
