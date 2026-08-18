using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelEngine.Storage.Api;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction.Transvoxel;

namespace VoxelEngine.Rendering.Runtime.SurfaceExtraction
{
    /// <summary>
    /// CPU-authored feature-aware mesh cache for all solid voxel geometry.
    ///
    /// The near-field extractor samples every authoritative voxel. Curvature is reconstructed
    /// from local style rules, while collision and destruction continue to use discrete cells.
    ///
    /// Every non-liquid solid participates in this field. Surface semantics, rather than a
    /// brick-wide renderer classifier, control local reconstruction.
    /// </summary>
    public sealed class CpuTransvoxelChunkCache : IDisposable
    {
        private static readonly ProfilerMarker s_PrepareMarker =
            new("Voxel.Surface.WorkerPrepare");
        private static readonly ProfilerMarker s_SnapshotMarker =
            new("Voxel.Surface.Snapshot");
        private static readonly ProfilerMarker s_CompactMarker =
            new("Voxel.Surface.TopologyCompact");
        private static readonly ProfilerMarker s_FacetedMergeMarker =
            new("Voxel.Surface.FacetedMerge");
        private static readonly ProfilerMarker s_ProfileMarker =
            new("Voxel.Surface.ProfileEmit");
        private static readonly ProfilerMarker s_UploadMarker =
            new("Voxel.Surface.Upload");
        public const int CellsPerAxis = 64;

        // A chunk is always CellsPerAxis cells regardless of ring, so extraction work per
        // chunk is constant; SourceStep only widens the world extent each cell spans. That
        // asymmetry is what makes coarse rings cheaper per unit volume: the density grid,
        // topology tables, and faceted masks below are all sized by cells, not voxels.
        // Only the snapshot brick cache scales, because it must still cover the full extent.
        public readonly int SourceStep;
        public readonly int VoxelsPerAxis;
        public readonly int BricksPerAxis;

        /// <summary>
        /// True when this ring reads the region mip pyramid rather than individual voxels.
        /// Rings finer than one brick have no mip level to read and require resident bricks;
        /// see <see cref="VoxelReadGrid.LevelForStride"/>.
        /// </summary>
        public readonly bool SamplesFromMips;
        /// <summary>
        /// Step 8 keeps the exact versioned/COW snapshot boundary but compresses each 8^3 block
        /// into eight spatial 4^3 HLOD subcells before meshing. This replaces the expensive exact
        /// Transvoxel fallback without ever treating Storage's any-solid block projection as
        /// render density.
        /// </summary>
        public bool UsesBlockHlod => SourceStep == VoxelReadGrid.BlockEdge;
        private const int FeaturePreservingFallbackStep = VoxelReadGrid.BlockEdge / 2;
        private bool SupportsFeaturePreservingFallback =>
            SourceStep == FeaturePreservingFallbackStep;

        /// <summary>Chunk geometry of the base ring (SourceStep 1). Authoring and capture tools
        /// address the world in full-resolution chunks, so they want these rather than the
        /// ring-dependent instance values.</summary>
        public const int BaseSourceStep = 1;
        public const int BaseVoxelsPerAxis = CellsPerAxis * BaseSourceStep;
        public const int BaseBricksPerAxis = BaseVoxelsPerAxis / VoxelReadGrid.BlockEdge;

        private const int Padding = 1;
        private const int GridSize = CellsPerAxis + 3;
        private const int GridSampleCount = GridSize * GridSize * GridSize;
        private const int CellsPerSlice = 512;
        private const int BrickCachePadding = 1;
        private const int MaxExactSnapshotRegions = 27;
        private const int ExactMixedPinChecksPerDeadline = 16;
        private readonly int BrickCacheEdge;
        private readonly int BrickCacheCount;
        private const uint FullyLitOcclusion = 0x0000FF00u;

        private static readonly int s_SurfaceVertices = Shader.PropertyToID("_SurfaceVertices");
        private static readonly int s_SurfaceIndices = Shader.PropertyToID("_SurfaceIndices");
        private static readonly int s_SurfaceIndexBase = Shader.PropertyToID("_SurfaceIndexBase");
        private static readonly int s_SurfaceVertexBase = Shader.PropertyToID("_SurfaceVertexBase");

        public sealed class Entry : IDisposable
        {
            public int3 Coordinate { get; private set; }
            /// <summary>Voxels this chunk spans per axis — ring-dependent, so bounds and
            /// any consumer's world-space reasoning must use it rather than a constant.</summary>
            public readonly int VoxelsPerAxis;
            /// <summary>Voxels between adjacent samples in the ring that produced this entry.</summary>
            public readonly int SourceStep;
            private readonly SurfaceGeometryArena _arena;
            private SurfaceGeometryLease _liveLease;
            private SurfaceGeometryLease _stagingLease;
            public ComputeBuffer Vertices => _arena.Vertices;
            public ComputeBuffer Indices => _arena.Indices;
            public ComputeBuffer Args => _arena.Args;
            public bool Ready;
            public int IndexCount;
            public int LastUsedFrame;
            public long GpuBytes { get; private set; }
            public int VertexCapacity { get; private set; }
            public int IndexCapacity { get; private set; }
            public ulong SourceVersion { get; internal set; }
            public uint MaterialPaletteVersion { get; internal set; }
            public uint SurfaceCatalogueVersion { get; internal set; }
            public ulong SurfaceCatalogueHash { get; internal set; }
            public uint CoatingCatalogueVersion { get; internal set; }
            public ulong CoatingCatalogueHash { get; internal set; }

            internal Entry(int3 coordinate, int voxelsPerAxis, int sourceStep,
                           SurfaceGeometryArena arena)
            {
                Coordinate = coordinate;
                VoxelsPerAxis = voxelsPerAxis;
                SourceStep = sourceStep;
                _arena = arena ?? throw new ArgumentNullException(nameof(arena));
            }

            internal void Reinitialize(int3 coordinate)
            {
                if (Ready || _liveLease.IsValid || _stagingLease.IsValid)
                    throw new InvalidOperationException(
                        "A surface entry must release its arena leases before reuse.");
                Coordinate = coordinate;
                IndexCount = 0;
                LastUsedFrame = 0;
                GpuBytes = 0;
                VertexCapacity = 0;
                IndexCapacity = 0;
                SourceVersion = 0;
                MaterialPaletteVersion = 0;
                SurfaceCatalogueVersion = 0;
                SurfaceCatalogueHash = 0;
                CoatingCatalogueVersion = 0;
                CoatingCatalogueHash = 0;
                WaitingForArena = false;
                _stagingVertexCursor = 0;
                _stagingIndexCursor = 0;
            }

            private int _stagingVertexCursor;
            private int _stagingIndexCursor;
            internal bool WaitingForArena { get; private set; }

            internal int RemainingUploadBytes(int vertexCount, int indexCount)
            {
                int verticesRemaining = math.max(0, vertexCount - _stagingVertexCursor);
                int indicesRemaining = math.max(0, indexCount - _stagingIndexCursor);
                return verticesRemaining * SmoothSurfaceVertex.Stride
                     + indicesRemaining * sizeof(uint)
                     + SurfaceGeometryArena.ArgsWordsPerDraw * sizeof(uint);
            }

            internal bool AdvanceUpload(NativeList<SmoothSurfaceVertex> vertices,
                                        NativeList<uint> indices,
                                        int byteBudget,
                                        out int uploadedBytes)
            {
                uploadedBytes = 0;
                if (byteBudget <= 0 || !EnsureUploadStaging(vertices.Length, indices.Length))
                    return false;

                int remainingBudget = byteBudget;
                int vertexRemaining = vertices.Length - _stagingVertexCursor;
                if (vertexRemaining > 0 && remainingBudget >= SmoothSurfaceVertex.Stride)
                {
                    int count = math.min(vertexRemaining,
                        remainingBudget / SmoothSurfaceVertex.Stride);
                    _arena.UploadVertices(vertices.AsArray(), _stagingVertexCursor,
                                          in _stagingLease, count);
                    int bytes = count * SmoothSurfaceVertex.Stride;
                    _stagingVertexCursor += count;
                    remainingBudget -= bytes;
                    uploadedBytes += bytes;
                }

                int indexRemaining = indices.Length - _stagingIndexCursor;
                if (_stagingVertexCursor == vertices.Length && indexRemaining > 0
                    && remainingBudget >= sizeof(uint))
                {
                    int count = math.min(indexRemaining, remainingBudget / sizeof(uint));
                    _arena.UploadIndices(indices.AsArray(), _stagingIndexCursor,
                                         in _stagingLease, count);
                    int bytes = count * sizeof(uint);
                    _stagingIndexCursor += count;
                    remainingBudget -= bytes;
                    uploadedBytes += bytes;
                }

                const int argsBytes = SurfaceGeometryArena.ArgsWordsPerDraw * sizeof(uint);
                if (_stagingVertexCursor != vertices.Length
                    || _stagingIndexCursor != indices.Length
                    || remainingBudget < argsBytes)
                    return false;

                _arena.UploadArgs((uint)indices.Length, in _stagingLease);
                uploadedBytes += argsBytes;

                SurfaceGeometryLease previous = _liveLease;
                _liveLease = _stagingLease;
                _stagingLease = default;
                _stagingVertexCursor = 0;
                _stagingIndexCursor = 0;
                IndexCount = indices.Length;
                VertexCapacity = _liveLease.VertexCapacity;
                IndexCapacity = _liveLease.IndexCapacity;
                GpuBytes = _arena.ReservedBytes(in _liveLease);
                Ready = true;
                _arena.Release(in previous);
                return true;
            }

            private bool EnsureUploadStaging(int vertexCount, int indexCount)
            {
                if (_stagingLease.IsValid) return true;
                if (!_arena.TryAcquire(vertexCount, indexCount, out _stagingLease))
                {
                    WaitingForArena = true;
                    return false;
                }
                WaitingForArena = false;
                _stagingVertexCursor = 0;
                _stagingIndexCursor = 0;
                return true;
            }

            internal void CancelUpload()
            {
                _arena.Release(in _stagingLease);
                _stagingLease = default;
                WaitingForArena = false;
                _stagingVertexCursor = 0;
                _stagingIndexCursor = 0;
            }

            public Bounds WorldBounds(float voxelSize)
            {
                float size = VoxelsPerAxis * voxelSize;
                Vector3 min = new Vector3(Coordinate.x, Coordinate.y, Coordinate.z) * size;
                return new Bounds(min + Vector3.one * (size * 0.5f),
                                  Vector3.one * (size + SourceStep * voxelSize * 2f));
            }

            /// <summary>
            /// Binds this chunk's arena offsets and issues its indirect draw.
            ///
            /// <paramref name="properties"/> must contain nothing but the two offsets below: the
            /// block is copied into the command buffer once per draw, so anything constant across
            /// the pass belongs in global state instead. The vertex and index buffers are the same
            /// shared arena for every chunk, so they are bound once by the caller rather than here.
            /// </summary>
            public void Draw(CommandBuffer commandBuffer, Material material,
                             MaterialPropertyBlock properties)
            {
                if (!Ready || IndexCount == 0 || Vertices == null || Indices == null || Args == null)
                    return;

                properties.SetInt(s_SurfaceVertexBase, _liveLease.VertexStart);
                properties.SetInt(s_SurfaceIndexBase, _liveLease.IndexStart);
                commandBuffer.DrawProceduralIndirect(Matrix4x4.identity, material, 0,
                    MeshTopology.Triangles, _arena.Args,
                    _liveLease.ArgsWordStart * sizeof(uint), properties);
            }

            public void Dispose()
            {
                CancelUpload();
                _arena.Release(in _liveLease);
                _liveLease = default;
                Ready = false;
                IndexCount = 0;
                GpuBytes = 0;
                VertexCapacity = 0;
                IndexCapacity = 0;
            }
        }

        private struct BuildState
        {
            public bool Active;
            public int3 Coordinate;
            public int Phase;   // 0 snapshot, 1 jobs, 2 faceted, 3 profiles, 4 seams, 5 append, 6 pin release
            public int Cursor;
            public ulong SourceVersion;
            public uint SlotGeneration;
            public uint MaterialPaletteVersion;
            public uint SurfaceCatalogueVersion;
            public ulong SurfaceCatalogueHash;
            public uint CoatingCatalogueVersion;
            public ulong CoatingCatalogueHash;
            public bool SnapshotTaken;
            public bool SnapshotInitialised;
            public int SnapshotCursor;
            public double SnapshotCpuMs;
            public bool HasOwnedSolid;
            public bool RequiresContinuousTopology;
            public bool UsedFeaturePreservingFallback;
            public double BuildStartSeconds;
            public double DensityScheduledSeconds;
            public double TopologyScheduledSeconds;
            public double FacetedScheduledSeconds;
        }

        private readonly Dictionary<int3, Entry> _entries = new();
        private readonly Stack<Entry> _entryPool = new();
        private readonly SurfaceChunkSlotGrid _slotGrid;
        private readonly HashSet<int3> _known = new();
        private bool _clipmapWindowValid;
        private int3 _clipmapCenter;
        private int _clipmapRadius;
        // Camera motion retires only the slabs that left the previous clipmap window. The
        // traversal is resumable, so even a teleport never turns residency cleanup into a scan
        // of every known chunk or a full old-window walk in one frame.
        private const int ClipmapEdgeCandidatesPerPrepare = 32;
        private bool _clipmapEdgeRetirementPending;
        private int3 _clipmapRetirementFromCenter;
        private int3 _clipmapRetirementToCenter;
        private int _clipmapRetirementRadius;
        private int _clipmapRetirementAxis;
        private int _clipmapRetirementDepth;
        private int _clipmapRetirementPlaneCursor;
        // Known-chunk liveness is maintained incrementally. A full HashSet scan in every worker
        // turns residency pressure into O(world-residency) frame work, so each known chunk owns
        // one round-robin queue record instead.
        private readonly Queue<int3> _residencyQueue = new();
        private readonly HashSet<int3> _queuedResidency = new();
        private const int ResidencyChecksPerPrepare = 32;

        // Full-region invalidations (journal overflow, residency publication, atomic world swap)
        // are also incremental. Fine-grained edits continue to use the brick path immediately.
        private readonly Queue<int3> _regionInvalidationQueue = new();
        private readonly HashSet<int3> _queuedRegionInvalidations = new();
        private readonly HashSet<int3> _rescanRegionInvalidations = new();
        private const int RegionInvalidationCandidatesPerPrepare = 64;
        private bool _hasActiveRegionInvalidation;
        private int3 _activeRegionInvalidation;
        private int3 _activeRegionMinChunk;
        private int3 _activeRegionChunkCounts;
        private int _activeRegionCandidateCursor;

        private readonly HashSet<int3> _dirty = new();
        // Dirty work is also kept in a persistent FIFO. The HashSet remains the authoritative
        // membership/coalescing structure; the queue gives build admission bounded incremental
        // traversal instead of rescanning every dirty chunk whenever one workspace becomes free.
        private readonly Queue<int3> _dirtyQueue = new();
        private readonly HashSet<int3> _queuedDirty = new();
        // Missing/stale chunks that are inside the actual camera frustum get a second queue
        // record. This never changes authoritative dirty membership or the global frame budget;
        // it only prevents thousands of valid 360-degree prefetch records from delaying a hole
        // the player can already see. Stale priority records are harmless and self-pruning.
        private readonly Queue<int3> _visibleDirtyQueue = new();
        private readonly HashSet<int3> _queuedVisibleDirty = new();
        private const int BuildSelectionCandidatesPerSlice = 64;
        private const int VisibleBuildSelectionCandidatesPerSlice = 8;
        private readonly Dictionary<int3, ulong> _desiredVersions = new();
        // Chunks whose last completed build produced no geometry, and the source version that
        // proved it. They hold no Entry and no GPU memory, so they cost a dictionary slot
        // rather than a resident chunk, and they stay out of the dirty set until invalidated.
        private readonly Dictionary<int3, ulong> _emptyVersions = new();
        private readonly Dictionary<int3, double> _queuedAtSeconds = new();
        private ulong _versionCounter;
        private readonly List<Entry> _visible = new();
        private readonly Plane[] _frustumPlanes = new Plane[6];

        // Heavy persistent native memory is lifecycle-owned by the reusable build workspace.
        // These handles are borrowed aliases kept only to avoid obscuring the job setup below.
        private readonly TransvoxelBuildWorkspace _workspace;
        private readonly NativeArray<float> _density;
        private readonly NativeArray<byte> _materials;
        private readonly NativeArray<uint> _surfaceSemantics;
        private readonly NativeArray<byte> _boundarySamples;
        private NativeArray<TransvoxelDensityBrick> _densityBricks;
        // Coarse-ring snapshot: one mip cell per lattice sample. Fixed size regardless of how
        // much world the chunk covers, unlike the brick cache it replaces.
        private NativeArray<byte> _mipSampleOccupancy;
        private NativeArray<byte> _mipSampleMaterials;
        private NativeList<byte> _densityMixedVoxels;
        private NativeList<ushort> _densityMixedSurfaceSemantics;
        private NativeList<byte> _densityMixedBoundarySamples;
        private NativeList<VoxelReadPinToken> _pinnedReadBlocks;
        private IRegionReadSource _pinnedReadSource;
        private NativeArray<byte> _pinnedMixedVoxels;
        private NativeArray<ushort> _pinnedMixedSurfaceSemantics;
        private NativeArray<byte> _pinnedMixedBoundarySamples;
        private int _pinnedReleaseCursor;
        private bool _discardBuildAfterPinRelease;
        private readonly PinnedRegionBlockRefs[] _pinnedRegionBlockRefs =
            new PinnedRegionBlockRefs[MaxExactSnapshotRegions];
        private IRegionReadSource _pinnedRegionSource;
        private int _pinnedRegionCount;
        private NativeArray<byte> _exactMixedFlags;
        private NativeList<int> _exactMixedBrickIndices;
        private NativeArray<byte> _snapshotClassificationFlags;
        private NativeArray<SurfaceBlockHlodSummary> _hlodSummaries;
        private NativeArray<byte> _hlodMaskScratch;
        private NativeArray<int> _hlodOverflow;
        private JobHandle _hlodJobHandle;
        private bool _hlodJobScheduled;
        private JobHandle _exactMetadataJobHandle;
        private bool _exactMetadataJobScheduled;
        private bool _exactMetadataReady;
        private ExactSnapshotRegionCoverage _exactMetadataRegionCoverage;
        private JobHandle _exactClassificationJobHandle;
        private bool _exactClassificationJobScheduled;
        private int _exactMixedPinCursor;
        private JobHandle _densityJobHandle;
        private bool _densityJobScheduled;
        private JobHandle _topologyJobHandle;
        private bool _topologyJobScheduled;
        private JobHandle _topologyCompactJobHandle;
        private bool _topologyCompactJobScheduled;
        private JobHandle _facetedMaskJobHandle;
        private bool _facetedMaskJobScheduled;
        private JobHandle _facetedMergeJobHandle;
        private bool _facetedMergeJobScheduled;
        private JobHandle _transitionJobHandle;
        private bool _transitionJobScheduled;
        private NativeArray<byte> _topologyCellClass;
        private NativeArray<byte> _topologyGeometryCounts;
        private NativeArray<byte> _topologyCellVertexIndices;
        private NativeArray<ushort> _topologyEdgeCodes;
        private NativeStream _topologyOutput;
        private NativeList<SmoothSurfaceVertex> _compactedTopologyVertices;
        private NativeList<uint> _compactedTopologyIndices;
        private NativeArray<int> _topologyOverflowCell;
        // Transition-cell tables, uploaded once per cache. Only coarse rings stitch faces, but
        // the tables are a few kilobytes and sharing the allocation keeps the build path simple.
        /// <summary>Half-stride samples per axis on a transition face: two per coarse cell,
        /// plus one to close the last cell.</summary>
        private const int FaceSamplesPerAxis = CellsPerAxis * 2 + 1;
        private NativeArray<float> _faceDensity;
        private NativeArray<byte> _faceMaterials;
        private NativeArray<uint> _faceSurfaces;
        private NativeArray<byte> _transitionCellClass;
        private NativeArray<byte> _transitionGeometryCounts;
        private NativeArray<byte> _transitionCellIndices;
        private NativeArray<ushort> _transitionVertexData;
        private int _transitionVertexStride;
        private int _transitionIndexStride;
        private NativeList<SmoothSurfaceVertex> _transitionVertices;
        private NativeList<uint> _transitionIndices;
        private int _transitionFace = -1;
        private int _transitionSampleCursor;
        private NativeArray<uint> _facetedMasks;
        private NativeList<SmoothSurfaceVertex> _facetedVertices;
        private NativeList<uint> _facetedIndices;
        private const int AppendElementsPerDeadlineCheck = 512;
        private int _resultAppendStage;
        private int _topologyAppendVertexCursor;
        private int _topologyAppendIndexCursor;
        private uint _topologyAppendVertexBase;
        private int _facetedAppendVertexCursor;
        private int _facetedAppendIndexCursor;
        private uint _facetedAppendVertexBase;
        private bool _transitionResultPending;
        private int _transitionAppendVertexCursor;
        private int _transitionAppendIndexCursor;
        private uint _transitionAppendVertexBase;

