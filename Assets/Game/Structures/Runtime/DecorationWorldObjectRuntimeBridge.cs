using System.Collections.Generic;
using Game.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Promotes stateful/interactable decoration placements into the shared world-object runtime while preserving
    /// GeneratedPropId identity. Purely visual decorations remain in the decoration presentation path.
    /// </summary>
    public static class DecorationWorldObjectRuntimeBridge
    {
        public static WorldObjectGeneratedScene Create(DecorationPlacement[] placements,
            WorldObjectStateStore state = null)
        {
            if (placements == null)
                return Empty(state);

            var objects = new List<WorldObjectDescriptor>();
            for (int i = 0; i < placements.Length; i++)
            {
                if (DecorationWorldObjectAdapter.TryCreate(in placements[i], out WorldObjectDescriptor descriptor))
                    objects.Add(descriptor);
            }

            WorldObjectDescriptor[] descriptors = objects.ToArray();
            WorldObjectConnection[] connections = new WorldObjectConnection[0];
            return new WorldObjectGeneratedScene
            {
                Objects = descriptors,
                Connections = connections,
                Runtime = new WorldObjectSceneRuntime(descriptors, connections, state),
            };
        }

        private static WorldObjectGeneratedScene Empty(WorldObjectStateStore state)
        {
            WorldObjectDescriptor[] objects = new WorldObjectDescriptor[0];
            WorldObjectConnection[] connections = new WorldObjectConnection[0];
            return new WorldObjectGeneratedScene
            {
                Objects = objects,
                Connections = connections,
                Runtime = new WorldObjectSceneRuntime(objects, connections, state),
            };
        }
    }
}
