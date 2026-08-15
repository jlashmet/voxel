namespace VoxelEngine.Net.Runtime.Transport
{
    /// <summary>
    /// Logical channels exposed by the concrete Unity Transport host. Values are internal API,
    /// not protocol message-kind values; NetworkPipeline handles remain driver-local details.
    /// </summary>
    public enum UtpChannel : byte
    {
        Event = 0,
        Ephemeral = 1,
        Repair = 2,
        Bulk = 3,
    }
}
