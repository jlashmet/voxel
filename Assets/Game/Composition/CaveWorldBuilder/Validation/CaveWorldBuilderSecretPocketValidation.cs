using System;
using Game.Materials.Api;
using Game.Structures.Api;
using Game.Structures.Runtime;
using Game.WorldBuilder.Api;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Composition;
using VoxelEngine.Showcase;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;
using VoxelEngine.Terrain.Api;

namespace Game.Composition.CaveWorldBuilder.Validation
{
    /// <summary>
    /// Focused built-player proof owned by CaveWorldBuilder. The scene contributes only a camera and
    /// deterministic orchestration; cave generation, secret-pocket selection, fracture presentation,
    /// voxel storage, rendering and destruction all execute through production implementations.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class CaveWorldBuilderSecretPocketValidation : MonoBehaviour
    {
        private const int AnchorX = -1024;
        private const int AnchorZ = 512;
        private const float VoxelMetres = ShowcaseWorld.VoxelSize;

        // Keep the focused validation on the same deterministic world seed as the production
        // Showcase driver. ShowcaseWorld constructs the production feature catalogue eagerly,
        // so an arbitrary validation-only seed can fail unrelated Kentridge placement before the
        // CaveWorldBuilder path under test starts.
        [SerializeField] private uint m_Seed = 0x5EED1234u;
        [SerializeField] private int m_BrickPoolCapacity = 196608;
        [SerializeField] private int m_LoadRadiusRegions = 2;
        [SerializeField] private int m_UnloadRadiusRegions = 3;
        [SerializeField] private float m_GenerateBudgetMs = 4f;

        private ShowcaseWorld _world;
        private CaveSecretPocketProjection _projection;
        private float _sequenceStart;
        private bool _breached;
        private bool _ready;

        private void OnEnable()
        {
            if (!Application.isPlaying) return;

            Camera cameraComponent = GetComponent<Camera>();
            cameraComponent.clearFlags = CameraClearFlags.Skybox;
            cameraComponent.nearClipPlane = 0.05f;
            cameraComponent.farClipPlane = 2500f;
            cameraComponent.fieldOfView = 68f;

            _world = new ShowcaseWorld(m_Seed, m_BrickPoolCapacity, m_LoadRadiusRegions, m_UnloadRadiusRegions);
            RenderingComposition.ResetSurfacePassDiagnostics("cave-worldbuilder-secret-pocket-validation-enabled");
            RenderingComposition.SetSurfaceBuildEnabled(false);
            RenderingComposition.SetFarBaseHeight(ShowcaseWorld.BaseHeightVoxels);
            RenderingComposition.SetVoxelRingRadiusMetres(m_LoadRadiusRegions * ShowcaseWorld.RegionMetres);
            RenderingComposition.SetVoxelDetailBandScale(0.8f);

            int surfaceY = TerrainQuery.HeightAt(AnchorX, AnchorZ, m_Seed);
            int3 entrance = new int3(AnchorX, surfaceY + 1, AnchorZ);
            PreloadAround(entrance);

            IStructureAuthoringSession authoring = _world.CreateStructureAuthoringSession(4_000_000);
            CaveConfig caveConfig = CaveConfig.Default;
            caveConfig.MainSegmentCount = 10;
            caveConfig.MaxBranches = 4;
            caveConfig.MaxBranchDepth = 2;
            caveConfig.BranchSegmentCount = 5;
            caveConfig.BranchChancePercent = 70;
            caveConfig.ChamberChancePercent = 25;
            caveConfig.SurfaceDescentSegments = 6;
            caveConfig.SurfaceDescentPerSegment = 8;
            caveConfig.BoundsHalfExtents = new int3(240, 96, 240);
            caveConfig.MinVerticalOffset = -72;
            caveConfig.MaxVerticalOffset = 16;

            CaveGenerationRequest request = CaveGenerationRequest.Standalone(
                0x4341564553454352ul,
                m_Seed,
                entrance,
                Facing.North,
                caveConfig.TunnelWidth,
                caveConfig.TunnelHeight,
                10);
            CaveMaterialPalette palette = new CaveMaterialPalette
            {
                Opening = GameMaterialIds.Empty,
                Rock = GameMaterialIds.DarkStone,
                Accent = GameMaterialIds.MasonryMedium,
                Decoration = GameMaterialIds.Moss,
                Water = GameMaterialIds.Water,
            };

            CaveAuthoringResult cave = CaveAuthoring.Author(authoring, in request, in caveConfig, in palette);
            if (cave.TraversalCandidates.Count < 2)
                throw new InvalidOperationException("CaveWorldBuilder validation did not produce enough reachable terminals.");

            CampaignBuilder campaign = Campaign.Create("cave-worldbuilder-secret-pocket-validation");
            RegionHandle region = campaign.World.Region("validation-cave");
            SiteRef hidden = region.Site(
                "hidden-pocket",
                SiteArchetype.Ruin,
                x => x.RequireCapability(SiteCapability.SecretCandidateHost));
            CavePlacementRequirements requirements = CavePlacementRequirements.AnyReachableTerminal(40);
            CavePlacementPreferences preferences = CavePlacementPreferences.PreferBranchTerminal;
            CaveSecretPocketConfig pocketConfig = new CaveSecretPocketConfig
            {
                BarrierThickness = 3,
                EntranceWidth = 12,
                EntranceHeight = 20,
                ConnectorLength = 8,
                PocketWidth = 28,
                PocketHeight = 24,
                PocketDepth = 30,
            };

            if (!CaveSecretPocketComposition.TryAuthorBest(
                    authoring,
                    in cave.TraversalCandidates,
                    in requirements,
                    in preferences,
                    hidden,
                    9500,
                    in pocketConfig,
                    out _projection,
                    out CaveSecretPocketCompositionFailure failure))
                throw new InvalidOperationException("CaveWorldBuilder secret-pocket composition failed: " + failure);

            var clueConfig = new CaveSecretPocketCluePresentationConfig(
                Coatings.Soot,
                64,
                m_Seed ^ 0x434C5545u);
            if (!CaveSecretPocketCluePresentation.TryApplyBoundaryEvidence(
                    authoring,
                    in _projection,
                    in clueConfig,
                    out int coatedVoxels))
                throw new InvalidOperationException("CaveWorldBuilder boundary clue presentation failed.");
            if (coatedVoxels <= 0)
                throw new InvalidOperationException("CaveWorldBuilder boundary clue presentation produced no evidence.");

            var renderingWorld = new RenderingWorldBinding(
                _world.ReadStorage,
                _world.Palette,
                _world.SurfaceRules,
                _world.CoatingRules,
                _world.ProfileBlocks);
            RenderingComposition.ConfigureWorld(in renderingWorld, _world.Changes, _world.Seed, farFieldEnabled: false);
            RenderingComposition.SetSurfaceBuildEnabled(true);

            _breached = false;
            _sequenceStart = Time.time;
            _ready = true;
            PlacePreBreakPose(far: true);
            Debug.Log(
                "CaveWorldBuilder secret validation ready: " +
                $"terminals={cave.TraversalCandidates.Count} clueVoxels={coatedVoxels} " +
                $"barrier={_projection.Pocket.Barrier.Min}->{_projection.Pocket.Barrier.MaxExclusive}");
        }

        private void Update()
        {
            if (!_ready || _world == null) return;

            float elapsed = Time.time - _sequenceStart;
            if (elapsed < 6f)
                PlacePreBreakPose(far: true);
            else if (elapsed < 12f)
                PlacePreBreakPose(far: false);
            else
            {
                if (!_breached) BreachWall();
                PlaceRevealedPose();
            }

            _world.StepStreaming(transform.position, m_GenerateBudgetMs);
        }

        private void OnDisable()
        {
            _ready = false;
            _breached = false;
            RenderingComposition.ResetTransientPresentation();
            RenderingComposition.ClearWorld();
            RenderingComposition.SetSurfaceBuildEnabled(true);
            _world?.StopBackgroundWork();
            _world?.Dispose();
            _world = null;
        }

        private void PreloadAround(int3 entrance)
        {
            int3 centre = ShowcaseWorld.RegionAt((float3)entrance * VoxelMetres);
            for (int z = -1; z <= 1; z++)
            for (int x = -1; x <= 1; x++)
                _world.GenerateRegionBlocking(centre + new int3(x, 0, z));
        }

        private void PlacePreBreakPose(bool far)
        {
            CaveSecretPocket pocket = _projection.Pocket;
            int3 forward = FacingVector(pocket.Terminal.ExitFacing);
            int distance = far ? 15 : 7;
            float3 eye = (float3)(pocket.Terminal.Position - forward * distance + new int3(0, 12, 0));
            PlaceVoxelPose(eye, BoundsCentre(pocket.Barrier));
        }

        private void PlaceRevealedPose()
        {
            CaveSecretPocket pocket = _projection.Pocket;
            float3 eye = BoundsCentre(pocket.Connector);
            eye.y = pocket.Connector.Min.y + 12f;
            float3 target = BoundsCentre(pocket.Pocket);
            target.y = pocket.Pocket.Min.y + 11f;
            PlaceVoxelPose(eye, target);
        }

        private void BreachWall()
        {
            CaveSecretPocket pocket = _projection.Pocket;
            int3 centre = new int3(
                (pocket.Barrier.Min.x + pocket.Barrier.MaxExclusive.x - 1) / 2,
                (pocket.Barrier.Min.y + pocket.Barrier.MaxExclusive.y - 1) / 2,
                (pocket.Barrier.Min.z + pocket.Barrier.MaxExclusive.z - 1) / 2);
            int changed = _world.Explode(centre, 9, (float3)FacingVector(pocket.Terminal.ExitFacing));
            if (changed <= 0)
                throw new InvalidOperationException("CaveWorldBuilder production destruction did not breach the secret wall.");

            _breached = true;
            Debug.Log($"CaveWorldBuilder secret validation wall destroyed: voxels={changed} centre={centre}");
        }

        private void PlaceVoxelPose(float3 eyeVoxels, float3 targetVoxels)
        {
            transform.position = (Vector3)(eyeVoxels * VoxelMetres);
            Vector3 target = (Vector3)(targetVoxels * VoxelMetres);
            Vector3 direction = target - transform.position;
            if (direction.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        private static float3 BoundsCentre(in DecorationBounds bounds) =>
            ((float3)bounds.Min + (float3)bounds.MaxExclusive) * 0.5f;

        private static int3 FacingVector(Facing facing)
        {
            return facing switch
            {
                Facing.North => new int3(0, 0, 1),
                Facing.South => new int3(0, 0, -1),
                Facing.East => new int3(1, 0, 0),
                Facing.West => new int3(-1, 0, 0),
                _ => throw new ArgumentOutOfRangeException(nameof(facing), facing, null),
            };
        }
    }
}
