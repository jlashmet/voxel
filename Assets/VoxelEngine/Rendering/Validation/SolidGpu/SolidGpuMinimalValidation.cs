using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Composition;
using VoxelEngine.Rendering.Runtime;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Rendering.Validation
{
    /// <summary>
    /// Smallest production-faithful visual discriminator for the solid GPU renderer.
    /// The fixture deliberately owns only deterministic authoritative inputs and evidence;
    /// Storage, scheduling, extraction, page publication, materials and drawing are production code.
    /// </summary>
    [AddComponentMenu("VoxelEngine/Validation/Solid GPU Minimal Validation")]
    public sealed class SolidGpuMinimalValidation : MonoBehaviour
    {
        private const byte GroundMaterial = 1;
        private const byte StoneMaterial = 2;
        private const byte ClayMaterial = 3;
        private const float VoxelSize = 0.1f;
        private const float TimeoutSeconds = 14f;
        private const int StableFramesRequired = 30;
        private const int ExpectedSolidVoxels = 41;
        private const int ExpectedExposedFaces = 114;
        private const int ExpectedCubicVertices = ExpectedExposedFaces * 4;
        private const int ExpectedCubicIndices = ExpectedExposedFaces * 6;

        private static readonly int3 FixtureBlock = int3.zero;
        private static readonly Vector3 Target = new(0.4f, 0.14f, 0.4f);
        private static readonly Vector3 CameraPosition = new(0.4f, 0.58f, -0.95f);

        private IVoxelStorageRuntime _storage;
        private Camera _camera;
        private float _startedAt;
        private int _stableFrames;
        private bool _completed;
        private bool _failed;

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            if (!SystemInfo.supportsComputeShaders)
            {
                Fail("compute shaders are unavailable");
                return;
            }

            ConfigureProductionRenderer();
            EnsureCamera();
            if (!CreateAndBindWorld())
                return;

            _startedAt = Time.unscaledTime;
            Debug.Log(
                "SOLIDGPU_MINIMAL authored: block=(0,0,0) solidVoxels=41"
                + $" expectedFaces={ExpectedExposedFaces}"
                + $" expectedVertices={ExpectedCubicVertices}"
                + $" expectedIndices={ExpectedCubicIndices}"
                + " materials=ground:1,stone:2,clay:3 style=Cubic");
        }

        private void Update()
        {
            if (!Application.isPlaying || _completed || _failed)
                return;

            HoldCamera();

            if (HasHardGpuFailure(out string failure))
            {
                Fail(failure);
                return;
            }

            VoxelSurfaceMetrics metrics = VoxelRenderBridge.SurfaceMetrics;
            SurfaceBenchmarkState state = RenderingDiagnosticsComposition.GetSurfaceBenchmarkState();
            bool converged = metrics.GpuCutoverAvailable
                && metrics.GpuResidentBackends > 0
                && metrics.GpuCompletedSolidBuilds > 0
                && metrics.VisibleSolidChunks > 0
                && state.IsConverged;

            if (converged)
            {
                _stableFrames++;
                if (_stableFrames >= StableFramesRequired)
                {
                    Debug.Log(
                        "SOLIDGPU_MINIMAL success: " + DescribeGpu(metrics)
                        + $" expectedFaces={ExpectedExposedFaces}"
                        + $" expectedVertices={ExpectedCubicVertices}"
                        + $" expectedIndices={ExpectedCubicIndices}");
                    _completed = true;
                }
            }
            else
            {
                _stableFrames = 0;
            }

            if (!_completed && Time.unscaledTime - _startedAt > TimeoutSeconds)
                Fail("minimal fixture did not converge: " + DescribeGpu(metrics));
        }

        private void ConfigureProductionRenderer()
        {
            RenderingComposition.ClearWorld();
            RenderingComposition.ResetSurfacePassDiagnostics("solid-gpu-minimal-validation");
            RenderingComposition.SetSurfaceBuildEnabled(true);
            RenderingComposition.SetWaterRenderEnabled(false);
            RenderingComposition.SetVoxelLodEnabled(false);
            RenderingComposition.SetVoxelRingRadiusMetres(6f);
            RenderingComposition.SetVoxelBuildConcurrency(4, 1);
            RenderingComposition.SetVoxelBuildBudgetMs(0.50, 4.0);
            RenderingComposition.SetEvictVisibleUnderArenaPressure(false);
            RenderingComposition.ConfigureEnvironment(
                Color.white,
                new Vector3(-0.45f, 0.80f, -0.35f).normalized,
                new Color(0.72f, 0.82f, 0.95f),
                new Color(0.12f, 0.24f, 0.42f));

            VoxelEngineBootstrap.ConfigureMaterialPresentation(
                GroundMaterial, new Color(0.30f, 0.62f, 0.24f),
                textureWeight: 0f, normalStrength: 0f,
                roughness: 0.9f, variation: 0f);
            VoxelEngineBootstrap.ConfigureMaterialPresentation(
                StoneMaterial, new Color(0.58f, 0.60f, 0.64f),
                textureWeight: 0f, normalStrength: 0f,
                roughness: 0.92f, variation: 0f);
            VoxelEngineBootstrap.ConfigureMaterialPresentation(
                ClayMaterial, new Color(0.78f, 0.24f, 0.15f),
                textureWeight: 0f, normalStrength: 0f,
                roughness: 0.88f, variation: 0f);
        }

        private bool CreateAndBindWorld()
        {
            _storage = VoxelEngineBootstrap.CreateStorage(
                expectedResidentRegions: 2,
                mixedBrickCapacity: 128,
                changeJournalCapacity: 128,
                maxMixedBrickAllocationBytes: 2L * 1024L * 1024L);

            _storage.RegisterMaterial(
                GroundMaterial, hardness: 8, DestructionClass.Crumble,
                SurfaceStyles.Cubic, uint.MaxValue);
            _storage.RegisterMaterial(
                StoneMaterial, hardness: 12, DestructionClass.Crumble,
                SurfaceStyles.Cubic, uint.MaxValue);
            _storage.RegisterMaterial(
                ClayMaterial, hardness: 10, DestructionClass.Crumble,
                SurfaceStyles.Cubic, uint.MaxValue);

            if (!AuthorFixture(_storage.Mutations))
            {
                Fail("could not author the one-block fixture");
                return false;
            }

            _storage.PublishAllResidentRegions();
            if (!VerifyAuthoritativeInput(_storage.Reads, out string inputFailure))
            {
                Fail(inputFailure);
                return false;
            }

            var world = new RenderingWorldBinding(
                _storage.Reads,
                _storage.MaterialPresentation,
                _storage.SurfacePresentation,
                _storage.CoatingPresentation);
            RenderingComposition.ConfigureWorld(
                in world, _storage.Changes, terrainSeed: 0x19C019C0u,
                farFieldEnabled: false);
            return true;
        }

        private static bool AuthorFixture(IRegionMutationStore mutations)
        {
            if (!mutations.TryBeginCellBlock(
                    FixtureBlock, markHardSurface: false, out VoxelBlockMutation mutation))
                return false;

            bool changed = false;
            VoxelCell ground = Cell(GroundMaterial);
            VoxelCell stone = Cell(StoneMaterial);
            VoxelCell clay = Cell(ClayMaterial);

            // 6x6 flat floor, one voxel of empty margin around every block edge.
            for (int z = 1; z <= 6; z++)
            for (int x = 1; x <= 6; x++)
                changed |= mutation.SetCell(CellIndex(x, 0, z), in ground);

            // Isolated stone cube.
            changed |= mutation.SetCell(CellIndex(2, 1, 2), in stone);

            // Two-high clay column.
            changed |= mutation.SetCell(CellIndex(4, 1, 2), in clay);
            changed |= mutation.SetCell(CellIndex(4, 2, 2), in clay);

            // Adjacent two-material pair.
            changed |= mutation.SetCell(CellIndex(2, 1, 5), in stone);
            changed |= mutation.SetCell(CellIndex(3, 1, 5), in clay);

            return mutations.CompletePartialBlock(ref mutation, changed);
        }

        private static bool VerifyAuthoritativeInput(
            IRegionReadSource reads, out string failure)
        {
            failure = null;
            if (!reads.TryPinWorldBlock(FixtureBlock, out PinnedVoxelReadBlock block))
            {
                failure = "authoritative fixture block was not readable after publication";
                return false;
            }

            try
            {
                if (block.Kind != VoxelReadBlockKind.Mixed)
                {
                    failure = $"expected one mixed block, got {block.Kind}";
                    return false;
                }

                int solid = 0;
                for (int z = 0; z < VoxelReadGrid.BlockEdge; z++)
                for (int y = 0; y < VoxelReadGrid.BlockEdge; y++)
                for (int x = 0; x < VoxelReadGrid.BlockEdge; x++)
                {
                    int index = CellIndex(x, y, z);
                    byte expectedMaterial = ExpectedMaterial(x, y, z);
                    byte actualMaterial = block.MixedVoxels[block.MixedOffset + index];
                    if (actualMaterial != expectedMaterial)
                    {
                        failure =
                            $"authoritative material mismatch at ({x},{y},{z}): "
                            + $"expected {expectedMaterial}, got {actualMaterial}";
                        return false;
                    }

                    if (actualMaterial != VoxelGrid.MaterialEmpty)
                    {
                        solid++;
                        ushort packed = block.MixedSurfaceSemantics[block.MixedOffset + index];
                        ushort style = VoxelSurfaceSemantics.FromStorage(packed).ReconstructionStyleId;
                        if (style != SurfaceStyles.Cubic)
                        {
                            failure =
                                $"authoritative style mismatch at ({x},{y},{z}): "
                                + $"expected Cubic, got {style}";
                            return false;
                        }
                    }
                }

                if (solid != ExpectedSolidVoxels)
                {
                    failure = $"expected {ExpectedSolidVoxels} solid voxels, got {solid}";
                    return false;
                }

                Debug.Log(
                    "SOLIDGPU_MINIMAL cpu-input-ok: solidVoxels=41"
                    + $" expectedExposedFaces={ExpectedExposedFaces}");
                return true;
            }
            finally
            {
                if (block.HasPinnedPayload)
                    reads.ReleasePinnedWorldBlock(in block.Pin);
            }
        }

        private static byte ExpectedMaterial(int x, int y, int z)
        {
            if (y == 0 && x >= 1 && x <= 6 && z >= 1 && z <= 6)
                return GroundMaterial;
            if (x == 2 && y == 1 && z == 2)
                return StoneMaterial;
            if (x == 4 && (y == 1 || y == 2) && z == 2)
                return ClayMaterial;
            if (x == 2 && y == 1 && z == 5)
                return StoneMaterial;
            if (x == 3 && y == 1 && z == 5)
                return ClayMaterial;
            return VoxelGrid.MaterialEmpty;
        }

        private static int CellIndex(int x, int y, int z) =>
            x | (y << VoxelReadGrid.BlockEdgeLog2)
              | (z << (VoxelReadGrid.BlockEdgeLog2 * 2));

        private static VoxelCell Cell(byte material) => new()
        {
            BaseMaterialId = material,
            Surface = new VoxelSurfaceSemantics { StyleId = SurfaceStyles.Cubic },
            Boundary = default,
        };

        private void EnsureCamera()
        {
            _camera = GetComponent<Camera>();
            if (_camera == null)
                _camera = gameObject.AddComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.72f, 0.82f, 0.95f);
            _camera.fieldOfView = 42f;
            _camera.nearClipPlane = 0.02f;
            _camera.farClipPlane = 24f;
            HoldCamera();
        }

        private void HoldCamera()
        {
            transform.position = CameraPosition;
            transform.rotation = Quaternion.LookRotation(
                (Target - CameraPosition).normalized, Vector3.up);
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
                failure = "GPU counters are non-zero: " + DescribeGpu(m);
                return true;
            }
            return false;
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
            + $" blocking={m.FramePathBlockingCompletionViolations}"
            + $" visible={m.VisibleSolidChunks}"
            + $" missing={m.MissingVisibleSolidChunks}";

        private void OnGUI()
        {
            if (!Application.isPlaying)
                return;

            GUI.Box(new Rect(16, 16, 430, 92), "GPU MINIMAL SOLID REPRO");
            GUI.Label(new Rect(28, 42, 400, 20),
                "Expected: green 6x6 floor | gray cube + pair | red 2-high column + pair");
            GUI.Label(new Rect(28, 62, 400, 20),
                "41 solid voxels | 114 exposed cubic faces | 456 vertices | 684 indices");
            GUI.Label(new Rect(28, 82, 400, 20),
                _failed ? "FAILED" : _completed ? "GPU CONVERGED" : "Waiting for GPU convergence...");
        }

        private void Fail(string reason)
        {
            if (_failed)
                return;
            _failed = true;
            Debug.LogError("SOLIDGPU_MINIMAL failure: " + reason);
        }

        private void OnDisable()
        {
            if (!Application.isPlaying)
                return;
            RenderingComposition.ClearWorld();
            _storage?.Dispose();
            _storage = null;
        }
    }
}
