using System;
using Game.WorldBuilder.Voxel;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Composition;
using VoxelEngine.Rendering.Runtime;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Rendering.Validation
{
    /// <summary>
    /// Rendering-owned built-player liveness discriminator. Two distant WorldBuilder landmarks are
    /// authored and made resident before rendering begins; the camera then performs a cold relocation.
    /// The only success path is production GPU extraction/publication convergence at the new view.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GpuSurfaceMirrorRelocationValidation : MonoBehaviour
    {
        private const uint Seed = 0x6D495252u;
        private const float InitialTimeoutSeconds = 25f;
        private const float RelocationTimeoutSeconds = 30f;

        private IVoxelStorageRuntime _storage;
        private Vector3 _firstTarget;
        private Vector3 _secondTarget;
        private float _phaseStarted;
        private ulong _baselinePublications;
        private int _phase;
        private float _nextDiagnostic;

        private void Start()
        {
            gameObject.tag = "MainCamera";
            Camera cameraComponent = gameObject.AddComponent<Camera>();
            cameraComponent.clearFlags = CameraClearFlags.Skybox;
            cameraComponent.nearClipPlane = 0.1f;
            cameraComponent.farClipPlane = 900f;
            cameraComponent.fieldOfView = 42f;

            RenderingComposition.ClearWorld();
            RenderingComposition.SetSurfaceBuildEnabled(true);
            RenderingComposition.SetVoxelLodEnabled(true);
            RenderingComposition.SetVoxelRingRadiusMetres(128f);
            RenderingComposition.SetVoxelDetailBandScale(1f);

            _storage = VoxelEngineBootstrap.CreateStorage(128, 32768);
            _storage.RegisterMaterial(1, 8, DestructionClass.Crumble, SurfaceStyles.Smooth, uint.MaxValue);
            _storage.RegisterMaterial(2, 12, DestructionClass.Crumble, SurfaceStyles.Planar, uint.MaxValue);
            VoxelEngineBootstrap.ConfigureMaterialPresentation(
                1, new Color(0.28f, 0.46f, 0.18f), 0.08f, 0.05f, 0.88f, 0.025f);
            VoxelEngineBootstrap.ConfigureMaterialPresentation(
                2, new Color(0.38f, 0.35f, 0.29f), 0.06f, 0.04f, 0.92f, 0.018f);

            _firstTarget = Realize(new MountainLandmarkSpec(
                new int3(-1120, 0, 0), 320, 96, 160, 32, 12, 100, 14, 6, 20));
            _secondTarget = Realize(new MountainLandmarkSpec(
                new int3(800, 0, 0), 320, 96, 160, 32, 12, 100, 14, 6, 20));
            _storage.PublishAllResidentRegions();

            var world = new RenderingWorldBinding(
                _storage.Reads,
                _storage.MaterialPresentation,
                _storage.SurfacePresentation,
                _storage.CoatingPresentation);
            RenderingComposition.ConfigureWorld(world, _storage.Changes, Seed, farFieldEnabled: false);

            _phase = 0;
            _phaseStarted = Time.unscaledTime;
            PositionCamera(_firstTarget);
            Debug.Log(
                $"GPU_MIRROR_RELOCATION start: distanceM={Vector3.Distance(_firstTarget, _secondTarget):0.0} " +
                "storagePreResident=true budgets=production");
        }

        private void LateUpdate()
        {
            if (_storage == null) return;

            Vector3 target = _phase == 0 ? _firstTarget : _secondTarget;
            PositionCamera(target);
            var metrics = VoxelRenderBridge.SurfaceMetrics;
            float now = Time.unscaledTime;

            if (now >= _nextDiagnostic)
            {
                _nextDiagnostic = now + 5f;
                Debug.Log(
                    $"GPU_MIRROR_RELOCATION state: phase={_phase} visible={metrics.VisibleSolidChunks} " +
                    $"missing={metrics.MissingVisibleSolidChunks} pub={metrics.GpuStep4CompletedBuilds}");
            }

            if (_phase == 0)
            {
                if (metrics.VisibleSolidChunks > 0
                    && metrics.MissingVisibleSolidChunks == 0
                    && metrics.GpuStep4CompletedBuilds > 0)
                {
                    _baselinePublications = (ulong)metrics.GpuStep4CompletedBuilds;
                    Debug.Log(
                        $"GPU_MIRROR_RELOCATION initial-ready: pub={_baselinePublications} " +
                        $"visible={metrics.VisibleSolidChunks}");
                    _phase = 1;
                    _phaseStarted = now;
                    PositionCamera(_secondTarget);
                    Debug.Log(
                        $"GPU_MIRROR_RELOCATION relocated: baselinePub={_baselinePublications} " +
                        $"distanceM={Vector3.Distance(_firstTarget, _secondTarget):0.0}");
                }
                else if (now - _phaseStarted > InitialTimeoutSeconds)
                {
                    Fail("initial view did not converge", metrics.VisibleSolidChunks, metrics.MissingVisibleSolidChunks, (ulong)metrics.GpuStep4CompletedBuilds);
                }
                return;
            }

            if (_phase == 1)
            {
                if (now - _phaseStarted >= 1f
                    && metrics.VisibleSolidChunks > 0
                    && metrics.MissingVisibleSolidChunks == 0
                    && (ulong)metrics.GpuStep4CompletedBuilds >= _baselinePublications + 4)
                {
                    _phase = 2;
                    Debug.Log(
                        $"GPU_MIRROR_RELOCATION success: baselinePub={_baselinePublications} " +
                        $"relocatedPub={metrics.GpuStep4CompletedBuilds} visible={metrics.VisibleSolidChunks}");
                }
                else if (now - _phaseStarted > RelocationTimeoutSeconds)
                {
                    Fail("relocated view did not recover publication", metrics.VisibleSolidChunks, metrics.MissingVisibleSolidChunks, (ulong)metrics.GpuStep4CompletedBuilds);
                }
            }
        }

        private Vector3 Realize(MountainLandmarkSpec spec)
        {
            using FeatureCatalogue catalogue =
                WorldBuilderMountainLandmarkCatalogue.Build(spec, 1, 2, 2, Allocator.TempJob);
            var manifest = FeaturePresentationCatalogueBaker.Build(catalogue, Seed);
            var features = manifest.Query(new FeaturePresentationBounds(new int3(-10000), new int3(10000)));
            if (features.Count == 0)
                throw new InvalidOperationException("GPU_MIRROR_RELOCATION failure: production landmark catalogue is empty.");

            int3 min = new int3(int.MaxValue);
            int3 max = new int3(int.MinValue);
            foreach (var feature in features)
            {
                min = math.min(min, feature.BoundsMin);
                max = math.max(max, feature.BoundsMax);
            }

            int3 regionMin = (int3)math.floor((float3)min / VoxelGrid.RegionVoxelEdge) - 1;
            int3 regionMax = (int3)math.floor((float3)max / VoxelGrid.RegionVoxelEdge) + 1;
            for (int z = regionMin.z; z <= regionMax.z; z++)
            for (int y = regionMin.y; y <= regionMax.y; y++)
            for (int x = regionMin.x; x <= regionMax.x; x++)
            {
                int3 region = new int3(x, y, z);
                _storage.Residency.EnsureRegionResident(region);
                using var build = new FeatureRegionBuild(region);
                while (!build.Step(catalogue, Seed, _storage.Reads, _storage.Mutations, int.MaxValue)) { }
                if (build.Report.BudgetExceeded)
                    throw new InvalidOperationException(
                        "GPU_MIRROR_RELOCATION failure: production landmark exceeded authored budget.");
            }

            return (Vector3)((float3)(min + max) * 0.05f);
        }

        private void PositionCamera(Vector3 target)
        {
            transform.position = target + new Vector3(-34f, 42f, -72f);
            transform.rotation = Quaternion.LookRotation((target - transform.position).normalized, Vector3.up);
        }

        private void Fail(string reason, int visible, int missing, ulong publications)
        {
            _phase = 2;
            Debug.LogError(
                $"GPU_MIRROR_RELOCATION failure: {reason}; visible={visible} " +
                $"missing={missing} pub={publications}");
        }

        private void OnDestroy()
        {
            RenderingComposition.ClearWorld();
            _storage?.Dispose();
            _storage = null;
            RenderingComposition.ResetTransientPresentation();
        }
    }
}
