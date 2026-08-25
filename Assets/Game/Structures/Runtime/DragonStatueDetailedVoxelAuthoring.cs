using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Stable production entry point for Dragon A. The sculpture is authored on the 10 cm
    /// canonical voxel grid from deterministic implicit/SDF-style volumes. Each production-reviewed
    /// pass owns a tightly scoped replacement; rejected silhouettes are removed before successors
    /// are authored.
    /// </summary>
    public static class DragonStatueDetailedVoxelAuthoring
    {
        // Expanded for the V6 open tail sweep and full hero-wing envelope. These bounds are also the
        // World Builder placement bounds, so every authored voxel must fit inside them.
        public static readonly int3 LocalMin = new int3(-112, 0, -100);
        public static readonly int3 LocalSize = new int3(232, 178, 214);

        public static void Author(IStructureAuthoringSession authoring, int3 origin)
        {
            DragonStatueConceptV3Authoring.Author(authoring, origin);
            DragonStatueConceptV4SilhouettePass.Apply(authoring, origin);
            DragonStatueConceptV5ProportionPass.Apply(authoring, origin);
            DragonStatueConceptV6HeroSilhouettePass.Apply(authoring, origin);
        }
    }
}
