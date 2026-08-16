using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Shared buildability rules for planned castle gates. Planning and validation use the same
    /// minimum edge lengths so a selected semantic opening cannot reach Runtime with too little
    /// curtain wall to contain it.
    /// </summary>
    public static class CastleGatePlanningRules
    {
        public static int PrimaryMinimumEdgeLength(in CastlePlan plan) =>
            math.max(CastleLayout.FrontGateWidth + 12, plan.WallThickness * 2) + 4;

        public static int InnerMinimumEdgeLength(in CastlePlan plan) =>
            math.max(CastleLayout.FrontGateWidth, plan.WallThickness * 2) + 4;

        public static int PosternMinimumEdgeLength(in CastlePlan plan) =>
            math.max(CastleLayout.PosternGateWidth + 4, plan.WallThickness + 8);

        internal static bool EdgeCanHostOpening(
            int2[] perimeter,
            int edgeIndex,
            int minimumLength)
        {
            if (perimeter == null || perimeter.Length < 2 ||
                edgeIndex < 0 || edgeIndex >= perimeter.Length)
                return false;

            int2 a = perimeter[edgeIndex];
            int2 b = perimeter[(edgeIndex + 1) % perimeter.Length];
            long dx = (long)b.x - a.x;
            long dz = (long)b.y - a.y;
            long minimum = minimumLength;
            return dx * dx + dz * dz >= minimum * minimum;
        }
    }
}
