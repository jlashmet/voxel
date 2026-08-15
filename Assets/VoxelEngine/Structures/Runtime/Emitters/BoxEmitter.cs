using Unity.Mathematics;

using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime.Emitters
{
    /// <summary>
    /// Boxes and ramps.
    ///
    /// Construction and membership live together deliberately: the rasteriser asks "is this voxel
    /// inside?", and if that test lived apart from the emitter the two could disagree about what
    /// the primitive means. A disagreement there is a wall with a hole in it that no test written
    /// against either half would catch.
    /// </summary>
    public static class BoxEmitter
    {
        public static Primitive Box(int3 min, int3 size, byte material, PrimitiveMode mode, int order,
                                    ushort surfaceStyle = 0, byte coating = 0)
        {
            return new Primitive
            {
                Shape = PrimitiveShape.Box,
                Mode = mode,
                Material = material,
                SurfaceStyle = surfaceStyle,
                Coating = coating,
                Order = order,
                A = min,
                B = min + math.max(size, new int3(1, 1, 1)) - 1,
            };
        }

        /// <summary>
        /// A wedge rising along <paramref name="axis"/>. Stairs, terrain skirts, buttresses.
        /// OR <see cref="ShapeOps.ReverseRampBit"/> into the axis to make it rise toward the negative end.
        /// </summary>
        public static Primitive Ramp(int3 min, int3 size, byte axis, byte material,
                                     PrimitiveMode mode, int order,
                                     ushort surfaceStyle = 0, byte coating = 0)
        {
            return new Primitive
            {
                Shape = PrimitiveShape.Ramp,
                Mode = mode,
                Material = material,
                SurfaceStyle = surfaceStyle,
                Coating = coating,
                Axis = (byte)(axis & ShapeOps.RampAxisMask),
                Direction = (sbyte)((axis & ShapeOps.ReverseRampBit) != 0 ? -1 : 1),
                Order = order,
                A = min,
                B = min + math.max(size, new int3(1, 1, 1)) - 1,
            };
        }

        public static bool BoxContains(in Primitive p, int3 voxel)
        {
            // Bounds are inclusive, so a one-voxel box is min == max rather than empty.
            return voxel.x >= p.A.x && voxel.x <= p.B.x
                && voxel.y >= p.A.y && voxel.y <= p.B.y
                && voxel.z >= p.A.z && voxel.z <= p.B.z;
        }

        /// <summary>
        /// Height rises linearly along the ramp's axis. Integer throughout: the multiply happens
        /// before the divide so the slope is exact rather than accumulating truncation.
        /// </summary>
        public static bool RampContains(in Primitive p, int3 voxel)
        {
            if (!BoxContains(in p, voxel)) return false;

            int axis = p.Axis & ShapeOps.RampAxisMask;

            int along = axis == 0 ? voxel.x - p.A.x
                      : axis == 2 ? voxel.z - p.A.z
                      : voxel.y - p.A.y;

            int span = axis == 0 ? p.B.x - p.A.x
                     : axis == 2 ? p.B.z - p.A.z
                     : p.B.y - p.A.y;

            if (span <= 0) return true;
            if (p.Direction < 0) along = span - along;

            int height = p.B.y - p.A.y + 1;
            int allowed = ((along + 1) * height) / (span + 1);

            return voxel.y - p.A.y < allowed;
        }
    }
}
