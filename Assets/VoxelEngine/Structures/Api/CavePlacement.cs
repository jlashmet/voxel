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
    /// ExitFacing preserves the final authored horizontal direction so downstream composition never
    /// has to reconstruct private turn state from terminal positions.
    /// </summary>
    public struct CaveTraversalCandidate
    {
        public int3 Position;
        public int TraversalDistance;
        public byte BranchDepth;
        public CaveTraversalFlags Flags;
        public Facing ExitFacing;

        public bool IsWellFormed
        {
            get
            {
                if (TraversalDistance < 0) return false;
                if ((Flags & CaveTraversalFlags.ReachableFromEntrance) == 0) return false;
                if ((Flags & CaveTraversalFlags.Terminal) == 0) return false;
                if (!IsCardinal(ExitFacing)) return false;

                bool main = (Flags & CaveTraversalFlags.MainPath) != 0;
                bool branch = (Flags & CaveTraversalFlags.Branch) != 0;
                if (main == branch) return false;
                if (main && BranchDepth != 0) return false;
                if (branch && BranchDepth == 0) return false;
                return true;
            }
        }

        private static bool IsCardinal(Facing facing) =>
            facing == Facing.North || facing == Facing.East ||
            facing == Facing.South || facing == Facing.West;
    }

    /// <summary>
    /// Terminal traversal candidates for one bounded cave. Cave generation emits at most one main
    /// terminal plus one terminal per authored branch, so this remains bounded by CaveConfig.MaxBranches.
    /// The 4096-byte fixed list covers the configured 33-candidate maximum without allocation.
    /// </summary>
    public struct CaveTraversalCandidateSet
    {
        public FixedList4096Bytes<CaveTraversalCandidate> Items;
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

    /// <summary>
    /// Optional soft ranking applied only after hard placement requirements pass. PreferredFlags are a
    /// conjunction: a candidate carrying every preferred flag outranks a hard-valid candidate that does
    /// not. If no hard-valid candidate satisfies the preference, selection falls back to the deepest
    /// hard-valid candidate instead of failing or weakening the hard requirements.
    /// </summary>
    public struct CavePlacementPreferences
    {
        private const CaveTraversalFlags KnownFlags =
            CaveTraversalFlags.ReachableFromEntrance |
            CaveTraversalFlags.MainPath |
            CaveTraversalFlags.Branch |
            CaveTraversalFlags.Terminal;

        public CaveTraversalFlags PreferredFlags;

        public bool IsWellFormed
        {
            get
            {
                if ((PreferredFlags & ~KnownFlags) != 0) return false;
                bool main = (PreferredFlags & CaveTraversalFlags.MainPath) != 0;
                bool branch = (PreferredFlags & CaveTraversalFlags.Branch) != 0;
                return !(main && branch);
            }
        }

        public static CavePlacementPreferences None => default;

        public static CavePlacementPreferences PreferBranchTerminal =>
            new CavePlacementPreferences
            {
                PreferredFlags = CaveTraversalFlags.Branch | CaveTraversalFlags.Terminal,
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
            CavePlacementPreferences preferences = CavePlacementPreferences.None;
            return TrySelectBest(in candidates, in requirements, in preferences, out selected);
        }

        /// <summary>
        /// Selects among hard-valid candidates, preferring candidates that satisfy all PreferredFlags.
        /// Preferences never admit a candidate rejected by requirements. Equal preference state falls
        /// back to the same deterministic deepest-first ordering as TrySelectDeepest.
        /// </summary>
        public static bool TrySelectBest(
            in CaveTraversalCandidateSet candidates,
            in CavePlacementRequirements requirements,
            in CavePlacementPreferences preferences,
            out CaveTraversalCandidate selected)
        {
            selected = default;
            if (!requirements.IsWellFormed || !preferences.IsWellFormed) return false;

            bool found = false;
            for (int i = 0; i < candidates.Items.Length; i++)
            {
                CaveTraversalCandidate candidate = candidates.Items[i];
                if (!requirements.Matches(in candidate)) continue;

                if (!found || IsBetter(in candidate, in selected, in preferences))
                {
                    selected = candidate;
                    found = true;
                }
            }
            return found;
        }

        private static bool IsBetter(
            in CaveTraversalCandidate candidate,
            in CaveTraversalCandidate selected,
            in CavePlacementPreferences preferences)
        {
            bool candidatePreferred = MatchesPreference(in candidate, in preferences);
            bool selectedPreferred = MatchesPreference(in selected, in preferences);
            if (candidatePreferred != selectedPreferred)
                return candidatePreferred;

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
            if (candidate.ExitFacing != selected.ExitFacing)
                return (int)candidate.ExitFacing < (int)selected.ExitFacing;
            return (byte)candidate.Flags < (byte)selected.Flags;
        }

        private static bool MatchesPreference(
            in CaveTraversalCandidate candidate,
            in CavePlacementPreferences preferences) =>
            preferences.PreferredFlags == CaveTraversalFlags.None ||
            (candidate.Flags & preferences.PreferredFlags) == preferences.PreferredFlags;
    }
}
