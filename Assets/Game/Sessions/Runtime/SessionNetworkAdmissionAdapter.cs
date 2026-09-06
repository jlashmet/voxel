using System;
using Game.Sessions.Api;
using VoxelEngine.Net.Api;

namespace Game.Sessions.Runtime
{
    /// <summary>
    /// Bridges durable Sessions identity to lower-level network admission. The generated network
    /// player id is a transient replication key derived from the stable slot; it is never party identity.
    /// Calls run on the owning simulation thread. The Net port must not reenter or mutate Sessions.
    /// </summary>
    public sealed class SessionNetworkAdmissionAdapter
    {
        private readonly PartySession _session;
        private readonly IAuthoritativePlayerAdmission _networkAdmission;
        private bool _authenticating;

        public SessionNetworkAdmissionAdapter(PartySession session, IAuthoritativePlayerAdmission networkAdmission)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _networkAdmission = networkAdmission ?? throw new ArgumentNullException(nameof(networkAdmission));
        }

        public bool Authenticate(PartyMemberId memberId, uint connectionId, NetworkSpawnPosition authoritativePosition, int reachVoxels, bool canAlterWorld)
        {
            if (_authenticating || connectionId == 0 || reachVoxels <= 0 ||
                !_session.TryGetMember(memberId, out PartyMemberSnapshot member) ||
                member.Slot.Value >= ushort.MaxValue)
                return false;

            TransportConnectionHandle handle = FromConnectionId(connectionId);
            bool hasOwner = _session.TryResolveConnection(handle, out PartyMemberId owner);
            if (hasOwner && owner != memberId)
                return false;

            bool alreadyConnected = member.Presence == PartyPresenceState.Connected;
            // Do not move a durable identity away from a live connection. Its owning transport
            // and Continuity flow must process the old disconnect before admitting a replacement.
            if (alreadyConnected && !hasOwner)
                return false;
            if (!alreadyConnected && hasOwner)
                return false;

            ushort networkPlayerId = checked((ushort)(member.Slot.Value + 1)); // network id 0 is reserved
            _authenticating = true;
            try
            {
                // A false result (or exception) leaves Sessions exactly as it was. In particular,
                // never publish MemberConnected and then disconnect an unrelated live binding.
                if (!_networkAdmission.AuthenticateNetworkPlayer(connectionId, networkPlayerId,
                        authoritativePosition, reachVoxels, canAlterWorld))
                    return false;

                // Same-connection retries are rechecked by Net, not inferred from a Sessions
                // handle. Preserve synchronization/readiness and avoid a second lifecycle event.
                return alreadyConnected || _session.BindConnection(memberId, handle);
            }
            finally
            {
                _authenticating = false;
            }
        }

        public bool Disconnect(uint connectionId) => connectionId != 0 && _session.Disconnect(FromConnectionId(connectionId));

        public static TransportConnectionHandle FromConnectionId(uint connectionId)
        {
            if (connectionId == 0) throw new ArgumentOutOfRangeException(nameof(connectionId));
            return new TransportConnectionHandle("network:" + connectionId);
        }
    }
}
