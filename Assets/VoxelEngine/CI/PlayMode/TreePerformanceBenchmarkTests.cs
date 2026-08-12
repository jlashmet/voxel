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
using Object = UnityEngine.Object;

namespace VoxelEngine.CI
{
    /// <summary>
    /// Bounded scaling report for semantic vegetation. Large counts measure domain and far-mesh
    /// generation without materializing thousands of GameObjects; the full production presentation
    /// path is measured at smaller counts. Runtime rows now report the spatial batching contract
    /// directly, while large-count projections use the same deterministic placement layout to
    /// estimate batch/object/mesh/draw pressure at 500-5000 trees.
    /// </summary>
    public sealed class TreePerformanceBenchmarkTests
    {
        // Keep these diagnostic constants aligned with ProceduralTreeRenderer. Runtime assertions
        // deliberately fail if the renderer's spatial batching policy changes without updating the
        // benchmark projection model.
        private const float BatchSizeMetres = 32f;
        private const int MinimumTreesPerBatch = 2;

        private static readonly int[] CoreCounts = { 10, 100, 500, 1000, 5000 };
        private static readonly int[] FarMeshCounts = { 10, 100, 500, 1000 };
        private static readonly int[] RuntimeCounts = { 10, 50, 100, 250 };
        private static readonly int[] ProjectionCounts = { 500, 1000, 5000 };

        [UnityTest, Timeout(600000)]
        public IEnumerator TreeScaling_ProducesBenchmarkArtifact()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            string outputDirectory = Path.Combine(projectRoot, "Artifacts", "TreeBenchmark");
            Directory.CreateDirectory(outputDirectory);
            string csvPath = Path.Combine(outputDirectory, "tree-benchmark.csv");
            string txtPath = Path.Combine(outputDirectory, "tree-benchmark.txt");

            var csv = new List<string>
            {
                "mode,count,elapsed_ms,branches,leaves,presentations,source_meshes,batches,batch_meshes,batched_trees,dynamic_trees,source_triangles_all_lods,estimated_mesh_triangle_storage,allocated_memory_bytes,avg_frame_ms,max_frame_ms,render_objects,estimated_visible_draws"
            };
            var text = new List<string>
            {
                $"unity={Application.unityVersion}",
                $"platform={Application.platform}",
                $"device={SystemInfo.deviceModel}",
                $"graphics={SystemInfo.graphicsDeviceName}",
                $"batchSizeMetres={BatchSizeMetres:F1}",
                $"minimumTreesPerBatch={MinimumTreesPerBatch}",
                "note=large counts are domain/far-mesh measurements; full renderer is bounded to keep CI responsive",
                "note=batch meshes currently duplicate healthy source-tree geometry so damaged trees can fall back to dormant per-tree meshes immediately",
            };

            TreeInstance warm = MakeInstance(0);
            ProceduralTreeSkeleton warmSkeleton = ProceduralTreeSkeletonBuilder.Generate(in warm);
            Mesh warmMesh = ProceduralTreeMeshBuilder.BuildMesh(warmSkeleton, 2);
            Object.Destroy(warmMesh);
            yield return null;

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
                long deltaMemory = Math.Max(0L, GC.GetTotalMemory(false) - beforeMemory);

                csv.Add(string.Join(",",
                    "core", count, F(stopwatch.Elapsed.TotalMilliseconds), branches, leaves,
                    0, 0, 0, 0, 0, 0, 0, 0, deltaMemory, "", "", 0, 0));
                text.Add($"core {count}: {stopwatch.Elapsed.TotalMilliseconds:F2} ms, " +
                         $"branches={branches:N0}, leaves={leaves:N0}, transientDelta={deltaMemory:N0} B");
                Flush(csvPath, txtPath, csv, text);
                yield return null;
            }

