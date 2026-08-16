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
        // Stable semantic choice ids. Each optional branch owns an independent dungeon sub-seed so
        // adding another choice later cannot perturb which existing rooms a castle receives.
        private const uint ArchiveChoice = 0x41524348u;   // ARCH
        private const uint PuzzleChoice = 0x50555A5Au;    // PUZZ
        private const uint TreasuryChoice = 0x54524541u;  // TREA
        private const uint CaveChoice = 0x43415645u;      // CAVE

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
                IncludeArchive = ChooseOptional(plan.Seed, ArchiveChoice, 75),
                IncludePuzzle = ChooseOptional(plan.Seed, PuzzleChoice, 70),
                IncludeTreasury = ChooseOptional(plan.Seed, TreasuryChoice, 60),
                IncludeCaveExit = ChooseOptional(plan.Seed, CaveChoice, 80),
            };

            uint dungeonSeed = CastleSeedPartition.Derive(
                plan.Seed, CastleSeedDomain.Dungeon);
            return DungeonPlanner.Create(dungeonSeed, in constraints);
        }

        private static bool ChooseOptional(uint castleSeed, uint choiceId, uint percent)
        {
            uint choiceSeed = CastleSeedPartition.Derive(
                castleSeed, CastleSeedDomain.Dungeon, choiceId);
            return choiceSeed % 100u < percent;
        }
    }
}
