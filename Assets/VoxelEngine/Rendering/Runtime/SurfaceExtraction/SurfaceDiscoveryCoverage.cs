using System.Collections.Generic;
using Unity.Mathematics;

namespace VoxelEngine.Rendering.Runtime.SurfaceExtraction
{
    /// <summary>
    /// Bounded presentation evidence from completed production surface discovery. A missing cache
    /// entry is not empty; only a completed discovery image can prove no surface is required.
    /// Each resident region needs 512 bits, one for each 64-voxel extraction cell.
    /// </summary>
    internal sealed class SurfaceDiscoveryCoverage
    {
        internal const int MaximumRegions = 1024;
        private sealed class Region
        {
            internal readonly ulong[] Surface = new ulong[8];
            internal bool Complete;
        }
        private readonly Dictionary<int3, Region> _regions = new();
        internal int Count => _regions.Count;

        internal void Invalidate(int3 region)
        {
            if (_regions.TryGetValue(region, out Region value)) value.Complete = false;
        }
        internal void Forget(int3 region) => _regions.Remove(region);
        internal void Clear() => _regions.Clear();
        internal void Begin(int3 region)
        {
            if (!_regions.TryGetValue(region, out Region value))
            {
                if (_regions.Count >= MaximumRegions) return;
                _regions.Add(region, value = new Region());
            }
            value.Complete = false;
            System.Array.Clear(value.Surface, 0, value.Surface.Length);
        }
        internal void AddSurfaceBlock(int3 worldBlock)
        {
            int3 fine = worldBlock >> 3;
            if (!_regions.TryGetValue(fine >> 3, out Region value)) return;
            int3 local = fine & 7;
            int bit = local.x + 8 * (local.y + 8 * local.z);
            value.Surface[bit >> 6] |= 1UL << (bit & 63);
        }
        internal void Complete(int3 region)
        {
            if (_regions.TryGetValue(region, out Region value)) value.Complete = true;
        }
        internal bool IsComplete(int3 region) =>
            _regions.TryGetValue(region, out Region value) && value.Complete;

        internal bool IsKnownEmpty(in SurfaceLodNodeKey node)
        {
            int3 min = node.Coordinate * node.SourceStep;
            for (int z = 0; z < node.SourceStep; z++)
            for (int y = 0; y < node.SourceStep; y++)
            for (int x = 0; x < node.SourceStep; x++)
            {
                int3 fine = min + new int3(x, y, z);
                if (!_regions.TryGetValue(fine >> 3, out Region region) || !region.Complete)
                    return false;
                int3 local = fine & 7;
                int bit = local.x + 8 * (local.y + 8 * local.z);
                if ((region.Surface[bit >> 6] & (1UL << (bit & 63))) != 0) return false;
            }
            return true;
        }
    }
}
