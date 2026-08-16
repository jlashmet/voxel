using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Runtime;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleWallDoorRealizerTests
    {
        [Test]
        public void ArchedInnerGatePreservesWallAboveAndCurvedShoulders()
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
                    Centre = new int3(96, 2, 96),
                    PlateauHeight = 4,
                    WallHeight = 90,
                    WallThickness = 8,
                };
                int2[] perimeter =
                {
                    new int2(-42, -34),
                    new int2(42, -34),
                    new int2(42, 34),
                    new int2(-42, 34),
                };
                var innerGate = new CastleGatePlacementSpec
                {
                    EdgeIndex = 2,
                    Centre = new int2(0, 34),
                    Outward = new float2(0f, 1f),
                };

                CastlePerimeterRealizer.Walls(ref brush, in plan, perimeter);
                CastleWallDoorRealizer.CarveArchedOpening(
                    ref brush,
                    in plan,
                    in innerGate,
                    CastleLayout.FrontGateWidth,
                    CastleLayout.FrontGateHeight);

                int baseY = plan.Centre.y + plan.PlateauHeight;
                int gateX = plan.Centre.x;
                int gateZ = plan.Centre.z + innerGate.Centre.y;
                Assert.AreEqual(Mat.Empty, brush.Get(gateX, baseY + 10, gateZ),
                    "Inner gate should open through the lower curtain wall.");
                Assert.AreEqual(Mat.Empty, brush.Get(gateX, baseY + 55, gateZ),
                    "The centre of the arched head should remain open.");
                Assert.AreEqual(Mat.Stone, brush.Get(gateX - 23, baseY + 55, gateZ),
                    "Curved arch shoulders must preserve masonry outside the semicircle.");
                Assert.AreEqual(Mat.Stone,
                    brush.Get(gateX, baseY + CastleLayout.FrontGateHeight + 6, gateZ),
                    "Inner gate must preserve the curtain wall above the passage.");

                CastleWallDoorRealizer.BuildArchedDoor(
                    ref brush,
                    in plan,
                    in innerGate,
                    CastleLayout.FrontGateWidth,
                    CastleLayout.FrontGateHeight,
                    CastleLayout.FrontGateDepth);

                Assert.AreEqual(Mat.Wood, brush.Get(gateX, baseY + 8, gateZ));
                Assert.AreEqual(Mat.DarkStone, brush.Get(gateX, baseY + 11, gateZ),
                    "Reusable wall door should retain its authored iron straps.");
                Assert.AreEqual(Mat.Stone,
                    brush.Get(gateX, baseY + CastleLayout.FrontGateHeight + 6, gateZ));
                Assert.AreEqual(0, brush.VoxelsWritten,
                    "Arched wall doors must remain on bulk column writes.");
                Assert.Greater(brush.BulkVoxelsWritten, 0);
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
