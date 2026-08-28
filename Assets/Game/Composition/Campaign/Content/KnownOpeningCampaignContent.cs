using System;
using Game.Composition.Kentridge.Api;
using Game.Cutscenes.Api;
using Game.Cutscenes.Content.Kentridge;
using Game.Quests.Api;
using Game.WorldBuilder.Api;

namespace Game.Composition.Campaign.Content
{
    public readonly struct KnownOpeningCampaignRoles
    {
        public SiteHandle StartingPub { get; }
        public SiteHandle FirstDestination { get; }
        public SiteHandle AwonSite { get; }
        public SiteHandle MedrareSite { get; }
        public NpcHandle Madeline { get; }
        public NpcHandle Steven { get; }
        public NpcHandle Logan { get; }
        public NpcHandle DestinationNpc { get; }
        public NpcHandle Awon { get; }
        public NpcHandle Medrare { get; }

        internal KnownOpeningCampaignRoles(
            SiteHandle startingPub,
            SiteHandle firstDestination,
            SiteHandle awonSite,
            SiteHandle medrareSite,
            NpcHandle madeline,
            NpcHandle steven,
            NpcHandle logan,
            NpcHandle destinationNpc,
            NpcHandle awon,
            NpcHandle medrare)
        {
            StartingPub = startingPub ?? throw new ArgumentNullException(nameof(startingPub));
            FirstDestination = firstDestination ?? throw new ArgumentNullException(nameof(firstDestination));
            AwonSite = awonSite ?? throw new ArgumentNullException(nameof(awonSite));
            MedrareSite = medrareSite ?? throw new ArgumentNullException(nameof(medrareSite));
            Madeline = madeline ?? throw new ArgumentNullException(nameof(madeline));
            Steven = steven ?? throw new ArgumentNullException(nameof(steven));
            Logan = logan ?? throw new ArgumentNullException(nameof(logan));
            DestinationNpc = destinationNpc ?? throw new ArgumentNullException(nameof(destinationNpc));
            Awon = awon ?? throw new ArgumentNullException(nameof(awon));
            Medrare = medrare ?? throw new ArgumentNullException(nameof(medrare));
        }
    }

    public sealed class KnownOpeningCampaignContent
    {
        public CampaignBlueprint Blueprint { get; }
        public KnownOpeningCampaignRoles Roles { get; }
        public SiteRef StartingPub => Roles.StartingPub.Ref;
        public SiteRef FirstDestination => Roles.FirstDestination.Ref;
        public SiteRef AwonSite => Roles.AwonSite.Ref;
        public SiteRef MedrareSite => Roles.MedrareSite.Ref;
        public NpcRef Madeline => Roles.Madeline.Ref;
        public NpcRef Steven => Roles.Steven.Ref;
        public NpcRef Logan => Roles.Logan.Ref;
        public NpcRef DestinationNpc => Roles.DestinationNpc.Ref;
        public NpcRef Awon => Roles.Awon.Ref;
        public NpcRef Medrare => Roles.Medrare.Ref;
        public ObjectiveRef TravelObjective { get; }
        public CutsceneRef IntroCutscene { get; }
        public CutsceneRef LoganOpeningCutscene { get; }
        public CutsceneRef AwonOpeningCutscene { get; }
        public CutsceneRef MedrareOpeningCutscene { get; }
        public CutsceneRef DestinationCutscene { get; }
        public QuestRef WellQuest => KentridgeWellQuestDefinition.Ref;

        private KnownOpeningCampaignContent(
            CampaignBlueprint blueprint,
            KnownOpeningCampaignRoles roles,
            ObjectiveRef travelObjective,
            CutsceneRef introCutscene,
            CutsceneRef loganOpeningCutscene,
            CutsceneRef awonOpeningCutscene,
            CutsceneRef medrareOpeningCutscene,
            CutsceneRef destinationCutscene)
        {
            Blueprint = blueprint ?? throw new ArgumentNullException(nameof(blueprint));
            Roles = roles;
            TravelObjective = travelObjective;
            IntroCutscene = introCutscene;
            LoganOpeningCutscene = loganOpeningCutscene;
            AwonOpeningCutscene = awonOpeningCutscene;
            MedrareOpeningCutscene = medrareOpeningCutscene;
            DestinationCutscene = destinationCutscene;
        }

