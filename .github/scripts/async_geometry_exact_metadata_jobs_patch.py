from pathlib import Path


def once(text, old, new, label):
    n = text.count(old)
    if n != 1:
        raise SystemExit(f'{label}: expected one match, found {n}')
    return text.replace(old, new, 1)

root = Path('Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction')
transvoxel = root / 'Transvoxel'

# -----------------------------------------------------------------------------
# Burst metadata/classification jobs.
# -----------------------------------------------------------------------------
jobs_path = transvoxel / 'ExactSnapshotMetadataJobs.cs'
jobs_path.write_text(r'''using Unity.Burst;
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
    /// exact brick cache. Region jobs are chained sequentially; each writes a disjoint intersection
    /// and therefore never contends with another region. Encoded refs may be authoritatively
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
''')
(transvoxel / 'ExactSnapshotMetadataJobs.cs.meta').write_text(
    'fileFormatVersion: 2\nguid: e2bcf6117b2646399a4a66bb2148d8ca\n')

# -----------------------------------------------------------------------------
# Workspace preallocates exact metadata scratch.
# -----------------------------------------------------------------------------
workspace_path = root / 'TransvoxelBuildWorkspace.cs'
w = workspace_path.read_text()
w = once(w,
'''        internal readonly NativeList<VoxelReadPinToken> PinnedReadBlocks;

        internal readonly NativeList<SmoothSurfaceVertex> CompactedTopologyVertices;''',
'''        internal readonly NativeList<VoxelReadPinToken> PinnedReadBlocks;
        internal readonly NativeArray<byte> ExactMixedFlags;
        internal readonly NativeList<int> ExactMixedBrickIndices;
        internal readonly NativeArray<byte> SnapshotClassificationFlags;

        internal readonly NativeList<SmoothSurfaceVertex> CompactedTopologyVertices;''',
'exact metadata workspace fields')
w = once(w,
'''            PinnedReadBlocks = new NativeList<VoxelReadPinToken>(
                brickCacheCount > 0 ? brickCacheCount : 1, Allocator.Persistent);

            CompactedTopologyVertices''',
'''            PinnedReadBlocks = new NativeList<VoxelReadPinToken>(
                brickCacheCount > 0 ? brickCacheCount : 1, Allocator.Persistent);
            if (!samplesFromMips)
            {
                ExactMixedFlags = new NativeArray<byte>(brickCacheCount, Allocator.Persistent,
                                                        NativeArrayOptions.UninitializedMemory);
                ExactMixedBrickIndices = new NativeList<int>(brickCacheCount, Allocator.Persistent);
                SnapshotClassificationFlags = new NativeArray<byte>(2, Allocator.Persistent,
                                                                     NativeArrayOptions.ClearMemory);
            }
            else
            {
                ExactMixedFlags = default;
                ExactMixedBrickIndices = default;
                SnapshotClassificationFlags = default;
            }

            CompactedTopologyVertices''', 'exact metadata workspace allocation')
w = once(w,
'''            if (PinnedReadBlocks.IsCreated) PinnedReadBlocks.Dispose();
            if (CompactedTopologyVertices.IsCreated) CompactedTopologyVertices.Dispose();''',
'''            if (PinnedReadBlocks.IsCreated) PinnedReadBlocks.Dispose();
            if (ExactMixedFlags.IsCreated) ExactMixedFlags.Dispose();
            if (ExactMixedBrickIndices.IsCreated) ExactMixedBrickIndices.Dispose();
            if (SnapshotClassificationFlags.IsCreated) SnapshotClassificationFlags.Dispose();
            if (CompactedTopologyVertices.IsCreated) CompactedTopologyVertices.Dispose();''',
'exact metadata workspace disposal')
workspace_path.write_text(w)

# -----------------------------------------------------------------------------
# Cache orchestration.
# -----------------------------------------------------------------------------
cache_path = root / 'CpuTransvoxelChunkCache.cs'
c = cache_path.read_text()

