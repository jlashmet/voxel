using System;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Rendering.Runtime.GpuVoxel
{
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

        private readonly ComputeShader _shader;
        private readonly int _sampleKernel;
        private readonly int _countKernel;
        private readonly int _writeKernel;

        private readonly ComputeBuffer _density;
        private readonly ComputeBuffer _sampleMaterial;
        private readonly ComputeBuffer _sampleSurface;
        private readonly ComputeBuffer _sampleBoundary;
        private readonly ComputeBuffer _cellVertexCounts;
        private readonly ComputeBuffer _cellTriangleCounts;
        private readonly ComputeBuffer _brickCache;
        private readonly ComputeBuffer _counters;

        private readonly ComputeBuffer _styleWords;
        private readonly ComputeBuffer _joinWords;
        private readonly ComputeBuffer _coatingWords;
        private readonly ComputeBuffer _defaultStyle;

        private readonly uint[] _counterStaging = new uint[2];
        private readonly uint[] _brickCacheStaging;
        private bool _disposed;

        public int CellsPerAxis { get; }
        public int Padding { get; }
        public int GridSize { get; }
        public int BrickCacheEdge { get; }

        public GpuSurfaceExtractor(ComputeShader shader, int cellsPerAxis, int padding = 2)
        {
            _shader = shader != null ? shader : throw new ArgumentNullException(nameof(shader));
            if (cellsPerAxis <= 0) throw new ArgumentOutOfRangeException(nameof(cellsPerAxis));
            if (padding < 2)
                throw new ArgumentOutOfRangeException(nameof(padding),
                    "Density taps reach two voxels out, so the grid needs at least that much skirt.");

            CellsPerAxis = cellsPerAxis;
            Padding = padding;
            GridSize = cellsPerAxis + padding * 2 + 1;

            _sampleKernel = shader.FindKernel("CSSampleDensity");
            _countKernel = shader.FindKernel("CSCountCells");
            _writeKernel = shader.FindKernel("CSWriteCells");

            int samples = GridSize * GridSize * GridSize;
            int cells = cellsPerAxis * cellsPerAxis * cellsPerAxis;

            _density = new ComputeBuffer(samples, sizeof(float), ComputeBufferType.Structured);
            _sampleMaterial = new ComputeBuffer(samples, sizeof(uint), ComputeBufferType.Structured);
            _sampleSurface = new ComputeBuffer(samples, sizeof(uint), ComputeBufferType.Structured);
            _sampleBoundary = new ComputeBuffer(samples, sizeof(uint), ComputeBufferType.Structured);
            _cellVertexCounts = new ComputeBuffer(cells, sizeof(uint), ComputeBufferType.Structured);
            _cellTriangleCounts = new ComputeBuffer(cells, sizeof(uint), ComputeBufferType.Structured);
            _counters = new ComputeBuffer(2, sizeof(uint), ComputeBufferType.Structured);

            // The neighbourhood spans the chunk's own bricks plus the padded skirt on both sides.
            int paddedVoxels = cellsPerAxis + padding * 2 + 1;
            BrickCacheEdge = paddedVoxels / VoxelReadGrid.BlockEdge + 3;
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
            _counterStaging[0] = 0;
            _counterStaging[1] = 0;
            _counters.SetData(_counterStaging);

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

            _counters.GetData(_counterStaging);
            int vertexCount = (int)_counterStaging[0];
            int indexCount = (int)_counterStaging[1];
            bool overflowed = vertexCount > vertexCapacity || indexCount > indexCapacity;
            return new GpuExtractionResult(Math.Min(vertexCount, vertexCapacity),
                                           Math.Min(indexCount, indexCapacity), overflowed);
        }

        /// <summary>Per-cell counts, for a caller sizing its reservation before writing.</summary>
        public void ReadCellCounts(uint[] vertexCounts, uint[] triangleCounts)
        {
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
        public void ReadDensity(float[] density) => _density.GetData(density);

        public void ReadSampleMaterials(uint[] materials) => _sampleMaterial.GetData(materials);

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
            _styleWords?.Release();
            _joinWords?.Release();
            _coatingWords?.Release();
            _defaultStyle?.Release();
        }
    }
}
