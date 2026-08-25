using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Stable production entry point for Dragon A. V10 is a clean hard-surface rebuild from empty
    /// authoritative voxel state; V3-V9 remain only as iteration history and are not composed.
    /// </summary>
    public static class DragonStatueDetailedVoxelAuthoring
    {
        public static readonly int3 LocalMin = new int3(-135, 0, -110);
        public static readonly int3 LocalSize = new int3(270, 170, 235);

        public static void Author(IStructureAuthoringSession authoring, int3 origin)
        {
            DragonStatueConceptV10HardSurfaceAuthoring.Author(authoring, origin);
        }
    }
}
