using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Temporary Runtime compatibility shim for keep-local legacy realizers. The authoritative
    /// spatial projection is owned by Structures.Api so presentation and realization share one
    /// interpretation of CastleSpatialPlan.KeepCentre.
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
