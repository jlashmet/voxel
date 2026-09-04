using System;
using Game.WorldBuilder.Api;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Composition;
using VoxelEngine.Rendering.Runtime.FarWorld;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Scene-lifetime composition for semantic far features. The source is derived metadata only;
    /// querying it never requests voxel regions. Distance/significance thresholds remain Showcase
    /// policy while the renderer stays producer-agnostic.
    /// </summary>
    internal sealed class ShowcaseFarFeatureRuntime : IDisposable
    {
        public const float RadiusMetres = 12000f;

        private readonly GameObject _root;
        private readonly ProceduralFarFeatureRenderer _renderer;
        private readonly ShowcaseFarFeatureStateAdapter _source;
        private readonly int _sourceCount;

        public ShowcaseFarFeatureRuntime(
            Transform parent,
            IFeaturePresentationSource source,
            int sourceCount,
            IStructureVisualStateSource states,
            float voxelSizeMetres,
            Camera camera)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (states == null) throw new ArgumentNullException(nameof(states));

            float verticalFov = camera != null ? camera.fieldOfView : 60f;
            int viewportHeight = camera != null && camera.pixelHeight > 0
                ? camera.pixelHeight
                : 1080;

            // A roughly 100 m landmark remains several pixels tall at 12 km while an ordinary
            // house naturally falls below the far threshold. Enter/exit gaps provide tier
            // hysteresis; the shared policy contains no Showcase names or coordinates.
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
            var presentation = new FarFeaturePresentationAdapter(
                source,
                selection,
                voxelSizeMetres);
            _source = new ShowcaseFarFeatureStateAdapter(presentation, states);
            _sourceCount = sourceCount;

            _root = new GameObject("Showcase Semantic Far Features");
            _root.transform.SetParent(parent, false);
            _renderer = _root.AddComponent<ProceduralFarFeatureRenderer>();
        }

        public int VisibleInstanceCount => _renderer != null ? _renderer.InstanceCount : 0;
        public int SourceCount => _sourceCount;

        public void Update(Camera camera, float3 fallbackCameraPosition)
        {
            if (_renderer == null) return;
            float3 cameraPosition = camera != null
                ? (float3)camera.transform.position
                : fallbackCameraPosition;
            _renderer.SetInstances(_source.Query(cameraPosition, RadiusMetres));
        }

        public string Describe() =>
            $"semantic={VisibleInstanceCount}/{SourceCount} radius={RadiusMetres:0}m";

        public void Dispose()
        {
            if (_root == null) return;
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(_root);
            else
                UnityEngine.Object.DestroyImmediate(_root);
        }
    }
}
