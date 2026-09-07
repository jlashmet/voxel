using Game.Characters.Api;
using Game.Composition.Kentridge.Playable;
using Game.Sessions.Api;
using Game.Sessions.Runtime;
using NUnit.Framework;
using VoxelEngine.Net.Runtime.Protocol;

namespace Game.Composition.Kentridge.Playable.Tests
{
    public sealed class KentridgeAuthoritativePlayerInputRouterTests
    {
        [Test]
        public void RoutesAuthenticatedNetworkPlayerThroughDurableSlotToBoundCharacter()
        {
            PartySession party = CreateParty(out PartyMemberSnapshot first, out PartyMemberSnapshot second);
            var character = new CharacterId("kentridge-player-2");
            Assert.That(party.BindCharacter(second.MemberId, character), Is.True);
            MakeGameplayReady(party, second.MemberId, 22);
            var sink = new CapturingCommandSink();
            var router = new KentridgeAuthoritativePlayerInputRouter(() => party, sink);
            C_PlayerInput input = Input(41, 7);

            router.ApplyInput(2, in input, 99);

            Assert.That(sink.Calls, Is.EqualTo(1));
            Assert.That(sink.CharacterId, Is.EqualTo(character));
            Assert.That(sink.Input.sequence, Is.EqualTo(7));
            Assert.That(sink.ServerTick, Is.EqualTo(99));
            Assert.That(router.AppliedInputs, Is.EqualTo(1));
            Assert.That(router.RejectedInputs, Is.Zero);
        }

        [Test]
        public void IgnoresCompatibilityPlayerIdAndUsesAuthenticatedNetworkIdentity()
        {
            PartySession party = CreateParty(out PartyMemberSnapshot first, out PartyMemberSnapshot second);
            var firstCharacter = new CharacterId("kentridge-player-1");
            var secondCharacter = new CharacterId("kentridge-player-2");
            Assert.That(party.BindCharacter(first.MemberId, firstCharacter), Is.True);
            Assert.That(party.BindCharacter(second.MemberId, secondCharacter), Is.True);
            MakeGameplayReady(party, first.MemberId, 11);
            MakeGameplayReady(party, second.MemberId, 22);
            var sink = new CapturingCommandSink();
            var router = new KentridgeAuthoritativePlayerInputRouter(() => party, sink);
            C_PlayerInput input = Input(41, 7);
#pragma warning disable CS0618
            input.playerId = 1;
#pragma warning restore CS0618

            router.ApplyInput(2, in input, 100);

            Assert.That(sink.Calls, Is.EqualTo(1));
            Assert.That(sink.CharacterId, Is.EqualTo(secondCharacter));
            Assert.That(router.RejectedInputs, Is.Zero);
        }

        [Test]
        public void FailsClosedWhenDurableMemberIsDisconnectedOrNotCharacterBound()
        {
            PartySession party = CreateParty(out _, out PartyMemberSnapshot second);
            var sink = new CapturingCommandSink();
            var router = new KentridgeAuthoritativePlayerInputRouter(() => party, sink);
            C_PlayerInput input = Input(41, 7);

            MakeGameplayReady(party, second.MemberId, 22);
            router.ApplyInput(2, in input, 101);
            Assert.That(sink.Calls, Is.Zero, "unbound durable member must not mutate gameplay");

            Assert.That(party.BindCharacter(second.MemberId, new CharacterId("kentridge-player-2")), Is.True);
            Assert.That(party.Disconnect(SessionNetworkAdmissionAdapter.FromConnectionId(22)), Is.True);
            router.ApplyInput(2, in input, 102);

            Assert.That(sink.Calls, Is.Zero, "disconnected durable member must not mutate gameplay");
            Assert.That(router.RejectedInputs, Is.EqualTo(2));
        }

        private static PartySession CreateParty(
            out PartyMemberSnapshot first,
            out PartyMemberSnapshot second)
        {
            var party = new PartySession(
                new GameSessionId("live"),
                new SessionStartupConfiguration(3, "v1", "content", true));
            JoinResult firstJoin = party.Join(new JoinRequest(
                new GameSessionId("live"), "host", "v1", "content"));
            JoinResult secondJoin = party.Join(new JoinRequest(
                new GameSessionId("live"), "client-a", "v1", "content"));
            Assert.That(firstJoin.Accepted, Is.True);
            Assert.That(secondJoin.Accepted, Is.True);
            first = firstJoin.Member;
            second = secondJoin.Member;
            return party;
        }

        private static void MakeGameplayReady(PartySession party, PartyMemberId memberId, uint connectionId)
        {
            Assert.That(party.BindConnection(
                memberId,
                SessionNetworkAdmissionAdapter.FromConnectionId(connectionId)), Is.True);
            Assert.That(party.MarkSynchronized(memberId), Is.True);
            Assert.That(party.MarkGameplayReady(memberId), Is.True);
        }

        private static C_PlayerInput Input(uint tick, ushort sequence) =>
            new C_PlayerInput
            {
                tick = tick,
                sequence = sequence,
                actions = (ushort)C_PlayerInput.ActionBits.UseMain
            };

        private sealed class CapturingCommandSink : IKentridgeAuthoritativePlayerCommandSink
        {
            public int Calls { get; private set; }
            public CharacterId CharacterId { get; private set; }
            public C_PlayerInput Input { get; private set; }
            public uint ServerTick { get; private set; }

            public void Apply(CharacterId characterId, in C_PlayerInput input, uint serverTick)
            {
                Calls++;
                CharacterId = characterId;
                Input = input;
                ServerTick = serverTick;
            }
        }
    }
}
