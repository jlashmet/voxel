using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Composes semantic treasure ranking with physical hidden-pocket validation. A terminal that is
    /// semantically ideal can still be physically unsuitable because a chamber or another cave volume
    /// already occupies the rock beyond it. Failed pocket preflights are atomic, so the planner removes
    /// that terminal and deterministically retries the next hard-valid candidate.
    /// </summary>
    public static class CaveTreasurePocketPlanner
    {
        public static bool TryAuthorBest(
            IStructureAuthoringSession authoring,
            in CaveTraversalCandidateSet candidates,
            int minTraversalDistance,
            in CaveSecretPocketConfig pocketConfig,
            out CaveGameplayPlacement placement,
            out CaveSecretPocket pocket)
        {
            placement = default;
            pocket = default;
            if (authoring == null || minTraversalDistance < 0 || !pocketConfig.IsWellFormed ||
                authoring.BudgetExceeded)
                return false;

            CaveTraversalCandidateSet remaining = candidates;
            int maxAttempts = remaining.Count;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                if (!CaveGameplayPlacementPlanner.TrySelectTreasure(
                        in remaining, minTraversalDistance, out CaveGameplayPlacement selected))
                    return false;

                CaveTraversalCandidate terminal = selected.Terminal;
                if (CaveSecretPocketAuthoring.TryAuthor(
                        authoring, in terminal, in pocketConfig, out CaveSecretPocket authored))
                {
                    placement = selected;
                    pocket = authored;
                    return true;
                }

                if (authoring.BudgetExceeded)
                    return false;

                remaining = Without(in remaining, in terminal);
            }

            return false;
        }

        private static CaveTraversalCandidateSet Without(
            in CaveTraversalCandidateSet candidates,
            in CaveTraversalCandidate excluded)
        {
            var result = new CaveTraversalCandidateSet();
            for (int i = 0; i < candidates.Items.Length; i++)
            {
                CaveTraversalCandidate candidate = candidates.Items[i];
                if (SameCandidate(in candidate, in excluded))
                    continue;
                result.Items.Add(candidate);
            }
            return result;
        }

        private static bool SameCandidate(
            in CaveTraversalCandidate left,
            in CaveTraversalCandidate right) =>
            left.Position.Equals(right.Position) &&
            left.TraversalDistance == right.TraversalDistance &&
            left.BranchDepth == right.BranchDepth &&
            left.Flags == right.Flags &&
            left.ExitFacing == right.ExitFacing;
    }
}
