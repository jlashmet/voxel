using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Chooses a player spawn column from the realized castle approach instead of a fixed world
    /// axis. The result lies beyond the complete authored castle envelope while retaining an
    /// oblique tangent offset so the first view does not collapse into a symmetric gate elevation.
    /// </summary>
    internal static class ShowcaseCastleSpawnPlanner
    {
        private const float BuildClearance = 96f;
        private const float MinimumTangentOffset = 120f;
        private const float MaximumTangentOffset = 190f;

        internal static int2 PlanColumn(
            in CastlePlan plan,
            in CastleSpatialProjection projection,
            in CastleBuildBounds bounds)
        {
            CastleApproachFrame approach = projection.Approach;
            float2 gateWorld = new float2(
                plan.Centre.x + approach.GateCentre.x,
                plan.Centre.z + approach.GateCentre.y);

            int maxX = bounds.MaxExclusive.x - 1;
            int maxZ = bounds.MaxExclusive.z - 1;
            float farthestOutward = 0f;
            Include(
                new float2(bounds.Min.x, bounds.Min.z),
                gateWorld,
                approach.Outward,
                ref farthestOutward);
            Include(
                new float2(maxX, bounds.Min.z),
                gateWorld,
                approach.Outward,
                ref farthestOutward);
            Include(
                new float2(bounds.Min.x, maxZ),
                gateWorld,
                approach.Outward,
                ref farthestOutward);
            Include(
                new float2(maxX, maxZ),
                gateWorld,
                approach.Outward,
                ref farthestOutward);

            float tangentOffset = math.clamp(
                math.max(plan.KeepHalfX, plan.BaileyHalfX) * 0.5f,
                MinimumTangentOffset,
                MaximumTangentOffset);
            int2 local = approach.LocalPoint(
                tangentOffset,
                math.max(0f, farthestOutward) + BuildClearance);
            return new int2(plan.Centre.x + local.x, plan.Centre.z + local.y);
        }

        private static void Include(
            float2 worldPoint,
            float2 gateWorld,
            float2 outward,
            ref float farthestOutward)
        {
            farthestOutward = math.max(
                farthestOutward,
                math.dot(worldPoint - gateWorld, outward));
        }
    }
}
