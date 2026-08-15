using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Edits.Api
{
    /// <summary>Deterministic validation/residency/application capability for canonical alteration events.</summary>
    public interface IAlterationApplier
    {
        bool Supports(in AlterationEvent evt);
        bool HasRequiredResidency(IRegionMutationStore storage, in AlterationEvent evt);
        bool HasRequiredResidencyExcept(
            IRegionMutationStore storage, in AlterationEvent evt, int3 excludedRegion);
        bool TryApply(
            IRegionMutationStore storage,
            in AlterationEvent evt,
            out NativeList<int3> affectedBlocks);
        bool TryApplyExceptRegion(
            IRegionMutationStore storage,
            in AlterationEvent evt,
            int3 excludedRegion,
            out NativeList<int3> affectedBlocks);
    }
}
