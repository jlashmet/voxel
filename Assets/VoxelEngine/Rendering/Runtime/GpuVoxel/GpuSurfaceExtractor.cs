using System;
using System.Runtime.InteropServices;
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
        public readonly int Handle;
        public readonly ulong Generation;
        internal readonly ProfileBlock[] ProfileBlocks;

        public GpuChunkExtraction(int3 chunkOriginVoxel, int3 brickCacheOrigin,
                                  int sourceStep, float voxelSize, int transitionFaceMask = 0,
                                  int handle = 0, ulong generation = 0,
                                  ProfileBlock[] profileBlocks = null)
        {
            ChunkOriginVoxel = chunkOriginVoxel;
            BrickCacheOrigin = brickCacheOrigin;
            SourceStep = sourceStep;
            VoxelSize = voxelSize;
            TransitionFaceMask = transitionFaceMask;
            Handle = handle;
            Generation = generation;
            ProfileBlocks = profileBlocks;
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
        public readonly uint UnsupportedMask;
        public bool Unsupported => UnsupportedMask != 0u;

        public GpuExtractionCounts(int vertexCount, int indexCount, bool unsupported = false)
            : this(vertexCount, indexCount, unsupported ? 1u : 0u)
        {
        }

        internal GpuExtractionCounts(int vertexCount, int indexCount, uint unsupportedMask)
        {
            VertexCount = vertexCount;
            IndexCount = indexCount;
            UnsupportedMask = unsupportedMask;
        }

        public bool IsEmpty => !Unsupported && (VertexCount == 0 || IndexCount == 0);
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

        [StructLayout(LayoutKind.Sequential)]
        internal struct BatchChunkDescriptor
        {
            internal int OriginX;
            internal int OriginY;
            internal int OriginZ;
            internal int SourceStep;
            internal uint TransitionFaceMask;
            internal float VoxelSize;
            internal uint Handle;
            internal uint GenerationLow;
            internal uint GenerationHigh;
            internal uint ProfileStart;
            internal uint ProfileCount;

            internal const int Stride = sizeof(int) * 4 + sizeof(uint) + sizeof(float)
                                      + sizeof(uint) * 5;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct GpuProfileBlock
        {
            internal int CentreX, CentreY, CentreZ;
            internal int InnerRadiusQ4, OuterRadiusQ4, FrontQ4, BackQ4;
            internal int BackingDepthVoxel;
            internal int StartX, StartY, EndX, EndY;
            internal uint Shape;
            internal uint Surface;
            internal uint Batch;

            internal const int Stride = sizeof(int) * 12 + sizeof(uint) * 3;

            internal static GpuProfileBlock From(in ProfileBlock block, int batch) => new()
            {
                CentreX = block.Centre.x,
                CentreY = block.Centre.y,
                CentreZ = block.Centre.z,
                InnerRadiusQ4 = block.InnerRadiusQ4,
                OuterRadiusQ4 = block.OuterRadiusQ4,
                FrontQ4 = block.FrontQ4,
                BackQ4 = block.BackQ4,
                BackingDepthVoxel = block.BackingDepthVoxel,
                StartX = block.StartDirection.x,
                StartY = block.StartDirection.y,
                EndX = block.EndDirection.x,
                EndY = block.EndDirection.y,
                Shape = (uint)block.Axis | ((uint)block.Material << 8)
                      | ((uint)block.SurfaceStyle << 16),
                Surface = block.Coating | ((uint)block.SurfaceDetail << 8)
                        | ((uint)block.JointHalfWidthQ4 << 16)
                        | ((uint)block.BevelQ4 << 24),
                Batch = unchecked((uint)batch),
            };
        }

        internal sealed class CountBatchResources : IDisposable
        {
            internal readonly int Capacity;
            internal readonly ComputeBuffer Chunks;
            internal readonly ComputeBuffer Density;
            internal readonly ComputeBuffer SampleMaterial;
            internal readonly ComputeBuffer SampleSurface;
            internal readonly ComputeBuffer SampleBoundary;
            internal readonly ComputeBuffer CellVertexCounts;
            internal readonly ComputeBuffer CellTriangleCounts;
            internal readonly ComputeBuffer CellReconstructionFlags;
            internal readonly ComputeBuffer FaceDensity;
            internal readonly ComputeBuffer FaceMaterial;
            internal readonly ComputeBuffer FaceSurface;
            internal readonly BatchChunkDescriptor[] Descriptors;
            internal readonly uint[] CounterZeros;
            internal ComputeBuffer Profiles;
            internal GpuProfileBlock[] ProfileStaging = Array.Empty<GpuProfileBlock>();
            internal int ProfileCount;

            internal CountBatchResources(int capacity, int sampleCount, int cellCount,
                                         int faceSampleCount)
            {
                Capacity = capacity;
                Chunks = new ComputeBuffer(capacity, BatchChunkDescriptor.Stride,
                                           ComputeBufferType.Structured);
                Density = new ComputeBuffer(capacity * sampleCount, sizeof(float),
                                            ComputeBufferType.Structured);
                SampleMaterial = new ComputeBuffer(capacity * sampleCount, sizeof(uint),
                                                   ComputeBufferType.Structured);
                SampleSurface = new ComputeBuffer(capacity * sampleCount, sizeof(uint),
                                                  ComputeBufferType.Structured);
                SampleBoundary = new ComputeBuffer(capacity * sampleCount, sizeof(uint),
                                                   ComputeBufferType.Structured);
                CellVertexCounts = new ComputeBuffer(capacity * cellCount, sizeof(uint),
                                                     ComputeBufferType.Structured);
                CellTriangleCounts = new ComputeBuffer(capacity * cellCount, sizeof(uint),
                                                       ComputeBufferType.Structured);
                CellReconstructionFlags = new ComputeBuffer(
                    capacity * cellCount, sizeof(uint), ComputeBufferType.Structured);
                FaceDensity = new ComputeBuffer(capacity * 6 * faceSampleCount, sizeof(float),
                                                ComputeBufferType.Structured);
                FaceMaterial = new ComputeBuffer(capacity * 6 * faceSampleCount, sizeof(uint),
                                                 ComputeBufferType.Structured);
                FaceSurface = new ComputeBuffer(capacity * 6 * faceSampleCount, sizeof(uint),
                                                ComputeBufferType.Structured);
                Descriptors = new BatchChunkDescriptor[capacity];
                CounterZeros = new uint[BatchHeaderWords + capacity * BatchRecordWords];
                Profiles = new ComputeBuffer(1, GpuProfileBlock.Stride,
                                             ComputeBufferType.Structured);
            }

            internal void StageProfiles(GpuChunkExtraction[] requests, int recordCount)
            {
                int count = 0;
                for (int i = 0; i < recordCount; i++)
                    count += requests[i].ProfileBlocks?.Length ?? 0;
                if (ProfileStaging.Length < count)
                    ProfileStaging = new GpuProfileBlock[Mathf.NextPowerOfTwo(count)];
                if (Profiles.count < Math.Max(1, count))
                {
                    Profiles.Release();
                    Profiles = new ComputeBuffer(Mathf.NextPowerOfTwo(count),
                                                 GpuProfileBlock.Stride,
                                                 ComputeBufferType.Structured);
                }

                int cursor = 0;
                for (int record = 0; record < recordCount; record++)
                {
                    BatchChunkDescriptor descriptor = Descriptors[record];
                    descriptor.ProfileStart = unchecked((uint)cursor);
                    ProfileBlock[] blocks = requests[record].ProfileBlocks;
                    descriptor.ProfileCount = unchecked((uint)(blocks?.Length ?? 0));
                    Descriptors[record] = descriptor;
                    if (blocks == null) continue;
                    for (int i = 0; i < blocks.Length; i++)
                        ProfileStaging[cursor++] = GpuProfileBlock.From(in blocks[i], record);
                }
                ProfileCount = count;
                if (count > 0) Profiles.SetData(ProfileStaging, 0, 0, count);
            }

            public void Dispose()
            {
                Chunks?.Release();
                Density?.Release();
                SampleMaterial?.Release();
                SampleSurface?.Release();
                SampleBoundary?.Release();
                CellVertexCounts?.Release();
                CellTriangleCounts?.Release();
                CellReconstructionFlags?.Release();
                FaceDensity?.Release();
                FaceMaterial?.Release();
                FaceSurface?.Release();
                Profiles?.Release();
            }
        }

        private const int BatchHeaderWords = 2;
        private const int BatchRecordWords = 10;

        private static readonly int IdDensity = Shader.PropertyToID("_Density");
        private static readonly int IdDensityWrite = Shader.PropertyToID("_DensityWrite");
        private static readonly int IdSampleMaterialWrite = Shader.PropertyToID("_SampleMaterialWrite");
        private static readonly int IdSampleSurfaceWrite = Shader.PropertyToID("_SampleSurfaceWrite");
        private static readonly int IdSampleBoundaryWrite = Shader.PropertyToID("_SampleBoundaryWrite");
        private static readonly int IdCellVertexCountsWrite = Shader.PropertyToID("_CellVertexCountsWrite");
        private static readonly int IdCellTriangleCountsWrite = Shader.PropertyToID("_CellTriangleCountsWrite");
        private static readonly int IdCellReconstructionFlagsWrite =
            Shader.PropertyToID("_CellReconstructionFlagsWrite");
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
        private static readonly int IdSolidWaterMaterialMask =
            Shader.PropertyToID("_SolidWaterMaterialMask");
        private static readonly int IdCellClass = Shader.PropertyToID("_RegularCellClass");
        private static readonly int IdGeometryCounts = Shader.PropertyToID("_RegularGeometryCounts");
        private static readonly int IdCellIndices = Shader.PropertyToID("_RegularCellIndices");
        private static readonly int IdEdgeCodes = Shader.PropertyToID("_RegularEdgeCodes");
        private static readonly int IdCellVertexCounts = Shader.PropertyToID("_CellVertexCounts");
        private static readonly int IdCellTriangleCounts = Shader.PropertyToID("_CellTriangleCounts");
        private static readonly int IdCellReconstructionFlags =
            Shader.PropertyToID("_CellReconstructionFlags");
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
        private static readonly int IdCopyVerticesSource =
            Shader.PropertyToID("_CopyVerticesSource");
        private static readonly int IdCopyVerticesDestination =
            Shader.PropertyToID("_CopyVerticesDestination");
        private static readonly int IdCopyIndicesSource =
            Shader.PropertyToID("_CopyIndicesSource");
        private static readonly int IdCopyIndicesDestination =
            Shader.PropertyToID("_CopyIndicesDestination");
        private static readonly int IdCopyVertexDestinationBase =
            Shader.PropertyToID("_CopyVertexDestinationBase");
        private static readonly int IdCopyIndexDestinationBase =
            Shader.PropertyToID("_CopyIndexDestinationBase");
        private static readonly int IdCopyVertexCount = Shader.PropertyToID("_CopyVertexCount");
        private static readonly int IdCopyIndexCount = Shader.PropertyToID("_CopyIndexCount");
        private static readonly int IdDrawArgs = Shader.PropertyToID("_DrawArgs");
        private static readonly int IdDrawArgsWordStart = Shader.PropertyToID("_DrawArgsWordStart");
        private static readonly int IdBatchCounters = Shader.PropertyToID("_BatchCounters");
        private static readonly int IdBatchCounterWordStart =
            Shader.PropertyToID("_BatchCounterWordStart");
        private static readonly int IdBatchRecordCount = Shader.PropertyToID("_BatchRecordCount");
        private static readonly int IdBatchVertexAlignment =
            Shader.PropertyToID("_BatchVertexAlignment");
        private static readonly int IdBatchIndexAlignment =
            Shader.PropertyToID("_BatchIndexAlignment");
        private static readonly int IdBatchChunks = Shader.PropertyToID("_BatchChunks");
        private static readonly int IdBatchDensity = Shader.PropertyToID("_BatchDensity");
        private static readonly int IdBatchDensityWrite = Shader.PropertyToID("_BatchDensityWrite");
        private static readonly int IdBatchSampleMaterial =
            Shader.PropertyToID("_BatchSampleMaterial");
        private static readonly int IdBatchSampleMaterialWrite =
            Shader.PropertyToID("_BatchSampleMaterialWrite");
        private static readonly int IdBatchSampleSurface =
            Shader.PropertyToID("_BatchSampleSurface");
        private static readonly int IdBatchSampleSurfaceWrite =
            Shader.PropertyToID("_BatchSampleSurfaceWrite");
        private static readonly int IdBatchSampleBoundary =
            Shader.PropertyToID("_BatchSampleBoundary");
        private static readonly int IdBatchSampleBoundaryWrite =
            Shader.PropertyToID("_BatchSampleBoundaryWrite");
        private static readonly int IdBatchCellVertexCounts =
            Shader.PropertyToID("_BatchCellVertexCounts");
        private static readonly int IdBatchCellVertexCountsWrite =
            Shader.PropertyToID("_BatchCellVertexCountsWrite");
        private static readonly int IdBatchCellTriangleCounts =
            Shader.PropertyToID("_BatchCellTriangleCounts");
        private static readonly int IdBatchCellTriangleCountsWrite =
            Shader.PropertyToID("_BatchCellTriangleCountsWrite");
        private static readonly int IdBatchCellReconstructionFlags =
            Shader.PropertyToID("_BatchCellReconstructionFlags");
        private static readonly int IdBatchCellReconstructionFlagsWrite =
            Shader.PropertyToID("_BatchCellReconstructionFlagsWrite");
        private static readonly int IdBatchFaceDensity = Shader.PropertyToID("_BatchFaceDensity");
        private static readonly int IdBatchFaceDensityWrite =
            Shader.PropertyToID("_BatchFaceDensityWrite");
        private static readonly int IdBatchFaceMaterial = Shader.PropertyToID("_BatchFaceMaterial");
        private static readonly int IdBatchFaceMaterialWrite =
            Shader.PropertyToID("_BatchFaceMaterialWrite");
        private static readonly int IdBatchFaceSurface = Shader.PropertyToID("_BatchFaceSurface");
        private static readonly int IdBatchFaceSurfaceWrite =
            Shader.PropertyToID("_BatchFaceSurfaceWrite");
        private static readonly int IdBatchVertices = Shader.PropertyToID("_BatchVertices");
        private static readonly int IdBatchIndices = Shader.PropertyToID("_BatchIndices");
        private static readonly int IdBatchDrawArgs = Shader.PropertyToID("_BatchDrawArgs");
        private static readonly int IdBatchVertexDestinationBase =
            Shader.PropertyToID("_BatchVertexDestinationBase");
        private static readonly int IdBatchIndexDestinationBase =
            Shader.PropertyToID("_BatchIndexDestinationBase");
        private static readonly int IdBatchArgsWordStart = Shader.PropertyToID("_BatchArgsWordStart");
        private static readonly int IdBatchPagedOutput = Shader.PropertyToID("_BatchPagedOutput");
        private static readonly int IdBatchVertexPageSize = Shader.PropertyToID("_BatchVertexPageSize");
        private static readonly int IdBatchIndexPageSize = Shader.PropertyToID("_BatchIndexPageSize");
        private static readonly int IdBatchMaxVertexPages =
            Shader.PropertyToID("_BatchMaxVertexPagesPerChunk");
        private static readonly int IdBatchMaxIndexPages =
            Shader.PropertyToID("_BatchMaxIndexPagesPerChunk");
        private static readonly int IdBatchVertexPageTable =
            Shader.PropertyToID("_BatchVertexPageTable");
        private static readonly int IdBatchIndexPageTable =
            Shader.PropertyToID("_BatchIndexPageTable");
        private static readonly int IdBatchProfiles = Shader.PropertyToID("_BatchProfiles");
        private static readonly int IdBatchProfileCount =
            Shader.PropertyToID("_BatchProfileCount");

        private readonly ComputeShader _shader;
        private readonly int _sampleKernel;
        private readonly int _batchSampleKernel;
        private readonly int _countKernel;
        private readonly int _batchCountKernel;
        private readonly int _countFacetedKernel;
        private readonly int _batchCountFacetedKernel;
        private readonly int _countDecorationsKernel;
        private readonly int _batchCountDecorationsKernel;
        private readonly int _batchSampleFacesKernel;
        private readonly int _batchCountTransitionsKernel;
        private readonly int _batchCountProfilesKernel;
        private readonly int _batchWriteKernel;
        private readonly int _batchWriteFacetedKernel;
        private readonly int _batchWriteDecorationsKernel;
        private readonly int _batchWriteTransitionsKernel;
        private readonly int _batchWriteProfilesKernel;
        private readonly int _batchPublishArgsKernel;
        private readonly int _writeKernel;
        private readonly int _writeFacetedKernel;
        private readonly int _writeDecorationsKernel;
        private readonly int _copyVerticesKernel;
        private readonly int _copyIndicesKernel;
        private readonly int _publishArgsKernel;
        private readonly int _copyCountersToBatchKernel;
        private readonly int _prefixBatchCountsKernel;
        private readonly int _faceKernel;
        private readonly int _transitionKernel;

        private readonly ComputeBuffer _density;
        private readonly ComputeBuffer _sampleMaterial;
        private readonly ComputeBuffer _sampleSurface;
        private readonly ComputeBuffer _sampleBoundary;
        private readonly ComputeBuffer _cellVertexCounts;
        private readonly ComputeBuffer _cellTriangleCounts;
        private readonly ComputeBuffer _cellReconstructionFlags;
        private readonly ComputeBuffer _brickCache;
        private readonly ComputeBuffer _counters;
        private readonly ComputeBuffer _faceDensity;
        private readonly ComputeBuffer _faceMaterial;
        private readonly ComputeBuffer _faceSurface;
        private readonly ComputeBuffer _chunkPages;
        private ComputeBuffer _writeScratchVertices;
        private ComputeBuffer _writeScratchIndices;
        private int _writeScratchVertexCapacity;
        private int _writeScratchIndexCapacity;

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
        private readonly int[] _int3Staging = new int[3];
        private readonly uint[] _pageStaging;
        private readonly uint[] _brickCacheStaging;
        private readonly CommandBuffer _productionCommands;
        private bool _disposed;

        /// <summary>Pages one chunk's geometry may span. Matches the arena's own ceiling.</summary>
        public int MaxPagesPerChunk { get; }

        /// <summary>
        /// Times two integers of bookkeeping have been copied back from the GPU.
        ///
        /// Production coalesces count records through the world-scoped batch coordinator, so it
        /// does not increment this per-extractor counter. Counter transfers here are retained only
        /// by blocking/oracle APIs; write/copy/args completion stays entirely GPU-ordered.
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
            // reaches one sample beyond every generated cell. The production path uses two because
            // transition sampling and smoothed density reconstruction need the second ring.
            if (padding < 1) throw new ArgumentOutOfRangeException(nameof(padding));
            CellsPerAxis = cellsPerAxis;
            Padding = padding;
            GridSize = cellsPerAxis + 1 + padding * 2;
            FaceSamplesPerAxis = cellsPerAxis * 2 + 1;

            _sampleKernel = shader.FindKernel("CSSampleDensity");
            _batchSampleKernel = shader.FindKernel("CSSampleDensityBatch");
            _countKernel = shader.FindKernel("CSCountCells");
            _batchCountKernel = shader.FindKernel("CSCountCellsBatch");
            _countFacetedKernel = shader.FindKernel("CSCountFacetedCells");
            _batchCountFacetedKernel = shader.FindKernel("CSCountFacetedCellsBatch");
            _countDecorationsKernel = shader.FindKernel("CSCountDecorations");
            _batchCountDecorationsKernel = shader.FindKernel("CSCountDecorationsBatch");
            _batchSampleFacesKernel = shader.FindKernel("CSSampleTransitionFacesBatch");
            _batchCountTransitionsKernel = shader.FindKernel("CSCountTransitionCellsBatch");
            _batchCountProfilesKernel = shader.FindKernel("CSCountProfileGeometryBatch");
            _batchWriteKernel = shader.FindKernel("CSWriteCellsBatch");
            _batchWriteFacetedKernel = shader.FindKernel("CSWriteFacetedCellsBatch");
            _batchWriteDecorationsKernel = shader.FindKernel("CSWriteDecorationsBatch");
            _batchWriteTransitionsKernel = shader.FindKernel("CSWriteTransitionCellsBatch");
            _batchWriteProfilesKernel = shader.FindKernel("CSWriteProfileGeometryBatch");
            _batchPublishArgsKernel = shader.FindKernel("CSPublishBatchDrawArgs");
            _writeKernel = shader.FindKernel("CSWriteCells");
            _writeFacetedKernel = shader.FindKernel("CSWriteFacetedCells");
            _writeDecorationsKernel = shader.FindKernel("CSWriteDecorations");
            _copyVerticesKernel = shader.FindKernel("CSCopyVerticesToArena");
            _copyIndicesKernel = shader.FindKernel("CSCopyIndicesToArena");
            _publishArgsKernel = shader.FindKernel("CSPublishDrawArgs");
            _copyCountersToBatchKernel = shader.FindKernel("CSCopyCountersToBatch");
            _prefixBatchCountsKernel = shader.FindKernel("CSPrefixBatchCounts");
            _faceKernel = shader.FindKernel("CSSampleTransitionFace");
            _transitionKernel = shader.FindKernel("CSTransitionCells");

            int samples = GridSize * GridSize * GridSize;
            int regularCellsPerAxis = cellsPerAxis + 1;
            int cells = regularCellsPerAxis * regularCellsPerAxis * regularCellsPerAxis;
            int faceSamples = FaceSamplesPerAxis * FaceSamplesPerAxis;

            _density = new ComputeBuffer(samples, sizeof(float), ComputeBufferType.Structured);
            _sampleMaterial = new ComputeBuffer(samples, sizeof(uint), ComputeBufferType.Structured);
            _sampleSurface = new ComputeBuffer(samples, sizeof(uint), ComputeBufferType.Structured);
            _sampleBoundary = new ComputeBuffer(samples, sizeof(uint), ComputeBufferType.Structured);
            _cellVertexCounts = new ComputeBuffer(cells, sizeof(uint), ComputeBufferType.Structured);
            _cellTriangleCounts = new ComputeBuffer(cells, sizeof(uint), ComputeBufferType.Structured);
            _cellReconstructionFlags = new ComputeBuffer(
                cells, sizeof(uint), ComputeBufferType.Structured);
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
            _productionCommands = new CommandBuffer { name = "Voxel GPU surface write batch" };
        }

        /// <summary>
        /// Uploads the surface rules. Cheap enough to do on any catalogue version change, since the
        /// catalogues are bounded at 32 styles, 256 join rules and 16 coatings.
        /// </summary>
        public void SetCatalogues(in SurfaceCatalogueView surfaces,
                                  in CoatingCatalogueView coatings,
                                  uint[] materialDefaultStyles,
                                  uint solidWaterMaterialMask = 0u)
        {
            for (ushort style = 0; style < GpuSurfaceCataloguePacking.StyleCount; style++)
                if (surfaces.Get(style).Reconstruction > SurfaceReconstruction.Cubic)
                    throw new NotSupportedException(
                        $"GPU surface extraction does not implement reconstruction {surfaces.Get(style).Reconstruction}.");
            for (byte coating = 0; coating < GpuSurfaceCataloguePacking.CoatingCount; coating++)
                if (coatings.Get(coating).DecorationShape > SurfaceDecorationShape.Clump)
                    throw new NotSupportedException(
                        $"GPU surface extraction does not implement decoration {coatings.Get(coating).DecorationShape}.");
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
            _shader.SetInt(IdSolidWaterMaterialMask, unchecked((int)solidWaterMaterialMask));
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
             || (uint)localBrick.z >= (uint)BrickCacheEdge)
                throw new ArgumentOutOfRangeException(nameof(localBrick));
            int flat = localBrick.x
                     + BrickCacheEdge * (localBrick.y + BrickCacheEdge * localBrick.z);
            _brickCacheStaging[flat] = entry;
        }

        public void ClearBrickCache() => Array.Clear(_brickCacheStaging, 0, _brickCacheStaging.Length);

        public void SetChunkPages(int[] pages, int count)
        {
            if (pages == null) throw new ArgumentNullException(nameof(pages));
            if (count < 0 || count > pages.Length || count > MaxPagesPerChunk)
                throw new ArgumentOutOfRangeException(nameof(count));
            Array.Clear(_pageStaging, 0, _pageStaging.Length);
            for (int i = 0; i < count; i++)
                _pageStaging[i] = unchecked((uint)pages[i]);
            _chunkPages.SetData(_pageStaging);
        }

        public GpuExtractionResult Extract(GpuVoxelBrickMirror mirror,
                                           GpuTransvoxelTables tables,
                                           int3 chunkOriginVoxel,
                                           int3 brickCacheOrigin,
                                           int sourceStep,
                                           float voxelSize,
                                           ComputeBuffer outputVertices,
                                           ComputeBuffer outputIndices,
                                           int vertexCapacity,
                                           int indexCapacity,
                                           int transitionFaceMask = 0,
                                           ProfileBlock[] profileBlocks = null)
        {
            var request = new GpuChunkExtraction(chunkOriginVoxel, brickCacheOrigin, sourceStep,
                                                 voxelSize, transitionFaceMask,
                                                 profileBlocks: profileBlocks);
            GpuExtractionCounts counts = Count(mirror, tables, request);
            if (counts.Unsupported)
                return new GpuExtractionResult(0, 0, overflowed: true);
            if (counts.VertexCount == 0 || counts.IndexCount == 0)
                return new GpuExtractionResult(0, 0, overflowed: false);
            if (counts.VertexCount > vertexCapacity || counts.IndexCount > indexCapacity)
                return new GpuExtractionResult(counts.VertexCount, counts.IndexCount, overflowed: true);
            Write(mirror, tables, request, outputVertices, outputIndices,
                  vertexCapacity, indexCapacity);
            return new GpuExtractionResult(counts.VertexCount, counts.IndexCount, overflowed: false);
        }

        public GpuExtractionCounts Count(GpuVoxelBrickMirror mirror,
                                         GpuTransvoxelTables tables,
                                         in GpuChunkExtraction request)
        {
            ThrowIfDisposed();
            if (mirror == null) throw new ArgumentNullException(nameof(mirror));
            if (tables == null) throw new ArgumentNullException(nameof(tables));
            DispatchDensity(mirror, request);
            DispatchCounts(tables);
            DispatchFacetedCounts();
            DispatchDecorationCounts();
            _shader.SetInt(IdFaceSamplesPerAxis, FaceSamplesPerAxis);
            for (int face = 0; face < 6; face++)
            {
                if ((request.TransitionFaceMask & (1 << face)) == 0) continue;
                DispatchFaceDensity(mirror, request, face);
                DispatchTransition(tables, face, countOnly: true);
            }
            _counters.GetData(_counterStaging);
            CounterReadbacks++;
            return new GpuExtractionCounts((int)_counterStaging[0], (int)_counterStaging[1],
                                           _counterStaging[2]);
        }

        internal GpuExtractionCounts ReadCountsBlocking()
        {
            _counters.GetData(_counterStaging);
            CounterReadbacks++;
            return new GpuExtractionCounts((int)_counterStaging[0], (int)_counterStaging[1],
                                           _counterStaging[2]);
        }

        public void Write(GpuVoxelBrickMirror mirror,
                          GpuTransvoxelTables tables,
                          in GpuChunkExtraction request,
                          ComputeBuffer outputVertices,
                          ComputeBuffer outputIndices,
                          int vertexCapacity,
                          int indexCapacity)
        {
            ThrowIfDisposed();
            if (mirror == null) throw new ArgumentNullException(nameof(mirror));
            if (tables == null) throw new ArgumentNullException(nameof(tables));
            if (outputVertices == null) throw new ArgumentNullException(nameof(outputVertices));
            if (outputIndices == null) throw new ArgumentNullException(nameof(outputIndices));

            SetChunkUniforms(request.ChunkOriginVoxel, request.BrickCacheOrigin,
                             request.SourceStep, request.VoxelSize,
                             request.TransitionFaceMask, usePersistentLookup: 0);
            BindDensity(_writeKernel, mirror);
            BindTables(_writeKernel, tables);
            _shader.SetBuffer(_writeKernel, IdDensity, _density);
            _shader.SetBuffer(_writeKernel, IdSampleMaterial, _sampleMaterial);
            _shader.SetBuffer(_writeKernel, IdSampleSurface, _sampleSurface);
            _shader.SetBuffer(_writeKernel, IdSampleBoundary, _sampleBoundary);
            _shader.SetBuffer(_writeKernel, IdCellVertexCounts, _cellVertexCounts);
            _shader.SetBuffer(_writeKernel, IdCellTriangleCounts, _cellTriangleCounts);
            _shader.SetBuffer(_writeKernel, IdCellReconstructionFlags, _cellReconstructionFlags);
            _shader.SetBuffer(_writeKernel, IdVertices, outputVertices);
            _shader.SetBuffer(_writeKernel, IdIndices, outputIndices);
            _shader.SetBuffer(_writeKernel, IdCounters, _counters);
            _shader.SetInt(IdVertexCapacity, vertexCapacity);
            _shader.SetInt(IdIndexCapacity, indexCapacity);
            _shader.SetInt(IdVertexWriteBase, 0);
            _shader.SetInt(IdIndexWriteBase, 0);
            DispatchKernel(_writeKernel, (CellsPerAxis + 1) * (CellsPerAxis + 1)
                                        * (CellsPerAxis + 1));
            BindDensity(_writeFacetedKernel, mirror);
            _shader.SetBuffer(_writeFacetedKernel, IdDensity, _density);
            _shader.SetBuffer(_writeFacetedKernel, IdSampleMaterial, _sampleMaterial);
            _shader.SetBuffer(_writeFacetedKernel, IdSampleSurface, _sampleSurface);
            _shader.SetBuffer(_writeFacetedKernel, IdSampleBoundary, _sampleBoundary);
            _shader.SetBuffer(_writeFacetedKernel, IdCellReconstructionFlags,
                              _cellReconstructionFlags);
            _shader.SetBuffer(_writeFacetedKernel, IdVertices, outputVertices);
            _shader.SetBuffer(_writeFacetedKernel, IdIndices, outputIndices);
            _shader.SetBuffer(_writeFacetedKernel, IdCounters, _counters);
            _shader.SetInt(IdVertexCapacity, vertexCapacity);
            _shader.SetInt(IdIndexCapacity, indexCapacity);
            _shader.SetInt(IdVertexWriteBase, 0);
            _shader.SetInt(IdIndexWriteBase, 0);
            DispatchKernel(_writeFacetedKernel, CellsPerAxis * CellsPerAxis * CellsPerAxis);
            BindDensity(_writeDecorationsKernel, mirror);
            _shader.SetBuffer(_writeDecorationsKernel, IdDensity, _density);
            _shader.SetBuffer(_writeDecorationsKernel, IdSampleMaterial, _sampleMaterial);
            _shader.SetBuffer(_writeDecorationsKernel, IdSampleSurface, _sampleSurface);
            _shader.SetBuffer(_writeDecorationsKernel, IdSampleBoundary, _sampleBoundary);
            _shader.SetBuffer(_writeDecorationsKernel, IdCellReconstructionFlags,
                              _cellReconstructionFlags);
            _shader.SetBuffer(_writeDecorationsKernel, IdVertices, outputVertices);
            _shader.SetBuffer(_writeDecorationsKernel, IdIndices, outputIndices);
            _shader.SetBuffer(_writeDecorationsKernel, IdCounters, _counters);
            _shader.SetInt(IdVertexCapacity, vertexCapacity);
            _shader.SetInt(IdIndexCapacity, indexCapacity);
            _shader.SetInt(IdVertexWriteBase, 0);
            _shader.SetInt(IdIndexWriteBase, 0);
            DispatchKernel(_writeDecorationsKernel, CellsPerAxis * CellsPerAxis * CellsPerAxis);
            for (int face = 0; face < 6; face++)
            {
                if ((request.TransitionFaceMask & (1 << face)) == 0) continue;
                DispatchFaceDensity(mirror, request, face);
                BindTransitionTables(_transitionKernel, tables);
                _shader.SetBuffer(_transitionKernel, IdVertices, outputVertices);
                _shader.SetBuffer(_transitionKernel, IdIndices, outputIndices);
                _shader.SetBuffer(_transitionKernel, IdCounters, _counters);
                _shader.SetInt(IdVertexCapacity, vertexCapacity);
                _shader.SetInt(IdIndexCapacity, indexCapacity);
                _shader.SetInt(IdVertexWriteBase, 0);
                _shader.SetInt(IdIndexWriteBase, 0);
                DispatchTransition(tables, face, countOnly: false);
            }
            DispatchProfileGeometry(profileBlocks: request.ProfileBlocks, outputVertices, outputIndices,
                                    vertexCapacity, indexCapacity);
        }

        internal CountBatchResources CreateCountBatchResources(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            int samples = GridSize * GridSize * GridSize;
            int regularCellsPerAxis = CellsPerAxis + 1;
            int cells = regularCellsPerAxis * regularCellsPerAxis * regularCellsPerAxis;
            int faceSamples = FaceSamplesPerAxis * FaceSamplesPerAxis;
            return new CountBatchResources(capacity, samples, cells, faceSamples);
        }

        /// <summary>
        /// Dispatches four kernels total for the whole lane, using dispatch Y as the chunk index.
        /// This is actual GPU batching: unlike recording N ordinary dispatches into one Unity
        /// command buffer, Metal receives one encoder workload per phase rather than per chunk.
        /// Transition faces join this path separately; callers must not silently omit them.
        /// </summary>
        internal void DispatchCountBatch(GpuVoxelBrickMirror mirror,
                                         GpuTransvoxelTables tables,
                                         GpuChunkExtraction[] requests,
                                         int recordCount,
                                         ComputeBuffer batchCounters,
                                         CountBatchResources resources)
        {
            ThrowIfDisposed();
            if (mirror == null) throw new ArgumentNullException(nameof(mirror));
            if (tables == null) throw new ArgumentNullException(nameof(tables));
            if (requests == null) throw new ArgumentNullException(nameof(requests));
            if (batchCounters == null) throw new ArgumentNullException(nameof(batchCounters));
            if (resources == null) throw new ArgumentNullException(nameof(resources));
            if (recordCount <= 0 || recordCount > resources.Capacity
                || recordCount > requests.Length)
                throw new ArgumentOutOfRangeException(nameof(recordCount));

            for (int i = 0; i < recordCount; i++)
            {
                int3 origin = requests[i].ChunkOriginVoxel;
                resources.Descriptors[i] = new BatchChunkDescriptor
                {
                    OriginX = origin.x,
                    OriginY = origin.y,
                    OriginZ = origin.z,
                    SourceStep = requests[i].SourceStep,
                    TransitionFaceMask = unchecked((uint)requests[i].TransitionFaceMask),
                    VoxelSize = requests[i].VoxelSize,
                    Handle = unchecked((uint)requests[i].Handle),
                    GenerationLow = (uint)requests[i].Generation,
                    GenerationHigh = (uint)(requests[i].Generation >> 32),
                };
            }
            resources.StageProfiles(requests, recordCount);
            resources.Chunks.SetData(resources.Descriptors, 0, 0, recordCount);
            _brickCache.SetData(_brickCacheStaging);
            batchCounters.SetData(resources.CounterZeros, 0, 0,
                                  BatchHeaderWords + recordCount * BatchRecordWords);

            SetChunkUniforms(int3.zero, int3.zero, 1, 1f, 0, 0);
            BindBatchShared(_batchSampleKernel, mirror, tables, batchCounters, resources);
            BindBatchShared(_batchCountKernel, mirror, tables, batchCounters, resources);
            BindBatchShared(_batchCountFacetedKernel, mirror, tables, batchCounters, resources);
            BindBatchShared(_batchCountDecorationsKernel, mirror, tables,
                            batchCounters, resources);
            BindBatchShared(_batchSampleFacesKernel, mirror, tables, batchCounters, resources);
            BindBatchShared(_batchCountTransitionsKernel, mirror, tables,
                            batchCounters, resources);
            BindBatchShared(_batchCountProfilesKernel, mirror, tables,
                            batchCounters, resources);
            BindTransitionTables(_batchCountTransitionsKernel, tables);
            _shader.SetInt(IdFaceSamplesPerAxis, FaceSamplesPerAxis);

            int samples = GridSize * GridSize * GridSize;
            int regularCellsPerAxis = CellsPerAxis + 1;
            int cells = regularCellsPerAxis * regularCellsPerAxis * regularCellsPerAxis;
            int semanticCells = CellsPerAxis * CellsPerAxis * CellsPerAxis;
            _shader.Dispatch(_batchSampleKernel, Groups(samples), recordCount, 1);
            _shader.Dispatch(_batchCountKernel, Groups(cells), recordCount, 1);
            _shader.Dispatch(_batchCountFacetedKernel,
                             Groups(semanticCells), recordCount, 1);
            _shader.Dispatch(_batchCountDecorationsKernel,
                             Groups(semanticCells), recordCount, 1);
            int faceSamples = FaceSamplesPerAxis * FaceSamplesPerAxis;
            _shader.Dispatch(_batchSampleFacesKernel, Groups(faceSamples), recordCount * 6, 1);
            _shader.Dispatch(_batchCountTransitionsKernel,
                             Groups(CellsPerAxis * CellsPerAxis), recordCount * 6, 1);
            if (resources.ProfileCount > 0)
                _shader.Dispatch(_batchCountProfilesKernel,
                                 Groups(resources.ProfileCount * 24), 1, 1);
        }

        /// <summary>
        /// Writes every surface category for every descriptor into its GPU-prefix range.
        /// </summary>
        internal void DispatchBaseWriteBatch(GpuVoxelBrickMirror mirror,
                                             GpuTransvoxelTables tables,
                                             GpuChunkExtraction[] requests,
                                             int recordCount,
                                             ComputeBuffer batchCounters,
                                             CountBatchResources resources,
                                             ComputeBuffer outputVertices,
                                             ComputeBuffer outputIndices)
        {
            ThrowIfDisposed();
            if (mirror == null) throw new ArgumentNullException(nameof(mirror));
            if (tables == null) throw new ArgumentNullException(nameof(tables));
            if (requests == null) throw new ArgumentNullException(nameof(requests));
            if (batchCounters == null) throw new ArgumentNullException(nameof(batchCounters));
            if (resources == null) throw new ArgumentNullException(nameof(resources));
            if (outputVertices == null) throw new ArgumentNullException(nameof(outputVertices));
            if (outputIndices == null) throw new ArgumentNullException(nameof(outputIndices));
            if (recordCount <= 0 || recordCount > resources.Capacity
                || recordCount > requests.Length)
                throw new ArgumentOutOfRangeException(nameof(recordCount));

            _productionCommands.Clear();
            BindBatchWriteShared(_batchWriteKernel, mirror, tables, batchCounters, resources,
                                 outputVertices, outputIndices);
            BindBatchWriteShared(_batchWriteFacetedKernel, mirror, tables, batchCounters, resources,
                                 outputVertices, outputIndices);
            BindBatchWriteShared(_batchWriteDecorationsKernel, mirror, tables, batchCounters, resources,
                                 outputVertices, outputIndices);
            BindBatchWriteShared(_batchWriteTransitionsKernel, mirror, tables, batchCounters, resources,
                                 outputVertices, outputIndices);
            BindBatchWriteShared(_batchWriteProfilesKernel, mirror, tables, batchCounters, resources,
                                 outputVertices, outputIndices);
            _shader.SetBuffer(_batchWriteTransitionsKernel, IdTransitionCellClass,
                              tables.TransitionCellClass);
            _shader.SetBuffer(_batchWriteTransitionsKernel, IdTransitionGeometryCounts,
                              tables.TransitionGeometryCounts);
            _shader.SetBuffer(_batchWriteTransitionsKernel, IdTransitionCellIndices,
                              tables.TransitionCellIndices);
            _shader.SetBuffer(_batchWriteTransitionsKernel, IdTransitionVertexData,
                              tables.TransitionVertexData);
            _shader.SetInt(IdFaceSamplesPerAxis, FaceSamplesPerAxis);

            int regularCellsPerAxis = CellsPerAxis + 1;
            int regularCells = regularCellsPerAxis * regularCellsPerAxis * regularCellsPerAxis;
            int semanticCells = CellsPerAxis * CellsPerAxis * CellsPerAxis;
            int transitionCells = CellsPerAxis * CellsPerAxis;
            int profileInvocations = Math.Max(1, resources.ProfileCount * 24);
            _productionCommands.DispatchCompute(_shader, _batchWriteKernel,
                                                Groups(regularCells), recordCount, 1);
            _productionCommands.DispatchCompute(_shader, _batchWriteFacetedKernel,
                                                Groups(semanticCells), recordCount, 1);
            _productionCommands.DispatchCompute(_shader, _batchWriteDecorationsKernel,
                                                Groups(semanticCells), recordCount, 1);
            _productionCommands.DispatchCompute(_shader, _batchWriteTransitionsKernel,
                                                Groups(transitionCells), recordCount * 6, 1);
            _productionCommands.DispatchCompute(_shader, _batchWriteProfilesKernel,
                                                Groups(profileInvocations), 1, 1);
            Graphics.ExecuteCommandBuffer(_productionCommands);
        }

        internal void CopyBatchToPagedArena(ComputeBuffer batchCounters, CountBatchResources resources,
                                            GpuMeshletPageArena arena, ComputeBuffer drawArgs,
                                            int drawArgsWordStart, int recordCount)
        {
            ThrowIfDisposed();
            if (batchCounters == null) throw new ArgumentNullException(nameof(batchCounters));
            if (resources == null) throw new ArgumentNullException(nameof(resources));
            if (arena == null) throw new ArgumentNullException(nameof(arena));
            if (drawArgs == null) throw new ArgumentNullException(nameof(drawArgs));
            if (recordCount <= 0 || recordCount > resources.Capacity)
                throw new ArgumentOutOfRangeException(nameof(recordCount));

            _productionCommands.Clear();
            _productionCommands.SetComputeIntParam(_shader, IdBatchRecordCount, recordCount);
            _productionCommands.SetComputeIntParam(_shader, IdBatchPagedOutput, 1);
            _productionCommands.SetComputeBufferParam(_shader, _batchPublishArgsKernel,
                                                      IdBatchCounters, batchCounters);
            _productionCommands.SetComputeBufferParam(_shader, _batchPublishArgsKernel,
                                                      IdBatchDrawArgs, drawArgs);
            _productionCommands.SetComputeIntParam(_shader, IdBatchArgsWordStart, drawArgsWordStart);
            _productionCommands.DispatchCompute(_shader, _batchPublishArgsKernel,
                                                Groups(recordCount), 1, 1);
            Graphics.ExecuteCommandBuffer(_productionCommands);
        }

        internal void DispatchCountBatchPersistent(GpuVoxelBrickMirror mirror,
                                                   GpuTransvoxelTables tables,
                                                   GpuChunkExtraction[] requests,
                                                   int recordCount,
                                                   ComputeBuffer batchCounters,
                                                   CountBatchResources resources)
        {
            ThrowIfDisposed();
            if (mirror == null) throw new ArgumentNullException(nameof(mirror));
            if (tables == null) throw new ArgumentNullException(nameof(tables));
            if (requests == null) throw new ArgumentNullException(nameof(requests));
            if (batchCounters == null) throw new ArgumentNullException(nameof(batchCounters));
            if (resources == null) throw new ArgumentNullException(nameof(resources));
            if (recordCount <= 0 || recordCount > resources.Capacity
                || recordCount > requests.Length)
                throw new ArgumentOutOfRangeException(nameof(recordCount));

            for (int i = 0; i < recordCount; i++)
            {
                int3 origin = requests[i].ChunkOriginVoxel;
                resources.Descriptors[i] = new BatchChunkDescriptor
                {
                    OriginX = origin.x,
                    OriginY = origin.y,
                    OriginZ = origin.z,
                    SourceStep = requests[i].SourceStep,
                    TransitionFaceMask = unchecked((uint)requests[i].TransitionFaceMask),
                    VoxelSize = requests[i].VoxelSize,
                    Handle = unchecked((uint)requests[i].Handle),
                    GenerationLow = (uint)requests[i].Generation,
                    GenerationHigh = (uint)(requests[i].Generation >> 32),
                };
            }
            resources.StageProfiles(requests, recordCount);
            resources.Chunks.SetData(resources.Descriptors, 0, 0, recordCount);
            batchCounters.SetData(resources.CounterZeros, 0, 0,
                                  BatchHeaderWords + recordCount * BatchRecordWords);

            SetChunkUniforms(int3.zero, int3.zero, 1, 1f, 0, usePersistentLookup: 1);
            BindBatchShared(_batchSampleKernel, mirror, tables, batchCounters, resources);
            BindBatchShared(_batchCountKernel, mirror, tables, batchCounters, resources);
            BindBatchShared(_batchCountFacetedKernel, mirror, tables, batchCounters, resources);
            BindBatchShared(_batchCountDecorationsKernel, mirror, tables,
                            batchCounters, resources);
            BindBatchShared(_batchSampleFacesKernel, mirror, tables, batchCounters, resources);
            BindBatchShared(_batchCountTransitionsKernel, mirror, tables,
                            batchCounters, resources);
            BindBatchShared(_batchCountProfilesKernel, mirror, tables,
                            batchCounters, resources);
            BindTransitionTables(_batchCountTransitionsKernel, tables);
            _shader.SetInt(IdFaceSamplesPerAxis, FaceSamplesPerAxis);

            int samples = GridSize * GridSize * GridSize;
            int regularCellsPerAxis = CellsPerAxis + 1;
            int cells = regularCellsPerAxis * regularCellsPerAxis * regularCellsPerAxis;
            int semanticCells = CellsPerAxis * CellsPerAxis * CellsPerAxis;
            _shader.Dispatch(_batchSampleKernel, Groups(samples), recordCount, 1);
            _shader.Dispatch(_batchCountKernel, Groups(cells), recordCount, 1);
            _shader.Dispatch(_batchCountFacetedKernel,
                             Groups(semanticCells), recordCount, 1);
            _shader.Dispatch(_batchCountDecorationsKernel,
                             Groups(semanticCells), recordCount, 1);
            int faceSamples = FaceSamplesPerAxis * FaceSamplesPerAxis;
            _shader.Dispatch(_batchSampleFacesKernel, Groups(faceSamples), recordCount * 6, 1);
            _shader.Dispatch(_batchCountTransitionsKernel,
                             Groups(CellsPerAxis * CellsPerAxis), recordCount * 6, 1);
            if (resources.ProfileCount > 0)
                _shader.Dispatch(_batchCountProfilesKernel,
                                 Groups(resources.ProfileCount * 24), 1, 1);
        }

        internal void DispatchWriteBatchPersistent(GpuVoxelBrickMirror mirror,
                                                   GpuTransvoxelTables tables,
                                                   GpuChunkExtraction[] requests,
                                                   int recordCount,
                                                   ComputeBuffer batchCounters,
                                                   CountBatchResources resources,
                                                   GpuMeshletPageArena arena,
                                                   ComputeBuffer drawArgs,
                                                   int drawArgsWordStart)
        {
            ThrowIfDisposed();
            if (mirror == null) throw new ArgumentNullException(nameof(mirror));
            if (tables == null) throw new ArgumentNullException(nameof(tables));
            if (requests == null) throw new ArgumentNullException(nameof(requests));
            if (batchCounters == null) throw new ArgumentNullException(nameof(batchCounters));
            if (resources == null) throw new ArgumentNullException(nameof(resources));
            if (arena == null) throw new ArgumentNullException(nameof(arena));
            if (drawArgs == null) throw new ArgumentNullException(nameof(drawArgs));
            if (recordCount <= 0 || recordCount > resources.Capacity
                || recordCount > requests.Length)
                throw new ArgumentOutOfRangeException(nameof(recordCount));

            _productionCommands.Clear();
            _productionCommands.SetComputeIntParam(_shader, IdBatchPagedOutput, 1);
            _productionCommands.SetComputeIntParam(_shader, IdBatchDrawArgs, drawArgsWordStart);
            BindBatchWriteShared(_batchWriteKernel, mirror, tables, batchCounters, resources,
                                 arena.VertexBuffer, arena.IndexBuffer);
            BindBatchWriteShared(_batchWriteFacetedKernel, mirror, tables, batchCounters, resources,
                                 arena.VertexBuffer, arena.IndexBuffer);
            BindBatchWriteShared(_batchWriteDecorationsKernel, mirror, tables, batchCounters, resources,
                                 arena.VertexBuffer, arena.IndexBuffer);
            BindBatchWriteShared(_batchWriteTransitionsKernel, mirror, tables, batchCounters, resources,
                                 arena.VertexBuffer, arena.IndexBuffer);
            BindBatchWriteShared(_batchWriteProfilesKernel, mirror, tables, batchCounters, resources,
                                 arena.VertexBuffer, arena.IndexBuffer);
            _productionCommands.SetComputeBufferParam(_shader, _batchWriteTransitionsKernel,
                                                      IdTransitionCellClass, tables.TransitionCellClass);
            _productionCommands.SetComputeBufferParam(_shader, _batchWriteTransitionsKernel,
                                                      IdTransitionGeometryCounts,
                                                      tables.TransitionGeometryCounts);
            _productionCommands.SetComputeBufferParam(_shader, _batchWriteTransitionsKernel,
                                                      IdTransitionCellIndices,
                                                      tables.TransitionCellIndices);
            _productionCommands.SetComputeBufferParam(_shader, _batchWriteTransitionsKernel,
                                                      IdTransitionVertexData,
                                                      tables.TransitionVertexData);
            _productionCommands.SetComputeIntParam(_shader, IdFaceSamplesPerAxis,
                                                   FaceSamplesPerAxis);

            int regularCellsPerAxis = CellsPerAxis + 1;
            int regularCells = regularCellsPerAxis * regularCellsPerAxis * regularCellsPerAxis;
            int semanticCells = CellsPerAxis * CellsPerAxis * CellsPerAxis;
            int transitionCells = CellsPerAxis * CellsPerAxis;
            int profileInvocations = Math.Max(1, resources.ProfileCount * 24);
            _productionCommands.DispatchCompute(_shader, _batchWriteKernel,
                                                Groups(regularCells), recordCount, 1);
            _productionCommands.DispatchCompute(_shader, _batchWriteFacetedKernel,
                                                Groups(semanticCells), recordCount, 1);
            _productionCommands.DispatchCompute(_shader, _batchWriteDecorationsKernel,
                                                Groups(semanticCells), recordCount, 1);
            _productionCommands.DispatchCompute(_shader, _batchWriteTransitionsKernel,
                                                Groups(transitionCells), recordCount * 6, 1);
            _productionCommands.DispatchCompute(_shader, _batchWriteProfilesKernel,
                                                Groups(profileInvocations), 1, 1);
            Graphics.ExecuteCommandBuffer(_productionCommands);
        }

        internal void CopyCountersToBatch(ComputeBuffer batchCounters, int batchCounterWordStart)
        {
            ThrowIfDisposed();
            if (batchCounters == null) throw new ArgumentNullException(nameof(batchCounters));
            if (batchCounterWordStart < 0
                || batchCounterWordStart + BatchRecordWords > batchCounters.count)
                throw new ArgumentOutOfRangeException(nameof(batchCounterWordStart));
            _shader.SetBuffer(_copyCountersToBatchKernel, IdCounters, _counters);
            _shader.SetBuffer(_copyCountersToBatchKernel, IdBatchCounters, batchCounters);
            _shader.SetInt(IdBatchCounterWordStart, batchCounterWordStart);
            _shader.Dispatch(_copyCountersToBatchKernel, 1, 1, 1);
        }

        internal void PrefixBatchCounts(ComputeBuffer batchCounters, int recordCount,
                                        int vertexAlignment = 1, int indexAlignment = 1)
        {
            ThrowIfDisposed();
            if (batchCounters == null) throw new ArgumentNullException(nameof(batchCounters));
            if (recordCount <= 0) throw new ArgumentOutOfRangeException(nameof(recordCount));
            _shader.SetBuffer(_prefixBatchCountsKernel, IdBatchCounters, batchCounters);
            _shader.SetInt(IdBatchRecordCount, recordCount);
            _shader.SetInt(IdBatchVertexAlignment, Math.Max(1, vertexAlignment));
            _shader.SetInt(IdBatchIndexAlignment, Math.Max(1, indexAlignment));
            _shader.Dispatch(_prefixBatchCountsKernel, 1, 1, 1);
        }

        internal void PublishBatchDrawArgs(ComputeBuffer batchCounters, ComputeBuffer drawArgs,
                                           int drawArgsWordStart, int recordCount)
        {
            ThrowIfDisposed();
            if (batchCounters == null) throw new ArgumentNullException(nameof(batchCounters));
            if (drawArgs == null) throw new ArgumentNullException(nameof(drawArgs));
            if (recordCount <= 0) throw new ArgumentOutOfRangeException(nameof(recordCount));
            _shader.SetBuffer(_batchPublishArgsKernel, IdBatchCounters, batchCounters);
            _shader.SetBuffer(_batchPublishArgsKernel, IdBatchDrawArgs, drawArgs);
            _shader.SetInt(IdBatchArgsWordStart, drawArgsWordStart);
            _shader.SetInt(IdBatchRecordCount, recordCount);
            DispatchKernel(_batchPublishArgsKernel, recordCount);
        }

        private void DispatchDensity(GpuVoxelBrickMirror mirror, in GpuChunkExtraction request)
        {
            _brickCache.SetData(_brickCacheStaging);
            SetChunkUniforms(request.ChunkOriginVoxel, request.BrickCacheOrigin,
                             request.SourceStep, request.VoxelSize,
                             request.TransitionFaceMask, usePersistentLookup: 0);
            BindDensity(_sampleKernel, mirror);
            _shader.SetBuffer(_sampleKernel, IdDensityWrite, _density);
            _shader.SetBuffer(_sampleKernel, IdSampleMaterialWrite, _sampleMaterial);
            _shader.SetBuffer(_sampleKernel, IdSampleSurfaceWrite, _sampleSurface);
            _shader.SetBuffer(_sampleKernel, IdSampleBoundaryWrite, _sampleBoundary);
            DispatchKernel(_sampleKernel, GridSize * GridSize * GridSize);
        }

        private void DispatchCounts(GpuTransvoxelTables tables)
        {
            Array.Clear(_counterStaging, 0, _counterStaging.Length);
            _counters.SetData(_counterStaging);
            _shader.SetBuffer(_countKernel, IdDensity, _density);
            _shader.SetBuffer(_countKernel, IdSampleMaterial, _sampleMaterial);
            _shader.SetBuffer(_countKernel, IdSampleSurface, _sampleSurface);
            _shader.SetBuffer(_countKernel, IdCellVertexCountsWrite, _cellVertexCounts);
            _shader.SetBuffer(_countKernel, IdCellTriangleCountsWrite, _cellTriangleCounts);
            _shader.SetBuffer(_countKernel, IdCellReconstructionFlagsWrite,
                              _cellReconstructionFlags);
            BindTables(_countKernel, tables);
            _shader.SetBuffer(_countKernel, IdCounters, _counters);
            DispatchKernel(_countKernel,
                           (CellsPerAxis + 1) * (CellsPerAxis + 1) * (CellsPerAxis + 1));
        }

        private void DispatchFacetedCounts()
        {
            _shader.SetBuffer(_countFacetedKernel, IdDensity, _density);
            _shader.SetBuffer(_countFacetedKernel, IdSampleMaterial, _sampleMaterial);
            _shader.SetBuffer(_countFacetedKernel, IdSampleSurface, _sampleSurface);
            _shader.SetBuffer(_countFacetedKernel, IdCellReconstructionFlagsWrite,
                              _cellReconstructionFlags);
            _shader.SetBuffer(_countFacetedKernel, IdCounters, _counters);
            DispatchKernel(_countFacetedKernel, CellsPerAxis * CellsPerAxis * CellsPerAxis);
        }

        private void DispatchDecorationCounts()
        {
            _shader.SetBuffer(_countDecorationsKernel, IdDensity, _density);
            _shader.SetBuffer(_countDecorationsKernel, IdSampleMaterial, _sampleMaterial);
            _shader.SetBuffer(_countDecorationsKernel, IdSampleSurface, _sampleSurface);
            _shader.SetBuffer(_countDecorationsKernel, IdSampleBoundary, _sampleBoundary);
            _shader.SetBuffer(_countDecorationsKernel, IdCellReconstructionFlags,
                              _cellReconstructionFlags);
            _shader.SetBuffer(_countDecorationsKernel, IdCounters, _counters);
            DispatchKernel(_countDecorationsKernel, CellsPerAxis * CellsPerAxis * CellsPerAxis);
        }

        private void DispatchProfileGeometry(ProfileBlock[] profileBlocks,
                                             ComputeBuffer outputVertices,
                                             ComputeBuffer outputIndices,
                                             int vertexCapacity,
                                             int indexCapacity)
        {
            if (profileBlocks == null || profileBlocks.Length == 0) return;
            using var profiles = new ComputeBuffer(profileBlocks.Length, GpuProfileBlock.Stride,
                                                   ComputeBufferType.Structured);
            var staging = new GpuProfileBlock[profileBlocks.Length];
            for (int i = 0; i < profileBlocks.Length; i++)
                staging[i] = GpuProfileBlock.From(in profileBlocks[i], batch: 0);
            profiles.SetData(staging);
            _shader.SetBuffer(_writeDecorationsKernel, IdBatchProfiles, profiles);
            _shader.SetInt(IdBatchProfileCount, profileBlocks.Length);
            _shader.SetBuffer(_writeDecorationsKernel, IdVertices, outputVertices);
            _shader.SetBuffer(_writeDecorationsKernel, IdIndices, outputIndices);
            _shader.SetBuffer(_writeDecorationsKernel, IdCounters, _counters);
            _shader.SetInt(IdVertexCapacity, vertexCapacity);
            _shader.SetInt(IdIndexCapacity, indexCapacity);
            DispatchKernel(_writeDecorationsKernel, profileBlocks.Length * 24);
        }

        private void DispatchFaceDensity(GpuVoxelBrickMirror mirror,
                                         in GpuChunkExtraction request, int face)
        {
            SetChunkUniforms(request.ChunkOriginVoxel, request.BrickCacheOrigin,
                             request.SourceStep, request.VoxelSize,
                             request.TransitionFaceMask, usePersistentLookup: 0);
            BindDensity(_faceKernel, mirror);
            _shader.SetBuffer(_faceKernel, IdFaceDensityWrite, _faceDensity);
            _shader.SetBuffer(_faceKernel, IdFaceMaterialWrite, _faceMaterial);
            _shader.SetBuffer(_faceKernel, IdFaceSurfaceWrite, _faceSurface);
            _shader.SetInt(IdFace, face);
            _shader.SetInt(IdFaceSamplesPerAxis, FaceSamplesPerAxis);
            DispatchKernel(_faceKernel, FaceSamplesPerAxis * FaceSamplesPerAxis);
        }

        private void DispatchTransition(GpuTransvoxelTables tables, int face, bool countOnly)
        {
            BindTransitionTables(_transitionKernel, tables);
            _shader.SetBuffer(_transitionKernel, IdFaceDensity, _faceDensity);
            _shader.SetBuffer(_transitionKernel, IdFaceMaterial, _faceMaterial);
            _shader.SetBuffer(_transitionKernel, IdFaceSurface, _faceSurface);
            _shader.SetBuffer(_transitionKernel, IdVertices,
                              countOnly ? _transitionSink : _writeScratchVertices);
            _shader.SetBuffer(_transitionKernel, IdIndices,
                              countOnly ? _transitionIndexSink : _writeScratchIndices);
            _shader.SetBuffer(_transitionKernel, IdCounters, _counters);
            _shader.SetInt(IdFace, face);
            _shader.SetInt(IdFaceSamplesPerAxis, FaceSamplesPerAxis);
            _shader.SetInt(IdTransitionCountOnly, countOnly ? 1 : 0);
            DispatchKernel(_transitionKernel, CellsPerAxis * CellsPerAxis);
        }

        public void ReadDensity(float[] destination)
        {
            if (destination == null || destination.Length != _density.count)
                throw new ArgumentException("Destination must match density buffer length.", nameof(destination));
            _density.GetData(destination);
            GeometryReadbacks++;
        }

        public void ReadSampleMaterials(uint[] destination)
        {
            if (destination == null || destination.Length != _sampleMaterial.count)
                throw new ArgumentException("Destination must match sample material buffer length.", nameof(destination));
            _sampleMaterial.GetData(destination);
            GeometryReadbacks++;
        }

        public void ReadSampleSurfaces(uint[] destination)
        {
            if (destination == null || destination.Length != _sampleSurface.count)
                throw new ArgumentException("Destination must match sample surface buffer length.", nameof(destination));
            _sampleSurface.GetData(destination);
            GeometryReadbacks++;
        }

        public void ReadSampleBoundaries(uint[] destination)
        {
            if (destination == null || destination.Length != _sampleBoundary.count)
                throw new ArgumentException("Destination must match sample boundary buffer length.", nameof(destination));
            _sampleBoundary.GetData(destination);
            GeometryReadbacks++;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _productionCommands?.Release();
            _density?.Release();
            _sampleMaterial?.Release();
            _sampleSurface?.Release();
            _sampleBoundary?.Release();
            _cellVertexCounts?.Release();
            _cellTriangleCounts?.Release();
            _cellReconstructionFlags?.Release();
            _brickCache?.Release();
            _counters?.Release();
            _faceDensity?.Release();
            _faceMaterial?.Release();
            _faceSurface?.Release();
            _chunkPages?.Release();
            _writeScratchVertices?.Release();
            _writeScratchIndices?.Release();
            _transitionSink?.Release();
            _transitionIndexSink?.Release();
            _styleWords?.Release();
            _joinWords?.Release();
            _coatingWords?.Release();
            _defaultStyle?.Release();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(GpuSurfaceExtractor));
        }

        private static int Groups(int items) => Math.Max(1, (items + ThreadGroupSize - 1) / ThreadGroupSize);
    }
}
