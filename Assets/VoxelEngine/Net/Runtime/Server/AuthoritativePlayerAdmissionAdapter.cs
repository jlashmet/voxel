using System;
using Unity.Mathematics;
using VoxelEngine.Net.Api;

namespace VoxelEngine.Net.Runtime.Server
{
    /// <summary>Adapts the existing server authentication path without owning party identity or reconnect policy.</summary>
    public sealed class AuthoritativePlayerAdmissionAdapter : IAuthoritativePlayerAdmission
    {
        private readonly AuthoritativeServerSession _server;

        public AuthoritativePlayerAdmissionAdapter(AuthoritativeServerSession server)
        {
            _server = server ?? throw new ArgumentNullException(nameof(server));
        }

        public bool AuthenticateNetworkPlayer(uint connectionId, ushort networkPlayerId, NetworkSpawnPosition authoritativePosition, int reachVoxels, bool canAlterWorld) =>
            _server.AuthenticateConnection(
                connectionId,
                networkPlayerId,
                new int3(authoritativePosition.X, authoritativePosition.Y, authoritativePosition.Z),
                reachVoxels,
                canAlterWorld);
    }
}
