using System;
using Game.WorldBuilder.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.WorldBuilder.Voxel
{
    /// <summary>Concrete voxel material IDs bound to semantic roles in a town-architecture program.</summary>
    public readonly struct TownArchitectureVoxelPalette
    {
        public readonly byte Wall;
        public readonly byte Roof;
        public readonly byte Structure;
        public readonly byte Ground;
        public readonly byte Trim;
        public readonly byte Accent;

        public TownArchitectureVoxelPalette(
            byte wall,
            byte roof,
            byte structure,
            byte ground,
            byte trim,
            byte accent)
        {
            Wall = wall;
            Roof = roof;
            Structure = structure;
            Ground = ground;
            Trim = trim;
            Accent = accent;
        }
    }

    /// <summary>
    /// Shared, terrain-aware voxel realization for town-architecture programs. The district layout is
    /// role driven: scenes supply only a centre, terrain query and material-role mapping.
    /// </summary>
    public static class WorldBuilderTownArchitectureVoxelAuthoring
    {
        private const int DistrictHalfWidth = 82;
        private const int DistrictHalfDepth = 66;

        public static void Author(
            IStructureAuthoringSession authoring,
            int2 districtCentre,
            Func<int, int, int> terrainHeightAt,
            TownArchitectureProgram program,
            in TownArchitectureVoxelPalette palette)
        {
            if (authoring == null) throw new ArgumentNullException(nameof(authoring));
            if (terrainHeightAt == null) throw new ArgumentNullException(nameof(terrainHeightAt));
            if (program == null) throw new ArgumentNullException(nameof(program));

            AuthorApproach(authoring, districtCentre, terrainHeightAt, in palette);
            AuthorPaletteDisplay(authoring, districtCentre, terrainHeightAt, in palette);
            AuthorLabel(authoring, districtCentre, terrainHeightAt, program.DisplayName, in palette);

            int3 residence = Grounded(districtCentre + new int2(-47, -12), terrainHeightAt);
            int3 commerce = Grounded(districtCentre + new int2(40, -12), terrainHeightAt);
            int3 civic = Grounded(districtCentre + new int2(-47, 34), terrainHeightAt);
            int3 landmark = Grounded(districtCentre + new int2(40, 34), terrainHeightAt);

            switch (program.Silhouette)
            {
                case TownArchitectureSilhouette.PastoralTimberFrame:
                    AuthorKentridge(authoring, residence, commerce, civic, landmark, in palette);
                    break;
                case TownArchitectureSilhouette.CivicVerticalStone:
                    AuthorHightown(authoring, residence, commerce, civic, landmark, in palette);
                    break;
                case TownArchitectureSilhouette.MoorlandLowStone:
                    AuthorMoordell(authoring, residence, commerce, civic, landmark, in palette);
                    break;
                case TownArchitectureSilhouette.RoyalFortified:
                    AuthorRossdam(authoring, residence, commerce, civic, landmark, in palette);
                    break;
                case TownArchitectureSilhouette.OrganicCanopy:
                    AuthorFairyVillage(authoring, residence, commerce, civic, landmark, in palette);
                    break;
                case TownArchitectureSilhouette.TribalHeavyTimber:
                    AuthorOrcVillage(authoring, residence, commerce, civic, landmark, in palette);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(program), program.Silhouette, "Unsupported town silhouette.");
            }
        }

        private static int3 Grounded(int2 xz, Func<int, int, int> terrainHeightAt) =>
            new int3(xz.x, terrainHeightAt(xz.x, xz.y) + 1, xz.y);

        private static void AuthorApproach(
            IStructureAuthoringSession a,
            int2 c,
            Func<int, int, int> height,
            in TownArchitectureVoxelPalette p)
        {
            // Short stepped paving strips make the district walkable while following local terrain.
            for (int z = -48; z <= 48; z += 8)
            {
                int x = c.x;
                int worldZ = c.y + z;
                int y = height(x, worldZ) + 1;
                a.Box(new int3(x - 7, y, worldZ - 4), new int3(14, 2, 8), p.Ground);
            }

            for (int x = -62; x <= 62; x += 8)
            {
                int worldX = c.x + x;
                int z = c.y + 8;
                int y = height(worldX, z) + 1;
                a.Box(new int3(worldX - 4, y, z - 6), new int3(8, 2, 12), p.Ground);
            }
        }

        private static void AuthorPaletteDisplay(
            IStructureAuthoringSession a,
            int2 c,
            Func<int, int, int> height,
            in TownArchitectureVoxelPalette p)
        {
            byte[] materials = { p.Wall, p.Roof, p.Structure, p.Ground, p.Trim, p.Accent };
            int startX = c.x - 45;
            int z = c.y - 48;
            for (int i = 0; i < materials.Length; i++)
            {
                int x = startX + i * 18;
                int y = height(x, z) + 1;
                a.Box(new int3(x - 6, y - 2, z - 5), new int3(12, 3, 10), p.Structure);
                a.Box(new int3(x - 5, y + 1, z - 4), new int3(10, 10, 8), materials[i]);
            }
        }

        private static void AuthorLabel(
            IStructureAuthoringSession a,
            int2 c,
            Func<int, int, int> height,
            string label,
            in TownArchitectureVoxelPalette p)
        {
            string text = label.ToUpperInvariant();
            const int Scale = 2;
            const int Advance = 12;
            int textWidth = Math.Max(1, text.Length * Advance - 2);
            int z = c.y - DistrictHalfDepth;
            int y = height(c.x, z) + 1;
            int left = c.x - textWidth / 2;

            // Freestanding supported sign; the lettering is actual voxel geometry, not an editor label.
            a.Box(new int3(left - 6, y + 4, z), new int3(textWidth + 12, 22, 3), p.Trim);
            a.Box(new int3(left - 3, y + 7, z - 2), new int3(textWidth + 6, 16, 2), p.Wall);
            a.Box(new int3(left - 3, y - 2, z + 1), new int3(4, 8, 4), p.Structure);
            a.Box(new int3(left + textWidth - 1, y - 2, z + 1), new int3(4, 8, 4), p.Structure);

            for (int i = 0; i < text.Length; i++)
                AuthorGlyph(a, text[i], new int3(left + i * Advance, y + 8, z - 4), Scale, p.Accent);
        }

        private static void AuthorGlyph(IStructureAuthoringSession a, char ch, int3 origin, int scale, byte material)
        {
            string[] rows = Glyph(ch);
            for (int row = 0; row < rows.Length; row++)
            for (int col = 0; col < rows[row].Length; col++)
            {
                if (rows[row][col] != '#') continue;
                a.Box(
                    new int3(origin.x + col * scale, origin.y + (6 - row) * scale, origin.z),
                    new int3(scale, scale, 2),
                    material);
            }
        }

        private static string[] Glyph(char ch)
        {
            switch (ch)
            {
                case 'A': return Rows(".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#");
                case 'C': return Rows(".####", "#....", "#....", "#....", "#....", "#....", ".####");
                case 'D': return Rows("####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####.");
                case 'E': return Rows("#####", "#....", "#....", "####.", "#....", "#....", "#####");
                case 'F': return Rows("#####", "#....", "#....", "####.", "#....", "#....", "#....");
                case 'G': return Rows(".####", "#....", "#....", "#.###", "#...#", "#...#", ".###.");
                case 'H': return Rows("#...#", "#...#", "#...#", "#####", "#...#", "#...#", "#...#");
                case 'I': return Rows("#####", "..#..", "..#..", "..#..", "..#..", "..#..", "#####");
                case 'K': return Rows("#...#", "#..#.", "#.#..", "##...", "#.#..", "#..#.", "#...#");
                case 'L': return Rows("#....", "#....", "#....", "#....", "#....", "#....", "#####");
                case 'M': return Rows("#...#", "##.##", "#.#.#", "#.#.#", "#...#", "#...#", "#...#");
                case 'N': return Rows("#...#", "##..#", "##..#", "#.#.#", "#..##", "#..##", "#...#");
                case 'O': return Rows(".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###.");
                case 'R': return Rows("####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#");
                case 'S': return Rows(".####", "#....", "#....", ".###.", "....#", "....#", "####.");
                case 'T': return Rows("#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#..");
                case 'V': return Rows("#...#", "#...#", "#...#", "#...#", "#...#", ".#.#.", "..#..");
                case 'W': return Rows("#...#", "#...#", "#...#", "#.#.#", "#.#.#", "##.##", "#...#");
                case 'Y': return Rows("#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#..");
                case ' ': return Rows(".....", ".....", ".....", ".....", ".....", ".....", ".....");
                default: return Rows("#####", "....#", "...#.", "..#..", ".#...", ".....", ".#...");
            }
        }

        private static string[] Rows(params string[] rows) => rows;

        private static void Foundation(IStructureAuthoringSession a, int3 origin, int width, int depth, byte material)
        {
            a.Box(new int3(origin.x - width / 2, origin.y - 5, origin.z - depth / 2), new int3(width, 6, depth), material);
        }

        private static void ShellWithGable(
            IStructureAuthoringSession a,
            int3 origin,
            int width,
            int depth,
            int wallHeight,
            int roofHeight,
            in TownArchitectureVoxelPalette p,
            bool heavyFrame = false)
        {
            Foundation(a, origin, width + 6, depth + 6, p.Ground);
            int3 min = new int3(origin.x - width / 2, origin.y, origin.z - depth / 2);
            a.HollowBox(min, new int3(width, wallHeight, depth), 2, p.Wall, true, true);
            a.Gable(
                new int3(min.x - 3, min.y + wallHeight, min.z - 3),
                new int3(width + 6, roofHeight, depth + 6),
                true,
                p.Roof);
            FrameCorners(a, min, width, depth, wallHeight, heavyFrame ? 4 : 2, p.Structure);
            CarveSouthDoor(a, min, width, p.Trim);
            SouthWindows(a, min, width, wallHeight, p.Accent);
        }

        private static void FrameCorners(
            IStructureAuthoringSession a,
            int3 min,
            int width,
            int depth,
            int height,
            int thickness,
            byte material)
        {
            int t = thickness;
            a.Box(new int3(min.x, min.y, min.z), new int3(t, height, t), material);
            a.Box(new int3(min.x + width - t, min.y, min.z), new int3(t, height, t), material);
            a.Box(new int3(min.x, min.y, min.z + depth - t), new int3(t, height, t), material);
            a.Box(new int3(min.x + width - t, min.y, min.z + depth - t), new int3(t, height, t), material);
            a.Box(new int3(min.x, min.y + height / 2, min.z - 1), new int3(width, t, t + 1), material);
        }

        private static void CarveSouthDoor(IStructureAuthoringSession a, int3 min, int width, byte trim)
        {
            int x = min.x + width / 2 - 4;
            a.Carve(new int3(x, min.y + 1, min.z - 1), new int3(8, 16, 4));
            a.Box(new int3(x - 2, min.y, min.z - 2), new int3(2, 19, 3), trim);
            a.Box(new int3(x + 8, min.y, min.z - 2), new int3(2, 19, 3), trim);
            a.Box(new int3(x - 2, min.y + 17, min.z - 2), new int3(12, 2, 3), trim);
        }

        private static void SouthWindows(IStructureAuthoringSession a, int3 min, int width, int wallHeight, byte accent)
        {
            int y = min.y + Math.Max(7, wallHeight / 3);
            int left = min.x + 6;
            int right = min.x + width - 12;
            a.Box(new int3(left, y, min.z - 2), new int3(6, 8, 2), accent);
            a.Box(new int3(right, y, min.z - 2), new int3(6, 8, 2), accent);
        }

        private static void Awning(IStructureAuthoringSession a, int3 origin, int width, byte material)
        {
            a.Box(new int3(origin.x - width / 2, origin.y + 14, origin.z - 20), new int3(width, 3, 10), material);
            a.Box(new int3(origin.x - width / 2 + 2, origin.y, origin.z - 20), new int3(2, 16, 2), material);
            a.Box(new int3(origin.x + width / 2 - 4, origin.y, origin.z - 20), new int3(2, 16, 2), material);
        }

        private static void AuthorKentridge(
            IStructureAuthoringSession a, int3 residence, int3 commerce, int3 civic, int3 landmark,
            in TownArchitectureVoxelPalette p)
        {
            ShellWithGable(a, residence, 36, 30, 24, 13, in p, true);
            ShellWithGable(a, commerce, 42, 32, 24, 12, in p, true);
            Awning(a, commerce, 34, p.Accent);

            ShellWithGable(a, civic, 42, 34, 30, 16, in p, true);
            int3 tower = civic + new int3(0, 0, 13);
            a.HollowBox(tower + new int3(-7, 0, -7), new int3(14, 42, 14), 2, p.Wall, true, true);
            a.Gable(tower + new int3(-9, 42, -9), new int3(18, 12, 18), true, p.Roof);

            Foundation(a, landmark, 34, 30, p.Ground);
            a.Cylinder(landmark.x, landmark.y, landmark.z, 9, 6, p.Wall, 6);
            for (int dx = -11; dx <= 11; dx += 22)
            for (int dz = -8; dz <= 8; dz += 16)
                a.Box(new int3(landmark.x + dx - 1, landmark.y, landmark.z + dz - 1), new int3(3, 26, 3), p.Structure);
            a.Gable(new int3(landmark.x - 15, landmark.y + 24, landmark.z - 12), new int3(30, 10, 24), true, p.Roof);
        }

        private static void AuthorHightown(
            IStructureAuthoringSession a, int3 residence, int3 commerce, int3 civic, int3 landmark,
            in TownArchitectureVoxelPalette p)
        {
            ShellWithGable(a, residence, 30, 26, 38, 14, in p);
            a.Box(residence + new int3(-15, 19, -14), new int3(30, 3, 3), p.Trim);

            ShellWithGable(a, commerce, 40, 30, 32, 14, in p);
            Awning(a, commerce, 36, p.Trim);
            a.Box(commerce + new int3(-14, 24, -17), new int3(28, 4, 3), p.Accent);

            ShellWithGable(a, civic, 48, 36, 42, 18, in p);
            for (int x = -18; x <= 18; x += 12)
                a.Box(civic + new int3(x - 2, 0, -22), new int3(4, 32, 4), p.Structure);

            Foundation(a, landmark, 36, 36, p.Ground);
            a.HollowBox(landmark + new int3(-11, 0, -11), new int3(22, 52, 22), 3, p.Wall, true, true);
            a.Cone(landmark.x, landmark.y + 52, landmark.z, 15, 18, p.Roof);
            a.Box(landmark + new int3(-8, 34, -13), new int3(16, 10, 3), p.Accent);
        }

        private static void AuthorMoordell(
            IStructureAuthoringSession a, int3 residence, int3 commerce, int3 civic, int3 landmark,
            in TownArchitectureVoxelPalette p)
        {
            ShellWithGable(a, residence, 40, 32, 18, 9, in p, true);
            a.Box(residence + new int3(-20, 3, 15), new int3(40, 4, 4), p.Trim);

            ShellWithGable(a, commerce, 46, 34, 22, 10, in p, true);
            Awning(a, commerce, 30, p.Roof);
            a.Box(commerce + new int3(12, 22, 5), new int3(7, 16, 7), p.Trim);

            ShellWithGable(a, civic, 48, 36, 22, 11, in p, true);
            a.Box(civic + new int3(-25, 0, -19), new int3(50, 5, 5), p.Trim);

            Foundation(a, landmark, 54, 42, p.Ground);
            a.Box(landmark + new int3(-27, 0, -21), new int3(54, 5, 3), p.Wall);
            a.Box(landmark + new int3(-27, 0, 18), new int3(54, 5, 3), p.Wall);
            for (int x = -18; x <= 18; x += 12)
            {
                a.Box(landmark + new int3(x - 2, 1, -7), new int3(4, 13, 4), p.Wall);
                a.Box(landmark + new int3(x - 6, 9, -7), new int3(12, 3, 4), p.Trim);
            }
            a.Cone(landmark.x + 16, landmark.y, landmark.z + 10, 8, 15, p.Trim);
        }

        private static void AuthorRossdam(
            IStructureAuthoringSession a, int3 residence, int3 commerce, int3 civic, int3 landmark,
            in TownArchitectureVoxelPalette p)
        {
            ShellWithGable(a, residence, 34, 28, 34, 13, in p);
            for (int x = -12; x <= 12; x += 8)
                a.Box(residence + new int3(x - 1, 0, -18), new int3(3, 28, 3), p.Trim);

            ShellWithGable(a, commerce, 42, 30, 30, 12, in p);
            Awning(a, commerce, 38, p.Accent);
            a.Box(commerce + new int3(-18, 23, -17), new int3(36, 4, 3), p.Trim);

            ShellWithGable(a, civic, 54, 40, 38, 15, in p);
            for (int x = -20; x <= 20; x += 10)
                a.Box(civic + new int3(x - 2, 0, -24), new int3(4, 34, 4), p.Structure);
            a.Box(civic + new int3(-18, 30, -22), new int3(36, 5, 3), p.Accent);

            Foundation(a, landmark, 64, 44, p.Ground);
            int leftX = landmark.x - 19;
            int rightX = landmark.x + 19;
            a.HollowBox(new int3(leftX - 9, landmark.y, landmark.z - 10), new int3(18, 36, 20), 3, p.Wall, true, true);
            a.HollowBox(new int3(rightX - 9, landmark.y, landmark.z - 10), new int3(18, 36, 20), 3, p.Wall, true, true);
            a.Box(new int3(leftX + 9, landmark.y + 18, landmark.z - 6), new int3(20, 8, 12), p.Wall);
            a.Carve(new int3(landmark.x - 6, landmark.y + 1, landmark.z - 8), new int3(12, 18, 16));
            a.Crenellate(new int3(leftX - 9, landmark.y + 36, landmark.z - 10), new int3(5, 0, 0), 4, 4, 6, 3, 2, p.Trim);
            a.Crenellate(new int3(rightX - 9, landmark.y + 36, landmark.z - 10), new int3(5, 0, 0), 4, 4, 6, 3, 2, p.Trim);
        }

        private static void AuthorFairyVillage(
            IStructureAuthoringSession a, int3 residence, int3 commerce, int3 civic, int3 landmark,
            in TownArchitectureVoxelPalette p)
        {
            Foundation(a, residence, 42, 38, p.Ground);
            a.Cylinder(residence.x, residence.y, residence.z, 7, 32, p.Structure);
            a.Disc(residence.x, residence.y + 25, residence.z, 19, p.Structure);
            a.HollowBox(residence + new int3(-12, 27, -11), new int3(24, 17, 22), 2, p.Wall, true, true);
            a.Cone(residence.x, residence.y + 44, residence.z, 17, 14, p.Roof);
            a.Box(residence + new int3(-2, 3, -9), new int3(4, 24, 4), p.Trim);

            Foundation(a, commerce, 44, 38, p.Ground);
            a.Cylinder(commerce.x, commerce.y, commerce.z, 6, 18, p.Structure);
            a.Disc(commerce.x, commerce.y + 16, commerce.z, 20, p.Roof);
            a.Cone(commerce.x, commerce.y + 16, commerce.z, 20, 12, p.Roof);
            a.Box(commerce + new int3(-14, 8, -15), new int3(28, 7, 4), p.Accent);

            Foundation(a, civic, 52, 44, p.Ground);
            for (int x = -18; x <= 18; x += 12)
                a.Cylinder(civic.x + x, civic.y, civic.z, 3, 28, p.Structure);
            a.Disc(civic.x, civic.y + 25, civic.z, 25, p.Roof);
            a.Cone(civic.x, civic.y + 25, civic.z, 25, 12, p.Roof);
            a.Cylinder(civic.x, civic.y + 1, civic.z, 5, 10, p.Accent);

            Foundation(a, landmark, 58, 42, p.Ground);
            a.Arch(landmark + new int3(-25, 0, -5), 50, 28, 10, 0, p.Structure);
            for (int x = -22; x <= 22; x += 11)
                a.Cone(landmark.x + x, landmark.y, landmark.z + 12, 4, 15 + (Math.Abs(x) % 5), p.Accent);
        }

        private static void AuthorOrcVillage(
            IStructureAuthoringSession a, int3 residence, int3 commerce, int3 civic, int3 landmark,
            in TownArchitectureVoxelPalette p)
        {
            ShellWithGable(a, residence, 40, 32, 20, 10, in p, true);
            a.Box(residence + new int3(-22, 16, -18), new int3(44, 5, 7), p.Roof);

            ShellWithGable(a, commerce, 44, 34, 23, 10, in p, true);
            Awning(a, commerce, 36, p.Trim);
            a.Box(commerce + new int3(12, 20, 5), new int3(8, 24, 8), p.Wall);
            a.Cone(commerce.x + 16, commerce.y + 44, commerce.z + 9, 6, 8, p.Trim);

            ShellWithGable(a, civic, 56, 38, 24, 13, in p, true);
            for (int x = -24; x <= 24; x += 12)
                a.Box(civic + new int3(x - 2, 0, -22), new int3(5, 31, 5), p.Structure);
            a.Box(civic + new int3(-22, 18, -24), new int3(44, 5, 5), p.Accent);

            Foundation(a, landmark, 66, 46, p.Ground);
            for (int x = -30; x <= 30; x += 6)
                a.Box(landmark + new int3(x - 2, 0, 14), new int3(4, 26 + (Math.Abs(x) % 5), 4), p.Structure);
            for (int x = -20; x <= 20; x += 40)
            {
                a.HollowBox(landmark + new int3(x - 9, 0, -9), new int3(18, 32, 18), 3, p.Wall, true, true);
                a.Gable(landmark + new int3(x - 11, 32, -11), new int3(22, 10, 22), true, p.Roof);
            }
            a.Box(landmark + new int3(-11, 17, -5), new int3(22, 7, 10), p.Trim);
            a.Carve(landmark + new int3(-6, 1, -8), new int3(12, 18, 16));
        }
    }
}
