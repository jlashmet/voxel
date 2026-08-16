using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Realizes the keep's rear timber oriel from an already-placed keep plan. Whether the oriel
    /// exists is planner-owned; this component owns only the historical voxel recipe.
    /// </summary>
    internal static class CastleRearOrielRealizer
    {
        internal static void Build(ref VoxelBrush brush, in CastlePlan plan)
        {
            int baseY = plan.Centre.y + plan.PlateauHeight;
            int hz = plan.KeepHalfZ;
            int keepMinZ = plan.Centre.z - hz + CastleLayout.LegacyKeepCentreZOffset;
            int keepDepth = hz * 2;

            const int width = 44;
            const int depth = 22;
            int minX = plan.Centre.x + 18;
            int wallZ = keepMinZ + keepDepth;
            int firstFloorY = baseY + plan.FloorHeight * 2;

            for (int x = 3; x < width - 2; x += 12)
            {
                brush.Box(new int3(minX + x, firstFloorY - 13, wallZ + 2),
                          new int3(5, 13, 14), Mat.DarkStone);
            }

            for (int storey = 0; storey < 2; storey++)
            {
                int y = firstFloorY + storey * plan.FloorHeight;
                brush.Box(new int3(minX, y, wallZ - 2), new int3(width, 4, depth), Mat.Wood);
                brush.Box(new int3(minX, y + 4, wallZ + depth - 5),
                          new int3(width, plan.FloorHeight - 7, 4), Mat.Wood);
                brush.Box(new int3(minX, y + 4, wallZ),
                          new int3(4, plan.FloorHeight - 7, depth - 3), Mat.Wood);
                brush.Box(new int3(minX + width - 4, y + 4, wallZ),
                          new int3(4, plan.FloorHeight - 7, depth - 3), Mat.Wood);

                for (int bay = 0; bay < 3; bay++)
                {
                    int bayX = minX + 5 + bay * 13;
                    brush.Box(new int3(bayX, y + 9, wallZ + depth - 4),
                              new int3(9, plan.FloorHeight - 18, 3), Mat.LitWindow);
                }

                brush.Box(new int3(minX + 8, y + 4, wallZ - 8),
                          new int3(width - 16, 25, 12), Mat.Empty);
                brush.Box(new int3(minX + 4, y + 4, wallZ + 4),
                          new int3(width - 8, plan.FloorHeight - 8, depth - 9), Mat.Empty);
            }

            int roofY = firstFloorY + plan.FloorHeight * 2;
            brush.Gable(new int3(minX - 4, roofY, wallZ - 4),
                        new int3(width + 8, 24, depth + 8), true, Mat.Tile);
            brush.Box(new int3(minX - 3, firstFloorY + plan.FloorHeight - 1, wallZ - 1),
                      new int3(width + 6, 3, depth + 1), Mat.DarkStone);
        }
    }
}
