using System.Collections.Generic;
using Unity.Mathematics;
using VoxelEngine.Rendering.Vegetation;
using TreeInstance = VoxelEngine.Core.Vegetation.TreeInstance;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Migration bridge between the showcase's voxel destruction command and semantic trees.
    ///
    /// The old voxel tree remains the collision/debris proxy for now, but it no longer decides
    /// whether a new procedural branch exists. The exact blast centre/radius is tested against the
    /// deterministic procedural skeleton itself, so a branch hit by the same gameplay edit is cut
    /// immediately and its connected descendants disappear from every visual LOD.
    /// </summary>
    public static class ProceduralTreeDamageBridge
    {
        private const float VoxelSize = 0.1f;

        /// <summary>
        /// Applies the same radial edit used by <see cref="ShowcaseWorld.Explode"/> to semantic
        /// vegetation. The operation is intentionally cheap enough to run once per impact: the
        /// showcase has only tens of trees and at most a few hundred branch segments per tree.
        /// </summary>
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

                // Height is also a conservative crown-radius bound for every current species.
                // This rejects nearly every tree before the branch/leaf loops on a normal impact.
                float broadRadius = skeleton.Height + blastRadius + 1f;
                if (math.lengthsq(root - impact) > broadRadius * broadRadius) continue;

                bool severed = false;
                int hitLeaves = 0;

                for (int branchIndex = 0; branchIndex < skeleton.Branches.Count; branchIndex++)
                {
                    ProceduralTreeMeshBuilder.BranchSegment branch = skeleton.Branches[branchIndex];
                    float branchRadius = math.max(branch.RadiusStart, branch.RadiusEnd);
                    float radius = blastRadius + branchRadius;
                    if (DistanceToSegmentSq(impact, root + branch.Start, root + branch.End)
                        > radius * radius)
                        continue;

                    // A low trunk hit should detach/fall the tree rather than deleting its crown
                    // in place. Upper trunk sections and side branches are ordinary cut points.
                    float midpointY = (branch.Start.y + branch.End.y) * 0.5f;
                    if (branch.Level == 0 && midpointY < skeleton.Height * 0.48f)
                    {
                        severed = true;
                        continue;
                    }

                    ProceduralTreeRegistry.RemoveBranch(treeIndex, branchIndex);
                }

                // Leaf-only impacts still need visible feedback even when the blast misses a woody
                // segment. The current leaf shader represents aggregate crown loss; branch-linked
                // leaves disappear geometrically whenever their parent branch is cut.
                for (int leafIndex = 0; leafIndex < skeleton.Leaves.Count; leafIndex++)
                {
                    ProceduralTreeMeshBuilder.LeafAnchor leaf = skeleton.Leaves[leafIndex];
                    float radius = blastRadius + leaf.Size * 0.35f;
                    if (math.lengthsq(root + leaf.Position - impact) <= radius * radius)
                        hitLeaves++;
                }

                float foliageHealth = treeIndex < damage.Count ? damage[treeIndex].FoliageHealth : 1f;
                if (hitLeaves > 0 && skeleton.Leaves.Count > 0)
                {
                    float fraction = hitLeaves / (float)skeleton.Leaves.Count;
                    // Small blasts must be noticeable, but one grazing hit must not erase a crown.
                    float loss = math.clamp(fraction * 2.5f, 0.06f, 0.45f);
                    foliageHealth = math.max(0f, foliageHealth - loss);
                }

                if (severed || hitLeaves > 0)
                    ProceduralTreeRegistry.SetDamage(treeIndex, foliageHealth, severed);
            }
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
