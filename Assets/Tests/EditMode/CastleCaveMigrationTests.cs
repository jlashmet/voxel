using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleCaveMigrationTests
    {
        [Test]
        public void CastleCaveRequestUsesExactSemanticCaveAnchorAndSharedUndergroundMode()
        {
            CastlePlan plan = CastlePlanner.Plan(new int3(160, 28, -70), 0xABCDEF12u);
            CastleUndergroundAttachmentConfig underground =
                CastleUndergroundAttachmentPresets.Compatibility(in plan);
            int3 caveAnchor = underground.ResolveCave(in plan);

            CaveGenerationRequest request = CastleCaveAuthoring.Request(in plan, caveAnchor);
            CaveConfig config = CastleCaveAuthoring.CompatibilityConfig;
            CaveMaterialPalette palette = CastleCaveAuthoring.CompatibilityPalette;

            Assert.Multiple(() =>
            {
                Assert.IsTrue(request.IsWellFormed);
                Assert.IsTrue(config.IsWellFormed);
                Assert.AreEqual(CaveEntranceMode.Underground, request.Entrance.Mode);
                Assert.AreEqual(caveAnchor, request.Origin);
                Assert.AreEqual(caveAnchor, request.EntranceWorldPosition);
                Assert.AreEqual(Facing.South, request.Entrance.Facing);
                Assert.AreEqual(28, request.Entrance.Width);
                Assert.AreEqual(32, request.Entrance.Height);
                Assert.AreEqual(Game.Materials.Api.GameMaterialIds.Empty, palette.Opening);
                Assert.AreEqual(Game.Materials.Api.GameMaterialIds.Crystal, palette.Accent);
                Assert.AreEqual(Game.Materials.Api.GameMaterialIds.Moss, palette.Decoration);
                Assert.AreEqual(Game.Materials.Api.GameMaterialIds.Water, palette.Water);
            });
        }

        [Test]
        public void CastleAdapterExecutesSharedGeneratorAndProducesStableHooks()
        {
            CastlePlan plan = CastlePlanner.Plan(int3.zero, 0x10203040u);
            int3 caveAnchor = CastleUndergroundAttachmentPresets
                .Compatibility(in plan)
                .ResolveCave(in plan);
            var a = new CountingSession();
            var b = new CountingSession();

            CaveAuthoringResult resultA = CastleCaveAuthoring.Author(a, in plan, caveAnchor);
            CaveAuthoringResult resultB = CastleCaveAuthoring.Author(b, in plan, caveAnchor);
            CaveGenerationRequest request = CastleCaveAuthoring.Request(in plan, caveAnchor);
            CaveHookSet hooksA = CaveHookPlanner.AtMainPathEnd(in request, resultA.MainPathEnd);
            CaveHookSet hooksB = CaveHookPlanner.AtMainPathEnd(in request, resultB.MainPathEnd);

            Assert.Multiple(() =>
            {
                Assert.That(resultA.SegmentsAuthored, Is.GreaterThan(0));
                Assert.That(a.ColumnWrites, Is.GreaterThan(0));
                Assert.AreEqual(a.ColumnWrites, b.ColumnWrites);
                Assert.AreEqual(resultA.SegmentsAuthored, resultB.SegmentsAuthored);
                Assert.AreEqual(resultA.MainPathEnd, resultB.MainPathEnd);
                Assert.AreEqual(3, hooksA.Count);
                Assert.AreEqual(hooksA.Count, hooksB.Count);
                for (int i = 0; i < hooksA.Count; i++)
                {
                    Assert.AreEqual(hooksA.Items[i].Kind, hooksB.Items[i].Kind);
                    Assert.AreEqual(resultA.MainPathEnd, hooksA.Items[i].Position);
                    Assert.AreEqual(hooksA.Items[i].Seed, hooksB.Items[i].Seed);
                    Assert.AreNotEqual(0ul, hooksA.Items[i].Seed);
                }
            });
        }

        private sealed class CountingSession : IStructureAuthoringSession
        {
            public int ColumnWrites { get; private set; }
            public bool BudgetExceeded => false;
            public int WriteBudget => int.MaxValue;
            public long TotalVoxelsWritten => ColumnWrites;

            public byte Get(int x, int y, int z) => 0;
            public byte GetCoating(int x, int y, int z) => 0;
            public bool IsSolid(int x, int y, int z) => false;
            public void Set(int x, int y, int z, byte material) { }
            public void SetStyled(int x, int y, int z, byte material, ushort surfaceStyle,
                byte coating = Coatings.None, VoxelSurfaceFlags flags = VoxelSurfaceFlags.None) { }
            public void Coat(int x, int y, int z, byte coating) { }
            public void FillBulk(int3 min, int3 size, byte material) { }
            public void FillColumnBulk(int x, int minY, int maxYExclusive, int z, byte material)
            {
                ColumnWrites++;
            }
            public void Box(int3 min, int3 size, byte material) { }
            public void HollowBox(int3 min, int3 size, int thickness, byte material,
                bool floor, bool ceiling) { }
            public void Cylinder(int cx, int baseY, int cz, int radius, int height,
                byte material, int innerRadius = 0) { }
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