# Phase documentation and constants.
c = c.replace(
'            public int Phase;   // 0 snapshot, 1 jobs, 2 faceted job, 3 profiles, 4 seams, 5 result append',
'            public int Phase;   // 0 snapshot, 1 jobs, 2 faceted, 3 profiles, 4 seams, 5 append, 6 pin release', 1)
c = once(c,
'''        private const int BrickCachePadding = 1;
        private readonly int BrickCacheEdge;''',
'''        private const int BrickCachePadding = 1;
        private const int MaxExactSnapshotRegions = 27;
        private const int ExactMixedPinChecksPerDeadline = 16;
        private readonly int BrickCacheEdge;''', 'exact metadata constants')

# Fields after pin state.
c = once(c,
'''        private int _pinnedReleaseCursor;
        private bool _discardBuildAfterPinRelease;
        private bool _snapshotPinUnavailable;
        private JobHandle _densityJobHandle;''',
'''        private int _pinnedReleaseCursor;
        private bool _discardBuildAfterPinRelease;
        private bool _snapshotPinUnavailable;
        private readonly PinnedRegionBlockRefs[] _pinnedRegionBlockRefs =
            new PinnedRegionBlockRefs[MaxExactSnapshotRegions];
        private IRegionReadSource _pinnedRegionSource;
        private int _pinnedRegionCount;
        private NativeArray<byte> _exactMixedFlags;
        private NativeList<int> _exactMixedBrickIndices;
        private NativeArray<byte> _snapshotClassificationFlags;
        private JobHandle _exactMetadataJobHandle;
        private bool _exactMetadataJobScheduled;
        private bool _exactMetadataReady;
        private JobHandle _exactClassificationJobHandle;
        private bool _exactClassificationJobScheduled;
        private int _exactMixedPinCursor;
        private JobHandle _densityJobHandle;''', 'exact metadata cache fields')

# Workspace aliases.
c = once(c,
'''            _pinnedReadBlocks = _workspace.PinnedReadBlocks;
            _compactedTopologyVertices = _workspace.CompactedTopologyVertices;''',
'''            _pinnedReadBlocks = _workspace.PinnedReadBlocks;
            _exactMixedFlags = _workspace.ExactMixedFlags;
            _exactMixedBrickIndices = _workspace.ExactMixedBrickIndices;
            _snapshotClassificationFlags = _workspace.SnapshotClassificationFlags;
            _compactedTopologyVertices = _workspace.CompactedTopologyVertices;''',
'exact metadata workspace aliases')

# Running jobs includes snapshot jobs.
c = once(c,
'''        public int RunningJobCount => _densityJobScheduled || _topologyJobScheduled
                                   || _facetedMaskJobScheduled || _transitionJobScheduled
                                    ? 1 : 0;''',
'''        public int RunningJobCount => _exactMetadataJobScheduled || _exactClassificationJobScheduled
                                   || _densityJobScheduled || _topologyJobScheduled
                                   || _facetedMaskJobScheduled || _transitionJobScheduled
                                    ? 1 : 0;''', 'snapshot jobs in metrics')

# Discard lifecycle must observe job completion before any release/reset.
c = once(c,
'''            // A sliced snapshot can become stale between frames. Unlike a scheduled Burst job it
            // owns no worker dependency yet, so abandon it immediately and let the newer dirty
            // generation restart rather than spending more budget on data we will reject anyway.
            if (_build.Active && !_build.SnapshotTaken
                && _desiredVersions.TryGetValue(_build.Coordinate, out ulong slicedDesired)
                && slicedDesired > _build.SourceVersion)
                _discardBuildAfterPinRelease = true;

            double deadline = Time.realtimeSinceStartupAsDouble
                            + math.max(0.0, budgetMs) * 0.001;
            if (_discardBuildAfterPinRelease)
            {
                if (!StepReleasePinnedSnapshotBlocks(deadline)) return;
                StaleBuildCount++;
                _discardBuildAfterPinRelease = false;
                ResetCompletedBuild();
            }''',
'''            // Snapshot work may now include Burst metadata/classification jobs. A newer source
            // generation marks the build for discard, but the frame path never completes an
            // unfinished job: it waits for IsCompleted, then drains leases under the deadline.
            if (_build.Active && !_build.SnapshotTaken
                && _desiredVersions.TryGetValue(_build.Coordinate, out ulong slicedDesired)
                && slicedDesired > _build.SourceVersion)
                _discardBuildAfterPinRelease = true;

            double deadline = Time.realtimeSinceStartupAsDouble
                            + math.max(0.0, budgetMs) * 0.001;
            if (_discardBuildAfterPinRelease)
            {
                if (!ScheduledJobsComplete()) return;
                CompleteJobs();
                ReleasePinnedRegionMetadataImmediate();
                if (!StepReleasePinnedSnapshotBlocks(deadline)) return;
                int3 retry = _build.Coordinate;
                StaleBuildCount++;
                _discardBuildAfterPinRelease = false;
                ResetCompletedBuild();
                MarkDirty(retry);
            }''', 'snapshot stale lifecycle')

