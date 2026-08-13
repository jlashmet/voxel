using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using VoxelEngine.Core.Storage;

namespace VoxelEngine.Rendering.SurfaceExtraction
{
    /// <summary>
    /// Classifies region bricks in parallel. Output is one byte per brick so workers never
    /// append or allocate; the scheduler compacts the sparse result after completion.
    /// </summary>
    [BurstCompile]
    internal struct SurfaceBrickDiscoveryJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<BrickRef> Bricks;
        [ReadOnly] public NativeArray<ulong> Occupancy;
        [WriteOnly] public NativeArray<byte> IsSurface;
        public int Edge;
        public int OccupancyWordsPerBrick;

        public void Execute(int index)
        {
            BrickRef brick = Bricks[index];
            if (brick.IsEmpty)
            {
                IsSurface[index] = 0;
                return;
            }
            int bx = index & (Edge - 1);
            int by = (index / Edge) & (Edge - 1);
            int bz = index / (Edge * Edge);
            bool boundary = !FullySolid(brick)
                || bx == 0 || by == 0 || bz == 0
                || bx + 1 == Edge || by + 1 == Edge || bz + 1 == Edge
                || !FullySolid(Bricks[index - 1]) || !FullySolid(Bricks[index + 1])
                || !FullySolid(Bricks[index - Edge]) || !FullySolid(Bricks[index + Edge])
                || !FullySolid(Bricks[index - Edge * Edge])
                || !FullySolid(Bricks[index + Edge * Edge]);
            IsSurface[index] = boundary ? (byte)1 : (byte)0;
        }

        private bool FullySolid(BrickRef brick)
        {
            if (brick.IsEmpty) return false;
            if (brick.IsUniform) return true;
            int offset = brick.PoolIndex * OccupancyWordsPerBrick;
            for (int i = 0; i < OccupancyWordsPerBrick; i++)
                if (Occupancy[offset + i] != ulong.MaxValue) return false;
            return true;
        }
    }
}
