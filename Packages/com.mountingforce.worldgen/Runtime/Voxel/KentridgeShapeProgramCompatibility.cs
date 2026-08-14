using System;
using Unity.Collections;
using VoxelEngine.Core.Features;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Expands early Kentridge shape programs that omitted the surface-style and coating operands
    /// added to emit instructions. Current programs pass through unchanged. This compatibility seam
    /// keeps the combined catalogue canonical while individual authoring helpers are migrated.
    /// </summary>
    internal static class KentridgeShapeProgramCompatibility
    {
        private enum Encoding : byte
        {
            Canonical,
            ShortEmit,
        }

        public static int CanonicalLength(in FeatureCatalogue source)
        {
            int total = 0;
            for (int i = 0; i < source.Definitions.Length; i++)
            {
                FeatureDefinition definition = source.Definitions[i];
                if (definition.ProgramLength <= 0) continue;
                Detect(source.Program, definition.ProgramOffset, definition.ProgramLength,
                       definition.Name.ToString(), out int length);
                total += length;
            }
            return total;
        }

        public static int CopyDefinition(
            NativeArray<int> source,
            int start,
            int length,
            NativeArray<int> target,
            int targetStart,
            string definitionName)
        {
            Encoding encoding = Detect(source, start, length, definitionName,
                                       out int canonicalLength);
            if (encoding == Encoding.Canonical)
            {
                for (int i = 0; i < length; i++)
                    target[targetStart + i] = source[start + i];
                return length;
            }

            int pc = start;
            int end = start + length;
            int write = targetStart;
            while (pc < end)
            {
                ShapeOp op = (ShapeOp)source[pc];
                int canonicalOperands = ShapeOps.OperandCount(op);
                int shortOperands = ShapeOps.IsEmit(op)
                    ? canonicalOperands - 2
                    : canonicalOperands;
                int instructionLength = 2 + shortOperands;

                if (!ShapeOps.IsEmit(op))
                {
                    for (int i = 0; i < instructionLength; i++)
                        target[write++] = source[pc + i];
                }
                else
                {
                    int shortMask = source[pc + 1];
                    int shortModeIndex = shortOperands - 1;
                    int canonicalModeIndex = canonicalOperands - 1;
                    int beforeModeMask = (1 << shortModeIndex) - 1;
                    int canonicalMask = shortMask & beforeModeMask;
                    if ((shortMask & (1 << shortModeIndex)) != 0)
                        canonicalMask |= 1 << canonicalModeIndex;

                    target[write++] = source[pc];
                    target[write++] = canonicalMask;
                    for (int operand = 0; operand < shortModeIndex; operand++)
                        target[write++] = source[pc + 2 + operand];
                    target[write++] = 0;
                    target[write++] = 0;
                    target[write++] = source[pc + 2 + shortModeIndex];
                }

                pc += instructionLength;
                if (op == ShapeOp.End) break;
            }

            int written = write - targetStart;
            if (written != canonicalLength)
                throw new InvalidOperationException(
                    "Kentridge bytecode normalization length mismatch for " + definitionName + ".");
            return written;
        }

        private static Encoding Detect(
            NativeArray<int> program,
            int start,
            int length,
            string definitionName,
            out int canonicalLength)
        {
            if (TryMeasure(program, start, length, true,
                           out canonicalLength, out int shortEmitCount)
                && shortEmitCount > 0)
                return Encoding.ShortEmit;

            if (TryMeasure(program, start, length, false,
                           out canonicalLength, out _))
                return Encoding.Canonical;

            throw new InvalidOperationException(
                "Kentridge definition contains malformed shape bytecode: " + definitionName + ".");
        }

        private static bool TryMeasure(
            NativeArray<int> program,
            int start,
            int length,
            bool shortEmit,
            out int canonicalLength,
            out int emitCount)
        {
            canonicalLength = 0;
            emitCount = 0;
            int end = start + length;
            if (start < 0 || length < 2 || end > program.Length) return false;

            int pc = start;
            while (pc < end)
            {
                if (pc + 1 >= end) return false;
                ShapeOp op = (ShapeOp)program[pc];
                int canonicalOperands = ShapeOps.OperandCount(op);
                if (canonicalOperands < 0) return false;

                bool emit = ShapeOps.IsEmit(op);
                int operands = shortEmit && emit
                    ? canonicalOperands - 2
                    : canonicalOperands;
                if (operands < 0 || pc + 2 + operands > end) return false;

                if (emit)
                {
                    int modeIndex = operands - 1;
                    int mask = program[pc + 1];
                    bool modeIsRegister = (mask & (1 << modeIndex)) != 0;
                    int mode = program[pc + 2 + modeIndex];
                    if (!modeIsRegister
                        && (mode < (int)PrimitiveMode.Fill
                            || mode > (int)PrimitiveMode.SurfaceDetail))
                        return false;
                    emitCount++;
                }

                pc += 2 + operands;
                canonicalLength += 2 + canonicalOperands;
                if (op == ShapeOp.End) return pc == end;
            }

            return false;
        }
    }
}
