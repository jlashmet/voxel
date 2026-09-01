using System;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Small deterministic composition helpers for already-compiled shape programs. These helpers
    /// only add bounded structured-control instructions; they never inspect world state or voxel data.
    /// </summary>
    public static class ShapeProgramComposition
    {
        /// <summary>
        /// Wraps a complete program in one local translation. The source program must terminate in
        /// End. Existing source instructions remain byte-for-byte unchanged inside the wrapper.
        /// </summary>
        public static int[] Translate(int[] source, int3 offset)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (source.Length < 2 || source[source.Length - 2] != (int)ShapeOp.End)
                throw new ArgumentException("Shape program must terminate in End.", nameof(source));

            int pushLength = ShapeOps.InstructionLength(ShapeOp.PushTransform);
            int popLength = ShapeOps.InstructionLength(ShapeOp.PopTransform);
            int endLength = ShapeOps.InstructionLength(ShapeOp.End);
            int sourceWithoutEnd = source.Length - endLength;
            var result = new int[pushLength + sourceWithoutEnd + popLength + endLength];

            int p = 0;
            result[p++] = (int)ShapeOp.PushTransform;
            result[p++] = 0;
            result[p++] = offset.x;
            result[p++] = offset.y;
            result[p++] = offset.z;

            Array.Copy(source, 0, result, p, sourceWithoutEnd);
            p += sourceWithoutEnd;

            result[p++] = (int)ShapeOp.PopTransform;
            result[p++] = 0;
            result[p++] = (int)ShapeOp.End;
            result[p] = 0;
            return result;
        }
    }
}
