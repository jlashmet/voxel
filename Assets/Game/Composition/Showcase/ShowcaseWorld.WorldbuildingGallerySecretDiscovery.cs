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
        private const int GallerySecretCaveX = -1340;
        private const int GallerySecretCaveZ = 220;

        private bool _gallerySecretDiscoveryReady;
        private CaveSecretPocketProjection _gallerySecretPocket;
        private int3 _gallerySecretEntrance;
        private int _gallerySecretBoundaryClueVoxels;
        private int _galleryNaturalApproachClueVoxels;

        public bool HasWorldbuildingGallerySecretDiscoveryContent => _gallerySecretDiscoveryReady;
        public CaveSecretPocketProjection WorldbuildingGallerySecretPocket => _gallerySecretPocket;
        public int WorldbuildingGallerySecretBoundaryClueVoxels => _gallerySecretBoundaryClueVoxels;
        public int WorldbuildingGalleryNaturalApproachClueVoxels => _galleryNaturalApproachClueVoxels;

        /// <summary>
        /// Adds the final acceptance secret as a bounded generated cave beside the legacy Gallery cave.
        /// The legacy cave is intentionally retained unchanged: a fresh-world regression proves its
        /// current topology cannot physically host the requested pocket. Scene-specific composition
        /// therefore selects a nearby supported generated cave while reusing the same production cave,
        /// pocket, clue and discovery abstractions validated by the dedicated module scene.
        /// </summary>
        public void EnsureWorldbuildingGallerySecretDiscoveryBlocking()
        {
            if (_gallerySecretDiscoveryReady) return;
            if (!HasGalleryContent)
                throw new InvalidOperationException(
                    "Worldbuilding Gallery content must exist before secret discovery is composed.");

            PreloadGalleryRegions();
            IStructureAuthoringSession authoring = CreateStructureAuthoringSession(4_000_000);
            CaveAuthoringResult cave = AuthorWorldbuildingGallerySecretCave(authoring, out _gallerySecretEntrance);
            if (cave.TraversalCandidates.Count < 2)
                throw new InvalidOperationException(
                    "Gallery secret acceptance cave did not produce enough reachable terminals.");

            CampaignBuilder campaign = Campaign.Create("worldbuilding-gallery-secret-discovery");
            RegionHandle region = campaign.World.Region("gallery-secret-cave");
            SiteRef hidden = region.Site(
                "moss-pocket",
                SiteArchetype.Ruin,
                x => x.RequireCapability(SiteCapability.SecretCandidateHost));

            CavePlacementRequirements requirements = CavePlacementRequirements.AnyReachableTerminal(40);
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

            _galleryNaturalApproachClueVoxels = CoatNaturalApproachEvidence(authoring, _gallerySecretEntrance);
            if (_galleryNaturalApproachClueVoxels <= 0)
                throw new InvalidOperationException("Gallery cave approach produced no environmental clue evidence.");

            _gallerySecretDiscoveryReady = true;
        }

        private CaveAuthoringResult AuthorWorldbuildingGallerySecretCave(
            IStructureAuthoringSession authoring,
            out int3 entrance)
        {
            int surfaceY = TerrainQuery.HeightAt(GallerySecretCaveX, GallerySecretCaveZ, Seed);
            entrance = new int3(GallerySecretCaveX, surfaceY - 18, GallerySecretCaveZ);

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
                0x5742475345435245ul,
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

            return CaveAuthoring.Author(authoring, in request, in caveConfig, in palette);
        }

        /// <summary>
        /// A gallery bake stores the authored chamber endpoint so it can restore presentation without
        /// regenerating the cave. This compatibility predicate remains as regression coverage for that
        /// legacy metadata path even though the final secret acceptance consumer uses its own supported
        /// generated cave.
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
            RequireGallerySecretDiscovery();
            int eyeZ = _gallerySecretEntrance.z - 72;
            int eyeY = TerrainQuery.HeightAt(_gallerySecretEntrance.x, eyeZ, Seed) + 18;
            return new float3(_gallerySecretEntrance.x, eyeY, eyeZ) * VoxelSize;
        }

        public float3 WorldbuildingGalleryNaturalSecretLookTarget()
        {
            RequireGallerySecretDiscovery();
            return new float3(
                _gallerySecretEntrance.x,
                _gallerySecretEntrance.y + 10,
                _gallerySecretEntrance.z + 8) * VoxelSize;
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
        /// Compatibility replay adapter retained for regression coverage of legacy Gallery bake
        /// metadata recovery. It cannot mutate authoritative storage.
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
