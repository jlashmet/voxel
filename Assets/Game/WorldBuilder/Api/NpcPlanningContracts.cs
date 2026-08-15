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
}
