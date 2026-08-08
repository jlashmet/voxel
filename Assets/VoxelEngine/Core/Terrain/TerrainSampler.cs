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
        public const int MaxHeight = 488;

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
