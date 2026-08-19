using System;
using System.Collections.Generic;
using Game.Story.Api;
using Game.WorldBuilder.Api;

namespace Game.Story.Runtime
{
    /// <summary>
    /// Deterministic WHEN / IF / THEN evaluator. All conditions for one incoming event are evaluated
    /// before any effects are applied, so effects from one rule cannot enable another rule during the
    /// same dispatch. Effects then execute in authored rule order and effect order.
    /// </summary>
    public static class StoryRuleEngine
    {
        private readonly struct PendingEffect
        {
            public IStoryEffectSpec Effect { get; }

            public PendingEffect(IStoryEffectSpec effect)
            {
                Effect = effect ?? throw new ArgumentNullException(nameof(effect));
            }
        }

        public static int Dispatch(
            IReadOnlyList<StoryRuleSpec> rules,
            StoryEvent storyEvent,
            IStoryStateView state,
            IStoryEffectSink effects)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (effects == null) throw new ArgumentNullException(nameof(effects));

            var pending = new List<PendingEffect>();
            int matchedRules = 0;

            // Evaluation phase: no effect may mutate state while conditions are being checked.
            for (var i = 0; i < rules.Count; i++)
            {
                StoryRuleSpec rule = rules[i] ?? throw new InvalidOperationException(
                    "Story rule collection contains a null rule at index " + i + ".");

                if (!TriggerMatches(rule.Trigger, storyEvent))
                    continue;
                if (!ConditionsMatch(rule.Conditions, state))
                    continue;

                matchedRules++;
                for (var j = 0; j < rule.Effects.Count; j++)
                    pending.Add(new PendingEffect(rule.Effects[j]));
            }

            // Execution phase: preserve authored rule order and effect order.
            for (var i = 0; i < pending.Count; i++)
                ApplyEffect(pending[i].Effect, effects);

            return matchedRules;
        }

        private static bool TriggerMatches(IStoryTriggerSpec trigger, StoryEvent storyEvent)
        {
            if (trigger is NewGameTriggerSpec)
                return storyEvent.Kind == StoryEventKind.NewGame;

            if (trigger is InteractWithNpcTriggerSpec interact)
                return storyEvent.Kind == StoryEventKind.NpcInteracted
                    && interact.Npc.Equals(storyEvent.Npc);

            if (trigger is CutsceneCompletedTriggerSpec completed)
                return storyEvent.Kind == StoryEventKind.CutsceneCompleted
                    && completed.Cutscene.Equals(storyEvent.Cutscene);

            if (trigger is QuestCompletedTriggerSpec questCompleted)
                return storyEvent.Kind == StoryEventKind.QuestCompleted
                    && questCompleted.Quest.Equals(storyEvent.Quest);

            throw new InvalidOperationException(
                "Unsupported story trigger type: " + (trigger?.GetType().FullName ?? "<null>") + ".");
        }

        private static bool ConditionsMatch(
            IReadOnlyList<IStoryConditionSpec> conditions,
            IStoryStateView state)
        {
            for (var i = 0; i < conditions.Count; i++)
            {
                IStoryConditionSpec condition = conditions[i];
                if (condition is ObjectiveActiveConditionSpec active)
                {
                    if (!state.IsObjectiveActive(active.Objective))
                        return false;
                    continue;
                }

                if (condition is QuestActiveConditionSpec questActive)
                {
                    if (!state.IsQuestActive(questActive.Quest))
                        return false;
                    continue;
                }

                if (condition is CutsceneNotCompletedConditionSpec notCompleted)
                {
                    if (state.IsCutsceneCompleted(notCompleted.Cutscene))
                        return false;
                    continue;
                }

                throw new InvalidOperationException(
                    "Unsupported story condition type: " +
                    (condition?.GetType().FullName ?? "<null>") + ".");
            }

            return true;
        }

        private static void ApplyEffect(IStoryEffectSpec effect, IStoryEffectSink sink)
        {
            if (effect is StartObjectiveEffectSpec start)
            {
                sink.StartObjective(start.Objective);
                return;
            }

            if (effect is StartQuestEffectSpec startQuest)
            {
                sink.StartQuest(startQuest.Quest);
                return;
            }

            if (effect is PlayCutsceneEffectSpec play)
            {
                sink.PlayCutscene(play.Cutscene);
                return;
            }

            throw new InvalidOperationException(
                "Unsupported story effect type: " + (effect?.GetType().FullName ?? "<null>") + ".");
        }
    }
}
