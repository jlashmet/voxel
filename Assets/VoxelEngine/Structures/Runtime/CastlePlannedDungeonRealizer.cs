using System;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Castle composition for a preplanned designed dungeon plus its natural cave continuation.
    /// Generic realization owns room shells/circulation, semantic furnishing remains reusable by
    /// room purpose, and this adapter joins castle-specific moving architecture and the planned
    /// CaveThreshold to the natural-cave realizer.
    /// </summary>
    internal static class CastlePlannedDungeonRealizer
    {
        internal static void Build(
            ref VoxelBrush brush,
            in CastlePlan keepPlan,
            DungeonPlan dungeonPlan)
        {
            if (dungeonPlan == null) throw new ArgumentNullException(nameof(dungeonPlan));
            if (!DungeonPlanValidator.TryValidate(dungeonPlan, out DungeonPlanIssue issue))
            {
                throw new InvalidOperationException(
                    $"Cannot realize invalid castle dungeon plan: {issue}.");
            }

            DungeonRealizer.Build(ref brush, dungeonPlan);
            DungeonRoomFurnisher.FurnishAll(ref brush, dungeonPlan);
            BuildTrapdoor(ref brush, dungeonPlan.Entrance);

            if (!dungeonPlan.HasCaveExit)
                return;

            DungeonRoomPlan threshold = dungeonPlan.Rooms[dungeonPlan.CaveThresholdRoomId];
            int3 caveOrigin = new int3(
                threshold.Centre.x,
                threshold.Centre.y - threshold.Size.y / 2,
                threshold.Centre.z);
            CastleCaveRealizer.Build(ref brush, in keepPlan, caveOrigin);
        }

        private static void BuildTrapdoor(ref VoxelBrush brush, int3 centre)
        {
            int half = CastleLayout.TrapdoorHalfSize;
            brush.Box(
                new int3(centre.x - half, centre.y, centre.z - half),
                new int3(half * 2, 2, half * 2),
                Mat.Wood);
            brush.Box(
                new int3(centre.x - half, centre.y + 2, centre.z - half),
                new int3(3, 2, half * 2),
                Mat.Gold);
            brush.Box(
                new int3(centre.x + half - 3, centre.y + 2, centre.z - half),
                new int3(3, 2, half * 2),
                Mat.Gold);
        }
    }
}
