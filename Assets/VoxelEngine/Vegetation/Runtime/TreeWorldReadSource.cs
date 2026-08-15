using System;
using System.Collections.Generic;
using VoxelEngine.Vegetation.Api;

namespace VoxelEngine.Vegetation.Runtime
{
    internal sealed class TreeWorldReadSource : ITreeWorldReadSource
    {
        internal static readonly TreeWorldReadSource Instance = new();
        private TreeWorldReadSource() { }
        public IReadOnlyList<TreeInstance> Instances => TreeWorldState.Instances;
        public IReadOnlyList<TreeDamageState> Damage => TreeWorldState.Damage;
        public int Version => TreeWorldState.Version;
        public int DamageVersion => TreeWorldState.DamageVersion;
        public event Action SnapshotChanged { add => TreeWorldState.SnapshotChanged += value; remove => TreeWorldState.SnapshotChanged -= value; }
        public event Action<TreeBranchCutEvent> BranchCut { add => TreeWorldState.BranchCut += value; remove => TreeWorldState.BranchCut -= value; }
        public event Action<TreeDamageChangedEvent> DamageChanged { add => TreeWorldState.DamageChanged += value; remove => TreeWorldState.DamageChanged -= value; }
        public event Action<TreeSeveredEvent> TreeSevered { add => TreeWorldState.TreeSevered += value; remove => TreeWorldState.TreeSevered -= value; }
        public IReadOnlyCollection<int> RemovedBranches(int treeIndex) => TreeWorldState.RemovedBranches(treeIndex);
        public TreeSkeletonSnapshot SkeletonFor(int treeIndex) => ProceduralTreeDamageService.SkeletonFor(treeIndex);
        public TreeSkeletonSnapshot SkeletonFor(in TreeInstance instance) => ProceduralTreeSkeletonBuilder.Generate(in instance);
    }
}