        private readonly float[] _cellDensity = new float[8];
        private readonly byte[] _cellMaterial = new byte[8];
        private readonly uint[] _cellSurface = new uint[8];
        private readonly byte[] _cellBoundary = new byte[8];
        private readonly SmoothSurfaceVertex[] _cellVertices = new SmoothSurfaceVertex[16];
        private readonly uint[] _faceMask = new uint[CellsPerAxis * CellsPerAxis];
        // Final build output stays in persistent native memory from Burst completion through
        // bounded arena upload. Streaming must not grow managed geometry Lists on the frame path.
        private NativeList<SmoothSurfaceVertex> _vertices;
        private NativeList<uint> _indices;
        private BuildState _build;
        private bool _pendingUpload;
        private SurfaceGeometryArena _geometryArena;
        private readonly bool _ownsGeometryArena;
        private TransvoxelLookupTables _lookupTables;
        private readonly bool _ownsLookupTables;
        private SurfaceCatalogueView _surfaceCatalogue;
        private SurfaceCatalogueView _buildSurfaceCatalogue;
        private CoatingCatalogueView _coatingCatalogue;
        private CoatingCatalogueView _buildCoatingCatalogue;
        private MaterialPaletteView _buildPalette;
        private uint _materialPaletteVersion;
        private ProfileBlock[] _profileBlocks = Array.Empty<ProfileBlock>();
        private ProfileBlock[] _buildProfileBlocks = Array.Empty<ProfileBlock>();
        private readonly Dictionary<int3, ProfileBlock[]> _profileBlocksByChunk = new();
        private IProfileBlockReadSource _profileBlockStore;
        private uint _profileBlockVersion;
        private readonly VoxelTimingWindow _snapshotTiming = new();
        private readonly VoxelTimingWindow _densityTurnaroundTiming = new();
        private readonly VoxelTimingWindow _topologyTurnaroundTiming = new();
        private readonly VoxelTimingWindow _topologyCompactTiming = new();
        private readonly VoxelTimingWindow _facetedTurnaroundTiming = new();
        private readonly VoxelTimingWindow _facetedMergeTiming = new();
        private readonly VoxelTimingWindow _profileEmitTiming = new();
        private readonly VoxelTimingWindow _uploadTiming = new();
        private readonly VoxelTimingWindow _queueLatencyTiming = new();
        private readonly VoxelTimingWindow _buildLatencyTiming = new();
        private readonly VoxelTimingWindow _ruleSyncTiming = new();
        private readonly VoxelTimingWindow _residencyPruneTiming = new();
        private readonly VoxelTimingWindow _capacityTiming = new();
        private readonly VoxelTimingWindow _buildSelectionTiming = new();

        public CpuTransvoxelChunkCache(int sourceStep = 1)
            : this(sourceStep, null, true, null, true, null)
        {
        }

        internal CpuTransvoxelChunkCache(int sourceStep, SurfaceGeometryArena geometryArena,
                                         TransvoxelLookupTables lookupTables)
            : this(sourceStep, geometryArena, false, lookupTables, false, null)
        {
        }

        internal CpuTransvoxelChunkCache(int sourceStep, SurfaceGeometryArena geometryArena,
                                         TransvoxelLookupTables lookupTables,
                                         SurfaceChunkSlotGrid slotGrid)
            : this(sourceStep, geometryArena, false, lookupTables, false, slotGrid)
        {
        }

        private CpuTransvoxelChunkCache(int sourceStep, SurfaceGeometryArena geometryArena,
                                         bool ownsGeometryArena,
                                         TransvoxelLookupTables lookupTables,
                                         bool ownsLookupTables,
                                         SurfaceChunkSlotGrid slotGrid)
        {
            _geometryArena = geometryArena;
            _ownsGeometryArena = ownsGeometryArena;
            _lookupTables = lookupTables ?? new TransvoxelLookupTables();
            _slotGrid = slotGrid ?? new SurfaceChunkSlotGrid();
            _ownsLookupTables = ownsLookupTables || lookupTables == null;
            if (sourceStep < 1 || (sourceStep & (sourceStep - 1)) != 0)
                throw new ArgumentOutOfRangeException(
                    nameof(sourceStep), sourceStep,
                    "Source step must be a positive power of two; chunk coordinates and brick "
                  + "decomposition rely on shifts.");
            SourceStep = sourceStep;
            VoxelsPerAxis = CellsPerAxis * sourceStep;
            BricksPerAxis = VoxelsPerAxis / VoxelReadGrid.BlockEdge;
            // A ring whose stride reaches a whole brick or more reads the mip pyramid instead
            // of caching bricks; its brick cache would grow with the cube of the stride and is
            // never allocated.
            SamplesFromMips = VoxelReadGrid.LevelForStride(sourceStep) >= 0;
            BrickCacheEdge = SamplesFromMips ? 0 : BricksPerAxis + BrickCachePadding * 2;
            BrickCacheCount = BrickCacheEdge * BrickCacheEdge * BrickCacheEdge;
            _surfaceCatalogue = SurfaceCatalogueView.CreateBuiltIns();
            _coatingCatalogue = CoatingCatalogueView.CreateBuiltIns();
            _workspace = new TransvoxelBuildWorkspace(
                GridSampleCount, BrickCacheCount, SamplesFromMips, UsesBlockHlod,
                SupportsFeaturePreservingFallback, BricksPerAxis, CellsPerAxis,
                FaceSamplesPerAxis);
            _density = _workspace.Density;
            _materials = _workspace.Materials;
            _surfaceSemantics = _workspace.SurfaceSemantics;
            _boundarySamples = _workspace.BoundarySamples;
            _densityBricks = _workspace.DensityBricks;
            _mipSampleOccupancy = _workspace.MipSampleOccupancy;
            _mipSampleMaterials = _workspace.MipSampleMaterials;
            _densityMixedVoxels = _workspace.DensityMixedVoxels;
            _densityMixedSurfaceSemantics = _workspace.DensityMixedSurfaceSemantics;
            _densityMixedBoundarySamples = _workspace.DensityMixedBoundarySamples;
            _pinnedReadBlocks = _workspace.PinnedReadBlocks;
            _exactMixedFlags = _workspace.ExactMixedFlags;
            _exactMixedBrickIndices = _workspace.ExactMixedBrickIndices;
            _snapshotClassificationFlags = _workspace.SnapshotClassificationFlags;
            _hlodSummaries = _workspace.HlodSummaries;
            _hlodMaskScratch = _workspace.HlodMaskScratch;
            _hlodOverflow = _workspace.HlodOverflow;
            _compactedTopologyVertices = _workspace.CompactedTopologyVertices;
            _compactedTopologyIndices = _workspace.CompactedTopologyIndices;
            _topologyOverflowCell = _workspace.TopologyOverflowCell;
            _topologyCellClass = _lookupTables.RegularCellClass;
            _topologyGeometryCounts = _lookupTables.RegularGeometryCounts;
            _topologyCellVertexIndices = _lookupTables.RegularCellVertexIndices;
            _topologyEdgeCodes = _lookupTables.RegularEdgeCodes;
            _facetedMasks = _workspace.FacetedMasks;
            _facetedVertices = _workspace.FacetedVertices;
            _facetedIndices = _workspace.FacetedIndices;
            _vertices = _workspace.Vertices;
            _indices = _workspace.Indices;
            _faceDensity = _workspace.FaceDensity;
            _faceMaterials = _workspace.FaceMaterials;
            _faceSurfaces = _workspace.FaceSurfaces;
            _transitionVertices = _workspace.TransitionVertices;
            _transitionIndices = _workspace.TransitionIndices;
            _transitionCellClass = _lookupTables.TransitionCellClass;
            _transitionGeometryCounts = _lookupTables.TransitionGeometryCounts;
            _transitionCellIndices = _lookupTables.TransitionCellIndices;
            _transitionVertexData = _lookupTables.TransitionVertexData;
            _transitionVertexStride = _lookupTables.TransitionVertexStride;
            _transitionIndexStride = _lookupTables.TransitionIndexStride;
        }

        /// <summary>
        /// Advances transition-face generation without ever waiting for an unfinished job.
        /// Face snapshots are sliced by the worker deadline, then the Burst transition mesh
        /// runs asynchronously. The previous ready chunk remains published until every seam
        /// face for the replacement has completed.
        /// </summary>
        private bool StepTransitionFaces(IRegionReadSource source,
                                         in MaterialPaletteView palette,
                                         Camera camera, float voxelSize,
                                         double deadlineSeconds)
        {
            if (MinViewDistanceMetres <= 0f || camera == null) return true;

            if (_transitionJobScheduled)
            {
                if (!_transitionJobHandle.IsCompleted) return false;

                // Completion is non-blocking because IsCompleted was observed above. The result
                // is now CPU-owned, but merging it into final output is itself budgeted.
                if (!GeometryFrameJobCompletionGuard.TryCompleteReady(
                        _transitionJobHandle, ref _framePathBlockingCompletionViolations))
                    return false;
                _transitionJobScheduled = false;
                _transitionResultPending = true;
                _transitionAppendVertexCursor = 0;
                _transitionAppendIndexCursor = 0;
                _transitionAppendVertexBase = 0;
            }

            if (_transitionResultPending)
            {
                if (!StepAppendNativeGeometry(_transitionVertices.AsArray(),
                                              _transitionIndices.AsArray(),
                                              ref _transitionAppendVertexCursor,
                                              ref _transitionAppendIndexCursor,
                                              ref _transitionAppendVertexBase,
                                              deadlineSeconds))
                    return false;

                _transitionResultPending = false;
                _build.Cursor = _transitionFace + 1;
                _transitionFace = -1;
                _transitionSampleCursor = 0;
            }

            Vector3 cameraPosition = camera.transform.position;
            while (_build.Cursor < 6)
            {
                int face = _build.Cursor;
                if (!FaceNeedsTransition(_build.Coordinate, face, voxelSize,
                                         cameraPosition))
                {
                    _build.Cursor++;
                    continue;
                }

                if (_transitionFace != face)
                {
                    _transitionFace = face;
                    _transitionSampleCursor = 0;
                }

                if (!StepTransitionFaceSnapshot(source, in palette, face,
                                                deadlineSeconds))
                    return false;

                _transitionVertices.Clear();
                _transitionIndices.Clear();
                var job = new TransitionMeshJob
                {
                    FaceDensity = _faceDensity,
                    FaceMaterials = _faceMaterials,
                    FaceSurfaces = _faceSurfaces,
                    FaceSamplesPerAxis = FaceSamplesPerAxis,
                    TransitionCellClass = _transitionCellClass,
                    TransitionGeometryCounts = _transitionGeometryCounts,
                    TransitionCellIndices = _transitionCellIndices,
                    TransitionVertexData = _transitionVertexData,
                    VertexDataStride = _transitionVertexStride,
                    CellIndexStride = _transitionIndexStride,
                    Vertices = _transitionVertices,
                    Indices = _transitionIndices,
                    ChunkOriginVoxel = _build.Coordinate * VoxelsPerAxis,
                    CellsPerAxis = CellsPerAxis,
                    SourceStep = SourceStep,
                    VoxelSize = voxelSize,
                    Face = face,
                };
                _transitionJobHandle = job.Schedule();
                _transitionJobScheduled = true;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Snapshots a transition face at the finer neighbour's sample spacing without
        /// monopolising the frame. Region read views are borrowed only inside this call;
        /// no borrowed Storage state survives across frames while the snapshot is sliced.
        /// </summary>
        private bool StepTransitionFaceSnapshot(IRegionReadSource source,
                                                in MaterialPaletteView palette, int face,
                                                double deadlineSeconds)
        {
            if (Time.realtimeSinceStartupAsDouble >= deadlineSeconds) return false;

            int axis = face >> 1;
            bool positive = (face & 1) != 0;
            int3 uAxis, vAxis;
            switch (axis)
            {
                case 0: uAxis = new int3(0, 1, 0); vAxis = new int3(0, 0, 1); break;
                case 1: uAxis = new int3(0, 0, 1); vAxis = new int3(1, 0, 0); break;
                default: uAxis = new int3(1, 0, 0); vAxis = new int3(0, 1, 0); break;
            }

            int3 chunkOrigin = _build.Coordinate * VoxelsPerAxis;
            int3 faceOrigin = chunkOrigin;
            if (positive) faceOrigin[axis] += VoxelsPerAxis;

            int halfStep = math.max(1, SourceStep / 2);
            int mipLevel = VoxelReadGrid.LevelForStride(halfStep);
            int sampleCount = FaceSamplesPerAxis * FaceSamplesPerAxis;
            RegionSampleCursor cursor = default;
            const int SamplesPerDeadlineCheck = 64;

            while (_transitionSampleCursor < sampleCount)
            {
                int end = math.min(sampleCount,
                                   _transitionSampleCursor + SamplesPerDeadlineCheck);
                for (; _transitionSampleCursor < end; _transitionSampleCursor++)
                {
                    int index = _transitionSampleCursor;
                    int u = index % FaceSamplesPerAxis;
                    int v = index / FaceSamplesPerAxis;
                    int3 voxel = faceOrigin
                               + uAxis * (u * halfStep)
                               + vAxis * (v * halfStep);

                    bool occupied = false;
                    byte material = VoxelGrid.MaterialEmpty;
                    if (TrySampleWorld(source, ref cursor, voxel, mipLevel,
                                       out bool sampled,
                                       out byte sampledMaterial))
                    {
                        occupied = sampled;
                        material = sampledMaterial;
                    }

                    _faceDensity[index] = occupied ? 0.5f : -0.5f;
                    _faceMaterials[index] = material;
                    _faceSurfaces[index] = occupied
                        ? palette.GetDefaultSurfaceStyle(material) : 0u;
                }

                if (_transitionSampleCursor < sampleCount
                    && Time.realtimeSinceStartupAsDouble >= deadlineSeconds)
                    return false;
            }

            return true;
        }

        public int MaxResidentChunks { get; set; } = 4096;
        /// <summary>
        /// Outer edge of this ring's band. Beyond it the next coarser ring takes over.
        /// </summary>
        public float MaxViewDistanceMetres { get; set; } = 96f;

        /// <summary>
        /// Inner edge of this ring's band. A chunk lying entirely inside it belongs to a finer
        /// ring and is neither drawn nor built here, so the rings partition the view rather
        /// than overlapping. Zero for the innermost ring.
        ///
        /// The test is against the chunk's *farthest* corner: a chunk is surrendered only once
        /// all of it is within the finer ring's reach, so a chunk straddling the boundary is
        /// still drawn here and the seam never opens into a gap.
        /// </summary>
        public float MinViewDistanceMetres { get; set; }
        public int ShardIndex { get; set; }
        public int ShardCount { get; set; } = 1;
        public int ResidentCount => _entries.Count;
        public int KnownCount => _known.Count;
        public int SlotCount => _known.Count;
        /// <summary>Number of exact-snapshot brick records reserved by this build workspace.</summary>
        public int SnapshotBrickCapacity => BrickCacheCount;
        public int DirtyCount => _dirty.Count + (_build.Active ? 1 : 0);
        public ulong ActiveSurfaceCatalogueHash => _surfaceCatalogue.CatalogueHash;
        public ulong CompletedBuildCount { get; private set; }
        public ulong StaleBuildCount { get; private set; }
        public ulong ExactMetadataScheduleCount { get; private set; }
        public ulong ExactMetadataCompleteCount { get; private set; }
        public ulong ExactMetadataRevisionRejectCount { get; private set; }
        public ulong ExactMetadataPinRejectCount { get; private set; }
        // Step-4 false-empty fallback lifecycle diagnostics. These counters do not affect
        // admission or publication; they distinguish policy selection, worker output and final
        // visibility when a coarse exact-owned chunk disappears in production.
        public ulong FeaturePreservingFallbackScheduleCount { get; private set; }
        public ulong FeaturePreservingFallbackCompleteCount { get; private set; }
        public ulong FeaturePreservingFallbackNonEmptyCount { get; private set; }
        public ulong FeaturePreservingFallbackPublishCount { get; private set; }
        // Last visibility pass diagnostics. These counters are reset by BeginVisibilityCollection
        // and never participate in scheduling; they distinguish ring ownership, frustum routing,
        // current-ready and current-empty states when a production LOD disappears.
        public int LastVisibilityKnownCount { get; private set; }
        public int LastVisibilityInBandCount { get; private set; }
        public int LastVisibilityFrustumCount { get; private set; }
        public int LastVisibilityReadyCount { get; private set; }
        public int LastVisibilityEmptyCount { get; private set; }
        public ulong MaterialPaletteInvalidationCount { get; private set; }
        public ulong SurfaceCatalogueInvalidationCount { get; private set; }
        public ulong CoatingCatalogueInvalidationCount { get; private set; }
        public ulong ProfileBlockInvalidationCount { get; private set; }
        public ulong UploadedGeometryBytes { get; private set; }
        public ulong CompletedDecorationClumps { get; private set; }
        public int MissingVisibleCount { get; private set; }
        public ulong CapacityPressureCount { get; private set; }
        private ulong _framePathBlockingCompletionViolations;
        public ulong FramePathBlockingCompletionViolations => _framePathBlockingCompletionViolations;
        public int RunningJobCount => _exactMetadataJobScheduled || _exactClassificationJobScheduled
                                   || _hlodJobScheduled || _densityJobScheduled
                                   || _topologyJobScheduled || _facetedMaskJobScheduled
                                   || _transitionJobScheduled
                                    ? 1 : 0;
        // Allocation-free diagnostics used by renderer telemetry/tests to distinguish a genuinely
        // active coarse build from a known chunk that fell out of the work lifecycle. Bits are
        // deliberately stable and local to this cache; they do not influence scheduling.
        public int ActiveBuildPhase => _build.Active ? _build.Phase : -1;
        public uint ActiveJobMask =>
            (_exactMetadataJobScheduled ? 1u << 0 : 0u)
          | (_exactClassificationJobScheduled ? 1u << 1 : 0u)
          | (_hlodJobScheduled ? 1u << 2 : 0u)
          | (_densityJobScheduled ? 1u << 3 : 0u)
          | (_topologyJobScheduled || _topologyCompactJobScheduled ? 1u << 4 : 0u)
          | (_facetedMaskJobScheduled || _facetedMergeJobScheduled ? 1u << 5 : 0u)
          | (_transitionJobScheduled ? 1u << 6 : 0u);
        public int PendingUploadCount => _pendingUpload ? 1 : 0;
        public int PendingUploadBytes
        {
            get
            {
                if (!_pendingUpload
                    || !_entries.TryGetValue(_build.Coordinate, out Entry entry))
                    return 0;
                return entry.RemainingUploadBytes(_vertices.Length, _indices.Length);
            }
        }
        public double LastSnapshotMs { get; private set; }
        public double LastTopologyCompactMs { get; private set; }
        public double LastUploadMs { get; private set; }
        public VoxelTimingSummary SnapshotTiming => _snapshotTiming.Snapshot();
        public VoxelTimingSummary DensityTurnaroundTiming => _densityTurnaroundTiming.Snapshot();
        public VoxelTimingSummary TopologyJobTurnaroundTiming => _topologyTurnaroundTiming.Snapshot();
        public VoxelTimingSummary TopologyCompactTiming => _topologyCompactTiming.Snapshot();
        public VoxelTimingSummary FacetedJobTurnaroundTiming => _facetedTurnaroundTiming.Snapshot();
        public VoxelTimingSummary FacetedMergeTiming => _facetedMergeTiming.Snapshot();
        public VoxelTimingSummary ProfileEmitTiming => _profileEmitTiming.Snapshot();
        public VoxelTimingSummary UploadTiming => _uploadTiming.Snapshot();
        public VoxelTimingSummary QueueLatencyTiming => _queueLatencyTiming.Snapshot();
        public VoxelTimingSummary BuildLatencyTiming => _buildLatencyTiming.Snapshot();
        public VoxelTimingSummary RuleSyncTiming => _ruleSyncTiming.Snapshot();
        public VoxelTimingSummary ResidencyPruneTiming => _residencyPruneTiming.Snapshot();
        public VoxelTimingSummary CapacityTiming => _capacityTiming.Snapshot();
        public VoxelTimingSummary BuildSelectionTiming => _buildSelectionTiming.Snapshot();
        public long ResidentGpuBytes
        {
            get
            {
                long total = 0;
                foreach (Entry entry in _entries.Values) total += entry.GpuBytes;
                return total;
            }
        }
        public IReadOnlyList<Entry> Visible => _visible;

        public bool OwnsRenderedChunk(int3 coordinate) =>
            _entries.TryGetValue(coordinate, out Entry entry) && entry.Ready;

        public int IndexedProfileBlockCount(int3 coordinate) =>
            _profileBlocksByChunk.TryGetValue(coordinate, out ProfileBlock[] blocks)
                ? blocks.Length : 0;

        /// <summary>
        /// Whether a chunk's sampled extent, including its one-sample halo, reaches into a
        /// region. The halo is a full <paramref name="sourceStep"/> wide, so a coarse ring
        /// reaches further past its own bounds than the base ring does.
        /// </summary>
        public static bool ChunkOverlapsRegion(int3 chunk, int3 region,
                                               int voxelsPerAxis = BaseVoxelsPerAxis,
                                               int sourceStep = BaseSourceStep)
        {
            int3 chunkMin = chunk * voxelsPerAxis - Padding * sourceStep;
            int3 chunkMax = (chunk + 1) * voxelsPerAxis + Padding * sourceStep;
            int3 regionMin = region * VoxelGrid.RegionVoxelEdge;
            int3 regionMax = regionMin + VoxelGrid.RegionVoxelEdge;
            return !math.any(chunkMax <= regionMin) && !math.any(chunkMin >= regionMax);
        }

        /// <summary>
        /// Admits chunks discovered from immutable Storage surface summaries. Discovery is not a
        /// mutation signal: once a chunk is known, its build snapshots the entire authoritative
        /// chunk, so later 512-brick publication slices from the same unchanged region must not
        /// advance its source generation and kill in-flight geometry. Real voxel edits continue
        /// through <see cref="InvalidateSurfaceBricks"/> and region invalidation below.
        /// Returns the number of newly admitted chunks.
        /// </summary>
        internal int DiscoverSurfaceBricks(IReadOnlyList<int3> worldBricks)
        {
            if (worldBricks == null) return 0;
            int admitted = 0;

            for (int i = 0; i < worldBricks.Count; i++)
            {
                int3 brick = worldBricks[i];
                int3 baseChunk = new(FloorDiv(brick.x, BricksPerAxis),
                                     FloorDiv(brick.y, BricksPerAxis),
                                     FloorDiv(brick.z, BricksPerAxis));
                int rx = FloorMod(brick.x, BricksPerAxis);
                int ry = FloorMod(brick.y, BricksPerAxis);
                int rz = FloorMod(brick.z, BricksPerAxis);

                int minX = rx == 0 ? -1 : 0;
                int maxX = rx == BricksPerAxis - 1 ? 1 : 0;
                int minY = ry == 0 ? -1 : 0;
                int maxY = ry == BricksPerAxis - 1 ? 1 : 0;
                int minZ = rz == 0 ? -1 : 0;
                int maxZ = rz == BricksPerAxis - 1 ? 1 : 0;

                for (int z = minZ; z <= maxZ; z++)
                for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    int3 chunk = baseChunk + new int3(x, y, z);
                    if (!OwnsShard(chunk) || _known.Contains(chunk)) continue;
                    if (!TrackKnown(chunk)) continue;

                    // Discovery establishes authoritative source state, not immediate build
                    // demand. Every LOD ring learns the same surface summaries, but only the
                    // ring currently owning this chunk should consume the renderer-wide build
                    // budget. CollectVisibleCoordinate activates in-band demand before worker
                    // admission; retaining only the desired generation here prevents thousands
                    // of finer/coarser off-band chunks from filling the dirty FIFO at startup.
                    _desiredVersions[chunk] = ++_versionCounter;
                    admitted++;
                }
            }
            return admitted;
        }

        /// <summary>
        /// Invalidates chunks touched by an authoritative voxel change. Unlike surface discovery,
        /// this path intentionally advances already-known chunk generations so active/ready
        /// geometry cannot publish stale voxel content.
        /// The one-sample Transvoxel padding can consume a neighbouring chunk's edge,
        /// so face/edge/corner neighbours are dirtied only when the brick lies on a chunk border.
        /// </summary>
        public void InvalidateSurfaceBricks(IReadOnlyList<int3> worldBricks)
        {
            if (worldBricks == null) return;

            for (int i = 0; i < worldBricks.Count; i++)
            {
                int3 brick = worldBricks[i];
                int3 baseChunk = new(FloorDiv(brick.x, BricksPerAxis),
                                     FloorDiv(brick.y, BricksPerAxis),
                                     FloorDiv(brick.z, BricksPerAxis));
                int rx = FloorMod(brick.x, BricksPerAxis);
                int ry = FloorMod(brick.y, BricksPerAxis);
                int rz = FloorMod(brick.z, BricksPerAxis);

                int minX = rx == 0 ? -1 : 0;
                int maxX = rx == BricksPerAxis - 1 ? 1 : 0;
                int minY = ry == 0 ? -1 : 0;
                int maxY = ry == BricksPerAxis - 1 ? 1 : 0;
                int minZ = rz == 0 ? -1 : 0;
                int maxZ = rz == BricksPerAxis - 1 ? 1 : 0;

                for (int z = minZ; z <= maxZ; z++)
                for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    int3 chunk = baseChunk + new int3(x, y, z);
                    if (!OwnsShard(chunk) || !TrackKnown(chunk)) continue;
                    Invalidate(chunk);
                }
            }
        }

