using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Screenshot/self-quit fallback for standalone lookdev scenes that do not implement
    /// <see cref="IShowcaseMeasurementDriver"/>.
    ///
    /// The full <see cref="ShowcasePlayerHarness"/> owns capture when a measurable showcase driver
    /// exists. Static benches such as ArchLookdev and TerrainLookdev intentionally have no movement
    /// driver, but they still need the exact same real-player screenshot contract. This fallback
    /// consumes only the common screenshot/run arguments and stays dormant for normal player runs.
    /// </summary>
    public static class RealPlayerScreenshotFallback
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindMeasurementDriver()) return;

            string screenshotDirectory = Argument("-voxel-screenshot-dir");
            double runSeconds = Value("-voxel-run-seconds", 0.0);
            if (string.IsNullOrEmpty(screenshotDirectory) && runSeconds <= 0.0) return;

            Application.runInBackground = true;
            if (HasFlag("-voxel-uncapped"))
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = -1;
            }

            var root = new GameObject("Real Player Screenshot Fallback")
            {
                hideFlags = HideFlags.DontSave
            };
            Reporter reporter = root.AddComponent<Reporter>();
            reporter.ScreenshotDirectory = screenshotDirectory;
            reporter.ScreenshotEvery = Value("-voxel-screenshot-every", 10.0);
            reporter.RunSeconds = runSeconds;
            UnityEngine.Object.DontDestroyOnLoad(root);
        }

        private static bool FindMeasurementDriver()
        {
            MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < behaviours.Length; i++)
                if (behaviours[i] is IShowcaseMeasurementDriver) return true;
            return false;
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

        private sealed class Reporter : MonoBehaviour
        {
            internal string ScreenshotDirectory;
            internal double ScreenshotEvery;
            internal double RunSeconds;

            private double _elapsed;
            private double _nextShot;
            private int _shotIndex;

            private void Update()
            {
                _elapsed += Time.unscaledDeltaTime;

                if (!string.IsNullOrEmpty(ScreenshotDirectory) && _elapsed >= _nextShot)
                {
                    _nextShot = _elapsed + Math.Max(1.0, ScreenshotEvery);
                    Directory.CreateDirectory(ScreenshotDirectory);
                    string path = Path.Combine(
                        ScreenshotDirectory,
                        $"frame_{_shotIndex:D3}_t{_elapsed:000.0}.png");
                    ScreenCapture.CaptureScreenshot(path);
                    Debug.Log($"HARNESS screenshot {path}");
                    _shotIndex++;
                }

                if (RunSeconds > 0.0 && _elapsed >= RunSeconds)
                {
                    Debug.Log($"HARNESS quit after {_elapsed:0.0}s");
                    Application.Quit(0);
                }
            }
        }
    }
}
