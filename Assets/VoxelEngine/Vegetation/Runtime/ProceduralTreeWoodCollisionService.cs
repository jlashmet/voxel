using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Random = Unity.Mathematics.Random;
using VoxelEngine.Vegetation.Api;
using TreeInstance = VoxelEngine.Vegetation.Api.TreeInstance;

namespace VoxelEngine.Vegetation.Runtime
{
    /// <summary>
    /// Wood-only collision queries over the authoritative semantic tree world. The broadphase uses
    /// conservative procedural bounds; exact queries test only surviving branch capsules, never
    /// foliage anchors, so gameplay can walk through leaves without walking through trunks.
    /// </summary>
    internal static class ProceduralTreeWoodCollisionService
    {
        private const float BroadphaseCellSizeMetres = 32f;

        private readonly struct CachedBounds
        {
            public readonly float3 Min;
            public readonly float3 Max;

            public CachedBounds(float3 min, float3 max)
            {
                Min = min;
                Max = max;
            }
        }

        private static readonly List<CachedBounds> s_Bounds = new();
        private static readonly Dictionary<Vector2Int, List<int>> s_BroadphaseGrid = new();
        private static readonly List<int> s_Candidates = new(64);
        private static int[] s_CandidateMarks = Array.Empty<int>();
        private static int s_CandidateStamp;
        private static int s_CachedRegistryVersion = int.MinValue;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlaySession()
        {
            s_Bounds.Clear();
            s_BroadphaseGrid.Clear();
            s_Candidates.Clear();
            s_CandidateMarks = Array.Empty<int>();
            s_CandidateStamp = 0;
            s_CachedRegistryVersion = int.MinValue;
        }

        public static bool OverlapsAabb(float3 minMetres, float3 maxMetres)
        {
            IReadOnlyList<TreeInstance> instances = TreeWorldState.Instances;
            if (instances.Count == 0) return false;

            float3 queryMin = math.min(minMetres, maxMetres);
            float3 queryMax = math.max(minMetres, maxMetres);
            EnsureIndex(instances);
            CollectCandidates(queryMin, queryMax);

            for (int candidateIndex = 0; candidateIndex < s_Candidates.Count; candidateIndex++)
            {
                int treeIndex = s_Candidates[candidateIndex];
                if ((uint)treeIndex >= (uint)instances.Count
                    || (uint)treeIndex >= (uint)s_Bounds.Count)
                    continue;

                CachedBounds cached = s_Bounds[treeIndex];
                if (!BoundsOverlap(queryMin, queryMax, cached.Min, cached.Max))
                    continue;

                TreeInstance instance = instances[treeIndex];
                TreeSkeletonSnapshot skeleton = ProceduralTreeDamageService.SkeletonFor(treeIndex);
                if (skeleton == null) continue;

                IReadOnlyCollection<int> directCuts = TreeWorldState.RemovedBranches(treeIndex);
                float3 root = instance.PositionMetres;
                for (int branchIndex = 0; branchIndex < skeleton.Branches.Count; branchIndex++)
                {
                    if (TreeSkeletonTopology.IsBranchRemoved(skeleton, directCuts, branchIndex))
                        continue;

                    TreeBranchSegment branch = skeleton.Branches[branchIndex];
                    float radius = math.max(0.01f, math.max(branch.RadiusStart, branch.RadiusEnd));
                    float3 expandedMin = queryMin - radius;
                    float3 expandedMax = queryMax + radius;
                    if (SegmentIntersectsAabb(
                            root + branch.Start, root + branch.End,
                            expandedMin, expandedMax))
                        return true;
                }
            }

            return false;
        }

