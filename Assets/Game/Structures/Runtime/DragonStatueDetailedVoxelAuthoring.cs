using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Stable production entry point for Dragon A. V11 is the current reference-silhouette sculpt;
    /// V3-V10 remain iteration history and are not composed directly by production callers.
    /// </summary>
    public static class DragonStatueDetailedVoxelAuthoring
    {
        public static readonly int3 LocalMin = new int3(-135, 0, -110);
        public static readonly int3 LocalSize = new int3(270, 170, 235);

        public static void Author(IStructureAuthoringSession authoring, int3 origin)
        {
            DragonStatueConceptV11ReferenceSilhouetteAuthoring.Author(authoring, origin);
        }
    }
}
