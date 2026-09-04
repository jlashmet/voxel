using System;
using UnityEngine;

namespace VoxelEngine.Rendering.Runtime.GpuVoxel
{
    /// <summary>
    /// CI-only production telemetry used to discriminate page-allocation failure modes without
    /// introducing any GPU-to-CPU transfer. Remove once the restoration defect is isolated.
    /// </summary>
    internal static class GpuSurfacePageAllocationDiagnostics
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("CI"), "true",
                               StringComparison.OrdinalIgnoreCase))
                return;

            var host = new GameObject("GpuSurfacePageAllocationDiagnostics");
            host.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<Reporter>();
        }

        private sealed class Reporter : MonoBehaviour
        {
            private float _nextReportAt = 5f;

            private void Update()
            {
                if (Time.unscaledTime < _nextReportAt) return;
                _nextReportAt = Time.unscaledTime + 5f;
                Debug.Log(
                    $"GPU_PAGE_STATUS records={GpuSurfaceMirrorCoordinator.CountBatchRecords} "
                    + $"readbacks={GpuSurfaceMirrorCoordinator.CountBatchReadbacks} "
                    + $"readbackFailures={GpuSurfaceMirrorCoordinator.CountBatchReadbackFailures} "
                    + $"exhausted={GpuSurfaceMirrorCoordinator.CountBatchAllocationExhausted} "
                    + $"stale={GpuSurfaceMirrorCoordinator.CountBatchAllocationStale} "
                    + $"tooLarge={GpuSurfaceMirrorCoordinator.CountBatchAllocationTooLarge}");
            }
        }
    }
}
