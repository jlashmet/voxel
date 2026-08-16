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

                Assert.IsTrue(TryFind(dungeon, DungeonRoomPurpose.Entrance, out _),
                    $"seed {seed}: dungeon lost mandatory entrance room");
                DungeonRoomPlan hall = Find(dungeon, DungeonRoomPurpose.GreatHall);
                Assert.AreEqual(dungeon.Entrance.y - 166 + 20, hall.Centre.y,
                    $"seed {seed}: main dungeon depth changed");

                bool hasPuzzle = TryFind(dungeon, DungeonRoomPurpose.Puzzle, out DungeonRoomPlan puzzle);
                bool hasTreasury = TryFind(
                    dungeon, DungeonRoomPurpose.Treasury, out DungeonRoomPlan treasury);
                if (hasPuzzle)
                {
                    Assert.AreEqual(226, math.abs(puzzle.Centre.x - dungeon.Entrance.x),
                        $"seed {seed}: puzzle branch left the authored side-room envelope");
                }
                if (hasTreasury)
                {
                    Assert.AreEqual(226, math.abs(treasury.Centre.x - dungeon.Entrance.x),
                        $"seed {seed}: treasury branch left the authored side-room envelope");
                }
                if (hasPuzzle && hasTreasury)
                {
                    Assert.AreEqual(
                        -(puzzle.Centre.x - dungeon.Entrance.x),
                        treasury.Centre.x - dungeon.Entrance.x,
                        $"seed {seed}: simultaneous side branches must stay on opposite sides");
                }

                bool hasCaveRoom = TryFind(
                    dungeon, DungeonRoomPurpose.CaveThreshold, out DungeonRoomPlan cave);
                Assert.AreEqual(dungeon.HasCaveExit, hasCaveRoom,
                    $"seed {seed}: cave-exit metadata disagrees with room graph");
                if (hasCaveRoom)
                {
                    Assert.AreEqual(411, math.abs(cave.Centre.z - dungeon.Entrance.z),
                        $"seed {seed}: cave threshold left the authored underground envelope");
                }
            }
        }

        [Test]
        public void CastleDungeonOptionalBranchesVaryDeterministicallyAcrossSeeds()
        {
            bool sawArchive = false, sawNoArchive = false;
            bool sawPuzzle = false, sawNoPuzzle = false;
            bool sawTreasury = false, sawNoTreasury = false;
            bool sawCave = false, sawNoCave = false;
            int minimumRoomCount = int.MaxValue;
            int maximumRoomCount = int.MinValue;

            for (uint seed = 1; seed <= 256; seed++)
            {
                CastlePlan dimensions = CastlePlanner.Create(int3.zero, seed);
                CastleTopologyPlan topology = CastleLayoutPlanner.Create(seed);
                topology.KeepPlacement = CastleKeepPlacement.Central;
                CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in dimensions, in topology);
                CastleSpatialProjection projection = CastleSpatialProjection.Create(
                    in dimensions, spatial);

                DungeonPlan first = CastleDungeonPlanning.Create(in dimensions, in projection);
                DungeonPlan second = CastleDungeonPlanning.Create(in dimensions, in projection);
                AssertEquivalent(first, second, seed);

                bool archive = TryFind(first, DungeonRoomPurpose.Archive, out _);
                bool puzzle = TryFind(first, DungeonRoomPurpose.Puzzle, out _);
                bool treasury = TryFind(first, DungeonRoomPurpose.Treasury, out _);
                bool cave = first.HasCaveExit;

                sawArchive |= archive;
                sawNoArchive |= !archive;
                sawPuzzle |= puzzle;
                sawNoPuzzle |= !puzzle;
                sawTreasury |= treasury;
                sawNoTreasury |= !treasury;
                sawCave |= cave;
                sawNoCave |= !cave;
                minimumRoomCount = math.min(minimumRoomCount, first.Rooms.Length);
                maximumRoomCount = math.max(maximumRoomCount, first.Rooms.Length);

                Assert.IsTrue(TryFind(first, DungeonRoomPurpose.Entrance, out _),
                    $"seed {seed}: mandatory entrance became optional");
                Assert.IsTrue(TryFind(first, DungeonRoomPurpose.GreatHall, out _),
                    $"seed {seed}: mandatory great hall became optional");
                Assert.IsTrue(
                    DungeonPlanValidator.TryValidate(first, out DungeonPlanIssue issue),
                    $"seed {seed}: optional topology produced invalid dungeon: {issue}");
            }

            Assert.IsTrue(sawArchive && sawNoArchive,
                "Archive choice did not vary across the sampled dungeon seeds.");
            Assert.IsTrue(sawPuzzle && sawNoPuzzle,
                "Puzzle choice did not vary across the sampled dungeon seeds.");
            Assert.IsTrue(sawTreasury && sawNoTreasury,
                "Treasury choice did not vary across the sampled dungeon seeds.");
            Assert.IsTrue(sawCave && sawNoCave,
                "Cave-exit choice did not vary across the sampled dungeon seeds.");
            Assert.Less(minimumRoomCount, maximumRoomCount,
                "Optional dungeon choices did not change the realized room graph size.");
        }

        [Test]
        public void ResolvedCastleCompletionCarriesDungeonGraph()
        {
            for (uint seed = 1; seed <= 64; seed++)
            {
                CastlePlan dimensions = CastlePlanner.Create(int3.zero, seed);
                CastleTopologyPlan topology = CastleLayoutPlanner.Create(seed);
                topology.KeepPlacement = CastleKeepPlacement.Central;
                CastleSpatialPlan raw = CastleSpatialPlanner.Create(in dimensions, in topology);

                Assert.IsNull(raw.Dungeon,
                    $"seed {seed}: core spatial planning should not depend on projected dungeon geometry");

                CastleSpatialPlan completed = CastleSpatialPlanCompletion.CompleteResolved(
                    in dimensions, raw);
                Assert.NotNull(completed.Dungeon,
                    $"seed {seed}: resolved completion did not attach a dungeon graph");

                CastleSpatialProjection projection = CastleSpatialProjection.Create(
                    in dimensions, completed);
                Assert.AreEqual(projection.TrapdoorCentre, completed.Dungeon.Entrance,
                    $"seed {seed}: completed dungeon entrance drifted from projected trapdoor");
                Assert.IsTrue(
                    DungeonPlanValidator.TryValidate(
                        completed.Dungeon, out DungeonPlanIssue issue),
                    $"seed {seed}: completed dungeon invalid: {issue}");
            }
        }

        [Test]
        public void CastleDungeonAdaptersShareOneConstraintPolicy()
        {
            string root = RepoRoot;
            string convenienceAdapter = File.ReadAllText(Path.Combine(
                root, "Assets", "VoxelEngine", "Structures", "Api",
                "CastleDungeonPlanner.cs"));
            string policyAdapter = File.ReadAllText(Path.Combine(
                root, "Assets", "VoxelEngine", "Structures", "Api",
                "CastleDungeonPlanning.cs"));

            StringAssert.Contains("CastleDungeonPlanning.Create", convenienceAdapter);
            StringAssert.DoesNotContain("new DungeonPlanningConstraints", convenienceAdapter,
                "CastleDungeonPlanner must not carry a second copy of castle dungeon dimensions.");
            StringAssert.Contains("new DungeonPlanningConstraints", policyAdapter,
                "CastleDungeonPlanning owns the castle-specific reusable-dungeon constraints.");

            for (uint seed = 1; seed <= 64; seed++)
            {
                CastlePlan dimensions = CastlePlanner.Create(int3.zero, seed);
                CastleTopologyPlan topology = CastleLayoutPlanner.Create(seed);
                topology.KeepPlacement = CastleKeepPlacement.Central;
                CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in dimensions, in topology);
                CastleSpatialProjection projection = CastleSpatialProjection.Create(
                    in dimensions, spatial);

                DungeonPlan viaSpatial = CastleDungeonPlanner.Create(in dimensions, spatial);
                DungeonPlan viaProjection = CastleDungeonPlanning.Create(in dimensions, in projection);
                AssertEquivalent(viaProjection, viaSpatial, seed);
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

        private static void AssertEquivalent(DungeonPlan expected, DungeonPlan actual, uint seed)
        {
            Assert.AreEqual(expected.Seed, actual.Seed, $"seed {seed}: seed drifted");
            Assert.AreEqual(expected.Entrance, actual.Entrance, $"seed {seed}: entrance drifted");
            Assert.AreEqual(expected.EntranceRoomId, actual.EntranceRoomId,
                $"seed {seed}: entrance room id drifted");
            Assert.AreEqual(expected.CaveThresholdRoomId, actual.CaveThresholdRoomId,
                $"seed {seed}: cave threshold id drifted");
            Assert.AreEqual(expected.Rooms.Length, actual.Rooms.Length,
                $"seed {seed}: room count drifted");
            Assert.AreEqual(expected.Connections.Length, actual.Connections.Length,
                $"seed {seed}: connection count drifted");

            for (int i = 0; i < expected.Rooms.Length; i++)
            {
                Assert.AreEqual(expected.Rooms[i].Id, actual.Rooms[i].Id,
                    $"seed {seed}, room {i}: id drifted");
                Assert.AreEqual(expected.Rooms[i].Purpose, actual.Rooms[i].Purpose,
                    $"seed {seed}, room {i}: purpose drifted");
                Assert.AreEqual(expected.Rooms[i].Centre, actual.Rooms[i].Centre,
                    $"seed {seed}, room {i}: centre drifted");
                Assert.AreEqual(expected.Rooms[i].Size, actual.Rooms[i].Size,
                    $"seed {seed}, room {i}: size drifted");
            }

            for (int i = 0; i < expected.Connections.Length; i++)
            {
                Assert.AreEqual(expected.Connections[i].FromRoomId, actual.Connections[i].FromRoomId,
                    $"seed {seed}, connection {i}: source drifted");
                Assert.AreEqual(expected.Connections[i].ToRoomId, actual.Connections[i].ToRoomId,
                    $"seed {seed}, connection {i}: target drifted");
                Assert.AreEqual(expected.Connections[i].Kind, actual.Connections[i].Kind,
                    $"seed {seed}, connection {i}: kind drifted");
            }
        }

        private static DungeonRoomPlan Find(DungeonPlan plan, DungeonRoomPurpose purpose)
        {
            if (TryFind(plan, purpose, out DungeonRoomPlan room))
                return room;
            Assert.Fail($"Missing dungeon room purpose {purpose}.");
            return default;
        }

        private static bool TryFind(
            DungeonPlan plan,
            DungeonRoomPurpose purpose,
            out DungeonRoomPlan room)
        {
            for (int i = 0; i < plan.Rooms.Length; i++)
            {
                if (plan.Rooms[i].Purpose != purpose) continue;
                room = plan.Rooms[i];
                return true;
            }

            room = default;
            return false;
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
