using System;
using Game.Composition.Campaign.Runtime;
using Game.Cutscenes.Api;
using Game.Encounters.Api;
using Game.Outcomes.Api;
using Game.Outcomes.Runtime;
using Game.WorldBuilder.Api;
using NUnit.Framework;

namespace Game.Composition.Campaign.Tests
{
    public sealed class CampaignOutcomeIntegrationTests
    {
        private static readonly EncounterId TerminalEncounter = new EncounterId("logan-castle-lower");
        private static readonly OutcomeConditionRef TerminalCondition =
            new OutcomeConditionRef("campaign:logan-castle-complete");

        [Test]
        public void CompletedAuthoredEncounterRoutesThroughSystem15ExactlyOnce()
        {
            CampaignBlueprint blueprint = BuildBlueprint();
            var authority = new OutcomeAuthorityRef("main-campaign-story");
            var outcome = new GameOutcomeRuntime(new[] { authority });
            var policy = new OutcomePolicyRouter(outcome, new[]
            {
                new OutcomePolicyRule(
                    TerminalCondition,
                    new GameOutcomeResolutionRequest(
                        new OutcomeResolutionId("main-campaign:success"),
                        authority,
                        GameOutcomeDisposition.Success,
                        new OutcomeRef("main-campaign-complete")))
            });
            int resolvedEvents = 0;
            outcome.OutcomeResolved += _ => resolvedEvents++;

            var runtime = new CampaignRuntime(
                blueprint,
                Array.Empty<CutsceneStageRealization>(),
                new NoActors(),
                new NoPresentation(),
                outcomeConditionObserver: condition =>
                {
                    if (!policy.TryObserve(condition, out GameOutcomeResolutionResult result))
                        throw new InvalidOperationException("No System15 outcome policy for " + condition + ".");
                    if (!result.Succeeded)
                        throw new InvalidOperationException("System15 rejected outcome condition " + condition + ".");
                });

            runtime.ObserveEncounter(ResolvedEncounter(EncounterResolutionResult.Failed));
            Assert.That(outcome.Snapshot().Lifecycle, Is.EqualTo(GameOutcomeLifecycle.Running),
                "Ordinary encounter failure must remain nonterminal unless authored policy maps it.");

            Assert.That(runtime.ObserveEncounter(ResolvedEncounter(EncounterResolutionResult.Completed)), Is.EqualTo(1));
            GameOutcomeSnapshot resolved = outcome.Snapshot();
            Assert.That(resolved.Lifecycle, Is.EqualTo(GameOutcomeLifecycle.Resolved));
            Assert.That(resolved.Disposition, Is.EqualTo(GameOutcomeDisposition.Success));
            Assert.That(resolved.Outcome, Is.EqualTo(new OutcomeRef("main-campaign-complete")));
            Assert.That(resolvedEvents, Is.EqualTo(1));

            Assert.That(runtime.ObserveEncounter(ResolvedEncounter(EncounterResolutionResult.Completed)), Is.EqualTo(1));
            Assert.That(outcome.Snapshot().Revision, Is.EqualTo(resolved.Revision));
            Assert.That(resolvedEvents, Is.EqualTo(1),
                "Repeated semantic observation must be idempotent in System15 and emit no duplicate resolution event.");
        }

        [Test]
        public void CampaignRejectsUnresolvedEncounterAsStoryFact()
        {
            var runtime = new CampaignRuntime(
                BuildBlueprint(),
                Array.Empty<CutsceneStageRealization>(),
                new NoActors(),
                new NoPresentation());

            var active = new EncounterSnapshot(
                new EncounterDefinition(TerminalEncounter, EncounterCombatPolicy.Required, "boss"),
                EncounterLifecycleState.Active,
                new EncounterMembershipSnapshot(Array.Empty<EncounterParticipant>()),
                null,
                "player-entered",
                "",
                1);

            Assert.Throws<InvalidOperationException>(() => runtime.ObserveEncounter(active));
        }

        private static CampaignBlueprint BuildBlueprint()
        {
            var game = Campaign.Create("outcome-integration");
            game.Story.Rule("terminal-outcome", rule => rule
                .When(StoryTrigger.EncounterResolved(TerminalEncounter, EncounterResolutionResult.Completed))
                .Then(StoryEffect.ObserveOutcomeCondition(TerminalCondition)));
            return game.Build();
        }

        private static EncounterSnapshot ResolvedEncounter(EncounterResolutionResult result) =>
            new EncounterSnapshot(
                new EncounterDefinition(TerminalEncounter, EncounterCombatPolicy.Required, "boss"),
                EncounterLifecycleState.Resolved,
                new EncounterMembershipSnapshot(Array.Empty<EncounterParticipant>()),
                new EncounterResolution(result, "authoritative encounter resolution"),
                "player-entered",
                "",
                2);

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
