using UnityEngine;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Keeps ArchLookdev's close-up hero foliage in the arch's world coordinate frame even though
    /// ArchReferenceGrowth is hosted on the movable Hero Arch Camera. The operation is synchronous:
    /// ArchReferenceGrowth invokes it immediately after building the hero mesh root, so presentation
    /// does not depend on render-pipeline callback ordering.
    /// </summary>
    public static class ArchReferenceGrowthWorldSpace
    {
        private const string HeroRootName = "Arch Reference Hero Growth";

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
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            Debug.Log("ARCH_REFERENCE_ANCHOR world-identity hero root applied.");
#endif
            return true;
        }
    }
}
