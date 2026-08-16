using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Temporary compatibility shim for legacy keep callers inside Runtime. The authoritative
    /// semantic-to-legacy projection now lives in Structures.Api so Runtime and presentation code
    /// cannot carry separate copies of the historical +60 Z keep offset.
    /// </summary>
    internal static class CastleKeepPlacementAdapter
    {
        internal static CastlePlan Place(in CastlePlan plan, int2 localKeepCentre) =>
            CastleSpatialLayoutProjector.PlaceKeepPlan(in plan, localKeepCentre);

        internal static int2 ActualKeepCentre(in CastlePlan placedPlan) =>
            CastleSpatialLayoutProjector.ActualKeepCentre(in placedPlan);
    }
}
