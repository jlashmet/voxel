using System;
using Game.Composition.Materials;
using Game.Materials.Api;
using Game.WorldBuilder.Voxel;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelEngine.Composition;
using VoxelEngine.Showcase;
using VoxelEngine.Structures.Api;
using TerrainQuery = VoxelEngine.Terrain.Api.TerrainQuery;

namespace Game.WorldBuilder.Validation.NewHouseReference
{
    /// <summary>
    /// Module-owned built-player proof for the reference-house WorldBuilder composition. The scene
    /// supplies only deterministic site/camera/light policy; geometry, storage, game materials and
    /// voxel rendering all run through their production contracts.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class NewHouseReferenceWorldBuilderValidation : MonoBehaviour
    {
        private const float VoxelMetres = 0.1f;

        [SerializeField] private uint m_Seed = 0x484F5553u;
        [SerializeField] private int m_BrickPoolCapacity = 196608;
        [SerializeField] private int m_LoadRadiusRegions = 2;
        [SerializeField] private int m_UnloadRadiusRegions = 3;
        [SerializeField] private float m_GenerateBudgetMs = 5f;

        private ShowcaseWorld _world;
        private int3 _origin;
        private NewHouseReferenceResult _result;
        private Vector3 _cameraTarget;
        private Vector3 _frontalPosition;
        private bool _ready;

        private void Start()
        {
            try
            {
                BuildReferenceComposition();
                _ready = true;
                Debug.Log(
                    "NEW_HOUSE_VALIDATION ready: " +
                    $"origin={_origin} bounds={_result.Min}->{_result.MaxExclusive} " +
                    $"doorX={_result.DoorCentreX} frontZ={_result.FrontZ} ridgeY={_result.RidgeY} " +
                    "materials=plaster,timber,roof,stone,glass,door,foliage " +
                    "referenceCamera=frontal portrait; audit=front-left,rear-right");
            }
            catch (Exception exception)
            {
                Debug.LogError("NEW_HOUSE_VALIDATION failure: " + exception);
                enabled = false;
            }
        }

        private void Update()
        {
            if (!_ready || _world == null) return;
            _world.StepStreaming(transform.position, m_GenerateBudgetMs);
            UpdateEvidenceCamera(Time.timeSinceLevelLoad);
        }

        private void OnDestroy()
        {
            _ready = false;
            RenderingComposition.ResetTransientPresentation();
            RenderingComposition.ClearWorld();
            RenderingComposition.SetSurfaceBuildEnabled(true);
            _world?.StopBackgroundWork();
            _world?.Dispose();
            _world = null;
        }

        private void BuildReferenceComposition()
        {
            GameMaterialComposition.Install();

            _world = new ShowcaseWorld(
                m_Seed,
                m_BrickPoolCapacity,
                m_LoadRadiusRegions,
                m_UnloadRadiusRegions);

            RenderingComposition.ResetSurfacePassDiagnostics("new-house-worldbuilder-validation");
            RenderingComposition.SetSurfaceBuildEnabled(false);
            RenderingComposition.SetFarBaseHeight(ShowcaseWorld.BaseHeightVoxels);
            RenderingComposition.SetVoxelRingRadiusMetres(
                m_LoadRadiusRegions * ShowcaseWorld.RegionMetres);
            RenderingComposition.SetVoxelDetailBandScale(0.8f);

            int surfaceY = TerrainQuery.HeightAt(0, 0, m_Seed);
            NewHouseReferenceConfig config = NewHouseReferenceConfig.Default;
            _origin = new int3(-config.Width / 2, surfaceY + 1, 20);
            PreloadAround(_origin + new int3(config.Width / 2, 0, config.Depth / 2));

            IStructureAuthoringSession authoring = _world.CreateStructureAuthoringSession(8_000_000);
            NewHouseReferencePalette palette = new(
                GameMaterialIds.HousePlaster,
                GameMaterialIds.HouseTimber,
                GameMaterialIds.HouseRoof,
                GameMaterialIds.HouseStone,
                GameMaterialIds.Glass,
                GameMaterialIds.HouseDoor,
                GameMaterialIds.Slate,
                GameMaterialIds.Grass,
                GameMaterialIds.FlowerWhite,
                GameMaterialIds.HouseFoliage);

            _result = NewHouseReferenceAuthoring.AuthorHouse(
                authoring, _origin, in config, in palette);
            NewHouseReferenceAuthoring.AuthorReferenceSite(
                authoring, _origin, in config, in palette);

            if (authoring.BudgetExceeded)
                throw new InvalidOperationException(
                    "Reference house exceeded the production structure-authoring write budget.");
            if (authoring.TotalVoxelsWritten <= 0)
                throw new InvalidOperationException("Reference house authored no voxel changes.");

            int foundationMaterial = authoring.Get(
                _origin.x + 4, _origin.y + config.FoundationHeight / 2, _origin.z + config.Depth / 2);
            if (foundationMaterial != GameMaterialIds.HouseStone)
                throw new InvalidOperationException(
                    $"Expected authored house-stone foundation, found material {foundationMaterial}.");

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

            ConfigureCamera(surfaceY, in config);
            EnsureLighting();
        }

        private void PreloadAround(int3 centreVoxel)
        {
            int3 centreRegion = ShowcaseWorld.RegionAt((float3)centreVoxel * VoxelMetres);
            for (int z = -1; z <= 1; z++)
            for (int x = -1; x <= 1; x++)
                _world.GenerateRegionBlocking(centreRegion + new int3(x, 0, z));
        }

        private void ConfigureCamera(int surfaceY, in NewHouseReferenceConfig config)
        {
            Camera cameraComponent = GetComponent<Camera>();
            cameraComponent.clearFlags = CameraClearFlags.Skybox;
            cameraComponent.nearClipPlane = 0.05f;
            cameraComponent.farClipPlane = 800f;
            cameraComponent.fieldOfView = 36f;

            // The supplied source is a portrait, almost perfectly frontal architectural plate.
            // Keep the primary proof on that axis; later fixed phases expose side/rear surfaces so
            // holes, floating geometry and roof intersections cannot hide behind the hero view.
            _cameraTarget = new Vector3(
                (_origin.x + config.Width * 0.5f) * VoxelMetres,
                (surfaceY + 70f) * VoxelMetres,
                (_origin.z + 16f) * VoxelMetres);
            _frontalPosition = new Vector3(
                _cameraTarget.x,
                _cameraTarget.y + 0.4f,
                _cameraTarget.z - 24.5f);
            ApplyCamera(_frontalPosition);
        }

        private void UpdateEvidenceCamera(float elapsedSeconds)
        {
            if (elapsedSeconds < 16f || elapsedSeconds >= 28f)
            {
                ApplyCamera(_frontalPosition);
                return;
            }

            if (elapsedSeconds < 22f)
            {
                ApplyCamera(_cameraTarget + new Vector3(-17f, 2.2f, -18f));
                return;
            }

            ApplyCamera(_cameraTarget + new Vector3(15f, 2.4f, 18f));
        }

        private void ApplyCamera(Vector3 position)
        {
            transform.position = position;
            transform.rotation = Quaternion.LookRotation(_cameraTarget - position, Vector3.up);
        }

        private static void EnsureLighting()
        {
            Light light = UnityEngine.Object.FindFirstObjectByType<Light>();
            if (light == null)
            {
                var lightObject = new GameObject("New House Validation Sun");
                light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
            }

            light.intensity = 1.25f;
            light.color = new Color(1f, 0.94f, 0.82f, 1f);
            light.transform.rotation = Quaternion.Euler(48f, -32f, 0f);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.54f, 0.66f, 0.78f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.38f, 0.39f, 0.38f, 1f);
            RenderSettings.ambientGroundColor = new Color(0.18f, 0.16f, 0.13f, 1f);
        }
    }
}
