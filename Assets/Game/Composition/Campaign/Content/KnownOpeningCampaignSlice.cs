using System;
using Game.Composition.Kentridge.Api;
using Game.Cutscenes.Api;
using Game.Cutscenes.Content.Kentridge;
using Game.WorldBuilder.Api;

namespace Game.Composition.Campaign.Content
{
    /// <summary>
    /// Mutable authoring handles for the existing opening while a CampaignBuilder is still open.
    /// This is composition data only; runtime progression remains owned by Story/Progression.
    /// </summary>
    internal sealed class KnownOpeningCampaignDraft
    {
        public KnownOpeningCampaignRoles Roles { get; }
        public ObjectiveHandle TravelObjective { get; }
        public CutsceneHandle IntroCutscene { get; }
        public CutsceneHandle AwonOpeningCutscene { get; }
        public CutsceneHandle SeeMedrareCutscene { get; }
        public CutsceneHandle MedrareJoinCutscene { get; }
        public CutsceneHandle MedrareFirstSpellCutscene { get; }
        public CutsceneHandle MedrareToChurchCutscene { get; }
        public CutsceneHandle DestinationCutscene { get; }

        public KnownOpeningCampaignDraft(
            KnownOpeningCampaignRoles roles,
            ObjectiveHandle travelObjective,
            CutsceneHandle introCutscene,
            CutsceneHandle awonOpeningCutscene,
            CutsceneHandle seeMedrareCutscene,
            CutsceneHandle medrareJoinCutscene,
            CutsceneHandle medrareFirstSpellCutscene,
            CutsceneHandle medrareToChurchCutscene,
            CutsceneHandle destinationCutscene)
        {
            Roles = roles;
            TravelObjective = travelObjective ?? throw new ArgumentNullException(nameof(travelObjective));
            IntroCutscene = introCutscene ?? throw new ArgumentNullException(nameof(introCutscene));
            AwonOpeningCutscene = awonOpeningCutscene ?? throw new ArgumentNullException(nameof(awonOpeningCutscene));
            SeeMedrareCutscene = seeMedrareCutscene ?? throw new ArgumentNullException(nameof(seeMedrareCutscene));
            MedrareJoinCutscene = medrareJoinCutscene ?? throw new ArgumentNullException(nameof(medrareJoinCutscene));
            MedrareFirstSpellCutscene = medrareFirstSpellCutscene ?? throw new ArgumentNullException(nameof(medrareFirstSpellCutscene));
            MedrareToChurchCutscene = medrareToChurchCutscene ?? throw new ArgumentNullException(nameof(medrareToChurchCutscene));
            DestinationCutscene = destinationCutscene ?? throw new ArgumentNullException(nameof(destinationCutscene));
        }
    }

    /// <summary>
    /// Plain authored opening slice. Functions separate world roles, objectives, cutscenes and Story
    /// rules without introducing a chapter/phase runtime abstraction.
    /// </summary>
    internal static class KnownOpeningCampaignSlice
    {
        public static KnownOpeningCampaignDraft Compose(
            CampaignBuilder game,
            CutsceneDefinition destinationCutsceneDefinition,
            Action<CutsceneAuthoringBuilder, KnownOpeningCampaignRoles> configureDestinationCutscene = null)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            ValidateDestination(destinationCutsceneDefinition, configureDestinationCutscene);

            KnownOpeningCampaignRoles roles = ComposeWorldAndRoles(game);
            ObjectiveHandle travelObjective = ComposeObjectives(roles);
            ComposeCutscenes(
                roles,
                destinationCutsceneDefinition,
                configureDestinationCutscene,
                out CutsceneHandle destinationCutscene,
                out CutsceneHandle introCutscene,
                out CutsceneHandle awonOpening,
                out CutsceneHandle seeMedrare,
                out CutsceneHandle medrareJoin,
                out CutsceneHandle medrareFirstSpell,
                out CutsceneHandle medrareToChurch);
            ComposeRules(
                game,
                roles,
                travelObjective,
                destinationCutscene,
                introCutscene,
                awonOpening,
                seeMedrare,
                medrareJoin,
                medrareFirstSpell,
                medrareToChurch);

