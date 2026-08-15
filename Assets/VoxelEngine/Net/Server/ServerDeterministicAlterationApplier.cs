using VoxelEngine.Core.Edits;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Net.Server
{
    /// <summary>
    /// Production server bridge for semantic voxel alterations. ServerCommandProcessor publishes
    /// an event only when this shared Core applier reports a real authoritative world change.
    /// </summary>
    public sealed class ServerDeterministicAlterationApplier : IAuthoritativeAlterationApplier
    {
        public bool TryApplyAlteration(IRegionMutationStore storage, in AlterationEvent evt)
        {
            bool changed = DeterministicAlterationApplier.TryApply(
                storage,
                in evt,
                out var affectedBricks);

            if (affectedBricks.IsCreated)
                affectedBricks.Dispose();

            return changed;
        }
    }
}
