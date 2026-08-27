#if DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MountingForce.DeveloperTools
{
    /// <summary>
    /// Development-player proof for command-line SceneIssue replay. This does not move the camera;
    /// it verifies that SceneIssueCapture reached the recorded pose and, when the capture runner
    /// explicitly requests it, releases that same replay after a bounded real-time delay.
    /// </summary>
    internal static class SceneIssueReplayVerification
    {
        private const string ReplayArgument = "-voxel-scene-issue";
        private const string ReleaseAfterArgument = "-voxel-scene-issue-release-after";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            string path = ReplayPath();
            if (string.IsNullOrEmpty(path))
                return;

            var host = new GameObject("Scene Issue Replay Verification")
            {
                hideFlags = HideFlags.DontSave
            };
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<Verifier>().IssuePath = path;
        }

        private static string ReplayPath()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (string.Equals(args[i], ReplayArgument, StringComparison.Ordinal))
                    return args[i + 1];
            return string.Empty;
        }

        private static float ReleaseAfterSeconds()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
            {
                if (!string.Equals(args[i], ReleaseAfterArgument, StringComparison.Ordinal))
                    continue;

                if (float.TryParse(
                        args[i + 1],
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out float seconds) && seconds > 0f)
                    return seconds;

                Debug.LogWarning($"Scene issue replay ignored invalid {ReleaseAfterArgument} value '{args[i + 1]}'.");
                return 0f;
            }

            return 0f;
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

                    float releaseAfter = ReleaseAfterSeconds();
                    if (releaseAfter > 0f)
                    {
                        float remaining = releaseAfter - Time.realtimeSinceStartup;
                        if (remaining > 0f)
                            yield return new WaitForSecondsRealtime(remaining);

                        SceneIssueCapture capture = UnityEngine.Object.FindFirstObjectByType<SceneIssueCapture>();
                        if (capture == null)
                        {
                            Debug.LogWarning("Scene issue replay could not find SceneIssueCapture to release.");
                            yield break;
                        }

                        // Reuse the capture tool's existing Release camera transition so replay mode,
                        // frozen pose state, and overlay state are cleared exactly as in interactive replay.
                        capture.SendMessage("ReleaseReplayCamera", SendMessageOptions.DontRequireReceiver);
                        Debug.Log(
                            $"Scene issue replay released through SceneIssueCapture after {releaseAfter:0.###}s.");
                    }

                    yield break;
                }

                Debug.LogWarning("Scene issue replay did not reach the recorded frozen camera pose.");
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