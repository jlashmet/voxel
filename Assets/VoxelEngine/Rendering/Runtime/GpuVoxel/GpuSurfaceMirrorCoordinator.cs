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
        private const long MinimumSharedMirrorBudgetBytes = 96L * 1024L * 1024L;
        private const int RecoveryBlocksPerFrame = 2048;
        private const int ChangeRecordsPerFrame = 128;

        private static readonly Queue<int3> s_RecoveryRegions = new();
        private static readonly HashSet<int3> s_ReadyRegions = new();
        private static readonly List<VoxelChangeRecord> s_Changes = new(ChangeRecordsPerFrame);

        private static GpuVoxelBrickMirror s_Mirror;
        private static IRegionReadSource s_Storage;
        private static IVoxelChangeSource s_ChangeSource;
        private static ulong s_ChangeCursor;
        private static int s_ReferenceCount;
        private static int s_ActiveExtractions;
        private static int s_LastPrepareFrame = -1;
        private static int3 s_RecoveryRegion;
        private static int s_RecoveryBlockIndex;
        private static bool s_HasRecoveryRegion;
        private static bool s_RecoveryEnumerated;

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

        internal static bool PrepareFromBridge(ulong requiredGeneration)
        {
            if (s_Mirror == null || !SystemInfo.supportsComputeShaders) return false;
            if (!VoxelRenderBridge.TryGetWorld(out VoxelWorldView world)) return false;
            if (world.Storage == null || world.Storage.Version != requiredGeneration) return false;

            if (!ReferenceEquals(s_Storage, world.Storage)
                || !ReferenceEquals(s_ChangeSource, VoxelRenderBridge.Changes))
            {
                AttachWorld(world.Storage, VoxelRenderBridge.Changes);
            }

            int frame = Time.frameCount;
            if (s_LastPrepareFrame == frame) return true;
            s_LastPrepareFrame = frame;

            // Never rewrite a slot while an earlier count/write may still be reading it. New GPU
            // admissions fall back to CPU while a changed generation waits; when the old GPU work
            // drains, the shared mirror catches up atomically before another extraction starts.
            if (s_ActiveExtractions != 0) return true;

            ProcessChanges();
            ProcessRecovery();
            return true;
        }

        internal static bool Covers(int3 brickCacheOrigin, int brickCacheEdge)
        {
            if (s_Storage == null || brickCacheEdge <= 0) return false;

            int shift = VoxelReadGrid.BlocksPerRegionEdgeLog2;
            int3 first = brickCacheOrigin >> shift;
            int3 last = (brickCacheOrigin + new int3(brickCacheEdge - 1)) >> shift;
            for (int z = first.z; z <= last.z; z++)
            for (int y = first.y; y <= last.y; y++)
            for (int x = first.x; x <= last.x; x++)
                if (!s_ReadyRegions.Contains(new int3(x, y, z))) return false;
            return true;
        }

        internal static void BeginExtraction() => s_ActiveExtractions++;

        internal static void EndExtraction()
        {
            if (s_ActiveExtractions > 0) s_ActiveExtractions--;
        }

        internal static int ReadyRegionCount => s_ReadyRegions.Count;
        internal static int ActiveExtractions => s_ActiveExtractions;

        private static void AttachWorld(IRegionReadSource storage, IVoxelChangeSource changes)
        {
            ResetWorld(disposeMirror: false);
            s_Storage = storage;
            s_ChangeSource = changes;
            s_ChangeCursor = changes?.CurrentVersion ?? storage.Version;
            EnumerateRecoveryRegions();
        }

        private static void EnumerateRecoveryRegions()
        {
            if (s_Storage == null || s_RecoveryEnumerated) return;
            using NativeArray<int3> regions = s_Storage.GetResidentRegionCoords(Allocator.Temp);
            for (int i = 0; i < regions.Length; i++) s_RecoveryRegions.Enqueue(regions[i]);
            s_RecoveryEnumerated = true;
        }

        private static void ProcessChanges()
        {
            if (s_ChangeSource == null || s_Storage == null) return;

            s_Changes.Clear();
            bool valid = s_ChangeSource.ReadSince(ref s_ChangeCursor, s_Changes,
                                                  ChangeRecordsPerFrame, out _);
            if (!valid)
            {
                // Retention was overrun. Region readiness is no longer trustworthy; rebuild the
                // bounded resident set from Storage rather than guessing which omitted brick moved.
                s_ReadyRegions.Clear();
                s_RecoveryRegions.Clear();
                s_HasRecoveryRegion = false;
                s_RecoveryBlockIndex = 0;
                s_RecoveryEnumerated = false;
                EnumerateRecoveryRegions();
                return;
            }

            for (int i = 0; i < s_Changes.Count; i++)
                ApplyChange(s_Changes[i]);
        }

        private static void ApplyChange(in VoxelChangeRecord change)
        {
            // Water does not participate in solid density unless another solid-affecting bit is set.
            VoxelChangeKind solid = change.Kind & ~VoxelChangeKind.Water;
            if (solid == VoxelChangeKind.None) return;

            int3 region = change.Region;
            bool wasReady = s_ReadyRegions.Remove(region);
            if (!s_Storage.IsRegionResident(region))
            {
                RemoveRegionLookup(region);
                return;
            }

            int3 minBlock = change.MinVoxel >> VoxelReadGrid.BlockEdgeLog2;
            int3 maxBlock = (change.MaxVoxelExclusive - 1) >> VoxelReadGrid.BlockEdgeLog2;
            bool ok = true;
            for (int z = minBlock.z; z <= maxBlock.z; z++)
            for (int y = minBlock.y; y <= maxBlock.y; y++)
            for (int x = minBlock.x; x <= maxBlock.x; x++)
                ok &= PublishBlock(new int3(x, y, z), change.Version);

            if (wasReady && ok) s_ReadyRegions.Add(region);
        }

        private static void ProcessRecovery()
        {
            if (s_Storage == null) return;
            EnumerateRecoveryRegions();

            int remaining = RecoveryBlocksPerFrame;
            while (remaining > 0)
            {
                if (!s_HasRecoveryRegion)
                {
                    if (s_RecoveryRegions.Count == 0) return;
                    s_RecoveryRegion = s_RecoveryRegions.Dequeue();
                    s_RecoveryBlockIndex = 0;
                    s_HasRecoveryRegion = true;
                    if (!s_Storage.IsRegionResident(s_RecoveryRegion))
                    {
                        s_HasRecoveryRegion = false;
                        continue;
                    }
                }

                int edge = VoxelReadGrid.BlocksPerRegionEdge;
                int total = VoxelReadGrid.BlocksPerRegion;
                bool regionOk = true;
                while (remaining > 0 && s_RecoveryBlockIndex < total)
                {
                    int index = s_RecoveryBlockIndex++;
                    int x = index % edge;
                    int yz = index / edge;
                    int y = yz % edge;
                    int z = yz / edge;
                    int3 worldBlock = (s_RecoveryRegion << VoxelReadGrid.BlocksPerRegionEdgeLog2)
                                    + new int3(x, y, z);
                    regionOk &= PublishBlock(worldBlock, s_Storage.Version);
                    remaining--;
                }

                if (s_RecoveryBlockIndex < total) return;
                if (regionOk) s_ReadyRegions.Add(s_RecoveryRegion);
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

                // Persistent shared slots are never recycled under an extraction. When the fixed
                // mirror fills, uncovered regions simply stay CPU-backed instead of risking a stale
                // coordinate->slot alias.
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
            s_ActiveExtractions = 0;
            s_LastPrepareFrame = -1;
            s_RecoveryRegions.Clear();
            s_ReadyRegions.Clear();
            s_Changes.Clear();
            s_HasRecoveryRegion = false;
            s_RecoveryBlockIndex = 0;
            s_RecoveryEnumerated = false;

            if (!disposeMirror || s_Mirror == null) return;
            s_Mirror.Dispose();
            s_Mirror = null;
        }
    }
}
