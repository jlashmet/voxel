using System;
using System.Collections.Generic;
using Game.Cutscenes.Api;
using Game.Quests.Api;

namespace Game.WorldBuilder.Api
{
    public interface IStoryTriggerSpec { }
    public interface IStoryConditionSpec { }
    public interface IStoryEffectSpec { }
    public interface IObjectiveCompletionSpec { }

    public sealed class NewGameTriggerSpec : IStoryTriggerSpec
    {
        internal NewGameTriggerSpec() { }
    }

    public sealed class InteractWithNpcTriggerSpec : IStoryTriggerSpec, IObjectiveCompletionSpec
    {
        public NpcRef Npc { get; }
        internal InteractWithNpcTriggerSpec(NpcRef npc) => Npc = npc;
    }

    public sealed class CutsceneCompletedTriggerSpec : IStoryTriggerSpec
    {
        public CutsceneRef Cutscene { get; }
        internal CutsceneCompletedTriggerSpec(CutsceneRef cutscene) => Cutscene = cutscene;
    }

    public sealed class QuestCompletedTriggerSpec : IStoryTriggerSpec
    {
        public QuestRef Quest { get; }
        internal QuestCompletedTriggerSpec(QuestRef quest) => Quest = quest;
    }

    public sealed class ObjectiveActiveConditionSpec : IStoryConditionSpec
    {
        public ObjectiveRef Objective { get; }
        internal ObjectiveActiveConditionSpec(ObjectiveRef objective) => Objective = objective;
    }

    public sealed class QuestActiveConditionSpec : IStoryConditionSpec
    {
        public QuestRef Quest { get; }
        internal QuestActiveConditionSpec(QuestRef quest) => Quest = quest;
    }

    public sealed class CutsceneNotCompletedConditionSpec : IStoryConditionSpec
    {
        public CutsceneRef Cutscene { get; }
        internal CutsceneNotCompletedConditionSpec(CutsceneRef cutscene) => Cutscene = cutscene;
    }

    public sealed class StartObjectiveEffectSpec : IStoryEffectSpec
    {
        public ObjectiveRef Objective { get; }
        internal StartObjectiveEffectSpec(ObjectiveRef objective) => Objective = objective;
    }

    public sealed class StartQuestEffectSpec : IStoryEffectSpec
    {
        public QuestRef Quest { get; }
        internal StartQuestEffectSpec(QuestRef quest) => Quest = quest;
    }

    public sealed class PlayCutsceneEffectSpec : IStoryEffectSpec
    {
        public CutsceneRef Cutscene { get; }
        internal PlayCutsceneEffectSpec(CutsceneRef cutscene) => Cutscene = cutscene;
    }

    public static class StoryTrigger
    {
        public static IStoryTriggerSpec NewGame() => new NewGameTriggerSpec();
        public static InteractWithNpcTriggerSpec InteractWith(NpcRef npc) => new InteractWithNpcTriggerSpec(npc);
        public static CutsceneCompletedTriggerSpec CutsceneCompleted(CutsceneRef cutscene) =>
            new CutsceneCompletedTriggerSpec(cutscene);
        public static QuestCompletedTriggerSpec QuestCompleted(QuestRef quest) =>
            new QuestCompletedTriggerSpec(quest);
    }

    public static class StoryCondition
    {
        public static IStoryConditionSpec ObjectiveActive(ObjectiveRef objective) => new ObjectiveActiveConditionSpec(objective);
        public static IStoryConditionSpec QuestActive(QuestRef quest) => new QuestActiveConditionSpec(quest);
        public static IStoryConditionSpec CutsceneNotCompleted(CutsceneRef cutscene) => new CutsceneNotCompletedConditionSpec(cutscene);
    }

    public static class StoryEffect
    {
        public static IStoryEffectSpec StartObjective(ObjectiveRef objective) => new StartObjectiveEffectSpec(objective);
        public static IStoryEffectSpec StartQuest(QuestRef quest) => new StartQuestEffectSpec(quest);
        public static IStoryEffectSpec PlayCutscene(CutsceneRef cutscene) => new PlayCutsceneEffectSpec(cutscene);
    }

    public static class ObjectiveCompletion
    {
        public static IObjectiveCompletionSpec InteractWith(NpcRef npc) => new InteractWithNpcTriggerSpec(npc);
    }

