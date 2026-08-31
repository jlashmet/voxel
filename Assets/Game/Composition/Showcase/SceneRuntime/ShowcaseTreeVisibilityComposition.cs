using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Rendering.Api;
using VoxelEngine.Rendering.Runtime.Vegetation;
using VoxelEngine.Vegetation.Api;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Showcase-owned presentation policy over the authoritative tree read source. This component
    /// owns only camera-window/tier state: placement, damage, severing, and stable tree identity
    /// remain in the vegetation module. Renderer hookup consumes the selected outputs separately.
    /// </summary>
    [DefaultExecutionOrder(410)]
    [DisallowMultipleComponent]
    public sealed class ShowcaseTreeVisibilityComposition : MonoBehaviour
    {
        [SerializeField] private float m_SectorSizeMetres = 64f;
        [SerializeField] private float m_FullTreeExitMetres = 120f;
        [SerializeField] private float m_SimplifiedTreeExitMetres = 480f;
        [SerializeField] private float m_CanopyExitMetres = 1800f;
        [SerializeField] private float m_LandmarkExitMetres = 12000f;
        [SerializeField] private float m_TierHysteresisMetres = 20f;
        [SerializeField] private float m_LandmarkScaleThreshold = 2.25f;

        private readonly List<TreeVisibilityEntry> _queried = new();
        private readonly List<SelectedTreePresentation> _individuals = new();
        private readonly List<TreeVisibilityEntry> _canopyMembers = new();
        private readonly TreeVisibilitySelector _selector = new();
        private IReadOnlyList<ForestCanopyCluster> _canopyClusters = System.Array.Empty<ForestCanopyCluster>();
        private Camera _camera;

        public IReadOnlyList<SelectedTreePresentation> Individuals => _individuals;
        public IReadOnlyList<TreeVisibilityEntry> CanopyMembers => _canopyMembers;
        public IReadOnlyList<ForestCanopyCluster> CanopyClusters => _canopyClusters;
        public int QueriedTreeCount => _queried.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<VoxelShowcase>() == null) return;
            if (FindFirstObjectByType<ShowcaseTreeVisibilityComposition>() != null) return;
            var go = new GameObject("Showcase Tree Visibility") { hideFlags = HideFlags.DontSave };
            go.AddComponent<ShowcaseTreeVisibilityComposition>();
        }

        private void OnDisable()
        {
            _selector.Reset();
            _queried.Clear();
            _individuals.Clear();
            _canopyMembers.Clear();
            _canopyClusters = System.Array.Empty<ForestCanopyCluster>();
        }

        private void LateUpdate()
        {
            _camera = _camera != null ? _camera : Camera.main;
            if (_camera == null) return;

            ITreeWorldReadSource source = TreeWorldReadRegistry.Current;
            float maxRadius = math.max(m_CanopyExitMetres, m_LandmarkExitMetres);
            float2 centre = new float2(_camera.transform.position.x, _camera.transform.position.z);
            VisibilitySectorBounds sectors = VisibilitySectorBounds.Around(
                centre, maxRadius, math.max(1f, m_SectorSizeMetres));

            VegetationVisibility.QueryTrees(
                source,
                math.max(1f, m_SectorSizeMetres),
                in sectors,
                _queried);

            var policy = new TreeVisibilityTierPolicy(
                math.max(1f, m_FullTreeExitMetres),
                math.max(m_FullTreeExitMetres, m_SimplifiedTreeExitMetres),
                math.max(m_SimplifiedTreeExitMetres, m_CanopyExitMetres),
                math.max(m_CanopyExitMetres, m_LandmarkExitMetres),
                math.max(0f, m_TierHysteresisMetres));

            float landmarkScale = math.max(0.01f, m_LandmarkScaleThreshold);
            _selector.Select(
                _queried,
                (float3)_camera.transform.position,
                in policy,
                tree => tree.Instance.Scale >= landmarkScale,
                _individuals,
                _canopyMembers);

            // Build clusters from exactly the trees that have handed off from individual proxies.
            // No independent landmark can enter this list, so the cluster cannot double-own it.
            _canopyClusters = ForestCanopyClusterBuilder.Build(_canopyMembers);
        }
    }
}
