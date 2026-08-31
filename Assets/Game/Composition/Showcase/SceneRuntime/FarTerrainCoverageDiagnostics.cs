using System;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Presentation/debug snapshot for far-terrain coverage. It is derived entirely from clipmap
    /// configuration/publication state and never participates in world generation or rendering
    /// authority.
    /// </summary>
    public readonly struct FarTerrainCoverageDiagnostics
    {
        public FarTerrainCoverageDiagnostics(
            float requestedOuterRadiusMetres,
            float guaranteedAuthoritativeRadiusMetres,
            int ringCount,
            int[] ringSpacingVoxels,
            bool startupFallbackActive,
            bool requestedCoverageGuaranteed)
        {
            RequestedOuterRadiusMetres = requestedOuterRadiusMetres;
            GuaranteedAuthoritativeRadiusMetres = guaranteedAuthoritativeRadiusMetres;
            RingCount = ringCount;
            RingSpacingVoxels = ringSpacingVoxels ?? Array.Empty<int>();
            StartupFallbackActive = startupFallbackActive;
            RequestedCoverageGuaranteed = requestedCoverageGuaranteed;
        }

        public float RequestedOuterRadiusMetres { get; }
        public float GuaranteedAuthoritativeRadiusMetres { get; }
        public int RingCount { get; }
        public int[] RingSpacingVoxels { get; }
        public bool StartupFallbackActive { get; }
        public bool RequestedCoverageGuaranteed { get; }

        public static FarTerrainCoverageDiagnostics Capture(VoxelFarTerrain far)
        {
            if (far == null) throw new ArgumentNullException(nameof(far));

            bool covered = FarTerrainCoverageMath.TryCalculateRequiredRingCount(
                far.InnerRadiusMetres,
                far.OuterRadiusMetres,
                resolution: 96,
                out int ringCount,
                out float guaranteedCoverageMetres);
            // RingCount is the configured clipmap answer, so use the instance's spacing resolver
            // for each ring rather than duplicating its configuration math here.
            ringCount = far.RingCount;
            var spacings = new int[ringCount];
            for (int ring = 0; ring < ringCount; ring++)
                spacings[ring] = far.SpacingForRing(ring);

            if (ringCount > 0)
                guaranteedCoverageMetres = FarTerrainCoverageMath.GuaranteedCardinalCoverageMetres(
                    far.InnerRadiusMetres,
                    resolution: 96,
                    ring: ringCount - 1);

            return new FarTerrainCoverageDiagnostics(
                far.OuterRadiusMetres,
                guaranteedCoverageMetres,
                ringCount,
                spacings,
                startupFallbackActive: !far.HasSampledHeightsForEveryRing,
                requestedCoverageGuaranteed: covered
                    && guaranteedCoverageMetres >= far.OuterRadiusMetres);
        }

        public override string ToString()
        {
            string spacing = RingSpacingVoxels.Length == 0
                ? "none"
                : string.Join("/", RingSpacingVoxels);
            return $"requested={RequestedOuterRadiusMetres:0.#}m "
                 + $"guaranteed={GuaranteedAuthoritativeRadiusMetres:0.#}m "
                 + $"rings={RingCount} spacingVox={spacing} "
                 + $"fallback={StartupFallbackActive} covered={RequestedCoverageGuaranteed}";
        }
    }
}
