using System.Collections.Generic;
using VoxelEngine.Vegetation.Api;

namespace VoxelEngine.Vegetation.Runtime
{
    /// <summary>
    /// Runtime-owned mutation facade for the procedural tree world. Mutable collections stay behind
    /// this assembly boundary; external systems consume Vegetation.Api values and read snapshots.
    /// </summary>
    public static class TreeWorldRuntime
    {
        public static void Replace(IReadOnlyList<TreeInstance> instances)
        {
            TreeWorldState.Replace(instances);
        }

        public static void Clear()
        {
            TreeWorldState.Replace(null);
        }

        public static bool RemoveBranch(int treeIndex, int branchIndex,
                                        Unity.Mathematics.float3 cutMetres = default,
                                        Unity.Mathematics.float3 impulse = default)
        {
            return TreeWorldState.RemoveBranch(treeIndex, branchIndex, cutMetres, impulse);
        }

        public static void SetDamage(int treeIndex, float foliageHealth, bool severed,
                                     Unity.Mathematics.float3 impactMetres = default,
                                     Unity.Mathematics.float3 impulse = default,
                                     int branchIndex = -1)
        {
            TreeWorldState.SetDamage(treeIndex, foliageHealth, severed,
                                     impactMetres, impulse, branchIndex);
        }

        public static IReadOnlyList<TreeInstance> Instances => TreeWorldState.Instances;
        public static IReadOnlyList<TreeDamageState> Damage => TreeWorldState.Damage;
        public static IReadOnlyCollection<int> RemovedBranches(int treeIndex) =>
            TreeWorldState.RemovedBranches(treeIndex);
    }
}
