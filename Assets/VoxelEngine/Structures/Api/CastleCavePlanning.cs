using System;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Castle-specific adapter from the designed dungeon threshold into the reusable natural-cave
    /// planner. It owns only the castle scale envelope and seed partition; CavePlanner owns cave
    /// topology and Runtime owns eventual voxel realization.
    /// </summary>
    public static class CastleCavePlanning
    {
        public static CavePlan Create(in CastlePlan castle, DungeonPlan dungeon)
        {
            if (dungeon == null) throw new ArgumentNullException(nameof(dungeon));
            if (!DungeonPlanValidator.TryValidate(dungeon, out DungeonPlanIssue dungeonIssue))
                throw new InvalidOperationException($"Cannot plan cave from invalid dungeon: {dungeonIssue}.");
            if (!dungeon.HasCaveExit)
                throw new InvalidOperationException("Dungeon has no cave threshold to continue from.");

            DungeonRoomPlan threshold = dungeon.Rooms[dungeon.CaveThresholdRoomId];
            int3 entrance = new int3(
                threshold.Centre.x,
                threshold.Centre.y - threshold.Size.y / 2,
                threshold.Centre.z);
            var constraints = new CavePlanningConstraints
            {
                Entrance = entrance,
                // Preserve the historical main-cavern scale while moving chamber topology into
                // planning. The old castle cave centred its first lobe 27 voxels above the threshold.
                EntranceToMainOffset = new int3(0, 27, 0),
                MainRadii = new int3(82, 36, 104),
                SecondaryChamberCount = 4,
                SecondaryMinRadii = new int3(31, 24, 34),
                SecondaryMaxRadii = new int3(60, 37, 76),
                MinimumHorizontalSpread = 54,
                MaximumHorizontalSpread = 142,
                VerticalSpread = 18,
                PassageWidth = 20,
                PassageHeight = 30,
            };
            uint caveSeed = CastleSeedPartition.Derive(
                castle.Seed, CastleSeedDomain.Cave);
            return CavePlanner.Create(caveSeed, in constraints);
        }
    }
}
