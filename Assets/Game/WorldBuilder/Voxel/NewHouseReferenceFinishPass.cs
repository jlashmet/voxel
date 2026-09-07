using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.WorldBuilder.Voxel
{
    /// <summary>
    /// Late reference-only finish pass applied after the structural refinement. It corrects the
    /// highest-value silhouette/details exposed by standalone-player comparison without moving
    /// camera, lighting, site policy, or material ownership into reusable authoring.
    /// </summary>
    internal static class NewHouseReferenceFinishPass
    {
        private const int TimberDepth = 2;

        public static void Apply(IStructureAuthoringSession a, int3 o,
            in NewHouseReferenceConfig c, in NewHouseReferencePalette p)
        {
            RefinePortraitGable(a, o, in c, in p);
            ReplaceOversizedCrest(a, o, in c, in p);
            RebuildHangingDetails(a, o, in c, in p);
        }

        private static void RefinePortraitGable(IStructureAuthoringSession a, int3 o,
            in NewHouseReferenceConfig c, in NewHouseReferencePalette p)
        {
            int centre = o.x + c.Width / 2;
            int upper = o.y + c.UpperFloorY;
            int eave = o.y + c.MainEaveY;
            int ridge = o.y + c.MainRidgeY;
            int front = o.z - 2;
            int portraitEave = upper + 31;

            // Iteration 7 exposed a real depth-ordering defect: FillArch repaired the old oversized
            // opening all the way forward to front-4, but the replacement ArchedPanel only carved
            // from front-1. The glass therefore existed behind three opaque plaster layers while only
            // its front-mounted muntins were visible. Refill the old opening, then carve through that
            // complete repair depth so the smaller reference window is actually visible in player output.
            FillArch(a, centre, eave + 10, front, 15, 21, p.Plaster);
            ArchedPanel(a, centre, eave + 12, front, 11, 17,
                p.Glass, p.Timber, p.Timber);

            a.Box(new int3(centre - 20, portraitEave + 4, front - 4),
                new int3(41, 2, TimberDepth), p.Timber);
            a.Box(new int3(centre - 14, portraitEave + 27, front - 4),
                new int3(29, 2, TimberDepth), p.Timber);
            a.Box(new int3(centre - 1, portraitEave + 4, front - 4),
                new int3(2, 6, TimberDepth), p.Timber);
            a.Box(new int3(centre - 1, eave + 31, front - 4),
                new int3(2, math.max(4, ridge - (eave + 31) - 4), TimberDepth), p.Timber);

            Line(a, centre - 19, portraitEave + 5, centre - 8, portraitEave + 18,
                front - 4, p.Timber);
            Line(a, centre + 19, portraitEave + 5, centre + 8, portraitEave + 18,
                front - 4, p.Timber);
            Line(a, centre - 13, portraitEave + 28, centre - 4, portraitEave + 38,
                front - 4, p.Timber);
            Line(a, centre + 13, portraitEave + 28, centre + 4, portraitEave + 38,
                front - 4, p.Timber);

            // Iteration 7's ten-voxel extension only softened the straight A-frame termination.
            // The reference has a materially longer hook that continues outward while dropping.
            // Extend that same production roof skin sixteen voxels and increase the quadratic drop.
            const int halfGable = 27;
            int roofZ = o.z - c.RoofOverhang - 2;
            for (int i = 1; i <= 16; i++)
            {
                int drop = (i * i + 24) / 32;
                int y = portraitEave - 1 - drop;
                int left = centre - halfGable - i;
                int right = centre + halfGable + i;
                a.Box(new int3(left, y, roofZ), new int3(1, 2, 18), p.Roof);
                a.Box(new int3(right, y, roofZ), new int3(1, 2, 18), p.Roof);
                a.Box(new int3(left, y - 1, front - 5), new int3(1, 2, TimberDepth), p.Timber);
                a.Box(new int3(right, y - 1, front - 5), new int3(1, 2, TimberDepth), p.Timber);
            }
        }

        private static void ReplaceOversizedCrest(IStructureAuthoringSession a, int3 o,
            in NewHouseReferenceConfig c, in NewHouseReferencePalette p)
        {
            int centre = o.x + c.Width / 2;
            int ridge = o.y + c.MainRidgeY;
            int z = o.z + 5;

            // Iteration 7 still retained the ridge+1 layers from both earlier crest builders, which
            // visually merged with the replacement into a tall rectangular gold post. Clear from
            // ridge+1 upward (leaving the actual roof ridge intact), then rebuild one compact finial.
            a.Carve(new int3(centre - 8, ridge + 1, o.z), new int3(17, 18, 16));
            a.Box(new int3(centre - 2, ridge + 1, z), new int3(5, 2, 5), p.Timber);
            a.Box(new int3(centre - 2, ridge + 3, z + 1), new int3(5, 2, 3), p.Ornament);
            a.Box(new int3(centre - 1, ridge + 5, z + 1), new int3(3, 3, 3), p.Ornament);
            a.Cone(centre, ridge + 8, z + 2, 2, 6, p.Ornament);
        }

        private static void RebuildHangingDetails(IStructureAuthoringSession a, int3 o,
            in NewHouseReferenceConfig c, in NewHouseReferencePalette p)
        {
            int upper = o.y + c.UpperFloorY;
            int front = o.z - 10;

            // The previous sign floated too far from the wall. Its volume is outside the reusable
            // shell, so it can be cleared without touching house structure and rebuilt closer in.
            int right = o.x + c.Width;
            a.Carve(new int3(right, upper + 4, front - 2), new int3(24, 32, 8));
            int bracketX = right - 2;
            a.Box(new int3(bracketX, upper + 27, front), new int3(12, 2, 2), p.Timber);
            a.Box(new int3(bracketX + 9, upper + 17, front), new int3(2, 11, 2), p.Timber);
            AddShield(a, bracketX + 5, upper + 8, front + 1, 8, 11, p.Timber, p.Ornament);

            // Rebuild the banner with a pointed lower silhouette instead of the blockout rectangle.
            int bannerX = o.x - 3;
            a.Carve(new int3(o.x - 12, upper + 3, front - 2), new int3(22, 29, 8));
            a.Box(new int3(bannerX - 2, upper + 27, front), new int3(16, 2, 2), p.Timber);
            a.Box(new int3(bannerX, upper + 9, front + 1), new int3(12, 18, 1), p.Accent);
            for (int row = 0; row < 5; row++)
            {
                int inset = (row + 1) / 2;
                a.Box(new int3(bannerX + inset, upper + 8 - row, front + 1),
                    new int3(12 - inset * 2, 1, 1), p.Accent);
            }
            int cx = bannerX + 6;
            int cy = upper + 18;
            a.Box(new int3(cx, cy - 4, front), new int3(1, 9, 1), p.Ornament);
            a.Box(new int3(cx - 3, cy, front), new int3(7, 1, 1), p.Ornament);
            Diagonal(a, cx - 2, cy - 2, cx + 2, cy + 2, front, p.Ornament);
            Diagonal(a, cx - 2, cy + 2, cx + 2, cy - 2, front, p.Ornament);
        }

        private static void FillArch(IStructureAuthoringSession a,
            int centreX, int y, int frontZ, int width, int height, byte material)
        {
            int radius = math.max(2, width / 2);
            int spring = math.max(2, height - radius - 1);
            for (int row = 0; row < height; row++)
            {
                int half = ArchHalfWidth(radius, spring, row);
                a.Box(new int3(centreX - half, y + row, frontZ - 4),
                    new int3(half * 2 + 1, 1, 6), material);
            }
        }

        private static void ArchedPanel(IStructureAuthoringSession a,
            int centreX, int y, int frontZ, int width, int height,
            byte panel, byte frame, byte surround)
        {
            int radius = math.max(2, width / 2);
            int spring = math.max(2, height - radius - 1);
            for (int row = 0; row < height; row++)
            {
                int half = ArchHalfWidth(radius, spring, row);
                // Clear through the complete front repair layer created by FillArch. Starting at
                // frontZ-1 left opaque plaster in front of the replacement glass in iteration 7.
                a.Carve(new int3(centreX - half, y + row, frontZ - 4),
                    new int3(half * 2 + 1, 1, 9));
                a.Box(new int3(centreX - half, y + row, frontZ + 1),
                    new int3(half * 2 + 1, 1, 2), panel);
                if (row >= spring)
                {
                    a.Box(new int3(centreX - half - 1, y + row, frontZ - 3),
                        new int3(1, 1, 2), surround);
                    a.Box(new int3(centreX + half + 1, y + row, frontZ - 3),
                        new int3(1, 1, 2), surround);
                }
            }
            a.Box(new int3(centreX - radius - 1, y - 1, frontZ - 3),
                new int3(1, spring + 2, 2), surround);
            a.Box(new int3(centreX + radius + 1, y - 1, frontZ - 3),
                new int3(1, spring + 2, 2), surround);
            a.Box(new int3(centreX - radius - 2, y - 2, frontZ - 3),
                new int3(radius * 2 + 5, 2, 3), surround);
            a.Box(new int3(centreX, y + 1, frontZ - 4),
                new int3(1, height - 3, 2), frame);
            a.Box(new int3(centreX - radius + 1, y + spring / 2, frontZ - 4),
                new int3(math.max(1, radius * 2 - 1), 1, 2), frame);
        }

        private static int ArchHalfWidth(int radius, int spring, int row)
        {
            if (row < spring) return radius;
            int dy = math.min(radius, row - spring);
            return math.max(0, (int)math.floor(math.sqrt(
                math.max(0, radius * radius - dy * dy))));
        }

        private static void AddShield(IStructureAuthoringSession a,
            int x, int y, int z, int width, int height, byte timber, byte ornament)
        {
            a.Box(new int3(x, y + 4, z), new int3(width, height - 4, 2), timber);
            for (int row = 0; row < 4; row++)
            {
                int inset = (3 - row) / 2;
                a.Box(new int3(x + inset, y + row, z),
                    new int3(width - inset * 2, 1, 2), timber);
            }
            int cx = x + width / 2;
            int cy = y + 7;
            a.Box(new int3(cx, cy - 3, z - 1), new int3(1, 7, 1), ornament);
            a.Box(new int3(cx - 3, cy, z - 1), new int3(7, 1, 1), ornament);
            Diagonal(a, cx - 2, cy - 2, cx + 2, cy + 2, z - 1, ornament);
            Diagonal(a, cx - 2, cy + 2, cx + 2, cy - 2, z - 1, ornament);
        }

        private static void Line(IStructureAuthoringSession a,
            int x0, int y0, int x1, int y1, int z, byte material)
        {
            int dx = x1 - x0;
            int dy = y1 - y0;
            int steps = math.max(math.abs(dx), math.abs(dy));
            if (steps == 0)
            {
                a.Box(new int3(x0, y0, z), new int3(1, 2, TimberDepth), material);
                return;
            }
            for (int i = 0; i <= steps; i++)
                a.Box(new int3(x0 + dx * i / steps, y0 + dy * i / steps, z),
                    new int3(1, 2, TimberDepth), material);
        }

        private static void Diagonal(IStructureAuthoringSession a,
            int x0, int y0, int x1, int y1, int z, byte material)
        {
            int dx = x1 - x0;
            int dy = y1 - y0;
            int steps = math.max(math.abs(dx), math.abs(dy));
            if (steps == 0)
            {
                a.Box(new int3(x0, y0, z), new int3(1, 1, TimberDepth), material);
                return;
            }
            for (int i = 0; i <= steps; i++)
                a.Box(new int3(x0 + dx * i / steps, y0 + dy * i / steps, z),
                    new int3(1, 1, TimberDepth), material);
        }
    }
}
