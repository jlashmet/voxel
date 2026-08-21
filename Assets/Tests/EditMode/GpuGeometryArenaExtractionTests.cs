using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using VoxelEngine.Rendering.Runtime.GpuVoxel;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Tests.EditMode
{
    /// <summary>
    /// Does count-reserve-write actually hold together end to end?
    ///
    /// A compute mesher cannot grow a buffer, so it has to be told where to write before it writes.
    /// That makes two claims load-bearing, and neither is provable by reading the code: that the
    /// count pass never under-reports what the write pass emits, and that the page indirection puts
    /// every vertex inside a page the chunk actually owns. Under-reporting truncates a chunk into a
    /// hole; a bad indirection scribbles over another chunk's geometry, which looks like a rendering
    /// glitch somewhere else entirely.
    ///
    /// These dispatch the real kernels against a real arena, and are skipped without a graphics
    /// device rather than passing vacuously.
    /// </summary>
    public sealed class GpuGeometryArenaExtractionTests
    {
        private const string ShaderPath =
            "Assets/VoxelEngine/Rendering/Resources/VoxelBrickMesher.compute";

        private const int CellsPerAxis = 8;
        private const int Padding = 2;

        // Small enough that a single chunk spans several pages, which is the case the indirection
        // exists for. One page per chunk would let a broken mapping pass.
        private const int VerticesPerPage = 128;

        private ComputeShader _shader;

        [SetUp]
        public void SetUp()
        {
            if (!SystemInfo.supportsComputeShaders)
                Assert.Ignore("No compute support on this device; the mesher cannot be exercised.");

            _shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(ShaderPath);
            Assert.NotNull(_shader, $"Compute shader missing at {ShaderPath}");
        }

        private static void FillHalfSolidCache(GpuSurfaceExtractor extractor,
                                               int solidBrickYLimit, byte material)
        {
            extractor.ClearBrickCache();
            for (int z = 0; z < extractor.BrickCacheEdge; z++)
            for (int y = 0; y < extractor.BrickCacheEdge; y++)
            for (int x = 0; x < extractor.BrickCacheEdge; x++)
            {
                bool solid = y < solidBrickYLimit;
                extractor.SetBrickCacheEntry(new int3(x, y, z),
                    GpuSurfaceExtractor.PackBrickCacheEntry(
                        solid ? VoxelBrickContent.Uniform : VoxelBrickContent.Empty,
                        solid ? material : (byte)0, -1));
            }
        }

        private static void ConfigureCatalogues(GpuSurfaceExtractor extractor)
        {
            MaterialPaletteView palette = default;
            var defaultStyles = new uint[256];
            for (int i = 0; i < 256; i++) defaultStyles[i] = palette.GetDefaultSurfaceStyle((byte)i);
            extractor.SetCatalogues(SurfaceCatalogueView.CreateBuiltIns(), default, defaultStyles);
        }

        [Test]
        public void CountedGeometryIsExactlyWhatTheWritePassEmits()
        {
            using var mirror = new GpuVoxelBrickMirror(slotCapacity: 8);
            using var tables = GpuTransvoxelTables.CreateDefault();
            using var extractor = new GpuSurfaceExtractor(_shader, CellsPerAxis, Padding);
            using var arena = new GpuGeometryArena(pageCount: 256, VerticesPerPage);

            ConfigureCatalogues(extractor);
            FillHalfSolidCache(extractor, solidBrickYLimit: 2, material: 1);

            // All six faces stitched, so the transition kernel's count mode is exercised too — it
            // is the half most likely to disagree, because counting and writing are separate exits
            // through the same code.
            var request = new GpuChunkExtraction(int3.zero, new int3(-1, -1, -1),
                                                 sourceStep: 2, voxelSize: 0.1f,
                                                 transitionFaceMask: 0b111111);

            GpuExtractionCounts counts = extractor.Count(mirror, tables, request);
            Assert.Greater(counts.VertexCount, 0,
                "The fixture must produce geometry, or nothing below is being tested.");
            Assert.Greater(counts.IndexCount, 0);

            Assert.AreEqual(GpuPageReservation.Granted,
                arena.TryReserve(chunkId: 1, counts, out IReadOnlyList<int> pages));
            Assert.Greater(pages.Count, 1,
                "This fixture is meant to span several pages; with one page a broken indirection "
              + "would still land in the right place.");

            GpuExtractionResult result = extractor.Write(
                mirror, tables, request, arena.Vertices, arena.Indices,
                pages, arena.VerticesPerPage, arena.IndicesPerPage);

            Assert.IsFalse(result.Overflowed,
                "The write pass emitted more than the count pass reserved. That is the one failure "
              + "the two-pass design exists to make impossible, and it truncates chunks into holes.");
            Assert.AreEqual(counts.VertexCount, result.VertexCount,
                "Counted and written vertex totals must agree exactly.");
            Assert.AreEqual(counts.IndexCount, result.IndexCount,
                "Counted and written index totals must agree exactly.");
        }

        [Test]
        public void EveryWrittenVertexLandsInsideAPageTheChunkOwns()
        {
            using var mirror = new GpuVoxelBrickMirror(slotCapacity: 8);
            using var tables = GpuTransvoxelTables.CreateDefault();
            using var extractor = new GpuSurfaceExtractor(_shader, CellsPerAxis, Padding);
            using var arena = new GpuGeometryArena(pageCount: 256, VerticesPerPage);

            ConfigureCatalogues(extractor);
            FillHalfSolidCache(extractor, solidBrickYLimit: 2, material: 1);

            var request = new GpuChunkExtraction(int3.zero, new int3(-1, -1, -1), 2, 0.1f,
                                                 transitionFaceMask: 0b111111);

            // Reserve a decoy chunk first so this one's pages are not 0, 1, 2... A mapping that
            // ignored the page list entirely would still pass against an identity reservation.
            Assert.AreEqual(GpuPageReservation.Granted,
                arena.TryReserve(chunkId: 99, new GpuExtractionCounts(VerticesPerPage * 3, 1),
                                 out IReadOnlyList<int> decoy));
            Assert.AreEqual(3, decoy.Count);

            GpuExtractionCounts counts = extractor.Count(mirror, tables, request);
            Assert.AreEqual(GpuPageReservation.Granted,
                arena.TryReserve(chunkId: 1, counts, out IReadOnlyList<int> pages));

            var owned = new HashSet<int>(pages);
            Assert.IsFalse(owned.Contains(decoy[0]), "The decoy must hold pages this chunk does not.");

            GpuExtractionResult result = extractor.Write(
                mirror, tables, request, arena.Vertices, arena.Indices,
                pages, arena.VerticesPerPage, arena.IndicesPerPage);
            Assert.IsFalse(result.Overflowed);
            Assert.Greater(result.IndexCount, 0);

            // Indices are read page by page, because a chunk's index run is only contiguous within
            // a page — which is the point of the arena.
            int remaining = result.IndexCount;
            int outside = 0;
            int firstOutside = -1;
            for (int p = 0; p < pages.Count && remaining > 0; p++)
            {
                int take = Mathf.Min(remaining, arena.IndicesPerPage);
                var page = new uint[take];
                arena.Indices.GetData(page, 0, arena.PageIndexOffset(pages[p]), take);
                remaining -= take;

                foreach (uint index in page)
                {
                    if (owned.Contains((int)index / arena.VerticesPerPage)) continue;
                    outside++;
                    if (firstOutside < 0) firstOutside = (int)index;
                }
            }

            Assert.AreEqual(0, outside,
                $"{outside} of {result.IndexCount} indices point outside this chunk's pages "
              + $"(first: vertex {firstOutside}, page {(firstOutside < 0 ? -1 : firstOutside / arena.VerticesPerPage)}; "
              + $"owned: {string.Join(",", pages)}). The page indirection is writing into memory "
              + "another chunk owns, which corrupts geometry somewhere else in the world.");
        }

        [Test]
        public void AChunkTooLargeForTheArenaIsRefusedWhole()
        {
            // Exhaustion has to leave the previous geometry standing. A partial write is a hole,
            // and a hole in a world that was previously covered is worse than an old surface.
            using var arena = new GpuGeometryArena(pageCount: 4, VerticesPerPage,
                                                   maxPagesPerChunk: 2);

            Assert.AreEqual(GpuPageReservation.Granted,
                arena.TryReserve(1, new GpuExtractionCounts(VerticesPerPage * 2, 4), out _));
            Assert.AreEqual(GpuPageReservation.TooLarge,
                arena.TryReserve(2, new GpuExtractionCounts(VerticesPerPage * 3, 4), out _));
            Assert.AreEqual(GpuPageReservation.Granted,
                arena.TryReserve(3, new GpuExtractionCounts(VerticesPerPage * 2, 4), out _));
            Assert.AreEqual(GpuPageReservation.Exhausted,
                arena.TryReserve(4, new GpuExtractionCounts(VerticesPerPage, 4), out _));

            Assert.IsTrue(arena.TryGetPages(1, out IReadOnlyList<int> stillHeld));
            Assert.AreEqual(2, stillHeld.Count,
                "A refused reservation must not disturb chunks that already fit.");
        }

        [Test]
        public void MoreIndicesThanThePageRatioAllowsIsRefusedRatherThanTruncated()
        {
            // Vertices and indices share a page list on the assumption that ordinary geometry stays
            // under the ratio. Geometry that does not must be refused, not silently clipped at the
            // end of its last page.
            using var arena = new GpuGeometryArena(pageCount: 16, VerticesPerPage,
                                                   indicesPerVertex: 2);

            int oneVertexPage = VerticesPerPage;
            Assert.AreEqual(GpuPageReservation.Granted,
                arena.TryReserve(1, new GpuExtractionCounts(oneVertexPage, arena.IndicesPerPage),
                                 out _));

            Assert.AreEqual(GpuPageReservation.TooLarge,
                arena.TryReserve(2, new GpuExtractionCounts(oneVertexPage, arena.IndicesPerPage + 1),
                                 out _));
            Assert.IsFalse(arena.TryGetPages(2, out _),
                "A refused chunk must hold no pages at all.");
        }

        [Test]
        public void WritingIntoAnOffsetRangeMatchesWritingAtZeroAndTouchesNothingElse()
        {
            // The renderer's existing arena hands out contiguous ranges, not pages, and the draw
            // path adds the chunk's base itself. So a range written by the GPU has to be identical
            // to one written at the origin, with index values still in the chunk's own numbering —
            // otherwise geometry produced on the GPU cannot be drawn by the path that already
            // exists, and every chunk would need to know which mesher made it.
            using var mirror = new GpuVoxelBrickMirror(slotCapacity: 8);
            using var tables = GpuTransvoxelTables.CreateDefault();
            using var extractor = new GpuSurfaceExtractor(_shader, CellsPerAxis, Padding);

            ConfigureCatalogues(extractor);
            FillHalfSolidCache(extractor, solidBrickYLimit: 2, material: 1);

            var request = new GpuChunkExtraction(int3.zero, new int3(-1, -1, -1), 2, 0.1f,
                                                 transitionFaceMask: 0b111111);

            const int capacity = 32768;
            const int vertexStart = 4096;
            const int indexStart = 8192;
            const uint sentinel = 0xDEADBEEFu;

            var vertices = new ComputeBuffer(capacity, GpuSurfaceExtractor.ReadbackVertex.Stride,
                                             ComputeBufferType.Structured);
            var indices = new GraphicsBuffer(
                GraphicsBuffer.Target.Raw | GraphicsBuffer.Target.Index,
                capacity, sizeof(uint));
            try
            {
                var poison = new uint[capacity];
                for (int i = 0; i < capacity; i++) poison[i] = sentinel;
                indices.SetData(poison);

                GpuExtractionCounts counts = extractor.Count(mirror, tables, request);
                Assert.Greater(counts.IndexCount, 0);

                GpuExtractionResult atZero = extractor.WriteRange(
                    mirror, tables, request, vertices, indices,
                    0, capacity / 2, 0, capacity / 2);
                var baseline = new uint[atZero.IndexCount];
                indices.GetData(baseline, 0, 0, atZero.IndexCount);

                indices.SetData(poison);
                extractor.Count(mirror, tables, request);
                GpuExtractionResult atOffset = extractor.WriteRange(
                    mirror, tables, request, vertices, indices,
                    vertexStart, capacity / 4, indexStart, capacity / 4);

                Assert.AreEqual(atZero.IndexCount, atOffset.IndexCount);
                Assert.AreEqual(atZero.VertexCount, atOffset.VertexCount);

                var offsetIndices = new uint[atOffset.IndexCount];
                indices.GetData(offsetIndices, 0, indexStart, atOffset.IndexCount);

                // Index *values* must be identical, not shifted: they are chunk-local on both sides.
                // Comparing as multisets, because the atomics that reserve space do not promise an
                // order and never have.
                System.Array.Sort(baseline);
                System.Array.Sort(offsetIndices);
                CollectionAssert.AreEqual(baseline, offsetIndices,
                    "Writing at an offset changed the index values. They must stay in the chunk's "
                  + "own numbering, because the draw shader adds the chunk's vertex base itself — "
                  + "shifting them here would double-count it.");

                uint maxIndex = 0;
                foreach (uint index in offsetIndices) maxIndex = System.Math.Max(maxIndex, index);
                Assert.Less((int)maxIndex, atOffset.VertexCount,
                    "An index points past the chunk's own vertex count, so it is not local.");

                // Nothing outside the range may have been touched.
                var before = new uint[indexStart];
                indices.GetData(before, 0, 0, indexStart);
                foreach (uint word in before)
                    Assert.AreEqual(sentinel, word,
                        "The write pass scribbled before its range. In the real arena that is "
                      + "another chunk's geometry.");

                int afterStart = indexStart + atOffset.IndexCount;
                var after = new uint[capacity - afterStart];
                indices.GetData(after, 0, afterStart, after.Length);
                foreach (uint word in after)
                    Assert.AreEqual(sentinel, word, "The write pass scribbled past its range.");
            }
            finally
            {
                vertices.Release();
                indices.Release();
            }
        }

        [Test]
        public void TheProductionPathNeverReadsGeometryBack()
        {
            // The point of moving extraction to the GPU is that generated geometry stops crossing
            // the bus. A readback of vertices, indices or the sampled field would put the CPU back
            // on the critical path and quietly undo the whole migration — and it would still look
            // correct, which is why this is asserted rather than reviewed.
            using var mirror = new GpuVoxelBrickMirror(slotCapacity: 8);
            using var tables = GpuTransvoxelTables.CreateDefault();
            using var extractor = new GpuSurfaceExtractor(_shader, CellsPerAxis, Padding);
            using var arena = new GpuGeometryArena(pageCount: 256, VerticesPerPage);

            ConfigureCatalogues(extractor);
            FillHalfSolidCache(extractor, solidBrickYLimit: 2, material: 1);

            var request = new GpuChunkExtraction(int3.zero, new int3(-1, -1, -1), 2, 0.1f,
                                                 transitionFaceMask: 0b111111);

            const int chunks = 4;
            for (int chunk = 0; chunk < chunks; chunk++)
            {
                GpuExtractionCounts counts = extractor.Count(mirror, tables, request);
                Assert.AreEqual(GpuPageReservation.Granted,
                    arena.TryReserve(chunk, counts, out IReadOnlyList<int> pages));
                extractor.Write(mirror, tables, request, arena.Vertices, arena.Indices,
                                pages, arena.VerticesPerPage, arena.IndicesPerPage);
            }

            Assert.AreEqual(0uL, extractor.GeometryReadbacks,
                "Something on the extraction path copied geometry or the sampled field back from "
              + "the GPU. Only the CPU-versus-GPU oracles may do that.");

            // Two per chunk — one for the count pass, one for the write pass — and it must not
            // scale with how much geometry there was.
            Assert.AreEqual((ulong)(chunks * 2), extractor.CounterReadbacks,
                "Bookkeeping readbacks must be a fixed number per chunk. Anything proportional to "
              + "the surface is the readback the invariant forbids, wearing a different name.");
        }

        [Test]
        public void ArenaSizingFollowsTheDeviceBudget()
        {
            // The mobile tier's 320 MB from the device matrix, checked arithmetically. Actually
            // committing it here would put a third of a gigabyte of ComputeBuffer into the editor
            // process for one assertion, which is the shape of the allocation that has taken this
            // machine down before.
            const long mobileBudget = 320L * 1024 * 1024;
            long bytesPerPage =
                (long)GpuMeshletPageArena.DefaultVerticesPerPage * GpuGeometryArena.VertexStrideBytes
              + (long)GpuMeshletPageArena.DefaultVerticesPerPage
                * GpuGeometryArena.DefaultIndicesPerVertex * sizeof(uint);
            Assert.AreEqual(mobileBudget / bytesPerPage,
                            GpuGeometryArena.PagesForBudget(mobileBudget));

            // The committed-bytes accounting itself is checked against a small arena.
            const long smallBudget = 8L * 1024 * 1024;
            int pages = GpuGeometryArena.PagesForBudget(smallBudget);
            using var arena = new GpuGeometryArena(pages, GpuMeshletPageArena.DefaultVerticesPerPage);

            Assert.LessOrEqual(arena.CommittedBytes, smallBudget,
                "The arena committed more than the budget it was sized from.");
            Assert.Greater(arena.CommittedBytes, smallBudget * 9 / 10,
                "and it should use most of it, or the rounding is wasting the budget.");
        }
    }
}
