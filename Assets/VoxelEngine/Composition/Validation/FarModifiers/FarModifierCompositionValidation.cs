using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Rendering.Runtime.FarWorld;
using VoxelEngine.Showcase;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Composition.Validation
{
    // Far-only production consumer: no near residency is requested. Modifier intent comes from
    // the same authored catalogue as Showcase, while the real clipmap owns terrain geometry.
    public sealed class FarModifierCompositionValidation : MonoBehaviour
    {
        private ShowcaseWorld _world;
        private ProceduralFarFeatureRenderer _renderer;
        private FarFeaturePresentationAdapter _adapter;
        private readonly HashSet<ulong> _modifiers = new();
        private Vector3 _target;
        private bool _reported;

        private void Start()
        {
            const uint seed = 0x5EED1234u;
            _world = new ShowcaseWorld(seed, 64, 1, 2);
            var binding = new RenderingWorldBinding(_world.ReadStorage, _world.Palette,
                _world.SurfaceRules, _world.CoatingRules, _world.ProfileBlocks);
            RenderingComposition.ConfigureWorld(binding, _world.Changes, seed, farFieldEnabled: true);
            gameObject.tag = "MainCamera";
            var camera = gameObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 1500f;
            camera.fieldOfView = 55f;
            camera.clearFlags = CameraClearFlags.Skybox;
            var source = _world.FarFeaturePresentation;
            var bakes = source.Query(new FeaturePresentationBounds(new int3(-10000), new int3(10000)));
            Vector3 centerSum = Vector3.zero;
            foreach (var bake in bakes)
            {
                bool additive = false;
                for (int i = 0; i < bake.PrimitiveCount; i++)
                {
                    var mode = bake.GetPrimitive(i).Mode;
                    additive |= mode == PrimitiveMode.Fill || mode == PrimitiveMode.FillIfEmpty;
                }
                if (additive) continue;
                _modifiers.Add(bake.SourceId);
                centerSum += (Vector3)((float3)(bake.BoundsMin + bake.BoundsMax) * 0.05f);
            }
            if (_modifiers.Count == 0) Debug.LogError("FAR_MODIFIER failure: no generated modifier inputs");
            _target = centerSum / Mathf.Max(1, _modifiers.Count);
            _adapter = new FarFeaturePresentationAdapter(source,
                new FarFeatureSelectionPolicy(
                    new FarFeatureSelectionPolicy.Thresholds(24, 18, 4, 3, 1.5f, 1),
                    new FarFeatureSelectionPolicy.DistanceCaps(1500, 1500, 1500), 55, 900), 0.1f);
            _renderer = gameObject.AddComponent<ProceduralFarFeatureRenderer>();
            var terrain = VoxelFarTerrain.Create(transform, seed, 100f, 1500f);
            terrain.Structures = _world.FarField;
            Debug.Log($"FAR_MODIFIER ready: canonicalModifiers={_modifiers.Count}");
        }

        private void Update()
        {
            if (_adapter == null) return;
            float angle = Time.time * 0.07f;
            transform.position = _target + new Vector3(Mathf.Sin(angle) * 80f, 45f, -Mathf.Cos(angle) * 80f);
            transform.LookAt(_target);
            var instances = _adapter.Query(transform.position, 1500f);
            foreach (var instance in instances)
                if (instance.Geometry == null || _modifiers.Contains(instance.StableId))
                    Debug.LogError("FAR_MODIFIER failure: modifier became standalone geometry");
            _renderer.SetInstances(instances);
            if (!_reported && Time.time >= 24f)
            {
                _reported = true;
                if (instances.Count == 0) Debug.LogError("FAR_MODIFIER failure: missing additive geometry");
                else Debug.Log($"FAR_MODIFIER success: solids={instances.Count} canonicalModifiers={_modifiers.Count}");
            }
        }

        private void OnDestroy()
        {
            if (_renderer != null) _renderer.Clear();
            RenderingComposition.ClearWorld();
            _world?.Dispose();
            RenderingComposition.ResetTransientPresentation();
        }
    }
}
