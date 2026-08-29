using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Rendering.Runtime.GpuVoxel
{
    /// <summary>
    /// World-scoped owner of the production GPU voxel mirror.
    ///
    /// The old path populated one mirror per surface worker by walking every brick in every chunk
    /// immediately before meshing. That multiplied both CPU traversal and voxel uploads by the
    /// number of overlapping chunks/workers. This coordinator instead mirrors Storage once: only
    /// blocks demanded by GPU chunk footprints are recovered, while later edits arrive through the
    /// canonical change feed. GPU chunk extraction only asks whether its exact block footprint is
    /// ready.
    /// </summary>
    internal static class GpuSurfaceMirrorCoordinator
    {
        // Eight step-2 worker-local mirrors previously committed about 98 MiB in aggregate. One
        // 96 MiB shared payload mirror therefore preserves approximately the same worst-ring
        // capacity while eliminating duplication; the compact lookup directory adds ~4%.
        private const long MinimumSharedMirrorBudgetBytes = 96L * 1024L * 1024L;

        // Recovery runs from worker admission on the frame path. Keep the slice deliberately small:
        // the prior 2048-block global-resident sweep measured 0.65-0.77 s in VoxelShowcase. Queueing
        // exact demanded blocks makes every bounded slice advance a waiting chunk instead of scanning
        // the other 262k blocks in a 512^3 Storage region first.
        private const int RecoveryBlocksPerFrame = 64;
        private const int ChangeRecordsPerFrame = 128;

        private static readonly Queue<int3> s_RecoveryBlocks = new();
        private static readonly HashSet<int3> s_QueuedRecoveryBlocks = new();
        private static readonly HashSet<int3> s_ReadyBlocks = new();
        private static readonly Dictionary<int3, int> s_ReadyBlockCountsByRegion = new();
        private static readonly Dictionary<int3, ulong> s_RegionLastSolidChangeVersion = new();
        private static readonly List<VoxelChangeRecord> s_Changes = new(ChangeRecordsPerFrame);
        private static readonly List<int3> s_BlockScratch = new(RecoveryBlocksPerFrame);

        private static GpuVoxelBrickMirror s_Mirror;
        private static IRegionReadSource s_Storage;
        private static IVoxelChangeSource s_ChangeSource;
        private static ulong s_ChangeCursor;
        private static ulong s_MirroredVersion;
        private static ulong s_KnownRegionHistoryFromVersion;
        private static int s_ReferenceCount;
        private static int s_ActiveExtractions;
        private static int s_LastPrepareFrame = -1;
        private static ulong s_LastPrepareWorldVersion;
        private static bool s_LastPrepareResult;

        internal static GpuVoxelBrickMirror Acquire(long requestedBudgetBytes)
        {
            if (s_Mirror == null)
            {
                long budget = Math.Max(MinimumSharedMirrorBudgetBytes,
                                       Math.Max(1L, requestedBudgetBytes) * 16L);
                int slots = GpuBrickBufferLayout.SlotsForBudget(budget);
                s_Mirror = new GpuVoxelBrickMirror(slots);
            }

            s_ReferenceCount++;
            return s_Mirror;
        }

        internal static void ReleaseReference()
        {
            if (s_ReferenceCount > 0) s_ReferenceCount--;
            if (s_ReferenceCount != 0) return;
            ResetWorld(disposeMirror: true);
        }

        /// <summary>
        /// Advances the shared mirror to the current authoritative generation without rewriting any
        /// GPU slot while an earlier count/write can still read it. The caller's snapshot may be an
        /// older global generation because unrelated streamed regions can advance Storage between
        /// snapshot and admission; <see cref="Covers(int3,int,ulong)"/> separately proves that none
        /// of this chunk's covered regions changed after that snapshot.
        /// </summary>
        internal static bool PrepareFromBridge(ulong requiredGeneration)
        {
            if (s_Mirror == null || !SystemInfo.supportsComputeShaders) return false;
            if (!VoxelRenderBridge.TryGetWorld(out VoxelWorldView world)) return false;
            if (world.Storage == null) return false;

            ulong currentGeneration = world.Storage.Version;
            if (requiredGeneration > currentGeneration) return false;

            if (!ReferenceEquals(s_Storage, world.Storage)
                || !ReferenceEquals(s_ChangeSource, VoxelRenderBridge.Changes))
            {
                AttachWorld(world.Storage, VoxelRenderBridge.Changes);
                currentGeneration = world.Storage.Version;
                if (requiredGeneration > currentGeneration) return false;
            }

            int frame = Time.frameCount;
            if (s_LastPrepareFrame == frame && s_LastPrepareWorldVersion == currentGeneration)
                return s_LastPrepareResult
                    && s_MirroredVersion == currentGeneration
                    && RecoveryComplete;
            s_LastPrepareFrame = frame;
            s_LastPrepareWorldVersion = currentGeneration;
            s_LastPrepareResult = false;

            // Journal replay is authoritative and must reach the current generation before demand
            // recovery publishes from Storage. Both are bounded, and neither may mutate the shared
            // mirror underneath an active extraction.
            bool changesSynchronized = s_MirroredVersion == currentGeneration;
            if (!changesSynchronized)
            {
                if (s_ActiveExtractions != 0) return false;
                changesSynchronized = SynchronizeChanges(currentGeneration);
            }
            if (!changesSynchronized) return false;

            // Covers queues only exact blocks an eligible GPU build is actually waiting for. A
            // bounded recovery slice therefore cannot be monopolized by unrelated blocks from the
            // same very large Storage region. Keep admission closed until the queued slice backlog
            // is fully drained; otherwise an already-covered worker can immediately reacquire the
            // shared extraction lease and starve the remaining demanded blocks indefinitely.
            if (!RecoveryComplete)
            {
                if (s_ActiveExtractions != 0) return false;
                ProcessRecovery();
            }

            s_LastPrepareResult = s_MirroredVersion == currentGeneration && RecoveryComplete;
            return s_LastPrepareResult;
        }

        /// <summary>
        /// True when every storage block sampled by the chunk is resident in the current mirror and
        /// no solid-affecting change in any covered region occurred after the caller's snapshot
        /// generation. Missing resident blocks are queued here so recovery follows the exact GPU
        /// footprint rather than the containing 512^3 Storage regions.
        /// </summary>
        internal static bool Covers(int3 brickCacheOrigin, int brickCacheEdge,
                                    ulong requiredGeneration)
        {
            if (s_Storage == null || brickCacheEdge <= 0) return false;
            if (requiredGeneration < s_KnownRegionHistoryFromVersion) return false;

            bool covered = true;
            int3 last = brickCacheOrigin + new int3(brickCacheEdge - 1);
            int shift = VoxelReadGrid.BlocksPerRegionEdgeLog2;
            for (int z = brickCacheOrigin.z; z <= last.z; z++)
            for (int y = brickCacheOrigin.y; y <= last.y; y++)
            for (int x = brickCacheOrigin.x; x <= last.x; x++)
            {
                int3 block = new int3(x, y, z);
                int3 region = block >> shift;
                if (s_RegionLastSolidChangeVersion.TryGetValue(region, out ulong changedAt)
                    && changedAt > requiredGeneration)
                    return false;

                if (s_ReadyBlocks.Contains(block)) continue;
                covered = false;
                if (s_Storage.IsRegionResident(region)) QueueRecoveryBlock(block);
            }
            return covered;
        }

        internal static void BeginExtraction() => s_ActiveExtractions++;

        internal static void EndExtraction()
        {
            if (s_ActiveExtractions > 0) s_ActiveExtractions--;
        }

        // Retained for existing diagnostics. Readiness itself is block-granular now.
        internal static int ReadyRegionCount => s_ReadyBlockCountsByRegion.Count;
        internal static int ReadyBlockCount => s_ReadyBlocks.Count;
        internal static int ActiveExtractions => s_ActiveExtractions;
        internal static ulong MirroredVersion => s_MirroredVersion;
        internal static bool RecoveryComplete => s_QueuedRecoveryBlocks.Count == 0;

        private static void AttachWorld(IRegionReadSource storage, IVoxelChangeSource changes)
        {
            ResetWorld(disposeMirror: false);
            s_Mirror.Clear();
            s_Storage = storage;
            s_ChangeSource = changes;
            s_ChangeCursor = changes?.CurrentVersion ?? storage.Version;
            s_MirroredVersion = storage.Version;
            s_KnownRegionHistoryFromVersion = storage.Version;
            BeginDemandRecovery(clearMirror: false);
        }

        private static bool SynchronizeChanges(ulong targetGeneration)
        {
            if (s_Storage == null) return false;

            if (s_ChangeSource == null)
            {
                // Without a journal there is no exact incremental replay. Rebuild from current
                // authoritative state and reject snapshots older than that new known-history floor.
                BeginDemandRecovery(clearMirror: true);
                s_RegionLastSolidChangeVersion.Clear();
                s_KnownRegionHistoryFromVersion = targetGeneration;
                s_MirroredVersion = targetGeneration;
                return true;
            }

            s_Changes.Clear();
            bool valid = s_ChangeSource.ReadSince(ref s_ChangeCursor, s_Changes,
                                                  ChangeRecordsPerFrame, out bool hasMore);
            if (!valid)
            {
                // Retention was overrun. Exact history before the current state is unknowable, so
                // rebuild and raise the history floor rather than admitting an old chunk snapshot.
                BeginDemandRecovery(clearMirror: true);
                s_RegionLastSolidChangeVersion.Clear();
                s_KnownRegionHistoryFromVersion = targetGeneration;
                s_MirroredVersion = targetGeneration;
                return true;
            }

            for (int i = 0; i < s_Changes.Count; i++)
                ApplyChange(s_Changes[i]);

            if (hasMore || s_ChangeCursor < targetGeneration) return false;
            s_MirroredVersion = targetGeneration;
            return true;
        }

        private static void ApplyChange(in VoxelChangeRecord change)
        {
            // Water does not participate in solid density unless another solid-affecting bit is set.
            VoxelChangeKind solid = change.Kind & ~VoxelChangeKind.Water;
            if (solid == VoxelChangeKind.None) return;

            int3 region = change.Region;
            if (!s_RegionLastSolidChangeVersion.TryGetValue(region, out ulong previous)
                || change.Version > previous)
                s_RegionLastSolidChangeVersion[region] = change.Version;

            bool resident = s_Storage.IsRegionResident(region);
            if (!resident)
            {
                InvalidateRegion(region, requeueReadyBlocks: false);
                return;
            }

            // Residency replacement can swap the compact backing wholesale. Invalidate only blocks
            // this GPU mirror actually published, then requeue those exact demanded coordinates.
            if ((change.Kind & VoxelChangeKind.Residency) != 0)
            {
                InvalidateRegion(region, requeueReadyBlocks: true);
                return;
            }

            int3 minBlock = change.MinVoxel >> VoxelReadGrid.BlockEdgeLog2;
            int3 maxBlock = (change.MaxVoxelExclusive - 1) >> VoxelReadGrid.BlockEdgeLog2;
            s_BlockScratch.Clear();
            foreach (int3 block in s_ReadyBlocks)
            {
                if (math.all(block >= minBlock) && math.all(block <= maxBlock))
                    s_BlockScratch.Add(block);
            }

            for (int i = 0; i < s_BlockScratch.Count; i++)
            {
                int3 block = s_BlockScratch[i];
                UnmarkReadyBlock(block);
                if (PublishPinnedBlock(block, change.Version)) MarkReadyBlock(block);
                else QueueRecoveryBlock(block);
            }
            s_BlockScratch.Clear();
        }

        private static void BeginDemandRecovery(bool clearMirror)
        {
            s_ReadyBlocks.Clear();
            s_ReadyBlockCountsByRegion.Clear();
            s_RecoveryBlocks.Clear();
            s_QueuedRecoveryBlocks.Clear();
            s_BlockScratch.Clear();
            if (clearMirror) s_Mirror.Clear();
        }

        private static void QueueRecoveryBlock(int3 block)
        {
            UnmarkReadyBlock(block);
            if (!s_QueuedRecoveryBlocks.Add(block)) return;
            s_RecoveryBlocks.Enqueue(block);

            // Covers can discover new demand after this frame's PrepareFromBridge result was cached.
            // Close that cached admission immediately so later workers in the same frame cannot
            // bypass the new backlog and keep an extraction continuously active.
            s_LastPrepareResult = false;
        }

        private static void InvalidateRegion(int3 region, bool requeueReadyBlocks)
        {
            int shift = VoxelReadGrid.BlocksPerRegionEdgeLog2;
            s_BlockScratch.Clear();
            foreach (int3 block in s_ReadyBlocks)
            {
                if (math.all((block >> shift) == region)) s_BlockScratch.Add(block);
            }

            bool requeue = requeueReadyBlocks && s_Storage.IsRegionResident(region);
            for (int i = 0; i < s_BlockScratch.Count; i++)
            {
                int3 block = s_BlockScratch[i];
                UnmarkReadyBlock(block);
                s_Mirror.Remove(block);
                if (requeue) QueueRecoveryBlock(block);
            }
            s_BlockScratch.Clear();

            if (!requeue) CancelQueuedRegion(region);
        }

        private static void CancelQueuedRegion(int3 region)
        {
            int shift = VoxelReadGrid.BlocksPerRegionEdgeLog2;
            s_BlockScratch.Clear();
            foreach (int3 block in s_QueuedRecoveryBlocks)
            {
                if (math.all((block >> shift) == region)) s_BlockScratch.Add(block);
            }
            for (int i = 0; i < s_BlockScratch.Count; i++)
                s_QueuedRecoveryBlocks.Remove(s_BlockScratch[i]);
            s_BlockScratch.Clear();

            // Queue<T> cannot remove arbitrary entries. If every authoritative queued coordinate
            // was cancelled, discard all physical stale entries now; otherwise ProcessRecovery skips
            // cancelled entries without spending recovery budget on them.
            if (s_QueuedRecoveryBlocks.Count == 0) s_RecoveryBlocks.Clear();
        }

        private static void ProcessRecovery()
        {
            if (s_Storage == null) return;

            int remaining = RecoveryBlocksPerFrame;
            int shift = VoxelReadGrid.BlocksPerRegionEdgeLog2;
            ulong generation = s_Storage.Version;
            bool hasView = false;
            int3 viewRegion = default;
            RegionReadView view = default;

            while (remaining > 0 && s_RecoveryBlocks.Count > 0)
            {
                int3 worldBlock = s_RecoveryBlocks.Dequeue();
                // Cancelled/stale queue entries stay physically in Queue<T>; the set is authoritative
                // and lets cancellation remain O(1) without consuming the bounded recovery slice.
                if (!s_QueuedRecoveryBlocks.Remove(worldBlock)) continue;

                int3 region = worldBlock >> shift;
                if (!s_Storage.IsRegionResident(region))
                {
                    s_Mirror.Remove(worldBlock);
                    continue;
                }

                if (s_Storage.Version != generation)
                {
                    QueueRecoveryBlock(worldBlock);
                    return;
                }

                if (!hasView || !viewRegion.Equals(region))
                {
                    if (!s_Storage.TryAcquireRegion(region, out view)) continue;
                    if (view.Version != generation)
                    {
                        QueueRecoveryBlock(worldBlock);
                        return;
                    }
                    viewRegion = region;
                    hasView = true;
                }

                int3 regionOrigin = region << shift;
                int3 localBlock = worldBlock - regionOrigin;
                bool published = PublishRegionBlock(in view, worldBlock, localBlock, generation);
                remaining--;
                if (published && s_Storage.IsRegionResident(region)) MarkReadyBlock(worldBlock);
            }
        }

        private static void MarkReadyBlock(int3 block)
        {
            if (!s_ReadyBlocks.Add(block)) return;
            int3 region = block >> VoxelReadGrid.BlocksPerRegionEdgeLog2;
            if (s_ReadyBlockCountsByRegion.TryGetValue(region, out int count))
                s_ReadyBlockCountsByRegion[region] = count + 1;
            else
                s_ReadyBlockCountsByRegion[region] = 1;
        }

        private static void UnmarkReadyBlock(int3 block)
        {
            if (!s_ReadyBlocks.Remove(block)) return;
            int3 region = block >> VoxelReadGrid.BlocksPerRegionEdgeLog2;
            if (!s_ReadyBlockCountsByRegion.TryGetValue(region, out int count)) return;
            if (count <= 1) s_ReadyBlockCountsByRegion.Remove(region);
            else s_ReadyBlockCountsByRegion[region] = count - 1;
        }

        private static bool PublishRegionBlock(in RegionReadView view, int3 worldBlock,
                                               int3 localBlock, ulong generation)
        {
            if (!view.TryGetBlock(localBlock, out VoxelReadBlock block))
            {
                s_Mirror.Remove(worldBlock);
                return false;
            }

            // Empty and uniform blocks carry no mixed payload. Classify them from the borrowed
            // region view and update only directory metadata; reserve Storage pin/COW work for the
            // comparatively sparse mixed blocks that actually need a voxel payload copied to GPU.
            if (block.Kind != VoxelReadBlockKind.Mixed)
            {
                VoxelBrickDelta delta = block.Kind == VoxelReadBlockKind.Empty
                    ? VoxelBrickDelta.EmptyAt(worldBlock, generation)
                    : VoxelBrickDelta.UniformAt(worldBlock, generation, block.UniformMaterial);
                GpuBrickPublish result = s_Mirror.Publish(
                    delta, default, default, default, 0, hasPayload: false);
                return result is not (GpuBrickPublish.NoSlot
                                   or GpuBrickPublish.PayloadMissing
                                   or GpuBrickPublish.Stale);
            }

            return PublishPinnedBlock(worldBlock, generation);
        }

        private static bool PublishPinnedBlock(int3 worldBlock, ulong generation)
        {
            if (!s_Storage.TryPinWorldBlock(worldBlock, out PinnedVoxelReadBlock block))
            {
                s_Mirror.Remove(worldBlock);
                return false;
            }

            try
            {
                VoxelBrickDelta delta = block.Kind switch
                {
                    VoxelReadBlockKind.Empty => VoxelBrickDelta.EmptyAt(worldBlock, generation),
                    VoxelReadBlockKind.Uniform =>
                        VoxelBrickDelta.UniformAt(worldBlock, generation, block.UniformMaterial),
                    _ => VoxelBrickDelta.MixedAt(worldBlock, generation, block.MixedOffset),
                };

                GpuBrickPublish result = s_Mirror.Publish(delta, block);
                if (result is GpuBrickPublish.NoSlot or GpuBrickPublish.PayloadMissing
                    or GpuBrickPublish.Stale)
                    return false;

                // The shared world mirror is authoritative for its ready demanded blocks. Keep mixed
                // slots stable until that coordinate changes/unloads; capacity pressure therefore
                // causes explicit CPU fallback instead of recycling a slot an in-flight shader reads.
                if (block.Kind == VoxelReadBlockKind.Mixed) s_Mirror.Pin(worldBlock);
                return true;
            }
            finally
            {
                if (block.HasPinnedPayload) s_Storage.ReleasePinnedWorldBlock(block.Pin);
            }
        }

        private static void ResetWorld(bool disposeMirror)
        {
            s_Storage = null;
            s_ChangeSource = null;
            s_ChangeCursor = 0;
            s_MirroredVersion = 0;
            s_KnownRegionHistoryFromVersion = 0;
            s_ActiveExtractions = 0;
            s_LastPrepareFrame = -1;
            s_LastPrepareWorldVersion = 0;
            s_LastPrepareResult = false;
            s_RecoveryBlocks.Clear();
            s_QueuedRecoveryBlocks.Clear();
            s_ReadyBlocks.Clear();
            s_ReadyBlockCountsByRegion.Clear();
            s_RegionLastSolidChangeVersion.Clear();
            s_Changes.Clear();
            s_BlockScratch.Clear();

            if (!disposeMirror || s_Mirror == null) return;
            s_Mirror.Dispose();
            s_Mirror = null;
        }
    }
}
