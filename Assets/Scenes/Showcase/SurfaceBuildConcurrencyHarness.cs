using System;
using System.Globalization;
using UnityEngine;
using VoxelEngine.Composition;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Benchmark-only override for the renderer's visible-convergence build ceiling.
    ///
    /// The existing showcase <c>-voxel-max-builds</c> knob changes both scheduler ceilings,
    /// which makes it unsuitable for an A/B whose question is specifically whether the production
    /// converging ceiling of 12 saturates the job pool. This hook changes only the converging
    /// value and preserves one bounded steady-state prefetch build, matching production.
    /// </summary>
    public static class SurfaceBuildConcurrencyHarness
    {
        private const string ArgumentName = "-voxel-converging-builds";
        private const int ConvergedPrefetchBuilds = 1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Apply()
        {
            int converging = ReadIntArgument(ArgumentName, -1);
            if (converging < 0) return;

            RenderingComposition.SetVoxelBuildConcurrency(converging, ConvergedPrefetchBuilds);
            Debug.Log($"HARNESS build concurrency converging={converging} converged={ConvergedPrefetchBuilds}");
        }

        private static int ReadIntArgument(string name, int fallback)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (!string.Equals(args[i], name, StringComparison.Ordinal)) continue;
                if (int.TryParse(args[i + 1], NumberStyles.Integer,
                                 CultureInfo.InvariantCulture, out int parsed))
                    return parsed;
                return fallback;
            }
            return fallback;
        }
    }
}
