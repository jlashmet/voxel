using System;
using System.Collections.Generic;
using Game.Composition.CaveWorldBuilder;
using Game.Materials.Api;
using Game.Structures.Api;
using Game.Structures.Runtime;
using Game.WorldBuilder.Api;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Composition;
using VoxelEngine.Showcase;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;
using VoxelEngine.Vegetation.Api;
using TerrainSampler = VoxelEngine.Terrain.Api.TerrainQuery;
using TreeInstance = VoxelEngine.Vegetation.Api.TreeInstance;

namespace Game.WorldBuilder.Validation
{
    /// <summary>
    /// Focused built-player proof for generated secret discovery. The scene owns only validation
    /// orchestration and camera placement. Terrain/storage, cave generation, secret-pocket authoring,
    /// clue coating, materials, voxel meshing, and tree rendering all execute through production paths.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class WorldBuilderSecretDiscoveryValidation : MonoBehaviour
    {
        private const float VoxelMetres = 0.1f;
        private const int CaveAnchorX = -1024;
        private const int CaveAnchorZ = 512;

        [SerializeField] private uint m_Seed = 0x53454352u;
        [SerializeField] private int m_BrickPoolCapacity = 196608;
        [SerializeField] private int m_LoadRadiusRegions = 2;
        [SerializeField] private int m_UnloadRadiusRegions = 3;
        [SerializeField] private float m_GenerateBudgetMs = 4f;

        private ShowcaseWorld _world;
        private readonly List<TreeInstance> _trees = new List<TreeInstance>();
        private bool _ready;

        private void OnEnable()
        {
            if (!Application.isPlaying) return;

            Camera cameraComponent = GetComponent<Camera>();
            cameraComponent.clearFlags = CameraClearFlags.Skybox;
            cameraComponent.nearClipPlane = 0.05f;
            cameraComponent.farClipPlane = 2500f;

            _world = new ShowcaseWorld(
                m_Seed,
                m_BrickPoolCapacity,
                m_LoadRadiusRegions,
                m_UnloadRadiusRegions);

            RenderingComposition.ResetSurfacePassDiagnostics("worldbuilder-secret-discovery-validation-enabled");
            RenderingComposition.SetSurfaceBuildEnabled(false);
            RenderingComposition.SetFarBaseHeight(ShowcaseWorld.BaseHeightVoxels);
            RenderingComposition.SetVoxelRingRadiusMetres(m_LoadRadiusRegions * ShowcaseWorld.RegionMetres);
            RenderingComposition.SetVoxelDetailBandScale(0.8f);

            int surfaceY = TerrainSampler.HeightAt(CaveAnchorX, CaveAnchorZ, m_Seed);
            int3 caveEntrance = new int3(CaveAnchorX, surfaceY - 18, CaveAnchorZ);
            PreloadAround(caveEntrance);

            IStructureAuthoringSession authoring = StructuresComposition.CreateAuthoringSession(
                _world.ReadStorage,
                _world.MutationStorage,
                _world.Palette,
                writeBudget: 4_000_000);

            CaveConfig caveConfig = CaveConfig.Default;
            caveConfig.MainSegmentCount = 10;
            caveConfig.MaxBranches = 4;
            caveConfig.MaxBranchDepth = 2;
            caveConfig.BranchSegmentCount = 5;
            caveConfig.BranchChancePercent = 70;
            caveConfig.ChamberChancePercent = 25;
            caveConfig.SurfaceDescentSegments = 0;
            caveConfig.BoundsHalfExtents = new int3(240, 96, 240);
            caveConfig.MinVerticalOffset = -72;
            caveConfig.MaxVerticalOffset = 16;

            CaveGenerationRequest request = CaveGenerationRequest.Underground(
                0x5345435243415645ul,
                caveEntrance,
                Facing.North,
                caveConfig.TunnelWidth,
                caveConfig.TunnelHeight,
                10);
            CaveMaterialPalette cavePalette = new CaveMaterialPalette
            {
                Opening = GameMaterialIds.Empty,
                Rock = GameMaterialIds.DarkStone,
                Accent = GameMaterialIds.MasonryMedium,
                Decoration = GameMaterialIds.Moss,
                Water = GameMaterialIds.Water,
            };

            CaveAuthoringResult cave = CaveAuthoring.Author(
                authoring,
                in request,
                in caveConfig,
                in cavePalette);
            if (cave.TraversalCandidates.Count < 2)
                throw new InvalidOperationException("Validation cave did not produce enough traversal terminals.");

            Campaign campaign = Campaign.Create("worldbuilder-secret-discovery-validation");
            RegionHandle region = campaign.World.Region("generated-cave");
            SiteRef approach = region.Site("approach", SiteArchetype.Ruin);
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
                    out CaveSecretPocketProjection projection,
                    out CaveSecretPocketCompositionFailure failure))
                throw new InvalidOperationException("Production cave secret authoring failed: " + failure);

