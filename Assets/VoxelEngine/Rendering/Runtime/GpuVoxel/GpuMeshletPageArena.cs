using System;
using System.Collections.Generic;

namespace VoxelEngine.Rendering.Runtime.GpuVoxel
{
    /// <summary>Why a page reservation failed.</summary>
    public enum GpuPageReservation
    {
        Granted = 0,

        /// <summary>The arena has too few free pages. Nothing was written.</summary>
        Exhausted = 1,

        /// <summary>The request needs more pages than one chunk may ever hold.</summary>
        TooLarge = 2,

        /// <summary>Nothing to store.</summary>
        Empty = 3,
    }

    /// <summary>
    /// Fixed-size pages of GPU geometry, handed out per chunk.
    ///
    /// Meshing on the GPU has a sizing problem the CPU path did not: the CPU knew how much geometry
    /// a chunk produced before allocating, because it had already produced it. A compute mesher has
    /// to reserve space before it writes, from a shader that cannot grow a buffer. The answer is
    /// count, prefix, then write — count the cells' output in one pass, reserve exactly that, and
    /// only then emit.
    ///
    /// Reservation is all-or-nothing. A build that cannot fit is refused whole, because half a
    /// chunk's triangles is a hole with extra steps: the coverage invariant wants the previous
    /// representation left standing, and that only works if a failed build changes nothing.
    ///
    /// Pages are fixed size and never coalesced, per the plan's §8.3 requirement that one chunk's
    /// geometry moving must never force repacking the arena. A chunk holds a page list rather than
    /// a contiguous range, so fragmentation costs an indirection instead of a compaction pass.
    /// </summary>
    public sealed class GpuMeshletPageArena
    {
        /// <summary>
        /// Vertices per page. Sized so an ordinary chunk needs a handful rather than one page each,
        /// which would make the page table as large as the geometry it indexes.
        /// </summary>
        public const int DefaultVerticesPerPage = 1024;

        /// <summary>
        /// Ceiling on pages for one chunk. A single chunk that wants more than this is pathological
        /// — the density field has degenerated — and letting it consume the arena would starve
        /// every other chunk in view.
        /// </summary>
        public const int DefaultMaxPagesPerChunk = 64;

        private readonly Stack<int> _free;
        private readonly Dictionary<int, List<int>> _pagesByChunk = new();
        private readonly Stack<List<int>> _listPool = new();

        public int PageCount { get; }
        public int VerticesPerPage { get; }
        public int MaxPagesPerChunk { get; }

        public int FreePages => _free.Count;
        public int ReservedPages => PageCount - _free.Count;
        public int ChunkCount => _pagesByChunk.Count;
        public int VertexCapacity => PageCount * VerticesPerPage;

        public ulong ExhaustedCount { get; private set; }
        public ulong RefusedTooLargeCount { get; private set; }

        public GpuMeshletPageArena(int pageCount,
                                   int verticesPerPage = DefaultVerticesPerPage,
                                   int maxPagesPerChunk = DefaultMaxPagesPerChunk)
        {
            if (pageCount <= 0) throw new ArgumentOutOfRangeException(nameof(pageCount));
            if (verticesPerPage <= 0) throw new ArgumentOutOfRangeException(nameof(verticesPerPage));
            if (maxPagesPerChunk <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxPagesPerChunk));

            PageCount = pageCount;
            VerticesPerPage = verticesPerPage;
            MaxPagesPerChunk = maxPagesPerChunk;
            _free = new Stack<int>(pageCount);
            for (int i = pageCount - 1; i >= 0; i--) _free.Push(i);
        }

        /// <summary>Pages a vertex count needs, rounding up.</summary>
        public int PagesFor(int vertexCount) =>
            vertexCount <= 0 ? 0 : (vertexCount + VerticesPerPage - 1) / VerticesPerPage;

        public bool TryGetPages(int chunkId, out IReadOnlyList<int> pages)
        {
            if (_pagesByChunk.TryGetValue(chunkId, out List<int> owned))
            {
                pages = owned;
                return true;
            }
            pages = Array.Empty<int>();
            return false;
        }

        /// <summary>
        /// Reserves pages for a chunk's counted output, replacing whatever it held.
        ///
        /// The previous reservation is only released once the new one is known to fit, so a build
        /// that cannot be placed leaves the chunk exactly as it was and the old geometry keeps
        /// covering the view.
        /// </summary>
        public GpuPageReservation TryReserve(int chunkId, int vertexCount,
                                             out IReadOnlyList<int> pages)
        {
            pages = Array.Empty<int>();

            int needed = PagesFor(vertexCount);
            if (needed == 0)
            {
                Release(chunkId);
                return GpuPageReservation.Empty;
            }

            if (needed > MaxPagesPerChunk)
            {
                RefusedTooLargeCount++;
                return GpuPageReservation.TooLarge;
            }

            _pagesByChunk.TryGetValue(chunkId, out List<int> existing);
            int alreadyHeld = existing?.Count ?? 0;
            if (needed - alreadyHeld > _free.Count)
            {
                ExhaustedCount++;
                return GpuPageReservation.Exhausted;
            }

            List<int> owned = existing ?? RentList();
            while (owned.Count > needed)
            {
                _free.Push(owned[^1]);
                owned.RemoveAt(owned.Count - 1);
            }
            while (owned.Count < needed) owned.Add(_free.Pop());

            _pagesByChunk[chunkId] = owned;
            pages = owned;
            return GpuPageReservation.Granted;
        }

        public bool Release(int chunkId)
        {
            if (!_pagesByChunk.Remove(chunkId, out List<int> owned)) return false;
            for (int i = 0; i < owned.Count; i++) _free.Push(owned[i]);
            ReturnList(owned);
            return true;
        }

        /// <summary>First vertex index of a page, for a shader writing into the shared buffer.</summary>
        public int PageVertexOffset(int page) => page * VerticesPerPage;

        public void Clear()
        {
            foreach (KeyValuePair<int, List<int>> pair in _pagesByChunk) ReturnList(pair.Value);
            _pagesByChunk.Clear();
            _free.Clear();
            for (int i = PageCount - 1; i >= 0; i--) _free.Push(i);
        }

        private List<int> RentList() => _listPool.Count > 0 ? _listPool.Pop() : new List<int>(4);

        private void ReturnList(List<int> list)
        {
            list.Clear();
            _listPool.Push(list);
        }
    }
}
