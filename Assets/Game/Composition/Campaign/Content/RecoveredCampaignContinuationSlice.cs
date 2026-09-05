using System;
using Game.Cutscenes.Api;
using Game.Encounters.Api;
using Game.Outcomes.Api;
using Game.WorldBuilder.Api;

namespace Game.Composition.Campaign.Content
{
    internal sealed class RecoveredCampaignContinuationDraft
    {
        public SiteHandle Church { get; }
        public NpcHandle Angel { get; }
        public SiteHandle RorikConflictSite { get; }
        public NpcHandle Rorik { get; }
        public SiteHandle MoordellDistributionSite { get; }
        public NpcHandle MoordellContact { get; }
        public SiteHandle RossdamBattleSite { get; }
        public NpcHandle RossdamContact { get; }
        public SiteHandle KentridgeMayorHouse { get; }
        public NpcHandle KentridgeMayor { get; }
        public SiteHandle LoganConflictSite { get; }
        public SiteHandle LoganCastleLowerSite { get; }
        public ObjectiveHandle ChurchObjective { get; }
        public ObjectiveHandle RorikObjective { get; }
        public ObjectiveHandle MoordellObjective { get; }
        public ObjectiveHandle RossdamObjective { get; }
        public ObjectiveHandle MayorObjective { get; }
        public CutsceneHandle AngelGiveQuestCutscene { get; }
        public CutsceneHandle RorikChallengeCutscene { get; }
        public CutsceneHandle MoordellDistributionCutscene { get; }
        public CutsceneHandle RossdamBattleStartCutscene { get; }
        public CutsceneHandle RossdamBattleEndCutscene { get; }
        public CutsceneHandle MayorLoganLeadCutscene { get; }
        public CutsceneHandle LoganBattleStartCutscene { get; }
        public CutsceneHandle LoganBattleEndCutscene { get; }
        public CutsceneHandle LoganCastleBattleStartCutscene { get; }
        public CutsceneHandle LoganCastleHoleCutscene { get; }

        public RecoveredCampaignContinuationDraft(
            SiteHandle church,
            NpcHandle angel,
            SiteHandle rorikConflictSite,
            NpcHandle rorik,
            SiteHandle moordellDistributionSite,
            NpcHandle moordellContact,
            SiteHandle rossdamBattleSite,
            NpcHandle rossdamContact,
            SiteHandle kentridgeMayorHouse,
            NpcHandle kentridgeMayor,
            SiteHandle loganConflictSite,
            SiteHandle loganCastleLowerSite,
            ObjectiveHandle churchObjective,
            ObjectiveHandle rorikObjective,
            ObjectiveHandle moordellObjective,
            ObjectiveHandle rossdamObjective,
            ObjectiveHandle mayorObjective,
            CutsceneHandle angelGiveQuestCutscene,
            CutsceneHandle rorikChallengeCutscene,
            CutsceneHandle moordellDistributionCutscene,
            CutsceneHandle rossdamBattleStartCutscene,
            CutsceneHandle rossdamBattleEndCutscene,
            CutsceneHandle mayorLoganLeadCutscene,
            CutsceneHandle loganBattleStartCutscene,
            CutsceneHandle loganBattleEndCutscene,
            CutsceneHandle loganCastleBattleStartCutscene,
            CutsceneHandle loganCastleHoleCutscene)
        {
            Church = church;
            Angel = angel;
            RorikConflictSite = rorikConflictSite;
            Rorik = rorik;
            MoordellDistributionSite = moordellDistributionSite;
            MoordellContact = moordellContact;
            RossdamBattleSite = rossdamBattleSite;
            RossdamContact = rossdamContact;
            KentridgeMayorHouse = kentridgeMayorHouse;
            KentridgeMayor = kentridgeMayor;
            LoganConflictSite = loganConflictSite;
            LoganCastleLowerSite = loganCastleLowerSite;
            ChurchObjective = churchObjective;
            RorikObjective = rorikObjective;
            MoordellObjective = moordellObjective;
            RossdamObjective = rossdamObjective;
            MayorObjective = mayorObjective;
            AngelGiveQuestCutscene = angelGiveQuestCutscene;
            RorikChallengeCutscene = rorikChallengeCutscene;
            MoordellDistributionCutscene = moordellDistributionCutscene;
            RossdamBattleStartCutscene = rossdamBattleStartCutscene;
            RossdamBattleEndCutscene = rossdamBattleEndCutscene;
            MayorLoganLeadCutscene = mayorLoganLeadCutscene;
            LoganBattleStartCutscene = loganBattleStartCutscene;
            LoganBattleEndCutscene = loganBattleEndCutscene;
            LoganCastleBattleStartCutscene = loganCastleBattleStartCutscene;
            LoganCastleHoleCutscene = loganCastleHoleCutscene;
        }
    }

