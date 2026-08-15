using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Random = Unity.Mathematics.Random;
using VoxelEngine.Vegetation.Api;
using TreeInstance = VoxelEngine.Vegetation.Api.TreeInstance;

namespace VoxelEngine.Vegetation.Runtime
{
    /// <summary>
    /// Render-independent collision and damage service for semantic trees. Gameplay supplies metre-
    /// space sweeps/blasts and the service mutates only <see cref="TreeWorldState"/>. Presentation
    /// reacts through typed tree-state events.
    ///
    /// Conservative bounds live in a lightweight spatial index. Exact procedural skeletons are
    /// generated only for nearby candidates and retained in a bounded cache. A severed tree remains
    /// queryable: Severed means the crown has disconnected, not that the remaining stump is immune.
    /// </summary>
    public static class ProceduralTreeDamageService
    {
        private const int MinimumVisibleDetachedSegments = 4;
        private const float BaseImpactTransferRadiusMetres = 0.65f;
        private const float BroadphaseCellSizeMetres = 32f;
        private const int MaxResidentSkeletons = 64;

        private sealed class CachedTree
        {
            public TreeSkeletonSnapshot Skeleton;
            public float3 BoundsMin;
            public float3 BoundsMax;
            public float HeightMetres;
            public bool ExactBounds;
            public ulong LastUse;
        }

        private static readonly List<CachedTree> s_Cache = new();
        private static readonly Dictionary<Vector2Int, List<int>> s_BroadphaseGrid = new();
        private static readonly List<int> s_Candidates = new(128);
        private static readonly List<int> s_CutCandidates = new(16);
        private static readonly List<int> s_PromotedCandidates = new(16);
        private static int[] s_CandidateMarks = System.Array.Empty<int>();
        private static int s_CandidateStamp;
        private static int s_CachedRegistryVersion = int.MinValue;
        private static int s_ResidentSkeletonCount;
        private static ulong s_UseCounter;
        private static float s_MaxFallbackRadius;

