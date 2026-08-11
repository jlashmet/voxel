using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Rendering.Vegetation;
using TreeInstance = VoxelEngine.Core.Vegetation.TreeInstance;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Semantic collision/damage bridge for procedural trees. Tree collision comes from the same
    /// deterministic branch skeleton that produces render geometry; no voxel trunk/crown proxy is
    /// required for hits or destruction.
    /// </summary>
    public static class ProceduralTreeDamageBridge
    {
        private sealed class CachedTree
        {
            public ProceduralTreeMeshBuilder.TreeSkeleton Skeleton;
            public float3 BoundsMin;
            public float3 BoundsMax;
        }

        private const float VoxelSize = 0.1f;
        private static readonly List<CachedTree> s_Cache = new();
        private static int s_CachedRegistryVersion = int.MinValue;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlaySession()
        {
            s_Cache.Clear();
            s_CachedRegistryVersion = int.MinValue;
        }

        /// <summary>
        /// Sweeps a tornado segment against live procedural tree branches and leaf anchors.
        /// Returns the closest semantic impact along the projectile segment.
        /// </summary>
        public static bool TrySweepImpact(float3 from, float3 to, float sweepRadius,
                                          out float3 hitMetres, out int treeIndex)
        {
            IReadOnlyList<TreeInstance> instances = ProceduralTreeRegistry.Instances;
            IReadOnlyList<ProceduralTreeRegistry.TreeDamageState> damage =
                ProceduralTreeRegistry.Damage;
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
                ProceduralTreeMeshBuilder.TreeSkeleton skeleton = cached.Skeleton;
                IReadOnlyCollection<int> directCuts = ProceduralTreeRegistry.RemovedBranches(i);
                float3 root = instance.PositionMetres;

                for (int branchIndex = 0; branchIndex < skeleton.Branches.Count; branchIndex++)
                {
                    if (IsBranchRemoved(skeleton, directCuts, branchIndex)) continue;

                    ProceduralTreeMeshBuilder.BranchSegment branch = skeleton.Branches[branchIndex];
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
                    if (parent >= 0 && IsBranchRemoved(skeleton, directCuts, parent)) continue;

                    ProceduralTreeMeshBuilder.LeafAnchor leaf = skeleton.Leaves[leafIndex];
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

        public static void ApplyExplosion(int3 centreVoxel, int radiusVoxels)
        {
            IReadOnlyList<TreeInstance> instances = ProceduralTreeRegistry.Instances;
            IReadOnlyList<ProceduralTreeRegistry.TreeDamageState> damage =
                ProceduralTreeRegistry.Damage;
            if (instances.Count == 0) return;

            EnsureCache(instances);

            float3 impact = (float3)centreVoxel * VoxelSize;
            float blastRadius = math.max(VoxelSize, radiusVoxels * VoxelSize);

            for (int treeIndex = 0; treeIndex < instances.Count; treeIndex++)
            {
                if ((uint)treeIndex >= (uint)s_Cache.Count) break;

                TreeInstance instance = instances[treeIndex];
                CachedTree cached = s_Cache[treeIndex];
                ProceduralTreeMeshBuilder.TreeSkeleton skeleton = cached.Skeleton;
                float3 root = instance.PositionMetres;

                float fallbackRadius = math.max(
                    3.0f, math.max(blastRadius * 2.5f, skeleton.Height * 0.35f));
                if (!SphereIntersectsBounds(impact, fallbackRadius,
                                            cached.BoundsMin, cached.BoundsMax))
                    continue;

                bool severed = false;
                bool removedAny = false;
                int hitLeaves = 0;
                int nearestBranch = -1;
                float nearestBranchSq = float.PositiveInfinity;

                for (int branchIndex = 0; branchIndex < skeleton.Branches.Count; branchIndex++)
                {
                    ProceduralTreeMeshBuilder.BranchSegment branch = skeleton.Branches[branchIndex];
                    float distanceSq =
                        DistanceToSegmentSq(impact, root + branch.Start, root + branch.End);

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
                        severed = true;
                        continue;
                    }

                    removedAny |= ProceduralTreeRegistry.RemoveBranch(treeIndex, branchIndex);
                }

                for (int leafIndex = 0; leafIndex < skeleton.Leaves.Count; leafIndex++)
                {
                    ProceduralTreeMeshBuilder.LeafAnchor leaf = skeleton.Leaves[leafIndex];
                    float radius = blastRadius + leaf.Size * 0.35f;
                    if (math.lengthsq(root + leaf.Position - impact) > radius * radius) continue;

                    hitLeaves++;

                    int parent = skeleton.LeafParents != null
                              && leafIndex < skeleton.LeafParents.Length
                        ? skeleton.LeafParents[leafIndex] : -1;
                    if ((uint)parent >= (uint)skeleton.Branches.Count) continue;

                    ProceduralTreeMeshBuilder.BranchSegment parentBranch = skeleton.Branches[parent];
                    if (IsLowerTrunk(in parentBranch, skeleton.Height))
                        severed = true;
                    else
                        removedAny |= ProceduralTreeRegistry.RemoveBranch(treeIndex, parent);
                }

                // Keep a small nearest-limb reconciliation radius for explosions centred between
                // branch tubes. This is semantic-to-semantic tolerance, not a legacy voxel proxy.
                if (!removedAny && !severed && hitLeaves == 0 && nearestBranch >= 0
                    && nearestBranchSq <= fallbackRadius * fallbackRadius)
                {
                    ProceduralTreeMeshBuilder.BranchSegment nearest =
                        skeleton.Branches[nearestBranch];
                    if (IsLowerTrunk(in nearest, skeleton.Height))
                        severed = true;
                    else
                        removedAny = ProceduralTreeRegistry.RemoveBranch(treeIndex, nearestBranch);
                }

                float foliageHealth =
                    treeIndex < damage.Count ? damage[treeIndex].FoliageHealth : 1f;
                if (hitLeaves > 0 && skeleton.Leaves.Count > 0)
                {
                    float fraction = hitLeaves / (float)skeleton.Leaves.Count;
                    float loss = math.clamp(fraction * 2.5f, 0.06f, 0.45f);
                    foliageHealth = math.max(0f, foliageHealth - loss);
                }

                if (severed || hitLeaves > 0)
                    ProceduralTreeRegistry.SetDamage(treeIndex, foliageHealth, severed);
            }
        }

        private static void EnsureCache(IReadOnlyList<TreeInstance> instances)
        {
            int version = ProceduralTreeRegistry.Version;
            if (s_CachedRegistryVersion == version && s_Cache.Count == instances.Count) return;

            s_Cache.Clear();
            for (int i = 0; i < instances.Count; i++)
            {
                TreeInstance instance = instances[i];
                ProceduralTreeMeshBuilder.TreeSkeleton skeleton =
                    ProceduralTreeMeshBuilder.GenerateSkeleton(in instance);
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
                                            ProceduralTreeMeshBuilder.TreeSkeleton skeleton,
                                            out float3 min, out float3 max)
        {
            float3 root = instance.PositionMetres;
            min = root;
            max = root;

            for (int i = 0; i < skeleton.Branches.Count; i++)
            {
                ProceduralTreeMeshBuilder.BranchSegment branch = skeleton.Branches[i];
                float radius = math.max(branch.RadiusStart, branch.RadiusEnd);
                float3 r = new(radius);
                min = math.min(min, root + math.min(branch.Start, branch.End) - r);
                max = math.max(max, root + math.max(branch.Start, branch.End) + r);
            }

            for (int i = 0; i < skeleton.Leaves.Count; i++)
            {
                ProceduralTreeMeshBuilder.LeafAnchor leaf = skeleton.Leaves[i];
                float3 r = new(math.max(0.05f, leaf.Size));
                min = math.min(min, root + leaf.Position - r);
                max = math.max(max, root + leaf.Position + r);
            }
        }

        private static bool IsBranchRemoved(ProceduralTreeMeshBuilder.TreeSkeleton skeleton,
                                            IReadOnlyCollection<int> directCuts,
                                            int branchIndex)
        {
            if (directCuts == null || directCuts.Count == 0) return false;
            int current = branchIndex;
            while (current >= 0)
            {
                if (directCuts.Contains(current)) return true;
                if (skeleton.BranchParents == null || current >= skeleton.BranchParents.Length)
                    break;
                int parent = skeleton.BranchParents[current];
                if (parent == current) break;
                current = parent;
            }
            return false;
        }

        private static bool BoundsOverlap(float3 aMin, float3 aMax, float3 bMin, float3 bMax) =>
            aMin.x <= bMax.x && aMax.x >= bMin.x
            && aMin.y <= bMax.y && aMax.y >= bMin.y
            && aMin.z <= bMax.z && aMax.z >= bMin.z;

        private static bool SphereIntersectsBounds(float3 centre, float radius,
                                                   float3 min, float3 max)
        {
            float3 nearest = math.clamp(centre, min, max);
            return math.lengthsq(centre - nearest) <= radius * radius;
        }

        private static bool IsLowerTrunk(
            in ProceduralTreeMeshBuilder.BranchSegment branch, float treeHeight)
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
