using UnityEngine;

namespace Game.Composition.Kentridge.Playable
{
    /// <summary>
    /// Reusable visual-root reconciliation for humanoid character presentations whose imported
    /// renderer bounds do not put the visible soles exactly on the authoritative actor root.
    /// Story, collision and navigation keep using the actor root; only presentation children move.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1000)]
    public sealed class CharacterVisualFootGrounding : MonoBehaviour
    {
        private const float AlignmentEpsilonMetres = 0.001f;
        private Transform _visualOffset;

        public static void Attach(GameObject root)
        {
            if (root == null || root.GetComponent<CharacterVisualFootGrounding>() != null) return;
            root.AddComponent<CharacterVisualFootGrounding>();
        }

        private void LateUpdate()
        {
            if (!isActiveAndEnabled) return;
            if (GetComponentInChildren<Animator>(true) == null) return;

            if (_visualOffset == null && !TryCreateVisualOffset(out _visualOffset)) return;
            AlignVisibleFeet(_visualOffset);
        }

        private bool TryCreateVisualOffset(out Transform offset)
        {
            offset = null;
            if (!TryGetVisibleBounds(out _)) return false;

            int childCount = transform.childCount;
            var originalChildren = new Transform[childCount];
            for (int i = 0; i < childCount; i++) originalChildren[i] = transform.GetChild(i);
            if (originalChildren.Length == 0) return false;

            var offsetObject = new GameObject("Character Visual Foot Offset");
            offset = offsetObject.transform;
            offset.SetParent(transform, false);
            for (int i = 0; i < originalChildren.Length; i++)
                originalChildren[i].SetParent(offset, true);
            return true;
        }

        private void AlignVisibleFeet(Transform offset)
        {
            if (offset == null || !TryGetVisibleBounds(out Bounds bounds)) return;
            float correction = transform.position.y - bounds.min.y;
            if (Mathf.Abs(correction) <= AlignmentEpsilonMetres) return;

            float scaleY = transform.lossyScale.y;
            if (Mathf.Abs(scaleY) <= 1e-6f) return;
            offset.localPosition += Vector3.up * (correction / scaleY);
        }

        private bool TryGetVisibleBounds(out Bounds bounds)
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            bool found = false;
            bounds = default;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else bounds.Encapsulate(renderer.bounds);
            }
            return found;
        }
    }
}
