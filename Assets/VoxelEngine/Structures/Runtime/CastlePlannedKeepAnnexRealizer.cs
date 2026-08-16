using System;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Trust-boundary adapter for the behavior-preserving keep-annex migration. Spatial planning
    /// explicitly owns which annexes exist; this adapter refuses unsupported future combinations
    /// rather than silently rebuilding the historical full recipe.
    /// </summary>
    internal static class CastlePlannedKeepAnnexRealizer
    {
        internal static void Build(
            ref VoxelBrush brush,
            in CastlePlan plan,
            in CastleKeepAnnexPlan annexes)
        {
            CastleKeepAnnexPlanValidator.RequireValid(in annexes);

            if (!annexes.HasGreatHallWing ||
                !annexes.HasChapelWing ||
                !annexes.HasBellTower)
            {
                throw new InvalidOperationException(
                    "The current keep-annex voxel recipe supports only the behavior-preserving " +
                    "Great Hall + chapel + bell-tower plan. Add selective annex realization " +
                    "before introducing topology variation for these flags.");
            }

            CastleKeepAnnexRealizer.Build(ref brush, in plan);
        }
    }
}
