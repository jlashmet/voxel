using System;
using System.Collections.Generic;
using UnityEngine;

namespace VoxelEngine.Rendering.Runtime.GpuVoxel
{
    /// <summary>
    /// The GPU-resident geometry the compute mesher writes into, and the page bookkeeping that says
    /// which chunk owns what.
    ///
    /// Two buffers and one page table. Vertices and indices ride the same page list — page k holds
    /// <see cref="VerticesPerPage"/> vertices and <see cref="IndicesPerPage"/> indices — so a chunk
    /// reserves once and the index side can never need a page the vertex side did not already take.
    ///
    /// Sized once from the device budget and never resized. Growing this on demand would mean
    /// reallocating hundreds of megabytes at exactly the moment the view is busiest, which is the
    /// failure the fixed budget exists to prevent.
    /// </summary>
    public sealed class GpuGeometryArena : IDisposable
    {
        /// <summary>
        /// Index slots per vertex slot.
        ///
        /// Measured extraction sits near 1.75 indices per vertex; 2 is the next integer up, and a
        /// non-integer ratio would make the page arithmetic in the shader a division rather than a
        /// shift-friendly multiply. Overshooting costs index memory, which is a quarter the width of
        /// a vertex; undershooting costs a refused build.
        /// </summary>
        public const int DefaultIndicesPerVertex = 2;

        private readonly GpuMeshletPageArena _pages;
        private readonly ComputeBuffer _vertices;
        private readonly GraphicsBuffer _indices;
        private bool _disposed;

        public int PageCount => _pages.PageCount;
        public int VerticesPerPage => _pages.VerticesPerPage;
        public int IndicesPerPage { get; }
        public int MaxPagesPerChunk => _pages.MaxPagesPerChunk;

        public int FreePages => _pages.FreePages;
        public int ReservedPages => _pages.ReservedPages;
        public int ChunkCount => _pages.ChunkCount;
        public ulong ExhaustedCount => _pages.ExhaustedCount;
        public ulong RefusedTooLargeCount => _pages.RefusedTooLargeCount;

        public ComputeBuffer Vertices => _vertices;
        public GraphicsBuffer Indices => _indices;

        /// <summary>Bytes committed, so a caller can check itself against the device budget.</summary>
        public long CommittedBytes =>
            (long)PageCount * VerticesPerPage * VertexStrideBytes
          + (long)PageCount * IndicesPerPage * sizeof(uint);

        /// <summary>Bytes per vertex as the mesher writes it: position, normal, material, active.</summary>
        public const int VertexStrideBytes = sizeof(float) * 6 + sizeof(uint) * 2;

        public GpuGeometryArena(int pageCount,
                                int verticesPerPage = GpuMeshletPageArena.DefaultVerticesPerPage,
                                int maxPagesPerChunk = GpuMeshletPageArena.DefaultMaxPagesPerChunk,
                                int indicesPerVertex = DefaultIndicesPerVertex)
        {
            if (indicesPerVertex <= 0) throw new ArgumentOutOfRangeException(nameof(indicesPerVertex));

            _pages = new GpuMeshletPageArena(pageCount, verticesPerPage, maxPagesPerChunk);
            IndicesPerPage = verticesPerPage * indicesPerVertex;

            _vertices = new ComputeBuffer(pageCount * verticesPerPage, VertexStrideBytes,
                                          ComputeBufferType.Structured);
            // The mesher writes indices through an RWByteAddressBuffer, so the allocation has
            // to be Raw; a Structured buffer bound to that UAV is undefined behaviour.
            _indices = new GraphicsBuffer(GraphicsBuffer.Target.Raw,
                                          pageCount * IndicesPerPage, sizeof(uint));
        }

        /// <summary>
        /// Pages an arena needs to hold <paramref name="budgetBytes"/> of geometry, floored at one.
        /// </summary>
        public static int PagesForBudget(long budgetBytes,
                                         int verticesPerPage = GpuMeshletPageArena.DefaultVerticesPerPage,
                                         int indicesPerVertex = DefaultIndicesPerVertex)
        {
            if (verticesPerPage <= 0) throw new ArgumentOutOfRangeException(nameof(verticesPerPage));
            long bytesPerPage = (long)verticesPerPage * VertexStrideBytes
                              + (long)verticesPerPage * indicesPerVertex * sizeof(uint);
            return (int)Math.Max(1, budgetBytes / bytesPerPage);
        }

        /// <summary>
        /// Reserves space for what the count pass says is coming.
        ///
        /// Index capacity is checked as well as vertex capacity: the page ratio is a fixed
        /// assumption about ordinary geometry, and a chunk that violates it has to be refused rather
        /// than allowed to write past the end of its last page.
        /// </summary>
        public GpuPageReservation TryReserve(int chunkId, in GpuExtractionCounts counts,
                                             out IReadOnlyList<int> pages)
        {
            ThrowIfDisposed();

            GpuPageReservation outcome = _pages.TryReserve(chunkId, counts.VertexCount, out pages);
            if (outcome != GpuPageReservation.Granted) return outcome;

            if (counts.IndexCount > pages.Count * IndicesPerPage)
            {
                // More indices than the ratio allows. Refusing whole leaves the chunk's previous
                // geometry standing, which is the coverage invariant; writing what fits would be a
                // hole with extra steps.
                _pages.Release(chunkId);
                pages = Array.Empty<int>();
                return GpuPageReservation.TooLarge;
            }

            return GpuPageReservation.Granted;
        }

        public bool TryGetPages(int chunkId, out IReadOnlyList<int> pages) =>
            _pages.TryGetPages(chunkId, out pages);

        public bool Release(int chunkId) => _pages.Release(chunkId);

        public void Clear() => _pages.Clear();

        /// <summary>First vertex slot of a page, in the shared buffer's numbering.</summary>
        public int PageVertexOffset(int page) => page * VerticesPerPage;

        /// <summary>First index slot of a page, in the shared buffer's numbering.</summary>
        public int PageIndexOffset(int page) => page * IndicesPerPage;

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(GpuGeometryArena));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _vertices?.Release();
            _indices?.Release();
            _pages.Clear();
        }
    }
}
