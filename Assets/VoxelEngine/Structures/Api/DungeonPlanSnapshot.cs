using System;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Produces an isolated, validated DungeonPlan copy for trust-boundary handoffs. DungeonPlan
    /// exposes mutable arrays for lightweight planning/tests, so Runtime must not retain caller-owned
    /// room or connection arrays after preflight.
    /// </summary>
    public static class DungeonPlanSnapshot
    {
        public static DungeonPlan CloneValidated(DungeonPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (!DungeonPlanValidator.TryValidate(plan, out DungeonPlanIssue issue))
                throw new InvalidOperationException($"Cannot snapshot invalid dungeon plan: {issue}.");

            return new DungeonPlan(
                plan.Seed,
                plan.Entrance,
                (DungeonRoomPlan[])plan.Rooms.Clone(),
                (DungeonConnectionPlan[])plan.Connections.Clone(),
                plan.EntranceRoomId,
                plan.CaveThresholdRoomId);
        }
    }
}
