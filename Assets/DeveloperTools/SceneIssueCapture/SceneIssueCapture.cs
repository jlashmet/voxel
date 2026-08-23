#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
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
        public int formatVersion = 2;
        public string id;
        public string capturedUtc;
        public string note;
        public string status = "open";
        public string resolvedUtc;
        public string resolutionSummary;
        public string regressionTest;
        public string fixCommit;
        public string unityVersion;
        public string platform;
        public string sceneName;
        public string scenePath;
        public int sceneBuildIndex;
        public SceneIssueFrameCapture[] captures;

        // Version-1 compatibility. New captures mirror the first frame here so older tools and
        // hand-written fixtures continue to work while formatVersion 2 uses captures[].
        public string screenshot;
        public int frameCount;
        public float timeSinceLevelLoad;
        public int screenWidth;
        public int screenHeight;
        public SceneIssueTransformSnapshot poseAnchor;
        public SceneIssueCameraSnapshot camera;
    }

    [Serializable]
    public sealed class SceneIssueFrameCapture
    {
        public string screenshot;
        public string capturedUtc;
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
    /// Development-only in-game issue capture overlay. One issue can contain many clean rendered
    /// frames, each with its own exact camera/pose metadata. This is useful for flicker, popping,
    /// transient holes and other failures where no single screenshot explains the defect.
    /// </summary>
    public sealed class SceneIssueCapture : MonoBehaviour
    {
        public const string ReplayRequestEditorPrefsKey = "MountingForce.SceneIssueCapture.ReplayRequest";

        private const KeyCode CaptureKey = KeyCode.F8;
        private const string CaptureDirectoryName = "SceneIssues";
        private const string RecordFileName = "issue.json";

        private sealed class PendingFrame
        {
            public SceneIssueFrameCapture Snapshot;
            public byte[] Png;
        }

        private readonly List<PendingFrame> _pendingFrames = new();

        private bool _captureInProgress;
        private bool _sessionActive;
        private bool _annotationVisible;
        private bool _overlayHidden;
        private bool _replayMode;
        private bool _annotationFocused;
        private string _note = string.Empty;
        private string _toast = string.Empty;
        private float _toastUntil;
        private SceneIssueCaptureRecord _pendingRecord;
        private SceneIssueCaptureRecord _replayRecord;
        private SceneIssueFrameCapture[] _replayFrames;
        private int _replayIndex;
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
            if ((_annotationVisible || _replayMode) && _frozenCamera != null)
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
            else if (_sessionActive)
                DrawActiveSessionControls();
            else if (!_captureInProgress)
                DrawCaptureButton();

            if (!string.IsNullOrEmpty(_toast) && Time.realtimeSinceStartup < _toastUntil)
                DrawToast();
        }

        private void HandleCaptureHotkey()
        {
            if (_annotationVisible || _captureInProgress || _replayMode)
                return;

            Event current = Event.current;
            if (current == null || current.type != EventType.KeyDown || current.keyCode != CaptureKey)
                return;

            current.Use();
            if (_sessionActive)
                AddFrameToActiveIssue();
            else
                BeginNewIssue();
        }

        private void DrawCaptureButton()
        {
            const float width = 150f;
            const float height = 38f;
            var rect = new Rect(Mathf.Max(10f, Screen.width - width - 12f), 12f, width, height);
            if (GUI.Button(rect, "Flag issue  [F8]"))
                BeginNewIssue();
        }

        private void DrawActiveSessionControls()
        {
            const float width = 250f;
            var area = new Rect(Mathf.Max(10f, Screen.width - width - 12f), 12f, width, 105f);
            GUILayout.BeginArea(area, GUI.skin.window);
            GUILayout.Label($"Issue session — {_pendingFrames.Count} screenshot(s)", _smallStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Add shot [F8]", GUILayout.Height(30f)))
                AddFrameToActiveIssue();
            if (GUILayout.Button("Finish issue", GUILayout.Height(30f)))
                OpenAnnotationForFinish();
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Cancel issue", GUILayout.Height(25f)))
                CancelPendingCapture("Issue capture cancelled.");
            GUILayout.EndArea();
        }

        private void BeginNewIssue()
        {
            if (_captureInProgress || _annotationVisible || _replayMode || _sessionActive)
                return;

            Camera camera = ResolveActiveCamera(null);
            if (camera == null)
            {
                ShowToast("No active game camera found; issue was not captured.");
                return;
            }

            _pendingFrames.Clear();
            _pendingRecord = BuildIssueRecord();
            _note = string.Empty;
            _sessionActive = true;

            // The first screenshot freezes the exact reported view and immediately asks for a
            // description. The user can then choose Keep capturing to resume the scene and catch
            // additional states of a flicker/transient failure with F8.
            PreserveAndPauseForAnnotation();
            BeginFrameCapture(camera, true);
        }

        private void AddFrameToActiveIssue()
        {
            if (!_sessionActive || _captureInProgress || _annotationVisible || _replayMode)
                return;

            Camera camera = ResolveActiveCamera(null);
            if (camera == null)
            {
                ShowToast("No active game camera found; screenshot was not added.");
                return;
            }

            BeginFrameCapture(camera, false);
        }

        private void BeginFrameCapture(Camera camera, bool openAnnotationAfter)
        {
            _captureInProgress = true;
            SceneIssueFrameCapture snapshot = BuildFrame(camera, _pendingFrames.Count + 1);
            if (openAnnotationAfter)
            {
                _frozenCamera = camera;
                _frozenAnchor = snapshot.poseAnchor;
                _frozenView = snapshot.camera;
                ApplyFrozenPose();
            }

            StartCoroutine(CaptureRenderedFrame(snapshot, openAnnotationAfter));
        }

        private IEnumerator CaptureRenderedFrame(SceneIssueFrameCapture snapshot, bool openAnnotationAfter)
        {
            _overlayHidden = true;
            yield return new WaitForEndOfFrame();

            try
            {
                if (openAnnotationAfter)
                    ApplyFrozenPose();

                Texture2D screenshot = ScreenCapture.CaptureScreenshotAsTexture();
                if (screenshot == null)
                    throw new InvalidOperationException("ScreenCapture returned no texture.");

                byte[] png = screenshot.EncodeToPNG();
                Destroy(screenshot);
                if (png == null || png.Length == 0)
                    throw new InvalidOperationException("PNG encoding returned no data.");

                _pendingFrames.Add(new PendingFrame { Snapshot = snapshot, Png = png });

                if (openAnnotationAfter)
                {
                    _annotationFocused = false;
                    _annotationVisible = true;
                }
                else
                {
                    ShowToast($"Added screenshot {_pendingFrames.Count} to this issue.");
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (_pendingFrames.Count == 0)
                    CancelPendingCapture("Capture failed; see the Unity Console for details.");
                else
                    ShowToast("Screenshot failed; previous issue screenshots are still kept.");
            }
            finally
            {
                _overlayHidden = false;
                _captureInProgress = false;
            }
        }

        private void OpenAnnotationForFinish()
        {
            if (!_sessionActive || _captureInProgress || _annotationVisible)
                return;

            Camera camera = ResolveActiveCamera(null);
            if (camera != null)
            {
                SceneIssueFrameCapture view = BuildFrame(camera, _pendingFrames.Count + 1);
                _frozenCamera = camera;
                _frozenAnchor = view.poseAnchor;
                _frozenView = view.camera;
            }

            PreserveAndPauseForAnnotation();
            _annotationFocused = false;
            _annotationVisible = true;
        }

        private void DrawAnnotationDialog()
        {
            var dim = new Rect(0f, 0f, Screen.width, Screen.height);
            Color oldColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.Box(dim, GUIContent.none);
            GUI.color = oldColor;

            float width = Mathf.Clamp(Screen.width - 40f, 320f, 700f);
            float height = Mathf.Clamp(Screen.height - 80f, 285f, 420f);
            var area = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);

            GUILayout.BeginArea(area, GUI.skin.window);
            GUILayout.Label("Flag scene issue", _titleStyle);
            GUILayout.Space(4f);
            GUILayout.Label(
                $"{_pendingFrames.Count} clean screenshot(s) captured. Each screenshot keeps its own exact frame, camera and pose metadata. Describe the issue below.",
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
            if (GUILayout.Button("Cancel issue", GUILayout.Height(34f)))
            {
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
                CancelPendingCapture(null);
                return;
            }

            if (GUILayout.Button("Keep capturing", GUILayout.Width(150f), GUILayout.Height(34f)))
            {
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
                ContinueCaptureSession();
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

        private void ContinueCaptureSession()
        {
            _annotationVisible = false;
            _annotationFocused = false;
            ReleaseFrozenPose();
            RestoreAfterAnnotation();
            ShowToast("Issue session active — press F8 whenever the bad state appears.");
        }

        private void SavePendingCapture()
        {
            if (_pendingRecord == null || _pendingFrames.Count == 0)
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
                _pendingRecord.captures = new SceneIssueFrameCapture[_pendingFrames.Count];

                for (int i = 0; i < _pendingFrames.Count; i++)
                {
                    PendingFrame pending = _pendingFrames[i];
                    _pendingRecord.captures[i] = pending.Snapshot;
                    File.WriteAllBytes(Path.Combine(captureDirectory, pending.Snapshot.screenshot), pending.Png);
                }

                MirrorFirstFrameIntoLegacyFields(_pendingRecord, _pendingRecord.captures[0]);
                File.WriteAllText(
                    Path.Combine(captureDirectory, RecordFileName),
                    JsonUtility.ToJson(_pendingRecord, true),
                    new UTF8Encoding(false));

                Debug.Log($"Scene issue captured: {captureDirectory} ({_pendingFrames.Count} screenshots)");
                ShowToast($"Issue saved with {_pendingFrames.Count} screenshot(s): {Path.GetFileName(captureDirectory)}");
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

        private static void MirrorFirstFrameIntoLegacyFields(SceneIssueCaptureRecord record, SceneIssueFrameCapture frame)
        {
            record.screenshot = frame.screenshot;
            record.frameCount = frame.frameCount;
            record.timeSinceLevelLoad = frame.timeSinceLevelLoad;
            record.screenWidth = frame.screenWidth;
            record.screenHeight = frame.screenHeight;
            record.poseAnchor = frame.poseAnchor;
            record.camera = frame.camera;
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
            _sessionActive = false;
            _pendingFrames.Clear();
            _pendingRecord = null;
            _note = string.Empty;
            ReleaseFrozenPose();
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

        private SceneIssueCaptureRecord BuildIssueRecord()
        {
            Scene scene = SceneManager.GetActiveScene();
            string sceneSlug = SanitizeFileComponent(string.IsNullOrEmpty(scene.name) ? "UntitledScene" : scene.name);
            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);

            return new SceneIssueCaptureRecord
            {
                formatVersion = 2,
                id = $"{timestamp}-{sceneSlug}",
                capturedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                note = string.Empty,
                status = "open",
                unityVersion = Application.unityVersion,
                platform = Application.platform.ToString(),
                sceneName = scene.name,
                scenePath = scene.path,
                sceneBuildIndex = scene.buildIndex
            };
        }

        private SceneIssueFrameCapture BuildFrame(Camera camera, int sequence)
        {
            Transform poseAnchor = SelectPoseAnchor(camera.transform);
            return new SceneIssueFrameCapture
            {
                screenshot = $"screenshot-{sequence:000}.png",
                capturedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
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

        private void ReleaseFrozenPose()
        {
            _frozenCamera = null;
            _frozenAnchor = null;
            _frozenView = null;
        }

        private static SceneIssueFrameCapture[] GetReplayFrames(SceneIssueCaptureRecord record)
        {
            if (record.captures != null && record.captures.Length > 0)
                return record.captures;

            if (record.camera == null)
                return Array.Empty<SceneIssueFrameCapture>();

            return new[]
            {
                new SceneIssueFrameCapture
                {
                    screenshot = record.screenshot,
                    capturedUtc = record.capturedUtc,
                    frameCount = record.frameCount,
                    timeSinceLevelLoad = record.timeSinceLevelLoad,
                    screenWidth = record.screenWidth,
                    screenHeight = record.screenHeight,
                    poseAnchor = record.poseAnchor,
                    camera = record.camera
                }
            };
        }

        private static Camera ResolveActiveCamera(SceneIssueCaptureRecord record)
        {
            Camera[] cameras = Camera.allCameras;
            SceneIssueCameraSnapshot target = null;
            if (record != null)
            {
                SceneIssueFrameCapture[] frames = GetReplayFrames(record);
                if (frames.Length > 0)
                    target = frames[0].camera;
            }

            if (target != null && !string.IsNullOrEmpty(target.hierarchyPath))
            {
                foreach (Camera candidate in cameras)
                {
                    if (candidate != null && GetHierarchyPath(candidate.transform) == target.hierarchyPath)
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
            float width = Mathf.Clamp(Screen.width - 40f, 360f, 680f);
            var area = new Rect((Screen.width - width) * 0.5f, 12f, width, 180f);
            GUILayout.BeginArea(area, GUI.skin.window);
            GUILayout.Label("Scene issue replay", _titleStyle);
            GUILayout.Label(_replayRecord != null ? _replayRecord.note : string.Empty, _bodyStyle);
            GUILayout.Space(5f);
            GUILayout.Label(
                _replayFrames == null ? string.Empty : $"Screenshot {_replayIndex + 1} of {_replayFrames.Length} — {_replayFrames[_replayIndex].screenshot}",
                _smallStyle);
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUI.enabled = _replayFrames != null && _replayIndex > 0;
            if (GUILayout.Button("Previous", GUILayout.Width(90f), GUILayout.Height(30f)))
                SelectReplayFrame(_replayIndex - 1);
            GUI.enabled = _replayFrames != null && _replayIndex + 1 < _replayFrames.Length;
            if (GUILayout.Button("Next", GUILayout.Width(90f), GUILayout.Height(30f)))
                SelectReplayFrame(_replayIndex + 1);
            GUI.enabled = true;
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Release camera", GUILayout.Width(130f), GUILayout.Height(30f)))
                ReleaseReplayCamera();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void SelectReplayFrame(int index)
        {
            if (_replayFrames == null || index < 0 || index >= _replayFrames.Length)
                return;

            _replayIndex = index;
            SceneIssueFrameCapture frame = _replayFrames[index];
            _frozenAnchor = frame.poseAnchor;
            _frozenView = frame.camera;
            ApplyFrozenPose();
        }

        private void ReleaseReplayCamera()
        {
            _replayMode = false;
            _replayRecord = null;
            _replayFrames = null;
            _replayIndex = 0;
            ReleaseFrozenPose();
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

            SceneIssueFrameCapture[] frames = GetReplayFrames(record);
            if (frames.Length == 0)
            {
                ShowToast("Replay capture contains no camera frames.");
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
            _replayFrames = frames;
            _replayIndex = 0;
            _frozenCamera = camera;
            _replayMode = true;
            SelectReplayFrame(0);
            ShowToast($"Replaying issue with {frames.Length} screenshot(s).");
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