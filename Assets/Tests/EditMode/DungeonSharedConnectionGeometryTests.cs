using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Runtime;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class DungeonSharedConnectionGeometryTests
    {
        [Test]
        public void RealizerUsesValidatedSharedStairShaftWhenRoomsAreOffset()
        {
            var table = new RegionTable(16, Allocator.Persistent);
            var pool = new BrickPool(8192, Allocator.Persistent);

            try
            {
                var reads = new RegionReadSource(in table, in pool);
                var mutations = new RegionMutationStore(in table, in pool);
                var brush = new VoxelBrush(reads, mutations, writeBudget: 2_000_000);
                var constraints = new DungeonPlanningConstraints
                {
                    Entrance = new int3(256, 300, 256),
                    UpperLevelDrop = 46,
                    MainLevelDrop = 166,
                    RoomHeight = 40,
                    MainHallHalfX = 100,
                    MainHallHalfZ = 75,
                    SideRoomOffset = 180,
                    SideRoomHalfX = 45,
                    SideRoomHalfZ = 45,
                    CavePassageLength = 180,
                    IncludeArchive = true,
                    IncludePuzzle = true,
                    IncludeTreasury = true,
                    IncludeCaveExit = true,
                };
                DungeonPlan plan = DungeonPlanner.Create(41u, in constraints);

                DungeonConnectionPlan firstStair = plan.Connections[0];
                DungeonRoomPlan archive = plan.Rooms[firstStair.ToRoomId];
                archive.Centre.x += 60;
                plan.Rooms[firstStair.ToRoomId] = archive;

                DungeonRoomPlan entrance = plan.Rooms[firstStair.FromRoomId];
                Assert.IsTrue(DungeonConnectionGeometry.TryStairShaftCentre(
                    in entrance, in archive, out int2 shaft));
                Assert.IsTrue(DungeonPlanValidator.TryValidate(plan, out DungeonPlanIssue issue),
                    issue.ToString());

                int upperFloor = DungeonConnectionGeometry.RoomFloor(in entrance);
                int lowerFloor = DungeonConnectionGeometry.RoomFloor(in archive);
                int probeY = upperFloor - 3;
                Assert.Greater(probeY, lowerFloor + archive.Size.y,
                    "Probe must lie between the two rooms so only the stair shaft can clear it.");

                brush.FillColumnBulk(shaft.x, probeY, probeY + 1, shaft.y, Mat.Stone);
                Assert.AreEqual(Mat.Stone, brush.Get(shaft.x, probeY, shaft.y));

                DungeonRealizer.Build(ref brush, plan);

                Assert.AreEqual(Mat.Empty, brush.Get(shaft.x, probeY, shaft.y),
                    "Runtime must carve the same shared stair shaft accepted by validation.");
            }
            finally
            {
                table.Dispose();
                pool.Dispose();
            }
        }
    }
}
