namespace Game.WorldBuilder.Api
{
    /// <summary>
    /// Generation-facing placement intent for one authored NPC. It preserves stable identity and the
    /// resolved-site role the NPC must inhabit without choosing a coordinate or depending on a gameplay
    /// actor implementation. RequiresConversation tells the site/placement backend that the final anchor
    /// must be suitable for player interaction rather than merely existing somewhere inside the site.
    /// </summary>
    public sealed class NpcPlacementPlan
    {
        public NpcRef Npc { get; }
        public SiteRef Site { get; }
        public bool RequiresConversation { get; }

        public NpcPlacementPlan(
            NpcRef npc,
            SiteRef site,
            bool requiresConversation)
        {
            Npc = npc;
            Site = site;
            RequiresConversation = requiresConversation;
        }
    }

    /// <summary>
    /// Post-site-resolution assignment for one NPC. This deliberately stops at concrete generated-site
    /// identity: a later site-realization adapter chooses a physical anchor suitable for the requested
    /// interaction semantics.
    /// </summary>
    public sealed class NpcSiteAssignment
    {
        public NpcRef Npc { get; }
        public SiteRef SiteRole { get; }
        public ResolvedSiteId Site { get; }
        public bool RequiresConversation { get; }

        public NpcSiteAssignment(
            NpcRef npc,
            SiteRef siteRole,
            ResolvedSiteId site,
            bool requiresConversation)
        {
            Npc = npc;
            SiteRole = siteRole;
            Site = site;
            RequiresConversation = requiresConversation;
        }
    }
}
