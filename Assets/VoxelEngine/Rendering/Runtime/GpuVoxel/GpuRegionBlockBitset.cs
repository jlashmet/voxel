using System.Collections.Generic;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Rendering.Runtime.GpuVoxel
{
    /// <summary>
    /// Compact CPU-side index for world read-block membership.
    ///
    /// A Storage region contains exactly 64 read blocks per edge. That makes every X row one
    /// 64-bit word, so a renderer footprint can test an entire contiguous row with one dictionary
    /// lookup plus one mask operation instead of thousands of HashSet&lt;int3&gt; probes. The mirror
    /// still owns authoritative ready/pending sets; this is only their range-query acceleration
    /// structure and never becomes voxel truth.
    /// </summary>
    internal sealed class GpuRegionBlockBitset
    {
        private const int RegionEdge = VoxelReadGrid.BlocksPerRegionEdge;
        private const int RowsPerRegion = RegionEdge * RegionEdge;

        private sealed class RegionBits
        {
            internal readonly ulong[] Rows = new ulong[RowsPerRegion];
            internal int Count;
        }

        private readonly Dictionary<int3, RegionBits> _regions = new();

        internal int Count { get; private set; }
        internal int RegionCount => _regions.Count;

        internal bool Add(int3 worldBlock)
        {
            Split(worldBlock, out int3 region, out int3 local);
            if (!_regions.TryGetValue(region, out RegionBits bits))
            {
                bits = new RegionBits();
                _regions.Add(region, bits);
            }

            int row = local.y + RegionEdge * local.z;
            ulong mask = 1UL << local.x;
            if ((bits.Rows[row] & mask) != 0UL) return false;
            bits.Rows[row] |= mask;
            bits.Count++;
            Count++;
            return true;
        }

        internal bool Remove(int3 worldBlock)
        {
            Split(worldBlock, out int3 region, out int3 local);
            if (!_regions.TryGetValue(region, out RegionBits bits)) return false;

            int row = local.y + RegionEdge * local.z;
            ulong mask = 1UL << local.x;
            if ((bits.Rows[row] & mask) == 0UL) return false;
            bits.Rows[row] &= ~mask;
            bits.Count--;
            Count--;
            if (bits.Count == 0) _regions.Remove(region);
            return true;
        }

        internal bool Contains(int3 worldBlock)
        {
            Split(worldBlock, out int3 region, out int3 local);
            return (GetRowMask(region, local.y, local.z) & (1UL << local.x)) != 0UL;
        }

        internal ulong GetRowMask(int3 region, int localY, int localZ)
        {
            if (!_regions.TryGetValue(region, out RegionBits bits)) return 0UL;
            return bits.Rows[localY + RegionEdge * localZ];
        }

        internal void Clear()
        {
            _regions.Clear();
            Count = 0;
        }

        private static void Split(int3 worldBlock, out int3 region, out int3 local)
        {
            int shift = VoxelReadGrid.BlocksPerRegionEdgeLog2;
            region = worldBlock >> shift;
            local = worldBlock - (region << shift);
        }
    }
}
