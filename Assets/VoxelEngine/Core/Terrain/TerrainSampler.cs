using System.Runtime.CompilerServices;

namespace VoxelEngine.Core.Terrain
{
    /// <summary>
    /// The canonical terrain surface function: integer, world-continuous, and a pure function of
    /// world coordinates.
    ///
    /// Everything that needs to know where the ground is reads it here — placement rules, terrain
    /// adaptation, the generator, and the renderer's far field. One function, so they cannot
    /// disagree.
    ///
    /// The "pure function of world coordinates" part is not a stylistic preference. Regions stream
    /// in any order and regenerate after eviction, so a region must derive the ground under a
    /// structure that mostly lives in a neighbour it has never seen. That is only possible if
    /// height at a point depends on nothing but the point and the seed.
    ///
    /// This replaces the sampler that reduced its inputs modulo the region edge, which produced
    /// *identical terrain in every region*. Nothing caught it because every determinism test
    /// compared a region against itself; terrain that repeats is perfectly deterministic.
    ///
    /// Integer throughout (Constitution I): a float here diverges between platforms by a voxel
    /// somewhere, and no single client can detect that its ground disagrees with everyone else's.
    /// </summary>
    public static class TerrainSampler
    {
        /// <summary>Base surface height in voxels, before octaves.</summary>
        public const int BaseHeight = 220;

        /// <summary>Lowest and highest surface the sampler will return, in voxels.</summary>
        public const int MinHeight = 8;

        /// <summary>
        /// 6 km of relief at 10 cm voxels. The ceiling is not a region-layer limit: residency
        /// follows the surface manifold rather than filling a vertical column, so height costs
        /// generation work, not memory.
        ///
        /// Set deliberately above the tallest peak the octaves below can produce (~5 km). A
        /// clamp that the terrain actually reaches would shear every summit into a flat mesa at
        /// exactly one altitude, which reads as a bug from any distance.
        /// </summary>
        public const int MaxHeight = 60_000;

        // -- mountains -------------------------------------------------------------
        // Mountains are held away from the origin by a radial ramp. Without it the massif
        // noise is stationary and spawn lands wherever it happens to fall, which is as likely
        // to be a summit as a valley. The ramp guarantees a walkable basin at spawn and puts
        // the range on the horizon where it can be seen.

        /// <summary>Radius, in voxels, inside which no mountain relief is applied at all.</summary>
        public const int ValleyRadius = 15_000;         // 1.5 km

        /// <summary>Radius, in voxels, at which mountain relief reaches full amplitude.</summary>
        public const int MountainFullRadius = 60_000;   // 6 km

        /// <summary>
        /// Fraction of full mountain amplitude at a column, in fixed point 0..1024.
        /// Uses squared distance throughout so no square root — and therefore no float — is
        /// needed (Constitution I).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int MountainMask(int worldX, int worldZ)
        {
            // Cheap axis rejection before any multiply. Every column the castle grades, and
            // every column near spawn, lands here — HeightAt is called millions of times during
            // a build, so the common case must not pay for the squared-distance test at all.
            // Inside a half-radius box the farthest corner is 0.707r, safely within the valley.
            const int half = ValleyRadius / 2;
            if (worldX > -half && worldX < half && worldZ > -half && worldZ < half) return 0;

            long dx = worldX;
            long dz = worldZ;
            long distanceSq = dx * dx + dz * dz;

            const long innerSq = (long)ValleyRadius * ValleyRadius;
            const long outerSq = (long)MountainFullRadius * MountainFullRadius;
            if (distanceSq <= innerSq) return 0;
            if (distanceSq >= outerSq) return 1024;

            // Linear in distance, not in squared distance. Ramping on d² kept relief near
            // zero for most of the approach — 2% at 1 km, 21% at 2 km — so the range only
            // existed in a thin band at the very edge of view and read as no mountains at all.
            // An integer square root keeps this exact and float-free.
            long distance = IntegerSqrt(distanceSq);
            long span = MountainFullRadius - ValleyRadius;
            long t = ((distance - ValleyRadius) * 1024L) / span;
            if (t < 0) t = 0;
            if (t > 1024) t = 1024;
            return (int)t;
        }

        /// <summary>Floor of the square root, by Newton iteration on integers.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long IntegerSqrt(long value)
        {
            if (value <= 0) return 0;
            long guess = value;
            long next = (guess + 1) >> 1;
            while (next < guess)
            {
                guess = next;
                next = (guess + value / guess) >> 1;
            }
            return guess;
        }

