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
        public int formatVersion = 3;
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

        // Compatibility fields. New captures mirror the first frame here so older replay/test
        // helpers can continue to use a single-frame fixture while formatVersion 3 uses captures[].
        public string screenshot;
        public int frameCount;
        public float timeSinceLevelLoad;
        public int screenWidth;
        public int screenHeight;
        public SceneIssueTransformSnapshot poseAnchor;
        public SceneIssueCameraSnapshot camera;
        public SceneIssueScreenCircle[] circles;
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
        public SceneIssueScreenCircle[] circles;
    }

    [Serializable]
    public sealed class SceneIssueScreenCircle
    {
        // Screen-space values normalized to the captured image. centerY uses GUI/top-down space.
        public float centerX;
        public float centerY;
        public float radius;
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
    /// frames, each with exact camera/pose metadata and optional screen-space circles identifying
    /// the problematic region. This is useful for flicker, popping, seams and transient holes.
    /// </summary>
    public sealed class SceneIssueCapture : MonoBehaviour
    {
        public const string ReplayRequestEditorPrefsKey = "MountingForce.SceneIssueCapture.ReplayRequest";
        private const string ReplayRequestCommandLineArgument = "-voxel-scene-issue";

        private const KeyCode CaptureKey = KeyCode.F8;
        private const string CaptureDirectoryName = "SceneIssues";
        private const string OpenCaptureDirectoryName = "open";
        private const string RecordFileName = "issue.json";
        private const int CircleSegments = 40;

        private sealed class PendingFrame
        {
            public SceneIssueFrameCapture Snapshot;
            public byte[] Png;
            public readonly List<SceneIssueScreenCircle> Circles = new();
        }

        private readonly List<PendingFrame> _pendingFrames = new();

        private bool _captureInProgress;
        private bool _sessionActive;
        private bool _annotationVisible;
        private bool _overlayHidden;
        private bool _replayMode;
        private bool _annotationFocused;
        private bool _drawingCircle;
        private string _note = string.Empty;
        private string _toast = string.Empty;
        private float _toastUntil;
        private SceneIssueCaptureRecord _pendingRecord;
        private SceneIssueCaptureRecord _replayRecord;
        private SceneIssueFrameCapture[] _replayFrames;
        private int _replayIndex;
        private int _annotationFrameIndex;
        private Vector2 _circleStart;
        private Texture2D _annotationTexture;
        private Texture2D _lineTexture;
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

        public static string GetOpenCaptureRootPath()
        {
            return Path.Combine(GetCaptureRootPath(), OpenCaptureDirectoryName);
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            ReleaseAnnotationTexture();
            if (_lineTexture != null)
                Destroy(_lineTexture);
        }

        private void Start()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
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
            {
                DrawReplayCircles();
                DrawReplayBanner();
            }
            else if (_sessionActive)
            {
                DrawActiveSessionControls();
            }
            else if (!_captureInProgress)
            {
                DrawCaptureButton();
            }

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

            PreserveAndPauseForAnnotation();
            BeginFrameCapture(camera);
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

            // Pause immediately after snapshotting the pose so the exact bad frame can be marked.
            PreserveAndPauseForAnnotation();
            BeginFrameCapture(camera);
        }

        private void BeginFrameCapture(Camera camera)
        {
            _captureInProgress = true;
            SceneIssueFrameCapture snapshot = BuildFrame(camera, _pendingFrames.Count + 1);
            _frozenCamera = camera;
            _frozenAnchor = snapshot.poseAnchor;
            _frozenView = snapshot.camera;
            ApplyFrozenPose();
            StartCoroutine(CaptureRenderedFrame(snapshot));
        }

        private IEnumerator CaptureRenderedFrame(SceneIssueFrameCapture snapshot)
        {
            _overlayHidden = true;
            yield return new WaitForEndOfFrame();

            try
            {
                ApplyFrozenPose();
                Texture2D screenshot = ScreenCapture.CaptureScreenshotAsTexture();
                if (screenshot == null)
                    throw new InvalidOperationException("ScreenCapture returned no texture.");

                byte[] png = screenshot.EncodeToPNG();
                Destroy(screenshot);
                if (png == null || png.Length == 0)
                    throw new InvalidOperationException("PNG encoding returned no data.");

                var pending = new PendingFrame { Snapshot = snapshot, Png = png };
                _pendingFrames.Add(pending);
                OpenAnnotationForFrame(_pendingFrames.Count - 1);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (_pendingFrames.Count == 0)
                    CancelPendingCapture("Capture failed; see the Unity Console for details.");
                else
                {
                    ReleaseFrozenPose();
                    RestoreAfterAnnotation();
                    ShowToast("Screenshot failed; previous issue screenshots are still kept.");
                }
            }
            finally
            {
                _overlayHidden = false;
                _captureInProgress = false;
            }
        }

        private void OpenAnnotationForFinish()
        {
            if (!_sessionActive || _captureInProgress || _annotationVisible || _pendingFrames.Count == 0)
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
            OpenAnnotationForFrame(_pendingFrames.Count - 1);
        }

        private void OpenAnnotationForFrame(int index)
        {
            if (index < 0 || index >= _pendingFrames.Count)
                return;

            _annotationFrameIndex = index;
            _annotationFocused = false;
            _drawingCircle = false;
            LoadAnnotationTexture(_pendingFrames[index].Png);
            _annotationVisible = true;
        }

        private void DrawAnnotationDialog()
        {
            var dim = new Rect(0f, 0f, Screen.width, Screen.height);
            Color oldColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.62f);
            GUI.Box(dim, GUIContent.none);
            GUI.color = oldColor;

            float width = Mathf.Clamp(Screen.width - 40f, 760f, 1180f);
            float height = Mathf.Clamp(Screen.height - 70f, 480f, 820f);
            var area = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);

            GUILayout.BeginArea(area, GUI.skin.window);
            GUILayout.Label("Flag scene issue", _titleStyle);
            GUILayout.Space(3f);
            GUILayout.Label(
                "Drag on the screenshot to circle the bad area. Circles belong to this specific captured frame and are shown again during replay.",
                _bodyStyle);
            GUILayout.Space(6f);

            GUILayout.BeginHorizontal(GUILayout.ExpandHeight(true));
            DrawAnnotationPreview(width * 0.60f, height - 150f);
            GUILayout.Space(10f);
            DrawAnnotationSidePanel(height - 150f);
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
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

        private void DrawAnnotationPreview(float targetWidth, float targetHeight)
        {
            GUILayout.BeginVertical(GUILayout.Width(targetWidth), GUILayout.ExpandHeight(true));

            GUILayout.BeginHorizontal();
            GUI.enabled = _annotationFrameIndex > 0;
            if (GUILayout.Button("Previous", GUILayout.Width(90f), GUILayout.Height(27f)))
                OpenAnnotationForFrame(_annotationFrameIndex - 1);
            GUI.enabled = _annotationFrameIndex + 1 < _pendingFrames.Count;
            if (GUILayout.Button("Next", GUILayout.Width(90f), GUILayout.Height(27f)))
                OpenAnnotationForFrame(_annotationFrameIndex + 1);
            GUI.enabled = true;
            GUILayout.FlexibleSpace();
            GUILayout.Label($"Frame {_annotationFrameIndex + 1} of {_pendingFrames.Count}", _smallStyle);
            GUILayout.EndHorizontal();

            Rect outer = GUILayoutUtility.GetRect(targetWidth, Mathf.Max(260f, targetHeight - 35f), GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            GUI.Box(outer, GUIContent.none);

            if (_annotationTexture == null)
            {
                GUI.Label(outer, "Screenshot preview unavailable.", _smallStyle);
                GUILayout.EndVertical();
                return;
            }

            Rect preview = FitRect(outer, _annotationTexture.width, _annotationTexture.height);
            GUI.DrawTexture(preview, _annotationTexture, ScaleMode.StretchToFill, false);
            HandleCircleInput(preview);

            PendingFrame pending = _pendingFrames[_annotationFrameIndex];
            DrawCircles(preview, pending.Circles, Color.red, 3f);

            if (_drawingCircle)
            {
                SceneIssueScreenCircle draft = BuildCircle(preview, _circleStart, Event.current.mousePosition);
                DrawCircle(preview, draft, new Color(1f, 0.6f, 0.15f, 1f), 2f);
            }

            GUILayout.EndVertical();
        }

        private void DrawAnnotationSidePanel(float targetHeight)
        {
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.Height(targetHeight));
            GUILayout.Label("Issue description", _smallStyle);
            GUI.SetNextControlName("SceneIssueDescription");
            _note = GUILayout.TextArea(_note ?? string.Empty, _textAreaStyle, GUILayout.ExpandHeight(true));
            if (!_annotationFocused && Event.current.type == EventType.Repaint)
            {
                GUI.FocusControl("SceneIssueDescription");
                _annotationFocused = true;
            }

            GUILayout.Space(8f);
            PendingFrame pending = _pendingFrames[_annotationFrameIndex];
            GUILayout.Label($"Circles on this frame: {pending.Circles.Count}", _smallStyle);
            GUILayout.Label(
                "Left-drag: draw circle\nRight-click: remove nearest circle\nClear: remove all circles from this frame",
                _bodyStyle);
            if (GUILayout.Button("Clear circles", GUILayout.Width(120f), GUILayout.Height(28f)))
                pending.Circles.Clear();
            GUILayout.EndVertical();
        }

        private void HandleCircleInput(Rect preview)
        {
            Event current = Event.current;
            if (current == null)
                return;

            if (current.type == EventType.MouseDown && preview.Contains(current.mousePosition))
            {
                if (current.button == 0)
                {
                    _drawingCircle = true;
                    _circleStart = current.mousePosition;
                    GUI.FocusControl(null);
                    current.Use();
                    return;
                }

                if (current.button == 1)
                {
                    RemoveNearestCircle(preview, current.mousePosition);
                    current.Use();
                    return;
                }
            }

            if (_drawingCircle && current.type == EventType.MouseDrag)
            {
                current.Use();
                return;
            }

            if (_drawingCircle && current.type == EventType.MouseUp && current.button == 0)
            {
                SceneIssueScreenCircle circle = BuildCircle(preview, _circleStart, current.mousePosition);
                if (circle.radius >= 0.008f)
                    _pendingFrames[_annotationFrameIndex].Circles.Add(circle);
                _drawingCircle = false;
                current.Use();
            }
        }

        private void RemoveNearestCircle(Rect preview, Vector2 mousePosition)
        {
            List<SceneIssueScreenCircle> circles = _pendingFrames[_annotationFrameIndex].Circles;
            if (circles.Count == 0)
                return;

            int nearest = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < circles.Count; i++)
            {
                float distance = Vector2.Distance(mousePosition, CircleCenter(preview, circles[i]));
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    nearest = i;
                }
            }

            if (nearest >= 0)
                circles.RemoveAt(nearest);
        }

        private static SceneIssueScreenCircle BuildCircle(Rect preview, Vector2 start, Vector2 end)
        {
            Vector2 center = new Vector2(
                Mathf.Clamp(start.x, preview.xMin, preview.xMax),
                Mathf.Clamp(start.y, preview.yMin, preview.yMax));
            Vector2 edge = new Vector2(
                Mathf.Clamp(end.x, preview.xMin, preview.xMax),
                Mathf.Clamp(end.y, preview.yMin, preview.yMax));
            float basis = Mathf.Max(1f, Mathf.Min(preview.width, preview.height));

            return new SceneIssueScreenCircle
            {
                centerX = Mathf.InverseLerp(preview.xMin, preview.xMax, center.x),
                centerY = Mathf.InverseLerp(preview.yMin, preview.yMax, center.y),
                radius = Mathf.Clamp01(Vector2.Distance(center, edge) / basis)
            };
        }

        private static Vector2 CircleCenter(Rect rect, SceneIssueScreenCircle circle)
        {
            return new Vector2(
                Mathf.Lerp(rect.xMin, rect.xMax, circle.centerX),
                Mathf.Lerp(rect.yMin, rect.yMax, circle.centerY));
        }

        private void ContinueCaptureSession()
        {
            _annotationVisible = false;
            _annotationFocused = false;
            _drawingCircle = false;
            ReleaseAnnotationTexture();
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
                string root = GetOpenCaptureRootPath();
                string captureDirectory = Path.Combine(root, _pendingRecord.id);
                Directory.CreateDirectory(captureDirectory);

                _pendingRecord.note = (_note ?? string.Empty).Trim();
                _pendingRecord.captures = new SceneIssueFrameCapture[_pendingFrames.Count];

                for (int i = 0; i < _pendingFrames.Count; i++)
                {
                    PendingFrame pending = _pendingFrames[i];
                    pending.Snapshot.circles = pending.Circles.ToArray();
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
            record.circles = frame.circles;
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
            _drawingCircle = false;
            _captureInProgress = false;
            _sessionActive = false;
            _pendingFrames.Clear();
            _pendingRecord = null;
            _note = string.Empty;
            ReleaseAnnotationTexture();
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
                formatVersion = 3,
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
                camera = CaptureCamera(camera),
                circles = Array.Empty<SceneIssueScreenCircle>()
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
                    camera = record.camera,
                    circles = record.circles ?? Array.Empty<SceneIssueScreenCircle>()
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

        private void DrawReplayCircles()
        {
            if (_replayFrames == null || _replayIndex < 0 || _replayIndex >= _replayFrames.Length)
                return;

            SceneIssueScreenCircle[] circles = _replayFrames[_replayIndex].circles;
            if (circles == null || circles.Length == 0)
                return;

            DrawCircles(new Rect(0f, 0f, Screen.width, Screen.height), circles, Color.red, 3f);
        }

        private void DrawReplayBanner()
        {
            float width = Mathf.Clamp(Screen.width - 40f, 360f, 680f);
            var area = new Rect((Screen.width - width) * 0.5f, 12f, width, 190f);
            GUILayout.BeginArea(area, GUI.skin.window);
            GUILayout.Label("Scene issue replay", _titleStyle);
            GUILayout.Label(_replayRecord != null ? _replayRecord.note : string.Empty, _bodyStyle);
            GUILayout.Space(5f);
            if (_replayFrames != null)
            {
                SceneIssueFrameCapture frame = _replayFrames[_replayIndex];
                int circleCount = frame.circles != null ? frame.circles.Length : 0;
                GUILayout.Label(
                    $"Screenshot {_replayIndex + 1} of {_replayFrames.Length} — {frame.screenshot} — {circleCount} marked region(s)",
                    _smallStyle);
            }
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static string ConsumeReplayRequestPath()
        {
#if UNITY_EDITOR
            string editorPath = UnityEditor.EditorPrefs.GetString(ReplayRequestEditorPrefsKey, string.Empty);
            if (!string.IsNullOrEmpty(editorPath))
            {
                UnityEditor.EditorPrefs.DeleteKey(ReplayRequestEditorPrefsKey);
                return editorPath;
            }
#endif

            string[] arguments = Environment.GetCommandLineArgs();
            for (int i = 0; i < arguments.Length; i++)
            {
                if (!string.Equals(arguments[i], ReplayRequestCommandLineArgument, StringComparison.Ordinal))
                    continue;

                if (i + 1 >= arguments.Length || string.IsNullOrWhiteSpace(arguments[i + 1]))
                {
                    Debug.LogWarning($"{ReplayRequestCommandLineArgument} requires an issue.json path.");
                    return string.Empty;
                }

                return arguments[i + 1];
            }

            return string.Empty;
        }

        private IEnumerator ConsumeReplayRequest()
        {
            string path = ConsumeReplayRequestPath();
            if (string.IsNullOrEmpty(path))
                yield break;

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

        private void LoadAnnotationTexture(byte[] png)
        {
            ReleaseAnnotationTexture();
            if (png == null || png.Length == 0)
                return;

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(png, false))
            {
                Destroy(texture);
                return;
            }

            _annotationTexture = texture;
        }

        private void ReleaseAnnotationTexture()
        {
            if (_annotationTexture == null)
                return;

            Destroy(_annotationTexture);
            _annotationTexture = null;
        }

        private void DrawCircles(Rect rect, IList<SceneIssueScreenCircle> circles, Color color, float thickness)
        {
            if (circles == null)
                return;

            for (int i = 0; i < circles.Count; i++)
                DrawCircle(rect, circles[i], color, thickness);
        }

        private void DrawCircle(Rect rect, SceneIssueScreenCircle circle, Color color, float thickness)
        {
            if (circle == null || circle.radius <= 0f)
                return;

            Vector2 center = CircleCenter(rect, circle);
            float radius = circle.radius * Mathf.Min(rect.width, rect.height);
            Vector2 previous = center + new Vector2(radius, 0f);
            for (int i = 1; i <= CircleSegments; i++)
            {
                float theta = (Mathf.PI * 2f * i) / CircleSegments;
                Vector2 next = center + new Vector2(Mathf.Cos(theta) * radius, Mathf.Sin(theta) * radius);
                DrawLine(previous, next, color, thickness);
                previous = next;
            }
        }

        private void DrawLine(Vector2 start, Vector2 end, Color color, float thickness)
        {
            if (_lineTexture == null)
                return;

            Vector2 delta = end - start;
            float length = delta.magnitude;
            if (length <= 0.01f)
                return;

            Matrix4x4 oldMatrix = GUI.matrix;
            Color oldColor = GUI.color;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            GUI.color = color;
            GUIUtility.RotateAroundPivot(angle, start);
            GUI.DrawTexture(new Rect(start.x, start.y - thickness * 0.5f, length, thickness), _lineTexture);
            GUI.matrix = oldMatrix;
            GUI.color = oldColor;
        }

        private static Rect FitRect(Rect outer, float contentWidth, float contentHeight)
        {
            if (contentWidth <= 0f || contentHeight <= 0f)
                return outer;

            float outerAspect = outer.width / Mathf.Max(1f, outer.height);
            float contentAspect = contentWidth / contentHeight;
            if (contentAspect > outerAspect)
            {
                float height = outer.width / contentAspect;
                return new Rect(outer.xMin, outer.yMin + (outer.height - height) * 0.5f, outer.width, height);
            }

            float width = outer.height * contentAspect;
            return new Rect(outer.xMin + (outer.width - width) * 0.5f, outer.yMin, width, outer.height);
        }

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

            _lineTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _lineTexture.SetPixel(0, 0, Color.white);
            _lineTexture.Apply();
        }
    }
}
#endif
