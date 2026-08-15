using Unity.Mathematics;
using VoxelEngine.Vegetation.Api;

namespace VoxelEngine.Vegetation.Runtime
{
    /// <summary>
    /// Runtime implementation of the stable semantic damage capability. The existing static
    /// service remains the optimized state/cache owner; this adapter adds no per-query allocation.
    /// </summary>
    public sealed class TreeDamageService : ITreeDamageService
    {
        public bool TrySweepImpact(
            float3 fromMetres,
            float3 toMetres,
            float sweepRadiusMetres,
            out float3 hitMetres,
            out int treeIndex) =>
            ProceduralTreeDamageService.TrySweepImpact(
                fromMetres, toMetres, sweepRadiusMetres, out hitMetres, out treeIndex);

        public void ApplyBlast(
            float3 impactMetres,
            float blastRadiusMetres,
            float3 impulse) =>
            ProceduralTreeDamageService.ApplyBlast(impactMetres, blastRadiusMetres, impulse);
    }
}
