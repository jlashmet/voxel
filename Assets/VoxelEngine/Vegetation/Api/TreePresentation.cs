using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace VoxelEngine.Vegetation.Api
{
    public struct TreeBranchSegment
    {
        public float3 Start;
        public float3 End;
        public float RadiusStart;
        public float RadiusEnd;
        public int Level;
    }

    public struct TreeLeafAnchor
    {
        public float3 Position;
        public float3 Direction;
        public float Size;
        public float Rotation;
        public float4 Colour;
        public TreeLeafStyle Style;
    }

    /// <summary>Immutable semantic skeleton snapshot consumed by presentation and queries.</summary>
    public sealed class TreeSkeletonSnapshot
    {
        private readonly TreeBranchSegment[] _branches;
        private readonly TreeLeafAnchor[] _leaves;
        private readonly int[] _branchParents;
        private readonly int[] _leafParents;

        public IReadOnlyList<TreeBranchSegment> Branches => _branches;
        public IReadOnlyList<TreeLeafAnchor> Leaves => _leaves;
        public IReadOnlyList<int> BranchParents => _branchParents;
        public IReadOnlyList<int> LeafParents => _leafParents;
        public TreeSpeciesProfile Profile { get; }
        public float Height { get; }

        public TreeSkeletonSnapshot(
            TreeBranchSegment[] branches,
            TreeLeafAnchor[] leaves,
            TreeSpeciesProfile profile,
            float height,
            int[] branchParents,
            int[] leafParents)
        {
            _branches = branches == null
                ? Array.Empty<TreeBranchSegment>()
                : (TreeBranchSegment[])branches.Clone();
            _leaves = leaves == null
                ? Array.Empty<TreeLeafAnchor>()
                : (TreeLeafAnchor[])leaves.Clone();
            _branchParents = branchParents == null
                ? Array.Empty<int>()
                : (int[])branchParents.Clone();
            _leafParents = leafParents == null
                ? Array.Empty<int>()
                : (int[])leafParents.Clone();
            Profile = profile;
            Height = height;
        }
    }

    public readonly struct TreeDamageState
    {
        public readonly float FoliageHealth;
        public readonly bool Severed;

        public TreeDamageState(float foliageHealth, bool severed)
        {
            FoliageHealth = foliageHealth;
            Severed = severed;
        }
    }

    public readonly struct TreeBranchCutEvent
    {
        public readonly int TreeIndex;
        public readonly int BranchIndex;
        public readonly float3 HitPointMetres;
        public readonly float3 Impulse;

        public TreeBranchCutEvent(int treeIndex, int branchIndex,
                                  float3 hitPointMetres, float3 impulse)
        {
            TreeIndex = treeIndex;
            BranchIndex = branchIndex;
            HitPointMetres = hitPointMetres;
            Impulse = impulse;
        }
    }

    public readonly struct TreeDamageChangedEvent
    {
        public readonly int TreeIndex;
        public readonly float FoliageHealth;
        public readonly bool Severed;

        public TreeDamageChangedEvent(int treeIndex, float foliageHealth, bool severed)
        {
            TreeIndex = treeIndex;
            FoliageHealth = foliageHealth;
            Severed = severed;
        }
    }

    public readonly struct TreeSeveredEvent
    {
        public readonly int TreeIndex;
        public readonly int BreakBranchIndex;
        public readonly float3 HitPointMetres;
        public readonly float3 Impulse;

        public TreeSeveredEvent(int treeIndex, int breakBranchIndex,
                                float3 hitPointMetres, float3 impulse)
        {
            TreeIndex = treeIndex;
            BreakBranchIndex = breakBranchIndex;
            HitPointMetres = hitPointMetres;
            Impulse = impulse;
        }
    }

    /// <summary>Read-only tree-world capability for rendering and other observers.</summary>
    public interface ITreeWorldReadSource
    {
        IReadOnlyList<TreeInstance> Instances { get; }
        IReadOnlyList<TreeDamageState> Damage { get; }
        int Version { get; }
        int DamageVersion { get; }

        event Action SnapshotChanged;
        event Action<TreeBranchCutEvent> BranchCut;
        event Action<TreeDamageChangedEvent> DamageChanged;
        event Action<TreeSeveredEvent> TreeSevered;

        IReadOnlyCollection<int> RemovedBranches(int treeIndex);
        TreeSkeletonSnapshot SkeletonFor(int treeIndex);
        TreeSkeletonSnapshot SkeletonFor(in TreeInstance instance);
    }

    /// <summary>
    /// Runtime registration point for the current read source. Consumers depend only on Api;
    /// Vegetation.Runtime owns the registered implementation.
    /// </summary>
    public static class TreeWorldReadRegistry
    {
        private static ITreeWorldReadSource s_Current = EmptyTreeWorldReadSource.Instance;

        public static ITreeWorldReadSource Current => s_Current;

        public static void Register(ITreeWorldReadSource source)
        {
            s_Current = source ?? EmptyTreeWorldReadSource.Instance;
        }

        private sealed class EmptyTreeWorldReadSource : ITreeWorldReadSource
        {
            public static readonly EmptyTreeWorldReadSource Instance = new();

            public IReadOnlyList<TreeInstance> Instances => Array.Empty<TreeInstance>();
            public IReadOnlyList<TreeDamageState> Damage => Array.Empty<TreeDamageState>();
            public int Version => 0;
            public int DamageVersion => 0;

            public event Action SnapshotChanged { add { } remove { } }
            public event Action<TreeBranchCutEvent> BranchCut { add { } remove { } }
            public event Action<TreeDamageChangedEvent> DamageChanged { add { } remove { } }
            public event Action<TreeSeveredEvent> TreeSevered { add { } remove { } }

            public IReadOnlyCollection<int> RemovedBranches(int treeIndex) => Array.Empty<int>();
            public TreeSkeletonSnapshot SkeletonFor(int treeIndex) => null;
            public TreeSkeletonSnapshot SkeletonFor(in TreeInstance instance) => null;
        }
    }

    public static class TreeSkeletonTopology
    {
        public static void ResolveRemovedBranches(
            TreeSkeletonSnapshot skeleton,
            IReadOnlyCollection<int> directCuts,
            HashSet<int> resolved)
        {
            resolved.Clear();
            if (skeleton == null || directCuts == null || directCuts.Count == 0) return;

            // A structural level-zero cut means the tree is no longer rooted. Preserve the exact
            // authored segment in directCuts for debris/presentation events, but treat the entire
            // remaining skeleton as disconnected for rendering and collision queries. Keeping the
            // lower trunk as a rooted semantic stump is what left the SceneIssue 033015 trunk
            // standing while the crown fell.
            foreach (int cut in directCuts)
            {
                if ((uint)cut >= (uint)skeleton.Branches.Count) continue;
                if (skeleton.Branches[cut].Level != 0) continue;
                for (int i = 0; i < skeleton.Branches.Count; i++) resolved.Add(i);
                return;
            }

            foreach (int cut in directCuts)
                if ((uint)cut < (uint)skeleton.Branches.Count)
                    resolved.Add(cut);

            for (int i = 0; i < skeleton.BranchParents.Count; i++)
            {
                int parent = skeleton.BranchParents[i];
                if (parent >= 0 && resolved.Contains(parent))
                    resolved.Add(i);
            }
        }

        public static bool IsBranchRemoved(
            TreeSkeletonSnapshot skeleton,
            IReadOnlyCollection<int> directCuts,
            int branchIndex)
        {
            if (skeleton == null || directCuts == null || directCuts.Count == 0) return false;

            // Match ResolveRemovedBranches: once a level-zero segment has severed, there is no
            // rooted procedural tree left to collide with. Detached debris owns the falling body.
            foreach (int cut in directCuts)
            {
                if ((uint)cut < (uint)skeleton.Branches.Count
                    && skeleton.Branches[cut].Level == 0)
                    return true;
            }

            int current = branchIndex;
            while (current >= 0)
            {
                foreach (int cut in directCuts)
                    if (cut == current) return true;

                if (current >= skeleton.BranchParents.Count) break;
                int parent = skeleton.BranchParents[current];
                if (parent == current) break;
                current = parent;
            }

            return false;
        }
    }
}
