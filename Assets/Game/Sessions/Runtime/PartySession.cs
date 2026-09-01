using System;
using System.Collections.Generic;
using Game.Characters.Api;
using Game.Sessions.Api;

namespace Game.Sessions.Runtime
{
    /// <summary>Opaque runtime-only transport handle. It is never persisted or exposed in Sessions.Api snapshots.</summary>
    public readonly struct TransportConnectionHandle : IEquatable<TransportConnectionHandle>
    {
        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);
        public TransportConnectionHandle(string value) { if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Connection handle is required.", nameof(value)); Value = value; }
        public bool Equals(TransportConnectionHandle other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is TransportConnectionHandle other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
    }

    /// <summary>
    /// Session-owned durable roster. Transport connection handles are an ephemeral association only;
    /// member/slot/character identity remains stable when that association changes.
    /// </summary>
    public sealed class PartySession : IPartySessionQuery
    {
        private readonly GameSessionId _sessionId;
        private readonly SessionStartupConfiguration _configuration;
        private readonly ICharacterBindingWriter _characterBindings;
        private readonly List<MemberState> _members = new List<MemberState>();
        private readonly Dictionary<PartyMemberId, int> _indexByMember = new Dictionary<PartyMemberId, int>();
        private readonly Dictionary<string, PartyMemberId> _memberByApplicant = new Dictionary<string, PartyMemberId>(StringComparer.Ordinal);
        private readonly Dictionary<TransportConnectionHandle, PartyMemberId> _memberByConnection = new Dictionary<TransportConnectionHandle, PartyMemberId>();
        private ulong _nextMemberOrdinal = 1;
        private ulong _eventSequence;
        private bool _gameplayStarted;

        public event Action<SessionLifecycleEvent> Changed;

        public PartySession(GameSessionId sessionId, SessionStartupConfiguration configuration, ICharacterBindingWriter characterBindings = null)
        {
            if (!sessionId.IsValid) throw new ArgumentException("Session id is required.", nameof(sessionId));
            _sessionId = sessionId;
            _configuration = configuration;
            _characterBindings = characterBindings;
        }

        public JoinResult Join(JoinRequest request)
        {
            JoinFailureReason compatibility = ValidateJoin(request);
            if (compatibility != JoinFailureReason.None)
                return new JoinResult(compatibility);

            PlayerSlot slot = AllocateLowestAvailableSlot();
            var memberId = new PartyMemberId(_sessionId.Value + ":member:" + _nextMemberOrdinal++);
            var role = _members.Count == 0 ? PartyLeadershipRole.Leader : PartyLeadershipRole.Member;
            var state = new MemberState(memberId, slot, request.ApplicantKey, role);
            _indexByMember.Add(memberId, _members.Count);
            _memberByApplicant.Add(request.ApplicantKey, memberId);
            _members.Add(state);
            Publish(SessionLifecycleEventKind.MemberJoined, memberId);
            return new JoinResult(JoinFailureReason.None, ToSnapshot(state));
        }

        public bool BindConnection(PartyMemberId memberId, TransportConnectionHandle connection)
        {
            if (!connection.IsValid || !_indexByMember.TryGetValue(memberId, out int index))
                return false;
            if (_memberByConnection.TryGetValue(connection, out PartyMemberId existing) && existing != memberId)
                return false;

            MemberState state = _members[index];
            if (state.HasConnection)
                _memberByConnection.Remove(state.Connection);
            state.Connection = connection;
            state.HasConnection = true;
            state.Presence = PartyPresenceState.Connected;
            state.Readiness = SessionReadinessState.Connected;
            _members[index] = state;
            _memberByConnection[connection] = memberId;
            Publish(SessionLifecycleEventKind.MemberConnected, memberId);
            return true;
        }

        public bool Disconnect(TransportConnectionHandle connection)
        {
            if (!_memberByConnection.TryGetValue(connection, out PartyMemberId memberId) || !_indexByMember.TryGetValue(memberId, out int index))
                return false;
            _memberByConnection.Remove(connection);
            MemberState state = _members[index];
            state.HasConnection = false;
            state.Connection = default;
            state.Presence = PartyPresenceState.Disconnected;
            state.Readiness = SessionReadinessState.Joined;
            _members[index] = state;
            Publish(SessionLifecycleEventKind.MemberDisconnected, memberId);
            return true;
        }

        public bool MarkSynchronized(PartyMemberId memberId)
        {
            if (!_indexByMember.TryGetValue(memberId, out int index)) return false;
            MemberState state = _members[index];
            if (!state.HasConnection || state.Readiness < SessionReadinessState.Connected) return false;
            state.Readiness = SessionReadinessState.Synchronized;
            _members[index] = state;
            Publish(SessionLifecycleEventKind.MemberSynchronized, memberId);
            return true;
        }

        public bool MarkGameplayReady(PartyMemberId memberId)
        {
            if (!_indexByMember.TryGetValue(memberId, out int index)) return false;
            MemberState state = _members[index];
            if (!state.HasConnection || state.Readiness < SessionReadinessState.Synchronized) return false;
            state.Readiness = SessionReadinessState.GameplayReady;
            _members[index] = state;
            Publish(SessionLifecycleEventKind.MemberGameplayReady, memberId);
            return true;
        }

        public bool CanLaunch()
        {
            if (_members.Count == 0) return false;
            for (int i = 0; i < _members.Count; i++)
            {
                if (_members[i].Readiness != SessionReadinessState.GameplayReady)
                    return false;
            }
            return true;
        }

        public bool StartGameplay()
        {
            if (!CanLaunch()) return false;
            _gameplayStarted = true;
            return true;
        }

        public bool BindCharacter(PartyMemberId memberId, CharacterId characterId)
        {
            if (!characterId.IsValid || !_indexByMember.TryGetValue(memberId, out int index)) return false;
            for (int i = 0; i < _members.Count; i++)
            {
                if (i != index && _members[i].CharacterId == characterId)
                    return false;
            }

            MemberState state = _members[index];
            if (state.CharacterId.IsValid && state.CharacterId != characterId)
                return false;

            if (_characterBindings != null)
            {
                CharacterRegistryFailure failure = _characterBindings.Bind(characterId, new CharacterBinding("party-member", memberId.Value));
                if (failure != CharacterRegistryFailure.None && failure != CharacterRegistryFailure.DuplicateBinding)
                    return false;
            }

            state.CharacterId = characterId;
            _members[index] = state;
            Publish(SessionLifecycleEventKind.CharacterBound, memberId);
            return true;
        }

        public bool Remove(PartyMemberId memberId)
        {
            if (!_indexByMember.TryGetValue(memberId, out int index)) return false;
            MemberState removed = _members[index];
            bool wasLeader = removed.LeadershipRole == PartyLeadershipRole.Leader;
            if (removed.HasConnection) _memberByConnection.Remove(removed.Connection);
            _memberByApplicant.Remove(removed.ApplicantKey);
            _members.RemoveAt(index);
            _indexByMember.Remove(memberId);
            ReindexFrom(index);
            Publish(SessionLifecycleEventKind.MemberRemoved, memberId);

            if (wasLeader && _members.Count > 0 && _configuration.LeaderTransferPolicy == LeaderTransferPolicy.OldestRemainingMember)
            {
                MemberState successor = _members[0];
                successor.LeadershipRole = PartyLeadershipRole.Leader;
                _members[0] = successor;
                Publish(SessionLifecycleEventKind.LeaderChanged, successor.MemberId);
            }
            return true;
        }

        public PartyRosterSnapshot Snapshot()
        {
            var result = new PartyMemberSnapshot[_members.Count];
            for (int i = 0; i < _members.Count; i++) result[i] = ToSnapshot(_members[i]);
            return new PartyRosterSnapshot(_sessionId, result);
        }

        public bool TryGetMember(PartyMemberId memberId, out PartyMemberSnapshot member)
        {
            if (_indexByMember.TryGetValue(memberId, out int index))
            {
                member = ToSnapshot(_members[index]);
                return true;
            }
            member = default;
            return false;
        }

        public bool TryResolveConnection(TransportConnectionHandle connection, out PartyMemberId memberId) =>
            _memberByConnection.TryGetValue(connection, out memberId);

        private JoinFailureReason ValidateJoin(JoinRequest request)
        {
            if (!request.SessionId.IsValid || string.IsNullOrWhiteSpace(request.ApplicantKey) || string.IsNullOrWhiteSpace(request.ProtocolVersion) || string.IsNullOrWhiteSpace(request.ContentCompatibilityKey))
                return JoinFailureReason.InvalidRequest;
            if (request.SessionId != _sessionId) return JoinFailureReason.SessionMismatch;
            if (!string.Equals(request.ProtocolVersion, _configuration.ProtocolVersion, StringComparison.Ordinal)) return JoinFailureReason.ProtocolVersionMismatch;
            if (!string.Equals(request.ContentCompatibilityKey, _configuration.ContentCompatibilityKey, StringComparison.Ordinal)) return JoinFailureReason.ContentMismatch;
            if (_memberByApplicant.ContainsKey(request.ApplicantKey)) return JoinFailureReason.DuplicateApplicant;
            if (_members.Count >= _configuration.Capacity) return JoinFailureReason.SessionFull;
            if ((_gameplayStarted || request.IsJoinInProgress) && !_configuration.AllowJoinInProgress) return JoinFailureReason.JoinInProgressDisabled;
            return JoinFailureReason.None;
        }

        private PlayerSlot AllocateLowestAvailableSlot()
        {
            for (int candidate = 0; candidate < _configuration.Capacity; candidate++)
            {
                bool used = false;
                for (int i = 0; i < _members.Count; i++)
                {
                    if (_members[i].Slot.Value == candidate) { used = true; break; }
                }
                if (!used) return new PlayerSlot(candidate);
            }
            throw new InvalidOperationException("No player slot is available despite capacity validation.");
        }

        private void ReindexFrom(int start)
        {
            for (int i = start; i < _members.Count; i++) _indexByMember[_members[i].MemberId] = i;
        }

        private void Publish(SessionLifecycleEventKind kind, PartyMemberId memberId) => Changed?.Invoke(new SessionLifecycleEvent(++_eventSequence, kind, memberId));

        private static PartyMemberSnapshot ToSnapshot(MemberState state) =>
            new PartyMemberSnapshot(state.MemberId, state.Slot, state.LeadershipRole, state.Presence, state.Readiness, state.CharacterId);

        private struct MemberState
        {
            public PartyMemberId MemberId;
            public PlayerSlot Slot;
            public string ApplicantKey;
            public PartyLeadershipRole LeadershipRole;
            public PartyPresenceState Presence;
            public SessionReadinessState Readiness;
            public CharacterId CharacterId;
            public TransportConnectionHandle Connection;
            public bool HasConnection;

            public MemberState(PartyMemberId memberId, PlayerSlot slot, string applicantKey, PartyLeadershipRole leadershipRole)
            {
                MemberId = memberId;
                Slot = slot;
                ApplicantKey = applicantKey;
                LeadershipRole = leadershipRole;
                Presence = PartyPresenceState.Joined;
                Readiness = SessionReadinessState.Joined;
                CharacterId = default;
                Connection = default;
                HasConnection = false;
            }
        }
    }
}
