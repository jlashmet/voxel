using System;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Castle-specific constraint adapter for the reusable <see cref="DungeonPlanner"/>. It owns
    /// only the relationship between the placed keep/trapdoor and the historical castle dungeon
    /// envelope; room graph construction remains generic and Runtime performs no planning.
    /// </summary>
    public static class CastleDungeonPlanner
    {
        public static DungeonPlan Create(
            in CastlePlan dimensions,
            CastleSpatialPlan spatial)
        {
            if (spatial == null) throw new ArgumentNullException(nameof(spatial));
            if (spatial.KeepRequiresTerrainResolution)
            {
                throw new InvalidOperationException(
                    "Castle dungeon planning requires a resolved keep placement.");
            }

            CastleSpatialProjection projection = CastleSpatialProjection.Create(
                in dimensions, spatial);
            CastlePlan keepPlan = projection.KeepPlan;
            int3 trapdoor = projection.TrapdoorCentre;

            var constraints = new DungeonPlanningConstraints
            {
                Entrance = trapdoor,
                UpperLevelDrop = 46,
                MainLevelDrop = 166,
                RoomHeight = 40,
                MainHallHalfX = 130,
                MainHallHalfZ = 90,
                SideRoomOffset = 226,
                SideRoomHalfX = 50,
                SideRoomHalfZ = 55,
                CavePassageLength = 321,
                IncludeArchive = true,
                IncludePuzzle = true,
                IncludeTreasury = true,
                IncludeCaveExit = true,
            };

            uint dungeonSeed = CastleSeedPartition.Derive(
                keepPlan.Seed, CastleSeedDomain.Dungeon);
            return DungeonPlanner.Create(dungeonSeed, in constraints);
        }
    }
}
