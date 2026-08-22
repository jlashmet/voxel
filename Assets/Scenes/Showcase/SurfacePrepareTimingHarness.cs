using System;
using System.Globalization;
using UnityEngine;
using VoxelEngine.Composition;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Low-frequency diagnostic for the worker-admission spike seen while the showcase streams.
    ///
    /// The renderer already measures these phases. This harness only snapshots those rolling
    /// windows every five seconds when the normal FPS logger is enabled, so it does not add
    /// stopwatch work to the renderer's frame path. The sparse cadence also keeps the snapshot
    /// sorting cost out of almost every measured frame.
    /// </summary>
    public static class SurfacePrepareTimingHarness
    {
        private const double ReportIntervalSeconds = 5.0;

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

            private void Update()
            {
                _elapsed += Time.unscaledDeltaTime;
                if (_elapsed < _nextReport) return;
                _nextReport += ReportIntervalSeconds;

                SurfacePrepareTimingSnapshot timing =
                    RenderingDiagnosticsComposition.GetSurfacePrepareTiming();
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
                    + "upload[p95={28:0.000} p99={29:0.000} max={30:0.000}]",
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
                    timing.UploadP95Ms, timing.UploadP99Ms, timing.UploadMaxMs));
            }
        }
    }
}