using System;
using System.Collections.Generic;
using Game.Composition.CaveWorldBuilder;
using Game.Materials.Api;
using Game.Structures.Api;
using Game.Structures.Runtime;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Composition;
using VoxelEngine.Showcase;
using VoxelEngine.Storage.Api;
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
    /// clue realization, materials, voxel meshing, destruction, and tree rendering all execute through
    /// production paths.
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
        private CaveAuthoringResult _cave;
        private CaveSecretPocketProjection _projection;
        private int3 _caveEntrance;
        private float _sequenceStart;
        private bool _wallDestroyed;
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
            int3 caveEntrance = new int3(CaveAnchorX, surfaceY + 1, CaveAnchorZ);
            PreloadAround(caveEntrance);

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
                0x5345435243415645ul,
                m_Seed,
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

            CampaignBuilder campaign = Campaign.Create("worldbuilder-secret-discovery-validation");
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

            var route = new SecretRouteId("generated-cave/breakable-boundary");
            SecretClueAnchorSpec[] clueAnchors = CaveSecretPocketClueAnchors.ForAuthoredBreakable(
                approach,
                route);
            if (clueAnchors.Length < 2)
                throw new InvalidOperationException("Generated cave secret did not expose reusable clue anchors.");

            var breakableContext = new SecretClueLocalContext(
                vegetationDensityPercent: 5,
                surfaceUniformityPercent: 92,
                structuralRegularityPercent: 88,
                occlusionPercent: 30,
                recentDisturbancePercent: 5);
            SecretClueAnomalyPlan breakableAnomaly = SecretClueAnomalyPlanner.Resolve(
                unchecked((int)m_Seed),
                route.Id + "/barrier-surface",
                SecretRouteKind.BreakableBarrier,
                SecretClueChannel.Visual,
                in breakableContext);
            if (breakableAnomaly.Motif != SecretClueMotifFamily.StructuralFracture)
                throw new InvalidOperationException(
                    "Local breakable-wall context no longer selects structural-fracture evidence; " +
                    "update the realizer rather than silently presenting an incompatible motif.");

            var clueConfig = new CaveSecretPocketCluePresentationConfig(
                Coatings.Soot,
                breakableAnomaly.StrengthPercent,
                m_Seed ^ 0x434C5545u);
            if (!CaveSecretPocketCluePresentation.TryApplyBoundaryEvidence(
                    authoring,
                    in projection,
                    in clueConfig,
                    out int coatedVoxels))
                throw new InvalidOperationException("Production cave clue presentation failed.");

            var naturalContext = new SecretClueLocalContext(
                vegetationDensityPercent: 92,
                surfaceUniformityPercent: 58,
                structuralRegularityPercent: 10,
                occlusionPercent: 72,
                recentDisturbancePercent: 8);
            SecretClueAnomalyPlan naturalAnomaly = SecretClueAnomalyPlanner.Resolve(
                unchecked((int)m_Seed),
                "generated-cave/natural-approach",
                SecretRouteKind.NaturalTraversal,
                SecretClueChannel.Environmental,
                in naturalContext,
                new[] { breakableAnomaly.Motif });

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

            PublishProductionTrees(surfaceY, in naturalAnomaly);

            _cave = cave;
            _projection = projection;
            _caveEntrance = caveEntrance;
            _wallDestroyed = false;
            _sequenceStart = Time.time;
            PlaceSequencePose(0f);

            _ready = true;
            Debug.Log(
                "WorldBuilder secret validation ready: " +
                $"caveSegments={cave.SegmentsAuthored} branches={cave.BranchesAuthored} " +
                $"terminals={cave.TraversalCandidates.Count} clueAnchors={clueAnchors.Length} " +
                $"breakableMotif={breakableAnomaly.Motif} breakableIntent={breakableAnomaly.ActionIntent} " +
                $"naturalMotif={naturalAnomaly.Motif} naturalIntent={naturalAnomaly.ActionIntent} " +
                $"crackVoxels={coatedVoxels} trees={_trees.Count} " +
                $"barrier={projection.Pocket.Barrier.Min}->{projection.Pocket.Barrier.MaxExclusive}");
        }

        private void Update()
        {
            if (!_ready || _world == null) return;

            float elapsed = Time.time - _sequenceStart;
            if (!_wallDestroyed && elapsed >= 16f)
                DestroySecretWall();

            PlaceSequencePose(elapsed);
            _world.StepStreaming(transform.position, m_GenerateBudgetMs);
        }

        private void OnDisable()
        {
            _ready = false;
            _wallDestroyed = false;
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

        private void PublishProductionTrees(int surfaceY, in SecretClueAnomalyPlan naturalAnomaly)
        {
            _trees.Clear();

            // Baseline vegetation establishes local normality around the feature.
            for (int i = 0; i < 24; i++)
            {
                float angle = i * (math.PI * 2f / 24f);
                float radius = 18f + (i % 5) * 2.5f;
                AddTree(
                    CaveAnchorX * VoxelMetres + math.cos(angle) * radius,
                    CaveAnchorZ * VoxelMetres + math.sin(angle) * radius,
                    i,
                    0.9f + (i % 4) * 0.08f);
            }

            // The chosen natural motif is a controlled local deviation, not a global secret marker.
            // In dense vegetation, two irregular banks with a narrow negative-space corridor produce
            // a readable "why is this path opening here?" anomaly toward the cave entrance.
            if (naturalAnomaly.Motif == SecretClueMotifFamily.VegetationDiscontinuity ||
                naturalAnomaly.Motif == SecretClueMotifFamily.SightlineGap)
            {
                int treeIndex = 100;
                for (int row = 0; row < 7; row++)
                {
                    float zMetres = CaveAnchorZ * VoxelMetres - 1.6f - row * 0.9f;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        for (int lane = 0; lane < 2; lane++)
                        {
                            uint h = AnomalyHash(m_Seed, row * 11 + lane * 3 + (side > 0 ? 1 : 0));
                            float jitter = ((h & 255u) / 255f - 0.5f) * 0.55f;
                            float xMetres = CaveAnchorX * VoxelMetres +
                                            side * (1.65f + lane * 1.35f + jitter);
                            AddTree(xMetres, zMetres + jitter * 0.35f, treeIndex++, 0.82f + (h % 5u) * 0.045f);
                        }
                    }
                }
            }

            VegetationComposition.ReplaceTreeWorld(_trees);
        }

        private void AddTree(float xMetres, float zMetres, int index, float scale)
        {
            int xVoxel = (int)math.round(xMetres / VoxelMetres);
            int zVoxel = (int)math.round(zMetres / VoxelMetres);
            int yVoxel = TerrainSampler.HeightAt(xVoxel, zVoxel, m_Seed);
            uint treeSeed = m_Seed ^ (uint)(index * 0x9E3779B9u + 1u);
            _trees.Add(new TreeInstance
            {
                PositionMetres = new float3(xMetres, yVoxel * VoxelMetres, zMetres),
                Species = (index & 1) == 0 ? TreeSpecies.Pine : TreeSpecies.Oak,
                Seed = treeSeed == 0u ? 1u : treeSeed,
                Scale = scale,
            });
        }

        private static uint AnomalyHash(uint seed, int value)
        {
            uint h = seed ^ unchecked((uint)value * 0x9E3779B9u + 0x85EBCA6Bu);
            h ^= h >> 16;
            h *= 0x7FEB352Du;
            h ^= h >> 15;
            h *= 0x846CA68Bu;
            return h ^ (h >> 16);
        }

        /// <summary>
        /// Drives a deterministic visual walkthrough of the production-authored cave. Each hold is
        /// longer than the player harness capture interval so evidence includes the natural approach,
        /// entrance, interior descent, clue-bearing false wall at two gameplay distances, breached
        /// wall, and hidden pocket beyond it.
        /// </summary>
        private void PlaceSequencePose(float elapsed)
        {
            CaveSecretPocket pocket = _projection.Pocket;
            int3 forward = FacingVector(pocket.Terminal.ExitFacing);
            float3 barrierTarget = BoundsCentre(pocket.Barrier);

            if (elapsed < 2.5f)
            {
                PlaceExteriorEntrance();
                return;
            }

            if (elapsed < 5.5f)
            {
                int3 eye = _caveEntrance + FacingVector(Facing.North) * 6 + new int3(0, 12, 0);
                float3 target = (float3)(_caveEntrance + FacingVector(Facing.North) * 14 + new int3(0, 10, 0));
                PlaceVoxelPose((float3)eye, target);
                return;
            }

            if (elapsed < 8.5f)
            {
                float3 eye = (float3)_cave.MainPathEnd + new float3(0f, 12f, 0f);
                float3 target = (float3)pocket.Terminal.Position + new float3(0f, 11f, 0f);
                if (math.lengthsq(target - eye) < 4f)
                    target = eye + (float3)FacingVector(pocket.Terminal.ExitFacing) * 12f;
                PlaceVoxelPose(eye, target);
                return;
            }

            if (elapsed < 11.5f)
            {
                int3 eye = pocket.Terminal.Position - forward * 12 + new int3(0, 12, 0);
                PlaceVoxelPose((float3)eye, barrierTarget);
                return;
            }

            if (elapsed < 16f)
            {
                int3 eye = pocket.Terminal.Position - forward * 6 + new int3(0, 12, 0);
                PlaceVoxelPose((float3)eye, barrierTarget);
                return;
            }

            if (elapsed < 19.5f)
            {
                int3 eye = pocket.Terminal.Position - forward * 6 + new int3(0, 12, 0);
                PlaceVoxelPose((float3)eye, BoundsCentre(pocket.Pocket));
                return;
            }

            float3 connectorEye = BoundsCentre(pocket.Connector);
            connectorEye.y = pocket.Connector.Min.y + 12f;
            float3 pocketTarget = BoundsCentre(pocket.Pocket);
            pocketTarget.y = pocket.Pocket.Min.y + 11f;
            PlaceVoxelPose(connectorEye, pocketTarget);
        }

        private void PlaceExteriorEntrance()
        {
            int eyeZ = _caveEntrance.z - 72;
            int eyeY = TerrainSampler.HeightAt(_caveEntrance.x, eyeZ, m_Seed) + 18;
            int surfaceY = TerrainSampler.HeightAt(_caveEntrance.x, _caveEntrance.z, m_Seed);

            transform.position = (float3)new int3(_caveEntrance.x, eyeY, eyeZ) * VoxelMetres;
            Vector3 target = (Vector3)(new float3(
                _caveEntrance.x,
                surfaceY + 2,
                _caveEntrance.z + 8) * VoxelMetres);
            LookAt(target);
        }

        private void PlaceVoxelPose(float3 eyeVoxels, float3 targetVoxels)
        {
            transform.position = (Vector3)(eyeVoxels * VoxelMetres);
            LookAt((Vector3)(targetVoxels * VoxelMetres));
        }

        private void LookAt(Vector3 target)
        {
            Vector3 direction = target - transform.position;
            if (direction.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        private void DestroySecretWall()
        {
            CaveSecretPocket pocket = _projection.Pocket;
            int3 barrierCentre = new int3(
                (pocket.Barrier.Min.x + pocket.Barrier.MaxExclusive.x - 1) / 2,
                (pocket.Barrier.Min.y + pocket.Barrier.MaxExclusive.y - 1) / 2,
                (pocket.Barrier.Min.z + pocket.Barrier.MaxExclusive.z - 1) / 2);
            int3 forward = FacingVector(pocket.Terminal.ExitFacing);
            int changed = _world.Explode(barrierCentre, 9, (float3)forward);
            if (changed <= 0)
                throw new InvalidOperationException("Production destruction failed to breach the authored secret wall.");

            _wallDestroyed = true;
            Debug.Log(
                "WorldBuilder secret validation wall destroyed: " +
                $"voxels={changed} centre={barrierCentre} hiddenPocket={pocket.Pocket.Min}->{pocket.Pocket.MaxExclusive}");
        }

        private static float3 BoundsCentre(in DecorationBounds bounds) =>
            ((float3)bounds.Min + (float3)bounds.MaxExclusive) * 0.5f;

        private static int3 FacingVector(Facing facing)
        {
            switch (facing)
            {
                case Facing.North: return new int3(0, 0, 1);
                case Facing.South: return new int3(0, 0, -1);
                case Facing.East: return new int3(1, 0, 0);
                case Facing.West: return new int3(-1, 0, 0);
                default: throw new ArgumentOutOfRangeException(nameof(facing), facing, null);
            }
        }
    }
}
