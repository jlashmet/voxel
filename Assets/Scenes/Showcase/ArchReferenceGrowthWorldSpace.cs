using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Keeps ArchLookdev's close-up hero foliage in the arch's world coordinate frame even though
    /// ArchReferenceGrowth is hosted on the movable Hero Arch Camera. The hook arms only for the
    /// ArchLookdev scene and removes itself after the first successful anchor, so there is no
    /// steady-state per-frame presentation cost.
    /// </summary>
    public static class ArchReferenceGrowthWorldSpace
    {
        private const string ArchSceneName = "ArchLookdev";
        private const string HeroRootName = "Arch Reference Hero Growth";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneHook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            RenderPipelineManager.beginCameraRendering -= AnchorOnBeginCameraRendering;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != ArchSceneName) return;
            ArmForNextRender();
        }

        public static void ArmForNextRender()
        {
            RenderPipelineManager.beginCameraRendering -= AnchorOnBeginCameraRendering;
            RenderPipelineManager.beginCameraRendering += AnchorOnBeginCameraRendering;
        }

        public static bool AnchorCamera(Camera camera)
        {
            if (camera == null || camera.GetComponent<ArchReferenceGrowth>() == null)
                return false;

            Transform heroRoot = camera.transform.Find(HeroRootName);
            if (heroRoot == null)
                return false;

            // ArchReferenceGrowth authors its vertices in the arch's world-space metre frame.
            // Keeping this child on the camera applies the saved camera pose a second time and
            // moves the foliage off the masonry. Preserve the authored local coordinates as world
            // coordinates instead.
            heroRoot.SetParent(null, false);
            heroRoot.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            heroRoot.localScale = Vector3.one;
            return true;
        }

        private static void AnchorOnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (!AnchorCamera(camera)) return;
            RenderPipelineManager.beginCameraRendering -= AnchorOnBeginCameraRendering;
        }
    }
}
