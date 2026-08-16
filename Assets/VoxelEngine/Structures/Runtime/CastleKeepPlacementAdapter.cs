using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    internal static class CastleKeepPlacementAdapter
    {
        internal const int LegacyKeepCentreZOffset = 60;
        internal static CastlePlan Place(in CastlePlan plan, int2 localKeepCentre)
        {
            CastlePlan placed = plan;
            placed.Centre = new int3(plan.Centre.x + localKeepCentre.x, plan.Centre.y, plan.Centre.z + localKeepCentre.y - LegacyKeepCentreZOffset);
            return placed;
        }
        internal static int2 ActualKeepCentre(in CastlePlan placedPlan) => new int2(placedPlan.Centre.x, placedPlan.Centre.z + LegacyKeepCentreZOffset);
    }
}
