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
    /// batch root or batch Mesh may be rebuilt. Severed trees must continue colliding with their
    /// remaining stump until the root-most standing segment is removed.
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
                Assert.That(renderer.DynamicPresentationCount, Is.EqualTo(0),
                            "Healthy batched trees must not retain per-tree GameObjects.");
                Assert.That(renderer.DynamicMeshCount, Is.EqualTo(0));
                Assert.That(renderer.ResidentSkeletonCount, Is.EqualTo(0),
                            "Healthy batch construction must release streamed skeletons.");
                Assert.That(renderer.PeakResidentSkeletonCountDuringLastRebuild,
                            Is.LessThanOrEqualTo(1));
                Assert.That(renderer.GeneratedMeshCount, Is.EqualTo(6));
                Assert.That(renderer.ResidentRenderObjectCount, Is.EqualTo(8));
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
                Assert.That(touchedFilters.Length, Is.EqualTo(3));
                var touchedMeshes = new Mesh[3];
                int touchedVertices = 0;
                for (int i = 0; i < touchedFilters.Length; i++)
                {
                    touchedMeshes[i] = touchedFilters[i].sharedMesh;
                    Assert.That(touchedMeshes[i], Is.Not.Null);
                    Assert.That(touchedMeshes[i].subMeshCount, Is.EqualTo(2));
                    touchedVertices += touchedMeshes[i].vertexCount;
                }
                Assert.That(touchedVertices, Is.GreaterThan(0),
                            "Healthy batch exists but contains no real geometry.");

                // Foliage damage alone is enough to release a tree from the static batch. This path
                // used to synchronously rebuild all three meshes for every healthy neighbour in the
                // same 32 m cell, producing visible frame hitches.
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
                Assert.That(object.ReferenceEquals(preservedTouchedBatch, touchedBatch), Is.True,
                            "Damage replaced the touched batch root instead of updating its indices in place.");
                Assert.That(object.ReferenceEquals(preservedUntouchedBatch, untouchedBatch), Is.True,
                            "Damage touched an unrelated spatial batch.");

                MeshFilter[] afterFilters = preservedTouchedBatch.GetComponentsInChildren<MeshFilter>(true);
                Assert.That(afterFilters.Length, Is.EqualTo(3));
                for (int i = 0; i < afterFilters.Length; i++)
                    Assert.That(object.ReferenceEquals(afterFilters[i].sharedMesh, touchedMeshes[i]), Is.True,
                                $"Damage rebuilt batch LOD{i} instead of retaining its Mesh buffer.");

                Assert.That(renderer.TryGetDynamicPresentationRoot(0, out Transform damagedRoot), Is.True);
                Assert.That(damagedRoot, Is.Not.Null);
                Assert.That(damagedRoot.GetComponentsInChildren<MeshRenderer>(true).Length, Is.EqualTo(3));
                for (int i = 1; i < instances.Length; i++)
                    Assert.That(renderer.TryGetDynamicPresentationRoot(i, out _), Is.False,
                                $"Healthy neighbour {i} was unnecessarily materialized.");

                // Gameplay broadphase and exact skeleton residency must remain bounded independently
                // of renderer residency.
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

                // Regression for the immortal-stump bug. First sever the crown above the base, then
                // prove the remaining lower trunk still participates in collision and can itself be
                // destroyed. Once its root-most segment is cut, no semantic tree geometry may hit.
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

                int upperLowerTrunk = FindLowerTrunkSegment(skeleton, minimumIndex: 2);
                Assert.That(upperLowerTrunk, Is.GreaterThanOrEqualTo(2));
                TreeBranchSegment firstSegment = skeleton.Branches[upperLowerTrunk];
                float3 firstImpact = destructibleTree.PositionMetres
                                   + (firstSegment.Start + firstSegment.End) * 0.5f;
                ProceduralTreeDamageService.ApplyBlast(
                    firstImpact, 0.10f, new float3(1f, 0f, 0f));

                Assert.That(TreeWorldRuntime.Damage[0].Severed, Is.True,
                            "The first lower-trunk impact did not sever the crown.");
                IReadOnlyCollection<int> firstCuts = TreeWorldRuntime.RemovedBranches(0);
                Assert.That(firstCuts.Count, Is.GreaterThan(0));

                int stumpBranch = FindLowestStandingTrunkSegment(skeleton, firstCuts);
                Assert.That(stumpBranch, Is.GreaterThanOrEqualTo(0),
                            "Test sever unexpectedly removed the entire root; no stump remained to exercise.");
                TreeBranchSegment stump = skeleton.Branches[stumpBranch];
                float3 stumpMid = destructibleTree.PositionMetres
                                + (stump.Start + stump.End) * 0.5f;
                bool stumpHit = ProceduralTreeDamageService.TrySweepImpact(
                    stumpMid + new float3(-1.5f, 0f, 0f),
                    stumpMid + new float3(1.5f, 0f, 0f),
                    0.20f, out _, out int stumpTreeIndex);
                Assert.That(stumpHit, Is.True,
                            "A severed tree's visible stump became non-collidable.");
                Assert.That(stumpTreeIndex, Is.EqualTo(0));

                ProceduralTreeDamageService.ApplyBlast(
                    stumpMid, 0.10f, new float3(1f, 0f, 0f));
                IReadOnlyCollection<int> finalCuts = TreeWorldRuntime.RemovedBranches(0);
                for (int branch = 0; branch < skeleton.Branches.Count; branch++)
                {
                    Assert.That(TreeSkeletonTopology.IsBranchRemoved(
                                    skeleton, finalCuts, branch), Is.True,
                                $"Branch {branch} survived the root-most stump destruction.");
                }

                bool ghostHit = ProceduralTreeDamageService.TrySweepImpact(
                    stumpMid + new float3(-1.5f, 0f, 0f),
                    stumpMid + new float3(1.5f, 0f, 0f),
                    0.20f, out _, out _);
                Assert.That(ghostHit, Is.False,
                            "Fully destroyed tree still has semantic collision after its root is gone.");

                // Let presentation consume the final BranchCut. There should be no standing dynamic
                // tree left; only temporary detached physics debris may remain.
                for (int frame = 0; frame < 10; frame++) yield return null;
                Assert.That(renderer.DynamicPresentationCount, Is.EqualTo(0),
                            "Fully destroyed root left a standing dynamic tree presentation.");
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

        private static int FindLowestStandingTrunkSegment(
            TreeSkeletonSnapshot skeleton, IReadOnlyCollection<int> cuts)
        {
            for (int i = 0; i < skeleton.Branches.Count; i++)
            {
                if (skeleton.Branches[i].Level != 0) continue;
                if (!TreeSkeletonTopology.IsBranchRemoved(skeleton, cuts, i)) return i;
            }
            return -1;
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