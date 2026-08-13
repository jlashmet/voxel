using Unity.Mathematics;

namespace VoxelEngine.Core.Features.Emitters
{
    /// <summary>
    /// Capsules — the segment between two points, thickened.
    ///
    /// Cave tunnels are chains of these. A chain is emitted as one capsule per segment rather than
    /// as a single primitive, so each piece clips to a sub-volume independently and a tunnel
    /// crossing a region boundary costs each region only the segments that reach it.
    /// </summary>
    public static class CapsuleChainEmitter
    {
        public static Primitive Capsule(int3 a, int3 b, int radius, byte material,
                                        PrimitiveMode mode, int order,
                                        ushort surfaceStyle = 0, byte coating = 0)
        {
            return new Primitive
            {
                Shape = PrimitiveShape.Capsule,
                Mode = mode,
                Material = material,
                SurfaceStyle = surfaceStyle,
                Coating = coating,
                Order = order,
                A = a,
                B = b,
                Radius = radius < 0 ? 0 : radius,
            };
        }

        /// <summary>
        /// Squared distance from the voxel to the segment, against the squared radius.
        ///
        /// The projection parameter is kept as a rational — numerator and denominator — rather
        /// than divided out, so the whole test stays in integers. Dividing early would round the
        /// closest point onto a lattice and leave visible facets along a diagonal tunnel.
        /// </summary>
        public static bool Contains(in Primitive p, int3 voxel) =>
            ContainsQ4(in p, voxel, p.Radius << 4);

        /// <summary>
        /// Fixed-point capsule membership for sub-voxel strokes such as mortar, engraving and
        /// cracks. Q4 keeps generation deterministic while allowing a half-voxel line to select
        /// the nearest connected lattice cells. Ordinary capsules call this with integer radii.
        /// </summary>
        public static bool ContainsQ4(in Primitive p, int3 voxel, int radiusQ4)
        {
            long abx = p.B.x - p.A.x, aby = p.B.y - p.A.y, abz = p.B.z - p.A.z;
            long apx = voxel.x - p.A.x, apy = voxel.y - p.A.y, apz = voxel.z - p.A.z;

            long abLengthSq = abx * abx + aby * aby + abz * abz;
            long dot = apx * abx + apy * aby + apz * abz;

            long closestX, closestY, closestZ;

            if (abLengthSq == 0 || dot <= 0)
            {
                closestX = p.A.x; closestY = p.A.y; closestZ = p.A.z;
            }
            else if (dot >= abLengthSq)
            {
                closestX = p.B.x; closestY = p.B.y; closestZ = p.B.z;
            }
            else
            {
                // Scaled comparison: compare distances multiplied by abLengthSq^2 so the
                // projection never needs to be rounded to a voxel.
                long px = p.A.x * abLengthSq + abx * dot;
                long py = p.A.y * abLengthSq + aby * dot;
                long pz = p.A.z * abLengthSq + abz * dot;

                long dx = voxel.x * abLengthSq - px;
                long dy = voxel.y * abLengthSq - py;
                long dz = voxel.z * abLengthSq - pz;

                long distSq = dx * dx + dy * dy + dz * dz;
                long radiusSq = (long)radiusQ4 * radiusQ4
                              * abLengthSq * abLengthSq / 256;

                return distSq <= radiusSq;
            }

            long ex = voxel.x - closestX, ey = voxel.y - closestY, ez = voxel.z - closestZ;
            return (ex * ex + ey * ey + ez * ez) * 256
                <= (long)radiusQ4 * radiusQ4;
        }
    }
}
