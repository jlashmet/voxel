using System;
using Game.Sessions.Api;

namespace Game.Continuity.Api
{
    public enum RecoveryState : byte
    {
        Connected = 0,
        ConnectionInterrupted = 1,
        Reconnecting = 2,
        Resynchronizing = 3,
        Recovered = 4,
        Expired = 5,
        Left = 6
    }

    public enum RecoveryPath : byte
    {
        FastRepair = 0,
        FullResynchronization = 1
    }

    public enum ReconnectFailureReason : byte
    {
        None = 0,
        UnknownMember = 1,
        SessionMismatch = 2,
        CredentialRejected = 3,
        NotRecoverable = 4,
        GraceExpired = 5,
        TransportRejected = 6
    }

    public readonly struct ContinuityPolicy
    {
        public double GraceSeconds { get; }
        public double FastRepairWindowSeconds { get; }

        public ContinuityPolicy(double graceSeconds, double fastRepairWindowSeconds)
        {
            if (graceSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(graceSeconds));
            if (fastRepairWindowSeconds < 0 || fastRepairWindowSeconds > graceSeconds)
                throw new ArgumentOutOfRangeException(nameof(fastRepairWindowSeconds));
            GraceSeconds = graceSeconds;
            FastRepairWindowSeconds = fastRepairWindowSeconds;
        }
    }

    public readonly struct ReconnectCredential
    {
        public GameSessionId SessionId { get; }
        public PartyMemberId MemberId { get; }
        public string Token { get; }

        public ReconnectCredential(GameSessionId sessionId, PartyMemberId memberId, string token)
        {
            if (!sessionId.IsValid) throw new ArgumentException("Session id is required.", nameof(sessionId));
            if (!memberId.IsValid) throw new ArgumentException("Member id is required.", nameof(memberId));
            if (string.IsNullOrWhiteSpace(token)) throw new ArgumentException("Reconnect token is required.", nameof(token));
            SessionId = sessionId;
            MemberId = memberId;
            Token = token;
        }
    }

    public readonly struct ReconnectRequest
    {
        public ReconnectCredential Credential { get; }
        public ReconnectRequest(ReconnectCredential credential) { Credential = credential; }
    }

    public readonly struct ReconnectResult
    {
        public bool Accepted => FailureReason == ReconnectFailureReason.None;
        public ReconnectFailureReason FailureReason { get; }
        public RecoveryPath Path { get; }
        public PartyMemberSnapshot Member { get; }

        public ReconnectResult(ReconnectFailureReason failureReason, RecoveryPath path = RecoveryPath.FastRepair, PartyMemberSnapshot member = default)
        {
            FailureReason = failureReason;
            Path = path;
            Member = member;
        }
    }

    public enum ContinuityEventKind : byte
    {
        Interrupted = 0,
        ReconnectStarted = 1,
        ResynchronizationStarted = 2,
        Recovered = 3,
        Expired = 4,
        Left = 5
    }

    public readonly struct ContinuityEvent
    {
        public ulong Sequence { get; }
        public ContinuityEventKind Kind { get; }
        public PartyMemberId MemberId { get; }
        public ContinuityEvent(ulong sequence, ContinuityEventKind kind, PartyMemberId memberId)
        {
            Sequence = sequence;
            Kind = kind;
            MemberId = memberId;
        }
    }

    public readonly struct RecoverySnapshot
    {
        public PartyMemberId MemberId { get; }
        public RecoveryState State { get; }
        public double GraceDeadline { get; }
        public RecoverySnapshot(PartyMemberId memberId, RecoveryState state, double graceDeadline)
        {
            MemberId = memberId;
            State = state;
            GraceDeadline = graceDeadline;
        }
    }

    public interface IContinuityQuery
    {
        bool TryGetRecovery(PartyMemberId memberId, out RecoverySnapshot recovery);
    }
}
