using System;
using Game.Sessions.Api;
using VoxelEngine.Net.Api;

namespace Game.Sessions.Runtime
{
    /// <summary>
    /// Bridges durable Sessions identity to lower-level network admission. The generated network
    /// player id is a transient replication key derived from the stable slot; it is never party identity.
    /// </summary>
    public sealed class SessionNetworkAdmissionAdapter
    {
        private readonly PartySession _session;
        private readonly IAuthoritativePlayerAdmission _networkAdmission;

        public SessionNetworkAdmissionAdapter(PartySession session, IAuthoritativePlayerAdmission networkAdmission)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _networkAdmission = networkAdmission ?? throw new ArgumentNullException(nameof(networkAdmission));
        }

        public bool Authenticate(PartyMemberId memberId, uint connectionId, NetworkSpawnPosition authoritativePosition, int reachVoxels, bool canAlterWorld)
        {
            if (connectionId == 0 || reachVoxels <= 0 || !_session.TryGetMember(memberId, out PartyMemberSnapshot member))
                return false;
            if (member.Slot.Value >= ushort.MaxValue)
                return false;

            ushort networkPlayerId = checked((ushort)(member.Slot.Value + 1)); // network id 0 is reserved
            TransportConnectionHandle handle = FromConnectionId(connectionId);
            if (!_session.BindConnection(memberId, handle))
                return false;

            if (_networkAdmission.AuthenticateNetworkPlayer(connectionId, networkPlayerId, authoritativePosition, reachVoxels, canAlterWorld))
                return true;

            _session.Disconnect(handle);
            return false;
        }

        public bool Disconnect(uint connectionId) => connectionId != 0 && _session.Disconnect(FromConnectionId(connectionId));

        public static TransportConnectionHandle FromConnectionId(uint connectionId)
        {
            if (connectionId == 0) throw new ArgumentOutOfRangeException(nameof(connectionId));
            return new TransportConnectionHandle("network:" + connectionId);
        }
    }
}
