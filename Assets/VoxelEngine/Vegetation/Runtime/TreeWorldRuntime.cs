using System;
using System.Collections.Generic;
using Unity.Mathematics;
using VoxelEngine.Vegetation.Api;

namespace VoxelEngine.Vegetation.Runtime
{
    /// <summary>
    /// Composition/gameplay facade for mutating Runtime-owned tree state.
    /// Presentation observes the separate Vegetation.Api read capability.
    /// </summary>
    public static class TreeWorldRuntime
    {
        public static IReadOnlyList<TreeInstance> Instances => TreeWorldState.Instances;
        public static IReadOnlyList<TreeDamageState> Damage => TreeWorldState.Damage;
        public static int Version => TreeWorldState.Version;
        public static int DamageVersion => TreeWorldState.DamageVersion;

        public static event Action SnapshotChanged
        {
            add => TreeWorldState.SnapshotChanged += value;
            remove => TreeWorldState.SnapshotChanged -= value;
        }

        public static event Action<TreeBranchCutEvent> BranchCut
        {
            add => TreeWorldState.BranchCut += value;
            remove => TreeWorldState.BranchCut -= value;
        }

        public static event Action<TreeDamageChangedEvent> DamageChanged
        {
            add => TreeWorldState.DamageChanged += value;
            remove => TreeWorldState.DamageChanged -= value;
        }

        public static event Action<TreeSeveredEvent> TreeSevered
        {
            add => TreeWorldState.TreeSevered += value;
            remove => TreeWorldState.TreeSevered -= value;
        }

        public static void Replace(IReadOnlyList<TreeInstance> instances) =>
            TreeWorldState.Replace(instances);

        public static IReadOnlyCollection<int> RemovedBranches(int treeIndex) =>
            TreeWorldState.RemovedBranches(treeIndex);

        public static bool RemoveBranch(
            int treeIndex,
            int branchIndex,
            float3 hitPointMetres = default,
            float3 impulse = default) =>
            TreeWorldState.RemoveBranch(treeIndex, branchIndex, hitPointMetres, impulse);

        public static void SetDamage(
            int index,
            float foliageHealth,
            bool severed,
            float3 hitPointMetres = default,
            float3 impulse = default,
            int breakBranchIndex = -1) =>
            TreeWorldState.SetDamage(index, foliageHealth, severed,
                                     hitPointMetres, impulse, breakBranchIndex);
    }
}
