using System;
using System.Collections.Generic;
using Game.Composition.Kentridge.Playable;
using Game.GameplayReplication.Api;
using Game.SessionOrchestration.Api;
using Game.SessionOrchestration.Runtime;
using Game.SessionPresentation.Api;
using Game.Sessions.Api;
using Game.Sessions.Runtime;
using NUnit.Framework;
using VoxelEngine.Net.Api;
using VoxelEngine.Net.Runtime.Server;

namespace Game.Composition.Kentridge.Playable.Tests
{
    public sealed class KentridgeMultiplayerRuntimeTests
    {
        [Test]
        public void ReadyAdmissionMarksOnlyAuthorityBoundDurableMemberGameplayReady()
        {
            PartySession session = CreateSession();
            JoinResult join = session.Join(new JoinRequest(new GameSessionId("live"), "client-a", "v1", "content"));
            Assert.That(join.Accepted, Is.True);
            var application = new PartySessionApplication(session, 3);
            var inner = new BindingAdmission(session, join.Member.MemberId, bind: true);
            var consumer = new KentridgeReadySessionAdmissionConsumer(inner, session, application);

            consumer.HandleSessionAdmission(17, ReadOnlySpan<byte>.Empty);

            Assert.That(session.TryGetMember(join.Member.MemberId, out PartyMemberSnapshot member), Is.True);
            Assert.That(member.Presence, Is.EqualTo(PartyPresenceState.Connected));
            Assert.That(member.Readiness, Is.EqualTo(SessionReadinessState.GameplayReady));
            Assert.That(application.Snapshot().TryIsReady(join.Member.MemberId, out bool ready), Is.True);
            Assert.That(ready, Is.True);
        }

        [Test]
        public void ReadyAdmissionDoesNotManufactureReadinessWhenNetworkBindingFails()
        {
            PartySession session = CreateSession();
            JoinResult join = session.Join(new JoinRequest(new GameSessionId("live"), "client-a", "v1", "content"));
            var application = new PartySessionApplication(session, 3);
            var consumer = new KentridgeReadySessionAdmissionConsumer(
                new BindingAdmission(session, join.Member.MemberId, bind: false), session, application);

            consumer.HandleSessionAdmission(17, ReadOnlySpan<byte>.Empty);

            Assert.That(session.TryGetMember(join.Member.MemberId, out PartyMemberSnapshot member), Is.True);
            Assert.That(member.Presence, Is.EqualTo(PartyPresenceState.Joined));
            Assert.That(member.Readiness, Is.EqualTo(SessionReadinessState.Joined));
            Assert.That(application.Snapshot().TryIsReady(join.Member.MemberId, out bool ready), Is.True);
            Assert.That(ready, Is.False);
        }

        [Test]
        public void SessionApplicationProjectionPublishesCapacityReadyIntentAndAuthoritativeStart()
        {
            PartySession session = CreateSession();
            JoinResult join = session.Join(new JoinRequest(new GameSessionId("live"), "host", "v1", "content"));
            Assert.That(session.BindConnection(join.Member.MemberId,
                SessionNetworkAdmissionAdapter.FromConnectionId(1)), Is.True);
            Assert.That(session.MarkSynchronized(join.Member.MemberId), Is.True);
            Assert.That(session.MarkGameplayReady(join.Member.MemberId), Is.True);
            var application = new PartySessionApplication(session, 3);
            Assert.That(application.SetReady(join.Member.MemberId, true).Accepted, Is.True);
            var source = new KentridgeSessionApplicationGameplayProjectionSource(application);

            GameplayProjectionState before = source.Capture();
            Assert.That(Value(before, "capacity"), Is.EqualTo("3"));
            Assert.That(Value(before, "gameplay-started"), Is.EqualTo("false"));
            Assert.That(Value(before, "member/" + join.Member.MemberId.Value + "/ready"), Is.EqualTo("true"));

            Assert.That(application.RequestStart(join.Member.MemberId).Accepted, Is.True);
            GameplayProjectionState after = source.Capture();
            Assert.That(Value(after, "gameplay-started"), Is.EqualTo("true"));
        }

