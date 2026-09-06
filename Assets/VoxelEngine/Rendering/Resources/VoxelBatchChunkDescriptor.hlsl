#ifndef VOXEL_BATCH_CHUNK_DESCRIPTOR_INCLUDED
#define VOXEL_BATCH_CHUNK_DESCRIPTOR_INCLUDED

// Shared 44-byte wire layout of GpuSurfaceExtractor.BatchChunkDescriptor. Allocation and
// extraction consume the same StructuredBuffer; even unused trailing fields affect its stride.
struct BatchChunkDescriptor
{
    int3 chunkOriginVoxel;
    int sourceStep;
    uint transitionFaceMask;
    float voxelSize;
    uint handle;
    uint generationLow;
    uint generationHigh;
    uint profileStart;
    uint profileCount;
};

#endif
