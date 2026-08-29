using System;
using Game.Story.Api;
using Game.Story.Runtime;
using Game.WorldBuilder.Api;

namespace Game.Composition.Campaign.Runtime
{
    /// <summary>
    /// Additional semantic event entry points for CampaignRuntime. Spatial systems decide that an
    /// authored boundary was crossed; CampaignRuntime remains responsible for applying resulting
    /// story effects through its existing state/effect interfaces.
    /// </summary>
    public static class CampaignRuntimeStoryEvents
    {
        public static int EnterSiteProximity(
            this CampaignRuntime runtime,
            CampaignBlueprint blueprint,
            SiteRef site)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (blueprint == null) throw new ArgumentNullException(nameof(blueprint));

            return StoryRuleEngine.Dispatch(
                blueprint.StoryRules,
                StoryEvent.SiteProximityEntered(site),
                runtime,
                (IStoryEffectSink)runtime);
        }
    }
}
