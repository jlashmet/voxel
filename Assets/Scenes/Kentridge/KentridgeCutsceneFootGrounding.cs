using System.Collections.Generic;
using UnityEngine;

namespace Game.Kentridge.PlayableSlice
{
    /// <summary>
    /// Keeps generated cutscene stage points semantic while adapting imported humanoid visuals whose
    /// prefab root is not exactly at the visible soles. The actor root remains the architecture-owned
    /// stage position used by story, camera and interaction code; only its visual children are offset.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1000)]
    public sealed class KentridgeCutsceneFootGrounding : MonoBehaviour
    {
        private const float AlignmentEpsilonMetres = 0.001f;
        private readonly HashSet<int> _normalizedRoots = new HashSet<int>();

        private void LateUpdate()
        {
            if (!gameObject.scene.IsValid() || !gameObject.scene.isLoaded) return;

            GameObject[] roots = gameObject.scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                GameObject root = roots[i];
                if (root == null || root == gameObject || !root.activeInHierarchy) continue;
                if (_normalizedRoots.Contains(root.GetInstanceID())) continue;
                if (root.GetComponentInChildren<Animator>(true) == null) continue;

                if (TryNormalizeVisibleFeet(root, out float correction, out float before, out float after))
                {
                    _normalizedRoots.Add(root.GetInstanceID());
                    Debug.Log(
                        "KENTRIDGE_FOOT_ALIGNMENT actor=" + root.name
                        + " stageY=" + root.transform.position.y.ToString("F3")
                        + " beforeMinY=" + before.ToString("F3")
                        + " correction=" + correction.ToString("F3")
                        + " afterMinY=" + after.ToString("F3"));
                }
            }
        }

        private static bool TryNormalizeVisibleFeet(
            GameObject root,
            out float correction,
            out float beforeMinY,
            out float afterMinY)
        {
            correction = 0f;
            beforeMinY = 0f;
            afterMinY = 0f;

            if (!TryGetVisibleBounds(root, out Bounds beforeBounds)) return false;
            beforeMinY = beforeBounds.min.y;
            correction = root.transform.position.y - beforeMinY;

            if (Mathf.Abs(correction) <= AlignmentEpsilonMetres)
            {
                afterMinY = beforeMinY;
                return true;
            }

            float scaleY = root.transform.lossyScale.y;
            if (Mathf.Abs(scaleY) <= 1e-6f) return false;

            int childCount = root.transform.childCount;
            var originalChildren = new Transform[childCount];
            for (var i = 0; i < childCount; i++)
                originalChildren[i] = root.transform.GetChild(i);

            var offsetObject = new GameObject("Kentridge Visual Foot Offset");
            Transform offset = offsetObject.transform;
            offset.SetParent(root.transform, false);
            for (var i = 0; i < originalChildren.Length; i++)
                originalChildren[i].SetParent(offset, true);

            offset.localPosition = Vector3.up * (correction / scaleY);

            if (!TryGetVisibleBounds(root, out Bounds afterBounds)) return false;
            afterMinY = afterBounds.min.y;
            return true;
        }

        private static bool TryGetVisibleBounds(GameObject root, out Bounds bounds)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bool found = false;
            bounds = default;
            for (var i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled) continue;
                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return found;
        }
    }
}
