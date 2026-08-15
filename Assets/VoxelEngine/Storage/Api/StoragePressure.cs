namespace VoxelEngine.Storage.Api
{
    /// <summary>
    /// Representation-independent snapshot of Storage's mixed-voxel memory pressure.
    /// Consumers make residency policy decisions from bytes; allocator slots remain private.
    /// </summary>
    public readonly struct StoragePressure
    {
        public readonly long UsedBytes;
        public readonly long CapacityBytes;
        /// <summary>
        /// Representation-correct high-water limit beyond which aggressive eviction should
        /// continue. Storage computes it from its allocator; consumers never need slot counts.
        /// </summary>
        public readonly long CriticalLimitBytes;
        public readonly bool IsUnderPressure;

        public StoragePressure(long usedBytes, long capacityBytes, long criticalLimitBytes,
                               bool isUnderPressure)
        {
            UsedBytes = usedBytes;
            CapacityBytes = capacityBytes;
            CriticalLimitBytes = criticalLimitBytes;
            IsUnderPressure = isUnderPressure;
        }
    }
}
