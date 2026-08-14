using Unity.Mathematics;
using VoxelEngine.Core.Edits;
using VoxelEngine.Core.Storage;

namespace VoxelEngine.Net.Server
{
    /// <summary>
    /// Production server bridge for semantic voxel alterations. ServerCommandProcessor publishes
    /// an event only when this shared Core applier reports a real authoritative world change.
    /// </summary>
    public sealed class ServerDeterministicAlterationApplier : IAuthoritativeAlterationApplier
    {
        public bool TryApplyAlteration(ref RegionTable table, ref BrickPool pool, in AlterationEvent evt)
        {
            bool changed = DeterministicAlterationApplier.TryApply(
                ref table,
                ref pool,
                in evt,
                out var affectedBricks);

            if (affectedBricks.IsCreated)
                affectedBricks.Dispose();

            return changed;
        }
    }
}
