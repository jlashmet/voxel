using VoxelEngine.Edits.Api;
using VoxelEngine.Edits.Runtime;

namespace VoxelEngine.Composition
{
    /// <summary>Composition-owned construction for deterministic edit application.</summary>
    public static class EditsComposition
    {
        public static IAlterationApplier CreateAlterationApplier() =>
            new DeterministicAlterationApplier();
    }
}