# Remove obsolete exact-block slice constant, keep mip slice.
c = once(c,
'''        private const int SnapshotBlocksPerDeadlineCheck = 8;
        private const int SnapshotMipSamplesPerDeadlineCheck = 64;''',
'''        private const int SnapshotMipSamplesPerDeadlineCheck = 64;''', 'remove main-thread exact scan constant')

# Replace exact snapshot and ClassifySnapshotBrick section up to mip snapshot.
start = c.index('        private bool StepExactDensitySnapshot(')
end = c.index('        private bool StepMipDensitySnapshot(', start)
replacement = r'''        private bool StepExactDensitySnapshot(IRegionReadSource source,
                                              in MaterialPaletteView palette,
                                              float voxelSize, double deadlineSeconds)
        {
            double sliceStart = Time.realtimeSinceStartupAsDouble;
            using var snapshotScope = s_SnapshotMarker.Auto();
            if (!_build.SnapshotInitialised)
            {
                if (_pinnedReadBlocks.Length != 0 || _pinnedRegionCount != 0)
                    throw new InvalidOperationException(
                        "Cannot begin a new exact snapshot while previous Storage leases remain.");
                _densityMixedVoxels.Clear();
                _densityMixedSurfaceSemantics.Clear();
                _densityMixedBoundarySamples.Clear();
                _pinnedReadSource = source;
                _pinnedReleaseCursor = 0;
                _pinnedMixedVoxels = default;
                _pinnedMixedSurfaceSemantics = default;
                _pinnedMixedBoundarySamples = default;
                _exactMixedPinCursor = 0;
                _exactMetadataReady = false;
                _buildSurfaceCatalogue = _surfaceCatalogue;
                _buildCoatingCatalogue = _coatingCatalogue;
                _buildPalette = palette;
                _build.MaterialPaletteVersion = palette.Version;
                _buildProfileBlocks = _profileBlocksByChunk.TryGetValue(
                    _build.Coordinate, out ProfileBlock[] blocks)
                    ? blocks : Array.Empty<ProfileBlock>();
                _build.SnapshotCursor = 0;
                _build.SnapshotCpuMs = 0.0;
                _build.HasOwnedSolid = false;
                _build.RequiresContinuousTopology = _buildProfileBlocks.Length > 0;
                _build.SnapshotInitialised = true;
            }

            int3 chunkOriginVoxel = _build.Coordinate * VoxelsPerAxis;
            int3 chunkBrickOrigin = new(chunkOriginVoxel.x >> VoxelReadGrid.BlockEdgeLog2,
                                        chunkOriginVoxel.y >> VoxelReadGrid.BlockEdgeLog2,
                                        chunkOriginVoxel.z >> VoxelReadGrid.BlockEdgeLog2);
            int3 cacheOrigin = chunkBrickOrigin - BrickCachePadding;

            if (!_exactMetadataReady)
            {
                if (!_exactMetadataJobScheduled)
                {
                    ScheduleExactMetadataSnapshot(source, cacheOrigin);
                    AccumulateSnapshotSlice(sliceStart, completed: false);
                    return false;
                }

                if (!_exactMetadataJobHandle.IsCompleted)
                {
                    AccumulateSnapshotSlice(sliceStart, completed: false);
                    return false;
                }

                _exactMetadataJobHandle.Complete();
                _exactMetadataJobScheduled = false;
                if (!PinnedRegionMetadataCurrent())
                {
                    ReleasePinnedRegionMetadataImmediate();
                    _discardBuildAfterPinRelease = true;
                    AccumulateSnapshotSlice(sliceStart, completed: false);
                    return false;
                }
                _exactMetadataReady = true;
            }

            // The worker identified only the mixed refs. Pin those payload versions in bounded
            // slices; uniform/empty blocks need no physical lease at all.
            while (_exactMixedPinCursor < _exactMixedBrickIndices.Length)
            {
                int end = math.min(_exactMixedBrickIndices.Length,
                                   _exactMixedPinCursor + ExactMixedPinChecksPerDeadline);
                for (; _exactMixedPinCursor < end; _exactMixedPinCursor++)
                {
                    int cacheIndex = _exactMixedBrickIndices[_exactMixedPinCursor];
                    int3 worldBlock = WorldBlockForCacheIndex(cacheIndex, cacheOrigin);
                    _snapshotPinUnavailable = false;
                    if (!source.TryPinWorldBlock(worldBlock, out PinnedVoxelReadBlock pinned))
                    {
                        _snapshotPinUnavailable = true;
                        AccumulateSnapshotSlice(sliceStart, completed: false);
                        return false;
                    }

                    TransvoxelDensityBrick expected = _densityBricks[cacheIndex];
                    if (pinned.Kind != VoxelReadBlockKind.Mixed || !pinned.HasPinnedPayload
                        || pinned.MixedOffset != expected.MixedOffset)
                    {
                        if (pinned.HasPinnedPayload)
                            source.ReleasePinnedWorldBlock(in pinned.Pin);
                        ReleasePinnedRegionMetadataImmediate();
                        _discardBuildAfterPinRelease = true;
                        AccumulateSnapshotSlice(sliceStart, completed: false);
                        return false;
                    }

                    if (!_pinnedMixedVoxels.IsCreated)
                    {
                        _pinnedMixedVoxels = pinned.MixedVoxels;
                        _pinnedMixedSurfaceSemantics = pinned.MixedSurfaceSemantics;
                        _pinnedMixedBoundarySamples = pinned.MixedBoundarySamples;
                    }
                    else if (_pinnedMixedVoxels.Length != pinned.MixedVoxels.Length
                             || _pinnedMixedSurfaceSemantics.Length
                                != pinned.MixedSurfaceSemantics.Length
                             || _pinnedMixedBoundarySamples.Length
                                != pinned.MixedBoundarySamples.Length)
                    {
                        source.ReleasePinnedWorldBlock(in pinned.Pin);
                        ReleasePinnedRegionMetadataImmediate();
                        _discardBuildAfterPinRelease = true;
                        AccumulateSnapshotSlice(sliceStart, completed: false);
                        return false;
                    }
                    _pinnedReadBlocks.Add(pinned.Pin);
                }

                if (_exactMixedPinCursor < _exactMixedBrickIndices.Length
                    && Time.realtimeSinceStartupAsDouble >= deadlineSeconds)
                {
                    AccumulateSnapshotSlice(sliceStart, completed: false);
                    return false;
                }
            }

            // Region refs may have changed while mixed payloads were pinned across frames. Never
            // splice metadata generations: reject the whole optimistic snapshot and try again.
            if (!PinnedRegionMetadataCurrent())
            {
                ReleasePinnedRegionMetadataImmediate();
                _discardBuildAfterPinRelease = true;
                AccumulateSnapshotSlice(sliceStart, completed: false);
                return false;
            }
            ReleasePinnedRegionMetadataImmediate();

            if (!_exactClassificationJobScheduled)
            {
                _snapshotClassificationFlags[0] = 0;
                _snapshotClassificationFlags[1] = 0;
                _exactClassificationJobHandle = new ExactSnapshotClassificationJob
                {
                    Bricks = _densityBricks,
                    MixedVoxels = PinnedMixedVoxelsOrFallback(),
                    MixedSurfaceSemantics = PinnedMixedSurfaceSemanticsOrFallback(),
                    MixedBoundarySamples = PinnedMixedBoundarySamplesOrFallback(),
                    Palette = _buildPalette,
                    Catalogue = _buildSurfaceCatalogue,
                    Coatings = _buildCoatingCatalogue,
                    BrickCacheEdge = BrickCacheEdge,
                    BricksPerAxis = BricksPerAxis,
                    BrickCachePadding = BrickCachePadding,
                    HasProfiles = _buildProfileBlocks.Length > 0,
                    Flags = _snapshotClassificationFlags,
                }.Schedule();
                _exactClassificationJobScheduled = true;
                AccumulateSnapshotSlice(sliceStart, completed: false);
                return false;
            }

            if (!_exactClassificationJobHandle.IsCompleted)
            {
                AccumulateSnapshotSlice(sliceStart, completed: false);
                return false;
            }
            _exactClassificationJobHandle.Complete();
            _exactClassificationJobScheduled = false;
            _build.HasOwnedSolid = _snapshotClassificationFlags[0] != 0;
            _build.RequiresContinuousTopology = _snapshotClassificationFlags[1] != 0;
            _build.SnapshotTaken = true;
            _exactMetadataReady = false;
            _exactMixedPinCursor = 0;
            AccumulateSnapshotSlice(sliceStart, completed: true);

            if (!_build.HasOwnedSolid && _buildProfileBlocks.Length == 0)
                return true;

            if (_build.RequiresContinuousTopology)
            {
                var job = new TransvoxelDensityJob
                {
                    Bricks = _densityBricks,
                    MixedVoxels = PinnedMixedVoxelsOrFallback(),
                    MixedSurfaceSemantics = PinnedMixedSurfaceSemanticsOrFallback(),
                    MixedBoundarySamples = PinnedMixedBoundarySamplesOrFallback(),
                    Palette = _buildPalette,
                    Catalogue = _buildSurfaceCatalogue,
                    Coatings = _buildCoatingCatalogue,
                    Density = _density,
                    Materials = _materials,
                    SurfaceSemantics = _surfaceSemantics,
                    BoundarySamples = _boundarySamples,
                    ChunkOriginVoxel = chunkOriginVoxel,
                    BrickCacheOrigin = cacheOrigin,
                    BrickCacheEdge = BrickCacheEdge,
                    GridSize = GridSize,
                    Padding = Padding,
                    SourceStep = SourceStep
                };
                _build.DensityScheduledSeconds = Time.realtimeSinceStartupAsDouble;
                _densityJobHandle = job.Schedule(GridSampleCount, 64);
                _densityJobScheduled = true;
                ScheduleTopologyJob(voxelSize, _densityJobHandle);
                ScheduleFacetedMaskJob(_densityJobHandle);
                ScheduleFacetedMergeJob(voxelSize);
            }
            return true;
        }

        private void ScheduleExactMetadataSnapshot(IRegionReadSource source, int3 cacheOrigin)
        {
            if (_pinnedRegionCount != 0)
                throw new InvalidOperationException("Exact metadata regions were already pinned.");
            _pinnedRegionSource = source;
            _exactMixedBrickIndices.Clear();

            JobHandle dependency = new ExactBrickMetadataClearJob
            {
                Bricks = _densityBricks,
                MixedFlags = _exactMixedFlags,
            }.Schedule(BrickCacheCount, 256);

            int edge = VoxelReadGrid.BlocksPerRegionEdge;
            int3 cacheMaxExclusive = cacheOrigin + BrickCacheEdge;
            int3 minRegion = cacheOrigin >> VoxelReadGrid.BlocksPerRegionEdgeLog2;
            int3 maxRegion = (cacheMaxExclusive - 1) >> VoxelReadGrid.BlocksPerRegionEdgeLog2;

            for (int rz = minRegion.z; rz <= maxRegion.z; rz++)
            for (int ry = minRegion.y; ry <= maxRegion.y; ry++)
            for (int rx = minRegion.x; rx <= maxRegion.x; rx++)
            {
                int3 regionCoord = new(rx, ry, rz);
                if (!source.TryPinRegionBlockRefs(regionCoord, out PinnedRegionBlockRefs pinned))
                    continue;
                if (_pinnedRegionCount >= MaxExactSnapshotRegions)
                {
                    source.ReleasePinnedRegion(in pinned.Pin);
                    ReleasePinnedRegionMetadataImmediate();
                    throw new InvalidOperationException(
                        "Exact snapshot exceeded the 3x3x3 pinned-region bound.");
                }
                _pinnedRegionBlockRefs[_pinnedRegionCount++] = pinned;

                int3 regionMin = regionCoord * edge;
                int3 regionMaxExclusive = regionMin + edge;
                int3 intersectionMin = math.max(cacheOrigin, regionMin);
                int3 intersectionMax = math.min(cacheMaxExclusive, regionMaxExclusive);
                int3 size = intersectionMax - intersectionMin;
                int volume = size.x * size.y * size.z;
                if (volume <= 0) continue;

                dependency = new ExactBrickMetadataRegionJob
                {
                    EncodedBlockRefs = pinned.EncodedBlockRefs,
                    RegionCoord = regionCoord,
                    IntersectionMinWorldBlock = intersectionMin,
                    IntersectionSize = size,
                    CacheOrigin = cacheOrigin,
                    BrickCacheEdge = BrickCacheEdge,
                    Bricks = _densityBricks,
                    MixedFlags = _exactMixedFlags,
                }.Schedule(volume, 128, dependency);
            }

            _exactMetadataJobHandle = new ExactMixedBrickCompactJob
            {
                MixedFlags = _exactMixedFlags,
                MixedIndices = _exactMixedBrickIndices,
            }.Schedule(dependency);
            _exactMetadataJobScheduled = true;
        }

        private static int3 WorldBlockForCacheIndex(int index, int3 cacheOrigin)
        {
            // BrickCacheEdge is instance state, so callers compute the coordinate inline below.
            // This placeholder is replaced by the instance overload immediately following it.
            return cacheOrigin + index;
        }

        private int3 WorldBlockForCacheIndex(int index, int3 cacheOrigin, bool _ = true)
        {
            int x = index % BrickCacheEdge;
            int y = (index / BrickCacheEdge) % BrickCacheEdge;
            int z = index / (BrickCacheEdge * BrickCacheEdge);
            return cacheOrigin + new int3(x, y, z);
        }

        private bool PinnedRegionMetadataCurrent()
        {
            if (_pinnedRegionCount == 0) return true;
            if (_pinnedRegionSource == null) return false;
            for (int i = 0; i < _pinnedRegionCount; i++)
            {
                VoxelRegionPinToken token = _pinnedRegionBlockRefs[i].Pin;
                if (!_pinnedRegionSource.IsPinnedRegionCurrent(in token)) return false;
            }
            return true;
        }

        private void ReleasePinnedRegionMetadataImmediate()
        {
            if (_pinnedRegionSource != null)
            {
                for (int i = 0; i < _pinnedRegionCount; i++)
                {
                    VoxelRegionPinToken token = _pinnedRegionBlockRefs[i].Pin;
                    _pinnedRegionSource.ReleasePinnedRegion(in token);
                    _pinnedRegionBlockRefs[i] = default;
                }
            }
            _pinnedRegionCount = 0;
            _pinnedRegionSource = null;
        }

'''
# Fix the intentionally simple helper call before writing: use the instance overload explicitly.
replacement = replacement.replace(
    'int3 worldBlock = WorldBlockForCacheIndex(cacheIndex, cacheOrigin);',
    'int3 worldBlock = WorldBlockForCacheIndex(cacheIndex, cacheOrigin, true);')
