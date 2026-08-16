using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Compatibility entry point retained while callers converge on CastlePlannedKeepWindowRealizer.
    /// All planned-window realization is owned by that single implementation.
    /// </summary>
    internal static class CastleKeepWindowRealizer
    {
        internal static void Build(
            ref VoxelBrush brush,
            in CastlePlan plan,
            int2 worldKeepCentre,
            CastleKeepWindowSpec[] windows)
        {
            int2 localKeepCentre = new int2(
                worldKeepCentre.x - plan.Centre.x,
                worldKeepCentre.y - plan.Centre.z);
            CastlePlan keepPlan = CastleSpatialProjection.ProjectKeepPlan(
                in plan, localKeepCentre);
            CastlePlannedKeepWindowRealizer.BuildAll(ref brush, in keepPlan, windows);
        }
    }
}
