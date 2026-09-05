using UnityEngine;
using VoxelEngine.Rendering.Runtime;

namespace VoxelEngine.Composition
{
    /// <summary>
    /// Application-facing near/far presentation handoff. The configured voxel-ring radius is not
    /// authoritative until Rendering has actually published complete visible near-surface coverage;
    /// consumers must keep their fallback representation available until then.
    /// </summary>
    public static class PublishedNearSurfaceCoverage
    {
        public static float RadiusMetres =>
            RenderingComposition.HasCompletePublishedNearSurfaceCoverage()
                ? Mathf.Max(0f, VoxelRenderBridge.SurfaceMaxVoxelRingRadiusMetres)
                : 0f;
    }
}
