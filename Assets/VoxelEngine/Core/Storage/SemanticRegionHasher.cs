using System;
using Unity.Mathematics;

namespace VoxelEngine.Core.Storage
{
    /// <summary>
    /// Cross-peer semantic fingerprint of region state.
    /// Allocator-local BrickPool indices never enter the hash. Material, authored surface,
    /// authored boundary, and the legacy network hard-surface bit are authoritative semantics.
    /// </summary>
    public static class SemanticRegionHasher
    {
        private const uint FnvOffsetBasis = 2166136261u;
        private const uint FnvPrime = 16777619u;

        public static uint HashRegion(in Region region, in BrickPool pool)
        {
            uint hash = BeginRegionHash(region.Coord);

            if (!region.BrickRefs.IsCreated)
                return hash;

            for (int i = 0; i < region.BrickRefs.Length; i++)
            {
                hash = MixByte(hash, region.IsHardSurfaceBrick(i) ? (byte)1 : (byte)0);

                BrickRef brick = region.BrickRefs[i];
                if (brick.IsMixed)
                {
                    hash = MixByte(hash, 2);
                    int offset = pool.VoxelOffset(brick.PoolIndex);
                    for (int voxel = 0; voxel < VoxelDimensions.VoxelsPerBrick; voxel++)
                    {
                        int cell = offset + voxel;
                        hash = MixByte(hash, pool.Voxels[cell]);
                        ushort surface = pool.SurfaceSemantics[cell];
                        hash = MixByte(hash, (byte)surface);
                        hash = MixByte(hash, (byte)(surface >> 8));
                        hash = MixByte(hash, pool.BoundarySamples[cell]);
                    }
                }
                else
                {
                    // Uniform BrickRef is valid only when there are no per-voxel overrides.
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

        internal static uint BeginRegionHash(int3 coord)
        {
            uint hash = FnvOffsetBasis;
            hash = MixInt(hash, coord.x);
            hash = MixInt(hash, coord.y);
            return MixInt(hash, coord.z);
        }

        private static uint MixInt(uint hash, int value)
        {
            uint v = unchecked((uint)value);
            hash = MixByte(hash, (byte)v);
            hash = MixByte(hash, (byte)(v >> 8));
            hash = MixByte(hash, (byte)(v >> 16));
            return MixByte(hash, (byte)(v >> 24));
        }

        internal static uint MixByte(uint hash, byte value)
        {
            hash ^= value;
            return hash * FnvPrime;
        }
    }
}