c = c[:start] + replacement + c[end:]

# Remove old SnapshotBlock method and its packed-copy classification helpers, which are now absent
# because the replacement ended before StepMipDensitySnapshot. The old SnapshotBlock sits after mip
# snapshot, so delete it separately up to StepCells while preserving pinned-release helpers.
old_snapshot_start = c.find('        private TransvoxelDensityBrick SnapshotBlock(')
if old_snapshot_start >= 0:
    old_snapshot_end = c.index('        private NativeArray<byte> PinnedMixedVoxelsOrFallback()', old_snapshot_start)
    c = c[:old_snapshot_start] + c[old_snapshot_end:]

# Remove the placeholder static overload; keep only instance helper.
placeholder = r'''        private static int3 WorldBlockForCacheIndex(int index, int3 cacheOrigin)
        {
            // BrickCacheEdge is instance state, so callers compute the coordinate inline below.
            // This placeholder is replaced by the instance overload immediately following it.
            return cacheOrigin + index;
        }

'''
c = c.replace(placeholder, '', 1)
c = c.replace('private int3 WorldBlockForCacheIndex(int index, int3 cacheOrigin, bool _ = true)',
              'private int3 WorldBlockForCacheIndex(int index, int3 cacheOrigin)', 1)
c = c.replace('WorldBlockForCacheIndex(cacheIndex, cacheOrigin, true)',
              'WorldBlockForCacheIndex(cacheIndex, cacheOrigin)', 1)

