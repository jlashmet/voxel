using System.Collections.Generic;
using Game.Continuity.Api;
using Game.Sessions.Api;
using NUnit.Framework;

namespace Game.Continuity.Tests
{
    public sealed class ContinuityApiContractTests
    {
        [Test]
        public void SnapshotUsesDurablePartyIdentityAndOwnsItsMemberList()
        {
            var members = new List<ContinuityMemberSnapshot>
            {
                new ContinuityMemberSnapshot(new PartyMemberId("member:hero"), ContinuityRecoveryState.Resynchronizing, 8)
            };
            var snapshot = new ContinuitySnapshot(9, members);
            members.Clear();

            Assert.That(snapshot.Revision, Is.EqualTo(9));
            Assert.That(snapshot.Members.Count, Is.EqualTo(1));
            Assert.That(snapshot.Members[0].MemberId.Value, Is.EqualTo("member:hero"));
            Assert.That(snapshot.Members[0].State, Is.EqualTo(ContinuityRecoveryState.Resynchronizing));
        }
    }
}
