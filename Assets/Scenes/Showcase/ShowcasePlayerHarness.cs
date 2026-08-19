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
            if (!log && runSeconds <= 0.0 && autoWalkAfter <= 0.0 && screenshotDir == null) return;

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
            UnityEngine.Object.DontDestroyOnLoad(root);
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

            private bool _walking;
            private double _nextShot;
            private int _shotIndex;
            private bool _shotThisFrame;

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

                if (!_walking && AutoWalkAfter > 0.0 && _totalElapsed >= AutoWalkAfter)
                {
                    var showcase = UnityEngine.Object.FindFirstObjectByType<VoxelShowcase>();
                    if (showcase != null)
                    {
                        showcase.AutoWalk = true;
                        _walking = true;
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

                if (RunSeconds > 0.0 && _totalElapsed >= RunSeconds)
                {
                    Debug.Log($"HARNESS done after {_totalElapsed:0.0}s");
                    Application.Quit();
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

                string phase = _walking ? "walking" : "stationary";
                string file = string.Format(CultureInfo.InvariantCulture,
                    "showcase-{0:D3}-t{1:000.0}s-{2}.png", _shotIndex++, _totalElapsed, phase);
                string path = Path.Combine(ScreenshotDirectory, file);

                ScreenCapture.CaptureScreenshot(path);
                _shotThisFrame = true;
                Debug.Log($"HARNESS screenshot {path}");
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
