namespace VoxelEngine.Net.Transport
{
    /// <summary>
    /// Logical channels exposed by the concrete Unity Transport host.
    ///
    /// This enum is deliberately independent from NetworkPipeline handles. Pipeline handles are
    /// driver-local implementation details; gameplay/networking code should only reason about the
    /// delivery semantics represented here.
    /// </summary>
    public enum UtpChannel : byte
    {
        Event = 0,
        Repair = 1,
        Bulk = 2,
    }
}
