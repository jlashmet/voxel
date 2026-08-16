using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Runtime;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastlePerimeterRealizerTests
    {
        [Test]
        public void IrregularPerimeterFollowsEveryEdgeWithoutFillingCourtyard()
        {
            var table = new RegionTable(8, Allocator.Persistent);
            var pool = new BrickPool(2048, Allocator.Persistent);

            try
            {
                var reads = new RegionReadSource(in table, in pool);
                var mutations = new RegionMutationStore(in table, in pool);
                var brush = new VoxelBrush(reads, mutations, writeBudget: 1);
                var plan = new CastlePlan
                {
                    Centre = new int3(64, 2, 64),
                    PlateauHeight = 4,
                    WallHeight = 24,
                    WallThickness = 6,
                };
                int2[] perimeter =
                {
                    new int2(-24, -18),
                    new int2(26, -14),
                    new int2(10, 30),
                };

                CastlePerimeterRealizer.Walls(ref brush, in plan, perimeter);

                int baseY = plan.Centre.y + plan.PlateauHeight;
                for (int edge = 0; edge < perimeter.Length; edge++)
                {
                    int2 a = perimeter[edge];
                    int2 b = perimeter[(edge + 1) % perimeter.Length];
                    int2 midpoint = new int2((a.x + b.x) / 2, (a.y + b.y) / 2);
                    int worldX = plan.Centre.x + midpoint.x;
                    int worldZ = plan.Centre.z + midpoint.y;

                    Assert.AreEqual(Mat.DarkStone, brush.Get(worldX, baseY, worldZ),
                        $"edge {edge} is missing its plinth");
                    Assert.AreEqual(Mat.Stone, brush.Get(worldX, baseY + plan.WallHeight, worldZ),
                        $"edge {edge} is missing its wall walk");
                }

                Assert.AreEqual(Mat.Empty, brush.Get(plan.Centre.x, baseY + 10, plan.Centre.z),
                    "The generic perimeter realizer must not fill the enclosed courtyard.");
                Assert.AreEqual(0, brush.VoxelsWritten,
                    "Arbitrary perimeter walls must remain on bulk column writes.");
                Assert.Greater(brush.BulkVoxelsWritten, 0);
                Assert.IsFalse(brush.BudgetExceeded);
            }
            finally
            {
                table.Dispose();
                pool.Dispose();
            }
        }

        [Test]
        public void DiagonalWallArrowSlitCutsAcrossSegmentNormal()
        {
            var table = new RegionTable(8, Allocator.Persistent);
            var pool = new BrickPool(4096, Allocator.Persistent);

            try
            {
                var reads = new RegionReadSource(in table, in pool);
                var mutations = new RegionMutationStore(in table, in pool);
                var brush = new VoxelBrush(reads, mutations, writeBudget: 1);
                var plan = new CastlePlan
                {
                    Centre = new int3(80, 2, 80),
                    PlateauHeight = 4,
                    WallHeight = 90,
                    WallThickness = 8,
                };
                int2[] perimeter =
                {
                    new int2(-30, -30),
                    new int2(30, 10),
                    new int2(-10, 42),
                };

                CastlePerimeterRealizer.Walls(ref brush, in plan, perimeter);

                float2 a = new float2(perimeter[0].x, perimeter[0].y);
                float2 delta = new float2(
                    perimeter[1].x - perimeter[0].x,
                    perimeter[1].y - perimeter[0].y);
                float2 slit = a + math.normalize(delta) * 40f;
                int worldX = plan.Centre.x + (int)math.round(slit.x);
                int worldZ = plan.Centre.z + (int)math.round(slit.y);
                int baseY = plan.Centre.y + plan.PlateauHeight;

                Assert.AreEqual(Mat.Empty, brush.Get(worldX, baseY + 48, worldZ),
                    "The diagonal arrow slit should be carved through the wall body.");
                Assert.AreEqual(Mat.Stone, brush.Get(worldX, baseY + 34, worldZ),
                    "The slit must not erase the wall below its opening.");
                Assert.AreEqual(0, brush.VoxelsWritten);
                Assert.IsFalse(brush.BudgetExceeded);
            }
            finally
            {
                table.Dispose();
                pool.Dispose();
            }
        }

        [Test]
        public void PlannedGateOpeningSplitsAnAngledWallEdge()
        {
            var table = new RegionTable(8, Allocator.Persistent);
            var pool = new BrickPool(2048, Allocator.Persistent);

            try
            {
                var reads = new RegionReadSource(in table, in pool);
                var mutations = new RegionMutationStore(in table, in pool);
                var brush = new VoxelBrush(reads, mutations, writeBudget: 1);
                var plan = new CastlePlan
                {
                    Centre = new int3(72, 2, 72),
                    PlateauHeight = 4,
                    WallHeight = 24,
                    WallThickness = 6,
                };
                int2[] perimeter =
                {
                    new int2(-28, -20),
                    new int2(28, -12),
                    new int2(18, 30),
                    new int2(-24, 24),
                };
                int2 gate = new int2(
                    (perimeter[0].x + perimeter[1].x) / 2,
                    (perimeter[0].y + perimeter[1].y) / 2);

                CastlePerimeterRealizer.Walls(
                    ref brush,
                    in plan,
                    perimeter,
                    gateEdgeIndex: 0,
                    localGateCentre: gate,
                    gateClearWidth: 14);

                int baseY = plan.Centre.y + plan.PlateauHeight;
                int gateX = plan.Centre.x + gate.x;
                int gateZ = plan.Centre.z + gate.y;
                Assert.AreEqual(Mat.Empty, brush.Get(gateX, baseY + 10, gateZ),
                    "The planned gate opening should remain clear through the wall body.");

                float2 tangent = math.normalize(new float2(
                    perimeter[1].x - perimeter[0].x,
                    perimeter[1].y - perimeter[0].y));
                for (int side = -1; side <= 1; side += 2)
                {
                    float2 sample = new float2(gate.x, gate.y) + tangent * (side * 13f);
                    int sampleX = plan.Centre.x + (int)math.round(sample.x);
                    int sampleZ = plan.Centre.z + (int)math.round(sample.y);
                    Assert.AreNotEqual(Mat.Empty, brush.Get(sampleX, baseY + 10, sampleZ),
                        $"wall did not resume on side {side} of the gate opening");
                }

                Assert.AreEqual(0, brush.VoxelsWritten);
                Assert.IsFalse(brush.BudgetExceeded);
            }
            finally
            {
                table.Dispose();
                pool.Dispose();
            }
        }
    }
}
