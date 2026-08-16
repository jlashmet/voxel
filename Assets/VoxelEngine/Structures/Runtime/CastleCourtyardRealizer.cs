using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using Random = Unity.Mathematics.Random;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>Realizes the occupied bailey space between the defensive shell and the keep.</summary>
    internal static class CastleCourtyardRealizer
    {
        internal static void Build(ref VoxelBrush brush, in CastlePlan plan)
        {
            int baseY = plan.Centre.y + plan.PlateauHeight;
            var rng = new Random(plan.Seed ^ 0xC0DEu);

            // Paving in the middle, worn to dirt at the edges.
            for (int z = -plan.BaileyHalfZ + 40; z < plan.BaileyHalfZ - 40; z++)
            for (int x = -plan.BaileyHalfX + 40; x < plan.BaileyHalfX - 40; x++)
            {
                byte material = rng.NextInt(0, 100) < 82 ? Mat.Stone : Mat.Dirt;
                brush.FillColumnBulk(plan.Centre.x + x, baseY, baseY + 1,
                                     plan.Centre.z + z, material);
            }

            // A well.
            int wx = plan.Centre.x - plan.BaileyHalfX / 2;
            int wz = plan.Centre.z + plan.BaileyHalfZ / 3;
            BuildWell(ref brush, wx, wz, baseY);

            // Lean-to outbuildings against the inside of the wall.
            for (int i = 0; i < 3; i++)
            {
                int bx = plan.Centre.x - plan.BaileyHalfX + 60 + i * 150;
                int bz = plan.Centre.z + plan.BaileyHalfZ - 130;
                int w = rng.NextInt(70, 100);
                int d = rng.NextInt(60, 84);
                int h = rng.NextInt(56, 76);

                brush.HollowBox(new int3(bx, baseY, bz), new int3(w, h, d),
                                5, Mat.Stone, false, false);
                brush.Box(new int3(bx + w / 2 - 9, baseY, bz),
                          new int3(18, 30, 5), Mat.Empty);
                brush.Gable(new int3(bx - 4, baseY + h, bz - 4),
                            new int3(w + 8, 30, d + 8), true, Mat.Tile);
            }
        }

        internal static void BuildPlanned(
            ref VoxelBrush brush,
            in CastlePlan plan,
            int2[] localPerimeter,
            bool hasWell,
            int2 localWellCentre) =>
            BuildPlanned(
                ref brush,
                in plan,
                localPerimeter,
                hasWell,
                localWellCentre,
                null);

        /// <summary>
        /// Realizes geometry from an already-planned arbitrary perimeter. Runtime clips paving to
        /// the supplied polygon and consumes planned well/building coordinates without making any
        /// semantic placement choices of its own.
        /// </summary>
        internal static void BuildPlanned(
            ref VoxelBrush brush,
            in CastlePlan plan,
            int2[] localPerimeter,
            bool hasWell,
            int2 localWellCentre,
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
            var rng = new Random(CastleSeedPartition.Derive(
                plan.Seed, CastleSeedDomain.Decor, 0xC047u));
            for (int z = minZ; z <= maxZ; z++)
            for (int x = minX; x <= maxX; x++)
            {
                var local = new int2(x, z);
                if (!CastlePolygonGeometry.ContainsPoint(local, localPerimeter))
                    continue;

                byte material = rng.NextInt(0, 100) < 82 ? Mat.Stone : Mat.Dirt;
                brush.FillColumnBulk(plan.Centre.x + x, baseY, baseY + 1,
                                     plan.Centre.z + z, material);
            }

            if (hasWell)
            {
                BuildWell(
                    ref brush,
                    plan.Centre.x + localWellCentre.x,
                    plan.Centre.z + localWellCentre.y,
                    baseY);
            }

            if (buildings == null)
                return;

            for (int i = 0; i < buildings.Length; i++)
                BuildOutbuilding(ref brush, in plan, in buildings[i], baseY);
        }

        private static void BuildOutbuilding(
            ref VoxelBrush brush,
            in CastlePlan plan,
            in CastleCourtyardBuildingSpec building,
            int baseY)
        {
            int width = building.HalfExtents.x * 2;
            int depth = building.HalfExtents.y * 2;
            if (width <= 0 || depth <= 0 || building.Height <= 0)
                return;

            int minX = plan.Centre.x + building.Centre.x - building.HalfExtents.x;
            int minZ = plan.Centre.z + building.Centre.y - building.HalfExtents.y;
            const int wallThickness = 5;
            const int doorWidth = 18;
            const int doorHeight = 30;
            const int doorDepth = 6;

            brush.HollowBox(
                new int3(minX, baseY, minZ),
                new int3(width, building.Height, depth),
                wallThickness,
                Mat.Stone,
                false,
                false);

            int2 entrance = building.EntranceDirection;
            if (entrance.x < 0)
            {
                brush.Box(
                    new int3(minX, baseY, minZ + depth / 2 - doorWidth / 2),
                    new int3(doorDepth, doorHeight, doorWidth),
                    Mat.Empty);
            }
            else if (entrance.x > 0)
            {
                brush.Box(
                    new int3(minX + width - doorDepth, baseY,
                             minZ + depth / 2 - doorWidth / 2),
                    new int3(doorDepth, doorHeight, doorWidth),
                    Mat.Empty);
            }
            else if (entrance.y > 0)
            {
                brush.Box(
                    new int3(minX + width / 2 - doorWidth / 2, baseY,
                             minZ + depth - doorDepth),
                    new int3(doorWidth, doorHeight, doorDepth),
                    Mat.Empty);
            }
            else
            {
                brush.Box(
                    new int3(minX + width / 2 - doorWidth / 2, baseY, minZ),
                    new int3(doorWidth, doorHeight, doorDepth),
                    Mat.Empty);
            }

            brush.Gable(
                new int3(minX - 4, baseY + building.Height, minZ - 4),
                new int3(width + 8, 30, depth + 8),
                building.RoofRidgeAlongX,
                Mat.Tile);
        }

        private static void BuildWell(ref VoxelBrush brush, int wx, int wz, int baseY)
        {
            brush.Cylinder(wx, baseY + 1, wz, 16, 12, Mat.DarkStone, 11);
            brush.Cylinder(wx, baseY - 60, wz, 11, 60, Mat.Empty);
            brush.Cylinder(wx, baseY - 60, wz, 10, 14, Mat.Water);
        }
    }
}
