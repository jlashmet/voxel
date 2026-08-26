using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
using VoxelEngine.Vegetation.Runtime;
using VoxelEngine.Vegetation.Api;
using VoxelEngine.Rendering.Runtime.Vegetation;
using TreeInstance = VoxelEngine.Vegetation.Api.TreeInstance;

namespace VoxelEngine.CI
{
    /// <summary>
    /// Performance/correctness contract for tree batching and damage. Healthy trees stay data-only.
    /// First damage must punch only the affected tree out of its existing batch index buffers -- no
    /// batch root or batch Mesh may be rebuilt. A structural trunk sever retires the rooted semantic
    /// tree immediately; detached presentation owns the falling tree from that point onward.
    /// </summary>
    public sealed class TreeBatchRenderingTests
    {
        [UnityTest]
        public IEnumerator HealthyForest_BatchesVisibly_AndDamageReleasesOneTree()
        {
            try
            {
                ProceduralTreeRenderer renderer = null;
                for (int frame = 0; frame < 60; frame++)
                {
                    renderer = FindRuntimeRenderer();
                    if (renderer != null) break;
                    yield return null;
                }
                Assert.That(renderer, Is.Not.Null);

                var instances = new TreeInstance[8];
                for (int i = 0; i < instances.Length; i++)
                {
                    bool secondCell = i >= 4;
                    int local = i % 4;
                    instances[i] = new TreeInstance
                    {
                        PositionMetres = new float3(
                            (secondCell ? 35f : 3f) + local * 5f,
                            0f,
                            3f + (local % 2) * 5f),
                        Species = (TreeSpecies)(i % 7),
                        Seed = 0xA341316Cu + (uint)i * 2654435761u,
                        Scale = 0.92f + (i % 3) * 0.06f,
                    };
                }
                TreeWorldRuntime.Replace(instances);

                for (int frame = 0;
                     frame < 90 && (renderer.PresentationCount != instances.Length
                                    || renderer.BatchCount != 2
                                    || renderer.BatchedTreeCount != instances.Length);
                     frame++)
                    yield return null;

                Assert.That(renderer.PresentationCount, Is.EqualTo(instances.Length));
                Assert.That(renderer.BatchCount, Is.EqualTo(2));
                Assert.That(renderer.BatchedTreeCount, Is.EqualTo(instances.Length));
                Assert.That(renderer.DynamicPresentationCount, Is.EqualTo(0));
                Assert.That(renderer.DynamicMeshCount, Is.EqualTo(0));
                Assert.That(renderer.ResidentSkeletonCount, Is.EqualTo(0));
                Assert.That(renderer.PeakResidentSkeletonCountDuringLastRebuild,
                            Is.LessThanOrEqualTo(1));
                Assert.That(renderer.GeneratedMeshCount, Is.EqualTo(8));
                Assert.That(renderer.ResidentRenderObjectCount, Is.EqualTo(10));
                Assert.That(renderer.EstimatedVisibleDrawCount, Is.EqualTo(4));

                for (int i = 0; i < instances.Length; i++)
                {
                    Assert.That(renderer.TryGetDynamicPresentationRoot(i, out _), Is.False);
                    Assert.That(FindTreeRoot(renderer, i), Is.Null);
                }

                Transform touchedBatch = FindBatchRoot(renderer, new Vector2Int(0, 0));
                Transform untouchedBatch = FindBatchRoot(renderer, new Vector2Int(1, 0));
                Assert.That(touchedBatch, Is.Not.Null);
                Assert.That(untouchedBatch, Is.Not.Null);

                MeshFilter[] touchedFilters = touchedBatch.GetComponentsInChildren<MeshFilter>(true);
                Assert.That(touchedFilters.Length, Is.EqualTo(4));
                var touchedMeshes = new Mesh[4];
                int touchedVertices = 0;
                for (int i = 0; i < touchedFilters.Length; i++)
                {
                    touchedMeshes[i] = touchedFilters[i].sharedMesh;
                    Assert.That(touchedMeshes[i], Is.Not.Null);
                    Assert.That(touchedMeshes[i].subMeshCount, Is.EqualTo(i < 3 ? 2 : 1));
                    touchedVertices += touchedMeshes[i].vertexCount;
                }
                Assert.That(touchedVertices, Is.GreaterThan(0));

                TreeWorldRuntime.SetDamage(0, 0.70f, false, float3.zero, float3.zero, -1);
                for (int frame = 0;
                     frame < 60 && (renderer.BatchedTreeCount != instances.Length - 1
                                    || renderer.DynamicPresentationCount != 1
                                    || renderer.LastDamageBatchReleaseCount != 1);
                     frame++)
                    yield return null;

                Assert.That(renderer.BatchCount, Is.EqualTo(2));
                Assert.That(renderer.BatchedTreeCount, Is.EqualTo(instances.Length - 1));
                Assert.That(renderer.DynamicPresentationCount, Is.EqualTo(1));
                Assert.That(renderer.ResidentSkeletonCount, Is.EqualTo(1));
                Assert.That(renderer.LastDamageBatchRebuildCount, Is.EqualTo(0),
                            "Runtime tree damage must never rebuild a spatial batch.");
                Assert.That(renderer.LastDamageBatchReleaseCount, Is.EqualTo(1),
                            "Exactly the affected tree should be punched out of its batch.");

                Transform preservedTouchedBatch = FindBatchRoot(renderer, new Vector2Int(0, 0));
                Transform preservedUntouchedBatch = FindBatchRoot(renderer, new Vector2Int(1, 0));
                Assert.That(object.ReferenceEquals(preservedTouchedBatch, touchedBatch), Is.True);
                Assert.That(object.ReferenceEquals(preservedUntouchedBatch, untouchedBatch), Is.True);

                MeshFilter[] afterFilters = preservedTouchedBatch.GetComponentsInChildren<MeshFilter>(true);
                Assert.That(afterFilters.Length, Is.EqualTo(4));
                for (int i = 0; i < afterFilters.Length; i++)
                    Assert.That(object.ReferenceEquals(afterFilters[i].sharedMesh, touchedMeshes[i]), Is.True,
                                $"Damage rebuilt batch LOD{i} instead of retaining its Mesh buffer.");

                Assert.That(renderer.TryGetDynamicPresentationRoot(0, out Transform damagedRoot), Is.True);
                Assert.That(damagedRoot, Is.Not.Null);
                Assert.That(damagedRoot.GetComponentsInChildren<MeshRenderer>(true).Length, Is.EqualTo(4));
                for (int i = 1; i < instances.Length; i++)
                    Assert.That(renderer.TryGetDynamicPresentationRoot(i, out _), Is.False);

                var sparseTrees = new TreeInstance[80];
                for (int i = 0; i < sparseTrees.Length; i++)
                {
                    sparseTrees[i] = new TreeInstance
                    {
                        PositionMetres = new float3(i * 96f, 0f, 0f),
                        Species = TreeSpecies.Oak,
                        Seed = 0x9E3779B9u + (uint)i * 2246822519u,
                        Scale = 1f,
                    };
                }
                TreeWorldRuntime.Replace(sparseTrees);

                int maxBroadphaseCandidates = 0;
                int maxSkeletonBuildsPerQuery = 0;
                for (int i = 0; i < sparseTrees.Length; i++)
                {
                    float3 root = sparseTrees[i].PositionMetres;
                    bool hit = ProceduralTreeDamageService.TrySweepImpact(
                        root + new float3(-2f, 2f, 0f),
                        root + new float3(2f, 2f, 0f),
                        0.35f,
                        out _, out int hitTreeIndex);
                    Assert.That(hit, Is.True, $"Sparse broadphase sweep missed tree {i}.");
                    Assert.That(hitTreeIndex, Is.EqualTo(i));
                    maxBroadphaseCandidates = Mathf.Max(
                        maxBroadphaseCandidates,
                        ProceduralTreeDamageService.LastBroadphaseCandidateCount);
                    maxSkeletonBuildsPerQuery = Mathf.Max(
                        maxSkeletonBuildsPerQuery,
                        ProceduralTreeDamageService.LastQuerySkeletonBuildCount);
                }
                Assert.That(maxBroadphaseCandidates, Is.LessThanOrEqualTo(2));
                Assert.That(maxSkeletonBuildsPerQuery, Is.LessThanOrEqualTo(1));
                Assert.That(ProceduralTreeDamageService.ResidentSkeletonCount,
                            Is.EqualTo(ProceduralTreeDamageService.SkeletonCacheCapacity));

                // Structural sever regression. A lower-trunk hit records its exact segment, but
                // topology must retire the entire rooted tree in one transition. This prevents both
                // the visible standing trunk from SceneIssue 033015 and an invisible ghost collider.
                var destructibleTree = new TreeInstance
                {
                    PositionMetres = new float3(0f, 0f, 0f),
                    Species = TreeSpecies.Oak,
                    Seed = 0xC0FFEE11u,
                    Scale = 1f,
                };
                TreeWorldRuntime.Replace(new[] { destructibleTree });
                TreeSkeletonSnapshot skeleton = ProceduralTreeDamageService.SkeletonFor(0);
                Assert.That(skeleton, Is.Not.Null);
                Assert.That(skeleton.Branches.Count, Is.GreaterThan(4));

                int lowerTrunk = FindLowerTrunkSegment(skeleton, minimumIndex: 2);
                Assert.That(lowerTrunk, Is.GreaterThanOrEqualTo(2));
                TreeBranchSegment segment = skeleton.Branches[lowerTrunk];
                float3 impact = destructibleTree.PositionMetres
                              + (segment.Start + segment.End) * 0.5f;
                ProceduralTreeDamageService.ApplyBlast(
                    impact, 0.10f, new float3(1f, 0f, 0f));

                Assert.That(TreeWorldRuntime.Damage[0].Severed, Is.True);
                IReadOnlyCollection<int> cuts = TreeWorldRuntime.RemovedBranches(0);
                Assert.That(cuts.Count, Is.EqualTo(1),
                            "A structural sever should preserve one exact direct-cut event.");
                for (int branch = 0; branch < skeleton.Branches.Count; branch++)
                {
                    Assert.That(TreeSkeletonTopology.IsBranchRemoved(skeleton, cuts, branch), Is.True,
                                $"Rooted branch {branch} survived a level-zero trunk sever.");
                }

                TreeBranchSegment rootSegment = skeleton.Branches[0];
                float3 rootMid = destructibleTree.PositionMetres
                               + (rootSegment.Start + rootSegment.End) * 0.5f;
                bool ghostHit = ProceduralTreeDamageService.TrySweepImpact(
                    rootMid + new float3(-1.5f, 0f, 0f),
                    rootMid + new float3(1.5f, 0f, 0f),
                    0.20f, out _, out _);
                Assert.That(ghostHit, Is.False,
                            "A severed tree remained collision-queryable after its rooted presentation retired.");

                for (int frame = 0; frame < 10; frame++) yield return null;
                Assert.That(renderer.DynamicPresentationCount, Is.EqualTo(0),
                            "Structural sever left a standing dynamic tree presentation.");
            }
            finally
            {
                TreeWorldRuntime.Replace(System.Array.Empty<TreeInstance>());
            }
        }

