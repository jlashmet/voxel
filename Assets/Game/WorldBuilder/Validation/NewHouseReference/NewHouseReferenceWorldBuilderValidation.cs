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
            // This feature's visual acceptance is intentionally CPU-rendered. The production
            // renderer keeps VOXEL_DISABLE_GPU_CUTOVER=1 as its supported emergency/A-B fallback,
            // and repository-owned module-player validation already launches scenes with that
            // setting. Set it before the first rendered frame so the standalone SceneIssue replay
            // exercises the same CPU surface path instead of depending on the unrelated GPU
            // restoration assignment.
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
                    "materials=plaster,timber,roof,stone,glass,painted-blue,ground-foliage " +
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

            // This proof preloads the complete deterministic footprint needed by the house before
            // authoring it. Running ShowcaseWorld.StepStreaming here is incorrect: that integration
            // loop also admits showcase landmarks (including the castle) and may evict/rebuild the
            // very regions being used as visual evidence as the audit camera moves. Keep the focused
            // WorldBuilder validation on its fixed production storage snapshot; only the evidence
            // camera changes after construction.
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

            // The legacy four-argument ShowcaseWorld constructor registers only the historical
            // showcase palette. That is insufficient for a WorldBuilder proof that authors stable
            // game material IDs 23-28: raw voxels can be written and read back while the bound world
            // palette still cannot classify those surfaces. Use the production game-material
            // constructor so storage/surface rules and renderer presentation share the same complete
            // catalogue. CastleOnly deliberately leaves the ordinary settlement catalogue empty;
            // this focused validation never advances landmark streaming, so no unrelated castle is
            // authored either.
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

            // The supplied texture set includes a combined timber/plaster facade plate that is not
            // appropriate for the separately authored geometry, and it has no isolated alpha-ready
            // foliage plate. Use the house timber texture for the plain brown entry and the normal
            // game foliage material for ivy/planting rather than stamping that combined plate onto
            // tiny foliage voxels. Slate remains the existing blue-painted architectural accent.
            NewHouseReferencePalette palette = new(
                GameMaterialIds.HousePlaster,
                GameMaterialIds.HouseTimber,
                GameMaterialIds.HouseRoof,
                GameMaterialIds.HouseStone,
                GameMaterialIds.Glass,
                GameMaterialIds.HouseTimber,
                GameMaterialIds.Slate,
                GameMaterialIds.Grass,
                GameMaterialIds.FlowerWhite,
                GameMaterialIds.Grass);

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

            // Structures.Api authoring mutates resident storage but deliberately does not own the
            // world's change journal. Publish the completed bounded authoring phase before binding
            // rendering so surface discovery receives every house/site region rather than only the
            // pre-existing terrain publication.
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
            cameraComponent.fieldOfView = 36f;

            // The supplied architectural plate leaves breathing room around the compact cottage.
            // Frame the corrected ~10 m silhouette rather than cropping it edge-to-edge.
            _cameraTarget = new Vector3(
                (_origin.x + config.Width * 0.5f) * VoxelMetres,
                (surfaceY + 50f) * VoxelMetres,
                (_origin.z + 14f) * VoxelMetres);
            _frontalPosition = new Vector3(
                _cameraTarget.x,
                _cameraTarget.y + 0.25f,
                _cameraTarget.z - 29.5f);
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
                ApplyCamera(_cameraTarget + new Vector3(-14f, 1.8f, -17f));
                return;
            }

            ApplyCamera(_cameraTarget + new Vector3(13f, 2.0f, 16f));
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

            light.intensity = 1.15f;
            light.color = new Color(1f, 0.95f, 0.86f, 1f);
            light.transform.rotation = Quaternion.Euler(48f, -32f, 0f);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.54f, 0.66f, 0.78f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.38f, 0.39f, 0.38f, 1f);
            RenderSettings.ambientGroundColor = new Color(0.18f, 0.16f, 0.13f, 1f);
        }
    }
}
