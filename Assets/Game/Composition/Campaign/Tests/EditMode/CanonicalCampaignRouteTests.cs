using System;
using System.Collections.Generic;
using Game.Characters.Api;
using Game.Composition.Campaign.Content;
using Game.Composition.Campaign.Runtime;
using Game.Cutscenes.Api;
using Game.Encounters.Api;
using Game.Encounters.Runtime;
using Game.Outcomes.Api;
using Game.Outcomes.Runtime;
using Game.Progression.Api;
using Game.WorldBuilder.Api;
using NUnit.Framework;

namespace Game.Composition.Campaign.Tests
{
    public sealed class CanonicalCampaignRouteTests
    {
        [Test]
        public void NormalNewGameRouteCrossesRecoveredConsequencesRestoreAndResolvesExactlyOnce()
        {
            AuthoredFullRunCampaignContent content = BuildContent();
            IReadOnlyList<CutsceneStageRealization> stages = BuildStages(content.Blueprint);
            var actors = new AnyActorProvider();
            var presentation = new ImmediatePresentation();
            var authority = new OutcomeAuthorityRef("main-campaign-story");
            var outcomes = new GameOutcomeRuntime(new[] { authority });
            var policy = new OutcomePolicyRouter(outcomes, new[]
            {
                new OutcomePolicyRule(
                    content.CompletionCondition,
                    new GameOutcomeResolutionRequest(
                        new OutcomeResolutionId("main-campaign:canonical-success"),
                        authority,
                        GameOutcomeDisposition.Success,
                        new OutcomeRef("main-campaign-complete")))
            });
            int outcomeEvents = 0;
            outcomes.OutcomeResolved += _ => outcomeEvents++;

            Action<OutcomeConditionRef> observeOutcome = condition =>
            {
                if (!policy.TryObserve(condition, out GameOutcomeResolutionResult result))
                    throw new InvalidOperationException("No System15 outcome policy for " + condition + ".");
                if (!result.Succeeded)
                    throw new InvalidOperationException("System15 rejected outcome condition " + condition + ".");
            };

            CampaignRuntime runtime = CreateRuntime(content, stages, actors, presentation, observeOutcome);
            var encounters = new EncounterRegistry(new EmptyCharacters());
            string milestone = "new-game";

            Assert.That(runtime.StartNewGame(), Is.GreaterThan(0));
            CompleteCutscene(runtime, content.IntroCutscene, milestone);
            milestone = "opening-intro-completed";

            // The well quest is intentionally optional. It starts normally but is never completed by
            // this canonical route; completion of the run must not depend on it.
            Assert.That(runtime.IsQuestActive(content.OptionalWellQuest), Is.True);

            Assert.That(runtime.InteractWithNpc(content.OpeningRoles.Awon.Ref), Is.GreaterThan(0));
            CompleteActiveCutscene(runtime, milestone);
            milestone = "awon-opening-completed";

            Assert.That(runtime.EnterSite(content.OpeningRoles.MedrareSite.Ref), Is.GreaterThan(0));
            CompleteActiveCutscene(runtime, milestone);
            milestone = "see-medrare-completed";

            Assert.That(runtime.InteractWithNpc(content.OpeningRoles.Medrare.Ref), Is.GreaterThan(0));
            CompleteActiveCutscene(runtime, milestone);
            milestone = "medrare-joined";
            Assert.That(runtime.IsPartyMemberJoined("Medrare"), Is.True);

            Assert.That(runtime.EnterSite(content.OpeningRoles.MedrareHouseSite.Ref), Is.GreaterThan(0));
            CompleteActiveCutscene(runtime, milestone); // first spell; completion chains to church cutscene
            milestone = "medrare-first-spell-completed";
            Assert.That(runtime.HasSpell("Flame"), Is.True);
            CompleteCutscene(runtime, content.MedrareToChurchCutscene, milestone);
            milestone = "medrare-to-church-completed";
            Assert.That(runtime.IsObjectiveActive(content.ChurchObjective), Is.True);

            Assert.That(runtime.InteractWithNpc(content.Angel), Is.GreaterThan(0));
            CompleteCutscene(runtime, content.AngelGiveQuestCutscene, milestone);
            milestone = "angel-give-quest-completed";
            Assert.That(runtime.IsObjectiveActive(content.RorikObjective), Is.True);

            Assert.That(runtime.InteractWithNpc(content.Rorik), Is.GreaterThan(0));
            CompleteCutscene(runtime, content.RorikChallengeCutscene, milestone);
            milestone = "rorik-challenge-completed";
            ResolveEncounter(encounters, runtime, content.RorikEncounter);
            milestone = "rorik-encounter-completed";
            Assert.That(runtime.IsObjectiveActive(content.MoordellObjective), Is.True);

            Assert.That(runtime.InteractWithNpc(content.MoordellContact), Is.GreaterThan(0));
            CompleteCutscene(runtime, content.MoordellDistributionCutscene, milestone);
            milestone = "moordell-distribution-completed";
            Assert.That(runtime.IsObjectiveActive(content.RossdamObjective), Is.True);

            Assert.That(runtime.InteractWithNpc(content.RossdamContact), Is.GreaterThan(0));
            CompleteCutscene(runtime, content.RossdamBattleStartCutscene, milestone);
            milestone = "rossdam-battle-start-completed";
            ResolveEncounter(encounters, runtime, content.RossdamBattleEncounter);
            CompleteCutscene(runtime, content.RossdamBattleEndCutscene, milestone);
            milestone = "rossdam-battle-end-completed";
            Assert.That(runtime.IsObjectiveActive(content.MayorObjective), Is.True);

            // Meaningful mid-run restore after multiple post-opening consequences. Restore into a
            // fresh CampaignRuntime and assert current progression without replaying old cutscenes.
            CampaignProgressSnapshot saved = runtime.CaptureProgress();
            runtime = CreateRuntime(content, stages, actors, presentation, observeOutcome);
            runtime.RestoreProgress(saved);
            Assert.That(runtime.HasActiveCutscene, Is.False, "Restore must not replay historical one-shots.");
            Assert.That(runtime.IsObjectiveActive(content.MayorObjective), Is.True);
            Assert.That(runtime.IsCutsceneCompleted(content.MoordellDistributionCutscene), Is.True);
            Assert.That(runtime.IsCutsceneCompleted(content.RossdamBattleEndCutscene), Is.True);
            milestone = "mid-run-current-state-restored";

            Assert.That(runtime.InteractWithNpc(content.KentridgeMayor), Is.GreaterThan(0));
            CompleteCutscene(runtime, content.MayorLoganLeadCutscene, milestone);
            milestone = "mayor-logan-lead-completed";
            CompleteCutscene(runtime, content.LoganBattleStartCutscene, milestone);
            milestone = "logan-battle-start-completed";

            ResolveEncounter(encounters, runtime, content.LoganBattleEncounter);
            CompleteCutscene(runtime, content.LoganBattleEndCutscene, milestone);
            milestone = "logan-battle-end-completed";
            CompleteCutscene(runtime, content.LoganCastleBattleStartCutscene, milestone);
            milestone = "logan-castle-battle-start-completed";

            ResolveEncounter(encounters, runtime, content.LoganCastleLowerEncounter);
            CompleteCutscene(runtime, content.LoganCastleHoleCutscene, milestone);
            milestone = "logan-castle-lower-logan-hole-completed";

            GameOutcomeSnapshot resolved = outcomes.Snapshot();
            Assert.That(resolved.Lifecycle, Is.EqualTo(GameOutcomeLifecycle.Resolved),
                "Canonical route dead-end after '" + milestone + "': System15 never resolved the run.");
            Assert.That(resolved.Disposition, Is.EqualTo(GameOutcomeDisposition.Success));
            Assert.That(resolved.Outcome, Is.EqualTo(new OutcomeRef("main-campaign-complete")));
            Assert.That(outcomeEvents, Is.EqualTo(1));
            Assert.That(runtime.IsQuestActive(content.OptionalWellQuest), Is.True,
                "Optional Kentridge well content must not gate canonical completion.");
        }

