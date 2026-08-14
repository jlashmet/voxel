using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
using VoxelEngine.Core.Vegetation;
using VoxelEngine.Rendering.Vegetation;
using TreeInstance = VoxelEngine.Core.Vegetation.TreeInstance;

namespace VoxelEngine.CI
{
    /// <summary>
    /// Performance/correctness contract for GameObject-free standing-tree rendering. Healthy trees
    /// stay data-only and spatially batched, damaged standing trees move to standalone GPU-submitted
    /// meshes, and only detached physics debris may materialize GameObjects.
    /// </summary>
    public sealed class TreeBatchRenderingTests
    {
        [UnityTest]
        public IEnumerator HealthyForest_UsesNoStandingTreeGameObjects_AndDamageReleasesOneTree()
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
                TreeWorldState.Replace(instances);

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
                Assert.That(renderer.ResidentSkeletonCount, Is.EqualTo(0),
                            "Healthy batch construction must release streamed skeletons.");
                Assert.That(renderer.PeakResidentSkeletonCountDuringLastRebuild,
                            Is.LessThanOrEqualTo(1));

                // Each spatial batch owns LOD0/1/2 plus one ultra-far impostor mesh. None of those
                // meshes require MeshRenderer, MeshFilter, LODGroup, or per-batch GameObjects.
                Assert.That(renderer.BatchMeshCount, Is.EqualTo(8));
                Assert.That(renderer.GeneratedMeshCount, Is.EqualTo(8));
                Assert.That(renderer.ResidentRenderObjectCount, Is.EqualTo(0));
                Assert.That(renderer.EstimatedVisibleDrawCount, Is.EqualTo(4));
                Assert.That(renderer.transform.childCount, Is.EqualTo(0),
                            "Standing tree batches must not materialize child GameObjects.");

                for (int i = 0; i < instances.Length; i++)
                    Assert.That(renderer.TryGetDynamicPresentationRoot(i, out _), Is.False);

                // Foliage damage releases exactly one tree from the static batch without rebuilding
                // either batch. The damaged standing tree still remains GameObject-free.
                TreeWorldState.SetDamage(0, 0.70f, false);
                for (int frame = 0;
                     frame < 60 && (renderer.BatchedTreeCount != instances.Length - 1
                                    || renderer.DynamicPresentationCount != 1
                                    || renderer.LastDamageBatchReleaseCount != 1);
                     frame++)
                    yield return null;

                Assert.That(renderer.BatchCount, Is.EqualTo(2));
                Assert.That(renderer.BatchMeshCount, Is.EqualTo(8));
                Assert.That(renderer.BatchedTreeCount, Is.EqualTo(instances.Length - 1));
                Assert.That(renderer.DynamicPresentationCount, Is.EqualTo(1));
                Assert.That(renderer.DynamicMeshCount, Is.EqualTo(4));
                Assert.That(renderer.ResidentSkeletonCount, Is.EqualTo(1));
                Assert.That(renderer.LastDamageBatchRebuildCount, Is.EqualTo(0),
                            "Runtime tree damage must never rebuild a spatial batch.");
                Assert.That(renderer.LastDamageBatchReleaseCount, Is.EqualTo(1),
                            "Exactly the affected tree should be punched out of its batch.");
                Assert.That(renderer.ResidentRenderObjectCount, Is.EqualTo(0));
                Assert.That(renderer.transform.childCount, Is.EqualTo(0),
                            "Damaged standing trees must not materialize child GameObjects.");
                Assert.That(renderer.TryGetDynamicPresentationRoot(0, out _), Is.False);
                for (int i = 1; i < instances.Length; i++)
                    Assert.That(renderer.TryGetDynamicPresentationRoot(i, out _), Is.False);

