using System;
using System.Collections.Generic;

namespace Game.Sessions.Api
{
    public readonly struct PartyMemberReadySnapshot
    {
        public PartyMemberId MemberId { get; }
        public bool ReadyToStart { get; }
        public PartyMemberReadySnapshot(PartyMemberId memberId, bool readyToStart)
        {
            if (!memberId.IsValid) throw new ArgumentException("Party member id is required.", nameof(memberId));
            MemberId = memberId;
            ReadyToStart = readyToStart;
        }
    }

    public sealed class PartySessionApplicationSnapshot
    {
        private readonly PartyMemberReadySnapshot[] _readyMembers;

        public PartySessionApplicationSnapshot(int capacity, bool gameplayStarted, IReadOnlyList<PartyMemberReadySnapshot> readyMembers)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            if (readyMembers == null) throw new ArgumentNullException(nameof(readyMembers));
            Capacity = capacity;
            GameplayStarted = gameplayStarted;
            _readyMembers = new PartyMemberReadySnapshot[readyMembers.Count];
            for (int i = 0; i < readyMembers.Count; i++) _readyMembers[i] = readyMembers[i];
        }

        public int Capacity { get; }
        public bool GameplayStarted { get; }
        public IReadOnlyList<PartyMemberReadySnapshot> ReadyMembers => _readyMembers;

        public bool TryIsReady(PartyMemberId memberId, out bool ready)
        {
            for (int i = 0; i < _readyMembers.Length; i++)
            {
                if (_readyMembers[i].MemberId != memberId) continue;
                ready = _readyMembers[i].ReadyToStart;
                return true;
            }
            ready = false;
            return false;
        }
    }

    public enum PartySessionCommandFailure : byte
    {
        None = 0,
        UnknownMember = 1,
        NotLeader = 2,
        NotReady = 3,
        AlreadyStarted = 4,
        InvalidRequest = 5
    }

    public readonly struct PartySessionCommandResult
    {
        public bool Accepted => Failure == PartySessionCommandFailure.None;
        public PartySessionCommandFailure Failure { get; }
        private PartySessionCommandResult(PartySessionCommandFailure failure) { Failure = failure; }
        public static PartySessionCommandResult Accept() => new PartySessionCommandResult(PartySessionCommandFailure.None);
        public static PartySessionCommandResult Reject(PartySessionCommandFailure failure) => new PartySessionCommandResult(failure);
    }

    /// <summary>Read-only application state for explicit lobby intent. It is independent from replication GameplayReady.</summary>
    public interface IPartySessionApplicationQuery
    {
        PartySessionApplicationSnapshot Snapshot();
    }

    /// <summary>Semantic ready/start/leave requests for application/UI callers. Implementations own mutation and authority.</summary>
    public interface IPartySessionApplicationCommands
    {
        PartySessionCommandResult SetReady(PartyMemberId memberId, bool ready);
        PartySessionCommandResult RequestStart(PartyMemberId memberId);
        PartySessionCommandResult Leave(PartyMemberId memberId);
    }
}
