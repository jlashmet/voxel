namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Showcase compatibility surface for the engine-level clipmap coverage contract. Scene
    /// composition keeps its existing call sites while the reusable/testable math lives in the
    /// rendering runtime assembly.
    /// </summary>
    public static class FarTerrainCoverageMath
    {
        public const float VoxelSizeMetres = Rendering.Runtime.FarTerrainCoverageMath.VoxelSizeMetres;
        public const int MaxRings = Rendering.Runtime.FarTerrainCoverageMath.MaxRings;

        public static int RingSpacingVoxels(float innerRadiusMetres, int resolution, int ring) =>
            Rendering.Runtime.FarTerrainCoverageMath.RingSpacingVoxels(
                innerRadiusMetres, resolution, ring);

        public static float RingHalfExtentMetres(
            float innerRadiusMetres,
            int resolution,
            int ring) =>
            Rendering.Runtime.FarTerrainCoverageMath.RingHalfExtentMetres(
                innerRadiusMetres, resolution, ring);

        public static float CameraSnapLossMetres(
            float innerRadiusMetres,
            int resolution,
            int ring) =>
            Rendering.Runtime.FarTerrainCoverageMath.CameraSnapLossMetres(
                innerRadiusMetres, resolution, ring);

        public static float GuaranteedCardinalCoverageMetres(
            float innerRadiusMetres,
            int resolution,
            int ring) =>
            Rendering.Runtime.FarTerrainCoverageMath.GuaranteedCardinalCoverageMetres(
                innerRadiusMetres, resolution, ring);

        public static bool TryCalculateRequiredRingCount(
            float innerRadiusMetres,
            float outerRadiusMetres,
            int resolution,
            out int ringCount,
            out float guaranteedCoverageMetres) =>
            Rendering.Runtime.FarTerrainCoverageMath.TryCalculateRequiredRingCount(
                innerRadiusMetres,
                outerRadiusMetres,
                resolution,
                out ringCount,
                out guaranteedCoverageMetres);

        public static int CalculateRequiredRingCount(
            float innerRadiusMetres,
            float outerRadiusMetres,
            int resolution) =>
            Rendering.Runtime.FarTerrainCoverageMath.CalculateRequiredRingCount(
                innerRadiusMetres, outerRadiusMetres, resolution);

        public static bool CanRetireStartupFallback(
            int currentAuthoritativePrefixRingCount,
            float innerRadiusMetres,
            float outerRadiusMetres,
            int resolution) =>
            Rendering.Runtime.FarTerrainCoverageMath.CanRetireStartupFallback(
                currentAuthoritativePrefixRingCount,
                innerRadiusMetres,
                outerRadiusMetres,
                resolution);

        public static float SnappedCardinalCoverageMetres(
            float cameraAxisMetres,
            float innerRadiusMetres,
            int resolution,
            int ring,
            bool positiveSide) =>
            Rendering.Runtime.FarTerrainCoverageMath.SnappedCardinalCoverageMetres(
                cameraAxisMetres,
                innerRadiusMetres,
                resolution,
                ring,
                positiveSide);
    }
}