        /// <summary>
        /// Consumes scheduler-local regions derived from the versioned change journal. Existing
        /// chunks in or adjacent to an edited region are rebuilt; the old ready mesh stays alive
        /// until the replacement is uploaded.
        /// </summary>
        public void InvalidateDirtyRegions(HashSet<int3> dirtyRegions)
        {
            if (dirtyRegions != null)
            {
                foreach (int3 region in dirtyRegions)
                {
                    if (_hasActiveRegionInvalidation && region.Equals(_activeRegionInvalidation))
                    {
                        _rescanRegionInvalidations.Add(region);
                        continue;
                    }
                    if (_queuedRegionInvalidations.Add(region))
                        _regionInvalidationQueue.Enqueue(region);
                }
            }

            StepRegionInvalidation();
        }

        private void StepRegionInvalidation()
        {
            int remaining = RegionInvalidationCandidatesPerPrepare;
            while (remaining > 0)
            {
                if (!_hasActiveRegionInvalidation)
                {
                    if (_regionInvalidationQueue.Count == 0) return;
                    _activeRegionInvalidation = _regionInvalidationQueue.Dequeue();
                    _hasActiveRegionInvalidation = true;
                    _activeRegionCandidateCursor = 0;

                    int halo = Padding * SourceStep;
                    int3 regionMin = _activeRegionInvalidation * VoxelGrid.RegionVoxelEdge;
                    int3 regionMax = regionMin + VoxelGrid.RegionVoxelEdge;
                    _activeRegionMinChunk = new int3(
                        FloorDiv(regionMin.x - halo, VoxelsPerAxis),
                        FloorDiv(regionMin.y - halo, VoxelsPerAxis),
                        FloorDiv(regionMin.z - halo, VoxelsPerAxis));
                    int3 maxChunk = new int3(
                        FloorDiv(regionMax.x + halo - 1, VoxelsPerAxis),
                        FloorDiv(regionMax.y + halo - 1, VoxelsPerAxis),
                        FloorDiv(regionMax.z + halo - 1, VoxelsPerAxis));
                    _activeRegionChunkCounts = maxChunk - _activeRegionMinChunk + 1;
                }

                int total = _activeRegionChunkCounts.x
                          * _activeRegionChunkCounts.y
                          * _activeRegionChunkCounts.z;
                while (remaining > 0 && _activeRegionCandidateCursor < total)
                {
                    int linear = _activeRegionCandidateCursor++;
                    int x = linear % _activeRegionChunkCounts.x;
                    int y = (linear / _activeRegionChunkCounts.x) % _activeRegionChunkCounts.y;
                    int z = linear / (_activeRegionChunkCounts.x * _activeRegionChunkCounts.y);
                    int3 chunk = _activeRegionMinChunk + new int3(x, y, z);
                    remaining--;
                    if (!OwnsShard(chunk) || !_known.Contains(chunk)) continue;
                    if (ChunkOverlapsRegion(chunk, _activeRegionInvalidation,
                                            VoxelsPerAxis, SourceStep))
                        Invalidate(chunk);
                }

                if (_activeRegionCandidateCursor < total) return;

                int3 completed = _activeRegionInvalidation;
                bool rescan = _rescanRegionInvalidations.Remove(completed);
                _queuedRegionInvalidations.Remove(completed);
                _hasActiveRegionInvalidation = false;
                _activeRegionCandidateCursor = 0;
                if (rescan && _queuedRegionInvalidations.Add(completed))
                    _regionInvalidationQueue.Enqueue(completed);
            }
        }

        public void Prepare(IRegionReadSource source, in MaterialPaletteView palette,
                            in SurfaceCatalogueView surfaceCatalogue,
                            in CoatingCatalogueView coatingCatalogue,
                            IProfileBlockReadSource profileBlocks,
                            Camera camera,
                            float voxelSize, int frame, double budgetMs = 0.20)
        {
            using var prepareScope = s_PrepareMarker.Auto();
            double sectionStart = Time.realtimeSinceStartupAsDouble;
            SetSurfaceCatalogue(in surfaceCatalogue);
            SetCoatingCatalogue(in coatingCatalogue);
            SetMaterialPaletteVersion(palette.Version);
            SetProfileBlocks(profileBlocks);
            _ruleSyncTiming.Add(ElapsedMs(sectionStart));
            sectionStart = Time.realtimeSinceStartupAsDouble;
            StepClipmapEdgeRetirement();
            StepResidencyPrune(source);
            _residencyPruneTiming.Add(ElapsedMs(sectionStart));
            if (_pendingUpload) return;
            sectionStart = Time.realtimeSinceStartupAsDouble;
            EnforceCapacity(camera, voxelSize);
            _capacityTiming.Add(ElapsedMs(sectionStart));

            if (camera == null || _dirty.Count == 0 && !_build.Active) return;

            if (_build.Active && _build.SnapshotTaken
                && _build.MaterialPaletteVersion != palette.Version
                && (!_desiredVersions.TryGetValue(_build.Coordinate, out ulong desired)
                    || desired <= _build.SourceVersion))
                Invalidate(_build.Coordinate);

            // Snapshot work may now include Burst metadata/classification jobs. A newer source
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
            }

            do
            {
                if (!_build.Active)
                {
                    double selectionStart = Time.realtimeSinceStartupAsDouble;
                    bool selected = BeginNearestBuild(camera, voxelSize, deadline);
                    _buildSelectionTiming.Add(ElapsedMs(selectionStart));
                    if (!selected) break;
                }

                if (_build.Phase == 0)
                {
                    if (!_build.SnapshotTaken
                        && !StepDensitySnapshot(source, in palette, voxelSize, deadline))
                        break;

                    // Step 8 already scheduled its summary -> greedy HLOD dependency chain as
                    // part of the immutable exact snapshot. It bypasses Transvoxel density,
                    // faceted and transition phases and rejoins the normal profile/publication path
                    // only after the HLOD job is ready and its Storage pins are released.
                    if (UsesBlockHlod)
                    {
                        _build.Phase = 7;
                        continue;
                    }

                    // Border invalidation intentionally discovers halo chunks. If the immutable
                    // snapshot proves this chunk owns no solid cells, publish a complete empty
                    // result without scanning/merging all 64^3 cells. Profile blocks still run
                    // because their authored geometry may overlap an otherwise empty core.
                    if (!_build.HasOwnedSolid && _buildProfileBlocks.Length == 0)
                    {
                        if (!StepReleasePinnedSnapshotBlocks(deadline)) break;
                        _build.Phase = 3;
                        _build.Cursor = 0;
                        continue;
                    }

                    if (!_build.RequiresContinuousTopology)
                    {
                        ScheduleSnapshotFacetedMaskJob();
                        _build.Phase = 2;
                        continue;
                    }

                    _build.Phase = 1;
                    continue;
                }

                if (_build.Phase == 1)
                {
                    if (!_topologyCompactJobHandle.IsCompleted
                        || !_facetedMergeJobHandle.IsCompleted) break;
                    if (!GeometryFrameJobCompletionGuard.TryCompleteReady(
                            _topologyCompactJobHandle, ref _framePathBlockingCompletionViolations)
                        || !GeometryFrameJobCompletionGuard.TryCompleteReady(
                            _facetedMergeJobHandle, ref _framePathBlockingCompletionViolations))
                        break;
                    _densityTurnaroundTiming.Add(ElapsedMs(_build.DensityScheduledSeconds));
                    _topologyTurnaroundTiming.Add(ElapsedMs(_build.TopologyScheduledSeconds));
                    _facetedTurnaroundTiming.Add(ElapsedMs(_build.FacetedScheduledSeconds));
                    _densityJobScheduled = false;
                    _topologyJobScheduled = false;
                    _topologyCompactJobScheduled = false;
                    _facetedMaskJobScheduled = false;
                    _facetedMergeJobScheduled = false;
                    if (SupportsFeaturePreservingFallback)
                        Step4FalseEmptyDiagnostics.RecordOrdinaryResult(
                            _build.HasOwnedSolid, _buildProfileBlocks.Length != 0,
                            _compactedTopologyVertices.Length + _facetedVertices.Length,
                            _compactedTopologyIndices.Length + _facetedIndices.Length);
                    if (RequiresFeaturePreservingFallback(
                            SourceStep, _build.HasOwnedSolid,
                            _buildProfileBlocks.Length != 0,
                            _compactedTopologyVertices.Length + _facetedVertices.Length,
                            _compactedTopologyIndices.Length + _facetedIndices.Length))
                    {
                        ScheduleFeaturePreservingHlod(voxelSize);
                        _build.UsedFeaturePreservingFallback = true;
                        _build.Phase = 7;
                        continue;
                    }
                    BeginCompletedResultAppend(includeTopology: true);
                    _build.Phase = 6;
                    continue;
                }

                if (_build.Phase == 2)
                {
                    if (!_facetedMaskJobScheduled) ScheduleFacetedMaskJob();
                    if (!_facetedMergeJobScheduled) ScheduleFacetedMergeJob(voxelSize);
                    if (!_facetedMergeJobHandle.IsCompleted) break;
                    if (!GeometryFrameJobCompletionGuard.TryCompleteReady(
                            _facetedMergeJobHandle, ref _framePathBlockingCompletionViolations))
                        break;
                    _facetedTurnaroundTiming.Add(ElapsedMs(_build.FacetedScheduledSeconds));
                    _facetedMaskJobScheduled = false;
                    _facetedMergeJobScheduled = false;
                    if (SupportsFeaturePreservingFallback)
                        Step4FalseEmptyDiagnostics.RecordOrdinaryResult(
                            _build.HasOwnedSolid, _buildProfileBlocks.Length != 0,
                            _facetedVertices.Length, _facetedIndices.Length);
                    if (RequiresFeaturePreservingFallback(
                            SourceStep, _build.HasOwnedSolid,
                            _buildProfileBlocks.Length != 0,
                            _facetedVertices.Length, _facetedIndices.Length))
                    {
                        ScheduleFeaturePreservingHlod(voxelSize);
                        _build.UsedFeaturePreservingFallback = true;
                        _build.Phase = 7;
                        continue;
                    }
                    BeginCompletedResultAppend(includeTopology: false);
                    _build.Phase = 6;
                    continue;
                }

                if (_build.Phase == 7)
                {
                    if (_hlodJobScheduled)
                    {
                        if (!_hlodJobHandle.IsCompleted) break;
                        if (!GeometryFrameJobCompletionGuard.TryCompleteReady(
                                _hlodJobHandle, ref _framePathBlockingCompletionViolations))
                            break;
                        _hlodJobScheduled = false;
                        _build.HasOwnedSolid = _indices.Length > 0;
                        if (_build.UsedFeaturePreservingFallback)
                        {
                            FeaturePreservingFallbackCompleteCount++;
                            if (_build.HasOwnedSolid)
                                FeaturePreservingFallbackNonEmptyCount++;
                            Step4FalseEmptyDiagnostics.RecordFallbackCompleted(
                                _build.HasOwnedSolid);
                        }
                    }
                    if (_hlodOverflow[0] != 0)
                        throw new InvalidOperationException(
                            $"Feature-preserving HLOD output overflow in chunk {_build.Coordinate}; "
                          + "refusing to allocate or publish partial coarse geometry.");
                    // Profile blocks validate their backing against the same immutable mixed-brick
                    // payloads. Keep COW pins alive through profile emission; phase 3 releases
                    // them under the normal deadline once the last profile has consumed them.
                    if (_buildProfileBlocks.Length == 0
                        && !StepReleasePinnedSnapshotBlocks(deadline))
                        break;
                    _build.Phase = 3;
                    _build.Cursor = 0;
                    continue;
                }

                if (_build.Phase == 6)
                {
                    // Profile geometry may still need mixed-brick backing from the immutable COW
                    // snapshot. Do not release those pins until profile emission has finished.
                    if (_buildProfileBlocks.Length == 0
                        && !StepReleasePinnedSnapshotBlocks(deadline))
                        break;
                    _build.Phase = 5;
                    continue;
                }

                if (_build.Phase == 5)
                {
                    if (!StepCompletedResultAppend(deadline)) break;
                    _build.Phase = 3;
                    _build.Cursor = 0;
                    continue;
                }

                if (_build.Phase == 3)
                {
                    double profileStart = Time.realtimeSinceStartupAsDouble;
                    bool profilesDone;
                    using (s_ProfileMarker.Auto())
                        profilesDone = StepProfileBlocks(voxelSize);
                    _profileEmitTiming.Add(ElapsedMs(profileStart));
                    if (!profilesDone) continue;

                    // Profile backing reads are complete. Drain the exact mixed-brick pins now,
                    // still under the worker deadline, before transition/publication can proceed.
                    if (!StepReleasePinnedSnapshotBlocks(deadline)) break;

                    // The step-8 HLOD grid and the step-4 inner ring both resolve geometry on a
                    // four-voxel lattice. Do not feed faceted HLOD through Transvoxel transition
                    // cells; finish directly and let the visual LOD regression police the aligned
                    // boundary. If that test exposes a seam, add a dedicated HLOD boundary pass.
                    if (UsesBlockHlod || _build.UsedFeaturePreservingFallback)
                    {
                        FinishBuild(frame);
                        if (_pendingUpload) break;
                        continue;
                    }

                    _build.Phase = 4;
                    _build.Cursor = 0;
                    _transitionFace = -1;
                    _transitionSampleCursor = 0;
                    continue;
                }

                if (_build.Phase == 4)
                {
                    if (!StepTransitionFaces(source, in palette, camera, voxelSize,
                                             deadline))
                        break;
                    FinishBuild(frame);
                    if (_pendingUpload) break;
                }
            }
            while (Time.realtimeSinceStartupAsDouble < deadline);
        }

        private static bool RequiresFeaturePreservingFallback(
            int sourceStep, bool hasOwnedSolid, bool hasProfileGeometry,
            int vertexCount, int indexCount) =>
            sourceStep == FeaturePreservingFallbackStep
            && hasOwnedSolid
            && !hasProfileGeometry
            && vertexCount == 0
            && indexCount == 0;

        private void ScheduleFeaturePreservingHlod(float voxelSize)
        {
            if (SupportsFeaturePreservingFallback)
            {
                FeaturePreservingFallbackScheduleCount++;
                Step4FalseEmptyDiagnostics.RecordFallbackScheduled();
            }
            if (!_hlodSummaries.IsCreated || !_hlodMaskScratch.IsCreated || !_hlodOverflow.IsCreated)
                throw new InvalidOperationException(
                    $"Feature-preserving scratch was not allocated for source step {SourceStep}.");

            _vertices.Clear();
            _indices.Clear();
            _hlodOverflow[0] = 0;
            JobHandle summaryHandle = new SurfaceBlockHlodSummaryJob
            {
                Bricks = _densityBricks,
                MixedVoxels = PinnedMixedVoxelsOrFallback(),
                Summaries = _hlodSummaries,
            }.Schedule(BrickCacheCount, 256);
            _hlodJobHandle = new SurfaceBlockHlodMeshJob
            {
                Summaries = _hlodSummaries,
                SummaryGridEdge = BrickCacheEdge,
                PaddingBricks = BrickCachePadding,
                CoreBrickEdge = BricksPerAxis,
                CoreOriginVoxel = _build.Coordinate * VoxelsPerAxis,
                VoxelSize = voxelSize,
                MaskScratch = _hlodMaskScratch,
                Vertices = _vertices,
                Indices = _indices,
                Overflow = _hlodOverflow,
            }.Schedule(summaryHandle);
            _hlodJobScheduled = true;
        }

        private void ScheduleTopologyJob(float voxelSize, JobHandle dependency = default)
        {
            int cellCount = CellsPerAxis * CellsPerAxis * CellsPerAxis;
            if (_topologyOutput.IsCreated) _topologyOutput.Dispose();
            _topologyOutput = new NativeStream(cellCount, Allocator.Persistent);
            var job = new TransvoxelTopologyJob
            {
                Density = _density,
                Materials = _materials,
                SurfaceSemantics = _surfaceSemantics,
                BoundarySamples = _boundarySamples,
                CellClass = _topologyCellClass,
                GeometryCounts = _topologyGeometryCounts,
                CellVertexIndices = _topologyCellVertexIndices,
                EdgeCodes = _topologyEdgeCodes,
                Catalogue = _buildSurfaceCatalogue,
                Coatings = _buildCoatingCatalogue,
                ChunkOriginVoxel = _build.Coordinate * VoxelsPerAxis,
                CellsPerAxis = CellsPerAxis,
                GridSize = GridSize,
                Padding = Padding,
                SourceStep = SourceStep,
                VoxelSize = voxelSize,
                Output = _topologyOutput.AsWriter(),
            };
            _build.TopologyScheduledSeconds = Time.realtimeSinceStartupAsDouble;
            _topologyJobHandle = job.Schedule(cellCount, 64, dependency);
            _topologyJobScheduled = true;
            ScheduleTopologyCompactJob();
        }

        private void ScheduleTopologyCompactJob()
        {
            var job = new TransvoxelCompactJob
            {
                Input = _topologyOutput.AsReader(),
                Vertices = _compactedTopologyVertices,
                Indices = _compactedTopologyIndices,
                OverflowCell = _topologyOverflowCell,
            };
            _topologyCompactJobHandle = job.Schedule(_topologyJobHandle);
            _topologyCompactJobScheduled = true;
        }

