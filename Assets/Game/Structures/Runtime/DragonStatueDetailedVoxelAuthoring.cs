using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Stable Model Viewer entry point for Dragon A. The sculpture is authored on the 10 cm
    /// canonical voxel grid. Each production-reviewed pass owns a tightly scoped replacement;
    /// rejected silhouettes are removed before their successors are authored.
    /// </summary>
    public static class DragonStatueDetailedVoxelAuthoring
    {
        public static readonly int3 LocalMin = DragonStatueConceptV3Authoring.LocalMin;
        public static readonly int3 LocalSize = DragonStatueConceptV3Authoring.LocalSize;

        public static void Author(IStructureAuthoringSession authoring, int3 origin)
        {
            DragonStatueConceptV3Authoring.Author(authoring, origin);
            DragonStatueConceptV4SilhouettePass.Apply(authoring, origin);
            DragonStatueConceptV5ProportionPass.Apply(authoring, origin);
        }
    }
}