    public enum CutsceneActorTargetKind
    {
        Npc = 0,
        PlayerSlot = 1
    }

    public readonly struct CutsceneActorTargetSpec
    {
        public CutsceneActorTargetKind Kind { get; }
        public NpcRef Npc { get; }
        public int PlayerSlot { get; }

        private CutsceneActorTargetSpec(CutsceneActorTargetKind kind, NpcRef npc, int playerSlot)
        {
            Kind = kind;
            Npc = npc;
            PlayerSlot = playerSlot;
        }

        internal static CutsceneActorTargetSpec ForNpc(NpcRef npc) =>
            new CutsceneActorTargetSpec(CutsceneActorTargetKind.Npc, npc, -1);

        internal static CutsceneActorTargetSpec ForPlayer(int playerSlot)
        {
            if (playerSlot < 0) throw new ArgumentOutOfRangeException(nameof(playerSlot));
            return new CutsceneActorTargetSpec(CutsceneActorTargetKind.PlayerSlot, default, playerSlot);
        }
    }

    public static class CutsceneActorTarget
    {
        public static CutsceneActorTargetSpec Npc(NpcRef npc) => CutsceneActorTargetSpec.ForNpc(npc);
        public static CutsceneActorTargetSpec Player(int playerSlot) => CutsceneActorTargetSpec.ForPlayer(playerSlot);
    }

    public readonly struct CutsceneActorBindingSpec
    {
        public CutsceneActorId Actor { get; }
        public CutsceneActorTargetSpec Target { get; }

        public CutsceneActorBindingSpec(CutsceneActorId actor, CutsceneActorTargetSpec target)
        {
            if (string.IsNullOrWhiteSpace(actor.Value))
                throw new ArgumentException("Cutscene actor binding requires an actor id.", nameof(actor));
            Actor = actor;
            Target = target;
        }
    }

    /// <summary>
    /// A concrete use of an authored cutscene definition in the generated world. This owns only
    /// physical/world binding: site and actor identities. Story sequencing is expressed separately
    /// through StoryRuleSpec.
    /// </summary>
    public sealed class CutsceneSpec
    {
        public CutsceneRef Ref { get; }
        public CutsceneDefinition Definition { get; }
        public SiteRef Site { get; }
        public IReadOnlyList<CutsceneActorBindingSpec> ActorBindings { get; }
        public IReadOnlyList<CutsceneStagePointId> StageRequirements => Definition.RequiredStagePoints;

        internal CutsceneSpec(
            CutsceneRef @ref,
            CutsceneDefinition definition,
            SiteRef site,
            CutsceneActorBindingSpec[] actorBindings)
        {
            Ref = @ref;
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Site = site;
            ActorBindings = actorBindings ?? Array.Empty<CutsceneActorBindingSpec>();
        }
    }

    /// <summary>Runtime story transition: WHEN Trigger, IF all Conditions, THEN Effects in authored order.</summary>
    public sealed class StoryRuleSpec
    {
        public StoryRuleRef Ref { get; }
        public IStoryTriggerSpec Trigger { get; }
        public IReadOnlyList<IStoryConditionSpec> Conditions { get; }
        public IReadOnlyList<IStoryEffectSpec> Effects { get; }

        internal StoryRuleSpec(
            StoryRuleRef @ref,
            IStoryTriggerSpec trigger,
            IStoryConditionSpec[] conditions,
            IStoryEffectSpec[] effects)
        {
            Ref = @ref;
            Trigger = trigger ?? throw new ArgumentNullException(nameof(trigger));
            Conditions = conditions ?? Array.Empty<IStoryConditionSpec>();
            Effects = effects ?? Array.Empty<IStoryEffectSpec>();
        }
    }

    /// <summary>
    /// Legacy single-objective source representation. CampaignRuntime compiles each instance into a
    /// one-step QuestDefinition; new authoring should use QuestHandle while this bridge remains.
    /// </summary>
    public sealed class ObjectiveSpec
    {
        public ObjectiveRef Ref { get; }
        public SiteRef Target { get; }
        public IObjectiveCompletionSpec Completion { get; }

        internal ObjectiveSpec(ObjectiveRef @ref, SiteRef target, IObjectiveCompletionSpec completion)
        {
            Ref = @ref;
            Target = target;
            Completion = completion;
        }
    }
}
