using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Game.Structures.Api;
using Game.Structures.Runtime;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Composition;
using VoxelEngine.Storage.Api;
using VoxelEngine.Storage.Runtime;
using VoxelEngine.Structures.Api;
using GameCastlePlan = Game.Structures.Api.CastlePlan;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Builds the expensive procedural castle against a private voxel store on a worker thread,
    /// then publishes only the touched 8^3 logical blocks back to the live world in bounded
    /// batches. The live RegionTable/BrickPool are never read or written by the worker.
    ///
    /// ShowcaseWorld's legacy castle loop tests IsComplete after every Step and would otherwise
    /// poll a waiting worker until the entire frame budget expires. IsComplete therefore also
    /// reports true after this session has already been stepped in the current Unity frame. On
    /// the next frame it becomes false again automatically until terminal completion. That keeps
    /// the existing scheduler to one bounded castle slice per player-loop iteration without
    /// conflating worker completion with live-world completion across frames.
    /// </summary>
    internal sealed class AsyncCastleBuildSession : ICastleBuildSession, IDisposable
    {
        private const int BlocksPerPublishSlice = 32;
        private const int MinimumPrivateMixedBrickCapacity = 8 * 1024;
        private const int PrivateMixedBrickSafetyReserve = 4 * 1024;

        private readonly IRegionSnapshotSource _liveSnapshots;
        private readonly IRegionMutationStore _liveMutations;
        private readonly IMaterialAuthoringCatalogue _materials;
        private readonly GameCastlePlan _plan;
        private readonly uint _terrainSeed;
        private readonly int3[] _regions;
        private readonly RegionSemanticSnapshot[] _sourceSnapshots;
        private IVoxelStorageRuntime _privateStorage;

        private int _captureCursor;
        private Task<BuildResult> _worker;
        private BuildResult _result;
        private int _publishCursor;
        private int _lastStepFrame = -1;
        private bool _terminalComplete;
        private volatile bool _cancelRequested;
        private bool _disposed;

        public AsyncCastleBuildSession(
            IRegionReadSource liveReads,
            IRegionMutationStore liveMutations,
            IMaterialAuthoringCatalogue materials,
            in CastlePlan plan,
            uint terrainSeed)
        {
            _liveSnapshots = liveReads as IRegionSnapshotSource
                ?? throw new ArgumentException(
                    "Showcase castle authoring requires a snapshot-capable read source.",
                    nameof(liveReads));
            _liveMutations = liveMutations
                ?? throw new ArgumentNullException(nameof(liveMutations));
            _materials = materials;
            _plan = plan.Value;
            _terrainSeed = terrainSeed;
            _regions = CastleRegions(in plan);
            _sourceSnapshots = new RegionSemanticSnapshot[_regions.Length];

            // The private BrickPool is deliberately not allocated here. Construction happens on
            // the worker after the compact terrain snapshots are captured, so creating the castle
            // session cannot synchronously reserve hundreds of MB on the player-loop thread.
            RenderingComposition.WorldClearing += Dispose;
        }

        /// <summary>
        /// Terminally true after publication, and transiently true after this session already
        /// consumed its one allowed slice in the current frame so ShowcaseWorld cannot busy-poll.
        /// </summary>
        public bool IsComplete => _terminalComplete || _lastStepFrame == Time.frameCount;

        public int StageNumber
        {
            get
            {
                if (_terminalComplete) return 9;
                if (_worker == null) return 1;
                if (!_worker.IsCompleted) return 2;
                return 8;
            }
        }

        public long TotalVoxelsWritten => _result?.TotalVoxelsWritten ?? 0L;

        public bool Step()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AsyncCastleBuildSession));
            if (_terminalComplete) return true;
            _lastStepFrame = Time.frameCount;

            // Snapshot exactly one already-generated castle region per frame. This is the only
            // live read needed by the worker and keeps snapshot encoding from becoming a single
            // multi-region startup hitch.
            if (_captureCursor < _regions.Length)
            {
                int3 region = _regions[_captureCursor];
                RegionSnapshotCaptureResult captured = _liveSnapshots.CaptureSemanticSnapshot(
                    region,
                    RegionSemanticSnapshotLimits.DefaultMaxSnapshotBytes,
                    out RegionSemanticSnapshot snapshot);
                if (captured != RegionSnapshotCaptureResult.Ok)
                    throw new InvalidOperationException(
                        $"Could not snapshot castle region {region}: {captured}.");

                _sourceSnapshots[_captureCursor++] = snapshot;
                return false;
            }

            if (_worker == null)
            {
                _worker = Task.Run(BuildOnPrivateStore);
                return false;
            }

            if (!_worker.IsCompleted)
                return false;

            if (_result == null)
            {
                // Propagate worker exceptions on the main thread with their original stack.
                _result = _worker.GetAwaiter().GetResult();
                if (_result == null)
                    throw new OperationCanceledException("Castle build was cancelled before publication.");
            }

            int end = math.min(_result.Blocks.Count,
                               _publishCursor + BlocksPerPublishSlice);
            for (; _publishCursor < end; _publishCursor++)
                PublishBlock(_result.Blocks[_publishCursor]);

            if (_publishCursor < _result.Blocks.Count)
                return false;

            // Native staging is needed only until the final live block is copied. Retain the small
            // BuildResult shell so TotalVoxelsWritten remains available to ShowcaseWorld when this
            // terminal Step returns true.
            _result.DisposePayloads();
            _terminalComplete = true;
            RenderingComposition.WorldClearing -= Dispose;
            return true;
        }

        private BuildResult BuildOnPrivateStore()
        {
            if (_cancelRequested) return null;

            int privateMixedCapacity = EstimatePrivateMixedBrickCapacity(in _plan, _regions.Length);
            IVoxelStorageRuntime storage = VoxelEngineBootstrap.CreateStorage(
                math.max(16, _regions.Length * 2),
                privateMixedCapacity,
                1024);
            _privateStorage = storage;

            try
            {
                for (int i = 0; i < _sourceSnapshots.Length; i++)
                {
                    if (_cancelRequested) return null;
                    RegionSemanticSnapshot snapshot = _sourceSnapshots[i];
                    if (!storage.SnapshotMutations.TryApplySemanticSnapshot(
                            snapshot.RegionCoord,
                            snapshot.Bytes,
                            snapshot.SemanticHash,
                            true))
                        throw new InvalidOperationException(
                            $"Private castle store rejected source snapshot {snapshot.RegionCoord}.");
                }

                if (_cancelRequested) return null;
                var tracking = new TrackingMutationStore(storage.Mutations);
                IStructureAuthoringSession authoring =
                    VoxelEngine.Composition.StructuresComposition.CreateAuthoringSession(
                        storage.Reads,
                        tracking,
                        _materials);
                var build = new CastleAuthoringBuild(authoring, in _plan, _terrainSeed);
                while (!build.Step())
                {
                    if (_cancelRequested) return null;
                }
                if (_cancelRequested) return null;

                // First classify the touched blocks. The result holds only compact descriptors;
                // mixed payloads are copied into three contiguous native staging arrays below.
                // No VoxelCell[512] objects enter the managed heap.
                var blocks = new List<BlockImage>(tracking.Touched.Count);
                int mixedCount = 0;
                foreach (KeyValuePair<int3, bool> touched in tracking.Touched)
                {
                    if (_cancelRequested) return null;
                    if (!storage.Reads.TryPinWorldBlock(touched.Key, out PinnedVoxelReadBlock block))
                        throw new InvalidOperationException(
                            $"Private castle block {touched.Key} was not readable after authoring.");

                    try
                    {
                        int payloadOffset = block.Kind == VoxelReadBlockKind.Mixed
                            ? mixedCount++ * VoxelReadGrid.VoxelsPerBlock
                            : -1;
                        blocks.Add(new BlockImage(
                            touched.Key,
                            block.Kind,
                            block.UniformMaterial,
                            payloadOffset,
                            touched.Value));
                    }
                    finally
                    {
                        if (block.HasPinnedPayload)
                            storage.Reads.ReleasePinnedWorldBlock(in block.Pin);
                    }
                }

                // Stable order keeps publication deterministic and makes frame-budget regressions
                // reproducible rather than dependent on Dictionary iteration order. PayloadOffset
                // remains stable because it addresses the staging arrays rather than list order.
                blocks.Sort(static (a, b) =>
                {
                    int c = a.WorldBlock.z.CompareTo(b.WorldBlock.z);
                    if (c != 0) return c;
                    c = a.WorldBlock.y.CompareTo(b.WorldBlock.y);
                    return c != 0 ? c : a.WorldBlock.x.CompareTo(b.WorldBlock.x);
                });

                int payloadVoxels = mixedCount * VoxelReadGrid.VoxelsPerBlock;
                NativeArray<byte> mixedMaterials = payloadVoxels > 0
                    ? new NativeArray<byte>(payloadVoxels, Allocator.Persistent,
                                            NativeArrayOptions.UninitializedMemory)
                    : default;
                NativeArray<ushort> mixedSurfaceSemantics = payloadVoxels > 0
                    ? new NativeArray<ushort>(payloadVoxels, Allocator.Persistent,
                                              NativeArrayOptions.UninitializedMemory)
                    : default;
                NativeArray<byte> mixedBoundarySamples = payloadVoxels > 0
                    ? new NativeArray<byte>(payloadVoxels, Allocator.Persistent,
                                            NativeArrayOptions.UninitializedMemory)
                    : default;

                try
                {
                    for (int i = 0; i < blocks.Count; i++)
                    {
                        if (_cancelRequested)
                        {
                            DisposeStaging(
                                ref mixedMaterials,
                                ref mixedSurfaceSemantics,
                                ref mixedBoundarySamples);
                            return null;
                        }

                        BlockImage image = blocks[i];
                        if (image.Kind != VoxelReadBlockKind.Mixed) continue;
                        if (!storage.Reads.TryPinWorldBlock(
                                image.WorldBlock, out PinnedVoxelReadBlock block))
                            throw new InvalidOperationException(
                                $"Private castle block {image.WorldBlock} disappeared during staging.");

                        try
                        {
                            if (block.Kind != VoxelReadBlockKind.Mixed || !block.HasPinnedPayload)
                                throw new InvalidOperationException(
                                    $"Private castle block {image.WorldBlock} changed kind during staging.");

                            NativeArray<byte>.Copy(
                                block.MixedVoxels,
                                block.MixedOffset,
                                mixedMaterials,
                                image.PayloadOffset,
                                VoxelReadGrid.VoxelsPerBlock);
                            NativeArray<ushort>.Copy(
                                block.MixedSurfaceSemantics,
                                block.MixedOffset,
                                mixedSurfaceSemantics,
                                image.PayloadOffset,
                                VoxelReadGrid.VoxelsPerBlock);
                            NativeArray<byte>.Copy(
                                block.MixedBoundarySamples,
                                block.MixedOffset,
                                mixedBoundarySamples,
                                image.PayloadOffset,
                                VoxelReadGrid.VoxelsPerBlock);
                        }
                        finally
                        {
                            if (block.HasPinnedPayload)
                                storage.Reads.ReleasePinnedWorldBlock(in block.Pin);
                        }
                    }

                    return new BuildResult(
                        build.TotalVoxelsWritten,
                        blocks,
                        mixedMaterials,
                        mixedSurfaceSemantics,
                        mixedBoundarySamples);
                }
                catch
                {
                    DisposeStaging(
                        ref mixedMaterials,
                        ref mixedSurfaceSemantics,
                        ref mixedBoundarySamples);
                    throw;
                }
            }
            finally
            {
                storage.Dispose();
                if (ReferenceEquals(_privateStorage, storage))
                    _privateStorage = null;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _cancelRequested = true;
            RenderingComposition.WorldClearing -= Dispose;

            Task<BuildResult> worker = _worker;
            if (worker != null)
            {
                // World teardown is a lifecycle boundary rather than the player-loop frame path.
                // Join so the worker cannot outlive the live material catalogue it borrowed. The
                // worker's finally block owns the private native storage on every exit path.
                try
                {
                    BuildResult pending = worker.GetAwaiter().GetResult();
                    pending?.DisposePayloads();
                }
                catch
                {
                    // A cancelled world's result is intentionally discarded. Runtime failures are
                    // surfaced by Step while the world is active; teardown must still release the
                    // live world even if the background task happened to fault concurrently.
                }
                _worker = null;
                _privateStorage = null;
            }
            else
            {
                _privateStorage?.Dispose();
                _privateStorage = null;
            }

            _result?.DisposePayloads();
            _result = null;
        }

        private void PublishBlock(BlockImage image)
        {
            if (image.Kind == VoxelReadBlockKind.Empty)
            {
                _liveMutations.SetWholeBlock(
                    image.WorldBlock, VoxelGrid.MaterialEmpty, image.MarkHardSurface);
                return;
            }

            if (image.Kind == VoxelReadBlockKind.Uniform)
            {
                _liveMutations.SetWholeBlock(
                    image.WorldBlock, image.UniformMaterial, image.MarkHardSurface);
                return;
            }

            if (!_liveMutations.TryBeginCellBlock(
                    image.WorldBlock, image.MarkHardSurface, out VoxelBlockMutation mutation))
                throw new InvalidOperationException(
                    $"Could not publish castle block {image.WorldBlock}.");

            bool copied = mutation.CopyStoragePayload(
                _result.MixedMaterials,
                _result.MixedSurfaceSemantics,
                _result.MixedBoundarySamples,
                image.PayloadOffset);
            if (!copied)
            {
                _liveMutations.CompletePartialBlock(ref mutation, false);
                throw new InvalidOperationException(
                    $"Could not bulk-publish castle block {image.WorldBlock}.");
            }

            _liveMutations.CompletePartialBlock(ref mutation, true);
        }

        internal static int EstimatePrivateMixedBrickCapacity(
            in GameCastlePlan plan, int sourceRegionCount)
        {
            // A height-field terrain needs at most one mixed surface block per x/z block column in
            // each captured source region. CastlePlanner.EstimateWrites is intentionally a generous
            // content-policy estimate; two mixed blocks per 512 write-equivalents covers partial
            // authoring edges while whole interior fills remain uniform. Keep a fixed reserve for
            // presentation details and future plan variance without returning to a 131k-brick pool.
            long regionColumns = (long)sourceRegionCount
                               * VoxelReadGrid.BlocksPerRegionEdge
                               * VoxelReadGrid.BlocksPerRegionEdge;
            long authoredBlocks =
                (CastlePlanner.EstimateWrites(in plan) + VoxelReadGrid.VoxelsPerBlock - 1)
                / VoxelReadGrid.VoxelsPerBlock;
            long estimate = regionColumns
                          + authoredBlocks * 2L
                          + PrivateMixedBrickSafetyReserve;
            if (estimate < MinimumPrivateMixedBrickCapacity)
                estimate = MinimumPrivateMixedBrickCapacity;
            if (estimate > int.MaxValue)
                throw new InvalidOperationException(
                    $"Castle plan requires an unsupported private mixed-brick capacity: {estimate}.");
            return (int)estimate;
        }

        private static void DisposeStaging(
            ref NativeArray<byte> materials,
            ref NativeArray<ushort> surfaceSemantics,
            ref NativeArray<byte> boundarySamples)
        {
            if (materials.IsCreated) materials.Dispose();
            if (surfaceSemantics.IsCreated) surfaceSemantics.Dispose();
            if (boundarySamples.IsCreated) boundarySamples.Dispose();
            materials = default;
            surfaceSemantics = default;
            boundarySamples = default;
        }

        private static int3[] CastleRegions(in CastlePlan plan)
        {
            int cx = plan.Centre.x;
            int cz = plan.Centre.z;
            int reach = math.max(plan.PlateauRadius + plan.CliffDrop + 8,
                                 VoxelGrid.RegionVoxelEdge);
            int shift = VoxelDimensions.RegionVoxelEdgeLog2;
            int minRx = (cx - reach) >> shift;
            int maxRx = (cx + reach) >> shift;
            int minRz = (cz - reach) >> shift;
            int maxRz = (cz + reach) >> shift;
            var regions = new int3[(maxRx - minRx + 1) * (maxRz - minRz + 1)];
            int cursor = 0;
            for (int rz = minRz; rz <= maxRz; rz++)
            for (int rx = minRx; rx <= maxRx; rx++)
                regions[cursor++] = new int3(rx, 0, rz);
            return regions;
        }

        private sealed class TrackingMutationStore : IRegionMutationStore
        {
            private readonly IRegionMutationStore _inner;
            public readonly Dictionary<int3, bool> Touched = new();

            public TrackingMutationStore(IRegionMutationStore inner) =>
                _inner = inner ?? throw new ArgumentNullException(nameof(inner));

            public bool IsRegionResident(int3 regionCoord) =>
                _inner.IsRegionResident(regionCoord);

            public bool SetWholeBlock(int3 worldBlock, byte material, bool markHardSurface)
            {
                bool changed = _inner.SetWholeBlock(worldBlock, material, markHardSurface);
                if (changed) Touch(worldBlock, markHardSurface);
                return changed;
            }

            public bool SetWholeCellBlock(
                int3 worldBlock, in VoxelCell cell, bool markHardSurface)
            {
                bool changed = _inner.SetWholeCellBlock(
                    worldBlock, in cell, markHardSurface);
                if (changed) Touch(worldBlock, markHardSurface);
                return changed;
            }

            public bool TryBeginPartialBlock(
                int3 worldBlock, byte targetMaterial, bool markHardSurface,
                out VoxelBlockMutation mutation)
            {
                bool ok = _inner.TryBeginPartialBlock(
                    worldBlock, targetMaterial, markHardSurface, out mutation);
                if (ok) Touch(worldBlock, markHardSurface);
                return ok;
            }

            public bool TryBeginCellBlock(
                int3 worldBlock, bool markHardSurface, out VoxelBlockMutation mutation)
            {
                bool ok = _inner.TryBeginCellBlock(
                    worldBlock, markHardSurface, out mutation);
                if (ok) Touch(worldBlock, markHardSurface);
                return ok;
            }

            public bool CompletePartialBlock(
                ref VoxelBlockMutation mutation, bool payloadChanged) =>
                _inner.CompletePartialBlock(ref mutation, payloadChanged);

            private void Touch(int3 worldBlock, bool markHardSurface)
            {
                if (Touched.TryGetValue(worldBlock, out bool previous))
                    Touched[worldBlock] = previous || markHardSurface;
                else
                    Touched.Add(worldBlock, markHardSurface);
            }
        }

        private sealed class BuildResult
        {
            public readonly long TotalVoxelsWritten;
            public readonly List<BlockImage> Blocks;
            public NativeArray<byte> MixedMaterials;
            public NativeArray<ushort> MixedSurfaceSemantics;
            public NativeArray<byte> MixedBoundarySamples;
            private bool _payloadsDisposed;

            public BuildResult(
                long totalVoxelsWritten,
                List<BlockImage> blocks,
                NativeArray<byte> mixedMaterials,
                NativeArray<ushort> mixedSurfaceSemantics,
                NativeArray<byte> mixedBoundarySamples)
            {
                TotalVoxelsWritten = totalVoxelsWritten;
                Blocks = blocks;
                MixedMaterials = mixedMaterials;
                MixedSurfaceSemantics = mixedSurfaceSemantics;
                MixedBoundarySamples = mixedBoundarySamples;
            }

            public void DisposePayloads()
            {
                if (_payloadsDisposed) return;
                _payloadsDisposed = true;
                if (MixedMaterials.IsCreated) MixedMaterials.Dispose();
                if (MixedSurfaceSemantics.IsCreated) MixedSurfaceSemantics.Dispose();
                if (MixedBoundarySamples.IsCreated) MixedBoundarySamples.Dispose();
                MixedMaterials = default;
                MixedSurfaceSemantics = default;
                MixedBoundarySamples = default;
            }
        }

        private readonly struct BlockImage
        {
            public readonly int3 WorldBlock;
            public readonly VoxelReadBlockKind Kind;
            public readonly byte UniformMaterial;
            public readonly int PayloadOffset;
            public readonly bool MarkHardSurface;

            public BlockImage(
                int3 worldBlock,
                VoxelReadBlockKind kind,
                byte uniformMaterial,
                int payloadOffset,
                bool markHardSurface)
            {
                WorldBlock = worldBlock;
                Kind = kind;
                UniformMaterial = uniformMaterial;
                PayloadOffset = payloadOffset;
                MarkHardSurface = markHardSurface;
            }
        }
    }
}
