using System.Collections.Generic;
using VoxelEngine.Core.Features;

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
    /// Written against the *default* parameters rather than reading registers, because US1's
    /// tests are about the evaluator and the rasteriser rather than about parameter plumbing.
    /// Register-driven dimensions arrive when the compiler does.
    /// </summary>
    public static class CottageProgram
    {
        public const int AnchorDoor = 0;
        public const int AnchorHearth = 1;

        /// <summary>Matches CottageFixture's declared footprint of 96 x 80 x 96 voxels.</summary>
        public static int[] Build()
        {
            const int width = 64;
            const int depth = 64;
            const int wallHeight = 32;
            const int wallThickness = 4;
            const int roofHeight = 16;

            const byte stone = CottageFixture.MaterialStone;
            const byte wood = CottageFixture.MaterialWood;

            var b = new ProgramBuilder();

            // Foundation, sunk so the walls have something to stand on.
            b.Box(0, 0, 0, width, 8, depth, stone, PrimitiveMode.Fill);

            // Solid block of wall, then the interior carved out of it. Cheaper to express and
            // impossible to leave a gap in a corner, which four separate walls invite.
            b.Box(0, 8, 0, width, wallHeight, depth, stone, PrimitiveMode.Fill);
            b.Box(wallThickness, 8, wallThickness,
                  width - 2 * wallThickness, wallHeight, depth - 2 * wallThickness,
                  0, PrimitiveMode.Carve);

            // Doorway through the south wall.
            b.Box(width / 2 - 6, 8, 0, 12, 20, wallThickness, 0, PrimitiveMode.Carve);

            // Gable roof sitting on the walls.
            b.Prism(0, 8 + wallHeight, 0, width, roofHeight, depth,
                    PrismProfile.Gable, wood, PrimitiveMode.Fill);

            b.Anchor(AnchorDoor, width / 2, 8, 0, Facing.South);
            b.Anchor(AnchorHearth, width / 2, 8, depth / 2, Facing.Up);

            b.End();

            return b.Build();
        }
    }
}
