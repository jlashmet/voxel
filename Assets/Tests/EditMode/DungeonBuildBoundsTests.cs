using System.IO;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class DungeonBuildBoundsTests
    {
        [Test]
        public void PlannedRoomsPassagesAndStairsFitInsideResolvedBounds()
        {
            DungeonPlanningConstraints constraints = WideDungeonConstraints();
            DungeonPlan plan = DungeonPlanner.Create(0xB01D5u, in constraints);
            DungeonBuildBounds bounds = DungeonBuildBoundsResolver.Resolve(plan);

            for (int i = 0; i < plan.Rooms.Length; i++)
            {
                DungeonRoomPlan room = plan.Rooms[i];
                int3 roomMin = room.Centre - room.Size / 2;
                int3 roomMax = roomMin + room.Size - 1;
                Assert.IsTrue(bounds.Contains(roomMin), $"room {i} minimum escaped bounds");
                Assert.IsTrue(bounds.Contains(roomMax), $"room {i} maximum escaped bounds");
                Assert.IsTrue(
                    bounds.Contains(roomMin - new int3(0, DungeonConnectionGeometry.FloorThickness, 0)),
                    $"room {i} authored floor escaped bounds");
            }

            for (int i = 0; i < plan.Connections.Length; i++)
            {
                DungeonConnectionPlan connection = plan.Connections[i];
                DungeonRoomPlan from = plan.Rooms[connection.FromRoomId];
                DungeonRoomPlan to = plan.Rooms[connection.ToRoomId];

                if (connection.Kind == DungeonConnectionKind.Stair)
                {
                    Assert.IsTrue(DungeonConnectionGeometry.TryStairShaftCentre(
                        in from, in to, out int2 shaft));
                    int lowY = math.min(
                        DungeonConnectionGeometry.RoomFloor(in from),
                        DungeonConnectionGeometry.RoomFloor(in to));
                    int highY = math.max(
                        DungeonConnectionGeometry.RoomFloor(in from),
                        DungeonConnectionGeometry.RoomFloor(in to));
                    int radius = DungeonConnectionGeometry.StairShaftRadius;
                    Assert.IsTrue(bounds.Contains(new int3(shaft.x - radius, lowY, shaft.y)));
                    Assert.IsTrue(bounds.Contains(new int3(shaft.x + radius, highY + 1, shaft.y)));
                    continue;
                }

                int floorY = DungeonConnectionGeometry.RoomFloor(in from);
                int width = DungeonConnectionGeometry.PassageWidth(connection.Kind);
                int height = DungeonConnectionGeometry.PassageHeight(connection.Kind);
                int half = width / 2;
                int2 corner = DungeonConnectionGeometry.PassageCorner(in from, in to);

                Assert.IsTrue(bounds.Contains(new int3(
                    corner.x,
                    floorY - DungeonConnectionGeometry.FloorThickness,
                    corner.y - half)));
                Assert.IsTrue(bounds.Contains(new int3(
                    corner.x,
                    floorY + height - 1,
                    corner.y + half - 1)));
            }
        }

        [Test]
        public void PassageGeometryIsSharedByBoundsAndRuntime()
        {
            string repoRoot = FindRepoRoot();
            string runtime = File.ReadAllText(Path.Combine(
                repoRoot, "Assets", "VoxelEngine", "Structures", "Runtime", "DungeonRealizer.cs"));
            string castleBounds = File.ReadAllText(Path.Combine(
                repoRoot, "Assets", "VoxelEngine", "Structures", "Api", "CastleBuildBounds.cs"));

            StringAssert.Contains("DungeonConnectionGeometry.PassageWidth(connection.Kind)", runtime);
            StringAssert.Contains("DungeonConnectionGeometry.PassageHeight(connection.Kind)", runtime);
            StringAssert.Contains("DungeonConnectionGeometry.PassageCorner(in from, in to)", runtime);
            StringAssert.DoesNotContain("CarvePassage(ref brush, in from, in to, 20, 30", runtime);
            StringAssert.DoesNotContain("CarvePassage(ref brush, in from, in to, 28, 32", runtime);
            StringAssert.Contains("DungeonBuildBoundsResolver.Resolve(dungeon)", castleBounds);
        }

        private static DungeonPlanningConstraints WideDungeonConstraints() =>
            new DungeonPlanningConstraints
            {
                Entrance = new int3(100, 320, -80),
                UpperLevelDrop = 52,
                MainLevelDrop = 116,
                RoomHeight = 32,
                MainHallHalfX = 54,
                MainHallHalfZ = 64,
                SideRoomOffset = 190,
                SideRoomHalfX = 32,
                SideRoomHalfZ = 36,
                CavePassageLength = 230,
                IncludeArchive = true,
                IncludePuzzle = true,
                IncludeTreasury = true,
                IncludeCaveExit = true,
            };

        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets")))
                dir = dir.Parent;
            Assert.NotNull(dir, "Could not locate project root containing Assets/.");
            return dir.FullName;
        }
    }
}
