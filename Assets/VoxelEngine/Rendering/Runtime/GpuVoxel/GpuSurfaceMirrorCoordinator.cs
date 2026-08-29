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
    /// number of overlapping chunks/workers. This coordinator instead mirrors Storage once: initial
    /// resident regions are recovered incrementally and later edits arrive through the canonical
    /// change feed. GPU chunk extraction only asks whether the required regions are mirrored.
    /// </summary>
    internal static class GpuSurfaceMirrorCoordinator
    {
        // Eight step-2 worker-local mirrors previously committed about 98 MiB in aggregate. One
        // 96 MiB shared payload mirror therefore preserves approximately the same worst-ring
        // capacity while eliminating duplication; the compact lookup directory adds ~4%.
        private const long MinimumSharedMirrorBudgetBytes = 96L * 1024L * 1024L;
        private const int RecoveryBlocksPerFrame = 2048;
        private const int ResidentScanSlotsPerFrame = 64;
        private const int ChangeRecordsPerFrame = 128;

        private static readonly Queue<int3> s_RecoveryRegions = new();
        private static readonly HashSet<int3> s_QueuedRecoveryRegions = new();
        private static readonly HashSet<int3> s_ReadyRegions = new();
        private static readonly Dictionary<int3, ulong> s_RegionLastSolidChangeVersion = new();
        private static readonly List<VoxelChangeRecord> s_Changes = new(ChangeRecordsPerFrame);

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

        private static int s_ResidentScanCursor;
        private static bool s_ResidentScanComplete;
        private static int3 s_RecoveryRegion;
        private static int s_RecoveryBlockIndex;
        private static bool s_RecoveryRegionOk;
        private static bool s_HasRecoveryRegion;

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
                return s_LastPrepareResult && s_MirroredVersion == currentGeneration;
            s_LastPrepareFrame = frame;
            s_LastPrepareWorldVersion = currentGeneration;
            s_LastPrepareResult = false;

            // Journal replay and resident recovery are independent bounded queues. Advance both in
            // the same frame when mutation is legal, but never rewrite a shared slot underneath an
            // active extraction. Once the mirror already matches the current generation, multiple
            // chunks may safely extract concurrently from that immutable image.
            bool changesSynchronized = s_MirroredVersion == currentGeneration;
            if (!changesSynchronized)
            {
                if (s_ActiveExtractions != 0) return false;
                changesSynchronized = SynchronizeChanges(currentGeneration);
            }

            if (!RecoveryComplete)
            {
                if (s_ActiveExtractions != 0) return false;
                ScanResidentRegions();
                ProcessRecovery();
            }

            if (!changesSynchronized || !RecoveryComplete) return false;

            s_LastPrepareResult = s_MirroredVersion == currentGeneration;
            return s_LastPrepareResult;
        }

        /// <summary>
        /// True when every region sampled by the chunk is resident in the current mirror and no
        /// solid-affecting change in those regions occurred after the caller's snapshot generation.
        /// Global changes elsewhere in the world do not invalidate otherwise identical chunk data.
        /// </summary>
        internal static bool Covers(int3 brickCacheOrigin, int brickCacheEdge,
                                    ulong requiredGeneration)
        {
            if (s_Storage == null || !RecoveryComplete || brickCacheEdge <= 0) return false;
            if (requiredGeneration < s_KnownRegionHistoryFromVersion) return false;

            int shift = VoxelReadGrid.BlocksPerRegionEdgeLog2;
            int3 first = brickCacheOrigin >> shift;
            int3 last = (brickCacheOrigin + new int3(brickCacheEdge - 1)) >> shift;
            for (int z = first.z; z <= last.z; z++)
            for (int y = first.y; y <= last.y; y++)
            for (int x = first.x; x <= last.x; x++)
            {
                int3 region = new int3(x, y, z);
                if (!s_ReadyRegions.Contains(region)) return false;
                if (s_RegionLastSolidChangeVersion.TryGetValue(region, out ulong changedAt)
                    && changedAt > requiredGeneration)
                    return false;
            }
            return true;
        }

        internal static void BeginExtraction() => s_ActiveExtractions++;

        internal static void EndExtraction()
        {
            if (s_ActiveExtractions > 0) s_ActiveExtractions--;
        }

        internal static int ReadyRegionCount => s_ReadyRegions.Count;
        internal static int ActiveExtractions => s_ActiveExtractions;
        internal static ulong MirroredVersion => s_MirroredVersion;
        internal static bool RecoveryComplete =>
            s_ResidentScanComplete && s_RecoveryRegions.Count == 0 && !s_HasRecoveryRegion;

        private static void AttachWorld(IRegionReadSource storage, IVoxelChangeSource changes)
        {
            ResetWorld(disposeMirror: false);
            s_Mirror.Clear();
            s_Storage = storage;
            s_ChangeSource = changes;
            s_ChangeCursor = changes?.CurrentVersion ?? storage.Version;
            s_MirroredVersion = storage.Version;
            s_KnownRegionHistoryFromVersion = storage.Version;
            BeginResidentRecovery(clearMirror: false);
        }

        private static bool SynchronizeChanges(ulong targetGeneration)
        {
            if (s_Storage == null) return false;

            if (s_ChangeSource == null)
            {
                // Without a journal there is no exact incremental replay. Rebuild from current
                // authoritative state and reject snapshots older than that new known-history floor.
                BeginResidentRecovery(clearMirror: true);
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
                BeginResidentRecovery(clearMirror: true);
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

            bool wasReady = s_ReadyRegions.Remove(region);
            if (!s_Storage.IsRegionResident(region))
            {
                s_QueuedRecoveryRegions.Remove(region);
                RemoveRegionLookup(region);
                return;
            }

            // A newly resident or previously incomplete region needs all 512 logical blocks before
            // it can be sampled safely. The change record may describe only the authoring write that
            // caused residency, not every unchanged block in the region.
            if (!wasReady || (change.Kind & VoxelChangeKind.Residency) != 0)
            {
                QueueRecoveryRegion(region);
                return;
            }

            int3 minBlock = change.MinVoxel >> VoxelReadGrid.BlockEdgeLog2;
            int3 maxBlock = (change.MaxVoxelExclusive - 1) >> VoxelReadGrid.BlockEdgeLog2;
            bool ok = true;
            for (int z = minBlock.z; z <= maxBlock.z; z++)
            for (int y = minBlock.y; y <= maxBlock.y; y++)
            for (int x = minBlock.x; x <= maxBlock.x; x++)
                ok &= PublishBlock(new int3(x, y, z), change.Version);

            if (ok) s_ReadyRegions.Add(region);
            else QueueRecoveryRegion(region);
        }

        private static void BeginResidentRecovery(bool clearMirror)
        {
            s_ReadyRegions.Clear();
            s_RecoveryRegions.Clear();
            s_QueuedRecoveryRegions.Clear();
            s_HasRecoveryRegion = false;
            s_RecoveryBlockIndex = 0;
            s_RecoveryRegionOk = true;
            s_ResidentScanCursor = 0;
            s_ResidentScanComplete = false;
            if (clearMirror) s_Mirror.Clear();
        }

        /// <summary>
        /// Uses Storage's bounded table cursor rather than allocating/copying the full resident set
        /// at world attach. Sparse/free slots therefore cannot become an unbounded startup frame.
        /// </summary>
        private static void ScanResidentRegions()
        {
            if (s_Storage == null || s_ResidentScanComplete) return;
            using var regions = new NativeArray<int3>(ResidentScanSlotsPerFrame,
                                                      Allocator.Temp,
                                                      NativeArrayOptions.UninitializedMemory);
            s_ResidentScanComplete = s_Storage.CopyResidentRegionCoords(
                ref s_ResidentScanCursor, regions, out int count);
            for (int i = 0; i < count; i++) QueueRecoveryRegion(regions[i]);
        }

        private static void QueueRecoveryRegion(int3 region)
        {
            s_ReadyRegions.Remove(region);
            if (s_HasRecoveryRegion && s_RecoveryRegion.Equals(region)) return;
            if (!s_QueuedRecoveryRegions.Add(region)) return;
            s_RecoveryRegions.Enqueue(region);
        }

        private static void ProcessRecovery()
        {
            if (s_Storage == null) return;

            int remaining = RecoveryBlocksPerFrame;
            while (remaining > 0)
            {
                if (!s_HasRecoveryRegion)
                {
                    if (s_RecoveryRegions.Count == 0) return;
                    s_RecoveryRegion = s_RecoveryRegions.Dequeue();
                    s_QueuedRecoveryRegions.Remove(s_RecoveryRegion);
                    s_RecoveryBlockIndex = 0;
                    s_RecoveryRegionOk = true;
                    s_HasRecoveryRegion = true;
                    if (!s_Storage.IsRegionResident(s_RecoveryRegion))
                    {
                        s_HasRecoveryRegion = false;
                        continue;
                    }
                }

                int edge = VoxelReadGrid.BlocksPerRegionEdge;
                int total = VoxelReadGrid.BlocksPerRegion;
                while (remaining > 0 && s_RecoveryBlockIndex < total)
                {
                    int index = s_RecoveryBlockIndex++;
                    int x = index % edge;
                    int yz = index / edge;
                    int y = yz % edge;
                    int z = yz / edge;
                    int3 worldBlock = (s_RecoveryRegion << VoxelReadGrid.BlocksPerRegionEdgeLog2)
                                    + new int3(x, y, z);
                    s_RecoveryRegionOk &= PublishBlock(worldBlock, s_Storage.Version);
                    remaining--;
                }

                if (s_RecoveryBlockIndex < total) return;
                if (s_RecoveryRegionOk && s_Storage.IsRegionResident(s_RecoveryRegion))
                    s_ReadyRegions.Add(s_RecoveryRegion);
                s_HasRecoveryRegion = false;
            }
        }

        private static bool PublishBlock(int3 worldBlock, ulong generation)
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

                // The shared world mirror is authoritative for its ready regions. Keep mixed slots
                // stable until that coordinate changes/unloads; capacity pressure therefore causes
                // explicit CPU fallback instead of recycling a slot an in-flight shader can read.
                if (block.Kind == VoxelReadBlockKind.Mixed) s_Mirror.Pin(worldBlock);
                return true;
            }
            finally
            {
                if (block.HasPinnedPayload) s_Storage.ReleasePinnedWorldBlock(block.Pin);
            }
        }

        private static void RemoveRegionLookup(int3 region)
        {
            int edge = VoxelReadGrid.BlocksPerRegionEdge;
            int3 origin = region << VoxelReadGrid.BlocksPerRegionEdgeLog2;
            for (int z = 0; z < edge; z++)
            for (int y = 0; y < edge; y++)
            for (int x = 0; x < edge; x++)
                s_Mirror.Remove(origin + new int3(x, y, z));
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
            s_RecoveryRegions.Clear();
            s_QueuedRecoveryRegions.Clear();
            s_ReadyRegions.Clear();
            s_RegionLastSolidChangeVersion.Clear();
            s_Changes.Clear();
            s_HasRecoveryRegion = false;
            s_RecoveryBlockIndex = 0;
            s_RecoveryRegionOk = true;
            s_ResidentScanCursor = 0;
            s_ResidentScanComplete = false;

            if (!disposeMirror || s_Mirror == null) return;
            s_Mirror.Dispose();
            s_Mirror = null;
        }
    }
}
