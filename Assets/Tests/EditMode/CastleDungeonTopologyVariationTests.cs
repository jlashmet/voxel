using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleDungeonTopologyVariationTests
    {
        [Test]
        public void CastleDungeonTopologyIsDeterministicValidAndVariesAcrossSeeds()
        {
            var signatures = new HashSet<string>();
            bool sawArchive = false, omittedArchive = false;
            bool sawPuzzle = false, omittedPuzzle = false;
            bool sawTreasury = false, omittedTreasury = false;
            bool sawCave = false, omittedCave = false;

            for (uint seed = 1; seed <= 512; seed++)
            {
                CastlePlan castle = CastlePlanner.Create(int3.zero, seed);
                int2 keepCentre = int2.zero;
                DungeonPlan first = CastleDungeonPlanning.Create(in castle, keepCentre);
                DungeonPlan second = CastleDungeonPlanning.Create(in castle, keepCentre);

                Assert.IsTrue(
                    DungeonPlanValidator.TryValidate(first, out DungeonPlanIssue issue),
                    $"seed {seed}: {issue}");
                AssertPlansEqual(first, second, seed);
                Assert.IsTrue(HasPurpose(first, DungeonRoomPurpose.Entrance),
                    $"seed {seed}: missing mandatory entrance");
                Assert.IsTrue(HasPurpose(first, DungeonRoomPurpose.GreatHall),
                    $"seed {seed}: missing mandatory great hall");

                bool archive = HasPurpose(first, DungeonRoomPurpose.Archive);
                bool puzzle = HasPurpose(first, DungeonRoomPurpose.Puzzle);
                bool treasury = HasPurpose(first, DungeonRoomPurpose.Treasury);
                bool cave = HasPurpose(first, DungeonRoomPurpose.CaveThreshold);
                signatures.Add($"{archive}:{puzzle}:{treasury}:{cave}");

                sawArchive |= archive; omittedArchive |= !archive;
                sawPuzzle |= puzzle; omittedPuzzle |= !puzzle;
                sawTreasury |= treasury; omittedTreasury |= !treasury;
                sawCave |= cave; omittedCave |= !cave;
            }

            Assert.Greater(signatures.Count, 1,
                "Castle dungeon seeds should produce more than one semantic graph shape.");
            Assert.IsTrue(sawArchive && omittedArchive, "Archive choice never varied.");
            Assert.IsTrue(sawPuzzle && omittedPuzzle, "Puzzle choice never varied.");
            Assert.IsTrue(sawTreasury && omittedTreasury, "Treasury choice never varied.");
            Assert.IsTrue(sawCave && omittedCave, "Cave-exit choice never varied.");
        }

        private static bool HasPurpose(DungeonPlan plan, DungeonRoomPurpose purpose)
        {
            for (int i = 0; i < plan.Rooms.Length; i++)
                if (plan.Rooms[i].Purpose == purpose) return true;
            return false;
        }

        private static void AssertPlansEqual(DungeonPlan first, DungeonPlan second, uint seed)
        {
            Assert.AreEqual(first.Seed, second.Seed, $"seed {seed}: root seed");
            Assert.AreEqual(first.Entrance, second.Entrance, $"seed {seed}: entrance");
            Assert.AreEqual(first.EntranceRoomId, second.EntranceRoomId,
                $"seed {seed}: entrance room");
            Assert.AreEqual(first.CaveThresholdRoomId, second.CaveThresholdRoomId,
                $"seed {seed}: cave room");
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
