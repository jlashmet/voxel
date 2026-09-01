using System.Collections.Generic;
using Game.Composition.CaveWorldBuilder;
using Game.Materials.Api;
using Game.Structures.Api;
using Game.Structures.Runtime;
using Game.WorldBuilder.Api;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;
using VoxelEngine.Terrain.Api;

namespace VoxelEngine.Tests.EditMode
{
    /// <summary>
    /// Physical-policy regression for Gallery secret discovery. The legacy Gallery cave is kept as
    /// evidence of an intrinsic placement conflict. The supported acceptance cave is tested against
    /// terrain occupancy rather than an infinite-solid fixture so insufficient overburden cannot be
    /// mistaken for a valid pocket host.
    /// </summary>
    public sealed class WorldbuildingGallerySecretDiscoveryPhysicalDiscriminatorTests
    {
        private const uint GallerySeed = 0x5EED1234u;
        private const int SecretCaveX = -1340;
        private const int SecretCaveZ = 220;

        [Test]
        public void LegacyGalleryCaveCannotHostRequestedPocketInFreshSolidWorld()
        {
            var world = new SolidVoxelSession();
            int y = TerrainQuery.HeightAt(-1120, 220, GallerySeed) + 1;
            int3 entrance = new int3(-1120, y, 220);

            CaveConfig caveConfig = CaveConfig.Default;
            caveConfig.MainSegmentCount = 14;
            caveConfig.MaxBranches = 4;
            caveConfig.MaxBranchDepth = 2;
            caveConfig.BranchSegmentCount = 5;
            caveConfig.ChamberChancePercent = 42;
            caveConfig.BoundsHalfExtents = new int3(240, 112, 240);

            CaveGenerationRequest request = CaveGenerationRequest.Standalone(
                0x5742474341564501ul,
                GallerySeed,
                entrance,
                Facing.North,
                24,
                26,
                8);
            CaveMaterialPalette palette = new CaveMaterialPalette
            {
                Opening = GameMaterialIds.Empty,
                Rock = GameMaterialIds.DarkStone,
                Accent = GameMaterialIds.Crystal,
                Decoration = GameMaterialIds.Moss,
                Water = GameMaterialIds.Water,
            };

            CaveAuthoringResult cave = CaveAuthoring.Author(world, in request, in caveConfig, in palette);
            bool authored = TryAuthorPocket(world, in cave, out _, out CaveSecretPocketCompositionFailure failure);

            Assert.Multiple(() =>
            {
                Assert.That(cave.TraversalCandidates.Count, Is.EqualTo(5),
                    "The legacy cave topology changed; re-evaluate this discriminator before changing Gallery policy.");
                Assert.That(authored, Is.False);
                Assert.That(failure, Is.EqualTo(CaveSecretPocketCompositionFailure.PhysicalConflict));
            });
        }

        [Test]
        public void SupportedGalleryAcceptanceCaveRequiresEnoughTerrainCoverForPocket()
        {
            int surfaceY = TerrainQuery.HeightAt(SecretCaveX, SecretCaveZ, GallerySeed);

            var shallowWorld = new TerrainVoxelSession(GallerySeed);
            CaveAuthoringResult shallow = AuthorSupportedCave(
                shallowWorld,
                new int3(SecretCaveX, surfaceY - 18, SecretCaveZ));
            bool shallowAuthored = TryAuthorPocket(
                shallowWorld,
                in shallow,
                out _,
                out CaveSecretPocketCompositionFailure shallowFailure);

            var coveredWorld = new TerrainVoxelSession(GallerySeed);
            CaveAuthoringResult covered = AuthorSupportedCave(
                coveredWorld,
                new int3(SecretCaveX, surfaceY - 48, SecretCaveZ));
            bool coveredAuthored = TryAuthorPocket(
                coveredWorld,
                in covered,
                out CaveSecretPocketProjection projection,
                out CaveSecretPocketCompositionFailure coveredFailure);

            Assert.Multiple(() =>
            {
                Assert.That(shallowAuthored, Is.False,
                    "The old 18-voxel cover unexpectedly became valid; re-evaluate the Gallery depth policy.");
                Assert.That(shallowFailure, Is.EqualTo(CaveSecretPocketCompositionFailure.PhysicalConflict));
                Assert.That(covered.TraversalCandidates.Count, Is.GreaterThanOrEqualTo(2));
                Assert.That(coveredAuthored, Is.True,
                    $"48-voxel Gallery cave cover failed with {coveredFailure}.");
                Assert.That(coveredFailure, Is.EqualTo(CaveSecretPocketCompositionFailure.None));
                Assert.That(projection.IsWellFormed, Is.True);
            });
        }

