using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Edits.Api
{
    /// <summary>Application capability for canonical alteration events. Caller disposes affectedBlocks when created.</summary>
    public interface IAlterationApplier
    {
        bool TryApply(
            IRegionMutationStore storage,
            in AlterationEvent evt,
            out NativeList<int3> affectedBlocks);
    }
}
