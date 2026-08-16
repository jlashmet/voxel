using System;
using Game.Cutscenes.Api;
using Game.Cutscenes.Content.Kentridge;
using Game.WorldBuilder.Api;

namespace Game.Composition.Campaign.Content
{
    /// <summary>
    /// Stable semantic roles already known to participate in the opening. Designer-facing relationships
    /// use unforgeable WorldBuilder handles; compiled/runtime consumers continue to receive stable refs.
    /// </summary>
    public readonly struct KnownOpeningCampaignRoles
    {
        public SiteHandle StartingPub { get; }
        public SiteHandle FirstDestination { get; }
        public NpcHandle Madeline { get; }
        public NpcHandle Steven { get; }
        public NpcHandle Logan { get; }
        public NpcHandle DestinationNpc { get; }

        internal KnownOpeningCampaignRoles(
            SiteHandle startingPub,
            SiteHandle firstDestination,
            NpcHandle madeline,
            NpcHandle steven,
            NpcHandle logan,
            NpcHandle destinationNpc)
        {
            StartingPub = startingPub ?? throw new ArgumentNullException(nameof(startingPub));
            FirstDestination = firstDestination ?? throw new ArgumentNullException(nameof(firstDestination));
            Madeline = madeline ?? throw new ArgumentNullException(nameof(madeline));
            Steven = steven ?? throw new ArgumentNullException(nameof(steven));
            Logan = logan ?? throw new ArgumentNullException(nameof(logan));
            DestinationNpc = destinationNpc ?? throw new ArgumentNullException(nameof(destinationNpc));
        }
    }

    /// <summary>
    /// Production authoring for the opening story facts that are currently known. The recovered world
    /// catalog remains the source of semantic ids, but this opening blueprint intentionally authors only
    /// the Kentridge overworld + Kentridge settlement because the current production generator is a
    /// single-region/single-settlement vertical-slice generator. The first destination deliberately
    /// remains a constraint-matched Kentridge-overworld site until its recovered semantic role is resolved.
    /// </summary>
    public sealed class KnownOpeningCampaignContent
    {
        public CampaignBlueprint Blueprint { get; }
        public KnownOpeningCampaignRoles Roles { get; }
        public SiteRef StartingPub => Roles.StartingPub.Ref;
        public SiteRef FirstDestination => Roles.FirstDestination.Ref;
        public NpcRef Madeline => Roles.Madeline.Ref;
        public NpcRef Steven => Roles.Steven.Ref;
        public NpcRef Logan => Roles.Logan.Ref;
        public NpcRef DestinationNpc => Roles.DestinationNpc.Ref;
        public ObjectiveRef TravelObjective { get; }
        public CutsceneRef IntroCutscene { get; }
        public CutsceneRef DestinationCutscene { get; }

        private KnownOpeningCampaignContent(
            CampaignBlueprint blueprint,
            KnownOpeningCampaignRoles roles,
            ObjectiveRef travelObjective,
            CutsceneRef introCutscene,
            CutsceneRef destinationCutscene)
        {
            Blueprint = blueprint ?? throw new ArgumentNullException(nameof(blueprint));
            Roles = roles;
            TravelObjective = travelObjective;
            IntroCutscene = introCutscene;
            DestinationCutscene = destinationCutscene;
        }

        public static KnownOpeningCampaignContent Build(
            CutsceneDefinition destinationCutsceneDefinition,
            Action<CutsceneAuthoringBuilder, KnownOpeningCampaignRoles> configureDestinationCutscene = null)
        {
            if (destinationCutsceneDefinition == null)
                throw new ArgumentNullException(nameof(destinationCutsceneDefinition));
            if (destinationCutsceneDefinition.RequiredActors.Count > 0
                && configureDestinationCutscene == null)
                throw new ArgumentException(
                    "Destination cutscene '" + destinationCutsceneDefinition.Id +
                    "' requires actor bindings; supply configureDestinationCutscene so campaign roles " +
                    "can be mapped to the cutscene's semantic actor ids.",
                    nameof(configureDestinationCutscene));

            var game = Game.WorldBuilder.Api.Campaign.Create("main-campaign");

            RecoveredOverworldRegionDefinition recoveredKentridge =
                RecoveredMountingForceWorldCatalog.Kentridge;
            RegionHandle kentridgeOverworld = game.World.Region(recoveredKentridge.RegionId);
            SettlementHandle kentridge = kentridgeOverworld.Settlement(
                recoveredKentridge.SettlementId,
                recoveredKentridge.SettlementArchetype);

            SiteHandle startingPub = kentridge.Pub(
                    "starting-pub",
                    site => site.RequireCapability(SiteCapability.PlayerSpawn(4)))
                .LegacyMap("mounting-force", "kentridge-pub");

            // The known story says only that the party goes somewhere else in the surrounding region.
            // The generator remains free to choose the concrete site as long as the hard
            // traversal/content needs are met; it is not forced into the starting settlement.
            SiteHandle firstDestination = kentridgeOverworld.Site(
                "first-destination",
                site => site
                    .DifferentSiteFrom(startingPub)
                    .ReachableFrom(startingPub, TraversalProfile.NormalParty));

            NpcHandle madeline = startingPub.Npc("madeline");
            NpcHandle steven = startingPub.Npc("steven");
            NpcHandle logan = startingPub.Npc("logan");
            NpcHandle destinationNpc = firstDestination.Npc(
                "destination-npc",
                npc => npc.RequireConversation());

            var roles = new KnownOpeningCampaignRoles(
                startingPub,
                firstDestination,
                madeline,
                steven,
                logan,
                destinationNpc);

            ObjectiveHandle travelObjective = firstDestination.Objective(
                "travel-to-first-destination",
                objective => objective.CompleteWhen(ObjectiveCompletion.InteractWith(destinationNpc)));

            CutsceneHandle destinationCutscene = firstDestination.Cutscene(
                destinationCutsceneDefinition,
                scene => configureDestinationCutscene?.Invoke(scene, roles));

            CutsceneHandle introCutscene = startingPub.Cutscene(
                KentridgeOpeningCutscene.Definition,
                scene => scene
                    .Bind(KentridgeOpeningCutscene.Lead, PlayerSlot.First)
                    .Bind(KentridgeOpeningCutscene.Madeline, madeline)
                    .Bind(KentridgeOpeningCutscene.Steven, steven)
                    .Bind(KentridgeOpeningCutscene.Logan, logan));

            game.Story.Rule("start-intro", rule => rule
                .When(StoryTrigger.NewGame())
                .Then(StoryEffect.PlayCutscene(introCutscene)));
            game.Story.Rule("start-travel-after-intro", rule => rule
                .When(StoryTrigger.CutsceneCompleted(introCutscene))
                .Then(StoryEffect.StartObjective(travelObjective)));
            game.Story.Rule("destination-conversation-trigger", rule => rule
                .When(StoryTrigger.InteractWith(destinationNpc))
                .If(StoryCondition.ObjectiveActive(travelObjective))
                .If(StoryCondition.CutsceneNotCompleted(destinationCutscene))
                .Then(StoryEffect.PlayCutscene(destinationCutscene)));

            return new KnownOpeningCampaignContent(
                game.Build(),
                roles,
                travelObjective,
                introCutscene,
                destinationCutscene);
        }
    }
}