    /// <summary>
    /// Source-evidence-backed continuation after the existing Kentridge opening. This is a second
    /// concrete composition function, intentionally not a generic chapter or phase abstraction.
    /// Positive recovered dependencies are preserved; documented authored bridges connect otherwise
    /// disconnected recovered components.
    /// </summary>
    internal static class RecoveredCampaignContinuationSlice
    {
        public static readonly EncounterId RorikEncounter = new EncounterId("campaign:rorik-conflict");
        public static readonly EncounterId RossdamBattleEncounter = new EncounterId("campaign:rossdam-battle");
        public static readonly EncounterId LoganBattleEncounter = new EncounterId("campaign:kentridge-logan-battle");
        public static readonly EncounterId LoganCastleLowerEncounter = new EncounterId("campaign:logan-castle-lower-battle");
        public static readonly OutcomeConditionRef CompletionCondition =
            new OutcomeConditionRef("campaign:logan-castle-lower-logan-hole-complete");

        public static RecoveredCampaignContinuationDraft Compose(
            CampaignBuilder game,
            KnownOpeningCampaignDraft opening)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            if (opening == null) throw new ArgumentNullException(nameof(opening));

            ComposeWorld(
                game,
                opening,
                out SiteHandle church,
                out NpcHandle angel,
                out SiteHandle rorikConflict,
                out NpcHandle rorik,
                out SiteHandle moordellDistribution,
                out NpcHandle moordellContact,
                out SiteHandle rossdamBattle,
                out NpcHandle rossdamContact,
                out SiteHandle mayorHouse,
                out NpcHandle mayor,
                out SiteHandle loganConflict,
                out SiteHandle loganCastleLower);

            ObjectiveHandle churchObjective = church.Objective(
                "campaign:visit-kentridge-church",
                objective => objective.CompleteWhen(ObjectiveCompletion.InteractWith(angel)));
            ObjectiveHandle rorikObjective = rorikConflict.Objective(
                "campaign:confront-rorik",
                objective => objective.CompleteWhen(ObjectiveCompletion.InteractWith(rorik)));
            ObjectiveHandle moordellObjective = moordellDistribution.Objective(
                "campaign:meet-moordell-contact",
                objective => objective.CompleteWhen(ObjectiveCompletion.InteractWith(moordellContact)));
            ObjectiveHandle rossdamObjective = rossdamBattle.Objective(
                "campaign:reach-rossdam-battle",
                objective => objective.CompleteWhen(ObjectiveCompletion.InteractWith(rossdamContact)));
            ObjectiveHandle mayorObjective = mayorHouse.Objective(
                "campaign:ask-kentridge-mayor-about-logan",
                objective => objective.CompleteWhen(ObjectiveCompletion.InteractWith(mayor)));

            CutsceneHandle angelGiveQuest = church.Cutscene(IdentityCutscene("angel-give-quest"));
            CutsceneHandle rorikChallenge = rorikConflict.Cutscene(IdentityCutscene("RorikDefeated"));
            CutsceneHandle moordellDistributionCutscene = moordellDistribution.Cutscene(IdentityCutscene("moordell-distribution"));
            CutsceneHandle rossdamBattleStart = rossdamBattle.Cutscene(IdentityCutscene("rossdam-battle-start"));
            CutsceneHandle rossdamBattleEnd = rossdamBattle.Cutscene(IdentityCutscene("rossdam-battle-end"));
            CutsceneHandle mayorLoganLead = mayorHouse.Cutscene(IdentityCutscene("kentridge-ask-mayor-logan"));
            CutsceneHandle loganBattleStart = loganConflict.Cutscene(IdentityCutscene("kentridge-logan-battle-start"));
            CutsceneHandle loganBattleEnd = loganConflict.Cutscene(IdentityCutscene("kentridge-logan-battle-end"));
            CutsceneHandle loganCastleBattleStart = loganCastleLower.Cutscene(IdentityCutscene("logan-castle-battle-start"));
            CutsceneHandle loganCastleHole = loganCastleLower.Cutscene(IdentityCutscene("logan-castle-lower-logan-hole"));

            ComposeRules(
                game,
                opening,
                churchObjective,
                rorikObjective,
                moordellObjective,
                rossdamObjective,
                mayorObjective,
                angel,
                rorik,
                moordellContact,
                rossdamContact,
                mayor,
                angelGiveQuest,
                rorikChallenge,
                moordellDistributionCutscene,
                rossdamBattleStart,
                rossdamBattleEnd,
                mayorLoganLead,
                loganBattleStart,
                loganBattleEnd,
                loganCastleBattleStart,
                loganCastleHole);

