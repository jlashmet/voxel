namespace VoxelEngine.Net.Api
{
    /// <summary>Transient network spawn position. This is transport/replication state, not durable gameplay identity.</summary>
    public readonly struct NetworkSpawnPosition
    {
        public int X { get; }
        public int Y { get; }
        public int Z { get; }

        public NetworkSpawnPosition(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    /// <summary>
    /// Lower-level authoritative admission seam. Connection and network-player ids are transient
    /// transport/replication identities and must not be persisted as party identity.
    /// </summary>
    public interface IAuthoritativePlayerAdmission
    {
        bool AuthenticateNetworkPlayer(
            uint connectionId,
            ushort networkPlayerId,
            NetworkSpawnPosition authoritativePosition,
            int reachVoxels,
            bool canAlterWorld);
    }
}
