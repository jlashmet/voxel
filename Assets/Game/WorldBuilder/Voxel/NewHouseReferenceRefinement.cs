using System;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.WorldBuilder.Voxel
{
    /// <summary>
    /// Reference-driven production refinement for the pinned ornate house. The base authoring owns
    /// reusable walls/openings/material roles; this layer deliberately replaces the conflicting roof
    /// and facade silhouette after direct built-player comparison. Camera, lighting and site policy
    /// remain outside this composition.
    /// </summary>
    public static class NewHouseReferenceRefinement
    {
        private const int TimberDepth = 2;

        public static NewHouseReferenceResult AuthorHouse(IStructureAuthoringSession a, int3 o,
            in NewHouseReferenceConfig c, in NewHouseReferencePalette p)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));

            NewHouseReferenceResult result = NewHouseReferenceAuthoring.AuthorHouse(a, o, in c, in p);
            ReplaceConflictingRoofComposition(a, o, in c, in p);
            RefineFacadeDepth(a, o, in c, in p);
            RebuildReferenceOrnaments(a, o, in c, in p);
            FinishAuditElevations(a, o, in c, in p);
            return result;
        }

        private static void ReplaceConflictingRoofComposition(IStructureAuthoringSession a, int3 o,
            in NewHouseReferenceConfig c, in NewHouseReferencePalette p)
        {
            int centre = o.x + c.Width / 2;
            int upper = o.y + c.UpperFloorY;
            int eave = o.y + c.MainEaveY;
            int ridge = o.y + c.MainRidgeY;

            // Root-cause correction after iterations 2/3: the old full-width/front-to-back roof and
            // low side-wing helpers remained underneath additive fixes. Remove that entire conflicting
            // upper roof volume before rebuilding the reference silhouette.
            a.Carve(new int3(o.x - 22, upper + 13, o.z - 18),
                new int3(c.Width + 44, ridge - upper + 32, c.Depth + 42));
            a.Carve(new int3(o.x - 20, upper - 8, o.z - 12),
                new int3(20, 22, 42));
            a.Carve(new int3(o.x + c.Width, upper - 8, o.z - 12),
                new int3(20, 22, 42));

            // Lower transverse roof shoulders: broad, low and clearly behind the portrait gable.
            int shoulderEave = upper + 22;
            GableAlongX(a,
                o.x - c.RoofOverhang - 5,
                o.x + c.Width + c.RoofOverhang + 5,
                o.z + 8,
                o.z + c.Depth + 4,
                shoulderEave,
                12,
                3,
                p.Roof);

            // Narrow steep portrait gable. The reference gable occupies about two thirds of the body
            // width and is shallow in depth, so the lower roof shoulders remain visible on both sides.
            const int halfGable = 27;
            int gableMinX = centre - halfGable;
            int gableMaxX = centre + halfGable + 1;
            int gableMinZ = o.z - c.RoofOverhang - 2;
            int gableMaxZ = o.z + 15;
            int portraitEave = upper + 31;
            int portraitRise = math.max(38, ridge - portraitEave);

            GableAlongZ(a, gableMinX, gableMaxX, gableMinZ, gableMaxZ,
                portraitEave, portraitRise, 3, p.Roof);
            FillFrontTriangle(a, centre, o.z - 2, portraitEave + 1,
                halfGable * 2 - 8, portraitRise - 3, p.Plaster);
            Line(a, gableMinX + 2, portraitEave - 1, centre, portraitEave + portraitRise - 2,
                o.z - 6, p.Timber);
            Line(a, gableMaxX - 3, portraitEave - 1, centre, portraitEave + portraitRise - 2,
                o.z - 6, p.Timber);

            // Swept eave tips visible in the reference, without the old deep blue slabs.
            for (int i = 0; i < 7; i++)
            {
                int y = portraitEave - 2 - i / 2;
                a.Box(new int3(gableMinX - 3 - i, y, gableMinZ), new int3(1, 2, 18), p.Roof);
                a.Box(new int3(gableMaxX + 2 + i, y, gableMinZ), new int3(1, 2, 18), p.Roof);
            }

            // Rebuild the left capped chimney after clearing the conflicting roof system.
            int chimneyX = o.x + 3;
            int chimneyZ = o.z + 22;
            int chimneyTop = shoulderEave + 24;
            a.Box(new int3(chimneyX, o.y, chimneyZ),
                new int3(10, chimneyTop - o.y, 10), p.Stone);
            a.Box(new int3(chimneyX - 2, chimneyTop - 8, chimneyZ - 2),
                new int3(14, 3, 14), p.Stone);
            a.Box(new int3(chimneyX - 1, chimneyTop - 3, chimneyZ - 1),
                new int3(12, 3, 12), p.Stone);
            a.Box(new int3(chimneyX + 2, chimneyTop, chimneyZ + 2),
                new int3(6, 4, 6), p.Stone);
        }

        private static void RefineFacadeDepth(IStructureAuthoringSession a, int3 o,
            in NewHouseReferenceConfig c, in NewHouseReferencePalette p)
        {
            int centre = o.x + c.Width / 2;
            int first = o.y + c.FirstFloorY;
            int upper = o.y + c.UpperFloorY;
            int portraitEave = upper + 31;
            int front = o.z - 7;

            // Strong three-dimensional portal surround and corbelled register break.
            a.Box(new int3(centre - 14, first, front), new int3(4, 26, 3), p.Stone);
            a.Box(new int3(centre + 10, first, front), new int3(4, 26, 3), p.Stone);
            a.Box(new int3(centre - 17, first + 24, front), new int3(7, 4, 3), p.Stone);
            a.Box(new int3(centre + 10, first + 24, front), new int3(7, 4, 3), p.Stone);
            a.Box(new int3(centre - 3, first + 29, front), new int3(7, 5, 3), p.Ornament);

            a.Box(new int3(o.x + 8, upper - 3, front - 1),
                new int3(c.Width - 16, 2, 2), p.Timber);
            for (int x = o.x + 10; x <= o.x + c.Width - 12; x += 12)
            {
                a.Box(new int3(x, upper - 7, front - 2), new int3(3, 6, 3), p.Timber);
                a.Box(new int3(x - 2, upper - 3, front - 2), new int3(7, 2, 3), p.Timber);
            }

            // Dense flower boxes at the two reference-visible upper openings.
            AddDenseFlowerBox(a, centre - 15, upper + 2, front - 2, 30, in p);
            AddDenseFlowerBox(a, centre - 12, portraitEave + 7, front - 2, 24, in p);

            // Connected climbing masses rather than isolated green columns.
            AddIvyMass(a, o.x + c.Width - 8, first + 2, front - 1, 35, -1, in p);
            AddIvyMass(a, o.x + 2, first + 5, front - 1, 18, 1, in p);
        }

        private static void RebuildReferenceOrnaments(IStructureAuthoringSession a, int3 o,
            in NewHouseReferenceConfig c, in NewHouseReferencePalette p)
        {
            int centre = o.x + c.Width / 2;
            int upper = o.y + c.UpperFloorY;
            int portraitEave = upper + 31;
            int portraitRise = math.max(38, o.y + c.MainRidgeY - portraitEave);
            int crestBase = portraitEave + portraitRise;
            int front = o.z - 10;

            // Compact stepped crest and pointed finial.
            a.Box(new int3(centre - 4, crestBase, o.z + 3), new int3(8, 2, 6), p.Timber);
            a.Box(new int3(centre - 3, crestBase + 2, o.z + 4), new int3(6, 3, 4), p.Ornament);
            a.Box(new int3(centre - 2, crestBase + 5, o.z + 5), new int3(4, 3, 2), p.Ornament);
            a.Cone(centre, crestBase + 8, o.z + 6, 2, 7, p.Ornament);

            // Slim left banner with border and restrained heraldic mark.
            int bannerX = o.x - 3;
            a.Carve(new int3(o.x - 12, upper + 2, front - 2), new int3(24, 34, 8));
            a.Box(new int3(bannerX - 2, upper + 27, front), new int3(17, 2, 2), p.Timber);
            a.Box(new int3(bannerX, upper + 7, front + 1), new int3(12, 20, 1), p.Accent);
            a.Box(new int3(bannerX, upper + 7, front), new int3(12, 1, 1), p.Ornament);
            a.Box(new int3(bannerX, upper + 26, front), new int3(12, 1, 1), p.Ornament);
            a.Box(new int3(bannerX, upper + 7, front), new int3(1, 20, 1), p.Ornament);
            a.Box(new int3(bannerX + 11, upper + 7, front), new int3(1, 20, 1), p.Ornament);
            int bx = bannerX + 6;
            int by = upper + 17;
            a.Box(new int3(bx, by - 4, front), new int3(1, 9, 1), p.Ornament);
            a.Box(new int3(bx - 3, by, front), new int3(7, 1, 1), p.Ornament);
            Diagonal(a, bx - 2, by - 2, bx + 2, by + 2, front, p.Ornament);
            Diagonal(a, bx - 2, by + 2, bx + 2, by - 2, front, p.Ornament);

            // Compact right hanging sign, separated from the wall by a bracket.
            int bracketX = o.x + c.Width + 1;
            a.Carve(new int3(bracketX - 1, upper + 5, front - 2), new int3(22, 30, 8));
            a.Box(new int3(bracketX, upper + 26, front), new int3(14, 2, 2), p.Timber);
            a.Box(new int3(bracketX + 10, upper + 17, front), new int3(2, 10, 2), p.Timber);
            AddShield(a, bracketX + 6, upper + 7, front + 1, 9, 12, p.Timber, p.Ornament);
        }

        private static void FinishAuditElevations(IStructureAuthoringSession a, int3 o,
            in NewHouseReferenceConfig c, in NewHouseReferencePalette p)
        {
            int first = o.y + c.FirstFloorY;
            int upper = o.y + c.UpperFloorY;
            int rear = o.z + c.Depth + 1;

            a.Box(new int3(o.x + 5, upper - 2, rear), new int3(c.Width - 10, 2, 2), p.Timber);
            a.Box(new int3(o.x + 5, upper + 25, rear), new int3(c.Width - 10, 2, 2), p.Timber);
            for (int x = o.x + 8; x < o.x + c.Width - 7; x += 16)
                a.Box(new int3(x, upper, rear), new int3(2, 25, 2), p.Timber);

            AddRearWindow(a, o.x + 22, first + 8, rear, 11, 18, in p);
            AddRearWindow(a, o.x + c.Width - 22, first + 8, rear, 11, 18, in p);
            AddRearWindow(a, o.x + c.Width / 2, upper + 7, rear, 13, 20, in p);

            // Side-elevation timber belts stop the audit views reading as plain boxes.
            int left = o.x - 1;
            int right = o.x + c.Width + 1;
            a.Box(new int3(left, upper - 2, o.z + 8), new int3(2, 2, c.Depth - 14), p.Timber);
            a.Box(new int3(right, upper - 2, o.z + 8), new int3(2, 2, c.Depth - 14), p.Timber);
            for (int z = o.z + 12; z < o.z + c.Depth - 6; z += 16)
            {
                a.Box(new int3(left, upper, z), new int3(2, 22, 2), p.Timber);
                a.Box(new int3(right, upper, z), new int3(2, 22, 2), p.Timber);
            }
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

        private static void GableAlongX(IStructureAuthoringSession a,
            int minX, int maxX, int minZ, int maxZ, int eave, int rise, int thickness, byte material)
        {
            int span = math.max(2, maxZ - minZ - 1);
            for (int z = minZ; z < maxZ; z++)
            {
                int edge = math.min(z - minZ, maxZ - 1 - z);
                int y = eave + (rise * edge * 2 + span / 2) / span;
                a.Box(new int3(minX, y, z), new int3(maxX - minX, thickness, 1), material);
            }
        }

        private static void GableAlongZ(IStructureAuthoringSession a,
            int minX, int maxX, int minZ, int maxZ, int eave, int rise, int thickness, byte material)
        {
            int span = math.max(2, maxX - minX - 1);
            for (int x = minX; x < maxX; x++)
            {
                int edge = math.min(x - minX, maxX - 1 - x);
                int y = eave + (rise * edge * 2 + span / 2) / span;
                a.Box(new int3(x, y, minZ), new int3(1, thickness, maxZ - minZ), material);
            }
        }

        private static void FillFrontTriangle(IStructureAuthoringSession a,
            int centreX, int frontZ, int eaveY, int width, int rise, byte material)
        {
            int half = width / 2;
            for (int row = 0; row < rise; row++)
            {
                int rowHalf = math.max(1, half * (rise - row) / rise);
                a.Box(new int3(centreX - rowHalf, eaveY + row, frontZ),
                    new int3(rowHalf * 2 + 1, 1, 3), material);
            }
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
