using System;
using System.Collections.Generic;
using Game.Characters.Api;
using Game.Continuity.Api;
using Game.GameplayReplication.Api;
using Game.SessionPresentation.Api;
using Game.Sessions.Api;

namespace Game.SessionPresentation.Runtime
{
    public sealed class SessionPresentationProjector : IPartyScreenPresentationQuery, ITeammateHudPresentationQuery
    {
        private readonly IPartySessionQuery _sessions;
        private readonly IPartySessionApplicationQuery _application;
        private readonly IContinuityQuery _continuity;
        private readonly IGameplayReplicationClientState _replication;
        private readonly ISessionMemberDisplayMetadataResolver _display;

        public SessionPresentationProjector(
            IPartySessionQuery sessions,
            IPartySessionApplicationQuery application,
            IContinuityQuery continuity,
            IGameplayReplicationClientState replication,
            ISessionMemberDisplayMetadataResolver display = null)
        {
            _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
            _application = application ?? throw new ArgumentNullException(nameof(application));
            _continuity = continuity ?? throw new ArgumentNullException(nameof(continuity));
            _replication = replication ?? throw new ArgumentNullException(nameof(replication));
            _display = display ?? new DefaultSessionMemberDisplayMetadataResolver();
        }

        public PartyScreenPresentationSnapshot CapturePartyScreen(PartyMemberId localMemberId)
        {
            PartyRosterSnapshot roster = _sessions.Snapshot();
            PartySessionApplicationSnapshot application = _application.Snapshot();
            var source = new List<PartyMemberSnapshot>(roster.Members.Count);
            for (int i = 0; i < roster.Members.Count; i++) source.Add(roster.Members[i]);
            source.Sort(CompareMembers);

            var rows = new PartyMemberPresentationSnapshot[source.Count];
            for (int i = 0; i < source.Count; i++) rows[i] = ProjectMember(source[i], localMemberId, application);

            SessionPresentationLifecycle lifecycle = DeriveLifecycle(rows, application.GameplayStarted);
            bool canStart = lifecycle == SessionPresentationLifecycle.ReadyToStart && LocalMemberIsLeader(rows);
            return new PartyScreenPresentationSnapshot(roster.SessionId, application.Capacity, lifecycle, canStart, rows);
        }

        public TeammateHudPresentationSnapshot CaptureTeammateHud(PartyMemberId localMemberId)
        {
            PartyScreenPresentationSnapshot party = CapturePartyScreen(localMemberId);
            var rows = new TeammateStatusSnapshot[party.Members.Count];
            for (int i = 0; i < party.Members.Count; i++)
            {
                PartyMemberPresentationSnapshot member = party.Members[i];
                rows[i] = new TeammateStatusSnapshot(
                    member.MemberId,
                    member.Slot,
                    member.CharacterId,
                    member.IsLocal,
                    member.Connection,
                    member.Readiness,
                    member.ReadyToStart,
                    member.GameplayReady,
                    member.Display.PrimaryLabel);
            }
            return new TeammateHudPresentationSnapshot(rows);
        }

        private PartyMemberPresentationSnapshot ProjectMember(
            PartyMemberSnapshot member,
            PartyMemberId localMemberId,
            PartySessionApplicationSnapshot application)
        {
            MemberConnectionPresentationState connection = DeriveConnection(member);
            bool gameplayReady = _replication.TryGetSynchronization(member.MemberId, out GameplaySynchronizationStatus synchronization)
                && synchronization.GameplayReady;
            MemberReadinessPresentationState readiness = gameplayReady
                ? MemberReadinessPresentationState.GameplayReady
                : connection == MemberConnectionPresentationState.Connected
                    ? MemberReadinessPresentationState.Synchronizing
                    : MemberReadinessPresentationState.WaitingForConnection;
            bool readyToStart = application.TryIsReady(member.MemberId, out bool ready) && ready;
            MemberDisplayMetadata display = _display.Resolve(member.MemberId, member.Slot, member.CharacterId, member.LeadershipRole);
            return new PartyMemberPresentationSnapshot(
                member.MemberId,
                member.Slot,
                member.CharacterId,
                member.LeadershipRole,
                member.MemberId == localMemberId,
                connection,
                readiness,
                readyToStart,
                gameplayReady,
                display);
        }

