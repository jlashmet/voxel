using System;
using Game.Outcomes.Api;
using Game.Outcomes.Runtime;
using NUnit.Framework;

namespace Game.Outcomes.Tests
{
    public sealed class GameOutcomeRuntimeTests
    {
        private static readonly OutcomeAuthorityRef CampaignAuthority =
            new OutcomeAuthorityRef("campaign-policy");

        [Test]
        public void UnmappedCombatLossLeavesOutcomeRunning()
        {
            var runtime = NewRuntime();
            var policy = new OutcomePolicyRouter(runtime, Array.Empty<OutcomePolicyRule>());

            bool matched = policy.TryObserve(
                new OutcomeConditionRef("combat:player-team-defeated"),
                out GameOutcomeResolutionResult result);

            Assert.That(matched, Is.False);
            Assert.That(result.Status, Is.EqualTo(default(GameOutcomeResolutionStatus)));
            Assert.That(runtime.Snapshot().Lifecycle, Is.EqualTo(GameOutcomeLifecycle.Running));
            Assert.That(runtime.Snapshot().Revision, Is.EqualTo(0));
        }

        [Test]
        public void ConfiguredCampaignSuccessResolvesExactlyOnce()
        {
            var runtime = NewRuntime();
            var condition = new OutcomeConditionRef("campaign:main-objective-complete");
            GameOutcomeResolutionRequest request = SuccessRequest("resolution:campaign-success");
            var policy = new OutcomePolicyRouter(
                runtime,
                new[] { new OutcomePolicyRule(condition, request) });
            int emitted = 0;
            GameOutcomeResolved observed = default;
            IGameOutcomeEvents events = runtime;
            events.OutcomeResolved += resolved =>
            {
                emitted++;
                observed = resolved;
            };

            Assert.That(policy.TryObserve(condition, out GameOutcomeResolutionResult first), Is.True);
            Assert.That(first.Status, Is.EqualTo(GameOutcomeResolutionStatus.Accepted));
            Assert.That(policy.TryObserve(condition, out GameOutcomeResolutionResult duplicate), Is.True);
            Assert.That(duplicate.Status, Is.EqualTo(GameOutcomeResolutionStatus.Idempotent));

            GameOutcomeSnapshot snapshot = runtime.Snapshot();
            Assert.That(snapshot.Lifecycle, Is.EqualTo(GameOutcomeLifecycle.Resolved));
            Assert.That(snapshot.Disposition, Is.EqualTo(GameOutcomeDisposition.Success));
            Assert.That(snapshot.Outcome, Is.EqualTo(new OutcomeRef("campaign:complete")));
            Assert.That(snapshot.ResolutionId, Is.EqualTo(request.ResolutionId));
            Assert.That(snapshot.Authority, Is.EqualTo(CampaignAuthority));
            Assert.That(snapshot.Revision, Is.EqualTo(1));
            Assert.That(emitted, Is.EqualTo(1));
            Assert.That(observed.ResolutionId, Is.EqualTo(request.ResolutionId));
            Assert.That(observed.Revision, Is.EqualTo(1));
        }

        [Test]
        public void PartyDefeatResolvesOnlyWhenAuthoredPolicyMapsIt()
        {
            var condition = new OutcomeConditionRef("party:all-defeated");
            var unconfigured = NewRuntime();
            var noFailurePolicy = new OutcomePolicyRouter(
                unconfigured,
                Array.Empty<OutcomePolicyRule>());

            Assert.That(
                noFailurePolicy.TryObserve(condition, out GameOutcomeResolutionResult ignored),
                Is.False);
            Assert.That(unconfigured.Snapshot().Lifecycle, Is.EqualTo(GameOutcomeLifecycle.Running));

            var configured = NewRuntime();
            var failureRequest = new GameOutcomeResolutionRequest(
                new OutcomeResolutionId("resolution:party-defeat"),
                CampaignAuthority,
                GameOutcomeDisposition.Failure,
                new OutcomeRef("campaign:party-defeated"));
            var failurePolicy = new OutcomePolicyRouter(
                configured,
                new[] { new OutcomePolicyRule(condition, failureRequest) });

            Assert.That(
                failurePolicy.TryObserve(condition, out GameOutcomeResolutionResult result),
                Is.True);
            Assert.That(result.Status, Is.EqualTo(GameOutcomeResolutionStatus.Accepted));
            Assert.That(configured.Snapshot().Disposition, Is.EqualTo(GameOutcomeDisposition.Failure));
            Assert.That(configured.Snapshot().Outcome, Is.EqualTo(new OutcomeRef("campaign:party-defeated")));
        }

