using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Deterministic voxel realization for planner-owned courtyard building footprints. Placement,
    /// entrance direction, roof axis, and dimensions are already decided by Structures.Api; this
    /// component only turns those specs into masonry, a doorway, and a pitched roof.
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
            if (building.HalfExtents.x <= 0 || building.HalfExtents.y <= 0 ||
                building.Height <= 0)
                return;

            int baseY = plan.Centre.y + plan.PlateauHeight;
            int2 c0 = ToWorld(in plan, building.FootprintCorner(0));
            int2 c1 = ToWorld(in plan, building.FootprintCorner(1));
            int2 c2 = ToWorld(in plan, building.FootprintCorner(2));
            int2 c3 = ToWorld(in plan, building.FootprintCorner(3));

            Wall(ref brush, c0, c1, baseY, building.Height);
            Wall(ref brush, c1, c2, baseY, building.Height);
            Wall(ref brush, c2, c3, baseY, building.Height);
            Wall(ref brush, c3, c0, baseY, building.Height);

            CarveDoor(ref brush, in plan, in building, baseY);
            Roof(ref brush, in plan, in building, baseY);
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
            int baseY)
        {
            int2 direction = building.EntranceDirection;
            if (math.abs(direction.x) + math.abs(direction.y) != 1)
                return;

            int2 centre = building.EntranceCentre;
            int2 tangent = direction.x != 0 ? new int2(0, 1) : new int2(1, 0);
            int2 left = ToWorld(in plan, centre - tangent * DoorHalfWidth);
            int2 right = ToWorld(in plan, centre + tangent * DoorHalfWidth);
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
            int baseY)
        {
            int halfX = building.HalfExtents.x + RoofOverhang;
            int halfZ = building.HalfExtents.y + RoofOverhang;
            int slopeHalfExtent = building.RoofRidgeAlongX ? halfZ : halfX;
            int roofHeight = math.clamp(slopeHalfExtent * 2 / 3, 14, 28);

            for (int offset = -slopeHalfExtent; offset <= slopeHalfExtent; offset += 2)
            {
                float normalized = math.saturate(math.abs(offset) / (float)slopeHalfExtent);
                int rise = (int)math.round((1f - normalized) * roofHeight);
                int2 startLocal;
                int2 endLocal;
                if (building.RoofRidgeAlongX)
                {
                    int z = building.Centre.y + offset;
                    startLocal = new int2(building.Centre.x - halfX, z);
                    endLocal = new int2(building.Centre.x + halfX, z);
                }
                else
                {
                    int x = building.Centre.x + offset;
                    startLocal = new int2(x, building.Centre.y - halfZ);
                    endLocal = new int2(x, building.Centre.y + halfZ);
                }

                VoxelWallRasterizer.FillSegment(
                    ref brush,
                    ToWorld(in plan, startLocal),
                    ToWorld(in plan, endLocal),
                    baseY + building.Height + rise,
                    2,
                    3,
                    Mat.Tile);
            }
        }

        private static int2 ToWorld(in CastlePlan plan, int2 local) =>
            new int2(plan.Centre.x + local.x, plan.Centre.z + local.y);
    }
}
