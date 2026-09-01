using System;
using Game.Composition.CaveWorldBuilder;
using Game.Materials.Api;
using Game.Structures.Api;
using Game.Structures.Runtime;
using Game.WorldBuilder.Api;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;
using VoxelEngine.Terrain.Api;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Thin Worldbuilding Gallery consumer for the reusable WorldBuilder secret-discovery feature.
    /// The gallery contributes only deterministic placement/presentation policy: cave topology,
    /// secret-pocket authoring, clue semantics and voxel rendering remain owned by their production
    /// modules.
    /// </summary>
    public sealed partial class ShowcaseWorld
    {
        private bool _gallerySecretDiscoveryReady;
        private CaveSecretPocketProjection _gallerySecretPocket;
        private int _gallerySecretBoundaryClueVoxels;
        private int _galleryNaturalApproachClueVoxels;

        public bool HasWorldbuildingGallerySecretDiscoveryContent => _gallerySecretDiscoveryReady;
        public CaveSecretPocketProjection WorldbuildingGallerySecretPocket => _gallerySecretPocket;
        public int WorldbuildingGallerySecretBoundaryClueVoxels => _gallerySecretBoundaryClueVoxels;
        public int WorldbuildingGalleryNaturalApproachClueVoxels => _galleryNaturalApproachClueVoxels;

        /// <summary>
        /// Adds the final acceptance secret to the existing generated gallery cave. Gallery bakes
        /// intentionally persist voxels rather than traversal-candidate metadata, so this bounded
        /// compatibility pass deterministically replays only the cave authoring operation to recover
        /// the production terminal set. It verifies the replay reaches the baked main-path endpoint
        /// before any secret-specific mutation is accepted.
        /// </summary>
        public void EnsureWorldbuildingGallerySecretDiscoveryBlocking()
        {
            if (_gallerySecretDiscoveryReady) return;
            if (!HasGalleryContent)
                throw new InvalidOperationException(
                    "Worldbuilding Gallery content must exist before secret discovery is composed.");

            PreloadGalleryRegions();
            IStructureAuthoringSession authoring = CreateStructureAuthoringSession(4_000_000);
            CaveAuthoringResult cave = AuthorGalleryCave(authoring);
            if (!math.all(cave.MainPathEnd == GalleryCavePathEnd))
                throw new InvalidOperationException(
                    $"Gallery cave replay diverged from baked metadata: expected={GalleryCavePathEnd} actual={cave.MainPathEnd}.");
            if (cave.TraversalCandidates.Count == 0)
                throw new InvalidOperationException("Gallery cave exposes no reachable secret-placement terminal.");

            CampaignBuilder campaign = Campaign.Create("worldbuilding-gallery-secret-discovery");
            RegionHandle region = campaign.World.Region("gallery-cave");
            SiteRef hidden = region.Site(
                "moss-pocket",
                SiteArchetype.Ruin,
                x => x.RequireCapability(SiteCapability.SecretCandidateHost));

            CavePlacementRequirements requirements = CavePlacementRequirements.AnyReachableTerminal();
            CavePlacementPreferences preferences = CavePlacementPreferences.PreferBranchTerminal;
            var pocketConfig = new CaveSecretPocketConfig
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
                    out _gallerySecretPocket,
                    out CaveSecretPocketCompositionFailure failure))
                throw new InvalidOperationException("Gallery cave secret authoring failed: " + failure);

            var clueConfig = new CaveSecretPocketCluePresentationConfig(
                Coatings.Moss,
                46,
                Seed ^ 0x53454352u);
            if (!CaveSecretPocketCluePresentation.TryApplyBoundaryEvidence(
                    authoring,
                    in _gallerySecretPocket,
                    in clueConfig,
                    out _gallerySecretBoundaryClueVoxels))
                throw new InvalidOperationException("Gallery secret boundary clue presentation failed.");

            int3 entrance = Grounded(s_GalleryExhibitXZ[6]);
            _galleryNaturalApproachClueVoxels = CoatNaturalApproachEvidence(authoring, entrance);
            if (_galleryNaturalApproachClueVoxels <= 0)
                throw new InvalidOperationException("Gallery cave approach produced no environmental clue evidence.");

            _gallerySecretDiscoveryReady = true;
        }

        /// <summary>
        /// Applies a sparse, deterministic moss trail to existing terrain on the approach to the
        /// generated cave. It changes coating only and rechecks occupancy, so the natural-route clue
        /// cannot create a path, wall, marker mesh or bypass.
        /// </summary>
        private int CoatNaturalApproachEvidence(IStructureAuthoringSession authoring, int3 entrance)
        {
            int coated = 0;
            for (int step = 0; step < 14; step++)
            {
                int z = entrance.z - 12 - step * 4;
                uint hash = GallerySecretHash(Seed ^ 0x4D4F5353u, step);
                int centreX = entrance.x + (int)(hash % 9u) - 4;
                int halfWidth = 2 + (int)((hash >> 8) % 3u);

                for (int x = centreX - halfWidth; x <= centreX + halfWidth; x++)
                {
                    int y = TerrainQuery.HeightAt(x, z, Seed);
                    if (!authoring.IsSolid(x, y, z)) continue;
                    authoring.Coat(x, y, z, Coatings.Moss);
                    if (!authoring.IsSolid(x, y, z))
                        throw new InvalidOperationException(
                            $"Environmental clue coating changed terrain occupancy at ({x},{y},{z}).");
                    coated++;
                }
            }
            return coated;
        }

        public float3 WorldbuildingGalleryNaturalSecretCameraPosition()
        {
            int3 entrance = Grounded(s_GalleryExhibitXZ[6]);
            int eyeZ = entrance.z - 72;
            int eyeY = TerrainQuery.HeightAt(entrance.x, eyeZ, Seed) + 18;
            return new float3(entrance.x, eyeY, eyeZ) * VoxelSize;
        }

        public float3 WorldbuildingGalleryNaturalSecretLookTarget()
        {
            int3 entrance = Grounded(s_GalleryExhibitXZ[6]);
            return new float3(entrance.x, entrance.y + 10, entrance.z + 8) * VoxelSize;
        }

        public float3 WorldbuildingGalleryBreakableSecretCameraPosition()
        {
            RequireGallerySecretDiscovery();
            CaveTraversalCandidate terminal = _gallerySecretPocket.Pocket.Terminal;
            int3 forward = GallerySecretFacingVector(terminal.ExitFacing);
            int3 eye = terminal.Position - forward * 17 + new int3(0, 11, 0);
            return (float3)eye * VoxelSize;
        }

        public float3 WorldbuildingGalleryBreakableSecretLookTarget()
        {
            RequireGallerySecretDiscovery();
            DecorationBounds barrier = _gallerySecretPocket.Pocket.Barrier;
            return (((float3)barrier.Min + (float3)barrier.MaxExclusive) * 0.5f) * VoxelSize;
        }

        private void RequireGallerySecretDiscovery()
        {
            if (!_gallerySecretDiscoveryReady)
                throw new InvalidOperationException("Gallery secret-discovery content has not been composed.");
        }

        private static int3 GallerySecretFacingVector(Facing facing)
        {
            return facing switch
            {
                Facing.North => new int3(0, 0, 1),
                Facing.South => new int3(0, 0, -1),
                Facing.East => new int3(1, 0, 0),
                Facing.West => new int3(-1, 0, 0),
                _ => throw new ArgumentOutOfRangeException(nameof(facing)),
            };
        }

        private static uint GallerySecretHash(uint seed, int value)
        {
            uint h = seed ^ unchecked((uint)value * 0x9E3779B9u + 0x85EBCA6Bu);
            h ^= h >> 16;
            h *= 0x7FEB352Du;
            h ^= h >> 15;
            h *= 0x846CA68Bu;
            return h ^ (h >> 16);
        }
    }
}