        [Test]
        public void DuplicateAndCompetingRequestsKeepFirstWinnerAndEmitOneEvent()
        {
            var storyAuthority = new OutcomeAuthorityRef("story-policy");
            var runtime = new GameOutcomeRuntime(new[] { CampaignAuthority, storyAuthority });
            GameOutcomeResolutionRequest winner = SuccessRequest("resolution:first");
            var competing = new GameOutcomeResolutionRequest(
                new OutcomeResolutionId("resolution:second"),
                storyAuthority,
                GameOutcomeDisposition.Failure,
                new OutcomeRef("campaign:failed"));
            var reusedIdWithDifferentPayload = new GameOutcomeResolutionRequest(
                winner.ResolutionId,
                CampaignAuthority,
                GameOutcomeDisposition.Failure,
                new OutcomeRef("campaign:changed"));
            int emitted = 0;
            runtime.OutcomeResolved += _ => emitted++;

            GameOutcomeResolutionResult first = runtime.RequestResolution(winner);
            GameOutcomeResolutionResult duplicate = runtime.RequestResolution(winner);
            GameOutcomeResolutionResult changedDuplicate =
                runtime.RequestResolution(reusedIdWithDifferentPayload);
            GameOutcomeResolutionResult lateCompetitor = runtime.RequestResolution(competing);

            Assert.That(first.Status, Is.EqualTo(GameOutcomeResolutionStatus.Accepted));
            Assert.That(duplicate.Status, Is.EqualTo(GameOutcomeResolutionStatus.Idempotent));
            Assert.That(changedDuplicate.Status, Is.EqualTo(GameOutcomeResolutionStatus.RejectedAlreadyResolved));
            Assert.That(lateCompetitor.Status, Is.EqualTo(GameOutcomeResolutionStatus.RejectedAlreadyResolved));
            Assert.That(runtime.Snapshot().ResolutionId, Is.EqualTo(winner.ResolutionId));
            Assert.That(runtime.Snapshot().Disposition, Is.EqualTo(GameOutcomeDisposition.Success));
            Assert.That(runtime.Snapshot().Revision, Is.EqualTo(1));
            Assert.That(emitted, Is.EqualTo(1));
        }

        [Test]
        public void FirstAuthoredRuleWinsWhenOneConditionHasCompetingMappings()
        {
            var runtime = NewRuntime();
            var condition = new OutcomeConditionRef("campaign:terminal");
            GameOutcomeResolutionRequest first = SuccessRequest("resolution:ordered-first");
            var second = new GameOutcomeResolutionRequest(
                new OutcomeResolutionId("resolution:ordered-second"),
                CampaignAuthority,
                GameOutcomeDisposition.Failure,
                new OutcomeRef("campaign:alternate"));
            var policy = new OutcomePolicyRouter(
                runtime,
                new[]
                {
                    new OutcomePolicyRule(condition, first),
                    new OutcomePolicyRule(condition, second)
                });

            Assert.That(policy.TryObserve(condition, out GameOutcomeResolutionResult result), Is.True);
            Assert.That(result.Status, Is.EqualTo(GameOutcomeResolutionStatus.Accepted));
            Assert.That(runtime.Snapshot().ResolutionId, Is.EqualTo(first.ResolutionId));
        }

