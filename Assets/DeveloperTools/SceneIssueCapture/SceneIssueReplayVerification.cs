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
    /// it only verifies that SceneIssueCapture has frozen the active camera at the recorded pose.
    /// </summary>
    internal static class SceneIssueReplayVerification
    {
        private const string ReplayArgument = "-voxel-scene-issue";

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
