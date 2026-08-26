using System;
using MountingForce.WorldGen;

namespace Game.Composition.WorldBuilderWorldGen.Runtime
{
    /// <summary>
    /// Integration-owned handoff for exact post-generation Kentridge placement facts. Game-facing
    /// composition depends on this bundle rather than on the concrete world-generation backend
    /// contracts; the WorldBuilder/backend integration layer remains the only place that unwraps them.
    /// </summary>
    public sealed class KentridgeCampaignRealizationFacts
    {
        internal ISettlementSiteRealizationFacts SiteFacts { get; }
        internal IHiddenSpaceRealizationFacts HiddenSpaceFacts { get; }

        public KentridgeCampaignRealizationFacts(
            ISettlementSiteRealizationFacts siteFacts,
            IHiddenSpaceRealizationFacts hiddenSpaceFacts = null)
        {
            SiteFacts = siteFacts ?? throw new ArgumentNullException(nameof(siteFacts));
            HiddenSpaceFacts = hiddenSpaceFacts;
        }
    }

    /// <summary>
    /// Keeps backend fact unwrapping inside the explicit WorldBuilder/world-generation integration
    /// assembly so higher-level Kentridge composition never names the legacy backend contracts.
    /// </summary>
    public static class KentridgeCampaignWorldRealizationBoundary
    {
        public static KentridgeCampaignWorldRealization Realize(
            KentridgeCampaignGenerationPlan generation,
            KentridgeCampaignRealizationFacts facts)
        {
            if (generation == null) throw new ArgumentNullException(nameof(generation));
            if (facts == null) throw new ArgumentNullException(nameof(facts));

            return KentridgeCampaignWorldRealizer.Realize(
                generation,
                facts.SiteFacts,
                facts.HiddenSpaceFacts);
        }
    }
}
