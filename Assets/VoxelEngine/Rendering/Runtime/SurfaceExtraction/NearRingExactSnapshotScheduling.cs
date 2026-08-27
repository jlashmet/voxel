using System;
using Unity.Jobs;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction.Transvoxel;

namespace VoxelEngine.Rendering.Runtime.SurfaceExtraction
{
    /// <summary>
    /// Keeps the two small exact near-ring metadata snapshots asynchronous while reducing the
    /// number of work-stealing batches they inject into Unity's already-busy geometry job pool.
    ///
    /// Step 1 and step 2 have only 10^3 and 18^3 padded metadata entries. The production
    /// scheduler used four batches for every clear and every intersecting region copy, multiplying
    /// a single snapshot into dozens of tiny worker-queue records. Running those jobs inline was
    /// rejected by the traversal regression because it delayed visible convergence. Instead this
    /// adapter preserves real JobHandles/dependencies and uses one batch per tiny parallel-for.
    ///
    /// Coarser exact rings keep the original batching. A thread-local flag is safe here because
    /// ScheduleExactMetadataSnapshot emits clear -> all region copies -> compact synchronously on
    /// the scheduling thread before another worker can enter this adapter sequence.
    /// </summary>
    internal static class NearRingExactSnapshotScheduling
    {
        internal const int CoalescedMetadataEntryLimit = 6000;

        [ThreadStatic]
        private static bool s_CoalesceCurrentSnapshot;

        internal static JobHandle Schedule(this ExactBrickMetadataClearJob job,
                                            int arrayLength, int innerloopBatchCount)
        {
            s_CoalesceCurrentSnapshot =
                arrayLength > 0 && arrayLength <= CoalescedMetadataEntryLimit;
            int batch = s_CoalesceCurrentSnapshot ? arrayLength : innerloopBatchCount;
            return IJobParallelForExtensions.Schedule(job, arrayLength, batch);
        }

        internal static JobHandle Schedule(this ExactBrickMetadataRegionJob job,
                                            int arrayLength, int innerloopBatchCount,
                                            JobHandle dependsOn)
        {
            int batch = s_CoalesceCurrentSnapshot && arrayLength > 0
                ? arrayLength : innerloopBatchCount;
            return IJobParallelForExtensions.Schedule(job, arrayLength, batch, dependsOn);
        }

        internal static JobHandle Schedule(this ExactMixedBrickCompactJob job,
                                            JobHandle dependsOn)
        {
            try
            {
                return IJobExtensions.Schedule(job, dependsOn);
            }
            finally
            {
                s_CoalesceCurrentSnapshot = false;
            }
        }
    }
}
