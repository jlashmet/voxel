using System;
using System.Globalization;
using Unity.Profiling;
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
    /// ProfilerRecorder values and direct solid-arena upload telemetry are sampled every frame and
    /// reduced to one-second maxima so a hitch cannot disappear merely because the reporting frame
    /// itself happened to be cheap.
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
            private ProfilerRecorder _schedulerPrepareRecorder;
            private ProfilerRecorder _workerAdmissionRecorder;
            private ProfilerRecorder _workerPrepareRecorder;
            private ProfilerRecorder _solidUploadRecorder;
            private ProfilerRecorder _gcCollectRecorder;
            private long _schedulerPrepareMaxNs;
            private long _workerAdmissionMaxNs;
            private long _workerPrepareMaxNs;
            private long _solidUploadMaxNs;
            private long _gcCollectMaxNs;
            private double _arenaUploadMaxMs;
            private int _arenaUploadMaxCalls;
            private long _arenaUploadMaxBytes;
            private int _arenaUploadMaxFrame = -1;
            private double _admissionMaxMs;
            private double _admissionSolidMs;
            private double _admissionArenaReliefMs;
            private double _admissionWaterMs;
            private double _admissionScheduleMs;
            private int _admissionMaxFrame = -1;

            private void Start()
            {
                _schedulerPrepareRecorder = ProfilerRecorder.StartNew(
                    ProfilerCategory.Scripts, "Voxel.Surface.SchedulerPrepare", 1);
                _workerAdmissionRecorder = ProfilerRecorder.StartNew(
                    ProfilerCategory.Scripts, "Voxel.Surface.WorkerAdmission", 1);
                _workerPrepareRecorder = ProfilerRecorder.StartNew(
                    ProfilerCategory.Scripts, "Voxel.Surface.WorkerPrepare", 1);
                _solidUploadRecorder = ProfilerRecorder.StartNew(
                    ProfilerCategory.Scripts, "Voxel.Surface.Upload", 1);
                _gcCollectRecorder = ProfilerRecorder.StartNew(
                    ProfilerCategory.Memory, "GC.Collect", 1);
                ResetGcBaselines();
            }

            private void OnDestroy()
            {
                _schedulerPrepareRecorder.Dispose();
                _workerAdmissionRecorder.Dispose();
                _workerPrepareRecorder.Dispose();
                _solidUploadRecorder.Dispose();
                _gcCollectRecorder.Dispose();
            }

            private void ResetGcBaselines()
            {
                _lastAllocatedBytes = GC.GetAllocatedBytesForCurrentThread();
                _lastGen0Collections = GC.CollectionCount(0);
                _lastGen1Collections = GC.CollectionCount(1);
                _lastGen2Collections = GC.CollectionCount(2);
            }

            private static long MaxRecorderValue(in ProfilerRecorder recorder, long current)
            {
                return recorder.Valid ? Math.Max(current, recorder.LastValue) : current;
            }

            private void SampleProfilerMaxima()
            {
                _schedulerPrepareMaxNs = MaxRecorderValue(
                    in _schedulerPrepareRecorder, _schedulerPrepareMaxNs);
                _workerAdmissionMaxNs = MaxRecorderValue(
                    in _workerAdmissionRecorder, _workerAdmissionMaxNs);
                _workerPrepareMaxNs = MaxRecorderValue(
                    in _workerPrepareRecorder, _workerPrepareMaxNs);
                _solidUploadMaxNs = MaxRecorderValue(
                    in _solidUploadRecorder, _solidUploadMaxNs);
                _gcCollectMaxNs = MaxRecorderValue(
                    in _gcCollectRecorder, _gcCollectMaxNs);

                SurfaceArenaUploadFrameSnapshot arenaUpload =
                    RenderingDiagnosticsComposition.GetSurfaceArenaUploadFrame();
                if (arenaUpload.WallMs > _arenaUploadMaxMs)
                {
                    _arenaUploadMaxMs = arenaUpload.WallMs;
                    _arenaUploadMaxCalls = arenaUpload.Calls;
                    _arenaUploadMaxBytes = arenaUpload.Bytes;
                    _arenaUploadMaxFrame = arenaUpload.Frame;
                }

                SurfaceAdmissionFrameSnapshot admission =
                    RenderingDiagnosticsComposition.GetSurfaceAdmissionFrame();
                if (admission.TotalMs <= _admissionMaxMs) return;
                _admissionMaxMs = admission.TotalMs;
                _admissionSolidMs = admission.SolidMs;
                _admissionArenaReliefMs = admission.ArenaReliefMs;
                _admissionWaterMs = admission.WaterMs;
                _admissionScheduleMs = admission.ScheduleBatchedJobsMs;
                _admissionMaxFrame = admission.Frame;
            }

            private void ResetProfilerMaxima()
            {
                _schedulerPrepareMaxNs = 0;
                _workerAdmissionMaxNs = 0;
                _workerPrepareMaxNs = 0;
                _solidUploadMaxNs = 0;
                _gcCollectMaxNs = 0;
                _arenaUploadMaxMs = 0.0;
                _arenaUploadMaxCalls = 0;
                _arenaUploadMaxBytes = 0;
                _arenaUploadMaxFrame = -1;
                _admissionMaxMs = 0.0;
                _admissionSolidMs = 0.0;
                _admissionArenaReliefMs = 0.0;
                _admissionWaterMs = 0.0;
                _admissionScheduleMs = 0.0;
                _admissionMaxFrame = -1;
            }

            private static double NanosecondsToMilliseconds(long nanoseconds) =>
                nanoseconds * 0.000001;

            private void Update()
            {
                SampleProfilerMaxima();
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
                double admissionResidualMs = Math.Max(
                    0.0, _admissionMaxMs - _admissionSolidMs - _admissionArenaReliefMs
                       - _admissionWaterMs - _admissionScheduleMs);

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
                    + "jobs={31} missing={32} gc[g0=+{33} g1=+{34} g2=+{35}] allocMain={36} "
                    + "frameMax[scheduler={37:0.000} admission={38:0.000} "
                    + "worker={39:0.000} solidUpload={40:0.000} gcCollect={41:0.000}] "
                    + "arenaUpload[ms={42:0.000} calls={43} bytes={44} frame={45}] "
                    + "admissionFrame[total={46:0.000} solid={47:0.000} relief={48:0.000} "
                    + "water={49:0.000} schedule={50:0.000} residual={51:0.000} frame={52}]",
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
                    gen0Delta, gen1Delta, gen2Delta, allocatedDelta,
                    NanosecondsToMilliseconds(_schedulerPrepareMaxNs),
                    NanosecondsToMilliseconds(_workerAdmissionMaxNs),
                    NanosecondsToMilliseconds(_workerPrepareMaxNs),
                    NanosecondsToMilliseconds(_solidUploadMaxNs),
                    NanosecondsToMilliseconds(_gcCollectMaxNs),
                    _arenaUploadMaxMs, _arenaUploadMaxCalls, _arenaUploadMaxBytes,
                    _arenaUploadMaxFrame,
                    _admissionMaxMs, _admissionSolidMs, _admissionArenaReliefMs,
                    _admissionWaterMs, _admissionScheduleMs, admissionResidualMs,
                    _admissionMaxFrame));

                // Exclude this diagnostic's own formatting/logging allocations from the next
                // interval as much as possible by taking the next baseline after the log call.
                ResetGcBaselines();
                ResetProfilerMaxima();
            }
        }
    }
}