using System;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Process-local notification bridge between authoritative WorldObject scene registries and optional
    /// presentation/composition consumers. Registry ownership and persistent state remain completely independent
    /// from Unity presentation; listeners may come and go without changing scene identity or save data.
    /// </summary>
    public static class WorldObjectSceneLifecycle
    {
        public static event Action<WorldObjectSceneRegistry, uint, WorldObjectGeneratedScene> Loaded;
        public static event Action<WorldObjectSceneRegistry, uint> Unloaded;

        internal static void PublishLoaded(WorldObjectSceneRegistry registry, uint parentId,
            WorldObjectGeneratedScene scene) => Loaded?.Invoke(registry, parentId, scene);

        internal static void PublishUnloaded(WorldObjectSceneRegistry registry, uint parentId) =>
            Unloaded?.Invoke(registry, parentId);
    }
}
