using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.WorldBuilder.Voxel
{
    /// <summary>
    /// Late reference-only finish pass applied after structural refinement. Camera, lighting, site
    /// policy, and material ownership remain outside reusable house authoring.
    /// </summary>
    internal static class NewHouseReferenceFinishPass
    {
        private const int TimberDepth = 2;

        public static void Apply(IStructureAuthoringSession a, int3 o,
            in NewHouseReferenceConfig c, in NewHouseReferencePalette p)
        {
            RefinePortraitGable(a, o, in c, in p);
            RemoveObsoleteRearGable(a, o, in c);
            RefineMiddleFacadeOpening(a, o, in c, in p);
            NewHouseReferenceEntryPortalFinish.Apply(a, o, in c, in p);
            ExtendReferenceChimney(a, o, in c, in p);
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
            int clearRise = math.max(38, ridge - portraitEave);
            int portraitRise = ReferencePortraitRise(in c, clearRise);
            int portraitRidge = portraitEave + portraitRise;

            RebuildSweptPortraitShell(a, o, in c, in p);

            FillArch(a, centre, eave + 10, front, 15, 21, p.Plaster);
            ArchedPanel(a, centre, eave + 12, front, 11, 17,
                p.Glass, p.Timber, p.Timber);
            AddDenseFlowerBox(a, centre - 12, portraitEave + 7, front - 2, 24, in p);

            a.Box(new int3(centre - 20, portraitEave + 4, front - 4),
                new int3(41, 2, TimberDepth), p.Timber);
            // Keep structural timber outside the compact high arch. A former 29-voxel belt at
            // portraitEave + 27 crossed the glazing and turned the reference arch into a flat cross.
            a.Box(new int3(centre - 1, portraitEave + 4, front - 4),
                new int3(2, 6, TimberDepth), p.Timber);
            a.Box(new int3(centre - 1, eave + 31, front - 4),
                new int3(2, math.max(4, portraitRidge - (eave + 31)), TimberDepth), p.Timber);

            Line(a, centre - 19, portraitEave + 5, centre - 8, portraitEave + 18,
                front - 4, p.Timber);
            Line(a, centre + 19, portraitEave + 5, centre + 8, portraitEave + 18,
                front - 4, p.Timber);
            Line(a, centre - 13, portraitEave + 28, centre - 4, portraitEave + 38,
                front - 4, p.Timber);
            Line(a, centre + 13, portraitEave + 28, centre + 4, portraitEave + 38,
                front - 4, p.Timber);
        }

        private static void RefineMiddleFacadeOpening(IStructureAuthoringSession a, int3 o,
            in NewHouseReferenceConfig c, in NewHouseReferencePalette p)
        {
            int centre = o.x + c.Width / 2;
            int upper = o.y + c.UpperFloorY;
            int front = o.z - 2;

            a.Carve(new int3(centre - 21, upper + 4, front - 6), new int3(43, 31, 10));
            a.Box(new int3(centre - 20, upper + 4, front - 4), new int3(41, 31, 7), p.Plaster);

            ArchedPanel(a, centre, upper + 8, front, 13, 19,
                p.Glass, p.Timber, p.Timber);
            AddCompactShutters(a, centre, upper + 9, front, 13, 17, p.Accent, p.Timber);
            AddDenseFlowerBox(a, centre - 11, upper + 4, front - 2, 22, in p);

            // The reference keeps the compact shuttered arch in an open plaster field bounded by
            // the facade's structural belts. Do not rebuild the obsolete room-sized timber cage
            // around this window; its tall jambs and diagonals dominated the portrait hierarchy.
        }

        private static void RebuildSweptPortraitShell(IStructureAuthoringSession a, int3 o,
            in NewHouseReferenceConfig c, in NewHouseReferencePalette p)
        {
            int centre = o.x + c.Width / 2;
            int upper = o.y + c.UpperFloorY;
            int ridge = o.y + c.MainRidgeY;
            int portraitEave = upper + 31;
            int clearRise = math.max(38, ridge - portraitEave);
            int rise = ReferencePortraitRise(in c, clearRise);
            int front = o.z - 2;
            int roofZ = o.z - c.RoofOverhang - 2;

            const int clearHalf = 34;
            int clearDepth = c.RoofOverhang + 20;
            a.Carve(new int3(centre - clearHalf, portraitEave, roofZ - 1),
                new int3(clearHalf * 2 + 1, clearRise + 8, clearDepth));

            const int baseHalf = 29;
            const int apexHalf = 2;
            const int roofDepth = 20;
            for (int row = 0; row <= rise; row++)
            {
                float t = row / (float)math.max(1, rise);
                float remaining = 1f - math.saturate(t);
                int half = apexHalf + (int)math.round(
                    (baseHalf - apexHalf) * math.pow(remaining, 1.45f));
                if (row < 6)
                    half += (6 - row + 1) / 2;

                int y = portraitEave + row;
                int shellHalf = math.max(1, half - 2);
                a.Box(new int3(centre - shellHalf, y, front - 1),
                    new int3(shellHalf * 2 + 1, 1, 6), p.Plaster);
                a.Box(new int3(centre - half - 2, y, roofZ),
                    new int3(4, 2, roofDepth), p.Roof);
                a.Box(new int3(centre + half - 1, y, roofZ),
                    new int3(4, 2, roofDepth), p.Roof);
                a.Box(new int3(centre - half + 1, y, front - 5),
                    new int3(2, 2, TimberDepth), p.Timber);
                a.Box(new int3(centre + half - 2, y, front - 5),
                    new int3(2, 2, TimberDepth), p.Timber);
            }

            int sweptRoot = baseHalf + 3;
            for (int i = 1; i <= 9; i++)
            {
                int drop = (i * i + 10) / 20;
                int y = portraitEave - 1 - drop;
                int left = centre - sweptRoot - i;
                int right = centre + sweptRoot + i;
                a.Box(new int3(left, y, roofZ), new int3(1, 2, 18), p.Roof);
                a.Box(new int3(right, y, roofZ), new int3(1, 2, 18), p.Roof);
                a.Box(new int3(left, y - 1, front - 5),
                    new int3(1, 2, TimberDepth), p.Timber);
                a.Box(new int3(right, y - 1, front - 5),
                    new int3(1, 2, TimberDepth), p.Timber);
            }
        }

        private static int ReferencePortraitRise(in NewHouseReferenceConfig c, int clearRise)
        {
            int widthDrivenRise = math.max(38, c.Width / 2 - 2);
            return math.min(clearRise, widthDrivenRise);
        }

        private static void RemoveObsoleteRearGable(IStructureAuthoringSession a, int3 o,
            in NewHouseReferenceConfig c)
        {
            int upper = o.y + c.UpperFloorY;
            int ridge = o.y + c.MainRidgeY;
            int rear = o.z + c.Depth + 1;
            int clearY = upper + 29;
            int clearHeight = math.max(1, ridge - clearY + 8);

            // FinishAuditElevations historically filled a full-height rear triangle to the obsolete
            // global ridge. The transverse shoulder roof reaches the rear near this wall's eave, so
            // that triangle protrudes through the roof in audit views instead of closing a real gap.
            a.Carve(new int3(o.x + 6, clearY, rear - 4),
                new int3(c.Width - 12, clearHeight, 8));
        }

        private static void ExtendReferenceChimney(IStructureAuthoringSession a, int3 o,
            in NewHouseReferenceConfig c, in NewHouseReferencePalette p)
        {
            int upper = o.y + c.UpperFloorY;
            int ridge = o.y + c.MainRidgeY;
            int x = o.x + 3;
            int z = o.z + 22;
            int baseY = upper + 14;
            int top = math.max(baseY + 12, ridge - 10);

            a.Box(new int3(x, baseY, z), new int3(10, top - baseY, 10), p.Stone);
            a.Box(new int3(x - 1, top - 15, z - 1), new int3(12, 2, 12), p.Stone);
            a.Box(new int3(x - 2, top - 6, z - 2), new int3(14, 3, 14), p.Stone);
            a.Box(new int3(x - 1, top - 2, z - 1), new int3(12, 2, 12), p.Stone);
            a.Box(new int3(x + 2, top, z + 2), new int3(6, 4, 6), p.Stone);
        }

        private static void ReplaceOversizedCrest(IStructureAuthoringSession a, int3 o,
            in NewHouseReferenceConfig c, in NewHouseReferencePalette p)
        {
            int centre = o.x + c.Width / 2;
            int upper = o.y + c.UpperFloorY;
            int ridge = o.y + c.MainRidgeY;
            int portraitEave = upper + 31;
            int clearRise = math.max(38, ridge - portraitEave);
            int portraitRidge = portraitEave + ReferencePortraitRise(in c, clearRise);
            int z = o.z + 5;

            a.Carve(new int3(centre - 8, portraitRidge + 1, o.z),
                new int3(17, ridge - portraitRidge + 18, 16));
            a.Box(new int3(centre - 2, portraitRidge + 1, z), new int3(5, 2, 5), p.Timber);
            a.Box(new int3(centre - 2, portraitRidge + 3, z + 1), new int3(5, 2, 3), p.Ornament);
            a.Box(new int3(centre - 1, portraitRidge + 5, z + 1), new int3(3, 3, 3), p.Ornament);
            a.Cone(centre, portraitRidge + 8, z + 2, 2, 6, p.Ornament);
        }

        private static void RebuildHangingDetails(IStructureAuthoringSession a, int3 o,
            in NewHouseReferenceConfig c, in NewHouseReferencePalette p)
        {
            int upper = o.y + c.UpperFloorY;
            int front = o.z - 10;

            int right = o.x + c.Width;
            a.Carve(new int3(right, upper + 4, front - 2), new int3(24, 32, 8));
            int bracketX = right - 2;
            a.Box(new int3(bracketX, upper + 27, front), new int3(12, 2, 2), p.Timber);
            a.Box(new int3(bracketX + 9, upper + 17, front), new int3(2, 11, 2), p.Timber);
            AddShield(a, bracketX + 5, upper + 8, front + 1, 8, 11, p.Timber, p.Ornament);

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

        private static void AddCompactShutters(IStructureAuthoringSession a,
            int centreX, int y, int frontZ, int windowWidth, int height,
            byte accent, byte timber)
        {
            int half = windowWidth / 2;
            const int shutterWidth = 4;
            int leftX = centreX - half - shutterWidth - 2;
            int rightX = centreX + half + 3;

            a.Box(new int3(leftX, y, frontZ - 5), new int3(shutterWidth, height, 2), accent);
            a.Box(new int3(rightX, y, frontZ - 5), new int3(shutterWidth, height, 2), accent);
            for (int yOffset = 4; yOffset < height - 1; yOffset += 5)
            {
                a.Box(new int3(leftX, y + yOffset, frontZ - 6),
                    new int3(shutterWidth, 1, 2), timber);
                a.Box(new int3(rightX, y + yOffset, frontZ - 6),
                    new int3(shutterWidth, 1, 2), timber);
            }
        }

        private static void AddDenseFlowerBox(IStructureAuthoringSession a,
            int x, int y, int z, int width, in NewHouseReferencePalette p)
        {
            a.Box(new int3(x, y, z), new int3(width, 3, 4), p.Timber);
            a.Box(new int3(x + 1, y + 3, z), new int3(width - 2, 4, 4), p.Foliage);
            for (int i = 2; i < width - 2; i += 2)
            {
                byte blossom = ((i / 2) % 3 == 0) ? p.Accent : p.Flowers;
                a.Box(new int3(x + i, y + 6 + ((i / 2) & 1), z - 1),
                    new int3(2, 2, 2), blossom);
            }
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
                a.Carve(new int3(centreX - half, y + row, frontZ - 4),
                    new int3(half * 2 + 1, 1, 9));

                // The reference windows are recessed panes held by a substantial timber frame,
                // not a full-width flat glass slab. Keep one voxel of visible inner frame around
                // the pane contour so the arch reads as layered joinery at 10 cm resolution.
                int panelHalf = math.max(0, half - 1);
                a.Box(new int3(centreX - panelHalf, y + row, frontZ + 1),
                    new int3(panelHalf * 2 + 1, 1, 2), panel);
                if (half > 0)
                {
                    a.Box(new int3(centreX - half, y + row, frontZ - 4),
                        new int3(1, 1, 2), frame);
                    a.Box(new int3(centreX + half, y + row, frontZ - 4),
                        new int3(1, 1, 2), frame);
                }

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
