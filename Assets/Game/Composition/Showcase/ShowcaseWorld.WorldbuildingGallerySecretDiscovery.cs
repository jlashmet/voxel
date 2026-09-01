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
        /// the production terminal set. The replay runs through a read-through/write-discard authoring
        /// session: it may observe the authoritative baked world but cannot alter it while recovering
        /// metadata. Route compatibility is verified before any secret-specific mutation is accepted.
        /// </summary>
        public void EnsureWorldbuildingGallerySecretDiscoveryBlocking()
        {
            if (_gallerySecretDiscoveryReady) return;
            if (!HasGalleryContent)
                throw new InvalidOperationException(
                    "Worldbuilding Gallery content must exist before secret discovery is composed.");

            PreloadGalleryRegions();
            IStructureAuthoringSession authoring = CreateStructureAuthoringSession(4_000_000);
            IStructureAuthoringSession replayAuthoring = new WorldbuildingGalleryCaveReplaySession(authoring);
            CaveAuthoringResult cave = AuthorGalleryCave(replayAuthoring);
            if (!IsWorldbuildingGalleryCaveReplayCompatible(GalleryCavePathEnd, in cave))
                throw new InvalidOperationException(
                    $"Gallery cave replay diverged from baked route semantics: expected={GalleryCavePathEnd} actual={cave.MainPathEnd}.");
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
        /// A gallery bake stores the authored chamber endpoint so it can restore presentation without
        /// regenerating the cave. The replay exists only to recover traversal terminals for newer
        /// composition. Horizontal endpoint and main-path traversal semantics identify the route;
        /// replay Y is intentionally allowed to differ because vertical cave placement is derived from
        /// the current surface-cover rules and can change across authoring revisions.
        /// </summary>
        public static bool IsWorldbuildingGalleryCaveReplayCompatible(
            int3 bakedMainPathEnd,
            in CaveAuthoringResult replay)
        {
            if (replay.MainPathEnd.x != bakedMainPathEnd.x || replay.MainPathEnd.z != bakedMainPathEnd.z)
                return false;
            if (replay.MainPathTraversalDistance <= 0)
                return false;

            CaveTraversalFlags required = CaveTraversalFlags.ReachableFromEntrance |
                                          CaveTraversalFlags.MainPath |
                                          CaveTraversalFlags.Terminal;
            for (int i = 0; i < replay.TraversalCandidates.Items.Length; i++)
            {
                CaveTraversalCandidate candidate = replay.TraversalCandidates.Items[i];
                if (!candidate.IsWellFormed) continue;
                if ((candidate.Flags & required) != required) continue;
                if (!math.all(candidate.Position == replay.MainPathEnd)) continue;
                if (candidate.TraversalDistance != replay.MainPathTraversalDistance) continue;
                return true;
            }

            return false;
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

        /// <summary>
        /// Compatibility replay adapter for the checked-in Gallery bake. Cave generation is reused
        /// to recover deterministic traversal metadata, but all geometry writes are deliberately
        /// discarded so replay cannot carve a second, vertically-shifted cave into authoritative
        /// baked storage before secret-pocket physical preflight.
        /// </summary>
        private sealed class WorldbuildingGalleryCaveReplaySession : IStructureAuthoringSession
        {
            private readonly IStructureAuthoringSession _source;

            public WorldbuildingGalleryCaveReplaySession(IStructureAuthoringSession source)
            {
                _source = source ?? throw new ArgumentNullException(nameof(source));
            }

            public bool BudgetExceeded => false;
            public int WriteBudget => int.MaxValue;
            public long TotalVoxelsWritten => 0;

            public byte Get(int x, int y, int z) => _source.Get(x, y, z);
            public byte GetCoating(int x, int y, int z) => _source.GetCoating(x, y, z);
            public bool IsSolid(int x, int y, int z) => _source.IsSolid(x, y, z);

            public void Set(int x, int y, int z, byte material) { }
            public void SetStyled(int x, int y, int z, byte material, ushort surfaceStyle,
                byte coating = Coatings.None, VoxelSurfaceFlags flags = VoxelSurfaceFlags.None) { }
            public void Coat(int x, int y, int z, byte coating) { }
            public void FillBulk(int3 min, int3 size, byte material) { }
            public void FillColumnBulk(int x, int minY, int maxYExclusive, int z, byte material) { }
            public void Box(int3 min, int3 size, byte material) { }
            public void HollowBox(int3 min, int3 size, int thickness, byte material, bool floor, bool ceiling) { }
            public void Cylinder(int cx, int baseY, int cz, int radius, int height, byte material,
                int innerRadius = 0) { }
            public void Disc(int cx, int y, int cz, int radius, byte material) { }
            public void Cone(int cx, int baseY, int cz, int radius, int height, byte material) { }
            public void HangingCone(int cx, int ceilingY, int cz, int radius, int height, byte material) { }
            public void Gable(int3 min, int3 size, bool alongX, byte material) { }
            public void Crenellate(int3 start, int3 step, int count, int width, int height,
                int merlon, int gap, byte material) { }
            public void CrenellateRing(int cx, int y, int cz, int radius, int height, byte material) { }
            public void Arch(int3 min, int width, int height, int depth, int depthAxis, byte material) { }
            public void Stairs(int3 min, int width, int steps, int rise, int run, int axis, byte material) { }
            public void SpiralStair(int cx, int baseY, int cz, int radius, int height, byte material) { }
            public void Carve(int3 min, int3 size) { }
            public void Weather(int3 min, int3 size, byte coating, uint seed, int chanceOutOf100) { }
        }
    }
}