            return new KnownOpeningCampaignDraft(
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

        private static KnownOpeningCampaignRoles ComposeWorldAndRoles(CampaignBuilder game)
        {
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

            return new KnownOpeningCampaignRoles(
                kentridgeOverworld,
                kentridge,
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
        }

        private static ObjectiveHandle ComposeObjectives(KnownOpeningCampaignRoles roles) =>
            roles.FirstDestination.Objective(
                "travel-to-first-destination",
                objective => objective.CompleteWhen(ObjectiveCompletion.InteractWith(roles.DestinationNpc)));

        private static void ComposeCutscenes(
            KnownOpeningCampaignRoles roles,
            CutsceneDefinition destinationCutsceneDefinition,
            Action<CutsceneAuthoringBuilder, KnownOpeningCampaignRoles> configureDestinationCutscene,
            out CutsceneHandle destinationCutscene,
            out CutsceneHandle introCutscene,
            out CutsceneHandle awonOpening,
            out CutsceneHandle seeMedrare,
            out CutsceneHandle medrareJoin,
            out CutsceneHandle medrareFirstSpell,
            out CutsceneHandle medrareToChurch)
        {
            destinationCutscene = roles.FirstDestination.Cutscene(
                destinationCutsceneDefinition,
                scene => configureDestinationCutscene?.Invoke(scene, roles));
            introCutscene = roles.StartingPub.Cutscene(
                KentridgeOpeningCutscene.Definition,
                scene => scene
                    .Bind(KentridgeOpeningCutscene.Lead, PlayerSlot.First)
                    .Bind(KentridgeOpeningCutscene.Madeline, roles.Madeline)
                    .Bind(KentridgeOpeningCutscene.Steven, roles.Steven)
                    .Bind(KentridgeOpeningCutscene.Logan, roles.Logan));
            awonOpening = roles.AwonSite.Cutscene(
                KentridgeOpeningProgressionCutscenes.AwonDefinition,
                scene => scene
                    .Bind(KentridgeOpeningProgressionCutscenes.Weldon, PlayerSlot.First)
                    .Bind(KentridgeOpeningProgressionCutscenes.Awon, roles.Awon)
                    .Bind(KentridgeOpeningProgressionCutscenes.Steven, roles.Steven)
                    .Bind(KentridgeOpeningProgressionCutscenes.Madeline, roles.Madeline)
                    .Bind(KentridgeOpeningProgressionCutscenes.Logan, roles.Logan));
            seeMedrare = roles.MedrareSite.Cutscene(
                KentridgeOpeningProgressionCutscenes.SeeMedrareDefinition);
            medrareJoin = roles.MedrareSite.Cutscene(
                KentridgeOpeningProgressionCutscenes.MedrareJoinDefinition,
                scene => scene
                    .Bind(KentridgeOpeningProgressionCutscenes.Weldon, PlayerSlot.First)
                    .Bind(KentridgeOpeningProgressionCutscenes.Medrare, roles.Medrare));
            medrareFirstSpell = roles.MedrareHouseSite.Cutscene(
                KentridgeOpeningProgressionCutscenes.MedrareFirstSpellDefinition);
            medrareToChurch = roles.MedrareHouseSite.Cutscene(
                KentridgeOpeningProgressionCutscenes.MedrareToChurchDefinition);
        }

        private static void ComposeRules(
            CampaignBuilder game,
            KnownOpeningCampaignRoles roles,
            ObjectiveHandle travelObjective,
            CutsceneHandle destinationCutscene,
            CutsceneHandle introCutscene,
            CutsceneHandle awonOpening,
            CutsceneHandle seeMedrare,
            CutsceneHandle medrareJoin,
            CutsceneHandle medrareFirstSpell,
            CutsceneHandle medrareToChurch)
        {
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
                .When(StoryTrigger.InteractWith(roles.Awon))
                .If(StoryCondition.CutsceneCompleted(introCutscene))
                .If(StoryCondition.CutsceneNotCompleted(awonOpening))
                .Then(StoryEffect.PlayCutscene(awonOpening)));
            game.Story.Rule("see-medrare-after-awon", rule => rule
                .When(StoryTrigger.EnterSiteProximity(roles.MedrareSite))
                .If(StoryCondition.CutsceneCompleted(awonOpening))
                .If(StoryCondition.CutsceneNotCompleted(seeMedrare))
                .Then(StoryEffect.PlayCutscene(seeMedrare)));
            game.Story.Rule("medrare-join-after-awon", rule => rule
                .When(StoryTrigger.InteractWith(roles.Medrare))
                .If(StoryCondition.CutsceneCompleted(awonOpening))
                .If(StoryCondition.CutsceneNotCompleted(medrareJoin))
                .Then(StoryEffect.PlayCutscene(medrareJoin)));
            game.Story.Rule("persist-medrare-join", rule => rule
                .When(StoryTrigger.CutsceneCompleted(medrareJoin))
                .Then(StoryEffect.JoinPartyMember("Medrare")));
            game.Story.Rule("medrare-first-spell-after-awon", rule => rule
                .When(StoryTrigger.EnterSiteProximity(roles.MedrareHouseSite))
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
                .When(StoryTrigger.InteractWith(roles.DestinationNpc))
                .If(StoryCondition.ObjectiveActive(travelObjective))
                .If(StoryCondition.CutsceneNotCompleted(destinationCutscene))
                .Then(StoryEffect.PlayCutscene(destinationCutscene)));
        }

        private static void ValidateDestination(
            CutsceneDefinition destinationCutsceneDefinition,
            Action<CutsceneAuthoringBuilder, KnownOpeningCampaignRoles> configureDestinationCutscene)
        {
            if (destinationCutsceneDefinition == null)
                throw new ArgumentNullException(nameof(destinationCutsceneDefinition));
            if (destinationCutsceneDefinition.RequiredActors.Count > 0 && configureDestinationCutscene == null)
                throw new ArgumentException(
                    "Destination cutscene '" + destinationCutsceneDefinition.Id +
                    "' requires actor bindings; supply configureDestinationCutscene so campaign roles can be mapped to the cutscene's semantic actor ids.",
                    nameof(configureDestinationCutscene));
        }
    }
}
