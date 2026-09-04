using System;
using System.Collections.Generic;
using Game.Characters.Api;
using Game.Sessions.Api;

namespace Game.SessionPresentation.Api
{
    public enum MemberConnectionPresentationState : byte
    {
        Joined = 0,
        Connected = 1,
        Interrupted = 2,
        Reconnecting = 3,
        Resynchronizing = 4,
        Expired = 5,
        Left = 6
    }

    public enum MemberReadinessPresentationState : byte
    {
        WaitingForConnection = 0,
        Synchronizing = 1,
        GameplayReady = 2
    }

    public enum SessionPresentationLifecycle : byte
    {
        Empty = 0,
        WaitingForPlayers = 1,
        Synchronizing = 2,
        ReadyToStart = 3,
        Active = 4
    }

    public readonly struct MemberDisplayMetadata
    {
        public string PrimaryLabel { get; }
        public string SecondaryLabel { get; }
        public string CharacterLabel { get; }

        public MemberDisplayMetadata(string primaryLabel, string secondaryLabel, string characterLabel)
        {
            PrimaryLabel = primaryLabel ?? string.Empty;
            SecondaryLabel = secondaryLabel ?? string.Empty;
            CharacterLabel = characterLabel ?? string.Empty;
        }
    }

    public readonly struct PartyMemberPresentationSnapshot
    {
        public PartyMemberId MemberId { get; }
        public PlayerSlot Slot { get; }
        public CharacterId CharacterId { get; }
        public bool HasCharacter => CharacterId.IsValid;
        public PartyLeadershipRole LeadershipRole { get; }
        public bool IsLocal { get; }
        public MemberConnectionPresentationState Connection { get; }
        public MemberReadinessPresentationState Readiness { get; }
        public bool ReadyToStart { get; }
        public bool GameplayReady { get; }
        public MemberDisplayMetadata Display { get; }

        public PartyMemberPresentationSnapshot(
            PartyMemberId memberId,
            PlayerSlot slot,
            CharacterId characterId,
            PartyLeadershipRole leadershipRole,
            bool isLocal,
            MemberConnectionPresentationState connection,
            MemberReadinessPresentationState readiness,
            bool readyToStart,
            bool gameplayReady,
            MemberDisplayMetadata display)
        {
            if (!memberId.IsValid) throw new ArgumentException("Durable party member id is required.", nameof(memberId));
            MemberId = memberId;
            Slot = slot;
            CharacterId = characterId;
            LeadershipRole = leadershipRole;
            IsLocal = isLocal;
            Connection = connection;
            Readiness = readiness;
            ReadyToStart = readyToStart;
            GameplayReady = gameplayReady;
            Display = display;
        }
    }

    public readonly struct TeammateStatusSnapshot
    {
        public PartyMemberId MemberId { get; }
        public PlayerSlot Slot { get; }
        public CharacterId CharacterId { get; }
        public bool HasCharacter => CharacterId.IsValid;
        public bool IsLocal { get; }
        public MemberConnectionPresentationState Connection { get; }
        public MemberReadinessPresentationState Readiness { get; }
        public bool ReadyToStart { get; }
        public bool GameplayReady { get; }
        public string Label { get; }

        public TeammateStatusSnapshot(
            PartyMemberId memberId,
            PlayerSlot slot,
            CharacterId characterId,
            bool isLocal,
            MemberConnectionPresentationState connection,
            MemberReadinessPresentationState readiness,
            bool readyToStart,
            bool gameplayReady,
            string label)
        {
            MemberId = memberId;
            Slot = slot;
            CharacterId = characterId;
            IsLocal = isLocal;
            Connection = connection;
            Readiness = readiness;
            ReadyToStart = readyToStart;
            GameplayReady = gameplayReady;
            Label = label ?? string.Empty;
        }
    }

    public sealed class PartyScreenPresentationSnapshot
    {
        private readonly PartyMemberPresentationSnapshot[] _members;

        public PartyScreenPresentationSnapshot(
            GameSessionId sessionId,
            int capacity,
            SessionPresentationLifecycle lifecycle,
            bool canStart,
            IReadOnlyList<PartyMemberPresentationSnapshot> members)
        {
            if (!sessionId.IsValid) throw new ArgumentException("Session id is required.", nameof(sessionId));
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            if (members == null) throw new ArgumentNullException(nameof(members));
            SessionId = sessionId;
            Capacity = capacity;
            Lifecycle = lifecycle;
            CanStart = canStart;
            _members = new PartyMemberPresentationSnapshot[members.Count];
            for (int i = 0; i < members.Count; i++) _members[i] = members[i];
        }

        public GameSessionId SessionId { get; }
        public int Capacity { get; }
        public SessionPresentationLifecycle Lifecycle { get; }
        public bool CanStart { get; }
        public IReadOnlyList<PartyMemberPresentationSnapshot> Members => _members;
    }

    public sealed class TeammateHudPresentationSnapshot
    {
        private readonly TeammateStatusSnapshot[] _members;
        public TeammateHudPresentationSnapshot(IReadOnlyList<TeammateStatusSnapshot> members)
        {
            if (members == null) throw new ArgumentNullException(nameof(members));
            _members = new TeammateStatusSnapshot[members.Count];
            for (int i = 0; i < members.Count; i++) _members[i] = members[i];
        }
        public IReadOnlyList<TeammateStatusSnapshot> Members => _members;
    }

    public interface ISessionMemberDisplayMetadataResolver
    {
        MemberDisplayMetadata Resolve(PartyMemberId memberId, PlayerSlot slot, CharacterId characterId, PartyLeadershipRole leadershipRole);
    }

    public interface IPartyScreenPresentationQuery
    {
        PartyScreenPresentationSnapshot CapturePartyScreen(PartyMemberId localMemberId);
    }

    public interface ITeammateHudPresentationQuery
    {
        TeammateHudPresentationSnapshot CaptureTeammateHud(PartyMemberId localMemberId);
    }

    public enum SessionPresentationIntentKind : byte
    {
        SetReady = 0,
        Start = 1,
        Leave = 2
    }

    public readonly struct SessionPresentationIntent
    {
        public SessionPresentationIntentKind Kind { get; }
        public PartyMemberId MemberId { get; }
        public bool Ready { get; }

        private SessionPresentationIntent(SessionPresentationIntentKind kind, PartyMemberId memberId, bool ready)
        {
            if (!memberId.IsValid) throw new ArgumentException("Party member id is required.", nameof(memberId));
            Kind = kind;
            MemberId = memberId;
            Ready = ready;
        }

        public static SessionPresentationIntent SetReady(PartyMemberId memberId, bool ready) =>
            new SessionPresentationIntent(SessionPresentationIntentKind.SetReady, memberId, ready);
        public static SessionPresentationIntent Start(PartyMemberId memberId) =>
            new SessionPresentationIntent(SessionPresentationIntentKind.Start, memberId, false);
        public static SessionPresentationIntent Leave(PartyMemberId memberId) =>
            new SessionPresentationIntent(SessionPresentationIntentKind.Leave, memberId, false);
    }

    public interface ISessionPresentationIntentRouter
    {
        PartySessionCommandResult Request(SessionPresentationIntent intent);
    }
}
