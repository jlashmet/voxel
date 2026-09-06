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

    /// <summary>Integer-voxel dimensions for the pinned 10 cm reference house.</summary>
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

        // The pinned image is substantially taller and more vertically layered than the discarded
        // cottage reference: stone ground storey, full timber/plaster middle storey and a dominant
        // steep front gable with a tall crest.
        public static NewHouseReferenceConfig Default => new(84, 56, 8, 34, 36, 3, 48, 3, 7);
    }

    public readonly struct NewHouseReferenceResult
    {
        public readonly int3 Min, MaxExclusive;
        public readonly int DoorCentreX, FrontZ, RidgeY;
        public NewHouseReferenceResult(int3 min, int3 max, int doorX, int frontZ, int ridgeY)
        { Min = min; MaxExclusive = max; DoorCentreX = doorX; FrontZ = frontZ; RidgeY = ridgeY; }
    }

    /// <summary>
    /// Reusable production WorldBuilder authoring for the pinned ornate blue-roof house.
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

            // Pinned-reference massing: a deep pale-stone base, a jettied timber/plaster middle
            // storey and a very steep front gable in front of a lower transverse blue roof.
            a.Box(o, new int3(c.Width, c.FoundationHeight, c.Depth), p.Stone);
            a.HollowBox(new int3(o.x, firstY, o.z),
                new int3(c.Width, c.FirstFloorHeight, c.Depth),
                c.WallThickness, p.Stone, false, true);
            a.HollowBox(new int3(o.x - UpperFacadeProjection, upperY, upperFrontZ),
                new int3(c.Width + UpperFacadeProjection * 2, c.UpperFloorHeight,
                    c.Depth + UpperFacadeProjection * 2),
                c.WallThickness, p.Plaster, true, false);

            int crossEave = upperY + 23;
            GableX(a,
                o.x - c.RoofOverhang - 4,
                o.x + c.Width + c.RoofOverhang + 4,
                o.z + 14,
                o.z + c.Depth + c.RoofOverhang,
                crossEave,
                15,
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
            AddSweptEaveTips(a, o, in c, eaveY, p.Roof, p.Timber);
            FillFrontGable(a, centreX, upperFrontZ, eaveY,
                c.Width - 6, c.MainRoofRise - 3, p.Plaster);

            AddChimney(a, o, in c, p.Stone);
            AddTimberFrame(a, o, in c, p.Timber);
            AddReferenceOpenings(a, o, in c, in p);
            AddFacadeDetails(a, o, in c, in p);
            AddCrestFinial(a, centreX, ridgeY, upperFrontZ, p.Timber);

            return new NewHouseReferenceResult(
                new int3(o.x - c.RoofOverhang - 15, o.y, o.z - c.RoofOverhang - 12),
                new int3(o.x + c.Width + c.RoofOverhang + 16,
                    ridgeY + 24,
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

            // The pinned image is an isolated architectural plate, not a lawn showcase. Keep only a
            // compact grounded apron and low planting around the foundation.
            a.Box(new int3(o.x - 22, o.y - 6, o.z - 30),
                new int3(c.Width + 44, 6, c.Depth + 62), p.Ground);

            int doorX = o.x + c.Width / 2;
            a.Box(new int3(doorX - 18, o.y, o.z - 17), new int3(36, 1, 18), p.Stone);
            a.Box(new int3(o.x + 4, o.y, o.z - 8), new int3(16, 3, 8), p.Stone);
            a.Box(new int3(o.x + c.Width - 20, o.y, o.z - 8), new int3(16, 3, 8), p.Stone);

            LowPlanting(a, o.x + 8, o.y + 3, o.z - 8, 13, p.Foliage);
            LowPlanting(a, o.x + c.Width - 20, o.y + 3, o.z - 8, 13, p.Foliage);

            // Small round planters echo the two flower pots at the stair base without restoring the
            // discarded reference's long stepping-stone path or conical lawn shrubs.
            a.Disc(doorX - 24, o.y + 4, o.z - 14, 3, p.Stone);
            a.Cone(doorX - 24, o.y + 5, o.z - 14, 2, 4, p.Foliage);
            a.Disc(doorX + 24, o.y + 4, o.z - 14, 3, p.Stone);
            a.Cone(doorX + 24, o.y + 5, o.z - 14, 2, 4, p.Foliage);
        }

        private static void AddReferenceOpenings(IStructureAuthoringSession a, int3 o,
            in NewHouseReferenceConfig c, in NewHouseReferencePalette p)
        {
            int centreX = o.x + c.Width / 2;
            int firstY = o.y + c.FirstFloorY;
            int upperY = o.y + c.UpperFloorY;
            int eaveY = o.y + c.MainEaveY;
            int upperFrontZ = o.z - UpperFacadeProjection;

            // Ground register: one large round-arched timber portal and two narrow arched windows.
            ArchedPanel(a, centreX, firstY + 2, o.z, 21, 29,
                p.Door, p.Timber, p.Stone, false);
            ArchedPanel(a, centreX - 29, firstY + 7, o.z, 11, 20,
                p.Glass, p.Timber, p.Stone, true);
            ArchedPanel(a, centreX + 29, firstY + 7, o.z, 11, 20,
                p.Glass, p.Timber, p.Stone, true);

            // Middle register: the reference's single large arched window with blue shutters.
            ArchedPanel(a, centreX, upperY + 6, upperFrontZ, 25, 27,
                p.Glass, p.Timber, p.Timber, true);
            AddShutters(a, centreX, upperY + 7, upperFrontZ, 25, 25, p.Accent, p.Timber);

            // Gable register: a second tall arched window nested inside the steep front gable.
            ArchedPanel(a, centreX, eaveY + 9, upperFrontZ, 19, 25,
                p.Glass, p.Timber, p.Timber, true);
        }

        private static void ArchedPanel(IStructureAuthoringSession a,
            int centreX, int y, int frontZ, int width, int height,
            byte panel, byte frame, byte surround, bool muntins)
        {
            int radius = math.max(2, width / 2);
            int spring = math.max(2, height - radius - 1);

            for (int row = 0; row < height; row++)
            {
                int half = ArchHalfWidth(radius, spring, row);
                a.Carve(new int3(centreX - half, y + row, frontZ - 1),
                    new int3(half * 2 + 1, 1, 8));
                a.Box(new int3(centreX - half, y + row, frontZ + 1),
                    new int3(half * 2 + 1, 1, 2), panel);

                // A two-voxel outer ring makes the stone/timber arch read from the target camera.
                if (row >= spring)
                {
                    a.Box(new int3(centreX - half - 2, y + row, frontZ - 3),
                        new int3(2, 1, 2), surround);
                    a.Box(new int3(centreX + half + 1, y + row, frontZ - 3),
                        new int3(2, 1, 2), surround);
                }
            }

            a.Box(new int3(centreX - radius - 2, y - 1, frontZ - 3),
                new int3(2, spring + 2, 2), surround);
            a.Box(new int3(centreX + radius + 1, y - 1, frontZ - 3),
                new int3(2, spring + 2, 2), surround);
            a.Box(new int3(centreX - radius - 3, y - 2, frontZ - 3),
                new int3(radius * 2 + 7, 2, 3), surround);

            if (!muntins)
            {
                // The entry door has strong vertical/horizontal timber paneling but no glass cross.
                a.Box(new int3(centreX - 1, y + 2, frontZ - 4),
                    new int3(2, spring - 2, 2), frame);
                a.Box(new int3(centreX - radius + 2, y + spring / 2, frontZ - 4),
                    new int3(radius * 2 - 3, 2, 2), frame);
                return;
            }

            a.Box(new int3(centreX - 1, y + 1, frontZ - 4),
                new int3(2, height - 3, 2), frame);
            a.Box(new int3(centreX - radius + 1, y + spring / 2, frontZ - 4),
                new int3(radius * 2 - 1, 2, 2), frame);
        }

        private static int ArchHalfWidth(int radius, int spring, int row)
        {
            if (row < spring) return radius;
            int dy = math.min(radius, row - spring);
            return math.max(0, (int)math.floor(math.sqrt(
                math.max(0, radius * radius - dy * dy))));
        }

        private static void AddShutters(IStructureAuthoringSession a,
            int centreX, int y, int frontZ, int windowWidth, int height,
            byte accent, byte timber)
        {
            int half = windowWidth / 2;
            const int shutterWidth = 7;
            int leftX = centreX - half - shutterWidth - 4;
            int rightX = centreX + half + 5;

            a.Box(new int3(leftX, y, frontZ - 5), new int3(shutterWidth, height, 2), accent);
            a.Box(new int3(rightX, y, frontZ - 5), new int3(shutterWidth, height, 2), accent);

            for (int yOffset = 5; yOffset < height - 2; yOffset += 7)
            {
                a.Box(new int3(leftX, y + yOffset, frontZ - 6),
                    new int3(shutterWidth, 1, 2), timber);
                a.Box(new int3(rightX, y + yOffset, frontZ - 6),
                    new int3(shutterWidth, 1, 2), timber);
            }
        }

        private static void AddTimberFrame(IStructureAuthoringSession a, int3 o,
            in NewHouseReferenceConfig c, byte timber)
        {
            int front = o.z - UpperFacadeProjection - 3;
            int upper = o.y + c.UpperFloorY;
            int eave = o.y + c.MainEaveY;
            int ridge = o.y + c.MainRidgeY;
            int centre = o.x + c.Width / 2;

            // Heavy floor/eave belts and corner posts frame the middle storey.
            a.Box(new int3(o.x - 4, upper - 3, front),
                new int3(c.Width + 8, 4, TimberDepth), timber);
            a.Box(new int3(o.x - 4, eave - 4, front),
                new int3(c.Width + 8, 4, TimberDepth), timber);
            Post(a, o.x + 5, upper, eave, front, timber);
            Post(a, o.x + c.Width - 8, upper, eave, front, timber);

            // Reference-visible lower braces around the central middle-storey opening.
            Line(a, o.x + 7, upper + 5, o.x + 22, upper + 18, front, timber);
            Line(a, o.x + c.Width - 8, upper + 5,
                o.x + c.Width - 23, upper + 18, front, timber);
            a.Box(new int3(o.x + 7, upper + 2, front),
                new int3(c.Width - 14, 3, TimberDepth), timber);

            // Double roof-edge timber and a central gable post reproduce the strongly outlined
            // triangular facade rather than the discarded broad blank plaster triangle.
            LineThick(a, o.x + 3, eave - 1, centre, ridge - 2, front, timber);
            LineThick(a, o.x + c.Width - 4, eave - 1, centre, ridge - 2, front, timber);
            Post(a, centre - 1, eave, ridge - 2, front, timber);
            a.Box(new int3(centre - 18, eave + 4, front),
                new int3(36, 3, TimberDepth), timber);
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

        private static void AddSweptEaveTips(IStructureAuthoringSession a, int3 o,
            in NewHouseReferenceConfig c, int eaveY, byte roof, byte timber)
        {
            int minX = o.x - c.RoofOverhang;
            int maxX = o.x + c.Width + c.RoofOverhang - 1;
            int minZ = o.z - c.RoofOverhang;
            int depth = c.Depth + c.RoofOverhang * 2;

            // The pinned roof ends flare outward and down before turning into the steep central
            // slope. Approximate that sweep with stepped derived roof cells and a visible timber lip.
            for (int extra = 1; extra <= 8; extra++)
            {
                int y = eaveY - 1 - (extra + 1) / 3;
                a.Box(new int3(minX - extra, y, minZ), new int3(1, c.RoofThickness, depth), roof);
                a.Box(new int3(maxX + extra, y, minZ), new int3(1, c.RoofThickness, depth), roof);
            }

            Line(a, minX - 8, eaveY - 4, minX + 2, eaveY, o.z - c.RoofOverhang - 2, timber);
            Line(a, maxX + 8, eaveY - 4, maxX - 2, eaveY, o.z - c.RoofOverhang - 2, timber);
        }

        private static void AddChimney(IStructureAuthoringSession a, int3 o,
            in NewHouseReferenceConfig c, byte stone)
        {
            int x = o.x + 3;
            int z = o.z + 18;
            int topY = o.y + c.MainEaveY + 29;
            int height = topY - o.y;
            a.Box(new int3(x, o.y, z), new int3(10, height, 10), stone);
            a.Box(new int3(x - 2, topY - 8, z - 2), new int3(14, 4, 14), stone);
            a.Box(new int3(x - 1, topY - 3, z - 1), new int3(12, 3, 12), stone);
            a.Box(new int3(x + 2, topY, z + 2), new int3(6, 5, 6), stone);
        }

        private static void AddCrestFinial(IStructureAuthoringSession a,
            int centreX, int ridgeY, int frontZ, byte timber)
        {
            int z = frontZ + 8;
            a.Box(new int3(centreX - 5, ridgeY + 1, z), new int3(10, 3, 6), timber);
            a.Box(new int3(centreX - 4, ridgeY + 4, z + 1), new int3(8, 5, 4), timber);
            a.Box(new int3(centreX - 3, ridgeY + 9, z + 1), new int3(6, 5, 4), timber);
            a.Cone(centreX, ridgeY + 14, z + 3, 4, 10, timber);
        }

        private static void AddFacadeDetails(IStructureAuthoringSession a, int3 o,
            in NewHouseReferenceConfig c, in NewHouseReferencePalette p)
        {
            int centre = o.x + c.Width / 2;
            int first = o.y + c.FirstFloorY;
            int upper = o.y + c.UpperFloorY;
            int eave = o.y + c.MainEaveY;
            int frontLower = o.z - 5;
            int frontUpper = o.z - UpperFacadeProjection - 5;

            FlowerBox(a, centre - 35, first + 5, frontLower, 13, in p);
            FlowerBox(a, centre + 22, first + 5, frontLower, 13, in p);
            FlowerBox(a, centre - 14, upper + 4, frontUpper, 28, in p);
            FlowerBox(a, centre - 11, eave + 6, frontUpper, 22, in p);

            // Broad central stone stair, flanked by the low masonry planter plinths in the reference.
            for (int step = 0; step < 5; step++)
            {
                int width = 34 - step * 3;
                a.Box(new int3(centre - width / 2, o.y + step * 2,
                        o.z - 19 + step * 3),
                    new int3(width, 2, 8), p.Stone);
            }
            a.Box(new int3(centre - 31, o.y, o.z - 13), new int3(12, 9, 10), p.Stone);
            a.Box(new int3(centre + 19, o.y, o.z - 13), new int3(12, 9, 10), p.Stone);

            AddIvy(a, o.x + c.Width - 3, first + 2, frontUpper, 19, -1, in p);
            AddIvy(a, o.x + 4, first + 3, frontUpper, 13, 1, in p);
            AddBanner(a, o, in c, p.Accent, p.Timber);
            AddHangingSign(a, o, in c, p.Timber, p.Accent);
        }

        private static void AddIvy(IStructureAuthoringSession a,
            int startX, int startY, int z, int segments, int xDirection,
            in NewHouseReferencePalette p)
        {
            for (int i = 0; i < segments; i++)
            {
                int y = startY + i * 4;
                int x = startX + xDirection * ((i / 3) % 4);
                int size = (i % 4 == 0) ? 3 : 2;
                a.Box(new int3(x, y, z), new int3(size, 3, 1), p.Foliage);
                if ((i & 1) == 0)
                    a.Box(new int3(x - 2, y + 1, z), new int3(2, 2, 1), p.Foliage);
            }
        }

        private static void AddBanner(IStructureAuthoringSession a, int3 o,
            in NewHouseReferenceConfig c, byte accent, byte timber)
        {
            int upper = o.y + c.UpperFloorY;
            int front = o.z - UpperFacadeProjection - 7;
            int x = o.x - 10;

            a.Box(new int3(x - 2, upper + 30, front), new int3(22, 2, 2), timber);
            a.Box(new int3(x, upper + 4, front + 1), new int3(17, 27, 1), accent);
            for (int row = 0; row < 6; row++)
            {
                int inset = row / 2;
                a.Box(new int3(x + inset, upper + 1 - row, front + 1),
                    new int3(17 - inset * 2, 1, 1), accent);
            }
        }

        private static void AddHangingSign(IStructureAuthoringSession a, int3 o,
            in NewHouseReferenceConfig c, byte timber, byte accent)
        {
            int upper = o.y + c.UpperFloorY;
            int front = o.z - UpperFacadeProjection - 8;
            int bracketX = o.x + c.Width + 2;

            a.Box(new int3(bracketX, upper + 29, front), new int3(20, 2, 2), timber);
            a.Box(new int3(bracketX + 15, upper + 13, front), new int3(2, 18, 2), timber);
            Shield(a, bracketX + 10, upper + 5, front + 1, 13, 18, timber);
            a.Box(new int3(bracketX + 15, upper + 11, front - 1), new int3(1, 10, 2), accent);
            a.Box(new int3(bracketX + 11, upper + 15, front - 1), new int3(9, 1, 2), accent);
        }

        private static void Shield(IStructureAuthoringSession a,
            int x, int y, int z, int width, int height, byte material)
        {
            int upperRows = height - 6;
            a.Box(new int3(x, y + 6, z), new int3(width, upperRows, 2), material);
            for (int row = 0; row < 6; row++)
            {
                int inset = (5 - row) / 2;
                a.Box(new int3(x + inset, y + row, z),
                    new int3(width - inset * 2, 1, 2), material);
            }
        }

        private static void FlowerBox(IStructureAuthoringSession a, int x, int y, int z, int w,
            in NewHouseReferencePalette p)
        {
            a.Box(new int3(x, y, z), new int3(w, 2, 3), p.Timber);
            a.Box(new int3(x + 1, y + 2, z + 1), new int3(w - 2, 3, 2), p.Foliage);
            for (int i = 2; i < w - 1; i += 4)
                a.Box(new int3(x + i, y + 5 + ((i / 4) & 1), z + 1),
                    new int3(1), p.Flowers);
        }

        private static void LowPlanting(IStructureAuthoringSession a,
            int x, int y, int z, int width, byte material)
        {
            for (int i = 0; i < width; i += 3)
                a.Box(new int3(x + i, y + (i % 2), z), new int3(3, 3, 3), material);
        }

        private static void Post(IStructureAuthoringSession a, int x, int y0, int y1, int z, byte m)
        {
            int height = math.max(1, y1 - y0 - 1);
            a.Box(new int3(x, y0 + 1, z), new int3(3, height, TimberDepth), m);
        }

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

        private static void LineThick(IStructureAuthoringSession a,
            int x0, int y0, int x1, int y1, int z, byte material)
        {
            Line(a, x0, y0, x1, y1, z, material);
            Line(a, x0, y0 + 2, x1, y1 + 2, z, material);
        }

        private static void Line(IStructureAuthoringSession a,
            int x0, int y0, int x1, int y1, int z, byte m)
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
