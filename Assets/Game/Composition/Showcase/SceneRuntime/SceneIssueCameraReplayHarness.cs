using System;
using System.IO;
using UnityEngine;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Replays SceneIssue camera evidence in a standalone player. Ordinary captured issues keep
    /// their historical single pinned pose. Architecture/world-authoring issues may opt into a
    /// camera-sequence replay using explicit replayStages so one exact built-player run can show a
    /// progression through spatially separated evidence without pretending those stages were user captures.
    /// </summary>
    public static class SceneIssueCameraReplayHarness
    {
        private const string ResourceName = "SceneIssueCameraPose";
        private const string ReplayArgument = "-voxel-scene-issue";
        private const string CameraSequenceMode = "camera-sequence";

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
            Debug.Log($"SCENEISSUE camera replay armed for {fixture.hierarchyPath}" +
                      (fixture.stages != null ? $" stages={fixture.stages.Length}" : string.Empty));
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
                if (record != null &&
                    string.Equals(record.replayMode, CameraSequenceMode, StringComparison.OrdinalIgnoreCase))
                    return BuildSequenceFixture(record);

                CameraSnapshot camera = record?.captures != null && record.captures.Length > 0
                    ? record.captures[0]?.camera
                    : record?.camera;
                if (camera == null || string.IsNullOrEmpty(camera.hierarchyPath))
                {
                    Debug.LogError("SCENEISSUE issue.json has no replayable camera snapshot.");
                    return null;
                }

                return FromCamera(camera, record.replayAction);
            }
            catch (Exception error)
            {
                Debug.LogError($"SCENEISSUE issue.json could not be parsed: {error.Message}");
                return null;
            }
        }

        private static PoseFixture BuildSequenceFixture(IssueRecord record)
        {
            if (record.replayStages == null || record.replayStages.Length < 2)
            {
                Debug.LogError("SCENEISSUE camera-sequence replay requires at least two replayStages.");
                return null;
            }

            var stages = new PoseStage[record.replayStages.Length];
            string hierarchy = null;
            for (int i = 0; i < record.replayStages.Length; i++)
            {
                IssueFrame frame = record.replayStages[i];
                CameraSnapshot camera = frame?.camera;
                if (camera == null || string.IsNullOrEmpty(camera.hierarchyPath))
                {
                    Debug.LogError($"SCENEISSUE replayStages[{i}] has no replayable camera snapshot.");
                    return null;
                }
                if (hierarchy == null) hierarchy = camera.hierarchyPath;
                if (!string.Equals(hierarchy, camera.hierarchyPath, StringComparison.Ordinal))
                {
                    Debug.LogError("SCENEISSUE camera-sequence stages must target one camera hierarchy path.");
                    return null;
                }

                stages[i] = new PoseStage
                {
                    position = camera.position,
                    rotation = camera.rotation,
                    fieldOfView = camera.fieldOfView,
                    orthographic = camera.orthographic,
                    orthographicSize = camera.orthographicSize,
                    nearClipPlane = camera.nearClipPlane,
                    farClipPlane = camera.farClipPlane,
                    travelSeconds = Mathf.Max(0.1f, frame.travelSeconds),
                    holdSeconds = Mathf.Max(0f, frame.holdSeconds),
                    label = string.IsNullOrEmpty(frame.label) ? $"stage-{i}" : frame.label,
                };
            }

            PoseStage first = stages[0];
            return new PoseFixture
            {
                hierarchyPath = hierarchy,
                position = first.position,
                rotation = first.rotation,
                fieldOfView = first.fieldOfView,
                orthographic = first.orthographic,
                orthographicSize = first.orthographicSize,
                nearClipPlane = first.nearClipPlane,
                farClipPlane = first.farClipPlane,
                replayMode = CameraSequenceMode,
                stages = stages,
            };
        }

        private static PoseFixture FromCamera(CameraSnapshot camera, string replayAction) =>
            new PoseFixture
            {
                hierarchyPath = camera.hierarchyPath,
                position = camera.position,
                rotation = camera.rotation,
                fieldOfView = camera.fieldOfView,
                orthographic = camera.orthographic,
                orthographicSize = camera.orthographicSize,
                nearClipPlane = camera.nearClipPlane,
                farClipPlane = camera.farClipPlane,
                replayAction = replayAction,
            };

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
            public IssueFrame[] replayStages;
            public CameraSnapshot camera;
            public string replayAction;
            public string replayMode;
        }

        [Serializable]
        private sealed class IssueFrame
        {
            public CameraSnapshot camera;
            public float travelSeconds = 1f;
            public float holdSeconds;
            public string label;
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
        private sealed class PoseStage
        {
            public Vector3 position;
            public Quaternion rotation;
            public float fieldOfView = 70f;
            public bool orthographic;
            public float orthographicSize = 5f;
            public float nearClipPlane = 0.05f;
            public float farClipPlane = 1000f;
            public float travelSeconds = 1f;
            public float holdSeconds;
            public string label;
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
            public string replayAction;
            public string replayMode;
            public PoseStage[] stages;
        }

        [DefaultExecutionOrder(10000)]
        private sealed class Replay : MonoBehaviour
        {
            internal PoseFixture Fixture;
            private Camera _camera;
            private bool _reported;
            private bool _actionComplete;
            private bool _sequenceStarted;
            private bool _sequenceHolding;
            private bool _sequenceCompleteReported;
            private int _sequenceStageIndex;
            private float _sequencePhaseSeconds;
            private Vector3 _sequenceStartPosition;
            private Quaternion _sequenceStartRotation;

            private void LateUpdate()
            {
                if (_camera == null)
                    _camera = FindCamera(Fixture.hierarchyPath);
                if (_camera == null) return;

                if (string.Equals(Fixture.replayMode, CameraSequenceMode, StringComparison.OrdinalIgnoreCase))
                {
                    ReplayCameraSequence();
                    return;
                }

                if (!_actionComplete)
                    TryReplayAction();

                Transform transform = _camera.transform;
                transform.SetPositionAndRotation(Fixture.position, Fixture.rotation);
                ApplyLens(Fixture.fieldOfView, Fixture.orthographic, Fixture.orthographicSize,
                    Fixture.nearClipPlane, Fixture.farClipPlane);

                if (_reported) return;
                _reported = true;
                Debug.Log($"SCENEISSUE camera pinned at {Fixture.position} fov={_camera.fieldOfView:0.###}");
            }

            private void ReplayCameraSequence()
            {
                PoseStage[] stages = Fixture.stages;
                if (stages == null || stages.Length == 0) return;
                Transform cameraTransform = _camera.transform;

                if (!_sequenceStarted)
                {
                    _sequenceStarted = true;
                    _sequenceHolding = true;
                    _sequenceStageIndex = 0;
                    _sequencePhaseSeconds = 0f;
                    ApplyStage(stages[0], cameraTransform);
                    Debug.Log($"SCENEISSUE sequence reached 1/{stages.Length} {stages[0].label} at {stages[0].position}");
                    return;
                }

                PoseStage stage = stages[_sequenceStageIndex];
                float dt = Time.unscaledDeltaTime;
                if (_sequenceHolding)
                {
                    ApplyStage(stage, cameraTransform);
                    _sequencePhaseSeconds += dt;
                    if (_sequencePhaseSeconds < stage.holdSeconds) return;
                    if (_sequenceStageIndex >= stages.Length - 1)
                    {
                        if (!_sequenceCompleteReported)
                        {
                            _sequenceCompleteReported = true;
                            Debug.Log($"SCENEISSUE camera sequence complete stages={stages.Length}");
                        }
                        return;
                    }

                    _sequenceStageIndex++;
                    _sequenceHolding = false;
                    _sequencePhaseSeconds = 0f;
                    _sequenceStartPosition = cameraTransform.position;
                    _sequenceStartRotation = cameraTransform.rotation;
                    return;
                }

                stage = stages[_sequenceStageIndex];
                _sequencePhaseSeconds += dt;
                float duration = Mathf.Max(0.1f, stage.travelSeconds);
                float t = Mathf.Clamp01(_sequencePhaseSeconds / duration);
                cameraTransform.position = Vector3.Lerp(_sequenceStartPosition, stage.position, t);
                cameraTransform.rotation = Quaternion.Slerp(_sequenceStartRotation, stage.rotation, t);
                ApplyLens(stage.fieldOfView, stage.orthographic, stage.orthographicSize,
                    stage.nearClipPlane, stage.farClipPlane);
                if (t < 1f) return;

                _sequenceHolding = true;
                _sequencePhaseSeconds = 0f;
                Debug.Log($"SCENEISSUE sequence reached {_sequenceStageIndex + 1}/{stages.Length} " +
                          $"{stage.label} at {stage.position}");
            }

            private void ApplyStage(PoseStage stage, Transform cameraTransform)
            {
                cameraTransform.SetPositionAndRotation(stage.position, stage.rotation);
                ApplyLens(stage.fieldOfView, stage.orthographic, stage.orthographicSize,
                    stage.nearClipPlane, stage.farClipPlane);
            }

            private void ApplyLens(
                float fieldOfView,
                bool orthographic,
                float orthographicSize,
                float nearClipPlane,
                float farClipPlane)
            {
                if (fieldOfView > 0f) _camera.fieldOfView = fieldOfView;
                _camera.orthographic = orthographic;
                if (orthographicSize > 0f) _camera.orthographicSize = orthographicSize;
                if (nearClipPlane > 0f) _camera.nearClipPlane = nearClipPlane;
                if (farClipPlane > nearClipPlane) _camera.farClipPlane = farClipPlane;
            }

            private void TryReplayAction()
            {
                if (string.IsNullOrEmpty(Fixture.replayAction))
                {
                    _actionComplete = true;
                    return;
                }

                if (!string.Equals(Fixture.replayAction, "interact", StringComparison.OrdinalIgnoreCase))
                {
                    Debug.LogError($"SCENEISSUE unsupported replayAction '{Fixture.replayAction}'.");
                    _actionComplete = true;
                    return;
                }

                VoxelShowcase showcase = _camera.GetComponent<VoxelShowcase>();
                if (showcase == null)
                    showcase = UnityEngine.Object.FindFirstObjectByType<VoxelShowcase>();
                if (showcase == null)
                    return;

                // The camera replay pins only presentation. Synchronize the production character
                // motor to the captured position before invoking the same operation bound to E;
                // otherwise replay would leave proximity at the spawn and could only prove the
                // pre-interaction closed state.
                showcase.TeleportTo(Fixture.position);
                if (!showcase.TryInteract())
                    return;

                _actionComplete = true;
                Debug.Log("SCENEISSUE replayAction interact accepted by VoxelShowcase.TryInteract.");
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
