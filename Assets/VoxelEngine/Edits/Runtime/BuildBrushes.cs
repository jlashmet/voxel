using System;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Foundation;

namespace VoxelEngine.Edits.Runtime
{
    /// <summary>
    /// Generative build brushes that place voxels in various shapes.
    /// These are the inverse of explosion: instead of removing material, they add it.
    /// All geometry is integer-based (Constitution Principle I: Determinism).
    ///
    /// Each brush expands a compact description (center, radius/size, material, seed) into
    /// an explicit list of voxel coordinates to write, mirroring how <see cref="ExplosionExpansion"/>
    /// expands destruction events. The seeded PRNG drives any stochastic variation so that
    /// the same input always produces identical output across all clients and platforms.
    /// </summary>
    public static class BuildBrushes
    {
        // -- public brush APIs ----------------------------------------------------

        /// <summary>
        /// Cube brush: places voxels in a rectangular box defined by min corner and size.
        /// Every voxel coordinate within the AABB is included — no interior exclusion.
        /// </summary>
        /// <param name="minCorner">Minimum corner of the cube in voxel coordinates.</param>
        /// <param name="size">Size of the cube along each axis (all axes same size for a true cube).</param>
        /// <param name="material">Material index to place. 0 = empty (demolition via brush).</param>
        /// <param name="seed">PRNG seed for deterministic variation. Unused by pure cube shape;
        ///   included so all brushes share the same signature and callers can use one expansion path.</param>
        /// <returns>A NativeList of int3 voxel coordinates inside the cube. Caller must Dispose.</returns>
        public static NativeList<int3> PlaceCube(int3 minCorner, int size, byte material, uint seed)
        {
            if (size <= 0)
                return new NativeList<int3>(0, Allocator.Temp);

            var result = new NativeList<int3>(size * size * size, Allocator.Temp);

            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    for (int z = 0; z < size; z++)
                    {
                        result.Add(minCorner + new int3(x, y, z));
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Sphere brush: places voxels within the given radius of center using integer spherical iteration.
        /// Only bricks whose integer centers lie strictly within the sphere volume are included.
        /// </summary>
        /// <param name="center">Center of the sphere in voxel coordinates.</param>
        /// <param name="radius">Radius in voxels. Must be > 0.</param>
        /// <param name="material">Material index to place. 0 = empty (demolition via brush).</param>
        /// <param name="seed">PRNG seed for deterministic variation in sub-brick displacement (if any).
        ///   Used to produce identical voxel lists across clients.</param>
        /// <returns>A NativeList of int3 voxel coordinates within the sphere. Caller must Dispose.</returns>
        public static NativeList<int3> PlaceSphere(int3 center, ushort radius, byte material, uint seed)
        {
            if (radius == 0)
                return new NativeList<int3>(0, Allocator.Temp);

            int radiusSquared = radius * radius;
            var result = new NativeList<int3>(math.min(radiusSquared * 4, 65536), Allocator.Temp);

            // Iterate over the bounding box of the sphere using integer arithmetic.
            for (int x = -radius; x <= radius; x++)
            {
                int remainingX = radiusSquared - x * x;

                for (int y = -radius; y <= radius; y++)
                {
                    int remainingXY = remainingX - y * y;
                    if (remainingXY < 0) continue;

                    int maxZ = IntMath.Isqrt(remainingXY);

                    for (int z = -maxZ; z <= maxZ; z++)
                    {
                        result.Add(center + new int3(x, y, z));
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Cylinder brush: vertical cylinder along the Y axis, centered at baseCenter.xz.
        /// The cylinder extends upward from baseCenter.y by <paramref name="height"/> voxels.
        /// </summary>
        /// <param name="baseCenter">Center of the cylinder's base in voxel coordinates.
        ///   XZ defines the circular cross-section; Y defines the bottom face.</param>
        /// <param name="radius">Radius of the cylinder in voxels. Must be > 0.</param>
        /// <param name="height">Height of the cylinder in voxels (along +Y). Must be >= 1.</param>
        /// <param name="material">Material index to place. 0 = empty (demolition via brush).</param>
        /// <param name="seed">PRNG seed for deterministic variation in sub-brick placement.</param>
        /// <returns>A NativeList of int3 voxel coordinates within the cylinder volume. Caller must Dispose.</returns>
        public static NativeList<int3> PlaceCylinder(int3 baseCenter, ushort radius, int height, byte material, uint seed)
        {
            if (radius == 0 || height <= 0)
                return new NativeList<int3>(0, Allocator.Temp);

            int radiusSquared = radius * radius;
            int estimatedCapacity = math.min(IntMath.MulDiv(radiusSquared * height, 7854, 10000), 131072);
            var result = new NativeList<int3>(estimatedCapacity, Allocator.Temp);

            for (int y = 0; y < height; y++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    int remainingX = radiusSquared - x * x;

                    for (int z = -radius; z <= radius; z++)
                    {
                        if (remainingX - z * z < 0) continue;

                        result.Add(baseCenter + new int3(x, y, z));
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Line brush: single-file voxel line from origin using Bresenham's algorithm along the direction vector.
        /// This produces a 1-voxel-wide path — suitable for building fences, beams, or edges.
        /// </summary>
        /// <param name="start">Starting voxel coordinate of the line in voxel coordinates.</param>
        /// <param name="direction">Direction as an integer vector. Only its direction matters;
        ///   magnitude is ignored and the line is walked <paramref name="length"/> voxels.
        ///   Integer rather than float3 by design: this determines which voxels are written,
        ///   and world state must not derive from floating-point (Constitution Principle I).</param>
        /// <param name="length">Effective length in voxels along the dominant axis.</param>
        /// <param name="material">Material index to place. 0 = empty (demolition via brush).</param>
        /// <param name="seed">PRNG seed for deterministic variation in sub-brick jitter (if enabled).
        ///   Unused by the pure line — included for signature consistency.</param>
        /// <returns>A NativeList of int3 voxel coordinates along the line. Caller must Dispose.</returns>
        public static NativeList<int3> PlaceLine(int3 start, int3 direction, byte length, byte material, uint seed)
        {
            if (length == 0)
                return new NativeList<int3>(0, Allocator.Temp);

            var result = new NativeList<int3>(math.max((int)length, 1), Allocator.Temp);

            int absDx = math.abs(direction.x);
            int absDy = math.abs(direction.y);
            int absDz = math.abs(direction.z);

            // Degenerate direction: walk straight up, matching the documented fallback.
            if (absDx == 0 && absDy == 0 && absDz == 0)
            {
                for (int i = 0; i < length; i++)
                    result.Add(start + new int3(0, i, 0));
                return result;
            }

            int sx = direction.x >= 0 ? 1 : -1;
            int sy = direction.y >= 0 ? 1 : -1;
            int sz = direction.z >= 0 ? 1 : -1;

            // 3D Bresenham driven by the dominant axis. Two error accumulators track the
            // minor axes; each is incremented by the minor delta and steps when it passes
            // half the dominant delta. All integer, so every machine walks the same voxels.
            int3 current = start;
            int dominant = math.max(absDx, math.max(absDy, absDz));
            int steps = math.min((int)length, dominant + 1);

            if (absDx == dominant)
            {
                int errY = 2 * absDy - absDx;
                int errZ = 2 * absDz - absDx;

                for (int i = 0; i < steps; i++)
                {
                    result.Add(current);

                    if (errY > 0) { current.y += sy; errY -= 2 * absDx; }
                    if (errZ > 0) { current.z += sz; errZ -= 2 * absDx; }
                    errY += 2 * absDy;
                    errZ += 2 * absDz;
                    current.x += sx;
                }
            }
            else if (absDy == dominant)
            {
                int errX = 2 * absDx - absDy;
                int errZ = 2 * absDz - absDy;

                for (int i = 0; i < steps; i++)
                {
                    result.Add(current);

                    if (errX > 0) { current.x += sx; errX -= 2 * absDy; }
                    if (errZ > 0) { current.z += sz; errZ -= 2 * absDy; }
                    errX += 2 * absDx;
                    errZ += 2 * absDz;
                    current.y += sy;
                }
            }
            else
            {
                int errX = 2 * absDx - absDz;
                int errY = 2 * absDy - absDz;

                for (int i = 0; i < steps; i++)
                {
                    result.Add(current);

                    if (errX > 0) { current.x += sx; errX -= 2 * absDz; }
                    if (errY > 0) { current.y += sy; errY -= 2 * absDz; }
                    errX += 2 * absDx;
                    errY += 2 * absDy;
                    current.z += sz;
                }
            }

            return result;
        }
    }
}
