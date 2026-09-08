using UnityEngine;

namespace VoxelEngine.Composition
{
    /// <summary>
    /// Historical validation telemetry shape retained only for Kentridge evidence compiled
    /// against the current renderer contract. Aggregate camera-visible counts remain observable;
    /// retired per-bounds index/source-step data is explicitly reported as unavailable (-1).
    /// </summary>
    internal readonly struct SurfaceBoundsCoverage
    {
        public readonly int VisibleChunkCount;
        public readonly int ReadyChunkCount;
        public readonly int ReadyIndexCount;
        public readonly int MinimumSourceStep;
        public readonly int MaximumSourceStep;

        public SurfaceBoundsCoverage(
            int visibleChunkCount,
            int readyChunkCount,
            int readyIndexCount,
            int minimumSourceStep,
            int maximumSourceStep)
        {
            VisibleChunkCount = visibleChunkCount;
            ReadyChunkCount = readyChunkCount;
            ReadyIndexCount = readyIndexCount;
            MinimumSourceStep = minimumSourceStep;
            MaximumSourceStep = maximumSourceStep;
        }
    }

    internal static class RenderingSurfaceCoverageDiagnostics
    {
        public static bool TryQueryVisibleSolidBounds(
            Bounds bounds,
            float voxelSize,
            out SurfaceBoundsCoverage coverage)
        {
            _ = bounds;
            _ = voxelSize;

            RenderingComposition.GetVoxelSurfaceCounts(out int visible, out _);
            RenderingComposition.TryGetSurfaceBuildStatus(
                out _,
                out _,
                out int resident,
                out _);

            coverage = new SurfaceBoundsCoverage(
                visible,
                resident,
                readyIndexCount: -1,
                minimumSourceStep: -1,
                maximumSourceStep: -1);

            // Current Rendering intentionally exposes no caller-bounds query. Returning false is
            // part of the diagnostic contract: aggregate metrics must never masquerade as
            // bounds-specific publication proof. Capture acceptance is governed by the canonical
            // HasCompletePublishedNearSurfaceCoverage gate plus authored storage/readiness checks.
            return false;
        }
    }
}
