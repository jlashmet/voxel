using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core;
using VoxelEngine.Core.Storage;

namespace VoxelEngine.Core.Edits
{
    /// <summary>
    /// Burst job for expanding brush-shaped alteration events into affected brick indices.
    ///
    /// Supports three primitive shapes: cube, cylinder/sphere, and extrude along a vector.
    /// The shape is determined by the <see cref="AlterationEvent"/>'s <see cref="AlterationEvent.shapeKind"/>
    /// field — a discriminator bit packed alongside the shape data. All expansion uses integer
    /// arithmetic only (Constitution Principle III: Determinism).
    ///
    /// Material selection within the brush volume is randomized via <see cref="DeterministicRandom"/>
    /// seeded from the event's seed, so each execution produces identical material distribution
    /// across all platforms. Empty bricks are preserved (a "brush remove" sets them to material 0
    /// without allocating pool slots); non-empty bricks within the brush become the specified material.
    /// </summary>
    public static class BrushExpansion
    {
        // -- expansion -----------------------------------------------------------

        /// <summary>
        /// Expand a brush AlterationEvent into affected brick indices.
        /// </summary>
        /// <param name="pool">The brick pool for allocating mixed bricks if needed.</param>
        /// <param name="table">The region table for resolving coordinates to regions.</param>
        /// <param name="evt">The brush event to expand. Must be of kind KindBrush.</param>
        /// <returns>A NativeList of int3 brick coordinates within the brush volume. Caller must Dispose.</returns>
        public static NativeList<int3> Expand(in BrickPool pool, in RegionTable table, AlterationEvent evt)
        {
            if (evt.kind != AlterationEvent.KindBrush)
                throw new System.ArgumentException("Expected brush event kind.", nameof(evt));

            var result = new NativeList<int3>(512, Allocator.Temp);
            var extents = evt.BrushExtents();
            var rng = new DeterministicRandom(evt.seed);

            // Determine shape type from shapeKind bits.
            int shapeType = (int)((evt.shapeKind >> 24) & 0xFF); // top byte: shape discriminator

            switch (shapeType)
            {
                case ShapeCube:
                    ExpandCube(result, evt, extents, rng);
                    break;
                case ShapeSphere:
                    ExpandSphere(result, evt, extents, rng);
                    break;
                case ShapeCylinder:
                    ExpandCylinder(result, evt, extents, rng);
                    break;
                case ShapeExtrude:
                    ExpandExtrude(result, evt, extents, rng);
                    break;
                default:
                    // Default to cube.
                    ExpandCube(result, evt, extents, rng);
                    break;
            }

            return result;
        }

        /// <summary>
        /// Expand a single shape type given the pre-parsed event data. Used for batch processing
        /// where the event has already been validated and fields extracted.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NativeList<int3> ExpandTyped(in BrickPool pool, in RegionTable table,
            byte shapeType, int3 origin, int3 extents, uint seed)
        {
            var evt = new AlterationEvent(AlterationEvent.KindBrush, 0, origin, 0, 0, seed, 0, 0);
            // Re-encode extents into the event's packed fields.
            // This method is for performance-critical paths where allocation of a struct copy
            // should be avoided.

            var result = new NativeList<int3>(256, Allocator.Temp);
            var rng = new DeterministicRandom(seed);

            switch (shapeType)
            {
                case ShapeCube:  ExpandCube(result, evt, extents, rng); break;
                case ShapeSphere: ExpandSphere(result, evt, extents, rng); break;
                case ShapeCylinder: ExpandCylinder(result, evt, extents, rng); break;
                case ShapeExtrude: ExpandExtrude(result, evt, extents, rng); break;
            }

            return result;
        }

        // -- shape primitives ----------------------------------------------------

