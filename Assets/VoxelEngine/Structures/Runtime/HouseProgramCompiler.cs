using System;
using System.Collections.Generic;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Compiles archetype-level house configuration into the existing deterministic shape-program
    /// representation. It deliberately emits only existing opcodes; voxel cells remain the sole
    /// authoritative result after ordinary ShapeProgram evaluation/rasterisation.
    /// </summary>
    public static class HouseProgramCompiler
    {
        public static int[] BuildCompatibilityProgram(
            in HouseConfig config,
            int mainDoorAnchorIndex,
            int hearthAnchorIndex)
        {
            ValidateCompatibilityConfig(in config);

            int width = config.Footprint.Primary.Size.x;
            int depth = config.Footprint.Primary.Size.y;
            int foundationHeight = config.Footprint.FoundationDepth;
            int wallHeight = config.Walls.Height;
            int wallThickness = config.Walls.Thickness;
            int wallBaseY = foundationHeight;
            int roofBaseY = wallBaseY + wallHeight;
            int roofSpan = config.Roof.RidgeAxis == RoofAxis.Z ? width / 2 : depth / 2;
            int roofHeight = roofSpan * config.Roof.PitchRise / config.Roof.PitchRun;

            byte foundationMaterial = config.Palette.Resolve(config.Footprint.FoundationMaterial);
            byte wallMaterial = config.Palette.Resolve(config.Walls.PrimaryMaterial);
            byte roofMaterial = config.Palette.Resolve(config.Roof.MaterialRole);

            var writer = new Writer();

            // Keep the compatibility program's operation order stable: foundation, solid wall
            // block, interior carve, front-door carve, roof, then anchors.
            writer.Box(0, 0, 0, width, foundationHeight, depth,
                foundationMaterial, PrimitiveMode.Fill);

            writer.Box(0, wallBaseY, 0, width, wallHeight, depth,
                wallMaterial, PrimitiveMode.Fill);
            writer.Box(wallThickness, wallBaseY, wallThickness,
                width - 2 * wallThickness,
                wallHeight,
                depth - 2 * wallThickness,
                0,
                PrimitiveMode.Carve);

            int doorX = width / 2 - config.MainDoor.Width / 2;
            int doorY = wallBaseY + config.MainDoor.BottomOffset;
            writer.Box(doorX, doorY, 0,
                config.MainDoor.Width,
                config.MainDoor.Height,
                wallThickness,
                0,
                PrimitiveMode.Carve);

            writer.Prism(0, roofBaseY, 0, width, roofHeight, depth,
                PrismProfile.Gable, roofMaterial, PrimitiveMode.Fill);

            writer.Anchor(mainDoorAnchorIndex,
                width / 2,
                doorY,
                0,
                Facing.South);
            writer.Anchor(hearthAnchorIndex,
                width / 2,
                wallBaseY,
                depth / 2,
                Facing.Up);
            writer.End();

            return writer.Build();
        }

        private static void ValidateCompatibilityConfig(in HouseConfig config)
        {
            if (!config.Footprint.IsWellFormed || !config.Walls.IsWellFormed)
                throw new ArgumentException("House footprint/wall configuration is invalid.");
            if (config.Footprint.FoundationDepth <= 0)
                throw new ArgumentException("Compatibility house requires a positive foundation depth.");
            if (config.MainDoor.Kind != StructureOpeningKind.Door ||
                StructureComponentValidation.Opening(in config.MainDoor, config.Footprint.Primary.Size.x)
                    != StructureComponentValidationIssue.None)
                throw new ArgumentException("Compatibility house main-door configuration is invalid.");
            if (StructureComponentValidation.Roof(in config.Roof)
                    != StructureComponentValidationIssue.None ||
                config.Roof.Style != RoofStyle.Gable)
                throw new ArgumentException("Compatibility house requires a supported gable roof.");

            int width = config.Footprint.Primary.Size.x;
            int depth = config.Footprint.Primary.Size.y;
            int thickness = config.Walls.Thickness;
            if (width <= thickness * 2 || depth <= thickness * 2)
                throw new ArgumentException("House wall thickness leaves no navigable interior.");
        }

        private sealed class Writer
        {
            private readonly List<int> _code = new();

            private void Emit(ShapeOp op, int mask, params int[] operands)
            {
                _code.Add((int)op);
                _code.Add(mask);
                _code.AddRange(operands);
            }

            public void Box(
                int x, int y, int z,
                int sx, int sy, int sz,
                byte material,
                PrimitiveMode mode)
                => Emit(ShapeOp.EmitBox, 0,
                    x, y, z, sx, sy, sz, material, 0, 0, (int)mode);

            public void Prism(
                int x, int y, int z,
                int sx, int sy, int sz,
                PrismProfile profile,
                byte material,
                PrimitiveMode mode)
                => Emit(ShapeOp.EmitPrism, 0,
                    x, y, z, sx, sy, sz, (int)profile, material, 0, 0, (int)mode);

            public void Anchor(int index, int x, int y, int z, Facing facing)
                => Emit(ShapeOp.SetAnchor, 0, index, x, y, z, (int)facing);

            public void End() => Emit(ShapeOp.End, 0);

            public int[] Build() => _code.ToArray();
        }
    }
}
