using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Stable production entry point for Dragon A. The hero sculpture is authored from one clean
    /// deterministic implicit/SDF-style definition, sampled into canonical 10 cm voxel state.
    /// Historical V3-V6 passes remain in source only for iteration provenance and are not composed.
    /// </summary>
    public static class DragonStatueDetailedVoxelAuthoring
    {
        // V7 owns the entire production object. Bounds include the hero wing arch and foreground tail
        // with intentional safety margin and are shared by World Builder placement and Model Viewer.
        public static readonly int3 LocalMin = new int3(-126, 0, -105);
        public static readonly int3 LocalSize = new int3(252, 174, 218);

        public static void Author(IStructureAuthoringSession authoring, int3 origin)
        {
            DragonStatueConceptV7CleanHeroAuthoring.Author(authoring, origin);
        }
    }
}
