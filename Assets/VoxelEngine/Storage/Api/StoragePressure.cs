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
        public readonly bool IsUnderPressure;

        public StoragePressure(long usedBytes, long capacityBytes, bool isUnderPressure)
        {
            UsedBytes = usedBytes;
            CapacityBytes = capacityBytes;
            IsUnderPressure = isUnderPressure;
        }
    }
}
