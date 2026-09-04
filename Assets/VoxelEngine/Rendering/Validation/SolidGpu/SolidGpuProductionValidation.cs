using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;
using VoxelEngine.Composition;
using VoxelEngine.Rendering.Runtime;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Rendering.Validation
{
    /// <summary>
    /// Rendering-owned production validation for the solid GPU surface backend.
    ///
    /// This fixture owns only composition and evidence. It creates authoritative Storage through
    /// VoxelEngineBootstrap, publishes semantic voxel blocks, binds them through RenderingComposition,
    /// and observes the same VoxelRenderBridge metrics used by production scenes. No alternate
    /// extractor, renderer, mirror, or scene-specific GPU enabling path exists here.
    /// </summary>
    [AddComponentMenu("VoxelEngine/Validation/Solid GPU Production Validation")]
    public sealed class SolidGpuProductionValidation : MonoBehaviour
    {
        private enum Stage
        {
            InitialConvergence,
            Traversal,
            EditConvergence,
            SettledMeasurement,
            RestartGap,
            RestartConvergence,
            Complete,
            Failed,
        }

        private const byte SmoothMaterial = 1;
        private const byte PlanarMaterial = 2;
        private const float VoxelSize = 0.1f;
        private const float BlockMetres = VoxelReadGrid.BlockEdge * VoxelSize;
        private const float StageTimeoutSeconds = 14f;
        private const float TraversalSeconds = 6f;
        private const int SettledFramesRequired = 160;
        private const long PcSurfaceGeometryBudgetBytes = 1280L * 1024L * 1024L;

        private readonly float[] _frameSamplesMs = new float[256];
        private IVoxelStorageRuntime _storage;
        private Camera _camera;
        private Stage _stage;
        private float _stageStartedAt;
        private int _settledFrames;
        private int _frameSampleCount;
        private ulong _beforeEditGpuBuilds;
        private bool _loggedFailure;

        private static readonly Vector3 Target =
            new(0f, BlockMetres * 2.0f, 0f);
        private static readonly int3 EditedBlock = new(0, 2, -5);

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            if (!SystemInfo.supportsComputeShaders)
            {
                Fail("compute shaders are unavailable on this supported validation path");
                return;
            }

            ConfigureProductionRenderer();
            EnsureCamera();
            CreateAndBindWorld();
            BeginStage(Stage.InitialConvergence);
        }

        private void Update()
        {
            if (!Application.isPlaying || _stage == Stage.Complete || _stage == Stage.Failed)
                return;

            if (HasHardGpuFailure(out string failure))
            {
                Fail(failure);
                return;
            }

            if (_stage != Stage.Traversal && _stage != Stage.SettledMeasurement
                && _stage != Stage.RestartGap
                && Time.unscaledTime - _stageStartedAt > StageTimeoutSeconds)
            {
                Fail($"stage {_stage} exceeded {StageTimeoutSeconds:0}s without converging");
                return;
            }

            switch (_stage)
            {
                case Stage.InitialConvergence:
                    HoldCamera(18f);
                    if (GpuViewConverged())
                    {
                        VoxelSurfaceMetrics metrics = VoxelRenderBridge.SurfaceMetrics;
                        Debug.Log(
                            "SOLIDGPU_VALIDATION initial-ready: "
                            + DescribeGpu(metrics));
                        BeginStage(Stage.Traversal);
                    }
                    break;

                case Stage.Traversal:
                    OrbitCamera(Time.unscaledTime - _stageStartedAt);
                    if (Time.unscaledTime - _stageStartedAt >= TraversalSeconds)
                    {
                        if (!GpuViewConverged())
                            return;

                        VoxelSurfaceMetrics metrics = VoxelRenderBridge.SurfaceMetrics;
                        Debug.Log(
                            "SOLIDGPU_VALIDATION traversal-ready: "
                            + DescribeGpu(metrics));
                        _beforeEditGpuBuilds = metrics.GpuCompletedSolidBuilds;
                        ApplyAuthoritativeEdit();
                        BeginStage(Stage.EditConvergence);
                    }
                    break;

                case Stage.EditConvergence:
                    HoldCamera(44f);
                    if (GpuViewConverged()
                        && VoxelRenderBridge.SurfaceMetrics.GpuCompletedSolidBuilds
                           > _beforeEditGpuBuilds)
                    {
                        Debug.Log(
                            "SOLIDGPU_VALIDATION edit-ready: "
                            + DescribeGpu(VoxelRenderBridge.SurfaceMetrics));
                        RenderingDiagnosticsComposition.ResetSolidRenderBenchmark();
                        _settledFrames = 0;
                        _frameSampleCount = 0;
                        BeginStage(Stage.SettledMeasurement);
                    }
                    break;

                case Stage.SettledMeasurement:
                    HoldCamera(44f);
                    RecordFrameTime();
                    _settledFrames++;
                    if (_settledFrames >= SettledFramesRequired)
                    {
                        if (!GpuViewConverged())
                        {
                            Fail("visible solid coverage stopped being converged during settled measurement");
                            return;
                        }

                        if (!ValidateSettledBudgets(out string budgetFailure))
                        {
                            Fail(budgetFailure);
                            return;
                        }

                        LogSettledEvidence();
                        RestartProductionWorld();
                        BeginStage(Stage.RestartGap);
                    }
                    break;

                case Stage.RestartGap:
                    HoldCamera(18f);
                    // Give the persistent URP feature one frame with no world so stale scheduler,
                    // page-arena, or mirror ownership cannot accidentally satisfy the restart.
                    if (Time.frameCount > 0 && Time.unscaledTime - _stageStartedAt >= 0.1f)
                    {
                        CreateAndBindWorld();
                        BeginStage(Stage.RestartConvergence);
                    }
                    break;

                case Stage.RestartConvergence:
                    HoldCamera(18f);
                    if (GpuViewConverged())
                    {
                        VoxelSurfaceMetrics metrics = VoxelRenderBridge.SurfaceMetrics;
                        Debug.Log(
                            "SOLIDGPU_VALIDATION restart-ready: "
                            + DescribeGpu(metrics));
                        Debug.Log(
                            "SOLIDGPU_VALIDATION success: "
                            + DescribeGpu(metrics)
                            + $" totalAllocatedMB={Profiler.GetTotalAllocatedMemoryLong() / (1024.0 * 1024.0):0.0}"
                            + $" graphicsMemoryMB={SystemInfo.graphicsMemorySize}");
                        _stage = Stage.Complete;
                    }
                    break;
            }
        }

        private void ConfigureProductionRenderer()
        {
            RenderingComposition.ClearWorld();
            RenderingComposition.ResetSurfacePassDiagnostics("solid-gpu-validation");
            RenderingComposition.SetSurfaceBuildEnabled(true);
            RenderingComposition.SetWaterRenderEnabled(false);
            RenderingComposition.SetVoxelLodEnabled(true);
            RenderingComposition.SetVoxelRingRadiusMetres(40f);
            RenderingComposition.SetVoxelDetailBandScale(0.6f);
            RenderingComposition.SetVoxelBuildConcurrency(8, 1);
            RenderingComposition.SetVoxelBuildBudgetMs(0.50, 8.0);
            RenderingComposition.SetEvictVisibleUnderArenaPressure(false);
            RenderingComposition.ConfigureEnvironment(
                Color.white,
                new Vector3(-0.4f, 0.82f, -0.4f).normalized,
                new Color(0.52f, 0.70f, 0.93f),
                new Color(0.15f, 0.39f, 0.80f));
            VoxelEngineBootstrap.ConfigureMaterialPresentation(
                SmoothMaterial, new Color(0.55f, 0.68f, 0.40f),
                textureWeight: 0.08f, normalStrength: 0.05f,
                roughness: 0.88f, variation: 0.025f);
            VoxelEngineBootstrap.ConfigureMaterialPresentation(
                PlanarMaterial, new Color(0.62f, 0.56f, 0.48f),
                textureWeight: 0.06f, normalStrength: 0.04f,
                roughness: 0.92f, variation: 0.018f);
        }

        private void EnsureCamera()
        {
            _camera = GetComponent<Camera>();
            if (_camera == null)
                _camera = gameObject.AddComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.52f, 0.70f, 0.93f);
            _camera.fieldOfView = 55f;
            _camera.nearClipPlane = 0.05f;
            _camera.farClipPlane = 120f;
            HoldCamera(18f);
        }

        private void CreateAndBindWorld()
        {
            _storage = VoxelEngineBootstrap.CreateStorage(
                expectedResidentRegions: 8,
                mixedBrickCapacity: 8192,
                changeJournalCapacity: 4096,
                maxMixedBrickAllocationBytes: 32L * 1024L * 1024L);

            _storage.RegisterMaterial(
                SmoothMaterial, hardness: 8, DestructionClass.Crumble,
                SurfaceStyles.Smooth, uint.MaxValue);
            _storage.RegisterMaterial(
                PlanarMaterial, hardness: 12, DestructionClass.Crumble,
                SurfaceStyles.Planar, uint.MaxValue);

            AuthorTableau(_storage.Mutations);
            _storage.PublishAllResidentRegions();

            var world = new RenderingWorldBinding(
                _storage.Reads,
                _storage.MaterialPresentation,
                _storage.SurfacePresentation,
                _storage.CoatingPresentation);
            RenderingComposition.ConfigureWorld(
                in world, _storage.Changes, terrainSeed: 0x51D6A11Du,
                farFieldEnabled: false);
        }

        private static void AuthorTableau(IRegionMutationStore mutations)
        {
            VoxelCell smooth = Cell(SmoothMaterial, SurfaceStyles.Smooth);
            VoxelCell planar = Cell(PlanarMaterial, SurfaceStyles.Planar);

            // A low courtyard gives the camera a broad continuous surface and crosses chunk
            // boundaries without requiring game-scene terrain generation.
            for (int x = -6; x <= 6; x++)
            for (int z = -6; z <= 6; z++)
                mutations.SetWholeCellBlock(new int3(x, 0, z), in smooth, false);

            // Four towers and a two-high perimeter exercise both smooth and faceted production
            // reconstruction through the same storage/palette/scheduler composition.
            for (int y = 1; y <= 4; y++)
            {
                SetTower(mutations, new int3(-5, y, -5), in smooth);
                SetTower(mutations, new int3(5, y, -5), in smooth);
                SetTower(mutations, new int3(-5, y, 5), in smooth);
                SetTower(mutations, new int3(5, y, 5), in smooth);
            }

            for (int i = -5; i <= 5; i++)
            {
                for (int y = 1; y <= 2; y++)
                {
                    mutations.SetWholeCellBlock(new int3(i, y, -5), in planar, true);
                    mutations.SetWholeCellBlock(new int3(i, y, 5), in planar, true);
                    mutations.SetWholeCellBlock(new int3(-5, y, i), in planar, true);
                    mutations.SetWholeCellBlock(new int3(5, y, i), in planar, true);
                }
            }

            // Preserve a visible gate opening and a stepped centre landmark so screenshots make
            // stale/edit failures obvious rather than producing a visually ambiguous flat slab.
            mutations.SetWholeBlock(new int3(-1, 1, -5), VoxelGrid.MaterialEmpty, false);
            mutations.SetWholeBlock(new int3(0, 1, -5), VoxelGrid.MaterialEmpty, false);
            mutations.SetWholeBlock(new int3(1, 1, -5), VoxelGrid.MaterialEmpty, false);
            mutations.SetWholeCellBlock(new int3(0, 1, 0), in smooth, false);
            mutations.SetWholeCellBlock(new int3(0, 2, 0), in smooth, false);
            mutations.SetWholeCellBlock(new int3(0, 3, 0), in smooth, false);
        }

        private static void SetTower(
            IRegionMutationStore mutations, int3 block, in VoxelCell cell)
        {
            mutations.SetWholeCellBlock(block, in cell, false);
            mutations.SetWholeCellBlock(block + new int3(1, 0, 0), in cell, false);
            mutations.SetWholeCellBlock(block + new int3(0, 0, 1), in cell, false);
            mutations.SetWholeCellBlock(block + new int3(1, 0, 1), in cell, false);
        }

        private static VoxelCell Cell(byte material, ushort style) => new()
        {
            BaseMaterialId = material,
            Surface = new VoxelSurfaceSemantics { StyleId = style },
            Boundary = default,
        };

        private void ApplyAuthoritativeEdit()
        {
            if (_storage == null)
            {
                Fail("storage disappeared before edit");
                return;
            }

            bool changed = _storage.Mutations.SetWholeBlock(
                EditedBlock, VoxelGrid.MaterialEmpty, markHardSurface: false);
            if (!changed)
            {
                Fail($"authoritative edit at block {EditedBlock} reported no change");
                return;
            }

            _storage.PublishAllResidentRegions();
        }

        private void RestartProductionWorld()
        {
            RenderingComposition.ClearWorld();
            _storage?.Dispose();
            _storage = null;
        }

        private bool GpuViewConverged()
        {
            VoxelSurfaceMetrics metrics = VoxelRenderBridge.SurfaceMetrics;
            SurfaceBenchmarkState state =
                RenderingDiagnosticsComposition.GetSurfaceBenchmarkState();
            return metrics.GpuCutoverAvailable
                && metrics.GpuResidentBackends > 0
                && metrics.GpuCompletedSolidBuilds > 0
                && state.IsConverged;
        }

        private static bool HasHardGpuFailure(out string failure)
        {
            failure = null;
            VoxelSurfaceMetrics m = VoxelRenderBridge.SurfaceMetrics;
            if (m.GpuFallbackSolidBuilds != 0
                || m.GpuUnsupportedSolidBuilds != 0
                || m.GpuContextFailureSolidBuilds != 0
                || m.GpuArenaFullSolidBuilds != 0
                || m.GpuCountFailureSolidBuilds != 0
                || m.GpuWriteFailureSolidBuilds != 0
                || m.GpuReadbackWaitSlices != 0
                || m.FramePathBlockingCompletionViolations != 0)
            {
                failure = "GPU production counters are non-zero: " + DescribeGpu(m);
                return true;
            }
            return false;
        }

        private bool ValidateSettledBudgets(out string failure)
        {
            failure = null;
            VoxelSurfaceMetrics metrics = VoxelRenderBridge.SurfaceMetrics;
            SolidRenderBenchmarkSnapshot benchmark =
                RenderingDiagnosticsComposition.GetSolidRenderBenchmark();

            if (metrics.SolidArenaCommittedBytes > PcSurfaceGeometryBudgetBytes)
            {
                failure =
                    $"solid arena committed {metrics.SolidArenaCommittedBytes} bytes, "
                    + $"above {PcSurfaceGeometryBudgetBytes} byte repository PC budget";
                return false;
            }

            if (benchmark.SchedulerPrepareP95Ms > 6.0
                || benchmark.SubmissionP95Ms > 6.0)
            {
                failure =
                    $"settled renderer p95 exceeds 6 ms budget: "
                    + $"prepare={benchmark.SchedulerPrepareP95Ms:0.###} "
                    + $"submission={benchmark.SubmissionP95Ms:0.###}";
                return false;
            }

            return true;
        }

        private void RecordFrameTime()
        {
            if (_frameSampleCount >= _frameSamplesMs.Length)
                return;
            _frameSamplesMs[_frameSampleCount++] = Time.unscaledDeltaTime * 1000f;
        }

        private void LogSettledEvidence()
        {
            SolidRenderBenchmarkSnapshot benchmark =
                RenderingDiagnosticsComposition.GetSolidRenderBenchmark();
            float p95 = Percentile(_frameSamplesMs, _frameSampleCount, 0.95f);
            float p99 = Percentile(_frameSamplesMs, _frameSampleCount, 0.99f);
            VoxelSurfaceMetrics metrics = VoxelRenderBridge.SurfaceMetrics;

            Debug.Log(
                "SOLIDGPU_VALIDATION settled: "
                + DescribeGpu(metrics)
                + $" frameP95Ms={p95:0.###} frameP99Ms={p99:0.###}"
                + $" prepareP95Ms={benchmark.SchedulerPrepareP95Ms:0.###}"
                + $" prepareP99Ms={benchmark.SchedulerPrepareP99Ms:0.###}"
                + $" submissionP95Ms={benchmark.SubmissionP95Ms:0.###}"
                + $" submissionP99Ms={benchmark.SubmissionP99Ms:0.###}"
                + $" arenaCommittedBytes={metrics.SolidArenaCommittedBytes}"
                + $" arenaUsedBytes={metrics.SolidArenaUsedBytes}"
                + $" uploadedGeometryBytes={metrics.UploadedGeometryBytes}");
        }

        private static float Percentile(float[] values, int count, float fraction)
        {
            if (count <= 0)
                return 0f;
            float[] copy = new float[count];
            Array.Copy(values, copy, count);
            Array.Sort(copy);
            int index = Mathf.Clamp(
                Mathf.CeilToInt((count - 1) * fraction), 0, count - 1);
            return copy[index];
        }

        private void OrbitCamera(float seconds)
        {
            float angle = seconds * 28f;
            float radians = angle * Mathf.Deg2Rad;
            Vector3 position = Target + new Vector3(
                Mathf.Sin(radians) * 10.5f,
                5.8f + Mathf.Sin(radians * 0.6f) * 0.6f,
                -Mathf.Cos(radians) * 10.5f);
            SetCamera(position);
        }

        private void HoldCamera(float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            SetCamera(Target + new Vector3(
                Mathf.Sin(radians) * 10.5f, 5.8f,
                -Mathf.Cos(radians) * 10.5f));
        }

        private void SetCamera(Vector3 position)
        {
            transform.position = position;
            transform.rotation = Quaternion.LookRotation(
                (Target - position).normalized, Vector3.up);
        }

        private void BeginStage(Stage stage)
        {
            _stage = stage;
            _stageStartedAt = Time.unscaledTime;
        }

        private static string DescribeGpu(in VoxelSurfaceMetrics m) =>
            $"gpuAvailable={m.GpuCutoverAvailable}"
            + $" backends={m.GpuResidentBackends}"
            + $" pub={m.GpuCompletedSolidBuilds}"
            + $" fallback={m.GpuFallbackSolidBuilds}"
            + $" unsupported={m.GpuUnsupportedSolidBuilds}"
            + $" contextFail={m.GpuContextFailureSolidBuilds}"
            + $" arenaFull={m.GpuArenaFullSolidBuilds}"
            + $" countFail={m.GpuCountFailureSolidBuilds}"
            + $" writeFail={m.GpuWriteFailureSolidBuilds}"
            + $" readbackWait={m.GpuReadbackWaitSlices}"
            + $" blocking={m.FramePathBlockingCompletionViolations}"
            + $" visible={m.VisibleSolidChunks}"
            + $" missing={m.MissingVisibleSolidChunks}";

        private void Fail(string reason)
        {
            if (_loggedFailure)
                return;
            _loggedFailure = true;
            _stage = Stage.Failed;
            Debug.LogError($"SOLIDGPU_VALIDATION failure: {reason}");
        }

        private void OnDisable()
        {
            if (!Application.isPlaying)
                return;

            RenderingComposition.ClearWorld();
            _storage?.Dispose();
            _storage = null;

            RenderingComposition.ResetTransientPresentation();
            RenderingComposition.SetWaterRenderEnabled(true);
            RenderingComposition.SetVoxelLodEnabled(true);
            RenderingComposition.SetSurfaceBuildEnabled(true);
        }
    }
}
