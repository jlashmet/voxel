using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Composition;
using VoxelEngine.Rendering.Api;
using VoxelEngine.Vegetation.Api;

namespace VoxelEngine.Showcase
{
    [AddComponentMenu("VoxelEngine/Showcases/Vegetation Rendering Showcase")]
    [DisallowMultipleComponent]
    public sealed class VegetationRenderingShowcase : MonoBehaviour
    {
        public const int InstancesPerKind = 3;
        internal const float VisibilitySectorSizeMetres = 32f;
        internal const float OrdinaryVisibilityRadiusMetres = 180f;

        [SerializeField] private uint m_Seed = 0x71E6A710u;
        [SerializeField] private bool m_CreateEnvironment = true;

        private readonly List<VegetationInstance> _instances = new();
        private readonly List<VegetationVisibilityEntry> _visibilityScratch = new();
        private readonly List<VegetationInstance> _visibleInstances = new();
        private IVegetationBatchRenderer _renderer;
        private FarFeatureSelectionPolicy _visibilityPolicy;
        private ulong _submittedVisibilityHash = ulong.MaxValue;

        public IVegetationBatchRenderer Renderer => _renderer;
        public IReadOnlyList<VegetationInstance> Instances => _instances;
        public int InstanceCount => _instances.Count;
        public int VisibleInstanceCount => _visibleInstances.Count;

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            Rebuild();
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying || _renderer == null) return;
            RefreshVisibleSubmission(Camera.main, force: false);
        }

        public void Rebuild()
        {
            if (_renderer == null)
                _renderer = VegetationLifeRenderingComposition.EnsureVegetationBatchRenderer(gameObject);

            if (m_CreateEnvironment)
                SubsystemRenderingShowcaseEnvironment.Ensure(transform);

            BuildInstances(m_Seed, _instances);
            _visibilityPolicy = CreateVisibilityPolicy(Camera.main);
            _submittedVisibilityHash = ulong.MaxValue;
            RefreshVisibleSubmission(Camera.main, force: true);
        }

        private void RefreshVisibleSubmission(Camera camera, bool force)
        {
            if (_visibilityPolicy == null)
                _visibilityPolicy = CreateVisibilityPolicy(camera);

            float3 cameraPosition = camera != null
                ? (float3)camera.transform.position
                : (float3)transform.position;
            SelectVisibleInstances(
                _instances,
                cameraPosition,
                _visibilityPolicy,
                _visibilityScratch,
                _visibleInstances);

            ulong hash = VisibilityHash(_visibleInstances);
            if (!force && hash == _submittedVisibilityHash) return;
            _submittedVisibilityHash = hash;
            _renderer.SetInstances(_visibleInstances);
        }

        internal static FarFeatureSelectionPolicy CreateVisibilityPolicy(Camera camera)
        {
            float verticalFov = camera != null ? camera.fieldOfView : 60f;
            int viewportHeight = camera != null && camera.pixelHeight > 0 ? camera.pixelHeight : 1080;
            return new FarFeatureSelectionPolicy(
                new FarFeatureSelectionPolicy.Thresholds(
                    midEnterPixels: 18f,
                    midExitPixels: 14f,
                    farEnterPixels: 8f,
                    farExitPixels: 6f,
                    horizonEnterPixels: 2f,
                    horizonExitPixels: 1f),
                new FarFeatureSelectionPolicy.DistanceCaps(
                    defaultMetres: OrdinaryVisibilityRadiusMetres,
                    importantMetres: ShowcaseFarFeatureRuntime.RadiusMetres + 250f,
                    horizonMetres: ShowcaseFarFeatureRuntime.RadiusMetres + 250f),
                verticalFov,
                viewportHeight);
        }

        internal static void SelectVisibleInstances(
            IReadOnlyList<VegetationInstance> source,
            float3 cameraPosition,
            FarFeatureSelectionPolicy policy,
            List<VegetationVisibilityEntry> visibilityScratch,
            List<VegetationInstance> output)
        {
            output.Clear();
            if (source == null || policy == null || visibilityScratch == null) return;

            VisibilitySectorBounds sectors = VisibilitySectorBounds.Around(
                new float2(cameraPosition.x, cameraPosition.z),
                OrdinaryVisibilityRadiusMetres,
                VisibilitySectorSizeMetres);
            VegetationVisibility.QueryVegetation(
                source,
                VisibilitySectorSizeMetres,
                in sectors,
                visibilityScratch);

            for (int i = 0; i < visibilityScratch.Count; i++)
            {
                VegetationVisibilityEntry entry = visibilityScratch[i];
                VegetationInstance instance = entry.Instance;
                float scale = math.max(0.05f, instance.Scale);
                var extents = new float3(0.5f * scale, scale, 0.5f * scale);
                float3 center = instance.PositionMetres + new float3(0f, extents.y, 0f);
                FarFeatureTier tier = policy.Select(
                    entry.StableId,
                    center,
                    extents,
                    cameraPosition,
                    FarFeatureImportance.Default);
                if (tier != FarFeatureTier.Culled)
                    output.Add(instance);
            }
        }

        private static ulong VisibilityHash(IReadOnlyList<VegetationInstance> instances)
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong hash = offset;
            for (int i = 0; i < instances.Count; i++)
            {
                ulong id = VegetationVisibility.StableVegetationId(instances[i]);
                for (int shift = 0; shift < 64; shift += 8)
                {
                    hash ^= (byte)(id >> shift);
                    hash *= prime;
                }
            }
            return hash;
        }

        public static void BuildInstances(uint seed, List<VegetationInstance> output)
        {
            output.Clear();
            const int columns = 8;
            const float spacing = 2.15f;

            for (int i = 0; i < VegetationCatalogue.Count; i++)
            {
                VegetationKind kind = VegetationCatalogue.KindAt(i);
                VegetationProfile profile = VegetationCatalogue.Get(kind);
                int column = i % columns;
                int row = i / columns;

                for (int sample = 0; sample < InstancesPerKind; sample++)
                {
                    uint instanceSeed = seed + (uint)i * 0x9E3779B9u + (uint)sample * 0x85EBCA6Bu;
                    float sampleOffset = (sample - 1) * 0.34f;
                    float3 normal = new float3(0f, 1f, 0f);
                    float3 position;

                    if (profile.GrowthForm == VegetationGrowthForm.Climber
                        || profile.GrowthForm == VegetationGrowthForm.Hanger)
                    {
                        normal = new float3(0f, 0f, -1f);
                        position = new float3(
                            (column - 3.5f) * spacing + sampleOffset,
                            0.85f + row * 0.52f + sample * 0.22f,
                            9.72f);
                    }
                    else
                    {
                        position = new float3(
                            (column - 3.5f) * spacing + sampleOffset,
                            0f,
                            1.3f + row * spacing + sample * 0.12f);
                    }

                    output.Add(new VegetationInstance
                    {
                        PositionMetres = position,
                        SurfaceNormal = normal,
                        Kind = kind,
                        Seed = instanceSeed == 0u ? 1u : instanceSeed,
                        Scale = 0.88f + sample * 0.12f,
                    });
                }
            }
        }
    }
}
