using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Runtime;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class DungeonRoomFurnisherTests
    {
        [Test]
        public void PlannedRoomPurposesReceiveAuthoredDetail()
        {
            var table = new RegionTable(16, Allocator.Persistent);
            var pool = new BrickPool(8192, Allocator.Persistent);

            try
            {
                var reads = new RegionReadSource(in table, in pool);
                var mutations = new RegionMutationStore(in table, in pool);
                var brush = new VoxelBrush(reads, mutations, writeBudget: 2_000_000);
                DungeonPlanningConstraints constraints = Constraints();
                DungeonPlan plan = DungeonPlanner.Create(53u, in constraints);

                DungeonRealizer.Build(ref brush, plan);
                DungeonRoomFurnisher.FurnishAll(ref brush, plan);

                DungeonRoomPlan archive = Find(plan, DungeonRoomPurpose.Archive);
                int3 archiveMin = archive.Centre - archive.Size / 2;
                Assert.AreEqual(
                    Mat.Wood,
                    brush.Get(archiveMin.x + 8, archiveMin.y + 10, archiveMin.z + 14),
                    "Archive room lost its shelving.");

                DungeonRoomPlan hall = Find(plan, DungeonRoomPurpose.GreatHall);
                int hallFloor = hall.Centre.y - hall.Size.y / 2;
                int daisZ = hall.Centre.z - hall.Size.z / 2 + 28;
                Assert.AreEqual(
                    Mat.DarkStone,
                    brush.Get(hall.Centre.x, hallFloor + 2, daisZ + 2),
                    "Great hall lost its planned dais.");

                DungeonRoomPlan puzzle = Find(plan, DungeonRoomPurpose.Puzzle);
                int puzzleFloor = puzzle.Centre.y - puzzle.Size.y / 2;
                Assert.AreEqual(
                    Mat.Slate,
                    brush.Get(puzzle.Centre.x, puzzleFloor + 2, puzzle.Centre.z),
                    "Puzzle room lost its central marker.");

                DungeonRoomPlan treasury = Find(plan, DungeonRoomPurpose.Treasury);
                int3 treasuryMin = treasury.Centre - treasury.Size / 2;
                int chestX = treasuryMin.x + treasury.Size.x / 4;
                int chestZ = treasury.Centre.z - math.max(14, treasury.Size.z / 3);
                Assert.AreEqual(
                    Mat.Gold,
                    brush.Get(chestX, treasuryMin.y + 9, chestZ),
                    "Treasury room lost its gold-banded storage.");

                Assert.Greater(brush.BulkVoxelsWritten + brush.VoxelsWritten, 0);
                Assert.IsFalse(brush.BudgetExceeded);
            }
            finally
            {
                table.Dispose();
                pool.Dispose();
            }
        }

        private static DungeonPlanningConstraints Constraints() =>
            new DungeonPlanningConstraints
            {
                Entrance = new int3(256, 300, 256),
                UpperLevelDrop = 46,
                MainLevelDrop = 166,
                RoomHeight = 40,
                MainHallHalfX = 130,
                MainHallHalfZ = 90,
                SideRoomOffset = 226,
                SideRoomHalfX = 50,
                SideRoomHalfZ = 55,
                CavePassageLength = 321,
                IncludeArchive = true,
                IncludePuzzle = true,
                IncludeTreasury = true,
                IncludeCaveExit = true,
            };

        private static DungeonRoomPlan Find(DungeonPlan plan, DungeonRoomPurpose purpose)
        {
            for (int i = 0; i < plan.Rooms.Length; i++)
                if (plan.Rooms[i].Purpose == purpose) return plan.Rooms[i];
            Assert.Fail($"Missing dungeon room purpose {purpose}.");
            return default;
        }
    }
}