            return new RecoveredCampaignContinuationDraft(
                church,
                angel,
                rorikConflict,
                rorik,
                moordellDistribution,
                moordellContact,
                rossdamBattle,
                rossdamContact,
                mayorHouse,
                mayor,
                loganConflict,
                loganCastleLower,
                churchObjective,
                rorikObjective,
                moordellObjective,
                rossdamObjective,
                mayorObjective,
                angelGiveQuest,
                rorikChallenge,
                moordellDistributionCutscene,
                rossdamBattleStart,
                rossdamBattleEnd,
                mayorLoganLead,
                loganBattleStart,
                loganBattleEnd,
                loganCastleBattleStart,
                loganCastleHole);
        }

        private static void ComposeWorld(
            CampaignBuilder game,
            KnownOpeningCampaignDraft opening,
            out SiteHandle church,
            out NpcHandle angel,
            out SiteHandle rorikConflict,
            out NpcHandle rorik,
            out SiteHandle moordellDistribution,
            out NpcHandle moordellContact,
            out SiteHandle rossdamBattle,
            out NpcHandle rossdamContact,
            out SiteHandle mayorHouse,
            out NpcHandle mayor,
            out SiteHandle loganConflict,
            out SiteHandle loganCastleLower)
        {
            KnownOpeningCampaignRoles roles = opening.Roles;
            church = roles.Kentridge.Site("campaign-kentridge-church")
                .LegacyMap("mounting-force", "kentridge-church");
            angel = church.Npc("campaign-angel", npc => npc.RequireConversation());

            // The church -> Rorik link is an explicit authored bridge. Do not attach a recovered map
            // whose chronology/ownership is not proven by the positive dependency graph.
            rorikConflict = roles.KentridgeOverworld.Site("campaign-rorik-conflict");
            rorik = rorikConflict.Npc("campaign-rorik", npc => npc.RequireConversation());

            RecoveredOverworldRegionDefinition moordellDef = RecoveredMountingForceWorldCatalog.Moordell;
            RegionHandle moordellRegion = game.World.Region(moordellDef.RegionId);
            SettlementHandle moordell = moordellRegion.Settlement(
                moordellDef.SettlementId,
                moordellDef.SettlementArchetype);
            moordellDistribution = moordell.Site("campaign-moordell-distribution")
                .LegacyMap("mounting-force", "moordell-building1");
            moordellContact = moordellDistribution.Npc(
                "campaign-moordell-contact",
                npc => npc.RequireConversation());

            RecoveredOverworldRegionDefinition rossdamDef = RecoveredMountingForceWorldCatalog.Rossdam;
            RegionHandle rossdamRegion = game.World.Region(rossdamDef.RegionId);
            SettlementHandle rossdam = rossdamRegion.Settlement(
                rossdamDef.SettlementId,
                rossdamDef.SettlementArchetype);
            rossdamBattle = rossdam.Site("campaign-rossdam-battle")
                .LegacyMap("mounting-force", "rossdam");
            rossdamContact = rossdamBattle.Npc(
                "campaign-rossdam-contact",
                npc => npc.RequireConversation());

            mayorHouse = roles.Kentridge.Site("campaign-kentridge-mayor-house")
                .LegacyMap("mounting-force", "kentridge-mayor-house");
            mayor = mayorHouse.Npc("campaign-kentridge-mayor", npc => npc.RequireConversation());

            // The mayor lead -> Logan battlefield bridge and castle geography are explicit voxel
            // authoring decisions around recovered scene dependencies, not filename-derived ordering.
            loganConflict = roles.KentridgeOverworld.Site("campaign-logan-conflict");
            loganCastleLower = rossdamRegion.Site("campaign-logan-castle-lower");
        }

