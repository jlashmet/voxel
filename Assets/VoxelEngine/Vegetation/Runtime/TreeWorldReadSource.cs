using System;
using System.Collections.Generic;
using VoxelEngine.Vegetation.Api;

namespace VoxelEngine.Vegetation.Runtime
{
    internal sealed class TreeWorldReadSource : ITreeWorldReadSource
    {
        internal static readonly TreeWorldReadSource Instance = new();

        private event Action SnapshotChangedHandlers;
        private event Action<TreeBranchCutEvent> BranchCutHandlers;
        private event Action<TreeDamageChangedEvent> DamageChangedHandlers;
        private event Action<TreeSeveredEvent> TreeSeveredHandlers;

        private TreeWorldReadSource()
        {
            RebindTreeWorldEvents();
        }

        public IReadOnlyList<TreeInstance> Instances => TreeWorldState.Instances;
        public IReadOnlyList<TreeDamageState> Damage => TreeWorldState.Damage;
        public int Version => TreeWorldState.Version;
        public int DamageVersion => TreeWorldState.DamageVersion;

        public event Action SnapshotChanged
        {
            add => SnapshotChangedHandlers += value;
            remove => SnapshotChangedHandlers -= value;
        }

        public event Action<TreeBranchCutEvent> BranchCut
        {
            add => BranchCutHandlers += value;
            remove => BranchCutHandlers -= value;
        }

        public event Action<TreeDamageChangedEvent> DamageChanged
        {
            add => DamageChangedHandlers += value;
            remove => DamageChangedHandlers -= value;
        }

        public event Action<TreeSeveredEvent> TreeSevered
        {
            add => TreeSeveredHandlers += value;
            remove => TreeSeveredHandlers -= value;
        }

        internal void RebindTreeWorldEvents()
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

        private void ForwardSnapshotChanged() => SnapshotChangedHandlers?.Invoke();
        private void ForwardBranchCut(TreeBranchCutEvent cut) => BranchCutHandlers?.Invoke(cut);
        private void ForwardDamageChanged(TreeDamageChangedEvent damage) => DamageChangedHandlers?.Invoke(damage);
        private void ForwardTreeSevered(TreeSeveredEvent severed) => TreeSeveredHandlers?.Invoke(severed);

        public IReadOnlyCollection<int> RemovedBranches(int treeIndex) => TreeWorldState.RemovedBranches(treeIndex);
        public TreeSkeletonSnapshot SkeletonFor(int treeIndex) => ProceduralTreeDamageService.SkeletonFor(treeIndex);
        public TreeSkeletonSnapshot SkeletonFor(in TreeInstance instance) => ProceduralTreeSkeletonBuilder.Generate(in instance);
    }
}