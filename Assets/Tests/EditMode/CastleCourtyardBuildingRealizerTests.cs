using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Runtime;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleCourtyardBuildingRealizerTests
    {
        [Test]
        public void PlannedOrientedBuildingKeepsShellDoorAndRoofOnBulkWrites()
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
                };
                float2 tangent = math.normalize(new float2(1f, 1f));
                var building = new CastleCourtyardBuildingSpec
                {
                    Id = 0,
                    Purpose = CastleCourtyardBuildingPurpose.Barracks,
                    WallEdgeIndex = 1,
                    Centre = int2.zero,
                    Tangent = tangent,
                    Inward = new float2(-tangent.y, tangent.x),
                    Width = 60,
                    Depth = 40,
                    Height = 36,
                };

                CastleCourtyardBuildingRealizer.Build(
                    ref brush, in plan, in building);

                int baseY = plan.Centre.y + plan.PlateauHeight;
                int2 shellLocal = building.FootprintCorner(0);
                int shellX = plan.Centre.x + shellLocal.x;
                int shellZ = plan.Centre.z + shellLocal.y;
                Assert.AreEqual(Mat.Stone, brush.Get(shellX, baseY + 12, shellZ),
                    "The rotated planned footprint corner should retain its masonry shell.");

                int2 doorLocal = building.DoorCentre;
                int doorX = plan.Centre.x + doorLocal.x;
                int doorZ = plan.Centre.z + doorLocal.y;
                Assert.AreEqual(Mat.Empty, brush.Get(doorX, baseY + 10, doorZ),
                    "The planned courtyard-facing doorway should be carved after the wall shell.");

                int roofHeight = math.clamp(building.Depth / 3, 14, 28);
                int ridgeY = baseY + building.Height + roofHeight;
                Assert.AreEqual(Mat.Tile,
                    brush.Get(plan.Centre.x, ridgeY, plan.Centre.z),
                    "The roof ridge should follow the building's planned wall tangent.");
                Assert.AreEqual(Mat.Empty,
                    brush.Get(plan.Centre.x, baseY + 12, plan.Centre.z),
                    "The building interior should remain hollow.");

                Assert.AreEqual(0, brush.VoxelsWritten,
                    "Courtyard buildings should stay entirely on bulk column writes.");
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