# Reset state invariants.
c = once(c,
'''        private void ResetCompletedBuild()
        {
            if (_pinnedReadBlocks.Length != 0)
                throw new InvalidOperationException(
                    "Build reset attempted before pinned Storage payloads were released.");
            _pendingUpload = false;''',
'''        private void ResetCompletedBuild()
        {
            if (_pinnedReadBlocks.Length != 0 || _pinnedRegionCount != 0
                || _exactMetadataJobScheduled || _exactClassificationJobScheduled)
                throw new InvalidOperationException(
                    "Build reset attempted before snapshot jobs/Storage leases were released.");
            _exactMetadataReady = false;
            _exactMixedPinCursor = 0;
            _pendingUpload = false;''', 'reset exact snapshot invariant')

# Removal defers while metadata leases exist too.
c = once(c,
'''            if (_build.Active && _build.Coordinate.Equals(chunk)
                && _pinnedReadBlocks.Length > 0)
            {
                // All handles were observed complete above, so this releases only Unity job
                // safety state. Physical brick pins are drained later under the build deadline.
                CompleteJobs();
                _discardBuildAfterPinRelease = true;
                return false;
            }''',
'''            if (_build.Active && _build.Coordinate.Equals(chunk)
                && (_pinnedReadBlocks.Length > 0 || _pinnedRegionCount > 0))
            {
                // All handles were observed complete above. Metadata leases are a fixed <=27 and
                // can release immediately; physical mixed-brick pins drain later under deadline.
                CompleteJobs();
                ReleasePinnedRegionMetadataImmediate();
                _discardBuildAfterPinRelease = true;
                return false;
            }''', 'removal defers metadata leases')

