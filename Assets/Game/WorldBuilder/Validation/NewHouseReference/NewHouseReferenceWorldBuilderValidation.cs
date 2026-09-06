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
    /// Module-owned built-player proof for the pinned reference-house WorldBuilder composition. The
    /// scene supplies deterministic site/camera/light policy; geometry, storage, game materials and
    /// voxel rendering all run through their production contracts.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class NewHouseReferenceWorldBuilderValidation : MonoBehaviour
    {
        private const float VoxelMetres = 0.1f;
        private const float SurfaceDiagnosticIntervalSeconds = 5f;
        private const string CpuFallbackVariable = "VOXEL_DISABLE_GPU_CUTOVER";

        [SerializeField] private uint m_Seed = 0x484F5553u;
        [SerializeField] private int m_BrickPoolCapacity = 196608;
        [SerializeField] private int m_LoadRadiusRegions = 2;
        [SerializeField] private int m_UnloadRadiusRegions = 3;

        private ShowcaseWorld _world;
        private int3 _origin;
        private NewHouseReferenceResult _result;
        private Vector3 _cameraTarget;
        private Vector3 _frontalPosition;
        private bool _ready;
        private float _nextSurfaceDiagnosticAt = SurfaceDiagnosticIntervalSeconds;

        private void Awake()
        {
            // This feature's visual acceptance remains CPU-rendered until the separate production
            // GPU restoration assignment lands. VOXEL_DISABLE_GPU_CUTOVER is an existing production
            // emergency/A-B path, not a test renderer.
            if (!Application.isEditor)
                Environment.SetEnvironmentVariable(CpuFallbackVariable, "1");

            Debug.Log("NEW_HOUSE_VALIDATION renderer=cpu-fallback");
        }

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
                    "materials=plaster,timber,roof,stone,glass,painted-blue,gold,sand-ground,moss-foliage " +
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

            float elapsedSeconds = Time.timeSinceLevelLoad;
            UpdateEvidenceCamera(elapsedSeconds);

            if (elapsedSeconds >= _nextSurfaceDiagnosticAt)
            {
                LogSurfaceDiagnostics(elapsedSeconds);
                _nextSurfaceDiagnosticAt += SurfaceDiagnosticIntervalSeconds;
            }
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
                m_UnloadRadiusRegions,
                GameMaterialComposition.SimulationDefinitions(),
                GameMaterialComposition.ShowcaseMaterials,
                features: ShowcaseFeatureContent.CastleOnly);

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

            // All roles remain normal game materials. Sand removes the iteration-1 dark rectangular
            // lawn/apron contrast, Moss gives connected ivy a readable mid-green value, and Gold is
            // reserved for the door/banner/sign/crest accents visible in the pinned image.
            NewHouseReferencePalette palette = new(
                GameMaterialIds.HousePlaster,
                GameMaterialIds.HouseTimber,
                GameMaterialIds.HouseRoof,
                GameMaterialIds.HouseStone,
                GameMaterialIds.Glass,
                GameMaterialIds.HouseTimber,
                GameMaterialIds.HouseDoor,
                GameMaterialIds.Sand,
                GameMaterialIds.FlowerWhite,
                GameMaterialIds.Moss,
                GameMaterialIds.Gold);

            _result = NewHouseReferenceRefinement.AuthorHouse(
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

            _world.PublishStructureAuthoringChanges();

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
            cameraComponent.fieldOfView = 40f;

            _cameraTarget = new Vector3(
                (_origin.x + config.Width * 0.5f) * VoxelMetres,
                (surfaceY + 70f) * VoxelMetres,
                (_origin.z + 10f) * VoxelMetres);
            _frontalPosition = new Vector3(
                _cameraTarget.x,
                _cameraTarget.y + 0.2f,
                _cameraTarget.z - 23.0f);
            ApplyCamera(_frontalPosition);
        }

        private void UpdateEvidenceCamera(float elapsedSeconds)
        {
            // SceneIssue replay captures near 10/20/30 seconds: target, front-left, rear-right.
            if (elapsedSeconds < 16f)
            {
                ApplyCamera(_frontalPosition);
                return;
            }

            if (elapsedSeconds < 26f)
            {
                ApplyCamera(_cameraTarget + new Vector3(-13f, 2.0f, -16f));
                return;
            }

            ApplyCamera(_cameraTarget + new Vector3(12f, 2.2f, 15f));
        }

        private void LogSurfaceDiagnostics(float elapsedSeconds)
        {
            RenderingComposition.GetVoxelSurfaceCounts(out int visible, out int missingVisible);
            bool hasBuildStatus = RenderingComposition.TryGetSurfaceBuildStatus(
                out int known,
                out int dirty,
                out int resident,
                out long residentGeometryBytes);
            bool completeCoverage = RenderingComposition.HasCompletePublishedNearSurfaceCoverage();
            string rings = RenderingComposition.DescribeVoxelRings() ?? "unavailable";
            rings = rings.Replace('\n', '|').Replace('\r', ' ');

            Debug.Log(
                "NEW_HOUSE_SURFACE " +
                $"t={elapsedSeconds:F1} visible={visible} missingVisible={missingVisible} " +
                $"known={known} dirty={dirty} resident={resident} bytes={residentGeometryBytes} " +
                $"hasStatus={hasBuildStatus} completeCoverage={completeCoverage} " +
                $"camera={transform.position} rings={rings}");
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

            light.intensity = 1.12f;
            light.color = new Color(1f, 0.91f, 0.76f, 1f);
            light.transform.rotation = Quaternion.Euler(42f, -38f, 0f);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.42f, 0.50f, 0.58f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.30f, 0.29f, 0.27f, 1f);
            RenderSettings.ambientGroundColor = new Color(0.13f, 0.11f, 0.09f, 1f);
        }
    }
}
