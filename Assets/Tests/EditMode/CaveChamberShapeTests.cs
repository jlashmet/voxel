using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CaveChamberShapeTests
    {
        [TestCase(CaveChamberShape.Round)]
        [TestCase(CaveChamberShape.Box)]
        public void ChamberShapeUsesSelectedBoundedPrimitive(CaveChamberShape shape)
        {
            CaveConfig config = CaveConfig.Default;
            config.TunnelWidth = 5;
            config.TunnelHeight = 7;
            config.SegmentLength = 6;
            config.MainSegmentCount = 6;
            config.TurnChancePercent = 0;
            config.VerticalChancePercent = 0;
            config.BranchChancePercent = 100;
            config.MaxBranches = 2;
            config.MaxBranchDepth = 1;
            config.BranchSegmentCount = 3;
            config.MinBranchSeparation = 0;
            config.ChamberChancePercent = 100;
            config.ChamberShape = shape;
            config.MinChamberRadius = 4;
            config.MaxChamberRadius = 6;
            config.MinChamberHeight = 5;
            config.MaxChamberHeight = 8;
            config.BoundsHalfExtents = new int3(96, 48, 96);
            config.MinVerticalOffset = -32;
            config.MaxVerticalOffset = 24;

            CaveGenerationRequest request = CaveGenerationRequest.Attached(
                0x778899ul, int3.zero, Facing.East, 5, 7, 3);
            CaveMaterialPalette palette = new CaveMaterialPalette
            {
                Opening = 0,
                Rock = 2,
                Accent = 3,
                Decoration = 4,
                Water = 5,
            };
            var session = new ShapeCountingSession();

            CaveAuthoringResult result = CaveAuthoring.Author(
                session, in request, in config, in palette);

            Assert.Multiple(() =>
            {
                Assert.That(result.BranchesAuthored, Is.GreaterThan(0));
                Assert.That(result.BranchesAuthored, Is.LessThanOrEqualTo(config.MaxBranches));
                Assert.That(result.ChambersAuthored, Is.GreaterThan(0));
                if (shape == CaveChamberShape.Round)
                {
                    Assert.That(session.Cylinders, Is.GreaterThan(0));
                    Assert.AreEqual(0, session.Boxes);
                }
                else
                {
                    Assert.That(session.Boxes, Is.GreaterThan(0));
                    Assert.AreEqual(0, session.Cylinders);
                }
            });
        }

        [Test]
        public void InvalidChamberShapeIsRejected()
        {
            CaveConfig config = CaveConfig.Default;
            config.ChamberShape = (CaveChamberShape)255;
            Assert.IsFalse(config.IsWellFormed);
        }

        private sealed class ShapeCountingSession : IStructureAuthoringSession
        {
            public int Boxes { get; private set; }
            public int Cylinders { get; private set; }
            public bool BudgetExceeded => false;
            public int WriteBudget => int.MaxValue;
            public long TotalVoxelsWritten => 0;
            public byte Get(int x, int y, int z) => 0;
            public byte GetCoating(int x, int y, int z) => 0;
            public bool IsSolid(int x, int y, int z) => false;
            public void Set(int x, int y, int z, byte material) { }
            public void SetStyled(int x, int y, int z, byte material, ushort surfaceStyle,
                byte coating = Coatings.None, VoxelSurfaceFlags flags = VoxelSurfaceFlags.None) { }
            public void Coat(int x, int y, int z, byte coating) { }
            public void FillBulk(int3 min, int3 size, byte material) { }
            public void FillColumnBulk(int x, int minY, int maxYExclusive, int z, byte material) { }
            public void Box(int3 min, int3 size, byte material) => Boxes++;
            public void HollowBox(int3 min, int3 size, int thickness, byte material,
                bool floor, bool ceiling) { }
            public void Cylinder(int cx, int baseY, int cz, int radius, int height,
                byte material, int innerRadius = 0) => Cylinders++;
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
