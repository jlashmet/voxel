using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Reusable realization for an arched door through an already-built curtain wall. Semantic
    /// callers choose the gate placement and dimensions; this component owns only bulk voxel
    /// carving and the wooden leaf. It is shared by posterns and inner-ward gates.
    /// </summary>
    public static class CastleWallDoorRealizer
    {
        public static void CarveArchedOpening(
            ref VoxelBrush brush,
            in CastlePlan plan,
            in CastleGatePlacementSpec gate,
            int width,
            int height)
        {
            if (width <= 0 || height <= 0)
                return;

            CastleApproachFrame frame = CastleApproachFrame.FromGate(in gate);
            int baseY = plan.Centre.y + plan.PlateauHeight + 1;
            int wallDepth = math.max(1, plan.WallThickness + 4);
            FillArch(
                ref brush,
                in plan,
                in frame,
                baseY,
                width,
                height,
                wallDepth,
                Mat.Empty);
        }

        public static void BuildArchedDoor(
            ref VoxelBrush brush,
            in CastlePlan plan,
            in CastleGatePlacementSpec gate,
            int width,
            int height,
            int depth)
        {
            if (width <= 4 || height <= 4 || depth <= 0)
                return;

            CastleApproachFrame frame = CastleApproachFrame.FromGate(in gate);
            int baseY = plan.Centre.y + plan.PlateauHeight + 1;
            int leafWidth = width - 4;
            int leafHeight = height - 4;

            FillArch(
                ref brush,
                in plan,
                in frame,
                baseY,
                leafWidth,
                leafHeight,
                depth,
                Mat.Wood);

            // Horizontal iron straps. Draw each strap through the same arch mask so a band can
            // never square off the curved head of a short door.
            for (int bandY = 10; bandY < leafHeight; bandY += 14)
            {
                int bandRows = math.min(2, leafHeight - bandY);
                FillArchRows(
                    ref brush,
                    in plan,
                    in frame,
                    baseY,
                    leafWidth,
                    leafHeight,
                    bandY,
                    bandRows,
                    depth + 1,
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
