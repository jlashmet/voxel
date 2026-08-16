using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Rendering.Runtime.SurfaceExtraction.Transvoxel
{
    [BurstCompile]
    internal struct ExactBrickMetadataClearJob : IJobParallelFor
    {
        [WriteOnly] public NativeArray<TransvoxelDensityBrick> Bricks;
        [WriteOnly] public NativeArray<byte> MixedFlags;

        public void Execute(int index)
        {
            Bricks[index] = default;
            MixedFlags[index] = 0;
        }
    }

    /// <summary>
    /// Maps one physically pinned region's compact block-ref metadata into the worker's padded
    /// exact brick cache. Region jobs fan out from one shared clear dependency; each writes a
    /// disjoint intersection, and the owner combines all region handles before compaction.
    /// Encoded refs may be authoritatively
    /// replaced while the job runs; the owning region revision is validated before output is used.
    /// </summary>
    [BurstCompile]
    internal struct ExactBrickMetadataRegionJob : IJobParallelFor
    {
        [NativeDisableContainerSafetyRestriction, ReadOnly]
        public NativeArray<int> EncodedBlockRefs;
        public int3 RegionCoord;
        public int3 IntersectionMinWorldBlock;
        public int3 IntersectionSize;
        public int3 CacheOrigin;
        public int BrickCacheEdge;

        [NativeDisableParallelForRestriction]
        public NativeArray<TransvoxelDensityBrick> Bricks;
        [NativeDisableParallelForRestriction]
        public NativeArray<byte> MixedFlags;

        public void Execute(int index)
        {
            int x = index % IntersectionSize.x;
            int yz = index / IntersectionSize.x;
            int y = yz % IntersectionSize.y;
            int z = yz / IntersectionSize.y;
            int3 worldBlock = IntersectionMinWorldBlock + new int3(x, y, z);

            int3 regionOrigin = RegionCoord * VoxelReadGrid.BlocksPerRegionEdge;
            int3 local = worldBlock - regionOrigin;
            int edge = VoxelReadGrid.BlocksPerRegionEdge;
            int regionIndex = local.x + edge * (local.y + edge * local.z);
            int encoded = EncodedBlockRefs[regionIndex];

            int3 cacheLocal = worldBlock - CacheOrigin;
            int cacheIndex = cacheLocal.x
                           + BrickCacheEdge * (cacheLocal.y + BrickCacheEdge * cacheLocal.z);
            VoxelReadBlockKind kind = VoxelReadBlockRefEncoding.Kind(encoded);
            if (kind == VoxelReadBlockKind.Empty)
            {
                Bricks[cacheIndex] = default;
                return;
            }

            if (kind == VoxelReadBlockKind.Uniform)
            {
                Bricks[cacheIndex] = new TransvoxelDensityBrick
                {
                    Kind = 1,
                    UniformMaterial = VoxelReadBlockRefEncoding.UniformMaterial(encoded),
                    MixedOffset = 0,
                };
                return;
            }

            Bricks[cacheIndex] = new TransvoxelDensityBrick
            {
                Kind = 2,
                UniformMaterial = 0,
                MixedOffset = VoxelReadBlockRefEncoding.MixedPayloadOffset(encoded),
            };
            MixedFlags[cacheIndex] = 1;
        }
    }

    [BurstCompile]
    internal struct ExactMixedBrickCompactJob : IJob
    {
        [ReadOnly] public NativeArray<byte> MixedFlags;
        public NativeList<int> MixedIndices;

        public void Execute()
        {
            MixedIndices.Clear();
            for (int i = 0; i < MixedFlags.Length; i++)
                if (MixedFlags[i] != 0) MixedIndices.AddNoResize(i);
        }
    }

    /// <summary>
    /// Derives the two build-routing facts previously discovered by a main-thread 287k-brick scan:
    /// whether the chunk owns any solid geometry and whether any material/surface semantics require
    /// the continuous Transvoxel path. Mixed payloads are immutable COW-pinned Storage versions.
    /// </summary>
    [BurstCompile]
    internal struct ExactSnapshotClassificationJob : IJob
    {
        [ReadOnly] public NativeArray<TransvoxelDensityBrick> Bricks;
        [NativeDisableContainerSafetyRestriction, ReadOnly]
        public NativeArray<byte> MixedVoxels;
        [NativeDisableContainerSafetyRestriction, ReadOnly]
        public NativeArray<ushort> MixedSurfaceSemantics;
        [NativeDisableContainerSafetyRestriction, ReadOnly]
        public NativeArray<byte> MixedBoundarySamples;
        public MaterialPaletteView Palette;
        public SurfaceCatalogueView Catalogue;
        public CoatingCatalogueView Coatings;
        public int BrickCacheEdge;
        public int BricksPerAxis;
        public int BrickCachePadding;
        public bool HasProfiles;
        public NativeArray<byte> Flags; // 0 = owns solid, 1 = continuous topology

        public void Execute()
        {
            bool hasOwnedSolid = false;
            bool requiresContinuous = HasProfiles;
            int plane = BrickCacheEdge * BrickCacheEdge;

            for (int index = 0; index < Bricks.Length; index++)
            {
                TransvoxelDensityBrick brick = Bricks[index];
                if (brick.Kind == 0) continue;

                int x = index % BrickCacheEdge;
                int y = (index / BrickCacheEdge) % BrickCacheEdge;
                int z = index / plane;
                bool ownsCore = x >= BrickCachePadding
                              && y >= BrickCachePadding
                              && z >= BrickCachePadding
                              && x < BrickCachePadding + BricksPerAxis
                              && y < BrickCachePadding + BricksPerAxis
                              && z < BrickCachePadding + BricksPerAxis;

                if (brick.Kind == 1)
                {
                    byte material = brick.UniformMaterial;
                    if (!IsSolid(material)) continue;
                    hasOwnedSolid |= ownsCore;
                    if (!requiresContinuous)
                    {
                        SurfaceStyleReadDefinition style = Catalogue.Get(
                            Palette.GetDefaultSurfaceStyle(material));
                        requiresContinuous = style.Reconstruction == SurfaceReconstruction.Smooth
                                          || style.Reconstruction == SurfaceReconstruction.Rounded;
                    }
                    if (hasOwnedSolid && requiresContinuous) break;
                    continue;
                }

                int end = brick.MixedOffset + VoxelReadGrid.VoxelsPerBlock;
                for (int voxel = brick.MixedOffset; voxel < end; voxel++)
                {
                    byte material = MixedVoxels[voxel];
                    if (!IsSolid(material)) continue;
                    hasOwnedSolid |= ownsCore;
                    if (!requiresContinuous)
                    {
                        uint surface = VoxelSurfaceSemantics.FromStorage(
                            MixedSurfaceSemantics[voxel]).Packed;
                        ushort styleId = (ushort)surface;
                        if (styleId == SurfaceStyles.MaterialDefault)
                            styleId = Palette.GetDefaultSurfaceStyle(material);
                        SurfaceStyleReadDefinition style = Catalogue.Get(styleId);
                        byte coating = (byte)(surface >> 16);
                        requiresContinuous = MixedBoundarySamples[voxel] != 0
                                          || Coatings.Get(coating).Displacement != 0
                                          || style.Reconstruction == SurfaceReconstruction.Smooth
                                          || style.Reconstruction == SurfaceReconstruction.Rounded;
                    }
                    if (hasOwnedSolid && requiresContinuous) break;
                }
                if (hasOwnedSolid && requiresContinuous) break;
            }

            Flags[0] = hasOwnedSolid ? (byte)1 : (byte)0;
            Flags[1] = requiresContinuous ? (byte)1 : (byte)0;
        }

        private static bool IsSolid(byte material) =>
            material != 0 && material != 11 && material != 16;
    }
}
