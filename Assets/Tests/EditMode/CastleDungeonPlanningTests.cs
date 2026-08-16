using System.IO;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleDungeonPlanningTests
    {
        [Test]
        public void CastleAdapterAnchorsReusableDungeonAtProjectedTrapdoor()
        {
            for (uint seed = 1; seed <= 64; seed++)
            {
                CastlePlan dimensions = CastlePlanner.Create(int3.zero, seed);
                CastleTopologyPlan topology = CastleLayoutPlanner.Create(seed);
                topology.KeepPlacement = CastleKeepPlacement.Central;
                CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in dimensions, in topology);
                CastleSpatialProjection projection = CastleSpatialProjection.Create(
                    in dimensions, spatial);

                DungeonPlan dungeon = CastleDungeonPlanning.Create(in dimensions, in projection);

                Assert.AreEqual(projection.TrapdoorCentre, dungeon.Entrance,
                    $"seed {seed}: dungeon entrance drifted from trapdoor");
                Assert.AreEqual(
                    CastleSeedPartition.Derive(dimensions.Seed, CastleSeedDomain.Dungeon),
                    dungeon.Seed,
                    $"seed {seed}: dungeon did not use its independent seed domain");
                Assert.IsTrue(
                    DungeonPlanValidator.TryValidate(dungeon, out DungeonPlanIssue issue),
                    $"seed {seed}: {issue}");

                DungeonRoomPlan hall = Find(dungeon, DungeonRoomPurpose.GreatHall);
                Assert.AreEqual(dungeon.Entrance.y - 166 + 20, hall.Centre.y,
                    $"seed {seed}: main dungeon depth changed");

                DungeonRoomPlan puzzle = Find(dungeon, DungeonRoomPurpose.Puzzle);
                DungeonRoomPlan treasury = Find(dungeon, DungeonRoomPurpose.Treasury);
                Assert.AreEqual(226, math.abs(puzzle.Centre.x - dungeon.Entrance.x));
                Assert.AreEqual(226, math.abs(treasury.Centre.x - dungeon.Entrance.x));
                Assert.AreEqual(
                    -(puzzle.Centre.x - dungeon.Entrance.x),
                    treasury.Centre.x - dungeon.Entrance.x,
                    $"seed {seed}: side branches must stay on opposite sides");

                DungeonRoomPlan cave = Find(dungeon, DungeonRoomPurpose.CaveThreshold);
                Assert.AreEqual(411, math.abs(cave.Centre.z - dungeon.Entrance.z),
                    $"seed {seed}: cave threshold left the authored underground envelope");
            }
        }

        [Test]
        public void DungeonPlanningRemainsOutsideRuntime()
        {
            string root = RepoRoot;
            string adapter = File.ReadAllText(Path.Combine(
                root, "Assets", "VoxelEngine", "Structures", "Api",
                "CastleDungeonPlanning.cs"));
            StringAssert.Contains("DungeonPlanner.Create", adapter);

            string runtimeDirectory = Path.Combine(
                root, "Assets", "VoxelEngine", "Structures", "Runtime");
            foreach (string file in Directory.GetFiles(runtimeDirectory, "*.cs"))
            {
                string source = File.ReadAllText(file);
                StringAssert.DoesNotContain("DungeonPlanner.Create", source,
                    $"{Path.GetFileName(file)} must consume a DungeonPlan rather than create one.");
            }
        }

        private static DungeonRoomPlan Find(DungeonPlan plan, DungeonRoomPurpose purpose)
        {
            for (int i = 0; i < plan.Rooms.Length; i++)
                if (plan.Rooms[i].Purpose == purpose) return plan.Rooms[i];
            Assert.Fail($"Missing dungeon room purpose {purpose}.");
            return default;
        }

        private static string RepoRoot
        {
            get
            {
                var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
                while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets")))
                    dir = dir.Parent;
                Assert.NotNull(dir, "Could not locate project root containing Assets/.");
                return dir.FullName;
            }
        }
    }
}
