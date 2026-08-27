using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace VoxelEngine.Showcase.Editor
{
    /// <summary>
    /// Builds a standalone player containing a single showcase scene, for frame-rate measurement.
    ///
    /// The project had no player build path at all, so every performance number to date came from
    /// either a batchmode test that never presents a frame or the editor, whose own render loop
    /// and Profiler window are part of what gets measured. Neither can answer what the game
    /// actually runs at. This builds the smallest player that can.
    ///
    ///   -voxelScene <path>        scene to build, default Assets/Scenes/SmallVoxelShowcase.unity
    ///   -voxelBuildOutput <dir>   output directory for the .app
    ///   -voxelDevelopment         development build for SceneIssue replay instrumentation
    ///   -voxelFrameTimingStats    enable Unity CPU/GPU FrameTiming data in this player only
    /// </summary>
    public static class ShowcasePlayerBuild
    {
        private const string DefaultScene = "Assets/Scenes/SmallVoxelShowcase.unity";

        public static void Build()
        {
            string scene = Argument("-voxelScene") ?? DefaultScene;
            string output = Argument("-voxelBuildOutput")
                            ?? Path.Combine(Directory.GetCurrentDirectory(), "Artifacts/Player");
            bool development = HasFlag("-voxelDevelopment");
            bool frameTimingStats = HasFlag("-voxelFrameTimingStats");

            if (!File.Exists(scene))
                throw new FileNotFoundException($"No scene at {scene}", scene);

            Directory.CreateDirectory(output);
            string appName = Path.GetFileNameWithoutExtension(scene) + ".app";
            string appPath = Path.Combine(output, appName);

            // A stale .app from an earlier build is not overwritten cleanly by Unity when the
            // scene set changes, and a half-replaced bundle fails to launch in a way that looks
            // like a runtime fault rather than a build one.
            if (Directory.Exists(appPath)) Directory.Delete(appPath, true);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { scene },
                locationPathName = appPath,
                target = BuildTarget.StandaloneOSX,
                targetGroup = BuildTargetGroup.Standalone,
                options = development
                    ? BuildOptions.Development
                    : BuildOptions.None
            };

            // FrameTimingManager only reports CPU/GPU breakdowns when the player was built with
            // frame timing stats enabled. This is a benchmark-build concern, not a project policy:
            // restore the serialized setting even if BuildPipeline throws so selecting this harness
            // never dirties ProjectSettings or changes ordinary player builds.
            bool previousFrameTimingStats = PlayerSettings.enableFrameTimingStats;
            BuildReport report;
            try
            {
                if (frameTimingStats) PlayerSettings.enableFrameTimingStats = true;
                report = BuildPipeline.BuildPlayer(options);
            }
            finally
            {
                PlayerSettings.enableFrameTimingStats = previousFrameTimingStats;
            }

            BuildSummary summary = report.summary;
            Debug.Log($"ShowcasePlayerBuild {summary.result} -> {appPath} "
                      + $"({summary.totalSize / (1024 * 1024)} MB, {summary.totalTime.TotalSeconds:0}s, "
                      + $"errors {summary.totalErrors}, frameTiming={frameTimingStats})");

            if (summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException(
                    $"Player build failed: {summary.result}, {summary.totalErrors} errors.");
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
    }
}
