using System;
using UnityEngine;
using VoxelEngine.Composition;

namespace VoxelEngine.Showcase.Validation
{
    /// <summary>
    /// Focused Showcase-owned built-player proof that production authored content can become
    /// vertically presentation-ready without changing the normal horizontal streaming radius.
    /// Geometry, storage, streaming and rendering all come from ShowcaseWorld.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FeatureResidencyValidation : MonoBehaviour
    {
        private const uint Seed = 0x5EED1234u;
        private const int BrickPoolCapacity = 800000;
        private const int LoadRadiusRegions = 4;
        private const int UnloadRadiusRegions = 6;
        private const float GenerateBudgetMs = 4f;
        private const float TimeoutSeconds = 35f;
        private static readonly Vector3 TargetMetres = new Vector3(154f, 30f, 56f);

        private ShowcaseWorld _world;
        private float _started;
        private bool _reported;

        private void Start()
        {
            gameObject.tag = "MainCamera";
            Camera cameraComponent = gameObject.AddComponent<Camera>();
            cameraComponent.clearFlags = CameraClearFlags.Skybox;
            cameraComponent.nearClipPlane = 0.05f;
            cameraComponent.farClipPlane = 2500f;
            cameraComponent.fieldOfView = 62f;

            _world = new ShowcaseWorld(
                Seed,
                BrickPoolCapacity,
                LoadRadiusRegions,
                UnloadRadiusRegions,
                maxMixedBrickAllocationBytes: long.MaxValue);

            if (_world.IsPresentationColumnContentSettled(TargetMetres))
                throw new InvalidOperationException(
                    "FEATURE_RESIDENCY failure: fresh production farmhouse column unexpectedly began settled.");

            RenderingComposition.ResetSurfacePassDiagnostics("showcase-feature-residency-validation-enabled");
            RenderingComposition.SetSurfaceBuildEnabled(false);
            RenderingComposition.SetFarBaseHeight(ShowcaseWorld.BaseHeightVoxels);
            RenderingComposition.SetVoxelRingRadiusMetres(LoadRadiusRegions * ShowcaseWorld.RegionMetres);
            RenderingComposition.SetVoxelDetailBandScale(0.8f);

            _world.StartWorldbuildingGalleryBlocking(null);
            if (!_world.HasGalleryContent)
                throw new InvalidOperationException(
                    "FEATURE_RESIDENCY failure: production Worldbuilding Gallery content did not start.");

            var renderingWorld = new RenderingWorldBinding(
                _world.ReadStorage,
                _world.Palette,
                _world.SurfaceRules,
                _world.CoatingRules,
                _world.ProfileBlocks);
            RenderingComposition.ConfigureWorld(
                in renderingWorld,
                _world.Changes,
                _world.Seed,
                farFieldEnabled: false);
            RenderingComposition.SetSurfaceBuildEnabled(true);

            _started = Time.unscaledTime;
            PositionCamera();
            Debug.Log(
                "FEATURE_RESIDENCY pending: initial=false " +
                $"target=detailed-farmhouse loadRadius={LoadRadiusRegions} unloadRadius={UnloadRadiusRegions}");
        }

        private void Update()
        {
            if (_world == null) return;

            PositionCamera();
            _world.StepStreaming(TargetMetres, GenerateBudgetMs);

            if (!_reported && _world.IsPresentationColumnContentSettled(TargetMetres))
            {
                _reported = true;
                Debug.Log(
                    "FEATURE_RESIDENCY ready: initial=false settled=true " +
                    $"loadRadius={LoadRadiusRegions} horizontalRadiusUnchanged=true " +
                    $"regionsGenerated={_world.RegionsGenerated}");
            }

            if (!_reported && Time.unscaledTime - _started > TimeoutSeconds)
            {
                _reported = true;
                Debug.LogError(
                    "FEATURE_RESIDENCY failure: production farmhouse column did not become content-settled " +
                    $"within {TimeoutSeconds:0}s at unchanged radius {LoadRadiusRegions}.");
            }
        }

        private void PositionCamera()
        {
            transform.position = new Vector3(132f, 48f, 24f);
            Vector3 lookTarget = new Vector3(TargetMetres.x, 27f, TargetMetres.z);
            transform.rotation = Quaternion.LookRotation((lookTarget - transform.position).normalized, Vector3.up);
        }

        private void OnDestroy()
        {
            RenderingComposition.ResetTransientPresentation();
            RenderingComposition.ClearWorld();
            RenderingComposition.SetSurfaceBuildEnabled(true);
            _world?.StopBackgroundWork();
            _world?.Dispose();
            _world = null;
        }
    }
}
