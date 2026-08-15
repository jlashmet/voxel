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
        private readonly int BrickCacheEdge;
        private readonly int BrickCacheCount;
        private const uint FullyLitOcclusion = 0x0000FF00u;

        private static readonly int s_SurfaceVertices = Shader.PropertyToID("_SurfaceVertices");
        private static readonly int s_SurfaceIndices = Shader.PropertyToID("_SurfaceIndices");
        private static readonly int s_SurfaceIndexBase = Shader.PropertyToID("_SurfaceIndexBase");

        public sealed class Entry : IDisposable
        {
            public readonly int3 Coordinate;
            /// <summary>Voxels this chunk spans per axis — ring-dependent, so bounds and
            /// any consumer's world-space reasoning must use it rather than a constant.</summary>
            public readonly int VoxelsPerAxis;
            /// <summary>Voxels between adjacent samples in the ring that produced this entry.</summary>
            public readonly int SourceStep;
            public ComputeBuffer Vertices;
            public ComputeBuffer Indices;
            public ComputeBuffer Args;
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

            internal Entry(int3 coordinate, int voxelsPerAxis, int sourceStep)
            {
                Coordinate = coordinate;
                VoxelsPerAxis = voxelsPerAxis;
                SourceStep = sourceStep;
            }

            internal void Upload(List<SmoothSurfaceVertex> vertices, List<uint> indices)
            {
                int requiredVertices = math.max(1, vertices.Count);
                int requiredIndices = math.max(1, indices.Count);
                if (Vertices != null && Indices != null && Args != null
                    && VertexCapacity >= requiredVertices && IndexCapacity >= requiredIndices)
                {
                    if (vertices.Count > 0) Vertices.SetData(vertices, 0, 0, vertices.Count);
                    if (indices.Count > 0) Indices.SetData(indices, 0, 0, indices.Count);
                    Args.SetData(new uint[] { (uint)indices.Count, 1u, 0u, 0u });
                    IndexCount = indices.Count;
                    Ready = true;
                    return;
                }
                ComputeBuffer nextVertices = null;
                ComputeBuffer nextIndices = null;
                ComputeBuffer nextArgs = null;

                try
                {
                    int nextVertexCapacity = math.ceilpow2(requiredVertices);
                    int nextIndexCapacity = math.ceilpow2(requiredIndices);
                    nextVertices = new ComputeBuffer(nextVertexCapacity,
                                                     SmoothSurfaceVertex.Stride,
                                                     ComputeBufferType.Structured);
                    nextIndices = new ComputeBuffer(nextIndexCapacity, sizeof(uint),
                                                    ComputeBufferType.Structured);
                    nextArgs = new ComputeBuffer(4, sizeof(uint),
                                                 ComputeBufferType.IndirectArguments);
                    if (vertices.Count > 0) nextVertices.SetData(vertices);
                    if (indices.Count > 0) nextIndices.SetData(indices);
                    nextArgs.SetData(new uint[] { (uint)indices.Count, 1u, 0u, 0u });
                }
                catch
                {
                    nextVertices?.Release();
                    nextIndices?.Release();
                    nextArgs?.Release();
                    throw;
                }

                Vertices?.Release();
                Indices?.Release();
                Args?.Release();
                Vertices = nextVertices;
                Indices = nextIndices;
                Args = nextArgs;
                IndexCount = indices.Count;
                VertexCapacity = nextVertices.count;
                IndexCapacity = nextIndices.count;
                GpuBytes = (long)VertexCapacity * SmoothSurfaceVertex.Stride
                         + (long)IndexCapacity * sizeof(uint) + 4L * sizeof(uint);
                Ready = true;
            }

            public Bounds WorldBounds(float voxelSize)
            {
                float size = VoxelsPerAxis * voxelSize;
                Vector3 min = new Vector3(Coordinate.x, Coordinate.y, Coordinate.z) * size;
                return new Bounds(min + Vector3.one * (size * 0.5f),
                                  Vector3.one * (size + SourceStep * voxelSize * 2f));
            }

            public void Draw(CommandBuffer commandBuffer, Material material,
                             MaterialPropertyBlock properties)
            {
                if (!Ready || IndexCount == 0 || Vertices == null || Indices == null || Args == null)
                    return;

                properties.SetBuffer(s_SurfaceVertices, Vertices);
                properties.SetBuffer(s_SurfaceIndices, Indices);
                properties.SetInt(s_SurfaceIndexBase, 0);
                commandBuffer.DrawProceduralIndirect(Matrix4x4.identity, material, 0,
                    MeshTopology.Triangles, Args, 0, properties);
            }

            public void Dispose()
            {
                Vertices?.Release();
                Indices?.Release();
                Args?.Release();
                Vertices = null;
                Indices = null;
                Args = null;
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
            public int Phase;   // 0 density, 1 continuous cells/decorations, 2 faceted planes
            public int Cursor;
            public ulong SourceVersion;
            public uint MaterialPaletteVersion;
            public uint SurfaceCatalogueVersion;
            public ulong SurfaceCatalogueHash;
            public uint CoatingCatalogueVersion;
            public ulong CoatingCatalogueHash;
            public bool SnapshotTaken;
            public bool HasOwnedSolid;
            public bool RequiresContinuousTopology;
            public double BuildStartSeconds;
            public double DensityScheduledSeconds;
            public double TopologyScheduledSeconds;
            public double FacetedScheduledSeconds;
        }

        private readonly Dictionary<int3, Entry> _entries = new();
        private readonly HashSet<int3> _known = new();
        private readonly HashSet<int3> _dirty = new();
        private readonly Dictionary<int3, ulong> _desiredVersions = new();
        // Chunks whose last completed build produced no geometry, and the source version that
        // proved it. They hold no Entry and no GPU memory, so they cost a dictionary slot
        // rather than a resident chunk, and they stay out of the dirty set until invalidated.
        private readonly Dictionary<int3, ulong> _emptyVersions = new();
        private readonly Dictionary<int3, double> _queuedAtSeconds = new();
        private ulong _versionCounter;
        private readonly List<Entry> _visible = new();
        private readonly Plane[] _frustumPlanes = new Plane[6];

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
        private NativeArray<uint> _facetedMasks;
        private NativeList<SmoothSurfaceVertex> _facetedVertices;
        private NativeList<uint> _facetedIndices;

        private readonly float[] _cellDensity = new float[8];
        private readonly byte[] _cellMaterial = new byte[8];
        private readonly uint[] _cellSurface = new uint[8];
        private readonly byte[] _cellBoundary = new byte[8];
        private readonly SmoothSurfaceVertex[] _cellVertices = new SmoothSurfaceVertex[16];
        private readonly uint[] _faceMask = new uint[CellsPerAxis * CellsPerAxis];
        private readonly List<SmoothSurfaceVertex> _vertices = new(16_384);
        private readonly List<uint> _indices = new(24_576);
        private BuildState _build;
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
        {
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
            _density = new NativeArray<float>(GridSampleCount, Allocator.Persistent,
                                              NativeArrayOptions.UninitializedMemory);
            _materials = new NativeArray<byte>(GridSampleCount, Allocator.Persistent,
                                               NativeArrayOptions.UninitializedMemory);
            _surfaceSemantics = new NativeArray<uint>(GridSampleCount, Allocator.Persistent,
                                                      NativeArrayOptions.UninitializedMemory);
            _boundarySamples = new NativeArray<byte>(GridSampleCount, Allocator.Persistent,
                                                     NativeArrayOptions.UninitializedMemory);
            if (SamplesFromMips)
            {
                _mipSampleOccupancy = new NativeArray<byte>(
                    GridSampleCount, Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory);
                _mipSampleMaterials = new NativeArray<byte>(
                    GridSampleCount, Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory);
            }
            else
            {
                _densityBricks = new NativeArray<TransvoxelDensityBrick>(
                    BrickCacheCount, Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory);
            }
            _densityMixedVoxels = new NativeList<byte>(64 * 1024, Allocator.Persistent);
            _densityMixedSurfaceSemantics = new NativeList<ushort>(64 * 1024, Allocator.Persistent);
            _densityMixedBoundarySamples = new NativeList<byte>(64 * 1024, Allocator.Persistent);
            _compactedTopologyVertices = new NativeList<SmoothSurfaceVertex>(16_384,
                                                                              Allocator.Persistent);
            _compactedTopologyIndices = new NativeList<uint>(24_576, Allocator.Persistent);
            _topologyOverflowCell = new NativeArray<int>(1, Allocator.Persistent);
            int cellCount = CellsPerAxis * CellsPerAxis * CellsPerAxis;
            _topologyCellClass = new NativeArray<byte>(256, Allocator.Persistent);
            _topologyGeometryCounts = new NativeArray<byte>(
                TransvoxelRegularTables.CellData.Length, Allocator.Persistent);
            _topologyCellVertexIndices = new NativeArray<byte>(
                TransvoxelRegularTables.CellData.Length *
                TransvoxelTopologyJob.MaxIndicesPerCell, Allocator.Persistent);
            _topologyEdgeCodes = new NativeArray<ushort>(256 * 12, Allocator.Persistent);
            _facetedMasks = new NativeArray<uint>(
                6 * CellsPerAxis * CellsPerAxis * CellsPerAxis, Allocator.Persistent);
            _facetedVertices = new NativeList<SmoothSurfaceVertex>(16_384, Allocator.Persistent);
            _facetedIndices = new NativeList<uint>(24_576, Allocator.Persistent);
            InitialiseTopologyTables();
            InitialiseTransitionTables();
        }

        /// <summary>
        /// Flattens the jagged transition tables into Burst-friendly arrays. The jagged form is
        /// how the data is published; jobs need fixed strides.
        /// </summary>
        private void InitialiseTransitionTables()
        {
            byte[] cellClass = TransvoxelTransitionTables.CellClass;
            RegularCellData[] cellData = TransvoxelTransitionTables.CellData;
            ushort[][] vertexData = TransvoxelTransitionTables.VertexData;

            _transitionVertexStride = 0;
            for (int i = 0; i < vertexData.Length; i++)
                _transitionVertexStride = math.max(_transitionVertexStride, vertexData[i].Length);
            _transitionIndexStride = 0;
            for (int i = 0; i < cellData.Length; i++)
                _transitionIndexStride = math.max(_transitionIndexStride,
                                                  cellData[i].VertexIndices.Length);

            _transitionCellClass = new NativeArray<byte>(cellClass.Length, Allocator.Persistent);
            for (int i = 0; i < cellClass.Length; i++) _transitionCellClass[i] = cellClass[i];

            _transitionGeometryCounts = new NativeArray<byte>(cellData.Length,
                                                              Allocator.Persistent);
            _transitionCellIndices = new NativeArray<byte>(
                cellData.Length * math.max(1, _transitionIndexStride), Allocator.Persistent);
            for (int i = 0; i < cellData.Length; i++)
            {
                _transitionGeometryCounts[i] = cellData[i].GeometryCounts;
                byte[] indices = cellData[i].VertexIndices;
                for (int j = 0; j < indices.Length; j++)
                    _transitionCellIndices[i * _transitionIndexStride + j] = indices[j];
            }

            _transitionVertexData = new NativeArray<ushort>(
                vertexData.Length * math.max(1, _transitionVertexStride), Allocator.Persistent);
            for (int i = 0; i < vertexData.Length; i++)
            {
                ushort[] row = vertexData[i];
                for (int j = 0; j < row.Length; j++)
                    _transitionVertexData[i * _transitionVertexStride + j] = row[j];
            }

            int faceSamples = FaceSamplesPerAxis * FaceSamplesPerAxis;
            _faceDensity = new NativeArray<float>(faceSamples, Allocator.Persistent);
            _faceMaterials = new NativeArray<byte>(faceSamples, Allocator.Persistent);
            _faceSurfaces = new NativeArray<uint>(faceSamples, Allocator.Persistent);
            _transitionVertices = new NativeList<SmoothSurfaceVertex>(2048, Allocator.Persistent);
            _transitionIndices = new NativeList<uint>(3072, Allocator.Persistent);
        }

        /// <summary>
        /// Stitches every face of the in-flight chunk that borders a finer ring.
        ///
        /// Runs after the regular cells are appended, so transition geometry is added to the
        /// same vertex and index lists and ships in one mesh. A chunk with no finer neighbour —
        /// the common case, and every chunk in the innermost ring — does no work here.
        /// </summary>
        private void AppendTransitionFaces(IRegionReadSource source,
                                           in MaterialPaletteView palette,
                                           Camera camera, float voxelSize)
        {
            if (MinViewDistanceMetres <= 0f || camera == null) return;

            Vector3 cameraPosition = camera.transform.position;
            for (int face = 0; face < 6; face++)
            {
                if (!FaceNeedsTransition(_build.Coordinate, face, voxelSize, cameraPosition))
                    continue;

                SnapshotTransitionFace(source, in palette, face);

                _transitionVertices.Clear();
                _transitionIndices.Clear();
                new TransitionMeshJob
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
                }.Run();

                uint vertexBase = (uint)_vertices.Count;
                NativeArray<SmoothSurfaceVertex> vertices = _transitionVertices.AsArray();
                for (int i = 0; i < vertices.Length; i++) _vertices.Add(vertices[i]);
                NativeArray<uint> indices = _transitionIndices.AsArray();
                for (int i = 0; i < indices.Length; i++) _indices.Add(vertexBase + indices[i]);
            }
        }

        /// <summary>
        /// Samples one chunk face at half this ring's stride — the finer neighbour's spacing.
        ///
        /// The chunk lattice cannot supply these: it is sampled at the ring's own stride and
        /// simply does not contain the intermediate positions. Reading them from the same
        /// authoritative source the finer ring reads is what makes the two sides agree on where
        /// the surface crosses, which is the whole mechanism by which the seam closes.
        /// </summary>
        private void SnapshotTransitionFace(IRegionReadSource source,
                                            in MaterialPaletteView palette, int face)
        {
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
            // Half a ring stride is one level finer than the ring itself; for the finest ring
            // that reaches actual voxels, which LevelForStride reports as a negative level.
            int mipLevel = VoxelReadGrid.LevelForStride(halfStep);
            RegionSampleCursor cursor = default;

            for (int v = 0; v < FaceSamplesPerAxis; v++)
            for (int u = 0; u < FaceSamplesPerAxis; u++)
            {
                int3 voxel = faceOrigin + uAxis * (u * halfStep) + vAxis * (v * halfStep);
                bool occupied = false;
                byte material = VoxelGrid.MaterialEmpty;
                if (TrySampleWorld(source, ref cursor, voxel, mipLevel,
                                   out bool sampled, out byte sampledMaterial))
                {
                    occupied = sampled;
                    material = sampledMaterial;
                }

                int index = u + FaceSamplesPerAxis * v;
                _faceDensity[index] = occupied ? 0.5f : -0.5f;
                _faceMaterials[index] = material;
                _faceSurfaces[index] = occupied
                    ? palette.GetDefaultSurfaceStyle(material) : 0u;
            }
        }

        private void InitialiseTopologyTables()
        {
            _topologyCellClass.CopyFrom(TransvoxelRegularTables.CellClass);
            for (int cellClass = 0; cellClass < TransvoxelRegularTables.CellData.Length;
                 cellClass++)
            {
                RegularCellData data = TransvoxelRegularTables.CellData[cellClass];
                _topologyGeometryCounts[cellClass] = data.GeometryCounts;
                int length = math.min(data.VertexIndices.Length,
                                      TransvoxelTopologyJob.MaxIndicesPerCell);
                for (int i = 0; i < length; i++)
                    _topologyCellVertexIndices[
                        cellClass * TransvoxelTopologyJob.MaxIndicesPerCell + i] =
                        data.VertexIndices[i];
            }
            for (int cell = 0; cell < TransvoxelRegularTables.VertexData.Length; cell++)
            {
                ushort[] edges = TransvoxelRegularTables.VertexData[cell];
                int length = math.min(edges.Length, 12);
                for (int i = 0; i < length; i++)
                    _topologyEdgeCodes[cell * 12 + i] = edges[i];
            }
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
        public int DirtyCount => _dirty.Count + (_build.Active ? 1 : 0);
        public ulong ActiveSurfaceCatalogueHash => _surfaceCatalogue.CatalogueHash;
        public ulong CompletedBuildCount { get; private set; }
        public ulong StaleBuildCount { get; private set; }
        public ulong UploadedGeometryBytes { get; private set; }
        public ulong CompletedDecorationClumps { get; private set; }
        public int MissingVisibleCount { get; private set; }
        public ulong CapacityPressureCount { get; private set; }
        public int RunningJobCount => _densityJobScheduled || _topologyJobScheduled
                                   || _facetedMaskJobScheduled ? 1 : 0;
        public int PendingUploadCount => _build.Active && _build.Phase >= 2 ? 1 : 0;
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
        /// Discovers/invalidates chunks from the scheduler's authoritative surface-brick stream.
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
                    if (!OwnsShard(chunk)) continue;
                    _known.Add(chunk);
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
            if (dirtyRegions == null || dirtyRegions.Count == 0 || _known.Count == 0) return;

            List<int3> affected = null;
            foreach (int3 chunk in _known)
            {
                foreach (int3 dirtyRegion in dirtyRegions)
                {
                    if (!ChunkOverlapsRegion(chunk, dirtyRegion, VoxelsPerAxis, SourceStep))
                        continue;
                    (affected ??= new List<int3>()).Add(chunk);
                    break;
                }
            }

            if (affected == null) return;
            for (int i = 0; i < affected.Count; i++) Invalidate(affected[i]);
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
            DropNoLongerResident(source);
            _residencyPruneTiming.Add(ElapsedMs(sectionStart));
            sectionStart = Time.realtimeSinceStartupAsDouble;
            EnforceCapacity(camera, voxelSize);
            _capacityTiming.Add(ElapsedMs(sectionStart));

            if (camera == null || _dirty.Count == 0 && !_build.Active) return;

            if (_build.Active && _build.SnapshotTaken
                && _build.MaterialPaletteVersion != palette.Version
                && (!_desiredVersions.TryGetValue(_build.Coordinate, out ulong desired)
                    || desired <= _build.SourceVersion))
                Invalidate(_build.Coordinate);

            double deadline = Time.realtimeSinceStartupAsDouble
                            + math.max(0.0, budgetMs) * 0.001;
            do
            {
                if (!_build.Active)
                {
                    double selectionStart = Time.realtimeSinceStartupAsDouble;
                    bool selected = BeginNearestBuild(camera, voxelSize);
                    _buildSelectionTiming.Add(ElapsedMs(selectionStart));
                    if (!selected) break;
                }

                if (_build.Phase == 0)
                {
                    if (!_densityJobScheduled)
                        ScheduleDensityJob(source, in palette, voxelSize);

                    // Border invalidation intentionally discovers halo chunks. If the immutable
                    // snapshot proves this chunk owns no solid cells, publish a complete empty
                    // result without scanning/merging all 64^3 cells. Profile blocks still run
                    // because their authored geometry may overlap an otherwise empty core.
                    if (!_build.HasOwnedSolid && _buildProfileBlocks.Length == 0)
                    {
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
                    _topologyCompactJobHandle.Complete();
                    _facetedMergeJobHandle.Complete();
                    _densityTurnaroundTiming.Add(ElapsedMs(_build.DensityScheduledSeconds));
                    _topologyTurnaroundTiming.Add(ElapsedMs(_build.TopologyScheduledSeconds));
                    _facetedTurnaroundTiming.Add(ElapsedMs(_build.FacetedScheduledSeconds));
                    _densityJobScheduled = false;
                    _topologyJobScheduled = false;
                    _topologyCompactJobScheduled = false;
                    _facetedMaskJobScheduled = false;
                    _facetedMergeJobScheduled = false;
                    CompactTopology(voxelSize);
                    AppendFacetedTopology();
                    _build.Phase = 3;
                    _build.Cursor = 0;
                    continue;
                }

                if (_build.Phase == 2)
                {
                    if (!_facetedMaskJobScheduled) ScheduleFacetedMaskJob();
                    if (!_facetedMergeJobScheduled) ScheduleFacetedMergeJob(voxelSize);
                    if (!_facetedMergeJobHandle.IsCompleted) break;
                    _facetedMergeJobHandle.Complete();
                    _facetedTurnaroundTiming.Add(ElapsedMs(_build.FacetedScheduledSeconds));
                    _facetedMaskJobScheduled = false;
                    _facetedMergeJobScheduled = false;
                    AppendFacetedTopology();
                    _build.Phase = 3;
                    _build.Cursor = 0;
                    continue;
                }

                double profileStart = Time.realtimeSinceStartupAsDouble;
                bool profilesDone;
                using (s_ProfileMarker.Auto()) profilesDone = StepProfileBlocks(voxelSize);
                _profileEmitTiming.Add(ElapsedMs(profileStart));
                if (profilesDone)
                {
                    AppendTransitionFaces(source, in palette, camera, voxelSize);
                    FinishBuild(frame);
                }
            }
            while (Time.realtimeSinceStartupAsDouble < deadline);
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

        private void CompactTopology(float voxelSize)
        {
            double compactStart = Time.realtimeSinceStartupAsDouble;
            using var compactScope = s_CompactMarker.Auto();
            int overflowCell = _topologyOverflowCell[0];
            if (overflowCell >= 0)
                throw new InvalidOperationException(
                    $"Continuous topology output overflow in chunk {_build.Coordinate}, " +
                    $"cell {overflowCell}; refusing to publish partial geometry.");
            NativeArray<SmoothSurfaceVertex> vertices = _compactedTopologyVertices.AsArray();
            for (int i = 0; i < vertices.Length; i++) _vertices.Add(vertices[i]);
            NativeArray<uint> indices = _compactedTopologyIndices.AsArray();
            for (int i = 0; i < indices.Length; i++) _indices.Add(indices[i]);
            _topologyOutput.Dispose();
            _topologyOutput = default;
            // Decorative coating clumps are intentionally not part of base-surface admission.
            // Blocking complete castle/terrain geometry on optional moss relief produced visible
            // holes and multi-millisecond compaction stalls. Coating colour remains present in
            // every base vertex; relief is a separately schedulable decoration domain.
            LastTopologyCompactMs =
                (Time.realtimeSinceStartupAsDouble - compactStart) * 1000.0;
            _topologyCompactTiming.Add(LastTopologyCompactMs);
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
                MixedVoxels = _densityMixedVoxels.AsArray(),
                MixedSurfaceSemantics = _densityMixedSurfaceSemantics.AsArray(),
                MixedBoundarySamples = _densityMixedBoundarySamples.AsArray(),
                Palette = _buildPalette,
                Catalogue = _buildSurfaceCatalogue,
                Coatings = _buildCoatingCatalogue,
                ChunkOriginVoxel = chunkOrigin,
                BrickCacheOrigin = chunkBrickOrigin - BrickCachePadding,
                BrickCacheEdge = BrickCacheEdge,
                CellsPerAxis = CellsPerAxis,
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
                VoxelSize = voxelSize,
            };
            _facetedMergeJobHandle = job.Schedule(_facetedMaskJobHandle);
            _facetedMergeJobScheduled = true;
        }

        private void AppendFacetedTopology()
        {
            double start = Time.realtimeSinceStartupAsDouble;
            using var scope = s_FacetedMergeMarker.Auto();
            uint vertexBase = (uint)_vertices.Count;
            NativeArray<SmoothSurfaceVertex> vertices = _facetedVertices.AsArray();
            for (int i = 0; i < vertices.Length; i++) _vertices.Add(vertices[i]);
            NativeArray<uint> indices = _facetedIndices.AsArray();
            for (int i = 0; i < indices.Length; i++) _indices.Add(vertexBase + indices[i]);
            _facetedMergeTiming.Add(ElapsedMs(start));
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

        public IReadOnlyList<Entry> CollectVisible(Camera camera, float voxelSize, int frame)
        {
            _visible.Clear();
            MissingVisibleCount = 0;
            if (camera == null) return _visible;

            GeometryUtility.CalculateFrustumPlanes(camera, _frustumPlanes);
            Vector3 cameraPosition = camera.transform.position;
            foreach (int3 coordinate in _known)
            {
                Bounds bounds = ChunkWorldBounds(coordinate, voxelSize);
                if (!WithinRingBand(bounds, cameraPosition)) continue;
                if (!GeometryUtility.TestPlanesAABB(_frustumPlanes, bounds))
                    continue;
                if (!_entries.TryGetValue(coordinate, out Entry entry) || !entry.Ready)
                {
                    // A known-empty chunk is a completed build with nothing to draw, not a
                    // hole waiting on work. Counting it as missing would keep the metric
                    // permanently alarmed across the mostly-air volume of any view sphere.
                    if (!_emptyVersions.ContainsKey(coordinate)) MissingVisibleCount++;
                    continue;
                }
                // A ready zero-index entry is a complete, intentionally empty result.
                if (entry.IndexCount == 0) continue;
                entry.LastUsedFrame = frame;
                _visible.Add(entry);
            }
            return _visible;
        }

        private bool BeginNearestBuild(Camera camera, float voxelSize)
        {
            if (_dirty.Count == 0) return false;

            int3 best = default;
            float bestScore = float.PositiveInfinity;
            float chunkMetres = VoxelsPerAxis * voxelSize;
            Vector3 cameraWorldPosition = camera.transform.position;
            GeometryUtility.CalculateFrustumPlanes(camera, _frustumPlanes);
            foreach (int3 candidate in _dirty)
            {
                Bounds bounds = ChunkWorldBounds(candidate, voxelSize);
                if (!WithinRingBand(bounds, cameraWorldPosition)) continue;
                Vector3 centre = (new Vector3(candidate.x, candidate.y, candidate.z)
                                + Vector3.one * 0.5f) * chunkMetres;
                float distance = (centre - cameraWorldPosition).sqrMagnitude;
                // Every visible candidate ranks ahead of every off-screen candidate. Distance
                // then fills the centre of the view before its edges.
                float score = GeometryUtility.TestPlanesAABB(_frustumPlanes, bounds)
                    ? distance : distance + 1_000_000_000f;
                if (score >= bestScore) continue;
                bestScore = score;
                best = candidate;
            }

            if (float.IsPositiveInfinity(bestScore)) return false;
            _dirty.Remove(best);
            _vertices.Clear();
            _indices.Clear();
            _build = new BuildState
            {
                Active = true, Coordinate = best, Phase = 0, Cursor = 0,
                SourceVersion = _desiredVersions.TryGetValue(best, out ulong version) ? version : 0,
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
            if (!_dirty.Contains(chunk) && (!_build.Active || !_build.Coordinate.Equals(chunk)))
                _queuedAtSeconds[chunk] = Time.realtimeSinceStartupAsDouble;
            _dirty.Add(chunk);
        }

        private bool OwnsShard(int3 chunk)
        {
            int count = math.max(1, ShardCount);
            uint hash = math.hash(chunk);
            return (int)(hash % (uint)count) == math.clamp(ShardIndex, 0, count - 1);
        }

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
            foreach (int3 chunk in _known) Invalidate(chunk);
        }

        private void SetMaterialPaletteVersion(uint version)
        {
            if (_materialPaletteVersion == version) return;
            _materialPaletteVersion = version;
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

        /// <summary>
        /// Snapshots one mip cell per lattice sample and schedules the coarse-ring density job.
        ///
        /// A coarse chunk spans far too much world to cache its bricks, but it only ever reads
        /// GridSize³ samples, so the snapshot is sized by the lattice rather than by the world
        /// extent. That is what keeps the outermost rings affordable no matter how much terrain
        /// they cover. Samples whose region is absent read as empty, which leaves a hole rather
        /// than inventing geometry the server never sent.
        /// </summary>
        private void ScheduleMipDensityJob(IRegionReadSource source,
                                           in MaterialPaletteView palette, float voxelSize)
        {
            double snapshotStart = Time.realtimeSinceStartupAsDouble;
            using var snapshotScope = s_SnapshotMarker.Auto();

            int3 chunkOriginVoxel = _build.Coordinate * VoxelsPerAxis;
            int mipLevel = VoxelReadGrid.LevelForStride(SourceStep);
            RegionSampleCursor cursor = default;
            bool anySolid = false;

            for (int gz = 0; gz < GridSize; gz++)
            for (int gy = 0; gy < GridSize; gy++)
            for (int gx = 0; gx < GridSize; gx++)
            {
                int index = GridIndex(gx, gy, gz);
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
            }

            _buildSurfaceCatalogue = _surfaceCatalogue;
            _buildCoatingCatalogue = _coatingCatalogue;
            _buildPalette = palette;
            _build.MaterialPaletteVersion = palette.Version;
            _build.SnapshotTaken = true;
            _buildProfileBlocks = Array.Empty<ProfileBlock>();
            _build.HasOwnedSolid = anySolid;

            LastSnapshotMs = (Time.realtimeSinceStartupAsDouble - snapshotStart) * 1000.0;
            _snapshotTiming.Add(LastSnapshotMs);
            if (!anySolid) return;

            // Coarse rings always take the continuous path. Authored planar/cubic styles
            // describe centimetre detail; at these strides a cell spans metres, and faceting
            // it would produce a staircase silhouette across the whole far field.
            _build.RequiresContinuousTopology = true;

            var job = new MipDensityJob
            {
                SampleOccupancy = _mipSampleOccupancy,
                SampleMaterials = _mipSampleMaterials,
                Palette = palette,
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
        }

        /// <summary>
        /// Resolves the padded brick neighbourhood around the chunk once and copies only mixed
        /// voxel payloads. This snapshot is immutable until the Burst density job completes, so
        /// gameplay may continue editing/evicting authoritative storage without racing the job.
        /// </summary>
        private void ScheduleDensityJob(IRegionReadSource source,
                                        in MaterialPaletteView palette, float voxelSize)
        {
            if (SamplesFromMips)
            {
                ScheduleMipDensityJob(source, in palette, voxelSize);
                return;
            }

            double snapshotStart = Time.realtimeSinceStartupAsDouble;
            using var snapshotScope = s_SnapshotMarker.Auto();
            _densityMixedVoxels.Clear();
            _densityMixedSurfaceSemantics.Clear();
            _densityMixedBoundarySamples.Clear();

            int3 chunkOriginVoxel = _build.Coordinate * VoxelsPerAxis;
            int3 chunkBrickOrigin = new(chunkOriginVoxel.x >> VoxelReadGrid.BlockEdgeLog2,
                                        chunkOriginVoxel.y >> VoxelReadGrid.BlockEdgeLog2,
                                        chunkOriginVoxel.z >> VoxelReadGrid.BlockEdgeLog2);
            int3 cacheOrigin = chunkBrickOrigin - BrickCachePadding;
            RegionSampleCursor cursor = default;

            for (int z = 0; z < BrickCacheEdge; z++)
            for (int y = 0; y < BrickCacheEdge; y++)
            for (int x = 0; x < BrickCacheEdge; x++)
            {
                int cacheIndex = x + BrickCacheEdge * (y + BrickCacheEdge * z);
                int3 worldBrick = cacheOrigin + new int3(x, y, z);
                _densityBricks[cacheIndex] = SnapshotBlock(source, ref cursor, worldBrick);
            }

            _buildSurfaceCatalogue = _surfaceCatalogue;
            _buildCoatingCatalogue = _coatingCatalogue;
            var job = new TransvoxelDensityJob
            {
                Bricks = _densityBricks,
                MixedVoxels = _densityMixedVoxels.AsArray(),
                MixedSurfaceSemantics = _densityMixedSurfaceSemantics.AsArray(),
                MixedBoundarySamples = _densityMixedBoundarySamples.AsArray(),
                Palette = palette,
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

            _build.MaterialPaletteVersion = palette.Version;
            _build.SnapshotTaken = true;
            _buildPalette = palette;
            _buildProfileBlocks = _profileBlocksByChunk.TryGetValue(
                _build.Coordinate, out ProfileBlock[] blocks)
                ? blocks : Array.Empty<ProfileBlock>();
            _build.HasOwnedSolid = SnapshotCoreHasSolid();
            if (!_build.HasOwnedSolid && _buildProfileBlocks.Length == 0)
            {
                LastSnapshotMs = (Time.realtimeSinceStartupAsDouble - snapshotStart) * 1000.0;
                _snapshotTiming.Add(LastSnapshotMs);
                return;
            }

            _build.RequiresContinuousTopology = _buildProfileBlocks.Length > 0;
            for (int i = 0; i < _densityBricks.Length
                            && !_build.RequiresContinuousTopology; i++)
            {
                TransvoxelDensityBrick brick = _densityBricks[i];
                if (brick.Kind != 1 || !IsSolidSurfaceMaterial(brick.UniformMaterial)) continue;
                SurfaceStyleReadDefinition style = _buildSurfaceCatalogue.Get(
                    palette.GetDefaultSurfaceStyle(brick.UniformMaterial));
                _build.RequiresContinuousTopology =
                    style.Reconstruction == SurfaceReconstruction.Smooth
                    || style.Reconstruction == SurfaceReconstruction.Rounded;
            }
            for (int i = 0; i < _densityMixedVoxels.Length
                            && !_build.RequiresContinuousTopology; i++)
            {
                byte material = _densityMixedVoxels[i];
                if (!IsSolidSurfaceMaterial(material)) continue;
                uint surface = VoxelSurfaceSemantics.FromStorage(
                    _densityMixedSurfaceSemantics[i]).Packed;
                ushort styleId = (ushort)surface;
                if (styleId == SurfaceStyles.MaterialDefault)
                    styleId = palette.GetDefaultSurfaceStyle(material);
                SurfaceStyleReadDefinition style = _buildSurfaceCatalogue.Get(styleId);
                byte coating = (byte)(surface >> 16);
                _build.RequiresContinuousTopology = _densityMixedBoundarySamples[i] != 0
                    || _buildCoatingCatalogue.Get(coating).Displacement != 0
                    || style.Reconstruction == SurfaceReconstruction.Smooth
                    || style.Reconstruction == SurfaceReconstruction.Rounded;
            }

            if (_build.RequiresContinuousTopology)
            {
                _build.DensityScheduledSeconds = Time.realtimeSinceStartupAsDouble;
                _densityJobHandle = job.Schedule(GridSampleCount, 64);
                _densityJobScheduled = true;
                ScheduleTopologyJob(voxelSize, _densityJobHandle);
                ScheduleFacetedMaskJob(_densityJobHandle);
                ScheduleFacetedMergeJob(voxelSize);
            }
            LastSnapshotMs = (Time.realtimeSinceStartupAsDouble - snapshotStart) * 1000.0;
            _snapshotTiming.Add(LastSnapshotMs);
        }

        private bool SnapshotCoreHasSolid()
        {
            int first = BrickCachePadding;
            int end = first + BricksPerAxis;
            for (int z = first; z < end; z++)
            for (int y = first; y < end; y++)
            for (int x = first; x < end; x++)
            {
                int index = x + BrickCacheEdge * (y + BrickCacheEdge * z);
                TransvoxelDensityBrick brick = _densityBricks[index];
                if (brick.Kind == 0) continue;
                if (brick.Kind == 1)
                {
                    if (IsSolidSurfaceMaterial(brick.UniformMaterial)) return true;
                    continue;
                }

                int endVoxel = brick.MixedOffset + VoxelReadGrid.VoxelsPerBlock;
                for (int voxel = brick.MixedOffset; voxel < endVoxel; voxel++)
                    if (IsSolidSurfaceMaterial(_densityMixedVoxels[voxel])) return true;
            }
            return false;
        }

        private TransvoxelDensityBrick SnapshotBlock(IRegionReadSource source,
                                                      ref RegionSampleCursor cursor,
                                                      int3 worldBlock)
        {
            if (!TryAcquireWorldBlock(source, ref cursor, worldBlock, out RegionReadView region)
                || !region.TryGetWorldBlock(worldBlock, out VoxelReadBlock block)
                || block.Kind == VoxelReadBlockKind.Empty)
                return default;

            if (block.Kind == VoxelReadBlockKind.Uniform)
            {
                return new TransvoxelDensityBrick
                {
                    Kind = 1,
                    UniformMaterial = block.UniformMaterial,
                    MixedOffset = 0
                };
            }

            int mixedOffset = _densityMixedVoxels.Length;
            int nextLength = mixedOffset + VoxelReadGrid.VoxelsPerBlock;
            _densityMixedVoxels.ResizeUninitialized(nextLength);
            _densityMixedSurfaceSemantics.ResizeUninitialized(nextLength);
            _densityMixedBoundarySamples.ResizeUninitialized(nextLength);
            if (!region.TryCopyWorldBlock(
                    worldBlock,
                    _densityMixedVoxels.AsArray(),
                    _densityMixedSurfaceSemantics.AsArray(),
                    _densityMixedBoundarySamples.AsArray(),
                    mixedOffset))
                throw new InvalidOperationException($"Failed to snapshot Storage read block {worldBlock}.");

            return new TransvoxelDensityBrick
            {
                Kind = 2,
                UniformMaterial = 0,
                MixedOffset = mixedOffset
            };
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
                    uint triangleBase = (uint)_vertices.Count;
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
                uint baseVertex = (uint)_vertices.Count;
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
            uint baseVertex = (uint)_vertices.Count;
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
            uint baseVertex = (uint)_vertices.Count;
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
                int3 voxel = chunkOrigin + local;
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
                neighbour[axis] += sign;
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
            p0[axis] += layer + (sign > 0 ? 1 : 0);
            p0[axisA] += a;
            p0[axisB] += b;
            float3 p1 = p0;
            float3 p2 = p0;
            float3 p3 = p0;
            p1[axisA] += width;
            p2[axisA] += width;
            p2[axisB] += height;
            p3[axisB] += height;
            p0 *= voxelSize;
            p1 *= voxelSize;
            p2 *= voxelSize;
            p3 *= voxelSize;
            float3 normal = float3.zero;
            normal[axis] = sign;
            uint baseVertex = (uint)_vertices.Count;
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
            material = _densityMixedVoxels[brick.MixedOffset + voxelIndex];
            surface = VoxelSurfaceSemantics.FromStorage(
                _densityMixedSurfaceSemantics[brick.MixedOffset + voxelIndex]).Packed;
            boundary = _densityMixedBoundarySamples[brick.MixedOffset + voxelIndex];
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

        private void FinishBuild(int frame)
        {
            if (_desiredVersions.TryGetValue(_build.Coordinate, out ulong desired)
                && desired > _build.SourceVersion)
            {
                // Input changed while the immutable snapshot/job was in flight. Never publish
                // stale geometry; the newer invalidation remains queued.
                StaleBuildCount++;
                _build = default;
                _vertices.Clear();
                _indices.Clear();
                return;
            }

            // An empty result is a complete answer, but it owns no geometry, so it must not
            // hold a resident slot. Air dominates any view sphere; letting it consume capacity
            // 1:1 with geometry is what drove real surfaces out of the cache and produced the
            // evict/rebuild churn. Record it as known-empty and reclaim the slot instead.
            if (_indices.Count == 0)
            {
                if (_entries.TryGetValue(_build.Coordinate, out Entry stale))
                {
                    stale.Dispose();
                    _entries.Remove(_build.Coordinate);
                }
                _emptyVersions[_build.Coordinate] = _build.SourceVersion;
                CompletedBuildCount++;
                _buildLatencyTiming.Add(ElapsedMs(_build.BuildStartSeconds));
                _desiredVersions.Remove(_build.Coordinate);
                _queuedAtSeconds.Remove(_build.Coordinate);
                _build = default;
                _vertices.Clear();
                _indices.Clear();
                return;
            }

            _emptyVersions.Remove(_build.Coordinate);
            if (!_entries.TryGetValue(_build.Coordinate, out Entry entry))
            {
                entry = new Entry(_build.Coordinate, VoxelsPerAxis, SourceStep);
                _entries.Add(_build.Coordinate, entry);
            }

            double uploadStart = Time.realtimeSinceStartupAsDouble;
            using (s_UploadMarker.Auto()) entry.Upload(_vertices, _indices);
            LastUploadMs = (Time.realtimeSinceStartupAsDouble - uploadStart) * 1000.0;
            _uploadTiming.Add(LastUploadMs);
            _buildLatencyTiming.Add(ElapsedMs(_build.BuildStartSeconds));
            CompletedBuildCount++;
            UploadedGeometryBytes += (ulong)entry.GpuBytes;
            entry.LastUsedFrame = frame;
            entry.SourceVersion = _build.SourceVersion;
            entry.MaterialPaletteVersion = _build.MaterialPaletteVersion;
            entry.SurfaceCatalogueVersion = _build.SurfaceCatalogueVersion;
            entry.SurfaceCatalogueHash = _build.SurfaceCatalogueHash;
            entry.CoatingCatalogueVersion = _build.CoatingCatalogueVersion;
            entry.CoatingCatalogueHash = _build.CoatingCatalogueHash;
            _desiredVersions.Remove(_build.Coordinate);
            _queuedAtSeconds.Remove(_build.Coordinate);
            _build = default;
            _vertices.Clear();
            _indices.Clear();
        }

        private void DropNoLongerResident(IRegionReadSource source)
        {
            if (_known.Count == 0) return;
            List<int3> gone = null;
            foreach (int3 chunk in _known)
            {
                if (AnyOverlappedRegionResident(source, chunk)) continue;
                (gone ??= new List<int3>()).Add(chunk);
            }

            if (gone == null) return;
            for (int i = 0; i < gone.Count; i++) RemoveChunk(gone[i]);
        }

        /// <summary>
        /// Whether any region the chunk overlaps is still resident. A coarse ring's chunk can be
        /// larger than a region and straddle several, so testing only the origin region would
        /// discard a chunk that still has most of its data — and, worse, keep one whose origin
        /// happens to survive while the rest of it has gone.
        /// </summary>
        private bool AnyOverlappedRegionResident(IRegionReadSource source, int3 chunk)
        {
            int3 minVoxel = chunk * VoxelsPerAxis;
            int3 maxVoxel = minVoxel + (VoxelsPerAxis - 1);
            int3 minRegion = new(FloorDiv(minVoxel.x, VoxelGrid.RegionVoxelEdge),
                                 FloorDiv(minVoxel.y, VoxelGrid.RegionVoxelEdge),
                                 FloorDiv(minVoxel.z, VoxelGrid.RegionVoxelEdge));
            int3 maxRegion = new(FloorDiv(maxVoxel.x, VoxelGrid.RegionVoxelEdge),
                                 FloorDiv(maxVoxel.y, VoxelGrid.RegionVoxelEdge),
                                 FloorDiv(maxVoxel.z, VoxelGrid.RegionVoxelEdge));

            for (int z = minRegion.z; z <= maxRegion.z; z++)
            for (int y = minRegion.y; y <= maxRegion.y; y++)
            for (int x = minRegion.x; x <= maxRegion.x; x++)
                if (source.IsRegionResident(new int3(x, y, z))) return true;
            return false;
        }

        private void EnforceCapacity(Camera camera, float voxelSize)
        {
            while (_entries.Count >= MaxResidentChunks && _dirty.Count > 0)
            {
                int3 victim = default;
                float farthest = -1f;
                Vector3 cameraPosition = camera != null ? camera.transform.position : Vector3.zero;
                float chunkMetres = VoxelsPerAxis * voxelSize;
                if (camera != null) GeometryUtility.CalculateFrustumPlanes(camera, _frustumPlanes);

                foreach (var pair in _entries)
                {
                    // A capacity limit may delay new work, but may never create a visible hole.
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
                    break;
                }
                if (_entries.TryGetValue(victim, out Entry entry)) entry.Dispose();
                _entries.Remove(victim);
                // Keep it known and queued. If the camera returns, nearest-first admission can
                // rebuild it; an evicted chunk must never become a permanent silent hole.
                _dirty.Add(victim);
            }
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

        private void RemoveChunk(int3 chunk)
        {
            _known.Remove(chunk);
            _dirty.Remove(chunk);
            _desiredVersions.Remove(chunk);
            _emptyVersions.Remove(chunk);
            _queuedAtSeconds.Remove(chunk);
            if (_entries.TryGetValue(chunk, out Entry entry))
            {
                entry.Dispose();
                _entries.Remove(chunk);
            }
            if (_build.Active && _build.Coordinate.Equals(chunk))
            {
                CompleteJobs();
                _build = default;
                _vertices.Clear();
                _indices.Clear();
            }
        }

        private void CompleteJobs()
        {
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
            foreach (Entry entry in _entries.Values) entry.Dispose();
            _entries.Clear();
            _known.Clear();
            _dirty.Clear();
            _desiredVersions.Clear();
            _queuedAtSeconds.Clear();
            _visible.Clear();
            _vertices.Clear();
            _indices.Clear();
            if (_density.IsCreated) _density.Dispose();
            if (_materials.IsCreated) _materials.Dispose();
            if (_surfaceSemantics.IsCreated) _surfaceSemantics.Dispose();
            if (_boundarySamples.IsCreated) _boundarySamples.Dispose();
            if (_densityBricks.IsCreated) _densityBricks.Dispose();
            if (_mipSampleOccupancy.IsCreated) _mipSampleOccupancy.Dispose();
            if (_mipSampleMaterials.IsCreated) _mipSampleMaterials.Dispose();
            if (_densityMixedVoxels.IsCreated) _densityMixedVoxels.Dispose();
            if (_densityMixedSurfaceSemantics.IsCreated) _densityMixedSurfaceSemantics.Dispose();
            if (_densityMixedBoundarySamples.IsCreated) _densityMixedBoundarySamples.Dispose();
            if (_topologyCellClass.IsCreated) _topologyCellClass.Dispose();
            if (_topologyGeometryCounts.IsCreated) _topologyGeometryCounts.Dispose();
            if (_topologyCellVertexIndices.IsCreated) _topologyCellVertexIndices.Dispose();
            if (_topologyEdgeCodes.IsCreated) _topologyEdgeCodes.Dispose();
            if (_topologyOutput.IsCreated) _topologyOutput.Dispose();
            if (_compactedTopologyVertices.IsCreated) _compactedTopologyVertices.Dispose();
            if (_compactedTopologyIndices.IsCreated) _compactedTopologyIndices.Dispose();
            if (_topologyOverflowCell.IsCreated) _topologyOverflowCell.Dispose();
            if (_faceDensity.IsCreated) _faceDensity.Dispose();
            if (_faceMaterials.IsCreated) _faceMaterials.Dispose();
            if (_faceSurfaces.IsCreated) _faceSurfaces.Dispose();
            if (_transitionCellClass.IsCreated) _transitionCellClass.Dispose();
            if (_transitionGeometryCounts.IsCreated) _transitionGeometryCounts.Dispose();
            if (_transitionCellIndices.IsCreated) _transitionCellIndices.Dispose();
            if (_transitionVertexData.IsCreated) _transitionVertexData.Dispose();
            if (_transitionVertices.IsCreated) _transitionVertices.Dispose();
            if (_transitionIndices.IsCreated) _transitionIndices.Dispose();
            if (_facetedMasks.IsCreated) _facetedMasks.Dispose();
            if (_facetedVertices.IsCreated) _facetedVertices.Dispose();
            if (_facetedIndices.IsCreated) _facetedIndices.Dispose();
            _build = default;
        }

        private static double ElapsedMs(double startSeconds) => startSeconds <= 0.0
            ? 0.0 : (Time.realtimeSinceStartupAsDouble - startSeconds) * 1000.0;
    }
}
