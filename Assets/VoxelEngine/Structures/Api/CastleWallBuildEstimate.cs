using System;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Relative curtain-wall workload model used by castle preflight. The historical wall recipe
    /// is calibrated to the legacy 240-equivalent-writes-per-voxel-of-perimeter estimate, while
    /// planned authoring changes that materially add mutation work scale that baseline.
    /// </summary>
    internal static class CastleWallBuildEstimate
    {
        private const double LegacyEquivalentWritesPerPerimeterVoxel = 240.0;

        internal static double EstimateEquivalentWrites(
            in CastlePlan plan,
            int2[] outerPerimeter,
            int2[] innerPerimeter,
            in CastleWallPlan walls)
        {
            if (!CastleWallPlanValidator.TryValidate(in walls, out _))
                return 0.0;

            double perimeter = PolygonPerimeter(outerPerimeter)
                             + PolygonPerimeter(innerPerimeter);
            if (perimeter <= 0.0)
                return 0.0;

            CastleWallPlan historical = CastleWallRecipe.Historical();
            double plannedUnits = PerimeterRecipeUnits(in plan, outerPerimeter, in walls)
                                + PerimeterRecipeUnits(in plan, innerPerimeter, in walls);
            double historicalUnits = PerimeterRecipeUnits(
                                        in plan, outerPerimeter, in historical)
                                   + PerimeterRecipeUnits(
                                        in plan, innerPerimeter, in historical);

            double baseline = perimeter * LegacyEquivalentWritesPerPerimeterVoxel;
            if (historicalUnits <= 0.0)
                return baseline;

            return baseline * math.max(0.0, plannedUnits / historicalUnits);
        }

        private static double PerimeterRecipeUnits(
            in CastlePlan plan,
            int2[] perimeter,
            in CastleWallPlan walls)
        {
            if (perimeter == null || perimeter.Length < 2)
                return 0.0;

            double units = 0.0;
            for (int edge = 0; edge < perimeter.Length; edge++)
            {
                int2 a = perimeter[edge];
                int2 b = perimeter[(edge + 1) % perimeter.Length];
                long dx = (long)b.x - a.x;
                long dz = (long)b.y - a.y;
                double length = Math.Sqrt(dx * (double)dx + dz * (double)dz);
                units += EdgeRecipeUnits(in plan, length, in walls);
            }
            return units;
        }

        private static double EdgeRecipeUnits(
            in CastlePlan plan,
            double length,
            in CastleWallPlan walls)
        {
            if (length <= 0.0)
                return 0.0;

            int height = math.max(0, plan.WallHeight);
            int thickness = math.max(0, plan.WallThickness);
            if (height == 0 || thickness == 0)
                return 0.0;

            double units = length * height * thickness;
            units += length * math.min(walls.MaxPlinthHeight, height) * thickness;

            if (height >= walls.CourseMinimumWallHeight)
                units += length * walls.CourseThickness * thickness;

            units += length * walls.WallWalkThickness * thickness;
            units += ArrowSlitUnits(length, height, thickness, in walls);
            units += CrenellationUnits(length, thickness, in walls);
            return units;
        }

        private static double ArrowSlitUnits(
            double length,
            int wallHeight,
            int wallThickness,
            in CastleWallPlan walls)
        {
            if (wallHeight < walls.ArrowSlitMinimumWallHeight)
                return 0.0;

            double usableEnd = length - walls.ArrowSlitEndInset;
            if (walls.ArrowSlitFirstDistance >= usableEnd)
                return 0.0;

            int count = 1 + (int)Math.Floor(
                (usableEnd - walls.ArrowSlitFirstDistance - 1e-9)
                / walls.ArrowSlitSpacing);
            if (count <= 0)
                return 0.0;

            int slitHeight = math.max(
                0,
                math.min(walls.ArrowSlitMaxHeight, wallHeight - walls.ArrowSlitYOffset));
            if (slitHeight == 0)
                return 0.0;

            double slitDepth = math.max(2.0, wallThickness * walls.ArrowSlitDepthScale * 2.0);
            return count * slitDepth * slitHeight * walls.ArrowSlitThickness;
        }

        private static double CrenellationUnits(
            double length,
            int wallThickness,
            in CastleWallPlan walls)
        {
            double period = walls.CrenellationMerlonLength + walls.CrenellationGapLength;
            if (period <= 0.0)
                return 0.0;

            int thickness = math.clamp(
                wallThickness,
                walls.CrenellationMinimumThickness,
                walls.CrenellationMaximumThickness);
            double merlonLength = 0.0;
            for (double distance = 0.0; distance < length; distance += period)
                merlonLength += math.min((double)walls.CrenellationMerlonLength, length - distance);

            return merlonLength * walls.CrenellationHeight * thickness;
        }

        private static double PolygonPerimeter(int2[] polygon)
        {
            if (polygon == null || polygon.Length < 2)
                return 0.0;

            double perimeter = 0.0;
            for (int i = 0; i < polygon.Length; i++)
            {
                int2 a = polygon[i];
                int2 b = polygon[(i + 1) % polygon.Length];
                long dx = (long)b.x - a.x;
                long dz = (long)b.y - a.y;
                perimeter += Math.Sqrt(dx * (double)dx + dz * (double)dz);
            }
            return perimeter;
        }
    }
}
