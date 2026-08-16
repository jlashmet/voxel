using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Realizes the keep's repeated front/rear window bays. Room purpose, circulation, facade
    /// decoration, and turret geometry remain owned by their respective keep components.
    /// </summary>
    internal static class CastleKeepFenestrationRealizer
    {
        internal static void Build(
            ref VoxelBrush brush,
            in CastlePlan plan,
            int3 min,
            int3 size,
            int baseY,
            int floors)
        {
            for (int f = 0; f < floors; f++)
            {
                int y = baseY + f * plan.FloorHeight + 12;
                int height = f == 1 ? plan.FloorHeight - 14 : plan.FloorHeight - 18;

                for (int i = 0; i < 3; i++)
                {
                    int x = min.x + size.x / 4 + i * size.x / 4 - 8;
                    bool mainEntrance = f == 0 && i == 1;
                    if (!mainEntrance)
                    {
                        brush.Arch(new int3(x, y, min.z), 16, height, 9, 2, Mat.Empty);
                        brush.Box(new int3(x + 3, y + 4, min.z + 2),
                                  new int3(10, height - 10, 2), Mat.LitWindow);
                        brush.Box(new int3(x + 7, y + 5, min.z + 1),
                                  new int3(2, height - 12, 3), Mat.DarkStone);
                        brush.Box(new int3(x + 3, y + height / 2, min.z + 1),
                                  new int3(10, 2, 3), Mat.DarkStone);
                    }

                    brush.Arch(new int3(x, y, min.z + size.z - 8),
                               16, height, 9, 2, Mat.Empty);
                }
            }
        }
    }
}
