using System;
using System.Collections.Generic;
using Game.Characters.Api;

namespace Game.Sessions.Api
{
    /// <summary>
    /// Durable Sessions identity only. Applicant/member/slot/character bindings may cross an authority
    /// process boundary; transport handles, readiness and connection state deliberately cannot.
    /// CharacterId may be unset for a lobby member that has not reached character binding yet.
    /// </summary>
    public readonly struct PartyMemberStateCapture
    {
        public PartyMemberId MemberId { get; }
        public PlayerSlot Slot { get; }
        public string ApplicantKey { get; }
        public PartyLeadershipRole LeadershipRole { get; }
        public CharacterId CharacterId { get; }
        public bool HasCharacter => CharacterId.IsValid;

        public PartyMemberStateCapture(
            PartyMemberId memberId,
            PlayerSlot slot,
            string applicantKey,
            PartyLeadershipRole leadershipRole,
            CharacterId characterId = default)
        {
            if (!memberId.IsValid) throw new ArgumentException("Member id is required.", nameof(memberId));
            if (string.IsNullOrWhiteSpace(applicantKey)) throw new ArgumentException("Applicant key is required.", nameof(applicantKey));
            MemberId = memberId;
            Slot = slot;
            ApplicantKey = applicantKey.Trim();
            LeadershipRole = leadershipRole;
            CharacterId = characterId;
        }
    }

    public sealed class PartySessionStateCapture
    {
        private readonly PartyMemberStateCapture[] _members;

        public GameSessionId SessionId { get; }
        public ulong NextMemberOrdinal { get; }
        public IReadOnlyList<PartyMemberStateCapture> Members => _members;

        public PartySessionStateCapture(
            GameSessionId sessionId,
            ulong nextMemberOrdinal,
            IReadOnlyList<PartyMemberStateCapture> members)
        {
            if (!sessionId.IsValid) throw new ArgumentException("Session id is required.", nameof(sessionId));
            if (nextMemberOrdinal == 0) throw new ArgumentOutOfRangeException(nameof(nextMemberOrdinal));
            if (members == null) throw new ArgumentNullException(nameof(members));
            _members = new PartyMemberStateCapture[members.Count];
            for (int i = 0; i < members.Count; i++) _members[i] = members[i];
            SessionId = sessionId;
            NextMemberOrdinal = nextMemberOrdinal;
        }
    }

    public enum PartySessionRestoreFailure : byte
    {
        None = 0,
        SessionMismatch = 1,
        InvalidState = 2,
        CapacityExceeded = 3,
        DuplicateMember = 4,
        DuplicateApplicant = 5,
        DuplicateSlot = 6,
        DuplicateCharacter = 7,
        LiveMemberMissing = 8,
        CharacterBindingRejected = 9
    }

    public interface IPartySessionStatePort
    {
        PartySessionStateCapture CaptureState();
        PartySessionRestoreFailure RestoreState(PartySessionStateCapture state);
    }
}
