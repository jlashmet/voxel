using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.WorldBuilder.Voxel
{
    /// <summary>
    /// Late lower-entry correction driven by direct standalone comparison with the pinned reference.
    /// It owns only the shallow central portal patch; side windows, upper facade, roof, site, and
    /// audit elevations remain outside this operation.
    /// </summary>
    internal static class NewHouseReferenceEntryPortalFinish
    {
        public static void Apply(IStructureAuthoringSession a, int3 o,
            in NewHouseReferenceConfig c, in NewHouseReferencePalette p)
        {
            int centre = o.x + c.Width / 2;
            int first = o.y + c.FirstFloorY;
            int front = o.z;
            int portalY = first + 3;

            const int innerRadius = 8;
            const int outerRadius = 13;
            const int spring = 16;
            const int patchHalf = 16;
            const int patchHeight = 31;

            // Iteration 13 proved that the remaining entry mismatch is authored geometry rather
            // than camera: the door reads as a flat rectangle because its same-material surround
            // never projects enough to form the reference's dominant round masonry portal. Clear
            // and refill only the central shallow patch so both narrow lower side windows survive.
            a.Carve(new int3(centre - patchHalf, first + 1, front - 7),
                new int3(patchHalf * 2 + 1, patchHeight, 11));
            a.Box(new int3(centre - patchHalf, first + 1, front - 3),
                new int3(patchHalf * 2 + 1, patchHeight, 6), p.Stone);

            // Recess the door behind the projecting surround and retain a true round top instead
            // of covering the opening with a rectangular evidence panel.
            int doorHeight = spring + innerRadius + 1;
            for (int row = 0; row < doorHeight; row++)
            {
                int half = row < spring
                    ? innerRadius
                    : CircleHalfWidth(innerRadius, row - spring);
                a.Carve(new int3(centre - half, portalY + row, front - 4),
                    new int3(half * 2 + 1, 1, 9));
                a.Box(new int3(centre - half, portalY + row, front + 1),
                    new int3(half * 2 + 1, 1, 2), p.Door);
            }

            // Deep jambs and a row-built outer arch put the stone several voxels ahead of the wall,
            // producing visible shadow/depth separation even though the wall and surround share the
            // same masonry material, as they do in the reference.
            int jambWidth = outerRadius - innerRadius + 1;
            a.Box(new int3(centre - outerRadius - 1, portalY - 2, front - 8),
                new int3(jambWidth + 1, spring + 3, 5), p.Stone);
            a.Box(new int3(centre + innerRadius, portalY - 2, front - 8),
                new int3(jambWidth + 1, spring + 3, 5), p.Stone);

            int springY = portalY + spring;
            for (int dy = 0; dy <= outerRadius; dy++)
            {
                int outerHalf = CircleHalfWidth(outerRadius, dy);
                int innerHalf = dy <= innerRadius ? CircleHalfWidth(innerRadius, dy) : -1;
                int y = springY + dy;

                if (innerHalf < 0)
                {
                    a.Box(new int3(centre - outerHalf, y, front - 8),
                        new int3(outerHalf * 2 + 1, 1, 5), p.Stone);
                }
                else
                {
                    int sideWidth = math.max(1, outerHalf - innerHalf);
                    a.Box(new int3(centre - outerHalf, y, front - 8),
                        new int3(sideWidth, 1, 5), p.Stone);
                    a.Box(new int3(centre + innerHalf + 1, y, front - 8),
                        new int3(sideWidth, 1, 5), p.Stone);
                }

                // A restrained extra face every few courses suggests individual voussoirs without
                // introducing a new material role or a parallel decorative mesh path.
                if ((dy % 3) == 1)
                {
                    a.Box(new int3(centre - outerHalf, y, front - 9),
                        new int3(2, 1, 2), p.Stone);
                    a.Box(new int3(centre + outerHalf - 1, y, front - 9),
                        new int3(2, 1, 2), p.Stone);
                }
            }

            a.Box(new int3(centre - outerRadius - 2, springY - 2, front - 9),
                new int3(jambWidth + 3, 3, 6), p.Stone);
            a.Box(new int3(centre + innerRadius - 1, springY - 2, front - 9),
                new int3(jambWidth + 3, 3, 6), p.Stone);
            a.Box(new int3(centre - innerRadius - 2, portalY - 2, front - 8),
                new int3(innerRadius * 2 + 5, 2, 5), p.Stone);

            // Restore compact centered hardware after the destructive entry repair.
            int hardwareZ = front - 2;
            a.Box(new int3(centre - 2, portalY + 8, hardwareZ),
                new int3(5, 4, 1), p.Ornament);
            a.Box(new int3(centre, portalY + 5, hardwareZ),
                new int3(1, 10, 1), p.Ornament);
            a.Box(new int3(centre - 4, portalY + 10, hardwareZ),
                new int3(9, 1, 1), p.Ornament);
        }

        private static int CircleHalfWidth(int radius, int dy)
        {
            int clamped = math.min(radius, math.max(0, dy));
            return math.max(0, (int)math.floor(math.sqrt(
                math.max(0, radius * radius - clamped * clamped))));
        }
    }
}
