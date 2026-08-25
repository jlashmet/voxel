using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Stable Model Viewer entry point for Dragon A. The actual sculpture is owned by the current
    /// reference-driven concept authoring so no legacy anatomy remains underneath later passes.
    /// </summary>
    public static class DragonStatueDetailedVoxelAuthoring
    {
        public static readonly int3 LocalMin = DragonStatueConceptV3Authoring.LocalMin;
        public static readonly int3 LocalSize = DragonStatueConceptV3Authoring.LocalSize;

        public static void Author(IStructureAuthoringSession authoring, int3 origin)
        {
            DragonStatueConceptV3Authoring.Author(authoring, origin);
        }
    }
}
