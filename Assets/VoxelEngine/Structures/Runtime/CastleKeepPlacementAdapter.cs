using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Temporary Runtime compatibility shim for legacy callers that still name this adapter.
    /// The spatial-to-legacy keep projection is API-owned so Runtime, presentation, interaction,
    /// and compatibility code all share one +60 Z anchor contract.
    /// </summary>
    internal static class CastleKeepPlacementAdapter
    {
        internal static CastlePlan Place(in CastlePlan plan, int2 localKeepCentre) =>
            CastleSpatialProjection.ProjectKeepPlan(in plan, localKeepCentre);

        internal static int2 ActualKeepCentre(in CastlePlan placedPlan) =>
            CastleSpatialProjection.ActualKeepCentre(in placedPlan);
    }
}
