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
            CastleWallDoorGeometry geometry = CastleWallDoorGeometryResolver.Resolve(
                in plan, in gate, in door);
            FillArchRows(
                ref brush,
                in geometry,
                door.Width,
                door.Height,
                0,
                door.Height,
                geometry.OpeningDepth,
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
            CastleWallDoorGeometry geometry = CastleWallDoorGeometryResolver.Resolve(
                in plan, in gate, in door);
            int leafWidth = geometry.LeafWidth;
            int leafHeight = geometry.LeafHeight;

            FillArchRows(
                ref brush,
                in geometry,
                leafWidth,
                leafHeight,
                0,
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
                    in geometry,
                    leafWidth,
                    leafHeight,
                    bandY,
                    bandRows,
                    door.Depth + door.StrapDepthExtra,
                    Mat.DarkStone);
            }
        }

        private static void FillArchRows(
            ref VoxelBrush brush,
            in CastleWallDoorGeometry geometry,
            int width,
            int height,
            int firstRow,
            int rowCount,
            int depth,
            byte material)
        {
            int lastRow = math.min(height, firstRow + rowCount);
            for (int row = math.max(0, firstRow); row < lastRow; row++)
            {
                if (!CastleWallDoorGeometry.TryGetArchRowSpan(
                        width, height, row, out int minOffset, out int maxOffset))
                    continue;

                int2 left = geometry.WorldPoint(minOffset);
                int2 right = geometry.WorldPoint(maxOffset);
                VoxelWallRasterizer.FillSegment(
                    ref brush,
                    left,
                    right,
                    geometry.BaseY + row,
                    1,
                    depth,
                    material);
            }
        }
    }
}
