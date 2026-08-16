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
        internal readonly NativeArray<byte> ExactMixedFlags;
        internal readonly NativeList<int> ExactMixedBrickIndices;
        internal readonly NativeArray<byte> SnapshotClassificationFlags;

        // Step-8 feature-preserving HLOD scratch. These arrays exist only on the outer exact
        // ring; finer Transvoxel workers pay no memory cost for the coarse representation.
        internal readonly NativeArray<SurfaceBlockHlodSummary> HlodSummaries;
        internal readonly NativeArray<byte> HlodMaskScratch;
        internal readonly NativeArray<int> HlodOverflow;

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
                                          bool samplesFromMips, bool usesBlockHlod,
                                          int hlodCoreBrickEdge, int cellsPerAxis,
                                          int faceSamplesPerAxis)
        {
            // The step-8 block HLOD path never evaluates the Transvoxel density lattice.
            // Leave those multi-megabyte arrays uncreated for HLOD workers instead of carrying
            // exact-step scratch that can never be scheduled.
            if (usesBlockHlod)
            {
                Density = default;
                Materials = default;
                SurfaceSemantics = default;
                BoundarySamples = default;
            }
            else
            {
                Density = new NativeArray<float>(gridSampleCount, Allocator.Persistent,
                                                 NativeArrayOptions.UninitializedMemory);
                Materials = new NativeArray<byte>(gridSampleCount, Allocator.Persistent,
                                                  NativeArrayOptions.UninitializedMemory);
                SurfaceSemantics = new NativeArray<uint>(gridSampleCount, Allocator.Persistent,
                                                         NativeArrayOptions.UninitializedMemory);
                BoundarySamples = new NativeArray<byte>(gridSampleCount, Allocator.Persistent,
                                                        NativeArrayOptions.UninitializedMemory);
            }

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

            // Exact COW readers normally borrow Storage payload arrays. Keep only a minimal
            // fallback list for the HLOD worker rather than reserving legacy copy capacity.
            int legacyMixedCapacity = usesBlockHlod ? 1 : 64 * 1024;
            DensityMixedVoxels = new NativeList<byte>(legacyMixedCapacity, Allocator.Persistent);
            DensityMixedSurfaceSemantics = new NativeList<ushort>(legacyMixedCapacity,
                                                                  Allocator.Persistent);
            DensityMixedBoundarySamples = new NativeList<byte>(legacyMixedCapacity,
                                                               Allocator.Persistent);
            PinnedReadBlocks = new NativeList<VoxelReadPinToken>(
                brickCacheCount > 0 ? brickCacheCount : 1, Allocator.Persistent);
            if (!samplesFromMips)
            {
                ExactMixedFlags = new NativeArray<byte>(brickCacheCount, Allocator.Persistent,
                                                        NativeArrayOptions.UninitializedMemory);
                ExactMixedBrickIndices = new NativeList<int>(brickCacheCount, Allocator.Persistent);
                SnapshotClassificationFlags = usesBlockHlod
                    ? default
                    : new NativeArray<byte>(2, Allocator.Persistent,
                                            NativeArrayOptions.ClearMemory);
            }
            else
            {
                ExactMixedFlags = default;
                ExactMixedBrickIndices = default;
                SnapshotClassificationFlags = default;
            }

            if (usesBlockHlod)
            {
                HlodSummaries = new NativeArray<SurfaceBlockHlodSummary>(
                    brickCacheCount, Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory);
                int subcellEdge = hlodCoreBrickEdge
                                * SurfaceBlockHlodMeshJob.SubcellsPerBrickAxis;
                HlodMaskScratch = new NativeArray<byte>(
                    subcellEdge * subcellEdge, Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory);
                HlodOverflow = new NativeArray<int>(1, Allocator.Persistent,
                                                    NativeArrayOptions.ClearMemory);
            }
            else
            {
                HlodSummaries = default;
                HlodMaskScratch = default;
                HlodOverflow = default;
            }

            if (usesBlockHlod)
            {
                CompactedTopologyVertices = default;
                CompactedTopologyIndices = default;
                TopologyOverflowCell = default;
                FacetedMasks = default;
                FacetedVertices = default;
                FacetedIndices = default;
                FaceDensity = default;
                FaceMaterials = default;
                FaceSurfaces = default;
                TransitionVertices = default;
                TransitionIndices = default;
            }
            else
            {
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
            }

            // The HLOD worker meshes a 128^3 subcell volume. Keep its output fixed-capacity and
            // comfortably below the shared GPU arena ceiling so Burst can use AddNoResize and
            // report overflow instead of growing native memory on the frame path.
            int finalVertexCapacity = usesBlockHlod ? 262_144 : 32_768;
            int finalIndexCapacity = usesBlockHlod ? 393_216 : 49_152;
            Vertices = new NativeList<SmoothSurfaceVertex>(finalVertexCapacity,
                                                           Allocator.Persistent);
            Indices = new NativeList<uint>(finalIndexCapacity, Allocator.Persistent);
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
            if (ExactMixedFlags.IsCreated) ExactMixedFlags.Dispose();
            if (ExactMixedBrickIndices.IsCreated) ExactMixedBrickIndices.Dispose();
            if (SnapshotClassificationFlags.IsCreated) SnapshotClassificationFlags.Dispose();
            if (HlodSummaries.IsCreated) HlodSummaries.Dispose();
            if (HlodMaskScratch.IsCreated) HlodMaskScratch.Dispose();
            if (HlodOverflow.IsCreated) HlodOverflow.Dispose();
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
