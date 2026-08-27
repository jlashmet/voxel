using System;
using System.IO;
using UnityEngine;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Pins a standalone showcase player to a captured SceneIssue camera pose. A temporary
    /// Resources/SceneIssueCameraPose.json fixture remains supported for older verification jobs;
    /// current jobs can instead pass the canonical issue.json via -voxel-scene-issue so a normal
    /// non-development player can replay the saved view without debug UI or a development watermark.
    /// </summary>
    public static class SceneIssueCameraReplayHarness
    {
        private const string ResourceName = "SceneIssueCameraPose";
        private const string ReplayArgument = "-voxel-scene-issue";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            PoseFixture fixture = LoadFixture();
            if (fixture == null || string.IsNullOrEmpty(fixture.hierarchyPath))
                return;

            var root = new GameObject("Scene Issue Camera Replay Harness")
            {
                hideFlags = HideFlags.DontSave
            };
            var replay = root.AddComponent<Replay>();
            replay.Fixture = fixture;
            UnityEngine.Object.DontDestroyOnLoad(root);
            Debug.Log($"SCENEISSUE camera replay armed for {fixture.hierarchyPath}");
        }

        private static PoseFixture LoadFixture()
        {
            TextAsset asset = Resources.Load<TextAsset>(ResourceName);
            if (asset != null)
            {
                try
                {
                    PoseFixture fixture = JsonUtility.FromJson<PoseFixture>(asset.text);
                    if (fixture != null && !string.IsNullOrEmpty(fixture.hierarchyPath))
                        return fixture;
                }
                catch (Exception error)
                {
                    Debug.LogError($"SCENEISSUE camera fixture could not be parsed: {error.Message}");
                    return null;
                }
            }

            string issuePath = Argument(ReplayArgument);
            if (string.IsNullOrEmpty(issuePath))
                return null;

            try
            {
                IssueRecord record = JsonUtility.FromJson<IssueRecord>(File.ReadAllText(issuePath));
                CameraSnapshot camera = record?.captures != null && record.captures.Length > 0
                    ? record.captures[0]?.camera
                    : record?.camera;
                if (camera == null || string.IsNullOrEmpty(camera.hierarchyPath))
                {
                    Debug.LogError("SCENEISSUE issue.json has no replayable camera snapshot.");
                    return null;
                }

                return new PoseFixture
                {
                    hierarchyPath = camera.hierarchyPath,
                    position = camera.position,
                    rotation = camera.rotation,
                    fieldOfView = camera.fieldOfView,
                    orthographic = camera.orthographic,
                    orthographicSize = camera.orthographicSize,
                    nearClipPlane = camera.nearClipPlane,
                    farClipPlane = camera.farClipPlane
                };
            }
            catch (Exception error)
            {
                Debug.LogError($"SCENEISSUE issue.json could not be parsed: {error.Message}");
                return null;
            }
        }

        private static string Argument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (string.Equals(args[i], name, StringComparison.Ordinal))
                    return args[i + 1];
            return null;
        }

        [Serializable]
        private sealed class IssueRecord
        {
            public IssueFrame[] captures;
            public CameraSnapshot camera;
        }

        [Serializable]
        private sealed class IssueFrame
        {
            public CameraSnapshot camera;
        }

        [Serializable]
        private sealed class CameraSnapshot
        {
            public string hierarchyPath;
            public Vector3 position;
            public Quaternion rotation;
            public float fieldOfView = 70f;
            public bool orthographic;
            public float orthographicSize = 5f;
            public float nearClipPlane = 0.05f;
            public float farClipPlane = 1000f;
        }

        [Serializable]
        private sealed class PoseFixture
        {
            public string hierarchyPath;
            public Vector3 position;
            public Quaternion rotation;
            public float fieldOfView = 70f;
            public bool orthographic;
            public float orthographicSize = 5f;
            public float nearClipPlane = 0.05f;
            public float farClipPlane = 1000f;
        }

        [DefaultExecutionOrder(10000)]
        private sealed class Replay : MonoBehaviour
        {
            internal PoseFixture Fixture;
            private Camera _camera;
            private bool _reported;

            private void LateUpdate()
            {
                if (_camera == null)
                    _camera = FindCamera(Fixture.hierarchyPath);
                if (_camera == null) return;

                Transform transform = _camera.transform;
                transform.SetPositionAndRotation(Fixture.position, Fixture.rotation);
                if (Fixture.fieldOfView > 0f)
                    _camera.fieldOfView = Fixture.fieldOfView;
                _camera.orthographic = Fixture.orthographic;
                if (Fixture.orthographicSize > 0f)
                    _camera.orthographicSize = Fixture.orthographicSize;
                if (Fixture.nearClipPlane > 0f)
                    _camera.nearClipPlane = Fixture.nearClipPlane;
                if (Fixture.farClipPlane > Fixture.nearClipPlane)
                    _camera.farClipPlane = Fixture.farClipPlane;

                if (_reported) return;
                _reported = true;
                Debug.Log($"SCENEISSUE camera pinned at {Fixture.position} fov={_camera.fieldOfView:0.###}");
            }

            private static Camera FindCamera(string hierarchyPath)
            {
                Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);
                for (int i = 0; i < cameras.Length; i++)
                {
                    Camera candidate = cameras[i];
                    if (candidate == null) continue;
                    if (HierarchyPath(candidate.transform) == hierarchyPath)
                        return candidate;
                }

                GameObject named = GameObject.Find(hierarchyPath);
                if (named != null && named.TryGetComponent(out Camera exact)) return exact;
                return null;
            }

            private static string HierarchyPath(Transform transform)
            {
                string path = transform.name;
                while (transform.parent != null)
                {
                    transform = transform.parent;
                    path = transform.name + "/" + path;
                }
                return path;
            }
        }
    }
}
