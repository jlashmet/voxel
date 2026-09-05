using System;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.WorldBuilder.Voxel
{
    public readonly struct NewHouseReferencePalette
    {
        public readonly byte Plaster, Timber, Roof, Stone, Glass, Door, Accent, Ground, Flowers, Foliage;

        public NewHouseReferencePalette(byte plaster, byte timber, byte roof, byte stone, byte glass,
            byte door, byte accent, byte ground, byte flowers, byte foliage)
        {
            Plaster = plaster; Timber = timber; Roof = roof; Stone = stone; Glass = glass;
            Door = door; Accent = accent; Ground = ground; Flowers = flowers; Foliage = foliage;
        }
    }

    /// <summary>Integer-voxel dimensions for the supplied 10 cm reference cottage.</summary>
    public readonly struct NewHouseReferenceConfig
    {
        public readonly int Width, Depth, FoundationHeight, FirstFloorHeight, UpperFloorHeight;
        public readonly int WallThickness, MainRoofRise, RoofThickness, RoofOverhang;

        public NewHouseReferenceConfig(int width, int depth, int foundationHeight, int firstFloorHeight,
            int upperFloorHeight, int wallThickness, int mainRoofRise, int roofThickness, int roofOverhang)
        {
            if (width < 72 || depth < 48 || foundationHeight <= 0 || firstFloorHeight <= 0 ||
                upperFloorHeight <= 0 || wallThickness <= 0 || mainRoofRise <= 0 ||
                roofThickness <= 0 || roofOverhang < 0)
                throw new ArgumentOutOfRangeException(nameof(width));
            Width = width; Depth = depth; FoundationHeight = foundationHeight;
            FirstFloorHeight = firstFloorHeight; UpperFloorHeight = upperFloorHeight;
            WallThickness = wallThickness; MainRoofRise = mainRoofRise;
            RoofThickness = roofThickness; RoofOverhang = roofOverhang;
        }

        public int FirstFloorY => FoundationHeight;
        public int UpperFloorY => FoundationHeight + FirstFloorHeight;
        public int MainEaveY => UpperFloorY + UpperFloorHeight;
        public int MainRidgeY => MainEaveY + MainRoofRise;

        // Direct reference comparison: the cottage is compact and tall, but not the oversized
        // three-storey plate produced by the earlier 104x72 / 48-rise interpretation.
        public static NewHouseReferenceConfig Default => new(88, 60, 8, 28, 28, 3, 34, 3, 5);
    }

    public readonly struct NewHouseReferenceResult
    {
        public readonly int3 Min, MaxExclusive;
        public readonly int DoorCentreX, FrontZ, RidgeY;
        public NewHouseReferenceResult(int3 min, int3 max, int doorX, int frontZ, int ridgeY)
        { Min = min; MaxExclusive = max; DoorCentreX = doorX; FrontZ = frontZ; RidgeY = ridgeY; }
    }

    /// <summary>
    /// Reusable production WorldBuilder authoring for the supplied half-timbered cottage.
    /// Material IDs are opaque; reference-only site/camera/light policy remains outside AuthorHouse.
    /// </summary>
    public static class NewHouseReferenceAuthoring
    {
        private const int TimberDepth = 2;
        private const int UpperFacadeProjection = 2;

        public static NewHouseReferenceResult AuthorHouse(IStructureAuthoringSession a, int3 o,
            in NewHouseReferenceConfig c, in NewHouseReferencePalette p)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));

            int firstY = o.y + c.FirstFloorY;
            int upperY = o.y + c.UpperFloorY;
            int eaveY = o.y + c.MainEaveY;
            int ridgeY = o.y + c.MainRidgeY;
            int centreX = o.x + c.Width / 2;
            int upperFrontZ = o.z - UpperFacadeProjection;

            // Reference massing: pale stone ground storey, modest jettied Tudor upper storey,
            // dominant steep front gable and small blue cross-roof shoulders.
            a.Box(o, new int3(c.Width, c.FoundationHeight, c.Depth), p.Stone);
            a.HollowBox(new int3(o.x, firstY, o.z),
                new int3(c.Width, c.FirstFloorHeight, c.Depth),
                c.WallThickness, p.Stone, false, true);
            a.HollowBox(new int3(o.x - UpperFacadeProjection, upperY, upperFrontZ),
                new int3(c.Width + UpperFacadeProjection * 2, c.UpperFloorHeight,
                    c.Depth + UpperFacadeProjection * 2),
                c.WallThickness, p.Plaster, true, false);

            int crossEave = upperY + 17;
            GableX(a,
                o.x - c.RoofOverhang - 3,
                o.x + c.Width + c.RoofOverhang + 3,
                o.z + 12,
                o.z + c.Depth + c.RoofOverhang,
                crossEave,
                16,
                c.RoofThickness,
                p.Roof);

            GableZ(a,
                o.x - c.RoofOverhang,
                o.x + c.Width + c.RoofOverhang,
                o.z - c.RoofOverhang,
                o.z + c.Depth + c.RoofOverhang,
                eaveY,
                c.MainRoofRise,
                c.RoofThickness,
                p.Roof);
            FillFrontGable(a, centreX, upperFrontZ, eaveY,
                c.Width - 8, c.MainRoofRise - 2, p.Plaster);

            AddChimney(a, o, in c, p.Stone);
            AddTimberFrame(a, o, in c, p.Timber);
            AddReferenceOpenings(a, o, in c, in p);
            AddFacadeDetails(a, o, in c, in p);
            AddRidgeFinials(a, centreX, ridgeY, upperFrontZ, p.Timber);

            return new NewHouseReferenceResult(
                new int3(o.x - c.RoofOverhang - 3, o.y, o.z - c.RoofOverhang - 10),
                new int3(o.x + c.Width + c.RoofOverhang + 4,
                    ridgeY + 9,
                    o.z + c.Depth + c.RoofOverhang + 1),
                centreX,
                upperFrontZ,
                ridgeY);
        }

        /// <summary>Reference-shot site dressing, deliberately separable from reusable house geometry.</summary>
        public static void AuthorReferenceSite(IStructureAuthoringSession a, int3 o,
            in NewHouseReferenceConfig c, in NewHouseReferencePalette p)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            a.Box(new int3(o.x - 38, o.y - 10, o.z - 52),
                new int3(c.Width + 82, 10, c.Depth + 100), p.Ground);

            int doorX = o.x + c.Width / 2;
            for (int z = o.z - 15; z >= o.z - 48; z -= 7)
            {
                int bend = ((o.z - z) / 7) % 4;
                a.Disc(doorX + (bend == 1 ? -2 : bend == 3 ? 2 : 0), o.y, z, 6, p.Stone);
            }

            Shrub(a, o.x + 14, o.y, o.z - 10, 5, p.Foliage);
            Shrub(a, o.x + c.Width - 13, o.y, o.z - 10, 5, p.Foliage);
            Shrub(a, o.x + c.Width + 13, o.y, o.z + 18, 5, p.Foliage);
            Shrub(a, o.x - 11, o.y, o.z + 21, 5, p.Foliage);
        }

        private static void AddReferenceOpenings(IStructureAuthoringSession a, int3 o,
            in NewHouseReferenceConfig c, in NewHouseReferencePalette p)
        {
            int centreX = o.x + c.Width / 2;
            int firstY = o.y + c.FirstFloorY;
            int upperY = o.y + c.UpperFloorY;
            int eaveY = o.y + c.MainEaveY;
            int upperFrontZ = o.z - UpperFacadeProjection;

            // The supplied front plate has a simple central timber door and two compact blue-framed
            // lower windows. The earlier giant arched openings were a visual misread of the source.
            RectDoor(a, centreX, firstY + 1, o.z, 13, 21, p.Door, p.Timber);
            RectWindow(a, centreX - 27, firstY + 6, o.z, 11, 15,
                p.Glass, p.Accent, p.Stone);
            RectWindow(a, centreX + 27, firstY + 6, o.z, 11, 15,
                p.Glass, p.Accent, p.Stone);

            AddUpperShutterBank(a, centreX, upperY + 5, upperFrontZ,
                p.Glass, p.Accent, p.Timber);

            // Only the small high gable window carries the narrow arched silhouette visible in the
            // reference; keeping it small preserves the large plaster/timber triangle around it.
            ArchedWindow(a, centreX, eaveY + 7, upperFrontZ, 11, 14,
                p.Glass, p.Timber);
        }

        private static void RectDoor(IStructureAuthoringSession a,
            int centreX, int y, int frontZ, int width, int height, byte door, byte trim)
        {
            int half = width / 2;
            a.Carve(new int3(centreX - half, y, frontZ - 1), new int3(width, height, 7));
            a.Box(new int3(centreX - half, y, frontZ + 1), new int3(width, height, 2), door);

            a.Box(new int3(centreX - half - 2, y - 1, frontZ - 1),
                new int3(2, height + 3, 2), trim);
            a.Box(new int3(centreX + half + 1, y - 1, frontZ - 1),
                new int3(2, height + 3, 2), trim);
            a.Box(new int3(centreX - half - 2, y + height, frontZ - 1),
                new int3(width + 5, 2, 2), trim);
        }

        private static void RectWindow(IStructureAuthoringSession a,
            int centreX, int y, int frontZ, int width, int height,
            byte panel, byte frame, byte surround)
        {
            int half = width / 2;
            a.Carve(new int3(centreX - half - 1, y - 1, frontZ - 1),
                new int3(width + 2, height + 2, 7));

            // Restore a compact structural reveal, then inset the panel behind the wall plane.
            a.Box(new int3(centreX - half - 1, y - 1, frontZ - 1),
                new int3(width + 2, 1, 2), surround);
            a.Box(new int3(centreX - half - 1, y + height, frontZ - 1),
                new int3(width + 2, 1, 2), surround);
            a.Box(new int3(centreX - half - 1, y, frontZ - 1),
                new int3(1, height, 2), surround);
            a.Box(new int3(centreX + half + 1, y, frontZ - 1),
                new int3(1, height, 2), surround);
            a.Box(new int3(centreX - half, y, frontZ + 1),
                new int3(width, height, 2), panel);

            // Blue painted frame/muntins are the high-contrast window identity in the reference.
            a.Box(new int3(centreX - half, y, frontZ - 2), new int3(1, height, 2), frame);
            a.Box(new int3(centreX + half, y, frontZ - 2), new int3(1, height, 2), frame);
            a.Box(new int3(centreX - half, y, frontZ - 2), new int3(width, 1, 2), frame);
            a.Box(new int3(centreX - half, y + height - 1, frontZ - 2),
                new int3(width, 1, 2), frame);
            a.Box(new int3(centreX, y + 1, frontZ - 2),
                new int3(1, height - 2, 2), frame);
            a.Box(new int3(centreX - half + 1, y + height / 2, frontZ - 2),
                new int3(width - 2, 1, 2), frame);
        }

        private static void AddUpperShutterBank(IStructureAuthoringSession a,
            int centreX, int y, int frontZ, byte panel, byte accent, byte timber)
        {
            const int bankWidth = 40;
            const int bankHeight = 17;
            int left = centreX - bankWidth / 2;

            a.Carve(new int3(left, y, frontZ - 1), new int3(bankWidth, bankHeight, 7));
            a.Box(new int3(left + 2, y + 1, frontZ + 1),
                new int3(bankWidth - 4, bankHeight - 2, 2), panel);

            // Four narrow blue shutter/window leaves separated by light timber match the source's
            // central blue bank without turning half the facade into one grey glass opening.
            const int leafWidth = 7;
            int[] offsets = { 3, 12, 21, 30 };
            for (int i = 0; i < offsets.Length; i++)
                a.Box(new int3(left + offsets[i], y + 1, frontZ - 3),
                    new int3(leafWidth, bankHeight - 2, 2), accent);

            a.Box(new int3(left - 2, y - 2, frontZ - 2),
                new int3(bankWidth + 4, 2, 3), timber);
            a.Box(new int3(left - 2, y + bankHeight, frontZ - 2),
                new int3(bankWidth + 4, 2, 3), timber);
            for (int x = left + 10; x < left + bankWidth; x += 9)
                a.Box(new int3(x, y, frontZ - 3), new int3(2, bankHeight, 2), timber);
        }

        private static void ArchedWindow(IStructureAuthoringSession a,
            int centreX, int y, int frontZ, int width, int height, byte panel, byte timber)
        {
            int radius = math.max(2, width / 2);
            int spring = height - radius;
            for (int row = 0; row < height; row++)
            {
                int half = row < spring
                    ? radius
                    : math.max(1, (int)math.floor(math.sqrt(
                        math.max(0, radius * radius - (row - spring) * (row - spring)))));
                a.Carve(new int3(centreX - half, y + row, frontZ - 1),
                    new int3(half * 2 + 1, 1, 7));
                a.Box(new int3(centreX - half, y + row, frontZ + 1),
                    new int3(half * 2 + 1, 1, 2), panel);
            }

            // Small cross mullion and sill; the surrounding Tudor A-frame supplies the outer arch.
            a.Box(new int3(centreX, y + 1, frontZ - 2),
                new int3(1, height - 3, 2), timber);
            a.Box(new int3(centreX - radius, y + spring / 2, frontZ - 2),
                new int3(radius * 2 + 1, 1, 2), timber);
            a.Box(new int3(centreX - radius - 1, y - 2, frontZ - 2),
                new int3(radius * 2 + 3, 2, 3), timber);
        }

        private static void AddTimberFrame(IStructureAuthoringSession a, int3 o,
            in NewHouseReferenceConfig c, byte timber)
        {
            int front = o.z - UpperFacadeProjection - 2;
            int upper = o.y + c.UpperFloorY;
            int eave = o.y + c.MainEaveY;
            int ridge = o.y + c.MainRidgeY;
            int centre = o.x + c.Width / 2;

            a.Box(new int3(o.x - 3, upper - 2, front),
                new int3(c.Width + 6, 3, TimberDepth), timber);
            a.Box(new int3(o.x - 3, eave - 3, front),
                new int3(c.Width + 6, 3, TimberDepth), timber);
            Post(a, o.x + 5, upper, eave, front, timber);
            Post(a, centre - 1, upper, eave, front, timber);
            Post(a, o.x + c.Width - 8, upper, eave, front, timber);

            Line(a, o.x + 5, eave - 2, centre, ridge - 2, front, timber);
            Line(a, o.x + c.Width - 6, eave - 2, centre, ridge - 2, front, timber);
            Post(a, centre - 1, eave, ridge - 2, front, timber);

            // Small braces read as Tudor structure without obscuring the windows.
            Line(a, o.x + 8, upper + 4, o.x + 21, upper + 16, front, timber);
            Line(a, o.x + c.Width - 9, upper + 4, o.x + c.Width - 22, upper + 16, front, timber);
        }

        private static void FillFrontGable(IStructureAuthoringSession a,
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

        private static void AddChimney(IStructureAuthoringSession a, int3 o,
            in NewHouseReferenceConfig c, byte stone)
        {
            int x = o.x + 6;
            int z = o.z + 19;
            int h = c.MainRidgeY + 3;
            a.Box(new int3(x, o.y, z), new int3(9, h, 10), stone);
            a.Box(new int3(x - 1, o.y + h - 3, z - 1), new int3(11, 3, 12), stone);
            a.Box(new int3(x + 1, o.y + h, z + 2), new int3(3, 5, 3), stone);
            a.Box(new int3(x + 6, o.y + h, z + 5), new int3(3, 5, 3), stone);
        }

        private static void AddRidgeFinials(IStructureAuthoringSession a,
            int centreX, int ridgeY, int frontZ, byte timber)
        {
            // The source has two small timber ridge ornaments, not the earlier pagoda-sized cone.
            a.Box(new int3(centreX - 4, ridgeY + 1, frontZ + 7), new int3(2, 7, 2), timber);
            a.Box(new int3(centreX + 3, ridgeY + 3, frontZ + 10), new int3(2, 7, 2), timber);
            a.Box(new int3(centreX - 5, ridgeY + 5, frontZ + 7), new int3(4, 1, 2), timber);
            a.Box(new int3(centreX + 2, ridgeY + 7, frontZ + 10), new int3(4, 1, 2), timber);
        }

        private static void AddFacadeDetails(IStructureAuthoringSession a, int3 o,
            in NewHouseReferenceConfig c, in NewHouseReferencePalette p)
        {
            int centre = o.x + c.Width / 2;
            int first = o.y + c.FirstFloorY;
            int upper = o.y + c.UpperFloorY;
            int frontLower = o.z - 4;
            int frontUpper = o.z - UpperFacadeProjection - 4;

            FlowerBox(a, centre - 34, first + 4, frontLower, 14, in p);
            FlowerBox(a, centre + 20, first + 4, frontLower, 14, in p);
            FlowerBox(a, centre - 18, upper + 1, frontUpper, 36, in p);
            FlowerBox(a, centre - 6, o.y + c.MainEaveY + 4, frontUpper, 12, in p);

            // Four low stone steps meet the 0.8 m foundation/door threshold cleanly.
            for (int step = 0; step < 4; step++)
            {
                int width = 26 - step * 3;
                a.Box(new int3(centre - width / 2, o.y + step * 2, o.z - 15 + step * 3),
                    new int3(width, 2, 7), p.Stone);
            }

            // Sparse, shallow ivy hugs the right facade instead of becoming a floating column.
            int ivyX = o.x + c.Width - 5;
            for (int i = 0; i < 11; i++)
            {
                int y = first + 3 + i * 4;
                int x = ivyX - (i % 3);
                int z = frontUpper;
                a.Box(new int3(x, y, z), new int3(2, 2, 1), p.Foliage);
                if ((i % 3) == 1)
                    a.Box(new int3(x - 3, y + 1, z), new int3(2, 1, 1), p.Foliage);
            }
        }

        private static void FlowerBox(IStructureAuthoringSession a, int x, int y, int z, int w,
            in NewHouseReferencePalette p)
        {
            a.Box(new int3(x, y, z), new int3(w, 2, 3), p.Timber);
            a.Box(new int3(x + 1, y + 2, z + 1), new int3(w - 2, 2, 2), p.Foliage);

            // Tiny one-voxel blossoms retain the reference hint without the previous 4x3x4 cubes.
            for (int i = 3; i < w - 2; i += 6)
                a.Box(new int3(x + i, y + 4, z + 1), new int3(1), p.Flowers);
        }

        private static void Shrub(IStructureAuthoringSession a, int x, int y, int z, int r, byte m)
        { a.Cone(x, y, z, r, r + 4, m); a.Disc(x, y + r / 2, z, r + 1, m); }

        private static void Post(IStructureAuthoringSession a, int x, int y0, int y1, int z, byte m)
        { a.Box(new int3(x, y0 + 1, z), new int3(3, y1 - y0 - 1, TimberDepth), m); }

        private static void GableX(IStructureAuthoringSession a, int minX, int maxX, int minZ,
            int maxZ, int eave, int rise, int t, byte m)
        {
            int span = math.max(2, maxZ - minZ - 1);
            for (int z = minZ; z < maxZ; z++)
            {
                int edge = math.min(z - minZ, maxZ - 1 - z);
                int y = eave + (rise * edge * 2 + span / 2) / span;
                a.Box(new int3(minX, y, z), new int3(maxX - minX, t, 1), m);
            }
        }

        private static void GableZ(IStructureAuthoringSession a, int minX, int maxX, int minZ,
            int maxZ, int eave, int rise, int t, byte m)
        {
            int span = math.max(2, maxX - minX - 1);
            for (int x = minX; x < maxX; x++)
            {
                int edge = math.min(x - minX, maxX - 1 - x);
                int y = eave + (rise * edge * 2 + span / 2) / span;
                a.Box(new int3(x, y, minZ), new int3(1, t, maxZ - minZ), m);
            }
        }

        private static void Line(IStructureAuthoringSession a, int x0, int y0, int x1, int y1, int z, byte m)
        {
            int dx = x1 - x0, dy = y1 - y0, steps = math.max(math.abs(dx), math.abs(dy));
            if (steps == 0)
            {
                a.Box(new int3(x0, y0, z), new int3(2, 2, TimberDepth), m);
                return;
            }

            for (int i = 0; i <= steps; i++)
                a.Box(new int3(x0 + dx * i / steps, y0 + dy * i / steps, z),
                    new int3(2, 2, TimberDepth), m);
        }
    }
}