            var clueConfig = new CaveSecretPocketCluePresentationConfig(
                Coatings.Moss,
                46,
                m_Seed ^ 0x434C5545u);
            if (!CaveSecretPocketCluePresentation.TryApplyBoundaryEvidence(
                    authoring,
                    in projection,
                    in clueConfig,
                    out int coatedVoxels))
                throw new InvalidOperationException("Production cave clue presentation failed.");

            var route = new SecretRouteId("generated-cave/breakable-boundary");
            SecretClueAnchorSpec[] clueAnchors = CaveSecretPocketClueAnchors.ForAuthoredBreakable(
                approach,
                route);
            if (clueAnchors.Length < 2)
                throw new InvalidOperationException("Generated cave secret did not expose reusable clue anchors.");

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

            PublishProductionTrees(surfaceY);
            PlaceCamera(in projection);

            _ready = true;
            Debug.Log(
                "WorldBuilder secret validation ready: " +
                $"caveSegments={cave.SegmentsAuthored} branches={cave.BranchesAuthored} " +
                $"terminals={cave.TraversalCandidates.Count} clueAnchors={clueAnchors.Length} " +
                $"coatedBarrierVoxels={coatedVoxels} trees={_trees.Count} " +
                $"barrier={projection.Pocket.Barrier.Min}->{projection.Pocket.Barrier.MaxExclusive}");
        }

        private void Update()
        {
            if (!_ready || _world == null) return;
            _world.StepStreaming(transform.position, m_GenerateBudgetMs);
        }

        private void OnDisable()
        {
            _ready = false;
            VegetationComposition.ReplaceTreeWorld(Array.Empty<TreeInstance>());
            RenderingComposition.ResetTransientPresentation();
            RenderingComposition.ClearWorld();
            RenderingComposition.SetSurfaceBuildEnabled(true);
            _world?.StopBackgroundWork();
            _world?.Dispose();
            _world = null;
            _trees.Clear();
        }

        private void PreloadAround(int3 caveEntrance)
        {
            float3 metres = (float3)caveEntrance * VoxelMetres;
            int3 centre = ShowcaseWorld.RegionAt(metres);
            for (int z = -1; z <= 1; z++)
            for (int x = -1; x <= 1; x++)
                _world.GenerateRegionBlocking(centre + new int3(x, 0, z));
        }

        private void PublishProductionTrees(int surfaceY)
        {
            _trees.Clear();
            for (int i = 0; i < 24; i++)
            {
                float angle = i * (math.PI * 2f / 24f);
                float radius = 18f + (i % 5) * 2.5f;
                float xMetres = CaveAnchorX * VoxelMetres + math.cos(angle) * radius;
                float zMetres = CaveAnchorZ * VoxelMetres + math.sin(angle) * radius;
                int xVoxel = (int)math.round(xMetres / VoxelMetres);
                int zVoxel = (int)math.round(zMetres / VoxelMetres);
                int yVoxel = TerrainSampler.HeightAt(xVoxel, zVoxel, m_Seed);
                uint treeSeed = m_Seed ^ (uint)(i * 0x9E3779B9u + 1u);
                _trees.Add(new TreeInstance
                {
                    PositionMetres = new float3(xMetres, yVoxel * VoxelMetres, zMetres),
                    Species = (i & 1) == 0 ? TreeSpecies.Pine : TreeSpecies.Oak,
                    Seed = treeSeed == 0u ? 1u : treeSeed,
                    Scale = 0.9f + (i % 4) * 0.08f,
                });
            }
            VegetationComposition.ReplaceTreeWorld(_trees);
        }

        private void PlaceCamera(in CaveSecretPocketProjection projection)
        {
            CaveTraversalCandidate terminal = projection.Pocket.Terminal;
            int3 forward = FacingVector(terminal.ExitFacing);
            int3 eyeVoxel = terminal.Position - forward * 18 + new int3(0, 12, 0);
            DecorationBounds barrier = projection.Pocket.Barrier;
            float3 targetVoxel = ((float3)barrier.Min + (float3)barrier.MaxExclusive) * 0.5f;

            transform.position = (float3)eyeVoxel * VoxelMetres;
            Vector3 target = (Vector3)(targetVoxel * VoxelMetres);
            Vector3 direction = target - transform.position;
            if (direction.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        private static int3 FacingVector(Facing facing)
        {
            switch (facing)
            {
                case Facing.North: return new int3(0, 0, 1);
                case Facing.South: return new int3(0, 0, -1);
                case Facing.East: return new int3(1, 0, 0);
                case Facing.West: return new int3(-1, 0, 0);
                default: throw new ArgumentOutOfRangeException(nameof(facing));
            }
        }
    }
}
