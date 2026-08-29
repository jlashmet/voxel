using System;
using System.Collections.Generic;
using Game.Composition.Campaign;
using Game.Composition.Campaign.Content;
using Game.Composition.Campaign.Runtime;
using Game.Cutscenes.Api;
using Game.Cutscenes.Content.Kentridge;
using Game.Quests.Api;
using Game.Story.Api;
using Game.Story.Runtime;
using Game.WorldBuilder.Api;
using NUnit.Framework;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class KentridgeOpeningProgressionTests
    {
        [Test]
        public void AuthoritativeOpeningSourceContentAndChoreographyArePreserved()
        {
            Assert.That(KentridgeOpeningScript.OriginalOpeningLineCount, Is.EqualTo(31));
            Assert.That(
                KentridgeOpeningScript.LineFor(KentridgeOpeningScript.CueForOriginalLine(27)),
                Is.EqualTo("There's a few things I have to do first though.  First, my father wanted me to stop by the house to show me something."),
                "The existing Logan opening must continue to direct Weldon to Awon rather than being rewritten by this feature.");

            string[] expectedLines =
            {
                "Weldon my boy!",
                "Hey dad.",
                "How are you all?  Good to see you Steven.  Hey madeline.",
                "Greetings sir.  A pleasure to see you again.",
                "Hi!  Tee hee.",
                "I don't believe I've met this young fellow.  Pleased to meet you.  I'm Weldon's father, Awon.",
                "The pleasure is all mine sir.  ",
                "We're going with Logan to meet with Lord Radcliffe later to ask about the lack of food lately.",
                "Ohhhh Madeline, you are a brave young bunch.  Be very careful though, Radcliffe can be a dangerous man.",
                "We understand and agree sir, but this matter is too important to ignore.  If things do go awry, I'm confident in our ability to defend ourselves.",
                "Well certainly you and Steven can hold your own, but what about Weldon?",
                "Weldon, when you see them rush gallantly into battle, you remember to stay back and cast your spells like a ninny you hear?",
                "Yes dad...",
                "And try not to cause too much harm, or everyone will come after you and you'll have to run around in circles, again like a complete ninny.",
                "Dad cut it out! I know how to handle myself.",
                "Ok ok. Haha.  Well anyway, the reason I asked you to stop by is I found an old family heirloom in the back room.",
                "Its behind a bunch of boxes that are too heavy for my old bones to move, but if you can clear them out, I think you'll find it useful.",
                "And even better, you don't even have to equip it!  Because any items you find will add to your skills, no equipping or unequipping is needed.",
                "If you click on your picture in the top left corner, you can see which items are equipped to each of your party members.",
                "What do I do after clicking the picture?",
                "Really Weldon? You are a wizard.  Figure it out already.",
                "Ok thanks dad, thats helpful.  We will check it out."
            };
            CutsceneActorId[] expectedSpeakers =
            {
                KentridgeOpeningProgressionCutscenes.Awon,
                KentridgeOpeningProgressionCutscenes.Weldon,
                KentridgeOpeningProgressionCutscenes.Awon,
                KentridgeOpeningProgressionCutscenes.Steven,
                KentridgeOpeningProgressionCutscenes.Madeline,
                KentridgeOpeningProgressionCutscenes.Awon,
                KentridgeOpeningProgressionCutscenes.Logan,
                KentridgeOpeningProgressionCutscenes.Madeline,
                KentridgeOpeningProgressionCutscenes.Awon,
                KentridgeOpeningProgressionCutscenes.Logan,
                KentridgeOpeningProgressionCutscenes.Awon,
                KentridgeOpeningProgressionCutscenes.Awon,
                KentridgeOpeningProgressionCutscenes.Weldon,
                KentridgeOpeningProgressionCutscenes.Awon,
                KentridgeOpeningProgressionCutscenes.Weldon,
                KentridgeOpeningProgressionCutscenes.Awon,
                KentridgeOpeningProgressionCutscenes.Awon,
                KentridgeOpeningProgressionCutscenes.Awon,
                KentridgeOpeningProgressionCutscenes.Awon,
                KentridgeOpeningProgressionCutscenes.Weldon,
                KentridgeOpeningProgressionCutscenes.Awon,
                KentridgeOpeningProgressionCutscenes.Weldon
            };

            CutsceneDefinition awon = KentridgeOpeningProgressionCutscenes.AwonDefinition;
            Assert.That(awon.Steps.Count, Is.EqualTo(expectedLines.Length));
            for (var i = 0; i < expectedLines.Length; i++)
            {
                Assert.That(awon.Steps[i].Type, Is.EqualTo(CutsceneStepType.Dialogue), "Awon step " + i);
                Assert.That(awon.Steps[i].Actor, Is.EqualTo(expectedSpeakers[i]), "Awon speaker " + i);
                Assert.That(KentridgeOpeningScript.LineFor(awon.Steps[i].Cue), Is.EqualTo(expectedLines[i]), "Awon line " + i);
            }

            CutsceneDefinition join = KentridgeOpeningProgressionCutscenes.MedrareJoinDefinition;
            Assert.That(join.Steps.Count, Is.EqualTo(4));
            Assert.That(join.Steps[0].Type, Is.EqualTo(CutsceneStepType.Camera));
            Assert.That(join.Steps[0].Cue, Is.EqualTo(KentridgeOpeningProgressionCutscenes.MedrareJoinZoomHalf));
            Assert.That(join.Steps[1].Type, Is.EqualTo(CutsceneStepType.Wait));
            Assert.That(join.Steps[1].DurationMilliseconds, Is.EqualTo(1500));
            Assert.That(join.Steps[2].Type, Is.EqualTo(CutsceneStepType.MoveActor));
            Assert.That(join.Steps[2].Actor, Is.EqualTo(KentridgeOpeningProgressionCutscenes.Medrare));
            Assert.That(join.Steps[2].StagePoint, Is.EqualTo(KentridgeOpeningProgressionCutscenes.MedrareApproachPoint));
            Assert.That(join.Steps[2].DurationMilliseconds, Is.EqualTo(2000));
            Assert.That(join.Steps[3].Type, Is.EqualTo(CutsceneStepType.Dialogue));
            Assert.That(join.Steps[3].Cue, Is.EqualTo(KentridgeOpeningScript.MedrareJoinSourceDialogue5000));
            Assert.That(join.StageRequirements.Count, Is.EqualTo(1));
            Assert.That(join.StageRequirements[0].Point, Is.EqualTo(KentridgeOpeningProgressionCutscenes.MedrareApproachPoint));
            Assert.That(join.StageRequirements[0].Region, Is.EqualTo(CutsceneStageRegion.ConversationApproach));
            Assert.That(join.StageRequirements[0].Facing, Is.EqualTo(CutsceneStageFacingHint.TowardStageCenter));

            Assert.That(KentridgeOpeningScript.LineFor(KentridgeOpeningScript.SeeMedrareSourceDialogue), Is.Empty);
            Assert.That(KentridgeOpeningScript.LineFor(KentridgeOpeningScript.MedrareJoinSourceDialogue5000), Is.Empty);
            Assert.That(KentridgeOpeningScript.LineFor(KentridgeOpeningScript.MedrareFirstSpellSourceDialogue), Is.Empty);
            Assert.That(KentridgeOpeningScript.LineFor(KentridgeOpeningScript.MedrareToChurchSourceDialogue), Is.Empty,
                "Missing pinned payloads must stay empty rather than being replaced with fabricated dialogue.");
        }

        [Test]
        public void OpeningProgressionUsesDistinctAwonGatedMedrareEventsAndPersistentEffects()
        {
            KnownOpeningCampaignContent content = BuildOpening();
            var state = new StoryState();
            var effects = new StoryEffects();

            Assert.That(DispatchNpc(content, content.Awon, state, effects), Is.Zero);
            Assert.That(DispatchSite(content, content.MedrareSite, state, effects), Is.Zero);
            Assert.That(DispatchNpc(content, content.Medrare, state, effects), Is.Zero);
            Assert.That(DispatchSite(content, content.MedrareHouseSite, state, effects), Is.Zero,
                "No Medrare event may fire before Awon completes.");

            state.Complete(content.IntroCutscene);
            Assert.That(DispatchNpc(content, content.Awon, state, effects), Is.EqualTo(1));
            Assert.That(effects.LastCutscene, Is.EqualTo(content.AwonOpeningCutscene));
            state.Complete(content.AwonOpeningCutscene);

            Assert.That(DispatchNpc(content, content.Medrare, state, effects), Is.EqualTo(1),
                "The original join is independently gated by Awon, not by an invented sighting prerequisite.");
            Assert.That(effects.LastCutscene, Is.EqualTo(content.MedrareJoinCutscene));
            Assert.That(DispatchSite(content, content.MedrareHouseSite, state, effects), Is.EqualTo(1),
                "The first-spell map event is independently gated by Awon in the pinned source.");
            Assert.That(effects.LastCutscene, Is.EqualTo(content.MedrareFirstSpellCutscene));
            Assert.That(DispatchSite(content, content.MedrareSite, state, effects), Is.EqualTo(1));
            Assert.That(effects.LastCutscene, Is.EqualTo(content.SeeMedrareCutscene),
                "The sighting and join must remain distinct post-Awon events.");

            state.Complete(content.SeeMedrareCutscene);
            state.Complete(content.MedrareJoinCutscene);
            state.Complete(content.MedrareFirstSpellCutscene);

            Assert.That(
                StoryRuleEngine.Dispatch(
                    content.Blueprint.StoryRules,
                    StoryEvent.CutsceneCompleted(content.MedrareJoinCutscene),
                    state,
                    effects),
                Is.EqualTo(1));
            Assert.That(effects.JoinedPartyMembers, Does.Contain("Medrare"));

            Assert.That(
                StoryRuleEngine.Dispatch(
                    content.Blueprint.StoryRules,
                    StoryEvent.CutsceneCompleted(content.MedrareFirstSpellCutscene),
                    state,
                    effects),
                Is.EqualTo(2));
            Assert.That(effects.GrantedSpells, Does.Contain("Flame"));
            Assert.That(effects.LastCutscene, Is.EqualTo(content.MedrareToChurchCutscene));
            state.Complete(content.MedrareToChurchCutscene);

            Assert.That(DispatchNpc(content, content.Awon, state, effects), Is.Zero);
            Assert.That(DispatchSite(content, content.MedrareSite, state, effects), Is.Zero);
            Assert.That(DispatchNpc(content, content.Medrare, state, effects), Is.Zero);
            Assert.That(DispatchSite(content, content.MedrareHouseSite, state, effects), Is.Zero,
                "Completed play-once beats must stay suppressed on revisit/re-entry.");
        }

        [Test]
        public void CampaignRuntimeSnapshotRestoresOneShotProgressionEffects()
        {
            var game = Game.WorldBuilder.Api.Campaign.Create("opening-progress-snapshot");
            SiteRef site = game.World.RequireSite("progress-site");
            var definition = new CutsceneDefinition(
                "opening-progress-snapshot-scene",
                CutsceneStageSetupDefinition.Empty,
                Array.Empty<CutsceneStep>());
            CutsceneRef cutscene = game.Story.Cutscene(definition, scene => scene.At(site));

            game.Story.Rule("play-once", rule => rule
                .When(StoryTrigger.NewGame())
                .If(StoryCondition.CutsceneNotCompleted(cutscene))
                .Then(StoryEffect.PlayCutscene(cutscene)));
            game.Story.Rule("join-medrare", rule => rule
                .When(StoryTrigger.CutsceneCompleted(cutscene))
                .Then(StoryEffect.JoinPartyMember("Medrare")));
            game.Story.Rule("grant-flame", rule => rule
                .When(StoryTrigger.CutsceneCompleted(cutscene))
                .Then(StoryEffect.GrantSpell("Flame")));

            CampaignBlueprint blueprint = game.Build();
            var runtime = new CampaignRuntime(
                blueprint,
                Array.Empty<CutsceneStageRealization>(),
                EmptyActorProvider.Instance,
                CompletedPresentation.Instance);

            Assert.That(runtime.StartNewGame(), Is.EqualTo(1));
            runtime.Tick(0);
            Assert.That(runtime.IsCutsceneCompleted(cutscene), Is.True);
            Assert.That(runtime.IsPartyMemberJoined("Medrare"), Is.True);
            Assert.That(runtime.HasSpell("Flame"), Is.True);

            CampaignProgressSnapshot snapshot = runtime.CaptureProgress();
            Assert.That(snapshot.CompletedCutscenes, Is.EqualTo(new[] { cutscene }));
            Assert.That(snapshot.JoinedPartyMembers, Is.EqualTo(new[] { "Medrare" }));
            Assert.That(snapshot.GrantedSpells, Is.EqualTo(new[] { "Flame" }));

            var restored = new CampaignRuntime(
                blueprint,
                Array.Empty<CutsceneStageRealization>(),
                EmptyActorProvider.Instance,
                CompletedPresentation.Instance);
            restored.RestoreProgress(snapshot);

            Assert.That(restored.IsCutsceneCompleted(cutscene), Is.True);
            Assert.That(restored.IsPartyMemberJoined("Medrare"), Is.True);
            Assert.That(restored.HasSpell("Flame"), Is.True);
            Assert.That(restored.StartNewGame(), Is.Zero,
                "Reloaded completion state must suppress the play-once cutscene instead of reopening an invalid intermediate state.");
        }

        private static KnownOpeningCampaignContent BuildOpening()
        {
            var destination = new CutsceneDefinition(
                "test.destination",
                CutsceneStageSetupDefinition.Empty,
                Array.Empty<CutsceneStep>());
            return KnownOpeningCampaignContent.Build(destination);
        }

        private static int DispatchNpc(
            KnownOpeningCampaignContent content,
            NpcRef npc,
            StoryState state,
            StoryEffects effects) =>
            StoryRuleEngine.Dispatch(content.Blueprint.StoryRules, StoryEvent.NpcInteracted(npc), state, effects);

        private static int DispatchSite(
            KnownOpeningCampaignContent content,
            SiteRef site,
            StoryState state,
            StoryEffects effects) =>
            StoryRuleEngine.Dispatch(content.Blueprint.StoryRules, StoryEvent.SiteProximityEntered(site), state, effects);

        private sealed class StoryState : IStoryStateView
        {
            private readonly HashSet<CutsceneRef> _completed = new HashSet<CutsceneRef>();
            public void Complete(CutsceneRef cutscene) => _completed.Add(cutscene);
            public bool IsObjectiveActive(ObjectiveRef objective) => false;
            public bool IsQuestActive(QuestRef quest) => false;
            public bool IsQuestCompleted(QuestRef quest) => false;
            public bool IsCutsceneCompleted(CutsceneRef cutscene) => _completed.Contains(cutscene);
        }

        private sealed class StoryEffects : IStoryProgressEffectSink
        {
            public CutsceneRef LastCutscene { get; private set; }
            public HashSet<string> JoinedPartyMembers { get; } = new HashSet<string>(StringComparer.Ordinal);
            public HashSet<string> GrantedSpells { get; } = new HashSet<string>(StringComparer.Ordinal);
            public void StartObjective(ObjectiveRef objective) { }
            public void StartQuest(QuestRef quest) { }
            public void PlayCutscene(CutsceneRef cutscene) => LastCutscene = cutscene;
            public void JoinPartyMember(string memberId) => JoinedPartyMembers.Add(memberId);
            public void GrantSpell(string spellId) => GrantedSpells.Add(spellId);
        }

        private sealed class EmptyActorProvider : IWorldBoundCutsceneActorProvider
        {
            public static readonly EmptyActorProvider Instance = new EmptyActorProvider();
            private EmptyActorProvider() { }
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

        private sealed class CompletedPresentation : ICutscenePresentation
        {
            public static readonly CompletedPresentation Instance = new CompletedPresentation();
            private CompletedPresentation() { }
            public ICutsceneOperation SetCamera(CutsceneCueId cameraCue) => CompletedCutsceneOperation.Instance;
            public ICutsceneOperation ShowDialogue(CutsceneActorId speaker, CutsceneCueId dialogueCue) => CompletedCutsceneOperation.Instance;
            public ICutsceneOperation PlaySound(CutsceneCueId soundCue) => CompletedCutsceneOperation.Instance;
        }
    }
}
