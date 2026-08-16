using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Deterministic voxel realization for planner-owned courtyard building footprints. Placement,
    /// purpose, orientation, and dimensions are already decided by Structures.Api; this component
    /// only turns those specs into masonry, a doorway, and a pitched roof.
    /// </summary>
    public static class CastleCourtyardBuildingRealizer
    {
        private const int WallThickness = 5;
        private const int DoorHalfWidth = 9;
        private const int DoorHeight = 28;
        private const int RoofOverhang = 6;

        public static void BuildAll(
            ref VoxelBrush brush,
            in CastlePlan plan,
            CastleCourtyardBuildingSpec[] buildings)
        {
            if (buildings == null) return;
            for (int i = 0; i < buildings.Length; i++)
            {
                CastleCourtyardBuildingSpec building = buildings[i];
                Build(ref brush, in plan, in building);
            }
        }

        public static void Build(
            ref VoxelBrush brush,
            in CastlePlan plan,
            in CastleCourtyardBuildingSpec building)
        {
            if (building.Width <= 0 || building.Depth <= 0 || building.Height <= 0)
                return;

            float tangentLength = math.length(building.Tangent);
            float inwardLength = math.length(building.Inward);
            if (tangentLength < 0.001f || inwardLength < 0.001f)
                return;

            float2 tangent = building.Tangent / tangentLength;
            float2 inward = building.Inward / inwardLength;
            int baseY = plan.Centre.y + plan.PlateauHeight;

            int2 c0 = ToWorld(in plan, building.FootprintCorner(0));
            int2 c1 = ToWorld(in plan, building.FootprintCorner(1));
            int2 c2 = ToWorld(in plan, building.FootprintCorner(2));
            int2 c3 = ToWorld(in plan, building.FootprintCorner(3));

            Wall(ref brush, c0, c1, baseY, building.Height);
            Wall(ref brush, c1, c2, baseY, building.Height);
            Wall(ref brush, c2, c3, baseY, building.Height);
            Wall(ref brush, c3, c0, baseY, building.Height);

            CarveDoor(ref brush, in plan, in building, tangent, baseY);
            Roof(ref brush, in plan, in building, tangent, inward, baseY);
        }

        private static void Wall(
            ref VoxelBrush brush,
            int2 start,
            int2 end,
            int baseY,
            int height)
        {
            VoxelWallRasterizer.FillSegment(
                ref brush, start, end, baseY, height, WallThickness, Mat.Stone);
            VoxelWallRasterizer.FillSegment(
                ref brush, start, end, baseY, math.min(8, height),
                WallThickness + 2, Mat.DarkStone);
        }

        private static void CarveDoor(
            ref VoxelBrush brush,
            in CastlePlan plan,
            in CastleCourtyardBuildingSpec building,
            float2 tangent,
            int baseY)
        {
            float2 centre = new float2(building.DoorCentre.x, building.DoorCentre.y);
            int2 left = ToWorld(in plan, Round(centre - tangent * DoorHalfWidth));
            int2 right = ToWorld(in plan, Round(centre + tangent * DoorHalfWidth));
            VoxelWallRasterizer.FillSegment(
                ref brush,
                left,
                right,
                baseY + 1,
                math.min(DoorHeight, math.max(1, building.Height - 4)),
                WallThickness + 4,
                Mat.Empty);
        }

        private static void Roof(
            ref VoxelBrush brush,
            in CastlePlan plan,
            in CastleCourtyardBuildingSpec building,
            float2 tangent,
            float2 inward,
            int baseY)
        {
            int halfWidth = building.Width / 2 + RoofOverhang;
            int halfDepth = building.Depth / 2 + RoofOverhang;
            int roofHeight = math.clamp(building.Depth / 3, 14, 28);
            float2 centre = new float2(building.Centre.x, building.Centre.y);

            for (int depth = -halfDepth; depth <= halfDepth; depth += 2)
            {
                float normalized = math.saturate(math.abs(depth) / (float)halfDepth);
                int rise = (int)math.round((1f - normalized) * roofHeight);
                float2 stripCentre = centre + inward * depth;
                int2 left = ToWorld(in plan, Round(stripCentre - tangent * halfWidth));
                int2 right = ToWorld(in plan, Round(stripCentre + tangent * halfWidth));
                VoxelWallRasterizer.FillSegment(
                    ref brush,
                    left,
                    right,
                    baseY + building.Height + rise,
                    2,
                    3,
                    Mat.Tile);
            }
        }

        private static int2 ToWorld(in CastlePlan plan, int2 local) =>
            new int2(plan.Centre.x + local.x, plan.Centre.z + local.y);

        private static int2 Round(float2 point) =>
            new int2((int)math.round(point.x), (int)math.round(point.y));
    }
}