        private void BeginCompletedResultAppend(bool includeTopology)
        {
            _resultAppendStage = includeTopology ? 0 : 1;
            _topologyAppendVertexCursor = 0;
            _topologyAppendIndexCursor = 0;
            _topologyAppendVertexBase = 0;
            _facetedAppendVertexCursor = 0;
            _facetedAppendIndexCursor = 0;
            _facetedAppendVertexBase = 0;
        }

        private bool StepCompletedResultAppend(double deadlineSeconds)
        {
            if (_resultAppendStage == 0)
            {
                double start = Time.realtimeSinceStartupAsDouble;
                using var scope = s_CompactMarker.Auto();
                int overflowCell = _topologyOverflowCell[0];
                if (overflowCell >= 0)
                    throw new InvalidOperationException(
                        $"Continuous topology output overflow in chunk {_build.Coordinate}, "
                      + $"cell {overflowCell}; refusing to publish partial geometry.");

                if (!StepAppendNativeGeometry(_compactedTopologyVertices.AsArray(),
                                              _compactedTopologyIndices.AsArray(),
                                              ref _topologyAppendVertexCursor,
                                              ref _topologyAppendIndexCursor,
                                              ref _topologyAppendVertexBase,
                                              deadlineSeconds))
                {
                    LastTopologyCompactMs = ElapsedMs(start);
                    _topologyCompactTiming.Add(LastTopologyCompactMs);
                    return false;
                }

                if (_topologyOutput.IsCreated) _topologyOutput.Dispose();
                _topologyOutput = default;
                LastTopologyCompactMs = ElapsedMs(start);
                _topologyCompactTiming.Add(LastTopologyCompactMs);
                _resultAppendStage = 1;
            }

            if (_resultAppendStage == 1)
            {
                double start = Time.realtimeSinceStartupAsDouble;
                using var scope = s_FacetedMergeMarker.Auto();
                if (!StepAppendNativeGeometry(_facetedVertices.AsArray(),
                                              _facetedIndices.AsArray(),
                                              ref _facetedAppendVertexCursor,
                                              ref _facetedAppendIndexCursor,
                                              ref _facetedAppendVertexBase,
                                              deadlineSeconds))
                {
                    _facetedMergeTiming.Add(ElapsedMs(start));
                    return false;
                }
                _facetedMergeTiming.Add(ElapsedMs(start));
                _resultAppendStage = 2;
            }
            return true;
        }

        private bool StepAppendNativeGeometry(NativeArray<SmoothSurfaceVertex> sourceVertices,
                                              NativeArray<uint> sourceIndices,
                                              ref int vertexCursor, ref int indexCursor,
                                              ref uint vertexBase,
                                              double deadlineSeconds)
        {
            if (Time.realtimeSinceStartupAsDouble >= deadlineSeconds) return false;
            if (vertexCursor == 0 && indexCursor == 0)
                vertexBase = (uint)_vertices.Length;

            while (vertexCursor < sourceVertices.Length)
            {
                int end = math.min(sourceVertices.Length,
                                   vertexCursor + AppendElementsPerDeadlineCheck);
                for (; vertexCursor < end; vertexCursor++)
                    _vertices.Add(sourceVertices[vertexCursor]);
                if (vertexCursor < sourceVertices.Length
                    && Time.realtimeSinceStartupAsDouble >= deadlineSeconds)
                    return false;
            }

            while (indexCursor < sourceIndices.Length)
            {
                int end = math.min(sourceIndices.Length,
                                   indexCursor + AppendElementsPerDeadlineCheck);
                for (; indexCursor < end; indexCursor++)
                    _indices.Add(vertexBase + sourceIndices[indexCursor]);
                if (indexCursor < sourceIndices.Length
                    && Time.realtimeSinceStartupAsDouble >= deadlineSeconds)
                    return false;
            }
            return true;
        }

        private void ScheduleFacetedMaskJob(JobHandle dependency = default)
        {
            var job = new FacetedMaskJob
            {
                Materials = _materials,
                SurfaceSemantics = _surfaceSemantics,
                BoundarySamples = _boundarySamples,
                Catalogue = _buildSurfaceCatalogue,
                Coatings = _buildCoatingCatalogue,
                CellsPerAxis = CellsPerAxis,
                GridSize = GridSize,
                Padding = Padding,
                FaceMasks = _facetedMasks,
            };
            _build.FacetedScheduledSeconds = Time.realtimeSinceStartupAsDouble;
            _facetedMaskJobHandle = job.Schedule(
                CellsPerAxis * CellsPerAxis * CellsPerAxis, 128, dependency);
            _facetedMaskJobScheduled = true;
        }

        private void ScheduleSnapshotFacetedMaskJob()
        {
            int3 chunkOrigin = _build.Coordinate * VoxelsPerAxis;
            int3 chunkBrickOrigin = chunkOrigin >> VoxelReadGrid.BlockEdgeLog2;
            var job = new SnapshotFacetedMaskJob
            {
                Bricks = _densityBricks,
                MixedVoxels = PinnedMixedVoxelsOrFallback(),
                MixedSurfaceSemantics = PinnedMixedSurfaceSemanticsOrFallback(),
                MixedBoundarySamples = PinnedMixedBoundarySamplesOrFallback(),
                Palette = _buildPalette,
                Catalogue = _buildSurfaceCatalogue,
                Coatings = _buildCoatingCatalogue,
                ChunkOriginVoxel = chunkOrigin,
                BrickCacheOrigin = chunkBrickOrigin - BrickCachePadding,
                BrickCacheEdge = BrickCacheEdge,
                CellsPerAxis = CellsPerAxis,
                SourceStep = SourceStep,
                FaceMasks = _facetedMasks,
            };
            _build.FacetedScheduledSeconds = Time.realtimeSinceStartupAsDouble;
            _facetedMaskJobHandle = job.Schedule(
                CellsPerAxis * CellsPerAxis * CellsPerAxis, 128);
            _facetedMaskJobScheduled = true;
        }

        private void ScheduleFacetedMergeJob(float voxelSize)
        {
            var job = new FacetedMergeJob
            {
                FaceMasks = _facetedMasks,
                Vertices = _facetedVertices,
                Indices = _facetedIndices,
                ChunkOrigin = _build.Coordinate * VoxelsPerAxis,
                CellsPerAxis = CellsPerAxis,
                SourceStep = SourceStep,
                VoxelSize = voxelSize,
            };
            _facetedMergeJobHandle = job.Schedule(_facetedMaskJobHandle);
            _facetedMergeJobScheduled = true;
        }

        private void MergeAllFacetedMasks(float voxelSize)
        {
            int3 chunkOrigin = _build.Coordinate * VoxelsPerAxis;
            int cellsPerPlane = CellsPerAxis * CellsPerAxis;
            int planeCount = 6 * CellsPerAxis;
            for (int planeIndex = 0; planeIndex < planeCount; planeIndex++)
            {
                int offset = planeIndex * cellsPerPlane;
                for (int i = 0; i < cellsPerPlane; i++) _faceMask[i] = _facetedMasks[offset + i];
                int layer = planeIndex % CellsPerAxis;
                int face = planeIndex / CellsPerAxis;
                int axis = face >> 1;
                int sign = (face & 1) == 0 ? -1 : 1;
                MergeFacetedMask(chunkOrigin, axis, sign, layer, voxelSize);
            }
        }

        public void BeginVisibilityCollection()
        {
            _visible.Clear();
            MissingVisibleCount = 0;
            LastVisibilityKnownCount = 0;
            LastVisibilityInBandCount = 0;
            LastVisibilityFrustumCount = 0;
            LastVisibilityReadyCount = 0;
            LastVisibilityEmptyCount = 0;
        }

        /// <summary>
        /// Evaluates one clipmap coordinate already routed to this shard. Visibility traversal is
        /// driven by the bounded camera-centred ring grid, never by the lifetime size of _known.
        /// </summary>
        public void CollectVisibleCoordinate(int3 coordinate, Plane[] frustumPlanes,
                                             Vector3 cameraPosition, float voxelSize, int frame)
        {
            if (!_known.Contains(coordinate)) return;
            LastVisibilityKnownCount++;

            Bounds bounds = ChunkWorldBounds(coordinate, voxelSize);
            if (!WithinRingBand(bounds, cameraPosition))
            {
                // Authoritative discovery is shared across LODs. Keep the known/version state,
                // but never let a chunk owned wholly by another ring remain active build demand.
                if (_dirty.Contains(coordinate)) ParkDirty(coordinate);
                return;
            }
            LastVisibilityInBandCount++;

            bool hasDesired = _desiredVersions.TryGetValue(coordinate, out ulong desired);
            bool currentGenerationInFlight = CurrentBuildCoversDesiredGeneration(
                coordinate, hasDesired, desired);
            bool ready = _entries.TryGetValue(coordinate, out Entry entry) && entry.Ready;
            bool currentReady = ready && (!hasDesired || entry.SourceVersion >= desired);
            bool currentEmpty = _emptyVersions.TryGetValue(coordinate, out ulong emptyVersion)
                             && (!hasDesired || emptyVersion >= desired);

            // This traversal covers the ring's dense active-slot list. Activate build demand for
            // every in-band chunk before the frustum test so geometry is prefetched around the
            // viewer, while still excluding the thousands of known chunks owned by other LODs.
            if (!currentReady && !currentEmpty && !currentGenerationInFlight)
                MarkDirty(coordinate);

            if (!GeometryUtility.TestPlanesAABB(frustumPlanes, bounds)) return;
            LastVisibilityFrustumCount++;
            if (currentReady) LastVisibilityReadyCount++;
            if (currentEmpty) LastVisibilityEmptyCount++;

            // Background prefetch above remains intentionally 360 degrees. Once a chunk is in
            // the actual camera frustum, however, promote its still-needed generation so build
            // selection cannot make a visible hole wait behind the entire prefetch shell.
            if (!currentReady && !currentEmpty && !currentGenerationInFlight)
                PromoteVisibleDirty(coordinate);

            if (ready)
            {
                // Keep the previous mesh drawable while a newer authoritative generation builds.
                // CurrentBuildCoversDesiredGeneration above prevents visibility from recreating a
                // duplicate dirty record for the exact generation already in flight.
                if (entry.IndexCount == 0) return;
                entry.LastUsedFrame = frame;
                _visible.Add(entry);
                return;
            }

            // A current known-empty result is complete, not a visual hole. Any other in-band
            // visible coordinate remains missing until its authoritative generation publishes.
            if (currentEmpty) return;

            MissingVisibleCount++;
        }

        /// <summary>
        /// Compatibility entry point for focused tests/tools. Production scheduling performs one
        /// ring traversal in VoxelSurfaceScheduler and routes coordinates directly to shards.
        /// This fallback is still bounded by the ring's configured view distance.
        /// </summary>
        public IReadOnlyList<Entry> CollectVisible(Camera camera, float voxelSize, int frame)
        {
            BeginVisibilityCollection();
            if (camera == null) return _visible;

            GeometryUtility.CalculateFrustumPlanes(camera, _frustumPlanes);
            Vector3 cameraPosition = camera.transform.position;
            float chunkMetres = VoxelsPerAxis * voxelSize;
            int radius = Mathf.CeilToInt(MaxViewDistanceMetres / chunkMetres) + 1;
            int3 centre = new(
                Mathf.FloorToInt(cameraPosition.x / chunkMetres),
                Mathf.FloorToInt(cameraPosition.y / chunkMetres),
                Mathf.FloorToInt(cameraPosition.z / chunkMetres));

            for (int z = -radius; z <= radius; z++)
            for (int y = -radius; y <= radius; y++)
            for (int x = -radius; x <= radius; x++)
            {
                int3 coordinate = centre + new int3(x, y, z);
                if (!OwnsShard(coordinate)) continue;
                CollectVisibleCoordinate(coordinate, _frustumPlanes, cameraPosition,
                                         voxelSize, frame);
            }
            return _visible;
        }

        private bool BeginNearestBuild(Camera camera, float voxelSize,
                                       double deadlineSeconds)
        {
            if (_dirty.Count == 0
                || _dirtyQueue.Count == 0 && _visibleDirtyQueue.Count == 0
                || Time.realtimeSinceStartupAsDouble >= deadlineSeconds)
                return false;

            int3 best = default;
            bool hasBest = false;
            float bestScore = float.PositiveInfinity;
            float chunkMetres = VoxelsPerAxis * voxelSize;
            Vector3 cameraWorldPosition = camera.transform.position;
            GeometryUtility.CalculateFrustumPlanes(camera, _frustumPlanes);

            // First sample only demand that was actually visible when collected. Camera motion can
            // stale that classification, so recheck both ring ownership and the current frustum.
            // A priority record that moved offscreen simply falls back to its existing background
            // FIFO record; no authoritative work is lost.
            int visibleCandidates = math.min(
                VisibleBuildSelectionCandidatesPerSlice, _visibleDirtyQueue.Count);
            for (int i = 0; i < visibleCandidates; i++)
            {
                int3 candidate = _visibleDirtyQueue.Dequeue();
                _queuedVisibleDirty.Remove(candidate);
                if (!_dirty.Contains(candidate)) continue;

                Bounds bounds = ChunkWorldBounds(candidate, voxelSize);
                if (!WithinRingBand(bounds, cameraWorldPosition))
                {
                    ParkDirty(candidate);
                    continue;
                }
                if (!GeometryUtility.TestPlanesAABB(_frustumPlanes, bounds))
                    continue;

                // Visibility already established urgency. Ranking dozens of visible holes by
                // distance cost the entire renderer-wide build budget in production (0.52 ms
                // selection p95 against a 0.50 ms budget). FIFO is fair, deterministic and lets
                // the selected workspace spend this frame advancing geometry instead.
                best = candidate;
                hasBest = true;
                break;
            }

            // No currently visible hole was ready for this workspace. Preserve the original
            // bounded background selection so 360-degree prefetch still converges opportunistically.
            if (!hasBest)
            {
                int candidates = math.min(BuildSelectionCandidatesPerSlice, _dirtyQueue.Count);
                for (int i = 0; i < candidates; i++)
                {
                    int3 candidate = _dirtyQueue.Dequeue();
                    _queuedDirty.Remove(candidate);
                    if (!_dirty.Contains(candidate)) continue; // stale queue record

                    Bounds bounds = ChunkWorldBounds(candidate, voxelSize);
                    if (!WithinRingBand(bounds, cameraWorldPosition))
                    {
                        ParkDirty(candidate);
                        continue;
                    }

                    Vector3 centre = (new Vector3(candidate.x, candidate.y, candidate.z)
                                    + Vector3.one * 0.5f) * chunkMetres;
                    float distance = (centre - cameraWorldPosition).sqrMagnitude;
                    float score = GeometryUtility.TestPlanesAABB(_frustumPlanes, bounds)
                        ? distance : distance + 1_000_000_000f;
                    if (!hasBest || score < bestScore)
                    {
                        if (hasBest) RequeueDirty(best);
                        bestScore = score;
                        best = candidate;
                        hasBest = true;
                    }
                    else
                    {
                        RequeueDirty(candidate);
                    }

                    // Score checks are cheap, but a destruction burst can enqueue thousands. The
                    // frame contract wins over exact global nearest ordering; later slices continue
                    // from the queue tail and converge without a scan spike.
                    if (Time.realtimeSinceStartupAsDouble >= deadlineSeconds) break;
                }
            }

            if (!hasBest) return false;
            // Priority selection leaves the background queue's physical record in place. Clear its
            // membership bit before admission so a failed slot acquisition can be reactivated on a
            // later visibility pass; the old physical record will self-prune as stale.
            _queuedDirty.Remove(best);
            _queuedVisibleDirty.Remove(best);
            if (!_slotGrid.TryGet(best, out SurfaceChunkSlot buildSlot)
                && !_slotGrid.TryAcquire(best, out buildSlot))
            {
                _dirty.Remove(best);
                return false;
            }
            _dirty.Remove(best);
            _vertices.Clear();
            _indices.Clear();
            _transitionFace = -1;
            _transitionSampleCursor = 0;
            _transitionResultPending = false;
            _resultAppendStage = 0;
            _topologyAppendVertexCursor = 0;
            _topologyAppendIndexCursor = 0;
            _facetedAppendVertexCursor = 0;
            _facetedAppendIndexCursor = 0;
            _transitionAppendVertexCursor = 0;
            _transitionAppendIndexCursor = 0;
            _build = new BuildState
            {
                Active = true, Coordinate = best, Phase = 0, Cursor = 0,
                SourceVersion = _desiredVersions.TryGetValue(best, out ulong version) ? version : 0,
                SlotGeneration = buildSlot.Generation,
                SurfaceCatalogueVersion = _surfaceCatalogue.Version,
                SurfaceCatalogueHash = _surfaceCatalogue.CatalogueHash,
                CoatingCatalogueVersion = _coatingCatalogue.Version,
                CoatingCatalogueHash = _coatingCatalogue.CatalogueHash,
                BuildStartSeconds = Time.realtimeSinceStartupAsDouble
            };
            if (_queuedAtSeconds.TryGetValue(best, out double queuedAt))
                _queueLatencyTiming.Add(ElapsedMs(queuedAt));
            return true;
        }

        private void Invalidate(int3 chunk)
        {
            _emptyVersions.Remove(chunk);
            _desiredVersions[chunk] = ++_versionCounter;
            MarkDirty(chunk);
        }

        private void MarkDirty(int3 chunk)
        {
            // Every active dirty record needs a durable desired generation. Most callers arrive
            // through Invalidate, but arena/capacity eviction can request a rebuild directly.
            // Keeping that generation lets parked work be reactivated safely when it becomes
            // visible again.
            if (!_desiredVersions.ContainsKey(chunk))
                _desiredVersions[chunk] = ++_versionCounter;
            if (_dirty.Add(chunk))
                _queuedAtSeconds[chunk] = Time.realtimeSinceStartupAsDouble;
            RequeueDirty(chunk);
        }

        private void RequeueDirty(int3 chunk)
        {
            if (!_dirty.Contains(chunk) || !_queuedDirty.Add(chunk)) return;
            _dirtyQueue.Enqueue(chunk);
        }

        private void PromoteVisibleDirty(int3 chunk)
        {
            if (!_dirty.Contains(chunk)) MarkDirty(chunk);
            RequeueVisibleDirty(chunk);
        }

        private void RequeueVisibleDirty(int3 chunk)
        {
            if (!_dirty.Contains(chunk) || !_queuedVisibleDirty.Add(chunk)) return;
            _visibleDirtyQueue.Enqueue(chunk);
        }

        private void ParkDirty(int3 chunk)
        {
            _dirty.Remove(chunk);
            _queuedDirty.Remove(chunk);
            _queuedVisibleDirty.Remove(chunk);
            _queuedAtSeconds.Remove(chunk);
            // Intentionally retain _desiredVersions: discovery/edit state remains authoritative,
            // and CollectVisibleCoordinate will reactivate it if this chunk enters the ring.
        }

        private bool CurrentBuildCoversDesiredGeneration(int3 chunk, bool hasDesired, ulong desired)
        {
            return _build.Active && _build.Coordinate.Equals(chunk)
                && (!hasDesired || _build.SourceVersion >= desired);
        }

        public static int ShardForChunk(int3 chunk, int shardCount)
        {
            int count = math.max(1, shardCount);
            return (int)(math.hash(chunk) % (uint)count);
        }

        private bool OwnsShard(int3 chunk) =>
            ShardForChunk(chunk, ShardCount) == math.clamp(ShardIndex, 0, math.max(1, ShardCount) - 1);

        private void SetSurfaceCatalogue(in SurfaceCatalogueView catalogue)
        {
            ulong hash = catalogue.CatalogueHash != 0
                ? catalogue.CatalogueHash : catalogue.ComputeHash();
            if (_surfaceCatalogue.Version == catalogue.Version
                && _surfaceCatalogue.CatalogueHash == hash) return;

            _surfaceCatalogue = catalogue;
            if (_surfaceCatalogue.CatalogueHash == 0)
                _surfaceCatalogue.Seal(_surfaceCatalogue.Version, hash);

            // Catalogue data participates in geometry. Existing meshes may remain visible while
            // every known chunk queues a replacement built from the new immutable snapshot.
            SurfaceCatalogueInvalidationCount++;
            foreach (int3 chunk in _known) Invalidate(chunk);
        }

        private void SetMaterialPaletteVersion(uint version)
        {
            if (_materialPaletteVersion == version) return;
            _materialPaletteVersion = version;
            MaterialPaletteInvalidationCount++;
            foreach (int3 chunk in _known) Invalidate(chunk);
        }

        private void SetCoatingCatalogue(in CoatingCatalogueView catalogue)
        {
            ulong hash = catalogue.CatalogueHash != 0
                ? catalogue.CatalogueHash : catalogue.ComputeHash();
            if (_coatingCatalogue.Version == catalogue.Version
                && _coatingCatalogue.CatalogueHash == hash) return;

            _coatingCatalogue = catalogue;
            if (_coatingCatalogue.CatalogueHash == 0)
                _coatingCatalogue.Seal(_coatingCatalogue.Version, hash);
            CoatingCatalogueInvalidationCount++;
            foreach (int3 chunk in _known) Invalidate(chunk);
        }

