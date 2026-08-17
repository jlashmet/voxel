using Game.Structures.Api;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    public sealed class WorldObjectGeneratedScene
    {
        public WorldObjectDescriptor[] Objects;
        public WorldObjectConnection[] Connections;
        public WorldObjectSceneRuntime Runtime;
    }

    /// <summary>Creates fully functional generated object scenes for structures and cave/mine chambers.</summary>
    public static class WorldObjectGeneratedSceneFactory
    {
        public static WorldObjectGeneratedScene CreateCastle(IStructureAuthoringSession geometry,
            uint worldSeed, uint parentId, in CastlePlan plan, WorldObjectStateStore state = null)
        {
            var authoring = new WorldObjectAuthoringSession(worldSeed, parentId);
            WorldObjectGeneratedContent.AuthorCastle(geometry, authoring, in plan);
            WorldObjectGeneratedExpansion.AuthorCastle(geometry, authoring, in plan);
            return Build(authoring, state);
        }

        public static WorldObjectGeneratedScene CreateMineCave(IStructureAuthoringSession geometry,
            uint worldSeed, uint parentId, DecorationBounds chamber, WorldObjectStateStore state = null)
        {
            var authoring = new WorldObjectAuthoringSession(worldSeed, parentId);
            WorldObjectGeneratedContent.AuthorMineCave(geometry, authoring, chamber);
            WorldObjectGeneratedExpansion.AuthorMineCave(geometry, authoring, chamber);
            return Build(authoring, state);
        }

        private static WorldObjectGeneratedScene Build(WorldObjectAuthoringSession authoring, WorldObjectStateStore state)
        {
            WorldObjectDescriptor[] objects = authoring.BuildObjects();
            WorldObjectConnection[] connections = authoring.BuildConnections();
            return new WorldObjectGeneratedScene
            {
                Objects = objects,
                Connections = connections,
                Runtime = new WorldObjectSceneRuntime(objects, connections, state),
            };
        }
    }
}
