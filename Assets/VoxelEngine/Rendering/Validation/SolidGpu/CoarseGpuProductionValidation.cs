using System;
using Game.WorldBuilder.Voxel;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Composition;
using VoxelEngine.Rendering.Runtime;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Rendering.Validation
{
    /// <summary>Real WorldBuilder realization viewed across the production step-4 and step-8 distance bands.</summary>
    public sealed class CoarseGpuProductionValidation : MonoBehaviour
    {
        private IVoxelStorageRuntime _storage;
        private Vector3 _target;
        private float _started;
        private bool _reported;
        private bool _step4Reported;
        private float _nextDiagnostic;

        private void Start()
        {
            const uint seed = 0x51D6A11Du;
            gameObject.tag = "MainCamera";
            var camera = gameObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 1000f;
            camera.fieldOfView = 8f;
            camera.clearFlags = CameraClearFlags.Skybox;
            RenderingComposition.ClearWorld();
            RenderingComposition.SetSurfaceBuildEnabled(true);
            RenderingComposition.SetVoxelLodEnabled(true);
            RenderingComposition.SetVoxelRingRadiusMetres(512f);
            RenderingComposition.SetVoxelDetailBandScale(1f);
            _storage = VoxelEngineBootstrap.CreateStorage(64, 8192);
            _storage.RegisterMaterial(1, 8, DestructionClass.Crumble, SurfaceStyles.Smooth, uint.MaxValue);
            _storage.RegisterMaterial(2, 12, DestructionClass.Crumble, SurfaceStyles.Planar, uint.MaxValue);
            VoxelEngineBootstrap.ConfigureMaterialPresentation(1, new Color(0.28f, 0.46f, 0.18f), 0.08f, 0.05f, 0.88f, 0.025f);
            VoxelEngineBootstrap.ConfigureMaterialPresentation(2, new Color(0.38f, 0.35f, 0.29f), 0.06f, 0.04f, 0.92f, 0.018f);
            var spec = new MountainLandmarkSpec(new int3(-160, 0, 0),
                320, 96, 160, 32, 12, 100, 14, 6, 20);
            using FeatureCatalogue catalogue = WorldBuilderMountainLandmarkCatalogue.Build(spec, 1, 2, 2, Allocator.TempJob);
            var manifest = FeaturePresentationCatalogueBaker.Build(catalogue, seed);
            var features = manifest.Query(new FeaturePresentationBounds(new int3(-10000), new int3(10000)));
            int3 min = new(int.MaxValue), max = new(int.MinValue);
            foreach (var feature in features)
            {
                min = math.min(min, feature.BoundsMin); max = math.max(max, feature.BoundsMax);
            }
            if (features.Count == 0) throw new InvalidOperationException("Coarse validation catalogue is empty.");
            _target = (Vector3)((float3)(min + max) * 0.05f);
            int3 regionMin = (int3)math.floor((float3)min / VoxelGrid.RegionVoxelEdge) - 1;
            int3 regionMax = (int3)math.floor((float3)max / VoxelGrid.RegionVoxelEdge) + 1;
            for (int z = regionMin.z; z <= regionMax.z; z++)
            for (int y = regionMin.y; y <= regionMax.y; y++)
            for (int x = regionMin.x; x <= regionMax.x; x++)
            {
                int3 region = new(x, y, z);
                _storage.Residency.EnsureRegionResident(region);
                using var build = new FeatureRegionBuild(region);
                while (!build.Step(catalogue, seed, _storage.Reads, _storage.Mutations, int.MaxValue)) { }
                if (build.Report.BudgetExceeded) throw new InvalidOperationException("Coarse catalogue exceeded its budget.");
            }
            _storage.PublishAllResidentRegions();
            var world = new RenderingWorldBinding(_storage.Reads, _storage.MaterialPresentation,
                _storage.SurfacePresentation, _storage.CoatingPresentation);
            RenderingComposition.ConfigureWorld(world, _storage.Changes, seed, farFieldEnabled: false);
            _started = Time.unscaledTime;
            PositionCamera();
        }

        private void LateUpdate()
        {
            if (_storage == null) return;
            PositionCamera();
            if (Time.unscaledTime >= _nextDiagnostic)
            {
                _nextDiagnostic = Time.unscaledTime + 10f;
                Debug.Log("COARSE_GPU state: " + VoxelRenderBridge.DescribeRings?.Invoke());
            }
            if (_reported) return;
            var metrics = VoxelRenderBridge.SurfaceMetrics;
            if (!_step4Reported && Time.unscaledTime - _started < 15f
                && metrics.GpuStep4CompletedBuilds > 0 && metrics.VisibleSolidChunks > 0)
            {
                _step4Reported = true;
                Debug.Log($"COARSE_GPU step4-ready: publications={metrics.GpuStep4CompletedBuilds} visible={metrics.VisibleSolidChunks}");
            }
            if (_step4Reported && metrics.GpuBlockHlodCompletedBuilds > 0 && metrics.VisibleSolidChunks > 0
                && Time.unscaledTime - _started >= 15f)
            {
                _reported = true;
                Debug.Log($"COARSE_GPU success: step4Publications={metrics.GpuStep4CompletedBuilds} step8Publications={metrics.GpuBlockHlodCompletedBuilds} visible={metrics.VisibleSolidChunks}");
            }
            else if (Time.unscaledTime - _started > 50f)
            {
                _reported = true;
                Debug.LogError($"COARSE_GPU failure: step4Publications={metrics.GpuStep4CompletedBuilds} step8Publications={metrics.GpuBlockHlodCompletedBuilds} visible={metrics.VisibleSolidChunks} missing={metrics.MissingVisibleSolidChunks}");
            }
        }

        private void PositionCamera()
        {
            float angle = Mathf.Sin((Time.unscaledTime - _started) * 0.08f) * 0.08f;
            bool intermediate = Time.unscaledTime - _started < 15f;
            float distance = intermediate ? 240f : 350f;
            GetComponent<Camera>().fieldOfView = intermediate ? 12f : 8f;
            transform.position = _target + new Vector3(Mathf.Sin(angle) * distance,
                intermediate ? 55f : 85f, -Mathf.Cos(angle) * distance);
            transform.LookAt(_target);
        }

        private void OnDestroy()
        {
            RenderingComposition.ClearWorld();
            _storage?.Dispose();
            RenderingComposition.ResetTransientPresentation();
        }
    }
}
