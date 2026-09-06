using System;
using System.Collections.Generic;
using System.Globalization;
using Game.Characters.Api;
using Game.GameplayReplication.Api;
using Game.Outcomes.Api;
using Game.SessionOrchestration.Api;
using Game.SessionOrchestration.Runtime;
using Game.SessionPresentation.Api;
using Game.Sessions.Api;
using Game.Sessions.Runtime;
using VoxelEngine.Net.Api;
using VoxelEngine.Net.Runtime.Server;

namespace Game.Composition.Kentridge.Playable
{
    /// <summary>
    /// Replicates the Sessions application state that is not part of the durable roster itself.
    /// This remains semantic game state: capacity, explicit ready intent, and whether gameplay has started.
    /// </summary>
    public sealed class KentridgeSessionApplicationGameplayProjectionSource : IGameplayProjectionSource
    {
        public static readonly GameplayProjectionId ProjectionId = new GameplayProjectionId("session-application");
        private readonly IPartySessionApplicationQuery _application;

        public KentridgeSessionApplicationGameplayProjectionSource(
            IPartySessionApplicationQuery application,
            bool requiredForGameplayReady = true)
        {
            _application = application ?? throw new ArgumentNullException(nameof(application));
            Descriptor = new GameplayProjectionDescriptor(ProjectionId, 1, requiredForGameplayReady);
        }

        public GameplayProjectionDescriptor Descriptor { get; }

        public GameplayProjectionState Capture()
        {
            PartySessionApplicationSnapshot snapshot = _application.Snapshot();
            var ready = new List<PartyMemberReadySnapshot>(snapshot.ReadyMembers);
            ready.Sort((left, right) => left.MemberId.CompareTo(right.MemberId));
            var entries = new List<GameplayProjectionEntry>(ready.Count + 2)
            {
                new GameplayProjectionEntry("capacity", snapshot.Capacity.ToString(CultureInfo.InvariantCulture)),
                new GameplayProjectionEntry("gameplay-started", snapshot.GameplayStarted ? "true" : "false")
            };
            for (int i = 0; i < ready.Count; i++)
            {
                entries.Add(new GameplayProjectionEntry(
                    "member/" + ready[i].MemberId.Value + "/ready",
                    ready[i].ReadyToStart ? "true" : "false"));
            }
            return new GameplayProjectionState(Descriptor, entries);
        }
    }

    /// <summary>
    /// Decorates the real Sessions/Net admission consumer with the authoritative synchronization
    /// transition required before a party may start. A client never grants itself readiness.
    /// </summary>
    public sealed class KentridgeReadySessionAdmissionConsumer : IAuthoritativeSessionAdmissionConsumer
    {
        private readonly IAuthoritativeSessionAdmissionConsumer _inner;
        private readonly PartySession _session;
        private readonly PartySessionApplication _application;

        public KentridgeReadySessionAdmissionConsumer(
            IAuthoritativeSessionAdmissionConsumer inner,
            PartySession session,
            PartySessionApplication application)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _application = application ?? throw new ArgumentNullException(nameof(application));
        }

