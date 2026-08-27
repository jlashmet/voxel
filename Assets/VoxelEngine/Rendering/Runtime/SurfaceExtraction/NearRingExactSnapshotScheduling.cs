using Unity.Jobs;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction.Transvoxel;

namespace VoxelEngine.Rendering.Runtime.SurfaceExtraction
{
    /// <summary>
    /// Keeps exact-snapshot metadata setup off the Unity job scheduler for the two tiny near-ring
    /// caches that are eligible for GPU extraction. Their complete padded metadata grids are only
    /// 10^3 (step 1) and 18^3 (step 2) entries. Scheduling clear + region fan-out + compaction for
    /// those grids costs more player-frame time than executing the same Burst-job bodies directly.
    ///
    /// Coarser exact rings are intentionally excluded: step 4 has 34^3 entries and step 8 has
    /// 66^3. Those retain the existing asynchronous Burst pipeline so a large snapshot can never
    /// become a main-thread scan again.
    /// </summary>
    internal static class NearRingExactSnapshotScheduling
    {
        internal const int InlineMetadataEntryLimit = 6000;

        internal static JobHandle Schedule(this ExactBrickMetadataClearJob job,
                                            int arrayLength, int innerloopBatchCount)
        {
            if (arrayLength >= 0 && arrayLength <= InlineMetadataEntryLimit)
            {
                for (int i = 0; i < arrayLength; i++) job.Execute(i);
                return default;
            }

            return IJobParallelForExtensions.Schedule(job, arrayLength, innerloopBatchCount);
        }

        internal static JobHandle Schedule(this ExactBrickMetadataRegionJob job,
                                            int arrayLength, int innerloopBatchCount,
                                            JobHandle dependsOn)
        {
            if (dependsOn.Equals(default(JobHandle)))
            {
                for (int i = 0; i < arrayLength; i++) job.Execute(i);
                return default;
            }

            return IJobParallelForExtensions.Schedule(
                job, arrayLength, innerloopBatchCount, dependsOn);
        }

        internal static JobHandle Schedule(this ExactMixedBrickCompactJob job,
                                            JobHandle dependsOn)
        {
            if (dependsOn.Equals(default(JobHandle)))
            {
                job.Execute();
                return default;
            }

            return IJobExtensions.Schedule(job, dependsOn);
        }
    }
}
