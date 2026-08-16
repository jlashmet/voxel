using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Runtime;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleWallDoorPlanTests
    {
        [Test]
        public void HistoricalSecondaryDoorRecipesRemainValid()
        {
            CastleWallDoorPlan postern = CastleWallDoorRecipe.PosternHistorical();
            CastleWallDoorPlan inner = CastleWallDoorRecipe.InnerWardHistorical();

            Assert.IsTrue(CastleWallDoorPlanValidator.TryValidate(
                in postern, out CastleWallDoorPlanIssue posternIssue), posternIssue.ToString());
            Assert.IsTrue(CastleWallDoorPlanValidator.TryValidate(
                in inner, out CastleWallDoorPlanIssue innerIssue), innerIssue.ToString());

            Assert.AreEqual(CastleLayout.PosternGateWidth, postern.Width);
            Assert.AreEqual(CastleLayout.PosternGateHeight, postern.Height);
            Assert.AreEqual(CastleLayout.PosternGateDepth, postern.Depth);
            Assert.AreEqual(CastleLayout.FrontGateWidth, inner.Width);
            Assert.AreEqual(CastleLayout.FrontGateHeight, inner.Height);
            Assert.AreEqual(CastleLayout.FrontGateDepth, inner.Depth);
        }

        [Test]
        public void SeededInnerWardDoorVariationIsDeterministicAndBounded()
        {
            bool sawNarrower = false;
            bool sawWider = false;

            for (uint seed = 1; seed <= 128; seed++)
            {
                CastleWallDoorPlan first = CastleWallDoorPlanner.InnerWard(seed);
                CastleWallDoorPlan second = CastleWallDoorPlanner.InnerWard(seed);

                Assert.AreEqual(first.Width, second.Width, $"seed {seed}: width changed");
                Assert.GreaterOrEqual(first.Width, 36, $"seed {seed}: width too small");
                Assert.LessOrEqual(first.Width, 56, $"seed {seed}: width too large");
                Assert.IsTrue(CastleWallDoorPlanValidator.TryValidate(
                    in first, out CastleWallDoorPlanIssue issue), $"seed {seed}: {issue}");

                sawNarrower |= first.Width < CastleLayout.FrontGateWidth;
                sawWider |= first.Width > CastleLayout.FrontGateWidth;
            }

            Assert.IsTrue(sawNarrower, "Seeded inner-door planning never produced a narrower opening.");
            Assert.IsTrue(sawWider, "Seeded inner-door planning never produced a wider opening.");
        }

        [Test]
        public void LayoutPlannerCarriesOnlySemanticallyRequiredSecondaryDoors()
        {
            for (uint seed = 1; seed <= 256; seed++)
            {
                CastleTopologyPlan topology = CastleLayoutPlanner.Create(seed);

                if (topology.HasPosternGate)
                {
                    CastleWallDoorPlan postern = topology.PosternDoor;
                    Assert.IsTrue(CastleWallDoorPlanValidator.TryValidate(
                        in postern, out CastleWallDoorPlanIssue posternIssue),
                        $"seed {seed}: {posternIssue}");
                }
                else
                {
                    Assert.AreEqual(0, topology.PosternDoor.Width,
                        $"seed {seed}: disabled postern carried a door recipe");
                }

                if (topology.Wards == CastleWardPattern.InnerAndOuterWards)
                {
                    CastleWallDoorPlan inner = topology.InnerWardDoor;
                    Assert.IsTrue(CastleWallDoorPlanValidator.TryValidate(
                        in inner, out CastleWallDoorPlanIssue innerIssue),
                        $"seed {seed}: {innerIssue}");
                }
                else
                {
                    Assert.AreEqual(0, topology.InnerWardDoor.Width,
                        $"seed {seed}: single ward carried an inner-door recipe");
                }
            }
        }

        [Test]
        public void WallDoorRealizerConsumesFrozenStrapPattern()
        {
            var table = new RegionTable(8, Allocator.Persistent);
            var pool = new BrickPool(2048, Allocator.Persistent);

            try
            {
                var reads = new RegionReadSource(in table, in pool);
                var mutations = new RegionMutationStore(in table, in pool);
                var brush = new VoxelBrush(reads, mutations, writeBudget: 1);
                var castle = new CastlePlan
                {
                    Centre = new int3(80, 2, 80),
                    PlateauHeight = 4,
                    WallHeight = 48,
                    WallThickness = 8,
                };
                int2[] perimeter =
                {
                    new int2(-30, -30),
                    new int2(30, -30),
                    new int2(30, 30),
                    new int2(-30, 30),
                };
                var gate = new CastleGatePlacementSpec
                {
                    EdgeIndex = 0,
                    Centre = new int2(0, -30),
                    Outward = new float2(0f, -1f),
                };
                CastleWallDoorPlan door = CastleWallDoorRecipe.Historical(20, 30, 3);
                door.StrapFirstY = 6;
                door.StrapSpacing = 9;
                door.StrapThickness = 1;
                CastleWallDoorPlanValidator.RequireValid(in door);

                CastlePerimeterRealizer.Walls(ref brush, in castle, perimeter);
                CastleWallDoorRealizer.CarveArchedOpening(
                    ref brush, in castle, in gate, in door);
                CastleWallDoorRealizer.BuildArchedDoor(
                    ref brush, in castle, in gate, in door);

                int x = castle.Centre.x;
                int z = castle.Centre.z - 30;
                int doorBaseY = castle.Centre.y + castle.PlateauHeight + 1;
                Assert.AreEqual(Mat.DarkStone, brush.Get(x, doorBaseY + 6, z),
                    "First iron strap should come from CastleWallDoorPlan.");
                Assert.AreEqual(Mat.Wood, brush.Get(x, doorBaseY + 7, z),
                    "The row after a one-voxel strap should remain wood.");
                Assert.AreEqual(Mat.DarkStone, brush.Get(x, doorBaseY + 15, z),
                    "Strap spacing should come from CastleWallDoorPlan.");

                Assert.AreEqual(0, brush.VoxelsWritten);
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
