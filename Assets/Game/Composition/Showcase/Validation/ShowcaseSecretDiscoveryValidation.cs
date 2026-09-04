using System;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Composition;

namespace VoxelEngine.Showcase.Validation
{
    /// <summary>
    /// Focused Showcase-owned built-player consumer for SecretDiscovery. It boots the production
    /// Worldbuilding Gallery image, composes the production secret consumer, and frames the natural
    /// environmental evidence and authored breakable boundary without acceptance-only geometry.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class ShowcaseSecretDiscoveryValidation : MonoBehaviour
    {
        [SerializeField] private uint m_Seed = 0x53484F57u;
        [SerializeField] private int m_BrickPoolCapacity = 196608;
        [SerializeField] private int m_LoadRadiusRegions = 2;
        [SerializeField] private int m_UnloadRadiusRegions = 3;
        [SerializeField] private float m_GenerateBudgetMs = 4f;

        private ShowcaseWorld _world;
        private float _sequenceStart;
        private bool _ready;

        private void OnEnable()
        {
            if (!Application.isPlaying) return;

            Camera cameraComponent = GetComponent<Camera>();
            cameraComponent.clearFlags = CameraClearFlags.Skybox;
            cameraComponent.nearClipPlane = 0.05f;
            cameraComponent.farClipPlane = 2500f;
            cameraComponent.fieldOfView = 75f;

            _world = new ShowcaseWorld(m_Seed, m_BrickPoolCapacity, m_LoadRadiusRegions, m_UnloadRadiusRegions);
            RenderingComposition.ResetSurfacePassDiagnostics("showcase-secret-discovery-validation-enabled");
            RenderingComposition.SetSurfaceBuildEnabled(false);
            RenderingComposition.SetFarBaseHeight(ShowcaseWorld.BaseHeightVoxels);
            RenderingComposition.SetVoxelRingRadiusMetres(m_LoadRadiusRegions * ShowcaseWorld.RegionMetres);
            RenderingComposition.SetVoxelDetailBandScale(0.8f);

            _world.StartWorldbuildingGalleryBlocking(null);
            if (!_world.HasGalleryContent)
                throw new InvalidOperationException("Showcase validation failed to boot production Worldbuilding Gallery content.");

            _world.EnsureWorldbuildingGallerySecretDiscoveryBlocking();
            if (!_world.HasWorldbuildingGallerySecretDiscoveryContent)
                throw new InvalidOperationException("Showcase validation failed to compose production SecretDiscovery content.");
            if (_world.WorldbuildingGallerySecretBoundaryClueVoxels <= 0 ||
                _world.WorldbuildingGalleryNaturalApproachClueVoxels <= 0)
                throw new InvalidOperationException("Showcase SecretDiscovery produced no visible clue evidence.");

            var renderingWorld = new RenderingWorldBinding(
                _world.ReadStorage,
                _world.Palette,
                _world.SurfaceRules,
                _world.CoatingRules,
                _world.ProfileBlocks);
            RenderingComposition.ConfigureWorld(in renderingWorld, _world.Changes, _world.Seed, farFieldEnabled: false);
            RenderingComposition.SetSurfaceBuildEnabled(true);

            _sequenceStart = Time.time;
            _ready = true;
            PlaceNaturalCluePose();
            Debug.Log(
                "Showcase secret validation ready: " +
                $"boundaryClueVoxels={_world.WorldbuildingGallerySecretBoundaryClueVoxels} " +
                $"naturalClueVoxels={_world.WorldbuildingGalleryNaturalApproachClueVoxels}");
        }

        private void Update()
        {
            if (!_ready || _world == null) return;

            float elapsed = Time.time - _sequenceStart;
            if (elapsed < 9f)
                PlaceNaturalCluePose();
            else
                PlaceBreakableCluePose();

            _world.StepStreaming(transform.position, m_GenerateBudgetMs);
        }

        private void OnDisable()
        {
            _ready = false;
            RenderingComposition.ResetTransientPresentation();
            RenderingComposition.ClearWorld();
            RenderingComposition.SetSurfaceBuildEnabled(true);
            _world?.StopBackgroundWork();
            _world?.Dispose();
            _world = null;
        }

        private void PlaceNaturalCluePose()
        {
            float3 position = _world.WorldbuildingGalleryNaturalSecretCameraPosition();
            float3 referenceTarget = _world.WorldbuildingGalleryNaturalSecretLookTarget();
            position += new float3(-4.5f, 1.2f, 0f);
            float3 target = new float3(position.x + 4.5f, referenceTarget.y, position.z + 3.2f);
            PlaceMetrePose(position, target);
        }

        private void PlaceBreakableCluePose()
        {
            float3 position = _world.WorldbuildingGalleryBreakableSecretCameraPosition();
            float3 target = _world.WorldbuildingGalleryBreakableSecretLookTarget();
            position = math.lerp(position, target, 0.35f);
            PlaceMetrePose(position, target);
        }

        private void PlaceMetrePose(float3 position, float3 target)
        {
            transform.position = (Vector3)position;
            Vector3 lookTarget = (Vector3)target;
            Vector3 direction = lookTarget - transform.position;
            if (direction.sqrMagnitude <= 0.001f)
                throw new InvalidOperationException("Showcase SecretDiscovery validation camera has no look direction.");
            transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }
    }
}
