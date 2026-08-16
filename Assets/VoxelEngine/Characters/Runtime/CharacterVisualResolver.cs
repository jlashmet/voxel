using System;
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

        public event Action<GameObject> VisualChanged;

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

        private void OnDestroy()
        {
            ClearVisual();
        }

        /// <summary>
        /// Re-evaluates the visual source. Reuses the owned instance when the selected
        /// source has not changed, while still enforcing the configured visual root.
        /// Otherwise replaces only the instance owned here.
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

            Transform root = VisualRoot;
            if (currentVisual != null && currentSourcePrefab == source)
            {
                NormalizeVisualTransform(currentVisual.transform, root);
                return currentVisual;
            }

            DestroyCurrentVisual();

            currentVisual = Instantiate(source, root, false);
            currentVisual.name = source.name;
            NormalizeVisualTransform(currentVisual.transform, root);
            currentSourcePrefab = source;
            VisualChanged?.Invoke(currentVisual);
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
            bool hadVisual = currentVisual != null || currentSourcePrefab != null;
            DestroyCurrentVisual();
            if (hadVisual)
            {
                VisualChanged?.Invoke(null);
            }
        }

        private void DestroyCurrentVisual()
        {
            if (currentVisual != null)
            {
                DestroyInstance(currentVisual);
            }

            currentVisual = null;
            currentSourcePrefab = null;
        }

        private static void NormalizeVisualTransform(Transform instance, Transform root)
        {
            if (instance.parent != root)
            {
                instance.SetParent(root, false);
            }

            instance.localPosition = Vector3.zero;
            instance.localRotation = Quaternion.identity;
            instance.localScale = Vector3.one;
        }

        private static void DestroyInstance(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                // Object.Destroy is deferred until the end of the frame. Hide the replaced
                // visual immediately so a generated/fallback swap cannot double-render.
                instance.SetActive(false);
                Object.Destroy(instance);
            }
            else
            {
                Object.DestroyImmediate(instance);
            }
        }
    }
}
