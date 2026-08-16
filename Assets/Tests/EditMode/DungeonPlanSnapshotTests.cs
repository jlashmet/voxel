using System;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class DungeonPlanSnapshotTests
    {
        [Test]
        public void SnapshotOwnsIndependentRoomAndConnectionArrays()
        {
            DungeonPlanningConstraints constraints = Constraints();
            DungeonPlan original = DungeonPlanner.Create(401u, in constraints);
            DungeonPlan snapshot = DungeonPlanSnapshot.CloneValidated(original);

            Assert.AreNotSame(original, snapshot);
            Assert.AreNotSame(original.Rooms, snapshot.Rooms);
            Assert.AreNotSame(original.Connections, snapshot.Connections);

            int3 snapshotEntranceCentre = snapshot.Rooms[snapshot.EntranceRoomId].Centre;
            int snapshotConnectionTarget = snapshot.Connections[0].ToRoomId;

            DungeonRoomPlan changedRoom = original.Rooms[original.EntranceRoomId];
            changedRoom.Centre += new int3(999, 0, 999);
            original.Rooms[original.EntranceRoomId] = changedRoom;
            original.Connections[0].ToRoomId = original.Rooms.Length + 10;

            Assert.AreEqual(snapshotEntranceCentre,
                snapshot.Rooms[snapshot.EntranceRoomId].Centre,
                "Caller room mutation leaked into the retained dungeon snapshot.");
            Assert.AreEqual(snapshotConnectionTarget, snapshot.Connections[0].ToRoomId,
                "Caller connection mutation leaked into the retained dungeon snapshot.");
            Assert.IsTrue(
                DungeonPlanValidator.TryValidate(snapshot, out DungeonPlanIssue issue),
                issue.ToString());
        }

        [Test]
        public void SnapshotRejectsInvalidPlanBeforeRetainingIt()
        {
            DungeonPlanningConstraints constraints = Constraints();
            DungeonPlan invalid = DungeonPlanner.Create(409u, in constraints);
            invalid.Connections[0].ToRoomId = invalid.Rooms.Length + 1;

            Assert.Throws<InvalidOperationException>(() =>
                DungeonPlanSnapshot.CloneValidated(invalid));
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
    }
}