        /// <summary>
        /// Surface height in voxels at a world column.
        ///
        /// Deliberately smooth at voxel scale: high-frequency detail costs surface area, and
        /// surface area is what the brick pool spends memory on.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int HeightAt(int worldX, int worldZ, uint seed)
        {
            int h = BaseHeight;

            // Massif and ridge octaves, at kilometre wavelengths. These carry essentially all
            // of the world's relief; the metre-scale octaves below only texture it.
            int mask = MountainMask(worldX, worldZ);
            if (mask > 0)
            {
                int massif = Octave(worldX, worldZ, 17, 85_000, seed ^ 0x4D4F554Eu);
                int ridge = Octave(worldX, worldZ, 15, 28_000, seed ^ 0x52494447u);
                int spur = Octave(worldX, worldZ, 13, 8_000, seed ^ 0x53505552u);
                // Rectify the massif so basins between ranges stay flat instead of carving
                // inverted valleys as deep as the peaks are tall.
                if (massif < 0) massif = massif >> 3;
                int relief = massif + ridge + spur;
                h += (int)(((long)relief * mask) >> 10);
            }

            h += Octave(worldX, worldZ, 9, 70, seed);
            h += Octave(worldX, worldZ, 7, 24, seed);
            h += Octave(worldX, worldZ, 5, 6, seed);

            // Fine detail, deliberately restored. A smooth analytic slope quantised to 10 cm
            // voxels shows clean concentric contour terraces under raking light, which reads as
            // a quarry. Breaking the height by a voxel or two scatters those contours.
            h += Octave(worldX, worldZ, 4, 4, seed);

            if (h < MinHeight) h = MinHeight;
            if (h > MaxHeight) h = MaxHeight;
            return h;
        }

        /// <summary>
        /// Steepness at a column, as rise in voxels over an 8-voxel run — the unit placement rules
        /// and definitions declare their slope limits in.
        ///
        /// Sampled symmetrically so the value at a point does not depend on which direction the
        /// caller approached from.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SlopeAt(int worldX, int worldZ, uint seed)
        {
            int hxMinus = HeightAt(worldX - 4, worldZ, seed);
            int hxPlus = HeightAt(worldX + 4, worldZ, seed);
            int hzMinus = HeightAt(worldX, worldZ - 4, seed);
            int hzPlus = HeightAt(worldX, worldZ + 4, seed);

            int dx = hxPlus - hxMinus;
            int dz = hzPlus - hzMinus;
            if (dx < 0) dx = -dx;
            if (dz < 0) dz = -dz;

            return dx > dz ? dx : dz;
        }

        // -- noise ---------------------------------------------------------------

        /// <summary>
        /// One octave of integer value noise: hash four lattice corners, interpolate with a
        /// fixed-point smoothstep.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Octave(int worldX, int worldZ, int log2Cell, int amplitude, uint seed)
        {
            int cell = 1 << log2Cell;

            // Arithmetic shift and mask, so negative world coordinates floor correctly rather
            // than truncating toward zero — which would mirror the terrain about the origin.
            int x0 = worldX >> log2Cell;
            int z0 = worldZ >> log2Cell;
            int fx = worldX & (cell - 1);
            int fz = worldZ & (cell - 1);

            int c00 = Corner(x0, z0, log2Cell, seed);
            int c10 = Corner(x0 + 1, z0, log2Cell, seed);
            int c01 = Corner(x0, z0 + 1, log2Cell, seed);
            int c11 = Corner(x0 + 1, z0 + 1, log2Cell, seed);

            int tx = Smooth(fx, cell);
            int tz = Smooth(fz, cell);

            int a = c00 + (((c10 - c00) * tx) >> 10);
            int b = c01 + (((c11 - c01) * tx) >> 10);
            int v = a + (((b - a) * tz) >> 10);

            return ((v * amplitude) >> 10) - (amplitude >> 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Corner(int x, int z, int salt, uint seed)
        {
            uint h = Hash((uint)x * 2654435761u ^ (uint)z * 2246822519u ^ seed ^ ((uint)salt << 24));
            return (int)((h >> 8) & 0x3FFu);
        }

        /// <summary>
        /// 3t² − 2t³ in fixed point, 0..1024.
        ///
        /// Unsigned rather than signed: the intermediate reaches about 1.07e9, which overflows a
        /// signed 32-bit multiply and fits comfortably in a uint.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Smooth(int f, int cell)
        {
            uint t = (uint)((f * 1024) / cell);
            return (int)((t * t * (3u * 1024u - 2u * t)) >> 20);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint Hash(uint v)
        {
            v ^= v >> 16; v *= 0x85ebca6bu;
            v ^= v >> 13; v *= 0xc2b2ae35u;
            v ^= v >> 16;
            return v;
        }
    }
}
