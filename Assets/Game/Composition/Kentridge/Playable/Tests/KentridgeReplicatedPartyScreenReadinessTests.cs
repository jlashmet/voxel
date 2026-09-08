using System.Collections.Generic;
using Game.Composition.Kentridge.Playable;
using Game.GameplayReplication.Api;
using Game.SessionPresentation.Api;
using Game.Sessions.Api;
using NUnit.Framework;

namespace Game.Composition.Kentridge.Playable.Tests
{
    public sealed class KentridgeReplicatedPartyScreenReadinessTests
    {
        [Test]
        public void ReplicatedPartyQuerySuppressesStaleActivePartyWhileGameplayStateResynchronizes()
        {
            var local = new PartyMemberId("live:member:2");
            var readState = new CachedActiveReadState(local);
            var query = new KentridgeReplicatedPartyScreenQuery(readState);

            PartyScreenPresentationSnapshot ready = query.CapturePartyScreen(local);
            Assert.That(ready, Is.Not.Null);
            Assert.That(ready.Lifecycle, Is.EqualTo(SessionPresentationLifecycle.Active));

            readState.GameplayReady = false;
            Assert.That(query.CapturePartyScreen(local), Is.Null,
                "Cached active party projections must not authorize local graph startup while gameplay state is resynchronizing.");

            readState.GameplayReady = true;
            PartyScreenPresentationSnapshot recovered = query.CapturePartyScreen(local);
            Assert.That(recovered, Is.Not.Null);
            Assert.That(recovered.Lifecycle, Is.EqualTo(SessionPresentationLifecycle.Active));
        }

        private sealed class CachedActiveReadState : IGameplayReplicationReadState
        {
            private readonly Dictionary<GameplayProjectionId, GameplayProjectionState> _states =
                new Dictionary<GameplayProjectionId, GameplayProjectionState>();

            public CachedActiveReadState(PartyMemberId local)
            {
                var sessionsDescriptor = new GameplayProjectionDescriptor(
                    KentridgeReplicatedPartyState.SessionsProjectionId, 1, true);
                _states[KentridgeReplicatedPartyState.SessionsProjectionId] =
                    new GameplayProjectionState(sessionsDescriptor, new[]
                    {
                        new GameplayProjectionEntry("session-id", "live"),
                        new GameplayProjectionEntry("slot/0/member-id", "live:member:1"),
                        new GameplayProjectionEntry("slot/0/leadership", PartyLeadershipRole.Leader.ToString()),
                        new GameplayProjectionEntry("slot/0/presence", PartyPresenceState.Connected.ToString()),
                        new GameplayProjectionEntry("slot/0/readiness", SessionReadinessState.GameplayReady.ToString()),
                        new GameplayProjectionEntry("slot/0/character-id", "kentridge-player-1"),
                        new GameplayProjectionEntry("slot/1/member-id", local.Value),
                        new GameplayProjectionEntry("slot/1/leadership", PartyLeadershipRole.Member.ToString()),
                        new GameplayProjectionEntry("slot/1/presence", PartyPresenceState.Connected.ToString()),
                        new GameplayProjectionEntry("slot/1/readiness", SessionReadinessState.GameplayReady.ToString()),
                        new GameplayProjectionEntry("slot/1/character-id", "kentridge-player-2")
                    });

                var applicationDescriptor = new GameplayProjectionDescriptor(
                    KentridgeSessionApplicationGameplayProjectionSource.ProjectionId, 1, true);
                _states[KentridgeSessionApplicationGameplayProjectionSource.ProjectionId] =
                    new GameplayProjectionState(applicationDescriptor, new[]
                    {
                        new GameplayProjectionEntry("capacity", "3"),
                        new GameplayProjectionEntry("gameplay-started", "true"),
                        new GameplayProjectionEntry("member/live:member:1/ready", "true"),
                        new GameplayProjectionEntry("member/" + local.Value + "/ready", "true")
                    });

                Revision = new GameplayRevision(7);
                SynchronizationState = GameplaySynchronizationState.Synchronized;
                GameplayReady = true;
            }

            public GameplayRevision Revision { get; }
            public GameplaySynchronizationState SynchronizationState { get; }
            public bool GameplayReady { get; set; }

            public bool TryGetProjection(GameplayProjectionId id, out GameplayProjectionState state) =>
                _states.TryGetValue(id, out state);
        }
    }
}
