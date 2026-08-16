using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class DungeonEntranceContractTests
    {
        [Test]
        public void ValidatorRejectsEntranceMetadataThatDriftsFromEntranceRoom()
        {
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
            DungeonPlan plan = DungeonPlanner.Create(67u, in constraints);
            DungeonRoomPlan entrance = plan.Rooms[plan.EntranceRoomId];
            entrance.Centre.x += 1;
            plan.Rooms[plan.EntranceRoomId] = entrance;

            Assert.IsFalse(DungeonPlanValidator.TryValidate(plan, out DungeonPlanIssue issue));
            Assert.AreEqual(DungeonPlanIssue.EntrancePlacementMismatch, issue);
        }
    }
}
