using Game.GameplayReplication.Api;
using Game.Sessions.Api;
using NUnit.Framework;

namespace Game.Composition.Kentridge.Playable.Tests
{
    public sealed class KentridgeContinuityReplicationStateTests
    {
        [Test]
        public void RecoveryBecomesReadyOnlyAfterAuthoritativeRevisionAdvances()
        {
            GameplayRevision current = new GameplayRevision(5);
            var state = new KentridgeContinuityReplicationState(() => current);
            var member = new PartyMemberId("member-1");

            state.RequestRecovery(member, GameplayRecoveryMode.FullSnapshot);
            Assert.That(state.TryGetSynchronization(member, out GameplaySynchronizationStatus pending), Is.True);
            Assert.That(pending.GameplayReady, Is.False);
            Assert.That(pending.Revision, Is.EqualTo(new GameplayRevision(5)));

            current = new GameplayRevision(6);
            Assert.That(state.TryGetSynchronization(member, out GameplaySynchronizationStatus ready), Is.True);
            Assert.That(ready.GameplayReady, Is.True);
            Assert.That(ready.Revision, Is.EqualTo(new GameplayRevision(6)));
        }
    }
}
