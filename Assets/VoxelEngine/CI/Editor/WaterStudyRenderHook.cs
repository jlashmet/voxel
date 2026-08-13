using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelEngine.CI
{
    /// <summary>
    /// Render-time presentation hook for the isolated water study. The study meshes are thin
    /// painted ribbons and pool cards, so both sides must remain visible from the high camera.
    /// </summary>
    [UnityEditor.InitializeOnLoad]
    internal static class WaterStudyRenderHook
    {
        static WaterStudyRenderHook()
        {
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }

        private static void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (camera == null || camera.name != "Water Study Camera") return;

            Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null || renderer.transform.root.name != "Water Reference Study") continue;
                Material material = renderer.sharedMaterial;
                if (material != null && material.HasProperty("_Cull"))
                    material.SetFloat("_Cull", 0f);
            }
        }
    }
}
