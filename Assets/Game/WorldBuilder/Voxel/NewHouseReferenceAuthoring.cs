using System;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.WorldBuilder.Voxel
{
    public readonly struct NewHouseReferencePalette
    {
        public readonly byte Plaster, Timber, Roof, Stone, Glass, Door, Accent, Ground, Flowers, Foliage, Ornament;

        public NewHouseReferencePalette(byte plaster, byte timber, byte roof, byte stone, byte glass,
            byte door, byte accent, byte ground, byte flowers, byte foliage, byte ornament)
        {
            Plaster = plaster; Timber = timber; Roof = roof; Stone = stone; Glass = glass;
            Door = door; Accent = accent; Ground = ground; Flowers = flowers; Foliage = foliage;
            Ornament = ornament;
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
        private const int FrontGableBackOffset = 22;

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

            a.Box(o, new int3(c.Width, c.FoundationHeight, c.Depth), p.Stone);
            a.HollowBox(new int3(o.x, firstY, o.z),
                new int3(c.Width, c.FirstFloorHeight, c.Depth),
                c.WallThickness, p.Stone, false, true);
            a.HollowBox(new int3(o.x - UpperFacadeProjection, upperY, upperFrontZ),
                new int3(c.Width + UpperFacadeProjection * 2, c.UpperFloorHeight,
                    c.Depth + UpperFacadeProjection * 2),
                c.WallThickness, p.Plaster, true, false);

            // The reference is not one giant front-to-back roof. A low transverse roof supplies the
            // broad blue shoulders while a shallow, steep front gable owns the portrait silhouette.
            int crossEave = upperY + 18;
            GableX(a,
                o.x - c.RoofOverhang - 4,
                o.x + c.Width + c.RoofOverhang + 4,
                o.z + 8,
                o.z + c.Depth + c.RoofOverhang,
                crossEave,
                14,
                c.RoofThickness,
                p.Roof);

            int frontRoofMinZ = o.z - c.RoofOverhang;
            int frontRoofMaxZ = o.z + FrontGableBackOffset;
            GableZ(a,
                o.x - c.RoofOverhang,
                o.x + c.Width + c.RoofOverhang,
                frontRoofMinZ,
                frontRoofMaxZ,
                eaveY,
                c.MainRoofRise,
                c.RoofThickness,
                p.Roof);
            AddSweptEaveTips(a, o, in c, eaveY, frontRoofMinZ, frontRoofMaxZ, p.Roof, p.Timber);
            FillFrontGable(a, centreX, upperFrontZ, eaveY,
                c.Width - 8, c.MainRoofRise - 3, p.Plaster);
            AddLowerSideRoofWings(a, o, in c, upperY, p.Roof, p.Timber);

            AddChimney(a, o, in c, p.Stone);
            AddTimberFrame(a, o, in c, p.Timber);
            AddReferenceOpenings(a, o, in c, in p);
            AddFacadeDetails(a, o, in c, in p);
            AddCrestFinial(a, centreX, ridgeY, upperFrontZ, p.Timber, p.Ornament);

            return new NewHouseReferenceResult(
                new int3(o.x - c.RoofOverhang - 15, o.y, o.z - c.RoofOverhang - 12),
                new int3(o.x + c.Width + c.RoofOverhang + 16,
                    ridgeY + 14,
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

            // Keep the plate boundary well outside the portrait camera; the pinned image has a muted
            // neutral ground rather than a bright lawn rectangle around the house.
            a.Box(new int3(o.x - 80, o.y - 4, o.z - 80),
                new int3(c.Width + 160, 4, c.Depth + 170), p.Ground);

            int doorX = o.x + c.Width / 2;
            a.Box(new int3(doorX - 20, o.y, o.z - 18), new int3(40, 1, 19), p.Stone);
            a.Box(new int3(o.x + 3, o.y, o.z - 8), new int3(17, 4, 9), p.Stone);
            a.Box(new int3(o.x + c.Width - 20, o.y, o.z - 8), new int3(17, 4, 9), p.Stone);

            LowPlanting(a, o.x + 6, o.y + 4, o.z - 8, 15, p.Foliage);
            LowPlanting(a, o.x + c.Width - 21, o.y + 4, o.z - 8, 15, p.Foliage);

            a.Disc(doorX - 25, o.y + 4, o.z - 14, 3, p.Stone);
            a.Cone(doorX - 25, o.y + 5, o.z - 14, 2, 4, p.Foliage);
            a.Disc(doorX + 25, o.y + 4, o.z - 14, 3, p.Stone);
            a.Cone(doorX + 25, o.y + 5, o.z - 14, 2, 4, p.Foliage);
        }

        private static void AddReferenceOpenings(IStructureAuthoringSession a, int3 o,
            in NewHouseReferenceConfig c, in NewHouseReferencePalette p)
        {
            int centreX = o.x + c.Width / 2;
            int firstY = o.y + c.FirstFloorY;
            int upperY = o.y + c.UpperFloorY;
            int eaveY = o.y + c.MainEaveY;
            int upperFrontZ = o.z - UpperFacadeProjection;

            ArchedPanel(a, centreX, firstY + 2, o.z, 19, 28,
                p.Door, p.Timber, p.Stone, false);
            ArchedPanel(a, centreX - 29, firstY + 7, o.z, 9, 19,
                p.Glass, p.Accent, p.Stone, true);
            ArchedPanel(a, centreX + 29, firstY + 7, o.z, 9, 19,
                p.Glass, p.Accent, p.Stone, true);

            ArchedPanel(a, centreX, upperY + 7, upperFrontZ, 17, 24,
                p.Glass, p.Timber, p.Timber, true);
            AddShutters(a, centreX, upperY + 8, upperFrontZ, 17, 22, p.Accent, p.Timber);

            ArchedPanel(a, centreX, eaveY + 10, upperFrontZ, 15, 21,
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

            if (!muntins)
            {
                a.Box(new int3(centreX, y + 2, frontZ - 4),
                    new int3(1, math.max(2, spring - 2), 2), frame);
                a.Box(new int3(centreX - radius + 2, y + spring / 2, frontZ - 4),
                    new int3(math.max(2, radius * 2 - 3), 1, 2), frame);
                return;
            }

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

        private static void AddShutters(IStructureAuthoringSession a,
            int centreX, int y, int frontZ, int windowWidth, int height,
            byte accent, byte timber)
        {
            int half = windowWidth / 2;
            const int shutterWidth = 5;
            int leftX = centreX - half - shutterWidth - 3;
            int rightX = centreX + half + 4;

            a.Box(new int3(leftX, y, frontZ - 5), new int3(shutterWidth, height, 2), accent);
            a.Box(new int3(rightX, y, frontZ - 5), new int3(shutterWidth, height, 2), accent);

            for (int yOffset = 5; yOffset < height - 2; yOffset += 6)
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

            // Thin but readable structural belts preserve plaster area instead of turning the facade
            // into oversized brown slabs.
            a.Box(new int3(o.x - 3, upper - 2, front),
                new int3(c.Width + 6, 2, TimberDepth), timber);
            a.Box(new int3(o.x - 3, eave - 3, front),
                new int3(c.Width + 6, 2, TimberDepth), timber);
            ThinPost(a, o.x + 5, upper, eave, front, timber);
            ThinPost(a, o.x + c.Width - 7, upper, eave, front, timber);
            a.Box(new int3(o.x + 7, upper + 3, front),
                new int3(c.Width - 14, 2, TimberDepth), timber);
            Line(a, o.x + 7, upper + 5, o.x + 20, upper + 16, front, timber);
            Line(a, o.x + c.Width - 8, upper + 5,
                o.x + c.Width - 21, upper + 16, front, timber);

            // Gable edge trim follows the blue roof. Keep the central post out of the arched window;
            // the reference has short vertical timber above/below it, not a giant cross through glass.
            Line(a, o.x + 4, eave - 1, centre, ridge - 2, front, timber);
            Line(a, o.x + c.Width - 5, eave - 1, centre, ridge - 2, front, timber);
            a.Box(new int3(centre - 15, eave + 3, front),
                new int3(30, 2, TimberDepth), timber);
            a.Box(new int3(centre, eave + 4, front), new int3(1, 5, TimberDepth), timber);
            int postAboveWindowY = eave + 32;
            a.Box(new int3(centre, postAboveWindowY, front),
                new int3(1, math.max(1, ridge - postAboveWindowY - 2), TimberDepth), timber);
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
            in NewHouseReferenceConfig c, int eaveY, int frontRoofMinZ, int frontRoofMaxZ,
            byte roof, byte timber)
        {
            int minX = o.x - c.RoofOverhang;
            int maxX = o.x + c.Width + c.RoofOverhang - 1;
            int depth = frontRoofMaxZ - frontRoofMinZ;

            for (int extra = 1; extra <= 7; extra++)
            {
                int y = eaveY - 1 - (extra + 1) / 3;
                a.Box(new int3(minX - extra, y, frontRoofMinZ),
                    new int3(1, c.RoofThickness, depth), roof);
                a.Box(new int3(maxX + extra, y, frontRoofMinZ),
                    new int3(1, c.RoofThickness, depth), roof);
            }

            int trimZ = frontRoofMinZ - 1;
            Line(a, minX - 7, eaveY - 4, minX + 2, eaveY, trimZ, timber);
            Line(a, maxX + 7, eaveY - 4, maxX - 2, eaveY, trimZ, timber);
        }

        private static void AddLowerSideRoofWings(IStructureAuthoringSession a, int3 o,
            in NewHouseReferenceConfig c, int upperY, byte roof, byte timber)
        {
            int frontZ = o.z - 3;
            const int depth = 16;
            for (int step = 0; step < 11; step++)
            {
                int y = upperY - 4 + step / 3;
                a.Box(new int3(o.x - 11 + step, y, frontZ), new int3(1, 2, depth), roof);
                a.Box(new int3(o.x + c.Width + 10 - step, y, frontZ), new int3(1, 2, depth), roof);
            }
            Line(a, o.x - 11, upperY - 4, o.x, upperY - 1, frontZ - 1, timber);
            Line(a, o.x + c.Width + 10, upperY - 4,
                o.x + c.Width - 1, upperY - 1, frontZ - 1, timber);
        }

        private static void AddChimney(IStructureAuthoringSession a, int3 o,
            in NewHouseReferenceConfig c, byte stone)
        {
            int x = o.x + 3;
            int z = o.z + 20;
            int topY = o.y + c.MainEaveY + 25;
            int height = topY - o.y;
            a.Box(new int3(x, o.y, z), new int3(10, height, 10), stone);
            a.Box(new int3(x - 2, topY - 8, z - 2), new int3(14, 3, 14), stone);
            a.Box(new int3(x - 1, topY - 3, z - 1), new int3(12, 3, 12), stone);
            a.Box(new int3(x + 2, topY, z + 2), new int3(6, 4, 6), stone);
        }

        private static void AddCrestFinial(IStructureAuthoringSession a,
            int centreX, int ridgeY, int frontZ, byte timber, byte ornament)
        {
            int z = frontZ + 7;
            a.Box(new int3(centreX - 3, ridgeY + 1, z), new int3(6, 2, 5), timber);
            a.Box(new int3(centreX - 2, ridgeY + 3, z + 1), new int3(4, 3, 3), ornament);
            a.Box(new int3(centreX - 1, ridgeY + 6, z + 1), new int3(2, 2, 3), ornament);
            a.Cone(centreX, ridgeY + 8, z + 2, 2, 5, ornament);
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
            FlowerBox(a, centre - 12, upper + 4, frontUpper, 24, in p);
            FlowerBox(a, centre - 9, eave + 7, frontUpper, 18, in p);

            for (int step = 0; step < 5; step++)
            {
                int width = 34 - step * 3;
                a.Box(new int3(centre - width / 2, o.y + step * 2,
                        o.z - 19 + step * 3),
                    new int3(width, 2, 8), p.Stone);
            }
            a.Box(new int3(centre - 31, o.y, o.z - 13), new int3(12, 9, 10), p.Stone);
            a.Box(new int3(centre + 19, o.y, o.z - 13), new int3(12, 9, 10), p.Stone);

            AddDoorHardware(a, centre, first + 10, o.z - 5, p.Ornament);
            AddIvy(a, o.x + c.Width - 5, first + 1, frontUpper, 29, -1, in p);
            AddIvy(a, o.x + 4, first + 3, frontUpper, 18, 1, in p);
            AddBanner(a, o, in c, p.Accent, p.Timber, p.Ornament);
            AddHangingSign(a, o, in c, p.Timber, p.Ornament);
        }

        private static void AddDoorHardware(IStructureAuthoringSession a,
            int centreX, int y, int z, byte ornament)
        {
            a.Box(new int3(centreX - 2, y, z), new int3(5, 5, 1), ornament);
            a.Box(new int3(centreX, y - 3, z), new int3(1, 11, 1), ornament);
            a.Box(new int3(centreX - 4, y + 2, z), new int3(9, 1, 1), ornament);
        }

        private static void AddIvy(IStructureAuthoringSession a,
            int startX, int startY, int z, int segments, int xDirection,
            in NewHouseReferencePalette p)
        {
            // Connected overlapping clusters read as climbing foliage instead of a dotted ladder.
            for (int i = 0; i < segments; i++)
            {
                int y = startY + i * 2;
                int x = startX + xDirection * ((i / 4) % 5);
                int width = (i % 3 == 0) ? 4 : 3;
                int height = (i % 4 == 0) ? 4 : 3;
                a.Box(new int3(x, y, z), new int3(width, height, 1), p.Foliage);
                if ((i & 1) == 0)
                    a.Box(new int3(x - 2 * xDirection, y + 1, z),
                        new int3(3, 2, 1), p.Foliage);
            }
        }

        private static void AddBanner(IStructureAuthoringSession a, int3 o,
            in NewHouseReferenceConfig c, byte accent, byte timber, byte ornament)
        {
            int upper = o.y + c.UpperFloorY;
            int front = o.z - UpperFacadeProjection - 7;
            int x = o.x - 8;
            const int width = 14;

            a.Box(new int3(x - 2, upper + 29, front), new int3(width + 7, 2, 2), timber);
            a.Box(new int3(x, upper + 5, front + 1), new int3(width, 24, 1), accent);
            for (int row = 0; row < 5; row++)
            {
                int inset = row / 2;
                a.Box(new int3(x + inset, upper + 1 - row, front + 1),
                    new int3(width - inset * 2, 1, 1), accent);
            }

            int cx = x + width / 2;
            int cy = upper + 16;
            a.Box(new int3(cx, cy - 6, front), new int3(1, 13, 1), ornament);
            a.Box(new int3(cx - 5, cy, front), new int3(11, 1, 1), ornament);
            Line(a, cx - 4, cy - 4, cx + 4, cy + 4, front, ornament);
            Line(a, cx - 4, cy + 4, cx + 4, cy - 4, front, ornament);
        }

        private static void AddHangingSign(IStructureAuthoringSession a, int3 o,
            in NewHouseReferenceConfig c, byte timber, byte ornament)
        {
            int upper = o.y + c.UpperFloorY;
            int front = o.z - UpperFacadeProjection - 8;
            int bracketX = o.x + c.Width + 1;

            a.Box(new int3(bracketX, upper + 27, front), new int3(16, 2, 2), timber);
            a.Box(new int3(bracketX + 12, upper + 14, front), new int3(2, 15, 2), timber);
            Shield(a, bracketX + 7, upper + 6, front + 1, 11, 15, timber);

            int cx = bracketX + 12;
            int cy = upper + 14;
            a.Box(new int3(cx, cy - 4, front), new int3(1, 9, 1), ornament);
            a.Box(new int3(cx - 4, cy, front), new int3(9, 1, 1), ornament);
            Line(a, cx - 3, cy - 3, cx + 3, cy + 3, front, ornament);
            Line(a, cx - 3, cy + 3, cx + 3, cy - 3, front, ornament);
        }

        private static void Shield(IStructureAuthoringSession a,
            int x, int y, int z, int width, int height, byte material)
        {
            int upperRows = height - 5;
            a.Box(new int3(x, y + 5, z), new int3(width, upperRows, 2), material);
            for (int row = 0; row < 5; row++)
            {
                int inset = (4 - row) / 2;
                a.Box(new int3(x + inset, y + row, z),
                    new int3(width - inset * 2, 1, 2), material);
            }
        }

        private static void FlowerBox(IStructureAuthoringSession a, int x, int y, int z, int w,
            in NewHouseReferencePalette p)
        {
            a.Box(new int3(x, y, z), new int3(w, 2, 3), p.Timber);
            a.Box(new int3(x + 1, y + 2, z + 1), new int3(w - 2, 3, 2), p.Foliage);
            for (int i = 2; i < w - 1; i += 3)
            {
                byte blossom = ((i / 3) & 1) == 0 ? p.Flowers : p.Accent;
                a.Box(new int3(x + i, y + 5 + ((i / 3) & 1), z + 1),
                    new int3(1), blossom);
            }
        }

        private static void LowPlanting(IStructureAuthoringSession a,
            int x, int y, int z, int width, byte material)
        {
            for (int i = 0; i < width; i += 3)
                a.Box(new int3(x + i, y + (i % 2), z), new int3(3, 3, 3), material);
        }

        private static void ThinPost(IStructureAuthoringSession a,
            int x, int y0, int y1, int z, byte material)
        {
            int height = math.max(1, y1 - y0 - 1);
            a.Box(new int3(x, y0 + 1, z), new int3(2, height, TimberDepth), material);
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

        private static void Line(IStructureAuthoringSession a,
            int x0, int y0, int x1, int y1, int z, byte material)
        {
            int dx = x1 - x0, dy = y1 - y0, steps = math.max(math.abs(dx), math.abs(dy));
            if (steps == 0)
            {
                a.Box(new int3(x0, y0, z), new int3(1, 2, TimberDepth), material);
                return;
            }

            for (int i = 0; i <= steps; i++)
                a.Box(new int3(x0 + dx * i / steps, y0 + dy * i / steps, z),
                    new int3(1, 2, TimberDepth), material);
        }
    }
}