        private static void ComposeRules(
            CampaignBuilder game,
            KnownOpeningCampaignDraft opening,
            ObjectiveHandle churchObjective,
            ObjectiveHandle rorikObjective,
            ObjectiveHandle moordellObjective,
            ObjectiveHandle rossdamObjective,
            ObjectiveHandle mayorObjective,
            NpcHandle angel,
            NpcHandle rorik,
            NpcHandle moordellContact,
            NpcHandle rossdamContact,
            NpcHandle mayor,
            CutsceneHandle angelGiveQuest,
            CutsceneHandle rorikChallenge,
            CutsceneHandle moordellDistribution,
            CutsceneHandle rossdamBattleStart,
            CutsceneHandle rossdamBattleEnd,
            CutsceneHandle mayorLoganLead,
            CutsceneHandle loganBattleStart,
            CutsceneHandle loganBattleEnd,
            CutsceneHandle loganCastleBattleStart,
            CutsceneHandle loganCastleHole)
        {
            // Recovered hard edge: medrare-to-church -> angel-give-quest. Geography remains a player
            // fact: completion starts a Progression objective; the actual angel interaction advances it.
            game.Story.Rule("campaign-start-church-objective", rule => rule
                .When(StoryTrigger.CutsceneCompleted(opening.MedrareToChurchCutscene))
                .Then(StoryEffect.StartObjective(churchObjective)));
            game.Story.Rule("campaign-angel-give-quest", rule => rule
                .When(StoryTrigger.InteractWith(angel))
                .If(StoryCondition.ObjectiveActive(churchObjective))
                .Then(StoryEffect.PlayCutscene(angelGiveQuest)));

            // Authored bridge from church charge to the recovered Rorik conflict component.
            game.Story.Rule("campaign-start-rorik-objective", rule => rule
                .When(StoryTrigger.CutsceneCompleted(angelGiveQuest))
                .Then(StoryEffect.StartObjective(rorikObjective)));
            game.Story.Rule("campaign-rorik-challenge", rule => rule
                .When(StoryTrigger.InteractWith(rorik))
                .If(StoryCondition.ObjectiveActive(rorikObjective))
                .Then(StoryEffect.PlayCutscene(rorikChallenge)));

            // Recovered hard edge: RorikDefeated -> Moordell distribution component. The owning
            // Encounter runtime supplies the completed battle fact; Story only starts travel truth.
            game.Story.Rule("campaign-rorik-complete-to-moordell", rule => rule
                .When(StoryTrigger.EncounterResolved(RorikEncounter))
                .Then(StoryEffect.StartObjective(moordellObjective)));
            game.Story.Rule("campaign-moordell-distribution", rule => rule
                .When(StoryTrigger.InteractWith(moordellContact))
                .If(StoryCondition.ObjectiveActive(moordellObjective))
                .Then(StoryEffect.PlayCutscene(moordellDistribution)));

            // Recovered hard edge: moordell-distribution -> rossdam-battle-start.
            game.Story.Rule("campaign-start-rossdam-objective", rule => rule
                .When(StoryTrigger.CutsceneCompleted(moordellDistribution))
                .Then(StoryEffect.StartObjective(rossdamObjective)));
            game.Story.Rule("campaign-rossdam-battle-start", rule => rule
                .When(StoryTrigger.InteractWith(rossdamContact))
                .If(StoryCondition.ObjectiveActive(rossdamObjective))
                .Then(StoryEffect.PlayCutscene(rossdamBattleStart)));
            game.Story.Rule("campaign-rossdam-battle-end", rule => rule
                .When(StoryTrigger.EncounterResolved(RossdamBattleEncounter))
                .Then(StoryEffect.PlayCutscene(rossdamBattleEnd)));

            // Recovered hard edge: rossdam-battle-end -> kentridge-ask-mayor-logan.
            game.Story.Rule("campaign-start-mayor-objective", rule => rule
                .When(StoryTrigger.CutsceneCompleted(rossdamBattleEnd))
                .Then(StoryEffect.StartObjective(mayorObjective)));
            game.Story.Rule("campaign-ask-mayor-logan", rule => rule
                .When(StoryTrigger.InteractWith(mayor))
                .If(StoryCondition.ObjectiveActive(mayorObjective))
                .Then(StoryEffect.PlayCutscene(mayorLoganLead)));

            // Authored bridge into the recovered Logan conflict; recovered hard edge then carries
            // kentridge-logan-battle-end -> logan-castle-battle-start.
            game.Story.Rule("campaign-logan-battle-start", rule => rule
                .When(StoryTrigger.CutsceneCompleted(mayorLoganLead))
                .Then(StoryEffect.PlayCutscene(loganBattleStart)));
            game.Story.Rule("campaign-logan-battle-end", rule => rule
                .When(StoryTrigger.EncounterResolved(LoganBattleEncounter))
                .Then(StoryEffect.PlayCutscene(loganBattleEnd)));
            game.Story.Rule("campaign-logan-castle-battle-start", rule => rule
                .When(StoryTrigger.CutsceneCompleted(loganBattleEnd))
                .Then(StoryEffect.PlayCutscene(loganCastleBattleStart)));

            // Recovered final subchain: lower battle end -> Logan hole. Completion of the semantic
            // consequence emits a System15 condition; Story does not set outcome state itself.
            game.Story.Rule("campaign-logan-castle-hole", rule => rule
                .When(StoryTrigger.EncounterResolved(LoganCastleLowerEncounter))
                .Then(StoryEffect.PlayCutscene(loganCastleHole)));
            game.Story.Rule("campaign-terminal-outcome-condition", rule => rule
                .When(StoryTrigger.CutsceneCompleted(loganCastleHole))
                .Then(StoryEffect.ObserveOutcomeCondition(CompletionCondition)));
        }

        private static CutsceneDefinition IdentityCutscene(string sourceSceneId) =>
            new CutsceneDefinition(
                "campaign." + sourceSceneId,
                CutsceneStageSetupDefinition.Empty,
                new[] { CutsceneStep.Dialogue(new CutsceneCueId("source:" + sourceSceneId)) });
    }
}
