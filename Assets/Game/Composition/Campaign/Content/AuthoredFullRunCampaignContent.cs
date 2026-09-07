using System;
using System.Collections.Generic;
using Game.Composition.Kentridge.Api;
using Game.Cutscenes.Api;
using Game.Encounters.Api;
using Game.Outcomes.Api;
using Game.Quests.Api;
using Game.WorldBuilder.Api;

namespace Game.Composition.Campaign.Content
{
    /// <summary>
    /// Production authored main-campaign composition from the recovered Kentridge opening through one
    /// evidence-backed terminal route. This object exposes stable semantic route identities for
    /// production composition/validation; it owns no runtime chapter, phase, or completion state.
    /// </summary>
    public sealed class AuthoredFullRunCampaignContent
    {
        private readonly QuestDefinition[] _questDefinitions;

        public CampaignBlueprint Blueprint { get; }
        public IReadOnlyList<QuestDefinition> QuestDefinitions => _questDefinitions;

        public KnownOpeningCampaignRoles OpeningRoles { get; }
        public CutsceneRef IntroCutscene { get; }
        public CutsceneRef MedrareToChurchCutscene { get; }
        public QuestRef OptionalWellQuest => KentridgeWellQuestDefinition.Ref;

        public SiteRef Church { get; }
        public NpcRef Angel { get; }
        public SiteRef RorikConflictSite { get; }
        public NpcRef Rorik { get; }
        public SiteRef MoordellDistributionSite { get; }
        public NpcRef MoordellContact { get; }
        public SiteRef RossdamBattleSite { get; }
        public NpcRef RossdamContact { get; }
        public SiteRef KentridgeMayorHouse { get; }
        public NpcRef KentridgeMayor { get; }
        public SiteRef LoganConflictSite { get; }
        public SiteRef LoganCastleLowerSite { get; }

        public ObjectiveRef ChurchObjective { get; }
        public ObjectiveRef RorikObjective { get; }
        public ObjectiveRef MoordellObjective { get; }
        public ObjectiveRef RossdamObjective { get; }
        public ObjectiveRef MayorObjective { get; }

        public CutsceneRef AngelGiveQuestCutscene { get; }
        public CutsceneRef RorikChallengeCutscene { get; }
        public CutsceneRef MoordellDistributionCutscene { get; }
        public CutsceneRef RossdamBattleStartCutscene { get; }
        public CutsceneRef RossdamBattleEndCutscene { get; }
        public CutsceneRef MayorLoganLeadCutscene { get; }
        public CutsceneRef LoganBattleStartCutscene { get; }
        public CutsceneRef LoganBattleEndCutscene { get; }
        public CutsceneRef LoganCastleBattleStartCutscene { get; }
        public CutsceneRef LoganCastleHoleCutscene { get; }

        public EncounterId RorikEncounter => RecoveredCampaignContinuationSlice.RorikEncounter;
        public EncounterId RossdamBattleEncounter => RecoveredCampaignContinuationSlice.RossdamBattleEncounter;
        public EncounterId LoganBattleEncounter => RecoveredCampaignContinuationSlice.LoganBattleEncounter;
        public EncounterId LoganCastleLowerEncounter => RecoveredCampaignContinuationSlice.LoganCastleLowerEncounter;
        public OutcomeConditionRef CompletionCondition => RecoveredCampaignContinuationSlice.CompletionCondition;

        private AuthoredFullRunCampaignContent(
            CampaignBlueprint blueprint,
            KnownOpeningCampaignDraft opening,
            RecoveredCampaignContinuationDraft continuation)
        {
            Blueprint = blueprint ?? throw new ArgumentNullException(nameof(blueprint));
            if (opening == null) throw new ArgumentNullException(nameof(opening));
            if (continuation == null) throw new ArgumentNullException(nameof(continuation));

            _questDefinitions = new[] { KentridgeWellQuestDefinition.Create() };
            OpeningRoles = opening.Roles;
            IntroCutscene = opening.IntroCutscene.Ref;
            MedrareToChurchCutscene = opening.MedrareToChurchCutscene.Ref;

            Church = continuation.Church.Ref;
            Angel = continuation.Angel.Ref;
            RorikConflictSite = continuation.RorikConflictSite.Ref;
            Rorik = continuation.Rorik.Ref;
            MoordellDistributionSite = continuation.MoordellDistributionSite.Ref;
            MoordellContact = continuation.MoordellContact.Ref;
            RossdamBattleSite = continuation.RossdamBattleSite.Ref;
            RossdamContact = continuation.RossdamContact.Ref;
            KentridgeMayorHouse = continuation.KentridgeMayorHouse.Ref;
            KentridgeMayor = continuation.KentridgeMayor.Ref;
            LoganConflictSite = continuation.LoganConflictSite.Ref;
            LoganCastleLowerSite = continuation.LoganCastleLowerSite.Ref;

            ChurchObjective = continuation.ChurchObjective.Ref;
            RorikObjective = continuation.RorikObjective.Ref;
            MoordellObjective = continuation.MoordellObjective.Ref;
            RossdamObjective = continuation.RossdamObjective.Ref;
            MayorObjective = continuation.MayorObjective.Ref;

            AngelGiveQuestCutscene = continuation.AngelGiveQuestCutscene.Ref;
            RorikChallengeCutscene = continuation.RorikChallengeCutscene.Ref;
            MoordellDistributionCutscene = continuation.MoordellDistributionCutscene.Ref;
            RossdamBattleStartCutscene = continuation.RossdamBattleStartCutscene.Ref;
            RossdamBattleEndCutscene = continuation.RossdamBattleEndCutscene.Ref;
            MayorLoganLeadCutscene = continuation.MayorLoganLeadCutscene.Ref;
            LoganBattleStartCutscene = continuation.LoganBattleStartCutscene.Ref;
            LoganBattleEndCutscene = continuation.LoganBattleEndCutscene.Ref;
            LoganCastleBattleStartCutscene = continuation.LoganCastleBattleStartCutscene.Ref;
            LoganCastleHoleCutscene = continuation.LoganCastleHoleCutscene.Ref;
        }

        public static AuthoredFullRunCampaignContent Build(
            CutsceneDefinition destinationCutsceneDefinition,
            Action<CutsceneAuthoringBuilder, KnownOpeningCampaignRoles> configureDestinationCutscene = null)
        {
            var game = Game.WorldBuilder.Api.Campaign.Create("main-campaign");
            KnownOpeningCampaignDraft opening = KnownOpeningCampaignSlice.Compose(
                game,
                destinationCutsceneDefinition,
                configureDestinationCutscene);
            RecoveredCampaignContinuationDraft continuation =
                RecoveredCampaignContinuationSlice.Compose(game, opening);
            return new AuthoredFullRunCampaignContent(game.Build(), opening, continuation);
        }
    }
}
