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
                upperFloorHeight <= 0 || wallThickness <= 0 || roofThickness <= 0 || roofOverhang < 0)
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
        public static NewHouseReferenceConfig Default => new(136, 78, 8, 34, 28, 4, 38, 4, 7);
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

        public static NewHouseReferenceResult AuthorHouse(IStructureAuthoringSession a, int3 o,
            in NewHouseReferenceConfig c, in NewHouseReferencePalette p)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            int firstY = o.y + c.FirstFloorY, upperY = o.y + c.UpperFloorY, eaveY = o.y + c.MainEaveY;

            a.Box(o, new int3(c.Width, c.FoundationHeight, c.Depth), p.Stone);
            a.HollowBox(new int3(o.x, firstY, o.z), new int3(c.Width, c.FirstFloorHeight, c.Depth),
                c.WallThickness, p.Plaster, false, true);
            a.HollowBox(new int3(o.x - 4, upperY, o.z - 4),
                new int3(c.Width + 8, c.UpperFloorHeight, c.Depth + 8),
                c.WallThickness, p.Plaster, true, false);

            GableX(a, o.x - c.RoofOverhang, o.x + c.Width + c.RoofOverhang,
                o.z - c.RoofOverhang, o.z + c.Depth + c.RoofOverhang,
                eaveY, c.MainRoofRise, c.RoofThickness, p.Roof);

            AddWing(a, o, in c, in p);
            AddEntry(a, o, in c, in p);
            AddDormer(a, o, in c, in p);
            AddChimney(a, o, in c, p.Stone);
            AddTimberFrame(a, o, in c, p.Timber);
            AddWindows(a, o, in c, in p);
            AddFacadeDetails(a, o, in c, in p);

            return new NewHouseReferenceResult(
                new int3(o.x - c.RoofOverhang - 8, o.y, o.z - 27),
                new int3(o.x + c.Width + 36, o.y + c.MainRidgeY + 18,
                    o.z + c.Depth + c.RoofOverhang + 4),
                o.x + c.Width / 2, o.z, o.y + c.MainRidgeY);
        }

        /// <summary>Reference-shot site dressing, deliberately separable from reusable house geometry.</summary>
        public static void AuthorReferenceSite(IStructureAuthoringSession a, int3 o,
            in NewHouseReferenceConfig c, in NewHouseReferencePalette p)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            a.Box(new int3(o.x - 46, o.y - 10, o.z - 62),
                new int3(c.Width + 100, 10, c.Depth + 118), p.Ground);
            int doorX = o.x + c.Width / 2;
            for (int z = o.z - 20; z >= o.z - 58; z -= 7)
            {
                int bend = ((o.z - z) / 7) % 4;
                a.Disc(doorX + (bend == 1 ? -3 : bend == 3 ? 3 : 0), o.y, z, 8, p.Stone);
            }
            a.Box(new int3(doorX - 15, o.y, o.z - 29), new int3(30, 2, 12), p.Stone);
            a.Box(new int3(doorX - 12, o.y + 2, o.z - 23), new int3(24, 2, 8), p.Stone);
            Shrub(a, o.x + 28, o.y, o.z - 14, 7, p.Foliage);
            Shrub(a, o.x + c.Width - 23, o.y, o.z - 10, 8, p.Foliage);
            Shrub(a, o.x + c.Width + 24, o.y, o.z + 12, 7, p.Foliage);
            Shrub(a, o.x - 14, o.y, o.z + 18, 6, p.Foliage);
        }

        private static void AddWing(IStructureAuthoringSession a, int3 o,
            in NewHouseReferenceConfig c, in NewHouseReferencePalette p)
        {
            int x = o.x + c.Width - 22, z = o.z + 12, y = o.y + c.FoundationHeight;
            a.HollowBox(new int3(x, y, z), new int3(58, 42, 58), c.WallThickness, p.Plaster, false, true);
            GableZ(a, x - 6, x + 64, z - 7, z + 65, y + 42, 25, c.RoofThickness, p.Roof);
            int faceZ = z - 2;
            a.Box(new int3(x + 5, y + 4, faceZ - 2), new int3(4, 30, TimberDepth), p.Timber);
            a.Box(new int3(x + 49, y + 4, faceZ - 2), new int3(4, 30, TimberDepth), p.Timber);
            a.Box(new int3(x + 5, y + 30, faceZ - 2), new int3(48, 4, TimberDepth), p.Timber);
            FrontWindow(a, x + 19, y + 10, faceZ, 22, 18, in p, false);
        }

        private static void AddEntry(IStructureAuthoringSession a, int3 o,
            in NewHouseReferenceConfig c, in NewHouseReferencePalette p)
        {
            const int width = 38, depth = 34, height = 42;
            int minX = o.x + c.Width / 2 - width / 2, minZ = o.z - 21, y = o.y + c.FoundationHeight;
            a.HollowBox(new int3(minX, y, minZ), new int3(width, height, depth),
                c.WallThickness, p.Plaster, false, true);
            GableZ(a, minX - 5, minX + width + 5, minZ - 6, minZ + depth + 4,
                y + height, 24, c.RoofThickness, p.Roof);

            const int doorW = 18, doorH = 27;
            int doorX = o.x + c.Width / 2 - doorW / 2, doorY = y + 2, faceZ = minZ;
            a.Carve(new int3(doorX, doorY, faceZ - 3), new int3(doorW, doorH, 9));
            a.Box(new int3(doorX + 2, doorY, faceZ - 2), new int3(doorW - 4, doorH - 5, 2), p.Door);
            int archY = doorY + doorH - 7;
            for (int row = 0; row < 7; row++)
            {
                int inset = row < 2 ? 1 : row < 4 ? 2 : row < 6 ? 3 : 5;
                a.Box(new int3(doorX + inset, archY + row, faceZ - 2),
                    new int3(math.max(2, doorW - inset * 2), 1, 2), p.Door);
            }
            a.Box(new int3(doorX - 3, doorY - 1, faceZ - 4), new int3(3, doorH + 3, 3), p.Timber);
            a.Box(new int3(doorX + doorW, doorY - 1, faceZ - 4), new int3(3, doorH + 3, 3), p.Timber);
            Line(a, doorX - 1, archY + 3, doorX + doorW / 2, doorY + doorH + 3, faceZ - 4, p.Timber);
            Line(a, doorX + doorW / 2, doorY + doorH + 3, doorX + doorW + 1, archY + 3, faceZ - 4, p.Timber);
        }

        private static void AddDormer(IStructureAuthoringSession a, int3 o,
            in NewHouseReferenceConfig c, in NewHouseReferencePalette p)
        {
            const int width = 42;
            int x = o.x + c.Width / 2 - width / 2 + 5, z = o.z - 13;
            int baseY = o.y + c.UpperFloorY + 8, eave = o.y + c.MainEaveY + 17;
            a.HollowBox(new int3(x, baseY, z), new int3(width, eave - baseY, 30),
                c.WallThickness, p.Plaster, true, true);
            GableZ(a, x - 5, x + width + 5, z - 5, z + 31, eave, 23, c.RoofThickness, p.Roof);
            FrontWindow(a, x + 10, baseY + 7, z, 22, 17, in p, false);
            a.Box(new int3(x + width / 2 - 2, baseY + 1, z - 3), new int3(4, eave - baseY + 16, 3), p.Timber);
            Line(a, x + 4, eave, x + width / 2, eave + 18, z - 3, p.Timber);
            Line(a, x + width - 4, eave, x + width / 2, eave + 18, z - 3, p.Timber);
        }

        private static void AddChimney(IStructureAuthoringSession a, int3 o,
            in NewHouseReferenceConfig c, byte stone)
        {
            int x = o.x + 8, z = o.z - 7, h = c.MainRidgeY + 13;
            a.Box(new int3(x, o.y, z), new int3(18, h, 19), stone);
            a.Box(new int3(x - 2, o.y + h - 4, z - 2), new int3(22, 5, 23), stone);
            a.Box(new int3(x + 3, o.y + h + 1, z + 3), new int3(5, 6, 5), stone);
            a.Box(new int3(x + 10, o.y + h + 1, z + 10), new int3(5, 6, 5), stone);
        }

        private static void AddTimberFrame(IStructureAuthoringSession a, int3 o,
            in NewHouseReferenceConfig c, byte timber)
        {
            int z = o.z - 3, first = o.y + c.FirstFloorY, upper = o.y + c.UpperFloorY, eave = o.y + c.MainEaveY;
            Post(a, o.x + 3, first, eave, z, timber); Post(a, o.x + 35, first, eave, z, timber);
            Post(a, o.x + 68, first, eave, z, timber); Post(a, o.x + 101, first, eave, z, timber);
            Post(a, o.x + c.Width - 7, first, eave, z, timber);
            a.Box(new int3(o.x, upper - 2, z), new int3(c.Width, 5, 3), timber);
            a.Box(new int3(o.x, eave - 5, z), new int3(c.Width, 5, 3), timber);
            Line(a, o.x + 7, first + 7, o.x + 31, upper - 5, z, timber);
            Line(a, o.x + 39, upper - 5, o.x + 61, first + 8, z, timber);
            Line(a, o.x + 75, first + 8, o.x + 96, upper - 5, z, timber);
            Line(a, o.x + 108, upper - 5, o.x + 128, first + 8, z, timber);
            Line(a, o.x + 6, upper + 5, o.x + 31, eave - 6, z, timber);
            Line(a, o.x + 40, eave - 6, o.x + 61, upper + 5, z, timber);
            Line(a, o.x + 75, upper + 5, o.x + 97, eave - 6, z, timber);
            Line(a, o.x + 107, eave - 6, o.x + 129, upper + 6, z, timber);
        }

        private static void AddWindows(IStructureAuthoringSession a, int3 o,
            in NewHouseReferenceConfig c, in NewHouseReferencePalette p)
        {
            int low = o.y + c.FoundationHeight + 9, high = o.y + c.UpperFloorY + 7;
            FrontWindow(a, o.x + 31, low, o.z, 21, 18, in p, true);
            FrontWindow(a, o.x + 91, low + 1, o.z, 22, 18, in p, true);
            FrontWindow(a, o.x + 25, high, o.z - 4, 20, 17, in p, false);
            FrontWindow(a, o.x + 99, high + 1, o.z - 4, 20, 16, in p, false);
        }

        private static void AddFacadeDetails(IStructureAuthoringSession a, int3 o,
            in NewHouseReferenceConfig c, in NewHouseReferencePalette p)
        {
            int front = o.z - 5, baseY = o.y + c.FoundationHeight;
            a.Box(new int3(o.x - 3, baseY, front), new int3(5, c.MainEaveY - c.FoundationHeight, 5), p.Timber);
            a.Box(new int3(o.x + c.Width - 2, baseY, front), new int3(5, c.MainEaveY - c.FoundationHeight, 5), p.Timber);
            a.Box(new int3(o.x - 5, o.y + c.UpperFloorY - 4, front), new int3(c.Width + 10, 5, 5), p.Timber);
            FlowerBox(a, o.x + 29, baseY + 6, front - 2, 25, in p);
            FlowerBox(a, o.x + 89, baseY + 7, front - 2, 26, in p);
            for (int i = 0; i < 16; i++)
            {
                int y = baseY + 3 + i * 2, x = o.x + c.Width - 9 - i % 4;
                a.Box(new int3(x, y, front - 1), new int3(3, 3, 2), p.Foliage);
                if ((i & 2) != 0) a.Box(new int3(x - 4, y + 2, front - 1), new int3(3, 2, 2), p.Foliage);
            }
        }

        private static void FrontWindow(IStructureAuthoringSession a, int x, int y, int z,
            int w, int h, in NewHouseReferencePalette p, bool shutters)
        {
            a.Carve(new int3(x, y, z - 5), new int3(w, h, 12));
            a.Box(new int3(x + 3, y + 3, z - 3), new int3(w - 6, h - 6, 2), p.Glass);
            int fz = z - 5;
            a.Box(new int3(x - 2, y - 2, fz), new int3(w + 4, 3, 3), p.Timber);
            a.Box(new int3(x - 2, y + h - 1, fz), new int3(w + 4, 3, 3), p.Timber);
            a.Box(new int3(x - 2, y, fz), new int3(3, h, 3), p.Timber);
            a.Box(new int3(x + w - 1, y, fz), new int3(3, h, 3), p.Timber);
            a.Box(new int3(x + w / 2 - 1, y + 1, fz - 1), new int3(2, h - 2, 2), p.Timber);
            a.Box(new int3(x + 1, y + h / 2 - 1, fz - 1), new int3(w - 2, 2, 2), p.Timber);
            if (!shutters) return;
            a.Box(new int3(x - 8, y, fz - 1), new int3(5, h, 2), p.Accent);
            a.Box(new int3(x + w + 3, y, fz - 1), new int3(5, h, 2), p.Accent);
        }

        private static void FlowerBox(IStructureAuthoringSession a, int x, int y, int z, int w,
            in NewHouseReferencePalette p)
        {
            a.Box(new int3(x, y, z), new int3(w, 4, 5), p.Timber);
            for (int i = 2; i < w - 2; i += 5)
            {
                int stem = 3 + i % 3;
                a.Box(new int3(x + i, y + 4, z + 2), new int3(2, stem, 2), p.Foliage);
                a.Box(new int3(x + i - 1, y + 4 + stem, z + 1), new int3(4, 3, 4), p.Flowers);
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
            if (steps == 0) { a.Box(new int3(x0, y0, z), new int3(2, 2, TimberDepth), m); return; }
            for (int i = 0; i <= steps; i++)
                a.Box(new int3(x0 + dx * i / steps, y0 + dy * i / steps, z), new int3(2, 2, TimberDepth), m);
        }
    }
}
