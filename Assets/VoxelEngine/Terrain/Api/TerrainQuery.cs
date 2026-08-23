using System.Runtime.CompilerServices;

namespace VoxelEngine.Terrain.Api
{
    /// <summary>
    /// Canonical deterministic terrain query shared by terrain generation and foreign systems that
    /// need surface placement information. This is pure API-level query logic: its result depends
    /// only on world coordinates and the terrain seed and it owns no runtime/storage state.
    /// </summary>
    public static class TerrainQuery
    {
        public const int BaseHeight = 220;
        public const int MinHeight = 8;
        public const int MaxHeight = 60_000;

        public const int ValleyRadius = 15_000;
        public const int MountainFullRadius = 60_000;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int MountainMask(int worldX, int worldZ)
        {
            const int half = ValleyRadius / 2;
            if (worldX > -half && worldX < half && worldZ > -half && worldZ < half) return 0;

            long dx = worldX;
            long dz = worldZ;
            long distanceSq = dx * dx + dz * dz;

            const long innerSq = (long)ValleyRadius * ValleyRadius;
            const long outerSq = (long)MountainFullRadius * MountainFullRadius;
            if (distanceSq <= innerSq) return 0;
            if (distanceSq >= outerSq) return 1024;

            long distance = IntegerSqrt(distanceSq);
            long span = MountainFullRadius - ValleyRadius;
            long t = ((distance - ValleyRadius) * 1024L) / span;
            if (t < 0) t = 0;
            if (t > 1024) t = 1024;
            return (int)t;
        }

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int HeightAt(int worldX, int worldZ, uint seed)
        {
            int h = BaseHeight;

            int mask = MountainMask(worldX, worldZ);
            if (mask > 0)
            {
                int massif = Octave(worldX, worldZ, 17, 85_000, seed ^ 0x4D4F554Eu);
                int ridge = Octave(worldX, worldZ, 15, 28_000, seed ^ 0x52494447u);
                int spur = Octave(worldX, worldZ, 13, 8_000, seed ^ 0x53505552u);
                if (massif < 0) massif >>= 3;
                int relief = massif + ridge + spur;
                h += (int)(((long)relief * mask) >> 10);
            }

            // The inhabited valley is one broad, calm landform. Settlement-scale terraces,
            // paths, banks, and cuts are authored by their owning features; putting stronger
            // 51.2 m + 12.8 m relief into the base sampler produces contour bands through those
            // features and exposes the dirt layer as a sawtooth edge at ordinary player scale.
            h += Octave(worldX, worldZ, 9, 18, seed);

            // Player-scale relief stays in vegetation/material presentation, where it enriches a
            // surface without changing collision or cutting a contour around every few footsteps.

            if (h < MinHeight) h = MinHeight;
            if (h > MaxHeight) h = MaxHeight;
            return h;
        }

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Octave(int worldX, int worldZ, int log2Cell, int amplitude, uint seed) =>
            OctaveFixed(worldX, worldZ, log2Cell, amplitude, seed) >> 10;

        /// <summary>
        /// One octave in 1/1024-voxel fixed-point units. Keeping this precision until related
        /// landscape layers are combined prevents independent integer rounding from inventing
        /// one-voxel steps that are not present in the underlying smooth field.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int OctaveFixed(int worldX, int worldZ, int log2Cell, int amplitude, uint seed)
        {
            int cell = 1 << log2Cell;
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

            return v * amplitude - ((amplitude >> 1) << 10);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Corner(int x, int z, int salt, uint seed)
        {
            uint h = Hash((uint)x * 2654435761u ^ (uint)z * 2246822519u ^ seed ^ ((uint)salt << 24));
            return (int)((h >> 8) & 0x3FFu);
        }

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