            foreach (int count in FarMeshCounts)
            {
                long triangles = 0;
                var stopwatch = Stopwatch.StartNew();
                for (int i = 0; i < count; i++)
                {
                    TreeInstance instance = MakeInstance(i);
                    ProceduralTreeSkeleton skeleton = ProceduralTreeSkeletonBuilder.Generate(in instance);
                    Mesh mesh = ProceduralTreeMeshBuilder.BuildMesh(skeleton, 2);
                    triangles += (long)mesh.GetIndexCount(0) / 3L;
                    triangles += (long)mesh.GetIndexCount(1) / 3L;
                    Object.Destroy(mesh);
                    if ((i & 63) == 63) yield return null;
                }
                stopwatch.Stop();

                csv.Add(string.Join(",",
                    "far_mesh", count, F(stopwatch.Elapsed.TotalMilliseconds), "", "",
                    0, count, 0, 0, 0, 0, triangles, triangles, "", "", "", 0, 0));
                text.Add($"far mesh {count}: {stopwatch.Elapsed.TotalMilliseconds:F2} ms, " +
                         $"LOD2 triangles={triangles:N0}");
                Flush(csvPath, txtPath, csv, text);
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

            double trianglesPerTreeAllLods = 0.0;
            foreach (int count in RuntimeCounts)
            {
                TreeWorldState.Replace(Array.Empty<TreeInstance>());
                for (int frame = 0; frame < 3; frame++) yield return null;

                var instances = new TreeInstance[count];
                for (int i = 0; i < count; i++) instances[i] = MakeInstance(i);

                long beforeAllocated = Profiler.GetTotalAllocatedMemoryLong();
                TreeWorldState.Replace(instances);

                float deadline = Time.realtimeSinceStartup + 90f;
                while (renderer.PresentationCount != count
                       && Time.realtimeSinceStartup < deadline)
                    yield return null;

                Assert.That(renderer.PresentationCount, Is.EqualTo(count),
                            $"Renderer did not realize {count} semantic trees before timeout.");

                ProjectBatchLayout(count, out int expectedBatches, out int expectedBatchedTrees);
                Assert.That(renderer.BatchCount, Is.EqualTo(expectedBatches),
                            "Runtime batch count no longer matches the benchmark's spatial-cell model.");
                Assert.That(renderer.BatchedTreeCount, Is.EqualTo(expectedBatchedTrees),
                            "Runtime batched-tree count no longer matches the benchmark's spatial-cell model.");
                Assert.That(renderer.EstimatedVisibleDrawCount, Is.LessThan(count * 2),
                            "Healthy forest batching must reduce visible bark+leaf draws below the per-tree path.");

                double frameTotal = 0.0;
                double frameMax = 0.0;
                const int sampledFrames = 15;
                for (int frame = 0; frame < sampledFrames; frame++)
                {
                    yield return null;
                    double ms = Time.unscaledDeltaTime * 1000.0;
                    frameTotal += ms;
                    frameMax = Math.Max(frameMax, ms);
                }

                long memoryDelta = Math.Max(0L,
                    Profiler.GetTotalAllocatedMemoryLong() - beforeAllocated);
                double avgFrame = frameTotal / sampledFrames;
                trianglesPerTreeAllLods = count > 0
                    ? renderer.TotalTriangleCountAllLods / (double)count : 0.0;
                int dynamicTrees = count - renderer.BatchedTreeCount;
                long renderObjects = count * 4L + renderer.BatchCount * 4L;
                long estimatedTriangleStorage = renderer.TotalTriangleCountAllLods
                    + (long)Math.Round(trianglesPerTreeAllLods * renderer.BatchedTreeCount);

                csv.Add(string.Join(",",
                    "runtime", count, F(renderer.LastRebuildMilliseconds), "", "",
                    renderer.PresentationCount, renderer.GeneratedMeshCount,
                    renderer.BatchCount, renderer.BatchMeshCount, renderer.BatchedTreeCount,
                    dynamicTrees, renderer.TotalTriangleCountAllLods, estimatedTriangleStorage,
                    memoryDelta, F(avgFrame), F(frameMax), renderObjects,
                    renderer.EstimatedVisibleDrawCount));
                text.Add($"runtime {count}: rebuild={renderer.LastRebuildMilliseconds:F2} ms, " +
                         $"presentations={renderer.PresentationCount:N0}, sourceMeshes={renderer.GeneratedMeshCount:N0}, " +
                         $"batches={renderer.BatchCount:N0}, batchMeshes={renderer.BatchMeshCount:N0}, " +
                         $"batchedTrees={renderer.BatchedTreeCount:N0}, dynamicTrees={dynamicTrees:N0}, " +
                         $"visibleDraws≈{renderer.EstimatedVisibleDrawCount:N0} vs perTree={count * 2:N0}, " +
                         $"sourceTriangles(all LODs)={renderer.TotalTriangleCountAllLods:N0}, " +
                         $"estimatedTriangleStorage={estimatedTriangleStorage:N0}, " +
                         $"renderObjects={renderObjects:N0}, allocatedDelta={memoryDelta:N0} B, " +
                         $"avgFrame={avgFrame:F2} ms, maxFrame={frameMax:F2} ms");
                Flush(csvPath, txtPath, csv, text);
            }

            foreach (int count in ProjectionCounts)
            {
                ProjectBatchLayout(count, out int projectedBatches, out int projectedBatchedTrees);
                int projectedDynamicTrees = count - projectedBatchedTrees;
                long sourceTriangles = (long)Math.Round(trianglesPerTreeAllLods * count);
                long estimatedTriangleStorage = (long)Math.Round(
                    trianglesPerTreeAllLods * (count + projectedBatchedTrees));
                long renderObjects = count * 4L + projectedBatches * 4L;
                long sourceMeshes = count * 3L;
                long batchMeshes = projectedBatches * 3L;
                long visibleDraws = (projectedBatches + projectedDynamicTrees) * 2L;

                csv.Add(string.Join(",",
                    "projection", count, "", "", "", count, sourceMeshes,
                    projectedBatches, batchMeshes, projectedBatchedTrees, projectedDynamicTrees,
                    sourceTriangles, estimatedTriangleStorage, "", "", "", renderObjects,
                    visibleDraws));
                text.Add($"projection {count}: batches≈{projectedBatches:N0}, " +
                         $"batchedTrees≈{projectedBatchedTrees:N0}, dynamicTrees≈{projectedDynamicTrees:N0}, " +
                         $"renderObjects≈{renderObjects:N0}, sourceMeshes≈{sourceMeshes:N0}, " +
                         $"batchMeshes≈{batchMeshes:N0}, visibleDraws≈{visibleDraws:N0} " +
                         $"(per-tree path={count * 2L:N0}), sourceTriangles≈{sourceTriangles:N0}, " +
                         $"estimatedTriangleStorage≈{estimatedTriangleStorage:N0}");
            }

            text.Add("recommendation=draw-call batching is active; next scaling target is eliminating eager three-LOD per-tree mesh realization and duplicate batch geometry while preserving damage fallback");
            Flush(csvPath, txtPath, csv, text);

            TreeWorldState.Replace(Array.Empty<TreeInstance>());
            for (int frame = 0; frame < 3; frame++) yield return null;

            UnityEngine.Debug.Log("Tree performance benchmark:\n" + string.Join("\n", text));
            Assert.That(File.Exists(csvPath), Is.True);
            Assert.That(File.Exists(txtPath), Is.True);
        }