        [Test]
        public void DeadEndDiagnosticNamesLastSemanticMilestone()
        {
            AuthoredFullRunCampaignContent content = BuildContent();
            CampaignRuntime runtime = CreateRuntime(
                content,
                BuildStages(content.Blueprint),
                new AnyActorProvider(),
                new ImmediatePresentation(),
                _ => { });

            runtime.StartNewGame();
            CompleteCutscene(runtime, content.IntroCutscene, "new-game");
            runtime.InteractWithNpc(content.OpeningRoles.Awon.Ref);
            CompleteActiveCutscene(runtime, "opening-intro-completed");

            AssertionException failure = Assert.Throws<AssertionException>(() =>
                RequireActiveCutscene(runtime, content.AngelGiveQuestCutscene, "awon-opening-completed"));
            StringAssert.Contains("awon-opening-completed", failure.Message);
        }

        private static AuthoredFullRunCampaignContent BuildContent() =>
            AuthoredFullRunCampaignContent.Build(
                new CutsceneDefinition(
                    "test-destination-cutscene",
                    CutsceneStageSetupDefinition.Empty,
                    Array.Empty<CutsceneStep>()));

        private static CampaignRuntime CreateRuntime(
            AuthoredFullRunCampaignContent content,
            IReadOnlyList<CutsceneStageRealization> stages,
            IWorldBoundCutsceneActorProvider actors,
            ICutscenePresentation presentation,
            Action<OutcomeConditionRef> outcomeObserver) =>
            new CampaignRuntime(
                content.Blueprint,
                stages,
                actors,
                presentation,
                content.QuestDefinitions,
                outcomeObserver);

