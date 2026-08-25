using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Stable production entry point for Dragon A. The hero sculpture starts from the clean V7
    /// deterministic implicit/SDF-style definition, then applies the production-capture-driven V8
    /// large-form correction. Both stages write canonical 10 cm voxel state; V3-V6 are historical only.
    /// </summary>
    public static class DragonStatueDetailedVoxelAuthoring
    {
        public static readonly int3 LocalMin = new int3(-126, 0, -105);
        public static readonly int3 LocalSize = new int3(252, 174, 218);

        public static void Author(IStructureAuthoringSession authoring, int3 origin)
        {
            DragonStatueConceptV7CleanHeroAuthoring.Author(authoring, origin);
            DragonStatueConceptV8AAAFormPass.Apply(authoring, origin);
        }
    }
}
