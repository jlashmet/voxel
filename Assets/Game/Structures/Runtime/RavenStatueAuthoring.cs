using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Compatibility name retained for callers created during the raven-sculpture branch. Raven
    /// occupancy has one canonical implementation in RavenSculptureAuthoring.
    /// </summary>
    public static class RavenStatueAuthoring
    {
        public static readonly int3 LocalMin = RavenSculptureAuthoring.LocalMin;
        public static readonly int3 LocalSize = RavenSculptureAuthoring.LocalSize;

        public static void Author(IStructureAuthoringSession authoring, int3 origin) =>
            RavenSculptureAuthoring.Author(authoring, origin);
    }
}
