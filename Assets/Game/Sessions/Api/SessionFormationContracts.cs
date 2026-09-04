using System;

namespace Game.Sessions.Api
{
    public enum SessionFormationFailure : byte
    {
        None = 0,
        InvalidRequest = 1,
        SessionUnavailable = 2,
        ProtocolMismatch = 3,
        ContentMismatch = 4,
        SessionFull = 5,
        ProviderUnavailable = 6,
        Rejected = 7
    }

    public readonly struct HostSessionRequest
    {
        public GameSessionId SessionId { get; }
        public SessionStartupConfiguration Configuration { get; }
        public string LocalApplicantKey { get; }

        public HostSessionRequest(GameSessionId sessionId, SessionStartupConfiguration configuration, string localApplicantKey)
        {
            if (!sessionId.IsValid) throw new ArgumentException("Session id is required.", nameof(sessionId));
            if (string.IsNullOrWhiteSpace(localApplicantKey)) throw new ArgumentException("Local applicant key is required.", nameof(localApplicantKey));
            SessionId = sessionId;
            Configuration = configuration;
            LocalApplicantKey = localApplicantKey.Trim();
        }
    }

    public readonly struct JoinSessionRequest
    {
        public JoinRequest Admission { get; }

        public JoinSessionRequest(JoinRequest admission)
        {
            if (!admission.SessionId.IsValid) throw new ArgumentException("Session id is required.", nameof(admission));
            if (string.IsNullOrWhiteSpace(admission.ApplicantKey)) throw new ArgumentException("Applicant key is required.", nameof(admission));
            Admission = admission;
        }
    }

    public readonly struct SessionFormationResult
    {
        public bool Succeeded => Failure == SessionFormationFailure.None;
        public SessionFormationFailure Failure { get; }
        public GameSessionId SessionId { get; }
        public PartyMemberId LocalMemberId { get; }
        public string Detail { get; }

        private SessionFormationResult(SessionFormationFailure failure, GameSessionId sessionId, PartyMemberId localMemberId, string detail)
        {
            Failure = failure;
            SessionId = sessionId;
            LocalMemberId = localMemberId;
            Detail = detail ?? string.Empty;
        }

        public static SessionFormationResult Success(GameSessionId sessionId, PartyMemberId localMemberId)
        {
            if (!sessionId.IsValid) throw new ArgumentException("Session id is required.", nameof(sessionId));
            if (!localMemberId.IsValid) throw new ArgumentException("Local member id is required.", nameof(localMemberId));
            return new SessionFormationResult(SessionFormationFailure.None, sessionId, localMemberId, string.Empty);
        }

        public static SessionFormationResult Reject(SessionFormationFailure failure, string detail)
        {
            if (failure == SessionFormationFailure.None) throw new ArgumentException("Rejected result requires a failure.", nameof(failure));
            return new SessionFormationResult(failure, default, default, detail);
        }
    }

    /// <summary>
    /// System07-owned semantic seam for frontend multiplayer formation. Implementations may use local,
    /// LAN or online providers; callers never receive transport handles or socket identifiers.
    /// </summary>
    public interface ISessionFormationService
    {
        SessionFormationResult Host(HostSessionRequest request);
        SessionFormationResult Join(JoinSessionRequest request);
    }
}
