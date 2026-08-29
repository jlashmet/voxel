using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Rendering.Runtime.GpuVoxel
{
    /// <summary>
    /// World-scoped, demand-filled GPU voxel mirror used by all near-ring surface workers.
    ///
    /// Chunk admission only names the brick footprint it needs. This coordinator copies those
    /// bricks from one borrowed region view under a frame-time and upload-byte budget, then keeps
    /// them coherent from Storage's compact change journal. It never walks a whole 512-voxel-edge
    /// region to satisfy a 64-voxel chunk and never builds a per-worker CPU snapshot.
    /// </summary>
    internal static class GpuSurfaceMirrorCoordinator
    {
        private const long MinimumSharedMirrorBudgetBytes = 96L * 1024L * 1024L;
        private const int ChangeRecordsPerFrame = 128;
        private const int TrackedBlockCapacity = 65536;
        private const int TrackedRegionCapacity = 128;
        private const int BlocksPerTrackedRegionCapacity = 8192;
        private const int CoverageChecksPerPoll = 128;
        internal const int DefaultUploadBudgetBytes = 256 * 1024;

        private static readonly Queue<int3> s_RecoveryRegions = new(TrackedRegionCapacity);
        private static readonly HashSet<int3> s_QueuedRecoveryRegions = new(TrackedRegionCapacity);
        private static readonly Dictionary<int3, Queue<int3>> s_PendingBlocksByRegion =
            new(TrackedRegionCapacity);
        private static readonly Dictionary<ActiveFootprint, int> s_DemandFootprints =
            new(32);
        private static readonly HashSet<int3> s_PendingBlocks = new(TrackedBlockCapacity);
        private static readonly HashSet<int3> s_ReadyBlocks = new(TrackedBlockCapacity);
        private static readonly Dictionary<int3, HashSet<int3>> s_ReadyBlocksByRegion =
            new(TrackedRegionCapacity);
        private static readonly Stack<Queue<int3>> s_BlockQueuePool =
            new(TrackedRegionCapacity);
        private static readonly Stack<HashSet<int3>> s_ReadySetPool =
            new(TrackedRegionCapacity);
        private static readonly List<int3> s_ChangedReadyScratch =
            new(BlocksPerTrackedRegionCapacity);
        private static readonly Queue<int3> s_ReadyResidencyOrder = new(TrackedBlockCapacity);
        private static readonly Queue<int3> s_MixedResidencyOrder = new(TrackedBlockCapacity);
        private static readonly HashSet<int3> s_MixedReadyBlocks = new(TrackedBlockCapacity);
        private static readonly Dictionary<int3, ulong> s_RegionLastSolidChangeVersion =
            new(TrackedRegionCapacity);
        private static readonly Dictionary<int3, int> s_ActiveRegionReaders =
            new(TrackedRegionCapacity);
        private static readonly Dictionary<ActiveFootprint, int> s_ActiveFootprints =
            new(32);
        private static readonly List<VoxelChangeRecord> s_Changes = new(ChangeRecordsPerFrame);

        private static GpuVoxelBrickMirror s_Mirror;
        private static IRegionReadSource s_Storage;
        private static IVoxelChangeSource s_ChangeSource;
        private static ulong s_ChangeCursor;
        private static ulong s_MirroredVersion;
        private static ulong s_KnownRegionHistoryFromVersion;
        private static int s_ReferenceCount;
        private static int s_ActiveExtractionCount;
        private static int s_LastPrepareFrame = -1;
        private static int s_LastExtractionDispatchFrame = -1;
        private static uint s_CoverageEpoch;
        private static ulong s_OptionalNonResidentHaloBlocksAccepted;

        private readonly struct ActiveFootprint : IEquatable<ActiveFootprint>
        {
            internal readonly int3 Origin;
            internal readonly int Edge;

            internal ActiveFootprint(int3 origin, int edge)
            {
                Origin = origin;
                Edge = edge;
            }

            public bool Equals(ActiveFootprint other) =>
                Edge == other.Edge && math.all(Origin == other.Origin);

            public override bool Equals(object obj) =>
                obj is ActiveFootprint other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(Origin.GetHashCode(), Edge);
        }

        internal static GpuVoxelBrickMirror Acquire(long requestedBudgetBytes)
        {
            EnsureCollectionPools();
            if (s_Mirror == null)
            {
                long budget = Math.Max(MinimumSharedMirrorBudgetBytes,
                                       Math.Max(1L, requestedBudgetBytes) * 16L);
                s_Mirror = new GpuVoxelBrickMirror(
                    GpuBrickBufferLayout.SlotsForBudget(budget));
            }
            s_ReferenceCount++;
            return s_Mirror;
        }

        internal static void ReleaseReference()
        {
            if (s_ReferenceCount > 0) s_ReferenceCount--;
            if (s_ReferenceCount == 0) ResetWorld(disposeMirror: true);
        }

        /// <summary>Attaches the current world. Heavy mirror work remains in PrepareFrame.</summary>
        internal static bool PrepareFromBridge(ulong requiredGeneration)
        {
            if (s_Mirror == null || !SystemInfo.supportsComputeShaders) return false;
            if (!VoxelRenderBridge.TryGetWorld(out VoxelWorldView world)
                || world.Storage == null)
                return false;
            if (requiredGeneration > world.Storage.Version) return false;

            if (!ReferenceEquals(s_Storage, world.Storage)
                || !ReferenceEquals(s_ChangeSource, VoxelRenderBridge.Changes))
                AttachWorld(world.Storage, VoxelRenderBridge.Changes);
            return requiredGeneration <= world.Storage.Version;
        }

        /// <summary>
        /// Replays edits and copies requested bricks once per rendered frame. SetData/scatter is
        /// performed here, never during a worker's chunk admission.
        /// </summary>
        internal static void PrepareFrame(IRegionReadSource storage, IVoxelChangeSource changes,
                                          int frame, double budgetMs,
                                          int uploadBudgetBytes = DefaultUploadBudgetBytes)
        {
            if (s_Mirror == null || storage == null || budgetMs <= 0.0) return;
            if (!ReferenceEquals(s_Storage, storage) || !ReferenceEquals(s_ChangeSource, changes))
                AttachWorld(storage, changes);
            if (s_LastPrepareFrame == frame) return;
            s_LastPrepareFrame = frame;

            double deadline = Time.realtimeSinceStartupAsDouble + budgetMs * 0.001;
            if (s_MirroredVersion != storage.Version
                && Time.realtimeSinceStartupAsDouble < deadline)
                SynchronizeChanges(storage.Version);
            if (Time.realtimeSinceStartupAsDouble < deadline)
                ProcessRecovery(deadline, Math.Max(0, uploadBudgetBytes));
            s_Mirror.FlushPendingUploads();
        }

        internal static void RequestCoverage(int3 brickCacheOrigin, int brickCacheEdge,
                                             int3 coreMinVoxel,
                                             int3 coreMaxVoxelExclusive)
        {
            if (brickCacheEdge <= 0) return;
            ChangeDemandFootprint(brickCacheOrigin, brickCacheEdge, 1);
        }

        internal static void ReleaseCoverage(int3 brickCacheOrigin, int brickCacheEdge,
                                             int3 coreMinVoxel,
                                             int3 coreMaxVoxelExclusive)
        {
            if (brickCacheEdge <= 0) return;
            ChangeDemandFootprint(brickCacheOrigin, brickCacheEdge, -1);

            if (s_DemandFootprints.Count == 0) ClearRecoveryQueues();
        }

        internal static bool Covers(int3 brickCacheOrigin, int brickCacheEdge,
                                    int3 coreMinVoxel, int3 coreMaxVoxelExclusive,
                                    ulong requiredGeneration, ref int scanCursor,
                                    ref bool roundIncomplete)
        {
            if (s_Storage == null || brickCacheEdge <= 0
                || requiredGeneration < s_KnownRegionHistoryFromVersion)
                return false;

            int regionShift = VoxelReadGrid.BlocksPerRegionEdgeLog2;
            int blockShift = VoxelReadGrid.BlockEdgeLog2;
            int blockCount = brickCacheEdge * brickCacheEdge * brickCacheEdge;
            int stop = Math.Min(blockCount, scanCursor + CoverageChecksPerPoll);
            for (; scanCursor < stop; scanCursor++)
            {
                int x = scanCursor % brickCacheEdge;
                int yz = scanCursor / brickCacheEdge;
                int y = yz % brickCacheEdge;
                int z = yz / brickCacheEdge;
                int3 block = new(x, y, z);
                block += brickCacheOrigin;
                int3 region = block >> regionShift;
                if (!s_Storage.IsRegionResident(region))
                {
                    int3 blockMinVoxel = block << blockShift;
                    int3 blockMaxVoxelExclusive =
                        blockMinVoxel + new int3(VoxelReadGrid.BlockEdge);
                    bool intersectsCore = math.all(blockMaxVoxelExclusive > coreMinVoxel)
                                       && math.all(blockMinVoxel < coreMaxVoxelExclusive);
                    if (intersectsCore) roundIncomplete = true;
                    else s_OptionalNonResidentHaloBlocksAccepted++;
                    continue;
                }
                if (s_PendingBlocks.Contains(block) || !s_ReadyBlocks.Contains(block))
                {
                    QueueRecoveryBlock(block);
                    roundIncomplete = true;
                    continue;
                }
                if (s_RegionLastSolidChangeVersion.TryGetValue(region, out ulong changedAt)
                    && changedAt > requiredGeneration)
                    roundIncomplete = true;
            }

            if (scanCursor < blockCount) return false;
            bool covered = !roundIncomplete;
            scanCursor = 0;
            roundIncomplete = false;
            return covered;
        }

        internal static void BeginExtraction(int3 brickCacheOrigin, int brickCacheEdge)
        {
            s_ActiveExtractionCount++;
            ChangeActiveFootprint(brickCacheOrigin, brickCacheEdge, 1);
            ChangeActiveRegionReaders(brickCacheOrigin, brickCacheEdge, 1);
        }

        internal static bool TryReserveExtractionDispatch(int frame)
        {
            if (s_LastExtractionDispatchFrame == frame) return false;
            s_LastExtractionDispatchFrame = frame;
            return true;
        }

        internal static void EndExtraction(int3 brickCacheOrigin, int brickCacheEdge)
        {
            if (s_ActiveExtractionCount > 0) s_ActiveExtractionCount--;
            ChangeActiveFootprint(brickCacheOrigin, brickCacheEdge, -1);
            ChangeActiveRegionReaders(brickCacheOrigin, brickCacheEdge, -1);
        }

        internal static int ReadyRegionCount => s_ReadyBlocksByRegion.Count;
        internal static int ReadyBlockCount => s_ReadyBlocks.Count;
        internal static int PendingBlockCount => s_PendingBlocks.Count;
        internal static int MirrorSlotCapacity => s_Mirror?.SlotCapacity ?? 0;
        internal static int ResidentMixedBrickCount => s_Mirror?.ResidentBricks ?? 0;
        internal static int ActiveRegionCount => s_ActiveRegionReaders.Count;
        internal static int ActiveExtractions => s_ActiveExtractionCount;
        internal static ulong MirroredVersion => s_MirroredVersion;
        internal static uint CoverageEpoch => s_CoverageEpoch;
        internal static bool RecoveryComplete => s_PendingBlocks.Count == 0;
        internal static ulong OptionalNonResidentHaloBlocksAccepted =>
            s_OptionalNonResidentHaloBlocksAccepted;

        private static void AttachWorld(IRegionReadSource storage, IVoxelChangeSource changes)
        {
            ResetWorld(disposeMirror: false);
            unchecked { s_CoverageEpoch++; }
            s_Mirror.Clear();
            s_Storage = storage;
            s_ChangeSource = changes;
            s_ChangeCursor = changes?.CurrentVersion ?? storage.Version;
            s_MirroredVersion = storage.Version;
            s_KnownRegionHistoryFromVersion = storage.Version;
        }

        private static bool SynchronizeChanges(ulong targetGeneration)
        {
            if (s_Storage == null) return false;
            if (s_ChangeSource == null)
            {
                InvalidateAll(targetGeneration);
                return true;
            }

            s_Changes.Clear();
            bool valid = s_ChangeSource.ReadSince(ref s_ChangeCursor, s_Changes,
                                                  ChangeRecordsPerFrame, out bool hasMore);
            if (!valid)
            {
                InvalidateAll(targetGeneration);
                return true;
            }

            for (int i = 0; i < s_Changes.Count; i++) ApplyChange(s_Changes[i]);
            if (hasMore || s_ChangeCursor < targetGeneration) return false;
            s_MirroredVersion = targetGeneration;
            return true;
        }

        private static void InvalidateAll(ulong targetGeneration)
        {
            unchecked { s_CoverageEpoch++; }
            s_Mirror.Clear();
            ClearReadyBlocks();
            s_ReadyResidencyOrder.Clear();
            s_MixedResidencyOrder.Clear();
            s_MixedReadyBlocks.Clear();
            ClearRecoveryQueues();
            s_RegionLastSolidChangeVersion.Clear();
            s_KnownRegionHistoryFromVersion = targetGeneration;
            s_MirroredVersion = targetGeneration;
        }

        private static void ApplyChange(in VoxelChangeRecord change)
        {
            if ((change.Kind & ~VoxelChangeKind.Water) == VoxelChangeKind.None) return;
            if (!s_RegionLastSolidChangeVersion.TryGetValue(change.Region, out ulong previous)
                || change.Version > previous)
                s_RegionLastSolidChangeVersion[change.Region] = change.Version;

            if (!s_ReadyBlocksByRegion.TryGetValue(
                    change.Region, out HashSet<int3> readyInRegion))
                return;
            bool wholeRegion = math.any(change.MaxVoxelExclusive <= change.MinVoxel);
            int3 min = change.MinVoxel >> VoxelReadGrid.BlockEdgeLog2;
            int3 max = wholeRegion
                ? default
                : (change.MaxVoxelExclusive - 1) >> VoxelReadGrid.BlockEdgeLog2;
            s_ChangedReadyScratch.Clear();
            int3 size = wholeRegion ? default : max - min + 1;
            long changedVolume = wholeRegion
                ? long.MaxValue : (long)size.x * size.y * size.z;
            if (!wholeRegion && changedVolume < readyInRegion.Count)
            {
                for (int z = min.z; z <= max.z; z++)
                for (int y = min.y; y <= max.y; y++)
                for (int x = min.x; x <= max.x; x++)
                {
                    int3 block = new(x, y, z);
                    if (readyInRegion.Contains(block)) s_ChangedReadyScratch.Add(block);
                }
            }
            else
            {
                foreach (int3 block in readyInRegion)
                {
                    if (wholeRegion || math.all(block >= min & block <= max))
                        s_ChangedReadyScratch.Add(block);
                }
            }
            if (s_ChangedReadyScratch.Count > 0)
                unchecked { s_CoverageEpoch++; }
            for (int i = 0; i < s_ChangedReadyScratch.Count; i++)
            {
                int3 block = s_ChangedReadyScratch[i];
                // An in-flight dispatch may still resolve this exact directory entry. Keep its
                // immutable old generation reachable until that dispatch releases the footprint;
                // changedAt above prevents any new request for the old generation from admitting.
                if (!IsBlockActive(block))
                {
                    RemoveReadyBlock(block);
                    s_MixedReadyBlocks.Remove(block);
                }
                QueueRecoveryBlock(block);
            }
        }

        private static void QueueRecoveryBlock(int3 block)
        {
            if (!IsBlockDemanded(block)) return;
            if (!s_PendingBlocks.Add(block)) return;
            int3 region = block >> VoxelReadGrid.BlocksPerRegionEdgeLog2;
            if (!s_PendingBlocksByRegion.TryGetValue(region, out Queue<int3> blocks))
            {
                blocks = s_BlockQueuePool.Count > 0
                    ? s_BlockQueuePool.Pop()
                    : new Queue<int3>(BlocksPerTrackedRegionCapacity);
                s_PendingBlocksByRegion.Add(region, blocks);
            }
            blocks.Enqueue(block);
            if (s_QueuedRecoveryRegions.Add(region)) s_RecoveryRegions.Enqueue(region);
        }

        private static void ProcessRecovery(double deadlineSeconds, int uploadBudgetBytes)
        {
            int stagedBytes = 0;
            int consecutiveBlockedRegions = 0;
            while (s_RecoveryRegions.Count > 0
                   && Time.realtimeSinceStartupAsDouble < deadlineSeconds)
            {
                int3 region = s_RecoveryRegions.Dequeue();
                s_QueuedRecoveryRegions.Remove(region);
                if (!s_PendingBlocksByRegion.TryGetValue(region, out Queue<int3> blocks)
                    || blocks.Count == 0)
                    continue;

                bool resident = s_Storage.TryAcquireRegion(region, out RegionReadView view);
                int blocksLeftToInspect = blocks.Count;
                bool madeProgress = false;
                while (blocks.Count > 0
                       && blocksLeftToInspect-- > 0
                       && Time.realtimeSinceStartupAsDouble < deadlineSeconds)
                {
                    if (stagedBytes >= uploadBudgetBytes && stagedBytes > 0)
                    {
                        RequeueRegion(region);
                        return;
                    }

                    int3 worldBlock = blocks.Dequeue();
                    // Queue<T> cannot erase arbitrary coordinates when a pending chunk is
                    // superseded. The demand set is authoritative; discard the physical stale
                    // entry without borrowing Storage or consuming upload bytes.
                    if (!s_PendingBlocks.Contains(worldBlock)
                        || !IsBlockDemanded(worldBlock))
                    {
                        s_PendingBlocks.Remove(worldBlock);
                        madeProgress = true;
                        continue;
                    }
                    if (IsBlockActive(worldBlock))
                    {
                        blocks.Enqueue(worldBlock);
                        continue;
                    }

                    madeProgress = true;
                    s_PendingBlocks.Remove(worldBlock);
                    int3 localBlock = worldBlock
                        - (region << VoxelReadGrid.BlocksPerRegionEdgeLog2);
                    if (!resident)
                    {
                        s_Mirror.Remove(worldBlock);
                        RemoveReadyBlock(worldBlock);
                        continue;
                    }
                    if (!view.TryGetBlock(localBlock, out VoxelReadBlock block))
                    {
                        QueueRecoveryBlock(worldBlock);
                        continue;
                    }

                    VoxelBrickDelta delta = block.Kind switch
                    {
                        VoxelReadBlockKind.Empty =>
                            VoxelBrickDelta.EmptyAt(worldBlock, view.Version),
                        VoxelReadBlockKind.Uniform =>
                            VoxelBrickDelta.UniformAt(
                                worldBlock, view.Version, block.UniformMaterial),
                        _ => VoxelBrickDelta.MixedAt(worldBlock, view.Version, 0),
                    };
                    GpuBrickPublish result = PublishBlock(
                        in delta, in view, localBlock, block.Kind);
                    if (result is GpuBrickPublish.NoSlot or GpuBrickPublish.PayloadMissing
                        or GpuBrickPublish.Stale)
                    {
                        QueueRecoveryBlock(worldBlock);
                        RequeueRegion(region);
                        return;
                    }

                    AddReadyBlock(worldBlock);
                    if (block.Kind == VoxelReadBlockKind.Mixed)
                    {
                        if (s_MixedReadyBlocks.Add(worldBlock))
                            s_MixedResidencyOrder.Enqueue(worldBlock);
                    }
                    else
                    {
                        s_MixedReadyBlocks.Remove(worldBlock);
                    }
                    if (result == GpuBrickPublish.Uploaded)
                    {
                        s_Mirror.Pin(worldBlock);
                        stagedBytes += GpuBrickBufferLayout.BytesPerMixedBrick;
                    }
                }

                if (blocks.Count > 0) RequeueRegion(region);
                else
                {
                    s_PendingBlocksByRegion.Remove(region);
                    blocks.Clear();
                    s_BlockQueuePool.Push(blocks);
                }

                if (madeProgress)
                {
                    consecutiveBlockedRegions = 0;
                }
                else if (blocks.Count > 0)
                {
                    consecutiveBlockedRegions++;
                    if (consecutiveBlockedRegions >= s_RecoveryRegions.Count) return;
                }
            }
        }

        private static GpuBrickPublish PublishBlock(
            in VoxelBrickDelta delta, in RegionReadView view, int3 localBlock,
            VoxelReadBlockKind kind)
        {
            GpuBrickPublish result = kind == VoxelReadBlockKind.Mixed
                ? s_Mirror.Publish(delta, in view, localBlock)
                : s_Mirror.Publish(delta, default(NativeArray<byte>),
                                   default(NativeArray<ushort>),
                                   default(NativeArray<byte>), 0, false);
            if (result != GpuBrickPublish.NoSlot || !TryEvictInactiveMixedBlock())
                return result;
            return kind == VoxelReadBlockKind.Mixed
                ? s_Mirror.Publish(delta, in view, localBlock)
                : s_Mirror.Publish(delta, default(NativeArray<byte>),
                                   default(NativeArray<ushort>),
                                   default(NativeArray<byte>), 0, false);
        }

        /// <summary>
        /// Geometry no longer reads its source bricks after count/write completes, so a cold brick
        /// outside every active extraction may be reclaimed. Removing its directory entry and
        /// readiness bit together prevents a reused slot from being addressed by the old world
        /// coordinate; the coverage epoch makes pending chunks request it again if still needed.
        /// </summary>
        private static bool TryEvictInactiveMixedBlock()
        {
            int attempts = s_MixedResidencyOrder.Count;
            while (attempts-- > 0 && s_MixedResidencyOrder.Count > 0)
            {
                int3 block = s_MixedResidencyOrder.Dequeue();
                if (!s_MixedReadyBlocks.Contains(block)) continue;
                if (IsBlockDemanded(block) || IsBlockActive(block))
                {
                    s_MixedResidencyOrder.Enqueue(block);
                    continue;
                }

                s_MixedReadyBlocks.Remove(block);
                RemoveReadyBlock(block);
                s_Mirror.Remove(block);
                unchecked { s_CoverageEpoch++; }
                return true;
            }
            return false;
        }

        private static void RequeueRegion(int3 region)
        {
            if (s_QueuedRecoveryRegions.Add(region)) s_RecoveryRegions.Enqueue(region);
        }

        private static void ChangeDemandFootprint(int3 origin, int edge, int delta)
        {
            var footprint = new ActiveFootprint(origin, edge);
            s_DemandFootprints.TryGetValue(footprint, out int readers);
            readers += delta;
            if (readers > 0)
            {
                s_DemandFootprints[footprint] = readers;
                return;
            }

            s_DemandFootprints.Remove(footprint);
        }

        private static bool IsBlockDemanded(int3 block)
        {
            foreach (ActiveFootprint footprint in s_DemandFootprints.Keys)
            {
                int3 end = footprint.Origin + new int3(footprint.Edge);
                if (math.all(block >= footprint.Origin & block < end)) return true;
            }
            return false;
        }

        private static void AddReadyBlock(int3 block)
        {
            if (s_ReadyBlocks.Contains(block)) return;
            if (s_ReadyBlocks.Count >= TrackedBlockCapacity)
                TryEvictInactiveReadyBlock();
            if (!s_ReadyBlocks.Add(block)) return;
            s_ReadyResidencyOrder.Enqueue(block);
            int3 region = block >> VoxelReadGrid.BlocksPerRegionEdgeLog2;
            if (!s_ReadyBlocksByRegion.TryGetValue(region, out HashSet<int3> blocks))
            {
                blocks = s_ReadySetPool.Count > 0
                    ? s_ReadySetPool.Pop()
                    : new HashSet<int3>(BlocksPerTrackedRegionCapacity);
                s_ReadyBlocksByRegion.Add(region, blocks);
            }
            blocks.Add(block);
        }

        private static bool TryEvictInactiveReadyBlock()
        {
            int attempts = s_ReadyResidencyOrder.Count;
            while (attempts-- > 0 && s_ReadyResidencyOrder.Count > 0)
            {
                int3 block = s_ReadyResidencyOrder.Dequeue();
                if (!s_ReadyBlocks.Contains(block)) continue;
                if (IsBlockDemanded(block) || IsBlockActive(block))
                {
                    s_ReadyResidencyOrder.Enqueue(block);
                    continue;
                }

                RemoveReadyBlock(block);
                s_MixedReadyBlocks.Remove(block);
                s_Mirror.Remove(block);
                return true;
            }
            return false;
        }

        private static void RemoveReadyBlock(int3 block)
        {
            if (!s_ReadyBlocks.Remove(block)) return;
            int3 region = block >> VoxelReadGrid.BlocksPerRegionEdgeLog2;
            if (!s_ReadyBlocksByRegion.TryGetValue(region, out HashSet<int3> blocks)) return;
            blocks.Remove(block);
            if (blocks.Count == 0)
            {
                s_ReadyBlocksByRegion.Remove(region);
                blocks.Clear();
                s_ReadySetPool.Push(blocks);
            }
        }

        private static void ChangeActiveRegionReaders(int3 brickCacheOrigin, int brickCacheEdge,
                                                      int delta)
        {
            if (brickCacheEdge <= 0 || delta == 0) return;
            int shift = VoxelReadGrid.BlocksPerRegionEdgeLog2;
            int3 first = brickCacheOrigin >> shift;
            int3 last = (brickCacheOrigin + new int3(brickCacheEdge - 1)) >> shift;
            for (int z = first.z; z <= last.z; z++)
            for (int y = first.y; y <= last.y; y++)
            for (int x = first.x; x <= last.x; x++)
            {
                int3 region = new(x, y, z);
                s_ActiveRegionReaders.TryGetValue(region, out int readers);
                readers += delta;
                if (readers > 0) s_ActiveRegionReaders[region] = readers;
                else s_ActiveRegionReaders.Remove(region);
            }
        }

        private static void ChangeActiveFootprint(int3 brickCacheOrigin, int brickCacheEdge,
                                                  int delta)
        {
            if (brickCacheEdge <= 0 || delta == 0) return;
            var footprint = new ActiveFootprint(brickCacheOrigin, brickCacheEdge);
            s_ActiveFootprints.TryGetValue(footprint, out int readers);
            readers += delta;
            if (readers > 0) s_ActiveFootprints[footprint] = readers;
            else s_ActiveFootprints.Remove(footprint);
        }

        private static bool IsBlockActive(int3 block)
        {
            foreach (ActiveFootprint footprint in s_ActiveFootprints.Keys)
            {
                int3 end = footprint.Origin + new int3(footprint.Edge);
                if (math.all(block >= footprint.Origin & block < end)) return true;
            }
            return false;
        }

        private static void ClearRecoveryQueues()
        {
            s_RecoveryRegions.Clear();
            s_QueuedRecoveryRegions.Clear();
            foreach (Queue<int3> blocks in s_PendingBlocksByRegion.Values)
            {
                blocks.Clear();
                s_BlockQueuePool.Push(blocks);
            }
            s_PendingBlocksByRegion.Clear();
            s_PendingBlocks.Clear();
        }

        private static void ClearReadyBlocks()
        {
            s_ReadyBlocks.Clear();
            foreach (HashSet<int3> blocks in s_ReadyBlocksByRegion.Values)
            {
                blocks.Clear();
                s_ReadySetPool.Push(blocks);
            }
            s_ReadyBlocksByRegion.Clear();
        }

        private static void EnsureCollectionPools()
        {
            while (s_BlockQueuePool.Count < TrackedRegionCapacity)
                s_BlockQueuePool.Push(
                    new Queue<int3>(BlocksPerTrackedRegionCapacity));
            while (s_ReadySetPool.Count < TrackedRegionCapacity)
                s_ReadySetPool.Push(
                    new HashSet<int3>(BlocksPerTrackedRegionCapacity));
        }

        private static void ResetWorld(bool disposeMirror)
        {
            s_Storage = null;
            s_ChangeSource = null;
            s_ChangeCursor = 0;
            s_MirroredVersion = 0;
            s_KnownRegionHistoryFromVersion = 0;
            s_ActiveExtractionCount = 0;
            s_LastPrepareFrame = -1;
            s_LastExtractionDispatchFrame = -1;
            s_OptionalNonResidentHaloBlocksAccepted = 0;
            ClearRecoveryQueues();
            ClearReadyBlocks();
            s_ChangedReadyScratch.Clear();
            s_ReadyResidencyOrder.Clear();
            s_MixedResidencyOrder.Clear();
            s_MixedReadyBlocks.Clear();
            s_RegionLastSolidChangeVersion.Clear();
            s_ActiveRegionReaders.Clear();
            s_ActiveFootprints.Clear();
            s_DemandFootprints.Clear();
            s_Changes.Clear();

            if (!disposeMirror || s_Mirror == null) return;
            s_Mirror.Dispose();
            s_Mirror = null;
        }
    }
}
