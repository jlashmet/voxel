using VoxelEngine.Rendering.Runtime;

namespace VoxelEngine.Composition
{
    /// <summary>
    /// Allocation-free publication diagnostics for derived near-field surface owners.
    /// Applications can gate validation/readiness on semantic renderer ownership without taking a
    /// dependency on Rendering.Runtime cache types or scene-specific implementation details.
    /// </summary>
    public static class RenderingSurfaceDiagnostics
    {
        /// <summary>
        /// Reports the dedicated liquid-surface owner's publication state from the most recently
        /// presented frame. A visible liquid chunk is a stronger signal than solid-terrain
        /// convergence: it proves authored Water/Cascade geometry reached the production liquid
        /// cache, completed a build, published GPU geometry, survived frustum culling, and is
        /// available to the Water render pass.
        /// </summary>
        public static void GetLiquidSurfaceCounts(
            out int resident,
            out int dirty,
            out int visible,
            out ulong completedBuilds)
        {
            var metrics = VoxelRenderBridge.SurfaceMetrics;
            resident = metrics.WaterResidentChunks;
            dirty = metrics.WaterDirtyChunks;
            visible = metrics.VisibleWaterChunks;
            completedBuilds = metrics.CompletedWaterBuilds;
        }
    }
}
