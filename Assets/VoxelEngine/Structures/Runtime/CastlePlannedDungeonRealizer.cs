using System;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Castle composition for a preplanned designed dungeon plus its natural cave continuation.
    /// Generic realization owns room shells/circulation, semantic furnishing remains reusable by
    /// room purpose, and this adapter joins castle-specific moving architecture to planned natural
    /// space without choosing topology during realization.
    /// </summary>
    internal static class CastlePlannedDungeonRealizer
    {
        /// <summary>
        /// Compatibility overload retained while callers migrate to the explicit CavePlan handoff.
        /// It preserves the historical fixed castle-cave recipe only for those legacy callers.
        /// </summary>
        internal static void Build(
            ref VoxelBrush brush,
            in CastlePlan keepPlan,
            DungeonPlan dungeonPlan)
        {
            ValidateDungeon(dungeonPlan);
            BuildDesigned(ref brush, dungeonPlan);

            if (!dungeonPlan.HasCaveExit)
                return;

            DungeonRoomPlan threshold = dungeonPlan.Rooms[dungeonPlan.CaveThresholdRoomId];
            int3 caveOrigin = CaveEntrance(in threshold);
            CastleCaveRealizer.Build(ref brush, in keepPlan, caveOrigin);
        }

        /// <summary>
        /// Runtime path for a fully completed spatial castle. Both designed and natural topology
        /// are supplied by planning; this overload contains no cave-planning or castle-scale choice.
        /// </summary>
        internal static void Build(
            ref VoxelBrush brush,
            DungeonPlan dungeonPlan,
            CavePlan cavePlan)
        {
            ValidateDungeon(dungeonPlan);
            BuildDesigned(ref brush, dungeonPlan);

            if (!dungeonPlan.HasCaveExit)
            {
                if (cavePlan != null)
                    throw new InvalidOperationException(
                        "Castle dungeon has no cave threshold but a natural cave plan was supplied.");
                return;
            }

            if (cavePlan == null)
                throw new InvalidOperationException(
                    "Castle dungeon has a cave threshold but no natural cave plan was supplied.");
            if (!CavePlanValidator.TryValidate(cavePlan, out CavePlanIssue caveIssue))
            {
                throw new InvalidOperationException(
                    $"Cannot realize invalid castle cave plan: {caveIssue}.");
            }

            DungeonRoomPlan threshold = dungeonPlan.Rooms[dungeonPlan.CaveThresholdRoomId];
            int3 expectedEntrance = CaveEntrance(in threshold);
            if (!cavePlan.Entrance.Equals(expectedEntrance))
            {
                throw new InvalidOperationException(
                    "Castle natural cave entrance does not align with the designed cave threshold.");
            }

            CaveRealizer.Build(ref brush, cavePlan);
        }

        private static void ValidateDungeon(DungeonPlan dungeonPlan)
        {
            if (dungeonPlan == null) throw new ArgumentNullException(nameof(dungeonPlan));
            if (!DungeonPlanValidator.TryValidate(dungeonPlan, out DungeonPlanIssue issue))
            {
                throw new InvalidOperationException(
                    $"Cannot realize invalid castle dungeon plan: {issue}.");
            }
        }

        private static void BuildDesigned(ref VoxelBrush brush, DungeonPlan dungeonPlan)
        {
            DungeonRealizer.Build(ref brush, dungeonPlan);
            DungeonRoomFurnisher.FurnishAll(ref brush, dungeonPlan);
            BuildTrapdoor(ref brush, dungeonPlan.Entrance);
        }

        private static int3 CaveEntrance(in DungeonRoomPlan threshold) =>
            new int3(
                threshold.Centre.x,
                threshold.Centre.y - threshold.Size.y / 2,
                threshold.Centre.z);

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
