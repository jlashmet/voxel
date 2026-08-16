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
    /// <summary>Interactive production-path look-development bench for the hero arch.</summary>
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
            Quaternion rotation = Quaternion.Euler(_cameraPitch, _cameraYaw, 0f);
            _camera.transform.position = _cameraFocus - rotation * Vector3.forward * _cameraDistance;
            _camera.transform.rotation = rotation;
        }

        private void HandleInspectionCamera()
        {
            if (Input.GetMouseButton(1))
            {
                _cameraYaw += Input.GetAxis("Mouse X") * 1.8f;
                _cameraPitch = Mathf.Clamp(_cameraPitch - Input.GetAxis("Mouse Y") * 1.5f, -55f, 70f);
            }
            Vector3 right = _camera.transform.right;
            Vector3 forward = Vector3.ProjectOnPlane(_camera.transform.forward, Vector3.up).normalized;
            float dt = Time.unscaledDeltaTime;
            if (Input.GetKey(KeyCode.W)) _cameraFocus += forward * _cameraMoveSpeed * dt;
            if (Input.GetKey(KeyCode.S)) _cameraFocus -= forward * _cameraMoveSpeed * dt;
            if (Input.GetKey(KeyCode.A)) _cameraFocus -= right * _cameraMoveSpeed * dt;
            if (Input.GetKey(KeyCode.D)) _cameraFocus += right * _cameraMoveSpeed * dt;
            if (Input.GetKey(KeyCode.E)) _cameraFocus += Vector3.up * _cameraMoveSpeed * dt;
            if (Input.GetKey(KeyCode.Q)) _cameraFocus -= Vector3.up * _cameraMoveSpeed * dt;
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.001f) _cameraDistance = Mathf.Max(1.5f, _cameraDistance - scroll * 0.8f);
            ApplyCameraTransform();
        }

        private void OnGUI()
        {
            EnsureStyles();
            DrawComparison();

            if (!_panelVisible) return;
            GUILayout.BeginArea(new Rect(12f, 12f, PanelWidth, Screen.height - 24f), _panelStyle);
            _scroll = GUILayout.BeginScrollView(_scroll);

            GUILayout.Label("ARCH LOOKDEV", _titleStyle);
            GUILayout.Label(_status, _valueStyle);
            GUILayout.Space(8f);
            DrawFormControls();
            DrawGrowthControls();
            DrawPresentationControls();
            DrawComparisonControls();
            DrawAutomationControls();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawFormControls()
        {
            GUILayout.Label("FORM", _sectionStyle);
            SliderInt("Clear span", ref _clearSpan, 18, 56);
            SliderInt("Pier height", ref _pierHeight, 20, 64);
            SliderInt("Ring thickness", ref _ringThickness, 4, 12);
            SliderInt("Depth", ref _depth, 6, 20);
            SliderInt("Voussoirs", ref _voussoirs, 5, 21);
            SliderInt("Shoulder", ref _shoulder, 4, 18);
            SliderInt("Top margin", ref _topMargin, 3, 14);
            SliderInt("Face recess", ref _faceRecess, 0, 4);
            SliderInt("Plinth height", ref _plinthHeight, 0, 8);
            SliderInt("Impost height", ref _impostHeight, 0, 6);
            SliderInt("Damage", ref _damage, 0, 3);
            SliderInt("Damage scale", ref _damageScale, 1, 4);
            SliderInt("Damage seed", ref _seedOffset, 0, 0xffff);
            GUILayout.Space(6f);
            GUILayout.Label("PROFILE", _sectionStyle);
            SliderInt("Joint half-width", ref _jointQ4, 0, 12);
            SliderInt("Bevel", ref _bevelQ4, 0, 12);
            SliderInt("Projection", ref _projectionQ4, -24, 24);
            SliderInt("Face depth", ref _faceDepthQ4, 1, 48);
        }

        private void DrawGrowthControls()
        {
            GUILayout.Label("GROWTH", _sectionStyle);
            SliderInt("Coverage", ref _mossCoverage, 0, 255);
            SliderInt("Density", ref _mossDensity, 0, 255);
            SliderInt("Radius", ref _mossRadiusQ4, 0, 64);
            SliderInt("Height", ref _mossHeightQ4, 0, 64);
            SliderInt("Drop", ref _mossDropQ4, 0, 96);
            SliderInt("Separation", ref _mossSeparation, 0, 32);
            SliderFloat("Hue", ref _mossHue, 0.14f, 0.32f);
            SliderFloat("Saturation", ref _mossSaturation, 0.2f, 0.9f);
            SliderFloat("Value", ref _mossValue, 0.2f, 0.75f);
            GUILayout.Space(6f);
        }

        private void DrawPresentationControls()
        {
            GUILayout.Label("PRESENTATION", _sectionStyle);
            SliderFloat("Stone warmth", ref _stoneWarmth, 0f, 1f);
            SliderFloat("Stone value", ref _stoneValue, 0.25f, 1f);
            SliderFloat("Sun azimuth", ref _sunAzimuth, -180f, 180f);
            SliderFloat("Sun elevation", ref _sunElevation, 5f, 85f);
            SliderFloat("Camera yaw", ref _cameraYaw, -180f, 180f);
            SliderFloat("Camera pitch", ref _cameraPitch, -40f, 40f);
            SliderFloat("Camera distance", ref _cameraDistance, 4f, 30f);
            SliderFloat("Camera FOV", ref _cameraFov, 20f, 70f);
            SliderFloat("Move speed", ref _cameraMoveSpeed, 0.5f, 15f);
            SliderFloat("Build budget (ms)", ref _buildBudgetMs, 1f, 25f);
        }

        private void DrawComparisonControls()
        {
            GUILayout.Label("REFERENCE", _sectionStyle);
            if (_targetImage == null)
                GUILayout.Label("No target image loaded", _valueStyle);
            else
                GUILayout.Label($"{_targetImage.width} × {_targetImage.height}", _valueStyle);
            SliderInt("Mode", ref _comparisonMode, 0, 2);
            SliderFloat("Opacity", ref _targetOpacity, 0f, 1f);
        }

        private void DrawAutomationControls()
        {
            GUILayout.Label("AUTOMATION", _sectionStyle);
            GUILayout.Label(_exchangeStatus, _valueStyle);
            if (GUILayout.Button("Save preset", _buttonStyle)) SavePreset();
            if (GUILayout.Button("Run parameter sweep", _buttonStyle) && !_sweepRunning)
                StartCoroutine(RunParameterSweep());
        }

        private IEnumerator RunParameterSweep()
        {
            _sweepRunning = true;
            try
            {
                int originalVoussoirs = _voussoirs;
                int originalJoint = _jointQ4;
                int originalBevel = _bevelQ4;
                int originalMoss = _mossCoverage;
                for (int i = 0; i < 4; i++)
                {
                    _sweepParameterIndex = i;
                    switch (i)
                    {
                        case 0:
                            foreach (int value in new[] { 7, 9, 11, 13, 15, 17 })
                            {
                                _voussoirs = value; Rebuild(); yield return null;
                                SaveSweepFrame(SweepLabels[i], value);
                            }
                            break;
                        case 1:
                            foreach (int value in new[] { 1, 2, 3, 4, 6, 8 })
                            {
                                _jointQ4 = value; Rebuild(); yield return null;
                                SaveSweepFrame(SweepLabels[i], value);
                            }
                            break;
                        case 2:
                            foreach (int value in new[] { 1, 2, 3, 4, 6, 8 })
                            {
                                _bevelQ4 = value; Rebuild(); yield return null;
                                SaveSweepFrame(SweepLabels[i], value);
                            }
                            break;
                        default:
                            foreach (int value in new[] { 0, 64, 96, 128, 160, 192, 224 })
                            {
                                _mossCoverage = value; Rebuild(); yield return null;
                                SaveSweepFrame(SweepLabels[i], value);
                            }
                            break;
                    }
                }
                _voussoirs = originalVoussoirs;
                _jointQ4 = originalJoint;
                _bevelQ4 = originalBevel;
                _mossCoverage = originalMoss;
                Rebuild();
            }
            finally { _sweepRunning = false; }
        }

        private void SaveSweepFrame(string label, int value)
        {
            string dir = Path.Combine(_exchangeDirectory, "sweeps");
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, $"{_sweepParameterIndex}-{label}-{value}.png");
            ScreenCapture.CaptureScreenshot(file);
        }

        private void EnsureStyles()
        {
            if (_panelStyle != null) return;
            _panelStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(14, 14, 14, 14),
                normal = { background = Texture2D.blackTexture }
            };
            _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
            _sectionStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold };
            _valueStyle = new GUIStyle(GUI.skin.label) { fontSize = 11 };
            _buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 11 };
        }

        private static void SliderInt(string label, ref int value, int min, int max)
        {
            GUILayout.Label($"{label}: {value}", GUI.skin.label);
            value = Mathf.RoundToInt(GUILayout.HorizontalSlider(value, min, max));
        }

        private static void SliderFloat(string label, ref float value, float min, float max)
        {
            GUILayout.Label($"{label}: {value:0.00}", GUI.skin.label);
            value = GUILayout.HorizontalSlider(value, min, max);
        }

        private void ApplyPresentation()
        {
            RenderingComposition.SetMaterialAlbedo(StoneMaterial, new Vector4(
                0.48f + 0.28f * _stoneWarmth,
                0.42f + 0.23f * _stoneWarmth,
                0.34f + 0.16f * _stoneWarmth,
                1f) * _stoneValue);
            RenderingComposition.SetCoatingTint(
                Coatings.Moss,
                Color.HSVToRGB(_mossHue, _mossSaturation, _mossValue));
            RenderingComposition.SetBuildBudgets(_buildBudgetMs, 0);
            _camera.fieldOfView = _cameraFov;
        }

        private void InitialiseExchange()
        {
            _exchangeDirectory = Path.Combine(Application.dataPath, "../Artifacts/ArchLookdev");
            Directory.CreateDirectory(_exchangeDirectory);
            _commandPath = Path.Combine(_exchangeDirectory, "command.json");
            _statePath = Path.Combine(_exchangeDirectory, "state.json");
            _presetPath = Path.Combine(_exchangeDirectory, "preset.json");
            _targetPath = Path.Combine(Application.dataPath, "Tests/References/arch-target.png");
        }

        private void LoadTargetImage()
        {
            if (!File.Exists(_targetPath)) return;
            byte[] bytes = File.ReadAllBytes(_targetPath);
            _targetImage = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            _targetImage.LoadImage(bytes);
        }

        private void DrawComparison()
        {
            if (_targetImage == null || _comparisonMode == 0) return;
            Rect rect = new Rect(0, 0, Screen.width, Screen.height);
            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, _targetOpacity);
            if (_comparisonMode == 1) GUI.DrawTexture(rect, _targetImage, ScaleMode.ScaleToFit, true);
            else
            {
                Rect half = new Rect(0, 0, Screen.width * 0.5f, Screen.height);
                GUI.DrawTexture(half, _targetImage, ScaleMode.ScaleToFit, true);
            }
            GUI.color = previous;
        }

        private void PollCommandInbox()
        {
            if (Time.unscaledTime < _nextCommandPoll) return;
            _nextCommandPoll = Time.unscaledTime + 0.2f;
            if (!File.Exists(_commandPath)) return;
            string text = File.ReadAllText(_commandPath);
            if (string.IsNullOrWhiteSpace(text) || text == _lastCommandText) return;
            _lastCommandText = text;
            ApplyCommand(text);
        }

        private void ApplyCommand(string json)
        {
            ArchLookdevCommand command = JsonUtility.FromJson<ArchLookdevCommand>(json);
            if (command == null) return;
            if (command.clearSpan > 0) _clearSpan = command.clearSpan;
            if (command.pierHeight > 0) _pierHeight = command.pierHeight;
            if (command.ringThickness > 0) _ringThickness = command.ringThickness;
            if (command.voussoirs > 0) _voussoirs = command.voussoirs;
            if (command.depth > 0) _depth = command.depth;
            if (command.shoulder >= 0) _shoulder = command.shoulder;
            if (command.topMargin >= 0) _topMargin = command.topMargin;
            if (command.faceRecess >= 0) _faceRecess = command.faceRecess;
            if (command.plinthHeight >= 0) _plinthHeight = command.plinthHeight;
            if (command.impostHeight >= 0) _impostHeight = command.impostHeight;
            if (command.damage >= 0) _damage = command.damage;
            if (command.damageScale > 0) _damageScale = command.damageScale;
            if (command.seedOffset >= 0) _seedOffset = command.seedOffset;
            if (command.jointQ4 >= 0) _jointQ4 = command.jointQ4;
            if (command.bevelQ4 >= 0) _bevelQ4 = command.bevelQ4;
            if (command.projectionQ4 != 0) _projectionQ4 = command.projectionQ4;
            if (command.faceDepthQ4 > 0) _faceDepthQ4 = command.faceDepthQ4;
            if (command.mossCoverage >= 0) _mossCoverage = command.mossCoverage;
            if (command.mossDensity >= 0) _mossDensity = command.mossDensity;
            if (command.mossRadiusQ4 >= 0) _mossRadiusQ4 = command.mossRadiusQ4;
            if (command.mossHeightQ4 >= 0) _mossHeightQ4 = command.mossHeightQ4;
            if (command.mossDropQ4 >= 0) _mossDropQ4 = command.mossDropQ4;
            if (command.mossSeparation >= 0) _mossSeparation = command.mossSeparation;
            if (command.mossHue >= 0) _mossHue = command.mossHue;
            if (command.mossSaturation >= 0) _mossSaturation = command.mossSaturation;
            if (command.mossValue >= 0) _mossValue = command.mossValue;
            if (command.sunAzimuth > -999) _sunAzimuth = command.sunAzimuth;
            if (command.sunElevation > 0) _sunElevation = command.sunElevation;
            if (command.cameraYaw > -999) _cameraYaw = command.cameraYaw;
            if (command.cameraPitch > -999) _cameraPitch = command.cameraPitch;
            if (command.cameraDistance > 0) _cameraDistance = command.cameraDistance;
            if (command.cameraFov > 0) _cameraFov = command.cameraFov;
            _pendingRebuild = true;
        }

        private void PublishState()
        {
            var state = new ArchLookdevState
            {
                clearSpan = _clearSpan,
                pierHeight = _pierHeight,
                ringThickness = _ringThickness,
                voussoirs = _voussoirs,
                depth = _depth,
                shoulder = _shoulder,
                topMargin = _topMargin,
                faceRecess = _faceRecess,
                plinthHeight = _plinthHeight,
                impostHeight = _impostHeight,
                damage = _damage,
                damageScale = _damageScale,
                seedOffset = _seedOffset,
                jointQ4 = _jointQ4,
                bevelQ4 = _bevelQ4,
                projectionQ4 = _projectionQ4,
                faceDepthQ4 = _faceDepthQ4,
                mossCoverage = _mossCoverage,
                mossDensity = _mossDensity,
                mossRadiusQ4 = _mossRadiusQ4,
                mossHeightQ4 = _mossHeightQ4,
                mossDropQ4 = _mossDropQ4,
                mossSeparation = _mossSeparation,
                mossHue = _mossHue,
                mossSaturation = _mossSaturation,
                mossValue = _mossValue,
                sunAzimuth = _sunAzimuth,
                sunElevation = _sunElevation,
                cameraYaw = _cameraYaw,
                cameraPitch = _cameraPitch,
                cameraDistance = _cameraDistance,
                cameraFov = _cameraFov,
                lastBuildMs = _lastBuildMs,
            };
            File.WriteAllText(_statePath, JsonUtility.ToJson(state, true));
            _exchangeStatus = "JSON LINK ACTIVE";
        }

        private void SavePreset()
        {
            PublishState();
            File.WriteAllText(_presetPath, File.ReadAllText(_statePath));
            _exchangeStatus = "PRESET SAVED";
        }

        [Serializable]
        private sealed class ArchLookdevCommand
        {
            public int clearSpan, pierHeight, ringThickness, voussoirs, depth;
            public int shoulder = -1, topMargin = -1, faceRecess = -1, plinthHeight = -1, impostHeight = -1;
            public int damage = -1, damageScale, seedOffset = -1;
            public int jointQ4 = -1, bevelQ4 = -1, projectionQ4, faceDepthQ4;
            public int mossCoverage = -1, mossDensity = -1, mossRadiusQ4 = -1;
            public int mossHeightQ4 = -1, mossDropQ4 = -1, mossSeparation = -1;
            public float mossHue = -1, mossSaturation = -1, mossValue = -1;
            public float sunAzimuth = -999, sunElevation, cameraYaw = -999, cameraPitch = -999;
            public float cameraDistance, cameraFov;
        }

        [Serializable]
        private sealed class ArchLookdevState
        {
            public int clearSpan, pierHeight, ringThickness, voussoirs, depth;
            public int shoulder, topMargin, faceRecess, plinthHeight, impostHeight;
            public int damage, damageScale, seedOffset;
            public int jointQ4, bevelQ4, projectionQ4, faceDepthQ4;
            public int mossCoverage, mossDensity, mossRadiusQ4, mossHeightQ4, mossDropQ4, mossSeparation;
            public float mossHue, mossSaturation, mossValue;
            public float sunAzimuth, sunElevation, cameraYaw, cameraPitch, cameraDistance, cameraFov;
            public double lastBuildMs;
        }
    }
}