        private static IReadOnlyList<CutsceneStageRealization> BuildStages(CampaignBlueprint blueprint)
        {
            var stages = new List<CutsceneStageRealization>();
            for (var i = 0; i < blueprint.Cutscenes.Count; i++)
            {
                CutsceneSpec cutscene = blueprint.Cutscenes[i];
                if (cutscene.Definition.RequiredStagePoints.Count == 0) continue;

                var binding = new CutsceneStageBinding();
                for (var j = 0; j < cutscene.Definition.RequiredStagePoints.Count; j++)
                {
                    CutsceneStagePointId point = cutscene.Definition.RequiredStagePoints[j];
                    binding.Bind(
                        point,
                        new CutsceneStagePoint(
                            new CutsceneInt3(j * 10, 0, 20),
                            new CutsceneInt3(0, 0, 1)));
                }
                stages.Add(new CutsceneStageRealization(cutscene.Ref, cutscene.Site, binding));
            }
            return stages;
        }

        private static void ResolveEncounter(
            EncounterRegistry encounters,
            CampaignRuntime runtime,
            EncounterId encounter)
        {
            Assert.That(
                encounters.Register(new EncounterDefinition(encounter, EncounterCombatPolicy.Required, "campaign")),
                Is.EqualTo(EncounterFailure.None));
            Assert.That(encounters.Activate(encounter, "player-entered"), Is.EqualTo(EncounterFailure.None));
            Assert.That(encounters.ApplyCombatResolved(encounter, victory: true), Is.EqualTo(EncounterFailure.None));
            Assert.That(encounters.TryGet(encounter, out EncounterSnapshot snapshot), Is.True);
            Assert.That(runtime.ObserveEncounter(snapshot), Is.GreaterThan(0),
                "Canonical route dead-end after encounter '" + encounter + "'.");
        }

        private static void CompleteActiveCutscene(CampaignRuntime runtime, string lastMilestone)
        {
            Assert.That(runtime.HasActiveCutscene, Is.True,
                "Canonical route dead-end after '" + lastMilestone + "': expected an active cutscene.");
            CutsceneRef active = runtime.ActiveCutscene;
            CompleteCutscene(runtime, active, lastMilestone);
        }

        private static void CompleteCutscene(
            CampaignRuntime runtime,
            CutsceneRef expected,
            string lastMilestone)
        {
            RequireActiveCutscene(runtime, expected, lastMilestone);
            for (var i = 0; i < 128 && runtime.HasActiveCutscene && runtime.ActiveCutscene.Equals(expected); i++)
                runtime.Tick(1000);
            if (runtime.HasActiveCutscene && runtime.ActiveCutscene.Equals(expected))
                Assert.Fail("Canonical route dead-end after '" + lastMilestone +
                            "': cutscene '" + expected + "' did not complete within bounded ticks.");
            Assert.That(runtime.IsCutsceneCompleted(expected), Is.True,
                "Canonical route dead-end after '" + lastMilestone +
                "': cutscene '" + expected + "' did not commit semantic completion.");
        }

        private static void RequireActiveCutscene(
            CampaignRuntime runtime,
            CutsceneRef expected,
            string lastMilestone)
        {
            Assert.That(runtime.HasActiveCutscene, Is.True,
                "Canonical route dead-end after '" + lastMilestone +
                "': expected cutscene '" + expected + "' but no authored rule advanced.");
            Assert.That(runtime.ActiveCutscene, Is.EqualTo(expected),
                "Canonical route dead-end after '" + lastMilestone +
                "': expected cutscene '" + expected + "'.");
        }

        private sealed class EmptyCharacters : ICharacterQuery
        {
            public IReadOnlyList<CharacterSnapshot> GetAll() => Array.Empty<CharacterSnapshot>();
            public bool TryGet(CharacterId id, out CharacterSnapshot snapshot)
            {
                snapshot = default;
                return false;
            }
            public bool TryResolve(CharacterBinding binding, out CharacterId id)
            {
                id = default;
                return false;
            }
        }

        private sealed class AnyActorProvider : IWorldBoundCutsceneActorProvider
        {
            public bool TryResolveNpc(NpcRef npc, out ICutsceneActorRuntime actor)
            {
                actor = new ImmediateActor();
                return true;
            }

            public bool TryResolvePlayer(int playerSlot, out ICutsceneActorRuntime actor)
            {
                actor = new ImmediateActor();
                return true;
            }
        }

        private sealed class ImmediateActor : ICutsceneActorRuntime
        {
            public CutsceneInt3 Position { get; private set; }

            public void PlaceAt(CutsceneStagePoint destination) => Position = destination.Position;
            public ICutsceneOperation MoveTo(CutsceneStagePoint destination, int durationHintMilliseconds)
            {
                Position = destination.Position;
                return CompletedCutsceneOperation.Instance;
            }
            public ICutsceneOperation FaceTowards(CutsceneInt3 targetPosition) => CompletedCutsceneOperation.Instance;
        }

        private sealed class ImmediatePresentation : ICutscenePresentation
        {
            public ICutsceneOperation SetCamera(CutsceneCueId cameraCue) => CompletedCutsceneOperation.Instance;
            public ICutsceneOperation ShowDialogue(CutsceneActorId speaker, CutsceneCueId dialogueCue) => CompletedCutsceneOperation.Instance;
            public ICutsceneOperation PlaySound(CutsceneCueId soundCue) => CompletedCutsceneOperation.Instance;
        }
    }
}
