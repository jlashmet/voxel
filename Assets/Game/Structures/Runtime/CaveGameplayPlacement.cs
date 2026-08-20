using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    public enum CaveGameplayPlacementKind : byte
    {
        Boss = 0,
        Treasure = 1,
    }

    /// <summary>
    /// Semantic gameplay reservation over an authored cave traversal terminal. This chooses where
    /// gameplay belongs; spawning/rendering is deliberately a later composition step.
    /// </summary>
    public struct CaveGameplayPlacement
    {
        public CaveGameplayPlacementKind Kind;
        public CaveTraversalCandidate Terminal;

        public bool IsWellFormed =>
            (Kind == CaveGameplayPlacementKind.Boss || Kind == CaveGameplayPlacementKind.Treasure) &&
            Terminal.IsWellFormed;
    }

    public static class CaveGameplayPlacementPlanner
    {
        /// <summary>
        /// Bosses occupy a sufficiently deep reachable main-path terminal. The threshold is authored
        /// by the caller and is traversal distance, never straight-line world distance.
        /// </summary>
        public static bool TrySelectBoss(
            in CaveTraversalCandidateSet candidates,
            int minTraversalDistance,
            out CaveGameplayPlacement placement)
        {
            placement = default;
            if (minTraversalDistance < 0)
                return false;

            CaveTraversalFlags mainTerminal =
                CaveTraversalFlags.ReachableFromEntrance |
                CaveTraversalFlags.MainPath |
                CaveTraversalFlags.Terminal;
            var requirements = new CavePlacementRequirements
            {
                MinTraversalDistance = minTraversalDistance,
                MaxTraversalDistance = -1,
                MinBranchDepth = 0,
                MaxBranchDepth = 0,
                RequiredFlags = mainTerminal,
                ForbiddenFlags = CaveTraversalFlags.Branch,
            };

            if (!CavePlacementResolver.TrySelectDeepest(
                    in candidates, in requirements, out CaveTraversalCandidate terminal))
                return false;

            placement = new CaveGameplayPlacement
            {
                Kind = CaveGameplayPlacementKind.Boss,
                Terminal = terminal,
            };
            return placement.IsWellFormed;
        }

        /// <summary>
        /// Treasure must be sufficiently deep and reachable. Optional branch terminals are preferred,
        /// but absence of a branch never weakens the hard depth/reachability requirements: selection
        /// falls back deterministically to the best hard-valid terminal.
        /// </summary>
        public static bool TrySelectTreasure(
            in CaveTraversalCandidateSet candidates,
            int minTraversalDistance,
            out CaveGameplayPlacement placement)
        {
            placement = default;
            if (minTraversalDistance < 0)
                return false;

            CavePlacementRequirements requirements =
                CavePlacementRequirements.AnyReachableTerminal(minTraversalDistance);
            CavePlacementPreferences preferences = CavePlacementPreferences.PreferBranchTerminal;
            if (!CavePlacementResolver.TrySelectBest(
                    in candidates, in requirements, in preferences,
                    out CaveTraversalCandidate terminal))
                return false;

            placement = new CaveGameplayPlacement
            {
                Kind = CaveGameplayPlacementKind.Treasure,
                Terminal = terminal,
            };
            return placement.IsWellFormed;
        }
    }
}
