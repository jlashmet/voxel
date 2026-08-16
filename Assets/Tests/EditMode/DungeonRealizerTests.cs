using System;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Runtime;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class DungeonRealizerTests
    {
        [Test]
        public void RealizerCarvesPlannedRoomsAndConnections()
        {
            var table = new RegionTable(16, Allocator.Persistent);
            var pool = new BrickPool(8192, Allocator.Persistent);

            try
            {
                var reads = new RegionReadSource(in table, in pool);
                var mutations = new RegionMutationStore(in table, in pool);
                var brush = new VoxelBrush(reads, mutations, writeBudget: 2_000_000);
                DungeonPlanningConstraints constraints = Constraints();
                DungeonPlan plan = DungeonPlanner.Create(41u, in constraints);

                DungeonRealizer.Build(ref brush, plan);

                DungeonRoomPlan puzzle = Find(plan, DungeonRoomPurpose.Puzzle);
                int puzzleFloor = puzzle.Centre.y - puzzle.Size.y / 2;
                Assert.AreEqual(
                    Mat.Empty,
                    brush.Get(puzzle.Centre.x, puzzleFloor + 12, puzzle.Centre.z),
                    "Planned puzzle room interior was not carved.");
                Assert.AreEqual(
                    Mat.DarkStone,
                    brush.Get(puzzle.Centre.x, puzzleFloor - 1, puzzle.Centre.z),
                    "Planned puzzle room did not receive its structural floor.");

                DungeonRoomPlan hall = Find(plan, DungeonRoomPurpose.GreatHall);
                int hallFloor = hall.Centre.y - hall.Size.y / 2;
                int corridorX = (hall.Centre.x + puzzle.Centre.x) / 2;
                Assert.AreEqual(
                    Mat.Empty,
                    brush.Get(corridorX, hallFloor + 10, hall.Centre.z),
                    "Hall-to-puzzle connection was not carved.");

                DungeonRoomPlan cave = Find(plan, DungeonRoomPurpose.CaveThreshold);
                int caveFloor = cave.Centre.y - cave.Size.y / 2;
                Assert.AreEqual(
                    Mat.Empty,
                    brush.Get(cave.Centre.x, caveFloor + 10, cave.Centre.z),
                    "Designed cave threshold was not carved.");

                Assert.Greater(brush.BulkVoxelsWritten + brush.VoxelsWritten, 0);
                Assert.IsFalse(brush.BudgetExceeded);
            }
            finally
            {
                table.Dispose();
                pool.Dispose();
            }
        }

        [Test]
        public void StairUsesSharedRoomOverlapRatherThanLowerRoomCentre()
        {
            var table = new RegionTable(16, Allocator.Persistent);
            var pool = new BrickPool(8192, Allocator.Persistent);

            try
            {
                var reads = new RegionReadSource(in table, in pool);
                var mutations = new RegionMutationStore(in table, in pool);
                var brush = new VoxelBrush(reads, mutations, writeBudget: 2_000_000);
                DungeonPlanningConstraints constraints = Constraints();
                DungeonPlan plan = DungeonPlanner.Create(47u, in constraints);
                int archiveIndex = FindIndex(plan, DungeonRoomPurpose.Archive);
                DungeonRoomPlan archive = plan.Rooms[archiveIndex];
                archive.Centre += new int3(50, 0, 0);
                plan.Rooms[archiveIndex] = archive;

                Assert.IsTrue(
                    DungeonPlanValidator.TryValidate(plan, out DungeonPlanIssue issue),
                    issue.ToString());

                DungeonConnectionPlan firstStair = plan.Connections[0];
                DungeonRoomPlan entrance = plan.Rooms[firstStair.FromRoomId];
                archive = plan.Rooms[firstStair.ToRoomId];
                Assert.IsTrue(
                    DungeonConnectionGeometry.TryStairShaftCentre(
                        in entrance, in archive, out int2 shaftCentre));
                Assert.AreNotEqual(archive.Centre.x, shaftCentre.x,
                    "Test setup must move the lower room centre away from the shared shaft overlap.");

                int entranceFloor = DungeonConnectionGeometry.RoomFloor(in entrance);
                int probeY = entranceFloor - 3;
                brush.FillBulk(
                    new int3(220, probeY, 230),
                    new int3(120, 1, 60),
                    Mat.Stone);

                DungeonRealizer.Build(ref brush, plan);

                Assert.AreEqual(
                    Mat.Empty,
                    brush.Get(shaftCentre.x, probeY, shaftCentre.y),
                    "Stair shaft was not carved through the shared room footprint.");
                Assert.AreEqual(
                    Mat.Stone,
                    brush.Get(archive.Centre.x, probeY, archive.Centre.z),
                    "Realizer still carved the stair at the lower room centre instead of the shared overlap.");
            }
            finally
            {
                table.Dispose();
                pool.Dispose();
            }
        }

        [Test]
        public void InvalidPlanIsRejectedBeforeAnyVoxelWrite()
        {
            var table = new RegionTable(8, Allocator.Persistent);
            var pool = new BrickPool(1024, Allocator.Persistent);

            try
            {
                var reads = new RegionReadSource(in table, in pool);
                var mutations = new RegionMutationStore(in table, in pool);
                var brush = new VoxelBrush(reads, mutations, writeBudget: 100_000);
                DungeonPlanningConstraints constraints = Constraints();
                DungeonPlan plan = DungeonPlanner.Create(43u, in constraints);
                plan.Connections[0].ToRoomId = plan.Rooms.Length + 1;

                Assert.Throws<InvalidOperationException>(() =>
                    DungeonRealizer.Build(ref brush, plan));
                Assert.AreEqual(0, brush.VoxelsWritten);
                Assert.AreEqual(0, brush.BulkVoxelsWritten);
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

        private static DungeonRoomPlan Find(DungeonPlan plan, DungeonRoomPurpose purpose) =>
            plan.Rooms[FindIndex(plan, purpose)];

        private static int FindIndex(DungeonPlan plan, DungeonRoomPurpose purpose)
        {
            for (int i = 0; i < plan.Rooms.Length; i++)
                if (plan.Rooms[i].Purpose == purpose) return i;
            Assert.Fail($"Missing dungeon room purpose {purpose}.");
            return -1;
        }
    }
}