        public static int ResidentSkeletonCount => s_ResidentSkeletonCount;
        public static int SkeletonCacheCapacity => MaxResidentSkeletons;
        public static int LastBroadphaseCandidateCount { get; private set; }
        public static int LastQuerySkeletonBuildCount { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlaySession()
        {
            s_Cache.Clear();
            s_BroadphaseGrid.Clear();
            s_Candidates.Clear();
            s_CutCandidates.Clear();
            s_PromotedCandidates.Clear();
            s_CandidateMarks = System.Array.Empty<int>();
            s_CandidateStamp = 0;
            s_CachedRegistryVersion = int.MinValue;
            s_ResidentSkeletonCount = 0;
            s_UseCounter = 0;
            s_MaxFallbackRadius = 0f;
            LastBroadphaseCandidateCount = 0;
            LastQuerySkeletonBuildCount = 0;
        }

        public static bool TrySweepImpact(float3 from, float3 to, float sweepRadius,
                                          out float3 hitMetres, out int treeIndex)
        {
            IReadOnlyList<TreeInstance> instances = TreeWorldState.Instances;
            hitMetres = default;
            treeIndex = -1;
            if (instances.Count == 0) return false;

            EnsureCache(instances);
            LastQuerySkeletonBuildCount = 0;

            float3 segmentMin = math.min(from, to) - sweepRadius;
            float3 segmentMax = math.max(from, to) + sweepRadius;
            CollectCandidates(segmentMin - sweepRadius, segmentMax + sweepRadius);

            float bestT = float.PositiveInfinity;
            for (int candidateIndex = 0; candidateIndex < s_Candidates.Count; candidateIndex++)
            {
                int i = s_Candidates[candidateIndex];
                if ((uint)i >= (uint)instances.Count || (uint)i >= (uint)s_Cache.Count) continue;

                CachedTree cached = s_Cache[i];
                if (!BoundsOverlap(segmentMin, segmentMax,
                                   cached.BoundsMin - sweepRadius,
                                   cached.BoundsMax + sweepRadius))
                    continue;

                TreeInstance instance = instances[i];
                TreeSkeletonSnapshot skeleton = EnsureSkeleton(i, in instance);
                if (skeleton == null) continue;
                if (!BoundsOverlap(segmentMin, segmentMax,
                                   cached.BoundsMin - sweepRadius,
                                   cached.BoundsMax + sweepRadius))
                    continue;

                IReadOnlyCollection<int> directCuts = TreeWorldState.RemovedBranches(i);
                float3 root = instance.PositionMetres;

                for (int branchIndex = 0; branchIndex < skeleton.Branches.Count; branchIndex++)
                {
                    if (TreeSkeletonTopology.IsBranchRemoved(
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
                              && leafIndex < skeleton.LeafParents.Count
                        ? skeleton.LeafParents[leafIndex] : -1;
                    if (parent >= 0 && TreeSkeletonTopology.IsBranchRemoved(
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
            IReadOnlyList<TreeDamageState> damage = TreeWorldState.Damage;
            if (instances.Count == 0) return;

            EnsureCache(instances);
            LastQuerySkeletonBuildCount = 0;
            float blastRadius = math.max(0.1f, blastRadiusMetres);
            float broadphaseRadius = math.max(
                s_MaxFallbackRadius, math.max(3.0f, blastRadius * 2.5f));
            float3 broadphaseExtent = new(broadphaseRadius);
            CollectCandidates(impactMetres - broadphaseExtent, impactMetres + broadphaseExtent);

            for (int candidateIndex = 0; candidateIndex < s_Candidates.Count; candidateIndex++)
            {
                int treeIndex = s_Candidates[candidateIndex];
                if ((uint)treeIndex >= (uint)instances.Count
                    || (uint)treeIndex >= (uint)s_Cache.Count)
                    continue;

                TreeInstance instance = instances[treeIndex];
                CachedTree cached = s_Cache[treeIndex];
                float cheapFallbackRadius = math.max(
                    3.0f, math.max(blastRadius * 2.5f, cached.HeightMetres * 0.35f));
                if (!SphereIntersectsBounds(impactMetres, cheapFallbackRadius,
                                            cached.BoundsMin, cached.BoundsMax))
                    continue;

                TreeSkeletonSnapshot skeleton = EnsureSkeleton(treeIndex, in instance);
                if (skeleton == null) continue;
                float3 root = instance.PositionMetres;
                float fallbackRadius = math.max(
                    3.0f, math.max(blastRadius * 2.5f, skeleton.Height * 0.35f));
                if (!SphereIntersectsBounds(impactMetres, fallbackRadius,
                                            cached.BoundsMin, cached.BoundsMax))
                    continue;

                s_CutCandidates.Clear();
                int severBranch = -1;
                float severBranchSq = float.PositiveInfinity;
                int nearestLowerTrunk = -1;
                float nearestLowerTrunkSq = float.PositiveInfinity;
                int hitLeaves = 0;
                int nearestBranch = -1;
                float nearestBranchSq = float.PositiveInfinity;
                IReadOnlyCollection<int> existingCuts = TreeWorldState.RemovedBranches(treeIndex);

                for (int branchIndex = 0; branchIndex < skeleton.Branches.Count; branchIndex++)
                {
                    if (TreeSkeletonTopology.IsBranchRemoved(
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

                    bool lowerTrunk = IsLowerTrunk(in branch, skeleton.Height);
                    if (lowerTrunk && distanceSq < nearestLowerTrunkSq)
                    {
                        nearestLowerTrunkSq = distanceSq;
                        nearestLowerTrunk = branchIndex;
                    }

                    float branchRadius = math.max(branch.RadiusStart, branch.RadiusEnd);
                    float radius = blastRadius + branchRadius;
                    if (distanceSq > radius * radius) continue;

                    if (lowerTrunk)
                    {
                        if (distanceSq < severBranchSq)
                        {
                            severBranchSq = distanceSq;
                            severBranch = branchIndex;
                        }
                    }
                    else if (!s_CutCandidates.Contains(branchIndex))
                    {
                        s_CutCandidates.Add(branchIndex);
                    }
                }

                for (int leafIndex = 0; leafIndex < skeleton.Leaves.Count; leafIndex++)
                {
                    int parent = skeleton.LeafParents != null
                              && leafIndex < skeleton.LeafParents.Count
                        ? skeleton.LeafParents[leafIndex] : -1;
                    if (parent >= 0 && TreeSkeletonTopology.IsBranchRemoved(
                            skeleton, existingCuts, parent))
                        continue;

                    TreeLeafAnchor leaf = skeleton.Leaves[leafIndex];
                    float radius = blastRadius + leaf.Size * 0.35f;
                    if (math.lengthsq(root + leaf.Position - impactMetres) > radius * radius)
                        continue;

                    hitLeaves++;
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
                    else if (!s_CutCandidates.Contains(parent))
                    {
                        s_CutCandidates.Add(parent);
                    }
                }

                // Trees can be partially embedded in terrain. Transfer a very-near low impact to
                // the lower trunk when the projectile clips the voxel lip in front of visible wood.
                if (severBranch < 0 && nearestLowerTrunk >= 0)
                {
                    float transferRadius = math.max(
                        BaseImpactTransferRadiusMetres, blastRadius + 0.25f);
                    float localImpactY = impactMetres.y - root.y;
                    if (localImpactY <= skeleton.Height * 0.52f
                        && nearestLowerTrunkSq <= transferRadius * transferRadius)
                    {
                        severBranch = nearestLowerTrunk;
                    }
                }

                if (severBranch < 0 && s_CutCandidates.Count == 0 && hitLeaves == 0
                    && nearestBranch >= 0 && nearestBranchSq <= fallbackRadius * fallbackRadius)
                {
                    TreeBranchSegment nearest = skeleton.Branches[nearestBranch];
                    if (IsLowerTrunk(in nearest, skeleton.Height))
                        severBranch = nearestBranch;
                    else
                        s_CutCandidates.Add(nearestBranch);
                }

                if (severBranch < 0 && s_CutCandidates.Count > 0)
                    PromoteCutCandidatesToVisibleLimbs(skeleton, s_CutCandidates);

                bool trunkCut = severBranch >= 0;
                bool removedAny = false;
                if (trunkCut)
                {
                    // A trunk cut removes that connected upper subtree. If this tree was already
                    // severed, the still-standing lower sections remain valid targets; eventually a
                    // root-most cut removes every remaining branch and leaves no immortal stump.
                    removedAny = TreeWorldState.RemoveBranch(
                        treeIndex, severBranch, impactMetres, impulse);
                }
                else
                {
                    for (int i = 0; i < s_CutCandidates.Count; i++)
                    {
                        int candidate = s_CutCandidates[i];
                        bool coveredByAnotherCandidate = false;
                        int parent = skeleton.BranchParents != null
                                  && candidate < skeleton.BranchParents.Count
                            ? skeleton.BranchParents[candidate] : -1;
                        while (parent >= 0)
                        {
                            if (s_CutCandidates.Contains(parent))
                            {
                                coveredByAnotherCandidate = true;
                                break;
                            }
                            parent = skeleton.BranchParents != null
                                  && parent < skeleton.BranchParents.Count
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

                if (trunkCut || hitLeaves > 0 || removedAny)
                {
                    bool alreadySevered = treeIndex < damage.Count && damage[treeIndex].Severed;
                    TreeWorldState.SetDamage(treeIndex, foliageHealth,
                                             alreadySevered || trunkCut,
                                             impactMetres, impulse, severBranch);
                }
            }
        }

        public static TreeSkeletonSnapshot SkeletonFor(int treeIndex)
        {
            IReadOnlyList<TreeInstance> instances = TreeWorldState.Instances;
            if ((uint)treeIndex >= (uint)instances.Count) return null;
            EnsureCache(instances);
            TreeInstance instance = instances[treeIndex];
            return EnsureSkeleton(treeIndex, in instance);
        }

        private static void PromoteCutCandidatesToVisibleLimbs(
            TreeSkeletonSnapshot skeleton, List<int> cutCandidates)
        {
            s_PromotedCandidates.Clear();
            for (int i = 0; i < cutCandidates.Count; i++)
            {
                int candidate = PromoteCutCandidateToVisibleLimb(skeleton, cutCandidates[i]);
                if (!s_PromotedCandidates.Contains(candidate))
                    s_PromotedCandidates.Add(candidate);
            }
            cutCandidates.Clear();
            cutCandidates.AddRange(s_PromotedCandidates);
        }

        private static int PromoteCutCandidateToVisibleLimb(
            TreeSkeletonSnapshot skeleton, int candidate)
        {
            if ((uint)candidate >= (uint)skeleton.Branches.Count) return candidate;
            if (skeleton.Branches[candidate].Level <= 0) return candidate;

            int promoted = candidate;
            while (CountConnectedSubtreeBranches(skeleton, promoted)
                   < MinimumVisibleDetachedSegments)
            {
                int parent = skeleton.BranchParents != null
                          && promoted < skeleton.BranchParents.Count
                    ? skeleton.BranchParents[promoted] : -1;
                if ((uint)parent >= (uint)skeleton.Branches.Count) break;
                if (skeleton.Branches[parent].Level <= 0) break;
                promoted = parent;
            }
            return promoted;
        }

        private static int CountConnectedSubtreeBranches(
            TreeSkeletonSnapshot skeleton, int branchIndex)
        {
            if ((uint)branchIndex >= (uint)skeleton.Branches.Count) return 0;
            IReadOnlyList<int> parents = skeleton.BranchParents;
            if (parents == null || parents.Count != skeleton.Branches.Count) return 1;

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
                    if ((uint)current >= (uint)parents.Count) break;
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
            s_BroadphaseGrid.Clear();
            s_Candidates.Clear();
            s_CandidateMarks = instances.Count == 0
                ? System.Array.Empty<int>()
                : new int[instances.Count];
            s_CandidateStamp = 0;
            s_ResidentSkeletonCount = 0;
            s_UseCounter = 0;
            s_MaxFallbackRadius = 0f;
            LastBroadphaseCandidateCount = 0;
            LastQuerySkeletonBuildCount = 0;

            for (int i = 0; i < instances.Count; i++)
            {
                TreeInstance instance = instances[i];
                CalculateConservativeBounds(in instance, out float3 min, out float3 max,
                                            out float heightMetres);
                s_Cache.Add(new CachedTree
                {
                    Skeleton = null,
                    BoundsMin = min,
                    BoundsMax = max,
                    HeightMetres = heightMetres,
                    ExactBounds = false,
                    LastUse = 0,
                });
                AddToBroadphaseGrid(i, min, max);
                s_MaxFallbackRadius = math.max(
                    s_MaxFallbackRadius, math.max(3f, heightMetres * 0.35f));
            }

            s_CachedRegistryVersion = version;
        }

        private static TreeSkeletonSnapshot EnsureSkeleton(int treeIndex,
                                                              in TreeInstance instance)
        {
            if ((uint)treeIndex >= (uint)s_Cache.Count) return null;
            CachedTree cached = s_Cache[treeIndex];
            if (cached.Skeleton == null)
            {
                cached.Skeleton = ProceduralTreeSkeletonBuilder.Generate(in instance);
                CalculateBounds(in instance, cached.Skeleton,
                                out cached.BoundsMin, out cached.BoundsMax);
                cached.HeightMetres = cached.Skeleton.Height;
                cached.ExactBounds = true;
                s_ResidentSkeletonCount++;
                LastQuerySkeletonBuildCount++;
            }

            cached.LastUse = ++s_UseCounter;
            EvictSkeletonsIfNeeded(treeIndex);
            return cached.Skeleton;
        }

        private static void EvictSkeletonsIfNeeded(int protectedTreeIndex)
        {
            while (s_ResidentSkeletonCount > MaxResidentSkeletons)
            {
                int oldestIndex = -1;
                ulong oldestUse = ulong.MaxValue;
                for (int i = 0; i < s_Cache.Count; i++)
                {
                    if (i == protectedTreeIndex) continue;
                    CachedTree candidate = s_Cache[i];
                    if (candidate.Skeleton == null || candidate.LastUse >= oldestUse) continue;
                    oldestUse = candidate.LastUse;
                    oldestIndex = i;
                }
                if (oldestIndex < 0) break;
                s_Cache[oldestIndex].Skeleton = null;
                s_ResidentSkeletonCount--;
            }
        }

        private static void CollectCandidates(float3 queryMin, float3 queryMax)
        {
            s_Candidates.Clear();
            LastBroadphaseCandidateCount = 0;
            if (s_Cache.Count == 0 || s_CandidateMarks.Length != s_Cache.Count) return;

            unchecked { s_CandidateStamp++; }
            if (s_CandidateStamp == 0)
            {
                System.Array.Clear(s_CandidateMarks, 0, s_CandidateMarks.Length);
                s_CandidateStamp = 1;
            }

            int minX = Mathf.FloorToInt(queryMin.x / BroadphaseCellSizeMetres);
            int maxX = Mathf.FloorToInt(queryMax.x / BroadphaseCellSizeMetres);
            int minZ = Mathf.FloorToInt(queryMin.z / BroadphaseCellSizeMetres);
            int maxZ = Mathf.FloorToInt(queryMax.z / BroadphaseCellSizeMetres);

            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    if (!s_BroadphaseGrid.TryGetValue(new Vector2Int(x, z),
                                                     out List<int> cellTrees))
                        continue;
                    for (int i = 0; i < cellTrees.Count; i++)
                    {
                        int treeIndex = cellTrees[i];
                        if ((uint)treeIndex >= (uint)s_CandidateMarks.Length) continue;
                        if (s_CandidateMarks[treeIndex] == s_CandidateStamp) continue;
                        s_CandidateMarks[treeIndex] = s_CandidateStamp;
                        s_Candidates.Add(treeIndex);
                    }
                }
            }
            LastBroadphaseCandidateCount = s_Candidates.Count;
        }

        private static void AddToBroadphaseGrid(int treeIndex, float3 min, float3 max)
        {
            int minX = Mathf.FloorToInt(min.x / BroadphaseCellSizeMetres);
            int maxX = Mathf.FloorToInt(max.x / BroadphaseCellSizeMetres);
            int minZ = Mathf.FloorToInt(min.z / BroadphaseCellSizeMetres);
            int maxZ = Mathf.FloorToInt(max.z / BroadphaseCellSizeMetres);

            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    var key = new Vector2Int(x, z);
                    if (!s_BroadphaseGrid.TryGetValue(key, out List<int> cellTrees))
                    {
                        cellTrees = new List<int>(8);
                        s_BroadphaseGrid.Add(key, cellTrees);
                    }
                    cellTrees.Add(treeIndex);
                }
            }
        }

        private static void CalculateConservativeBounds(in TreeInstance instance,
                                                        out float3 min, out float3 max,
                                                        out float heightMetres)
        {
            TreeSpeciesProfile profile = TreeSpeciesProfiles.Get(instance.Species);
            float scale = math.max(0.05f, instance.Scale <= 0f ? 1f : instance.Scale);
            var rng = new Random(instance.Seed == 0 ? 1u : instance.Seed);
            heightMetres = rng.NextFloat(profile.HeightMin, profile.HeightMax) * scale;

            float primaryHeightFactor = instance.Species == TreeSpecies.Pine ? 1.20f : 1.18f;
            float pathLength = heightMetres * profile.BranchLengthFactor
                             * primaryHeightFactor * 1.08f;
            float childRatio = profile.BranchLengthDecay * 1.14f;
            float pathMultiplier = 0f;
            float term = 1f;
            int levels = math.max(1, profile.BranchLevels);
            for (int level = 0; level < levels; level++)
            {
                pathMultiplier += term;
                term *= childRatio;
            }
            pathLength *= pathMultiplier;

            float leafSize = profile.LeafSize * scale
                           * (1f + math.max(0f, profile.LeafSizeVariance));
            if (profile.LeafStyle == TreeLeafStyle.Blossom) leafSize *= 1.14f;
            float pad = profile.LeafSpread * scale
                      + leafSize
                      + profile.TrunkRadiusMax * scale
                      + 0.25f;
            float horizontalExtent = pathLength + pad + heightMetres * 0.20f;
            float verticalDown = pathLength + pad;
            float verticalUp = heightMetres + pathLength + pad;
            float3 root = instance.PositionMetres;
            min = root + new float3(-horizontalExtent, -verticalDown, -horizontalExtent);
            max = root + new float3(horizontalExtent, verticalUp, horizontalExtent);
        }

        private static void CalculateBounds(in TreeInstance instance,
                                            TreeSkeletonSnapshot skeleton,
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
