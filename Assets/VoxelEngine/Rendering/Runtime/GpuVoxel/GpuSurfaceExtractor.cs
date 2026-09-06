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
    /// Standalone/editor callers describe one dense brick neighbourhood directly. Production batch
    /// lanes instead resolve the persistent mirror into reusable GPU-owned dense slices before the
    /// meshing kernels run, keeping persistent directory traversal out of this shader compilation.
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
            internal readonly GpuBrickCachePreparation PreparedCache;
            internal readonly int BrickCacheEdge;
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
                                         int faceSampleCount, int brickCacheEdge)
            {
                Capacity = capacity;
                BrickCacheEdge = brickCacheEdge;
                PreparedCache = new GpuBrickCachePreparation(capacity, brickCacheEdge);
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
                CellReconstructionFlags = new ComputeBuffer(capacity * cellCount, sizeof(uint),
                                                            ComputeBufferType.Structured);
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
                PreparedCache?.Dispose();
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
        private static readonly int IdBatchBrickCacheViews =
            Shader.PropertyToID("_BatchBrickCacheViews");
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
            _batchSampleKernel = shader.FindKernel("CSBatchSampleDensity");
            _countKernel = shader.FindKernel("CSCountCells");
            _batchCountKernel = shader.FindKernel("CSBatchCountCells");
            _countFacetedKernel = shader.FindKernel("CSCountFacetedFaces");
            _batchCountFacetedKernel = shader.FindKernel("CSBatchCountFacetedFaces");
            _countDecorationsKernel = shader.FindKernel("CSCountDecorations");
            _batchCountDecorationsKernel = shader.FindKernel("CSBatchCountDecorations");
            _batchSampleFacesKernel = shader.FindKernel("CSBatchSampleFaces");
            _batchCountTransitionsKernel = shader.FindKernel("CSBatchCountTransitions");
            _batchCountProfilesKernel = shader.FindKernel("CSBatchCountProfiles");
            _batchWriteKernel = shader.FindKernel("CSBatchWriteCells");
            _batchWriteFacetedKernel = shader.FindKernel("CSBatchWriteFacetedFaces");
            _batchWriteDecorationsKernel = shader.FindKernel("CSBatchWriteDecorations");
            _batchWriteTransitionsKernel = shader.FindKernel("CSBatchWriteTransitions");
            _batchWriteProfilesKernel = shader.FindKernel("CSBatchWriteProfiles");
            _batchPublishArgsKernel = shader.FindKernel("CSBatchPublishArgs");
            _writeKernel = shader.FindKernel("CSWriteCells");
            _writeFacetedKernel = shader.FindKernel("CSWriteFacetedFaces");
            _writeDecorationsKernel = shader.FindKernel("CSWriteDecorations");
            _copyVerticesKernel = shader.FindKernel("CSCopyVertices");
            _copyIndicesKernel = shader.FindKernel("CSCopyIndices");
            _publishArgsKernel = shader.FindKernel("CSPublishArgs");
            _copyCountersToBatchKernel = shader.FindKernel("CSCopyCountersToBatch");
            _prefixBatchCountsKernel = shader.FindKernel("CSPrefixBatchCounts");
            _faceKernel = shader.FindKernel("CSSampleFace");
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
                                  uint[] materialDefaultStyles)
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
            BindShared(_countFacetedKernel, mirror, tables);
            BindShared(_countDecorationsKernel, mirror, tables);
            BindShared(_writeKernel, mirror, tables);
            BindShared(_writeFacetedKernel, mirror, tables);
            BindShared(_writeDecorationsKernel, mirror, tables);

            // Writable aliases only where the kernel actually writes, so no dispatch exceeds the
            // eight-UAV floor.
            _shader.SetBuffer(_sampleKernel, IdDensityWrite, _density);
            _shader.SetBuffer(_sampleKernel, IdSampleMaterialWrite, _sampleMaterial);
            _shader.SetBuffer(_sampleKernel, IdSampleSurfaceWrite, _sampleSurface);
            _shader.SetBuffer(_sampleKernel, IdSampleBoundaryWrite, _sampleBoundary);
            _shader.SetBuffer(_countKernel, IdCellVertexCountsWrite, _cellVertexCounts);
            _shader.SetBuffer(_countKernel, IdCellTriangleCountsWrite, _cellTriangleCounts);
            _shader.SetBuffer(_countKernel, IdCellReconstructionFlagsWrite,
                              _cellReconstructionFlags);

            _shader.SetBuffer(_writeKernel, IdVertices, vertices);
            _shader.SetBuffer(_writeKernel, IdIndices, indices);
            _shader.SetBuffer(_writeFacetedKernel, IdVertices, vertices);
            _shader.SetBuffer(_writeFacetedKernel, IdIndices, indices);
            _shader.SetBuffer(_writeDecorationsKernel, IdVertices, vertices);
            _shader.SetBuffer(_writeDecorationsKernel, IdIndices, indices);

            int samples = GridSize * GridSize * GridSize;
            int regularCellsPerAxis = CellsPerAxis + 1;
            int cells = regularCellsPerAxis * regularCellsPerAxis * regularCellsPerAxis;

            _shader.Dispatch(_sampleKernel, Groups(samples), 1, 1);
            _shader.Dispatch(_countKernel, Groups(cells), 1, 1);
            int semanticCells = CellsPerAxis * CellsPerAxis * CellsPerAxis;
            _shader.Dispatch(_countFacetedKernel, Groups(semanticCells), 1, 1);
            _shader.Dispatch(_countDecorationsKernel, Groups(semanticCells), 1, 1);
            _shader.Dispatch(_writeKernel, Groups(cells), 1, 1);
            _shader.Dispatch(_writeFacetedKernel, Groups(semanticCells), 1, 1);
            _shader.Dispatch(_writeDecorationsKernel, Groups(semanticCells), 1, 1);

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
        /// This blocking compatibility path is compiled only in the editor for isolated GPU oracle
        /// tests. Production uses the batched GPU prefix/page-allocation path and reads back neither
        /// geometry nor counters.
        /// </summary>
#if UNITY_EDITOR
        public GpuExtractionCounts Count(GpuVoxelBrickMirror mirror, GpuTransvoxelTables tables,
                                         in GpuChunkExtraction request)
        {
            DispatchCount(mirror, tables, request);
            CounterReadbacks++;
            _counters.GetData(_counterStaging);
            return new GpuExtractionCounts((int)_counterStaging[2], (int)_counterStaging[3],
                                           _counterStaging[0]);
        }
