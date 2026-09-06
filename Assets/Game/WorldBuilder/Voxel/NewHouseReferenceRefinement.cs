using System;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.WorldBuilder.Voxel
{
    /// <summary>
    /// Reference-driven refinement layered on the reusable base house. This remains production
    /// WorldBuilder geometry: no camera, lighting, evidence, or scene policy is owned here.
    /// </summary>
    public static class NewHouseReferenceRefinement
    {
        private const int TimberDepth = 2;

        public static NewHouseReferenceResult AuthorHouse(IStructureAuthoringSession a, int3 o,
            in NewHouseReferenceConfig c, in NewHouseReferencePalette p)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));

            NewHouseReferenceResult result = NewHouseReferenceAuthoring.AuthorHouse(a, o, in c, in p);
            NarrowPortraitGableAndRestoreShoulders(a, o, in c, in p);
            RefineFrontFacade(a, o, in c, in p);
            RebuildReferenceOrnaments(a, o, in c, in p);
            FinishSideAndRearAudits(a, o, in c, in p);
            return result;
        }

        private static void NarrowPortraitGableAndRestoreShoulders(IStructureAuthoringSession a, int3 o,
            in NewHouseReferenceConfig c, in NewHouseReferencePalette p)
        {
            int centre = o.x + c.Width / 2;
            int eave = o.y + c.MainEaveY;
            int ridge = o.y + c.MainRidgeY;
            int frontMinZ = o.z - c.RoofOverhang - 2;
            const int frontDepth = 34;
            const int halfGable = 29;
            int keepMin = centre - halfGable;
            int keepMax = centre + halfGable;

            // Iteration 2 still read as an almost full-width A-frame. Remove only the high outer
            // portrait roof/fill and replace it with the lower transverse shoulders seen in the pin.
            int leftWidth = math.max(1, keepMin - (o.x - c.RoofOverhang - 10));
            int rightStart = keepMax + 1;
            int rightWidth = math.max(1, o.x + c.Width + c.RoofOverhang + 10 - rightStart);
            a.Carve(new int3(o.x - c.RoofOverhang - 10, eave - 2, frontMinZ),
                new int3(leftWidth, ridge - eave + 20, frontDepth));
            a.Carve(new int3(rightStart, eave - 2, frontMinZ),
                new int3(rightWidth, ridge - eave + 20, frontDepth));

            AddShoulderRoof(a, o.x - 12, keepMin + 3, frontMinZ, o.z + 24,
                eave - 11, true, p.Roof, p.Timber);
            AddShoulderRoof(a, keepMax - 2, o.x + c.Width + 12, frontMinZ, o.z + 24,
                eave - 11, false, p.Roof, p.Timber);

            // Restore the left chimney after the outer-gable carve and give it the capped masonry
            // silhouette visible beside the lower blue shoulder.
            int chimneyX = o.x + 3;
            int chimneyZ = o.z + 20;
            int chimneyTop = eave + 24;
            a.Box(new int3(chimneyX, o.y, chimneyZ),
                new int3(10, chimneyTop - o.y, 10), p.Stone);
            a.Box(new int3(chimneyX - 2, chimneyTop - 8, chimneyZ - 2),
                new int3(14, 3, 14), p.Stone);
            a.Box(new int3(chimneyX - 1, chimneyTop - 3, chimneyZ - 1),
                new int3(12, 3, 12), p.Stone);
            a.Box(new int3(chimneyX + 2, chimneyTop, chimneyZ + 2),
                new int3(6, 4, 6), p.Stone);
        }

        private static void AddShoulderRoof(IStructureAuthoringSession a,
            int minX, int maxX, int minZ, int maxZ, int baseY, bool risesRight,
            byte roof, byte timber)
        {
            int span = math.max(1, maxX - minX);
            for (int x = minX; x < maxX; x++)
            {
                int along = risesRight ? x - minX : maxX - 1 - x;
                int y = baseY + (along * 8) / span;
                a.Box(new int3(x, y, minZ), new int3(1, 3, maxZ - minZ), roof);
            }

            int trimX = risesRight ? minX : maxX - 1;
            a.Box(new int3(trimX, baseY - 1, minZ - 1),
                new int3(2, 2, maxZ - minZ + 1), timber);
        }

        private static void RefineFrontFacade(IStructureAuthoringSession a, int3 o,
            in NewHouseReferenceConfig c, in NewHouseReferencePalette p)
        {
            int centre = o.x + c.Width / 2;
            int first = o.y + c.FirstFloorY;
            int upper = o.y + c.UpperFloorY;
            int eave = o.y + c.MainEaveY;
            int front = o.z - 7;

            // Strong masonry portal surround: paired pilasters, spring blocks and a stepped keystone.
            a.Box(new int3(centre - 14, first, front), new int3(4, 26, 3), p.Stone);
            a.Box(new int3(centre + 10, first, front), new int3(4, 26, 3), p.Stone);
            a.Box(new int3(centre - 17, first + 24, front), new int3(7, 4, 3), p.Stone);
            a.Box(new int3(centre + 10, first + 24, front), new int3(7, 4, 3), p.Stone);
            a.Box(new int3(centre - 3, first + 29, front), new int3(7, 5, 3), p.Ornament);
            a.Box(new int3(centre - 1, first + 33, front), new int3(3, 3, 3), p.Stone);

            // Timber belts and corbels around the second register are deliberately broken into
            // smaller components instead of the iteration-2 slab-like horizontal strips.
            a.Box(new int3(o.x + 8, upper - 3, front - 1),
                new int3(c.Width - 16, 2, 2), p.Timber);
            for (int x = o.x + 10; x <= o.x + c.Width - 12; x += 12)
            {
                a.Box(new int3(x, upper - 7, front - 2), new int3(3, 6, 3), p.Timber);
                a.Box(new int3(x - 2, upper - 3, front - 2), new int3(7, 2, 3), p.Timber);
            }

            // Compact carved accents beside the tall upper window and below the gable window.
            a.Box(new int3(centre - 24, upper + 14, front - 1), new int3(5, 7, 2), p.Timber);
            a.Box(new int3(centre + 19, upper + 14, front - 1), new int3(5, 7, 2), p.Timber);
            a.Box(new int3(centre - 3, eave + 3, front - 1), new int3(7, 5, 2), p.Ornament);

            AddDenseFlowerBox(a, centre - 15, upper + 2, front - 2, 30, in p);
            AddDenseFlowerBox(a, centre - 12, eave + 6, front - 2, 24, in p);

            // Reference vegetation forms connected masses wrapping the right facade rather than
            // isolated vertical blobs.
            AddIvyMass(a, o.x + c.Width - 9, first + 3, front - 1, 34, -1, in p);
            AddIvyMass(a, o.x + 1, first + 5, front - 1, 20, 1, in p);
        }

        private static void RebuildReferenceOrnaments(IStructureAuthoringSession a, int3 o,
            in NewHouseReferenceConfig c, in NewHouseReferencePalette p)
        {
            int centre = o.x + c.Width / 2;
            int upper = o.y + c.UpperFloorY;
            int ridge = o.y + c.MainRidgeY;
            int front = o.z - 10;

            // Remove the cross-like iteration-2 crest and replace it with a compact stepped gold
            // pedestal and pointed finial.
            a.Carve(new int3(centre - 7, ridge, o.z + 1), new int3(15, 18, 18));
            a.Box(new int3(centre - 5, ridge + 1, o.z + 5), new int3(10, 2, 7), p.Timber);
            a.Box(new int3(centre - 4, ridge + 3, o.z + 6), new int3(8, 3, 5), p.Ornament);
            a.Box(new int3(centre - 3, ridge + 6, o.z + 7), new int3(6, 3, 3), p.Ornament);
            a.Cone(centre, ridge + 9, o.z + 8, 3, 8, p.Ornament);

            // Rebuild the left banner at a slimmer portrait scale with a gold border and central
            // heraldic motif instead of one oversized flat asterisk.
            int bannerX = o.x - 4;
            a.Carve(new int3(o.x - 12, upper, front - 2), new int3(24, 38, 8));
            a.Box(new int3(bannerX - 2, upper + 28, front), new int3(18, 2, 2), p.Timber);
            a.Box(new int3(bannerX, upper + 5, front + 1), new int3(13, 24, 1), p.Accent);
            a.Box(new int3(bannerX, upper + 5, front), new int3(13, 1, 1), p.Ornament);
            a.Box(new int3(bannerX, upper + 28, front), new int3(13, 1, 1), p.Ornament);
            a.Box(new int3(bannerX, upper + 5, front), new int3(1, 24, 1), p.Ornament);
            a.Box(new int3(bannerX + 12, upper + 5, front), new int3(1, 24, 1), p.Ornament);
            int bx = bannerX + 6;
            int by = upper + 17;
            a.Box(new int3(bx, by - 5, front), new int3(1, 11, 1), p.Ornament);
            a.Box(new int3(bx - 4, by, front), new int3(9, 1, 1), p.Ornament);
            Diagonal(a, bx - 3, by - 3, bx + 3, by + 3, front, p.Ornament);
            Diagonal(a, bx - 3, by + 3, bx + 3, by - 3, front, p.Ornament);

            // Smaller bracketed shield on the right, with a readable hanging gap.
            int bracketX = o.x + c.Width + 1;
            a.Carve(new int3(bracketX - 1, upper + 4, front - 2), new int3(25, 34, 8));
            a.Box(new int3(bracketX, upper + 27, front), new int3(15, 2, 2), p.Timber);
            a.Box(new int3(bracketX + 11, upper + 17, front), new int3(2, 11, 2), p.Timber);
            AddShield(a, bracketX + 7, upper + 6, front + 1, 10, 13, p.Timber, p.Ornament);
        }

        private static void FinishSideAndRearAudits(IStructureAuthoringSession a, int3 o,
            in NewHouseReferenceConfig c, in NewHouseReferencePalette p)
        {
            int first = o.y + c.FirstFloorY;
            int upper = o.y + c.UpperFloorY;
            int rear = o.z + c.Depth + 1;

            // The reference does not specify the hidden rear, but audit views must still show a
            // believable finished house rather than an un-authored plaster box.
            a.Box(new int3(o.x + 5, upper - 2, rear), new int3(c.Width - 10, 2, 2), p.Timber);
            a.Box(new int3(o.x + 5, upper + 25, rear), new int3(c.Width - 10, 2, 2), p.Timber);
            for (int x = o.x + 8; x < o.x + c.Width - 7; x += 16)
                a.Box(new int3(x, upper, rear), new int3(2, 25, 2), p.Timber);

            AddRearWindow(a, o.x + 22, first + 8, rear, 11, 18, in p);
            AddRearWindow(a, o.x + c.Width - 22, first + 8, rear, 11, 18, in p);
            AddRearWindow(a, o.x + c.Width / 2, upper + 7, rear, 13, 20, in p);
        }

        private static void AddRearWindow(IStructureAuthoringSession a, int centreX, int y, int z,
            int width, int height, in NewHouseReferencePalette p)
        {
            int half = width / 2;
            a.Carve(new int3(centreX - half, y, z - 4), new int3(width, height, 8));
            a.Box(new int3(centreX - half, y, z - 1), new int3(width, height, 2), p.Glass);
            a.Box(new int3(centreX - half - 1, y - 1, z - 2), new int3(width + 2, 2, 3), p.Timber);
            a.Box(new int3(centreX - half - 1, y + height, z - 2), new int3(width + 2, 2, 3), p.Timber);
            a.Box(new int3(centreX - half - 1, y, z - 2), new int3(2, height, 3), p.Timber);
            a.Box(new int3(centreX + half, y, z - 2), new int3(2, height, 3), p.Timber);
            a.Box(new int3(centreX, y + 1, z - 3), new int3(1, height - 2, 2), p.Timber);
        }

        private static void AddDenseFlowerBox(IStructureAuthoringSession a,
            int x, int y, int z, int width, in NewHouseReferencePalette p)
        {
            a.Box(new int3(x, y, z), new int3(width, 3, 4), p.Timber);
            a.Box(new int3(x + 1, y + 3, z), new int3(width - 2, 4, 4), p.Foliage);
            for (int i = 2; i < width - 2; i += 2)
            {
                byte blossom = ((i / 2) % 3 == 0) ? p.Accent : p.Flowers;
                a.Box(new int3(x + i, y + 6 + ((i / 2) & 1), z - 1), new int3(2, 2, 2), blossom);
            }
        }

        private static void AddIvyMass(IStructureAuthoringSession a,
            int startX, int startY, int z, int segments, int xDirection,
            in NewHouseReferencePalette p)
        {
            for (int i = 0; i < segments; i++)
            {
                int y = startY + i * 2;
                int x = startX + xDirection * ((i / 5) % 6);
                int size = 3 + (i % 3);
                a.Box(new int3(x, y, z), new int3(size, 4, 2), p.Foliage);
                if ((i & 1) == 0)
                    a.Box(new int3(x - xDirection * 3, y + 2, z - 1), new int3(4, 3, 2), p.Foliage);
            }
        }

        private static void AddShield(IStructureAuthoringSession a,
            int x, int y, int z, int width, int height, byte timber, byte ornament)
        {
            a.Box(new int3(x, y + 4, z), new int3(width, height - 4, 2), timber);
            for (int row = 0; row < 4; row++)
            {
                int inset = (3 - row) / 2;
                a.Box(new int3(x + inset, y + row, z), new int3(width - inset * 2, 1, 2), timber);
            }
            int cx = x + width / 2;
            int cy = y + 7;
            a.Box(new int3(cx, cy - 3, z - 1), new int3(1, 7, 1), ornament);
            a.Box(new int3(cx - 3, cy, z - 1), new int3(7, 1, 1), ornament);
            Diagonal(a, cx - 2, cy - 2, cx + 2, cy + 2, z - 1, ornament);
            Diagonal(a, cx - 2, cy + 2, cx + 2, cy - 2, z - 1, ornament);
        }

        private static void Diagonal(IStructureAuthoringSession a,
            int x0, int y0, int x1, int y1, int z, byte material)
        {
            int dx = x1 - x0;
            int dy = y1 - y0;
            int steps = math.max(math.abs(dx), math.abs(dy));
            for (int i = 0; i <= steps; i++)
                a.Box(new int3(x0 + dx * i / steps, y0 + dy * i / steps, z),
                    new int3(1, 1, TimberDepth), material);
        }
    }
}
