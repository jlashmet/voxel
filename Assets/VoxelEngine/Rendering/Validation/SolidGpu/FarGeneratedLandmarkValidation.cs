using Game.WorldBuilder.Voxel;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Composition;
using VoxelEngine.Rendering.Api;
using VoxelEngine.Rendering.Runtime.FarWorld;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Rendering.Validation
{
    // A production catalogue consumer with no detailed residency: this isolates the far
    // representation used before streaming. Geometry and materials come from production systems.
    public sealed class FarGeneratedLandmarkValidation : MonoBehaviour
    {
        private IVoxelStorageRuntime _storage;
        private ProceduralFarFeatureRenderer _renderer;
        private Camera _camera;
        private bool _reported;

        private void Start()
        {
            _camera = gameObject.AddComponent<Camera>();
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 1500f;
            _camera.fieldOfView = 55f;
            _camera.clearFlags = CameraClearFlags.Skybox;
            _storage = VoxelEngineBootstrap.CreateStorage(1, 1);
            _storage.RegisterMaterial(1, 8, DestructionClass.Crumble, SurfaceStyles.Smooth, uint.MaxValue);
            _storage.RegisterMaterial(2, 12, DestructionClass.Crumble, SurfaceStyles.Planar, uint.MaxValue);
            var world = new RenderingWorldBinding(_storage.Reads, _storage.MaterialPresentation,
                _storage.SurfacePresentation, _storage.CoatingPresentation);
            RenderingComposition.ConfigureWorld(world, _storage.Changes, 0x51D6A11Du, farFieldEnabled: false);
            var spec = new MountainLandmarkSpec(new int3(-600, 0, 0),
                1200, 280, 500, 80, 30, 360, 46, 6, 60);
            using FeatureCatalogue catalogue = WorldBuilderMountainLandmarkCatalogue.Build(
                spec, 1, 2, 2, Allocator.TempJob);
            var manifest = FeaturePresentationCatalogueBaker.Build(catalogue, 0x51D6A11Du);
            var policy = new FarFeatureSelectionPolicy(
                new FarFeatureSelectionPolicy.Thresholds(24, 18, 4, 3, 1.5f, 1),
                new FarFeatureSelectionPolicy.DistanceCaps(1500, 1500, 1500), 55, 900);
            var adapter = new FarFeaturePresentationAdapter(manifest, policy, 0.1f);
            _renderer = gameObject.AddComponent<ProceduralFarFeatureRenderer>();
            var instances = adapter.Query(float3.zero, 1500);
            _renderer.SetInstances(instances);
            int ramps = 0;
            foreach (var instance in instances)
                for (int i = 0; i < instance.Geometry.PrimitiveCount; i++)
                    if (instance.Geometry.GetPrimitive(i).Shape == FarFeatureGeometryShape.Ramp) ramps++;
            if (instances.Count != 2 || ramps < 6)
                Debug.LogError($"FAR_LANDMARK failure: instances={instances.Count} ramps={ramps}");
            else Debug.Log($"FAR_LANDMARK ready: instances={instances.Count} ramps={ramps}");
        }

        private void LateUpdate()
        {
            if (_camera == null) return;
            float angle = (-55f + Time.time * 4f) * Mathf.Deg2Rad;
            Vector3 target = new(0, 14, 60);
            transform.position = target + new Vector3(Mathf.Sin(angle) * 160f, 85f, -Mathf.Cos(angle) * 160f);
            transform.LookAt(target);
            if (!_reported && Time.time >= 24f)
            {
                _reported = true;
                Debug.Log($"FAR_LANDMARK success: instances={_renderer.InstanceCount}");
            }
        }

        private void OnDestroy()
        {
            if (_renderer != null) _renderer.Clear();
            RenderingComposition.ClearWorld();
            _storage?.Dispose();
            RenderingComposition.ResetTransientPresentation();
        }
    }
}