        public void HandleSessionAdmission(uint connectionId, ReadOnlySpan<byte> payload)
        {
            _inner.HandleSessionAdmission(connectionId, payload);
            if (connectionId == 0 ||
                !_session.TryResolveConnection(SessionNetworkAdmissionAdapter.FromConnectionId(connectionId), out PartyMemberId memberId))
                return;

            if (!_session.TryGetMember(memberId, out PartyMemberSnapshot member))
                return;
            if (member.Readiness < SessionReadinessState.Synchronized && !_session.MarkSynchronized(memberId))
                return;
            if (!_session.TryGetMember(memberId, out member))
                return;
            if (member.Readiness < SessionReadinessState.GameplayReady && !_session.MarkGameplayReady(memberId))
                return;

            _application.SetReady(memberId, true);
        }
    }

    /// <summary>
    /// Semantic parser for the required replicated Sessions projections. It deliberately consumes
    /// only copied gameplay-projection values and never transport identity.
    /// </summary>
    public static class KentridgeReplicatedPartyState
    {
        public static readonly GameplayProjectionId SessionsProjectionId = new GameplayProjectionId("sessions");

        public static bool IsActiveAndReady(
            IGameplayReplicationReadState readState,
            GameSessionId expectedSession,
            PartyMemberId expectedLocalMember)
        {
            if (readState == null || !readState.GameplayReady ||
                !expectedSession.IsValid || !expectedLocalMember.IsValid ||
                !readState.TryGetProjection(SessionsProjectionId, out GameplayProjectionState sessions) ||
                !readState.TryGetProjection(KentridgeSessionApplicationGameplayProjectionSource.ProjectionId,
                    out GameplayProjectionState application) ||
                !TryValue(sessions, "session-id", out string sessionId) ||
                !string.Equals(sessionId, expectedSession.Value, StringComparison.Ordinal) ||
                !TryBool(application, "gameplay-started", out bool gameplayStarted) || !gameplayStarted)
                return false;

            for (int i = 0; i < sessions.Entries.Count; i++)
            {
                GameplayProjectionEntry entry = sessions.Entries[i];
                if (!entry.Key.EndsWith("/member-id", StringComparison.Ordinal) ||
                    !string.Equals(entry.Value, expectedLocalMember.Value, StringComparison.Ordinal))
                    continue;
                string prefix = entry.Key.Substring(0, entry.Key.Length - "member-id".Length);
                return TryValue(sessions, prefix + "presence", out string presence) &&
                       string.Equals(presence, PartyPresenceState.Connected.ToString(), StringComparison.Ordinal) &&
                       TryValue(sessions, prefix + "readiness", out string readiness) &&
                       string.Equals(readiness, SessionReadinessState.GameplayReady.ToString(), StringComparison.Ordinal);
            }
            return false;
        }

        internal static bool TryValue(GameplayProjectionState state, string key, out string value)
        {
            if (state != null)
            {
                IReadOnlyList<GameplayProjectionEntry> entries = state.Entries;
                for (int i = 0; i < entries.Count; i++)
                {
                    if (!string.Equals(entries[i].Key, key, StringComparison.Ordinal)) continue;
                    value = entries[i].Value;
                    return true;
                }
            }
            value = string.Empty;
            return false;
        }

        internal static bool TryBool(GameplayProjectionState state, string key, out bool value)
        {
            value = false;
            return TryValue(state, key, out string raw) && bool.TryParse(raw, out value);
        }
    }

    /// <summary>
    /// Application formation decorator. Host formation completes after real authority admission so
    /// the leader can issue Start. Join formation remains pending until the admitted durable member
    /// has received the authoritative active-game snapshot on its real network connection.
    /// </summary>
    public sealed class KentridgeGameplayReadyFormationService : IAsyncSessionFormationService
    {
        private readonly IAsyncSessionFormationService _inner;
        private readonly IGameplayReplicationReadState _readState;
        private readonly Action _pumpClient;
        private readonly Action _cancelActiveClient;

        public KentridgeGameplayReadyFormationService(
            IAsyncSessionFormationService inner,
            IGameplayReplicationReadState readState,
            Action pumpClient,
            Action cancelActiveClient = null)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _readState = readState ?? throw new ArgumentNullException(nameof(readState));
            _pumpClient = pumpClient ?? throw new ArgumentNullException(nameof(pumpClient));
            _cancelActiveClient = cancelActiveClient;
        }

        public ISessionFormationOperation BeginHost(HostSessionRequest request) =>
            new Operation(_inner.BeginHost(request), _readState, _pumpClient, _cancelActiveClient, false);

        public ISessionFormationOperation BeginJoin(JoinSessionRequest request) =>
            new Operation(_inner.BeginJoin(request), _readState, _pumpClient, _cancelActiveClient, true);

        public SessionFormationResult Host(HostSessionRequest request) =>
            SessionFormationResult.Reject(SessionFormationFailure.ProviderUnavailable,
                "Asynchronous provider requires BeginHost");

        public SessionFormationResult Join(JoinSessionRequest request) =>
            SessionFormationResult.Reject(SessionFormationFailure.ProviderUnavailable,
                "Asynchronous provider requires BeginJoin");

        private sealed class Operation : ISessionFormationOperation
        {
            private readonly ISessionFormationOperation _inner;
            private readonly IGameplayReplicationReadState _readState;
            private readonly Action _pumpClient;
            private readonly Action _cancelActiveClient;
            private readonly bool _waitForActiveGameplay;
            private bool _admitted;
            private bool _terminal;
            private bool _cancelled;
            private SessionFormationResult _admission;

            public Operation(
                ISessionFormationOperation inner,
                IGameplayReplicationReadState readState,
                Action pumpClient,
                Action cancelActiveClient,
                bool waitForActiveGameplay)
            {
                _inner = inner ?? throw new ArgumentNullException(nameof(inner));
                _readState = readState;
                _pumpClient = pumpClient;
                _cancelActiveClient = cancelActiveClient;
                _waitForActiveGameplay = waitForActiveGameplay;
            }

            public bool TryGetResult(out SessionFormationResult result)
            {
                result = default;
                if (_cancelled) return false;
                if (_terminal)
                {
                    result = _admission;
                    return true;
                }

                if (!_admitted)
                {
                    if (!_inner.TryGetResult(out SessionFormationResult innerResult))
                        return false;
                    _admission = innerResult;
                    if (!innerResult.Succeeded || !_waitForActiveGameplay)
                    {
                        _terminal = true;
                        result = _admission;
                        return true;
                    }
                    _admitted = true;
                }

                _pumpClient();
                if (!KentridgeReplicatedPartyState.IsActiveAndReady(
                        _readState, _admission.SessionId, _admission.LocalMemberId))
                    return false;

                _terminal = true;
                result = _admission;
                return true;
            }

            public void Cancel()
            {
                if (_cancelled) return;
                _cancelled = true;
                _inner.Cancel();
                if (_admitted) _cancelActiveClient?.Invoke();
            }
        }
    }

    /// <summary>
    /// Client-side party projection used by Application after Join completes. The snapshot is rebuilt
    /// from authoritative GameplayReplication state and highlights only the supplied durable local member.
    /// </summary>
    public sealed class KentridgeReplicatedPartyScreenQuery : IPartyScreenPresentationQuery
    {
        private readonly IGameplayReplicationReadState _readState;

        public KentridgeReplicatedPartyScreenQuery(IGameplayReplicationReadState readState)
        {
            _readState = readState ?? throw new ArgumentNullException(nameof(readState));
        }

        public PartyScreenPresentationSnapshot CapturePartyScreen(PartyMemberId localMemberId)
        {
            if (!localMemberId.IsValid ||
                !_readState.TryGetProjection(KentridgeReplicatedPartyState.SessionsProjectionId,
                    out GameplayProjectionState sessions) ||
                !_readState.TryGetProjection(KentridgeSessionApplicationGameplayProjectionSource.ProjectionId,
                    out GameplayProjectionState application) ||
                !KentridgeReplicatedPartyState.TryValue(sessions, "session-id", out string rawSession) ||
                !KentridgeReplicatedPartyState.TryValue(application, "capacity", out string rawCapacity) ||
                !int.TryParse(rawCapacity, NumberStyles.Integer, CultureInfo.InvariantCulture, out int capacity) ||
                capacity < 1)
                return null;

            GameSessionId sessionId;
            try { sessionId = new GameSessionId(rawSession); }
            catch (ArgumentException) { return null; }

            KentridgeReplicatedPartyState.TryBool(application, "gameplay-started", out bool gameplayStarted);
            var members = new List<PartyMemberPresentationSnapshot>();
            for (int slotValue = 0; slotValue < capacity; slotValue++)
            {
                string prefix = "slot/" + slotValue.ToString(CultureInfo.InvariantCulture) + "/";
                if (!KentridgeReplicatedPartyState.TryValue(sessions, prefix + "member-id", out string rawMember))
                    continue;
                if (!KentridgeReplicatedPartyState.TryValue(sessions, prefix + "leadership", out string rawLeadership) ||
                    !Enum.TryParse(rawLeadership, out PartyLeadershipRole leadership) ||
                    !KentridgeReplicatedPartyState.TryValue(sessions, prefix + "presence", out string rawPresence) ||
                    !Enum.TryParse(rawPresence, out PartyPresenceState presence) ||
                    !KentridgeReplicatedPartyState.TryValue(sessions, prefix + "readiness", out string rawReadiness) ||
                    !Enum.TryParse(rawReadiness, out SessionReadinessState readiness))
                    return null;

                PartyMemberId memberId;
                CharacterId characterId = default;
                try
                {
                    memberId = new PartyMemberId(rawMember);
                    if (KentridgeReplicatedPartyState.TryValue(sessions, prefix + "character-id", out string rawCharacter) &&
                        !string.IsNullOrWhiteSpace(rawCharacter))
                        characterId = new CharacterId(rawCharacter);
                }
                catch (ArgumentException) { return null; }

                KentridgeReplicatedPartyState.TryBool(
                    application, "member/" + memberId.Value + "/ready", out bool readyToStart);
                bool gameplayReady = readiness == SessionReadinessState.GameplayReady;
                members.Add(new PartyMemberPresentationSnapshot(
                    memberId,
                    new PlayerSlot(slotValue),
                    characterId,
                    leadership,
                    memberId == localMemberId,
                    Connection(presence, readiness),
                    Readiness(readiness),
                    readyToStart,
                    gameplayReady,
                    new MemberDisplayMetadata(
                        leadership == PartyLeadershipRole.Leader ? "Leader" : "Player " + (slotValue + 1),
                        gameplayReady ? "Ready" : "Synchronizing",
                        characterId.IsValid ? characterId.Value : string.Empty)));
            }

            SessionPresentationLifecycle lifecycle = Lifecycle(members, gameplayStarted);
            bool canStart = !gameplayStarted && CanStart(members, localMemberId);
            return new PartyScreenPresentationSnapshot(sessionId, capacity, lifecycle, canStart, members);
        }

        private static MemberConnectionPresentationState Connection(
            PartyPresenceState presence, SessionReadinessState readiness)
        {
            if (presence == PartyPresenceState.Disconnected)
                return MemberConnectionPresentationState.Interrupted;
            if (presence == PartyPresenceState.Connected)
                return readiness == SessionReadinessState.Synchronized
                    ? MemberConnectionPresentationState.Resynchronizing
                    : MemberConnectionPresentationState.Connected;
            return MemberConnectionPresentationState.Joined;
        }

        private static MemberReadinessPresentationState Readiness(SessionReadinessState readiness)
        {
            if (readiness == SessionReadinessState.GameplayReady)
                return MemberReadinessPresentationState.GameplayReady;
            if (readiness >= SessionReadinessState.Connected)
                return MemberReadinessPresentationState.Synchronizing;
            return MemberReadinessPresentationState.WaitingForConnection;
        }

        private static SessionPresentationLifecycle Lifecycle(
            IReadOnlyList<PartyMemberPresentationSnapshot> members, bool gameplayStarted)
        {
            if (members.Count == 0) return SessionPresentationLifecycle.Empty;
            if (gameplayStarted) return SessionPresentationLifecycle.Active;
            bool allConnected = true;
            bool allGameplayReady = true;
            for (int i = 0; i < members.Count; i++)
            {
                allConnected &= members[i].Connection == MemberConnectionPresentationState.Connected;
                allGameplayReady &= members[i].GameplayReady;
            }
            if (!allConnected) return SessionPresentationLifecycle.WaitingForPlayers;
            return allGameplayReady
                ? SessionPresentationLifecycle.ReadyToStart
                : SessionPresentationLifecycle.Synchronizing;
        }

        private static bool CanStart(
            IReadOnlyList<PartyMemberPresentationSnapshot> members, PartyMemberId localMemberId)
        {
            bool localLeader = false;
            for (int i = 0; i < members.Count; i++)
            {
                PartyMemberPresentationSnapshot member = members[i];
                if (!member.GameplayReady || !member.ReadyToStart) return false;
                if (member.MemberId == localMemberId && member.LeadershipRole == PartyLeadershipRole.Leader)
                    localLeader = true;
            }
            return localLeader;
        }
    }

    /// <summary>
    /// Non-authoritative client graph for the shared Application/SessionOrchestration lifecycle.
    /// It owns no campaign/domain authority; its only deterministic step advances the production
    /// network client that feeds the GameplayReplication read model.
    /// </summary>
    public sealed class KentridgeReplicatedClientSessionGraphFactory : ISessionRuntimeGraphFactory
    {
        private readonly IGameplayReplicationReadState _readState;
        private readonly Action _pumpClient;

        public KentridgeReplicatedClientSessionGraphFactory(
            IGameplayReplicationReadState readState,
            Action pumpClient)
        {
            _readState = readState ?? throw new ArgumentNullException(nameof(readState));
            _pumpClient = pumpClient ?? throw new ArgumentNullException(nameof(pumpClient));
        }

        public ISessionRuntimeGraph Compose(GameSessionIdentity identity)
        {
            if (identity == null) throw new ArgumentNullException(nameof(identity));
            if (!_readState.GameplayReady)
                throw new SessionCompositionException(
                    GameSessionFailure.BindingsNotReady,
                    "Replicated gameplay state is not ready for client composition.");
            return new Graph(_readState, _pumpClient);
        }

        private sealed class Graph : ISessionRuntimeGraph
        {
            private readonly IGameplayReplicationReadState _readState;
            private readonly IReadOnlyList<ISessionUpdateStep> _steps;
            private bool _disposed;

            public Graph(IGameplayReplicationReadState readState, Action pumpClient)
            {
                _readState = readState;
                _steps = new ISessionUpdateStep[] { new ReplicationStep(pumpClient) };
            }

            public bool GameplayBindingsReady => !_disposed && _readState.GameplayReady;
            public IReadOnlyList<ISessionUpdateStep> UpdateSteps => _steps;
            public IGameOutcomeQuery OutcomeQuery => null;
            public void InitializeNewGame() { ThrowIfDisposed(); }
            public void StartCommands() { ThrowIfDisposed(); }
            public void StopCommands() { }
            public void SettleAuthoritativeState() { }
            public void DetachExternalAdapters() { }
            public void Dispose() { _disposed = true; }
            private void ThrowIfDisposed()
            {
                if (_disposed) throw new ObjectDisposedException(nameof(Graph));
            }
        }

        private sealed class ReplicationStep : ISessionUpdateStep
        {
            private readonly Action _pumpClient;
            public ReplicationStep(Action pumpClient) => _pumpClient = pumpClient;
            public SessionUpdatePhase Phase => SessionUpdatePhase.Replication;
            public int Order => 0;
            public string SemanticId => "kentridge.multiplayer.client-replication";
            public void Tick(int elapsedMilliseconds) => _pumpClient();
        }
    }
}