        [Test]
        public void ExactSurfaceCaveAcceptanceEyeMustOccupyAuthoredEmptyVoxel()
        {
            int surfaceY = TerrainQuery.HeightAt(SecretCaveX, SecretCaveZ, GallerySeed);
            int3 entrance = new int3(SecretCaveX, surfaceY + 1, SecretCaveZ);
            var world = new TerrainVoxelSession(GallerySeed);
            CaveAuthoringResult cave = AuthorRuntimeSurfaceCave(world, entrance);

            bool authored = TryAuthorPocket(
                world,
                in cave,
                out CaveSecretPocketProjection projection,
                out CaveSecretPocketCompositionFailure failure);
            Assert.That(authored, Is.True, $"Exact runtime cave pocket failed with {failure}.");

            CaveTraversalCandidate terminal = projection.Pocket.Terminal;
            int3 forward = FacingVector(terminal.ExitFacing);
            float3 helperEye = (float3)(terminal.Position - forward * 17 + new int3(0, 11, 0));
            float3 barrierTarget = ((float3)projection.Pocket.Barrier.Min +
                                    (float3)projection.Pocket.Barrier.MaxExclusive) * 0.5f;
            float3 acceptanceEye = math.lerp(helperEye, barrierTarget, 0.35f);
            int3 eyeVoxel = (int3)math.round(acceptanceEye);

            Assert.That(
                world.IsSolid(eyeVoxel.x, eyeVoxel.y, eyeVoxel.z),
                Is.False,
                $"The exact Gallery acceptance eye must be inside production-authored cave air. " +
                $"eye={acceptanceEye} rounded={eyeVoxel} terminal={terminal.Position} " +
                $"exit={terminal.ExitFacing} barrier={projection.Pocket.Barrier.Min}->{projection.Pocket.Barrier.MaxExclusive}.");
        }

        private static CaveAuthoringResult AuthorSupportedCave(
            IStructureAuthoringSession world,
            int3 entrance)
        {
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
            CaveMaterialPalette palette = GalleryPalette();
            return CaveAuthoring.Author(world, in request, in caveConfig, in palette);
        }

        private static CaveAuthoringResult AuthorRuntimeSurfaceCave(
            IStructureAuthoringSession world,
            int3 entrance)
        {
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
                0x5742475345435245ul,
                GallerySeed,
                entrance,
                Facing.North,
                caveConfig.TunnelWidth,
                caveConfig.TunnelHeight,
                10);
            CaveMaterialPalette palette = GalleryPalette();
            return CaveAuthoring.Author(world, in request, in caveConfig, in palette);
        }

        private static CaveMaterialPalette GalleryPalette()
        {
            return new CaveMaterialPalette
            {
                Opening = GameMaterialIds.Empty,
                Rock = GameMaterialIds.DarkStone,
                Accent = GameMaterialIds.MasonryMedium,
                Decoration = GameMaterialIds.Moss,
                Water = GameMaterialIds.Water,
            };
        }

        private static int3 FacingVector(Facing facing)
        {
            return facing switch
            {
                Facing.North => new int3(0, 0, 1),
                Facing.South => new int3(0, 0, -1),
                Facing.East => new int3(1, 0, 0),
                Facing.West => new int3(-1, 0, 0),
                _ => int3.zero,
            };
        }

