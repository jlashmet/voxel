namespace VoxelEngine.Net.Runtime.Server
{
    /// <summary>Shared fixed-clock constants for authoritative networking/simulation handoff.</summary>
    public static class AuthoritativeTickConfig
    {
        public const uint TickRateHz = 30;
        public const float TickDurationMs = 1000f / TickRateHz;
        public const uint RollbackWindowTicks = 15;
        public const uint HotEventRetentionTicks = 60;
    }
}
