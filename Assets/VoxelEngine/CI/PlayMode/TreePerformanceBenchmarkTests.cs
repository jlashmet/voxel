using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.TestTools;
using VoxelEngine.Core.Vegetation;
using VoxelEngine.Rendering.Vegetation;
using TreeInstance = VoxelEngine.Core.Vegetation.TreeInstance;

namespace VoxelEngine.CI
{
    /// <summary>
    /// Reproducible scaling report for semantic vegetation. Core generation is measured through
    /// 5,000 identities without retaining generated skeletons; full runtime presentation is measured
    /// through 1,000 trees so we can see when per-tree GameObjects/meshes become the next bottleneck.
    /// This produces data, not hardware-specific pass/fail frame-time thresholds.
    /// </summary>
    public sealed class TreePerformanceBenchmarkTests
    {
        private static readonly int[] CoreCounts = { 10, 100, 500, 1000, 5000 };
        private static readonly int[] RuntimeCounts = { 10, 100, 500, 1000 };

        [UnityTest]
        public IEnumerator TreeScaling_ProducesBenchmarkArtifact()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            string outputDirectory = Path.Combine(projectRoot, "Artifacts", "TreeBenchmark");
            Directory.CreateDirectory(outputDirectory);
            string csvPath = Path.Combine(outputDirectory, "tree-benchmark.csv");
            string txtPath = Path.Combine(outputDirectory, "tree-benchmark.txt");

            var csv = new List<string>
            {
                "mode,count,elapsed_ms,branches,leaves,presentations,meshes,triangles_all_lods,allocated_memory_bytes,avg_frame_ms,max_frame_ms"
            };
            var text = new List<string>
            {
                $"unity={Application.unityVersion}",
                $"platform={Application.platform}",
                $"device={SystemInfo.deviceModel}",
                $"graphics={SystemInfo.graphicsDeviceName}",
            };

            // Warm deterministic code paths before the first measurement.
            TreeInstance warm = MakeInstance(0);
            ProceduralTreeSkeletonBuilder.Generate(in warm);

            foreach (int count in CoreCounts)
            {
                long beforeMemory = GC.GetTotalMemory(true);
                long branches = 0;
                long leaves = 0;
                var stopwatch = Stopwatch.StartNew();
                for (int i = 0; i < count; i++)
                {
                    TreeInstance instance = MakeInstance(i);
                    ProceduralTreeSkeleton skeleton = ProceduralTreeSkeletonBuilder.Generate(in instance);
                    branches += skeleton.Branches.Count;
                    leaves += skeleton.Leaves.Count;
                }
                stopwatch.Stop();
                long afterMemory = GC.GetTotalMemory(false);
                long deltaMemory = Math.Max(0L, afterMemory - beforeMemory);

                csv.Add(string.Join(",",
                    "core", count,
                    F(stopwatch.Elapsed.TotalMilliseconds),
                    branches, leaves, 0, 0, 0, deltaMemory, "", ""));
                text.Add($"core {count}: {stopwatch.Elapsed.TotalMilliseconds:F2} ms, " +
                         $"branches={branches:N0}, leaves={leaves:N0}, transientDelta={deltaMemory:N0} B");
                yield return null;
            }

            ProceduralTreeRenderer renderer = null;
            for (int frame = 0; frame < 60; frame++)
            {
                renderer = FindRuntimeRenderer();
                if (renderer != null) break;
                yield return null;
            }
            Assert.That(renderer, Is.Not.Null,
                        "Production ProceduralTreeRenderer bootstrap was not available for benchmark.");

            foreach (int count in RuntimeCounts)
            {
                TreeWorldState.Replace(Array.Empty<TreeInstance>());
                for (int frame = 0; frame < 5; frame++) yield return null;

                var instances = new TreeInstance[count];
                for (int i = 0; i < count; i++) instances[i] = MakeInstance(i);

                long beforeAllocated = Profiler.GetTotalAllocatedMemoryLong();
                TreeWorldState.Replace(instances);

                float deadline = Time.realtimeSinceStartup + 30f;
                while (renderer.PresentationCount != count
                       && Time.realtimeSinceStartup < deadline)
                    yield return null;

                Assert.That(renderer.PresentationCount, Is.EqualTo(count),
                            $"Renderer did not realize {count} semantic trees before timeout.");

                double frameTotal = 0.0;
                double frameMax = 0.0;
                const int sampledFrames = 30;
                for (int frame = 0; frame < sampledFrames; frame++)
                {
                    yield return null;
                    double ms = Time.unscaledDeltaTime * 1000.0;
                    frameTotal += ms;
                    frameMax = Math.Max(frameMax, ms);
                }

                long afterAllocated = Profiler.GetTotalAllocatedMemoryLong();
                long memoryDelta = Math.Max(0L, afterAllocated - beforeAllocated);
                double avgFrame = frameTotal / sampledFrames;

                csv.Add(string.Join(",",
                    "runtime", count,
                    F(renderer.LastRebuildMilliseconds),
                    "", "",
                    renderer.PresentationCount,
                    renderer.GeneratedMeshCount,
                    renderer.TotalTriangleCountAllLods,
                    memoryDelta,
                    F(avgFrame), F(frameMax)));
                text.Add($"runtime {count}: rebuild={renderer.LastRebuildMilliseconds:F2} ms, " +
                         $"presentations={renderer.PresentationCount:N0}, meshes={renderer.GeneratedMeshCount:N0}, " +
                         $"triangles(all LODs)={renderer.TotalTriangleCountAllLods:N0}, " +
                         $"allocatedDelta={memoryDelta:N0} B, avgFrame={avgFrame:F2} ms, maxFrame={frameMax:F2} ms");
            }

            TreeWorldState.Replace(Array.Empty<TreeInstance>());
            for (int frame = 0; frame < 3; frame++) yield return null;

            File.WriteAllLines(csvPath, csv);
            File.WriteAllLines(txtPath, text);
            UnityEngine.Debug.Log("Tree performance benchmark:\n" + string.Join("\n", text));

            Assert.That(File.Exists(csvPath), Is.True);
            Assert.That(File.Exists(txtPath), Is.True);
        }

        private static TreeInstance MakeInstance(int index)
        {
            const int columns = 64;
            int x = index % columns;
            int z = index / columns;
            TreeSpecies species = (TreeSpecies)(index % 7);
            return new TreeInstance
            {
                PositionMetres = new float3(x * 4.5f, 0f, z * 4.5f),
                Species = species,
                Seed = 0x9E3779B9u ^ (uint)(index * 747796405 + 2891336453u),
                Scale = 0.88f + (index % 9) * 0.03f,
            };
        }

        private static ProceduralTreeRenderer FindRuntimeRenderer()
        {
            ProceduralTreeRenderer[] all = Resources.FindObjectsOfTypeAll<ProceduralTreeRenderer>();
            foreach (ProceduralTreeRenderer candidate in all)
            {
                if (candidate == null || candidate.gameObject == null) continue;
                if (!candidate.gameObject.scene.IsValid()) continue;
                return candidate;
            }
            return null;
        }

        private static string F(double value) =>
            value.ToString("F3", CultureInfo.InvariantCulture);
    }
}