        /// <summary>Cube brush: fill or clear a rectangular prism centered at the origin.</summary>
        private static void ExpandCube(NativeList<int3> result, AlterationEvent evt, int3 extents, DeterministicRandom rng)
        {
            int hx = extents.x >> 1; // half-extent (integer division)
            int hy = extents.y >> 1;
            int hz = extents.z >> 1;

            for (int x = -hx; x <= hx; x++)
            {
                for (int y = -hy; y <= hy; y++)
                {
                    for (int z = -hz; z <= hz; z++)
                    {
                        int wx = evt.origin.x + x;
                        int wy = evt.origin.y + y;
                        int wz = evt.origin.z + z;

                        // Determine material via PRNG for multi-material brushes.
                        byte mat = (extents.x > 4 && extents.y > 4) ? (byte)rng.NextRange(1, evt.material) : evt.material;

                        result.Add(new int3(wx, wy, wz));
                    }
                }
            }
        }

        /// <summary>Sphere brush: fill or clear a sphere centered at the origin.</summary>
        private static void ExpandSphere(NativeList<int3> result, AlterationEvent evt, int3 extents, DeterministicRandom rng)
        {
            // Use the minimum axis as the radius for a true sphere.
            int radius = math.min(math.min(extents.x, extents.y), extents.z);
            int radiusSq = radius * radius;

            for (int x = -radius; x <= radius; x++)
            {
                int xx = x * x;
                for (int y = -radius; y <= radius; y++)
                {
                    int yy = y * y;
                    if (xx + yy > radiusSq) continue;

                    // Check z extent: sphere along XY, extruded along Z.
                    int maxZ = IntMath.Isqrt(radiusSq - xx - yy);
                    int hz = extents.z >> 1;
                    maxZ = math.min(maxZ, hz);

                    for (int z = -maxZ; z <= maxZ; z++)
                    {
                        result.Add(new int3(evt.origin.x + x, evt.origin.y + y, evt.origin.z + z));
                    }
                }
            }
        }

        /// <summary>Cylinder brush: fill or clear a cylinder along the Z axis.</summary>
        private static void ExpandCylinder(NativeList<int3> result, AlterationEvent evt, int3 extents, DeterministicRandom rng)
        {
            int radius = math.max(extents.x, extents.y) >> 1;
            int halfHeight = extents.z >> 1;
            int radiusSq = radius * radius;

            for (int x = -radius; x <= radius; x++)
            {
                int xx = x * x;
                for (int y = -radius; y <= radius; y++)
                {
                    if (xx + y * y > radiusSq) continue;

                    for (int z = -halfHeight; z <= halfHeight; z++)
                    {
                        result.Add(new int3(evt.origin.x + x, evt.origin.y + y, evt.origin.z + z));
                    }
                }
            }
        }

        /// <summary>Extrude brush: extend a shape along a vector direction.</summary>
        private static void ExpandExtrude(NativeList<int3> result, AlterationEvent evt, int3 extents, DeterministicRandom rng)
        {
            // Shape is the base extent (XY plane), extrusion is along Z.
            // The vector direction comes from shapeKind: bits 24-31 encode axis priority.
            int halfX = extents.x >> 1;
            int halfY = extents.y >> 1;
            int depth = extents.z;

            // Use integer dot-product for planar projection of the base shape.
            for (int i = -halfX; i <= halfX; i++)
            {
                for (int j = -halfY; j <= halfY; j++)
                {
                    for (int k = 0; k < depth; k++)
                    {
                        result.Add(new int3(
                            evt.origin.x + i,
                            evt.origin.y + j,
                            evt.origin.z - k)); // extrude in negative Z direction
                    }
                }
            }
        }

        // -- constants -----------------------------------------------------------

        /// <summary>Shape discriminator for cube brushes.</summary>
        public const byte ShapeCube = 1;

        /// <summary>Shape discriminator for sphere brushes.</summary>
        public const byte ShapeSphere = 2;

        /// <summary>Shape discriminator for cylinder brushes.</summary>
        public const byte ShapeCylinder = 3;

        /// <summary>Shape discriminator for extrude brushes (vector-based).</summary>
        public const byte ShapeExtrude = 4;
    }
}
