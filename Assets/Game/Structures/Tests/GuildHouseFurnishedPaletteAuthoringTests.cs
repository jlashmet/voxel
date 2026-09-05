using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Tests
{
    public sealed class GuildHouseFurnishedPaletteAuthoringTests
    {
        [Test]
        public void ExplicitPaletteAuthorsThroughProductionHouseAndDecorationEmitters()
        {
            GuildHousePrototype prototype = GuildHousePrototypeComposition.Build(
                GuildHouseKind.Wizards,
                DecorationRegionTheme.Kentridge,
                0x1234ABCDu,
                712u,
                int3.zero,
                128,
                128,
                requestedRooms: 16);
            Assert.That(
                GuildHouseFurnishingPalette.TryCreate(
                    GuildHouseKind.Wizards,
                    new ushort[] { 127, 233, 400 },
                    out GuildHouseFurnishingPalette palette),
                Is.True);

            var authoring = new RecordingAuthoringSession();
            Assert.That(
                GuildHouseFurnishedPrototypeAuthoring.TryAuthor(
                    authoring,
                    in prototype,
                    in palette,
                    out GuildHouseUnplacedFurnishing[] unplaced),
                Is.True);
            Assert.That(authoring.OperationCount, Is.GreaterThan(0));
            Assert.That(authoring.BoxCount, Is.GreaterThan(0), "production shell/prop emitters should author boxes");
            for (int i = 0; i < unplaced.Length; i++)
                Assert.That(unplaced[i].IsWellFormed, Is.True);
        }

        private sealed class RecordingAuthoringSession : IStructureAuthoringSession
        {
            public bool BudgetExceeded => false;
            public int WriteBudget => int.MaxValue;
            public long TotalVoxelsWritten => OperationCount;
            public int OperationCount { get; private set; }
            public int BoxCount { get; private set; }

            public byte Get(int x, int y, int z) => 0;
            public byte GetCoating(int x, int y, int z) => 0;
            public bool IsSolid(int x, int y, int z) => false;

            public void Set(int x, int y, int z, byte material) => OperationCount++;
            public void SetStyled(int x, int y, int z, byte material, ushort surfaceStyle,
                byte coating = Coatings.None, VoxelSurfaceFlags flags = VoxelSurfaceFlags.None) => OperationCount++;
            public void Coat(int x, int y, int z, byte coating) => OperationCount++;
            public void FillBulk(int3 min, int3 size, byte material) => OperationCount++;
            public void FillColumnBulk(int x, int minY, int maxYExclusive, int z, byte material) => OperationCount++;
            public void Box(int3 min, int3 size, byte material) { OperationCount++; BoxCount++; }
            public void HollowBox(int3 min, int3 size, int thickness, byte material, bool floor, bool ceiling) { OperationCount++; BoxCount++; }
            public void Cylinder(int cx, int baseY, int cz, int radius, int height, byte material, int innerRadius = 0) => OperationCount++;
            public void Disc(int cx, int y, int cz, int radius, byte material) => OperationCount++;
            public void Cone(int cx, int baseY, int cz, int radius, int height, byte material) => OperationCount++;
            public void HangingCone(int cx, int ceilingY, int cz, int radius, int height, byte material) => OperationCount++;
            public void Gable(int3 min, int3 size, bool alongX, byte material) => OperationCount++;
            public void Crenellate(int3 start, int3 step, int count, int width, int height, int merlon, int gap, byte material) => OperationCount++;
            public void CrenellateRing(int cx, int y, int cz, int radius, int height, byte material) => OperationCount++;
            public void Arch(int3 min, int width, int height, int depth, int depthAxis, byte material) => OperationCount++;
            public void Stairs(int3 min, int width, int steps, int rise, int run, int axis, byte material) => OperationCount++;
            public void SpiralStair(int cx, int baseY, int cz, int radius, int height, byte material) => OperationCount++;
            public void Carve(int3 min, int3 size) => OperationCount++;
            public void Weather(int3 min, int3 size, byte coating, uint seed, int chanceOutOf100) => OperationCount++;
        }
    }
}
