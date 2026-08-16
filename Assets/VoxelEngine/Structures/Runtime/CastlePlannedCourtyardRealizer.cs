using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Realizes a frozen spatial courtyard. Perimeter, surface variation, well placement, and
    /// courtyard building specs all arrive from planning; this component performs voxel mutation
    /// without drawing Runtime randomness or deriving semantic seeds.
    /// </summary>
    internal static class CastlePlannedCourtyardRealizer
    {
        internal static void Build(
            ref VoxelBrush brush,
            in CastlePlan plan,
            int2[] localPerimeter,
            bool hasWell,
            int2 localWellCentre,
            in CastleSitePlan sitePlan,
            CastleCourtyardBuildingSpec[] buildings)
        {
            if (localPerimeter == null || localPerimeter.Length < 3)
                return;

            int minX = localPerimeter[0].x;
            int maxX = minX;
            int minZ = localPerimeter[0].y;
            int maxZ = minZ;
            for (int i = 1; i < localPerimeter.Length; i++)
            {
                minX = math.min(minX, localPerimeter[i].x);
                maxX = math.max(maxX, localPerimeter[i].x);
                minZ = math.min(minZ, localPerimeter[i].y);
                maxZ = math.max(maxZ, localPerimeter[i].y);
            }

            int baseY = plan.Centre.y + plan.PlateauHeight;
            for (int z = minZ; z <= maxZ; z++)
            for (int x = minX; x <= maxX; x++)
            {
                var local = new int2(x, z);
                if (!CastlePolygonGeometry.ContainsPoint(local, localPerimeter))
                    continue;

                byte material = sitePlan.ShouldUseCourtyardStone(x, z)
                    ? Mat.Stone
                    : Mat.Dirt;
                brush.FillColumnBulk(
                    plan.Centre.x + x,
                    baseY,
                    baseY + 1,
                    plan.Centre.z + z,
                    material);
            }

            if (hasWell)
            {
                BuildWell(
                    ref brush,
                    plan.Centre.x + localWellCentre.x,
                    plan.Centre.z + localWellCentre.y,
                    baseY);
            }

            CastleCourtyardBuildingRealizer.BuildAll(ref brush, in plan, buildings);
        }

        private static void BuildWell(ref VoxelBrush brush, int wx, int wz, int baseY)
        {
            brush.Cylinder(wx, baseY + 1, wz, 16, 12, Mat.DarkStone, 11);
            brush.Cylinder(wx, baseY - 60, wz, 11, 60, Mat.Empty);
            brush.Cylinder(wx, baseY - 60, wz, 10, 14, Mat.Water);
        }
    }
}
