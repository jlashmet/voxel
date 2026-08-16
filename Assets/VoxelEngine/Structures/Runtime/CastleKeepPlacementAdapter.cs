using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Legacy compatibility shim retained for old Runtime callers. Spatial castle realization uses
    /// CastleSpatialProjection directly; this shim shares the single API-owned migration offset.
    /// </summary>
    internal static class CastleKeepPlacementAdapter
    {
        internal const int LegacyKeepCentreZOffset = CastleLayout.LegacyKeepCentreZOffset;

        internal static CastlePlan Place(in CastlePlan plan, int2 localKeepCentre)
        {
            CastlePlan placed = plan;
            placed.Centre = new int3(
                plan.Centre.x + localKeepCentre.x,
                plan.Centre.y,
                plan.Centre.z + localKeepCentre.y - LegacyKeepCentreZOffset);
            return placed;
        }

        internal static int2 ActualKeepCentre(in CastlePlan placedPlan) =>
            new int2(
                placedPlan.Centre.x,
                placedPlan.Centre.z + LegacyKeepCentreZOffset);
    }
}