        private void SetProfileBlocks(IProfileBlockReadSource store)
        {
            uint version = store?.Version ?? 0;
            if (ReferenceEquals(_profileBlockStore, store) && _profileBlockVersion == version)
                return;
            _profileBlockStore = store;
            _profileBlockVersion = version;
            _profileBlocks = store?.Snapshot() ?? Array.Empty<ProfileBlock>();
            RebuildProfileBlockIndex();
            ProfileBlockInvalidationCount++;
            foreach (int3 chunk in _known) Invalidate(chunk);
        }

        private void RebuildProfileBlockIndex()
        {
            _profileBlocksByChunk.Clear();
            var staging = new Dictionary<int3, List<ProfileBlock>>();
            for (int i = 0; i < _profileBlocks.Length; i++)
            {
                ProfileBlock block = _profileBlocks[i];
                block.Bounds(out int3 min, out int3 max);
                int3 first = new(FloorDiv(min.x, VoxelsPerAxis),
                                 FloorDiv(min.y, VoxelsPerAxis),
                                 FloorDiv(min.z, VoxelsPerAxis));
                int3 last = new(FloorDiv(max.x, VoxelsPerAxis),
                                FloorDiv(max.y, VoxelsPerAxis),
                                FloorDiv(max.z, VoxelsPerAxis));
                for (int z = first.z; z <= last.z; z++)
                for (int y = first.y; y <= last.y; y++)
                for (int x = first.x; x <= last.x; x++)
                {
                    int3 chunk = new(x, y, z);
                    if (!staging.TryGetValue(chunk, out List<ProfileBlock> blocks))
                        staging.Add(chunk, blocks = new List<ProfileBlock>());
                    blocks.Add(block);
                }
            }
            foreach (var pair in staging) _profileBlocksByChunk.Add(pair.Key, pair.Value.ToArray());
        }

        private const int SnapshotMipSamplesPerDeadlineCheck = 64;

        /// <summary>
        /// Advances the authoritative-to-immutable snapshot boundary without ever walking a full
        /// chunk in one frame. The snapshot lives entirely in this workspace's persistent native
        /// buffers; borrowed Storage views are reacquired inside each slice and never survive the
        /// call. A later journal invalidation rejects the partial generation before publication.
        /// </summary>
        private bool StepDensitySnapshot(IRegionReadSource source,
                                         in MaterialPaletteView palette,
                                         float voxelSize, double deadlineSeconds)
        {
            if (Time.realtimeSinceStartupAsDouble >= deadlineSeconds) return false;
            return SamplesFromMips
                ? StepMipDensitySnapshot(source, in palette, voxelSize, deadlineSeconds)
                : StepExactDensitySnapshot(source, in palette, voxelSize, deadlineSeconds);
        }

