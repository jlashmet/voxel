using System;
using System.Collections.Generic;
using VoxelEngine.Vegetation.Api;

namespace VoxelEngine.Vegetation.Runtime
{
    internal sealed class TreeWorldReadSource : ITreeWorldReadSource
    {
        internal static readonly TreeWorldReadSource Instance = new();

        private event Action _snapshotChanged;
        private event Action<TreeBranchCutEvent> _branchCut;
        private event Action<TreeDamageChangedEvent> _damageChanged;
        private event Action<TreeSeveredEvent> _treeSevered;

        private TreeWorldReadSource()
        {
            Rebind();
        }

        public IReadOnlyList<TreeInstance> Instances => TreeWorldState.Instances;
        public IReadOnlyList<TreeDamageState> Damage => TreeWorldState.Damage;
        public int Version => TreeWorldState.Version;
        public int DamageVersion => TreeWorldState.DamageVersion;

        public event Action SnapshotChanged
        {
            add => _snapshotChanged += value;
            remove => _snapshotChanged -= value;
        }

        public event Action<TreeBranchCutEvent> BranchCut
        {
            add => _branchCut += value;
            remove => _branchCut -= value;
        }

        public event Action<TreeDamageChangedEvent> DamageChanged
        {
            add => _damageChanged += value;
            remove => _damageChanged -= value;
        }

        public event Action<TreeSeveredEvent> TreeSevered
        {
            add => _treeSevered += value;
            remove => _treeSevered -= value;
        }

        internal void Rebind()
        {
            // TreeWorldState clears its static delegates at subsystem registration. Keep observer
            // subscriptions on this stable read source and restore only the forwarding hooks.
            TreeWorldState.SnapshotChanged -= ForwardSnapshotChanged;
            TreeWorldState.BranchCut -= ForwardBranchCut;
            TreeWorldState.DamageChanged -= ForwardDamageChanged;
            TreeWorldState.TreeSevered -= ForwardTreeSevered;

            TreeWorldState.SnapshotChanged += ForwardSnapshotChanged;
            TreeWorldState.BranchCut += ForwardBranchCut;
            TreeWorldState.DamageChanged += ForwardDamageChanged;
            TreeWorldState.TreeSevered += ForwardTreeSevered;
        }

        private void ForwardSnapshotChanged() => _snapshotChanged?.Invoke();
        private void ForwardBranchCut(TreeBranchCutEvent cut) => _branchCut?.Invoke(cut);
        private void ForwardDamageChanged(TreeDamageChangedEvent damage) => _damageChanged?.Invoke(damage);
        private void ForwardTreeSevered(TreeSeveredEvent severed) => _treeSevered?.Invoke(severed);

        public IReadOnlyCollection<int> RemovedBranches(int treeIndex) => TreeWorldState.RemovedBranches(treeIndex);
        public TreeSkeletonSnapshot SkeletonFor(int treeIndex) => ProceduralTreeDamageService.SkeletonFor(treeIndex);
        public TreeSkeletonSnapshot SkeletonFor(in TreeInstance instance) => ProceduralTreeSkeletonBuilder.Generate(in instance);
    }
}
