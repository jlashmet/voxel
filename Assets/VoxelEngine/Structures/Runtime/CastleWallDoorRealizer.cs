using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Reusable realization for an arched door through an already-built curtain wall. Semantic
    /// callers choose the gate placement and frozen door recipe; this component owns only bulk
    /// voxel carving and leaf realization.
    /// </summary>
    public static class CastleWallDoorRealizer
    {
        /// <summary>Compatibility overload for callers that still supply only dimensions.</summary>
        public static void CarveArchedOpening(
            ref VoxelBrush brush,
            in CastlePlan plan,
            in CastleGatePlacementSpec gate,
            int width,
            int height)
        {
            CastleWallDoorPlan door = CastleWallDoorRecipe.Historical(width, height, 1);
            CarveArchedOpening(ref brush, in plan, in gate, in door);
        }

        public static void CarveArchedOpening(
            ref VoxelBrush brush,
            in CastlePlan plan,
            in CastleGatePlacementSpec gate,
            in CastleWallDoorPlan door)
        {
            CastleWallDoorPlanValidator.RequireValid(in door);

            CastleApproachFrame frame = CastleApproachFrame.FromGate(in gate);
            int baseY = plan.Centre.y + plan.PlateauHeight + 1;
            int wallDepth = math.max(1, plan.WallThickness + door.OpeningDepthExtra);
            FillArch(
                ref brush,
                in plan,
                in frame,
                baseY,
                door.Width,
                door.Height,
                wallDepth,
                Mat.Empty);
        }

        /// <summary>Compatibility overload for callers that still supply only dimensions.</summary>
        public static void BuildArchedDoor(
            ref VoxelBrush brush,
            in CastlePlan plan,
            in CastleGatePlacementSpec gate,
            int width,
            int height,
            int depth)
        {
            CastleWallDoorPlan door = CastleWallDoorRecipe.Historical(width, height, depth);
            BuildArchedDoor(ref brush, in plan, in gate, in door);
        }

        public static void BuildArchedDoor(
            ref VoxelBrush brush,
            in CastlePlan plan,
            in CastleGatePlacementSpec gate,
            in CastleWallDoorPlan door)
        {
            CastleWallDoorPlanValidator.RequireValid(in door);

            CastleApproachFrame frame = CastleApproachFrame.FromGate(in gate);
            int baseY = plan.Centre.y + plan.PlateauHeight + 1;
            int leafWidth = door.Width - door.LeafWidthReduction;
            int leafHeight = door.Height - door.LeafHeightReduction;

            FillArch(
                ref brush,
                in plan,
                in frame,
                baseY,
                leafWidth,
                leafHeight,
                door.Depth,
                Mat.Wood);

            // Horizontal iron straps. Draw each strap through the same arch mask so a band can
            // never square off the curved head of a short door.
            for (int bandY = door.StrapFirstY;
                 bandY < leafHeight;
                 bandY += door.StrapSpacing)
            {
                int bandRows = math.min(door.StrapThickness, leafHeight - bandY);
                FillArchRows(
                    ref brush,
                    in plan,
                    in frame,
                    baseY,
                    leafWidth,
                    leafHeight,
                    bandY,
                    bandRows,
                    door.Depth + door.StrapDepthExtra,
                    Mat.DarkStone);
            }
        }

        private static void FillArch(
            ref VoxelBrush brush,
            in CastlePlan plan,
            in CastleApproachFrame frame,
            int baseY,
            int width,
            int height,
            int depth,
            byte material) =>
            FillArchRows(
                ref brush,
                in plan,
                in frame,
                baseY,
                width,
                height,
                0,
                height,
                depth,
                material);

        private static void FillArchRows(
            ref VoxelBrush brush,
            in CastlePlan plan,
            in CastleApproachFrame frame,
            int baseY,
            int width,
            int height,
            int firstRow,
            int rowCount,
            int depth,
            byte material)
        {
            int half = width / 2;
            int minFullOffset = -half;
            int maxFullOffset = width - half - 1;
            int archBase = height - half;
            int lastRow = math.min(height, firstRow + rowCount);

            for (int row = math.max(0, firstRow); row < lastRow; row++)
            {
                int minOffset = minFullOffset;
                int maxOffset = maxFullOffset;
                if (row > archBase)
                {
                    int dy = row - archBase;
                    int radiusSquared = half * half;
                    while (minOffset <= maxOffset &&
                           minOffset * minOffset + dy * dy > radiusSquared)
                        minOffset++;
                    while (maxOffset >= minOffset &&
                           maxOffset * maxOffset + dy * dy > radiusSquared)
                        maxOffset--;
                    if (minOffset > maxOffset)
                        continue;
                }

                int2 localLeft = frame.LocalPoint(minOffset, 0f);
                int2 localRight = frame.LocalPoint(maxOffset, 0f);
                int2 left = ToWorld(in plan, localLeft);
                int2 right = ToWorld(in plan, localRight);
                VoxelWallRasterizer.FillSegment(
                    ref brush,
                    left,
                    right,
                    baseY + row,
                    1,
                    depth,
                    material);
            }
        }

        private static int2 ToWorld(in CastlePlan plan, int2 local) =>
            new int2(plan.Centre.x + local.x, plan.Centre.z + local.y);
    }
}
