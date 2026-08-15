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
using VoxelEngine.Vegetation.Api;
using VoxelEngine.Rendering.Vegetation;
using TreeInstance = VoxelEngine.Vegetation.Api.TreeInstance;
using Object = UnityEngine.Object;

namespace VoxelEngine.CI
{
    /// <summary>
    /// Bounded scaling report for semantic vegetation. Healthy batched trees are data-only after
    /// rebuild: resident meshes/GameObjects belong only to spatial batches plus trees that actually
    /// need dynamic presentations. Large-count projections use the same deterministic layout.
    /// </summary>
    public sealed class TreePerformanceBenchmarkTests
    {
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
                "mode,count,elapsed_ms,branches,leaves,presentations,resident_meshes,batches,batch_meshes,batched_trees,dynamic_trees,semantic_triangles_all_lods,estimated_resident_triangle_storage,allocated_memory_bytes,avg_frame_ms,max_frame_ms,render_objects,estimated_visible_draws"
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
                "note=healthy batched trees retain no per-tree GameObjects or meshes; batch construction currently uses temporary source Mesh objects that are destroyed after CombineMeshes",
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
                int expectedDynamicTrees = count - expectedBatchedTrees;
                Assert.That(renderer.BatchCount, Is.EqualTo(expectedBatches),
                            "Runtime batch count no longer matches the benchmark spatial-cell model.");
                Assert.That(renderer.BatchedTreeCount, Is.EqualTo(expectedBatchedTrees));
                Assert.That(renderer.DynamicPresentationCount, Is.EqualTo(expectedDynamicTrees),
                            "Healthy batched trees must not retain dynamic per-tree presentations.");
                Assert.That(renderer.DynamicMeshCount, Is.EqualTo(expectedDynamicTrees * 3));
                Assert.That(renderer.GeneratedMeshCount,
                            Is.EqualTo(renderer.BatchMeshCount + renderer.DynamicMeshCount));
                Assert.That(renderer.ResidentRenderObjectCount,
                            Is.EqualTo((expectedBatches + expectedDynamicTrees) * 4));
                Assert.That(renderer.EstimatedVisibleDrawCount, Is.LessThan(count * 2));

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
                long estimatedTriangleStorage = renderer.TotalTriangleCountAllLods;

                csv.Add(string.Join(",",
                    "runtime", count, F(renderer.LastRebuildMilliseconds), "", "",
                    renderer.PresentationCount, renderer.GeneratedMeshCount,
                    renderer.BatchCount, renderer.BatchMeshCount, renderer.BatchedTreeCount,
                    renderer.DynamicPresentationCount, renderer.TotalTriangleCountAllLods,
                    estimatedTriangleStorage, memoryDelta, F(avgFrame), F(frameMax),
                    renderer.ResidentRenderObjectCount, renderer.EstimatedVisibleDrawCount));
                text.Add($"runtime {count}: rebuild={renderer.LastRebuildMilliseconds:F2} ms, " +
                         $"semanticPresentations={renderer.PresentationCount:N0}, residentMeshes={renderer.GeneratedMeshCount:N0}, " +
                         $"batches={renderer.BatchCount:N0}, batchMeshes={renderer.BatchMeshCount:N0}, " +
                         $"batchedTrees={renderer.BatchedTreeCount:N0}, dynamicTrees={renderer.DynamicPresentationCount:N0}, " +
                         $"visibleDraws≈{renderer.EstimatedVisibleDrawCount:N0} vs perTree={count * 2:N0}, " +
                         $"semanticTriangles(all LODs)={renderer.TotalTriangleCountAllLods:N0}, " +
                         $"residentTriangleStorage≈{estimatedTriangleStorage:N0}, " +
                         $"renderObjects={renderer.ResidentRenderObjectCount:N0}, allocatedDelta={memoryDelta:N0} B, " +
                         $"avgFrame={avgFrame:F2} ms, maxFrame={frameMax:F2} ms");
                Flush(csvPath, txtPath, csv, text);
            }

            foreach (int count in ProjectionCounts)
            {
                ProjectBatchLayout(count, out int projectedBatches, out int projectedBatchedTrees);
                int projectedDynamicTrees = count - projectedBatchedTrees;
                long semanticTriangles = (long)Math.Round(trianglesPerTreeAllLods * count);
                long residentMeshes = (projectedBatches + projectedDynamicTrees) * 3L;
                long batchMeshes = projectedBatches * 3L;
                long renderObjects = (projectedBatches + projectedDynamicTrees) * 4L;
                long visibleDraws = (projectedBatches + projectedDynamicTrees) * 2L;

                csv.Add(string.Join(",",
                    "projection", count, "", "", "", count, residentMeshes,
                    projectedBatches, batchMeshes, projectedBatchedTrees, projectedDynamicTrees,
                    semanticTriangles, semanticTriangles, "", "", "", renderObjects,
                    visibleDraws));
                text.Add($"projection {count}: batches≈{projectedBatches:N0}, " +
                         $"batchedTrees≈{projectedBatchedTrees:N0}, dynamicTrees≈{projectedDynamicTrees:N0}, " +
                         $"renderObjects≈{renderObjects:N0}, residentMeshes≈{residentMeshes:N0}, " +
                         $"batchMeshes≈{batchMeshes:N0}, visibleDraws≈{visibleDraws:N0} " +
                         $"(per-tree path={count * 2L:N0}), semanticTriangles≈{semanticTriangles:N0}, " +
                         $"residentTriangleStorage≈{semanticTriangles:N0}");
            }

            text.Add("recommendation=healthy tree residency duplication is removed; next scaling target is direct batch-buffer construction so rebuilds stop creating temporary per-tree Unity Mesh objects before CombineMeshes");
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
