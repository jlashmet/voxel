using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Trust-boundary adapter for planned keep annex realization. Spatial planning owns which
    /// annexes exist; Runtime validates the snapshot and realizes exactly those selected pieces.
    /// Keep exterior features such as the rear oriel are realized in the preceding exterior stage.
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
        }
    }
}
