using Game.Structures.Api;
using Game.Structures.Runtime;
using Game.WorldBuilder.Api;
using VoxelEngine.Structures.Api;

namespace Game.Composition.CaveWorldBuilder
{
    /// <summary>
    /// Failure reported by the reusable cave-secret composition boundary. Physical conflicts are
    /// retried against the next deterministic traversal candidate; mutation/budget failures abort
    /// because the authoring session can no longer be assumed safe for another attempt.
    /// </summary>
    public enum CaveSecretPocketCompositionFailure : byte
    {
        None = 0,
        InvalidRequest = 1,
        NoMatchingTraversal = 2,
        PhysicalConflict = 3,
        InsufficientWriteBudget = 4,
        MutationFailure = 5,
    }

    /// <summary>
    /// Composes existing cave traversal semantics with verified hidden-pocket authoring and the
    /// canonical WorldBuilder candidate bridge. It owns no scene policy, presentation, interaction
    /// state, or discovery state; consumers supply only semantic placement requirements and a site.
    /// </summary>
    public static class CaveSecretPocketComposition
    {
        public static bool TryAuthorBest(
            IStructureAuthoringSession authoring,
            in CaveTraversalCandidateSet candidates,
            in CavePlacementRequirements requirements,
            in CavePlacementPreferences preferences,
            SiteRef site,
            int qualityBasisPoints,
            in CaveSecretPocketConfig config,
            out CaveSecretPocketProjection projection,
            out CaveSecretPocketCompositionFailure failure)
        {
            projection = default;
            failure = CaveSecretPocketCompositionFailure.InvalidRequest;
            if (authoring == null || string.IsNullOrEmpty(site.Id) ||
                qualityBasisPoints < 0 || qualityBasisPoints > 10000 ||
                !requirements.IsWellFormed || !preferences.IsWellFormed || !config.IsWellFormed)
                return false;

            CaveTraversalCandidateSet remaining = candidates;
            bool sawPhysicalConflict = false;
            while (CavePlacementResolver.TrySelectBest(
                       in remaining, in requirements, in preferences, out CaveTraversalCandidate terminal))
            {
                CaveSecretPocketAuthoringFailure authoringFailure;
                if (CaveSecretPocketAuthoring.TryAuthor(
                        authoring, in terminal, in config, out CaveSecretPocket pocket, out authoringFailure))
                {
                    projection = new CaveSecretPocketProjection(site, in pocket, qualityBasisPoints);
                    failure = CaveSecretPocketCompositionFailure.None;
                    return true;
                }

                if (authoringFailure != CaveSecretPocketAuthoringFailure.PhysicalConflict)
                {
                    failure = Map(authoringFailure);
                    return false;
                }

                sawPhysicalConflict = true;
                if (!Remove(ref remaining, in terminal))
                {
                    failure = CaveSecretPocketCompositionFailure.MutationFailure;
                    return false;
                }
            }

            failure = sawPhysicalConflict
                ? CaveSecretPocketCompositionFailure.PhysicalConflict
                : CaveSecretPocketCompositionFailure.NoMatchingTraversal;
            return false;
        }

        private static CaveSecretPocketCompositionFailure Map(CaveSecretPocketAuthoringFailure failure)
        {
            switch (failure)
            {
                case CaveSecretPocketAuthoringFailure.InsufficientWriteBudget:
                    return CaveSecretPocketCompositionFailure.InsufficientWriteBudget;
                case CaveSecretPocketAuthoringFailure.PhysicalConflict:
                    return CaveSecretPocketCompositionFailure.PhysicalConflict;
                case CaveSecretPocketAuthoringFailure.MutationFailure:
                    return CaveSecretPocketCompositionFailure.MutationFailure;
                default:
                    return CaveSecretPocketCompositionFailure.InvalidRequest;
            }
        }

        private static bool Remove(
            ref CaveTraversalCandidateSet candidates,
            in CaveTraversalCandidate selected)
        {
            for (int i = 0; i < candidates.Items.Length; i++)
            {
                CaveTraversalCandidate candidate = candidates.Items[i];
                if (!Same(in candidate, in selected)) continue;
                candidates.Items.RemoveAt(i);
                return true;
            }
            return false;
        }

        private static bool Same(
            in CaveTraversalCandidate left,
            in CaveTraversalCandidate right) =>
            left.Position.x == right.Position.x &&
            left.Position.y == right.Position.y &&
            left.Position.z == right.Position.z &&
            left.TraversalDistance == right.TraversalDistance &&
            left.BranchDepth == right.BranchDepth &&
            left.Flags == right.Flags &&
            left.ExitFacing == right.ExitFacing;
    }
}
