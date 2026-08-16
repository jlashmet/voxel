using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Temporary Runtime compatibility shim for the legacy keep recipe. The authoritative
    /// translation now lives in Structures.Api so runtime realization and presentation clients
    /// project the same semantic keep centre into the historical +60 Z authoring frame.
    /// </summary>
    internal static class CastleKeepPlacementAdapter
    {
        internal const int LegacyKeepCentreZOffset =
            CastleSpatialLayoutProjection.LegacyKeepCentreZOffset;

        internal static CastlePlan Place(in CastlePlan plan, int2 localKeepCentre) =>
            CastleSpatialLayoutProjection.PlaceKeepPlan(in plan, localKeepCentre);

        internal static int2 ActualKeepCentre(in CastlePlan placedPlan) =>
            CastleSpatialLayoutProjection.ActualKeepCentre(in placedPlan);
    }
}
