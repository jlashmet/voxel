using System;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Composition;
using VoxelEngine.Rendering.Runtime.FarWorld;
using VoxelEngine.Structures.Api;

namespace Game.Kentridge.PlayableSlice
{
    /// <summary>
    /// Kentridge scene composition for the shared semantic far-feature pipeline. The source is
    /// renderer-neutral presentation baked from the same canonical FeatureCatalogue later consumed
    /// by physical voxel realization. Querying and drawing it never requests voxel residency.
    /// </summary>
    internal sealed class KentridgeFarFeatureRuntime : IDisposable
    {
        public const float RadiusMetres = 12000f;

        private readonly GameObject _root;
        private readonly ProceduralFarFeatureRenderer _renderer;
        private readonly FarFeaturePresentationAdapter _source;
        private readonly int _sourceCount;

        public KentridgeFarFeatureRuntime(
            Transform parent,
            IFeaturePresentationSource source,
            int sourceCount,
            float voxelSizeMetres,
            Camera camera)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (source == null) throw new ArgumentNullException(nameof(source));

            float verticalFov = camera != null ? camera.fieldOfView : 60f;
            int viewportHeight = camera != null && camera.pixelHeight > 0
                ? camera.pixelHeight
                : 1080;

            // Thresholds are Kentridge presentation policy. Selection mechanics and renderer are
            // shared; no settlement identity or coordinate is embedded in engine code.
            var selection = new FarFeatureSelectionPolicy(
                new FarFeatureSelectionPolicy.Thresholds(
                    midEnterPixels: 24f,
                    midExitPixels: 18f,
                    farEnterPixels: 4f,
                    farExitPixels: 3f,
                    horizonEnterPixels: 1.5f,
                    horizonExitPixels: 1f),
                new FarFeatureSelectionPolicy.DistanceCaps(
                    defaultMetres: RadiusMetres + 250f,
                    importantMetres: RadiusMetres + 250f,
                    horizonMetres: RadiusMetres + 250f),
                verticalFov,
                viewportHeight);
            _source = new FarFeaturePresentationAdapter(source, selection, voxelSizeMetres);
            _sourceCount = sourceCount;

            _root = new GameObject("Kentridge Semantic Far Features");
            _root.transform.SetParent(parent, false);
            _renderer = _root.AddComponent<ProceduralFarFeatureRenderer>();
        }

        public int SourceCount => _sourceCount;
        public int VisibleInstanceCount => _renderer != null ? _renderer.InstanceCount : 0;
        internal int PersistentInstanceObjectCount =>
            _renderer != null ? _renderer.PersistentInstanceObjectCount : 0;

        public void Update(Camera camera, float3 fallbackCameraPosition)
        {
            if (_renderer == null) return;
            float3 cameraPosition = camera != null
                ? (float3)camera.transform.position
                : fallbackCameraPosition;
            _renderer.SetInstances(_source.Query(cameraPosition, RadiusMetres));
        }

        public void Dispose()
        {
            if (_root == null) return;
            if (UnityEngine.Application.isPlaying)
                UnityEngine.Object.Destroy(_root);
            else
                UnityEngine.Object.DestroyImmediate(_root);
        }
    }
}
