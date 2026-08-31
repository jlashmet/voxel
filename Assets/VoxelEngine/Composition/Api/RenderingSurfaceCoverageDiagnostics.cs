using System.Reflection;
using UnityEngine;
using VoxelEngine.Rendering.Runtime;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;

namespace VoxelEngine.Composition
{
    /// <summary>
    /// Read-only, bounded observation of the production solid draw set for evidence and tests.
    /// Callers supply semantic world bounds; the renderer remains the sole owner of extraction,
    /// scheduling, publication, and visibility policy.
    /// </summary>
    public readonly struct SurfaceBoundsCoverage
    {
        public readonly int VisibleChunkCount;
        public readonly int ReadyChunkCount;
        public readonly long ReadyIndexCount;
        public readonly int MinimumSourceStep;
        public readonly int MaximumSourceStep;

        internal SurfaceBoundsCoverage(
            int visibleChunkCount,
            int readyChunkCount,
            long readyIndexCount,
            int minimumSourceStep,
            int maximumSourceStep)
        {
            VisibleChunkCount = visibleChunkCount;
            ReadyChunkCount = readyChunkCount;
            ReadyIndexCount = readyIndexCount;
            MinimumSourceStep = minimumSourceStep;
            MaximumSourceStep = maximumSourceStep;
        }

        public bool HasReadyGeometry => ReadyChunkCount > 0 && ReadyIndexCount > 0;
    }

    /// <summary>
    /// Diagnostics-only query over the most recent production surface-pass draw set. It never
    /// advances the renderer, discovers chunks, requests residency, or changes budgets.
    /// </summary>
    public static class RenderingSurfaceCoverageDiagnostics
    {
        private static readonly PropertyInfo s_ActivePassProperty = typeof(VoxelRenderBridge)
            .GetProperty("ActivePass", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo s_SchedulerField = typeof(VoxelRenderPass)
            .GetField("_scheduler", BindingFlags.Instance | BindingFlags.NonPublic);

        public static bool TryQueryVisibleSolidBounds(
            Bounds worldBounds,
            float voxelSizeMetres,
            out SurfaceBoundsCoverage coverage)
        {
            coverage = default;
            if (voxelSizeMetres <= 0f || s_ActivePassProperty == null || s_SchedulerField == null)
                return false;

            var pass = s_ActivePassProperty.GetValue(null) as VoxelRenderPass;
            if (pass == null) return false;
            var scheduler = s_SchedulerField.GetValue(pass) as VoxelSurfaceScheduler;
            if (scheduler == null) return false;

            int visible = 0;
            int ready = 0;
            long indices = 0;
            int minStep = int.MaxValue;
            int maxStep = 0;
            var entries = scheduler.VisibleSolids;
            for (var i = 0; i < entries.Count; i++)
            {
                CpuTransvoxelChunkCache.Entry entry = entries[i];
                float edge = entry.VoxelsPerAxis * voxelSizeMetres;
                var min = new Vector3(
                    entry.Coordinate.x * edge,
                    entry.Coordinate.y * edge,
                    entry.Coordinate.z * edge);
                var chunkBounds = new Bounds(
                    min + Vector3.one * (edge * 0.5f),
                    Vector3.one * edge);
                if (!chunkBounds.Intersects(worldBounds)) continue;

                visible++;
                minStep = Mathf.Min(minStep, entry.SourceStep);
                maxStep = Mathf.Max(maxStep, entry.SourceStep);
                if (!entry.Ready || entry.IndexCount <= 0) continue;
                ready++;
                indices += entry.IndexCount;
            }

            coverage = new SurfaceBoundsCoverage(
                visible,
                ready,
                indices,
                visible > 0 ? minStep : 0,
                visible > 0 ? maxStep : 0);
            return true;
        }
    }
}
