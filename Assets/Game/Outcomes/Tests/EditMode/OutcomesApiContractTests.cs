using System;
using Game.Outcomes.Api;
using NUnit.Framework;

namespace Game.Outcomes.Tests
{
    public sealed class OutcomesApiContractTests
    {
        [Test]
        public void ResolvedSnapshotCarriesSemanticDispositionAndReference()
        {
            var snapshot = new GameOutcomeSnapshot(
                GameOutcomeLifecycle.Resolved,
                GameOutcomeDisposition.Success,
                new OutcomeRef("campaign:ridge-secured"),
                11);

            Assert.That(snapshot.Lifecycle, Is.EqualTo(GameOutcomeLifecycle.Resolved));
            Assert.That(snapshot.Disposition, Is.EqualTo(GameOutcomeDisposition.Success));
            Assert.That(snapshot.Outcome.Value, Is.EqualTo("campaign:ridge-secured"));
            Assert.That(snapshot.ResolutionId.IsValid, Is.True);
            Assert.That(snapshot.Authority.IsValid, Is.True);
            Assert.That(snapshot.Revision, Is.EqualTo(11));
        }

        [Test]
        public void RunningSnapshotRejectsTerminalOutcomeData()
        {
            Assert.Throws<ArgumentException>(() => new GameOutcomeSnapshot(
                GameOutcomeLifecycle.Running,
                GameOutcomeDisposition.Success,
                new OutcomeRef("campaign:invalid"),
                1));
        }

        [Test]
        public void ResolutionRequestRequiresSemanticAuthorityAndTerminalDisposition()
        {
            Assert.Throws<ArgumentException>(() => new GameOutcomeResolutionRequest(
                new OutcomeResolutionId("resolution:test"),
                default,
                GameOutcomeDisposition.Success,
                new OutcomeRef("campaign:complete")));

            Assert.Throws<ArgumentException>(() => new GameOutcomeResolutionRequest(
                new OutcomeResolutionId("resolution:test"),
                new OutcomeAuthorityRef("campaign-policy"),
                GameOutcomeDisposition.None,
                new OutcomeRef("campaign:complete")));
        }
    }
}