# Scheduled jobs include exact snapshot workers.
c = once(c,
'''        private bool ScheduledJobsComplete()
        {
            if (_densityJobScheduled && !_densityJobHandle.IsCompleted) return false;''',
'''        private bool ScheduledJobsComplete()
        {
            if (_exactMetadataJobScheduled && !_exactMetadataJobHandle.IsCompleted) return false;
            if (_exactClassificationJobScheduled && !_exactClassificationJobHandle.IsCompleted)
                return false;
            if (_densityJobScheduled && !_densityJobHandle.IsCompleted) return false;''',
'snapshot jobs completion check')

c = once(c,
'''        private void CompleteJobs()
        {
            if (_densityJobScheduled)
            {
                _densityJobHandle.Complete();''',
'''        private void CompleteJobs()
        {
            if (_exactMetadataJobScheduled)
            {
                _exactMetadataJobHandle.Complete();
                _exactMetadataJobScheduled = false;
            }
            if (_exactClassificationJobScheduled)
            {
                _exactClassificationJobHandle.Complete();
                _exactClassificationJobScheduled = false;
            }
            if (_densityJobScheduled)
            {
                _densityJobHandle.Complete();''',
'snapshot jobs completion')

# Dispose releases region leases after job completion.
c = once(c,
'''        public void Dispose()
        {
            CompleteJobs();
            ReleasePinnedSnapshotBlocksImmediate();''',
'''        public void Dispose()
        {
            CompleteJobs();
            ReleasePinnedRegionMetadataImmediate();
            ReleasePinnedSnapshotBlocksImmediate();''', 'dispose metadata leases')

