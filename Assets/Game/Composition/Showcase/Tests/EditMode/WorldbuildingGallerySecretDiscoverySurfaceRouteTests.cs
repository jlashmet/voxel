using System.Collections.Generic;
using Game.Materials.Api;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;
using VoxelEngine.Terrain.Api;

namespace VoxelEngine.Tests.EditMode
{
    /// <summary>
    /// Behavioral regression for the Gallery's natural discovery route. The route must begin at the
    /// visible terrain surface and descend through the production cave authorer; an underground cave
    /// with unrelated surface coating is not a discoverable route.
    /// </summary>
    public sealed class WorldbuildingGallerySecretDiscoverySurfaceRouteTests
    {
        private const uint GallerySeed = 0x5EED1234u;
        private const int SecretCaveX = -1340;
        private const int SecretCaveZ = 220;

        [Test]
        public void SurfaceConnectedGalleryCaveCarvesDescendingRouteFromVisibleMouth()
        {
            int surfaceY = TerrainQuery.HeightAt(SecretCaveX, SecretCaveZ, GallerySeed);
            int3 entrance = new int3(SecretCaveX, surfaceY + 1, SecretCaveZ);
            var world = new RecordingSolidSession();

            CaveConfig caveConfig = GalleryCaveConfig();
            CaveGenerationRequest request = CaveGenerationRequest.Standalone(
                0x5742475345435245ul,
                GallerySeed,
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

            CaveAuthoringResult cave = CaveAuthoring.Author(world, in request, in caveConfig, in palette);

            int firstSegmentEndZ = entrance.z + request.Entrance.ClearanceLength + caveConfig.SegmentLength;
            int firstSegmentFloorY = entrance.y - caveConfig.SurfaceDescentPerSegment;
            int3 firstDescendingFloor = new int3(entrance.x, firstSegmentFloorY, firstSegmentEndZ);

            Assert.Multiple(() =>
            {
                Assert.That(request.Entrance.Mode, Is.EqualTo(CaveEntranceMode.Surface));
                Assert.That(world.IsSolid(firstDescendingFloor.x, firstDescendingFloor.y, firstDescendingFloor.z), Is.False,
                    "The first production cave segment must physically descend from the surface mouth.");
                Assert.That(cave.MainPathTraversalDistance, Is.GreaterThanOrEqualTo(
                    caveConfig.SurfaceDescentSegments * caveConfig.SegmentLength));
                Assert.That(cave.MainPathEnd.y, Is.LessThanOrEqualTo(
                    entrance.y - caveConfig.SurfaceDescentSegments * caveConfig.SurfaceDescentPerSegment),
                    "The covered cave network must be reached by the authored descent, not by an unrelated underground entrance.");
                Assert.That(cave.TraversalCandidates.Count, Is.GreaterThanOrEqualTo(2));
            });
        }

        private static CaveConfig GalleryCaveConfig()
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
            return caveConfig;
        }

        private sealed class RecordingSolidSession : IStructureAuthoringSession
        {
            private readonly HashSet<int3> _empty = new HashSet<int3>();

            public bool BudgetExceeded => false;
            public int WriteBudget => int.MaxValue;
            public long TotalVoxelsWritten => _empty.Count;
            public byte Get(int x, int y, int z) => IsSolid(x, y, z) ? (byte)2 : GameMaterialIds.Empty;
            public byte GetCoating(int x, int y, int z) => Coatings.None;
            public bool IsSolid(int x, int y, int z) => !_empty.Contains(new int3(x, y, z));

            public void Set(int x, int y, int z, byte material)
            {
                int3 p = new int3(x, y, z);
                if (material == GameMaterialIds.Empty) _empty.Add(p);
                else _empty.Remove(p);
            }

            public void SetStyled(int x, int y, int z, byte material, ushort surfaceStyle,
                byte coating = Coatings.None, VoxelSurfaceFlags flags = VoxelSurfaceFlags.None) => Set(x, y, z, material);
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
                for (int y = minY; y < maxYExclusive; y++) Set(x, y, z, material);
            }
            public void Box(int3 min, int3 size, byte material) => FillBulk(min, size, material);
            public void HollowBox(int3 min, int3 size, int thickness, byte material, bool floor, bool ceiling) => FillBulk(min, size, material);
            public void Cylinder(int cx, int baseY, int cz, int radius, int height, byte material, int innerRadius = 0) { }
            public void Disc(int cx, int y, int cz, int radius, byte material) { }
            public void Cone(int cx, int baseY, int cz, int radius, int height, byte material) { }
            public void HangingCone(int cx, int ceilingY, int cz, int radius, int height, byte material) { }
            public void Gable(int3 min, int3 size, bool alongX, byte material) { }
            public void Crenellate(int3 start, int3 step, int count, int width, int height, int merlon, int gap, byte material) { }
            public void CrenellateRing(int cx, int y, int cz, int radius, int height, byte material) { }
            public void Arch(int3 min, int width, int height, int depth, int depthAxis, byte material) { }
            public void Stairs(int3 min, int width, int steps, int rise, int run, int axis, byte material) { }
            public void SpiralStair(int cx, int baseY, int cz, int radius, int height, byte material) { }
            public void Carve(int3 min, int3 size) => FillBulk(min, size, GameMaterialIds.Empty);
            public void Weather(int3 min, int3 size, byte coating, uint seed, int chanceOutOf100) { }
        }
    }
}
