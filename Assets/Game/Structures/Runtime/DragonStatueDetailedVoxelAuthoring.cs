using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Stable production entry point for Dragon A. V15 is the current reference-silhouette sculpt
    /// and forcibly reapplies catalogue placement styles to existing voxels: Slate/Planar for the
    /// cool body and wings, Gold/Sharp for warm armor and bone accents.
    /// </summary>
    public static class DragonStatueDetailedVoxelAuthoring
    {
        public static readonly int3 LocalMin = new int3(-135, 0, -110);
        public static readonly int3 LocalSize = new int3(270, 170, 235);

        public static void Author(IStructureAuthoringSession authoring, int3 origin)
        {
            DragonStatueConceptV15ForcedBlockSurfaceAuthoring.Author(authoring, origin);
        }
    }
}