        [Test]
        public void UnauthorizedAuthorityCannotResolveRunningGame()
        {
            var runtime = NewRuntime();
            var unauthorized = new GameOutcomeResolutionRequest(
                new OutcomeResolutionId("resolution:unauthorized"),
                new OutcomeAuthorityRef("combat-runtime"),
                GameOutcomeDisposition.Failure,
                new OutcomeRef("campaign:combat-loss"));

            GameOutcomeResolutionResult result = runtime.RequestResolution(unauthorized);

            Assert.That(result.Status, Is.EqualTo(GameOutcomeResolutionStatus.RejectedUnauthorized));
            Assert.That(runtime.Snapshot().Lifecycle, Is.EqualTo(GameOutcomeLifecycle.Running));
            Assert.That(runtime.Snapshot().Revision, Is.EqualTo(0));
        }

        [Test]
        public void TechnicalShutdownWithoutGameplayPolicyCreatesNoOutcome()
        {
            var runtime = NewRuntime();
            var policy = new OutcomePolicyRouter(
                runtime,
                new[]
                {
                    new OutcomePolicyRule(
                        new OutcomeConditionRef("campaign:main-objective-complete"),
                        SuccessRequest("resolution:campaign-success"))
                });

            bool matched = policy.TryObserve(
                new OutcomeConditionRef("technical:server-shutdown"),
                out GameOutcomeResolutionResult ignored);

            Assert.That(matched, Is.False);
            Assert.That(runtime.Snapshot().Lifecycle, Is.EqualTo(GameOutcomeLifecycle.Running));
        }

        [Test]
        public void RestoredResolvedSnapshotDoesNotReplayHistoricalResolution()
        {
            var original = NewRuntime();
            GameOutcomeResolutionRequest winner = SuccessRequest("resolution:persisted");
            Assert.That(
                original.RequestResolution(winner).Status,
                Is.EqualTo(GameOutcomeResolutionStatus.Accepted));
            GameOutcomeSnapshot persisted = original.Snapshot();

            var restored = new GameOutcomeRuntime(new[] { CampaignAuthority }, persisted);
            int emittedAfterRestore = 0;
            restored.OutcomeResolved += _ => emittedAfterRestore++;

            Assert.That(restored.Snapshot().Lifecycle, Is.EqualTo(GameOutcomeLifecycle.Resolved));
            Assert.That(restored.Snapshot().ResolutionId, Is.EqualTo(winner.ResolutionId));
            Assert.That(restored.Snapshot().Revision, Is.EqualTo(1));
            Assert.That(emittedAfterRestore, Is.EqualTo(0));

            GameOutcomeResolutionResult replay = restored.RequestResolution(winner);
            var competitor = new GameOutcomeResolutionRequest(
                new OutcomeResolutionId("resolution:late-after-restore"),
                CampaignAuthority,
                GameOutcomeDisposition.Failure,
                new OutcomeRef("campaign:late-failure"));
            GameOutcomeResolutionResult late = restored.RequestResolution(competitor);

            Assert.That(replay.Status, Is.EqualTo(GameOutcomeResolutionStatus.Idempotent));
            Assert.That(late.Status, Is.EqualTo(GameOutcomeResolutionStatus.RejectedAlreadyResolved));
            Assert.That(restored.Snapshot().ResolutionId, Is.EqualTo(winner.ResolutionId));
            Assert.That(restored.Snapshot().Revision, Is.EqualTo(1));
            Assert.That(emittedAfterRestore, Is.EqualTo(0));
        }

        private static GameOutcomeRuntime NewRuntime() =>
            new GameOutcomeRuntime(new[] { CampaignAuthority });

        private static GameOutcomeResolutionRequest SuccessRequest(string resolutionId) =>
            new GameOutcomeResolutionRequest(
                new OutcomeResolutionId(resolutionId),
                CampaignAuthority,
                GameOutcomeDisposition.Success,
                new OutcomeRef("campaign:complete"));
    }
}
