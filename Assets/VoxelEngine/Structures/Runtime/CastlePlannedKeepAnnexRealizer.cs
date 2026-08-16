using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Trust-boundary adapter for planned keep annex realization. Spatial planning owns which
    /// annexes exist; Runtime validates the snapshot and realizes exactly those selected pieces.
    /// </summary>
    internal static class CastlePlannedKeepAnnexRealizer
    {
        internal static void Build(
            ref VoxelBrush brush,
            in CastlePlan plan,
            in CastleKeepAnnexPlan annexes)
        {
            CastleKeepAnnexPlanValidator.RequireValid(in annexes);
            CastleKeepAnnexRealizer.BuildPlanned(ref brush, in plan, in annexes);
            if (annexes.HasRearOriel)
                CastleRearOrielRealizer.Build(ref brush, in plan);
        }
    }
}
