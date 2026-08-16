using UnityEngine;

namespace VoxelEngine.Characters.Runtime
{
    /// <summary>
    /// Resolves a character's visual without coupling gameplay to a specific model source.
    /// A preferred/generated visual wins when available; otherwise an author-assigned
    /// fallback can keep the character usable while generated content is unavailable.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterVisualResolver : MonoBehaviour
    {
        [SerializeField] private Transform visualRoot;
        [SerializeField] private GameObject preferredVisualPrefab;
        [SerializeField] private GameObject fallbackVisualPrefab;

        private GameObject currentVisual;
        private GameObject currentSourcePrefab;

        public Transform VisualRoot
        {
            get => visualRoot != null ? visualRoot : transform;
            set => visualRoot = value;
        }

        public GameObject PreferredVisualPrefab
        {
            get => preferredVisualPrefab;
            set => preferredVisualPrefab = value;
        }

        public GameObject FallbackVisualPrefab
        {
            get => fallbackVisualPrefab;
            set => fallbackVisualPrefab = value;
        }

        public GameObject CurrentVisual => currentVisual;

        public GameObject CurrentSourcePrefab => currentSourcePrefab;

        private void Awake()
        {
            ResolveVisual();
        }

        /// <summary>
        /// Re-evaluates the visual source. Reuses the owned instance when the selected
        /// source has not changed; otherwise replaces only the instance owned here.
        /// </summary>
        public GameObject ResolveVisual()
        {
            GameObject source = preferredVisualPrefab != null
                ? preferredVisualPrefab
                : fallbackVisualPrefab;

            if (source == null)
            {
                ClearVisual();
                return null;
            }

            if (currentVisual != null && currentSourcePrefab == source)
            {
                return currentVisual;
            }

            ClearVisual();

            Transform root = VisualRoot;
            currentVisual = Instantiate(source, root, false);
            currentVisual.name = source.name;
            currentVisual.transform.localPosition = Vector3.zero;
            currentVisual.transform.localRotation = Quaternion.identity;
            currentVisual.transform.localScale = Vector3.one;
            currentSourcePrefab = source;
            return currentVisual;
        }

        /// <summary>
        /// Assigns a generated/preferred visual and optionally swaps to it immediately.
        /// Passing null restores fallback resolution on refresh.
        /// </summary>
        public void SetPreferredVisual(GameObject prefab, bool refresh = true)
        {
            preferredVisualPrefab = prefab;
            if (refresh)
            {
                ResolveVisual();
            }
        }

        public void SetFallbackVisual(GameObject prefab, bool refresh = true)
        {
            fallbackVisualPrefab = prefab;
            if (refresh)
            {
                ResolveVisual();
            }
        }

        public void ClearVisual()
        {
            if (currentVisual != null)
            {
                DestroyInstance(currentVisual);
            }

            currentVisual = null;
            currentSourcePrefab = null;
        }

        private static void DestroyInstance(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(instance);
            }
            else
            {
                Object.DestroyImmediate(instance);
            }
        }
    }
}