cache_path.write_text(c)

# -----------------------------------------------------------------------------
# Tests and doc.
# -----------------------------------------------------------------------------
test_path = Path('Assets/Tests/EditMode/GeometryPipelineArchitectureTests.cs')
t = test_path.read_text()
t = t.replace(
'''            StringAssert.Contains("SnapshotCursor", cache);
            StringAssert.Contains("SnapshotBlocksPerDeadlineCheck", cache);
            StringAssert.Contains("Time.realtimeSinceStartupAsDouble >= deadlineSeconds", cache);''',
'''            StringAssert.Contains("ScheduleExactMetadataSnapshot", cache);
            StringAssert.Contains("_exactMetadataJobHandle.IsCompleted", cache);
            StringAssert.Contains("ExactMixedPinChecksPerDeadline", cache);
            StringAssert.Contains("Time.realtimeSinceStartupAsDouble >= deadlineSeconds", cache);''', 1)
if 'ExactBlockMetadataTraversalRunsInBurst' not in t:
    insert = r'''

        [Test]
        public void ExactBlockMetadataTraversalRunsInBurst()
        {
            string cache = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "CpuTransvoxelChunkCache.cs"));
            string jobs = ReadRenderingSource(
                Path.Combine("SurfaceExtraction", "Transvoxel", "ExactSnapshotMetadataJobs.cs"));
            StringAssert.Contains("ScheduleExactMetadataSnapshot", cache);
            StringAssert.Contains("ExactBrickMetadataRegionJob", jobs);
            StringAssert.Contains("ExactMixedBrickCompactJob", jobs);
            StringAssert.Contains("ExactSnapshotClassificationJob", jobs);
            StringAssert.Contains("IsPinnedRegionCurrent", cache);
            StringAssert.DoesNotContain("private TransvoxelDensityBrick SnapshotBlock", cache);
            StringAssert.DoesNotContain("private void ClassifySnapshotBrick", cache);
            StringAssert.DoesNotContain("SnapshotBlocksPerDeadlineCheck", cache);
        }
'''
    marker = '\n    }\n}'
    pos = t.rfind(marker)
    if pos < 0: raise SystemExit('architecture tests closing marker missing')
    t = t[:pos] + insert + t[pos:]
