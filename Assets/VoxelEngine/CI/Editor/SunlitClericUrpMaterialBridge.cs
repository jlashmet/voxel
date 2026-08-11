using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelEngine.CI
{
    /// <summary>
    /// Hooks only the deterministic CI lookdev camera. The shipping voxel renderer and gameplay
    /// scene are untouched: the capture still generates the real brickmap world first, then this
    /// replaces the temporary visual proxy with the purpose-built polished diorama for the shot.
    /// </summary>
    [InitializeOnLoad]
    internal static class SunlitClericUrpMaterialBridge
    {
        private static bool _prepared;

        static SunlitClericUrpMaterialBridge()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }

        private static void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (_prepared || camera == null || camera.name != "Sunlit Cleric Camera") return;

            GameObject originalHero = GameObject.Find("Madeline Lookdev Proxy");
            if (originalHero == null)
            {
                Debug.LogError("Sunlit Cleric: original proxy was not found, so the polished lookdev could not be positioned.");
                return;
            }

            Vector3 origin = originalHero.transform.position;

            // Hide only the temporary showcase visuals. The actual ShowcaseWorld / brick storage
            // has already been generated and remains the substrate being exercised by CI.
            GameObject smoothLayers = GameObject.Find("Sunlit Cleric Smooth Layers");
            if (smoothLayers != null) smoothLayers.SetActive(false);

            GameObject voxelSurface = GameObject.Find("Voxel Surface");
            if (voxelSurface != null) voxelSurface.SetActive(false);

            _prepared = true;
            SunlitClericPolishedDiorama.Build(camera, origin);
        }
    }
}
