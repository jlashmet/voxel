namespace VoxelEngine.Rendering.Runtime.SurfaceExtraction
{
    /// <summary>
    /// Build-request urgency for solid render geometry. Lower numeric values always win.
    /// Correctness coverage is intentionally ahead of visual refinement so reducing the frame
    /// budget delays detail rather than exposing a hole.
    /// </summary>
    internal enum SurfaceBuildPriority : byte
    {
        MissingVisibleCoverage = 0,
        PreserveActiveCoverage = 1,
        VisibleRefinement = 2,
        Prefetch = 3,
    }
}
