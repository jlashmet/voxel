using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Runtime;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastlePosternRealizerTests
    {
        [Test]
        public void PosternCarvesOnlyLowWallAndAddsDoorLeaf()
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
                    Centre = new int3(80, 2, 80),
                    PlateauHeight = 4,
                    WallHeight = 70,
                    WallThickness = 8,
                };
                int2[] perimeter =
                {
                    new int2(-30, -30),
                    new int2(30, -30),
                    new int2(30, 30),
                    new int2(-30, 30),
                };
                var postern = new CastleGatePlacementSpec
                {
                    EdgeIndex = 2,
                    Centre = new int2(0, 30),
                    Outward = new float2(0f, 1f),
                };
                CastleWallDoorPlan door = CastleWallDoorRecipe.PosternHistorical();
                CastleWallDoorGeometry geometry = CastleWallDoorGeometryResolver.Resolve(
                    in plan, in postern, in door);

                CastlePerimeterRealizer.Walls(ref brush, in plan, perimeter);
                CastlePosternRealizer.CarveOpening(
                    ref brush, in plan, in postern, in door);

                int baseY = plan.Centre.y + plan.PlateauHeight;
                int gateX = plan.Centre.x + postern.Centre.x;
                int gateZ = plan.Centre.z + postern.Centre.y;
                Assert.AreEqual(Mat.Empty, brush.Get(gateX, baseY + 8, gateZ),
                    "Postern opening should cut through the lower curtain wall.");
                Assert.AreEqual(Mat.Stone,
                    brush.Get(gateX, baseY + CastleLayout.PosternGateHeight + 6, gateZ),
                    "Postern must preserve masonry above the doorway.");

                CastlePosternRealizer.BuildDoor(
                    ref brush, in plan, in postern, in door);

                Assert.AreEqual(Mat.Wood, brush.Get(gateX, baseY + 6, gateZ),
                    "Postern should receive a wooden door leaf.");
                Assert.AreEqual(Mat.DarkStone, brush.Get(gateX, baseY + 11, gateZ),
                    "Postern door should retain its authored iron band.");
                Assert.AreEqual(Mat.Stone,
                    brush.Get(gateX, baseY + CastleLayout.PosternGateHeight + 6, gateZ),
                    "Building the door must not erase the wall above it.");

                int3[] interactionLeaf = geometry.LeafVoxels();
                Assert.Greater(interactionLeaf.Length, 0);
                for (int i = 0; i < interactionLeaf.Length; i++)
                {
                    Assert.AreNotEqual(Mat.Empty, brush.Get(
                        interactionLeaf[i].x, interactionLeaf[i].y, interactionLeaf[i].z),
                        $"shared interaction geometry missed authored leaf voxel {interactionLeaf[i]}");
                }

                Assert.AreEqual(0, brush.VoxelsWritten,
                    "Postern realization should stay on bulk wall writes.");
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
