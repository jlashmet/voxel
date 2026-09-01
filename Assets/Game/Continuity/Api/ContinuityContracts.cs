using System;
using System.Collections.Generic;
using Game.Sessions.Api;

namespace Game.Continuity.Api
{
    public enum ContinuityRecoveryState : byte
    {
        Connected = 0,
        ConnectionInterrupted = 1,
        Reconnecting = 2,
        Resynchronizing = 3,
        Recovered = 4,
        Expired = 5
    }

    public readonly struct ContinuityMemberSnapshot
    {
        public PartyMemberId MemberId { get; }
        public ContinuityRecoveryState State { get; }
        public ulong Revision { get; }
        public ContinuityMemberSnapshot(PartyMemberId memberId, ContinuityRecoveryState state, ulong revision)
        {
            if (!memberId.IsValid) throw new ArgumentException("Party member id is required.", nameof(memberId));
            MemberId = memberId; State = state; Revision = revision;
        }
    }

    public sealed class ContinuitySnapshot
    {
        public ulong Revision { get; }
        public IReadOnlyList<ContinuityMemberSnapshot> Members { get; }
        public ContinuitySnapshot(ulong revision, IReadOnlyList<ContinuityMemberSnapshot> members)
        {
            if (members == null) throw new ArgumentNullException(nameof(members));
            var copy = new ContinuityMemberSnapshot[members.Count];
            for (int i = 0; i < members.Count; i++) copy[i] = members[i];
            Revision = revision; Members = copy;
        }
    }

    public interface IContinuityQuery
    {
        ContinuitySnapshot Snapshot();
    }
}
