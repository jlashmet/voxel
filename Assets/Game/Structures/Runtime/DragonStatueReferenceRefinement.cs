using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Kept as a compatibility hook for Model Viewer. Dragon A is now authored completely by
    /// DragonStatueConceptV3Authoring through DragonStatueDetailedVoxelAuthoring, so there is no
    /// destructive post-pass and no hidden legacy anatomy underneath the reference sculpt.
    /// </summary>
    public static class DragonStatueReferenceRefinement
    {
        public static void Apply(IStructureAuthoringSession authoring, int3 origin)
        {
            if (authoring == null) throw new System.ArgumentNullException(nameof(authoring));
        }
    }
}