        public static KnownOpeningCampaignContent Build(
            CutsceneDefinition destinationCutsceneDefinition,
            Action<CutsceneAuthoringBuilder, KnownOpeningCampaignRoles> configureDestinationCutscene = null)
        {
            if (destinationCutsceneDefinition == null)
                throw new ArgumentNullException(nameof(destinationCutsceneDefinition));
            if (destinationCutsceneDefinition.RequiredActors.Count > 0 && configureDestinationCutscene == null)
                throw new ArgumentException(
                    "Destination cutscene '" + destinationCutsceneDefinition.Id +
                    "' requires actor bindings; supply configureDestinationCutscene so campaign roles can be mapped to the cutscene's semantic actor ids.",
                    nameof(configureDestinationCutscene));

            var game = Game.WorldBuilder.Api.Campaign.Create("main-campaign");
            RecoveredOverworldRegionDefinition recoveredKentridge = RecoveredMountingForceWorldCatalog.Kentridge;
            RegionHandle kentridgeOverworld = game.World.Region(recoveredKentridge.RegionId);
            SettlementHandle kentridge = kentridgeOverworld.Settlement(recoveredKentridge.SettlementId, recoveredKentridge.SettlementArchetype);

            SiteHandle startingPub = kentridge.Pub(
                    "starting-pub",
                    site => site.RequireCapability(SiteCapability.PlayerSpawn(4)))
                .LegacyMap("mounting-force", "kentridge-pub");

            SiteHandle firstDestination = kentridgeOverworld.Site(
                "first-destination",
                site => site.DifferentSiteFrom(startingPub).ReachableFrom(startingPub, TraversalProfile.NormalParty));
            SiteHandle awonSite = kentridgeOverworld.Site(
                "awon-the-immortal",
                site => site.DifferentSiteFrom(startingPub).ReachableFrom(startingPub, TraversalProfile.NormalParty));
            SiteHandle medrareSite = kentridgeOverworld.Site(
                "medrare",
                site => site.DifferentSiteFrom(startingPub).ReachableFrom(startingPub, TraversalProfile.NormalParty));

            NpcHandle madeline = startingPub.Npc("madeline");
            NpcHandle steven = startingPub.Npc("steven");
            NpcHandle logan = startingPub.Npc("logan");
            NpcHandle destinationNpc = firstDestination.Npc("destination-npc", npc => npc.RequireConversation());
            NpcHandle awon = awonSite.Npc("awon", npc => npc.RequireConversation());
            NpcHandle medrare = medrareSite.Npc("medrare", npc => npc.RequireConversation());

            var roles = new KnownOpeningCampaignRoles(
                startingPub, firstDestination, awonSite, medrareSite,
                madeline, steven, logan, destinationNpc, awon, medrare);

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

            CutsceneHandle loganOpening = startingPub.Cutscene(
                KentridgeOpeningProgressionCutscenes.LoganDefinition,
                scene => scene.Bind(KentridgeOpeningProgressionCutscenes.Logan, logan));
            CutsceneHandle awonOpening = awonSite.Cutscene(
                KentridgeOpeningProgressionCutscenes.AwonDefinition,
                scene => scene.Bind(KentridgeOpeningProgressionCutscenes.Awon, awon));
            CutsceneHandle medrareOpening = medrareSite.Cutscene(
                KentridgeOpeningProgressionCutscenes.MedrareDefinition,
                scene => scene.Bind(KentridgeOpeningProgressionCutscenes.Medrare, medrare));

            game.Story.Rule("start-intro", rule => rule
                .When(StoryTrigger.NewGame())
                .Then(StoryEffect.PlayCutscene(introCutscene)));
            game.Story.Rule("start-well-quest", rule => rule
                .When(StoryTrigger.NewGame())
                .Then(StoryEffect.StartQuest(KentridgeWellQuestDefinition.Ref)));
            game.Story.Rule("logan-opening-after-pub", rule => rule
                .When(StoryTrigger.CutsceneCompleted(introCutscene))
                .If(StoryCondition.CutsceneNotCompleted(loganOpening))
                .Then(StoryEffect.PlayCutscene(loganOpening)));
            game.Story.Rule("start-travel-after-logan", rule => rule
                .When(StoryTrigger.CutsceneCompleted(loganOpening))
                .Then(StoryEffect.StartObjective(travelObjective)));
            game.Story.Rule("awon-opening-on-entry", rule => rule
                .When(StoryTrigger.EnterSite(awonSite))
                .If(StoryCondition.CutsceneNotCompleted(awonOpening))
                .Then(StoryEffect.PlayCutscene(awonOpening)));
            game.Story.Rule("medrare-opening-on-entry", rule => rule
                .When(StoryTrigger.EnterSite(medrareSite))
                .If(StoryCondition.CutsceneNotCompleted(medrareOpening))
                .Then(StoryEffect.PlayCutscene(medrareOpening)));
            game.Story.Rule("destination-conversation-trigger", rule => rule
                .When(StoryTrigger.InteractWith(destinationNpc))
                .If(StoryCondition.ObjectiveActive(travelObjective))
                .If(StoryCondition.CutsceneNotCompleted(destinationCutscene))
                .Then(StoryEffect.PlayCutscene(destinationCutscene)));

            return new KnownOpeningCampaignContent(
                game.Build(), roles, travelObjective, introCutscene,
                loganOpening, awonOpening, medrareOpening, destinationCutscene);
        }
    }
}
