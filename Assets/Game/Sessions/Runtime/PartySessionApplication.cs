using System;
using System.Collections.Generic;
using Game.Sessions.Api;

namespace Game.Sessions.Runtime
{
    /// <summary>
    /// Sessions-owned semantic application seam for lobby ready/start/leave intent.
    /// Transport connection handles remain private to PartySession and are never accepted here.
    /// </summary>
    public sealed class PartySessionApplication : IPartySessionApplicationQuery, IPartySessionApplicationCommands
    {
        private readonly PartySession _session;
        private readonly int _capacity;
        private readonly HashSet<PartyMemberId> _readyMembers = new HashSet<PartyMemberId>();
        private bool _gameplayStarted;

        public PartySessionApplication(PartySession session, int capacity)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            _capacity = capacity;
        }

        public PartySessionApplicationSnapshot Snapshot()
        {
            PartyRosterSnapshot roster = _session.Snapshot();
            var ready = new PartyMemberReadySnapshot[roster.Members.Count];
            for (int i = 0; i < roster.Members.Count; i++)
            {
                PartyMemberId memberId = roster.Members[i].MemberId;
                ready[i] = new PartyMemberReadySnapshot(memberId, _readyMembers.Contains(memberId));
            }
            return new PartySessionApplicationSnapshot(_capacity, _gameplayStarted, ready);
        }

        public PartySessionCommandResult SetReady(PartyMemberId memberId, bool ready)
        {
            if (_gameplayStarted) return PartySessionCommandResult.Reject(PartySessionCommandFailure.AlreadyStarted);
            if (!_session.TryGetMember(memberId, out _)) return PartySessionCommandResult.Reject(PartySessionCommandFailure.UnknownMember);
            if (ready) _readyMembers.Add(memberId); else _readyMembers.Remove(memberId);
            return PartySessionCommandResult.Accept();
        }

        public PartySessionCommandResult RequestStart(PartyMemberId memberId)
        {
            if (_gameplayStarted) return PartySessionCommandResult.Reject(PartySessionCommandFailure.AlreadyStarted);
            if (!_session.TryGetMember(memberId, out PartyMemberSnapshot requester))
                return PartySessionCommandResult.Reject(PartySessionCommandFailure.UnknownMember);
            if (requester.LeadershipRole != PartyLeadershipRole.Leader)
                return PartySessionCommandResult.Reject(PartySessionCommandFailure.NotLeader);

            PartyRosterSnapshot roster = _session.Snapshot();
            if (roster.Members.Count == 0) return PartySessionCommandResult.Reject(PartySessionCommandFailure.NotReady);
            for (int i = 0; i < roster.Members.Count; i++)
            {
                PartyMemberSnapshot member = roster.Members[i];
                if (!_readyMembers.Contains(member.MemberId) || member.Readiness != SessionReadinessState.GameplayReady)
                    return PartySessionCommandResult.Reject(PartySessionCommandFailure.NotReady);
            }

            if (!_session.StartGameplay()) return PartySessionCommandResult.Reject(PartySessionCommandFailure.NotReady);
            _gameplayStarted = true;
            return PartySessionCommandResult.Accept();
        }

        public PartySessionCommandResult Leave(PartyMemberId memberId)
        {
            if (!_session.TryGetMember(memberId, out _)) return PartySessionCommandResult.Reject(PartySessionCommandFailure.UnknownMember);
            _readyMembers.Remove(memberId);
            return _session.Remove(memberId)
                ? PartySessionCommandResult.Accept()
                : PartySessionCommandResult.Reject(PartySessionCommandFailure.InvalidRequest);
        }
    }
}
