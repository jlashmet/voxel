using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelEngine.Composition;
using VoxelEngine.Storage.Api;

using VoxelEngine.Structures.Api;
using Mat = Game.Materials.Api.GameMaterialIds;   // engine-side Mat constants were removed

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Interactive production-path look-development bench for the hero arch.
    ///
    /// The exchange contract (state.json / command.json / hero-preset.json) is consumed by
    /// tools/arch-lookdev.sh and specified in Assets/Scenes/ArchLookdev.md. Commands are
    /// {requestId, action, name, sweepAxis, settings} and published state nests the complete
    /// settings object under "settings" so a capture can be reproduced exactly. Do not flatten
    /// either shape without updating the CLI and the document together.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class ArchLookdev : MonoBehaviour
    {
        private const float VoxelSize = 0.1f;
        private const byte StoneMaterial = Mat.MasonryMedium;
        private const float PanelWidth = 330f;

        private IVoxelStorageRuntime _storage;
        private IProfileBlockReadSource _profileBlocks;
        private Camera _camera;
        private Vector2 _scroll;
        private bool _panelVisible = true;
        private bool _pendingRebuild;
        private bool _cameraInitialized;
        private bool _sweepRunning;
        private bool _stateDirty;
        private string _status = "Building…";
        private string _exchangeStatus = "JSON LINK READY";
        private double _lastBuildMs;
        private GUIStyle _panelStyle, _titleStyle, _sectionStyle, _valueStyle, _buttonStyle;
        private Vector4 _originalStoneAlbedo, _originalMossTint;

        // Form
        private int _clearSpan = 32, _pierHeight = 40, _ringThickness = 7;
        private int _voussoirs = 13, _depth = 12, _shoulder = 10, _topMargin = 8;
        private int _faceRecess = 1, _plinthHeight = 4, _impostHeight = 3;
        private int _damage, _damageScale = 2, _seedOffset = 0x2222;
        private int _jointQ4 = 4, _bevelQ4 = 4, _projectionQ4 = 8, _faceDepthQ4 = 16;

        // Growth
        private int _mossCoverage = 115, _mossDensity = 210, _mossRadiusQ4 = 18;
        private int _mossHeightQ4 = 2, _mossDropQ4 = 18, _mossSeparation;
        private float _mossHue = 0.22f, _mossSaturation = 0.53f, _mossValue = 0.39f;

        // Presentation
        private float _stoneWarmth = 0.58f, _stoneValue = 0.68f;
        private float _sunAzimuth = -48f, _sunElevation = 50f;
        private Vector3 _cameraFocus;
        private float _cameraYaw = 14.5f, _cameraPitch = 3.2f;
        private float _cameraDistance = 14.5f, _cameraFov = 34f;
        private float _cameraMoveSpeed = 4f;
        private float _buildBudgetMs = 12f;
        private int _lastBayWidth, _lastBayHeight;

        // Comparison and automation
        private Texture2D _targetImage;
        private string _targetPath, _exchangeDirectory, _commandPath, _statePath, _presetPath;
        private string _lastCommandText;
        private float _nextCommandPoll;
        private float _nextStatePublish;
        private int _comparisonMode = 1;
        private float _targetOpacity = 0.46f;
        private int _sweepParameterIndex;
        private static readonly string[] SweepLabels =
            { "Voussoirs", "Joint", "Bevel", "Moss" };

        // VoxelEngine.Showcase does not reference Structures.Runtime, so the ruin-damage enum is
        // not nameable here (and the architecture guard forbids naming it). These labels mirror
        // that enum by ordinal purely for the read-only ruin-state readout in the panel.
        private static readonly string[] RuinDamageLabels =
        {
            "Intact", "BrokenCrown", "BrokenLeftHaunch", "BrokenRightHaunch", "CollapsedShoulder",
        };

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            _camera = GetComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.43f, 0.44f, 0.48f);
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 80f;
            _camera.allowHDR = false;
            _originalStoneAlbedo = RenderingComposition.GetMaterialAlbedo(StoneMaterial);
            _originalMossTint = RenderingComposition.GetCoatingTint(Coatings.Moss);
            RenderingComposition.SetBuildBudgets(_buildBudgetMs, 0);
            RenderingComposition.SetSky(
                new Color(0.627f, 0.722f, 0.773f, 1f),
                new Color(0.341f, 0.600f, 0.847f, 1f));
            InitialiseExchange();
            LoadTargetImage();
            Rebuild();
            PublishState();
        }

        private void OnDisable()
        {
            RenderingComposition.ClearWorld();
            RenderingComposition.SetMaterialAlbedo(StoneMaterial, _originalStoneAlbedo);
            RenderingComposition.SetCoatingTint(Coatings.Moss, _originalMossTint);
            if (_targetImage != null) Destroy(_targetImage);
            DisposeWorld();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Tab)) _panelVisible = !_panelVisible;
            if (Input.GetKeyDown(KeyCode.R)) Rebuild();
            else if (_pendingRebuild) Rebuild();
            if (Input.GetKeyDown(KeyCode.F)) FrameCamera(_lastBayWidth, _lastBayHeight);
            ApplyPresentation();
            HandleInspectionCamera();
            PollCommandInbox();
            if (_stateDirty && Time.unscaledTime >= _nextStatePublish)
            {
                _nextStatePublish = Time.unscaledTime + 0.25f;
                _stateDirty = false;
                PublishState();
            }

            if (RenderingComposition.TryGetSurfaceBuildStatus(
                    out int knownChunks,
                    out int dirtyChunks,
                    out int residentChunks,
                    out long residentGeometryBytes))
            {
                bool converged = dirtyChunks == 0 && residentChunks >= knownChunks;
                _status = converged
                    ? $"READY  {residentGeometryBytes / (1024f * 1024f):0.0} MB  ·  {_lastBuildMs:0} ms authoring"
                    : $"MESHING  {residentChunks}/{knownChunks} chunks";
            }
        }

        private void Rebuild()
        {
            var watch = Stopwatch.StartNew();
            IVoxelStorageRuntime nextStorage = VoxelEngineBootstrap.CreateStorage(8, 24_000);
            const uint coatings = (1u << Coatings.Moss) | (1u << Coatings.Snow)
                                | (1u << Coatings.Soot) | (1u << Coatings.Wet);
            nextStorage.RegisterMaterial(StoneMaterial, 210, DestructionClass.Crumble,
                                         SurfaceStyles.MasonryJoint, coatings);
            nextStorage.ConfigureCoatingDecoration(
                Coatings.Moss,
                (byte)_mossDensity,
                (byte)_mossRadiusQ4,
                (byte)_mossHeightQ4,
                (byte)_mossDropQ4,
                (byte)_mossSeparation);

            var request = new ArchLookdevBuildRequest
            {
                ClearSpan = _clearSpan,
                PierHeight = _pierHeight,
                RingThickness = _ringThickness,
                Depth = _depth,
                VoussoirCount = _voussoirs,
                ShoulderWidth = _shoulder,
                TopMargin = _topMargin,
                FaceRecess = _faceRecess,
                PlinthHeight = _plinthHeight,
                ImpostHeight = _impostHeight,
                Damage = _damage,
                DamageSeed = 0xA341u + (uint)_seedOffset,
                DamageScale = _damageScale,
                ProfileJointHalfWidthQ4 = _jointQ4,
                ProfileBevelQ4 = _bevelQ4,
                ProfileProjectionQ4 = _projectionQ4,
                ProfileDepthQ4 = _faceDepthQ4,
                StoneMaterial = StoneMaterial,
                SurfaceStyle = SurfaceStyles.MasonryJoint,
                Coating = Coatings.Moss,
                CoatingCoverage = _mossCoverage,
                BrushBudget = 2_000_000,
            };
            ArchLookdevBuildResult build = StructuresComposition.BuildArchLookdev(
                nextStorage, in request);

            IVoxelStorageRuntime oldStorage = _storage;
            _storage = nextStorage;
            _profileBlocks = build.ProfileBlocks;
            var world = new RenderingWorldBinding(
                _storage.Reads,
                _storage.MaterialPresentation,
                _storage.SurfacePresentation,
                _storage.CoatingPresentation,
                _profileBlocks);
            RenderingComposition.ConfigureWorld(
                in world, _storage.Changes, 0, _buildBudgetMs, 0, farFieldEnabled: false);
            oldStorage?.Dispose();

            watch.Stop();
            _lastBuildMs = watch.Elapsed.TotalMilliseconds;
            _pendingRebuild = false;
            _lastBayWidth = build.Width;
            _lastBayHeight = build.Height;
            if (!_cameraInitialized) FrameCamera(build.Width, build.Height);
            _stateDirty = true;
        }

        private void DisposeWorld()
        {
            _storage?.Dispose();
            _storage = null;
        }

        private void FrameCamera(int width, int height)
        {
            _cameraFocus = new Vector3(0f, height * VoxelSize * 0.5f, 0.45f);
            _cameraYaw = 14.5f;
            _cameraPitch = 3.2f;
            _cameraDistance = Mathf.Max(8f, width * VoxelSize * 1.62f);
            _cameraInitialized = true;
            ApplyCameraTransform();
        }

        private void ApplyCameraTransform()
        {
            Quaternion orbit = Quaternion.Euler(_cameraPitch, _cameraYaw, 0f);
            _camera.transform.position = _cameraFocus + orbit * (Vector3.back * _cameraDistance);
            _camera.transform.rotation = orbit;
            _camera.fieldOfView = _cameraFov;
        }

        private void HandleInspectionCamera()
        {
            // Dragging a slider must not also orbit the world behind the panel.
            bool pointerOverPanel = _panelVisible && Input.mousePosition.x < PanelWidth + 28f;
            bool changed = false;

            if (!pointerOverPanel && Input.GetMouseButton(1))
            {
                _cameraYaw += Input.GetAxis("Mouse X") * 3.5f;
                _cameraPitch = Mathf.Clamp(_cameraPitch - Input.GetAxis("Mouse Y") * 3.5f,
                                           -80f, 80f);
                changed = true;
            }

            if (!pointerOverPanel && Input.GetMouseButton(2))
            {
                float scale = _cameraDistance * 0.0025f;
                _cameraFocus -= _camera.transform.right * Input.GetAxis("Mouse X") * scale;
                _cameraFocus -= _camera.transform.up * Input.GetAxis("Mouse Y") * scale;
                changed = true;
            }

            if (!pointerOverPanel && Mathf.Abs(Input.mouseScrollDelta.y) > 0.01f)
            {
                _cameraDistance = Mathf.Clamp(
                    _cameraDistance * Mathf.Exp(-Input.mouseScrollDelta.y * 0.12f), 1.5f, 40f);
                changed = true;
            }

            Vector3 forward = Vector3.ProjectOnPlane(_camera.transform.forward, Vector3.up).normalized;
            Vector3 motion = forward * Input.GetAxisRaw("Vertical")
                           + _camera.transform.right * Input.GetAxisRaw("Horizontal");
            if (Input.GetKey(KeyCode.E)) motion += Vector3.up;
            if (Input.GetKey(KeyCode.Q)) motion -= Vector3.up;
            if (motion.sqrMagnitude > 0.001f)
            {
                float speed = _cameraMoveSpeed * (Input.GetKey(KeyCode.LeftShift) ? 2.5f : 1f);
                _cameraFocus += motion.normalized * speed * Time.unscaledDeltaTime;
                changed = true;
            }

            if (changed)
            {
                ApplyCameraTransform();
                _stateDirty = true;
            }
        }

        private void ApplyPresentation()
        {
            Color.RGBToHSV(new Color(_stoneWarmth, 0.58f, 0.42f), out float h, out float s, out _);
            Color stone = Color.HSVToRGB(h, s * 0.72f, _stoneValue);
            RenderingComposition.SetMaterialAlbedo(
                StoneMaterial, new Vector4(stone.r, stone.g, stone.b, 1f));
            Color moss = Color.HSVToRGB(_mossHue, _mossSaturation, _mossValue);
            RenderingComposition.SetCoatingTint(
                Coatings.Moss, new Vector4(moss.r, moss.g, moss.b, 1f));
            float azimuth = _sunAzimuth * Mathf.Deg2Rad;
            float elevation = _sunElevation * Mathf.Deg2Rad;
            RenderingComposition.SetSunDirection(new Vector3(
                Mathf.Sin(azimuth) * Mathf.Cos(elevation), Mathf.Sin(elevation),
                Mathf.Cos(azimuth) * Mathf.Cos(elevation)).normalized);
            RenderingComposition.SetBuildBudgets(_buildBudgetMs, 0);
            ApplyCameraTransform();
        }

        private void OnGUI()
        {
            if (!Application.isPlaying) return;
            EnsureStyles();
            DrawTargetComparison();
            if (!_panelVisible)
            {
                if (GUI.Button(new Rect(16, 16, 190, 34), "TAB  ·  OPEN STONE BENCH", _buttonStyle))
                    _panelVisible = true;
                return;
            }

            GUILayout.BeginArea(new Rect(14, 14, PanelWidth, Screen.height - 28), _panelStyle);
            GUILayout.Label("STONEWRIGHT’S BENCH", _titleStyle);
            GUILayout.Label("HERO ARCH · PRODUCTION SURFACE", _valueStyle);
            GUILayout.Space(9);
            _scroll = GUILayout.BeginScrollView(_scroll, false, false);

            Section("FORM");
            IntSlider("Clear span", ref _clearSpan, 16, 56, 2);
            IntSlider("Pier height", ref _pierHeight, 20, 64);
            IntSlider("Ring thickness", ref _ringThickness, 4, 12);
            IntSlider("Voussoirs", ref _voussoirs, 7, 25, 2);
            IntSlider("Depth", ref _depth, 6, 20);
            IntSlider("Shoulder", ref _shoulder, 4, 20);
            IntSlider("Top margin", ref _topMargin, 2, 16);
            IntSlider("Face recess", ref _faceRecess, 1, Mathf.Max(1, _depth - 2));
            IntSlider("Plinth height", ref _plinthHeight, 2, 10);
            IntSlider("Impost height", ref _impostHeight, 2, 10);
            IntSlider("Ruin state", ref _damage, 0, 4);
            GUILayout.Label(RuinDamageLabels[Mathf.Clamp(_damage, 0, RuinDamageLabels.Length - 1)],
                            _valueStyle);
            IntSlider("Damage scale", ref _damageScale, 1, 6);
            IntSlider("Variation seed", ref _seedOffset, 0, 65535, 257);

            Section("CUT STONE · Q4 VOXELS");
            IntSlider("Joint half-width", ref _jointQ4, 1, 12);
            IntSlider("Arris bevel", ref _bevelQ4, 1, 12);
            IntSlider("Face projection", ref _projectionQ4, 1, 16);
            IntSlider("Face depth", ref _faceDepthQ4, 4, 28);

            Section("GROWTH");
            IntSlider("Coverage", ref _mossCoverage, 0, 255);
            IntSlider("Mat density", ref _mossDensity, 0, 255);
            IntSlider("Mat radius Q4", ref _mossRadiusQ4, 4, 32);
            IntSlider("Mat lift Q4", ref _mossHeightQ4, 1, 10);
            IntSlider("Overhang Q4", ref _mossDropQ4, 0, 32);
            IntSlider("Separation", ref _mossSeparation, 0, 3);
            FloatSlider("Moss hue", ref _mossHue, 0.12f, 0.38f, false);
            FloatSlider("Moss saturation", ref _mossSaturation, 0f, 1f, false);
            FloatSlider("Moss value", ref _mossValue, 0.1f, 0.8f, false);

            Section("REFERENCE");
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(_comparisonMode == 0, "OFF", _buttonStyle)) _comparisonMode = 0;
            if (GUILayout.Toggle(_comparisonMode == 1, "SPLIT", _buttonStyle)) _comparisonMode = 1;
            if (GUILayout.Toggle(_comparisonMode == 2, "OVERLAY", _buttonStyle)) _comparisonMode = 2;
            if (GUILayout.Toggle(_comparisonMode == 3, "TARGET", _buttonStyle)) _comparisonMode = 3;
            GUILayout.EndHorizontal();
            FloatSlider("Reference opacity", ref _targetOpacity, 0.05f, 1f, false);

            Section("LIGHT & LENS");
            FloatSlider("Stone warmth", ref _stoneWarmth, 0.35f, 0.85f, false);
            FloatSlider("Stone value", ref _stoneValue, 0.35f, 0.9f, false);
            FloatSlider("Sun azimuth", ref _sunAzimuth, -180f, 180f, false, "°");
            FloatSlider("Sun elevation", ref _sunElevation, 10f, 85f, false, "°");
            FloatSlider("Camera yaw", ref _cameraYaw, -180f, 180f, false, "°");
            FloatSlider("Camera pitch", ref _cameraPitch, -80f, 80f, false, "°");
            FloatSlider("Camera distance", ref _cameraDistance, 1.5f, 40f, false);
            FloatSlider("Field of view", ref _cameraFov, 20f, 60f, false, "°");
            FloatSlider("Move speed", ref _cameraMoveSpeed, 0.5f, 12f, false);
            FloatSlider("Mesh budget", ref _buildBudgetMs, 0.2f, 20f, false, " ms");
            GUILayout.EndScrollView();

            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("RESET", _buttonStyle, GUILayout.Height(27))) ResetDefaults();
            if (GUILayout.Button("COPY SETTINGS", _buttonStyle, GUILayout.Height(27)))
                GUIUtility.systemCopyBuffer = SettingsJson();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("SAVE PRESET", _buttonStyle, GUILayout.Height(27))) SavePreset();
            if (GUILayout.Button("LOAD PRESET", _buttonStyle, GUILayout.Height(27))) LoadPreset();
            if (GUILayout.Button("CAPTURE", _buttonStyle, GUILayout.Height(27)))
                StartCoroutine(CaptureWhenReady("manual"));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("SWEEP  " + SweepLabels[_sweepParameterIndex], _buttonStyle,
                                 GUILayout.Height(27)) && !_sweepRunning)
                StartCoroutine(RunSweep());
            if (GUILayout.Button("NEXT AXIS", _buttonStyle, GUILayout.Height(27)))
                _sweepParameterIndex = (_sweepParameterIndex + 1) % SweepLabels.Length;
            GUILayout.EndHorizontal();
            GUILayout.Label(_status, _valueStyle);
            GUILayout.Label(_exchangeStatus, _valueStyle);
            GUILayout.Label("Geometry rebuilds automatically", _valueStyle);
            GUILayout.Label("RMB orbit · MMB pan · wheel dolly", _valueStyle);
            GUILayout.Label("WASD/QE move · Shift faster · F frame · Tab hide", _valueStyle);
            GUILayout.EndArea();
        }

        private void InitialiseExchange()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            _exchangeDirectory = Path.Combine(projectRoot, "Artifacts", "ArchLookdev");
            Directory.CreateDirectory(_exchangeDirectory);
            _commandPath = Path.Combine(_exchangeDirectory, "command.json");
            _statePath = Path.Combine(_exchangeDirectory, "state.json");
            _presetPath = Path.Combine(_exchangeDirectory, "hero-preset.json");
        }

        private void LoadTargetImage()
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string[] candidates =
            {
                Path.Combine(home, "Downloads", "Sunlit Cleric by the Waterfall.png"),
                Path.Combine(projectRoot, "Artifacts", "ArchLookdev", "target.png"),
                Path.Combine(Application.dataPath, "Tests", "References", "arch-target.png"),
            };
            foreach (string candidate in candidates)
            {
                if (!File.Exists(candidate)) continue;
                byte[] bytes = File.ReadAllBytes(candidate);
                _targetImage = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                    { name = "Arch Reference", hideFlags = HideFlags.DontSave };
                if (_targetImage.LoadImage(bytes))
                {
                    _targetPath = candidate;
                    _exchangeStatus = "REFERENCE LOADED";
                    return;
                }
                Destroy(_targetImage);
                _targetImage = null;
            }
            _exchangeStatus = "REFERENCE NOT FOUND";
        }

        private void DrawTargetComparison()
        {
            if (_targetImage == null || _comparisonMode == 0) return;
            float left = _panelVisible ? PanelWidth + 42f : 0f;
            Rect viewport = new(left, 0f, Screen.width - left, Screen.height);
            Color previous = GUI.color;

            if (_comparisonMode == 1)
            {
                Rect targetRect = new(viewport.x + viewport.width * 0.5f, viewport.y,
                                      viewport.width * 0.5f, viewport.height);
                GUI.BeginGroup(targetRect);
                GUI.color = Color.black;
                GUI.DrawTexture(new Rect(0f, 0f, targetRect.width, targetRect.height),
                                Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.DrawTexture(new Rect(-viewport.width * 0.5f, 0f,
                                         viewport.width, viewport.height),
                                _targetImage, ScaleMode.ScaleToFit, false);
                GUI.EndGroup();
                GUI.color = new Color(0.91f, 0.72f, 0.32f, 0.9f);
                GUI.DrawTexture(new Rect(targetRect.x - 1f, 0f, 2f, Screen.height),
                                Texture2D.whiteTexture);
            }
            else
            {
                GUI.color = _comparisonMode == 3 ? Color.black
                    : new Color(1f, 1f, 1f, _targetOpacity);
                if (_comparisonMode == 3) GUI.DrawTexture(viewport, Texture2D.whiteTexture);
                GUI.color = _comparisonMode == 3 ? Color.white
                    : new Color(1f, 1f, 1f, _targetOpacity);
                GUI.DrawTexture(viewport, _targetImage, ScaleMode.ScaleToFit, false);
            }
            GUI.color = previous;
        }

        private void PollCommandInbox()
        {
            if (Time.unscaledTime < _nextCommandPoll || string.IsNullOrEmpty(_commandPath)) return;
            _nextCommandPoll = Time.unscaledTime + 0.25f;
            if (!File.Exists(_commandPath)) return;
            try
            {
                string text = File.ReadAllText(_commandPath);
                if (string.IsNullOrWhiteSpace(text) || text == _lastCommandText) return;
                LookdevCommand command = JsonUtility.FromJson<LookdevCommand>(text);
                if (command == null || string.IsNullOrEmpty(command.action)) return;
                _lastCommandText = text;
                ExecuteCommand(command);
                File.Delete(_commandPath);
            }
            catch (Exception exception)
            {
                _exchangeStatus = "COMMAND ERROR · " + exception.Message;
            }
        }

        private void ExecuteCommand(LookdevCommand command)
        {
            string action = command.action.ToLowerInvariant();
            if (action == "apply")
            {
                ApplySettings(command.settings, true);
                _exchangeStatus = "APPLIED · " + command.requestId;
            }
            else if (action == "capture")
            {
                if (command.settings.clearSpan > 0) ApplySettings(command.settings, true);
                _exchangeStatus = "CAPTURING · " + command.requestId;
                StartCoroutine(CaptureWhenReady(string.IsNullOrEmpty(command.name)
                    ? command.requestId : command.name, command.requestId));
            }
            else if (action == "save") SavePreset();
            else if (action == "load") LoadPreset();
            else if (action == "sweep" && !_sweepRunning)
            {
                int parsed = Array.FindIndex(SweepLabels, label =>
                    string.Equals(label, command.sweepAxis, StringComparison.OrdinalIgnoreCase));
                if (parsed >= 0) _sweepParameterIndex = parsed;
                StartCoroutine(RunSweep(command.requestId));
            }
            PublishState(command.requestId);
        }

        private IEnumerator CaptureWhenReady(string label, string requestId = null)
        {
            yield return WaitForSurface();
            yield return new WaitForEndOfFrame();
            string safeLabel = SafeName(string.IsNullOrEmpty(label) ? "capture" : label);
            string stem = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + "-" + safeLabel;
            string pngPath = Path.Combine(_exchangeDirectory, stem + ".png");
            string jsonPath = Path.Combine(_exchangeDirectory, stem + ".json");
            int width = Mathf.Max(512, _camera.pixelWidth);
            int height = Mathf.Max(512, _camera.pixelHeight);
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
            RenderTexture oldTarget = _camera.targetTexture;
            RenderTexture oldActive = RenderTexture.active;
            try
            {
                target.Create();
                _camera.targetTexture = target;
                _camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                image.Apply(false, false);
                File.WriteAllBytes(pngPath, image.EncodeToPNG());
                File.WriteAllText(jsonPath, SettingsJson());
                _exchangeStatus = "CAPTURED · " + Path.GetFileName(pngPath);
                PublishState(requestId, pngPath);
            }
            finally
            {
                _camera.targetTexture = oldTarget;
                RenderTexture.active = oldActive;
                target.Release();
                Destroy(target);
                Destroy(image);
            }
        }

        private IEnumerator WaitForSurface()
        {
            int stable = 0;
            for (int frame = 0; frame < 512 && stable < 3; frame++)
            {
                bool ready = RenderingComposition.TryGetSurfaceBuildStatus(
                        out int knownChunks, out int dirtyChunks, out int residentChunks, out _)
                    && dirtyChunks == 0
                    && residentChunks >= knownChunks;
                stable = ready ? stable + 1 : 0;
                yield return null;
            }
            if (stable < 3)
            {
                RenderingComposition.TryGetSurfaceBuildStatus(
                    out int knownChunks, out int dirtyChunks, out int residentChunks, out _);
                _exchangeStatus = "CAPTURE FAILED · SURFACE DID NOT CONVERGE";
                PublishState();
                throw new InvalidOperationException(
                    $"Surface did not converge: known={knownChunks}, "
                  + $"resident={residentChunks}, dirty={dirtyChunks}.");
            }
        }

        private IEnumerator RunSweep(string requestId = null)
        {
            _sweepRunning = true;
            LookdevSettings original = GetSettings();
            int[] values = SweepValues(_sweepParameterIndex);
            string axis = SweepLabels[_sweepParameterIndex].ToLowerInvariant();
            string directory = Path.Combine(_exchangeDirectory,
                DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-sweep-" + axis);
            Directory.CreateDirectory(directory);
            for (int i = 0; i < values.Length; i++)
            {
                SetSweepValue(_sweepParameterIndex, values[i]);
                Rebuild();
                yield return WaitForSurface();
                yield return CaptureToPath(Path.Combine(directory,
                    $"{i + 1:00}-{axis}-{values[i]}.png"));
                File.WriteAllText(Path.Combine(directory,
                    $"{i + 1:00}-{axis}-{values[i]}.json"), SettingsJson());
            }
            BuildContactSheet(directory, axis, values);
            ApplySettings(original, true);
            _sweepRunning = false;
            _exchangeStatus = "SWEEP READY · " + directory;
            PublishState(requestId, directory);
        }

        private IEnumerator CaptureToPath(string path)
        {
            yield return new WaitForEndOfFrame();
            const int size = 768;
            var target = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32);
            var image = new Texture2D(size, size, TextureFormat.RGBA32, false);
            RenderTexture oldTarget = _camera.targetTexture;
            RenderTexture oldActive = RenderTexture.active;
            try
            {
                target.Create(); _camera.targetTexture = target; _camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, size, size), 0, 0, false);
                image.Apply(false, false); File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                _camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
                target.Release(); Destroy(target); Destroy(image);
            }
        }

        private static void BuildContactSheet(string directory, string axis, int[] values)
        {
            const int tile = 384;
            const int columns = 3;
            const int rows = 2;
            var sheetTarget = new RenderTexture(tile * columns, tile * rows, 0,
                                                RenderTextureFormat.ARGB32);
            var sheet = new Texture2D(tile * columns, tile * rows, TextureFormat.RGBA32, false);
            RenderTexture oldActive = RenderTexture.active;
            var loaded = new Texture2D[values.Length];
            try
            {
                sheetTarget.Create();
                RenderTexture.active = sheetTarget;
                GL.Clear(true, true, new Color(0.055f, 0.060f, 0.055f, 1f));
                GL.PushMatrix();
                GL.LoadPixelMatrix(0f, tile * columns, tile * rows, 0f);
                for (int i = 0; i < values.Length; i++)
                {
                    string candidate = Path.Combine(directory,
                        $"{i + 1:00}-{axis}-{values[i]}.png");
                    loaded[i] = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    loaded[i].LoadImage(File.ReadAllBytes(candidate));
                    int column = i % columns;
                    int row = i / columns;
                    Graphics.DrawTexture(new Rect(column * tile, row * tile, tile, tile), loaded[i]);
                }
                GL.PopMatrix();
                sheet.ReadPixels(new Rect(0, 0, tile * columns, tile * rows), 0, 0, false);
                sheet.Apply(false, false);
                File.WriteAllBytes(Path.Combine(directory, "contact-sheet.png"), sheet.EncodeToPNG());
                File.WriteAllText(Path.Combine(directory, "manifest.json"),
                    JsonUtility.ToJson(new SweepManifest { axis = axis, values = values }, true));
            }
            finally
            {
                RenderTexture.active = oldActive;
                for (int i = 0; i < loaded.Length; i++)
                    if (loaded[i] != null) Destroy(loaded[i]);
                sheetTarget.Release();
                Destroy(sheetTarget);
                Destroy(sheet);
            }
        }

        private static int[] SweepValues(int axis) => axis switch
        {
            0 => new[] { 9, 11, 13, 15, 17 },
            1 => new[] { 2, 3, 4, 5, 6 },
            2 => new[] { 1, 2, 3, 4, 5 },
            _ => new[] { 55, 85, 115, 145, 175 },
        };

        private void SetSweepValue(int axis, int value)
        {
            if (axis == 0) _voussoirs = value;
            else if (axis == 1) _jointQ4 = value;
            else if (axis == 2) _bevelQ4 = value;
            else _mossCoverage = value;
        }

        private void SavePreset()
        {
            File.WriteAllText(_presetPath, SettingsJson());
            _exchangeStatus = "PRESET SAVED · hero-preset.json";
            PublishState();
        }

        private void LoadPreset()
        {
            if (!File.Exists(_presetPath))
            {
                _exchangeStatus = "NO SAVED PRESET";
                return;
            }
            ApplySettings(JsonUtility.FromJson<LookdevSettings>(File.ReadAllText(_presetPath)), true);
            _exchangeStatus = "PRESET LOADED · hero-preset.json";
            PublishState();
        }

        private void PublishState(string requestId = null, string capturePath = null)
        {
            if (string.IsNullOrEmpty(_statePath)) return;
            var state = new LookdevState
            {
                requestId = requestId ?? "", status = _exchangeStatus,
                targetPath = _targetPath ?? "", capturePath = capturePath ?? "",
                commandPath = _commandPath, presetPath = _presetPath,
                lastBuildMs = _lastBuildMs,
                settings = GetSettings(),
            };
            File.WriteAllText(_statePath, JsonUtility.ToJson(state, true));
        }

        private static string SafeName(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars()) value = value.Replace(invalid, '-');
            return value.Replace(' ', '-').ToLowerInvariant();
        }

        private void Section(string label)
        {
            GUILayout.Space(12);
            GUILayout.Label(label, _sectionStyle);
        }

        private void IntSlider(string label, ref int value, int min, int max, int step = 1)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(174));
            GUILayout.Label(value.ToString(), _valueStyle, GUILayout.Width(48));
            GUILayout.EndHorizontal();
            int next = Mathf.RoundToInt(GUILayout.HorizontalSlider(value, min, max) / step) * step;
            next = Mathf.Clamp(next, min, max);
            if (next != value) { value = next; _pendingRebuild = true; _stateDirty = true; }
        }

        private void FloatSlider(string label, ref float value, float min, float max,
                                 bool rebuild, string suffix = "")
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(174));
            GUILayout.Label($"{value:0.00}{suffix}", _valueStyle, GUILayout.Width(74));
            GUILayout.EndHorizontal();
            float next = GUILayout.HorizontalSlider(value, min, max);
            if (Mathf.Abs(next - value) < 0.0001f) return;
            value = next;
            if (rebuild) _pendingRebuild = true;
            _stateDirty = true;
        }

        private void EnsureStyles()
        {
            if (_panelStyle != null) return;
            _panelStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(18, 18, 16, 16),
                normal = { background = MakeTexture(new Color(0.055f, 0.060f, 0.055f, 0.94f)) }
            };
            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 19, fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.91f, 0.82f, 0.60f) }
            };
            _sectionStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11, fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.66f, 0.73f, 0.55f) }
            };
            _valueStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10, alignment = TextAnchor.MiddleRight,
                normal = { textColor = new Color(0.72f, 0.72f, 0.67f) }
            };
            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11, fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.14f, 0.12f, 0.08f) }
            };
        }

        private void ResetDefaults()
        {
            _clearSpan = 32; _pierHeight = 40; _ringThickness = 7; _voussoirs = 13;
            _depth = 12; _shoulder = 10; _topMargin = 8; _faceRecess = 1;
            _plinthHeight = 4; _impostHeight = 3; _damage = 0; _damageScale = 2;
            _seedOffset = 0x2222; _jointQ4 = 4; _bevelQ4 = 4; _projectionQ4 = 8;
            _faceDepthQ4 = 16; _mossCoverage = 115; _mossDensity = 210;
            _mossRadiusQ4 = 18; _mossHeightQ4 = 2; _mossDropQ4 = 18;
            _mossSeparation = 0; _mossHue = 0.22f; _mossSaturation = 0.53f;
            _mossValue = 0.39f; _stoneWarmth = 0.58f; _stoneValue = 0.68f;
            _sunAzimuth = -48f; _sunElevation = 50f; _cameraYaw = 14.5f;
            _cameraPitch = 3.2f; _cameraDistance = 14.5f; _cameraFov = 34f;
            _cameraMoveSpeed = 4f;
            _buildBudgetMs = 12f;
            Rebuild();
            FrameCamera(_lastBayWidth, _lastBayHeight);
        }

        private LookdevSettings GetSettings() => new()
        {
            clearSpan = _clearSpan, pierHeight = _pierHeight,
            ringThickness = _ringThickness, voussoirs = _voussoirs, depth = _depth,
            shoulder = _shoulder, topMargin = _topMargin, faceRecess = _faceRecess,
            plinthHeight = _plinthHeight, impostHeight = _impostHeight,
            damage = _damage, damageScale = _damageScale, seedOffset = _seedOffset,
            jointQ4 = _jointQ4, bevelQ4 = _bevelQ4, projectionQ4 = _projectionQ4,
            faceDepthQ4 = _faceDepthQ4, mossCoverage = _mossCoverage,
            mossDensity = _mossDensity, mossRadiusQ4 = _mossRadiusQ4,
            mossHeightQ4 = _mossHeightQ4, mossDropQ4 = _mossDropQ4,
            mossSeparation = _mossSeparation, mossHue = _mossHue,
            mossSaturation = _mossSaturation, mossValue = _mossValue,
            stoneWarmth = _stoneWarmth, stoneValue = _stoneValue,
            sunAzimuth = _sunAzimuth, sunElevation = _sunElevation,
            cameraYaw = _cameraYaw, cameraPitch = _cameraPitch,
            cameraDistance = _cameraDistance, cameraFov = _cameraFov,
            cameraFocusX = _cameraFocus.x, cameraFocusY = _cameraFocus.y,
            cameraFocusZ = _cameraFocus.z,
            cameraMoveSpeed = _cameraMoveSpeed,
            buildBudgetMs = _buildBudgetMs,
        };

        private string SettingsJson() => JsonUtility.ToJson(GetSettings(), true);

        private void ApplySettings(LookdevSettings settings, bool rebuild)
        {
            _clearSpan = Mathf.Clamp(settings.clearSpan, 16, 56);
            _pierHeight = Mathf.Clamp(settings.pierHeight, 20, 64);
            _ringThickness = Mathf.Clamp(settings.ringThickness, 4, 12);
            _voussoirs = Mathf.Clamp(settings.voussoirs, 7, 25);
            _depth = Mathf.Clamp(settings.depth, 6, 20);
            _shoulder = Mathf.Clamp(settings.shoulder, 4, 20);
            _topMargin = Mathf.Clamp(settings.topMargin, 2, 16);
            _faceRecess = Mathf.Clamp(settings.faceRecess, 1, _depth - 2);
            _plinthHeight = Mathf.Clamp(settings.plinthHeight, 2, 10);
            _impostHeight = Mathf.Clamp(settings.impostHeight, 2, 10);
            _damage = Mathf.Clamp(settings.damage, 0, 4);
            _damageScale = Mathf.Clamp(settings.damageScale, 1, 6);
            _seedOffset = Mathf.Clamp(settings.seedOffset, 0, 65535);
            _jointQ4 = Mathf.Clamp(settings.jointQ4, 1, 12);
            _bevelQ4 = Mathf.Clamp(settings.bevelQ4, 1, 12);
            _projectionQ4 = Mathf.Clamp(settings.projectionQ4, 1, 16);
            _faceDepthQ4 = Mathf.Clamp(settings.faceDepthQ4, 4, 28);
            _mossCoverage = Mathf.Clamp(settings.mossCoverage, 0, 255);
            _mossDensity = Mathf.Clamp(settings.mossDensity, 0, 255);
            _mossRadiusQ4 = Mathf.Clamp(settings.mossRadiusQ4, 4, 32);
            _mossHeightQ4 = Mathf.Clamp(settings.mossHeightQ4, 1, 10);
            _mossDropQ4 = Mathf.Clamp(settings.mossDropQ4, 0, 32);
            _mossSeparation = Mathf.Clamp(settings.mossSeparation, 0, 3);
            _mossHue = Mathf.Clamp(settings.mossHue, 0.12f, 0.38f);
            _mossSaturation = Mathf.Clamp01(settings.mossSaturation);
            _mossValue = Mathf.Clamp(settings.mossValue, 0.1f, 0.8f);
            _stoneWarmth = Mathf.Clamp(settings.stoneWarmth, 0.35f, 0.85f);
            _stoneValue = Mathf.Clamp(settings.stoneValue, 0.35f, 0.9f);
            _sunAzimuth = Mathf.Clamp(settings.sunAzimuth, -180f, 180f);
            _sunElevation = Mathf.Clamp(settings.sunElevation, 10f, 85f);
            _cameraYaw = Mathf.Clamp(settings.cameraYaw, -180f, 180f);
            _cameraPitch = Mathf.Clamp(settings.cameraPitch, -80f, 80f);
            _cameraDistance = Mathf.Clamp(settings.cameraDistance, 1.5f, 40f);
            _cameraFov = Mathf.Clamp(settings.cameraFov, 20f, 60f);
            _cameraFocus = new Vector3(settings.cameraFocusX, settings.cameraFocusY,
                                       settings.cameraFocusZ);
            _cameraInitialized = true;
            _cameraMoveSpeed = Mathf.Clamp(settings.cameraMoveSpeed, 0.5f, 12f);
            _buildBudgetMs = Mathf.Clamp(settings.buildBudgetMs, 0.2f, 20f);
            if (rebuild) Rebuild();
            ApplyPresentation();
        }

        [Serializable]
        private struct LookdevSettings
        {
            public int clearSpan, pierHeight, ringThickness, voussoirs, depth;
            public int shoulder, topMargin, faceRecess, plinthHeight, impostHeight;
            public int damage, damageScale, seedOffset, jointQ4, bevelQ4, projectionQ4;
            public int faceDepthQ4, mossCoverage, mossDensity, mossRadiusQ4;
            public int mossHeightQ4, mossDropQ4, mossSeparation;
            public float mossHue, mossSaturation, mossValue, stoneWarmth, stoneValue;
            public float sunAzimuth, sunElevation, cameraYaw, cameraPitch;
            public float cameraDistance, cameraFov, cameraFocusX, cameraFocusY, cameraFocusZ;
            public float cameraMoveSpeed, buildBudgetMs;
        }

        [Serializable]
        private sealed class LookdevCommand
        {
            public string requestId;
            public string action;
            public string name;
            public string sweepAxis;
            public LookdevSettings settings;
        }

        [Serializable]
        private sealed class LookdevState
        {
            public string requestId;
            public string status;
            public string targetPath;
            public string capturePath;
            public string commandPath;
            public string presetPath;
            public double lastBuildMs;
            public LookdevSettings settings;
        }

        [Serializable]
        private sealed class SweepManifest
        {
            public string axis;
            public int[] values;
        }

        private static Texture2D MakeTexture(Color colour)
        {
            var texture = new Texture2D(1, 1) { hideFlags = HideFlags.DontSave };
            texture.SetPixel(0, 0, colour);
            texture.Apply();
            return texture;
        }
    }
}
