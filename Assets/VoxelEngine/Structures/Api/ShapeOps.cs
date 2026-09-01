using System;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// The shape program instruction set.
    ///
    /// A program is a flat <c>int</c> array. Each instruction is:
    ///
    /// <code>
    ///   [ opcode ] [ modeMask ] [ operand 0 ] [ operand 1 ] ... [ operand n-1 ]
    /// </code>
    ///
    /// where <c>modeMask</c> bit <c>i</c> means operand <c>i</c> is a register index rather than
    /// an immediate value. A separate mask rather than tagging the operands themselves, because
    /// tagging a bit inside the value collides with negative immediates — and negative
    /// coordinates are ordinary here, since a definition's local origin need not be its corner.
    ///
    /// Operand counts are fixed per opcode, so the decoder always knows where the next
    /// instruction starts without a length field, and a malformed program fails validation rather
    /// than running off the end.
    ///
    /// Control flow is structured and bounded: no jumps, no computed trip counts. That is what
    /// lets validation *prove* an upper bound on emitted primitives instead of discovering it at
    /// runtime in a region that is already mid-generation.
    /// </summary>
    public enum ShapeOp
    {
        /// <summary>Ends the program. Zero operands.</summary>
        End = 0,

        // -- emit ---------------------------------------------------------------

        /// <summary>minX, minY, minZ, sizeX, sizeY, sizeZ, material, style, coating, mode</summary>
        EmitBox = 1,

        /// <summary>centreX, baseY, centreZ, radius, height, axis, material, style, coating, mode</summary>
        EmitCylinder = 2,

        /// <summary>minX, minY, minZ, sizeX, sizeY, sizeZ, profile, material, style, coating, mode</summary>
        EmitPrism = 3,

        /// <summary>ax, ay, az, bx, by, bz, radius, material, style, coating, mode</summary>
        EmitCapsule = 4,

        /// <summary>minX, minY, minZ, sizeX, sizeY, sizeZ, axis, material, style, coating, mode</summary>
        EmitRamp = 5,

        /// <summary>minX, minY, minZ, sizeX, sizeY, sizeZ, radius, material, style, coating, mode</summary>
        EmitRoundedBox = 6,

        /// <summary>centreX, centreY, centreZ, radiusX, radiusY, radiusZ, material, style, coating, mode</summary>
        EmitEllipsoid = 7,

        /// <summary>baseX, baseY, baseZ, height, baseRadius, topRadius, axis, material, style, coating, mode</summary>
        EmitFrustum = 8,

        /// <summary>centreX, centreY, centreZ, outerRadius, innerRadius, depth, axis, half, material, style, coating, mode</summary>
        EmitAnnulus = 9,

        /// <summary>centreX, centreY, centreZ, outerRadius, innerRadius, depth, axis, startX, startY, endX, endY, material, style, coating, mode</summary>
        EmitArcWedge = 10,

        // -- control ------------------------------------------------------------

        /// <summary>count, strideX, strideY, strideZ, bodyInstructionCount</summary>
        Repeat = 11,

        /// <summary>register, min, max, bodyInstructionCount</summary>
        IfRange = 12,

        /// <summary>offsetX, offsetY, offsetZ</summary>
        PushTransform = 13,

        /// <summary>(none)</summary>
        PopTransform = 14,

        /// <summary>slotIndex</summary>
        CallSlot = 15,

        // -- query --------------------------------------------------------------

        /// <summary>destRegister, offsetX, offsetZ</summary>
        SampleGround = 16,

        /// <summary>destRegister, min, max, quantum</summary>
        DrawRange = 17,

        /// <summary>anchorIndex, x, y, z, facing</summary>
        SetAnchor = 18,

        /// <summary>destRegister, valueA, valueB, operation</summary>
        Arithmetic = 19,

        // -- terrain ------------------------------------------------------------

        /// <summary>
        /// ax, ay, az, bx, by, bz, coreRadius, gradingRadius, maximumCutFill,
        /// fillDepth, clearAbove, edgeVariation, material, seedLow31,
        /// packedSurfaceOuterAndScale.
        ///
        /// Emits one bounded terrain-following corridor. A and B are already-resolved target
        /// elevations; rasterisation blends the existing column surface toward that target using
        /// a bounded grading influence while the independently encoded authored surface radius
        /// controls material/detail coverage. Legacy callers may still pass a plain scale in the
        /// final operand, preserving the original single-influence corridor contract.
        /// </summary>
        EmitTerrainCorridor = 20,
    }

    /// <summary>Operations available to <see cref="ShapeOp.Arithmetic"/>. Integer only.</summary>
    public enum ArithmeticOp
    {
        Add = 0,
        Subtract = 1,
        Multiply = 2,

        /// <summary>Truncating division. Division by zero yields zero rather than trapping.</summary>
        Divide = 3,

        Min = 4,
        Max = 5,
    }

    /// <summary>Instruction metadata and the register layout.</summary>
    public static class ShapeOps
    {
        public const int RegisterCount = 32;
        public const int FirstParameterRegister = 0;
        public const int RegisterBase = 16;
        public const int RegisterSlot = 17;
        public const int FirstScratchRegister = 18;
        public const int MaxTransformDepth = 8;
        public const byte RampAxisMask = 0x7F;
        public const byte ReverseRampBit = 0x80;

        private const int TerrainCorridorPackedMarker = 1 << 30;
        private const int TerrainCorridorPackedFieldMask = (1 << 15) - 1;

        private static readonly int[] Operands =
        {
            0, // End
            10, // EmitBox
            10, // EmitCylinder
            11, // EmitPrism
            11, // EmitCapsule
            11, // EmitRamp
            11, // EmitRoundedBox
            10, // EmitEllipsoid
            11, // EmitFrustum
            12, // EmitAnnulus
            15, // EmitArcWedge
            5, // Repeat
            4, // IfRange
            3, // PushTransform
            0, // PopTransform
            1, // CallSlot
            3, // SampleGround
            4, // DrawRange
            5, // SetAnchor
            4, // Arithmetic
            15, // EmitTerrainCorridor
        };

        public static int OperandCount(ShapeOp op)
        {
            int index = (int)op;
            return (uint)index < (uint)Operands.Length ? Operands[index] : -1;
        }

        public static int InstructionLength(ShapeOp op)
        {
            int operands = OperandCount(op);
            return operands < 0 ? -1 : 2 + operands;
        }

        public static bool IsEmit(ShapeOp op) =>
            op >= ShapeOp.EmitBox && op <= ShapeOp.EmitArcWedge
            || op == ShapeOp.EmitTerrainCorridor;

        /// <summary>
        /// Packs the authored visible-surface outer radius and authored-grid scale into the
        /// existing final terrain-corridor operand. The marker leaves legacy plain-scale programs
        /// unambiguous and avoids growing the bytecode or primitive storage contract.
        /// </summary>
        public static int PackTerrainCorridorSurfaceOuterAndScale(int surfaceOuterRadius, int scale)
        {
            if (surfaceOuterRadius < 1 || surfaceOuterRadius > TerrainCorridorPackedFieldMask)
                throw new ArgumentOutOfRangeException(nameof(surfaceOuterRadius));
            if (scale < 1 || scale > TerrainCorridorPackedFieldMask)
                throw new ArgumentOutOfRangeException(nameof(scale));
            return TerrainCorridorPackedMarker | (surfaceOuterRadius << 15) | scale;
        }

        public static bool HasPackedTerrainCorridorSurfaceOuter(int packedOrLegacyScale)
            => (packedOrLegacyScale & TerrainCorridorPackedMarker) != 0;

        public static int TerrainCorridorScale(int packedOrLegacyScale)
            => HasPackedTerrainCorridorSurfaceOuter(packedOrLegacyScale)
                ? packedOrLegacyScale & TerrainCorridorPackedFieldMask
                : Math.Max(1, packedOrLegacyScale);

        public static int TerrainCorridorSurfaceOuterRadius(
            int packedOrLegacyScale,
            int legacyOuterRadius)
            => HasPackedTerrainCorridorSurfaceOuter(packedOrLegacyScale)
                ? (packedOrLegacyScale >> 15) & TerrainCorridorPackedFieldMask
                : legacyOuterRadius;
    }
}
