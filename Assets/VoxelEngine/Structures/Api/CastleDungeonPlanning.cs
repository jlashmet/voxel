namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Castle-owned constraints adapter for the reusable DungeonPlanner. This preserves the current
    /// underground scale while moving room identity, connectivity, and seeded branch placement out
    /// of CastleDungeonRealizer.
    /// </summary>
    public static class CastleDungeonPlanning
    {
        public static DungeonPlan Create(
            in CastlePlan plan,
            in CastleSpatialProjection projection)
        {
            var constraints = new DungeonPlanningConstraints
            {
                Entrance = projection.TrapdoorCentre,
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

            uint dungeonSeed = CastleSeedPartition.Derive(
                plan.Seed, CastleSeedDomain.Dungeon);
            return DungeonPlanner.Create(dungeonSeed, in constraints);
        }
    }
}
