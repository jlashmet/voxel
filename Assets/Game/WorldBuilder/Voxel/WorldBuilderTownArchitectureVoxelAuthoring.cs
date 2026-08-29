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

        public TownArchitectureVoxelPalette(byte wall, byte roof, byte structure, byte ground, byte trim, byte accent)
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
    /// Shared terrain-aware voxel realization for town-architecture programs. Scenes supply only a district
    /// centre, terrain query and material-role mapping. All architecture and 10 cm construction details live here.
    /// </summary>
    public static class WorldBuilderTownArchitectureVoxelAuthoring
    {
        public const int DistrictHalfWidthVoxels = 82;
        public const int DistrictHalfDepthVoxels = 66;
        public const int EstimatedMaxHeightVoxels = 78;

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

            int seedShift = (int)(program.Seed % 5u) - 2;
            int3 residence = Grounded(districtCentre + new int2(-47 + seedShift, -12), terrainHeightAt);
            int3 commerce = Grounded(districtCentre + new int2(40, -12 + seedShift), terrainHeightAt);
            int3 civic = Grounded(districtCentre + new int2(-47, 34 - seedShift), terrainHeightAt);
            int3 landmark = Grounded(districtCentre + new int2(40 - seedShift, 34), terrainHeightAt);

            switch (program.Silhouette)
            {
                case TownArchitectureSilhouette.PastoralTimberFrame:
                    AuthorKentridge(authoring, residence, commerce, civic, landmark, program, in palette);
                    break;
                case TownArchitectureSilhouette.CivicVerticalStone:
                    AuthorHightown(authoring, residence, commerce, civic, landmark, program, in palette);
                    break;
                case TownArchitectureSilhouette.MoorlandLowStone:
                    AuthorMoordell(authoring, residence, commerce, civic, landmark, program, in palette);
                    break;
                case TownArchitectureSilhouette.RoyalFortified:
                    AuthorRossdam(authoring, residence, commerce, civic, landmark, program, in palette);
                    break;
                case TownArchitectureSilhouette.OrganicCanopy:
                    AuthorFairyVillage(authoring, residence, commerce, civic, landmark, program, in palette);
                    break;
                case TownArchitectureSilhouette.TribalHeavyTimber:
                    AuthorOrcVillage(authoring, residence, commerce, civic, landmark, program, in palette);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(program), program.Silhouette, "Unsupported town silhouette.");
            }
        }

        private static int3 Grounded(int2 xz, Func<int, int, int> terrainHeightAt) =>
            new int3(xz.x, terrainHeightAt(xz.x, xz.y) + 1, xz.y);

        private static int U(TownArchitectureProgram program) => Math.Max(1, program.DetailUnitBlocks);

        private static void AuthorApproach(IStructureAuthoringSession a, int2 c, Func<int, int, int> height, in TownArchitectureVoxelPalette p)
        {
            for (int z = -48; z <= 48; z += 8)
            {
                int worldZ = c.y + z;
                int y = height(c.x, worldZ) + 1;
                a.Box(new int3(c.x - 7, y, worldZ - 4), new int3(14, 2, 8), p.Ground);
            }

            for (int x = -62; x <= 62; x += 8)
            {
                int worldX = c.x + x;
                int z = c.y + 8;
                int y = height(worldX, z) + 1;
                a.Box(new int3(worldX - 4, y, z - 6), new int3(8, 2, 12), p.Ground);
            }
        }

        private static void AuthorPaletteDisplay(IStructureAuthoringSession a, int2 c, Func<int, int, int> height, in TownArchitectureVoxelPalette p)
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

        private static void AuthorLabel(IStructureAuthoringSession a, int2 c, Func<int, int, int> height, string label, in TownArchitectureVoxelPalette p)
        {
            string text = label.ToUpperInvariant();
            const int scale = 2;
            const int advance = 12;
            int textWidth = Math.Max(1, text.Length * advance - 2);
            int z = c.y - DistrictHalfDepthVoxels;
            int y = height(c.x, z) + 1;
            int left = c.x - textWidth / 2;

            a.Box(new int3(left - 6, y + 4, z), new int3(textWidth + 12, 22, 3), p.Trim);
            a.Box(new int3(left - 3, y + 7, z - 2), new int3(textWidth + 6, 16, 2), p.Wall);
            a.Box(new int3(left - 3, y - 2, z + 1), new int3(4, 8, 4), p.Structure);
            a.Box(new int3(left + textWidth - 1, y - 2, z + 1), new int3(4, 8, 4), p.Structure);

            for (int i = 0; i < text.Length; i++)
                AuthorGlyph(a, text[i], new int3(left + i * advance, y + 8, z - 4), scale, p.Accent);
        }

        private static void AuthorGlyph(IStructureAuthoringSession a, char ch, int3 origin, int scale, byte material)
        {
            string[] rows = Glyph(ch);
            for (int row = 0; row < rows.Length; row++)
            for (int col = 0; col < rows[row].Length; col++)
            {
                if (rows[row][col] != '#') continue;
                a.Box(new int3(origin.x + col * scale, origin.y + (6 - row) * scale, origin.z), new int3(scale, scale, 2), material);
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

        private static int3 GabledShell(
            IStructureAuthoringSession a,
            int3 origin,
            int width,
            int depth,
            int wallHeight,
            int roofHeight,
            TownArchitectureProgram program,
            in TownArchitectureVoxelPalette p,
            int windowRows,
            int windowColumns,
            bool heavyFrame = false)
        {
            Foundation(a, origin, width + 6, depth + 6, p.Ground);
            int3 min = new int3(origin.x - width / 2, origin.y, origin.z - depth / 2);
            a.HollowBox(min, new int3(width, wallHeight, depth), 2, p.Wall, true, true);
            a.Gable(new int3(min.x - 3, min.y + wallHeight, min.z - 3), new int3(width + 6, roofHeight, depth + 6), true, p.Roof);
            FrameCorners(a, min, width, depth, wallHeight, heavyFrame ? 3 * U(program) : 2 * U(program), p.Structure);
            AuthorFacadeDetails(a, min, width, wallHeight, windowRows, windowColumns, program, in p);
            AuthorGableFinish(a, min, width, depth, wallHeight, roofHeight, program, in p);
            return min;
        }

        private static int3 FlatParapetShell(
            IStructureAuthoringSession a,
            int3 origin,
            int width,
            int depth,
            int wallHeight,
            TownArchitectureProgram program,
            in TownArchitectureVoxelPalette p,
            int windowRows,
            int windowColumns)
        {
            Foundation(a, origin, width + 8, depth + 8, p.Ground);
            int3 min = new int3(origin.x - width / 2, origin.y, origin.z - depth / 2);
            a.HollowBox(min, new int3(width, wallHeight, depth), 3, p.Wall, true, true);
            AuthorFacadeDetails(a, min, width, wallHeight, windowRows, windowColumns, program, in p);
            AuthorMasonryCourses(a, min, width, wallHeight, program, p.Trim);
            AuthorLayeredCoping(a, min, width, depth, wallHeight, program, p.Trim);
            return min;
        }

        private static void FrameCorners(IStructureAuthoringSession a, int3 min, int width, int depth, int height, int thickness, byte material)
        {
            int t = Math.Max(1, thickness);
            a.Box(new int3(min.x, min.y, min.z), new int3(t, height, t), material);
            a.Box(new int3(min.x + width - t, min.y, min.z), new int3(t, height, t), material);
            a.Box(new int3(min.x, min.y, min.z + depth - t), new int3(t, height, t), material);
            a.Box(new int3(min.x + width - t, min.y, min.z + depth - t), new int3(t, height, t), material);
        }

        private static void AuthorFacadeDetails(
            IStructureAuthoringSession a,
            int3 min,
            int width,
            int wallHeight,
            int rows,
            int columns,
            TownArchitectureProgram program,
            in TownArchitectureVoxelPalette p)
        {
            int safeRows = Math.Max(1, Math.Min(2, rows));
            int safeColumns = Math.Max(1, Math.Min(4, columns));
            int doorReserve = 10;
            int available = Math.Max(12, width - doorReserve - 12);
            int spacing = Math.Max(8, available / safeColumns);
            int left = min.x + 5;

            for (int row = 0; row < safeRows; row++)
            {
                int y = min.y + 7 + row * Math.Max(11, (wallHeight - 10) / safeRows);
                for (int col = 0; col < safeColumns; col++)
                {
                    int x = left + col * spacing;
                    if (x + 8 >= min.x + width / 2 - 3 && x <= min.x + width / 2 + 7)
                        x += 10;
                    if (x + 8 >= min.x + width - 4) continue;
                    AuthorOpening(a, new int3(x, y, min.z), program.OpeningStyle, program, in p);
                }
            }

            AuthorDoor(a, min, width, program, in p);
        }

        private static void AuthorOpening(
            IStructureAuthoringSession a,
            int3 origin,
            TownArchitectureOpeningStyle style,
            TownArchitectureProgram program,
            in TownArchitectureVoxelPalette p)
        {
            int u = U(program);
            int w = 6 * u;
            int h = 8 * u;
            int recess = 2 * u;
            bool mullion = true;
            bool shutters = false;
            bool pointed = false;

            switch (style)
            {
                case TownArchitectureOpeningStyle.OrderedStone:
                    h = 10 * u;
                    break;
                case TownArchitectureOpeningStyle.DeepWeatheredStone:
                    w = 5 * u;
                    h = 7 * u;
                    recess = 3 * u;
                    shutters = true;
                    break;
                case TownArchitectureOpeningStyle.FortifiedReveal:
                    w = 4 * u;
                    h = 11 * u;
                    recess = 3 * u;
                    mullion = false;
                    break;
                case TownArchitectureOpeningStyle.OrganicPointed:
                    w = 7 * u;
                    h = 9 * u;
                    pointed = true;
                    mullion = false;
                    break;
                case TownArchitectureOpeningStyle.HeavySlit:
                    w = 3 * u;
                    h = 10 * u;
                    recess = 3 * u;
                    mullion = false;
                    break;
            }

            a.Carve(new int3(origin.x, origin.y, origin.z - u), new int3(w, h, recess + 2 * u));
            if (pointed)
                a.Carve(new int3(origin.x + u, origin.y + h, origin.z - u), new int3(Math.Max(u, w - 2 * u), 2 * u, recess + 2 * u));

            a.Box(new int3(origin.x, origin.y, origin.z + recess), new int3(w, h, u), p.Accent);
            AuthorOpeningFrame(a, origin, w, h, program, p.Trim, mullion, shutters);

            if (pointed)
            {
                a.Box(new int3(origin.x + u, origin.y + h, origin.z - 2 * u), new int3(u, 2 * u, 2 * u), p.Trim);
                a.Box(new int3(origin.x + w - 2 * u, origin.y + h, origin.z - 2 * u), new int3(u, 2 * u, 2 * u), p.Trim);
                a.Box(new int3(origin.x + w / 2 - u / 2, origin.y + h + u, origin.z - 2 * u), new int3(u, 2 * u, 2 * u), p.Accent);
            }
        }

        private static void AuthorOpeningFrame(
            IStructureAuthoringSession a,
            int3 origin,
            int width,
            int height,
            TownArchitectureProgram program,
            byte trim,
            bool mullion,
            bool shutters)
        {
            int u = U(program);
            int z = origin.z - 2 * u;
            a.Box(new int3(origin.x - u, origin.y - u, z), new int3(width + 2 * u, u, 2 * u), trim); // sill
            a.Box(new int3(origin.x - u, origin.y + height, z), new int3(width + 2 * u, u, 2 * u), trim); // lintel
            a.Box(new int3(origin.x - u, origin.y, z), new int3(u, height, 2 * u), trim);
            a.Box(new int3(origin.x + width, origin.y, z), new int3(u, height, 2 * u), trim);
            if (mullion && width >= 4 * u)
                a.Box(new int3(origin.x + width / 2, origin.y + u, z - u), new int3(u, height - 2 * u, u), trim);
            if (shutters)
            {
                a.Box(new int3(origin.x - 3 * u, origin.y, z), new int3(2 * u, height, u), trim);
                a.Box(new int3(origin.x + width + u, origin.y, z), new int3(2 * u, height, u), trim);
            }
        }

        private static void AuthorDoor(IStructureAuthoringSession a, int3 min, int width, TownArchitectureProgram program, in TownArchitectureVoxelPalette p)
        {
            int u = U(program);
            int doorWidth = program.OpeningStyle == TownArchitectureOpeningStyle.HeavySlit ? 10 * u : 8 * u;
            int doorHeight = program.OpeningStyle == TownArchitectureOpeningStyle.FortifiedReveal ? 18 * u : 16 * u;
            int x = min.x + width / 2 - doorWidth / 2;
            a.Carve(new int3(x, min.y + u, min.z - u), new int3(doorWidth, doorHeight, 4 * u));
            a.Box(new int3(x - u, min.y, min.z - 2 * u), new int3(u, doorHeight + 2 * u, 2 * u), p.Trim);
            a.Box(new int3(x + doorWidth, min.y, min.z - 2 * u), new int3(u, doorHeight + 2 * u, 2 * u), p.Trim);
            a.Box(new int3(x - u, min.y + doorHeight + u, min.z - 2 * u), new int3(doorWidth + 2 * u, u, 2 * u), p.Trim);
            a.Box(new int3(x - u, min.y, min.z - 3 * u), new int3(doorWidth + 2 * u, u, 4 * u), p.Structure); // threshold
            a.Box(new int3(x + doorWidth - 2 * u, min.y + doorHeight / 2, min.z - 3 * u), new int3(u, u, u), p.Accent); // hardware

            if (program.OpeningStyle == TownArchitectureOpeningStyle.TimberFramed)
            {
                a.Box(new int3(x - 4 * u, min.y + doorHeight + 2 * u, min.z - 6 * u), new int3(doorWidth + 8 * u, 2 * u, 6 * u), p.Roof);
                a.Box(new int3(x - 3 * u, min.y, min.z - 6 * u), new int3(2 * u, doorHeight, 2 * u), p.Structure);
                a.Box(new int3(x + doorWidth + u, min.y, min.z - 6 * u), new int3(2 * u, doorHeight, 2 * u), p.Structure);
            }
            else
            {
                for (int step = 0; step < 3; step++)
                    a.Box(new int3(x - (3 - step) * u, min.y - step * u, min.z - (7 - step * 2) * u), new int3(doorWidth + (6 - step * 2) * u, u, 3 * u), p.Ground);
            }
        }

        private static void AuthorGableFinish(IStructureAuthoringSession a, int3 min, int width, int depth, int wallHeight, int roofHeight, TownArchitectureProgram program, in TownArchitectureVoxelPalette p)
        {
            int u = U(program);
            a.Box(new int3(min.x - 3 * u, min.y + wallHeight - u, min.z - 4 * u), new int3(width + 6 * u, 2 * u, 2 * u), p.Trim); // fascia
            a.Box(new int3(min.x - 3 * u, min.y + wallHeight - u, min.z + depth + 2 * u), new int3(width + 6 * u, 2 * u, 2 * u), p.Trim);
            a.Box(new int3(min.x + width / 2 - u, min.y + wallHeight + roofHeight - 2 * u, min.z - 3 * u), new int3(2 * u, 2 * u, depth + 6 * u), p.Trim); // ridge cap
        }

        private static void AuthorMasonryCourses(IStructureAuthoringSession a, int3 min, int width, int wallHeight, TownArchitectureProgram program, byte trim)
        {
            int u = U(program);
            for (int y = 5 * u; y < wallHeight - 3 * u; y += 7 * u)
                a.Box(new int3(min.x + 2 * u, min.y + y, min.z - u), new int3(width - 4 * u, u, u), trim);
        }

        private static void AuthorQuoins(IStructureAuthoringSession a, int3 min, int width, int wallHeight, TownArchitectureProgram program, byte trim)
        {
            int u = U(program);
            for (int y = 0; y < wallHeight; y += 4 * u)
            {
                a.Box(new int3(min.x - u, min.y + y, min.z - u), new int3(3 * u, 2 * u, 2 * u), trim);
                a.Box(new int3(min.x + width - 2 * u, min.y + y + 2 * u, min.z - u), new int3(3 * u, 2 * u, 2 * u), trim);
            }
        }

        private static void AuthorTimberBracing(IStructureAuthoringSession a, int3 min, int width, int wallHeight, TownArchitectureProgram program, byte structure)
        {
            int u = U(program);
            a.Box(new int3(min.x + 2 * u, min.y + wallHeight / 2, min.z - 2 * u), new int3(width - 4 * u, 2 * u, 2 * u), structure);
            for (int x = min.x + 6 * u; x < min.x + width - 4 * u; x += 10 * u)
            {
                a.Box(new int3(x, min.y + 2 * u, min.z - 2 * u), new int3(2 * u, wallHeight - 4 * u, 2 * u), structure);
                for (int s = 0; s < 4; s++)
                    a.Box(new int3(x + s * u, min.y + 3 * u + s * 2 * u, min.z - 3 * u), new int3(2 * u, 2 * u, u), structure);
            }
        }

        private static void AuthorChimney(IStructureAuthoringSession a, int3 origin, int wallHeight, int roofHeight, TownArchitectureProgram program, in TownArchitectureVoxelPalette p, int side)
        {
            int u = U(program);
            int x = origin.x + side * 9;
            int y = origin.y + wallHeight - 4 * u;
            a.Box(new int3(x - 3 * u, y, origin.z + 5 * u), new int3(6 * u, roofHeight + 12 * u, 6 * u), p.Wall);
            a.Box(new int3(x - 4 * u, y + roofHeight + 11 * u, origin.z + 4 * u), new int3(8 * u, 2 * u, 8 * u), p.Trim);
        }

        private static void AuthorLeanTo(IStructureAuthoringSession a, int3 origin, int width, int depth, int height, TownArchitectureProgram program, in TownArchitectureVoxelPalette p)
        {
            int u = U(program);
            int3 min = new int3(origin.x, origin.y, origin.z - depth / 2);
            a.HollowBox(min, new int3(width, height, depth), 2 * u, p.Wall, true, true);
            for (int i = 0; i < 6; i++)
                a.Box(new int3(min.x - u, min.y + height + i * u, min.z - u + i * u), new int3(width + 2 * u, u, depth + 2 * u - i * 2 * u), p.Roof);
        }

        private static void AuthorLayeredCoping(IStructureAuthoringSession a, int3 min, int width, int depth, int wallHeight, TownArchitectureProgram program, byte trim)
        {
            int u = U(program);
            a.Box(new int3(min.x - 2 * u, min.y + wallHeight, min.z - 2 * u), new int3(width + 4 * u, u, depth + 4 * u), trim);
            a.Carve(new int3(min.x + 2 * u, min.y + wallHeight, min.z + 2 * u), new int3(width - 4 * u, 2 * u, depth - 4 * u));
            a.Box(new int3(min.x - u, min.y + wallHeight + u, min.z - u), new int3(width + 2 * u, u, u), trim);
            a.Box(new int3(min.x - u, min.y + wallHeight + u, min.z + depth), new int3(width + 2 * u, u, u), trim);
        }

        private static void AuthorButtress(IStructureAuthoringSession a, int3 basePos, int height, TownArchitectureProgram program, byte wall, byte trim)
        {
            int u = U(program);
            a.Box(basePos, new int3(5 * u, height, 6 * u), wall);
            a.Box(basePos + new int3(-u, height - 2 * u, -u), new int3(7 * u, 2 * u, 8 * u), trim);
        }

        private static void AuthorArrowSlit(IStructureAuthoringSession a, int3 pos, TownArchitectureProgram program, in TownArchitectureVoxelPalette p)
        {
            int u = U(program);
            a.Carve(new int3(pos.x, pos.y, pos.z - u), new int3(2 * u, 10 * u, 5 * u));
            a.Box(new int3(pos.x, pos.y, pos.z + 3 * u), new int3(2 * u, 10 * u, u), p.Accent);
            a.Box(new int3(pos.x - 2 * u, pos.y - u, pos.z - 2 * u), new int3(6 * u, u, 2 * u), p.Trim);
            a.Box(new int3(pos.x - 2 * u, pos.y + 10 * u, pos.z - 2 * u), new int3(6 * u, u, 2 * u), p.Trim);
        }

        private static void AuthorKentridge(IStructureAuthoringSession a, int3 residence, int3 commerce, int3 civic, int3 landmark, TownArchitectureProgram program, in TownArchitectureVoxelPalette p)
        {
            int3 r = GabledShell(a, residence, 36, 30, 24, 15, program, in p, 1, 2, true);
            AuthorTimberBracing(a, r, 36, 24, program, p.Structure);
            AuthorChimney(a, residence, 24, 15, program, in p, (program.Seed & 1u) == 0 ? -1 : 1);

            int3 shop = GabledShell(a, commerce, 42, 32, 24, 13, program, in p, 1, 3, true);
            AuthorTimberBracing(a, shop, 42, 24, program, p.Structure);
            a.Box(commerce + new int3(-17, 15, -20), new int3(34, 2, 9), p.Roof);
            a.Box(commerce + new int3(-15, 0, -20), new int3(2, 17, 2), p.Structure);
            a.Box(commerce + new int3(13, 0, -20), new int3(2, 17, 2), p.Structure);

            int3 church = GabledShell(a, civic, 42, 34, 30, 17, program, in p, 2, 2, true);
            AuthorTimberBracing(a, church, 42, 30, program, p.Structure);
            a.HollowBox(civic + new int3(-7, 0, 10), new int3(14, 42, 14), 2, p.Wall, true, true);
            a.Gable(civic + new int3(-9, 42, 8), new int3(18, 12, 18), true, p.Roof);

            Foundation(a, landmark, 40, 36, p.Ground);
            a.Cylinder(landmark.x, landmark.y, landmark.z, 10, 7, p.Wall, 6);
            a.Carve(new int3(landmark.x - 6, landmark.y + 3, landmark.z - 6), new int3(12, 8, 12));
            for (int dx = -12; dx <= 12; dx += 24)
            for (int dz = -9; dz <= 9; dz += 18)
                a.Box(new int3(landmark.x + dx - 2, landmark.y, landmark.z + dz - 2), new int3(4, 27, 4), p.Structure);
            a.Gable(new int3(landmark.x - 16, landmark.y + 24, landmark.z - 13), new int3(32, 11, 26), true, p.Roof);
            a.Box(new int3(landmark.x - 17, landmark.y + 23, landmark.z - 15), new int3(34, 2, 2), p.Trim);
        }

        private static void AuthorHightown(IStructureAuthoringSession a, int3 residence, int3 commerce, int3 civic, int3 landmark, TownArchitectureProgram program, in TownArchitectureVoxelPalette p)
        {
            int3 left = GabledShell(a, residence + new int3(-8, 0, 0), 20, 28, 38, 15, program, in p, 2, 1);
            int3 right = GabledShell(a, residence + new int3(10, 0, 2), 20, 26, 34, 13, program, in p, 2, 1);
            AuthorQuoins(a, left, 20, 38, program, p.Trim);
            AuthorQuoins(a, right, 20, 34, program, p.Trim);

            int3 shop = GabledShell(a, commerce, 40, 30, 34, 15, program, in p, 2, 3);
            AuthorMasonryCourses(a, shop, 40, 34, program, p.Trim);
            a.Box(commerce + new int3(-18, 23, -18), new int3(36, 3, 4), p.Trim);
            for (int x = -14; x <= 14; x += 7)
                a.Box(commerce + new int3(x, 25, -20), new int3(1, 7, 1), p.Structure);

            int3 hall = GabledShell(a, civic, 48, 36, 43, 19, program, in p, 2, 3);
            AuthorQuoins(a, hall, 48, 43, program, p.Trim);
            AuthorMasonryCourses(a, hall, 48, 43, program, p.Trim);
            for (int x = -18; x <= 18; x += 12)
                a.Box(civic + new int3(x - 2, 0, -22), new int3(4, 32, 4), p.Structure);

            Foundation(a, landmark, 42, 42, p.Ground);
            a.HollowBox(landmark + new int3(-12, 0, -12), new int3(24, 54, 24), 3, p.Wall, true, true);
            for (int y = 8; y <= 38; y += 15)
                AuthorOpening(a, landmark + new int3(-3, y, -12), TownArchitectureOpeningStyle.OrderedStone, program, in p);
            a.Cone(landmark.x, landmark.y + 54, landmark.z, 17, 20, p.Roof);
            a.Arch(landmark + new int3(-18, 0, 13), 36, 22, 8, 0, p.Structure);
            for (int s = 0; s < 5; s++)
                a.Box(landmark + new int3(-18 + s * 2, s, 18 + s * 2), new int3(36 - s * 4, 1, 5), p.Ground);
        }

        private static void AuthorMoordell(IStructureAuthoringSession a, int3 residence, int3 commerce, int3 civic, int3 landmark, TownArchitectureProgram program, in TownArchitectureVoxelPalette p)
        {
            int3 r = GabledShell(a, residence, 40, 32, 18, 10, program, in p, 1, 2, true);
            AuthorMasonryCourses(a, r, 40, 18, program, p.Trim);
            AuthorLeanTo(a, residence + new int3(18, 0, 5), 16, 24, 12, program, in p);
            AuthorChimney(a, residence, 18, 10, program, in p, -1);

            int3 shop = GabledShell(a, commerce, 46, 34, 22, 11, program, in p, 1, 3, true);
            AuthorMasonryCourses(a, shop, 46, 22, program, p.Trim);
            AuthorLeanTo(a, commerce + new int3(-25, 0, 5), 15, 25, 13, program, in p);

            int3 hall = GabledShell(a, civic, 48, 36, 23, 12, program, in p, 1, 3, true);
            AuthorQuoins(a, hall, 48, 23, program, p.Trim);
            a.Box(civic + new int3(-24, 0, -20), new int3(48, 4, 4), p.Trim);

            Foundation(a, landmark, 58, 44, p.Ground);
            a.Box(landmark + new int3(-27, 0, -21), new int3(54, 5, 3), p.Wall);
            a.Box(landmark + new int3(-27, 0, 18), new int3(54, 5, 3), p.Wall);
            for (int x = -18; x <= 18; x += 12)
            {
                a.Box(landmark + new int3(x - 2, 1, -7), new int3(4, 13, 4), p.Wall);
                a.Box(landmark + new int3(x - 6, 9, -7), new int3(12, 2, 4), p.Trim);
            }
            a.Cone(landmark.x + 16, landmark.y, landmark.z + 10, 8, 15, p.Trim);
            for (int z = -18; z <= 18; z += 6)
                a.Carve(landmark + new int3(-30, -1, z), new int3(60, 2, 2)); // drainage/ditch articulation
        }

        private static void AuthorRossdam(IStructureAuthoringSession a, int3 residence, int3 commerce, int3 civic, int3 landmark, TownArchitectureProgram program, in TownArchitectureVoxelPalette p)
        {
            int3 r = FlatParapetShell(a, residence, 34, 28, 34, program, in p, 2, 2);
            AuthorQuoins(a, r, 34, 34, program, p.Trim);
            AuthorMasonryCourses(a, r, 34, 34, program, p.Trim);

            int3 shop = GabledShell(a, commerce, 42, 30, 30, 13, program, in p, 2, 3);
            AuthorQuoins(a, shop, 42, 30, program, p.Trim);
            a.Box(commerce + new int3(-19, 22, -18), new int3(38, 3, 5), p.Accent);

            int3 hall = FlatParapetShell(a, civic, 54, 40, 39, program, in p, 2, 3);
            AuthorQuoins(a, hall, 54, 39, program, p.Trim);
            for (int x = -24; x <= 20; x += 11)
                AuthorButtress(a, civic + new int3(x, 0, -24), 30, program, p.Wall, p.Trim);
            a.Crenellate(civic + new int3(-27, 41, -20), new int3(6, 0, 0), 9, 4, 5, 3, 2, p.Trim);

            AuthorRossdamGatehouse(a, landmark, program, in p);
        }

        private static void AuthorRossdamGatehouse(IStructureAuthoringSession a, int3 landmark, TownArchitectureProgram program, in TownArchitectureVoxelPalette p)
        {
            int u = U(program);
            Foundation(a, landmark, 68, 48, p.Ground);
            int leftX = landmark.x - 20;
            int rightX = landmark.x + 20;
            int3 leftMin = new int3(leftX - 10, landmark.y, landmark.z - 11);
            int3 rightMin = new int3(rightX - 10, landmark.y, landmark.z - 11);
            a.HollowBox(leftMin, new int3(20, 38, 22), 3, p.Wall, true, true);
            a.HollowBox(rightMin, new int3(20, 38, 22), 3, p.Wall, true, true);
            AuthorLayeredCoping(a, leftMin, 20, 22, 38, program, p.Trim);
            AuthorLayeredCoping(a, rightMin, 20, 22, 38, program, p.Trim);
            a.Crenellate(new int3(leftX - 10, landmark.y + 40, landmark.z - 11), new int3(5, 0, 0), 4, 4, 6, 3, 2, p.Trim);
            a.Crenellate(new int3(rightX - 10, landmark.y + 40, landmark.z - 11), new int3(5, 0, 0), 4, 4, 6, 3, 2, p.Trim);

            // Tower-wall transitions and dimensional gate frame.
            a.Box(new int3(leftX + 10, landmark.y + 12, landmark.z - 8), new int3(20, 20, 16), p.Wall);
            a.Box(new int3(landmark.x - 10, landmark.y + 28, landmark.z - 10), new int3(20, 8, 20), p.Wall);
            a.Carve(new int3(landmark.x - 7, landmark.y + u, landmark.z - 9), new int3(14, 21, 18));
            a.Box(new int3(landmark.x - 10, landmark.y, landmark.z - 11), new int3(3, 25, 4), p.Trim);
            a.Box(new int3(landmark.x + 7, landmark.y, landmark.z - 11), new int3(3, 25, 4), p.Trim);
            a.Box(new int3(landmark.x - 10, landmark.y + 22, landmark.z - 11), new int3(20, 3, 4), p.Trim);
            a.Box(new int3(landmark.x - 7, landmark.y + 13, landmark.z - 12), new int3(14, 2, 2), p.Accent); // gate crossbar/hardware

            AuthorArrowSlit(a, new int3(leftX - u, landmark.y + 14, landmark.z - 11), program, in p);
            AuthorArrowSlit(a, new int3(rightX - u, landmark.y + 14, landmark.z - 11), program, in p);
            AuthorButtress(a, new int3(leftX - 13, landmark.y, landmark.z - 14), 28, program, p.Wall, p.Trim);
            AuthorButtress(a, new int3(rightX + 8, landmark.y, landmark.z - 14), 28, program, p.Wall, p.Trim);

            // Shared access stair reads at player scale and demonstrates believable wall access.
            for (int s = 0; s < 8; s++)
                a.Box(new int3(leftX - 8 + s, landmark.y + s, landmark.z + 12 + s * 2), new int3(10, u, 4), p.Ground);
        }

        private static void AuthorFairyVillage(IStructureAuthoringSession a, int3 residence, int3 commerce, int3 civic, int3 landmark, TownArchitectureProgram program, in TownArchitectureVoxelPalette p)
        {
            Foundation(a, residence, 44, 40, p.Ground);
            a.Cylinder(residence.x, residence.y, residence.z, 8, 34, p.Structure);
            for (int i = 0; i < 4; i++)
                a.Cylinder(residence.x + (i - 2) * 3, residence.y, residence.z + 5 + i * 2, 2, 16 + i * 3, p.Structure);
            a.Disc(residence.x, residence.y + 26, residence.z, 20, p.Structure);
            int3 roomMin = residence + new int3(-12, 27, -11);
            a.HollowBox(roomMin, new int3(24, 18, 22), 2, p.Wall, true, true);
            AuthorFacadeDetails(a, roomMin, 24, 18, 1, 2, program, in p);
            a.Cone(residence.x, residence.y + 45, residence.z, 18, 16, p.Roof);
            a.Disc(residence.x, residence.y + 45, residence.z, 19, p.Trim);

            Foundation(a, commerce, 46, 40, p.Ground);
            a.Cylinder(commerce.x, commerce.y, commerce.z, 7, 20, p.Structure);
            a.Disc(commerce.x, commerce.y + 17, commerce.z, 21, p.Roof);
            a.Cone(commerce.x, commerce.y + 17, commerce.z, 21, 14, p.Roof);
            a.Arch(commerce + new int3(-13, 2, -15), 26, 18, 7, 0, p.Trim);
            a.Box(commerce + new int3(-12, 10, -17), new int3(24, 2, 2), p.Accent);

            Foundation(a, civic, 54, 46, p.Ground);
            for (int x = -18; x <= 18; x += 12)
                a.Cylinder(civic.x + x, civic.y, civic.z, 3, 29 + Math.Abs(x) / 6, p.Structure);
            a.Disc(civic.x, civic.y + 26, civic.z, 26, p.Roof);
            a.Cone(civic.x, civic.y + 26, civic.z, 26, 14, p.Roof);
            a.Cylinder(civic.x, civic.y + 1, civic.z, 5, 11, p.Accent);
            for (int x = -18; x <= 18; x += 6)
                a.Box(civic + new int3(x, 29, -20), new int3(1, 7, 1), p.Trim);

            Foundation(a, landmark, 62, 46, p.Ground);
            a.Arch(landmark + new int3(-27, 0, -6), 54, 30, 10, 0, p.Structure);
            a.Box(landmark + new int3(-25, 19, -8), new int3(50, 3, 12), p.Ground);
            for (int x = -23; x <= 23; x += 8)
            {
                a.Box(landmark + new int3(x, 22, -10), new int3(1, 7, 1), p.Trim);
                a.Cone(landmark.x + x, landmark.y, landmark.z + 13, 4, 14 + Math.Abs(x) % 6, p.Accent);
            }
        }

        private static void AuthorOrcVillage(IStructureAuthoringSession a, int3 residence, int3 commerce, int3 civic, int3 landmark, TownArchitectureProgram program, in TownArchitectureVoxelPalette p)
        {
            int3 r = GabledShell(a, residence, 40, 32, 21, 11, program, in p, 1, 2, true);
            AuthorTimberBracing(a, r, 40, 21, program, p.Structure);
            AuthorSpikes(a, residence + new int3(-20, 22, -18), 8, 6, program, p.Trim);

            int3 shop = GabledShell(a, commerce, 44, 34, 24, 11, program, in p, 1, 2, true);
            AuthorTimberBracing(a, shop, 44, 24, program, p.Structure);
            a.Box(commerce + new int3(12, 20, 5), new int3(8, 24, 8), p.Wall);
            a.Cone(commerce.x + 16, commerce.y + 44, commerce.z + 9, 6, 8, p.Trim); // forge hood
            a.Box(commerce + new int3(-18, 13, -20), new int3(36, 3, 7), p.Trim);

            int3 hall = GabledShell(a, civic, 56, 38, 25, 14, program, in p, 1, 3, true);
            AuthorTimberBracing(a, hall, 56, 25, program, p.Structure);
            for (int x = -24; x <= 24; x += 12)
                a.Box(civic + new int3(x - 2, 0, -23), new int3(5, 32, 5), p.Structure);
            AuthorSpikes(a, civic + new int3(-25, 27, -24), 9, 6, program, p.Trim);

            Foundation(a, landmark, 70, 50, p.Ground);
            for (int x = -30; x <= 30; x += 6)
            {
                int height = 26 + Math.Abs(x) % 7;
                a.Box(landmark + new int3(x - 2, 0, 16), new int3(4, height, 4), p.Structure);
                a.Cone(landmark.x + x, landmark.y + height, landmark.z + 18, 3, 6, p.Trim);
            }
            for (int x = -20; x <= 20; x += 40)
            {
                int3 towerMin = landmark + new int3(x - 9, 0, -9);
                a.HollowBox(towerMin, new int3(18, 33, 18), 3, p.Wall, true, true);
                AuthorOpening(a, towerMin + new int3(8, 13, 0), TownArchitectureOpeningStyle.HeavySlit, program, in p);
                a.Gable(landmark + new int3(x - 11, 33, -11), new int3(22, 11, 22), true, p.Roof);
                AuthorSpikes(a, landmark + new int3(x - 10, 34, -12), 5, 5, program, p.Trim);
            }
            a.Box(landmark + new int3(-12, 17, -6), new int3(24, 8, 12), p.Trim);
            a.Carve(landmark + new int3(-7, 1, -9), new int3(14, 19, 18));
            a.Box(landmark + new int3(-8, 10, -11), new int3(16, 3, 3), p.Accent); // gate crossbar
        }

        private static void AuthorSpikes(IStructureAuthoringSession a, int3 start, int count, int spacing, TownArchitectureProgram program, byte material)
        {
            int u = U(program);
            for (int i = 0; i < count; i++)
            {
                int x = start.x + i * spacing;
                a.Box(new int3(x, start.y, start.z), new int3(2 * u, 5 * u, 2 * u), material);
                a.Cone(x + u, start.y + 5 * u, start.z + u, 2 * u, 5 * u, material);
            }
        }
    }
}
