using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelEngine.Rendering.Runtime.GpuVoxel
{
    /// <summary>
    /// Temporary production bridge while GPU publication identity is being moved out of the mixed
    /// legacy CPU cache. Candidate geometry is committed only after the frame that submitted and
    /// finalized it, and the commit kernel revalidates the handle's current desired generation.
    ///
    /// This is not the final world/build/config identity contract: the stronger CPU approval gate
    /// remains required before the CPU rendering backend is deleted. It exists so status-aware GPU
    /// publication can be exercised in VoxelShowcase without reverting to immediate submission
    /// success or a CPU rendering fallback.
    /// </summary>
    internal static class GpuPendingPublicationPump
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Install()
        {
            RenderPipelineManager.endFrameRendering -= OnEndFrameRendering;
            RenderPipelineManager.endFrameRendering += OnEndFrameRendering;
        }

        private static void OnEndFrameRendering(
            ScriptableRenderContext context, Camera[] cameras)
        {
            GpuSurfacePageArena.CommitCurrentPendingForActiveArena(Time.frameCount);
        }
    }
}
