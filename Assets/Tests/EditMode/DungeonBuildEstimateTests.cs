using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class DungeonBuildEstimateTests
    {
        [Test]
        public void EstimateGrowsWithOptionalRoomsAndConnections()
        {
            DungeonPlanningConstraints minimal = Constraints();
            minimal.IncludeArchive = false;
            minimal.IncludePuzzle = false;
            minimal.IncludeTreasury = false;
            minimal.IncludeCaveExit = false;

            DungeonPlanningConstraints full = Constraints();
            DungeonPlan minimalPlan = DungeonPlanner.Create(7u, in minimal);
            DungeonPlan fullPlan = DungeonPlanner.Create(7u, in full);

            long minimalCost = DungeonBuildEstimate.Estimate(minimalPlan);
            long fullCost = DungeonBuildEstimate.Estimate(fullPlan);

            Assert.Greater(minimalCost, 0);
            Assert.Greater(fullCost, minimalCost);
            Assert.AreEqual(fullCost, DungeonBuildEstimate.Estimate(fullPlan),
                "Estimate must be a pure function of the prepared dungeon plan.");
        }

        [Test]
        public void LongerCavePassageRaisesEstimate()
        {
            DungeonPlanningConstraints shortPassage = Constraints();
            shortPassage.CavePassageLength = 80;
            DungeonPlanningConstraints longPassage = Constraints();
            longPassage.CavePassageLength = 360;

            long shortCost = DungeonBuildEstimate.Estimate(
                DungeonPlanner.Create(11u, in shortPassage));
            long longCost = DungeonBuildEstimate.Estimate(
                DungeonPlanner.Create(11u, in longPassage));

            Assert.Greater(longCost, shortCost);
        }

        private static DungeonPlanningConstraints Constraints() =>
            new DungeonPlanningConstraints
            {
                Entrance = new int3(300, 420, 500),
                UpperLevelDrop = 46,
                MainLevelDrop = 166,
                RoomHeight = 40,
                MainHallHalfX = 130,
                MainHallHalfZ = 90,
                SideRoomOffset = 226,
                SideRoomHalfX = 50,
                SideRoomHalfZ = 58,
                CavePassageLength = 321,
                IncludeArchive = true,
                IncludePuzzle = true,
                IncludeTreasury = true,
                IncludeCaveExit = true,
            };
    }
}
