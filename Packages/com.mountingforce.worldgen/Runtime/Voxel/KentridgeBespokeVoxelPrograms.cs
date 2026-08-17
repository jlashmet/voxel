using System;
using MountingForce.WorldGen.Architecture;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Semantic voxel programs for Kentridge's deliberately bespoke landmark archetypes.
    ///
    /// These preserve the authored silhouettes from the original Kentridge catalogue while replacing
    /// anonymous box/carve bytecode with architecture roles. The selected city style therefore owns
    /// corner radii and reconstruction for the inn, warehouse, mansion, church and well exactly as it
    /// does for generated houses and anonymous frontage. Shared glazed-opening and timber-frame
    /// construction comes from ArchitectureVoxelPatterns rather than Kentridge-local implementations.
    /// </summary>
    internal static class KentridgeBespokeVoxelPrograms
    {
        internal readonly struct Program
        {
            public readonly int[] Code;
            public readonly int3 Door;

            public Program(int[] code, int3 door)
            {
                Code = code;
                Door = door;
            }
        }

        public static Program Build(
            StructureArchetype archetype,
            ArchitectureTheme theme,
            VoxelWorldGenSettings settings,
            StructureGeometryProfile geometry)
        {
            switch (archetype)
            {
                case StructureArchetype.Inn:
                    return InnProgram(theme, settings, geometry);
                case StructureArchetype.Warehouse:
                    return WarehouseProgram(theme, settings, geometry);
                case StructureArchetype.Mansion:
                    return MansionProgram(theme, settings, geometry);
                case StructureArchetype.Church:
                    return ChurchProgram(theme, settings, geometry);
                case StructureArchetype.Well:
                    return WellProgram(theme, settings, geometry);
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(archetype), archetype,
                        "The archetype is not a bespoke Kentridge landmark.");
            }
        }

        private static Program InnProgram(
            ArchitectureTheme theme,
            VoxelWorldGenSettings settings,
            StructureGeometryProfile geometry)
        {
            int s = settings.VoxelsPerDecimetre;
            var b = new ProgramBuilder(geometry, s);
            byte foundation = settings.Materials.Resolve(theme.Foundation);
            byte wall = settings.Materials.Resolve(theme.Wall);
            byte timber = settings.Materials.Resolve(theme.Frame);
            byte glass = settings.Materials.Resolve(theme.Window);
            byte roof = settings.Materials.Resolve(theme.Roof);

            int x0 = 14 * s, z0 = 14 * s, w = 138 * s, d = 126 * s;
            int f = theme.FoundationHeightDm * s;
            int t = theme.WallThicknessDm * s;
            int wallH = theme.FloorHeightDm * 2 * s;
            int doorW = 20 * s;
            int doorX = x0 + w / 2 - doorW / 2;
            int rearZ = z0 + d - (t + s);
            int sideRightX = x0 + w - (t + s);
            int windowH = theme.WindowHeightDm * s;

            b.FoundationBox(x0, 0, z0, w, f, d, foundation);
            b.ShellBox(x0, f, z0, w, wallH, d, wall);
            b.InteriorCarve(x0 + t, f, z0 + t, w - 2 * t, wallH, d - 2 * t);
            b.Carve(doorX, f, z0, doorW, theme.DoorHeightDm * s, t + s);

            for (int storey = 0; storey < 2; storey++)
            {
                int y = f + storey * theme.FloorHeightDm * s + theme.WindowBaseDm * s;

                for (int bay = 0; bay < 4; bay++)
                {
                    int x = x0 + (16 + bay * 31) * s;
                    AddWindowZ(b, x, y, z0, 12 * s, windowH, t + s, glass);
                }

                for (int bay = 0; bay < 3; bay++)
                {
                    int x = x0 + (24 + bay * 42) * s;
                    AddWindowZ(b, x, y, rearZ, 12 * s, windowH, t + s, glass);
                }

                int sideA = z0 + 31 * s;
                int sideB = z0 + d - 43 * s;
                AddWindowX(b, x0, y, sideA, t + s, windowH, 12 * s, glass);
                AddWindowX(b, x0, y, sideB, t + s, windowH, 12 * s, glass);
                AddWindowX(b, sideRightX, y, sideA, t + s, windowH, 12 * s, glass);
                AddWindowX(b, sideRightX, y, sideB, t + s, windowH, 12 * s, glass);
            }

            AddTimberFrame(b, x0, z0, w, d, f, wallH, theme.BeamWidthDm * s, timber);
            b.Prism(x0 - 5 * s, f + wallH, z0 - 5 * s,
                w + 10 * s, theme.GrandRoofHeightDm * s, d + 10 * s,
                PrismProfile.Gable, roof);

            // Preserve the original deep entrance porch and hanging-sign beam.
            b.FoundationBox(doorX - 8 * s, f, z0 - 18 * s,
                doorW + 16 * s, 3 * s, 20 * s, foundation);
            b.Box(doorX - 7 * s, f, z0 - 15 * s, 4 * s, 30 * s, 4 * s, timber);
            b.Box(doorX + doorW + 3 * s, f, z0 - 15 * s, 4 * s, 30 * s, 4 * s, timber);
            b.Box(doorX - 7 * s, f + 27 * s, z0 - 15 * s,
                doorW + 14 * s, 4 * s, 4 * s, timber);

            int serviceW = 13 * s;
            b.Carve(x0 + w / 2 - serviceW / 2, f, rearZ,
                serviceW, theme.DoorHeightDm * s, t + s);

            int3 door = new int3(doorX + doorW / 2, f, z0);
            b.Anchor(0, door, Facing.South);
            return new Program(b.Finish(), door);
        }

        private static Program WarehouseProgram(
            ArchitectureTheme theme,
            VoxelWorldGenSettings settings,
            StructureGeometryProfile geometry)
        {
            int s = settings.VoxelsPerDecimetre;
            var b = new ProgramBuilder(geometry, s);
            byte stone = settings.Materials.Resolve(theme.Foundation);
            byte timber = settings.Materials.Resolve(theme.Frame);
            byte glass = settings.Materials.Resolve(theme.Window);
            byte roof = settings.Materials.Resolve(MaterialRole.Slate);

            int x0 = 15 * s, z0 = 18 * s, w = 158 * s, d = 142 * s;
            int f = 8 * s, wallH = 55 * s, t = 5 * s;
            int doorW = 42 * s, doorX = x0 + w / 2 - doorW / 2;
            int rearZ = z0 + d - (t + s);
            int sideRightX = x0 + w - (t + s);

            b.FoundationBox(x0, 0, z0, w, f, d, stone);
            b.ShellBox(x0, f, z0, w, wallH, d, timber);
            b.InteriorCarve(x0 + t, f, z0 + t, w - 2 * t, wallH, d - 2 * t);
            b.Carve(doorX, f, z0, doorW, 38 * s, t + s);

            for (int i = 0; i <= 5; i++)
            {
                int x = x0 + math.min(w - 5 * s, i * 31 * s);
                b.Box(x, f, z0 - s, 5 * s, wallH, 4 * s, stone);
                b.Box(x, f, z0 + d - 3 * s, 5 * s, wallH, 4 * s, stone);
            }

            int highY = f + 35 * s;
            for (int bay = 0; bay < 3; bay++)
            {
                int z = z0 + (26 + bay * 40) * s;
                AddWindowX(b, x0, highY, z, t + s, 10 * s, 16 * s, glass);
                AddWindowX(b, sideRightX, highY, z, t + s, 10 * s, 16 * s, glass);
            }

            int rearDoorW = 34 * s;
            b.Carve(x0 + w / 2 - rearDoorW / 2, f, rearZ,
                rearDoorW, 32 * s, t + s);

            b.Prism(x0 - 5 * s, f + wallH, z0 - 5 * s,
                w + 10 * s, 32 * s, d + 10 * s, PrismProfile.Gable, roof);

            int3 door = new int3(doorX + doorW / 2, f, z0);
            b.Anchor(0, door, Facing.South);
            return new Program(b.Finish(), door);
        }

        private static Program MansionProgram(
            ArchitectureTheme theme,
            VoxelWorldGenSettings settings,
            StructureGeometryProfile geometry)
        {
            int s = settings.VoxelsPerDecimetre;
            var b = new ProgramBuilder(geometry, s);
            byte stone = settings.Materials.Resolve(theme.AccentStone);
            byte wall = settings.Materials.Resolve(theme.Wall);
            byte timber = settings.Materials.Resolve(theme.Frame);
            byte glass = settings.Materials.Resolve(MaterialRole.WarmWindow);
            byte roof = settings.Materials.Resolve(MaterialRole.Slate);

            int x0 = 26 * s, z0 = 26 * s, w = 210 * s, d = 188 * s;
            int f = 9 * s, t = 5 * s;
            int wallH = 3 * theme.FloorHeightDm * s;
            int doorW = 22 * s, doorX = x0 + w / 2 - doorW / 2;
            int rearZ = z0 + d - (t + s);
            int sideRightX = x0 + w - (t + s);

            b.FoundationBox(x0, 0, z0, w, f, d, stone);
            b.ShellBox(x0, f, z0, w, wallH, d, wall);
            b.InteriorCarve(x0 + t, f, z0 + t, w - 2 * t, wallH, d - 2 * t);
            b.Carve(doorX, f, z0, doorW, 30 * s, t + s);

            for (int storey = 0; storey < 3; storey++)
            {
                int y = f + storey * theme.FloorHeightDm * s + theme.WindowBaseDm * s;

                for (int bay = 0; bay < 5; bay++)
                {
                    int x = x0 + (18 + bay * 39) * s;
                    AddWindowZ(b, x, y, z0,
                        13 * s, theme.WindowHeightDm * s, t + s, glass);
                    AddWindowZ(b, x, y, rearZ,
                        13 * s, theme.WindowHeightDm * s, t + s, glass);
                }

                for (int bay = 0; bay < 3; bay++)
                {
                    int z = z0 + (38 + bay * 50) * s;
                    AddWindowX(b, x0, y, z, t + s,
                        theme.WindowHeightDm * s, 13 * s, glass);
                    AddWindowX(b, sideRightX, y, z, t + s,
                        theme.WindowHeightDm * s, 13 * s, glass);
                }

                if (storey > 0)
                {
                    int bandY = f + storey * theme.FloorHeightDm * s - 2 * s;
                    b.Box(x0, bandY, z0 - s, w, 4 * s, 3 * s, stone);
                    b.Box(x0, bandY, z0 + d - 2 * s, w, 4 * s, 3 * s, stone);
                }
            }

            b.Box(x0, f, z0 - s, 7 * s, wallH, 5 * s, stone);
            b.Box(x0 + w - 7 * s, f, z0 - s, 7 * s, wallH, 5 * s, stone);
            b.Box(x0, f, z0 + d - 4 * s, 7 * s, wallH, 5 * s, stone);
            b.Box(x0 + w - 7 * s, f, z0 + d - 4 * s, 7 * s, wallH, 5 * s, stone);
            b.Box(doorX - 18 * s, f, z0 - 24 * s, 7 * s, 42 * s, 7 * s, stone);
            b.Box(doorX + doorW + 11 * s, f, z0 - 24 * s, 7 * s, 42 * s, 7 * s, stone);
            b.Box(doorX - 20 * s, f + 38 * s, z0 - 26 * s,
                doorW + 40 * s, 5 * s, 30 * s, timber);

            b.Prism(x0 - 6 * s, f + wallH, z0 - 6 * s,
                w + 12 * s, theme.GrandRoofHeightDm * s, d + 12 * s,
                PrismProfile.Gable, roof);

            int3 door = new int3(doorX + doorW / 2, f, z0);
            b.Anchor(0, door, Facing.South);
            return new Program(b.Finish(), door);
        }

        private static Program ChurchProgram(
            ArchitectureTheme theme,
            VoxelWorldGenSettings settings,
            StructureGeometryProfile geometry)
        {
            int s = settings.VoxelsPerDecimetre;
            var b = new ProgramBuilder(geometry, s);
            byte stone = settings.Materials.Resolve(theme.AccentStone);
            byte wall = settings.Materials.Resolve(theme.Wall);
            byte glass = settings.Materials.Resolve(theme.Window);
            byte roof = settings.Materials.Resolve(MaterialRole.Slate);

            int x0 = 22 * s, z0 = 18 * s, w = 120 * s, d = 132 * s;
            int f = 8 * s, t = 5 * s, naveH = 62 * s;
            int doorW = 20 * s, doorX = x0 + w / 2 - doorW / 2;
            int rearZ = z0 + d - (t + s);
            int sideRightX = x0 + w - (t + s);

            b.FoundationBox(x0, 0, z0, w, f, d, stone);
            b.ShellBox(x0, f, z0, w, naveH, d, wall);
            b.InteriorCarve(x0 + t, f, z0 + t, w - 2 * t, naveH, d - 2 * t);
            b.Carve(doorX, f, z0, doorW, 30 * s, t + s);
            AddWindowZ(b, x0 + 16 * s, f + 27 * s, z0,
                14 * s, 23 * s, t + s, glass);
            AddWindowZ(b, x0 + w - 30 * s, f + 27 * s, z0,
                14 * s, 23 * s, t + s, glass);

            for (int bay = 0; bay < 3; bay++)
            {
                int z = z0 + (56 + bay * 29) * s;
                AddWindowX(b, x0, f + 25 * s, z, t + s, 25 * s, 12 * s, glass);
                AddWindowX(b, sideRightX, f + 25 * s, z, t + s, 25 * s, 12 * s, glass);
            }

            AddWindowZ(b, x0 + 30 * s, f + 30 * s, rearZ,
                15 * s, 24 * s, t + s, glass);
            AddWindowZ(b, x0 + w - 45 * s, f + 30 * s, rearZ,
                15 * s, 24 * s, t + s, glass);

            b.Prism(x0 - 5 * s, f + naveH, z0 - 5 * s,
                w + 10 * s, 38 * s, d + 10 * s, PrismProfile.Gable, roof);

            int towerW = 42 * s;
            int towerX = x0 + w / 2 - towerW / 2;
            b.ShellBox(towerX, f, z0 + 5 * s, towerW, 112 * s, 42 * s, stone);
            b.InteriorCarve(towerX + 5 * s, f, z0 + 10 * s,
                towerW - 10 * s, 94 * s, 32 * s);
            b.Carve(doorX, f, z0, doorW, 30 * s, 12 * s);
            b.Prism(towerX - 4 * s, f + 112 * s, z0 + s,
                towerW + 8 * s, 36 * s, 50 * s, PrismProfile.Gable, roof);

            int3 door = new int3(doorX + doorW / 2, f, z0);
            b.Anchor(0, door, Facing.South);
            return new Program(b.Finish(), door);
        }

        private static Program WellProgram(
            ArchitectureTheme theme,
            VoxelWorldGenSettings settings,
            StructureGeometryProfile geometry)
        {
            int s = settings.VoxelsPerDecimetre;
            var b = new ProgramBuilder(geometry, s);
            byte stone = settings.Materials.Resolve(theme.AccentStone);
            byte timber = settings.Materials.Resolve(theme.Frame);
            byte roof = settings.Materials.Resolve(theme.Roof);
            byte water = settings.Materials.Resolve(MaterialRole.Water);
            int cx = 28 * s, cz = 28 * s;

            b.ShellCylinder(cx, 0, cz, 22 * s, 11 * s, 1, stone);
            b.InteriorCylinderCarve(cx, 3 * s, cz, 14 * s, 12 * s, 1);
            b.RawCylinder(cx, 3 * s, cz, 13 * s, 2 * s, 1, water);
            b.Box(7 * s, 8 * s, 25 * s, 5 * s, 37 * s, 5 * s, timber);
            b.Box(44 * s, 8 * s, 25 * s, 5 * s, 37 * s, 5 * s, timber);
            b.Box(7 * s, 42 * s, 23 * s, 42 * s, 5 * s, 9 * s, timber);
            b.Prism(4 * s, 47 * s, 16 * s, 48 * s, 13 * s, 24 * s,
                PrismProfile.Gable, roof);

            int3 interaction = new int3(cx, 11 * s, cz);
            b.Anchor(0, interaction, Facing.South);
            return new Program(b.Finish(), interaction);
        }

        private static void AddWindowZ(
            ProgramBuilder b,
            int x, int y, int z,
            int width, int height, int depth,
            byte material) =>
            ArchitectureVoxelPatterns.GlazedOpening(
                b.Inner, x, y, z, width, height, depth, material);

        private static void AddWindowX(
            ProgramBuilder b,
            int x, int y, int z,
            int depth, int height, int width,
            byte material) =>
            ArchitectureVoxelPatterns.GlazedOpening(
                b.Inner, x, y, z, depth, height, width, material);

        private static void AddTimberFrame(
            ProgramBuilder b,
            int x0, int z0, int width, int depth,
            int baseY, int wallHeight, int beam,
            byte timber) =>
            ArchitectureVoxelPatterns.TimberFrame(
                b.Inner,
                x0, z0, width, depth,
                baseY, wallHeight, beam, timber);

        private sealed class ProgramBuilder
        {
            private readonly ArchitectureShapeProgramBuilder _inner;

            public ProgramBuilder(StructureGeometryProfile profile, int voxelsPerDecimetre)
            {
                _inner = new ArchitectureShapeProgramBuilder(profile, voxelsPerDecimetre);
            }

            public ArchitectureShapeProgramBuilder Inner => _inner;

            public void FoundationBox(
                int x, int y, int z,
                int sx, int sy, int sz,
                byte material) =>
                _inner.FoundationBox(x, y, z, sx, sy, sz, material);

            public void ShellBox(
                int x, int y, int z,
                int sx, int sy, int sz,
                byte material) =>
                _inner.ShellBox(x, y, z, sx, sy, sz, material);

            public void Box(
                int x, int y, int z,
                int sx, int sy, int sz,
                byte material) =>
                _inner.DetailBox(x, y, z, sx, sy, sz, material);

            public void Carve(
                int x, int y, int z,
                int sx, int sy, int sz) =>
                _inner.OpeningCarve(x, y, z, sx, sy, sz);

            public void InteriorCarve(
                int x, int y, int z,
                int sx, int sy, int sz) =>
                _inner.InteriorCarve(x, y, z, sx, sy, sz);

            public void ShellCylinder(
                int cx, int y, int cz,
                int radius, int height,
                byte axis, byte material) =>
                _inner.ShellCylinder(cx, y, cz, radius, height, axis, material);

            public void InteriorCylinderCarve(
                int cx, int y, int cz,
                int radius, int height,
                byte axis) =>
                _inner.InteriorCylinderCarve(cx, y, cz, radius, height, axis);

            public void RawCylinder(
                int cx, int y, int cz,
                int radius, int height,
                byte axis, byte material) =>
                _inner.RawCylinder(cx, y, cz, radius, height, axis, material);

            public void Prism(
                int x, int y, int z,
                int sx, int sy, int sz,
                PrismProfile profile,
                byte material) =>
                _inner.Prism(x, y, z, sx, sy, sz, profile, material);

            public void Anchor(int index, int3 p, Facing facing) =>
                _inner.Anchor(index, p, facing);

            public int[] Finish() => _inner.Finish();
        }
    }
}
