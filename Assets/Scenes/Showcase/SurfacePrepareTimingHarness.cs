using System;
using System.Globalization;
using UnityEngine;
using VoxelEngine.Composition;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Low-frequency diagnostic for the worker-admission spike seen while the showcase streams.
    ///
    /// The renderer already measures these phases and exposes its active solid-job count. This
    /// harness snapshots those existing values once per FPS window when the normal FPS logger is
    /// enabled. GC counters and main-thread allocated bytes are sampled before formatting/logging
    /// so the diagnostic's own strings do not masquerade as workload allocation in that window.
    /// </summary>
    public static class SurfacePrepareTimingHarness
    {
        private const double ReportIntervalSeconds = 1.0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!HasFlag("-voxel-fps-log")) return;

            var root = new GameObject("Surface Prepare Timing Harness")
            {
                hideFlags = HideFlags.DontSave
            };
            root.AddComponent<Reporter>();
            UnityEngine.Object.DontDestroyOnLoad(root);
        }

        private static bool HasFlag(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
                if (string.Equals(args[i], name, StringComparison.Ordinal)) return true;
            return false;
        }

        private sealed class Reporter : MonoBehaviour
        {
            private double _elapsed;
            private double _nextReport = ReportIntervalSeconds;
            private long _lastAllocatedBytes;
            private int _lastGen0Collections;
            private int _lastGen1Collections;
            private int _lastGen2Collections;

            private void Start()
            {
                ResetGcBaselines();
            }

            private void ResetGcBaselines()
            {
                _lastAllocatedBytes = GC.GetAllocatedBytesForCurrentThread();
                _lastGen0Collections = GC.CollectionCount(0);
                _lastGen1Collections = GC.CollectionCount(1);
                _lastGen2Collections = GC.CollectionCount(2);
            }

            private void Update()
            {
                _elapsed += Time.unscaledDeltaTime;
                if (_elapsed < _nextReport) return;
                _nextReport += ReportIntervalSeconds;

                // Capture these before reading timing snapshots or constructing log strings.
                // CollectionCount is process-wide managed-GC evidence; allocated bytes are this
                // main thread only and tell us whether managed churn is accumulating between GCs.
                long allocatedNow = GC.GetAllocatedBytesForCurrentThread();
                int gen0Now = GC.CollectionCount(0);
                int gen1Now = GC.CollectionCount(1);
                int gen2Now = GC.CollectionCount(2);
                long allocatedDelta = Math.Max(0L, allocatedNow - _lastAllocatedBytes);
                int gen0Delta = Math.Max(0, gen0Now - _lastGen0Collections);
                int gen1Delta = Math.Max(0, gen1Now - _lastGen1Collections);
                int gen2Delta = Math.Max(0, gen2Now - _lastGen2Collections);

                SurfacePrepareTimingSnapshot timing =
                    RenderingDiagnosticsComposition.GetSurfacePrepareTiming();
                SurfaceBenchmarkState state =
                    RenderingDiagnosticsComposition.GetSurfaceBenchmarkState();
                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "PREPARESECTIONS t={0:0.0} "
                    + "worker[p95={1:0.000} p99={2:0.000} max={3:0.000}] "
                    + "rule[p95={4:0.000} p99={5:0.000} max={6:0.000}] "
                    + "residency[p95={7:0.000} p99={8:0.000} max={9:0.000}] "
                    + "capacity[p95={10:0.000} p99={11:0.000} max={12:0.000}] "
                    + "select[p95={13:0.000} p99={14:0.000} max={15:0.000}] "
                    + "snapshot[p95={16:0.000} p99={17:0.000} max={18:0.000}] "
                    + "compact[p95={19:0.000} p99={20:0.000} max={21:0.000}] "
                    + "facetedMerge[p95={22:0.000} p99={23:0.000} max={24:0.000}] "
                    + "profile[p95={25:0.000} p99={26:0.000} max={27:0.000}] "
                    + "upload[p95={28:0.000} p99={29:0.000} max={30:0.000}] "
                    + "jobs={31} missing={32} gc[g0=+{33} g1=+{34} g2=+{35}] allocMain={36}",
                    _elapsed,
                    timing.WorkerP95Ms, timing.WorkerP99Ms, timing.WorkerMaxMs,
                    timing.RuleSyncP95Ms, timing.RuleSyncP99Ms, timing.RuleSyncMaxMs,
                    timing.ResidencyP95Ms, timing.ResidencyP99Ms, timing.ResidencyMaxMs,
                    timing.CapacityP95Ms, timing.CapacityP99Ms, timing.CapacityMaxMs,
                    timing.SelectionP95Ms, timing.SelectionP99Ms, timing.SelectionMaxMs,
                    timing.SnapshotP95Ms, timing.SnapshotP99Ms, timing.SnapshotMaxMs,
                    timing.CompactP95Ms, timing.CompactP99Ms, timing.CompactMaxMs,
                    timing.FacetedMergeP95Ms, timing.FacetedMergeP99Ms,
                    timing.FacetedMergeMaxMs,
                    timing.ProfileP95Ms, timing.ProfileP99Ms, timing.ProfileMaxMs,
                    timing.UploadP95Ms, timing.UploadP99Ms, timing.UploadMaxMs,
                    state.RunningSolidJobs, state.MissingVisibleSolidChunks,
                    gen0Delta, gen1Delta, gen2Delta, allocatedDelta));

                // Exclude this diagnostic's own formatting/logging allocations from the next
                // interval as much as possible by taking the next baseline after the log call.
                ResetGcBaselines();
            }
        }
    }
}
