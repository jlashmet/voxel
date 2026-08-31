using System;
using Game.Composition.Materials;
using Game.Materials.Api;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;
using VoxelEngine.Composition;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Built-player visual validation for the shared stylized water renderer. Every visible water
    /// surface is authored into the ordinary ShowcaseWorld voxel store and reaches rendering through
    /// RenderingWorldBinding; this component owns only exhibit placement and inspection camera intent.
    /// </summary>
    [AddComponentMenu("VoxelEngine/Showcases/Water Rendering Showcase")]
    [DisallowMultipleComponent]
    public sealed class WaterRenderingShowcase : MonoBehaviour, IShowcaseMeasurementDriver
    {
        private const uint Seed = 0x57A7E123u;
        private const int BrickPoolCapacity = 32768;
        private const long StorageBudgetBytes = 128L * 1024L * 1024L;
        private const float InspectionSpeed = 8f;
        private const float CaptureTelemetryIntervalSeconds = 10f;
        private const double BytesPerMebibyte = 1024d * 1024d;

        private readonly FrameTiming[] _frameTimings = new FrameTiming[1];
        private ShowcaseWorld _world;
        private Camera _camera;
        private Transform _sun;
        private Vector3 _focus;
        private Vector3 _modeStartPosition;
        private Quaternion _modeStartRotation;
        private float _modeTime;
        private MeasurementMode _measurementMode;
        private bool _ready;
        private bool _unattendedCapture;
        private float _captureTime;
        private byte _captureViewPhase = byte.MaxValue;
        private float _telemetryTime;
        private float _telemetryFrameSeconds;
        private int _telemetryFrameCount;
        private bool _autoWalk;
        private bool _autoSurvey;
        private bool _autoRecede;

        private enum MeasurementMode : byte
        {
            None,
            Movement,
            Recede,
            Survey,
        }

        public bool IsReady => _ready;
        public int ActiveActors => 0;

        public bool AutoWalk
        {
            get => _autoWalk;
            set
            {
                _autoWalk = value;
                if (value) BeginMovement();
                else EndMovement();
            }
        }

        public bool AutoSurvey
        {
            get => _autoSurvey;
            set
            {
                _autoSurvey = value;
                if (value) BeginSurvey();
                else EndSurvey();
            }
        }

        public bool AutoRecede
        {
            get => _autoRecede;
            set
            {
                _autoRecede = value;
                if (value) BeginRecede();
                else EndRecede();
            }
        }

        public float SurveyHeightMetres { get; set; } = 12f;
        public float SurveySpinDegreesPerSecond { get; set; } = 10f;
        public float RecedeSpeedMetresPerSecond { get; set; } = 4f;
        public float RecedeMaxDistanceMetres { get; set; } = 18f;

        public float DistanceToLandmarkMetres
        {
            get
            {
                if (_camera == null) return 0f;
                Vector3 delta = _camera.transform.position - _focus;
                delta.y = 0f;
                return delta.magnitude;
            }
        }

        public string DescribeFarTerrain() => "FAR disabled water-showcase";

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            RenderingComposition.ResetSurfacePassDiagnostics("water-showcase-enabled");
            RenderingComposition.SetSurfaceBuildEnabled(true);
            RenderingComposition.SetFarBaseHeight(ShowcaseWorld.BaseHeightVoxels);
            RenderingComposition.SetVoxelRingRadiusMetres(ShowcaseWorld.RegionMetres);
            RenderingComposition.SetVoxelLodEnabled(false);
            RenderingComposition.SetVoxelDetailBandScale(1f);

            _world = new ShowcaseWorld(
                Seed,
                BrickPoolCapacity,
                loadRadiusRegions: 1,
                unloadRadiusRegions: 2,
                GameMaterialComposition.SimulationDefinitions(),
                StorageBudgetBytes,
                ShowcaseFeatureContent.HouseOnly,
                ShowcaseStartupSource.Generate);

            _world.GenerateRegionBlocking(int3.zero);
            AuthorExhibits();

            var renderingWorld = new RenderingWorldBinding(
                _world.ReadStorage,
                _world.Palette,
                _world.SurfaceRules,
                _world.CoatingRules,
                _world.ProfileBlocks);
            RenderingComposition.ConfigureWorld(
                in renderingWorld, _world.Changes, _world.Seed, farFieldEnabled: false);

            CreateLighting();
            _camera = CreateCamera();
            string requestedView = Environment.GetEnvironmentVariable("VOXEL_WATER_SHOWCASE_VIEW");
            _unattendedCapture = string.IsNullOrWhiteSpace(requestedView)
                && HasCommandLineArgument("-voxel-screenshot-dir");
            _captureTime = 0f;
            _captureViewPhase = byte.MaxValue;
            ResetCaptureTelemetry();
            ApplyView(_unattendedCapture ? "near" : requestedView ?? "wide");
            if (_unattendedCapture)
                _captureViewPhase = 0;
            _ready = true;

            Debug.Log(
                "WATER_RENDERING_SHOWCASE ready: canonical voxel storage + RenderingWorldBinding; " +
                "profiles=still,river,waterfall; views=near,wide,elevated,waterfall.");
        }

        private void OnDisable()
        {
            _ready = false;
            _measurementMode = MeasurementMode.None;
            _autoWalk = false;
            _autoSurvey = false;
            _autoRecede = false;
            _unattendedCapture = false;
            _captureTime = 0f;
            _captureViewPhase = byte.MaxValue;
            ResetCaptureTelemetry();

            _world?.StopBackgroundWork();
            RenderingComposition.ResetTransientPresentation();
            RenderingComposition.ClearWorld();
            RenderingComposition.SetSurfaceBuildEnabled(true);

            if (_camera != null)
                Destroy(_camera.gameObject);
            _camera = null;
            if (_sun != null)
                Destroy(_sun.gameObject);
            _sun = null;

            _world?.Dispose();
            _world = null;
        }

        private void AuthorExhibits()
        {
            // A raised, stepped stone shelf keeps the visual comparison deterministic while the
            // ordinary generated terrain and detailed-house feature remain visible around it.
            int lakeY = MaxSurfaceHeight(24, 184, 42, 182) + 6;
            _world.AuthorVoxelBox(new int3(24, lakeY - 3, 42), new int3(160, 3, 140), GameMaterialIds.DarkStone);
            _world.AuthorVoxelBox(new int3(24, lakeY, 42), new int3(160, 4, 104), GameMaterialIds.Water);
            _world.AuthorVoxelBox(new int3(24, lakeY, 146), new int3(160, 3, 18), GameMaterialIds.Water);
            _world.AuthorVoxelBox(new int3(24, lakeY, 164), new int3(160, 2, 10), GameMaterialIds.Water);
            _world.AuthorVoxelBox(new int3(24, lakeY, 174), new int3(160, 1, 8), GameMaterialIds.Water);

            // Stone shoreline bands make the decreasing depth and contact foam legible from the
            // same camera without replacing the generated terrain with a custom mesh.
            _world.AuthorVoxelBox(new int3(20, lakeY - 1, 38), new int3(168, 2, 4), GameMaterialIds.Stone);
            _world.AuthorVoxelBox(new int3(20, lakeY - 1, 182), new int3(168, 2, 4), GameMaterialIds.Stone);

            int riverY = MaxSurfaceHeight(204, 446, 56, 132) + 8;
            _world.AuthorVoxelBox(new int3(204, riverY - 2, 68), new int3(242, 2, 56), GameMaterialIds.DarkStone);
            _world.AuthorVoxelBox(new int3(204, riverY, 78), new int3(242, 2, 36), GameMaterialIds.RiverWater);
            _world.AuthorVoxelBox(new int3(204, riverY, 68), new int3(242, 5, 8), GameMaterialIds.Stone);
            _world.AuthorVoxelBox(new int3(204, riverY, 116), new int3(242, 5, 8), GameMaterialIds.Stone);

            // A masonry bridge is ordinary voxel structure contact: water remains authored with
            // the same semantic river material immediately beneath representative built geometry.
            _world.AuthorVoxelBox(new int3(300, riverY + 2, 66), new int3(22, 4, 60), GameMaterialIds.MasonryMedium);
            _world.AuthorVoxelBox(new int3(304, riverY - 1, 66), new int3(5, 8, 60), GameMaterialIds.MasonryLarge);
            _world.AuthorVoxelBox(new int3(313, riverY - 1, 66), new int3(5, 8, 60), GameMaterialIds.MasonryLarge);

            int fallBaseY = MaxSurfaceHeight(304, 424, 194, 276) + 5;
            const int cliffHeight = 72;
            _world.AuthorVoxelBox(new int3(304, fallBaseY, 224), new int3(120, cliffHeight, 18), GameMaterialIds.DarkStone);
            _world.AuthorVoxelBox(new int3(329, fallBaseY + cliffHeight - 4, 205), new int3(70, 4, 19), GameMaterialIds.RiverWater);

            // Compose one connected fall from a few overlapping semantic bands rather than a row
            // of shallow parallel ribbons. Keep every lower boundary one voxel above the receiving
            // river so canonical Cascade impact topology and spray are generated at the pool rather
            // than several voxels up the cliff; width/front-depth variation supplies the silhouette.
            _world.AuthorVoxelBox(new int3(333, fallBaseY + 9, 199), new int3(22, 59, 3), GameMaterialIds.Cascade);
            _world.AuthorVoxelBox(new int3(349, fallBaseY + 9, 198), new int3(24, 63, 4), GameMaterialIds.Cascade);
            _world.AuthorVoxelBox(new int3(368, fallBaseY + 9, 199), new int3(23, 63, 3), GameMaterialIds.Cascade);
            _world.AuthorVoxelBox(new int3(384, fallBaseY + 9, 198), new int3(13, 59, 4), GameMaterialIds.Cascade);

            _world.AuthorVoxelBox(new int3(318, fallBaseY + 2, 176), new int3(92, 4, 49), GameMaterialIds.Cascade);
            _world.AuthorVoxelBox(new int3(324, fallBaseY + 6, 184), new int3(80, 2, 33), GameMaterialIds.RiverWater);

            _focus = new Vector3(25.5f, (lakeY + 18) * ShowcaseWorld.VoxelSize, 16.8f);
        }

        private int MaxSurfaceHeight(int minX, int maxX, int minZ, int maxZ)
        {
            int max = 0;
            for (int z = minZ; z <= maxZ; z += 8)
            for (int x = minX; x <= maxX; x += 8)
                max = math.max(max, _world.SurfaceHeight(x, z));
            return max;
        }

        private Camera CreateCamera()
        {
            GameObject cameraObject = new GameObject("Water Showcase Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(transform, false);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.22f, 0.34f, 0.48f, 1f);
            camera.fieldOfView = 55f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 350f;
            cameraObject.AddComponent<AudioListener>();
            return camera;
        }

        private void CreateLighting()
        {
            RenderSettings.ambientLight = new Color(0.38f, 0.43f, 0.50f, 1f);
            GameObject lightObject = new GameObject("Water Showcase Sun");
            lightObject.transform.SetParent(transform, false);
            Light sun = lightObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(52f, -28f, 0f);
            _sun = lightObject.transform;
        }

        private void Update()
        {
            if (!_ready || _camera == null)
                return;

            if (_measurementMode != MeasurementMode.None)
            {
                UpdateMeasurement();
                return;
            }

            if (_unattendedCapture)
            {
                UpdateUnattendedCaptureView();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Alpha1)) ApplyView("near");
            if (Input.GetKeyDown(KeyCode.Alpha2)) ApplyView("wide");
            if (Input.GetKeyDown(KeyCode.Alpha3)) ApplyView("elevated");
            if (Input.GetKeyDown(KeyCode.Alpha4)) ApplyView("waterfall");

            float speed = InspectionSpeed * (Input.GetKey(KeyCode.LeftShift) ? 2.5f : 1f) * Time.deltaTime;
            Vector3 delta = Vector3.zero;
            if (Input.GetKey(KeyCode.W)) delta += _camera.transform.forward;
            if (Input.GetKey(KeyCode.S)) delta -= _camera.transform.forward;
            if (Input.GetKey(KeyCode.D)) delta += _camera.transform.right;
            if (Input.GetKey(KeyCode.A)) delta -= _camera.transform.right;
            if (Input.GetKey(KeyCode.E)) delta += Vector3.up;
            if (Input.GetKey(KeyCode.Q)) delta -= Vector3.up;
            if (delta.sqrMagnitude > 0f)
                _camera.transform.position += delta.normalized * speed;
        }

        private void UpdateUnattendedCaptureView()
        {
            FrameTimingManager.CaptureFrameTimings();
            float deltaTime = Time.unscaledDeltaTime;
            _captureTime += deltaTime;
            _telemetryTime += deltaTime;
            _telemetryFrameSeconds += deltaTime;
            _telemetryFrameCount++;
            if (_telemetryTime >= CaptureTelemetryIntervalSeconds)
                EmitCaptureTelemetry();

            // The first 2-second capture occurs before cold-view convergence. Hold each useful
            // framing long enough that the 10-second cadence records a converged near view at 12s,
            // a wide comparison at 22s, then four time-separated waterfall frames from 32s onward.
            byte phase = _captureTime < 18f ? (byte)0 : _captureTime < 30f ? (byte)1 : (byte)2;
            if (phase == _captureViewPhase)
                return;

            _captureViewPhase = phase;
            ApplyView(phase == 0 ? "near" : phase == 1 ? "wide" : "waterfall");
        }

        private void EmitCaptureTelemetry()
        {
            uint timingCount = FrameTimingManager.GetLatestTimings(1, _frameTimings);
            double cpuFrameMs = timingCount > 0 ? _frameTimings[0].cpuFrameTime : -1d;
            double gpuFrameMs = timingCount > 0 ? _frameTimings[0].gpuFrameTime : -1d;
            double averageFrameMs = _telemetryFrameCount > 0
                ? _telemetryFrameSeconds * 1000d / _telemetryFrameCount
                : -1d;
            double allocatedMiB = Profiler.GetTotalAllocatedMemoryLong() / BytesPerMebibyte;
            double reservedMiB = Profiler.GetTotalReservedMemoryLong() / BytesPerMebibyte;
            double monoUsedMiB = Profiler.GetMonoUsedSizeLong() / BytesPerMebibyte;

            Debug.Log(
                $"WATER_RENDERING_SHOWCASE_METRICS elapsed={_captureTime:F1}s " +
                $"avgFrameMs={averageFrameMs:F3} cpuFrameMs={cpuFrameMs:F3} gpuFrameMs={gpuFrameMs:F3} " +
                $"allocatedMiB={allocatedMiB:F1} reservedMiB={reservedMiB:F1} monoUsedMiB={monoUsedMiB:F1} " +
                $"graphicsDevice={SystemInfo.graphicsDeviceName}");

            _telemetryTime = 0f;
            _telemetryFrameSeconds = 0f;
            _telemetryFrameCount = 0;
        }

        private void ResetCaptureTelemetry()
        {
            _telemetryTime = 0f;
            _telemetryFrameSeconds = 0f;
            _telemetryFrameCount = 0;
        }

        private static bool HasCommandLineArgument(string expected)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int i = 0; i < arguments.Length; i++)
            {
                if (string.Equals(arguments[i], expected, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private void ApplyView(string view)
        {
            view = string.IsNullOrEmpty(view) ? "wide" : view.Trim().ToLowerInvariant();
            Vector3 position;
            Vector3 target;
            switch (view)
            {
                case "near":
                    position = new Vector3(11f, 26.5f, 5.5f);
                    target = new Vector3(10f, 24.5f, 12.5f);
                    break;
                case "elevated":
                    position = new Vector3(25.5f, 48f, 0.5f);
                    target = new Vector3(25.5f, 24f, 16.5f);
                    break;
                case "waterfall":
                    // Face the semantic cascade sheet nearly square-on so vertical motion, edge
                    // breakup and impact treatment are judged rather than hidden by cliff parallax.
                    position = new Vector3(36.3f, 28.7f, 14.7f);
                    target = new Vector3(36.3f, 27.0f, 20.0f);
                    break;
                default:
                    position = new Vector3(25.5f, 38f, -1.5f);
                    target = _focus;
                    view = "wide";
                    break;
            }

            _camera.transform.SetPositionAndRotation(
                position, Quaternion.LookRotation(target - position, Vector3.up));
            Debug.Log($"WATER_RENDERING_SHOWCASE view={view} position={position}");
        }

        public void BeginMovement() => BeginMeasurement(MeasurementMode.Movement);
        public void BeginRecede() => BeginMeasurement(MeasurementMode.Recede);
        public void BeginSurvey() => BeginMeasurement(MeasurementMode.Survey);
        public void EndMovement() => EndMeasurement(MeasurementMode.Movement);
        public void EndRecede() => EndMeasurement(MeasurementMode.Recede);
        public void EndSurvey() => EndMeasurement(MeasurementMode.Survey);

        private void BeginMeasurement(MeasurementMode mode)
        {
            if (!_ready || _camera == null)
                return;
            _modeStartPosition = _camera.transform.position;
            _modeStartRotation = _camera.transform.rotation;
            _modeTime = 0f;
            _measurementMode = mode;
        }

        private void EndMeasurement(MeasurementMode mode)
        {
            if (_measurementMode != mode || _camera == null)
                return;
            _camera.transform.SetPositionAndRotation(_modeStartPosition, _modeStartRotation);
            _measurementMode = MeasurementMode.None;
            _modeTime = 0f;
        }

        private void UpdateMeasurement()
        {
            _modeTime += Time.deltaTime;
            switch (_measurementMode)
            {
                case MeasurementMode.Movement:
                    _camera.transform.position = _modeStartPosition
                        + _camera.transform.right * Mathf.Sin(_modeTime * 0.7f) * 7f;
                    break;
                case MeasurementMode.Recede:
                    _camera.transform.position = _modeStartPosition
                        - _camera.transform.forward * Mathf.Min(_modeTime * RecedeSpeedMetresPerSecond, RecedeMaxDistanceMetres)
                        + Vector3.up * Mathf.Min(_modeTime * 1.4f, 7f);
                    break;
                case MeasurementMode.Survey:
                    float angle = _modeTime * SurveySpinDegreesPerSecond * Mathf.Deg2Rad;
                    Vector3 offset = new Vector3(Mathf.Sin(angle) * 18f, SurveyHeightMetres, -Mathf.Cos(angle) * 18f);
                    _camera.transform.position = _focus + offset;
                    _camera.transform.rotation = Quaternion.LookRotation(_focus - _camera.transform.position, Vector3.up);
                    break;
            }
        }
    }
}
