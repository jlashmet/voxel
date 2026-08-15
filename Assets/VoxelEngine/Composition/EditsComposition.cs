using VoxelEngine.Edits.Api;
using VoxelEngine.Edits.Runtime;

namespace VoxelEngine.Composition
{
    /// <summary>Composition-owned construction for the concrete Edits.Runtime implementation.</summary>
    internal static class EditsComposition
    {
        internal static IAlterationApplier CreateAlterationApplier() =>
            new DeterministicAlterationApplier();
    }
}
