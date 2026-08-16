using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Realizes the four structural corner turrets attached to the keep shell. Tower geometry
    /// remains centralized in CastleTowerRealizer; this component owns only keep-corner placement.
    /// </summary>
    internal static class CastleKeepTurretRealizer
    {
        internal static void Build(
            ref VoxelBrush brush,
            in CastlePlan plan,
            int3 min,
            int3 size,
            int baseY)
        {
            for (int i = 0; i < 4; i++)
            {
                int cx = min.x + (i % 2 == 0 ? 0 : size.x);
                int cz = min.z + (i < 2 ? 0 : size.z);
                CastleTowerRealizer.Build(
                    ref brush,
                    in plan,
                    new int3(cx, baseY, cz),
                    26,
                    plan.KeepHeight + 30,
                    true);
            }
        }
    }
}
