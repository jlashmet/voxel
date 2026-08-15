using Unity.Mathematics;

namespace VoxelEngine.Vegetation.Api
{
    /// <summary>
    /// Semantic tree collision/damage capability for gameplay. Callers work only in metre-space
    /// domain values; Vegetation.Runtime owns broadphase indexing, procedural skeleton caching and
    /// authoritative tree-state mutation.
    /// </summary>
    public interface ITreeDamageService
    {
        bool TrySweepImpact(
            float3 fromMetres,
            float3 toMetres,
            float sweepRadiusMetres,
            out float3 hitMetres,
            out int treeIndex);

        void ApplyBlast(
            float3 impactMetres,
            float blastRadiusMetres,
            float3 impulse);
    }
}
