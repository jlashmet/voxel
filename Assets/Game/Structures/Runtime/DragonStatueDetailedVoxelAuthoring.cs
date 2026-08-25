using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Stable production entry point for Dragon A. V14 is the current reference-silhouette sculpt
    /// and explicitly restricts visible geometry to block-authored Planar/Sharp surfaces: Slate for
    /// the cool body/wings and Gold for warm armor/bone accents. Earlier versions remain history.
    /// </summary>
    public static class DragonStatueDetailedVoxelAuthoring
    {
        public static readonly int3 LocalMin = new int3(-135, 0, -110);
        public static readonly int3 LocalSize = new int3(270, 170, 235);

        public static void Author(IStructureAuthoringSession authoring, int3 origin)
        {
            DragonStatueConceptV14BlockSurfaceAuthoring.Author(authoring, origin);
        }
    }
}