        private static int FindLowerTrunkSegment(TreeSkeletonSnapshot skeleton, int minimumIndex)
        {
            int result = -1;
            for (int i = minimumIndex; i < skeleton.Branches.Count; i++)
            {
                TreeBranchSegment branch = skeleton.Branches[i];
                if (branch.Level != 0) continue;
                float midpointY = (branch.Start.y + branch.End.y) * 0.5f;
                if (midpointY >= skeleton.Height * 0.48f) continue;
                result = i;
            }
            return result;
        }

        private static Transform FindBatchRoot(ProceduralTreeRenderer renderer, Vector2Int key)
        {
            string name = $"Tree Batch {key.x},{key.y}";
            for (int i = 0; i < renderer.transform.childCount; i++)
            {
                Transform child = renderer.transform.GetChild(i);
                if (!child.gameObject.activeInHierarchy) continue;
                if (child.name == name) return child;
            }
            return null;
        }

        private static Transform FindTreeRoot(ProceduralTreeRenderer renderer, int index)
        {
            string prefix = $"Tree {index:000} ";
            for (int i = 0; i < renderer.transform.childCount; i++)
            {
                Transform child = renderer.transform.GetChild(i);
                if (!child.gameObject.activeInHierarchy) continue;
                if (child.name.StartsWith(prefix)) return child;
            }
            return null;
        }

        private static ProceduralTreeRenderer FindRuntimeRenderer()
        {
            ProceduralTreeRenderer[] all = Resources.FindObjectsOfTypeAll<ProceduralTreeRenderer>();
            foreach (ProceduralTreeRenderer renderer in all)
            {
                if (renderer == null || renderer.gameObject == null) continue;
                if (!renderer.gameObject.scene.IsValid()) continue;
                return renderer;
            }
            return null;
        }
    }
}
