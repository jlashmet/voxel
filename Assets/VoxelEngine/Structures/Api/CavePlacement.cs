using System;
using Unity.Collections;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    [Flags]
    public enum CaveTraversalFlags : byte
    {
        None = 0,
        ReachableFromEntrance = 1 << 0,
        MainPath = 1 << 1,
        Branch = 1 << 2,
        Terminal = 1 << 3,
    }

    /// <summary>
    /// A bounded semantic placement point derived from the authored cave traversal tree.
    /// TraversalDistance is cumulative carved path length from the entrance, not Euclidean distance.
    /// </summary>
    public struct CaveTraversalCandidate
    {
        public int3 Position;
        public int TraversalDistance;
        public byte BranchDepth;
        public CaveTraversalFlags Flags;

        public bool IsWellFormed
        {
            get
            {
                if (TraversalDistance < 0) return false;
                if ((Flags & CaveTraversalFlags.ReachableFromEntrance) == 0) return false;
                if ((Flags & CaveTraversalFlags.Terminal) == 0) return false;

                bool main = (Flags & CaveTraversalFlags.MainPath) != 0;
                bool branch = (Flags & CaveTraversalFlags.Branch) != 0;
                if (main == branch) return false;
                if (main && BranchDepth != 0) return false;
                if (branch && BranchDepth == 0) return false;
                return true;
            }
        }
    }

    /// <summary>
    /// Terminal traversal candidates for one bounded cave. Cave generation emits at most one main
    /// terminal plus one terminal per authored branch, so this remains bounded by CaveConfig.MaxBranches.
    /// The 1024-byte list comfortably covers the current 33-candidate hard maximum without making
    /// CaveAuthoringResult carry a 4KB inline collection.
    /// </summary>
    public struct CaveTraversalCandidateSet
    {
        public FixedList1024Bytes<CaveTraversalCandidate> Items;
        public int Count => Items.Length;
    }

    /// <summary>
    /// Integer-only hard requirements for gameplay-aware cave placement. These rules deliberately
    /// describe traversal semantics rather than world-space proximity, so a path that loops back near
    /// the entrance can still count as deep progression.
    /// </summary>
    public struct CavePlacementRequirements
    {
        public int MinTraversalDistance;

        /// <summary>-1 means no upper bound.</summary>
        public int MaxTraversalDistance;

        public byte MinBranchDepth;
        public byte MaxBranchDepth;
        public CaveTraversalFlags RequiredFlags;
        public CaveTraversalFlags ForbiddenFlags;

        public bool IsWellFormed =>
            MinTraversalDistance >= 0 &&
            (MaxTraversalDistance == -1 || MaxTraversalDistance >= MinTraversalDistance) &&
            MinBranchDepth <= MaxBranchDepth &&
            (RequiredFlags & ForbiddenFlags) == 0;

        public bool Matches(in CaveTraversalCandidate candidate)
        {
            if (!IsWellFormed || !candidate.IsWellFormed) return false;
            if (candidate.TraversalDistance < MinTraversalDistance) return false;
            if (MaxTraversalDistance >= 0 && candidate.TraversalDistance > MaxTraversalDistance) return false;
            if (candidate.BranchDepth < MinBranchDepth || candidate.BranchDepth > MaxBranchDepth) return false;
            if ((candidate.Flags & RequiredFlags) != RequiredFlags) return false;
            return (candidate.Flags & ForbiddenFlags) == 0;
        }

        public static CavePlacementRequirements AnyReachableTerminal(int minTraversalDistance = 0) =>
            new CavePlacementRequirements
            {
                MinTraversalDistance = minTraversalDistance,
                MaxTraversalDistance = -1,
                MinBranchDepth = 0,
                MaxBranchDepth = byte.MaxValue,
                RequiredFlags = CaveTraversalFlags.ReachableFromEntrance | CaveTraversalFlags.Terminal,
                ForbiddenFlags = CaveTraversalFlags.None,
            };
    }

    public static class CavePlacementResolver
    {
        /// <summary>
        /// Selects the deepest matching candidate. Equal-depth ties use a total lexicographic order,
        /// so selection cannot depend on generation/enumeration order.
        /// </summary>
        public static bool TrySelectDeepest(
            in CaveTraversalCandidateSet candidates,
            in CavePlacementRequirements requirements,
            out CaveTraversalCandidate selected)
        {
            selected = default;
            if (!requirements.IsWellFormed) return false;

            bool found = false;
            for (int i = 0; i < candidates.Items.Length; i++)
            {
                CaveTraversalCandidate candidate = candidates.Items[i];
                if (!requirements.Matches(in candidate)) continue;

                if (!found || IsBetter(in candidate, in selected))
                {
                    selected = candidate;
                    found = true;
                }
            }
            return found;
        }

        private static bool IsBetter(in CaveTraversalCandidate candidate, in CaveTraversalCandidate selected)
        {
            if (candidate.TraversalDistance != selected.TraversalDistance)
                return candidate.TraversalDistance > selected.TraversalDistance;
            if (candidate.BranchDepth != selected.BranchDepth)
                return candidate.BranchDepth > selected.BranchDepth;
            if (candidate.Position.x != selected.Position.x)
                return candidate.Position.x < selected.Position.x;
            if (candidate.Position.y != selected.Position.y)
                return candidate.Position.y < selected.Position.y;
            if (candidate.Position.z != selected.Position.z)
                return candidate.Position.z < selected.Position.z;
            return (byte)candidate.Flags < (byte)selected.Flags;
        }
    }
}
