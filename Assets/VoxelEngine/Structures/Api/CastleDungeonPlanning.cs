using Unity.Mathematics;

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
            in CastleSpatialProjection projection) =>
            CreateAtEntrance(in plan, projection.TrapdoorCentre);

        /// <summary>
        /// Creates the same castle-owned dungeon directly from the semantic keep centre. Validation
        /// uses this overload so it can verify an attached dungeon without calling
        /// CastleSpatialProjection.Create and recursing back into CastleSpatialPlanValidator.
        /// </summary>
        public static DungeonPlan Create(
            in CastlePlan plan,
            int2 localKeepCentre) =>
            CreateAtEntrance(in plan, EntranceForKeep(in plan, localKeepCentre));

        /// <summary>World-space trapdoor/dungeon entrance implied by a semantic keep centre.</summary>
        public static int3 EntranceForKeep(
            in CastlePlan plan,
            int2 localKeepCentre)
        {
            int baseY = plan.Centre.y + plan.PlateauHeight;
            return new int3(
                plan.Centre.x + localKeepCentre.x,
                baseY,
                plan.Centre.z + localKeepCentre.y + 40);
        }

        private static DungeonPlan CreateAtEntrance(
            in CastlePlan plan,
            int3 entrance)
        {
            var constraints = new DungeonPlanningConstraints
            {
                Entrance = entrance,
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
