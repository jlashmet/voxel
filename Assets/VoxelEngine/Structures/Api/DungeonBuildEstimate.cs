using System;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Conservative expensive-write-equivalent estimate for a designed DungeonPlan. The estimate
    /// follows planned room volume and connection length so topology changes affect admission cost
    /// without pretending bulk voxel writes cost the same as individual authored edits.
    /// </summary>
    public static class DungeonBuildEstimate
    {
        public static long Estimate(DungeonPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (!DungeonPlanValidator.TryValidate(plan, out DungeonPlanIssue issue))
                throw new ArgumentException($"Cannot estimate invalid dungeon plan: {issue}.", nameof(plan));

            double cost = 0.0;
            for (int i = 0; i < plan.Rooms.Length; i++)
            {
                DungeonRoomPlan room = plan.Rooms[i];
                double footprint = room.Size.x * (double)room.Size.z;
                cost += footprint * (4.0 + room.Size.y * 0.20);
                cost += FurnishingAllowance(room.Purpose);
            }

            for (int i = 0; i < plan.Connections.Length; i++)
            {
                DungeonConnectionPlan connection = plan.Connections[i];
                DungeonRoomPlan from = plan.Rooms[connection.FromRoomId];
                DungeonRoomPlan to = plan.Rooms[connection.ToRoomId];
                int3 delta = math.abs(to.Centre - from.Centre);
                if (connection.Kind == DungeonConnectionKind.Stair)
                {
                    cost += math.max(1, delta.y) * math.PI_DBL * 14.0 * 14.0 * 0.30;
                    continue;
                }

                double horizontal = delta.x + (double)delta.z;
                double width = connection.Kind == DungeonConnectionKind.SecretPassage ? 28.0 : 20.0;
                double height = connection.Kind == DungeonConnectionKind.SecretPassage ? 32.0 : 30.0;
                cost += horizontal * width * (4.0 + height * 0.20);
            }

            return (long)Math.Ceiling(cost);
        }

        private static double FurnishingAllowance(DungeonRoomPurpose purpose)
        {
            switch (purpose)
            {
                case DungeonRoomPurpose.Archive: return 55_000.0;
                case DungeonRoomPurpose.GreatHall: return 65_000.0;
                case DungeonRoomPurpose.Puzzle: return 45_000.0;
                case DungeonRoomPurpose.Treasury: return 50_000.0;
                default: return 5_000.0;
            }
        }
    }
}
