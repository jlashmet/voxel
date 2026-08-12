using Unity.Mathematics;

namespace VoxelEngine.Structures
{
    public struct WorldArtVoxelStoneSpec
    {
        public int3 Min;
        public int3 Size;
        public byte Material;
        public uint Seed;
        public int ChipStrength;

        public static WorldArtVoxelStoneSpec DressedBlock(int3 min, int3 size, byte material, uint seed,
                                                           int chipStrength = 1)
        {
            return new WorldArtVoxelStoneSpec
            {
                Min = min,
                Size = size,
                Material = material,
                Seed = seed,
                ChipStrength = math.clamp(chipStrength, 0, 2)
            };
        }
    }

    /// <summary>
    /// Reusable cut-stone voxel primitives. These write only authoritative voxel data; any visible
    /// triangles are derived later by the normal voxel surface extractor.
    /// </summary>
    public static class WorldArtVoxelStonePrimitives
    {
        /// <summary>
        /// Rectangular dressed block with restrained deterministic corner losses. Large faces stay
        /// planar; weathering is concentrated at a few exposed corners instead of random surface noise.
        /// </summary>
        public static void DressedBlock(ref VoxelBrush brush, in WorldArtVoxelStoneSpec spec)
        {
            if (math.any(spec.Size <= 0)) return;
            brush.Box(spec.Min, spec.Size, spec.Material);
            if (spec.ChipStrength <= 0) return;

            int3 max = spec.Min + spec.Size - 1;
            uint h = Hash(spec.Seed);

            // Never erode all corners. Dressed masonry needs quiet planes and only a few authored chips.
            for (int corner = 0; corner < 8; corner++)
            {
                uint bit = 1u << corner;
                if ((h & bit) == 0u) continue;
                if (CountBits(h & 0xffu) > 3 && corner > 2) continue;

                int x = (corner & 1) == 0 ? spec.Min.x : max.x;
                int y = (corner & 2) == 0 ? spec.Min.y : max.y;
                int z = (corner & 4) == 0 ? spec.Min.z : max.z;
                brush.Set(x, y, z, 0);

                if (spec.ChipStrength < 2) continue;
                int sx = (corner & 1) == 0 ? 1 : -1;
                int sy = (corner & 2) == 0 ? 1 : -1;
                int sz = (corner & 4) == 0 ? 1 : -1;
                uint variant = Hash(spec.Seed + (uint)(corner * 97));
                if ((variant & 1u) != 0u && spec.Size.x > 4) brush.Set(x + sx, y, z, 0);
                if ((variant & 2u) != 0u && spec.Size.y > 4) brush.Set(x, y + sy, z, 0);
                if ((variant & 4u) != 0u && spec.Size.z > 4) brush.Set(x, y, z + sz, 0);
            }
        }

        private static int CountBits(uint x)
        {
            int count = 0;
            while (x != 0)
            {
                count += (int)(x & 1u);
                x >>= 1;
            }
            return count;
        }

        private static uint Hash(uint x)
        {
            x ^= x >> 16;
            x *= 0x7feb352du;
            x ^= x >> 15;
            x *= 0x846ca68bu;
            x ^= x >> 16;
            return x;
        }
    }
}
