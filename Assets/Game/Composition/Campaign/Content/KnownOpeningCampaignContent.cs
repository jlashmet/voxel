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
        public SiteHandle MedrareHouseSite { get; }
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
            SiteHandle medrareHouseSite,
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
            MedrareHouseSite = medrareHouseSite ?? throw new ArgumentNullException(nameof(medrareHouseSite));
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
        public SiteRef MedrareHouseSite => Roles.MedrareHouseSite.Ref;
        public NpcRef Madeline => Roles.Madeline.Ref;
        public NpcRef Steven => Roles.Steven.Ref;
        public NpcRef Logan => Roles.Logan.Ref;
        public NpcRef DestinationNpc => Roles.DestinationNpc.Ref;
        public NpcRef Awon => Roles.Awon.Ref;
        public NpcRef Medrare => Roles.Medrare.Ref;
        public ObjectiveRef TravelObjective { get; }
        public CutsceneRef IntroCutscene { get; }
        public CutsceneRef AwonOpeningCutscene { get; }
        public CutsceneRef SeeMedrareCutscene { get; }
        public CutsceneRef MedrareJoinCutscene { get; }
        public CutsceneRef MedrareFirstSpellCutscene { get; }
        public CutsceneRef MedrareToChurchCutscene { get; }
        public CutsceneRef DestinationCutscene { get; }
        public QuestRef WellQuest => KentridgeWellQuestDefinition.Ref;

        private KnownOpeningCampaignContent(
            CampaignBlueprint blueprint,
            KnownOpeningCampaignRoles roles,
            ObjectiveRef travelObjective,
            CutsceneRef introCutscene,
            CutsceneRef awonOpeningCutscene,
            CutsceneRef seeMedrareCutscene,
            CutsceneRef medrareJoinCutscene,
            CutsceneRef medrareFirstSpellCutscene,
            CutsceneRef medrareToChurchCutscene,
            CutsceneRef destinationCutscene)
        {
            Blueprint = blueprint ?? throw new ArgumentNullException(nameof(blueprint));
            Roles = roles;
            TravelObjective = travelObjective;
            IntroCutscene = introCutscene;
            AwonOpeningCutscene = awonOpeningCutscene;
            SeeMedrareCutscene = seeMedrareCutscene;
            MedrareJoinCutscene = medrareJoinCutscene;
            MedrareFirstSpellCutscene = medrareFirstSpellCutscene;
            MedrareToChurchCutscene = medrareToChurchCutscene;
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
            SettlementHandle kentridge = kentridgeOverworld.Settlement(
                recoveredKentridge.SettlementId,
                recoveredKentridge.SettlementArchetype);

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
                "kentridge-medrare-encounter",
                site => site.DifferentSiteFrom(startingPub).ReachableFrom(startingPub, TraversalProfile.NormalParty));
            SiteHandle medrareHouseSite = kentridgeOverworld.Site(
                "medrare-house-lower",
                site => site
                    .DifferentSiteFrom(medrareSite)
                    .ReachableFrom(startingPub, TraversalProfile.NormalParty)
                    .ReachableFrom(medrareSite, TraversalProfile.NormalParty));

            NpcHandle madeline = startingPub.Npc("madeline");
            NpcHandle steven = startingPub.Npc("steven");
            NpcHandle logan = startingPub.Npc("logan");
            NpcHandle destinationNpc = firstDestination.Npc("destination-npc", npc => npc.RequireConversation());
            NpcHandle awon = awonSite.Npc("awon", npc => npc.RequireConversation());
            NpcHandle medrare = medrareSite.Npc("medrare", npc => npc.RequireConversation());

            var roles = new KnownOpeningCampaignRoles(
                startingPub,
                firstDestination,
                awonSite,
                medrareSite,
                medrareHouseSite,
                madeline,
                steven,
                logan,
                destinationNpc,
                awon,
                medrare);

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
            CutsceneHandle awonOpening = awonSite.Cutscene(
                KentridgeOpeningProgressionCutscenes.AwonDefinition,
                scene => scene
                    .Bind(KentridgeOpeningProgressionCutscenes.Weldon, PlayerSlot.First)
                    .Bind(KentridgeOpeningProgressionCutscenes.Awon, awon)
                    .Bind(KentridgeOpeningProgressionCutscenes.Steven, steven)
                    .Bind(KentridgeOpeningProgressionCutscenes.Madeline, madeline)
                    .Bind(KentridgeOpeningProgressionCutscenes.Logan, logan));
            CutsceneHandle seeMedrare = medrareSite.Cutscene(
                KentridgeOpeningProgressionCutscenes.SeeMedrareDefinition);
            CutsceneHandle medrareJoin = medrareSite.Cutscene(
                KentridgeOpeningProgressionCutscenes.MedrareJoinDefinition,
                scene => scene
                    .Bind(KentridgeOpeningProgressionCutscenes.Weldon, PlayerSlot.First)
                    .Bind(KentridgeOpeningProgressionCutscenes.Medrare, medrare));
            CutsceneHandle medrareFirstSpell = medrareHouseSite.Cutscene(
                KentridgeOpeningProgressionCutscenes.MedrareFirstSpellDefinition);
            CutsceneHandle medrareToChurch = medrareHouseSite.Cutscene(
                KentridgeOpeningProgressionCutscenes.MedrareToChurchDefinition);

            game.Story.Rule("start-intro", rule => rule
                .When(StoryTrigger.NewGame())
                .Then(StoryEffect.PlayCutscene(introCutscene)));
            game.Story.Rule("start-well-quest", rule => rule
                .When(StoryTrigger.NewGame())
                .Then(StoryEffect.StartQuest(KentridgeWellQuestDefinition.Ref)));
            game.Story.Rule("start-travel-after-intro", rule => rule
                .When(StoryTrigger.CutsceneCompleted(introCutscene))
                .Then(StoryEffect.StartObjective(travelObjective)));
            game.Story.Rule("awon-opening-on-visit", rule => rule
                .When(StoryTrigger.InteractWith(awon))
                .If(StoryCondition.CutsceneCompleted(introCutscene))
                .If(StoryCondition.CutsceneNotCompleted(awonOpening))
                .Then(StoryEffect.PlayCutscene(awonOpening)));
            game.Story.Rule("see-medrare-after-awon", rule => rule
                .When(StoryTrigger.EnterSiteProximity(medrareSite))
                .If(StoryCondition.CutsceneCompleted(awonOpening))
                .If(StoryCondition.CutsceneNotCompleted(seeMedrare))
                .Then(StoryEffect.PlayCutscene(seeMedrare)));
            game.Story.Rule("medrare-join-after-awon", rule => rule
                .When(StoryTrigger.InteractWith(medrare))
                .If(StoryCondition.CutsceneCompleted(awonOpening))
                .If(StoryCondition.CutsceneNotCompleted(medrareJoin))
                .Then(StoryEffect.PlayCutscene(medrareJoin)));
            game.Story.Rule("persist-medrare-join", rule => rule
                .When(StoryTrigger.CutsceneCompleted(medrareJoin))
                .Then(StoryEffect.JoinPartyMember("Medrare")));
            game.Story.Rule("medrare-first-spell-after-awon", rule => rule
                .When(StoryTrigger.EnterSiteProximity(medrareHouseSite))
                .If(StoryCondition.CutsceneCompleted(awonOpening))
                .If(StoryCondition.CutsceneNotCompleted(medrareFirstSpell))
                .Then(StoryEffect.PlayCutscene(medrareFirstSpell)));
            game.Story.Rule("grant-flame-after-first-spell", rule => rule
                .When(StoryTrigger.CutsceneCompleted(medrareFirstSpell))
                .Then(StoryEffect.GrantSpell("Flame")));
            game.Story.Rule("medrare-to-church-after-first-spell", rule => rule
                .When(StoryTrigger.CutsceneCompleted(medrareFirstSpell))
                .If(StoryCondition.CutsceneNotCompleted(medrareToChurch))
                .Then(StoryEffect.PlayCutscene(medrareToChurch)));
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
                awonOpening,
                seeMedrare,
                medrareJoin,
                medrareFirstSpell,
                medrareToChurch,
                destinationCutscene);
        }
    }
}
