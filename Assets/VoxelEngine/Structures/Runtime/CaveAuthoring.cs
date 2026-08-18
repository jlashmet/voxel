using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>Summary of one bounded generic cave authoring pass.</summary>
    public struct CaveAuthoringResult
    {
        public int SegmentsAuthored;
        public int BranchesAuthored;
        public int ChambersAuthored;
        public int3 MainPathEnd;
    }

    /// <summary>
    /// Stable public entry point for reusable cave authoring. Validation and overflow checks stay at
    /// the API boundary; the deterministic integer network implementation is internal and shared by
    /// standalone, structure-attached, and underground requests.
    /// </summary>
    public static class CaveAuthoring
    {
        public static CaveAuthoringResult Author(
            IStructureAuthoringSession authoring,
            in CaveGenerationRequest request,
            in CaveConfig config,
            in CaveMaterialPalette palette)
        {
            if (authoring == null) throw new System.ArgumentNullException(nameof(authoring));
            if (!request.IsWellFormed)
                throw new System.ArgumentException("Cave generation request is invalid.", nameof(request));
            if (!config.IsWellFormed)
                throw new System.ArgumentException("Cave configuration is invalid.", nameof(config));
            if (!request.TryGetWorldBounds(in config, out _))
                throw new System.ArgumentException("Cave bounds overflow world coordinates.", nameof(request));
            if (!request.EntranceFitsBounds(in config))
                throw new System.ArgumentException(
                    "Cave entrance/clearance exceeds the declared cave bounds.", nameof(request));

            return CaveNetworkAuthoringCore.Author(
                authoring,
                in request,
                in config,
                in palette);
        }
    }
}
