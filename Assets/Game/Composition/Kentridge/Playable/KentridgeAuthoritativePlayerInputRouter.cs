using System;
using Game.Characters.Api;
using Game.Sessions.Api;
using Game.Sessions.Runtime;
using VoxelEngine.Net.Runtime.Protocol;
using VoxelEngine.Net.Runtime.Server;

namespace Game.Composition.Kentridge.Playable
{
    /// <summary>
    /// Narrow production command boundary after Net has authenticated a transport-owned player id.
    /// The command sink receives only the durable Characters identity resolved by Sessions; it never
    /// receives transport connection ids, party-member ids, or a client-claimed gameplay identity.
    /// </summary>
    public interface IKentridgeAuthoritativePlayerCommandSink
    {
        void Apply(CharacterId characterId, in C_PlayerInput input, uint serverTick);
    }

    /// <summary>
    /// Resolves the transient network player id assigned by SessionNetworkAdmissionAdapter back
    /// through the authoritative PartySession slot to its durable CharacterId before gameplay input
    /// can cross into Kentridge commands. Invalid, disconnected, not-ready, or unbound identities
    /// fail closed and never reach gameplay mutation.
    /// </summary>
    public sealed class KentridgeAuthoritativePlayerInputRouter : IAuthoritativePlayerInputSink
    {
        private readonly Func<PartySession> _partySession;
        private readonly IKentridgeAuthoritativePlayerCommandSink _commands;

        public KentridgeAuthoritativePlayerInputRouter(
            Func<PartySession> partySession,
            IKentridgeAuthoritativePlayerCommandSink commands)
        {
            _partySession = partySession ?? throw new ArgumentNullException(nameof(partySession));
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        }

        public long RejectedInputs { get; private set; }
        public long AppliedInputs { get; private set; }

        public void ApplyInput(ushort playerId, in C_PlayerInput input, uint serverTick)
        {
            if (playerId == 0)
            {
                RejectedInputs++;
                return;
            }

            PartySession party = _partySession();
            if (party == null)
            {
                RejectedInputs++;
                return;
            }

            // SessionNetworkAdmissionAdapter reserves network id 0 and deterministically derives
            // the transient replication id as slot + 1. Do not inspect any compatibility playerId
            // carried by the input struct; transport authentication is the only identity source.
            int slot = playerId - 1;
            PartyRosterSnapshot roster = party.Snapshot();
            for (int i = 0; i < roster.Members.Count; i++)
            {
                PartyMemberSnapshot member = roster.Members[i];
                if (member.Slot.Value != slot) continue;
                if (member.Presence != PartyPresenceState.Connected ||
                    member.Readiness != SessionReadinessState.GameplayReady ||
                    !member.HasCharacter)
                {
                    RejectedInputs++;
                    return;
                }

                _commands.Apply(member.CharacterId, in input, serverTick);
                AppliedInputs++;
                return;
            }

            RejectedInputs++;
        }
    }
}
