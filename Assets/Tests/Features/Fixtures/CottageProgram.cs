using System.Collections.Generic;
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
    /// Compatibility fixture for the original cottage. Defaults now enter through the production
    /// house authoring config/compiler while this fixture retains the historical anchor indices and
    /// material ids used by the world-feature tests.
    /// </summary>
    public static class CottageProgram
    {
        public const int AnchorDoor = 0;
        public const int AnchorHearth = 1;

        /// <summary>Matches CottageFixture's declared footprint of 96 x 80 x 96 voxels.</summary>
        public static int[] Build()
        {
            HouseConfig config = HousePresets.CottageCompatibility(
                CottageFixture.MaterialStone,
                CottageFixture.MaterialWood);
            return HouseProgramCompiler.BuildCompatibilityProgram(
                in config,
                AnchorDoor,
                AnchorHearth);
        }
    }
}
