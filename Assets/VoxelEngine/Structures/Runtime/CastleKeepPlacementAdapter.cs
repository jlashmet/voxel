using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    internal static class CastleKeepPlacementAdapter
    {
        internal const int LegacyKeepCentreZOffset = CastleSpatialLayoutProjection.LegacyKeepCentreZOffset;
        internal static CastlePlan Place(in CastlePlan plan, int2 localKeepCentre) => CastleSpatialLayoutProjection.PlaceKeepPlan(in plan, localKeepCentre);
        internal static int2 ActualKeepCentre(in CastlePlan placedPlan) => CastleSpatialLayoutProjection.ActualKeepCentre(in placedPlan);
    }
}
