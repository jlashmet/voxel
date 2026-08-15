using System;
using Game.Cutscenes.Api;
using Game.Cutscenes.Content.Kentridge;
using Game.WorldBuilder.Api;

namespace Game.Composition.Campaign.Content
{
    /// <summary>
    /// Production authoring for the opening story facts that are currently known. The first
    /// destination deliberately remains a constraint-matched site, and the destination cutscene
    /// definition is supplied by the caller because its choreography/dialogue has not been recovered.
    /// No placeholder dialogue, destination archetype, NPC name, or world coordinate is invented here.
    /// </summary>
    public sealed class KnownOpeningCampaignContent
    {
        public CampaignBlueprint Blueprint { get; }
        public SiteRef StartingPub { get; }
        public SiteRef FirstDestination { get; }
        public NpcRef Madeline { get; }
        public NpcRef Steven { get; }
        public NpcRef Logan { get; }
        public NpcRef DestinationNpc { get; }
        public ObjectiveRef TravelObjective { get; }
        public CutsceneRef IntroCutscene { get; }
        public CutsceneRef DestinationCutscene { get; }

        private KnownOpeningCampaignContent(
            CampaignBlueprint blueprint,
            SiteRef startingPub,
            SiteRef firstDestination,
            NpcRef madeline,
            NpcRef steven,
            NpcRef logan,
            NpcRef destinationNpc,
            ObjectiveRef travelObjective,
            CutsceneRef introCutscene,
            CutsceneRef destinationCutscene)
        {
            Blueprint = blueprint ?? throw new ArgumentNullException(nameof(blueprint));
            StartingPub = startingPub;
            FirstDestination = firstDestination;
            Madeline = madeline;
            Steven = steven;
            Logan = logan;
            DestinationNpc = destinationNpc;
            TravelObjective = travelObjective;
            IntroCutscene = introCutscene;
            DestinationCutscene = destinationCutscene;
        }

        public static KnownOpeningCampaignContent Build(
            CutsceneDefinition destinationCutsceneDefinition)
        {
            if (destinationCutsceneDefinition == null)
                throw new ArgumentNullException(nameof(destinationCutsceneDefinition));

            var game = Game.WorldBuilder.Api.Campaign.Create("main-campaign");

            SiteRef startingPub = game.World.RequireSite("starting-pub", site => site
                .Archetype(SiteArchetype.Pub)
                .RequireCapability(SiteCapability.Interior)
                .RequireCapability(SiteCapability.PlayerSpawn(4))
                .RequireCapability(SiteCapability.PublicExit));

            // The known story says only that the party goes somewhere else. The generator remains
            // free to choose the concrete site as long as the hard traversal/content needs are met.
            SiteRef firstDestination = game.World.RequireSite("first-destination", site => site
                .DifferentSiteFrom(startingPub)
                .ReachableFrom(startingPub, TraversalProfile.NormalParty));

            NpcRef madeline = game.World.RequireNpc("madeline", npc => npc.PlaceAt(startingPub));
            NpcRef steven = game.World.RequireNpc("steven", npc => npc.PlaceAt(startingPub));
            NpcRef logan = game.World.RequireNpc("logan", npc => npc.PlaceAt(startingPub));
            NpcRef destinationNpc = game.World.RequireNpc("destination-npc", npc => npc
                .PlaceAt(firstDestination)
                .RequireConversation());

            ObjectiveRef travelObjective = game.Story.Objective(
                "travel-to-first-destination",
                objective => objective
                    .Target(firstDestination)
                    .CompleteWhen(ObjectiveCompletion.InteractWith(destinationNpc)));

            CutsceneRef destinationCutscene = game.Story.Cutscene(
                destinationCutsceneDefinition,
                scene => scene.At(firstDestination));
            CutsceneRef introCutscene = game.Story.Cutscene(
                KentridgeOpeningCutscene.Definition,
                scene => scene
                    .At(startingPub)
                    .Bind(KentridgeOpeningCutscene.Lead, CutsceneActorTarget.Player(0))
                    .Bind(KentridgeOpeningCutscene.Madeline, CutsceneActorTarget.Npc(madeline))
                    .Bind(KentridgeOpeningCutscene.Steven, CutsceneActorTarget.Npc(steven))
                    .Bind(KentridgeOpeningCutscene.Logan, CutsceneActorTarget.Npc(logan)));

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
                startingPub,
                firstDestination,
                madeline,
                steven,
                logan,
                destinationNpc,
                travelObjective,
                introCutscene,
                destinationCutscene);
        }
    }
}
