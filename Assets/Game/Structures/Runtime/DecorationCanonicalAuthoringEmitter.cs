using Game.Structures.Api;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Routes a mixed canonical decoration placement set through the existing production catalog
    /// emitters. This type owns no prop geometry; each catalog remains authoritative for its range.
    /// </summary>
    public static class DecorationCanonicalAuthoringEmitter
    {
        public static bool TryAuthorGeometry(
            IStructureAuthoringSession authoring,
            DecorationPlacement[] placements,
            in DecorationContext context,
            DecorationRegionTheme region)
        {
            if (authoring == null || placements == null || !context.IsWellFormed)
                return false;

            return DecorationContentAuthoringEmitter.TryAuthorGeometry(authoring, placements, in context) &&
                   DecorationExpansion200AuthoringEmitter.TryAuthorGeometry(authoring, placements, in context) &&
                   DecorationExpansion260AuthoringEmitter.TryAuthorGeometry(authoring, placements, in context, region) &&
                   DecorationExpansion300AuthoringEmitter.TryAuthorGeometry(authoring, placements, in context, region) &&
                   DecorationExpansion320AuthoringEmitter.TryAuthorGeometry(authoring, placements, in context, region) &&
                   DecorationExpansion340AuthoringEmitter.TryAuthorGeometry(authoring, placements, in context) &&
                   DecorationExpansion360AuthoringEmitter.TryAuthorGeometry(authoring, placements, in context, region) &&
                   DecorationExpansion380AuthoringEmitter.TryAuthorGeometry(authoring, placements, in context, region) &&
                   DecorationExpansion400AuthoringEmitter.TryAuthorGeometry(authoring, placements, in context) &&
                   GuildSignatureDecorationEmitter.TryAuthorGeometry(authoring, placements, in context, region);
        }
    }
}
