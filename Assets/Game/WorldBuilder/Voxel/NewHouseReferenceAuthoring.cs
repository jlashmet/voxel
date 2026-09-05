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
            if (width < 96 || depth < 56 || foundationHeight <= 0 || firstFloorHeight <= 0 ||
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

        // The supplied portrait reference is intentionally tall and front-gabled rather than a
        // broad side-gabled house. These dimensions preserve that silhouette at the project's
        // authoritative 10 cm voxel scale.
        public static NewHouseReferenceConfig Default => new(104, 72, 10, 36, 34, 4, 48, 4, 7);
    }

    public readonly struct NewHouseReferenceResult
    {
        public readonly int3 Min, MaxExclusive;
        public readonly int DoorCentreX, FrontZ, RidgeY;
        public NewHouseReferenceResult(int3 min, int3 max, int doorX, int frontZ, int ridgeY)
        { Min = min; MaxExclusive = max; DoorCentreX = doorX; FrontZ = frontZ; RidgeY = ridgeY; }
    }

    /// <summary>
    /// Reusable production WorldBuilder authoring for the supplied crooked Tudor cottage.
    /// Material IDs are opaque; reference-only site/camera/light policy remains outside AuthorHouse.
    /// </summary>
    public static class NewHouseReferenceAuthoring
    {
        private const int TimberDepth = 3;
        private const int UpperFacadeProjection = 3;

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

            // Reference massing: a grounded stone lower storey, slightly jettied plaster/Tudor
            // upper storey, a tall front-facing gable, and lower cross-roof shoulders behind it.
            a.Box(o, new int3(c.Width, c.FoundationHeight, c.Depth), p.Stone);
            a.HollowBox(new int3(o.x, firstY, o.z),
                new int3(c.Width, c.FirstFloorHeight, c.Depth),
                c.WallThickness, p.Stone, false, true);
            a.HollowBox(new int3(o.x - UpperFacadeProjection, upperY, upperFrontZ),
                new int3(c.Width + UpperFacadeProjection * 2, c.UpperFloorHeight,
                    c.Depth + UpperFacadeProjection * 2),
                c.WallThickness, p.Plaster, true, false);

            // The shallow cross roof reads as the two blue side shoulders in the supplied image.
            int crossEave = upperY + 21;
            GableX(a,
                o.x - c.RoofOverhang - 4,
                o.x + c.Width + c.RoofOverhang + 4,
                o.z + 15,
                o.z + c.Depth + c.RoofOverhang,
                crossEave,
                22,
                c.RoofThickness,
                p.Roof);

            // Dominant roof ridge runs in depth so the camera sees the steep triangular gable.
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
                c.Width - 8, c.MainRoofRise - 3, p.Plaster);

            AddChimney(a, o, in c, p.Stone);
            AddTimberFrame(a, o, in c, p.Timber);
            AddReferenceOpenings(a, o, in c, in p);
            AddFacadeDetails(a, o, in c, in p);
            AddRidgeFinial(a, centreX, ridgeY, upperFrontZ, p.Timber);

            return new NewHouseReferenceResult(
                new int3(o.x - c.RoofOverhang - 4, o.y, o.z - c.RoofOverhang - 13),
                new int3(o.x + c.Width + c.RoofOverhang + 5,
                    ridgeY + 20,
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
            a.Box(new int3(o.x - 46, o.y - 10, o.z - 62),
                new int3(c.Width + 100, 10, c.Depth + 118), p.Ground);

            int doorX = o.x + c.Width / 2;
            for (int z = o.z - 18; z >= o.z - 58; z -= 7)
            {
                int bend = ((o.z - z) / 7) % 4;
                a.Disc(doorX + (bend == 1 ? -3 : bend == 3 ? 3 : 0), o.y, z, 8, p.Stone);
            }

            Shrub(a, o.x + 19, o.y, o.z - 13, 6, p.Foliage);
            Shrub(a, o.x + c.Width - 17, o.y, o.z - 12, 7, p.Foliage);
            Shrub(a, o.x + c.Width + 17, o.y, o.z + 19, 7, p.Foliage);
            Shrub(a, o.x - 14, o.y, o.z + 24, 6, p.Foliage);
        }

        private static void AddReferenceOpenings(IStructureAuthoringSession a, int3 o,
            in NewHouseReferenceConfig c, in NewHouseReferencePalette p)
        {
            int centreX = o.x + c.Width / 2;
            int firstY = o.y + c.FirstFloorY;
            int upperY = o.y + c.UpperFloorY;
            int eaveY = o.y + c.MainEaveY;
            int upperFrontZ = o.z - UpperFacadeProjection;

            // Broad arched central door and two narrow arched lower windows in the stone base.
            ArchedOpening(a, centreX, firstY + 2, o.z, 23, 30,
                p.Door, p.Stone, p.Timber, false, p.Accent);
            ArchedOpening(a, centreX - 35, firstY + 10, o.z, 15, 22,
                p.Glass, p.Stone, p.Timber, false, p.Accent);
            ArchedOpening(a, centreX + 35, firstY + 10, o.z, 15, 22,
                p.Glass, p.Stone, p.Timber, false, p.Accent);

            // The reference's visual centre is one broad arched middle window with clearly open
            // blue shutters, not the former pair of rectangular facade windows.
            ArchedOpening(a, centreX, upperY + 5, upperFrontZ, 29, 27,
                p.Glass, p.Timber, p.Timber, true, p.Accent);

            // A single arched gable window anchors the steep upper triangle.
            ArchedOpening(a, centreX, eaveY + 8, upperFrontZ, 21, 23,
                p.Glass, p.Timber, p.Timber, false, p.Accent);

            AddDoorRibs(a, centreX, firstY + 2, o.z, 23, 30, p.Timber);
        }

        private static void ArchedOpening(IStructureAuthoringSession a,
            int centreX, int y, int frontZ, int panelWidth, int panelHeight,
            byte panelMaterial, byte surroundMaterial, byte mullionMaterial,
            bool shutters, byte shutterMaterial)
        {
            const int frame = 3;
            int outerWidth = panelWidth + frame * 2;
            int outerHeight = panelHeight + frame + 1;

            // Carve only the arched silhouette row-by-row, then refill its border as the structural
            // surround. The upper corner voxels remain wall, so the opening itself is genuinely
            // arched rather than a rectangular hole hidden behind decoration.
            for (int row = 0; row < outerHeight; row++)
            {
                int outerHalf = ArchHalfWidth(outerWidth, outerHeight, row);
                a.Carve(new int3(centreX - outerHalf, y + row, frontZ - 5),
                    new int3(outerHalf * 2 + 1, 1, 11));

                int innerRow = row - frame;
                int innerHalf = innerRow >= 0 && innerRow < panelHeight
                    ? ArchHalfWidth(panelWidth, panelHeight, innerRow)
                    : -1;

                if (innerHalf < 0)
                {
                    a.Box(new int3(centreX - outerHalf, y + row, frontZ - 5),
                        new int3(outerHalf * 2 + 1, 1, 3), surroundMaterial);
                    continue;
                }

                int side = math.max(1, outerHalf - innerHalf);
                a.Box(new int3(centreX - outerHalf, y + row, frontZ - 5),
                    new int3(side, 1, 3), surroundMaterial);
                a.Box(new int3(centreX + innerHalf + 1, y + row, frontZ - 5),
                    new int3(side, 1, 3), surroundMaterial);
                a.Box(new int3(centreX - innerHalf, y + row, frontZ - 2),
                    new int3(innerHalf * 2 + 1, 1, 2), panelMaterial);
            }

            // Sill and attached muntins sit directly against the panel depth.
            int panelHalf = panelWidth / 2;
            a.Box(new int3(centreX - panelHalf - 2, y - 2, frontZ - 5),
                new int3(panelWidth + 4, 3, 4), surroundMaterial);
            if (panelMaterial != 0)
            {
                int spring = math.max(7, panelHeight - panelWidth / 2);
                a.Box(new int3(centreX - 1, y + 2, frontZ - 4),
                    new int3(2, math.max(3, spring - 2), 2), mullionMaterial);
                a.Box(new int3(centreX - panelHalf + 2, y + spring / 2, frontZ - 4),
                    new int3(math.max(3, panelWidth - 4), 2, 2), mullionMaterial);
            }

            if (shutters)
                AddOpenShutters(a, centreX, y + 1, frontZ, panelWidth, panelHeight - 2,
                    shutterMaterial, mullionMaterial);
        }

        private static int ArchHalfWidth(int width, int height, int row)
        {
            int radius = math.max(2, (width - 1) / 2);
            int archHeight = math.min(radius, math.max(2, height - 3));
            int spring = height - archHeight;
            if (row < spring) return radius;

            int dy = row - spring;
            int squared = math.max(0, radius * radius - dy * dy);
            return math.max(1, (int)math.floor(math.sqrt(squared)));
        }

        private static void AddOpenShutters(IStructureAuthoringSession a,
            int centreX, int y, int frontZ, int windowWidth, int height,
            byte accent, byte timber)
        {
            int half = windowWidth / 2;
            int leftHinge = centreX - half - 4;
            int rightHinge = centreX + half + 3;
            const int strips = 4;

            // Stepped strips rotate each panel visibly away from the facade in voxel space; this
            // is actual depth-bearing shutter geometry, not a coplanar painted slab.
            for (int i = 0; i < strips; i++)
            {
                a.Box(new int3(leftHinge - 2 - i * 2, y, frontZ - 6 - i),
                    new int3(3, height, 2), accent);
                a.Box(new int3(rightHinge + i * 2, y, frontZ - 6 - i),
                    new int3(3, height, 2), accent);
            }

            a.Box(new int3(leftHinge - 2 - strips * 2, y + 4, frontZ - 10),
                new int3(strips * 2 + 3, 2, 2), timber);
            a.Box(new int3(rightHinge, y + height - 6, frontZ - 10),
                new int3(strips * 2 + 3, 2, 2), timber);
        }

        private static void AddDoorRibs(IStructureAuthoringSession a,
            int centreX, int y, int frontZ, int width, int height, byte timber)
        {
            int half = width / 2;
            a.Box(new int3(centreX - 1, y + 2, frontZ - 4),
                new int3(2, height - 8, 2), timber);
            a.Box(new int3(centreX - half + 3, y + 8, frontZ - 4),
                new int3(width - 6, 2, 2), timber);
            a.Box(new int3(centreX - half + 3, y + 17, frontZ - 4),
                new int3(width - 6, 2, 2), timber);
        }

        private static void AddTimberFrame(IStructureAuthoringSession a, int3 o,
            in NewHouseReferenceConfig c, byte timber)
        {
            int front = o.z - UpperFacadeProjection - 2;
            int upper = o.y + c.UpperFloorY;
            int eave = o.y + c.MainEaveY;
            int ridge = o.y + c.MainRidgeY;
            int centre = o.x + c.Width / 2;

            a.Box(new int3(o.x - 4, upper - 3, front),
                new int3(c.Width + 8, 5, TimberDepth), timber);
            a.Box(new int3(o.x - 4, eave - 4, front),
                new int3(c.Width + 8, 5, TimberDepth), timber);
            Post(a, o.x + 5, upper, eave, front, timber);
            Post(a, centre - 2, upper, eave, front, timber);
            Post(a, o.x + c.Width - 9, upper, eave, front, timber);

            // Heavy A-frame follows the principal gable rather than a side-facing roof.
            Line(a, o.x + 4, eave - 2, centre, ridge - 3, front, timber);
            Line(a, o.x + c.Width - 5, eave - 2, centre, ridge - 3, front, timber);
            Post(a, centre - 2, eave, ridge - 2, front, timber);

            // Crooked braces retain hand-built character without changing the dominant symmetry.
            Line(a, o.x + 8, upper + 5, o.x + 28, eave - 7, front, timber);
            Line(a, o.x + c.Width - 9, upper + 6, o.x + c.Width - 29, eave - 7, front, timber);
        }

        private static void FillFrontGable(IStructureAuthoringSession a,
            int centreX, int frontZ, int eaveY, int width, int rise, byte material)
        {
            int half = width / 2;
            for (int row = 0; row < rise; row++)
            {
                int rowHalf = math.max(1, half * (rise - row) / rise);
                a.Box(new int3(centreX - rowHalf, eaveY + row, frontZ),
                    new int3(rowHalf * 2 + 1, 1, 4), material);
            }
        }

        private static void AddChimney(IStructureAuthoringSession a, int3 o,
            in NewHouseReferenceConfig c, byte stone)
        {
            int x = o.x + 8;
            int z = o.z + 24;
            int h = c.MainRidgeY + 8;
            a.Box(new int3(x, o.y, z), new int3(16, h, 18), stone);
            a.Box(new int3(x - 2, o.y + h - 5, z - 2), new int3(20, 5, 22), stone);
            a.Box(new int3(x + 2, o.y + h, z + 3), new int3(5, 7, 5), stone);
            a.Box(new int3(x + 9, o.y + h, z + 10), new int3(5, 7, 5), stone);
        }

        private static void AddRidgeFinial(IStructureAuthoringSession a,
            int centreX, int ridgeY, int frontZ, byte timber)
        {
            a.Cylinder(centreX, ridgeY - 2, frontZ + 5, 2, 11, timber);
            a.Cone(centreX, ridgeY + 8, frontZ + 5, 5, 10, timber);
            a.Box(new int3(centreX - 5, ridgeY + 4, frontZ + 4),
                new int3(11, 2, 3), timber);
        }

        private static void AddFacadeDetails(IStructureAuthoringSession a, int3 o,
            in NewHouseReferenceConfig c, in NewHouseReferencePalette p)
        {
            int centre = o.x + c.Width / 2;
            int first = o.y + c.FirstFloorY;
            int upper = o.y + c.UpperFloorY;
            int frontLower = o.z - 6;
            int frontUpper = o.z - UpperFacadeProjection - 7;

            FlowerBox(a, centre - 43, first + 4, frontLower, 17, in p);
            FlowerBox(a, centre + 27, first + 4, frontLower, 17, in p);
            FlowerBox(a, centre - 17, upper + 1, frontUpper, 34, in p);
            FlowerBox(a, centre - 13, o.y + c.MainEaveY + 4, frontUpper, 26, in p);

            // Stone steps rise credibly to the high foundation/door threshold.
            for (int step = 0; step < 4; step++)
            {
                int width = 34 - step * 4;
                a.Box(new int3(centre - width / 2, o.y + step * 3, o.z - 18 + step * 4),
                    new int3(width, 3, 8), p.Stone);
            }

            // Organic ivy is kept mostly to one side as in the supplied reference.
            int ivyX = o.x + c.Width - 8;
            for (int i = 0; i < 19; i++)
            {
                int y = first + 4 + i * 3;
                int x = ivyX - (i % 4);
                int z = frontUpper - 1 - ((i / 4) & 1);
                a.Box(new int3(x, y, z), new int3(3, 3, 2), p.Foliage);
                if ((i & 2) != 0)
                    a.Box(new int3(x - 4, y + 2, z), new int3(3, 2, 2), p.Foliage);
            }
        }

        private static void FlowerBox(IStructureAuthoringSession a, int x, int y, int z, int w,
            in NewHouseReferencePalette p)
        {
            a.Box(new int3(x, y, z), new int3(w, 4, 5), p.Timber);
            for (int i = 2; i < w - 2; i += 5)
            {
                int stem = 3 + i % 3;
                a.Box(new int3(x + i, y + 4, z + 2), new int3(2, stem, 2), p.Foliage);
                a.Box(new int3(x + i - 1, y + 4 + stem, z + 1),
                    new int3(4, 3, 4), p.Flowers);
            }
        }

        private static void Shrub(IStructureAuthoringSession a, int x, int y, int z, int r, byte m)
        { a.Cone(x, y, z, r, r + 5, m); a.Disc(x, y + r / 2, z, r + 2, m); }

        private static void Post(IStructureAuthoringSession a, int x, int y0, int y1, int z, byte m)
        { a.Box(new int3(x, y0 + 1, z), new int3(4, y1 - y0 - 1, TimberDepth), m); }

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
