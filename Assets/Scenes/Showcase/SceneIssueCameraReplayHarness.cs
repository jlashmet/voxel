using System;
using UnityEngine;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Pins a standalone showcase player to a captured SceneIssue camera pose when a temporary
    /// Resources/SceneIssueCameraPose.json fixture is present. Normal players are unaffected.
    ///
    /// The resource is intentionally not checked in with a particular issue. Visual-verification
    /// CI branches create it from the capture being reviewed, so the same production player path
    /// can replay any saved camera without adding issue-specific scene code.
    /// </summary>
    public static class SceneIssueCameraReplayHarness
    {
        private const string ResourceName = "SceneIssueCameraPose";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            TextAsset asset = Resources.Load<TextAsset>(ResourceName);
            if (asset == null) return;

            PoseFixture fixture;
            try
            {
                fixture = JsonUtility.FromJson<PoseFixture>(asset.text);
            }
            catch (Exception error)
            {
                Debug.LogError($"SCENEISSUE camera fixture could not be parsed: {error.Message}");
                return;
            }

            if (fixture == null || string.IsNullOrEmpty(fixture.hierarchyPath))
            {
                Debug.LogError("SCENEISSUE camera fixture is missing hierarchyPath.");
                return;
            }

            var root = new GameObject("Scene Issue Camera Replay Harness")
            {
                hideFlags = HideFlags.DontSave
            };
            var replay = root.AddComponent<Replay>();
            replay.Fixture = fixture;
            UnityEngine.Object.DontDestroyOnLoad(root);
            Debug.Log($"SCENEISSUE camera replay armed for {fixture.hierarchyPath}");
        }

        [Serializable]
        private sealed class PoseFixture
        {
            public string hierarchyPath;
            public Vector3 position;
            public Quaternion rotation;
            public float fieldOfView = 70f;
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
