using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;

namespace VoxelEngine.Rendering.Runtime.GpuVoxel
{
    /// <summary>
    /// Bounds immutable source ownership for GPU surface requests against the shared mirror.
    ///
    /// Waiting workers own no source residency. Exact steps retain their whole packed-entry
    /// footprint because mixed entries contain live mirror-slot indices until count/write finishes.
    /// Step 8 copies HLOD summaries instead, so it needs only the whole-request edit watch while its
    /// bounded source slices are prepared. Different source steps do not overlap: a worst-case
    /// step-4 exact footprint is 34^3 blocks, leaving too little of the shared mixed mirror for an
    /// unrelated exact/coarse recovery footprint. Fine steps 1/2 may batch two requests safely.
    /// </summary>
    internal static class GpuSurfaceSourceAdmission
    {
        private static readonly HashSet<GpuSurfaceExtractionContext> s_Owners = new();
        private static readonly Dictionary<GpuSurfaceExtractionContext, int> s_Waiters = new();
        private static int s_ActiveStep;
        private static ulong s_WorldEpoch;
        private static int s_PreferredStep;
        private static int s_PreferredUntilFrame;

        internal static int ActiveCount => s_Owners.Count;
        internal static int ActiveStep => s_ActiveStep;
        internal static int WaitingCount => s_Waiters.Count;

        internal static bool TryAcquire(GpuSurfaceExtractionContext owner,
                                        in GpuChunkExtraction request,
                                        int brickCacheEdge,
                                        out ulong coverageWorldEpoch)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (brickCacheEdge <= 0) throw new ArgumentOutOfRangeException(nameof(brickCacheEdge));
            coverageWorldEpoch = 0;

            ulong world = GpuSurfaceMirrorCoordinator.ResourceWorldEpoch;
            if (s_WorldEpoch != world)
                ResetForWorld(world);

            if (s_Owners.Contains(owner))
                throw new InvalidOperationException("GPU source admission owner was acquired twice.");

            int step = request.SourceStep;
            int capacity = MaximumOwners(step);
            if (s_Owners.Count != 0)
            {
                bool otherStepWaiting = HasWaitingStepOtherThan(s_ActiveStep);
                if (s_ActiveStep != step || s_Owners.Count >= capacity || otherStepWaiting)
                {
                    s_Waiters[owner] = step;
                    return false;
                }
            }
            else if (s_PreferredStep != 0 && s_PreferredStep != step)
            {
                // Reserve a few frames for the mode that was already waiting. If that waiter was
                // cancelled, clear the preference immediately instead of manufacturing a stall.
                if (HasWaitingStep(s_PreferredStep) && Time.frameCount <= s_PreferredUntilFrame)
                {
                    s_Waiters[owner] = step;
                    return false;
                }
                s_PreferredStep = 0;
            }

            s_ActiveStep = step;
            s_Waiters.Remove(owner);
            if (s_PreferredStep == step) s_PreferredStep = 0;
            if (!s_Owners.Add(owner))
                throw new InvalidOperationException("GPU source admission owner collision.");

            int coreExtentVoxels = GpuSolidChunkCache.CellsPerAxis * step;
            int3 coreMaxVoxelExclusive = request.ChunkOriginVoxel + new int3(coreExtentVoxels);
            coverageWorldEpoch = step == VoxelReadGrid.BlockEdge
                ? GpuSurfaceMirrorCoordinator.RequestEditWatch(request.BrickCacheOrigin, brickCacheEdge)
                : GpuSurfaceMirrorCoordinator.RequestCoverage(request.BrickCacheOrigin, brickCacheEdge,
                    request.ChunkOriginVoxel, coreMaxVoxelExclusive);
            return true;
        }

        internal static void CancelWaiting(GpuSurfaceExtractionContext owner)
        {
            if (owner != null) s_Waiters.Remove(owner);
        }

        internal static void Release(GpuSurfaceExtractionContext owner,
                                     in GpuChunkExtraction request,
                                     int brickCacheEdge,
                                     ulong coverageWorldEpoch)
        {
            if (owner == null) return;
            s_Waiters.Remove(owner);
            if (!s_Owners.Remove(owner)) return;

            int step = request.SourceStep;
            int coreExtentVoxels = GpuSolidChunkCache.CellsPerAxis * step;
            int3 coreMaxVoxelExclusive = request.ChunkOriginVoxel + new int3(coreExtentVoxels);
            if (step == VoxelReadGrid.BlockEdge)
                GpuSurfaceMirrorCoordinator.ReleaseEditWatch(
                    request.BrickCacheOrigin, brickCacheEdge, coverageWorldEpoch);
            else
                GpuSurfaceMirrorCoordinator.ReleaseCoverage(request.BrickCacheOrigin, brickCacheEdge,
                    request.ChunkOriginVoxel, coreMaxVoxelExclusive, coverageWorldEpoch);

            if (s_Owners.Count != 0) return;
            int releasedStep = s_ActiveStep;
            s_ActiveStep = 0;
            s_PreferredStep = NextWaitingStep(releasedStep);
            s_PreferredUntilFrame = Time.frameCount + 4;
        }

        private static int MaximumOwners(int step) => step <= 2 ? 2 : 1;

        private static int StepBit(int step) => step switch
        {
            1 => 1 << 0,
            2 => 1 << 1,
            4 => 1 << 2,
            8 => 1 << 3,
            _ => throw new ArgumentOutOfRangeException(nameof(step)),
        };

        private static int WaitingStepMask()
        {
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
            // The coordinator already discarded the prior world's demand/edit maps. Old contexts
            // may still call Release later; their ownership is intentionally forgotten here.
            s_Owners.Clear();
            s_Waiters.Clear();
            s_ActiveStep = 0;
            s_PreferredStep = 0;
            s_PreferredUntilFrame = 0;
            s_WorldEpoch = world;
        }
    }
}
