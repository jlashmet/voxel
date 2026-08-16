using System;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Runtime;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class DungeonPlanShellRealizerTests
    {
        [Test]
        public void PlannedRoomsAndCorridorsRealizeFromGraphCoordinates()
        {
            var table = new RegionTable(8, Allocator.Persistent);
            var pool = new BrickPool(8192, Allocator.Persistent);

            try
            {
                var reads = new RegionReadSource(in table, in pool);
                var mutations = new RegionMutationStore(in table, in pool);
                var brush = new VoxelBrush(reads, mutations, writeBudget: 250_000);
                var constraints = new DungeonPlanningConstraints
                {
                    Entrance = new int3(128, 200, 128),
                    UpperLevelDrop = 48,
                    MainLevelDrop = 100,
                    RoomHeight = 32,
                    MainHallHalfX = 40,
                    MainHallHalfZ = 40,
                    SideRoomOffset = 80,
                    SideRoomHalfX = 24,
                    SideRoomHalfZ = 24,
                    CavePassageLength = 64,
                    IncludeArchive = true,
                    IncludePuzzle = true,
                    IncludeTreasury = true,
                    IncludeCaveExit = true,
                };
                DungeonPlan plan = DungeonPlanner.Create(37u, in constraints);

                DungeonConnectionPlan corridor = FindConnection(
                    plan, DungeonConnectionKind.Corridor);
                DungeonRoomPlan from = plan.Rooms[corridor.FromRoomId];
                DungeonRoomPlan to = plan.Rooms[corridor.ToRoomId];
                int3 midpoint = new int3(
                    (from.Centre.x + to.Centre.x) / 2,
                    math.min(from.Centre.y - from.Size.y / 2,
                             to.Centre.y - to.Size.y / 2),
                    (from.Centre.z + to.Centre.z) / 2);
                brush.FillColumnBulk(
                    midpoint.x, midpoint.y, midpoint.y + 30, midpoint.z, Mat.Stone);

                DungeonPlanShellRealizer.Build(ref brush, plan);

                Assert.AreEqual(
                    Mat.Empty,
                    brush.Get(midpoint.x, midpoint.y + 10, midpoint.z),
                    "Planned corridor did not carve through pre-existing solid geometry.");

                for (int i = 0; i < plan.Rooms.Length; i++)
                {
                    DungeonRoomPlan room = plan.Rooms[i];
                    int floorY = room.Centre.y - room.Size.y / 2;
                    Assert.AreEqual(
                        Mat.DarkStone,
                        brush.Get(room.Centre.x, floorY - 1, room.Centre.z),
                        $"room {room.Id}/{room.Purpose} is missing its planned floor");
                    Assert.AreEqual(
                        Mat.Empty,
                        brush.Get(room.Centre.x, floorY + 4, room.Centre.z),
                        $"room {room.Id}/{room.Purpose} did not realize as traversable space");
                }

                Assert.IsFalse(brush.BudgetExceeded);
            }
            finally
            {
                table.Dispose();
                pool.Dispose();
            }
        }

        [Test]
        public void RealizerRejectsCorruptedGraphBeforeWriting()
        {
            var table = new RegionTable(8, Allocator.Persistent);
            var pool = new BrickPool(1024, Allocator.Persistent);

            try
            {
                var reads = new RegionReadSource(in table, in pool);
                var mutations = new RegionMutationStore(in table, in pool);
                var brush = new VoxelBrush(reads, mutations);
                var constraints = new DungeonPlanningConstraints
                {
                    Entrance = new int3(64, 180, 64),
                    UpperLevelDrop = 48,
                    MainLevelDrop = 100,
                    RoomHeight = 32,
                    MainHallHalfX = 40,
                    MainHallHalfZ = 40,
                    SideRoomOffset = 80,
                    SideRoomHalfX = 24,
                    SideRoomHalfZ = 24,
                    CavePassageLength = 64,
                    IncludeArchive = true,
                    IncludePuzzle = true,
                    IncludeTreasury = true,
                    IncludeCaveExit = true,
                };
                DungeonPlan plan = DungeonPlanner.Create(41u, in constraints);
                DungeonRoomPlan corrupted = plan.Rooms[0];
                corrupted.Id = 99;
                plan.Rooms[0] = corrupted;

                Assert.Throws<InvalidOperationException>(() =>
                    DungeonPlanShellRealizer.Build(ref brush, plan));
                Assert.AreEqual(0, brush.TotalVoxelsWritten,
                    "Invalid dungeon graphs must be rejected before voxel mutation.");
            }
            finally
            {
                table.Dispose();
                pool.Dispose();
            }
        }

        [Test]
        public void GenericShellDoesNotOwnCastleFurnitureOrNaturalCaves()
        {
            string source = System.IO.File.ReadAllText(System.IO.Path.Combine(
                System.IO.Directory.GetCurrentDirectory(),
                "Assets", "VoxelEngine", "Structures", "Runtime",
                "DungeonPlanShellRealizer.cs"));

            StringAssert.DoesNotContain("CastleCaveRealizer", source);
            StringAssert.DoesNotContain("DungeonRoomPurpose.Puzzle", source);
            StringAssert.DoesNotContain("DungeonRoomPurpose.Treasury", source);
            StringAssert.DoesNotContain("DungeonRoomPurpose.Archive", source);
        }

        private static DungeonConnectionPlan FindConnection(
            DungeonPlan plan,
            DungeonConnectionKind kind)
        {
            for (int i = 0; i < plan.Connections.Length; i++)
                if (plan.Connections[i].Kind == kind) return plan.Connections[i];
            Assert.Fail($"Dungeon plan did not contain a {kind} connection.");
            return default;
        }
    }
}
