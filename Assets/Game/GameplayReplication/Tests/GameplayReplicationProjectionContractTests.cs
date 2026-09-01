using System.Collections.Generic;
using Game.Characters.Api;
using Game.Continuity.Api;
using Game.GameplayReplication.Adapters;
using Game.GameplayReplication.Api;
using Game.GameplayReplication.Runtime;
using Game.Outcomes.Api;
using Game.Progression.Api;
using Game.Sessions.Api;
using Game.Vitality.Api;
using NUnit.Framework;

namespace Game.GameplayReplication.Tests
{
    public sealed class GameplayReplicationProjectionContractTests
    {
        [Test]
        public void MinimalOwningApiSnapshotsProduceDeterministicSemanticProjections()
        {
            var vitality = new VitalityGameplayProjectionSource(new VitalityQueryFixture(
                new VitalitySnapshot(new CharacterId("character:z"), 0, 80, true, 7),
                new VitalitySnapshot(new CharacterId("character:a"), 65, 100, false, 5)));

            var progression = new ProgressionGameplayProjectionSource(new ProgressionQueryFixture(
                new ProgressionSnapshot(
                    12,
                    new[]
                    {
                        new QuestProgressSnapshot(
                            new QuestId("quest:ridge"),
                            ProgressionLifecycleState.Active,
                            new[]
                            {
                                new ObjectiveProgressSnapshot(new ObjectiveId("objective:exit"), ProgressionLifecycleState.Inactive, 2),
                                new ObjectiveProgressSnapshot(new ObjectiveId("objective:enter"), ProgressionLifecycleState.Completed, 3)
                            },
                            4)
                    },
                    new[]
                    {
                        new ObjectiveProgressSnapshot(new ObjectiveId("objective:camp"), ProgressionLifecycleState.Active, 9)
                    })));

            var sessions = new SessionQueryFixture(
                new GameSessionId("session:co-op"),
                new PartyMemberSnapshot(new PartyMemberId("member:z"), new PlayerSlot(3), PartyLeadershipRole.Member, PartyPresenceState.Connected, SessionReadinessState.GameplayReady, new CharacterId("character:z")),
                new PartyMemberSnapshot(new PartyMemberId("member:a"), new PlayerSlot(0), PartyLeadershipRole.Leader, PartyPresenceState.Connected, SessionReadinessState.GameplayReady, new CharacterId("character:a")));
            var continuity = new ContinuityGameplayProjectionSource(
                new ContinuityQueryFixture(
                    new RecoverySnapshot(new PartyMemberId("member:z"), RecoveryState.Resynchronizing, 30.5),
                    new RecoverySnapshot(new PartyMemberId("member:a"), RecoveryState.Connected, 0)),
                sessions);

            var outcomes = new OutcomesGameplayProjectionSource(new OutcomeQueryFixture(
                new GameOutcomeSnapshot(
                    GameOutcomeLifecycle.Resolved,
                    GameOutcomeDisposition.Success,
                    new OutcomeRef("campaign:ridge-secured"),
                    2)));

            var builder = new GameplayPublicationBuilder(new IGameplayProjectionSource[]
            {
                vitality,
                progression,
                outcomes,
                continuity
            });

            GameplayPublication publication = builder.PublishSnapshot();

            Assert.That(publication.Projections[0].Descriptor.Id.Value, Is.EqualTo("continuity"));
            Assert.That(publication.Projections[1].Descriptor.Id.Value, Is.EqualTo("outcomes"));
            Assert.That(publication.Projections[2].Descriptor.Id.Value, Is.EqualTo("progression"));
            Assert.That(publication.Projections[3].Descriptor.Id.Value, Is.EqualTo("vitality"));

            AssertEntry(publication.Projections[0], "member/member:a/state", "Connected");
            AssertEntry(publication.Projections[0], "member/member:z/state", "Resynchronizing");
            AssertEntry(publication.Projections[0], "member/member:z/grace-deadline", "30.5");
            AssertEntry(publication.Projections[1], "lifecycle", "Resolved");
            AssertEntry(publication.Projections[1], "disposition", "Success");
            AssertEntry(publication.Projections[1], "outcome-ref", "campaign:ridge-secured");
            AssertEntry(publication.Projections[2], "quest/quest:ridge/objective/objective:enter/state", "Completed");
            AssertEntry(publication.Projections[2], "objective/objective:camp/state", "Active");
            AssertEntry(publication.Projections[3], "character:a/current", "65");
            AssertEntry(publication.Projections[3], "character:z/defeated", "true");
        }

        private static void AssertEntry(GameplayProjectionState state, string key, string expectedValue)
        {
            for (int i = 0; i < state.Entries.Count; i++)
            {
                if (state.Entries[i].Key == key)
                {
                    Assert.That(state.Entries[i].Value, Is.EqualTo(expectedValue));
                    return;
                }
            }
            Assert.Fail("Missing gameplay projection entry: " + key);
        }

        private sealed class VitalityQueryFixture : IVitalityQuery
        {
            private readonly VitalitySnapshot[] _snapshots;
            public VitalityQueryFixture(params VitalitySnapshot[] snapshots) => _snapshots = snapshots;
            public IReadOnlyList<VitalitySnapshot> GetAll() => _snapshots;
            public bool TryGet(CharacterId characterId, out VitalitySnapshot snapshot)
            {
                for (int i = 0; i < _snapshots.Length; i++)
                {
                    if (_snapshots[i].CharacterId == characterId) { snapshot = _snapshots[i]; return true; }
                }
                snapshot = default;
                return false;
            }
        }

        private sealed class ProgressionQueryFixture : IProgressionQuery
        {
            private readonly ProgressionSnapshot _snapshot;
            public ProgressionQueryFixture(ProgressionSnapshot snapshot) => _snapshot = snapshot;
            public ProgressionSnapshot Snapshot() => _snapshot;
        }

        private sealed class SessionQueryFixture : IPartySessionQuery
        {
            private readonly PartyRosterSnapshot _snapshot;
            public SessionQueryFixture(GameSessionId sessionId, params PartyMemberSnapshot[] members)
                => _snapshot = new PartyRosterSnapshot(sessionId, members);
            public PartyRosterSnapshot Snapshot() => _snapshot;
            public bool TryGetMember(PartyMemberId memberId, out PartyMemberSnapshot member)
            {
                for (int i = 0; i < _snapshot.Members.Count; i++)
                {
                    if (_snapshot.Members[i].MemberId == memberId)
                    {
                        member = _snapshot.Members[i];
                        return true;
                    }
                }
                member = default;
                return false;
            }
        }

        private sealed class ContinuityQueryFixture : IContinuityQuery
        {
            private readonly RecoverySnapshot[] _snapshots;
            public ContinuityQueryFixture(params RecoverySnapshot[] snapshots) => _snapshots = snapshots;
            public bool TryGetRecovery(PartyMemberId memberId, out RecoverySnapshot recovery)
            {
                for (int i = 0; i < _snapshots.Length; i++)
                {
                    if (_snapshots[i].MemberId == memberId)
                    {
                        recovery = _snapshots[i];
                        return true;
                    }
                }
                recovery = default;
                return false;
            }
        }

        private sealed class OutcomeQueryFixture : IGameOutcomeQuery
        {
            private readonly GameOutcomeSnapshot _snapshot;
            public OutcomeQueryFixture(GameOutcomeSnapshot snapshot) => _snapshot = snapshot;
            public GameOutcomeSnapshot Snapshot() => _snapshot;
        }
    }
}
