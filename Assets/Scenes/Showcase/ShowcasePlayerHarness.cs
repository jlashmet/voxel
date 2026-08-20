using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Command-line controls for measuring a showcase scene in a standalone player.
    ///
    /// Frame cost has so far been measured in two places that both distort it. A batchmode test
    /// renders to a RenderTexture and never presents, so a presentation stall is invisible to it.
    /// The editor adds its own render loop, an open Profiler window, and frame-rate throttling on
    /// top of the game's. A player build is the only environment where the frame rate means what
    /// it says, and this makes one measurable without a human watching the overlay.
    ///
    /// All of it is opt-in from the command line, so a player launched normally behaves normally.
    ///
    ///   -voxel-uncapped          disable VSync and any target frame rate
    ///   -voxel-fps-log           write one line per second of frame statistics to the player log
    ///   -voxel-run-seconds N     quit after N seconds
    ///   -voxel-autowalk-after N  start walking a scripted loop N seconds in
    ///   -voxel-screenshot-dir D  write periodic screenshots to D
    ///   -voxel-screenshot-every N  seconds between screenshots, default 10
    /// </summary>
    public static class ShowcasePlayerHarness
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (UnityEngine.Object.FindFirstObjectByType<VoxelShowcase>() == null) return;

            // Subsystem A/B, for attributing frame cost in a scene that renders many things at
            // once. Disabling a renderer here is a measurement, not a setting: it answers "what
            // does this cost" directly instead of inferring it from a profile.
            string disable = Argument("-voxel-disable");
            if (!string.IsNullOrEmpty(disable))
            {
                foreach (string name in disable.Split(','))
                    DisableSubsystem(name.Trim());
            }

            double arenaMb = Value("-voxel-arena-mb", 0.0);
            if (arenaMb > 0.0)
            {
                VoxelEngine.Composition.RenderingComposition.SetVoxelArenaBudgetBytes(
                    (long)(arenaMb * 1024 * 1024));
                Debug.Log($"HARNESS arena budget {arenaMb} MB");
            }

            int maxBuilds = (int)Value("-voxel-max-builds", -1.0);
            if (maxBuilds >= 0)
            {
                VoxelEngine.Composition.RenderingComposition.SetVoxelBuildConcurrency(
                    maxBuilds, Math.Min(maxBuilds, 1));
                Debug.Log($"HARNESS build concurrency converging={maxBuilds}");
            }

            double buildBudget = Value("-voxel-build-budget-ms", -1.0);
            if (buildBudget >= 0.0)
            {
                double scale = Value("-voxel-build-budget-scale", 8.0);
                VoxelEngine.Composition.RenderingComposition.SetVoxelBuildBudgetMs(
                    buildBudget, scale);
                Debug.Log($"HARNESS build budget {buildBudget} ms x{scale}");
            }

            // A player pauses when its window loses focus, and an automated run has no reason to
            // hold focus — the log simply stops, the process stays alive, and the main thread
            // parks in the event loop. That reads as a hang in the thing being measured.
            Application.runInBackground = true;

            if (HasFlag("-voxel-track-flicker"))
            {
                VoxelEngine.Composition.RenderingComposition.SetTrackSurfaceReappearance(true);
                Debug.Log("HARNESS tracking surface reappearance");
            }

            if (HasFlag("-voxel-uncapped"))
            {
                // Both are needed: vSyncCount overrides targetFrameRate whenever it is non-zero,
                // so clearing only one of them still leaves the player pinned to the display.
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = -1;
                Debug.Log("HARNESS uncapped: vSync off, targetFrameRate unset");
            }

            bool log = HasFlag("-voxel-fps-log");
            double runSeconds = Value("-voxel-run-seconds", 0.0);
            double autoWalkAfter = Value("-voxel-autowalk-after", 0.0);
            string screenshotDir = Argument("-voxel-screenshot-dir");
            if (!log && runSeconds <= 0.0 && autoWalkAfter <= 0.0 && screenshotDir == null
                && Value("-voxel-freeze-builds-after", 0.0) <= 0.0
                && Value("-voxel-survey-after", 0.0) <= 0.0) return;

            var root = new GameObject("Showcase Player Harness")
            {
                hideFlags = HideFlags.DontSave
            };
            Reporter reporter = root.AddComponent<Reporter>();
            reporter.Logging = log;
            reporter.RunSeconds = runSeconds;
            reporter.AutoWalkAfter = autoWalkAfter;
            reporter.ScreenshotDirectory = screenshotDir;
            reporter.ScreenshotEvery = Value("-voxel-screenshot-every", 10.0);
            reporter.FreezeBuildsAfter = Value("-voxel-freeze-builds-after", 0.0);
            reporter.AssertFrom = Value("-voxel-assert-from", 0.0);
            reporter.RecedeAfter = Value("-voxel-recede-after", 0.0);
            reporter.SurveyAfter = Value("-voxel-survey-after", 0.0);
            reporter.SurveyHeight = (float)Value("-voxel-survey-height", 55.0);
            reporter.SurveySpin = (float)Value("-voxel-survey-spin", 30.0);
            reporter.RecedeSpeed = (float)Value("-voxel-recede-speed", 8.0);
            reporter.RecedeMaxDistance = (float)Value("-voxel-recede-max", 360.0);
            UnityEngine.Object.DontDestroyOnLoad(root);
        }

        private static void DisableSubsystem(string name)
        {
            switch (name)
            {
                case "vegetation":
                    DisableBehavioursNamed("ProceduralVegetationBatchRenderer");
                    break;
                case "trees":
                    DisableBehavioursNamed("ProceduralTreeRenderer");
                    break;
                case "ambientlife":
                    DisableBehavioursNamed("ProceduralAmbientLifeBatchRenderer");
                    break;
                case "farterrain":
                    DisableBehavioursNamed("VoxelFarTerrain");
                    break;
                case "visible-eviction":
                    VoxelEngine.Composition.RenderingComposition
                        .SetEvictVisibleUnderArenaPressure(false);
                    Debug.Log("HARNESS disabled eviction of on-screen chunks");
                    break;
                case "water":
                    VoxelEngine.Composition.RenderingComposition.SetWaterRenderEnabled(false);
                    Debug.Log("HARNESS disabled water surface");
                    break;
                case "voxels":
                    VoxelEngine.Composition.RenderingComposition.SetSurfaceBuildEnabled(false);
                    Debug.Log("HARNESS disabled voxels (surface build)");
                    break;
                default:
                    Debug.LogWarning($"HARNESS unknown subsystem '{name}'");
                    break;
            }
        }

        /// <summary>
        /// Disables every behaviour whose type has the given short name.
        ///
        /// Matching on the short name rather than the namespace-qualified one is deliberate: the
        /// architecture guards read scene sources as text, and a fully-qualified renderer
        /// Runtime namespace in a string literal reads to them as a boundary violation even
        /// though nothing here references the type.
        /// </summary>
        private static void DisableBehavioursNamed(string typeName)
        {
            int disabled = 0;
            var behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] == null) continue;
                if (behaviours[i].GetType().Name != typeName) continue;
                behaviours[i].enabled = false;
                disabled++;
            }
            Debug.Log($"HARNESS disabled {disabled} of {typeName}");
        }

        private static string Argument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.Ordinal)) return args[i + 1];
            return null;
        }

        private static bool HasFlag(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
                if (string.Equals(args[i], name, StringComparison.Ordinal)) return true;
            return false;
        }

        private static double Value(string name, double fallback)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.Ordinal)
                    && double.TryParse(args[i + 1], NumberStyles.Float,
                                       CultureInfo.InvariantCulture, out double parsed))
                    return parsed;
            return fallback;
        }

        /// <summary>
        /// Accumulates raw frame intervals and reports them once a second.
        ///
        /// The interesting number here is not the mean. A run that averages 200 FPS while
        /// stalling for 100 ms twice a second is the symptom being chased, so the worst frame in
        /// each window is reported alongside the percentiles rather than smoothed away.
        /// </summary>
        private sealed class Reporter : MonoBehaviour
        {
            private const int Capacity = 4096;

            internal bool Logging;
            internal double RunSeconds;
            internal double AutoWalkAfter;
            internal string ScreenshotDirectory;
            internal double ScreenshotEvery;
            internal double FreezeBuildsAfter;

            /// <summary>
            /// Seconds after which the correctness invariants below are enforced. Before it the
            /// world is still filling and every one of them is legitimately violated, so a check
            /// that ran from the first frame would only ever assert that startup exists.
            /// </summary>
            internal double AssertFrom;

            private int _failures;
            internal double RecedeAfter;
            internal double SurveyAfter;
            internal float SurveyHeight;
            internal float SurveySpin;

            private bool _surveying;
            internal float RecedeSpeed;
            internal float RecedeMaxDistance;

            private bool _receding;

            private bool _walking;
            private bool _frozen;
            private double _nextShot;
            private int _shotIndex;
            private bool _shotThisFrame;

            // Per-frame surface counts. A flicker is geometry that leaves and returns within a
            // few frames, so it cannot be seen at the one-second cadence the frame line uses:
            // sampling has to be every frame, and what matters is the excursion, not the mean.
            private int _lastVisible = -1;
            private int _visibleMin;
            private int _visibleMax;
            private int _visibleDrops;
            private int _visibleDropWorst;
            private int _missingMax;
            private ulong _reappearBase;

            private readonly float[] _intervals = new float[Capacity];
            private int _count;
            private float _windowElapsed;
            private float _totalElapsed;
            private int _window;

            private void Update()
            {
                float dt = Time.unscaledDeltaTime;
                _totalElapsed += dt;
                _windowElapsed += dt;

                // The frame after a capture carries the cost of encoding and writing a PNG, which
                // is tens of milliseconds and nothing to do with the renderer. Counting it would
                // put a spike in every percentile at exactly the screenshot interval and invite
                // the conclusion that something periodic is wrong.
                if (_shotThisFrame) _shotThisFrame = false;
                else if (_count < Capacity) _intervals[_count++] = dt;

                CaptureIfDue();

                VoxelEngine.Composition.RenderingComposition.GetVoxelSurfaceCounts(
                    out int visibleNow, out int missingNow);
                if (_lastVisible < 0)
                {
                    _visibleMin = _visibleMax = visibleNow;
                }
                else
                {
                    if (visibleNow < _visibleMin) _visibleMin = visibleNow;
                    if (visibleNow > _visibleMax) _visibleMax = visibleNow;
                    int drop = _lastVisible - visibleNow;
                    if (drop > 0)
                    {
                        _visibleDrops++;
                        if (drop > _visibleDropWorst) _visibleDropWorst = drop;
                    }
                }
                if (missingNow > _missingMax) _missingMax = missingNow;
                _lastVisible = visibleNow;

                // Freezing surface building once the view has filled separates the two costs
                // that a slow frame can hide. Extraction stops; everything already extracted
                // keeps drawing. If the frame rate recovers, the cost was building; if it does
                // not, the cost was drawing what was built.
                if (!_frozen && FreezeBuildsAfter > 0.0 && _totalElapsed >= FreezeBuildsAfter)
                {
                    _frozen = true;
                    VoxelEngine.Composition.RenderingComposition.SetSurfaceBuildEnabled(false);
                    Debug.Log($"HARNESS froze surface builds at t={_totalElapsed:0.0}s");
                }

                if (!_surveying && SurveyAfter > 0.0 && _totalElapsed >= SurveyAfter)
                {
                    var vantage = UnityEngine.Object.FindFirstObjectByType<VoxelShowcase>();
                    if (vantage != null)
                    {
                        vantage.SurveyHeightMetres = SurveyHeight;
                        vantage.SurveySpinDegreesPerSecond = SurveySpin;
                        vantage.AutoSurvey = true;
                        _surveying = true;
                        ArmCoverageLatch();
                        Debug.Log($"HARNESS survey on at t={_totalElapsed:0.0}s");
                    }
                }

                if (!_receding && RecedeAfter > 0.0 && _totalElapsed >= RecedeAfter)
                {
                    var target = UnityEngine.Object.FindFirstObjectByType<VoxelShowcase>();
                    if (target != null)
                    {
                        target.RecedeSpeedMetresPerSecond = RecedeSpeed;
                        target.RecedeMaxDistanceMetres = RecedeMaxDistance;
                        target.AutoRecede = true;
                        _receding = true;
                        ArmCoverageLatch();
                        Debug.Log($"HARNESS recede on at t={_totalElapsed:0.0}s");
                    }
                }

                if (!_walking && AutoWalkAfter > 0.0 && _totalElapsed >= AutoWalkAfter)
                {
                    var showcase = UnityEngine.Object.FindFirstObjectByType<VoxelShowcase>();
                    if (showcase != null)
                    {
                        showcase.AutoWalk = true;
                        _walking = true;
                        ArmCoverageLatch();
                        Debug.Log($"HARNESS autowalk on at t={_totalElapsed:0.0}s");
                    }
                }

                if (Logging && _windowElapsed >= 1f)
                {
                    Report();
                    _windowElapsed = 0f;
                    _count = 0;
                    _window++;
                }

                if (AssertFrom > 0.0 && _totalElapsed >= AssertFrom) CheckInvariants();

                if (RunSeconds > 0.0 && _totalElapsed >= RunSeconds)
                {
                    Debug.Log($"HARNESS done after {_totalElapsed:0.0}s, "
                            + $"assertion failures {_failures}");
                    Application.Quit(_failures == 0 ? 0 : 1);
                }
            }

            /// <summary>
            /// Writes a screenshot on a fixed interval, tagged with the phase it was taken in.
            ///
            /// A frame-rate number says the renderer is fast; it does not say the world looks
            /// right. Holes, doubled terrain and missing landmarks have all passed timing checks
            /// in this project, so a CI run that reports frame cost without publishing pictures
            /// is reporting half the result.
            /// </summary>
            private void CaptureIfDue()
            {
                if (string.IsNullOrEmpty(ScreenshotDirectory)) return;
                if (_totalElapsed < _nextShot) return;

                _nextShot = _totalElapsed + Math.Max(1.0, ScreenshotEvery);

                try
                {
                    Directory.CreateDirectory(ScreenshotDirectory);
                }
                catch (Exception error)
                {
                    Debug.LogError($"HARNESS screenshot directory failed: {error.Message}");
                    ScreenshotDirectory = null;
                    return;
                }

                string phase = _surveying ? "survey"
                             : _receding ? "recede"
                             : _walking ? "walking" : "stationary";
                string file = string.Format(CultureInfo.InvariantCulture,
                    "showcase-{0:D3}-t{1:000.0}s-{2}.png", _shotIndex++, _totalElapsed, phase);
                string path = Path.Combine(ScreenshotDirectory, file);

                ScreenCapture.CaptureScreenshot(path);
                _shotThisFrame = true;
                Debug.Log($"HARNESS screenshot {path}");
            }

            /// <summary>
            /// Correctness invariants, checked every frame once the world has settled.
            ///
            /// These are the defects this scene actually produced, expressed as assertions so a
            /// regression is a failed run rather than something a person has to notice in a
            /// screenshot: geometry must never be given up once drawn, a still camera must have
            /// no unbuilt chunks in view, and a still camera must draw a stable set.
            /// </summary>
            private void CheckInvariants()
            {
                VoxelEngine.Composition.RenderingComposition.GetVoxelSurfaceCounts(
                    out int visible, out int missing);

                // Geometry the renderer had and gave up, at any time, moving or not. Terrain that
                // leaves the view and returns is never correct.
                ulong reappeared =
                    VoxelEngine.Composition.RenderingComposition.GetSurfaceReappearances();
                if (!_assertBaselineTaken)
                {
                    // The counter is cumulative from startup, and startup legitimately churns.
                    // Only what happens after the world settles is a regression.
                    _assertBaselineTaken = true;
                    _assertedReappearances = reappeared;
                }
                else if (reappeared > _assertedReappearances)
                {
                    Fail($"chunk reappeared after leaving the drawn set "
                       + $"({reappeared - _assertedReappearances} since last frame)");
                    _assertedReappearances = reappeared;
                }

                if (visible <= 0) Fail("no chunks drawn");

                // Only while still: a moving camera legitimately outruns extraction, and holding
                // it to zero would assert that the machine is fast rather than that it is right.
                if (_walking || _receding || (_surveying && SurveySpin > 0f)) return;

                // Latch rather than time out. A vantage change has to extract the view behind it
                // from nothing, and how long that takes is a property of the machine — asserting
                // a fixed settle window would only measure hardware. What is always true is that
                // once a still camera has drawn everything it wants, it must keep drawing it.
                // Surface counts describe the frame that was drawn, which is the one before the
                // camera moved. Without this the latch re-arms on the teleport frame, sees the
                // old view's zero, and then fires on the new view's first frame.
                if (_coverageGraceFrames > 0)
                {
                    _coverageGraceFrames--;
                    return;
                }

                if (missing == 0)
                {
                    _reachedFullCoverage = true;
                    return;
                }

                if (_reachedFullCoverage)
                    Fail($"{missing} chunks wanted and not drawn after the view was complete");
            }

            private bool _reachedFullCoverage;
            private int _coverageGraceFrames;

            private void ArmCoverageLatch()
            {
                _reachedFullCoverage = false;
                _coverageGraceFrames = 4;
            }
            private ulong _assertedReappearances;
            private bool _assertBaselineTaken;

            private void Fail(string reason)
            {
                _failures++;
                // One line per distinct failure, not one per frame: a broken invariant is broken
                // every frame and the log is the artefact a person reads.
                if (_failures <= 20) Debug.LogError($"ASSERT t={_totalElapsed:0.0} {reason}");
            }

            private void Report()
            {
                if (_count == 0) return;

                var sorted = new float[_count];
                Array.Copy(_intervals, sorted, _count);
                Array.Sort(sorted);

                float mean = 0f;
                for (int i = 0; i < _count; i++) mean += sorted[i];
                mean /= _count;

                // Ring residency alongside the frame line: a LOD ring meshing terrain that a
                // finer ring also covers is invisible in a frame-time number and shows up in a
                // screenshot only as patches that look like a shading bug.
                string rings = VoxelEngine.Composition.RenderingComposition.DescribeVoxelRings();
                if (rings != null) Debug.Log(rings);

                var showcase = UnityEngine.Object.FindFirstObjectByType<VoxelShowcase>();
                if (showcase != null)
                {
                    Debug.Log(showcase.DescribeFarTerrain());
                    Debug.Log($"DIST landmark={showcase.DistanceToLandmarkMetres:0.#}m");
                }

                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "SURFACE t={0:0.0} visible={1} min={2} max={3} swing={4} "
                    + "drops={5} worstDrop={6} missingMax={7} reappeared={8}",
                    _totalElapsed, _lastVisible, _visibleMin, _visibleMax,
                    _visibleMax - _visibleMin, _visibleDrops, _visibleDropWorst, _missingMax,
                    VoxelEngine.Composition.RenderingComposition.GetSurfaceReappearances()
                        - _reappearBase));
                _reappearBase =
                    VoxelEngine.Composition.RenderingComposition.GetSurfaceReappearances();
                _visibleMin = _visibleMax = _lastVisible;
                _visibleDrops = 0;
                _visibleDropWorst = 0;
                _missingMax = 0;

                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "FPSLOG t={0:0.0} frames={1} fps={2:0.0} "
                    + "ms p50={3:0.00} p95={4:0.00} p99={5:0.00} max={6:0.00} mean={7:0.00}",
                    _totalElapsed, _count, _count / Mathf.Max(0.0001f, _windowElapsed),
                    Percentile(sorted, 0.50) * 1000f, Percentile(sorted, 0.95) * 1000f,
                    Percentile(sorted, 0.99) * 1000f, sorted[_count - 1] * 1000f, mean * 1000f));
            }

            private static float Percentile(float[] sorted, double fraction)
            {
                int index = Mathf.Clamp(
                    Mathf.CeilToInt((float)(fraction * sorted.Length)) - 1, 0, sorted.Length - 1);
                return sorted[index];
            }
        }
    }
}
