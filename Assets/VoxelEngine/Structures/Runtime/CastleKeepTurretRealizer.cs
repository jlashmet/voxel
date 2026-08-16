using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Places the four legacy keep corner turrets and delegates their geometry to the shared tower
    /// realizer. This component owns only keep-local placement; it makes no topology or random
    /// decisions.
    /// </summary>
    internal static class CastleKeepTurretRealizer
    {
        internal static void BuildAll(
            ref VoxelBrush brush,
            in CastlePlan plan,
            int3 keepMin,
            int3 keepSize,
            int baseY)
        {
            for (int i = 0; i < 4; i++)
            {
                int cx = keepMin.x + (i % 2 == 0 ? 0 : keepSize.x);
                int cz = keepMin.z + (i < 2 ? 0 : keepSize.z);
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
