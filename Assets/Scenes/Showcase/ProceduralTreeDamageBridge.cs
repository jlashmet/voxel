using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Rendering.Vegetation;
using TreeInstance = VoxelEngine.Core.Vegetation.TreeInstance;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Migration bridge between the showcase's voxel destruction command and semantic trees.
    ///
    /// Legacy voxel trees are still the collision/debris proxy, but their scaffold does not match
    /// the new procedural skeleton. Exact semantic intersections are preferred; when the proxy is
    /// what was hit, a bounded nearest-limb fallback maps that edit onto the same visible tree.
    /// Deterministic tree skeletons and bounds are cached once per registry snapshot so repeated
    /// contact never regenerates vegetation geometry on the gameplay thread.
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

                // The fallback is intentionally wider than the exact blast because the hidden old
                // voxel proxy can sit a few metres away from the new procedural limb. Using the
                // cached AABB still rejects the rest of the grove before branch/leaf tests.
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

                // Migration-only reconciliation. The old voxel scaffold and semantic tree are
                // deliberately different shapes. A nearby proxy hit therefore maps to the nearest
                // visible procedural limb if no exact branch/leaf intersection was found.
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
    }
}
