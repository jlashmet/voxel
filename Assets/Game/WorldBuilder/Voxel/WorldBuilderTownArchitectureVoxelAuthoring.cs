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
    /// Shared terrain-aware realization for registered town programs. The backend dispatches only on reusable
    /// architectural capabilities (massing, opening and detail features), never on a town id or town name.
    /// </summary>
    public static class WorldBuilderTownArchitectureVoxelAuthoring
    {
        public const int DistrictHalfWidthVoxels = TownArchitectureDistrictBounds.HalfWidthVoxels;
        public const int DistrictHalfDepthVoxels = TownArchitectureDistrictBounds.HalfDepthVoxels;
        public const int EstimatedMaxHeightVoxels = TownArchitectureDistrictBounds.EstimatedMaxHeightVoxels;

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
            AuthorLabelPlinth(authoring, districtCentre, terrainHeightAt, in palette);

            int seedShift = (int)(program.Seed % 5u) - 2;
            int3 residence = Grounded(districtCentre + new int2(-47 + seedShift, -12), terrainHeightAt);
            int3 commerce = Grounded(districtCentre + new int2(40, -12 + seedShift), terrainHeightAt);
            int3 civic = Grounded(districtCentre + new int2(-47, 34 - seedShift), terrainHeightAt);
            int3 landmark = Grounded(districtCentre + new int2(40 - seedShift, 34), terrainHeightAt);

            for (int i = 0; i < program.Composition.Roles.Count; i++)
            {
                TownArchitectureRoleRecipe recipe = program.Composition.Roles[i];
                int3 origin = RoleOrigin(recipe.Role, residence, commerce, civic, landmark);
                AuthorRole(authoring, origin, recipe, program, in palette);
            }
        }

        private static int3 RoleOrigin(
            TownArchitectureStructureRole role,
            int3 residence,
            int3 commerce,
            int3 civic,
            int3 landmark)
        {
            switch (role)
            {
                case TownArchitectureStructureRole.Residential: return residence;
                case TownArchitectureStructureRole.Commercial: return commerce;
                case TownArchitectureStructureRole.CivicCommunal: return civic;
                case TownArchitectureStructureRole.LandmarkInfrastructure: return landmark;
                default: throw new ArgumentOutOfRangeException(nameof(role), role, "Unsupported town structure role.");
            }
        }

        private static int3 Grounded(int2 xz, Func<int, int, int> terrainHeightAt) =>
            new int3(xz.x, terrainHeightAt(xz.x, xz.y) + 1, xz.y);

        private static int U(TownArchitectureProgram program) => Math.Max(1, program.DetailUnitBlocks);

        private static bool Has(TownArchitectureDetailFeatures value, TownArchitectureDetailFeatures feature) =>
            (value & feature) != 0;

        private static void AuthorRole(
            IStructureAuthoringSession a,
            int3 origin,
            TownArchitectureRoleRecipe recipe,
            TownArchitectureProgram program,
            in TownArchitectureVoxelPalette p)
        {
            int3 min;
            switch (recipe.Massing)
            {
                case TownArchitectureMassing.GabledFrame:
                    min = GabledShell(a, origin, recipe, program, in p, 2);
                    break;
                case TownArchitectureMassing.StoneGabled:
                    min = GabledShell(a, origin, recipe, program, in p, 3);
                    break;
                case TownArchitectureMassing.LowStoneLeanTo:
                    min = GabledShell(a, origin, recipe, program, in p, 3);
                    AuthorLeanTo(a, min, recipe, program, in p);
                    break;
                case TownArchitectureMassing.FortifiedParapet:
                    min = ParapetShell(a, origin, recipe, program, in p);
                    break;
                case TownArchitectureMassing.OrganicCanopy:
                    min = OrganicShell(a, origin, recipe, program, in p);
                    break;
                case TownArchitectureMassing.HeavyStockade:
                    min = StockadeShell(a, origin, recipe, program, in p);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(recipe), recipe.Massing, "Unsupported reusable town massing capability.");
            }

            AuthorOpeningSet(a, min, recipe, program, in p);
            ApplyFeatures(a, origin, min, recipe, program, in p);
        }

        private static int3 GabledShell(
            IStructureAuthoringSession a,
            int3 origin,
            TownArchitectureRoleRecipe recipe,
            TownArchitectureProgram program,
            in TownArchitectureVoxelPalette p,
            int thickness)
        {
            Foundation(a, origin, recipe.Width + 6, recipe.Depth + 6, p.Ground);
            int3 min = new int3(origin.x - recipe.Width / 2, origin.y, origin.z - recipe.Depth / 2);
            a.HollowBox(min, new int3(recipe.Width, recipe.WallHeight, recipe.Depth), thickness, p.Wall, true, true);
            int roofHeight = Math.Max(8, recipe.RoofHeight);
            a.Gable(
                new int3(min.x - 3, min.y + recipe.WallHeight, min.z - 3),
                new int3(recipe.Width + 6, roofHeight, recipe.Depth + 6),
                true,
                p.Roof);
            return min;
        }

        private static int3 ParapetShell(
            IStructureAuthoringSession a,
            int3 origin,
            TownArchitectureRoleRecipe recipe,
            TownArchitectureProgram program,
            in TownArchitectureVoxelPalette p)
        {
            Foundation(a, origin, recipe.Width + 8, recipe.Depth + 8, p.Ground);
            int3 min = new int3(origin.x - recipe.Width / 2, origin.y, origin.z - recipe.Depth / 2);
            a.HollowBox(min, new int3(recipe.Width, recipe.WallHeight, recipe.Depth), 3, p.Wall, true, true);
            int u = U(program);
            a.Box(new int3(min.x - u, min.y + recipe.WallHeight, min.z - u),
                new int3(recipe.Width + 2 * u, 2 * u, recipe.Depth + 2 * u), p.Trim);
            return min;
        }

        private static int3 OrganicShell(
            IStructureAuthoringSession a,
            int3 origin,
            TownArchitectureRoleRecipe recipe,
            TownArchitectureProgram program,
            in TownArchitectureVoxelPalette p)
        {
            int u = U(program);
            Foundation(a, origin, recipe.Width + 4, recipe.Depth + 4, p.Ground);
            int trunkRadius = Math.Max(4, recipe.Width / 7);
            int trunkHeight = Math.Max(16, recipe.WallHeight);
            a.Cylinder(origin.x, origin.y, origin.z, trunkRadius, trunkHeight, p.Structure);
            int podY = origin.y + Math.Max(10, trunkHeight / 2);
            int3 min = new int3(origin.x - recipe.Width / 2, podY, origin.z - recipe.Depth / 2);
            a.HollowBox(min, new int3(recipe.Width, recipe.WallHeight, recipe.Depth), Math.Max(2, u), p.Wall, true, true);
            a.Disc(origin.x, podY + recipe.WallHeight, origin.z, Math.Max(recipe.Width, recipe.Depth) / 2 + 5, p.Roof);
            a.Disc(origin.x, podY + recipe.WallHeight + 3 * u, origin.z, Math.Max(recipe.Width, recipe.Depth) / 2, p.Roof);
            if (recipe.RoofHeight > 0)
                a.Cone(origin.x, podY + recipe.WallHeight, origin.z, Math.Max(6, recipe.Width / 3), recipe.RoofHeight, p.Accent);
            return min;
        }

        private static int3 StockadeShell(
            IStructureAuthoringSession a,
            int3 origin,
            TownArchitectureRoleRecipe recipe,
            TownArchitectureProgram program,
            in TownArchitectureVoxelPalette p)
        {
            int3 min = GabledShell(a, origin, recipe, program, in p, 3);
            int u = U(program);
            for (int x = min.x; x <= min.x + recipe.Width; x += Math.Max(4, 4 * u))
            {
                a.Cylinder(x, min.y, min.z - 2 * u, Math.Max(1, u), recipe.WallHeight + 5 * u, p.Structure);
                a.Cylinder(x, min.y, min.z + recipe.Depth + u, Math.Max(1, u), recipe.WallHeight + 5 * u, p.Structure);
            }
            return min;
        }

        private static void ApplyFeatures(
            IStructureAuthoringSession a,
            int3 origin,
            int3 min,
            TownArchitectureRoleRecipe recipe,
            TownArchitectureProgram program,
            in TownArchitectureVoxelPalette p)
        {
            TownArchitectureDetailFeatures f = recipe.Features;
            if (Has(f, TownArchitectureDetailFeatures.TimberFrame)) AuthorTimberFrame(a, min, recipe, program, p.Structure);
            if (Has(f, TownArchitectureDetailFeatures.MasonryCourses)) AuthorMasonryCourses(a, min, recipe, program, p.Trim);
            if (Has(f, TownArchitectureDetailFeatures.Balcony)) AuthorBalcony(a, min, recipe, program, in p);
            if (Has(f, TownArchitectureDetailFeatures.Awning)) AuthorAwning(a, min, recipe, program, in p);
            if (Has(f, TownArchitectureDetailFeatures.CivicArch)) AuthorCivicArch(a, min, recipe, program, in p);
            if (Has(f, TownArchitectureDetailFeatures.Chimney)) AuthorChimney(a, origin, recipe, program, in p);
            if (Has(f, TownArchitectureDetailFeatures.Buttress)) AuthorButtresses(a, min, recipe, program, p.Trim);
            if (Has(f, TownArchitectureDetailFeatures.Crenellation)) AuthorCrenellation(a, min, recipe, program, p.Trim);
            if (Has(f, TownArchitectureDetailFeatures.Canopy)) AuthorCanopyDetails(a, origin, min, recipe, program, in p);
            if (Has(f, TownArchitectureDetailFeatures.Stockade)) AuthorStockadeDetails(a, min, recipe, program, in p);
            if (Has(f, TownArchitectureDetailFeatures.Spikes)) AuthorSpikes(a, min, recipe, program, p.Accent);
            if (Has(f, TownArchitectureDetailFeatures.LeanTo) && recipe.Massing != TownArchitectureMassing.LowStoneLeanTo)
                AuthorLeanTo(a, min, recipe, program, in p);
        }

        private static void Foundation(IStructureAuthoringSession a, int3 origin, int width, int depth, byte material)
        {
            a.Box(new int3(origin.x - width / 2, origin.y - 5, origin.z - depth / 2), new int3(width, 6, depth), material);
        }

        private static void AuthorOpeningSet(
            IStructureAuthoringSession a,
            int3 min,
            TownArchitectureRoleRecipe recipe,
            TownArchitectureProgram program,
            in TownArchitectureVoxelPalette p)
        {
            int columns = recipe.Width >= 42 ? 3 : 2;
            int spacing = recipe.Width / (columns + 1);
            for (int col = 1; col <= columns; col++)
                AuthorOpening(a, new int3(min.x + spacing * col - 3, min.y + 8, min.z), recipe.OpeningStyle, program, in p);

            int doorWidth = Math.Max(5, 5 * U(program));
            int doorHeight = Math.Max(10, 10 * U(program));
            int doorX = min.x + recipe.Width / 2 - doorWidth / 2;
            a.Carve(new int3(doorX, min.y + 1, min.z - 1), new int3(doorWidth, doorHeight, 4));
            a.Box(new int3(doorX - 2, min.y, min.z - 2), new int3(2, doorHeight + 3, 3), p.Trim);
            a.Box(new int3(doorX + doorWidth, min.y, min.z - 2), new int3(2, doorHeight + 3, 3), p.Trim);
            a.Box(new int3(doorX - 2, min.y + doorHeight + 1, min.z - 2), new int3(doorWidth + 4, 2, 3), p.Trim);
        }

        private static void AuthorOpening(
            IStructureAuthoringSession a,
            int3 origin,
            TownArchitectureOpeningStyle style,
            TownArchitectureProgram program,
            in TownArchitectureVoxelPalette p)
        {
            int u = U(program);
            int width = style == TownArchitectureOpeningStyle.HeavySlit ? 3 * u : 6 * u;
            int height = style == TownArchitectureOpeningStyle.HeavySlit ? 10 * u : 8 * u;
            int depth = style == TownArchitectureOpeningStyle.DeepWeatheredStone ||
                        style == TownArchitectureOpeningStyle.FortifiedReveal ? 5 : 3;
            a.Carve(new int3(origin.x, origin.y, origin.z - 1), new int3(width, height, depth));
            a.Box(new int3(origin.x - u, origin.y - u, origin.z - 2), new int3(width + 2 * u, u, 3), p.Trim);
            a.Box(new int3(origin.x - u, origin.y + height, origin.z - 2), new int3(width + 2 * u, u, 3), p.Trim);
            a.Box(new int3(origin.x - u, origin.y, origin.z - 2), new int3(u, height, 3), p.Structure);
            a.Box(new int3(origin.x + width, origin.y, origin.z - 2), new int3(u, height, 3), p.Structure);
            if (style == TownArchitectureOpeningStyle.OrderedStone || style == TownArchitectureOpeningStyle.OrganicPointed)
                a.Box(new int3(origin.x + width / 2, origin.y, origin.z - 3), new int3(u, height, 2), p.Accent);
        }

        private static void AuthorTimberFrame(IStructureAuthoringSession a, int3 min, TownArchitectureRoleRecipe r, TownArchitectureProgram program, byte material)
        {
            int u = Math.Max(1, 2 * U(program));
            a.Box(new int3(min.x, min.y, min.z - u), new int3(u, r.WallHeight, u), material);
            a.Box(new int3(min.x + r.Width - u, min.y, min.z - u), new int3(u, r.WallHeight, u), material);
            a.Box(new int3(min.x, min.y + r.WallHeight / 2, min.z - u), new int3(r.Width, u, u), material);
            a.Box(new int3(min.x, min.y + r.WallHeight - u, min.z - u), new int3(r.Width, u, u), material);
        }

        private static void AuthorMasonryCourses(IStructureAuthoringSession a, int3 min, TownArchitectureRoleRecipe r, TownArchitectureProgram program, byte material)
        {
            int u = U(program);
            for (int y = min.y + 5 * u; y < min.y + r.WallHeight; y += Math.Max(6, 6 * u))
                a.Box(new int3(min.x - u, y, min.z - u), new int3(r.Width + 2 * u, u, u), material);
        }

        private static void AuthorBalcony(IStructureAuthoringSession a, int3 min, TownArchitectureRoleRecipe r, TownArchitectureProgram program, in TownArchitectureVoxelPalette p)
        {
            int u = U(program);
            int width = Math.Max(14, r.Width / 2);
            int x = min.x + r.Width / 2 - width / 2;
            int y = min.y + Math.Max(11, r.WallHeight / 2);
            a.Box(new int3(x, y, min.z - 6 * u), new int3(width, 2 * u, 7 * u), p.Structure);
            for (int bx = x; bx <= x + width; bx += Math.Max(5, 5 * u))
                a.Box(new int3(bx, y + 2 * u, min.z - 6 * u), new int3(u, 6 * u, u), p.Trim);
            a.Box(new int3(x, y + 7 * u, min.z - 6 * u), new int3(width, u, u), p.Trim);
        }

        private static void AuthorAwning(IStructureAuthoringSession a, int3 min, TownArchitectureRoleRecipe r, TownArchitectureProgram program, in TownArchitectureVoxelPalette p)
        {
            int u = U(program);
            int width = Math.Max(14, r.Width / 2);
            int x = min.x + r.Width / 2 - width / 2;
            int y = min.y + Math.Max(10, r.WallHeight / 2);
            a.Box(new int3(x, y, min.z - 7 * u), new int3(width, 2 * u, 8 * u), p.Roof);
            a.Box(new int3(x, y - 7 * u, min.z - 7 * u), new int3(2 * u, 8 * u, 2 * u), p.Structure);
            a.Box(new int3(x + width - 2 * u, y - 7 * u, min.z - 7 * u), new int3(2 * u, 8 * u, 2 * u), p.Structure);
        }

        private static void AuthorCivicArch(IStructureAuthoringSession a, int3 min, TownArchitectureRoleRecipe r, TownArchitectureProgram program, in TownArchitectureVoxelPalette p)
        {
            int width = Math.Max(12, Math.Min(20, r.Width / 2));
            int height = Math.Max(16, Math.Min(28, r.WallHeight));
            int x = min.x + r.Width / 2 - width / 2;
            a.Arch(new int3(x, min.y + 1, min.z - 5), width, height, 5, 2, p.Trim);
        }

        private static void AuthorChimney(IStructureAuthoringSession a, int3 origin, TownArchitectureRoleRecipe r, TownArchitectureProgram program, in TownArchitectureVoxelPalette p)
        {
            int u = U(program);
            int side = (program.Seed & 1u) == 0u ? -1 : 1;
            int x = origin.x + side * Math.Max(6, r.Width / 4);
            int z = origin.z + Math.Max(3, r.Depth / 5);
            int baseY = origin.y + r.WallHeight - 3 * u;
            a.Cylinder(x, baseY, z, Math.Max(2, 2 * u), Math.Max(12, r.RoofHeight + 8), p.Trim);
            a.Box(new int3(x - 3 * u, baseY + Math.Max(12, r.RoofHeight + 8), z - 3 * u), new int3(6 * u, 2 * u, 6 * u), p.Accent);
        }

        private static void AuthorButtresses(IStructureAuthoringSession a, int3 min, TownArchitectureRoleRecipe r, TownArchitectureProgram program, byte material)
        {
            int u = U(program);
            int w = Math.Max(3, 3 * u);
            int h = Math.Max(12, r.WallHeight - 4);
            a.Box(new int3(min.x - w, min.y, min.z - w), new int3(w, h, 2 * w), material);
            a.Box(new int3(min.x + r.Width, min.y, min.z - w), new int3(w, h, 2 * w), material);
        }

        private static void AuthorCrenellation(IStructureAuthoringSession a, int3 min, TownArchitectureRoleRecipe r, TownArchitectureProgram program, byte material)
        {
            int u = U(program);
            int width = Math.Max(3, 3 * u);
            int gap = Math.Max(3, 3 * u);
            int count = Math.Max(3, r.Width / (width + gap));
            a.Crenellate(new int3(min.x, min.y + r.WallHeight + 2 * u, min.z - u), new int3(width + gap, 0, 0), count, width, 4 * u, width, gap, material);
        }

        private static void AuthorCanopyDetails(IStructureAuthoringSession a, int3 origin, int3 min, TownArchitectureRoleRecipe r, TownArchitectureProgram program, in TownArchitectureVoxelPalette p)
        {
            int u = U(program);
            int y = min.y + r.WallHeight + 6 * u;
            a.Disc(origin.x - 5 * u, y, origin.z, Math.Max(8, r.Width / 3), p.Roof);
            a.Disc(origin.x + 5 * u, y + 2 * u, origin.z + 3 * u, Math.Max(7, r.Depth / 3), p.Accent);
        }

        private static void AuthorStockadeDetails(IStructureAuthoringSession a, int3 min, TownArchitectureRoleRecipe r, TownArchitectureProgram program, in TownArchitectureVoxelPalette p)
        {
            int u = U(program);
            for (int x = min.x - 4 * u; x <= min.x + r.Width + 4 * u; x += Math.Max(6, 6 * u))
                a.Cylinder(x, min.y, min.z - 7 * u, Math.Max(1, u), Math.Max(12, r.WallHeight / 2), p.Structure);
        }

        private static void AuthorSpikes(IStructureAuthoringSession a, int3 min, TownArchitectureRoleRecipe r, TownArchitectureProgram program, byte material)
        {
            int u = U(program);
            int y = min.y + r.WallHeight;
            for (int x = min.x + 3 * u; x < min.x + r.Width; x += Math.Max(8, 8 * u))
                a.Cone(x, y, min.z - 2 * u, Math.Max(2, 2 * u), Math.Max(7, 7 * u), material);
        }

        private static void AuthorLeanTo(IStructureAuthoringSession a, int3 min, TownArchitectureRoleRecipe r, TownArchitectureProgram program, in TownArchitectureVoxelPalette p)
        {
            int u = U(program);
            int width = Math.Max(14, r.Width / 2);
            int depth = Math.Max(14, r.Depth / 2);
            int wall = Math.Max(10, r.WallHeight / 2);
            int3 leanMin = new int3(min.x + r.Width - 2 * u, min.y, min.z + r.Depth / 4);
            a.HollowBox(leanMin, new int3(width, wall, depth), Math.Max(2, 2 * u), p.Wall, true, true);
            a.Gable(new int3(leanMin.x - 2 * u, leanMin.y + wall, leanMin.z - 2 * u), new int3(width + 4 * u, Math.Max(7, r.RoofHeight / 2), depth + 4 * u), false, p.Roof);
        }

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

        private static void AuthorLabelPlinth(IStructureAuthoringSession a, int2 c, Func<int, int, int> height, in TownArchitectureVoxelPalette p)
        {
            int z = c.y - DistrictHalfDepthVoxels;
            int y = height(c.x, z) + 1;
            a.Box(new int3(c.x - 30, y, z), new int3(60, 5, 3), p.Trim);
            a.Box(new int3(c.x - 26, y + 5, z - 1), new int3(52, 9, 2), p.Wall);
            a.Box(new int3(c.x - 22, y + 8, z - 3), new int3(44, 3, 2), p.Accent);
        }
    }
}
