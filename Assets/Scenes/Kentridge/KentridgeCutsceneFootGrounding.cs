using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
        private readonly Dictionary<GameObject, Transform> _visualOffsets = new Dictionary<GameObject, Transform>();

#if DEVELOPMENT_BUILD
        [Serializable]
        private sealed class ReplayDimensions
        {
            public int screenWidth;
            public int screenHeight;
        }

        private bool _verificationCaptureStarted;
#endif

        private void LateUpdate()
        {
            if (!gameObject.scene.IsValid() || !gameObject.scene.isLoaded) return;

            GameObject[] roots = gameObject.scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                GameObject root = roots[i];
                if (root == null || root == gameObject || !root.activeInHierarchy) continue;
                if (root.GetComponentInChildren<Animator>(true) == null) continue;

                bool createdOffset = false;
                if (!_visualOffsets.TryGetValue(root, out Transform offset) || offset == null)
                {
                    if (!TryCreateVisualOffset(root, out offset)) continue;
                    _visualOffsets[root] = offset;
                    createdOffset = true;
                }

                if (!TryAlignVisibleFeet(root, offset, out float correction, out float before, out float after))
                    continue;

                if (createdOffset)
                {
                    Debug.Log(
                        "KENTRIDGE_FOOT_ALIGNMENT actor=" + root.name
                        + " stageY=" + root.transform.position.y.ToString("F3")
                        + " beforeMinY=" + before.ToString("F3")
                        + " correction=" + correction.ToString("F3")
                        + " afterMinY=" + after.ToString("F3"));
                }
            }

#if DEVELOPMENT_BUILD
            TryBeginCleanReplayVerification(roots);
#endif
        }

        private static bool TryCreateVisualOffset(GameObject root, out Transform offset)
        {
            offset = null;
            if (!TryGetVisibleBounds(root, out _)) return false;

            int childCount = root.transform.childCount;
            var originalChildren = new Transform[childCount];
            for (var i = 0; i < childCount; i++)
                originalChildren[i] = root.transform.GetChild(i);

            var offsetObject = new GameObject("Kentridge Visual Foot Offset");
            offset = offsetObject.transform;
            offset.SetParent(root.transform, false);
            for (var i = 0; i < originalChildren.Length; i++)
                originalChildren[i].SetParent(offset, true);

            return true;
        }

        private static bool TryAlignVisibleFeet(
            GameObject root,
            Transform offset,
            out float correction,
            out float beforeMinY,
            out float afterMinY)
        {
            correction = 0f;
            beforeMinY = 0f;
            afterMinY = 0f;

            if (offset == null || !TryGetVisibleBounds(root, out Bounds beforeBounds)) return false;
            beforeMinY = beforeBounds.min.y;
            correction = root.transform.position.y - beforeMinY;

            if (Mathf.Abs(correction) <= AlignmentEpsilonMetres)
            {
                afterMinY = beforeMinY;
                return true;
            }

            float scaleY = root.transform.lossyScale.y;
            if (Mathf.Abs(scaleY) <= 1e-6f) return false;

            offset.localPosition += Vector3.up * (correction / scaleY);

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
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
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

#if DEVELOPMENT_BUILD
        private void TryBeginCleanReplayVerification(GameObject[] roots)
        {
            if (_verificationCaptureStarted) return;
            if (!TryReadArgument("-voxel-scene-issue", out string issuePath)) return;
            if (!TryReadArgument("-voxel-screenshot-dir", out string screenshotDirectory)) return;

            var slice = GetComponent<KentridgePlayableSlice>();
            var presentation = GetComponent<KentridgeOpeningPresentation>();
            if (slice == null || presentation == null || !slice.OpeningCutsceneStarted) return;
            if (!presentation.OpeningOverheadActive || presentation.FadeAlpha > 0.001f) return;
            if (!HasVisibleOpeningActor(roots, "Weldon")
                || !HasVisibleOpeningActor(roots, "Madeline")
                || !HasVisibleOpeningActor(roots, "Steven"))
                return;

            ReplayDimensions dimensions;
            try
            {
                dimensions = JsonUtility.FromJson<ReplayDimensions>(File.ReadAllText(issuePath));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return;
            }

            if (dimensions == null || dimensions.screenWidth <= 0 || dimensions.screenHeight <= 0) return;

            _verificationCaptureStarted = true;
            StartCoroutine(CaptureCleanReplayVerification(
                screenshotDirectory,
                dimensions.screenWidth,
                dimensions.screenHeight));
        }

        private static bool HasVisibleOpeningActor(GameObject[] roots, string actorName)
        {
            for (var i = 0; i < roots.Length; i++)
            {
                GameObject root = roots[i];
                if (root != null
                    && root.activeInHierarchy
                    && string.Equals(root.name, actorName, StringComparison.OrdinalIgnoreCase)
                    && TryGetVisibleBounds(root, out _))
                    return true;
            }
            return false;
        }

        private IEnumerator CaptureCleanReplayVerification(string screenshotDirectory, int width, int height)
        {
            // Let the current LateUpdate foot reconciliation and replay-camera freeze reach the
            // rendered frame, then render only the gameplay camera. Camera.Render excludes OnGUI
            // replay/dialogue controls and the development-player watermark by construction.
            yield return new WaitForEndOfFrame();

            Camera camera = GetComponent<Camera>();
            if (camera == null) yield break;

            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture target = null;
            Texture2D image = null;
            try
            {
                Directory.CreateDirectory(screenshotDirectory);
                target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
                camera.targetTexture = target;
                camera.Render();

                RenderTexture.active = target;
                image = new Texture2D(width, height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                image.Apply(false, false);

                string path = Path.Combine(
                    screenshotDirectory,
                    "zzzz-kentridge-grounding-verification.png");
                File.WriteAllBytes(path, image.EncodeToPNG());
                Debug.Log(
                    "KENTRIDGE_CLEAN_VERIFICATION path=" + path
                    + " width=" + width
                    + " height=" + height);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                if (image != null) Destroy(image);
                if (target != null) RenderTexture.ReleaseTemporary(target);
            }
        }

        private static bool TryReadArgument(string name, out string value)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (var i = 0; i + 1 < args.Length; i++)
            {
                if (!string.Equals(args[i], name, StringComparison.Ordinal)) continue;
                value = args[i + 1];
                return !string.IsNullOrEmpty(value);
            }

            value = string.Empty;
            return false;
        }
#endif
    }
}
