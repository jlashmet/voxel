using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Rendering.Runtime.GpuVoxel
{
    /// <summary>
    /// Bounds immutable source ownership for GPU surface requests against the shared mirror.
    /// Waiting workers own no source residency. Exact steps retain their whole packed-entry
    /// footprint because mixed entries contain live mirror-slot indices until count/write finishes.
    /// Step 8 copies HLOD summaries, so it keeps only the request edit watch while bounded slices
    /// are prepared. Different source steps do not overlap; exact owners are capacity-bounded by
    /// their worst-case mixed-brick footprint and the actual shared mirror slot count.
    /// </summary>
    internal static class GpuSurfaceSourceAdmission
    {
        private static readonly HashSet<GpuSurfaceExtractionContext> s_Owners = new();
        private static readonly Dictionary<GpuSurfaceExtractionContext, int> s_OwnerReservations = new();
        private static readonly Dictionary<GpuSurfaceExtractionContext, int> s_Waiters = new();
        private static readonly List<GpuSurfaceExtractionContext> s_DeadWaiters = new();
        private static int s_ActiveStep;
        private static int s_ReservedMixedSlots;
        private static ulong s_WorldEpoch;
        private static int s_PreferredStep;
        private static int s_PreferredUntilFrame;

        internal static int ActiveCount => s_Owners.Count;
        internal static int ActiveStep => s_ActiveStep;
        internal static int ReservedMixedSlots => s_ReservedMixedSlots;
        internal static int WaitingCount { get { PruneInactiveWaiters(); return s_Waiters.Count; } }

        internal static bool TryAcquire(GpuSurfaceExtractionContext owner,
                                        in GpuChunkExtraction request,
                                        int brickCacheEdge,
                                        out ulong coverageWorldEpoch)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (brickCacheEdge <= 0) throw new ArgumentOutOfRangeException(nameof(brickCacheEdge));
            coverageWorldEpoch = 0;

            ulong world = GpuSurfaceMirrorCoordinator.ResourceWorldEpoch;
            if (s_WorldEpoch != world) ResetForWorld(world);
            PruneInactiveWaiters();

            if (s_Owners.Contains(owner))
                throw new InvalidOperationException("GPU source admission owner was acquired twice.");

            int step = request.SourceStep;
            int reservation = RequiredMixedSlots(step, brickCacheEdge);
            if (s_Owners.Count != 0)
            {
                // Once another LOD is waiting, drain the current mode instead of replenishing it.
                // This prevents continuous near work from starving coarse ownership indefinitely.
                bool otherStepWaiting = HasWaitingStepOtherThan(s_ActiveStep);
                bool capacityFull = s_Owners.Count >= GpuSurfaceMirrorCoordinator.MaxConcurrentExtractionChains
                    || (long)s_ReservedMixedSlots + reservation > owner.Mirror.SlotCapacity;
                if (s_ActiveStep != step || capacityFull || otherStepWaiting)
                {
                    s_Waiters[owner] = step;
                    return false;
                }
            }
            else if (s_PreferredStep != 0 && s_PreferredStep != step)
            {
                if (HasWaitingStep(s_PreferredStep) && Time.frameCount <= s_PreferredUntilFrame)
                {
                    s_Waiters[owner] = step;
                    return false;
                }
                s_PreferredStep = 0;
            }

            // One step-8 HLOD owner at a time. Its mixed-slot reservation is zero because copied
            // summaries, not packed live slot references, survive preparation.
            if (step == VoxelReadGrid.BlockEdge && s_Owners.Count != 0)
            {
                s_Waiters[owner] = step;
                return false;
            }

            s_ActiveStep = step;
            s_Waiters.Remove(owner);
            if (s_PreferredStep == step) s_PreferredStep = 0;
            if (!s_Owners.Add(owner))
                throw new InvalidOperationException("GPU source admission owner collision.");
            s_OwnerReservations.Add(owner, reservation);
            s_ReservedMixedSlots = checked(s_ReservedMixedSlots + reservation);

            int coreExtentVoxels = GpuSolidChunkCache.CellsPerAxis * step;
            int3 coreMaxVoxelExclusive = request.ChunkOriginVoxel + new int3(coreExtentVoxels);
            coverageWorldEpoch = step == VoxelReadGrid.BlockEdge
                ? GpuSurfaceMirrorCoordinator.RequestEditWatch(request.BrickCacheOrigin, brickCacheEdge)
                : GpuSurfaceMirrorCoordinator.RequestCoverage(request.BrickCacheOrigin, brickCacheEdge,
                    request.ChunkOriginVoxel, coreMaxVoxelExclusive);
            return true;
        }

        internal static void Release(GpuSurfaceExtractionContext owner,
                                     in GpuChunkExtraction request,
                                     int brickCacheEdge,
                                     ulong coverageWorldEpoch)
        {
            if (owner == null) return;
            s_Waiters.Remove(owner);
            bool owned = s_Owners.Remove(owner);
            if (s_OwnerReservations.Remove(owner, out int reservation))
                s_ReservedMixedSlots = Math.Max(0, s_ReservedMixedSlots - reservation);

            // Coverage/edit-watch lifetime is explicit at the extraction context. Some production-
            // faithful queue paths can already hold coordinator coverage when they enter the
            // admission-aware context, so release that coordinator ownership even when this
            // admission set did not create it. World-epoch checks inside the coordinator make a
            // stale-world release harmless, while an early return here would leak demand and
            // contaminate later requests.
            int step = request.SourceStep;
            int coreExtentVoxels = GpuSolidChunkCache.CellsPerAxis * step;
            int3 coreMaxVoxelExclusive = request.ChunkOriginVoxel + new int3(coreExtentVoxels);
            if (step == VoxelReadGrid.BlockEdge)
                GpuSurfaceMirrorCoordinator.ReleaseEditWatch(
                    request.BrickCacheOrigin, brickCacheEdge, coverageWorldEpoch);
            else
                GpuSurfaceMirrorCoordinator.ReleaseCoverage(request.BrickCacheOrigin, brickCacheEdge,
                    request.ChunkOriginVoxel, coreMaxVoxelExclusive, coverageWorldEpoch);

            if (!owned) return;
            if (s_Owners.Count != 0) return;
            int releasedStep = s_ActiveStep;
            s_ActiveStep = 0;
            s_PreferredStep = NextWaitingStep(releasedStep);
            s_PreferredUntilFrame = Time.frameCount + 4;
        }

        private static int RequiredMixedSlots(int step, int edge)
        {
            if (step == VoxelReadGrid.BlockEdge) return 0;
            long blocks = (long)edge * edge * edge;
            if (blocks <= 0 || blocks > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(edge));
            return (int)blocks;
        }

        private static int StepBit(int step) => step switch
        {
            1 => 1 << 0,
            2 => 1 << 1,
            4 => 1 << 2,
            8 => 1 << 3,
            _ => throw new ArgumentOutOfRangeException(nameof(step)),
        };

        private static void PruneInactiveWaiters()
        {
            s_DeadWaiters.Clear();
            foreach (var pair in s_Waiters)
                if (!pair.Key.HasActiveRequest) s_DeadWaiters.Add(pair.Key);
            for (int i = 0; i < s_DeadWaiters.Count; i++) s_Waiters.Remove(s_DeadWaiters[i]);
            s_DeadWaiters.Clear();
        }

        private static int WaitingStepMask()
        {
            PruneInactiveWaiters();
            int mask = 0;
            foreach (int step in s_Waiters.Values) mask |= StepBit(step);
            return mask;
        }

        private static bool HasWaitingStep(int step) =>
            (WaitingStepMask() & StepBit(step)) != 0;

        private static bool HasWaitingStepOtherThan(int step) =>
            (WaitingStepMask() & ~StepBit(step)) != 0;

        private static int NextWaitingStep(int afterStep)
        {
            int mask = WaitingStepMask();
            if (mask == 0) return 0;
            int[] steps = { 1, 2, 4, 8 };
            int start = Array.IndexOf(steps, afterStep);
            for (int offset = 1; offset <= steps.Length; offset++)
            {
                int step = steps[(Math.Max(0, start) + offset) % steps.Length];
                if ((mask & StepBit(step)) != 0) return step;
            }
            return 0;
        }

        private static void ResetForWorld(ulong world)
        {
            // Coordinator world reset already discarded the prior world's demand/edit maps.
            s_Owners.Clear();
            s_OwnerReservations.Clear();
            s_Waiters.Clear();
            s_DeadWaiters.Clear();
            s_ActiveStep = 0;
            s_ReservedMixedSlots = 0;
            s_PreferredStep = 0;
            s_PreferredUntilFrame = 0;
            s_WorldEpoch = world;
        }
    }
}
