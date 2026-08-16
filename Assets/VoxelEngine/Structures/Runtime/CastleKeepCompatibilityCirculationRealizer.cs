using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Preserves the historical fixed-orientation keep entrance, grand stair, aisle, and spiral
    /// stair for compatibility builds. Spatial castles use CastleKeepCirculationRealizer with an
    /// explicit planner-owned circulation plan instead of this authored fallback.
    /// </summary>
    internal static class CastleKeepCompatibilityCirculationRealizer
    {
        internal static void Build(
            ref VoxelBrush brush,
            in CastlePlan plan,
            int3 min,
            int3 size,
            int baseY,
            int floors)
        {
            int entranceX = plan.Centre.x;
            brush.Arch(new int3(entranceX - 15, baseY + 1, min.z - 1),
                       30, 34, 10, 2, Mat.Empty);
            brush.Box(new int3(entranceX - 15, baseY + 2, min.z + 9),
                      new int3(4, 29, 3), Mat.Wood);
            brush.Box(new int3(entranceX + 11, baseY + 2, min.z + 9),
                      new int3(4, 29, 3), Mat.Wood);
            brush.Box(new int3(entranceX - 9, baseY + 1, min.z + 8),
                      new int3(18, 24, size.z / 2 - 28), Mat.Empty);

            int grandX = plan.Centre.x - 68;
            int grandZ = min.z + 28;
            const int grandWidth = 18;
            const int grandRise = 2;
            const int grandRun = 3;
            int grandSteps = plan.FloorHeight / grandRise;
            brush.Box(new int3(grandX, baseY + 1, grandZ),
                      new int3(grandWidth, plan.FloorHeight + 18, grandSteps * grandRun), Mat.Empty);
            brush.Stairs(new int3(grandX, baseY + 1, grandZ), grandWidth,
                         grandSteps, grandRise, grandRun, 2, Mat.Wood);
            brush.Box(new int3(grandX - 3, baseY + 1, grandZ),
                      new int3(3, 20, 3), Mat.Wood);
            brush.Box(new int3(grandX + grandWidth, baseY + 1, grandZ),
                      new int3(3, 20, 3), Mat.Wood);

            int stairX = min.x + 34;
            int stairZ = min.z + 34;
            const int stairRadius = 22;
            brush.SpiralStair(stairX, baseY + 2, stairZ, stairRadius,
                              floors * plan.FloorHeight, Mat.Stone);
        }
    }
}
