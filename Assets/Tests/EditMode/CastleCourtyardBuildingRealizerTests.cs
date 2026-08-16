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
        public void PlannedServiceBuildingKeepsShellDoorAndRoofOnBulkWrites()
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
                var building = new CastleCourtyardBuildingSpec
                {
                    Id = 0,
                    Role = CastleCourtyardBuildingRole.Service,
                    Centre = int2.zero,
                    HalfExtents = new int2(30, 20),
                    Height = 36,
                    EntranceDirection = new int2(0, -1),
                    RoofRidgeAlongX = true,
                };

                CastleCourtyardBuildingRealizer.Build(
                    ref brush, in plan, in building);

                int baseY = plan.Centre.y + plan.PlateauHeight;
                int2 shellLocal = building.FootprintCorner(0);
                int shellX = plan.Centre.x + shellLocal.x;
                int shellZ = plan.Centre.z + shellLocal.y;
                Assert.AreEqual(Mat.Stone, brush.Get(shellX, baseY + 12, shellZ),
                    "The planned footprint corner should retain its masonry shell.");

                int2 doorLocal = building.EntranceCentre;
                int doorX = plan.Centre.x + doorLocal.x;
                int doorZ = plan.Centre.z + doorLocal.y;
                Assert.AreEqual(Mat.Empty, brush.Get(doorX, baseY + 10, doorZ),
                    "The planned courtyard-facing doorway should be carved after the wall shell.");

                int roofHeight = math.clamp((building.HalfExtents.y + 6) * 2 / 3, 14, 28);
                int ridgeY = baseY + building.Height + roofHeight;
                Assert.AreEqual(Mat.Tile,
                    brush.Get(plan.Centre.x, ridgeY, plan.Centre.z),
                    "The roof ridge should follow RoofRidgeAlongX.");
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
