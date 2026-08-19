using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace VoxelEngine.Showcase.Editor
{
    /// <summary>
    /// Turns a binary profiler capture into a text report of where CPU time went.
    ///
    /// A batchmode run can write a <c>.raw</c> capture but nothing in the repo could read one, so
    /// deep-profiling a headless run produced an artefact only a human with the Profiler window
    /// open could interpret. This loads the capture and aggregates self time per sample across
    /// every frame, which is the "hot methods" view — and, because it reports per thread, it also
    /// distinguishes a main thread that is busy from one parked in a wait.
    /// </summary>
    public static class ProfilerCaptureReport
    {
        private const int MaxReportedSamples = 60;

        public static void Report()
        {
            string input = Argument("-profileInput");
            string output = Argument("-profileOutput");
            if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(output))
                throw new InvalidOperationException(
                    "ProfilerCaptureReport needs -profileInput <capture.raw> -profileOutput <report.txt>.");

            if (!File.Exists(input))
                throw new FileNotFoundException($"No profiler capture at {input}", input);

            if (!ProfilerDriver.LoadProfile(input, false))
                throw new InvalidOperationException($"Unity refused to load the capture at {input}.");

            int first = ProfilerDriver.firstFrameIndex;
            int last = ProfilerDriver.lastFrameIndex;
            if (first < 0 || last < first)
                throw new InvalidOperationException(
                    $"Capture at {input} holds no frames (first={first}, last={last}).");

            // Self time per sample, and total time per thread, both accumulated over every frame
            // in the capture so one slow frame cannot masquerade as a trend.
            var selfMsByThreadSample = new Dictionary<string, Dictionary<string, double>>();
            var totalMsByThread = new Dictionary<string, double>();
            int frameCount = 0;

            var iterator = new ProfilerFrameDataIterator();
            for (int frame = first; frame <= last; frame++)
            {
                int threadCount = iterator.GetThreadCount(frame);
                if (threadCount <= 0) continue;
                frameCount++;

                for (int thread = 0; thread < threadCount; thread++)
                {
                    iterator.SetRoot(frame, thread);
                    string threadName = iterator.GetThreadName();
                    if (string.IsNullOrEmpty(threadName)) threadName = $"thread{thread}";

                    var depths = new List<int>();
                    var names = new List<string>();
                    var durations = new List<double>();
                    while (iterator.Next(true))
                    {
                        depths.Add(iterator.depth);
                        names.Add(iterator.name);
                        durations.Add(iterator.durationMS);
                    }
                    if (names.Count == 0) continue;

                    if (!selfMsByThreadSample.TryGetValue(threadName,
                                                          out Dictionary<string, double> samples))
                    {
                        samples = new Dictionary<string, double>();
                        selfMsByThreadSample[threadName] = samples;
                    }

                    // The iterator does not guarantee roots at depth zero, so the shallowest
                    // sample present defines the root level for this thread. Assuming zero left
                    // every thread total at nothing and printed an empty report.
                    int rootDepth = int.MaxValue;
                    for (int i = 0; i < depths.Count; i++)
                        if (depths[i] < rootDepth) rootDepth = depths[i];

                    // Samples arrive depth-first in pre-order, so a sample's direct children are
                    // the following entries one level deeper, up to the next entry at its own
                    // level or shallower. Self time is duration minus that.
                    for (int i = 0; i < names.Count; i++)
                    {
                        double childTotal = 0.0;
                        for (int j = i + 1; j < names.Count && depths[j] > depths[i]; j++)
                            if (depths[j] == depths[i] + 1) childTotal += durations[j];

                        double self = Math.Max(0.0, durations[i] - childTotal);
                        samples.TryGetValue(names[i], out double running);
                        samples[names[i]] = running + self;

                        if (depths[i] == rootDepth)
                        {
                            totalMsByThread.TryGetValue(threadName, out double threadTotal);
                            totalMsByThread[threadName] = threadTotal + durations[i];
                        }
                    }
                }
            }
            iterator.Dispose();

            var report = new StringBuilder();
            report.AppendLine($"capture: {input}");
            report.AppendLine($"frames: {frameCount} (profiler frames {first}..{last})");
            report.AppendLine();

            var threadOrder = new List<KeyValuePair<string, double>>(totalMsByThread);
            threadOrder.Sort((a, b) => b.Value.CompareTo(a.Value));

            foreach (KeyValuePair<string, double> thread in threadOrder)
            {
                double perFrame = frameCount > 0 ? thread.Value / frameCount : 0.0;
                report.AppendLine(
                    $"== {thread.Key}: {thread.Value:F1} ms total, {perFrame:F3} ms/frame");

                if (!selfMsByThreadSample.TryGetValue(thread.Key,
                                                      out Dictionary<string, double> samples))
                    continue;

                var hot = new List<KeyValuePair<string, double>>(samples);
                hot.Sort((a, b) => b.Value.CompareTo(a.Value));
                int reported = Math.Min(MaxReportedSamples, hot.Count);
                for (int i = 0; i < reported; i++)
                {
                    double selfPerFrame = frameCount > 0 ? hot[i].Value / frameCount : 0.0;
                    if (selfPerFrame < 0.001) break;
                    report.AppendLine(
                        $"   {selfPerFrame,9:F3} ms/frame  {hot[i].Value,10:F1} ms  {hot[i].Key}");
                }
                report.AppendLine();
            }

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
            File.WriteAllText(output, report.ToString());
            Debug.Log($"ProfilerCaptureReport wrote {output}");
        }

        private static string Argument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.Ordinal))
                    return args[i + 1];
            return null;
        }
    }
}