                // Gameplay broadphase and skeleton residency stay bounded independently of renderer
                // residency.
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
                TreeWorldState.Replace(sparseTrees);

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
                // destroyed. Fully removing the root must leave no standing GPU presentation.
                var destructibleTree = new TreeInstance
                {
                    PositionMetres = new float3(0f, 0f, 0f),
                    Species = TreeSpecies.Oak,
                    Seed = 0xC0FFEE11u,
                    Scale = 1f,
                };
                TreeWorldState.Replace(new[] { destructibleTree });
                ProceduralTreeSkeleton skeleton = ProceduralTreeDamageService.SkeletonFor(0);
                Assert.That(skeleton, Is.Not.Null);
                Assert.That(skeleton.Branches.Count, Is.GreaterThan(4));

                int upperLowerTrunk = FindLowerTrunkSegment(skeleton, minimumIndex: 2);
                Assert.That(upperLowerTrunk, Is.GreaterThanOrEqualTo(2));
                TreeBranchSegment firstSegment = skeleton.Branches[upperLowerTrunk];
                float3 firstImpact = destructibleTree.PositionMetres
                                   + (firstSegment.Start + firstSegment.End) * 0.5f;
                ProceduralTreeDamageService.ApplyBlast(
                    firstImpact, 0.10f, new float3(1f, 0f, 0f));

                Assert.That(TreeWorldState.Damage[0].Severed, Is.True);
                IReadOnlyCollection<int> firstCuts = TreeWorldState.RemovedBranches(0);
                Assert.That(firstCuts.Count, Is.GreaterThan(0));

                int stumpBranch = FindLowestStandingTrunkSegment(skeleton, firstCuts);
                Assert.That(stumpBranch, Is.GreaterThanOrEqualTo(0));
                TreeBranchSegment stump = skeleton.Branches[stumpBranch];
                float3 stumpMid = destructibleTree.PositionMetres
                                + (stump.Start + stump.End) * 0.5f;
                bool stumpHit = ProceduralTreeDamageService.TrySweepImpact(
                    stumpMid + new float3(-1.5f, 0f, 0f),
                    stumpMid + new float3(1.5f, 0f, 0f),
                    0.20f, out _, out int stumpTreeIndex);
                Assert.That(stumpHit, Is.True);
                Assert.That(stumpTreeIndex, Is.EqualTo(0));

                ProceduralTreeDamageService.ApplyBlast(
                    stumpMid, 0.10f, new float3(1f, 0f, 0f));
                IReadOnlyCollection<int> finalCuts = TreeWorldState.RemovedBranches(0);
                for (int branch = 0; branch < skeleton.Branches.Count; branch++)
                {
                    Assert.That(ProceduralTreeSkeletonBuilder.IsBranchRemoved(
                                    skeleton, finalCuts, branch), Is.True,
                                $"Branch {branch} survived root-most stump destruction.");
                }

                bool ghostHit = ProceduralTreeDamageService.TrySweepImpact(
                    stumpMid + new float3(-1.5f, 0f, 0f),
                    stumpMid + new float3(1.5f, 0f, 0f),
                    0.20f, out _, out _);
                Assert.That(ghostHit, Is.False);

                for (int frame = 0; frame < 10; frame++) yield return null;
                Assert.That(renderer.DynamicPresentationCount, Is.EqualTo(0));
                Assert.That(renderer.ResidentRenderObjectCount, Is.EqualTo(0));
            }
            finally
            {
                TreeWorldState.Replace(System.Array.Empty<TreeInstance>());
            }
        }

        private static int FindLowerTrunkSegment(ProceduralTreeSkeleton skeleton, int minimumIndex)
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
            ProceduralTreeSkeleton skeleton, IReadOnlyCollection<int> cuts)
        {
            for (int i = 0; i < skeleton.Branches.Count; i++)
            {
                if (skeleton.Branches[i].Level != 0) continue;
                if (!ProceduralTreeSkeletonBuilder.IsBranchRemoved(skeleton, cuts, i)) return i;
            }
            return -1;
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