        private MemberConnectionPresentationState DeriveConnection(PartyMemberSnapshot member)
        {
            if (_continuity.TryGetRecovery(member.MemberId, out RecoverySnapshot recovery))
            {
                switch (recovery.State)
                {
                    case RecoveryState.ConnectionInterrupted: return MemberConnectionPresentationState.Interrupted;
                    case RecoveryState.Reconnecting: return MemberConnectionPresentationState.Reconnecting;
                    case RecoveryState.Resynchronizing: return MemberConnectionPresentationState.Resynchronizing;
                    case RecoveryState.Expired: return MemberConnectionPresentationState.Expired;
                    case RecoveryState.Left: return MemberConnectionPresentationState.Left;
                }
            }

            switch (member.Presence)
            {
                case PartyPresenceState.Connected: return MemberConnectionPresentationState.Connected;
                case PartyPresenceState.Disconnected: return MemberConnectionPresentationState.Interrupted;
                default: return MemberConnectionPresentationState.Joined;
            }
        }

        private static SessionPresentationLifecycle DeriveLifecycle(PartyMemberPresentationSnapshot[] rows, bool gameplayStarted)
        {
            if (gameplayStarted) return SessionPresentationLifecycle.Active;
            if (rows.Length == 0) return SessionPresentationLifecycle.Empty;

            bool allStartReady = true;
            bool anyConnected = false;
            for (int i = 0; i < rows.Length; i++)
            {
                allStartReady &= rows[i].ReadyToStart && rows[i].GameplayReady;
                anyConnected |= rows[i].Connection == MemberConnectionPresentationState.Connected;
            }
            if (allStartReady) return SessionPresentationLifecycle.ReadyToStart;
            return anyConnected ? SessionPresentationLifecycle.Synchronizing : SessionPresentationLifecycle.WaitingForPlayers;
        }

        private static bool LocalMemberIsLeader(PartyMemberPresentationSnapshot[] rows)
        {
            for (int i = 0; i < rows.Length; i++)
                if (rows[i].IsLocal && rows[i].LeadershipRole == PartyLeadershipRole.Leader) return true;
            return false;
        }

        private static int CompareMembers(PartyMemberSnapshot left, PartyMemberSnapshot right)
        {
            int slot = left.Slot.CompareTo(right.Slot);
            return slot != 0 ? slot : left.MemberId.CompareTo(right.MemberId);
        }
    }

    public sealed class DefaultSessionMemberDisplayMetadataResolver : ISessionMemberDisplayMetadataResolver
    {
        public MemberDisplayMetadata Resolve(PartyMemberId memberId, PlayerSlot slot, CharacterId characterId, PartyLeadershipRole leadershipRole)
        {
            string primary = "Player " + (slot.Value + 1);
            string secondary = leadershipRole == PartyLeadershipRole.Leader ? "Leader" : "Teammate";
            string character = characterId.IsValid ? characterId.Value : "Character unassigned";
            return new MemberDisplayMetadata(primary, secondary, character);
        }
    }

    public sealed class SessionPresentationIntentRouter : ISessionPresentationIntentRouter
    {
        private readonly IPartySessionApplicationCommands _commands;
        public SessionPresentationIntentRouter(IPartySessionApplicationCommands commands) =>
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));

        public PartySessionCommandResult Request(SessionPresentationIntent intent)
        {
            switch (intent.Kind)
            {
                case SessionPresentationIntentKind.SetReady: return _commands.SetReady(intent.MemberId, intent.Ready);
                case SessionPresentationIntentKind.Start: return _commands.RequestStart(intent.MemberId);
                case SessionPresentationIntentKind.Leave: return _commands.Leave(intent.MemberId);
                default: return PartySessionCommandResult.Reject(PartySessionCommandFailure.InvalidRequest);
            }
        }
    }
}
