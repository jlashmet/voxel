using System.Collections.Generic;
using Unity.Mathematics;
using VoxelEngine.Structures.Runtime;

using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.Features.Fixtures
{
    /// <summary>
    /// Assembles a shape program by hand.
    ///
    /// A compiler for the authoring text format arrives with US8. Writing the opcodes directly
    /// until then keeps the evaluator's tests independent of a parser that does not exist yet —
    /// a fixture built through the parser cannot tell you which of the two broke.
    /// </summary>
    public sealed class ProgramBuilder
    {
        private readonly List<int> _code = new();

        /// <summary>Marks operand <paramref name="index"/> as a register reference.</summary>
        public static int Reg(int index) => index;

        public ProgramBuilder Emit(ShapeOp op, int mask, params int[] operands)
        {
            _code.Add((int)op);
            _code.Add(mask);
            _code.AddRange(operands);
            return this;
        }

        public ProgramBuilder Box(int x, int y, int z, int sx, int sy, int sz,
                                  byte material, PrimitiveMode mode, int mask = 0,
                                  ushort style = 0, byte coating = 0)
            => Emit(ShapeOp.EmitBox, mask, x, y, z, sx, sy, sz, material,
                    style, coating, (int)mode);

        public ProgramBuilder Prism(int x, int y, int z, int sx, int sy, int sz,
                                    PrismProfile profile, byte material, PrimitiveMode mode, int mask = 0,
                                    ushort style = 0, byte coating = 0)
            => Emit(ShapeOp.EmitPrism, mask, x, y, z, sx, sy, sz, (int)profile,
                    material, style, coating, (int)mode);

        public ProgramBuilder Cylinder(int x, int y, int z, int radius, int height, byte axis,
                                       byte material, PrimitiveMode mode, int mask = 0,
                                       ushort style = 0, byte coating = 0)
            => Emit(ShapeOp.EmitCylinder, mask, x, y, z, radius, height, axis,
                    material, style, coating, (int)mode);

        public ProgramBuilder Anchor(int index, int x, int y, int z, Facing facing, int mask = 0)
            => Emit(ShapeOp.SetAnchor, mask, index, x, y, z, (int)facing);

        public ProgramBuilder End() => Emit(ShapeOp.End, 0);

        public int[] Build() => _code.ToArray();
        public int Length => _code.Count;
    }

    /// <summary>
    /// A cottage: foundation, four walls, a hollow interior, a door, and a gable roof.
    ///
    /// The compatibility fixture now expresses its defaults through the same shared architectural
    /// component contracts used by configurable structures. It still emits the original bounded
    /// integer shape-program sequence so WB031 does not silently change the established cottage.
    /// Register-driven dimensions arrive with the detailed house configuration tasks.
    /// </summary>
    public static class CottageProgram
    {
        public const int AnchorDoor = 0;
        public const int AnchorHearth = 1;

        /// <summary>Matches CottageFixture's declared footprint of 96 x 80 x 96 voxels.</summary>
        public static int[] Build()
        {
            var footprint = new StructureFootprintConfig
            {
                Primary = new StructureFootprintRect(int2.zero, new int2(64, 64)),
                BasePlane = BasePlaneRule.LowestGround,
                FoundationStyle = StructureFoundationStyle.Slab,
                FoundationDepth = 8,
                FoundationMaterial = StructureMaterialRole.Foundation,
            };

            var wall = new StructureWallRunConfig
            {
                Length = footprint.Primary.Size.x,
                Height = 32,
                Thickness = 4,
                PrimaryMaterial = StructureMaterialRole.PrimaryWall,
                CornerBehavior = StructureWallCornerBehavior.Overlap,
            };

            var door = new OpeningConfig
            {
                Kind = StructureOpeningKind.Door,
                Width = 12,
                Height = 20,
                BottomOffset = 0,
                Spacing = 0,
                StartMargin = 0,
                EndMargin = 0,
                FrameThickness = 0,
                LintelThickness = 0,
                WidthVariation = 0,
                HeightVariation = 0,
                FillMaterialRole = StructureMaterialRole.Opening,
            };

            var roof = new RoofConfig
            {
                Style = RoofStyle.Gable,
                RidgeAxis = RoofAxis.Z,
                PitchRise = 1,
                PitchRun = 2,
                EaveOverhang = 0,
                Thickness = 1,
                ParapetHeight = 0,
                MaterialRole = StructureMaterialRole.Roof,
                TrimMaterialRole = StructureMaterialRole.Trim,
            };

            var palette = new StructureMaterialPalette
            {
                Foundation = CottageFixture.MaterialStone,
                PrimaryWall = CottageFixture.MaterialStone,
                Roof = CottageFixture.MaterialWood,
                Opening = 0,
            };

            int width = footprint.Primary.Size.x;
            int depth = footprint.Primary.Size.y;
            int wallBaseY = footprint.FoundationDepth;
            int roofSpan = roof.RidgeAxis == RoofAxis.Z ? width : depth;
            int roofHeight = (roofSpan / 2 * roof.PitchRise) / roof.PitchRun;
            byte foundationMaterial = palette.Resolve(footprint.FoundationMaterial);
            byte wallMaterial = palette.Resolve(wall.PrimaryMaterial);
            byte roofMaterial = palette.Resolve(roof.MaterialRole);

            var b = new ProgramBuilder();

            // Foundation, sunk so the walls have something to stand on.
            b.Box(0, 0, 0, width, footprint.FoundationDepth, depth,
                  foundationMaterial, PrimitiveMode.Fill);

            // Solid block of wall, then the interior carved out of it. Cheaper to express and
            // impossible to leave a gap in a corner, which four separate walls invite.
            b.Box(0, wallBaseY, 0, width, wall.Height, depth, wallMaterial, PrimitiveMode.Fill);
            b.Box(wall.Thickness, wallBaseY, wall.Thickness,
                  width - 2 * wall.Thickness, wall.Height, depth - 2 * wall.Thickness,
                  0, PrimitiveMode.Carve);

            // Doorway through the south wall.
            b.Box(width / 2 - door.Width / 2, wallBaseY + door.BottomOffset, 0,
                  door.Width, door.Height, wall.Thickness,
                  palette.Resolve(door.FillMaterialRole), PrimitiveMode.Carve);

            // Gable roof sitting on the walls.
            b.Prism(0, wallBaseY + wall.Height, 0, width, roofHeight, depth,
                    PrismProfile.Gable, roofMaterial, PrimitiveMode.Fill);

            b.Anchor(AnchorDoor, width / 2, wallBaseY, 0, Facing.South);
            b.Anchor(AnchorHearth, width / 2, wallBaseY, depth / 2, Facing.Up);

            b.End();

            return b.Build();
        }
    }
}
