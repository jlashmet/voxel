using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>Summary of one bounded generic cave authoring pass.</summary>
    public struct CaveAuthoringResult
    {
        public int SegmentsAuthored;
        public int BranchesAuthored;
        public int ChambersAuthored;
        public int3 MainPathEnd;
        public int MainPathTraversalDistance;
        public CaveTraversalCandidateSet TraversalCandidates;
    }

    /// <summary>
    /// Public semantic capability for authoring one cave through the shared Structures implementation.
    /// </summary>
    public interface ICaveAuthoring
    {
        CaveAuthoringResult Author(
            IStructureAuthoringSession authoring,
            in CaveGenerationRequest request,
            in CaveConfig config,
            in CaveMaterialPalette palette);
    }
}
