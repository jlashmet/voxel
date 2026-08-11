using System.Collections.Generic;
using Unity.Mathematics;
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
    /// </summary>
    public static class ProceduralTreeDamageBridge
    {
        private const float VoxelSize = 0.1f;

        public static void ApplyExplosion(int3 centreVoxel, int radiusVoxels)
        {
            IReadOnlyList<TreeInstance> instances = ProceduralTreeRegistry.Instances;
            IReadOnlyList<ProceduralTreeRegistry.TreeDamageState> damage =
                ProceduralTreeRegistry.Damage;
            if (instances.Count == 0) return;

            float3 impact = (float3)centreVoxel * VoxelSize;
            float blastRadius = math.max(VoxelSize, radiusVoxels * VoxelSize);

            for (int treeIndex = 0; treeIndex < instances.Count; treeIndex++)
            {
                TreeInstance instance = instances[treeIndex];
                ProceduralTreeMeshBuilder.TreeSkeleton skeleton =
                    ProceduralTreeMeshBuilder.GenerateSkeleton(in instance);
                float3 root = instance.PositionMetres;

                float broadRadius = skeleton.Height + blastRadius + 1f;
                if (math.lengthsq(root - impact) > broadRadius * broadRadius) continue;

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

                    // Leaf damage has to change topology too. Cutting the owning twig removes the
                    // leaf cluster from the rebuilt mesh instead of relying on a subtle whole-crown
                    // shader threshold to communicate the hit.
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

                // Migration-only reconciliation. The old voxel scaffold and new semantic tree are
                // deliberately different shapes, so the projectile can visibly pass through a new
                // limb and hit an old proxy voxel a few metres behind it. If that proxy impact is
                // still close to this tree, map it to the nearest procedural limb rather than doing
                // nothing. Remove this once tornado collision queries semantic vegetation directly.
                if (!removedAny && !severed && hitLeaves == 0 && nearestBranch >= 0)
                {
                    float fallbackRadius = math.max(
                        3.0f, math.max(blastRadius * 2.5f, skeleton.Height * 0.35f));
                    if (nearestBranchSq <= fallbackRadius * fallbackRadius)
                    {
                        ProceduralTreeMeshBuilder.BranchSegment nearest =
                            skeleton.Branches[nearestBranch];
                        if (IsLowerTrunk(in nearest, skeleton.Height))
                            severed = true;
                        else
                            removedAny =
                                ProceduralTreeRegistry.RemoveBranch(treeIndex, nearestBranch);
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

                if (severed || hitLeaves > 0)
                    ProceduralTreeRegistry.SetDamage(treeIndex, foliageHealth, severed);
            }
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
