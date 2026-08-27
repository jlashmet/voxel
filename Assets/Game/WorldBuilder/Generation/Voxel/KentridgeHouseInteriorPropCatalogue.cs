using System;
using System.Collections.Generic;
using MountingForce.WorldGen.Architecture;
using MountingForce.WorldGen.Content.Kentridge;
using VoxelEngine.Structures.Api;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Reusable interior-prop catalogue for generated Kentridge houses, shops, inns, and pubs.
    /// The catalogue appends furniture to the owning structure program so props inherit the
    /// building's exact placement, orientation, precedence, and streaming lifetime.
    /// </summary>
    internal static class KentridgeHouseInteriorPropCatalogue
    {
        // A deliberately distinctive common table top also serves as the behavioral signature
        // exercised through KentridgeSharedStructureVoxelCatalogue by the regression test.
        private const int TableWidthDm = 23;
        private const int TableHeightDm = 2;
        private const int TableDepthDm = 13;
        private const int TablePedestalDm = 5;
        private const int TablePedestalHeightDm = 8;
        private const int TableSideInsetDm = 4;
        private const int TableRearOffsetDm = 23;

        public static int[] Decorate(
            StructureForm form,
            ArchitectureTheme theme,
            VoxelWorldGenSettings settings,
            int[] program)
        {
            if (!form.IsGenerated)
                throw new ArgumentException(
                    "Interior prop catalogue only decorates generated Kentridge structures.",
                    nameof(form));

            int endLength = ShapeOps.InstructionLength(ShapeOp.End);
            if (program == null
                || program.Length < endLength
                || (ShapeOp)program[program.Length - endLength] != ShapeOp.End)
            {
                throw new InvalidOperationException(
                    "Generated Kentridge structure program is missing its terminal End instruction.");
            }

            int scale = settings.VoxelsPerDecimetre;
            int width = form.WidthDm * scale;
            int depth = form.DepthDm * scale;
            int foundation = theme.FoundationHeightDm * scale;
            int wall = Math.Max(1, theme.WallThicknessDm * scale);
            Int3 envelopeDm = SettlementFootprints.For(settings.Settlement, form.Archetype);
            int x0 = (envelopeDm.X * scale - width) / 2;
            int z0 = 10 * scale;

            int left = x0 + wall;
            int right = x0 + width - wall;
            int front = z0 + wall;
            int rear = z0 + depth - wall;
            int interiorWidth = right - left;
            int interiorDepth = rear - front;
            if (interiorWidth < 58 * scale || interiorDepth < 47 * scale)
            {
                throw new InvalidOperationException(
                    "Generated Kentridge structure is too small for its bounded interior prop layout.");
            }

            byte timber = settings.Materials.Resolve(theme.Frame);
            byte cloth = settings.Materials.Resolve(MaterialRole.Cloth);
            byte accent = settings.Materials.Resolve(theme.AccentStone);

            var code = new List<int>(program.Length + 72);
            for (int i = 0; i < program.Length - endLength; i++)
                code.Add(program[i]);

            int tableX = left + TableSideInsetDm * scale;
            int tableZ = rear - TableRearOffsetDm * scale;
            EmitBox(
                code,
                tableX + 9 * scale,
                foundation,
                tableZ + 4 * scale,
                TablePedestalDm * scale,
                TablePedestalHeightDm * scale,
                TablePedestalDm * scale,
                timber);
            EmitBox(
                code,
                tableX,
                foundation + TablePedestalHeightDm * scale,
                tableZ,
                TableWidthDm * scale,
                TableHeightDm * scale,
                TableDepthDm * scale,
                timber);

            if (form.Archetype == StructureArchetype.Shop)
            {
                AddShopFurniture(code, right, rear, foundation, scale, timber, accent);
            }
            else if (form.RoleId == (int)KentridgeRole.Inn
                  || form.RoleId == (int)KentridgeRole.Pub)
            {
                AddHospitalityFurniture(code, right, rear, foundation, scale, timber);
            }
            else
            {
                AddHomeFurniture(code, right, rear, foundation, scale, timber, cloth);
            }

            for (int i = program.Length - endLength; i < program.Length; i++)
                code.Add(program[i]);

            return KentridgeHouseVerticalCirculation.Decorate(
                form,
                theme,
                settings,
                code.ToArray());
        }

        private static void AddHomeFurniture(
            List<int> code,
            int right,
            int rear,
            int foundation,
            int scale,
            byte timber,
            byte cloth)
        {
            // A low bed frame and cloth mattress make ordinary homes read as inhabited without
            // entering the front-door lane or the central hearth strip.
            int bedX = right - 24 * scale;
            int bedZ = rear - 20 * scale;
            EmitBox(code, bedX, foundation, bedZ,
                22 * scale, 4 * scale, 12 * scale, timber);
            EmitBox(code, bedX + scale, foundation + 4 * scale, bedZ + scale,
                20 * scale, 3 * scale, 10 * scale, cloth);
        }

        private static void AddShopFurniture(
            List<int> code,
            int right,
            int rear,
            int foundation,
            int scale,
            byte timber,
            byte accent)
        {
            // Rear counter plus wall shelf: customers keep the central/front circulation space.
            int counterX = right - 28 * scale;
            int counterZ = rear - 18 * scale;
            EmitBox(code, counterX, foundation, counterZ,
                24 * scale, 9 * scale, 6 * scale, timber);
            EmitBox(code, counterX - scale, foundation + 9 * scale, counterZ - scale,
                26 * scale, 2 * scale, 8 * scale, accent);
            EmitBox(code, right - 24 * scale, foundation + 13 * scale, rear - 5 * scale,
                20 * scale, 16 * scale, 4 * scale, timber);
        }

        private static void AddHospitalityFurniture(
            List<int> code,
            int right,
            int rear,
            int foundation,
            int scale,
            byte timber)
        {
            // A rear bench complements the shared table; the Pub keeps its pre-existing bar counter.
            int benchX = right - 28 * scale;
            int benchZ = rear - 22 * scale;
            EmitBox(code, benchX, foundation, benchZ,
                24 * scale, 6 * scale, 6 * scale, timber);
            EmitBox(code, benchX, foundation + 6 * scale, benchZ + 5 * scale,
                24 * scale, 8 * scale, 3 * scale, timber);
        }

        private static void EmitBox(
            List<int> code,
            int x,
            int y,
            int z,
            int sx,
            int sy,
            int sz,
            byte material)
        {
            code.Add((int)ShapeOp.EmitBox);
            code.Add(0);
            code.Add(x);
            code.Add(y);
            code.Add(z);
            code.Add(sx);
            code.Add(sy);
            code.Add(sz);
            code.Add(material);
            code.Add(0);
            code.Add(0);
            code.Add((int)PrimitiveMode.Fill);
        }
    }
}
