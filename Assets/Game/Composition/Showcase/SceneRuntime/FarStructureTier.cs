namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Showcase composition selection state for structure presentations. Rendering receives the
    /// generic FarFeatureTier after the adapter has applied this game-owned policy.
    /// </summary>
    public enum FarStructureTier : byte
    {
        Culled = 0,
        Mid = 1,
        Far = 2,
        Horizon = 3
    }
}
