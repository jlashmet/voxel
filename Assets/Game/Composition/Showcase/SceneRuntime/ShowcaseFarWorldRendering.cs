using System.Collections.Generic;
using Game.Structures.Api;
using UnityEngine;
using VoxelEngine.Composition;
using VoxelEngine.Rendering.Api;
using VoxelEngine.Rendering.Runtime.FarWorld;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Showcase-owned camera/configuration adapter over the generic derived feature-presentation source.
    /// Castle planning is still used temporarily as a lifecycle signal until the legacy event is removed,
    /// but all far-world records now come from ShowcaseWorld.FeaturePresentation and follow the same
    /// selection/render path as every other sparse generated feature.
    /// </summary>
    [DefaultExecutionOrder(405)]
    [DisallowMultipleComponent]
    public sealed class ShowcaseFarWorldRendering : MonoBehaviour
    {
        private const float QueryRadiusMetres = 12500f;

        private readonly List<FarFeatureInstance> _renderInstances = new();
        private ShowcaseWorld _world;
        private FarFeatureSelectionPolicy _policy;
        private FarFeaturePresentationAdapter _source;
        private ProceduralFarFeatureRenderer _renderer;
        private Camera _camera;
        private float _policyFov;
        private int _policyHeight;

        public int SemanticRecordCount => _renderInstances.Count;
        public int RenderInstanceCount => _renderer != null ? _renderer.InstanceCount : 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetPlanningSubscription()
        {
            ShowcaseWorld.CastlePlanned -= OnCastlePlanned;
            ShowcaseWorld.CastlePlanned += OnCastlePlanned;
        }

        private static void OnCastlePlanned(ShowcaseWorld world, CastlePlan plan)
        {
            ShowcaseFarWorldRendering component = FindFirstObjectByType<ShowcaseFarWorldRendering>();
            if (component == null)
            {
                var go = new GameObject("Showcase Far World Rendering")
                {
                    hideFlags = HideFlags.DontSave,
                };
                component = go.AddComponent<ShowcaseFarWorldRendering>();
            }

            component.Bind(world);
        }

        public void Bind(ShowcaseWorld world)
        {
            if (ReferenceEquals(_world, world) && _source != null) return;

            _world = world;
            _policy = null;
            _source = null;
            _renderInstances.Clear();

            if (_world == null) return;
            EnsurePolicyAndSource();
        }

        private void LateUpdate()
        {
            if (_world == null) return;
            if (!RenderingComposition.TryGetWorld(out _, out _))
            {
                Unbind();
                return;
            }

            _camera = _camera != null ? _camera : Camera.main;
            if (_camera == null) return;
            EnsurePolicyAndSource();
            if (_source == null) return;

            IReadOnlyList<FarFeatureInstance> selected = _source.Query(
                (Unity.Mathematics.float3)_camera.transform.position,
                QueryRadiusMetres);
            _renderInstances.Clear();
            if (_renderInstances.Capacity < selected.Count) _renderInstances.Capacity = selected.Count;
            for (int i = 0; i < selected.Count; i++)
                _renderInstances.Add(selected[i]);

            if (_renderer == null)
                _renderer = gameObject.GetComponent<ProceduralFarFeatureRenderer>()
                    ?? gameObject.AddComponent<ProceduralFarFeatureRenderer>();
            _renderer.SetInstances(_renderInstances);
        }

        public void Unbind()
        {
            _world = null;
            _source = null;
            _policy?.ClearHistory();
            _policy = null;
            _renderInstances.Clear();
            if (_renderer != null) _renderer.Clear();
        }

        private void EnsurePolicyAndSource()
        {
            if (_world == null) return;

            Camera camera = _camera != null ? _camera : Camera.main;
            float fov = camera != null ? camera.fieldOfView : 60f;
            int height = camera != null ? Mathf.Max(1, camera.pixelHeight) : 1080;
            if (_policy != null && Mathf.Approximately(_policyFov, fov) && _policyHeight == height)
                return;

            _policyFov = fov;
            _policyHeight = height;
            _policy = new FarFeatureSelectionPolicy(
                new FarFeatureSelectionPolicy.Thresholds(
                    midEnterPixels: 24f,
                    midExitPixels: 18f,
                    farEnterPixels: 8f,
                    farExitPixels: 5f,
                    horizonEnterPixels: 1.4f,
                    horizonExitPixels: 0.8f),
                new FarFeatureSelectionPolicy.DistanceCaps(
                    defaultMetres: 1500f,
                    importantMetres: 8000f,
                    horizonMetres: QueryRadiusMetres),
                fov,
                height);
            _source = new FarFeaturePresentationAdapter(
                _world.FeaturePresentation,
                _policy,
                ShowcaseWorld.VoxelSize,
                ImportanceFor);
        }

        private static FarFeatureImportance ImportanceFor(FeaturePresentationBake bake)
        {
            // Sparse generated structures are semantic horizon features in this Showcase composition,
            // so small houses and large castles remain represented on distant hills without teaching
            // Rendering about either category. Large natural landforms receive the shorter important cap;
            // ordinary generated features still rely on projected significance alone.
            switch (bake.Kind)
            {
                case FeatureKind.Structure:
                    return FarFeatureImportance.Horizon;
                case FeatureKind.Landform:
                    return FarFeatureImportance.Important;
                default:
                    return FarFeatureImportance.Default;
            }
        }

        private void OnDestroy()
        {
            Unbind();
        }
    }
}
