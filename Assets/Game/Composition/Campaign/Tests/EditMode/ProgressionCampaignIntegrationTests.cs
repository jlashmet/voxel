using System;
using System.Reflection;
using Game.Composition.Campaign.Runtime;
using Game.Cutscenes.Api;
using Game.Progression.Api;
using Game.Quests.Api;
using Game.WorldBuilder.Api;
using NUnit.Framework;
using LegacyQuestStepDefinition = Game.Quests.Api.QuestStepDefinition;

namespace Game.Composition.Campaign.Tests
{
    public sealed class ProgressionCampaignIntegrationTests
    {
        [Test]
        public void CampaignUsesOneProgressionSessionAndFeedsQuestCompletionBackToStory()
        {
            CampaignFixture fixture = BuildFixture();
            var runtime = new CampaignRuntime(
                fixture.Blueprint,
                Array.Empty<CutsceneStageRealization>(),
                new NoActors(),
                new NoPresentation(),
                new[] { fixture.Quest });

            Assert.That(runtime.StartNewGame(), Is.EqualTo(2));
            Assert.That(runtime.IsObjectiveActive(fixture.Objective), Is.True);
            Assert.That(runtime.IsQuestActive(fixture.Quest.Ref), Is.True);

            runtime.InteractWithNpc(fixture.Guide);

            Assert.That(runtime.IsObjectiveCompleted(fixture.Objective), Is.True);
            Assert.That(runtime.IsQuestCompleted(fixture.Quest.Ref), Is.True);
            Assert.That(runtime.IsPartyMemberJoined("ally"), Is.True,
                "Quest completion must flow back through Story rather than executing consequences in Progression.");

            ProgressionSnapshot snapshot = runtime.Progression.Snapshot();
            Assert.That(snapshot.Quests.Count, Is.EqualTo(1));
            Assert.That(snapshot.StandaloneObjectives.Count, Is.EqualTo(1));
            Assert.That(snapshot.Quests[0].State, Is.EqualTo(ProgressionLifecycleState.Completed));
            Assert.That(snapshot.StandaloneObjectives[0].State, Is.EqualTo(ProgressionLifecycleState.Completed));

            CampaignProgressSnapshot campaignSnapshot = runtime.CaptureProgress();
            Assert.That(campaignSnapshot.Progression, Is.Not.Null);
            Assert.That(campaignSnapshot.Progression.Quests.Count, Is.EqualTo(1));
            Assert.That(campaignSnapshot.Progression.StandaloneObjectives.Count, Is.EqualTo(1));

            var restored = new CampaignRuntime(
                fixture.Blueprint,
                Array.Empty<CutsceneStageRealization>(),
                new NoActors(),
                new NoPresentation(),
                new[] { fixture.Quest });
            restored.RestoreProgress(campaignSnapshot);
            Assert.That(restored.IsObjectiveCompleted(fixture.Objective), Is.True);
            Assert.That(restored.IsQuestCompleted(fixture.Quest.Ref), Is.True);
            Assert.That(restored.IsPartyMemberJoined("ally"), Is.True);
        }

        [Test]
        public void CampaignRuntimeDoesNotOwnParallelMutableQuestOrObjectiveCollections()
        {
            FieldInfo[] fields = typeof(CampaignRuntime).GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
            for (var i = 0; i < fields.Length; i++)
            {
                Assert.That(fields[i].Name, Is.Not.EqualTo("_activeObjectives"));
                Assert.That(fields[i].Name, Is.Not.EqualTo("_completedObjectives"));
                Assert.That(fields[i].Name, Is.Not.EqualTo("_activeQuests"));
                Assert.That(fields[i].Name, Is.Not.EqualTo("_completedQuests"));
            }

            Assert.That(typeof(CampaignRuntime).GetField(
                "_progression",
                BindingFlags.Instance | BindingFlags.NonPublic), Is.Not.Null);
        }

        private static CampaignFixture BuildFixture()
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

            var quest = new QuestDefinition(questRef, new[]
            {
                new LegacyQuestStepDefinition(
                    new QuestStepRef("talk"),
                    guide.Id,
                    QuestCompletion.InteractWith(guide.Id))
            });

            return new CampaignFixture(game.Build(), quest, objective.Ref, guide.Ref);
        }

        private sealed class CampaignFixture
        {
            public CampaignFixture(CampaignBlueprint blueprint, QuestDefinition quest, ObjectiveRef objective, NpcRef guide)
            {
                Blueprint = blueprint;
                Quest = quest;
                Objective = objective;
                Guide = guide;
            }

            public CampaignBlueprint Blueprint { get; }
            public QuestDefinition Quest { get; }
            public ObjectiveRef Objective { get; }
            public NpcRef Guide { get; }
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
            public ICutsceneOperation SetCamera(CutsceneCueId cameraCue) => CompletedCutsceneOperation.Instance;
            public ICutsceneOperation ShowDialogue(CutsceneActorId speaker, CutsceneCueId dialogueCue) => CompletedCutsceneOperation.Instance;
            public ICutsceneOperation PlaySound(CutsceneCueId soundCue) => CompletedCutsceneOperation.Instance;
        }
    }
}