        [Test]
        public void JoiningFormationWaitsForMatchingActiveReplicatedDurableMember()
        {
            var sessionId = new GameSessionId("live");
            var memberId = new PartyMemberId("live:member:2");
            var inner = new FakeFormationService(SessionFormationResult.Success(sessionId, memberId));
            var readState = new FakeReadState();
            int pumps = 0;
            var service = new KentridgeGameplayReadyFormationService(inner, readState, () => pumps++);
            ISessionFormationOperation operation = service.BeginJoin(new JoinSessionRequest(
                new JoinRequest(sessionId, "client-a", "v1", "content")));

            Assert.That(operation.TryGetResult(out _), Is.False);
            Assert.That(pumps, Is.EqualTo(1));

            readState.SetActive(sessionId, memberId, slot: 1, capacity: 3);
            Assert.That(operation.TryGetResult(out SessionFormationResult result), Is.True);
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.LocalMemberId, Is.EqualTo(memberId));
            Assert.That(pumps, Is.EqualTo(2));
        }

        [Test]
        public void HostFormationCompletesAfterAdmissionWithoutWaitingForGameplayStart()
        {
            var sessionId = new GameSessionId("live");
            var memberId = new PartyMemberId("live:member:1");
            var inner = new FakeFormationService(SessionFormationResult.Success(sessionId, memberId));
            var service = new KentridgeGameplayReadyFormationService(inner, new FakeReadState(), () => { });
            var config = new SessionStartupConfiguration(3, "v1", "content", true);
            ISessionFormationOperation operation = service.BeginHost(new HostSessionRequest(sessionId, config, "host"));

            Assert.That(operation.TryGetResult(out SessionFormationResult result), Is.True);
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.LocalMemberId, Is.EqualTo(memberId));
        }

        [Test]
        public void ReplicatedPartyQueryUsesAuthoritativeRosterAndMarksOnlyDurableLocalMember()
        {
            var sessionId = new GameSessionId("live");
            var local = new PartyMemberId("live:member:2");
            var readState = new FakeReadState();
            readState.SetActive(sessionId, local, slot: 1, capacity: 3, includeLeader: true);
            var query = new KentridgeReplicatedPartyScreenQuery(readState);

            PartyScreenPresentationSnapshot snapshot = query.CapturePartyScreen(local);

            Assert.That(snapshot, Is.Not.Null);
            Assert.That(snapshot.SessionId, Is.EqualTo(sessionId));
            Assert.That(snapshot.Capacity, Is.EqualTo(3));
            Assert.That(snapshot.Lifecycle, Is.EqualTo(SessionPresentationLifecycle.Active));
            Assert.That(snapshot.Members.Count, Is.EqualTo(2));
            Assert.That(snapshot.Members[0].IsLocal, Is.False);
            Assert.That(snapshot.Members[1].IsLocal, Is.True);
            Assert.That(snapshot.Members[1].MemberId, Is.EqualTo(local));
            Assert.That(snapshot.Members[1].GameplayReady, Is.True);
        }

        [Test]
        public void ReplicatedClientGraphRunsOnlyReplicationPumpThroughSharedOrchestrator()
        {
            var sessionId = new GameSessionId("live");
            var memberId = new PartyMemberId("live:member:2");
            var readState = new FakeReadState();
            readState.SetActive(sessionId, memberId, slot: 1, capacity: 3);
            int pumps = 0;
            var factory = new KentridgeReplicatedClientSessionGraphFactory(readState, () => pumps++);
            var orchestrator = new GameSessionOrchestrator(factory);
            var identity = new GameSessionIdentity("campaign", "world", sessionId.Value, "config");

            Assert.That(orchestrator.Prepare(GameSessionStartRequest.NewGame(identity)).Succeeded, Is.True);
            Assert.That(orchestrator.EnterRunning().Succeeded, Is.True);
            Assert.That(orchestrator.Tick(16).Succeeded, Is.True);
            Assert.That(pumps, Is.EqualTo(1));
            Assert.That(orchestrator.Snapshot.GameplayReady, Is.True);
            Assert.That(orchestrator.Shutdown().Succeeded, Is.True);
        }

        private static PartySession CreateSession() => new PartySession(
            new GameSessionId("live"), new SessionStartupConfiguration(3, "v1", "content", true));

        private static string Value(GameplayProjectionState state, string key)
        {
            for (int i = 0; i < state.Entries.Count; i++)
                if (state.Entries[i].Key == key) return state.Entries[i].Value;
            return null;
        }

        private sealed class BindingAdmission : IAuthoritativeSessionAdmissionConsumer
        {
            private readonly PartySession _session;
            private readonly PartyMemberId _memberId;
            private readonly bool _bind;
            public BindingAdmission(PartySession session, PartyMemberId memberId, bool bind)
            {
                _session = session;
                _memberId = memberId;
                _bind = bind;
            }
            public void HandleSessionAdmission(uint connectionId, ReadOnlySpan<byte> payload)
            {
                if (_bind)
                    _session.BindConnection(_memberId,
                        SessionNetworkAdmissionAdapter.FromConnectionId(connectionId));
            }
        }

        private sealed class FakeFormationService : IAsyncSessionFormationService
        {
            private readonly SessionFormationResult _result;
            public FakeFormationService(SessionFormationResult result) => _result = result;
            public ISessionFormationOperation BeginHost(HostSessionRequest request) => new Operation(_result);
            public ISessionFormationOperation BeginJoin(JoinSessionRequest request) => new Operation(_result);
            public SessionFormationResult Host(HostSessionRequest request) => _result;
            public SessionFormationResult Join(JoinSessionRequest request) => _result;

            private sealed class Operation : ISessionFormationOperation
            {
                private readonly SessionFormationResult _result;
                private bool _cancelled;
                public Operation(SessionFormationResult result) => _result = result;
                public bool TryGetResult(out SessionFormationResult result)
                {
                    result = _result;
                    return !_cancelled;
                }
                public void Cancel() => _cancelled = true;
            }
        }

        private sealed class FakeReadState : IGameplayReplicationReadState
        {
            private readonly Dictionary<GameplayProjectionId, GameplayProjectionState> _states =
                new Dictionary<GameplayProjectionId, GameplayProjectionState>();
            public GameplayRevision Revision { get; private set; }
            public GameplaySynchronizationState SynchronizationState { get; private set; }
            public bool GameplayReady { get; private set; }
            public bool TryGetProjection(GameplayProjectionId id, out GameplayProjectionState state) =>
                _states.TryGetValue(id, out state);

            public void SetActive(
                GameSessionId sessionId,
                PartyMemberId localMember,
                int slot,
                int capacity,
                bool includeLeader = false)
            {
                var sessionsDescriptor = new GameplayProjectionDescriptor(
                    KentridgeReplicatedPartyState.SessionsProjectionId, 1, true);
                var sessionEntries = new List<GameplayProjectionEntry>
                {
                    new GameplayProjectionEntry("session-id", sessionId.Value)
                };
                if (includeLeader)
                {
                    AddMember(sessionEntries, 0, new PartyMemberId(sessionId.Value + ":member:1"),
                        PartyLeadershipRole.Leader, "kentridge-player-1");
                }
                AddMember(sessionEntries, slot, localMember,
                    includeLeader ? PartyLeadershipRole.Member : PartyLeadershipRole.Leader,
                    "kentridge-player-" + (slot + 1));
                _states[KentridgeReplicatedPartyState.SessionsProjectionId] =
                    new GameplayProjectionState(sessionsDescriptor, sessionEntries);

                var appDescriptor = new GameplayProjectionDescriptor(
                    KentridgeSessionApplicationGameplayProjectionSource.ProjectionId, 1, true);
                var appEntries = new List<GameplayProjectionEntry>
                {
                    new GameplayProjectionEntry("capacity", capacity.ToString()),
                    new GameplayProjectionEntry("gameplay-started", "true"),
                    new GameplayProjectionEntry("member/" + localMember.Value + "/ready", "true")
                };
                if (includeLeader)
                    appEntries.Add(new GameplayProjectionEntry(
                        "member/" + sessionId.Value + ":member:1/ready", "true"));
                _states[KentridgeSessionApplicationGameplayProjectionSource.ProjectionId] =
                    new GameplayProjectionState(appDescriptor, appEntries);
                Revision = new GameplayRevision(1);
                SynchronizationState = GameplaySynchronizationState.Synchronized;
                GameplayReady = true;
            }

            private static void AddMember(
                List<GameplayProjectionEntry> entries,
                int slot,
                PartyMemberId memberId,
                PartyLeadershipRole leadership,
                string characterId)
            {
                string prefix = "slot/" + slot + "/";
                entries.Add(new GameplayProjectionEntry(prefix + "member-id", memberId.Value));
                entries.Add(new GameplayProjectionEntry(prefix + "leadership", leadership.ToString()));
                entries.Add(new GameplayProjectionEntry(prefix + "presence", PartyPresenceState.Connected.ToString()));
                entries.Add(new GameplayProjectionEntry(prefix + "readiness", SessionReadinessState.GameplayReady.ToString()));
                entries.Add(new GameplayProjectionEntry(prefix + "character-id", characterId));
            }
        }
    }
}
