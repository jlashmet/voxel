using System;

namespace VoxelEngine.Core.Storage
{
    /// <summary>
    /// Cross-peer semantic fingerprint of region state.
    ///
    /// Allocator-local BrickPool indices never enter the hash. Material bytes and the authored
    /// hard-surface semantic bit do, because both affect authoritative derived geometry.
    /// </summary>
    public static class SemanticRegionHasher
    {
        private const uint FnvOffsetBasis = 2166136261u;
        private const uint FnvPrime = 16777619u;

        public static uint HashRegion(in Region region, in BrickPool pool)
        {
            uint hash = FnvOffsetBasis;
            hash = MixInt(hash, region.Coord.x);
            hash = MixInt(hash, region.Coord.y);
            hash = MixInt(hash, region.Coord.z);

            if (!region.BrickRefs.IsCreated)
                return hash;

            for (int i = 0; i < region.BrickRefs.Length; i++)
            {
                hash = MixByte(hash, region.IsHardSurfaceBrick(i) ? (byte)1 : (byte)0);

                BrickRef brick = region.BrickRefs[i];
                if (brick.IsMixed)
                {
                    hash = MixByte(hash, 2);
                    for (int voxel = 0; voxel < VoxelDimensions.VoxelsPerBrick; voxel++)
                        hash = MixByte(hash, pool.GetVoxel(brick.PoolIndex, voxel));
                }
                else
                {
                    hash = MixByte(hash, 1);
                    hash = MixByte(hash, brick.UniformMaterial);
                }
            }

            return hash;
        }

        public static uint HashBytes(ReadOnlySpan<byte> data)
        {
            uint hash = FnvOffsetBasis;
            for (int i = 0; i < data.Length; i++)
                hash = MixByte(hash, data[i]);
            return hash;
        }

        private static uint MixInt(uint hash, int value)
        {
            uint v = unchecked((uint)value);
            hash = MixByte(hash, (byte)v);
            hash = MixByte(hash, (byte)(v >> 8));
            hash = MixByte(hash, (byte)(v >> 16));
            return MixByte(hash, (byte)(v >> 24));
        }

        private static uint MixByte(uint hash, byte value)
        {
            hash ^= value;
            return hash * FnvPrime;
        }
    }
}
