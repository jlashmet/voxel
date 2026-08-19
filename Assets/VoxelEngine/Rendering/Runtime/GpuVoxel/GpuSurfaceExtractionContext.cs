using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction.Transvoxel;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Rendering.Runtime.GpuVoxel
{
    /// <summary>Why a chunk could not be meshed on the GPU this attempt.</summary>
    public enum GpuStageOutcome
    {
        /// <summary>Staged; the neighbourhood is resident and the field has been counted.</summary>
        Staged = 0,

        /// <summary>The mirror could not admit every mixed brick. Retry once coverage frees slots.</summary>
        NoSlot = 1,

        /// <summary>The chunk crosses no surface. Nothing to draw, and nothing to reserve.</summary>
        Empty = 2,
    }

    /// <summary>
    /// The production entry point to GPU extraction: everything the compute mesher needs, owned in
    /// one place, driven from the snapshot the CPU builder already gathers.
    ///
    /// <para>The CPU path's first phase flattens a chunk's brick neighbourhood into a small dense
    /// array — kind, uniform material, and an offset into pinned mixed payloads — precisely because
    /// the density job needs random access to it without touching region tables. That array is also
    /// exactly what the GPU wants, so the two paths share a snapshot rather than each taking their
    /// own. Sharing it is what makes this an alternative back end rather than a second pipeline.</para>
    ///
    /// <para>Nothing generated comes back. Voxels go up, geometry is written straight into the
    /// renderer's arena, and the only readback is the two-integer count that lets the arena refuse a
    /// build whole instead of truncating it into a hole.</para>
    /// </summary>
    public sealed class GpuSurfaceExtractionContext : IDisposable
    {
        /// <summary>
        /// Where the compute mesher lives. Loaded by path rather than by a serialized reference
        /// because extraction is wired up in code, and a missing reference on a
        /// ScriptableRendererFeature is invisible until the frame it is needed.
        /// </summary>
        public const string ShaderResourcePath = "VoxelBrickMesher";

        private readonly GpuVoxelBrickMirror _mirror;
        private readonly GpuTransvoxelTables _tables;
        private readonly GpuSurfaceExtractor _extractor;
        private readonly int _brickCacheEdge;
        private readonly uint[] _materialDefaultStyles = new uint[256];
        private bool _cataloguesUploaded;
        private uint _uploadedPaletteVersion;
        private uint _uploadedSurfaceVersion;
        private ulong _uploadedSurfaceHash;
        private uint _uploadedCoatingVersion;
        private ulong _uploadedCoatingHash;
        private bool _disposed;

        private GpuChunkExtraction _staged;
        private GpuExtractionCounts _stagedCounts;
        private bool _hasStaged;
        private int _writeVertexCapacity;
        private int _writeIndexCapacity;

        public GpuVoxelBrickMirror Mirror => _mirror;
        public GpuTransvoxelTables Tables => _tables;
        public GpuSurfaceExtractor Extractor => _extractor;
        public int BrickCacheEdge => _brickCacheEdge;

        public ulong ChunksStaged { get; private set; }
        public ulong ChunksWritten { get; private set; }
        public ulong ChunksRefusedNoSlot { get; private set; }
        public ulong ChunksEmpty { get; private set; }
        public ulong ChunksOverflowed { get; private set; }
        public long MirrorCommittedBytes => _mirror.CommittedBytes;

        private GpuSurfaceExtractionContext(ComputeShader shader, int cellsPerAxis, int padding,
                                            int mirrorSlots, int brickCacheEdge)
        {
            _mirror = new GpuVoxelBrickMirror(mirrorSlots);
            _tables = GpuTransvoxelTables.CreateDefault();
            _extractor = new GpuSurfaceExtractor(shader, cellsPerAxis, padding, brickCacheEdge);
            _brickCacheEdge = _extractor.BrickCacheEdge;
        }

        /// <summary>
        /// Builds the context, or returns null if this device cannot run it.
        ///
        /// Returning null rather than throwing is deliberate: GPU extraction is an alternative back
        /// end, and a device without compute support must fall back to the CPU path silently rather
        /// than fail to render.
        /// </summary>
        public static GpuSurfaceExtractionContext TryCreate(int cellsPerAxis, int padding,
                                                            long mirrorBudgetBytes,
                                                            ComputeShader shader = null)
        {
            return TryCreate(cellsPerAxis, padding, mirrorBudgetBytes, brickCacheEdge: 0, shader);
        }

        /// <summary>
        /// Builds a context whose dense brick cache uses the exact edge of the CPU snapshot it will
        /// consume. Production must use this overload: a merely-large-enough derived cache can index
        /// the same flattened snapshot with a different stride and silently sample the wrong bricks.
        /// </summary>
        public static GpuSurfaceExtractionContext TryCreate(int cellsPerAxis, int padding,
                                                            long mirrorBudgetBytes,
                                                            int brickCacheEdge,
                                                            ComputeShader shader = null)
        {
            if (!SystemInfo.supportsComputeShaders) return null;
            if (brickCacheEdge < 0) throw new ArgumentOutOfRangeException(nameof(brickCacheEdge));

            shader ??= Resources.Load<ComputeShader>(ShaderResourcePath);
            if (shader == null) return null;

            int slots = GpuBrickBufferLayout.SlotsForBudget(mirrorBudgetBytes);
            if (slots <= 0) return null;

            try
            {
                return new GpuSurfaceExtractionContext(
                    shader, cellsPerAxis, padding, slots, brickCacheEdge);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"GPU surface extraction unavailable: {e.Message}");
                return null;
            }
        }

        /// <summary>Uploads the surface rules. Cheap, and only needed on a catalogue change.</summary>
        public void SetCatalogues(in SurfaceCatalogueView surfaces, in CoatingCatalogueView coatings,
                                  in MaterialPaletteView palette)
        {
            if (_cataloguesUploaded
                && _uploadedPaletteVersion == palette.Version
                && _uploadedSurfaceVersion == surfaces.Version
                && _uploadedSurfaceHash == surfaces.CatalogueHash
                && _uploadedCoatingVersion == coatings.Version
                && _uploadedCoatingHash == coatings.CatalogueHash)
                return;

            for (int i = 0; i < 256; i++)
                _materialDefaultStyles[i] = palette.GetDefaultSurfaceStyle((byte)i);
            _extractor.SetCatalogues(surfaces, coatings, _materialDefaultStyles);
            _uploadedPaletteVersion = palette.Version;
            _uploadedSurfaceVersion = surfaces.Version;
            _uploadedSurfaceHash = surfaces.CatalogueHash;
            _uploadedCoatingVersion = coatings.Version;
            _uploadedCoatingHash = coatings.CatalogueHash;
            _cataloguesUploaded = true;
        }

        /// <summary>
        /// Mirrors one chunk's brick neighbourhood and counts what it will emit.
        ///
        /// <paramref name="generation"/> is the snapshot version the bricks came from. Neighbouring
        /// chunks share bricks, so stamping the version lets the mirror skip a brick it already
        /// holds at that version — which is most of them, most frames.
        ///
        /// Admission is all-or-nothing. A neighbourhood that is only partly resident would mesh a
        /// surface against bricks that read as air, and that is a hole rather than a slow frame, so
        /// a refused brick abandons the whole attempt and unpins what was taken.
        /// </summary>
        internal GpuStageOutcome TryStage(NativeArray<TransvoxelDensityBrick> bricks,
                                          NativeArray<byte> mixedVoxels,
                                          NativeArray<ushort> mixedSurfaceSemantics,
                                          NativeArray<byte> mixedBoundarySamples,
                                          in GpuChunkExtraction request,
                                          ulong generation)
        {
            if (!TryPin(bricks, mixedVoxels, mixedSurfaceSemantics, mixedBoundarySamples,
                        request, generation))
                return GpuStageOutcome.NoSlot;

            _stagedCounts = _extractor.Count(_mirror, _tables, request);
            _staged = request;
            _hasStaged = true;
            ChunksStaged++;

            if (_stagedCounts.IsEmpty)
            {
                Release();
                ChunksEmpty++;
                return GpuStageOutcome.Empty;
            }

            return GpuStageOutcome.Staged;
        }

        /// <summary>
        /// Pins the neighbourhood and starts the counting pass without waiting for its result.
        ///
        /// The synchronous <see cref="TryStage"/> blocks on a GPU flush, which measured an order of
        /// magnitude more than the meshing it waits for and starved every other worker sharing the
        /// frame's build budget. This variant leaves the chunk mid-count: the neighbourhood stays
        /// pinned, and <see cref="TryCompleteStage"/> finishes it on a later frame.
        /// </summary>
        internal GpuStageOutcome TryBeginStage(NativeArray<TransvoxelDensityBrick> bricks,
                                               NativeArray<byte> mixedVoxels,
                                               NativeArray<ushort> mixedSurfaceSemantics,
                                               NativeArray<byte> mixedBoundarySamples,
                                               in GpuChunkExtraction request,
                                               ulong generation)
        {
            if (!TryPin(bricks, mixedVoxels, mixedSurfaceSemantics, mixedBoundarySamples,
                        request, generation))
                return GpuStageOutcome.NoSlot;

            _staged = request;
            _hasStaged = true;
            _extractor.BeginCount(_mirror, _tables, request);
            return GpuStageOutcome.Staged;
        }

        /// <summary>
        /// Finishes a <see cref="TryBeginStage"/>. Ready means the counts are known and the
        /// neighbourhood is still pinned for the write; Failed and an empty count both release it.
        /// </summary>
        internal GpuSurfaceExtractor.GpuCounterPoll TryCompleteStage(
            out GpuExtractionCounts counts)
        {
            ThrowIfDisposed();
            counts = default;
            if (!_hasStaged) return GpuSurfaceExtractor.GpuCounterPoll.Failed;

            GpuSurfaceExtractor.GpuCounterPoll poll = _extractor.TryCompleteCount(out counts);
            if (poll == GpuSurfaceExtractor.GpuCounterPoll.Pending) return poll;
            if (poll == GpuSurfaceExtractor.GpuCounterPoll.Failed)
            {
                Release();
                return poll;
            }

            _stagedCounts = counts;
            ChunksStaged++;
            if (counts.IsEmpty)
            {
                Release();
                ChunksEmpty++;
            }
            return poll;
        }

        /// <summary>Starts the write pass into an already-reserved arena range.</summary>
        public void BeginWriteRange(ComputeBuffer vertices, ComputeBuffer indices,
                                    int vertexStart, int vertexCapacity,
                                    int indexStart, int indexCapacity)
        {
            ThrowIfDisposed();
            _writeVertexCapacity = vertexCapacity;
            _writeIndexCapacity = indexCapacity;
            _extractor.BeginWriteRange(_mirror, _tables, _staged, vertices, indices,
                                       vertexStart, vertexCapacity, indexStart, indexCapacity);
        }

        /// <summary>
        /// Finishes a <see cref="BeginWriteRange"/>, applying the same count/write agreement check
        /// the blocking path makes before anything is published.
        /// </summary>
        public GpuSurfaceExtractor.GpuCounterPoll TryCompleteWriteRange(out int indexCount)
        {
            ThrowIfDisposed();
            indexCount = 0;
            if (!_hasStaged) return GpuSurfaceExtractor.GpuCounterPoll.Failed;

            GpuSurfaceExtractor.GpuCounterPoll poll = _extractor.TryCompleteWriteRange(
                _writeVertexCapacity, _writeIndexCapacity, out GpuExtractionResult result);
            if (poll != GpuSurfaceExtractor.GpuCounterPoll.Ready) return poll;

            if (result.Overflowed
                || result.VertexCount != _stagedCounts.VertexCount
                || result.IndexCount != _stagedCounts.IndexCount)
            {
                ChunksOverflowed++;
                return GpuSurfaceExtractor.GpuCounterPoll.Failed;
            }

            indexCount = result.IndexCount;
            ChunksWritten++;
            return GpuSurfaceExtractor.GpuCounterPoll.Ready;
        }

        private bool TryPin(NativeArray<TransvoxelDensityBrick> bricks,
                            NativeArray<byte> mixedVoxels,
                            NativeArray<ushort> mixedSurfaceSemantics,
                            NativeArray<byte> mixedBoundarySamples,
                            in GpuChunkExtraction request,
                            ulong generation)
        {
            ThrowIfDisposed();
            _hasStaged = false;

            int expected = _brickCacheEdge * _brickCacheEdge * _brickCacheEdge;
            if (bricks.Length < expected)
                throw new ArgumentException(
                    $"Brick snapshot holds {bricks.Length} entries; the GPU cache spans {expected}.",
                    nameof(bricks));

            _extractor.ClearBrickCache();

            int pinned = 0;
            for (int z = 0; z < _brickCacheEdge; z++)
            for (int y = 0; y < _brickCacheEdge; y++)
            for (int x = 0; x < _brickCacheEdge; x++)
            {
                var local = new int3(x, y, z);
                TransvoxelDensityBrick brick =
                    bricks[x + _brickCacheEdge * (y + _brickCacheEdge * z)];
                int3 coordinate = request.BrickCacheOrigin + local;

                if (brick.Kind != 2)
                {
                    VoxelBrickContent content = brick.Kind == 1
                        ? VoxelBrickContent.Uniform : VoxelBrickContent.Empty;
                    _extractor.SetBrickCacheEntry(local,
                        GpuSurfaceExtractor.PackBrickCacheEntry(content, brick.UniformMaterial, -1));
                    continue;
                }

                var delta = VoxelBrickDelta.MixedAt(coordinate, generation, brick.MixedOffset);
                GpuBrickPublish published = _mirror.Publish(
                    delta, mixedVoxels, mixedSurfaceSemantics, mixedBoundarySamples,
                    brick.MixedOffset, hasPayload: true);

                if (published is GpuBrickPublish.NoSlot or GpuBrickPublish.PayloadMissing)
                {
                    UnpinStaged(bricks, request.BrickCacheOrigin, pinned);
                    ChunksRefusedNoSlot++;
                    return false;
                }

                if (!_mirror.TryGetSlot(coordinate, out int slot))
                {
                    UnpinStaged(bricks, request.BrickCacheOrigin, pinned);
                    ChunksRefusedNoSlot++;
                    return false;
                }

                // Pinned for the duration of the build so eviction cannot pull a brick out from
                // under the dispatch that is about to read it.
                _mirror.Pin(coordinate);
                pinned++;

                _extractor.SetBrickCacheEntry(local,
                    GpuSurfaceExtractor.PackBrickCacheEntry(VoxelBrickContent.Mixed, 0, slot));
            }

            return true;
        }

        /// <summary>What the staged chunk will emit. Valid only between a stage and its release.</summary>
        public GpuExtractionCounts StagedCounts => _stagedCounts;

        /// <summary>
        /// Writes the staged chunk into a range the renderer's arena has already reserved.
        ///
        /// Index values stay in the chunk's own numbering, exactly as the CPU mesher produces them,
        /// so the draw path cannot tell which mesher filled the range.
        /// </summary>
        public bool TryWriteRange(ComputeBuffer vertices, ComputeBuffer indices,
                                  int vertexStart, int vertexCapacity,
                                  int indexStart, int indexCapacity,
                                  out int indexCount)
        {
            ThrowIfDisposed();
            indexCount = 0;
            if (!_hasStaged) return false;

            GpuExtractionResult result = _extractor.WriteRange(
                _mirror, _tables, _staged, vertices, indices,
                vertexStart, vertexCapacity, indexStart, indexCapacity);

            // SurfaceGeometryArena rounds reservations up for alignment, so capacity overflow alone
            // is not enough to prove count/write agreement. A bad count can over-emit into alignment
            // slack and still fit the raw range. Refuse any disagreement before publication.
            if (result.Overflowed
                || result.VertexCount != _stagedCounts.VertexCount
                || result.IndexCount != _stagedCounts.IndexCount)
            {
                ChunksOverflowed++;
                return false;
            }

            indexCount = result.IndexCount;
            ChunksWritten++;
            return true;
        }

        /// <summary>Unpins the staged neighbourhood. Must be called once the build is done or abandoned.</summary>
        public void Release()
        {
            if (!_hasStaged) return;
            _hasStaged = false;
            // An abandoned build must not leave a readback outstanding: the next Begin* would poll
            // it and read the previous chunk's counters.
            _extractor.CancelPendingCounters();
            for (int z = 0; z < _brickCacheEdge; z++)
            for (int y = 0; y < _brickCacheEdge; y++)
            for (int x = 0; x < _brickCacheEdge; x++)
                _mirror.Unpin(_staged.BrickCacheOrigin + new int3(x, y, z));
        }

        private void UnpinStaged(NativeArray<TransvoxelDensityBrick> bricks, int3 cacheOrigin,
                                 int pinnedCount)
        {
            if (pinnedCount <= 0) return;
            int seen = 0;
            for (int z = 0; z < _brickCacheEdge && seen < pinnedCount; z++)
            for (int y = 0; y < _brickCacheEdge && seen < pinnedCount; y++)
            for (int x = 0; x < _brickCacheEdge && seen < pinnedCount; x++)
            {
                if (bricks[x + _brickCacheEdge * (y + _brickCacheEdge * z)].Kind != 2) continue;
                _mirror.Unpin(cacheOrigin + new int3(x, y, z));
                seen++;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(GpuSurfaceExtractionContext));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _extractor.Dispose();
            _tables.Dispose();
            _mirror.Dispose();
        }
    }
}