        private static void EnsureIndex(IReadOnlyList<TreeInstance> instances)
        {
            int version = TreeWorldState.Version;
            if (s_CachedRegistryVersion == version && s_Bounds.Count == instances.Count)
                return;

            s_Bounds.Clear();
            s_BroadphaseGrid.Clear();
            s_Candidates.Clear();
            s_CandidateMarks = instances.Count == 0 ? Array.Empty<int>() : new int[instances.Count];
            s_CandidateStamp = 0;

            for (int i = 0; i < instances.Count; i++)
            {
                TreeInstance instance = instances[i];
                CalculateConservativeBounds(in instance, out float3 min, out float3 max);
                s_Bounds.Add(new CachedBounds(min, max));
                AddToBroadphaseGrid(i, min, max);
            }

            s_CachedRegistryVersion = version;
        }

        private static void CollectCandidates(float3 queryMin, float3 queryMax)
        {
            s_Candidates.Clear();
            if (s_Bounds.Count == 0 || s_CandidateMarks.Length != s_Bounds.Count) return;

            unchecked { s_CandidateStamp++; }
            if (s_CandidateStamp == 0)
            {
                Array.Clear(s_CandidateMarks, 0, s_CandidateMarks.Length);
                s_CandidateStamp = 1;
            }

            int minX = Mathf.FloorToInt(queryMin.x / BroadphaseCellSizeMetres);
            int maxX = Mathf.FloorToInt(queryMax.x / BroadphaseCellSizeMetres);
            int minZ = Mathf.FloorToInt(queryMin.z / BroadphaseCellSizeMetres);
            int maxZ = Mathf.FloorToInt(queryMax.z / BroadphaseCellSizeMetres);

            for (int x = minX; x <= maxX; x++)
            for (int z = minZ; z <= maxZ; z++)
            {
                if (!s_BroadphaseGrid.TryGetValue(new Vector2Int(x, z), out List<int> cellTrees))
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

        private static void AddToBroadphaseGrid(int treeIndex, float3 min, float3 max)
        {
            int minX = Mathf.FloorToInt(min.x / BroadphaseCellSizeMetres);
            int maxX = Mathf.FloorToInt(max.x / BroadphaseCellSizeMetres);
            int minZ = Mathf.FloorToInt(min.z / BroadphaseCellSizeMetres);
            int maxZ = Mathf.FloorToInt(max.z / BroadphaseCellSizeMetres);

            for (int x = minX; x <= maxX; x++)
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

        private static void CalculateConservativeBounds(
            in TreeInstance instance, out float3 min, out float3 max)
        {
            TreeSpeciesProfile profile = TreeSpeciesProfiles.Get(instance.Species);
            float scale = math.max(0.05f, instance.Scale <= 0f ? 1f : instance.Scale);
            var rng = new Random(instance.Seed == 0 ? 1u : instance.Seed);
            float heightMetres = rng.NextFloat(profile.HeightMin, profile.HeightMax) * scale;

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

        private static bool SegmentIntersectsAabb(float3 start, float3 end, float3 min, float3 max)
        {
            float3 delta = end - start;
            float tMin = 0f;
            float tMax = 1f;
            return ClipAxis(start.x, delta.x, min.x, max.x, ref tMin, ref tMax)
                && ClipAxis(start.y, delta.y, min.y, max.y, ref tMin, ref tMax)
                && ClipAxis(start.z, delta.z, min.z, max.z, ref tMin, ref tMax);
        }

        private static bool ClipAxis(
            float start, float delta, float min, float max, ref float tMin, ref float tMax)
        {
            if (math.abs(delta) <= 1e-6f)
                return start >= min && start <= max;

            float inverse = 1f / delta;
            float t1 = (min - start) * inverse;
            float t2 = (max - start) * inverse;
            if (t1 > t2)
            {
                float swap = t1;
                t1 = t2;
                t2 = swap;
            }

            tMin = math.max(tMin, t1);
            tMax = math.min(tMax, t2);
            return tMin <= tMax;
        }

        private static bool BoundsOverlap(float3 aMin, float3 aMax, float3 bMin, float3 bMax) =>
            aMin.x <= bMax.x && aMax.x >= bMin.x
            && aMin.y <= bMax.y && aMax.y >= bMin.y
            && aMin.z <= bMax.z && aMax.z >= bMin.z;
    }
}
