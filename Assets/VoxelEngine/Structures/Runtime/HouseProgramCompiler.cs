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
        /// <summary>
        /// Compiles the original cottage compatibility preset with the exact historical opcode
        /// ordering. Keep this path stable so richer house authoring cannot silently change the
        /// baseline fixture while the general compiler evolves.
        /// </summary>
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

        /// <summary>
        /// General bounded house compiler used by non-compatibility presets. The same config type
        /// controls dimensions, levels, facade openings, roof family, and optional chimney; no
        /// preset owns a private geometry path.
        /// </summary>
        public static int[] BuildProgram(
            in HouseConfig config,
            int mainDoorAnchorIndex,
            int hearthAnchorIndex)
        {
            ValidateGeneralConfig(in config);

            int width = config.Width;
            int depth = config.Depth;
            int foundationHeight = config.FoundationDepth;
            int wallHeight = config.Walls.Height;
            int wallThickness = config.WallThickness;
            int wallBaseY = foundationHeight;
            int roofBaseY = wallBaseY + wallHeight;

            byte foundationMaterial = config.Palette.Resolve(config.Footprint.FoundationMaterial);
            byte wallMaterial = config.Palette.Resolve(config.Walls.PrimaryMaterial);
            byte floorMaterial = config.Palette.Resolve(config.Floors.SlabMaterialRole);
            byte roofMaterial = config.Palette.Resolve(config.Roof.MaterialRole);

            var writer = new Writer();

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

            EmitIntermediateFloors(in config, wallBaseY, width, depth, wallThickness,
                floorMaterial, writer);

            EmitDoorLayout(in config.FrontDoors, wallBaseY, width, depth, wallThickness, writer);
            EmitDoorLayout(in config.RearDoors, wallBaseY, width, depth, wallThickness, writer);
            EmitDoorLayout(in config.LeftDoors, wallBaseY, width, depth, wallThickness, writer);
            EmitDoorLayout(in config.RightDoors, wallBaseY, width, depth, wallThickness, writer);

            EmitWindowLayout(in config.FrontWindows, wallBaseY, width, depth, wallThickness, writer);
            EmitWindowLayout(in config.RearWindows, wallBaseY, width, depth, wallThickness, writer);
            EmitWindowLayout(in config.LeftWindows, wallBaseY, width, depth, wallThickness, writer);
            EmitWindowLayout(in config.RightWindows, wallBaseY, width, depth, wallThickness, writer);

            EmitRoof(in config.Roof, roofBaseY, width, depth, roofMaterial, writer);
            EmitChimney(in config, roofBaseY, writer);

            int mainDoorY = wallBaseY + config.FrontDoors.Opening.BottomOffset;
            writer.Anchor(mainDoorAnchorIndex,
                width / 2,
                mainDoorY,
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

        private static void EmitIntermediateFloors(
            in HouseConfig config,
            int wallBaseY,
            int width,
            int depth,
            int wallThickness,
            byte material,
            Writer writer)
        {
            if (config.FloorCount <= 1 || config.Floors.SlabThickness <= 0)
                return;

            int interiorWidth = width - 2 * wallThickness;
            int interiorDepth = depth - 2 * wallThickness;
            for (int level = 1; level < config.FloorCount; level++)
            {
                int y = wallBaseY + level * config.FloorHeight - config.Floors.SlabThickness;
                writer.Box(wallThickness, y, wallThickness,
                    interiorWidth, config.Floors.SlabThickness, interiorDepth,
                    material, PrimitiveMode.Fill);
            }
        }

        private static void EmitDoorLayout(
            in HouseDoorLayoutConfig layout,
            int wallBaseY,
            int width,
            int depth,
            int wallThickness,
            Writer writer)
        {
            if (layout.Count <= 0)
                return;

            for (int i = 0; i < layout.Count; i++)
            {
                int runLength = IsNorthSouthFacade(layout.Facade) ? width : depth;
                int offset = OpeningOffset(layout.Placement, layout.ExplicitOffsets, i,
                    layout.Count, runLength, layout.Opening.Width);
                EmitFacadeOpening(layout.Facade, offset, wallBaseY + layout.Opening.BottomOffset,
                    layout.Opening.Width, layout.Opening.Height,
                    width, depth, wallThickness, writer);
            }
        }

        private static void EmitWindowLayout(
            in HouseWindowLayoutConfig layout,
            int wallBaseY,
            int width,
            int depth,
            int wallThickness,
            Writer writer)
        {
            if (layout.Count <= 0)
                return;

            for (int i = 0; i < layout.Count; i++)
            {
                int runLength = IsNorthSouthFacade(layout.Facade) ? width : depth;
                int offset = OpeningOffset(layout.Placement, layout.ExplicitOffsets, i,
                    layout.Count, runLength, layout.Opening.Width);
                EmitFacadeOpening(layout.Facade, offset, wallBaseY + layout.Opening.BottomOffset,
                    layout.Opening.Width, layout.Opening.Height,
                    width, depth, wallThickness, writer);
            }
        }

        private static int OpeningOffset(
            HouseFacadePlacementMode mode,
            Unity.Collections.FixedList128Bytes<int> explicitOffsets,
            int index,
            int count,
            int runLength,
            int openingWidth)
        {
            switch (mode)
            {
                case HouseFacadePlacementMode.ExplicitOffsets:
                    return explicitOffsets[index];
                case HouseFacadePlacementMode.EvenlySpaced:
                {
                    int free = runLength - count * openingWidth;
                    int gap = free / (count + 1);
                    return gap + index * (openingWidth + gap);
                }
                default:
                    if (count == 1)
                        return (runLength - openingWidth) / 2;
                    int groupWidth = count * openingWidth;
                    return (runLength - groupWidth) / 2 + index * openingWidth;
            }
        }

        private static bool IsNorthSouthFacade(HouseFacade facade) =>
            facade == HouseFacade.Front || facade == HouseFacade.Rear;

        private static void EmitFacadeOpening(
            HouseFacade facade,
            int offset,
            int y,
            int openingWidth,
            int openingHeight,
            int width,
            int depth,
            int wallThickness,
            Writer writer)
        {
            switch (facade)
            {
                case HouseFacade.Front:
                    writer.Box(offset, y, 0,
                        openingWidth, openingHeight, wallThickness, 0, PrimitiveMode.Carve);
                    break;
                case HouseFacade.Rear:
                    writer.Box(offset, y, depth - wallThickness,
                        openingWidth, openingHeight, wallThickness, 0, PrimitiveMode.Carve);
                    break;
                case HouseFacade.Left:
                    writer.Box(0, y, offset,
                        wallThickness, openingHeight, openingWidth, 0, PrimitiveMode.Carve);
                    break;
                case HouseFacade.Right:
                    writer.Box(width - wallThickness, y, offset,
                        wallThickness, openingHeight, openingWidth, 0, PrimitiveMode.Carve);
                    break;
            }
        }

        private static void EmitRoof(
            in RoofConfig roof,
            int roofBaseY,
            int width,
            int depth,
            byte material,
            Writer writer)
        {
            switch (roof.Style)
            {
                case RoofStyle.Flat:
                    writer.Box(0, roofBaseY, 0, width, roof.Thickness, depth,
                        material, PrimitiveMode.Fill);
                    return;
                case RoofStyle.Gable:
                case RoofStyle.Shed:
                {
                    int halfSpan = roof.RidgeAxis == RoofAxis.Z ? width / 2 : depth / 2;
                    int roofHeight = halfSpan * roof.PitchRise / roof.PitchRun;
                    writer.Prism(0, roofBaseY, 0, width, roofHeight, depth,
                        roof.Style == RoofStyle.Gable ? PrismProfile.Gable : PrismProfile.Shed,
                        material, PrimitiveMode.Fill);
                    return;
                }
                default:
                    throw new ArgumentException("Hip roofs are exposed by the shared config but require a later bounded primitive composition before this compiler may emit them.");
            }
        }

        private static void EmitChimney(in HouseConfig config, int roofBaseY, Writer writer)
        {
            if (!config.Chimney.Enabled)
                return;

            VerticalAccentConfig geometry = config.Chimney.Geometry;
            byte material = config.Palette.Resolve(geometry.MaterialRole);
            writer.Box(config.Chimney.LocalPosition.x, roofBaseY, config.Chimney.LocalPosition.y,
                geometry.Width, geometry.Height, geometry.Depth,
                material, PrimitiveMode.Fill);
        }

        private static void ValidateCompatibilityConfig(in HouseConfig config)
        {
            ValidateCore(in config);
            if (config.MainDoor.Kind != StructureOpeningKind.Door ||
                StructureComponentValidation.Opening(in config.MainDoor, config.Footprint.Primary.Size.x)
                    != StructureComponentValidationIssue.None)
                throw new ArgumentException("Compatibility house main-door configuration is invalid.");
            if (StructureComponentValidation.Roof(in config.Roof)
                    != StructureComponentValidationIssue.None ||
                config.Roof.Style != RoofStyle.Gable)
                throw new ArgumentException("Compatibility house requires a supported gable roof.");
        }

        private static void ValidateGeneralConfig(in HouseConfig config)
        {
            ValidateCore(in config);
            if (config.Floors.FloorCount <= 0 || config.Floors.LevelHeight <= 0 ||
                config.Floors.SlabThickness < 0)
                throw new ArgumentException("House floor configuration is invalid.");
            if (config.Walls.Height < config.Floors.FloorCount * config.Floors.LevelHeight)
                throw new ArgumentException("House wall height does not contain all configured levels.");
            if (!config.FrontDoors.IsWellFormed || !config.RearDoors.IsWellFormed ||
                !config.LeftDoors.IsWellFormed || !config.RightDoors.IsWellFormed ||
                !config.FrontWindows.IsWellFormed || !config.RearWindows.IsWellFormed ||
                !config.LeftWindows.IsWellFormed || !config.RightWindows.IsWellFormed)
                throw new ArgumentException("House facade opening configuration is invalid.");
            if (StructureComponentValidation.Roof(in config.Roof)
                != StructureComponentValidationIssue.None)
                throw new ArgumentException("House roof configuration is invalid.");
            if (!config.Chimney.IsWellFormed)
                throw new ArgumentException("House chimney configuration is invalid.");
        }

        private static void ValidateCore(in HouseConfig config)
        {
            if (!config.Footprint.IsWellFormed || !config.Walls.IsWellFormed)
                throw new ArgumentException("House footprint/wall configuration is invalid.");
            if (config.Footprint.FoundationDepth <= 0)
                throw new ArgumentException("House requires a positive foundation depth.");

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
