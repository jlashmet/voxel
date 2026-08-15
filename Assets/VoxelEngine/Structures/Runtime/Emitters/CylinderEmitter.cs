using Unity.Mathematics;

using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime.Emitters
{
    /// <summary>Cylinders on a cardinal axis: towers, wells, pillars, tunnels with square ends.</summary>
    public static class CylinderEmitter
    {
        public static Primitive Cylinder(int3 centre, int radius, int height, byte axis,
                                         byte material, PrimitiveMode mode, int order,
                                         ushort surfaceStyle = 0, byte coating = 0)
        {
            if (radius < 0) radius = 0;
            if (height < 1) height = 1;

            int3 min, max;

            switch (axis)
            {
                case 0: // along x
                    min = new int3(centre.x, centre.y - radius, centre.z - radius);
                    max = new int3(centre.x + height - 1, centre.y + radius, centre.z + radius);
                    break;
                case 2: // along z
                    min = new int3(centre.x - radius, centre.y - radius, centre.z);
                    max = new int3(centre.x + radius, centre.y + radius, centre.z + height - 1);
                    break;
                default: // along y
                    min = new int3(centre.x - radius, centre.y, centre.z - radius);
                    max = new int3(centre.x + radius, centre.y + height - 1, centre.z + radius);
                    break;
            }

            return new Primitive
            {
                Shape = PrimitiveShape.Cylinder,
                Mode = mode,
                Material = material,
                SurfaceStyle = surfaceStyle,
                Coating = coating,
                Axis = axis,
                Order = order,
                A = min,
                B = max,
                Radius = radius,
            };
        }

        /// <summary>
        /// Squared integer distance from the axis, compared against the squared radius.
        ///
        /// Squared comparison rather than a square root: exact, cheap, and free of the
        /// platform-dependent rounding a float sqrt would introduce into a value that decides
        /// what the world contains.
        /// </summary>
        public static bool Contains(in Primitive p, int3 voxel)
        {
            if (!BoxEmitter.BoxContains(in p, voxel)) return false;

            int du, dv;

            switch (p.Axis)
            {
                case 0:
                    du = voxel.y - (p.A.y + p.Radius);
                    dv = voxel.z - (p.A.z + p.Radius);
                    break;
                case 2:
                    du = voxel.x - (p.A.x + p.Radius);
                    dv = voxel.y - (p.A.y + p.Radius);
                    break;
                default:
                    du = voxel.x - (p.A.x + p.Radius);
                    dv = voxel.z - (p.A.z + p.Radius);
                    break;
            }

            return du * du + dv * dv <= p.Radius * p.Radius;
        }
    }
}
