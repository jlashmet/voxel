using UnityEngine;
using UnityEngine.SceneManagement;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Keeps ArchLookdev's close-up hero foliage in the arch's world coordinate frame even though
    /// ArchReferenceGrowth is hosted on the movable Hero Arch Camera. Installation is independent
    /// of scene callback ordering: if growth already built its root we anchor immediately; if not,
    /// the camera's child-change callback catches the root as soon as it is parented. There is no
    /// steady-state Update or render-pipeline callback.
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
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallStartupAnchor()
        {
            InstallForActiveArchLookdev();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != ArchSceneName) return;
            InstallForActiveArchLookdev();
        }

        private static void InstallForActiveArchLookdev()
        {
            ArchLookdev lookdev = Object.FindAnyObjectByType<ArchLookdev>();
            if (lookdev == null) return;
            EnsureInstalled(lookdev.GetComponent<Camera>());
        }

        public static void EnsureInstalled(Camera camera)
        {
            if (camera == null) return;
            ArchReferenceGrowthWorldSpaceAnchor anchor =
                camera.GetComponent<ArchReferenceGrowthWorldSpaceAnchor>();
            if (anchor == null)
                anchor = camera.gameObject.AddComponent<ArchReferenceGrowthWorldSpaceAnchor>();
            anchor.TryAnchor();
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
    }

    [DisallowMultipleComponent]
    internal sealed class ArchReferenceGrowthWorldSpaceAnchor : MonoBehaviour
    {
        private void OnEnable()
        {
            TryAnchor();
        }

        private void OnTransformChildrenChanged()
        {
            TryAnchor();
        }

        public void TryAnchor()
        {
            ArchReferenceGrowthWorldSpace.AnchorCamera(GetComponent<Camera>());
        }
    }
}