        private static bool TryAuthorPocket(
            IStructureAuthoringSession world,
            in CaveAuthoringResult cave,
            out CaveSecretPocketProjection projection,
            out CaveSecretPocketCompositionFailure failure)
        {
            var campaign = Campaign.Create("gallery-secret-physical-discriminator");
            SiteRef hidden = campaign.World.Region("gallery-secret-cave").Site(
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

            return CaveSecretPocketComposition.TryAuthorBest(
                world,
                in cave.TraversalCandidates,
                in requirements,
                in preferences,
                hidden,
                9500,
                in pocketConfig,
                out projection,
                out failure);
        }

        private class SolidVoxelSession : IStructureAuthoringSession
        {
            protected readonly HashSet<int3> Empty = new HashSet<int3>();
            protected readonly HashSet<int3> SolidOverrides = new HashSet<int3>();

            public bool BudgetExceeded => false;
            public int WriteBudget => int.MaxValue;
            public long TotalVoxelsWritten => Empty.Count + SolidOverrides.Count;

            public byte Get(int x, int y, int z) => IsSolid(x, y, z) ? (byte)2 : (byte)0;
            public byte GetCoating(int x, int y, int z) => Coatings.None;
            public virtual bool IsSolid(int x, int y, int z) => !Empty.Contains(new int3(x, y, z));

            public void Set(int x, int y, int z, byte material)
            {
                int3 p = new int3(x, y, z);
                if (material == GameMaterialIds.Empty)
                {
                    Empty.Add(p);
                    SolidOverrides.Remove(p);
                }
                else
                {
                    Empty.Remove(p);
                    SolidOverrides.Add(p);
                }
            }

            public void SetStyled(int x, int y, int z, byte material, ushort surfaceStyle,
                byte coating = Coatings.None, VoxelSurfaceFlags flags = VoxelSurfaceFlags.None) =>
                Set(x, y, z, material);
            public void Coat(int x, int y, int z, byte coating) { }

            public void FillBulk(int3 min, int3 size, byte material)
            {
                for (int y = min.y; y < min.y + size.y; y++)
                for (int z = min.z; z < min.z + size.z; z++)
                for (int x = min.x; x < min.x + size.x; x++)
                    Set(x, y, z, material);
            }

            public void FillColumnBulk(int x, int minY, int maxYExclusive, int z, byte material)
            {
                for (int y = minY; y < maxYExclusive; y++)
                    Set(x, y, z, material);
            }

            public void Box(int3 min, int3 size, byte material) => FillBulk(min, size, material);
            public void HollowBox(int3 min, int3 size, int thickness, byte material, bool floor, bool ceiling) =>
                FillBulk(min, size, material);

            public void Cylinder(int cx, int baseY, int cz, int radius, int height, byte material,
                int innerRadius = 0)
            {
                int outer2 = radius * radius;
                int inner2 = innerRadius * innerRadius;
                for (int y = baseY; y < baseY + height; y++)
                for (int z = -radius; z <= radius; z++)
                for (int x = -radius; x <= radius; x++)
                {
                    int d2 = x * x + z * z;
                    if (d2 > outer2 || (innerRadius > 0 && d2 < inner2)) continue;
                    Set(cx + x, y, cz + z, material);
                }
            }

            public void Disc(int cx, int y, int cz, int radius, byte material)
            {
                int r2 = radius * radius;
                for (int z = -radius; z <= radius; z++)
                for (int x = -radius; x <= radius; x++)
                    if (x * x + z * z <= r2)
                        Set(cx + x, y, cz + z, material);
            }

            public void Cone(int cx, int baseY, int cz, int radius, int height, byte material) { }
            public void HangingCone(int cx, int ceilingY, int cz, int radius, int height, byte material) { }
            public void Gable(int3 min, int3 size, bool alongX, byte material) { }
            public void Crenellate(int3 start, int3 step, int count, int width, int height,
                int merlon, int gap, byte material) { }
            public void CrenellateRing(int cx, int y, int cz, int radius, int height, byte material) { }
            public void Arch(int3 min, int width, int height, int depth, int depthAxis, byte material) { }
            public void Stairs(int3 min, int width, int steps, int rise, int run, int axis, byte material) { }
            public void SpiralStair(int cx, int baseY, int cz, int radius, int height, byte material) { }
            public void Carve(int3 min, int3 size) => FillBulk(min, size, GameMaterialIds.Empty);
            public void Weather(int3 min, int3 size, byte coating, uint seed, int chanceOutOf100) { }
        }

        private sealed class TerrainVoxelSession : SolidVoxelSession
        {
            private readonly uint _seed;

            public TerrainVoxelSession(uint seed)
            {
                _seed = seed;
            }

            public override bool IsSolid(int x, int y, int z)
            {
                int3 p = new int3(x, y, z);
                if (Empty.Contains(p)) return false;
                if (SolidOverrides.Contains(p)) return true;
                return y <= TerrainQuery.HeightAt(x, z, _seed);
            }
        }
    }
}
