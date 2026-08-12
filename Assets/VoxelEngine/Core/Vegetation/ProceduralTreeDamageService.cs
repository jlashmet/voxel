using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace VoxelEngine.Core.Vegetation
{
    /// <summary>
    /// Render-independent collision and damage service for semantic trees. Gameplay supplies metre-
    /// space sweeps/blasts and the service mutates only <see cref="TreeWorldState"/>. Presentation
    /// reacts through typed tree-state events.
    /// </summary>
    public static class ProceduralTreeDamageService
    {
        private const int MinimumVisibleDetachedSegments = 4;

        private sealed class CachedTree
        {
            public ProceduralTreeSkeleton Skeleton;
            public float3 BoundsMin;
            public float3 BoundsMax;
        }

        private static readonly List<CachedTree> s_Cache = new();
        private static int s_CachedRegistryVersion = int.MinValue;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlaySession()
        {
            s_Cache.Clear();
            s_CachedRegistryVersion = int.MinValue;
        }

        public static bool TrySweepImpact(float3 from, float3 to, float sweepRadius,
                                          out float3 hitMetres, out int treeIndex)
        {
            IReadOnlyList<TreeInstance> instances = TreeWorldState.Instances;
            IReadOnlyList<TreeWorldState.TreeDamageState> damage = TreeWorldState.Damage;
            hitMetres = default;
            treeIndex = -1;
            if (instances.Count == 0) return false;

            EnsureCache(instances);
            float3 segmentMin = math.min(from, to) - sweepRadius;
            float3 segmentMax = math.max(from, to) + sweepRadius;
            float bestT = float.PositiveInfinity;

            for (int i = 0; i < instances.Count && i < s_Cache.Count; i++)
            {
                if (i < damage.Count && damage[i].Severed) continue;

                CachedTree cached = s_Cache[i];
                if (!BoundsOverlap(segmentMin, segmentMax,
                                   cached.BoundsMin - sweepRadius,
                                   cached.BoundsMax + sweepRadius))
                    continue;

                TreeInstance instance = instances[i];
                ProceduralTreeSkeleton skeleton = cached.Skeleton;
                IReadOnlyCollection<int> directCuts = TreeWorldState.RemovedBranches(i);
                float3 root = instance.PositionMetres;

                for (int branchIndex = 0; branchIndex < skeleton.Branches.Count; branchIndex++)
                {
                    if (ProceduralTreeSkeletonBuilder.IsBranchRemoved(
                            skeleton, directCuts, branchIndex))
                        continue;

                    TreeBranchSegment branch = skeleton.Branches[branchIndex];
                    float branchRadius = math.max(branch.RadiusStart, branch.RadiusEnd);
                    float radius = math.max(0.01f, sweepRadius + branchRadius);
                    float distanceSq = SegmentSegmentDistanceSq(
                        from, to, root + branch.Start, root + branch.End, out float projectileT);
                    if (distanceSq > radius * radius || projectileT >= bestT) continue;

                    bestT = projectileT;
                    treeIndex = i;
                    hitMetres = math.lerp(from, to, projectileT);
                }

                for (int leafIndex = 0; leafIndex < skeleton.Leaves.Count; leafIndex++)
                {
                    int parent = skeleton.LeafParents != null
                              && leafIndex < skeleton.LeafParents.Length
                        ? skeleton.LeafParents[leafIndex] : -1;
                    if (parent >= 0 && ProceduralTreeSkeletonBuilder.IsBranchRemoved(
                            skeleton, directCuts, parent))
                        continue;

                    TreeLeafAnchor leaf = skeleton.Leaves[leafIndex];
                    float radius = sweepRadius + math.max(0.06f, leaf.Size * 0.35f);
                    float distanceSq = PointSegmentDistanceSq(
                        root + leaf.Position, from, to, out float projectileT);
                    if (distanceSq > radius * radius || projectileT >= bestT) continue;

                    bestT = projectileT;
                    treeIndex = i;
                    hitMetres = math.lerp(from, to, projectileT);
                }
            }

            return treeIndex >= 0;
        }

        public static void ApplyBlast(float3 impactMetres, float blastRadiusMetres,
                                      float3 impulse)
        {
            IReadOnlyList<TreeInstance> instances = TreeWorldState.Instances;
            IReadOnlyList<TreeWorldState.TreeDamageState> damage = TreeWorldState.Damage;
            if (instances.Count == 0) return;

            EnsureCache(instances);
            float blastRadius = math.max(0.1f, blastRadiusMetres);

            for (int treeIndex = 0; treeIndex < instances.Count; treeIndex++)
            {
                if ((uint)treeIndex >= (uint)s_Cache.Count) break;
                if (treeIndex < damage.Count && damage[treeIndex].Severed) continue;

                TreeInstance instance = instances[treeIndex];
                CachedTree cached = s_Cache[treeIndex];
                ProceduralTreeSkeleton skeleton = cached.Skeleton;
                float3 root = instance.PositionMetres;

                float fallbackRadius = math.max(
                    3.0f, math.max(blastRadius * 2.5f, skeleton.Height * 0.35f));
                if (!SphereIntersectsBounds(impactMetres, fallbackRadius,
                                            cached.BoundsMin, cached.BoundsMax))
                    continue;

                var cutCandidates = new List<int>(8);
                int severBranch = -1;
                float severBranchSq = float.PositiveInfinity;
                int hitLeaves = 0;
                int nearestBranch = -1;
                float nearestBranchSq = float.PositiveInfinity;
                IReadOnlyCollection<int> existingCuts = TreeWorldState.RemovedBranches(treeIndex);

                for (int branchIndex = 0; branchIndex < skeleton.Branches.Count; branchIndex++)
                {
                    if (ProceduralTreeSkeletonBuilder.IsBranchRemoved(
                            skeleton, existingCuts, branchIndex))
                        continue;

                    TreeBranchSegment branch = skeleton.Branches[branchIndex];
                    float distanceSq = DistanceToSegmentSq(
                        impactMetres, root + branch.Start, root + branch.End);

                    if (distanceSq < nearestBranchSq)
                    {
                        nearestBranchSq = distanceSq;
                        nearestBranch = branchIndex;
                    }

                    float branchRadius = math.max(branch.RadiusStart, branch.RadiusEnd);
                    float radius = blastRadius + branchRadius;
                    if (distanceSq > radius * radius) continue;

                    if (IsLowerTrunk(in branch, skeleton.Height))
                    {
                        if (distanceSq < severBranchSq)
                        {
                            severBranchSq = distanceSq;
                            severBranch = branchIndex;
                        }
                        continue;
                    }

                    if (!cutCandidates.Contains(branchIndex)) cutCandidates.Add(branchIndex);
                }

                for (int leafIndex = 0; leafIndex < skeleton.Leaves.Count; leafIndex++)
                {
                    TreeLeafAnchor leaf = skeleton.Leaves[leafIndex];
                    float radius = blastRadius + leaf.Size * 0.35f;
                    if (math.lengthsq(root + leaf.Position - impactMetres) > radius * radius)
                        continue;

                    hitLeaves++;
                    int parent = skeleton.LeafParents != null
                              && leafIndex < skeleton.LeafParents.Length
                        ? skeleton.LeafParents[leafIndex] : -1;
                    if ((uint)parent >= (uint)skeleton.Branches.Count) continue;

                    TreeBranchSegment parentBranch = skeleton.Branches[parent];
                    if (IsLowerTrunk(in parentBranch, skeleton.Height))
                    {
                        float distanceSq = DistanceToSegmentSq(
                            impactMetres, root + parentBranch.Start, root + parentBranch.End);
                        if (distanceSq < severBranchSq)
                        {
                            severBranchSq = distanceSq;
                            severBranch = parent;
                        }
                    }
                    else if (!cutCandidates.Contains(parent))
                    {
                        cutCandidates.Add(parent);
                    }
                }

                if (severBranch < 0 && cutCandidates.Count == 0 && hitLeaves == 0
                    && nearestBranch >= 0 && nearestBranchSq <= fallbackRadius * fallbackRadius)
                {
                    TreeBranchSegment nearest = skeleton.Branches[nearestBranch];
                    if (IsLowerTrunk(in nearest, skeleton.Height))
                        severBranch = nearestBranch;
                    else
                        cutCandidates.Add(nearestBranch);
                }

                if (severBranch < 0 && cutCandidates.Count > 0)
                    PromoteCutCandidatesToVisibleLimbs(skeleton, cutCandidates);

                bool severed = severBranch >= 0;
                bool removedAny = false;
                if (severed)
                {
                    // A trunk sever is one connected semantic cut. The standing renderer keeps the
                    // stump while the branch-cut presenter receives the entire upper subtree.
                    removedAny = TreeWorldState.RemoveBranch(
                        treeIndex, severBranch, impactMetres, impulse);
                }
                else
                {
                    // Remove only top-most candidates. If a parent is cut, descendants are derived
                    // from topology and should not generate duplicate detached debris events.
                    for (int i = 0; i < cutCandidates.Count; i++)
                    {
                        int candidate = cutCandidates[i];
                        bool coveredByAnotherCandidate = false;
                        int parent = skeleton.BranchParents != null
                                  && candidate < skeleton.BranchParents.Length
                            ? skeleton.BranchParents[candidate] : -1;
                        while (parent >= 0)
                        {
                            if (cutCandidates.Contains(parent))
                            {
                                coveredByAnotherCandidate = true;
                                break;
                            }
                            parent = skeleton.BranchParents != null
                                  && parent < skeleton.BranchParents.Length
                                ? skeleton.BranchParents[parent] : -1;
                        }
                        if (coveredByAnotherCandidate) continue;
                        removedAny |= TreeWorldState.RemoveBranch(
                            treeIndex, candidate, impactMetres, impulse);
                    }
                }

                float foliageHealth =
                    treeIndex < damage.Count ? damage[treeIndex].FoliageHealth : 1f;
                if (hitLeaves > 0 && skeleton.Leaves.Count > 0)
                {
                    float fraction = hitLeaves / (float)skeleton.Leaves.Count;
                    float loss = math.clamp(fraction * 2.5f, 0.06f, 0.45f);
                    foliageHealth = math.max(0f, foliageHealth - loss);
                }

                if (severed || hitLeaves > 0 || removedAny)
                    TreeWorldState.SetDamage(treeIndex, foliageHealth, severed,
                                             impactMetres, impulse);
            }
        }

        public static ProceduralTreeSkeleton SkeletonFor(int treeIndex)
        {
            IReadOnlyList<TreeInstance> instances = TreeWorldState.Instances;
            if ((uint)treeIndex >= (uint)instances.Count) return null;
            EnsureCache(instances);
            return treeIndex < s_Cache.Count ? s_Cache[treeIndex].Skeleton : null;
        }

        private static void PromoteCutCandidatesToVisibleLimbs(
            ProceduralTreeSkeleton skeleton, List<int> cutCandidates)
        {
            var promoted = new List<int>(cutCandidates.Count);
            for (int i = 0; i < cutCandidates.Count; i++)
            {
                int candidate = PromoteCutCandidateToVisibleLimb(skeleton, cutCandidates[i]);
                if (!promoted.Contains(candidate)) promoted.Add(candidate);
            }

            cutCandidates.Clear();
            cutCandidates.AddRange(promoted);
        }

        private static int PromoteCutCandidateToVisibleLimb(
            ProceduralTreeSkeleton skeleton, int candidate)
        {
            if ((uint)candidate >= (uint)skeleton.Branches.Count) return candidate;
            if (skeleton.Branches[candidate].Level <= 0) return candidate;

            int promoted = candidate;
            while (CountConnectedSubtreeBranches(skeleton, promoted)
                   < MinimumVisibleDetachedSegments)
            {
                int parent = skeleton.BranchParents != null
                          && promoted < skeleton.BranchParents.Length
                    ? skeleton.BranchParents[promoted] : -1;
                if ((uint)parent >= (uint)skeleton.Branches.Count) break;
                if (skeleton.Branches[parent].Level <= 0) break;
                promoted = parent;
            }
            return promoted;
        }

        private static int CountConnectedSubtreeBranches(
            ProceduralTreeSkeleton skeleton, int branchIndex)
        {
            if ((uint)branchIndex >= (uint)skeleton.Branches.Count) return 0;
            int[] parents = skeleton.BranchParents;
            if (parents == null || parents.Length != skeleton.Branches.Count) return 1;

            int count = 0;
            for (int i = branchIndex; i < skeleton.Branches.Count; i++)
            {
                int current = i;
                while (current >= 0)
                {
                    if (current == branchIndex)
                    {
                        count++;
                        break;
                    }
                    if ((uint)current >= (uint)parents.Length) break;
                    int parent = parents[current];
                    if (parent == current) break;
                    current = parent;
                }
            }
            return count;
        }

        private static void EnsureCache(IReadOnlyList<TreeInstance> instances)
        {
            int version = TreeWorldState.Version;
            if (s_CachedRegistryVersion == version && s_Cache.Count == instances.Count) return;

            s_Cache.Clear();
            for (int i = 0; i < instances.Count; i++)
            {
                TreeInstance instance = instances[i];
                ProceduralTreeSkeleton skeleton = ProceduralTreeSkeletonBuilder.Generate(in instance);
                CalculateBounds(in instance, skeleton, out float3 min, out float3 max);
                s_Cache.Add(new CachedTree
                {
                    Skeleton = skeleton,
                    BoundsMin = min,
                    BoundsMax = max,
                });
            }
            s_CachedRegistryVersion = version;
        }

        private static void CalculateBounds(in TreeInstance instance,
                                            ProceduralTreeSkeleton skeleton,
                                            out float3 min, out float3 max)
        {
            float3 root = instance.PositionMetres;
            min = root;
            max = root;

            for (int i = 0; i < skeleton.Branches.Count; i++)
            {
                TreeBranchSegment branch = skeleton.Branches[i];
                float radius = math.max(branch.RadiusStart, branch.RadiusEnd);
                float3 r = new(radius);
                min = math.min(min, root + math.min(branch.Start, branch.End) - r);
                max = math.max(max, root + math.max(branch.Start, branch.End) + r);
            }

            for (int i = 0; i < skeleton.Leaves.Count; i++)
            {
                TreeLeafAnchor leaf = skeleton.Leaves[i];
                float3 r = new(math.max(0.05f, leaf.Size));
                min = math.min(min, root + leaf.Position - r);
                max = math.max(max, root + leaf.Position + r);
            }
        }

        private static bool BoundsOverlap(float3 aMin, float3 aMax,
                                          float3 bMin, float3 bMax) =>
            aMin.x <= bMax.x && aMax.x >= bMin.x
            && aMin.y <= bMax.y && aMax.y >= bMin.y
            && aMin.z <= bMax.z && aMax.z >= bMin.z;

        private static bool SphereIntersectsBounds(float3 centre, float radius,
                                                   float3 min, float3 max)
        {
            float3 nearest = math.clamp(centre, min, max);
            return math.lengthsq(centre - nearest) <= radius * radius;
        }

        private static bool IsLowerTrunk(in TreeBranchSegment branch, float treeHeight)
        {
            float midpointY = (branch.Start.y + branch.End.y) * 0.5f;
            return branch.Level == 0 && midpointY < treeHeight * 0.48f;
        }

        private static float DistanceToSegmentSq(float3 point, float3 a, float3 b)
        {
            float3 ab = b - a;
            float denominator = math.lengthsq(ab);
            if (denominator <= 1e-10f) return math.lengthsq(point - a);
            float t = math.saturate(math.dot(point - a, ab) / denominator);
            return math.lengthsq(point - (a + ab * t));
        }

        private static float PointSegmentDistanceSq(float3 point, float3 a, float3 b,
                                                    out float segmentT)
        {
            float3 ab = b - a;
            float denominator = math.lengthsq(ab);
            if (denominator <= 1e-10f)
            {
                segmentT = 0f;
                return math.lengthsq(point - a);
            }
            segmentT = math.saturate(math.dot(point - a, ab) / denominator);
            return math.lengthsq(point - (a + ab * segmentT));
        }

        private static float SegmentSegmentDistanceSq(
            float3 p1, float3 q1, float3 p2, float3 q2, out float projectileT)
        {
            float3 d1 = q1 - p1;
            float3 d2 = q2 - p2;
            float3 r = p1 - p2;
            float a = math.dot(d1, d1);
            float e = math.dot(d2, d2);
            float f = math.dot(d2, r);
            float s;
            float t;
            const float epsilon = 1e-8f;

            if (a <= epsilon && e <= epsilon)
            {
                s = 0f;
                t = 0f;
            }
            else if (a <= epsilon)
            {
                s = 0f;
                t = math.saturate(f / e);
            }
            else
            {
                float c = math.dot(d1, r);
                if (e <= epsilon)
                {
                    t = 0f;
                    s = math.saturate(-c / a);
                }
                else
                {
                    float b = math.dot(d1, d2);
                    float denominator = a * e - b * b;
                    s = math.abs(denominator) > epsilon
                        ? math.saturate((b * f - c * e) / denominator)
                        : 0f;
                    t = (b * s + f) / e;
                    if (t < 0f)
                    {
                        t = 0f;
                        s = math.saturate(-c / a);
                    }
                    else if (t > 1f)
                    {
                        t = 1f;
                        s = math.saturate((b - c) / a);
                    }
                }
            }

            projectileT = s;
            float3 closest1 = p1 + d1 * s;
            float3 closest2 = p2 + d2 * t;
            return math.lengthsq(closest1 - closest2);
        }
    }
}
