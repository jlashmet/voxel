using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Temporary Runtime compatibility wrapper. The historical keep anchor transform now belongs
    /// to Structures.Api so presentation/interaction code can project the same spatial keep plan.
    /// </summary>
    internal static class CastleKeepPlacementAdapter
    {
        internal const int LegacyKeepCentreZOffset = CastleSpatialProjection.LegacyKeepCentreZOffset;

        internal static CastlePlan Place(in CastlePlan plan, int2 localKeepCentre) =>
            CastleSpatialProjection.ProjectKeepPlan(in plan, localKeepCentre);

        internal static int2 ActualKeepCentre(in CastlePlan placedPlan) =>
            CastleSpatialProjection.ActualKeepCentre(in placedPlan);
    }
}
