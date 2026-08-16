using System;
using Unity.Collections;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction.Transvoxel;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Rendering.Runtime.SurfaceExtraction
{
    /// <summary>
    /// Reusable native scratch owned by one solid geometry build worker.
    ///
    /// Render residency (chunk identities, slot generations and published arena leases) lives
    /// outside this object. This workspace owns only temporary snapshot/extraction/output memory
    /// reused from build to build, so residency can scale independently from the expensive native
    /// working set. CpuTransvoxelChunkCache may keep borrowed copies of these NativeContainer
    /// handles for compact job setup, but this object is the sole lifecycle/disposal owner.
    /// </summary>
    internal sealed class TransvoxelBuildWorkspace : IDisposable
    {
        internal readonly NativeArray<float> Density;
        internal readonly NativeArray<byte> Materials;
        internal readonly NativeArray<uint> SurfaceSemantics;
        internal readonly NativeArray<byte> BoundarySamples;
        internal readonly NativeArray<TransvoxelDensityBrick> DensityBricks;
        internal readonly NativeArray<byte> MipSampleOccupancy;
        internal readonly NativeArray<byte> MipSampleMaterials;
        internal readonly NativeList<byte> DensityMixedVoxels;
        internal readonly NativeList<ushort> DensityMixedSurfaceSemantics;
        internal readonly NativeList<byte> DensityMixedBoundarySamples;
        internal readonly NativeList<VoxelReadPinToken> PinnedReadBlocks;

        internal readonly NativeList<SmoothSurfaceVertex> CompactedTopologyVertices;
        internal readonly NativeList<uint> CompactedTopologyIndices;
        internal readonly NativeArray<int> TopologyOverflowCell;

        internal readonly NativeArray<uint> FacetedMasks;
        internal readonly NativeList<SmoothSurfaceVertex> FacetedVertices;
        internal readonly NativeList<uint> FacetedIndices;

        internal readonly NativeArray<float> FaceDensity;
        internal readonly NativeArray<byte> FaceMaterials;
        internal readonly NativeArray<uint> FaceSurfaces;
        internal readonly NativeList<SmoothSurfaceVertex> TransitionVertices;
        internal readonly NativeList<uint> TransitionIndices;

        internal readonly NativeList<SmoothSurfaceVertex> Vertices;
        internal readonly NativeList<uint> Indices;

        internal TransvoxelBuildWorkspace(int gridSampleCount, int brickCacheCount,
                                          bool samplesFromMips, int cellsPerAxis,
                                          int faceSamplesPerAxis)
        {
            Density = new NativeArray<float>(gridSampleCount, Allocator.Persistent,
                                             NativeArrayOptions.UninitializedMemory);
            Materials = new NativeArray<byte>(gridSampleCount, Allocator.Persistent,
                                              NativeArrayOptions.UninitializedMemory);
            SurfaceSemantics = new NativeArray<uint>(gridSampleCount, Allocator.Persistent,
                                                     NativeArrayOptions.UninitializedMemory);
            BoundarySamples = new NativeArray<byte>(gridSampleCount, Allocator.Persistent,
                                                    NativeArrayOptions.UninitializedMemory);

            if (samplesFromMips)
            {
                MipSampleOccupancy = new NativeArray<byte>(
                    gridSampleCount, Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory);
                MipSampleMaterials = new NativeArray<byte>(
                    gridSampleCount, Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory);
                DensityBricks = default;
            }
            else
            {
                DensityBricks = new NativeArray<TransvoxelDensityBrick>(
                    brickCacheCount, Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory);
                MipSampleOccupancy = default;
                MipSampleMaterials = default;
            }

            DensityMixedVoxels = new NativeList<byte>(64 * 1024, Allocator.Persistent);
            DensityMixedSurfaceSemantics = new NativeList<ushort>(64 * 1024,
                                                                  Allocator.Persistent);
            DensityMixedBoundarySamples = new NativeList<byte>(64 * 1024,
                                                               Allocator.Persistent);
            PinnedReadBlocks = new NativeList<VoxelReadPinToken>(
                brickCacheCount > 0 ? brickCacheCount : 1, Allocator.Persistent);

            CompactedTopologyVertices = new NativeList<SmoothSurfaceVertex>(
                16_384, Allocator.Persistent);
            CompactedTopologyIndices = new NativeList<uint>(24_576, Allocator.Persistent);
            TopologyOverflowCell = new NativeArray<int>(1, Allocator.Persistent);
            FacetedMasks = new NativeArray<uint>(
                6 * cellsPerAxis * cellsPerAxis * cellsPerAxis,
                Allocator.Persistent);
            FacetedVertices = new NativeList<SmoothSurfaceVertex>(16_384, Allocator.Persistent);
            FacetedIndices = new NativeList<uint>(24_576, Allocator.Persistent);

            int faceSamples = faceSamplesPerAxis * faceSamplesPerAxis;
            FaceDensity = new NativeArray<float>(faceSamples, Allocator.Persistent);
            FaceMaterials = new NativeArray<byte>(faceSamples, Allocator.Persistent);
            FaceSurfaces = new NativeArray<uint>(faceSamples, Allocator.Persistent);
            TransitionVertices = new NativeList<SmoothSurfaceVertex>(2048, Allocator.Persistent);
            TransitionIndices = new NativeList<uint>(3072, Allocator.Persistent);

            Vertices = new NativeList<SmoothSurfaceVertex>(32_768, Allocator.Persistent);
            Indices = new NativeList<uint>(49_152, Allocator.Persistent);
        }

        public void Dispose()
        {
            if (Density.IsCreated) Density.Dispose();
            if (Materials.IsCreated) Materials.Dispose();
            if (SurfaceSemantics.IsCreated) SurfaceSemantics.Dispose();
            if (BoundarySamples.IsCreated) BoundarySamples.Dispose();
            if (DensityBricks.IsCreated) DensityBricks.Dispose();
            if (MipSampleOccupancy.IsCreated) MipSampleOccupancy.Dispose();
            if (MipSampleMaterials.IsCreated) MipSampleMaterials.Dispose();
            if (DensityMixedVoxels.IsCreated) DensityMixedVoxels.Dispose();
            if (DensityMixedSurfaceSemantics.IsCreated) DensityMixedSurfaceSemantics.Dispose();
            if (DensityMixedBoundarySamples.IsCreated) DensityMixedBoundarySamples.Dispose();
            if (PinnedReadBlocks.IsCreated) PinnedReadBlocks.Dispose();
            if (CompactedTopologyVertices.IsCreated) CompactedTopologyVertices.Dispose();
            if (CompactedTopologyIndices.IsCreated) CompactedTopologyIndices.Dispose();
            if (TopologyOverflowCell.IsCreated) TopologyOverflowCell.Dispose();
            if (FacetedMasks.IsCreated) FacetedMasks.Dispose();
            if (FacetedVertices.IsCreated) FacetedVertices.Dispose();
            if (FacetedIndices.IsCreated) FacetedIndices.Dispose();
            if (FaceDensity.IsCreated) FaceDensity.Dispose();
            if (FaceMaterials.IsCreated) FaceMaterials.Dispose();
            if (FaceSurfaces.IsCreated) FaceSurfaces.Dispose();
            if (TransitionVertices.IsCreated) TransitionVertices.Dispose();
            if (TransitionIndices.IsCreated) TransitionIndices.Dispose();
            if (Vertices.IsCreated) Vertices.Dispose();
            if (Indices.IsCreated) Indices.Dispose();
        }
    }
}
