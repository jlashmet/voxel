using UnityEngine;

namespace VoxelEngine.Rendering.Runtime
{
    /// <summary>
    /// Pure geometric clipmap coverage math. Distances and resolution are explicit inputs; this
    /// helper owns no camera, scene, storage, world-generation, or render-object state.
    /// </summary>
    public static class FarTerrainCoverageMath
    {
        public const float VoxelSizeMetres = 0.1f;
        public const int MaxRings = 12;

        public static int RingSpacingVoxels(float innerRadiusMetres, int resolution, int ring)
        {
            int safeResolution = Mathf.Max(1, resolution);
            float innerVoxels = Mathf.Max(0f, innerRadiusMetres) / VoxelSizeMetres;
            int baseSpacing = Mathf.Max(1, Mathf.NextPowerOfTwo(
                Mathf.CeilToInt(innerVoxels * 2f / safeResolution)));
            return baseSpacing << Mathf.Clamp(ring, 0, MaxRings - 1);
        }

        public static float RingHalfExtentMetres(
            float innerRadiusMetres,
            int resolution,
            int ring)
        {
            int spacing = RingSpacingVoxels(innerRadiusMetres, resolution, ring);
            return spacing * Mathf.Max(1, resolution) * 0.5f * VoxelSizeMetres;
        }

        public static float CameraSnapLossMetres(
            float innerRadiusMetres,
            int resolution,
            int ring)
        {
            return RingSpacingVoxels(innerRadiusMetres, resolution, ring) * VoxelSizeMetres;
        }

        public static float GuaranteedCardinalCoverageMetres(
            float innerRadiusMetres,
            int resolution,
            int ring)
        {
            return Mathf.Max(
                0f,
                RingHalfExtentMetres(innerRadiusMetres, resolution, ring)
                - CameraSnapLossMetres(innerRadiusMetres, resolution, ring));
        }

        public static bool TryCalculateRequiredRingCount(
            float innerRadiusMetres,
            float outerRadiusMetres,
            int resolution,
            out int ringCount,
            out float guaranteedCoverageMetres)
        {
            float requested = Mathf.Max(0f, outerRadiusMetres);
            for (int ring = 0; ring < MaxRings; ring++)
            {
                guaranteedCoverageMetres = GuaranteedCardinalCoverageMetres(
                    innerRadiusMetres,
                    resolution,
                    ring);
                if (guaranteedCoverageMetres >= requested)
                {
                    ringCount = ring + 1;
                    return true;
                }
            }

            ringCount = MaxRings;
            guaranteedCoverageMetres = GuaranteedCardinalCoverageMetres(
                innerRadiusMetres,
                resolution,
                MaxRings - 1);
            return false;
        }

        public static int CalculateRequiredRingCount(
            float innerRadiusMetres,
            float outerRadiusMetres,
            int resolution)
        {
            TryCalculateRequiredRingCount(
                innerRadiusMetres,
                outerRadiusMetres,
                resolution,
                out int ringCount,
                out _);
            return ringCount;
        }

        public static bool CanRetireStartupFallback(
            int currentAuthoritativePrefixRingCount,
            float innerRadiusMetres,
            float outerRadiusMetres,
            int resolution)
        {
            if (!TryCalculateRequiredRingCount(
                    innerRadiusMetres,
                    outerRadiusMetres,
                    resolution,
                    out int requiredRingCount,
                    out float guaranteedCoverageMetres))
                return false;

            return currentAuthoritativePrefixRingCount >= requiredRingCount
                && guaranteedCoverageMetres >= Mathf.Max(0f, outerRadiusMetres);
        }

        public static float SnappedCardinalCoverageMetres(
            float cameraAxisMetres,
            float innerRadiusMetres,
            int resolution,
            int ring,
            bool positiveSide)
        {
            int spacing = RingSpacingVoxels(innerRadiusMetres, resolution, ring);
            int centreVoxel = Mathf.FloorToInt(cameraAxisMetres / VoxelSizeMetres);
            int snappedCentreVoxel = FloorTo(centreVoxel, spacing);
            float snappedCentreMetres = snappedCentreVoxel * VoxelSizeMetres;
            float halfExtent = RingHalfExtentMetres(innerRadiusMetres, resolution, ring);
            float min = snappedCentreMetres - halfExtent;
            float max = snappedCentreMetres + halfExtent;
            return positiveSide ? max - cameraAxisMetres : cameraAxisMetres - min;
        }

        private static int FloorTo(int value, int step)
        {
            int quotient = value / step;
            if (value % step != 0 && value < 0) quotient--;
            return quotient * step;
        }
    }
}
