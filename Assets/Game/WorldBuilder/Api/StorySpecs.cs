using System;
using System.Collections.Generic;

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

    public sealed class ObjectiveActiveConditionSpec : IStoryConditionSpec
    {
        public ObjectiveRef Objective { get; }
        internal ObjectiveActiveConditionSpec(ObjectiveRef objective) => Objective = objective;
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

    public sealed class PlayCutsceneEffectSpec : IStoryEffectSpec
    {
        public CutsceneRef Cutscene { get; }
        internal PlayCutsceneEffectSpec(CutsceneRef cutscene) => Cutscene = cutscene;
    }

    public static class StoryTrigger
    {
        public static IStoryTriggerSpec NewGame() => new NewGameTriggerSpec();
        public static InteractWithNpcTriggerSpec InteractWith(NpcRef npc) => new InteractWithNpcTriggerSpec(npc);
    }

    public static class StoryCondition
    {
        public static IStoryConditionSpec ObjectiveActive(ObjectiveRef objective) => new ObjectiveActiveConditionSpec(objective);
        public static IStoryConditionSpec CutsceneNotCompleted(CutsceneRef cutscene) => new CutsceneNotCompletedConditionSpec(cutscene);
    }

    public static class StoryEffect
    {
        public static IStoryEffectSpec StartObjective(ObjectiveRef objective) => new StartObjectiveEffectSpec(objective);
        public static IStoryEffectSpec PlayCutscene(CutsceneRef cutscene) => new PlayCutsceneEffectSpec(cutscene);
    }

    public static class ObjectiveCompletion
    {
        public static IObjectiveCompletionSpec InteractWith(NpcRef npc) => new InteractWithNpcTriggerSpec(npc);
    }

    public sealed class CutsceneSpec
    {
        public CutsceneRef Ref { get; }
        public SiteRef Site { get; }
        public IStoryTriggerSpec Trigger { get; }
        public IReadOnlyList<IStoryConditionSpec> Conditions { get; }
        public IReadOnlyList<IStoryEffectSpec> Effects { get; }

        internal CutsceneSpec(
            CutsceneRef @ref,
            SiteRef site,
            IStoryTriggerSpec trigger,
            IStoryConditionSpec[] conditions,
            IStoryEffectSpec[] effects)
        {
            Ref = @ref;
            Site = site;
            Trigger = trigger;
            Conditions = conditions ?? Array.Empty<IStoryConditionSpec>();
            Effects = effects ?? Array.Empty<IStoryEffectSpec>();
        }
    }

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
