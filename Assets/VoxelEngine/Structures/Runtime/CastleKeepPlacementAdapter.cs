using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Bridges semantic keep placement to the legacy keep recipe while that recipe still carries
    /// its historical +60 Z authoring offset internally. Spatial planning owns the actual keep
    /// centre; this adapter is the only place that knows how to translate it for migrated runtime
    /// realization.
    /// </summary>
    internal static class CastleKeepPlacementAdapter
    {
        internal const int LegacyKeepCentreZOffset = 60;

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
