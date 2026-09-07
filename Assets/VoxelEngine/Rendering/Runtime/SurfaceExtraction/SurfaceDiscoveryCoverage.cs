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
        internal const int GpuWordsPerRegion = 20; // signed xyz, completeness, 512 surface bits
        internal ulong Version { get; private set; } = 1;

        internal int CopyGpuWords(uint[] destination)
        {
            if (destination == null || destination.Length < MaximumRegions * GpuWordsPerRegion)
                throw new System.ArgumentException("A bounded region-image staging array is required.", nameof(destination));
            int offset = 0;
            foreach (var pair in _regions)
            {
                destination[offset] = unchecked((uint)pair.Key.x);
                destination[offset + 1] = unchecked((uint)pair.Key.y);
                destination[offset + 2] = unchecked((uint)pair.Key.z);
                destination[offset + 3] = pair.Value.Complete ? 1u : 0u;
                for (int word = 0; word < 8; word++)
                {
                    ulong bits = pair.Value.Surface[word];
                    destination[offset + 4 + word * 2] = (uint)bits;
                    destination[offset + 5 + word * 2] = (uint)(bits >> 32);
                }
                offset += GpuWordsPerRegion;
            }
            return _regions.Count;
        }

        internal void Invalidate(int3 region)
        {
            if (_regions.TryGetValue(region, out Region value) && value.Complete)
            { value.Complete = false; Version++; }
        }
        internal void Forget(int3 region) { if (_regions.Remove(region)) Version++; }
        internal void Clear() { if (_regions.Count > 0) { _regions.Clear(); Version++; } }
        internal void Begin(int3 region)
        {
            if (!_regions.TryGetValue(region, out Region value))
            {
                if (_regions.Count >= MaximumRegions) return;
                _regions.Add(region, value = new Region());
            }
            Version++;
            value.Complete = false;
            System.Array.Clear(value.Surface, 0, value.Surface.Length);
        }
        internal void AddSurfaceBlock(int3 worldBlock)
        {
            int3 fine = worldBlock >> 3;
            if (!_regions.TryGetValue(fine >> 3, out Region value)) return;
            int3 local = fine & 7;
            int bit = local.x + 8 * (local.y + 8 * local.z);
            ulong mask = 1UL << (bit & 63);
            if ((value.Surface[bit >> 6] & mask) == 0)
            {
                value.Surface[bit >> 6] |= mask;
                // In-progress images cannot prove anything. Publish their bits at Complete,
                // without uploading every individual discovery addition to the GPU.
                if (value.Complete) Version++;
            }
        }
        internal void Complete(int3 region)
        {
            if (_regions.TryGetValue(region, out Region value) && !value.Complete)
            { value.Complete = true; Version++; }
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
