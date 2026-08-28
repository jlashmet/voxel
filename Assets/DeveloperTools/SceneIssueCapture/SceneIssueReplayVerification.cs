#if DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MountingForce.DeveloperTools
{
    /// <summary>
    /// Development-player proof for command-line SceneIssue replay. This does not move the camera;
    /// it verifies that SceneIssueCapture has frozen the active camera at the recorded pose and,
    /// when the screenshot runner is active, emits a clean camera-only verification frame.
    /// </summary>
    internal static class SceneIssueReplayVerification
    {
        private const string ReplayArgument = "-voxel-scene-issue";
        private const string ScreenshotDirectoryArgument = "-voxel-screenshot-dir";
        private const string CleanVerificationFileName = "verification-clean.png";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            string path = CommandLineValue(ReplayArgument);
            if (string.IsNullOrEmpty(path))
                return;

            var host = new GameObject("Scene Issue Replay Verification")
            {
                hideFlags = HideFlags.DontSave
            };
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<Verifier>().IssuePath = path;
        }

        private static string CommandLineValue(string argument)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (string.Equals(args[i], argument, StringComparison.Ordinal))
                    return args[i + 1];
            return string.Empty;
        }

        private sealed class Verifier : MonoBehaviour
        {
            public string IssuePath;

            private IEnumerator Start()
            {
                SceneIssueCaptureRecord record;
                try
                {
                    record = JsonUtility.FromJson<SceneIssueCaptureRecord>(File.ReadAllText(IssuePath));
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    yield break;
                }

                if (record == null || !string.Equals(
                        SceneManager.GetActiveScene().path, record.scenePath, StringComparison.Ordinal))
                    yield break;

                SceneIssueFrameCapture[] frames = record.captures;
                if (frames == null || frames.Length == 0 || frames[0]?.camera == null)
                    yield break;

                SceneIssueCameraSnapshot target = frames[0].camera;
                float deadline = Time.realtimeSinceStartup + 10f;
                while (Time.realtimeSinceStartup < deadline)
                {
                    yield return new WaitForEndOfFrame();
                    Camera camera = ResolveCamera(target.hierarchyPath);
                    if (camera == null)
                        continue;

                    if (Vector3.Distance(camera.transform.position, target.position) > 0.01f)
                        continue;
                    if (Quaternion.Angle(camera.transform.rotation, target.rotation) > 0.1f)
                        continue;
                    if (Mathf.Abs(camera.fieldOfView - target.fieldOfView) > 0.01f)
                        continue;

                    Debug.Log(
                        $"Replaying issue with {frames.Length} screenshot(s). Verified standalone frozen pose.");

                    string screenshotDirectory = CommandLineValue(ScreenshotDirectoryArgument);
                    if (!string.IsNullOrEmpty(screenshotDirectory))
                    {
                        yield return new WaitForSecondsRealtime(2f);
                        yield return CaptureCleanVerification(camera, frames[0], screenshotDirectory);
                    }
                    yield break;
                }

                Debug.LogWarning("Scene issue replay did not reach the recorded frozen camera pose.");
            }

            private static IEnumerator CaptureCleanVerification(
                Camera camera,
                SceneIssueFrameCapture frame,
                string screenshotDirectory)
            {
                int width = Mathf.Max(1, frame.screenWidth);
                int height = Mathf.Max(1, frame.screenHeight);
                string outputPath = Path.Combine(screenshotDirectory, CleanVerificationFileName);
                Directory.CreateDirectory(screenshotDirectory);

                RenderTexture previousTarget = camera.targetTexture;
                RenderTexture previousActive = RenderTexture.active;
                var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
                {
                    name = "Scene Issue Clean Verification",
                    hideFlags = HideFlags.DontSave
                };
                var pixels = new Texture2D(width, height, TextureFormat.RGB24, false)
                {
                    name = "Scene Issue Clean Verification Readback",
                    hideFlags = HideFlags.DontSave
                };

                Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(
                    FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                bool[] canvasStates = new bool[canvases.Length];

                try
                {
                    for (int i = 0; i < canvases.Length; i++)
                    {
                        canvasStates[i] = canvases[i] != null && canvases[i].enabled;
                        if (canvases[i] != null)
                            canvases[i].enabled = false;
                    }

                    camera.targetTexture = target;
                    RenderTexture.active = target;
                    camera.Render();
                    pixels.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                    pixels.Apply(false, false);
                    File.WriteAllBytes(outputPath, pixels.EncodeToPNG());
                    Debug.Log(
                        $"Scene issue clean verification captured: {outputPath} ({width}x{height}).");
                }
                finally
                {
                    camera.targetTexture = previousTarget;
                    RenderTexture.active = previousActive;
                    for (int i = 0; i < canvases.Length; i++)
                        if (canvases[i] != null)
                            canvases[i].enabled = canvasStates[i];
                    UnityEngine.Object.Destroy(target);
                    UnityEngine.Object.Destroy(pixels);
                }

                yield return null;
            }
        }

        private static Camera ResolveCamera(string hierarchyPath)
        {
            Camera[] cameras = Camera.allCameras;
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera camera = cameras[i];
                if (camera != null && string.Equals(
                        GetHierarchyPath(camera.transform), hierarchyPath, StringComparison.Ordinal))
                    return camera;
            }
            return Camera.main;
        }

        private static string GetHierarchyPath(Transform transform)
        {
            if (transform == null)
                return string.Empty;

            var builder = new StringBuilder(transform.name);
            Transform parent = transform.parent;
            while (parent != null)
            {
                builder.Insert(0, '/');
                builder.Insert(0, parent.name);
                parent = parent.parent;
            }
            return builder.ToString();
        }
    }
}
#endif