test_path.write_text(t)

# Progress doc: complete the exact metadata off-thread milestone and clean duplicate next-slice text.
doc_path = Path('docs/ASYNC_GEOMETRY_PIPELINE.md')
d = doc_path.read_text()
d = d.replace('- [ ] Move compact block-kind/ref snapshot traversal itself off the frame thread with versioned job-safe region metadata.\n',
              '- [x] Move compact block-kind/ref snapshot traversal itself off the frame thread with versioned job-safe region metadata.\n', 1)
d = d.replace('    - [ ] Schedule exact block-kind/ref classification in Burst and validate every pinned region revision before accepting output.\n',
              '    - [x] Schedule exact block-kind/ref classification in Burst and validate every pinned region revision before accepting output.\n', 1)
# Deduplicate the repeated current-next-slice line if still present.
lines = d.splitlines()
out = []
seen_next = False
for line in lines:
    if line.strip().startswith(('1. Move authoritative snapshot publication',
                                '2. Move authoritative snapshot publication',
                                '3. Move authoritative snapshot publication')):
        if seen_next:
            continue
        line = '1. Replace the remaining global-world build version checks with region/brick dependency revisions.'
        seen_next = True
    out.append(line)
d = '\n'.join(out) + ('\n' if d.endswith('\n') else '')
doc_path.write_text(d)

# Final static invariants.
cache = cache_path.read_text()
assert 'SnapshotBlocksPerDeadlineCheck' not in cache
assert 'private TransvoxelDensityBrick SnapshotBlock' not in cache
assert 'private void ClassifySnapshotBrick' not in cache
assert 'ScheduleExactMetadataSnapshot' in cache
assert '_exactMetadataJobHandle.IsCompleted' in cache
assert 'ExactMixedPinChecksPerDeadline' in cache
assert 'IsPinnedRegionCurrent' in cache
assert 'ExactSnapshotClassificationJob' in jobs_path.read_text()
assert 'ExactBrickMetadataRegionJob' in jobs_path.read_text()
