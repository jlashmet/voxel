using System;
using Game.Composition.Campaign.Content;
using Game.Composition.Kentridge.Api;
using Game.Composition.Kentridge.Runtime;
using Game.Composition.WorldBuilderWorldGen.Runtime;
using Game.Cutscenes.Api;
using Game.WorldBuilder.Api;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Voxel;

namespace Game.Kentridge.PlayableSlice
{
    /// <summary>
    /// Production campaign bootstrap for the shipped Kentridge player. Local Kentridge geometry keeps
    /// the opening-only planner contract so existing hidden-space and rich-building realization remain
    /// exact, while runtime progression/outcome authority comes from the authored full-run hierarchy.
    /// </summary>
    internal sealed class KentridgePlayableFullRunBootstrap
    {
        public AuthoredFullRunCampaignContent Content { get; }
        public KentridgeCampaignGenerationPlan OpeningGeometry { get; }
        public AuthoredFullRunKentridgeComposition Composition { get; }

        private KentridgePlayableFullRunBootstrap(
            AuthoredFullRunCampaignContent content,
            KentridgeCampaignGenerationPlan openingGeometry,
            AuthoredFullRunKentridgeComposition composition)
        {
            Content = content ?? throw new ArgumentNullException(nameof(content));
            OpeningGeometry = openingGeometry ?? throw new ArgumentNullException(nameof(openingGeometry));
            Composition = composition ?? throw new ArgumentNullException(nameof(composition));
        }

        public static KentridgePlayableFullRunBootstrap Compose(
            CutsceneDefinition destinationCutsceneDefinition,
            CutsceneActorId destinationSpeaker,
            uint seed,
            SettlementPlan settlement,
            int voxelsPerDecimetre)
        {
            if (destinationCutsceneDefinition == null)
                throw new ArgumentNullException(nameof(destinationCutsceneDefinition));
            if (settlement == null) throw new ArgumentNullException(nameof(settlement));
            if (voxelsPerDecimetre < 1)
                throw new ArgumentOutOfRangeException(nameof(voxelsPerDecimetre));

            Action<CutsceneAuthoringBuilder, KnownOpeningCampaignRoles> configureDestination =
                (scene, roles) => scene.Bind(destinationSpeaker, roles.DestinationNpc);

            KnownOpeningCampaignContent opening = KnownOpeningCampaignContent.Build(
                destinationCutsceneDefinition,
                configureDestination);
            KentridgeCampaignGenerationPlan openingGeometry =
                Game.Composition.Kentridge.Runtime.KentridgeCampaignSessionBootstrap.Plan(
                    opening.Blueprint,
                    KentridgePlayableWorldBuilderBridge.Resolve(settlement));
            KentridgeCampaignWorldRealization richOpening =
                KentridgeCampaignWorldRealizationBoundary.Realize(
                    openingGeometry,
                    new KentridgeCampaignRealizationFacts(
                        new KentridgeVoxelSiteRealizationFacts(
                            settlement,
                            voxelsPerDecimetre)));

            AuthoredFullRunKentridgeComposition fullRun =
                AuthoredFullRunKentridgeComposition.Build(
                    destinationCutsceneDefinition,
                    seed,
                    settlement.CentreDm,
                    voxelsPerDecimetre,
                    configureDestination,
                    richOpening);

            return new KentridgePlayableFullRunBootstrap(
                fullRun.Content,
                openingGeometry,
                fullRun);
        }
    }
}
