using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace VoxelEngine.Rendering.Runtime.SurfaceExtraction
{
    /// <summary>
    /// Classifies logical Storage read blocks from a caller-owned occupancy snapshot. The job
    /// never touches RegionReadView or physical Storage payload memory, so it can safely remain in flight while
    /// authoritative Storage edits, publishes, or evicts regions.
    /// </summary>
    [BurstCompile]
    internal struct SurfaceBrickDiscoveryJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<ulong> OccupiedWords;
        [ReadOnly] public NativeArray<ulong> FullySolidWords;
        [WriteOnly] public NativeArray<byte> IsSurface;
        public int Edge;

        public void Execute(int index)
        {
            if (!Bit(OccupiedWords, index))
            {
                IsSurface[index] = 0;
                return;
            }

            int bx = index & (Edge - 1);
            int by = (index / Edge) & (Edge - 1);
            int bz = index / (Edge * Edge);

            bool boundary = !Bit(FullySolidWords, index)
                || bx == 0 || by == 0 || bz == 0
                || bx + 1 == Edge || by + 1 == Edge || bz + 1 == Edge
                || !Bit(FullySolidWords, index - 1)
                || !Bit(FullySolidWords, index + 1)
                || !Bit(FullySolidWords, index - Edge)
                || !Bit(FullySolidWords, index + Edge)
                || !Bit(FullySolidWords, index - Edge * Edge)
                || !Bit(FullySolidWords, index + Edge * Edge);
            IsSurface[index] = boundary ? (byte)1 : (byte)0;
        }

        private static bool Bit(NativeArray<ulong> words, int index) =>
            (words[index >> 6] & (1UL << (index & 63))) != 0UL;
    }

    /// <summary>
    /// Compacts the byte classification into block coordinates on a worker thread. Capacity is
    /// provisioned for the worst case by the scheduler, so the job never allocates or resizes.
    /// </summary>
    [BurstCompile]
    internal struct SurfaceBrickCompactJob : IJob
    {
        [ReadOnly] public NativeArray<byte> IsSurface;
        public NativeList<int3> SurfaceBlocks;
        public int Edge;

        /// <summary>
        /// Surface discovery is intentionally polled on later frames instead of completed on the
        /// frame path. Explicitly flush the terminal pipeline schedule so worker threads can make
        /// progress even when callers repeatedly poll from a tight EditMode/render loop. Flushing
        /// only submits queued jobs; it never waits for this dependency chain to finish.
        /// </summary>
        public JobHandle Schedule(JobHandle dependsOn)
        {
            JobHandle handle = IJobExtensions.Schedule(this, dependsOn);
            JobHandle.ScheduleBatchedJobs();
            return handle;
        }

        public void Execute()
        {
            SurfaceBlocks.Clear();
            for (int index = 0; index < IsSurface.Length; index++)
            {
                if (IsSurface[index] == 0) continue;
                int bx = index & (Edge - 1);
                int by = (index / Edge) & (Edge - 1);
                int bz = index / (Edge * Edge);
                SurfaceBlocks.AddNoResize(new int3(bx, by, bz));
            }
        }
    }
}
