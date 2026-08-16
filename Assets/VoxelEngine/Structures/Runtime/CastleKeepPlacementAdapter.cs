using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Temporary Runtime compatibility shim for the legacy keep recipe. The actual spatial-to-
    /// legacy projection is API-owned so Runtime, Composition, presentation, and interaction all
    /// share one placement contract while the keep recipe still carries its historical +60 Z
    /// authoring offset internally.
    /// </summary>
    internal static class CastleKeepPlacementAdapter
    {
        internal static CastlePlan Place(in CastlePlan plan, int2 localKeepCentre) =>
            CastleSpatialLayoutProjection.ProjectKeepPlan(in plan, localKeepCentre);

        internal static int2 ActualKeepCentre(in CastlePlan placedPlan) =>
            CastleSpatialLayoutProjection.ActualKeepCentre(in placedPlan);
    }
}