        private bool StepExactDensitySnapshot(IRegionReadSource source,
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

                if (!GeometryFrameJobCompletionGuard.TryCompleteReady(
                        _exactMetadataJobHandle, ref _framePathBlockingCompletionViolations))
                {
                    AccumulateSnapshotSlice(sliceStart, completed: false);
                    return false;
                }
                _exactMetadataJobScheduled = false;
                ExactMetadataCompleteCount++;
                if (!_exactMetadataRegionCoverage.IsComplete)
                {
                    // A failed region metadata pin means this exact snapshot is unavailable,
                    // never that the cleared cache range is authoritatively empty. Waited jobs
                    // are already complete here, so release every successful pin and retry the
                    // generation through the existing bounded discard/requeue lifecycle.
                    ExactMetadataPinRejectCount++;
                    ReleasePinnedRegionMetadataImmediate();
                    _discardBuildAfterPinRelease = true;
                    AccumulateSnapshotSlice(sliceStart, completed: false);
                    return false;
                }
                if (!PinnedRegionMetadataCurrent())
                {
                    ExactMetadataRevisionRejectCount++;
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
                    if (!source.TryPinWorldBlock(worldBlock, out PinnedVoxelReadBlock pinned))
                    {
                        ExactMetadataPinRejectCount++;
                        // Metadata said this block was mixed, but the coordinate can no longer
                        // supply that immutable COW payload. The optimistic snapshot is no longer
                        // coherent (for example, residency or a writer raced the metadata copy).
                        // Do not spin forever on the same cursor: reject this generation and let
                        // the existing bounded pin-release path retry from fresh metadata.
                        ReleasePinnedRegionMetadataImmediate();
                        _discardBuildAfterPinRelease = true;
                        AccumulateSnapshotSlice(sliceStart, completed: false);
                        return false;
                    }

                    TransvoxelDensityBrick expected = _densityBricks[cacheIndex];
                    if (pinned.Kind != VoxelReadBlockKind.Mixed || !pinned.HasPinnedPayload
                        || pinned.MixedOffset != expected.MixedOffset)
                    {
                        ExactMetadataPinRejectCount++;
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
                        ExactMetadataPinRejectCount++;
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
                ExactMetadataRevisionRejectCount++;
                ReleasePinnedRegionMetadataImmediate();
                _discardBuildAfterPinRelease = true;
                AccumulateSnapshotSlice(sliceStart, completed: false);
                return false;
            }
            ReleasePinnedRegionMetadataImmediate();

            if (UsesBlockHlod)
            {
                ScheduleFeaturePreservingHlod(voxelSize);
                _build.HasOwnedSolid = true; // resolved from final HLOD output on completion
                _build.RequiresContinuousTopology = false;
                _build.SnapshotTaken = true;
                _exactMetadataReady = false;
                _exactMixedPinCursor = 0;
                AccumulateSnapshotSlice(sliceStart, completed: true);
                return true;
            }

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
            if (!GeometryFrameJobCompletionGuard.TryCompleteReady(
                    _exactClassificationJobHandle, ref _framePathBlockingCompletionViolations))
            {
                AccumulateSnapshotSlice(sliceStart, completed: false);
                return false;
            }
            _exactClassificationJobScheduled = false;
            _build.HasOwnedSolid = _snapshotClassificationFlags[0] != 0;
            _build.RequiresContinuousTopology = _snapshotClassificationFlags[1] != 0;
            if (SupportsFeaturePreservingFallback)
                Step4FalseEmptyDiagnostics.RecordExactClassification(
                    _build.HasOwnedSolid, _buildProfileBlocks.Length != 0);
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
            _exactMetadataRegionCoverage.Reset();

            JobHandle clearHandle = new ExactBrickMetadataClearJob
            {
                Bricks = _densityBricks,
                MixedFlags = _exactMixedFlags,
            }.Schedule(BrickCacheCount, 256);
            // Region intersections are disjoint cache ranges. Schedule every copy behind the
            // shared clear only, then combine their handles once before compaction. Chaining each
            // copy behind the previous region serializes phase-0 snapshot work and can starve
            // coarse LOD workers on Metal even though the copies are independent.
            JobHandle dependency = clearHandle;

            int edge = VoxelReadGrid.BlocksPerRegionEdge;
            int3 cacheMaxExclusive = cacheOrigin + BrickCacheEdge;
            int3 coreMinWorldBlock = cacheOrigin + BrickCachePadding;
            int3 minRegion = cacheOrigin >> VoxelReadGrid.BlocksPerRegionEdgeLog2;
            int3 maxRegion = (cacheMaxExclusive - 1) >> VoxelReadGrid.BlocksPerRegionEdgeLog2;

            for (int rz = minRegion.z; rz <= maxRegion.z; rz++)
            for (int ry = minRegion.y; ry <= maxRegion.y; ry++)
            for (int rx = minRegion.x; rx <= maxRegion.x; rx++)
            {
                int3 regionCoord = new(rx, ry, rz);
                bool requiredRegion = ExactSnapshotRegionCoverage.RegionIntersectsCore(
                    regionCoord, edge, coreMinWorldBlock, BricksPerAxis);
                bool pinnedRegion = source.TryPinRegionBlockRefs(
                    regionCoord, out PinnedRegionBlockRefs pinned);
                _exactMetadataRegionCoverage.RecordRegion(requiredRegion, pinnedRegion);
                if (!pinnedRegion) continue;
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

                JobHandle regionHandle = new ExactBrickMetadataRegionJob
                {
                    EncodedBlockRefs = pinned.EncodedBlockRefs,
                    RegionCoord = regionCoord,
                    IntersectionMinWorldBlock = intersectionMin,
                    IntersectionSize = size,
                    CacheOrigin = cacheOrigin,
                    BrickCacheEdge = BrickCacheEdge,
                    Bricks = _densityBricks,
                    MixedFlags = _exactMixedFlags,
                }.Schedule(volume, 128, clearHandle);
                dependency = JobHandle.CombineDependencies(dependency, regionHandle);
            }

            _exactMetadataJobHandle = new ExactMixedBrickCompactJob
            {
                MixedFlags = _exactMixedFlags,
                MixedIndices = _exactMixedBrickIndices,
            }.Schedule(dependency);
            ExactMetadataScheduleCount++;
            _exactMetadataJobScheduled = true;
        }

        private int3 WorldBlockForCacheIndex(int index, int3 cacheOrigin)
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

        private bool StepMipDensitySnapshot(IRegionReadSource source,
                                            in MaterialPaletteView palette,
                                            float voxelSize, double deadlineSeconds)
        {
            double sliceStart = Time.realtimeSinceStartupAsDouble;
            using var snapshotScope = s_SnapshotMarker.Auto();
            if (!_build.SnapshotInitialised)
            {
                _buildSurfaceCatalogue = _surfaceCatalogue;
                _buildCoatingCatalogue = _coatingCatalogue;
                _buildPalette = palette;
                _build.MaterialPaletteVersion = palette.Version;
                _buildProfileBlocks = Array.Empty<ProfileBlock>();
                _build.SnapshotCursor = 0;
                _build.SnapshotCpuMs = 0.0;
                _build.HasOwnedSolid = false;
                _build.RequiresContinuousTopology = false;
                _build.SnapshotInitialised = true;
            }

            int3 chunkOriginVoxel = _build.Coordinate * VoxelsPerAxis;
            int mipLevel = VoxelReadGrid.LevelForStride(SourceStep);
            RegionSampleCursor cursor = default;
            while (_build.SnapshotCursor < GridSampleCount)
            {
                int batchEnd = math.min(GridSampleCount,
                    _build.SnapshotCursor + SnapshotMipSamplesPerDeadlineCheck);
                for (; _build.SnapshotCursor < batchEnd; _build.SnapshotCursor++)
                {
                    int index = _build.SnapshotCursor;
                    int gx = index % GridSize;
                    int gy = (index / GridSize) % GridSize;
                    int gz = index / (GridSize * GridSize);
                    int3 voxel = chunkOriginVoxel
                               + (new int3(gx, gy, gz) - Padding) * SourceStep;

                    bool occupied = false;
                    byte material = VoxelGrid.MaterialEmpty;
                    if (TrySampleWorld(source, ref cursor, voxel, mipLevel,
                                       out bool sampled, out byte sampledMaterial))
                    {
                        occupied = sampled;
                        material = sampledMaterial;
                    }
                    _mipSampleOccupancy[index] = occupied ? (byte)1 : (byte)0;
                    _mipSampleMaterials[index] = material;
                    _build.HasOwnedSolid |= occupied && IsSolidSurfaceMaterial(material);
                }

                if (_build.SnapshotCursor < GridSampleCount
                    && Time.realtimeSinceStartupAsDouble >= deadlineSeconds)
                {
                    AccumulateSnapshotSlice(sliceStart, completed: false);
                    return false;
                }
            }

            _build.SnapshotTaken = true;
            _build.RequiresContinuousTopology = _build.HasOwnedSolid;
            AccumulateSnapshotSlice(sliceStart, completed: true);
            if (!_build.HasOwnedSolid) return true;

            var job = new MipDensityJob
            {
                SampleOccupancy = _mipSampleOccupancy,
                SampleMaterials = _mipSampleMaterials,
                Palette = _buildPalette,
                Density = _density,
                Materials = _materials,
                SurfaceSemantics = _surfaceSemantics,
                BoundarySamples = _boundarySamples,
                GridSize = GridSize,
            };
            _build.DensityScheduledSeconds = Time.realtimeSinceStartupAsDouble;
            _densityJobHandle = job.Schedule(GridSampleCount, 256);
            _densityJobScheduled = true;
            ScheduleTopologyJob(voxelSize, _densityJobHandle);
            return true;
        }

        private void AccumulateSnapshotSlice(double sliceStart, bool completed)
        {
            _build.SnapshotCpuMs += ElapsedMs(sliceStart);
            if (!completed) return;
            LastSnapshotMs = _build.SnapshotCpuMs;
            _snapshotTiming.Add(LastSnapshotMs);
        }

        private NativeArray<byte> PinnedMixedVoxelsOrFallback() =>
            _pinnedMixedVoxels.IsCreated
                ? _pinnedMixedVoxels : _densityMixedVoxels.AsArray();

        private NativeArray<ushort> PinnedMixedSurfaceSemanticsOrFallback() =>
            _pinnedMixedSurfaceSemantics.IsCreated
                ? _pinnedMixedSurfaceSemantics : _densityMixedSurfaceSemantics.AsArray();

        private NativeArray<byte> PinnedMixedBoundarySamplesOrFallback() =>
            _pinnedMixedBoundarySamples.IsCreated
                ? _pinnedMixedBoundarySamples : _densityMixedBoundarySamples.AsArray();

        private const int PinnedReleasesPerDeadlineCheck = 64;

        /// <summary>
        /// Releases immutable Storage payload versions incrementally. Job completion is not
        /// allowed to turn into a large frame-thread unpin loop; slow release merely delays the
        /// next build while the previous published geometry remains valid.
        /// </summary>
        private bool StepReleasePinnedSnapshotBlocks(double deadlineSeconds)
        {
            if (_pinnedReadBlocks.Length == 0)
            {
                ClearPinnedSnapshotState();
                return true;
            }
            if (_pinnedReadSource == null)
                throw new InvalidOperationException("Pinned snapshot lost its Storage source.");

            while (_pinnedReleaseCursor < _pinnedReadBlocks.Length)
            {
                int end = math.min(_pinnedReadBlocks.Length,
                                   _pinnedReleaseCursor + PinnedReleasesPerDeadlineCheck);
                for (; _pinnedReleaseCursor < end; _pinnedReleaseCursor++)
                {
                    VoxelReadPinToken token = _pinnedReadBlocks[_pinnedReleaseCursor];
                    _pinnedReadSource.ReleasePinnedWorldBlock(in token);
                }
                if (_pinnedReleaseCursor < _pinnedReadBlocks.Length
                    && Time.realtimeSinceStartupAsDouble >= deadlineSeconds)
                    return false;
            }

            _pinnedReadBlocks.Clear();
            ClearPinnedSnapshotState();
            return true;
        }

        private void ReleasePinnedSnapshotBlocksImmediate()
        {
            if (_pinnedReadSource != null)
            {
                for (int i = _pinnedReleaseCursor; i < _pinnedReadBlocks.Length; i++)
                {
                    VoxelReadPinToken token = _pinnedReadBlocks[i];
                    _pinnedReadSource.ReleasePinnedWorldBlock(in token);
                }
            }
            _pinnedReadBlocks.Clear();
            ClearPinnedSnapshotState();
        }

        private void ClearPinnedSnapshotState()
        {
            _pinnedReadSource = null;
            _pinnedMixedVoxels = default;
            _pinnedMixedSurfaceSemantics = default;
            _pinnedMixedBoundarySamples = default;
            _pinnedReleaseCursor = 0;
        }

        private bool StepCells(float voxelSize)
        {
            int cellCount = CellsPerAxis * CellsPerAxis * CellsPerAxis;
            int end = math.min(cellCount, _build.Cursor + CellsPerSlice);
            int3 chunkOrigin = _build.Coordinate * VoxelsPerAxis;

            for (int cellIndex = _build.Cursor; cellIndex < end; cellIndex++)
            {
                int x = cellIndex % CellsPerAxis;
                int y = (cellIndex / CellsPerAxis) % CellsPerAxis;
                int z = cellIndex / (CellsPerAxis * CellsPerAxis);
                PolygoniseCell(chunkOrigin, new int3(x, y, z), voxelSize);
            }

            _build.Cursor = end;
            return end >= cellCount;
        }

        private void PolygoniseCell(int3 chunkOrigin, int3 cell, float voxelSize)
        {
            TryEmitSurfaceDecoration(chunkOrigin + cell, voxelSize);
            int caseCode = 0;
            for (int i = 0; i < 8; i++)
            {
                int3 grid = cell + Padding + TransvoxelRegularTables.CornerOffsets[i];
                int sampleIndex = GridIndex(grid.x, grid.y, grid.z);
                float density = _density[sampleIndex];
                _cellDensity[i] = density;
                _cellMaterial[i] = _materials[sampleIndex];
                _cellSurface[i] = _surfaceSemantics[sampleIndex];
                _cellBoundary[i] = _boundarySamples[sampleIndex];
                if (density < 0f) caseCode |= 1 << i;
            }

            if (caseCode == 0 || caseCode == 255) return;

            bool hasContinuousSurface = false;
            bool hasPlanarSurface = false;
            bool hasRoundedSurface = false;
            bool hasAuthoredBoundary = false;
            bool hasDisplacedCoating = false;
            for (int i = 0; i < 8; i++)
            {
                hasAuthoredBoundary |= _cellBoundary[i] != 0;
                byte coating = (byte)(_cellSurface[i] >> 16);
                hasDisplacedCoating |= _buildCoatingCatalogue.Get(coating).Displacement != 0;
            }
            for (int i = 0; i < 8; i++)
            {
                if (!IsSolidSurfaceMaterial(_cellMaterial[i])) continue;
                SurfaceStyleReadDefinition definition = _buildSurfaceCatalogue.Get(
                    (ushort)_cellSurface[i]);
                if (definition.Reconstruction == SurfaceReconstruction.Smooth
                    || definition.Reconstruction == SurfaceReconstruction.Rounded
                    || definition.Reconstruction == SurfaceReconstruction.Planar
                       && (hasAuthoredBoundary || hasDisplacedCoating))
                {
                    hasContinuousSurface = true;
                    if (definition.Reconstruction == SurfaceReconstruction.Planar)
                        hasPlanarSurface = true;
                    else
                        hasRoundedSurface = true;
                }
            }
            // Planar cells need continuous topology only when authoring retained a sub-voxel
            // boundary. Ordinary planar occupancy has exact axis-aligned faces and is emitted in
            // phase 2, avoiding diagonal marching triangles across walls and piers.
            if (!hasContinuousSurface) return;

            RegularCellData cellData =
                TransvoxelRegularTables.CellData[TransvoxelRegularTables.CellClass[caseCode]];
            ushort[] edgeCodes = TransvoxelRegularTables.VertexData[caseCode];
            int vertexCount = cellData.VertexCount;
            for (int i = 0; i < vertexCount; i++)
            {
                ushort edgeCode = edgeCodes[i];
                int corner0 = (edgeCode >> 4) & 0x0F;
                int corner1 = edgeCode & 0x0F;
                float d0 = _cellDensity[corner0];
                float d1 = _cellDensity[corner1];

                int3 o0 = TransvoxelRegularTables.CornerOffsets[corner0];
                int3 o1 = TransvoxelRegularTables.CornerOffsets[corner1];
                int3 edgeDelta = math.abs(o1 - o0);
                int edgeAxis = edgeDelta.x != 0 ? 0 : edgeDelta.y != 0 ? 1 : 2;
                var boundary0 = new VoxelBoundarySample { Packed = _cellBoundary[corner0] };
                var boundary1 = new VoxelBoundarySample { Packed = _cellBoundary[corner1] };
                if ((boundary0.IsAuthored && !boundary0.AppliesAlong(edgeAxis))
                    || (boundary1.IsAuthored && !boundary1.AppliesAlong(edgeAxis)))
                {
                    d0 = IsSolidSurfaceMaterial(_cellMaterial[corner0]) ? 0.5f : -0.5f;
                    d1 = IsSolidSurfaceMaterial(_cellMaterial[corner1]) ? 0.5f : -0.5f;
                }
                float t0 = math.abs(d1 - d0) > 1e-7f ? d1 / (d1 - d0) : 0.5f;
                float t1 = 1f - t0;
                float3 localVoxel = ((float3)(cell + o0) * t0
                                   + (float3)(cell + o1) * t1) * SourceStep;
                float3 worldVoxel = chunkOrigin + localVoxel + 0.5f;

                int3 g0 = cell + Padding + o0;
                int3 g1 = cell + Padding + o1;
                float3 n0 = DensityNormal(g0);
                float3 n1 = DensityNormal(g1);
                float3 normal = math.normalizesafe(n0 * t0 + n1 * t1,
                                                    new float3(0f, 1f, 0f));

                // Positive density is the solid side. Carry both its material and independent
                // surface semantics; if that endpoint is empty, fall back to another solid corner.
                int selectedCorner = d0 > d1 ? corner0 : corner1;
                byte material = _cellMaterial[selectedCorner];
                uint selectedSurface = _cellSurface[selectedCorner];
                if (!IsSolidSurfaceMaterial(material))
                {
                    for (int corner = 0; corner < 8; corner++)
                    {
                        if (!IsSolidSurfaceMaterial(_cellMaterial[corner])) continue;
                        material = _cellMaterial[corner];
                        selectedSurface = _cellSurface[corner];
                        break;
                    }
                }

                _cellVertices[i] = new SmoothSurfaceVertex
                {
                    Position = (Vector3)(worldVoxel * voxelSize),
                    Normal = (Vector3)normal,
                    Material = PackSurfaceAttributes(material, selectedSurface),
                    Active = FullyLitOcclusion
                };
            }

            int indexCount = cellData.TriangleCount * 3;
            byte[] localIndices = cellData.VertexIndices;
            if (hasPlanarSurface && !hasRoundedSurface)
            {
                // Continuous topology does not imply smooth shading. Duplicate the three
                // vertices of each planar triangle and assign its geometric normal, preserving
                // cut-stone facets while keeping the occupancy-constrained isosurface positions.
                for (int i = 0; i < indexCount; i += 3)
                {
                    SmoothSurfaceVertex a = _cellVertices[localIndices[i]];
                    SmoothSurfaceVertex b = _cellVertices[localIndices[i + 1]];
                    SmoothSurfaceVertex c = _cellVertices[localIndices[i + 2]];
                    Vector3 face = Vector3.Cross(b.Position - a.Position, c.Position - a.Position)
                                          .normalized;
                    Vector3 expected = a.Normal + b.Normal + c.Normal;
                    if (Vector3.Dot(face, expected) < 0f) face = -face;
                    a.Normal = face;
                    b.Normal = face;
                    c.Normal = face;
                    uint triangleBase = (uint)_vertices.Length;
                    _vertices.Add(a);
                    _vertices.Add(b);
                    _vertices.Add(c);
                    _indices.Add(triangleBase);
                    _indices.Add(triangleBase + 1);
                    _indices.Add(triangleBase + 2);
                }
            }
            else
            {
                uint baseVertex = (uint)_vertices.Length;
                for (int i = 0; i < vertexCount; i++) _vertices.Add(_cellVertices[i]);
                for (int i = 0; i < indexCount; i++)
                    _indices.Add(baseVertex + localIndices[i]);
            }
        }

        private bool StepFacetedPlanes(float voxelSize)
        {
            const int planesPerStep = 8;
            int planeCount = 3 * 2 * CellsPerAxis;
            int end = math.min(planeCount, _build.Cursor + planesPerStep);
            int3 chunkOrigin = _build.Coordinate * VoxelsPerAxis;
            for (int planeIndex = _build.Cursor; planeIndex < end; planeIndex++)
            {
                int layer = planeIndex % CellsPerAxis;
                int face = planeIndex / CellsPerAxis;
                int axis = face >> 1;
                int sign = (face & 1) == 0 ? -1 : 1;
                BuildFacetedMask(chunkOrigin, axis, sign, layer);
                MergeFacetedMask(chunkOrigin, axis, sign, layer, voxelSize);
            }
            _build.Cursor = end;
            return end >= planeCount;
        }

        private bool StepProfileBlocks(float voxelSize)
        {
            const int blocksPerStep = 8;
            int end = math.min(_buildProfileBlocks.Length, _build.Cursor + blocksPerStep);
            for (int i = _build.Cursor; i < end; i++)
                EmitProfileBlock(in _buildProfileBlocks[i], voxelSize);
            _build.Cursor = end;
            return end >= _buildProfileBlocks.Length;
        }

        private void EmitProfileBlock(in ProfileBlock block, float voxelSize)
        {
            block.Bounds(out int3 blockMin, out int3 blockMax);
            int3 chunkMin = _build.Coordinate * VoxelsPerAxis;
            int3 chunkMax = chunkMin + VoxelsPerAxis;
            if (math.any(blockMin >= chunkMax) || math.any(blockMax < chunkMin)) return;

            int axisA = (block.Axis + 1) % 3;
            int axisB = (block.Axis + 2) % 3;
            float start = math.atan2(block.StartDirection.y, block.StartDirection.x);
            float finish = math.atan2(block.EndDirection.y, block.EndDirection.x);
            if (finish <= start) finish += math.PI * 2f;
            float inner = block.InnerRadiusQ4 * (1f / 16f);
            float outer = block.OuterRadiusQ4 * (1f / 16f);
            float midRadius = (inner + outer) * 0.5f;
            float jointAngle = block.JointHalfWidthQ4 * (1f / 16f)
                             / math.max(1f, midRadius);
            start += jointAngle;
            finish -= jointAngle;
            if (finish <= start) return;

            float bevel = math.min(block.BevelQ4 * (1f / 16f), (outer - inner) * 0.22f);
            float front = block.FrontQ4 * (1f / 16f);
            float shoulder = math.min(front + bevel, block.BackQ4 * (1f / 16f));
            float back = block.BackQ4 * (1f / 16f);
            float frontInner = inner + bevel;
            float frontOuter = outer - bevel;
            // Radial beds need a much narrower face bevel than the intrados/extrados. Applying
            // the full radial bevel at both sides doubles the authored mortar gap and turns the
            // ring into a black-outlined diagram rather than cut masonry.
            float angularBevel = bevel * 0.32f / math.max(1f, midRadius);
            float faceStart = math.min(start + angularBevel, (start + finish) * 0.5f);
            float faceFinish = math.max(finish - angularBevel, (start + finish) * 0.5f);
            int segments = math.clamp((int)math.ceil((finish - start) * outer / 0.55f), 2, 24);
            for (int segment = 0; segment < segments; segment++)
            {
                float a0 = math.lerp(start, finish, segment / (float)segments);
                float a1 = math.lerp(start, finish, (segment + 1f) / segments);
                float faceA0 = math.lerp(faceStart, faceFinish, segment / (float)segments);
                float faceA1 = math.lerp(faceStart, faceFinish,
                                         (segment + 1f) / segments);
                float3 f00 = ProfilePoint(block.Centre, axisA, axisB, block.Axis,
                                          frontInner, faceA0, front);
                float3 f01 = ProfilePoint(block.Centre, axisA, axisB, block.Axis,
                                          frontOuter, faceA0, front);
                float3 f11 = ProfilePoint(block.Centre, axisA, axisB, block.Axis,
                                          frontOuter, faceA1, front);
                float3 f10 = ProfilePoint(block.Centre, axisA, axisB, block.Axis,
                                          frontInner, faceA1, front);
                if (!TryReadProfileBacking((f00 + f01 + f11 + f10) * 0.25f,
                                           block.Axis, back, out uint backingSurface))
                    continue;
                VoxelSurfaceSemantics current = VoxelSurfaceSemantics.FromPacked(backingSurface);
                uint attributes = PackSurfaceAttributes(block.Material,
                    new VoxelSurfaceSemantics
                    {
                        StyleId = block.SurfaceStyle,
                        CoatingId = current.CoatingId != Coatings.None
                            ? current.CoatingId : block.Coating,
                        Flags = VoxelSurfaceFlags.PreserveFeature,
                        Detail = block.SurfaceDetail,
                    }.Packed);

                float3 frontNormal = float3.zero;
                frontNormal[block.Axis] = -1f;
                EmitProfileQuad(f00, f01, f11, f10, frontNormal, attributes, voxelSize);

                float3 outer0 = ProfilePoint(block.Centre, axisA, axisB, block.Axis,
                                             outer, a0, shoulder);
                float3 outer1 = ProfilePoint(block.Centre, axisA, axisB, block.Axis,
                                             outer, a1, shoulder);
                float3 outerNormal = float3.zero;
                float middle = (a0 + a1) * 0.5f;
                outerNormal[axisA] = math.cos(middle);
                outerNormal[axisB] = math.sin(middle);
                EmitProfileQuad(f01, outer0, outer1, f11,
                                math.normalizesafe(outerNormal - frontNormal * 0.55f),
                                attributes, voxelSize);

                float3 inner0 = ProfilePoint(block.Centre, axisA, axisB, block.Axis,
                                             inner, a0, shoulder);
                float3 inner1 = ProfilePoint(block.Centre, axisA, axisB, block.Axis,
                                             inner, a1, shoulder);
                float3 innerNormal = -outerNormal;
                EmitProfileQuad(inner0, f00, f10, inner1,
                                math.normalizesafe(innerNormal - frontNormal * 0.55f),
                                attributes, voxelSize);

                float3 outerBack0 = ProfilePoint(block.Centre, axisA, axisB, block.Axis,
                                                 outer, a0, back);
                float3 outerBack1 = ProfilePoint(block.Centre, axisA, axisB, block.Axis,
                                                 outer, a1, back);
                EmitProfileQuad(outer0, outerBack0, outerBack1, outer1,
                                outerNormal, attributes, voxelSize);
                float3 innerBack0 = ProfilePoint(block.Centre, axisA, axisB, block.Axis,
                                                 inner, a0, back);
                float3 innerBack1 = ProfilePoint(block.Centre, axisA, axisB, block.Axis,
                                                 inner, a1, back);
                EmitProfileQuad(innerBack0, inner0, inner1, innerBack1,
                                innerNormal, attributes, voxelSize);
            }

            uint sideAttributes = PackSurfaceAttributes(block.Material,
                new VoxelSurfaceSemantics
                {
                    StyleId = block.SurfaceStyle,
                    CoatingId = block.Coating,
                    Flags = VoxelSurfaceFlags.PreserveFeature,
                    Detail = block.SurfaceDetail,
                }.Packed);
            EmitProfileRadialSide(in block, start, inner, outer, frontInner, frontOuter,
                                  faceStart, front, shoulder, back, axisA, axisB, -1f,
                                  sideAttributes, voxelSize);
            EmitProfileRadialSide(in block, finish, inner, outer, frontInner, frontOuter,
                                  faceFinish, front, shoulder, back, axisA, axisB, 1f,
                                  sideAttributes, voxelSize);
        }

        private void EmitProfileRadialSide(in ProfileBlock block, float angle,
                                           float inner, float outer,
                                           float frontInner, float frontOuter,
                                           float faceAngle, float front,
                                           float shoulder, float back,
                                           int axisA, int axisB, float sign,
                                           uint attributes, float voxelSize)
        {
            float3 f0 = ProfilePoint(block.Centre, axisA, axisB, block.Axis,
                                     frontInner, faceAngle, front);
            float3 f1 = ProfilePoint(block.Centre, axisA, axisB, block.Axis,
                                     frontOuter, faceAngle, front);
            float3 s0 = ProfilePoint(block.Centre, axisA, axisB, block.Axis,
                                     inner, angle, shoulder);
            float3 s1 = ProfilePoint(block.Centre, axisA, axisB, block.Axis,
                                     outer, angle, shoulder);
            float3 b0 = ProfilePoint(block.Centre, axisA, axisB, block.Axis,
                                     inner, angle, back);
            float3 b1 = ProfilePoint(block.Centre, axisA, axisB, block.Axis,
                                     outer, angle, back);
            if (!TryReadProfileBacking((f0 + f1 + b0 + b1) * 0.25f,
                                       block.Axis, back, out _)) return;
            float3 normal = float3.zero;
            normal[axisA] = -math.sin(angle) * sign;
            normal[axisB] = math.cos(angle) * sign;
            float3 frontNormal = float3.zero;
            frontNormal[block.Axis] = -1f;
            EmitProfileQuad(f0, s0, s1, f1,
                            math.normalizesafe(normal - frontNormal * 0.55f),
                            attributes, voxelSize);
            EmitProfileQuad(s0, b0, b1, s1, normal, attributes, voxelSize);
        }

        private bool TryReadProfileBacking(float3 position, int axis, float back,
                                           out uint surface)
        {
            int3 voxel = (int3)math.round(position);
            voxel[axis] = (int)math.round(back);
            ReadSnapshotCell(voxel, out byte material, out surface, out _);
            return IsSolidSurfaceMaterial(material);
        }

        private void EmitProfileQuad(float3 p0, float3 p1, float3 p2, float3 p3,
                                     float3 normal, uint attributes, float voxelSize)
        {
            float3 centroid = (p0 + p1 + p2 + p3) * 0.25f;
            int3 owner = new(FloorDiv((int)math.floor(centroid.x), VoxelsPerAxis),
                             FloorDiv((int)math.floor(centroid.y), VoxelsPerAxis),
                             FloorDiv((int)math.floor(centroid.z), VoxelsPerAxis));
            if (!owner.Equals(_build.Coordinate)) return;
            p0 *= voxelSize; p1 *= voxelSize; p2 *= voxelSize; p3 *= voxelSize;
            uint baseVertex = (uint)_vertices.Length;
            var n = (Vector3)math.normalizesafe(normal, new float3(0f, 1f, 0f));
            _vertices.Add(new SmoothSurfaceVertex { Position = (Vector3)p0, Normal = n, Material = attributes, Active = FullyLitOcclusion });
            _vertices.Add(new SmoothSurfaceVertex { Position = (Vector3)p1, Normal = n, Material = attributes, Active = FullyLitOcclusion });
            _vertices.Add(new SmoothSurfaceVertex { Position = (Vector3)p2, Normal = n, Material = attributes, Active = FullyLitOcclusion });
            _vertices.Add(new SmoothSurfaceVertex { Position = (Vector3)p3, Normal = n, Material = attributes, Active = FullyLitOcclusion });
            bool flip = math.dot(math.cross(p1 - p0, p2 - p0), normal) < 0f;
            if (flip)
            {
                _indices.Add(baseVertex); _indices.Add(baseVertex + 2); _indices.Add(baseVertex + 1);
                _indices.Add(baseVertex); _indices.Add(baseVertex + 3); _indices.Add(baseVertex + 2);
            }
            else
            {
                _indices.Add(baseVertex); _indices.Add(baseVertex + 1); _indices.Add(baseVertex + 2);
                _indices.Add(baseVertex); _indices.Add(baseVertex + 2); _indices.Add(baseVertex + 3);
            }
        }

        private static float3 ProfilePoint(int3 centre, int axisA, int axisB, int axis,
                                           float radius, float angle, float depth)
        {
            float3 point = centre;
            point[axisA] += math.cos(angle) * radius;
            point[axisB] += math.sin(angle) * radius;
            point[axis] = depth;
            return point;
        }

        private void TryEmitSurfaceDecoration(int3 voxel, float voxelSize)
        {
            ReadSnapshotCell(voxel, out byte material, out uint surface, out _);
            if (!IsSolidSurfaceMaterial(material)) return;
            byte coating = (byte)(surface >> 16);
            CoatingReadDefinition definition = _buildCoatingCatalogue.Get(coating);
            if (definition.DecorationShape == SurfaceDecorationShape.None
                || definition.DecorationDensity == 0) return;

            for (int face = 0; face < 6; face++)
            {
                if ((definition.DecorationFaceMask & (1 << face)) == 0) continue;
                int axis = face >> 1;
                int sign = (face & 1) == 0 ? -1 : 1;
                int3 neighbour = voxel;
                neighbour[axis] += sign;
                ReadSnapshotCell(neighbour, out byte adjacent, out _, out _);
                if (IsSolidSurfaceMaterial(adjacent)) continue;
                uint faceHash = DecorationHash(voxel, (byte)(coating + face * 17));
                if ((faceHash & 0xFFu) >= definition.DecorationDensity) continue;
                if (!IsDecorationAnchor(voxel, coating, face,
                                        definition.DecorationSeparation)) continue;

                EmitDecorationClump(voxel, material, surface, definition,
                                    axis, sign, faceHash, voxelSize);
                CompletedDecorationClumps++;
            }
        }

        private static bool IsDecorationAnchor(int3 voxel, byte coating, int face, int separation)
        {
            if (separation <= 0) return true;
            int stride = separation + 1;
            int axis = face >> 1;
            int axisA = (axis + 1) % 3;
            int axisB = (axis + 2) % 3;
            uint rowHash = DecorationHash(voxel, (byte)(coating + face * 17));
            int offsetA = (int)(rowHash % (uint)stride);
            int offsetB = (int)((rowHash >> 8) % (uint)stride);
            return FloorMod(voxel[axisA] + offsetA, stride) == 0
                && FloorMod(voxel[axisB] + offsetB, stride) == 0;
        }

        private void EmitDecorationClump(int3 voxel, byte material, uint surface,
                                         CoatingReadDefinition definition, int axis, int sign,
                                         uint hash, float voxelSize)
        {
            int axisA = (axis + 1) % 3;
            int axisB = (axis + 2) % 3;
            float jitterA = (((hash >> 8) & 0xFFu) * (1f / 255f) - 0.5f) * 0.42f;
            float jitterB = (((hash >> 16) & 0xFFu) * (1f / 255f) - 0.5f) * 0.42f;
            float radiusA = definition.DecorationRadiusQ4 * (1f / 16f)
                          * math.lerp(0.85f, 1.18f, ((hash >> 5) & 0xFFu) / 255f);
            float radiusB = definition.DecorationRadiusQ4 * (1f / 16f)
                          * math.lerp(0.62f, 1.05f, ((hash >> 13) & 0xFFu) / 255f);
            float height = definition.DecorationHeightQ4 * (1f / 16f)
                         * math.lerp(0.78f, 1.22f, ((hash >> 24) & 0xFFu) * (1f / 255f));
            float3 centre = (float3)voxel + new float3(0.5f);
            centre[axis] = voxel[axis] + (sign > 0 ? 1f : 0f);
            centre[axisA] += jitterA;
            centre[axisB] += jitterB;
            uint attributes = PackSurfaceAttributes(material, surface);
            uint baseVertex = (uint)_vertices.Length;
            const int sides = 6;
            for (int ring = 0; ring < 2; ring++)
            {
                float scale = ring == 0 ? 1f : 0.94f;
                float normalOffset = ring == 0 ? 0.025f : height;
                for (int side = 0; side < sides; side++)
                {
                    float angle = (side + ((hash >> (side + 3)) & 7u) * 0.018f)
                                * (math.PI * 2f / sides);
                    AddMoundVertex(centre, axis, axisA, axisB, sign,
                                   math.cos(angle) * radiusA * scale,
                                   math.sin(angle) * radiusB * scale,
                                   normalOffset, attributes, voxelSize);
                }
            }

            for (uint side = 0; side < sides; side++)
            {
                uint next = (side + 1u) % sides;
                _indices.Add(baseVertex + side);
                _indices.Add(baseVertex + sides + side);
                _indices.Add(baseVertex + next);
                _indices.Add(baseVertex + next);
                _indices.Add(baseVertex + sides + side);
                _indices.Add(baseVertex + sides + next);
            }
            for (uint side = 1; side < sides - 1; side++)
            {
                _indices.Add(baseVertex + sides);
                _indices.Add(baseVertex + sides + side + 1u);
                _indices.Add(baseVertex + sides + side);
            }

            if (axis == 1 && sign > 0 && definition.DecorationDropQ4 > 0)
                EmitDecorationFringes(voxel, centre, axisA, axisB, radiusA, radiusB,
                                      definition.DecorationDropQ4 * (1f / 16f), hash,
                                      attributes, voxelSize);
        }

        private void EmitDecorationFringes(int3 voxel, float3 centre, int axisA, int axisB,
                                           float radiusA, float radiusB, float maxDrop,
                                           uint hash, uint attributes, float voxelSize)
        {
            for (int face = 0; face < 6; face++)
            {
                int faceAxis = face >> 1;
                if (faceAxis == 1) continue;
                int faceSign = (face & 1) == 0 ? -1 : 1;
                int3 neighbour = voxel;
                neighbour[faceAxis] += faceSign;
                ReadSnapshotCell(neighbour, out byte adjacent, out _, out _);
                if (IsSolidSurfaceMaterial(adjacent)) continue;

                int direction = faceAxis == axisB
                    ? (faceSign > 0 ? 0 : 1)
                    : (faceSign > 0 ? 2 : 3);
                EmitDecorationFringe(centre, axisA, axisB, radiusA, radiusB,
                                     maxDrop, hash ^ (uint)(face * 0x9E37), direction,
                                     attributes, voxelSize);
            }
        }

        private void EmitDecorationFringe(float3 centre, int axisA, int axisB,
                                          float radiusA, float radiusB, float maxDrop,
                                          uint hash, int direction,
                                          uint attributes, float voxelSize)
        {
            // A shallow skirt hangs only over a genuinely exposed ledge. Its extent varies
            // deterministically, while world occupancy decides direction.
            float3 tangent = float3.zero;
            float3 outward = float3.zero;
            if (direction < 2)
            {
                tangent[axisA] = 1f;
                outward[axisB] = direction == 0 ? 1f : -1f;
            }
            else
            {
                tangent[axisB] = 1f;
                outward[axisA] = direction == 2 ? 1f : -1f;
            }
            float halfWidth = math.lerp(0.20f, 0.42f, ((hash >> 3) & 0xFFu) / 255f)
                            * (direction < 2 ? radiusA : radiusB);
            float drop = maxDrop * math.lerp(0.38f, 1f, ((hash >> 11) & 0xFFu) / 255f);
            float outwardRadius = direction < 2 ? radiusB : radiusA;
            float3 lip = centre + outward * outwardRadius;
            float3 p0 = lip - tangent * halfWidth;
            float3 p1 = lip + tangent * halfWidth;
            float3 p2 = p1 - new float3(0f, drop, 0f) - outward * 0.07f;
            float3 p3 = p0 - new float3(0f, drop * 0.72f, 0f) - outward * 0.05f;
            EmitProfileQuad(p0, p1, p2, p3, outward, attributes, voxelSize);
        }

        private void AddMoundVertex(float3 centre, int axis, int axisA, int axisB, int sign,
                                    float a, float b, float height,
                                    uint attributes, float voxelSize)
        {
            float3 position = centre;
            position[axisA] += a;
            position[axisB] += b;
            position[axis] += height * sign;
            float3 normal = float3.zero;
            normal[axis] = sign;
            _vertices.Add(new SmoothSurfaceVertex
            {
                Position = (Vector3)(position * voxelSize), Normal = (Vector3)normal,
                Material = attributes, Active = FullyLitOcclusion
            });
        }

        private static uint DecorationHash(int3 voxel, byte coating)
        {
            uint h = (uint)voxel.x * 0x9E3779B9u ^ (uint)voxel.y * 0x85EBCA6Bu
                   ^ (uint)voxel.z * 0xC2B2AE35u ^ (uint)coating * 0x27D4EB2Fu;
            h ^= h >> 16;
            h *= 0x7FEB352Du;
            h ^= h >> 15;
            return h;
        }

        private void BuildFacetedMask(int3 chunkOrigin, int axis, int sign, int layer)
        {
            int axisA = (axis + 1) % 3;
            int axisB = (axis + 2) % 3;
            for (int b = 0; b < CellsPerAxis; b++)
            for (int a = 0; a < CellsPerAxis; a++)
            {
                int3 local = int3.zero;
                local[axis] = layer;
                local[axisA] = a;
                local[axisB] = b;
                int3 voxel = chunkOrigin + local * SourceStep;
                ReadSnapshotCell(voxel, out byte material, out uint surface,
                                 out byte boundary);
                SurfaceStyleReadDefinition style = _buildSurfaceCatalogue.Get((ushort)surface);
                byte coating = (byte)(surface >> 16);
                bool displacedCoating =
                    _buildCoatingCatalogue.Get(coating).Displacement != 0;
                var authoredBoundary = new VoxelBoundarySample { Packed = boundary };
                bool boundaryAffectsFace = authoredBoundary.AppliesAlong(axis);
                bool faceted = IsSolidSurfaceMaterial(material)
                    && (style.Reconstruction == SurfaceReconstruction.Sharp
                        || style.Reconstruction == SurfaceReconstruction.Cubic
                        || style.Reconstruction == SurfaceReconstruction.Planar
                           && !boundaryAffectsFace && !displacedCoating);
                if (!faceted)
                {
                    _faceMask[a + b * CellsPerAxis] = 0;
                    continue;
                }

                int3 neighbour = voxel;
                neighbour[axis] += sign * SourceStep;
                ReadSnapshotCell(neighbour, out byte neighbourMaterial, out _,
                                 out byte neighbourBoundary);
                var neighbourAuthoredBoundary =
                    new VoxelBoundarySample { Packed = neighbourBoundary };
                _faceMask[a + b * CellsPerAxis] = IsSolidSurfaceMaterial(neighbourMaterial)
                    || neighbourAuthoredBoundary.AppliesAlong(axis)
                    ? 0u : PackSurfaceAttributes(material, surface) + 1u;
            }
        }

        private void MergeFacetedMask(int3 chunkOrigin, int axis, int sign, int layer,
                                      float voxelSize)
        {
            int axisA = (axis + 1) % 3;
            int axisB = (axis + 2) % 3;
            for (int b = 0; b < CellsPerAxis; b++)
            for (int a = 0; a < CellsPerAxis; a++)
            {
                uint encoded = _faceMask[a + b * CellsPerAxis];
                if (encoded == 0) continue;
                int width = 1;
                while (a + width < CellsPerAxis
                       && _faceMask[a + width + b * CellsPerAxis] == encoded) width++;
                int height = 1;
                bool extend = true;
                while (b + height < CellsPerAxis && extend)
                {
                    for (int k = 0; k < width; k++)
                    {
                        if (_faceMask[a + k + (b + height) * CellsPerAxis] == encoded) continue;
                        extend = false;
                        break;
                    }
                    if (extend) height++;
                }
                for (int db = 0; db < height; db++)
                for (int da = 0; da < width; da++)
                    _faceMask[a + da + (b + db) * CellsPerAxis] = 0;
                EmitFacetedQuad(chunkOrigin, axis, axisA, axisB, sign, layer,
                                a, b, width, height, encoded - 1u, voxelSize);
            }
        }

        private void EmitFacetedQuad(int3 chunkOrigin, int axis, int axisA, int axisB,
                                     int sign, int layer, int a, int b,
                                     int width, int height, uint attributes, float voxelSize)
        {
            float3 p0 = chunkOrigin;
            p0[axis] += (layer + (sign > 0 ? 1 : 0)) * SourceStep;
            p0[axisA] += a * SourceStep;
            p0[axisB] += b * SourceStep;
            float3 p1 = p0;
            float3 p2 = p0;
            float3 p3 = p0;
            p1[axisA] += width * SourceStep;
            p2[axisA] += width * SourceStep;
            p2[axisB] += height * SourceStep;
            p3[axisB] += height * SourceStep;
            p0 *= voxelSize;
            p1 *= voxelSize;
            p2 *= voxelSize;
            p3 *= voxelSize;
            float3 normal = float3.zero;
            normal[axis] = sign;
            uint baseVertex = (uint)_vertices.Length;
            _vertices.Add(new SmoothSurfaceVertex { Position = (Vector3)p0, Normal = (Vector3)normal, Material = attributes, Active = FullyLitOcclusion });
            _vertices.Add(new SmoothSurfaceVertex { Position = (Vector3)p1, Normal = (Vector3)normal, Material = attributes, Active = FullyLitOcclusion });
            _vertices.Add(new SmoothSurfaceVertex { Position = (Vector3)p2, Normal = (Vector3)normal, Material = attributes, Active = FullyLitOcclusion });
            _vertices.Add(new SmoothSurfaceVertex { Position = (Vector3)p3, Normal = (Vector3)normal, Material = attributes, Active = FullyLitOcclusion });
            bool flip = math.dot(math.cross(p1 - p0, p2 - p0), normal) < 0f;
            if (flip)
            {
                _indices.Add(baseVertex); _indices.Add(baseVertex + 2); _indices.Add(baseVertex + 1);
                _indices.Add(baseVertex); _indices.Add(baseVertex + 3); _indices.Add(baseVertex + 2);
            }
            else
            {
                _indices.Add(baseVertex); _indices.Add(baseVertex + 1); _indices.Add(baseVertex + 2);
                _indices.Add(baseVertex); _indices.Add(baseVertex + 2); _indices.Add(baseVertex + 3);
            }
        }

        private void ReadSnapshotCell(int3 voxel, out byte material, out uint surface,
                                      out byte boundary)
        {
            int3 chunkBrickOrigin = _build.Coordinate * BricksPerAxis;
            int3 cacheOrigin = chunkBrickOrigin - BrickCachePadding;
            int3 worldBrick = voxel >> VoxelReadGrid.BlockEdgeLog2;
            int3 localBrick = worldBrick - cacheOrigin;
            if (math.any(localBrick < 0) || math.any(localBrick >= BrickCacheEdge))
            {
                material = 0;
                surface = 0;
                boundary = 0;
                return;
            }
            int brickIndex = localBrick.x
                           + BrickCacheEdge * (localBrick.y + BrickCacheEdge * localBrick.z);
            TransvoxelDensityBrick brick = _densityBricks[brickIndex];
            if (brick.Kind == 0)
            {
                material = 0;
                surface = 0;
                boundary = 0;
                return;
            }
            if (brick.Kind == 1)
            {
                material = brick.UniformMaterial;
                surface = _buildPalette.GetDefaultSurfaceStyle(material);
                boundary = 0;
                return;
            }
            int3 local = voxel & VoxelReadGrid.BlockEdgeMask;
            int voxelIndex = local.x | (local.y << 3) | (local.z << 6);
            NativeArray<byte> mixedVoxels = PinnedMixedVoxelsOrFallback();
            NativeArray<ushort> mixedSurfaces = PinnedMixedSurfaceSemanticsOrFallback();
            NativeArray<byte> mixedBoundaries = PinnedMixedBoundarySamplesOrFallback();
            material = mixedVoxels[brick.MixedOffset + voxelIndex];
            surface = VoxelSurfaceSemantics.FromStorage(
                mixedSurfaces[brick.MixedOffset + voxelIndex]).Packed;
            boundary = mixedBoundaries[brick.MixedOffset + voxelIndex];
            if ((ushort)surface == SurfaceStyles.MaterialDefault)
                surface = (surface & 0xFFFF0000u)
                        | _buildPalette.GetDefaultSurfaceStyle(material);
        }

        private float3 DensityNormal(int3 grid)
        {
            float x = DensityAtGrid(grid.x - 1, grid.y, grid.z)
                    - DensityAtGrid(grid.x + 1, grid.y, grid.z);
            float y = DensityAtGrid(grid.x, grid.y - 1, grid.z)
                    - DensityAtGrid(grid.x, grid.y + 1, grid.z);
            float z = DensityAtGrid(grid.x, grid.y, grid.z - 1)
                    - DensityAtGrid(grid.x, grid.y, grid.z + 1);
            return math.normalizesafe(new float3(x, y, z), new float3(0f, 1f, 0f));
        }

        private float DensityAtGrid(int x, int y, int z)
        {
            x = math.clamp(x, 0, GridSize - 1);
            y = math.clamp(y, 0, GridSize - 1);
            z = math.clamp(z, 0, GridSize - 1);
            return _density[GridIndex(x, y, z)];
        }

        private static bool IsSolidSurfaceMaterial(byte material) =>
            material != 0 && material != 11 && material != 16;

        private static uint PackSurfaceAttributes(byte material, uint surface) =>
            material
            | (((surface >> 16) & 0xFFu) << 8)
            | ((surface & 0xFFu) << 16)
            | (((surface >> 24) & 0xFFu) << 24);

        private SurfaceGeometryArena GetGeometryArena()
        {
            // Scheduler workers receive an eagerly allocated shared arena. Standalone caches
            // remain cheap until they actually publish their first piece of geometry.
            if (_geometryArena == null)
                _geometryArena = new SurfaceGeometryArena(256 * 1024, 768 * 1024, 512);
            return _geometryArena;
        }

        private Entry AcquireEntry(int3 coordinate)
        {
            if (_entryPool.Count == 0)
                return new Entry(coordinate, VoxelsPerAxis, SourceStep, GetGeometryArena());

            Entry entry = _entryPool.Pop();
            entry.Reinitialize(coordinate);
            return entry;
        }

        private void RecycleEntry(Entry entry)
        {
            if (entry == null) return;
            entry.Dispose();
            _entryPool.Push(entry);
        }

        private void FinishBuild(int frame)
        {
            if (!BuildOwnsCurrentSlot())
            {
                RejectPendingOrCompletedBuild(stale: true);
                return;
            }
            if (_desiredVersions.TryGetValue(_build.Coordinate, out ulong desired)
                && desired > _build.SourceVersion)
            {
                RejectPendingOrCompletedBuild(stale: true);
                return;
            }

            // An empty result is complete immediately because there is no GPU payload to
            // publish. Removing an old ready entry is the atomic publication of "air".
            if (_indices.Length == 0)
            {
                if (_entries.TryGetValue(_build.Coordinate, out Entry stale))
                {
                    RecycleEntry(stale);
                    _entries.Remove(_build.Coordinate);
                }
                _emptyVersions[_build.Coordinate] = _build.SourceVersion;
                if (SourceStep == FeaturePreservingFallbackStep)
                    Step4FalseEmptyDiagnostics.RecordReadyEmptyPublication(
                        _build.Coordinate, _build.HasOwnedSolid,
                        _buildProfileBlocks.Length != 0,
                        _build.UsedFeaturePreservingFallback);
                CompletedBuildCount++;
                _buildLatencyTiming.Add(ElapsedMs(_build.BuildStartSeconds));
                _desiredVersions.Remove(_build.Coordinate);
                _queuedAtSeconds.Remove(_build.Coordinate);
                ResetCompletedBuild();
                return;
            }

            _emptyVersions.Remove(_build.Coordinate);
            if (!_entries.TryGetValue(_build.Coordinate, out Entry entry))
            {
                entry = AcquireEntry(_build.Coordinate);
                _entries.Add(_build.Coordinate, entry);
            }

            // CPU geometry is complete, but GPU publication is a scheduler-owned phase.
            // Keep the build payload and any previous ready Entry alive until the global
            // upload budget admits this replacement.
            _pendingUpload = true;
        }

        /// <summary>
        /// Advances one pending GPU publication by at most <paramref name="byteBudget"/>
        /// payload bytes. Returns true only when the replacement became visible.
        /// </summary>
        public bool TryPublishPending(int frame, int byteBudget, out int uploadedBytes)
        {
            uploadedBytes = 0;
            if (!_pendingUpload || byteBudget <= 0) return false;
            if (!BuildOwnsCurrentSlot())
            {
                RejectPendingOrCompletedBuild(stale: true);
                return false;
            }

            if (_desiredVersions.TryGetValue(_build.Coordinate, out ulong desired)
                && desired > _build.SourceVersion)
            {
                RejectPendingOrCompletedBuild(stale: true);
                return false;
            }

            if (!_entries.TryGetValue(_build.Coordinate, out Entry entry))
            {
                RejectPendingOrCompletedBuild(stale: false);
                return false;
            }

            double uploadStart = Time.realtimeSinceStartupAsDouble;
            bool published;
            using (s_UploadMarker.Auto())
                published = entry.AdvanceUpload(_vertices, _indices, byteBudget,
                                                out uploadedBytes);
            LastUploadMs = (Time.realtimeSinceStartupAsDouble - uploadStart) * 1000.0;
            _uploadTiming.Add(LastUploadMs);
            UploadedGeometryBytes += (ulong)math.max(0, uploadedBytes);
            if (!published) return false;

            entry.LastUsedFrame = frame;
            entry.SourceVersion = _build.SourceVersion;
            entry.MaterialPaletteVersion = _build.MaterialPaletteVersion;
            entry.SurfaceCatalogueVersion = _build.SurfaceCatalogueVersion;
            entry.SurfaceCatalogueHash = _build.SurfaceCatalogueHash;
            entry.CoatingCatalogueVersion = _build.CoatingCatalogueVersion;
            entry.CoatingCatalogueHash = _build.CoatingCatalogueHash;
            CompletedBuildCount++;
            if (_build.UsedFeaturePreservingFallback)
            {
                FeaturePreservingFallbackPublishCount++;
                Step4FalseEmptyDiagnostics.RecordFallbackPublished();
            }
            _buildLatencyTiming.Add(ElapsedMs(_build.BuildStartSeconds));
            _desiredVersions.Remove(_build.Coordinate);
            _queuedAtSeconds.Remove(_build.Coordinate);
            ResetCompletedBuild();
            return true;
        }

        private void RejectPendingOrCompletedBuild(bool stale)
        {
            if (_entries.TryGetValue(_build.Coordinate, out Entry entry))
            {
                entry.CancelUpload();
                if (!entry.Ready)
                {
                    RecycleEntry(entry);
                    _entries.Remove(_build.Coordinate);
                }
            }
            if (stale) StaleBuildCount++;
            ResetCompletedBuild();
        }

        private void ResetCompletedBuild()
        {
            if (_pinnedReadBlocks.Length != 0 || _pinnedRegionCount != 0
                || _exactMetadataJobScheduled || _exactClassificationJobScheduled
                || _hlodJobScheduled)
                throw new InvalidOperationException(
                    "Build reset attempted before snapshot jobs/Storage leases were released.");
            _exactMetadataReady = false;
            _exactMixedPinCursor = 0;
            _pendingUpload = false;
            _discardBuildAfterPinRelease = false;
            _build = default;
            _vertices.Clear();
            _indices.Clear();
            _transitionFace = -1;
            _transitionSampleCursor = 0;
            _transitionResultPending = false;
            _resultAppendStage = 0;
            _topologyAppendVertexCursor = 0;
            _topologyAppendIndexCursor = 0;
            _facetedAppendVertexCursor = 0;
            _facetedAppendIndexCursor = 0;
            _transitionAppendVertexCursor = 0;
            _transitionAppendIndexCursor = 0;
        }

        public void SetClipmapWindow(int3 centre, int radius)
        {
            int nextRadius = math.max(0, radius);
            if (_clipmapWindowValid && _clipmapRadius == nextRadius
                && math.any(_clipmapCenter != centre))
                ScheduleClipmapEdgeRetirement(_clipmapCenter, centre, nextRadius);

            _clipmapCenter = centre;
            _clipmapRadius = nextRadius;
            _clipmapWindowValid = true;
            _slotGrid.UpdateWindow(centre, nextRadius);
        }

        private void ScheduleClipmapEdgeRetirement(int3 fromCenter, int3 toCenter, int radius)
        {
            if (math.all(fromCenter == toCenter)) return;

            if (_clipmapEdgeRetirementPending && _clipmapRetirementRadius == radius)
            {
                int3 activeDelta = _clipmapRetirementToCenter - _clipmapRetirementFromCenter;
                int3 extendedDelta = toCenter - _clipmapRetirementFromCenter;
                bool sameDirection = true;
                for (int axis = 0; axis < 3; axis++)
                {
                    int active = activeDelta[axis];
                    int extended = extendedDelta[axis];
                    if (active != 0 && extended != 0
                        && math.sign(active) != math.sign(extended))
                    {
                        sameDirection = false;
                        break;
                    }
                }

                // Continuous movement in the same direction simply extends the outgoing slab.
                // Keep the existing cursor so already-checked edge coordinates are not revisited.
                if (sameDirection)
                {
                    _clipmapRetirementToCenter = toCenter;
                    return;
                }
            }

            _clipmapRetirementFromCenter = fromCenter;
            _clipmapRetirementToCenter = toCenter;
            _clipmapRetirementRadius = radius;
            _clipmapRetirementAxis = 0;
            _clipmapRetirementDepth = 0;
            _clipmapRetirementPlaneCursor = 0;
            _clipmapEdgeRetirementPending = true;
        }

        private void StepClipmapEdgeRetirement()
        {
            if (!_clipmapEdgeRetirementPending) return;

            int remaining = ClipmapEdgeCandidatesPerPrepare;
            int edge = _clipmapRetirementRadius * 2 + 1;
            int planeCount = edge * edge;
            int3 delta = _clipmapRetirementToCenter - _clipmapRetirementFromCenter;

            while (remaining > 0 && _clipmapRetirementAxis < 3)
            {
                int axis = _clipmapRetirementAxis;
                int shift = delta[axis];
                int depthCount = math.min(math.abs(shift), edge);
                if (depthCount == 0 || _clipmapRetirementDepth >= depthCount)
                {
                    _clipmapRetirementAxis++;
                    _clipmapRetirementDepth = 0;
                    _clipmapRetirementPlaneCursor = 0;
                    continue;
                }

                int axisA = (axis + 1) % 3;
                int axisB = (axis + 2) % 3;
                while (remaining > 0 && _clipmapRetirementPlaneCursor < planeCount)
                {
                    int linear = _clipmapRetirementPlaneCursor++;
                    int a = linear % edge;
                    int b = linear / edge;
                    int3 coordinate = _clipmapRetirementFromCenter;
                    coordinate[axisA] += a - _clipmapRetirementRadius;
                    coordinate[axisB] += b - _clipmapRetirementRadius;
                    coordinate[axis] += shift > 0
                        ? -_clipmapRetirementRadius + _clipmapRetirementDepth
                        : _clipmapRetirementRadius - _clipmapRetirementDepth;
                    remaining--;

                    // Diagonal movement makes edge planes overlap. Current-window ownership and
                    // _known membership make those duplicates free without another hash set.
                    if (WithinClipmapWindow(coordinate) || !OwnsShard(coordinate)
                        || !_known.Contains(coordinate))
                        continue;
                    if (!TryRemoveChunk(coordinate)) RequeueResidency(coordinate);
                }

                if (_clipmapRetirementPlaneCursor < planeCount) return;
                _clipmapRetirementPlaneCursor = 0;
                _clipmapRetirementDepth++;
            }

            if (_clipmapRetirementAxis < 3) return;
            _clipmapEdgeRetirementPending = false;
            _clipmapRetirementAxis = 0;
            _clipmapRetirementDepth = 0;
            _clipmapRetirementPlaneCursor = 0;
        }

        private bool WithinClipmapWindow(int3 chunk)
        {
            if (!_clipmapWindowValid) return true;
            int3 delta = math.abs(chunk - _clipmapCenter);
            return math.cmax(delta) <= _clipmapRadius;
        }

        private bool TrackKnown(int3 chunk)
        {
            // Surface discovery/change feeds can cover a much larger resident Storage window than
            // this LOD ring draws. Render residency is admitted only inside the camera clipmap;
            // otherwise _known and, critically, the build queue would grow with world streaming
            // rather than the fixed view footprint.
            if (!WithinClipmapWindow(chunk)) return false;
            if (_known.Contains(chunk)) return true;
            if (!_slotGrid.TryAcquire(chunk, out _)) return false;

            _known.Add(chunk);
            RequeueResidency(chunk);
            return true;
        }

        private bool BuildOwnsCurrentSlot()
        {
            return _build.Active && WithinClipmapWindow(_build.Coordinate)
                && _slotGrid.TryGet(_build.Coordinate, out SurfaceChunkSlot slot)
                && slot.Generation == _build.SlotGeneration;
        }

        private void RetireSlot(int3 chunk)
        {
            _slotGrid.Retire(chunk);
        }

        private void RequeueResidency(int3 chunk)
        {
            if (!_known.Contains(chunk) || !_queuedResidency.Add(chunk)) return;
            _residencyQueue.Enqueue(chunk);
        }

        private void StepResidencyPrune(IRegionReadSource source)
        {
            int checks = math.min(ResidencyChecksPerPrepare, _residencyQueue.Count);
            for (int i = 0; i < checks; i++)
            {
                int3 chunk = _residencyQueue.Dequeue();
                _queuedResidency.Remove(chunk);
                if (!_known.Contains(chunk)) continue;

                if (WithinClipmapWindow(chunk) && AllOwnedCoreRegionsResident(source, chunk))
                {
                    RequeueResidency(chunk);
                    continue;
                }

                // Out-of-window or non-resident chunks both retire incrementally. In-flight
                // geometry is never waited on, and BuildOwnsCurrentSlot prevents an out-of-window
                // generation from publishing while it waits for this cleanup pass. If removal is deferred, put the chunk
                // back in the liveness queue and recheck it on a later frame.
                if (!TryRemoveChunk(chunk)) RequeueResidency(chunk);
            }
        }

        /// <summary>
        /// Whether every Storage region intersecting the chunk's unpadded owned core is
        /// currently resident. Exact extraction may optimistically treat an unavailable halo as
        /// empty, but a missing core region can never satisfy exact-snapshot completeness and
        /// must not remain active build demand merely because its halo touches resident Storage.
        /// A later residency publication re-runs surface discovery and readmits the chunk.
        /// </summary>
        private bool AllOwnedCoreRegionsResident(IRegionReadSource source, int3 chunk)
        {
            int3 minVoxel = chunk * VoxelsPerAxis;
            int3 maxVoxel = (chunk + 1) * VoxelsPerAxis - 1;
            int3 minRegion = new(FloorDiv(minVoxel.x, VoxelGrid.RegionVoxelEdge),
                                 FloorDiv(minVoxel.y, VoxelGrid.RegionVoxelEdge),
                                 FloorDiv(minVoxel.z, VoxelGrid.RegionVoxelEdge));
            int3 maxRegion = new(FloorDiv(maxVoxel.x, VoxelGrid.RegionVoxelEdge),
                                 FloorDiv(maxVoxel.y, VoxelGrid.RegionVoxelEdge),
                                 FloorDiv(maxVoxel.z, VoxelGrid.RegionVoxelEdge));

            for (int z = minRegion.z; z <= maxRegion.z; z++)
            for (int y = minRegion.y; y <= maxRegion.y; y++)
            for (int x = minRegion.x; x <= maxRegion.x; x++)
                if (!source.IsRegionResident(new int3(x, y, z))) return false;
            return true;
        }


        internal bool TryEvictOneForArenaPressure(Camera camera, float voxelSize)
        {
            if (_entries.Count == 0) return false;

            int3 victim = default;
            float farthest = -1f;
            Vector3 cameraPosition = camera != null ? camera.transform.position : Vector3.zero;
            float chunkMetres = VoxelsPerAxis * voxelSize;
            if (camera != null) GeometryUtility.CalculateFrustumPlanes(camera, _frustumPlanes);

            foreach (var pair in _entries)
            {
                // Keep current replacement geometry alive. Arena pressure may only retire a
                // different, already-published, offscreen lease.
                if (_build.Active && pair.Key.Equals(_build.Coordinate)) continue;
                Bounds bounds = ChunkWorldBounds(pair.Key, voxelSize);
                if (camera != null && GeometryUtility.TestPlanesAABB(_frustumPlanes, bounds))
                    continue;

                Vector3 centre = (new Vector3(pair.Key.x, pair.Key.y, pair.Key.z)
                                + Vector3.one * 0.5f) * chunkMetres;
                float distance = (centre - cameraPosition).sqrMagnitude;
                if (distance <= farthest) continue;
                farthest = distance;
                victim = pair.Key;
            }

            if (farthest < 0f) return false;
            if (_entries.TryGetValue(victim, out Entry entry)) RecycleEntry(entry);
            _entries.Remove(victim);
            MarkDirty(victim);
            return true;
        }

        /// <summary>
        /// Squared camera distance of the chunk this worker is waiting to publish, if any. The
        /// scheduler uses the nearest such chunk to decide which resident leases may be retired
        /// when the arena has no offscreen geometry left to give up.
        /// </summary>
        internal bool TryGetPendingPublishDistanceSq(Camera camera, float voxelSize,
                                                     out float distanceSq)
        {
            distanceSq = 0f;
            if (!_pendingUpload || !_build.Active) return false;
            distanceSq = ChunkDistanceSq(_build.Coordinate, camera, voxelSize);
            return true;
        }

        /// <summary>
        /// Retires up to <paramref name="maxEvictions"/> of the farthest eligible leases in a single
        /// pass over the entry table.
        ///
        /// Relief used to answer "give me one victim", so freeing N chunks meant N full scans of the
        /// table with a frustum test per entry every time. Under pressure that is the dominant cost
        /// in SchedulerPrepare — the same scan repeated, on every frame, over a table holding
        /// thousands of leases. One pass that selects N victims costs what one old call did.
        ///
        /// With <paramref name="offscreenOnly"/> the pass gives up only geometry outside the frustum,
        /// which is the cheap choice and always the first one tried. Otherwise it retires anything
        /// published that sits farther than <paramref name="minDistanceSq"/>, which is how a fully
        /// on-screen resident set still makes room for the chunk nearest the camera.
        /// </summary>
        internal int EvictFarthest(Camera camera, float voxelSize, bool offscreenOnly,
                                   float minDistanceSq, int maxEvictions)
        {
            if (_entries.Count == 0 || maxEvictions <= 0) return 0;

            int wanted = math.min(maxEvictions, MaxEvictionVictims);
            if (_evictionVictims == null || _evictionVictims.Length < MaxEvictionVictims)
            {
                _evictionVictims = new int3[MaxEvictionVictims];
                _evictionVictimDistances = new float[MaxEvictionVictims];
            }

            int found = 0;
            Vector3 cameraPosition = camera != null ? camera.transform.position : Vector3.zero;
            if (camera != null) GeometryUtility.CalculateFrustumPlanes(camera, _frustumPlanes);

            foreach (var pair in _entries)
            {
                // Keep current replacement geometry alive. Relief may only retire a different,
                // already-published lease.
                if (_build.Active && pair.Key.Equals(_build.Coordinate)) continue;
                if (offscreenOnly)
                {
                    Bounds bounds = ChunkWorldBounds(pair.Key, voxelSize);
                    if (camera != null && GeometryUtility.TestPlanesAABB(_frustumPlanes, bounds))
                        continue;
                }
                else if (!pair.Value.Ready)
                {
                    continue;
                }

                float distance = ChunkDistanceSq(pair.Key, camera, voxelSize);
                if (!offscreenOnly && distance <= minDistanceSq) continue;
                if (found == wanted && distance <= _evictionVictimDistances[found - 1]) continue;

                // Keep the running selection ordered farthest-first; it is at most a few entries.
                int slot = found < wanted ? found++ : wanted - 1;
                while (slot > 0 && _evictionVictimDistances[slot - 1] < distance)
                {
                    _evictionVictimDistances[slot] = _evictionVictimDistances[slot - 1];
                    _evictionVictims[slot] = _evictionVictims[slot - 1];
                    slot--;
                }
                _evictionVictimDistances[slot] = distance;
                _evictionVictims[slot] = pair.Key;
            }

            for (int i = 0; i < found; i++)
            {
                int3 victim = _evictionVictims[i];
                if (_entries.TryGetValue(victim, out Entry entry)) RecycleEntry(entry);
                _entries.Remove(victim);
                MarkDirty(victim);
            }
            return found;
        }

        private const int MaxEvictionVictims = 16;
        private int3[] _evictionVictims;
        private float[] _evictionVictimDistances;

        private float ChunkDistanceSq(int3 coordinate, Camera camera, float voxelSize)
        {
            Vector3 cameraPosition = camera != null ? camera.transform.position : Vector3.zero;
            float chunkMetres = VoxelsPerAxis * voxelSize;
            Vector3 centre = (new Vector3(coordinate.x, coordinate.y, coordinate.z)
                            + Vector3.one * 0.5f) * chunkMetres;
            return (centre - cameraPosition).sqrMagnitude;
        }

        private void EnforceCapacity(Camera camera, float voxelSize)
        {
            if (_entries.Count < MaxResidentChunks || _dirty.Count == 0) return;

            int3 victim = default;
            float farthest = -1f;
            Vector3 cameraPosition = camera != null ? camera.transform.position : Vector3.zero;
            float chunkMetres = VoxelsPerAxis * voxelSize;
            if (camera != null) GeometryUtility.CalculateFrustumPlanes(camera, _frustumPlanes);

            foreach (var pair in _entries)
            {
                // Capacity pressure is also bounded: at most one offscreen lease retires from
                // this workspace per Prepare call. Repeated eviction loops turn a cache miss into
                // a frame spike exactly when streaming is already under pressure.
                if (camera != null && GeometryUtility.TestPlanesAABB(
                        _frustumPlanes, ChunkWorldBounds(pair.Key, voxelSize)))
                    continue;
                Vector3 centre = (new Vector3(pair.Key.x, pair.Key.y, pair.Key.z)
                                + Vector3.one * 0.5f) * chunkMetres;
                float distance = (centre - cameraPosition).sqrMagnitude;
                if (distance <= farthest) continue;
                farthest = distance;
                victim = pair.Key;
            }

            if (farthest < 0f)
            {
                CapacityPressureCount++;
                return;
            }
            if (_entries.TryGetValue(victim, out Entry entry)) RecycleEntry(entry);
            _entries.Remove(victim);
            MarkDirty(victim);
        }

        /// <summary>
        /// Whether a chunk falls in this ring's band.
        ///
        /// <para>The band is an axis-aligned box shell, not a sphere. That is deliberate and
        /// load-bearing: a spherical boundary cuts across chunk faces, so a coarse chunk would
        /// meet a finer neighbour over part of one face and a same-resolution neighbour over
        /// the rest, and a transition cell has nowhere to attach. Snapping the boundary to the
        /// coarse ring's own chunk grid means every LOD change happens exactly on a chunk face,
        /// which is the precondition for stitching it. This is the standard clipmap
        /// arrangement.</para>
        ///
        /// <para>The inner cut tests the chunk's farthest corner and the outer cut its nearest,
        /// so the bands overlap by up to one chunk rather than leaving a gap when the viewer
        /// moves between frames.</para>
        /// </summary>
        private bool WithinRingBand(Bounds bounds, Vector3 cameraPosition)
        {
            Vector3 extents = bounds.extents;
            Vector3 delta = bounds.center - cameraPosition;
            float nearX = Mathf.Max(0f, Mathf.Abs(delta.x) - extents.x);
            float nearY = Mathf.Max(0f, Mathf.Abs(delta.y) - extents.y);
            float nearZ = Mathf.Max(0f, Mathf.Abs(delta.z) - extents.z);
            // Chebyshev distance: the box shell's defining metric.
            float near = Mathf.Max(nearX, Mathf.Max(nearY, nearZ));
            if (near > MaxViewDistanceMetres) return false;
            if (MinViewDistanceMetres <= 0f) return true;

            float far = Mathf.Max(Mathf.Abs(delta.x) + extents.x,
                        Mathf.Max(Mathf.Abs(delta.y) + extents.y,
                                  Mathf.Abs(delta.z) + extents.z));
            return far > MinViewDistanceMetres;
        }

        /// <summary>
        /// Whether the neighbour across <paramref name="face"/> belongs to a finer ring, which
        /// is where this chunk must emit transition geometry. Faces are indexed as
        /// 0=-X, 1=+X, 2=-Y, 3=+Y, 4=-Z, 5=+Z.
        ///
        /// A finer neighbour exists exactly when this chunk sits on the inner edge of the band:
        /// the neighbour in that direction lies wholly inside <see cref="MinViewDistanceMetres"/>
        /// and is therefore owned by the ring one step finer.
        /// </summary>
        public bool FaceNeedsTransition(int3 coordinate, int face, float voxelSize,
                                        Vector3 cameraPosition)
        {
            if (MinViewDistanceMetres <= 0f) return false;

            int axis = face >> 1;
            int direction = (face & 1) == 0 ? -1 : 1;
            int3 neighbour = coordinate;
            neighbour[axis] += direction;

            Bounds neighbourBounds = ChunkWorldBounds(neighbour, voxelSize);
            Vector3 extents = neighbourBounds.extents;
            Vector3 delta = neighbourBounds.center - cameraPosition;
            float far = Mathf.Max(Mathf.Abs(delta.x) + extents.x,
                        Mathf.Max(Mathf.Abs(delta.y) + extents.y,
                                  Mathf.Abs(delta.z) + extents.z));
            // Wholly inside the inner cut means the finer ring owns it outright.
            return far <= MinViewDistanceMetres;
        }

        private Bounds ChunkWorldBounds(int3 coordinate, float voxelSize)
        {
            float size = VoxelsPerAxis * voxelSize;
            Vector3 min = new Vector3(coordinate.x, coordinate.y, coordinate.z) * size;
            return new Bounds(min + Vector3.one * (size * 0.5f),
                              Vector3.one * (size + SourceStep * voxelSize * 2f));
        }

        private bool ScheduledJobsComplete()
        {
            if (_exactMetadataJobScheduled && !_exactMetadataJobHandle.IsCompleted) return false;
            if (_exactClassificationJobScheduled && !_exactClassificationJobHandle.IsCompleted)
                return false;
            if (_hlodJobScheduled && !_hlodJobHandle.IsCompleted) return false;
            if (_densityJobScheduled && !_densityJobHandle.IsCompleted) return false;
            if (_topologyJobScheduled
                && !(_topologyCompactJobScheduled
                    ? _topologyCompactJobHandle.IsCompleted
                    : _topologyJobHandle.IsCompleted)) return false;
            if (_facetedMaskJobScheduled
                && !(_facetedMergeJobScheduled
                    ? _facetedMergeJobHandle.IsCompleted
                    : _facetedMaskJobHandle.IsCompleted)) return false;
            if (_transitionJobScheduled && !_transitionJobHandle.IsCompleted) return false;
            return true;
        }

        /// <summary>
        /// Removes a no-longer-resident chunk only when doing so cannot wait for worker
        /// geometry. If its build is still running, residency pruning defers removal to
        /// a later frame instead of converting eviction pressure into a frame barrier.
        /// </summary>
        private bool TryRemoveChunk(int3 chunk)
        {
            if (_build.Active && _build.Coordinate.Equals(chunk)
                && !ScheduledJobsComplete())
                return false;
            if (_build.Active && _build.Coordinate.Equals(chunk)
                && (_pinnedReadBlocks.Length > 0 || _pinnedRegionCount > 0))
            {
                // All handles were observed complete above. Metadata leases are a fixed <=27 and
                // can release immediately; physical mixed-brick pins drain later under deadline.
                CompleteJobs();
                ReleasePinnedRegionMetadataImmediate();
                _discardBuildAfterPinRelease = true;
                return false;
            }

            _known.Remove(chunk);
            RetireSlot(chunk);
            _queuedResidency.Remove(chunk);
            _dirty.Remove(chunk);
            _queuedDirty.Remove(chunk);
            _queuedVisibleDirty.Remove(chunk);
            _desiredVersions.Remove(chunk);
            _emptyVersions.Remove(chunk);
            _queuedAtSeconds.Remove(chunk);
            if (_entries.TryGetValue(chunk, out Entry entry))
            {
                RecycleEntry(entry);
                _entries.Remove(chunk);
            }
            if (_build.Active && _build.Coordinate.Equals(chunk))
            {
                // Every handle was observed complete above, so these Complete calls only
                // release job safety dependencies; none can stall the frame.
                CompleteJobs();
                _pendingUpload = false;
                _build = default;
                _vertices.Clear();
                _indices.Clear();
                _transitionFace = -1;
                _transitionSampleCursor = 0;
            }
            return true;
        }

        private void CompleteJobs()
        {
            if (_exactMetadataJobScheduled)
            {
                _exactMetadataJobHandle.Complete(); // teardown may synchronize
                _exactMetadataJobScheduled = false;
            }
            if (_exactClassificationJobScheduled)
            {
                _exactClassificationJobHandle.Complete(); // teardown may synchronize
                _exactClassificationJobScheduled = false;
            }
            if (_hlodJobScheduled)
            {
                _hlodJobHandle.Complete(); // teardown may synchronize
                _hlodJobScheduled = false;
            }
            if (_densityJobScheduled)
            {
                _densityJobHandle.Complete();
                _densityJobScheduled = false;
            }
            if (_topologyJobScheduled)
            {
                if (_topologyCompactJobScheduled) _topologyCompactJobHandle.Complete();
                else _topologyJobHandle.Complete();
                _topologyJobScheduled = false;
                _topologyCompactJobScheduled = false;
            }
            if (_facetedMaskJobScheduled)
            {
                if (_facetedMergeJobScheduled) _facetedMergeJobHandle.Complete();
                else _facetedMaskJobHandle.Complete();
                _facetedMaskJobScheduled = false;
                _facetedMergeJobScheduled = false;
            }
            if (_transitionJobScheduled)
            {
                _transitionJobHandle.Complete();
                _transitionJobScheduled = false;
            }
        }

        /// <summary>
        /// Maps a chunk coordinate in this ring's own coordinate space to the region that
        /// contains its origin. Derived from the voxel origin rather than a chunks-per-region
        /// shift, because a coarse ring's chunk can be as large as, or larger than, a region.
        /// </summary>
        private int3 ChunkRegion(int3 chunk)
        {
            int3 originVoxel = chunk * VoxelsPerAxis;
            return new int3(FloorDiv(originVoxel.x, VoxelGrid.RegionVoxelEdge),
                            FloorDiv(originVoxel.y, VoxelGrid.RegionVoxelEdge),
                            FloorDiv(originVoxel.z, VoxelGrid.RegionVoxelEdge));
        }


        private struct RegionSampleCursor
        {
            public bool HasLookup;
            public bool Resident;
            public int3 RegionCoord;
            public RegionReadView View;
        }

        private static bool TryAcquireWorldBlock(IRegionReadSource source,
                                                 ref RegionSampleCursor cursor,
                                                 int3 worldBlock,
                                                 out RegionReadView view)
        {
            int3 regionCoord = worldBlock >> VoxelReadGrid.BlocksPerRegionEdgeLog2;
            return TryAcquireRegion(source, ref cursor, regionCoord, out view);
        }

        private static bool TrySampleWorld(IRegionReadSource source,
                                           ref RegionSampleCursor cursor,
                                           int3 worldVoxel, int level,
                                           out bool occupied, out byte material)
        {
            int3 regionCoord = new(
                FloorDiv(worldVoxel.x, VoxelGrid.RegionVoxelEdge),
                FloorDiv(worldVoxel.y, VoxelGrid.RegionVoxelEdge),
                FloorDiv(worldVoxel.z, VoxelGrid.RegionVoxelEdge));
            if (!TryAcquireRegion(source, ref cursor, regionCoord, out RegionReadView region))
            {
                occupied = false;
                material = VoxelGrid.MaterialEmpty;
                return false;
            }

            int3 localVoxel = worldVoxel - regionCoord * VoxelGrid.RegionVoxelEdge;
            return region.TrySample(localVoxel, level, out occupied, out material);
        }

        private static bool TryAcquireRegion(IRegionReadSource source,
                                             ref RegionSampleCursor cursor,
                                             int3 regionCoord,
                                             out RegionReadView view)
        {
            if (!cursor.HasLookup || math.any(cursor.RegionCoord != regionCoord))
            {
                cursor.RegionCoord = regionCoord;
                cursor.HasLookup = true;
                cursor.Resident = source.TryAcquireRegion(regionCoord, out cursor.View);
            }

            view = cursor.View;
            return cursor.Resident;
        }

        private static int GridIndex(int x, int y, int z) =>
            x + GridSize * (y + GridSize * z);

        private static int FloorDiv(int value, int divisor)
        {
            int quotient = value / divisor;
            int remainder = value % divisor;
            return remainder < 0 ? quotient - 1 : quotient;
        }

        private static int FloorMod(int value, int divisor)
        {
            int result = value % divisor;
            return result < 0 ? result + divisor : result;
        }

        public void Dispose()
        {
            CompleteJobs();
            ReleasePinnedRegionMetadataImmediate();
            ReleasePinnedSnapshotBlocksImmediate();
            foreach (Entry entry in _entries.Values) entry.Dispose();
            _entries.Clear();
            foreach (Entry entry in _entryPool) entry.Dispose();
            _entryPool.Clear();
            _known.Clear();
            _dirty.Clear();
            _desiredVersions.Clear();
            _queuedAtSeconds.Clear();
            _visible.Clear();
            _vertices.Clear();
            _indices.Clear();
            if (_topologyOutput.IsCreated) _topologyOutput.Dispose();
            _workspace.Dispose();
            if (_ownsLookupTables)
            {
                _lookupTables?.Dispose();
                _lookupTables = null;
            }
            _build = default;
            if (_ownsGeometryArena)
            {
                _geometryArena?.Dispose();
                _geometryArena = null;
            }
        }

        private static double ElapsedMs(double startSeconds) => startSeconds <= 0.0
            ? 0.0 : (Time.realtimeSinceStartupAsDouble - startSeconds) * 1000.0;
    }
}
