using System;
using Game.Composition.Kentridge.Api;
using Game.Cutscenes.Api;
using Game.Quests.Api;
using Game.WorldBuilder.Api;

namespace Game.Composition.Campaign.Content
{
    public readonly struct KnownOpeningCampaignRoles
    {
        public RegionHandle KentridgeOverworld { get; }
        public SettlementHandle Kentridge { get; }
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
            RegionHandle kentridgeOverworld,
            SettlementHandle kentridge,
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
            KentridgeOverworld = kentridgeOverworld ?? throw new ArgumentNullException(nameof(kentridgeOverworld));
            Kentridge = kentridge ?? throw new ArgumentNullException(nameof(kentridge));
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
            KnownOpeningCampaignDraft draft)
        {
            Blueprint = blueprint ?? throw new ArgumentNullException(nameof(blueprint));
            if (draft == null) throw new ArgumentNullException(nameof(draft));
            Roles = draft.Roles;
            TravelObjective = draft.TravelObjective.Ref;
            IntroCutscene = draft.IntroCutscene.Ref;
            AwonOpeningCutscene = draft.AwonOpeningCutscene.Ref;
            SeeMedrareCutscene = draft.SeeMedrareCutscene.Ref;
            MedrareJoinCutscene = draft.MedrareJoinCutscene.Ref;
            MedrareFirstSpellCutscene = draft.MedrareFirstSpellCutscene.Ref;
            MedrareToChurchCutscene = draft.MedrareToChurchCutscene.Ref;
            DestinationCutscene = draft.DestinationCutscene.Ref;
        }

        public static KnownOpeningCampaignContent Build(
            CutsceneDefinition destinationCutsceneDefinition,
            Action<CutsceneAuthoringBuilder, KnownOpeningCampaignRoles> configureDestinationCutscene = null)
        {
            var game = Game.WorldBuilder.Api.Campaign.Create("main-campaign");
            KnownOpeningCampaignDraft draft = KnownOpeningCampaignSlice.Compose(
                game,
                destinationCutsceneDefinition,
                configureDestinationCutscene);
            return new KnownOpeningCampaignContent(game.Build(), draft);
        }
    }
}
