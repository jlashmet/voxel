using System.Collections.Generic;
using Game.Structures.Api;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Composition;
using VoxelEngine.Rendering.Api;
using VoxelEngine.Rendering.Runtime.FarWorld;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// SceneRuntime consumer of the Showcase planning event. The event arrives synchronously when
    /// CastlePlan exists, before physical castle realization, so this component can publish the
    /// independent semantic proxy immediately without requesting distant voxel residency.
    /// </summary>
    [DefaultExecutionOrder(405)]
    [DisallowMultipleComponent]
    public sealed class ShowcaseFarWorldRendering : MonoBehaviour
    {
        private const float QueryRadiusMetres = 12500f;

        private readonly ShowcaseCastleVisibilityManifest _visibility = new();
        private readonly List<FarFeatureInstance> _renderInstances = new();
        private ShowcaseWorld _world;
        private ShowcaseFarStructureSource _source;
        private FarWorldVisibilityPolicy _policy;
        private ProceduralFarFeatureRenderer _renderer;
        private Camera _camera;
        private float _policyFov;
        private int _policyHeight;

        public int SemanticRecordCount => _visibility.Count;
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
            component._visibility.Register(in plan);
        }

        public void Bind(ShowcaseWorld world)
        {
            if (ReferenceEquals(_world, world) && _source != null) return;

            _world = world;
            _visibility.Clear();
            _policy = null;
            _source = null;

            if (_world == null) return;
            if (_world.TryGetPlannedCastle(out CastlePlan existingPlan))
                _visibility.Register(in existingPlan);
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

            float2 cameraXZ = new float2(_camera.transform.position.x, _camera.transform.position.z);
            IReadOnlyList<FarFeatureInstance> selected = _source.Query(cameraXZ, QueryRadiusMetres);
            _renderInstances.Clear();
            if (_renderInstances.Capacity < selected.Count) _renderInstances.Capacity = selected.Count;
            for (int i = 0; i < selected.Count; i++)
            {
                FarFeatureInstance instance = selected[i];
                if (instance.StableId == _visibility.CastleKey)
                {
                    instance = new FarFeatureInstance(
                        instance.StableId,
                        instance.Position,
                        instance.Rotation,
                        instance.Scale,
                        instance.BoundsCenter,
                        instance.BoundsExtents,
                        ShowcaseCastleFarPresentation.ProxyKey,
                        instance.StyleKey,
                        instance.Tier,
                        instance.Flags);
                }
                _renderInstances.Add(instance);
            }

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
            _visibility.Clear();
            _renderInstances.Clear();
            if (_renderer != null) _renderer.Clear();
        }

        private void EnsurePolicyAndSource()
        {
            if (_world == null) return;

            Camera camera = _camera != null ? _camera : Camera.main;
            float fov = camera != null ? camera.fieldOfView : 60f;
            int height = camera != null ? Mathf.Max(1, camera.pixelHeight) : 1080;
            if (_policy == null || !Mathf.Approximately(_policyFov, fov) || _policyHeight != height)
            {
                _policyFov = fov;
                _policyHeight = height;
                _policy = new FarWorldVisibilityPolicy(
                    new FarWorldVisibilityPolicy.Thresholds(
                        midEnterPixels: 24f,
                        midExitPixels: 18f,
                        farEnterPixels: 8f,
                        farExitPixels: 5f,
                        horizonEnterPixels: 1.4f,
                        horizonExitPixels: 0.8f),
                    new FarWorldVisibilityPolicy.DistanceCaps(
                        ordinaryMetres: 1500f,
                        settlementAnchorMetres: 4000f,
                        landmarkMetres: 8000f,
                        horizonLandmarkMetres: 12500f),
                    fov,
                    height);
                _source = new ShowcaseFarStructureSource(
                    _visibility.Source,
                    (record, cameraXZ) => _policy.Select(record, cameraXZ),
                    GroundHeightMetres);
            }
        }

        private float GroundHeightMetres(float2 worldXZMetres)
        {
            if (_world == null) return 0f;
            int voxelX = Mathf.FloorToInt(worldXZMetres.x / ShowcaseWorld.VoxelSize);
            int voxelZ = Mathf.FloorToInt(worldXZMetres.y / ShowcaseWorld.VoxelSize);
            return _world.SurfaceHeight(voxelX, voxelZ) * ShowcaseWorld.VoxelSize;
        }

        private void OnDestroy()
        {
            Unbind();
        }
    }
}
