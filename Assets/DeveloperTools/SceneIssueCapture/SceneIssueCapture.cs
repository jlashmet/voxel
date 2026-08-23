#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MountingForce.DeveloperTools
{
    [Serializable]
    public sealed class SceneIssueCaptureRecord
    {
        public int formatVersion = 1;
        public string id;
        public string capturedUtc;
        public string note;
        public string screenshot;
        public string unityVersion;
        public string platform;
        public string sceneName;
        public string scenePath;
        public int sceneBuildIndex;
        public int frameCount;
        public float timeSinceLevelLoad;
        public int screenWidth;
        public int screenHeight;
        public SceneIssueTransformSnapshot poseAnchor;
        public SceneIssueCameraSnapshot camera;
    }

    [Serializable]
    public sealed class SceneIssueTransformSnapshot
    {
        public string hierarchyPath;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 localScale;
    }

    [Serializable]
    public sealed class SceneIssueCameraSnapshot
    {
        public string hierarchyPath;
        public Vector3 position;
        public Quaternion rotation;
        public float fieldOfView;
        public bool orthographic;
        public float orthographicSize;
        public float nearClipPlane;
        public float farClipPlane;
    }

    /// <summary>
    /// Development-only in-game issue capture overlay. It records the rendered viewpoint before
    /// showing annotation UI, then stores a screenshot and a replayable scene/camera fixture.
    /// </summary>
    public sealed class SceneIssueCapture : MonoBehaviour
    {
        public const string ReplayRequestEditorPrefsKey = "MountingForce.SceneIssueCapture.ReplayRequest";

        private const KeyCode CaptureKey = KeyCode.F8;
        private const string CaptureDirectoryName = "SceneIssues";
        private const string ScreenshotFileName = "screenshot.png";
        private const string RecordFileName = "issue.json";

        private bool _captureInProgress;
        private bool _annotationVisible;
        private bool _overlayHidden;
        private bool _replayMode;
        private bool _annotationFocused;
        private string _note = string.Empty;
        private string _toast = string.Empty;
        private float _toastUntil;
        private byte[] _pendingPng;
        private SceneIssueCaptureRecord _pendingRecord;
        private SceneIssueCaptureRecord _replayRecord;
        private Camera _frozenCamera;
        private SceneIssueTransformSnapshot _frozenAnchor;
        private SceneIssueCameraSnapshot _frozenView;
        private float _previousTimeScale;
        private CursorLockMode _previousCursorLock;
        private bool _previousCursorVisible;
        private bool _annotationOwnsPauseState;
        private GUIStyle _titleStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _textAreaStyle;
        private GUIStyle _smallStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<SceneIssueCapture>() != null)
                return;

            var host = new GameObject("Scene Issue Capture");
            DontDestroyOnLoad(host);
            host.AddComponent<SceneIssueCapture>();
        }

        public static string GetCaptureRootPath()
        {
#if UNITY_EDITOR
            var assetsDirectory = new DirectoryInfo(Application.dataPath);
            var projectDirectory = assetsDirectory.Parent;
            if (projectDirectory != null)
                return Path.Combine(projectDirectory.FullName, CaptureDirectoryName);
#endif
            return Path.Combine(Application.persistentDataPath, CaptureDirectoryName);
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
#if UNITY_EDITOR
            StartCoroutine(ConsumeReplayRequest());
#endif
        }

        private void LateUpdate()
        {
            if ((_captureInProgress || _replayMode) && _frozenCamera != null)
                ApplyFrozenPose();
        }

        private void OnGUI()
        {
            if (_overlayHidden)
                return;

            EnsureStyles();
            HandleCaptureHotkey();

            if (_annotationVisible)
            {
                DrawAnnotationDialog();
                return;
            }

            if (_replayMode)
                DrawReplayBanner();
            else if (!_captureInProgress)
                DrawCaptureButton();

            if (!string.IsNullOrEmpty(_toast) && Time.realtimeSinceStartup < _toastUntil)
                DrawToast();
        }

        private void HandleCaptureHotkey()
        {
            if (_annotationVisible || _captureInProgress || _replayMode)
                return;

            var current = Event.current;
            if (current == null || current.type != EventType.KeyDown || current.keyCode != CaptureKey)
                return;

            current.Use();
            BeginCapture();
        }

        private void DrawCaptureButton()
        {
            const float width = 150f;
            const float height = 38f;
            var rect = new Rect(Mathf.Max(10f, Screen.width - width - 12f), 12f, width, height);
            if (GUI.Button(rect, "Flag issue  [F8]"))
                BeginCapture();
        }

        private void BeginCapture()
        {
            if (_captureInProgress || _annotationVisible || _replayMode)
                return;

            Camera camera = ResolveActiveCamera(null);
            if (camera == null)
            {
                ShowToast("No active game camera found; issue was not captured.");
                return;
            }

            _pendingRecord = BuildRecord(camera);
            _frozenCamera = camera;
            _frozenView = _pendingRecord.camera;
            _frozenAnchor = _pendingRecord.poseAnchor;
            _captureInProgress = true;
            PreserveAndPauseForAnnotation();
            StartCoroutine(CaptureRenderedFrame());
        }

        private IEnumerator CaptureRenderedFrame()
        {
            _overlayHidden = true;
            yield return new WaitForEndOfFrame();

            try
            {
                ApplyFrozenPose();
                Texture2D screenshot = ScreenCapture.CaptureScreenshotAsTexture();
                if (screenshot == null)
                    throw new InvalidOperationException("ScreenCapture returned no texture.");

                _pendingPng = screenshot.EncodeToPNG();
                Destroy(screenshot);
                if (_pendingPng == null || _pendingPng.Length == 0)
                    throw new InvalidOperationException("PNG encoding returned no data.");

                _note = string.Empty;
                _annotationFocused = false;
                _annotationVisible = true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                CancelPendingCapture("Capture failed; see the Unity Console for details.");
            }
            finally
            {
                _overlayHidden = false;
                _captureInProgress = false;
            }
        }

        private void DrawAnnotationDialog()
        {
            var dim = new Rect(0f, 0f, Screen.width, Screen.height);
            Color oldColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.Box(dim, GUIContent.none);
            GUI.color = oldColor;

            float width = Mathf.Clamp(Screen.width - 40f, 300f, 680f);
            float height = Mathf.Clamp(Screen.height - 80f, 260f, 390f);
            var area = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);

            GUILayout.BeginArea(area, GUI.skin.window);
            GUILayout.Label("Flag scene issue", _titleStyle);
            GUILayout.Space(4f);
            GUILayout.Label(
                "The clean screenshot and exact camera pose are already captured. Describe what is wrong in this view.",
                _bodyStyle);
            GUILayout.Space(8f);

            GUI.SetNextControlName("SceneIssueDescription");
            _note = GUILayout.TextArea(_note ?? string.Empty, _textAreaStyle, GUILayout.ExpandHeight(true));
            if (!_annotationFocused && Event.current.type == EventType.Repaint)
            {
                GUI.FocusControl("SceneIssueDescription");
                _annotationFocused = true;
            }

            GUILayout.Space(10f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Cancel", GUILayout.Height(34f)))
            {
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
                CancelPendingCapture(null);
                return;
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Save issue", GUILayout.Width(150f), GUILayout.Height(34f)))
            {
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
                SavePendingCapture();
                return;
            }

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void SavePendingCapture()
        {
            if (_pendingRecord == null || _pendingPng == null)
            {
                CancelPendingCapture("Nothing was captured.");
                return;
            }

            try
            {
                string root = GetCaptureRootPath();
                string captureDirectory = Path.Combine(root, _pendingRecord.id);
                Directory.CreateDirectory(captureDirectory);

                _pendingRecord.note = (_note ?? string.Empty).Trim();
                _pendingRecord.screenshot = ScreenshotFileName;

                File.WriteAllBytes(Path.Combine(captureDirectory, ScreenshotFileName), _pendingPng);
                File.WriteAllText(
                    Path.Combine(captureDirectory, RecordFileName),
                    JsonUtility.ToJson(_pendingRecord, true),
                    new UTF8Encoding(false));

                Debug.Log($"Scene issue captured: {captureDirectory}");
                ShowToast($"Issue saved: {Path.GetFileName(captureDirectory)}");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                ShowToast("Could not save issue capture; see the Unity Console.");
            }
            finally
            {
                ClearPendingCapture();
                RestoreAfterAnnotation();
            }
        }

        private void CancelPendingCapture(string toast)
        {
            ClearPendingCapture();
            RestoreAfterAnnotation();
            if (!string.IsNullOrEmpty(toast))
                ShowToast(toast);
        }

        private void ClearPendingCapture()
        {
            _annotationVisible = false;
            _annotationFocused = false;
            _captureInProgress = false;
            _pendingPng = null;
            _pendingRecord = null;
            _note = string.Empty;
            _frozenCamera = null;
            _frozenAnchor = null;
            _frozenView = null;
        }

        private void PreserveAndPauseForAnnotation()
        {
            if (_annotationOwnsPauseState)
                return;

            _previousTimeScale = Time.timeScale;
            _previousCursorLock = Cursor.lockState;
            _previousCursorVisible = Cursor.visible;
            _annotationOwnsPauseState = true;

            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void RestoreAfterAnnotation()
        {
            if (!_annotationOwnsPauseState)
                return;

            Time.timeScale = _previousTimeScale;
            Cursor.lockState = _previousCursorLock;
            Cursor.visible = _previousCursorVisible;
            _annotationOwnsPauseState = false;
        }

        private SceneIssueCaptureRecord BuildRecord(Camera camera)
        {
            Scene scene = SceneManager.GetActiveScene();
            Transform poseAnchor = SelectPoseAnchor(camera.transform);
            string sceneSlug = SanitizeFileComponent(string.IsNullOrEmpty(scene.name) ? "UntitledScene" : scene.name);
            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);

            return new SceneIssueCaptureRecord
            {
                id = $"{timestamp}-{sceneSlug}",
                capturedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                note = string.Empty,
                screenshot = ScreenshotFileName,
                unityVersion = Application.unityVersion,
                platform = Application.platform.ToString(),
                sceneName = scene.name,
                scenePath = scene.path,
                sceneBuildIndex = scene.buildIndex,
                frameCount = Time.frameCount,
                timeSinceLevelLoad = Time.timeSinceLevelLoad,
                screenWidth = Screen.width,
                screenHeight = Screen.height,
                poseAnchor = CaptureTransform(poseAnchor),
                camera = CaptureCamera(camera)
            };
        }

        private static Transform SelectPoseAnchor(Transform cameraTransform)
        {
            // Prefer the nearest ancestor that actually looks like a movement/controller object.
            // Falling back to the camera itself is intentionally conservative: blindly moving
            // transform.root can move an entire scene when cameras live under a composition root.
            for (Transform current = cameraTransform; current != null; current = current.parent)
            {
                if (current.GetComponent<CharacterController>() != null ||
                    current.GetComponent<Rigidbody>() != null ||
                    HasMovementDriver(current))
                    return current;
            }

            return cameraTransform;
        }

        private static bool HasMovementDriver(Transform transform)
        {
            MonoBehaviour[] behaviours = transform.GetComponents<MonoBehaviour>();
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour == null)
                    continue;

                string typeName = behaviour.GetType().Name;
                if (typeName.IndexOf("Player", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    typeName.IndexOf("Character", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    typeName.IndexOf("Controller", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    typeName.IndexOf("Motor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    typeName.IndexOf("Showcase", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    typeName.IndexOf("CameraRig", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        private static Transform ResolveRecordedAnchor(Transform cameraTransform, SceneIssueTransformSnapshot snapshot)
        {
            if (cameraTransform == null || snapshot == null || string.IsNullOrEmpty(snapshot.hierarchyPath))
                return null;

            for (Transform current = cameraTransform; current != null; current = current.parent)
            {
                if (GetHierarchyPath(current) == snapshot.hierarchyPath)
                    return current;
            }

            return null;
        }

        private static SceneIssueTransformSnapshot CaptureTransform(Transform transform)
        {
            return new SceneIssueTransformSnapshot
            {
                hierarchyPath = GetHierarchyPath(transform),
                position = transform.position,
                rotation = transform.rotation,
                localScale = transform.localScale
            };
        }

        private static SceneIssueCameraSnapshot CaptureCamera(Camera camera)
        {
            return new SceneIssueCameraSnapshot
            {
                hierarchyPath = GetHierarchyPath(camera.transform),
                position = camera.transform.position,
                rotation = camera.transform.rotation,
                fieldOfView = camera.fieldOfView,
                orthographic = camera.orthographic,
                orthographicSize = camera.orthographicSize,
                nearClipPlane = camera.nearClipPlane,
                farClipPlane = camera.farClipPlane
            };
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

        private void ApplyFrozenPose()
        {
            if (_frozenCamera == null || _frozenView == null)
                return;

            Transform anchor = ResolveRecordedAnchor(_frozenCamera.transform, _frozenAnchor);
            if (_frozenAnchor != null && anchor != null)
            {
                anchor.SetPositionAndRotation(_frozenAnchor.position, _frozenAnchor.rotation);
                anchor.localScale = _frozenAnchor.localScale;
            }

            _frozenCamera.transform.SetPositionAndRotation(_frozenView.position, _frozenView.rotation);
            _frozenCamera.fieldOfView = _frozenView.fieldOfView;
            _frozenCamera.orthographic = _frozenView.orthographic;
            _frozenCamera.orthographicSize = _frozenView.orthographicSize;
            _frozenCamera.nearClipPlane = _frozenView.nearClipPlane;
            _frozenCamera.farClipPlane = _frozenView.farClipPlane;
        }

        private static Camera ResolveActiveCamera(SceneIssueCaptureRecord record)
        {
            Camera[] cameras = Camera.allCameras;
            if (record != null && record.camera != null && !string.IsNullOrEmpty(record.camera.hierarchyPath))
            {
                foreach (Camera candidate in cameras)
                {
                    if (candidate != null && GetHierarchyPath(candidate.transform) == record.camera.hierarchyPath)
                        return candidate;
                }
            }

            if (Camera.main != null)
                return Camera.main;

            Camera best = null;
            foreach (Camera candidate in cameras)
            {
                if (candidate == null || !candidate.enabled || !candidate.gameObject.activeInHierarchy)
                    continue;

                if (best == null || candidate.depth > best.depth)
                    best = candidate;
            }

            return best;
        }

        private static string SanitizeFileComponent(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "capture";

            char[] invalid = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(value.Length);
            foreach (char character in value)
            {
                bool isInvalid = false;
                foreach (char invalidCharacter in invalid)
                {
                    if (character != invalidCharacter)
                        continue;

                    isInvalid = true;
                    break;
                }

                builder.Append(isInvalid ? '-' : character);
            }

            return builder.ToString();
        }

        private void DrawReplayBanner()
        {
            float width = Mathf.Clamp(Screen.width - 40f, 320f, 620f);
            var area = new Rect((Screen.width - width) * 0.5f, 12f, width, 150f);
            GUILayout.BeginArea(area, GUI.skin.window);
            GUILayout.Label("Scene issue replay", _titleStyle);
            GUILayout.Label(_replayRecord != null ? _replayRecord.note : string.Empty, _bodyStyle);
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Camera is pinned to the recorded viewpoint.", _smallStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Release camera", GUILayout.Width(130f), GUILayout.Height(30f)))
                ReleaseReplayCamera();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void ReleaseReplayCamera()
        {
            _replayMode = false;
            _replayRecord = null;
            _frozenCamera = null;
            _frozenAnchor = null;
            _frozenView = null;
            ShowToast("Replay camera released at the captured location.");
        }

#if UNITY_EDITOR
        private IEnumerator ConsumeReplayRequest()
        {
            string path = UnityEditor.EditorPrefs.GetString(ReplayRequestEditorPrefsKey, string.Empty);
            if (string.IsNullOrEmpty(path))
                yield break;

            UnityEditor.EditorPrefs.DeleteKey(ReplayRequestEditorPrefsKey);

            SceneIssueCaptureRecord record;
            try
            {
                record = JsonUtility.FromJson<SceneIssueCaptureRecord>(File.ReadAllText(path));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                ShowToast("Could not read the requested scene issue capture.");
                yield break;
            }

            if (record == null)
            {
                ShowToast("The requested scene issue capture was empty.");
                yield break;
            }

            if (!string.Equals(SceneManager.GetActiveScene().path, record.scenePath, StringComparison.Ordinal))
            {
                ShowToast("Replay scene did not match the capture scene.");
                yield break;
            }

            Camera camera = null;
            for (int frame = 0; frame < 300 && camera == null; frame++)
            {
                camera = ResolveActiveCamera(record);
                if (camera == null)
                    yield return null;
            }

            if (camera == null)
            {
                ShowToast("Replay could not find an active camera in this scene.");
                yield break;
            }

            _replayRecord = record;
            _frozenCamera = camera;
            _frozenAnchor = record.poseAnchor;
            _frozenView = record.camera;
            _replayMode = true;
            ApplyFrozenPose();
            ShowToast("Replaying captured viewpoint.");
        }
#endif

        private void DrawToast()
        {
            float width = Mathf.Clamp(Screen.width - 40f, 300f, 560f);
            var area = new Rect((Screen.width - width) * 0.5f, Mathf.Max(10f, Screen.height - 72f), width, 52f);
            GUI.Box(area, _toast, _bodyStyle);
        }

        private void ShowToast(string message)
        {
            _toast = message ?? string.Empty;
            _toastUntil = Time.realtimeSinceStartup + 5f;
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null)
                return;

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };
            _bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                wordWrap = true
            };
            _smallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                wordWrap = true
            };
            _textAreaStyle = new GUIStyle(GUI.skin.textArea)
            {
                fontSize = 14,
                wordWrap = true
            };
        }
    }
}
#endif
