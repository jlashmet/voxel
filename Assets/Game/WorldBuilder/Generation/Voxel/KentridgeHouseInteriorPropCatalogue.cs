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
            byte warm = settings.Materials.Resolve(MaterialRole.WarmWindow);

            var code = new List<int>(program.Length + 420);
            for (int i = 0; i < program.Length - endLength; i++)
                code.Add(program[i]);

            int tableX = left + TableSideInsetDm * scale;
            int tableZ = rear - TableRearOffsetDm * scale;
            AddTable(code, tableX, tableZ, foundation, scale, timber);

            if (form.Archetype == StructureArchetype.Shop)
            {
                AddShopFurniture(code, right, rear, foundation, scale, timber, accent);
            }
            else if (form.RoleId == (int)KentridgeRole.Pub)
            {
                AddPubFurniture(
                    code, left, right, front, rear, tableX, tableZ,
                    foundation, scale, timber, cloth, accent, warm);
            }
            else if (form.RoleId == (int)KentridgeRole.Inn)
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
            int benchX = right - 28 * scale;
            int benchZ = rear - 22 * scale;
            EmitBox(code, benchX, foundation, benchZ,
                24 * scale, 6 * scale, 6 * scale, timber);
            EmitBox(code, benchX, foundation + 6 * scale, benchZ + 5 * scale,
                24 * scale, 8 * scale, 3 * scale, timber);
        }

        private static void AddPubFurniture(
            List<int> code,
            int left,
            int right,
            int front,
            int rear,
            int commonTableX,
            int commonTableZ,
            int foundation,
            int scale,
            byte timber,
            byte cloth,
            byte accent,
            byte warm)
        {
            // Keep a clear front/centre aisle. Tables occupy the left side while the bar anchors
            // the right rear wall, matching the captured player's approach through the front door.
            AddChair(code, commonTableX + 8 * scale, commonTableZ - 7 * scale,
                foundation, scale, timber, false);
            AddChair(code, commonTableX + 8 * scale, commonTableZ + TableDepthDm * scale,
                foundation, scale, timber, true);
            AddTableChairs(code, left + 8 * scale, front + 14 * scale,
                foundation, scale, timber);
            AddTableChairs(code, left + 8 * scale, front + 42 * scale,
                foundation, scale, timber);

            int barX = right - 48 * scale;
            int barZ = rear - 24 * scale;
            EmitBox(code, barX, foundation, barZ,
                44 * scale, 9 * scale, 8 * scale, timber);
            EmitBox(code, barX - scale, foundation + 9 * scale, barZ - scale,
                46 * scale, 2 * scale, 10 * scale, accent);

            // Two narrow shelves behind the bartender leave the warm rear windows readable.
            EmitBox(code, barX + 4 * scale, foundation + 13 * scale, rear - 4 * scale,
                36 * scale, 2 * scale, 3 * scale, timber);
            EmitBox(code, barX + 4 * scale, foundation + 22 * scale, rear - 4 * scale,
                36 * scale, 2 * scale, 3 * scale, timber);

            AddWomanBartender(
                code, barX + 20 * scale, rear - 14 * scale,
                foundation, scale, timber, cloth, accent, warm);
        }

        private static void AddTableChairs(
            List<int> code,
            int tableX,
            int tableZ,
            int foundation,
            int scale,
            byte timber)
        {
            AddTable(code, tableX, tableZ, foundation, scale, timber);
            AddChair(code, tableX + 8 * scale, tableZ - 7 * scale,
                foundation, scale, timber, false);
            AddChair(code, tableX + 8 * scale, tableZ + TableDepthDm * scale,
                foundation, scale, timber, true);
        }

        private static void AddTable(
            List<int> code,
            int tableX,
            int tableZ,
            int foundation,
            int scale,
            byte timber)
        {
            EmitBox(code,
                tableX + 9 * scale,
                foundation,
                tableZ + 4 * scale,
                TablePedestalDm * scale,
                TablePedestalHeightDm * scale,
                TablePedestalDm * scale,
                timber);
            EmitBox(code,
                tableX,
                foundation + TablePedestalHeightDm * scale,
                tableZ,
                TableWidthDm * scale,
                TableHeightDm * scale,
                TableDepthDm * scale,
                timber);
        }

        private static void AddChair(
            List<int> code,
            int x,
            int z,
            int foundation,
            int scale,
            byte timber,
            bool backAtRear)
        {
            EmitBox(code, x, foundation + 4 * scale, z,
                6 * scale, 3 * scale, 6 * scale, timber);
            int backZ = backAtRear ? z + 4 * scale : z;
            EmitBox(code, x, foundation + 5 * scale, backZ,
                6 * scale, 8 * scale, 2 * scale, timber);
        }

        private static void AddWomanBartender(
            List<int> code,
            int x,
            int z,
            int foundation,
            int scale,
            byte timber,
            byte cloth,
            byte accent,
            byte warm)
        {
            // Static voxel staff figure: dress/apron and long side hair make the requested woman
            // bartender legible without introducing an NPC/AI system into a baked furnishing pass.
            EmitBox(code, x, foundation, z,
                8 * scale, 8 * scale, 5 * scale, cloth);
            EmitBox(code, x - scale, foundation + 8 * scale, z,
                10 * scale, 7 * scale, 5 * scale, cloth);
            EmitBox(code, x + scale, foundation + 8 * scale, z - scale,
                6 * scale, 7 * scale, scale, accent);
            EmitBox(code, x + scale, foundation + 15 * scale, z,
                6 * scale, 5 * scale, 5 * scale, warm);
            EmitBox(code, x, foundation + 19 * scale, z - scale,
                8 * scale, 3 * scale, 6 * scale, timber);
            EmitBox(code, x - scale, foundation + 13 * scale, z - scale,
                2 * scale, 7 * scale, 6 * scale, timber);
            EmitBox(code, x + 7 * scale, foundation + 13 * scale, z - scale,
                2 * scale, 7 * scale, 6 * scale, timber);
            EmitBox(code, x - 3 * scale, foundation + 8 * scale, z + scale,
                2 * scale, 7 * scale, 3 * scale, warm);
            EmitBox(code, x + 9 * scale, foundation + 8 * scale, z + scale,
                2 * scale, 7 * scale, 3 * scale, warm);
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
