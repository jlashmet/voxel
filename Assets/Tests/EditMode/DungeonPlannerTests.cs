using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class DungeonPlannerTests
    {
        [Test]
        public void PlannerIsDeterministicAndStructurallyValidAcrossSeeds()
        {
            DungeonPlanningConstraints constraints = CastleScaleConstraints();
            for (uint seed = 1; seed <= 256; seed++)
            {
                DungeonPlan first = DungeonPlanner.Create(seed, in constraints);
                DungeonPlan second = DungeonPlanner.Create(seed, in constraints);

                Assert.IsTrue(
                    DungeonPlanValidator.TryValidate(first, out DungeonPlanIssue issue),
                    $"seed {seed}: {issue}");
                AssertPlansEqual(first, second, seed);
                Assert.AreEqual(DungeonRoomPurpose.Entrance,
                    first.Rooms[first.EntranceRoomId].Purpose);
                Assert.GreaterOrEqual(
                    first.Rooms[first.EntranceRoomId].Size.x,
                    DungeonConnectionGeometry.StairShaftDiameter,
                    $"seed {seed}: entrance is narrower than its planned stair shaft");
                Assert.GreaterOrEqual(
                    first.Rooms[first.EntranceRoomId].Size.z,
                    DungeonConnectionGeometry.StairShaftDiameter,
                    $"seed {seed}: entrance is shallower than its planned stair shaft");
                Assert.IsTrue(first.HasCaveExit);
                Assert.AreEqual(DungeonRoomPurpose.CaveThreshold,
                    first.Rooms[first.CaveThresholdRoomId].Purpose);
            }
        }

        [Test]
        public void IndependentSeedsVaryBranchAndCavePlacement()
        {
            DungeonPlanningConstraints constraints = CastleScaleConstraints();
            var signatures = new HashSet<string>();

            for (uint seed = 1; seed <= 128; seed++)
            {
                DungeonPlan plan = DungeonPlanner.Create(seed, in constraints);
                DungeonRoomPlan puzzle = Find(plan, DungeonRoomPurpose.Puzzle);
                DungeonRoomPlan treasury = Find(plan, DungeonRoomPurpose.Treasury);
                DungeonRoomPlan cave = Find(plan, DungeonRoomPurpose.CaveThreshold);
                signatures.Add($"{math.sign(puzzle.Centre.x - constraints.Entrance.x)}:" +
                               $"{math.sign(treasury.Centre.x - constraints.Entrance.x)}:" +
                               $"{math.sign(cave.Centre.z - constraints.Entrance.z)}");
            }

            Assert.Greater(signatures.Count, 1,
                "Dungeon seed should vary branch assignment or cave direction.");
        }

        [Test]
        public void PlannerCanOmitOptionalSemanticRooms()
        {
            DungeonPlanningConstraints constraints = CastleScaleConstraints();
            constraints.IncludeArchive = false;
            constraints.IncludePuzzle = false;
            constraints.IncludeTreasury = false;
            constraints.IncludeCaveExit = false;

            DungeonPlan plan = DungeonPlanner.Create(19u, in constraints);

            Assert.AreEqual(2, plan.Rooms.Length);
            Assert.AreEqual(DungeonRoomPurpose.Entrance, plan.Rooms[0].Purpose);
            Assert.AreEqual(DungeonRoomPurpose.GreatHall, plan.Rooms[1].Purpose);
            Assert.IsFalse(plan.HasCaveExit);
            Assert.IsTrue(
                DungeonPlanValidator.TryValidate(plan, out DungeonPlanIssue issue),
                issue.ToString());
        }

        [Test]
        public void ValidatorRejectsBrokenConnectionEndpoint()
        {
            DungeonPlanningConstraints constraints = CastleScaleConstraints();
            DungeonPlan plan = DungeonPlanner.Create(23u, in constraints);
            plan.Connections[0].ToRoomId = plan.Rooms.Length + 7;

            Assert.IsFalse(
                DungeonPlanValidator.TryValidate(plan, out DungeonPlanIssue issue));
            Assert.AreEqual(DungeonPlanIssue.InvalidConnectionEndpoint, issue);
        }

        [Test]
        public void ValidatorRejectsOverlappingRooms()
        {
            DungeonPlanningConstraints constraints = CastleScaleConstraints();
            DungeonPlan plan = DungeonPlanner.Create(29u, in constraints);
            int puzzle = FindIndex(plan, DungeonRoomPurpose.Puzzle);
            int treasury = FindIndex(plan, DungeonRoomPurpose.Treasury);
            plan.Rooms[treasury].Centre = plan.Rooms[puzzle].Centre;

            Assert.IsFalse(
                DungeonPlanValidator.TryValidate(plan, out DungeonPlanIssue issue));
            Assert.AreEqual(DungeonPlanIssue.OverlappingRooms, issue);
        }

        [Test]
        public void ValidatorRejectsHorizontalConnectionAcrossDifferentFloors()
        {
            DungeonPlanningConstraints constraints = CastleScaleConstraints();
            DungeonPlan plan = DungeonPlanner.Create(31u, in constraints);
            int puzzle = FindIndex(plan, DungeonRoomPurpose.Puzzle);
            plan.Rooms[puzzle].Centre += new int3(0, 8, 0);

            Assert.IsFalse(
                DungeonPlanValidator.TryValidate(plan, out DungeonPlanIssue issue));
            Assert.AreEqual(DungeonPlanIssue.InvalidConnectionGeometry, issue);
        }

        [Test]
        public void ValidatorRejectsStairWithoutSharedShaftFootprint()
        {
            DungeonPlanningConstraints constraints = CastleScaleConstraints();
            DungeonPlan plan = DungeonPlanner.Create(37u, in constraints);
            int archive = FindIndex(plan, DungeonRoomPurpose.Archive);
            plan.Rooms[archive].Centre += new int3(400, 0, 0);

            Assert.IsFalse(
                DungeonPlanValidator.TryValidate(plan, out DungeonPlanIssue issue));
            Assert.AreEqual(DungeonPlanIssue.InvalidConnectionGeometry, issue);
        }

        [Test]
        public void ValidatorRejectsUnknownConnectionKind()
        {
            DungeonPlanningConstraints constraints = CastleScaleConstraints();
            DungeonPlan plan = DungeonPlanner.Create(43u, in constraints);
            plan.Connections[0].Kind = (DungeonConnectionKind)255;

            Assert.IsFalse(
                DungeonPlanValidator.TryValidate(plan, out DungeonPlanIssue issue));
            Assert.AreEqual(DungeonPlanIssue.InvalidConnectionKind, issue);
        }

        private static DungeonPlanningConstraints CastleScaleConstraints() =>
            new DungeonPlanningConstraints
            {
                Entrance = new int3(320, 420, 500),
                UpperLevelDrop = 46,
                MainLevelDrop = 166,
                RoomHeight = 40,
                MainHallHalfX = 130,
                MainHallHalfZ = 90,
                SideRoomOffset = 226,
                SideRoomHalfX = 50,
                SideRoomHalfZ = 58,
                CavePassageLength = 320,
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

        private static void AssertPlansEqual(DungeonPlan first, DungeonPlan second, uint seed)
        {
            Assert.AreEqual(first.Seed, second.Seed, $"seed {seed}: root seed");
            Assert.AreEqual(first.Entrance, second.Entrance, $"seed {seed}: entrance");
            Assert.AreEqual(first.EntranceRoomId, second.EntranceRoomId,
                $"seed {seed}: entrance id");
            Assert.AreEqual(first.CaveThresholdRoomId, second.CaveThresholdRoomId,
                $"seed {seed}: cave id");
            Assert.AreEqual(first.Rooms.Length, second.Rooms.Length,
                $"seed {seed}: room count");
            Assert.AreEqual(first.Connections.Length, second.Connections.Length,
                $"seed {seed}: connection count");

            for (int i = 0; i < first.Rooms.Length; i++)
            {
                Assert.AreEqual(first.Rooms[i].Id, second.Rooms[i].Id,
                    $"seed {seed}: room {i} id");
                Assert.AreEqual(first.Rooms[i].Purpose, second.Rooms[i].Purpose,
                    $"seed {seed}: room {i} purpose");
                Assert.AreEqual(first.Rooms[i].Centre, second.Rooms[i].Centre,
                    $"seed {seed}: room {i} centre");
                Assert.AreEqual(first.Rooms[i].Size, second.Rooms[i].Size,
                    $"seed {seed}: room {i} size");
            }

            for (int i = 0; i < first.Connections.Length; i++)
            {
                Assert.AreEqual(first.Connections[i].FromRoomId, second.Connections[i].FromRoomId,
                    $"seed {seed}: connection {i} from");
                Assert.AreEqual(first.Connections[i].ToRoomId, second.Connections[i].ToRoomId,
                    $"seed {seed}: connection {i} to");
                Assert.AreEqual(first.Connections[i].Kind, second.Connections[i].Kind,
                    $"seed {seed}: connection {i} kind");
            }
        }
    }
}
