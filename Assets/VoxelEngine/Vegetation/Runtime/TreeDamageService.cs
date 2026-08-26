using Unity.Mathematics;
using VoxelEngine.Vegetation.Api;

namespace VoxelEngine.Vegetation.Runtime
{
    /// <summary>
    /// Runtime implementation of the stable semantic damage capability. The existing static
    /// services remain the optimized state/cache owners; this adapter adds no per-query allocation.
    /// </summary>
    public sealed class TreeDamageService : ITreeDamageService
    {
        public bool OverlapsWoodAabb(float3 minMetres, float3 maxMetres) =>
            ProceduralTreeWoodCollisionService.OverlapsAabb(minMetres, maxMetres);

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
