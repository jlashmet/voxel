using System;
using System.Collections.Generic;
using Game.Characters.Api;

namespace Game.Sessions.Api
{
    public readonly struct GameSessionId : IEquatable<GameSessionId>, IComparable<GameSessionId>
    {
        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);
        public GameSessionId(string value) { if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Session id is required.", nameof(value)); Value = value; }
        public int CompareTo(GameSessionId other) => StringComparer.Ordinal.Compare(Value ?? string.Empty, other.Value ?? string.Empty);
        public bool Equals(GameSessionId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is GameSessionId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(GameSessionId left, GameSessionId right) => left.Equals(right);
        public static bool operator !=(GameSessionId left, GameSessionId right) => !left.Equals(right);
    }

    /// <summary>Durable party identity. Survives transport reconnect and is eligible for durable restore.</summary>
    public readonly struct PartyMemberId : IEquatable<PartyMemberId>, IComparable<PartyMemberId>
    {
        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);
        public PartyMemberId(string value) { if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Party member id is required.", nameof(value)); Value = value; }
        public int CompareTo(PartyMemberId other) => StringComparer.Ordinal.Compare(Value ?? string.Empty, other.Value ?? string.Empty);
        public bool Equals(PartyMemberId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is PartyMemberId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(PartyMemberId left, PartyMemberId right) => left.Equals(right);
        public static bool operator !=(PartyMemberId left, PartyMemberId right) => !left.Equals(right);
    }

    /// <summary>Stable slot inside one session. Slot identity survives reconnect and is never a transport connection index.</summary>
    public readonly struct PlayerSlot : IEquatable<PlayerSlot>, IComparable<PlayerSlot>
    {
        public int Value { get; }
        public bool IsValid => Value >= 0;
        public PlayerSlot(int value) { if (value < 0) throw new ArgumentOutOfRangeException(nameof(value)); Value = value; }
        public int CompareTo(PlayerSlot other) => Value.CompareTo(other.Value);
        public bool Equals(PlayerSlot other) => Value == other.Value;
        public override bool Equals(object obj) => obj is PlayerSlot other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => Value.ToString();
        public static bool operator ==(PlayerSlot left, PlayerSlot right) => left.Equals(right);
        public static bool operator !=(PlayerSlot left, PlayerSlot right) => !left.Equals(right);
    }

    public enum PartyLeadershipRole : byte { Member = 0, Leader = 1 }
    public enum PartyPresenceState : byte { Joined = 0, Connected = 1, Disconnected = 2 }
    public enum SessionReadinessState : byte { Joined = 0, Connected = 1, Synchronized = 2, GameplayReady = 3 }
    public enum LeaderTransferPolicy : byte { OldestRemainingMember = 0, ExplicitOnly = 1 }

    public readonly struct SessionStartupConfiguration
    {
        public int Capacity { get; }
        public string ProtocolVersion { get; }
        public string ContentCompatibilityKey { get; }
        public bool AllowJoinInProgress { get; }
        public LeaderTransferPolicy LeaderTransferPolicy { get; }

        public SessionStartupConfiguration(int capacity, string protocolVersion, string contentCompatibilityKey, bool allowJoinInProgress, LeaderTransferPolicy leaderTransferPolicy = LeaderTransferPolicy.OldestRemainingMember)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            if (string.IsNullOrWhiteSpace(protocolVersion)) throw new ArgumentException("Protocol version is required.", nameof(protocolVersion));
            if (string.IsNullOrWhiteSpace(contentCompatibilityKey)) throw new ArgumentException("Content compatibility key is required.", nameof(contentCompatibilityKey));
            Capacity = capacity;
            ProtocolVersion = protocolVersion;
            ContentCompatibilityKey = contentCompatibilityKey;
            AllowJoinInProgress = allowJoinInProgress;
            LeaderTransferPolicy = leaderTransferPolicy;
        }
    }

    public readonly struct PartyMemberSnapshot
    {
        public PartyMemberId MemberId { get; }
        public PlayerSlot Slot { get; }
        public PartyLeadershipRole LeadershipRole { get; }
        public PartyPresenceState Presence { get; }
        public SessionReadinessState Readiness { get; }
        public CharacterId CharacterId { get; }
        public bool HasCharacter => CharacterId.IsValid;

        public PartyMemberSnapshot(PartyMemberId memberId, PlayerSlot slot, PartyLeadershipRole leadershipRole, PartyPresenceState presence, SessionReadinessState readiness, CharacterId characterId = default)
        {
            MemberId = memberId;
            Slot = slot;
            LeadershipRole = leadershipRole;
            Presence = presence;
            Readiness = readiness;
            CharacterId = characterId;
        }
    }

    public readonly struct PartyRosterSnapshot
    {
        public GameSessionId SessionId { get; }
        public IReadOnlyList<PartyMemberSnapshot> Members { get; }
        public PartyRosterSnapshot(GameSessionId sessionId, IReadOnlyList<PartyMemberSnapshot> members) { SessionId = sessionId; Members = members ?? throw new ArgumentNullException(nameof(members)); }
    }

    public enum JoinFailureReason : byte
    {
        None = 0,
        SessionMismatch = 1,
        ProtocolVersionMismatch = 2,
        ContentMismatch = 3,
        SessionFull = 4,
        JoinInProgressDisabled = 5,
        DuplicateApplicant = 6,
        UnknownMember = 7,
        InvalidRequest = 8
    }

    public readonly struct JoinRequest
    {
        public GameSessionId SessionId { get; }
        public string ApplicantKey { get; }
        public string ProtocolVersion { get; }
        public string ContentCompatibilityKey { get; }
        public bool IsJoinInProgress { get; }
        public JoinRequest(GameSessionId sessionId, string applicantKey, string protocolVersion, string contentCompatibilityKey, bool isJoinInProgress = false)
        {
            SessionId = sessionId;
            ApplicantKey = applicantKey;
            ProtocolVersion = protocolVersion;
            ContentCompatibilityKey = contentCompatibilityKey;
            IsJoinInProgress = isJoinInProgress;
        }
    }

    public readonly struct JoinResult
    {
        public bool Accepted => FailureReason == JoinFailureReason.None;
        public JoinFailureReason FailureReason { get; }
        public PartyMemberSnapshot Member { get; }
        public JoinResult(JoinFailureReason failureReason, PartyMemberSnapshot member = default) { FailureReason = failureReason; Member = member; }
    }

    public readonly struct JoinConnectionInfo
    {
        public string Endpoint { get; }
        public string AdmissionToken { get; }
        public JoinConnectionInfo(string endpoint, string admissionToken) { Endpoint = endpoint ?? string.Empty; AdmissionToken = admissionToken ?? string.Empty; }
    }

    public interface IJoinProvider
    {
        JoinConnectionInfo ResolveConnection(GameSessionId sessionId, PartyMemberId memberId);
    }

    public enum SessionLifecycleEventKind : byte
    {
        MemberJoined = 0,
        MemberConnected = 1,
        MemberSynchronized = 2,
        MemberGameplayReady = 3,
        MemberDisconnected = 4,
        MemberRemoved = 5,
        LeaderChanged = 6,
        CharacterBound = 7
    }

    public readonly struct SessionLifecycleEvent
    {
        public ulong Sequence { get; }
        public SessionLifecycleEventKind Kind { get; }
        public PartyMemberId MemberId { get; }
        public SessionLifecycleEvent(ulong sequence, SessionLifecycleEventKind kind, PartyMemberId memberId) { Sequence = sequence; Kind = kind; MemberId = memberId; }
    }

    public interface IPartySessionQuery
    {
        PartyRosterSnapshot Snapshot();
        bool TryGetMember(PartyMemberId memberId, out PartyMemberSnapshot member);
    }
}
