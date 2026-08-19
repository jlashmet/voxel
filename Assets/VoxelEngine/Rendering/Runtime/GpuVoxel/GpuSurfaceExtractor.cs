using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Rendering.Runtime.GpuVoxel
{
    /// <summary>Everything one chunk's extraction needs to know about where it sits in the world.</summary>
    public readonly struct GpuChunkExtraction
    {
        public readonly int3 ChunkOriginVoxel;
        public readonly int3 BrickCacheOrigin;
        public readonly int SourceStep;
        public readonly float VoxelSize;

        /// <summary>
        /// Bit per face — 0=-X, 1=+X, 2=-Y, 3=+Y, 4=-Z, 5=+Z — set where this chunk borders a
        /// finer ring and must be stitched. Zero for a chunk whose neighbours are all its own
        /// resolution, which is the common case.
        /// </summary>
        public readonly int TransitionFaceMask;

        public GpuChunkExtraction(int3 chunkOriginVoxel, int3 brickCacheOrigin,
                                  int sourceStep, float voxelSize, int transitionFaceMask = 0)
        {
            ChunkOriginVoxel = chunkOriginVoxel;
            BrickCacheOrigin = brickCacheOrigin;
            SourceStep = sourceStep;
            VoxelSize = voxelSize;
            TransitionFaceMask = transitionFaceMask;
        }
    }

    /// <summary>
    /// What a chunk's count pass says it is about to emit, before any of it is written.
    ///
    /// This is the whole reason the mesher runs in two halves. A shader cannot grow a buffer, so
    /// space has to be reserved first, and it can only be reserved once someone knows how much.
    /// </summary>
    public readonly struct GpuExtractionCounts
    {
        public readonly int VertexCount;
        public readonly int IndexCount;

        public GpuExtractionCounts(int vertexCount, int indexCount)
        {
            VertexCount = vertexCount;
            IndexCount = indexCount;
        }

        public bool IsEmpty => VertexCount == 0 || IndexCount == 0;
    }

    /// <summary>What one chunk's extraction produced.</summary>
    public readonly struct GpuExtractionResult
    {
        public readonly int VertexCount;
        public readonly int IndexCount;
        public readonly bool Overflowed;

        public GpuExtractionResult(int vertexCount, int indexCount, bool overflowed)
        {
            VertexCount = vertexCount;
            IndexCount = indexCount;
            Overflowed = overflowed;
        }

        public bool IsEmpty => IndexCount == 0;
    }

    /// <summary>
    /// Runs the compute mesher over one chunk.
    ///
    /// Three dispatches per chunk in the order the sizing strategy requires: sample the density
    /// field over a padded grid, count what each cell will emit, then write geometry. The counts
    /// exist so the caller can reserve pages before anything is written; nothing here reads geometry
    /// back, per the plan's no-readback invariant.
    ///
    /// The brick cache is the trick that keeps this simple. Density taps reach two voxels past the
    /// chunk in every direction, which crosses brick boundaries, and resolving that on the GPU would
    /// otherwise want a hash map from brick coordinate to mirror slot. Instead the caller flattens
    /// the chunk's brick neighbourhood into a small dense array — exactly as the CPU job already
    /// does for its own reads — so the shader indexes it with plain arithmetic.
    /// </summary>
    public sealed class GpuSurfaceExtractor : IDisposable
    {
        private const int ThreadGroupSize = 64;

        private static readonly int IdDensity = Shader.PropertyToID("_Density");
        private static readonly int IdDensityWrite = Shader.PropertyToID("_DensityWrite");
        private static readonly int IdSampleMaterialWrite = Shader.PropertyToID("_SampleMaterialWrite");
        private static readonly int IdSampleSurfaceWrite = Shader.PropertyToID("_SampleSurfaceWrite");
        private static readonly int IdSampleBoundaryWrite = Shader.PropertyToID("_SampleBoundaryWrite");
        private static readonly int IdCellVertexCountsWrite = Shader.PropertyToID("_CellVertexCountsWrite");
        private static readonly int IdCellTriangleCountsWrite = Shader.PropertyToID("_CellTriangleCountsWrite");
        private static readonly int IdSampleMaterial = Shader.PropertyToID("_SampleMaterial");
        private static readonly int IdSampleSurface = Shader.PropertyToID("_SampleSurface");
        private static readonly int IdSampleBoundary = Shader.PropertyToID("_SampleBoundary");
        private static readonly int IdBrickMaterials = Shader.PropertyToID("_BrickMaterials");
        private static readonly int IdBrickSurface = Shader.PropertyToID("_BrickSurfaceSemantics");
        private static readonly int IdBrickBoundary = Shader.PropertyToID("_BrickBoundarySamples");
        private static readonly int IdBrickCache = Shader.PropertyToID("_BrickCache");
        private static readonly int IdBrickCacheOrigin = Shader.PropertyToID("_BrickCacheOrigin");
        private static readonly int IdBrickCacheEdge = Shader.PropertyToID("_BrickCacheEdge");
        private static readonly int IdStyleWords = Shader.PropertyToID("_StyleWords");
        private static readonly int IdJoinWords = Shader.PropertyToID("_JoinWords");
        private static readonly int IdCoatingWords = Shader.PropertyToID("_CoatingWords");
        private static readonly int IdDefaultStyle = Shader.PropertyToID("_MaterialDefaultStyle");
        private static readonly int IdCellClass = Shader.PropertyToID("_RegularCellClass");
        private static readonly int IdGeometryCounts = Shader.PropertyToID("_RegularGeometryCounts");
        private static readonly int IdCellIndices = Shader.PropertyToID("_RegularCellIndices");
        private static readonly int IdEdgeCodes = Shader.PropertyToID("_RegularEdgeCodes");
        private static readonly int IdCellVertexCounts = Shader.PropertyToID("_CellVertexCounts");
        private static readonly int IdCellTriangleCounts = Shader.PropertyToID("_CellTriangleCounts");
        private static readonly int IdVertices = Shader.PropertyToID("_Vertices");
        private static readonly int IdIndices = Shader.PropertyToID("_Indices");
        private static readonly int IdCounters = Shader.PropertyToID("_Counters");
        private static readonly int IdChunkOrigin = Shader.PropertyToID("_ChunkOriginVoxel");
        private static readonly int IdCellsPerAxis = Shader.PropertyToID("_CellsPerAxis");
        private static readonly int IdGridSize = Shader.PropertyToID("_GridSize");
        private static readonly int IdPadding = Shader.PropertyToID("_Padding");
        private static readonly int IdSourceStep = Shader.PropertyToID("_SourceStep");
        private static readonly int IdVoxelSize = Shader.PropertyToID("_VoxelSize");
        private static readonly int IdVertexCapacity = Shader.PropertyToID("_VertexCapacity");
        private static readonly int IdIndexCapacity = Shader.PropertyToID("_IndexCapacity");
        private static readonly int IdTransitionCellClass = Shader.PropertyToID("_TransitionCellClass");
        private static readonly int IdTransitionGeometryCounts =
            Shader.PropertyToID("_TransitionGeometryCounts");
        private static readonly int IdTransitionCellIndices =
            Shader.PropertyToID("_TransitionCellIndices");
        private static readonly int IdTransitionVertexData =
            Shader.PropertyToID("_TransitionVertexData");
        private static readonly int IdTransitionVertexStride =
            Shader.PropertyToID("_TransitionVertexStride");
        private static readonly int IdTransitionIndexStride =
            Shader.PropertyToID("_TransitionIndexStride");
        private static readonly int IdFaceDensityWrite = Shader.PropertyToID("_FaceDensityWrite");
        private static readonly int IdFaceMaterialWrite = Shader.PropertyToID("_FaceMaterialWrite");
        private static readonly int IdFaceSurfaceWrite = Shader.PropertyToID("_FaceSurfaceWrite");
        private static readonly int IdFaceDensity = Shader.PropertyToID("_FaceDensity");
        private static readonly int IdFaceMaterial = Shader.PropertyToID("_FaceMaterial");
        private static readonly int IdFaceSurface = Shader.PropertyToID("_FaceSurface");
        private static readonly int IdFace = Shader.PropertyToID("_Face");
        private static readonly int IdFaceSamplesPerAxis = Shader.PropertyToID("_FaceSamplesPerAxis");
        private static readonly int IdTransitionCountOnly = Shader.PropertyToID("_TransitionCountOnly");
        private static readonly int IdChunkPages = Shader.PropertyToID("_ChunkPages");
        private static readonly int IdVerticesPerPage = Shader.PropertyToID("_VerticesPerPage");
        private static readonly int IdIndicesPerPage = Shader.PropertyToID("_IndicesPerPage");
        private static readonly int IdVertexWriteBase = Shader.PropertyToID("_VertexWriteBase");
        private static readonly int IdIndexWriteBase = Shader.PropertyToID("_IndexWriteBase");

        private readonly ComputeShader _shader;
        private readonly int _sampleKernel;
        private readonly int _countKernel;
        private readonly int _writeKernel;
        private readonly int _faceKernel;
        private readonly int _transitionKernel;

        private readonly ComputeBuffer _density;
        private readonly ComputeBuffer _sampleMaterial;
        private readonly ComputeBuffer _sampleSurface;
        private readonly ComputeBuffer _sampleBoundary;
        private readonly ComputeBuffer _cellVertexCounts;
        private readonly ComputeBuffer _cellTriangleCounts;
        private readonly ComputeBuffer _brickCache;
        private readonly ComputeBuffer _counters;
        private readonly ComputeBuffer _faceDensity;
        private readonly ComputeBuffer _faceMaterial;
        private readonly ComputeBuffer _faceSurface;
        private readonly ComputeBuffer _chunkPages;

        // Bound to the transition kernel while it is counting. It returns before touching either,
        // but an unbound UAV is undefined behaviour rather than a no-op, so it gets somewhere
        // harmless to point at.
        private readonly ComputeBuffer _transitionSink;
        private readonly ComputeBuffer _transitionIndexSink;

        private readonly ComputeBuffer _styleWords;
        private readonly ComputeBuffer _joinWords;
        private readonly ComputeBuffer _coatingWords;
        private readonly ComputeBuffer _defaultStyle;

        private readonly uint[] _counterStaging = new uint[4];
        private readonly uint[] _pageStaging;
        private readonly uint[] _brickCacheStaging;
        private bool _disposed;

        /// <summary>Pages one chunk's geometry may span. Matches the arena's own ceiling.</summary>
        public int MaxPagesPerChunk { get; }

        /// <summary>
        /// Times two integers of bookkeeping have been copied back from the GPU.
        ///
        /// This is the transfer the no-readback invariant permits, and it is bounded: one per count
        /// pass and one per write pass, regardless of how much geometry the chunk holds. What the
        /// invariant forbids is a readback that grows with the surface, because that puts the CPU
        /// back on the critical path the migration exists to get it off.
        /// </summary>
        public ulong CounterReadbacks { get; private set; }

        /// <summary>
        /// Times generated geometry or the sampled field has been copied back.
        ///
        /// Must stay zero on the frame path. Only the CPU-versus-GPU oracles read these, and they
        /// are verification code that allocates and blocks by design.
        /// </summary>
        public ulong GeometryReadbacks { get; private set; }

        public int CellsPerAxis { get; }
        public int Padding { get; }
        public int GridSize { get; }
        public int BrickCacheEdge { get; }

        /// <summary>
        /// Samples along one axis of a transition face snapshot: the finer neighbour's spacing, so
        /// twice this ring's cells plus the shared far edge.
        /// </summary>
        public int FaceSamplesPerAxis { get; }

        /// <param name="brickCacheEdge">
        /// Bricks per axis in the neighbourhood the caller will describe. Zero derives a value that
        /// covers the padded grid, which is right for a standalone caller; production passes the
        /// CPU builder's own edge instead, because the two must index the same flattened snapshot
        /// and a derived value that merely happens to be large enough would still address it wrong.
        /// </param>
        public GpuSurfaceExtractor(ComputeShader shader, int cellsPerAxis, int padding = 2,
                                   int brickCacheEdge = 0)
        {
            _shader = shader != null ? shader : throw new ArgumentNullException(nameof(shader));
            if (cellsPerAxis <= 0) throw new ArgumentOutOfRangeException(nameof(cellsPerAxis));

            // One voxel of skirt is the floor: the density normal is a central difference, so it
            // reaches one sample past the cell it belongs to. Wider taps beyond that clamp at the
            // grid edge, exactly as the CPU job does, so a wider skirt changes precision rather than
            // correctness — and the production builder runs at one.
            if (padding < 1)
                throw new ArgumentOutOfRangeException(nameof(padding),
                    "The density normal is a central difference, so the grid needs a voxel of skirt.");

            CellsPerAxis = cellsPerAxis;
            Padding = padding;
            GridSize = cellsPerAxis + padding * 2 + 1;

            FaceSamplesPerAxis = cellsPerAxis * 2 + 1;

            _sampleKernel = shader.FindKernel("CSSampleDensity");
            _countKernel = shader.FindKernel("CSCountCells");
            _writeKernel = shader.FindKernel("CSWriteCells");
            _faceKernel = shader.FindKernel("CSSampleFace");
            _transitionKernel = shader.FindKernel("CSTransitionCells");

            int samples = GridSize * GridSize * GridSize;
            int cells = cellsPerAxis * cellsPerAxis * cellsPerAxis;
            int faceSamples = FaceSamplesPerAxis * FaceSamplesPerAxis;

            _density = new ComputeBuffer(samples, sizeof(float), ComputeBufferType.Structured);
            _sampleMaterial = new ComputeBuffer(samples, sizeof(uint), ComputeBufferType.Structured);
            _sampleSurface = new ComputeBuffer(samples, sizeof(uint), ComputeBufferType.Structured);
            _sampleBoundary = new ComputeBuffer(samples, sizeof(uint), ComputeBufferType.Structured);
            _cellVertexCounts = new ComputeBuffer(cells, sizeof(uint), ComputeBufferType.Structured);
            _cellTriangleCounts = new ComputeBuffer(cells, sizeof(uint), ComputeBufferType.Structured);
            _counters = new ComputeBuffer(4, sizeof(uint), ComputeBufferType.Structured);
            MaxPagesPerChunk = GpuMeshletPageArena.DefaultMaxPagesPerChunk;
            _chunkPages = new ComputeBuffer(MaxPagesPerChunk, sizeof(uint),
                                            ComputeBufferType.Structured);
            _pageStaging = new uint[MaxPagesPerChunk];
            _transitionSink = new ComputeBuffer(1, ReadbackVertex.Stride,
                                                ComputeBufferType.Structured);
            _transitionIndexSink = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Structured);
            _faceDensity = new ComputeBuffer(faceSamples, sizeof(float), ComputeBufferType.Structured);
            _faceMaterial = new ComputeBuffer(faceSamples, sizeof(uint), ComputeBufferType.Structured);
            _faceSurface = new ComputeBuffer(faceSamples, sizeof(uint), ComputeBufferType.Structured);

            // The neighbourhood spans the chunk's own bricks plus the padded skirt on both sides.
            int paddedVoxels = cellsPerAxis + padding * 2 + 1;
            BrickCacheEdge = brickCacheEdge > 0
                ? brickCacheEdge
                : paddedVoxels / VoxelReadGrid.BlockEdge + 3;
            int bricks = BrickCacheEdge * BrickCacheEdge * BrickCacheEdge;
            _brickCache = new ComputeBuffer(bricks, sizeof(uint), ComputeBufferType.Structured);
            _brickCacheStaging = new uint[bricks];

            _styleWords = new ComputeBuffer(GpuSurfaceCataloguePacking.StyleCount, sizeof(uint),
                                            ComputeBufferType.Structured);
            _joinWords = new ComputeBuffer(GpuSurfaceCataloguePacking.JoinRuleCount, sizeof(uint),
                                           ComputeBufferType.Structured);
            _coatingWords = new ComputeBuffer(
                GpuSurfaceCataloguePacking.CoatingCount * GpuSurfaceCataloguePacking.CoatingWords,
                sizeof(uint), ComputeBufferType.Structured);
            _defaultStyle = new ComputeBuffer(256, sizeof(uint), ComputeBufferType.Structured);
        }

        /// <summary>
        /// Uploads the surface rules. Cheap enough to do on any catalogue version change, since the
        /// catalogues are bounded at 32 styles, 256 join rules and 16 coatings.
        /// </summary>
        public void SetCatalogues(in SurfaceCatalogueView surfaces,
                                  in CoatingCatalogueView coatings,
                                  uint[] materialDefaultStyles)
        {
            var styleWords = new uint[GpuSurfaceCataloguePacking.StyleCount];
            var joinWords = new uint[GpuSurfaceCataloguePacking.JoinRuleCount];
            var coatingWords = new uint[GpuSurfaceCataloguePacking.CoatingCount
                                      * GpuSurfaceCataloguePacking.CoatingWords];

            GpuSurfaceCataloguePacking.PackCatalogue(surfaces, styleWords, joinWords);
            GpuSurfaceCataloguePacking.PackCoatings(coatings, coatingWords);

            _styleWords.SetData(styleWords);
            _joinWords.SetData(joinWords);
            _coatingWords.SetData(coatingWords);

            var defaults = new uint[256];
            if (materialDefaultStyles != null)
                Array.Copy(materialDefaultStyles, defaults,
                           Math.Min(materialDefaultStyles.Length, defaults.Length));
            _defaultStyle.SetData(defaults);
        }

        /// <summary>
        /// Describes one brick to the shader: kind, uniform material, and mirror slot.
        /// Layout matches _BrickCache in VoxelBrickDensity.hlsl.
        /// </summary>
        public static uint PackBrickCacheEntry(VoxelBrickContent content, byte uniformMaterial,
                                               int slot) =>
            (uint)content
            | ((uint)uniformMaterial << 8)
            | (content == VoxelBrickContent.Mixed && slot >= 0 ? (uint)slot << 16 : 0u);

        public void SetBrickCacheEntry(int3 localBrick, uint entry)
        {
            if ((uint)localBrick.x >= (uint)BrickCacheEdge
             || (uint)localBrick.y >= (uint)BrickCacheEdge
             || (uint)localBrick.z >= (uint)BrickCacheEdge) return;

            _brickCacheStaging[localBrick.x
                + BrickCacheEdge * (localBrick.y + BrickCacheEdge * localBrick.z)] = entry;
        }

        public void ClearBrickCache() => Array.Clear(_brickCacheStaging, 0, _brickCacheStaging.Length);

        /// <summary>
        /// Meshes one chunk into the supplied geometry buffers.
        ///
        /// The counts are read back for the caller's page reservation, which is the one transfer the
        /// no-readback invariant permits: it is two integers of bookkeeping, not geometry, and it is
        /// what lets the arena refuse a build whole rather than truncate it.
        /// </summary>
        public GpuExtractionResult Extract(GpuVoxelBrickMirror mirror, GpuTransvoxelTables tables,
                                           int3 chunkOriginVoxel, int3 brickCacheOrigin,
                                           int sourceStep, float voxelSize,
                                           ComputeBuffer vertices, ComputeBuffer indices,
                                           int vertexCapacity, int indexCapacity)
        {
            ThrowIfDisposed();
            if (mirror == null) throw new ArgumentNullException(nameof(mirror));
            if (tables == null) throw new ArgumentNullException(nameof(tables));

            _brickCache.SetData(_brickCacheStaging);
            ResetCounters();
            SetIdentityPaging(vertexCapacity, indexCapacity);
            SetChunkUniforms(chunkOriginVoxel, brickCacheOrigin, sourceStep, voxelSize,
                             vertexCapacity, indexCapacity);

            BindShared(_sampleKernel, mirror, tables);
            BindShared(_countKernel, mirror, tables);
            BindShared(_writeKernel, mirror, tables);

            // Writable aliases only where the kernel actually writes, so no dispatch exceeds the
            // eight-UAV floor.
            _shader.SetBuffer(_sampleKernel, IdDensityWrite, _density);
            _shader.SetBuffer(_sampleKernel, IdSampleMaterialWrite, _sampleMaterial);
            _shader.SetBuffer(_sampleKernel, IdSampleSurfaceWrite, _sampleSurface);
            _shader.SetBuffer(_sampleKernel, IdSampleBoundaryWrite, _sampleBoundary);
            _shader.SetBuffer(_countKernel, IdCellVertexCountsWrite, _cellVertexCounts);
            _shader.SetBuffer(_countKernel, IdCellTriangleCountsWrite, _cellTriangleCounts);

            _shader.SetBuffer(_writeKernel, IdVertices, vertices);
            _shader.SetBuffer(_writeKernel, IdIndices, indices);

            int samples = GridSize * GridSize * GridSize;
            int cells = CellsPerAxis * CellsPerAxis * CellsPerAxis;

            _shader.Dispatch(_sampleKernel, Groups(samples), 1, 1);
            _shader.Dispatch(_countKernel, Groups(cells), 1, 1);
            _shader.Dispatch(_writeKernel, Groups(cells), 1, 1);

            return ReadCounters(vertexCapacity, indexCapacity);
        }

        /// <summary>
        /// Appends one face's transition cells to geometry already extracted for this chunk.
        ///
        /// Call after <see cref="Extract"/>, once per face that borders a finer ring. The counters
        /// are deliberately not reset: transition geometry belongs to the same chunk and shares its
        /// buffers, so the returned counts are cumulative and the caller's reservation covers both.
        ///
        /// The face is sampled at half this ring's stride, which is the finer neighbour's spacing and
        /// the reason this needs its own pass rather than a second read of the chunk lattice — that
        /// lattice does not contain the intermediate positions at all.
        /// </summary>
        public GpuExtractionResult ExtractTransitionFace(
            GpuVoxelBrickMirror mirror, GpuTransvoxelTables tables,
            int face, int3 chunkOriginVoxel, int3 brickCacheOrigin,
            int sourceStep, float voxelSize,
            ComputeBuffer vertices, ComputeBuffer indices,
            int vertexCapacity, int indexCapacity)
        {
            ThrowIfDisposed();
            if (mirror == null) throw new ArgumentNullException(nameof(mirror));
            if (tables == null) throw new ArgumentNullException(nameof(tables));
            if ((uint)face >= 6u) throw new ArgumentOutOfRangeException(nameof(face));

            SetIdentityPaging(vertexCapacity, indexCapacity);
            SetChunkUniforms(chunkOriginVoxel, brickCacheOrigin, sourceStep, voxelSize,
                             vertexCapacity, indexCapacity);
            _shader.SetInt(IdFace, face);
            _shader.SetInt(IdFaceSamplesPerAxis, FaceSamplesPerAxis);
            _shader.SetInt(IdTransitionCountOnly, 0);

            BindShared(_faceKernel, mirror, tables);
            BindShared(_transitionKernel, mirror, tables);
            BindTransitionTables(_transitionKernel, tables);

            _shader.SetBuffer(_faceKernel, IdFaceDensityWrite, _faceDensity);
            _shader.SetBuffer(_faceKernel, IdFaceMaterialWrite, _faceMaterial);
            _shader.SetBuffer(_faceKernel, IdFaceSurfaceWrite, _faceSurface);

            _shader.SetBuffer(_transitionKernel, IdFaceDensity, _faceDensity);
            _shader.SetBuffer(_transitionKernel, IdFaceMaterial, _faceMaterial);
            _shader.SetBuffer(_transitionKernel, IdFaceSurface, _faceSurface);
            _shader.SetBuffer(_transitionKernel, IdVertices, vertices);
            _shader.SetBuffer(_transitionKernel, IdIndices, indices);

            _shader.Dispatch(_faceKernel, Groups(FaceSamplesPerAxis * FaceSamplesPerAxis), 1, 1);
            _shader.Dispatch(_transitionKernel, Groups(CellsPerAxis * CellsPerAxis), 1, 1);

            return ReadCounters(vertexCapacity, indexCapacity);
        }

        /// <summary>
        /// Counts what this chunk is about to emit, without emitting any of it.
        ///
        /// This is the first half of the count-reserve-write cycle the arena needs. It runs the
        /// sampling and counting kernels, and the transition kernel in a mode that takes every one
        /// of the same early exits and table lookups but writes nothing — so the number returned
        /// cannot be smaller than the geometry it is reserved for, which is the only property that
        /// makes an all-or-nothing reservation safe.
        ///
        /// Two integers come back. That is the one transfer the no-readback invariant permits: it
        /// is bookkeeping, not geometry, and it is what lets the arena refuse a build whole rather
        /// than truncate it into a hole.
        /// </summary>
        public GpuExtractionCounts Count(GpuVoxelBrickMirror mirror, GpuTransvoxelTables tables,
                                         in GpuChunkExtraction request)
        {
            DispatchCount(mirror, tables, request);
            CounterReadbacks++;
            _counters.GetData(_counterStaging);
            return new GpuExtractionCounts((int)_counterStaging[2], (int)_counterStaging[3]);
        }

        /// <summary>
        /// Runs the counting pass and asks for the counters without waiting for them.
        ///
        /// <see cref="Count"/> blocks the calling thread until the GPU drains, which on a frame path
        /// costs far more than the meshing it is waiting for. Poll <see cref="TryCompleteCount"/>
        /// on later frames instead; the build that needs the answer is already sliced across frames.
        /// </summary>
        public void BeginCount(GpuVoxelBrickMirror mirror, GpuTransvoxelTables tables,
                               in GpuChunkExtraction request)
        {
            DispatchCount(mirror, tables, request);
            RequestCounters();
        }

        private void DispatchCount(GpuVoxelBrickMirror mirror, GpuTransvoxelTables tables,
                                   in GpuChunkExtraction request)
        {
            ThrowIfDisposed();
            if (mirror == null) throw new ArgumentNullException(nameof(mirror));
            if (tables == null) throw new ArgumentNullException(nameof(tables));

            _brickCache.SetData(_brickCacheStaging);
            ResetCounters();

            // Capacity is irrelevant while counting — nothing is written — but the uniforms are
            // shared with the write pass, so they are set to something harmless rather than stale.
            SetChunkUniforms(request.ChunkOriginVoxel, request.BrickCacheOrigin,
                             request.SourceStep, request.VoxelSize, 0, 0);

            BindShared(_sampleKernel, mirror, tables);
            BindShared(_countKernel, mirror, tables);
            _shader.SetBuffer(_sampleKernel, IdDensityWrite, _density);
            _shader.SetBuffer(_sampleKernel, IdSampleMaterialWrite, _sampleMaterial);
            _shader.SetBuffer(_sampleKernel, IdSampleSurfaceWrite, _sampleSurface);
            _shader.SetBuffer(_sampleKernel, IdSampleBoundaryWrite, _sampleBoundary);
            _shader.SetBuffer(_countKernel, IdCellVertexCountsWrite, _cellVertexCounts);
            _shader.SetBuffer(_countKernel, IdCellTriangleCountsWrite, _cellTriangleCounts);

            int samples = GridSize * GridSize * GridSize;
            int cells = CellsPerAxis * CellsPerAxis * CellsPerAxis;
            _shader.Dispatch(_sampleKernel, Groups(samples), 1, 1);
            _shader.Dispatch(_countKernel, Groups(cells), 1, 1);

            DispatchTransitionFaces(mirror, tables, request, countOnly: true);
        }

        /// <summary>
        /// Writes the chunk into pages the caller has already reserved.
        ///
        /// The density field is not re-sampled: <see cref="Count"/> left it in place, and the two
        /// halves are meant to be called back to back on the same chunk. Transition faces are
        /// re-sampled, because there is only one face snapshot buffer and six possible faces.
        ///
        /// <paramref name="pages"/> is the chunk's page list from the arena. Its order is the order
        /// the shader walks, so vertex <c>n</c> lands in page <c>n / verticesPerPage</c> — which is
        /// why a chunk's geometry can be scattered without anything having to be compacted.
        /// </summary>
        public GpuExtractionResult Write(GpuVoxelBrickMirror mirror, GpuTransvoxelTables tables,
                                         in GpuChunkExtraction request,
                                         ComputeBuffer vertices, ComputeBuffer indices,
                                         System.Collections.Generic.IReadOnlyList<int> pages,
                                         int verticesPerPage, int indicesPerPage)
        {
            ThrowIfDisposed();
            if (mirror == null) throw new ArgumentNullException(nameof(mirror));
            if (tables == null) throw new ArgumentNullException(nameof(tables));
            if (pages == null) throw new ArgumentNullException(nameof(pages));
            if (pages.Count == 0) return new GpuExtractionResult(0, 0, false);
            if (pages.Count > MaxPagesPerChunk)
                throw new ArgumentOutOfRangeException(nameof(pages),
                    $"{pages.Count} pages exceeds the {MaxPagesPerChunk} one chunk may hold.");
            if (verticesPerPage <= 0) throw new ArgumentOutOfRangeException(nameof(verticesPerPage));
            if (indicesPerPage <= 0) throw new ArgumentOutOfRangeException(nameof(indicesPerPage));

            for (int i = 0; i < pages.Count; i++) _pageStaging[i] = (uint)pages[i];
            _chunkPages.SetData(_pageStaging, 0, 0, pages.Count);
            _shader.SetInt(IdVerticesPerPage, verticesPerPage);
            _shader.SetInt(IdIndicesPerPage, indicesPerPage);
            _shader.SetInt(IdVertexWriteBase, 0);
            _shader.SetInt(IdIndexWriteBase, 0);

            // Capacity is expressed in the chunk's own local numbering, not the arena's, because
            // that is the space the write cursors count in.
            int vertexCapacity = pages.Count * verticesPerPage;
            int indexCapacity = pages.Count * indicesPerPage;

            ResetCounters();
            SetChunkUniforms(request.ChunkOriginVoxel, request.BrickCacheOrigin,
                             request.SourceStep, request.VoxelSize, vertexCapacity, indexCapacity);

            BindShared(_writeKernel, mirror, tables);
            _shader.SetBuffer(_writeKernel, IdVertices, vertices);
            _shader.SetBuffer(_writeKernel, IdIndices, indices);

            int cells = CellsPerAxis * CellsPerAxis * CellsPerAxis;
            _shader.Dispatch(_writeKernel, Groups(cells), 1, 1);

            DispatchTransitionFaces(mirror, tables, request, countOnly: false,
                                    vertices, indices);

            return ReadCounters(vertexCapacity, indexCapacity);
        }

        /// <summary>
        /// Writes the chunk into a plain contiguous range someone else allocated.
        ///
        /// This is the seam onto the renderer's existing geometry arena, which hands out ranges
        /// rather than pages. Index values stay in the chunk's own numbering — the draw shader adds
        /// the chunk's vertex base when it dereferences them — so a range written here is
        /// indistinguishable from one the CPU mesher uploaded, and the render path does not have to
        /// know which produced it.
        ///
        /// As with <see cref="Write"/>, the density field is not re-sampled: <see cref="Count"/>
        /// must have run on this chunk immediately before.
        /// </summary>
        public GpuExtractionResult WriteRange(GpuVoxelBrickMirror mirror, GpuTransvoxelTables tables,
                                              in GpuChunkExtraction request,
                                              ComputeBuffer vertices, ComputeBuffer indices,
                                              int vertexStart, int vertexCapacity,
                                              int indexStart, int indexCapacity)
        {
            DispatchWriteRange(mirror, tables, request, vertices, indices,
                               vertexStart, vertexCapacity, indexStart, indexCapacity);
            return ReadCounters(vertexCapacity, indexCapacity);
        }

        /// <summary>
        /// Writes the staged chunk into an arena range and asks for the verification counters
        /// without waiting. The non-blocking counterpart of <see cref="WriteRange"/>; complete it
        /// with <see cref="TryCompleteWriteRange"/>.
        /// </summary>
        public void BeginWriteRange(GpuVoxelBrickMirror mirror, GpuTransvoxelTables tables,
                                    in GpuChunkExtraction request,
                                    ComputeBuffer vertices, ComputeBuffer indices,
                                    int vertexStart, int vertexCapacity,
                                    int indexStart, int indexCapacity)
        {
            DispatchWriteRange(mirror, tables, request, vertices, indices,
                               vertexStart, vertexCapacity, indexStart, indexCapacity);
            RequestCounters();
        }

        private void DispatchWriteRange(GpuVoxelBrickMirror mirror, GpuTransvoxelTables tables,
                                        in GpuChunkExtraction request,
                                        ComputeBuffer vertices, ComputeBuffer indices,
                                        int vertexStart, int vertexCapacity,
                                        int indexStart, int indexCapacity)
        {
            ThrowIfDisposed();
            if (mirror == null) throw new ArgumentNullException(nameof(mirror));
            if (tables == null) throw new ArgumentNullException(nameof(tables));
            if (vertexStart < 0) throw new ArgumentOutOfRangeException(nameof(vertexStart));
            if (indexStart < 0) throw new ArgumentOutOfRangeException(nameof(indexStart));
            if (vertexCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(vertexCapacity));
            if (indexCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(indexCapacity));

            ResetCounters();
            SetIdentityPaging(vertexCapacity, indexCapacity, vertexStart, indexStart);
            SetChunkUniforms(request.ChunkOriginVoxel, request.BrickCacheOrigin,
                             request.SourceStep, request.VoxelSize, vertexCapacity, indexCapacity);

            BindShared(_writeKernel, mirror, tables);
            _shader.SetBuffer(_writeKernel, IdVertices, vertices);
            _shader.SetBuffer(_writeKernel, IdIndices, indices);

            int cells = CellsPerAxis * CellsPerAxis * CellsPerAxis;
            _shader.Dispatch(_writeKernel, Groups(cells), 1, 1);

            DispatchTransitionFaces(mirror, tables, request, countOnly: false, vertices, indices);
        }

        private void DispatchTransitionFaces(GpuVoxelBrickMirror mirror, GpuTransvoxelTables tables,
                                             in GpuChunkExtraction request, bool countOnly,
                                             ComputeBuffer vertices = null,
                                             ComputeBuffer indices = null)
        {
            if (request.TransitionFaceMask == 0) return;

            _shader.SetInt(IdFaceSamplesPerAxis, FaceSamplesPerAxis);
            _shader.SetInt(IdTransitionCountOnly, countOnly ? 1 : 0);

            BindShared(_faceKernel, mirror, tables);
            BindShared(_transitionKernel, mirror, tables);
            BindTransitionTables(_transitionKernel, tables);
            _shader.SetBuffer(_faceKernel, IdFaceDensityWrite, _faceDensity);
            _shader.SetBuffer(_faceKernel, IdFaceMaterialWrite, _faceMaterial);
            _shader.SetBuffer(_faceKernel, IdFaceSurfaceWrite, _faceSurface);
            _shader.SetBuffer(_transitionKernel, IdFaceDensity, _faceDensity);
            _shader.SetBuffer(_transitionKernel, IdFaceMaterial, _faceMaterial);
            _shader.SetBuffer(_transitionKernel, IdFaceSurface, _faceSurface);

            // The transition kernel declares both, so both must be bound even when it writes
            // nothing; an unbound UAV is undefined behaviour, not a no-op.
            _shader.SetBuffer(_transitionKernel, IdVertices,
                              vertices != null ? vertices : _transitionSink);
            _shader.SetBuffer(_transitionKernel, IdIndices,
                              indices != null ? indices : _transitionIndexSink);

            for (int face = 0; face < 6; face++)
            {
                if ((request.TransitionFaceMask & (1 << face)) == 0) continue;
                _shader.SetInt(IdFace, face);
                _shader.Dispatch(_faceKernel, Groups(FaceSamplesPerAxis * FaceSamplesPerAxis), 1, 1);
                _shader.Dispatch(_transitionKernel, Groups(CellsPerAxis * CellsPerAxis), 1, 1);
            }
        }

        /// <summary>
        /// Maps the chunk's local numbering straight onto a plain buffer: one page, the size of the
        /// whole thing. Keeps the shader to a single addressing path rather than branching between
        /// paged and unpaged writes.
        /// </summary>
        private void SetIdentityPaging(int vertexCapacity, int indexCapacity,
                                       int vertexWriteBase = 0, int indexWriteBase = 0)
        {
            _pageStaging[0] = 0;
            _chunkPages.SetData(_pageStaging, 0, 0, 1);
            _shader.SetInt(IdVerticesPerPage, Math.Max(1, vertexCapacity));
            _shader.SetInt(IdIndicesPerPage, Math.Max(1, indexCapacity));
            _shader.SetInt(IdVertexWriteBase, vertexWriteBase);
            _shader.SetInt(IdIndexWriteBase, indexWriteBase);
        }

        private void ResetCounters()
        {
            Array.Clear(_counterStaging, 0, _counterStaging.Length);
            _counters.SetData(_counterStaging);
        }

        private void SetChunkUniforms(int3 chunkOriginVoxel, int3 brickCacheOrigin,
                                      int sourceStep, float voxelSize,
                                      int vertexCapacity, int indexCapacity)
        {
            _shader.SetInts(IdChunkOrigin, chunkOriginVoxel.x, chunkOriginVoxel.y, chunkOriginVoxel.z);
            _shader.SetInts(IdBrickCacheOrigin, brickCacheOrigin.x, brickCacheOrigin.y,
                            brickCacheOrigin.z);
            _shader.SetInt(IdBrickCacheEdge, BrickCacheEdge);
            _shader.SetInt(IdCellsPerAxis, CellsPerAxis);
            _shader.SetInt(IdGridSize, GridSize);
            _shader.SetInt(IdPadding, Padding);
            _shader.SetInt(IdSourceStep, sourceStep);
            _shader.SetFloat(IdVoxelSize, voxelSize);
            _shader.SetInt(IdVertexCapacity, vertexCapacity);
            _shader.SetInt(IdIndexCapacity, indexCapacity);
        }

        /// <summary>Whether a pass dispatched with a Begin* call has produced its counters yet.</summary>
        public enum GpuCounterPoll
        {
            /// <summary>The GPU has not finished. Ask again on a later frame.</summary>
            Pending = 0,
            /// <summary>Counters are in <see cref="_counterStaging"/>.</summary>
            Ready = 1,
            /// <summary>The readback failed, or none was outstanding. Abandon the attempt.</summary>
            Failed = 2,
        }

        /// <summary>
        /// Whether counters can be fetched without stalling. A device without async readback keeps
        /// the blocking path, which is correct but costs a pipeline flush per pass.
        /// </summary>
        public static bool SupportsAsyncCounters => SystemInfo.supportsAsyncGPUReadback;

        private AsyncGPUReadbackRequest _counterRequest;
        private bool _counterRequestPending;

        private void RequestCounters()
        {
            _counterRequest = AsyncGPUReadback.Request(_counters);
            _counterRequestPending = true;
        }

        private GpuCounterPoll PollCounters()
        {
            if (!_counterRequestPending) return GpuCounterPoll.Failed;
            if (_counterRequest.hasError)
            {
                _counterRequestPending = false;
                return GpuCounterPoll.Failed;
            }
            if (!_counterRequest.done) return GpuCounterPoll.Pending;

            _counterRequestPending = false;
            NativeArray<uint> data = _counterRequest.GetData<uint>();
            int count = Math.Min(_counterStaging.Length, data.Length);
            for (int i = 0; i < count; i++) _counterStaging[i] = data[i];
            CounterReadbacks++;
            return GpuCounterPoll.Ready;
        }

        /// <summary>Completes a <see cref="BeginCount"/> without blocking.</summary>
        public GpuCounterPoll TryCompleteCount(out GpuExtractionCounts counts)
        {
            counts = default;
            GpuCounterPoll poll = PollCounters();
            if (poll != GpuCounterPoll.Ready) return poll;
            counts = new GpuExtractionCounts((int)_counterStaging[2], (int)_counterStaging[3]);
            return GpuCounterPoll.Ready;
        }

        /// <summary>Completes a <see cref="BeginWriteRange"/> without blocking.</summary>
        public GpuCounterPoll TryCompleteWriteRange(int vertexCapacity, int indexCapacity,
                                                    out GpuExtractionResult result)
        {
            result = default;
            GpuCounterPoll poll = PollCounters();
            if (poll != GpuCounterPoll.Ready) return poll;
            result = BuildResult(vertexCapacity, indexCapacity);
            return GpuCounterPoll.Ready;
        }

        /// <summary>Drops any outstanding readback so an abandoned build cannot complete into the next.</summary>
        public void CancelPendingCounters() => _counterRequestPending = false;

        private GpuExtractionResult BuildResult(int vertexCapacity, int indexCapacity)
        {
            int vertexCount = (int)_counterStaging[0];
            int indexCount = (int)_counterStaging[1];
            bool overflowed = vertexCount > vertexCapacity || indexCount > indexCapacity;
            return new GpuExtractionResult(Math.Min(vertexCount, vertexCapacity),
                                           Math.Min(indexCount, indexCapacity), overflowed);
        }

        private GpuExtractionResult ReadCounters(int vertexCapacity, int indexCapacity)
        {
            CounterReadbacks++;
            _counters.GetData(_counterStaging);
            return BuildResult(vertexCapacity, indexCapacity);
        }

        /// <summary>
        /// Per-cell counts, for diagnostics.
        ///
        /// Not the sizing path: <see cref="Count"/> returns the totals the shader summed, which is
        /// two integers rather than one per cell. Reading the whole array back would scale with the
        /// chunk, so calling this counts as a geometry readback.
        /// </summary>
        public void ReadCellCounts(uint[] vertexCounts, uint[] triangleCounts)
        {
            GeometryReadbacks++;
            _cellVertexCounts.GetData(vertexCounts);
            _cellTriangleCounts.GetData(triangleCounts);
        }

        /// <summary>
        /// One vertex as the shader writes it. Mirrors the SurfaceVertex struct in the compute
        /// shader; used only to read geometry back for the oracle.
        /// </summary>
        public struct ReadbackVertex
        {
            public Vector3 Position;
            public Vector3 Normal;
            public uint Material;
            public uint Active;

            public const int Stride = sizeof(float) * 6 + sizeof(uint) * 2;
        }

        /// <summary>Sampled density, for the CPU-vs-GPU oracle. Never called on the frame path.</summary>
        public void ReadDensity(float[] density)
        {
            GeometryReadbacks++;
            _density.GetData(density);
        }

        public void ReadSampleMaterials(uint[] materials)
        {
            GeometryReadbacks++;
            _sampleMaterial.GetData(materials);
        }

        /// <summary>Face snapshot, for the transition oracle. Never called on the frame path.</summary>
        public void ReadFaceDensity(float[] density)
        {
            GeometryReadbacks++;
            _faceDensity.GetData(density);
        }

        private void BindTransitionTables(int kernel, GpuTransvoxelTables tables)
        {
            _shader.SetBuffer(kernel, IdTransitionCellClass, tables.TransitionCellClass);
            _shader.SetBuffer(kernel, IdTransitionGeometryCounts, tables.TransitionGeometryCounts);
            _shader.SetBuffer(kernel, IdTransitionCellIndices, tables.TransitionCellIndices);
            _shader.SetBuffer(kernel, IdTransitionVertexData, tables.TransitionVertexData);
            _shader.SetInt(IdTransitionVertexStride, tables.TransitionVertexStride);
            _shader.SetInt(IdTransitionIndexStride, tables.TransitionIndexStride);
        }

        private void BindShared(int kernel, GpuVoxelBrickMirror mirror, GpuTransvoxelTables tables)
        {
            _shader.SetBuffer(kernel, IdDensity, _density);
            _shader.SetBuffer(kernel, IdSampleMaterial, _sampleMaterial);
            _shader.SetBuffer(kernel, IdSampleSurface, _sampleSurface);
            _shader.SetBuffer(kernel, IdSampleBoundary, _sampleBoundary);
            _shader.SetBuffer(kernel, IdBrickMaterials, mirror.Materials);
            _shader.SetBuffer(kernel, IdBrickSurface, mirror.SurfaceSemantics);
            _shader.SetBuffer(kernel, IdBrickBoundary, mirror.BoundarySamples);
            _shader.SetBuffer(kernel, IdBrickCache, _brickCache);
            _shader.SetBuffer(kernel, IdStyleWords, _styleWords);
            _shader.SetBuffer(kernel, IdJoinWords, _joinWords);
            _shader.SetBuffer(kernel, IdCoatingWords, _coatingWords);
            _shader.SetBuffer(kernel, IdDefaultStyle, _defaultStyle);
            _shader.SetBuffer(kernel, IdCellClass, tables.CellClass);
            _shader.SetBuffer(kernel, IdGeometryCounts, tables.GeometryCounts);
            _shader.SetBuffer(kernel, IdCellIndices, tables.CellIndices);
            _shader.SetBuffer(kernel, IdEdgeCodes, tables.EdgeCodes);
            _shader.SetBuffer(kernel, IdCellVertexCounts, _cellVertexCounts);
            _shader.SetBuffer(kernel, IdCellTriangleCounts, _cellTriangleCounts);
            _shader.SetBuffer(kernel, IdCounters, _counters);
            _shader.SetBuffer(kernel, IdChunkPages, _chunkPages);
        }

        private static int Groups(int items) => (items + ThreadGroupSize - 1) / ThreadGroupSize;

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(GpuSurfaceExtractor));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _density?.Release();
            _sampleMaterial?.Release();
            _sampleSurface?.Release();
            _sampleBoundary?.Release();
            _cellVertexCounts?.Release();
            _cellTriangleCounts?.Release();
            _brickCache?.Release();
            _counters?.Release();
            _faceDensity?.Release();
            _faceMaterial?.Release();
            _faceSurface?.Release();
            _chunkPages?.Release();
            _transitionSink?.Release();
            _transitionIndexSink?.Release();
            _styleWords?.Release();
            _joinWords?.Release();
            _coatingWords?.Release();
            _defaultStyle?.Release();
        }
    }
}
