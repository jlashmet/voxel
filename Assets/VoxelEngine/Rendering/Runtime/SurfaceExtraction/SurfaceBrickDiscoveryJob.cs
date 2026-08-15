using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Rendering.SurfaceExtraction
{
    /// <summary>
    /// Classifies logical Storage read blocks in parallel. Output is one byte per block so
    /// workers never append or allocate; the scheduler compacts the sparse result afterward.
    /// </summary>
    [BurstCompile]
    internal struct SurfaceBrickDiscoveryJob : IJobParallelFor
    {
        [ReadOnly] public RegionReadView Region;
        [WriteOnly] public NativeArray<byte> IsSurface;
        public int Edge;

        public void Execute(int index)
        {
            int bx = index & (Edge - 1);
            int by = (index / Edge) & (Edge - 1);
            int bz = index / (Edge * Edge);
            int3 block = new int3(bx, by, bz);

            if (!Region.TryGetBlock(block, out VoxelReadBlock self) || !self.IsSolid)
            {
                IsSurface[index] = 0;
                return;
            }

            bool boundary = !Region.IsBlockFullySolid(block)
                || bx == 0 || by == 0 || bz == 0
                || bx + 1 == Edge || by + 1 == Edge || bz + 1 == Edge
                || !FullySolid(block + new int3(-1, 0, 0))
                || !FullySolid(block + new int3(1, 0, 0))
                || !FullySolid(block + new int3(0, -1, 0))
                || !FullySolid(block + new int3(0, 1, 0))
                || !FullySolid(block + new int3(0, 0, -1))
                || !FullySolid(block + new int3(0, 0, 1));
            IsSurface[index] = boundary ? (byte)1 : (byte)0;
        }

        private bool FullySolid(int3 localBlock) =>
            Region.TryGetBlock(localBlock, out VoxelReadBlock block)
            && block.IsSolid
            && Region.IsBlockFullySolid(localBlock);
    }
}
