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
        /// <summary>
        /// True when the axis-aligned metre-space box overlaps surviving tree wood. Foliage is not
        /// solid and branches removed by damage stop participating immediately.
        /// </summary>
        bool OverlapsWoodAabb(float3 minMetres, float3 maxMetres);

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
