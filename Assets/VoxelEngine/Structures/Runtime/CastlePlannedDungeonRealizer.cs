using System;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Castle composition for a preplanned designed dungeon plus its natural cave continuation.
    /// Generic realization owns room shells/circulation, semantic furnishing remains reusable by
    /// room purpose, and this adapter only joins the planned CaveThreshold to the castle cave.
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
            if (!dungeonPlan.HasCaveExit)
                return;

            DungeonRoomPlan threshold = dungeonPlan.Rooms[dungeonPlan.CaveThresholdRoomId];
            int3 caveOrigin = new int3(
                threshold.Centre.x,
                threshold.Centre.y - threshold.Size.y / 2,
                threshold.Centre.z);
            CastleCaveRealizer.Build(ref brush, in keepPlan, caveOrigin);
        }
    }
}
