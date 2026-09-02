using System;
using System.Collections.Generic;
using VoxelEngine.Vegetation.Api;

namespace VoxelEngine.Vegetation.Runtime
{
    internal sealed class TreeWorldReadSource : ITreeWorldReadSource
    {
        internal static readonly TreeWorldReadSource Instance = new();

        private TreeWorldReadSource()
        {
            Rebind();
        }

        public IReadOnlyList<TreeInstance> Instances => TreeWorldState.Instances;
        public IReadOnlyList<TreeDamageState> Damage => TreeWorldState.Damage;
        public int Version => TreeWorldState.Version;
        public int DamageVersion => TreeWorldState.DamageVersion;

        public event Action SnapshotChanged;
        public event Action<TreeBranchCutEvent> BranchCut;
        public event Action<TreeDamageChangedEvent> DamageChanged;
        public event Action<TreeSeveredEvent> TreeSevered;

        internal void Rebind()
        {
            TreeWorldState.SnapshotChanged -= ForwardSnapshotChanged;
            TreeWorldState.BranchCut -= ForwardBranchCut;
            TreeWorldState.DamageChanged -= ForwardDamageChanged;
            TreeWorldState.TreeSevered -= ForwardTreeSevered;

            TreeWorldState.SnapshotChanged += ForwardSnapshotChanged;
            TreeWorldState.BranchCut += ForwardBranchCut;
            TreeWorldState.DamageChanged += ForwardDamageChanged;
            TreeWorldState.TreeSevered += ForwardTreeSevered;
        }

        public IReadOnlyCollection<int> RemovedBranches(int treeIndex) => TreeWorldState.RemovedBranches(treeIndex);
        public TreeSkeletonSnapshot SkeletonFor(int treeIndex) => ProceduralTreeDamageService.SkeletonFor(treeIndex);
        public TreeSkeletonSnapshot SkeletonFor(in TreeInstance instance) => ProceduralTreeSkeletonBuilder.Generate(in instance);

        private void ForwardSnapshotChanged() => SnapshotChanged?.Invoke();
        private void ForwardBranchCut(TreeBranchCutEvent change) => BranchCut?.Invoke(change);
        private void ForwardDamageChanged(TreeDamageChangedEvent change) => DamageChanged?.Invoke(change);
        private void ForwardTreeSevered(TreeSeveredEvent change) => TreeSevered?.Invoke(change);
    }
}