        private static void ProjectBatchLayout(int count,
                                               out int batchCount,
                                               out int batchedTreeCount)
        {
            var cells = new Dictionary<Vector2Int, int>();
            for (int i = 0; i < count; i++)
            {
                Vector3 position = (Vector3)MakeInstance(i).PositionMetres;
                var key = new Vector2Int(
                    Mathf.FloorToInt(position.x / BatchSizeMetres),
                    Mathf.FloorToInt(position.z / BatchSizeMetres));
                cells.TryGetValue(key, out int occupants);
                cells[key] = occupants + 1;
            }

            batchCount = 0;
            batchedTreeCount = 0;
            foreach (KeyValuePair<Vector2Int, int> cell in cells)
            {
                if (cell.Value < MinimumTreesPerBatch) continue;
                batchCount++;
                batchedTreeCount += cell.Value;
            }
        }

        private static void Flush(string csvPath, string txtPath,
                                  List<string> csv, List<string> text)
        {
            File.WriteAllLines(csvPath, csv);
            File.WriteAllLines(txtPath, text);
        }

        private static TreeInstance MakeInstance(int index)
        {
            const int columns = 64;
            int x = index % columns;
            int z = index / columns;
            TreeSpecies species = (TreeSpecies)(index % 7);
            uint seed = 0x9E3779B9u ^ ((uint)index * 747796405u + 2891336453u);
            if (seed == 0) seed = 1u;
            return new TreeInstance
            {
                PositionMetres = new float3(x * 4.5f, 0f, z * 4.5f),
                Species = species,
                Seed = seed,
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
