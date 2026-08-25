using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Stable production entry point for Dragon A. V9 is a clean reference-proportion rebuild from
    /// empty authoritative voxel state; V3-V8 remain only as iteration history and are not composed.
    /// </summary>
    public static class DragonStatueDetailedVoxelAuthoring
    {
        public static readonly int3 LocalMin = new int3(-130, 0, -105);
        public static readonly int3 LocalSize = new int3(260, 170, 220);

        public static void Author(IStructureAuthoringSession authoring, int3 origin)
        {
            DragonStatueConceptV9ReferenceAuthoring.Author(authoring, origin);
        }
    }
}
