using System;
using Game.Composition.Campaign;
using Game.Composition.Campaign.Runtime;
using Game.Cutscenes.Api;
using Game.Progression.Api;
using Game.Quests.Api;
using Game.WorldBuilder.Api;
using NUnit.Framework;

namespace Game.Progression.Tests
{
    public sealed class ProgressionCampaignIntegrationTests
    {
        [Test]
        public void CampaignUsesOneProgressionSessionAndFeedsQuestCompletionBackToStory()
        {
            var game = Game.WorldBuilder.Api.Campaign.Create("progression-integration");
            RegionHandle region = game.World.Region("region");
            SiteHandle site = region.Site("site");
            NpcHandle guide = site.Npc("guide");
            ObjectiveHandle objective = site.Objective(
                "objective:guide",
                authored => authored.CompleteWhen(ObjectiveCompletion.InteractWith(guide)));
            var questRef = new QuestRef("quest:guide");

            game.Story.Rule("start-objective", rule => rule
                .When(StoryTrigger.NewGame())
                .Then(StoryEffect.StartObjective(objective)));
            game.Story.Rule("start-quest", rule => rule
                .When(StoryTrigger.NewGame())
                .Then(StoryEffect.StartQuest(questRef)));
            game.Story.Rule("quest-complete-consequence", rule => rule
                .When(StoryTrigger.QuestCompleted(questRef))
                .Then(StoryEffect.JoinPartyMember("ally")));

            CampaignBlueprint blueprint = game.Build();
            var quest = new QuestDefinition(questRef, new[]
            {
                new Game.Quests.Api.QuestStepDefinition(
                    new QuestStepRef("talk"),
                    guide.Id,
                    QuestCompletion.InteractWith(guide.Id))
            });
            var runtime = new CampaignRuntime(
                blueprint,
                Array.Empty<CutsceneStageRealization>(),
                new NoActors(),
                new NoPresentation(),
                new[] { quest });

            Assert.That(runtime.StartNewGame(), Is.EqualTo(2));
            Assert.That(runtime.IsObjectiveActive(objective.Ref), Is.True);
            Assert.That(runtime.IsQuestActive(questRef), Is.True);

            runtime.InteractWithNpc(guide.Ref);

            Assert.That(runtime.IsObjectiveCompleted(objective.Ref), Is.True);
            Assert.That(runtime.IsQuestCompleted(questRef), Is.True);
            Assert.That(runtime.IsPartyMemberJoined("ally"), Is.True,
                "Quest completion must flow back through Story rather than executing consequences in Progression.");

            ProgressionSnapshot snapshot = runtime.Progression.Snapshot();
            Assert.That(snapshot.Quests.Count, Is.EqualTo(1));
            Assert.That(snapshot.StandaloneObjectives.Count, Is.EqualTo(1));
            Assert.That(snapshot.Quests[0].State, Is.EqualTo(ProgressionLifecycleState.Completed));
            Assert.That(snapshot.StandaloneObjectives[0].State, Is.EqualTo(ProgressionLifecycleState.Completed));

            CampaignProgressSnapshot campaignSnapshot = runtime.CaptureProgress();
            Assert.That(ReferenceEquals(campaignSnapshot.Progression, null), Is.False);
            Assert.That(campaignSnapshot.Progression.Quests.Count, Is.EqualTo(1));
            Assert.That(campaignSnapshot.Progression.StandaloneObjectives.Count, Is.EqualTo(1));
        }

        private sealed class NoActors : IWorldBoundCutsceneActorProvider
        {
            public bool TryResolveNpc(NpcRef npc, out ICutsceneActorRuntime actor)
            {
                actor = null;
                return false;
            }

            public bool TryResolvePlayer(int playerSlot, out ICutsceneActorRuntime actor)
            {
                actor = null;
                return false;
            }
        }

        private sealed class NoPresentation : ICutscenePresentation
        {
            public ICutsceneOperation SetCamera(CutsceneCueId cameraCue) =>
                CompletedCutsceneOperation.Instance;

            public ICutsceneOperation ShowDialogue(CutsceneActorId speaker, CutsceneCueId dialogueCue) =>
                CompletedCutsceneOperation.Instance;

            public ICutsceneOperation PlaySound(CutsceneCueId soundCue) =>
                CompletedCutsceneOperation.Instance;
        }
    }
}
