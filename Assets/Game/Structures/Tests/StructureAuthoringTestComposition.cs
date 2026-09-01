using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace Game.Structures.Tests
{
    /// <summary>
    /// Test-only composition for exercising Game.Structures authorers with the real shared
    /// VoxelEngine runtime implementation. Production Game.Structures remains API-only.
    /// </summary>
    internal static class ShedAuthoring
    {
        public static void Author(IStructureAuthoringSession authoring, int3 origin, in ShedConfig config) =>
            Game.Structures.Runtime.ShedAuthoring.Author(
                new StructureComponentAuthoringService(), authoring, origin, in config);

        public static void Author(IStructureComponentAuthoring components, IStructureAuthoringSession authoring,
            int3 origin, in ShedConfig config) =>
            Game.Structures.Runtime.ShedAuthoring.Author(components, authoring, origin, in config);
    }

    internal static class ChurchAuthoring
    {
        public static void Author(IStructureAuthoringSession authoring, int3 origin, in ChurchConfig config) =>
            Game.Structures.Runtime.ChurchAuthoring.Author(
                new StructureComponentAuthoringService(), authoring, origin, in config);

        public static void Author(IStructureComponentAuthoring components, IStructureAuthoringSession authoring,
            int3 origin, in ChurchConfig config) =>
            Game.Structures.Runtime.ChurchAuthoring.Author(components, authoring, origin, in config);
    }

    internal static class CathedralAuthoring
    {
        public static void Author(IStructureAuthoringSession authoring, int3 origin, in CathedralConfig config) =>
            Game.Structures.Runtime.CathedralAuthoring.Author(
                new StructureComponentAuthoringService(), authoring, origin, in config);

        public static void Author(IStructureComponentAuthoring components, IStructureAuthoringSession authoring,
            int3 origin, in CathedralConfig config) =>
            Game.Structures.Runtime.CathedralAuthoring.Author(components, authoring, origin, in config);
    }

    internal static class CathedralWorldbuildingAuthoring
    {
        public static void Author(IStructureAuthoringSession authoring, int3 origin,
            in CathedralWorldbuildingConfig config) =>
            Game.Structures.Runtime.CathedralWorldbuildingAuthoring.Author(
                new StructureComponentAuthoringService(), authoring, origin, in config);

        public static void Author(IStructureComponentAuthoring components, IStructureAuthoringSession authoring,
            int3 origin, in CathedralWorldbuildingConfig config) =>
            Game.Structures.Runtime.CathedralWorldbuildingAuthoring.Author(components, authoring, origin, in config);
    }

    internal static class TempleAuthoring
    {
        public static void Author(IStructureAuthoringSession authoring, int3 origin, in TempleConfig config) =>
            Game.Structures.Runtime.TempleAuthoring.Author(
                new StructureComponentAuthoringService(), authoring, origin, in config);

        public static void Author(IStructureComponentAuthoring components, IStructureAuthoringSession authoring,
            int3 origin, in TempleConfig config) =>
            Game.Structures.Runtime.TempleAuthoring.Author(components, authoring, origin, in config);
    }
}
