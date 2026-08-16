using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Compatibility shim for the extracted keep recipe. The coordinate transform itself lives in
    /// Structures.Api so runtime realization and application interaction use one projection.
    /// </summary>
    internal static class CastleKeepPlacementAdapter
    {
        internal static CastlePlan Place(in CastlePlan plan, int2 localKeepCentre) =>
            CastleSpatialLayoutProjector.PlaceLegacyKeepRecipe(in plan, localKeepCentre);

        internal static int2 ActualKeepCentre(in CastlePlan placedPlan) =>
            CastleSpatialLayoutProjector.ActualKeepCentre(in placedPlan);
    }
}