#endif

        /// <summary>
        /// Runs the counting pass and asks for the counters without waiting for them.
        ///
        /// The editor-only Count compatibility helper blocks until the GPU drains. Production
        /// callers poll <see cref="TryCompleteCount"/> on later frames instead; the build that
        /// needs the answer is already sliced across frames.
        /// </summary>
        public void BeginCount(GpuVoxelBrickMirror mirror, GpuTransvoxelTables tables,
                               in GpuChunkExtraction request)
        {
            DispatchCount(mirror, tables, request);
            RequestCounters();
        }

        /// <summary>
        /// Copies the current four-word count result into one shared batch record. A later prefix
        /// kernel fills the record's aligned offsets/capacities; the sampled field remains private
        /// to this extractor for its write pass.
        /// </summary>
        internal void CopyCountToBatch(ComputeBuffer batchCounters, int recordIndex)
        {
            ThrowIfDisposed();
            if (batchCounters == null) throw new ArgumentNullException(nameof(batchCounters));
            int wordStart = BatchHeaderWords + recordIndex * BatchRecordWords;
            if (recordIndex < 0 || wordStart + 4 > batchCounters.count)
                throw new ArgumentOutOfRangeException(nameof(recordIndex));
            _shader.SetBuffer(_copyCountersToBatchKernel, IdCounters, _counters);
            _shader.SetBuffer(_copyCountersToBatchKernel, IdBatchCounters, batchCounters);
            _shader.SetInt(IdBatchCounterWordStart, wordStart);
            _shader.Dispatch(_copyCountersToBatchKernel, 1, 1, 1);
        }

        internal const int BatchHeaderWords = 4;
        internal const int BatchRecordWords = 17;

        internal void PrepareCountBatchResources(ref CountBatchResources resources, int capacity)
        {
            // Call only for an idle lane, after its completion fence. Layout changes affect the
            // resolver's flattening stride as well as buffer sizes; extra capacity is not enough.
            if (resources != null && resources.Capacity == capacity
                && MatchesCountBatchResources(resources)) return;
            resources?.Dispose();
            resources = CreateCountBatchResources(capacity);
        }

        internal bool HasSameBatchLayout(GpuSurfaceExtractor other) => other != null
            && CellsPerAxis == other.CellsPerAxis && Padding == other.Padding
            && BrickCacheEdge == other.BrickCacheEdge;

        private bool MatchesCountBatchResources(CountBatchResources resources) =>
            resources.BrickCacheEdge == BrickCacheEdge
            && resources.Density.count == resources.Capacity * GridSize * GridSize * GridSize
            && resources.CellVertexCounts.count == resources.Capacity
                * (CellsPerAxis + 1) * (CellsPerAxis + 1) * (CellsPerAxis + 1)
            && resources.FaceDensity.count == resources.Capacity * 6
                * FaceSamplesPerAxis * FaceSamplesPerAxis;

        internal CountBatchResources CreateCountBatchResources(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            int samples = GridSize * GridSize * GridSize;
            int regularCellsPerAxis = CellsPerAxis + 1;
            int cells = regularCellsPerAxis * regularCellsPerAxis * regularCellsPerAxis;
            int faceSamples = FaceSamplesPerAxis * FaceSamplesPerAxis;
            return new CountBatchResources(capacity, samples, cells, faceSamples, BrickCacheEdge);
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
            if (!MatchesCountBatchResources(resources))
                throw new ArgumentException("Batch resources do not match the extractor layout.", nameof(resources));
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
            resources.PreparedCache.Dispatch(mirror, requests, recordCount);
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
                                             int recordCount,
                                             ComputeBuffer batchCounters,
                                             CountBatchResources resources,
                                             ComputeBuffer vertices,
                                             ComputeBuffer indices,
                                             int vertexDestinationBase = 0,
                                             int indexDestinationBase = 0,
                                             ComputeBuffer args = null,
                                             int argsWordStart = 0,
                                             GpuSurfacePageArena pageArena = null,
                                             int frame = 0)
        {
            ThrowIfDisposed();
            if (mirror == null) throw new ArgumentNullException(nameof(mirror));
            if (tables == null) throw new ArgumentNullException(nameof(tables));
            if (batchCounters == null) throw new ArgumentNullException(nameof(batchCounters));
            if (resources == null) throw new ArgumentNullException(nameof(resources));
            if (!MatchesCountBatchResources(resources))
                throw new ArgumentException("Batch resources do not match the extractor layout.", nameof(resources));
            if (vertices == null) throw new ArgumentNullException(nameof(vertices));
            if (indices == null) throw new ArgumentNullException(nameof(indices));
            if (recordCount <= 0 || recordCount > resources.Capacity)
                throw new ArgumentOutOfRangeException(nameof(recordCount));
            if (vertexDestinationBase < 0)
                throw new ArgumentOutOfRangeException(nameof(vertexDestinationBase));
            if (indexDestinationBase < 0)
                throw new ArgumentOutOfRangeException(nameof(indexDestinationBase));
            _shader.SetInt(IdBatchVertexDestinationBase, vertexDestinationBase);
            _shader.SetInt(IdBatchIndexDestinationBase, indexDestinationBase);
            _shader.SetInt(IdBatchPagedOutput, pageArena != null ? 1 : 0);

            BindBatchShared(_batchWriteKernel, mirror, tables, batchCounters, resources);
            BindBatchShared(_batchWriteFacetedKernel, mirror, tables,
                            batchCounters, resources);
            BindBatchShared(_batchWriteDecorationsKernel, mirror, tables,
                            batchCounters, resources);
            BindBatchShared(_batchWriteTransitionsKernel, mirror, tables,
                            batchCounters, resources);
            BindBatchShared(_batchWriteProfilesKernel, mirror, tables,
                            batchCounters, resources);
            BindTransitionTables(_batchWriteTransitionsKernel, tables);
            _shader.SetBuffer(_batchWriteKernel, IdBatchVertices, vertices);
            _shader.SetBuffer(_batchWriteKernel, IdBatchIndices, indices);
            _shader.SetBuffer(_batchWriteFacetedKernel, IdBatchVertices, vertices);
            _shader.SetBuffer(_batchWriteFacetedKernel, IdBatchIndices, indices);
            _shader.SetBuffer(_batchWriteDecorationsKernel, IdBatchVertices, vertices);
            _shader.SetBuffer(_batchWriteDecorationsKernel, IdBatchIndices, indices);
            _shader.SetBuffer(_batchWriteTransitionsKernel, IdBatchVertices, vertices);
            _shader.SetBuffer(_batchWriteTransitionsKernel, IdBatchIndices, indices);
            _shader.SetBuffer(_batchWriteProfilesKernel, IdBatchVertices, vertices);
            _shader.SetBuffer(_batchWriteProfilesKernel, IdBatchIndices, indices);
            if (pageArena != null)
            {
                int[] pagedKernels =
                {
                    _batchWriteKernel, _batchWriteFacetedKernel,
                    _batchWriteDecorationsKernel, _batchWriteTransitionsKernel,
                    _batchWriteProfilesKernel
                };
                foreach (int kernel in pagedKernels)
                {
                    _shader.SetBuffer(kernel, IdBatchVertexPageTable,
                                      pageArena.VertexPageTable);
                    _shader.SetBuffer(kernel, IdBatchIndexPageTable,
                                      pageArena.IndexPageTable);
                }
                _shader.SetInt(IdBatchVertexPageSize, GpuSurfacePageArena.VertexPageSize);
                _shader.SetInt(IdBatchIndexPageSize, GpuSurfacePageArena.IndexPageSize);
                _shader.SetInt(IdBatchMaxVertexPages,
                               GpuSurfacePageArena.MaxVertexPagesPerChunk);
                _shader.SetInt(IdBatchMaxIndexPages,
                               GpuSurfacePageArena.MaxIndexPagesPerChunk);
            }
            int regularAxis = CellsPerAxis + 1;
            _shader.Dispatch(_batchWriteKernel,
                             Groups(regularAxis * regularAxis * regularAxis), recordCount, 1);
            _shader.Dispatch(_batchWriteFacetedKernel,
                             Groups(CellsPerAxis * CellsPerAxis * CellsPerAxis), recordCount, 1);
            _shader.Dispatch(_batchWriteDecorationsKernel,
                             Groups(CellsPerAxis * CellsPerAxis * CellsPerAxis), recordCount, 1);
            _shader.Dispatch(_batchWriteTransitionsKernel,
                             Groups(CellsPerAxis * CellsPerAxis), recordCount * 6, 1);
            if (resources.ProfileCount > 0)
                _shader.Dispatch(_batchWriteProfilesKernel,
                                 Groups(resources.ProfileCount * 24), 1, 1);
            if (args != null)
            {
                _shader.SetBuffer(_batchPublishArgsKernel, IdBatchCounters, batchCounters);
                _shader.SetBuffer(_batchPublishArgsKernel, IdBatchDrawArgs, args);
                _shader.SetInt(IdBatchRecordCount, recordCount);
                _shader.SetInt(IdBatchArgsWordStart, argsWordStart);
                _shader.Dispatch(_batchPublishArgsKernel, Groups(recordCount), 1, 1);
            }
            if (pageArena != null)
                pageArena.PublishBatch(resources.Chunks, batchCounters,
                                       recordCount, BatchRecordWords, frame);
        }

        internal void PrefixCountBatch(ComputeBuffer batchCounters, int recordCount,
                                       int vertexAlignment, int indexAlignment)
        {
            // A context may be cancelled after its count record was copied but before the shared
            // lane seals. Prefixing uses only the still-live shader asset and lane buffer, so it
            // deliberately remains valid after this extractor's private buffers are disposed.
            if (batchCounters == null) throw new ArgumentNullException(nameof(batchCounters));
            if (recordCount <= 0
                || BatchHeaderWords + recordCount * BatchRecordWords > batchCounters.count)
                throw new ArgumentOutOfRangeException(nameof(recordCount));
            if (vertexAlignment <= 0) throw new ArgumentOutOfRangeException(nameof(vertexAlignment));
            if (indexAlignment <= 0) throw new ArgumentOutOfRangeException(nameof(indexAlignment));
            _shader.SetBuffer(_prefixBatchCountsKernel, IdBatchCounters, batchCounters);
            _shader.SetInt(IdBatchRecordCount, recordCount);
            _shader.SetInt(IdBatchVertexAlignment, vertexAlignment);
            _shader.SetInt(IdBatchIndexAlignment, indexAlignment);
            _shader.Dispatch(_prefixBatchCountsKernel, 1, 1, 1);
        }

        internal void DispatchCountToBatch(GpuVoxelBrickMirror mirror,
                                           GpuTransvoxelTables tables,
                                           in GpuChunkExtraction request,
                                           ComputeBuffer batchCounters,
                                           int recordIndex)
        {
            DispatchCount(mirror, tables, request);
            CopyCountToBatch(batchCounters, recordIndex);
        }

        /// <summary>
        /// Records one chunk into a shared count command buffer. Parameters and bindings are
        /// command-buffer state, so later chunks cannot overwrite this chunk before execution.
        /// The coordinator submits the entire lane once, allowing Unity/Metal to encode many
        /// chunks without opening a compute encoder and upload encoder for every dispatch.
        /// </summary>
        internal void RecordCountToBatch(CommandBuffer commands,
                                         GpuVoxelBrickMirror mirror,
                                         GpuTransvoxelTables tables,
                                         in GpuChunkExtraction request,
                                         ComputeBuffer batchCounters,
                                         int recordIndex)
        {
            ThrowIfDisposed();
            if (commands == null) throw new ArgumentNullException(nameof(commands));
            if (mirror == null) throw new ArgumentNullException(nameof(mirror));
            if (tables == null) throw new ArgumentNullException(nameof(tables));
            if (batchCounters == null) throw new ArgumentNullException(nameof(batchCounters));

            int wordStart = BatchHeaderWords + recordIndex * BatchRecordWords;
            if (recordIndex < 0 || wordStart + 4 > batchCounters.count)
                throw new ArgumentOutOfRangeException(nameof(recordIndex));

            _brickCache.SetData(_brickCacheStaging);
            ResetCounters();
            RecordChunkUniforms(commands, request.ChunkOriginVoxel, request.BrickCacheOrigin,
                                request.SourceStep, request.VoxelSize, 0, 0);

            RecordBindShared(commands, _sampleKernel, mirror, tables);
            RecordBindShared(commands, _countKernel, mirror, tables);
            RecordBindShared(commands, _countFacetedKernel, mirror, tables);
            RecordBindShared(commands, _countDecorationsKernel, mirror, tables);
            commands.SetComputeBufferParam(_shader, _sampleKernel, IdDensityWrite, _density);
            commands.SetComputeBufferParam(_shader, _sampleKernel, IdSampleMaterialWrite,
                                           _sampleMaterial);
            commands.SetComputeBufferParam(_shader, _sampleKernel, IdSampleSurfaceWrite,
                                           _sampleSurface);
            commands.SetComputeBufferParam(_shader, _sampleKernel, IdSampleBoundaryWrite,
                                           _sampleBoundary);
            commands.SetComputeBufferParam(_shader, _countKernel, IdCellVertexCountsWrite,
                                           _cellVertexCounts);
            commands.SetComputeBufferParam(_shader, _countKernel, IdCellTriangleCountsWrite,
                                           _cellTriangleCounts);
            commands.SetComputeBufferParam(_shader, _countKernel,
                                           IdCellReconstructionFlagsWrite,
                                           _cellReconstructionFlags);

            int samples = GridSize * GridSize * GridSize;
            int regularCellsPerAxis = CellsPerAxis + 1;
            int cells = regularCellsPerAxis * regularCellsPerAxis * regularCellsPerAxis;
            int semanticCells = CellsPerAxis * CellsPerAxis * CellsPerAxis;
            commands.DispatchCompute(_shader, _sampleKernel, Groups(samples), 1, 1);
            commands.DispatchCompute(_shader, _countKernel, Groups(cells), 1, 1);
            commands.DispatchCompute(_shader, _countFacetedKernel, Groups(semanticCells), 1, 1);
            commands.DispatchCompute(_shader, _countDecorationsKernel, Groups(semanticCells), 1, 1);
            RecordTransitionFaces(commands, mirror, tables, request, countOnly: true);

            commands.SetComputeBufferParam(_shader, _copyCountersToBatchKernel,
                                           IdCounters, _counters);
            commands.SetComputeBufferParam(_shader, _copyCountersToBatchKernel,
                                           IdBatchCounters, batchCounters);
            commands.SetComputeIntParam(_shader, IdBatchCounterWordStart, wordStart);
            commands.DispatchCompute(_shader, _copyCountersToBatchKernel, 1, 1, 1);
        }

        internal void RecordPrefixCountBatch(CommandBuffer commands,
                                             ComputeBuffer batchCounters,
                                             int recordCount,
                                             int vertexAlignment,
                                             int indexAlignment)
        {
            if (commands == null) throw new ArgumentNullException(nameof(commands));
            if (batchCounters == null) throw new ArgumentNullException(nameof(batchCounters));
            if (recordCount <= 0
                || BatchHeaderWords + recordCount * BatchRecordWords > batchCounters.count)
                throw new ArgumentOutOfRangeException(nameof(recordCount));
            commands.SetComputeBufferParam(_shader, _prefixBatchCountsKernel,
                                           IdBatchCounters, batchCounters);
            commands.SetComputeIntParam(_shader, IdBatchRecordCount, recordCount);
            commands.SetComputeIntParam(_shader, IdBatchVertexAlignment, vertexAlignment);
            commands.SetComputeIntParam(_shader, IdBatchIndexAlignment, indexAlignment);
            commands.DispatchCompute(_shader, _prefixBatchCountsKernel, 1, 1, 1);
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
            BindShared(_countFacetedKernel, mirror, tables);
            BindShared(_countDecorationsKernel, mirror, tables);
            _shader.SetBuffer(_sampleKernel, IdDensityWrite, _density);
            _shader.SetBuffer(_sampleKernel, IdSampleMaterialWrite, _sampleMaterial);
            _shader.SetBuffer(_sampleKernel, IdSampleSurfaceWrite, _sampleSurface);
            _shader.SetBuffer(_sampleKernel, IdSampleBoundaryWrite, _sampleBoundary);
            _shader.SetBuffer(_countKernel, IdCellVertexCountsWrite, _cellVertexCounts);
            _shader.SetBuffer(_countKernel, IdCellTriangleCountsWrite, _cellTriangleCounts);
            _shader.SetBuffer(_countKernel, IdCellReconstructionFlagsWrite,
                              _cellReconstructionFlags);

            int samples = GridSize * GridSize * GridSize;
            int regularCellsPerAxis = CellsPerAxis + 1;
            int cells = regularCellsPerAxis * regularCellsPerAxis * regularCellsPerAxis;
            _shader.Dispatch(_sampleKernel, Groups(samples), 1, 1);
            _shader.Dispatch(_countKernel, Groups(cells), 1, 1);
            int semanticCells = CellsPerAxis * CellsPerAxis * CellsPerAxis;
            _shader.Dispatch(_countFacetedKernel, Groups(semanticCells), 1, 1);
            _shader.Dispatch(_countDecorationsKernel, Groups(semanticCells), 1, 1);

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
            BindShared(_writeFacetedKernel, mirror, tables);
            BindShared(_writeDecorationsKernel, mirror, tables);
            _shader.SetBuffer(_writeKernel, IdVertices, vertices);
            _shader.SetBuffer(_writeKernel, IdIndices, indices);
            _shader.SetBuffer(_writeFacetedKernel, IdVertices, vertices);
            _shader.SetBuffer(_writeFacetedKernel, IdIndices, indices);
            _shader.SetBuffer(_writeDecorationsKernel, IdVertices, vertices);
            _shader.SetBuffer(_writeDecorationsKernel, IdIndices, indices);

            int regularCellsPerAxis = CellsPerAxis + 1;
            int cells = regularCellsPerAxis * regularCellsPerAxis * regularCellsPerAxis;
            _shader.Dispatch(_writeKernel, Groups(cells), 1, 1);
            int semanticCells = CellsPerAxis * CellsPerAxis * CellsPerAxis;
            _shader.Dispatch(_writeFacetedKernel, Groups(semanticCells), 1, 1);
            _shader.Dispatch(_writeDecorationsKernel, Groups(semanticCells), 1, 1);

            DispatchTransitionFaces(mirror, tables, request, countOnly: false,
                                    vertices, indices);

            return ReadCounters(vertexCapacity, indexCapacity);
        }

        /// <summary>
        /// Writes the chunk into a plain contiguous range someone else allocated.
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
        /// Oracle-only asynchronous write verification. Production uses
        /// <see cref="WriteRangeToScratch"/> and an explicit copy fence instead.
        /// </summary>
        public void BeginWriteRange(GpuVoxelBrickMirror mirror, GpuTransvoxelTables tables,
                                    in GpuChunkExtraction request,
                                    ComputeBuffer vertices, ComputeBuffer indices,
                                    int vertexStart, int vertexCapacity,
                                    int indexStart, int indexCapacity)
        {
            // Write into private scratch first so verification never touches a live arena range.
            EnsureWriteScratch(vertexCapacity, indexCapacity);
            DispatchWriteRange(mirror, tables, request,
                               _writeScratchVertices, _writeScratchIndices,
                               0, vertexCapacity, 0, indexCapacity);
            RequestCounters();
        }

        /// <summary>
        /// Queues a count-reserved production write into private scratch without requesting a
        /// second counter readback. The caller orders copy/publication with a graphics fence.
        /// Count/write equality remains covered by <see cref="WriteRange"/> and GPU oracle tests.
        /// </summary>
        public void WriteRangeToScratch(GpuVoxelBrickMirror mirror, GpuTransvoxelTables tables,
                                        in GpuChunkExtraction request,
                                        int vertexCapacity, int indexCapacity)
        {
            EnsureWriteScratch(vertexCapacity, indexCapacity);
            DispatchWriteRange(mirror, tables, request,
                               _writeScratchVertices, _writeScratchIndices,
                               0, vertexCapacity, 0, indexCapacity);
        }

        /// <summary>
        /// Records generation, arena copies, and args publication as one ordered submission.
        /// Production uses this path so Unity does not create a Metal encoder/state-upload pair
        /// for every kernel in the chain.
        /// </summary>
        public void WriteScratchCopyAndPublish(
            GpuVoxelBrickMirror mirror, GpuTransvoxelTables tables,
            in GpuChunkExtraction request,
            int vertexCapacity, int indexCapacity,
            ComputeBuffer vertices, ComputeBuffer indices,
            ComputeBuffer args, int argsWordStart,
            int vertexStart, int vertexCount,
            int indexStart, int indexCount)
        {
            ThrowIfDisposed();
            EnsureWriteScratch(vertexCapacity, indexCapacity);
            _productionCommands.Clear();
            RecordWriteRange(_productionCommands, mirror, tables, request,
                             _writeScratchVertices, _writeScratchIndices,
                             0, vertexCapacity, 0, indexCapacity);
            RecordCopyAndPublish(_productionCommands, vertices, indices, args, argsWordStart,
                                 vertexStart, vertexCount, indexStart, indexCount);
            Graphics.ExecuteCommandBuffer(_productionCommands);
            _productionCommands.Clear();
        }

        public void PublishEmpty(ComputeBuffer args, int argsWordStart)
        {
            ThrowIfDisposed();
            _productionCommands.Clear();
            RecordCopyAndPublish(_productionCommands, null, null, args, argsWordStart,
                                 0, 0, 0, 0);
            Graphics.ExecuteCommandBuffer(_productionCommands);
            _productionCommands.Clear();
        }

        public void CopyCompletedWriteRange(ComputeBuffer vertices, ComputeBuffer indices,
                                            ComputeBuffer args, int argsWordStart,
                                            int vertexStart, int vertexCount,
                                            int indexStart, int indexCount)
        {
            ThrowIfDisposed();
            if (vertices == null) throw new ArgumentNullException(nameof(vertices));
            if (indices == null) throw new ArgumentNullException(nameof(indices));
            if (args == null) throw new ArgumentNullException(nameof(args));
            if (argsWordStart < 0) throw new ArgumentOutOfRangeException(nameof(argsWordStart));
            if (vertexStart < 0) throw new ArgumentOutOfRangeException(nameof(vertexStart));
            if (indexStart < 0) throw new ArgumentOutOfRangeException(nameof(indexStart));
            if (vertexCount < 0 || vertexCount > _writeScratchVertexCapacity)
                throw new ArgumentOutOfRangeException(nameof(vertexCount));
            if (indexCount < 0 || indexCount > _writeScratchIndexCapacity)
                throw new ArgumentOutOfRangeException(nameof(indexCount));

            if (vertexCount > 0)
            {
                _shader.SetBuffer(_copyVerticesKernel, IdCopyVerticesSource,
                                  _writeScratchVertices);
                _shader.SetBuffer(_copyVerticesKernel, IdCopyVerticesDestination, vertices);
                _shader.SetInt(IdCopyVertexDestinationBase, vertexStart);
                _shader.SetInt(IdCopyVertexCount, vertexCount);
                _shader.Dispatch(_copyVerticesKernel, Groups(vertexCount), 1, 1);
            }
            if (indexCount > 0)
            {
                _shader.SetBuffer(_copyIndicesKernel, IdCopyIndicesSource, _writeScratchIndices);
                _shader.SetBuffer(_copyIndicesKernel, IdCopyIndicesDestination, indices);
                _shader.SetInt(IdCopyIndexDestinationBase, indexStart);
                _shader.SetInt(IdCopyIndexCount, indexCount);
                _shader.Dispatch(_copyIndicesKernel, Groups(indexCount), 1, 1);
            }
            _shader.SetBuffer(_publishArgsKernel, IdDrawArgs, args);
            _shader.SetInt(IdDrawArgsWordStart, argsWordStart);
            _shader.SetInt(IdCopyIndexCount, indexCount);
            _shader.Dispatch(_publishArgsKernel, 1, 1, 1);
        }

        private void EnsureWriteScratch(int vertexCapacity, int indexCapacity)
        {
            if (_writeScratchVertexCapacity < vertexCapacity)
            {
                int capacity = GrowCapacity(_writeScratchVertexCapacity, vertexCapacity);
                _writeScratchVertices?.Release();
                _writeScratchVertices = new ComputeBuffer(
                    capacity, ReadbackVertex.Stride, ComputeBufferType.Structured);
                _writeScratchVertexCapacity = capacity;
            }
            if (_writeScratchIndexCapacity < indexCapacity)
            {
                int capacity = GrowCapacity(_writeScratchIndexCapacity, indexCapacity);
                _writeScratchIndices?.Release();
                _writeScratchIndices = new ComputeBuffer(
                    capacity, sizeof(uint), ComputeBufferType.Structured);
                _writeScratchIndexCapacity = capacity;
            }
        }

        private void RecordWriteRange(CommandBuffer commands,
                                      GpuVoxelBrickMirror mirror,
                                      GpuTransvoxelTables tables,
                                      in GpuChunkExtraction request,
                                      ComputeBuffer vertices, ComputeBuffer indices,
                                      int vertexStart, int vertexCapacity,
                                      int indexStart, int indexCapacity)
        {
            if (commands == null) throw new ArgumentNullException(nameof(commands));
            if (mirror == null) throw new ArgumentNullException(nameof(mirror));
            if (tables == null) throw new ArgumentNullException(nameof(tables));
            if (vertices == null) throw new ArgumentNullException(nameof(vertices));
            if (indices == null) throw new ArgumentNullException(nameof(indices));
            if (vertexCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(vertexCapacity));
            if (indexCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(indexCapacity));

            ResetCounters();
            _pageStaging[0] = 0;
            _chunkPages.SetData(_pageStaging, 0, 0, 1);
            commands.SetComputeIntParam(_shader, IdVerticesPerPage,
                                        Math.Max(1, vertexCapacity));
            commands.SetComputeIntParam(_shader, IdIndicesPerPage,
                                        Math.Max(1, indexCapacity));
            commands.SetComputeIntParam(_shader, IdVertexWriteBase, vertexStart);
            commands.SetComputeIntParam(_shader, IdIndexWriteBase, indexStart);
            RecordChunkUniforms(commands, request.ChunkOriginVoxel, request.BrickCacheOrigin,
                                request.SourceStep, request.VoxelSize,
                                vertexCapacity, indexCapacity);

            RecordBindShared(commands, _writeKernel, mirror, tables);
            RecordBindShared(commands, _writeFacetedKernel, mirror, tables);
            RecordBindShared(commands, _writeDecorationsKernel, mirror, tables);
            commands.SetComputeBufferParam(_shader, _writeKernel, IdVertices, vertices);
            commands.SetComputeBufferParam(_shader, _writeKernel, IdIndices, indices);
            commands.SetComputeBufferParam(_shader, _writeFacetedKernel, IdVertices, vertices);
            commands.SetComputeBufferParam(_shader, _writeFacetedKernel, IdIndices, indices);
            commands.SetComputeBufferParam(_shader, _writeDecorationsKernel, IdVertices, vertices);
            commands.SetComputeBufferParam(_shader, _writeDecorationsKernel, IdIndices, indices);

            int regularCellsPerAxis = CellsPerAxis + 1;
            int cells = regularCellsPerAxis * regularCellsPerAxis * regularCellsPerAxis;
            int semanticCells = CellsPerAxis * CellsPerAxis * CellsPerAxis;
            commands.DispatchCompute(_shader, _writeKernel, Groups(cells), 1, 1);
            commands.DispatchCompute(_shader, _writeFacetedKernel, Groups(semanticCells), 1, 1);
            commands.DispatchCompute(_shader, _writeDecorationsKernel, Groups(semanticCells), 1, 1);
            RecordTransitionFaces(commands, mirror, tables, request, countOnly: false,
                                  vertices, indices);
        }

        private void RecordCopyAndPublish(CommandBuffer commands,
                                          ComputeBuffer vertices, ComputeBuffer indices,
                                          ComputeBuffer args, int argsWordStart,
                                          int vertexStart, int vertexCount,
                                          int indexStart, int indexCount)
        {
            if (commands == null) throw new ArgumentNullException(nameof(commands));
            if (args == null) throw new ArgumentNullException(nameof(args));
            if (vertexCount > 0)
            {
                if (vertices == null) throw new ArgumentNullException(nameof(vertices));
                commands.SetComputeBufferParam(_shader, _copyVerticesKernel,
                                               IdCopyVerticesSource, _writeScratchVertices);
                commands.SetComputeBufferParam(_shader, _copyVerticesKernel,
                                               IdCopyVerticesDestination, vertices);
                commands.SetComputeIntParam(_shader, IdCopyVertexDestinationBase, vertexStart);
                commands.SetComputeIntParam(_shader, IdCopyVertexCount, vertexCount);
                commands.DispatchCompute(_shader, _copyVerticesKernel, Groups(vertexCount), 1, 1);
            }
            if (indexCount > 0)
            {
                if (indices == null) throw new ArgumentNullException(nameof(indices));
                commands.SetComputeBufferParam(_shader, _copyIndicesKernel,
                                               IdCopyIndicesSource, _writeScratchIndices);
                commands.SetComputeBufferParam(_shader, _copyIndicesKernel,
                                               IdCopyIndicesDestination, indices);
                commands.SetComputeIntParam(_shader, IdCopyIndexDestinationBase, indexStart);
                commands.SetComputeIntParam(_shader, IdCopyIndexCount, indexCount);
                commands.DispatchCompute(_shader, _copyIndicesKernel, Groups(indexCount), 1, 1);
            }
            commands.SetComputeBufferParam(_shader, _publishArgsKernel, IdDrawArgs, args);
            commands.SetComputeIntParam(_shader, IdDrawArgsWordStart, argsWordStart);
            commands.SetComputeIntParam(_shader, IdCopyIndexCount, indexCount);
            commands.DispatchCompute(_shader, _publishArgsKernel, 1, 1, 1);
        }

        private static int GrowCapacity(int current, int required)
        {
            int capacity = Math.Max(256, current);
            while (capacity < required)
            {
                if (capacity > int.MaxValue / 2) return required;
                capacity *= 2;
            }
            return capacity;
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
            BindShared(_writeFacetedKernel, mirror, tables);
            BindShared(_writeDecorationsKernel, mirror, tables);
            _shader.SetBuffer(_writeKernel, IdVertices, vertices);
            _shader.SetBuffer(_writeKernel, IdIndices, indices);
            _shader.SetBuffer(_writeFacetedKernel, IdVertices, vertices);
            _shader.SetBuffer(_writeFacetedKernel, IdIndices, indices);
            _shader.SetBuffer(_writeDecorationsKernel, IdVertices, vertices);
            _shader.SetBuffer(_writeDecorationsKernel, IdIndices, indices);

            int regularCellsPerAxis = CellsPerAxis + 1;
            int cells = regularCellsPerAxis * regularCellsPerAxis * regularCellsPerAxis;
            _shader.Dispatch(_writeKernel, Groups(cells), 1, 1);
            int semanticCells = CellsPerAxis * CellsPerAxis * CellsPerAxis;
            _shader.Dispatch(_writeFacetedKernel, Groups(semanticCells), 1, 1);
            _shader.Dispatch(_writeDecorationsKernel, Groups(semanticCells), 1, 1);

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
            counts = new GpuExtractionCounts((int)_counterStaging[2], (int)_counterStaging[3],
                                             _counterStaging[0]);
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

        public void ReadSampleSurfaces(uint[] surfaces)
        {
            GeometryReadbacks++;
            _sampleSurface.GetData(surfaces);
        }

        public void ReadSampleBoundaries(uint[] boundaries)
        {
            GeometryReadbacks++;
            _sampleBoundary.GetData(boundaries);
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
            _shader.SetBuffer(kernel, IdCellReconstructionFlags, _cellReconstructionFlags);
            _shader.SetBuffer(kernel, IdCounters, _counters);
            _shader.SetBuffer(kernel, IdChunkPages, _chunkPages);
        }

        private void BindBatchShared(int kernel,
                                     GpuVoxelBrickMirror mirror,
                                     GpuTransvoxelTables tables,
                                     ComputeBuffer batchCounters,
                                     CountBatchResources resources)
        {
            _shader.SetBuffer(kernel, IdBrickMaterials, mirror.Materials);
            _shader.SetBuffer(kernel, IdBrickSurface, mirror.SurfaceSemantics);
            _shader.SetBuffer(kernel, IdBrickBoundary, mirror.BoundarySamples);
            _shader.SetBuffer(kernel, IdBrickCache, resources.PreparedCache.DenseEntries);
            _shader.SetBuffer(kernel, IdBatchBrickCacheViews, resources.PreparedCache.RequestViews);
            _shader.SetBuffer(kernel, IdStyleWords, _styleWords);
            _shader.SetBuffer(kernel, IdJoinWords, _joinWords);
            _shader.SetBuffer(kernel, IdCoatingWords, _coatingWords);
            _shader.SetBuffer(kernel, IdDefaultStyle, _defaultStyle);
            _shader.SetBuffer(kernel, IdCellClass, tables.CellClass);
            _shader.SetBuffer(kernel, IdGeometryCounts, tables.GeometryCounts);
            _shader.SetBuffer(kernel, IdCellIndices, tables.CellIndices);
            _shader.SetBuffer(kernel, IdEdgeCodes, tables.EdgeCodes);
            _shader.SetBuffer(kernel, IdBatchCounters, batchCounters);
            _shader.SetBuffer(kernel, IdBatchChunks, resources.Chunks);
            _shader.SetBuffer(kernel, IdBatchDensity, resources.Density);
            _shader.SetBuffer(kernel, IdBatchDensityWrite, resources.Density);
            _shader.SetBuffer(kernel, IdBatchSampleMaterial, resources.SampleMaterial);
            _shader.SetBuffer(kernel, IdBatchSampleMaterialWrite, resources.SampleMaterial);
            _shader.SetBuffer(kernel, IdBatchSampleSurface, resources.SampleSurface);
            _shader.SetBuffer(kernel, IdBatchSampleSurfaceWrite, resources.SampleSurface);
            _shader.SetBuffer(kernel, IdBatchSampleBoundary, resources.SampleBoundary);
            _shader.SetBuffer(kernel, IdBatchSampleBoundaryWrite, resources.SampleBoundary);
            _shader.SetBuffer(kernel, IdBatchCellVertexCounts, resources.CellVertexCounts);
            _shader.SetBuffer(kernel, IdBatchCellVertexCountsWrite, resources.CellVertexCounts);
            _shader.SetBuffer(kernel, IdBatchCellTriangleCounts, resources.CellTriangleCounts);
            _shader.SetBuffer(kernel, IdBatchCellTriangleCountsWrite,
                              resources.CellTriangleCounts);
            _shader.SetBuffer(kernel, IdBatchCellReconstructionFlags,
                              resources.CellReconstructionFlags);
            _shader.SetBuffer(kernel, IdBatchCellReconstructionFlagsWrite,
                              resources.CellReconstructionFlags);
            _shader.SetBuffer(kernel, IdBatchFaceDensity, resources.FaceDensity);
            _shader.SetBuffer(kernel, IdBatchFaceDensityWrite, resources.FaceDensity);
            _shader.SetBuffer(kernel, IdBatchFaceMaterial, resources.FaceMaterial);
            _shader.SetBuffer(kernel, IdBatchFaceMaterialWrite, resources.FaceMaterial);
            _shader.SetBuffer(kernel, IdBatchFaceSurface, resources.FaceSurface);
            _shader.SetBuffer(kernel, IdBatchFaceSurfaceWrite, resources.FaceSurface);
            _shader.SetBuffer(kernel, IdBatchProfiles, resources.Profiles);
            _shader.SetInt(IdBatchProfileCount, resources.ProfileCount);
            // Both branches of the paged-output selector are present in the compiled write
            // kernels. Bind harmless uint SRVs for contiguous oracle paths; a page arena replaces
            // them before paged production dispatch.
            _shader.SetBuffer(kernel, IdBatchVertexPageTable, resources.CellVertexCounts);
            _shader.SetBuffer(kernel, IdBatchIndexPageTable, resources.CellTriangleCounts);
        }

        private void RecordChunkUniforms(CommandBuffer commands,
                                         int3 chunkOriginVoxel,
                                         int3 brickCacheOrigin,
                                         int sourceStep, float voxelSize,
                                         int vertexCapacity, int indexCapacity)
        {
            _int3Staging[0] = chunkOriginVoxel.x;
            _int3Staging[1] = chunkOriginVoxel.y;
            _int3Staging[2] = chunkOriginVoxel.z;
            commands.SetComputeIntParams(_shader, IdChunkOrigin, _int3Staging);
            _int3Staging[0] = brickCacheOrigin.x;
            _int3Staging[1] = brickCacheOrigin.y;
            _int3Staging[2] = brickCacheOrigin.z;
            commands.SetComputeIntParams(_shader, IdBrickCacheOrigin, _int3Staging);
            commands.SetComputeIntParam(_shader, IdBrickCacheEdge, BrickCacheEdge);
            commands.SetComputeIntParam(_shader, IdCellsPerAxis, CellsPerAxis);
            commands.SetComputeIntParam(_shader, IdGridSize, GridSize);
            commands.SetComputeIntParam(_shader, IdPadding, Padding);
            commands.SetComputeIntParam(_shader, IdSourceStep, sourceStep);
            commands.SetComputeFloatParam(_shader, IdVoxelSize, voxelSize);
            commands.SetComputeIntParam(_shader, IdVertexCapacity, vertexCapacity);
            commands.SetComputeIntParam(_shader, IdIndexCapacity, indexCapacity);
        }

        private void RecordBindTransitionTables(CommandBuffer commands, int kernel,
                                                GpuTransvoxelTables tables)
        {
            commands.SetComputeBufferParam(_shader, kernel, IdTransitionCellClass,
                                           tables.TransitionCellClass);
            commands.SetComputeBufferParam(_shader, kernel, IdTransitionGeometryCounts,
                                           tables.TransitionGeometryCounts);
            commands.SetComputeBufferParam(_shader, kernel, IdTransitionCellIndices,
                                           tables.TransitionCellIndices);
            commands.SetComputeBufferParam(_shader, kernel, IdTransitionVertexData,
                                           tables.TransitionVertexData);
            commands.SetComputeIntParam(_shader, IdTransitionVertexStride,
                                        tables.TransitionVertexStride);
            commands.SetComputeIntParam(_shader, IdTransitionIndexStride,
                                        tables.TransitionIndexStride);
        }

        private void RecordBindShared(CommandBuffer commands, int kernel,
                                      GpuVoxelBrickMirror mirror,
                                      GpuTransvoxelTables tables)
        {
            commands.SetComputeBufferParam(_shader, kernel, IdDensity, _density);
            commands.SetComputeBufferParam(_shader, kernel, IdSampleMaterial, _sampleMaterial);
            commands.SetComputeBufferParam(_shader, kernel, IdSampleSurface, _sampleSurface);
            commands.SetComputeBufferParam(_shader, kernel, IdSampleBoundary, _sampleBoundary);
            commands.SetComputeBufferParam(_shader, kernel, IdBrickMaterials, mirror.Materials);
            commands.SetComputeBufferParam(_shader, kernel, IdBrickSurface,
                                           mirror.SurfaceSemantics);
            commands.SetComputeBufferParam(_shader, kernel, IdBrickBoundary,
                                           mirror.BoundarySamples);
            commands.SetComputeBufferParam(_shader, kernel, IdBrickCache, _brickCache);
            commands.SetComputeBufferParam(_shader, kernel, IdStyleWords, _styleWords);
            commands.SetComputeBufferParam(_shader, kernel, IdJoinWords, _joinWords);
            commands.SetComputeBufferParam(_shader, kernel, IdCoatingWords, _coatingWords);
            commands.SetComputeBufferParam(_shader, kernel, IdDefaultStyle, _defaultStyle);
            commands.SetComputeBufferParam(_shader, kernel, IdCellClass, tables.CellClass);
            commands.SetComputeBufferParam(_shader, kernel, IdGeometryCounts,
                                           tables.GeometryCounts);
            commands.SetComputeBufferParam(_shader, kernel, IdCellIndices, tables.CellIndices);
            commands.SetComputeBufferParam(_shader, kernel, IdEdgeCodes, tables.EdgeCodes);
            commands.SetComputeBufferParam(_shader, kernel, IdCellVertexCounts,
                                           _cellVertexCounts);
            commands.SetComputeBufferParam(_shader, kernel, IdCellTriangleCounts,
                                           _cellTriangleCounts);
            commands.SetComputeBufferParam(_shader, kernel, IdCellReconstructionFlags,
                                           _cellReconstructionFlags);
            commands.SetComputeBufferParam(_shader, kernel, IdCounters, _counters);
            commands.SetComputeBufferParam(_shader, kernel, IdChunkPages, _chunkPages);
        }

        private void RecordTransitionFaces(CommandBuffer commands,
                                           GpuVoxelBrickMirror mirror,
                                           GpuTransvoxelTables tables,
                                           in GpuChunkExtraction request,
                                           bool countOnly,
                                           ComputeBuffer vertices = null,
                                           ComputeBuffer indices = null)
        {
            if (request.TransitionFaceMask == 0) return;

            commands.SetComputeIntParam(_shader, IdFaceSamplesPerAxis, FaceSamplesPerAxis);
            commands.SetComputeIntParam(_shader, IdTransitionCountOnly, countOnly ? 1 : 0);
            RecordBindShared(commands, _faceKernel, mirror, tables);
            RecordBindShared(commands, _transitionKernel, mirror, tables);
            RecordBindTransitionTables(commands, _transitionKernel, tables);
            commands.SetComputeBufferParam(_shader, _faceKernel, IdFaceDensityWrite,
                                           _faceDensity);
            commands.SetComputeBufferParam(_shader, _faceKernel, IdFaceMaterialWrite,
                                           _faceMaterial);
            commands.SetComputeBufferParam(_shader, _faceKernel, IdFaceSurfaceWrite,
                                           _faceSurface);
            commands.SetComputeBufferParam(_shader, _transitionKernel, IdFaceDensity,
                                           _faceDensity);
            commands.SetComputeBufferParam(_shader, _transitionKernel, IdFaceMaterial,
                                           _faceMaterial);
            commands.SetComputeBufferParam(_shader, _transitionKernel, IdFaceSurface,
                                           _faceSurface);
            commands.SetComputeBufferParam(_shader, _transitionKernel, IdVertices,
                vertices != null ? vertices : _transitionSink);
            commands.SetComputeBufferParam(_shader, _transitionKernel, IdIndices,
                indices != null ? indices : _transitionIndexSink);

            for (int face = 0; face < 6; face++)
            {
                if ((request.TransitionFaceMask & (1 << face)) == 0) continue;
                commands.SetComputeIntParam(_shader, IdFace, face);
                commands.DispatchCompute(_shader, _faceKernel,
                    Groups(FaceSamplesPerAxis * FaceSamplesPerAxis), 1, 1);
                commands.DispatchCompute(_shader, _transitionKernel,
                    Groups(CellsPerAxis * CellsPerAxis), 1, 1);
            }
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
            _cellReconstructionFlags?.Release();
            _brickCache?.Release();
            _counters?.Release();
            _faceDensity?.Release();
            _faceMaterial?.Release();
            _faceSurface?.Release();
            _chunkPages?.Release();
            _transitionSink?.Release();
            _transitionIndexSink?.Release();
            _writeScratchVertices?.Release();
            _writeScratchIndices?.Release();
            _styleWords?.Release();
            _joinWords?.Release();
            _coatingWords?.Release();
            _defaultStyle?.Release();
            _productionCommands?.Release();
        }
    }
}
